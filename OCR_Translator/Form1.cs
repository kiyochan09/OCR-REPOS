using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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

        // =========================================================
        // ページ単位の領域管理
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

        // 現在表示しているページの領域
        private List<OcrRegion> regions = new List<OcrRegion>();

        // ページごとの領域設定
        // キーは PDF のページ番号（0始まり）
        private Dictionary<int, List<OcrRegion>> pageRegions =
            new Dictionary<int, List<OcrRegion>>();

        // 自動領域判定結果（ページ単位）
        // ユーザーが補正したページ設定 pageRegions を最優先し、
        // 未補正ページではこの自動判定結果を表示する。
        private Dictionary<int, List<OcrRegion>> autoPageRegions =
            new Dictionary<int, List<OcrRegion>>();

private bool isDrawingRegion = false;
        private Point regionStartPoint;
        private Rectangle regionPreviewRectangle;

        private int movingRegionIndex = -1;
        private Point moveStartPoint;
        private Rectangle moveOriginalRectangle;

        private int hoverRegionIndex = -1;
        private ImageCoordinateHelper.ResizeMode hoverResizeMode = ImageCoordinateHelper.ResizeMode.None;
        private ImageCoordinateHelper.ResizeMode resizeMode = ImageCoordinateHelper.ResizeMode.None;
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

            // Designer.cs のイベント接続状態に依存しないよう明示的に接続
            btnStartOcr.Click -= btnStartOcr_Click;
            btnStartOcr.Click += btnStartOcr_Click;

            // 全ページの自動領域判定
            btnAutoLayout.Click -= btnAutoLayout_Click;
            btnAutoLayout.Click += btnAutoLayout_Click;
        }

        // =========================================================
        // ページ単位の領域管理
        // =========================================================

        private OcrRegion CloneRegion(OcrRegion source)
        {
            return new OcrRegion
            {
                Name = source.Name,
                Type = source.Type,
                X = source.X,
                Y = source.Y,
                Width = source.Width,
                Height = source.Height
            };
        }

        private List<OcrRegion> CloneRegions(IEnumerable<OcrRegion> source)
        {
            return source.Select(CloneRegion).ToList();
        }

        private void ShowCurrentPage()
        {
            if (pdfDocument == null)
                return;

            if (currentPage < 0 || currentPage >= pdfDocument.PageCount)
                return;

            try
            {
                const int dpi = 150;

                using Image image =
                    pdfDocument.Render(
                        currentPage,
                        dpi,
                        dpi,
                        PdfRenderFlags.Annotations
                    );

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
                    "ページを表示できませんでした。"
                        + Environment.NewLine
                        + Environment.NewLine
                        + ex.Message,
                    "PDF表示エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }





        private void RefreshRegionList()
        {
            lstRegions.Items.Clear();

            foreach (OcrRegion region in regions)
            {
                lstRegions.Items.Add(region.Name);
            }

            if (regions.Count > 0)
            {
                lstRegions.SelectedIndex = 0;
            }

            pictureBox1.Invalidate();
        }

        private void UpdatePageDisplayTitle()
        {
            if (pdfDocument == null)
                return;

            Text =
                $"OCR Translator - {currentPage + 1}/{pdfDocument.PageCount}";
        }

        private void SaveCurrentPageRegions()
        {
            if (pdfDocument == null)
                return;

            _layoutStorage.TrySaveCurrentPageRegions(
                currentPage,
                regions,
                pageRegions,
                autoPageRegions);
        }

        private void LoadCurrentPageRegions()
        {
            regions.Clear();
            regions.AddRange(
                _layoutStorage.LoadPageRegions(
                    currentPage,
                    pageRegions,
                    autoPageRegions));

            RefreshRegionList();
        }
        private void SwitchToPage(int pageIndex)
        {
            if (pdfDocument == null)
                return;

            if (pageIndex < 0 || pageIndex >= pdfDocument.PageCount)
                return;

            SaveCurrentPageRegions();

            currentPage = pageIndex;

            LoadCurrentPageRegions();

            ShowCurrentPage();
        }

        private void btnOpenPdf_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog dialog = new OpenFileDialog();

            dialog.Filter =
                "PDF files (*.pdf)|*.pdf|" +
                "All files (*.*)|*.*";

            dialog.Title = "PDFファイルを開く";

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            try
            {
                // 以前のPDFを解放
                if (pdfDocument != null)
                {
                    pdfDocument.Dispose();
                    pdfDocument = null;
                }

                currentPdfPath = dialog.FileName;

                // 新しいPDFなので、前のPDFのページ別設定を破棄
                pageRegions.Clear();
                autoPageRegions.Clear();
                regions.Clear();
                lstRegions.Items.Clear();

                // PDFを読み込む
                pdfDocument = PdfDocument.Load(currentPdfPath);

                // 最初のページ
                currentPage = 0;

                // 最初のページの領域を読み込む
                LoadCurrentPageRegions();

                // ページを表示
                ShowCurrentPage();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "ページを表示できませんでした。"
                        + Environment.NewLine
                        + Environment.NewLine
                        + ex.Message,
                    "PDF表示エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void ShowPdfPage(int pageIndex)
        {
            if (pdfDocument == null)
                return;

            if (pageIndex < 0 || pageIndex >= pdfDocument.PageCount)
                return;

            try
            {
                // PDFを高解像度でレンダリング
                Image image = pdfDocument.Render(
                    pageIndex,
                    2480,
                    3508,
                    300,
                    300,
                    PdfRenderFlags.Annotations);

                Image oldImage = pictureBox1.Image;
                pictureBox1.Image = image;

                if (oldImage != null)
                    oldImage.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "ページを表示できませんでした。\n\n" + ex.Message,
                    "表示エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void InitializeOcrResultView()
        {
            // 右側OCR表示領域をタブ化する。
            tabOcrResult = new TabControl
            {
                Dock = DockStyle.Fill
            };

            tabOcrText = new TabPage("本文");
            tabOcrTable = new TabPage("表");

            // 現在のRichTextBoxをOCR結果タブへ移動する。
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

            // 表表示用DataGridView
            dgvOcrTable = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = true,
                RowHeadersVisible = false,
                AutoSizeRowsMode =
                    DataGridViewAutoSizeRowsMode.AllCells,
                AutoSizeColumnsMode =
                    DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode =
                    DataGridViewSelectionMode.CellSelect,
                MultiSelect = false
            };

            dgvOcrTable.DefaultCellStyle.WrapMode =
                DataGridViewTriState.True;

            tabOcrTable.Controls.Add(dgvOcrTable);

            // 右側セルにTabControlを1つだけ配置する。
            tableLayoutPanel1.Controls.Add(
                tabOcrResult,
                1,
                0);
        }

        private void AddOcrResultTab(string type, string title)
        {
            if (tabOcrResult == null)
                return;

            RichTextBox resultBox = new RichTextBox
            {
                Dock = DockStyle.Fill
            };
            resultBox.MouseClick += richTextBox1_MouseClick;

            TabPage page = new TabPage(title);
            page.Controls.Add(resultBox);
            tabOcrResult.TabPages.Add(page);
            ocrResultTextBoxes[type] = resultBox;
        }

        private void ClearOcrResultTabs()
        {
            foreach (RichTextBox resultBox in ocrResultTextBoxes.Values)
            {
                resultBox.Clear();
            }
        }

        // 
        // btnRegionSettings
        // 
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
                MessageBox.Show(
                    "削除する領域を選択してください。",
                    "領域未選択",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            // 現在選択されている領域を削除
            regions.RemoveAt(index);
            lstRegions.Items.RemoveAt(index);

            // 現在ページの領域情報を更新
            pageRegions[currentPage] = CloneRegions(regions);

            // 選択状態を解除
            lstRegions.ClearSelected();

            // 数値入力欄をクリア
            numX.Value = 0;
            numY.Value = 0;
            numWidth.Value = 0;
            numHeight.Value = 0;

            // 画像を再描画
            pictureBox1.Invalidate();
        }

        private void btnSaveLayout_Click(object sender, EventArgs e)
        {
            SaveCurrentPageRegions();

            PageLayout layout = _layoutStorage.BuildPageLayout(pageRegions);

            string path = Path.Combine(
                Application.StartupPath,
                "page_layout.json");

            try
            {
                _layoutStorage.SaveToJsonFile(layout, path);

                MessageBox.Show(
                    $"ページ単位の設定を保存しました。\n\n" +
                    $"保存ページ数: {pageRegions.Count}\n" +
                    $"ファイル: {path}",
                    "保存完了",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "保存に失敗しました。\n\n" + ex.Message,
                    "保存エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void btnNextPage_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null)
            {
                return;
            }

            if (currentPage < pdfDocument.PageCount - 1)
            {
                SwitchToPage(currentPage + 1);
            }
        }

        private void btnPrevPage_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null)
            {
                return;
            }

            if (currentPage > 0)
            {
                SwitchToPage(currentPage - 1);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SaveCurrentPageRegions();

            pdfDocument?.Dispose();
            pdfDocument = null;

            base.OnFormClosed(e);
        }

        private void lstRegions_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = lstRegions.SelectedIndex;

            if (index < 0 || index >= regions.Count)
                return;

            OcrRegion region = regions[index];

            numX.Value = region.X;
            numY.Value = region.Y;
            numWidth.Value = region.Width;
            numHeight.Value = region.Height;

            string displayName = region.Name;

            if (cmbRegionType.Items.Contains(displayName))
            {
                isUpdatingRegionTypeCombo = true;

                try
                {
                    cmbRegionType.SelectedItem = displayName;
                }
                finally
                {
                    isUpdatingRegionTypeCombo = false;
                }
            }
        }

        private void cmbRegionType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (isUpdatingRegionTypeCombo)
                return;

            int index = lstRegions.SelectedIndex;

            if (index < 0 || index >= regions.Count)
                return;

            string newName = cmbRegionType.Text;

            if (string.IsNullOrWhiteSpace(newName))
                return;

            OcrRegion region = regions[index];

            region.Name = newName;
            region.Type = GetRegionType();

            lstRegions.Items[index] = newName;

            pageRegions[currentPage] = _layoutStorage.CloneRegions(regions);
            // または ForceSavePageRegions を使う
            _layoutStorage.ForceSavePageRegions(currentPage, regions, pageRegions);

            pictureBox1.Invalidate();
        }

        private void btnUpdateRegion_Click(object sender, EventArgs e)
        {
            int index = lstRegions.SelectedIndex;

            if (index < 0 || index >= regions.Count)
            {
                MessageBox.Show(
                    "更新する領域をListBoxから選択してください。",
                    "領域未選択",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

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

            // ユーザーが自動判定結果を補正したので、
            // 現在の領域をユーザー設定として保存する。
            pageRegions[currentPage] = CloneRegions(regions);

            pictureBox1.Invalidate();
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (pictureBox1.Image == null)
                return;

            float scaleX =
                (float)pictureBox1.ClientSize.Width /
                pictureBox1.Image.Width;

            float scaleY =
                (float)pictureBox1.ClientSize.Height /
                pictureBox1.Image.Height;

            float scale = Math.Min(scaleX, scaleY);

            float displayWidth =
                pictureBox1.Image.Width * scale;

            float displayHeight =
                pictureBox1.Image.Height * scale;

            float offsetX =
                (pictureBox1.ClientSize.Width - displayWidth) / 2;

            float offsetY =
                (pictureBox1.ClientSize.Height - displayHeight) / 2;



            for (int i = 0; i < regions.Count; i++)
            {
                OcrRegion region = regions[i];

                Color regionColor = GetRegionColor(region.Type);

                using Pen regionPen = new Pen(regionColor, 2);

                Rectangle imageRect =
                    new Rectangle(
                        region.X,
                        region.Y,
                        region.Width,
                        region.Height);

                Rectangle screenRect =
                    ImageCoordinateHelper.ImageToScreen(imageRect, pictureBox1);

                e.Graphics.DrawRectangle(
                    regionPen,
                    screenRect);

                // 選択中の領域に四隅のハンドルを表示
                if (i == lstRegions.SelectedIndex)
                {
                    using Brush handleBrush =
                        new SolidBrush(Color.White);

                    using Pen handlePen =
                        new Pen(Color.Red, 1);

                    int handleSize = ResizeHandleSize;

                    Point[] handles =
                    {
                new Point(
                    screenRect.Left,
                    screenRect.Top),

                new Point(
                    screenRect.Right,
                    screenRect.Top),

                new Point(
                    screenRect.Left,
                    screenRect.Bottom),

                new Point(
                    screenRect.Right,
                    screenRect.Bottom)
            };

                    foreach (Point handle in handles)
                    {
                        Rectangle handleRect =
                            new Rectangle(
                                handle.X - handleSize / 2,
                                handle.Y - handleSize / 2,
                                handleSize,
                                handleSize);

                        e.Graphics.FillRectangle(
                            handleBrush,
                            handleRect);

                        e.Graphics.DrawRectangle(
                            handlePen,
                            handleRect);
                    }
                }
            }

            // 新規領域作成中の青い矩形
            if (isDrawingRegion)
            {
                using Pen previewPen =
                    new Pen(Color.Blue, 2);

                e.Graphics.DrawRectangle(
                    previewPen,
                    regionPreviewRectangle);
            }
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            if (pictureBox1.Image == null)
                return;

            int hitIndex = hoverRegionIndex;

            if (hitIndex >= 0 &&
                hitIndex < regions.Count)
            {
                lstRegions.SelectedIndex = hitIndex;

                OcrRegion region =
                    regions[hitIndex];

                Rectangle screenRect =
                    ImageCoordinateHelper.ImageToScreen(
                        new Rectangle(
                            region.X,
                            region.Y,
                            region.Width,
                            region.Height),
                        pictureBox1);

                // MouseMoveで決定済みの操作方法をそのまま使用
                ResizeMode mode = hoverResizeMode;

                if (mode != ResizeMode.None)
                {
                    resizeMode = mode;
                    movingRegionIndex = -1;

                    resizeStartPoint = e.Location;

                    resizeOriginalRectangle =
                        new Rectangle(
                            region.X,
                            region.Y,
                            region.Width,
                            region.Height);

                    return;
                }

                // 中央をクリック → 移動
                movingRegionIndex = hitIndex;

                moveStartPoint = e.Location;

                moveOriginalRectangle =
                    new Rectangle(
                        region.X,
                        region.Y,
                        region.Width,
                        region.Height);

                pictureBox1.Cursor = Cursors.SizeAll;

                return;
            }

            // 何もない場所 → 新規領域作成
            isDrawingRegion = true;

            regionStartPoint = e.Location;

            regionPreviewRectangle =
                new Rectangle(
                    e.X,
                    e.Y,
                    0,
                    0);
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (pictureBox1.Image == null)
                return;

            // マウスが近づいた領域を自動選択
            if (resizeMode == ResizeMode.None &&
                movingRegionIndex < 0 &&
                !isDrawingRegion)
            {
                int nearIndex = ImageCoordinateHelper.HitTestRegionNear(
                    e.Location,
                    20,
                    regions,
                    pictureBox1);

                if (nearIndex >= 0 &&
                    lstRegions.SelectedIndex != nearIndex)
                {
                    lstRegions.SelectedIndex = nearIndex;
                    pictureBox1.Invalidate();
                }
            }

            // ==========================================
            // マウス位置から対象領域と操作方法を決定
            // ==========================================
            if (resizeMode == ResizeMode.None &&
                movingRegionIndex < 0 &&
                !isDrawingRegion)
            {
                hoverRegionIndex = -1;
                hoverResizeMode = ResizeMode.None;

                int nearIndex = ImageCoordinateHelper.HitTestRegionNear(
                    e.Location,
                    20,
                    regions,
                    pictureBox1);

                if (nearIndex >= 0)
                {
                    hoverRegionIndex = nearIndex;

                    // 自動選択
                    if (lstRegions.SelectedIndex != nearIndex)
                    {
                        lstRegions.SelectedIndex = nearIndex;
                        pictureBox1.Invalidate();
                    }

                    OcrRegion hoverRegion =
                        regions[nearIndex];

                    Rectangle hoverRect =
                        ImageCoordinateHelper.ImageToScreen(
                            new Rectangle(
                                hoverRegion.X,
                                hoverRegion.Y,
                                hoverRegion.Width,
                                hoverRegion.Height),
                            pictureBox1);

                    ResizeMode mode =
                        ImageCoordinateHelper.GetResizeMode(e.Location, hoverRect);

                    hoverResizeMode = mode;

                    switch (mode)
                    {
                        case ResizeMode.Left:
                        case ResizeMode.Right:
                            pictureBox1.Cursor = Cursors.SizeWE;
                            break;

                        case ResizeMode.Top:
                        case ResizeMode.Bottom:
                            pictureBox1.Cursor = Cursors.SizeNS;
                            break;

                        case ResizeMode.TopLeft:
                        case ResizeMode.BottomRight:
                            pictureBox1.Cursor = Cursors.SizeNWSE;
                            break;

                        case ResizeMode.TopRight:
                        case ResizeMode.BottomLeft:
                            pictureBox1.Cursor = Cursors.SizeNESW;
                            break;

                        default:
                            pictureBox1.Cursor = Cursors.SizeAll;
                            break;
                    }
                }
                else
                {
                    pictureBox1.Cursor = Cursors.Default;
                }
            }

            // ==========================================
            // 既存領域のリサイズ
            // ==========================================
            if (resizeMode != ResizeMode.None)
            {
                if (lstRegions.SelectedIndex < 0)
                    return;

                OcrRegion region = regions[lstRegions.SelectedIndex];

                float scaleX =
                    (float)pictureBox1.ClientSize.Width /
                    pictureBox1.Image.Width;

                float scaleY =
                    (float)pictureBox1.ClientSize.Height /
                    pictureBox1.Image.Height;

                float scale = Math.Min(scaleX, scaleY);

                int deltaX =
                    (int)((e.X - resizeStartPoint.X) / scale);

                int deltaY =
                    (int)((e.Y - resizeStartPoint.Y) / scale);

                int newX = resizeOriginalRectangle.X;
                int newY = resizeOriginalRectangle.Y;
                int newWidth = resizeOriginalRectangle.Width;
                int newHeight = resizeOriginalRectangle.Height;

                const int minSize = 20;

                switch (resizeMode)
                {
                    case ResizeMode.Left:
                        newX = resizeOriginalRectangle.X + deltaX;
                        newWidth = resizeOriginalRectangle.Width - deltaX;
                        break;

                    case ResizeMode.Right:
                        newWidth = resizeOriginalRectangle.Width + deltaX;
                        break;

                    case ResizeMode.Top:
                        newY = resizeOriginalRectangle.Y + deltaY;
                        newHeight = resizeOriginalRectangle.Height - deltaY;
                        break;

                    case ResizeMode.Bottom:
                        newHeight = resizeOriginalRectangle.Height + deltaY;
                        break;

                    case ResizeMode.TopLeft:
                        newX = resizeOriginalRectangle.X + deltaX;
                        newWidth = resizeOriginalRectangle.Width - deltaX;
                        newY = resizeOriginalRectangle.Y + deltaY;
                        newHeight = resizeOriginalRectangle.Height - deltaY;
                        break;

                    case ResizeMode.TopRight:
                        newWidth = resizeOriginalRectangle.Width + deltaX;
                        newY = resizeOriginalRectangle.Y + deltaY;
                        newHeight = resizeOriginalRectangle.Height - deltaY;
                        break;

                    case ResizeMode.BottomLeft:
                        newX = resizeOriginalRectangle.X + deltaX;
                        newWidth = resizeOriginalRectangle.Width - deltaX;
                        newHeight = resizeOriginalRectangle.Height + deltaY;
                        break;

                    case ResizeMode.BottomRight:
                        newWidth = resizeOriginalRectangle.Width + deltaX;
                        newHeight = resizeOriginalRectangle.Height + deltaY;
                        break;
                }

                // 最小サイズ
                if (newWidth < minSize)
                {
                    newWidth = minSize;

                    if (resizeMode == ResizeMode.Left ||
                        resizeMode == ResizeMode.TopLeft ||
                        resizeMode == ResizeMode.BottomLeft)
                    {
                        newX =
                            resizeOriginalRectangle.Right - minSize;
                    }
                }

                if (newHeight < minSize)
                {
                    newHeight = minSize;

                    if (resizeMode == ResizeMode.Top ||
                        resizeMode == ResizeMode.TopLeft ||
                        resizeMode == ResizeMode.TopRight)
                    {
                        newY =
                            resizeOriginalRectangle.Bottom - minSize;
                    }
                }

                // ページ外にはみ出さないようにする
                newX = Math.Max(0, newX);
                newY = Math.Max(0, newY);

                if (newX + newWidth > pictureBox1.Image.Width)
                {
                    newWidth =
                        pictureBox1.Image.Width - newX;
                }

                if (newY + newHeight > pictureBox1.Image.Height)
                {
                    newHeight =
                        pictureBox1.Image.Height - newY;
                }

                // 領域を更新
                region.X = newX;
                region.Y = newY;
                region.Width = newWidth;
                region.Height = newHeight;

                // 数値入力欄にも反映
                numX.Value =
                    Math.Min(
                        numX.Maximum,
                        Math.Max(numX.Minimum, region.X));

                numY.Value =
                    Math.Min(
                        numY.Maximum,
                        Math.Max(numY.Minimum, region.Y));

                numWidth.Value =
                    Math.Min(
                        numWidth.Maximum,
                        Math.Max(numWidth.Minimum, region.Width));

                numHeight.Value =
                    Math.Min(
                        numHeight.Maximum,
                        Math.Max(numHeight.Minimum, region.Height));

                pictureBox1.Invalidate();

                return;
            }


            // ==========================================
            // 既存領域の移動
            // ==========================================
            if (movingRegionIndex >= 0)
            {
                OcrRegion region =
                    regions[movingRegionIndex];

                float scaleX =
                    (float)pictureBox1.ClientSize.Width /
                    pictureBox1.Image.Width;

                float scaleY =
                    (float)pictureBox1.ClientSize.Height /
                    pictureBox1.Image.Height;

                float scale = Math.Min(scaleX, scaleY);

                int deltaX =
                    (int)((e.X - moveStartPoint.X) / scale);

                int deltaY =
                    (int)((e.Y - moveStartPoint.Y) / scale);

                int newX =
                    moveOriginalRectangle.X + deltaX;

                int newY =
                    moveOriginalRectangle.Y + deltaY;

                newX = Math.Max(0, newX);
                newY = Math.Max(0, newY);

                if (newX + region.Width >
                    pictureBox1.Image.Width)
                {
                    newX =
                        pictureBox1.Image.Width -
                        region.Width;
                }

                if (newY + region.Height >
                    pictureBox1.Image.Height)
                {
                    newY =
                        pictureBox1.Image.Height -
                        region.Height;
                }

                region.X = newX;
                region.Y = newY;

                numX.Value =
                    Math.Min(
                        numX.Maximum,
                        Math.Max(numX.Minimum, region.X));

                numY.Value =
                    Math.Min(
                        numY.Maximum,
                        Math.Max(numY.Minimum, region.Y));

                pictureBox1.Invalidate();

                return;
            }


            // ==========================================
            // 新規領域作成
            // ==========================================
            if (!isDrawingRegion)
                return;

            int drawX =
                Math.Min(regionStartPoint.X, e.X);

            int drawY =
                Math.Min(regionStartPoint.Y, e.Y);

            int drawWidth =
                Math.Abs(e.X - regionStartPoint.X);

            int drawHeight =
                Math.Abs(e.Y - regionStartPoint.Y);

            regionPreviewRectangle =
                new Rectangle(
                    drawX,
                    drawY,
                    drawWidth,
                    drawHeight);

            pictureBox1.Invalidate();
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

            if (movingRegionIndex >= 0)
            {
                movingRegionIndex = -1;

                pictureBox1.Invalidate();

                return;
            }

            if (!isDrawingRegion)
                return;

            isDrawingRegion = false;

            if (regionPreviewRectangle.Width < 5 ||
                regionPreviewRectangle.Height < 5)
            {
                pictureBox1.Invalidate();
                return;
            }

            Rectangle imageRect =
                ImageCoordinateHelper.ScreenToImage(
                    regionPreviewRectangle,
                    pictureBox1);

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

            // 追加した領域を選択
            lstRegions.SelectedIndex = index;

            // NumericUpDownに値を表示
            numX.Value = Math.Min(numX.Maximum, Math.Max(numX.Minimum, region.X));
            numY.Value = Math.Min(numY.Maximum, Math.Max(numY.Minimum, region.Y));
            numWidth.Value = Math.Min(numWidth.Maximum, Math.Max(numWidth.Minimum, region.Width));
            numHeight.Value = Math.Min(numHeight.Maximum, Math.Max(numHeight.Minimum, region.Height));

            pictureBox1.Invalidate();
        }

        


        private Color GetRegionColor(string type)
        {
            switch (type)
            {
                case "body":
                    return Color.Blue;

                case "heading":
                    return Color.Green;

                case "footnote":
                    return Color.Gray;

                case "table":
                    return Color.Orange;

                case "image":
                    return Color.DeepSkyBlue;

                case "map":
                    return Color.Goldenrod;

                case "ignore":
                    return Color.Red;

                default:
                    return Color.Black;
            }
        }
                
        

        

        // =========================================================
        // OCR結果表示用の表内部読み順
        //
        // ユーザー指定の「表」領域に属するOCRだけを対象とする。
        // 本文など、表以外の項目の順序は変更しない。
        //
        // 表では単純な Y 座標による「行」判定を行わない。
        // OCRの文字列ブロックが複数段に分かれた場合でも、
        // X方向の位置関係を基準に表内部のまとまりを作る。
        //
        // ※文字列内容による特別扱いは行わない。
        // =========================================================
                
        // =========================================================
        // 全ページ自動領域判定
        //
        // PDFを開いた後、OCR開始とは別に実行する。
        // 全ページについて NDLOCR-Lite + ndlocr_auto_region.py を実行し、
        // auto_layout.json をページ単位で保存する。
        // ユーザーが補正済みの pageRegions は上書きしない。
        // =========================================================
        private async void btnAutoLayout_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null || string.IsNullOrWhiteSpace(currentPdfPath))
            {
                MessageBox.Show(
                    "先にPDFを開いてください。",
                    "領域自動判定",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            string pythonExe = Path.Combine(projectDir, "venv", "Scripts", "python.exe");
            string autoRegionScript = Path.Combine(projectDir, "ndlocr_auto_region.py");

            if (!File.Exists(pythonExe))
            {
                MessageBox.Show(
                    $"Pythonが見つかりません。\n{pythonExe}",
                    "領域自動判定",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (!File.Exists(autoRegionScript))
            {
                MessageBox.Show(
                    $"自動領域判定スクリプトが見つかりません。\n{autoRegionScript}",
                    "領域自動判定",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            string pdfName = Path.GetFileNameWithoutExtension(currentPdfPath);
            string outputRoot = Path.Combine(
                projectDir,
                "ocr_results",
                pdfName);

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

                // 全ページ処理中の状態を専用ウィンドウで表示する。
                progressForm = new ProgressForm(pdfDocument.PageCount);
                progressForm.StartPosition = FormStartPosition.CenterParent;
                progressForm.Show(this);
                progressForm.UpdateProgress(0, pdfDocument.PageCount, "準備中...");

                richTextBox1.Clear();
                richTextBox1.AppendText("========== 全ページ領域自動判定 ==========" + Environment.NewLine);
                richTextBox1.AppendText($"PDF: {Path.GetFileName(currentPdfPath)}" + Environment.NewLine);
                richTextBox1.AppendText($"ページ数: {pdfDocument.PageCount}" + Environment.NewLine + Environment.NewLine);

                // 既存の自動判定結果をクリアする。
                // ユーザー補正済み pageRegions は保持する。
                autoPageRegions.Clear();

                for (int pageIndex = 0; pageIndex < pdfDocument.PageCount; pageIndex++)
                {
                    string pageMessage =
                        $"ページ {pageIndex + 1} / {pdfDocument.PageCount} を処理しています...";

                    richTextBox1.AppendText(
                        $"---------- {pageIndex + 1}/{pdfDocument.PageCount} ページ ----------" +
                        Environment.NewLine);
                    richTextBox1.AppendText(pageMessage + Environment.NewLine);
                    richTextBox1.Refresh();
                    progressForm?.UpdateProgress(
                        pageIndex,
                        pdfDocument.PageCount,
                        pageMessage + "\r\nNDLOCR-Liteを実行しています。");

                    string pageDir = Path.Combine(
                        outputRoot,
                        $"page_{pageIndex + 1:0000}");
                    Directory.CreateDirectory(pageDir);

                    string imagePath = Path.Combine(pageDir, "page.png");
                    string resultJson = Path.Combine(pageDir, "auto_layout.json");

                    try
                    {
                        const int dpi = 150;

                        using (Image rendered = pdfDocument.Render(
                            pageIndex,
                            dpi,
                            dpi,
                            PdfRenderFlags.Annotations))
                        {
                            rendered.Save(
                                imagePath,
                                System.Drawing.Imaging.ImageFormat.Png);
                        }

                        OcrProcessor.ProcessResult result = await OcrProcessor.RunAutoRegionProcessAsync(
                            pythonExe,
                            autoRegionScript,
                            projectDir,
                            imagePath,
                            pageDir);

                        string log =
                            "[STDOUT]\r\n" + result.Stdout +
                            "\r\n[STDERR]\r\n" + result.Stderr;

                        File.WriteAllText(
                            Path.Combine(pageDir, "ndlocr_run.log"),
                            log,
                            new UTF8Encoding(false));

                        if (result.ExitCode != 0)
                        {
                            failureCount++;
                            richTextBox1.AppendText(
                                $"失敗: 終了コード {result.ExitCode}" + Environment.NewLine);
                            progressForm?.UpdateProgress(
                                pageIndex + 1,
                                pdfDocument.PageCount,
                                $"ページ {pageIndex + 1} 失敗\r\n終了コード: {result.ExitCode}");
                            continue;
                        }

                        if (!File.Exists(resultJson))
                        {
                            failureCount++;
                            richTextBox1.AppendText(
                                "失敗: auto_layout.json が生成されませんでした。" + Environment.NewLine);
                            progressForm?.UpdateProgress(
                                pageIndex + 1,
                                pdfDocument.PageCount,
                                $"ページ {pageIndex + 1} 失敗\r\nauto_layout.json がありません。");
                            continue;
                        }

                        List<AutoLayoutRegion> detected =
                            OcrJsonParser.LoadAutoLayoutJson(resultJson);

                        List<OcrRegion> converted = detected
                            .Select(OcrProcessor.ConvertAutoLayoutRegion)
                            .ToList();

                        autoPageRegions[pageIndex] = converted;

                        successCount++;
                        richTextBox1.AppendText(
                            $"成功: 自動領域 {converted.Count}件" + Environment.NewLine);
                        progressForm?.UpdateProgress(
                            pageIndex + 1,
                            pdfDocument.PageCount,
                            $"ページ {pageIndex + 1} 完了\r\n自動領域 {converted.Count}件");
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        richTextBox1.AppendText(
                            "失敗: " + ex.Message + Environment.NewLine);
                        progressForm?.UpdateProgress(
                            pageIndex + 1,
                            pdfDocument.PageCount,
                            $"ページ {pageIndex + 1} 失敗\r\n{ex.Message}");
                    }
                }

                // 現在ページを、ユーザー補正済みならそれを、
                // 未補正なら自動判定結果を表示する。
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
                richTextBox1.AppendText(
                    Environment.NewLine +
                    "========== 全ページ判定例外 ==========" + Environment.NewLine +
                    ex + Environment.NewLine);
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

            // 現在ページのユーザー指定領域を確定する。
            SaveCurrentPageRegions();

            richTextBox1.Clear();
            richTextBox1.AppendText("========== OCR開始 ==========\r\n");
            richTextBox1.AppendText($"現在ページ: {currentPage + 1}\r\n");

            // ---------------------------------------------------------
            // 重要:
            // ユーザーが領域を指定している場合は、ユーザー指定を
            // 最優先とする。自動領域判定は分類には使用しない。
            // ---------------------------------------------------------
            bool useUserRegions = regions.Count > 0;

            if (useUserRegions)
            {
                richTextBox1.AppendText(
                    $"ユーザー指定領域: {regions.Count}件\r\n");

                for (int i = 0; i < regions.Count; i++)
                {
                    OcrRegion r = regions[i];
                    richTextBox1.AppendText(
                        $"  [{i + 1:00}] {r.Name} / {r.Type} " +
                        $"x={r.X}, y={r.Y}, " +
                        $"width={r.Width}, height={r.Height}\r\n");
                }
            }
            else
            {
                richTextBox1.AppendText(
                    "ユーザー指定領域がありません。自動領域判定を使用します。\r\n");
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
            string pageDir = Path.Combine(
                projectDir,
                "ocr_results",
                pdfName,
                $"page_{currentPage + 1:0000}");

            Directory.CreateDirectory(pageDir);
            string imagePath = Path.Combine(pageDir, "page.png");

            try
            {
                btnStartOcr.Enabled = false;
                Cursor = Cursors.WaitCursor;

                const int dpi = 150;
                using (Image rendered = pdfDocument.Render(
                    currentPage,
                    dpi,
                    dpi,
                    PdfRenderFlags.Annotations))
                {
                    rendered.Save(
                        imagePath,
                        System.Drawing.Imaging.ImageFormat.Png);
                }

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

                using var process = new Process
                {
                    StartInfo = psi,
                    EnableRaisingEvents = true
                };

                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                var completion = new TaskCompletionSource<int>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null)
                        return;

                    stdout.AppendLine(e.Data);

                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            richTextBox1.AppendText(e.Data + "\r\n");
                            richTextBox1.SelectionStart = richTextBox1.TextLength;
                            richTextBox1.ScrollToCaret();
                        }));
                    }
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null)
                        return;

                    stderr.AppendLine(e.Data);

                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            richTextBox1.AppendText(
                                "[ERROR] " + e.Data + "\r\n");
                            richTextBox1.SelectionStart = richTextBox1.TextLength;
                            richTextBox1.ScrollToCaret();
                        }));
                    }
                };

                process.Exited += (_, _) =>
                    completion.TrySetResult(process.ExitCode);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                int exitCode = await completion.Task;

                string log =
                    "[STDOUT]\r\n" + stdout +
                    "\r\n[STDERR]\r\n" + stderr;

                File.WriteAllText(
                    Path.Combine(pageDir, "ndlocr_run.log"),
                    log,
                    new UTF8Encoding(false));

                string pageJson = Path.Combine(
                    pageDir,
                    "page.json");

                string resultJson = Path.Combine(
                    pageDir,
                    "auto_layout.json");

                richTextBox1.AppendText(
                    $"\r\nNDLOCR-Lite終了コード: {exitCode}\r\n");

                if (exitCode != 0)
                {
                    richTextBox1.AppendText(
                        "========== OCR失敗 ==========\r\n");
                    return;
                }

                if (!File.Exists(pageJson))
                {
                    richTextBox1.AppendText(
                        $"page.json が見つかりません: {pageJson}\r\n");
                    return;
                }

                // 自動領域はユーザー指定がない場合だけ必要。
                if (!useUserRegions && !File.Exists(resultJson))
                {
                    richTextBox1.AppendText(
                        $"auto_layout.json が見つかりません: {resultJson}\r\n");
                    return;
                }

                List<OcrDisplayItem> ocrItems =
                    OcrJsonParser.LoadNdlocrPageJson(pageJson);

                List<AutoLayoutRegion> autoRegions =
                    useUserRegions
                        ? new List<AutoLayoutRegion>()
                        : OcrJsonParser.LoadAutoLayoutJson(resultJson);

                richTextBox1.AppendText(
                    $"OCR項目数: {ocrItems.Count}\r\n");

                if (useUserRegions)
                {
                    richTextBox1.AppendText(
                        $"領域判定: ユーザー指定領域 ({regions.Count}件)\r\n\r\n");
                }
                else
                {
                    richTextBox1.AppendText(
                        $"領域判定: 自動領域 ({autoRegions.Count}件)\r\n\r\n");
                }

                // OCR処理中のログは結果画面に残さず、補正対象だけを表示する。
                ClearOcrResultTabs();
                nextAnnotationNumber = 1;

                // =========================================================
                // OCR結果の領域分類
                //
                // ユーザー指定領域がある場合:
                //     必ずユーザー指定領域を使用する。
                //
                // 指定がない場合:
                //     auto_layout.jsonを使用する。
                // =========================================================

                // NDLOCR-Liteの検出順ではなく、表内部だけは
                // 表として自然な「上→下、左→右」の順にして表示する。
                List<OcrDisplayItem> displayItems =
                    OcrSorter.SortTableItemsForDisplay(
                        ocrItems,
                        regions,
                        useUserRegions,
                        autoRegions);

                Dictionary<string, int> itemNumbers = new();

                foreach (OcrDisplayItem item in displayItems)
                {
                    string type = useUserRegions
                        ? OcrProcessor.FindUserRegionType(item, regions)
                        : OcrProcessor.FindAutoLayoutRegionType(item, autoRegions);

                    if (type == "table")
                        continue;

                    string tabType = ocrResultTextBoxes.ContainsKey(type)
                        ? type
                        : "unclassified";

                    int number = itemNumbers.TryGetValue(tabType, out int currentNumber)
                        ? currentNumber + 1
                        : 1;
                    itemNumbers[tabType] = number;

                    ocrResultTextBoxes[tabType].AppendText(
                        $"[{number:00}] {item.Text}\r\n");
                }



                // =========================================================
                // 表OCRを右側の「表」タブへ表示
                // =========================================================

                List<OcrDisplayItem> tableItems =
                    new List<OcrDisplayItem>();

                foreach (OcrDisplayItem item in displayItems)
                {
                    string type = useUserRegions
                        ? OcrProcessor.FindUserRegionType(item, regions)
                        : OcrProcessor.FindAutoLayoutRegionType(item, autoRegions);

                    if (type == "table")
                    {
                        tableItems.Add(item);
                    }
                }

                OcrTableDisplay.DisplayOcrTable(dgvOcrTable, tableItems);

                // =========================================================
                // 本文OCRだけを抽出
                // =========================================================

                List<OcrDisplayItem> bodyItems =
                    new List<OcrDisplayItem>();

                foreach (OcrDisplayItem item in ocrItems)
                {
                    string type = useUserRegions
                        ? OcrProcessor.FindUserRegionType(item, regions)
                        : OcrProcessor.FindAutoLayoutRegionType(item, autoRegions);

                    if (type == "body")
                    {
                        bodyItems.Add(item);
                    }
                }

                List<OcrDisplayItem> orderedBody =
                    OcrSorter.SortBodyReadingOrder(bodyItems);

                // =========================================================
                // 本文読み順テキストを保存
                // =========================================================

                string bodyReadingOrderText =
                    string.Join(
                        "",
                        orderedBody.Select(
                            item => item.Text));

                string bodyReadingOrderPath = Path.Combine(
                    pageDir,
                    "body_reading_order.txt");

                File.WriteAllText(
                    bodyReadingOrderPath,
                    bodyReadingOrderText,
                    new UTF8Encoding(false));

            }
            catch (Exception ex)
            {
                richTextBox1.AppendText(
                    "\r\n========== OCR例外 ==========\r\n");
                richTextBox1.AppendText(ex + "\r\n");
            }
            finally
            {
                Cursor = Cursors.Default;
                btnStartOcr.Enabled = true;
            }
        }

        private void btnRegionSettings_Click(object? sender, EventArgs e)
        {
            if (pdfDocument == null || pictureBox1.Image == null)
            {
                MessageBox.Show(
                    "先にPDFを開いてください。",
                    "領域設定",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            // 領域設定モードを開始
            isDrawingRegion = true;

            // 新規領域作成の開始位置をリセット
            regionPreviewRectangle = Rectangle.Empty;

            pictureBox1.Focus();
            pictureBox1.Cursor = Cursors.Cross;
            Cursor = Cursors.Cross;

            pictureBox1.Invalidate();
        }

        private void richTextBox1_MouseClick(object? sender, MouseEventArgs e)
        {
            if (sender is not RichTextBox resultBox)
                return;

            // OCR結果の右端をクリックした場合だけ、注釈番号を付け外しする。
            if (e.Button != MouseButtons.Left ||
                e.X < resultBox.ClientSize.Width - 48)
            {
                return;
            }

            int characterIndex = resultBox.GetCharIndexFromPosition(e.Location);
            int lineIndex = resultBox.GetLineFromCharIndex(characterIndex);

            if (lineIndex < 0 || lineIndex >= resultBox.Lines.Length)
                return;

            ToggleAnnotationNumber(resultBox, lineIndex);
        }

        private void btnAddAnnotationNumber_Click(object? sender, EventArgs e)
        {
            RichTextBox? resultBox = tabOcrResult?.SelectedTab?
                .Controls
                .OfType<RichTextBox>()
                .FirstOrDefault();

            if (resultBox == null)
            {
                MessageBox.Show(
                    "本文・見出し・注釈文・図のタブで、対象行を選択してください。",
                    "注釈番号",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            int lineIndex = resultBox.GetLineFromCharIndex(resultBox.SelectionStart);
            ToggleAnnotationNumber(resultBox, lineIndex);
        }

        private void ToggleAnnotationNumber(RichTextBox resultBox, int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= resultBox.Lines.Length)
                return;

            string line = resultBox.Lines[lineIndex];

            if (string.IsNullOrWhiteSpace(line))
                return;

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
    }
}
