using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using OCR_Translator.Models;
using OCR_Translator.Services;

namespace OCR_Translator
{
    public partial class Form1
    {
        // 画面右側：バッチ・進捗管理ダッシュボード
        private Panel pnlBatchDashboard = null!;
        private Label lblBatchDashboardTitle = null!;
        private ListBox lstBatches = null!;
        private Button btnLoadSelectedBatch = null!;
        private Button btnSaveAndCloseBatch = null!;
        private Button btnSearchBatchFromDashboard = null!;
        private Button btnExportWordFromDashboard = null!;
        private Button btnResetUiCache = null!;
        private Button btnDeleteDiskOcrData = null!;

        public class BatchItemInfo
        {
            public int BatchIndex { get; set; }
            public int StartPage { get; set; }
            public int EndPage { get; set; }
            public bool IsCompleted { get; set; }
            public int CompletedPagesCount { get; set; }
            public int FootnoteCount { get; set; }
            public int FigureCount { get; set; }
            public int TableCount { get; set; }
            public DateTime? LastModified { get; set; }

            public override string ToString()
            {
                string status = IsCompleted
                    ? $"✅ 完了 ({CompletedPagesCount}/{EndPage - StartPage + 1}P, 注:{FootnoteCount}, 図:{FigureCount}, 表:{TableCount})"
                    : CompletedPagesCount > 0
                        ? $"🔄 処理中 ({CompletedPagesCount}/{EndPage - StartPage + 1}P)"
                        : "⏳ 未処理";

                string timeStr = LastModified.HasValue
                    ? $" [更新: {LastModified.Value:MM/dd HH:mm}]"
                    : "";

                return $"第{BatchIndex}バッチ [{StartPage}〜{EndPage}P]  {status}{timeStr}";
            }
        }

        private void InitializeBatchDashboard()
        {
            pnlBatchDashboard = new Panel
            {
                Dock = DockStyle.Right,
                Width = 295,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(8)
            };

            lblBatchDashboardTitle = new Label
            {
                Text = "📋 バッチ・進捗管理",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 58, 138),
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft
            };

            lstBatches = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Yu Gothic UI", 9.0f, FontStyle.Regular),
                ItemHeight = 44,
                IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawFixed
            };
            lstBatches.DrawItem += LstBatches_DrawItem;
            lstBatches.DoubleClick += (s, e) => LoadSelectedBatch();

