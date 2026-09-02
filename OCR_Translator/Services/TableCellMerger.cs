using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    public class TableMergeSpan
    {
        public int StartCol { get; set; }
        public int StartRow { get; set; }
        public int ColSpan { get; set; }
        public int RowSpan { get; set; }

        /// <summary>
        /// 結合後の集約テキスト（左上・上が空白でも範囲内の全データを保持）
        /// </summary>
        public string MergedText { get; set; } = "";

        /// <summary>
        /// 結合前の各セルの元データを保持（結合解除時に完全復元）
        /// </summary>
        public Dictionary<(int col, int row), string> OriginalCellTexts { get; set; } = new();

        public TableMergeSpan(int startCol, int startRow, int colSpan, int rowSpan)
        {
            StartCol = startCol;
            StartRow = startRow;
            ColSpan = Math.Max(1, colSpan);
            RowSpan = Math.Max(1, rowSpan);
        }

        public bool Contains(int col, int row)
        {
            return col >= StartCol && col < StartCol + ColSpan &&
                   row >= StartRow && row < StartRow + RowSpan;
        }

        public bool IsTopLeft(int col, int row)
        {
            return col == StartCol && row == StartRow;
        }

        public bool OverlapsWith(int minCol, int minRow, int maxCol, int maxRow)
        {
            return !(StartCol + ColSpan - 1 < minCol ||
                     StartCol > maxCol ||
                     StartRow + RowSpan - 1 < minRow ||
                     StartRow > maxRow);
        }
    }

    public static class TableCellMerger
    {
        /// <summary>
        /// DataGridViewで選択中の複数セルを1つに結合します。
        /// 範囲の左上や上が空白であっても、範囲内にあるすべてのテキストデータを消失させずに
        /// 結合後のセルに集約・表示します。
        /// </summary>
        public static bool MergeSelectedCells(DataGridView dgv, List<TableMergeSpan> mergeSpans)
        {
            if (dgv.SelectedCells.Count <= 1) return false;

            int minCol = int.MaxValue;
            int maxCol = int.MinValue;
            int minRow = int.MaxValue;
            int maxRow = int.MinValue;

            foreach (DataGridViewCell cell in dgv.SelectedCells)
            {
                minCol = Math.Min(minCol, cell.ColumnIndex);
                maxCol = Math.Max(maxCol, cell.ColumnIndex);
                minRow = Math.Min(minRow, cell.RowIndex);
                maxRow = Math.Max(maxRow, cell.RowIndex);
            }

            int colSpan = maxCol - minCol + 1;
            int rowSpan = maxRow - minRow + 1;

            if (colSpan <= 1 && rowSpan <= 1) return false;

            // 範囲内のすべてのセルからテキストを収集（左上・上が空白でも、下や右など範囲内にある全データを保持）
            var cellsWithData = new List<(int col, int row, string text)>();
            var span = new TableMergeSpan(minCol, minRow, colSpan, rowSpan);

            for (int r = minRow; r <= maxRow; r++)
            {
                for (int c = minCol; c <= maxCol; c++)
                {
                    string rawVal = dgv.Rows[r].Cells[c].Value?.ToString() ?? "";
                    span.OriginalCellTexts[(c, r)] = rawVal;

                    string trimmed = rawVal.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        cellsWithData.Add((c, r, trimmed));
                    }
                }
            }

            // 範囲内のテキストを読み順（上から下、左から右）で自然に連結
            string mergedText = "";
            if (cellsWithData.Count > 0)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < cellsWithData.Count; i++)
                {
                    if (i > 0)
                    {
                        char prevLast = cellsWithData[i - 1].text.Last();
                        char currFirst = cellsWithData[i].text.First();
                        if (IsAsciiAlnum(prevLast) && IsAsciiAlnum(currFirst))
                            sb.Append(' ');
                    }
                    sb.Append(cellsWithData[i].text);
                }
                mergedText = sb.ToString();
            }

            span.MergedText = mergedText;

            // 左上セルに結合テキストをセットし、それ以外をクリア
            dgv.Rows[minRow].Cells[minCol].Value = mergedText;

            for (int r = minRow; r <= maxRow; r++)
            {
                for (int c = minCol; c <= maxCol; c++)
                {
                    if (r == minRow && c == minCol) continue;
                    dgv.Rows[r].Cells[c].Value = "";
                }
            }

            // 重複する既存スパンを削除して新しいスパンを追加
            mergeSpans.RemoveAll(s => s.OverlapsWith(minCol, minRow, maxCol, maxRow));
            mergeSpans.Add(span);

            dgv.Invalidate();
            return true;
        }

        /// <summary>
        /// 文字列のあるセルに続く空白セル群のみを自動で横方向に一括結合します（データのあるセルは絶対に削除・上書きしません）。
        /// </summary>
        public static int AutoMergeBlankCells(DataGridView dgv, List<TableMergeSpan> mergeSpans, int dataStartCol = 3)
        {
            int mergeCount = 0;
            if (dgv.RowCount == 0 || dgv.ColumnCount <= dataStartCol) return 0;

            for (int r = 0; r < dgv.RowCount; r++)
            {
                int c = dataStartCol;
                while (c < dgv.ColumnCount)
                {
                    // 既に結合されている場合はスキップ
                    var existingSpan = mergeSpans.FirstOrDefault(s => s.Contains(c, r));
                    if (existingSpan != null)
                    {
                        c = existingSpan.StartCol + existingSpan.ColSpan;
                        continue;
                    }

                    var val = dgv.Rows[r].Cells[c].Value?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(val))
                    {
                        // 右側に連続する空白セルのみを探索
                        int nextC = c + 1;
                        while (nextC < dgv.ColumnCount)
                        {
                            var nextSpan = mergeSpans.FirstOrDefault(s => s.Contains(nextC, r));
                            if (nextSpan != null) break;

                            var nextVal = dgv.Rows[r].Cells[nextC].Value?.ToString()?.Trim();
                            if (!string.IsNullOrEmpty(nextVal)) break; // データがあるセルに到達したら停止

                            nextC++;
                        }

                        int spanLen = nextC - c;
                        if (spanLen > 1)
                        {
                            var newSpan = new TableMergeSpan(c, r, spanLen, 1);
                            newSpan.MergedText = val;
                            for (int sc = c; sc < nextC; sc++)
                            {
                                newSpan.OriginalCellTexts[(sc, r)] = dgv.Rows[r].Cells[sc].Value?.ToString() ?? "";
                            }

                            mergeSpans.RemoveAll(s => s.OverlapsWith(c, r, c + spanLen - 1, r));
                            mergeSpans.Add(newSpan);
                            mergeCount++;
                            c = nextC;
                            continue;
                        }
                    }
                    c++;
                }
            }

            dgv.Invalidate();
            return mergeCount;
        }

        /// <summary>
        /// 選択中のセルの結合を解除し、元の各セルのデータを完全に復元します。
        /// </summary>
        public static bool UnmergeSelectedCells(DataGridView dgv, List<TableMergeSpan> mergeSpans)
        {
            if (dgv.SelectedCells.Count == 0) return false;

            var toRemove = new List<TableMergeSpan>();
            foreach (DataGridViewCell cell in dgv.SelectedCells)
            {
                var span = mergeSpans.FirstOrDefault(s => s.Contains(cell.ColumnIndex, cell.RowIndex));
                if (span != null && !toRemove.Contains(span))
                {
                    toRemove.Add(span);
                }
            }

            if (toRemove.Count == 0) return false;

            foreach (var span in toRemove)
            {
                // 元のデータを各セルに復元
                foreach (var kvp in span.OriginalCellTexts)
                {
                    int c = kvp.Key.col;
                    int r = kvp.Key.row;
                    if (r >= 0 && r < dgv.RowCount && c >= 0 && c < dgv.ColumnCount)
                    {
                        dgv.Rows[r].Cells[c].Value = kvp.Value;
                    }
                }
                mergeSpans.Remove(span);
            }

            dgv.Invalidate();
            return true;
        }

        /// <summary>
        /// DataGridViewのセル描画時に結合セルを1つのセルとして綺麗に描画します。
        /// </summary>
        public static void PaintMergedCell(DataGridViewCellPaintingEventArgs e, List<TableMergeSpan> mergeSpans, DataGridView dgv)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics == null) return;

            var span = mergeSpans.FirstOrDefault(s => s.Contains(e.ColumnIndex, e.RowIndex));
            if (span == null) return;

            e.Handled = true;

            // 結合範囲全体の矩形領域を計算
            Rectangle totalRect = Rectangle.Empty;
            for (int r = span.StartRow; r < span.StartRow + span.RowSpan && r < dgv.RowCount; r++)
            {
                for (int c = span.StartCol; c < span.StartCol + span.ColSpan && c < dgv.ColumnCount; c++)
                {
                    Rectangle cellBounds = dgv.GetCellDisplayRectangle(c, r, false);
                    if (totalRect.IsEmpty)
                        totalRect = cellBounds;
                    else
                        totalRect = Rectangle.Union(totalRect, cellBounds);
                }
            }

            if (totalRect.IsEmpty) return;

            // 背景描画
            bool isSelected = false;
            for (int r = span.StartRow; r < span.StartRow + span.RowSpan && r < dgv.RowCount; r++)
            {
                for (int c = span.StartCol; c < span.StartCol + span.ColSpan && c < dgv.ColumnCount; c++)
                {
                    if (dgv.Rows[r].Cells[c].Selected) { isSelected = true; break; }
                }
                if (isSelected) break;
            }

            Color backColor = isSelected
                ? dgv.DefaultCellStyle.SelectionBackColor
                : (span.ColSpan > 1 || span.RowSpan > 1 ? Color.FromArgb(245, 248, 255) : dgv.DefaultCellStyle.BackColor);

            // 1. セル背景を描画
            using (Brush backBrush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(backBrush, e.CellBounds);
            }

            // 2. 外枠ボーダー描画（結合領域の外側のみ）
            using (Pen borderPen = new Pen(dgv.GridColor, 1))
            {
                // 上端
                if (e.RowIndex == span.StartRow)
                    e.Graphics.DrawLine(borderPen, e.CellBounds.Left, e.CellBounds.Top, e.CellBounds.Right, e.CellBounds.Top);
                // 下端
                if (e.RowIndex == span.StartRow + span.RowSpan - 1)
                    e.Graphics.DrawLine(borderPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
                // 左端
                if (e.ColumnIndex == span.StartCol)
                    e.Graphics.DrawLine(borderPen, e.CellBounds.Left, e.CellBounds.Top, e.CellBounds.Left, e.CellBounds.Bottom);
                // 右端
                if (e.ColumnIndex == span.StartCol + span.ColSpan - 1)
                    e.Graphics.DrawLine(borderPen, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom);
            }

            // 3. テキストを描画（各セル描画時にtotalRectを渡すことで、どのセルが空白であってもクリッピング領域に綺麗に描画されます）
            string text = !string.IsNullOrEmpty(span.MergedText)
                ? span.MergedText
                : (dgv.Rows[span.StartRow].Cells[span.StartCol].Value?.ToString() ?? "");

            if (!string.IsNullOrEmpty(text))
            {
                Color foreColor = isSelected ? dgv.DefaultCellStyle.SelectionForeColor : dgv.DefaultCellStyle.ForeColor;
                TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak;
                Rectangle textRect = new Rectangle(
                    totalRect.Left + 4, totalRect.Top + 2,
                    totalRect.Width - 8, totalRect.Height - 4);

                TextRenderer.DrawText(e.Graphics, text, dgv.Font, textRect, foreColor, flags);
            }
        }

        /// <summary>
        /// DataGridViewと結合スパン情報から、表名（表1, 表2...）およびページごとに分離された
        /// 構造化表（StructuredTable）のリストを抽出します。
        /// 異なる表のデータが混入しないよう、表名（TableName）に基づいて厳密にグループ化します。
        /// </summary>
        public static List<StructuredTable> ExtractTablesFromDataGridView(
            DataGridView dgv,
            List<TableMergeSpan> mergeSpans)
        {
            var tables = new List<StructuredTable>();
            if (dgv == null || dgv.RowCount == 0 || dgv.ColumnCount == 0) return tables;

            int startCol = 0;
            bool hasMeta = dgv.Columns.Count > 3 && dgv.Columns[0].Name == "Page" && dgv.Columns[1].Name == "Table" && dgv.Columns[2].Name == "Row";
            if (hasMeta) startCol = 3;

            int totalDataCols = dgv.ColumnCount - startCol;
            if (totalDataCols <= 0) return tables;

            if (!hasMeta)
            {
                var tbl = new StructuredTable { PageNumber = 1, TableName = "表1", ColumnCount = totalDataCols, RowCount = dgv.RowCount };
                for (int r = 0; r < dgv.RowCount; r++)
                {
                    var row = new StructuredTableRow { PageNumber = 1, TableName = "表1", RowIndex = r + 1 };
                    for (int c = 0; c < dgv.ColumnCount; c++)
                        row.Cells.Add(dgv.Rows[r].Cells[c].Value?.ToString() ?? "");
                    tbl.Rows.Add(row);
                }
                foreach (var span in mergeSpans)
                {
                    tbl.MergeSpans.Add(new TableMergeSpan(span.StartCol, span.StartRow, span.ColSpan, span.RowSpan) { MergedText = span.MergedText });
                }
                tables.Add(tbl);
                return tables;
            }

            // 表名（TableName）とページ番号に基づいて行を厳密にグループ化
            var tableGroups = new Dictionary<string, (int pageNum, string tableName, int startDgvRow, List<DataGridViewRow> rows)>();
            for (int r = 0; r < dgv.RowCount; r++)
            {
                var dgvRow = dgv.Rows[r];
                if (dgvRow.IsNewRow) continue;
                if (dgvRow.Cells["Table"].Value == null && dgvRow.Cells["Row"].Value == null) continue;

                int pageNum = int.TryParse(dgvRow.Cells["Page"].Value?.ToString(), out int p) ? p : 1;
                string tableName = dgvRow.Cells["Table"].Value?.ToString()?.Trim() ?? "表1";
                if (string.IsNullOrWhiteSpace(tableName)) tableName = "表1";

                string key = $"{pageNum}_{tableName}";
                if (!tableGroups.TryGetValue(key, out var group))
                {
                    group = (pageNum, tableName, r, new List<DataGridViewRow>());
                    tableGroups[key] = group;
                }
                group.rows.Add(dgvRow);
            }

            foreach (var kvp in tableGroups)
            {
                var (pageNum, tableName, startDgvRow, rows) = kvp.Value;

                // この表の有効列数を計算
                int maxColsInGroup = 1;
                for (int r = 0; r < rows.Count; r++)
                {
                    for (int c = dgv.ColumnCount - 1; c >= startCol; c--)
                    {
                        if (!string.IsNullOrWhiteSpace(rows[r].Cells[c].Value?.ToString()))
                        {
                            int colIdx = c - startCol + 1;
                            if (colIdx > maxColsInGroup) maxColsInGroup = colIdx;
                            break;
                        }
                    }
                }

                var sTable = new StructuredTable
                {
                    PageNumber = pageNum,
                    TableName = tableName,
                    ColumnCount = maxColsInGroup,
                    RowCount = rows.Count
                };

                for (int r = 0; r < rows.Count; r++)
                {
                    var sRow = new StructuredTableRow
                    {
                        PageNumber = pageNum,
                        TableName = tableName,
                        RowIndex = r + 1
                    };
                    for (int c = 0; c < maxColsInGroup; c++)
                    {
                        int dgvColIdx = startCol + c;
                        string val = dgvColIdx < dgv.ColumnCount ? (rows[r].Cells[dgvColIdx].Value?.ToString() ?? "") : "";
                        sRow.Cells.Add(val);
                    }
                    sTable.Rows.Add(sRow);
                }

                // この表の相対結合スパンを抽出
                int endDgvRow = startDgvRow + rows.Count - 1;
                foreach (var span in mergeSpans)
                {
                    if (span.StartRow >= startDgvRow && span.StartRow <= endDgvRow && span.StartCol >= startCol)
                    {
                        int relRow = span.StartRow - startDgvRow;
                        int relCol = span.StartCol - startCol;
                        if (relCol < maxColsInGroup)
                        {
                            int colSpan = Math.Min(span.ColSpan, maxColsInGroup - relCol);
                            int rowSpan = Math.Min(span.RowSpan, rows.Count - relRow);
                            sTable.MergeSpans.Add(new TableMergeSpan(relCol, relRow, colSpan, rowSpan)
                            {
                                MergedText = span.MergedText
                            });
                        }
                    }
                }

                tables.Add(sTable);
            }

            return tables;
        }

        /// <summary>
        /// 結合情報を完全に反映したWord/Excel対応のHTMLテーブル形式およびTSV形式でクリップボードにコピーします。
        /// 複数表がある場合も表ごとに分離して出力します。
        /// </summary>
        public static void CopyTableToClipboard(DataGridView dgv, List<TableMergeSpan> mergeSpans)
        {
            if (dgv.RowCount == 0 || dgv.ColumnCount == 0) return;

            var tables = ExtractTablesFromDataGridView(dgv, mergeSpans);
            if (tables.Count == 0) return;

            var sbHtml = new StringBuilder();
            var sbTsv = new StringBuilder();

            foreach (var tbl in tables)
            {
                if (tables.Count > 1 || !string.IsNullOrEmpty(tbl.TableName))
                {
                    sbHtml.AppendLine($"<p style=\"font-weight: bold; margin-top: 14px; margin-bottom: 6px; color: #1e293b;\">◆ {System.Web.HttpUtility.HtmlEncode(tbl.TableName)}</p>");
                    sbTsv.AppendLine($"◆ {tbl.TableName}");
                }

                sbHtml.AppendLine("<table border=\"1\" style=\"border-collapse: collapse; font-family: 'Yu Gothic', sans-serif; font-size: 10.5pt; margin-bottom: 16px;\">");

                // データ行（結合スパン反映）
                for (int r = 0; r < tbl.Rows.Count; r++)
                {
                    sbHtml.AppendLine("  <tr>");
                    var row = tbl.Rows[r];
                    for (int c = 0; c < tbl.ColumnCount; c++)
                    {
                        var span = tbl.MergeSpans.FirstOrDefault(s => s.Contains(c, r));

                        if (span != null)
                        {
                            if (!span.IsTopLeft(c, r))
                            {
                                if (c > 0) sbTsv.Append("\t");
                                continue;
                            }

                            string text = !string.IsNullOrEmpty(span.MergedText)
                                ? span.MergedText
                                : (c < row.Cells.Count ? row.Cells[c] : "");

                            string encoded = System.Web.HttpUtility.HtmlEncode(text).Replace("\n", "<br>");
                            string spanAttr = "";
                            if (span.ColSpan > 1) spanAttr += $" colspan=\"{span.ColSpan}\"";
                            if (span.RowSpan > 1) spanAttr += $" rowspan=\"{span.RowSpan}\"";

                            sbHtml.AppendLine($"    <td{spanAttr} style=\"padding: 6px 8px; border: 1px solid #999;\">{encoded}</td>");

                            if (c > 0) sbTsv.Append("\t");
                            sbTsv.Append(text.Replace("\r", "").Replace("\n", " "));
                        }
                        else
                        {
                            string text = c < row.Cells.Count ? row.Cells[c] : "";
                            string encoded = System.Web.HttpUtility.HtmlEncode(text).Replace("\n", "<br>");
                            sbHtml.AppendLine($"    <td style=\"padding: 6px 8px; border: 1px solid #999;\">{encoded}</td>");

                            if (c > 0) sbTsv.Append("\t");
                            sbTsv.Append(text.Replace("\r", "").Replace("\n", " "));
                        }
                    }
                    sbHtml.AppendLine("  </tr>");
                    sbTsv.AppendLine();
                }

                sbHtml.AppendLine("</table>");
                sbTsv.AppendLine();
            }

            string cfHtml = WrapHtmlForClipboard(sbHtml.ToString());

            var dataObj = new DataObject();
            dataObj.SetData(DataFormats.Html, cfHtml);
            dataObj.SetData(DataFormats.UnicodeText, sbTsv.ToString());
            dataObj.SetData(DataFormats.Text, sbTsv.ToString());

            Clipboard.SetDataObject(dataObj, true);
        }

        private static string WrapHtmlForClipboard(string htmlFragment)
        {
            string headerTemplate =
                "Version:0.9\r\n" +
                "StartHTML:00000000\r\n" +
                "EndHTML:00000000\r\n" +
                "StartFragment:00000000\r\n" +
                "EndFragment:00000000\r\n";

            string docPrefix = "<!DOCTYPE html><html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"></head><body><!--StartFragment-->";
            string docSuffix = "<!--EndFragment--></body></html>";

            byte[] headerBytes = Encoding.UTF8.GetBytes(headerTemplate);
            byte[] prefixBytes = Encoding.UTF8.GetBytes(docPrefix);
            byte[] fragBytes = Encoding.UTF8.GetBytes(htmlFragment);
            byte[] suffixBytes = Encoding.UTF8.GetBytes(docSuffix);

            int startHtml = headerBytes.Length;
            int startFragment = startHtml + prefixBytes.Length;
            int endFragment = startFragment + fragBytes.Length;
            int endHtml = endFragment + suffixBytes.Length;

            string finalHeader =
                $"Version:0.9\r\n" +
                $"StartHTML:{startHtml:D8}\r\n" +
                $"EndHTML:{endHtml:D8}\r\n" +
                $"StartFragment:{startFragment:D8}\r\n" +
                $"EndFragment:{endFragment:D8}\r\n";

            return finalHeader + docPrefix + htmlFragment + docSuffix;
        }

        /// <summary>
        /// 本文・見出し・注釈文・切り出し図画像および結合された表をWord対応文書（.html / .doc）として出力します。
        /// </summary>
        public static void ExportToWordFile(
            string filePath,
            string bodyText,
            string headingText,
            string footnoteText,
            DataGridView? dgv,
            List<TableMergeSpan> mergeSpans,
            AppSettings settings,
            List<FigureItem>? figures = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\">");
            sb.AppendLine("<meta charset=\"utf-8\">");
            sb.AppendLine("<style>");
            sb.AppendLine($"body {{ font-family: '{settings.FontFamilyName}', 'Yu Gothic', sans-serif; font-size: {settings.FontSize:0.#}pt; line-height: 1.6; margin: 30px; }}");
            sb.AppendLine("h1, h2, h3 { color: #1a365d; margin-top: 20px; }");
            sb.AppendLine("p { margin-bottom: 1em; text-indent: 1em; }");
            sb.AppendLine("table { border-collapse: collapse; width: 100%; margin: 20px 0; font-size: 10pt; }");
            sb.AppendLine("th, td { border: 1px solid #555; padding: 6px 10px; text-align: left; vertical-align: middle; }");
            sb.AppendLine("th { background-color: #e2e8f0; font-weight: bold; }");
            sb.AppendLine(".figure-box { margin: 20px 0; text-align: center; }");
            sb.AppendLine(".figure-caption { font-size: 9.5pt; font-weight: bold; color: #334155; margin-top: 6px; }");
            sb.AppendLine(".footnote { font-size: 9pt; color: #4a5568; border-top: 1px solid #cbd5e0; padding-top: 10px; margin-top: 30px; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            if (!string.IsNullOrWhiteSpace(headingText))
            {
                sb.AppendLine("<h2>見出し</h2>");
                foreach (var line in headingText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    sb.AppendLine($"<h3>{System.Web.HttpUtility.HtmlEncode(line)}</h3>");
                }
            }

            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                sb.AppendLine("<h2>本文</h2>");
                foreach (var para in bodyText.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    sb.AppendLine($"<p>{System.Web.HttpUtility.HtmlEncode(para).Replace("\n", "<br>")}</p>");
                }
            }

            if (dgv != null && dgv.RowCount > 0)
            {
                var tables = ExtractTablesFromDataGridView(dgv, mergeSpans);
                foreach (var tbl in tables)
                {
                    sb.AppendLine($"<p style=\"font-weight: bold; margin-top: 16px; margin-bottom: 6px; color: #1e293b;\">◆ {System.Web.HttpUtility.HtmlEncode(tbl.TableName)}</p>");
                    sb.AppendLine("<table>");

                    for (int r = 0; r < tbl.Rows.Count; r++)
                    {
                        sb.AppendLine("  <tr>");
                        var row = tbl.Rows[r];
                        for (int c = 0; c < tbl.ColumnCount; c++)
                        {
                            var span = tbl.MergeSpans.FirstOrDefault(s => s.Contains(c, r));
                            if (span != null)
                            {
                                if (!span.IsTopLeft(c, r)) continue;

                                string text = !string.IsNullOrEmpty(span.MergedText)
                                    ? span.MergedText
                                    : (c < row.Cells.Count ? row.Cells[c] : "");

                                string spanAttr = "";
                                if (span.ColSpan > 1) spanAttr += $" colspan=\"{span.ColSpan}\"";
                                if (span.RowSpan > 1) spanAttr += $" rowspan=\"{span.RowSpan}\"";

                                sb.AppendLine($"    <td{spanAttr}>{System.Web.HttpUtility.HtmlEncode(text).Replace("\n", "<br>")}</td>");
                            }
                            else
                            {
                                string text = c < row.Cells.Count ? row.Cells[c] : "";
                                sb.AppendLine($"    <td>{System.Web.HttpUtility.HtmlEncode(text).Replace("\n", "<br>")}</td>");
                            }
                        }
                        sb.AppendLine("  </tr>");
                    }
                    sb.AppendLine("</table>");
                }
            }

            if (figures != null && figures.Count > 0)
            {
                sb.AppendLine("<h2>図</h2>");
                foreach (var fig in figures)
                {
                    if (fig.ImageBytes != null && fig.ImageBytes.Length > 0)
                    {
                        string base64 = Convert.ToBase64String(fig.ImageBytes);
                        sb.AppendLine("<div class=\"figure-box\">");
                        sb.AppendLine($"  <img src=\"data:{fig.MimeType};base64,{base64}\" style=\"max-width: 100%; height: auto; border: 1px solid #94a3b8; border-radius: 4px; box-shadow: 0 1px 3px rgba(0,0,0,0.1);\" />");
                        sb.AppendLine($"  <div class=\"figure-caption\">[ページ {fig.PageNumber}] {System.Web.HttpUtility.HtmlEncode(fig.Name)} ({fig.Bounds.Width}×{fig.Bounds.Height} px)</div>");
                        sb.AppendLine("</div>");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(footnoteText))
            {
                sb.AppendLine("<div class=\"footnote\">");
                sb.AppendLine("<h4>注釈</h4>");
                foreach (var line in footnoteText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    sb.AppendLine($"<p>{System.Web.HttpUtility.HtmlEncode(line)}</p>");
                }
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
        }

        private static bool IsAsciiAlnum(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
        }
    }
}
