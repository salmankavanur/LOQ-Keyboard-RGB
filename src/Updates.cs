using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace LoqRgb
{
    /// <summary>
    /// Checks GitHub Releases for a newer version. Only runs when the user asks. Release builds know their
    /// repository because CI stamps it into the assembly (AssemblyMetadata "RepositoryUrl").
    /// </summary>
    internal static class Updates
    {
        public sealed class Result
        {
            public bool Newer;
            public string Version;   // e.g. "1.2.0"
            public string PageUrl;   // release page to download from
            public string Error;
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
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        /// <summary>Parses "v1.2", "1.2.3", "v1.2.3-beta" into a Version (null if it isn't one).</summary>
        public static Version ParseTag(string tag)
        {
            if (tag == null) return null;
            Match m = Regex.Match(tag.Trim().TrimStart('v', 'V'), @"^\d+(\.\d+){0,3}"); // leading version number
            Version v;
            if (!m.Success || !Version.TryParse(m.Value.Contains(".") ? m.Value : m.Value + ".0", out v)) return null;
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
        }

        /// <summary>Asks GitHub for the latest release of <paramref name="repositoryUrl"/>; calls back on a worker thread.</summary>
        public static void CheckAsync(string repositoryUrl, Action<Result> done)
        {
            ThreadPool.QueueUserWorkItem(delegate { done(Check(repositoryUrl)); });
        }

        public static Result Check(string repositoryUrl)
        {
            var r = new Result();
            try
            {
                Match m = Regex.Match(repositoryUrl ?? "", @"github\.com/([^/]+)/([^/]+)$");
                if (!m.Success) { r.Error = "No update source is configured for this build."; return r; }
                string api = "https://api.github.com/repos/" + m.Groups[1].Value + "/" + m.Groups[2].Value + "/releases/latest";

                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create(api);
                req.UserAgent = "LOQKeyboardRGB-update-check"; // GitHub requires a User-Agent; keep it generic
                req.Accept = "application/vnd.github+json";
                req.Timeout = 10000;
                string json;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream()))
                    json = reader.ReadToEnd();

                string tag = Field(json, "tag_name");
                r.PageUrl = Field(json, "html_url");
                Version latest = ParseTag(tag);
                if (latest == null) { r.Error = "Couldn't read the latest release."; return r; }
                r.Version = latest.Build > 0 || latest.Revision == 0 ? latest.ToString(3) : latest.ToString();
                Version cur = Current;
                r.Newer = latest > new Version(cur.Major, cur.Minor, Math.Max(0, cur.Build), Math.Max(0, cur.Revision));
            }
            catch (WebException ex)
            {
                var resp = ex.Response as HttpWebResponse;
                r.Error = resp != null && resp.StatusCode == HttpStatusCode.NotFound
                    ? "No releases have been published yet."
                    : "Couldn't reach GitHub (" + ex.Status + ").";
            }
            catch (Exception ex)
            {
                r.Error = ex.Message;
            }
            return r;
        }

        /// <summary>Top-level string field of a JSON object (enough for the GitHub release response).</summary>
        static string Field(string json, string name)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? Regex.Unescape(m.Groups[1].Value) : null;
        }
    }
}
