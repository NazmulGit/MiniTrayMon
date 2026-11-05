using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace MiniTrayMon
{
    /// <summary>
    /// Main tray application. Owns the NotifyIcon, StatsCollector, timer,
    /// and orchestrates icon rendering + menu updates.
    /// </summary>
    public class TrayApp : Form
    {
        private const string AppName = "MiniTrayMon";
        private const string Version = "1.5.1";
        private const int PollIntervalMs = 1500;

        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly StatsCollector _collector;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly ToolStripMenuItem _modeNumericRowItem, _modeVerticalBarsItem;
        private readonly ToolStripMenuItem _cpuItem, _ramItem, _diskItem, _netItem, _overallItem;
        private readonly ToolStripMenuItem _metricCpuItem, _metricRamItem, _metricDiskItem, _metricNetItem;
        private readonly ToolStripMenuItem _meterModeFloatItem, _meterModeFixedItem, _meterModeOverlayItem;
        private readonly ToolStripMenuItem _meterModeMenu;
        private readonly ToolStripMenuItem _sizeSmallItem, _sizeMediumItem, _sizeLargeItem, _sizeXLItem;
        private readonly ToolStripMenuItem _autoStartItem;

        private DisplayMode _mode = DisplayMode.NumericRow;
        private MetricFlags _visibleMetrics = MetricFlags.All;
        private UiScale _scale = UiScale.Medium;
        private SystemStats _lastStats;
        private OverlayForm _overlay;
        private TaskbarSpeedMeter _speedMeter;
        private ToolStripMenuItem _speedMeterToggleItem;

        public TrayApp()
        {
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.Visible = false;
            this.FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
            };

            _collector = new StatsCollector();

            // Tray icon
            _tray = new NotifyIcon
            {
                Icon = Properties.PlaceholderIcon(),
                Visible = true,
                Text = AppName + " " + Version
            };

            // Menu
            _menu = new ContextMenuStrip();
            _menu.Items.Add("Show live overlay", null, (s, e) => ShowOverlay());
            _menu.Items.Add(new ToolStripSeparator());

            // Display mode submenu
            var modeMenu = new ToolStripMenuItem("Display mode");
            _modeNumericRowItem = new ToolStripMenuItem("Numeric Row", null, (s, e) => SetMode(DisplayMode.NumericRow));
            _modeVerticalBarsItem = new ToolStripMenuItem("Vertical Bars", null, (s, e) => SetMode(DisplayMode.VerticalBars));
            modeMenu.DropDownItems.AddRange(new ToolStripItem[] { _modeNumericRowItem, _modeVerticalBarsItem });
            _menu.Items.Add(modeMenu);

            // Metric visibility submenu — check/uncheck to show only what you want
            // (e.g. select CPU only -> tray icon + speed meter show CPU alone).
            var metricsMenu = new ToolStripMenuItem("Show metrics");
            _metricCpuItem = new ToolStripMenuItem("CPU", null, (s, e) => ToggleMetric(MetricFlags.Cpu)) { CheckOnClick = false };
            _metricRamItem = new ToolStripMenuItem("RAM", null, (s, e) => ToggleMetric(MetricFlags.Ram)) { CheckOnClick = false };
            _metricDiskItem = new ToolStripMenuItem("Disk", null, (s, e) => ToggleMetric(MetricFlags.Disk)) { CheckOnClick = false };
            _metricNetItem = new ToolStripMenuItem("Network", null, (s, e) => ToggleMetric(MetricFlags.Net)) { CheckOnClick = false };
            metricsMenu.DropDownItems.AddRange(new ToolStripItem[] { _metricCpuItem, _metricRamItem, _metricDiskItem, _metricNetItem });
            _menu.Items.Add(metricsMenu);

            // Size submenu — scales both the tray icon and the taskbar speed
            // meter together as one shared "look size" setting.
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

            // Auto-start toggle — writes/removes a per-user Windows Run
            // registry key so the app launches automatically at login.
            _autoStartItem = new ToolStripMenuItem("Start with Windows", null, (s, e) => ToggleAutoStart());
            _menu.Items.Add(_autoStartItem);
            _menu.Items.Add(new ToolStripSeparator());

            // Speed meter toggle (taskbar overlay)
            _speedMeterToggleItem = new ToolStripMenuItem("Show speed meter on taskbar", null, (s, e) => ToggleSpeedMeter());
            _menu.Items.Add(_speedMeterToggleItem);

            // Meter position mode — mirrors the meter's own right-click menu
            // so it's reachable even while the meter is click-through (Overlay).
            _meterModeMenu = new ToolStripMenuItem("Speed meter position mode") { Enabled = false };
            _meterModeFloatItem = new ToolStripMenuItem("Float (drag to move)", null, (s, e) => SetMeterMode(MeterMode.Float));
            _meterModeFixedItem = new ToolStripMenuItem("Fixed (locked position)", null, (s, e) => SetMeterMode(MeterMode.Fixed));
            _meterModeOverlayItem = new ToolStripMenuItem("Overlay (click-through)", null, (s, e) => SetMeterMode(MeterMode.Overlay));
            _meterModeMenu.DropDownItems.AddRange(new ToolStripItem[] { _meterModeFloatItem, _meterModeFixedItem, _meterModeOverlayItem });
            _menu.Items.Add(_meterModeMenu);
            _menu.Items.Add(new ToolStripMenuItem("Reset speed meter position", null, (s, e) => ResetMeterPosition()));
            _menu.Items.Add(new ToolStripSeparator());

            _cpuItem = new ToolStripMenuItem("CPU") { Enabled = false };
            _ramItem = new ToolStripMenuItem("Memory") { Enabled = false };
            _diskItem = new ToolStripMenuItem("Disk") { Enabled = false };
            _netItem = new ToolStripMenuItem("Network") { Enabled = false };
            _overallItem = new ToolStripMenuItem("Overall") { Enabled = false };
            _menu.Items.Add(_cpuItem);
            _menu.Items.Add(_ramItem);
            _menu.Items.Add(_diskItem);
            _menu.Items.Add(_netItem);
            _menu.Items.Add(_overallItem);
            _menu.Items.Add(new ToolStripSeparator());

            _menu.Items.Add("About", null, (s, e) =>
                MessageBox.Show(AppName + " " + Version + "\nLightweight system tray monitor\n\nCPU / RAM / Disk / Network",
                    "About", MessageBoxButtons.OK, MessageBoxIcon.Information));
            _menu.Items.Add("Exit", null, (s, e) => Quit());

            _tray.ContextMenuStrip = _menu;
            _tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowOverlay(); };
            _tray.MouseMove += (s, e) => UpdateMenuText();

            // Timer
            _timer = new System.Windows.Forms.Timer { Interval = PollIntervalMs };
            _timer.Tick += (s, e) => Poll();
            _timer.Start();

            // Restore saved display mode, enabled metrics, and size scale —
            // these three plus meter mode/position form one saved "look"
            // profile so the app reopens exactly as it was left.
            SetMode(Properties.GetMode());
            SetVisibleMetrics(Properties.GetVisibleMetrics());
            SetScale(Properties.GetUiScale());

            // Reflect the real current registry state, not a cached setting,
            // so the checkbox never lies if the user removed the entry some
            // other way (e.g. Task Manager's Startup tab).
            _autoStartItem.Checked = Properties.GetAutoStartEnabled();

            // Restore speed meter visibility if it was enabled last session
            if (Properties.GetSpeedMeterVisible())
            {
                ToggleSpeedMeter();
            }

            // first poll after short delay so counters have a baseline
            var warmup = new System.Windows.Forms.Timer { Interval = 600 };
            warmup.Tick += (s, e) => { Poll(); warmup.Stop(); warmup.Dispose(); };
            warmup.Start();
        }

        private void SetMode(DisplayMode m)
        {
            _mode = m;
            _modeNumericRowItem.Checked = (m == DisplayMode.NumericRow);
            _modeVerticalBarsItem.Checked = (m == DisplayMode.VerticalBars);
            try { Properties.SetMode(m); } catch { }
            if (_lastStats != null) RenderIcon();
        }

        /// <summary>
        /// Flip one metric's visibility on/off, keeping at least CPU enabled
        /// so the tray icon never goes completely blank by accident.
        /// </summary>
        private void ToggleMetric(MetricFlags metric)
        {
            MetricFlags next = _visibleMetrics.Has(metric)
                ? (_visibleMetrics & ~metric)
                : (_visibleMetrics | metric);

            if (next == MetricFlags.None)
                return; // refuse to hide everything

            SetVisibleMetrics(next);
        }

        private void SetVisibleMetrics(MetricFlags flags)
        {
            _visibleMetrics = flags;
            _metricCpuItem.Checked = flags.Has(MetricFlags.Cpu);
            _metricRamItem.Checked = flags.Has(MetricFlags.Ram);
            _metricDiskItem.Checked = flags.Has(MetricFlags.Disk);
            _metricNetItem.Checked = flags.Has(MetricFlags.Net);

            try { Properties.SetVisibleMetrics(flags); } catch { }

            if (_speedMeter != null) _speedMeter.SetVisibleMetrics(flags);
            if (_lastStats != null) RenderIcon();
        }

        /// <summary>
        /// Change the shared "look size" — applies to both the tray icon
        /// and the taskbar speed meter (if shown) together, since size is
        /// meant to feel like one setting rather than two separate ones.
        /// </summary>
        private void SetScale(UiScale scale)
        {
            _scale = scale;
            _sizeSmallItem.Checked = (scale == UiScale.Small);
            _sizeMediumItem.Checked = (scale == UiScale.Medium);
            _sizeLargeItem.Checked = (scale == UiScale.Large);
            _sizeXLItem.Checked = (scale == UiScale.ExtraLarge);

            try { Properties.SetUiScale(scale); } catch { }

            if (_speedMeter != null) _speedMeter.SetScale(scale);
            if (_lastStats != null) RenderIcon();
        }

        /// <summary>
        /// Toggle the Windows per-user auto-start registry entry. Reflects
        /// the real outcome in the checkbox — if the registry write fails
        /// (e.g. permissions), the checkbox is reverted and the user is
        /// told rather than silently lying about the state.
        /// </summary>
        private void ToggleAutoStart()
        {
            bool wantEnabled = !_autoStartItem.Checked;
            bool ok = Properties.SetAutoStartEnabled(wantEnabled);
            if (ok)
            {
                _autoStartItem.Checked = wantEnabled;
            }
            else
            {
                MessageBox.Show(
                    "Could not update the Windows startup entry. Try running as your normal user account (no elevation needed) and check registry permissions for HKCU.",
                    "Auto-start", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Poll()
        {
            try
            {
                _lastStats = _collector.Next();
                RenderIcon();
                UpdateMenuText();
                if (_overlay != null && !_overlay.IsDisposed && _overlay.Visible)
                    _overlay.UpdateStats(_lastStats);
                if (_speedMeter != null && !_speedMeter.IsDisposed && _speedMeter.Visible)
                    _speedMeter.UpdateStats(_lastStats);
                // Debug log for verification (one line per poll, rotated)
                try { LogStats(_lastStats); } catch { }
            }
            catch (Exception ex)
            {
                _tray.Text = AppName + " error: " + ex.Message;
            }
        }

        private static readonly string LogPath =
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "minitraymon.log");

        private void LogStats(SystemStats s)
        {
            string line = string.Format("{0:HH:mm:ss}|CPU={1:F1}|RAM={2:F1}|DSK={3:F1}|NET_D={4:F2}|NET_U={5:F2}|OVL={6:F1}",
                s.Timestamp, s.CpuPercent, s.RamPercent, s.DiskPercent, s.NetDownMbps, s.NetUpMbps, s.OverallPercent);
            // Keep only last 50 lines to avoid unbounded growth
            try
            {
                var lines = new System.Collections.Generic.List<string>();
                if (System.IO.File.Exists(LogPath))
                    lines.AddRange(System.IO.File.ReadAllLines(LogPath));
                lines.Add(line);
                if (lines.Count > 50) lines.RemoveRange(0, lines.Count - 50);
                System.IO.File.WriteAllLines(LogPath, lines);
            }
            catch { }
        }

        private void RenderIcon()
        {
            if (_lastStats == null) return;
            var oldIcon = _tray.Icon;
            _tray.Icon = IconRenderer.Render(_lastStats, _mode, _visibleMetrics, _scale);
            // The previous icon handle is owned by us now; destroy to avoid leak.
            if (oldIcon != null)
            {
                try { NativeMethods.DestroyIcon(oldIcon.Handle); } catch { }
                oldIcon.Dispose();
            }
        }

        private void UpdateMenuText()
        {
            if (_lastStats == null) return;
            var s = _lastStats;
            _cpuItem.Text = "  CPU:      " + s.CpuPercent.ToString("F1") + " %";
            _ramItem.Text = "  Memory:   " + s.RamPercent.ToString("F0") + " %  (" +
                            s.RamUsedGB.ToString("F1") + " GB used)";
            _diskItem.Text = "  Disk:     " + s.DiskPercent.ToString("F0") + " %  (" +
                             s.DiskTotalMbps.ToString("F1") + " Mbps)";
            _netItem.Text = "  Network:  v " + s.NetDownMbps.ToString("F1") + "  ^ " +
                            s.NetUpMbps.ToString("F1") + " Mbps";
            _overallItem.Text = "  Overall:  " + s.OverallPercent.ToString("F1") + " %";
        }

        private void ShowOverlay()
        {
            if (_overlay == null || _overlay.IsDisposed)
            {
                _overlay = new OverlayForm();
                _overlay.FormClosed += (s, e) => _overlay = null;
            }
            if (_lastStats != null) _overlay.UpdateStats(_lastStats);
            _overlay.Show();
            _overlay.BringToFront();
        }

        /// <summary>
        /// Show or hide the always-on-top speed meter on the taskbar.
        /// Persisted so it auto-shows on next launch if enabled. Restores
        /// saved drag position and interaction mode, and applies the current
        /// visible-metrics selection. Also wires the meter's own right-click
        /// menu events (mode change, metric toggle, reset, hide) back into
        /// this app so both menus (tray icon + meter itself) stay in sync.
        /// </summary>
        private void ToggleSpeedMeter()
        {
            if (_speedMeter == null)
            {
                _speedMeter = new TaskbarSpeedMeter();
                _speedMeter.FormClosed += (s, e) =>
                {
                    _speedMeter = null;
                    UpdateSpeedMeterMenu();
                    _meterModeMenu.Enabled = false;
                };
                _speedMeter.PositionChanged += p => { try { Properties.SetMeterPosition(p); } catch { } };
                _speedMeter.ModeChanged += m =>
                {
                    UpdateMeterModeMenu(m);
                    try { Properties.SetMeterMode(m); } catch { }
                };
                _speedMeter.VisibleMetricsChanged += flags =>
                {
                    // Keep the tray icon's own "Show metrics" checkmarks and
                    // the tray icon rendering in sync with a change made from
                    // the meter's own right-click menu.
                    _visibleMetrics = flags;
                    _metricCpuItem.Checked = flags.Has(MetricFlags.Cpu);
                    _metricRamItem.Checked = flags.Has(MetricFlags.Ram);
                    _metricDiskItem.Checked = flags.Has(MetricFlags.Disk);
                    _metricNetItem.Checked = flags.Has(MetricFlags.Net);
                    try { Properties.SetVisibleMetrics(flags); } catch { }
                    if (_lastStats != null) RenderIcon();
                };
                _speedMeter.ResetPositionRequested += ResetMeterPosition;
                _speedMeter.HideRequested += ToggleSpeedMeter;
                _speedMeter.ScaleChanged += sc =>
                {
                    _scale = sc;
                    _sizeSmallItem.Checked = (sc == UiScale.Small);
                    _sizeMediumItem.Checked = (sc == UiScale.Medium);
                    _sizeLargeItem.Checked = (sc == UiScale.Large);
                    _sizeXLItem.Checked = (sc == UiScale.ExtraLarge);
                    try { Properties.SetUiScale(sc); } catch { }
                    if (_lastStats != null) RenderIcon();
                };

                MeterMode mode = Properties.GetMeterMode();
                _speedMeter.SetMode(mode);
                UpdateMeterModeMenu(mode);
                _meterModeMenu.Enabled = true;

                _speedMeter.SetVisibleMetrics(_visibleMetrics);
                _speedMeter.SetScale(_scale);

                var savedPos = Properties.GetMeterPosition();
                if (savedPos.HasValue)
                    _speedMeter.Location = savedPos.Value;
                else
                    _speedMeter.PositionOnTaskbar();

                _speedMeter.Show();
                if (_lastStats != null) _speedMeter.UpdateStats(_lastStats);
            }
            else
            {
                _speedMeter.Close();
                _speedMeter = null;
                _meterModeMenu.Enabled = false;
            }
            UpdateSpeedMeterMenu();
            try { Properties.SetSpeedMeterVisible(_speedMeter != null); } catch { }
        }

        /// <summary>
        /// Switch the meter's interaction mode from the tray icon's own menu
        /// (mirrors what's reachable by right-clicking the meter directly —
        /// useful when the meter is in Overlay/click-through mode and can't
        /// be right-clicked itself).
        /// </summary>
        private void SetMeterMode(MeterMode mode)
        {
            if (_speedMeter == null) return;
            _speedMeter.SetMode(mode);
            UpdateMeterModeMenu(mode);
            try { Properties.SetMeterMode(mode); } catch { }
        }

        private void UpdateMeterModeMenu(MeterMode mode)
        {
            _meterModeFloatItem.Checked = (mode == MeterMode.Float);
            _meterModeFixedItem.Checked = (mode == MeterMode.Fixed);
            _meterModeOverlayItem.Checked = (mode == MeterMode.Overlay);
        }

        /// <summary>
        /// Snap the speed meter back to its default taskbar-anchored spot
        /// and forget any custom dragged position.
        /// </summary>
        private void ResetMeterPosition()
        {
            try { Properties.ClearMeterPosition(); } catch { }
            if (_speedMeter != null) _speedMeter.PositionOnTaskbar();
        }

        private void UpdateSpeedMeterMenu()
        {
            if (_speedMeterToggleItem != null)
                _speedMeterToggleItem.Checked = (_speedMeter != null);
        }

        private void Quit()
        {
            _timer.Stop();
            if (_speedMeter != null) _speedMeter.Close();
            _tray.Visible = false;
            Application.Exit();
        }

        // The main form is only a message host for the tray icon: it must never be
        // shown. Application.Run() calls SetVisibleCore(true); the old code passed that
        // through while the handle did not exist yet, so Windows showed the form as a
        // minimized caption bar above the taskbar that could not be closed (v1.5.1 fix).
        protected override void SetVisibleCore(bool value)
        {
            if (!IsHandleCreated) CreateHandle();
            base.SetVisibleCore(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
                if (_speedMeter != null) { _speedMeter.Close(); _speedMeter.Dispose(); }
                _tray.Dispose();
                _collector.Dispose();
            }
            base.Dispose(disposing);
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
            public static extern bool DestroyIcon(IntPtr handle);
        }
    }
}
