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

            for (int pIdx = 0; pIdx < pages.Count; pIdx++)
            {
                var page = pages[pIdx];

                // 2ページ目以降の先頭にページ区切りを挿入
                if (pIdx > 0)
                {
                    body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                }

                // 1. 見出し（「見出し」という人工タイトルは出力せず、見出しテキストのみ出力）
                foreach (var hText in page.Headings)
                {
                    if (string.IsNullOrWhiteSpace(hText)) continue;
                    string cleanHeading = RemovePagePrefix(hText);
                    if (string.IsNullOrWhiteSpace(cleanHeading)) continue;

                    var p = new Paragraph(
                        new ParagraphProperties(
                            new SpacingBetweenLines { Before = "180", After = "100" }),
                        new Run(
                            new RunProperties(
                                CreateRunFonts(fontFamily),
                                new FontSize { Val = (baseHalfPoints + 4).ToString() },
                                new FontSizeComplexScript { Val = (baseHalfPoints + 4).ToString() },
                                new Bold(),
                                new WColor { Val = "1A365D" }),
                            new Text(cleanHeading) { Space = SpaceProcessingModeValues.Preserve }));
                    body.AppendChild(p);
                }

                // 2. 本文（「本文」や「--- ページ 1 ---」は出力せず、段落のみ出力）
                foreach (var paraText in page.BodyParagraphs)
                {
                    if (string.IsNullOrWhiteSpace(paraText)) continue;
                    string cleanPara = RemovePageDivider(paraText);
                    if (string.IsNullOrWhiteSpace(cleanPara)) continue;

                    var p = new Paragraph(
                        new ParagraphProperties(
                            new Indentation { FirstLine = "420" }, // 1文字インデント
                            new SpacingBetweenLines { Line = "360", LineRule = LineSpacingRuleValues.Auto, After = "160" }));

                    string[] lines = cleanPara.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var runProps = new RunProperties(
                            CreateRunFonts(fontFamily),
                            new FontSize { Val = baseHalfPoints.ToString() },
                            new FontSizeComplexScript { Val = baseHalfPoints.ToString() });

                        if (settings.FontBold)
                        {
                            runProps.AppendChild(new Bold());
                        }

                        var run = new Run(
                            runProps,
                            new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });

                        p.AppendChild(run);
                        if (i < lines.Length - 1)
                        {
                            p.AppendChild(new Run(new Break()));
                        }
                    }

                    body.AppendChild(p);
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

                        var p = new Paragraph(
                            pPr,
                            new Run(
                                new RunProperties(
                                    CreateRunFonts(fontFamily),
                                    new FontSize { Val = Math.Max(16, baseHalfPoints - 3).ToString() },
                                    new FontSizeComplexScript { Val = Math.Max(16, baseHalfPoints - 3).ToString() },
                                    new WColor { Val = "4A5568" }),
                                new Text(cleanFn) { Space = SpaceProcessingModeValues.Preserve }));
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
                            string cellText = !string.IsNullOrEmpty(span.MergedText)
                                ? span.MergedText
                                : (c < sRow.Cells.Count ? sRow.Cells[c] : "");

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

        private static string RemovePagePrefix(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string cleaned = Regex.Replace(text, @"^\[P\d+-\d+\]\s*", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"^\[P\d+\]\s*", "", RegexOptions.IgnoreCase);
            return cleaned.Trim();
        }

        private static string RemovePageDivider(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(l => !Regex.IsMatch(l.Trim(), @"^---\s*ページ\s*\d+\s*---$"))
                .ToList();
            return string.Join(Environment.NewLine, lines).Trim();
        }
    }
}
