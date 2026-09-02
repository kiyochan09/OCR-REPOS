using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OCR_Translator.Forms;
using OCR_Translator.Models;
using OCR_Translator.Services;
using ResizeMode = OCR_Translator.Services.ImageCoordinateHelper.ResizeMode;

namespace OCR_Translator
{
    public partial class Form1 : Form
    {
        private PdfDocument? pdfDocument;
        private int currentPage = 0;
        private string? currentPdfPath;

        private AppSettings appSettings = new AppSettings();

        private DataGridView? dgvOcrTable;
        private readonly List<TableMergeSpan> tableMergeSpans = new();
        private readonly List<FigureItem> extractedFigures = new();
        private readonly List<OcrPageData> ocrPageDataList = new();
        private TabControl? tabOcrResult;
        private TabPage? tabOcrText;
        private TabPage? tabOcrTable;
        private TabPage? tabOcrImage;
        private FlowLayoutPanel? pnlFigureGallery;
        private readonly Dictionary<string, RichTextBox> ocrResultTextBoxes = new();
        private readonly LayoutStorage _layoutStorage = new LayoutStorage();

        private RichTextBox txtLog = null!;

        private List<OcrRegion> regions = new List<OcrRegion>();
        private Dictionary<int, List<OcrRegion>> pageRegions = new();
        private Dictionary<int, List<OcrRegion>> autoPageRegions = new();

        private bool isDrawingRegion = false;
        private Point regionStartPoint;
        private Rectangle regionPreviewRectangle;

        private int movingRegionIndex = -1;
        private Point moveStartPoint;
        private Rectangle moveOriginalRectangle;

        private int hoverRegionIndex = -1;
        private ResizeMode hoverResizeMode = ResizeMode.None;
        private ResizeMode resizeMode = ResizeMode.None;
        private Rectangle resizeOriginalRectangle;
        private Point resizeStartPoint;

        private const int ResizeHandleSize = 8;
        private bool isUpdatingNumericValues = false;
        private int nextAnnotationNumber = 1;

        // 表の罫線編集用コントロールおよび状態
        private Button btnTableAddHLine = null!;
        private Button btnTableAddVLine = null!;
        private Button btnTableDeleteLine = null!;
        private Button btnTableClearLines = null!;
        private Label lblTableLineStatus = null!;

        private ImageCoordinateHelper.RuleLineType activeLineAddMode = ImageCoordinateHelper.RuleLineType.None;
        private bool isLineDeleteMode = false;

        // 罫線操作・複数選択状態
        private readonly HashSet<int> selectedRuleLineIndices = new();
        private int selectedRuleLineIndex => selectedRuleLineIndices.Count > 0 ? selectedRuleLineIndices.First() : -1;

        private enum RuleLineDragMode
        {
            None,
            MoveLine,      // 罫線本体移動（Ctrlキーで一括複製）
            AdjustStart,   // 端点1 (Start) の長さ一括変更
            AdjustEnd      // 端点2 (End) の長さ一括変更
        }

        private RuleLineDragMode ruleLineDragMode = RuleLineDragMode.None;
        private int draggingRuleLineIndex = -1;
        private int draggingRuleRegionIndex = -1;
        private bool isCtrlCopyDragging = false;
        private Point dragStartImagePoint;
        private readonly Dictionary<int, TableRuleLine> dragInitialRuleLines = new();

        // ホバー状態
        private int hoveringRuleLineIndex = -1;
        private int hoveringRuleRegionIndex = -1;
        private ImageCoordinateHelper.RuleLineHitPart hoveringRuleLinePart = ImageCoordinateHelper.RuleLineHitPart.None;

        public Form1()
        {
            InitializeComponent();
            appSettings = SettingsManager.LoadSettings();

            InitializeLogView();
            InitializeOcrResultView();
            InitializeTableRuleLineControls();
            ApplySettingsToViews();

            lblOrientationBadge.Click += btnOptions_Click;
            lblDocTypeBadge.Click += btnOptions_Click;

            numX.ValueChanged += (s, e) => ApplyNumericBoundsToSelectedRegion();
            numY.ValueChanged += (s, e) => ApplyNumericBoundsToSelectedRegion();
            numWidth.ValueChanged += (s, e) => ApplyNumericBoundsToSelectedRegion();
            numHeight.ValueChanged += (s, e) => ApplyNumericBoundsToSelectedRegion();

            btnAutoLayout.Click -= btnAutoLayout_Click;
            btnAutoLayout.Click += btnAutoLayout_Click;

            btnExportWord.Click += btnExportWord_Click;

            pictureBox1.Resize += (s, e) => pictureBox1.Invalidate();
        }

        public static string GetRegionTypeName(string type)
        {
            return type switch
            {
                "body" => "本文",
                "heading" => "見出し",
                "footnote" => "注釈文",
                "table" => "表",
                "image" => "図",
                _ => "本文"
            };
        }

        private void ChangeSelectedRegionType(string newType, string newName)
        {
            int index = lstRegions.SelectedIndex;
            if (index < 0 || index >= regions.Count) return;

            OcrRegion region = regions[index];
            region.Type = newType;
            region.Name = newName;
            if (newType == "table")
            {
                region.EnsureRuleLines();
            }

            lstRegions.Items[index] = newName;
            pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
            _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
            UpdateTableLineControlsState();
            pictureBox1.Invalidate();

            if (newType == "image")
            {
                ExtractCurrentPageFigures();
            }
        }

        private void CreateNewRegionWithBounds(Rectangle imageRect, string type, string name)
        {
            OcrRegion region = new OcrRegion
            {
                Name = name,
                Type = type,
                X = imageRect.X,
                Y = imageRect.Y,
                Width = imageRect.Width,
                Height = imageRect.Height
            };

            if (type == "table")
            {
                region.EnsureRuleLines();
            }

            regions.Add(region);
            int index = lstRegions.Items.Add(region.Name);
            lstRegions.SelectedIndex = index;

            isUpdatingNumericValues = true;
            try
            {
                numX.Value = Math.Min(numX.Maximum, Math.Max(numX.Minimum, region.X));
                numY.Value = Math.Min(numY.Maximum, Math.Max(numY.Minimum, region.Y));
                numWidth.Value = Math.Min(numWidth.Maximum, Math.Max(numWidth.Minimum, region.Width));
                numHeight.Value = Math.Min(numHeight.Maximum, Math.Max(numHeight.Minimum, region.Height));
            }
            finally
            {
                isUpdatingNumericValues = false;
            }

            pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
            _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
            UpdateTableLineControlsState();
            pictureBox1.Invalidate();

            if (type == "image")
            {
                ExtractCurrentPageFigures();
            }
        }

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
            const int dpi = 150;

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
                    using Image pageImg = pdfDocument.Render(pIdx, dpi, dpi, PdfRenderFlags.Annotations);
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

        private void InitializeLogView()
        {
            txtLog = new RichTextBox
            {
                Dock = DockStyle.Bottom,
                Height = 140,
                ReadOnly = true,
                BackColor = SystemColors.Window,
                Font = new Font("Consolas", 9F),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            Controls.Add(txtLog);
        }

        private void ShowCurrentPage()
        {
            if (pdfDocument == null) return;
            if (currentPage < 0 || currentPage >= pdfDocument.PageCount) return;

            try
            {
                const int dpi = 150;
                using Image image = pdfDocument.Render(
                    currentPage, dpi, dpi, PdfRenderFlags.Annotations);

                Bitmap displayBitmap = new Bitmap(image);
                Image? oldImage = pictureBox1.Image;
                pictureBox1.Image = displayBitmap;
                oldImage?.Dispose();

                UpdatePageDisplayTitle();
                pictureBox1.Invalidate();
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

        private void UpdatePageDisplayTitle()
        {
            if (pdfDocument == null) return;
            Text = $"OCR Translator - {currentPage + 1}/{pdfDocument.PageCount}";
        }

        private void SaveCurrentPageRegions()
        {
            if (pdfDocument == null) return;
            _layoutStorage.TrySaveCurrentPageRegions(
                currentPage, regions, pageRegions, autoPageRegions);
        }

        private void LoadCurrentPageRegions()
        {
            regions.Clear();
            regions.AddRange(
                _layoutStorage.LoadPageRegions(
                    currentPage, pageRegions, autoPageRegions));
            RefreshRegionList();
        }

        private void SwitchToPage(int pageIndex)
        {
            if (pdfDocument == null) return;
            if (pageIndex < 0 || pageIndex >= pdfDocument.PageCount) return;

            SaveCurrentPageRegions();
            currentPage = pageIndex;
            LoadCurrentPageRegions();
            ShowCurrentPage();
        }

        private void RefreshRegionList()
        {
            lstRegions.Items.Clear();
            foreach (OcrRegion region in regions)
                lstRegions.Items.Add(region.Name);

            if (regions.Count > 0)
                lstRegions.SelectedIndex = 0;

            pictureBox1.Invalidate();
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
                LoadCurrentPageRegions();
                ShowCurrentPage();
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

        private void btnNextPage_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null) return;
            if (currentPage < pdfDocument.PageCount - 1)
                SwitchToPage(currentPage + 1);
        }

        private void btnPrevPage_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null) return;
            if (currentPage > 0)
                SwitchToPage(currentPage - 1);
        }

