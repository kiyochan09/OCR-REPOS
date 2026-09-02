using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    public class StructuredTableRow
    {
        public int PageNumber { get; set; }
        public string TableName { get; set; } = "";
        public int RowIndex { get; set; }
        public List<string> Cells { get; set; } = new();
    }

    public class StructuredTable
    {
        public int PageNumber { get; set; }
        public string TableName { get; set; } = "";
        public int ColumnCount { get; set; }
        public int RowCount { get; set; }
        public List<StructuredTableRow> Rows { get; set; } = new();
        public List<TableMergeSpan> MergeSpans { get; set; } = new();
    }

    public static class TableGridExtractor
    {
        /// <summary>
        /// ユーザーが設定・配置した表領域と縦横の罫線に基づき、データを消失させずに
        /// 各セルのOCRテキストを正確に行列（2Dグリッド）として抽出します。
        /// ※自動的なセル結合・データ消去は行わず、セル結合はユーザー操作に委ねます。
        /// </summary>
        public static List<StructuredTable> ExtractStructuredTables(
            int pageNumber,
            List<OcrDisplayItem> ocrItems,
            List<OcrRegion> regions,
            List<AutoLayoutRegion>? autoRegions = null,
            string docType = "japanese")
        {
            var result = new List<StructuredTable>();
            bool isWestern = string.Equals(docType, "western", StringComparison.OrdinalIgnoreCase);

            var tableRegions = regions.Where(r => r.Type == "table").ToList();

            if (tableRegions.Count == 0 && autoRegions != null && autoRegions.Count > 0)
            {
                tableRegions = autoRegions
                    .Where(r => r.Type == "table")
                    .Select(OcrProcessor.ConvertAutoLayoutRegion)
                    .ToList();
            }

            if (tableRegions.Count == 0)
            {
                var tableItems = ocrItems.Where(i =>
                    (autoRegions != null && OcrProcessor.FindAutoLayoutRegionType(i, autoRegions) == "table")
                ).ToList();

                if (tableItems.Count > 0)
                {
                    var fallbackTable = new StructuredTable
                    {
                        PageNumber = pageNumber,
                        TableName = "表1",
                        ColumnCount = 1,
                        RowCount = tableItems.Count
                    };

                    int rIdx = 1;
                    foreach (var item in tableItems.OrderBy(i => i.Y).ThenBy(i => i.X))
                    {
                        fallbackTable.Rows.Add(new StructuredTableRow
                        {
                            PageNumber = pageNumber,
                            TableName = "表1",
                            RowIndex = rIdx++,
                            Cells = new List<string> { item.Text.Trim() }
                        });
                    }
                    result.Add(fallbackTable);
                }

                return result;
            }

            int tableNumber = 1;
            foreach (var table in tableRegions)
            {
                string tableName = (string.IsNullOrWhiteSpace(table.Name) || table.Name == "表" || table.Name == "＋ 表")
                    ? $"表{tableNumber}"
                    : table.Name;
                tableNumber++;

                // テーブル領域内のOCRアイテム
                var insideItems = ocrItems.Where(item =>
                {
                    int cx = item.X + item.Width / 2;
                    int cy = item.Y + item.Height / 2;
                    return cx >= table.X - 4 && cx <= table.X + table.Width + 4 &&
                           cy >= table.Y - 4 && cy <= table.Y + table.Height + 4;
                }).ToList();

                table.EnsureRuleLines();

                // 1. 縦罫線から列境界 (X座標群) を抽出
                var vLinePosList = table.RuleLines
                    .Where(l => l.IsVertical)
                    .Select(l => l.Pos)
                    .Where(x => x > table.X + 4 && x < table.X + table.Width - 4)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                vLinePosList = ClusterCoordinates(vLinePosList, 6);

                var colBounds = new List<int> { table.X };
                colBounds.AddRange(vLinePosList);
                colBounds.Add(table.X + table.Width);

                // 2. 横罫線から行境界 (Y座標群) を抽出
                var hLinePosList = table.RuleLines
                    .Where(l => !l.IsVertical)
                    .Select(l => l.Pos)
                    .Where(y => y > table.Y + 4 && y < table.Y + table.Height - 4)
                    .Distinct()
                    .OrderBy(y => y)
                    .ToList();

                hLinePosList = ClusterCoordinates(hLinePosList, 6);

                var rowBounds = new List<int> { table.Y };
                rowBounds.AddRange(hLinePosList);
                rowBounds.Add(table.Y + table.Height);

                int numCols = colBounds.Count - 1;
                int numRows = rowBounds.Count - 1;

                if (numCols <= 0) numCols = 1;
                if (numRows <= 0) numRows = 1;

                // 3. 各格子セル (r, c) に属するOCR項目を収集
                var cellItems = new List<OcrDisplayItem>[numRows, numCols];
                for (int r = 0; r < numRows; r++)
                    for (int c = 0; c < numCols; c++)
                        cellItems[r, c] = new List<OcrDisplayItem>();

                foreach (var item in insideItems)
                {
                    int cx = item.X + item.Width / 2;
                    int cy = item.Y + item.Height / 2;

                    // 行インデックス特定
                    int targetRow = -1;
                    for (int r = 0; r < numRows; r++)
                    {
                        if (cy >= rowBounds[r] && (r == numRows - 1 || cy < rowBounds[r + 1]))
                        {
                            targetRow = r;
                            break;
                        }
                    }
                    if (targetRow < 0)
                        targetRow = Math.Min(numRows - 1, Math.Max(0, (int)((long)(cy - table.Y) * numRows / Math.Max(1, table.Height))));

                    // 列インデックス特定
                    int targetCol = -1;
                    for (int c = 0; c < numCols; c++)
                    {
                        if (cx >= colBounds[c] && (c == numCols - 1 || cx < colBounds[c + 1]))
                        {
                            targetCol = c;
                            break;
                        }
                    }
                    if (targetCol < 0)
                        targetCol = Math.Min(numCols - 1, Math.Max(0, (int)((long)(cx - table.X) * numCols / Math.Max(1, table.Width))));

                    cellItems[targetRow, targetCol].Add(item);
                }

                // 4. 各セル内のテキストを自然に結合（各セル独立して保持、データ消失防止）
                var structuredTable = new StructuredTable
                {
                    PageNumber = pageNumber,
                    TableName = tableName,
                    ColumnCount = numCols,
                    RowCount = numRows
                };

                for (int r = 0; r < numRows; r++)
                {
                    var rowData = new StructuredTableRow
                    {
                        PageNumber = pageNumber,
                        TableName = tableName,
                        RowIndex = r + 1
                    };

                    for (int c = 0; c < numCols; c++)
                    {
                        var itemsInCell = cellItems[r, c];
                        string cellText = JoinCellTextItems(itemsInCell, isWestern);
                        rowData.Cells.Add(cellText);
                    }

                    structuredTable.Rows.Add(rowData);
                }

                result.Add(structuredTable);
            }

            return result;
        }

        /// <summary>
        /// セル内の複数OCR項目を読み順で自然に連結し、文章が途切れないようにします。
        /// </summary>
        public static string JoinCellTextItems(List<OcrDisplayItem> items, bool isWestern)
        {
            if (items == null || items.Count == 0) return "";
            if (items.Count == 1) return items[0].Text.Trim();

            // 行（Y座標）でグループ化して読み順ソート
            var rows = new List<List<OcrDisplayItem>>();
            foreach (var item in items.OrderBy(i => i.Y + i.Height / 2.0))
            {
                double centerY = item.Y + item.Height / 2.0;
                List<OcrDisplayItem>? targetRow = null;
                double bestDist = double.MaxValue;

                foreach (var row in rows)
                {
                    double rowCenterY = row.Average(x => x.Y + x.Height / 2.0);
                    double dist = Math.Abs(centerY - rowCenterY);
                    double minH = Math.Min(item.Height, row.Min(r => r.Height));

                    if (dist < minH * 0.45 && dist < bestDist)
                    {
                        targetRow = row;
                        bestDist = dist;
                    }
                }

                if (targetRow == null)
                {
                    targetRow = new List<OcrDisplayItem>();
                    rows.Add(targetRow);
                }
                targetRow.Add(item);
            }

            // 各行内をX座標順にソート
            foreach (var row in rows)
            {
                row.Sort((a, b) => a.X.CompareTo(b.X));
            }

            // 行順にソート
            rows.Sort((a, b) =>
            {
                double aY = a.Average(x => x.Y + x.Height / 2.0);
                double bY = b.Average(x => x.Y + x.Height / 2.0);
                return aY.CompareTo(bY);
            });

            // 行ごとのテキストを作成
            var lineTexts = new List<string>();
            foreach (var row in rows)
            {
                var rowTokens = row.Select(r => r.Text.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
                if (rowTokens.Count == 0) continue;

                if (isWestern)
                {
                    lineTexts.Add(string.Join(" ", rowTokens));
                }
                else
                {
                    // 和文: 英数字同士の間のみ空白を入れ、日本語間は直接連結
                    var sb = new StringBuilder();
                    for (int i = 0; i < rowTokens.Count; i++)
                    {
                        if (i > 0)
                        {
                            char prevLast = rowTokens[i - 1].Last();
                            char currFirst = rowTokens[i].First();
                            if (IsAsciiAlnum(prevLast) && IsAsciiAlnum(currFirst))
                                sb.Append(' ');
                        }
                        sb.Append(rowTokens[i]);
                    }
                    lineTexts.Add(sb.ToString());
                }
            }

            if (lineTexts.Count == 0) return "";
            if (lineTexts.Count == 1) return lineTexts[0];

            // 複数行の文章を継続連結
            if (isWestern)
            {
                return string.Join(" ", lineTexts);
            }
            else
            {
                // 和文の場合、行間も自然に連結（英数字境界のみ空白）
                var sb = new StringBuilder();
                for (int i = 0; i < lineTexts.Count; i++)
                {
                    if (i > 0)
                    {
                        char prevLast = lineTexts[i - 1].Last();
                        char currFirst = lineTexts[i].First();
                        if (IsAsciiAlnum(prevLast) && IsAsciiAlnum(currFirst))
                            sb.Append(' ');
                    }
                    sb.Append(lineTexts[i]);
                }
                return sb.ToString();
            }
        }

        private static bool IsAsciiAlnum(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
        }

        private static List<int> ClusterCoordinates(List<int> coords, int tolerance)
        {
            if (coords.Count <= 1) return coords;

            var clusters = new List<List<int>>();
            foreach (var val in coords)
            {
                if (clusters.Count == 0 || Math.Abs(val - clusters.Last().Average()) > tolerance)
                {
                    clusters.Add(new List<int> { val });
                }
                else
                {
                    clusters.Last().Add(val);
                }
            }

            return clusters.Select(c => (int)Math.Round(c.Average())).ToList();
        }
    }
}
