using System;
using System.Drawing;
using System.Windows.Forms;

namespace LoqRgb
{
    /// <summary>Keys and defaults for feature options stored in <see cref="AppSettings"/>.</summary>
    internal static class Opt
    {
        public const string FlashOnAttention = "flashOnAttention";
        public const string FlashColor = "flashColor";
        public const string LowBatteryFlash = "lowBatteryFlash";

        public static readonly Color DefaultFlashColor = Color.FromArgb(255, 176, 0);
    }

    /// <summary>The Settings window: one scrolling column of sections.</summary>
    internal sealed class SettingsDialog : Form
    {
        readonly AppSettings settings;
        readonly Action changed;
        readonly Action<Color> testFlash;
        readonly Panel body;
        readonly ToolTip tips = new ToolTip();
        int y = 12;
        const int Left0 = 18, Width0 = 470;

        public SettingsDialog(AppSettings settings, Action changed, Action<Color> testFlash)
        {
            this.settings = settings;
            this.changed = changed;
            this.testFlash = testFlash;

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            Text = "Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 560);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;

            body = new Panel { Left = 0, Top = 0, Width = 520, Height = 500, AutoScroll = true, BackColor = Theme.Bg };
            Controls.Add(body);
            Controls.Add(new Panel { Left = 0, Top = 500, Width = 520, Height = 1, BackColor = Theme.Border });
            var close = new Button
            {
                Text = "Done", DialogResult = DialogResult.OK, Left = 416, Top = 514, Width = 86, Height = 32,
                FlatStyle = FlatStyle.Flat, BackColor = Theme.Accent, ForeColor = Color.White, Cursor = Cursors.Hand
            };
            close.FlatAppearance.BorderColor = Theme.Accent;
            Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;

            BuildNotifications();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Gfx.DarkTitleBar(Handle);
        }

        // ------------------------------------------------------------ building blocks

        void Section(string title, string subtitle)
        {
            if (y > 12) y += 10;
            body.Controls.Add(new Label { Text = title.ToUpperInvariant(), Left = Left0, Top = y, AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8F, FontStyle.Bold) });
            y += 20;
            if (subtitle != null)
            {
                var l = new Label { Text = subtitle, Left = Left0, Top = y, Width = Width0, ForeColor = Theme.Muted };
                l.Height = TextRenderer.MeasureText(subtitle, l.Font, new Size(Width0, int.MaxValue), TextFormatFlags.WordBreak).Height + 6;
                body.Controls.Add(l);
                y += l.Height + 6;
            }
        }

        CheckBox Toggle(string text, string key, bool fallback, Action after)
        {
            var c = new CheckBox
            {
                Text = text, Left = Left0, Top = y, AutoSize = true, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand, Checked = settings.GetBool(key, fallback)
            };
            c.FlatAppearance.BorderColor = Theme.Border;
            c.FlatAppearance.CheckedBackColor = Theme.Accent;
            c.CheckedChanged += delegate
            {
                settings.SetBool(key, c.Checked);
                Commit();
                if (after != null) after();
            };
            body.Controls.Add(c);
            y += 28;
            return c;
        }

        Button Small(string text, int x, int top, int w)
        {
            var b = new Button
            {
                Text = text, Left = x, Top = top, Width = w, Height = 28, FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Panel, ForeColor = Theme.Text, Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Theme.Border;
            b.FlatAppearance.MouseOverBackColor = Theme.PanelHover;
            body.Controls.Add(b);
            return b;
        }

        Button ColorButton(string key, Color fallback, int x, int top)
        {
            var b = Small("", x, top, 46);
            b.BackColor = settings.GetColor(key, fallback);
            b.FlatAppearance.MouseOverBackColor = Gfx.Blend(b.BackColor, Color.White, 0.2);
            tips.SetToolTip(b, "Change colour");
            b.Click += delegate
            {
                using (var dlg = new ColorDialog { FullOpen = true, Color = b.BackColor })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    b.BackColor = dlg.Color;
                    b.FlatAppearance.MouseOverBackColor = Gfx.Blend(dlg.Color, Color.White, 0.2);
                    settings.SetColor(key, dlg.Color);
                    Commit();
                }
            };
            return b;
        }

        void Note(string text)
        {
            var l = new Label { Text = text, Left = Left0 + 22, Top = y - 2, Width = Width0 - 22, ForeColor = Theme.Muted };
            l.Height = TextRenderer.MeasureText(text, l.Font, new Size(Width0 - 22, int.MaxValue), TextFormatFlags.WordBreak).Height + 6;
            body.Controls.Add(l);
            y += l.Height + 6;
        }

        void Commit()
        {
            settings.Save();
            if (changed != null) changed();
        }

        // ------------------------------------------------------------ sections

        void BuildNotifications()
        {
            Section("Notifications", "Briefly flash the keyboard, then return to your lighting.");
            Toggle("Flash when an app needs attention", Opt.FlashOnAttention, true, null);
            Note("When an app's taskbar button starts flashing: new chat messages (WhatsApp, Teams, Discord…), finished downloads, prompts.");
            body.Controls.Add(new Label { Text = "Flash colour", Left = Left0 + 22, Top = y + 5, AutoSize = true, ForeColor = Theme.Text });
            ColorButton(Opt.FlashColor, Opt.DefaultFlashColor, Left0 + 112, y);
            Button test = Small("Test flash", Left0 + 166, y, 96);
            test.Click += delegate { testFlash(settings.GetColor(Opt.FlashColor, Opt.DefaultFlashColor)); };
            y += 38;
            Toggle("Flash on low battery (orange at 20%, red at 10%)", Opt.LowBatteryFlash, true, null);
            Note("From scripts or build tools:  LOQKeyboardRGB.exe --flash 00FF00 [--times 3]");
        }
    }
}
