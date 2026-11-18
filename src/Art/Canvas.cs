using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>The game's palette. Everything in the world is drawn from these ramps so the art stays coherent.</summary>
    public static class Pal
    {
        static int H(int rgb) { return Col.Hex(rgb); }

        // ink / night
        public static readonly int Ink0 = H(0x0B0A12), Ink1 = H(0x15121F), Ink2 = H(0x211C30), Ink3 = H(0x2E2843);
        // slate & steel
        public static readonly int St0 = H(0x262A3B), St1 = H(0x363C52), St2 = H(0x4B546D), St3 = H(0x66728B), St4 = H(0x8996AA), St5 = H(0xB2BDC8), St6 = H(0xDCE3E6);
        // foliage
        public static readonly int Lf0 = H(0x12222A), Lf1 = H(0x1B3536), Lf2 = H(0x234B41), Lf3 = H(0x30644A), Lf4 = H(0x4A8152), Lf5 = H(0x72A05A), Lf6 = H(0xA3BF6C);
        // grass ground
        public static readonly int Gr0 = H(0x1F3330), Gr1 = H(0x284338), Gr2 = H(0x33543F), Gr3 = H(0x426648), Gr4 = H(0x567A50), Gr5 = H(0x7C9A5C);
        // earth & wood
        public static readonly int Ea0 = H(0x1E161B), Ea1 = H(0x2E2126), Ea2 = H(0x44302E), Ea3 = H(0x5F4337), Ea4 = H(0x7E5D45), Ea5 = H(0xA17F59), Ea6 = H(0xC6A678), Ea7 = H(0xE2CC9E);
        // ember
        public static readonly int Em0 = H(0x3D1418), Em1 = H(0x6B1F1E), Em2 = H(0x9E3123), Em3 = H(0xCF5327), Em4 = H(0xEC8A32), Em5 = H(0xF7BE52), Em6 = H(0xFDE999), Em7 = H(0xFFF8DC);
        // gloam
        public static readonly int Gl0 = H(0x231C3A), Gl1 = H(0x3A2E5E), Gl2 = H(0x584688), Gl3 = H(0x7F6CB6), Gl4 = H(0xA7A0DC), Gl5 = H(0xC9D3F5), Gl6 = H(0xEAF3FF);
        // crimson
        public static readonly int Cr0 = H(0x3F1022), Cr1 = H(0x6E1730), Cr2 = H(0xA2233B), Cr3 = H(0xCF4150), Cr4 = H(0xEB7A78);
        // water
        public static readonly int Wa0 = H(0x10202E), Wa1 = H(0x162E40), Wa2 = H(0x1F4254), Wa3 = H(0x2D5C6C), Wa4 = H(0x4A8490), Wa5 = H(0x86B8BC);
        // skin
        public static readonly int Sk0 = H(0x5E3A36), Sk1 = H(0x8E5A4A), Sk2 = H(0xBF8565), Sk3 = H(0xE0AE86), Sk4 = H(0xF3D2AE);
        // bone & parchment
        public static readonly int Bn0 = H(0x6F6756), Bn1 = H(0x9C927A), Bn2 = H(0xC8BE9F), Bn3 = H(0xE8E0C4), Bn4 = H(0xF7F2E0);
        // gold / brass
        public static readonly int Go0 = H(0x4A3520), Go1 = H(0x7A5A2C), Go2 = H(0xA8823C), Go3 = H(0xD2AC52), Go4 = H(0xEED58A);
        // olive
        public static readonly int Ol0 = H(0x2A3320), Ol1 = H(0x3C4A2A), Ol2 = H(0x57683A), Ol3 = H(0x7C8C4E), Ol4 = H(0xA4B26A);
        // venom
        public static readonly int Vn0 = H(0x243A1C), Vn1 = H(0x3F6A24), Vn2 = H(0x6FA82E), Vn3 = H(0xA8E04A), Vn4 = H(0xDCFA9A);
        // frost
        public static readonly int Fr0 = H(0x1E3A5C), Fr1 = H(0x2F6A9A), Fr2 = H(0x58A8D8), Fr3 = H(0x9BDCF4), Fr4 = H(0xE2F8FF);
        public static readonly int White = H(0xFFFFFF), Black = H(0x000000);
    }

    /// <summary>A small pixel canvas with the primitives a pixel artist needs. All procedural art is drawn on these.</summary>
    public sealed class Cv
    {
        public readonly int W, H;
        public readonly int[] P;

        public Cv(int w, int h) { W = w; H = h; P = new int[w * h]; }

        public bool In(int x, int y) { return x >= 0 && y >= 0 && x < W && y < H; }
        public void Set(int x, int y, int c) { if (x >= 0 && y >= 0 && x < W && y < H) P[y * W + x] = c; }
        public int Get(int x, int y) { return (x >= 0 && y >= 0 && x < W && y < H) ? P[y * W + x] : 0; }
        public bool Solid(int x, int y) { return (x >= 0 && y >= 0 && x < W && y < H) && ((P[y * W + x] >> 24) & 255) != 0; }
        public void Clear() { Array.Clear(P, 0, P.Length); }

        public Cv Copy() { Cv c = new Cv(W, H); Array.Copy(P, c.P, P.Length); return c; }

        public void Rect(int x, int y, int w, int h, int c)
        {
            for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) Set(i, j, c);
        }

        public void Ell(float cx, float cy, float rx, float ry, int c)
        {
            if (rx <= 0 || ry <= 0) return;
            int y0 = M.Floor(cy - ry), y1 = M.Ceil(cy + ry), x0 = M.Floor(cx - rx), x1 = M.Ceil(cx + rx);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
        }

        public void Circle(float cx, float cy, float r, int c) { Ell(cx, cy, r, r, c); }

        public void Line(int x0, int y0, int x1, int y1, int c)
        {
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            int guard = 0;
            while (guard++ < 5000)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public void Thick(float x0, float y0, float x1, float y1, float w0, float w1, int c)
        {
            float len = M.Dist(x0, y0, x1, y1);
            int n = Math.Max(1, (int)(len * 2));
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float r = M.Lerp(w0, w1, t) * 0.5f;
                Ell(M.Lerp(x0, x1, t), M.Lerp(y0, y1, t), r, r, c);
            }
        }

        /// <summary>Scanline polygon fill (even-odd). pts = x0,y0,x1,y1,...</summary>
        public void Poly(int c, params float[] pts)
        {
            int n = pts.Length / 2;
            float minY = 1e9f, maxY = -1e9f;
            for (int i = 0; i < n; i++) { if (pts[i * 2 + 1] < minY) minY = pts[i * 2 + 1]; if (pts[i * 2 + 1] > maxY) maxY = pts[i * 2 + 1]; }
            float[] xs = new float[n];
            for (int y = M.Floor(minY); y <= M.Ceil(maxY); y++)
            {
                float sy = y + 0.5f; int cnt = 0;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    float ya = pts[i * 2 + 1], yb = pts[j * 2 + 1];
                    if ((ya <= sy && yb > sy) || (yb <= sy && ya > sy))
                        xs[cnt++] = pts[i * 2] + (sy - ya) / (yb - ya) * (pts[j * 2] - pts[i * 2]);
                }
                Array.Sort(xs, 0, cnt);
                for (int k = 0; k + 1 < cnt; k += 2)
                {
                    int xa = M.Round(xs[k]), xb = M.Round(xs[k + 1]);
                    for (int x = xa; x < xb; x++) Set(x, y, c);
                }
            }
        }

        /// <summary>Builds a char-to-colour table: Map('a', colourA, 'b', colourB, ...).</summary>
        public static int[] Map(params object[] pairs)
        {
            int[] m = new int[128];
            for (int i = 0; i + 1 < pairs.Length; i += 2) m[(char)pairs[i]] = (int)pairs[i + 1];
            return m;
        }

        /// <summary>Stamps hand-pixelled rows. Characters missing from the map (and '.') are transparent.</summary>
        public void Ascii(int x, int y, string[] rows, int[] map)
        {
            for (int j = 0; j < rows.Length; j++)
            {
                string r = rows[j];
                for (int i = 0; i < r.Length; i++)
                {
                    char ch = r[i];
                    if (ch == '.' || ch == ' ' || ch >= 128) continue;
                    int c = map[ch];
                    if (c != 0) Set(x + i, y + j, c);
                }
            }
        }

        public void Draw(Cv src, int dx, int dy) { Draw(src, dx, dy, false); }

        public void Draw(Cv src, int dx, int dy, bool flip)
        {
            for (int j = 0; j < src.H; j++)
                for (int i = 0; i < src.W; i++)
                {
                    int c = src.P[j * src.W + (flip ? src.W - 1 - i : i)];
                    int a = (c >> 24) & 255;
                    if (a == 0) continue;
                    if (a >= 254) Set(dx + i, dy + j, c);
                    else
                    {
                        int d = Get(dx + i, dy + j);
                        if (((d >> 24) & 255) == 0) Set(dx + i, dy + j, c);
                        else Set(dx + i, dy + j, (d & Col.Opaque) | (Col.Lerp(d, c, a / 255f) & 0xFFFFFF));
                    }
                }
        }

        public void DrawSprite(Sprite s, int x, int y, bool flip)
        {
            int dx = x - (flip ? s.W - 1 - s.OX : s.OX), dy = y - s.OY;
            for (int j = 0; j < s.H; j++)
                for (int i = 0; i < s.W; i++)
                {
                    int c = s.Px[j * s.W + (flip ? s.W - 1 - i : i)];
                    if (((c >> 24) & 255) != 0) Set(dx + i, dy + j, c);
                }
        }

        /// <summary>Adds a one pixel outline around everything already drawn.</summary>
        public void Outline(int c) { Outline(c, false); }

        public void Outline(int c, bool corners)
        {
            int[] src = (int[])P.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (((src[y * W + x] >> 24) & 255) != 0) continue;
                    bool n = false;
                    if (x > 0 && ((src[y * W + x - 1] >> 24) & 255) != 0) n = true;
                    else if (x < W - 1 && ((src[y * W + x + 1] >> 24) & 255) != 0) n = true;
                    else if (y > 0 && ((src[(y - 1) * W + x] >> 24) & 255) != 0) n = true;
                    else if (y < H - 1 && ((src[(y + 1) * W + x] >> 24) & 255) != 0) n = true;
                    else if (corners)
                    {
                        if (x > 0 && y > 0 && ((src[(y - 1) * W + x - 1] >> 24) & 255) != 0) n = true;
                        else if (x < W - 1 && y > 0 && ((src[(y - 1) * W + x + 1] >> 24) & 255) != 0) n = true;
                        else if (x > 0 && y < H - 1 && ((src[(y + 1) * W + x - 1] >> 24) & 255) != 0) n = true;
                        else if (x < W - 1 && y < H - 1 && ((src[(y + 1) * W + x + 1] >> 24) & 255) != 0) n = true;
                    }
                    if (n) P[y * W + x] = c;
                }
        }

        /// <summary>
        /// Cheap volume: pixels on the upper-left rim get lighter, pixels on the lower-right rim get darker.
        /// 'skip' colours (e.g. outlines, glows) are left alone.
        /// </summary>
        public void RimLight(int light, int dark)
        {
            int[] src = (int[])P.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int c = src[y * W + x];
                    int a = (c >> 24) & 255;
                    if (a != 255) continue;
                    bool up = y == 0 || ((src[(y - 1) * W + x] >> 24) & 255) == 0;
                    bool left = x == 0 || ((src[y * W + x - 1] >> 24) & 255) == 0;
                    bool down = y == H - 1 || ((src[(y + 1) * W + x] >> 24) & 255) == 0;
                    bool right = x == W - 1 || ((src[y * W + x + 1] >> 24) & 255) == 0;
                    if ((up || left) && light != 0 && !(down || right)) P[y * W + x] = Col.Shade(c, light);
                    else if ((down || right) && dark != 0 && !(up || left)) P[y * W + x] = Col.Shade(c, -dark);
                }
        }

        public void Replace(int from, int to)
        {
            for (int i = 0; i < P.Length; i++) if (P[i] == from) P[i] = to;
        }

        public delegate int PixelFn(int x, int y, int c);

        /// <summary>Runs a function over every non-transparent pixel.</summary>
        public void Each(PixelFn fn)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int c = P[y * W + x];
                    if (((c >> 24) & 255) != 0) P[y * W + x] = fn(x, y, c);
                }
        }

        /// <summary>Bounding box of drawn pixels.</summary>
        public bool Bounds(out int x0, out int y0, out int x1, out int y1)
        {
            x0 = W; y0 = H; x1 = -1; y1 = -1;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (((P[y * W + x] >> 24) & 255) != 0)
                    {
                        if (x < x0) x0 = x; if (x > x1) x1 = x;
                        if (y < y0) y0 = y; if (y > y1) y1 = y;
                    }
            return x1 >= x0;
        }

        public Sprite ToSprite(int ox, int oy)
        {
            Sprite s = new Sprite(W, H, ox, oy);
            Array.Copy(P, s.Px, P.Length);
            return s;
        }

        /// <summary>Sprite cropped to its drawn pixels; the origin is adjusted so it still refers to the same point.</summary>
        public Sprite ToSpriteTrim(int ox, int oy)
        {
            int x0, y0, x1, y1;
            if (!Bounds(out x0, out y0, out x1, out y1)) return new Sprite(1, 1, 0, 0);
            Sprite s = new Sprite(x1 - x0 + 1, y1 - y0 + 1, ox - x0, oy - y0);
            for (int y = y0; y <= y1; y++) Array.Copy(P, y * W + x0, s.Px, (y - y0) * s.W, s.W);
            return s;
        }

        /// <summary>4x4 ordered dither threshold in [0,1).</summary>
        public static float Bayer(int x, int y)
        {
            int[] b = BayerM;
            return (b[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
        }
        static readonly int[] BayerM = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    }
}
