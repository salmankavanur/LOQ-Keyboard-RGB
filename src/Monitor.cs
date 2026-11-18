using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LoqRgb
{
    /// <summary>Samples CPU, RAM, GPU and battery. All values are 0..1, or -1 when unavailable.</summary>
    internal sealed class SystemStats : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct MEMORYSTATUSEX
        {
            public int dwLength, dwMemoryLoad;
            public long ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
        [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

        long lastIdle, lastKernel, lastUser;
        bool cpuPrimed;
        PerformanceCounterCategory gpuCategory;
        bool gpuUnavailable;
        Dictionary<string, CounterSample> lastGpu = new Dictionary<string, CounterSample>();

        public double Cpu = -1, Ram = -1, Gpu = -1, Battery = -1;
        public bool Charging;

        public void Sample()
        {
            // CPU: busy share of (kernel + user) time since last sample (kernel time includes idle)
            long idle, kernel, user;
            if (GetSystemTimes(out idle, out kernel, out user))
            {
                if (cpuPrimed)
                {
                    long total = (kernel - lastKernel) + (user - lastUser);
                    if (total > 0) Cpu = Math.Max(0, Math.Min(1, 1 - (double)(idle - lastIdle) / total));
                }
                lastIdle = idle; lastKernel = kernel; lastUser = user;
                cpuPrimed = true;
            }

            var mem = new MEMORYSTATUSEX { dwLength = Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
            if (GlobalMemoryStatusEx(ref mem)) Ram = mem.dwMemoryLoad / 100.0;

            SampleGpu();

            PowerStatus p = SystemInformation.PowerStatus;
            if ((p.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) != 0 || p.BatteryLifePercent > 1.0f)
            {
                Battery = -1;
                Charging = false;
            }
            else
            {
                Battery = p.BatteryLifePercent;
                Charging = p.PowerLineStatus == PowerLineStatus.Online;
            }
        }

        /// <summary>
        /// GPU: sum of 3D-engine utilisation over all processes, from the "GPU Engine" performance
        /// counters (what Task Manager shows). Unavailable on some drivers / locales.
        /// </summary>
        void SampleGpu()
        {
            if (gpuUnavailable) return;
            try
            {
                if (gpuCategory == null) gpuCategory = new PerformanceCounterCategory("GPU Engine");
                InstanceDataCollection util = gpuCategory.ReadCategory()["utilization percentage"];
                if (util == null) { gpuUnavailable = true; Gpu = -1; return; }
                var now = new Dictionary<string, CounterSample>();
                double sum = 0;
                bool any = false;
                foreach (InstanceData d in util.Values)
                {
                    if (d.InstanceName.IndexOf("engtype_3D", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    now[d.InstanceName] = d.Sample;
                    CounterSample prev;
                    if (lastGpu.TryGetValue(d.InstanceName, out prev))
                    {
                        sum += CounterSample.Calculate(prev, d.Sample);
                        any = true;
                    }
                }
                lastGpu = now;
                if (any) Gpu = Math.Max(0, Math.Min(1, sum / 100.0));
            }
            catch
            {
                gpuUnavailable = true;
                Gpu = -1;
            }
        }

        public void Dispose() { }
    }

    /// <summary>System monitor effect: zone 1 CPU, zone 2 RAM, zone 3 GPU, zone 4 battery.</summary>
    internal sealed class MonitorSource : IFrameSource
    {
        static readonly Color Unavailable = Color.FromArgb(60, 60, 70);

        readonly SystemStats stats = new SystemStats();
        readonly double[] shown = { -1, -1, -1, -1 };
        double nextSample;
        volatile bool sampling;

        public int IntervalMs { get { return 50; } }
        public string Problem { get { return null; } }

        /// <summary>0 = green, 0.5 = yellow, 1 = red.</summary>
        public static Color LoadColor(double load)
        {
            if (load < 0) return Unavailable;
            return Gfx.Hue(120 * (1 - Math.Max(0, Math.Min(1, load))));
        }

        public void Render(LightState s, double t, double dt, Color[] o)
        {
            // Sample on the thread pool: the GPU counters take ~1 s to initialise on first use.
            if (t >= nextSample && !sampling)
            {
                sampling = true;
                nextSample = t + 1.0;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    try { stats.Sample(); }
                    catch { }
                    finally { sampling = false; }
                });
            }

            // battery is inverted: full = green, empty = red
            double[] target = { stats.Cpu, stats.Ram, stats.Gpu, stats.Battery < 0 ? -1 : 1 - stats.Battery };
            double k = 1 - Math.Exp(-4 * dt); // glide between samples
            for (int z = 0; z < 4; z++)
            {
                if (target[z] < 0) { shown[z] = -1; o[z] = Unavailable; continue; }
                shown[z] = shown[z] < 0 ? target[z] : shown[z] + (target[z] - shown[z]) * k;
                o[z] = LoadColor(shown[z]);
            }

            if (stats.Charging && stats.Battery >= 0)
                o[3] = Gfx.Scale(o[3], 0.45 + 0.55 * (0.5 + 0.5 * Math.Cos(2 * Math.PI * t / 3.0)));
        }

        public void Dispose()
        {
            stats.Dispose();
        }
    }
}
