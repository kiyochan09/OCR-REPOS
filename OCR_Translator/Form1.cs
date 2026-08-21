using System;
using System.Drawing;
using System.Windows.Forms;
using PdfiumViewer;
using System.Text.Json;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;

namespace OCR_Translator
{
    public class OcrResult
    {
        public int x { get; set; }
        public int y { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public string text { get; set; } = "";
        public double confidence { get; set; }
        public bool isVertical { get; set; }
        public int id { get; set; }
    }

    public class AutoRegionsResult
    {
        public string image { get; set; } = "";
        public string json { get; set; } = "";

        public List<OcrResult> results { get; set; } = new();
    }

    public partial class Form1 : Form
    {
        // PDF関連
        private PdfDocument? pdfDocument;
        private int currentPage = 0;
        private string? currentPdfPath;     

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

        public class AutoLayout
        {
            [JsonPropertyName("image")]
            public string Image { get; set; } = "";

            [JsonPropertyName("image_width")]
            public int ImageWidth { get; set; }

            [JsonPropertyName("image_height")]
            public int ImageHeight { get; set; }

            [JsonPropertyName("regions")]
            public List<AutoLayoutRegion> Regions { get; set; }
                = new List<AutoLayoutRegion>();
        }

        public class AutoLayoutRegion
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("type")]
            public string Type { get; set; } = "";

            [JsonPropertyName("x")]
            public int X { get; set; }

            [JsonPropertyName("y")]
            public int Y { get; set; }

            [JsonPropertyName("width")]
            public int Width { get; set; }

            [JsonPropertyName("height")]
            public int Height { get; set; }

            [JsonPropertyName("orientation")]
            public string Orientation { get; set; } = "";

            [JsonPropertyName("ocr_count")]
            public int OcrCount { get; set; }
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

        private List<AutoRegion> autoRegions = new List<AutoRegion>();

        private class AutoLayoutResult
        {
            public string? image { get; set; }

            public int image_width { get; set; }

            public int image_height { get; set; }

            public List<AutoRegion> regions { get; set; } = new List<AutoRegion>();
        }

        private class AutoRegion
        {
            public string name { get; set; } = "";

            public string type { get; set; } = "";

            public int x { get; set; }

            public int y { get; set; }

            public int width { get; set; }

            public int height { get; set; }

            public string orientation { get; set; } = "";

            public int ocr_count { get; set; }
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
                                
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "ページを表示できませんでした。\n\n" + ex.Message,
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

        

        private List<OcrRegion> regions = new List<OcrRegion>();
                
        
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

        public Form1()
        {
            InitializeComponent();
            pictureBox1.Paint += pictureBox1_Paint;
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

                // PDFを読み込む
                pdfDocument = PdfDocument.Load(currentPdfPath);

                // 最初のページ
                currentPage = 0;

                // ページを表示
                ShowCurrentPage();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "PDFを開けませんでした。\n\n" + ex.Message,
                    "PDFエラー",
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

            if (index < 0)
                return;

            regions.RemoveAt(index);
            lstRegions.Items.RemoveAt(index);
        }