        private void btnDeleteRegion_Click(object sender, EventArgs e)
        {
            int index = lstRegions.SelectedIndex;
            if (index < 0 || index >= regions.Count)
            {
                MessageBox.Show("削除する領域を選択してください。", "領域未選択",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            regions.RemoveAt(index);
            lstRegions.Items.RemoveAt(index);
            pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
            lstRegions.ClearSelected();

            isUpdatingNumericValues = true;
            try
            {
                numX.Value = 0;
                numY.Value = 0;
                numWidth.Value = 0;
                numHeight.Value = 0;
            }
            finally
            {
                isUpdatingNumericValues = false;
            }

            UpdateTableLineControlsState();
            pictureBox1.Invalidate();
        }

        private void ApplyNumericBoundsToSelectedRegion()
        {
            if (isUpdatingNumericValues) return;
            int index = lstRegions.SelectedIndex;
            if (index < 0 || index >= regions.Count) return;

            OcrRegion region = regions[index];
            region.X = (int)numX.Value;
            region.Y = (int)numY.Value;
            region.Width = (int)numWidth.Value;
            region.Height = (int)numHeight.Value;

            pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
            _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
            pictureBox1.Invalidate();
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

        private void lstRegions_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = lstRegions.SelectedIndex;
            if (index < 0 || index >= regions.Count || regions[index].Type != "table")
            {
                selectedRuleLineIndices.Clear();
            }
            else
            {
                selectedRuleLineIndices.RemoveWhere(idx => idx >= regions[index].RuleLines.Count);
            }

            UpdateTableLineControlsState();
            if (index < 0 || index >= regions.Count) return;

            OcrRegion region = regions[index];
            isUpdatingNumericValues = true;
            try
            {
                numX.Value = Math.Min(numX.Maximum, Math.Max(numX.Minimum, region.X));
                numY.Value = Math.Min(numY.Maximum, Math.Max(numY.Minimum, region.Y));
                numWidth.Value = Math.Min(numWidth.Maximum, Math.Max(numWidth.Minimum, region.Width));
                numHeight.Value = Math.Min(numHeight.Maximum, Math.Max(numHeight.Minimum, region.Height));
            }
            finally
            {
                isUpdatingNumericValues = false;
            }
        }

        private void ShowRegionContextMenu(Point screenLocation, OcrRegion region)
        {
            var menu = new ContextMenuStrip();

            var titleItem = new ToolStripMenuItem($"【{region.Name}】 区分を変更")
            {
                Enabled = false,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };
            menu.Items.Add(titleItem);
            menu.Items.Add(new ToolStripSeparator());

            var types = new (string type, string name)[]
            {
                ("body", "本文"),
                ("heading", "見出し"),
                ("table", "表"),
                ("footnote", "注釈文"),
                ("image", "図")
            };

            foreach (var (t, n) in types)
            {
                var item = new ToolStripMenuItem(n)
                {
                    Checked = (region.Type == t)
                };
                string targetType = t;
                string targetName = n;
                item.Click += (s, ev) => ChangeSelectedRegionType(targetType, targetName);
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());
            var delItem = new ToolStripMenuItem("🗑 この領域を削除")
            {
                ForeColor = Color.DarkRed
            };
            delItem.Click += (s, ev) => btnDeleteRegion_Click(this, EventArgs.Empty);
            menu.Items.Add(delItem);

            menu.Show(pictureBox1, screenLocation);
        }

        private void ShowEmptyCanvasContextMenu(Point screenLocation, Point imgPoint)
        {
            var menu = new ContextMenuStrip();
            var titleItem = new ToolStripMenuItem("新規領域を作成 (クリック位置):")
            {
                Enabled = false,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };
            menu.Items.Add(titleItem);
            menu.Items.Add(new ToolStripSeparator());

            int w = 250;
            int h = 100;
            int x = Math.Max(0, imgPoint.X - w / 2);
            int y = Math.Max(0, imgPoint.Y - h / 2);
            if (pictureBox1.Image != null)
            {
                w = Math.Min(w, pictureBox1.Image.Width - x);
                h = Math.Min(h, pictureBox1.Image.Height - y);
            }
            Rectangle newRect = new Rectangle(x, y, w, h);

            var types = new (string type, string name)[]
            {
                ("body", "＋ 本文 領域を作成"),
                ("heading", "＋ 見出し 領域を作成"),
                ("table", "＋ 表 領域を作成"),
                ("footnote", "＋ 注釈文 領域を作成"),
                ("image", "＋ 図 領域を作成")
            };

            foreach (var (t, title) in types)
            {
                string targetType = t;
                string regionName = GetRegionTypeName(t);
                var item = new ToolStripMenuItem(title);
                item.Click += (s, ev) => CreateNewRegionWithBounds(newRect, targetType, regionName);
                menu.Items.Add(item);
            }

            menu.Show(pictureBox1, screenLocation);
        }

        private void btnRegionSettings_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null || pictureBox1.Image == null)
            {
                MessageBox.Show("先にPDFを開いてください。", "領域設定",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            isDrawingRegion = true;
            regionPreviewRectangle = Rectangle.Empty;
            pictureBox1.Focus();
            pictureBox1.Cursor = Cursors.Cross;
            Cursor = Cursors.Cross;
            pictureBox1.Invalidate();
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (pictureBox1.Image == null) return;

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
                    region.EnsureRuleLines();

                    for (int lineIdx = 0; lineIdx < region.RuleLines.Count; lineIdx++)
                    {
                        var line = region.RuleLines[lineIdx];
                        bool isLineSelected = isRegionSelected && selectedRuleLineIndices.Contains(lineIdx);
                        bool isLineHovered = (hoveringRuleRegionIndex == i && hoveringRuleLineIndex == lineIdx);
                        bool isLineDragging = (draggingRuleRegionIndex == i && (draggingRuleLineIndex == lineIdx || (ruleLineDragMode != RuleLineDragMode.None && selectedRuleLineIndices.Contains(lineIdx))));

                        Point p1Img = line.IsVertical ? new Point(line.Pos, line.Start) : new Point(line.Start, line.Pos);
                        Point p2Img = line.IsVertical ? new Point(line.Pos, line.End) : new Point(line.End, line.Pos);

                        Point p1 = ImageCoordinateHelper.ImageToScreenPoint(p1Img, pictureBox1);
                        Point p2 = ImageCoordinateHelper.ImageToScreenPoint(p2Img, pictureBox1);

                        Color lineColor;
                        float lineThick;
                        System.Drawing.Drawing2D.DashStyle dashStyle = System.Drawing.Drawing2D.DashStyle.Solid;

                        if (isLineSelected || isLineDragging)
                        {
                            lineColor = Color.Gold;
                            lineThick = 3f;
                        }
                        else if (isLineHovered)
                        {
                            lineColor = Color.Yellow;
                            lineThick = 2.5f;
                        }
                        else if (isRegionSelected)
                        {
                            lineColor = Color.Cyan;
                            lineThick = 1.8f;
                        }
                        else
                        {
                            lineColor = Color.DeepSkyBlue;
                            lineThick = 1.2f;
                            dashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        }

                        using Pen linePen = new Pen(lineColor, lineThick) { DashStyle = dashStyle };
                        e.Graphics.DrawLine(linePen, p1, p2);

                        // 端点ハンドルの描画
                        if (isLineSelected || isLineHovered || isLineDragging)
                        {
                            int hs = 8;
                            using Brush hBrush = new SolidBrush(Color.White);
                            using Pen hPen = new Pen(Color.Red, 1.5f);

                            Rectangle r1 = new Rectangle(p1.X - hs / 2, p1.Y - hs / 2, hs, hs);
                            Rectangle r2 = new Rectangle(p2.X - hs / 2, p2.Y - hs / 2, hs, hs);

                            e.Graphics.FillRectangle(hBrush, r1);
                            e.Graphics.DrawRectangle(hPen, r1);

                            e.Graphics.FillRectangle(hBrush, r2);
                            e.Graphics.DrawRectangle(hPen, r2);
                        }
                        else if (isRegionSelected)
                        {
                            int hs = 5;
                            using Brush hBrush = new SolidBrush(Color.Cyan);
                            e.Graphics.FillRectangle(hBrush, p1.X - hs / 2, p1.Y - hs / 2, hs, hs);
                            e.Graphics.FillRectangle(hBrush, p2.X - hs / 2, p2.Y - hs / 2, hs, hs);
                        }
                    }
                }

                // 領域選択ハンドル（四隅）
                if (isRegionSelected)
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
            }

            // 罫線追加モード中のマウスプレビュー線描画
            if (activeLineAddMode != ImageCoordinateHelper.RuleLineType.None && lstRegions.SelectedIndex >= 0)
            {
                OcrRegion selRegion = regions[lstRegions.SelectedIndex];
                if (selRegion.Type == "table")
                {
                    Point mousePos = pictureBox1.PointToClient(Cursor.Position);
                    Rectangle tableScreenRect = ImageCoordinateHelper.ImageToScreen(
                        new Rectangle(selRegion.X, selRegion.Y, selRegion.Width, selRegion.Height),
                        pictureBox1);

                    if (tableScreenRect.Contains(mousePos))
                    {
                        using Pen previewLinePen = new Pen(Color.Gold, 2f) { DashStyle = System.Drawing.Drawing2D.DashStyle.DashDot };
                        if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Horizontal)
                        {
                            e.Graphics.DrawLine(previewLinePen, tableScreenRect.Left, mousePos.Y, tableScreenRect.Right, mousePos.Y);
                        }
                        else if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Vertical)
                        {
                            e.Graphics.DrawLine(previewLinePen, mousePos.X, tableScreenRect.Top, mousePos.X, tableScreenRect.Bottom);
                        }
                    }
                }
            }

            if (isDrawingRegion)
            {
                using Pen previewPen = new Pen(Color.Blue, 2);
                e.Graphics.DrawRectangle(previewPen, regionPreviewRectangle);
            }
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (pictureBox1.Image == null) return;

            // 右クリック：罫線削除 or 領域区分変更 / 新規領域作成
            if (e.Button == MouseButtons.Right)
            {
                int curIdx = lstRegions.SelectedIndex;
                if (curIdx >= 0 && curIdx < regions.Count && regions[curIdx].Type == "table")
                {
                    if (ImageCoordinateHelper.HitTestTableRuleLines(e.Location, 8, 6, regions[curIdx], selectedRuleLineIndices, pictureBox1, out int hitIdx, out _))
                    {
                        var table = regions[curIdx];
                        if (selectedRuleLineIndices.Contains(hitIdx) && selectedRuleLineIndices.Count > 1)
                        {
                            foreach (int delIdx in selectedRuleLineIndices.OrderByDescending(x => x))
                            {
                                if (delIdx >= 0 && delIdx < table.RuleLines.Count)
                                    table.RuleLines.RemoveAt(delIdx);
                            }
                        }
                        else
                        {
                            table.RuleLines.RemoveAt(hitIdx);
                        }
                        selectedRuleLineIndices.Clear();
                        pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                        _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                        UpdateTableLineControlsState();
                        pictureBox1.Invalidate();
                        return;
                    }
                }

                for (int i = 0; i < regions.Count; i++)
                {
                    if (i == curIdx) continue;
                    if (regions[i].Type == "table")
                    {
                        if (ImageCoordinateHelper.HitTestTableRuleLines(e.Location, 8, 6, regions[i], null, pictureBox1, out int hitIdx, out _))
                        {
                            regions[i].RuleLines.RemoveAt(hitIdx);
                            selectedRuleLineIndices.Clear();
                            pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                            _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                            UpdateTableLineControlsState();
                            pictureBox1.Invalidate();
                            return;
                        }
                    }
                }

                // 領域上の右クリック判定（区分変更メニュー）
                int hitRegionIdx = -1;
                for (int i = regions.Count - 1; i >= 0; i--)
                {
                    Rectangle screenRect = ImageCoordinateHelper.ImageToScreen(
                        new Rectangle(regions[i].X, regions[i].Y, regions[i].Width, regions[i].Height),
                        pictureBox1);
                    if (screenRect.Contains(e.Location))
                    {
                        hitRegionIdx = i;
                        break;
                    }
                }

                if (hitRegionIdx >= 0)
                {
                    lstRegions.SelectedIndex = hitRegionIdx;
                    pictureBox1.Invalidate();
                    ShowRegionContextMenu(e.Location, regions[hitRegionIdx]);
                    return;
                }
                else
                {
                    // 空白キャンバス上の右クリック：新規領域作成メニュー
                    Point imgPoint = ImageCoordinateHelper.ScreenToImagePoint(e.Location, pictureBox1);
                    ShowEmptyCanvasContextMenu(e.Location, imgPoint);
                    return;
                }
            }

