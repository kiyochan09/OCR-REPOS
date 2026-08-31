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
using OCR_Translator.Models;
using OCR_Translator.Services;
using ResizeMode = OCR_Translator.Services.ImageCoordinateHelper.ResizeMode;

namespace OCR_Translator
{
    public partial class Form1 : Form
    {
        // PDF関連
        private PdfDocument? pdfDocument;
        private int currentPage = 0;
        private string? currentPdfPath;

        private DataGridView? dgvOcrTable;
        private TabControl? tabOcrResult;
        private TabPage? tabOcrText;
        private TabPage? tabOcrTable;
        private readonly Dictionary<string, RichTextBox> ocrResultTextBoxes = new();
        private readonly LayoutStorage _layoutStorage = new LayoutStorage();

        // 現在表示しているページの領域
        private List<OcrRegion> regions = new List<OcrRegion>();

        // ページごとの領域設定（キーは PDF のページ番号 0始まり）
        private Dictionary<int, List<OcrRegion>> pageRegions = new();

        // 自動領域判定結果（ページ単位）
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

        private bool isUpdatingRegionTypeCombo = false;
        private int nextAnnotationNumber = 1;

        public Form1()
        {
            InitializeComponent();
            InitializeOcrResultView();

            cmbRegionType.SelectedIndexChanged += cmbRegionType_SelectedIndexChanged;
            richTextBox1.MouseClick += richTextBox1_MouseClick;

            btnStartOcr.Click -= btnStartOcr_Click;
            btnStartOcr.Click += btnStartOcr_Click;

            btnAutoLayout.Click -= btnAutoLayout_Click;
            btnAutoLayout.Click += btnAutoLayout_Click;
        }

        // =========================================================
        // 領域タイプ変換
        // =========================================================
        private string GetRegionType()
        {
            return cmbRegionType.Text switch
            {
                "本文" => "body",
                "見出し" => "heading",
                "注釈文" => "footnote",
                "表" => "table",
                "図" => "image",
                "画像" => "image",
                _ => "body"
            };
        }

