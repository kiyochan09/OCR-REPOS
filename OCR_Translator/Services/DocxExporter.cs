using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OCR_Translator.Models;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using WColor = DocumentFormat.OpenXml.Wordprocessing.Color;

namespace OCR_Translator.Services
{
    /// <summary>
    /// 各ページの抽出データ（見出し、本文、表、図、注釈）を
    /// アプリ基本設定で指定されたフォント・サイズを統一適用し、
    /// 人工的な見出し（「本文」「表」「図」等）を含めず、自然な文書フローで
    /// ネイティブWord文書（.docx）として出力するエクスポーター
    /// </summary>
    public static class DocxExporter
    {
        public static void ExportToDocxFile(
            string filePath,
            List<OcrPageData> pages,
            AppSettings settings)
        {
            pages = NormalizeAndRenumberPages(pages, settings);

            // ページ跨ぎの文章・段落を自動結合
            if (settings.MergeCrossPageParagraphs)
            {
                pages = MergeCrossPageParagraphs(pages, settings);
            }

            if (File.Exists(filePath))
            {
                try { File.Delete(filePath); } catch { }
            }

            using WordprocessingDocument wordDoc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
            MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            // アプリ基本設定のフォントおよびサイズを反映
            string fontFamily = string.IsNullOrWhiteSpace(settings.FontFamilyName) ? "Yu Gothic UI" : settings.FontFamilyName;
            int baseHalfPoints = (int)Math.Round(settings.FontSize * 2.0);
            if (baseHalfPoints <= 0) baseHalfPoints = 22; // 11pt

            // ドキュメント既定スタイル・フォントの登録
            AddDefaultStyle(mainPart, fontFamily, baseHalfPoints);

            // 全ページの全本文段落・全本文行を収集（見出しが本文中に存在するかを文書全体で厳密判定）
            var allBodyLinesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allBodyNormalizedLines = new List<string>();
            var allBodyTextList = new List<string>();
            foreach (var p in pages)
            {
                if (p.BodyParagraphs != null)
                {
                    foreach (var bp in p.BodyParagraphs)
                    {
                        if (string.IsNullOrWhiteSpace(bp)) continue;
                        allBodyTextList.Add(bp);
                        string[] lines = bp.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var l in lines)
                        {
                            string t = l.Trim();
                            if (!string.IsNullOrEmpty(t))
                            {
                                allBodyLinesSet.Add(t);
                                allBodyNormalizedLines.Add(NormalizeForComparison(t));
                            }
                        }
                    }
                }
            }

