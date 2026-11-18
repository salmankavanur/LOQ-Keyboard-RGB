using System;
using System.Management;
using Microsoft.Win32;

namespace LoqRgb
{
    internal enum LenovoPowerMode { Unknown = 0, Quiet = 1, Balanced = 2, Performance = 3, Custom = 255 }

    /// <summary>
    /// Follows the Lenovo power mode (Fn+Q: Quiet / Balanced / Performance).
    /// Changes arrive as the LENOVO_GAMEZONE_SMART_FAN_MODE_EVENT WMI event, which a normal user may subscribe to
    /// (reading the mode directly through LENOVO_GAMEZONE_DATA needs admin rights). The starting value comes
    /// from the registry key Lenovo's SmartEngine service keeps for that event.
    /// </summary>
    internal sealed class PowerModeWatcher : IDisposable
    {
        const string RegistryPath = @"SOFTWARE\Lenovo\SmartEngine\modules\F1020\SmartFanModeEvent";
        ManagementEventWatcher watcher;

        /// <summary>Raised on a background thread when the mode changes.</summary>
        public event Action<LenovoPowerMode> Changed;

        public LenovoPowerMode Current { get; private set; }
        public bool Available { get; private set; }

        /// <summary>The app's watcher, for the Settings window.</summary>
        public static PowerModeWatcher Shared { get; private set; }

        public static string SettingKey(LenovoPowerMode m) { return "powerModeProfile." + m; }
        public const string EnabledKey = "powerModeEnabled";

        public PowerModeWatcher()
        {
            Shared = this;
            Current = ReadRegistry();
            try
            {
                watcher = new ManagementEventWatcher(
                    new ManagementScope(@"root\WMI"),
                    new EventQuery("SELECT * FROM LENOVO_GAMEZONE_SMART_FAN_MODE_EVENT"));
                watcher.EventArrived += OnEvent;
                watcher.Start();
                Available = true;
            }
            catch
            {
                watcher = null;
                Available = false; // not a Lenovo gaming laptop, or WMI unavailable
            }
        }

        public static LenovoPowerMode FromCode(long code)
        {
            switch (code)
            {
                case 1: return LenovoPowerMode.Quiet;
                case 2: return LenovoPowerMode.Balanced;
                case 3: return LenovoPowerMode.Performance;
                case 0: return LenovoPowerMode.Unknown;
                default: return LenovoPowerMode.Custom; // 224 / 255 on models with a custom mode
            }
        }

        static LenovoPowerMode ReadRegistry()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(RegistryPath))
                {
                    object v = k == null ? null : k.GetValue("SmartFanMode");
                    return v == null ? LenovoPowerMode.Unknown : FromCode(Convert.ToInt64(v));
                }
            }
            catch
            {
                return LenovoPowerMode.Unknown;
            }
        }

        void OnEvent(object sender, EventArrivedEventArgs e)
        {
            try
            {
                LenovoPowerMode mode = FromCode(Convert.ToInt64(e.NewEvent["mode"]));
                if (mode == Current) return;
                Current = mode;
                var h = Changed;
                if (h != null) h(mode);
            }
            catch { }
        }

        public void Dispose()
        {
            if (watcher == null) return;
            try { watcher.Stop(); } catch { }
            watcher.Dispose();
            watcher = null;
        }
    }
}
