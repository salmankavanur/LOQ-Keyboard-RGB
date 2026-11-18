using System;
using System.Collections.Generic;
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

        public const string BatteryMode = "batteryMode";      // 0 keep, 1 low brightness, 2 off
        public const string OffWithScreen = "offWithScreen";
        public const string IdleMinutes = "idleMinutes";      // 0 = never
        public const string NightEnabled = "nightEnabled";
        public const string NightProfile = "nightProfile";
        public const string NightFrom = "nightFrom";
        public const string NightTo = "nightTo";

        public const string AppRulesEnabled = "appRulesEnabled";
        public const string AppRules = "appRules";

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
            ClientSize = new Size(520, 660);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;

            body = new Panel { Left = 0, Top = 0, Width = 520, Height = 600, AutoScroll = true, BackColor = Theme.Bg };
            Controls.Add(body);
            Controls.Add(new Panel { Left = 0, Top = 600, Width = 520, Height = 1, BackColor = Theme.Border });
            var close = new Button
            {
                Text = "Done", DialogResult = DialogResult.OK, Left = 416, Top = 614, Width = 86, Height = 32,
                FlatStyle = FlatStyle.Flat, BackColor = Theme.Accent, ForeColor = Color.White, Cursor = Cursors.Hand
            };
            close.FlatAppearance.BorderColor = Theme.Accent;
            Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;

            BuildNotifications();
            BuildPower();
            BuildLenovoMode();
            BuildAppRules();
            BuildHotkeys();
            BuildInstall();
        }

        void BuildInstall()
        {
            Section("Installation", null);
            if (Installer.RunningFromInstall)
            {
                Note("Installed for this user in " + Installer.InstallDir + ". Remove it from Windows Settings → Apps, or here:");
                Button un = Small("Uninstall…", Left0 + 22, y, 120);
                un.Click += delegate
                {
                    System.Diagnostics.Process.Start(Installer.InstalledExe, "--uninstall");
                };
                y += 40;
            }
            else
            {
                Note("Running from " + System.IO.Path.GetDirectoryName(Application.ExecutablePath) +
                     ". Install it to get a Start menu entry and an uninstaller (no admin rights needed)." +
                     (Installer.IsInstalled ? " An installed copy exists; installing again updates it." : ""));
                Button inst = Small(Installer.IsInstalled ? "Update installed copy" : "Install for this user", Left0 + 22, y, 180);
                inst.Click += delegate
                {
                    try { Installer.InstallAndRelaunch(); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Installation failed:\n" + ex.Message, "LOQ Keyboard RGB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };
                y += 40;
            }
        }

        void Choice(string label, string key, int fallback, string[] items, int[] values)
        {
            body.Controls.Add(new Label { Text = label, Left = Left0, Top = y + 4, AutoSize = true, ForeColor = Theme.Text });
            ComboBox c = Combo(Left0 + 190, y, 180, false);
            c.Items.AddRange(items);
            int current = settings.GetInt(key, fallback);
            c.SelectedIndex = Math.Max(0, Array.IndexOf(values, current));
            c.SelectedIndexChanged += delegate
            {
                settings.SetInt(key, values[c.SelectedIndex]);
                Commit();
            };
            y += 34;
        }

        void BuildLenovoMode()
        {
            PowerModeWatcher w = PowerModeWatcher.Shared;
            Section("Lenovo power mode (Fn+Q)", "Switch lighting with the power mode: e.g. blue for Quiet, white for Balanced, red for Performance.");
            if (w == null || !w.Available)
            {
                Note("Not available: this laptop doesn't report Lenovo power-mode changes.");
                return;
            }
            Toggle("Change lighting with the power mode", PowerModeWatcher.EnabledKey, false, null);
            string[] profiles = AppSettings.ListProfiles();
            foreach (LenovoPowerMode m in new[] { LenovoPowerMode.Quiet, LenovoPowerMode.Balanced, LenovoPowerMode.Performance })
            {
                string key = PowerModeWatcher.SettingKey(m);
                string label = m + (w.Current == m ? "   (now)" : "");
                body.Controls.Add(new Label { Text = label, Left = Left0 + 22, Top = y + 4, AutoSize = true, ForeColor = w.Current == m ? Theme.Accent : Theme.Text });
                ComboBox c = Combo(Left0 + 190, y, 180, false);
                c.Items.Add("(no change)");
                c.Items.AddRange(profiles);
                string current = settings.GetString(key, null);
                c.SelectedIndex = current == null ? 0 : Math.Max(0, c.Items.IndexOf(current));
                c.SelectedIndexChanged += delegate
                {
                    settings.SetString(key, c.SelectedIndex <= 0 ? null : (string)c.SelectedItem);
                    Commit();
                };
                y += 34;
            }
            Note("Changing the lighting yourself overrides the mode's profile until you press Fn+Q again.");
        }

        void BuildPower()
        {
            Section("Power & schedule", null);
            Choice("On battery", Opt.BatteryMode, 0, new[] { "Keep my lighting", "Low brightness", "Turn lights off" }, new[] { 0, 1, 2 });
            Choice("Turn off when idle for", Opt.IdleMinutes, 0,
                new[] { "Never", "1 minute", "2 minutes", "5 minutes", "10 minutes", "15 minutes", "30 minutes" },
                new[] { 0, 1, 2, 5, 10, 15, 30 });
            Toggle("Turn lights off while the screen is off", Opt.OffWithScreen, true, null);

            y += 4;
            Toggle("Night profile", Opt.NightEnabled, false, null);
            string[] times = new string[48];
            for (int i = 0; i < 48; i++) times[i] = string.Format("{0:00}:{1:00}", i / 2, (i % 2) * 30);
            ComboBox profile = Combo(Left0 + 22, y, 160, false);
            profile.Items.AddRange(AppSettings.ListProfiles());
            profile.SelectedItem = settings.GetString(Opt.NightProfile, null);
            if (profile.SelectedIndex < 0 && profile.Items.Count > 0)
            {
                profile.SelectedIndex = 0;
                settings.SetString(Opt.NightProfile, (string)profile.SelectedItem);
            }
            profile.SelectedIndexChanged += delegate { settings.SetString(Opt.NightProfile, profile.SelectedItem as string); Commit(); };

            body.Controls.Add(new Label { Text = "from", Left = Left0 + 190, Top = y + 4, AutoSize = true, ForeColor = Theme.Muted });
            ComboBox from = Combo(Left0 + 226, y, 80, false);
            from.Items.AddRange(times);
            from.SelectedItem = settings.GetString(Opt.NightFrom, "22:00");
            from.SelectedIndexChanged += delegate { settings.SetString(Opt.NightFrom, from.SelectedItem as string); Commit(); };

            body.Controls.Add(new Label { Text = "to", Left = Left0 + 314, Top = y + 4, AutoSize = true, ForeColor = Theme.Muted });
            ComboBox to = Combo(Left0 + 336, y, 80, false);
            to.Items.AddRange(times);
            to.SelectedItem = settings.GetString(Opt.NightTo, "07:00");
            to.SelectedIndexChanged += delegate { settings.SetString(Opt.NightTo, to.SelectedItem as string); Commit(); };
            y += 38;
            Note("Changing the lighting yourself during the night pauses the night profile until the next night.");
        }

        void BuildHotkeys()
        {
            Section("Hotkeys", "Work everywhere, even in games. Click a box and press a new combination; Backspace clears it, Esc cancels.");
            var conflictLabels = new List<KeyValuePair<HotkeyAction, Label>>();
            Action showConflicts = () =>
            {
                foreach (var kv in conflictLabels) kv.Value.Visible = Hotkeys.Conflicts.Contains(kv.Key);
            };
            Toggle("Enable hotkeys", Hotkeys.EnabledKey, true, showConflicts);
            foreach (HotkeyAction a in Hotkeys.All)
            {
                HotkeyAction action = a;
                body.Controls.Add(new Label { Text = Hotkeys.Label(a), Left = Left0 + 22, Top = y + 4, AutoSize = true, ForeColor = Theme.Text });
                var box = new HotkeyBox { Left = Left0 + 190, Top = y, Width = 170, Value = Hotkeys.Get(settings, a) };
                body.Controls.Add(box);
                var conflict = new Label { Text = "⚠ in use", Left = Left0 + 370, Top = y + 4, AutoSize = true, ForeColor = Theme.Warn, Visible = false };
                tips.SetToolTip(conflict, "Another program already uses this combination. Pick a different one.");
                body.Controls.Add(conflict);
                conflictLabels.Add(new KeyValuePair<HotkeyAction, Label>(a, conflict));
                box.ValueChanged += k =>
                {
                    Hotkeys.Set(settings, action, k);
                    Commit();
                    showConflicts();
                };
                y += 32;
            }
            showConflicts();
        }

        ComboBox Combo(int x, int top, int w, bool editable)
        {
            var c = new ComboBox
            {
                Left = x, Top = top, Width = w, FlatStyle = FlatStyle.Flat, BackColor = Theme.Panel, ForeColor = Theme.Text,
                DropDownStyle = editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList
            };
            if (!editable)
            {
                c.DrawMode = DrawMode.OwnerDrawFixed;
                c.DrawItem += (s, e) =>
                {
                    bool hot = (e.State & DrawItemState.ComboBoxEdit) == 0 && (e.State & DrawItemState.Selected) != 0;
                    using (var b = new SolidBrush(hot ? Theme.Accent : Theme.Panel)) e.Graphics.FillRectangle(b, e.Bounds);
                    if (e.Index < 0) return;
                    var r = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, c.Items[e.Index].ToString(), c.Font, r, hot ? Color.White : Theme.Text,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                };
            }
            body.Controls.Add(c);
            return c;
        }

        ListBox DarkList(int height)
        {
            var l = new ListBox
            {
                Left = Left0, Top = y, Width = Width0, Height = height, BackColor = Theme.Panel, ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 24, IntegralHeight = false
            };
            l.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                bool sel = (e.State & DrawItemState.Selected) != 0;
                using (var b = new SolidBrush(sel ? Theme.Accent : Theme.Panel)) e.Graphics.FillRectangle(b, e.Bounds);
                var r = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, l.Items[e.Index].ToString(), l.Font, r, sel ? Color.White : Theme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            body.Controls.Add(l);
            y += height + 8;
            return l;
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
            body.Controls.Add(new Label
            {
                Text = title.ToUpperInvariant(), Left = Left0, Top = y, AutoSize = true, UseMnemonic = false,
                ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            });
            y += 20;
            if (subtitle != null) y += Wrapped(subtitle, Left0, y, Width0) + 6;
        }

        /// <summary>A muted label that wraps within the given width and sizes itself; returns its height.</summary>
        int Wrapped(string text, int x, int top, int width)
        {
            var l = new Label
            {
                Text = text, Left = x, Top = top, ForeColor = Theme.Muted, UseMnemonic = false,
                AutoSize = true, MaximumSize = new Size(width, 0)
            };
            body.Controls.Add(l);
            return l.PreferredHeight > 0 ? l.GetPreferredSize(new Size(width, 0)).Height : l.Height;
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
            y += Wrapped(text, Left0 + 22, y - 2, Width0 - 22) + 8;
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

        void BuildAppRules()
        {
            Section("App profiles", "Use a profile automatically while a specific app is in front. Your normal lighting comes back when you switch away.");
            Toggle("Enable app profiles", Opt.AppRulesEnabled, true, null);

            List<AppRule> rules = AppRule.Parse(settings.GetString(Opt.AppRules, ""));
            ListBox list = DarkList(110);
            Action reload = () =>
            {
                list.Items.Clear();
                foreach (AppRule r in rules) list.Items.Add(r);
            };
            Action save = () =>
            {
                settings.SetString(Opt.AppRules, AppRule.Serialize(rules));
                Commit();
                reload();
            };
            reload();

            ComboBox app = Combo(Left0, y + 2, 150, true);
            app.Items.AddRange(AppRule.RunningApps());
            tips.SetToolTip(app, "The app's .exe name. Pick a running app or type one, e.g. valorant");
            body.Controls.Add(new Label { Text = "→", Left = Left0 + 154, Top = y + 5, AutoSize = true, ForeColor = Theme.Muted });
            ComboBox profile = Combo(Left0 + 172, y + 2, 140, false);
            profile.Items.AddRange(AppSettings.ListProfiles());
            if (profile.Items.Count > 0) profile.SelectedIndex = 0;

            Button add = Small("Add", Left0 + 318, y, 72);
            add.Click += delegate
            {
                string name = AppRule.Normalize(app.Text);
                var prof = profile.SelectedItem as string;
                if (name.Length == 0 || name.IndexOfAny(new[] { '|', '>' }) >= 0 || prof == null) return;
                rules.RemoveAll(r => r.App.Equals(name, StringComparison.OrdinalIgnoreCase));
                rules.Add(new AppRule { App = name, Profile = prof });
                save();
                app.Text = "";
            };
            Button remove = Small("Remove", Left0 + 394, y, 76);
            remove.Click += delegate
            {
                var r = list.SelectedItem as AppRule;
                if (r == null) return;
                rules.Remove(r);
                save();
            };
            y += 38;
            Note("Rules don't apply while the lights are turned off. Changing the lighting yourself overrides the rule until you switch apps.");
        }
    }
}
