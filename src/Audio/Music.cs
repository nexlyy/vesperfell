using System;

namespace Vesperfell
{
    /// <summary>
    /// Generative score. Each mood is a short composed progression (chords, a bell melody, plucked strings) played by
    /// a handful of synthesised voices, with small random variations so it never loops exactly. A percussion layer
    /// fades in with combat.
    /// </summary>
    public sealed class MusicSynth
    {
        const int SR = AudioEngine.SR;
        const int SN = 4096;
        static readonly float[] SinT = MakeSin();

        public volatile int Want = -1;
        public volatile float CombatTarget;

        sealed class Track
        {
            public float Bpm = 60; public int[][] Chords; public int[] Bass; public int[][] Mel, Harp;
            public float PadVol = 0.5f, PadBright = 0.5f, BassVol = 0.5f, HarpVol = 0.5f, BellVol = 0.5f, MelProb = 1f, HarpProb = 1f, Sparkle = 0f, BellDecay = 1.3f;
            public bool Drive; public int[] Scale = { 62, 65, 67, 69, 72 };
        }
        static readonly Track[] Tracks = BuildTracks();

        sealed class PadV { public float Freq = 220, Target, Amp, Pan; public double Ph, Ph2; }
        sealed class Pluck { public float[] Buf = new float[1400]; public int Len = 100, Pos, Life; public float Amp, Fb, Pan; public bool On; }
        sealed class BellV { public float Freq, Pan; public bool On; public double[] Ph = new double[5]; public float[] A = new float[5], Mul = new float[5]; public int Life; }
        sealed class Drum { public int Kind; public float T, Amp, Pan; public bool On; public double Ph; }

        readonly PadV[] pad = new PadV[8];
        readonly Pluck[] plk = new Pluck[12];
        readonly BellV[] bells = new BellV[6];
        readonly Drum[] drums = new Drum[6];
        int plkNext, bellNext, drumNext;
        double bassPh; float bassFreq = 73.4f, bassAmp, bassTarget;
        int cur = -1; Track t; float gain; bool fadingOut;
        double stepAcc, samplesPerStep = SR; int step;
        float combat; double cAcc; int cStep;
        double tremPh;
        float h2, h3, h4, h5;
        readonly Rng rng = new Rng(9001);

        static float[] MakeSin()
        {
            float[] s = new float[SN];
            for (int i = 0; i < SN; i++) s[i] = (float)Math.Sin(i * Math.PI * 2 / SN);
            return s;
        }

        static float Freq(int midi) { return 440f * (float)Math.Pow(2, (midi - 69) / 12.0); }

        public MusicSynth()
        {
            for (int i = 0; i < pad.Length; i++) { pad[i] = new PadV(); pad[i].Pan = (i % 2 == 0 ? -0.35f : 0.35f) * (1 + i / 8f); }
            for (int i = 0; i < plk.Length; i++) plk[i] = new Pluck();
            for (int i = 0; i < bells.Length; i++) bells[i] = new BellV();
            for (int i = 0; i < drums.Length; i++) drums[i] = new Drum();
        }

        // the score
        static int[] B(params int[] n) { return n; }

