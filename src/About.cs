using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace LoqRgb
{
    internal static class Developer
    {
        public const string Name = "Salman MP";
        public const string Initials = "SM";
        public static readonly string[] Roles = { "Full Stack Developer", "DevOps Engineer", "Tech Consultant" };
        public const string Location = "Malappuram, Keralam, India";
    }

    /// <summary>Round "i" info button for the header.</summary>
    internal sealed class InfoButton : Control
    {
        bool hot, down;

        public InfoButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
            BackColor = Theme.Bg;
        }

        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            float d = Math.Min(Width, Height) - 2;
            var r = new RectangleF((Width - d) / 2f, (Height - d) / 2f, d, d);
            Color fill = down ? Theme.Border : hot ? Theme.PanelHover : Theme.Panel;
            Color ring = hot ? Theme.Accent : Theme.Border;
            Color ink = hot ? Color.White : Theme.Text;
            using (var b = new SolidBrush(fill)) g.FillEllipse(b, r);
            using (var p = new Pen(ring, 1.4f)) g.DrawEllipse(p, r);

            // the "i": a dot and a stem
            float cx = r.X + r.Width / 2f, s = d / 32f;
            using (var b = new SolidBrush(ink))
            {
                g.FillEllipse(b, cx - 2.1f * s, r.Y + 7.5f * s, 4.2f * s, 4.2f * s);
                using (GraphicsPath stem = Gfx.Rounded(new RectangleF(cx - 1.8f * s, r.Y + 13.5f * s, 3.6f * s, 11f * s), 1.6f * s))
                    g.FillPath(b, stem);
            }
        }
    }

    /// <summary>Developer card: avatar, name, roles and location.</summary>
    internal sealed class DeveloperCard : Control
    {
        static Image avatar;
        static bool avatarLoaded;

        /// <summary>Portrait embedded in the exe (assets\avatar.png); null if missing.</summary>
        static Image Avatar
        {
            get
            {
                if (!avatarLoaded)
                {
                    avatarLoaded = true;
                    try
                    {
                        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("LoqRgb.avatar.png"))
                            if (s != null) avatar = new Bitmap(Image.FromStream(s));
                    }
                    catch { avatar = null; }
                }
                return avatar;
            }
        }

        public DeveloperCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            float k = DeviceDpi / 96f;

            var card = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Gfx.Rounded(card, 12 * k))
            using (var b = new SolidBrush(Theme.Panel))
            using (var pen = new Pen(Theme.Border))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }

            // Avatar: embedded portrait in a circle with a glowing ring (initials as fallback)
            float av = 76 * k, ax = 18 * k, ay = (Height - av) / 2f;
            var avRect = new RectangleF(ax, ay, av, av);
            var ringRect = RectangleF.Inflate(avRect, 3 * k, 3 * k);
            using (var b = new LinearGradientBrush(ringRect, Color.FromArgb(45, 230, 170), Color.FromArgb(79, 140, 255), 45f))
                g.FillEllipse(b, ringRect);
            Image photo = Avatar;
            if (photo != null)
            {
                using (var clip = new GraphicsPath())
                {
                    clip.AddEllipse(avRect);
                    GraphicsState saved = g.Save();
                    g.SetClip(clip);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(photo, avRect);
                    g.Restore(saved);
                }
                using (var p = new Pen(Theme.Panel, 1.5f * k)) g.DrawEllipse(p, avRect);
            }
            else
            {
                using (var b = new LinearGradientBrush(avRect, Color.FromArgb(79, 140, 255), Color.FromArgb(180, 60, 255), 45f))
                    g.FillEllipse(b, avRect);
                using (var f = new Font("Segoe UI Semibold", 17F))
                    TextRenderer.DrawText(g, Developer.Initials, f, Rectangle.Round(avRect), Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            int tx = (int)(ax + av + 18 * k);
            int y = (int)(16 * k);
            using (var nameFont = new Font("Segoe UI Semibold", 14F))
            {
                TextRenderer.DrawText(g, Developer.Name, nameFont, new Point(tx, y), Theme.Text, TextFormatFlags.NoPadding);
                y += nameFont.Height + (int)(6 * k);
            }

            // Role pills
            int px = tx;
            using (var pf = new Font("Segoe UI", 8.25F))
            {
                foreach (string role in Developer.Roles)
                {
                    Size sz = TextRenderer.MeasureText(role, pf, Size.Empty, TextFormatFlags.NoPadding);
                    var pill = new RectangleF(px, y, sz.Width + 16 * k, sz.Height + 8 * k);
                    if (pill.Right > Width - 12 * k)
                    {
                        px = tx;
                        y += (int)(pill.Height + 6 * k);
                        pill = new RectangleF(px, y, pill.Width, pill.Height);
                    }
                    using (GraphicsPath p = Gfx.Rounded(pill, pill.Height / 2f))
                    using (var b = new SolidBrush(Color.FromArgb(40, Theme.Accent)))
                    using (var pen = new Pen(Color.FromArgb(110, Theme.Accent)))
                    {
                        g.FillPath(b, p);
                        g.DrawPath(pen, p);
                    }
                    TextRenderer.DrawText(g, role, pf, Rectangle.Round(pill), Color.FromArgb(190, 210, 255),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    px = (int)(pill.Right + 6 * k);
                }
                y += (int)(TextRenderer.MeasureText("Ag", pf).Height + 8 * k + 10 * k);
            }

            // Location with a map-pin glyph
            float pinW = 10 * k, pinH = 14 * k;
            var pin = new RectangleF(tx, y + 1 * k, pinW, pinH);
            using (var path = new GraphicsPath())
            {
                path.AddArc(pin.X, pin.Y, pinW, pinW, 150, 240);
                path.AddLine(pin.X + pinW * 0.93f, pin.Y + pinW * 0.75f, pin.X + pinW / 2f, pin.Bottom);
                path.CloseFigure();
                using (var b = new SolidBrush(Theme.Bad)) g.FillPath(b, path);
            }
            using (var b = new SolidBrush(Theme.Panel))
                g.FillEllipse(b, pin.X + pinW * 0.3f, pin.Y + pinW * 0.3f, pinW * 0.4f, pinW * 0.4f);
            TextRenderer.DrawText(g, Developer.Location, Font, new Point((int)(tx + pinW + 7 * k), y), Theme.Muted, TextFormatFlags.NoPadding);
        }
    }

    internal static class AboutDialog
    {
        public static string Version
        {
            get
            {
                Version v = Assembly.GetExecutingAssembly().GetName().Version;
                return v.Major + "." + v.Minor + "." + v.Build;
            }
        }

        public static void Show(IWin32Window owner, LightState state, string deviceName)
        {
            using (var f = new Form())
            using (Icon icon = Gfx.MakeIcon(state))
            {
                f.AutoScaleDimensions = new SizeF(96F, 96F);
                f.AutoScaleMode = AutoScaleMode.Dpi;
                f.Font = new Font("Segoe UI", 9F);
                f.Text = "About LOQ Keyboard RGB";
                f.Icon = icon;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MinimizeBox = f.MaximizeBox = false;
                f.ShowInTaskbar = false;
                f.StartPosition = FormStartPosition.CenterParent;
                f.ClientSize = new Size(440, 372);
                f.BackColor = Theme.Bg;
                f.ForeColor = Theme.Text;
                f.HandleCreated += delegate { DarkTitleBar(f.Handle); };

                var logo = new PictureBox { Left = 22, Top = 22, Width = 48, Height = 48, SizeMode = PictureBoxSizeMode.Zoom };
                logo.Image = new Icon(icon, 48, 48).ToBitmap();
                f.Controls.Add(logo);
                f.Controls.Add(new Label { Text = "LOQ Keyboard RGB", Left = 82, Top = 20, AutoSize = true, Font = new Font("Segoe UI Semibold", 15F) });
                var versionLabel = new Label { Text = "Version " + Version, Left = 85, Top = 52, AutoSize = true, ForeColor = Theme.Muted };
                f.Controls.Add(versionLabel);
                string repo = Updates.RepositoryUrl;
                if (repo != null)
                {
                    var update = new LinkLabel
                    {
                        Text = "Check for updates", Left = 170, Top = 52, AutoSize = true,
                        LinkColor = Theme.Accent, ActiveLinkColor = Color.White, VisitedLinkColor = Theme.Accent,
                        LinkBehavior = LinkBehavior.HoverUnderline
                    };
                    string releaseUrl = null;
                    update.LinkClicked += delegate
                    {
                        if (releaseUrl != null)
                        {
                            System.Diagnostics.Process.Start(releaseUrl);
                            return;
                        }
                        update.Text = "Checking…";
                        update.Enabled = false;
                        Updates.CheckAsync(repo, r => update.BeginInvoke(new Action(() =>
                        {
                            update.Enabled = true;
                            if (r.Error != null) { update.Text = r.Error + " Retry"; return; }
                            if (!r.Newer) { update.Text = "Up to date ✓"; return; }
                            releaseUrl = r.PageUrl ?? repo + "/releases/latest";
                            update.Text = "Version " + r.Version + " available: download";
                        })));
                    };
                    f.Controls.Add(update);
                }

                f.Controls.Add(new Label
                {
                    Left = 22, Top = 88, Width = 396, Height = 40, ForeColor = Theme.Text,
                    Text = "Control and customize the 4-zone RGB keyboard backlight on Lenovo LOQ laptops: effects, colours, profiles and more."
                });

                f.Controls.Add(new Panel { Left = 22, Top = 140, Width = 396, Height = 1, BackColor = Theme.Border });
                f.Controls.Add(new Label { Text = "DEVELOPED BY", Left = 22, Top = 156, AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8F, FontStyle.Bold) });
                f.Controls.Add(new DeveloperCard { Left = 22, Top = 178, Width = 396, Height = 128, Font = f.Font });

                string device = deviceName != null ? "Keyboard: " + deviceName + "  ·  connected" : "Keyboard: not detected";
                f.Controls.Add(new Label { Text = "© " + DateTime.Now.Year + " " + Developer.Name + "\n" + device, Left = 22, Top = 322, Width = 300, Height = 36, ForeColor = Theme.Muted });

                var close = new Button
                {
                    Text = "Close", DialogResult = DialogResult.OK, Left = 332, Top = 324, Width = 86, Height = 32,
                    FlatStyle = FlatStyle.Flat, BackColor = Theme.Accent, ForeColor = Color.White, Cursor = Cursors.Hand
                };
                close.FlatAppearance.BorderColor = Theme.Accent;
                f.Controls.Add(close);
                f.AcceptButton = close;
                f.CancelButton = close;

                f.ShowDialog(owner);
                if (logo.Image != null) logo.Image.Dispose();
            }
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        static void DarkTitleBar(IntPtr h)
        {
            try { int on = 1; DwmSetWindowAttribute(h, 20, ref on, 4); } catch { }
        }
    }
}
