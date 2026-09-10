using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OCR_Translator.Forms;
using OCR_Translator.Models;
using OCR_Translator.Services;
using ResizeMode = OCR_Translator.Services.ImageCoordinateHelper.ResizeMode;

namespace OCR_Translator
{
    public partial class Form1 : Form
    {
        // PDF表示状態
        public const int DefaultPdfRenderDpi = 300;
        private PdfDocument? pdfDocument;
        private int currentPage = 0;
        private string? currentPdfPath;

        // 設定・ストレージ
        private AppSettings appSettings = new AppSettings();
        private readonly LayoutStorage _layoutStorage = new LayoutStorage();

        // 領域データ
        private List<OcrRegion> regions = new List<OcrRegion>();
        private Dictionary<int, List<OcrRegion>> pageRegions = new();
        private Dictionary<int, List<OcrRegion>> autoPageRegions = new();

        // 領域マウスインタラクション状態
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

        // 表の罫線編集コントロール・状態
        private Button btnTableAddHLine = null!;
        private Button btnTableAddVLine = null!;
        private Button btnTableDeleteLine = null!;
        private Button btnTableClearLines = null!;
        private Label lblTableLineStatus = null!;

        private ImageCoordinateHelper.RuleLineType activeLineAddMode = ImageCoordinateHelper.RuleLineType.None;
        private bool isLineDeleteMode = false;

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

        private int hoveringRuleLineIndex = -1;
        private int hoveringRuleRegionIndex = -1;
        private ImageCoordinateHelper.RuleLineHitPart hoveringRuleLinePart = ImageCoordinateHelper.RuleLineHitPart.None;

        // OCR結果・UIコントロール
        private RichTextBox txtLog = null!;
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

        private float currentZoomFactor = 0f; // 0 = 全体表示 (Fit)
        private bool isUpdatingZoomCombo = false;
        private bool isUpdatingPageJump = false;
        private BatchSearchForm? _batchSearchForm;

        public int TotalPdfPages => pdfDocument?.PageCount ?? 0;
        public int TargetPageStart => (int)numPageStart.Value;
        public int TargetPageEnd => (int)numPageEnd.Value;
        public string CurrentPdfFileNameWithoutExtension => string.IsNullOrEmpty(currentPdfPath) ? "" : System.IO.Path.GetFileNameWithoutExtension(currentPdfPath);
        public string GetOcrResultText(string key) => ocrResultTextBoxes.TryGetValue(key, out var box) ? box.Text : "";

        public Form1()
        {
            InitializeComponent();
            appSettings = SettingsManager.LoadSettings();

            Cursor = Cursors.Default;
            KeyPreview = true;
            KeyDown += Form1_KeyDown;
            pictureBox1.MouseLeave += pictureBox1_MouseLeave;

            InitializeLogView();
            InitializeOcrResultView();
            InitializeTableRuleLineControls();
            InitializeBatchDashboard();
            InitializeZoomControls();
            InitializePageJumpControls();
            InitializeToolbarColorIcons();
            ApplySettingsToViews();

            lblOrientationBadge.Click += btnOptions_Click;
            lblDocTypeBadge.Click += btnOptions_Click;
            lblDeckBadge.Click += lblDeckBadge_Click;

            numX.ValueChanged += (s, e) => ApplyNumericBoundsToSelectedRegion();
            numY.ValueChanged += (s, e) => ApplyNumericBoundsToSelectedRegion();
            numWidth.ValueChanged += (s, e) => ApplyNumericBoundsToSelectedRegion();
            numHeight.ValueChanged += (s, e) => ApplyNumericBoundsToSelectedRegion();

            numPageStart.ValueChanged += (s, e) => UpdateTargetRangeDisplay();
            numPageEnd.ValueChanged += (s, e) => UpdateTargetRangeDisplay();

            btnAutoLayout.Click -= btnAutoLayout_Click;
            btnAutoLayout.Click += btnAutoLayout_Click;

            btnExportWord.Click += btnExportWord_Click;
            btnSearchBatch.Click += btnSearchBatch_Click;

            pictureBox1.Resize += (s, e) => pictureBox1.Invalidate();
            FormClosing += (s, e) => SaveCurrentPageData();
        }

        private void btnSearchBatch_Click(object? sender, EventArgs e) => OpenBatchSearchDialog();