        // =========================================================
        // PDF表示
        // =========================================================
        private void ShowCurrentPage()
        {
            if (pdfDocument == null)
                return;

            if (currentPage < 0 || currentPage >= pdfDocument.PageCount)
                return;

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

        // =========================================================
        // ページ単位の領域管理
        // =========================================================
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

        // =========================================================
        // イベントハンドラー
        // =========================================================
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

        private void btnAddRegion_Click(object sender, EventArgs e)
        {
            OcrRegion region = new OcrRegion
            {
                Name = cmbRegionType.Text,
                Type = GetRegionType(),
                X = (int)numX.Value,
                Y = (int)numY.Value,
                Width = (int)numWidth.Value,
                Height = (int)numHeight.Value
            };

            regions.Add(region);
            lstRegions.Items.Add(region.Name);
            pictureBox1.Invalidate();
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

            numX.Value = 0;
            numY.Value = 0;
            numWidth.Value = 0;
            numHeight.Value = 0;

            pictureBox1.Invalidate();
        }

        private void btnUpdateRegion_Click(object sender, EventArgs e)
        {
            int index = lstRegions.SelectedIndex;
            if (index < 0 || index >= regions.Count)
            {
                MessageBox.Show("更新する領域をListBoxから選択してください。", "領域未選択",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            OcrRegion region = regions[index];
            region.Name = cmbRegionType.Text;
            region.Type = GetRegionType();
            region.X = (int)numX.Value;
            region.Y = (int)numY.Value;
            region.Width = (int)numWidth.Value;
            region.Height = (int)numHeight.Value;

            lstRegions.Items[index] = region.Name;
            lstRegions.SelectedIndex = index;
            pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
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
            if (index < 0 || index >= regions.Count) return;

            OcrRegion region = regions[index];
            numX.Value = region.X;
            numY.Value = region.Y;
            numWidth.Value = region.Width;
            numHeight.Value = region.Height;

            if (cmbRegionType.Items.Contains(region.Name))
            {
                isUpdatingRegionTypeCombo = true;
                try { cmbRegionType.SelectedItem = region.Name; }
                finally { isUpdatingRegionTypeCombo = false; }
            }
        }

        private void cmbRegionType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (isUpdatingRegionTypeCombo) return;

            int index = lstRegions.SelectedIndex;
            if (index < 0 || index >= regions.Count) return;

            string newName = cmbRegionType.Text;
            if (string.IsNullOrWhiteSpace(newName)) return;

            OcrRegion region = regions[index];
            region.Name = newName;
            region.Type = GetRegionType();
            lstRegions.Items[index] = newName;

            pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
            _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);
            pictureBox1.Invalidate();
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

        // =========================================================
        // PictureBox 描画・マウス操作
        // =========================================================
        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (pictureBox1.Image == null) return;

            for (int i = 0; i < regions.Count; i++)
            {
                OcrRegion region = regions[i];
                Color regionColor = GetRegionColor(region.Type);
                using Pen regionPen = new Pen(regionColor, 2);

                Rectangle screenRect = ImageCoordinateHelper.ImageToScreen(
                    new Rectangle(region.X, region.Y, region.Width, region.Height),
                    pictureBox1);

                e.Graphics.DrawRectangle(regionPen, screenRect);

                if (i == lstRegions.SelectedIndex)
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

            if (isDrawingRegion)
            {
                using Pen previewPen = new Pen(Color.Blue, 2);
                e.Graphics.DrawRectangle(previewPen, regionPreviewRectangle);
            }
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || pictureBox1.Image == null)
                return;

            int hitIndex = hoverRegionIndex;
            if (hitIndex >= 0 && hitIndex < regions.Count)
            {
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

            isDrawingRegion = true;
            regionStartPoint = e.Location;
            regionPreviewRectangle = new Rectangle(e.X, e.Y, 0, 0);
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (pictureBox1.Image == null) return;

            if (resizeMode == ResizeMode.None && movingRegionIndex < 0 && !isDrawingRegion)
            {
                int nearIndex = ImageCoordinateHelper.HitTestRegionNear(
                    e.Location, 20, regions, pictureBox1);

                if (nearIndex >= 0 && lstRegions.SelectedIndex != nearIndex)
                {
                    lstRegions.SelectedIndex = nearIndex;
                    pictureBox1.Invalidate();
                }
            }

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

                numX.Value = Math.Min(numX.Maximum, Math.Max(numX.Minimum, region.X));
                numY.Value = Math.Min(numY.Maximum, Math.Max(numY.Minimum, region.Y));
                numWidth.Value = Math.Min(numWidth.Maximum, Math.Max(numWidth.Minimum, region.Width));
                numHeight.Value = Math.Min(numHeight.Maximum, Math.Max(numHeight.Minimum, region.Height));

                pictureBox1.Invalidate();
                return;
            }

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
                numX.Value = Math.Min(numX.Maximum, Math.Max(numX.Minimum, region.X));
                numY.Value = Math.Min(numY.Maximum, Math.Max(numY.Minimum, region.Y));
                pictureBox1.Invalidate();
                return;
            }

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

            OcrRegion region = new OcrRegion
            {
                Name = cmbRegionType.Text,
                Type = GetRegionType(),
                X = imageRect.X,
                Y = imageRect.Y,
                Width = imageRect.Width,
                Height = imageRect.Height
            };

            regions.Add(region);
            int index = lstRegions.Items.Add(region.Name);
            lstRegions.SelectedIndex = index;

            numX.Value = Math.Min(numX.Maximum, Math.Max(numX.Minimum, region.X));
            numY.Value = Math.Min(numY.Maximum, Math.Max(numY.Minimum, region.Y));
            numWidth.Value = Math.Min(numWidth.Maximum, Math.Max(numWidth.Minimum, region.Width));
            numHeight.Value = Math.Min(numHeight.Maximum, Math.Max(numHeight.Minimum, region.Height));

            pictureBox1.Invalidate();
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

        // =========================================================
        // OCR結果表示タブ初期化
        // =========================================================
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
            AddOcrResultTab("image", "図");
            AddOcrResultTab("unclassified", "未分類");

            dgvOcrTable = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = true,
                RowHeadersVisible = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false
            };
            dgvOcrTable.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            tabOcrTable.Controls.Add(dgvOcrTable);

            tableLayoutPanel1.Controls.Add(tabOcrResult, 1, 0);
        }

