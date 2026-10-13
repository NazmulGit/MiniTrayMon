using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiniTrayMon
{
    /// <summary>
    /// A slim strip that sits on the Windows taskbar styled like the built-in
    /// clock/date row — not a floating box.
    ///
    /// Three interaction modes (right-click the strip itself to switch, or
    /// use the tray icon's menu):
    /// - Float (default): fully interactive. Left-click drag moves it
    ///   anywhere; right-click opens its own menu.
    /// - Fixed: position locked (drag disabled) but still clickable —
    ///   right-click still opens the menu so you can switch modes back.
    /// - Overlay: click-through, mouse passes straight to the taskbar
    ///   beneath. Use this once you've parked the meter where you want it
    ///   and don't want it intercepting clicks at all. Only reachable back
    ///   via the tray icon's own right-click menu since the strip itself
    ///   can't be right-clicked while click-through.
    ///
    /// Layout shows only the metrics enabled via MetricFlags, e.g. CPU-only.
    /// </summary>
    public class TaskbarSpeedMeter : Form
    {
        private SystemStats _last;
        private MetricFlags _visible = MetricFlags.All;
        private MeterMode _mode = MeterMode.Float;
        private UiScale _scale = UiScale.Medium;

        // Base (Medium-scale) dimensions — actual size/fonts are these times
        // the current UiScale multiplier.
        private const int BaseHeight = 42;
        private const int BaseSegmentWidth = 110;
        private const float BaseValueFontSize = 12f;
        private const float BaseSmallFontSize = 9.5f;

        // Drag state
        private bool _dragging;
        private Point _dragStartMouse;
        private Point _dragStartWindow;

        // Win32 extended window styles for click-through + no-focus
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int GWL_EXSTYLE = -20;
        private const int LWA_COLORKEY = 1;
        private const int LWA_ALPHA = 2;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, int crKey, byte alpha, int flags);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        // SetWindowPos: keep our current position/size, just force z-order.
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        // Colors — adapted at runtime based on detected taskbar brightness.
        private static readonly Color CpuDark = Color.FromArgb(40, 140, 70);
        private static readonly Color CpuLight = Color.FromArgb(120, 210, 130);
        private static readonly Color RamDark = Color.FromArgb(40, 90, 200);
        private static readonly Color RamLight = Color.FromArgb(130, 170, 255);
        private static readonly Color DiskDark = Color.FromArgb(200, 130, 20);
        private static readonly Color DiskLight = Color.FromArgb(255, 190, 90);
        private static readonly Color MutedDark = Color.FromArgb(90, 90, 100);
        private static readonly Color MutedLight = Color.FromArgb(160, 160, 170);

        private static Color TaskbarBg = GetTaskbarColor();
        private static bool IsLightTaskbar;
        private static Color CpuColor, RamColor, DiskColor, DownColor, UpColor, MutedColor;

        private Font _font;
        private Font _smallFont;
        private readonly System.Windows.Forms.Timer _topmostTimer;

        // Native right-click menu on the strip itself
        private readonly ContextMenuStrip _menu;
        private ToolStripMenuItem _modeFloatItem, _modeFixedItem, _modeOverlayItem;
        private ToolStripMenuItem _metricCpuItem, _metricRamItem, _metricDiskItem, _metricNetItem;
        private ToolStripMenuItem _sizeSmallItem, _sizeMediumItem, _sizeLargeItem, _sizeXLItem;

        /// <summary>Raised when the user drags the meter to a new position (for persistence).</summary>
        public event Action<Point> PositionChanged;
        /// <summary>Raised when the interaction mode changes via this meter's own menu.</summary>
        public event Action<MeterMode> ModeChanged;
        /// <summary>Raised when the user toggles a metric via this meter's own menu.</summary>
        public event Action<MetricFlags> VisibleMetricsChanged;
        /// <summary>Raised when "Reset position" is chosen from this meter's own menu.</summary>
        public event Action ResetPositionRequested;
        /// <summary>Raised when "Hide speed meter" is chosen from this meter's own menu.</summary>
        public event Action HideRequested;
        /// <summary>Raised when the size/scale changes via this meter's own menu.</summary>
        public event Action<UiScale> ScaleChanged;

        public TaskbarSpeedMeter()
        {
            ResolveColorsForTaskbar();

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.TopLevel = true;
            this.ShowInTaskbar = false;
            // Enlarged strip — was 30px tall / ~85px per segment; now taller
            // and wider per segment so text is clearly readable at a glance.
            // Actual size is (re-)computed by ApplyScale() below.
            this.Size = new Size(BaseSegmentWidth * 4, BaseHeight);
            this.BackColor = TaskbarBg;
            this.DoubleBuffered = true;
            this.Paint += TaskbarSpeedMeter_Paint;

            BuildFonts();

            // Native right-click menu, shown directly on the strip
            _menu = new ContextMenuStrip();
            var modeMenu = new ToolStripMenuItem("Position mode");
            _modeFloatItem = new ToolStripMenuItem("Float (drag to move)", null, (s, e) => SetMode(MeterMode.Float));
            _modeFixedItem = new ToolStripMenuItem("Fixed (locked position)", null, (s, e) => SetMode(MeterMode.Fixed));
            _modeOverlayItem = new ToolStripMenuItem("Overlay (click-through)", null, (s, e) => SetMode(MeterMode.Overlay));
            modeMenu.DropDownItems.AddRange(new ToolStripItem[] { _modeFloatItem, _modeFixedItem, _modeOverlayItem });
            _menu.Items.Add(modeMenu);

            var metricsMenu = new ToolStripMenuItem("Show metrics");
            _metricCpuItem = new ToolStripMenuItem("CPU", null, (s, e) => ToggleMetric(MetricFlags.Cpu));
            _metricRamItem = new ToolStripMenuItem("RAM", null, (s, e) => ToggleMetric(MetricFlags.Ram));
            _metricDiskItem = new ToolStripMenuItem("Disk", null, (s, e) => ToggleMetric(MetricFlags.Disk));
            _metricNetItem = new ToolStripMenuItem("Network", null, (s, e) => ToggleMetric(MetricFlags.Net));
            metricsMenu.DropDownItems.AddRange(new ToolStripItem[] { _metricCpuItem, _metricRamItem, _metricDiskItem, _metricNetItem });
            _menu.Items.Add(metricsMenu);

            // Size submenu — increase/decrease + explicit picks, affects both
            // the meter and (via TrayApp) the tray icon together.
            var sizeMenu = new ToolStripMenuItem("Size");
            sizeMenu.DropDownItems.Add("Increase size", null, (s, e) => SetScale(_scale.Increase()));
            sizeMenu.DropDownItems.Add("Decrease size", null, (s, e) => SetScale(_scale.Decrease()));
            sizeMenu.DropDownItems.Add(new ToolStripSeparator());
            _sizeSmallItem = new ToolStripMenuItem("Small", null, (s, e) => SetScale(UiScale.Small));
            _sizeMediumItem = new ToolStripMenuItem("Medium", null, (s, e) => SetScale(UiScale.Medium));
            _sizeLargeItem = new ToolStripMenuItem("Large", null, (s, e) => SetScale(UiScale.Large));
            _sizeXLItem = new ToolStripMenuItem("Extra Large", null, (s, e) => SetScale(UiScale.ExtraLarge));
            sizeMenu.DropDownItems.AddRange(new ToolStripItem[] { _sizeSmallItem, _sizeMediumItem, _sizeLargeItem, _sizeXLItem });
            _menu.Items.Add(sizeMenu);

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("Reset position", null, (s, e) => { if (ResetPositionRequested != null) ResetPositionRequested(); });
            _menu.Items.Add("Hide speed meter", null, (s, e) => { if (HideRequested != null) HideRequested(); });
            this.ContextMenuStrip = _menu;

            this.MouseDown += TaskbarSpeedMeter_MouseDown;
            this.MouseMove += TaskbarSpeedMeter_MouseMove;
            this.MouseUp += TaskbarSpeedMeter_MouseUp;

            this.Shown += (s, e) => { ApplyInteractionMode(); KeepOnTop(); };
            this.VisibleChanged += (s, e) => { if (this.Visible) { ApplyInteractionMode(); KeepOnTop(); } };

            // Re-assert HWND_TOPMOST periodically. explorer.exe reclaims the
            // topmost band whenever the taskbar itself is clicked (opening
            // Start, tray flyouts, jump lists, etc.), which otherwise drops
            // this window behind the taskbar. SetWindowPos here is a cheap
            // no-op when we're already on top, so a short interval is safe.
            _topmostTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _topmostTimer.Tick += (s, e) => KeepOnTop();
            _topmostTimer.Start();

            UpdateMenuChecks();
            UpdateSizeMenuChecks();
        }

        /// <summary>
        /// (Re)build the value/label fonts at the current scale's size.
        /// Disposes any previous fonts to avoid a GDI handle leak — this is
        /// called both at construction and whenever the scale changes.
        /// </summary>
        private void BuildFonts()
        {
            if (_font != null) _font.Dispose();
            if (_smallFont != null) _smallFont.Dispose();
            float mult = _scale.Multiplier();
            _font = new Font("Segoe UI", BaseValueFontSize * mult, FontStyle.Bold);
            _smallFont = new Font("Segoe UI", BaseSmallFontSize * mult, FontStyle.Regular);
        }

        /// <summary>
        /// Change the overall size (font + strip dimensions) of the meter.
        /// Affects only this meter; TrayApp mirrors the same UiScale onto
        /// the tray icon so "size" feels like one shared setting.
        /// </summary>
        public void SetScale(UiScale scale)
        {
            _scale = scale;
            BuildFonts();
            this.Height = (int)(BaseHeight * scale.Multiplier());
            RecomputeWidth();
            UpdateSizeMenuChecks();
            this.Invalidate();
            if (ScaleChanged != null) ScaleChanged(scale);
        }

        public UiScale CurrentScale { get { return _scale; } }

        private void RecomputeWidth()
        {
            int count = _visible.Count();
            int segW = (int)(BaseSegmentWidth * _scale.Multiplier());
            int newWidth = Math.Max((int)(90 * _scale.Multiplier()), count * segW);
            if (this.Width != newWidth)
            {
                this.Width = newWidth;
                if (_mode != MeterMode.Float) PositionOnTaskbar();
            }
        }

        private void UpdateSizeMenuChecks()
        {
            if (_sizeSmallItem == null) return;
            _sizeSmallItem.Checked = (_scale == UiScale.Small);
            _sizeMediumItem.Checked = (_scale == UiScale.Medium);
            _sizeLargeItem.Checked = (_scale == UiScale.Large);
            _sizeXLItem.Checked = (_scale == UiScale.ExtraLarge);
        }

        /// <summary>
        /// Update which metrics are shown. Triggers a repaint and resizes
        /// the window to fit however many segments remain.
        /// </summary>
        public void SetVisibleMetrics(MetricFlags flags)
        {
            _visible = flags;
            RecomputeWidth();
            UpdateMenuChecks();
            this.Invalidate();
        }

        private void ToggleMetric(MetricFlags metric)
        {
            MetricFlags next = _visible.Has(metric) ? (_visible & ~metric) : (_visible | metric);
            if (next == MetricFlags.None) return; // refuse to hide everything
            SetVisibleMetrics(next);
            if (VisibleMetricsChanged != null) VisibleMetricsChanged(next);
        }

        /// <summary>
        /// Switch between Float (draggable), Fixed (locked position, still
        /// clickable), and Overlay (click-through) modes.
        /// </summary>
        public void SetMode(MeterMode mode)
        {
            _mode = mode;
            ApplyInteractionMode();
            UpdateMenuChecks();
            this.Invalidate();
            if (ModeChanged != null) ModeChanged(mode);
        }

        public MeterMode Mode { get { return _mode; } }

        /// <summary>Legacy convenience: true when in Overlay (click-through) mode.</summary>
        public bool IsLocked { get { return _mode == MeterMode.Overlay; } }

        /// <summary>Legacy convenience for callers that only know locked/unlocked.</summary>
        public void SetLocked(bool locked)
        {
            SetMode(locked ? MeterMode.Overlay : MeterMode.Float);
        }

        private void UpdateMenuChecks()
        {
            if (_modeFloatItem == null) return;
            _modeFloatItem.Checked = (_mode == MeterMode.Float);
            _modeFixedItem.Checked = (_mode == MeterMode.Fixed);
            _modeOverlayItem.Checked = (_mode == MeterMode.Overlay);
            _metricCpuItem.Checked = _visible.Has(MetricFlags.Cpu);
            _metricRamItem.Checked = _visible.Has(MetricFlags.Ram);
            _metricDiskItem.Checked = _visible.Has(MetricFlags.Disk);
            _metricNetItem.Checked = _visible.Has(MetricFlags.Net);
        }

        private void TaskbarSpeedMeter_MouseDown(object sender, MouseEventArgs e)
        {
            // Dragging is only allowed in Float mode; Fixed keeps position
            // locked, Overlay is click-through so this never fires there.
            if (_mode != MeterMode.Float || e.Button != MouseButtons.Left) return;
            _dragging = true;
            _dragStartMouse = Cursor.Position;
            _dragStartWindow = this.Location;
        }

        private void TaskbarSpeedMeter_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            var cur = Cursor.Position;
            int dx = cur.X - _dragStartMouse.X;
            int dy = cur.Y - _dragStartMouse.Y;
            this.Location = new Point(_dragStartWindow.X + dx, _dragStartWindow.Y + dy);
        }

        private void TaskbarSpeedMeter_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            if (PositionChanged != null) PositionChanged(this.Location);
        }

        /// <summary>
        /// Sample the actual taskbar background color by reading a pixel from
        /// the taskbar area at runtime.
        /// </summary>
        private static Color GetTaskbarColor()
        {
            try
            {
                var screen = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                using (var bmp = new Bitmap(1, 1))
                using (var g = Graphics.FromImage(bmp))
                {
                    int sampleX = screen.Width / 2;
                    int sampleY = screen.Height - 8;
                    g.CopyFromScreen(sampleX, sampleY, 0, 0, new Size(1, 1));
                    Color c = bmp.GetPixel(0, 0);
                    if (c.R == 0 && c.G == 0 && c.B == 0)
                    {
                        g.CopyFromScreen(50, screen.Height - 8, 0, 0, new Size(1, 1));
                        c = bmp.GetPixel(0, 0);
                        if (c.R == 0 && c.G == 0 && c.B == 0)
                            return Color.FromArgb(32, 32, 36);
                    }
                    return c;
                }
            }
            catch
            {
                return Color.FromArgb(32, 32, 36);
            }
        }

        private static void ResolveColorsForTaskbar()
        {
            TaskbarBg = GetTaskbarColor();
            double lum = 0.299 * TaskbarBg.R + 0.587 * TaskbarBg.G + 0.114 * TaskbarBg.B;
            IsLightTaskbar = lum > 128;

            if (IsLightTaskbar)
            {
                CpuColor = CpuDark; RamColor = RamDark; DiskColor = DiskDark;
                DownColor = CpuDark; UpColor = DiskDark; MutedColor = MutedDark;
            }
            else
            {
                CpuColor = CpuLight; RamColor = RamLight; DiskColor = DiskLight;
                DownColor = CpuLight; UpColor = DiskLight; MutedColor = MutedLight;
            }
        }

        private void TaskbarSpeedMeter_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Float/Fixed: draw a subtle border so the user can see the strip
            // is interactive (dashed amber = draggable, solid gray = fixed).
            if (_mode == MeterMode.Float)
            {
                using (var p = new Pen(Color.FromArgb(180, 255, 210, 90), 1))
                {
                    p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    g.DrawRectangle(p, 0, 0, this.Width - 1, this.Height - 1);
                }
            }
            else if (_mode == MeterMode.Fixed)
            {
                using (var p = new Pen(Color.FromArgb(120, 140, 140, 150), 1))
                    g.DrawRectangle(p, 0, 0, this.Width - 1, this.Height - 1);
            }

            if (_last == null) return;
            var s = _last;

            float mult = _scale.Multiplier();
            int x = (int)(8 * mult);
            int y = (int)(9 * mult);

            if (_visible.Has(MetricFlags.Cpu))
            {
                x = DrawSegment(g, x, y, "CPU", s.CpuPercent.ToString("F0"), "%", CpuColor);
                x = DrawDivider(g, x, y);
            }
            if (_visible.Has(MetricFlags.Ram))
            {
                x = DrawSegment(g, x, y, "RAM", s.RamPercent.ToString("F0"), "%", RamColor);
                x = DrawDivider(g, x, y);
            }
            if (_visible.Has(MetricFlags.Disk))
            {
                x = DrawSegment(g, x, y, "DSK", s.DiskPercent.ToString("F0"), "%", DiskColor);
                x = DrawDivider(g, x, y);
            }
            if (_visible.Has(MetricFlags.Net))
            {
                float downKBps = s.NetDownMbps * 122.07f;
                float upKBps = s.NetUpMbps * 122.07f;
                string downStr = FormatSpeed(downKBps);
                string upStr = FormatSpeed(upKBps);
                x = DrawNetSegment(g, x, y, downStr, upStr);
            }
        }

        private int DrawSegment(Graphics g, int x, int y, string label, string value, string unit, Color valColor)
        {
            using (var b = new SolidBrush(MutedColor))
            {
                g.DrawString(label, _smallFont, b, x, y);
                var lblW = g.MeasureString(label, _smallFont);
                x += (int)lblW.Width + 5;
            }
            using (var b = new SolidBrush(valColor))
            {
                g.DrawString(value, _font, b, x, y - 3);
                var valW = g.MeasureString(value, _font);
                x += (int)valW.Width + 1;
            }
            using (var b = new SolidBrush(MutedColor))
            {
                g.DrawString(unit, _smallFont, b, x, y);
                var unitW = g.MeasureString(unit, _smallFont);
                x += (int)unitW.Width + 9;
            }
            return x;
        }

        private int DrawDivider(Graphics g, int x, int y)
        {
            int lineH = (int)(24 * _scale.Multiplier());
            int top = (int)(3 * _scale.Multiplier());
            using (var p = new Pen(Color.FromArgb(70, 70, 78), 1))
                g.DrawLine(p, x, y - top, x, y + lineH);
            return x + (int)(9 * _scale.Multiplier());
        }

        private int DrawNetSegment(Graphics g, int x, int y, string downStr, string upStr)
        {
            using (var bD = new SolidBrush(DownColor))
            {
                g.DrawString("↓", _font, bD, x, y - 3);
                var aw = g.MeasureString("↓", _font);
                x += (int)aw.Width;
                g.DrawString(downStr, _font, bD, x, y - 3);
                var dw = g.MeasureString(downStr, _font);
                x += (int)dw.Width + 9;
            }
            using (var bU = new SolidBrush(UpColor))
            {
                g.DrawString("↑", _font, bU, x, y - 3);
                var aw = g.MeasureString("↑", _font);
                x += (int)aw.Width;
                g.DrawString(upStr, _font, bU, x, y - 3);
                var uw = g.MeasureString(upStr, _font);
                x += (int)uw.Width + 6;
            }
            return x;
        }

        private static string FormatSpeed(float kbps)
        {
            if (kbps >= 1024f)
                return (kbps / 1024f).ToString("F1") + "M";
            return kbps.ToString("F0") + "K";
        }

        /// <summary>
        /// Apply the correct Win32 styles for the current mode:
        /// - Float / Fixed: normal window — receives mouse clicks, drag,
        ///   and right-click for the menu.
        /// - Overlay: click-through (WS_EX_TRANSPARENT) so it never blocks
        ///   other tray icons or taskbar interaction; only reachable again
        ///   via the tray icon's own menu.
        /// Colorkey transparency is always applied so the background blends
        /// with the taskbar regardless of mode.
        /// </summary>
        private void ApplyInteractionMode()
        {
            int ex = GetWindowLong(this.Handle, GWL_EXSTYLE);
            ex |= WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            if (_mode == MeterMode.Overlay)
                ex |= WS_EX_TRANSPARENT;
            else
                ex &= ~WS_EX_TRANSPARENT;
            SetWindowLong(this.Handle, GWL_EXSTYLE, ex);
            SetLayeredWindowAttributes(this.Handle, TaskbarBg.ToArgb() & 0xFFFFFF, 0, LWA_COLORKEY);
        }

        /// <summary>
        /// Position the meter on the taskbar, just left of the system tray clock.
        /// Only used for the default/reset position — a dragged position
        /// (loaded from settings) overrides this.
        /// </summary>
        public void PositionOnTaskbar()
        {
            try
            {
                var screen = Screen.PrimaryScreen;
                var wa = screen.WorkingArea;
                int taskbarTop = wa.Bottom;
                int taskbarHeight = screen.Bounds.Bottom - taskbarTop;
                int y = taskbarTop + (taskbarHeight - this.Height) / 2;
                if (y < taskbarTop + 1) y = taskbarTop + 1;
                int x = wa.Right - this.Width - 170;
                this.Location = new Point(x, y);
            }
            catch
            {
                var wa = Screen.PrimaryScreen.WorkingArea;
                this.Location = new Point(wa.Right - this.Width - 170, wa.Bottom - this.Height - 2);
            }
        }

        public void UpdateStats(SystemStats s)
        {
            _last = s;
            this.Invalidate();
        }

        /// <summary>
        /// Force this window back above the taskbar's z-order.
        /// Windows' shell (explorer.exe) re-asserts its own topmost taskbar
        /// band whenever the user clicks the taskbar or opens Start/tray
        /// flyouts, which silently drops any other "always on top" window
        /// behind it. Calling SetWindowPos with HWND_TOPMOST on every poll
        /// tick re-pins us above the shell so the meter never disappears
        /// behind the taskbar after a click.
        /// </summary>
        public void KeepOnTop()
        {
            if (!this.IsHandleCreated || this.IsDisposed) return;
            try
            {
                SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            catch { }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (this.Visible) ApplyInteractionMode();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_topmostTimer != null) { _topmostTimer.Stop(); _topmostTimer.Dispose(); }
                if (_menu != null) _menu.Dispose();
                if (_font != null) _font.Dispose();
                if (_smallFont != null) _smallFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
