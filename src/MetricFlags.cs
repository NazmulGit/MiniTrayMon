using System;

namespace MiniTrayMon
{
    /// <summary>
    /// Which resource metrics are currently enabled for display, shared between
    /// the tray icon (IconRenderer) and the taskbar speed meter (TaskbarSpeedMeter).
    /// Bitwise flags so any combination can be shown/hidden independently.
    /// </summary>
    [Flags]
    public enum MetricFlags
    {
        None = 0,
        Cpu = 1,
        Ram = 2,
        Disk = 4,
        Net = 8,
        All = Cpu | Ram | Disk | Net
    }

    public static class MetricFlagsExtensions
    {
        public static bool Has(this MetricFlags flags, MetricFlags flag)
        {
            return (flags & flag) == flag;
        }

        /// <summary>
        /// Count how many metrics are enabled — used to size layouts dynamically.
        /// </summary>
        public static int Count(this MetricFlags flags)
        {
            int n = 0;
            if (flags.Has(MetricFlags.Cpu)) n++;
            if (flags.Has(MetricFlags.Ram)) n++;
            if (flags.Has(MetricFlags.Disk)) n++;
            if (flags.Has(MetricFlags.Net)) n++;
            return n;
        }

        public static string ToSettingString(this MetricFlags flags)
        {
            return ((int)flags).ToString();
        }

        public static MetricFlags FromSettingString(string s)
        {
            int v;
            if (int.TryParse(s, out v)) return (MetricFlags)v;
            return MetricFlags.All;
        }
    }

    /// <summary>
    /// How the taskbar speed meter behaves with respect to mouse input.
    /// </summary>
    public enum MeterMode
    {
        /// <summary>Draggable with the mouse, right-click for its menu. Default.</summary>
        Float,
        /// <summary>Position locked (no drag) but still clickable — right-click still works.</summary>
        Fixed,
        /// <summary>Click-through overlay — mouse passes straight to the taskbar beneath.
        /// Only reachable via the tray icon's own right-click menu.</summary>
        Overlay
    }

    /// <summary>
    /// Overall "look size" for both the tray icon and the taskbar speed meter.
    /// A single shared scale so increasing/decreasing size affects everything
    /// consistently rather than having separate icon/meter size settings.
    /// </summary>
    public enum UiScale
    {
        Small,
        Medium,
        Large,
        ExtraLarge
    }

    public static class UiScaleExtensions
    {
        /// <summary>Multiplier applied to base font sizes / dimensions.</summary>
        public static float Multiplier(this UiScale scale)
        {
            switch (scale)
            {
                case UiScale.Small: return 0.75f;
                case UiScale.Large: return 1.3f;
                case UiScale.ExtraLarge: return 1.6f;
                default: return 1.0f; // Medium
            }
        }

        public static UiScale Increase(this UiScale scale)
        {
            switch (scale)
            {
                case UiScale.Small: return UiScale.Medium;
                case UiScale.Medium: return UiScale.Large;
                case UiScale.Large: return UiScale.ExtraLarge;
                default: return UiScale.ExtraLarge; // already max
            }
        }

        public static UiScale Decrease(this UiScale scale)
        {
            switch (scale)
            {
                case UiScale.ExtraLarge: return UiScale.Large;
                case UiScale.Large: return UiScale.Medium;
                case UiScale.Medium: return UiScale.Small;
                default: return UiScale.Small; // already min
            }
        }
    }
}
