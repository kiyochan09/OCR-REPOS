using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using OCR_Translator.Models;
using OCR_Translator.Services;

namespace OCR_Translator.Forms
{
    public class SearchResultItem
    {
        public int PageNumber { get; set; }
        public string TargetType { get; set; } = "本文"; // 本文, 見出し, 注釈文, 表, 未分類
        public string MatchedText { get; set; } = "";
        public string Snippet { get; set; } = "";
        public int LineIndex { get; set; }
    }

    public class BatchSearchForm : Form
    {
        private readonly Form1 _mainForm;
        private readonly List<SearchResultItem> _results = new();
        private int _currentResultIndex = -1;

        // UI コントロール
        private TextBox txtSearchQuery = null!;
        private Button btnClear = null!;
        private Button btnSearch = null!;
        private ComboBox cmbSearchScope = null!;
        private CheckBox chkBody = null!;
        private CheckBox chkHeading = null!;
        private CheckBox chkFootnote = null!;
        private CheckBox chkTable = null!;
        private CheckBox chkUnclassified = null!;
        private CheckBox chkMatchCase = null!;
        private CheckBox chkRegex = null!;
        private DataGridView dgvResults = null!;
        private Label lblStatus = null!;
        private Button btnPrev = null!;
        private Button btnNext = null!;
        private Button btnClose = null!;

        public BatchSearchForm(Form1 mainForm)
        {
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            InitializeComponents();
            UpdateScopeOptions();
        }

        private void InitializeComponents()
        {
            Text = "🔍 バッチ・プロジェクト内テキスト検索";
            Size = new Size(760, 560);
            MinimumSize = new Size(620, 420);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Yu Gothic UI", 9.5f, FontStyle.Regular);
            BackColor = Color.FromArgb(248, 250, 252);
            KeyPreview = true;

            // 1. 上部コントロールパネル
            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 125,
                Padding = new Padding(12, 10, 12, 6),
                BackColor = Color.White
            };

            // 1行目: 検索ボックス ＆ クリアボタン ＆ 検索ボタン ＆ 範囲選択
            var pnlRow1 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            var lblQuery = new Label
            {
                Text = "検索文字列:",
                AutoSize = true,
                Margin = new Padding(0, 7, 6, 0),
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59)
            };

