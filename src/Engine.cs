using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

namespace LoqRgb
{
    /// <summary>Renders one frame (four zone colours) of a software effect.</summary>
    public interface IFrameSource : IDisposable
    {
        /// <summary>Milliseconds between frames.</summary>
        int IntervalMs { get; }
        /// <summary>Non-null when the source cannot work (e.g. no audio device).</summary>
        string Problem { get; }
        void Render(LightState state, double time, double dt, Color[] output);
    }

    /// <summary>
    /// Sits between the UI and the keyboard. Hardware effects are sent once; software effects and
    /// flashes run on a background thread that streams frames to the keyboard.
    /// </summary>
    public sealed class LightingEngine : IDisposable
    {
        readonly KeyboardController keyboard = new KeyboardController();
        readonly object sync = new object();
        readonly AutoResetEvent wake = new AutoResetEvent(false);

        LightState state = new LightState();
        Thread worker;
        volatile bool disposed;
        DateTime retryAfter = DateTime.MinValue;
        bool? lastOk;
        string lastError;
        string lastProblem;

        // flash overlay
        bool flashing;
        Color flashColor;
        int flashCount;
        DateTime flashStart;

        /// <summary>Raised on the engine thread with each software frame (a copy).</summary>
        public event Action<Color[]> FrameRendered;
        /// <summary>Raised when the connection state changes: ok, error/problem text. May be raised on any thread.</summary>
        public event Action<bool, string> StatusChanged;

        public string DeviceName
        {
            get { lock (sync) return keyboard.DeviceName; }
        }

        public LightState State
        {
            get { lock (sync) return state; }
        }

        public void SetState(LightState s)
        {
            lock (sync)
            {
                state = s.Clone();
                retryAfter = DateTime.MinValue;
                if (worker != null) { wake.Set(); return; }
                if (!Effects.IsSoftware(state.Effect) && !flashing)
                {
                    SendLocked(state.ToReport());
                    return;
                }
                StartWorkerLocked();
            }
        }

        /// <summary>Re-sends the current state (after sleep, unlock, etc).</summary>
        public void Reapply()
        {
            SetState(State);
        }

        /// <summary>Flashes all zones with a colour, then returns to the current lighting.</summary>
        public void Flash(Color color, int times)
        {
            lock (sync)
            {
                flashColor = color;
                flashCount = Math.Max(1, Math.Min(20, times));
                flashStart = DateTime.UtcNow;
                flashing = true;
                retryAfter = DateTime.MinValue;
                if (worker != null) wake.Set();
                else StartWorkerLocked();
            }
        }

        void StartWorkerLocked()
        {
            worker = new Thread(Run) { IsBackground = true, Name = "LightingEngine" };
            worker.SetApartmentState(ApartmentState.MTA); // WASAPI / COM sources are created on this thread
            worker.Start();
        }

        bool SendLocked(byte[] report)
        {
            if (DateTime.UtcNow < retryAfter) return false;
            string err;
            bool ok = keyboard.Send(report, out err);
            if (!ok) retryAfter = DateTime.UtcNow.AddSeconds(2); // don't rescan HID devices 30x a second
            if (lastOk != ok || lastError != err)
            {
                lastOk = ok;
                lastError = err;
                RaiseStatus();
            }
            return ok;
        }

        void RaiseStatus()
        {
            var h = StatusChanged;
            if (h == null) return;
            bool ok = lastOk ?? false;
            h(ok, ok ? lastProblem : lastError);
        }

        void SetProblem(string problem)
        {
            lock (sync)
            {
                if (problem == lastProblem) return;
                lastProblem = problem;
                if (lastOk == true) RaiseStatus();
            }
        }

        void Run()
        {
            IFrameSource source = null;
            Effect sourceEffect = Effect.Off;
            var clock = Stopwatch.StartNew();
            double last = 0;
            var frame = new Color[4];
            try
            {
                while (!disposed)
                {
                    LightState s;
                    bool fl;
                    Color fc;
                    int fCount;
                    double fElapsed;
                    lock (sync)
                    {
                        s = state;
                        fl = flashing;
                        fc = flashColor;
                        fCount = flashCount;
                        fElapsed = (DateTime.UtcNow - flashStart).TotalSeconds;
                        if (!fl && !Effects.IsSoftware(s.Effect))
                        {
                            // Back to a hardware effect: send it and stop streaming.
                            SendLocked(s.ToReport());
                            worker = null;
                            break;
                        }
                    }

                    double t = clock.Elapsed.TotalSeconds;
                    double dt = Math.Min(0.25, t - last);
                    last = t;
                    int interval = 33;

                    if (fl)
                    {
                        const double period = 0.34;
                        if (fElapsed >= fCount * period)
                        {
                            lock (sync) flashing = false;
                            continue;
                        }
                        Color c = (fElapsed % period) < period * 0.55 ? fc : Color.Black;
                        for (int z = 0; z < 4; z++) frame[z] = c;
                        lock (sync) SendLocked(LightState.FrameReport(frame, 2));
                        interval = 20;
                    }
                    else
                    {
                        if (source == null || sourceEffect != s.Effect)
                        {
                            if (source != null) source.Dispose();
                            source = CreateSource(s.Effect);
                            sourceEffect = s.Effect;
                        }
                        source.Render(s, t, dt, frame);
                        SetProblem(source.Problem);
                        lock (sync) SendLocked(LightState.FrameReport(frame, s.Brightness));
                        interval = source.IntervalMs;
                    }

                    var h = FrameRendered;
                    if (h != null) h((Color[])frame.Clone());
                    wake.WaitOne(interval);
                }
            }
            catch (Exception ex)
            {
                SetProblem("Lighting engine error: " + ex.Message);
                lock (sync) worker = null;
            }
            finally
            {
                if (source != null) source.Dispose();
                SetProblem(null);
            }
        }

        static IFrameSource CreateSource(Effect e)
        {
            switch (e)
            {
                case Effect.Audio: return new AudioSource();
                case Effect.Ambient: return new AmbientSource();
                case Effect.Monitor: return new MonitorSource();
                case Effect.Drift:
                case Effect.Fade:
                case Effect.Strobe: return new AnimationSource(e);
                default: return new SolidSource();
            }
        }

        public void Dispose()
        {
            disposed = true;
            wake.Set();
            Thread t;
            lock (sync) t = worker;
            if (t != null) t.Join(1000);
            lock (sync) keyboard.Dispose();
        }
    }

    /// <summary>Fallback: shows the zone colours.</summary>
    internal sealed class SolidSource : IFrameSource
    {
        public int IntervalMs { get { return 250; } }
        public string Problem { get { return null; } }
        public void Render(LightState s, double t, double dt, Color[] o)
        {
            for (int z = 0; z < 4; z++) o[z] = s.Zones[z];
        }
        public void Dispose() { }
    }
}
