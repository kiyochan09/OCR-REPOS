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
            btnExportWord = new Button();
            btnStartOcr = new Button();
            btnNextPage = new Button();
            btnPrevPage = new Button();
            btnOpenPdf = new Button();
            pnlRegionSettings = new Panel();
            btnUpdateRegion = new Button();
            lstRegions = new ListBox();
            btnDeleteRegion = new Button();
            btnApplyTemplate = new Button();
            btnSaveLayout = new Button();
            btnAddRegion = new Button();
            numHeight = new NumericUpDown();
            numWidth = new NumericUpDown();
            numY = new NumericUpDown();
            numX = new NumericUpDown();
            cmbRegionType = new ComboBox();
            btnTestCrop = new Button();
            btnAutoLayout = new Button();
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
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Controls.Add(richTextBox1, 1, 0);
            tableLayoutPanel1.Controls.Add(pictureBox1, 0, 0);
            tableLayoutPanel1.Location = new Point(0, 74);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 1;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tableLayoutPanel1.Size = new Size(1050, 678);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // richTextBox1
            // 
            richTextBox1.Dock = DockStyle.Fill;
            richTextBox1.Location = new Point(528, 3);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(519, 672);
            richTextBox1.TabIndex = 1;
            richTextBox1.Text = "";
            // 
            // pictureBox1
            // 
            pictureBox1.Location = new Point(3, 3);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(519, 669);
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
            panelToolbar.Controls.Add(btnAutoLayout);
            panelToolbar.Controls.Add(btnTestCrop);
            panelToolbar.Controls.Add(btnExportWord);
            panelToolbar.Controls.Add(btnStartOcr);
            panelToolbar.Controls.Add(btnNextPage);
            panelToolbar.Controls.Add(btnPrevPage);
            panelToolbar.Controls.Add(btnOpenPdf);
            panelToolbar.Dock = DockStyle.Top;
            panelToolbar.Location = new Point(0, 0);
            panelToolbar.Name = "panelToolbar";
            panelToolbar.Size = new Size(1379, 78);
            panelToolbar.TabIndex = 1;
            // 
            // btnExportWord
            // 
            btnExportWord.Location = new Point(780, 25);
            btnExportWord.Name = "btnExportWord";
            btnExportWord.Size = new Size(112, 34);
            btnExportWord.TabIndex = 4;
            btnExportWord.Text = "Word出力";
            btnExportWord.UseVisualStyleBackColor = true;
            // 
            // btnStartOcr
            // 
            btnStartOcr.Location = new Point(589, 25);
            btnStartOcr.Name = "btnStartOcr";
            btnStartOcr.Size = new Size(112, 34);
            btnStartOcr.TabIndex = 3;
            btnStartOcr.Text = "OCR開始";
            btnStartOcr.UseVisualStyleBackColor = true;
            btnStartOcr.Click += btnStartOcr_Click;
            // 
            // btnNextPage
            // 
            btnNextPage.Location = new Point(403, 25);
            btnNextPage.Name = "btnNextPage";
            btnNextPage.Size = new Size(112, 34);
            btnNextPage.TabIndex = 2;
            btnNextPage.Text = "次のページ";
            btnNextPage.UseVisualStyleBackColor = true;
            // 
            // btnPrevPage
            // 
            btnPrevPage.Location = new Point(229, 25);
            btnPrevPage.Name = "btnPrevPage";
            btnPrevPage.Size = new Size(112, 34);
            btnPrevPage.TabIndex = 1;
            btnPrevPage.Text = " 前のページ";
            btnPrevPage.UseVisualStyleBackColor = true;
            // 
            // btnOpenPdf
            // 
            btnOpenPdf.Location = new Point(59, 25);
            btnOpenPdf.Name = "btnOpenPdf";
            btnOpenPdf.Size = new Size(112, 34);
            btnOpenPdf.TabIndex = 0;
            btnOpenPdf.Text = "PDFを開く";
            btnOpenPdf.UseVisualStyleBackColor = true;
            btnOpenPdf.Click += btnOpenPdf_Click;
            // 
            // pnlRegionSettings
            // 
            pnlRegionSettings.AccessibleName = "pnlRegionSettings";
            pnlRegionSettings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pnlRegionSettings.Controls.Add(btnUpdateRegion);
            pnlRegionSettings.Controls.Add(lstRegions);
            pnlRegionSettings.Controls.Add(btnDeleteRegion);
            pnlRegionSettings.Controls.Add(btnApplyTemplate);
            pnlRegionSettings.Controls.Add(btnSaveLayout);
            pnlRegionSettings.Controls.Add(btnAddRegion);
            pnlRegionSettings.Controls.Add(numHeight);
            pnlRegionSettings.Controls.Add(numWidth);
            pnlRegionSettings.Controls.Add(numY);
            pnlRegionSettings.Controls.Add(numX);
            pnlRegionSettings.Controls.Add(cmbRegionType);
            pnlRegionSettings.Location = new Point(1060, 74);
            pnlRegionSettings.Name = "pnlRegionSettings";
            pnlRegionSettings.Size = new Size(300, 678);
            pnlRegionSettings.TabIndex = 5;
            // 
            // btnUpdateRegion
            // 
            btnUpdateRegion.Location = new Point(182, 576);
            btnUpdateRegion.Name = "btnUpdateRegion";
            btnUpdateRegion.Size = new Size(99, 42);
            btnUpdateRegion.TabIndex = 10;
            btnUpdateRegion.Text = "領域更新";
            btnUpdateRegion.UseVisualStyleBackColor = true;
            btnUpdateRegion.Click += btnUpdateRegion_Click;
            // 
            // lstRegions
            // 
            lstRegions.FormattingEnabled = true;
            lstRegions.Location = new Point(72, 116);
            lstRegions.Name = "lstRegions";
            lstRegions.Size = new Size(180, 129);
            lstRegions.TabIndex = 9;
            lstRegions.SelectedIndexChanged += lstRegions_SelectedIndexChanged;
            // 
            // btnDeleteRegion
            // 
            btnDeleteRegion.Location = new Point(166, 268);
            btnDeleteRegion.Name = "btnDeleteRegion";
            btnDeleteRegion.RightToLeft = RightToLeft.Yes;
            btnDeleteRegion.Size = new Size(112, 34);
            btnDeleteRegion.TabIndex = 8;
            btnDeleteRegion.Text = " 領域削除";
            btnDeleteRegion.UseVisualStyleBackColor = true;
            btnDeleteRegion.Click += btnDeleteRegion_Click;
            // 
            // btnApplyTemplate
            // 
            btnApplyTemplate.Location = new Point(15, 574);
            btnApplyTemplate.Name = "btnApplyTemplate";
            btnApplyTemplate.Size = new Size(137, 41);
            btnApplyTemplate.TabIndex = 7;
            btnApplyTemplate.Text = "全ページに適用";
            btnApplyTemplate.UseVisualStyleBackColor = true;
            // 
            // btnSaveLayout
            // 
            btnSaveLayout.Location = new Point(71, 632);
            btnSaveLayout.Name = "btnSaveLayout";
            btnSaveLayout.Size = new Size(167, 34);
            btnSaveLayout.TabIndex = 6;
            btnSaveLayout.Text = " 設定を保存";
            btnSaveLayout.UseVisualStyleBackColor = true;
            btnSaveLayout.Click += btnSaveLayout_Click;
            // 
            // btnAddRegion
            // 
            btnAddRegion.Location = new Point(29, 264);
            btnAddRegion.Name = "btnAddRegion";
            btnAddRegion.Size = new Size(115, 43);
            btnAddRegion.TabIndex = 5;
            btnAddRegion.Text = "領域追加";
            btnAddRegion.UseVisualStyleBackColor = true;
            btnAddRegion.Click += btnAddRegion_Click;
            // 
            // numHeight
            // 
            numHeight.Location = new Point(65, 515);
            numHeight.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            numHeight.Name = "numHeight";
            numHeight.Size = new Size(180, 31);
            numHeight.TabIndex = 4;
            // 
            // numWidth
            // 
            numWidth.Location = new Point(65, 460);
            numWidth.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            numWidth.Name = "numWidth";
            numWidth.Size = new Size(180, 31);
            numWidth.TabIndex = 3;
            // 
            // numY
            // 
            numY.Location = new Point(62, 394);
            numY.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            numY.Name = "numY";
            numY.Size = new Size(180, 31);
            numY.TabIndex = 2;
            // 
            // numX
            // 
            numX.Location = new Point(58, 334);
            numX.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            numX.Name = "numX";
            numX.Size = new Size(180, 31);
            numX.TabIndex = 1;
            // 
            // cmbRegionType
            // 
            cmbRegionType.FormattingEnabled = true;
            cmbRegionType.Items.AddRange(new object[] { "本文", "", "見出し", "", "ヘッダー", "", "フッター", "", "脚注", "", "表", "", "画像", "", "地図", "", "OCRしない" });
            cmbRegionType.Location = new Point(62, 41);
            cmbRegionType.Name = "cmbRegionType";
            cmbRegionType.Size = new Size(182, 33);
            cmbRegionType.TabIndex = 0;
            cmbRegionType.Text = "本文";
            // 
            // btnTestCrop
            // 
            btnTestCrop.Location = new Point(1226, 25);
            btnTestCrop.Name = "btnTestCrop";
            btnTestCrop.Size = new Size(132, 34);
            btnTestCrop.TabIndex = 5;
            btnTestCrop.Text = "選択領域テスト";
            btnTestCrop.UseVisualStyleBackColor = true;
            btnTestCrop.Click += btnTestCrop_Click;
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
        private Button btnStartOcr;
        private Button btnNextPage;
        private Button btnPrevPage;
        private Panel pnlRegionSettings;
        private ComboBox cmbRegionType;
        private NumericUpDown numX;
        private NumericUpDown numWidth;
        private NumericUpDown numY;
        private Button btnSaveLayout;
        private Button btnAddRegion;
        private NumericUpDown numHeight;
        private Button btnDeleteRegion;
        private Button btnApplyTemplate;
        private ListBox lstRegions;
        private Button btnUpdateRegion;
        private Button btnTestCrop;

        private Button btnAutoLayout;
    }
}
