using System;
using System.Drawing;
using System.Windows.Forms;

namespace MiniTrayMon
{
    /// <summary>
    /// Detailed overlay window shown on left-click of the tray icon.
    /// Auto-refreshes every second while open and auto-closes after 15s.
    /// </summary>
    public class OverlayForm : Form
    {
        private readonly Label _lbl;
        private readonly System.Windows.Forms.Timer _closeTimer;
        private readonly System.Windows.Forms.Timer _refreshTimer;
        private int _remaining;
        private SystemStats _last;

        public OverlayForm()
        {
            this.Text = "MiniTrayMon - Live Stats";
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.ClientSize = new Size(340, 260);
            this.BackColor = Color.FromArgb(20, 20, 25);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9);

            _lbl = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(20, 20, 25),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10)
            };
            this.Controls.Add(_lbl);

            _remaining = 15;
            _closeTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _closeTimer.Tick += (s, e) =>
            {
                _remaining--;
                if (_last != null) RenderStats(_last); // refresh countdown
                if (_remaining <= 0) this.Close();
            };

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _refreshTimer.Tick += (s, e) =>
            {
                // pull fresh stats from the parent (set via UpdateStats each poll)
            };
        }

        public void UpdateStats(SystemStats s)
        {
            _last = s;
            RenderStats(s);
        }

        private void RenderStats(SystemStats s)
        {
            _lbl.Text =
                "CPU        :  " + s.CpuPercent.ToString("F1") + " %\r\n" +
                "Memory     :  " + s.RamPercent.ToString("F1") + " %   (" +
                    s.RamUsedGB.ToString("F1") + " GB / " + s.RamTotalGB.ToString("F1") + " GB)\r\n" +
                "  Free     :  " + s.RamFreeGB.ToString("F1") + " GB\r\n" +
                "Disk       :  " + s.DiskPercent.ToString("F1") + " %\r\n" +
                "  Read     :  " + (s.DiskReadKBps / 1024f).ToString("F2") + " MB/s   (" +
                    s.DiskReadKBps.ToString("F0") + " KB/s)\r\n" +
                "  Write    :  " + (s.DiskWriteKBps / 1024f).ToString("F2") + " MB/s   (" +
                    s.DiskWriteKBps.ToString("F0") + " KB/s)\r\n" +
                "  Total    :  " + s.DiskTotalMbps.ToString("F2") + " Mbps\r\n" +
                "Network    :  v " + s.NetDownMbps.ToString("F2") + "  ^ " + s.NetUpMbps.ToString("F2") + " Mbps\r\n" +
                "Overall    :  " + s.OverallPercent.ToString("F1") + " %\r\n" +
                "\r\n(closes in " + _remaining + "s)";
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (this.Visible)
            {
                _closeTimer.Start();
                // position near tray (bottom-right)
                var wa = Screen.PrimaryScreen.WorkingArea;
                this.Location = new Point(wa.Right - this.Width - 12, wa.Bottom - this.Height - 12);
            }
            else _closeTimer.Stop();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _closeTimer.Dispose();
                _refreshTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