        static Track[] BuildTracks()
        {
            int[] Dm = { 50, 57, 62, 65 }, Bb = { 46, 53, 58, 62 }, F = { 41, 48, 57, 60 }, C = { 48, 55, 60, 64 }, Gm = { 43, 50, 58, 62 };
            int[] As4 = { 45, 52, 57, 62 }, A = { 45, 52, 57, 61 }, G = { 43, 50, 59, 62 }, Am = { 45, 52, 57, 60 }, Eb = { 51, 58, 63, 67 };
            int[] Cm = { 48, 55, 60, 63 }, Eh = { 52, 58, 62, 67 }, D5 = { 50, 57, 62, 69 }, Dd = { 50, 56, 62, 65 }, Bbm = { 46, 53, 58, 61 }, A7 = { 45, 52, 55, 61 };
            int[] D = { 50, 57, 62, 66 }, Bm = { 47, 54, 59, 62 }, A5 = { 45, 52, 57, 64 };
            int[] arp = { -1, 0, -1, -1, 0, -1, -1, 0 }, z = { 0, 0, 0, 0, 0, 0, 0, 0 };
            Track[] tr = new Track[7];

            // 0 - title: the Vesper theme
            Track m = new Track(); m.Bpm = 60;
            m.Chords = new[] { Dm, Bb, F, C, Dm, Gm, As4, A }; m.Bass = new[] { 38, 46, 41, 48, 38, 43, 45, 45 };
            m.Mel = new[]
            {
                B(74, 0, 0, 0, 77, 0, 76, 0), B(74, 0, 0, 0, 0, 0, 69, 0), B(72, 0, 0, 0, 69, 0, 0, 0), B(67, 0, 0, 0, 0, 0, 64, 0),
                B(74, 0, 0, 0, 77, 0, 79, 0), B(79, 0, 0, 0, 77, 0, 74, 0), B(76, 0, 0, 0, 74, 0, 0, 0), B(73, 0, 0, 0, 0, 0, 0, 0),
            };
            m.Harp = new[] { arp, arp, arp, arp, arp, arp, arp, arp };
            m.PadVol = 0.55f; m.PadBright = 0.5f; m.BassVol = 0.5f; m.HarpVol = 0.3f; m.BellVol = 0.6f; m.HarpProb = 0.85f; m.BellDecay = 1.1f;
            tr[Zones.MusMenu] = m;

            // 1 - Emberwick: a lute tune in the Dorian mode
            Track v = new Track(); v.Bpm = 84;
            v.Chords = new[] { Dm, G, Dm, C, F, G, Am, Dm }; v.Bass = new[] { 38, 43, 38, 48, 41, 43, 45, 38 };
            v.Harp = new[]
            {
                B(62, 65, 69, 65, 74, 0, 72, 69), B(71, 0, 67, 71, 74, 0, 71, 0), B(69, 0, 65, 69, 74, 0, 77, 0), B(76, 0, 72, 0, 67, 0, 64, 0),
                B(65, 69, 72, 69, 77, 0, 76, 72), B(74, 0, 71, 67, 71, 0, 74, 0), B(76, 0, 72, 69, 72, 0, 71, 0), B(74, 0, 0, 69, 62, 0, 0, 0),
            };
            v.Mel = new[] { z, z, z, B(0, 0, 0, 0, 0, 0, 0, 79), z, z, z, B(74, 0, 0, 0, 0, 0, 0, 0) };
            v.PadVol = 0.3f; v.PadBright = 0.55f; v.BassVol = 0.4f; v.HarpVol = 0.55f; v.BellVol = 0.35f; v.HarpProb = 0.92f; v.MelProb = 0.6f;
            tr[Zones.MusVillage] = v;

            // 2 - wilds: sparse and watchful
            Track w = new Track(); w.Bpm = 48;
            w.Chords = new[] { Dm, Dm, Bb, Bb, Gm, Gm, Dm, A5 }; w.Bass = new[] { 38, 38, 46, 46, 43, 43, 38, 45 };
            w.Harp = new[]
            {
                B(0, 0, 0, 0, 69, 0, 0, 0), B(0, 0, 74, 0, 0, 0, 0, 0), B(0, 0, 0, 0, 65, 0, 0, 0), B(0, 0, 70, 0, 0, 0, 69, 0),
                B(0, 0, 0, 0, 67, 0, 0, 0), B(0, 0, 74, 0, 0, 0, 70, 0), B(0, 0, 0, 0, 69, 0, 0, 0), B(0, 0, 64, 0, 0, 0, 0, 0),
            };
            w.Mel = new[] { B(62, 0, 0, 0, 0, 0, 0, 0), z, z, z, B(57, 0, 0, 0, 0, 0, 0, 0), z, z, z };
            w.PadVol = 0.5f; w.PadBright = 0.38f; w.BassVol = 0.5f; w.HarpVol = 0.4f; w.BellVol = 0.4f; w.HarpProb = 0.8f; w.MelProb = 0.55f; w.Sparkle = 0.025f;
            tr[Zones.MusWild] = w;

            // 3 - ruins: the Neapolitan chill
            Track r = new Track(); r.Bpm = 44;
            r.Chords = new[] { Dm, Eb, Dm, Cm, Dm, Bb, Eh, A }; r.Bass = new[] { 38, 39, 38, 36, 38, 46, 40, 45 };
            r.Mel = new[]
            {
                B(0, 0, 0, 0, 81, 0, 0, 0), B(0, 0, 79, 0, 0, 0, 0, 0), B(0, 0, 0, 0, 77, 0, 0, 0), B(0, 0, 75, 0, 0, 0, 0, 0),
                B(0, 0, 0, 0, 74, 0, 0, 0), B(0, 0, 0, 0, 0, 0, 77, 0), B(0, 0, 76, 0, 0, 0, 0, 0), B(0, 0, 73, 0, 0, 0, 0, 0),
            };
            r.Harp = new[] { B(50, 0, 0, 0, 0, 0, 0, 0), z, B(50, 0, 0, 57, 0, 0, 0, 0), z, B(50, 0, 0, 0, 0, 0, 0, 0), z, B(52, 0, 0, 0, 0, 0, 0, 0), z };
            r.PadVol = 0.55f; r.PadBright = 0.34f; r.BassVol = 0.55f; r.HarpVol = 0.4f; r.BellVol = 0.38f; r.MelProb = 0.8f; r.BellDecay = 0.9f;
            r.Scale = new[] { 62, 63, 65, 67, 69 };
            tr[Zones.MusRuins] = r;

            // 4 - the undercroft: low strings and a deep, slow toll
            Track d = new Track(); d.Bpm = 40;
            d.Chords = new[] { D5, Dd, Dm, Bbm, D5, Dd, Gm, A7 }; d.Bass = new[] { 38, 38, 38, 46, 38, 38, 43, 45 };
            d.Harp = new[]
            {
                B(38, 0, 0, 0, 0, 0, 0, 0), B(0, 0, 0, 0, 44, 0, 0, 0), B(38, 0, 0, 0, 0, 0, 0, 0), B(0, 0, 0, 0, 46, 0, 0, 49),
                B(38, 0, 0, 0, 0, 0, 45, 0), B(0, 0, 0, 0, 44, 0, 0, 0), B(43, 0, 0, 0, 0, 0, 0, 0), B(45, 0, 0, 0, 0, 0, 0, 0),
            };
            d.Mel = new[] { B(50, 0, 0, 0, 0, 0, 0, 0), z, z, z, B(50, 0, 0, 0, 0, 0, 0, 0), z, z, z };
            d.PadVol = 0.5f; d.PadBright = 0.26f; d.BassVol = 0.65f; d.HarpVol = 0.5f; d.BellVol = 0.5f; d.MelProb = 0.7f; d.BellDecay = 0.6f;
            d.Scale = new[] { 62, 65, 68, 69, 72 };
            tr[Zones.MusDungeon] = d;

            // 5 - the Silent Prior: driving ostinato under the theme
            Track b = new Track(); b.Bpm = 116; b.Drive = true;
            b.Chords = new[] { Dm, Dm, Bb, Bb, Gm, Gm, A, A }; b.Bass = new[] { 38, 38, 46, 46, 43, 43, 45, 45 };
            b.Harp = new[]
            {
                B(38, 38, 50, 38, 45, 38, 50, 48), B(38, 38, 50, 38, 45, 38, 50, 48), B(46, 46, 58, 46, 53, 46, 58, 57), B(46, 46, 58, 46, 53, 46, 58, 57),
                B(43, 43, 55, 43, 50, 43, 55, 53), B(43, 43, 55, 43, 50, 43, 55, 53), B(45, 45, 57, 45, 52, 45, 61, 57), B(45, 45, 57, 45, 52, 45, 61, 57),
            };
            b.Mel = new[]
            {
                B(74, 0, 77, 0, 76, 0, 74, 0), B(69, 0, 0, 0, 0, 0, 0, 0), B(74, 0, 77, 0, 79, 0, 77, 0), B(74, 0, 0, 0, 0, 0, 0, 0),
                B(79, 0, 77, 0, 74, 0, 70, 0), B(69, 0, 0, 0, 67, 0, 0, 0), B(76, 0, 73, 0, 69, 0, 73, 0), B(76, 0, 0, 0, 0, 0, 0, 0),
            };
            b.PadVol = 0.5f; b.PadBright = 0.6f; b.BassVol = 0.6f; b.HarpVol = 0.5f; b.BellVol = 0.5f; b.BellDecay = 2.2f;
            tr[Zones.MusBoss] = b;

            // 6 - dawn: the theme, at last in the major
            Track n = new Track(); n.Bpm = 66;
            n.Chords = new[] { D, G, Bm, A, D, G, A, D }; n.Bass = new[] { 38, 43, 47, 45, 38, 43, 45, 38 };
            n.Mel = new[]
            {
                B(74, 0, 0, 0, 78, 0, 76, 0), B(74, 0, 0, 0, 0, 0, 71, 0), B(74, 0, 0, 0, 71, 0, 0, 0), B(73, 0, 0, 0, 0, 0, 69, 0),
                B(74, 0, 0, 0, 78, 0, 81, 0), B(79, 0, 0, 0, 78, 0, 74, 0), B(76, 0, 0, 0, 73, 0, 0, 0), B(74, 0, 0, 0, 0, 0, 0, 0),
            };
            int[] arp2 = { 0, -1, 0, -1, 0, -1, -1, -1 };
            n.Harp = new[] { arp2, arp2, arp2, arp2, arp2, arp2, arp2, arp2 };
            n.PadVol = 0.5f; n.PadBright = 0.62f; n.BassVol = 0.45f; n.HarpVol = 0.36f; n.BellVol = 0.6f; n.HarpProb = 0.9f; n.BellDecay = 1.0f;
            n.Scale = new[] { 62, 64, 66, 69, 71 };
            tr[Zones.MusDawn] = n;
            return tr;
        }

