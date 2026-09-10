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
            string docType = "japanese",
            int startTableNumber = 1)
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
                return result;
            }

            // 見開きや段組を考慮し、自然な読み順（左ページ/カラム -> 右ページ/カラム、各領域内は上から下）に整列
            tableRegions = tableRegions
                .OrderBy(t => t.X < 1650 ? 0 : 1)
                .ThenBy(t => t.Y)
                .ToList();

            int tableNumber = startTableNumber;
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

                // 縦罫線が未登録の場合、autoRegionsのVerticalLinesから補完
                if (vLinePosList.Count == 0 && autoRegions != null)
                {
                    var matchingAuto = autoRegions.FirstOrDefault(a => a.Type == "table" &&
                        Math.Abs(a.X - table.X) < 30 && Math.Abs(a.Y - table.Y) < 30);
                    if (matchingAuto != null && matchingAuto.VerticalLines.Count > 0)
                    {
                        vLinePosList = matchingAuto.VerticalLines
                            .Where(x => x > table.X + 4 && x < table.X + table.Width - 4)
                            .Distinct().OrderBy(x => x).ToList();
                    }
                }

                // 縦線がない場合、テキスト行内の明瞭な水平ギャップから列境界を推定
                if (vLinePosList.Count == 0 && insideItems.Count >= 4)
                {
                    var gaps = new List<int>();
                    var groupedByY = insideItems.GroupBy(it => (int)Math.Round((it.Y + it.Height / 2.0) / 35.0));
                    foreach (var g in groupedByY)
                    {
                        var rowItems = g.OrderBy(it => it.X).ToList();
                        for (int i = 0; i < rowItems.Count - 1; i++)
                        {
                            int right1 = rowItems[i].X + rowItems[i].Width;
                            int left2 = rowItems[i + 1].X;
                            if (left2 - right1 >= 25)
                            {
                                gaps.Add((right1 + left2) / 2);
                            }
                        }
                    }
                    if (gaps.Count >= 2)
                    {
                        var clusteredGaps = ClusterCoordinates(gaps.OrderBy(x => x).ToList(), 30);
                        foreach (var cg in clusteredGaps)
                        {
                            if (gaps.Count(g => Math.Abs(g - cg) <= 30) >= 2 &&
                                cg > table.X + 25 && cg < table.X + table.Width - 25)
                            {
                                vLinePosList.Add(cg);
                            }
                        }
                        vLinePosList = vLinePosList.Distinct().OrderBy(x => x).ToList();
                    }
                }

                vLinePosList = ClusterCoordinates(vLinePosList, 6);

                var colBounds = new List<int> { table.X };
                colBounds.AddRange(vLinePosList);
                colBounds.Add(table.X + table.Width);

                // 縦罫線（列境界）をまたぐOCRアイテムがあれば、境界位置で分割して各列に正しく配分
                // （隣り合う列の値が1つのセルに混入するのを防止）
                if (vLinePosList.Count > 0)
                {
                    var splitItems = new List<OcrDisplayItem>();
                    foreach (var it in insideItems)
                    {
                        splitItems.AddRange(SplitItemByVerticalLines(it, vLinePosList));
                    }
                    insideItems = splitItems;
                }

                // 2. 横罫線から行境界 (Y座標群) を抽出
                var hLinePosList = table.RuleLines
                    .Where(l => !l.IsVertical)
                    .Select(l => l.Pos)
                    .Where(y => y > table.Y + 4 && y < table.Y + table.Height - 4)
                    .Distinct()
                    .OrderBy(y => y)
                    .ToList();

                // 横罫線が未登録の場合、autoRegionsのHorizontalLinesから補完
                if (hLinePosList.Count == 0 && autoRegions != null)
                {
                    var matchingAuto = autoRegions.FirstOrDefault(a => a.Type == "table" &&
                        Math.Abs(a.X - table.X) < 30 && Math.Abs(a.Y - table.Y) < 30);
                    if (matchingAuto != null && matchingAuto.HorizontalLines.Count > 0)
                    {
                        hLinePosList = matchingAuto.HorizontalLines
                            .Where(y => y > table.Y + 4 && y < table.Y + table.Height - 4)
                            .Distinct().OrderBy(y => y).ToList();
                    }
                }

                hLinePosList = ClusterCoordinates(hLinePosList, 6);

                var baseHBounds = new List<int> { table.Y };
                baseHBounds.AddRange(hLinePosList);
                baseHBounds.Add(table.Y + table.Height);

                // 横罫線で区切られた各帯（バンド）内で、複数テキスト行が存在する場合は自動サブ分割
                var finalRowBounds = new List<int>();
                for (int b = 0; b < baseHBounds.Count - 1; b++)
                {
                    int yTop = baseHBounds[b];
                    int yBottom = baseHBounds[b + 1];

                    var bandItems = insideItems.Where(it =>
                    {
                        int cy = it.Y + it.Height / 2;
                        return cy >= yTop - 2 && cy < yBottom + 2;
                    }).OrderBy(it => it.Y + it.Height / 2.0).ToList();

                    if (bandItems.Count == 0)
                    {
                        if (finalRowBounds.Count == 0) finalRowBounds.Add(yTop);
                        finalRowBounds.Add(yBottom);
                        continue;
                    }

                    // テキスト行クラスタリング（垂直中心Y座標の近さでグルーピング）
                    var textRows = new List<List<OcrDisplayItem>>();
                    foreach (var item in bandItems)
                    {
                        double cy = item.Y + item.Height / 2.0;
                        List<OcrDisplayItem>? targetRow = null;
                        double bestDist = double.MaxValue;

                        foreach (var tr in textRows)
                        {
                            double trCy = tr.Average(x => x.Y + x.Height / 2.0);
                            double dist = Math.Abs(cy - trCy);
                            double minH = Math.Min(item.Height, tr.Min(x => x.Height));

                            if (dist < minH * 0.55 && dist < bestDist)
                            {
                                targetRow = tr;
                                bestDist = dist;
                            }
                        }

                        if (targetRow == null)
                        {
                            targetRow = new List<OcrDisplayItem>();
                            textRows.Add(targetRow);
                        }
                        targetRow.Add(item);
                    }

                    if (textRows.Count <= 1)
                    {
                        if (finalRowBounds.Count == 0) finalRowBounds.Add(yTop);
                        finalRowBounds.Add(yBottom);
                    }
                    else
                    {
                        textRows.Sort((a, b) => a.Average(x => x.Y + x.Height / 2.0).CompareTo(b.Average(x => x.Y + x.Height / 2.0)));
                        if (finalRowBounds.Count == 0) finalRowBounds.Add(yTop);

                        for (int i = 0; i < textRows.Count - 1; i++)
                        {
                            double tr1Bottom = textRows[i].Max(x => x.Y + x.Height);
                            double tr2Top = textRows[i + 1].Min(x => x.Y);
                            int midY = (int)Math.Round((tr1Bottom + tr2Top) / 2.0);
                            if (midY <= finalRowBounds.Last()) midY = finalRowBounds.Last() + 1;
                            finalRowBounds.Add(midY);
                        }
                        finalRowBounds.Add(yBottom);
                    }
                }

                var rowBounds = finalRowBounds;
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

        /// <summary>
        /// 縦罫線（列境界）をまたぐOCRアイテムを、境界位置付近の空白または文字幅比率で分割します。
        /// これにより、隣り合う列の値が1つのセルに混入するのを防止します。
        /// </summary>
        public static List<OcrDisplayItem> SplitItemByVerticalLines(OcrDisplayItem item, List<int> vLines)
        {
            var currentItems = new List<OcrDisplayItem> { item };

            foreach (int vX in vLines)
            {
                var nextItems = new List<OcrDisplayItem>();
                foreach (var it in currentItems)
                {
                    int itLeft = it.X;
                    int itRight = it.X + it.Width;

                    // 境界線がアイテム内部を横切っているか（マージン12px以上）
                    if (vX > itLeft + 12 && vX < itRight - 12)
                    {
                        double ratio = (double)(vX - itLeft) / Math.Max(1, it.Width);
                        string text = it.Text;
                        int splitIdx = FindBestSplitIndex(text, ratio);

                        if (splitIdx > 0 && splitIdx < text.Length)
                        {
                            string leftText = text.Substring(0, splitIdx).Trim();

                            // 区切りの空白文字をスキップ
                            int rightStart = splitIdx;
                            while (rightStart < text.Length && (char.IsWhiteSpace(text[rightStart]) || text[rightStart] == '　'))
                                rightStart++;

                            string rightText = rightStart < text.Length ? text.Substring(rightStart).Trim() : "";
                            int leftWidth = vX - itLeft;
                            int rightWidth = itRight - vX;

                            if (!string.IsNullOrEmpty(leftText))
                            {
                                nextItems.Add(new OcrDisplayItem
                                {
                                    Text = leftText,
                                    X = itLeft,
                                    Y = it.Y,
                                    Width = leftWidth,
                                    Height = it.Height,
                                    IsVertical = it.IsVertical
                                });
                            }

                            if (!string.IsNullOrEmpty(rightText))
                            {
                                nextItems.Add(new OcrDisplayItem
                                {
                                    Text = rightText,
                                    X = vX,
                                    Y = it.Y,
                                    Width = rightWidth,
                                    Height = it.Height,
                                    IsVertical = it.IsVertical
                                });
                            }
                            continue;
                        }
                    }

                    nextItems.Add(it);
                }
                currentItems = nextItems;
            }

            return currentItems;
        }

        private static double GetCharWeight(char c)
        {
            if (c <= 127) return 1.0;
            return 2.0; // 全角文字（漢字・ひらがな・カタカナ等）
        }

        private static int FindBestSplitIndex(string text, double ratio)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= 1) return -1;

            double totalWeight = text.Sum(c => GetCharWeight(c));
            double targetWeight = totalWeight * ratio;

            var cumWeights = new double[text.Length];
            double cur = 0;
            for (int i = 0; i < text.Length; i++)
            {
                cur += GetCharWeight(text[i]);
                cumWeights[i] = cur;
            }

            // 1. 空白文字（半角スペース、全角スペース、タブ）を探索
            int bestWs = -1;
            double minWsDist = double.MaxValue;
            for (int i = 1; i < text.Length - 1; i++)
            {
                if (char.IsWhiteSpace(text[i]) || text[i] == '　')
                {
                    double dist = Math.Abs(cumWeights[i - 1] - targetWeight);
                    if (dist < minWsDist)
                    {
                        minWsDist = dist;
                        bestWs = i;
                    }
                }
            }

            if (bestWs != -1 && minWsDist <= totalWeight * 0.25)
            {
                return bestWs;
            }

            // 2. 空白が近傍にない場合は、累積文字幅が targetWeight に最も近い境界で分割
            int bestIdx = 1;
            double minDiff = double.MaxValue;
            for (int i = 1; i < text.Length; i++)
            {
                double diff = Math.Abs(cumWeights[i - 1] - targetWeight);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    bestIdx = i;
                }
            }

            return bestIdx;
        }
    }
}
