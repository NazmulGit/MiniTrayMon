using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MiniTrayMon
{
    /// <summary>
    /// Helpers for app-level state: default icon, persisted settings.
    /// Stored as simple key=value lines in a settings file next to the exe
    /// for zero-dependency persistence.
    /// </summary>
    internal static class Properties
    {
        private static readonly string SettingsPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "minitraymon.settings");

        // Setting keys
        private const string KeyDisplayMode = "DisplayMode";
        private const string KeySpeedMeterVisible = "SpeedMeterVisible";
        private const string KeyVisibleMetrics = "VisibleMetrics";
        private const string KeyMeterX = "MeterX";
        private const string KeyMeterY = "MeterY";
        private const string KeyMeterLocked = "MeterLocked"; // legacy bool, kept for back-compat
        private const string KeyMeterMode = "MeterMode";      // new: Float / Fixed / Overlay
        private const string KeyUiScale = "UiScale";          // Small / Medium / Large / ExtraLarge

        /// <summary>
        /// Plain dark icon used before first stats sample arrives.
        /// </summary>
        public static Icon PlaceholderIcon()
        {
            using (var bmp = new Bitmap(16, 16))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(25, 25, 30));
                using (var f = new Font("Arial", 7, FontStyle.Bold))
                using (var b = new SolidBrush(Color.White))
                    g.DrawString("M", f, b, 3, 2);
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        // -------- settings read/write helpers --------

        private static Dictionary<string, string> LoadSettings()
        {
            var dict = new Dictionary<string, string>();
            try
            {
                if (File.Exists(SettingsPath))
                {
                    foreach (var line in File.ReadAllLines(SettingsPath))
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0)
                        {
                            string k = line.Substring(0, eq).Trim();
                            string v = line.Substring(eq + 1).Trim();
                            dict[k] = v;
                        }
                    }
                }
            }
            catch { }
            return dict;
        }

        private static void SaveSettings(Dictionary<string, string> dict)
        {
            try
            {
                var lines = new List<string>();
                foreach (var kv in dict)
                    lines.Add(kv.Key + "=" + kv.Value);
                File.WriteAllLines(SettingsPath, lines);
            }
            catch { /* non-fatal */ }
        }

        // -------- Display Mode --------

        public static DisplayMode GetMode()
        {
            var dict = LoadSettings();
            string val;
            if (dict.TryGetValue(KeyDisplayMode, out val))
            {
                foreach (var name in Enum.GetNames(typeof(DisplayMode)))
                    if (val.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return (DisplayMode)Enum.Parse(typeof(DisplayMode), name, true);
            }
            return DisplayMode.NumericRow;
        }

        public static void SetMode(DisplayMode m)
        {
            var dict = LoadSettings();
            dict[KeyDisplayMode] = m.ToString();
            SaveSettings(dict);
        }

        // -------- Speed Meter visibility --------

        public static bool GetSpeedMeterVisible()
        {
            var dict = LoadSettings();
            string val;
            if (dict.TryGetValue(KeySpeedMeterVisible, out val))
            {
                bool b;
                if (bool.TryParse(val, out b)) return b;
            }
            return false;
        }

        public static void SetSpeedMeterVisible(bool visible)
        {
            var dict = LoadSettings();
            dict[KeySpeedMeterVisible] = visible.ToString();
            SaveSettings(dict);
        }

        // -------- Visible metrics (per-metric show/hide) --------

        public static MetricFlags GetVisibleMetrics()
        {
            var dict = LoadSettings();
            string val;
            if (dict.TryGetValue(KeyVisibleMetrics, out val))
                return MetricFlagsExtensions.FromSettingString(val);
            return MetricFlags.All;
        }

        public static void SetVisibleMetrics(MetricFlags flags)
        {
            var dict = LoadSettings();
            dict[KeyVisibleMetrics] = flags.ToSettingString();
            SaveSettings(dict);
        }

        // -------- Speed meter position + lock --------

        /// <summary>
        /// Returns the saved meter position, or null if none was saved yet
        /// (meaning the meter should use its default taskbar-anchored spot).
        /// </summary>
        public static Point? GetMeterPosition()
        {
            var dict = LoadSettings();
            string xs, ys;
            if (dict.TryGetValue(KeyMeterX, out xs) && dict.TryGetValue(KeyMeterY, out ys))
            {
                int x, y;
                if (int.TryParse(xs, out x) && int.TryParse(ys, out y))
                    return new Point(x, y);
            }
            return null;
        }

        public static void SetMeterPosition(Point p)
        {
            var dict = LoadSettings();
            dict[KeyMeterX] = p.X.ToString();
            dict[KeyMeterY] = p.Y.ToString();
            SaveSettings(dict);
        }

        public static void ClearMeterPosition()
        {
            var dict = LoadSettings();
            dict.Remove(KeyMeterX);
            dict.Remove(KeyMeterY);
            SaveSettings(dict);
        }

        public static bool GetMeterLocked()
        {
            var dict = LoadSettings();
            string val;
            if (dict.TryGetValue(KeyMeterLocked, out val))
            {
                bool b;
                if (bool.TryParse(val, out b)) return b;
            }
            return false;
        }

        public static void SetMeterLocked(bool locked)
        {
            var dict = LoadSettings();
            dict[KeyMeterLocked] = locked.ToString();
            SaveSettings(dict);
        }

        // -------- Speed meter interaction mode (Float / Fixed / Overlay) --------

        /// <summary>
        /// Float = draggable + right-clickable, moves freely.
        /// Fixed = position locked (no drag) but still clickable for the
        ///         right-click menu.
        /// Overlay = click-through (old "locked" behavior) — mouse passes
        ///           straight to the taskbar beneath; only way back in is
        ///           via the tray icon's right-click menu.
        /// Defaults to Float so a fresh install is immediately interactive.
        /// </summary>
        public static MeterMode GetMeterMode()
        {
            var dict = LoadSettings();
            string val;
            if (dict.TryGetValue(KeyMeterMode, out val))
            {
                foreach (var name in Enum.GetNames(typeof(MeterMode)))
                    if (val.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return (MeterMode)Enum.Parse(typeof(MeterMode), name, true);
            }
            // Back-compat: honor the old bool if MeterMode was never saved.
            if (dict.TryGetValue(KeyMeterLocked, out val))
            {
                bool oldLocked;
                if (bool.TryParse(val, out oldLocked))
                    return oldLocked ? MeterMode.Overlay : MeterMode.Float;
            }
            return MeterMode.Float;
        }

        public static void SetMeterMode(MeterMode mode)
        {
            var dict = LoadSettings();
            dict[KeyMeterMode] = mode.ToString();
            SaveSettings(dict);
        }

        // -------- Overall size / scale (shared by tray icon + speed meter) --------

        public static UiScale GetUiScale()
        {
            var dict = LoadSettings();
            string val;
            if (dict.TryGetValue(KeyUiScale, out val))
            {
                foreach (var name in Enum.GetNames(typeof(UiScale)))
                    if (val.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return (UiScale)Enum.Parse(typeof(UiScale), name, true);
            }
            return UiScale.Medium;
        }

        public static void SetUiScale(UiScale scale)
        {
            var dict = LoadSettings();
            dict[KeyUiScale] = scale.ToString();
            SaveSettings(dict);
        }

        // -------- Windows auto-start (HKCU\...\Run registry key) --------

        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "MiniTrayMon";

        /// <summary>
        /// True if a per-user auto-start entry for this exe currently exists
        /// in the registry. Reads live from the registry rather than a saved
        /// setting, so it always reflects reality even if the user removed
        /// it some other way (e.g. Task Manager's Startup tab).
        /// </summary>
        public static bool GetAutoStartEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    object val = key.GetValue(RunValueName);
                    return val != null;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Enable or disable launching this exe automatically when the
        /// current Windows user logs in, via the standard per-user Run key
        /// (no admin rights required; scoped to HKCU, not machine-wide).
        /// </summary>
        public static bool SetAutoStartEnabled(bool enabled)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
                                  ?? Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null) return false;
                    if (enabled)
                    {
                        string exePath = Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue(RunValueName, "\"" + exePath + "\"");
                    }
                    else
                    {
                        if (key.GetValue(RunValueName) != null)
                            key.DeleteValue(RunValueName, false);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
