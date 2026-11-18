using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LoqRgb
{
    /// <summary>One "when this app is in front, use this profile" rule.</summary>
    internal sealed class AppRule
    {
        public string App;      // process name without ".exe", e.g. "valorant"
        public string Profile;

        public override string ToString()
        {
            return App + ".exe  →  " + Profile;
        }

        public static string Normalize(string app)
        {
            app = (app ?? "").Trim();
            if (app.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) app = app.Substring(0, app.Length - 4);
            return app;
        }

        // Stored as "app>profile|app>profile": '>' and '|' can't appear in file or process names.
        public static List<AppRule> Parse(string s)
        {
            var list = new List<AppRule>();
            if (string.IsNullOrEmpty(s)) return list;
            foreach (string part in s.Split('|'))
            {
                int i = part.IndexOf('>');
                if (i <= 0 || i == part.Length - 1) continue;
                list.Add(new AppRule { App = Normalize(part.Substring(0, i)), Profile = part.Substring(i + 1) });
            }
            return list;
        }

        public static string Serialize(IEnumerable<AppRule> rules)
        {
            return string.Join("|", rules.Select(r => r.App + ">" + r.Profile));
        }

        /// <summary>Names of running apps that have a visible window, for the rule editor.</summary>
        public static string[] RunningApps()
        {
            int self = Process.GetCurrentProcess().Id;
            return Process.GetProcesses()
                .Where(p => { try { return p.Id != self && p.MainWindowHandle != IntPtr.Zero; } catch { return false; } })
                .Select(p => p.ProcessName)
                .Where(n => !n.Equals("explorer", StringComparison.OrdinalIgnoreCase) && !n.Equals("TextInputHost", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    /// <summary>Raises <see cref="Changed"/> with the foreground app's process name (debounced).</summary>
    internal sealed class ForegroundWatcher : IDisposable
    {
        delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

        [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc proc, uint pid, uint thread, uint flags);
        [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

        const uint EVENT_SYSTEM_FOREGROUND = 3;
        readonly WinEventProc proc; // keep the delegate alive while hooked
        readonly Timer debounce = new Timer { Interval = 600 };
        IntPtr hook;
        string last;

        public event Action<string> Changed;

        public ForegroundWatcher()
        {
            proc = OnEvent;
            debounce.Tick += delegate { debounce.Stop(); Check(); };
            hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, proc, 0, 0, 0 /* OUTOFCONTEXT */);
        }

        void OnEvent(IntPtr h, uint evt, IntPtr hwnd, int obj, int child, uint thread, uint time)
        {
            debounce.Stop();
            debounce.Start();
        }

        public static string ForegroundApp()
        {
            uint pid;
            GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            if (pid == 0) return null;
            try { return Process.GetProcessById((int)pid).ProcessName; }
            catch { return null; }
        }

        /// <summary>Re-evaluates now (e.g. after the rules changed).</summary>
        public void Check()
        {
            string app = ForegroundApp();
            if (app == null) return;
            last = app;
            var h = Changed;
            if (h != null) h(app);
        }

        public string Current { get { return last; } }

        public void Dispose()
        {
            debounce.Dispose();
            if (hook != IntPtr.Zero) UnhookWinEvent(hook);
            hook = IntPtr.Zero;
        }
    }
}
