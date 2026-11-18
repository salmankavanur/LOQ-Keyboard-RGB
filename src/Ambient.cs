using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LoqRgb
{
    /// <summary>
    /// Screen ambient light: the primary screen is shrunk to a tiny image (GDI halftone StretchBlt,
    /// fast and done by the OS), split into four vertical strips, and each strip's average colour
    /// drives the matching zone.
    /// </summary>
    internal sealed class AmbientSource : IFrameSource
    {
        const int W = 32, H = 12;
        static readonly double[] Smoothing = { 2.5, 5, 9, 16 }; // follow rate per second, by speed

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int w, int h);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern int SetStretchBltMode(IntPtr dc, int mode);
        [DllImport("gdi32.dll")] static extern bool SetBrushOrgEx(IntPtr dc, int x, int y, IntPtr old);
        [DllImport("gdi32.dll")] static extern bool StretchBlt(IntPtr dst, int dx, int dy, int dw, int dh, IntPtr src, int sx, int sy, int sw, int sh, int rop);
        [DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr dc, IntPtr bmp, int start, int lines, int[] bits, ref BITMAPINFOHEADER info, int usage);

        readonly int[] pixels = new int[W * H];
        readonly double[,] current = new double[4, 3];
        bool primed;
        string problem;

        // Reading the screen back from the GPU costs ~25 ms wall / ~10 ms CPU per sample, so sample ~10x a second;
        // the colour smoothing hides the lower rate.
        public int IntervalMs { get { return 70; } }
        public string Problem { get { return problem; } }

        /// <summary>Average colour of each quarter of the screen (left to right).</summary>
        public static bool Sample(int[] pixels, double[,] avg)
        {
            Rectangle b = Screen.PrimaryScreen.Bounds;
            IntPtr screen = GetDC(IntPtr.Zero);
            if (screen == IntPtr.Zero) return false;
            // Two stages: a fast point-sampled shrink of the whole screen to MidW x MidH, then a proper
            // area-averaging (halftone) shrink of that small image. Halftoning the full-resolution
            // screen directly costs ~30 ms per frame; this is a fraction of that.
            const int MidW = 256, MidH = 144;
            const int SRCCOPY = 0x00CC0020;
            IntPtr mid = CreateCompatibleDC(screen), mem = CreateCompatibleDC(screen);
            IntPtr midBmp = CreateCompatibleBitmap(screen, MidW, MidH), bmp = CreateCompatibleBitmap(screen, W, H);
            IntPtr oldMid = SelectObject(mid, midBmp), old = SelectObject(mem, bmp);
            try
            {
                SetStretchBltMode(mid, 3 /* COLORONCOLOR: point sampling */);
                if (!StretchBlt(mid, 0, 0, MidW, MidH, screen, b.X, b.Y, b.Width, b.Height, SRCCOPY)) return false;
                SetStretchBltMode(mem, 4 /* HALFTONE: area averaging */);
                SetBrushOrgEx(mem, 0, 0, IntPtr.Zero);
                if (!StretchBlt(mem, 0, 0, W, H, mid, 0, 0, MidW, MidH, SRCCOPY)) return false;
                SelectObject(mem, old);
                var info = new BITMAPINFOHEADER { biSize = 40, biWidth = W, biHeight = -H, biPlanes = 1, biBitCount = 32 };
                if (GetDIBits(mem, bmp, 0, H, pixels, ref info, 0) != H) return false;
            }
            finally
            {
                SelectObject(mem, old);
                SelectObject(mid, oldMid);
                DeleteObject(bmp);
                DeleteObject(midBmp);
                DeleteDC(mem);
                DeleteDC(mid);
                ReleaseDC(IntPtr.Zero, screen);
            }

            for (int z = 0; z < 4; z++)
            {
                double r = 0, g = 0, bl = 0;
                int n = 0;
                for (int y = 0; y < H; y++)
                    for (int x = z * W / 4; x < (z + 1) * W / 4; x++)
                    {
                        int p = pixels[y * W + x];
                        // square before averaging so bright details aren't drowned by dark areas
                        double pr = (p >> 16) & 255, pg = (p >> 8) & 255, pb = p & 255;
                        r += pr * pr; g += pg * pg; bl += pb * pb;
                        n++;
                    }
                avg[z, 0] = Math.Sqrt(r / n);
                avg[z, 1] = Math.Sqrt(g / n);
                avg[z, 2] = Math.Sqrt(bl / n);
            }
            return true;
        }

        public void Render(LightState s, double t, double dt, Color[] o)
        {
            var target = new double[4, 3];
            bool ok;
            try { ok = Sample(pixels, target); }
            catch (Exception ex) { ok = false; problem = "Ambient: " + ex.Message; }
            if (!ok)
            {
                if (problem == null) problem = "Ambient: the screen can't be captured right now (locked or secure desktop).";
                for (int z = 0; z < 4; z++) o[z] = Color.FromArgb((int)current[z, 0], (int)current[z, 1], (int)current[z, 2]);
                return;
            }
            problem = null;

            double k = primed ? 1 - Math.Exp(-Smoothing[Math.Max(1, Math.Min(4, s.Speed)) - 1] * dt) : 1;
            primed = true;
            for (int z = 0; z < 4; z++)
            {
                for (int c = 0; c < 3; c++) current[z, c] += (target[z, c] - current[z, c]) * k;
                o[z] = Vivid(current[z, 0], current[z, 1], current[z, 2]);
            }
        }

        /// <summary>LEDs look washed out with averaged screen colours: boost saturation, keep dark scenes dark.</summary>
        static Color Vivid(double r, double g, double b)
        {
            double max = Math.Max(r, Math.Max(g, b));
            if (max < 1) return Color.Black;
            double gray = (r + g + b) / 3;
            const double sat = 1.6;
            r = gray + (r - gray) * sat;
            g = gray + (g - gray) * sat;
            b = gray + (b - gray) * sat;
            double newMax = Math.Max(r, Math.Max(g, b));
            if (newMax > 0)
            {
                double scale = max / newMax; // keep perceived brightness
                r *= scale; g *= scale; b *= scale;
            }
            return Color.FromArgb(Clamp(r), Clamp(g), Clamp(b));
        }

        static int Clamp(double v)
        {
            return v < 0 ? 0 : v > 255 ? 255 : (int)v;
        }

        public void Dispose() { }
    }
}
