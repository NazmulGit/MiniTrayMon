using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MiniTrayMon
{
    /// <summary>
    /// Display modes for the tray icon.
    /// Two modes, matching the user's reference images:
    /// 1. NumericRow — numbers on top, labels below (horizontal strip)
    /// 2. VerticalBars — vertical bar graph for each metric
    /// </summary>
    public enum DisplayMode
    {
        /// <summary>Numbers on top row, labels (CPU/RAM/DSK/NET) below.</summary>
        NumericRow,
        /// <summary>Vertical bars, one per metric, filled bottom-up.</summary>
        VerticalBars
    }

    /// <summary>
    /// One resource metric ready to be drawn: its value, label, and color.
    /// Built by IconRenderer.CollectVisible() from a SystemStats snapshot,
    /// filtered down to only the metrics the user has enabled.
    /// </summary>
    internal struct MetricSlot
    {
        public string Label;      // "CPU" / "RAM" / "DSK" / "NET"
        public string ShortLabel; // "C" / "R" / "D" / "N" (VerticalBars mode)
        public int Value;         // 0..100
        public Color Color;
    }

    /// <summary>
    /// Renders the tray icon bitmap from a SystemStats snapshot,
    /// according to the active DisplayMode, enabled MetricFlags, and UiScale.
    /// </summary>
    public static class IconRenderer
    {
        // Base canvas is always 128x128 (Windows scales down for the tray);
        // UiScale only affects font/bar sizing within that canvas so bigger
        // scales render bolder/larger content, not a bigger bitmap (which
        // the tray would just downsample anyway).
        public const int IconSize = 128;

        // Palette — bright colors that pop on both light and dark taskbars
        private static readonly Color CpuColor = Color.FromArgb(80, 220, 120);   // green
        private static readonly Color RamColor = Color.FromArgb(100, 170, 255);   // blue
        private static readonly Color DiskColor = Color.FromArgb(255, 190, 70);  // orange
        private static readonly Color NetColor = Color.FromArgb(210, 130, 255);  // purple
        private static readonly Color TextColor = Color.White;
        private static readonly Color TextOutline = Color.Black;
        private static readonly Color TrackColor = Color.FromArgb(100, 100, 110, 120); // semi-transparent gray
        private static readonly Color LabelColor = Color.FromArgb(200, 200, 210);

        /// <summary>
        /// Build an icon for the tray based on stats + mode + which metrics
        /// are enabled + UI scale. Uses 32bpp ARGB so transparency is
        /// preserved in the HICON.
        /// </summary>
        public static Icon Render(SystemStats s, DisplayMode mode, MetricFlags visible, UiScale scale)
        {
            using (var bmp = new Bitmap(IconSize, IconSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                var slots = CollectVisible(s, visible);
                float mult = scale.Multiplier();

                if (slots.Count == 0)
                {
                    // Nothing enabled — draw a muted placeholder so the tray
                    // icon isn't completely blank/invisible.
                    using (var f = new Font("Arial", 14 * mult, FontStyle.Bold))
                        DrawOutlinedText(g, "—", f, LabelColor, TextOutline, IconSize / 2f - 8, IconSize / 2f - 14);
                }
                else if (mode == DisplayMode.VerticalBars)
                    RenderVerticalBars(g, slots, mult);
                else
                    RenderNumericRow(g, slots, mult);

                IntPtr hIcon = bmp.GetHicon();
                return Icon.FromHandle(hIcon);
            }
        }

        /// <summary>
        /// Convert a SystemStats snapshot into an ordered list of MetricSlot,
        /// keeping only the metrics enabled in <paramref name="visible"/>.
        /// Order is always CPU, RAM, Disk, Net regardless of enable order.
        /// </summary>
        internal static List<MetricSlot> CollectVisible(SystemStats s, MetricFlags visible)
        {
            var list = new List<MetricSlot>(4);
            float netLoad = Math.Min(100, s.NetDownMbps + s.NetUpMbps * 2f);

            if (visible.Has(MetricFlags.Cpu))
                list.Add(new MetricSlot { Label = "CPU", ShortLabel = "C", Value = (int)s.CpuPercent, Color = CpuColor });
            if (visible.Has(MetricFlags.Ram))
                list.Add(new MetricSlot { Label = "RAM", ShortLabel = "R", Value = (int)s.RamPercent, Color = RamColor });
            if (visible.Has(MetricFlags.Disk))
                list.Add(new MetricSlot { Label = "DSK", ShortLabel = "D", Value = (int)s.DiskPercent, Color = DiskColor });
            if (visible.Has(MetricFlags.Net))
                list.Add(new MetricSlot { Label = "NET", ShortLabel = "N", Value = (int)netLoad, Color = NetColor });

            return list;
        }

        // ===================================================================
        // Mode 1: NumericRow — numbers on top, labels below.
        // Layout adapts to however many metrics are enabled (1-4), so a
        // single enabled metric renders as one large centered number.
        // Font sizes scale with the UiScale multiplier.
        // ===================================================================
        private static void RenderNumericRow(Graphics g, List<MetricSlot> slots, float mult)
        {
            int count = slots.Count;
            int cellW = IconSize / count;
            // Bigger font when fewer metrics are shown, so a single metric
            // fills the icon nicely instead of looking tiny. Then scaled by
            // the user's chosen UiScale on top of that.
            float baseNumSize = count == 1 ? 48f : (count == 2 ? 34f : 26f);
            float baseLblSize = count == 1 ? 16f : 12f;
            int numFontSize = Clamp((int)(baseNumSize * mult), 8, 60);
            int lblFontSize = Clamp((int)(baseLblSize * mult), 6, 24);
            int numY = count == 1 ? 30 : 18;
            int lblY = count == 1 ? 92 : 80;

            // At larger scales with 3-4 metrics enabled, the requested font
            // can exceed the cell width (each cell is only IconSize/count
            // pixels wide). Shrink the number font down until the widest
            // possible 3-digit value ("100") fits within the cell, so text
            // never overflows into neighboring segments.
            float maxCellTextWidth = cellW - 6f; // small margin each side
            while (numFontSize > 8)
            {
                SizeF probeSize;
                using (var probeFont = new Font("Consolas", numFontSize, FontStyle.Bold))
                    probeSize = g.MeasureString("100", probeFont);
                if (probeSize.Width <= maxCellTextWidth) break;
                numFontSize--;
            }

            using (var fNum = new Font("Consolas", numFontSize, FontStyle.Bold))
            using (var fLbl = new Font("Arial", lblFontSize, FontStyle.Bold))
            {
                for (int i = 0; i < count; i++)
                {
                    var slot = slots[i];
                    int cx = i * cellW;

                    string numText = slot.Value.ToString();
                    var sz = g.MeasureString(numText, fNum);
                    float nx = cx + (cellW - sz.Width) / 2f;
                    Color numCol = slot.Value > 80 ? Color.OrangeRed : slot.Color;
                    DrawOutlinedText(g, numText, fNum, numCol, TextOutline, nx, numY);

                    var lsz = g.MeasureString(slot.Label, fLbl);
                    float lx = cx + (cellW - lsz.Width) / 2f;
                    DrawOutlinedText(g, slot.Label, fLbl, LabelColor, TextOutline, lx, lblY);

                    if (i > 0)
                    {
                        using (var p = new Pen(Color.FromArgb(80, 80, 90, 100), 1))
                            g.DrawLine(p, cx, 12, cx, IconSize - 12);
                    }
                }
            }
        }

        // ===================================================================
        // Mode 2: VerticalBars — vertical bar graph, one bar per enabled metric.
        // Layout adapts: fewer bars render wider so a single metric fills
        // the icon width nicely. Label/percentage fonts scale with UiScale.
        // ===================================================================
        private static void RenderVerticalBars(Graphics g, List<MetricSlot> slots, float mult)
        {
            int barCount = slots.Count;
            int totalW = IconSize - 16;
            int barW = totalW / barCount - 4;
            int gap = barCount > 1 ? (totalW - barW * barCount) / (barCount - 1) : 0;
            int barAreaTop = 8;
            int barAreaBottom = IconSize - 30;
            int barAreaH = barAreaBottom - barAreaTop;

            int lblFontSize = Clamp((int)(10 * mult), 6, 20);
            int pctFontSize = Clamp((int)(9 * mult), 6, 18);

            // Shrink the percentage font if it would overflow the bar width
            // (bars get narrower as more metrics are shown, and mult can
            // push font size up independently of bar width).
            float maxBarTextWidth = barW - 2f;
            while (pctFontSize > 6)
            {
                SizeF probe;
                using (var probeFont = new Font("Consolas", pctFontSize, FontStyle.Bold))
                    probe = g.MeasureString("100%", probeFont);
                if (probe.Width <= maxBarTextWidth) break;
                pctFontSize--;
            }

            using (var fLbl = new Font("Arial", lblFontSize, FontStyle.Bold))
            using (var fPct = new Font("Consolas", pctFontSize, FontStyle.Bold))
            {
                for (int i = 0; i < barCount; i++)
                {
                    var slot = slots[i];
                    int bx = 8 + i * (barW + gap);
                    float pct = Math.Max(0, Math.Min(100, slot.Value));
                    int fillH = (int)(pct / 100f * barAreaH);

                    using (var track = new SolidBrush(TrackColor))
                        g.FillRectangle(track, bx, barAreaTop, barW, barAreaH);

                    using (var fill = new SolidBrush(slot.Color))
                        g.FillRectangle(fill, bx, barAreaBottom - fillH, barW, fillH);

                    using (var p = new Pen(Color.FromArgb(150, 150, 160), 1))
                        g.DrawRectangle(p, bx, barAreaTop, barW, barAreaH);

                    string pctText = ((int)pct) + "%";
                    var psz = g.MeasureString(pctText, fPct);
                    float px = bx + (barW - psz.Width) / 2f;
                    float py = (fillH > psz.Height + 4)
                        ? barAreaBottom - fillH + 2
                        : barAreaTop;
                    DrawOutlinedText(g, pctText, fPct, TextColor, TextOutline, px, py);

                    var lsz = g.MeasureString(slot.ShortLabel, fLbl);
                    float lx = bx + (barW - lsz.Width) / 2f;
                    DrawOutlinedText(g, slot.ShortLabel, fLbl, slot.Color, TextOutline, lx, IconSize - 22);
                }
            }
        }

        private static int Clamp(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }

        /// <summary>
        /// Draw text with a dark outline around it so it's readable on any
        /// background color (light or dark taskbar).
        /// </summary>
        private static void DrawOutlinedText(Graphics g, string text, Font font, Color fill, Color outline, float x, float y)
        {
            var gp = new GraphicsPath();
            gp.AddString(text, font.FontFamily, (int)font.Style, font.Size, new PointF(x, y), StringFormat.GenericDefault);

            using (var pen = new Pen(outline, 3f) { LineJoin = LineJoin.Round })
                g.DrawPath(pen, gp);
            using (var brush = new SolidBrush(fill))
                g.FillPath(brush, gp);
            gp.Dispose();
        }
    }
}
