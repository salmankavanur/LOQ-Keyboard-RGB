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
        static readonly Effect[] SoftwareOrder = { Effect.Audio, Effect.Ambient, Effect.Monitor, Effect.Drift, Effect.Fade, Effect.Strobe };
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
            Text = Program.MainWindowTitle; // other instances find this window by its title
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
            StartBatteryWatch();
            StartAppRules();
            StartLenovoMode();
            StartPowerRules();
            StartUpdates();
        }

        // ---------------------------------------------------------------- automatic updates

        UpdateService updates;
        Button updateBtn;

        void StartUpdates()
        {
            updates = new UpdateService(settings, this, () => !Visible, () => { reallyExit = true; Close(); });
            updates.Changed += RefreshUpdateButton;

            // First start after an update: say so once.
            string current = Updates.Current.ToString(3);
            string last = settings.GetString(UpdateService.LastRunVersionKey, null);
            if (last != null && last != current && Updates.ParseTag(current) > Updates.ParseTag(last))
                tray.ShowBalloonTip(4000, "LOQ Keyboard RGB updated", "Now running version " + current + ".", ToolTipIcon.Info);
            if (last != current)
            {
                settings.SetString(UpdateService.LastRunVersionKey, current);
                settings.Save();
            }
        }

        void RefreshUpdateButton()
        {
            Updates.Release r = updates.Available;
            string status = updates.Status ?? "";
            bool working = status.StartsWith("Downloading") || status.StartsWith("Installing");
            updateBtn.Visible = r != null || working;
            updateBtn.Text = working ? (status.Length > 26 ? status.Substring(0, 26) : status)
                           : r != null ? "⬆  Update to " + r.DisplayVersion : "";
            updateBtn.Enabled = !working;
            SetChip(updateBtn, !working);
            tips.SetToolTip(updateBtn, status.StartsWith("Update failed") ? status : "Download, verify and install the new version. The app restarts by itself.");
            if (status.StartsWith("Update failed") && r != null) updateBtn.Text = "⬆  Retry update";
        }

        // ---------------------------------------------------------------- Lenovo power mode link (#9)

        PowerModeWatcher lenovoMode;
        LightState modeState;
        string modeProfile;
        bool modeSuspended;

        void StartLenovoMode()
        {
            lenovoMode = new PowerModeWatcher();
            lenovoMode.Changed += m =>
            {
                if (IsHandleCreated) BeginInvoke(new Action(() => { modeSuspended = false; EvaluateLenovoMode(); }));
            };
            EvaluateLenovoMode();
        }

        void EvaluateLenovoMode()
        {
            string profile = null;
            if (settings.GetBool(PowerModeWatcher.EnabledKey, false) && !modeSuspended && lenovoMode.Current != LenovoPowerMode.Unknown)
                profile = settings.GetString(PowerModeWatcher.SettingKey(lenovoMode.Current), null);
            LightState st = string.IsNullOrEmpty(profile) ? null : AppSettings.LoadProfile(profile);
            if (st == null) profile = null;
            if (profile == modeProfile && (st == null) == (modeState == null)) return;
            modeProfile = profile;
            modeState = st;
            ApplyToKeyboard();
            RefreshUi();
            RenderStatus();
            UpdateIcons();
        }

        // ---------------------------------------------------------------- power & schedule rules (#8)

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO { public int cbSize; public uint dwTime; }

        [StructLayout(LayoutKind.Sequential)]
        struct POWERBROADCAST_SETTING { public Guid PowerSetting; public int DataLength; public int Data; }

        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
        [DllImport("user32.dll")] static extern IntPtr RegisterPowerSettingNotification(IntPtr recipient, ref Guid setting, int flags);
        [DllImport("user32.dll")] static extern bool UnregisterPowerSettingNotification(IntPtr handle);

        static Guid ConsoleDisplayState = new Guid("6FE69556-704A-47A0-8F24-C28D936FDA47");
        const int WM_POWERBROADCAST = 0x0218, PBT_POWERSETTINGCHANGE = 0x8013;

        readonly System.Windows.Forms.Timer policyTimer = new System.Windows.Forms.Timer();
        IntPtr displayNotify;
        bool idleOff, screenOff, onBattery, nightSuspended;
        LightState nightState;
        string nightName;

        void StartPowerRules()
        {
            policyTimer.Interval = 1000;
            policyTimer.Tick += delegate { EvaluatePolicy(); };
            policyTimer.Start();
            EvaluatePolicy();
        }

        static TimeSpan IdleTime()
        {
            var info = new LASTINPUTINFO { cbSize = Marshal.SizeOf(typeof(LASTINPUTINFO)) };
            if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
            return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
        }

        static bool InWindow(string from, string to, DateTime now)
        {
            TimeSpan a, b;
            if (!TimeSpan.TryParse(from, out a) || !TimeSpan.TryParse(to, out b) || a == b) return false;
            TimeSpan t = now.TimeOfDay;
            return a < b ? t >= a && t < b : t >= a || t < b; // window may cross midnight
        }

        /// <summary>Re-evaluates idle, battery and night rules; re-applies the lighting when anything changed.</summary>
        void EvaluatePolicy()
        {
            int idleMinutes = settings.GetInt(Opt.IdleMinutes, 0);
            bool newIdle = idleMinutes > 0 && IdleTime().TotalMinutes >= idleMinutes;

            PowerStatus p = SystemInformation.PowerStatus;
            bool newBattery = p.PowerLineStatus == PowerLineStatus.Offline &&
                              (p.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == 0;

            bool nightOn = settings.GetBool(Opt.NightEnabled, false) &&
                           InWindow(settings.GetString(Opt.NightFrom, "22:00"), settings.GetString(Opt.NightTo, "07:00"), DateTime.Now);
            if (!nightOn) nightSuspended = false; // re-arm for the next night
            string newNightName = nightOn && !nightSuspended ? settings.GetString(Opt.NightProfile, null) : null;

            bool screen = screenOff && settings.GetBool(Opt.OffWithScreen, true);
            if (newIdle == idleOff && newBattery == onBattery && newNightName == nightName && screen == screenOff) return;

            idleOff = newIdle;
            onBattery = newBattery;
            screenOff = screen;
            if (newNightName != nightName)
            {
                nightName = newNightName;
                nightState = nightName != null ? AppSettings.LoadProfile(nightName) : null;
                if (nightState == null) nightName = null;
            }
            ApplyToKeyboard();
            RefreshUi();
            RenderStatus();
            UpdateIcons();
        }

        string PowerRuleText()
        {
            if (settings.State.Effect == Effect.Off) return null;
            if (screenOff) return "Lights off while the screen is off";
            if (idleOff) return "Lights off: no input for " + settings.GetInt(Opt.IdleMinutes, 0) + " min";
            if (onBattery)
            {
                int mode = settings.GetInt(Opt.BatteryMode, 0);
                if (mode == 2) return "On battery: lights off";
                if (mode == 1) return "On battery: low brightness";
            }
            if (autoState == null && modeState != null) return lenovoMode.Current + " mode: " + modeProfile;
            if (autoState == null && nightState != null) return "Night profile: " + nightName;
            return null;
        }

        void RegisterDisplayNotification()
        {
            if (displayNotify == IntPtr.Zero)
                displayNotify = RegisterPowerSettingNotification(Handle, ref ConsoleDisplayState, 0 /* window handle */);
        }

        void OnDisplayState(int state)
        {
            // 0 = off, 1 = on, 2 = dimmed
            bool off = state == 0 && settings.GetBool(Opt.OffWithScreen, true);
            if (off == screenOff) return;
            screenOff = off;
            ApplyToKeyboard();
            RenderStatus();
        }

        // ---------------------------------------------------------------- app profiles (#6)

        ForegroundWatcher foreground;
        List<AppRule> appRules = new List<AppRule>();
        string autoProfile, autoApp;
        LightState autoState;

        void StartAppRules()
        {
            appRules = AppRule.Parse(settings.GetString(Opt.AppRules, ""));
            foreground = new ForegroundWatcher();
            foreground.Changed += OnForegroundApp;
            foreground.Check();
        }

        void OnForegroundApp(string app)
        {
            if (app.Equals(System.Diagnostics.Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase))
                return; // keep whatever is showing while the user looks at this window
            AppRule rule = settings.GetBool(Opt.AppRulesEnabled, true) && settings.State.Effect != Effect.Off
                ? appRules.FirstOrDefault(r => r.App.Equals(app, StringComparison.OrdinalIgnoreCase))
                : null;
            if (rule == null) { ClearAutoProfile(); return; }
            if (rule.Profile == autoProfile && app == autoApp) return;

            LightState st = AppSettings.LoadProfile(rule.Profile);
            if (st == null) { ClearAutoProfile(); return; }
            autoProfile = rule.Profile;
            autoApp = app;
            autoState = st;
            ApplyToKeyboard();
            RefreshUi();
            RenderStatus();
            UpdateIcons();
        }

        /// <summary>Returns to the user's own lighting if an app profile is active.</summary>
        void ClearAutoProfile()
        {
            if (autoProfile == null) return;
            autoProfile = autoApp = null;
            autoState = null;
            ApplyToKeyboard();
            RefreshUi();
            RenderStatus();
            UpdateIcons();
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
            // second row: widths follow the labels, leftover space shared evenly across the 394 px row
            const int rowWidth = 394, gap = 6;
            int[] widths = SoftwareOrder.Select(e => TextRenderer.MeasureText(e.ToString(), Font).Width + 18).ToArray();
            int extra = Math.Max(0, (rowWidth - widths.Sum() - gap * (widths.Length - 1)) / widths.Length);
            int x = 16;
            for (int i = 0; i < SoftwareOrder.Length; i++)
            {
                Effect fx = SoftwareOrder[i];
                Button b = Chip(fx.ToString(), x, 390, widths[i] + extra, 34);
                x += widths[i] + extra + gap;
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
            tips.SetToolTip(view, "Click a zone to select it · Ctrl+click to select several · double-click to pick its color");
            updateBtn = Chip("", 520, 602, 176, 32);
            updateBtn.Visible = false;
            updateBtn.Click += delegate { if (updates != null) updates.Install(); };
            Button settingsBtn = Chip("⚙  Settings", 704, 602, 120, 32);
            settingsBtn.Click += delegate { ShowSettings(); };
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

        void ShowSettings()
        {
            if (!Visible) ShowFromTray();
            using (var dlg = new SettingsDialog(settings, OnOptionsChanged, c => engine.Flash(c, 3)))
                dlg.ShowDialog(this);
        }

        /// <summary>Called when an option changes in the Settings window.</summary>
        void OnOptionsChanged()
        {
            UpdateShellHook();
            if (IsHandleCreated) Hotkeys.RegisterAll(Handle, settings);
            nightName = null; // reload the night profile in case it changed
            nightState = null;
            modeProfile = null; // likewise for the power-mode profile
            modeState = null;
            if (lenovoMode != null) EvaluateLenovoMode();
            if (policyTimer.Enabled) EvaluatePolicy();
            ApplyToKeyboard(); // always re-apply: a rule may have just been switched off
            RefreshUi();
            RenderStatus();
            UpdateIcons();
            appRules = AppRule.Parse(settings.GetString(Opt.AppRules, ""));
            autoProfile = null; // force re-evaluation with the new rules
            if (foreground != null) foreground.Check();
        }

        // ---------------------------------------------------------------- notifications (#5)

        [DllImport("user32.dll")] static extern bool RegisterShellHookWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool DeregisterShellHookWindow(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int RegisterWindowMessage(string name);

        const int WM_COPYDATA = 0x004A;
        const int HSHELL_FLASH = 0x8006;
        int shellHookMsg;
        bool shellHooked;
        DateTime lastAttentionFlash = DateTime.MinValue;
        readonly System.Windows.Forms.Timer batteryTimer = new System.Windows.Forms.Timer();
        int batteryWarnedAt = 100;

        [StructLayout(LayoutKind.Sequential)]
        struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        void UpdateShellHook()
        {
            if (!IsHandleCreated) return;
            bool want = settings.GetBool(Opt.FlashOnAttention, true);
            if (want && !shellHooked)
            {
                if (shellHookMsg == 0) shellHookMsg = RegisterWindowMessage("SHELLHOOK");
                shellHooked = RegisterShellHookWindow(Handle);
            }
            else if (!want && shellHooked)
            {
                DeregisterShellHookWindow(Handle);
                shellHooked = false;
            }
        }

        void StartBatteryWatch()
        {
            batteryTimer.Interval = 30000;
            batteryTimer.Tick += delegate { CheckBattery(); };
            batteryTimer.Start();
        }

        void CheckBattery()
        {
            PowerStatus p = SystemInformation.PowerStatus;
            if ((p.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) != 0 || p.BatteryLifePercent > 1f) return;
            int pct = (int)Math.Round(p.BatteryLifePercent * 100);
            if (p.PowerLineStatus == PowerLineStatus.Online)
            {
                batteryWarnedAt = 100; // re-arm once charging
                return;
            }
            if (!settings.GetBool(Opt.LowBatteryFlash, true)) return;
            if (pct <= 10 && batteryWarnedAt > 10)
            {
                batteryWarnedAt = 10;
                engine.Flash(Color.Red, 5);
            }
            else if (pct <= 20 && batteryWarnedAt > 20)
            {
                batteryWarnedAt = 20;
                engine.Flash(Color.FromArgb(255, 100, 0), 3);
            }
        }

        /// <summary>Commands handed over by another LOQKeyboardRGB.exe started with command-line switches (newline-separated).</summary>
        void HandleRemoteCommand(string command)
        {
            string[] p = command.Split('\n');
            Color c;
            switch (p[0])
            {
                case "flash":
                    int times;
                    if (p.Length < 2 || !Gfx.TryParseHex(p[1], out c)) return;
                    if (p.Length < 3 || !int.TryParse(p[2], out times)) times = 3;
                    engine.Flash(c, times);
                    break;
                case "profile":
                    if (p.Length >= 2) LoadProfile(p[1]);
                    break;
                case "color":
                    if (p.Length < 2 || !Gfx.TryParseHex(p[1], out c)) return;
                    for (int i = 0; i < 4; i++) settings.State.Zones[i] = c;
                    settings.State.Effect = Effect.Static;
                    Changed();
                    break;
                case "toggle":
                    ToggleLights();
                    break;
                case "exit": // sent by the installer / uninstaller
                    reallyExit = true;
                    BeginInvoke(new Action(Close));
                    break;
                case "off":
                    if (settings.State.Effect != Effect.Off) SetEffect(Effect.Off);
                    break;
                case "on":
                    if (settings.State.Effect == Effect.Off) SetEffect(settings.LastOnEffect);
                    break;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_POWERBROADCAST && m.WParam.ToInt32() == PBT_POWERSETTINGCHANGE)
            {
                var ps = (POWERBROADCAST_SETTING)Marshal.PtrToStructure(m.LParam, typeof(POWERBROADCAST_SETTING));
                if (ps.PowerSetting == ConsoleDisplayState) OnDisplayState(ps.Data);
            }
            if (m.Msg == Hotkeys.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (Enum.IsDefined(typeof(HotkeyAction), id)) OnHotkey((HotkeyAction)id);
                return;
            }
            if (m.Msg == WM_COPYDATA)
            {
                try
                {
                    var cds = (COPYDATASTRUCT)Marshal.PtrToStructure(m.LParam, typeof(COPYDATASTRUCT));
                    if (cds.dwData == (IntPtr)Program.CopyDataTag && cds.cbData > 0 && cds.cbData < 4096)
                        HandleRemoteCommand(Marshal.PtrToStringUni(cds.lpData, cds.cbData / 2));
                    m.Result = (IntPtr)1;
                }
                catch { }
                return;
            }
            if (shellHookMsg != 0 && m.Msg == shellHookMsg && (m.WParam.ToInt64() & 0xFFFF) == (HSHELL_FLASH & 0xFFFF))
            {
                // Taskbar buttons blink repeatedly; flash at most once every 8 seconds.
                if ((DateTime.UtcNow - lastAttentionFlash).TotalSeconds > 8)
                {
                    lastAttentionFlash = DateTime.UtcNow;
                    engine.Flash(settings.GetColor(Opt.FlashColor, Opt.DefaultFlashColor), 3);
                }
            }
            base.WndProc(ref m);
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
            // A manual change wins over an active app profile until the next app switch.
            if (autoProfile != null)
            {
                autoProfile = autoApp = null;
                autoState = null;
                RenderStatus();
            }
            // ...pauses the power-mode profile until the next Fn+Q...
            if (modeState != null)
            {
                modeSuspended = true;
                modeState = null;
                modeProfile = null;
                RenderStatus();
            }
            // ...and pauses the night profile until the next night.
            if (nightState != null)
            {
                nightSuspended = true;
                nightState = null;
                nightName = null;
                RenderStatus();
            }
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

            view.State = EffectiveState();
            view.Selected = selected;
            view.Invalidate();
        }

        void ApplyToKeyboard()
        {
            LightState s = EffectiveState();
            if (!Effects.IsSoftware(s.Effect)) view.LiveFrame = null;
            engine.SetState(s);
        }

        /// <summary>
        /// What the keyboard should show right now, after all automatic rules:
        /// lights turned off by the user > idle / screen off > battery mode > app profile > night profile > user's lighting.
        /// </summary>
        LightState EffectiveState()
        {
            LightState s = settings.State;
            if (s.Effect == Effect.Off) return s;
            if (autoState != null) s = autoState;
            else if (modeState != null) s = modeState;
            else if (nightState != null) s = nightState;
            if (idleOff || screenOff) return new LightState { Effect = Effect.Off };
            if (onBattery)
            {
                int mode = settings.GetInt(Opt.BatteryMode, 0);
                if (mode == 2) return new LightState { Effect = Effect.Off };
                if (mode == 1 && s.Brightness != 1)
                {
                    s = s.Clone();
                    s.Brightness = 1;
                }
            }
            return s;
        }

        bool engineOk;
        string engineMessage;

        void OnEngineStatus(bool ok, string message)
        {
            if (InvokeRequired)
            {
                if (IsHandleCreated) BeginInvoke(new Action<bool, string>(OnEngineStatus), ok, message);
                return;
            }
            engineOk = ok;
            engineMessage = message;
            RenderStatus();
        }

        void RenderStatus()
        {
            bool ok = engineOk;
            string message = engineMessage;
            string trayText = "LOQ Keyboard RGB" + (autoProfile != null ? " · " + autoProfile : "");
            if (tray != null) tray.Text = trayText.Length > 63 ? trayText.Substring(0, 63) : trayText;
            string rule = PowerRuleText();
            if (ok && message == null && rule != null)
            {
                statusLabel.ForeColor = Theme.Accent;
                statusLabel.Text = "●  " + rule;
                tips.SetToolTip(statusLabel, "An automatic rule from Settings is active. Your own lighting returns when it ends.");
                return;
            }
            if (ok && message == null && autoProfile != null)
            {
                statusLabel.ForeColor = Theme.Accent;
                statusLabel.Text = "●  Auto profile: " + autoProfile + "  (" + autoApp + ")";
                tips.SetToolTip(statusLabel, "\"" + autoApp + "\" is in front, so its app profile is active. Your own lighting returns when you switch away.");
                return;
            }
            if (!ok)
            {
                statusLabel.ForeColor = Theme.Bad;
                statusLabel.Text = message != null && message.Contains("per-key")
                    ? "●  Per-key RGB keyboard: not supported yet"
                    : "●  Keyboard not found";
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
            currentIcon = Gfx.MakeIcon(EffectiveState());
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
            lastProfile = name;
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
            shellHooked = false;
            UpdateShellHook();
            Hotkeys.RegisterAll(Handle, settings);
            displayNotify = IntPtr.Zero;
            RegisterDisplayNotification();
        }

        // ---------------------------------------------------------------- hotkeys (#7)

        string lastProfile;

        void OnHotkey(HotkeyAction action)
        {
            switch (action)
            {
                case HotkeyAction.Toggle: ToggleLights(); break;
                case HotkeyAction.NextProfile: CycleProfile(1); break;
                case HotkeyAction.PreviousProfile: CycleProfile(-1); break;
                case HotkeyAction.BrightnessUp: SetBrightness(2); break;
                case HotkeyAction.BrightnessDown: SetBrightness(1); break;
                case HotkeyAction.NextEffect:
                    Effect[] cycle = AllEffects.Where(e => e != Effect.Off).ToArray();
                    int i = Array.IndexOf(cycle, settings.State.Effect);
                    SetEffect(cycle[(i + 1) % cycle.Length]);
                    break;
            }
        }

        void CycleProfile(int step)
        {
            string[] names = AppSettings.ListProfiles();
            if (names.Length == 0) return;
            string current = profileBox.SelectedItem as string ?? lastProfile;
            int i = current == null ? -1 : Array.FindIndex(names, n => n.Equals(current, StringComparison.OrdinalIgnoreCase));
            i = i < 0 ? (step > 0 ? 0 : names.Length - 1) : (i + step + names.Length) % names.Length;
            RefreshProfiles(names[i]);
            LoadProfile(names[i]);
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
            if (shellHooked) DeregisterShellHookWindow(Handle);
            Hotkeys.UnregisterAll(Handle);
            policyTimer.Stop();
            if (displayNotify != IntPtr.Zero) UnregisterPowerSettingNotification(displayNotify);
            batteryTimer.Stop();
            tray.Visible = false;
            tray.Dispose();
            if (foreground != null) foreground.Dispose();
            if (lenovoMode != null) lenovoMode.Dispose();
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