            txtSearchQuery = new TextBox
            {
                Width = 220,
                Font = new Font("Yu Gothic UI", 10f),
                Margin = new Padding(0, 2, 6, 0)
            };
            txtSearchQuery.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    PerformSearch();
                }
            };

            btnClear = new Button
            {
                Text = "✖ クリア",
                Width = 76,
                Height = 30,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = new Font(Font, FontStyle.Regular),
                UseVisualStyleBackColor = false,
                Margin = new Padding(0, 1, 8, 0),
                Cursor = Cursors.Hand
            };
            btnClear.Click += (s, e) => ClearSearchQuery();

            btnSearch = new Button
            {
                Text = "🔍 検索",
                Width = 84,
                Height = 30,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                Font = new Font(Font, FontStyle.Bold),
                UseVisualStyleBackColor = false,
                Margin = new Padding(0, 1, 10, 0),
                Cursor = Cursors.Hand
            };
            btnSearch.Click += (s, e) => PerformSearch();

            cmbSearchScope = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 220,
                Font = new Font("Yu Gothic UI", 9.5f),
                Margin = new Padding(0, 3, 0, 0)
            };

            pnlRow1.Controls.Add(lblQuery);
            pnlRow1.Controls.Add(txtSearchQuery);
            pnlRow1.Controls.Add(btnClear);
            pnlRow1.Controls.Add(btnSearch);
            pnlRow1.Controls.Add(cmbSearchScope);

            // 2行目: 検索対象チェックボックス
            var pnlRow2 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 4, 0, 0)
            };

            var lblTarget = new Label
            {
                Text = "対象:",
                AutoSize = true,
                Margin = new Padding(0, 6, 8, 0),
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            chkBody = new CheckBox { Text = "本文", Checked = true, AutoSize = true, Margin = new Padding(0, 5, 12, 0) };
            chkHeading = new CheckBox { Text = "見出し", Checked = true, AutoSize = true, Margin = new Padding(0, 5, 12, 0) };
            chkFootnote = new CheckBox { Text = "注釈文", Checked = true, AutoSize = true, Margin = new Padding(0, 5, 12, 0) };
            chkTable = new CheckBox { Text = "表", Checked = true, AutoSize = true, Margin = new Padding(0, 5, 12, 0) };
            chkUnclassified = new CheckBox { Text = "未分類", Checked = false, AutoSize = true, Margin = new Padding(0, 5, 12, 0) };

            pnlRow2.Controls.Add(lblTarget);
            pnlRow2.Controls.Add(chkBody);
            pnlRow2.Controls.Add(chkHeading);
            pnlRow2.Controls.Add(chkFootnote);
            pnlRow2.Controls.Add(chkTable);
            pnlRow2.Controls.Add(chkUnclassified);

            // 3行目: オプションチェックボックス
            var pnlRow3 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 30,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            var lblOpt = new Label
            {
                Text = "設定:",
                AutoSize = true,
                Margin = new Padding(0, 5, 8, 0),
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            chkMatchCase = new CheckBox { Text = "大文字/小文字を区別", Checked = false, AutoSize = true, Margin = new Padding(0, 4, 16, 0) };
            chkRegex = new CheckBox { Text = "正規表現", Checked = false, AutoSize = true, Margin = new Padding(0, 4, 16, 0) };

            pnlRow3.Controls.Add(lblOpt);
            pnlRow3.Controls.Add(chkMatchCase);
            pnlRow3.Controls.Add(chkRegex);

            pnlTop.Controls.Add(pnlRow3);
            pnlTop.Controls.Add(pnlRow2);
            pnlTop.Controls.Add(pnlRow1);

            // 2. 下部ステータス＆ナビゲーションパネル
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = Color.FromArgb(241, 245, 249)
            };

            lblStatus = new Label
            {
                Text = "検索文字列を入力して「検索」をクリックしてください。",
                Dock = DockStyle.Left,
                AutoSize = false,
                Width = 380,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(51, 65, 85),
                Font = new Font(Font, FontStyle.Regular)
            };

            var pnlNavButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 320,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            btnClose = new Button
            {
                Text = "閉じる (Esc)",
                Size = new Size(95, 30),
                UseVisualStyleBackColor = true,
                Margin = new Padding(6, 0, 0, 0)
            };
            btnClose.Click += (s, e) => Close();

            btnNext = new Button
            {
                Text = "次へ ▶ (F3)",
                Size = new Size(95, 30),
                Enabled = false,
                UseVisualStyleBackColor = true,
                Margin = new Padding(6, 0, 0, 0)
            };
            btnNext.Click += (s, e) => NavigateResult(1);

            btnPrev = new Button
            {
                Text = "◀ 前へ (Shift+F3)",
                Size = new Size(110, 30),
                Enabled = false,
                UseVisualStyleBackColor = true,
                Margin = new Padding(0, 0, 0, 0)
            };
            btnPrev.Click += (s, e) => NavigateResult(-1);

            pnlNavButtons.Controls.Add(btnClose);
            pnlNavButtons.Controls.Add(btnNext);
            pnlNavButtons.Controls.Add(btnPrev);

            pnlBottom.Controls.Add(lblStatus);
            pnlBottom.Controls.Add(pnlNavButtons);

            // 3. 中央結果グリッド
            dgvResults = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                RowTemplate = { Height = 28 }
            };

            dgvResults.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "ページ",
                DataPropertyName = "PageNumber",
                Width = 75,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font(Font, FontStyle.Bold) }
            });

            dgvResults.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "種別",
                DataPropertyName = "TargetType",
                Width = 85,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dgvResults.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "出現行 / スニペット",
                DataPropertyName = "Snippet",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });

            dgvResults.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            dgvResults.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            dgvResults.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 58, 138);

            dgvResults.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _results.Count)
                {
                    _currentResultIndex = e.RowIndex;
                    JumpToCurrentResult();
                }
            };

            dgvResults.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _results.Count)
                {
                    _currentResultIndex = e.RowIndex;
                    JumpToCurrentResult();
                }
            };

            // 全体レイアウト構成
            Controls.Add(dgvResults);
            Controls.Add(pnlBottom);
            Controls.Add(pnlTop);

            // キーボードショートカット
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    e.Handled = true;
                    Close();
                }
                else if (e.KeyCode == Keys.F3)
                {
                    e.Handled = true;
                    if (e.Shift)
                        NavigateResult(-1);
                    else
                        NavigateResult(1);
                }
            };
        }

        public void UpdateScopeOptions()
        {
            int startP = _mainForm.TargetPageStart;
            int endP = _mainForm.TargetPageEnd;
            int totalPages = _mainForm.TotalPdfPages;

            cmbSearchScope.Items.Clear();
            cmbSearchScope.Items.Add($"現在のバッチ (P.{startP}〜{endP})");
            if (totalPages > 0)
            {
                cmbSearchScope.Items.Add($"全バッチ / 全ページ (P.1〜{totalPages})");
            }
            else
            {
                cmbSearchScope.Items.Add("全バッチ / 全ページ");
            }

            cmbSearchScope.SelectedIndex = 0;
        }

        public void SetSearchQuery(string query)
        {
            txtSearchQuery.Text = query ?? "";
            txtSearchQuery.SelectAll();
            txtSearchQuery.Focus();
        }

        public void ClearSearchQuery()
        {
            txtSearchQuery.Text = "";
            _results.Clear();
            _currentResultIndex = -1;
            dgvResults.DataSource = null;
            btnPrev.Enabled = false;
            btnNext.Enabled = false;
            lblStatus.Text = "検索文字列を入力して「検索」をクリックしてください。";
            txtSearchQuery.Focus();
        }

        public void PerformSearch()
        {
            string query = txtSearchQuery.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                lblStatus.Text = "検索文字列を入力してください。";
                return;
            }

            _results.Clear();
            _currentResultIndex = -1;

            bool isCurrentBatchOnly = (cmbSearchScope.SelectedIndex == 0);
            int startP = isCurrentBatchOnly ? _mainForm.TargetPageStart : 1;
            int endP = isCurrentBatchOnly ? _mainForm.TargetPageEnd : _mainForm.TotalPdfPages;
            if (endP <= 0) endP = Math.Max(startP, 1);

            bool searchBody = chkBody.Checked;
            bool searchHeading = chkHeading.Checked;
            bool searchFootnote = chkFootnote.Checked;
            bool searchTable = chkTable.Checked;
            bool searchUnclassified = chkUnclassified.Checked;

            bool matchCase = chkMatchCase.Checked;
            bool useRegex = chkRegex.Checked;

            Regex? regex = null;
            if (useRegex)
            {
                try
                {
                    var regexOptions = RegexOptions.Multiline;
                    if (!matchCase) regexOptions |= RegexOptions.IgnoreCase;
                    regex = new Regex(query, regexOptions);
                }
                catch (Exception ex)
                {
                    lblStatus.Text = $"正規表現エラー: {ex.Message}";
                    return;
                }
            }

            // 1. メイン画面のUIに現在表示されているテキストをパース
            var uiBodyMap = Form1.ParseBodyTextByPages(_mainForm.GetOcrResultText("body"));
            var uiHeadingMap = Form1.ParseItemsByPages(_mainForm.GetOcrResultText("heading"));
            var uiFootnoteMap = Form1.ParseItemsByPages(_mainForm.GetOcrResultText("footnote"));
            var uiUnclassifiedMap = Form1.ParseItemsByPages(_mainForm.GetOcrResultText("unclassified"));

            // プロジェクト保存先ディレクトリ
            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            string pdfName = _mainForm.CurrentPdfFileNameWithoutExtension;

            for (int p = startP; p <= endP; p++)
            {
                OcrPageData? pageData = null;

                // 本文検索
                if (searchBody)
                {
                    List<string>? bodyParas = null;
                    if (uiBodyMap.TryGetValue(p, out var paras))
                    {
                        bodyParas = paras;
                    }
                    else
                    {
                        pageData ??= OcrPageDataService.LoadPageData(Path.Combine(projectDir, "ocr_results", pdfName, $"page_{p:0000}"));
                        bodyParas = pageData?.BodyParagraphs;
                    }

                    if (bodyParas != null)
                    {
                        foreach (var para in bodyParas)
                        {
                            var lines = para.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                            for (int li = 0; li < lines.Length; li++)
                            {
                                string line = lines[li];
                                if (IsMatching(line, query, matchCase, regex, out string matched))
                                {
                                    _results.Add(new SearchResultItem
                                    {
                                        PageNumber = p,
                                        TargetType = "本文",
                                        MatchedText = matched,
                                        Snippet = line.Trim(),
                                        LineIndex = li
                                    });
                                }
                            }
                        }
                    }
                }

                // 見出し検索
                if (searchHeading)
                {
                    List<string>? headings = null;
                    if (uiHeadingMap.TryGetValue(p, out var hList))
                    {
                        headings = hList;
                    }
                    else
                    {
                        pageData ??= OcrPageDataService.LoadPageData(Path.Combine(projectDir, "ocr_results", pdfName, $"page_{p:0000}"));
                        headings = pageData?.Headings;
                    }

                    if (headings != null)
                    {
                        for (int hi = 0; hi < headings.Count; hi++)
                        {
                            string h = DocxExporter.RemovePagePrefix(headings[hi]);
                            if (IsMatching(h, query, matchCase, regex, out string matched))
                            {
                                _results.Add(new SearchResultItem
                                {
                                    PageNumber = p,
                                    TargetType = "見出し",
                                    MatchedText = matched,
                                    Snippet = h.Trim(),
                                    LineIndex = hi
                                });
                            }
                        }
                    }
                }

                // 注釈文検索
                if (searchFootnote)
                {
                    List<string>? footnotes = null;
                    if (uiFootnoteMap.TryGetValue(p, out var fnList))
                    {
                        footnotes = fnList;
                    }
                    else
                    {
                        pageData ??= OcrPageDataService.LoadPageData(Path.Combine(projectDir, "ocr_results", pdfName, $"page_{p:0000}"));
                        footnotes = pageData?.Footnotes;
                    }

                    if (footnotes != null)
                    {
                        for (int fni = 0; fni < footnotes.Count; fni++)
                        {
                            string fn = DocxExporter.RemovePagePrefix(footnotes[fni]);
                            if (IsMatching(fn, query, matchCase, regex, out string matched))
                            {
                                _results.Add(new SearchResultItem
                                {
                                    PageNumber = p,
                                    TargetType = "注釈文",
                                    MatchedText = matched,
                                    Snippet = fn.Trim(),
                                    LineIndex = fni
                                });
                            }
                        }
                    }
                }

                // 表検索
                if (searchTable)
                {
                    pageData ??= OcrPageDataService.LoadPageData(Path.Combine(projectDir, "ocr_results", pdfName, $"page_{p:0000}"));
                    if (pageData?.Tables != null)
                    {
                        foreach (var table in pageData.Tables)
                        {
                            foreach (var row in table.Rows)
                            {
                                string rowText = string.Join(" | ", row.Cells);
                                if (IsMatching(rowText, query, matchCase, regex, out string matched))
                                {
                                    _results.Add(new SearchResultItem
                                    {
                                        PageNumber = p,
                                        TargetType = "表",
                                        MatchedText = matched,
                                        Snippet = $"[{table.TableName}] {rowText}",
                                        LineIndex = 0
                                    });
                                }
                            }
                        }
                    }
                }

                // 未分類検索
                if (searchUnclassified)
                {
                    if (uiUnclassifiedMap.TryGetValue(p, out var uList))
                    {
                        for (int ui = 0; ui < uList.Count; ui++)
                        {
                            string u = DocxExporter.RemovePagePrefix(uList[ui]);
                            if (IsMatching(u, query, matchCase, regex, out string matched))
                            {
                                _results.Add(new SearchResultItem
                                {
                                    PageNumber = p,
                                    TargetType = "未分類",
                                    MatchedText = matched,
                                    Snippet = u.Trim(),
                                    LineIndex = ui
                                });
                            }
                        }
                    }
                }
            }

            // DataGridView の更新
            dgvResults.DataSource = null;
            dgvResults.DataSource = _results;

            int distinctPages = _results.Select(r => r.PageNumber).Distinct().Count();
            if (_results.Count > 0)
            {
                lblStatus.Text = $"{_results.Count} 件の一致が見つかりました（対象: {distinctPages} ページ）";
                btnPrev.Enabled = true;
                btnNext.Enabled = true;

                _currentResultIndex = 0;
                SelectGridRow(_currentResultIndex);
                JumpToCurrentResult();
            }
            else
            {
                lblStatus.Text = "一致する文字列は見つかりませんでした。";
                btnPrev.Enabled = false;
                btnNext.Enabled = false;
            }
        }

        private static bool IsMatching(string text, string query, bool matchCase, Regex? regex, out string matchedText)
        {
            matchedText = query;
            if (string.IsNullOrEmpty(text)) return false;

            if (regex != null)
            {
                var match = regex.Match(text);
                if (match.Success)
                {
                    matchedText = match.Value;
                    return true;
                }
                return false;
            }

            var comp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            int idx = text.IndexOf(query, comp);
            if (idx >= 0)
            {
                matchedText = text.Substring(idx, query.Length);
                return true;
            }

            return false;
        }

        private void NavigateResult(int direction)
        {
            if (_results.Count == 0) return;

            _currentResultIndex += direction;
            if (_currentResultIndex >= _results.Count) _currentResultIndex = 0;
            if (_currentResultIndex < 0) _currentResultIndex = _results.Count - 1;

            SelectGridRow(_currentResultIndex);
            JumpToCurrentResult();
        }

        private void SelectGridRow(int index)
        {
            if (index >= 0 && index < dgvResults.Rows.Count)
            {
                dgvResults.ClearSelection();
                dgvResults.Rows[index].Selected = true;
                if (!dgvResults.Rows[index].Displayed)
                {
                    dgvResults.FirstDisplayedScrollingRowIndex = index;
                }
            }
        }

        private void JumpToCurrentResult()
        {
            if (_currentResultIndex < 0 || _currentResultIndex >= _results.Count) return;

            var item = _results[_currentResultIndex];
            _mainForm.JumpToSearchResult(item.PageNumber, item.TargetType, item.MatchedText, item.Snippet);
        }
    }
}
