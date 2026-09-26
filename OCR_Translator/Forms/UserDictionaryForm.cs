using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using OCR_Translator.Models;
using OCR_Translator.Services;

namespace OCR_Translator.Forms
{
    public class UserDictionaryForm : Form
    {
        private TabControl tabControl = null!;

        // 1. 縦中横（10〜99）タブ
        private readonly List<TcyTemplateEntry> allTcyEntries = new();
        private DataGridView dgvTcy = null!;
        private TextBox txtTcySearch = null!;
        private Label lblTcyStatus = null!;
        private Button btnChangeTcyImage = null!;
        private Button btnRegenTcyFont = null!;
        private Button btnRebuildAllTcy = null!;

        // 2. 語句・誤読修正タブ
        private readonly List<UserDictionaryEntry> allWordEntries = new();
        private DataGridView dgvWords = null!;
        private TextBox txtWordSearch = null!;
        private TextBox txtTestInput = null!;
        private TextBox txtTestOutput = null!;
        private Label lblWordStatus = null!;
        private Button btnAddWord = null!;
        private Button btnDeleteWord = null!;
        private Button btnMoveUp = null!;
        private Button btnMoveDown = null!;

        // 共通ボタン
        private Button btnSave = null!;
        private Button btnClose = null!;

        private bool isModified = false;
        private readonly string? initialWord;
        private readonly int initialTabIndex;

        public UserDictionaryForm(string? initialTargetWord = null, int initialTab = 0)
        {
            initialWord = initialTargetWord;
            initialTabIndex = initialTab;

            InitializeComponents();
            LoadAllData();

            if (!string.IsNullOrWhiteSpace(initialWord))
            {
                tabControl.SelectedIndex = 1; // 語句タブ
                AddNewWordEntryWithWord(initialWord);
            }
            else if (initialTabIndex >= 0 && initialTabIndex < tabControl.TabCount)
            {
                tabControl.SelectedIndex = initialTabIndex;
            }
        }

        private void InitializeComponents()
        {
            Text = "OCR補正辞書・縦中横（10〜99）画像登録の管理";
            Size = new Size(920, 720);
            MinimumSize = new Size(800, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            Font = new Font("Meiryo UI", 9.5f, FontStyle.Regular);
            BackColor = Color.FromArgb(248, 249, 250);

            // ==========================================
            // メイン TabControl
            // ==========================================
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                ItemSize = new Size(240, 34),
                SizeMode = TabSizeMode.Fixed,
                Font = new Font("Meiryo UI", 10f, FontStyle.Bold)
            };

            var tabTcy = new TabPage("縦中横（10〜99）画像・数値登録") { BackColor = Color.White, Font = this.Font };
            var tabWords = new TabPage("語句・誤読修正ルール") { BackColor = Color.White, Font = this.Font };

            BuildTcyTab(tabTcy);
            BuildWordsTab(tabWords);

            tabControl.TabPages.Add(tabTcy);
            tabControl.TabPages.Add(tabWords);

            // ==========================================
            // ボトム フッターパネル (保存/閉じる)
            // ==========================================
            var pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                Padding = new Padding(15, 8, 15, 8),
                BackColor = Color.FromArgb(240, 244, 248)
            };