        private void AddOcrResultTab(string type, string title)
        {
            if (tabOcrResult == null) return;
            RichTextBox resultBox = new RichTextBox { Dock = DockStyle.Fill };
            resultBox.MouseClick += richTextBox1_MouseClick;
            TabPage page = new TabPage(title);
            page.Controls.Add(resultBox);
            tabOcrResult.TabPages.Add(page);
            ocrResultTextBoxes[type] = resultBox;
        }

        private void ClearOcrResultTabs()
        {
            foreach (RichTextBox resultBox in ocrResultTextBoxes.Values)
                resultBox.Clear();
        }

        // =========================================================
        // 注釈番号
        // =========================================================
        private void richTextBox1_MouseClick(object? sender, MouseEventArgs e)
        {
            if (sender is not RichTextBox resultBox) return;
            if (e.Button != MouseButtons.Left || e.X < resultBox.ClientSize.Width - 48) return;

            int characterIndex = resultBox.GetCharIndexFromPosition(e.Location);
            int lineIndex = resultBox.GetLineFromCharIndex(characterIndex);
            ToggleAnnotationNumber(resultBox, lineIndex);
        }

        private void btnAddAnnotationNumber_Click(object? sender, EventArgs e)
        {
            RichTextBox? resultBox = tabOcrResult?.SelectedTab?
                .Controls.OfType<RichTextBox>().FirstOrDefault();

            if (resultBox == null)
            {
                MessageBox.Show("本文・見出し・注釈文・図のタブで、対象行を選択してください。",
                    "注釈番号", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int lineIndex = resultBox.GetLineFromCharIndex(resultBox.SelectionStart);
            ToggleAnnotationNumber(resultBox, lineIndex);
        }

        private void ToggleAnnotationNumber(RichTextBox resultBox, int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= resultBox.Lines.Length) return;
            string line = resultBox.Lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line)) return;

            const string noteMarker = "\t【注";
            int markerIndex = line.LastIndexOf(noteMarker, StringComparison.Ordinal);

            string updatedLine = markerIndex >= 0
                ? line[..markerIndex]
                : line + $"\t【注{nextAnnotationNumber++}】";

