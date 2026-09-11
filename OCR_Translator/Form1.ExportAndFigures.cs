using PdfiumViewer;
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
        private void ExtractCurrentPageFigures()
        {
            if (pictureBox1.Image is not Bitmap bmp) return;

            extractedFigures.RemoveAll(f => f.PageNumber == currentPage + 1);

            var imgRegions = regions.Where(r => OcrProcessor.NormalizeRegionType(r.Type) == "image" || OcrProcessor.NormalizeRegionType(r.Name) == "image").ToList();
            int figNum = 1;
            foreach (var reg in imgRegions)
            {
                var figItem = FigureExtractor.CropAndCompressFigure(bmp, reg, currentPage + 1, FigureExtractor.DefaultMaxBytes);
                if (figItem != null)
                {
                    figItem.Name = string.IsNullOrWhiteSpace(reg.Name) || reg.Name == "本文" ? $"図{figNum++}" : reg.Name;
                    extractedFigures.Add(figItem);
                }
            }

            RefreshFigureGalleryView();
        }

        private List<FigureItem> GetAllFigureItems()
        {
            if (pdfDocument == null) return extractedFigures;

            SaveCurrentPageRegions();

            var result = new List<FigureItem>();
            int dpi = appSettings.RenderDpi > 0 ? appSettings.RenderDpi : DefaultPdfRenderDpi;

            for (int pIdx = 0; pIdx < pdfDocument.PageCount; pIdx++)
            {
                List<OcrRegion> pRegs;
                if (pIdx == currentPage)
                    pRegs = regions;
                else if (pageRegions.TryGetValue(pIdx, out var savedRegs))
                    pRegs = savedRegs;
                else
                    continue;

                var imgRegs = pRegs.Where(r => OcrProcessor.NormalizeRegionType(r.Type) == "image" || OcrProcessor.NormalizeRegionType(r.Name) == "image").ToList();
                if (imgRegs.Count == 0) continue;

                // 既に抽出済みのアイテムがあれば再利用
                var existingForPage = extractedFigures.Where(f => f.PageNumber == pIdx + 1).ToList();
                if (existingForPage.Count == imgRegs.Count && existingForPage.Count > 0)
                {
                    result.AddRange(existingForPage);
                    continue;
                }

                // PDFから直接レンダリングして切り出し
                try
                {
                    using Image pageImg = pdfDocument.Render(pIdx, dpi, dpi,
                        PdfRenderFlags.Annotations | PdfRenderFlags.ForPrinting | PdfRenderFlags.LcdText | PdfRenderFlags.CorrectFromDpi);
                    using Bitmap pageBmp = new Bitmap(pageImg);

                    int figNum = 1;
                    foreach (var reg in imgRegs)
                    {
                        var figItem = FigureExtractor.CropAndCompressFigure(pageBmp, reg, pIdx + 1, FigureExtractor.DefaultMaxBytes);
                        if (figItem != null)
                        {
                            figItem.Name = string.IsNullOrWhiteSpace(reg.Name) || reg.Name == "本文" ? $"図{figNum++}" : reg.Name;
                            result.Add(figItem);
                        }
                    }
                }
                catch
                {
                    // レンダリングエラー時は既存データを使用
                }
            }

            if (result.Count > 0)
            {
                extractedFigures.Clear();
                extractedFigures.AddRange(result);
                RefreshFigureGalleryView();
                return result;
            }

            return extractedFigures;
        }

        private void RefreshFigureGalleryView()
        {
            if (pnlFigureGallery == null || tabOcrImage == null) return;
            pnlFigureGallery.SuspendLayout();
            pnlFigureGallery.Controls.Clear();

            tabOcrImage.Text = extractedFigures.Count > 0 ? $"図 ({extractedFigures.Count})" : "図";

            if (extractedFigures.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "※図（image）領域が設定されている場合、ここに500KB以下に最適化された切り出し画像がそのまま表示されます。\n\n・画像上で「図」領域を設定して「OCR開始」を実行するか、画像上で右クリックして「図」領域を作成してください。\n・切り出された画像はそのままWord出力に含まれ、個別コピーも可能です。",
                    AutoSize = false,
                    Size = new Size(Math.Max(420, pnlFigureGallery.ClientSize.Width - 30), 120),
                    ForeColor = Color.DimGray,
                    Margin = new Padding(10),
                    Font = new Font(Font.FontFamily, 9.5f)
                };
                pnlFigureGallery.Controls.Add(lblEmpty);
                pnlFigureGallery.ResumeLayout();
                return;
            }

            int cardWidth = Math.Max(420, pnlFigureGallery.ClientSize.Width - 30);

            foreach (var fig in extractedFigures)
            {
                var card = new Panel
                {
                    Size = new Size(cardWidth, 310),
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(0, 0, 0, 16),
                    Padding = new Padding(8)
                };

                var headerLabel = new Label
                {
                    Text = $"📄 ページ {fig.PageNumber} - {fig.Name}  ({fig.Bounds.Width}×{fig.Bounds.Height} px,  {fig.FileSizeKb:0.#} KB)",
                    Dock = DockStyle.Top,
                    Height = 26,
                    Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(30, 41, 59)
                };

                var btnPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 36,
                    FlowDirection = FlowDirection.LeftToRight,
                    Padding = new Padding(0, 4, 0, 0)
                };

                var btnCopy = new Button
                {
                    Text = "📋 クリップボードにコピー",
                    Size = new Size(185, 30),
                    UseVisualStyleBackColor = true
                };
                btnCopy.Click += (s, e) =>
                {
                    if (fig.Image != null)
                    {
                        Clipboard.SetImage(fig.Image);
                        txtLog.AppendText($"【画像コピー】[P{fig.PageNumber}] {fig.Name} をクリップボードにコピーしました（WordやExcelに貼り付け可能）。" + Environment.NewLine);
                        MessageBox.Show("画像をクリップボードにコピーしました。\nWordやExcel、ペイント等にそのまま貼り付け（Ctrl+V）できます。", "画像コピー完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };

                var btnSave = new Button
                {
                    Text = "💾 画像として保存",
                    Size = new Size(140, 30),
                    UseVisualStyleBackColor = true
                };
                btnSave.Click += (s, e) =>
                {
                    using SaveFileDialog sfd = new SaveFileDialog();
                    sfd.Filter = fig.MimeType == "image/png" ? "PNG画像 (*.png)|*.png|JPEG画像 (*.jpg)|*.jpg" : "JPEG画像 (*.jpg)|*.jpg|PNG画像 (*.png)|*.png";
                    sfd.FileName = $"figure_P{fig.PageNumber}_{fig.Name}.{(fig.MimeType == "image/png" ? "png" : "jpg")}";
                    if (sfd.ShowDialog(this) == DialogResult.OK)
                    {
                        File.WriteAllBytes(sfd.FileName, fig.ImageBytes);
                        MessageBox.Show($"画像を保存しました。\n{sfd.FileName}", "保存完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };

                var picPreview = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    Image = fig.Image,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.FromArgb(241, 245, 249),
                    BorderStyle = BorderStyle.FixedSingle,
                    Cursor = Cursors.Hand
                };
                picPreview.Click += (s, e) =>
                {
                    if (fig.Image != null)
                    {
                        Form viewForm = new Form
                        {
                            Text = $"[P{fig.PageNumber}] {fig.Name} - プレビュー ({fig.FileSizeKb:0.#} KB)",
                            Size = new Size(Math.Min(1000, fig.Bounds.Width + 60), Math.Min(800, fig.Bounds.Height + 80)),
                            StartPosition = FormStartPosition.CenterParent
                        };
                        PictureBox p = new PictureBox { Dock = DockStyle.Fill, Image = fig.Image, SizeMode = PictureBoxSizeMode.Zoom };
                        viewForm.Controls.Add(p);
                        viewForm.Show(this);
                    }
                };

                btnPanel.Controls.Add(btnCopy);
                btnPanel.Controls.Add(btnSave);

                card.Controls.Add(picPreview);
                card.Controls.Add(headerLabel);
                card.Controls.Add(btnPanel);

                pnlFigureGallery.Controls.Add(card);
            }

            pnlFigureGallery.ResumeLayout();
        }

        private bool isSyncingHeadings = false;

        private void ClearOcrResultTabs()
        {
            isSyncingHeadings = true;
            try
            {
                foreach (RichTextBox resultBox in ocrResultTextBoxes.Values)
                    resultBox.Clear();
            }
            finally
            {
                isSyncingHeadings = false;
            }

            tableMergeSpans.Clear();
            extractedFigures.Clear();
            ocrPageDataList.Clear();
            RefreshFigureGalleryView();

            if (dgvOcrTable != null)
            {
                dgvOcrTable.Rows.Clear();
                dgvOcrTable.Columns.Clear();
            }
        }

        private int GetNextAnnotationNumberForCurrentPage()
        {
            int maxNum = 0;
            int pageNum = currentPage + 1;

            if (ocrResultTextBoxes.TryGetValue("body", out var bBox))
            {
                string bodyText = bBox.Text;
                var bodyByPage = ParseBodyTextByPages(bodyText);
                if (bodyByPage.TryGetValue(pageNum, out var paras))
                {
                    foreach (var para in paras)
                    {
                        var matches = System.Text.RegularExpressions.Regex.Matches(para, @"【注(\d+)】");
                        foreach (System.Text.RegularExpressions.Match m in matches)
                        {
                            if (int.TryParse(m.Groups[1].Value, out int n))
                                maxNum = Math.Max(maxNum, n);
                        }
                    }
                }
                else
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(bodyText, @"【注(\d+)】");
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        if (int.TryParse(m.Groups[1].Value, out int n))
                            maxNum = Math.Max(maxNum, n);
                    }
                }
            }

            return maxNum + 1;
        }

        private int GetNextFootnoteNumberForCurrentPage()
        {
            int maxNum = 0;
            int pageNum = currentPage + 1;

            // 1. 注釈文タブから検出
            if (ocrResultTextBoxes.TryGetValue("footnote", out var fBox))
            {
                var lines = fBox.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                string prefix = $"[P{pageNum}";
                foreach (var line in lines)
                {
                    if (line.StartsWith(prefix) || (!line.StartsWith("[P") && currentPage == 0))
                    {
                        var matches = System.Text.RegularExpressions.Regex.Matches(line, @"【(?:注釈文|注)(\d+)】");
                        foreach (System.Text.RegularExpressions.Match m in matches)
                        {
                            if (int.TryParse(m.Groups[1].Value, out int n))
                                maxNum = Math.Max(maxNum, n);
                        }
                    }
                }
            }

            // 2. 本文タブ内の既存【注釈文N】からも検出
            if (ocrResultTextBoxes.TryGetValue("body", out var bBox))
            {
                string bodyText = bBox.Text;
                var bodyByPage = ParseBodyTextByPages(bodyText);
                if (bodyByPage.TryGetValue(pageNum, out var paras))
                {
                    foreach (var para in paras)
                    {
                        var matches = System.Text.RegularExpressions.Regex.Matches(para, @"【注釈文(\d+)】");
                        foreach (System.Text.RegularExpressions.Match m in matches)
                        {
                            if (int.TryParse(m.Groups[1].Value, out int n))
                                maxNum = Math.Max(maxNum, n);
                        }
                    }
                }
            }

            // 3. ocrPageDataList からも検出
            var pData = ocrPageDataList.FirstOrDefault(p => p.PageNumber == pageNum);
            if (pData != null && pData.Footnotes != null)
            {
                foreach (var fn in pData.Footnotes)
                {
                    var matches = System.Text.RegularExpressions.Regex.Matches(fn, @"【(?:注釈文|注)(\d+)】");
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        if (int.TryParse(m.Groups[1].Value, out int n))
                            maxNum = Math.Max(maxNum, n);
                    }
                }
            }

            return maxNum + 1;
        }

        public static Dictionary<int, List<string>> ParseBodyTextByPages(string fullBodyText)
        {
            var result = new Dictionary<int, List<string>>();
            if (string.IsNullOrWhiteSpace(fullBodyText)) return result;

            // 各行を走査して "--- ページ N ---" 境界でページごとにグループ化
            string[] rawLines = fullBodyText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int curPage = 1;
            bool hasExplicitPageHeader = false;
            var pageLinesMap = new Dictionary<int, List<string>>();
            var initialLines = new List<string>();

            foreach (string rawLine in rawLines)
            {
                string trimmed = rawLine.Trim();
                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^---\s*ページ\s*(\d+)\s*---$");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int parsedPageNum))
                {
                    hasExplicitPageHeader = true;
                    curPage = parsedPageNum;
                    if (!pageLinesMap.ContainsKey(curPage))
                    {
                        pageLinesMap[curPage] = new List<string>();
                    }
                }
                else
                {
                    if (!hasExplicitPageHeader)
                    {
                        initialLines.Add(rawLine);
                    }
                    else
                    {
                        if (!pageLinesMap.ContainsKey(curPage))
                        {
                            pageLinesMap[curPage] = new List<string>();
                        }
                        pageLinesMap[curPage].Add(rawLine);
                    }
                }
            }

            if (!hasExplicitPageHeader)
            {
                pageLinesMap[1] = initialLines;
            }
            else if (initialLines.Count > 0 && initialLines.Any(l => !string.IsNullOrWhiteSpace(l)))
            {
                int firstHeaderPage = pageLinesMap.Keys.DefaultIfEmpty(1).Min();
                if (pageLinesMap.TryGetValue(firstHeaderPage, out var firstList))
                {
                    firstList.InsertRange(0, initialLines);
                }
                else
                {
                    pageLinesMap[1] = initialLines;
                }
            }

            foreach (var kvp in pageLinesMap)
            {
                string pageText = string.Join(Environment.NewLine, kvp.Value).Trim();
                if (string.IsNullOrEmpty(pageText)) continue;

                var paras = pageText.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();

                if (paras.Count > 0)
                {
                    result[kvp.Key] = paras;
                }
            }

            return result;
        }

        public static Dictionary<int, List<string>> ParseItemsByPages(string fullText)
        {
            var result = new Dictionary<int, List<string>>();
            if (string.IsNullOrWhiteSpace(fullText)) return result;

            var lines = fullText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            int curPage = 1;
            foreach (var line in lines)
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"^\[P(\d+)(?:-\d+)?\]\s*(.*)$");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int pageNum))
                {
                    curPage = pageNum;
                    string content = match.Groups[2].Value.Trim();
                    if (!result.ContainsKey(curPage)) result[curPage] = new List<string>();
                    if (!string.IsNullOrEmpty(content)) result[curPage].Add(content);
                }
                else
                {
                    if (!result.ContainsKey(curPage)) result[curPage] = new List<string>();
                    result[curPage].Add(line.Trim());
                }
            }

            return result;
        }

        /// <summary>
        /// 見出しタブの全テキストをページごとに解析し、第1階層（見出し領域・インデントなし）と第2階層（本文小見出し・インデントあり）に分類して返します。
        /// </summary>
        public static Dictionary<int, (List<string> Headings, List<string> Subheadings)> ParseHeadingsByPagesWithLevels(string fullText)
        {
            var result = new Dictionary<int, (List<string> Headings, List<string> Subheadings)>();
            if (string.IsNullOrWhiteSpace(fullText)) return result;

            var lines = fullText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            int curPage = 1;
            foreach (var line in lines)
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"^\[P(\d+)(?:-\d+)?\]\s*(.*)$");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int pageNum))
                {
                    curPage = pageNum;
                    string rawContent = match.Groups[2].Value;
                    if (!result.ContainsKey(curPage)) result[curPage] = (new List<string>(), new List<string>());

                    bool isIndented = rawContent.StartsWith("    ") || rawContent.StartsWith("　") || rawContent.StartsWith("\t") || rawContent.StartsWith("  ");
                    string clean = DocxExporter.RemovePagePrefix(rawContent.Trim());
                    if (!string.IsNullOrEmpty(clean) && clean.Length <= 80 && !clean.Contains("。") &&
                        !System.Text.RegularExpressions.Regex.IsMatch(clean, @"^【(?:注釈文|注)\d+】") && !DocxExporter.IsGarbageOrNoiseHeading(clean))
                    {
                        bool isSection = System.Text.RegularExpressions.Regex.IsMatch(clean, @"^第[0-9０-９一二三四五六七八九十百千万]+[節項条回]") ||
                                         System.Text.RegularExpressions.Regex.IsMatch(clean, @"^(?:Section|Sec\.)\s+[0-9IVXLCDMivxlcdm]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        bool isSub = isIndented || isSection || OcrSorter.IsSubheadingText(clean);
                        if (isSub && !OcrSorter.IsMajorHeading(clean))
                            result[curPage].Subheadings.Add(clean);
                        else
                            result[curPage].Headings.Add(clean);
                    }
                }
                else
                {
                    if (!result.ContainsKey(curPage)) result[curPage] = (new List<string>(), new List<string>());
                    bool isIndented = line.StartsWith("    ") || line.StartsWith("　") || line.StartsWith("\t") || line.StartsWith("  ");
                    string clean = DocxExporter.RemovePagePrefix(line.Trim());
                    if (!string.IsNullOrEmpty(clean) && clean.Length <= 80 && !clean.Contains("。") &&
                        !System.Text.RegularExpressions.Regex.IsMatch(clean, @"^【(?:注釈文|注)\d+】") && !DocxExporter.IsGarbageOrNoiseHeading(clean))
                    {
                        bool isSection = System.Text.RegularExpressions.Regex.IsMatch(clean, @"^第[0-9０-９一二三四五六七八九十百千万]+[節項条回]") ||
                                         System.Text.RegularExpressions.Regex.IsMatch(clean, @"^(?:Section|Sec\.)\s+[0-9IVXLCDMivxlcdm]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        bool isSub = isIndented || isSection || OcrSorter.IsSubheadingText(clean);
                        if (isSub && !OcrSorter.IsMajorHeading(clean))
                            result[curPage].Subheadings.Add(clean);
                        else
                            result[curPage].Headings.Add(clean);
                    }
                }
            }

            return result;
        }

        private List<OcrPageData> BuildExportPages()
        {
            var allFigures = GetAllFigureItems();

            // DataGridViewから全表を厳密に分離して抽出（表1, 表2...のデータ混入を完全防止）
            List<StructuredTable> allTables = new();
            if (dgvOcrTable != null && dgvOcrTable.RowCount > 0)
            {
                allTables = TableCellMerger.ExtractTablesFromDataGridView(dgvOcrTable, tableMergeSpans);
            }

            string bodyText = ocrResultTextBoxes.TryGetValue("body", out var bBox) ? bBox.Text : "";
            string headingText = ocrResultTextBoxes.TryGetValue("heading", out var hBox) ? hBox.Text : "";
            string footnoteText = ocrResultTextBoxes.TryGetValue("footnote", out var fBox) ? fBox.Text : "";

            var bodyByPage = ParseBodyTextByPages(bodyText);
            var headingsByPage = ParseItemsByPages(headingText);
            var footnotesByPage = OcrPageDataService.ParseFootnotesByPages(footnoteText);

            // 本文タブの青色小見出しをページごとに厳密抽出して該当ページの見出しリストにマージ
            if (ocrResultTextBoxes.TryGetValue("body", out var bodyRtb))
            {
                var blueHeadingsByPage = ExtractBlueHeadingsByPageFromRichTextBox(bodyRtb, currentPage + 1);
                foreach (var kvp in blueHeadingsByPage)
                {
                    int pNum = kvp.Key;
                    if (!headingsByPage.ContainsKey(pNum))
                    {
                        headingsByPage[pNum] = new List<string>();
                    }
                    foreach (var bh in kvp.Value)
                    {
                        if (!headingsByPage[pNum].Contains(bh))
                        {
                            headingsByPage[pNum].Add(bh);
                        }
                    }
                }
            }

            // プロジェクトフォルダ内の全 page_XXXX/page_data.json を収集
            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            string pdfName = string.IsNullOrEmpty(currentPdfPath) ? "" : Path.GetFileNameWithoutExtension(currentPdfPath);
            int totalPdfPages = pdfDocument != null ? pdfDocument.PageCount : 1;
            var diskPages = OcrPageDataService.LoadAllProjectPageData(projectDir, pdfName, totalPdfPages);

            // メモリ上の ocrPageDataList と diskPages をマージ
            var combinedPageDict = new Dictionary<int, OcrPageData>();
            foreach (var dp in diskPages)
            {
                combinedPageDict[dp.PageNumber] = dp;
            }
            foreach (var mp in ocrPageDataList)
            {
                combinedPageDict[mp.PageNumber] = mp;
            }

            // UIの本文に存在する全ページも確実に登録
            foreach (var kvp in bodyByPage)
            {
                if (!combinedPageDict.ContainsKey(kvp.Key))
                {
                    combinedPageDict[kvp.Key] = new OcrPageData { PageNumber = kvp.Key };
                }
            }

            var exportList = combinedPageDict.Values.OrderBy(p => p.PageNumber).ToList();

            if (exportList.Count > 0)
            {
                // 各ページの図・表およびUI編集後の最新本文・見出し・注釈文を完全同期
                foreach (var pData in exportList)
                {
                    var pageFigs = allFigures.Where(f => f.PageNumber == pData.PageNumber).ToList();
                    if (pageFigs.Count > 0)
                    {
                        pData.Figures = pageFigs;
                    }

                    if (allTables.Count > 0)
                    {
                        var pTables = allTables.Where(t => t.PageNumber == pData.PageNumber).ToList();
                        if (pTables.Count > 0)
                        {
                            pData.Tables = pTables;
                        }
                    }

                    // UIで編集された本文段落を同期
                    if (bodyByPage.TryGetValue(pData.PageNumber, out var paras) && paras.Count > 0)
                    {
                        pData.BodyParagraphs = paras;
                    }

                    // UIの見出しタブの最新状態を同期（UIに該当ページの見出しがある場合のみ）
                    if (headingsByPage.TryGetValue(pData.PageNumber, out var headings) && headings.Count > 0)
                    {
                        pData.Headings = headings.Select(h => DocxExporter.RemovePagePrefix(h))
                            .Where(h => !string.IsNullOrWhiteSpace(h) && h.Length <= 80 && !h.Contains("。") && !Regex.IsMatch(h, @"^【(?:注釈文|注)\d+】"))
                            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    }
                    else if (pData.Headings != null && pData.Headings.Count > 0)
                    {
                        pData.Headings = pData.Headings
                            .Select(h => DocxExporter.RemovePagePrefix(h))
                            .Where(h => !string.IsNullOrWhiteSpace(h) && h.Length <= 80 && !h.Contains("。") && !Regex.IsMatch(h, @"^【(?:注釈文|注)\d+】"))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    }

                    // UIの注釈文タブの最新状態を同期（UIに該当ページの注釈文がある場合のみ）
                    if (footnotesByPage.TryGetValue(pData.PageNumber, out var footnotes) && footnotes.Count > 0)
                    {
                        pData.Footnotes = footnotes;
                    }
                }

                return exportList;
            }

            // ocrPageDataListが空の場合（OCR未実行時や部分実行時）はUIのテキスト・表・図から構築
            var fallbackList = new List<OcrPageData>();
            var p1 = new OcrPageData { PageNumber = 1 };

            if (!string.IsNullOrWhiteSpace(headingText))
            {
                foreach (var line in headingText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                    p1.Headings.Add(line.Trim());
            }

            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                foreach (var p in bodyText.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
                    p1.BodyParagraphs.Add(p.Trim());
            }

            if (allTables.Count > 0)
            {
                p1.Tables.AddRange(allTables);
            }

            p1.Figures.AddRange(allFigures);

            if (!string.IsNullOrWhiteSpace(footnoteText))
            {
                foreach (var line in footnoteText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                    p1.Footnotes.Add(line.Trim());
            }

            fallbackList.Add(p1);
            return fallbackList;
        }

        private void btnExportWord_Click(object? sender, EventArgs e)
        {
            using SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Word文書 (*.docx)|*.docx|Word(HTML/Doc) (*.doc)|*.doc|HTML文書 (*.html)|*.html";
            sfd.Title = "Word文書 (.docx) として保存";
            sfd.DefaultExt = "docx";
            sfd.AddExtension = true;
            sfd.FilterIndex = 1;
            string defaultName = string.IsNullOrEmpty(currentPdfPath)
                ? "OCR_Result.docx"
                : Path.GetFileNameWithoutExtension(currentPdfPath) + "_OCR.docx";
            sfd.FileName = defaultName;

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    SaveCurrentPageData();
                    var exportPages = BuildExportPages();

                    string savePath = sfd.FileName;
                    string ext = Path.GetExtension(savePath).ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext))
                    {
                        savePath += ".docx";
                        ext = ".docx";
                    }

                    if (ext == ".docx")
                    {
                        DocxExporter.ExportToDocxFile(savePath, exportPages, appSettings);
                    }
                    else
                    {
                        string bodyText = ocrResultTextBoxes.TryGetValue("body", out var bBox) ? bBox.Text : "";
                        string headingText = ocrResultTextBoxes.TryGetValue("heading", out var hBox) ? hBox.Text : "";
                        string footnoteText = ocrResultTextBoxes.TryGetValue("footnote", out var fBox) ? fBox.Text : "";
                        var allFigs = GetAllFigureItems();

                        TableCellMerger.ExportToWordFile(
                            savePath,
                            bodyText,
                            headingText,
                            footnoteText,
                            dgvOcrTable,
                            tableMergeSpans,
                            appSettings,
                            allFigs);
                    }

                    int totalFigs = exportPages.Sum(p => p.Figures.Count);
                    int totalTables = exportPages.Sum(p => p.Tables.Count);
                    txtLog.AppendText($"【Word出力】{savePath} に保存しました（{exportPages.Count}ページ、表: {totalTables}点、図: {totalFigs}点）。" + Environment.NewLine);
                    MessageBox.Show($"Word文書を出力しました。\n{savePath}", "Word出力完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存に失敗しました。\n{ex.Message}", "Word出力エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private readonly HashSet<string> userRemovedHeadings = new(StringComparer.OrdinalIgnoreCase);

        public bool IsHeadingRemoved(OcrPageData? pageData, int pageNum, string headingText)
        {
            if (string.IsNullOrWhiteSpace(headingText)) return true;
            string clean = DocxExporter.RemovePagePrefix(headingText.Trim());
            string norm = DocxExporter.NormalizeForComparison(clean);

            bool isChapterOrSection = System.Text.RegularExpressions.Regex.IsMatch(clean, @"^(?:序章|終章|第[0-9０-９一二三四五六七八九十百千万]+[章節編部])");
            if (isChapterOrSection) return false; // 章や節の見出しは絶対に削除扱いしない

            var removedSnapshot = userRemovedHeadings.ToList();
            if (removedSnapshot.Contains($"{pageNum}::{clean}")) return true;
            if (removedSnapshot.Any(u => u.StartsWith($"{pageNum}::") &&
                (DocxExporter.NormalizeForComparison(u.Substring(u.IndexOf("::") + 2)) == norm ||
                 (clean.Contains(u.Substring(u.IndexOf("::") + 2), StringComparison.OrdinalIgnoreCase) ||
                  u.Substring(u.IndexOf("::") + 2).Contains(clean, StringComparison.OrdinalIgnoreCase)))))
            {
                return true;
            }

            var pageRemoved = pageData?.RemovedHeadings?.ToList();
            if (pageRemoved != null)
            {
                if (pageRemoved.Contains(clean, StringComparer.OrdinalIgnoreCase)) return true;
                if (pageRemoved.Any(rh =>
                    rh.Equals(clean, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(norm) && DocxExporter.NormalizeForComparison(rh) == norm) ||
                    (clean.Contains(rh, StringComparison.OrdinalIgnoreCase) ||
                     rh.Contains(clean, StringComparison.OrdinalIgnoreCase))))
                {
                    return true;
                }
            }

            return false;
        }

        public int GetPageNumberAtSelection(RichTextBox? rtb, int charIndex)
        {
            if (rtb == null || rtb.TextLength == 0) return currentPage + 1;

            try
            {
                int lineIdx = rtb.GetLineFromCharIndex(Math.Min(charIndex, rtb.TextLength - 1));
                if (lineIdx >= 0 && lineIdx < rtb.Lines.Length)
                {
                    // 見出しタブや注釈文タブ（各行が [P12] 形式で始まっている場合）
                    string currentLine = rtb.Lines[lineIdx].Trim();
                    var pMatch = System.Text.RegularExpressions.Regex.Match(currentLine, @"^\[P(\d+)(?:-\d+)?\]");
                    if (pMatch.Success && int.TryParse(pMatch.Groups[1].Value, out int pNumFromLine))
                    {
                        return pNumFromLine;
                    }

                    // 本文タブ（上に遡って直前の "--- ページ N ---" を探す）
                    for (int i = lineIdx; i >= 0; i--)
                    {
                        string line = rtb.Lines[i].Trim();
                        var match = System.Text.RegularExpressions.Regex.Match(line, @"^---\s*ページ\s*(\d+)\s*---$");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int parsedPNum))
                        {
                            return parsedPNum;
                        }
                    }
                }
            }
            catch { }

            return currentPage + 1;
        }

        private void UnmarkHeadingInBodyRichTextBox(int targetPageNum, string headingText)
        {
            if (string.IsNullOrWhiteSpace(headingText)) return;
            if (!ocrResultTextBoxes.TryGetValue("body", out var bBox) || bBox == null || bBox.TextLength == 0) return;

            try
            {
                string cleanTarget = DocxExporter.RemovePagePrefix(headingText.Trim());
                string normTarget = DocxExporter.NormalizeForComparison(cleanTarget);

                bool hasPageMarkers = bBox.Text.Contains("--- ページ");
                int curPage = 1;
                bool inTargetPage = !hasPageMarkers || (curPage == targetPageNum);
                string[] lines = bBox.Lines;

                for (int i = 0; i < lines.Length; i++)
                {
                    string rawLine = lines[i];
                    string trimmed = rawLine.Trim();

                    var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^---\s*ページ\s*(\d+)\s*---$");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int pNum))
                    {
                        curPage = pNum;
                        inTargetPage = (curPage == targetPageNum);
                        continue;
                    }

                    if (inTargetPage && !string.IsNullOrWhiteSpace(trimmed))
                    {
                        string cleanLine = DocxExporter.RemovePagePrefix(trimmed);
                        string normLine = DocxExporter.NormalizeForComparison(cleanLine);

                        if (cleanLine.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase) ||
                            cleanLine.StartsWith(cleanTarget, StringComparison.OrdinalIgnoreCase) ||
                            cleanLine.Contains(cleanTarget, StringComparison.OrdinalIgnoreCase) ||
                            cleanTarget.Contains(cleanLine, StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(normTarget) && (normLine.Contains(normTarget) || normTarget.Contains(normLine))))
                        {
                            int lineStart = bBox.GetFirstCharIndexFromLine(i);
                            if (lineStart >= 0)
                            {
                                int origStart = bBox.SelectionStart;
                                int origLen = bBox.SelectionLength;

                                bBox.Select(lineStart, rawLine.Length);
                                bBox.SelectionColor = Color.FromArgb(31, 41, 55);
                                bBox.SelectionFont = new Font(bBox.Font, FontStyle.Regular);

                                bBox.Select(origStart, origLen);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void MarkHeadingInBodyRichTextBox(int targetPageNum, string headingText)
        {
            if (string.IsNullOrWhiteSpace(headingText)) return;
            if (!ocrResultTextBoxes.TryGetValue("body", out var bBox) || bBox == null || bBox.TextLength == 0) return;

            try
            {
                string cleanTarget = DocxExporter.RemovePagePrefix(headingText.Trim());
                string normTarget = DocxExporter.NormalizeForComparison(cleanTarget);

                int curPage = 1;
                bool inTargetPage = false;
                string[] lines = bBox.Lines;

                for (int i = 0; i < lines.Length; i++)
                {
                    string rawLine = lines[i];
                    string trimmed = rawLine.Trim();

                    var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^---\s*ページ\s*(\d+)\s*---$");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int pNum))
                    {
                        curPage = pNum;
                        inTargetPage = (curPage == targetPageNum);
                        continue;
                    }

                    if (inTargetPage && !string.IsNullOrWhiteSpace(trimmed))
                    {
                        string cleanLine = DocxExporter.RemovePagePrefix(trimmed);
                        string normLine = DocxExporter.NormalizeForComparison(cleanLine);

                        if (cleanLine.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase) ||
                            cleanLine.StartsWith(cleanTarget, StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(normTarget) && normLine.StartsWith(normTarget)))
                        {
                            int lineStart = bBox.GetFirstCharIndexFromLine(i);
                            if (lineStart >= 0)
                            {
                                int origStart = bBox.SelectionStart;
                                int origLen = bBox.SelectionLength;

                                bBox.Select(lineStart, rawLine.Length);
                                bBox.SelectionColor = Color.FromArgb(37, 99, 235);
                                bBox.SelectionFont = new Font(bBox.Font, FontStyle.Bold);

                                bBox.Select(origStart, origLen);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void btnAddHeading_Click(object? sender, EventArgs e)
        {
            RichTextBox? resultBox = tabOcrResult?.SelectedTab?.Controls.OfType<RichTextBox>().FirstOrDefault()
                                  ?? (ocrResultTextBoxes.TryGetValue("body", out var bBox) ? bBox : null);

            if (resultBox == null)
            {
                MessageBox.Show("本文タブまたは見出しタブを選択してください。",
                    "見出し設定", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 見出しタブにいる場合は、選択行の小見出し解除を実行
            if (tabOcrResult?.SelectedTab?.Text.Contains("見出し") == true)
            {
                RemoveHeadingFromSelection(resultBox);
                return;
            }

            if (resultBox.SelectionLength == 0)
            {
                // 選択範囲がない場合はカーソル位置の行全体を選択
                int currentLineIdx = resultBox.GetLineFromCharIndex(resultBox.SelectionStart);
                if (currentLineIdx >= 0 && currentLineIdx < resultBox.Lines.Length)
                {
                    int lineStart = resultBox.GetFirstCharIndexFromLine(currentLineIdx);
                    int lineLen = resultBox.Lines[currentLineIdx].Length;
                    resultBox.Select(lineStart, lineLen);
                }
            }

            string selectedText = resultBox.SelectedText.Trim();
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                MessageBox.Show("見出しに設定する文字列を本文から選択してください。",
                    "見出し設定", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int targetPageNum = GetPageNumberAtSelection(resultBox, resultBox.SelectionStart);

            var headingLines = selectedText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => DocxExporter.RemovePagePrefix(l.Trim()))
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();

            if (headingLines.Count == 0) return;

            var pData = ocrPageDataList.FirstOrDefault(p => p.PageNumber == targetPageNum);

            // すでに小見出しとして登録・着色されている場合はトグル解除動作
            bool isAlreadyHeading = false;
            if (ocrResultTextBoxes.TryGetValue("body", out var bodyBox) && resultBox == bodyBox && IsBlueSubheadingColor(resultBox.SelectionColor))
            {
                isAlreadyHeading = true;
            }
            else if (pData?.Headings != null && headingLines.Any(hl => pData.Headings.Any(h => DocxExporter.RemovePagePrefix(h).Equals(hl, StringComparison.OrdinalIgnoreCase))))
            {
                isAlreadyHeading = true;
            }

            if (isAlreadyHeading)
            {
                RemoveHeadingFromSelection(resultBox);
                return;
            }

            // 本文での強調（選択された全行を太字＆鮮やかな青色 #2563EB / RGB: 37, 99, 235）
            if (bodyBox != null)
            {
                if (resultBox == bodyBox)
                {
                    resultBox.SelectionColor = Color.FromArgb(37, 99, 235);
                    resultBox.SelectionFont = new Font(resultBox.Font, FontStyle.Bold);
                }
                else
                {
                    foreach (var line in headingLines)
                    {
                        MarkHeadingInBodyRichTextBox(targetPageNum, line);
                    }
                }
            }

            string pdfName = string.IsNullOrEmpty(currentPdfPath) ? "" : Path.GetFileNameWithoutExtension(currentPdfPath);
            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            if (pData == null)
            {
                string pageDir = Path.Combine(projectDir, "ocr_results", pdfName, $"page_{targetPageNum:0000}");
                pData = OcrPageDataService.LoadPageData(pageDir) ?? new OcrPageData { PageNumber = targetPageNum };
                ocrPageDataList.Add(pData);
            }

            if (pData.Headings == null) pData.Headings = new List<string>();
            if (pData.RemovedHeadings != null)
            {
                foreach (var line in headingLines)
                {
                    pData.RemovedHeadings.RemoveAll(rh => rh.Equals(line, StringComparison.OrdinalIgnoreCase));
                }
            }
            foreach (var line in headingLines)
            {
                userRemovedHeadings.Remove($"{targetPageNum}::{line}");
                if (!pData.Headings.Contains(line, StringComparer.OrdinalIgnoreCase))
                {
                    pData.Headings.Add(line);
                }
            }

            // ディスクへ即時永続保存
            if (!string.IsNullOrEmpty(pdfName))
            {
                string pageDir = Path.Combine(projectDir, "ocr_results", pdfName, $"page_{targetPageNum:0000}");
                if (Directory.Exists(pageDir))
                {
                    OcrPageDataService.SavePageData(pageDir, pData);
                }
            }

            // 見出しの完全同期を実行（見出しタブ・内部データを一括更新）
            SyncHeadings(targetPageNum, updateHeadingTab: true);
            SaveCurrentPageData();

            txtLog.AppendText($"【小見出し設定】[P{targetPageNum}] 「{string.Join(" / ", headingLines)}」（青色太字）を小見出しとして登録し、見出し一覧を更新しました。" + Environment.NewLine);
        }

        private void RemoveHeadingFromSelection(RichTextBox? resultBox)
        {
            if (resultBox == null) return;

            int startChar = resultBox.SelectionStart;
            int selLen = resultBox.SelectionLength;
            int startLineIdx = resultBox.GetLineFromCharIndex(startChar);
            int endLineIdx = resultBox.GetLineFromCharIndex(startChar + Math.Max(0, selLen > 0 ? selLen - 1 : 0));

            var linesByPage = new Dictionary<int, HashSet<string>>();

            for (int i = startLineIdx; i <= endLineIdx && i < resultBox.Lines.Length; i++)
            {
                string raw = resultBox.Lines[i].Trim();
                if (string.IsNullOrWhiteSpace(raw)) continue;

                int lineCharIdx = resultBox.GetFirstCharIndexFromLine(i);
                int linePageNum = GetPageNumberAtSelection(resultBox, lineCharIdx);

                string clean = DocxExporter.RemovePagePrefix(raw);
                if (!string.IsNullOrWhiteSpace(clean))
                {
                    if (!linesByPage.TryGetValue(linePageNum, out var list))
                    {
                        list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        linesByPage[linePageNum] = list;
                    }
                    list.Add(clean);
                }
            }

            if (linesByPage.Count == 0 && selLen > 0)
            {
                string selClean = DocxExporter.RemovePagePrefix(resultBox.SelectedText.Trim());
                if (!string.IsNullOrWhiteSpace(selClean))
                {
                    int pNum = GetPageNumberAtSelection(resultBox, startChar);
                    linesByPage[pNum] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { selClean };
                }
            }

            if (linesByPage.Count == 0) return;

            string pdfName = string.IsNullOrEmpty(currentPdfPath) ? "" : Path.GetFileNameWithoutExtension(currentPdfPath);
            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            bool isBodyBox = (ocrResultTextBoxes.TryGetValue("body", out var bBox) && resultBox == bBox);

            foreach (var kvp in linesByPage)
            {
                int pNum = kvp.Key;
                var linesToRemove = kvp.Value;

                // 1. 本文タブ（bBox）側の該当行の色と太字を解除（通常文字色 #1F2937, レギュラー）
                foreach (var line in linesToRemove)
                {
                    UnmarkHeadingInBodyRichTextBox(pNum, line);
                }

                // 2. メモリ上およびディスク上の OcrPageData から該当見出しを即時完全削除 & 解除リストへ追加
                var pData = ocrPageDataList.FirstOrDefault(p => p.PageNumber == pNum);
                if (pData == null && !string.IsNullOrEmpty(pdfName))
                {
                    string pageDir = Path.Combine(projectDir, "ocr_results", pdfName, $"page_{pNum:0000}");
                    pData = OcrPageDataService.LoadPageData(pageDir) ?? new OcrPageData { PageNumber = pNum };
                    ocrPageDataList.Add(pData);
                }

                if (pData != null)
                {
                    if (pData.Headings != null)
                    {
                        foreach (var line in linesToRemove)
                        {
                            string normLine = DocxExporter.NormalizeForComparison(line);
                            pData.Headings.RemoveAll(h =>
                            {
                                string cleanH = DocxExporter.RemovePagePrefix(h);
                                string normH = DocxExporter.NormalizeForComparison(cleanH);
                                return cleanH.Equals(line, StringComparison.OrdinalIgnoreCase) ||
                                       cleanH.Contains(line, StringComparison.OrdinalIgnoreCase) ||
                                       line.Contains(cleanH, StringComparison.OrdinalIgnoreCase) ||
                                       (!string.IsNullOrEmpty(normLine) && (normH == normLine || normH.Contains(normLine) || normLine.Contains(normH)));
                            });
                        }
                    }
                    if (pData.RemovedHeadings == null) pData.RemovedHeadings = new List<string>();
                    foreach (var line in linesToRemove)
                    {
                        if (!pData.RemovedHeadings.Contains(line, StringComparer.OrdinalIgnoreCase))
                        {
                            pData.RemovedHeadings.Add(line);
                        }
                        userRemovedHeadings.Add($"{pNum}::{line}");
                    }

                    // ディスクへ即時永続保存！
                    if (!string.IsNullOrEmpty(pdfName))
                    {
                        string pageDir = Path.Combine(projectDir, "ocr_results", pdfName, $"page_{pNum:0000}");
                        if (Directory.Exists(pageDir))
                        {
                            OcrPageDataService.SavePageData(pageDir, pData);
                        }
                    }
                }

                txtLog.AppendText($"【小見出し解除】[P{pNum}] 「{string.Join(" / ", linesToRemove)}」の小見出し設定を解除し、見出し一覧を更新しました。" + Environment.NewLine);
            }

            if (isBodyBox && bBox != null)
            {
                bBox.SelectionColor = Color.FromArgb(31, 41, 55);
                bBox.SelectionFont = new Font(bBox.Font, FontStyle.Regular);
            }

            // 3. 見出しタブを最新状態に即時完全再構築（updateHeadingTab: true）して保存
            SyncHeadings(null, updateHeadingTab: true);
            SaveCurrentPageData();
        }

        /// <summary>
        /// 見出しタブのテキストボックスがユーザーによって手動編集（行削除等）された際に呼び出され、
        /// 削除された見出しを即座に内部データ・解除リスト・ディスクおよび本文タブ（強調解除）へ反映します。
        /// </summary>
        public void OnHeadingTabTextChanged(RichTextBox hBox)
        {
            if (isSyncingHeadings || string.IsNullOrEmpty(currentPdfPath)) return;

            string headingText = hBox.Text;
            var headingsByPage = ParseItemsByPages(headingText);

            string pdfName = Path.GetFileNameWithoutExtension(currentPdfPath);
            string projectDir = OcrProcessor.FindOcrEngineDirectory();

            foreach (var pData in ocrPageDataList.ToList())
            {
                int pNum = pData.PageNumber;
                if (pData.Headings == null || pData.Headings.Count == 0) continue;

                // UIの見出しテキストに該当ページが含まれていない場合は、非表示バッチ等であるため削除とみなさない
                if (!headingsByPage.ContainsKey(pNum)) continue;

                var remaining = headingsByPage[pNum];
                var remainingClean = remaining.Select(h => DocxExporter.RemovePagePrefix(h)).ToHashSet(StringComparer.OrdinalIgnoreCase);

                var removedInPage = new List<string>();
                foreach (var oldH in pData.Headings.ToList())
                {
                    string cleanOld = DocxExporter.RemovePagePrefix(oldH);
                    bool isChOrSec = System.Text.RegularExpressions.Regex.IsMatch(cleanOld, @"^(?:序章|終章|第[0-9０-９一二三四五六七八九十百千万]+[章節編部])");
                    if (isChOrSec) continue; // 章や節は手動テキスト変更による誤消去から保護

                    if (!remainingClean.Contains(cleanOld))
                    {
                        removedInPage.Add(cleanOld);
                    }
                }

                if (removedInPage.Count > 0)
                {
                    if (pData.RemovedHeadings == null) pData.RemovedHeadings = new List<string>();
                    foreach (var rem in removedInPage)
                    {
                        if (!pData.RemovedHeadings.Contains(rem, StringComparer.OrdinalIgnoreCase))
                        {
                            pData.RemovedHeadings.Add(rem);
                        }
                        userRemovedHeadings.Add($"{pNum}::{rem}");
                        UnmarkHeadingInBodyRichTextBox(pNum, rem);
                    }
                    pData.Headings.RemoveAll(h => removedInPage.Contains(DocxExporter.RemovePagePrefix(h), StringComparer.OrdinalIgnoreCase));

                    if (!string.IsNullOrEmpty(pdfName))
                    {
                        string pageDir = Path.Combine(projectDir, "ocr_results", pdfName, $"page_{pNum:0000}");
                        if (Directory.Exists(pageDir))
                        {
                            OcrPageDataService.SavePageData(pageDir, pData);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 本文テキスト（各段落の小見出し行）から小見出しを直接抽出し、見出しタブおよび内部データ（ocrPageDataList / page_data.json）を最新化します。
        /// </summary>
        public void SyncHeadings(int? specificPage = null, bool updateHeadingTab = true)
        {
            isSyncingHeadings = true;
            try
            {
                string pdfName = string.IsNullOrEmpty(currentPdfPath) ? "" : Path.GetFileNameWithoutExtension(currentPdfPath);
                string projectDir = OcrProcessor.FindOcrEngineDirectory();
                int totalPdfPages = pdfDocument != null ? pdfDocument.PageCount : 1;

                if (!string.IsNullOrEmpty(pdfName))
                {
                    var diskPages = OcrPageDataService.LoadAllProjectPageData(projectDir, pdfName, totalPdfPages);
                    foreach (var dp in diskPages.ToList())
                    {
                        var existing = ocrPageDataList.FirstOrDefault(p => p.PageNumber == dp.PageNumber);
                        if (existing == null)
                        {
                            ocrPageDataList.Add(dp);
                        }
                        else if (dp.RemovedHeadings != null)
                        {
                            if (existing.RemovedHeadings == null) existing.RemovedHeadings = new List<string>();
                            foreach (var rh in dp.RemovedHeadings)
                            {
                                if (!existing.RemovedHeadings.Contains(rh, StringComparer.OrdinalIgnoreCase))
                                {
                                    existing.RemovedHeadings.Add(rh);
                                }
                            }
                        }
                        if (dp.RemovedHeadings != null)
                        {
                            foreach (var rh in dp.RemovedHeadings)
                            {
                                userRemovedHeadings.Add($"{dp.PageNumber}::{rh}");
                            }
                        }
                    }
                }

                string bodyText = ocrResultTextBoxes.TryGetValue("body", out var bBox) ? bBox.Text : "";
                var bodyByPage = ParseBodyTextByPages(bodyText);

                var targetPages = new HashSet<int>();
                if (specificPage.HasValue)
                {
                    targetPages.Add(specificPage.Value);
                }
                else
                {
                    targetPages.Add(currentPage + 1);
                    foreach (var k in bodyByPage.Keys) targetPages.Add(k);
                    foreach (var p in ocrPageDataList.ToList()) targetPages.Add(p.PageNumber);
                }

                // 本文タブのRichTextBoxから青色強調（小見出し）を取得
                Dictionary<int, HashSet<string>>? blueHeadingsByPage = null;
                if (ocrResultTextBoxes.TryGetValue("body", out var bodyBox) && bodyBox != null && bodyBox.TextLength > 0)
                {
                    blueHeadingsByPage = ExtractBlueHeadingsByPageFromRichTextBox(bodyBox, currentPage + 1);
                }

                foreach (int pNum in targetPages.ToList())
                {
                    var pageData = ocrPageDataList.FirstOrDefault(p => p.PageNumber == pNum);
                    if (pageData == null)
                    {
                        string pageDir = Path.Combine(projectDir, "ocr_results", pdfName, $"page_{pNum:0000}");
                        pageData = OcrPageDataService.LoadPageData(pageDir) ?? new OcrPageData { PageNumber = pNum };
                        ocrPageDataList.Add(pageData);
                    }

                    if (pageData.RemovedHeadings != null)
                    {
                        foreach (var rh in pageData.RemovedHeadings)
                        {
                            userRemovedHeadings.Add($"{pNum}::{rh}");
                        }
                    }

                    var pageHeadingList = new List<string>();       // 第1階層: 見出し領域（親見出し）
                    var pageSubheadingList = new List<string>();    // 第2階層: 本文小見出し（子見出し）

                    // 1. 既存の pageData.Headings および pageData.Subheadings を分類・保持
                    if (pageData.Headings != null)
                    {
                        foreach (var h in pageData.Headings.ToList())
                        {
                            string cleanH = DocxExporter.RemovePagePrefix(h);
                            if (DocxExporter.IsGarbageOrNoiseHeading(cleanH)) continue;

                            bool isSection = Regex.IsMatch(cleanH, @"^第[0-9０-９一二三四五六七八九十百千万]+節(?:[\s　・:：].*)?$");
                            bool isSub = isSection || OcrSorter.IsSubheadingText(cleanH);
                            if (isSub && !OcrSorter.IsMajorHeading(cleanH))
                            {
                                if (!pageSubheadingList.Contains(cleanH, StringComparer.OrdinalIgnoreCase))
                                    pageSubheadingList.Add(cleanH);
                            }
                            else
                            {
                                if (!pageHeadingList.Contains(cleanH, StringComparer.OrdinalIgnoreCase))
                                    pageHeadingList.Add(cleanH);
                            }
                        }
                    }

                    if (pageData.Subheadings != null)
                    {
                        foreach (var sh in pageData.Subheadings.ToList())
                        {
                            string cleanSH = DocxExporter.RemovePagePrefix(sh);
                            if (!string.IsNullOrWhiteSpace(cleanSH) && !DocxExporter.IsGarbageOrNoiseHeading(cleanSH))
                            {
                                if (!pageSubheadingList.Contains(cleanSH, StringComparer.OrdinalIgnoreCase))
                                    pageSubheadingList.Add(cleanSH);
                            }
                        }
                    }

                    // 2. 本文段落からの小見出し（第2階層）抽出
                    var parasToCheck = new List<string>();
                    if (bodyByPage.TryGetValue(pNum, out var paras) && paras.Count > 0)
                    {
                        parasToCheck = paras;
                    }
                    else if (pageData.BodyParagraphs != null && pageData.BodyParagraphs.Count > 0)
                    {
                        parasToCheck = pageData.BodyParagraphs;
                    }

                    HashSet<string>? blueSet = null;
                    blueHeadingsByPage?.TryGetValue(pNum, out blueSet);

                    foreach (var para in parasToCheck)
                    {
                        var lines = para.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        foreach (var line in lines)
                        {
                            string trimmed = line.Trim();
                            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 80 || trimmed.Contains("。") || Regex.IsMatch(trimmed, @"^【(?:注釈文|注)\d+】")) continue;

                            string cleanH = DocxExporter.RemovePagePrefix(trimmed);
                            if (DocxExporter.IsGarbageOrNoiseHeading(cleanH)) continue;

                            bool isSection = Regex.IsMatch(cleanH, @"^第[0-9０-９一二三四五六七八九十百千万]+節(?:[\s　・:：].*)?$");
                            bool isBlueInUI = blueSet != null && (blueSet.Contains(trimmed) || blueSet.Contains(cleanH));
                            bool isSub = isBlueInUI || (blueSet == null && appSettings.AutoDetectSubheadings && OcrSorter.IsSubheadingText(trimmed));

                            if (isSection)
                            {
                                pageData.RemovedHeadings?.RemoveAll(rh => rh.Equals(cleanH, StringComparison.OrdinalIgnoreCase) || DocxExporter.NormalizeForComparison(rh) == DocxExporter.NormalizeForComparison(cleanH));
                                userRemovedHeadings.Remove($"{pNum}::{cleanH}");
                                if (!pageSubheadingList.Contains(cleanH, StringComparer.OrdinalIgnoreCase))
                                    pageSubheadingList.Add(cleanH);
                            }
                            else if (isSub)
                            {
                                bool isRemoved = IsHeadingRemoved(pageData, pNum, cleanH);
                                if (isRemoved)
                                {
                                    UnmarkHeadingInBodyRichTextBox(pNum, cleanH);
                                    continue;
                                }
                                if (!pageSubheadingList.Contains(cleanH, StringComparer.OrdinalIgnoreCase))
                                    pageSubheadingList.Add(cleanH);
                            }
                        }
                    }

                    // 前方一致フラグメントの除去
                    pageHeadingList.RemoveAll(h1 => pageHeadingList.Any(h2 => h2.Length > h1.Length && h2.StartsWith(h1, StringComparison.OrdinalIgnoreCase)));
                    pageSubheadingList.RemoveAll(s1 => pageSubheadingList.Any(s2 => s2.Length > s1.Length && s2.StartsWith(s1, StringComparison.OrdinalIgnoreCase)));

                    pageData.Headings = pageHeadingList.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    pageData.Subheadings = pageSubheadingList.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }

                if (updateHeadingTab && ocrResultTextBoxes.TryGetValue("heading", out var currentHBox) && currentHBox != null)
                {
                    var sb = new StringBuilder();
                    var seenChapterKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pData in ocrPageDataList.ToList().OrderBy(p => p.PageNumber))
                    {
                        // 1. 第1階層: 見出し領域（親見出し） -> インデントなし
                        if (pData.Headings != null && pData.Headings.Count > 0)
                        {
                            var uniqueHeadings = pData.Headings
                                .Select(h => DocxExporter.RemovePagePrefix(h).Trim())
                                .Where(h => !string.IsNullOrWhiteSpace(h) && !DocxExporter.IsGarbageOrNoiseHeading(h))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();

                            uniqueHeadings.RemoveAll(h1 => uniqueHeadings.Any(h2 => h2.Length > h1.Length && h2.StartsWith(h1, StringComparison.OrdinalIgnoreCase)));

                            foreach (var cleanH in uniqueHeadings)
                            {
                                string? chKey = OcrSorter.ExtractMajorHeadingKey(cleanH);
                                if (chKey != null && seenChapterKeys.Contains(chKey))
                                {
                                    // 既に前のページで登場した章の重複（柱・ランニングヘッダー）は除外
                                    continue;
                                }
                                if (chKey != null) seenChapterKeys.Add(chKey);
                                sb.AppendLine($"[P{pData.PageNumber}] {cleanH}");
                            }
                        }

                        // 2. 第2階層: 本文小見出し（子見出し） -> 半角4文字分インデント
                        if (pData.Subheadings != null && pData.Subheadings.Count > 0)
                        {
                            var uniqueSubheadings = pData.Subheadings
                                .Select(sh => DocxExporter.RemovePagePrefix(sh).Trim())
                                .Where(sh => !string.IsNullOrWhiteSpace(sh) && !DocxExporter.IsGarbageOrNoiseHeading(sh))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();

                            uniqueSubheadings.RemoveAll(s1 => uniqueSubheadings.Any(s2 => s2.Length > s1.Length && s2.StartsWith(s1, StringComparison.OrdinalIgnoreCase)));

                            foreach (var cleanSH in uniqueSubheadings)
                            {
                                sb.AppendLine($"[P{pData.PageNumber}]     {cleanSH}");
                            }
                        }
                    }
                    string newText = sb.ToString();
                    if (currentHBox.Text != newText)
                    {
                        currentHBox.Text = newText;
                    }
                }
            }
            finally
            {
                isSyncingHeadings = false;
            }
        }

        /// <summary>
        /// 全ページの見出しを最新の本文編集内容と同期し、UIを更新・保存します。
        /// </summary>
        public void SyncHeadingsAndRefreshUi()
        {
            SyncHeadings(null, updateHeadingTab: true);
            SaveCurrentPageData();
            txtLog.AppendText($"【見出し同期】全ページの見出し一覧を本文の最新編集内容と同期しました。" + Environment.NewLine);
        }

        private static bool IsMajorChapterHeading(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            if (OcrSorter.IsMajorHeading(t))
            {
                return true;
            }
            if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^(?:(?:\d+|[IVXLCDM]+)\s+[A-Z]|Chapter\s+\d+|Conclusion|References|Bibliography|Contents)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return true;
            }
            return false;
        }

        public static bool IsBlueSubheadingColor(Color c)
        {
            // 注釈番号（濃紺: Color.FromArgb(30, 58, 138)）や注釈文（緑色）は小見出し色から除外
            if (c.ToArgb() == Color.FromArgb(30, 58, 138).ToArgb()) return false;
            if (c.G > 100 && c.G > c.B) return false; // 緑色系統

            // 鮮やかな青色（Tailwind Blue-600/700/800 や RoyalBlue, DodgerBlue, Blue）
            if (c.ToArgb() == Color.FromArgb(37, 99, 235).ToArgb() ||
                c.ToArgb() == Color.FromArgb(29, 78, 216).ToArgb() ||
                c.ToArgb() == Color.FromArgb(30, 64, 175).ToArgb() ||
                c.ToArgb() == Color.RoyalBlue.ToArgb() ||
                c.ToArgb() == Color.DodgerBlue.ToArgb() ||
                c.ToArgb() == Color.Blue.ToArgb())
            {
                return true;
            }

            // 青成分が強く、RやGより顕著に高い鮮明な青色（明度・彩度チェック）
            if (c.B >= 180 && c.B > c.R + 60 && c.B > c.G + 20) return true;

            return false;
        }

        public static Dictionary<int, HashSet<string>> ExtractBlueHeadingsByPageFromRichTextBox(RichTextBox? rtb, int fallbackPageNum = 1)
        {
            var result = new Dictionary<int, HashSet<string>>();
            if (rtb == null || rtb.TextLength == 0) return result;

            try
            {
                if (!rtb.IsHandleCreated)
                {
                    try { _ = rtb.Handle; } catch { }
                }

                int origStart = rtb.SelectionStart;
                int origLen = rtb.SelectionLength;

                string[] lines = rtb.Lines;
                int curPageNum = Math.Max(1, fallbackPageNum);

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string trimmed = line.Trim();

                    var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^---\s*ページ\s*(\d+)\s*---$");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int pNum))
                    {
                        curPageNum = pNum;
                    }
                    else if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        // 見出し行の妥当性検査: 80文字超や句点「。」を含む段落本文、注釈文タグは小見出しとして抽出しない
                        if (trimmed.Length > 80 || trimmed.Contains("。") ||
                            System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^【(?:注釈文|注)\d+】"))
                        {
                            continue;
                        }

                        int lineStart = rtb.GetFirstCharIndexFromLine(i);
                        if (lineStart >= 0 && line.Length > 0)
                        {
                            // 行頭の最初の文字が青色文字であるかを検査
                            int firstNonWs = 0;
                            while (firstNonWs < line.Length && char.IsWhiteSpace(line[firstNonWs])) firstNonWs++;

                            if (firstNonWs < line.Length && lineStart + firstNonWs < rtb.TextLength)
                            {
                                rtb.Select(lineStart + firstNonWs, 1);
                                if (IsBlueSubheadingColor(rtb.SelectionColor))
                                {
                                    if (!result.ContainsKey(curPageNum))
                                    {
                                        result[curPageNum] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                    }
                                    result[curPageNum].Add(trimmed);
                                }
                            }
                        }
                    }
                }

                rtb.Select(origStart, origLen);
            }
            catch { }

            return result;
        }

        public static HashSet<string> ExtractBlueHeadingsFromRichTextBox(RichTextBox? rtb)
        {
            var byPage = ExtractBlueHeadingsByPageFromRichTextBox(rtb);
            var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var set in byPage.Values)
            {
                foreach (var h in set) all.Add(h);
            }
            return all;
        }

        public static void AppendColoredBodyToTextBox(RichTextBox rtb, string text, List<string>? knownHeadings = null, bool autoDetectSubheadings = false)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (!rtb.IsHandleCreated)
            {
                try { _ = rtb.Handle; } catch { }
            }

            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            Font baseFont = rtb.Font ?? new Font("Yu Gothic UI", 11f);
            Font boldFont = new Font(baseFont, FontStyle.Bold);
            Font regularFont = new Font(baseFont, FontStyle.Regular);

            Color blueColor = Color.FromArgb(37, 99, 235); // 鮮やかな青色 (Tailwind Blue-600)
            Color greenColor = Color.FromArgb(21, 128, 61); // 鮮やかな緑色 (DarkGreen)
            Color navyColor = Color.FromArgb(30, 58, 138); // 濃紺 (注釈番号)
            Color grayColor = Color.FromArgb(100, 116, 139); // 灰色 (ページ区切り)
            Color normalColor = Color.FromArgb(31, 41, 55); // 通常本文

            for (int i = 0; i < lines.Length; i++)
            {
                string rawLine = lines[i];
                string trimmed = rawLine.Trim();

                if (trimmed.StartsWith("--- ページ") && trimmed.EndsWith("---"))
                {
                    int start = rtb.TextLength;
                    rtb.AppendText(rawLine + Environment.NewLine);
                    rtb.Select(start, rawLine.Length);
                    rtb.SelectionColor = grayColor;
                    rtb.SelectionFont = regularFont;
                    rtb.Select(rtb.TextLength, 0);
                    continue;
                }

                bool isSubheading = false;
                // 小見出しは80文字以下、句点「。」なし、かつ注釈文・注釈番号でないこと
                if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Length <= 80 && !trimmed.Contains("。") &&
                    !Regex.IsMatch(trimmed, @"^【(?:注釈文|注)\d+】"))
                {
                    if (knownHeadings != null)
                    {
                        if (knownHeadings.Any(h => !string.IsNullOrWhiteSpace(h) &&
                            (h.Trim().Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
                             DocxExporter.RemovePagePrefix(h.Trim()).Equals(trimmed, StringComparison.OrdinalIgnoreCase))))
                        {
                            isSubheading = true;
                        }
                    }
                    else if (autoDetectSubheadings && OcrSorter.IsSubheadingText(trimmed))
                    {
                        isSubheading = true;
                    }
                }

                if (isSubheading)
                {
                    // 小見出し（青色文字 ＆ 太字）
                    int start = rtb.TextLength;
                    rtb.AppendText(rawLine + Environment.NewLine);
                    rtb.Select(start, rawLine.Length);
                    rtb.SelectionColor = blueColor;
                    rtb.SelectionFont = boldFont;
                    rtb.Select(rtb.TextLength, 0);
                }
                else
                {
                    // 通常本文
                    if (trimmed.Contains("【注") || trimmed.Contains("【注釈文") || trimmed.Contains("[注"))
                    {
                        var parts = Regex.Split(rawLine, @"(【(?:注釈文|注)\d+】|\[(?:注釈文|注)?\d+\])");
                        foreach (var part in parts)
                        {
                            if (string.IsNullOrEmpty(part)) continue;
                            int pStart = rtb.TextLength;
                            rtb.AppendText(part);
                            rtb.Select(pStart, part.Length);

                            if (Regex.IsMatch(part, @"^【(?:注釈文)\d+】"))
                            {
                                rtb.SelectionColor = greenColor;
                                rtb.SelectionFont = boldFont;
                            }
                            else if (Regex.IsMatch(part, @"^(?:【注\d+】|\[(?:注釈文|注)?\d+\])$"))
                            {
                                rtb.SelectionColor = navyColor;
                                rtb.SelectionFont = boldFont;
                            }
                            else
                            {
                                rtb.SelectionColor = normalColor;
                                rtb.SelectionFont = regularFont;
                            }
                            rtb.Select(rtb.TextLength, 0);
                        }
                        rtb.AppendText(Environment.NewLine);
                    }
                    else
                    {
                        int start = rtb.TextLength;
                        rtb.AppendText(rawLine + Environment.NewLine);
                        rtb.Select(start, rawLine.Length);
                        rtb.SelectionColor = normalColor;
                        rtb.SelectionFont = regularFont;
                        rtb.Select(rtb.TextLength, 0);
                    }
                }
            }

            rtb.SelectionStart = rtb.TextLength;
            rtb.SelectionLength = 0;
        }

        public void SetupOcrResultContextMenu(RichTextBox rtb)
        {
            var menu = new ContextMenuStrip();
            menu.Opening += (s, e) =>
            {
                if (rtb.SelectionLength == 0)
                {
                    Point clientPt = rtb.PointToClient(Cursor.Position);
                    int charIdx = rtb.GetCharIndexFromPosition(clientPt);
                    if (charIdx >= 0 && charIdx < rtb.TextLength)
                    {
                        rtb.SelectionStart = charIdx;
                    }
                }
            };

            var mnuHeading = new ToolStripMenuItem("📑 小見出しに設定（青色文字）", null, (s, e) => btnAddHeading_Click(null, EventArgs.Empty));
            mnuHeading.ForeColor = Color.FromArgb(29, 78, 216); // Blue
            mnuHeading.Font = new Font(menu.Font, FontStyle.Bold);

            var mnuClearHeading = new ToolStripMenuItem("↩️ 小見出しを解除（通常文字）", null, (s, e) => RemoveHeadingFromSelection(rtb));

            var mnuSyncHeadings = new ToolStripMenuItem("🔄 見出し一覧を再同期・更新", null, (s, e) => SyncHeadingsAndRefreshUi());

            var mnuFootnote = new ToolStripMenuItem("📝 注釈文に設定（緑色文字）", null, (s, e) => btnAddFootnote_Click(null, EventArgs.Empty));
            mnuFootnote.ForeColor = Color.FromArgb(21, 128, 61); // Green

            var mnuNoteNum = new ToolStripMenuItem("🏷️ 注釈番号を挿入（【注N】）", null, (s, e) => btnAddAnnotationNumber_Click(null, EventArgs.Empty));

            var mnuSyncFootnotes = new ToolStripMenuItem("🔄 注釈番号を再採番・同期", null, (s, e) => SyncAndRenumberAllFootnotesUi());

            var mnuReloadBatch = new ToolStripMenuItem("🔄 ディスクから全データを再読み込み", null, (s, e) =>
            {
                int startP = (int)numPageStart.Value;
                int endP = (int)numPageEnd.Value;
                LoadBatchDataToUi(startP, endP);
                txtLog.AppendText($"【データ再読込】P.{startP}〜{endP} の全データをディスクから再読み込みしました。" + Environment.NewLine);
            });

            var mnuCopy = new ToolStripMenuItem("📋 コピー", null, (s, e) => { if (rtb.SelectionLength > 0) rtb.Copy(); });
            var mnuCut = new ToolStripMenuItem("✂️ 切り取り", null, (s, e) => { if (rtb.SelectionLength > 0) rtb.Cut(); });
            var mnuPaste = new ToolStripMenuItem("📄 貼り付け", null, (s, e) => { if (Clipboard.ContainsText()) rtb.Paste(); });
            var mnuSelectAll = new ToolStripMenuItem("🔘 すべて選択", null, (s, e) => rtb.SelectAll());

            var mnuSearch = new ToolStripMenuItem("🔍 バッチ全体を検索... (Ctrl+F)", null, (s, e) => OpenBatchSearchDialog());
            mnuSearch.Font = new Font(menu.Font, FontStyle.Bold);
            mnuSearch.ForeColor = Color.FromArgb(15, 23, 42);

            menu.Items.Add(mnuHeading);
            menu.Items.Add(mnuClearHeading);
            menu.Items.Add(mnuSyncHeadings);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(mnuFootnote);
            menu.Items.Add(mnuNoteNum);
            menu.Items.Add(mnuSyncFootnotes);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(mnuReloadBatch);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(mnuSearch);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(mnuCut);
            menu.Items.Add(mnuCopy);
            menu.Items.Add(mnuPaste);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(mnuSelectAll);

            rtb.ContextMenuStrip = menu;
        }

        private void btnAddFootnote_Click(object? sender, EventArgs e)
        {
            RichTextBox? resultBox = tabOcrResult?.SelectedTab?.Controls.OfType<RichTextBox>().FirstOrDefault()
                                  ?? (ocrResultTextBoxes.TryGetValue("body", out var bBox) ? bBox : null);

            if (resultBox == null) return;

            if (resultBox.SelectionLength == 0)
            {
                int currentLineIdx = resultBox.GetLineFromCharIndex(resultBox.SelectionStart);
                if (currentLineIdx >= 0 && currentLineIdx < resultBox.Lines.Length)
                {
                    int lineStart = resultBox.GetFirstCharIndexFromLine(currentLineIdx);
                    int lineLen = resultBox.Lines[currentLineIdx].Length;
                    resultBox.Select(lineStart, lineLen);
                }
            }

            string selectedText = resultBox.SelectedText.Trim();
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                MessageBox.Show("注釈文に設定する文字列を選択してください。",
                    "注釈文設定", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool isFootnoteTab = tabOcrResult?.SelectedTab?.Text.Contains("注釈") == true ||
                (ocrResultTextBoxes.TryGetValue("footnote", out var fnBox) && resultBox == fnBox);

            if (isFootnoteTab)
            {
                // 注釈文タブ内の選択範囲を結合して注釈番号タグを付与
                int startChar = resultBox.SelectionStart;
                int linePageNum = GetPageNumberAtSelection(resultBox, startChar);

                int selLen = resultBox.SelectionLength;
                int startLineIdx = resultBox.GetLineFromCharIndex(startChar);
                int endLineIdx = resultBox.GetLineFromCharIndex(startChar + Math.Max(0, selLen > 0 ? selLen - 1 : 0));

                var selectedLines = new List<string>();
                for (int i = startLineIdx; i <= endLineIdx && i < resultBox.Lines.Length; i++)
                {
                    string rawLine = resultBox.Lines[i].Trim();
                    if (!string.IsNullOrWhiteSpace(rawLine))
                    {
                        selectedLines.Add(rawLine);
                    }
                }

                if (selectedLines.Count > 0)
                {
                    var mergedList = OcrPageDataService.MergeFootnoteLines(selectedLines);
                    var pData = ocrPageDataList.FirstOrDefault(p => p.PageNumber == linePageNum);
                    if (pData != null)
                    {
                        if (pData.Footnotes == null) pData.Footnotes = new List<string>();
                        foreach (var m in mergedList)
                        {
                            string cleanText = Regex.Replace(m, @"^【(?:注釈文|注)(?:\d+|__NEW__)】\s*", "").Trim();
                            string item = $"【注釈文__NEW__】 {cleanText}";
                            pData.Footnotes.Add(item);
                        }
                    }
                }

                SaveCurrentPageData();
                SyncAndRenumberAllFootnotesUi();
                txtLog.AppendText($"【注釈文設定】注釈文タブの選択範囲を注釈文として結合・再採番しました。" + Environment.NewLine);
                return;
            }

            int targetPageNum = GetPageNumberAtSelection(resultBox, resultBox.SelectionStart);

            int unlinkedNum = FindFirstUnlinkedAnnotationNumberForCurrentPage();
            string fnTag = unlinkedNum > 0 ? $"【注釈文{unlinkedNum}】" : "【注釈文__NEW__】";
            string footnoteItem = $"{fnTag} {selectedText}";

            // 1. 本文（OCR結果画面）側で「【注釈文N】 <選択文章>」として設定
            int selStart = resultBox.SelectionStart;
            resultBox.SelectedText = footnoteItem;

            // 2. 注釈文タブにも登録
            if (ocrResultTextBoxes.TryGetValue("footnote", out var fBox))
            {
                AppendColoredFootnoteToTextBox(fBox, $"[P{targetPageNum}]", fnTag, selectedText);
            }

            // 3. 現在のページデータにも登録
            var pageData = ocrPageDataList.FirstOrDefault(p => p.PageNumber == targetPageNum);
            if (pageData != null)
            {
                if (pageData.Footnotes == null) pageData.Footnotes = new List<string>();
                if (!pageData.Footnotes.Contains(footnoteItem))
                {
                    pageData.Footnotes.Add(footnoteItem);
                }
            }

            SaveCurrentPageData();

            SyncAndRenumberAllFootnotesUi(preserveCaretInPage: targetPageNum, preferredCaretPos: selStart + fnTag.Length);
        }

        private static void AppendColoredFootnoteToTextBox(RichTextBox rtb, string prefix, string fnTag, string text)
        {
            // [P1] 等のページ番号プレフィックス
            rtb.SelectionStart = rtb.TextLength;
            rtb.SelectionLength = 0;
            rtb.SelectionColor = Color.FromArgb(100, 116, 139);
            rtb.SelectionFont = new Font(rtb.Font, FontStyle.Regular);
            rtb.AppendText(prefix + " ");

            // 【注釈文1】を緑色（DarkGreen / #15803D）かつ太字で表示
            rtb.SelectionStart = rtb.TextLength;
            rtb.SelectionLength = 0;
            rtb.SelectionColor = Color.FromArgb(21, 128, 61); // 鮮やかな緑色 (DarkGreen)
            rtb.SelectionFont = new Font(rtb.Font, FontStyle.Bold);
            rtb.AppendText(fnTag);

            // 注釈文テキスト
            rtb.SelectionStart = rtb.TextLength;
            rtb.SelectionLength = 0;
            rtb.SelectionColor = Color.FromArgb(31, 41, 55);
            rtb.SelectionFont = new Font(rtb.Font, FontStyle.Regular);
            rtb.AppendText(" " + text + Environment.NewLine);

            rtb.SelectionStart = rtb.TextLength;
            rtb.SelectionLength = 0;
        }

        private void btnAddAnnotationNumber_Click(object? sender, EventArgs e)
        {
            RichTextBox? resultBox = tabOcrResult?.SelectedTab?
                .Controls.OfType<RichTextBox>().FirstOrDefault()
                ?? (ocrResultTextBoxes.TryGetValue("body", out var bBox) ? bBox : null);

            if (resultBox == null)
            {
                MessageBox.Show("本文タブを選択してください。",
                    "注釈番号", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int targetPageNum = GetPageNumberAtSelection(resultBox, resultBox.SelectionStart);

            int insertPos = resultBox.SelectionStart;
            resultBox.Select(insertPos, resultBox.SelectionLength);
            resultBox.SelectedText = "【注__NEW__】";

            SaveCurrentPageData();

            // 全ページの注釈番号を自動で再採番・同期
            SyncAndRenumberAllFootnotesUi(preserveCaretInPage: targetPageNum, preferredCaretPos: insertPos + 4);
        }

        /// <summary>
        /// 全ページの注釈番号を設定（文書通し番号または大見出し単位）に従って再採番し、
        /// ディスクおよび現在表示中のUI（本文タブ・注釈文タブ）を同期更新します。
        /// </summary>
        public void SyncAndRenumberAllFootnotesUi(int? preserveCaretInPage = null, int? preferredCaretPos = null)
        {
            if (string.IsNullOrEmpty(currentPdfPath) || pdfDocument == null) return;

            SaveCurrentPageData();

            string pdfName = Path.GetFileNameWithoutExtension(currentPdfPath);
            string projectDir = OcrProcessor.FindOcrEngineDirectory();

            var normalizedPages = OcrPageDataService.NormalizeAndRenumberProjectFootnotes(
                projectDir, pdfName, pdfDocument.PageCount, appSettings);

            ocrPageDataList.Clear();
            ocrPageDataList.AddRange(normalizedPages);

            // 現在表示中のバッチUIを再読込
            int startP = (int)numPageStart.Value;
            int endP = (int)numPageEnd.Value;
            LoadBatchDataToUi(startP, endP);

            // スクロールおよびカーソル位置の復元
            ScrollOcrResultToPage(currentPage + 1);

            if (preserveCaretInPage.HasValue && preferredCaretPos.HasValue &&
                ocrResultTextBoxes.TryGetValue("body", out var bBox))
            {
                try
                {
                    int targetPos = Math.Clamp(preferredCaretPos.Value, 0, bBox.TextLength);
                    bBox.Select(targetPos, 0);
                    bBox.ScrollToCaret();
                }
                catch { }
            }

            string modeName = appSettings.FootnoteNumberingScope == "majorHeading" ? "大見出し単位" : "通し番号";
            txtLog.AppendText($"【注釈採番】全ページの注釈番号を「{modeName}」で整列・再採番しました。" + Environment.NewLine);
        }

        /// <summary>
        /// 現在のページで本文中に存在するが、注釈文タブ（または本文注釈行）で未登録の【注N】の最小番号を探索します。
        /// </summary>
        private int FindFirstUnlinkedAnnotationNumberForCurrentPage()
        {
            int pageNum = currentPage + 1;
            var bodyNotes = new List<int>();
            var footnoteNotes = new HashSet<int>();

            // 本文から【注N】を抽出
            if (ocrResultTextBoxes.TryGetValue("body", out var bBox))
            {
                var bodyByPage = ParseBodyTextByPages(bBox.Text);
                if (bodyByPage.TryGetValue(pageNum, out var paras))
                {
                    foreach (var para in paras)
                    {
                        var matches = Regex.Matches(para, @"【注(\d+)】");
                        foreach (Match m in matches)
                        {
                            if (int.TryParse(m.Groups[1].Value, out int n) && !bodyNotes.Contains(n))
                            {
                                bodyNotes.Add(n);
                            }
                        }
                    }
                }
            }

            // 注釈文タブおよび本文内の【注釈文N】を抽出
            if (ocrResultTextBoxes.TryGetValue("footnote", out var fBox))
            {
                var lines = fBox.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                string prefix = $"[P{pageNum}";
                foreach (var line in lines)
                {
                    if (line.StartsWith(prefix) || (!line.StartsWith("[P") && currentPage == 0))
                    {
                        var matches = Regex.Matches(line, @"【(?:注釈文|注)(\d+)】");
                        foreach (Match m in matches)
                        {
                            if (int.TryParse(m.Groups[1].Value, out int n))
                            {
                                footnoteNotes.Add(n);
                            }
                        }
                    }
                }
            }

            if (ocrResultTextBoxes.TryGetValue("body", out var bBox2))
            {
                var bodyByPage = ParseBodyTextByPages(bBox2.Text);
                if (bodyByPage.TryGetValue(pageNum, out var paras))
                {
                    foreach (var para in paras)
                    {
                        var matches = Regex.Matches(para, @"【注釈文(\d+)】");
                        foreach (Match m in matches)
                        {
                            if (int.TryParse(m.Groups[1].Value, out int n))
                            {
                                footnoteNotes.Add(n);
                            }
                        }
                    }
                }
            }

            var pData = ocrPageDataList.FirstOrDefault(p => p.PageNumber == pageNum);
            if (pData?.Footnotes != null)
            {
                foreach (var fn in pData.Footnotes)
                {
                    var matches = Regex.Matches(fn, @"【(?:注釈文|注)(\d+)】");
                    foreach (Match m in matches)
                    {
                        if (int.TryParse(m.Groups[1].Value, out int n))
                        {
                            footnoteNotes.Add(n);
                        }
                    }
                }
            }

            // 未リンクの【注N】があれば最初に見つかった番号を返す
            foreach (int bn in bodyNotes)
            {
                if (!footnoteNotes.Contains(bn))
                {
                    return bn;
                }
            }

            return -1;
        }
    }
}
