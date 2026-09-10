namespace OCR_Translator
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            toolTipMain = new ToolTip(components);

            tableLayoutPanel1 = new TableLayoutPanel();
            pnlCanvasContainer = new Panel();
            pictureBox1 = new PictureBox();
            richTextBox1 = new RichTextBox();
            panelToolbar = new FlowLayoutPanel();
            btnOpenPdf = new Button();
            btnClosePdf = new Button();
            btnFirstPage = new Button();
            btnPrevPage = new Button();
            numCurrentPage = new NumericUpDown();
            lblTotalPages = new Label();
            btnNextPage = new Button();
            btnLastPage = new Button();
            lblTargetRangePrefix = new Label();
            numPageStart = new NumericUpDown();
            lblRangeDash = new Label();
            numPageEnd = new NumericUpDown();
            lblTargetRangeInfo = new Label();
            btnNextBatch20 = new Button();
            btnZoomOut = new Button();
            cmbZoom = new ComboBox();
            btnZoomIn = new Button();
            btnRegionSettings = new Button();
            btnReorderMode = new Button();
            btnAutoLayout = new Button();
            btnStartOcr = new Button();
            btnAddHeading = new Button();
            btnAddFootnote = new Button();
            btnAddAnnotationNumber = new Button();
            btnExportWord = new Button();
            btnSearchBatch = new Button();
            btnOptions = new Button();
            lblOrientationBadge = new Label();
            lblDocTypeBadge = new Label();
            lblDeckBadge = new Label();
            pnlRegionSettings = new Panel();
            lstRegions = new ListBox();
            btnDeleteRegion = new Button();
            numHeight = new NumericUpDown();
            numWidth = new NumericUpDown();
            numY = new NumericUpDown();
            numX = new NumericUpDown();
            btnSaveLayout = new Button();
            btnCopyRegionsToPages = new Button();
            btnReOcrRegion = new Button();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            panelToolbar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numPageStart).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numPageEnd).BeginInit();
            pnlRegionSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numHeight).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numWidth).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numY).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numX).BeginInit();
            SuspendLayout();
            // 
            // pnlRegionSettings（画面左端）
            // 
            pnlRegionSettings.AccessibleName = "pnlRegionSettings";
            pnlRegionSettings.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            pnlRegionSettings.AutoScroll = true;
            pnlRegionSettings.Controls.Add(lstRegions);
            pnlRegionSettings.Controls.Add(btnDeleteRegion);
            pnlRegionSettings.Controls.Add(numHeight);
            pnlRegionSettings.Controls.Add(numWidth);
            pnlRegionSettings.Controls.Add(numY);
            pnlRegionSettings.Controls.Add(numX);
            pnlRegionSettings.Controls.Add(btnSaveLayout);
            pnlRegionSettings.Controls.Add(btnCopyRegionsToPages);
            pnlRegionSettings.Controls.Add(btnReOcrRegion);
            pnlRegionSettings.Location = new Point(0, 84);
            pnlRegionSettings.Name = "pnlRegionSettings";
            pnlRegionSettings.Size = new Size(280, 668);
            pnlRegionSettings.TabIndex = 5;
            // 
            // lstRegions
            // 
            lstRegions.FormattingEnabled = true;
            lstRegions.Location = new Point(15, 15);
            lstRegions.Name = "lstRegions";
            lstRegions.Size = new Size(245, 115);
            lstRegions.TabIndex = 9;
            lstRegions.SelectedIndexChanged += lstRegions_SelectedIndexChanged;
            // 
            // btnDeleteRegion
            // 
            btnDeleteRegion.Location = new Point(15, 138);
            btnDeleteRegion.Name = "btnDeleteRegion";
            btnDeleteRegion.Size = new Size(245, 34);
            btnDeleteRegion.TabIndex = 8;
            btnDeleteRegion.Text = "🗑 領域削除";
            btnDeleteRegion.UseVisualStyleBackColor = true;
            btnDeleteRegion.Click += btnDeleteRegion_Click;
            // 
            // numX
            // 
            numX.Location = new Point(15, 185);
            numX.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            numX.Name = "numX";
            numX.Size = new Size(116, 31);
            numX.TabIndex = 1;
            // 
            // numY
            // 
            numY.Location = new Point(144, 185);
            numY.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            numY.Name = "numY";
            numY.Size = new Size(116, 31);
            numY.TabIndex = 2;
            // 
            // numWidth
            // 
            numWidth.Location = new Point(15, 230);
            numWidth.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            numWidth.Name = "numWidth";
            numWidth.Size = new Size(116, 31);
            numWidth.TabIndex = 3;
            // 
            // numHeight
            // 
            numHeight.Location = new Point(144, 230);
            numHeight.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            numHeight.Name = "numHeight";
            numHeight.Size = new Size(116, 31);
            numHeight.TabIndex = 4;
            // 
            // btnSaveLayout
            // 
            btnSaveLayout.Location = new Point(15, 480);
            btnSaveLayout.Name = "btnSaveLayout";
            btnSaveLayout.Size = new Size(245, 34);
            btnSaveLayout.TabIndex = 5;
            btnSaveLayout.Text = "💾 領域設定を保存";
            btnSaveLayout.UseVisualStyleBackColor = true;
            btnSaveLayout.Click += btnSaveLayout_Click;
            // 
            // btnCopyRegionsToPages
            // 
            btnCopyRegionsToPages.Location = new Point(15, 520);
            btnCopyRegionsToPages.Name = "btnCopyRegionsToPages";
            btnCopyRegionsToPages.Size = new Size(245, 34);
            btnCopyRegionsToPages.TabIndex = 6;
            btnCopyRegionsToPages.Text = "📋 領域を他ページへコピー";
            btnCopyRegionsToPages.UseVisualStyleBackColor = true;
            btnCopyRegionsToPages.Click += btnCopyRegionsToPages_Click;
            toolTipMain.SetToolTip(btnCopyRegionsToPages, "現在のページで設定した領域を他のページ（全ページ/指定範囲）へ一括コピー");
            // 
            // btnReOcrRegion
            // 
            btnReOcrRegion.Location = new Point(15, 560);
            btnReOcrRegion.Name = "btnReOcrRegion";
            btnReOcrRegion.Size = new Size(245, 34);
            btnReOcrRegion.TabIndex = 7;
            btnReOcrRegion.Text = "🎯 選択領域のみ再OCR";
            btnReOcrRegion.UseVisualStyleBackColor = true;
            btnReOcrRegion.Click += btnReOcrRegion_Click;
            toolTipMain.SetToolTip(btnReOcrRegion, "選択した領域のみを再OCRし、既存の編集済みテキストを保護した上で確認・反映します");
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Controls.Add(pnlCanvasContainer, 0, 0);
            tableLayoutPanel1.Controls.Add(richTextBox1, 1, 0);
            tableLayoutPanel1.Location = new Point(285, 84);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 1;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Size = new Size(1094, 668);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // pnlCanvasContainer
            // 
            pnlCanvasContainer.AutoScroll = true;
            pnlCanvasContainer.BackColor = Color.FromArgb(240, 242, 245);
            pnlCanvasContainer.Controls.Add(pictureBox1);
            pnlCanvasContainer.Dock = DockStyle.Fill;
            pnlCanvasContainer.Location = new Point(3, 3);
            pnlCanvasContainer.Name = "pnlCanvasContainer";
            pnlCanvasContainer.Size = new Size(541, 662);
            pnlCanvasContainer.TabIndex = 0;
            // 
            // richTextBox1
            // 
            richTextBox1.Dock = DockStyle.Fill;
            richTextBox1.Location = new Point(550, 3);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(541, 662);
            richTextBox1.TabIndex = 1;
            richTextBox1.Text = "";
            // 
            // pictureBox1
            // 
            pictureBox1.BorderStyle = BorderStyle.FixedSingle;
            pictureBox1.Dock = DockStyle.Fill;
            pictureBox1.Location = new Point(0, 0);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(541, 662);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            pictureBox1.Paint += pictureBox1_Paint;
            pictureBox1.MouseDown += pictureBox1_MouseDown;
            pictureBox1.MouseMove += pictureBox1_MouseMove;
            pictureBox1.MouseUp += pictureBox1_MouseUp;
            // 
            // panelToolbar
            // 
            panelToolbar.BackColor = Color.FromArgb(245, 247, 250);
            panelToolbar.Controls.Add(btnOpenPdf);
            panelToolbar.Controls.Add(btnClosePdf);
            panelToolbar.Controls.Add(btnFirstPage);
            panelToolbar.Controls.Add(btnPrevPage);
            panelToolbar.Controls.Add(numCurrentPage);
            panelToolbar.Controls.Add(lblTotalPages);
            panelToolbar.Controls.Add(btnNextPage);
            panelToolbar.Controls.Add(btnLastPage);
            panelToolbar.Controls.Add(btnNextBatch20);
            panelToolbar.Controls.Add(lblTargetRangePrefix);
            panelToolbar.Controls.Add(numPageStart);
            panelToolbar.Controls.Add(lblRangeDash);
            panelToolbar.Controls.Add(numPageEnd);
            panelToolbar.Controls.Add(lblTargetRangeInfo);
            panelToolbar.Controls.Add(btnZoomOut);
            panelToolbar.Controls.Add(cmbZoom);
            panelToolbar.Controls.Add(btnZoomIn);
            panelToolbar.Controls.Add(btnRegionSettings);
            panelToolbar.Controls.Add(btnReorderMode);
            panelToolbar.Controls.Add(btnAutoLayout);
            panelToolbar.Controls.Add(btnStartOcr);
            panelToolbar.Controls.Add(btnAddHeading);
            panelToolbar.Controls.Add(btnAddFootnote);
            panelToolbar.Controls.Add(btnAddAnnotationNumber);
            panelToolbar.Controls.Add(btnExportWord);
            panelToolbar.Controls.Add(btnSearchBatch);
            panelToolbar.Controls.Add(btnOptions);
            panelToolbar.Controls.Add(lblOrientationBadge);
            panelToolbar.Controls.Add(lblDocTypeBadge);
            panelToolbar.Controls.Add(lblDeckBadge);
            panelToolbar.Dock = DockStyle.Top;
            panelToolbar.FlowDirection = FlowDirection.LeftToRight;
            panelToolbar.Location = new Point(0, 0);
            panelToolbar.Name = "panelToolbar";
            panelToolbar.Padding = new Padding(10, 10, 10, 10);
            panelToolbar.Size = new Size(1379, 80);
            panelToolbar.TabIndex = 1;
            panelToolbar.WrapContents = false;
            panelToolbar.AutoScroll = true;
            // 
            // btnOpenPdf
            // 
            btnOpenPdf.Font = new Font("Segoe UI Emoji", 20F, FontStyle.Regular);
            btnOpenPdf.Location = new Point(13, 13);
            btnOpenPdf.Name = "btnOpenPdf";
            btnOpenPdf.Size = new Size(54, 54);
            btnOpenPdf.TabIndex = 0;
            btnOpenPdf.Text = "📂";
            btnOpenPdf.UseVisualStyleBackColor = true;
            btnOpenPdf.Click += btnOpenPdf_Click;
            toolTipMain.SetToolTip(btnOpenPdf, "PDFを開く");
            // 
            // btnClosePdf
            // 
            btnClosePdf.Font = new Font("Segoe UI Emoji", 20F, FontStyle.Regular);
            btnClosePdf.Location = new Point(73, 13);
            btnClosePdf.Name = "btnClosePdf";
            btnClosePdf.Size = new Size(54, 54);
            btnClosePdf.TabIndex = 1;
            btnClosePdf.Text = "📕";
            btnClosePdf.UseVisualStyleBackColor = true;
            btnClosePdf.Click += btnClosePdf_Click;
            toolTipMain.SetToolTip(btnClosePdf, "PDFを保存して閉じる");
            // 
            // btnFirstPage
            // 
            btnFirstPage.Font = new Font("Segoe UI Emoji", 19F, FontStyle.Regular);
            btnFirstPage.Location = new Point(73, 13);
            btnFirstPage.Name = "btnFirstPage";
            btnFirstPage.Size = new Size(54, 54);
            btnFirstPage.TabIndex = 1;
            btnFirstPage.Text = "⏮️";
            btnFirstPage.UseVisualStyleBackColor = true;
            btnFirstPage.Click += btnFirstPage_Click;
            toolTipMain.SetToolTip(btnFirstPage, "最初のページ");
            // 
            // btnPrevPage
            // 
            btnPrevPage.Font = new Font("Segoe UI Emoji", 19F, FontStyle.Regular);
            btnPrevPage.Location = new Point(133, 13);
            btnPrevPage.Name = "btnPrevPage";
            btnPrevPage.Size = new Size(54, 54);
            btnPrevPage.TabIndex = 2;
            btnPrevPage.Text = "◀️";
            btnPrevPage.UseVisualStyleBackColor = true;
            btnPrevPage.Click += btnPrevPage_Click;
            toolTipMain.SetToolTip(btnPrevPage, "前のページ");
            // 
            // numCurrentPage
            // 
            numCurrentPage.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            numCurrentPage.Location = new Point(193, 22);
            numCurrentPage.Margin = new Padding(3, 9, 3, 3);
            numCurrentPage.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numCurrentPage.Maximum = new decimal(new int[] { 1, 0, 0, 0 });
            numCurrentPage.Name = "numCurrentPage";
            numCurrentPage.Size = new Size(62, 35);
            numCurrentPage.TabIndex = 3;
            numCurrentPage.TextAlign = HorizontalAlignment.Center;
            numCurrentPage.Value = new decimal(new int[] { 1, 0, 0, 0 });
            toolTipMain.SetToolTip(numCurrentPage, "表示ページ番号 (入力してジャンプ)");
            // 
            // lblTotalPages
            // 
            lblTotalPages.AutoSize = true;
            lblTotalPages.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            lblTotalPages.Location = new Point(258, 24);
            lblTotalPages.Margin = new Padding(0, 11, 3, 3);
            lblTotalPages.Name = "lblTotalPages";
            lblTotalPages.Size = new Size(42, 30);
            lblTotalPages.TabIndex = 4;
            lblTotalPages.Text = "/ 0";
            lblTotalPages.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // btnNextPage
            // 
            btnNextPage.Font = new Font("Segoe UI Emoji", 19F, FontStyle.Regular);
            btnNextPage.Location = new Point(303, 13);
            btnNextPage.Name = "btnNextPage";
            btnNextPage.Size = new Size(54, 54);
            btnNextPage.TabIndex = 5;
            btnNextPage.Text = "▶️";
            btnNextPage.UseVisualStyleBackColor = true;
            btnNextPage.Click += btnNextPage_Click;
            toolTipMain.SetToolTip(btnNextPage, "次のページ");
            // 
            // btnLastPage
            // 
            btnLastPage.Font = new Font("Segoe UI Emoji", 19F, FontStyle.Regular);
            btnLastPage.Location = new Point(253, 13);
            btnLastPage.Name = "btnLastPage";
            btnLastPage.Size = new Size(54, 54);
            btnLastPage.TabIndex = 4;
            btnLastPage.Text = "⏭️";
            btnLastPage.UseVisualStyleBackColor = true;
            btnLastPage.Click += btnLastPage_Click;
            toolTipMain.SetToolTip(btnLastPage, "最後のページ");
            // 
            // lblTargetRangePrefix
            // 
            lblTargetRangePrefix.AutoSize = true;
            lblTargetRangePrefix.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            lblTargetRangePrefix.ForeColor = Color.FromArgb(30, 58, 138);
            lblTargetRangePrefix.Location = new Point(313, 24);
            lblTargetRangePrefix.Margin = new Padding(6, 13, 2, 3);
            lblTargetRangePrefix.Name = "lblTargetRangePrefix";
            lblTargetRangePrefix.Size = new Size(88, 30);
            lblTargetRangePrefix.TabIndex = 5;
            lblTargetRangePrefix.Text = "対象範囲:";
            lblTargetRangePrefix.TextAlign = ContentAlignment.MiddleRight;
            toolTipMain.SetToolTip(lblTargetRangePrefix, "現在OCR・自動判定の処理対象となっているページ範囲");
            // 
            // numPageStart
            // 
            numPageStart.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            numPageStart.Location = new Point(404, 22);
            numPageStart.Margin = new Padding(3, 10, 3, 3);
            numPageStart.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numPageStart.Name = "numPageStart";
            numPageStart.Size = new Size(58, 35);
            numPageStart.TabIndex = 6;
            numPageStart.TextAlign = HorizontalAlignment.Center;
            numPageStart.Value = new decimal(new int[] { 1, 0, 0, 0 });
            toolTipMain.SetToolTip(numPageStart, "OCR処理の開始ページ");
            // 
            // lblRangeDash
            // 
            lblRangeDash.AutoSize = true;
            lblRangeDash.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblRangeDash.Location = new Point(468, 24);
            lblRangeDash.Margin = new Padding(2, 13, 2, 3);
            lblRangeDash.Name = "lblRangeDash";
            lblRangeDash.Size = new Size(29, 30);
            lblRangeDash.TabIndex = 7;
            lblRangeDash.Text = "〜";
            lblRangeDash.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // numPageEnd
            // 
            numPageEnd.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            numPageEnd.Location = new Point(502, 22);
            numPageEnd.Margin = new Padding(3, 10, 3, 3);
            numPageEnd.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numPageEnd.Name = "numPageEnd";
            numPageEnd.Size = new Size(58, 35);
            numPageEnd.TabIndex = 8;
            numPageEnd.TextAlign = HorizontalAlignment.Center;
            numPageEnd.Value = new decimal(new int[] { 20, 0, 0, 0 });
            toolTipMain.SetToolTip(numPageEnd, "OCR処理の終了ページ (推奨: 20ページ単位)");
            // 
            // lblTargetRangeInfo
            // 
            lblTargetRangeInfo.AutoSize = true;
            lblTargetRangeInfo.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblTargetRangeInfo.ForeColor = Color.FromArgb(71, 85, 105);
            lblTargetRangeInfo.Location = new Point(565, 24);
            lblTargetRangeInfo.Margin = new Padding(2, 14, 6, 3);
            lblTargetRangeInfo.Name = "lblTargetRangeInfo";
            lblTargetRangeInfo.Size = new Size(60, 30);
            lblTargetRangeInfo.TabIndex = 9;
            lblTargetRangeInfo.Text = "/ 0P";
            lblTargetRangeInfo.TextAlign = ContentAlignment.MiddleLeft;
            toolTipMain.SetToolTip(lblTargetRangeInfo, "総ページ数および選択中の対象ページ数");
            // 
            // btnNextBatch20
            // 
            btnNextBatch20.Font = new Font("Segoe UI Emoji", 19F, FontStyle.Regular);
            btnNextBatch20.Location = new Point(598, 13);
            btnNextBatch20.Margin = new Padding(6, 3, 2, 3);
            btnNextBatch20.Name = "btnNextBatch20";
            btnNextBatch20.Size = new Size(54, 54);
            btnNextBatch20.TabIndex = 10;
            btnNextBatch20.Text = "📚";
            btnNextBatch20.UseVisualStyleBackColor = true;
            btnNextBatch20.Click += btnNextBatch20_Click;
            toolTipMain.SetToolTip(btnNextBatch20, "次の20ページを一括範囲設定 (バッチ送り)");
            // 
            // btnZoomOut
            // 
            btnZoomOut.Font = new Font("Segoe UI Emoji", 14F, FontStyle.Bold);
            btnZoomOut.Location = new Point(536, 13);
            btnZoomOut.Name = "btnZoomOut";
            btnZoomOut.Size = new Size(54, 54);
            btnZoomOut.TabIndex = 9;
            btnZoomOut.Text = "➖";
            btnZoomOut.UseVisualStyleBackColor = true;
            toolTipMain.SetToolTip(btnZoomOut, "縮小 (Ctrl+ホイール下)");
            // 
            // cmbZoom
            // 
            cmbZoom.DropDownStyle = ComboBoxStyle.DropDown;
            cmbZoom.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            cmbZoom.FormattingEnabled = true;
            cmbZoom.Location = new Point(596, 21);
            cmbZoom.Margin = new Padding(3, 8, 3, 3);
            cmbZoom.Name = "cmbZoom";
            cmbZoom.Size = new Size(130, 36);
            cmbZoom.TabIndex = 10;
            toolTipMain.SetToolTip(cmbZoom, "ズーム倍率 (50%〜300% / 全体表示)");
            // 
            // btnZoomIn
            // 
            btnZoomIn.Font = new Font("Segoe UI Emoji", 14F, FontStyle.Bold);
            btnZoomIn.Location = new Point(732, 13);
            btnZoomIn.Name = "btnZoomIn";
            btnZoomIn.Size = new Size(54, 54);
            btnZoomIn.TabIndex = 11;
            btnZoomIn.Text = "➕";
            btnZoomIn.UseVisualStyleBackColor = true;
            toolTipMain.SetToolTip(btnZoomIn, "拡大 (Ctrl+ホイール上)");
            // 
            // btnRegionSettings
            // 
            btnRegionSettings.Font = new Font("Segoe UI Emoji", 20F, FontStyle.Regular);
            btnRegionSettings.Location = new Point(313, 13);
            btnRegionSettings.Name = "btnRegionSettings";
            btnRegionSettings.Size = new Size(54, 54);
            btnRegionSettings.TabIndex = 5;
            btnRegionSettings.Text = "📐";
            btnRegionSettings.UseVisualStyleBackColor = true;
            btnRegionSettings.Click += btnRegionSettings_Click;
            toolTipMain.SetToolTip(btnRegionSettings, "領域設定");
            // 
            // btnReorderMode
            // 
            btnReorderMode.Font = new Font("Segoe UI Emoji", 20F, FontStyle.Regular);
            btnReorderMode.Location = new Point(373, 13);
            btnReorderMode.Name = "btnReorderMode";
            btnReorderMode.Size = new Size(54, 54);
            btnReorderMode.TabIndex = 6;
            btnReorderMode.Text = "🔄";
            btnReorderMode.UseVisualStyleBackColor = true;
            btnReorderMode.Click += btnReorderMode_Click;
            toolTipMain.SetToolTip(btnReorderMode, "読み順変更モード（領域をクリックした順に番号を割り当て）");
            // 
            // btnAutoLayout
            // 
            btnAutoLayout.Font = new Font("Segoe UI Emoji", 20F, FontStyle.Regular);
            btnAutoLayout.Location = new Point(433, 13);
            btnAutoLayout.Name = "btnAutoLayout";
            btnAutoLayout.Size = new Size(54, 54);
            btnAutoLayout.TabIndex = 7;
            btnAutoLayout.Text = "⚡";
            btnAutoLayout.UseVisualStyleBackColor = true;
            toolTipMain.SetToolTip(btnAutoLayout, "領域自動判定");
            // 
            // btnStartOcr
            // 
            btnStartOcr.Font = new Font("Segoe UI Emoji", 20F, FontStyle.Regular);
            btnStartOcr.Location = new Point(493, 13);
            btnStartOcr.Name = "btnStartOcr";
            btnStartOcr.Size = new Size(54, 54);
            btnStartOcr.TabIndex = 8;
            btnStartOcr.Text = "🔍";
            btnStartOcr.UseVisualStyleBackColor = true;
            btnStartOcr.Click += btnStartOcr_Click;
            toolTipMain.SetToolTip(btnStartOcr, "OCR開始");
            // 
            // btnAddHeading
            // 
            btnAddHeading.Font = new Font("Segoe UI Emoji", 20F, FontStyle.Regular);
            btnAddHeading.Location = new Point(553, 13);
            btnAddHeading.Name = "btnAddHeading";
            btnAddHeading.Size = new Size(54, 54);
            btnAddHeading.TabIndex = 9;
            btnAddHeading.Text = "🔖";
            btnAddHeading.UseVisualStyleBackColor = true;
            btnAddHeading.Click += btnAddHeading_Click;
            toolTipMain.SetToolTip(btnAddHeading, "見出し設定 (本文選択範囲を見出しに設定)");
            // 
            // btnAddFootnote
            // 
            btnAddFootnote.Font = new Font("Segoe UI Emoji", 20F, FontStyle.Regular);
            btnAddFootnote.Location = new Point(613, 13);
            btnAddFootnote.Name = "btnAddFootnote";
            btnAddFootnote.Size = new Size(54, 54);
            btnAddFootnote.TabIndex = 10;
            btnAddFootnote.Text = "📑";
            btnAddFootnote.UseVisualStyleBackColor = true;
            btnAddFootnote.Click += btnAddFootnote_Click;
            toolTipMain.SetToolTip(btnAddFootnote, "注釈文設定 (本文選択範囲を注釈文に設定＆番号リンク)");
            // 
            // btnAddAnnotationNumber
            // 
            btnAddAnnotationNumber.Font = new Font("Segoe UI Emoji", 20F, FontStyle.Regular);
            btnAddAnnotationNumber.Location = new Point(673, 13);
            btnAddAnnotationNumber.Name = "btnAddAnnotationNumber";
            btnAddAnnotationNumber.Size = new Size(54, 54);
            btnAddAnnotationNumber.TabIndex = 11;
            btnAddAnnotationNumber.Text = "🔢";
            btnAddAnnotationNumber.UseVisualStyleBackColor = true;
            btnAddAnnotationNumber.Click += btnAddAnnotationNumber_Click;
            toolTipMain.SetToolTip(btnAddAnnotationNumber, "注釈番号挿入");
            // 
            // btnExportWord
            // 
            btnExportWord.Font = new Font("Segoe UI", 21F, FontStyle.Bold);
            btnExportWord.ForeColor = Color.FromArgb(43, 87, 154);
            btnExportWord.Location = new Point(733, 13);
            btnExportWord.Name = "btnExportWord";
            btnExportWord.Size = new Size(54, 54);
            btnExportWord.TabIndex = 12;
            btnExportWord.Text = "W";
            btnExportWord.UseVisualStyleBackColor = true;
            toolTipMain.SetToolTip(btnExportWord, "Word出力 (.docx)");
            // 
            // btnSearchBatch
            // 
            btnSearchBatch.Font = new Font("Segoe UI Emoji", 20F, FontStyle.Regular);
            btnSearchBatch.Location = new Point(793, 13);
            btnSearchBatch.Name = "btnSearchBatch";
            btnSearchBatch.Size = new Size(54, 54);
            btnSearchBatch.TabIndex = 13;
            btnSearchBatch.Text = "🔍";
            btnSearchBatch.UseVisualStyleBackColor = true;
            btnSearchBatch.Click += btnSearchBatch_Click;
            toolTipMain.SetToolTip(btnSearchBatch, "バッチ全体を検索 (Ctrl+F)");
            // 
            // btnOptions
            // 
            btnOptions.Font = new Font("Segoe UI Emoji", 20F, FontStyle.Regular);
            btnOptions.Location = new Point(853, 13);
            btnOptions.Name = "btnOptions";
            btnOptions.Size = new Size(54, 54);
            btnOptions.TabIndex = 14;
            btnOptions.Text = "⚙️";
            btnOptions.UseVisualStyleBackColor = true;
            btnOptions.Click += btnOptions_Click;
            toolTipMain.SetToolTip(btnOptions, "オプション設定");
            // 
            // lblOrientationBadge
            // 
            lblOrientationBadge.Cursor = Cursors.Hand;
            lblOrientationBadge.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblOrientationBadge.Location = new Point(853, 13);
            lblOrientationBadge.Name = "lblOrientationBadge";
            lblOrientationBadge.Size = new Size(120, 54);
            lblOrientationBadge.TabIndex = 14;
            lblOrientationBadge.Text = "↕ 縦書き優先";
            lblOrientationBadge.TextAlign = ContentAlignment.MiddleCenter;
            toolTipMain.SetToolTip(lblOrientationBadge, "クリックして組方向を変更");
            // 
            // lblDocTypeBadge
            // 
            lblDocTypeBadge.Cursor = Cursors.Hand;
            lblDocTypeBadge.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblDocTypeBadge.Location = new Point(979, 13);
            lblDocTypeBadge.Name = "lblDocTypeBadge";
            lblDocTypeBadge.Size = new Size(125, 54);
            lblDocTypeBadge.TabIndex = 15;
            lblDocTypeBadge.Text = "🗾 和書(日本語)";
            lblDocTypeBadge.TextAlign = ContentAlignment.MiddleCenter;
            toolTipMain.SetToolTip(lblDocTypeBadge, "クリックして書籍種別を変更");
            // 
            // lblDeckBadge
            // 
            lblDeckBadge.Cursor = Cursors.Hand;
            lblDeckBadge.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblDeckBadge.Location = new Point(1110, 13);
            lblDeckBadge.Name = "lblDeckBadge";
            lblDeckBadge.Size = new Size(110, 54);
            lblDeckBadge.TabIndex = 16;
            lblDeckBadge.Text = "📑 2段組";
            lblDeckBadge.TextAlign = ContentAlignment.MiddleCenter;
            toolTipMain.SetToolTip(lblDeckBadge, "クリックして段組（1段/2段/3段/自動）を変更");
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1600, 752);
            Controls.Add(panelToolbar);
            Controls.Add(tableLayoutPanel1);
            Controls.Add(pnlRegionSettings);
            Name = "Form1";
            Text = "OCR Translator";
            tableLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            panelToolbar.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)numPageStart).EndInit();
            ((System.ComponentModel.ISupportInitialize)numPageEnd).EndInit();
            ((System.ComponentModel.ISupportInitialize)numHeight).EndInit();
            ((System.ComponentModel.ISupportInitialize)numWidth).EndInit();
            ((System.ComponentModel.ISupportInitialize)numY).EndInit();
            ((System.ComponentModel.ISupportInitialize)numX).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel1;
        private Panel pnlCanvasContainer;
        private PictureBox pictureBox1;
        private RichTextBox richTextBox1;
        private FlowLayoutPanel panelToolbar;
        private Button btnOpenPdf;
        private Button btnClosePdf;
        private Button btnFirstPage;
        private Button btnPrevPage;
        private NumericUpDown numCurrentPage;
        private Label lblTotalPages;
        private Button btnNextPage;
        private Button btnLastPage;
        private Label lblTargetRangePrefix;
        private NumericUpDown numPageStart;
        private Label lblRangeDash;
        private NumericUpDown numPageEnd;
        private Label lblTargetRangeInfo;
        private Button btnNextBatch20;
        private Button btnZoomOut;
        private ComboBox cmbZoom;
        private Button btnZoomIn;
        private Button btnRegionSettings;
        private Button btnReorderMode;
        private Button btnAutoLayout;
        private Button btnStartOcr;
        private Button btnAddHeading;
        private Button btnAddFootnote;
        private Button btnAddAnnotationNumber;
        private Button btnExportWord;
        private Button btnSearchBatch;
        private Button btnOptions;
        private Label lblOrientationBadge;
        private Label lblDocTypeBadge;
        private Label lblDeckBadge;
        private ToolTip toolTipMain;

        private Panel pnlRegionSettings;
        private NumericUpDown numX;
        private NumericUpDown numWidth;
        private NumericUpDown numY;
        private Button btnSaveLayout;
        private Button btnCopyRegionsToPages;
        private NumericUpDown numHeight;
        private Button btnDeleteRegion;
        private ListBox lstRegions;
        private Button btnReOcrRegion;
    }
}