        // voices
        void SwitchTo(int track)
        {
            cur = track;
            t = track >= 0 && track < Tracks.Length ? Tracks[track] : null;
            step = 0;
            for (int i = 0; i < pad.Length; i++) pad[i].Target = 0;
            bassTarget = 0;
            if (t == null) return;
            samplesPerStep = SR * 60.0 / t.Bpm / 2.0;
            stepAcc = samplesPerStep;
            float br = t.PadBright;
            h2 = 0.5f * br; h3 = 0.34f * br * br; h4 = 0.25f * br * br * br; h5 = 0.2f * br * br * br * br;
        }

        void SetChord(int[] notes)
        {
            for (int i = 0; i < pad.Length; i++) pad[i].Target = 0;
            bool[] used = new bool[pad.Length];
            for (int k = 0; k < notes.Length; k++)
            {
                int best = -1; float bestAmp = 1e9f;
                for (int i = 0; i < pad.Length; i++) if (!used[i] && pad[i].Amp < bestAmp) { bestAmp = pad[i].Amp; best = i; }
                if (best < 0) break;
                used[best] = true;
                pad[best].Freq = Freq(notes[k]); pad[best].Target = t.PadVol * 0.085f;
            }
        }

        void PluckNote(float freq, float amp, float pan)
        {
            Pluck p = plk[plkNext]; plkNext = (plkNext + 1) % plk.Length;
            p.Len = M.Clamp((int)(SR / freq), 16, 1399);
            float st = 0, bright = 0.25f + 0.5f * Math.Min(1f, amp * 2f);
            for (int i = 0; i < p.Len; i++) { st += bright * ((rng.F() * 2f - 1f) - st); p.Buf[i] = st; }
            p.Pos = 0; p.Amp = amp; p.Pan = pan; p.On = true; p.Life = SR * 4;
            p.Fb = freq < 110 ? 0.9975f : (freq < 300 ? 0.996f : 0.9945f);
        }

