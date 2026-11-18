using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LoqRgb
{
    /// <summary>Animated, clickable preview of the LOQ 15 keyboard split into its 4 lighting zones.</summary>
    public sealed class KeyboardView : Control
    {
        struct Key
        {
            public RectangleF R;   // in key units
            public int Zone;
        }

        const float MainW = 15f, PadGap = 0.45f, PadW = 4f, RowGap = 0.14f;
        static readonly float TotalW = MainW + PadGap + PadW;
        static readonly float[] RowH = { 0.62f, 1f, 1f, 1f, 1f, 1f };
        static readonly float TotalH;
        static readonly List<Key> AllKeys = new List<Key>();
        static readonly RectangleF[] ZoneBounds = new RectangleF[4];
        static readonly string[] ZoneLabels = { "Zone 1 · Left", "Zone 2 · Center", "Zone 3 · Right", "Zone 4 · Numpad" };

        static KeyboardView()
        {
            float[][] main =
            {
                Row(0, 15, 0),
                Row(0, 13, 2f),
                Row(1.5f, 12, 1.5f),
                Row(1.75f, 11, 2.25f),
                Row(2.25f, 10, 2.75f),
                new[] { 1f, 1f, 1f, 1f, 5.5f, 1f, 1.5f, 1f, 1f, 1f }
            };
            float[][] pad =
            {
                Row(0, 4, 0), Row(0, 4, 0), Row(0, 4, 0), Row(0, 4, 0), Row(0, 4, 0),
                new[] { 2f, 1f, 1f }
            };

            float y = 0;
            for (int r = 0; r < main.Length; r++)
            {
                float x = 0;
                foreach (float w in main[r])
                {
                    float cx = x + w / 2;
                    AddKey(x, y, w, RowH[r], cx < 5.5f ? 0 : cx < 10.5f ? 1 : 2);
                    x += w;
                }
                x = MainW + PadGap;
                foreach (float w in pad[r])
                {
                    AddKey(x, y, w, RowH[r], 3);
                    x += w;
                }
                y += RowH[r] + RowGap;
            }
            TotalH = y - RowGap;

            for (int z = 0; z < 4; z++) ZoneBounds[z] = RectangleF.Empty;
            foreach (Key k in AllKeys)
                ZoneBounds[k.Zone] = ZoneBounds[k.Zone].IsEmpty ? k.R : RectangleF.Union(ZoneBounds[k.Zone], k.R);
        }

        static float[] Row(float first, int ones, float last)
        {
            var l = new List<float>();
            if (first > 0) l.Add(first);
            for (int i = 0; i < ones; i++) l.Add(1f);
            if (last > 0) l.Add(last);
            return l.ToArray();
        }

        static void AddKey(float x, float y, float w, float h, int zone)
        {
            AllKeys.Add(new Key { R = new RectangleF(x, y, w, h), Zone = zone });
        }

        public LightState State;
        public bool[] Selected = new bool[4];
        /// <summary>zone, ctrlHeld, isDoubleClick</summary>
        public event Action<int, bool, bool> ZoneClicked;

        readonly Timer timer = new Timer();
        readonly DateTime start = DateTime.UtcNow;
        float unit = 1, originX, originY;
        int hover = -1;

        public KeyboardView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
            Cursor = Cursors.Hand;
            timer.Interval = 33;
            timer.Tick += delegate { if (State != null && IsAnimated(State.Effect) && Visible) Invalidate(); };
            timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }

        public static bool IsAnimated(Effect e)
        {
            return Effects.IsAnimated(e);
        }

        /// <summary>Latest frame streamed by the lighting engine (software effects). Set from any thread.</summary>
        public volatile Color[] LiveFrame;

        RectangleF ToPx(RectangleF u)
        {
            return new RectangleF(originX + u.X * unit, originY + u.Y * unit, u.Width * unit, u.Height * unit);
        }

        Color KeyColor(Key k, double t)
        {
            LightState s = State;
            if (s == null || s.Effect == Effect.Off) return Color.Empty;
            int sp = Math.Max(1, Math.Min(4, s.Speed)) - 1;
            double f = s.Brightness >= 2 ? 1.0 : 0.55;
            Color c;
            if (Effects.IsSoftware(s.Effect))
            {
                Color[] live = LiveFrame;
                c = live != null ? live[k.Zone] : s.Zones[k.Zone];
                return Gfx.Scale(c, f);
            }
            switch (s.Effect)
            {
                case Effect.Breath:
                    double period = new[] { 6.0, 4.5, 3.0, 2.0 }[sp];
                    f *= 0.1 + 0.9 * (0.5 - 0.5 * Math.Cos(2 * Math.PI * t / period));
                    c = s.Zones[k.Zone];
                    break;
                case Effect.Wave:
                    double v = new[] { 45.0, 85.0, 135.0, 200.0 }[sp];
                    double cx = k.R.X + k.R.Width / 2;
                    double dir = s.WaveRightToLeft ? -1 : 1;
                    c = Gfx.Hue(cx / TotalW * 300 - dir * v * t);
                    break;
                case Effect.Smooth:
                    c = Gfx.Hue(new[] { 25.0, 50.0, 80.0, 120.0 }[sp] * t);
                    break;
                default:
                    c = s.Zones[k.Zone];
                    break;
            }
            return Gfx.Scale(c, f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            var deck = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Gfx.Rounded(deck, 14))
            using (var b = new LinearGradientBrush(deck, Color.FromArgb(24, 26, 33), Color.FromArgb(12, 13, 17), 90f))
            using (var pen = new Pen(Theme.Border))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }

            float labelH = Font.Height + 16;
            const float pad = 22;
            unit = Math.Min((deck.Width - pad * 2) / TotalW, (deck.Height - pad * 2 - labelH) / TotalH);
            originX = deck.X + (deck.Width - TotalW * unit) / 2;
            originY = deck.Y + pad + (deck.Height - pad * 2 - labelH - TotalH * unit) / 2;
            double t = (DateTime.UtcNow - start).TotalSeconds;

            for (int z = 0; z < 4; z++)
            {
                bool sel = Selected != null && Selected[z];
                if (!sel && z != hover) continue;
                RectangleF zr = ToPx(ZoneBounds[z]);
                zr.Inflate(unit * 0.16f, unit * 0.16f);
                using (GraphicsPath p = Gfx.Rounded(zr, unit * 0.3f))
                using (var b = new SolidBrush(Color.FromArgb(sel ? 26 : 12, 255, 255, 255)))
                using (var pen = new Pen(Color.FromArgb(sel ? 190 : 70, 255, 255, 255), sel ? 1.6f : 1f))
                {
                    if (sel) pen.DashStyle = DashStyle.Dash;
                    g.FillPath(b, p);
                    g.DrawPath(pen, p);
                }
            }

            float inset = unit * 0.07f;
            foreach (Key k in AllKeys)
            {
                Color c = KeyColor(k, t);
                RectangleF r = ToPx(k.R);
                r.Inflate(-inset, -inset);
                using (GraphicsPath p = Gfx.Rounded(r, unit * 0.16f))
                {
                    if (c.IsEmpty || c.R + c.G + c.B < 12)
                    {
                        using (var b = new SolidBrush(Theme.KeyCap)) g.FillPath(b, p);
                        using (var pen = new Pen(Theme.KeyOffBorder)) g.DrawPath(pen, p);
                        continue;
                    }
                    RectangleF gr = r;
                    gr.Inflate(unit * 0.07f, unit * 0.07f);
                    using (GraphicsPath gp = Gfx.Rounded(gr, unit * 0.22f))
                    using (var gb = new SolidBrush(Color.FromArgb(60, c)))
                        g.FillPath(gb, gp);
                    using (var b = new SolidBrush(Gfx.Blend(Theme.KeyCap, c, 0.2)))
                        g.FillPath(b, p);
                    using (var pen = new Pen(Color.FromArgb(225, c), Math.Max(1f, unit * 0.04f)))
                        g.DrawPath(pen, p);
                    float lw = Math.Min(r.Width - unit * 0.3f, unit * 0.32f);
                    float lh = Math.Max(1.5f, unit * 0.07f);
                    using (var lb = new SolidBrush(Gfx.Blend(c, Color.White, 0.35)))
                        g.FillRectangle(lb, r.X + unit * 0.15f, r.Y + unit * 0.15f, lw, lh);
                }
            }

            int ly = (int)(originY + TotalH * unit + 12);
            for (int z = 0; z < 4; z++)
            {
                bool sel = Selected != null && Selected[z];
                RectangleF zr = ToPx(ZoneBounds[z]);
                Size sz = TextRenderer.MeasureText(ZoneLabels[z], Font);
                int dot = 8;
                int x = (int)(zr.X + zr.Width / 2 - (sz.Width + dot + 4) / 2f);
                Color col = sel ? Theme.Text : z == hover ? Color.FromArgb(195, 200, 214) : Theme.Muted;
                if (State != null)
                    using (var b = new SolidBrush(Gfx.ZoneSwatch(State, z)))
                        g.FillEllipse(b, x, ly + (sz.Height - dot) / 2f, dot, dot);
                TextRenderer.DrawText(g, ZoneLabels[z], Font, new Point(x + dot + 4, ly), col);
            }
        }

        int ZoneAt(Point pt)
        {
            if (unit <= 0) return -1;
            float ux = (pt.X - originX) / unit, uy = (pt.Y - originY) / unit;
            foreach (Key k in AllKeys)
                if (k.R.Contains(ux, uy)) return k.Zone;
            if (uy < -0.4f || uy > TotalH + 1.6f || ux < -0.4f || ux > TotalW + 0.4f) return -1;
            if (ux >= MainW + PadGap / 2) return 3;
            return ux < 5.5f ? 0 : ux < 10.5f ? 1 : 2;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int z = ZoneAt(e.Location);
            if (z != hover) { hover = z; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != -1) { hover = -1; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int z = ZoneAt(e.Location);
            if (e.Button == MouseButtons.Left && z >= 0 && ZoneClicked != null)
                ZoneClicked(z, (ModifierKeys & Keys.Control) != 0, false);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int z = ZoneAt(e.Location);
            if (e.Button == MouseButtons.Left && z >= 0 && ZoneClicked != null)
                ZoneClicked(z, false, true);
        }
    }
}
