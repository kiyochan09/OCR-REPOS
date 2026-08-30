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
        public class OcrRegion
        {
            public string Name { get; set; } = "本文";
            public string Type { get; set; } = "body";
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        public class PageLayout
        {
            public TemplateSettings Template { get; set; } = new TemplateSettings();

            public Dictionary<string, PageSettings> Pages { get; set; }
                = new Dictionary<string, PageSettings>();
        }

        public class TemplateSettings
        {
            public string Name { get; set; } = "縦書き本文";

            public List<OcrRegion> Regions { get; set; }
                = new List<OcrRegion>();
        }

        public class PageSettings
        {
            public bool UseTemplate { get; set; } = true;

            public List<OcrRegion> Regions { get; set; }
                = new List<OcrRegion>();
        }

        private void ShowCurrentPage()
        {
            if (pdfDocument == null)
            {
                return;
            }

            if (currentPage < 0 || currentPage >= pdfDocument.PageCount)
            {
                return;
            }

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

        private string GetRegionType()
        {
            return cmbRegionType.Text switch
            {
                "本文" => "body",
                "見出し" => "heading",
                "ヘッダー" => "header",
                "フッター" => "footer",
                "脚注" => "footnote",
                "表" => "table",
                "画像" => "image",
                "地図" => "map",
                "OCRしない" => "ignore",
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
        private ResizeMode hoverResizeMode = ResizeMode.None;
        private enum ResizeMode
        {
            None,
            Left,
            Right,
            Top,
            Bottom,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        private ResizeMode resizeMode = ResizeMode.None;
        private Rectangle resizeOriginalRectangle;
        private Point resizeStartPoint;

        private const int ResizeHandleSize = 8;

        private bool isUpdatingRegionTypeCombo = false;



        public Form1()
        {
            InitializeComponent();
            InitializeOcrResultView();

            cmbRegionType.SelectedIndexChanged += cmbRegionType_SelectedIndexChanged;

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

        private void SaveCurrentPageRegions()
        {
            if (pdfDocument == null)
                return;

            // すでにユーザー補正済みとして保存されているページは、
            // 現在の regions をそのまま保存する。
            if (pageRegions.ContainsKey(currentPage))
            {
                pageRegions[currentPage] = CloneRegions(regions);
                return;
            }

            // 自動判定結果が存在する場合は、
            // 現在の regions と自動判定結果を比較する。
            // 同一なら、まだユーザー補正されていないと判断して保存しない。
            if (autoPageRegions.TryGetValue(
                currentPage,
                out List<OcrRegion>? autoRegions))
            {
                if (AreRegionsEqual(regions, autoRegions))
                {
                    return;
                }
            }

            // 自動判定結果と異なる場合は、
            // ユーザーが補正したものとして保存する。
            pageRegions[currentPage] = CloneRegions(regions);
        }

        private bool AreRegionsEqual(
            List<OcrRegion> regions1,
            List<OcrRegion> regions2)
        {
            if (regions1.Count != regions2.Count)
                return false;

            for (int i = 0; i < regions1.Count; i++)
            {
                OcrRegion a = regions1[i];
                OcrRegion b = regions2[i];

                if (a.X != b.X ||
                    a.Y != b.Y ||
                    a.Width != b.Width ||
                    a.Height != b.Height ||
                    a.Type != b.Type)
                {
                    return false;
                }
            }

            return true;
        }
        private void LoadCurrentPageRegions()
        {
            regions.Clear();

            // ユーザーが保存・補正した領域を最優先する。
            if (pageRegions.TryGetValue(
                currentPage,
                out List<OcrRegion>? savedRegions))
            {
                regions.AddRange(CloneRegions(savedRegions));
            }
            // ユーザー設定がないページは自動判定結果を表示する。
            else if (autoPageRegions.TryGetValue(
                currentPage,
                out List<OcrRegion>? autoRegions))
            {
                regions.AddRange(CloneRegions(autoRegions));
            }

            RefreshRegionList();
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

            tabOcrText = new TabPage("OCR結果");
            tabOcrTable = new TabPage("表");

            // 現在のRichTextBoxをOCR結果タブへ移動する。
            tableLayoutPanel1.Controls.Remove(richTextBox1);

            richTextBox1.Dock = DockStyle.Fill;

            tabOcrText.Controls.Add(richTextBox1);

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

            tabOcrResult.TabPages.Add(tabOcrText);
            tabOcrResult.TabPages.Add(tabOcrTable);

            // 右側セルにTabControlを1つだけ配置する。
            tableLayoutPanel1.Controls.Add(
                tabOcrResult,
                1,
                0);
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

            PageLayout layout = new PageLayout();

            layout.Template.Name = "縦書き本文";
            layout.Template.Regions = new List<OcrRegion>();

            foreach (KeyValuePair<int, List<OcrRegion>> pair in pageRegions)
            {
                string pageKey = (pair.Key + 1).ToString();

                layout.Pages[pageKey] = new PageSettings
                {
                    UseTemplate = false,
                    Regions = CloneRegions(pair.Value)
                };
            }

            string json = JsonSerializer.Serialize(
                layout,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            string path = Path.Combine(
                Application.StartupPath,
                "page_layout.json");

            File.WriteAllText(path, json);

            MessageBox.Show(
                $"ページ単位の設定を保存しました。\n\n" +
                $"保存ページ数: {pageRegions.Count}\n" +
                $"ファイル: {path}",
                "保存完了",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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

            pageRegions[currentPage] = CloneRegions(regions);

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
                    ImageRectangleToScreenRectangle(imageRect);

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
                    ImageRectangleToScreenRectangle(
                        new Rectangle(
                            region.X,
                            region.Y,
                            region.Width,
                            region.Height));

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

            HitTestRegionNear(e.Location, 20);

            // マウスが近づいた領域を自動選択
            if (resizeMode == ResizeMode.None &&
                movingRegionIndex < 0 &&
                !isDrawingRegion)
            {
                int nearIndex = HitTestRegionNear(e.Location, 20);

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

                int nearIndex = HitTestRegionNear(e.Location, 20);

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
                        ImageRectangleToScreenRectangle(
                            new Rectangle(
                                hoverRegion.X,
                                hoverRegion.Y,
                                hoverRegion.Width,
                                hoverRegion.Height));

                    ResizeMode mode =
                        GetResizeMode(e.Location, hoverRect);

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
                ScreenRectangleToImageRectangle(regionPreviewRectangle);

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

        private Rectangle ScreenRectangleToImageRectangle(Rectangle screenRect)
        {
            if (pictureBox1.Image == null)
                return Rectangle.Empty;

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

            int x = (int)((screenRect.X - offsetX) / scale);
            int y = (int)((screenRect.Y - offsetY) / scale);

            int width = (int)(screenRect.Width / scale);
            int height = (int)(screenRect.Height / scale);

            x = Math.Max(0, x);
            y = Math.Max(0, y);

            width = Math.Min(width, pictureBox1.Image.Width - x);
            height = Math.Min(height, pictureBox1.Image.Height - y);

            return new Rectangle(
                x,
                y,
                width,
                height);
        }

        private int HitTestRegion(Point screenPoint)
        {
            if (pictureBox1.Image == null)
                return -1;

            for (int i = regions.Count - 1; i >= 0; i--)
            {
                OcrRegion region = regions[i];

                Rectangle screenRect =
                    ImageRectangleToScreenRectangle(
                        new Rectangle(
                            region.X,
                            region.Y,
                            region.Width,
                            region.Height));

                if (screenRect.Contains(screenPoint))
                    return i;
            }

            return -1;
        }

        private Rectangle ImageRectangleToScreenRectangle(Rectangle imageRect)
        {
            if (pictureBox1.Image == null)
                return Rectangle.Empty;

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

            int x =
                (int)(offsetX + imageRect.X * scale);

            int y =
                (int)(offsetY + imageRect.Y * scale);

            int width =
                (int)(imageRect.Width * scale);

            int height =
                (int)(imageRect.Height * scale);

            return new Rectangle(
                x,
                y,
                width,
                height);


        }

        private ResizeMode GetResizeMode(Point point, Rectangle rect)
        {
            int h = ResizeHandleSize;
            int edge = 20;

            Rectangle topLeft = new Rectangle(
                rect.Left - h / 2,
                rect.Top - h / 2,
                h,
                h);

            Rectangle topRight = new Rectangle(
                rect.Right - h / 2,
                rect.Top - h / 2,
                h,
                h);

            Rectangle bottomLeft = new Rectangle(
                rect.Left - h / 2,
                rect.Bottom - h / 2,
                h,
                h);

            Rectangle bottomRight = new Rectangle(
                rect.Right - h / 2,
                rect.Bottom - h / 2,
                h,
                h);

            if (topLeft.Contains(point))
                return ResizeMode.TopLeft;

            if (topRight.Contains(point))
                return ResizeMode.TopRight;

            if (bottomLeft.Contains(point))
                return ResizeMode.BottomLeft;

            if (bottomRight.Contains(point))
                return ResizeMode.BottomRight;



            Rectangle left = new Rectangle(
                rect.Left - edge / 2,
                rect.Top + edge,
                edge,
                Math.Max(0, rect.Height - edge * 2));

            Rectangle right = new Rectangle(
                rect.Right - edge / 2,
                rect.Top + edge,
                edge,
                Math.Max(0, rect.Height - edge * 2));

            Rectangle top = new Rectangle(
                rect.Left + edge,
                rect.Top - edge / 2,
                Math.Max(0, rect.Width - edge * 2),
                edge);

            Rectangle bottom = new Rectangle(
                rect.Left + edge,
                rect.Bottom - edge / 2,
                Math.Max(0, rect.Width - edge * 2),
                edge);

            if (left.Contains(point))
                return ResizeMode.Left;

            if (right.Contains(point))
                return ResizeMode.Right;

            if (top.Contains(point))
                return ResizeMode.Top;

            if (bottom.Contains(point))
                return ResizeMode.Bottom;

            return ResizeMode.None;
        }

        private Color GetRegionColor(string type)
        {
            switch (type)
            {
                case "body":
                    return Color.Blue;

                case "heading":
                    return Color.Green;

                case "header":
                    return Color.Purple;

                case "footer":
                    return Color.Brown;

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
                
        private int HitTestRegionNear(Point point, int tolerance)
        {
            int nearestIndex = -1;
            double nearestDistance = double.MaxValue;

            for (int i = 0; i < regions.Count; i++)
            {
                OcrRegion region = regions[i];

                Rectangle screenRect =
                    ImageRectangleToScreenRectangle(
                        new Rectangle(
                            region.X,
                            region.Y,
                            region.Width,
                            region.Height));

                Rectangle expandedRect =
                    new Rectangle(
                        screenRect.X - tolerance,
                        screenRect.Y - tolerance,
                        screenRect.Width + tolerance * 2,
                        screenRect.Height + tolerance * 2);

                if (!expandedRect.Contains(point))
                    continue;

                // 矩形の中心までの距離を計算
                float centerX =
                    screenRect.Left + screenRect.Width / 2f;

                float centerY =
                    screenRect.Top + screenRect.Height / 2f;

                double distance =
                    Math.Sqrt(
                        Math.Pow(point.X - centerX, 2) +
                        Math.Pow(point.Y - centerY, 2));

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        private sealed class OcrDisplayItem
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public bool IsVertical { get; set; }
            public string Text { get; set; } = "";
        }

        private sealed class AutoLayoutRegion
        {
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";

            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }

            public int Rows { get; set; }
            public int Columns { get; set; }

            public List<AutoLayoutCell> Cells { get; set; }
                = new List<AutoLayoutCell>();
        }
        private sealed class AutoLayoutCell
        {
            public int Row { get; set; }
            public int Column { get; set; }

            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }

            public string Text { get; set; } = "";
            public int OcrCount { get; set; }
        }

        // =========================================================
        // 本文読み順
        //
        // 縦書き:
        //   1. OCRを縦列にグループ化
        //   2. 列を右 → 左
        //   3. 各列を上 → 下
        //
        // 横書き:
        //   1. 上 → 下
        //   2. 同じ位置なら左 → 右
        // =========================================================

        private List<OcrDisplayItem> SortBodyReadingOrder(
            List<OcrDisplayItem> items)
        {
            if (items.Count <= 1)
                return new List<OcrDisplayItem>(items);

            int verticalCount =
                items.Count(item => item.IsVertical);

            bool isVertical =
                verticalCount * 2 >= items.Count;

            // ---------------------------------------------------------
            // 横書き本文
            // ---------------------------------------------------------
            if (!isVertical)
            {
                return items
                    .OrderBy(item => item.Y)
                    .ThenBy(item => item.X)
                    .ToList();
            }

            // ---------------------------------------------------------
            // 縦書き本文
            //
            // OCRの中心Xを基準に縦列を作る。
            // ---------------------------------------------------------

            var columns =
                new List<List<OcrDisplayItem>>();

            foreach (OcrDisplayItem item in
                items.OrderByDescending(
                    item => item.X + item.Width / 2))
            {
                double centerX =
                    item.X + item.Width / 2.0;

                List<OcrDisplayItem>? targetColumn = null;
                double bestDistance = double.MaxValue;

                foreach (List<OcrDisplayItem> column in columns)
                {
                    double columnCenterX =
                        column.Average(
                            x => x.X + x.Width / 2.0);

                    double distance =
                        Math.Abs(centerX - columnCenterX);

                    double averageWidth =
                        column.Average(x => x.Width);

                    double tolerance =
                        Math.Max(
                            8.0,
                            Math.Max(
                                item.Width,
                                averageWidth) * 1.5);

                    if (distance <= tolerance &&
                        distance < bestDistance)
                    {
                        targetColumn = column;
                        bestDistance = distance;
                    }
                }

                if (targetColumn == null)
                {
                    targetColumn =
                        new List<OcrDisplayItem>();

                    columns.Add(targetColumn);
                }

                targetColumn.Add(item);
            }

            // ---------------------------------------------------------
            // 各縦列は上 → 下
            // ---------------------------------------------------------

            foreach (List<OcrDisplayItem> column in columns)
            {
                column.Sort(
                    (a, b) =>
                    {
                        int result =
                            a.Y.CompareTo(b.Y);

                        if (result != 0)
                            return result;

                        return a.X.CompareTo(b.X);
                    });
            }

            // ---------------------------------------------------------
            // 縦列そのものは右 → 左
            // ---------------------------------------------------------

            columns.Sort(
                (a, b) =>
                {
                    double aX =
                        a.Average(
                            x => x.X + x.Width / 2.0);

                    double bX =
                        b.Average(
                            x => x.X + x.Width / 2.0);

                    return bX.CompareTo(aX);
                });

            return columns
                .SelectMany(column => column)
                .ToList();
        }

        // =========================================================
        // OCR結果表示用の表内部読み順
        //
        // ユーザー指定の「表」領域に属するOCRだけを対象とする。
        // 本文・脚注など、表以外の項目の順序は変更しない。
        //
        // 表では単純な Y 座標による「行」判定を行わない。
        // OCRの文字列ブロックが複数段に分かれた場合でも、
        // X方向の位置関係を基準に表内部のまとまりを作る。
        //
        // ※文字列内容による特別扱いは行わない。
        // =========================================================
        private List<OcrDisplayItem> SortTableItemsForDisplay(
            List<OcrDisplayItem> items,
            List<OcrRegion> userRegions,
            bool useUserRegions,
            List<AutoLayoutRegion> autoRegions)
        {
            if (items.Count <= 1)
                return new List<OcrDisplayItem>(items);

            string GetTypeForItem(OcrDisplayItem item)
            {
                return useUserRegions
                    ? FindUserRegionType(item, userRegions)
                    : FindAutoLayoutRegionType(item, autoRegions);
            }

            // ---------------------------------------------------------
            // 表OCRだけを抽出
            // ---------------------------------------------------------

            List<OcrDisplayItem> tableItems = items
                .Where(item => GetTypeForItem(item) == "table")
                .ToList();

            if (tableItems.Count <= 1)
                return new List<OcrDisplayItem>(items);

            // ---------------------------------------------------------
            // 表内部のX方向グループを作る
            //
            // OCRボックスの中心Xを基準にする。
            // ただし、固定の座標値には依存しない。
            // ---------------------------------------------------------

            var columns = new List<List<OcrDisplayItem>>();

            foreach (OcrDisplayItem item in tableItems
                .OrderBy(item => item.X + item.Width / 2.0)
                .ThenBy(item => item.Y))
            {
                double itemCenterX =
                    item.X + item.Width / 2.0;

                List<OcrDisplayItem>? targetColumn = null;
                double bestDistance = double.MaxValue;

                foreach (List<OcrDisplayItem> column in columns)
                {
                    double columnCenterX =
                        column.Average(
                            x => x.X + x.Width / 2.0);

                    double averageWidth =
                        column.Count == 0
                            ? Math.Max(1, item.Width)
                            : column.Average(
                                x => Math.Max(1, x.Width));

                    // OCRボックス幅を基準にした相対許容値
                    double tolerance =
                        Math.Max(4.0, averageWidth * 0.8);

                    double distance =
                        Math.Abs(itemCenterX - columnCenterX);

                    if (distance <= tolerance &&
                        distance < bestDistance)
                    {
                        targetColumn = column;
                        bestDistance = distance;
                    }
                }

                if (targetColumn == null)
                {
                    targetColumn =
                        new List<OcrDisplayItem>();

                    columns.Add(targetColumn);
                }

                targetColumn.Add(item);
            }

            // ---------------------------------------------------------
            // 各X列の内部は上→下
            // ---------------------------------------------------------

            foreach (List<OcrDisplayItem> column in columns)
            {
                column.Sort((a, b) =>
                {
                    int result =
                        a.Y.CompareTo(b.Y);

                    if (result != 0)
                        return result;

                    return a.X.CompareTo(b.X);
                });
            }

            // ---------------------------------------------------------
            // X列を左→右に並べる
            //
            // 表が通常の横書き表の場合はこちら。
            // ---------------------------------------------------------

            columns.Sort((a, b) =>
            {
                double ax =
                    a.Average(
                        x => x.X + x.Width / 2.0);

                double bx =
                    b.Average(
                        x => x.X + x.Width / 2.0);

                return ax.CompareTo(bx);
            });

            // ---------------------------------------------------------
            // 表項目を完成
            // ---------------------------------------------------------

            List<OcrDisplayItem> sortedTableItems =
                columns
                    .SelectMany(column => column)
                    .ToList();

            // ---------------------------------------------------------
            // 元のOCRリストでは、表項目の位置だけを置換。
            // 本文など他の領域の順序は変更しない。
            // ---------------------------------------------------------

            List<OcrDisplayItem> result =
                new List<OcrDisplayItem>(items);

            int tableIndex = 0;

            for (int i = 0; i < result.Count; i++)
            {
                if (GetTypeForItem(result[i]) == "table")
                {
                    result[i] =
                        sortedTableItems[tableIndex];

                    tableIndex++;
                }
            }

            return result;
        }

        private void CreateOcrTableView()
        {
            if (dgvOcrTable != null)
            {
                return;
            }

            dgvOcrTable = new DataGridView
            {
                Name = "dgvOcrTable",
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = true,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false
            };

            dgvOcrTable.ColumnHeadersDefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            dgvOcrTable.DefaultCellStyle.WrapMode =
                DataGridViewTriState.True;

            dgvOcrTable.Visible = false;

            tableLayoutPanel1.Controls.Add(dgvOcrTable, 1, 0);
        }

        private void DisplayOcrTable(
    List<OcrDisplayItem> tableItems)
        {
            if (dgvOcrTable == null)
                return;

            dgvOcrTable.Columns.Clear();
            dgvOcrTable.Rows.Clear();

            if (tableItems.Count == 0)
                return;

            dgvOcrTable.Columns.Add(
                "Index",
                "No.");

            dgvOcrTable.Columns.Add(
                "Text",
                "OCR結果");

            for (int i = 0; i < tableItems.Count; i++)
            {
                OcrDisplayItem item = tableItems[i];

                dgvOcrTable.Rows.Add(
                    i + 1,
                    item.Text);
            }

            dgvOcrTable.Columns["Index"]!.FillWeight = 15;
            dgvOcrTable.Columns["Text"]!.FillWeight = 85;
        }

        private void DisplayDetectedTables(
    List<AutoLayoutRegion> autoRegions)
        {
            if (tabOcrTable == null)
                return;

            tabOcrTable.Controls.Clear();

            TabControl tableTabs = new TabControl
            {
                Dock = DockStyle.Fill
            };

            List<AutoLayoutRegion> tables =
                autoRegions
                    .Where(r =>
                        string.Equals(
                            r.Type,
                            "table",
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (tables.Count == 0)
            {
                Label label = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "検出された表はありません。",
                    TextAlign = ContentAlignment.MiddleCenter
                };

                tabOcrTable.Controls.Add(label);
                return;
            }

            for (int tableIndex = 0;
                 tableIndex < tables.Count;
                 tableIndex++)
            {
                AutoLayoutRegion table =
                    tables[tableIndex];

                TabPage page = new TabPage(
                    string.IsNullOrWhiteSpace(table.Name)
                        ? $"表{tableIndex + 1}"
                        : table.Name);

                DataGridView grid =
                    CreateTableGrid(table);

                page.Controls.Add(grid);

                tableTabs.TabPages.Add(page);
            }

            tabOcrTable.Controls.Add(tableTabs);
        }

        private DataGridView CreateTableGrid(
    AutoLayoutRegion table)
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeRowsMode =
                    DataGridViewAutoSizeRowsMode.AllCells,
                AutoSizeColumnsMode =
                    DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode =
                    DataGridViewSelectionMode.CellSelect,
                MultiSelect = false
            };

            grid.DefaultCellStyle.WrapMode =
                DataGridViewTriState.True;

            int rows = table.Rows;
            int columns = table.Columns;

            if (rows <= 0 || columns <= 0)
            {
                if (table.Cells.Count > 0)
                {
                    rows = table.Cells.Max(c => c.Row);
                    columns = table.Cells.Max(c => c.Column);
                }
            }

            if (rows <= 0 || columns <= 0)
                return grid;

            for (int column = 1;
                 column <= columns;
                 column++)
            {
                grid.Columns.Add(
                    $"Column{column}",
                    $"列{column}");
            }

            grid.Rows.Add(rows);

            foreach (AutoLayoutCell cell in table.Cells)
            {
                int rowIndex = cell.Row - 1;
                int columnIndex = cell.Column - 1;

                if (rowIndex < 0 ||
                    rowIndex >= grid.Rows.Count)
                    continue;

                if (columnIndex < 0 ||
                    columnIndex >= grid.Columns.Count)
                    continue;

                grid.Rows[rowIndex]
                    .Cells[columnIndex]
                    .Value = cell.Text;
            }

            return grid;
        }
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

            string projectDir = FindOcrEngineDirectory();
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

                        ProcessResult result = await RunAutoRegionProcessAsync(
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
                            LoadAutoLayoutJson(resultJson);

                        List<OcrRegion> converted = detected
                            .Select(ConvertAutoLayoutRegion)
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

        private sealed class ProcessResult
        {
            public int ExitCode { get; init; }
            public string Stdout { get; init; } = "";
            public string Stderr { get; init; } = "";
        }

        private async Task<ProcessResult> RunAutoRegionProcessAsync(
            string pythonExe,
            string autoRegionScript,
            string projectDir,
            string imagePath,
            string pageDir)
        {
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
                if (e.Data != null)
                    stdout.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    stderr.AppendLine(e.Data);
            };

            process.Exited += (_, _) =>
                completion.TrySetResult(process.ExitCode);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            int exitCode = await completion.Task;

            return new ProcessResult
            {
                ExitCode = exitCode,
                Stdout = stdout.ToString(),
                Stderr = stderr.ToString()
            };
        }

        private OcrRegion ConvertAutoLayoutRegion(AutoLayoutRegion source)
        {
            return new OcrRegion
            {
                Name = GetRegionDisplayName(source.Type),
                Type = source.Type,
                X = source.X,
                Y = source.Y,
                Width = source.Width,
                Height = source.Height
            };
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

            string projectDir = FindOcrEngineDirectory();
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
                    LoadNdlocrPageJson(pageJson);

                List<AutoLayoutRegion> autoRegions =
                    useUserRegions
                        ? new List<AutoLayoutRegion>()
                        : LoadAutoLayoutJson(resultJson);

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

                richTextBox1.AppendText(
                    "========== OCR結果 ==========\r\n");

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
                    SortTableItemsForDisplay(
                        ocrItems,
                        regions,
                        useUserRegions,
                        autoRegions);

                for (int i = 0; i < displayItems.Count; i++)
                {
                    OcrDisplayItem item = displayItems[i];

                    string type = useUserRegions
                        ? FindUserRegionType(item, regions)
                        : FindAutoLayoutRegionType(item, autoRegions);

                    string direction =
                        item.IsVertical ? "縦" : "横";

                    richTextBox1.AppendText(
                        $"[{i:00}] [{GetRegionDisplayName(type)}] " +
                        $"[{direction}] {item.Text}\r\n");
                }



                // =========================================================
                // 表OCRを右側の「表」タブへ表示
                // =========================================================

                List<OcrDisplayItem> tableItems =
                    new List<OcrDisplayItem>();

                foreach (OcrDisplayItem item in displayItems)
                {
                    string type = useUserRegions
                        ? FindUserRegionType(item, regions)
                        : FindAutoLayoutRegionType(item, autoRegions);

                    if (type == "table")
                    {
                        tableItems.Add(item);
                    }
                }

                richTextBox1.AppendText(
                    Environment.NewLine +
                    $"表表示項目数: {tableItems.Count}" +
                    Environment.NewLine);

                DisplayOcrTable(tableItems);

                DisplayOcrTable(tableItems);

                // =========================================================
                // 本文OCRだけを抽出
                // =========================================================

                List<OcrDisplayItem> bodyItems =
                    new List<OcrDisplayItem>();

                foreach (OcrDisplayItem item in ocrItems)
                {
                    string type = useUserRegions
                        ? FindUserRegionType(item, regions)
                        : FindAutoLayoutRegionType(item, autoRegions);

                    if (type == "body")
                    {
                        bodyItems.Add(item);
                    }
                }

                richTextBox1.AppendText(
                    $"\r\n本文OCR項目数: {bodyItems.Count}\r\n");

                // =========================================================
                // 本文読み順
                // =========================================================

                richTextBox1.AppendText(
                    "\r\n========== 本文読み順 ==========\r\n");

                List<OcrDisplayItem> orderedBody =
                    SortBodyReadingOrder(bodyItems);

                for (int i = 0; i < orderedBody.Count; i++)
                {
                    OcrDisplayItem item = orderedBody[i];

                    richTextBox1.AppendText(
                        $"[{i + 1:00}] {item.Text}\r\n");
                }

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

                richTextBox1.AppendText(
                    $"本文読み順結果: {bodyReadingOrderPath}\r\n");

                richTextBox1.AppendText(
                    "\r\n========== OCR処理完了 ==========\r\n");
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

        // =========================================================
        // ユーザー指定領域によるOCR分類
        // =========================================================
        //
        // OCR項目の中心点がユーザー指定領域内に入っているかで
        // 判定する。
        //
        // ユーザー指定領域が存在する場合、auto_layout.jsonの
        // 自動判定結果は一切使用しない。
        //
        // 領域外のOCRは「未分類」とし、本文読み順には入れない。
        // =========================================================
        private string FindUserRegionType(
            OcrDisplayItem item,
            List<OcrRegion> userRegions)
        {
            int centerX =
                item.X + item.Width / 2;

            int centerY =
                item.Y + item.Height / 2;

            foreach (OcrRegion region in userRegions)
            {
                if (centerX >= region.X &&
                    centerX <= region.X + region.Width &&
                    centerY >= region.Y &&
                    centerY <= region.Y + region.Height)
                {
                    return region.Type;
                }
            }

            return "";
        }

        private List<OcrDisplayItem> LoadNdlocrPageJson(string path)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            var result = new List<OcrDisplayItem>();
            CollectNdlocrItems(doc.RootElement, result);
            return result;
        }

        private void CollectNdlocrItems(JsonElement element, List<OcrDisplayItem> result)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                bool isTextline = false;
                if (element.TryGetProperty("isTextline", out JsonElement tl))
                {
                    isTextline = tl.ValueKind == JsonValueKind.True ||
                                 (tl.ValueKind == JsonValueKind.String &&
                                  string.Equals(tl.GetString(), "true", StringComparison.OrdinalIgnoreCase));
                }

                if (isTextline && TryParseNdlocrItem(element, out OcrDisplayItem? item))
                {
                    result.Add(item!);
                    return;
                }

                foreach (JsonProperty property in element.EnumerateObject())
                    CollectNdlocrItems(property.Value, result);
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child in element.EnumerateArray())
                    CollectNdlocrItems(child, result);
            }
        }

        private bool TryParseNdlocrItem(JsonElement obj, out OcrDisplayItem? result)
        {
            result = null;
            if (!obj.TryGetProperty("text", out JsonElement textElement) || textElement.ValueKind != JsonValueKind.String)
                return false;

            string text = textElement.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(text)) return false;

            int x = 0, y = 0, width = 0, height = 0;
            if (obj.TryGetProperty("boundingBox", out JsonElement box) && box.ValueKind == JsonValueKind.Array)
            {
                var points = new List<(int X, int Y)>();
                foreach (JsonElement point in box.EnumerateArray())
                {
                    if (point.ValueKind != JsonValueKind.Array) continue;
                    var values = new List<int>();
                    foreach (JsonElement value in point.EnumerateArray())
                    {
                        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int n))
                            values.Add(n);
                    }
                    if (values.Count >= 2) points.Add((values[0], values[1]));
                }

                if (points.Count > 0)
                {
                    x = points.Min(p => p.X);
                    y = points.Min(p => p.Y);
                    width = Math.Max(0, points.Max(p => p.X) - x);
                    height = Math.Max(0, points.Max(p => p.Y) - y);
                }
            }

            bool isVertical = false;
            if (obj.TryGetProperty("isVertical", out JsonElement vertical))
            {
                isVertical = vertical.ValueKind == JsonValueKind.True ||
                             (vertical.ValueKind == JsonValueKind.String &&
                              string.Equals(vertical.GetString(), "true", StringComparison.OrdinalIgnoreCase));
            }

            result = new OcrDisplayItem
            {
                X = x, Y = y, Width = width, Height = height,
                IsVertical = isVertical, Text = text
            };
            return true;
        }

        private List<AutoLayoutRegion> LoadAutoLayoutJson(string path)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            var result = new List<AutoLayoutRegion>();
            JsonElement root = doc.RootElement;
            JsonElement regionsElement;

            if (root.TryGetProperty("regions", out regionsElement) && regionsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in regionsElement.EnumerateArray()) AddAutoLayoutRegion(item, result);
                return result;
            }

            if (root.TryGetProperty("Regions", out regionsElement) && regionsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in regionsElement.EnumerateArray()) AddAutoLayoutRegion(item, result);
            }
            return result;
        }

        private void AddAutoLayoutRegion(
    JsonElement item,
    List<AutoLayoutRegion> result)
        {
            if (item.ValueKind != JsonValueKind.Object)
                return;

            AutoLayoutRegion region = new AutoLayoutRegion
            {
                Name = ReadJsonString(item, "name", "Name"),
                Type = ReadJsonString(item, "type", "Type"),
                X = ReadJsonInt(item, "x", "X"),
                Y = ReadJsonInt(item, "y", "Y"),
                Width = ReadJsonInt(item, "width", "Width"),
                Height = ReadJsonInt(item, "height", "Height"),
                Rows = ReadJsonInt(item, "rows", "Rows"),
                Columns = ReadJsonInt(item, "columns", "Columns")
            };

            if (item.TryGetProperty(
                    "cells",
                    out JsonElement cellsElement)
                &&
                cellsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement cellElement
                         in cellsElement.EnumerateArray())
                {
                    if (cellElement.ValueKind != JsonValueKind.Object)
                        continue;

                    AutoLayoutCell cell = new AutoLayoutCell
                    {
                        Row = ReadJsonInt(
                            cellElement,
                            "row",
                            "Row"),

                        Column = ReadJsonInt(
                            cellElement,
                            "column",
                            "Column"),

                        X = ReadJsonInt(
                            cellElement,
                            "x",
                            "X"),

                        Y = ReadJsonInt(
                            cellElement,
                            "y",
                            "Y"),

                        Width = ReadJsonInt(
                            cellElement,
                            "width",
                            "Width"),

                        Height = ReadJsonInt(
                            cellElement,
                            "height",
                            "Height"),

                        Text = ReadJsonString(
                            cellElement,
                            "text",
                            "Text"),

                        OcrCount = ReadJsonInt(
                            cellElement,
                            "ocr_count",
                            "OcrCount")
                    };

                    region.Cells.Add(cell);
                }
            }

            result.Add(region);
        }

        private string ReadJsonString(JsonElement obj, string lower, string upper)
        {
            if (obj.TryGetProperty(lower, out JsonElement a) && a.ValueKind == JsonValueKind.String)
                return a.GetString() ?? "";
            if (obj.TryGetProperty(upper, out JsonElement b) && b.ValueKind == JsonValueKind.String)
                return b.GetString() ?? "";
            return "";
        }

        private int ReadJsonInt(JsonElement obj, string lower, string upper)
        {
            JsonElement element;
            if (!obj.TryGetProperty(lower, out element) && !obj.TryGetProperty(upper, out element)) return 0;
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int number)) return number;
            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out int parsed)) return parsed;
            return 0;
        }

        private string FindAutoLayoutRegionType(OcrDisplayItem item, List<AutoLayoutRegion> regions)
        {
            int centerX = item.X + item.Width / 2;
            int centerY = item.Y + item.Height / 2;
            foreach (AutoLayoutRegion region in regions)
            {
                if (centerX >= region.X && centerX <= region.X + region.Width &&
                    centerY >= region.Y && centerY <= region.Y + region.Height)
                    return region.Type;
            }
            return "";
        }

        private string GetRegionDisplayName(string type)
        {
            return type switch
            {
                "body" => "本文",
                "heading" => "見出し",
                "header" => "ヘッダー",
                "footer" => "フッター",
                "footnote" => "脚注",
                "table" => "表",
                "image" => "画像",
                "map" => "地図",
                "ignore" => "OCRしない",
                _ => "未分類"
            };
        }

        private string FindOcrEngineDirectory()
        {
            string? dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                string candidate = Path.Combine(dir, "ocr_engine");
                if (File.Exists(Path.Combine(candidate, "ndlocr_auto_region.py")))
                    return candidate;
                DirectoryInfo? parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }

            string fallback = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "ocr_engine"));
            return fallback;
        }

        private void btnTestCrop_Click(object sender, EventArgs e)
        {
            // ========================================
            // 領域が選択されているか確認
            // ========================================

            int index = lstRegions.SelectedIndex;

            if (index < 0 || index >= regions.Count)
            {
                MessageBox.Show(
                    "先に領域を選択してください。",
                    "領域テスト",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            // ========================================
            // ページ画像があるか確認
            // ========================================

            if (pictureBox1.Image == null)
            {
                MessageBox.Show(
                    "ページ画像がありません。",
                    "領域テスト",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            // ========================================
            // OCR領域を取得
            // ========================================

            OcrRegion region = regions[index];

            // ========================================
            // 現在表示しているページ画像をコピー
            // ========================================

            Bitmap pageImage;

            using (Bitmap source = new Bitmap(pictureBox1.Image))
            {
                pageImage = new Bitmap(source);
            }

            // ========================================
            // 選択領域を切り出す
            // ========================================

            Bitmap croppedImage = CropRegion(
                pageImage,
                region);

            pageImage.Dispose();

            // ========================================
            // 切り出した画像を保存
            // ========================================

            string projectDir =
                @"C:\Users\natur\source\repos\OCR_Translator\ocr_engine";

            string ocrInput =
                Path.Combine(
                    projectDir,
                    "ocr_input.png");

            croppedImage.Save(
                ocrInput,
                System.Drawing.Imaging.ImageFormat.Png);

            // ========================================
            // 切り出した画像を表示
            // ========================================

            Form previewForm = new Form();

            previewForm.Text =
                "OCR領域テスト - " + region.Name;

            previewForm.StartPosition =
                FormStartPosition.CenterParent;

            previewForm.Size =
                new Size(800, 600);

            PictureBox previewPictureBox =
                new PictureBox();

            previewPictureBox.Dock =
                DockStyle.Fill;

            previewPictureBox.SizeMode =
                PictureBoxSizeMode.Zoom;

            previewPictureBox.Image =
                croppedImage;

            previewForm.Controls.Add(
                previewPictureBox);

            previewForm.Show(this);

            // ========================================
            // Python OCRを実行
            // ========================================
            try
            {
                

                string pythonExe =
                    Path.Combine(
                        projectDir,
                        "venv",
                        "Scripts",
                        "python.exe");

                string pythonScript =
                    Path.Combine(
                        projectDir,
                        "ocr_region.py");

                
                // ファイル存在確認
                if (!File.Exists(pythonExe))
                {
                    MessageBox.Show(
                        "python.exe が見つかりません。\r\n\r\n" +
                        pythonExe,
                        "Pythonエラー",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return;
                }

                if (!File.Exists(pythonScript))
                {
                    MessageBox.Show(
                        "ocr_region.py が見つかりません。\r\n\r\n" +
                        pythonScript,
                        "Pythonエラー",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return;
                }

                if (!File.Exists(ocrInput))
                {
                    MessageBox.Show(
                        "ocr_input.png が見つかりません。\r\n\r\n" +
                        ocrInput,
                        "OCR入力画像エラー",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return;
                }

                // ========================================
                // cmd.exe からPythonを実行
                // 黒いコンソール画面を表示する
                // ========================================



                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = pythonExe;
                psi.WorkingDirectory = projectDir;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                psi.ArgumentList.Add(pythonScript);
                psi.ArgumentList.Add(ocrInput);


                using (Process process =
                       new Process())
                {
                    process.StartInfo = psi;

                    process.Start();

                    string standardOutput =
                        process.StandardOutput.ReadToEnd();

                    string standardError =
                        process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    MessageBox.Show(
                        "Python終了コード: " +
                        process.ExitCode +
                        "\r\n\r\n" +
                        "【標準出力】\r\n" +
                        standardOutput +
                        "\r\n\r\n" +
                        "【エラー出力】\r\n" +
                        standardError,
                        "Python OCR結果",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                MessageBox.Show(
                    "Python OCRを起動しました。\r\n\r\n" +
                    "入力画像:\r\n" +
                    ocrInput,
                    "OCRテスト",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Python OCRの起動に失敗しました。\r\n\r\n" +
                    ex.ToString(),
                    "OCRエラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
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
        private Bitmap CropRegion(Bitmap source, OcrRegion region)
        {
            int x = Math.Max(0, region.X);
            int y = Math.Max(0, region.Y);

            int right = Math.Min(
                source.Width,
                region.X + region.Width);

            int bottom = Math.Min(
                source.Height,
                region.Y + region.Height);

            int width = right - x;
            int height = bottom - y;

            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException(
                    "OCR領域が画像の範囲外です。");
            }

            Rectangle cropRect =
                new Rectangle(
                    x,
                    y,
                    width,
                    height);

            return source.Clone(
                cropRect,
                source.PixelFormat);
        }



    }
}