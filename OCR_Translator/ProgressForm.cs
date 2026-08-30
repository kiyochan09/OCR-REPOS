using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace OCR_Translator
{
    /// <summary>
    /// 全ページの領域自動判定中に、現在の進行状況を表示する小さなモデルレスウィンドウ。
    /// </summary>
    public sealed class ProgressForm : Form
    {
        private readonly Label lblTitle;
        private readonly Label lblStatus;
        private readonly ProgressBar progressBar;
        private readonly Label lblCount;
        private readonly Label lblElapsed;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly System.Windows.Forms.Timer timer;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool AllowClose { get; set; }

        public ProgressForm(int totalPages)
        {
            Text = "領域自動判定 - 進行状況";
            ClientSize = new Size(520, 190);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ControlBox = true;

            lblTitle = new Label
            {
                AutoSize = false,
                Location = new Point(20, 18),
                Size = new Size(480, 28),
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                Text = "全ページの領域自動判定を実行しています。"
            };

            progressBar = new ProgressBar
            {
                Location = new Point(20, 55),
                Size = new Size(480, 25),
                Minimum = 0,
                Maximum = Math.Max(1, totalPages),
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };

            lblCount = new Label
            {
                AutoSize = false,
                Location = new Point(20, 88),
                Size = new Size(480, 24),
                Text = $"0 / {totalPages} ページ"
            };

            lblElapsed = new Label
            {
                AutoSize = false,
                Location = new Point(350, 88),
                Size = new Size(150, 24),
                TextAlign = ContentAlignment.MiddleRight,
                Text = "経過: 00:00"
            };

            lblStatus = new Label
            {
                AutoSize = false,
                Location = new Point(20, 118),
                Size = new Size(480, 50),
                Text = "準備中..."
            };

            Controls.Add(lblTitle);
            Controls.Add(progressBar);
            Controls.Add(lblCount);
            Controls.Add(lblElapsed);
            Controls.Add(lblStatus);

            timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (_, _) =>
            {
                lblElapsed.Text = $"経過: {stopwatch.Elapsed:hh':'mm':'ss}";
            };
            stopwatch.Start();
            timer.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                return;
            }

            if (e.Cancel)
                return;

            stopwatch.Stop();
            timer.Stop();
            base.OnFormClosing(e);
        }

        public void UpdateProgress(int completedPages, int totalPages, string status)
        {
            if (IsDisposed)
                return;

            completedPages = Math.Max(0, Math.Min(completedPages, totalPages));
            progressBar.Maximum = Math.Max(1, totalPages);
            progressBar.Value = Math.Min(completedPages, progressBar.Maximum);
            lblCount.Text = $"{completedPages} / {totalPages} ページ";
            lblStatus.Text = status;
            Refresh();
        }

        public void SetCompleted(string message)
        {
            if (IsDisposed)
                return;

            progressBar.Value = progressBar.Maximum;
            stopwatch.Stop();
            timer.Stop();
            lblCount.Text = $"{progressBar.Maximum} / {progressBar.Maximum} ページ";
            lblStatus.Text = message;
            Refresh();
        }
    }
}