            string combinedAllBodyNormalized = NormalizeForComparison(string.Join(" ", allBodyTextList));
            var emittedStandaloneHeadings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int pIdx = 0; pIdx < pages.Count; pIdx++)
            {
                var page = pages[pIdx];

                // 改ページ設定が有効な場合のみ2ページ目以降の先頭にページ区切りを挿入
                if (settings.InsertPageBreakOnWordExport && pIdx > 0)
                {
                    body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                }

                // 1. 本文外の独立見出し領域の出力
                // 本文（全ページ）中に存在する見出しは本文フロー内で出力するため完全スキップ
                if (page.Headings != null)
                {
                    foreach (var hText in page.Headings)
                    {
                        if (string.IsNullOrWhiteSpace(hText)) continue;
                        string cleanHeading = RemovePagePrefix(hText);
                        if (string.IsNullOrWhiteSpace(cleanHeading)) continue;

                        string normClean = NormalizeForComparison(cleanHeading);
                        if (string.IsNullOrEmpty(normClean)) continue;

                        // 同一ページの本文段落内に、独立した行として既に存在するかのみを判定（存在すれば本文フローで出力）
                        bool isPresentInThisPageBody = page.BodyParagraphs != null && page.BodyParagraphs.Any(bp =>
                        {
                            if (string.IsNullOrWhiteSpace(bp)) return false;
                            var lines = bp.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                            return lines.Any(l =>
                            {
                                string t = l.Trim();
                                return t.Equals(cleanHeading, StringComparison.OrdinalIgnoreCase) ||
                                       NormalizeForComparison(t) == normClean;
                            });
                        });

                        if (isPresentInThisPageBody) continue;

                        // 文書全体で既に先頭出力された独立見出しは2度と出力しない（重複完全防止）
                        if (!emittedStandaloneHeadings.Add(cleanHeading) || !emittedStandaloneHeadings.Add(normClean)) continue;

                        bool isMajor = OcrSorter.IsMajorHeading(cleanHeading);
                        bool isSub = !isMajor && OcrSorter.IsSubheadingText(cleanHeading);
                        int headingHalfPts = isSub ? (baseHalfPoints + 1) : (baseHalfPoints + 4);
                        string headingColor = isSub ? "1E293B" : "1A365D";

                        var p = new Paragraph(
                            new ParagraphProperties(
                                new SpacingBetweenLines { Before = "180", After = isSub ? "80" : "100" }),
                            new Run(
                                new RunProperties(
                                    CreateRunFonts(fontFamily),
                                    new FontSize { Val = headingHalfPts.ToString() },
                                    new FontSizeComplexScript { Val = headingHalfPts.ToString() },
                                    new Bold(),
                                    new WColor { Val = headingColor }),
                                new Text(cleanHeading) { Space = SpaceProcessingModeValues.Preserve }));
                        body.AppendChild(p);
                    }
                }

                // 2. 本文（「本文」や「--- ページ 1 ---」は出力せず、注釈文（【注釈文N】）も本文から除外して注釈欄へ移動）
                bool isWesternDoc = (settings.DocumentType == "western");

                if (page.BodyParagraphs != null)
                {
                    foreach (var paraText in page.BodyParagraphs)
                    {
                        if (string.IsNullOrWhiteSpace(paraText)) continue;
                        string cleanPara = RemovePageDivider(paraText);
                        if (string.IsNullOrWhiteSpace(cleanPara)) continue;

                        var bodyLines = new List<string>();
                        string[] rawLines = cleanPara.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                        foreach (var rawLine in rawLines)
                        {
                            string trimmed = rawLine.Trim();
                            // 【注釈文1】などの注釈文行が本文段落に存在する場合、本文から除外して注釈リストへ登録
                            if (Regex.IsMatch(trimmed, @"^【(?:注釈文|注)\d+】"))
                            {
                                if (page.Footnotes == null) page.Footnotes = new List<string>();
                                if (!page.Footnotes.Contains(trimmed))
                                {
                                    page.Footnotes.Add(trimmed);
                                }
                            }
                            else if (!string.IsNullOrWhiteSpace(trimmed))
                            {
                                bodyLines.Add(rawLine);
                            }
                        }

                    if (bodyLines.Count == 0) continue;

                    // 段落内の各行を走査し、小見出し行と本文行を厳密に分離して出力
                    var currentBodyLines = new List<string>();

                    void FlushBodyParagraph()
                    {
                        if (currentBodyLines.Count == 0) return;

                        bool isParagraphWestern = isWesternDoc || IsMainlyWesternText(string.Join(" ", currentBodyLines));
                        string paragraphCombinedText;
                        if (isParagraphWestern)
                        {
                            // 英文段落: 単語間の半角スペース結合 ＆ 行末ハイフネーション復元
                            var sb = new System.Text.StringBuilder();
                            foreach (var rawLine in currentBodyLines)
                            {
                                string trimmed = rawLine.Trim();
                                if (string.IsNullOrEmpty(trimmed)) continue;

                                if (sb.Length == 0)
                                {
                                    sb.Append(trimmed);
                                }
                                else
                                {
                                    if (sb.ToString().EndsWith("-"))
                                    {
                                        sb.Length--; // 行末ハイフン削除して単語結合 (e.g. sustain- + able -> sustainable)
                                        sb.Append(trimmed);
                                    }
                                    else
                                    {
                                        sb.Append(" ");
                                        sb.Append(trimmed);
                                    }
                                }
                            }
                            paragraphCombinedText = sb.ToString();
                        }
                        else
                        {
                            // 和文段落: 全行を自然に結合
                            paragraphCombinedText = string.Join("", currentBodyLines.Select(l => l.Trim()));
                        }

                        if (!string.IsNullOrWhiteSpace(paragraphCombinedText))
                        {
                            var pPr = new ParagraphProperties(
                                new SpacingBetweenLines { Line = "360", LineRule = LineSpacingRuleValues.Auto, After = "160" });

                            if (!isParagraphWestern)
                            {
                                pPr.Indentation = new Indentation { FirstLine = "420" }; // 和文1文字字下げ
                            }

                            var p = new Paragraph(pPr);
                            AppendFormattedBodyLine(p, paragraphCombinedText, fontFamily, baseHalfPoints, settings.FontBold);
                            body.AppendChild(p);
                        }

                        currentBodyLines.Clear();
                    }

                    foreach (var line in bodyLines)
                    {
                        string trimmed = line.Trim();
                        string normLine = NormalizeForComparison(trimmed);
                        bool isRemoved = page.RemovedHeadings != null && page.RemovedHeadings.Any(rh =>
                            !string.IsNullOrWhiteSpace(rh) && (
                                rh.Trim().Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
                                trimmed.StartsWith(rh.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrEmpty(normLine) && NormalizeForComparison(RemovePagePrefix(rh)) == normLine)));

                        bool isSub = false;
                        if (!isRemoved && trimmed.Length <= 80 && !trimmed.Contains("。") &&
                            !Regex.IsMatch(trimmed, @"^【(?:注釈文|注)\d+】"))
                        {
                            if (page.Headings != null)
                            {
                                isSub = page.Headings.Any(h => !string.IsNullOrWhiteSpace(h) && (
                                           h.Trim().Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
                                           trimmed.Equals(RemovePagePrefix(h), StringComparison.OrdinalIgnoreCase) ||
                                           (!string.IsNullOrEmpty(normLine) && NormalizeForComparison(RemovePagePrefix(h)) == normLine)));
                            }
                            else if (settings.AutoDetectSubheadings)
                            {
                                isSub = OcrSorter.IsSubheadingText(trimmed);
                            }
                        }

                        if (isSub)
                        {
                            // 1. 先行する本文行があれば独立した本文段落としてフラッシュ
                            FlushBodyParagraph();

                            // 2. 小見出しを独立した太字・字下げなしの単独行段落として出力
                            var pPr = new ParagraphProperties(
                                new SpacingBetweenLines { Before = "180", After = "80" });
                            var p = new Paragraph(pPr);
                            var runProps = new RunProperties(
                                CreateRunFonts(fontFamily),
                                new FontSize { Val = (baseHalfPoints + 1).ToString() },
                                new FontSizeComplexScript { Val = (baseHalfPoints + 1).ToString() },
                                new Bold(),
                                new WColor { Val = "1E293B" });
                            p.AppendChild(new Run(runProps, new Text(trimmed) { Space = SpaceProcessingModeValues.Preserve }));
                            body.AppendChild(p);
                        }
                        else
                        {
                            currentBodyLines.Add(line);
                        }
                    }

                    // 最後に残った本文行をフラッシュ
                    FlushBodyParagraph();
                }
            }

                // 3. 表（本文の直後に配置。表名（表1, 表2...）を明示して配置）
                foreach (var tbl in page.Tables)
                {
                    if (tbl.Rows.Count == 0) continue;

                    string displayTableName = string.IsNullOrWhiteSpace(tbl.TableName) ? "表" : tbl.TableName;
                    var tableTitlePara = new Paragraph(
                        new ParagraphProperties(
                            new SpacingBetweenLines { Before = "180", After = "80" }),
                        new Run(
                            new RunProperties(
                                CreateRunFonts(fontFamily),
                                new FontSize { Val = (baseHalfPoints + 1).ToString() },
                                new FontSizeComplexScript { Val = (baseHalfPoints + 1).ToString() },
                                new Bold(),
                                new WColor { Val = "1E293B" }),
                            new Text($"◆ {displayTableName}") { Space = SpaceProcessingModeValues.Preserve }));
                    body.AppendChild(tableTitlePara);

                    InsertStructuredTableToDocx(body, tbl, fontFamily, baseHalfPoints);
                }

                // 4. 図（本文・表の直後に配置。「図」というタイトルは出力せず、画像とキャプションのみ配置）
                foreach (var fig in page.Figures)
                {
                    if (fig.ImageBytes != null && fig.ImageBytes.Length > 0)
                    {
                        InsertImageToBody(mainPart, body, fig, fontFamily, baseHalfPoints);
                    }
                }

                // 5. 注釈（ページの最後に配置）
                if (page.Footnotes.Count > 0)
                {
                    bool firstFn = true;
                    foreach (var fnText in page.Footnotes)
                    {
                        if (string.IsNullOrWhiteSpace(fnText)) continue;
                        string cleanFn = RemovePagePrefix(fnText);
                        if (string.IsNullOrWhiteSpace(cleanFn)) continue;

                        var pPr = new ParagraphProperties(
                            firstFn
                                ? new SpacingBetweenLines { Before = "200", After = "60" }
                                : new SpacingBetweenLines { After = "60" });
                        if (firstFn)
                        {
                            pPr.ParagraphBorders = new ParagraphBorders(new TopBorder { Val = BorderValues.Single, Size = 6, Color = "CBD5E0" });
                            firstFn = false;
                        }

                        var p = new Paragraph(pPr);
                        var match = Regex.Match(cleanFn, @"^(【(?:注釈文|注)\d+】|\[(?:注釈文|注)?\d+\])\s*(.*)$");
                        int fnHalfPoints = Math.Max(16, baseHalfPoints - 3);

                        if (match.Success)
                        {
                            string noteNumTag = match.Groups[1].Value;
                            // タグ表記を「【注釈文N】」に統一
                            noteNumTag = Regex.Replace(noteNumTag, @"^【注(\d+)】", "【注釈文$1】");
                            string noteContent = match.Groups[2].Value;

                            // 注釈文ラベル部分（太字・緑色 #15803D）
                            var numRunProps = new RunProperties(
                                CreateRunFonts(fontFamily),
                                new FontSize { Val = fnHalfPoints.ToString() },
                                new FontSizeComplexScript { Val = fnHalfPoints.ToString() },
                                new Bold(),
                                new WColor { Val = "15803D" }); // 鮮やかな緑色
                            p.AppendChild(new Run(numRunProps, new Text(noteNumTag + " ") { Space = SpaceProcessingModeValues.Preserve }));

                            // 注釈内容
                            var textRunProps = new RunProperties(
                                CreateRunFonts(fontFamily),
                                new FontSize { Val = fnHalfPoints.ToString() },
                                new FontSizeComplexScript { Val = fnHalfPoints.ToString() },
                                new WColor { Val = "374151" });
                            p.AppendChild(new Run(textRunProps, new Text(noteContent) { Space = SpaceProcessingModeValues.Preserve }));
                        }
                        else
                        {
                            var textRunProps = new RunProperties(
                                CreateRunFonts(fontFamily),
                                new FontSize { Val = fnHalfPoints.ToString() },
                                new FontSizeComplexScript { Val = fnHalfPoints.ToString() },
                                new WColor { Val = "15803D" });
                            p.AppendChild(new Run(textRunProps, new Text(cleanFn) { Space = SpaceProcessingModeValues.Preserve }));
                        }

                        body.AppendChild(p);
                    }
                }
            }

