using System;
using System.Drawing;

namespace LoqRgb
{
    /// <summary>Colour animations built from the four zone colours: Drift, Fade and Strobe.</summary>
    internal sealed class AnimationSource : IFrameSource
    {
        static readonly double[] DriftPeriod = { 16, 10, 6, 3 };       // seconds for one full rotation
        static readonly double[] FadeStep = { 4, 2.5, 1.5, 0.8 };       // seconds per colour
        static readonly double[] StrobeStep = { 0.16, 0.11, 0.08, 0.055 }; // seconds per strobe step

        readonly Effect effect;

        public AnimationSource(Effect effect)
        {
            this.effect = effect;
        }

        public int IntervalMs { get { return 25; } }
        public string Problem { get { return null; } }

        static double Smooth(double x)
        {
            return x * x * (3 - 2 * x);
        }

        /// <summary>Colour at a position along the looped sequence zone1 → zone2 → zone3 → zone4 → zone1.</summary>
        static Color Along(Color[] zones, double pos)
        {
            pos = ((pos % 4) + 4) % 4;
            int i = (int)pos;
            return Gfx.Blend(zones[i], zones[(i + 1) % 4], Smooth(pos - i));
        }

        public void Render(LightState s, double t, double dt, Color[] o)
        {
            int sp = Math.Max(1, Math.Min(4, s.Speed)) - 1;
            switch (effect)
            {
                case Effect.Drift:
                    // the colour sequence slides across the zones, left to right
                    double shift = t / DriftPeriod[sp] * 4;
                    for (int z = 0; z < 4; z++) o[z] = Along(s.Zones, z - shift);
                    break;

                case Effect.Fade:
                    Color c = Along(s.Zones, t / FadeStep[sp]);
                    for (int z = 0; z < 4; z++) o[z] = c;
                    break;

                default: // Strobe: double flash on the left half, then double flash on the right half
                    int step = (int)(t / StrobeStep[sp]) % 8;
                    bool left = step == 0 || step == 2;
                    bool right = step == 4 || step == 6;
                    for (int z = 0; z < 4; z++)
                        o[z] = (z < 2 ? left : right) ? s.Zones[z] : Color.Black;
                    break;
            }
        }

        public void Dispose() { }
    }
}