        void Strike(float freq, float amp, float decay, float pan)
        {
            BellV b = bells[bellNext]; bellNext = (bellNext + 1) % bells.Length;
            float[] amps = { 1f, 0.42f, 0.28f, 0.14f, 0.09f }, decs = { 1f, 1.5f, 2.3f, 3.4f, 4.8f };
            b.Freq = freq; b.Pan = pan; b.On = true; b.Life = SR * 6;
            for (int i = 0; i < 5; i++) { b.A[i] = amp * amps[i]; b.Mul[i] = (float)Math.Exp(-decay * decs[i] / SR); b.Ph[i] = 0; }
        }

        void Hit(int kind, float amp, float pan)
        {
            if (amp <= 0.01f) return;
            Drum d = drums[drumNext]; drumNext = (drumNext + 1) % drums.Length;
            d.Kind = kind; d.T = 0; d.Amp = amp; d.Pan = pan; d.On = true; d.Ph = 0;
        }

        static readonly int[] PatLow = { 1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 0 };
        static readonly int[] PatTom = { 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 1 };
        static readonly int[] PatTick = { 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1 };

        void OnStep()
        {
            int bars = t.Chords.Length;
            int bar = (step / 8) % bars, s = step % 8;
            if (s == 0)
            {
                SetChord(t.Chords[bar]);
                bassFreq = Freq(t.Bass[bar]); bassTarget = t.BassVol * 0.11f;
            }
            int mnote = t.Mel != null ? t.Mel[bar % t.Mel.Length][s] : 0;
            if (mnote > 0 && rng.F() < t.MelProb) Strike(Freq(mnote), t.BellVol * 0.16f, t.BellDecay, rng.Sym(0.3f));
            int h = t.Harp != null ? t.Harp[bar % t.Harp.Length][s] : 0;
            if (h == -1) { int[] ch = t.Chords[bar]; h = ch[(s * 3 + bar) % ch.Length] + 12; }
            if (h > 0 && rng.F() < t.HarpProb) PluckNote(Freq(h), t.HarpVol * (0.2f + rng.F() * 0.08f), rng.Sym(0.5f));
            else if (t.Sparkle > 0 && rng.F() < t.Sparkle) PluckNote(Freq(t.Scale[rng.Int(t.Scale.Length)] + 12), t.HarpVol * 0.14f, rng.Sym(0.7f));
            step++;
        }

