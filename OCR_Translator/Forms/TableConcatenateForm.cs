using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OCR_Translator.Services;

namespace OCR_Translator.Forms
{
    public class TableConcatenateForm : Form
    {
        private readonly List<TableGridItemInfo> _allTables;
        private readonly List<TableGridItemInfo> _orderedTables;

        public List<TableGridItemInfo> SelectedTables { get; private set; } = new();
        public bool IsVertical => rbVertical.Checked;
        public bool SkipDuplicateHeader => chkSkipHeader.Checked;
        public string NewTableName => txtNewTableName.Text.Trim();

        // UI コントロール
        private CheckedListBox clbTables = null!;
        private Button btnMoveUp = null!;
        private Button btnMoveDown = null!;
        private RadioButton rbVertical = null!;
        private RadioButton rbHorizontal = null!;
        private CheckBox chkSkipHeader = null!;
        private TextBox txtNewTableName = null!;
        private DataGridView dgvPreview = null!;
        private Label lblPreviewSummary = null!;
        private Button btnExecute = null!;
        private Button btnCancel = null!;

        private bool _isUpdatingUi = false;

        public TableConcatenateForm(
            List<TableGridItemInfo> availableTables,
            List<TableGridItemInfo>? preSelectedTables = null)
        {
            _allTables = availableTables ?? new List<TableGridItemInfo>();
            _orderedTables = new List<TableGridItemInfo>(_allTables);

            InitializeComponents();

            // 初期選択の設定
            var preSelected = preSelectedTables ?? new List<TableGridItemInfo>();
            for (int i = 0; i < _orderedTables.Count; i++)
            {
                var tbl = _orderedTables[i];
                if (preSelected.Any(ps => ps.PageNumber == tbl.PageNumber && string.Equals(ps.TableName, tbl.TableName, StringComparison.OrdinalIgnoreCase)))
                {
                    clbTables.SetItemChecked(i, true);
                }
            }

            // 初期選択が1つ以下なら、先頭2つをデフォルトでチェック
            if (clbTables.CheckedIndices.Count < 2 && _orderedTables.Count >= 2)
            {
                clbTables.SetItemChecked(0, true);
                clbTables.SetItemChecked(1, true);
            }

            UpdateSelectedTablesAndPreview();
        }