            btnSave = new Button
            {
                Text = "[保存] すべて保存して適用",
                Location = new Point(pnlFooter.Width - 280, 9),
                Size = new Size(170, 36),
                BackColor = Color.FromArgb(22, 101, 52),
                ForeColor = Color.White,
                Font = new Font("Meiryo UI", 9.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) => SaveAndApplyAll();

            btnClose = new Button
            {
                Text = "閉じる",
                Location = new Point(pnlFooter.Width - 100, 9),
                Size = new Size(90, 36),
                UseVisualStyleBackColor = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnClose.Click += (s, e) => CloseWithCheck();

            pnlFooter.Controls.Add(btnSave);
            pnlFooter.Controls.Add(btnClose);

            this.Controls.Add(tabControl);
            this.Controls.Add(pnlFooter);
        }

        // ====================================================================
        // 1. 縦中横（10〜99）画像登録タブ
        // ====================================================================
        private void BuildTcyTab(TabPage tab)
        {
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                Padding = new Padding(15, 8, 15, 8),
                BackColor = Color.FromArgb(240, 244, 255)
            };
            var lblTitle = new Label
            {
                Text = "縦中横（10〜99・アラビア数字）の基準画像・数値登録",
                Font = new Font("Meiryo UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                AutoSize = true,
                Location = new Point(12, 10)
            };
            var lblSubtitle = new Label
            {
                Text = "縦書き文書内の2桁数字（10〜99）を照合・認識するための基準画像です。各数値ごとにカスタム画像を登録・変更できます。",
                Font = new Font("Meiryo UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize = true,
                Location = new Point(14, 35)
            };
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);

            // ツールバー
            var pnlToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                Padding = new Padding(15, 6, 15, 6)
            };

            var lblSearch = new Label { Text = "検索:", AutoSize = true, Location = new Point(15, 13), ForeColor = Color.FromArgb(51, 65, 85) };
            txtTcySearch = new TextBox { Location = new Point(55, 9), Size = new Size(95, 26), PlaceholderText = "例: 27" };
            txtTcySearch.TextChanged += (s, e) => FilterTcyGrid();

            btnChangeTcyImage = new Button
            {
                Text = "画像を変更・登録...",
                Location = new Point(160, 7),
                Size = new Size(150, 30),
                BackColor = Color.FromArgb(238, 242, 255),
                ForeColor = Color.FromArgb(67, 56, 202),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Meiryo UI", 9f, FontStyle.Bold)
            };
            btnChangeTcyImage.FlatAppearance.BorderColor = Color.FromArgb(199, 210, 254);
            btnChangeTcyImage.Click += (s, e) => ChangeSelectedTcyImage();

            btnRegenTcyFont = new Button
            {
                Text = "標準フォントから再生成",
                Location = new Point(320, 7),
                Size = new Size(165, 30),
                UseVisualStyleBackColor = true
            };
            btnRegenTcyFont.Click += (s, e) => RegenSelectedTcyFont();

            btnRebuildAllTcy = new Button
            {
                Text = "10〜99全画像を一括生成",
                Location = new Point(495, 7),
                Size = new Size(175, 30),
                BackColor = Color.FromArgb(254, 243, 199),
                ForeColor = Color.FromArgb(146, 64, 14),
                FlatStyle = FlatStyle.Flat
            };
            btnRebuildAllTcy.FlatAppearance.BorderColor = Color.FromArgb(253, 230, 138);
            btnRebuildAllTcy.Click += (s, e) => RebuildAllTcyTemplates();

            lblTcyStatus = new Label
            {
                Text = "登録件数: 0 件",
                AutoSize = true,
                Location = new Point(680, 13),
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = new Font("Meiryo UI", 9f, FontStyle.Bold)
            };

            pnlToolbar.Controls.Add(lblSearch);
            pnlToolbar.Controls.Add(txtTcySearch);
            pnlToolbar.Controls.Add(btnChangeTcyImage);
            pnlToolbar.Controls.Add(btnRegenTcyFont);
            pnlToolbar.Controls.Add(btnRebuildAllTcy);
            pnlToolbar.Controls.Add(lblTcyStatus);

            // DataGridView
            dgvTcy = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.Fixed3D,
                RowHeadersWidth = 30,
                RowTemplate = { Height = 40 }
            };

            var colVal = new DataGridViewTextBoxColumn { HeaderText = "数値", DataPropertyName = "Value", Width = 70 };
            colVal.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colVal.DefaultCellStyle.Font = new Font("Meiryo UI", 11f, FontStyle.Bold);

