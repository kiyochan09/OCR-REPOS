using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OCR_Translator.Models;

namespace OCR_Translator.Forms
{
    public class OcrExecutionForm : Form
    {
        private readonly int currentPage;               // 0-based
        private readonly int batchStart;                // 1-based
        private readonly int batchEnd;                  // 1-based
        private readonly int totalPages;                // 1-based
        private readonly HashSet<int> regionModifiedPages; // 0-based (PDF画面領域を修正したページのみ)
        private readonly string? pdfName;

        public List<int> SelectedPages { get; private set; } = new();

        private Label lblDocInfo = null!;
        private RadioButton rbCurrentPage = null!;
        private RadioButton rbModifiedPages = null!;
        private RadioButton rbBatchRange = null!;
        private RadioButton rbCustomRange = null!;
        private NumericUpDown numCustomStart = null!;
        private NumericUpDown numCustomEnd = null!;
        private Label lblSummary = null!;
        private Button btnExecute = null!;
        private Button btnCancel = null!;

        public OcrExecutionForm(
            int currentPageIndex,
            int batchStart1Based,
            int batchEnd1Based,
            int totalPagesCount,
            HashSet<int> regionModifiedPageSet,
            string? documentName = null)
        {
            currentPage = currentPageIndex;
            batchStart = batchStart1Based;
            batchEnd = batchEnd1Based;
            totalPages = totalPagesCount;
            regionModifiedPages = regionModifiedPageSet ?? new HashSet<int>();
            pdfName = documentName;

            InitializeComponents();
            UpdateScopeSelection();
        }

        private void InitializeComponents()
        {
            Text = "OCR文字認識 実行設定";
            Size = new Size(540, 490);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            BackColor = Color.FromArgb(248, 250, 252);

            var pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };

            // 1. 文書・現在位置ヘッダー
            string docTitle = !string.IsNullOrEmpty(pdfName) ? pdfName : "開いている文書";
            lblDocInfo = new Label
            {
                Text = $"📄 対象: {docTitle} (全 {totalPages} ページ)  /  現在: 第 {currentPage + 1} ページ",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(20, 15),
                Size = new Size(485, 26),
                AutoEllipsis = true
            };
            pnlMain.Controls.Add(lblDocInfo);

            // 2. 実行範囲グループボックス
            var grpScope = new GroupBox
            {
                Text = " OCRを実行する範囲を選択 ",
                Location = new Point(20, 48),
                Size = new Size(485, 275),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 58, 138),
                BackColor = Color.White
            };

            // オプション1: このページだけ (最優先・デフォルト推奨)
            rbCurrentPage = new RadioButton
            {
                Text = $"📄 このページだけ (第 {currentPage + 1} ページのみ)",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(21, 128, 61),
                Location = new Point(20, 30),
                Size = new Size(445, 30),
                Checked = true
            };
            rbCurrentPage.CheckedChanged += (s, e) => UpdateScopeSelection();
            grpScope.Controls.Add(rbCurrentPage);

            var lblCurDesc = new Label
            {
                Text = "現在画面に表示されているページのみ即座に認識を実行します（推奨・高速）。",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(42, 60),
                Size = new Size(420, 20)
            };
            grpScope.Controls.Add(lblCurDesc);

            // オプション2: 修正されたページのみ (PDF画面領域を修正したページのみ)
            int modCount = regionModifiedPages.Count;
            string modDesc = modCount > 0
                ? $"({modCount}件: P.{string.Join(", P.", regionModifiedPages.OrderBy(p => p).Take(5).Select(p => (p + 1).ToString()))}{(modCount > 5 ? "..." : "")})"
                : "(0件 - 領域修正なし)";

            rbModifiedPages = new RadioButton
            {
                Text = $"✏️ 修正されたページのみ {modDesc}",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = modCount > 0 ? Color.FromArgb(30, 41, 59) : Color.FromArgb(148, 163, 184),
                Location = new Point(20, 88),
                Size = new Size(445, 30),
                Enabled = modCount > 0
            };
            rbModifiedPages.CheckedChanged += (s, e) => UpdateScopeSelection();
            grpScope.Controls.Add(rbModifiedPages);

            var lblModDesc = new Label
            {
                Text = @"PDF画面領域（本文・見出し・表・図）を追加・変形・削除したページのみ差分認識します。
※右画面タブのテキスト編集は除外されます。",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(42, 118),
                Size = new Size(420, 32)
            };
            grpScope.Controls.Add(lblModDesc);

            // オプション3: 現在のバッチ範囲
            rbBatchRange = new RadioButton
            {
                Text = $"📑 現在の作業バッチ範囲 (P.{batchStart} 〜 P.{batchEnd})",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(20, 155),
                Size = new Size(445, 30)
            };
            rbBatchRange.CheckedChanged += (s, e) => UpdateScopeSelection();
            grpScope.Controls.Add(rbBatchRange);

            var lblBatchDesc = new Label
            {
                Text = $"ツールバーで設定されているバッチ範囲内（最大20ページ）を一括処理します。",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(42, 185),
                Size = new Size(420, 20)
            };
            grpScope.Controls.Add(lblBatchDesc);

            // オプション4: ページ範囲を指定
            rbCustomRange = new RadioButton
            {
                Text = "📚 ページ範囲を直接指定:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(20, 210),
                Size = new Size(185, 30)
            };
            rbCustomRange.CheckedChanged += (s, e) =>
            {
                numCustomStart.Enabled = rbCustomRange.Checked;
                numCustomEnd.Enabled = rbCustomRange.Checked;
                UpdateScopeSelection();
            };
            grpScope.Controls.Add(rbCustomRange);