        private void InitializeComponents()
        {
            Text = "表の連結（複数領域・複数ページまたぎ）";
            Size = new Size(860, 640);
            MinimumSize = new Size(760, 540);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            Font = new Font("Meiryo UI", 9.5f, FontStyle.Regular);
            BackColor = Color.FromArgb(248, 249, 250);

            var pnlMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(12)
            };
            pnlMain.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360f));
            pnlMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pnlMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            pnlMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));

            // 左側パネル: 対象テーブル選択＆順序並べ替え
            var grpLeft = new GroupBox
            {
                Text = "1. 連結する表を選択（2つ以上選択）",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ForeColor = Color.FromArgb(30, 41, 59)
            };

            clbTables = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                Font = new Font("Meiryo UI", 9.5f)
            };
            clbTables.ItemCheck += (s, e) =>
            {
                if (_isUpdatingUi) return;
                BeginInvoke(new Action(UpdateSelectedTablesAndPreview));
            };

            PopulateTableList();

            var pnlReorder = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 5, 0, 0)
            };

            btnMoveUp = new Button
            {
                Text = "▲ 上へ",
                Size = new Size(80, 32),
                UseVisualStyleBackColor = true
            };
            btnMoveUp.Click += (s, e) => MoveSelectedItem(-1);

            btnMoveDown = new Button
            {
                Text = "▼ 下へ",
                Size = new Size(80, 32),
                UseVisualStyleBackColor = true
            };
            btnMoveDown.Click += (s, e) => MoveSelectedItem(1);

            var btnSelectAll = new Button
            {
                Text = "全選択",
                Size = new Size(80, 32),
                UseVisualStyleBackColor = true
            };
            btnSelectAll.Click += (s, e) =>
            {
                _isUpdatingUi = true;
                for (int i = 0; i < clbTables.Items.Count; i++) clbTables.SetItemChecked(i, true);
                _isUpdatingUi = false;
                UpdateSelectedTablesAndPreview();
            };

            var btnClearAll = new Button
            {
                Text = "解除",
                Size = new Size(65, 32),
                UseVisualStyleBackColor = true
            };
            btnClearAll.Click += (s, e) =>
            {
                _isUpdatingUi = true;
                for (int i = 0; i < clbTables.Items.Count; i++) clbTables.SetItemChecked(i, false);
                _isUpdatingUi = false;
                UpdateSelectedTablesAndPreview();
            };

            pnlReorder.Controls.Add(btnMoveUp);
            pnlReorder.Controls.Add(btnMoveDown);
            pnlReorder.Controls.Add(btnSelectAll);
            pnlReorder.Controls.Add(btnClearAll);

            var lblHint = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                Text = "※リストの上にある表が先頭（上側・左側）に配置されます。",
                ForeColor = Color.DimGray,
                TextAlign = ContentAlignment.MiddleLeft
            };

            grpLeft.Controls.Add(clbTables);
            grpLeft.Controls.Add(lblHint);
            grpLeft.Controls.Add(pnlReorder);

            // 右側パネル: 設定＆プレビュー
            var pnlRight = new Panel
            {
                Dock = DockStyle.Fill
            };

            var grpOptions = new GroupBox
            {
                Text = "2. 連結オプション",
                Dock = DockStyle.Top,
                Height = 150,
                Padding = new Padding(12),
                ForeColor = Color.FromArgb(30, 41, 59)
            };

            rbVertical = new RadioButton
            {
                Text = "縦方向に連結（行追加 / 複数ページまたぎ・上下領域）",
                Location = new Point(16, 26),
                Size = new Size(420, 24),
                Checked = true,
                Font = new Font("Meiryo UI", 9.5f, FontStyle.Bold)
            };
            rbVertical.CheckedChanged += (s, e) =>
            {
                chkSkipHeader.Enabled = rbVertical.Checked;
                UpdateSelectedTablesAndPreview();
            };

            rbHorizontal = new RadioButton
            {
                Text = "横方向に連結（列追加 / 左右領域またぎ）",
                Location = new Point(16, 52),
                Size = new Size(420, 24)
            };
            rbHorizontal.CheckedChanged += (s, e) => UpdateSelectedTablesAndPreview();

            chkSkipHeader = new CheckBox
            {
                Text = "2つ目以降の表の先頭行が重複ヘッダーの場合のみ除外する",
                Location = new Point(36, 80),
                Size = new Size(420, 24),
                Checked = true,
                ForeColor = Color.DarkSlateBlue
            };
            chkSkipHeader.CheckedChanged += (s, e) => UpdateSelectedTablesAndPreview();

            var lblName = new Label
            {
                Text = "連結後の表名:",
                Location = new Point(16, 112),
                Size = new Size(100, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtNewTableName = new TextBox
            {
                Location = new Point(120, 112),
                Size = new Size(240, 26)
            };

            grpOptions.Controls.Add(rbVertical);
            grpOptions.Controls.Add(rbHorizontal);
            grpOptions.Controls.Add(chkSkipHeader);
            grpOptions.Controls.Add(lblName);
            grpOptions.Controls.Add(txtNewTableName);

            var grpPreview = new GroupBox
            {
                Text = "3. 連結プレビュー",
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ForeColor = Color.FromArgb(30, 41, 59)
            };

            lblPreviewSummary = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Meiryo UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };

            dgvPreview = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                BackgroundColor = Color.White
            };

            grpPreview.Controls.Add(dgvPreview);
            grpPreview.Controls.Add(lblPreviewSummary);

            pnlRight.Controls.Add(grpPreview);
            pnlRight.Controls.Add(grpOptions);

            // 下部ボタンバー
            var pnlBottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 0)
            };

            btnExecute = new Button
            {
                Text = "🔗 連結を実行",
                Size = new Size(150, 38),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.System,
                Font = new Font("Meiryo UI", 10f, FontStyle.Bold),
                DialogResult = DialogResult.OK
            };
            btnExecute.Click += (s, e) =>
            {
                if (SelectedTables.Count < 2)
                {
                    MessageBox.Show("連結する表を2つ以上チェックしてください。", "表の連結", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
            };

            btnCancel = new Button
            {
                Text = "キャンセル",
                Size = new Size(100, 38),
                UseVisualStyleBackColor = true,
                DialogResult = DialogResult.Cancel
            };

            pnlBottom.Controls.Add(btnExecute);
            pnlBottom.Controls.Add(btnCancel);

            pnlMain.Controls.Add(grpLeft, 0, 0);
            pnlMain.Controls.Add(pnlRight, 1, 0);
            pnlMain.Controls.Add(pnlBottom, 0, 1);
            pnlMain.SetColumnSpan(pnlBottom, 2);

            Controls.Add(pnlMain);
        }

        private void PopulateTableList()
        {
            _isUpdatingUi = true;
            clbTables.Items.Clear();
            foreach (var tbl in _orderedTables)
            {
                clbTables.Items.Add(tbl.DisplayTitle);
            }
            _isUpdatingUi = false;
        }

        private void MoveSelectedItem(int direction)
        {
            int idx = clbTables.SelectedIndex;
            if (idx < 0) return;

            int targetIdx = idx + direction;
            if (targetIdx < 0 || targetIdx >= _orderedTables.Count) return;

            bool isChecked = clbTables.GetItemChecked(idx);
            bool targetChecked = clbTables.GetItemChecked(targetIdx);

            var item = _orderedTables[idx];
            _orderedTables.RemoveAt(idx);
            _orderedTables.Insert(targetIdx, item);

            PopulateTableList();

            clbTables.SetItemChecked(targetIdx, isChecked);
            clbTables.SetItemChecked(idx, targetChecked);
            clbTables.SelectedIndex = targetIdx;

            UpdateSelectedTablesAndPreview();
        }

        private void UpdateSelectedTablesAndPreview()
        {
            SelectedTables.Clear();
            for (int i = 0; i < clbTables.Items.Count; i++)
            {
                if (clbTables.GetItemChecked(i))
                {
                    SelectedTables.Add(_orderedTables[i]);
                }
            }

            if (SelectedTables.Count == 0)
            {
                lblPreviewSummary.Text = "表が選択されていません。左側で2つ以上の表をチェックしてください。";
                dgvPreview.Columns.Clear();
                dgvPreview.Rows.Clear();
                btnExecute.Enabled = false;
                return;
            }

            btnExecute.Enabled = (SelectedTables.Count >= 2);

            // 表名初期値の自動設定
            if (string.IsNullOrWhiteSpace(txtNewTableName.Text) || txtNewTableName.Tag as string == "auto")
            {
                string autoName = SelectedTables[0].TableName;
                if (!autoName.Contains("連結"))
                {
                    autoName = $"{autoName} (連結)";
                }
                txtNewTableName.Text = autoName;
                txtNewTableName.Tag = "auto";
            }

            // プレビューの構築
            BuildPreviewGrid();
        }

        private void BuildPreviewGrid()
        {
            dgvPreview.Columns.Clear();
            dgvPreview.Rows.Clear();

            if (SelectedTables.Count == 0) return;

            if (IsVertical)
            {
                // 縦方向
                int maxCols = SelectedTables.Max(t => t.ColumnCount);
                if (maxCols < 1) maxCols = 1;

                dgvPreview.Columns.Add("Row", "行");
                dgvPreview.Columns["Row"]!.FillWeight = 10;

                for (int c = 1; c <= maxCols; c++)
                {
                    dgvPreview.Columns.Add($"Col{c}", $"列{c}");
                    dgvPreview.Columns[$"Col{c}"]!.FillWeight = Math.Max(15, 90 / maxCols);
                }

                int previewRowIndex = 1;
                for (int tIdx = 0; tIdx < SelectedTables.Count; tIdx++)
                {
                    var tbl = SelectedTables[tIdx];
                    int colOffset = tbl.Rows.Count > 0 && tbl.Rows[0].DataGridView?.Columns.Count > 3 && tbl.Rows[0].DataGridView?.Columns[0]?.Name == "Page" ? 3 : 0;
                    bool isDupHeader = false;
                    if (tIdx > 0 && SkipDuplicateHeader && tbl.Rows.Count > 1 && SelectedTables[0].Rows.Count > 0)
                    {
                        isDupHeader = TableCellMerger.IsDuplicateHeaderRow(SelectedTables[0].Rows[0], tbl.Rows[0], colOffset, maxCols);
                    }
                    int startR = isDupHeader ? 1 : 0;

                    for (int r = startR; r < tbl.Rows.Count; r++)
                    {
                        var srcRow = tbl.Rows[r];
                        var rowCells = new object[dgvPreview.Columns.Count];
                        rowCells[0] = previewRowIndex++;

                        for (int c = 0; c < maxCols; c++)
                        {
                            int dgvCol = colOffset + c;
                            if (srcRow.Cells.Count > dgvCol)
                            {
                                rowCells[c + 1] = srcRow.Cells[dgvCol].Value?.ToString() ?? "";
                            }
                        }

                        dgvPreview.Rows.Add(rowCells);
                    }
                }

                lblPreviewSummary.Text = $"【縦連結】選択: {SelectedTables.Count}個 | 結果: {dgvPreview.RowCount}行 × {maxCols}列 (重複見出し除外: {(SkipDuplicateHeader ? "適用" : "無効")})";
            }
            else
            {
                // 横方向
                int totalCols = SelectedTables.Sum(t => t.ColumnCount);
                int maxRows = SelectedTables.Max(t => t.RowCount);

                dgvPreview.Columns.Add("Row", "行");
                dgvPreview.Columns["Row"]!.FillWeight = 10;

                for (int c = 1; c <= totalCols; c++)
                {
                    dgvPreview.Columns.Add($"Col{c}", $"列{c}");
                    dgvPreview.Columns[$"Col{c}"]!.FillWeight = Math.Max(15, 90 / totalCols);
                }

                for (int r = 0; r < maxRows; r++)
                {
                    var rowCells = new object[totalCols + 1];
                    rowCells[0] = r + 1;

                    int currentCol = 1;
                    for (int tIdx = 0; tIdx < SelectedTables.Count; tIdx++)
                    {
                        var tbl = SelectedTables[tIdx];
                        int colOffset = tbl.Rows.Count > 0 && tbl.Rows[0].DataGridView?.Columns.Count > 3 && tbl.Rows[0].DataGridView?.Columns[0]?.Name == "Page" ? 3 : 0;

                        if (r < tbl.Rows.Count)
                        {
                            var srcRow = tbl.Rows[r];
                            for (int c = 0; c < tbl.ColumnCount; c++)
                            {
                                int dgvCol = colOffset + c;
                                if (srcRow.Cells.Count > dgvCol)
                                {
                                    rowCells[currentCol + c] = srcRow.Cells[dgvCol].Value?.ToString() ?? "";
                                }
                            }
                        }
                        currentCol += tbl.ColumnCount;
                    }

                    dgvPreview.Rows.Add(rowCells);
                }

                lblPreviewSummary.Text = $"【横連結】選択: {SelectedTables.Count}個 | 結果: {maxRows}行 × {totalCols}列";
            }
        }
    }
}
