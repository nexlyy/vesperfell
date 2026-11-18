using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>
    /// Smooth enlargements of sprites for the high-resolution interface. Pixel art is doubled with an edge-following
    /// rule (EPX) until it is well past the size wanted and then box-filtered down, so curves and diagonals come out as
    /// clean anti-aliased edges instead of staircases. Sprites given a large source of their own (rendered 3D icons and
    /// portraits) are resampled from that instead.
    /// </summary>
    public static class HiArt
    {
        static readonly Dictionary<Sprite, Sprite[]> cache = new Dictionary<Sprite, Sprite[]>();
        static readonly Dictionary<Sprite, Sprite> sources = new Dictionary<Sprite, Sprite>();

        /// <summary>Gives a sprite a large image to be scaled from (any size; it is fitted to the sprite's own box).</summary>
        public static void SetSource(Sprite s, Sprite big)
        {
            if (s == null || big == null) return;
            lock (cache) { sources[s] = big; cache.Remove(s); }
        }

        public static bool HasSource(Sprite s) { lock (cache) return s != null && sources.ContainsKey(s); }

        /// <summary>The sprite k times larger (origin scaled with it).</summary>
        public static Sprite Get(Sprite s, int k)
        {
            if (k <= 1 || s == null) return s;
            if (k > 24) k = 24;
            Sprite[] a; Sprite src;
            lock (cache)
            {
                if (!cache.TryGetValue(s, out a)) { a = new Sprite[25]; cache[s] = a; }
                if (a[k] != null) return a[k];
                sources.TryGetValue(s, out src);
            }
            Sprite big = src != null ? Resample(src, s.W * k, s.H * k) : Enlarge(s, k);
            big.OX = s.OX * k; big.OY = s.OY * k;
            lock (cache) a[k] = big;
            return big;
        }

        static Sprite Enlarge(Sprite s, int k)
        {
            Sprite cur = Clean(s);
            int f = 1;
            while (f < k * 2 && f < 16) { cur = Epx(cur); f *= 2; }
            return f == k ? cur : Resample(cur, s.W * k, s.H * k);
        }

        /// <summary>A copy with every clear pixel exactly 0 and the emissive flag folded into plain opacity.</summary>
        static Sprite Clean(Sprite s)
        {
            Sprite o = new Sprite(s.W, s.H, s.OX, s.OY);
            for (int i = 0; i < s.Px.Length; i++)
            {
                int c = s.Px[i], a = (c >> 24) & 255;
                o.Px[i] = a == 0 ? 0 : (a >= 254 ? (c | Col.Opaque) : c);
            }
            return o;
        }

        static int[] softA, softB;

        /// <summary>
        /// Blurs only the alpha bytes of p (w by h) with a 5-tap binomial filter. A hard-edged mask (explored ground on a
        /// map) then has a rounded contour once it is drawn masked, instead of one that follows the squares it was made of.
        /// </summary>
        public static void SoftenAlpha(int[] p, int w, int h)
        {
            int n = w * h;
            if (softA == null || softA.Length < n) { softA = new int[n]; softB = new int[n]; }
            int[] a = softA, b = softB;
            for (int i = 0; i < n; i++) a[i] = (p[i] >> 24) & 255;
            for (int y = 0; y < h; y++)
            {
                int r = y * w;
                for (int x = 0; x < w; x++)
                {
                    int xm2 = x > 1 ? x - 2 : 0, xm1 = x > 0 ? x - 1 : 0, xp1 = x < w - 1 ? x + 1 : w - 1, xp2 = x < w - 2 ? x + 2 : w - 1;
                    b[r + x] = a[r + xm2] + 4 * a[r + xm1] + 6 * a[r + x] + 4 * a[r + xp1] + a[r + xp2];
                }
            }
            for (int y = 0; y < h; y++)
            {
                int r = y * w;
                int ym2 = (y > 1 ? y - 2 : 0) * w, ym1 = (y > 0 ? y - 1 : 0) * w, yp1 = (y < h - 1 ? y + 1 : h - 1) * w, yp2 = (y < h - 2 ? y + 2 : h - 1) * w;
                for (int x = 0; x < w; x++)
                {
                    int v = (b[ym2 + x] + 4 * b[ym1 + x] + 6 * b[r + x] + 4 * b[yp1 + x] + b[yp2 + x] + 128) >> 8;
                    p[r + x] = (p[r + x] & 0xFFFFFF) | (v << 24);
                }
            }
        }

        /// <summary>Doubles an opaque image (w by h) into dst (2w by 2h) with the EPX rule, so its edges follow the shapes.</summary>
        public static void Epx2(int[] p, int w, int h, int[] q)
        {
            int w2 = w * 2;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int P = p[y * w + x];
                    int A = y > 0 ? p[(y - 1) * w + x] : P;
                    int B = x < w - 1 ? p[y * w + x + 1] : P;
                    int C = x > 0 ? p[y * w + x - 1] : P;
                    int D = y < h - 1 ? p[(y + 1) * w + x] : P;
                    int e0 = P, e1 = P, e2 = P, e3 = P;
                    if (C == A && C != D && A != B) e0 = A;
                    if (A == B && A != C && B != D) e1 = B;
                    if (D == C && D != B && C != A) e2 = C;
                    if (B == D && B != A && D != C) e3 = D;
                    int o0 = y * 2 * w2 + x * 2;
                    q[o0] = e0; q[o0 + 1] = e1; q[o0 + w2] = e2; q[o0 + w2 + 1] = e3;
                }
        }

        /// <summary>EPX / Scale2x: each pixel becomes four, taking a neighbour's colour where two neighbours agree on a diagonal edge.</summary>
        static Sprite Epx(Sprite s)
        {
            int w = s.W, h = s.H, w2 = w * 2;
            Sprite o = new Sprite(w * 2, h * 2);
            int[] p = s.Px, q = o.Px;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int P = p[y * w + x];
                    int A = y > 0 ? p[(y - 1) * w + x] : 0;
                    int B = x < w - 1 ? p[y * w + x + 1] : 0;
                    int C = x > 0 ? p[y * w + x - 1] : 0;
                    int D = y < h - 1 ? p[(y + 1) * w + x] : 0;
                    int e0 = P, e1 = P, e2 = P, e3 = P;
                    if (C == A && C != D && A != B) e0 = A;
                    if (A == B && A != C && B != D) e1 = B;
                    if (D == C && D != B && C != A) e2 = C;
                    if (B == D && B != A && D != C) e3 = D;
                    int o0 = y * 2 * w2 + x * 2;
                    q[o0] = e0; q[o0 + 1] = e1; q[o0 + w2] = e2; q[o0 + w2 + 1] = e3;
                }
            return o;
        }

        /// <summary>Resizes with premultiplied alpha: an area (box) filter going down, a tent filter going up.</summary>
        public static Sprite Resample(Sprite s, int nw, int nh)
        {
            int w = s.W, h = s.H, n = w * h;
            nw = Math.Max(1, nw); nh = Math.Max(1, nh);
            float[] r = new float[n], g = new float[n], b = new float[n], a = new float[n];
            for (int i = 0; i < n; i++)
            {
                int c = s.Px[i], al = (c >> 24) & 255;
                if (al >= 254) al = 255;
                float fa = al / 255f;
                a[i] = fa; r[i] = ((c >> 16) & 255) * fa; g[i] = ((c >> 8) & 255) * fa; b[i] = (c & 255) * fa;
            }
            r = PassY(PassX(r, w, h, nw), nw, h, nh); g = PassY(PassX(g, w, h, nw), nw, h, nh);
            b = PassY(PassX(b, w, h, nw), nw, h, nh); a = PassY(PassX(a, w, h, nw), nw, h, nh);
            Sprite o = new Sprite(nw, nh);
            for (int i = 0; i < nw * nh; i++)
            {
                float fa = a[i];
                if (fa < 0.004f) continue;
                int A = (int)(fa * 255f + 0.5f);
                A = A >= 253 ? 255 : A;
                float k = 1f / fa;
                o.Px[i] = (A << 24) | (C8(r[i] * k) << 16) | (C8(g[i] * k) << 8) | C8(b[i] * k);
            }
            return o;
        }

        static int C8(float v) { int c = (int)(v + 0.5f); return c < 0 ? 0 : (c > 255 ? 255 : c); }

        static float[] PassX(float[] src, int w, int h, int nw)
        {
            if (nw == w) return src;
            float[] dst = new float[nw * h];
            float f = w / (float)nw;
            for (int X = 0; X < nw; X++)
            {
                if (f >= 1)
                {
                    float a0 = X * f, a1 = a0 + f;
                    int i0 = (int)a0, i1 = Math.Min(w - 1, (int)Math.Ceiling(a1) - 1);
                    for (int i = i0; i <= i1; i++)
                    {
                        float ov = (Math.Min(a1, i + 1) - Math.Max(a0, i)) / f;
                        if (ov <= 0) continue;
                        for (int y = 0; y < h; y++) dst[y * nw + X] += src[y * w + i] * ov;
                    }
                }
                else
                {
                    float c = (X + 0.5f) * f - 0.5f;
                    int i0 = (int)Math.Floor(c); float t = c - i0;
                    int ia = Math.Max(0, Math.Min(w - 1, i0)), ib = Math.Max(0, Math.Min(w - 1, i0 + 1));
                    for (int y = 0; y < h; y++) dst[y * nw + X] = src[y * w + ia] * (1 - t) + src[y * w + ib] * t;
                }
            }
            return dst;
        }

        static float[] PassY(float[] src, int w, int h, int nh)
        {
            if (nh == h) return src;
            float[] dst = new float[w * nh];
            float f = h / (float)nh;
            for (int Y = 0; Y < nh; Y++)
            {
                if (f >= 1)
                {
                    float a0 = Y * f, a1 = a0 + f;
                    int j0 = (int)a0, j1 = Math.Min(h - 1, (int)Math.Ceiling(a1) - 1);
                    for (int j = j0; j <= j1; j++)
                    {
                        float ov = (Math.Min(a1, j + 1) - Math.Max(a0, j)) / f;
                        if (ov <= 0) continue;
                        int so = j * w, d0 = Y * w;
                        for (int x = 0; x < w; x++) dst[d0 + x] += src[so + x] * ov;
                    }
                }
                else
                {
                    float c = (Y + 0.5f) * f - 0.5f;
                    int j0 = (int)Math.Floor(c); float t = c - j0;
                    int ja = Math.Max(0, Math.Min(h - 1, j0)), jb = Math.Max(0, Math.Min(h - 1, j0 + 1));
                    for (int x = 0; x < w; x++) dst[Y * w + x] = src[ja * w + x] * (1 - t) + src[jb * w + x] * t;
                }
            }
            return dst;
        }
    }
}
