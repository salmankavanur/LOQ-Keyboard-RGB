using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace LoqRgb
{
    /// <summary>
    /// Values below 100 are hardware effect codes understood by the keyboard firmware.
    /// Values from 100 are software effects: the app renders frames and streams them as static colours.
    /// </summary>
    public enum Effect
    {
        Off = 0, Static = 1, Breath = 3, Wave = 4, Smooth = 6,
        Audio = 100, Ambient = 101
    }

    public static class Effects
    {
        public static bool IsSoftware(Effect e) { return (int)e >= 100; }

        public static bool IsAnimated(Effect e)
        {
            return e == Effect.Breath || e == Effect.Wave || e == Effect.Smooth || IsSoftware(e);
        }

        public static bool UsesSpeed(Effect e)
        {
            return e == Effect.Breath || e == Effect.Wave || e == Effect.Smooth || e == Effect.Audio || e == Effect.Ambient;
        }

        /// <summary>Effects whose output is built from the four zone colours.</summary>
        public static bool UsesZoneColors(Effect e)
        {
            return e == Effect.Static || e == Effect.Breath || e == Effect.Audio;
        }

        public static string Describe(Effect e)
        {
            switch (e)
            {
                case Effect.Audio: return "Zones react to the sound playing on this PC: bass on the left, treble on the right. Uses your zone colours.";
                case Effect.Ambient: return "Each zone takes the average colour of the matching quarter of your screen (games, videos, wallpaper). Speed sets how quickly it follows.";
                default: return null;
            }
        }
    }

    public sealed class LightState
    {
        public Effect Effect = Effect.Static;
        public int Speed = 2;        // 1 (slow) .. 4 (fast)
        public int Brightness = 2;   // 1 low, 2 high
        public bool WaveRightToLeft;
        public Color[] Zones =
        {
            Color.FromArgb(0, 110, 255), Color.FromArgb(0, 210, 255),
            Color.FromArgb(150, 0, 255), Color.FromArgb(255, 0, 150)
        };

        public LightState Clone()
        {
            var c = (LightState)MemberwiseClone();
            c.Zones = (Color[])Zones.Clone();
            return c;
        }

        /// <summary>Builds the 33-byte feature report: CC 16 effect speed brightness [R G B]x4 .. waveLTR waveRTL.</summary>
        public byte[] ToReport()
        {
            var b = new byte[33];
            b[0] = 0xCC;
            b[1] = 0x16;
            if (Effect == Effect.Off)
            {
                b[2] = (byte)Effect.Static;
                b[3] = 1;
                b[4] = 0;
                return b;
            }
            // Software effects fall back to their zone colours when sent as a single report.
            if (Effects.IsSoftware(Effect)) return FrameReport(Zones, Brightness);
            b[2] = (byte)Effect;
            b[3] = (byte)Math.Max(1, Math.Min(4, Speed));
            b[4] = (byte)Math.Max(1, Math.Min(2, Brightness));
            if (Effect == Effect.Static || Effect == Effect.Breath)
            {
                for (int z = 0; z < 4; z++)
                {
                    b[5 + z * 3] = Zones[z].R;
                    b[6 + z * 3] = Zones[z].G;
                    b[7 + z * 3] = Zones[z].B;
                }
            }
            if (Effect == Effect.Wave)
            {
                b[18] = (byte)(WaveRightToLeft ? 0 : 1);
                b[19] = (byte)(WaveRightToLeft ? 1 : 0);
            }
            return b;
        }

        /// <summary>A static-colour report used to stream software-rendered frames.</summary>
        public static byte[] FrameReport(Color[] zones, int brightness)
        {
            var b = new byte[33];
            b[0] = 0xCC;
            b[1] = 0x16;
            b[2] = (byte)Effect.Static;
            b[3] = 1;
            b[4] = (byte)Math.Max(1, Math.Min(2, brightness));
            for (int z = 0; z < 4; z++)
            {
                b[5 + z * 3] = zones[z].R;
                b[6 + z * 3] = zones[z].G;
                b[7 + z * 3] = zones[z].B;
            }
            return b;
        }

        public IEnumerable<string> ToLines()
        {
            yield return "effect=" + Effect;
            yield return "speed=" + Speed;
            yield return "brightness=" + Brightness;
            yield return "waveDirection=" + (WaveRightToLeft ? "rtl" : "ltr");
            for (int z = 0; z < 4; z++) yield return "zone" + (z + 1) + "=" + Gfx.ToHex(Zones[z]);
        }

        public static LightState FromValues(Dictionary<string, string> d)
        {
            var s = new LightState();
            string v;
            Effect e;
            int n;
            if (d.TryGetValue("effect", out v) && Enum.TryParse(v, true, out e) && Enum.IsDefined(typeof(Effect), e)) s.Effect = e;
            if (d.TryGetValue("speed", out v) && int.TryParse(v, out n)) s.Speed = Math.Max(1, Math.Min(4, n));
            if (d.TryGetValue("brightness", out v) && int.TryParse(v, out n)) s.Brightness = Math.Max(1, Math.Min(2, n));
            if (d.TryGetValue("waveDirection", out v)) s.WaveRightToLeft = v.Equals("rtl", StringComparison.OrdinalIgnoreCase);
            for (int z = 0; z < 4; z++)
            {
                Color c;
                if (d.TryGetValue("zone" + (z + 1), out v) && Gfx.TryParseHex(v, out c)) s.Zones[z] = c;
            }
            return s;
        }
    }

    internal static class KeyValues
    {
        public static Dictionary<string, string> Read(IEnumerable<string> lines)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in lines)
            {
                int i = line.IndexOf('=');
                if (i <= 0 || line.TrimStart().StartsWith("#")) continue;
                d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
            }
            return d;
        }
    }

    public sealed class AppSettings
    {
        public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LOQ Keyboard RGB");
        public static readonly string ProfileDir = Path.Combine(Dir, "Profiles");
        static readonly string FilePath = Path.Combine(Dir, "settings.txt");

        public LightState State = new LightState();
        public bool MinimizeToTray = true;
        public bool TrayTipShown;
        public Effect LastOnEffect = Effect.Static;

        public static AppSettings Load()
        {
            var s = new AppSettings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                var d = KeyValues.Read(File.ReadAllLines(FilePath));
                s.State = LightState.FromValues(d);
                string v;
                Effect e;
                if (d.TryGetValue("minimizeToTray", out v)) s.MinimizeToTray = v == "true";
                if (d.TryGetValue("trayTipShown", out v)) s.TrayTipShown = v == "true";
                if (d.TryGetValue("lastOnEffect", out v) && Enum.TryParse(v, true, out e) && e != Effect.Off && Enum.IsDefined(typeof(Effect), e)) s.LastOnEffect = e;
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var lines = new List<string>
                {
                    "minimizeToTray=" + (MinimizeToTray ? "true" : "false"),
                    "trayTipShown=" + (TrayTipShown ? "true" : "false"),
                    "lastOnEffect=" + LastOnEffect
                };
                lines.AddRange(State.ToLines());
                File.WriteAllLines(FilePath, lines);
            }
            catch { }
        }

        public static string[] ListProfiles()
        {
            if (!Directory.Exists(ProfileDir)) return new string[0];
            return Directory.GetFiles(ProfileDir, "*.txt")
                .Select(p => Path.GetFileNameWithoutExtension(p))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static LightState LoadProfile(string name)
        {
            string p = ProfilePath(name);
            if (!File.Exists(p)) return null;
            try { return LightState.FromValues(KeyValues.Read(File.ReadAllLines(p))); }
            catch { return null; }
        }

        public static void SaveProfile(string name, LightState state)
        {
            Directory.CreateDirectory(ProfileDir);
            File.WriteAllLines(ProfilePath(name), state.ToLines());
        }

        public static void DeleteProfile(string name)
        {
            string p = ProfilePath(name);
            if (File.Exists(p)) File.Delete(p);
        }

        public static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }

        static string ProfilePath(string name)
        {
            return Path.Combine(ProfileDir, Sanitize(name) + ".txt");
        }

        public static void EnsureDefaultProfiles()
        {
            try
            {
                if (Directory.Exists(ProfileDir)) return;
                Directory.CreateDirectory(ProfileDir);
                SaveProfile("Ocean", Preset(Effect.Static, "#0050FF", "#00A0FF", "#00E0FF", "#00FFC0"));
                SaveProfile("Sunset", Preset(Effect.Static, "#FF2000", "#FF5A00", "#FF9000", "#FF0060"));
                SaveProfile("Cyber", Preset(Effect.Static, "#FF00C8", "#8A00FF", "#00C8FF", "#00FFA0"));
                SaveProfile("Rainbow Wave", Preset(Effect.Wave, "#FF0000", "#00FF00", "#0000FF", "#FFFFFF"));
                SaveProfile("Breathing Red", Preset(Effect.Breath, "#FF0000", "#FF0000", "#FF0000", "#FF0000"));
                SaveProfile("Pure White", Preset(Effect.Static, "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF"));
            }
            catch { }
        }

        static LightState Preset(Effect e, params string[] hex)
        {
            var s = new LightState { Effect = e, Speed = 2, Brightness = 2 };
            for (int z = 0; z < 4; z++)
            {
                Color c;
                if (Gfx.TryParseHex(hex[z], out c)) s.Zones[z] = c;
            }
            return s;
        }
    }
}
