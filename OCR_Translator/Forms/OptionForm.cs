using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using OCR_Translator.Models;

namespace OCR_Translator.Forms
{
    public class OptionForm : Form
    {
        private readonly AppSettings currentSettings;
        public AppSettings ResultSettings { get; private set; }

        // フォント設定
        private ComboBox cmbFontFamily = null!;
        private ComboBox cmbFontSize = null!;
        private CheckBox chkBold = null!;
        private Button btnChooseFontDialog = null!;
        private TextBox txtFontPreview = null!;

        // 組方向設定
        private RadioButton rdoOrientationAuto = null!;
        private RadioButton rdoOrientationVertical = null!;
        private RadioButton rdoOrientationHorizontal = null!;

        // 書籍種別設定
        private RadioButton rdoDocTypeJapanese = null!;
        private RadioButton rdoDocTypeWestern = null!;
        private Label lblWesternDesc = null!;

        // 操作ボタン
        private Button btnOk = null!;
        private Button btnCancel = null!;

        public OptionForm(AppSettings settings)
        {
            currentSettings = settings.Clone();
            ResultSettings = settings.Clone();

            InitializeComponents();
            LoadCurrentSettingsToUi();
            UpdatePreview();
        }

        private void InitializeComponents()
        {
            this.Text = "オプション設定";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(540, 610);
            this.Font = new Font("Yu Gothic UI", 9.5f, FontStyle.Regular);

            int currentY = 15;

            // =========================================================
            // 1. フォント・サイズ設定 GroupBox
            // =========================================================
            var grpFont = new GroupBox
            {
                Text = "フォント・サイズ設定（OCR結果表示用）",
                Location = new Point(15, currentY),
                Size = new Size(495, 185),
                ForeColor = Color.DarkSlateBlue
            };

            var lblFont = new Label
            {
                Text = "フォント名:",
                Location = new Point(15, 30),
                AutoSize = true,
                ForeColor = Color.Black
            };

            cmbFontFamily = new ComboBox
            {
                Location = new Point(95, 27),
                Size = new Size(180, 26),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // よく使われる日本語・欧文フォントを先頭に配置
            string[] popularFonts = {
                "Yu Gothic UI", "游ゴシック", "メイリオ", "ＭＳ ゴシック", "ＭＳ 明朝",
                "BIZ UDPGothic", "BIZ UDPMincho", "Segoe UI", "Arial", "Times New Roman", "Consolas"
            };

            using (var installedFonts = new InstalledFontCollection())
            {
                var installedNames = installedFonts.Families.Select(f => f.Name).ToHashSet();
                foreach (var fontName in popularFonts)
                {
                    if (installedNames.Contains(fontName))
                        cmbFontFamily.Items.Add(fontName);
                }

                // 区切り線代わりにその他すべてのフォントを追加
                foreach (var family in installedFonts.Families.OrderBy(f => f.Name))
                {
                    if (!cmbFontFamily.Items.Contains(family.Name))
                        cmbFontFamily.Items.Add(family.Name);
                }
            }

            var lblSize = new Label
            {
                Text = "サイズ:",
                Location = new Point(290, 30),
                AutoSize = true,
                ForeColor = Color.Black
            };

            cmbFontSize = new ComboBox
            {
                Location = new Point(345, 27),
                Size = new Size(65, 26),
                DropDownStyle = ComboBoxStyle.DropDown
            };
            string[] fontSizes = { "9", "10", "10.5", "11", "12", "14", "16", "18", "20", "22", "24", "28", "32" };
            cmbFontSize.Items.AddRange(fontSizes);

            chkBold = new CheckBox
            {
                Text = "太字",
                Location = new Point(420, 28),
                AutoSize = true,
                ForeColor = Color.Black
            };

            btnChooseFontDialog = new Button
            {
                Text = "詳細設定...",
                Location = new Point(95, 62),
                Size = new Size(110, 28),
                UseVisualStyleBackColor = true
            };
            btnChooseFontDialog.Click += BtnChooseFontDialog_Click;

            var lblPreview = new Label
            {
                Text = "プレビュー:",
                Location = new Point(15, 100),
                AutoSize = true,
                ForeColor = Color.Black
            };

            txtFontPreview = new TextBox
            {
                Text = "国文学 OCR Translator - 吾輩は猫である。ABC 123",
                Location = new Point(95, 96),
                Size = new Size(385, 75),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White
            };

            cmbFontFamily.SelectedIndexChanged += (s, e) => UpdatePreview();
            cmbFontSize.TextChanged += (s, e) => UpdatePreview();
            chkBold.CheckedChanged += (s, e) => UpdatePreview();

            grpFont.Controls.Add(lblFont);
            grpFont.Controls.Add(cmbFontFamily);
            grpFont.Controls.Add(lblSize);
            grpFont.Controls.Add(cmbFontSize);
            grpFont.Controls.Add(chkBold);
            grpFont.Controls.Add(btnChooseFontDialog);
            grpFont.Controls.Add(lblPreview);
            grpFont.Controls.Add(txtFontPreview);

            this.Controls.Add(grpFont);
            currentY += 195;

            // =========================================================
            // 2. 組方向・読み順設定 GroupBox
            // =========================================================
            var grpOrientation = new GroupBox
            {
                Text = "組方向・読み順設定",
                Location = new Point(15, currentY),
                Size = new Size(495, 120),
                ForeColor = Color.DarkSlateBlue
            };

            rdoOrientationAuto = new RadioButton
            {
                Text = "🔄 自動判定（書籍の行・文字配置から自動検出）",
                Location = new Point(20, 26),
                Size = new Size(450, 24),
                ForeColor = Color.Black
            };

            rdoOrientationVertical = new RadioButton
            {
                Text = "⬇ 縦書き優先（日本語縦組書籍：右列から左列・上から下）",
                Location = new Point(20, 54),
                Size = new Size(450, 24),
                ForeColor = Color.Black
            };

            rdoOrientationHorizontal = new RadioButton
            {
                Text = "➡ 横書き優先（横組書籍：上行から下行・左から右）",
                Location = new Point(20, 82),
                Size = new Size(450, 24),
                ForeColor = Color.Black
            };

            grpOrientation.Controls.Add(rdoOrientationAuto);
            grpOrientation.Controls.Add(rdoOrientationVertical);
            grpOrientation.Controls.Add(rdoOrientationHorizontal);

            this.Controls.Add(grpOrientation);
            currentY += 130;

            // =========================================================
            // 3. 書籍種別（OCR条件）設定 GroupBox
            // =========================================================
            var grpDocType = new GroupBox
            {
                Text = "書籍種別設定（OCR条件）",
                Location = new Point(15, currentY),
                Size = new Size(495, 140),
                ForeColor = Color.DarkSlateBlue
            };

            rdoDocTypeJapanese = new RadioButton
            {
                Text = "🇯🇵 和書（日本語）：通常の日本語文献・古典籍・近現代書",
                Location = new Point(20, 26),
                Size = new Size(450, 24),
                ForeColor = Color.Black
            };

            rdoDocTypeWestern = new RadioButton
            {
                Text = "🌐 洋書（英欧文）：英語・欧文書籍（横書き・単語間スペース保持）",
                Location = new Point(20, 54),
                Size = new Size(450, 24),
                ForeColor = Color.Black
            };

            lblWesternDesc = new Label
            {
                Text = "※洋書モードを選択すると、横書き読み順と英単語スペース保持が自動的に適用され、欧文テキストの認識・出力精度が向上します。",
                Location = new Point(38, 82),
                Size = new Size(445, 45),
                ForeColor = Color.DimGray,
                Font = new Font("Yu Gothic UI", 8.5f, FontStyle.Regular)
            };

            rdoDocTypeJapanese.CheckedChanged += DocType_CheckedChanged;
            rdoDocTypeWestern.CheckedChanged += DocType_CheckedChanged;

            grpDocType.Controls.Add(rdoDocTypeJapanese);
            grpDocType.Controls.Add(rdoDocTypeWestern);
            grpDocType.Controls.Add(lblWesternDesc);

            this.Controls.Add(grpDocType);
            currentY += 155;

            // =========================================================
            // 4. OK / キャンセル ボタン
            // =========================================================
            btnOk = new Button
            {
                Text = "OK",
                Location = new Point(290, currentY),
                Size = new Size(105, 34),
                DialogResult = DialogResult.OK,
                UseVisualStyleBackColor = true
            };
            btnOk.Click += BtnOk_Click;

            btnCancel = new Button
            {
                Text = "キャンセル",
                Location = new Point(405, currentY),
                Size = new Size(105, 34),
                DialogResult = DialogResult.Cancel,
                UseVisualStyleBackColor = true
            };

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
        }

        private void LoadCurrentSettingsToUi()
        {
            // フォント名
            if (cmbFontFamily.Items.Contains(currentSettings.FontFamilyName))
                cmbFontFamily.SelectedItem = currentSettings.FontFamilyName;
            else if (cmbFontFamily.Items.Count > 0)
                cmbFontFamily.SelectedIndex = 0;

            // フォントサイズ
            cmbFontSize.Text = currentSettings.FontSize.ToString("0.#");

            // 太字
            chkBold.Checked = currentSettings.FontBold;

            // 組方向
            switch (currentSettings.TextOrientation.ToLowerInvariant())
            {
                case "vertical":
                    rdoOrientationVertical.Checked = true;
                    break;
                case "horizontal":
                    rdoOrientationHorizontal.Checked = true;
                    break;
                default:
                    rdoOrientationAuto.Checked = true;
                    break;
            }

            // 書籍種別
            if (currentSettings.DocumentType.ToLowerInvariant() == "western")
            {
                rdoDocTypeWestern.Checked = true;
            }
            else
            {
                rdoDocTypeJapanese.Checked = true;
            }
        }

        private void DocType_CheckedChanged(object? sender, EventArgs e)
        {
            if (rdoDocTypeWestern.Checked)
            {
                // 洋書選択時は横書きを推奨・自動選択
                rdoOrientationHorizontal.Checked = true;
            }
        }

        private void BtnChooseFontDialog_Click(object? sender, EventArgs e)
        {
            using var fontDialog = new FontDialog
            {
                ShowColor = false,
                Font = ResultSettings.CreateFont()
            };

            if (fontDialog.ShowDialog(this) == DialogResult.OK)
            {
                if (cmbFontFamily.Items.Contains(fontDialog.Font.FontFamily.Name))
                    cmbFontFamily.SelectedItem = fontDialog.Font.FontFamily.Name;
                else
                {
                    cmbFontFamily.Items.Insert(0, fontDialog.Font.FontFamily.Name);
                    cmbFontFamily.SelectedIndex = 0;
                }

                cmbFontSize.Text = fontDialog.Font.Size.ToString("0.#");
                chkBold.Checked = fontDialog.Font.Bold;
                UpdatePreview();
            }
        }

        private void UpdatePreview()
        {
            try
            {
                string familyName = cmbFontFamily.Text;
                if (string.IsNullOrWhiteSpace(familyName))
                    familyName = "Yu Gothic UI";

                if (!float.TryParse(cmbFontSize.Text, out float size) || size < 6 || size > 72)
                    size = 11.0f;

                FontStyle style = chkBold.Checked ? FontStyle.Bold : FontStyle.Regular;
                txtFontPreview.Font = new Font(familyName, size, style);
            }
            catch
            {
                txtFontPreview.Font = new Font("Yu Gothic UI", 11.0f, FontStyle.Regular);
            }
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            // フォント設定取得
            ResultSettings.FontFamilyName = cmbFontFamily.Text;
            if (float.TryParse(cmbFontSize.Text, out float size) && size >= 6 && size <= 72)
                ResultSettings.FontSize = size;
            ResultSettings.FontBold = chkBold.Checked;

            // 組方向設定取得
            if (rdoOrientationVertical.Checked)
                ResultSettings.TextOrientation = "vertical";
            else if (rdoOrientationHorizontal.Checked)
                ResultSettings.TextOrientation = "horizontal";
            else
                ResultSettings.TextOrientation = "auto";

            // 書籍種別設定取得
            if (rdoDocTypeWestern.Checked)
            {
                ResultSettings.DocumentType = "western";
                ResultSettings.TextOrientation = "horizontal"; // 洋書は横書き
            }
            else
            {
                ResultSettings.DocumentType = "japanese";
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