            int lineStart = resultBox.GetFirstCharIndexFromLine(lineIndex);
            resultBox.Select(lineStart, line.Length);
            resultBox.SelectedText = updatedLine;
            resultBox.Select(lineStart + updatedLine.Length, 0);
            resultBox.Focus();
        }

        // =========================================================
        // 領域テスト切り出し
        // =========================================================
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

        // =========================================================
        // 全ページ自動領域判定
        // =========================================================
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

                richTextBox1.Clear();
                richTextBox1.AppendText("========== 全ページ領域自動判定 ==========" + Environment.NewLine);
                richTextBox1.AppendText($"PDF: {Path.GetFileName(currentPdfPath)}" + Environment.NewLine);
                richTextBox1.AppendText($"ページ数: {pdfDocument.PageCount}" + Environment.NewLine + Environment.NewLine);

                autoPageRegions.Clear();

                for (int pageIndex = 0; pageIndex < pdfDocument.PageCount; pageIndex++)
                {
                    string pageMessage = $"ページ {pageIndex + 1} / {pdfDocument.PageCount} を処理しています...";
                    richTextBox1.AppendText($"---------- {pageIndex + 1}/{pdfDocument.PageCount} ページ ----------" + Environment.NewLine);
                    richTextBox1.AppendText(pageMessage + Environment.NewLine);
                    richTextBox1.Refresh();
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
                            pythonExe, autoRegionScript, projectDir, imagePath, pageDir);

                        string log = "[STDOUT]\r\n" + result.Stdout + "\r\n[STDERR]\r\n" + result.Stderr;
                        File.WriteAllText(Path.Combine(pageDir, "ndlocr_run.log"), log, new UTF8Encoding(false));

                        if (result.ExitCode != 0)
                        {
                            failureCount++;
                            richTextBox1.AppendText($"失敗: 終了コード {result.ExitCode}" + Environment.NewLine);
                            progressForm?.UpdateProgress(pageIndex + 1, pdfDocument.PageCount,
                                $"ページ {pageIndex + 1} 失敗\r\n終了コード: {result.ExitCode}");
                            continue;
                        }

                        if (!File.Exists(resultJson))
                        {
                            failureCount++;
                            richTextBox1.AppendText("失敗: auto_layout.json が生成されませんでした。" + Environment.NewLine);
                            progressForm?.UpdateProgress(pageIndex + 1, pdfDocument.PageCount,
                                $"ページ {pageIndex + 1} 失敗\r\nauto_layout.json がありません。");
                            continue;
                        }

                        List<AutoLayoutRegion> detected = OcrJsonParser.LoadAutoLayoutJson(resultJson);
                        List<OcrRegion> converted = detected.Select(OcrProcessor.ConvertAutoLayoutRegion).ToList();
                        autoPageRegions[pageIndex] = converted;

                        successCount++;
                        richTextBox1.AppendText($"成功: 自動領域 {converted.Count}件" + Environment.NewLine);
                        progressForm?.UpdateProgress(pageIndex + 1, pdfDocument.PageCount,
                            $"ページ {pageIndex + 1} 完了\r\n自動領域 {converted.Count}件");
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        richTextBox1.AppendText("失敗: " + ex.Message + Environment.NewLine);
                        progressForm?.UpdateProgress(pageIndex + 1, pdfDocument.PageCount,
                            $"ページ {pageIndex + 1} 失敗\r\n{ex.Message}");
                    }
                }

                currentPage = originalPage;
                LoadCurrentPageRegions();
                ShowCurrentPage();

                richTextBox1.AppendText(Environment.NewLine);
                richTextBox1.AppendText("========== 全ページ判定完了 ==========" + Environment.NewLine);
                richTextBox1.AppendText($"成功: {successCount}ページ" + Environment.NewLine);
                richTextBox1.AppendText($"失敗: {failureCount}ページ" + Environment.NewLine);
                richTextBox1.AppendText("左側の枠を確認し、必要なページだけ領域を補正してください。" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                richTextBox1.AppendText(Environment.NewLine +
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

        // =========================================================
        // OCR開始
        // =========================================================
        private async void btnStartOcr_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null || string.IsNullOrWhiteSpace(currentPdfPath))
            {
                MessageBox.Show("先にPDFを開いてください。", "OCR開始",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveCurrentPageRegions();
            richTextBox1.Clear();
            richTextBox1.AppendText("========== OCR開始 ==========\r\n");
            richTextBox1.AppendText($"現在ページ: {currentPage + 1}\r\n");

            bool useUserRegions = regions.Count > 0;

            if (useUserRegions)
            {
                richTextBox1.AppendText($"ユーザー指定領域: {regions.Count}件\r\n");
                for (int i = 0; i < regions.Count; i++)
                {
                    OcrRegion r = regions[i];
                    richTextBox1.AppendText(
                        $"  [{i + 1:00}] {r.Name} / {r.Type} x={r.X}, y={r.Y}, width={r.Width}, height={r.Height}\r\n");
                }
            }
            else
            {
                richTextBox1.AppendText("ユーザー指定領域がありません。自動領域判定を使用します。\r\n");
            }

            richTextBox1.AppendText("ページ画像を作成しています...\r\n");
            richTextBox1.Refresh();

            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            string pythonExe = Path.Combine(projectDir, "venv", "Scripts", "python.exe");
            string autoRegionScript = Path.Combine(projectDir, "ndlocr_auto_region.py");

            if (!File.Exists(pythonExe))
            {
                richTextBox1.AppendText($"Pythonが見つかりません: {pythonExe}\r\n");
                return;
            }

            if (!File.Exists(autoRegionScript))
            {
                richTextBox1.AppendText($"スクリプトが見つかりません: {autoRegionScript}\r\n");
                return;
            }

            string pdfName = Path.GetFileNameWithoutExtension(currentPdfPath);
            string pageDir = Path.Combine(projectDir, "ocr_results", pdfName, $"page_{currentPage + 1:0000}");
            Directory.CreateDirectory(pageDir);
            string imagePath = Path.Combine(pageDir, "page.png");

            try
            {
                btnStartOcr.Enabled = false;
                Cursor = Cursors.WaitCursor;

                const int dpi = 150;
                using (Image rendered = pdfDocument.Render(currentPage, dpi, dpi, PdfRenderFlags.Annotations))
                    rendered.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);

                richTextBox1.AppendText("ページ画像作成完了\r\n");
                richTextBox1.AppendText("NDLOCR-Liteを実行しています...\r\n");
                richTextBox1.Refresh();

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

                using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

                process.OutputDataReceived += (_, ev) =>
                {
                    if (ev.Data == null) return;
                    stdout.AppendLine(ev.Data);
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            richTextBox1.AppendText(ev.Data + "\r\n");
                            richTextBox1.SelectionStart = richTextBox1.TextLength;
                            richTextBox1.ScrollToCaret();
                        }));
                    }
                };

                process.ErrorDataReceived += (_, ev) =>
                {
                    if (ev.Data == null) return;
                    stderr.AppendLine(ev.Data);
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            richTextBox1.AppendText("[ERROR] " + ev.Data + "\r\n");
                            richTextBox1.SelectionStart = richTextBox1.TextLength;
                            richTextBox1.ScrollToCaret();
                        }));
                    }
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

                richTextBox1.AppendText($"\r\nNDLOCR-Lite終了コード: {exitCode}\r\n");

                if (exitCode != 0)
                {
                    richTextBox1.AppendText("========== OCR失敗 ==========\r\n");
                    return;
                }

                if (!File.Exists(pageJson))
                {
                    richTextBox1.AppendText($"page.json が見つかりません: {pageJson}\r\n");
                    return;
                }

                if (!useUserRegions && !File.Exists(resultJson))
                {
                    richTextBox1.AppendText($"auto_layout.json が見つかりません: {resultJson}\r\n");
                    return;
                }

                List<OcrDisplayItem> ocrItems = OcrJsonParser.LoadNdlocrPageJson(pageJson);
                List<AutoLayoutRegion> autoRegions = useUserRegions
                    ? new List<AutoLayoutRegion>()
                    : OcrJsonParser.LoadAutoLayoutJson(resultJson);

                richTextBox1.AppendText($"OCR項目数: {ocrItems.Count}\r\n");
                richTextBox1.AppendText(useUserRegions
                    ? $"領域判定: ユーザー指定領域 ({regions.Count}件)\r\n\r\n"
                    : $"領域判定: 自動領域 ({autoRegions.Count}件)\r\n\r\n");

                ClearOcrResultTabs();
                nextAnnotationNumber = 1;

                List<OcrDisplayItem> displayItems = OcrSorter.SortTableItemsForDisplay(
                    ocrItems, regions, useUserRegions, autoRegions);

                Dictionary<string, int> itemNumbers = new();
                foreach (OcrDisplayItem item in displayItems)
                {
                    string type = useUserRegions
                        ? OcrProcessor.FindUserRegionType(item, regions)
                        : OcrProcessor.FindAutoLayoutRegionType(item, autoRegions);

                    if (type == "table") continue;

                    string tabType = ocrResultTextBoxes.ContainsKey(type) ? type : "unclassified";
                    int number = itemNumbers.TryGetValue(tabType, out int currentNumber) ? currentNumber + 1 : 1;
                    itemNumbers[tabType] = number;
                    ocrResultTextBoxes[tabType].AppendText($"[{number:00}] {item.Text}\r\n");
                }

                List<OcrDisplayItem> tableItems = displayItems.Where(item =>
                    (useUserRegions
                        ? OcrProcessor.FindUserRegionType(item, regions)
                        : OcrProcessor.FindAutoLayoutRegionType(item, autoRegions)) == "table")
                    .ToList();

                OcrTableDisplay.DisplayOcrTable(dgvOcrTable, tableItems);

                List<OcrDisplayItem> bodyItems = ocrItems.Where(item =>
                    (useUserRegions
                        ? OcrProcessor.FindUserRegionType(item, regions)
                        : OcrProcessor.FindAutoLayoutRegionType(item, autoRegions)) == "body")
                    .ToList();

                List<OcrDisplayItem> orderedBody = OcrSorter.SortBodyReadingOrder(bodyItems);

                string bodyReadingOrderText = string.Join("", orderedBody.Select(item => item.Text));
                string bodyReadingOrderPath = Path.Combine(pageDir, "body_reading_order.txt");
                File.WriteAllText(bodyReadingOrderPath, bodyReadingOrderText, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                richTextBox1.AppendText("\r\n========== OCR例外 ==========\r\n");
                richTextBox1.AppendText(ex + "\r\n");
            }
            finally
            {
                Cursor = Cursors.Default;
                btnStartOcr.Enabled = true;
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