            numCustomStart = new NumericUpDown
            {
                Minimum = 1,
                Maximum = Math.Max(1, totalPages),
                Value = Math.Max(1, batchStart),
                Location = new Point(210, 213),
                Size = new Size(65, 26),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                TextAlign = HorizontalAlignment.Center,
                Enabled = false
            };
            numCustomStart.ValueChanged += (s, e) => UpdateScopeSelection();
            grpScope.Controls.Add(numCustomStart);

            var lblTilde = new Label
            {
                Text = "〜",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(280, 214),
                AutoSize = true,
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            grpScope.Controls.Add(lblTilde);

            numCustomEnd = new NumericUpDown
            {
                Minimum = 1,
                Maximum = Math.Max(1, totalPages),
                Value = Math.Min(Math.Max(1, totalPages), batchEnd),
                Location = new Point(305, 213),
                Size = new Size(65, 26),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                TextAlign = HorizontalAlignment.Center,
                Enabled = false
            };
            numCustomEnd.ValueChanged += (s, e) => UpdateScopeSelection();
            grpScope.Controls.Add(numCustomEnd);

            var lblTotalBadge = new Label
            {
                Text = $"/ 全{totalPages}P",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(375, 217),
                AutoSize = true
            };
            grpScope.Controls.Add(lblTotalBadge);

            pnlMain.Controls.Add(grpScope);

            // 3. 実行対象サマリー
            lblSummary = new Label
            {
                Location = new Point(20, 332),
                Size = new Size(485, 42),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(29, 78, 216),
                BackColor = Color.FromArgb(239, 246, 255),
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "対象: 第 1 ページのみ （計 1 ページ）"
            };
            pnlMain.Controls.Add(lblSummary);

            // 4. ボタン配置
            btnExecute = new Button
            {
                Text = "🚀 OCR実行開始",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(245, 385),
                Size = new Size(150, 42),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;
            pnlMain.Controls.Add(btnExecute);

            btnCancel = new Button
            {
                Text = "キャンセル",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                Location = new Point(405, 385),
                Size = new Size(100, 42),
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;
            pnlMain.Controls.Add(btnCancel);

            Controls.Add(pnlMain);
            AcceptButton = btnExecute;
            CancelButton = btnCancel;
        }

        private void UpdateScopeSelection()
        {
            var pages = CalculateSelectedPages();
            SelectedPages = pages;

            if (pages.Count == 0)
            {
                lblSummary.Text = "⚠️ 実行対象となるページがありません。";
                lblSummary.ForeColor = Color.FromArgb(220, 38, 38);
                lblSummary.BackColor = Color.FromArgb(254, 242, 242);
                btnExecute.Enabled = false;
                return;
            }

            btnExecute.Enabled = true;
            lblSummary.ForeColor = Color.FromArgb(29, 78, 216);
            lblSummary.BackColor = Color.FromArgb(239, 246, 255);

            if (rbCurrentPage.Checked)
            {
                lblSummary.Text = $"対象: 第 {currentPage + 1} ページのみ （計 1 ページ）";
            }
            else if (rbModifiedPages.Checked)
            {
                string pList = string.Join(", ", pages.OrderBy(p => p).Take(8).Select(p => $"P.{p + 1}"));
                if (pages.Count > 8) pList += $" ...他{pages.Count - 8}頁";
                lblSummary.Text = $"対象（PDF画面領域修正）: {pList} （計 {pages.Count} ページ）";
            }
            else if (rbBatchRange.Checked)
            {
                lblSummary.Text = $"対象: バッチ範囲 P.{batchStart} 〜 P.{batchEnd} （計 {pages.Count} ページ）";
            }
            else if (rbCustomRange.Checked)
            {
                int s = (int)numCustomStart.Value;
                int e = (int)numCustomEnd.Value;
                lblSummary.Text = $"対象: 指定範囲 P.{s} 〜 P.{e} （計 {pages.Count} ページ）";
            }
        }

        private List<int> CalculateSelectedPages()
        {
            var list = new List<int>();

            if (rbCurrentPage.Checked)
            {
                if (currentPage >= 0 && currentPage < totalPages)
                    list.Add(currentPage);
            }
            else if (rbModifiedPages.Checked)
            {
                list.AddRange(regionModifiedPages.Where(p => p >= 0 && p < totalPages).OrderBy(p => p));
            }
            else if (rbBatchRange.Checked)
            {
                int s = Math.Max(0, batchStart - 1);
                int e = Math.Min(totalPages - 1, batchEnd - 1);
                for (int p = s; p <= e; p++)
                    list.Add(p);
            }
            else if (rbCustomRange.Checked)
            {
                int s = Math.Max(0, (int)numCustomStart.Value - 1);
                int e = Math.Min(totalPages - 1, (int)numCustomEnd.Value - 1);
                if (s > e)
                {
                    int tmp = s;
                    s = e;
                    e = tmp;
                }
                for (int p = s; p <= e; p++)
                    list.Add(p);
            }

            return list;
        }

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            SelectedPages = CalculateSelectedPages();
            if (SelectedPages.Count == 0)
            {
                MessageBox.Show("OCRを実行する対象ページが選択されていません。", "OCR実行設定",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