            var colImg = new DataGridViewImageColumn
            {
                HeaderText = "登録基準画像",
                DataPropertyName = "Thumbnail",
                Width = 100,
                ImageLayout = DataGridViewImageCellLayout.Normal
            };
            colImg.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            var colEnabled = new DataGridViewCheckBoxColumn { HeaderText = "認識有効", DataPropertyName = "Enabled", Width = 80 };
            var colFile = new DataGridViewTextBoxColumn { HeaderText = "主画像ファイル", DataPropertyName = "PrimaryImage", Width = 150 };
            var colImagesCount = new DataGridViewTextBoxColumn { HeaderText = "登録バリエーション数", DataPropertyName = "ImagesCountDisplay", Width = 150 };
            var colDesc = new DataGridViewTextBoxColumn { HeaderText = "説明・備考", DataPropertyName = "Description", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };

            dgvTcy.Columns.AddRange(colVal, colImg, colEnabled, colFile, colImagesCount, colDesc);
            dgvTcy.CellValueChanged += (s, e) => { isModified = true; };
            dgvTcy.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvTcy.IsCurrentCellDirty)
                    dgvTcy.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            var pnlGrid = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 0, 15, 10) };
            pnlGrid.Controls.Add(dgvTcy);

            tab.Controls.Add(pnlGrid);
            tab.Controls.Add(pnlToolbar);
            tab.Controls.Add(pnlHeader);
        }

        private class TcyRowViewModel
        {
            public TcyTemplateEntry Entry { get; set; }
            public TcyRowViewModel(TcyTemplateEntry entry) { Entry = entry; }

            public string Value => Entry.Value;
            public Image? Thumbnail => Entry.Thumbnail;
            public bool Enabled { get => Entry.Enabled; set => Entry.Enabled = value; }
            public string PrimaryImage => Entry.PrimaryImage;
            public string ImagesCountDisplay => $"{Entry.Images?.Count ?? 1} 種類 (明朝/ゴシック/等)";
            public string Description { get => Entry.Description ?? ""; set => Entry.Description = value; }
        }

        private void FilterTcyGrid()
        {
            string q = txtTcySearch.Text.Trim();
            var filtered = string.IsNullOrEmpty(q)
                ? allTcyEntries
                : allTcyEntries.Where(e => e.Value.Contains(q) || (e.Description != null && e.Description.Contains(q))).ToList();

            var list = filtered.Select(e => new TcyRowViewModel(e)).ToList();
            dgvTcy.DataSource = new BindingSource { DataSource = list };
            lblTcyStatus.Text = $"登録件数: {allTcyEntries.Count} 件" + (filtered.Count != allTcyEntries.Count ? $" (表示: {filtered.Count})" : "");
        }

        private void ChangeSelectedTcyImage()
        {
            if (dgvTcy.CurrentRow?.DataBoundItem is TcyRowViewModel row)
            {
                using var ofd = new OpenFileDialog
                {
                    Title = $"数値「{row.Value}」の登録画像を選択",
                    Filter = "画像ファイル (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|すべてのファイル (*.*)|*.*"
                };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    TcyDictionaryService.RegisterCustomImage(row.Entry, ofd.FileName);
                    isModified = true;
                    FilterTcyGrid();
                    MessageBox.Show($"数値「{row.Value}」の画像を登録・更新しました。", "画像登録完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                MessageBox.Show("画像を変更する行を選択してください。", "選択", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void RegenSelectedTcyFont()
        {
            if (dgvTcy.CurrentRow?.DataBoundItem is TcyRowViewModel row)
            {
                string dir = TcyDictionaryService.GetTcyTemplatesDirectory();
                string fileName = $"{row.Value}.png";
                string targetPath = Path.Combine(dir, fileName);

                using var bmp = TcyDictionaryService.RenderNumberImage(row.Value, "MS Mincho", 32);
                bmp.Save(targetPath, System.Drawing.Imaging.ImageFormat.Png);
                row.Entry.Thumbnail?.Dispose();
                row.Entry.Thumbnail = new Bitmap(bmp);
                row.Entry.PrimaryImage = fileName;

                isModified = true;
                FilterTcyGrid();
                MessageBox.Show($"数値「{row.Value}」の画像を明朝フォントから再生成しました。", "再生成完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void RebuildAllTcyTemplates()
        {
            var dr = MessageBox.Show(
                "0〜9、10〜99の全数字テンプレート画像（明朝・ゴシック・Arial）を一括生成・初期化しますか？\n（登録済みのカスタム画像は保持されます）",
                "全画像の一括生成確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (dr == DialogResult.Yes)
            {
                allTcyEntries.Clear();
                var list = TcyDictionaryService.RebuildAllTemplates();
                allTcyEntries.AddRange(list);
                isModified = true;
                FilterTcyGrid();
                MessageBox.Show($"0〜9、10〜99（全{allTcyEntries.Count}件）の画像を生成・登録しました！", "生成完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ====================================================================
        // 2. 語句・誤読修正ルールタブ
        // ====================================================================
        private void BuildWordsTab(TabPage tab)
        {
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                Padding = new Padding(15, 8, 15, 8),
                BackColor = Color.FromArgb(240, 244, 248)
            };
            var lblTitle = new Label
            {
                Text = "ユーザー定義OCR補正辞書（単語・誤読修正ルール）",
                Font = new Font("Meiryo UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                AutoSize = true,
                Location = new Point(12, 10)
            };
            var lblSubtitle = new Label
            {
                Text = "OCR認識時に自動修正する語句・専門用語・正規表現置換を登録します。",
                Font = new Font("Meiryo UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(14, 35)
            };
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);

            // ツールバー
            var pnlToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                Padding = new Padding(15, 6, 15, 6)
            };

            var lblSearch = new Label { Text = "絞り込み:", AutoSize = true, Location = new Point(15, 13), ForeColor = Color.FromArgb(51, 65, 85) };
            txtWordSearch = new TextBox { Location = new Point(80, 9), Size = new Size(160, 26), PlaceholderText = "キーワード検索..." };
            txtWordSearch.TextChanged += (s, e) => FilterWordGrid();

            btnAddWord = new Button
            {
                Text = "＋ ルールを追加",
                Location = new Point(250, 7),
                Size = new Size(125, 30),
                BackColor = Color.FromArgb(238, 242, 255),
                ForeColor = Color.FromArgb(67, 56, 202),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Meiryo UI", 9f, FontStyle.Bold)
            };
            btnAddWord.FlatAppearance.BorderColor = Color.FromArgb(199, 210, 254);
            btnAddWord.Click += (s, e) => AddNewWordEntry();

            btnDeleteWord = new Button
            {
                Text = "✕ 選択行を削除",
                Location = new Point(385, 7),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(254, 242, 242),
                ForeColor = Color.FromArgb(185, 28, 28),
                FlatStyle = FlatStyle.Flat
            };
            btnDeleteWord.FlatAppearance.BorderColor = Color.FromArgb(254, 202, 202);
            btnDeleteWord.Click += (s, e) => DeleteSelectedWordEntry();

            btnMoveUp = new Button
            {
                Text = "▲ 上へ",
                Location = new Point(515, 7),
                Size = new Size(65, 30),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat
            };
            btnMoveUp.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnMoveUp.Click += (s, e) => MoveSelectedWordEntry(-1);

            btnMoveDown = new Button
            {
                Text = "▼ 下へ",
                Location = new Point(585, 7),
                Size = new Size(65, 30),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat
            };
            btnMoveDown.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnMoveDown.Click += (s, e) => MoveSelectedWordEntry(1);

            lblWordStatus = new Label
            {
                Text = "登録件数: 0 件",
                AutoSize = true,
                Location = new Point(660, 13),
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = new Font("Meiryo UI", 9f, FontStyle.Bold)
            };

            pnlToolbar.Controls.Add(lblSearch);
            pnlToolbar.Controls.Add(txtWordSearch);
            pnlToolbar.Controls.Add(btnAddWord);
            pnlToolbar.Controls.Add(btnDeleteWord);
            pnlToolbar.Controls.Add(btnMoveUp);
            pnlToolbar.Controls.Add(btnMoveDown);
            pnlToolbar.Controls.Add(lblWordStatus);

            // テストプレビュー
            var grpTest = new GroupBox
            {
                Text = "リアルタイム変換テスト",
                Dock = DockStyle.Bottom,
                Height = 110,
                Padding = new Padding(12, 6, 12, 6),
                ForeColor = Color.FromArgb(30, 41, 59)
            };
            var pnlTestInner = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            pnlTestInner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            pnlTestInner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            pnlTestInner.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
            pnlTestInner.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var lblTestIn = new Label { Text = "テスト入力文字列:", AutoSize = true, ForeColor = Color.FromArgb(71, 85, 105) };
            var lblTestOut = new Label { Text = "変換後プレビュー:", AutoSize = true, ForeColor = Color.FromArgb(71, 85, 105) };
            txtTestInput = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, PlaceholderText = "ここにテスト文章を入力..." };
            txtTestInput.TextChanged += (s, e) => UpdateTestPreview();
            txtTestOutput = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(254, 252, 232) };

            pnlTestInner.Controls.Add(lblTestIn, 0, 0);
            pnlTestInner.Controls.Add(lblTestOut, 1, 0);
            pnlTestInner.Controls.Add(txtTestInput, 0, 1);
            pnlTestInner.Controls.Add(txtTestOutput, 1, 1);
            grpTest.Controls.Add(pnlTestInner);

            // DataGridView
            dgvWords = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.Fixed3D,
                RowHeadersWidth = 30,
                RowTemplate = { Height = 28 }
            };

            var colOrig = new DataGridViewTextBoxColumn { HeaderText = "修正前（誤認識・検索パターン）", DataPropertyName = "Original", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 40 };
            var colRep = new DataGridViewTextBoxColumn { HeaderText = "修正後（正しい表記・置換語）", DataPropertyName = "Replacement", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 40 };
            var colType = new DataGridViewComboBoxColumn { HeaderText = "種別", DataPropertyName = "TypeDisplay", Width = 100 };
            colType.Items.AddRange("完全一致", "正規表現");
            var colWordDesc = new DataGridViewTextBoxColumn { HeaderText = "備考・説明", DataPropertyName = "Description", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 20 };

            dgvWords.Columns.AddRange(colOrig, colRep, colType, colWordDesc);
            dgvWords.CellValueChanged += (s, e) => { isModified = true; UpdateTestPreview(); };
            dgvWords.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvWords.IsCurrentCellDirty)
                    dgvWords.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            var pnlGrid = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 0, 15, 6) };
            pnlGrid.Controls.Add(dgvWords);

            tab.Controls.Add(pnlGrid);
            tab.Controls.Add(grpTest);
            tab.Controls.Add(pnlToolbar);
            tab.Controls.Add(pnlHeader);
        }

        private class WordRowViewModel
        {
            public UserDictionaryEntry Entry { get; set; }
            public WordRowViewModel(UserDictionaryEntry entry) { Entry = entry; }

            public string Original { get => Entry.Original; set => Entry.Original = value; }
            public string Replacement { get => Entry.Replacement; set => Entry.Replacement = value; }
            public string TypeDisplay { get => Entry.IsRegex ? "正規表現" : "完全一致"; set => Entry.IsRegex = (value == "正規表現"); }
            public string Description { get => Entry.Description; set => Entry.Description = value; }
        }

        private void LoadAllData()
        {
            // 1. TCY
            allTcyEntries.Clear();
            allTcyEntries.AddRange(TcyDictionaryService.LoadEntries());
            FilterTcyGrid();

            // 2. Words
            allWordEntries.Clear();
            allWordEntries.AddRange(UserDictionaryService.LoadEntries());
            FilterWordGrid();

            isModified = false;
        }

        private void FilterWordGrid()
        {
            string q = txtWordSearch.Text.Trim().ToLowerInvariant();
            var filtered = string.IsNullOrEmpty(q)
                ? allWordEntries
                : allWordEntries.Where(e =>
                    (e.Original != null && e.Original.ToLowerInvariant().Contains(q)) ||
                    (e.Replacement != null && e.Replacement.ToLowerInvariant().Contains(q)) ||
                    (e.Description != null && e.Description.ToLowerInvariant().Contains(q))).ToList();

            var list = filtered.Select(e => new WordRowViewModel(e)).ToList();
            dgvWords.DataSource = new BindingSource { DataSource = list };
            lblWordStatus.Text = $"登録件数: {allWordEntries.Count} 件" + (filtered.Count != allWordEntries.Count ? $" (表示: {filtered.Count})" : "");
            UpdateTestPreview();
        }

        private void AddNewWordEntry()
        {
            var newEntry = new UserDictionaryEntry { Original = "", Replacement = "", IsRegex = false, Description = "" };
            allWordEntries.Insert(0, newEntry);
            isModified = true;
            FilterWordGrid();
            if (dgvWords.Rows.Count > 0)
            {
                dgvWords.ClearSelection();
                dgvWords.Rows[0].Selected = true;
                dgvWords.CurrentCell = dgvWords.Rows[0].Cells[0];
                dgvWords.BeginEdit(true);
            }
        }

        private void AddNewWordEntryWithWord(string word)
        {
            var newEntry = new UserDictionaryEntry { Original = word, Replacement = word, IsRegex = false, Description = "" };
            allWordEntries.Insert(0, newEntry);
            isModified = true;
            FilterWordGrid();
            if (dgvWords.Rows.Count > 0)
            {
                dgvWords.ClearSelection();
                dgvWords.Rows[0].Selected = true;
                dgvWords.CurrentCell = dgvWords.Rows[0].Cells[1];
                dgvWords.BeginEdit(true);
            }
            txtTestInput.Text = $"例: {word} の認識テスト";
        }

        private void DeleteSelectedWordEntry()
        {
            if (dgvWords.CurrentRow?.DataBoundItem is WordRowViewModel row)
            {
                var dr = MessageBox.Show($"登録「{row.Original}」を削除しますか？", "削除確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.Yes)
                {
                    allWordEntries.Remove(row.Entry);
                    isModified = true;
                    FilterWordGrid();
                }
            }
        }

        private void MoveSelectedWordEntry(int direction)
        {
            if (dgvWords.CurrentRow?.DataBoundItem is WordRowViewModel row)
            {
                int idx = allWordEntries.IndexOf(row.Entry);
                int newIdx = idx + direction;
                if (newIdx >= 0 && newIdx < allWordEntries.Count)
                {
                    var item = allWordEntries[idx];
                    allWordEntries.RemoveAt(idx);
                    allWordEntries.Insert(newIdx, item);
                    isModified = true;
                    FilterWordGrid();
                    for (int i = 0; i < dgvWords.Rows.Count; i++)
                    {
                        if (dgvWords.Rows[i].DataBoundItem is WordRowViewModel r && r.Entry == item)
                        {
                            dgvWords.ClearSelection();
                            dgvWords.Rows[i].Selected = true;
                            dgvWords.CurrentCell = dgvWords.Rows[i].Cells[0];
                            break;
                        }
                    }
                }
            }
        }

        private void UpdateTestPreview()
        {
            string inText = txtTestInput.Text;
            if (string.IsNullOrEmpty(inText))
            {
                txtTestOutput.Text = "";
                return;
            }
            txtTestOutput.Text = UserDictionaryService.ApplyEntries(inText, allWordEntries);
        }

        private void SaveAndApplyAll()
        {
            try
            {
                // 1. TCY 保存
                TcyDictionaryService.SaveEntries(allTcyEntries);

                // 2. Words 保存
                var validWords = allWordEntries.Where(e => !string.IsNullOrWhiteSpace(e.Original)).ToList();
                UserDictionaryService.SaveEntries(validWords);

                isModified = false;
                MessageBox.Show($"補正辞書および縦中横登録データを保存・適用しました！\n（縦中横: {allTcyEntries.Count}件、語句ルール: {validWords.Count}件）", "保存完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CloseWithCheck()
        {
            if (isModified)
            {
                var dr = MessageBox.Show("未保存の変更があります。破棄して閉じますか？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.No)
                    return;
            }
            this.Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (isModified && this.DialogResult != DialogResult.OK)
            {
                var dr = MessageBox.Show("未保存の変更があります。破棄して閉じますか？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnFormClosing(e);
        }
    }
}
