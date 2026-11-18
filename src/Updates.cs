using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace LoqRgb
{
    /// <summary>
    /// GitHub Releases lookup, download and verification. Release builds know their repository because CI
    /// stamps it into the assembly (AssemblyMetadata "RepositoryUrl"); development builds never update.
    /// </summary>
    internal static class Updates
    {
        public sealed class Asset
        {
            public string Name, Url, Sha256;
        }

        public sealed class Release
        {
            public Version Version;
            public string DisplayVersion;   // "1.2.0"
            public string PageUrl;
            public Asset Setup, Portable;
            public bool Newer;
        }

        /// <summary>"https://github.com/owner/repo", or null for local builds.</summary>
        public static string RepositoryUrl
        {
            get
            {
                var attr = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                    .Cast<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => a.Key == "RepositoryUrl");
                return attr == null || string.IsNullOrEmpty(attr.Value) ? null : attr.Value.TrimEnd('/');
            }
        }

        public static Version Current
        {
            get { return Normalize(Assembly.GetExecutingAssembly().GetName().Version); }
        }

        static Version Normalize(Version v)
        {
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
        }

        /// <summary>Leading version number of a tag: "v1.2", "1.2.3", "v1.2.3-beta" (null if none).</summary>
        public static Version ParseTag(string tag)
        {
            if (tag == null) return null;
            Match m = Regex.Match(tag.Trim().TrimStart('v', 'V'), @"^\d+(\.\d+){0,3}");
            Version v;
            if (!m.Success || !Version.TryParse(m.Value.Contains(".") ? m.Value : m.Value + ".0", out v)) return null;
            return Normalize(v);
        }

        static HttpWebRequest Request(string url)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "LOQKeyboardRGB-updater"; // GitHub requires a User-Agent; keep it generic
            req.Timeout = 15000;
            req.ReadWriteTimeout = 30000;
            return req;
        }

        /// <summary>Latest published release. Throws with a readable message on failure.</summary>
        public static Release GetLatest(string repositoryUrl)
        {
            Match m = Regex.Match(repositoryUrl ?? "", @"github\.com/([^/]+)/([^/]+)$");
            if (!m.Success) throw new InvalidOperationException("No update source is configured for this build.");
            HttpWebRequest req = Request("https://api.github.com/repos/" + m.Groups[1].Value + "/" + m.Groups[2].Value + "/releases/latest");
            req.Accept = "application/vnd.github+json";
            string json;
            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream()))
                    json = reader.ReadToEnd();
            }
            catch (WebException ex)
            {
                var resp = ex.Response as HttpWebResponse;
                throw new InvalidOperationException(resp != null && resp.StatusCode == HttpStatusCode.NotFound
                    ? "No releases have been published yet."
                    : "Couldn't reach GitHub (" + ex.Status + ").");
            }
            return ParseRelease(json);
        }

        public static Release ParseRelease(string json)
        {
            var root = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null) throw new InvalidOperationException("Unexpected response from GitHub.");
            Version v = ParseTag(Str(root, "tag_name"));
            if (v == null) throw new InvalidOperationException("Couldn't read the latest release's version.");

            var r = new Release
            {
                Version = v,
                DisplayVersion = v.Revision > 0 ? v.ToString() : v.ToString(3),
                PageUrl = Str(root, "html_url"),
                Newer = v > Current
            };
            string body = Str(root, "body") ?? "";
            var assets = root.ContainsKey("assets") ? root["assets"] as IEnumerable : null;
            if (assets != null)
            {
                foreach (object o in assets)
                {
                    var a = o as Dictionary<string, object>;
                    if (a == null) continue;
                    string name = Str(a, "name") ?? "";
                    var asset = new Asset { Name = name, Url = Str(a, "browser_download_url"), Sha256 = Sha256For(a, name, body) };
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        if (name.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0) r.Setup = asset;
                        else if (name.IndexOf("portable", StringComparison.OrdinalIgnoreCase) >= 0) r.Portable = asset;
                    }
                }
            }
            return r;
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) ? v as string : null;
        }

        /// <summary>The asset's SHA-256: GitHub's "digest" field, else the checksum table in the release notes.</summary>
        static string Sha256For(Dictionary<string, object> asset, string name, string body)
        {
            string digest = Str(asset, "digest");
            if (digest != null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                return digest.Substring(7).ToLowerInvariant();
            Match m = Regex.Match(body, Regex.Escape(name) + @"\W{1,8}([0-9a-fA-F]{64})");
            return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
        }

        /// <summary>Downloads an asset to a temp folder and checks its SHA-256. Throws if it doesn't match.</summary>
        public static string Download(Asset asset, Action<int> percent)
        {
            if (asset == null || asset.Url == null) throw new InvalidOperationException("This release has no file for this kind of install.");
            if (string.IsNullOrEmpty(asset.Sha256)) throw new InvalidOperationException("The release has no checksum for " + asset.Name + ", so it can't be verified.");
            string dir = Path.Combine(Path.GetTempPath(), "LOQKeyboardRGB-update");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, Path.GetFileName(asset.Name));

            HttpWebRequest req = Request(asset.Url);
            req.AllowAutoRedirect = true;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (Stream src = resp.GetResponseStream())
            using (var dst = File.Create(path))
            {
                long total = resp.ContentLength, done = 0;
                var buf = new byte[81920];
                int n, last = -1;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    dst.Write(buf, 0, n);
                    done += n;
                    int p = total > 0 ? (int)(done * 100 / total) : -1;
                    if (p != last && percent != null) percent(p);
                    last = p;
                }
            }

            string hash = Sha256File(path);
            if (!hash.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(path); } catch { }
                throw new InvalidOperationException("The downloaded file failed its SHA-256 check and was discarded.");
            }
            return path;
        }

        public static string Sha256File(string path)
        {
            using (var sha = SHA256.Create())
            using (var f = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>True when installed by the setup program: updates then go through the new setup.</summary>
        public static bool InstalledBySetup
        {
            get { return Installer.SetupUninstaller != null; }
        }

        /// <summary>
        /// Starts installing a downloaded update. The caller must exit right after this returns:
        /// the new version starts by itself (the setup relaunches it; a portable exe is swapped and restarted).
        /// </summary>
        public static void Apply(string file, bool startInTray)
        {
            if (InstalledBySetup)
            {
                Process.Start(file, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH=" + (startInTray ? "tray" : "show"));
                return;
            }

            // Portable / self-installed: a running exe can be renamed but not overwritten, so swap the files.
            string exe = Application.ExecutablePath;
            string old = exe + ".old";
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            try
            {
                File.Copy(file, exe, true);
            }
            catch
            {
                File.Move(old, exe); // put the current version back
                throw;
            }
            Process.Start(exe, "--wait-pid " + Process.GetCurrentProcess().Id + (startInTray ? " --tray" : ""));
        }

        /// <summary>Removes the previous exe left behind by a portable update (called at startup).</summary>
        public static void CleanupAfterUpdate()
        {
            try
            {
                string old = Application.ExecutablePath + ".old";
                if (File.Exists(old)) File.Delete(old);
                string dir = Path.Combine(Path.GetTempPath(), "LOQKeyboardRGB-update");
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch { }
        }
    }

    /// <summary>
    /// Automatic updates for the running app: periodic checks, and download + install either silently
    /// (when the window is hidden) or when the user clicks "Update".
    /// </summary>
    internal sealed class UpdateService
    {
        public const string AutoCheckKey = "updates.autoCheck";
        public const string AutoInstallKey = "updates.autoInstall";
        const string LastCheckKey = "updates.lastCheck";
        public const string LastRunVersionKey = "updates.lastRunVersion";

        public static UpdateService Current { get; private set; }

        readonly AppSettings settings;
        readonly Control ui;
        readonly Func<bool> windowHidden;
        readonly Action exitApp;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        int busy;

        /// <summary>The newer release found by the last check, if any.</summary>
        public Updates.Release Available { get; private set; }
        /// <summary>Short status for the UI ("Up to date", "Downloading 40%", errors…).</summary>
        public string Status { get; private set; }
        /// <summary>Raised on the UI thread whenever Available or Status change.</summary>
        public event Action Changed;

        public static bool Supported { get { return Updates.RepositoryUrl != null; } }

        public UpdateService(AppSettings settings, Control ui, Func<bool> windowHidden, Action exitApp)
        {
            this.settings = settings;
            this.ui = ui;
            this.windowHidden = windowHidden;
            this.exitApp = exitApp;
            Current = this;
            if (!Supported) return;
            timer.Interval = 20000; // first check shortly after startup, then every few hours
            timer.Tick += delegate
            {
                timer.Interval = 3 * 60 * 60 * 1000;
                if (DueForCheck()) Check(false);
            };
            timer.Start();
        }

        bool DueForCheck()
        {
            if (!settings.GetBool(AutoCheckKey, true)) return false;
            DateTime last;
            return !DateTime.TryParse(settings.GetString(LastCheckKey, ""), null, System.Globalization.DateTimeStyles.RoundtripKind, out last)
                   || (DateTime.UtcNow - last).TotalHours >= 20;
        }

        void Set(string status)
        {
            Status = status;
            var h = Changed;
            if (h != null) h();
        }

        void OnUi(Action a)
        {
            if (ui.IsHandleCreated) ui.BeginInvoke(a);
        }

        /// <summary>Checks GitHub for a newer release (manual = the user asked).</summary>
        public void Check(bool manual)
        {
            if (!Supported || Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
            if (manual) Set("Checking…");
            ThreadPool.QueueUserWorkItem(delegate
            {
                Updates.Release r = null;
                string error = null;
                try { r = Updates.GetLatest(Updates.RepositoryUrl); }
                catch (Exception ex) { error = ex.Message; }
                OnUi(() =>
                {
                    Interlocked.Exchange(ref busy, 0);
                    settings.SetString(LastCheckKey, DateTime.UtcNow.ToString("o"));
                    settings.Save();
                    if (error != null) { Set(manual ? error : null); return; }
                    Available = r.Newer ? r : null;
                    Set(r.Newer ? "Version " + r.DisplayVersion + " is available" : (manual ? "Up to date (" + Updates.Current.ToString(3) + ")" : null));
                    // Install by itself only while the window is hidden, so it never interrupts the user.
                    if (r.Newer && !manual && settings.GetBool(AutoInstallKey, true) && windowHidden()) Install();
                });
            });
        }

        /// <summary>Downloads, verifies and installs the available update, then restarts the app.</summary>
        public void Install()
        {
            Updates.Release r = Available;
            if (r == null || Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
            Updates.Asset asset = Updates.InstalledBySetup ? r.Setup : r.Portable;
            bool tray = windowHidden();
            Set("Downloading " + r.DisplayVersion + "…");
            ThreadPool.QueueUserWorkItem(delegate
            {
                string file = null, error = null;
                try { file = Updates.Download(asset, p => { if (p >= 0 && p % 10 == 0) OnUi(() => Set("Downloading " + r.DisplayVersion + "… " + p + "%")); }); }
                catch (Exception ex) { error = ex.Message; }
                OnUi(() =>
                {
                    if (error != null)
                    {
                        Interlocked.Exchange(ref busy, 0);
                        Set("Update failed: " + error);
                        return;
                    }
                    try
                    {
                        Set("Installing " + r.DisplayVersion + "…");
                        Updates.Apply(file, tray);
                        exitApp();
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Exchange(ref busy, 0);
                        Set("Update failed: " + ex.Message);
                    }
                });
            });
        }
    }
}
