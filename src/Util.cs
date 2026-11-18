using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;

namespace LoqRgb
{
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(17, 19, 24);
        public static readonly Color Panel = Color.FromArgb(28, 31, 39);
        public static readonly Color PanelHover = Color.FromArgb(38, 42, 52);
        public static readonly Color Border = Color.FromArgb(48, 53, 66);
        public static readonly Color Text = Color.FromArgb(230, 232, 238);
        public static readonly Color Muted = Color.FromArgb(138, 144, 160);
        public static readonly Color Accent = Color.FromArgb(79, 140, 255);
        public static readonly Color Good = Color.FromArgb(74, 222, 128);
        public static readonly Color Bad = Color.FromArgb(248, 113, 113);
        public static readonly Color KeyCap = Color.FromArgb(26, 29, 36);
        public static readonly Color KeyOffBorder = Color.FromArgb(50, 55, 66);
    }

    internal static class Gfx
    {
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);

        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d < 1f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Hue(double h)
        {
            h = ((h % 360) + 360) % 360;
            double x = 1 - Math.Abs((h / 60) % 2 - 1);
            double r = 0, g = 0, b = 0;
            switch ((int)(h / 60))
            {
                case 0: r = 1; g = x; break;
                case 1: r = x; g = 1; break;
                case 2: g = 1; b = x; break;
                case 3: g = x; b = 1; break;
                case 4: r = x; b = 1; break;
                default: r = 1; b = x; break;
            }
            return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        public static Color Scale(Color c, double f)
        {
            return Color.FromArgb(Clamp(c.R * f), Clamp(c.G * f), Clamp(c.B * f));
        }

        public static Color Blend(Color a, Color b, double t)
        {
            return Color.FromArgb(Clamp(a.R + (b.R - a.R) * t), Clamp(a.G + (b.G - a.G) * t), Clamp(a.B + (b.B - a.B) * t));
        }

        public static Color Contrast(Color c)
        {
            double l = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
            return l > 150 ? Color.FromArgb(18, 20, 26) : Color.White;
        }

        static int Clamp(double v)
        {
            return v < 0 ? 0 : v > 255 ? 255 : (int)Math.Round(v);
        }

        public static string ToHex(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }

        public static bool TryParseHex(string s, out Color c)
        {
            c = Color.Empty;
            if (s == null) return false;
            s = s.Trim().TrimStart('#');
            if (s.Length == 3) s = new string(new[] { s[0], s[0], s[1], s[1], s[2], s[2] });
            int v;
            if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) return false;
            c = Color.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255);
            return true;
        }

        /// <summary>Colour shown for zone z in small swatches (tray icon, labels) for a given state.</summary>
        public static Color ZoneSwatch(LightState s, int z)
        {
            if (s.Effect == Effect.Off) return Color.FromArgb(70, 75, 90);
            if (s.Effect == Effect.Wave || s.Effect == Effect.Smooth) return Hue(z * 80);
            return s.Zones[z];
        }

        public static Icon MakeIcon(LightState s)
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var p = Rounded(new RectangleF(1, 6, 30, 20), 5))
                using (var b = new SolidBrush(Color.FromArgb(30, 33, 41)))
                using (var pen = new Pen(Color.FromArgb(90, 96, 112)))
                {
                    g.FillPath(b, p);
                    g.DrawPath(pen, p);
                }
                for (int z = 0; z < 4; z++)
                {
                    Color c = ZoneSwatch(s, z);
                    if (c.R + c.G + c.B < 90) c = Color.FromArgb(70, 75, 90);
                    using (var p = Rounded(new RectangleF(4.5f + z * 6f, 10, 5, 12), 1.5f))
                    using (var b = new SolidBrush(c))
                        g.FillPath(b, p);
                }
                IntPtr h = bmp.GetHicon();
                try
                {
                    using (Icon tmp = Icon.FromHandle(h))
                        return (Icon)tmp.Clone();
                }
                finally
                {
                    DestroyIcon(h);
                }
            }
        }
    }
}
