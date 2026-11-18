using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LoqRgb
{
    /// <summary>
    /// Per-user install (no admin rights): copies the exe to %LOCALAPPDATA%\Programs\LOQ Keyboard RGB,
    /// adds a Start menu shortcut and an entry in Settings → Apps, whose Uninstall runs "--uninstall".
    /// </summary>
    internal static class Installer
    {
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LOQKeyboardRGB";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "LOQKeyboardRGB";

        public static readonly string InstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "LOQ Keyboard RGB");

        public static string InstalledExe { get { return Path.Combine(InstallDir, "LOQKeyboardRGB.exe"); } }

        static string ShortcutPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "LOQ Keyboard RGB.lnk"); }
        }

        public static bool IsInstalled
        {
            get
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstallKey))
                    return k != null && File.Exists(InstalledExe);
            }
        }

        public static bool RunningFromInstall
        {
            get { return string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>Installs (or updates) this exe for the current user. Returns the installed path.</summary>
        public static string Install()
        {
            Directory.CreateDirectory(InstallDir);
            if (!RunningFromInstall) File.Copy(Application.ExecutablePath, InstalledExe, true);

            CreateShortcut(ShortcutPath, InstalledExe, "Control the keyboard backlight of Lenovo LOQ laptops");

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", "LOQ Keyboard RGB");
                k.SetValue("DisplayVersion", AboutDialog.Version);
                k.SetValue("Publisher", Developer.Name);
                k.SetValue("DisplayIcon", InstalledExe + ",0");
                k.SetValue("InstallLocation", InstallDir);
                k.SetValue("UninstallString", "\"" + InstalledExe + "\" --uninstall");
                k.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }

            // keep "Start with Windows" pointing at the installed copy
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                if (k.GetValue(RunName) != null) k.SetValue(RunName, "\"" + InstalledExe + "\" --tray");

            return InstalledExe;
        }

        /// <summary>Installs, starts the installed copy once this process has exited, and exits.</summary>
        public static void InstallAndRelaunch()
        {
            string exe = Install();
            Process.Start(exe, "--wait-pid " + Process.GetCurrentProcess().Id);
            Application.Exit();
        }

        static void CreateShortcut(string path, string target, string description)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            try
            {
                object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                Type lt = lnk.GetType();
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { target });
                lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { Path.GetDirectoryName(target) });
                lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { description });
                lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { target + ",0" });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(lnk);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
            }
        }

        /// <summary>Handles "--uninstall" (run from Settings → Apps). Returns the exit code.</summary>
        public static int Uninstall(bool quiet)
        {
            if (!quiet && MessageBox.Show("Uninstall LOQ Keyboard RGB?", "LOQ Keyboard RGB",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return 1;

            bool removeSettings = !quiet && MessageBox.Show(
                "Also delete your settings and profiles?\n\n" + AppSettings.Dir, "LOQ Keyboard RGB",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

            try { File.Delete(ShortcutPath); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    if (k != null) k.DeleteValue(RunName, false);
            }
            catch { }
            if (removeSettings)
            {
                try { Directory.Delete(AppSettings.Dir, true); } catch { }
            }

            // The exe can't delete itself while running: remove the folder shortly after we exit.
            if (Directory.Exists(InstallDir))
            {
                var psi = new ProcessStartInfo("cmd.exe",
                    "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"" + InstallDir + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
            }
            if (!quiet) MessageBox.Show("LOQ Keyboard RGB has been uninstalled.", "LOQ Keyboard RGB", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
    }
}