        private void LoadAutoLayout(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                MessageBox.Show(
                    "自動レイアウトJSONが見つかりません。\n\n" +
                    jsonPath,
                    "自動レイアウト",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            try
            {
                string json = File.ReadAllText(
                    jsonPath,
                    System.Text.Encoding.UTF8);

                AutoLayout? layout =
                    JsonSerializer.Deserialize<AutoLayout>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                if (layout == null)
                {
                    MessageBox.Show(
                        "自動レイアウトJSONを読み込めませんでした。",
                        "自動レイアウト",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return;
                }

                // 現在の領域をクリア
                regions.Clear();
                lstRegions.Items.Clear();

                // 自動検出された領域を追加
                foreach (AutoLayoutRegion autoRegion in layout.Regions)
                {
                    OcrRegion region = new OcrRegion
                    {
                        Name = autoRegion.Name,
                        Type = autoRegion.Type,
                        X = autoRegion.X,
                        Y = autoRegion.Y,
                        Width = autoRegion.Width,
                        Height = autoRegion.Height
                    };

                    regions.Add(region);
                    lstRegions.Items.Add(region.Name);
                }

                // 最初の領域を選択
                if (regions.Count > 0)
                {
                    lstRegions.SelectedIndex = 0;
                }

                pictureBox1.Invalidate();

                MessageBox.Show(
                    $"自動レイアウトを読み込みました。\n\n" +
                    $"領域数: {regions.Count}",
                    "自動レイアウト",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "自動レイアウトの読み込み中にエラーが発生しました。\n\n" +
                    ex.Message,
                    "自動レイアウトエラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        private void btnSaveLayout_Click(object sender, EventArgs e)
        {
            PageLayout layout = new PageLayout();

            layout.Template.Name = "縦書き本文";
            layout.Template.Regions = new List<OcrRegion>(regions);

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
                $"設定を保存しました。\n\n{path}",
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
                currentPage++;
                ShowCurrentPage();
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
                currentPage--;
                ShowCurrentPage();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
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
                cmbRegionType.SelectedItem = displayName;
            }
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

        private bool IsPointInsideRegion(
            OcrResult item,
            OcrRegion region)
        {
            // OCR結果の中心座標を計算
            double centerX =
                item.x + item.width / 2.0;

            double centerY =
                item.y + item.height / 2.0;

            // 領域内に中心点が入っているか確認
            return
                centerX >= region.X &&
                centerX <= region.X + region.Width &&
                centerY >= region.Y &&
                centerY <= region.Y + region.Height;
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



                ProcessStartInfo psi =
    new ProcessStartInfo();

                psi.FileName = pythonExe;

                psi.WorkingDirectory =
                    projectDir;

                psi.UseShellExecute = false;

                psi.CreateNoWindow = false;

                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                psi.ArgumentList.Add(
                    pythonScript);

                psi.ArgumentList.Add(
                    ocrInput);

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
        
        private void ApplyAutoLayout(AutoLayout layout)
        {
            if (layout.Regions == null || layout.Regions.Count == 0)
            {
                MessageBox.Show(
                    "自動領域が検出されませんでした。",
                    "自動領域解析",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            // 既存の領域をクリア
            regions.Clear();

            // ListBoxもクリア
            lstRegions.Items.Clear();

            foreach (var region in layout.Regions)
            {
                OcrRegion ocrRegion = new OcrRegion
                {
                    Name = region.Name,
                    Type = region.Type,
                    X = region.X,
                    Y = region.Y,
                    Width = region.Width,
                    Height = region.Height
                };

                regions.Add(ocrRegion);

                lstRegions.Items.Add(
                    $"{region.Name} ({region.Orientation})");
            }

            pictureBox1.Invalidate();

            if (lstRegions.Items.Count > 0)
            {
                lstRegions.SelectedIndex = 0;
            }

            MessageBox.Show(
                $"自動領域を読み込みました。\n\n" +
                $"領域数: {layout.Regions.Count}\n" +
                $"画像サイズ: {layout.ImageWidth} × {layout.ImageHeight}",
                "自動領域解析",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }


        private async void btnStartOcr_Click(object sender, EventArgs e)
        {
            try
            {
                if (pictureBox1.Image == null)
                {
                    MessageBox.Show(
                        "先にPDFを開いてください。",
                        "OCR",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    return;
                }

                btnStartOcr.Enabled = false;

                richTextBox1.Clear();
                richTextBox1.Text = "OCRを実行しています...\r\n";

                // =========================================================
                // OCR_Translator.exe の場所から上方向に検索して
                // ocr_engine フォルダを探す
                // =========================================================

                string? currentDir =
                    AppDomain.CurrentDomain.BaseDirectory;

                string? ocrEngineDir = null;

                while (!string.IsNullOrEmpty(currentDir))
                {
                    string candidate =
                        Path.Combine(currentDir, "ocr_engine");

                    string candidatePython =
                        Path.Combine(
                            candidate,
                            "venv",
                            "Scripts",
                            "python.exe");

                    if (Directory.Exists(candidate) &&
                        File.Exists(candidatePython))
                    {
                        ocrEngineDir = candidate;
                        break;
                    }

                    DirectoryInfo? parent =
                        Directory.GetParent(currentDir);

                    if (parent == null)
                        break;

                    currentDir = parent.FullName;
                }

                // =========================================================
                // OCRエンジンが見つからない場合
                // =========================================================

                if (string.IsNullOrEmpty(ocrEngineDir))
                {
                    MessageBox.Show(
                        "ocr_engine フォルダと Python が見つかりません。\r\n\r\n" +
                        "実行フォルダ:\r\n" +
                        AppDomain.CurrentDomain.BaseDirectory,
                        "OCRエラー",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return;
                }

                // =========================================================
                // Python
                // =========================================================

                string pythonExe =
                    Path.Combine(
                        ocrEngineDir,
                        "venv",
                        "Scripts",
                        "python.exe");

                // =========================================================
                // NDLOCR-Lite 自動領域スクリプト
                // =========================================================

                string scriptPath =
                    Path.Combine(
                        ocrEngineDir,
                        "ndlocr_auto_region.py");

                // =========================================================
                // OCR入力画像
                // =========================================================

                string imagePath =
                    Path.Combine(
                        ocrEngineDir,
                        "ocr_input.png");

                // =========================================================
                // OCR出力フォルダ
                // =========================================================

                string outputDir =
                    Path.Combine(
                        ocrEngineDir,
                        "auto_region_test");

                // =========================================================
                // ファイル確認
                // =========================================================

                if (!File.Exists(pythonExe))
                {
                    MessageBox.Show(
                        "Pythonが見つかりません。\r\n\r\n" +
                        pythonExe,
                        "OCRエラー",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return;
                }

                if (!File.Exists(scriptPath))
                {
                    MessageBox.Show(
                        "NDLOCRスクリプトが見つかりません。\r\n\r\n" +
                        scriptPath,
                        "OCRエラー",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return;
                }

                // =========================================================
                // 現在表示しているPDFページをOCR入力画像として保存
                // =========================================================

                using (Bitmap bitmap =
                    new Bitmap(
                        pictureBox1.Image.Width,
                        pictureBox1.Image.Height))
                {
                    using (Graphics g =
                        Graphics.FromImage(bitmap))
                    {
                        g.DrawImage(
                            pictureBox1.Image,
                            0,
                            0,
                            bitmap.Width,
                            bitmap.Height);
                    }

                    bitmap.Save(
                        imagePath,
                        System.Drawing.Imaging.ImageFormat.Png);                    
                    
                }

                // =========================================================
                // 出力フォルダ作成
                // =========================================================

                Directory.CreateDirectory(outputDir);

                // =========================================================
                // Python実行
                //
                // python.exe ndlocr_auto_region.py
                //          ocr_input.png
                //          auto_region_test
                // =========================================================

                ProcessStartInfo psi =
                    new ProcessStartInfo();

                psi.FileName = pythonExe;

                psi.WorkingDirectory =
                    ocrEngineDir;

                psi.UseShellExecute = false;

                psi.CreateNoWindow = true;

                psi.RedirectStandardOutput = true;

                psi.RedirectStandardError = true;

                psi.StandardOutputEncoding =
                    System.Text.Encoding.UTF8;

                psi.StandardErrorEncoding =
                    System.Text.Encoding.UTF8;

                psi.ArgumentList.Add(
                    scriptPath);

                psi.ArgumentList.Add(
                    imagePath);

                psi.ArgumentList.Add(
                    outputDir);

                using Process process =
                    new Process();

                process.StartInfo = psi;

                process.Start();

                string stdout =
                    await process.StandardOutput.ReadToEndAsync();

                string stderr =
                    await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                // =========================================================
                // Pythonエラー
                // =========================================================

                if (process.ExitCode != 0)
                {
                    richTextBox1.Text =
                        "OCRでエラーが発生しました。\r\n\r\n" +
                        "終了コード: " +
                        process.ExitCode +
                        "\r\n\r\n" +
                        "【標準出力】\r\n" +
                        stdout +
                        "\r\n\r\n" +
                        "【エラー出力】\r\n" +
                        stderr;

                    MessageBox.Show(
                        "NDLOCR-Liteが異常終了しました。\r\n\r\n" +
                        "終了コード: " +
                        process.ExitCode +
                        "\r\n\r\n" +
                        "【エラー出力】\r\n" +
                        stderr,
                        "NDLOCR-Lite エラー",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return;
                }

                // =========================================================
                // 自動領域JSON
                // =========================================================

                string resultPath =
                    Path.Combine(
                        outputDir,
                        "auto_regions.json");

                if (!File.Exists(resultPath))
                {
                    MessageBox.Show(
                        "OCR結果が見つかりません。\r\n\r\n" +
                        resultPath +
                        "\r\n\r\n" +
                        "Python出力:\r\n" +
                        stdout +
                        "\r\n\r\n" +
                        "エラー出力:\r\n" +
                        stderr,
                        "OCRエラー",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return;
                }

                // =========================================================
                // JSON読み込み
                // =========================================================

                string json =
                    await File.ReadAllTextAsync(
                        resultPath,
                        System.Text.Encoding.UTF8);

                AutoRegionsResult? result =
                    JsonSerializer.Deserialize<AutoRegionsResult>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                if (result == null)
                {
                    MessageBox.Show(
                        "OCR結果JSONを読み込めませんでした。",
                        "OCRエラー",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    return;
                }

                // =========================================================
                // OCR結果表示
                // =========================================================

                richTextBox1.Clear();

                foreach (OcrRegion region in regions)
                {
                    richTextBox1.AppendText(
                        $"========== {region.Name} ==========\r\n");

                    int count = 0;

                    foreach (OcrResult item in result.results)
                    {
                        if (!IsPointInsideRegion(item, region))
                            continue;

                        richTextBox1.AppendText(
                            item.text +
                            Environment.NewLine);

                        count++;
                    }

                    richTextBox1.AppendText(
                        $"[この領域のOCR数: {count}]\r\n\r\n");
                }

                richTextBox1.AppendText(
                    $"全OCR検出数: {result.results.Count}\r\n");

                richTextBox1.AppendText(
                    $"自動領域数: {regions.Count}\r\n");



                // =========================================================
                // 自動レイアウトJSONを読み込む
                // =========================================================

                string autoLayoutPath =
                    Path.Combine(
                        outputDir,
                        "auto_layout.json");

                if (File.Exists(autoLayoutPath))
                {
                    string autoLayoutJson =
                        await File.ReadAllTextAsync(
                            autoLayoutPath,
                            System.Text.Encoding.UTF8);

                    AutoLayout? autoLayout =
                        JsonSerializer.Deserialize<AutoLayout>(
                            autoLayoutJson,
                            new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                    if (autoLayout != null)
                    {
                        ApplyAutoLayout(autoLayout);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "OCRエラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnStartOcr.Enabled = true;
            }
        }


    }
}