            if (e.Button != MouseButtons.Left) return;

            // 1. 横罫線追加モード
            if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Horizontal)
            {
                int selIdx = lstRegions.SelectedIndex;
                if (selIdx >= 0 && selIdx < regions.Count && regions[selIdx].Type == "table")
                {
                    OcrRegion table = regions[selIdx];
                    table.EnsureRuleLines();
                    Point imgPt = ImageCoordinateHelper.ScreenToImagePoint(e.Location, pictureBox1);
                    if (imgPt.Y > table.Y + 2 && imgPt.Y < table.Y + table.Height - 2)
                    {
                        var newLine = new TableRuleLine(false, imgPt.Y, table.X, table.X + table.Width);
                        table.RuleLines.Add(newLine);
                        selectedRuleLineIndices.Clear();
                        selectedRuleLineIndices.Add(table.RuleLines.Count - 1);
                        pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                        _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                    }
                }
                activeLineAddMode = ImageCoordinateHelper.RuleLineType.None;
                UpdateTableLineControlsState();
                pictureBox1.Invalidate();
                return;
            }

            // 2. 縦罫線追加モード
            if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Vertical)
            {
                int selIdx = lstRegions.SelectedIndex;
                if (selIdx >= 0 && selIdx < regions.Count && regions[selIdx].Type == "table")
                {
                    OcrRegion table = regions[selIdx];
                    table.EnsureRuleLines();
                    Point imgPt = ImageCoordinateHelper.ScreenToImagePoint(e.Location, pictureBox1);
                    if (imgPt.X > table.X + 2 && imgPt.X < table.X + table.Width - 2)
                    {
                        var newLine = new TableRuleLine(true, imgPt.X, table.Y, table.Y + table.Height);
                        table.RuleLines.Add(newLine);
                        selectedRuleLineIndices.Clear();
                        selectedRuleLineIndices.Add(table.RuleLines.Count - 1);
                        pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                        _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                    }
                }
                activeLineAddMode = ImageCoordinateHelper.RuleLineType.None;
                UpdateTableLineControlsState();
                pictureBox1.Invalidate();
                return;
            }

            // 3. 罫線削除モード
            if (isLineDeleteMode)
            {
                for (int i = 0; i < regions.Count; i++)
                {
                    if (regions[i].Type == "table")
                    {
                        if (ImageCoordinateHelper.HitTestTableRuleLines(e.Location, 8, 8, regions[i], selectedRuleLineIndices, pictureBox1, out int hitIdx, out _))
                        {
                            regions[i].RuleLines.RemoveAt(hitIdx);
                            selectedRuleLineIndices.Clear();
                            pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                            _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                            break;
                        }
                    }
                }
                isLineDeleteMode = false;
                UpdateTableLineControlsState();
                pictureBox1.Invalidate();
                return;
            }

            // 4. 通常モードでの罫線選択・端点ドラッグ・Ctrlコピー移動
            int curTableIdx = lstRegions.SelectedIndex;
            if (curTableIdx >= 0 && curTableIdx < regions.Count && regions[curTableIdx].Type == "table")
            {
                var table = regions[curTableIdx];
                table.EnsureRuleLines();

                if (ImageCoordinateHelper.HitTestTableRuleLines(e.Location, 8, 6, table, selectedRuleLineIndices, pictureBox1, out int hitIdx, out var hitPart))
                {
                    bool isShift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
                    bool isCtrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;

                    if (hitPart == ImageCoordinateHelper.RuleLineHitPart.LineBody)
                    {
                        if (isShift)
                        {
                            if (selectedRuleLineIndices.Contains(hitIdx))
                                selectedRuleLineIndices.Remove(hitIdx);
                            else
                                selectedRuleLineIndices.Add(hitIdx);
                        }
                        else if (!selectedRuleLineIndices.Contains(hitIdx))
                        {
                            selectedRuleLineIndices.Clear();
                            selectedRuleLineIndices.Add(hitIdx);
                        }
                    }
                    else
                    {
                        // 端点操作 (StartHandle / EndHandle)
                        if (!selectedRuleLineIndices.Contains(hitIdx))
                        {
                            if (!isShift) selectedRuleLineIndices.Clear();
                            selectedRuleLineIndices.Add(hitIdx);
                        }
                    }

                    if (selectedRuleLineIndices.Count == 0)
                    {
                        UpdateTableLineControlsState();
                        pictureBox1.Invalidate();
                        return;
                    }

                    draggingRuleLineIndex = hitIdx;
                    draggingRuleRegionIndex = curTableIdx;
                    dragStartImagePoint = ImageCoordinateHelper.ScreenToImagePoint(e.Location, pictureBox1);

                    var line = table.RuleLines[hitIdx];

                    if (hitPart == ImageCoordinateHelper.RuleLineHitPart.StartHandle)
                    {
                        ruleLineDragMode = RuleLineDragMode.AdjustStart;
                        isCtrlCopyDragging = false;
                        pictureBox1.Cursor = line.IsVertical ? Cursors.SizeNS : Cursors.SizeWE;
                    }
                    else if (hitPart == ImageCoordinateHelper.RuleLineHitPart.EndHandle)
                    {
                        ruleLineDragMode = RuleLineDragMode.AdjustEnd;
                        isCtrlCopyDragging = false;
                        pictureBox1.Cursor = line.IsVertical ? Cursors.SizeNS : Cursors.SizeWE;
                    }
                    else
                    {
                        ruleLineDragMode = RuleLineDragMode.MoveLine;
                        if (isCtrl)
                        {
                            isCtrlCopyDragging = true;
                            var newSelected = new HashSet<int>();
                            foreach (int idx in selectedRuleLineIndices.OrderBy(x => x))
                            {
                                var copyLine = table.RuleLines[idx].Clone();
                                table.RuleLines.Add(copyLine);
                                newSelected.Add(table.RuleLines.Count - 1);
                            }
                            selectedRuleLineIndices.Clear();
                            foreach (int n in newSelected) selectedRuleLineIndices.Add(n);
                            draggingRuleLineIndex = selectedRuleLineIndices.Last();
                            pictureBox1.Cursor = Cursors.Cross;
                        }
                        else
                        {
                            isCtrlCopyDragging = false;
                            pictureBox1.Cursor = line.IsVertical ? Cursors.VSplit : Cursors.HSplit;
                        }
                    }

                    dragInitialRuleLines.Clear();
                    foreach (int idx in selectedRuleLineIndices)
                    {
                        if (idx >= 0 && idx < table.RuleLines.Count)
                            dragInitialRuleLines[idx] = table.RuleLines[idx].Clone();
                    }

                    UpdateTableLineControlsState();
                    pictureBox1.Invalidate();
                    return;
                }
                else if (!((Control.ModifierKeys & Keys.Shift) == Keys.Shift))
                {
                    selectedRuleLineIndices.Clear();
                    UpdateTableLineControlsState();
                    pictureBox1.Invalidate();
                }
            }

            // 他の表領域内の罫線ヒット判定
            for (int i = 0; i < regions.Count; i++)
            {
                if (i == curTableIdx) continue;
                if (regions[i].Type == "table")
                {
                    var table = regions[i];
                    table.EnsureRuleLines();
                    if (ImageCoordinateHelper.HitTestTableRuleLines(e.Location, 8, 6, table, null, pictureBox1, out int hitIdx, out var hitPart))
                    {
                        lstRegions.SelectedIndex = i;
                        selectedRuleLineIndices.Clear();
                        selectedRuleLineIndices.Add(hitIdx);
                        draggingRuleLineIndex = hitIdx;
                        draggingRuleRegionIndex = i;
                        dragStartImagePoint = ImageCoordinateHelper.ScreenToImagePoint(e.Location, pictureBox1);

                        var line = table.RuleLines[hitIdx];

                        if (hitPart == ImageCoordinateHelper.RuleLineHitPart.StartHandle)
                        {
                            ruleLineDragMode = RuleLineDragMode.AdjustStart;
                            isCtrlCopyDragging = false;
                            pictureBox1.Cursor = line.IsVertical ? Cursors.SizeNS : Cursors.SizeWE;
                        }
                        else if (hitPart == ImageCoordinateHelper.RuleLineHitPart.EndHandle)
                        {
                            ruleLineDragMode = RuleLineDragMode.AdjustEnd;
                            isCtrlCopyDragging = false;
                            pictureBox1.Cursor = line.IsVertical ? Cursors.SizeNS : Cursors.SizeWE;
                        }
                        else
                        {
                            bool isCtrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                            if (isCtrl)
                            {
                                var copyLine = line.Clone();
                                table.RuleLines.Add(copyLine);
                                selectedRuleLineIndices.Clear();
                                selectedRuleLineIndices.Add(table.RuleLines.Count - 1);
                                draggingRuleLineIndex = table.RuleLines.Count - 1;
                                ruleLineDragMode = RuleLineDragMode.MoveLine;
                                isCtrlCopyDragging = true;
                                pictureBox1.Cursor = Cursors.Cross;
                            }
                            else
                            {
                                ruleLineDragMode = RuleLineDragMode.MoveLine;
                                isCtrlCopyDragging = false;
                                pictureBox1.Cursor = line.IsVertical ? Cursors.VSplit : Cursors.HSplit;
                            }
                        }

                        dragInitialRuleLines.Clear();
                        foreach (int idx in selectedRuleLineIndices)
                        {
                            if (idx >= 0 && idx < table.RuleLines.Count)
                                dragInitialRuleLines[idx] = table.RuleLines[idx].Clone();
                        }

                        UpdateTableLineControlsState();
                        pictureBox1.Invalidate();
                        return;
                    }
                }
            }

            // 5. 既存領域のリサイズ・移動判定
            int hitIndex = hoverRegionIndex;
            if (hitIndex >= 0 && hitIndex < regions.Count)
            {
                selectedRuleLineIndices.Clear();
                lstRegions.SelectedIndex = hitIndex;
                OcrRegion region = regions[hitIndex];

                if (hoverResizeMode != ResizeMode.None)
                {
                    resizeMode = hoverResizeMode;
                    movingRegionIndex = -1;
                    resizeStartPoint = e.Location;
                    resizeOriginalRectangle = new Rectangle(
                        region.X, region.Y, region.Width, region.Height);
                    return;
                }

                movingRegionIndex = hitIndex;
                moveStartPoint = e.Location;
                moveOriginalRectangle = new Rectangle(
                    region.X, region.Y, region.Width, region.Height);
                pictureBox1.Cursor = Cursors.SizeAll;
                return;
            }

            // 6. 新規領域描画
            selectedRuleLineIndices.Clear();
            isDrawingRegion = true;
            regionStartPoint = e.Location;
            regionPreviewRectangle = new Rectangle(e.X, e.Y, 0, 0);
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (pictureBox1.Image == null) return;

            // 1. 罫線ドラッグ（端点長さ一括調整 / 位置一括移動 / Ctrl一括コピー移動）
            if (ruleLineDragMode != RuleLineDragMode.None &&
                draggingRuleRegionIndex >= 0 && draggingRuleRegionIndex < regions.Count)
            {
                OcrRegion table = regions[draggingRuleRegionIndex];
                Point imgPt = ImageCoordinateHelper.ScreenToImagePoint(e.Location, pictureBox1);
                int deltaX = imgPt.X - dragStartImagePoint.X;
                int deltaY = imgPt.Y - dragStartImagePoint.Y;

                TableRuleLine? primaryLine = draggingRuleLineIndex >= 0 && draggingRuleLineIndex < table.RuleLines.Count
                    ? table.RuleLines[draggingRuleLineIndex]
                    : null;

                if (ruleLineDragMode == RuleLineDragMode.AdjustStart)
                {
                    foreach (var kvp in dragInitialRuleLines)
                    {
                        int idx = kvp.Key;
                        var initLine = kvp.Value;
                        if (idx >= 0 && idx < table.RuleLines.Count)
                        {
                            var line = table.RuleLines[idx];
                            if (line.IsVertical)
                            {
                                int newStart = Math.Max(table.Y, Math.Min(initLine.Start + deltaY, line.End - 1));
                                line.Start = newStart;
                            }
                            else
                            {
                                int newStart = Math.Max(table.X, Math.Min(initLine.Start + deltaX, line.End - 1));
                                line.Start = newStart;
                            }
                        }
                    }
                    pictureBox1.Cursor = (primaryLine?.IsVertical ?? false) ? Cursors.SizeNS : Cursors.SizeWE;
                    pictureBox1.Invalidate();
                    return;
                }

                if (ruleLineDragMode == RuleLineDragMode.AdjustEnd)
                {
                    foreach (var kvp in dragInitialRuleLines)
                    {
                        int idx = kvp.Key;
                        var initLine = kvp.Value;
                        if (idx >= 0 && idx < table.RuleLines.Count)
                        {
                            var line = table.RuleLines[idx];
                            if (line.IsVertical)
                            {
                                int newEnd = Math.Max(line.Start + 1, Math.Min(initLine.End + deltaY, table.Y + table.Height));
                                line.End = newEnd;
                            }
                            else
                            {
                                int newEnd = Math.Max(line.Start + 1, Math.Min(initLine.End + deltaX, table.X + table.Width));
                                line.End = newEnd;
                            }
                        }
                    }
                    pictureBox1.Cursor = (primaryLine?.IsVertical ?? false) ? Cursors.SizeNS : Cursors.SizeWE;
                    pictureBox1.Invalidate();
                    return;
                }

                if (ruleLineDragMode == RuleLineDragMode.MoveLine)
                {
                    foreach (var kvp in dragInitialRuleLines)
                    {
                        int idx = kvp.Key;
                        var initLine = kvp.Value;
                        if (idx >= 0 && idx < table.RuleLines.Count)
                        {
                            var line = table.RuleLines[idx];
                            if (line.IsVertical)
                            {
                                line.Pos = Math.Max(table.X + 2, Math.Min(initLine.Pos + deltaX, table.X + table.Width - 2));
                            }
                            else
                            {
                                line.Pos = Math.Max(table.Y + 2, Math.Min(initLine.Pos + deltaY, table.Y + table.Height - 2));
                            }
                        }
                    }
                    pictureBox1.Cursor = isCtrlCopyDragging ? Cursors.Cross : ((primaryLine?.IsVertical ?? false) ? Cursors.VSplit : Cursors.HSplit);
                    pictureBox1.Invalidate();
                    return;
                }
            }

            // 2. 罫線追加・削除モードのカーソル・プレビュー
            if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Horizontal)
            {
                pictureBox1.Cursor = Cursors.HSplit;
                pictureBox1.Invalidate();
                return;
            }
            if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Vertical)
            {
                pictureBox1.Cursor = Cursors.VSplit;
                pictureBox1.Invalidate();
                return;
            }
            if (isLineDeleteMode)
            {
                pictureBox1.Cursor = Cursors.Hand;
                return;
            }

            // 3. 通常モードでの罫線ホバー判定
            bool ruleLineHovered = false;
            if (resizeMode == ResizeMode.None && movingRegionIndex < 0 && !isDrawingRegion)
            {
                int curTableIdx = lstRegions.SelectedIndex;
                if (curTableIdx >= 0 && curTableIdx < regions.Count && regions[curTableIdx].Type == "table")
                {
                    var table = regions[curTableIdx];
                    table.EnsureRuleLines();

                    if (ImageCoordinateHelper.HitTestTableRuleLines(e.Location, 8, 6, table, selectedRuleLineIndices, pictureBox1, out int hitIdx, out var hitPart))
                    {
                        hoveringRuleLineIndex = hitIdx;
                        hoveringRuleRegionIndex = curTableIdx;
                        hoveringRuleLinePart = hitPart;

                        var line = table.RuleLines[hitIdx];
                        if (hitPart == ImageCoordinateHelper.RuleLineHitPart.StartHandle || hitPart == ImageCoordinateHelper.RuleLineHitPart.EndHandle)
                        {
                            pictureBox1.Cursor = line.IsVertical ? Cursors.SizeNS : Cursors.SizeWE;
                        }
                        else
                        {
                            bool isCtrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                            pictureBox1.Cursor = isCtrl ? Cursors.Hand : (line.IsVertical ? Cursors.VSplit : Cursors.HSplit);
                        }

                        ruleLineHovered = true;
                        pictureBox1.Invalidate();
                    }
                }

                if (!ruleLineHovered && hoveringRuleLineIndex != -1)
                {
                    hoveringRuleLineIndex = -1;
                    hoveringRuleRegionIndex = -1;
                    hoveringRuleLinePart = ImageCoordinateHelper.RuleLineHitPart.None;
                    pictureBox1.Invalidate();
                }
            }

            if (ruleLineHovered) return;

            // 4. 領域ホバー・リサイズ判定
            if (resizeMode == ResizeMode.None && movingRegionIndex < 0 && !isDrawingRegion)
            {
                hoverRegionIndex = -1;
                hoverResizeMode = ResizeMode.None;

                int nearIndex = ImageCoordinateHelper.HitTestRegionNear(
                    e.Location, 20, regions, pictureBox1);

                if (nearIndex >= 0)
                {
                    hoverRegionIndex = nearIndex;

                    if (lstRegions.SelectedIndex != nearIndex)
                    {
                        lstRegions.SelectedIndex = nearIndex;
                        pictureBox1.Invalidate();
                    }

                    OcrRegion hoverRegion = regions[nearIndex];
                    Rectangle hoverRect = ImageCoordinateHelper.ImageToScreen(
                        new Rectangle(hoverRegion.X, hoverRegion.Y,
                            hoverRegion.Width, hoverRegion.Height),
                        pictureBox1);

                    ResizeMode mode = ImageCoordinateHelper.GetResizeMode(e.Location, hoverRect);
                    hoverResizeMode = mode;

                    pictureBox1.Cursor = mode switch
                    {
                        ResizeMode.Left or ResizeMode.Right => Cursors.SizeWE,
                        ResizeMode.Top or ResizeMode.Bottom => Cursors.SizeNS,
                        ResizeMode.TopLeft or ResizeMode.BottomRight => Cursors.SizeNWSE,
                        ResizeMode.TopRight or ResizeMode.BottomLeft => Cursors.SizeNESW,
                        _ => Cursors.SizeAll
                    };
                }
                else
                {
                    pictureBox1.Cursor = Cursors.Default;
                }
            }

            // 5. 領域リサイズ中
            if (resizeMode != ResizeMode.None)
            {
                if (lstRegions.SelectedIndex < 0) return;
                OcrRegion region = regions[lstRegions.SelectedIndex];

                float scale = Math.Min(
                    (float)pictureBox1.ClientSize.Width / pictureBox1.Image.Width,
                    (float)pictureBox1.ClientSize.Height / pictureBox1.Image.Height);

                int deltaX = (int)((e.X - resizeStartPoint.X) / scale);
                int deltaY = (int)((e.Y - resizeStartPoint.Y) / scale);

                int newX = resizeOriginalRectangle.X;
                int newY = resizeOriginalRectangle.Y;
                int newWidth = resizeOriginalRectangle.Width;
                int newHeight = resizeOriginalRectangle.Height;

                switch (resizeMode)
                {
                    case ResizeMode.Left:
                        newX += deltaX; newWidth -= deltaX; break;
                    case ResizeMode.Right:
                        newWidth += deltaX; break;
                    case ResizeMode.Top:
                        newY += deltaY; newHeight -= deltaY; break;
                    case ResizeMode.Bottom:
                        newHeight += deltaY; break;
                    case ResizeMode.TopLeft:
                        newX += deltaX; newWidth -= deltaX;
                        newY += deltaY; newHeight -= deltaY; break;
                    case ResizeMode.TopRight:
                        newWidth += deltaX;
                        newY += deltaY; newHeight -= deltaY; break;
                    case ResizeMode.BottomLeft:
                        newX += deltaX; newWidth -= deltaX;
                        newHeight += deltaY; break;
                    case ResizeMode.BottomRight:
                        newWidth += deltaX; newHeight += deltaY; break;
                }

                const int minSize = 20;
                if (newWidth < minSize)
                {
                    newWidth = minSize;
                    if (resizeMode is ResizeMode.Left or ResizeMode.TopLeft or ResizeMode.BottomLeft)
                        newX = resizeOriginalRectangle.Right - minSize;
                }
                if (newHeight < minSize)
                {
                    newHeight = minSize;
                    if (resizeMode is ResizeMode.Top or ResizeMode.TopLeft or ResizeMode.TopRight)
                        newY = resizeOriginalRectangle.Bottom - minSize;
                }

                newX = Math.Max(0, newX);
                newY = Math.Max(0, newY);
                newWidth = Math.Min(newWidth, pictureBox1.Image.Width - newX);
                newHeight = Math.Min(newHeight, pictureBox1.Image.Height - newY);

                region.X = newX; region.Y = newY;
                region.Width = newWidth; region.Height = newHeight;

                isUpdatingNumericValues = true;
                try
                {
                    numX.Value = Math.Min(numX.Maximum, Math.Max(numX.Minimum, region.X));
                    numY.Value = Math.Min(numY.Maximum, Math.Max(numY.Minimum, region.Y));
                    numWidth.Value = Math.Min(numWidth.Maximum, Math.Max(numWidth.Minimum, region.Width));
                    numHeight.Value = Math.Min(numHeight.Maximum, Math.Max(numHeight.Minimum, region.Height));
                }
                finally
                {
                    isUpdatingNumericValues = false;
                }

                pictureBox1.Invalidate();
                return;
            }

            // 6. 領域移動中
            if (movingRegionIndex >= 0)
            {
                OcrRegion region = regions[movingRegionIndex];
                float scale = Math.Min(
                    (float)pictureBox1.ClientSize.Width / pictureBox1.Image.Width,
                    (float)pictureBox1.ClientSize.Height / pictureBox1.Image.Height);

                int newX = moveOriginalRectangle.X + (int)((e.X - moveStartPoint.X) / scale);
                int newY = moveOriginalRectangle.Y + (int)((e.Y - moveStartPoint.Y) / scale);

                newX = Math.Max(0, Math.Min(newX, pictureBox1.Image.Width - region.Width));
                newY = Math.Max(0, Math.Min(newY, pictureBox1.Image.Height - region.Height));

                region.X = newX; region.Y = newY;

                isUpdatingNumericValues = true;
                try
                {
                    numX.Value = Math.Min(numX.Maximum, Math.Max(numX.Minimum, region.X));
                    numY.Value = Math.Min(numY.Maximum, Math.Max(numY.Minimum, region.Y));
                }
                finally
                {
                    isUpdatingNumericValues = false;
                }

                pictureBox1.Invalidate();
                return;
            }

            // 7. 新規領域描画中
            if (isDrawingRegion)
            {
                int drawX = Math.Min(regionStartPoint.X, e.X);
                int drawY = Math.Min(regionStartPoint.Y, e.Y);
                int drawWidth = Math.Abs(e.X - regionStartPoint.X);
                int drawHeight = Math.Abs(e.Y - regionStartPoint.Y);
                regionPreviewRectangle = new Rectangle(drawX, drawY, drawWidth, drawHeight);
                pictureBox1.Invalidate();
            }
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (ruleLineDragMode != RuleLineDragMode.None)
            {
                if (draggingRuleRegionIndex >= 0 && draggingRuleRegionIndex < regions.Count)
                {
                    OcrRegion table = regions[draggingRuleRegionIndex];
                    foreach (var line in table.RuleLines)
                    {
                        if (line.Start > line.End)
                        {
                            int tmp = line.Start;
                            line.Start = line.End;
                            line.End = tmp;
                        }
                    }
                    pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                    _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                }
                ruleLineDragMode = RuleLineDragMode.None;
                draggingRuleLineIndex = -1;
                draggingRuleRegionIndex = -1;
                dragInitialRuleLines.Clear();
                isCtrlCopyDragging = false;
                pictureBox1.Cursor = Cursors.Default;
                pictureBox1.Invalidate();
                return;
            }

            if (resizeMode != ResizeMode.None)
            {
                resizeMode = ResizeMode.None;
                pictureBox1.Cursor = Cursors.Default;
                pictureBox1.Invalidate();
                return;
            }

            if (movingRegionIndex >= 0)
            {
                movingRegionIndex = -1;
                pictureBox1.Cursor = Cursors.Default;
                pictureBox1.Invalidate();
                return;
            }

            if (!isDrawingRegion) return;
            isDrawingRegion = false;

            if (regionPreviewRectangle.Width < 5 || regionPreviewRectangle.Height < 5)
            {
                pictureBox1.Invalidate();
                return;
            }

            Rectangle imageRect = ImageCoordinateHelper.ScreenToImage(
                regionPreviewRectangle, pictureBox1);

            var menu = new ContextMenuStrip();
            var titleItem = new ToolStripMenuItem("新規領域の種別を選択:")
            {
                Enabled = false,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };
            menu.Items.Add(titleItem);
            menu.Items.Add(new ToolStripSeparator());

            var types = new (string type, string name)[]
            {
                ("body", "＋ 本文"),
                ("heading", "＋ 見出し"),
                ("table", "＋ 表"),
                ("footnote", "＋ 注釈文"),
                ("image", "＋ 図")
            };

            foreach (var (t, n) in types)
            {
                string targetType = t;
                string regionName = n;
                var item = new ToolStripMenuItem(n);
                item.Click += (s, ev) => CreateNewRegionWithBounds(imageRect, targetType, regionName);
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());
            var cancelItem = new ToolStripMenuItem("✕ キャンセル");
            cancelItem.Click += (s, ev) => pictureBox1.Invalidate();
            menu.Items.Add(cancelItem);

            menu.Show(pictureBox1, e.Location);
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

        private void InitializeOcrResultView()
        {
            tabOcrResult = new TabControl { Dock = DockStyle.Fill };
            tabOcrText = new TabPage("本文");
            tabOcrTable = new TabPage("表");

            tableLayoutPanel1.Controls.Remove(richTextBox1);
            richTextBox1.Dock = DockStyle.Fill;
            tabOcrText.Controls.Add(richTextBox1);
            ocrResultTextBoxes["body"] = richTextBox1;

            tabOcrResult.TabPages.Add(tabOcrText);
            tabOcrResult.TabPages.Add(tabOcrTable);

            AddOcrResultTab("heading", "見出し");
            AddOcrResultTab("footnote", "注釈文");

            tabOcrImage = new TabPage("図");
            pnlFigureGallery = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(10)
            };
            pnlFigureGallery.Resize += (s, e) =>
            {
                int w = Math.Max(420, pnlFigureGallery.ClientSize.Width - 30);
                foreach (Control ctrl in pnlFigureGallery.Controls)
                {
                    if (ctrl is Panel p) p.Width = w;
                }
            };
            tabOcrImage.Controls.Add(pnlFigureGallery);
            tabOcrResult.TabPages.Add(tabOcrImage);

            tabOcrResult.SelectedIndexChanged += (s, e) =>
            {
                if (tabOcrResult.SelectedTab == tabOcrImage)
                {
                    GetAllFigureItems();
                    RefreshFigureGalleryView();
                }
            };

            AddOcrResultTab("unclassified", "未分類");

            // 表操作用ツールバーパネル
            var pnlTableToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 38,
                Padding = new Padding(4),
                BackColor = Color.FromArgb(248, 249, 250),
                WrapContents = false
            };

            var btnMergeCells = new Button
            {
                Text = "⊞ 選択セルを結合",
                Size = new Size(130, 28),
                BackColor = Color.LightSkyBlue,
                FlatStyle = FlatStyle.System
            };
            btnMergeCells.Click += (s, e) =>
            {
                if (dgvOcrTable != null)
                {
                    if (TableCellMerger.MergeSelectedCells(dgvOcrTable, tableMergeSpans))
                    {
                        txtLog.AppendText("【セル結合】選択されたセルを結合しました。" + Environment.NewLine);
                    }
                    else
                    {
                        MessageBox.Show("結合するセルを2つ以上選択してください。", "セル結合", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };

            var btnAutoMergeBlanks = new Button
            {
                Text = "⚡ 空白セルを一括結合",
                Size = new Size(150, 28),
                UseVisualStyleBackColor = true
            };
            btnAutoMergeBlanks.Click += (s, e) =>
            {
                if (dgvOcrTable != null)
                {
                    int merged = TableCellMerger.AutoMergeBlankCells(dgvOcrTable, tableMergeSpans);
                    txtLog.AppendText($"【一括結合】{merged}箇所の空白セルを自動結合しました。" + Environment.NewLine);
                }
            };

            var btnUnmergeCells = new Button
            {
                Text = "✂ 結合解除",
                Size = new Size(95, 28),
                UseVisualStyleBackColor = true
            };
            btnUnmergeCells.Click += (s, e) =>
            {
                if (dgvOcrTable != null)
                {
                    if (TableCellMerger.UnmergeSelectedCells(dgvOcrTable, tableMergeSpans))
                    {
                        txtLog.AppendText("【結合解除】セルの結合を解除しました。" + Environment.NewLine);
                    }
                }
            };

            var btnCopyTable = new Button
            {
                Text = "📋 表をコピー (Word/Excel)",
                Size = new Size(170, 28),
                UseVisualStyleBackColor = true
            };
            btnCopyTable.Click += (s, e) =>
            {
                if (dgvOcrTable != null)
                {
                    TableCellMerger.CopyTableToClipboard(dgvOcrTable, tableMergeSpans);
                    txtLog.AppendText("【表コピー】結合状態を保持したWord/Excel対応テーブルをクリップボードにコピーしました。" + Environment.NewLine);
                    MessageBox.Show("結合状態を保持した表をクリップボードにコピーしました。\nWordまたはExcelにそのまま貼り付け（Ctrl+V）できます。", "表コピー", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            pnlTableToolbar.Controls.Add(btnMergeCells);
            pnlTableToolbar.Controls.Add(btnAutoMergeBlanks);
            pnlTableToolbar.Controls.Add(btnUnmergeCells);
            pnlTableToolbar.Controls.Add(btnCopyTable);

            dgvOcrTable = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = true,
                RowHeadersVisible = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = true
            };
            dgvOcrTable.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // コンテキストメニュー (右クリック)
            var contextMenu = new ContextMenuStrip();
            var mnuMerge = new ToolStripMenuItem("選択セルを結合 (Ctrl+M)");
            mnuMerge.Click += (s, e) => { if (dgvOcrTable != null) TableCellMerger.MergeSelectedCells(dgvOcrTable, tableMergeSpans); };

            var mnuAutoMerge = new ToolStripMenuItem("空白セルを一括結合");
            mnuAutoMerge.Click += (s, e) => { if (dgvOcrTable != null) TableCellMerger.AutoMergeBlankCells(dgvOcrTable, tableMergeSpans); };

            var mnuUnmerge = new ToolStripMenuItem("セル結合を解除");
            mnuUnmerge.Click += (s, e) => { if (dgvOcrTable != null) TableCellMerger.UnmergeSelectedCells(dgvOcrTable, tableMergeSpans); };

            var mnuCopy = new ToolStripMenuItem("Word/Excel用に表をコピー (Ctrl+C)");
            mnuCopy.Click += (s, e) => { if (dgvOcrTable != null) TableCellMerger.CopyTableToClipboard(dgvOcrTable, tableMergeSpans); };

            contextMenu.Items.Add(mnuMerge);
            contextMenu.Items.Add(mnuAutoMerge);
            contextMenu.Items.Add(mnuUnmerge);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(mnuCopy);
            dgvOcrTable.ContextMenuStrip = contextMenu;

            // キーボードショートカット (Ctrl+M, Ctrl+C)
            dgvOcrTable.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.M)
                {
                    TableCellMerger.MergeSelectedCells(dgvOcrTable, tableMergeSpans);
                    e.Handled = true;
                }
                else if (e.Control && e.KeyCode == Keys.C)
                {
                    TableCellMerger.CopyTableToClipboard(dgvOcrTable, tableMergeSpans);
                    e.Handled = true;
                }
            };

            // 結合セルのビジュアル描画
            dgvOcrTable.CellPainting += (s, e) =>
            {
                TableCellMerger.PaintMergedCell(e, tableMergeSpans, dgvOcrTable);
            };

            tabOcrTable.Controls.Add(dgvOcrTable);
            tabOcrTable.Controls.Add(pnlTableToolbar);

            tableLayoutPanel1.Controls.Add(tabOcrResult, 1, 0);
        }

        private void InitializeTableRuleLineControls()
        {
            var grpTableLines = new GroupBox
            {
                Text = "表の罫線設定",
                Location = new Point(10, 285),
                Size = new Size(255, 185),
                ForeColor = Color.DarkSlateGray
            };

            btnTableAddHLine = new Button
            {
                Text = "＋ 横罫線追加",
                Location = new Point(8, 24),
                Size = new Size(116, 32),
                UseVisualStyleBackColor = true
            };
            btnTableAddHLine.Click += (s, e) =>
            {
                if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Horizontal)
                {
                    activeLineAddMode = ImageCoordinateHelper.RuleLineType.None;
                }
                else
                {
                    activeLineAddMode = ImageCoordinateHelper.RuleLineType.Horizontal;
                    isLineDeleteMode = false;
                }
                UpdateTableLineControlsState();
            };

            btnTableAddVLine = new Button
            {
                Text = "＋ 縦罫線追加",
                Location = new Point(130, 24),
                Size = new Size(116, 32),
                UseVisualStyleBackColor = true
            };
            btnTableAddVLine.Click += (s, e) =>
            {
                if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Vertical)
                {
                    activeLineAddMode = ImageCoordinateHelper.RuleLineType.None;
                }
                else
                {
                    activeLineAddMode = ImageCoordinateHelper.RuleLineType.Vertical;
                    isLineDeleteMode = false;
                }
                UpdateTableLineControlsState();
            };

            btnTableDeleteLine = new Button
            {
                Text = "－ 罫線削除",
                Location = new Point(8, 62),
                Size = new Size(116, 32),
                UseVisualStyleBackColor = true
            };
            btnTableDeleteLine.Click += (s, e) =>
            {
                int index = lstRegions.SelectedIndex;
                if (index >= 0 && index < regions.Count && regions[index].Type == "table")
                {
                    if (selectedRuleLineIndices.Count > 0)
                    {
                        var table = regions[index];
                        foreach (int delIdx in selectedRuleLineIndices.OrderByDescending(x => x))
                        {
                            if (delIdx >= 0 && delIdx < table.RuleLines.Count)
                                table.RuleLines.RemoveAt(delIdx);
                        }
                        selectedRuleLineIndices.Clear();
                        pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                        _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                        UpdateTableLineControlsState();
                        pictureBox1.Invalidate();
                        return;
                    }
                }

                isLineDeleteMode = !isLineDeleteMode;
                if (isLineDeleteMode)
                    activeLineAddMode = ImageCoordinateHelper.RuleLineType.None;
                UpdateTableLineControlsState();
            };

            btnTableClearLines = new Button
            {
                Text = "罫線全消去",
                Location = new Point(130, 62),
                Size = new Size(116, 32),
                UseVisualStyleBackColor = true
            };
            btnTableClearLines.Click += (s, e) =>
            {
                int index = lstRegions.SelectedIndex;
                if (index >= 0 && index < regions.Count && regions[index].Type == "table")
                {
                    if (MessageBox.Show("この表のすべての罫線を消去しますか？", "罫線全消去",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        regions[index].RuleLines.Clear();
                        selectedRuleLineIndices.Clear();
                        pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
                        _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
                        UpdateTableLineControlsState();
                        pictureBox1.Invalidate();
                    }
                }
            };

            lblTableLineStatus = new Label
            {
                Text = "※Shift+クリック: 複数選択\n※端点ドラッグ: 一括長さ変更\n※Ctrl+ドラッグ: 罫線コピー",
                Location = new Point(8, 100),
                Size = new Size(238, 75),
                ForeColor = Color.DimGray,
                Font = new Font(Font.FontFamily, 8.5f)
            };

            grpTableLines.Controls.Add(btnTableAddHLine);
            grpTableLines.Controls.Add(btnTableAddVLine);
            grpTableLines.Controls.Add(btnTableDeleteLine);
            grpTableLines.Controls.Add(btnTableClearLines);
            grpTableLines.Controls.Add(lblTableLineStatus);

            pnlRegionSettings.Controls.Add(grpTableLines);
            UpdateTableLineControlsState();
        }

        private void UpdateTableLineControlsState()
        {
            if (btnTableAddHLine == null) return;

            int index = lstRegions.SelectedIndex;
            bool isTableSelected = index >= 0 && index < regions.Count && regions[index].Type == "table";

            btnTableAddHLine.Enabled = isTableSelected;
            btnTableAddVLine.Enabled = isTableSelected;
            btnTableDeleteLine.Enabled = isTableSelected;
            btnTableClearLines.Enabled = isTableSelected;

            if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Horizontal)
            {
                btnTableAddHLine.BackColor = Color.LightSkyBlue;
                btnTableAddVLine.BackColor = SystemColors.Control;
                btnTableDeleteLine.BackColor = SystemColors.Control;
                lblTableLineStatus.Text = "【横罫線追加】表内の位置をクリックしてください";
                lblTableLineStatus.ForeColor = Color.DarkBlue;
            }
            else if (activeLineAddMode == ImageCoordinateHelper.RuleLineType.Vertical)
            {
                btnTableAddHLine.BackColor = SystemColors.Control;
                btnTableAddVLine.BackColor = Color.LightSkyBlue;
                btnTableDeleteLine.BackColor = SystemColors.Control;
                lblTableLineStatus.Text = "【縦罫線追加】表内の位置をクリックしてください";
                lblTableLineStatus.ForeColor = Color.DarkBlue;
            }
            else if (isLineDeleteMode)
            {
                btnTableAddHLine.BackColor = SystemColors.Control;
                btnTableAddVLine.BackColor = SystemColors.Control;
                btnTableDeleteLine.BackColor = Color.LightCoral;
                lblTableLineStatus.Text = "【罫線削除】削除する罫線をクリックしてください";
                lblTableLineStatus.ForeColor = Color.DarkRed;
            }
            else if (isTableSelected && selectedRuleLineIndices.Count > 1)
            {
                btnTableAddHLine.BackColor = SystemColors.Control;
                btnTableAddVLine.BackColor = SystemColors.Control;
                btnTableDeleteLine.BackColor = SystemColors.Control;
                lblTableLineStatus.Text = $"【罫線複数選択中 ({selectedRuleLineIndices.Count}本)】\n・端点(■)ドラッグ: 一括長さ変更\n・ドラッグ: 一括移動\n・Ctrl+ドラッグ: 一括コピー";
                lblTableLineStatus.ForeColor = Color.DarkRed;
            }
            else if (isTableSelected && selectedRuleLineIndices.Count == 1)
            {
                btnTableAddHLine.BackColor = SystemColors.Control;
                btnTableAddVLine.BackColor = SystemColors.Control;
                btnTableDeleteLine.BackColor = SystemColors.Control;
                lblTableLineStatus.Text = "【罫線選択中】\n・Shift+クリック: 複数選択\n・端点(■)ドラッグ: 長さ変更\n・Ctrl+ドラッグ: 罫線コピー";
                lblTableLineStatus.ForeColor = Color.DarkGoldenrod;
            }
            else
            {
                btnTableAddHLine.BackColor = SystemColors.Control;
                btnTableAddVLine.BackColor = SystemColors.Control;
                btnTableDeleteLine.BackColor = SystemColors.Control;
                lblTableLineStatus.Text = isTableSelected
                    ? "※Shift+クリック: 複数選択\n※端点ドラッグ: 一括長さ変更\n※Ctrl+ドラッグ: 罫線コピー"
                    : "※表領域を選択すると罫線を編集できます";
                lblTableLineStatus.ForeColor = Color.DimGray;
            }
        }

        private void UpdateOptionBadges()
        {
            if (lblOrientationBadge == null || lblDocTypeBadge == null) return;

            // 組方向バッジ (縦書き/横書き/自動)
            switch (appSettings.TextOrientation)
            {
                case "vertical":
                    lblOrientationBadge.Text = " ↕ 縦書き優先 ";
                    lblOrientationBadge.BackColor = Color.FromArgb(103, 58, 183); // Deep Purple
                    lblOrientationBadge.ForeColor = Color.White;
                    break;
                case "horizontal":
                    lblOrientationBadge.Text = " ↔ 横書き優先 ";
                    lblOrientationBadge.BackColor = Color.FromArgb(0, 131, 143); // Cyan / Teal
                    lblOrientationBadge.ForeColor = Color.White;
                    break;
                default:
                    lblOrientationBadge.Text = " 🔄 自動判定 ";
                    lblOrientationBadge.BackColor = Color.FromArgb(69, 90, 100); // Slate Gray
                    lblOrientationBadge.ForeColor = Color.White;
                    break;
            }

            // 書籍種別バッジ (洋書/和書)
            switch (appSettings.DocumentType)
            {
                case "western":
                    lblDocTypeBadge.Text = " 🌍 洋書(英欧文) ";
                    lblDocTypeBadge.BackColor = Color.FromArgb(230, 81, 0); // Amber / Orange
                    lblDocTypeBadge.ForeColor = Color.White;
                    break;
                case "japanese":
                default:
                    lblDocTypeBadge.Text = " 🗾 和書(日本語) ";
                    lblDocTypeBadge.BackColor = Color.FromArgb(27, 94, 32); // Forest Green
                    lblDocTypeBadge.ForeColor = Color.White;
                    break;
            }
        }

        private void ApplySettingsToViews()
        {
            UpdateOptionBadges();

            Font contentFont = appSettings.CreateFont();

            if (richTextBox1 != null)
                richTextBox1.Font = contentFont;

            foreach (var box in ocrResultTextBoxes.Values)
            {
                box.Font = contentFont;
            }

            if (dgvOcrTable != null)
            {
                dgvOcrTable.DefaultCellStyle.Font = contentFont;
                try
                {
                    dgvOcrTable.ColumnHeadersDefaultCellStyle.Font = new Font(
                        contentFont.FontFamily,
                        Math.Max(9.0f, contentFont.Size - 1.0f),
                        FontStyle.Bold);
                }
                catch
                {
                    // フォントスタイル適用不可時のフォールバック
                }
            }
        }

        private void btnOptions_Click(object? sender, EventArgs e)
        {
            using var dlg = new OptionForm(appSettings);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                appSettings = dlg.ResultSettings;
                SettingsManager.SaveSettings(appSettings);
                ApplySettingsToViews();

                string docTypeName = appSettings.DocumentType == "western" ? "洋書（英欧文）" : "和書（日本語）";
                string orientationName = appSettings.TextOrientation switch
                {
                    "vertical" => "縦書き優先",
                    "horizontal" => "横書き優先",
                    _ => "自動判定"
                };

                txtLog.AppendText(
                    $"【設定保存】フォント: {appSettings.FontFamilyName} {appSettings.FontSize:0.#}pt" +
                    $"{(appSettings.FontBold ? " (太字)" : "")} / 組方向: {orientationName} / 書籍種別: {docTypeName}" +
                    Environment.NewLine);
            }
        }

        private void AddOcrResultTab(string type, string title)
        {
            if (tabOcrResult == null) return;
            RichTextBox resultBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = appSettings.CreateFont()
            };
            TabPage page = new TabPage(title);
            page.Controls.Add(resultBox);
            tabOcrResult.TabPages.Add(page);
            ocrResultTextBoxes[type] = resultBox;
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

        private void ClearOcrResultTabs()
        {
            foreach (RichTextBox resultBox in ocrResultTextBoxes.Values)
                resultBox.Clear();

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

        private List<OcrPageData> BuildExportPages()
        {
            var allFigures = GetAllFigureItems();

            // DataGridViewから全表を厳密に分離して抽出（表1, 表2...のデータ混入を完全防止）
            List<StructuredTable> allTables = new();
            if (dgvOcrTable != null && dgvOcrTable.RowCount > 0)
            {
                allTables = TableCellMerger.ExtractTablesFromDataGridView(dgvOcrTable, tableMergeSpans);
            }

            if (ocrPageDataList.Count > 0)
            {
                // 各ページの図・表の最新状態を同期
                foreach (var pData in ocrPageDataList)
                {
                    pData.Figures = allFigures.Where(f => f.PageNumber == pData.PageNumber).ToList();
                    if (allTables.Count > 0)
                    {
                        var pTables = allTables.Where(t => t.PageNumber == pData.PageNumber).ToList();
                        if (pTables.Count > 0)
                        {
                            pData.Tables = pTables;
                        }
                    }
                }

                return ocrPageDataList;
            }

            // ocrPageDataListが空の場合（OCR未実行時や部分実行時）はUIのテキスト・表・図から構築
            string bodyText = ocrResultTextBoxes.TryGetValue("body", out var bBox) ? bBox.Text : "";
            string headingText = ocrResultTextBoxes.TryGetValue("heading", out var hBox) ? hBox.Text : "";
            string footnoteText = ocrResultTextBoxes.TryGetValue("footnote", out var fBox) ? fBox.Text : "";

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

        private void btnAddAnnotationNumber_Click(object? sender, EventArgs e)
        {
            RichTextBox? resultBox = tabOcrResult?.SelectedTab?
                .Controls.OfType<RichTextBox>().FirstOrDefault();

            if (resultBox == null || resultBox.SelectionLength == 0)
            {
                MessageBox.Show("注釈を付ける文字列を選択してください。",
                    "注釈番号", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int insertPos = resultBox.SelectionStart + resultBox.SelectionLength;
            resultBox.Select(insertPos, 0);
            resultBox.SelectedText = $"【注{nextAnnotationNumber++}】";
        }

        private void btnTestCrop_Click(object sender, EventArgs e)
        {
            int index = lstRegions.SelectedIndex;
            if (index < 0 || index >= regions.Count)
            {
                MessageBox.Show("先に領域を選択してください。", "領域テスト",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (pictureBox1.Image == null)
            {
                MessageBox.Show("ページ画像がありません。", "領域テスト",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            OcrRegion region = regions[index];
            Bitmap croppedImage;

            using (Bitmap source = new Bitmap(pictureBox1.Image))
            {
                croppedImage = RegionImageExtractor.Crop(source, region);
            }

            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            string ocrInput = Path.Combine(projectDir, "ocr_input.png");
            croppedImage.Save(ocrInput, System.Drawing.Imaging.ImageFormat.Png);

            Form previewForm = new Form
            {
                Text = "OCR領域テスト - " + region.Name,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(800, 600)
            };

            PictureBox previewPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = croppedImage
            };
            previewForm.Controls.Add(previewPictureBox);
            previewForm.Show(this);

            try
            {
                string pythonExe = Path.Combine(projectDir, "venv", "Scripts", "python.exe");
                string pythonScript = Path.Combine(projectDir, "ocr_region.py");

                if (!File.Exists(pythonExe))
                {
                    MessageBox.Show("python.exe が見つかりません。\n\n" + pythonExe,
                        "Pythonエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show("ocr_region.py が見つかりません。\n\n" + pythonScript,
                        "Pythonエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                ProcessStartInfo psi = new ProcessStartInfo
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
                psi.ArgumentList.Add(pythonScript);
                psi.ArgumentList.Add(ocrInput);

                using Process process = new Process { StartInfo = psi };
                process.Start();
                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();

                MessageBox.Show(
                    "Python終了コード: " + process.ExitCode + "\n\n" +
                    "【標準出力】\n" + standardOutput + "\n\n" +
                    "【エラー出力】\n" + standardError,
                    "Python OCR結果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Python OCRの起動に失敗しました。\n\n" + ex,
                    "OCRエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            ProgressForm? progressForm = null;

            try
            {
                btnAutoLayout.Enabled = false;
                btnStartOcr.Enabled = false;
                btnOpenPdf.Enabled = false;
                btnPrevPage.Enabled = false;
                btnNextPage.Enabled = false;
                Cursor = Cursors.WaitCursor;

                progressForm = new ProgressForm(pdfDocument.PageCount);
                progressForm.StartPosition = FormStartPosition.CenterParent;
                progressForm.Show(this);
                progressForm.UpdateProgress(0, pdfDocument.PageCount, "準備中...");

                txtLog.Clear();
                txtLog.AppendText("========== 全ページ領域自動判定 ==========" + Environment.NewLine);
                txtLog.AppendText($"PDF: {Path.GetFileName(currentPdfPath)}" + Environment.NewLine);
                txtLog.AppendText($"ページ数: {pdfDocument.PageCount}" + Environment.NewLine + Environment.NewLine);

                autoPageRegions.Clear();

                for (int pageIndex = 0; pageIndex < pdfDocument.PageCount; pageIndex++)
                {
                    string pageMessage = $"ページ {pageIndex + 1} / {pdfDocument.PageCount} を処理しています...";
                    txtLog.AppendText($"---------- {pageIndex + 1}/{pdfDocument.PageCount} ページ ----------" + Environment.NewLine);
                    txtLog.AppendText(pageMessage + Environment.NewLine);
                    txtLog.Refresh();
                    progressForm?.UpdateProgress(pageIndex, pdfDocument.PageCount,
                        pageMessage + "\r\nNDLOCR-Liteを実行しています。");

                    string pageDir = Path.Combine(outputRoot, $"page_{pageIndex + 1:0000}");
                    Directory.CreateDirectory(pageDir);

                    string imagePath = Path.Combine(pageDir, "page.png");
                    string resultJson = Path.Combine(pageDir, "auto_layout.json");

                    try
                    {
                        const int dpi = 150;
                        using (Image rendered = pdfDocument.Render(pageIndex, dpi, dpi, PdfRenderFlags.Annotations))
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
                            progressForm?.UpdateProgress(pageIndex + 1, pdfDocument.PageCount,
                                $"ページ {pageIndex + 1} 失敗\r\n終了コード: {result.ExitCode}");
                            continue;
                        }

                        if (!File.Exists(resultJson))
                        {
                            failureCount++;
                            txtLog.AppendText("失敗: auto_layout.json が生成されませんでした。" + Environment.NewLine);
                            progressForm?.UpdateProgress(pageIndex + 1, pdfDocument.PageCount,
                                $"ページ {pageIndex + 1} 失敗\r\nauto_layout.json がありません。");
                            continue;
                        }

                        List<AutoLayoutRegion> detected = OcrJsonParser.LoadAutoLayoutJson(resultJson);
                        List<OcrRegion> converted = detected.Select(OcrProcessor.ConvertAutoLayoutRegion).ToList();
                        autoPageRegions[pageIndex] = converted;

                        successCount++;
                        txtLog.AppendText($"成功: 自動領域 {converted.Count}件" + Environment.NewLine);
                        progressForm?.UpdateProgress(pageIndex + 1, pdfDocument.PageCount,
                            $"ページ {pageIndex + 1} 完了\r\n自動領域 {converted.Count}件");
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        txtLog.AppendText("失敗: " + ex.Message + Environment.NewLine);
                        progressForm?.UpdateProgress(pageIndex + 1, pdfDocument.PageCount,
                            $"ページ {pageIndex + 1} 失敗\r\n{ex.Message}");
                    }
                }

                currentPage = originalPage;
                LoadCurrentPageRegions();
                ShowCurrentPage();

                txtLog.AppendText(Environment.NewLine);
                txtLog.AppendText("========== 全ページ判定完了 ==========" + Environment.NewLine);
                txtLog.AppendText($"成功: {successCount}ページ" + Environment.NewLine);
                txtLog.AppendText($"失敗: {failureCount}ページ" + Environment.NewLine);
                txtLog.AppendText("左側の枠を確認し、必要なページだけ領域を補正してください。" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                txtLog.AppendText(Environment.NewLine +
                    "========== 全ページ判定例外 ==========" + Environment.NewLine + ex + Environment.NewLine);
            }
            finally
            {
                if (progressForm != null)
                {
                    progressForm.AllowClose = true;
                    progressForm.Close();
                    progressForm.Dispose();
                }

                Cursor = Cursors.Default;
                btnAutoLayout.Enabled = true;
                btnStartOcr.Enabled = true;
                btnOpenPdf.Enabled = true;
                btnPrevPage.Enabled = true;
                btnNextPage.Enabled = true;
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

            ClearOcrResultTabs();
            txtLog.Clear();
            txtLog.AppendText("========== 全ページOCR開始 ==========" + Environment.NewLine);
            txtLog.AppendText($"PDF: {Path.GetFileName(currentPdfPath)}" + Environment.NewLine);
            txtLog.AppendText($"ページ数: {pdfDocument.PageCount}" + Environment.NewLine + Environment.NewLine);

            try
            {
                btnStartOcr.Enabled = false;
                btnAutoLayout.Enabled = false;
                btnOpenPdf.Enabled = false;
                btnPrevPage.Enabled = false;
                btnNextPage.Enabled = false;
                Cursor = Cursors.WaitCursor;

                for (int pageIndex = 0; pageIndex < pdfDocument.PageCount; pageIndex++)
                {
                    if (pageIndex != currentPage)
                    {
                        SaveCurrentPageRegions();
                        currentPage = pageIndex;
                        LoadCurrentPageRegions();
                        ShowCurrentPage();
                        await Task.Delay(50);
                    }

                    txtLog.AppendText($"---------- ページ {pageIndex + 1}/{pdfDocument.PageCount} ----------" + Environment.NewLine);
                    txtLog.Refresh();

                    string pageDir = Path.Combine(projectDir, "ocr_results", pdfName, $"page_{pageIndex + 1:0000}");
                    Directory.CreateDirectory(pageDir);
                    string imagePath = Path.Combine(pageDir, "page.png");

                    const int dpi = 150;
                    using (Image rendered = pdfDocument.Render(pageIndex, dpi, dpi, PdfRenderFlags.Annotations))
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

                    string pageJson = Path.Combine(pageDir, "page.json");
                    string resultJson = Path.Combine(pageDir, "auto_layout.json");

                    if (exitCode != 0)
                    {
                        failureCount++;
                        txtLog.AppendText($"失敗: 終了コード {exitCode}" + Environment.NewLine);
                        continue;
                    }

                    if (!File.Exists(pageJson))
                    {
                        failureCount++;
                        txtLog.AppendText("失敗: page.json が生成されませんでした。" + Environment.NewLine);
                        continue;
                    }

                    bool useUserRegions = regions.Count > 0;
                    if (!useUserRegions && !File.Exists(resultJson))
                    {
                        failureCount++;
                        txtLog.AppendText("失敗: auto_layout.json が見つかりません。" + Environment.NewLine);
                        continue;
                    }

                    List<OcrDisplayItem> ocrItems = OcrJsonParser.LoadNdlocrPageJson(pageJson);
                    List<AutoLayoutRegion> autoRegions = useUserRegions
                        ? new List<AutoLayoutRegion>()
                        : OcrJsonParser.LoadAutoLayoutJson(resultJson);

                    txtLog.AppendText($"OCR項目数: {ocrItems.Count}" + Environment.NewLine);

                    // 図（画像切り出し＆500KB以下自動圧縮・テキストOCRは除外）
                    var imgRegions = useUserRegions
                        ? regions.Where(r => OcrProcessor.NormalizeRegionType(r.Type) == "image").ToList()
                        : autoRegions.Where(r => OcrProcessor.NormalizeRegionType(r.Type) == "image")
                                     .Select(r => OcrProcessor.ConvertAutoLayoutRegion(r)).ToList();

                    if (imgRegions.Count > 0 && File.Exists(imagePath))
                    {
                        using Bitmap pageBmp = new Bitmap(imagePath);
                        int figNum = 1;
                        foreach (var imgReg in imgRegions)
                        {
                            var figItem = FigureExtractor.CropAndCompressFigure(pageBmp, imgReg, pageIndex + 1, FigureExtractor.DefaultMaxBytes);
                            if (figItem != null)
                            {
                                figItem.Name = $"図{figNum++}";
                                extractedFigures.Add(figItem);

                                string figSavePath = Path.Combine(pageDir, $"figure_{figNum - 1:00}.{(figItem.MimeType == "image/png" ? "png" : "jpg")}");
                                File.WriteAllBytes(figSavePath, figItem.ImageBytes);
                                txtLog.AppendText($"【図切り出し】[P{pageIndex + 1}] {figItem.Name} ({figItem.Bounds.Width}×{figItem.Bounds.Height}px, {figItem.FileSizeKb:0.#}KB <= 500KB)" + Environment.NewLine);
                            }
                        }
                    }

                    // タイプ別に分類（OcrProcessorのNormalizeRegionTypeを一貫して使用）
                    Dictionary<string, List<OcrDisplayItem>> itemsByType = new();
                    foreach (OcrDisplayItem item in ocrItems)
                    {
                        string type = useUserRegions
                            ? OcrProcessor.FindUserRegionType(item, regions)
                            : OcrProcessor.FindAutoLayoutRegionType(item, autoRegions);

                        if (string.IsNullOrEmpty(type))
                            type = "unclassified";

                        // 図領域内のテキストはOCR結果に含めない
                        if (type == "image")
                            continue;

                        if (!itemsByType.ContainsKey(type))
                            itemsByType[type] = new List<OcrDisplayItem>();
                        itemsByType[type].Add(item);
                    }

                    // ページ出力用データ構造の作成
                    var pageData = new OcrPageData { PageNumber = pageIndex + 1 };

                    // 本文（全ページ・本文領域のみを段落ごとに改行して表示）
                    if (itemsByType.TryGetValue("body", out List<OcrDisplayItem>? bodyList) && bodyList.Count > 0)
                    {
                        string pageBodyText = OcrSorter.FormatBodyParagraphs(
                            bodyList, appSettings.TextOrientation, appSettings.DocumentType);

                        if (ocrResultTextBoxes["body"].TextLength > 0)
                        {
                            ocrResultTextBoxes["body"].AppendText(Environment.NewLine + Environment.NewLine);
                        }
                        ocrResultTextBoxes["body"].AppendText($"--- ページ {pageIndex + 1} ---" + Environment.NewLine);
                        ocrResultTextBoxes["body"].AppendText(pageBodyText);

                        foreach (var p in pageBodyText.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            pageData.BodyParagraphs.Add(p.Trim());
                        }
                    }

                    // 見出し
                    if (itemsByType.TryGetValue("heading", out List<OcrDisplayItem>? headingList))
                    {
                        int n = 1;
                        foreach (OcrDisplayItem item in headingList)
                        {
                            ocrResultTextBoxes["heading"].AppendText($"[P{pageIndex + 1}-{n++:00}] {item.Text}" + Environment.NewLine);
                            pageData.Headings.Add(item.Text.Trim());
                        }
                    }

                    // 注釈文
                    if (itemsByType.TryGetValue("footnote", out List<OcrDisplayItem>? footnoteList))
                    {
                        int n = 1;
                        foreach (OcrDisplayItem item in footnoteList)
                        {
                            ocrResultTextBoxes["footnote"].AppendText($"[P{pageIndex + 1}-{n++:00}] {item.Text}" + Environment.NewLine);
                            pageData.Footnotes.Add(item.Text.Trim());
                        }
                    }

                    // 未分類
                    if (itemsByType.TryGetValue("unclassified", out List<OcrDisplayItem>? unclassifiedList))
                    {
                        int n = 1;
                        foreach (OcrDisplayItem item in unclassifiedList)
                            ocrResultTextBoxes["unclassified"].AppendText($"[P{pageIndex + 1}-{n++:00}] {item.Text}" + Environment.NewLine);
                    }

                    // 表（ユーザーの罫線補正を反映した行列2D構造としてDataGridViewに追記）
                    if (itemsByType.TryGetValue("table", out List<OcrDisplayItem>? tableList) && tableList.Count > 0)
                    {
                        var structuredTables = TableGridExtractor.ExtractStructuredTables(
                            pageIndex + 1, ocrItems, regions, autoRegions, appSettings.DocumentType);

                        pageData.Tables.AddRange(structuredTables);

                        if (dgvOcrTable != null && structuredTables.Count > 0)
                        {
                            int maxCols = structuredTables.Max(t => t.ColumnCount);
                            int currentDataCols = dgvOcrTable.Columns.Count > 3 ? dgvOcrTable.Columns.Count - 3 : 0;

                            if (dgvOcrTable.Columns.Count == 0 || maxCols > currentDataCols)
                            {
                                int totalDataCols = Math.Max(maxCols, currentDataCols);
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

                                for (int c = 1; c <= totalDataCols; c++)
                                {
                                    int colIdx = dgvOcrTable.Columns.Add($"Col{c}", $"列{c}");
                                    dgvOcrTable.Columns[colIdx]!.FillWeight = Math.Max(15, 72 / totalDataCols);
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

                                foreach (var span in sTable.MergeSpans)
                                {
                                    tableMergeSpans.Add(new TableMergeSpan(span.StartCol, baseRow + span.StartRow, span.ColSpan, span.RowSpan));
                                }
                            }
                        }
                    }

                    // 図
                    pageData.Figures = extractedFigures.Where(f => f.PageNumber == pageIndex + 1).ToList();

                    ocrPageDataList.Add(pageData);

                    // 読み順ファイル保存（段落ごとに改行）
                    if (itemsByType.TryGetValue("body", out List<OcrDisplayItem>? bodyForSave) && bodyForSave.Count > 0)
                    {
                        string bodyReadingOrderText = OcrSorter.FormatBodyParagraphs(
                            bodyForSave, appSettings.TextOrientation, appSettings.DocumentType);
                        string bodyReadingOrderPath = Path.Combine(pageDir, "body_reading_order.txt");
                        File.WriteAllText(bodyReadingOrderPath, bodyReadingOrderText, new UTF8Encoding(false));
                    }

                    successCount++;
                    txtLog.AppendText("完了" + Environment.NewLine + Environment.NewLine);
                }

                txtLog.AppendText("========== 全ページOCR完了 ==========" + Environment.NewLine);
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
                RefreshFigureGalleryView();

                currentPage = originalPage;
                LoadCurrentPageRegions();
                ShowCurrentPage();

                Cursor = Cursors.Default;
                btnStartOcr.Enabled = true;
                btnAutoLayout.Enabled = true;
                btnOpenPdf.Enabled = true;
                btnPrevPage.Enabled = true;
                btnNextPage.Enabled = true;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SaveCurrentPageRegions();
            pdfDocument?.Dispose();
            pdfDocument = null;
            base.OnFormClosed(e);
        }
    }
}
