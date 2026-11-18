using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Vesperfell
{
    /// <summary>A small stereo reverb (four damped combs and two all-passes per channel).</summary>
    public sealed class Reverb
    {
        sealed class Comb
        {
            readonly float[] buf; int pos; float store;
            public float Feedback = 0.82f, Damp = 0.32f;
            public Comb(int len) { buf = new float[len]; }
            public float Process(float x)
            {
                float y = buf[pos];
                store = y * (1f - Damp) + store * Damp;
                buf[pos] = x + store * Feedback;
                if (++pos >= buf.Length) pos = 0;
                return y;
            }
        }
        sealed class AllPass
        {
            readonly float[] buf; int pos;
            public AllPass(int len) { buf = new float[len]; }
            public float Process(float x)
            {
                float b = buf[pos];
                float y = -x + b;
                buf[pos] = x + b * 0.5f;
                if (++pos >= buf.Length) pos = 0;
                return y;
            }
        }

        readonly Comb[] cl, cr; readonly AllPass[] al, ar;

        public Reverb()
        {
            int[] c = { 1116, 1188, 1277, 1356, 1422, 1491 }; int[] a = { 556, 441, 341 };
            cl = new Comb[c.Length]; cr = new Comb[c.Length]; al = new AllPass[a.Length]; ar = new AllPass[a.Length];
            for (int i = 0; i < c.Length; i++) { cl[i] = new Comb(c[i]); cr[i] = new Comb(c[i] + 23); }
            for (int i = 0; i < a.Length; i++) { al[i] = new AllPass(a[i]); ar[i] = new AllPass(a[i] + 23); }
        }

        public void Process(float[] inL, float[] inR, float[] outL, float[] outR, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float x = (inL[i] + inR[i]) * 0.03f;
                float l = 0, r = 0;
                for (int k = 0; k < cl.Length; k++) { l += cl[k].Process(x); r += cr[k].Process(x); }
                for (int k = 0; k < al.Length; k++) { l = al[k].Process(l); r = ar[k].Process(r); }
                outL[i] = l; outR[i] = r;
            }
        }
    }

    /// <summary>
    /// Audio output through the Windows waveOut API, with three buses: sound effects, generative music and ambience.
    /// Everything is synthesised - there are no audio files. If no device is available the game simply runs silent.
    /// </summary>
    public sealed class AudioEngine : IDisposable
    {
        public const int SR = 44100;
        const int BufFrames = 1024, NumBufs = 4;

        public float VolMaster = 0.9f, VolMusic = 0.7f, VolSfx = 0.8f, VolAmb = 0.7f;
        public bool Ok;
        public string Status = "not started";

        IntPtr hwo; IntPtr[] hdrs, datas; Thread thread; volatile bool running;
        readonly float[] mixL = new float[BufFrames], mixR = new float[BufFrames];
        readonly float[] musL = new float[BufFrames], musR = new float[BufFrames], ambL = new float[BufFrames], ambR = new float[BufFrames];
        readonly float[] sfxL = new float[BufFrames], sfxR = new float[BufFrames], sendL = new float[BufFrames], sendR = new float[BufFrames];
        readonly float[] wetL = new float[BufFrames], wetR = new float[BufFrames];
        readonly short[] pcm = new short[BufFrames * 2];
        float[][] bank;

        struct Voice { public float[] Data; public double Pos; public float Step, VL, VR; public bool Active; }
        struct Cmd { public int Id; public float Vol, Pan, Pitch; }
        readonly Voice[] voices = new Voice[40];
        readonly Queue<Cmd> pending = new Queue<Cmd>();
        readonly object gate = new object();
        public readonly MusicSynth Music = new MusicSynth();
        public readonly AmbSynth Amb = new AmbSynth();
        readonly Reverb reverb = new Reverb();
        volatile float sfxSend = 0.12f;
        float sendNow = 0.12f;

        public void BuildBank() { if (bank == null) bank = SfxBank.Build(); }

        public bool Start()
        {
            try
            {
                if (Native.waveOutGetNumDevs() == 0) { Status = "no output device"; return false; }
                Native.WAVEFORMATEX fmt = new Native.WAVEFORMATEX();
                fmt.wFormatTag = 1; fmt.nChannels = 2; fmt.nSamplesPerSec = SR; fmt.wBitsPerSample = 16;
                fmt.nBlockAlign = 4; fmt.nAvgBytesPerSec = SR * 4; fmt.cbSize = 0;
                int r = Native.waveOutOpen(out hwo, Native.WAVE_MAPPER, ref fmt, IntPtr.Zero, IntPtr.Zero, 0);
                if (r != 0) { Status = "waveOutOpen failed (" + r + ")"; return false; }
                int hsize = Marshal.SizeOf(typeof(Native.WAVEHDR));
                hdrs = new IntPtr[NumBufs]; datas = new IntPtr[NumBufs];
                for (int i = 0; i < NumBufs; i++)
                {
                    datas[i] = Marshal.AllocHGlobal(BufFrames * 4);
                    hdrs[i] = Marshal.AllocHGlobal(hsize);
                    Native.WAVEHDR h = new Native.WAVEHDR();
                    h.lpData = datas[i]; h.dwBufferLength = BufFrames * 4;
                    Marshal.StructureToPtr(h, hdrs[i], false);
                    Native.waveOutPrepareHeader(hwo, hdrs[i], (uint)hsize);
                }
                running = true; Ok = true; Status = "ok";
                thread = new Thread(ThreadLoop);
                thread.IsBackground = true; thread.Name = "Vesperfell audio"; thread.Priority = ThreadPriority.AboveNormal;
                thread.Start();
                return true;
            }
            catch (Exception ex) { Status = ex.Message; Ok = false; return false; }
        }

        void ThreadLoop()
        {
            int hsize = Marshal.SizeOf(typeof(Native.WAVEHDR));
            int flagsOff = (int)Marshal.OffsetOf(typeof(Native.WAVEHDR), "dwFlags");
            while (running)
            {
                bool wrote = false;
                for (int i = 0; i < NumBufs && running; i++)
                {
                    uint flags = (uint)Marshal.ReadInt32(hdrs[i], flagsOff);
                    if ((flags & Native.WHDR_INQUEUE) != 0) continue;
                    try { Mix(mixL, mixR, BufFrames); } catch { Array.Clear(mixL, 0, BufFrames); Array.Clear(mixR, 0, BufFrames); }
                    for (int k = 0; k < BufFrames; k++)
                    {
                        pcm[k * 2] = (short)(mixL[k] * 32000f);
                        pcm[k * 2 + 1] = (short)(mixR[k] * 32000f);
                    }
                    Marshal.Copy(pcm, 0, datas[i], BufFrames * 2);
                    Native.waveOutWrite(hwo, hdrs[i], (uint)hsize);
                    wrote = true;
                }
                if (!wrote) Thread.Sleep(3);
            }
        }

        public void Play(int id, float vol, float pan, float pitch)
        {
            if (bank == null || id < 0 || id >= bank.Length || bank[id] == null || vol <= 0.001f) return;
            Cmd c; c.Id = id; c.Vol = vol; c.Pan = pan; c.Pitch = pitch;
            lock (gate) { if (pending.Count < 48) pending.Enqueue(c); }
        }

        public void SetMusic(int track) { Music.Want = track; }
        public void SetCombat(float level) { Music.CombatTarget = level; }

        public void SetAmbience(int amb, float fire, float water, bool dawn)
        {
            Amb.Zone = amb; Amb.Fire = fire; Amb.Water = water; Amb.Dawn = dawn;
            sfxSend = amb == Zones.AmbDungeon ? 0.36f : 0.12f;
        }

        /// <summary>Renders one block. Public so tests can render audio offline, without a device.</summary>
        public void Mix(float[] L, float[] R, int n)
        {
            lock (gate)
            {
                while (pending.Count > 0)
                {
                    Cmd c = pending.Dequeue();
                    int slot = -1;
                    for (int i = 0; i < voices.Length; i++) if (!voices[i].Active) { slot = i; break; }
                    if (slot < 0) slot = 0;
                    float p = c.Pan < -1 ? -1 : (c.Pan > 1 ? 1 : c.Pan);
                    voices[slot].Data = bank[c.Id]; voices[slot].Pos = 0; voices[slot].Step = c.Pitch <= 0.05f ? 1f : c.Pitch;
                    voices[slot].VL = c.Vol * (p > 0 ? 1f - p * 0.75f : 1f); voices[slot].VR = c.Vol * (p < 0 ? 1f + p * 0.75f : 1f);
                    voices[slot].Active = true;
                }
            }
            Array.Clear(sfxL, 0, n); Array.Clear(sfxR, 0, n);
            for (int v = 0; v < voices.Length; v++)
            {
                if (!voices[v].Active) continue;
                float[] d = voices[v].Data; double pos = voices[v].Pos; float step = voices[v].Step, vl = voices[v].VL, vr = voices[v].VR;
                int last = d.Length - 1;
                for (int i = 0; i < n; i++)
                {
                    int idx = (int)pos;
                    if (idx >= last) { voices[v].Active = false; break; }
                    float fr = (float)(pos - idx);
                    float s = d[idx] + (d[idx + 1] - d[idx]) * fr;
                    sfxL[i] += s * vl; sfxR[i] += s * vr;
                    pos += step;
                }
                voices[v].Pos = pos;
            }
            Music.Render(musL, musR, n);
            Amb.Render(ambL, ambR, n);
            float vm = VolMusic, va = VolAmb, vs = VolSfx, master = VolMaster;
            float target = sfxSend;
            for (int i = 0; i < n; i++)
            {
                sendNow += (target - sendNow) * 0.0005f;
                float ml = musL[i] * vm, mr = musR[i] * vm, al = ambL[i] * va, ar = ambR[i] * va, sl = sfxL[i] * vs, sr = sfxR[i] * vs;
                sendL[i] = ml * 0.9f + sl * sendNow * 3f + al * 0.35f;
                sendR[i] = mr * 0.9f + sr * sendNow * 3f + ar * 0.35f;
                L[i] = ml + al + sl; R[i] = mr + ar + sr;
            }
            reverb.Process(sendL, sendR, wetL, wetR, n);
            for (int i = 0; i < n; i++)
            {
                float l = (L[i] + wetL[i]) * master, r = (R[i] + wetR[i]) * master;
                // gentle limiter
                L[i] = l > 0.6f ? 0.6f + (float)Math.Tanh((l - 0.6f) * 2.2f) * 0.38f : (l < -0.6f ? -0.6f + (float)Math.Tanh((l + 0.6f) * 2.2f) * 0.38f : l);
                R[i] = r > 0.6f ? 0.6f + (float)Math.Tanh((r - 0.6f) * 2.2f) * 0.38f : (r < -0.6f ? -0.6f + (float)Math.Tanh((r + 0.6f) * 2.2f) * 0.38f : r);
            }
        }

        /// <summary>Renders audio without a device (tests and analysis).</summary>
        public float[] RenderOffline(float seconds)
        {
            int total = (int)(seconds * SR);
            float[] res = new float[total * 2];
            float[] l = new float[BufFrames], r = new float[BufFrames];
            for (int done = 0; done < total; done += BufFrames)
            {
                int n = Math.Min(BufFrames, total - done);
                Mix(l, r, BufFrames);
                for (int i = 0; i < n; i++) { res[(done + i) * 2] = l[i]; res[(done + i) * 2 + 1] = r[i]; }
            }
            return res;
        }

        public static void SaveWav(string path, float[] interleaved, int channels)
        {
            using (BinaryWriter w = new BinaryWriter(File.Create(path)))
            {
                int bytes = interleaved.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes); w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)channels); w.Write(SR); w.Write(SR * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
                for (int i = 0; i < interleaved.Length; i++)
                {
                    float v = interleaved[i]; if (v > 1) v = 1; else if (v < -1) v = -1;
                    w.Write((short)(v * 32000f));
                }
            }
        }

        public void Dispose()
        {
            running = false;
            try { if (thread != null) thread.Join(400); } catch { }
            if (hwo != IntPtr.Zero)
            {
                try
                {
                    Native.waveOutReset(hwo);
                    int hsize = Marshal.SizeOf(typeof(Native.WAVEHDR));
                    for (int i = 0; i < NumBufs; i++)
                    {
                        Native.waveOutUnprepareHeader(hwo, hdrs[i], (uint)hsize);
                        Marshal.FreeHGlobal(hdrs[i]); Marshal.FreeHGlobal(datas[i]);
                    }
                    Native.waveOutClose(hwo);
                }
                catch { }
                hwo = IntPtr.Zero;
            }
            Ok = false;
        }
    }
}
