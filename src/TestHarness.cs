using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace MiniTrayMon
{
    /// <summary>
    /// Standalone test harness — builds a SystemStats snapshot, renders
    /// the icon in all 3 modes, and saves each as a PNG for visual inspection.
    /// Does NOT use PerformanceCounters — uses a fixed sample for reproducibility.
    /// </summary>
    internal static class TestHarness
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string outDir = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

            // Sample stats similar to a moderate-load system
            var s = new SystemStats
            {
                CpuPercent = 42,
                RamPercent = 68,
                RamUsedGB = 21.5f,
                RamTotalGB = 31.9f,
                RamFreeGB = 10.4f,
                DiskPercent = 15,
                DiskReadKBps = 1200,
                DiskWriteKBps = 800,
                DiskTotalMbps = 15.6f,
                NetUpMbps = 0.3f,
                NetDownMbps = 6.8f,
                Timestamp = DateTime.Now
            };

            Console.WriteLine("Sample stats:");
            Console.WriteLine("  CPU      : " + s.CpuPercent + " %");
            Console.WriteLine("  Memory   : " + s.RamPercent + " %  (" + s.RamUsedGB + " / " + s.RamTotalGB + " GB)");
            Console.WriteLine("  Disk     : " + s.DiskPercent + " %  (" + s.DiskTotalMbps + " Mbps)");
            Console.WriteLine("  Network  : down " + s.NetDownMbps + "  up " + s.NetUpMbps + " Mbps");
            Console.WriteLine("  Overall  : " + s.OverallPercent + " %");
            Console.WriteLine();

            // Render all modes with all metrics enabled at Medium scale
            foreach (DisplayMode mode in Enum.GetValues(typeof(DisplayMode)))
            {
                using (var icon = IconRenderer.Render(s, mode, MetricFlags.All, UiScale.Medium))
                {
                    // Convert icon to bitmap for saving as PNG
                    using (var bmp = icon.ToBitmap())
                    {
                        // Scale up 4x for visibility
                        int scale = 4;
                        using (var big = new Bitmap(IconRenderer.IconSize * scale, IconRenderer.IconSize * scale))
                        using (var g = Graphics.FromImage(big))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                            g.DrawImage(bmp, 0, 0, big.Width, big.Height);
                            string path = Path.Combine(outDir, "preview_" + mode + ".png");
                            big.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                            Console.WriteLine("Saved: " + path);
                        }
                    }
                }
            }

            // Render CPU-only in both modes, to verify per-metric filtering
            var cpuOnly = MetricFlags.Cpu;
            foreach (DisplayMode mode in Enum.GetValues(typeof(DisplayMode)))
            {
                using (var icon = IconRenderer.Render(s, mode, cpuOnly, UiScale.Medium))
                using (var bmp = icon.ToBitmap())
                {
                    int scale = 4;
                    using (var big = new Bitmap(IconRenderer.IconSize * scale, IconRenderer.IconSize * scale))
                    using (var g = Graphics.FromImage(big))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        g.DrawImage(bmp, 0, 0, big.Width, big.Height);
                        string path = Path.Combine(outDir, "preview_CpuOnly_" + mode + ".png");
                        big.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("Saved: " + path);
                    }
                }
            }

            // Render all 4 UiScale levels (NumericRow, all metrics) to verify
            // the size-scaling feature visually.
            foreach (UiScale scaleLevel in Enum.GetValues(typeof(UiScale)))
            {
                using (var icon = IconRenderer.Render(s, DisplayMode.NumericRow, MetricFlags.All, scaleLevel))
                using (var bmp = icon.ToBitmap())
                {
                    int scale = 4;
                    using (var big = new Bitmap(IconRenderer.IconSize * scale, IconRenderer.IconSize * scale))
                    using (var g = Graphics.FromImage(big))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        g.DrawImage(bmp, 0, 0, big.Width, big.Height);
                        string path = Path.Combine(outDir, "preview_Scale_" + scaleLevel + ".png");
                        big.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                        Console.WriteLine("Saved: " + path);
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("Done. All mode + metric-filter + scale previews saved.");
        }
    }
}
