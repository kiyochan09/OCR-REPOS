using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using OCR_Translator.Models;
using OCR_Translator.Services;

namespace OCR_Translator.Forms
{
    public class OptionForm : Form
    {
        private readonly AppSettings currentSettings;
        public AppSettings ResultSettings { get; private set; }

        // タブコントロール
        private TabControl tabControlMain = null!;

        // 1. フォント・表示設定
        private ComboBox cmbFontFamily = null!;
        private ComboBox cmbFontSize = null!;
        private CheckBox chkBold = null!;
        private Button btnChooseFontDialog = null!;
        private TextBox txtFontPreview = null!;
        private RadioButton rdoPostOcrFit = null!;
        private RadioButton rdoPostOcr50 = null!;
        private RadioButton rdoPostOcr75 = null!;
        private RadioButton rdoPostOcr85 = null!;
        private RadioButton rdoPostOcr100 = null!;
        private RadioButton rdoNavScopeBatch = null!;
        private RadioButton rdoNavScopeFile = null!;

        // 2. 組方向・OCR認識設定
        private RadioButton rdoOrientationAuto = null!;
        private RadioButton rdoOrientationVertical = null!;
        private RadioButton rdoOrientationHorizontal = null!;
        private RadioButton rdoDocTypeJapanese = null!;
        private RadioButton rdoDocTypeWestern = null!;
        private Label lblWesternDesc = null!;
        private RadioButton rdoDpi300 = null!;
        private RadioButton rdoDpi400 = null!;
        private RadioButton rdoDpi600 = null!;
        private RadioButton rdoDeckAuto = null!;
        private RadioButton rdoDeck1 = null!;
        private RadioButton rdoDeck2 = null!;
        private RadioButton rdoDeck3 = null!;
        private RadioButton rdoSubheadingAutoOff = null!;
        private RadioButton rdoSubheadingAutoOn = null!;

        // 3. 出力・書式設定
        private CheckBox chkInsertPageBreak = null!;
        private CheckBox chkMergeCrossPage = null!;
        private RadioButton rdoFootnoteContinuous = null!;
        private RadioButton rdoFootnoteMajorHeading = null!;
        private NumericUpDown numLineCharCount = null!;
        private NumericUpDown numBatchPageSize = null!;

        // 4. ユーザー辞書タブ用
        private Label lblTcyCount = null!;
        private Label lblWordDictCount = null!;
        private TextBox txtDictTestIn = null!;
        private TextBox txtDictTestOut = null!;

        // 操作ボタン
        private Button btnOk = null!;
        private Button btnCancel = null!;

        public OptionForm(AppSettings settings, int initialTabIndex = 0)
        {
            currentSettings = settings.Clone();
            ResultSettings = settings.Clone();

            InitializeComponents();
            LoadCurrentSettingsToUi();
            UpdatePreview();
            RefreshUserDictInfo();

            if (initialTabIndex >= 0 && initialTabIndex < tabControlMain.TabCount)
            {
                tabControlMain.SelectedIndex = initialTabIndex;
            }
        }

