using System;
using System.Drawing;
using System.Windows.Forms;
using OCR_Translator.Models;

namespace OCR_Translator.Forms
{
    public enum RegionOcrAction
    {
        None,
        CopyToClipboard,
        AppendToTab,
        ReplaceSelection
    }

    public class RegionOcrResultForm : Form
    {
        public RegionOcrAction SelectedAction { get; private set; } = RegionOcrAction.None;
        public string ResultText => txtResult != null ? txtResult.Text : "";

        private readonly Image? _croppedImage;
        private readonly string _regionName;
        private readonly string _regionType;
        private readonly int _pageNumber;

        private PictureBox picPreview = null!;
        private TextBox txtResult = null!;
        private Button btnCopy = null!;
        private Button btnAppend = null!;
        private Button btnReplace = null!;
        private Button btnCancel = null!;
        private Label lblStatus = null!;

        public RegionOcrResultForm(
            Image? croppedImage,
            string regionName,
            string regionType,
            int pageNumber,
            string initialText)
        {
            _croppedImage = croppedImage;
            _regionName = regionName;
            _regionType = regionType;
            _pageNumber = pageNumber;

            InitializeComponents(initialText);
        }

        private void InitializeComponents(string initialText)
        {
            string typeDisplay = _regionType switch
            {
                "body" => "本文",
                "heading" => "見出し",
                "footnote" => "注釈文",
                "image" => "図",
                _ => "領域"
            };

            Text = $"選択領域の再OCR結果 - P.{_pageNumber} 【{_regionName}】 ({typeDisplay})";
            Size = new Size(860, 560);
            MinimumSize = new Size(680, 420);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            BackColor = Color.FromArgb(248, 249, 250);

            var pnlMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(12)
            };
            pnlMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            pnlMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            pnlMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));

            // 1. ヘッダー
            var pnlHeader = new Panel { Dock = DockStyle.Fill };
            var lblTitle = new Label
            {
                Text = $"🎯 P.{_pageNumber} 【{_regionName}】 ({typeDisplay}) のOCR文字認識結果です。下のボタンから反映方法を選択してください。",
                Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 43, 73),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlHeader.Controls.Add(lblTitle);
            pnlMain.Controls.Add(pnlHeader, 0, 0);

            // 2. 中央コンテンツ (左右分割: 画像とテキスト)
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 320,
                SplitterWidth = 6,
                BorderStyle = BorderStyle.FixedSingle
            };

            // 左側: 画像プレビュー
            var pnlLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var lblImgHeader = new Label
            {
                Text = "【切り出し領域画像】",
                Dock = DockStyle.Top,
                Height = 26,
                BackColor = Color.FromArgb(240, 242, 245),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0),
                Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold)
            };
            picPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(245, 245, 245),
                Image = _croppedImage
            };
            pnlLeft.Controls.Add(picPreview);
            pnlLeft.Controls.Add(lblImgHeader);
            splitContainer.Panel1.Controls.Add(pnlLeft);

            // 右側: 認識結果テキスト
            var pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var lblTxtHeader = new Label
            {
                Text = "【認識結果テキスト（直接修正可能）】",
                Dock = DockStyle.Top,
                Height = 26,
                BackColor = Color.FromArgb(240, 242, 245),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0),
                Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold)
            };
            txtResult = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Yu Gothic UI", 11f, FontStyle.Regular),
                Text = initialText,
                BorderStyle = BorderStyle.None
            };
            pnlRight.Controls.Add(txtResult);
            pnlRight.Controls.Add(lblTxtHeader);
            splitContainer.Panel2.Controls.Add(pnlRight);

            pnlMain.Controls.Add(splitContainer, 0, 1);

            // 3. フッターボタン部
            var pnlFooter = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 8)
            };

            lblStatus = new Label
            {
                Dock = DockStyle.Left,
                Width = 240,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.DarkGreen,
                Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
                Text = ""
            };

            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            btnCancel = new Button
            {
                Text = "閉じる",
                Size = new Size(85, 38),
                Margin = new Padding(4, 0, 0, 0),
                FlatStyle = FlatStyle.System
            };
            btnCancel.Click += (s, e) =>
            {
                SelectedAction = RegionOcrAction.None;
                DialogResult = DialogResult.Cancel;
                Close();
            };

            btnReplace = new Button
            {
                Text = "🔄 選択箇所を置換",
                Size = new Size(135, 38),
                Margin = new Padding(4, 0, 0, 0),
                BackColor = Color.FromArgb(250, 245, 230),
                FlatStyle = FlatStyle.System
            };
            btnReplace.Click += (s, e) =>
            {
                SelectedAction = RegionOcrAction.ReplaceSelection;
                DialogResult = DialogResult.OK;
                Close();
            };

            btnAppend = new Button
            {
                Text = $"📝 {typeDisplay}末尾に追記",
                Size = new Size(165, 38),
                Margin = new Padding(4, 0, 0, 0),
                BackColor = Color.FromArgb(225, 245, 230),
                Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.System
            };
            btnAppend.Click += (s, e) =>
            {
                SelectedAction = RegionOcrAction.AppendToTab;
                DialogResult = DialogResult.OK;
                Close();
            };

            btnCopy = new Button
            {
                Text = "📋 コピー",
                Size = new Size(100, 38),
                Margin = new Padding(4, 0, 0, 0),
                BackColor = Color.FromArgb(235, 240, 250),
                FlatStyle = FlatStyle.System
            };
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(txtResult.Text))
                {
                    Clipboard.SetText(txtResult.Text);
                    lblStatus.Text = "✔ クリップボードへコピー済";
                }
            };

            pnlButtons.Controls.Add(btnCancel);
            pnlButtons.Controls.Add(btnReplace);
            pnlButtons.Controls.Add(btnAppend);
            pnlButtons.Controls.Add(btnCopy);

            pnlFooter.Controls.Add(pnlButtons);
            pnlFooter.Controls.Add(lblStatus);
            pnlMain.Controls.Add(pnlFooter, 0, 2);

            Controls.Add(pnlMain);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _croppedImage?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
