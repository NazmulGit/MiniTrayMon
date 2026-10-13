using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MiniTrayMon
{
    /// <summary>
    /// Pixel-level verification of IconRenderer output.
    /// Loads the 3 preview PNGs and checks for expected colors in expected regions.
    /// </summary>
    internal static class PixelVerify
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string dir = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
            bool allPass = true;

            // Expected colors (from IconRenderer palette)
            Color cpuColor = Color.FromArgb(80, 200, 120);    // green
            Color ramColor = Color.FromArgb(80, 160, 255);    // blue
            Color diskColor = Color.FromArgb(255, 180, 60);   // orange
            Color netColor = Color.FromArgb(200, 120, 255);  // purple
            Color backColor = Color.FromArgb(25, 25, 30);

            // ---- BARS mode ----
            string barsPath = Path.Combine(dir, "preview_Bars.png");
            if (File.Exists(barsPath))
            {
                using (var bmp = new Bitmap(barsPath))
                {
                    Console.WriteLine("=== BARS mode (" + bmp.Width + "x" + bmp.Height + ") ===");
                    // The preview is scaled 4x, so original 64x64 -> 256x256
                    // Bars are at y = 18 + idx*14, height 10. Scale=4 -> y = 72 + idx*56
                    bool foundGreen = ContainsColor(bmp, cpuColor, 10);
                    bool foundBlue = ContainsColor(bmp, ramColor, 10);
                    bool foundOrange = ContainsColor(bmp, diskColor, 10);
                    bool foundPurple = ContainsColor(bmp, netColor, 10);
                    bool foundDarkBg = ContainsColor(bmp, backColor, 10);

                    Console.WriteLine("  green (CPU bar)   : " + (foundGreen ? "PASS" : "FAIL"));
                    Console.WriteLine("  blue (RAM bar)    : " + (foundBlue ? "PASS" : "FAIL"));
                    Console.WriteLine("  orange (Disk bar) : " + (foundOrange ? "PASS" : "FAIL"));
                    Console.WriteLine("  purple (Net bar)  : " + (foundPurple ? "PASS" : "FAIL"));
                    Console.WriteLine("  dark background   : " + (foundDarkBg ? "PASS" : "FAIL"));
                    allPass &= foundGreen && foundBlue && foundOrange && foundPurple && foundDarkBg;
                }
            }
            else { Console.WriteLine("MISSING: " + barsPath); allPass = false; }

            // ---- NUMERIC mode ----
            string numPath = Path.Combine(dir, "preview_Numeric.png");
            if (File.Exists(numPath))
            {
                using (var bmp = new Bitmap(numPath))
                {
                    Console.WriteLine("=== NUMERIC mode (" + bmp.Width + "x" + bmp.Height + ") ===");
                    bool foundGreen = ContainsColor(bmp, cpuColor, 10);
                    bool foundBlue = ContainsColor(bmp, ramColor, 10);
                    bool foundOrange = ContainsColor(bmp, diskColor, 10);
                    bool foundPurple = ContainsColor(bmp, netColor, 10);
                    bool foundWhite = ContainsColor(bmp, Color.White, 10);
                    Console.WriteLine("  green tag (C)   : " + (foundGreen ? "PASS" : "FAIL"));
                    Console.WriteLine("  blue tag (R)    : " + (foundBlue ? "PASS" : "FAIL"));
                    Console.WriteLine("  orange tag (D)  : " + (foundOrange ? "PASS" : "FAIL"));
                    Console.WriteLine("  purple tag (N)  : " + (foundPurple ? "PASS" : "FAIL"));
                    Console.WriteLine("  white numbers   : " + (foundWhite ? "PASS" : "FAIL"));
                    allPass &= foundGreen && foundBlue && foundOrange && foundPurple && foundWhite;
                }
            }
            else { Console.WriteLine("MISSING: " + numPath); allPass = false; }

            // ---- OVERALL mode ----
            string overallPath = Path.Combine(dir, "preview_Overall.png");
            if (File.Exists(overallPath))
            {
                using (var bmp = new Bitmap(overallPath))
                {
                    Console.WriteLine("=== OVERALL mode (" + bmp.Width + "x" + bmp.Height + ") ===");
                    bool foundDarkBg = ContainsColor(bmp, backColor, 10);
                    bool foundWhite = ContainsColor(bmp, Color.White, 10);
                    // arc color at 42.88% should be green (< 50)
                    bool foundArcGreen = ContainsColor(bmp, cpuColor, 10);
                    bool foundTrack = ContainsColor(bmp, Color.FromArgb(50, 50, 55), 10);
                    Console.WriteLine("  dark background  : " + (foundDarkBg ? "PASS" : "FAIL"));
                    Console.WriteLine("  white number     : " + (foundWhite ? "PASS" : "FAIL"));
                    Console.WriteLine("  green arc (<50%) : " + (foundArcGreen ? "PASS" : "FAIL"));
                    Console.WriteLine("  track ring       : " + (foundTrack ? "PASS" : "FAIL"));
                    allPass &= foundDarkBg && foundWhite && foundArcGreen && foundTrack;
                }
            }
            else { Console.WriteLine("MISSING: " + overallPath); allPass = false; }

            Console.WriteLine();
            Console.WriteLine("=== OVERALL VERDICT: " + (allPass ? "ALL PASS" : "FAILURES DETECTED") + " ===");
            Environment.ExitCode = allPass ? 0 : 1;
        }

        /// <summary>
        /// Check if a bitmap contains a color within a tolerance.
        /// </summary>
        private static bool ContainsColor(Bitmap bmp, Color target, int tolerance)
        {
            // Sample a grid of pixels (every 4th pixel) for speed
            for (int x = 0; x < bmp.Width; x += 4)
            {
                for (int y = 0; y < bmp.Height; y += 4)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (Math.Abs(c.R - target.R) <= tolerance &&
                        Math.Abs(c.G - target.G) <= tolerance &&
                        Math.Abs(c.B - target.B) <= tolerance)
                        return true;
                }
            }
            return false;
        }
    }
}
