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

            // 3. テキストを描画（最新のユーザー入力値であるセル値を最優先とし、未設定の場合はspan.MergedTextを使用）
            string cellVal = (span.StartRow < dgv.RowCount && span.StartCol < dgv.ColumnCount)
                ? (dgv.Rows[span.StartRow].Cells[span.StartCol].Value?.ToString() ?? "")
                : "";
            string text = !string.IsNullOrWhiteSpace(cellVal)
                ? cellVal
                : (!string.IsNullOrEmpty(span.MergedText) ? span.MergedText : "");

            if (!string.IsNullOrEmpty(text) && span.MergedText != text)
            {
                span.MergedText = text;
            }

            if (!string.IsNullOrEmpty(text))
            {
                Color foreColor = isSelected ? dgv.DefaultCellStyle.SelectionForeColor : dgv.DefaultCellStyle.ForeColor;
                TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak;
                Rectangle textRect = new Rectangle(
                    totalRect.Left + 4, totalRect.Top + 2,
                    totalRect.Width - 8, totalRect.Height - 4);

                Font cellFont = e.CellStyle.Font ?? dgv.DefaultCellStyle.Font ?? dgv.Font;
                TextRenderer.DrawText(e.Graphics, text, cellFont, textRect, foreColor, flags);
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
                    string spanVal = (span.StartRow < dgv.RowCount && span.StartCol < dgv.ColumnCount)
                        ? (dgv.Rows[span.StartRow].Cells[span.StartCol].Value?.ToString() ?? "")
                        : "";
                    string spanText = !string.IsNullOrWhiteSpace(spanVal) ? spanVal : span.MergedText;
                    tbl.MergeSpans.Add(new TableMergeSpan(span.StartCol, span.StartRow, span.ColSpan, span.RowSpan) { MergedText = spanText });
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
                            string spanVal = (span.StartRow < dgv.RowCount && span.StartCol < dgv.ColumnCount)
                                ? (dgv.Rows[span.StartRow].Cells[span.StartCol].Value?.ToString() ?? "")
                                : "";
                            string spanText = !string.IsNullOrWhiteSpace(spanVal) ? spanVal : span.MergedText;
                            sTable.MergeSpans.Add(new TableMergeSpan(relCol, relRow, colSpan, rowSpan)
                            {
                                MergedText = spanText
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

                            string cellVal = (c < row.Cells.Count) ? row.Cells[c] : "";
                            string text = !string.IsNullOrWhiteSpace(cellVal)
                                ? cellVal
                                : (!string.IsNullOrEmpty(span.MergedText) ? span.MergedText : "");

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
                    string trimmedPara = para.Trim();
                    if (OcrSorter.IsSubheadingText(trimmedPara))
                    {
                        sb.AppendLine($"<h3 style=\"margin-top: 16px; margin-bottom: 6px; font-weight: bold; color: #1e293b;\">{System.Web.HttpUtility.HtmlEncode(trimmedPara)}</h3>");
                    }
                    else
                    {
                        sb.AppendLine($"<p>{System.Web.HttpUtility.HtmlEncode(para).Replace("\n", "<br>")}</p>");
                    }
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

                                string cellVal = (c < row.Cells.Count) ? row.Cells[c] : "";
                                string text = !string.IsNullOrWhiteSpace(cellVal)
                                    ? cellVal
                                    : (!string.IsNullOrEmpty(span.MergedText) ? span.MergedText : "");

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

        /// <summary>
        /// DataGridView内のすべての表ブロック（連続した行グループ）を走査して一覧化します。
        /// </summary>
        public static List<TableGridItemInfo> ScanTablesFromDataGridView(DataGridView dgv)
        {
            var result = new List<TableGridItemInfo>();
            if (dgv == null || dgv.RowCount == 0) return result;

            bool hasMeta = dgv.Columns.Count > 3 &&
                           dgv.Columns[0].Name == "Page" &&
                           dgv.Columns[1].Name == "Table" &&
                           dgv.Columns[2].Name == "Row";
            int startCol = hasMeta ? 3 : 0;

            TableGridItemInfo? currentTable = null;

            for (int r = 0; r < dgv.RowCount; r++)
            {
                var row = dgv.Rows[r];
                if (row.IsNewRow) continue;

                int pageNum = 1;
                string tableName = "表1";

                if (hasMeta)
                {
                    if (row.Cells["Page"]?.Value != null && int.TryParse(row.Cells["Page"].Value?.ToString(), out int p))
                        pageNum = p;

                    if (row.Cells["Table"]?.Value != null)
                        tableName = row.Cells["Table"].Value?.ToString()?.Trim() ?? "表1";
                }

                if (string.IsNullOrWhiteSpace(tableName)) tableName = "表1";

                // この行の有効列数を計算
                int validCols = 0;
                for (int c = dgv.ColumnCount - 1; c >= startCol; c--)
                {
                    if (!string.IsNullOrWhiteSpace(row.Cells[c].Value?.ToString()))
                    {
                        validCols = c - startCol + 1;
                        break;
                    }
                }

                if (currentTable == null || currentTable.PageNumber != pageNum || !string.Equals(currentTable.TableName, tableName, StringComparison.OrdinalIgnoreCase))
                {
                    currentTable = new TableGridItemInfo
                    {
                        StartRowIndex = r,
                        PageNumber = pageNum,
                        TableName = tableName,
                        RowCount = 0,
                        ColumnCount = validCols
                    };
                    result.Add(currentTable);
                }

                currentTable.Rows.Add(row);
                currentTable.RowCount++;
                if (validCols > currentTable.ColumnCount)
                {
                    currentTable.ColumnCount = validCols;
                }
            }

            return result;
        }

        /// <summary>
        /// StructuredTable のリストに基づいて DataGridView の列・行および結合スパンを一括展開します。
        /// </summary>
        public static void PopulateDataGridViewWithTables(
            DataGridView dgv,
            List<StructuredTable> tables,
            List<TableMergeSpan> mergeSpans)
        {
            if (dgv == null) return;

            dgv.Rows.Clear();
            mergeSpans.Clear();

            if (tables == null || tables.Count == 0) return;

            int maxCols = tables.Max(t => t.ColumnCount);
            if (maxCols < 1) maxCols = 1;

            int currentDataCols = dgv.Columns.Count > 3 ? dgv.Columns.Count - 3 : 0;

            if (dgv.Columns.Count == 0)
            {
                dgv.Columns.Clear();
                dgv.Columns.Add("Page", "ページ");
                dgv.Columns.Add("Table", "表名");
                dgv.Columns.Add("Row", "行");
                dgv.Columns["Page"]!.FillWeight = 8;
                dgv.Columns["Table"]!.FillWeight = 12;
                dgv.Columns["Row"]!.FillWeight = 8;
                dgv.Columns["Page"]!.ReadOnly = true;
                dgv.Columns["Table"]!.ReadOnly = true;
                dgv.Columns["Row"]!.ReadOnly = true;

                for (int c = 1; c <= maxCols; c++)
                {
                    int colIdx = dgv.Columns.Add($"Col{c}", $"列{c}");
                    dgv.Columns[colIdx]!.FillWeight = Math.Max(15, 72 / maxCols);
                }
            }
            else if (maxCols > currentDataCols)
            {
                for (int c = currentDataCols + 1; c <= maxCols; c++)
                {
                    int colIdx = dgv.Columns.Add($"Col{c}", $"列{c}");
                    dgv.Columns[colIdx]!.FillWeight = Math.Max(15, 72 / maxCols);
                }
            }

            int colOffset = dgv.Columns.Count > 3 && dgv.Columns[0].Name == "Page" ? 3 : 0;

            foreach (var sTable in tables)
            {
                int baseRow = dgv.Rows.Count;
                foreach (var sRow in sTable.Rows)
                {
                    var rowCells = new object[dgv.Columns.Count];
                    if (colOffset >= 3)
                    {
                        rowCells[0] = sTable.PageNumber;
                        rowCells[1] = sTable.TableName;
                        rowCells[2] = sRow.RowIndex;
                    }

                    for (int c = 0; c < sRow.Cells.Count && (c + colOffset) < rowCells.Length; c++)
                    {
                        rowCells[c + colOffset] = sRow.Cells[c];
                    }

                    int addedIdx = dgv.Rows.Add(rowCells);
                    dgv.Rows[addedIdx].Tag = new TableRowMeta
                    {
                        OriginalPage = sRow.PageNumber > 0 ? sRow.PageNumber : sTable.PageNumber,
                        OriginalTable = !string.IsNullOrEmpty(sRow.TableName) ? sRow.TableName : sTable.TableName,
                        OriginalRowIndex = sRow.RowIndex
                    };
                }

                foreach (var span in sTable.MergeSpans)
                {
                    mergeSpans.Add(new TableMergeSpan(colOffset + span.StartCol, baseRow + span.StartRow, span.ColSpan, span.RowSpan)
                    {
                        MergedText = span.MergedText
                    });
                }
            }

            dgv.Invalidate();
        }

        /// <summary>
        /// 複数の構造化表を縦方向（行追加）または横方向（列追加）に連結・統合します。
        /// </summary>
        public static bool ConcatenateTables(
            DataGridView dgv,
            List<TableMergeSpan> mergeSpans,
            List<TableGridItemInfo> sourceTables,
            bool isVertical,
            bool skipDuplicateHeader,
            string newTableName)
        {
            if (dgv == null || sourceTables == null || sourceTables.Count < 2) return false;

            // 既存のすべてのテーブルを抽出
            var allTables = ExtractTablesFromDataGridView(dgv, mergeSpans);
            if (allTables.Count == 0) return false;

            var primarySource = sourceTables[0];
            int primaryPage = primarySource.PageNumber;
            string finalName = string.IsNullOrWhiteSpace(newTableName) ? primarySource.TableName : newTableName.Trim();

            // sourceTables に該当する StructuredTable を allTables から取得
            var tablesToMerge = new List<StructuredTable>();
            foreach (var st in sourceTables)
            {
                var match = allTables.FirstOrDefault(at => at.PageNumber == st.PageNumber && string.Equals(at.TableName, st.TableName, StringComparison.OrdinalIgnoreCase));
                if (match != null && !tablesToMerge.Contains(match))
                {
                    tablesToMerge.Add(match);
                }
            }

            if (tablesToMerge.Count < 2) return false;

            StructuredTable combinedTable;

            if (isVertical)
            {
                // 縦方向（行追加）
                int maxCols = tablesToMerge.Max(t => t.ColumnCount);
                combinedTable = new StructuredTable
                {
                    PageNumber = primaryPage,
                    TableName = finalName,
                    ColumnCount = maxCols
                };

                // 1つ目のテーブルの行とスパンを追加
                var firstTbl = tablesToMerge[0];
                foreach (var row in firstTbl.Rows)
                {
                    var newRow = new StructuredTableRow
                    {
                        PageNumber = row.PageNumber > 0 ? row.PageNumber : firstTbl.PageNumber,
                        TableName = finalName,
                        RowIndex = combinedTable.Rows.Count + 1,
                        Cells = new List<string>(row.Cells)
                    };
                    while (newRow.Cells.Count < maxCols) newRow.Cells.Add("");
                    combinedTable.Rows.Add(newRow);
                }

                foreach (var span in firstTbl.MergeSpans)
                {
                    combinedTable.MergeSpans.Add(new TableMergeSpan(span.StartCol, span.StartRow, span.ColSpan, span.RowSpan)
                    {
                        MergedText = span.MergedText
                    });
                }

                // 2つ目以降のテーブルの行とスパンを追加
                for (int i = 1; i < tablesToMerge.Count; i++)
                {
                    var nextTbl = tablesToMerge[i];
                    int baseRowOffset = combinedTable.Rows.Count;
                    bool isDupHeader = skipDuplicateHeader && nextTbl.Rows.Count > 1 &&
                                       firstTbl.Rows.Count > 0 &&
                                       IsDuplicateHeaderRow(firstTbl.Rows[0], nextTbl.Rows[0]);
                    int startR = isDupHeader ? 1 : 0;

                    for (int r = startR; r < nextTbl.Rows.Count; r++)
                    {
                        var row = nextTbl.Rows[r];
                        var newRow = new StructuredTableRow
                        {
                            PageNumber = row.PageNumber > 0 ? row.PageNumber : nextTbl.PageNumber,
                            TableName = finalName,
                            RowIndex = combinedTable.Rows.Count + 1,
                            Cells = new List<string>(row.Cells)
                        };
                        while (newRow.Cells.Count < maxCols) newRow.Cells.Add("");
                        combinedTable.Rows.Add(newRow);
                    }

                    foreach (var span in nextTbl.MergeSpans)
                    {
                        if (isDupHeader && span.StartRow == 0) continue;

                        int adjustedRow = baseRowOffset + (span.StartRow - startR);
                        if (adjustedRow >= 0)
                        {
                            combinedTable.MergeSpans.Add(new TableMergeSpan(span.StartCol, adjustedRow, span.ColSpan, span.RowSpan)
                            {
                                MergedText = span.MergedText
                            });
                        }
                    }
                }

                combinedTable.RowCount = combinedTable.Rows.Count;
            }
            else
            {
                // 横方向（列追加）
                int totalCols = tablesToMerge.Sum(t => t.ColumnCount);
                int maxRows = tablesToMerge.Max(t => t.RowCount);

                combinedTable = new StructuredTable
                {
                    PageNumber = primaryPage,
                    TableName = finalName,
                    ColumnCount = totalCols,
                    RowCount = maxRows
                };

                for (int r = 0; r < maxRows; r++)
                {
                    combinedTable.Rows.Add(new StructuredTableRow
                    {
                        PageNumber = primaryPage,
                        TableName = finalName,
                        RowIndex = r + 1,
                        Cells = new List<string>(new string[totalCols])
                    });
                }

                int currentColOffset = 0;
                for (int i = 0; i < tablesToMerge.Count; i++)
                {
                    var tbl = tablesToMerge[i];
                    for (int r = 0; r < tbl.Rows.Count && r < maxRows; r++)
                    {
                        for (int c = 0; c < tbl.Rows[r].Cells.Count && (currentColOffset + c) < totalCols; c++)
                        {
                            combinedTable.Rows[r].Cells[currentColOffset + c] = tbl.Rows[r].Cells[c];
                        }
                    }

                    foreach (var span in tbl.MergeSpans)
                    {
                        combinedTable.MergeSpans.Add(new TableMergeSpan(currentColOffset + span.StartCol, span.StartRow, span.ColSpan, span.RowSpan)
                        {
                            MergedText = span.MergedText
                        });
                    }

                    currentColOffset += tbl.ColumnCount;
                }
            }

            // allTables を更新: 最初の結合対象テーブルの位置を combinedTable に置き換え、他の結合対象テーブルを削除
            int primaryIdx = allTables.IndexOf(tablesToMerge[0]);
            if (primaryIdx >= 0)
            {
                allTables[primaryIdx] = combinedTable;
                for (int i = 1; i < tablesToMerge.Count; i++)
                {
                    allTables.Remove(tablesToMerge[i]);
                }
            }
            else
            {
                foreach (var t in tablesToMerge) allTables.Remove(t);
                allTables.Add(combinedTable);
            }

            // DataGridView を再構築
            PopulateDataGridViewWithTables(dgv, allTables, mergeSpans);
            return true;
        }

        /// <summary>
        /// 選択セルの文字列を空白で分割し、後半を右隣の列に設定（または既存値に追記）します。
        /// </summary>
        public static bool SplitSelectedCellBySpace(DataGridView dgv, List<TableMergeSpan> mergeSpans, int dataStartCol = 3)
        {
            if (dgv.CurrentCell == null) return false;
            int r = dgv.CurrentCell.RowIndex;
            int c = dgv.CurrentCell.ColumnIndex;

            if (r < 0 || c < dataStartCol || c >= dgv.ColumnCount) return false;

            string fullText = dgv.Rows[r].Cells[c].Value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(fullText)) return false;

            int splitIdx = FindBestCellSplitIndex(fullText);
            if (splitIdx <= 0 || splitIdx >= fullText.Length) return false;

            string leftText = fullText.Substring(0, splitIdx).Trim();
            int rightStart = splitIdx;
            while (rightStart < fullText.Length && (char.IsWhiteSpace(fullText[rightStart]) || fullText[rightStart] == '　'))
                rightStart++;

            string rightText = rightStart < fullText.Length ? fullText.Substring(rightStart).Trim() : "";
            if (string.IsNullOrEmpty(rightText)) return false;

            if (c + 1 >= dgv.ColumnCount)
            {
                int newColIdx = dgv.Columns.Add($"Col{dgv.ColumnCount - dataStartCol + 1}", $"列{dgv.ColumnCount - dataStartCol + 1}");
                dgv.Columns[newColIdx].FillWeight = 20;
            }

            string existingRight = dgv.Rows[r].Cells[c + 1].Value?.ToString()?.Trim() ?? "";
            string combinedRight = string.IsNullOrEmpty(existingRight) ? rightText : (rightText + " " + existingRight);

            mergeSpans.RemoveAll(s => s.Contains(c, r) || s.Contains(c + 1, r));

            dgv.Rows[r].Cells[c].Value = leftText;
            dgv.Rows[r].Cells[c + 1].Value = combinedRight;
            dgv.Invalidate();
            return true;
        }

        /// <summary>
        /// 選択セルの値を右隣の列へ移動（または既存値に追記）します。
        /// </summary>
        public static bool ShiftSelectedCellValueRight(DataGridView dgv, List<TableMergeSpan> mergeSpans, int dataStartCol = 3)
        {
            if (dgv.CurrentCell == null) return false;
            int r = dgv.CurrentCell.RowIndex;
            int c = dgv.CurrentCell.ColumnIndex;

            if (r < 0 || c < dataStartCol || c >= dgv.ColumnCount) return false;

            string curText = dgv.Rows[r].Cells[c].Value?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(curText)) return false;

            if (c + 1 >= dgv.ColumnCount)
            {
                int newColIdx = dgv.Columns.Add($"Col{dgv.ColumnCount - dataStartCol + 1}", $"列{dgv.ColumnCount - dataStartCol + 1}");
                dgv.Columns[newColIdx].FillWeight = 20;
            }

            string existingRight = dgv.Rows[r].Cells[c + 1].Value?.ToString()?.Trim() ?? "";
            string combinedRight = string.IsNullOrEmpty(existingRight) ? curText : (curText + " " + existingRight);

            mergeSpans.RemoveAll(s => s.Contains(c, r) || s.Contains(c + 1, r));

            dgv.Rows[r].Cells[c].Value = "";
            dgv.Rows[r].Cells[c + 1].Value = combinedRight;
            dgv.CurrentCell = dgv.Rows[r].Cells[c + 1];
            dgv.Invalidate();
            return true;
        }

        /// <summary>
        /// 選択セルの値を左隣の列へ移動（または既存値に追記）します。
        /// </summary>
        public static bool ShiftSelectedCellValueLeft(DataGridView dgv, List<TableMergeSpan> mergeSpans, int dataStartCol = 3)
        {
            if (dgv.CurrentCell == null) return false;
            int r = dgv.CurrentCell.RowIndex;
            int c = dgv.CurrentCell.ColumnIndex;

            if (r < 0 || c <= dataStartCol || c >= dgv.ColumnCount) return false;

            string curText = dgv.Rows[r].Cells[c].Value?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(curText)) return false;

            string existingLeft = dgv.Rows[r].Cells[c - 1].Value?.ToString()?.Trim() ?? "";
            string combinedLeft = string.IsNullOrEmpty(existingLeft) ? curText : (existingLeft + " " + curText);

            mergeSpans.RemoveAll(s => s.Contains(c, r) || s.Contains(c - 1, r));

            dgv.Rows[r].Cells[c].Value = "";
            dgv.Rows[r].Cells[c - 1].Value = combinedLeft;
            dgv.CurrentCell = dgv.Rows[r].Cells[c - 1];
            dgv.Invalidate();
            return true;
        }

        private static int FindBestCellSplitIndex(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= 1) return -1;

            var wsIndices = new List<int>();
            for (int i = 1; i < text.Length - 1; i++)
            {
                if (char.IsWhiteSpace(text[i]) || text[i] == '　')
                {
                    if (!char.IsWhiteSpace(text[i - 1]) && text[i - 1] != '　')
                    {
                        wsIndices.Add(i);
                    }
                }
            }

            if (wsIndices.Count == 0) return -1;
            if (wsIndices.Count == 1) return wsIndices[0];

            // 1. 閉じ括弧直後の空白（日付や注釈の直後）
            for (int i = wsIndices.Count - 1; i >= 0; i--)
            {
                int idx = wsIndices[i];
                char prev = text[idx - 1];
                if (prev == ')' || prev == '）' || prev == ']' || prev == '］' || prev == '}' || prev == '｝')
                {
                    return idx;
                }
            }

            // 2. 和文と欧文・英数字の境界にある空白
            foreach (int idx in wsIndices)
            {
                char prev = text[idx - 1];
                int nextIdx = idx;
                while (nextIdx < text.Length && (char.IsWhiteSpace(text[nextIdx]) || text[nextIdx] == '　'))
                    nextIdx++;
                if (nextIdx < text.Length)
                {
                    char next = text[nextIdx];
                    if (IsCjk(prev) != IsCjk(next))
                    {
                        return idx;
                    }
                }
            }

            // 3. 文字幅換算で中央に最も近い空白
            double totalWeight = text.Sum(c => c <= 127 ? 1.0 : 2.0);
            double halfWeight = totalWeight / 2.0;

            int bestIdx = wsIndices[0];
            double minDiff = double.MaxValue;
            double curWeight = 0;

            for (int i = 0; i < text.Length; i++)
            {
                curWeight += text[i] <= 127 ? 1.0 : 2.0;
                if (wsIndices.Contains(i))
                {
                    double diff = Math.Abs(curWeight - halfWeight);
                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        bestIdx = i;
                    }
                }
            }

            return bestIdx;
        }

        private static bool IsCjk(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF) ||
                   (c >= 0x3040 && c <= 0x309F) ||
                   (c >= 0x30A0 && c <= 0x30FF) ||
                   (c >= 0x3400 && c <= 0x4DBF);
        }

        /// <summary>
        /// 2つ目以降のテーブルの先頭行が、最初のテーブルのヘッダー行（重複見出し）であるかを判定します。
        /// </summary>
        public static bool IsDuplicateHeaderRow(StructuredTableRow firstHeaderRow, StructuredTableRow candidateRow)
        {
            if (firstHeaderRow == null || candidateRow == null) return false;
            var c1 = firstHeaderRow.Cells;
            var c2 = candidateRow.Cells;
            if (c1 == null || c2 == null || c1.Count == 0 || c2.Count == 0) return false;

            int matchCount = 0;
            int comparedCount = 0;
            for (int i = 0; i < Math.Min(c1.Count, c2.Count); i++)
            {
                string v1 = (c1[i] ?? "").Trim();
                string v2 = (c2[i] ?? "").Trim();
                if (!string.IsNullOrEmpty(v1) && !string.IsNullOrEmpty(v2))
                {
                    comparedCount++;
                    if (string.Equals(v1, v2, StringComparison.OrdinalIgnoreCase))
                    {
                        matchCount++;
                    }
                }
            }

            return comparedCount > 0 && ((double)matchCount / comparedCount >= 0.5);
        }

        /// <summary>
        /// DataGridViewRow ベースで、2つ目以降のテーブルの先頭行がヘッダー行と一致するか判定します。
        /// </summary>
        public static bool IsDuplicateHeaderRow(DataGridViewRow firstHeaderRow, DataGridViewRow candidateRow, int startCol, int colCount)
        {
            if (firstHeaderRow == null || candidateRow == null) return false;
            int matchCount = 0;
            int comparedCount = 0;
            for (int c = 0; c < colCount; c++)
            {
                int colIdx = startCol + c;
                if (colIdx >= firstHeaderRow.Cells.Count || colIdx >= candidateRow.Cells.Count) break;

                string v1 = (firstHeaderRow.Cells[colIdx].Value?.ToString() ?? "").Trim();
                string v2 = (candidateRow.Cells[colIdx].Value?.ToString() ?? "").Trim();
                if (!string.IsNullOrEmpty(v1) && !string.IsNullOrEmpty(v2))
                {
                    comparedCount++;
                    if (string.Equals(v1, v2, StringComparison.OrdinalIgnoreCase))
                    {
                        matchCount++;
                    }
                }
            }

            return comparedCount > 0 && ((double)matchCount / comparedCount >= 0.5);
        }
    }

    public class TableRowMeta
    {
        public int OriginalPage { get; set; }
        public string OriginalTable { get; set; } = "";
        public int OriginalRowIndex { get; set; }
    }

    public class TableGridItemInfo
    {
        public int StartRowIndex { get; set; }
        public int RowCount { get; set; }
        public int PageNumber { get; set; }
        public string TableName { get; set; } = "";
        public int ColumnCount { get; set; }
        public string DisplayTitle => $"P.{PageNumber} 【{TableName}】 ({ColumnCount}列 × {RowCount}行)";
        public List<DataGridViewRow> Rows { get; set; } = new();
    }
}
