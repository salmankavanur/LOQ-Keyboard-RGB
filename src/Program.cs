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

        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        /// <summary>
        /// LOQKeyboardRGB.exe                 open the app
        ///   --tray                           start hidden in the tray (used by "Start with Windows")
        ///   --off | --on | --toggle          switch the backlight without opening the window
        ///   --profile "Name"                 apply a saved profile
        ///   --color FF0000                   set all zones to one static colour
        /// Exit code 0 = applied, 1 = keyboard not found / write failed, 2 = bad argument.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            bool tray = false, off = false, on = false, toggle = false;
            string profile = null, color = null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "--tray") tray = true;
                else if (a == "--off") off = true;
                else if (a == "--on") on = true;
                else if (a == "--toggle") toggle = true;
                else if (a == "--profile" && i + 1 < args.Length) profile = args[++i];
                else if (a == "--color" && i + 1 < args.Length) color = args[++i];
            }

            if (off || on || toggle || profile != null || color != null)
                return RunCommand(off, on, toggle, profile, color);

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
