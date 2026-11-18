using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Vesperfell
{
    /// <summary>
    /// The smooth surfaces of the 3D world, painted procedurally at start-up. Every material is a height field with colour
    /// and gloss at eight texels to the world unit; its normal map is taken from that height. Model surfaces repeat every
    /// 32 units (where the old 32-texel art did), ground surfaces every 64. Every layer wraps seamlessly.
    /// </summary>
    internal static class MatGen
    {
        public const int MS = 256, GS = 512;
        public const int GGrass = 0, GForest = 1, GPale = 2, GAsh = 3, GThicket = 4, GDirt = 5, GMud = 6, GCobble = 7, GStone = 8,
            GPlank = 9, GDFloor = 10, GCarpet = 11, GBed = 12, GCount = 13;

        /// <summary>Colour + height (alpha) and normal + gloss (alpha) per layer, 0xAARRGGBB.</summary>
        public static int[][] ModelAlb, ModelNrm, GroundAlb, GroundNrm;
        /// <summary>Brightness factor per model layer, so each averages what the old art did (the vertex colours were tuned to it).</summary>
        public static float[] ModelK;
        /// <summary>Average colour of each ground layer as painted (r, g, b per layer), so each map can tint it to its palette.</summary>
        public static readonly float[] GroundMean = new float[GCount * 3];
        public static long BuildMs;
        static readonly object gate = new object();

        // working canvas
        struct C3
        {
            public float R, G, B;
            public C3(float r, float g, float b) { R = r; G = g; B = b; }
            public static C3 X(int hex) { return new C3(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f); }
            public static C3 Grey(float v) { return new C3(v, v, v); }
            public static C3 operator *(C3 a, float k) { return new C3(a.R * k, a.G * k, a.B * k); }
            public static C3 operator +(C3 a, C3 b) { return new C3(a.R + b.R, a.G + b.G, a.B + b.B); }
            public static C3 Mix(C3 a, C3 b, float t) { return new C3(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t); }
        }

        sealed class Buf
        {
            public readonly int N;
            public readonly float[] H, R, G, B, S;
            public Buf(int n) { N = n; int k = n * n; H = new float[k]; R = new float[k]; G = new float[k]; B = new float[k]; S = new float[k]; }
        }

        delegate void Painter(float u, float v, out float h, out C3 c, out float s);

        static void Fill(Buf b, Painter p)
        {
            int n = b.N;
            Parallel.For(0, n, delegate(int y)
            {
                float v = (y + 0.5f) / n;
                for (int x = 0; x < n; x++)
                {
                    float h, s; C3 c;
                    p((x + 0.5f) / n, v, out h, out c, out s);
                    int i = y * n + x;
                    b.H[i] = h; b.R[i] = c.R; b.G[i] = c.G; b.B[i] = c.B; b.S[i] = s;
                }
            });
        }

        const int Blade = 0, LeafShape = 1, Rod = 2;

        /// <summary>
        /// A soft stroke from (x, y) along angle a, len long, half-width w (texels): a blade tapers to its tip, a leaf is
        /// pointed at both ends, a rod is even. It rises from h0 to h1 and is drawn only where it stands above what is there.
        /// </summary>
        static void Stroke(Buf b, float x, float y, float a, float len, float w, int shape, float h0, float h1, C3 c0, C3 c1, float gloss)
        {
            int n = b.N;
            float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a);
            float ex = x + dx * len, ey = y + dy * len, inv = 1f / Math.Max(0.001f, len);
            int xa = M.Floor(Math.Min(x, ex) - w - 1), xb = M.Ceil(Math.Max(x, ex) + w + 1);
            int ya = M.Floor(Math.Min(y, ey) - w - 1), yb = M.Ceil(Math.Max(y, ey) + w + 1);
            for (int py = ya; py <= yb; py++)
            {
                int row = Wrap(py, n) * n;
                for (int px = xa; px <= xb; px++)
                {
                    float qx = px + 0.5f - x, qy = py + 0.5f - y;
                    float t = (qx * dx + qy * dy) * inv;
                    if (t < 0) t = 0; else if (t > 1) t = 1;
                    float rx = qx - dx * len * t, ry = qy - dy * len * t;
                    float d = (float)Math.Sqrt(rx * rx + ry * ry);
                    float wt = shape == Blade ? w * (1 - 0.8f * t) : (shape == LeafShape ? w * (float)Math.Sqrt(Math.Max(0, Math.Sin(Math.PI * t))) : w);
                    float cov = wt + 0.5f - d;
                    if (cov <= 0) continue;
                    if (cov > 1) cov = 1;
                    float hh = h0 + (h1 - h0) * t;
                    if (shape != Blade)
                    {
                        float q = d / Math.Max(0.3f, wt);
                        hh *= 0.55f + 0.45f * (float)Math.Sqrt(Math.Max(0, 1 - q * q));
                        if (shape == LeafShape && d < 0.6f) hh -= 0.05f;      // the midrib
                    }
                    int i = row + Wrap(px, n);
                    if (hh <= b.H[i]) continue;
                    b.H[i] += (hh - b.H[i]) * cov;
                    b.R[i] += (c0.R + (c1.R - c0.R) * t - b.R[i]) * cov;
                    b.G[i] += (c0.G + (c1.G - c0.G) * t - b.G[i]) * cov;
                    b.B[i] += (c0.B + (c1.B - c0.B) * t - b.B[i]) * cov;
                    b.S[i] += (gloss - b.S[i]) * cov;
                }
            }
        }

        /// <summary>Colour with height in alpha, and the normal (taken from the height; depth = texels per unit of height) with gloss in alpha.</summary>
        static void Pack(Buf b, float depth, int[] alb, int[] nrm)
        {
            int n = b.N;
            Parallel.For(0, n, delegate(int y)
            {
                int ym = Wrap(y - 1, n) * n, yp = Wrap(y + 1, n) * n, yr = y * n;
                for (int x = 0; x < n; x++)
                {
                    int xm = Wrap(x - 1, n), xp = Wrap(x + 1, n);
                    float sx = (b.H[yr + xp] - b.H[yr + xm]) * 0.5f * depth;
                    float sy = (b.H[yp + x] - b.H[ym + x]) * 0.5f * depth;
                    float l = 1f / (float)Math.Sqrt(sx * sx + sy * sy + 1);
                    int i = yr + x;
                    nrm[i] = (B8(b.S[i]) << 24) | (B8(-sx * l * 0.5f + 0.5f) << 16) | (B8(-sy * l * 0.5f + 0.5f) << 8) | B8(l * 0.5f + 0.5f);
                    alb[i] = (B8(b.H[i]) << 24) | (B8(b.R[i]) << 16) | (B8(b.G[i]) << 8) | B8(b.B[i]);
                }
            });
        }

        static int B8(float v) { int c = (int)(v * 255 + 0.5f); return c < 0 ? 0 : (c > 255 ? 255 : c); }
        static float C01(float v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }

        // noise that wraps around the layer
        static float Hf(int x, int y, int seed) { return (Noise.Hash(x, y, seed) & 0xFFFFFF) / 16777216f; }
        static int Wrap(int i, int p) { i %= p; return i < 0 ? i + p : i; }
        static float WrapF(float x, float p) { return x - p * (float)Math.Floor(x / p); }
        /// <summary>Signed distance from x to the nearest multiple of p.</summary>
        static float Near(float x, float p) { return x - p * (float)Math.Round(x / p); }
        static float Sm(float a, float b, float x) { float t = (x - a) / (b - a); t = t < 0 ? 0 : (t > 1 ? 1 : t); return t * t * (3 - 2 * t); }
        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        static float R(Random r) { return (float)r.NextDouble(); }

        static float VN(float x, float y, int px, int py, int seed)
        {
            int xi = M.Floor(x), yi = M.Floor(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int x0 = Wrap(xi, px), y0 = Wrap(yi, py), x1 = x0 + 1 >= px ? 0 : x0 + 1, y1 = y0 + 1 >= py ? 0 : y0 + 1;
            float a = Hf(x0, y0, seed), b = Hf(x1, y0, seed), c = Hf(x0, y1, seed), d = Hf(x1, y1, seed);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        /// <summary>Fractal noise over the unit square, wrapping; fx by fy cells across it in the first octave.</summary>
        static float Fbm(float u, float v, int fx, int fy, int oct, int seed)
        {
            float s = 0, a = 0.5f, n = 0;
            for (int i = 0; i < oct; i++)
            {
                s += VN(u * fx, v * fy, fx, fy, seed + i * 131) * a; n += a;
                fx *= 2; fy *= 2; a *= 0.5f;
            }
            return s / n;
        }

        /// <summary>Wrapping Worley cells: nx by ny jittered points over the unit square. Distances are in cell units.</summary>
        static void Cells(float u, float v, int nx, int ny, float jit, int seed, out float f1, out float f2, out int id, out float ox, out float oy)
        {
            float x = u * nx, y = v * ny;
            int xi = M.Floor(x), yi = M.Floor(y);
            f1 = 9; f2 = 9; id = 0; ox = 0; oy = 0;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = xi + i, cy = yi + j, wx = Wrap(cx, nx), wy = Wrap(cy, ny);
                    float px = cx + 0.5f + (Hf(wx, wy, seed) - 0.5f) * jit, py = cy + 0.5f + (Hf(wx, wy, seed + 1) - 0.5f) * jit;
                    float dx = x - px, dy = y - py, d = dx * dx + dy * dy;
                    if (d < f1) { f2 = f1; f1 = d; id = wy * nx + wx; ox = dx; oy = dy; }
                    else if (d < f2) f2 = d;
                }
            f1 = (float)Math.Sqrt(f1); f2 = (float)Math.Sqrt(f2);
        }

        static float RoundRect(float lx, float ly, float w, float h, float rc)
        {
            float ex = Math.Min(lx, w - lx), ey = Math.Min(ly, h - ly);
            if (ex < rc && ey < rc) return rc - M.Len(rc - ex, rc - ey);
            return Math.Min(ex, ey);
        }

        static float SegD(float x, float y, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay, wx = x - ax, wy = y - ay;
            float t = C01((wx * vx + wy * vy) / (vx * vx + vy * vy));
            return M.Len(wx - vx * t, wy - vy * t);
        }

        // build
        /// <summary>Paints every layer once; later calls (and the renderer) reuse the result.</summary>
        public static void Build()
        {
            lock (gate)
            {
                if (ModelAlb != null) return;
                Stopwatch sw = Stopwatch.StartNew();
                int mc = Mat3.Count;
                int[][] ma = new int[mc][], mn = new int[mc][], ga = new int[GCount][], gn = new int[GCount][];
                float[] mk = new float[mc];
                Parallel.For(0, GCount + mc, delegate(int job)
                {
                    if (job < GCount)
                    {
                        Buf b = new Buf(GS);
                        float d = PaintGround(job, b);
                        double r = 0, g = 0, bl = 0;
                        for (int i = 0; i < b.H.Length; i++) { r += C01(b.R[i]); g += C01(b.G[i]); bl += C01(b.B[i]); }
                        GroundMean[job * 3] = (float)(r / b.H.Length); GroundMean[job * 3 + 1] = (float)(g / b.H.Length); GroundMean[job * 3 + 2] = (float)(bl / b.H.Length);
                        ga[job] = new int[GS * GS]; gn[job] = new int[GS * GS];
                        Pack(b, d * 8, ga[job], gn[job]);
                    }
                    else
                    {
                        int id = job - GCount;
                        Buf b = new Buf(MS);
                        float d = PaintModel(id, b);
                        double l = 0;
                        for (int i = 0; i < b.H.Length; i++) l += C01(b.R[i]) * 0.3f + C01(b.G[i]) * 0.59f + C01(b.B[i]) * 0.11f;
                        float lum = (float)(l / b.H.Length);
                        mk[id] = id == 0 ? 1f : M.Clamp(Mat3.OldLum(id) / Math.Max(0.05f, lum), 0.5f, 2.5f);
                        ma[id] = new int[MS * MS]; mn[id] = new int[MS * MS];
                        Pack(b, d * 8, ma[id], mn[id]);
                    }
                });
                ModelK = mk; ModelNrm = mn; GroundAlb = ga; GroundNrm = gn;
                ModelAlb = ma;
                BuildMs = sw.ElapsedMilliseconds;
                Log.Line("Materials painted in " + BuildMs + " ms");
            }
        }

        /// <summary>Starts painting on a worker thread, so it overlaps the rest of start-up.</summary>
        public static void Prewarm()
        {
            Task.Factory.StartNew(delegate { try { Build(); } catch (Exception e) { Log.Line("Material prewarm failed: " + e.Message); } });
        }

        // model surfaces (32 world units across)
        static float PaintModel(int id, Buf b)
        {
            Random r = new Random(1000 + id * 7919);
            switch (id)
            {
                case Mat3.Plank: Fill(b, MPlank); return 0.6f;
                case Mat3.Bark: Fill(b, MBark); return 1.2f;
                case Mat3.Brick: Fill(b, MBrick); return 0.7f;
                case Mat3.Rock: Fill(b, MRock); return 1.4f;
                case Mat3.Slate: Fill(b, MSlate); return 0.6f;
                case Mat3.Tile: Fill(b, MTile); return 1.3f;
                case Mat3.Plaster: Fill(b, MPlaster); return 0.35f;
                case Mat3.Thatch: Fill(b, MThatch); return 0.9f;
                case Mat3.Leaf:
                    Fill(b, MShade);
                    for (int i = 0; i < 1700; i++)
                    {
                        float len = (1.6f + R(r) * 1.2f) * 8, hh = 0.35f + R(r) * 0.65f, t = 0.58f + R(r) * 0.42f;
                        Stroke(b, R(r) * MS, R(r) * MS, R(r) * M.TAU, len, len * (0.3f + R(r) * 0.08f), LeafShape, hh, hh, C3.Grey(t), C3.Grey(t * 0.9f), 0.26f);
                    }
                    return 0.7f;
                case Mat3.Cloth: Fill(b, MCloth); return 1.0f;
                case Mat3.Metal: Fill(b, MMetal); return 0.35f;
                case Mat3.Cobble: Fill(b, MCobble); return 1.4f;
                case Mat3.Moss: Fill(b, MMoss); return 0.8f;
                case Mat3.DBrick: Fill(b, MDBrick); return 1.0f;
                case Mat3.Books: Fill(b, Books()); return 1.0f;
                case Mat3.Carpet: Fill(b, MCarpet); return 0.25f;
                case Mat3.Timber: Fill(b, MTimber); return 0.6f;
                case Mat3.Straw:
                    Fill(b, MStrawBed);
                    for (int i = 0; i < 2600; i++)
                    {
                        float a = (R(r) - 0.5f) * 1.2f + (R(r) < 0.5f ? 0 : M.PI), hh = 0.3f + R(r) * 0.7f, t = 0.6f + R(r) * 0.4f;
                        Stroke(b, R(r) * MS, R(r) * MS, a, (2 + R(r) * 4) * 8, 1 + R(r) * 0.8f, Rod, hh, hh, C3.Grey(t), C3.Grey(t * 0.92f), 0.08f);
                    }
                    return 0.5f;
                case Mat3.Bone: Fill(b, MBone); return 0.4f;
                case Mat3.Rune: Fill(b, MRune); return 1.0f;
                case Mat3.Needle:
                    Fill(b, MShade);
                    for (int i = 0; i < 5200; i++)
                    {
                        float hh = 0.3f + R(r) * 0.7f, t = 0.55f + R(r) * 0.4f;
                        Stroke(b, R(r) * MS, R(r) * MS, M.PI / 2 + (R(r) - 0.5f) * 1.2f, (1.2f + R(r) * 1.2f) * 8, 0.75f, Rod, hh, hh, C3.Grey(t), C3.Grey(t * 0.85f), 0.12f);
                    }
                    return 0.45f;
                case Mat3.Fur: Fill(b, MFur); return 0.7f;
                default: Fill(b, MFlat); return 0;
            }
        }

        static void MFlat(float u, float v, out float h, out C3 c, out float s) { h = 0.5f; c = C3.Grey(1); s = 0.08f; }
        static void MShade(float u, float v, out float h, out C3 c, out float s) { float n = Fbm(u, v, 8, 8, 3, 911); h = 0; c = C3.Grey(0.2f + 0.1f * n); s = 0.04f; }
        static void MStrawBed(float u, float v, out float h, out C3 c, out float s) { float n = Fbm(u, v, 16, 16, 3, 912); h = 0.05f * n; c = C3.Grey(0.36f + 0.1f * n); s = 0.03f; }

        static void MPlank(float u, float v, out float h, out C3 c, out float s)
        {
            // boards 8 units wide running along u, one butt joint each, nailed either side of it
            float X = u * 32, Y = v * 32;
            int row = Math.Min(3, (int)(Y / 8)); float ly = Y - row * 8;
            float dj = Near(X - Hf(row, 0, 701) * 32, 32);
            float e = Math.Min(Math.Min(ly, 8 - ly), Math.Abs(dj));
            float body = Sm(0.1f, 0.55f, e);
            float warp = Fbm(u, v, 2, 4, 2, 710 + row) * 7;
            float grain = 0.5f + 0.5f * M.Sin((Y * 2.3f + warp) * M.PI);
            float fib = Fbm(u, v, 4, 96, 2, 720);
            float tone = (Hf(row, 1, 702) - 0.5f) * 0.16f;
            float kx = Near(X - Hf(row, 2, 703) * 32, 32), ky = Y - (row * 8 + 2.5f + Hf(row, 3, 704) * 3);
            float knot = Math.Max(0, 1 - M.Len(kx * 0.6f, ky) / 1.5f);
            float nail = 9;
            for (int k = 0; k < 4; k++) nail = Math.Min(nail, M.Len(dj - ((k & 1) == 0 ? 1.1f : -1.1f), ly - (k < 2 ? 2.2f : 5.8f)));
            h = body * (0.8f + 0.1f * fib + 0.08f * grain) - knot * 0.1f;
            c = C3.Grey(Lerp(0.3f, 0.8f + 0.08f * grain + 0.1f * (fib - 0.5f) + tone - knot * knot * 0.3f, Sm(0.05f, 0.35f, e)));
            s = 0.14f + 0.08f * fib;
            if (nail < 0.32f) { float k = Sm(0.32f, 0.2f, nail); h += 0.08f * k; c = C3.Mix(c, C3.Grey(0.3f), k); s = Lerp(s, 0.5f, k); }
        }

        static void MBark(float u, float v, out float h, out C3 c, out float s)
        {
            // long rounded plates between furrows that run down the trunk (v), each plate broken across here and there
            float x = u * 11 + (Fbm(u, v, 2, 2, 3, 731) - 0.5f) * 1.3f + (Fbm(u, v, 11, 1, 1, 739) - 0.5f) * 0.8f;
            float px = x - (float)Math.Floor(x);
            int ci = Wrap(M.Floor(x), 11), nb = 2 + ci % 2;
            float y = v * nb + Hf(ci, 0, 736);
            float fine = Fbm(u, v, 40, 6, 3, 734);
            float fur = Sm(0.03f, 0.22f, Math.Min(px, 1 - px) + (Fbm(u, v, 24, 6, 2, 732) - 0.5f) * 0.1f);
            float brk = Sm(0f, 0.06f, Math.Abs(Near(y + (px - 0.5f) * (Hf(ci, 1, 740) - 0.5f) * 1.2f, 1)));
            float plate = fur * Math.Max(brk, 0.35f);
            float ridge = (float)Math.Sqrt(Math.Max(0f, M.Sin(px * M.PI)));
            float lich = Sm(0.6f, 0.7f, Fbm(u, v, 6, 6, 3, 735)) * plate;
            float tone = (Hf(ci, Wrap(M.Floor(y), nb), 737) - 0.5f) * 0.12f;
            h = 0.05f + 0.65f * plate * (0.6f + 0.4f * ridge) + 0.18f * fine * plate + 0.05f * fine;
            c = C3.Grey(0.36f + 0.46f * plate + 0.16f * (fine - 0.5f) + tone * plate + lich * 0.18f);
            s = 0.05f;
        }

        static void MBrick(float u, float v, out float h, out C3 c, out float s)
        {
            // running bond, 16 by 8, worn arrises
            float X = u * 32, Y = v * 32;
            int row = Math.Min(3, (int)(Y / 8)); float ly = Y - row * 8;
            float x = X + (row & 1) * 8; int col = (int)(x / 16); float lx = x - col * 16;
            float e = Math.Min(Math.Min(lx, 16 - lx), Math.Min(ly, 8 - ly)) + (Fbm(u, v, 16, 16, 2, 741) - 0.5f) * 0.5f;
            float face = Sm(0.3f, 1.0f, e);
            float rough = Fbm(u, v, 16, 16, 3, 742), stain = Fbm(u, v, 3, 3, 3, 744);
            float tone = (Hf(col & 1, row, 743) - 0.5f) * 0.2f;
            h = face * (0.78f + 0.22f * rough);
            float a = 0.82f + tone + (rough - 0.5f) * 0.14f - Sm(0.55f, 0.75f, stain) * 0.12f;
            c = C3.Grey(Lerp(0.44f + (rough - 0.5f) * 0.1f, a, Sm(0.15f, 0.5f, e)));
            s = Lerp(0.04f, 0.12f, face);
        }

        static void MDBrick(float u, float v, out float h, out C3 c, out float s)
        {
            // old ashlar: courses 8 high, each laid in two or three blocks of its own lengths, damp and pitted
            float X = u * 32, Y = v * 32;
            int row = Math.Min(3, (int)(Y / 8)); float ly = Y - row * 8;
            int nb = Hf(row, 0, 751) < 0.5f ? 2 : 3;
            float x = WrapF(X - Hf(row, 1, 752) * 32, 32);
            float prev = 0, next = 32; int k = 0;
            for (int i = 1; i < nb; i++)
            {
                float bnd = i * 32f / nb + (Hf(row, 10 + i, 753) - 0.5f) * 5;
                if (bnd <= x) { prev = bnd; k = i; } else { next = bnd; break; }
            }
            float e = Math.Min(Math.Min(x - prev, next - x), Math.Min(ly, 8 - ly)) + (Fbm(u, v, 12, 12, 3, 754) - 0.5f) * 0.9f;
            float face = Sm(0.35f, 1.3f, e);
            float rough = Fbm(u, v, 12, 12, 4, 755), pit = Fbm(u, v, 48, 48, 2, 756);
            float damp = Fbm(u, v, 8, 1, 3, 757);
            float tone = (Hf(row * 4 + k, 0, 758) - 0.5f) * 0.24f;
            h = face * (0.62f + 0.3f * rough + 0.08f * pit);
            float a = 0.78f + tone + (rough - 0.5f) * 0.18f + (pit - 0.5f) * 0.08f - Sm(0.55f, 0.7f, damp) * 0.12f;
            c = C3.Grey(Lerp(0.3f, a, Sm(0.2f, 0.6f, e)));
            s = Lerp(0.06f, 0.2f + Sm(0.5f, 0.7f, damp) * 0.15f, face);
        }

        static void MRock(float u, float v, out float h, out C3 c, out float s)
        {
            // weathered stone: broad lumps, faint bedding, and fractured faces with cracks between them
            float n = Fbm(u, v, 4, 4, 5, 761), fine = Fbm(u, v, 32, 32, 2, 763);
            float wu = (Fbm(u, v, 4, 4, 2, 764) - 0.5f) * 0.05f, wv = (Fbm(u, v, 4, 4, 2, 765) - 0.5f) * 0.05f;
            float f1, f2, ox, oy; int id;
            Cells(u + wu, v + wv, 4, 4, 0.85f, 762, out f1, out f2, out id, out ox, out oy);
            float cmask = Sm(0.42f, 0.56f, Fbm(u, v, 3, 3, 3, 769));       // only some of the joints have opened
            float crack = 1 - (1 - Sm(0f, 0.05f + 0.04f * fine, f2 - f1)) * cmask;
            float face = (Hf(id, 0, 766) - 0.5f) * cmask;
            float tilt = (ox * (Hf(id, 1, 767) - 0.5f) + oy * (Hf(id, 2, 768) - 0.5f)) * 0.45f * cmask;
            float bed = M.Sin((v * 7 + n * 1.5f) * M.TAU) * 0.5f + 0.5f;
            h = (0.3f + 0.45f * n + 0.1f * fine + 0.08f * bed + tilt) * (0.5f + 0.5f * crack);
            c = C3.Grey(0.64f + 0.3f * (n - 0.5f) + 0.12f * (fine - 0.5f) + face * 0.12f + (bed - 0.5f) * 0.06f - (1 - crack) * 0.25f);
            s = 0.1f + 0.08f * fine;
        }

        static void MSlate(float u, float v, out float h, out C3 c, out float s)
        {
            // courses 6.4 deep (v runs down the roof), slates 8 wide; each thickens to its butt, which shades the course below
            float X = u * 32, Y = v * 32;
            int row = Math.Min(4, (int)(Y / 6.4f)); float ly = Y - row * 6.4f, t = ly / 6.4f;
            float x = X + Hf(row, 0, 771) * 8; int col = (int)(x / 8); float lx = x - col * 8; col &= 3;
            float body = Sm(0.08f, 0.3f, Math.Min(lx, 8 - lx)) * Sm(0f, 0.3f, 6.4f - ly);
            float rough = Fbm(u, v, 12, 12, 3, 773);
            float tilt = (Hf(col, row, 772) - 0.5f) * 0.03f * (lx - 4);
            float tone = (Hf(col, row, 774) - 0.5f) * 0.16f;
            float lich = Sm(0.66f, 0.72f, Fbm(u, v, 5, 5, 3, 775));
            h = 0.12f + body * (0.3f + 0.45f * t + tilt + 0.1f * rough);
            c = C3.Grey(Lerp(0.38f, 0.86f - t * 0.12f + tone + (rough - 0.5f) * 0.08f + lich * 0.12f, body));
            s = Lerp(0.04f, 0.3f - lich * 0.2f, body);
        }

        static void MTile(float u, float v, out float h, out C3 c, out float s)
        {
            // barrel tiles, courses of 8
            float X = u * 32, Y = v * 32;
            int row = Math.Min(3, (int)(Y / 8)); float ly = Y - row * 8, t = ly / 8;
            float x = X + (row & 1) * 4; int col = (int)(x / 8); float lx = x - col * 8; col &= 3;
            float bar = (float)Math.Pow(Math.Max(0, Math.Sin(Math.PI * lx / 8)), 0.6);
            float lip = Sm(0f, 0.35f, 8 - ly);
            float rough = Fbm(u, v, 16, 16, 2, 781);
            float tone = (Hf(col, row, 782) - 0.5f) * 0.18f;
            h = (0.12f + 0.62f * bar + 0.22f * t + 0.04f * rough) * lip;
            c = C3.Grey((0.66f + 0.22f * bar + tone + (rough - 0.5f) * 0.08f) * (0.55f + 0.45f * lip));
            s = 0.18f + 0.1f * bar;
        }

        static void MPlaster(float u, float v, out float h, out C3 c, out float s)
        {
            // lime plaster: soft trowel waves, hairline cracks that come and go, streaks under the stains, patches fallen away
            float n = Fbm(u, v, 3, 3, 4, 791), f = Fbm(u, v, 24, 24, 2, 792);
            float mask = Sm(0.52f, 0.62f, Fbm(u, v, 3, 3, 3, 795));
            float crack = 1 - (1 - Sm(0f, 0.01f, Math.Abs(Fbm(u, v, 2, 2, 4, 793) - 0.5f))) * mask;
            float spall = Sm(0.73f, 0.75f, Fbm(u, v, 4, 4, 4, 796));
            float stain = Sm(0.55f, 0.8f, Fbm(u, v, 2, 3, 3, 794));
            float drip = Sm(0.6f, 0.9f, Fbm(u, v, 12, 2, 3, 797)) * stain;
            h = 0.55f + 0.25f * (n - 0.5f) + 0.1f * (f - 0.5f) - (1 - crack) * 0.2f - spall * (0.2f - 0.12f * f);
            c = C3.Grey(0.9f + 0.06f * (n - 0.5f) + 0.05f * (f - 0.5f) - stain * 0.1f - drip * 0.08f - (1 - crack) * 0.22f - spall * (0.14f + 0.1f * f));
            s = 0.05f;
        }

        static void MThatch(float u, float v, out float h, out C3 c, out float s)
        {
            // straw laid down the roof in courses of 8, ragged where each course ends
            float Y = v * 32;
            int row = Math.Min(3, (int)(Y / 8)); float ly = Y - row * 8, t = ly / 8;
            float wv = Fbm(u, v, 4, 2, 2, 801 + row) * 0.04f;
            float strand = Fbm(u + wv, v, 64, 4, 3, 805), fine = Fbm(u + wv, v, 160, 8, 1, 806);
            float cut = Sm(0f, 0.45f, 7.1f + (Fbm(u, v, 48, 1, 2, 807 + row) - 0.5f) * 2f - ly);
            h = (0.22f + 0.45f * strand + 0.13f * fine + 0.2f * t) * (0.25f + 0.75f * cut);
            c = C3.Grey((0.6f + 0.3f * strand + 0.12f * fine) * (0.55f + 0.45f * cut) * (0.86f + 0.14f * t));
            s = 0.05f;
        }

        static void MCloth(float u, float v, out float h, out C3 c, out float s)
        {
            // plain weave, a thread every half unit, over loose folds
            float tx = u * 64, ty = v * 64;
            int ix = M.Floor(tx), iy = M.Floor(ty);
            float fx = tx - ix, fy = ty - iy;
            float pw = M.Sin(M.PI * fx), pf = M.Sin(M.PI * fy);
            float weave = ((ix + iy) & 1) == 0 ? pw * (0.6f + 0.4f * pf) : pf * (0.6f + 0.4f * pw);
            float fold = Fbm(u, v, 2, 3, 4, 811), slub = Fbm(u, v, 64, 2, 1, 812), nap = Fbm(u, v, 48, 48, 2, 813);
            h = 0.045f * weave + 0.9f * fold + 0.055f * nap;
            c = C3.Grey(0.8f + 0.035f * weave + 0.05f * (slub - 0.5f) + 0.06f * (nap - 0.5f) + 0.12f * (fold - 0.5f));
            s = 0.04f;
        }

        static void MMetal(float u, float v, out float h, out C3 c, out float s)
        {
            float br = Fbm(u, v, 2, 96, 3, 821), dent = Fbm(u, v, 3, 3, 4, 822), pit = Fbm(u, v, 48, 48, 2, 824);
            float grime = Sm(0.56f, 0.72f, Fbm(u, v, 4, 4, 4, 823));
            h = 0.5f + 0.35f * (dent - 0.5f) + 0.06f * (br - 0.5f) - grime * 0.06f * pit;
            c = C3.Grey(0.8f + 0.14f * (br - 0.5f) - grime * (0.16f + 0.1f * pit));
            s = 0.8f - grime * 0.45f + 0.08f * (br - 0.5f);
        }

        static void MCobble(float u, float v, out float h, out C3 c, out float s)
        {
            float f1, f2, ox, oy; int id;
            Cells(u, v, 5, 5, 0.7f, 831, out f1, out f2, out id, out ox, out oy);
            float e = f2 - f1, stone = Sm(0.04f, 0.4f, e), rough = Fbm(u, v, 24, 24, 2, 832);
            h = 0.1f + 0.8f * stone + 0.1f * rough;
            c = C3.Grey(Lerp(0.4f, 0.82f + (Hf(id, 0, 833) - 0.5f) * 0.22f + (rough - 0.5f) * 0.1f, Sm(0.02f, 0.1f, e)));
            s = Lerp(0.05f, 0.28f, stone);
        }

        static void MMoss(float u, float v, out float h, out C3 c, out float s)
        {
            float cl = Fbm(u, v, 8, 8, 4, 841), fib = Fbm(u, v, 96, 96, 2, 842);
            h = Sm(0.3f, 0.7f, cl) * 0.7f + fib * 0.3f;
            c = C3.Grey(0.64f + 0.3f * (cl - 0.5f) + 0.16f * (fib - 0.5f));
            s = 0.06f;
        }

        static readonly C3[] BookCols = { C3.X(0x6A1E1E), C3.X(0x1E3A2A), C3.X(0x1E2A4A), C3.X(0x5A3A1E), C3.X(0x6A5A2A), C3.X(0x3A1E3A), C3.X(0x2A2A2A) };

        static Painter Books()
        {
            // two shelves per repeat, each packed with books of their own widths, heights and bindings
            Random r = new Random(77);
            float[][] edges = new float[2][], tops = new float[2][]; int[][] cols = new int[2][];
            for (int sh = 0; sh < 2; sh++)
            {
                List<float> e = new List<float>(), tp = new List<float>(); List<int> cl = new List<int>();
                float x = 0;
                while (x < 31.9f)
                {
                    float w = 1.5f + R(r) * 1.9f;
                    if (32 - x - w < 1.3f) w = 32 - x;
                    e.Add(x); cl.Add(r.Next(BookCols.Length)); tp.Add(1.2f + R(r) * 4.5f);
                    x += w;
                }
                e.Add(32);
                edges[sh] = e.ToArray(); tops[sh] = tp.ToArray(); cols[sh] = cl.ToArray();
            }
            return delegate(float u, float v, out float h, out C3 c, out float s)
            {
                float X = u * 32, Y = v * 32;
                int sh = Math.Min(1, (int)(Y / 16)); float ly = Y - sh * 16;
                if (ly > 14.8f)
                {
                    float g = Fbm(u, v, 4, 64, 2, 851);
                    h = 1; c = C3.Grey(0.5f + 0.1f * g); s = 0.1f; return;
                }
                float[] ed = edges[sh];
                int k = 0;
                while (k < ed.Length - 2 && ed[k + 1] <= X) k++;
                float w = ed[k + 1] - ed[k], lx = X - ed[k], top = tops[sh][k];
                if (ly < top) { h = 0.02f; c = C3.Grey(0.12f); s = 0.02f; return; }
                float prof = (float)Math.Sqrt(Math.Max(0, Math.Sin(Math.PI * lx / w)));
                float d = Math.Min(Math.Abs(ly - (top + 1.6f)), Math.Min(Math.Abs(ly - (top + 2.6f)), Math.Abs(ly - 12.6f)));
                float wear = Fbm(u, v, 16, 16, 2, 852);
                h = 0.35f + 0.6f * prof;
                if (d < 0.22f) { c = C3.X(0xB08A3A) * (0.8f + 0.3f * prof); s = 0.55f; }
                else { c = BookCols[cols[sh][k]] * (0.75f + 0.35f * prof + 0.1f * (wear - 0.5f)); s = 0.16f; }
                if (ly < top + 0.35f) { c = C3.X(0xC8B890) * 0.8f; s = 0.05f; }
            };
        }

        static void MCarpet(float u, float v, out float h, out C3 c, out float s)
        {
            float X = u * 32, Y = v * 32;
            float dd = Math.Abs(WrapF(X, 8) - 4) + Math.Abs(WrapF(Y, 8) - 4);
            float motif = 1 - Sm(0.2f, 0.55f, Math.Abs(dd - 2.6f));
            float border = 1 - Sm(2.6f, 3.2f, X) + Sm(28.8f, 29.4f, X);
            float pile = Fbm(u, v, 128, 128, 1, 861);
            h = 0.5f + 0.3f * (pile - 0.5f) + 0.1f * motif * (1 - border);
            c = C3.Grey(Lerp(0.8f + 0.2f * motif, 0.62f, border) + (pile - 0.5f) * 0.08f);
            s = 0.03f;
        }

        static void MTimber(float u, float v, out float h, out C3 c, out float s)
        {
            float X = u * 32;
            float warp = Fbm(u, v, 4, 2, 2, 871) * 6;
            float grain = 0.5f + 0.5f * M.Sin((X * 1.6f + warp) * M.PI);
            float fib = Fbm(u, v, 96, 4, 2, 872);
            float check = Sm(0f, 0.02f, Math.Abs(Fbm(u, v, 6, 1, 3, 873) - 0.5f));
            h = 0.6f + 0.12f * grain + 0.1f * (fib - 0.5f) - (1 - check) * 0.4f;
            c = C3.Grey(0.7f + 0.1f * grain + 0.1f * (fib - 0.5f) - (1 - check) * 0.25f);
            s = 0.1f;
        }

        static void MBone(float u, float v, out float h, out C3 c, out float s)
        {
            float n = Fbm(u, v, 3, 3, 4, 881), f = Fbm(u, v, 32, 32, 2, 882);
            float f1, f2, ox, oy; int id;
            Cells(u, v, 24, 24, 0.9f, 883, out f1, out f2, out id, out ox, out oy);
            float pore = Hf(id, 1, 884) < 0.35f ? 1 - Sm(0.08f, 0.2f, f1) : 0;
            h = 0.5f + 0.2f * (n - 0.5f) + 0.06f * (f - 0.5f) - pore * 0.3f;
            c = C3.Grey(0.88f + 0.06f * (n - 0.5f) + 0.04f * (f - 0.5f) - pore * 0.3f);
            s = 0.32f - pore * 0.2f;
        }

        static void MRune(float u, float v, out float h, out C3 c, out float s)
        {
            float X = u * 32, Y = v * 32;
            float n = Fbm(u, v, 4, 4, 4, 891), fine = Fbm(u, v, 32, 32, 2, 892);
            float d = Math.Min(SegD(X, Y, 16, 5, 16, 27), SegD(X, Y, 9.5f, 11, 22.5f, 11));
            d = Math.Min(d, Math.Min(SegD(X, Y, 11, 15, 21, 25), SegD(X, Y, 21, 15, 11, 25)));
            float groove = 1 - Sm(0.35f, 0.85f, d);
            h = (0.45f + 0.4f * n + 0.15f * fine) * (1 - groove * 0.65f);
            c = C3.Grey(0.7f + 0.26f * (n - 0.5f) + 0.1f * (fine - 0.5f) - groove * 0.32f);
            s = 0.14f;
        }

        static void MFur(float u, float v, out float h, out C3 c, out float s)
        {
            float flow = Fbm(u, v, 3, 3, 2, 901) * 0.08f;
            float st = Fbm(u + flow, v, 64, 6, 3, 902), cl = Fbm(u, v, 6, 4, 3, 903);
            h = 0.3f + 0.5f * st + 0.2f * cl;
            c = C3.Grey(0.62f + 0.48f * (st - 0.5f) + 0.1f * (cl - 0.5f));
            s = 0.1f;
        }

        // ground (64 world units across, full colour)
        static float PaintGround(int id, Buf b)
        {
            Random r = new Random(5000 + id * 104729);
            switch (id)
            {
                case GGrass: return Grass(b, r, false);
                case GPale: return Grass(b, r, true);
                case GForest: return ForestFloor(b, r, false);
                case GThicket: return ForestFloor(b, r, true);
                case GAsh: return Ash(b, r);
                case GDirt: return Dirt(b, r);
                case GMud: return Mud(b, r);
                case GCobble: Fill(b, GCobblePx); return 0.8f;
                case GStone: Fill(b, GFlagPx); return 0.8f;
                case GPlank: Fill(b, GPlankPx); return 0.6f;
                case GDFloor: Fill(b, GDFloorPx); return 0.9f;
                case GCarpet: Fill(b, GCarpetPx); return 0.22f;
                default: return Bed(b, r);
            }
        }

        static float Grass(Buf b, Random r, bool pale)
        {
            int sd = pale ? 1210 : 1200;
            Fill(b, delegate(float u, float v, out float h, out C3 c, out float s)
            {
                float n = Fbm(u, v, 8, 8, 4, sd), f = Fbm(u, v, 48, 48, 2, sd + 1);
                float cov = Sm(0.36f, 0.62f, Fbm(u, v, 6, 6, 3, sd + 2));
                C3 soil = pale ? C3.Mix(C3.X(0x34332A), C3.X(0x48463A), n) : C3.Mix(C3.X(0x2A2318), C3.X(0x3A3022), n);
                C3 sward = pale ? C3.Mix(C3.X(0x3E4A3E), C3.X(0x56604C), f) : C3.Mix(C3.X(0x22361E), C3.X(0x2E4626), f);
                c = C3.Mix(soil, sward, 0.35f + 0.6f * cov);
                h = 0.06f + 0.1f * f + 0.12f * cov;
                s = 0.06f;
            });
            int count = pale ? 20000 : 30000;
            for (int i = 0; i < count; i++)
            {
                float x = R(r), y = R(r);
                float dens = Sm(0.2f, 0.55f, Fbm(x, y, 6, 6, 3, sd + 2));
                if (R(r) > dens + 0.1f) continue;
                float a = pale ? 0.6f + (R(r) - 0.5f) * 1.4f : R(r) * M.TAU;
                float len = (1.1f + R(r) * 1.9f) * 8 * (pale ? 1.35f : 1f);
                float t = R(r), hb = 0.25f + R(r) * 0.25f;
                C3 c0 = pale ? C3.Mix(C3.X(0x4A5444), C3.X(0x707A60), t) : C3.Mix(C3.X(0x23401E), C3.X(0x3C5C2A), t);
                C3 c1 = pale ? C3.Mix(C3.X(0x707A62), C3.X(0x9C9C7C), t) : C3.Mix(C3.X(0x3E5E2C), C3.X(0x6A7E3A), t);
                Stroke(b, x * GS, y * GS, a, len, 0.9f + R(r) * 0.6f, Blade, hb, hb + 0.3f + R(r) * 0.2f, c0, c1, 0.15f);
            }
            return 0.4f;
        }

        static float ForestFloor(Buf b, Random r, bool thicket)
        {
            int sd = thicket ? 1310 : 1300;
            Fill(b, delegate(float u, float v, out float h, out C3 c, out float s)
            {
                float n = Fbm(u, v, 8, 8, 4, sd), f = Fbm(u, v, 64, 64, 2, sd + 1);
                float moss = Sm(0.56f, 0.66f, Fbm(u, v, 5, 5, 4, sd + 2));
                C3 soil = thicket ? C3.Mix(C3.X(0x14130F), C3.X(0x201C16), n) : C3.Mix(C3.X(0x1C1812), C3.X(0x2C2418), n);
                c = C3.Mix(soil, C3.Mix(C3.X(0x1E3420), C3.X(0x2C4628), f), moss);
                h = 0.05f + 0.1f * f + moss * (0.22f + 0.2f * f);
                s = 0.08f;
            });
            C3[] litter = { C3.X(0x4A3424), C3.X(0x5E4228), C3.X(0x3A2A20), C3.X(0x6A4A2A), C3.X(0x4A2A22), C3.X(0x2E2C1E) };
            int leaves = thicket ? 7000 : 5200;
            for (int i = 0; i < leaves; i++)
            {
                float len = (0.7f + R(r) * 0.8f) * 8, hh = 0.25f + R(r) * 0.45f;
                C3 c = litter[r.Next(litter.Length)] * (0.8f + R(r) * 0.4f);
                if (thicket) c = C3.Mix(c, C3.X(0x1C2A1E), 0.55f);
                Stroke(b, R(r) * GS, R(r) * GS, R(r) * M.TAU, len, len * (0.28f + R(r) * 0.12f), LeafShape, hh, hh, c, c * 0.85f, 0.2f);
            }
            for (int i = 0; i < 2600; i++)
            {
                float hh = 0.45f + R(r) * 0.3f; C3 c = C3.X(0x5A3A22) * (0.7f + R(r) * 0.5f);
                Stroke(b, R(r) * GS, R(r) * GS, R(r) * M.TAU, (0.8f + R(r) * 0.8f) * 8, 0.6f, Rod, hh, hh, c, c, 0.1f);
            }
            for (int i = 0; i < (thicket ? 90 : 220); i++)
            {
                float hh = 0.8f + R(r) * 0.15f; C3 c = C3.X(0x2A2018) * (0.8f + R(r) * 0.4f);
                Stroke(b, R(r) * GS, R(r) * GS, R(r) * M.TAU, (2 + R(r) * 4) * 8, 0.6f + R(r) * 0.5f, Rod, hh, hh, c, c * 0.8f, 0.08f);
            }
            if (thicket)
                for (int i = 0; i < 50; i++)
                {
                    // roots snaking over the ground
                    float x = R(r) * GS, y = R(r) * GS, a = R(r) * M.TAU, w = 2f + R(r) * 1.5f;
                    C3 c = C3.X(0x3A2A1E) * (0.8f + R(r) * 0.4f);
                    for (int k = 0; k < 6; k++)
                    {
                        float len = (1.5f + R(r)) * 8;
                        Stroke(b, x, y, a, len, w, Rod, 0.92f, 0.9f, c, c * 0.9f, 0.1f);
                        x += M.Cos(a) * len; y += M.Sin(a) * len; a += (R(r) - 0.5f) * 0.7f; w *= 0.9f;
                    }
                }
            return thicket ? 0.9f : 0.6f;
        }

        static float Ash(Buf b, Random r)
        {
            Fill(b, delegate(float u, float v, out float h, out C3 c, out float s)
            {
                float n = Fbm(u, v, 6, 6, 4, 1400), f = Fbm(u, v, 64, 64, 2, 1401);
                c = C3.Mix(C3.X(0x34363A), C3.X(0x56585A), n * 0.7f + f * 0.3f);
                h = 0.2f + 0.25f * n + 0.08f * f;
                s = 0.04f;
            });
            for (int i = 0; i < 3000; i++)
            {
                float len = (0.3f + R(r) * 0.5f) * 8, hh = 0.45f + R(r) * 0.15f; C3 c = C3.Grey(0.1f + R(r) * 0.07f);
                Stroke(b, R(r) * GS, R(r) * GS, R(r) * M.TAU, len, len * 0.4f, LeafShape, hh, hh, c, c, 0.18f);
            }
            for (int i = 0; i < 420; i++)
            {
                float w = 1.2f + R(r) * 1.8f, hh = 0.7f + R(r) * 0.2f; C3 c = C3.Mix(C3.X(0x4A4A50), C3.X(0x6E6E72), R(r));
                Stroke(b, R(r) * GS, R(r) * GS, R(r) * M.TAU, w * (0.3f + R(r) * 0.8f), w, Rod, hh, hh, c, c * 0.9f, 0.14f);
            }
            for (int i = 0; i < 1500; i++)
            {
                float hb = 0.45f + R(r) * 0.1f; C3 c = C3.Mix(C3.X(0x56564A), C3.X(0x7A7A66), R(r));
                Stroke(b, R(r) * GS, R(r) * GS, R(r) * M.TAU, (1 + R(r) * 1.5f) * 8, 0.8f, Blade, hb, hb + 0.3f, c, c * 1.1f, 0.06f);
            }
            return 0.45f;
        }

        static readonly C3[] Pebble = { C3.X(0x5A5048), C3.X(0x6A6058), C3.X(0x4A4038), C3.X(0x58585A), C3.X(0x6E645A) };

        static float Dirt(Buf b, Random r)
        {
            Fill(b, delegate(float u, float v, out float h, out C3 c, out float s)
            {
                float n = Fbm(u, v, 6, 6, 4, 1500), f = Fbm(u, v, 48, 48, 3, 1501), rut = Fbm(u, v, 3, 8, 3, 1502);
                float crack = Sm(0f, 0.018f, Math.Abs(Fbm(u, v, 5, 5, 3, 1503) - 0.5f));
                c = C3.Mix(C3.X(0x3A2A20), C3.X(0x5A4434), n) * ((0.9f + 0.2f * f) * (0.72f + 0.28f * crack));
                h = 0.15f + 0.25f * rut + 0.1f * f - (1 - crack) * 0.12f;
                s = 0.07f;
                float f1, f2, ox, oy; int id;
                Cells(u, v, 20, 20, 0.8f, 1504, out f1, out f2, out id, out ox, out oy);
                if (Hf(id, 0, 1505) < 0.42f)
                {
                    float rr = 0.12f + Hf(id, 1, 1506) * 0.22f;
                    if (f1 < rr)
                    {
                        float dome = (float)Math.Sqrt(1 - (f1 / rr) * (f1 / rr)), ph = 0.38f + 0.45f * dome;
                        if (ph > h)
                        {
                            float k = Sm(0f, 0.15f, 1 - f1 / rr);
                            h = Lerp(h, ph, k);
                            c = C3.Mix(c, Pebble[(int)(Hf(id, 2, 1507) * 5)] * (0.75f + 0.35f * dome), k);
                            s = Lerp(s, 0.2f, k);
                        }
                    }
                }
            });
            for (int i = 0; i < 60; i++)
            {
                // a few tufts where the traffic does not reach
                float cx = R(r) * GS, cy = R(r) * GS;
                for (int k = 0; k < 14; k++)
                {
                    float hb = 0.4f + R(r) * 0.15f, t = R(r);
                    Stroke(b, cx + (R(r) - 0.5f) * 24, cy + (R(r) - 0.5f) * 24, R(r) * M.TAU, (0.8f + R(r) * 0.8f) * 8, 0.8f, Blade, hb, hb + 0.35f,
                        C3.Mix(C3.X(0x2A3620), C3.X(0x3E4A2A), t), C3.Mix(C3.X(0x4A5A30), C3.X(0x6A6A3A), t), 0.12f);
                }
            }
            return 0.55f;
        }

        static float Mud(Buf b, Random r)
        {
            Fill(b, delegate(float u, float v, out float h, out C3 c, out float s)
            {
                float n = Fbm(u, v, 6, 6, 4, 1600), f = Fbm(u, v, 40, 40, 3, 1601);
                float churn = 1 - Math.Abs(2 * Fbm(u, v, 10, 10, 3, 1602) - 1);
                float p = Sm(0.57f, 0.61f, Fbm(u, v, 4, 4, 4, 1603));          // standing water: flat, dark, mirror-bright
                C3 mud = C3.Mix(C3.X(0x241E1A), C3.X(0x3A3028), n) * (0.88f + 0.24f * f);
                c = C3.Mix(mud, C3.Mix(C3.X(0x12171C), C3.X(0x1A2228), f), p);
                h = Lerp(0.25f + 0.35f * churn + 0.1f * f, 0.12f, p);
                s = Lerp(0.3f + 0.12f * f, 0.95f, p);
            });
            for (int i = 0; i < 260; i++)
            {
                float hh = 0.5f + R(r) * 0.15f; C3 c = C3.X(0x5A4A2A) * (0.7f + R(r) * 0.4f);
                Stroke(b, R(r) * GS, R(r) * GS, R(r) * M.TAU, (1 + R(r) * 1.5f) * 8, 0.7f, Rod, hh, hh, c, c, 0.2f);
            }
            return 0.6f;
        }

        static readonly C3[] CobbleC = { C3.X(0x444854), C3.X(0x4E5462), C3.X(0x5A606E), C3.X(0x666C7A), C3.X(0x52545A) };

        static void GCobblePx(float u, float v, out float h, out C3 c, out float s)
        {
            float f1, f2, ox, oy; int id;
            Cells(u, v, 9, 11, 0.75f, 1700, out f1, out f2, out id, out ox, out oy);
            float e = f2 - f1;
            float stone = Sm(0.05f, 0.13f, e), dome = Sm(0.05f, 0.5f, e);
            float rough = Fbm(u, v, 64, 64, 2, 1701), tone = Hf(id, 0, 1702);
            float tilt = (Hf(id, 3, 1705) - 0.5f) * ox * 0.3f + (Hf(id, 4, 1706) - 0.5f) * oy * 0.3f;
            C3 sc = CobbleC[(int)(Hf(id, 1, 1703) * 5)] * (0.85f + 0.3f * tone + 0.12f * (rough - 0.5f)) + C3.Grey(0.07f * Sm(0.25f, 0.55f, e));
            float moss = Sm(0.5f, 0.62f, Fbm(u, v, 24, 24, 3, 1704));
            C3 gap = C3.Mix(C3.X(0x1C1A18), C3.X(0x223424), moss);
            h = Lerp(0.05f + 0.1f * rough, 0.35f + 0.5f * dome + 0.08f * rough + tilt, stone);
            c = C3.Mix(gap, sc, stone);
            s = Lerp(0.08f, 0.36f + 0.12f * tone, stone);
        }

        static readonly C3[] FlagC = { C3.X(0x3C435A), C3.X(0x4B546D), C3.X(0x444C62), C3.X(0x56607A), C3.X(0x3A4054) };

        static void GFlagPx(float u, float v, out float h, out C3 c, out float s)
        {
            // flagstones 16 by 12.8, courses offset at random, mossy joints, the odd crack
            float X = u * 64, Y = v * 64;
            int row = Math.Min(4, (int)(Y / 12.8f)); float ly = Y - row * 12.8f;
            float x = X + Hf(row, 0, 1800) * 16; int col = (int)(x / 16); float lx = x - col * 16; col &= 3;
            int sid = row * 4 + col;
            float e = RoundRect(lx, ly, 16, 12.8f, 0.9f) + (Fbm(u, v, 32, 32, 2, 1801) - 0.5f) * 0.5f;
            float body = Sm(0.25f, 1.0f, e);
            float rough = Fbm(u, v, 24, 24, 3, 1802), big = Fbm(u, v, 4, 4, 3, 1803);
            float tilt = (Hf(sid, 1, 1804) - 0.5f) * 0.02f * (lx - 8) + (Hf(sid, 2, 1805) - 0.5f) * 0.025f * (ly - 6.4f);
            float crack = Hf(sid, 3, 1806) < 0.4f ? Sm(0f, 0.012f, Math.Abs(Fbm(u, v, 3, 3, 3, 1807 + sid * 7) - 0.5f)) : 1;
            C3 sc = FlagC[(int)(Hf(sid, 4, 1808) * 5)] * (0.88f + 0.22f * rough - Sm(0.6f, 0.75f, big) * 0.15f);
            float moss = Sm(0.48f, 0.6f, Fbm(u, v, 16, 16, 3, 1809));
            C3 joint = C3.Mix(C3.X(0x1A1E1A), C3.X(0x1E3C34), moss);
            h = Lerp(0.05f, 0.55f + 0.25f * rough + tilt, body) - (1 - crack) * 0.25f * body;
            c = C3.Mix(joint, sc * (1 - (1 - crack) * 0.4f), Sm(0.1f, 0.5f, body));
            s = Lerp(0.08f, 0.3f, body);
        }

        static void GPlankPx(float u, float v, out float h, out C3 c, out float s)
        {
            // thirteen weathered boards across, two lengths each, nailed at the joints and to the joists every 16
            float X = u * 64, Y = v * 64;
            const float BW = 64f / 13;
            int row = Math.Min(12, (int)(Y / BW)); float ly = Y - row * BW;
            float j1 = Hf(row, 0, 1900) * 64, j2 = j1 + 22 + Hf(row, 1, 1901) * 20;
            float d1 = Near(X - j1, 64), d2 = Near(X - j2, 64);
            float dj = Math.Abs(d1) < Math.Abs(d2) ? d1 : d2;
            int seg = row * 2 + (WrapF(X - j1, 64) < WrapF(j2 - j1, 64) ? 0 : 1);
            float e = Math.Min(Math.Min(ly, BW - ly), Math.Abs(dj) * 1.4f);
            float body = Sm(0.12f, 0.5f, e);
            float warp = Fbm(u, v, 3, 13, 2, 1902 + seg) * 6;
            float grain = 0.5f + 0.5f * M.Sin((Y * 2.6f + warp) * M.PI);
            float fib = Fbm(u, v, 6, 160, 2, 1903), weather = Fbm(u, v, 4, 4, 3, 1904);
            C3 wood = C3.Mix(C3.X(0x4A342E), C3.X(0x76563F), Hf(seg, 2, 1905)) * (0.86f + 0.12f * grain + 0.12f * (fib - 0.5f));
            wood = C3.Mix(wood, C3.X(0x5A5550), Sm(0.55f, 0.75f, weather) * 0.4f);
            float nail = 9, sx = Near(X - 8, 16);
            for (int k = 0; k < 2; k++)
            {
                float ny = ly - (k == 0 ? 1.1f : BW - 1.1f);
                nail = Math.Min(nail, Math.Min(M.Len(Math.Abs(dj) - 0.9f, ny), M.Len(sx, ny)));
            }
            h = body * (0.75f + 0.12f * grain + 0.1f * fib);
            c = C3.Mix(C3.X(0x120E0C), wood, Sm(0.06f, 0.35f, e));
            s = 0.12f + 0.06f * fib;
            if (nail < 0.3f) { float k = Sm(0.3f, 0.18f, nail); h += 0.06f * k; c = C3.Mix(c, C3.Grey(0.28f), k); s = Lerp(s, 0.45f, k); }
        }

        static readonly C3[] DFlagC = { C3.X(0x2A2E43), C3.X(0x33384F), C3.X(0x2E3248), C3.X(0x3D435B), C3.X(0x282B3E) };

        static void GDFloorPx(float u, float v, out float h, out C3 c, out float s)
        {
            // the undercroft floor: 16-unit flags (as the map's tiles), some laid as halves or quarters
            float X = u * 64, Y = v * 64;
            int tx = Math.Min(3, (int)(X / 16)), ty = Math.Min(3, (int)(Y / 16));
            float lx = X - tx * 16, ly = Y - ty * 16;
            float hs = Hf(tx, ty, 2000);
            int split = hs < 0.4f ? 0 : (hs < 0.65f ? 1 : (hs < 0.85f ? 2 : 3));
            float sw = (split == 1 || split == 3) ? 8 : 16, sh = (split == 2 || split == 3) ? 8 : 16;
            int sx = (int)(lx / sw), sy = (int)(ly / sh);
            float qx = lx - sx * sw, qy = ly - sy * sh;
            int sid = (ty * 4 + tx) * 4 + sy * 2 + sx;
            float e = RoundRect(qx, qy, sw, sh, 1.0f) + (Fbm(u, v, 32, 32, 2, 2001) - 0.5f) * 0.6f;
            float body = Sm(0.3f, 1.2f, e);
            float rough = Fbm(u, v, 32, 32, 3, 2002), big = Fbm(u, v, 4, 4, 3, 2003);
            float tilt = (Hf(sid, 5, 2009) - 0.5f) * 0.03f * (qx - sw / 2) + (Hf(sid, 6, 2010) - 0.5f) * 0.03f * (qy - sh / 2);
            float crack = Hf(sid, 1, 2004) < 0.3f ? Sm(0f, 0.01f, Math.Abs(Fbm(u, v, 3, 3, 3, 2005 + sid * 7) - 0.5f)) : 1;
            float stain = Sm(0.6f, 0.75f, big);
            C3 slab = DFlagC[(int)(Hf(sid, 2, 2006) * 5)] * ((0.88f + 0.24f * rough) * (1 - stain * 0.3f));
            C3 joint = C3.Mix(C3.X(0x14151F), C3.X(0x1B3536), Sm(0.6f, 0.7f, Fbm(u, v, 16, 16, 3, 2007)));
            h = Lerp(0.04f, 0.55f + 0.3f * rough + tilt, body) - (1 - crack) * 0.3f * body;
            c = C3.Mix(joint, slab * (1 - (1 - crack) * 0.45f), Sm(0.15f, 0.5f, body));
            s = Lerp(0.06f, 0.24f + 0.12f * stain, body);
        }

        static void GCarpetPx(float u, float v, out float h, out C3 c, out float s)
        {
            // the field of a long runner: a lattice of lozenges over a worn red pile (the border is drawn where it ends)
            float X = u * 64, Y = v * 64;
            float dd = Math.Abs(WrapF(X, 8) - 4) + Math.Abs(WrapF(Y, 8) - 4);
            float motif = 1 - Sm(0.2f, 0.55f, Math.Abs(dd - 2.8f)), dot = 1 - Sm(0.35f, 0.7f, dd);
            float pile = Fbm(u, v, 256, 256, 1, 2100), pile2 = Fbm(u, v, 64, 64, 2, 2101);
            float worn = Sm(0.6f, 0.72f, Fbm(u, v, 3, 3, 4, 2102));
            C3 field = C3.X(0x6A1A2E) * (0.9f + 0.15f * pile2);
            C3 pat = C3.Mix(field, C3.X(0x9A5A2A), Math.Max(motif, dot) * (1 - worn * 0.6f));
            c = C3.Mix(pat, C3.X(0x4A1424), worn * 0.5f) * (0.92f + 0.16f * pile);
            h = 0.5f + 0.25f * (pile - 0.5f) + 0.12f * motif - worn * 0.15f;
            s = 0.03f;
        }

        static float Bed(Buf b, Random r)
        {
            Fill(b, delegate(float u, float v, out float h, out C3 c, out float s)
            {
                float n = Fbm(u, v, 6, 6, 4, 2200), f = Fbm(u, v, 48, 48, 2, 2201);
                c = C3.Mix(C3.X(0x2A2A22), C3.X(0x3E3C30), n) * (0.9f + 0.2f * f);
                h = 0.15f + 0.2f * n + 0.05f * f;
                s = 0.3f;
                float f1, f2, ox, oy; int id;
                Cells(u, v, 16, 16, 0.85f, 2202, out f1, out f2, out id, out ox, out oy);
                if (Hf(id, 0, 2203) < 0.6f)
                {
                    float rr = 0.15f + Hf(id, 1, 2204) * 0.25f;
                    if (f1 < rr)
                    {
                        float dome = (float)Math.Sqrt(1 - (f1 / rr) * (f1 / rr)), k = Sm(0f, 0.15f, 1 - f1 / rr);
                        h = Lerp(h, 0.45f + 0.4f * dome, k);
                        c = C3.Mix(c, Pebble[(int)(Hf(id, 2, 2205) * 5)] * (0.7f + 0.35f * dome), k);
                        s = Lerp(s, 0.45f, k);
                    }
                }
            });
            for (int i = 0; i < 500; i++)
            {
                float hb = 0.6f + R(r) * 0.1f, t = R(r);
                Stroke(b, R(r) * GS, R(r) * GS, R(r) * M.TAU, (1.5f + R(r) * 2f) * 8, 1f, Blade, hb, hb + 0.2f,
                    C3.Mix(C3.X(0x2A3A22), C3.X(0x3A4A2A), t), C3.Mix(C3.X(0x3A4A2A), C3.X(0x4A5A30), t), 0.3f);
            }
            return 0.6f;
        }

        // inspection
        /// <summary>Writes every layer as pictures (colour, and lit from the upper left to show its relief) for review.</summary>
        public static void Dump(string dir)
        {
            Directory.CreateDirectory(dir);
            Build();
            for (int i = 0; i < ModelAlb.Length; i++) DumpLayer(dir, "m" + i.ToString("00"), ModelAlb[i], ModelNrm[i], MS, ModelK[i]);
            for (int i = 0; i < GroundAlb.Length; i++) DumpLayer(dir, "g" + i.ToString("00"), GroundAlb[i], GroundNrm[i], GS, 1);
            File.WriteAllText(Path.Combine(dir, "matgen.txt"), "painted in " + BuildMs + " ms\r\n");
        }

        static void DumpLayer(string dir, string name, int[] alb, int[] nrm, int n, float k)
        {
            int[] a = new int[n * n], l = new int[n * n];
            float lx = -0.45f, ly = -0.55f, lz = 0.7f;
            for (int i = 0; i < a.Length; i++)
            {
                int c = alb[i], q = nrm[i];
                float r = ((c >> 16) & 255) / 255f * k, g = ((c >> 8) & 255) / 255f * k, b = (c & 255) / 255f * k;
                a[i] = unchecked((int)0xFF000000) | (B8(r) << 16) | (B8(g) << 8) | B8(b);
                float nx = ((q >> 16) & 255) / 127.5f - 1, ny = ((q >> 8) & 255) / 127.5f - 1, nz = (q & 255) / 127.5f - 1;
                float gl = ((q >> 24) & 255) / 255f;
                float d = Math.Max(0, nx * lx + ny * ly + nz * lz), sp = (float)Math.Pow(Math.Max(0, nz * 0.9f + d * 0.1f), 2 + gl * 40) * gl;
                float f = 0.3f + 0.9f * d;
                l[i] = unchecked((int)0xFF000000) | (B8(r * f + sp * 0.4f) << 16) | (B8(g * f + sp * 0.4f) << 8) | B8(b * f + sp * 0.4f);
            }
            Save(Path.Combine(dir, name + "_alb.png"), a, n);
            Save(Path.Combine(dir, name + "_lit.png"), l, n);
        }

        static void Save(string path, int[] px, int n)
        {
            using (Bitmap bmp = new Bitmap(n, n, PixelFormat.Format32bppArgb))
            {
                BitmapData d = bmp.LockBits(new Rectangle(0, 0, n, n), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                Marshal.Copy(px, 0, d.Scan0, px.Length);
                bmp.UnlockBits(d);
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }
}