            mainPart.Document.Save();
        }

        private static void AddDefaultStyle(MainDocumentPart mainPart, string fontFamily, int baseHalfPoints)
        {
            StyleDefinitionsPart stylePart = mainPart.AddNewPart<StyleDefinitionsPart>();
            Styles styles = new Styles();

            DocDefaults docDefaults = new DocDefaults(
                new RunPropertiesDefault(
                    new RunPropertiesBaseStyle(
                        CreateRunFonts(fontFamily),
                        new FontSize { Val = baseHalfPoints.ToString() },
                        new FontSizeComplexScript { Val = baseHalfPoints.ToString() }
                    )
                ),
                new ParagraphPropertiesDefault()
            );
            styles.AppendChild(docDefaults);

            Style normalStyle = new Style
            {
                Type = StyleValues.Paragraph,
                StyleId = "Normal",
                Default = true,
                CustomStyle = false
            };
            normalStyle.AppendChild(new StyleName { Val = "Normal" });
            normalStyle.AppendChild(new StyleRunProperties(
                CreateRunFonts(fontFamily),
                new FontSize { Val = baseHalfPoints.ToString() },
                new FontSizeComplexScript { Val = baseHalfPoints.ToString() }
            ));
            styles.AppendChild(normalStyle);

            stylePart.Styles = styles;
            stylePart.Styles.Save();
        }

        /// <summary>
        /// 単一文字列から OcrPageData を組み立てて出力する互換用オーバーロード
        /// </summary>
        public static void ExportToDocxFile(
            string filePath,
            string bodyText,
            string headingText,
            string footnoteText,
            DataGridView? dgv,
            List<TableMergeSpan> mergeSpans,
            AppSettings settings,
            List<FigureItem>? figures = null)
        {
            var pages = ConvertToPageDataList(bodyText, headingText, footnoteText, dgv, mergeSpans, figures);
            ExportToDocxFile(filePath, pages, settings);
        }

        private static List<OcrPageData> ConvertToPageDataList(
            string bodyText,
            string headingText,
            string footnoteText,
            DataGridView? dgv,
            List<TableMergeSpan> mergeSpans,
            List<FigureItem>? figures)
        {
            var pageData = new OcrPageData { PageNumber = 1 };

            if (!string.IsNullOrWhiteSpace(headingText))
            {
                foreach (var line in headingText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    pageData.Headings.Add(line);
                }
            }

            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                foreach (var para in bodyText.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    pageData.BodyParagraphs.Add(para);
                }
            }

            if (dgv != null && dgv.RowCount > 0 && dgv.ColumnCount > 0)
            {
                var extracted = TableCellMerger.ExtractTablesFromDataGridView(dgv, mergeSpans);
                pageData.Tables.AddRange(extracted);
            }

            if (figures != null && figures.Count > 0)
            {
                pageData.Figures.AddRange(figures);
            }

            if (!string.IsNullOrWhiteSpace(footnoteText))
            {
                foreach (var line in footnoteText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    pageData.Footnotes.Add(line);
                }
            }

            return new List<OcrPageData> { pageData };
        }

        private static void InsertStructuredTableToDocx(
            Body body,
            StructuredTable tableData,
            string fontFamily,
            int baseHalfPoints)
        {
            if (tableData.Rows.Count == 0) return;

            int colCount = tableData.ColumnCount > 0
                ? tableData.ColumnCount
                : tableData.Rows.Max(r => r.Cells.Count);
            if (colCount <= 0) colCount = 1;

            Table table = new Table();

            // 表プロパティ
            TableProperties tblPr = new TableProperties(
                new TableJustification { Val = TableRowAlignmentValues.Center },
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 6, Color = "444444" },
                    new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "444444" },
                    new LeftBorder { Val = BorderValues.Single, Size = 6, Color = "444444" },
                    new RightBorder { Val = BorderValues.Single, Size = 6, Color = "444444" },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" }
                )
            );
            table.AppendChild(tblPr);

            // TableGrid (OpenXML必須要素)
            TableGrid tblGrid = new TableGrid();
            int colWidthDxa = Math.Max(720, 8500 / colCount);
            for (int c = 0; c < colCount; c++)
            {
                tblGrid.AppendChild(new GridColumn { Width = colWidthDxa.ToString() });
            }
            table.AppendChild(tblGrid);

            int cellFontSize = Math.Max(16, baseHalfPoints - 2);

            // データ行
            for (int r = 0; r < tableData.Rows.Count; r++)
            {
                var sRow = tableData.Rows[r];
                TableRow tr = new TableRow(new TableRowProperties(new CantSplit()));

                for (int c = 0; c < colCount; c++)
                {
                    var span = tableData.MergeSpans.FirstOrDefault(s => s.Contains(c, r));

                    if (span != null)
                    {
                        // 水平結合で先頭列以外のセルはスキップ
                        if (c > span.StartCol) continue;

                        TableCell tc = new TableCell();
                        TableCellProperties tcp = new TableCellProperties(
                            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                            new TableCellMargin(
                                new TopMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                                new BottomMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                                new LeftMargin { Width = "140", Type = TableWidthUnitValues.Dxa },
                                new RightMargin { Width = "140", Type = TableWidthUnitValues.Dxa }
                            )
                        );

                        if (span.ColSpan > 1)
                        {
                            tcp.AppendChild(new GridSpan { Val = span.ColSpan });
                        }

                        if (span.RowSpan > 1)
                        {
                            if (r == span.StartRow)
                                tcp.AppendChild(new VerticalMerge { Val = MergedCellValues.Restart });
                            else
                                tcp.AppendChild(new VerticalMerge { Val = MergedCellValues.Continue });
                        }

                        tc.AppendChild(tcp);

                        if (r == span.StartRow)
                        {
                            string cellVal = c < sRow.Cells.Count ? sRow.Cells[c] : "";
                            string cellText = !string.IsNullOrWhiteSpace(cellVal)
                                ? cellVal
                                : (!string.IsNullOrEmpty(span.MergedText) ? span.MergedText : "");

                            var p = new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Left }));
                            string[] lines = cellText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                            for (int li = 0; li < lines.Length; li++)
                            {
                                p.AppendChild(new Run(
                                    new RunProperties(
                                        CreateRunFonts(fontFamily),
                                        new FontSize { Val = cellFontSize.ToString() },
                                        new FontSizeComplexScript { Val = cellFontSize.ToString() }),
                                    new Text(lines[li]) { Space = SpaceProcessingModeValues.Preserve }));
                                if (li < lines.Length - 1)
                                    p.AppendChild(new Run(new Break()));
                            }
                            tc.AppendChild(p);
                        }
                        else
                        {
                            tc.AppendChild(new Paragraph());
                        }

                        tr.AppendChild(tc);
                    }
                    else
                    {
                        string cellText = c < sRow.Cells.Count ? sRow.Cells[c] : "";
                        TableCell tc = new TableCell(
                            new TableCellProperties(
                                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                                new TableCellMargin(
                                    new TopMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                                    new BottomMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                                    new LeftMargin { Width = "140", Type = TableWidthUnitValues.Dxa },
                                    new RightMargin { Width = "140", Type = TableWidthUnitValues.Dxa }
                                )
                            ),
                            new Paragraph(
                                new ParagraphProperties(new Justification { Val = JustificationValues.Left }),
                                new Run(
                                    new RunProperties(
                                        CreateRunFonts(fontFamily),
                                        new FontSize { Val = cellFontSize.ToString() },
                                        new FontSizeComplexScript { Val = cellFontSize.ToString() }),
                                    new Text(cellText) { Space = SpaceProcessingModeValues.Preserve }))
                        );
                        tr.AppendChild(tc);
                    }
                }
                table.AppendChild(tr);
            }

            body.AppendChild(table);

            // テーブル後の余白
            body.AppendChild(new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "180" })));
        }

        private static void InsertImageToBody(MainDocumentPart mainPart, Body body, FigureItem fig, string fontFamily, int baseHalfPoints)
        {
            var partType = fig.MimeType == "image/png" ? ImagePartType.Png : ImagePartType.Jpeg;
            ImagePart imagePart = mainPart.AddImagePart(partType);
            using (var ms = new MemoryStream(fig.ImageBytes))
            {
                imagePart.FeedData(ms);
            }
            string relationshipId = mainPart.GetIdOfPart(imagePart);

            long maxWidthEmu = 5029200L;
            long maxHeightEmu = 5943600L;

            int imgW = fig.Bounds.Width > 0 ? fig.Bounds.Width : 600;
            int imgH = fig.Bounds.Height > 0 ? fig.Bounds.Height : 400;

            long widthEmu = (long)imgW * 9525L;
            long heightEmu = (long)imgH * 9525L;

            if (widthEmu > maxWidthEmu)
            {
                double scale = (double)maxWidthEmu / widthEmu;
                widthEmu = maxWidthEmu;
                heightEmu = (long)(heightEmu * scale);
            }
            if (heightEmu > maxHeightEmu)
            {
                double scale = (double)maxHeightEmu / heightEmu;
                heightEmu = maxHeightEmu;
                widthEmu = (long)(widthEmu * scale);
            }

            var inline = new DW.Inline(
                new DW.Extent() { Cx = widthEmu, Cy = heightEmu },
                new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties() { Id = (UInt32Value)1U, Name = fig.Name },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties() { Id = (UInt32Value)0U, Name = fig.Name },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip() { Embed = relationshipId, CompressionState = A.BlipCompressionValues.Print },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset() { X = 0L, Y = 0L },
                                    new A.Extents() { Cx = widthEmu, Cy = heightEmu }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                    ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                )
            );
            inline.DistanceFromTop = (UInt32Value)0U;
            inline.DistanceFromBottom = (UInt32Value)0U;
            inline.DistanceFromLeft = (UInt32Value)0U;
            inline.DistanceFromRight = (UInt32Value)0U;

            var element = new Drawing(inline);

            var imgPara = new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new SpacingBetweenLines { Before = "120", After = "60" }),
                new Run(element));
            body.AppendChild(imgPara);

            // キャプション
            if (!string.IsNullOrWhiteSpace(fig.Name))
            {
                int captionFontSize = Math.Max(16, baseHalfPoints - 2);
                var captionPara = new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Center },
                        new SpacingBetweenLines { After = "240" }),
                    new Run(
                        new RunProperties(
                            CreateRunFonts(fontFamily),
                            new FontSize { Val = captionFontSize.ToString() },
                            new FontSizeComplexScript { Val = captionFontSize.ToString() },
                            new Bold(),
                            new WColor { Val = "334155" }),
                        new Text(fig.Name) { Space = SpaceProcessingModeValues.Preserve }));
                body.AppendChild(captionPara);
            }
        }

        private static void AppendFormattedBodyLine(Paragraph p, string lineText, string fontFamily, int baseHalfPoints, bool isBold)
        {
            if (string.IsNullOrEmpty(lineText)) return;

            // 注釈番号タグ（【注1】や【注釈文1】、[注1]等）を正規表現で分離
            var parts = Regex.Split(lineText, @"(【(?:注釈文|注)\d+】|\[(?:注釈文|注)?\d+\])");
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                bool isNoteTag = Regex.IsMatch(part, @"^(?:【(?:注釈文|注)\d+】|\[(?:注釈文|注)?\d+\])$");
                if (isNoteTag)
                {
                    // 上付き（Superscript）の注釈番号
                    var noteRunProps = new RunProperties(
                        CreateRunFonts(fontFamily),
                        new FontSize { Val = Math.Max(16, baseHalfPoints - 2).ToString() },
                        new FontSizeComplexScript { Val = Math.Max(16, baseHalfPoints - 2).ToString() },
                        new VerticalTextAlignment { Val = VerticalPositionValues.Superscript },
                        new Bold(),
                        new WColor { Val = "1E3A8A" }
                    );
                    p.AppendChild(new Run(noteRunProps, new Text(part) { Space = SpaceProcessingModeValues.Preserve }));
                }
                else
                {
                    var normalRunProps = new RunProperties(
                        CreateRunFonts(fontFamily),
                        new FontSize { Val = baseHalfPoints.ToString() },
                        new FontSizeComplexScript { Val = baseHalfPoints.ToString() }
                    );
                    if (isBold)
                    {
                        normalRunProps.AppendChild(new Bold());
                    }
                    p.AppendChild(new Run(normalRunProps, new Text(part) { Space = SpaceProcessingModeValues.Preserve }));
                }
            }
        }

        private static RunFonts CreateRunFonts(string fontFamily)
        {
            return new RunFonts
            {
                Ascii = fontFamily,
                EastAsia = fontFamily,
                HighAnsi = fontFamily,
                ComplexScript = fontFamily,
                Hint = FontTypeHintValues.EastAsia
            };
        }

        public static string RemovePagePrefix(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string cleaned = text.Trim();
            while (true)
            {
                string next = Regex.Replace(cleaned, @"^\[P\d+(?:-\d+)?\]\s*", "", RegexOptions.IgnoreCase).Trim();
                if (next == cleaned) break;
                cleaned = next;
            }
            return cleaned;
        }

        /// <summary>
        /// OCR誤認識による記号連続行やノイズ、文途切れ行を見出し対象外と判定します。
        /// </summary>
        public static bool IsGarbageOrNoiseHeading(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            string clean = RemovePagePrefix(text.Trim());

            // 1. 〇, ○, ●, ―, ─, — 等の記号が3個以上含まれるノイズ（例: 一、〇、〇〇〇...）
            if (clean.Count(c => c == '〇' || c == '○' || c == '●' || c == '―' || c == '─' || c == '—' || c == '■' || c == '▲' || c == '▼' || c == '・') >= 3)
            {
                return true;
            }

            // 2. 同一文字が4文字以上連続している
            if (Regex.IsMatch(clean, @"(.)\1{3,}"))
            {
                return true;
            }

            // 3. 接続詞や文頭表現による文途切れ行の除外
            if (Regex.IsMatch(clean, @"^(?:これに対して|それに対して|したがって|そのため|しかしながら|また[、,]|その一方で|このように|前述したように)"))
            {
                return true;
            }

            return false;
        }

        public static string NormalizeForComparison(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            // 空白・改行・記号を除去し小文字化して比較用文字列を作成
            return Regex.Replace(text, @"[\s\p{P}\p{S}]", "").ToLowerInvariant();
        }

        public static string RemovePageDivider(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(l => !Regex.IsMatch(l.Trim(), @"^---\s*ページ\s*\d+\s*---$"))
                .ToList();
            return string.Join(Environment.NewLine, lines).Trim();
        }
        /// <summary>
        /// 全ページにわたり、注釈番号（【注N】）および注釈文（【注釈文N】）の採番（文書通し番号または大見出し単位）を適用します。
        /// また、図番号（図1, 図2...）や表番号（表1, 表2...）も文書全体で綺麗に通番化します。
        /// </summary>
        public static List<OcrPageData> NormalizeAndRenumberPages(List<OcrPageData> sourcePages, AppSettings? settings = null)
        {
            if (sourcePages == null || sourcePages.Count == 0) return new List<OcrPageData>();

            // 全ページの注釈番号を共通サービスで再採番
            OcrPageDataService.RenumberAllFootnotes(sourcePages, settings);

            int globalTableCounter = 1;
            int globalFigureCounter = 1;

            var normalizedPages = new List<OcrPageData>();

            foreach (var page in sourcePages)
            {
                var filteredHeadings = new List<string>();
                if (page.Headings != null)
                {
                    var seenH = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var h in page.Headings)
                    {
                        if (string.IsNullOrWhiteSpace(h)) continue;
                        var match = Regex.Match(h, @"^\[P(\d+)(?:-\d+)?\]");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int explicitP))
                        {
                            if (explicitP != page.PageNumber) continue;
                        }
                        string clean = RemovePagePrefix(h);
                        if (!string.IsNullOrWhiteSpace(clean) && seenH.Add(clean))
                        {
                            filteredHeadings.Add(clean);
                        }
                    }
                }

                var newPage = new OcrPageData
                {
                    PageNumber = page.PageNumber,
                    Headings = filteredHeadings,
                    BodyParagraphs = page.BodyParagraphs != null ? new List<string>(page.BodyParagraphs) : new List<string>(),
                    Tables = new List<StructuredTable>(),
                    Figures = new List<FigureItem>(),
                    Footnotes = page.Footnotes != null ? new List<string>(page.Footnotes) : new List<string>()
                };

                // 4. 表の通番化
                if (page.Tables != null)
                {
                    foreach (var tbl in page.Tables)
                    {
                        var newTbl = new StructuredTable
                        {
                            PageNumber = tbl.PageNumber,
                            TableName = $"表{globalTableCounter++}",
                            ColumnCount = tbl.ColumnCount,
                            RowCount = tbl.RowCount,
                            Rows = tbl.Rows,
                            MergeSpans = tbl.MergeSpans
                        };
                        newPage.Tables.Add(newTbl);
                    }
                }

                // 5. 図の通番化
                if (page.Figures != null)
                {
                    foreach (var fig in page.Figures)
                    {
                        var newFig = new FigureItem
                        {
                            PageNumber = fig.PageNumber,
                            Name = $"図{globalFigureCounter++}",
                            Bounds = fig.Bounds,
                            ImageBytes = fig.ImageBytes,
                            MimeType = fig.MimeType,
                            Image = fig.Image
                        };
                        newPage.Figures.Add(newFig);
                    }
                }

                normalizedPages.Add(newPage);
            }

            return normalizedPages;
        }

        private static bool IsMainlyWesternText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            int latinCount = text.Count(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));
            int cjkCount = text.Count(c => c >= 0x3000 && c <= 0x9FFF);
            return latinCount > cjkCount;
        }

        /// <summary>
        /// ページ境界をまたぐ文章・段落を判定し、前ページの末尾段落と次ページの先頭段落を自動結合します。
        /// </summary>
        public static List<OcrPageData> MergeCrossPageParagraphs(List<OcrPageData> sourcePages, AppSettings settings)
        {
            if (sourcePages == null || sourcePages.Count <= 1)
                return sourcePages ?? new List<OcrPageData>();

            int lastNonEmptyPageIdx = -1;

            for (int i = 0; i < sourcePages.Count; i++)
            {
                var page = sourcePages[i];
                if (page.BodyParagraphs == null || page.BodyParagraphs.Count == 0)
                    continue;

                if (lastNonEmptyPageIdx >= 0)
                {
                    var prevPage = sourcePages[lastNonEmptyPageIdx];
                    if (prevPage.BodyParagraphs != null && prevPage.BodyParagraphs.Count > 0)
                    {
                        string lastPara = prevPage.BodyParagraphs.Last();
                        string firstPara = page.BodyParagraphs.First();

                        bool isWestern = (settings.DocumentType == "western" || IsMainlyWesternText(lastPara) || IsMainlyWesternText(firstPara));

                        // 次ページの先頭段落が見出しであるかを検査（先頭行が見出しリストに含まれているか、または小見出しである場合はマージしない）
                        bool isFirstParaHeading = false;
                        string firstLine = firstPara.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(firstLine))
                        {
                            string normFirst = NormalizeForComparison(firstLine);
                            bool isRemoved = page.RemovedHeadings != null && page.RemovedHeadings.Any(rh =>
                                !string.IsNullOrWhiteSpace(rh) && (
                                    rh.Trim().Equals(firstLine, StringComparison.OrdinalIgnoreCase) ||
                                    firstLine.StartsWith(rh.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                    (!string.IsNullOrEmpty(normFirst) && NormalizeForComparison(RemovePagePrefix(rh)) == normFirst)));

                            if (!isRemoved)
                            {
                                if (page.Headings != null)
                                {
                                    if (page.Headings.Any(h => !string.IsNullOrWhiteSpace(h) && (
                                        h.Trim().Equals(firstLine, StringComparison.OrdinalIgnoreCase) ||
                                        firstLine.StartsWith(h.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                        firstLine.Equals(RemovePagePrefix(h), StringComparison.OrdinalIgnoreCase) ||
                                        firstLine.StartsWith(RemovePagePrefix(h), StringComparison.OrdinalIgnoreCase) ||
                                        (!string.IsNullOrEmpty(normFirst) && NormalizeForComparison(RemovePagePrefix(h)) == normFirst))))
                                    {
                                        isFirstParaHeading = true;
                                    }
                                }
                                else if (settings.AutoDetectSubheadings && OcrSorter.IsSubheadingText(firstLine))
                                {
                                    isFirstParaHeading = true;
                                }
                            }
                        }

                        if (!isFirstParaHeading && ShouldMergeCrossPage(lastPara, firstPara, isWestern))
                        {
                            string mergedPara = lastPara.TrimEnd() + Environment.NewLine + firstPara.TrimStart();
                            prevPage.BodyParagraphs[prevPage.BodyParagraphs.Count - 1] = mergedPara;
                            page.BodyParagraphs.RemoveAt(0);
                        }
                    }
                }

                if (page.BodyParagraphs != null && page.BodyParagraphs.Count > 0)
                {
                    lastNonEmptyPageIdx = i;
                }
            }

            return sourcePages;
        }

        private static bool ShouldMergeCrossPage(string lastPara, string firstPara, bool isWestern)
        {
            if (string.IsNullOrWhiteSpace(lastPara) || string.IsNullOrWhiteSpace(firstPara))
                return false;

            string trimmedLast = lastPara.Trim();
            string trimmedFirst = firstPara.Trim();

            if (string.IsNullOrEmpty(trimmedLast) || string.IsNullOrEmpty(trimmedFirst))
                return false;

            // 先頭行・末尾行を取り出して検査
            string firstLineOfFirstPara = trimmedFirst.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
            string lastLineOfLastPara = trimmedLast.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() ?? "";

            if (string.IsNullOrEmpty(firstLineOfFirstPara) || string.IsNullOrEmpty(lastLineOfLastPara))
                return false;

            // 小見出し（Subheading）の場合は前後の段落と結合しない
            if (OcrSorter.IsSubheadingText(firstLineOfFirstPara) || OcrSorter.IsSubheadingText(lastLineOfLastPara) ||
                OcrSorter.IsSubheadingText(trimmedFirst) || OcrSorter.IsSubheadingText(trimmedLast))
                return false;

            // 箇条書き・リスト項目の開始記号（- 項目, ・, ■, ◆, 1., (1) 等）で始まる場合は結合しない
            if (Regex.IsMatch(trimmedFirst, @"^(?:[-*・■◆▲●○▶※]\s+|\d+[\.\)]|\(\d+\)|[①-⑳])") ||
                Regex.IsMatch(firstLineOfFirstPara, @"^(?:[-*・■◆▲●○▶※]\s+|\d+[\.\)]|\(\d+\)|[①-⑳])"))
                return false;

            // 引用符・独立括弧で始まる段落は結合しない（ただし引用内継続の小文字等は除く）
            if (trimmedFirst.StartsWith("「") || trimmedFirst.StartsWith("『") || trimmedFirst.StartsWith("“") || trimmedFirst.StartsWith("\"") ||
                trimmedFirst.StartsWith("（") || trimmedFirst.StartsWith("【") || trimmedFirst.StartsWith("［") || trimmedFirst.StartsWith("["))
            {
                // 欧文で前ページが引用符開き中の場合を除く
                if (!trimmedLast.EndsWith("“") && !trimmedLast.EndsWith("\"") && !trimmedLast.EndsWith("「") && !trimmedLast.EndsWith("『"))
                    return false;
            }

            // 前ページ末尾がコロン「:」「：」で終わる場合（引用導入文・リスト導入文等）は結合しない
            if (trimmedLast.EndsWith(":") || trimmedLast.EndsWith("：") || lastLineOfLastPara.EndsWith(":") || lastLineOfLastPara.EndsWith("："))
                return false;

            if (isWestern)
            {
                // 洋書・英文の判定ルール

                // 1. 行末ハイフンで終わっている場合 (e.g. "sustain-" + "able") -> 100% 結合
                if (trimmedLast.EndsWith("-") || lastLineOfLastPara.EndsWith("-"))
                    return true;

                // 2. 次ページの先頭が小文字英字で始まる場合 (e.g. "focusing...", "creation...", "with...", "rebels...") -> 100% 結合
                // ※OCRのブレで先頭に半角スペースが入っていても trimmedFirst は小文字で始まる
                if (char.IsLower(trimmedFirst[0]))
                    return true;

                // 3. 次ページの先頭が文の継続記号（コンマ、セミコロン、閉じ括弧等）で始まる場合
                if (trimmedFirst.StartsWith(",") || trimmedFirst.StartsWith(";") || trimmedFirst.StartsWith(")") || trimmedFirst.StartsWith("]"))
                    return true;

                // 4. 前ページ末尾が文末約物 (. ! ? ." !" ?" 等) で終わっていない場合 (e.g. "... rebels by the principal")
                bool endsWithSentencePunct = Regex.IsMatch(trimmedLast, @"[\.\!\?][\""\'\)]*$");
                if (!endsWithSentencePunct)
                {
                    // 前ページ末尾がピリオド等なしで途切れている場合、次ページが英字・数字・記号で始まれば結合
                    return true;
                }

                return false;
            }
            else
            {
                // 和書の判定ルール

                // 1. 次ページの先頭が和文継続文字・小書き文字・助詞・句読点等で始まる場合
                if (Regex.IsMatch(trimmedFirst, @"^[、っッゃゅょャュョぁぃぅぇぉァィゥェォゎヮヶー〜…てにをはがでとのへからよりたりたるただばならないますです]"))
                    return true;

                // 2. 前ページ末尾が読点・接続記号（、, ・ ― — … 等）で終わっている場合
                if (Regex.IsMatch(trimmedLast, @"[、,・―—…]\s*$"))
                    return true;

                // 3. 先頭に全角スペース（和文段落字下げ）がある場合は新規段落として結合しない
                if (firstPara.StartsWith("　") || firstLineOfFirstPara.StartsWith("　"))
                    return false;

                // 4. 前ページ末尾が文末約物 (。 . ！ ？ ! ? 」 』 ） ) 等) で終わっていない場合
                bool endsWithSentencePunct = OcrSorter.EndsWithSentencePunctuation(trimmedLast);
                if (!endsWithSentencePunct)
                    return true;

                return false;
            }
        }
    }
}
