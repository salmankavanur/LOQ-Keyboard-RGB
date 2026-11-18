using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace LoqRgb
{
    // ------------------------------------------------------------------ WASAPI interop (loopback capture)

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorCom { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }

    /// <summary>Captures what the PC is playing (WASAPI loopback) and reads it as mono samples.</summary>
    internal sealed class LoopbackCapture : IDisposable
    {
        static readonly Guid FloatSubFormat = new Guid("00000003-0000-0010-8000-00aa00389b71");

        IAudioClient client;
        IAudioCaptureClient capture;
        readonly int channels, bits;
        readonly bool isFloat;
        float[] fbuf = new float[0];
        short[] sbuf = new short[0];
        int[] ibuf = new int[0];

        public int SampleRate { get; private set; }

        public LoopbackCapture()
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            IMMDevice device;
            Check(enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out device), "No audio output device");
            Guid iid = typeof(IAudioClient).GUID;
            object o;
            Check(device.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out o), "Audio device unavailable");
            client = (IAudioClient)o;

            IntPtr fmt;
            Check(client.GetMixFormat(out fmt), "Could not read the audio format");
            try
            {
                ushort tag = (ushort)Marshal.ReadInt16(fmt, 0);
                channels = Marshal.ReadInt16(fmt, 2);
                SampleRate = Marshal.ReadInt32(fmt, 4);
                bits = Marshal.ReadInt16(fmt, 14);
                if (tag == 0xFFFE)
                {
                    var g = new byte[16];
                    Marshal.Copy(new IntPtr(fmt.ToInt64() + 24), g, 0, 16);
                    isFloat = new Guid(g) == FloatSubFormat;
                }
                else isFloat = tag == 3;
                Check(client.Initialize(0 /* shared */, 0x00020000 /* LOOPBACK */, 10000000, 0, fmt, IntPtr.Zero), "Could not start audio capture");
            }
            finally
            {
                Marshal.FreeCoTaskMem(fmt);
            }

            Guid cid = typeof(IAudioCaptureClient).GUID;
            object c;
            Check(client.GetService(ref cid, out c), "Audio capture service unavailable");
            capture = (IAudioCaptureClient)c;
            Check(client.Start(), "Could not start audio capture");
        }

        static void Check(int hr, string what)
        {
            if (hr < 0) throw new InvalidOperationException(what + string.Format(" (0x{0:X8}).", hr));
        }

        /// <summary>Reads all pending audio, calling sink for every mono sample. Returns false if the device went away.</summary>
        public bool Read(Action<float> sink)
        {
            uint packet;
            while (true)
            {
                if (capture.GetNextPacketSize(out packet) < 0) return false;
                if (packet == 0) return true;
                IntPtr data;
                uint frames, flags;
                ulong p1, p2;
                if (capture.GetBuffer(out data, out frames, out flags, out p1, out p2) < 0) return false;
                int n = (int)frames * channels;
                bool silent = (flags & 2) != 0;
                if (silent || n == 0)
                {
                    for (int f = 0; f < frames; f++) sink(0f);
                }
                else if (isFloat && bits == 32)
                {
                    if (fbuf.Length < n) fbuf = new float[n];
                    Marshal.Copy(data, fbuf, 0, n);
                    for (int f = 0; f < frames; f++)
                    {
                        float sum = 0;
                        for (int ch = 0; ch < channels; ch++) sum += fbuf[f * channels + ch];
                        sink(sum / channels);
                    }
                }
                else if (bits == 16)
                {
                    if (sbuf.Length < n) sbuf = new short[n];
                    Marshal.Copy(data, sbuf, 0, n);
                    for (int f = 0; f < frames; f++)
                    {
                        float sum = 0;
                        for (int ch = 0; ch < channels; ch++) sum += sbuf[f * channels + ch] / 32768f;
                        sink(sum / channels);
                    }
                }
                else if (bits == 32)
                {
                    if (ibuf.Length < n) ibuf = new int[n];
                    Marshal.Copy(data, ibuf, 0, n);
                    for (int f = 0; f < frames; f++)
                    {
                        float sum = 0;
                        for (int ch = 0; ch < channels; ch++) sum += ibuf[f * channels + ch] / 2147483648f;
                        sink(sum / channels);
                    }
                }
                capture.ReleaseBuffer(frames);
            }
        }

        public void Dispose()
        {
            try { if (client != null) client.Stop(); } catch { }
            if (capture != null) Marshal.ReleaseComObject(capture);
            if (client != null) Marshal.ReleaseComObject(client);
            capture = null;
            client = null;
        }
    }

    // ------------------------------------------------------------------ the effect

    /// <summary>
    /// Audio visualizer: zone 1 = bass, 2 = low mids, 3 = high mids, 4 = treble.
    /// Each zone shows its own colour at a brightness that follows its band, with automatic gain.
    /// </summary>
    internal sealed class AudioSource : IFrameSource
    {
        const int N = 2048;
        static readonly double[,] Bands = { { 30, 160 }, { 160, 700 }, { 700, 3000 }, { 3000, 12000 } };
        static readonly double[] Release = { 1.2, 2.0, 3.2, 5.0 };   // brightness drop per second, by speed

        readonly float[] ring = new float[N];
        int ringPos;
        readonly double[] re = new double[N], im = new double[N], window = new double[N];
        readonly double[] level = new double[4];
        readonly double[] gain = { 1, 1, 1, 1 };
        LoopbackCapture capture;
        double retryAt;
        string problem;

        public AudioSource()
        {
            for (int i = 0; i < N; i++) window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (N - 1));
        }

        public int IntervalMs { get { return 25; } }
        public string Problem { get { return problem; } }

        void Push(float s)
        {
            ring[ringPos] = s;
            ringPos = (ringPos + 1) % N;
        }

        public void Render(LightState s, double t, double dt, Color[] o)
        {
            if (capture == null && t >= retryAt)
            {
                try
                {
                    capture = new LoopbackCapture();
                    problem = null;
                }
                catch (Exception ex)
                {
                    problem = "Audio: " + ex.Message;
                    retryAt = t + 3; // device may appear later (e.g. headphones plugged in)
                }
            }
            if (capture != null && !capture.Read(Push))
            {
                capture.Dispose();
                capture = null;
                retryAt = t + 1; // default device changed: reopen
            }

            for (int i = 0; i < N; i++)
            {
                re[i] = ring[(ringPos + i) % N] * window[i];
                im[i] = 0;
            }
            Fft(re, im);

            int rate = capture != null ? capture.SampleRate : 48000;
            double binHz = (double)rate / N;
            int sp = Math.Max(1, Math.Min(4, s.Speed)) - 1;
            var v = new double[4];
            for (int b = 0; b < 4; b++)
            {
                int lo = Math.Max(1, (int)(Bands[b, 0] / binHz));
                int hi = Math.Min(N / 2 - 1, Math.Max(lo, (int)(Bands[b, 1] / binHz)));
                double sum = 0;
                for (int k = lo; k <= hi; k++) sum += re[k] * re[k] + im[k] * im[k];
                v[b] = Math.Log10(1 + Math.Sqrt(sum / (hi - lo + 1)));
            }

            // Automatic gain: each band follows its recent peak, but is never boosted more than
            // ~1.8x relative to the loudest band (so leakage from a loud band stays dim) and
            // never amplifies near-silence.
            double loudest = 0;
            for (int b = 0; b < 4; b++) loudest = Math.Max(loudest, Math.Max(v[b], gain[b] - gain[b] * 0.12 * dt));
            for (int b = 0; b < 4; b++)
            {
                gain[b] = Math.Max(Math.Max(v[b], 0.35), Math.Max(gain[b] - gain[b] * 0.12 * dt, loudest * 0.55));
                double norm = v[b] / gain[b];
                norm = Math.Max(0, (norm - 0.12) / 0.88);
                norm = Math.Pow(Math.Min(1, norm), 1.4);

                level[b] = norm > level[b] ? level[b] + (norm - level[b]) * 0.7 : Math.Max(norm, level[b] - Release[sp] * dt);
                o[b] = Gfx.Scale(s.Zones[b], 0.04 + 0.96 * level[b]);
            }
        }

        static void Fft(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    double tr = re[i]; re[i] = re[j]; re[j] = tr;
                    double ti = im[i]; im[i] = im[j]; im[j] = ti;
                }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                double wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, b = a + len / 2;
                        double xr = re[b] * cr - im[b] * ci;
                        double xi = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - xr; im[b] = im[a] - xi;
                        re[a] += xr; im[a] += xi;
                        double ncr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr;
                        cr = ncr;
                    }
                }
            }
        }

        public void Dispose()
        {
            if (capture != null) capture.Dispose();
            capture = null;
        }
    }
}