        void OnCombatStep()
        {
            float v = t != null && t.Drive ? 1f : combat;
            int s = cStep & 15;
            if (PatLow[s] != 0) Hit(0, v * 0.5f, -0.1f);
            if (PatTom[s] != 0) Hit(1, v * 0.3f, 0.3f);
            if (PatTick[s] != 0) Hit(2, v * 0.09f, -0.4f);
            if (t != null && !t.Drive && v > 0.3f && (s & 1) == 0) PluckNote(bassFreq * 2f, v * 0.11f, rng.Sym(0.3f));
            cStep++;
        }

        // rendering
        public void Render(float[] L, float[] R, int n)
        {
            double cSamples = SR * 60.0 / 112.0 / 2.0;
            for (int i = 0; i < n; i++)
            {
                if (cur != Want && !fadingOut) fadingOut = true;
                if (fadingOut)
                {
                    gain -= 1f / (SR * 1.4f);
                    if (gain <= 0) { gain = 0; fadingOut = false; SwitchTo(Want); }
                }
                else if (t != null && gain < 1f) gain = Math.Min(1f, gain + 1f / (SR * 2.5f));
                combat += (CombatTarget - combat) * 0.00005f;

                if (t != null)
                {
                    stepAcc += 1;
                    if (stepAcc >= samplesPerStep) { stepAcc -= samplesPerStep; OnStep(); }
                    if (combat > 0.03f || t.Drive)
                    {
                        cAcc += 1;
                        if (cAcc >= cSamples) { cAcc -= cSamples; OnCombatStep(); }
                    }
                }
                float l = 0, r = 0;
                tremPh += 0.35 / SR; if (tremPh >= 1) tremPh -= 1;
                float trem = 0.86f + 0.14f * SinT[(int)(tremPh * SN) & (SN - 1)];

                for (int k = 0; k < pad.Length; k++)
                {
                    PadV v = pad[k];
                    v.Amp += (v.Target - v.Amp) * (v.Target > v.Amp ? 0.00004f : 0.000028f);
                    if (v.Amp < 0.0004f) continue;
                    double inc = v.Freq / SR;
                    v.Ph += inc; if (v.Ph >= 1) v.Ph -= 1;
                    v.Ph2 += inc * 1.0045; if (v.Ph2 >= 1) v.Ph2 -= 1;
                    double a = v.Ph * SN, b = v.Ph2 * SN;
                    float s = SinT[(int)a & (SN - 1)] + h2 * SinT[(int)(a * 2) & (SN - 1)] + h3 * SinT[(int)(a * 3) & (SN - 1)] + h4 * SinT[(int)(a * 4) & (SN - 1)] + h5 * SinT[(int)(a * 5) & (SN - 1)];
                    float s2 = SinT[(int)b & (SN - 1)] + h2 * SinT[(int)(b * 2) & (SN - 1)] + h3 * SinT[(int)(b * 3) & (SN - 1)];
                    float sig = (s + s2) * 0.5f * v.Amp * trem;
                    l += sig * (1f - v.Pan * 0.5f); r += sig * (1f + v.Pan * 0.5f);
                }

                bassAmp += (bassTarget - bassAmp) * 0.00006f;
                if (bassAmp > 0.0004f)
                {
                    bassPh += bassFreq / SR; if (bassPh >= 1) bassPh -= 1;
                    double a = bassPh * SN;
                    float s = (SinT[(int)a & (SN - 1)] + 0.35f * SinT[(int)(a * 2) & (SN - 1)] + 0.12f * SinT[(int)(a * 3) & (SN - 1)]) * bassAmp;
                    l += s; r += s;
                }

                for (int k = 0; k < plk.Length; k++)
                {
                    Pluck p = plk[k];
                    if (!p.On) continue;
                    int nx = p.Pos + 1; if (nx >= p.Len) nx = 0;
                    float y = p.Buf[p.Pos];
                    p.Buf[p.Pos] = (y + p.Buf[nx]) * 0.5f * p.Fb;
                    p.Pos = nx;
                    float s = y * p.Amp;
                    l += s * (1f - p.Pan * 0.6f); r += s * (1f + p.Pan * 0.6f);
                    if (--p.Life <= 0) p.On = false;
                }

                for (int k = 0; k < bells.Length; k++)
                {
                    BellV b = bells[k];
                    if (!b.On) continue;
                    float s = 0;
                    double inc = b.Freq / SR;
                    b.Ph[0] += inc; if (b.Ph[0] >= 1) b.Ph[0] -= 1; s += b.A[0] * SinT[(int)(b.Ph[0] * SN) & (SN - 1)]; b.A[0] *= b.Mul[0];
                    b.Ph[1] += inc * 2.0; if (b.Ph[1] >= 1) b.Ph[1] -= 1; s += b.A[1] * SinT[(int)(b.Ph[1] * SN) & (SN - 1)]; b.A[1] *= b.Mul[1];
                    b.Ph[2] += inc * 3.01; if (b.Ph[2] >= 1) b.Ph[2] -= 1; s += b.A[2] * SinT[(int)(b.Ph[2] * SN) & (SN - 1)]; b.A[2] *= b.Mul[2];
                    b.Ph[3] += inc * 4.2; if (b.Ph[3] >= 1) b.Ph[3] -= 1; s += b.A[3] * SinT[(int)(b.Ph[3] * SN) & (SN - 1)]; b.A[3] *= b.Mul[3];
                    b.Ph[4] += inc * 5.4; if (b.Ph[4] >= 1) b.Ph[4] -= 1; s += b.A[4] * SinT[(int)(b.Ph[4] * SN) & (SN - 1)]; b.A[4] *= b.Mul[4];
                    l += s * (1f - b.Pan * 0.6f); r += s * (1f + b.Pan * 0.6f);
                    if (--b.Life <= 0 || b.A[0] < 0.0002f) b.On = false;
                }

                for (int k = 0; k < drums.Length; k++)
                {
                    Drum d = drums[k];
                    if (!d.On) continue;
                    float s;
                    if (d.Kind == 2)
                    {
                        s = (rng.F() * 2f - 1f) * (float)Math.Exp(-d.T * 95f);
                        if (d.T > 0.06f) d.On = false;
                    }
                    else
                    {
                        float f = d.Kind == 0 ? 46f + 78f * (float)Math.Exp(-d.T * 30f) : 88f + 84f * (float)Math.Exp(-d.T * 26f);
                        d.Ph += f / SR; if (d.Ph >= 1) d.Ph -= 1;
                        s = SinT[(int)(d.Ph * SN) & (SN - 1)] * (float)Math.Exp(-d.T * (d.Kind == 0 ? 7.5f : 10f));
                        if (d.T < 0.006f) s += (rng.F() * 2f - 1f) * 0.5f;
                        if (d.T > 0.7f) d.On = false;
                    }
                    s *= d.Amp;
                    l += s * (1f - d.Pan * 0.5f); r += s * (1f + d.Pan * 0.5f);
                    d.T += 1f / SR;
                }
                L[i] = l * gain; R[i] = r * gain;
            }
        }
    }

