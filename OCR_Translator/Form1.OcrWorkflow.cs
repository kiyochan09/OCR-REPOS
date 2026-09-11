using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using OCR_Translator.Forms;
using OCR_Translator.Models;
using OCR_Translator.Services;

namespace OCR_Translator
{
    public partial class Form1
    {
        private void SaveCurrentPageRegions()
        {
            if (regions.Count == 0 && !pageRegions.ContainsKey(currentPage))
                return;

            pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
            _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
        }

        private void LoadCurrentPageRegions()
        {
            regions.Clear();
            regions.AddRange(
                _layoutStorage.LoadPageRegions(
                    currentPage, pageRegions, autoPageRegions));
            RefreshRegionList();
        }

        private void btnSaveLayout_Click(object sender, EventArgs e)
        {
            SaveCurrentPageRegions();
            PageLayout layout = _layoutStorage.BuildPageLayout(pageRegions);
            string path = Path.Combine(Application.StartupPath, "page_layout.json");

            try
            {
                _layoutStorage.SaveToJsonFile(layout, path);
                MessageBox.Show(
                    $"ページ単位の設定を保存しました。\n\n保存ページ数: {pageRegions.Count}\nファイル: {path}",
                    "保存完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存に失敗しました。\n\n" + ex.Message,
                    "保存エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnAutoLayout_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null || string.IsNullOrWhiteSpace(currentPdfPath))
            {
                MessageBox.Show("先にPDFを開いてください。", "領域自動判定",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            string pythonExe = Path.Combine(projectDir, "venv", "Scripts", "python.exe");
            string autoRegionScript = Path.Combine(projectDir, "ndlocr_auto_region.py");

            if (!File.Exists(pythonExe))
            {
                MessageBox.Show($"Pythonが見つかりません。\n{pythonExe}", "領域自動判定",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!File.Exists(autoRegionScript))
            {
                MessageBox.Show($"自動領域判定スクリプトが見つかりません。\n{autoRegionScript}",
                    "領域自動判定", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string pdfName = Path.GetFileNameWithoutExtension(currentPdfPath);
            string outputRoot = Path.Combine(projectDir, "ocr_results", pdfName);
            Directory.CreateDirectory(outputRoot);

            int originalPage = currentPage;
            int successCount = 0;
            int failureCount = 0;
            int startPage = Math.Max(0, (int)numPageStart.Value - 1);
            int endPage = Math.Min(pdfDocument.PageCount - 1, (int)numPageEnd.Value - 1);
            if (startPage > endPage)
            {
                int tmp = startPage;
                startPage = endPage;
                endPage = tmp;
            }
            int targetPageCount = endPage - startPage + 1;

            SaveCurrentPageRegions();

            ProgressForm? progressForm = null;

            try
            {
                btnAutoLayout.Enabled = false;
                btnStartOcr.Enabled = false;
                btnOpenPdf.Enabled = false;
                btnFirstPage.Enabled = false;
                btnPrevPage.Enabled = false;
                btnNextPage.Enabled = false;
                btnLastPage.Enabled = false;
                btnNextBatch20.Enabled = false;
                numCurrentPage.Enabled = false;
                Cursor = Cursors.WaitCursor;

                progressForm = new ProgressForm(targetPageCount, "領域自動判定 - 進行状況", "レイアウト領域（本文・見出し・表・図）を自動抽出しています。");
                progressForm.StartPosition = FormStartPosition.CenterParent;
                progressForm.Show(this);
                progressForm.UpdateProgress(0, targetPageCount, "準備中...");

                txtLog.Clear();
                txtLog.AppendText($"========== 領域自動判定開始 (P.{startPage + 1} 〜 P.{endPage + 1} / 全{pdfDocument.PageCount}ページ) ==========" + Environment.NewLine);
                txtLog.AppendText($"PDF: {Path.GetFileName(currentPdfPath)}" + Environment.NewLine);
                txtLog.AppendText($"処理範囲: {startPage + 1} 〜 {endPage + 1} ページ ({targetPageCount} ページ)" + Environment.NewLine + Environment.NewLine);

                for (int pageIndex = startPage; pageIndex <= endPage; pageIndex++)
                {
                    int currentStep = pageIndex - startPage;
                    string pageMessage = $"ページ {pageIndex + 1} / {pdfDocument.PageCount} (範囲内: {currentStep + 1}/{targetPageCount}) を処理しています...";
                    txtLog.AppendText($"---------- {pageIndex + 1}/{pdfDocument.PageCount} ページ ----------" + Environment.NewLine);
                    txtLog.AppendText(pageMessage + Environment.NewLine);
                    txtLog.Refresh();
                    progressForm?.UpdateProgress(currentStep, targetPageCount,
                        pageMessage + "\r\nレイアウト領域を検出中...");

                    string pageDir = Path.Combine(outputRoot, $"page_{pageIndex + 1:0000}");
                    Directory.CreateDirectory(pageDir);

                    string imagePath = Path.Combine(pageDir, "page.png");
                    string resultJson = Path.Combine(pageDir, "auto_layout.json");

                    try
                    {
                        int dpi = appSettings.RenderDpi > 0 ? appSettings.RenderDpi : DefaultPdfRenderDpi;
                        using (Image rendered = pdfDocument.Render(pageIndex, dpi, dpi,
                            PdfRenderFlags.Annotations | PdfRenderFlags.ForPrinting | PdfRenderFlags.LcdText | PdfRenderFlags.CorrectFromDpi))
                            rendered.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);

                        OcrProcessor.ProcessResult result = await OcrProcessor.RunAutoRegionProcessAsync(
                            pythonExe, autoRegionScript, projectDir, imagePath, pageDir,
                            appSettings.TextOrientation, appSettings.DocumentType);

                        string log = "[STDOUT]\r\n" + result.Stdout + "\r\n[STDERR]\r\n" + result.Stderr;
                        File.WriteAllText(Path.Combine(pageDir, "ndlocr_run.log"), log, new UTF8Encoding(false));

                        if (result.ExitCode != 0)
                        {
                            failureCount++;
                            txtLog.AppendText($"失敗: 終了コード {result.ExitCode}" + Environment.NewLine);
                            progressForm?.UpdateProgress(currentStep + 1, targetPageCount,
                                $"ページ {pageIndex + 1} 失敗\r\n終了コード: {result.ExitCode}");
                            continue;
                        }

                        if (!File.Exists(resultJson))
                        {
                            failureCount++;
                            txtLog.AppendText("失敗: auto_layout.json が生成されませんでした。" + Environment.NewLine);
                            progressForm?.UpdateProgress(currentStep + 1, targetPageCount,
                                $"ページ {pageIndex + 1} 失敗\r\nauto_layout.json がありません。");
                            continue;
                        }

                        List<AutoLayoutRegion> detected = OcrJsonParser.LoadAutoLayoutJson(resultJson);
                        List<OcrRegion> converted = detected.Select(OcrProcessor.ConvertAutoLayoutRegion).ToList();
                        List<OcrRegion> divided = ApplyDeckDivision(converted, appSettings.DeckCount, appSettings.DocumentType, appSettings.TextOrientation);
                        autoPageRegions[pageIndex] = divided;
                        pageRegions[pageIndex] = _layoutStorage.CloneRegions(divided);

                        successCount++;
                        txtLog.AppendText($"成功: 自動領域 {divided.Count}件 検出" + (appSettings.DeckCount >= 2 ? $" ({appSettings.DeckCount}段組分割適用)" : "") + Environment.NewLine);
                        progressForm?.UpdateProgress(currentStep + 1, targetPageCount,
                            $"ページ {pageIndex + 1} 完了\r\n自動領域 {divided.Count}件を検出しました");
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        txtLog.AppendText("失敗: " + ex.Message + Environment.NewLine);
                        progressForm?.UpdateProgress(currentStep + 1, targetPageCount,
                            $"ページ {pageIndex + 1} 失敗\r\n{ex.Message}");
                    }
                }

                currentPage = startPage;
                SetZoom(0f, true); // 領域自動判定完了後は全体表示 (Fit)
                LoadCurrentPageRegions();
                ShowCurrentPage();

                txtLog.AppendText(Environment.NewLine);
                txtLog.AppendText($"========== 領域自動判定完了 (P.{startPage + 1} 〜 P.{endPage + 1}) ==========" + Environment.NewLine);
                txtLog.AppendText($"成功: {successCount}ページ" + Environment.NewLine);
                txtLog.AppendText($"失敗: {failureCount}ページ" + Environment.NewLine);
                txtLog.AppendText("左側プレビューに抽出した領域枠を表示しました。枠の位置や種類（本文・見出し・表・図）を確認・調整した上で、「OCR開始」ボタンを押してください。" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                txtLog.AppendText(Environment.NewLine +
                    "========== 領域自動判定例外 ==========" + Environment.NewLine + ex + Environment.NewLine);
            }
            finally
            {
                if (progressForm != null)
                {
                    progressForm.AllowClose = true;
                    progressForm.Close();
                    progressForm.Dispose();
                }

                RefreshFigureGalleryView();
                RefreshBatchList();

                Cursor = Cursors.Default;
                btnAutoLayout.Enabled = true;
                btnStartOcr.Enabled = true;
                btnOpenPdf.Enabled = true;
                btnFirstPage.Enabled = true;
                btnPrevPage.Enabled = true;
                btnNextPage.Enabled = true;
                btnLastPage.Enabled = true;
                btnNextBatch20.Enabled = true;
                numCurrentPage.Enabled = true;
                UpdatePageDisplayTitle();
            }
        }

        private async void btnStartOcr_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null || string.IsNullOrWhiteSpace(currentPdfPath))
            {
                MessageBox.Show("先にPDFを開いてください。", "OCR開始",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            string pythonExe = Path.Combine(projectDir, "venv", "Scripts", "python.exe");
            string autoRegionScript = Path.Combine(projectDir, "ndlocr_auto_region.py");

            if (!File.Exists(pythonExe))
            {
                MessageBox.Show($"Pythonが見つかりません。\n{pythonExe}", "OCR開始",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!File.Exists(autoRegionScript))
            {
                MessageBox.Show($"スクリプトが見つかりません。\n{autoRegionScript}", "OCR開始",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string pdfName = Path.GetFileNameWithoutExtension(currentPdfPath);
            int originalPage = currentPage;
            int successCount = 0;
            int failureCount = 0;
            int startPage = Math.Max(0, (int)numPageStart.Value - 1);
            int endPage = Math.Min(pdfDocument.PageCount - 1, (int)numPageEnd.Value - 1);
            if (startPage > endPage)
            {
                int tmp = startPage;
                startPage = endPage;
                endPage = tmp;
            }
            int targetPageCount = endPage - startPage + 1;

            SaveCurrentPageRegions();
            SaveCurrentPageData();
            txtLog.Clear();
            txtLog.AppendText($"========== OCR処理開始 (P.{startPage + 1} 〜 P.{endPage + 1} / 全{pdfDocument.PageCount}ページ) ==========" + Environment.NewLine);
            txtLog.AppendText($"PDF: {Path.GetFileName(currentPdfPath)}" + Environment.NewLine);
            txtLog.AppendText($"処理範囲: {startPage + 1} 〜 {endPage + 1} ページ ({targetPageCount} ページ)" + Environment.NewLine + Environment.NewLine);

            ProgressForm? progressForm = null;

            try
            {
                btnStartOcr.Enabled = false;
                btnAutoLayout.Enabled = false;
                btnOpenPdf.Enabled = false;
                btnFirstPage.Enabled = false;
                btnPrevPage.Enabled = false;
                btnNextPage.Enabled = false;
                btnLastPage.Enabled = false;
                btnNextBatch20.Enabled = false;
                numCurrentPage.Enabled = false;
                Cursor = Cursors.WaitCursor;

                progressForm = new ProgressForm(targetPageCount, "OCR文字認識 - 進行状況", "全ページの文字認識（OCR）を実行しています。");
                progressForm.StartPosition = FormStartPosition.CenterParent;
                progressForm.Show(this);
                progressForm.UpdateProgress(0, targetPageCount, "準備中...");

                for (int pageIndex = startPage; pageIndex <= endPage; pageIndex++)
                {
                    int currentStep = pageIndex - startPage;
                    string pageMessage = $"ページ {pageIndex + 1} / {pdfDocument.PageCount} (範囲内: {currentStep + 1}/{targetPageCount}) を処理しています...";
                    currentPage = pageIndex;
                    LoadCurrentPageRegions();
                    ShowCurrentPage();
                    await Task.Delay(30);

                    bool useUserRegions = regions.Count > 0;

                    txtLog.AppendText($"---------- ページ {pageIndex + 1}/{pdfDocument.PageCount} " + (useUserRegions ? $"({regions.Count}個の手動領域を使用)" : "(自動領域を使用)") + " ----------" + Environment.NewLine);
                    txtLog.AppendText(pageMessage + Environment.NewLine);
                    txtLog.Refresh();
                    progressForm?.UpdateProgress(currentStep, targetPageCount,
                        pageMessage + "\r\nNDLOCR-Lite文字認識を実行中...");

                    string pageDir = Path.Combine(projectDir, "ocr_results", pdfName, $"page_{pageIndex + 1:0000}");
                    Directory.CreateDirectory(pageDir);
                    string imagePath = Path.Combine(pageDir, "page.png");

                    int dpi = appSettings.RenderDpi > 0 ? appSettings.RenderDpi : DefaultPdfRenderDpi;
                    using (Image rendered = pdfDocument.Render(pageIndex, dpi, dpi,
                        PdfRenderFlags.Annotations | PdfRenderFlags.ForPrinting | PdfRenderFlags.LcdText | PdfRenderFlags.CorrectFromDpi))
                        rendered.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);

                    var psi = new ProcessStartInfo
                    {
                        FileName = pythonExe,
                        WorkingDirectory = projectDir,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };
                    psi.Environment["PYTHONUTF8"] = "1";
                    psi.Environment["PYTHONIOENCODING"] = "utf-8";
                    psi.ArgumentList.Add(autoRegionScript);
                    psi.ArgumentList.Add(imagePath);
                    psi.ArgumentList.Add(pageDir);

                    if (!string.IsNullOrEmpty(appSettings.TextOrientation))
                    {
                        psi.ArgumentList.Add("--orientation");
                        psi.ArgumentList.Add(appSettings.TextOrientation);
                    }

                    if (!string.IsNullOrEmpty(appSettings.DocumentType))
                    {
                        psi.ArgumentList.Add("--doc-type");
                        psi.ArgumentList.Add(appSettings.DocumentType);
                    }

                    using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                    var stdout = new StringBuilder();
                    var stderr = new StringBuilder();
                    var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

                    process.OutputDataReceived += (_, ev) =>
                    {
                        if (ev.Data == null) return;
                        stdout.AppendLine(ev.Data);
                    };

                    process.ErrorDataReceived += (_, ev) =>
                    {
                        if (ev.Data == null) return;
                        stderr.AppendLine(ev.Data);
                    };

                    process.Exited += (_, _) => completion.TrySetResult(process.ExitCode);
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    int exitCode = await completion.Task;

                    string log = "[STDOUT]\r\n" + stdout + "\r\n[STDERR]\r\n" + stderr;
                    File.WriteAllText(Path.Combine(pageDir, "ndlocr_run.log"), log, new UTF8Encoding(false));

                    string resultJson = Path.Combine(pageDir, "auto_layout.json");
                    string textJson = ResolveOcrTextJsonPath(pageDir);

                    if (exitCode != 0)
                    {
                        failureCount++;
                        txtLog.AppendText($"失敗: 終了コード {exitCode}" + Environment.NewLine);
                        progressForm?.UpdateProgress(currentStep + 1, targetPageCount,
                            $"ページ {pageIndex + 1} 失敗\r\n終了コード: {exitCode}");
                        continue;
                    }

                    if (string.IsNullOrEmpty(textJson) || !File.Exists(textJson))
                    {
                        failureCount++;
                        txtLog.AppendText("失敗: OCR結果JSONが生成されませんでした。" + Environment.NewLine);
                        progressForm?.UpdateProgress(currentStep + 1, targetPageCount,
                            $"ページ {pageIndex + 1} 失敗\r\nOCR結果JSONがありません。");
                        continue;
                    }

                    List<AutoLayoutRegion> autoRegions = File.Exists(resultJson)
                        ? OcrJsonParser.LoadAutoLayoutJson(resultJson)
                        : new List<AutoLayoutRegion>();

                    if (!useUserRegions && autoRegions.Count > 0)
                    {
                        List<OcrRegion> converted = autoRegions.Select(OcrProcessor.ConvertAutoLayoutRegion).ToList();
                        List<OcrRegion> divided = ApplyDeckDivision(converted, appSettings.DeckCount, appSettings.DocumentType, appSettings.TextOrientation);
                        autoPageRegions[pageIndex] = divided;
                        pageRegions[pageIndex] = _layoutStorage.CloneRegions(divided);
                        regions.Clear();
                        regions.AddRange(divided);
                        RefreshRegionList();
                    }

                    ProcessPageOcrResult(pageIndex, pageDir, imagePath, textJson, resultJson, useUserRegions, regions, autoRegions);

                    successCount++;
                    txtLog.AppendText("完了 (page_data.json 保存・右画面反映済)" + Environment.NewLine + Environment.NewLine);
                    progressForm?.UpdateProgress(currentStep + 1, targetPageCount,
                        $"ページ {pageIndex + 1} 完了\r\n文字認識結果を展開しました。");
                }

                txtLog.AppendText("========== OCRバッチ処理完了 ==========" + Environment.NewLine);
                txtLog.AppendText($"成功: {successCount}ページ" + Environment.NewLine);
                txtLog.AppendText($"失敗: {failureCount}ページ" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                txtLog.AppendText(Environment.NewLine + "========== OCR例外 ==========" + Environment.NewLine);
                txtLog.AppendText(ex + Environment.NewLine);
            }
            finally
            {
                if (progressForm != null)
                {
                    progressForm.AllowClose = true;
                    progressForm.Close();
                    progressForm.Dispose();
                }

                if (!string.IsNullOrEmpty(currentPdfPath) && pdfDocument != null)
                {
                    var normalizedPages = OcrPageDataService.NormalizeAndRenumberProjectFootnotes(
                        projectDir, pdfName, pdfDocument.PageCount, appSettings);
                    ocrPageDataList.Clear();
                    ocrPageDataList.AddRange(normalizedPages);
                }

                RefreshFigureGalleryView();
                RefreshBatchList();

                currentPage = startPage;
                float postZoom = ParseZoomFactor(appSettings.PostOcrZoomRatio);
                SetZoom(postZoom, true); // OCR完了後はオプション設定倍率 (初期値: 75%) を適用
                LoadCurrentPageRegions();
                ShowCurrentPage();

                // 対象バッチの全ページを展開（OCR実行ページは最新結果で安全に上書き反映）
                int batchSize = Math.Max(5, appSettings.BatchPageSize);
                int batchStart = (startPage / batchSize) * batchSize + 1;
                int batchEnd = Math.Min(pdfDocument.PageCount, batchStart + batchSize - 1);
                LoadBatchDataToUi(batchStart, batchEnd);

                if (tabOcrResult != null && tabOcrText != null)
                {
                    tabOcrResult.SelectedTab = tabOcrText;
                }
                ScrollOcrResultToPage(startPage + 1);

                Cursor = Cursors.Default;
                btnStartOcr.Enabled = true;
                btnAutoLayout.Enabled = true;
                btnOpenPdf.Enabled = true;
                btnFirstPage.Enabled = true;
                btnPrevPage.Enabled = true;
                btnNextPage.Enabled = true;
                btnLastPage.Enabled = true;
                btnNextBatch20.Enabled = true;
                numCurrentPage.Enabled = true;
                UpdatePageDisplayTitle();
            }
        }

        /// <summary>
        /// pageDirからNDLOCRのテキスト認識JSON（auto_regions.json または page.json等）を検索して返します。
        /// </summary>
        private static string ResolveOcrTextJsonPath(string pageDir)
        {
            string autoRegionsJson = Path.Combine(pageDir, "auto_regions.json");
            if (File.Exists(autoRegionsJson)) return autoRegionsJson;

            string pageJson = Path.Combine(pageDir, "page.json");
            if (File.Exists(pageJson)) return pageJson;

            var jsonCandidates = Directory.GetFiles(pageDir, "*.json")
                .Where(f => !f.EndsWith("auto_layout.json", StringComparison.OrdinalIgnoreCase) &&
                            !f.EndsWith("page_data.json", StringComparison.OrdinalIgnoreCase) &&
                            !f.EndsWith("detected_line_segments.json", StringComparison.OrdinalIgnoreCase) &&
                            !f.EndsWith("bordered_regions.json", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return jsonCandidates.Count > 0 ? jsonCandidates[0] : "";
        }

        /// <summary>
        /// 単一ページのOCR結果（テキスト・注釈・見出し・表・図）を解析し、UIタブおよびディスクへ反映します。
        /// </summary>
        private void ProcessPageOcrResult(
            int pageIndex,
            string pageDir,
            string imagePath,
            string pageJson,
            string resultJson,
            bool useUserRegions,
            List<OcrRegion> curUserRegions,
            List<AutoLayoutRegion> curAutoRegions)
        {
            if (!File.Exists(pageJson)) return;

            List<OcrDisplayItem> ocrItems = OcrJsonParser.LoadNdlocrPageJson(pageJson);
            List<AutoLayoutRegion> autoRegions = curAutoRegions ?? (File.Exists(resultJson) ? OcrJsonParser.LoadAutoLayoutJson(resultJson) : new List<AutoLayoutRegion>());

            txtLog.AppendText($"OCR項目数: {ocrItems.Count}" + Environment.NewLine);

            // 図（画像切り出し＆500KB以下自動圧縮・テキストOCRは除外）
            var imgRegions = useUserRegions
                ? curUserRegions.Where(r => OcrProcessor.NormalizeRegionType(r.Type) == "image" || OcrProcessor.NormalizeRegionType(r.Name) == "image").ToList()
                : autoRegions.Where(r => OcrProcessor.NormalizeRegionType(r.Type) == "image" || OcrProcessor.NormalizeRegionType(r.Name) == "image")
                             .Select(r => OcrProcessor.ConvertAutoLayoutRegion(r)).ToList();

            if (imgRegions.Count > 0 && File.Exists(imagePath))
            {
                try
                {
                    using Bitmap pageBmp = new Bitmap(imagePath);
                    int figNum = 1;
                    foreach (var imgReg in imgRegions)
                    {
                        var figItem = FigureExtractor.CropAndCompressFigure(pageBmp, imgReg, pageIndex + 1, FigureExtractor.DefaultMaxBytes);
                        if (figItem != null)
                        {
                            figItem.Name = string.IsNullOrWhiteSpace(imgReg.Name) || imgReg.Name == "本文" ? $"図{figNum++}" : imgReg.Name;
                            extractedFigures.RemoveAll(f => f.PageNumber == pageIndex + 1 && f.Name == figItem.Name);
                            extractedFigures.Add(figItem);

                            string figSavePath = Path.Combine(pageDir, $"figure_{figNum - 1:00}.{(figItem.MimeType == "image/png" ? "png" : "jpg")}");
                            File.WriteAllBytes(figSavePath, figItem.ImageBytes);
                            txtLog.AppendText($"【図切り出し】[P{pageIndex + 1}] {figItem.Name} ({figItem.Bounds.Width}×{figItem.Bounds.Height}px, {figItem.FileSizeKb:0.#}KB <= 500KB)" + Environment.NewLine);
                        }
                    }
                }
                catch (Exception ex)
                {
                    txtLog.AppendText($"【図切り出しエラー】[P{pageIndex + 1}] {ex.Message}" + Environment.NewLine);
                }
            }

            // タイプ別に分類（OcrProcessorのNormalizeRegionTypeを一貫して使用）
            Dictionary<string, List<OcrDisplayItem>> itemsByType = new();
            foreach (OcrDisplayItem item in ocrItems)
            {
                string type = useUserRegions
                    ? OcrProcessor.FindUserRegionType(item, curUserRegions)
                    : OcrProcessor.FindAutoLayoutRegionType(item, autoRegions);

                // 図領域内のテキストはOCR結果に含めない
                if (type == "image")
                    continue;

                // 手動領域設定時：領域外のテキストはOCR対象外として完全に除外（設定領域のみをOCR）
                if (useUserRegions && curUserRegions.Count > 0)
                {
                    if (string.IsNullOrEmpty(type) || type == "unclassified")
                    {
                        continue;
                    }
                }
                else
                {
                    // 自動判定時：未分類テキストは本文へフォールバックして文字欠落を防止
                    if (string.IsNullOrEmpty(type) || type == "unclassified")
                    {
                        type = "body";
                    }
                }

                if (!itemsByType.ContainsKey(type))
                    itemsByType[type] = new List<OcrDisplayItem>();
                itemsByType[type].Add(item);
            }

            // ページ出力用データ構造の作成
            var pageData = new OcrPageData { PageNumber = pageIndex + 1 };

            // 本文（全ページ・本文領域のみを段落ごとに改行して表示）
            string pageBodyText = "";
            if (itemsByType.TryGetValue("body", out List<OcrDisplayItem>? bodyList) && bodyList.Count > 0)
            {
                var userBodyRegions = (useUserRegions && curUserRegions != null && curUserRegions.Count > 0)
                    ? curUserRegions.Where(r => OcrProcessor.NormalizeRegionType(r.Type) == "body" || OcrProcessor.NormalizeRegionType(r.Name) == "body").ToList()
                    : (curUserRegions != null && curUserRegions.Count > 0
                        ? curUserRegions.Where(r => OcrProcessor.NormalizeRegionType(r.Type) == "body" || OcrProcessor.NormalizeRegionType(r.Name) == "body").ToList()
                        : (autoRegions != null && autoRegions.Count > 0
                            ? autoRegions.Where(r => (OcrProcessor.NormalizeRegionType(r.Type) == "body" || OcrProcessor.NormalizeRegionType(r.Name) == "body") && (r.Orientation != "vertical" || appSettings.TextOrientation == "vertical"))
                                         .OrderBy(r => appSettings.TextOrientation == "vertical" ? -(int)(r.X / 400) : (int)(r.X / 500))
                                         .ThenBy(r => r.Y)
                                         .Select(OcrProcessor.ConvertAutoLayoutRegion)
                                         .ToList()
                            : null));

                pageBodyText = OcrSorter.FormatBodyParagraphs(
                    bodyList,
                    appSettings.TextOrientation,
                    appSettings.DocumentType,
                    appSettings.DeckCount,
                    userBodyRegions,
                    appSettings.LineCharCount,
                    appSettings.AutoDetectSubheadings);

                foreach (var p in pageBodyText.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string cleanP = p.Trim();
                    pageData.BodyParagraphs.Add(cleanP);

                    // 本文内の小見出し（第2階層・子見出し）を検出して登録
                    foreach (var line in cleanP.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                    {
                        string trimmedLine = line.Trim();
                        if (trimmedLine.Length <= 80 && !trimmedLine.Contains("。") && !Regex.IsMatch(trimmedLine, @"^【(?:注釈文|注)\d+】"))
                        {
                            string cleanH = DocxExporter.RemovePagePrefix(trimmedLine);
                            if (string.IsNullOrWhiteSpace(cleanH)) continue;

                            bool isSection = Regex.IsMatch(cleanH, @"^第[0-9０-９一二三四五六七八九十百千万]+節(?:[\s　・:：].*)?$");
                            bool isSub = appSettings.AutoDetectSubheadings && OcrSorter.IsSubheadingText(trimmedLine);

                            if (isSection || isSub)
                            {
                                if (!pageData.Subheadings.Contains(cleanH, StringComparer.OrdinalIgnoreCase))
                                {
                                    pageData.Subheadings.Add(cleanH);
                                }
                            }
                        }
                    }
                }
            }

            // 見出し領域（明示的なheading領域がある場合: 第1階層の親見出しとして登録）
            if (itemsByType.TryGetValue("heading", out List<OcrDisplayItem>? headingList) && headingList.Count > 0)
            {
                foreach (OcrDisplayItem item in headingList)
                {
                    string cleanH = DocxExporter.RemovePagePrefix(item.Text.Trim());
                    if (!string.IsNullOrWhiteSpace(cleanH))
                    {
                        bool isSection = Regex.IsMatch(cleanH, @"^第[0-9０-９一二三四五六七八九十百千万]+[節項条回]") ||
                                         Regex.IsMatch(cleanH, @"^(?:Section|Sec\.)\s+[0-9IVXLCDMivxlcdm]+", RegexOptions.IgnoreCase);
                        if (isSection)
                        {
                            if (!pageData.Subheadings.Contains(cleanH, StringComparer.OrdinalIgnoreCase))
                                pageData.Subheadings.Add(cleanH);
                        }
                        else
                        {
                            if (!pageData.Headings.Contains(cleanH, StringComparer.OrdinalIgnoreCase))
                                pageData.Headings.Add(cleanH);
                        }
                    }
                }
            }

            // 本文タブへの展開（見出し・小見出しを青色太字で強調表示）
            if (!string.IsNullOrEmpty(pageBodyText) && ocrResultTextBoxes.TryGetValue("body", out var bBox))
            {
                if (bBox.TextLength > 0)
                {
                    bBox.AppendText(Environment.NewLine + Environment.NewLine);
                }
                var allHeadingsForHighlight = (pageData.Headings ?? new List<string>()).Concat(pageData.Subheadings ?? new List<string>()).ToList();
                AppendColoredBodyToTextBox(bBox, $"--- ページ {pageIndex + 1} ---" + Environment.NewLine + pageBodyText, allHeadingsForHighlight, appSettings.AutoDetectSubheadings);
            }

            // 見出しタブへの展開（第1階層・親見出しはインデントなし、第2階層・子見出しは半角4文字分インデント）
            if (ocrResultTextBoxes.TryGetValue("heading", out var hBox) && hBox != null)
            {
                if ((pageData.Headings != null && pageData.Headings.Count > 0) || (pageData.Subheadings != null && pageData.Subheadings.Count > 0))
                {
                    int n = 1;
                    if (pageData.Headings != null)
                    {
                        foreach (var h in pageData.Headings)
                        {
                            hBox.AppendText($"[P{pageIndex + 1}-{n++:00}] {h}" + Environment.NewLine);
                        }
                    }
                    if (pageData.Subheadings != null)
                    {
                        foreach (var sh in pageData.Subheadings)
                        {
                            hBox.AppendText($"[P{pageIndex + 1}-{n++:00}]     {sh}" + Environment.NewLine);
                        }
                    }
                }
            }

            // 注釈文（複数行を段落単位に結合し、【注釈文N】タグで登録）
            if (itemsByType.TryGetValue("footnote", out List<OcrDisplayItem>? footnoteList) && footnoteList.Count > 0)
            {
                var userFootnoteRegions = useUserRegions
                    ? curUserRegions.Where(r => OcrProcessor.NormalizeRegionType(r.Type) == "footnote" || OcrProcessor.NormalizeRegionType(r.Name) == "footnote").ToList()
                    : null;

                var sortedFootnotes = OcrSorter.SortBodyReadingOrder(
                    footnoteList,
                    appSettings.TextOrientation,
                    appSettings.DocumentType,
                    appSettings.DeckCount,
                    userFootnoteRegions);

                // 前ページからの注釈番号の継続性を考慮
                int fallbackStartNum = 1;
                if (pageIndex > 0 && ocrPageDataList.Count > 0)
                {
                    var prevPages = ocrPageDataList.Where(p => p.PageNumber <= pageIndex && p.Footnotes != null && p.Footnotes.Count > 0).OrderBy(p => p.PageNumber).ToList();
                    if (prevPages.Count > 0 && prevPages[^1].Footnotes.Count > 0)
                    {
                        var lastMatch = System.Text.RegularExpressions.Regex.Match(prevPages[^1].Footnotes[^1], @"^【(?:注釈文|注)(\d+)】");
                        if (lastMatch.Success && int.TryParse(lastMatch.Groups[1].Value, out int ln))
                        {
                            fallbackStartNum = ln;
                        }
                    }
                }

                var mergedFootnotes = OcrPageDataService.MergeFootnoteLines(
                    sortedFootnotes.Select(i => i.Text),
                    fallbackStartNum);

                int n = 1;
                foreach (var fnItem in mergedFootnotes)
                {
                    pageData.Footnotes.Add(fnItem);

                    var match = System.Text.RegularExpressions.Regex.Match(fnItem, @"^(【(?:注釈文|注)\d+】)\s*(.*)$");
                    string tag = match.Success ? match.Groups[1].Value : $"【注釈文{n}】";
                    string body = match.Success ? match.Groups[2].Value : fnItem;

                    if (ocrResultTextBoxes.TryGetValue("footnote", out var fBox))
                    {
                        AppendColoredFootnoteToTextBox(fBox, $"[P{pageIndex + 1}-{n:00}]", tag, body);
                    }
                    n++;
                }
            }

            // 未分類（手動領域設定時で枠外のもの）
            if (itemsByType.TryGetValue("unclassified", out List<OcrDisplayItem>? unclassifiedList) && unclassifiedList.Count > 0)
            {
                int n = 1;
                foreach (OcrDisplayItem item in unclassifiedList)
                {
                    if (ocrResultTextBoxes.TryGetValue("unclassified", out var uBox))
                    {
                        uBox.AppendText($"[P{pageIndex + 1}-{n++:00}] {item.Text}" + Environment.NewLine);
                    }
                }
            }

            // 表（ユーザーの罫線補正を反映した行列2D構造としてDataGridViewに追記）
            if (itemsByType.TryGetValue("table", out List<OcrDisplayItem>? tableList) && tableList.Count > 0)
            {
                var structuredTables = TableGridExtractor.ExtractStructuredTables(
                    pageIndex + 1, ocrItems, curUserRegions, autoRegions, appSettings.DocumentType);

                pageData.Tables.AddRange(structuredTables);

                if (dgvOcrTable != null && structuredTables.Count > 0)
                {
                    int maxCols = structuredTables.Max(t => t.ColumnCount);
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

                    foreach (var sTable in structuredTables)
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
            }

            // 図
            pageData.Figures = extractedFigures.Where(f => f.PageNumber == pageIndex + 1).ToList();

            // メモリ内リストを更新し、ディスクへ page_data.json として永続保存
            ocrPageDataList.RemoveAll(p => p.PageNumber == pageIndex + 1);
            ocrPageDataList.Add(pageData);
            OcrPageDataService.SavePageData(pageDir, pageData);

            // 読み順ファイル保存（段落ごとに改行）
            if (itemsByType.TryGetValue("body", out List<OcrDisplayItem>? bodyForSave) && bodyForSave.Count > 0)
            {
                string bodyReadingOrderText = OcrSorter.FormatBodyParagraphs(
                    bodyForSave,
                    appSettings.TextOrientation,
                    appSettings.DocumentType,
                    appSettings.DeckCount,
                    null,
                    appSettings.LineCharCount,
                    appSettings.AutoDetectSubheadings);
                string bodyReadingOrderPath = Path.Combine(pageDir, "body_reading_order.txt");
                File.WriteAllText(bodyReadingOrderPath, bodyReadingOrderText, new UTF8Encoding(false));
            }
        }

        private async void btnTestCrop_Click(object sender, EventArgs e)
        {
            int index = lstRegions.SelectedIndex;
            if (index < 0 || index >= regions.Count)
            {
                MessageBox.Show("先に領域を選択してください。", "領域再OCR",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await RunSingleRegionOcrAsync(regions[index]);
        }

        /// <summary>
        /// 選択された特定領域のみをOCRし、既存の編集済みテキストを保護した上で
        /// プレビュー・コピー・安全追記・置換を行うワークフローを実行します。
        /// </summary>
        public async Task RunSingleRegionOcrAsync(OcrRegion? region)
        {
            if (region == null)
            {
                MessageBox.Show("対象の領域が選択されていません。", "領域再OCR",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (pictureBox1.Image == null)
            {
                MessageBox.Show("ページ画像がありません。", "領域再OCR",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string normType = OcrProcessor.NormalizeRegionType(region.Type);

            // 図（image）の場合は切り出しと最適化を行ってギャラリーを更新
            if (normType == "image")
            {
                ExtractCurrentPageFigures();
                MessageBox.Show($"図「{region.Name}」を切り出し、図ギャラリーへ登録しました。",
                    "図切り出し完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Bitmap croppedBmp;
            try
            {
                using Bitmap source = new Bitmap(pictureBox1.Image);
                croppedBmp = RegionImageExtractor.Crop(source, region);
            }
            catch (Exception ex)
            {
                MessageBox.Show("領域画像の切り出しに失敗しました。\n\n" + ex.Message,
                    "領域再OCRエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            string pdfName = string.IsNullOrEmpty(currentPdfPath) ? "" : Path.GetFileNameWithoutExtension(currentPdfPath);
            string pageDir = Path.Combine(projectDir, "ocr_results", pdfName, $"page_{currentPage + 1:0000}");
            string textJson = ResolveOcrTextJsonPath(pageDir);

            string recognizedText = "";
            Cursor = Cursors.WaitCursor;

            try
            {
                // 1. 既存の生OCRデータ（page.json）から、この領域内に含まれるテキスト行を検索
                List<OcrDisplayItem> inRegionItems = new();
                List<OcrDisplayItem> allItems = new();
                if (File.Exists(textJson))
                {
                    allItems = OcrJsonParser.LoadNdlocrPageJson(textJson);
                    var regionRect = new Rectangle(region.X, region.Y, region.Width, region.Height);
                    inRegionItems = allItems.Where(item =>
                    {
                        double cx = item.X + item.Width / 2.0;
                        double cy = item.Y + item.Height / 2.0;
                        return regionRect.Contains((int)cx, (int)cy);
                    }).ToList();
                }

                if (inRegionItems.Count > 0)
                {
                    recognizedText = OcrSorter.FormatSingleTierParagraphs(
                        inRegionItems,
                        isVertical: (appSettings.TextOrientation == "vertical"),
                        isWestern: (appSettings.DocumentType == "western"),
                        autoDetectSubheadings: appSettings.AutoDetectSubheadings);
                }
                else
                {
                    // 2. 既存データ内に該当文字行がない場合はPython OCRエンジンを実行
                    string pythonExe = Path.Combine(projectDir, "venv", "Scripts", "python.exe");
                    string autoRegionScript = Path.Combine(projectDir, "ndlocr_auto_region.py");
                    string tempDir = Path.Combine(pageDir, "temp_single_ocr");
                    Directory.CreateDirectory(tempDir);
                    string cropPath = Path.Combine(tempDir, "crop.png");
                    croppedBmp.Save(cropPath, System.Drawing.Imaging.ImageFormat.Png);

                    if (File.Exists(pythonExe) && File.Exists(autoRegionScript))
                    {
                        txtLog.AppendText($"【領域再OCR】P.{currentPage + 1} 領域「{region.Name}」をPython OCRで認識中..." + Environment.NewLine);
                        var psi = new ProcessStartInfo
                        {
                            FileName = pythonExe,
                            WorkingDirectory = projectDir,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            StandardOutputEncoding = Encoding.UTF8,
                            StandardErrorEncoding = Encoding.UTF8
                        };
                        psi.Environment["PYTHONUTF8"] = "1";
                        psi.Environment["PYTHONIOENCODING"] = "utf-8";
                        psi.ArgumentList.Add(autoRegionScript);
                        psi.ArgumentList.Add(cropPath);
                        psi.ArgumentList.Add(tempDir);
                        if (!string.IsNullOrEmpty(appSettings.TextOrientation))
                        {
                            psi.ArgumentList.Add("--orientation");
                            psi.ArgumentList.Add(appSettings.TextOrientation);
                        }
                        if (!string.IsNullOrEmpty(appSettings.DocumentType))
                        {
                            psi.ArgumentList.Add("--doc-type");
                            psi.ArgumentList.Add(appSettings.DocumentType);
                        }

                        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                        process.Exited += (_, _) => completion.TrySetResult(process.ExitCode);
                        process.Start();
                        int exitCode = await completion.Task;

                        string cropTextJson = ResolveOcrTextJsonPath(tempDir);
                        if (exitCode == 0 && File.Exists(cropTextJson))
                        {
                            var cropItems = OcrJsonParser.LoadNdlocrPageJson(cropTextJson);
                            recognizedText = OcrSorter.FormatSingleTierParagraphs(
                                cropItems,
                                isVertical: (appSettings.TextOrientation == "vertical"),
                                isWestern: (appSettings.DocumentType == "western"),
                                autoDetectSubheadings: appSettings.AutoDetectSubheadings);

                            foreach (var ci in cropItems)
                            {
                                ci.X += region.X;
                                ci.Y += region.Y;
                            }
                            inRegionItems = cropItems;
                        }
                    }
                }

                // 表領域（table）の場合はダイアログを介さず、新構造で直接「表」タブへ強制置換反映
                if (normType == "table")
                {
                    region.EnsureRuleLines();
                    string resultJson = Path.Combine(pageDir, "auto_layout.json");
                    if (!File.Exists(resultJson))
                        resultJson = Path.Combine(pageDir, "result.json");
                    List<AutoLayoutRegion> curAuto = File.Exists(resultJson)
                        ? OcrJsonParser.LoadAutoLayoutJson(resultJson)
                        : new List<AutoLayoutRegion>();

                    // 1ページ内に複数表が存在する場合、何番目の表領域であるかを特定
                    var allTableRegions = regions.Where(r => OcrProcessor.NormalizeRegionType(r.Type) == "table").ToList();
                    int tableIndex = allTableRegions.IndexOf(region);
                    int startTableNumber = (tableIndex >= 0) ? tableIndex + 1 : 1;

                    var itemsForTable = allItems.Count > 0 ? allItems : inRegionItems;
                    var tables = TableGridExtractor.ExtractStructuredTables(
                        currentPage + 1,
                        itemsForTable,
                        new List<OcrRegion> { region },
                        curAuto,
                        appSettings.DocumentType,
                        startTableNumber: startTableNumber);

                    if (tables.Count > 0)
                    {
                        ReplaceTableInCurrentBatch(region, tables[0], tableIndex);
                        MessageBox.Show(
                            $"P.{currentPage + 1} の表「{tables[0].TableName}」を新構造({tables[0].ColumnCount}列×{tables[0].RowCount}行)で「表」タブへ直接置換しました。\n表タブ上でそのままセルの結合や直接編集を行えます。",
                            "表再OCR完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("表領域から表構造を抽出できませんでした。",
                            "表再OCR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("領域再OCRの実行中にエラーが発生しました。\n\n" + ex.Message,
                    "領域再OCRエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            // 本文・見出し・注釈文の場合はプレビュー確認ダイアログを表示して反映
            using var dlg = new RegionOcrResultForm(
                croppedBmp,
                region.Name,
                region.Type,
                currentPage + 1,
                recognizedText);

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                if (dlg.SelectedAction == RegionOcrAction.AppendToTab)
                {
                    AppendRegionTextToTargetTab(region.Type, dlg.ResultText);
                }
                else if (dlg.SelectedAction == RegionOcrAction.ReplaceSelection)
                {
                    ReplaceSelectedTextInTargetTab(region.Type, dlg.ResultText);
                }
            }
        }

        private void ReplaceTableInCurrentBatch(OcrRegion region, StructuredTable newTable, int tableIndex)
        {
            int targetPageNum = currentPage + 1;
            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            string pdfName = string.IsNullOrEmpty(currentPdfPath) ? "" : Path.GetFileNameWithoutExtension(currentPdfPath);
            string pageDir = Path.Combine(projectDir, "ocr_results", pdfName, $"page_{targetPageNum:0000}");

            var pageData = ocrPageDataList.FirstOrDefault(p => p.PageNumber == targetPageNum);
            if (pageData == null)
            {
                pageData = OcrPageDataService.LoadPageData(pageDir) ?? new OcrPageData { PageNumber = targetPageNum };
                ocrPageDataList.Add(pageData);
            }

            pageData.Tables ??= new List<StructuredTable>();

            // 1ページに複数表がある場合の安全な特定ロジック:
            // 1. 表名（「表1」「表2」等）による完全一致
            int existingIdx = pageData.Tables.FindIndex(t =>
                string.Equals(t.TableName, newTable.TableName, StringComparison.OrdinalIgnoreCase));

            // 2. ユーザー固有の領域名による一致
            if (existingIdx < 0 && !string.IsNullOrWhiteSpace(region.Name) && region.Name != "表" && region.Name != "＋ 表")
            {
                existingIdx = pageData.Tables.FindIndex(t =>
                    string.Equals(t.TableName, region.Name, StringComparison.OrdinalIgnoreCase));
            }

            // 3. ページ内の表領域インデックス（例: 2番目の表領域なら2番目の表）による位置的一致
            if (existingIdx < 0 && tableIndex >= 0 && tableIndex < pageData.Tables.Count)
            {
                existingIdx = tableIndex;
            }

            // 4. ページ内に表が1つだけ存在する場合はその唯一の表を置換
            if (existingIdx < 0 && pageData.Tables.Count == 1)
            {
                existingIdx = 0;
            }

            if (existingIdx >= 0 && existingIdx < pageData.Tables.Count)
            {
                if (string.IsNullOrWhiteSpace(newTable.TableName) || newTable.TableName == "表")
                {
                    newTable.TableName = pageData.Tables[existingIdx].TableName;
                }
                pageData.Tables[existingIdx] = newTable;
            }
            else
            {
                pageData.Tables.Add(newTable);
            }

            // ディスクの page_data.json へ永続保存
            OcrPageDataService.SavePageData(pageDir, pageData);

            // DataGridView を最新構造で再構築
            RefreshTableDataGridFromCurrentBatch();

            // 表タブへ切り替え
            if (tabOcrTable != null && tabOcrResult != null)
            {
                tabOcrResult.SelectedTab = tabOcrTable;
            }

            txtLog.AppendText($"【領域再OCR反映】P.{targetPageNum} の表「{newTable.TableName}」を新構造({newTable.ColumnCount}列×{newTable.RowCount}行)で置換更新しました。" + Environment.NewLine);
        }

        private void RefreshTableDataGridFromCurrentBatch()
        {
            if (dgvOcrTable == null) return;

            tableMergeSpans.Clear();
            dgvOcrTable.Rows.Clear();
            dgvOcrTable.Columns.Clear();

            var allTables = ocrPageDataList
                .Where(p => p.Tables != null && p.Tables.Count > 0)
                .OrderBy(p => p.PageNumber)
                .SelectMany(p => p.Tables.OrderBy(t => t.TableName, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (allTables.Count == 0) return;

            int maxCols = Math.Max(1, allTables.Max(t => t.ColumnCount));

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

            foreach (var sTable in allTables)
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

        private void AppendRegionTextToTargetTab(string regionType, string textToAppend)
        {
            if (string.IsNullOrWhiteSpace(textToAppend)) return;

            string normType = OcrProcessor.NormalizeRegionType(regionType);
            if (normType == "table")
            {
                Clipboard.SetText(textToAppend);
                MessageBox.Show("表領域の認識結果をクリップボードにコピーしました。表タブで貼り付けまたは編集を行ってください。",
                    "表OCR結果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string boxKey = normType switch
            {
                "heading" => "heading",
                "footnote" => "footnote",
                _ => "body"
            };

            if (ocrResultTextBoxes.TryGetValue(boxKey, out var targetBox))
            {
                if (targetBox.TextLength > 0 && !targetBox.Text.EndsWith("\n"))
                {
                    targetBox.AppendText(Environment.NewLine + Environment.NewLine);
                }
                else if (targetBox.TextLength > 0 && !targetBox.Text.EndsWith("\n\n"))
                {
                    targetBox.AppendText(Environment.NewLine);
                }

                targetBox.AppendText(textToAppend + Environment.NewLine);
                SaveCurrentPageData();
                txtLog.AppendText($"【領域再OCR反映】P.{currentPage + 1} {GetRegionTypeName(normType)}タブ末尾に追記しました。" + Environment.NewLine);
            }
        }

        private void ReplaceSelectedTextInTargetTab(string regionType, string replacementText)
        {
            if (string.IsNullOrWhiteSpace(replacementText)) return;

            string normType = OcrProcessor.NormalizeRegionType(regionType);
            if (normType == "table")
            {
                Clipboard.SetText(replacementText);
                MessageBox.Show("表領域の認識結果をクリップボードにコピーしました。表タブで貼り付けまたは編集を行ってください。",
                    "表OCR結果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string boxKey = normType switch
            {
                "heading" => "heading",
                "footnote" => "footnote",
                _ => "body"
            };

            if (ocrResultTextBoxes.TryGetValue(boxKey, out var targetBox))
            {
                if (targetBox.SelectionLength > 0)
                {
                    targetBox.SelectedText = replacementText;
                }
                else
                {
                    if (targetBox.TextLength > 0 && !targetBox.Text.EndsWith("\n"))
                    {
                        targetBox.AppendText(Environment.NewLine + Environment.NewLine);
                    }
                    targetBox.AppendText(replacementText + Environment.NewLine);
                }
                SaveCurrentPageData();
                txtLog.AppendText($"【領域再OCR反映】P.{currentPage + 1} {GetRegionTypeName(normType)}タブの選択箇所を置換しました。" + Environment.NewLine);
            }
        }

        private static List<OcrRegion> ApplyDeckDivision(
            List<OcrRegion> sourceRegions,
            int deckCount,
            string docType = "japanese",
            string orientationMode = "auto")
        {
            if (sourceRegions.Count == 0)
                return sourceRegions;

            var nonBodyRegions = sourceRegions.Where(r => r.Type != "body").ToList();
            var bodyRegions = sourceRegions.Where(r => r.Type == "body").ToList();

            if (bodyRegions.Count == 0)
                return sourceRegions;

            // 1. 横書き・縦書きの判定
            bool isHorizontal;
            if (orientationMode == "horizontal" || docType == "western")
            {
                isHorizontal = true;
            }
            else if (orientationMode == "vertical")
            {
                isHorizontal = false;
            }
            else
            {
                // auto: 領域名やOrientation属性から自動判定
                bool hasHorizontalHint = sourceRegions.Any(r =>
                    r.Orientation == "horizontal" ||
                    r.Name.Contains("横") ||
                    r.Name.Contains("左段") ||
                    r.Name.Contains("右段") ||
                    r.Name.Contains("左ページ") ||
                    r.Name.Contains("右ページ"));

                isHorizontal = hasHorizontalHint;
            }

            int minX = bodyRegions.Min(r => r.X);
            int maxX = bodyRegions.Max(r => r.X + r.Width);
            int minY = bodyRegions.Min(r => r.Y);
            int maxY = bodyRegions.Max(r => r.Y + r.Height);
            int totalW = maxX - minX;
            int totalH = maxY - minY;

            double midX = minX + totalW / 2.0;
            var rightSideRegions = bodyRegions.Where(r => r.X + r.Width / 2.0 >= midX).ToList();
            var leftSideRegions = bodyRegions.Where(r => r.X + r.Width / 2.0 < midX).ToList();

            // 見開き判定（横幅が高さより明確に広い、かつ左右両方にブロックが存在する、または横幅 > 250 かつ 幅 > 高さ * 1.15）
            bool isSpread = (totalW > totalH * 1.15 && totalW > 250) ||
                            sourceRegions.Any(r => r.Name.Contains("左ページ") || r.Name.Contains("右ページ"));

            var result = new List<OcrRegion>();

            if (isSpread)
            {
                // =========================================================
                // 見開き（2ページ分）
                // =========================================================
                int rMinX = rightSideRegions.Count > 0 ? rightSideRegions.Min(r => r.X) : (int)midX + 5;
                int rMaxX = rightSideRegions.Count > 0 ? rightSideRegions.Max(r => r.X + r.Width) : maxX;
                int rMinY = rightSideRegions.Count > 0 ? rightSideRegions.Min(r => r.Y) : minY;
                int rMaxY = rightSideRegions.Count > 0 ? rightSideRegions.Max(r => r.Y + r.Height) : maxY;
                int rW = Math.Max(10, rMaxX - rMinX);
                int rH = Math.Max(10, rMaxY - rMinY);

                int lMinX = leftSideRegions.Count > 0 ? leftSideRegions.Min(r => r.X) : minX;
                int lMaxX = leftSideRegions.Count > 0 ? leftSideRegions.Max(r => r.X + r.Width) : (int)midX - 5;
                int lMinY = leftSideRegions.Count > 0 ? leftSideRegions.Min(r => r.Y) : minY;
                int lMaxY = leftSideRegions.Count > 0 ? leftSideRegions.Max(r => r.Y + r.Height) : maxY;
                int lW = Math.Max(10, lMaxX - lMinX);
                int lH = Math.Max(10, lMaxY - lMinY);

                int effectiveDeck = deckCount;
                if (deckCount <= 0)
                {
                    effectiveDeck = isHorizontal ? 1 : 2;
                }

                if (isHorizontal)
                {
                    // 横書き見開き：左ページ（左半面） → 右ページ（右半面）
                    if (effectiveDeck == 2)
                    {
                        int lHalfH = (int)(lH * 0.485);
                        int lGap = lH - (lHalfH * 2);
                        result.Add(new OcrRegion { Name = "左上本文", Type = "body", Orientation = "horizontal", X = lMinX, Y = lMinY, Width = lW, Height = lHalfH });
                        result.Add(new OcrRegion { Name = "左下本文", Type = "body", Orientation = "horizontal", X = lMinX, Y = lMinY + lHalfH + lGap, Width = lW, Height = lHalfH });

                        int rHalfH = (int)(rH * 0.485);
                        int rGap = rH - (rHalfH * 2);
                        result.Add(new OcrRegion { Name = "右上本文", Type = "body", Orientation = "horizontal", X = rMinX, Y = rMinY, Width = rW, Height = rHalfH });
                        result.Add(new OcrRegion { Name = "右下本文", Type = "body", Orientation = "horizontal", X = rMinX, Y = rMinY + rHalfH + rGap, Width = rW, Height = rHalfH });
                    }
                    else if (effectiveDeck == 3)
                    {
                        int lThirdH = (int)(lH * 0.315);
                        int lGap = Math.Max(2, (lH - (lThirdH * 3)) / 2);
                        result.Add(new OcrRegion { Name = "左上本文", Type = "body", Orientation = "horizontal", X = lMinX, Y = lMinY, Width = lW, Height = lThirdH });
                        result.Add(new OcrRegion { Name = "左中本文", Type = "body", Orientation = "horizontal", X = lMinX, Y = lMinY + lThirdH + lGap, Width = lW, Height = lThirdH });
                        result.Add(new OcrRegion { Name = "左下本文", Type = "body", Orientation = "horizontal", X = lMinX, Y = lMinY + (lThirdH + lGap) * 2, Width = lW, Height = lThirdH });

                        int rThirdH = (int)(rH * 0.315);
                        int rGap = Math.Max(2, (rH - (rThirdH * 3)) / 2);
                        result.Add(new OcrRegion { Name = "右上本文", Type = "body", Orientation = "horizontal", X = rMinX, Y = rMinY, Width = rW, Height = rThirdH });
                        result.Add(new OcrRegion { Name = "右中本文", Type = "body", Orientation = "horizontal", X = rMinX, Y = rMinY + rThirdH + rGap, Width = rW, Height = rThirdH });
                        result.Add(new OcrRegion { Name = "右下本文", Type = "body", Orientation = "horizontal", X = rMinX, Y = rMinY + (rThirdH + rGap) * 2, Width = rW, Height = rThirdH });
                    }
                    else
                    {
                        // 1段組見開き: ① 左ページ本文 -> ② 右ページ本文
                        result.Add(new OcrRegion { Name = "左ページ本文", Type = "body", Orientation = "horizontal", X = lMinX, Y = lMinY, Width = lW, Height = lH });
                        result.Add(new OcrRegion { Name = "右ページ本文", Type = "body", Orientation = "horizontal", X = rMinX, Y = rMinY, Width = rW, Height = rH });
                    }
                }
                else
                {
                    // 和書・縦書き見開き：右ページ（右半面） → 左ページ（左半面）
                    if (effectiveDeck == 2)
                    {
                        int rHalfH = (int)(rH * 0.485);
                        int rGap = rH - (rHalfH * 2);
                        result.Add(new OcrRegion { Name = "右上本文", Type = "body", Orientation = "vertical", X = rMinX, Y = rMinY, Width = rW, Height = rHalfH });
                        result.Add(new OcrRegion { Name = "右下本文", Type = "body", Orientation = "vertical", X = rMinX, Y = rMinY + rHalfH + rGap, Width = rW, Height = rHalfH });

                        int lHalfH = (int)(lH * 0.485);
                        int lGap = lH - (lHalfH * 2);
                        result.Add(new OcrRegion { Name = "左上本文", Type = "body", Orientation = "vertical", X = lMinX, Y = lMinY, Width = lW, Height = lHalfH });
                        result.Add(new OcrRegion { Name = "左下本文", Type = "body", Orientation = "vertical", X = lMinX, Y = lMinY + lHalfH + lGap, Width = lW, Height = lHalfH });
                    }
                    else if (effectiveDeck == 3)
                    {
                        int rThirdH = (int)(rH * 0.315);
                        int rGap = Math.Max(2, (rH - (rThirdH * 3)) / 2);
                        result.Add(new OcrRegion { Name = "右上本文", Type = "body", Orientation = "vertical", X = rMinX, Y = rMinY, Width = rW, Height = rThirdH });
                        result.Add(new OcrRegion { Name = "右中本文", Type = "body", Orientation = "vertical", X = rMinX, Y = rMinY + rThirdH + rGap, Width = rW, Height = rThirdH });
                        result.Add(new OcrRegion { Name = "右下本文", Type = "body", Orientation = "vertical", X = rMinX, Y = rMinY + (rThirdH + rGap) * 2, Width = rW, Height = rThirdH });

                        int lThirdH = (int)(lH * 0.315);
                        int lGap = Math.Max(2, (lH - (lThirdH * 3)) / 2);
                        result.Add(new OcrRegion { Name = "左上本文", Type = "body", Orientation = "vertical", X = lMinX, Y = lMinY, Width = lW, Height = lThirdH });
                        result.Add(new OcrRegion { Name = "左中本文", Type = "body", Orientation = "vertical", X = lMinX, Y = lMinY + lThirdH + lGap, Width = lW, Height = lThirdH });
                        result.Add(new OcrRegion { Name = "左下本文", Type = "body", Orientation = "vertical", X = lMinX, Y = lMinY + (lThirdH + lGap) * 2, Width = lW, Height = lThirdH });
                    }
                    else
                    {
                        // 1段組見開き: ① 右ページ本文 -> ② 左ページ本文
                        result.Add(new OcrRegion { Name = "右ページ本文", Type = "body", Orientation = "vertical", X = rMinX, Y = rMinY, Width = rW, Height = rH });
                        result.Add(new OcrRegion { Name = "左ページ本文", Type = "body", Orientation = "vertical", X = lMinX, Y = lMinY, Width = lW, Height = lH });
                    }
                }
            }
            else
            {
                // =========================================================
                // 単一ページ（縦長または単一画面）
                // =========================================================
                if (isHorizontal)
                {
                    // =====================================================
                    // 横書き段組（左右分割：左段 -> 右段）
                    // =====================================================
                    if (deckCount == 2 || (deckCount == 0 && leftSideRegions.Count > 0 && rightSideRegions.Count > 0 && sourceRegions.Any(r => r.Name.Contains("段"))))
                    {
                        // 2段組: ① 左段本文 -> ② 右段本文
                        int lColMinX = leftSideRegions.Count > 0 ? leftSideRegions.Min(r => r.X) : minX;
                        int lColMaxX = leftSideRegions.Count > 0 ? leftSideRegions.Max(r => r.X + r.Width) : (int)midX - 5;
                        int lColMinY = leftSideRegions.Count > 0 ? leftSideRegions.Min(r => r.Y) : minY;
                        int lColMaxY = leftSideRegions.Count > 0 ? leftSideRegions.Max(r => r.Y + r.Height) : maxY;
                        int lColW = Math.Max(10, lColMaxX - lColMinX);
                        int lColH = Math.Max(10, lColMaxY - lColMinY);

                        int rColMinX = rightSideRegions.Count > 0 ? rightSideRegions.Min(r => r.X) : (int)midX + 5;
                        int rColMaxX = rightSideRegions.Count > 0 ? rightSideRegions.Max(r => r.X + r.Width) : maxX;
                        int rColMinY = rightSideRegions.Count > 0 ? rightSideRegions.Min(r => r.Y) : minY;
                        int rColMaxY = rightSideRegions.Count > 0 ? rightSideRegions.Max(r => r.Y + r.Height) : maxY;
                        int rColW = Math.Max(10, rColMaxX - rColMinX);
                        int rColH = Math.Max(10, rColMaxY - rColMinY);

                        if (deckCount == 2 && (leftSideRegions.Count == 0 || rightSideRegions.Count == 0))
                        {
                            int halfW = (int)(totalW * 0.485);
                            int gap = totalW - (halfW * 2);
                            result.Add(new OcrRegion { Name = "左段本文", Type = "body", Orientation = "horizontal", X = minX, Y = minY, Width = halfW, Height = totalH });
                            result.Add(new OcrRegion { Name = "右段本文", Type = "body", Orientation = "horizontal", X = minX + halfW + gap, Y = minY, Width = halfW, Height = totalH });
                        }
                        else
                        {
                            result.Add(new OcrRegion { Name = "左段本文", Type = "body", Orientation = "horizontal", X = lColMinX, Y = lColMinY, Width = lColW, Height = lColH });
                            result.Add(new OcrRegion { Name = "右段本文", Type = "body", Orientation = "horizontal", X = rColMinX, Y = rColMinY, Width = rColW, Height = rColH });
                        }
                    }
                    else if (deckCount == 3)
                    {
                        // 3段組: ① 左段本文 -> ② 中段本文 -> ③ 右段本文
                        int thirdW = (int)(totalW * 0.315);
                        int gap = Math.Max(2, (totalW - (thirdW * 3)) / 2);
                        result.Add(new OcrRegion { Name = "左段本文", Type = "body", Orientation = "horizontal", X = minX, Y = minY, Width = thirdW, Height = totalH });
                        result.Add(new OcrRegion { Name = "中段本文", Type = "body", Orientation = "horizontal", X = minX + thirdW + gap, Y = minY, Width = thirdW, Height = totalH });
                        result.Add(new OcrRegion { Name = "右段本文", Type = "body", Orientation = "horizontal", X = minX + (thirdW + gap) * 2, Y = minY, Width = thirdW, Height = totalH });
                    }
                    else
                    {
                        // 1段組（単段）または自動判定で単段
                        if (bodyRegions.Count > 1 && bodyRegions.All(r => r.Name.Contains("段") || r.Name.Contains("本文")))
                        {
                            foreach (var b in bodyRegions.OrderBy(r => r.X))
                            {
                                result.Add(new OcrRegion
                                {
                                    Name = b.Name,
                                    Type = "body",
                                    Orientation = "horizontal",
                                    X = b.X,
                                    Y = b.Y,
                                    Width = b.Width,
                                    Height = b.Height
                                });
                            }
                        }
                        else
                        {
                            result.Add(new OcrRegion { Name = "本文", Type = "body", Orientation = "horizontal", X = minX, Y = minY, Width = totalW, Height = totalH });
                        }
                    }
                }
                else
                {
                    // =====================================================
                    // 縦書き段組（上下分割：上段 -> 下段）
                    // =====================================================
                    if (deckCount == 2)
                    {
                        int halfH = (int)(totalH * 0.485);
                        int gap = totalH - (halfH * 2);
                        result.Add(new OcrRegion { Name = "上段本文", Type = "body", Orientation = "vertical", X = minX, Y = minY, Width = totalW, Height = halfH });
                        result.Add(new OcrRegion { Name = "下段本文", Type = "body", Orientation = "vertical", X = minX, Y = minY + halfH + gap, Width = totalW, Height = halfH });
                    }
                    else if (deckCount == 3)
                    {
                        int thirdH = (int)(totalH * 0.315);
                        int gap = Math.Max(2, (totalH - (thirdH * 3)) / 2);
                        result.Add(new OcrRegion { Name = "上段本文", Type = "body", Orientation = "vertical", X = minX, Y = minY, Width = totalW, Height = thirdH });
                        result.Add(new OcrRegion { Name = "中段本文", Type = "body", Orientation = "vertical", X = minX, Y = minY + thirdH + gap, Width = totalW, Height = thirdH });
                        result.Add(new OcrRegion { Name = "下段本文", Type = "body", Orientation = "vertical", X = minX, Y = minY + (thirdH + gap) * 2, Width = totalW, Height = thirdH });
                    }
                    else
                    {
                        result.Add(new OcrRegion { Name = "本文", Type = "body", Orientation = "vertical", X = minX, Y = minY, Width = totalW, Height = totalH });
                    }
                }
            }

            if (nonBodyRegions.Count > 0)
            {
                result.AddRange(nonBodyRegions);
            }

            return result;
        }
    }
}
