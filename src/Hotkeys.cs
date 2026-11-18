using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LoqRgb
{
    internal enum HotkeyAction
    {
        Toggle = 1, NextProfile, PreviousProfile, BrightnessUp, BrightnessDown, NextEffect
    }

    /// <summary>System-wide hotkeys (RegisterHotKey). Bindings are stored as Keys values in the settings.</summary>
    internal static class Hotkeys
    {
        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        public const int WM_HOTKEY = 0x0312;
        public const string EnabledKey = "hotkeysEnabled";

        public static readonly HotkeyAction[] All =
        {
            HotkeyAction.Toggle, HotkeyAction.NextProfile, HotkeyAction.PreviousProfile,
            HotkeyAction.BrightnessUp, HotkeyAction.BrightnessDown, HotkeyAction.NextEffect
        };

        /// <summary>Actions whose key combination could not be registered (taken by another program).</summary>
        public static readonly HashSet<HotkeyAction> Conflicts = new HashSet<HotkeyAction>();

        public static string Label(HotkeyAction a)
        {
            switch (a)
            {
                case HotkeyAction.Toggle: return "Lights on / off";
                case HotkeyAction.NextProfile: return "Next profile";
                case HotkeyAction.PreviousProfile: return "Previous profile";
                case HotkeyAction.BrightnessUp: return "Brightness high";
                case HotkeyAction.BrightnessDown: return "Brightness low";
                default: return "Next effect";
            }
        }

        public static Keys Default(HotkeyAction a)
        {
            const Keys mods = Keys.Control | Keys.Alt;
            switch (a)
            {
                case HotkeyAction.Toggle: return mods | Keys.L;
                case HotkeyAction.NextProfile: return mods | Keys.Right;
                case HotkeyAction.PreviousProfile: return mods | Keys.Left;
                case HotkeyAction.BrightnessUp: return mods | Keys.Up;
                case HotkeyAction.BrightnessDown: return mods | Keys.Down;
                default: return mods | Keys.E;
            }
        }

        static string SettingKey(HotkeyAction a) { return "hotkey." + a; }

        /// <summary>The binding for an action; Keys.None means unassigned.</summary>
        public static Keys Get(AppSettings s, HotkeyAction a)
        {
            return (Keys)s.GetInt(SettingKey(a), (int)Default(a));
        }

        public static void Set(AppSettings s, HotkeyAction a, Keys k)
        {
            s.SetInt(SettingKey(a), (int)k);
        }

        public static string Describe(Keys k)
        {
            if ((k & Keys.KeyCode) == Keys.None) return "None";
            var parts = new List<string>();
            if ((k & Keys.Control) != 0) parts.Add("Ctrl");
            if ((k & Keys.Alt) != 0) parts.Add("Alt");
            if ((k & Keys.Shift) != 0) parts.Add("Shift");
            Keys code = k & Keys.KeyCode;
            string name;
            switch (code)
            {
                case Keys.Left: name = "←"; break;
                case Keys.Right: name = "→"; break;
                case Keys.Up: name = "↑"; break;
                case Keys.Down: name = "↓"; break;
                default:
                    name = code >= Keys.D0 && code <= Keys.D9 ? ((char)('0' + (code - Keys.D0))).ToString() : code.ToString();
                    break;
            }
            parts.Add(name);
            return string.Join(" + ", parts);
        }

        /// <summary>A usable global hotkey needs a key plus at least Ctrl or Alt (or an F-key).</summary>
        public static bool IsValid(Keys k)
        {
            Keys code = k & Keys.KeyCode;
            if (code == Keys.None || code == Keys.ControlKey || code == Keys.Menu || code == Keys.ShiftKey) return false;
            bool fkey = code >= Keys.F1 && code <= Keys.F24;
            return fkey || (k & (Keys.Control | Keys.Alt)) != 0;
        }

        public static void RegisterAll(IntPtr hwnd, AppSettings s)
        {
            UnregisterAll(hwnd);
            Conflicts.Clear();
            if (!s.GetBool(EnabledKey, true)) return;
            foreach (HotkeyAction a in All)
            {
                Keys k = Get(s, a);
                if (!IsValid(k)) continue;
                uint mods = 0x4000; // MOD_NOREPEAT
                if ((k & Keys.Alt) != 0) mods |= 1;
                if ((k & Keys.Control) != 0) mods |= 2;
                if ((k & Keys.Shift) != 0) mods |= 4;
                if (!RegisterHotKey(hwnd, (int)a, mods, (uint)(k & Keys.KeyCode))) Conflicts.Add(a);
            }
        }

        public static void UnregisterAll(IntPtr hwnd)
        {
            foreach (HotkeyAction a in All) UnregisterHotKey(hwnd, (int)a);
        }
    }

    /// <summary>A box that records a key combination when focused.</summary>
    internal sealed class HotkeyBox : TextBox
    {
        Keys value;
        public event Action<Keys> ValueChanged;

        public HotkeyBox()
        {
            ReadOnly = true;
            BackColor = Theme.Panel;
            ForeColor = Theme.Text;
            BorderStyle = BorderStyle.FixedSingle;
            Cursor = Cursors.Hand;
            ShortcutsEnabled = false;
            TextAlign = HorizontalAlignment.Center;
        }

        public Keys Value
        {
            get { return value; }
            set { this.value = value; Text = Hotkeys.Describe(value); }
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Text = "Press keys…";
            BackColor = Theme.PanelHover;
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Text = Hotkeys.Describe(value);
            BackColor = Theme.Panel;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys code = keyData & Keys.KeyCode;
            if (code == Keys.Escape) { Parent.Focus(); return true; }                     // cancel
            if (code == Keys.Back || code == Keys.Delete) { Commit(Keys.None); return true; } // clear
            if (code == Keys.ControlKey || code == Keys.Menu || code == Keys.ShiftKey || code == Keys.LWin || code == Keys.RWin)
                return true; // wait for the real key
            if (!Hotkeys.IsValid(keyData))
            {
                Text = "Add Ctrl or Alt";
                return true;
            }
            Commit(keyData);
            return true;
        }

        void Commit(Keys k)
        {
            Value = k;
            if (ValueChanged != null) ValueChanged(k);
            if (Parent != null) Parent.Focus();
        }
    }
}
