using System;

namespace Vesperfell
{
    /// <summary>Tiny synthesis toolkit used to build every sound effect at start-up.</summary>
    public static class Syn
    {
        public const int SR = AudioEngine.SR;
        public static Rng R = new Rng(777);
        const float TAU = 6.2831853f;

        public static float[] New(float sec) { return new float[Math.Max(64, (int)(sec * SR))]; }

        /// <summary>Pitched tone with an exponential sweep f0→f1. wave: 0 sine, 1 saw, 2 square, 3 triangle.</summary>
        public static void Tone(float[] b, float start, float dur, float f0, float f1, float amp, float attack, float decay, int wave, float vibHz, float vibAmt)
        {
            int s0 = (int)(start * SR), n = (int)(dur * SR);
            double ph = 0;
            float ratio = f1 / f0;
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i;
                if (k >= b.Length) break;
                float t = i / (float)SR, u = i / (float)n;
                float f = f0 * (float)Math.Pow(ratio, u);
                if (vibHz > 0) f *= 1f + vibAmt * (float)Math.Sin(TAU * vibHz * t);
                ph += f / SR;
                float x = (float)(ph - Math.Floor(ph)), w;
                switch (wave)
                {
                    case 1: w = 2f * x - 1f; break;
                    case 2: w = x < 0.5f ? 1f : -1f; break;
                    case 3: w = 4f * Math.Abs(x - 0.5f) - 1f; break;
                    default: w = (float)Math.Sin(TAU * x); break;
                }
                float env = (attack > 0 ? Math.Min(1f, t / attack) : 1f) * (float)Math.Exp(-decay * t);
                float tail = (dur - t) / 0.006f;
                if (tail < 1f) env *= Math.Max(0f, tail);
                b[k] += w * amp * env;
            }
        }

        public static void Tone(float[] b, float start, float dur, float f0, float f1, float amp, float attack, float decay)
        {
            Tone(b, start, dur, f0, f1, amp, attack, decay, 0, 0, 0);
        }

