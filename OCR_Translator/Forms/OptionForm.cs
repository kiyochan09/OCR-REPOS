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

        // バッチ処理設定
        private NumericUpDown numBatchPageSize = null!;

        // 段組設定
        private RadioButton rdoDeckAuto = null!;
        private RadioButton rdoDeck1 = null!;
        private RadioButton rdoDeck2 = null!;
        private RadioButton rdoDeck3 = null!;

        // 1行文字数設定
        private NumericUpDown numLineCharCount = null!;

        // Word出力設定
        private CheckBox chkInsertPageBreak = null!;
        private CheckBox chkMergeCrossPage = null!;

        // 注釈番号採番設定
        private RadioButton rdoFootnoteContinuous = null!;
        private RadioButton rdoFootnoteMajorHeading = null!;

        // 小見出し自動認識設定
        private RadioButton rdoSubheadingAutoOff = null!;
        private RadioButton rdoSubheadingAutoOn = null!;

        // ページ移動スコープ設定
        private RadioButton rdoNavScopeBatch = null!;
        private RadioButton rdoNavScopeFile = null!;

        // OCR解像度設定
        private RadioButton rdoDpi300 = null!;
        private RadioButton rdoDpi400 = null!;
        private RadioButton rdoDpi600 = null!;

        // OCR終了後の表示倍率設定
        private RadioButton rdoPostOcrFit = null!;
        private RadioButton rdoPostOcr50 = null!;
        private RadioButton rdoPostOcr75 = null!;
        private RadioButton rdoPostOcr85 = null!;
        private RadioButton rdoPostOcr100 = null!;

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
            this.Size = new Size(550, 850);
            this.AutoScroll = true;
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
            // 4. バッチ処理設定 GroupBox
            // =========================================================
            var grpBatch = new GroupBox
            {
                Text = "バッチ処理設定（大規模PDF・メモリ制御）",
                Location = new Point(15, currentY),
                Size = new Size(495, 75),
                ForeColor = Color.DarkSlateBlue
            };

            var lblBatch = new Label
            {
                Text = "1バッチあたりのページ数:",
                Location = new Point(15, 30),
                AutoSize = true,
                ForeColor = Color.Black
            };

            numBatchPageSize = new NumericUpDown
            {
                Location = new Point(190, 27),
                Size = new Size(65, 26),
                Minimum = 5,
                Maximum = 100,
                Value = 20,
                Increment = 5,
                TextAlign = HorizontalAlignment.Center
            };

            var lblBatchDesc = new Label
            {
                Text = "ページ （推奨: 20ページ。16GBメモリで安定動作）",
                Location = new Point(265, 30),
                AutoSize = true,
                ForeColor = Color.DimGray,
                Font = new Font("Yu Gothic UI", 8.5f, FontStyle.Regular)
            };

            grpBatch.Controls.Add(lblBatch);
            grpBatch.Controls.Add(numBatchPageSize);
            grpBatch.Controls.Add(lblBatchDesc);

            this.Controls.Add(grpBatch);
            currentY += 85;

            // =========================================================
            // 5. 段組設定 GroupBox
            // =========================================================
            var grpDeck = new GroupBox
            {
                Text = "段組設定（マルチカラム・多段組）",
                Location = new Point(15, currentY),
                Size = new Size(495, 75),
                ForeColor = Color.DarkSlateBlue
            };

            rdoDeckAuto = new RadioButton
            {
                Text = "⚡ 自動判定",
                Location = new Point(15, 30),
                Size = new Size(100, 24),
                ForeColor = Color.Black,
                Checked = true
            };

            rdoDeck1 = new RadioButton
            {
                Text = "1段組（単段）",
                Location = new Point(125, 30),
                Size = new Size(110, 24),
                ForeColor = Color.Black
            };

            rdoDeck2 = new RadioButton
            {
                Text = "2段組（上・下段）",
                Location = new Point(245, 30),
                Size = new Size(125, 24),
                ForeColor = Color.Black
            };

            rdoDeck3 = new RadioButton
            {
                Text = "3段組",
                Location = new Point(380, 30),
                Size = new Size(80, 24),
                ForeColor = Color.Black
            };

            grpDeck.Controls.Add(rdoDeckAuto);
            grpDeck.Controls.Add(rdoDeck1);
            grpDeck.Controls.Add(rdoDeck2);
            grpDeck.Controls.Add(rdoDeck3);

            this.Controls.Add(grpDeck);
            currentY += 85;

            // =========================================================
            // 6. 1行文字数設定 GroupBox
            // =========================================================
            var grpLineWrap = new GroupBox
            {
                Text = "本文1行文字数設定（改行・フォーマット）",
                Location = new Point(15, currentY),
                Size = new Size(495, 75),
                ForeColor = Color.DarkSlateBlue
            };

            var lblLineChar = new Label
            {
                Text = "1行あたりの文字数:",
                Location = new Point(15, 30),
                AutoSize = true,
                ForeColor = Color.Black
            };

            numLineCharCount = new NumericUpDown
            {
                Location = new Point(160, 27),
                Size = new Size(65, 26),
                Minimum = 0,
                Maximum = 200,
                Value = 0,
                Increment = 5,
                TextAlign = HorizontalAlignment.Center
            };

            var lblLineCharDesc = new Label
            {
                Text = "文字 （0: 段落単位で改行・強制改行なし。数値を指定すると段落内を指定文字数で強制改行）",
                Location = new Point(235, 30),
                AutoSize = true,
                ForeColor = Color.DimGray,
                Font = new Font("Yu Gothic UI", 8.5f, FontStyle.Regular)
            };

            grpLineWrap.Controls.Add(lblLineChar);
            grpLineWrap.Controls.Add(numLineCharCount);
            grpLineWrap.Controls.Add(lblLineCharDesc);

            this.Controls.Add(grpLineWrap);
            currentY += 85;

            // =========================================================
            // 7. Word出力設定 GroupBox
            // =========================================================
            var grpWordExport = new GroupBox
            {
                Text = "Word（.docx）出力設定",
                Location = new Point(15, currentY),
                Size = new Size(495, 85),
                ForeColor = Color.DarkSlateBlue
            };

            chkMergeCrossPage = new CheckBox
            {
                Text = "ページをまたぐ文章・段落を自動結合する（ハイフン復元・文末未完接続）",
                Location = new Point(15, 25),
                AutoSize = true,
                ForeColor = Color.Black
            };

            chkInsertPageBreak = new CheckBox
            {
                Text = "各ページの間に改ページ（ページ区切り）を挿入する",
                Location = new Point(15, 52),
                AutoSize = true,
                ForeColor = Color.Black
            };

            grpWordExport.Controls.Add(chkMergeCrossPage);
            grpWordExport.Controls.Add(chkInsertPageBreak);

            this.Controls.Add(grpWordExport);
            currentY += 95;

            // =========================================================
            // 7.5 注釈番号（【注N】）の採番方式 GroupBox
            // =========================================================
            var grpFootnoteScope = new GroupBox
            {
                Text = "注釈番号（【注N】）の採番方式",
                Location = new Point(15, currentY),
                Size = new Size(495, 80),
                ForeColor = Color.DarkSlateBlue
            };

            rdoFootnoteContinuous = new RadioButton
            {
                Text = "通し番号（初期値: 文書全体を通して連番 1..N）",
                Location = new Point(15, 25),
                AutoSize = true,
                ForeColor = Color.Black
            };

            rdoFootnoteMajorHeading = new RadioButton
            {
                Text = "大見出し単位（章・大見出しごとに 1 からリセット）",
                Location = new Point(15, 50),
                AutoSize = true,
                ForeColor = Color.Black
            };

            grpFootnoteScope.Controls.Add(rdoFootnoteContinuous);
            grpFootnoteScope.Controls.Add(rdoFootnoteMajorHeading);

            this.Controls.Add(grpFootnoteScope);
            currentY += 90;

            // =========================================================
            // 7.8 小見出しの自動認識設定 GroupBox
            // =========================================================
            var grpSubheading = new GroupBox
            {
                Text = "小見出しの自動認識設定",
                Location = new Point(15, currentY),
                Size = new Size(495, 80),
                ForeColor = Color.DarkSlateBlue
            };

            rdoSubheadingAutoOff = new RadioButton
            {
                Text = "自動認識しない（初期値: 手動で指定した小見出しのみ登録）",
                Location = new Point(15, 25),
                AutoSize = true,
                ForeColor = Color.Black
            };

            rdoSubheadingAutoOn = new RadioButton
            {
                Text = "自動認識する（本文中の記号・数字見出し等を自動検出して登録）",
                Location = new Point(15, 50),
                AutoSize = true,
                ForeColor = Color.Black
            };

            grpSubheading.Controls.Add(rdoSubheadingAutoOff);
            grpSubheading.Controls.Add(rdoSubheadingAutoOn);

            this.Controls.Add(grpSubheading);
            currentY += 90;

            // =========================================================
            // 8. 「最初/最後」ページ移動ボタン（⏮️/⏭️）の動作範囲 GroupBox
            // =========================================================
            var grpNavScope = new GroupBox
            {
                Text = "「最初/最後」ページ移動ボタン（⏮️/⏭️）の動作範囲",
                Location = new Point(15, currentY),
                Size = new Size(495, 80),
                ForeColor = Color.DarkSlateBlue
            };

            rdoNavScopeBatch = new RadioButton
            {
                Text = "作業中バッチの範囲内（開始〜終了ページ）で移動（推奨）",
                Location = new Point(15, 25),
                AutoSize = true,
                ForeColor = Color.Black
            };

            rdoNavScopeFile = new RadioButton
            {
                Text = "ファイル全体（1ページ〜最終ページ）で移動",
                Location = new Point(15, 50),
                AutoSize = true,
                ForeColor = Color.Black
            };

            grpNavScope.Controls.Add(rdoNavScopeBatch);
            grpNavScope.Controls.Add(rdoNavScopeFile);

            this.Controls.Add(grpNavScope);
            currentY += 90;

            // =========================================================
            // 8.5 OCRレンダリング解像度（DPI）設定 GroupBox
            // =========================================================
            var grpDpi = new GroupBox
            {
                Text = "OCR解像度（DPI）設定（原本保持・にじみ防止）",
                Location = new Point(15, currentY),
                Size = new Size(495, 80),
                ForeColor = Color.DarkSlateBlue
            };

            rdoDpi300 = new RadioButton
            {
                Text = "300 DPI (標準・推奨)",
                Location = new Point(15, 25),
                AutoSize = true,
                ForeColor = Color.Black
            };

            rdoDpi400 = new RadioButton
            {
                Text = "400 DPI (超高精細・細小文字用)",
                Location = new Point(175, 25),
                AutoSize = true,
                ForeColor = Color.Black
            };

            rdoDpi600 = new RadioButton
            {
                Text = "600 DPI (最高精細)",
                Location = new Point(365, 25),
                AutoSize = true,
                ForeColor = Color.Black
            };

            var lblDpiDesc = new Label
            {
                Text = "※原本PDFの超高解像度を維持してレンダリングし、文字のにじみや誤認識を防ぎます。",
                Location = new Point(15, 52),
                AutoSize = true,
                ForeColor = Color.DimGray,
                Font = new Font("Yu Gothic UI", 8.5f, FontStyle.Regular)
            };

            grpDpi.Controls.Add(rdoDpi300);
            grpDpi.Controls.Add(rdoDpi400);
            grpDpi.Controls.Add(rdoDpi600);
            grpDpi.Controls.Add(lblDpiDesc);

            this.Controls.Add(grpDpi);
            currentY += 90;

            // =========================================================
            // 8.6 OCR終了後の表示倍率設定 GroupBox
            // =========================================================
            var grpPostOcrZoom = new GroupBox
            {
                Text = "OCR終了後の表示倍率設定",
                Location = new Point(15, currentY),
                Size = new Size(495, 80),
                ForeColor = Color.DarkSlateBlue
            };

            rdoPostOcrFit = new RadioButton { Text = "全体表示", Location = new Point(15, 25), AutoSize = true, ForeColor = Color.Black };
            rdoPostOcr50 = new RadioButton { Text = "50%", Location = new Point(105, 25), AutoSize = true, ForeColor = Color.Black };
            rdoPostOcr75 = new RadioButton { Text = "75% (推奨)", Location = new Point(165, 25), AutoSize = true, ForeColor = Color.Black };
            rdoPostOcr85 = new RadioButton { Text = "85%", Location = new Point(270, 25), AutoSize = true, ForeColor = Color.Black };
            rdoPostOcr100 = new RadioButton { Text = "100%", Location = new Point(330, 25), AutoSize = true, ForeColor = Color.Black };

            var lblPostOcrZoomDesc = new Label
            {
                Text = "※OCR処理完了直後に、プレビュー画像をこの指定倍率へ自動設定します。",
                Location = new Point(15, 52),
                AutoSize = true,
                ForeColor = Color.DimGray,
                Font = new Font("Yu Gothic UI", 8.5f, FontStyle.Regular)
            };

            grpPostOcrZoom.Controls.Add(rdoPostOcrFit);
            grpPostOcrZoom.Controls.Add(rdoPostOcr50);
            grpPostOcrZoom.Controls.Add(rdoPostOcr75);
            grpPostOcrZoom.Controls.Add(rdoPostOcr85);
            grpPostOcrZoom.Controls.Add(rdoPostOcr100);
            grpPostOcrZoom.Controls.Add(lblPostOcrZoomDesc);

            this.Controls.Add(grpPostOcrZoom);
            currentY += 95;

            // =========================================================
            // 9. OK / キャンセル ボタン
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

            // バッチ処理
            numBatchPageSize.Value = Math.Max(5, Math.Min(100, currentSettings.BatchPageSize));

            // 段組設定
            switch (currentSettings.DeckCount)
            {
                case 1:
                    rdoDeck1.Checked = true;
                    break;
                case 2:
                    rdoDeck2.Checked = true;
                    break;
                case 3:
                    rdoDeck3.Checked = true;
                    break;
                default:
                    rdoDeckAuto.Checked = true;
                    break;
            }

            // 1行文字数設定（0: 段落単位で改行）
            numLineCharCount.Value = Math.Max(0, Math.Min(200, currentSettings.LineCharCount));

            // Word出力設定
            chkInsertPageBreak.Checked = currentSettings.InsertPageBreakOnWordExport;
            chkMergeCrossPage.Checked = currentSettings.MergeCrossPageParagraphs;

            // 注釈番号採番方式
            if (currentSettings.FootnoteNumberingScope == "majorHeading")
            {
                rdoFootnoteMajorHeading.Checked = true;
            }
            else
            {
                rdoFootnoteContinuous.Checked = true;
            }

            // 小見出し自動認識設定
            if (currentSettings.AutoDetectSubheadings)
            {
                rdoSubheadingAutoOn.Checked = true;
            }
            else
            {
                rdoSubheadingAutoOff.Checked = true;
            }

            // ページ移動スコープ設定
            if (currentSettings.FirstLastNavScope == "file")
            {
                rdoNavScopeFile.Checked = true;
            }
            else
            {
                rdoNavScopeBatch.Checked = true;
            }

            // OCR解像度（DPI）設定
            switch (currentSettings.RenderDpi)
            {
                case 400:
                    rdoDpi400.Checked = true;
                    break;
                case 600:
                    rdoDpi600.Checked = true;
                    break;
                default:
                    rdoDpi300.Checked = true;
                    break;
            }

            // OCR終了後表示倍率設定
            switch (currentSettings.PostOcrZoomRatio)
            {
                case "Fit":
                case "全体":
                case "全体表示":
                    rdoPostOcrFit.Checked = true;
                    break;
                case "50%":
                case "50":
                    rdoPostOcr50.Checked = true;
                    break;
                case "85%":
                case "85":
                    rdoPostOcr85.Checked = true;
                    break;
                case "100%":
                case "100":
                    rdoPostOcr100.Checked = true;
                    break;
                case "75%":
                case "75":
                default:
                    rdoPostOcr75.Checked = true;
                    break;
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

            // バッチ処理設定取得
            ResultSettings.BatchPageSize = (int)numBatchPageSize.Value;

            // 段組設定取得
            if (rdoDeck1.Checked)
                ResultSettings.DeckCount = 1;
            else if (rdoDeck2.Checked)
                ResultSettings.DeckCount = 2;
            else if (rdoDeck3.Checked)
                ResultSettings.DeckCount = 3;
            else
                ResultSettings.DeckCount = 0; // 自動

            // 1行文字数設定取得
            ResultSettings.LineCharCount = (int)numLineCharCount.Value;

            // Word出力設定取得
            ResultSettings.InsertPageBreakOnWordExport = chkInsertPageBreak.Checked;
            ResultSettings.MergeCrossPageParagraphs = chkMergeCrossPage.Checked;
            ResultSettings.FootnoteNumberingScope = rdoFootnoteMajorHeading.Checked ? "majorHeading" : "continuous";
            ResultSettings.AutoDetectSubheadings = rdoSubheadingAutoOn.Checked;

            // ページ移動スコープ設定取得
            ResultSettings.FirstLastNavScope = rdoNavScopeFile.Checked ? "file" : "batch";

            // OCR解像度（DPI）設定取得
            if (rdoDpi400.Checked)
                ResultSettings.RenderDpi = 400;
            else if (rdoDpi600.Checked)
                ResultSettings.RenderDpi = 600;
            else
                ResultSettings.RenderDpi = 300;

            // OCR終了後倍率設定取得
            if (rdoPostOcrFit.Checked)
                ResultSettings.PostOcrZoomRatio = "Fit";
            else if (rdoPostOcr50.Checked)
                ResultSettings.PostOcrZoomRatio = "50%";
            else if (rdoPostOcr85.Checked)
                ResultSettings.PostOcrZoomRatio = "85%";
            else if (rdoPostOcr100.Checked)
                ResultSettings.PostOcrZoomRatio = "100%";
            else
                ResultSettings.PostOcrZoomRatio = "75%";

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
