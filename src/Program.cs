using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LoqRgb
{
    internal static class Program
    {
        public const string MutexName = @"Local\LOQKeyboardRGB.Instance";
        public const string ShowEventName = @"Local\LOQKeyboardRGB.Show";

        public const string MainWindowTitle = "LOQ Keyboard RGB";
        public const int CopyDataTag = 0x4C514B; // "LQK"

        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder s, int max);
        [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr hwnd, int msg, IntPtr wParam, ref CopyData lParam, int flags, int timeout, out IntPtr result);

        [StructLayout(LayoutKind.Sequential)]
        struct CopyData
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        /// <summary>
        /// LOQKeyboardRGB.exe                 open the app
        ///   --tray                           start hidden in the tray (used by "Start with Windows")
        ///   --off | --on | --toggle          switch the backlight without opening the window
        ///   --profile "Name"                 apply a saved profile
        ///   --color FF0000                   set all zones to one static colour
        ///   --flash 00FF00 [--times 3]       flash the keyboard, then return to the current lighting
        /// If the app is already running, the command is handed to it.
        /// Exit code 0 = applied, 1 = keyboard not found / write failed, 2 = bad argument.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            bool tray = false, off = false, on = false, toggle = false, install = false, uninstall = false, quiet = false;
            string profile = null, color = null, flash = null;
            int times = 3, waitPid = 0;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "--install") install = true;
                else if (a == "--uninstall") uninstall = true;
                else if (a == "--quiet") quiet = true;
                else if (a == "--wait-pid" && i + 1 < args.Length && int.TryParse(args[i + 1], out waitPid)) i++;
                else if (a == "--tray") tray = true;
                else if (a == "--off") off = true;
                else if (a == "--on") on = true;
                else if (a == "--toggle") toggle = true;
                else if (a == "--profile" && i + 1 < args.Length) profile = args[++i];
                else if (a == "--color" && i + 1 < args.Length) color = args[++i];
                else if (a == "--flash" && i + 1 < args.Length) flash = args[++i];
                else if (a == "--times" && i + 1 < args.Length && int.TryParse(args[i + 1], out times)) i++;
            }

            if (waitPid != 0)
            {
                // started by the installer: let the old copy exit and release the single-instance mutex
                try { System.Diagnostics.Process.GetProcessById(waitPid).WaitForExit(10000); } catch { }
            }

            if (uninstall || install)
            {
                Application.EnableVisualStyles();
                IntPtr other = FindRunningWindow();
                if (other != IntPtr.Zero)
                {
                    SendCommand(other, "exit"); // close the running copy so its files can be replaced/removed
                    Thread.Sleep(1500);
                }
                if (uninstall) return Installer.Uninstall(quiet);
                string exe = Installer.Install();
                if (!quiet) System.Diagnostics.Process.Start(exe);
                return 0;
            }

            if (off || on || toggle || profile != null || color != null || flash != null)
            {
                string command =
                    flash != null ? "flash\n" + flash + "\n" + times :
                    profile != null ? "profile\n" + profile :
                    color != null ? "color\n" + color :
                    toggle ? "toggle" : off ? "off" : "on";
                IntPtr running = FindRunningWindow();
                if (running != IntPtr.Zero && SendCommand(running, command)) return 0;
                if (flash != null) return RunFlash(flash, times);
                return RunCommand(off, on, toggle, profile, color);
            }

            bool created;
            using (var mutex = new Mutex(true, MutexName, out created))
            {
                if (!created)
                {
                    try { using (var ev = EventWaitHandle.OpenExisting(ShowEventName)) ev.Set(); }
                    catch { }
                    return 0;
                }
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(tray));
                GC.KeepAlive(mutex);
            }
            return 0;
        }

        /// <summary>The main window of another running instance (it may be hidden in the tray).</summary>
        static IntPtr FindRunningWindow()
        {
            var ids = new System.Collections.Generic.HashSet<uint>();
            int self = System.Diagnostics.Process.GetCurrentProcess().Id;
            foreach (var p in System.Diagnostics.Process.GetProcessesByName(System.Diagnostics.Process.GetCurrentProcess().ProcessName))
                if (p.Id != self) ids.Add((uint)p.Id);
            if (ids.Count == 0) return IntPtr.Zero;

            IntPtr found = IntPtr.Zero;
            var title = new System.Text.StringBuilder(64);
            EnumWindows((h, l) =>
            {
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (!ids.Contains(pid)) return true;
                title.Length = 0;
                GetWindowText(h, title, 64);
                if (title.ToString() != MainWindowTitle) return true;
                found = h;
                return false;
            }, IntPtr.Zero);
            return found;
        }

        static bool SendCommand(IntPtr hwnd, string command)
        {
            IntPtr text = Marshal.StringToHGlobalUni(command);
            try
            {
                var cds = new CopyData { dwData = (IntPtr)CopyDataTag, cbData = command.Length * 2, lpData = text };
                IntPtr result;
                IntPtr ok = SendMessageTimeout(hwnd, 0x004A /* WM_COPYDATA */, IntPtr.Zero, ref cds, 0x0002 /* ABORTIFHUNG */, 3000, out result);
                return ok != IntPtr.Zero && result != IntPtr.Zero;
            }
            finally
            {
                Marshal.FreeHGlobal(text);
            }
        }

        /// <summary>Flash without the app running: show the saved lighting, flash, restore it.</summary>
        static int RunFlash(string hex, int times)
        {
            Color c;
            if (!Gfx.TryParseHex(hex, out c)) return 2;
            times = Math.Max(1, Math.Min(20, times));
            LightState s = AppSettings.Load().State;
            if (Effects.IsSoftware(s.Effect)) s.Effect = Effect.Static; // don't keep streaming after we exit
            bool ok = false;
            using (var engine = new LightingEngine())
            {
                engine.StatusChanged += (good, msg) => ok = good;
                engine.SetState(s);
                engine.Flash(c, times);
                Thread.Sleep(times * 340 + 300);
            }
            return ok ? 0 : 1;
        }

        static int RunCommand(bool off, bool on, bool toggle, string profile, string color)
        {
            AppSettings settings = AppSettings.Load();
            LightState s = settings.State;

            if (toggle)
            {
                if (s.Effect == Effect.Off) on = true; else off = true;
            }

            if (profile != null)
            {
                LightState p = AppSettings.LoadProfile(profile);
                if (p == null) return 2;
                settings.State = s = p;
            }
            else if (color != null)
            {
                Color c;
                if (!Gfx.TryParseHex(color, out c)) return 2;
                for (int z = 0; z < 4; z++) s.Zones[z] = c;
                s.Effect = Effect.Static;
            }
            else if (off)
            {
                if (s.Effect != Effect.Off) settings.LastOnEffect = s.Effect;
                s.Effect = Effect.Off;
            }
            else if (on && s.Effect == Effect.Off)
            {
                s.Effect = settings.LastOnEffect;
            }

            settings.Save();
            using (var kb = new KeyboardController())
            {
                string err;
                return kb.Apply(s, out err) ? 0 : 1;
            }
        }
    }
}
