using System.Collections.Generic;
using System;
using System.IO;
using System.Text;

namespace Vesperfell
{
    /// <summary>Float math helpers (the framework only ships double versions).</summary>
    public static class M
    {
        public const float PI = 3.14159265f;
        public const float TAU = 6.2831853f;
        public static float Sin(float a) { return (float)Math.Sin(a); }
        public static float Cos(float a) { return (float)Math.Cos(a); }
        public static float Sqrt(float a) { return (float)Math.Sqrt(a); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Pow(float a, float b) { return (float)Math.Pow(a, b); }
        public static float Abs(float a) { return a < 0 ? -a : a; }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static float Clamp01(float v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        public static int Floor(float v) { int i = (int)v; return (v < i) ? i - 1 : i; }
        public static int Round(float v) { return Floor(v + 0.5f); }
        public static int Ceil(float v) { int i = (int)v; return (v > i) ? i + 1 : i; }
        public static float Dist(float x0, float y0, float x1, float y1) { float dx = x1 - x0, dy = y1 - y0; return (float)Math.Sqrt(dx * dx + dy * dy); }
        public static float Dist2(float x0, float y0, float x1, float y1) { float dx = x1 - x0, dy = y1 - y0; return dx * dx + dy * dy; }
        public static float Len(float x, float y) { return (float)Math.Sqrt(x * x + y * y); }
        public static float Smooth(float t) { t = Clamp01(t); return t * t * (3 - 2 * t); }
        public static float Sign(float v) { return v < 0 ? -1 : (v > 0 ? 1 : 0); }
        /// <summary>Shortest signed difference between two angles, in (-PI, PI].</summary>
        public static float AngDiff(float a, float b)
        {
            float d = (b - a) % TAU;
            if (d > PI) d -= TAU; else if (d < -PI) d += TAU;
            return d;
        }
        public static float Approach(float v, float target, float step)
        {
            if (v < target) { v += step; return v > target ? target : v; }
            v -= step; return v < target ? target : v;
        }
        /// <summary>Frame-rate independent exponential smoothing factor.</summary>
        public static float Damp(float rate, float dt) { return 1f - (float)Math.Exp(-rate * dt); }
        public static float EaseOut(float t) { t = Clamp01(t); return 1 - (1 - t) * (1 - t); }
        public static float EaseIn(float t) { t = Clamp01(t); return t * t; }
        public static float EaseOutBack(float t)
        {
            t = Clamp01(t); float c1 = 1.70158f, c3 = c1 + 1; float u = t - 1;
            return 1 + c3 * u * u * u + c1 * u * u;
        }
    }

    /// <summary>Small deterministic xorshift generator. Every procedural system takes one so results are reproducible.</summary>
    public sealed class Rng
    {
        uint s;
        public Rng(int seed) { s = (uint)seed * 747796405u + 2891336453u; if (s == 0) s = 0x9E3779B9; Next(); Next(); }
        public uint Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
        /// <summary>Integer in [0, n).</summary>
        public int Int(int n) { return n <= 0 ? 0 : (int)(Next() % (uint)n); }
        /// <summary>Integer in [a, b] inclusive.</summary>
        public int Range(int a, int b) { return b <= a ? a : a + (int)(Next() % (uint)(b - a + 1)); }
        public float F() { return (Next() & 0xFFFFFF) / 16777216f; }
        public float F(float a, float b) { return a + (b - a) * F(); }
        public bool Chance(float p) { return F() < p; }
        public T Pick<T>(T[] arr) { return arr[Int(arr.Length)]; }
        public float Sym(float a) { return (F() * 2 - 1) * a; }
    }

    public static class Noise
    {
        public static uint Hash(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
        /// <summary>Hash in [0,1).</summary>
        public static float H(int x, int y, int seed) { return (Hash(x, y, seed) & 0xFFFFFF) / 16777216f; }

        /// <summary>Smooth value noise in [0,1].</summary>
        public static float Value(float x, float y, int seed)
        {
            int xi = M.Floor(x), yi = M.Floor(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = H(xi, yi, seed), b = H(xi + 1, yi, seed), c = H(xi, yi + 1, seed), d = H(xi + 1, yi + 1, seed);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        public static float Fbm(float x, float y, int seed, int oct)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            for (int i = 0; i < oct; i++)
            {
                sum += Value(x, y, seed + i * 131) * amp; norm += amp;
                x *= 2.03f; y *= 2.03f; amp *= 0.5f;
            }
            return sum / norm;
        }

        /// <summary>Value noise that wraps every 'period' cells – used for scrolling fog.</summary>
        public static float Tile(float x, float y, int period, int seed)
        {
            int xi = M.Floor(x), yi = M.Floor(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int x0 = ((xi % period) + period) % period, y0 = ((yi % period) + period) % period;
            int x1 = (x0 + 1) % period, y1 = (y0 + 1) % period;
            float a = H(x0, y0, seed), b = H(x1, y0, seed), c = H(x0, y1, seed), d = H(x1, y1, seed);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }
    }

    public static class Log
    {
        static readonly object gate = new object();
        static StreamWriter file;
        public static string Dir;

        public static void Init(string dir, string name)
        {
            try
            {
                Dir = dir;
                Directory.CreateDirectory(dir);
                file = new StreamWriter(Path.Combine(dir, name), false, Encoding.UTF8);
                file.AutoFlush = true;
            }
            catch { file = null; }
        }

        public static void Line(string s)
        {
            lock (gate)
            {
                try { if (file != null) file.WriteLine(s); } catch { }
                try { Console.WriteLine(s); } catch { }
            }
        }
        public static void Line(string fmt, params object[] a) { Line(string.Format(System.Globalization.CultureInfo.InvariantCulture, fmt, a)); }
        public static void Close() { lock (gate) { try { if (file != null) file.Dispose(); } catch { } file = null; } }
    }
    public static class Lists
    {
        /// <summary>Removes an item found at index i while walking a list backwards. Something the item set off may have
        /// cleared or reshuffled the list in the meantime (a boss dying clears every projectile), so it checks first.</summary>
        public static void RemoveAt<T>(List<T> list, int i, T item) where T : class
        {
            if (i < list.Count && ReferenceEquals(list[i], item)) list.RemoveAt(i);
            else list.Remove(item);
        }
    }
}