    /// <summary>Ambient sound beds: wind, night insects, owls and far-off howls, marsh frogs, dripping stone, birdsong at dawn.</summary>
    public sealed class AmbSynth
    {
        const int SR = AudioEngine.SR;
        public volatile int Zone;
        public volatile float Fire, Water;
        public volatile bool Dawn;

        sealed class Ev { public int Kind; public float T, Dur, F0, F1, Amp, Pan, Rate; public bool On; public double Ph, Ph2; }
        readonly Ev[] evs = new Ev[12];
        int evNext;
        readonly Rng rng = new Rng(555);
        float gWind, gCrick, gForest, gMarsh, gRuin, gDung, gFire, gWater, gBird;
        float wl1, wl2, wr1, wr2, rum1, rum2, wa1, wa2, wb1, fr1;
        double lfo1, lfo2, moan1, moan2;
        float tChirp = 1, tOwl = 12, tHowl = 30, tFrog = 2, tDrip = 2, tBird = 1, tCrackle, tBubble = 3;
        int birdBurst;

        public AmbSynth() { for (int i = 0; i < evs.Length; i++) evs[i] = new Ev(); }

        Ev Add(int kind, float dur, float f0, float f1, float amp, float pan, float rate)
        {
            Ev e = evs[evNext]; evNext = (evNext + 1) % evs.Length;
            e.Kind = kind; e.T = 0; e.Dur = dur; e.F0 = f0; e.F1 = f1; e.Amp = amp; e.Pan = pan; e.Rate = rate; e.On = true; e.Ph = 0; e.Ph2 = 0;
            return e;
        }