        private void InitializeComponents()
        {
            this.Text = "オプション設定";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(620, 710);
            this.Font = new Font("Yu Gothic UI", 9.5f, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            // =========================================================
            // メイン TabControl
            // =========================================================
            tabControlMain = new TabControl
            {
                Location = new Point(12, 12),
                Size = new Size(580, 595),
                ItemSize = new Size(130, 30),
                SizeMode = TabSizeMode.Fixed,
                Font = new Font("Yu Gothic UI", 9.5f, FontStyle.Bold)
            };

            var tabDisplay = new TabPage("フォント・表示") { BackColor = Color.White, AutoScroll = true, Font = this.Font };
            var tabOcr = new TabPage("OCR・認識") { BackColor = Color.White, AutoScroll = true, Font = this.Font };
            var tabOutput = new TabPage("出力・書式") { BackColor = Color.White, AutoScroll = true, Font = this.Font };
            var tabUserDict = new TabPage("補正辞書・置換") { BackColor = Color.White, AutoScroll = true, Font = this.Font };

            BuildDisplayTab(tabDisplay);
            BuildOcrTab(tabOcr);
            BuildOutputTab(tabOutput);
            BuildUserDictTab(tabUserDict);

            tabControlMain.TabPages.Add(tabDisplay);
            tabControlMain.TabPages.Add(tabOcr);
            tabControlMain.TabPages.Add(tabOutput);
            tabControlMain.TabPages.Add(tabUserDict);
            this.Controls.Add(tabControlMain);

            // =========================================================
            // OK / キャンセル ボタン
            // =========================================================
            btnOk = new Button
            {
                Text = "OK",
                Location = new Point(365, 620),
                Size = new Size(105, 36),
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(30, 64, 175),
                ForeColor = Color.White,
                Font = new Font("Yu Gothic UI", 9.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += BtnOk_Click;

            btnCancel = new Button
            {
                Text = "キャンセル",
                Location = new Point(480, 620),
                Size = new Size(105, 36),
                DialogResult = DialogResult.Cancel,
                UseVisualStyleBackColor = true
            };

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
        }

        private void BuildDisplayTab(TabPage tab)
        {
            int currentY = 10;

            // 1. フォント設定
            var grpFont = new GroupBox
            {
                Text = "フォント設定（OCR結果・エディタ表示用）",
                Location = new Point(12, currentY),
                Size = new Size(535, 175),
                ForeColor = Color.DarkSlateBlue
            };

            var lblFont = new Label { Text = "フォント名:", Location = new Point(15, 28), AutoSize = true, ForeColor = Color.Black };
            cmbFontFamily = new ComboBox
            {
                Location = new Point(95, 25),
                Size = new Size(180, 26),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

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
                foreach (var family in installedFonts.Families.OrderBy(f => f.Name))
                {
                    if (!cmbFontFamily.Items.Contains(family.Name))
                        cmbFontFamily.Items.Add(family.Name);
                }
            }

            var lblSize = new Label { Text = "サイズ:", Location = new Point(290, 28), AutoSize = true, ForeColor = Color.Black };
            cmbFontSize = new ComboBox { Location = new Point(345, 25), Size = new Size(65, 26), DropDownStyle = ComboBoxStyle.DropDown };
            cmbFontSize.Items.AddRange(new[] { "9", "10", "10.5", "11", "12", "14", "16", "18", "20", "22", "24", "28", "32" });

            chkBold = new CheckBox { Text = "太字", Location = new Point(425, 26), AutoSize = true, ForeColor = Color.Black };
            btnChooseFontDialog = new Button { Text = "詳細設定...", Location = new Point(95, 56), Size = new Size(100, 26), AutoSize = false };
            btnChooseFontDialog.Click += BtnChooseFontDialog_Click;

            var lblPreview = new Label { Text = "プレビュー:", Location = new Point(15, 88), AutoSize = true, ForeColor = Color.Black };
            txtFontPreview = new TextBox
            {
                Text = "国文学 OCR Translator - 吾輩は猫である。ABC 123",
                Location = new Point(95, 85),
                Size = new Size(425, 75),
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
            tab.Controls.Add(grpFont);
            currentY += 185;

            // 2. OCR終了後の表示倍率
            var grpPostOcrZoom = new GroupBox
            {
                Text = "OCR終了後のプレビュー表示倍率",
                Location = new Point(12, currentY),
                Size = new Size(535, 75),
                ForeColor = Color.DarkSlateBlue
            };
            rdoPostOcrFit = new RadioButton { Text = "全体表示", Location = new Point(15, 25), AutoSize = true, ForeColor = Color.Black };
            rdoPostOcr50 = new RadioButton { Text = "50%", Location = new Point(115, 25), AutoSize = true, ForeColor = Color.Black };
            rdoPostOcr75 = new RadioButton { Text = "75% (推奨)", Location = new Point(180, 25), AutoSize = true, ForeColor = Color.Black };
            rdoPostOcr85 = new RadioButton { Text = "85%", Location = new Point(290, 25), AutoSize = true, ForeColor = Color.Black };
            rdoPostOcr100 = new RadioButton { Text = "100%", Location = new Point(360, 25), AutoSize = true, ForeColor = Color.Black };

            grpPostOcrZoom.Controls.Add(rdoPostOcrFit);
            grpPostOcrZoom.Controls.Add(rdoPostOcr50);
            grpPostOcrZoom.Controls.Add(rdoPostOcr75);
            grpPostOcrZoom.Controls.Add(rdoPostOcr85);
            grpPostOcrZoom.Controls.Add(rdoPostOcr100);
            tab.Controls.Add(grpPostOcrZoom);
            currentY += 85;

            // 3. ページ移動スコープ
            var grpNavScope = new GroupBox
            {
                Text = "「最初/最後」ページ移動ボタン（⏮️/⏭️）の動作範囲",
                Location = new Point(12, currentY),
                Size = new Size(535, 80),
                ForeColor = Color.DarkSlateBlue
            };
            rdoNavScopeBatch = new RadioButton { Text = "作業中バッチの範囲内（開始〜終了ページ）で移動（推奨）", Location = new Point(15, 24), AutoSize = true, ForeColor = Color.Black };
            rdoNavScopeFile = new RadioButton { Text = "ファイル全体（1ページ〜最終ページ）で移動", Location = new Point(15, 48), AutoSize = true, ForeColor = Color.Black };
            grpNavScope.Controls.Add(rdoNavScopeBatch);
            grpNavScope.Controls.Add(rdoNavScopeFile);
            tab.Controls.Add(grpNavScope);
        }

        private void BuildOcrTab(TabPage tab)
        {
            int currentY = 10;

            // 1. 組方向設定
            var grpOrientation = new GroupBox
            {
                Text = "組方向・読み順設定",
                Location = new Point(12, currentY),
                Size = new Size(535, 110),
                ForeColor = Color.DarkSlateBlue
            };
            rdoOrientationAuto = new RadioButton { Text = "🔄 自動判定（書籍の行・文字配置から自動検出）", Location = new Point(15, 24), Size = new Size(490, 24), ForeColor = Color.Black };
            rdoOrientationVertical = new RadioButton { Text = "⬇ 縦書き優先（日本語縦組書籍：右列から左列・上から下）", Location = new Point(15, 50), Size = new Size(490, 24), ForeColor = Color.Black };
            rdoOrientationHorizontal = new RadioButton { Text = "➡ 横書き優先（横組書籍：上行から下行・左から右）", Location = new Point(15, 76), Size = new Size(490, 24), ForeColor = Color.Black };
            grpOrientation.Controls.Add(rdoOrientationAuto);
            grpOrientation.Controls.Add(rdoOrientationVertical);
            grpOrientation.Controls.Add(rdoOrientationHorizontal);
            tab.Controls.Add(grpOrientation);
            currentY += 120;

            // 2. 書籍種別設定
            var grpDocType = new GroupBox
            {
                Text = "書籍種別設定（OCR条件）",
                Location = new Point(12, currentY),
                Size = new Size(535, 115),
                ForeColor = Color.DarkSlateBlue
            };
            rdoDocTypeJapanese = new RadioButton { Text = "🇯🇵 和書（日本語）：通常の日本語文献・古典籍・近現代書", Location = new Point(15, 24), Size = new Size(490, 24), ForeColor = Color.Black };
            rdoDocTypeWestern = new RadioButton { Text = "🌐 洋書（英欧文）：英語・欧文書籍（横書き・単語間スペース保持）", Location = new Point(15, 50), Size = new Size(490, 24), ForeColor = Color.Black };
            lblWesternDesc = new Label
            {
                Text = "※洋書モードを選択すると、横書き読み順と英単語スペース保持が自動的に適用されます。",
                Location = new Point(35, 76),
                Size = new Size(480, 30),
                ForeColor = Color.DimGray,
                Font = new Font("Yu Gothic UI", 8.5f, FontStyle.Regular)
            };
            rdoDocTypeJapanese.CheckedChanged += DocType_CheckedChanged;
            rdoDocTypeWestern.CheckedChanged += DocType_CheckedChanged;
            grpDocType.Controls.Add(rdoDocTypeJapanese);
            grpDocType.Controls.Add(rdoDocTypeWestern);
            grpDocType.Controls.Add(lblWesternDesc);
            tab.Controls.Add(grpDocType);
            currentY += 125;

            // 3. OCR解像度
            var grpDpi = new GroupBox
            {
                Text = "OCR解像度（DPI）設定（原本保持・にじみ防止）",
                Location = new Point(12, currentY),
                Size = new Size(535, 65),
                ForeColor = Color.DarkSlateBlue
            };
            rdoDpi300 = new RadioButton { Text = "300 DPI (標準・推奨)", Location = new Point(15, 25), AutoSize = true, ForeColor = Color.Black };
            rdoDpi400 = new RadioButton { Text = "400 DPI (高精細)", Location = new Point(200, 25), AutoSize = true, ForeColor = Color.Black };
            rdoDpi600 = new RadioButton { Text = "600 DPI (最高精細)", Location = new Point(360, 25), AutoSize = true, ForeColor = Color.Black };
            grpDpi.Controls.Add(rdoDpi300);
            grpDpi.Controls.Add(rdoDpi400);
            grpDpi.Controls.Add(rdoDpi600);
            tab.Controls.Add(grpDpi);
            currentY += 75;

            // 4. 段組設定
            var grpDeck = new GroupBox
            {
                Text = "段組設定（マルチカラム・多段組）",
                Location = new Point(12, currentY),
                Size = new Size(535, 65),
                ForeColor = Color.DarkSlateBlue
            };
            rdoDeckAuto = new RadioButton { Text = "⚡ 自動判定", Location = new Point(15, 25), Size = new Size(100, 24), ForeColor = Color.Black };
            rdoDeck1 = new RadioButton { Text = "1段組（単段）", Location = new Point(135, 25), Size = new Size(115, 24), ForeColor = Color.Black };
            rdoDeck2 = new RadioButton { Text = "2段組（2段）", Location = new Point(265, 25), Size = new Size(115, 24), ForeColor = Color.Black };
            rdoDeck3 = new RadioButton { Text = "3段組", Location = new Point(395, 25), Size = new Size(90, 24), ForeColor = Color.Black };
            grpDeck.Controls.Add(rdoDeckAuto);
            grpDeck.Controls.Add(rdoDeck1);
            grpDeck.Controls.Add(rdoDeck2);
            grpDeck.Controls.Add(rdoDeck3);
            tab.Controls.Add(grpDeck);
            currentY += 75;

            // 5. 小見出し自動認識
            var grpSubheading = new GroupBox
            {
                Text = "小見出しの自動認識設定",
                Location = new Point(12, currentY),
                Size = new Size(535, 65),
                ForeColor = Color.DarkSlateBlue
            };
            rdoSubheadingAutoOff = new RadioButton { Text = "自動認識しない（手動指定のみ）", Location = new Point(15, 25), AutoSize = true, ForeColor = Color.Black };
            rdoSubheadingAutoOn = new RadioButton { Text = "自動認識する（本文中の記号・数字見出し等を自動検出）", Location = new Point(240, 25), AutoSize = true, ForeColor = Color.Black };
            grpSubheading.Controls.Add(rdoSubheadingAutoOff);
            grpSubheading.Controls.Add(rdoSubheadingAutoOn);
            tab.Controls.Add(grpSubheading);
        }

        private void BuildOutputTab(TabPage tab)
        {
            int currentY = 10;

            // 1. Word出力設定
            var grpWordExport = new GroupBox
            {
                Text = "Word（.docx）出力設定",
                Location = new Point(12, currentY),
                Size = new Size(535, 85),
                ForeColor = Color.DarkSlateBlue
            };
            chkMergeCrossPage = new CheckBox { Text = "ページをまたぐ文章・段落を自動結合する（ハイフン復元・文末未完接続）", Location = new Point(15, 24), AutoSize = true, ForeColor = Color.Black };
            chkInsertPageBreak = new CheckBox { Text = "各ページの間に改ページ（ページ区切り）を挿入する", Location = new Point(15, 50), AutoSize = true, ForeColor = Color.Black };
            grpWordExport.Controls.Add(chkMergeCrossPage);
            grpWordExport.Controls.Add(chkInsertPageBreak);
            tab.Controls.Add(grpWordExport);
            currentY += 95;

            // 2. 注釈番号採番
            var grpFootnoteScope = new GroupBox
            {
                Text = "注釈番号（【注N】）の採番方式",
                Location = new Point(12, currentY),
                Size = new Size(535, 75),
                ForeColor = Color.DarkSlateBlue
            };
            rdoFootnoteContinuous = new RadioButton { Text = "通し番号（初期値: 文書全体を通して連番 1..N）", Location = new Point(15, 24), AutoSize = true, ForeColor = Color.Black };
            rdoFootnoteMajorHeading = new RadioButton { Text = "大見出し単位（章・大見出しごとに 1 からリセット）", Location = new Point(15, 48), AutoSize = true, ForeColor = Color.Black };
            grpFootnoteScope.Controls.Add(rdoFootnoteContinuous);
            grpFootnoteScope.Controls.Add(rdoFootnoteMajorHeading);
            tab.Controls.Add(grpFootnoteScope);
            currentY += 85;

            // 3. 1行文字数設定
            var grpLineWrap = new GroupBox
            {
                Text = "本文1行文字数設定（改行フォーマット）",
                Location = new Point(12, currentY),
                Size = new Size(535, 75),
                ForeColor = Color.DarkSlateBlue
            };
            var lblLineChar = new Label { Text = "1行文字数:", Location = new Point(15, 28), AutoSize = true, ForeColor = Color.Black };
            numLineCharCount = new NumericUpDown { Location = new Point(105, 25), Size = new Size(65, 26), Minimum = 0, Maximum = 200, Value = 0, Increment = 5, TextAlign = HorizontalAlignment.Center };
            var lblLineCharDesc = new Label { Text = "文字 （0: 段落単位で改行。数値を指定すると段落内を指定文字数で強制改行）", Location = new Point(180, 28), AutoSize = true, ForeColor = Color.DimGray, Font = new Font("Yu Gothic UI", 8.5f, FontStyle.Regular) };
            grpLineWrap.Controls.Add(lblLineChar);
            grpLineWrap.Controls.Add(numLineCharCount);
            grpLineWrap.Controls.Add(lblLineCharDesc);
            tab.Controls.Add(grpLineWrap);
            currentY += 85;

            // 4. バッチ処理設定
            var grpBatch = new GroupBox
            {
                Text = "バッチ処理設定（大規模PDF・メモリ制御）",
                Location = new Point(12, currentY),
                Size = new Size(535, 75),
                ForeColor = Color.DarkSlateBlue
            };
            var lblBatch = new Label { Text = "1バッチあたりのページ数:", Location = new Point(15, 28), AutoSize = true, ForeColor = Color.Black };
            numBatchPageSize = new NumericUpDown { Location = new Point(190, 25), Size = new Size(65, 26), Minimum = 5, Maximum = 100, Value = 20, Increment = 5, TextAlign = HorizontalAlignment.Center };
            var lblBatchDesc = new Label { Text = "ページ （推奨: 20ページ。16GBメモリで安定動作）", Location = new Point(265, 28), AutoSize = true, ForeColor = Color.DimGray, Font = new Font("Yu Gothic UI", 8.5f, FontStyle.Regular) };
            grpBatch.Controls.Add(lblBatch);
            grpBatch.Controls.Add(numBatchPageSize);
            grpBatch.Controls.Add(lblBatchDesc);
            tab.Controls.Add(grpBatch);
        }

        private void BuildUserDictTab(TabPage tab)
        {
            int currentY = 12;

            // 1. 縦中横（10〜99）グループ
            var grpTcy = new GroupBox
            {
                Text = "縦中横（10〜99）画像・数値登録",
                Location = new Point(12, currentY),
                Size = new Size(535, 105),
                ForeColor = Color.DarkSlateBlue
            };

            lblTcyCount = new Label
            {
                Text = "登録件数: 読み込み中...",
                Location = new Point(15, 25),
                AutoSize = true,
                ForeColor = Color.FromArgb(30, 41, 59),
                Font = new Font("Yu Gothic UI", 9.5f, FontStyle.Bold)
            };

            var btnOpenTcy = new Button
            {
                Text = "縦中横（10〜99）画像・数値登録の管理画面を開く...",
                Location = new Point(15, 52),
                Size = new Size(495, 38),
                BackColor = Color.FromArgb(238, 242, 255),
                ForeColor = Color.FromArgb(67, 56, 202),
                Font = new Font("Yu Gothic UI", 9.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnOpenTcy.FlatAppearance.BorderColor = Color.FromArgb(199, 210, 254);
            btnOpenTcy.Click += (s, e) =>
            {
                using var dictForm = new UserDictionaryForm(null, 0); // 0: 縦中横タブ
                dictForm.ShowDialog(this);
                RefreshUserDictInfo();
            };

            grpTcy.Controls.Add(lblTcyCount);
            grpTcy.Controls.Add(btnOpenTcy);
            tab.Controls.Add(grpTcy);
            currentY += 115;

            // 2. 語句・誤読修正ルールグループ
            var grpWord = new GroupBox
            {
                Text = "語句・誤読修正ルール（専門用語・正規表現）",
                Location = new Point(12, currentY),
                Size = new Size(535, 105),
                ForeColor = Color.DarkSlateBlue
            };

            lblWordDictCount = new Label
            {
                Text = "登録ルール数: 読み込み中...",
                Location = new Point(15, 25),
                AutoSize = true,
                ForeColor = Color.FromArgb(30, 41, 59),
                Font = new Font("Yu Gothic UI", 9.5f, FontStyle.Bold)
            };

            var btnOpenWord = new Button
            {
                Text = "語句・誤読修正ルールの追加・編集・削除画面を開く...",
                Location = new Point(15, 52),
                Size = new Size(495, 38),
                BackColor = Color.FromArgb(240, 253, 244),
                ForeColor = Color.FromArgb(22, 101, 52),
                Font = new Font("Yu Gothic UI", 9.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnOpenWord.FlatAppearance.BorderColor = Color.FromArgb(187, 247, 208);
            btnOpenWord.Click += (s, e) =>
            {
                using var dictForm = new UserDictionaryForm(null, 1); // 1: 語句タブ
                dictForm.ShowDialog(this);
                RefreshUserDictInfo();
            };

            grpWord.Controls.Add(lblWordDictCount);
            grpWord.Controls.Add(btnOpenWord);
            tab.Controls.Add(grpWord);
            currentY += 115;

            // 3. リアルタイム変換テスト
            var grpQuickTest = new GroupBox
            {
                Text = "リアルタイム変換テスト（現在の辞書ルールをその場で検証）",
                Location = new Point(12, currentY),
                Size = new Size(535, 130),
                ForeColor = Color.DarkSlateBlue
            };

            var lblIn = new Label { Text = "テスト入力文字列:", Location = new Point(15, 22), AutoSize = true, ForeColor = Color.Black };
            txtDictTestIn = new TextBox
            {
                Location = new Point(15, 42),
                Size = new Size(245, 75),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                PlaceholderText = "ここにテスト文章を入力..."
            };

            var lblOut = new Label { Text = "変換後プレビュー:", Location = new Point(275, 22), AutoSize = true, ForeColor = Color.Black };
            txtDictTestOut = new TextBox
            {
                Location = new Point(275, 42),
                Size = new Size(245, 75),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(254, 252, 232)
            };

            txtDictTestIn.TextChanged += (s, e) =>
            {
                string text = txtDictTestIn.Text;
                var entries = UserDictionaryService.LoadEntries();
                txtDictTestOut.Text = UserDictionaryService.ApplyEntries(text, entries);
            };

            grpQuickTest.Controls.Add(lblIn);
            grpQuickTest.Controls.Add(txtDictTestIn);
            grpQuickTest.Controls.Add(lblOut);
            grpQuickTest.Controls.Add(txtDictTestOut);
            tab.Controls.Add(grpQuickTest);
        }

        private void RefreshUserDictInfo()
        {
            try
            {
                var tcyEntries = TcyDictionaryService.LoadEntries();
                if (lblTcyCount != null)
                {
                    lblTcyCount.Text = $"登録件数: {tcyEntries.Count} 件 （10〜99の全基準画像を登録済み）";
                }

                var wordEntries = UserDictionaryService.LoadEntries();
                if (lblWordDictCount != null)
                {
                    lblWordDictCount.Text = $"登録ルール数: {wordEntries.Count} 件";
                }

                if (txtDictTestIn != null && !string.IsNullOrEmpty(txtDictTestIn.Text) && txtDictTestOut != null)
                {
                    txtDictTestOut.Text = UserDictionaryService.ApplyEntries(txtDictTestIn.Text, wordEntries);
                }
            }
            catch
            {
            }
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
                rdoDocTypeWestern.Checked = true;
            else
                rdoDocTypeJapanese.Checked = true;

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

            // 1行文字数
            numLineCharCount.Value = Math.Max(0, Math.Min(200, currentSettings.LineCharCount));

            // Word出力設定
            chkInsertPageBreak.Checked = currentSettings.InsertPageBreakOnWordExport;
            chkMergeCrossPage.Checked = currentSettings.MergeCrossPageParagraphs;
            rdoFootnoteContinuous.Checked = (currentSettings.FootnoteNumberingScope != "majorHeading");
            rdoFootnoteMajorHeading.Checked = (currentSettings.FootnoteNumberingScope == "majorHeading");
            rdoSubheadingAutoOn.Checked = currentSettings.AutoDetectSubheadings;
            rdoSubheadingAutoOff.Checked = !currentSettings.AutoDetectSubheadings;
            rdoNavScopeFile.Checked = (currentSettings.FirstLastNavScope == "file");
            rdoNavScopeBatch.Checked = (currentSettings.FirstLastNavScope != "file");

            // DPI
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

            // OCR終了後倍率
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
            ResultSettings.FontFamilyName = cmbFontFamily.Text;
            if (float.TryParse(cmbFontSize.Text, out float size) && size >= 6 && size <= 72)
                ResultSettings.FontSize = size;
            ResultSettings.FontBold = chkBold.Checked;

            if (rdoOrientationVertical.Checked)
                ResultSettings.TextOrientation = "vertical";
            else if (rdoOrientationHorizontal.Checked)
                ResultSettings.TextOrientation = "horizontal";
            else
                ResultSettings.TextOrientation = "auto";

            if (rdoDocTypeWestern.Checked)
            {
                ResultSettings.DocumentType = "western";
                ResultSettings.TextOrientation = "horizontal";
            }
            else
            {
                ResultSettings.DocumentType = "japanese";
            }

            ResultSettings.BatchPageSize = (int)numBatchPageSize.Value;

            if (rdoDeck1.Checked)
                ResultSettings.DeckCount = 1;
            else if (rdoDeck2.Checked)
                ResultSettings.DeckCount = 2;
            else if (rdoDeck3.Checked)
                ResultSettings.DeckCount = 3;
            else
                ResultSettings.DeckCount = 0;

            ResultSettings.LineCharCount = (int)numLineCharCount.Value;
            ResultSettings.InsertPageBreakOnWordExport = chkInsertPageBreak.Checked;
            ResultSettings.MergeCrossPageParagraphs = chkMergeCrossPage.Checked;
            ResultSettings.FootnoteNumberingScope = rdoFootnoteMajorHeading.Checked ? "majorHeading" : "continuous";
            ResultSettings.AutoDetectSubheadings = rdoSubheadingAutoOn.Checked;
            ResultSettings.FirstLastNavScope = rdoNavScopeFile.Checked ? "file" : "batch";

            if (rdoDpi400.Checked)
                ResultSettings.RenderDpi = 400;
            else if (rdoDpi600.Checked)
                ResultSettings.RenderDpi = 600;
            else
                ResultSettings.RenderDpi = 300;

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
