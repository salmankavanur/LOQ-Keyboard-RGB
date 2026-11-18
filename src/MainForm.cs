using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LoqRgb
{
    public sealed class MainForm : Form
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "LOQKeyboardRGB";

        static readonly Effect[] EffectOrder = { Effect.Off, Effect.Static, Effect.Breath, Effect.Wave, Effect.Smooth };
        static readonly Effect[] SoftwareOrder = { Effect.Audio, Effect.Ambient, Effect.Monitor };
        static readonly Effect[] AllEffects = EffectOrder.Concat(SoftwareOrder).ToArray();
        static readonly string[] ZoneNames = { "Left", "Center", "Right", "Numpad" };
        static readonly string[] SpeedNames = { "Slow", "Medium", "Fast", "Fastest" };
        static readonly string[] PaletteHex =
        {
            "#FF0000", "#FF5A00", "#FFB000", "#FFF000", "#7CFF00", "#00FF40",
            "#00FFC8", "#00C8FF", "#0050FF", "#6A00FF", "#FF00C8", "#FFFFFF"
        };

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        readonly bool startHidden;
        readonly AppSettings settings;
        readonly LightingEngine engine = new LightingEngine();
        readonly bool[] selected = { true, false, false, false };
        readonly Dictionary<Effect, Button> effectButtons = new Dictionary<Effect, Button>();
        readonly Button[] speedButtons = new Button[4];
        readonly Button[] zoneButtons = new Button[4];
        readonly ToolTip tips = new ToolTip();
        readonly System.Windows.Forms.Timer reapplyTimer = new System.Windows.Forms.Timer();
        int[] customColors;

        KeyboardView view;
        Label statusLabel;
        Button briLow, briHigh, dirLtr, dirRtl;
        CheckBox linkBox, autoStartBox, trayBox;
        ComboBox profileBox;
        TextBox hexBox;
        NotifyIcon tray;
        ContextMenuStrip trayMenu;
        Icon currentIcon;
        EventWaitHandle showSignal;
        bool reallyExit, loadingProfiles;

        public MainForm(bool startHidden)
        {
            this.startHidden = startHidden;
            settings = AppSettings.Load();
            AppSettings.EnsureDefaultProfiles();

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            Text = "LOQ Keyboard RGB";
            ClientSize = new Size(840, 650);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            BuildUi();
            BuildTray();
            ResumeLayout(false);

            if (IsAutoStart()) SetAutoStart(true); // keep the Run entry pointing at this exe if it moved

            engine.StatusChanged += OnEngineStatus;
            engine.FrameRendered += frame => view.LiveFrame = frame;

            RefreshUi();
            RefreshProfiles(null);
            ApplyToKeyboard();
            UpdateIcons();

            reapplyTimer.Interval = 2500;
            reapplyTimer.Tick += delegate { reapplyTimer.Stop(); engine.Reapply(); };
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
            StartShowListener();
        }

        // ---------------------------------------------------------------- UI construction

        Label Caption(string text, int x, int y)
        {
            var l = new Label { Text = text, Left = x, Top = y, AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 8F, FontStyle.Bold) };
            Controls.Add(l);
            return l;
        }

        Button Chip(string text, int x, int y, int w, int h)
        {
            var b = new Button
            {
                Text = text, Left = x, Top = y, Width = w, Height = h,
                FlatStyle = FlatStyle.Flat, BackColor = Theme.Panel, ForeColor = Theme.Text,
                Cursor = Cursors.Hand, TabStop = false, UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderColor = Theme.Border;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Theme.PanelHover;
            b.FlatAppearance.MouseDownBackColor = Theme.Border;
            Controls.Add(b);
            return b;
        }

        static void SetChip(Button b, bool on)
        {
            b.BackColor = on ? Theme.Accent : Theme.Panel;
            b.ForeColor = on ? Color.White : (b.Enabled ? Theme.Text : Theme.Muted);
            b.FlatAppearance.BorderColor = on ? Theme.Accent : Theme.Border;
            b.FlatAppearance.MouseOverBackColor = on ? Theme.Accent : Theme.PanelHover;
            b.FlatAppearance.MouseDownBackColor = on ? Theme.Accent : Theme.Border;
        }

        CheckBox Check(string text, int x, int y)
        {
            var c = new CheckBox { Text = text, Left = x, Top = y, AutoSize = true, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            c.FlatAppearance.BorderColor = Theme.Border;
            c.FlatAppearance.CheckedBackColor = Theme.Accent;
            Controls.Add(c);
            return c;
        }

        void BuildUi()
        {
            Controls.Add(new Label { Text = "LOQ Keyboard RGB", AutoSize = true, Left = 13, Top = 12, Font = new Font("Segoe UI Semibold", 15F), ForeColor = Theme.Text });
            statusLabel = new Label { Left = 384, Top = 20, Width = 396, Height = 20, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.Muted };
            Controls.Add(statusLabel);

            var info = new InfoButton { Left = 792, Top = 14, Width = 32, Height = 32 };
            info.Click += delegate { ShowAbout(); };
            tips.SetToolTip(info, "About LOQ Keyboard RGB");
            Controls.Add(info);

            view = new KeyboardView { Left = 16, Top = 56, Width = 808, Height = 258, BackColor = Theme.Bg, Font = Font };
            view.ZoneClicked += OnZoneClicked;
            Controls.Add(view);

            // Left column: effect, speed, brightness, wave direction
            Caption("EFFECT", 16, 330);
            for (int i = 0; i < EffectOrder.Length; i++)
            {
                Effect fx = EffectOrder[i];
                Button b = Chip(fx.ToString(), 16 + i * 80, 350, 74, 34);
                b.Click += delegate { SetEffect(fx); };
                effectButtons[fx] = b;
            }
            for (int i = 0; i < SoftwareOrder.Length; i++)
            {
                Effect fx = SoftwareOrder[i];
                Button b = Chip(fx.ToString(), 16 + i * 66, 390, 62, 34);
                b.Click += delegate { SetEffect(fx); };
                tips.SetToolTip(b, Effects.Describe(fx));
                effectButtons[fx] = b;
            }

            Caption("SPEED", 16, 440);
            for (int i = 0; i < 4; i++)
            {
                int speed = i + 1;
                speedButtons[i] = Chip(SpeedNames[i], 16 + i * 100, 460, 94, 34);
                speedButtons[i].Click += delegate { settings.State.Speed = speed; Changed(); };
            }

            Caption("BRIGHTNESS", 16, 515);
            briLow = Chip("Low", 16, 535, 80, 34);
            briHigh = Chip("High", 102, 535, 80, 34);
            briLow.Click += delegate { SetBrightness(1); };
            briHigh.Click += delegate { SetBrightness(2); };

            Caption("WAVE DIRECTION", 196, 515);
            dirLtr = Chip("Left → Right", 196, 535, 97, 34);
            dirRtl = Chip("Right → Left", 299, 535, 97, 34);
            dirLtr.Click += delegate { settings.State.WaveRightToLeft = false; Changed(); };
            dirRtl.Click += delegate { settings.State.WaveRightToLeft = true; Changed(); };

            // Right column: zones, colours, profiles
            const int rx = 436;
            Caption("ZONE COLORS", rx, 330);
            linkBox = Check("Link all zones", 722, 326);
            linkBox.ForeColor = Theme.Muted;
            linkBox.CheckedChanged += delegate
            {
                for (int i = 0; i < 4; i++) selected[i] = linkBox.Checked || i == FirstSelected();
                RefreshUi();
            };

            for (int z = 0; z < 4; z++)
            {
                int zone = z;
                Button b = Chip((z + 1) + "  " + ZoneNames[z], rx + z * 99, 350, 91, 44);
                b.Font = new Font("Segoe UI Semibold", 9F);
                b.Click += delegate { OnZoneButton(zone); };
                tips.SetToolTip(b, "Click to select · click again to pick a color · Ctrl+click to select several");
                zoneButtons[z] = b;
            }

            for (int i = 0; i < PaletteHex.Length; i++)
            {
                Color c;
                Gfx.TryParseHex(PaletteHex[i], out c);
                Button s = Chip("", rx + i * 32, 402, 28, 28);
                s.BackColor = c;
                s.FlatAppearance.MouseOverBackColor = Gfx.Blend(c, Color.White, 0.25);
                s.FlatAppearance.MouseDownBackColor = c;
                s.Click += delegate { ApplyColor(c); };
                tips.SetToolTip(s, PaletteHex[i]);
            }

            Button custom = Chip("Custom color…", rx, 438, 130, 30);
            custom.Click += delegate { PickCustomColor(); };

            hexBox = new TextBox
            {
                Left = rx + 138, Top = 441, Width = 100, BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.Panel, ForeColor = Theme.Text, Font = new Font("Consolas", 10F), MaxLength = 7
            };
            hexBox.KeyDown += OnHexKeyDown;
            tips.SetToolTip(hexBox, "Type a hex color (e.g. #FF3300) and press Enter");
            Controls.Add(hexBox);

            Button fillAll = Chip("Copy to all zones", rx + 246, 438, 142, 30);
            fillAll.Click += delegate { CopyToAllZones(); };

            Caption("PROFILE", rx, 515);
            profileBox = new ComboBox
            {
                Left = rx, Top = 535, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat, BackColor = Theme.Panel, ForeColor = Theme.Text,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = (int)Math.Round(26 * ScreenDpi() / 96f), Cursor = Cursors.Hand
            };
            profileBox.DrawItem += OnProfileDrawItem;
            profileBox.SelectedIndexChanged += delegate
            {
                if (loadingProfiles) return;
                var name = profileBox.SelectedItem as string;
                if (name != null) LoadProfile(name);
            };
            Controls.Add(profileBox);
            Button save = Chip("Save as…", rx + 206, 535, 88, 34);
            save.Click += delegate { SaveProfile(); };
            Button del = Chip("Delete", rx + 300, 535, 88, 34);
            del.Click += delegate { DeleteProfile(); };

            // Footer
            Controls.Add(new Panel { Left = 16, Top = 592, Width = 808, Height = 1, BackColor = Theme.Border });
            autoStartBox = Check("Start with Windows", 16, 608);
            autoStartBox.Checked = IsAutoStart();
            autoStartBox.CheckedChanged += delegate { SetAutoStart(autoStartBox.Checked); };
            trayBox = Check("Keep running in the tray when closed", 176, 608);
            trayBox.Checked = settings.MinimizeToTray;
            trayBox.CheckedChanged += delegate { settings.MinimizeToTray = trayBox.Checked; settings.Save(); };
            Controls.Add(new Label
            {
                Left = 444, Top = 610, Width = 380, Height = 20, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.Muted,
                Text = "Double-click a zone on the keyboard to pick its color"
            });
        }

        static float ScreenDpi()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX;
        }

        void OnProfileDrawItem(object sender, DrawItemEventArgs e)
        {
            bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool hot = !edit && (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(hot ? Theme.Accent : Theme.Panel)) e.Graphics.FillRectangle(b, e.Bounds);
            string text = e.Index >= 0 ? (string)profileBox.Items[e.Index] : "Choose a profile…";
            Color fore = hot ? Color.White : e.Index >= 0 ? Theme.Text : Theme.Muted;
            var r = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, text, profileBox.Font, r, fore, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        void ShowAbout()
        {
            if (!Visible) ShowFromTray();
            AboutDialog.Show(this, settings.State, engine.DeviceName);
        }

        void BuildTray()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Opening += (s, e) => { BuildTrayMenu(); e.Cancel = false; };
            tray = new NotifyIcon { Text = "LOQ Keyboard RGB", ContextMenuStrip = trayMenu, Visible = true };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowFromTray(); };
        }

        void BuildTrayMenu()
        {
            trayMenu.Items.Clear();
            LightState s = settings.State;
            var open = new ToolStripMenuItem("Open LOQ Keyboard RGB", null, delegate { ShowFromTray(); });
            open.Font = new Font(open.Font, FontStyle.Bold);
            trayMenu.Items.Add(open);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(new ToolStripMenuItem(s.Effect == Effect.Off ? "Turn lights on" : "Turn lights off", null, delegate { ToggleLights(); }));

            var fxMenu = new ToolStripMenuItem("Effect");
            foreach (Effect fx in AllEffects)
            {
                if (fx == Effect.Off) continue;
                if (fx == SoftwareOrder[0]) fxMenu.DropDownItems.Add(new ToolStripSeparator());
                Effect local = fx;
                fxMenu.DropDownItems.Add(new ToolStripMenuItem(fx.ToString(), null, delegate { SetEffect(local); }) { Checked = s.Effect == fx });
            }
            trayMenu.Items.Add(fxMenu);

            var bri = new ToolStripMenuItem("Brightness");
            bri.DropDownItems.Add(new ToolStripMenuItem("Low", null, delegate { SetBrightness(1); }) { Checked = s.Effect != Effect.Off && s.Brightness == 1 });
            bri.DropDownItems.Add(new ToolStripMenuItem("High", null, delegate { SetBrightness(2); }) { Checked = s.Effect != Effect.Off && s.Brightness == 2 });
            trayMenu.Items.Add(bri);

            var prof = new ToolStripMenuItem("Profiles");
            foreach (string name in AppSettings.ListProfiles())
            {
                string n = name;
                prof.DropDownItems.Add(new ToolStripMenuItem(n, null, delegate { LoadProfile(n); }));
            }
            if (prof.DropDownItems.Count == 0) prof.DropDownItems.Add(new ToolStripMenuItem("(none saved)") { Enabled = false });
            trayMenu.Items.Add(prof);

            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(new ToolStripMenuItem("About", null, delegate { ShowAbout(); }));
            trayMenu.Items.Add(new ToolStripMenuItem("Exit", null, delegate { reallyExit = true; Close(); }));
        }

        // ---------------------------------------------------------------- state changes

        int FirstSelected()
        {
            for (int i = 0; i < 4; i++) if (selected[i]) return i;
            return 0;
        }

        void SetEffect(Effect fx)
        {
            LightState s = settings.State;
            if (fx == Effect.Off && s.Effect != Effect.Off) settings.LastOnEffect = s.Effect;
            s.Effect = fx;
            Changed();
        }

        void SetBrightness(int level)
        {
            settings.State.Brightness = level;
            if (settings.State.Effect == Effect.Off) settings.State.Effect = settings.LastOnEffect;
            Changed();
        }

        void ToggleLights()
        {
            SetEffect(settings.State.Effect == Effect.Off ? settings.LastOnEffect : Effect.Off);
        }

        void SelectZone(int z, bool ctrl)
        {
            if (linkBox.Checked)
            {
                for (int i = 0; i < 4; i++) selected[i] = true;
            }
            else if (ctrl)
            {
                selected[z] = !selected[z];
                if (!selected.Any(x => x)) selected[z] = true;
            }
            else
            {
                for (int i = 0; i < 4; i++) selected[i] = i == z;
            }
            RefreshUi();
        }

        void OnZoneButton(int z)
        {
            bool ctrl = (ModifierKeys & Keys.Control) != 0;
            bool alreadyOnlyThis = selected.Count(x => x) == 1 && selected[z];
            if (!ctrl && (alreadyOnlyThis || linkBox.Checked))
            {
                SelectZone(z, false);
                PickCustomColor();
                return;
            }
            SelectZone(z, ctrl);
        }

        void OnZoneClicked(int z, bool ctrl, bool doubleClick)
        {
            if (doubleClick)
            {
                PickCustomColor();
                return;
            }
            SelectZone(z, ctrl);
        }

        void ApplyColor(Color c)
        {
            LightState s = settings.State;
            for (int i = 0; i < 4; i++) if (selected[i]) s.Zones[i] = c;
            if (!Effects.UsesZoneColors(s.Effect)) s.Effect = Effect.Static;
            Changed();
        }

        void CopyToAllZones()
        {
            LightState s = settings.State;
            Color c = s.Zones[FirstSelected()];
            for (int i = 0; i < 4; i++) s.Zones[i] = c;
            if (!Effects.UsesZoneColors(s.Effect)) s.Effect = Effect.Static;
            Changed();
        }

        void PickCustomColor()
        {
            using (var dlg = new ColorDialog { FullOpen = true, AnyColor = true, Color = settings.State.Zones[FirstSelected()] })
            {
                if (customColors != null) dlg.CustomColors = customColors;
                if (dlg.ShowDialog(this) == DialogResult.OK) ApplyColor(dlg.Color);
                customColors = dlg.CustomColors;
            }
        }

        void OnHexKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            Color c;
            if (Gfx.TryParseHex(hexBox.Text, out c)) ApplyColor(c);
            else hexBox.ForeColor = Theme.Bad;
        }

        void Changed()
        {
            RefreshUi();
            ApplyToKeyboard();
            settings.Save();
            UpdateIcons();
        }

        void RefreshUi()
        {
            LightState s = settings.State;
            foreach (var kv in effectButtons) SetChip(kv.Value, kv.Key == s.Effect);

            bool animated = Effects.UsesSpeed(s.Effect);
            for (int i = 0; i < 4; i++)
            {
                speedButtons[i].Enabled = animated;
                SetChip(speedButtons[i], animated && s.Speed == i + 1);
            }

            bool on = s.Effect != Effect.Off;
            SetChip(briLow, on && s.Brightness == 1);
            SetChip(briHigh, on && s.Brightness == 2);

            bool wave = s.Effect == Effect.Wave;
            dirLtr.Enabled = dirRtl.Enabled = wave;
            SetChip(dirLtr, wave && !s.WaveRightToLeft);
            SetChip(dirRtl, wave && s.WaveRightToLeft);

            for (int z = 0; z < 4; z++)
            {
                Button b = zoneButtons[z];
                Color c = s.Zones[z];
                b.BackColor = c;
                b.ForeColor = Gfx.Contrast(c);
                b.FlatAppearance.BorderColor = selected[z] ? Color.White : Theme.Border;
                b.FlatAppearance.BorderSize = selected[z] ? 3 : 1;
                b.FlatAppearance.MouseOverBackColor = Gfx.Blend(c, Color.White, 0.18);
                b.FlatAppearance.MouseDownBackColor = c;
            }

            hexBox.Text = Gfx.ToHex(s.Zones[FirstSelected()]);
            hexBox.ForeColor = Theme.Text;

            view.State = s;
            view.Selected = selected;
            view.Invalidate();
        }

        void ApplyToKeyboard()
        {
            if (!Effects.IsSoftware(settings.State.Effect)) view.LiveFrame = null;
            engine.SetState(settings.State);
        }

        void OnEngineStatus(bool ok, string message)
        {
            if (InvokeRequired)
            {
                if (IsHandleCreated) BeginInvoke(new Action<bool, string>(OnEngineStatus), ok, message);
                return;
            }
            if (!ok)
            {
                statusLabel.ForeColor = Theme.Bad;
                statusLabel.Text = "●  Keyboard not found";
            }
            else if (message != null)
            {
                statusLabel.ForeColor = Theme.Warn;
                statusLabel.Text = "●  " + (message.Length > 48 ? message.Substring(0, 47) + "…" : message);
            }
            else
            {
                statusLabel.ForeColor = Theme.Good;
                statusLabel.Text = "●  Keyboard connected  ·  " + engine.DeviceName;
            }
            tips.SetToolTip(statusLabel, message ?? (ok ? "Lighting applied to the keyboard." : null));
        }

        void UpdateIcons()
        {
            Icon old = currentIcon;
            currentIcon = Gfx.MakeIcon(settings.State);
            Icon = currentIcon;
            tray.Icon = currentIcon;
            if (old != null) old.Dispose();
        }

        // ---------------------------------------------------------------- profiles

        void RefreshProfiles(string select)
        {
            loadingProfiles = true;
            profileBox.Items.Clear();
            foreach (string n in AppSettings.ListProfiles()) profileBox.Items.Add(n);
            if (select != null) profileBox.SelectedItem = select;
            loadingProfiles = false;
        }

        void LoadProfile(string name)
        {
            LightState st = AppSettings.LoadProfile(name);
            if (st == null) return;
            settings.State = st;
            Changed();
        }

        void SaveProfile()
        {
            string current = profileBox.SelectedItem as string;
            string name = Prompt.Show(this, "Save profile", "Profile name:", current ?? "");
            if (string.IsNullOrWhiteSpace(name)) return;
            name = AppSettings.Sanitize(name);
            try
            {
                AppSettings.SaveProfile(name, settings.State);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save the profile:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            RefreshProfiles(name);
        }

        void DeleteProfile()
        {
            string name = profileBox.SelectedItem as string;
            if (name == null) return;
            if (MessageBox.Show(this, "Delete the profile \"" + name + "\"?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try { AppSettings.DeleteProfile(name); } catch { }
            RefreshProfiles(null);
        }

        // ---------------------------------------------------------------- autostart, tray, lifecycle

        static bool IsAutoStart()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(RunName) != null;
        }

        static void SetAutoStart(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\" --tray");
                    else k.DeleteValue(RunName, false);
                }
            }
            catch { }
        }

        void ShowFromTray()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        void StartShowListener()
        {
            showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowEventName);
            var t = new Thread(() =>
            {
                while (true)
                {
                    showSignal.WaitOne();
                    try { if (IsHandleCreated) BeginInvoke(new Action(ShowFromTray)); }
                    catch (InvalidOperationException) { }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        void ScheduleReapply()
        {
            if (!IsHandleCreated) return;
            BeginInvoke(new Action(() => { reapplyTimer.Stop(); reapplyTimer.Start(); }));
        }

        void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume) ScheduleReapply();
        }

        void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.ConsoleConnect) ScheduleReapply();
        }

        protected override void SetVisibleCore(bool value)
        {
            if (startHidden && !IsHandleCreated)
            {
                CreateHandle();
                value = false;
            }
            base.SetVisibleCore(value);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int dark = 1;
                DwmSetWindowAttribute(Handle, 20, ref dark, 4); // immersive dark title bar
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!reallyExit && settings.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (!settings.TrayTipShown)
                {
                    tray.ShowBalloonTip(4000, "Still running in the tray",
                        "LOQ Keyboard RGB keeps your lighting applied after sleep. Right-click the tray icon to exit.", ToolTipIcon.Info);
                    settings.TrayTipShown = true;
                    settings.Save();
                }
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            tray.Visible = false;
            tray.Dispose();
            engine.Dispose();
            base.OnFormClosed(e);
        }
    }

    internal static class Prompt
    {
        public static string Show(IWin32Window owner, string title, string label, string initial)
        {
            using (var f = new Form())
            {
                f.AutoScaleDimensions = new SizeF(96F, 96F);
                f.AutoScaleMode = AutoScaleMode.Dpi;
                f.Font = new Font("Segoe UI", 9F);
                f.Text = title;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MinimizeBox = f.MaximizeBox = false;
                f.ShowInTaskbar = false;
                f.StartPosition = FormStartPosition.CenterParent;
                f.ClientSize = new Size(320, 118);
                f.BackColor = Theme.Bg;
                f.ForeColor = Theme.Text;

                var l = new Label { Text = label, Left = 14, Top = 14, AutoSize = true };
                var t = new TextBox { Left = 14, Top = 38, Width = 292, Text = initial, BackColor = Theme.Panel, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
                var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, Left = 150, Top = 76, Width = 75, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Theme.Accent, ForeColor = Color.White };
                ok.FlatAppearance.BorderColor = Theme.Accent;
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 231, Top = 76, Width = 75, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Theme.Panel, ForeColor = Theme.Text };
                cancel.FlatAppearance.BorderColor = Theme.Border;
                f.Controls.AddRange(new Control[] { l, t, ok, cancel });
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                return f.ShowDialog(owner) == DialogResult.OK ? t.Text.Trim() : null;
            }
        }
    }
}