        /// <summary>Band-passed noise with a sweeping centre. bell = smooth swell-and-fade (whooshes), otherwise attack/decay.</summary>
        public static void Noise(float[] b, float start, float dur, float f0, float f1, float q, float amp, float attack, float decay, bool bell)
        {
            int s0 = (int)(start * SR), n = (int)(dur * SR);
            float low = 0, band = 0, ratio = f1 / f0;
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i;
                if (k >= b.Length) break;
                float t = i / (float)SR, u = i / (float)n;
                float fc = Math.Min(9000f, f0 * (float)Math.Pow(ratio, u));
                float f = 2f * (float)Math.Sin(Math.PI * fc / SR);
                float x = R.F() * 2f - 1f;
                low += f * band;
                float high = x - low - q * band;
                band += f * high;
                float env;
                if (bell) { float s = (float)Math.Sin(Math.PI * u); env = s * (float)Math.Sqrt(s); }
                else
                {
                    env = (attack > 0 ? Math.Min(1f, t / attack) : 1f) * (float)Math.Exp(-decay * t);
                    float tail = (dur - t) / 0.006f;
                    if (tail < 1f) env *= Math.Max(0f, tail);
                }
                b[k] += band * amp * env;
            }
        }

        /// <summary>Low-passed noise (thuds, rumbles) with a sweeping cutoff.</summary>
        public static void LpNoise(float[] b, float start, float dur, float c0, float c1, float amp, float attack, float decay)
        {
            int s0 = (int)(start * SR), n = (int)(dur * SR);
            float st = 0, st2 = 0, ratio = c1 / c0;
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i;
                if (k >= b.Length) break;
                float t = i / (float)SR, u = i / (float)n;
                float a = 1f - (float)Math.Exp(-TAU * c0 * Math.Pow(ratio, u) / SR);
                st += a * ((R.F() * 2f - 1f) - st); st2 += a * (st - st2);
                float env = (attack > 0 ? Math.Min(1f, t / attack) : 1f) * (float)Math.Exp(-decay * t);
                float tail = (dur - t) / 0.006f;
                if (tail < 1f) env *= Math.Max(0f, tail);
                b[k] += st2 * amp * env * 3f;
            }
        }

        /// <summary>High-passed noise (hiss, ticks, shimmer).</summary>
        public static void HpNoise(float[] b, float start, float dur, float cut, float amp, float attack, float decay)
        {
            int s0 = (int)(start * SR), n = (int)(dur * SR);
            float st = 0, a = 1f - (float)Math.Exp(-TAU * cut / SR);
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i;
                if (k >= b.Length) break;
                float t = i / (float)SR;
                float x = R.F() * 2f - 1f;
                st += a * (x - st);
                float env = (attack > 0 ? Math.Min(1f, t / attack) : 1f) * (float)Math.Exp(-decay * t);
                float tail = (dur - t) / 0.004f;
                if (tail < 1f) env *= Math.Max(0f, tail);
                b[k] += (x - st) * amp * env;
            }
        }

        /// <summary>A struck bell: hum, prime, tierce, quint, nominal and upper partials, each with its own decay.</summary>
        public static void Bell(float[] b, float start, float freq, float amp, float decay)
        {
            float[] ratio = { 0.5f, 1f, 1.19f, 1.5f, 2f, 2.51f, 3.01f, 4.1f };
            float[] amps = { 0.45f, 1f, 0.55f, 0.3f, 0.65f, 0.22f, 0.18f, 0.1f };
            float[] dec = { 0.6f, 1f, 1.4f, 1.9f, 2.3f, 3.2f, 3.8f, 5f };
            float len = b.Length / (float)SR - start;
            for (int i = 0; i < ratio.Length; i++)
                Tone(b, start, len, freq * ratio[i], freq * ratio[i], amp * amps[i] * 0.4f, 0.002f, decay * dec[i]);
        }

        /// <summary>A rough animal voice: sawtooth roughened by a fast, unsteady tremolo.</summary>
        public static void Growl(float[] b, float start, float dur, float f0, float f1, float roughHz, float amp)
        {
            int s0 = (int)(start * SR), n = (int)(dur * SR);
            double ph = 0, rp = 0; float st = 0, wob = 0;
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i;
                if (k >= b.Length) break;
                float u = i / (float)n;
                float f = f0 * (float)Math.Pow(f1 / f0, u);
                ph += f / SR;
                wob += ((R.F() * 2f - 1f) - wob) * 0.002f;
                rp += roughHz * (1f + wob * 6f) / SR;
                float saw = 2f * (float)(ph - Math.Floor(ph)) - 1f;
                float rough = 0.5f + 0.5f * (float)Math.Sin(TAU * rp);
                float x = saw * (0.35f + 0.65f * rough);
                st += 0.11f * (x - st);
                float s = (float)Math.Sin(Math.PI * u);
                b[k] += st * amp * (float)Math.Sqrt(s) * 2.2f;
            }
        }

        public static void LowPass(float[] b, float hz)
        {
            float a = 1f - (float)Math.Exp(-TAU * hz / SR), st = 0;
            for (int i = 0; i < b.Length; i++) { st += a * (b[i] - st); b[i] = st; }
        }

        public static void Trem(float[] b, float hz, float depth)
        {
            for (int i = 0; i < b.Length; i++) b[i] *= 1f - depth * (0.5f + 0.5f * (float)Math.Sin(TAU * hz * i / SR));
        }

        public static void Drive(float[] b, float gain)
        {
            for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * gain);
        }

        public static float[] Finish(float[] b, float peak)
        {
            float mx = 0.0001f;
            for (int i = 0; i < b.Length; i++) { float a = Math.Abs(b[i]); if (a > mx) mx = a; }
            float g = peak / mx;
            int fade = Math.Min(b.Length / 4, 220);
            for (int i = 0; i < b.Length; i++)
            {
                float v = b[i] * g;
                int fromEnd = b.Length - 1 - i;
                if (fromEnd < fade) v *= fromEnd / (float)fade;
                b[i] = v;
            }
            return b;
        }
    }

    /// <summary>Synthesises the whole sound-effect library. Takes a fraction of a second at start-up.</summary>
    public static class SfxBank
    {
        public static float[][] Build()
        {
            Syn.R = new Rng(777);
            Rng r = new Rng(31337);
            float[][] k = new float[Sfx.Count][];
            float[] b;

            // interface
            b = Syn.New(0.05f); Syn.Tone(b, 0, 0.045f, 900, 1350, 1, 0.003f, 40); k[Sfx.UiHover] = Syn.Finish(b, 0.22f);
            b = Syn.New(0.07f); Syn.Tone(b, 0, 0.03f, 700, 500, 1, 0.001f, 60); Syn.HpNoise(b, 0, 0.012f, 3000, 0.6f, 0, 200); k[Sfx.UiClick] = Syn.Finish(b, 0.38f);
            b = Syn.New(0.2f); Syn.Noise(b, 0, 0.18f, 400, 2600, 0.5f, 1, 0, 0, true); Syn.Tone(b, 0, 0.16f, 330, 520, 0.4f, 0.02f, 10); k[Sfx.UiOpen] = Syn.Finish(b, 0.3f);
            b = Syn.New(0.2f); Syn.Noise(b, 0, 0.18f, 2600, 400, 0.5f, 1, 0, 0, true); Syn.Tone(b, 0, 0.16f, 520, 330, 0.4f, 0.02f, 10); k[Sfx.UiClose] = Syn.Finish(b, 0.28f);
            b = Syn.New(0.05f); Syn.Tone(b, 0, 0.03f, 1250, 1250, 1, 0.001f, 90); k[Sfx.UiTab] = Syn.Finish(b, 0.28f);
            b = Syn.New(0.18f); Syn.Tone(b, 0, 0.15f, 150, 118, 1, 0.005f, 8, 2, 0, 0); Syn.LowPass(b, 900); k[Sfx.UiError] = Syn.Finish(b, 0.34f);
            b = Syn.New(0.25f); Syn.HpNoise(b, 0, 0.05f, 2500, 0.8f, 0.002f, 60); Syn.Tone(b, 0.01f, 0.2f, 1900, 1500, 0.5f, 0.002f, 18); Syn.Tone(b, 0.01f, 0.2f, 2850, 2700, 0.3f, 0.002f, 22); k[Sfx.Equip] = Syn.Finish(b, 0.38f);
            b = Syn.New(0.22f); Syn.Tone(b, 0, 0.09f, 1976, 1976, 1, 0.001f, 30); Syn.Tone(b, 0.05f, 0.15f, 2637, 2637, 0.9f, 0.001f, 22); k[Sfx.PickGold] = Syn.Finish(b, 0.32f);
            b = Syn.New(0.34f);
            { float[] f = { 1047, 1319, 1568 }; for (int i = 0; i < 3; i++) { Syn.Tone(b, i * 0.055f, 0.2f, f[i], f[i], 1, 0.002f, 16); Syn.Tone(b, i * 0.055f, 0.15f, f[i] * 2, f[i] * 2, 0.25f, 0.002f, 24); } }
            k[Sfx.PickItem] = Syn.Finish(b, 0.38f);
            b = Syn.New(0.16f); Syn.Tone(b, 0, 0.13f, 480, 900, 1, 0.005f, 12); Syn.HpNoise(b, 0, 0.01f, 3500, 0.5f, 0, 200); k[Sfx.PickPotion] = Syn.Finish(b, 0.36f);
            b = Syn.New(0.5f);
            Syn.Tone(b, 0, 0.1f, 240, 170, 1, 0.005f, 14); Syn.Tone(b, 0.13f, 0.1f, 220, 150, 0.9f, 0.005f, 14); Syn.Tone(b, 0.26f, 0.12f, 200, 130, 0.8f, 0.005f, 14);
            Syn.LpNoise(b, 0, 0.4f, 600, 400, 0.12f, 0.02f, 4); k[Sfx.Potion] = Syn.Finish(b, 0.42f);
            b = Syn.New(1.5f);
            { float[] f = { 523.3f, 659.3f, 784f, 1046.5f }; for (int i = 0; i < 4; i++) Syn.Bell(b, i * 0.09f, f[i], 0.8f, 3f); }
            Syn.HpNoise(b, 0.27f, 0.7f, 5000, 0.06f, 0.05f, 5); k[Sfx.LevelUp] = Syn.Finish(b, 0.5f);
            b = Syn.New(1.1f); Syn.Bell(b, 0, 587.3f, 1, 3.5f); Syn.Bell(b, 0.16f, 880f, 0.9f, 3.2f); k[Sfx.QuestStart] = Syn.Finish(b, 0.42f);
            b = Syn.New(1.7f);
            { float[] f = { 587.3f, 740f, 880f, 1174.7f }; float[] t = { 0, 0.13f, 0.26f, 0.42f }; for (int i = 0; i < 4; i++) Syn.Bell(b, t[i], f[i], 0.9f, 2.8f); }
            k[Sfx.QuestDone] = Syn.Finish(b, 0.5f);
            b = Syn.New(1.3f);
            Syn.Tone(b, 0, 1.2f, 110, 220, 0.8f, 0.3f, 1.5f); Syn.Tone(b, 0, 1.2f, 165, 330, 0.5f, 0.3f, 1.5f);
            Syn.Tone(b, 0.2f, 1.0f, 1760, 1780, 0.25f, 0.2f, 3, 0, 6, 0.01f); Syn.HpNoise(b, 0.1f, 1f, 6000, 0.05f, 0.3f, 3);
            k[Sfx.Rune] = Syn.Finish(b, 0.5f);

            // weapons
            b = Syn.New(0.19f); Syn.Noise(b, 0, 0.16f, 700, 2400, 0.45f, 1, 0, 0, true); Syn.Noise(b, 0.04f, 0.13f, 2400, 900, 0.5f, 0.5f, 0, 0, true); k[Sfx.Swing] = Syn.Finish(b, 0.45f);
            b = Syn.New(0.3f); Syn.Noise(b, 0, 0.27f, 300, 1500, 0.4f, 1, 0, 0, true); Syn.LpNoise(b, 0.02f, 0.2f, 300, 120, 0.5f, 0.02f, 12); k[Sfx.SwingHeavy] = Syn.Finish(b, 0.55f);
            b = Syn.New(0.09f); Syn.Noise(b, 0, 0.075f, 1400, 3200, 0.5f, 1, 0, 0, true); k[Sfx.Stab] = Syn.Finish(b, 0.36f);
            b = Syn.New(0.12f); Syn.Tone(b, 0, 0.09f, 160, 60, 1, 0.001f, 28); Syn.LpNoise(b, 0, 0.05f, 1400, 500, 0.7f, 0.001f, 50); k[Sfx.HitFlesh] = Syn.Finish(b, 0.6f);
            b = Syn.New(0.18f); Syn.Tone(b, 0, 0.14f, 120, 45, 1, 0.001f, 18); Syn.LpNoise(b, 0, 0.09f, 1800, 400, 0.9f, 0.001f, 35); Syn.Noise(b, 0, 0.05f, 900, 600, 0.6f, 0.5f, 0.001f, 60, false); k[Sfx.HitHeavy] = Syn.Finish(b, 0.7f);
            b = Syn.New(0.1f); Syn.Noise(b, 0, 0.03f, 1900, 1700, 0.25f, 1, 0.001f, 90, false); Syn.Tone(b, 0, 0.05f, 950, 820, 0.5f, 0.001f, 60); Syn.Noise(b, 0.035f, 0.03f, 2400, 2100, 0.25f, 0.7f, 0.001f, 100, false); k[Sfx.HitBone] = Syn.Finish(b, 0.55f);
            b = Syn.New(0.3f); Syn.Tone(b, 0, 0.25f, 2400, 2380, 0.6f, 0.001f, 14); Syn.Tone(b, 0, 0.2f, 3600, 3560, 0.4f, 0.001f, 18); Syn.HpNoise(b, 0, 0.03f, 4000, 0.8f, 0.001f, 120); k[Sfx.Crit] = Syn.Finish(b, 0.45f);
            b = Syn.New(0.34f);
            { float[] f = { 520, 1130, 1870, 2410, 3100 }; for (int i = 0; i < f.Length; i++) Syn.Tone(b, 0, 0.3f, f[i], f[i] * 0.995f, 0.5f - i * 0.07f, 0.001f, 10 + i * 3); }
            Syn.HpNoise(b, 0, 0.02f, 3000, 1, 0.0005f, 150); Syn.LpNoise(b, 0, 0.05f, 500, 200, 0.6f, 0.001f, 40); k[Sfx.Block] = Syn.Finish(b, 0.6f);
            b = Syn.New(0.15f); Syn.Tone(b, 0, 0.12f, 240, 150, 0.8f, 0.001f, 30, 3, 0, 0); Syn.Noise(b, 0, 0.07f, 2500, 5000, 0.5f, 0.6f, 0.001f, 50, false); k[Sfx.Bow] = Syn.Finish(b, 0.48f);
            b = Syn.New(0.08f); Syn.LpNoise(b, 0, 0.035f, 1500, 600, 1, 0.0005f, 90); Syn.Tone(b, 0, 0.06f, 330, 170, 0.6f, 0.001f, 45); k[Sfx.ArrowHit] = Syn.Finish(b, 0.5f);
            b = Syn.New(0.08f); Syn.Noise(b, 0, 0.065f, 2200, 4500, 0.5f, 1, 0, 0, true); k[Sfx.Knife] = Syn.Finish(b, 0.34f);

            // magic
            b = Syn.New(0.19f); Syn.Noise(b, 0, 0.16f, 500, 1900, 0.6f, 1, 0, 0, true); Syn.Tone(b, 0, 0.14f, 300, 620, 0.3f, 0.01f, 14); k[Sfx.CastEmber] = Syn.Finish(b, 0.42f);
            b = Syn.New(0.36f); Syn.Noise(b, 0, 0.32f, 300, 2500, 0.5f, 1, 0, 0, true); Syn.Tone(b, 0, 0.3f, 90, 170, 0.6f, 0.03f, 6); Syn.LpNoise(b, 0, 0.3f, 800, 2000, 0.3f, 0.05f, 5); k[Sfx.Fireball] = Syn.Finish(b, 0.58f);
            b = Syn.New(0.9f); Syn.Tone(b, 0, 0.5f, 110, 38, 1, 0.001f, 7); Syn.LpNoise(b, 0, 0.7f, 2200, 180, 1, 0.001f, 6); Syn.Noise(b, 0, 0.2f, 1200, 300, 0.8f, 0.6f, 0.001f, 18, false); Syn.Drive(b, 1.8f); k[Sfx.Explode] = Syn.Finish(b, 0.85f);
            b = Syn.New(0.5f); Syn.HpNoise(b, 0, 0.45f, 4500, 0.5f, 0.01f, 7);
            { float[] f = { 1500, 2250, 3000, 3900 }; for (int i = 0; i < 4; i++) Syn.Tone(b, i * 0.03f, 0.3f, f[i], f[i] * 0.8f, 0.25f, 0.002f, 12); }
            Syn.Noise(b, 0, 0.3f, 3000, 800, 0.4f, 0.5f, 0, 0, true); k[Sfx.Frost] = Syn.Finish(b, 0.5f);
            b = Syn.New(0.7f); Syn.HpNoise(b, 0, 0.04f, 1500, 1, 0.0005f, 70); Syn.LpNoise(b, 0.01f, 0.6f, 3500, 250, 1, 0.001f, 7); Syn.Tone(b, 0, 0.5f, 70, 45, 0.7f, 0.01f, 6); Syn.Drive(b, 2f); k[Sfx.Lightning] = Syn.Finish(b, 0.8f);
            b = Syn.New(0.16f); Syn.Tone(b, 0, 0.13f, 400, 1700, 0.7f, 0.01f, 10); Syn.Noise(b, 0, 0.13f, 1000, 5000, 0.5f, 0.5f, 0, 0, true); k[Sfx.Blink] = Syn.Finish(b, 0.42f);

            // skills
            b = Syn.New(0.18f); Syn.Noise(b, 0, 0.16f, 500, 1200, 0.7f, 1, 0, 0, true); k[Sfx.Dodge] = Syn.Finish(b, 0.3f);
            b = Syn.New(0.65f); Syn.LpNoise(b, 0, 0.6f, 700, 180, 1, 0.08f, 5); Syn.Tone(b, 0, 0.5f, 220, 110, 0.2f, 0.1f, 5); k[Sfx.Stealth] = Syn.Finish(b, 0.4f);
            b = Syn.New(0.14f); Syn.Tone(b, 0, 0.02f, 1800, 1500, 1, 0.0005f, 150); Syn.HpNoise(b, 0, 0.015f, 3000, 0.7f, 0, 200); Syn.Tone(b, 0.09f, 0.02f, 1300, 1100, 0.8f, 0.0005f, 150); k[Sfx.TrapSet] = Syn.Finish(b, 0.34f);
            b = Syn.New(0.22f); Syn.HpNoise(b, 0, 0.03f, 2500, 1, 0.0005f, 110); Syn.Tone(b, 0, 0.18f, 800, 760, 0.6f, 0.001f, 18); Syn.Tone(b, 0, 0.15f, 1900, 1850, 0.4f, 0.001f, 22); Syn.LpNoise(b, 0, 0.05f, 600, 300, 0.7f, 0.001f, 50); k[Sfx.TrapSnap] = Syn.Finish(b, 0.65f);
            b = Syn.New(0.7f); for (int i = 0; i < 9; i++) Syn.Noise(b, i * 0.055f + r.F() * 0.02f, 0.09f, 1800 + r.F() * 800, 4200, 0.5f, 0.6f, 0, 0, true); k[Sfx.ArrowRain] = Syn.Finish(b, 0.42f);
            b = Syn.New(0.4f); Syn.Tone(b, 0, 0.35f, 700, 700, 0.6f, 0.005f, 7, 0, 7, 0.012f); Syn.Tone(b, 0, 0.35f, 1050, 1050, 0.4f, 0.005f, 8, 0, 7, 0.012f); Syn.HpNoise(b, 0, 0.02f, 3000, 0.4f, 0, 150); k[Sfx.Mark] = Syn.Finish(b, 0.4f);
            b = Syn.New(0.34f); Syn.Noise(b, 0, 0.3f, 300, 1800, 0.5f, 1, 0, 0, true); Syn.LpNoise(b, 0, 0.08f, 400, 150, 0.9f, 0.001f, 30); k[Sfx.Charge] = Syn.Finish(b, 0.58f);
            b = Syn.New(0.56f); Syn.Noise(b, 0, 0.26f, 500, 2200, 0.5f, 1, 0, 0, true); Syn.Noise(b, 0.22f, 0.3f, 600, 2600, 0.5f, 1, 0, 0, true); k[Sfx.Whirl] = Syn.Finish(b, 0.5f);
            b = Syn.New(1.7f);
            { float[] f = { 293.7f, 440f, 587.3f, 740f }; for (int i = 0; i < 4; i++) Syn.Tone(b, 0, 1.5f, f[i], f[i], 0.3f, 0.25f, 1.8f, 0, 5, 0.004f); }
            Syn.Bell(b, 0.05f, 1174.7f, 0.5f, 3f); k[Sfx.Oath] = Syn.Finish(b, 0.5f);

            // footsteps
            b = Syn.New(0.09f); Syn.Noise(b, 0, 0.075f, 900, 1600, 0.9f, 1, 0, 0, true); k[Sfx.StepGrass] = Syn.Finish(b, 0.24f);
            b = Syn.New(0.06f); Syn.Noise(b, 0, 0.02f, 2400, 2000, 0.3f, 1, 0.0005f, 140, false); Syn.Tone(b, 0, 0.035f, 190, 140, 0.6f, 0.0005f, 80); k[Sfx.StepStone] = Syn.Finish(b, 0.3f);
            b = Syn.New(0.09f); Syn.Tone(b, 0, 0.06f, 250, 170, 1, 0.0005f, 50, 3, 0, 0); Syn.LpNoise(b, 0, 0.03f, 1200, 500, 0.5f, 0.0005f, 90); k[Sfx.StepWood] = Syn.Finish(b, 0.34f);
            b = Syn.New(0.1f); Syn.LpNoise(b, 0, 0.08f, 1000, 500, 1, 0.004f, 40); k[Sfx.StepDirt] = Syn.Finish(b, 0.25f);
            b = Syn.New(0.15f); Syn.LpNoise(b, 0, 0.12f, 600, 300, 1, 0.01f, 22); Syn.Tone(b, 0.03f, 0.08f, 180, 320, 0.5f, 0.01f, 25); k[Sfx.StepMud] = Syn.Finish(b, 0.3f);

            // creatures
            b = Syn.New(0.6f); Syn.Growl(b, 0, 0.55f, 92, 74, 27, 1); k[Sfx.WolfGrowl] = Syn.Finish(b, 0.52f);
            b = Syn.New(0.16f); Syn.HpNoise(b, 0, 0.03f, 2200, 1, 0.0005f, 110); Syn.Growl(b, 0, 0.13f, 140, 100, 40, 0.6f); Syn.Tone(b, 0, 0.04f, 700, 500, 0.3f, 0.001f, 60); k[Sfx.WolfBite] = Syn.Finish(b, 0.55f);
            b = Syn.New(0.4f); Syn.HpNoise(b, 0, 0.36f, 3200, 1, 0.02f, 7); Syn.Trem(b, 31, 0.5f); k[Sfx.Hiss] = Syn.Finish(b, 0.38f);
            b = Syn.New(0.14f); Syn.Noise(b, 0, 0.07f, 1400, 900, 0.5f, 1, 0.002f, 40, false); Syn.Tone(b, 0, 0.1f, 520, 200, 0.6f, 0.002f, 25); k[Sfx.Spit] = Syn.Finish(b, 0.44f);
            b = Syn.New(0.8f); Syn.Growl(b, 0, 0.75f, 115, 86, 9, 1); Syn.Tone(b, 0, 0.75f, 172, 128, 0.25f, 0.1f, 3, 0, 5, 0.03f); Syn.LowPass(b, 700); k[Sfx.Groan] = Syn.Finish(b, 0.45f);
            b = Syn.New(0.44f);
            for (int i = 0; i < 6; i++) { float t = i * 0.055f + r.F() * 0.02f; Syn.Noise(b, t, 0.025f, 1700 + r.F() * 900, 1500, 0.25f, 1, 0.0005f, 110, false); Syn.Tone(b, t, 0.03f, 800 + r.F() * 500, 700, 0.3f, 0.0005f, 90); }
            k[Sfx.BoneRattle] = Syn.Finish(b, 0.44f);
            b = Syn.New(0.45f); Syn.Tone(b, 0, 0.4f, 900, 1400, 0.5f, 0.05f, 6, 0, 9, 0.02f); Syn.HpNoise(b, 0, 0.4f, 5000, 0.1f, 0.08f, 7); k[Sfx.WispCast] = Syn.Finish(b, 0.34f);
            b = Syn.New(0.9f);
            { float[] f = { 98f, 147f, 196f }; for (int i = 0; i < 3; i++) Syn.Tone(b, 0, 0.82f, f[i], f[i] * 0.97f, 0.5f, 0.08f, 2.5f, 1, 5, 0.01f); }
            Syn.LowPass(b, 750); k[Sfx.Chant] = Syn.Finish(b, 0.45f);
            b = Syn.New(0.46f); Syn.Tone(b, 0, 0.3f, 220, 55, 1, 0.001f, 10); Syn.Noise(b, 0, 0.2f, 900, 300, 0.5f, 0.8f, 0.001f, 16, false); Syn.HpNoise(b, 0, 0.3f, 4000, 0.22f, 0.01f, 10); k[Sfx.SigilBlast] = Syn.Finish(b, 0.68f);
            b = Syn.New(0.72f); Syn.Tone(b, 0, 0.6f, 85, 34, 1, 0.001f, 6); Syn.LpNoise(b, 0, 0.5f, 900, 150, 1, 0.001f, 8);
            { float[] f = { 410, 970, 1630 }; for (int i = 0; i < 3; i++) Syn.Tone(b, 0, 0.35f, f[i], f[i] * 0.99f, 0.25f, 0.001f, 9); }
            Syn.Drive(b, 1.6f); k[Sfx.KnightSlam] = Syn.Finish(b, 0.85f);
            b = Syn.New(0.66f); Syn.Noise(b, 0, 0.6f, 400, 2200, 0.35f, 0.8f, 0, 0, true); Syn.Tone(b, 0, 0.6f, 60, 140, 0.6f, 0.4f, 0.5f); k[Sfx.KnightCharge] = Syn.Finish(b, 0.55f);
            b = Syn.New(0.34f); Syn.LpNoise(b, 0, 0.3f, 2200, 300, 1, 0.005f, 10); Syn.Tone(b, 0, 0.28f, 420, 110, 0.6f, 0.005f, 9); k[Sfx.EnemyDie] = Syn.Finish(b, 0.5f);
            b = Syn.New(0.5f); Syn.HpNoise(b, 0, 0.4f, 1800, 0.6f, 0.01f, 8); Syn.Tone(b, 0, 0.35f, 320, 90, 0.5f, 0.01f, 8);
            { float[] t = { 0.05f, 0.12f, 0.2f }; foreach (float tt in t) Syn.Noise(b, tt, 0.025f, 1900, 1600, 0.25f, 0.8f, 0.0005f, 110, false); }
            k[Sfx.UndeadDie] = Syn.Finish(b, 0.5f);
            b = Syn.New(0.16f); Syn.Tone(b, 0, 0.13f, 190, 105, 1, 0.002f, 20, 3, 0, 0); Syn.LpNoise(b, 0, 0.07f, 1200, 500, 0.6f, 0.001f, 40); k[Sfx.PlayerHurt] = Syn.Finish(b, 0.55f);
            b = Syn.New(1.7f); Syn.Tone(b, 0, 1.3f, 300, 55, 0.8f, 0.01f, 2.2f, 0, 4, 0.02f); Syn.Bell(b, 0.1f, 110, 0.6f, 1.5f); Syn.LpNoise(b, 0, 0.8f, 900, 150, 0.4f, 0.02f, 3); k[Sfx.PlayerDie] = Syn.Finish(b, 0.6f);

            // world
            b = Syn.New(0.85f); Syn.Tone(b, 0, 0.3f, 170, 260, 0.5f, 0.05f, 4, 1, 0, 0); Syn.LowPass(b, 1200); Syn.Tone(b, 0.02f, 0.03f, 1500, 1200, 0.6f, 0.0005f, 100);
            { float[] f = { 1568, 1976, 2349 }; for (int i = 0; i < 3; i++) Syn.Tone(b, 0.28f + i * 0.08f, 0.3f, f[i], f[i], 0.5f, 0.001f, 14); }
            k[Sfx.ChestOpen] = Syn.Finish(b, 0.5f);
            b = Syn.New(0.26f);
            { float[] t = { 0, 0.035f, 0.08f }; foreach (float tt in t) Syn.Noise(b, tt, 0.04f, 900 + r.F() * 600, 700, 0.4f, 1, 0.0005f, 70, false); }
            Syn.LpNoise(b, 0, 0.1f, 500, 200, 0.9f, 0.001f, 25); k[Sfx.Break] = Syn.Finish(b, 0.62f);
            b = Syn.New(0.22f); Syn.Noise(b, 0, 0.09f, 1500, 3500, 0.5f, 1, 0, 0, true); Syn.HpNoise(b, 0.04f, 0.16f, 3000, 0.5f, 0.01f, 16); k[Sfx.Cut] = Syn.Finish(b, 0.44f);
            b = Syn.New(1.35f); Syn.LpNoise(b, 0, 1.1f, 350, 250, 1, 0.15f, 1.2f); Syn.Trem(b, 13, 0.6f);
            Syn.Tone(b, 1.0f, 0.2f, 90, 55, 1, 0.001f, 14); Syn.Noise(b, 1.0f, 0.05f, 700, 500, 0.5f, 0.7f, 0.001f, 60, false);
            for (int i = 0; i < 5; i++) Syn.Tone(b, 0.1f + i * 0.17f, 0.08f, 1400 + r.F() * 900, 1300, 0.12f, 0.001f, 40);
            k[Sfx.Gate] = Syn.Finish(b, 0.62f);
            b = Syn.New(0.72f); Syn.Noise(b, 0, 0.5f, 300, 1700, 0.5f, 1, 0, 0, true); Syn.Tone(b, 0, 0.4f, 120, 200, 0.3f, 0.1f, 4);
            for (int i = 0; i < 9; i++) Syn.HpNoise(b, 0.1f + r.F() * 0.55f, 0.008f, 2500, 0.7f, 0, 300);
            k[Sfx.Kindle] = Syn.Finish(b, 0.5f);
            b = Syn.New(2.1f); Syn.Bell(b, 0, 587.3f, 0.8f, 2.2f); Syn.Bell(b, 0.02f, 880f, 0.6f, 2.4f);
            Syn.Tone(b, 0, 1.7f, 293.7f, 293.7f, 0.2f, 0.3f, 1.6f); Syn.Tone(b, 0, 1.7f, 440f, 440f, 0.2f, 0.3f, 1.6f); k[Sfx.Shrine] = Syn.Finish(b, 0.5f);
            b = Syn.New(1.7f); Syn.Bell(b, 0, 110f, 1, 1.6f); Syn.Bell(b, 0, 220.5f, 0.4f, 2f); k[Sfx.BellToll] = Syn.Finish(b, 0.72f);
            b = Syn.New(6.6f);
            { float[] t = { 0, 1.7f, 3.4f }; for (int i = 0; i < 3; i++) { Syn.Bell(b, t[i], 82.4f, 1f - i * 0.1f, 0.55f); Syn.Bell(b, t[i], 164.8f, 0.5f, 0.7f); Syn.Bell(b, t[i] + 0.01f, 329.6f, 0.2f, 1.2f); } }
            Syn.Tone(b, 0.5f, 5.5f, 1318.5f, 1318.5f, 0.05f, 1.5f, 0.5f, 0, 4, 0.003f); k[Sfx.BellGreat] = Syn.Finish(b, 0.85f);
            b = Syn.New(1.35f); Syn.Growl(b, 0, 1.2f, 75, 48, 17, 1); Syn.LpNoise(b, 0, 1.2f, 700, 200, 0.6f, 0.1f, 2); Syn.Tone(b, 0, 1.2f, 37, 30, 0.6f, 0.1f, 1.5f); Syn.Drive(b, 1.5f); k[Sfx.BossRoar] = Syn.Finish(b, 0.8f);
            b = Syn.New(0.34f); Syn.Tone(b, 0, 0.3f, 1600, 300, 0.5f, 0.02f, 8); Syn.HpNoise(b, 0, 0.3f, 3500, 0.4f, 0.1f, 8); k[Sfx.Teleport] = Syn.Finish(b, 0.4f);
            b = Syn.New(0.16f); Syn.Tone(b, 0, 0.07f, 2093, 2093, 1, 0.0005f, 45); Syn.Tone(b, 0.035f, 0.1f, 2794, 2794, 0.8f, 0.0005f, 32); k[Sfx.Coin] = Syn.Finish(b, 0.28f);
            b = Syn.New(0.26f);
            { float[] t = { 0, 0.07f, 0.16f }; foreach (float tt in t) Syn.HpNoise(b, tt, 0.05f, 2200, 0.8f, 0.01f, 45); }
            k[Sfx.Page] = Syn.Finish(b, 0.3f);
            b = Syn.New(2.4f); Syn.Tone(b, 0, 0.6f, 290, 500, 0.7f, 0.2f, 0.3f, 0, 5.5f, 0.012f); Syn.Tone(b, 0.6f, 1.6f, 500, 410, 0.7f, 0.01f, 1.6f, 0, 5.5f, 0.015f);
            Syn.Tone(b, 0, 0.6f, 580, 1000, 0.15f, 0.2f, 0.3f); Syn.Tone(b, 0.6f, 1.6f, 1000, 820, 0.15f, 0.01f, 1.8f); Syn.LowPass(b, 1500); k[Sfx.Howl] = Syn.Finish(b, 0.34f);
            b = Syn.New(0.3f); Syn.LpNoise(b, 0, 0.25f, 2800, 500, 1, 0.002f, 12); Syn.Tone(b, 0.02f, 0.1f, 500, 250, 0.4f, 0.005f, 20); k[Sfx.Splash] = Syn.Finish(b, 0.44f);
            b = Syn.New(0.95f); Syn.LpNoise(b, 0, 0.8f, 600, 150, 1, 0.005f, 4); Syn.Tone(b, 0, 0.4f, 70, 40, 0.8f, 0.001f, 7);
            for (int i = 0; i < 8; i++) Syn.Noise(b, r.F() * 0.6f, 0.03f, 1200 + r.F() * 1200, 1000, 0.3f, 0.5f, 0.0005f, 90, false);
            Syn.Drive(b, 1.5f); k[Sfx.WallBreak] = Syn.Finish(b, 0.8f);
            b = Syn.New(0.7f); Syn.Tone(b, 0, 0.5f, 660, 990, 0.4f, 0.1f, 5); Syn.Tone(b, 0.1f, 0.5f, 990, 1320, 0.3f, 0.1f, 5); Syn.HpNoise(b, 0, 0.5f, 6000, 0.05f, 0.1f, 6); k[Sfx.Heal] = Syn.Finish(b, 0.34f);

            for (int i = 0; i < k.Length; i++) if (k[i] == null) k[i] = Syn.Finish(Syn.New(0.02f), 0f);
            return k;
        }
    }
}