            // 下部アクションパネル
            var pnlBottomActions = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 226,
                Padding = new Padding(0, 6, 0, 0)
            };

            btnLoadSelectedBatch = new Button
            {
                Text = "📂 選択バッチを開く/補正",
                Dock = DockStyle.Top,
                Height = 34,
                Font = new Font("Yu Gothic UI", 9.0f, FontStyle.Bold),
                BackColor = Color.White,
                UseVisualStyleBackColor = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            btnLoadSelectedBatch.Click += (s, e) => LoadSelectedBatch();

            btnSaveAndCloseBatch = new Button
            {
                Text = "💾 バッチ保存して閉じる",
                Dock = DockStyle.Top,
                Height = 34,
                Font = new Font("Yu Gothic UI", 9.0f, FontStyle.Bold),
                BackColor = Color.FromArgb(240, 249, 255),
                ForeColor = Color.FromArgb(3, 105, 161),
                UseVisualStyleBackColor = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            btnSaveAndCloseBatch.Click += (s, e) => SaveAndCloseCurrentBatch();

            btnSearchBatchFromDashboard = new Button
            {
                Text = "🔍 バッチ/全ページ検索",
                Dock = DockStyle.Top,
                Height = 34,
                Font = new Font("Yu Gothic UI", 9.0f, FontStyle.Bold),
                BackColor = Color.FromArgb(240, 253, 250),
                ForeColor = Color.FromArgb(13, 148, 136),
                UseVisualStyleBackColor = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            btnSearchBatchFromDashboard.Click += (s, e) => OpenBatchSearchDialog();

            btnExportWordFromDashboard = new Button
            {
                Text = "🆆 全バッチ結合 Word出力",
                Dock = DockStyle.Top,
                Height = 36,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(43, 87, 154),
                ForeColor = Color.White,
                UseVisualStyleBackColor = false,
                Margin = new Padding(0, 0, 0, 4)
            };
            btnExportWordFromDashboard.Click += (s, e) => btnExportWord_Click(s, e);

            btnResetUiCache = new Button
            {
                Text = "🧹 表示クリア (メモリ解放)",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Yu Gothic UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 85, 105),
                BackColor = Color.White,
                UseVisualStyleBackColor = true,
                Margin = new Padding(0, 0, 0, 3)
            };
            btnResetUiCache.Click += (s, e) => ResetUiBatchCache();

            btnDeleteDiskOcrData = new Button
            {
                Text = "🗑 OCR生データを削除 (容量解放)",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Yu Gothic UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(185, 28, 28),
                BackColor = Color.FromArgb(254, 242, 242),
                UseVisualStyleBackColor = true
            };
            btnDeleteDiskOcrData.Click += (s, e) => DeleteDiskOcrData();

            pnlBottomActions.Controls.Add(btnDeleteDiskOcrData);
            pnlBottomActions.Controls.Add(btnResetUiCache);
            pnlBottomActions.Controls.Add(btnExportWordFromDashboard);
            pnlBottomActions.Controls.Add(btnSearchBatchFromDashboard);
            pnlBottomActions.Controls.Add(btnSaveAndCloseBatch);
            pnlBottomActions.Controls.Add(btnLoadSelectedBatch);

            pnlBatchDashboard.Controls.Add(lstBatches);
            pnlBatchDashboard.Controls.Add(pnlBottomActions);
            pnlBatchDashboard.Controls.Add(lblBatchDashboardTitle);

            this.Controls.Add(pnlBatchDashboard);
            pnlBatchDashboard.BringToFront();
        }

        private void LstBatches_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= lstBatches.Items.Count) return;

            e.DrawBackground();
            var item = lstBatches.Items[e.Index] as BatchItemInfo;
            if (item == null) return;

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            // 1行目: バッチ番号 ＆ ステータス
            string line1Title = $"第{item.BatchIndex}バッチ [{item.StartPage}〜{item.EndPage}P]";
            string line1Status = item.IsCompleted
                ? "✅ 完了"
                : item.CompletedPagesCount > 0
                    ? $"🔄 処理中 ({item.CompletedPagesCount}/{item.EndPage - item.StartPage + 1}P)"
                    : "⏳ 未処理";

            Color statusColor = isSelected ? SystemColors.HighlightText
                : item.IsCompleted ? Color.FromArgb(21, 128, 61)
                : item.CompletedPagesCount > 0 ? Color.FromArgb(180, 83, 9)
                : Color.FromArgb(100, 116, 139);

            // 2行目: サブ要素 ＆ 最新更新日時
            string timeStr = item.LastModified.HasValue
                ? $"更新: {item.LastModified.Value:MM/dd HH:mm:ss}"
                : "未保存";
            string line2Sub = $"注:{item.FootnoteCount} 図:{item.FigureCount} 表:{item.TableCount} | {timeStr}";

            Color subColor = isSelected ? Color.FromArgb(226, 232, 240) : Color.FromArgb(100, 116, 139);

            using (Font boldFont = new Font(e.Font ?? lstBatches.Font, FontStyle.Bold))
            using (Font smallFont = new Font(e.Font?.FontFamily ?? lstBatches.Font.FontFamily, 8.0f, FontStyle.Regular))
            using (Brush titleBrush = new SolidBrush(isSelected ? SystemColors.HighlightText : Color.FromArgb(30, 41, 59)))
            using (Brush statusBrush = new SolidBrush(statusColor))
            using (Brush subBrush = new SolidBrush(subColor))
            {
                e.Graphics.DrawString(line1Title, boldFont, titleBrush, e.Bounds.X + 4, e.Bounds.Y + 4);
                var titleSize = e.Graphics.MeasureString(line1Title, boldFont);
                e.Graphics.DrawString(line1Status, boldFont, statusBrush, e.Bounds.X + 4 + titleSize.Width + 6, e.Bounds.Y + 4);

                e.Graphics.DrawString(line2Sub, smallFont, subBrush, e.Bounds.X + 6, e.Bounds.Y + 23);
            }

            if (!isSelected)
            {
                using Pen borderPen = new Pen(Color.FromArgb(226, 232, 240));
                e.Graphics.DrawLine(borderPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            e.DrawFocusRectangle();
        }

        /// <summary>
        /// PDFの総ページ数およびディスク上の page_data.json の存在状況からバッチ一覧を再構築します。
        /// </summary>
        public void RefreshBatchList()
        {
            lstBatches.Items.Clear();
            if (pdfDocument == null || string.IsNullOrEmpty(currentPdfPath)) return;

            int totalPages = pdfDocument.PageCount;
            int batchSize = Math.Max(5, appSettings.BatchPageSize);
            string pdfName = Path.GetFileNameWithoutExtension(currentPdfPath);
            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            string ocrResultsDir = Path.Combine(projectDir, "ocr_results", pdfName);

            int batchIdx = 1;
            for (int startP = 1; startP <= totalPages; startP += batchSize)
            {
                int endP = Math.Min(totalPages, startP + batchSize - 1);
                int completedPages = 0;
                int footnoteCount = 0;
                int figureCount = 0;
                int tableCount = 0;
                DateTime? latestWriteTime = null;

                for (int p = startP; p <= endP; p++)
                {
                    string pageDir = Path.Combine(ocrResultsDir, $"page_{p:0000}");
                    string jsonPath = Path.Combine(pageDir, "page_data.json");
                    if (File.Exists(jsonPath))
                    {
                        DateTime writeTime = File.GetLastWriteTime(jsonPath);
                        if (latestWriteTime == null || writeTime > latestWriteTime)
                        {
                            latestWriteTime = writeTime;
                        }
                    }

                    var pageData = OcrPageDataService.LoadPageData(pageDir);
                    if (pageData != null)
                    {
                        completedPages++;
                        footnoteCount += pageData.Footnotes?.Count ?? 0;
                        figureCount += pageData.Figures?.Count ?? 0;
                        tableCount += pageData.Tables?.Count ?? 0;
                    }
                }

                var batchInfo = new BatchItemInfo
                {
                    BatchIndex = batchIdx++,
                    StartPage = startP,
                    EndPage = endP,
                    CompletedPagesCount = completedPages,
                    IsCompleted = (completedPages == (endP - startP + 1)),
                    FootnoteCount = footnoteCount,
                    FigureCount = figureCount,
                    TableCount = tableCount,
                    LastModified = latestWriteTime
                };

                lstBatches.Items.Add(batchInfo);
            }
        }

        /// <summary>
        /// 単一ページの保存時に、バッチ全体の再スキャンを行わずに対象バッチの更新日時のみを高速更新します。
        /// </summary>
        public void UpdateBatchItemForPage(int pageNum)
        {
            if (lstBatches == null || lstBatches.Items.Count == 0) return;
            for (int i = 0; i < lstBatches.Items.Count; i++)
            {
                if (lstBatches.Items[i] is BatchItemInfo batch && pageNum >= batch.StartPage && pageNum <= batch.EndPage)
                {
                    batch.LastModified = DateTime.Now;
                    lstBatches.Invalidate(lstBatches.GetItemRectangle(i));
                    break;
                }
            }
        }

        /// <summary>
        /// 選択されたバッチのページ範囲を設定し、画像および構造体を読み込みます。
        /// </summary>
        private void LoadSelectedBatch()
        {
            if (lstBatches.SelectedItem is not BatchItemInfo batch)
            {
                MessageBox.Show("読み込むバッチをリストから選択してください。", "バッチ読込", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveCurrentPageData();

            numPageStart.Value = batch.StartPage;
            numPageEnd.Value = batch.EndPage;
            SwitchToPage(batch.StartPage - 1);

            // このバッチの保存済み page_data.json をUIタブ（本文・見出し・注釈文・表・図）へ展開
            LoadBatchDataToUi(batch.StartPage, batch.EndPage);

            txtLog.AppendText($"【バッチ読込】第{batch.BatchIndex}バッチ ({batch.StartPage}〜{batch.EndPage}P) を読み込みました。" + Environment.NewLine);
        }

        /// <summary>
        /// 指定ページ範囲の保存済みデータをUIの各タブへ展開します。
        /// </summary>
        private void LoadBatchDataToUi(int startPage, int endPage)
        {
            if (string.IsNullOrEmpty(currentPdfPath)) return;
            string pdfName = Path.GetFileNameWithoutExtension(currentPdfPath);
            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            string ocrResultsDir = Path.Combine(projectDir, "ocr_results", pdfName);

            isSyncingHeadings = true;
            try
            {
                ClearOcrResultTabs();

                string? currentRunningChapter = null;
                if (startPage > 1)
                {
                    for (int prevP = startPage - 1; prevP >= 1; prevP--)
                    {
                        string prevDir = Path.Combine(ocrResultsDir, $"page_{prevP:0000}");
                        var prevData = OcrPageDataService.LoadPageData(prevDir);
                        if (prevData?.Headings != null)
                        {
                            var major = prevData.Headings.FirstOrDefault(IsMajorChapterHeading);
                            if (!string.IsNullOrEmpty(major))
                            {
                                currentRunningChapter = major;
                                break;
                            }
                        }
                    }
                }

                for (int p = startPage; p <= endPage; p++)
            {
                string pageDir = Path.Combine(ocrResultsDir, $"page_{p:0000}");
                var pageData = OcrPageDataService.LoadPageData(pageDir);
                if (pageData == null) continue;

                pageData.PageNumber = p;
                ocrPageDataList.RemoveAll(x => x.PageNumber == p);
                ocrPageDataList.Add(pageData);

                // 本文タブ
                if (ocrResultTextBoxes.TryGetValue("body", out var bBox))
                {
                    if (pageData.BodyParagraphs != null && pageData.BodyParagraphs.Count > 0)
                    {
                        string fullPageBody = $"--- ページ {p} ---" + Environment.NewLine +
                                              string.Join(Environment.NewLine + Environment.NewLine, pageData.BodyParagraphs) + Environment.NewLine + Environment.NewLine;
                        AppendColoredBodyToTextBox(bBox, fullPageBody, pageData.Headings, appSettings.AutoDetectSubheadings);
                    }
                }

                // 見出しタブ
                if (ocrResultTextBoxes.TryGetValue("heading", out var hBox))
                {
                    if (pageData.Headings == null) pageData.Headings = new List<string>();
                    bool headingsChanged = false;

                    // 1. 本文から大見出し（章タイトル等）および小見出しの自動検出
                    // 大見出し（IsMajorHeading）は小見出し自動認識設定のオン/オフにかかわらず常に自動検出
                    // 小見出し（IsSubheadingText）は appSettings.AutoDetectSubheadings が有効な場合に自動検出
                    if (pageData.BodyParagraphs != null && pageData.BodyParagraphs.Count > 0)
                    {
                        foreach (var para in pageData.BodyParagraphs)
                        {
                            foreach (var line in para.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                            {
                                string trimmedLine = line.Trim();
                                if (trimmedLine.Length > 80 || trimmedLine.Contains("。") || Regex.IsMatch(trimmedLine, @"^【(?:注釈文|注)\d+】") || DocxExporter.IsGarbageOrNoiseHeading(trimmedLine)) continue;

                                string cleanH = DocxExporter.RemovePagePrefix(trimmedLine);
                                if (string.IsNullOrWhiteSpace(cleanH)) continue;

                                bool isMajor = IsMajorChapterHeading(cleanH);
                                bool isSection = Regex.IsMatch(cleanH, @"^第[0-9０-９一二三四五六七八九十百千万]+節(?:[\s　・:：].*)?$");
                                bool isSub = appSettings.AutoDetectSubheadings && OcrSorter.IsSubheadingText(trimmedLine);

                                if (isMajor || isSection || (!IsHeadingRemoved(pageData, p, cleanH) && isSub))
                                {
                                    if (isMajor || isSection)
                                    {
                                        pageData.RemovedHeadings?.RemoveAll(rh => rh.Equals(cleanH, StringComparison.OrdinalIgnoreCase) || DocxExporter.NormalizeForComparison(rh) == DocxExporter.NormalizeForComparison(cleanH));
                                        userRemovedHeadings.Remove($"{p}::{cleanH}");
                                    }

                                    if (!pageData.Headings.Contains(cleanH, StringComparer.OrdinalIgnoreCase))
                                    {
                                        if (isMajor)
                                        {
                                            pageData.Headings.Insert(0, cleanH);
                                            currentRunningChapter = cleanH;
                                        }
                                        else
                                        {
                                            pageData.Headings.Add(cleanH);
                                        }
                                        headingsChanged = true;
                                    }
                                }
                            }
                        }
                    }

                    // 2. ページ上部の見出し（章タイトル・柱）から大見出しを検出（BodyParagraphsに含まれていなかった場合）
                    if (!pageData.Headings.Any(IsMajorChapterHeading))
                    {
                        string pageJsonPath = Path.Combine(pageDir, "page.json");
                        if (File.Exists(pageJsonPath))
                        {
                            try
                            {
                                string jsonText = File.ReadAllText(pageJsonPath, Encoding.UTF8);
                                using var doc = System.Text.Json.JsonDocument.Parse(jsonText);
                                if (doc.RootElement.TryGetProperty("results", out var resultsElem))
                                {
                                    foreach (var item in resultsElem.EnumerateArray())
                                    {
                                        int y = item.TryGetProperty("y", out var yElem) ? yElem.GetInt32() : 9999;
                                        if (y > 250) continue; // ページ上部（250px未満）の見出し・柱のみを対象

                                        string text = item.TryGetProperty("text", out var tElem) ? tElem.GetString() ?? "" : "";
                                        string cleanText = DocxExporter.RemovePagePrefix(text.Trim());
                                        // 末尾のページ番号を除去（例: "序章 民族自決運動の比較政治史 3" -> "序章 民族自決運動の比較政治史"）
                                        cleanText = Regex.Replace(cleanText, @"[\s　・:]*\d+$", "").Trim();

                                        if (IsMajorChapterHeading(cleanText) && !IsHeadingRemoved(pageData, p, cleanText))
                                        {
                                            // 前ページまでの章タイトルと異なる新しい大見出し（章開始）である場合のみ登録
                                            if (string.IsNullOrEmpty(currentRunningChapter) ||
                                                (!cleanText.Equals(currentRunningChapter, StringComparison.OrdinalIgnoreCase) &&
                                                 !cleanText.StartsWith(currentRunningChapter, StringComparison.OrdinalIgnoreCase) &&
                                                 !currentRunningChapter.StartsWith(cleanText, StringComparison.OrdinalIgnoreCase)))
                                            {
                                                if (!pageData.Headings.Contains(cleanText, StringComparer.OrdinalIgnoreCase))
                                                {
                                                    pageData.Headings.Insert(0, cleanText);
                                                    currentRunningChapter = cleanText;
                                                    headingsChanged = true;
                                                }
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    if (headingsChanged)
                    {
                        OcrPageDataService.SavePageData(pageDir, pageData);
                    }

                    // 現在の章タイトルを更新
                    var existingMajor = pageData.Headings.FirstOrDefault(IsMajorChapterHeading);
                    if (!string.IsNullOrEmpty(existingMajor))
                    {
                        currentRunningChapter = existingMajor;
                    }

                    foreach (var h in pageData.Headings.ToList())
                    {
                        string cleanH = DocxExporter.RemovePagePrefix(h);
                        if (!string.IsNullOrWhiteSpace(cleanH) && !DocxExporter.IsGarbageOrNoiseHeading(cleanH))
                        {
                            hBox.AppendText($"[P{p}] {cleanH}" + Environment.NewLine);
                        }
                    }
                }

                // 注釈文タブ
                if (ocrResultTextBoxes.TryGetValue("footnote", out var fBox) && pageData.Footnotes != null)
                {
                    foreach (var fn in pageData.Footnotes.ToList())
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(fn, @"^(【(?:注釈文|注)\d+】)\s*(.*)$");
                        if (match.Success)
                        {
                            AppendColoredFootnoteToTextBox(fBox, $"[P{p}]", match.Groups[1].Value, match.Groups[2].Value);
                        }
                        else
                        {
                            AppendColoredFootnoteToTextBox(fBox, $"[P{p}]", "【注釈文】", fn);
                        }
                    }
                }

                // 表
                if (pageData.Tables != null && pageData.Tables.Count > 0 && dgvOcrTable != null)
                {
                    int maxCols = pageData.Tables.Max(t => t.ColumnCount);
                    int currentDataCols = dgvOcrTable.Columns.Count > 3 ? dgvOcrTable.Columns.Count - 3 : 0;

                    if (dgvOcrTable.Columns.Count == 0)
                    {
                        dgvOcrTable.Columns.Clear();
                        dgvOcrTable.Columns.Add("Page", "ページ");
                        dgvOcrTable.Columns.Add("Table", "表名");
                        dgvOcrTable.Columns.Add("Row", "行");
                        dgvOcrTable.Columns["Page"]!.FillWeight = 8;
                        dgvOcrTable.Columns["Table"]!.FillWeight = 12;
                        dgvOcrTable.Columns["Row"]!.FillWeight = 8;
                        dgvOcrTable.Columns["Page"]!.ReadOnly = true;
                        dgvOcrTable.Columns["Table"]!.ReadOnly = true;
                        dgvOcrTable.Columns["Row"]!.ReadOnly = true;

                        for (int c = 1; c <= maxCols; c++)
                        {
                            int colIdx = dgvOcrTable.Columns.Add($"Col{c}", $"列{c}");
                            dgvOcrTable.Columns[colIdx]!.FillWeight = Math.Max(15, 72 / maxCols);
                        }
                    }
                    else if (maxCols > currentDataCols)
                    {
                        for (int c = currentDataCols + 1; c <= maxCols; c++)
                        {
                            int colIdx = dgvOcrTable.Columns.Add($"Col{c}", $"列{c}");
                            dgvOcrTable.Columns[colIdx]!.FillWeight = Math.Max(15, 72 / maxCols);
                        }
                    }

                    foreach (var sTable in pageData.Tables.OrderBy(t => t.TableName, StringComparer.OrdinalIgnoreCase))
                    {
                        int baseRow = dgvOcrTable.Rows.Count;
                        foreach (var sRow in sTable.Rows)
                        {
                            var rowCells = new object[dgvOcrTable.Columns.Count];
                            rowCells[0] = sRow.PageNumber;
                            rowCells[1] = sRow.TableName;
                            rowCells[2] = sRow.RowIndex;

                            for (int c = 0; c < sRow.Cells.Count && (c + 3) < rowCells.Length; c++)
                            {
                                rowCells[c + 3] = sRow.Cells[c];
                            }

                            dgvOcrTable.Rows.Add(rowCells);
                        }

                        int colOffset = dgvOcrTable.Columns.Count > 3 && dgvOcrTable.Columns[0].Name == "Page" ? 3 : 0;
                        foreach (var span in sTable.MergeSpans)
                        {
                            tableMergeSpans.Add(new TableMergeSpan(colOffset + span.StartCol, baseRow + span.StartRow, span.ColSpan, span.RowSpan)
                            {
                                MergedText = span.MergedText
                            });
                        }
                    }
                }

                // 図
                if (pageData.Figures != null && pageData.Figures.Count > 0)
                {
                    foreach (var fig in pageData.Figures)
                    {
                        extractedFigures.RemoveAll(f => f.PageNumber == p && f.Name == fig.Name);
                        extractedFigures.Add(fig);
                    }
                }
            }

            RefreshFigureGalleryView();

            // 全ページの本文から抽出した最新の小見出し一覧を見出しタブへ完全反映
            SyncHeadings(null, updateHeadingTab: true);

            if (tabOcrResult != null && tabOcrText != null)
            {
                tabOcrResult.SelectedTab = tabOcrText;
            }
            if (ocrResultTextBoxes.TryGetValue("body", out var bodyBox))
            {
                bodyBox.SelectionStart = 0;
                bodyBox.ScrollToCaret();
            }
            }
            finally
            {
                isSyncingHeadings = false;
            }
        }

        /// <summary>
        /// 現在のバッチデータをすべて保存し、メモリを解放してバッチを閉じます。
        /// </summary>
        private void SaveAndCloseCurrentBatch()
        {
            if (pdfDocument == null || string.IsNullOrEmpty(currentPdfPath)) return;

            SaveCurrentPageData();

            int startP = (int)numPageStart.Value;
            int endP = (int)numPageEnd.Value;

            RefreshBatchList();

            txtLog.AppendText($"【バッチ保存完了】P.{startP}〜P.{endP} の構造体を保存し、メモリを解放しました。" + Environment.NewLine);
            MessageBox.Show($"バッチ（P.{startP}〜{endP}）の構造体データを保存しました。\n右側パネルのステータスが更新されました。", "バッチ保存完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// UIのタブ表示とメモリ上のバッチデータをクリアして解放します（ディスク上のOCRデータは安全に保持されます）。
        /// </summary>
        private void ResetUiBatchCache()
        {
            var confirm = MessageBox.Show(
                "UIタブの表示と作業メモリをクリアしますか？\n\n※ ディスクに保存されたOCR結果データ（page_data.jsonやOCR生データ）は安全に保持されますので、いつでも再度バッチを読み込めます。",
                "表示クリア（メモリ解放）の確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            ocrPageDataList.Clear();
            ClearOcrResultTabs();
            RefreshBatchList();

            txtLog.AppendText("【表示クリア】UIタブの表示と作業メモリを解放しました（ディスクデータは保持されています）。" + Environment.NewLine);
        }

        /// <summary>
        /// Word文書（.docx）出力完了後などに、ディスク上のOCR解析生データフォルダ（画像やJSON）を一括削除してディスク容量を解放します。
        /// </summary>
        private void DeleteDiskOcrData()
        {
            if (string.IsNullOrEmpty(currentPdfPath))
            {
                MessageBox.Show("PDFが開かれていません。", "OCR生データ削除", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string pdfName = Path.GetFileNameWithoutExtension(currentPdfPath);
            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            string ocrResultsDir = Path.Combine(projectDir, "ocr_results", pdfName);

            if (!Directory.Exists(ocrResultsDir))
            {
                MessageBox.Show($"「{pdfName}」のOCR生データフォルダは既に存在しないか、削除済みです。", "削除完了済み", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"【OCR生データの完全削除】\n\n" +
                $"「{pdfName}」のディスク上の全OCR生データ（画像、page.json、構造体等）を削除します。\n\n" +
                "・Word文書（.docx）が出力済みで、ディスク容量を節約したい場合に実行します。\n" +
                "・削除すると、アプリ内でバッチを再度開いて補正・再出力することはできなくなります。\n\n" +
                "本当に削除してもよろしいですか？",
                "OCR生データ削除の確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            try
            {
                Directory.Delete(ocrResultsDir, true);

                ocrPageDataList.Clear();
                ClearOcrResultTabs();
                RefreshBatchList();

                txtLog.AppendText($"【OCR生データ削除】「{pdfName}」の全OCR生データフォルダを削除し、ディスク容量を解放しました。" + Environment.NewLine);
                MessageBox.Show("OCR生データ（画像・JSON）をディスクから完全に削除しました。\nディスク容量が解放されました。", "削除完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("削除中にエラーが発生しました。\n" + ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
