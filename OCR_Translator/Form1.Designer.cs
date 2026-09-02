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
            tableLayoutPanel1 = new TableLayoutPanel();
            richTextBox1 = new RichTextBox();
            pictureBox1 = new PictureBox();
            panelToolbar = new Panel();
            lblDocTypeBadge = new Label();
            lblOrientationBadge = new Label();
            btnOptions = new Button();
            btnAutoLayout = new Button();
            btnAddAnnotationNumber = new Button();
            btnExportWord = new Button();
            btnStartOcr = new Button();
            btnRegionSettings = new Button();
            btnNextPage = new Button();
            btnPrevPage = new Button();
            btnOpenPdf = new Button();
            pnlRegionSettings = new Panel();
            lstRegions = new ListBox();
            btnDeleteRegion = new Button();
            numHeight = new NumericUpDown();
            numWidth = new NumericUpDown();
            numY = new NumericUpDown();
            numX = new NumericUpDown();
            btnSaveLayout = new Button();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            panelToolbar.SuspendLayout();
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
            pnlRegionSettings.Location = new Point(0, 78);
            pnlRegionSettings.Name = "pnlRegionSettings";
            pnlRegionSettings.Size = new Size(280, 674);
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
            btnDeleteRegion.Text = "－ 領域削除";
            btnDeleteRegion.UseVisualStyleBackColor = true;
            btnDeleteRegion.Click += btnDeleteRegion_Click;
            // 
            // numX
            // 
            numX.Location = new Point(15, 185);
            numX.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            numX.Name = "numX";
            numX.Size = new Size(118, 31);
            numX.TabIndex = 1;
            // 
            // numY
            // 
            numY.Location = new Point(142, 185);
            numY.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            numY.Name = "numY";
            numY.Size = new Size(118, 31);
            numY.TabIndex = 2;
            // 
            // numWidth
            // 
            numWidth.Location = new Point(15, 225);
            numWidth.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            numWidth.Name = "numWidth";
            numWidth.Size = new Size(118, 31);
            numWidth.TabIndex = 3;
            // 
            // numHeight
            // 
            numHeight.Location = new Point(142, 225);
            numHeight.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            numHeight.Name = "numHeight";
            numHeight.Size = new Size(118, 31);
            numHeight.TabIndex = 4;
            // 
            // btnSaveLayout
            // 
            btnSaveLayout.Location = new Point(15, 470);
            btnSaveLayout.Name = "btnSaveLayout";
            btnSaveLayout.Size = new Size(245, 36);
            btnSaveLayout.TabIndex = 6;
            btnSaveLayout.Text = "💾 設定を保存";
            btnSaveLayout.UseVisualStyleBackColor = true;
            btnSaveLayout.Click += btnSaveLayout_Click;
            // 
            // tableLayoutPanel1（中央〜右側）
            // 
            tableLayoutPanel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Controls.Add(richTextBox1, 1, 0);
            tableLayoutPanel1.Controls.Add(pictureBox1, 0, 0);
            tableLayoutPanel1.Location = new Point(280, 78);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 1;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Size = new Size(1099, 674);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // richTextBox1
            // 
            richTextBox1.Dock = DockStyle.Fill;
            richTextBox1.Location = new Point(552, 3);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(544, 668);
            richTextBox1.TabIndex = 1;
            richTextBox1.Text = "";
            // 
            // pictureBox1
            // 
            pictureBox1.Dock = DockStyle.Fill;
            pictureBox1.Location = new Point(3, 3);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(543, 668);
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
            panelToolbar.Controls.Add(lblDocTypeBadge);
            panelToolbar.Controls.Add(lblOrientationBadge);
            panelToolbar.Controls.Add(btnOptions);
            panelToolbar.Controls.Add(btnAutoLayout);
            panelToolbar.Controls.Add(btnAddAnnotationNumber);
            panelToolbar.Controls.Add(btnExportWord);
            panelToolbar.Controls.Add(btnStartOcr);
            panelToolbar.Controls.Add(btnRegionSettings);
            panelToolbar.Controls.Add(btnNextPage);
            panelToolbar.Controls.Add(btnPrevPage);
            panelToolbar.Controls.Add(btnOpenPdf);
            panelToolbar.Dock = DockStyle.Top;
            panelToolbar.Location = new Point(0, 0);
            panelToolbar.Name = "panelToolbar";
            panelToolbar.Size = new Size(1379, 78);
            panelToolbar.TabIndex = 1;
            // 
            // lblDocTypeBadge
            // 
            lblDocTypeBadge.Cursor = Cursors.Hand;
            lblDocTypeBadge.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblDocTypeBadge.Location = new Point(1095, 22);
            lblDocTypeBadge.Name = "lblDocTypeBadge";
            lblDocTypeBadge.Size = new Size(130, 34);
            lblDocTypeBadge.TabIndex = 9;
            lblDocTypeBadge.Text = "🗾 和書(日本語)";
            lblDocTypeBadge.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblOrientationBadge
            // 
            lblOrientationBadge.Cursor = Cursors.Hand;
            lblOrientationBadge.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblOrientationBadge.Location = new Point(965, 22);
            lblOrientationBadge.Name = "lblOrientationBadge";
            lblOrientationBadge.Size = new Size(125, 34);
            lblOrientationBadge.TabIndex = 8;
            lblOrientationBadge.Text = "↕ 縦書き優先";
            lblOrientationBadge.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // btnOptions
            // 
            btnOptions.Location = new Point(845, 22);
            btnOptions.Name = "btnOptions";
            btnOptions.Size = new Size(110, 34);
            btnOptions.TabIndex = 7;
            btnOptions.Text = "オプション ⚙";
            btnOptions.UseVisualStyleBackColor = true;
            btnOptions.Click += btnOptions_Click;
            // 
            // btnAutoLayout
            // 
            btnAutoLayout.Location = new Point(720, 22);
            btnAutoLayout.Name = "btnAutoLayout";
            btnAutoLayout.Size = new Size(120, 34);
            btnAutoLayout.TabIndex = 6;
            btnAutoLayout.Text = "領域自動判定";
            btnAutoLayout.UseVisualStyleBackColor = true;
            // 
            // btnAddAnnotationNumber
            // 
            btnAddAnnotationNumber.Location = new Point(620, 22);
            btnAddAnnotationNumber.Name = "btnAddAnnotationNumber";
            btnAddAnnotationNumber.Size = new Size(95, 34);
            btnAddAnnotationNumber.TabIndex = 6;
            btnAddAnnotationNumber.Text = "注釈番号";
            btnAddAnnotationNumber.UseVisualStyleBackColor = true;
            btnAddAnnotationNumber.Click += btnAddAnnotationNumber_Click;
            // 
            // btnExportWord
            // 
            btnExportWord.Location = new Point(520, 22);
            btnExportWord.Name = "btnExportWord";
            btnExportWord.Size = new Size(95, 34);
            btnExportWord.TabIndex = 5;
            btnExportWord.Text = "Word出力";
            btnExportWord.UseVisualStyleBackColor = true;
            // 
            // btnStartOcr
            // 
            btnStartOcr.Location = new Point(420, 22);
            btnStartOcr.Name = "btnStartOcr";
            btnStartOcr.Size = new Size(95, 34);
            btnStartOcr.TabIndex = 4;
            btnStartOcr.Text = "OCR開始";
            btnStartOcr.UseVisualStyleBackColor = true;
            btnStartOcr.Click += btnStartOcr_Click;
            // 
            // btnRegionSettings
            // 
            btnRegionSettings.Location = new Point(320, 22);
            btnRegionSettings.Name = "btnRegionSettings";
            btnRegionSettings.Size = new Size(95, 34);
            btnRegionSettings.TabIndex = 3;
            btnRegionSettings.Text = "領域設定";
            btnRegionSettings.UseVisualStyleBackColor = true;
            btnRegionSettings.Click += btnRegionSettings_Click;
            // 
            // btnNextPage
            // 
            btnNextPage.Location = new Point(220, 22);
            btnNextPage.Name = "btnNextPage";
            btnNextPage.Size = new Size(95, 34);
            btnNextPage.TabIndex = 2;
            btnNextPage.Text = "次のページ";
            btnNextPage.UseVisualStyleBackColor = true;
            btnNextPage.Click += btnNextPage_Click;
            // 
            // btnPrevPage
            // 
            btnPrevPage.Location = new Point(120, 22);
            btnPrevPage.Name = "btnPrevPage";
            btnPrevPage.Size = new Size(95, 34);
            btnPrevPage.TabIndex = 1;
            btnPrevPage.Text = "前のページ";
            btnPrevPage.UseVisualStyleBackColor = true;
            btnPrevPage.Click += btnPrevPage_Click;
            // 
            // btnOpenPdf
            // 
            btnOpenPdf.Location = new Point(15, 22);
            btnOpenPdf.Name = "btnOpenPdf";
            btnOpenPdf.Size = new Size(100, 34);
            btnOpenPdf.TabIndex = 0;
            btnOpenPdf.Text = "PDFを開く";
            btnOpenPdf.UseVisualStyleBackColor = true;
            btnOpenPdf.Click += btnOpenPdf_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1379, 752);
            Controls.Add(panelToolbar);
            Controls.Add(tableLayoutPanel1);
            Controls.Add(pnlRegionSettings);
            Name = "Form1";
            Text = "Form1";
            tableLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            panelToolbar.ResumeLayout(false);
            pnlRegionSettings.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)numHeight).EndInit();
            ((System.ComponentModel.ISupportInitialize)numWidth).EndInit();
            ((System.ComponentModel.ISupportInitialize)numY).EndInit();
            ((System.ComponentModel.ISupportInitialize)numX).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutPanel1;
        private PictureBox pictureBox1;
        private RichTextBox richTextBox1;
        private Panel panelToolbar;
        private Button btnOpenPdf;
        private Button btnExportWord;
        private Button btnAddAnnotationNumber;
        private Button btnStartOcr;
        private Button btnNextPage;
        private Button btnPrevPage;
        private Panel pnlRegionSettings;
        private NumericUpDown numX;
        private NumericUpDown numWidth;
        private NumericUpDown numY;
        private Button btnSaveLayout;
        private NumericUpDown numHeight;
        private Button btnDeleteRegion;
        private ListBox lstRegions;

        private Button btnAutoLayout;
        private Button btnRegionSettings;
        private Button btnOptions;
        private Label lblOrientationBadge;
        private Label lblDocTypeBadge;

    }
}
