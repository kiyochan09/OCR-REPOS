using PdfiumViewer;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using OCR_Translator.Models;
using OCR_Translator.Services;

namespace OCR_Translator
{
    public partial class Form1
    {
        private void ShowCurrentPage()
        {
            if (pdfDocument == null) return;
            if (currentPage < 0 || currentPage >= pdfDocument.PageCount) return;

            try
            {
                int dpi = appSettings.RenderDpi > 0 ? appSettings.RenderDpi : DefaultPdfRenderDpi;
                using Image image = pdfDocument.Render(
                    currentPage, dpi, dpi,
                    PdfRenderFlags.Annotations | PdfRenderFlags.ForPrinting | PdfRenderFlags.LcdText | PdfRenderFlags.CorrectFromDpi);

                Bitmap displayBitmap = new Bitmap(image);
                Image? oldImage = pictureBox1.Image;
                pictureBox1.Image = displayBitmap;
                oldImage?.Dispose();

                UpdateCanvasLayout();
                UpdatePageDisplayTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "ページの表示に失敗しました。\n\n" + ex.Message,
                    "PDF表示エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void UpdateTargetRangeDisplay()
        {
            if (pdfDocument == null)
            {
                if (lblTargetRangeInfo != null)
                    lblTargetRangeInfo.Text = "/ 0P";
                return;
            }

            int startP = (int)numPageStart.Value;
            int endP = (int)numPageEnd.Value;
            int count = Math.Max(0, endP - startP + 1);

            if (lblTargetRangeInfo != null)
            {
                lblTargetRangeInfo.Text = $"/ {pdfDocument.PageCount}P ({count}P)";
            }

            UpdatePageDisplayTitle();
        }

        private bool isSwitchingPage = false;

        private void UpdatePageDisplayTitle()
        {
            if (pdfDocument == null)
            {
                Text = "OCR Translator";
                isUpdatingPageJump = true;
                try
                {
                    numCurrentPage.Minimum = 1;
                    numCurrentPage.Maximum = 1;
                    numCurrentPage.Value = 1;
                    numCurrentPage.Enabled = false;
                    lblTotalPages.Text = "/ 0";
                    if (lblTargetRangeInfo != null)
                        lblTargetRangeInfo.Text = "/ 0P";
                    btnFirstPage.Enabled = false;
                    btnPrevPage.Enabled = false;
                    btnNextPage.Enabled = false;
                    btnLastPage.Enabled = false;
                    btnNextBatch20.Enabled = false;
                }
                finally
                {
                    isUpdatingPageJump = false;
                }
                return;
            }

            string fileName = string.IsNullOrEmpty(currentPdfPath)
                ? "名称未設定"
                : System.IO.Path.GetFileName(currentPdfPath);
            int startP = (int)numPageStart.Value;
            int endP = (int)numPageEnd.Value;
            int count = Math.Max(0, endP - startP + 1);
            Text = $"OCR Translator - [{fileName}] - 表示: P.{currentPage + 1}/{pdfDocument.PageCount} - 【対象範囲: P.{startP}〜{endP} (計{count}P)】";

            isUpdatingPageJump = true;
            try
            {
                numCurrentPage.Enabled = true;
                numCurrentPage.Minimum = 1;
                numCurrentPage.Maximum = pdfDocument.PageCount;
                numCurrentPage.Value = Math.Clamp(currentPage + 1, 1, pdfDocument.PageCount);
                lblTotalPages.Text = $"/ {pdfDocument.PageCount}";
                if (lblTargetRangeInfo != null)
                {
                    lblTargetRangeInfo.Text = $"/ {pdfDocument.PageCount}P ({count}P)";
                }

                btnPrevPage.Enabled = (currentPage > 0);
                btnNextPage.Enabled = (currentPage < pdfDocument.PageCount - 1);
                btnNextBatch20.Enabled = true;

                if (appSettings.FirstLastNavScope == "batch")
                {
                    int batchStartIdx = Math.Max(0, (int)numPageStart.Value - 1);
                    int batchEndIdx = Math.Min(pdfDocument.PageCount - 1, (int)numPageEnd.Value - 1);

                    btnFirstPage.Enabled = (currentPage > 0);
                    btnLastPage.Enabled = (currentPage < pdfDocument.PageCount - 1);

                    toolTipMain.SetToolTip(btnFirstPage, currentPage == batchStartIdx
                        ? "ファイル先頭ページ (P.1) へジャンプ"
                        : $"作業バッチ先頭ページ (P.{batchStartIdx + 1}) へジャンプ");
                    toolTipMain.SetToolTip(btnLastPage, currentPage == batchEndIdx
                        ? $"ファイル末尾ページ (P.{pdfDocument.PageCount}) へジャンプ"
                        : $"作業バッチ末尾ページ (P.{batchEndIdx + 1}) へジャンプ");
                }
                else
                {
                    btnFirstPage.Enabled = (currentPage > 0);
                    btnLastPage.Enabled = (currentPage < pdfDocument.PageCount - 1);
                    toolTipMain.SetToolTip(btnFirstPage, "最初のページ (P.1)");
                    toolTipMain.SetToolTip(btnLastPage, $"最後のページ (P.{pdfDocument.PageCount})");
                }
            }
            finally
            {
                isUpdatingPageJump = false;
            }
        }

        private void SwitchToPage(int pageIndex)
        {
            if (pdfDocument == null) return;
            if (pageIndex < 0 || pageIndex >= pdfDocument.PageCount) return;
            if (isSwitchingPage) return;

            isSwitchingPage = true;
            try
            {
                SaveCurrentPageData();
                currentPage = pageIndex;

                int pageNum = pageIndex + 1;
                int startP = (int)numPageStart.Value;
                int endP = (int)numPageEnd.Value;

                // 指定ページが現在ロード中のバッチ範囲外の場合、自動的に該当バッチへ更新してデータロード
                if (pageNum < startP || pageNum > endP)
                {
                    int batchSize = Math.Max(5, appSettings.BatchPageSize);
                    int bStart = ((pageNum - 1) / batchSize) * batchSize + 1;
                    int bEnd = Math.Min(pdfDocument.PageCount, bStart + batchSize - 1);
                    numPageStart.Value = bStart;
                    numPageEnd.Value = bEnd;
                    LoadBatchDataToUi(bStart, bEnd);
                }

                LoadCurrentPageRegions();
                ShowCurrentPage();

                // OCR結果画面（本文・見出し・注釈）を該当ページ位置へ連動スクロール
                ScrollOcrResultToPage(pageNum);
            }
            finally
            {
                isSwitchingPage = false;
            }
        }

        /// <summary>
        /// OCR結果画面（本文・見出し・注釈文）を現在選択されたページの表示位置へ連動スクロールします。
        /// </summary>
        public void ScrollOcrResultToPage(int pageNum)
        {
            try
            {
                // 1. 本文タブのスクロール連動
                if (ocrResultTextBoxes.TryGetValue("body", out var bBox) && bBox.TextLength > 0)
                {
                    string targetHeader = $"--- ページ {pageNum} ---";
                    int idx = bBox.Find(targetHeader, RichTextBoxFinds.None);
                    if (idx < 0)
                    {
                        targetHeader = $"--- ページ {pageNum}";
                        idx = bBox.Find(targetHeader, RichTextBoxFinds.None);
                    }
                    if (idx >= 0)
                    {
                        bBox.SelectionStart = idx;
                        bBox.SelectionLength = 0;
                        bBox.ScrollToCaret();
                    }
                }

                // 2. 見出しタブのスクロール連動
                if (ocrResultTextBoxes.TryGetValue("heading", out var hBox) && hBox.TextLength > 0)
                {
                    string targetHeading = $"[P{pageNum}]";
                    int idx = hBox.Find(targetHeading, RichTextBoxFinds.None);
                    if (idx >= 0)
                    {
                        hBox.SelectionStart = idx;
                        hBox.SelectionLength = 0;
                        hBox.ScrollToCaret();
                    }
                }

                // 3. 注釈文タブのスクロール連動
                if (ocrResultTextBoxes.TryGetValue("footnote", out var fBox) && fBox.TextLength > 0)
                {
                    string targetFootnote = $"[P{pageNum}]";
                    int idx = fBox.Find(targetFootnote, RichTextBoxFinds.None);
                    if (idx >= 0)
                    {
                        fBox.SelectionStart = idx;
                        fBox.SelectionLength = 0;
                        fBox.ScrollToCaret();
                    }
                }
            }
            catch
            {
                // スクロール時の例外は安全に無視
            }
        }

        /// <summary>
        /// 検索結果アイテムへジャンプし、該当ページを表示してテキストをハイライト選択・スクロールします。
        /// </summary>
        public void JumpToSearchResult(int pageNumber, string targetType, string searchText, string snippet)
        {
            if (pdfDocument == null) return;
            if (pageNumber < 1 || pageNumber > pdfDocument.PageCount) return;

            // 1. ページ移動（必要に応じてバッチ読込・画像表示）
            if (currentPage != pageNumber - 1)
            {
                SwitchToPage(pageNumber - 1);
            }

            // 2. 該当タブへの切替
            RichTextBox? targetRtb = null;
            if (tabOcrResult != null)
            {
                if (targetType == "本文")
                {
                    if (tabOcrText != null) tabOcrResult.SelectedTab = tabOcrText;
                    ocrResultTextBoxes.TryGetValue("body", out targetRtb);
                }
                else if (targetType == "見出し")
                {
                    var tab = tabOcrResult.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Text.Contains("見出し"));
                    if (tab != null) tabOcrResult.SelectedTab = tab;
                    ocrResultTextBoxes.TryGetValue("heading", out targetRtb);
                }
                else if (targetType == "注釈文")
                {
                    var tab = tabOcrResult.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Text.Contains("注釈"));
                    if (tab != null) tabOcrResult.SelectedTab = tab;
                    ocrResultTextBoxes.TryGetValue("footnote", out targetRtb);
                }
                else if (targetType == "未分類")
                {
                    var tab = tabOcrResult.TabPages.Cast<TabPage>().FirstOrDefault(t => t.Text.Contains("未分類"));
                    if (tab != null) tabOcrResult.SelectedTab = tab;
                    ocrResultTextBoxes.TryGetValue("unclassified", out targetRtb);
                }
                else if (targetType == "表")
                {
                    if (tabOcrTable != null) tabOcrResult.SelectedTab = tabOcrTable;
                    if (dgvOcrTable != null && dgvOcrTable.RowCount > 0)
                    {
                        foreach (DataGridViewRow row in dgvOcrTable.Rows)
                        {
                            foreach (DataGridViewCell cell in row.Cells)
                            {
                                if (cell.Value != null && cell.Value.ToString()!.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                                {
                                    dgvOcrTable.ClearSelection();
                                    cell.Selected = true;
                                    dgvOcrTable.CurrentCell = cell;
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            // 3. RichTextBox 内でのキーワードハイライト＆スクロール
            if (targetRtb != null && targetRtb.TextLength > 0 && !string.IsNullOrEmpty(searchText))
            {
                targetRtb.HideSelection = false;

                // ページヘッダー（--- ページ N --- または [PN]）の位置を探索
                int searchStartPos = 0;
                string pageHeader1 = $"--- ページ {pageNumber} ---";
                string pageHeader2 = $"[P{pageNumber}]";
                int pHeaderIdx = targetRtb.Find(pageHeader1, RichTextBoxFinds.None);
                if (pHeaderIdx < 0) pHeaderIdx = targetRtb.Find(pageHeader2, RichTextBoxFinds.None);
                if (pHeaderIdx >= 0)
                {
                    searchStartPos = pHeaderIdx;
                }

                // まずページヘッダー以降でキーワードを探索
                int foundIdx = targetRtb.Find(searchText, searchStartPos, RichTextBoxFinds.None);
                if (foundIdx < 0 && searchStartPos > 0)
                {
                    // 見つからなければ全文から探索
                    foundIdx = targetRtb.Find(searchText, 0, RichTextBoxFinds.None);
                }

                if (foundIdx >= 0)
                {
                    targetRtb.Select(foundIdx, searchText.Length);
                    targetRtb.ScrollToCaret();
                }
                else if (!string.IsNullOrEmpty(snippet))
                {
                    string firstSnippetWord = snippet.Split(' ', '　', '\t', '\r', '\n').FirstOrDefault(w => w.Length >= 3) ?? "";
                    if (!string.IsNullOrEmpty(firstSnippetWord))
                    {
                        int snipIdx = targetRtb.Find(firstSnippetWord, searchStartPos, RichTextBoxFinds.None);
                        if (snipIdx >= 0)
                        {
                            targetRtb.Select(snipIdx, firstSnippetWord.Length);
                            targetRtb.ScrollToCaret();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 現在編集中の全ページ（本文・見出し・注釈文・図・表）を同期し、ディスクの page_data.json およびメモリへ安全に自動保存します。
        /// </summary>
        private void SaveCurrentPageData()
        {
            if (string.IsNullOrEmpty(currentPdfPath) || pdfDocument == null) return;

            SaveCurrentPageRegions();

            string pdfName = System.IO.Path.GetFileNameWithoutExtension(currentPdfPath);
            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            string ocrResultsDir = System.IO.Path.Combine(projectDir, "ocr_results", pdfName);

            string bodyText = ocrResultTextBoxes.TryGetValue("body", out var bBox) ? bBox.Text : "";
            string headingText = ocrResultTextBoxes.TryGetValue("heading", out var hBox) ? hBox.Text : "";
            string footnoteText = ocrResultTextBoxes.TryGetValue("footnote", out var fBox) ? fBox.Text : "";

            var bodyByPage = ParseBodyTextByPages(bodyText);
            var headingsByPageWithLevels = ParseHeadingsByPagesWithLevels(headingText);
            var footnotesByPage = OcrPageDataService.ParseFootnotesByPages(footnoteText);

            // 保存対象のページ番号セット（現在ページ ＋ UIに存在する全ページ）
            var pagesToSave = new HashSet<int> { currentPage + 1 };
            foreach (var k in bodyByPage.Keys) pagesToSave.Add(k);
            foreach (var k in headingsByPageWithLevels.Keys) pagesToSave.Add(k);
            foreach (var k in footnotesByPage.Keys) pagesToSave.Add(k);
            foreach (var p in ocrPageDataList.ToList()) pagesToSave.Add(p.PageNumber);

            List<StructuredTable> allTables = new();
            if (dgvOcrTable != null && dgvOcrTable.RowCount > 0)
            {
                allTables = TableCellMerger.ExtractTablesFromDataGridView(dgvOcrTable, tableMergeSpans);
            }

            foreach (int pNum in pagesToSave)
            {
                string pageDir = System.IO.Path.Combine(ocrResultsDir, $"page_{pNum:0000}");

                // 既存または新規の OcrPageData を取得（ディスクに既存データがあればそれを読み込んで保持）
                var pageData = ocrPageDataList.FirstOrDefault(p => p.PageNumber == pNum);
                if (pageData == null)
                {
                    pageData = OcrPageDataService.LoadPageData(pageDir);
                    if (pageData == null)
                    {
                        pageData = new OcrPageData { PageNumber = pNum };
                    }
                    ocrPageDataList.Add(pageData);
                }

                // 本文の同期（UIに該当ページの本文がある場合のみ更新）
                if (bodyByPage.TryGetValue(pNum, out var paras) && paras.Count > 0)
                {
                    pageData.BodyParagraphs = paras;
                }

                // 見出しタブの同期（UIの見出しタブに該当ページの見出しがある場合、ユーザーの手動編集を最優先で反映）
                if (headingsByPageWithLevels.TryGetValue(pNum, out var levelHeadings))
                {
                    var newHeadings = levelHeadings.Headings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var newSubheadings = levelHeadings.Subheadings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var allUserHeadings = newHeadings.Concat(newSubheadings).ToList();

                    // ユーザーが見出しタブに明示的に記載した見出しは、RemovedHeadings から削除（復活）
                    if (pageData.RemovedHeadings != null)
                    {
                        pageData.RemovedHeadings.RemoveAll(rh => allUserHeadings.Any(nh =>
                            nh.Equals(rh, StringComparison.OrdinalIgnoreCase) ||
                            DocxExporter.NormalizeForComparison(nh) == DocxExporter.NormalizeForComparison(rh)));
                    }
                    foreach (var nh in allUserHeadings)
                    {
                        userRemovedHeadings.Remove($"{pNum}::{nh}");
                    }

                    // ユーザーが見出しタブから手動削除した見出しを検出し、RemovedHeadings に追加して本文の強調を解除
                    var existingCombined = (pageData.Headings ?? new List<string>()).Concat(pageData.Subheadings ?? new List<string>()).ToList();
                    foreach (var oldH in existingCombined)
                    {
                        string cleanOld = DocxExporter.RemovePagePrefix(oldH);
                        if (!allUserHeadings.Contains(cleanOld, StringComparer.OrdinalIgnoreCase))
                        {
                            if (pageData.RemovedHeadings == null) pageData.RemovedHeadings = new List<string>();
                            if (!pageData.RemovedHeadings.Contains(cleanOld, StringComparer.OrdinalIgnoreCase))
                            {
                                pageData.RemovedHeadings.Add(cleanOld);
                            }
                            userRemovedHeadings.Add($"{pNum}::{cleanOld}");
                            UnmarkHeadingInBodyRichTextBox(pNum, cleanOld);
                        }
                    }

                    pageData.Headings = newHeadings;
                    pageData.Subheadings = newSubheadings;
                }
                else if (tabOcrResult?.SelectedTab?.Text.Contains("見出し") == true &&
                         bodyByPage.ContainsKey(pNum) &&
                         !isSyncingHeadings &&
                         ((pageData.Headings != null && pageData.Headings.Count > 0) || (pageData.Subheadings != null && pageData.Subheadings.Count > 0)))
                {
                    // 該当ページが現在UIにロードされており、かつ見出しタブが選択された状態で該当ページの見出しが1件もない場合（ユーザーが全削除した場合）
                    var existingCombined = (pageData.Headings ?? new List<string>()).Concat(pageData.Subheadings ?? new List<string>()).ToList();
                    foreach (var oldH in existingCombined)
                    {
                        string cleanOld = DocxExporter.RemovePagePrefix(oldH);
                        if (pageData.RemovedHeadings == null) pageData.RemovedHeadings = new List<string>();
                        if (!pageData.RemovedHeadings.Contains(cleanOld, StringComparer.OrdinalIgnoreCase))
                        {
                            pageData.RemovedHeadings.Add(cleanOld);
                        }
                        userRemovedHeadings.Add($"{pNum}::{cleanOld}");
                        UnmarkHeadingInBodyRichTextBox(pNum, cleanOld);
                    }
                    pageData.Headings?.Clear();
                    pageData.Subheadings?.Clear();
                }

                // 注釈文タブの同期（UIに該当ページの注釈文がある場合のみ更新）
                if (footnotesByPage.TryGetValue(pNum, out var footnotes) && footnotes.Count > 0)
                {
                    pageData.Footnotes = footnotes;
                }

                // 図
                var pageFigs = extractedFigures.Where(f => f.PageNumber == pNum).ToList();
                if (pageFigs.Count > 0)
                {
                    pageData.Figures = pageFigs;
                }

                // 表
                if (allTables.Count > 0)
                {
                    var pTables = allTables.Where(t => t.PageNumber == pNum).ToList();
                    if (pTables.Count > 0)
                    {
                        pageData.Tables = pTables;
                    }
                }

                // ディスクへ永続保存（空のダミーデータで既存データを破壊しないよう安全チェック）
                bool hasValidContent = (pageData.BodyParagraphs != null && pageData.BodyParagraphs.Count > 0) ||
                                      (pageData.Headings != null && pageData.Headings.Count > 0) ||
                                      (pageData.Tables != null && pageData.Tables.Count > 0) ||
                                      (pageData.Figures != null && pageData.Figures.Count > 0) ||
                                      (pageData.Footnotes != null && pageData.Footnotes.Count > 0);

                if (hasValidContent)
                {
                    OcrPageDataService.SavePageData(pageDir, pageData);
                }

                // バッチ管理ダッシュボードの更新日時を高速更新
                UpdateBatchItemForPage(pNum);
            }
        }

        private void btnClosePdf_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentPdfPath) && pdfDocument == null) return;

            try
            {
                SaveCurrentPageData();

                string closedName = string.IsNullOrEmpty(currentPdfPath) ? "PDF" : System.IO.Path.GetFileName(currentPdfPath);

                pdfDocument?.Dispose();
                pdfDocument = null;
                currentPdfPath = "";

                pageRegions.Clear();
                autoPageRegions.Clear();
                regions.Clear();
                lstRegions.Items.Clear();
                ClearOcrResultTabs();

                Image? oldImage = pictureBox1.Image;
                pictureBox1.Image = null;
                oldImage?.Dispose();

                UpdateCanvasLayout();
                UpdatePageDisplayTitle();
                RefreshBatchList();

                txtLog.AppendText($"【PDFを閉じる】{closedName} の設定および編集内容を保存し、PDFを閉じました。" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                MessageBox.Show("PDFの保存・終了処理中にエラーが発生しました。\n" + ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnOpenPdf_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*";
            dialog.Title = "PDFファイルを開く";

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                pdfDocument?.Dispose();
                pdfDocument = null;

                currentPdfPath = dialog.FileName;
                pageRegions.Clear();
                autoPageRegions.Clear();
                regions.Clear();
                lstRegions.Items.Clear();
                ClearOcrResultTabs();
                txtLog.Clear();

                pdfDocument = PdfDocument.Load(currentPdfPath);
                currentPage = 0;

                int batchSize = Math.Max(5, appSettings.BatchPageSize);

                numPageStart.Minimum = 1;
                numPageStart.Maximum = pdfDocument.PageCount;
                numPageStart.Value = 1;

                numPageEnd.Minimum = 1;
                numPageEnd.Maximum = pdfDocument.PageCount;
                numPageEnd.Value = Math.Min(batchSize, pdfDocument.PageCount);

                string pdfName = System.IO.Path.GetFileNameWithoutExtension(currentPdfPath);
                string projectDir = OcrProcessor.FindOcrEngineDirectory();
                var normalizedPages = OcrPageDataService.NormalizeAndRenumberProjectFootnotes(
                    projectDir, pdfName, pdfDocument.PageCount, appSettings);
                ocrPageDataList.Clear();
                ocrPageDataList.AddRange(normalizedPages);

                RefreshBatchList();

                LoadCurrentPageRegions();
                ShowCurrentPage();

                // 初回バッチ範囲のOCR結果・注釈データをUIへ展開
                LoadBatchDataToUi(1, (int)numPageEnd.Value);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "ページを表示できませんでした。\n\n" + ex.Message,
                    "PDF表示エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void btnFirstPage_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null) return;
            if (appSettings.FirstLastNavScope == "batch")
            {
                int batchStartIdx = Math.Max(0, (int)numPageStart.Value - 1);
                if (currentPage != batchStartIdx)
                {
                    SwitchToPage(batchStartIdx);
                }
                else if (currentPage > 0)
                {
                    // すでにバッチ先頭の場合はファイル先頭へ
                    SwitchToPage(0);
                }
            }
            else
            {
                if (currentPage > 0)
                    SwitchToPage(0);
            }
        }

        private void btnPrevPage_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null) return;
            if (currentPage > 0)
                SwitchToPage(currentPage - 1);
        }

        private void btnNextPage_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null) return;
            if (currentPage < pdfDocument.PageCount - 1)
                SwitchToPage(currentPage + 1);
        }

        private void btnLastPage_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null) return;
            if (appSettings.FirstLastNavScope == "batch")
            {
                int batchEndIdx = Math.Min(pdfDocument.PageCount - 1, (int)numPageEnd.Value - 1);
                if (currentPage != batchEndIdx)
                {
                    SwitchToPage(batchEndIdx);
                }
                else if (currentPage < pdfDocument.PageCount - 1)
                {
                    // すでにバッチ末尾の場合はファイル末尾へ
                    SwitchToPage(pdfDocument.PageCount - 1);
                }
            }
            else
            {
                if (currentPage < pdfDocument.PageCount - 1)
                    SwitchToPage(pdfDocument.PageCount - 1);
            }
        }

        private void btnNextBatch20_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null) return;

            int totalPages = pdfDocument.PageCount;
            int batchSize = Math.Max(5, appSettings.BatchPageSize);
            int currentStart = (int)numPageStart.Value;
            int currentEnd = (int)numPageEnd.Value;

            var menu = new ContextMenuStrip();
            menu.Font = new Font("Yu Gothic UI", 9.5f, FontStyle.Regular);

            // 1. 各バッチ一覧項目
            int batchIdx = 1;
            for (int startP = 1; startP <= totalPages; startP += batchSize)
            {
                int endP = Math.Min(totalPages, startP + batchSize - 1);
                int bStart = startP;
                int bEnd = endP;
                int bNum = batchIdx++;

                bool isCurrentBatch = (currentStart == bStart && currentEnd == bEnd);
                string prefix = isCurrentBatch ? "✔ " : "   ";
                string title = $"{prefix}第{bNum}バッチ:  P.{bStart} 〜 P.{bEnd}  ({bEnd - bStart + 1}ページ)";

                var item = new ToolStripMenuItem(title, null, (s, ev) =>
                {
                    numPageStart.Value = bStart;
                    numPageEnd.Value = bEnd;
                    SwitchToPage(bStart - 1);
                    LoadBatchDataToUi(bStart, bEnd);
                    txtLog.AppendText($"【バッチ選択】第{bNum}バッチ (P.{bStart}〜P.{bEnd}) に対象範囲を切り替えました。" + Environment.NewLine);
                });

                if (isCurrentBatch)
                {
                    item.Font = new Font(menu.Font, FontStyle.Bold);
                    item.BackColor = Color.FromArgb(239, 246, 255);
                    item.ForeColor = Color.FromArgb(29, 78, 216);
                }

                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());

            // 2. 次のバッチへ進む（クイック送り）
            int nextStart = (int)numPageEnd.Value + 1;
            if (nextStart > totalPages) nextStart = 1;
            int nextEnd = Math.Min(totalPages, nextStart + batchSize - 1);
            var mnuNext = new ToolStripMenuItem($"⏩ 次のバッチへ進む (P.{nextStart}〜P.{nextEnd})", null, (s, ev) =>
            {
                numPageStart.Value = nextStart;
                numPageEnd.Value = nextEnd;
                SwitchToPage(nextStart - 1);
                LoadBatchDataToUi(nextStart, nextEnd);
                txtLog.AppendText($"【バッチ送り】次の範囲 (P.{nextStart}〜P.{nextEnd}) に進みました。" + Environment.NewLine);
            });
            menu.Items.Add(mnuNext);

            // 3. 全ページ一括
            var mnuAll = new ToolStripMenuItem($"🌐 全ページを一括選択 (P.1〜P.{totalPages})", null, (s, ev) =>
            {
                numPageStart.Value = 1;
                numPageEnd.Value = totalPages;
                SwitchToPage(0);
                LoadBatchDataToUi(1, totalPages);
                txtLog.AppendText($"【バッチ選択】全ページ (P.1〜P.{totalPages}) を一括選択しました。" + Environment.NewLine);
            });
            menu.Items.Add(mnuAll);

            // ボタンの直下にメニューを表示
            if (btnNextBatch20 != null && btnNextBatch20.IsHandleCreated)
            {
                menu.Show(btnNextBatch20, new Point(0, btnNextBatch20.Height));
            }
            else
            {
                menu.Show(Cursor.Position);
            }
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (pictureBox1.Image == null) return;

            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            for (int i = 0; i < regions.Count; i++)
            {
                OcrRegion region = regions[i];
                Color regionColor = GetRegionColor(region.Type);
                bool isRegionSelected = (i == lstRegions.SelectedIndex);
                using Pen regionPen = new Pen(regionColor, isRegionSelected ? 2.5f : 2f);

                Rectangle screenRect = ImageCoordinateHelper.ImageToScreen(
                    new Rectangle(region.X, region.Y, region.Width, region.Height),
                    pictureBox1);

                e.Graphics.DrawRectangle(regionPen, screenRect);

                // 表の罫線描画
                if (region.Type == "table")
                {
                    DrawTableRuleLines(e.Graphics, region, i, isRegionSelected);
                }

                // 領域選択ハンドル（四隅）
                if (isRegionSelected && !isReadingOrderMode)
                {
                    using Brush handleBrush = new SolidBrush(Color.White);
                    using Pen handlePen = new Pen(Color.Red, 1);
                    int h = ResizeHandleSize;

                    Point[] handles =
                    {
                        new Point(screenRect.Left, screenRect.Top),
                        new Point(screenRect.Right, screenRect.Top),
                        new Point(screenRect.Left, screenRect.Bottom),
                        new Point(screenRect.Right, screenRect.Bottom)
                    };

                    foreach (Point handle in handles)
                    {
                        Rectangle handleRect = new Rectangle(
                            handle.X - h / 2, handle.Y - h / 2, h, h);
                        e.Graphics.FillRectangle(handleBrush, handleRect);
                        e.Graphics.DrawRectangle(handlePen, handleRect);
                    }
                }

                // 領域の右上隅に読み順バッジ（①, ②, ③...）を描画
                int orderNum = i + 1;
                string badgeText = $"{orderNum}";
                Color badgeBgColor = isRegionSelected ? Color.FromArgb(234, 88, 12) : regionColor;

                if (isReadingOrderMode)
                {
                    if (assignedRegions.Contains(region))
                    {
                        int assignedIdx = reorderedRegions.IndexOf(region) + 1;
                        badgeText = $"{assignedIdx}";
                        badgeBgColor = Color.FromArgb(16, 185, 129); // Vibrant Emerald Green
                    }
                    else
                    {
                        badgeText = "?";
                        badgeBgColor = Color.FromArgb(100, 116, 139); // Slate Gray
                    }
                }

                int badgeDiameter = 24;
                int badgeX = screenRect.Right - badgeDiameter / 2 - 2;
                int badgeY = screenRect.Top - badgeDiameter / 2 + 2;
                Rectangle badgeRect = new Rectangle(badgeX, badgeY, badgeDiameter, badgeDiameter);

                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Brush bgBrush = new SolidBrush(badgeBgColor))
                {
                    e.Graphics.FillEllipse(bgBrush, badgeRect);
                }
                using (Pen borderPen = new Pen(Color.White, 1.8f))
                {
                    e.Graphics.DrawEllipse(borderPen, badgeRect);
                }

                using (Font badgeFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (Brush textBrush = new SolidBrush(Color.White))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    e.Graphics.DrawString(badgeText, badgeFont, textBrush, badgeRect, sf);
                }
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
            }

            // 罫線追加モード中のマウスプレビュー線描画
            DrawRuleLinePreview(e.Graphics);

            // 新規領域ドラッグ中のプレビュー枠描画
            if (isDrawingRegion)
            {
                using Pen previewPen = new Pen(Color.Blue, 2);
                e.Graphics.DrawRectangle(previewPen, regionPreviewRectangle);
            }

            // 読み順設定モード中の上部案内バナー描画
            if (isReadingOrderMode)
            {
                string bannerMsg = $"【読み順設定中】 領域をクリックした順に番号を割り当てます (現在: {reorderedRegions.Count + 1}番目を指定中 / 全{regions.Count}件) - [🔄ボタンまたは右クリックで確定]";
                using Font bannerFont = new Font("Yu Gothic UI", 10f, FontStyle.Bold);
                SizeF bannerSize = e.Graphics.MeasureString(bannerMsg, bannerFont);
                int bannerW = (int)bannerSize.Width + 24;
                int bannerH = 32;
                int bannerX = Math.Max(10, (pictureBox1.ClientSize.Width - bannerW) / 2);
                int bannerY = 10;
                Rectangle bannerRect = new Rectangle(bannerX, bannerY, bannerW, bannerH);

                using (Brush bannerBg = new SolidBrush(Color.FromArgb(235, 30, 41, 59)))
                {
                    e.Graphics.FillRectangle(bannerBg, bannerRect);
                }
                using (Pen bannerBorder = new Pen(Color.FromArgb(234, 88, 12), 2))
                {
                    e.Graphics.DrawRectangle(bannerBorder, bannerRect);
                }
                using (Brush bannerTextBrush = new SolidBrush(Color.FromArgb(254, 240, 138)))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    e.Graphics.DrawString(bannerMsg, bannerFont, bannerTextBrush, bannerRect, sf);
                }
            }
        }

        private Color GetRegionColor(string type)
        {
            return type switch
            {
                "body" => Color.Blue,
                "heading" => Color.Green,
                "footnote" => Color.Gray,
                "table" => Color.Orange,
                "image" => Color.DeepSkyBlue,
                "map" => Color.Goldenrod,
                "ignore" => Color.Red,
                _ => Color.Black
            };
        }

        private static readonly float[] zoomPresetLevels = { 0.5f, 0.75f, 0.85f, 1.0f };

        /// <summary>
        /// 文字列（"Fit", "全体", "50%", "75%", "85%", "100%" 等）から倍率浮動小数点を解析します。
        /// </summary>
        public static float ParseZoomFactor(string? ratio)
        {
            if (string.IsNullOrWhiteSpace(ratio)) return 0.75f;
            string s = ratio.Trim().ToLowerInvariant();
            if (s.Contains("fit") || s.Contains("全体")) return 0f;
            if (s.Contains("50")) return 0.5f;
            if (s.Contains("85")) return 0.85f;
            if (s.Contains("100")) return 1.0f;
            if (s.Contains("75")) return 0.75f;

            string numStr = s.TrimEnd('%', ' ');
            if (float.TryParse(numStr, out float pct) && pct > 0)
            {
                return pct / 100f;
            }
            return 0.75f;
        }

        public void SetZoom(float factor, bool updateCombo = true)
        {
            if (factor <= 0f)
            {
                currentZoomFactor = 0f;
            }
            else
            {
                currentZoomFactor = Math.Clamp(factor, 0.25f, 3.0f);
            }

            if (updateCombo && cmbZoom != null)
            {
                isUpdatingZoomCombo = true;
                try
                {
                    if (currentZoomFactor <= 0f)
                    {
                        cmbZoom.SelectedIndex = 0; // 全体表示 (Fit)
                    }
                    else
                    {
                        int pct = (int)Math.Round(currentZoomFactor * 100);
                        string match = $"{pct}%";
                        int idx = cmbZoom.FindStringExact(match);
                        if (idx >= 0)
                        {
                            cmbZoom.SelectedIndex = idx;
                        }
                        else
                        {
                            cmbZoom.Text = match;
                        }
                    }
                }
                finally
                {
                    isUpdatingZoomCombo = false;
                }
            }

            UpdateCanvasLayout();
        }

        public void ZoomIn()
        {
            if (pictureBox1.Image == null) return;

            if (currentZoomFactor <= 0f)
            {
                SetZoom(0.5f);
            }
            else
            {
                float nextLevel = zoomPresetLevels.FirstOrDefault(z => z > currentZoomFactor + 0.01f);
                if (nextLevel == 0f)
                {
                    SetZoom(1.0f);
                }
                else
                {
                    SetZoom(nextLevel);
                }
            }
        }

        public void ZoomOut()
        {
            if (pictureBox1.Image == null) return;

            if (currentZoomFactor <= 0f)
            {
                return;
            }
            else
            {
                float prevLevel = zoomPresetLevels.LastOrDefault(z => z < currentZoomFactor - 0.01f);
                if (prevLevel == 0f)
                {
                    SetZoom(0f); // 全体表示 (Fit) へ戻す
                }
                else
                {
                    SetZoom(prevLevel);
                }
            }
        }

        private void cmbZoom_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (isUpdatingZoomCombo) return;

            if (cmbZoom.SelectedIndex == 0) // 全体表示 (Fit)
            {
                SetZoom(0f, false);
            }
            else if (cmbZoom.SelectedItem is string text)
            {
                string numStr = text.TrimEnd('%', ' ');
                if (float.TryParse(numStr, out float pct))
                {
                    SetZoom(pct / 100f, false);
                }
            }
        }

        private void cmbZoom_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;

                string text = cmbZoom.Text.Trim();
                if (text.Contains("Fit", StringComparison.OrdinalIgnoreCase) || text.Contains("全体"))
                {
                    SetZoom(0f, true);
                    return;
                }

                string numStr = text.TrimEnd('%', ' ');
                if (float.TryParse(numStr, out float pct) && pct > 0)
                {
                    SetZoom(pct / 100f, true);
                }
            }
        }