        private void Form1_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.F)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                OpenBatchSearchDialog();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                CancelAllInteractiveDrawingModes();
            }
        }

        public void OpenBatchSearchDialog()
        {
            if (_batchSearchForm != null && !_batchSearchForm.IsDisposed)
            {
                _batchSearchForm.UpdateScopeOptions();
                _batchSearchForm.BringToFront();
                _batchSearchForm.Activate();

                RichTextBox? activeBox = tabOcrResult?.SelectedTab?.Controls.OfType<RichTextBox>().FirstOrDefault();
                if (activeBox != null && activeBox.SelectionLength > 0 && !string.IsNullOrWhiteSpace(activeBox.SelectedText))
                {
                    _batchSearchForm.SetSearchQuery(activeBox.SelectedText.Trim());
                }
                return;
            }

            _batchSearchForm = new BatchSearchForm(this);
            RichTextBox? currentBox = tabOcrResult?.SelectedTab?.Controls.OfType<RichTextBox>().FirstOrDefault();
            if (currentBox != null && currentBox.SelectionLength > 0 && !string.IsNullOrWhiteSpace(currentBox.SelectedText))
            {
                _batchSearchForm.SetSearchQuery(currentBox.SelectedText.Trim());
            }
            _batchSearchForm.Show(this);
        }

        private void InitializeToolbarColorIcons()
        {
            void SetButtonColorIcon(Button btn, string iconName)
            {
                if (btn == null) return;
                btn.Text = "";
                btn.Image = ColorIconHelper.CreateColorIcon(iconName, 32);
                btn.ImageAlign = ContentAlignment.MiddleCenter;
                btn.TextImageRelation = TextImageRelation.Overlay;
                btn.UseVisualStyleBackColor = true;
            }

            SetButtonColorIcon(btnOpenPdf, "open_pdf");
            SetButtonColorIcon(btnClosePdf, "close_pdf");
            SetButtonColorIcon(btnFirstPage, "first_page");
            SetButtonColorIcon(btnPrevPage, "prev_page");
            SetButtonColorIcon(btnNextPage, "next_page");
            SetButtonColorIcon(btnLastPage, "last_page");
            SetButtonColorIcon(btnNextBatch20, "next_batch");
            SetButtonColorIcon(btnZoomOut, "zoom_out");
            SetButtonColorIcon(btnZoomIn, "zoom_in");
            SetButtonColorIcon(btnRegionSettings, "region_settings");
            SetButtonColorIcon(btnReorderMode, "reorder_mode");
            SetButtonColorIcon(btnAutoLayout, "auto_layout");
            SetButtonColorIcon(btnStartOcr, "start_ocr");
            SetButtonColorIcon(btnAddHeading, "add_heading");
            SetButtonColorIcon(btnAddFootnote, "add_footnote");
            SetButtonColorIcon(btnAddAnnotationNumber, "add_annotation");
            SetButtonColorIcon(btnExportWord, "export_word");
            SetButtonColorIcon(btnSearchBatch, "search");
            SetButtonColorIcon(btnOptions, "options");
        }

        private void InitializeZoomControls()
        {
            cmbZoom.Items.Clear();
            cmbZoom.Items.AddRange(new object[]
            {
                "全体表示 (Fit)",
                "50%",
                "75%",
                "85%",
                "100%"
            });
            cmbZoom.SelectedIndex = 0;

            cmbZoom.SelectedIndexChanged += cmbZoom_SelectedIndexChanged;
            cmbZoom.KeyDown += cmbZoom_KeyDown;
            btnZoomIn.Click += (s, e) => ZoomIn();
            btnZoomOut.Click += (s, e) => ZoomOut();

            pnlCanvasContainer.Resize += (s, e) => UpdateCanvasLayout();
            pictureBox1.MouseWheel += pictureBox1_MouseWheel;
            pnlCanvasContainer.MouseWheel += pictureBox1_MouseWheel;
        }

        private void InitializePageJumpControls()
        {
            numCurrentPage.ValueChanged += (s, e) =>
            {
                if (isUpdatingPageJump || pdfDocument == null) return;
                int targetPage = (int)numCurrentPage.Value - 1;
                if (targetPage >= 0 && targetPage < pdfDocument.PageCount && targetPage != currentPage)
                {
                    SwitchToPage(targetPage);
                }
            };

            numCurrentPage.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && pdfDocument != null)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    int targetPage = (int)numCurrentPage.Value - 1;
                    if (targetPage >= 0 && targetPage < pdfDocument.PageCount && targetPage != currentPage)
                    {
                        SwitchToPage(targetPage);
                    }
                }
            };
        }

        private void InitializeLogView()
        {
            txtLog = new RichTextBox
            {
                Dock = DockStyle.Bottom,
                Height = 140,
                ReadOnly = true,
                BackColor = SystemColors.Window,
                Font = new Font("Consolas", 9f),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            Controls.Add(txtLog);
        }

        private void InitializeOcrResultView()
        {
            tabOcrResult = new TabControl
            {
                Dock = DockStyle.Fill,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                SizeMode = TabSizeMode.Normal,
                ItemSize = new Size(0, 44),
                Padding = new Point(20, 8),
                Font = new Font("Meiryo UI", 10.5f, FontStyle.Bold)
            };

            // タブのカスタム描画：通常比約2倍の大型タブ＋領域枠線色と完全同期したカラーコーディング
            tabOcrResult.DrawItem += (sender, e) =>
            {
                if (e.Index < 0 || e.Index >= tabOcrResult.TabPages.Count) return;

                var tabPage = tabOcrResult.TabPages[e.Index];
                var tabRect = tabOcrResult.GetTabRect(e.Index);
                bool isSelected = (tabOcrResult.SelectedIndex == e.Index);
                string text = tabPage.Text;

                // 領域枠線色に連動させた配色定義
                // body = Blue, table = Orange, heading = Green, footnote = Gray, image = DeepSkyBlue
                Color accentColor;
                Color lightBg;
                Color darkText;

                if (text.Contains("本文"))
                {
                    accentColor = Color.FromArgb(24, 100, 215);  // Blue (本文枠線色)
                    lightBg = Color.FromArgb(238, 244, 255);
                    darkText = Color.FromArgb(15, 65, 145);
                }
                else if (text.Contains("表"))
                {
                    accentColor = Color.FromArgb(235, 120, 0);   // Orange (表枠線色)
                    lightBg = Color.FromArgb(255, 246, 235);
                    darkText = Color.FromArgb(160, 75, 0);
                }
                else if (text.Contains("見出し"))
                {
                    accentColor = Color.FromArgb(40, 145, 55);   // Green (見出し枠線色)
                    lightBg = Color.FromArgb(236, 248, 238);
                    darkText = Color.FromArgb(25, 100, 35);
                }
                else if (text.Contains("注釈"))
                {
                    accentColor = Color.FromArgb(115, 125, 135); // Gray (注釈文枠線色)
                    lightBg = Color.FromArgb(245, 246, 248);
                    darkText = Color.FromArgb(70, 75, 85);
                }
                else if (text.Contains("図"))
                {
                    accentColor = Color.FromArgb(0, 155, 230);   // DeepSkyBlue (図枠線色)
                    lightBg = Color.FromArgb(235, 248, 255);
                    darkText = Color.FromArgb(0, 105, 160);
                }
                else // 未分類など
                {
                    accentColor = Color.FromArgb(125, 75, 165);  // Purple
                    lightBg = Color.FromArgb(248, 242, 252);
                    darkText = Color.FromArgb(85, 45, 115);
                }

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                if (isSelected)
                {
                    // 選択中タブ: 領域の鮮やかなアクセントカラーで塗りつぶし
                    using var bgBrush = new SolidBrush(accentColor);
                    e.Graphics.FillRectangle(bgBrush, tabRect);

                    // 上部に明るいハイライトライン（2px）
                    using var topPen = new Pen(Color.FromArgb(220, Color.White), 2f);
                    e.Graphics.DrawLine(topPen, tabRect.Left, tabRect.Top + 1, tabRect.Right, tabRect.Top + 1);

                    // 白文字でくっきり大きく描画
                    TextRenderer.DrawText(
                        e.Graphics,
                        text,
                        tabOcrResult.Font,
                        tabRect,
                        Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                }
                else
                {
                    // 非選択タブ: 優しいパステル調の背景
                    using var bgBrush = new SolidBrush(lightBg);
                    e.Graphics.FillRectangle(bgBrush, tabRect);

                    // 境界線（薄いグレー）
                    using var borderPen = new Pen(Color.FromArgb(210, 215, 225), 1f);
                    e.Graphics.DrawRectangle(borderPen, tabRect);

                    // 上部に領域枠線色のカラーバー（4px）を配置し、非選択時も一目でカテゴリが識別可能
                    using var topBrush = new SolidBrush(accentColor);
                    e.Graphics.FillRectangle(topBrush, tabRect.Left, tabRect.Top, tabRect.Width, 4);

                    // 濃い領域カラーの文字で描画
                    TextRenderer.DrawText(
                        e.Graphics,
                        text,
                        tabOcrResult.Font,
                        tabRect,
                        darkText,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                }
            };

            tabOcrText = new TabPage("本文");
            tabOcrTable = new TabPage("表");

            tableLayoutPanel1.Controls.Remove(richTextBox1);
            richTextBox1.Dock = DockStyle.Fill;
            richTextBox1.HideSelection = false;
            richTextBox1.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.F)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    OpenBatchSearchDialog();
                }
            };
            richTextBox1.HandleCreated += (s, e) => ApplyMarginsToOcrTextBox(richTextBox1);
            if (richTextBox1.IsHandleCreated)
                ApplyMarginsToOcrTextBox(richTextBox1);
            SetupOcrResultContextMenu(richTextBox1);
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
                tabOcrResult.Invalidate();

                if (tabOcrResult.SelectedTab?.Text.Contains("見出し") == true)
                {
                    SyncHeadings(null, updateHeadingTab: true);
                    if (ocrResultTextBoxes.TryGetValue("heading", out var currentBox))
                    {
                        ApplyMarginsToOcrTextBox(currentBox);
                    }
                    SaveCurrentPageData();
                }
                else
                {
                    SaveCurrentPageData();

                    if (tabOcrResult.SelectedTab == tabOcrImage)
                    {
                        GetAllFigureItems();
                        RefreshFigureGalleryView();
                    }
                    else if (tabOcrResult.SelectedTab == tabOcrTable)
                    {
                        ActivateCurrentPageTableRegion();
                    }
                    else if (tabOcrResult.SelectedTab?.Controls.OfType<RichTextBox>().FirstOrDefault() is RichTextBox currentBox)
                    {
                        ApplyMarginsToOcrTextBox(currentBox);
                    }
                }
            };

            AddOcrResultTab("unclassified", "未分類");

            // 表操作用ツールバーパネル（上下間隔・ボタン高さを約2.5倍に拡張し、文字がゆったり全て見える設計）
            var pnlTableToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 58),
                Padding = new Padding(8, 8, 8, 8),
                BackColor = Color.FromArgb(248, 249, 250),
                WrapContents = true
            };

            var btnMergeCells = new Button
            {
                Text = "⊞ 選択セルを結合",
                Size = new Size(190, 40),
                Margin = new Padding(3, 2, 5, 2),
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
                Size = new Size(165, 40),
                Margin = new Padding(3, 2, 5, 2),
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
                Size = new Size(110, 40),
                Margin = new Padding(3, 2, 5, 2),
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

            var btnJumpToRegion = new Button
            {
                Text = "🔍 画像の表領域・罫線を編集",
                Size = new Size(410, 40),
                Margin = new Padding(3, 2, 5, 2),
                BackColor = Color.Ivory,
                FlatStyle = FlatStyle.System
            };
            btnJumpToRegion.Click += (s, e) =>
            {
                if (dgvOcrTable?.CurrentRow != null && dgvOcrTable.CurrentRow.Index >= 0)
                {
                    ActivateTableRegionFromGridRow(dgvOcrTable.CurrentRow.Index);
                }
                else
                {
                    ActivateCurrentPageTableRegion();
                }
            };

            var btnConcatenateTables = new Button
            {
                Text = "🔗 表を連結 (複数領域・ページまたぎ)",
                Size = new Size(260, 40),
                Margin = new Padding(3, 2, 5, 2),
                BackColor = Color.FromArgb(230, 245, 255),
                FlatStyle = FlatStyle.System
            };
            btnConcatenateTables.Click += (s, e) => OpenTableConcatenateDialog();

            var btnReloadTableFromDisk = new Button
            {
                Text = "🔄 ディスクから表を再読込",
                Size = new Size(200, 40),
                Margin = new Padding(3, 2, 5, 2),
                BackColor = Color.FromArgb(240, 248, 255),
                FlatStyle = FlatStyle.System
            };
            btnReloadTableFromDisk.Click += (s, e) => ReloadCurrentPageTableFromDisk();

            pnlTableToolbar.Controls.Add(btnMergeCells);
            pnlTableToolbar.Controls.Add(btnAutoMergeBlanks);
            pnlTableToolbar.Controls.Add(btnUnmergeCells);
            pnlTableToolbar.Controls.Add(btnJumpToRegion);
            pnlTableToolbar.Controls.Add(btnConcatenateTables);
            pnlTableToolbar.Controls.Add(btnReloadTableFromDisk);

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
                MultiSelect = true,
                Font = appSettings.CreateFont()
            };
            dgvOcrTable.DefaultCellStyle.Font = appSettings.CreateFont();
            dgvOcrTable.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // 表グリッドのセルまたは行をクリックした際、該当ページの表領域（外枠・罫線）を復元して選択
            dgvOcrTable.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                ActivateTableRegionFromGridRow(e.RowIndex);
            };

            dgvOcrTable.RowHeaderMouseClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                ActivateTableRegionFromGridRow(e.RowIndex);
            };

            // 右クリック時に未選択セルであればそのセルを選択対象にする
            dgvOcrTable.CellMouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    if (!dgvOcrTable.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected)
                    {
                        dgvOcrTable.ClearSelection();
                        dgvOcrTable.CurrentCell = dgvOcrTable.Rows[e.RowIndex].Cells[e.ColumnIndex];
                        dgvOcrTable.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected = true;
                    }
                }
            };

            // コンテキストメニュー (右クリック)
            var contextMenu = new ContextMenuStrip();
            var mnuMerge = new ToolStripMenuItem("選択セルを結合 (Ctrl+M)");
            mnuMerge.Click += (s, e) => { if (dgvOcrTable != null) TableCellMerger.MergeSelectedCells(dgvOcrTable, tableMergeSpans); };

            var mnuAutoMerge = new ToolStripMenuItem("空白セルを一括結合");
            mnuAutoMerge.Click += (s, e) => { if (dgvOcrTable != null) TableCellMerger.AutoMergeBlankCells(dgvOcrTable, tableMergeSpans); };

            var mnuUnmerge = new ToolStripMenuItem("セル結合を解除");
            mnuUnmerge.Click += (s, e) => { if (dgvOcrTable != null) TableCellMerger.UnmergeSelectedCells(dgvOcrTable, tableMergeSpans); };

            var mnuSplitBySpace = new ToolStripMenuItem("セルの値を空白で右列へ分割");
            mnuSplitBySpace.Click += (s, e) =>
            {
                if (dgvOcrTable != null && TableCellMerger.SplitSelectedCellBySpace(dgvOcrTable, tableMergeSpans))
                {
                    SaveCurrentPageData();
                }
            };

            var mnuShiftRight = new ToolStripMenuItem("セルの値を右列へ移動");
            mnuShiftRight.Click += (s, e) =>
            {
                if (dgvOcrTable != null && TableCellMerger.ShiftSelectedCellValueRight(dgvOcrTable, tableMergeSpans))
                {
                    SaveCurrentPageData();
                }
            };

            var mnuShiftLeft = new ToolStripMenuItem("セルの値を左列へ移動");
            mnuShiftLeft.Click += (s, e) =>
            {
                if (dgvOcrTable != null && TableCellMerger.ShiftSelectedCellValueLeft(dgvOcrTable, tableMergeSpans))
                {
                    SaveCurrentPageData();
                }
            };

            var mnuCopy = new ToolStripMenuItem("Word/Excel用に表をコピー (Ctrl+C)");
            mnuCopy.Click += (s, e) => { if (dgvOcrTable != null) TableCellMerger.CopyTableToClipboard(dgvOcrTable, tableMergeSpans); };

            var mnuJumpToRegion = new ToolStripMenuItem("画像の表領域へ移動・罫線を再編集");
            mnuJumpToRegion.Click += (s, e) =>
            {
                if (dgvOcrTable?.CurrentRow != null && dgvOcrTable.CurrentRow.Index >= 0)
                {
                    ActivateTableRegionFromGridRow(dgvOcrTable.CurrentRow.Index);
                }
            };

            var mnuConcatenate = new ToolStripMenuItem("🔗 選択した表を連結 (複数領域・ページまたぎ)...");
            mnuConcatenate.Click += (s, e) => OpenTableConcatenateDialog();

            contextMenu.Items.Add(mnuMerge);
            contextMenu.Items.Add(mnuAutoMerge);
            contextMenu.Items.Add(mnuUnmerge);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(mnuSplitBySpace);
            contextMenu.Items.Add(mnuShiftRight);
            contextMenu.Items.Add(mnuShiftLeft);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(mnuCopy);
            var mnuReloadFromDisk = new ToolStripMenuItem("🔄 ディスクの保存データから表を再読み込み");
            mnuReloadFromDisk.Click += (s, e) => ReloadCurrentPageTableFromDisk();

            contextMenu.Items.Add(mnuJumpToRegion);
            contextMenu.Items.Add(mnuConcatenate);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(mnuReloadFromDisk);
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

            // 結合セルの編集開始時に、既存の結合テキストをエディタに初期表示
            dgvOcrTable.CellBeginEdit += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var span = tableMergeSpans.FirstOrDefault(sp => sp.Contains(e.ColumnIndex, e.RowIndex));
                if (span != null)
                {
                    string currentText = !string.IsNullOrWhiteSpace(dgvOcrTable.Rows[span.StartRow].Cells[span.StartCol].Value?.ToString())
                        ? dgvOcrTable.Rows[span.StartRow].Cells[span.StartCol].Value!.ToString()!
                        : (span.MergedText ?? "");

                    if (dgvOcrTable.EditingControl is TextBox tb)
                    {
                        tb.Text = currentText;
                        tb.SelectAll();
                    }
                }
            };

            // セル編集確定時（Enter/フォーカス移動）に、ユーザー入力値（ロシア語含む）を結合スパンおよびセルへ確実に反映・永続化
            dgvOcrTable.CellEndEdit += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

                string editedText = dgvOcrTable.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "";

                var span = tableMergeSpans.FirstOrDefault(sp => sp.Contains(e.ColumnIndex, e.RowIndex));
                if (span != null)
                {
                    span.MergedText = editedText;
                    if (span.StartRow < dgvOcrTable.RowCount && span.StartCol < dgvOcrTable.ColumnCount)
                    {
                        dgvOcrTable.Rows[span.StartRow].Cells[span.StartCol].Value = editedText;
                    }
                    for (int r = span.StartRow; r < span.StartRow + span.RowSpan && r < dgvOcrTable.RowCount; r++)
                    {
                        for (int c = span.StartCol; c < span.StartCol + span.ColSpan && c < dgvOcrTable.ColumnCount; c++)
                        {
                            if (r != span.StartRow || c != span.StartCol)
                            {
                                dgvOcrTable.Rows[r].Cells[c].Value = "";
                            }
                        }
                    }
                    dgvOcrTable.Invalidate();
                }

                SaveCurrentPageData();
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

        private void AddOcrResultTab(string type, string title)
        {
            if (tabOcrResult == null) return;
            RichTextBox resultBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                HideSelection = false,
                DetectUrls = false,
                Font = appSettings.CreateFont()
            };
            resultBox.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.F)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    OpenBatchSearchDialog();
                }
            };
            resultBox.HandleCreated += (s, e) => ApplyMarginsToOcrTextBox(resultBox);
            if (resultBox.IsHandleCreated)
                ApplyMarginsToOcrTextBox(resultBox);
            SetupOcrResultContextMenu(resultBox);

            TabPage page = new TabPage(title);
            page.Controls.Add(resultBox);
            tabOcrResult.TabPages.Add(page);
            ocrResultTextBoxes[type] = resultBox;

            if (type == "heading")
            {
                resultBox.TextChanged += (s, e) => OnHeadingTabTextChanged(resultBox);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int EM_SETMARGINS = 0x00D3;
        private const int EC_LEFTMARGIN = 0x0001;
        private const int EC_RIGHTMARGIN = 0x0002;

        /// <summary>
        /// OCR結果表示テキストボックスの左右余白（約3文字分）を設定します。
        /// </summary>
        private void ApplyMarginsToOcrTextBox(RichTextBox? box)
        {
            if (box == null) return;
            try
            {
                Font f = box.Font ?? appSettings.CreateFont();
                // 3文字分の余白幅（フォントの全角3文字相当の幅を目安に算出）
                int char3Width = TextRenderer.MeasureText("あああ", f).Width - TextRenderer.MeasureText("", f).Width;
                int margin = Math.Max(24, char3Width);

                if (box.IsHandleCreated)
                {
                    SendMessage(box.Handle, EM_SETMARGINS, (IntPtr)(EC_LEFTMARGIN | EC_RIGHTMARGIN), (IntPtr)((margin << 16) | margin));
                }
            }
            catch
            {
                // エラー時は何もしない
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

            // 段組バッジ (自動/1段/2段/3段)
            if (lblDeckBadge != null)
            {
                switch (appSettings.DeckCount)
                {
                    case 1:
                        lblDeckBadge.Text = " 📑 1段組 ";
                        lblDeckBadge.BackColor = Color.FromArgb(74, 85, 104); // Slate Blue
                        lblDeckBadge.ForeColor = Color.White;
                        break;
                    case 2:
                        lblDeckBadge.Text = " 📑 2段組 ";
                        lblDeckBadge.BackColor = Color.FromArgb(30, 58, 138); // Royal / Navy Blue
                        lblDeckBadge.ForeColor = Color.White;
                        break;
                    case 3:
                        lblDeckBadge.Text = " 📑 3段組 ";
                        lblDeckBadge.BackColor = Color.FromArgb(13, 148, 136); // Teal
                        lblDeckBadge.ForeColor = Color.White;
                        break;
                    default:
                        lblDeckBadge.Text = " 📑 自動段組 ";
                        lblDeckBadge.BackColor = Color.FromArgb(100, 116, 139); // Slate Gray
                        lblDeckBadge.ForeColor = Color.White;
                        break;
                }
            }
        }

        private void lblDeckBadge_Click(object? sender, EventArgs e)
        {
            // 自動(0) -> 1段(1) -> 2段(2) -> 3段(3) -> 自動(0) をローテーション
            appSettings.DeckCount = (appSettings.DeckCount + 1) % 4;
            SettingsManager.SaveSettings(appSettings);
            UpdateOptionBadges();

            string deckName = appSettings.DeckCount switch
            {
                1 => "1段組（単段）",
                2 => "2段組（上・下段）",
                3 => "3段組",
                _ => "自動判定"
            };

            txtLog.AppendText($"【段組切替】段組設定を「{deckName}」に変更しました。" + Environment.NewLine);
        }

        private void ApplySettingsToViews()
        {
            UpdateOptionBadges();
            UpdatePageDisplayTitle();

            if (btnNextBatch20 != null)
            {
                btnNextBatch20.Text = "";
                toolTipMain.SetToolTip(btnNextBatch20, $"次の{appSettings.BatchPageSize}ページを一括範囲設定 (バッチ送り)");
            }

            Font contentFont = appSettings.CreateFont();

            if (richTextBox1 != null)
            {
                richTextBox1.Font = contentFont;
                ApplyMarginsToOcrTextBox(richTextBox1);
            }

            foreach (var box in ocrResultTextBoxes.Values)
            {
                box.Font = contentFont;
                ApplyMarginsToOcrTextBox(box);
            }

            if (dgvOcrTable != null)
            {
                dgvOcrTable.Font = contentFont;
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
                RefreshBatchList();

                if (pdfDocument != null)
                {
                    SyncAndRenumberAllFootnotesUi();
                }

                string docTypeName = appSettings.DocumentType == "western" ? "洋書（英欧文）" : "和書（日本語）";
                string orientationName = appSettings.TextOrientation switch
                {
                    "vertical" => "縦書き優先",
                    "horizontal" => "横書き優先",
                    _ => "自動判定"
                };
                string navScopeName = appSettings.FirstLastNavScope == "file" ? "ファイル全体" : "作業バッチ範囲内";

                txtLog.AppendText(
                    $"【設定保存】フォント: {appSettings.FontFamilyName} {appSettings.FontSize:0.#}pt" +
                    $"{(appSettings.FontBold ? " (太字)" : "")} / 組方向: {orientationName} / 書籍種別: {docTypeName} / 1行文字数: {(appSettings.LineCharCount > 0 ? $"{appSettings.LineCharCount}文字" : "段落単位")} / 注釈採番: {(appSettings.FootnoteNumberingScope == "majorHeading" ? "大見出し単位" : "通し番号")} / 小見出し自動認識: {(appSettings.AutoDetectSubheadings ? "する" : "しない")} / バッチサイズ: {appSettings.BatchPageSize}P / OCR後倍率: {appSettings.PostOcrZoomRatio} / 移動: {navScopeName}" +
                    Environment.NewLine);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                SaveCurrentPageRegions();
                SaveCurrentPageData();
            }
            catch { }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                SaveCurrentPageRegions();
                SaveCurrentPageData();
            }
            catch { }
            pdfDocument?.Dispose();
            pdfDocument = null;
            base.OnFormClosed(e);
        }
    }
}