        public void Render(float[] L, float[] R, int n)
        {
            int z = Zone; bool dawn = Dawn;
            float tw = 0, tc = 0, tf = 0, tm = 0, tr = 0, td = 0, tb = 0;
            switch (z)
            {
                case Zones.AmbField: tw = 0.5f; tc = 0.55f; break;
                case Zones.AmbVillage: tw = 0.35f; tc = 0.3f; break;
                case Zones.AmbForest: tw = 0.45f; tc = 0.75f; tf = 1f; break;
                case Zones.AmbMarsh: tw = 0.3f; tc = 0.45f; tm = 1f; break;
                case Zones.AmbRuins: tw = 0.85f; tr = 1f; tc = 0.15f; break;
                case Zones.AmbDungeon: td = 1f; tw = 0.12f; break;
            }
            if (dawn && z != Zones.AmbDungeon) { tb = 1f; tc = 0; tf = 0; tr = 0; tw = 0.3f; }
            const float k = 0.03f;
            gWind += (tw - gWind) * k; gCrick += (tc - gCrick) * k; gForest += (tf - gForest) * k; gMarsh += (tm - gMarsh) * k;
            gRuin += (tr - gRuin) * k; gDung += (td - gDung) * k; gBird += (tb - gBird) * k;
            gFire += (Fire - gFire) * 0.08f; gWater += (Water - gWater) * 0.08f;
            float blk = n / (float)SR;

            // ---- schedule one-shot voices
            tChirp -= blk;
            if (tChirp <= 0 && gCrick > 0.05f)
            {
                tChirp = 0.25f + rng.F() * 1.1f;
                Add(1, 0.22f + rng.F() * 0.3f, 4300 + rng.F() * 700, 0, 0.03f * gCrick, rng.Sym(0.9f), 26 + rng.F() * 14);
            }
            tOwl -= blk;
            if (tOwl <= 0 && gForest > 0.3f) { tOwl = 14 + rng.F() * 22; Add(2, 1.25f, 330 + rng.F() * 40, 0, 0.035f * gForest, rng.Sym(0.8f), 0); }
            tHowl -= blk;
            if (tHowl <= 0 && gForest > 0.3f) { tHowl = 35 + rng.F() * 50; Add(3, 2.4f, 290 + rng.F() * 30, 0, 0.022f * gForest, rng.Sym(0.9f), 0); }
            tFrog -= blk;
            if (tFrog <= 0 && gMarsh > 0.1f)
            {
                tFrog = rng.Chance(0.4f) ? 0.3f : 0.9f + rng.F() * 2.6f;
                Add(4, 0.24f + rng.F() * 0.2f, 200 + rng.F() * 110, 0, 0.03f * gMarsh, rng.Sym(0.9f), 21 + rng.F() * 9);
            }
            tBubble -= blk;
            if (tBubble <= 0 && gMarsh > 0.1f) { tBubble = 1.5f + rng.F() * 4f; Add(8, 0.08f, 280 + rng.F() * 150, 700, 0.02f * gMarsh, rng.Sym(0.8f), 0); }
            tDrip -= blk;
            if (tDrip <= 0 && gDung > 0.1f) { tDrip = 1.5f + rng.F() * 4.5f; Add(5, 0.7f, 1100 + rng.F() * 700, 0, 0.05f * gDung, rng.Sym(0.9f), 0); }
            tBird -= blk;
            if (tBird <= 0 && gBird > 0.1f)
            {
                if (birdBurst <= 0) birdBurst = 2 + rng.Int(4);
                float f = 2400 + rng.F() * 1900;
                Add(6, 0.07f + rng.F() * 0.13f, f, f * (0.72f + rng.F() * 0.7f), 0.03f * gBird, rng.Sym(0.9f), 0);
                birdBurst--;
                tBird = birdBurst > 0 ? 0.09f + rng.F() * 0.16f : 0.9f + rng.F() * 3.5f;
            }
            tCrackle -= blk;
            if (tCrackle <= 0 && gFire > 0.03f)
            {
                tCrackle = 0.02f + rng.F() * 0.16f;
                Add(7, 0.004f + rng.F() * 0.01f, 0, 0, (0.03f + rng.F() * 0.07f) * gFire, rng.Sym(0.5f), 0);
            }

            for (int i = 0; i < n; i++)
            {
                float l = 0, r = 0;
                lfo1 += 0.07 / SR; if (lfo1 >= 1) lfo1 -= 1;
                lfo2 += 0.113 / SR; if (lfo2 >= 1) lfo2 -= 1;
                float a1 = 0.5f + 0.5f * (float)Math.Sin(lfo1 * 6.2831853), a2 = 0.5f + 0.5f * (float)Math.Sin(lfo2 * 6.2831853 + 1.3);
                float nl = rng.F() * 2f - 1f, nr = rng.F() * 2f - 1f;

                if (gWind > 0.003f)
                {
                    float c1 = 0.035f + 0.045f * a1, c2 = 0.035f + 0.045f * a2;
                    wl1 += c1 * (nl - wl1); wl2 += c1 * (wl1 - wl2);
                    wr1 += c2 * (nr - wr1); wr2 += c2 * (wr1 - wr2);
                    float g = gWind * 0.34f;
                    l += wl2 * g * (0.45f + 0.55f * a2); r += wr2 * g * (0.45f + 0.55f * a1);
                }
                if (gRuin > 0.003f)
                {
                    moan1 += 110.0 / SR; if (moan1 >= 1) moan1 -= 1;
                    moan2 += 116.4 / SR; if (moan2 >= 1) moan2 -= 1;
                    float s = ((float)Math.Sin(moan1 * 6.2831853) + (float)Math.Sin(moan2 * 6.2831853)) * 0.012f * gRuin * a1;
                    l += s; r += s;
                }
                if (gDung > 0.003f)
                {
                    rum1 += 0.012f * (nl - rum1); rum2 += 0.012f * (rum1 - rum2);
                    float s = rum2 * 1.6f * gDung;
                    l += s; r += s;
                }
                if (gFire > 0.003f)
                {
                    fr1 += 0.04f * (nr - fr1);
                    float s = fr1 * 0.2f * gFire * (0.7f + 0.3f * a2);
                    l += s; r += s;
                }
                if (gWater > 0.003f)
                {
                    wa1 += 0.3f * (nl - wa1); wa2 += 0.09f * (nl - wa2); wb1 += 0.3f * (nr - wb1);
                    float g = 0.11f * gWater * (0.75f + 0.25f * a1);
                    l += (wa1 - wa2) * g; r += (wb1 - wa2) * g;
                }

                for (int v = 0; v < evs.Length; v++)
                {
                    Ev e = evs[v];
                    if (!e.On) continue;
                    float u = e.T / e.Dur, s = 0;
                    switch (e.Kind)
                    {
                        case 1:     // cricket chirp: a pulsed high tone
                            {
                                e.Ph += e.F0 / SR; e.Ph2 += e.Rate / SR;
                                float pulse = (float)Math.Sin(e.Ph2 * 6.2831853); pulse = pulse > 0.2f ? 1f : 0f;
                                s = (float)Math.Sin(e.Ph * 6.2831853) * pulse * (float)Math.Sin(Math.PI * u);
                            }
                            break;
                        case 2:     // owl: two soft hoots
                            {
                                float env = 0;
                                if (e.T < 0.34f) env = (float)Math.Sin(Math.PI * e.T / 0.34f);
                                else if (e.T > 0.52f && e.T < 1.2f) env = (float)Math.Sin(Math.PI * (e.T - 0.52f) / 0.68f);
                                e.Ph += e.F0 * (1f - 0.04f * u) / SR;
                                s = (float)Math.Sin(e.Ph * 6.2831853) * env * env;
                            }
                            break;
                        case 3:     // a wolf, a long way off
                            {
                                float f = u < 0.25f ? e.F0 * (1f + 0.7f * (u / 0.25f)) : e.F0 * (1.7f - 0.3f * ((u - 0.25f) / 0.75f));
                                f *= 1f + 0.012f * (float)Math.Sin(e.T * 34.5f);
                                e.Ph += f / SR;
                                float env = Math.Min(1f, u * 5f) * Math.Min(1f, (1f - u) * 2.2f);
                                s = ((float)Math.Sin(e.Ph * 6.2831853) + 0.25f * (float)Math.Sin(e.Ph * 12.5663706)) * env;
                            }
                            break;
                        case 4:     // frog
                            {
                                e.Ph += e.F0 / SR; e.Ph2 += e.Rate / SR;
                                float am = 0.5f + 0.5f * (float)Math.Sin(e.Ph2 * 6.2831853);
                                s = ((float)Math.Sin(e.Ph * 6.2831853) + 0.5f * (float)Math.Sin(e.Ph * 12.5663706)) * am * am * (float)Math.Sin(Math.PI * u);
                            }
                            break;
                        case 5:     // drip with two echoes
                            {
                                e.Ph += e.F0 * (1f - 0.25f * Math.Min(1f, e.T * 10f)) / SR;
                                float env = (float)Math.Exp(-e.T * 30f);
                                if (e.T > 0.17f) env += 0.4f * (float)Math.Exp(-(e.T - 0.17f) * 30f);
                                if (e.T > 0.34f) env += 0.16f * (float)Math.Exp(-(e.T - 0.34f) * 30f);
                                s = (float)Math.Sin(e.Ph * 6.2831853) * env;
                            }
                            break;
                        case 6:     // bird
                        case 8:     // marsh bubble
                            {
                                float f = e.F0 + (e.F1 - e.F0) * u;
                                e.Ph += f / SR;
                                s = (float)Math.Sin(e.Ph * 6.2831853) * (float)Math.Sin(Math.PI * u);
                            }
                            break;
                        case 7:     // ember crackle
                            s = (rng.F() * 2f - 1f) * (1f - u);
                            break;
                    }
                    s *= e.Amp;
                    l += s * (1f - e.Pan * 0.7f); r += s * (1f + e.Pan * 0.7f);
                    e.T += 1f / SR;
                    if (e.T >= e.Dur) e.On = false;
                }
                L[i] = l; R[i] = r;
            }
        }
    }
}