        private void pictureBox1_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys.HasFlag(Keys.Control))
            {
                if (e is HandledMouseEventArgs handledE)
                {
                    handledE.Handled = true;
                }

                if (e.Delta > 0)
                {
                    ZoomIn();
                }
                else if (e.Delta < 0)
                {
                    ZoomOut();
                }
            }
        }

        private void UpdateCanvasLayout()
        {
            if (pnlCanvasContainer == null) return;

            if (pictureBox1.Image == null)
            {
                pictureBox1.Dock = DockStyle.Fill;
                pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
                pictureBox1.Invalidate();
                return;
            }

            if (currentZoomFactor <= 0f)
            {
                // 全体表示 (Fit)
                pnlCanvasContainer.AutoScroll = false;
                pictureBox1.Dock = DockStyle.Fill;
                pictureBox1.Location = Point.Empty;
                pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            }
            else
            {
                // 固定倍率表示 (50%〜300%+)
                pnlCanvasContainer.AutoScroll = true;
                pictureBox1.Dock = DockStyle.None;
                pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;

                int targetW = (int)Math.Round(pictureBox1.Image.Width * currentZoomFactor);
                int targetH = (int)Math.Round(pictureBox1.Image.Height * currentZoomFactor);

                int panelW = pnlCanvasContainer.ClientSize.Width;
                int panelH = pnlCanvasContainer.ClientSize.Height;

                int posX = targetW < panelW ? (panelW - targetW) / 2 : 0;
                int posY = targetH < panelH ? (panelH - targetH) / 2 : 0;

                pictureBox1.Location = new Point(posX, posY);
                pictureBox1.Size = new Size(targetW, targetH);
            }

            pictureBox1.Invalidate();
        }
    }
}
