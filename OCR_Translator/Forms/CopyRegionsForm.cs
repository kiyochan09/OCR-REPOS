using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OCR_Translator.Models;
using OCR_Translator.Services;

namespace OCR_Translator.Forms
{
    public class CopyRegionsForm : Form
    {
        private readonly int sourcePage; // 0-based
        private readonly int minPage;   // 1-based
        private readonly int maxPage;   // 1-based
        private readonly List<OcrRegion> sourceRegions;

        public List<int> SelectedTargetPages { get; private set; } = new();

        private Label lblSourceInfo = null!;
        private RadioButton rbAll = null!;
        private RadioButton rbFromCurrent = null!;
        private RadioButton rbOdd = null!;
        private RadioButton rbEven = null!;
        private RadioButton rbCustom = null!;
        private TextBox txtCustom = null!;
        private Label lblPreview = null!;
        private Button btnApply = null!;
        private Button btnCancel = null!;

        public CopyRegionsForm(int sourcePageIndex, int minPage1Based, int maxPage1Based, List<OcrRegion> regions)
        {
            sourcePage = sourcePageIndex;
            minPage = minPage1Based;
            maxPage = maxPage1Based;
            sourceRegions = regions ?? new List<OcrRegion>();

            InitializeComponents();
            UpdateTargetPreview();
        }

        private void InitializeComponents()
        {
            Text = "領域を他ページへコピー";
            Size = new Size(520, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            BackColor = Color.FromArgb(248, 249, 250);

            var pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };

            // コピー元情報
            lblSourceInfo = new Label
            {
                Text = $"📋 コピー元: 第 {sourcePage + 1} ページ（設定領域: {sourceRegions.Count} 件）",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(20, 15),
                AutoSize = true
            };
            pnlMain.Controls.Add(lblSourceInfo);

            // 適用先グループ
            var grpTarget = new GroupBox
            {
                Text = " 適用先のページ範囲を選択 ",
                Location = new Point(20, 50),
                Size = new Size(460, 240),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            rbAll = new RadioButton
            {
                Text = $"開いている全ページに適用 (P.{minPage} 〜 P.{maxPage})",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                Location = new Point(20, 30),
                AutoSize = true,
                Checked = true
            };
            rbAll.CheckedChanged += (s, e) => UpdateTargetPreview();
            grpTarget.Controls.Add(rbAll);

            rbFromCurrent = new RadioButton
            {
                Text = $"現在ページ以降に適用 (P.{sourcePage + 1} 〜 P.{maxPage})",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                Location = new Point(20, 65),
                AutoSize = true
            };
            rbFromCurrent.CheckedChanged += (s, e) => UpdateTargetPreview();
            grpTarget.Controls.Add(rbFromCurrent);

            rbOdd = new RadioButton
            {
                Text = "奇数ページのみに適用 (P.1, P.3, P.5...)",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                Location = new Point(20, 100),
                AutoSize = true
            };
            rbOdd.CheckedChanged += (s, e) => UpdateTargetPreview();
            grpTarget.Controls.Add(rbOdd);

            rbEven = new RadioButton
            {
                Text = "偶数ページのみに適用 (P.2, P.4, P.6...)",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                Location = new Point(20, 135),
                AutoSize = true
            };
            rbEven.CheckedChanged += (s, e) => UpdateTargetPreview();
            grpTarget.Controls.Add(rbEven);

            rbCustom = new RadioButton
            {
                Text = "指定ページ範囲に適用:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                Location = new Point(20, 170),
                AutoSize = true
            };
            rbCustom.CheckedChanged += (s, e) =>
            {
                txtCustom.Enabled = rbCustom.Checked;
                UpdateTargetPreview();
            };
            grpTarget.Controls.Add(rbCustom);

            txtCustom = new TextBox
            {
                Location = new Point(200, 168),
                Size = new Size(230, 26),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                Text = $"{minPage}-{maxPage}",
                Enabled = false
            };
            txtCustom.TextChanged += (s, e) => UpdateTargetPreview();
            grpTarget.Controls.Add(txtCustom);

            pnlMain.Controls.Add(grpTarget);

            // プレビュー表示
            lblPreview = new Label
            {
                Location = new Point(20, 305),
                Size = new Size(460, 45),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 85, 105),
                Text = "適用対象: "
            };
            pnlMain.Controls.Add(lblPreview);

            // ボタン配置
            btnApply = new Button
            {
                Text = "適用 (コピー実行)",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(230, 370),
                Size = new Size(130, 40),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Click += BtnApply_Click;
            pnlMain.Controls.Add(btnApply);

            btnCancel = new Button
            {
                Text = "キャンセル",
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                Location = new Point(370, 370),
                Size = new Size(110, 40),
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
            pnlMain.Controls.Add(btnCancel);

            Controls.Add(pnlMain);
            AcceptButton = btnApply;
            CancelButton = btnCancel;
        }

        private List<int> CalculateTargetPages()
        {
            var list = new List<int>();

            if (rbAll.Checked)
            {
                for (int p = minPage; p <= maxPage; p++)
                {
                    int p0 = p - 1;
                    if (p0 != sourcePage)
                        list.Add(p0);
                }
            }
            else if (rbFromCurrent.Checked)
            {
                int start = sourcePage + 1; // 1-based current
                for (int p = start + 1; p <= maxPage; p++)
                {
                    int p0 = p - 1;
                    list.Add(p0);
                }
            }
            else if (rbOdd.Checked)
            {
                for (int p = minPage; p <= maxPage; p++)
                {
                    if (p % 2 == 1 && (p - 1) != sourcePage)
                        list.Add(p - 1);
                }
            }
            else if (rbEven.Checked)
            {
                for (int p = minPage; p <= maxPage; p++)
                {
                    if (p % 2 == 0 && (p - 1) != sourcePage)
                        list.Add(p - 1);
                }
            }
            else if (rbCustom.Checked)
            {
                var parsed = LayoutStorage.ParsePageRange(txtCustom.Text, minPage, maxPage);
                list = parsed.Where(p => p != sourcePage).ToList();
            }

            return list;
        }

        private void UpdateTargetPreview()
        {
            var pages = CalculateTargetPages();
            if (pages.Count == 0)
            {
                lblPreview.Text = "⚠️ 適用対象となる他のページがありません。";
                lblPreview.ForeColor = Color.FromArgb(220, 38, 38);
                btnApply.Enabled = false;
            }
            else
            {
                string pageListStr = string.Join(", ", pages.Take(10).Select(p => $"P.{p + 1}"));
                if (pages.Count > 10)
                    pageListStr += $" 他 {pages.Count - 10} ページ";

                lblPreview.Text = $"✔ 適用対象 ({pages.Count}ページ): {pageListStr}\n（※コピー元 P.{sourcePage + 1} は上書きされません）";
                lblPreview.ForeColor = Color.FromArgb(22, 101, 52);
                btnApply.Enabled = true;
            }
        }

        private void BtnApply_Click(object? sender, EventArgs e)
        {
            SelectedTargetPages = CalculateTargetPages();
            if (SelectedTargetPages.Count == 0)
            {
                MessageBox.Show("適用対象のページが選択されていません。", "確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
