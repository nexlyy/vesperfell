using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>Procedurally drawn vegetation and rocks. Every call with a different seed yields a unique variant.</summary>
    public static class NatureArt
    {
        public static readonly int[] RampTeal = { Pal.Lf0, Pal.Lf1, Pal.Lf2, Pal.Lf3, Pal.Lf4 };
        public static readonly int[] RampGreen = { Pal.Lf1, Pal.Lf2, Pal.Lf3, Pal.Lf4, Pal.Lf5 };
        public static readonly int[] RampRust = { Pal.Em0, Pal.Em1, Pal.Em2, Pal.Em3, Pal.Em4 };
        public static readonly int[] RampViolet = { Pal.Ink3, Pal.Gl1, Pal.Gl2, Pal.Gl3, Pal.Gl4 };
        public static readonly int[] RampPale = { Pal.Ol0, Pal.Ol1, Pal.Ol2, Pal.Ol3, Pal.Ol4 };
        public static readonly int[] RampThorn = { Pal.Ink2, Pal.Cr0, Pal.Cr1, Pal.Cr2, Pal.Cr3 };

        static void Canopy(Cv cv, Rng r, int[] rp, float ccx, float ccy, float spreadX, float spreadY, int n, float rMin, float rMax)
        {
            float[] bx = new float[n], by = new float[n], br = new float[n];
            for (int i = 0; i < n; i++)
            {
                float a = r.F() * M.TAU, d = M.Sqrt(r.F());
                bx[i] = ccx + M.Cos(a) * d * spreadX;
                by[i] = ccy + M.Sin(a) * d * spreadY;
                br[i] = rMin + r.F() * (rMax - rMin);
            }
            // back-to-front (top first) so lower clumps overlap upper ones
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (by[j] < by[i])
                    {
                        float t = by[i]; by[i] = by[j]; by[j] = t;
                        t = bx[i]; bx[i] = bx[j]; bx[j] = t;
                        t = br[i]; br[i] = br[j]; br[j] = t;
                    }
            for (int i = 0; i < n; i++) cv.Circle(bx[i], by[i] + 1f, br[i] + 1.3f, rp[0]);
            for (int i = 0; i < n; i++)
            {
                cv.Circle(bx[i], by[i], br[i], rp[1]);
                cv.Circle(bx[i] - br[i] * 0.16f, by[i] - br[i] * 0.2f, br[i] * 0.74f, rp[2]);
                cv.Circle(bx[i] - br[i] * 0.3f, by[i] - br[i] * 0.38f, br[i] * 0.44f, rp[3]);
                if (br[i] > 4.5f)
                {
                    int hx = (int)(bx[i] - br[i] * 0.42f), hy = (int)(by[i] - br[i] * 0.52f);
                    cv.Set(hx, hy, rp[4]); cv.Set(hx + 1, hy, rp[4]);
                    if (r.Chance(0.6f)) cv.Set(hx, hy + 1, rp[4]);
                }
            }
        }

        static void LeafTexture(Cv cv, Rng r, int[] rp, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int x = r.Int(cv.W), y = r.Int(cv.H);
                int c = cv.Get(x, y);
                if (c == rp[2]) { cv.Set(x, y, rp[1]); if (r.Chance(0.5f)) cv.Set(x + 1, y, rp[1]); }
                else if (c == rp[3]) cv.Set(x, y, rp[2]);
                else if (c == rp[1] && r.Chance(0.5f)) cv.Set(x, y, rp[0]);
            }
        }

        static void Trunk(Cv cv, int cx, int by, int th, float w0, float w1, int light, int mid, int dark)
        {
            for (int i = 0; i <= th; i++)
            {
                int y = by - i;
                float t = i / (float)Math.Max(1, th);
                int hw = (int)(M.Lerp(w0, w1, t) + 0.5f);
                if (i == 0) hw += 2; else if (i == 1) hw += 1;
                for (int x = cx - hw; x <= cx + hw; x++)
                {
                    int c = mid;
                    if (x <= cx - hw + (hw > 2 ? 1 : 0)) c = light;
                    else if (x >= cx + hw - 1) c = dark;
                    if (((x * 7 + y * 5) % 9) == 0 && c == mid) c = dark;
                    cv.Set(x, y, c);
                }
            }
        }

        /// <summary>Broad-leafed tree with a clumped canopy.</summary>
        public static Sprite Oak(Rng r, int[] rp, float sc)
        {
            int W = (int)(56 * sc) + 4, H = (int)(66 * sc) + 4;
            Cv cv = new Cv(W, H);
            int cx = W / 2, by = H - 3;
            int th = (int)((15 + r.Int(5)) * sc);
            Trunk(cv, cx, by, th, 3.2f * sc, 2.0f * sc, Pal.Ea3, Pal.Ea2, Pal.Ea1);
            // a low branch stub
            if (r.Chance(0.5f)) { int s = r.Chance(0.5f) ? -1 : 1; cv.Thick(cx, by - th * 0.55f, cx + s * 6 * sc, by - th * 0.8f, 2.4f, 1.4f, Pal.Ea1); }
            // shade the trunk under the canopy
            for (int i = (int)(th * 0.55f); i <= th; i++)
                for (int x = cx - 5; x <= cx + 5; x++)
                    if (cv.Solid(x, by - i)) cv.Set(x, by - i, Pal.Ea0);
            Canopy(cv, r, rp, cx + r.Sym(2), by - th - 9 * sc, 16 * sc, 10 * sc, 9 + r.Int(4), 6.5f * sc, 10.5f * sc);
            LeafTexture(cv, r, rp, W * H / 9);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(cx, by + 1);
        }

        /// <summary>Tall conifer made of layered, jagged tiers.</summary>
        public static Sprite Pine(Rng r, int[] rp, float sc)
        {
            int W = (int)(40 * sc) + 4, H = (int)(74 * sc) + 4;
            Cv cv = new Cv(W, H);
            int cx = W / 2, by = H - 3;
            Trunk(cv, cx, by, (int)(9 * sc), 2.0f * sc, 1.6f * sc, Pal.Ea3, Pal.Ea2, Pal.Ea1);
            int tiers = 5 + r.Int(2);
            float step = 10.5f * sc, ht = 16f * sc;
            for (int i = 0; i < tiers; i++)
            {
                float yb = by - 6 * sc - i * step;
                float hw = (16.5f - i * (13.5f / tiers)) * sc;
                int lean = r.Int(3) - 1;
                for (int dy = 0; dy < (int)ht; dy++)
                {
                    int y = (int)(yb - dy);
                    float w = hw * (1f - dy / ht);
                    int wi = (int)(w + ((dy % 3) == 0 ? 1.6f : 0f) + 0.5f);
                    for (int x = -wi; x <= wi; x++)
                    {
                        int c = rp[1];
                        if (dy < 2) c = rp[0];
                        else if (x > wi * 0.45f) c = rp[0];
                        else if (x < -wi * 0.62f && dy > 2) c = rp[3];
                        else if (x < -wi * 0.2f) c = rp[2];
                        if (c == rp[2] && Noise.H(x + i * 31, dy, 5) < 0.18f) c = rp[1];
                        if (c == rp[1] && Noise.H(x + i * 17, dy, 9) < 0.14f) c = rp[0];
                        cv.Set(cx + x + (dy < 4 ? lean : 0), y, c);
                    }
                }
            }
            cv.Set(cx, (int)(by - 6 * sc - (tiers - 1) * step - ht), rp[2]);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(cx, by + 1);
        }

        static void Branch(Cv cv, Rng r, float x, float y, float ang, float len, float w, int depth, int c)
        {
            float x2 = x + M.Cos(ang) * len, y2 = y + M.Sin(ang) * len;
            cv.Thick(x, y, x2, y2, w, Math.Max(1f, w * 0.7f), c);
            if (depth <= 0) return;
            Branch(cv, r, x2, y2, ang - r.F(0.3f, 0.8f), len * r.F(0.62f, 0.8f), w * 0.68f, depth - 1, c);
            Branch(cv, r, x2, y2, ang + r.F(0.3f, 0.8f), len * r.F(0.62f, 0.8f), w * 0.68f, depth - 1, c);
            if (depth > 1 && r.Chance(0.4f)) Branch(cv, r, x2, y2, ang + r.Sym(0.25f), len * 0.6f, w * 0.6f, depth - 2, c);
        }

        /// <summary>Leafless, clawing tree.</summary>
        public static Sprite DeadTree(Rng r, int bark, float sc)
        {
            int W = (int)(50 * sc) + 4, H = (int)(58 * sc) + 4;
            Cv cv = new Cv(W, H);
            int cx = W / 2, by = H - 3;
            cv.Rect(cx - 3, by - 1, 7, 2, bark);
            Branch(cv, r, cx, by, -M.PI / 2 + r.Sym(0.18f), 17 * sc, 5.2f * sc, 3, bark);
            cv.RimLight(1, 1);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(cx, by + 1);
        }

        public static Sprite Bush(Rng r, int[] rp, bool berries)
        {
            Cv cv = new Cv(30, 22);
            int cx = 15, by = 19;
            Canopy(cv, r, rp, cx, by - 6, 6.5f, 2.5f, 5, 4f, 6f);
            LeafTexture(cv, r, rp, 40);
            if (berries) for (int i = 0; i < 5; i++) { int x = cx + r.Range(-8, 8), y = by - r.Range(3, 10); if (cv.Solid(x, y)) cv.Set(x, y, Pal.Cr3); }
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(cx, by + 1);
        }

        /// <summary>Thornbrake – a bramble wall that can be cut down.</summary>
        public static Sprite Bramble(Rng r)
        {
            Cv cv = new Cv(36, 28);
            int cx = 18, by = 25;
            int[] rp = RampThorn;
            for (int i = 0; i < 9; i++)
            {
                float a = -M.PI * r.F(0.08f, 0.92f);
                float len = r.F(9, 15);
                float sx = cx + r.Sym(9);
                float mx = sx + M.Cos(a) * len * 0.6f + r.Sym(3), my = by - 2 + M.Sin(a) * len * 0.8f;
                float ex = mx + r.Sym(8), ey = my + r.F(2, 7);
                cv.Thick(sx, by - 1, mx, my, 2.4f, 1.8f, rp[1]);
                cv.Thick(mx, my, ex, ey, 1.8f, 1.2f, rp[1]);
                // thorns
                for (int k = 0; k < 4; k++)
                {
                    float t = r.F();
                    int tx = (int)M.Lerp(sx, mx, t), ty = (int)M.Lerp(by - 1, my, t);
                    cv.Set(tx + (r.Chance(0.5f) ? -2 : 2), ty - 1, rp[3]);
                }
            }
            cv.RimLight(1, 1);
            for (int i = 0; i < 16; i++) { int x = r.Int(36), y = r.Int(28); if (cv.Solid(x, y) && !cv.Solid(x, y - 1)) cv.Set(x, y - 1, rp[4]); }
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(cx, by + 1);
        }

        public static Sprite Rock(Rng r, int size, bool mossy)
        {
            float R = size == 0 ? 4.5f : (size == 1 ? 7.5f : 12f);
            int W = (int)(R * 2) + 8, H = (int)(R * 1.7f) + 8;
            Cv cv = new Cv(W, H);
            float cx = W / 2f, cy = H / 2f + 1;
            int n = 9;
            float[] pts = new float[n * 2], top = new float[n * 2], hi = new float[n * 2];
            float ph = r.F() * M.TAU;
            for (int i = 0; i < n; i++)
            {
                float a = ph + i * M.TAU / n;
                float rr = R * r.F(0.78f, 1.08f);
                float x = M.Cos(a) * rr, y = M.Sin(a) * rr * 0.72f;
                pts[i * 2] = cx + x; pts[i * 2 + 1] = cy + y;
                top[i * 2] = cx + x * 0.72f - R * 0.14f; top[i * 2 + 1] = cy + y * 0.66f - R * 0.24f;
                hi[i * 2] = cx + x * 0.36f - R * 0.3f; hi[i * 2 + 1] = cy + y * 0.3f - R * 0.38f;
            }
            cv.Poly(Pal.St1, pts);
            cv.Poly(Pal.St2, top);
            cv.Poly(Pal.St3, hi);
            if (size > 0)
            {
                int kx = (int)(cx + r.Sym(R * 0.3f)), ky = (int)(cy - R * 0.2f);
                cv.Line(kx, ky, kx + r.Range(1, 3), ky + r.Range(2, (int)R), Pal.St0);
                cv.Set((int)(cx - R * 0.45f), (int)(cy - R * 0.45f), Pal.St4);
            }
            if (mossy)
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        if (cv.Solid(x, y) && !cv.Solid(x, y - 1) && Noise.H(x, y, 77) < 0.75f)
                        {
                            cv.Set(x, y, Pal.Lf3);
                            if (Noise.H(x, y, 78) < 0.5f && cv.Solid(x, y + 1)) cv.Set(x, y + 1, Pal.Lf2);
                        }
            }
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim((int)cx, (int)(cy + R * 0.72f) + 1);
        }

        public static Sprite GrassTuft(Rng r, int light, int mid, bool tall)
        {
            int W = tall ? 12 : 9, H = tall ? 14 : 8;
            Cv cv = new Cv(W, H);
            int blades = tall ? 6 : 4;
            for (int i = 0; i < blades; i++)
            {
                int bx = 1 + r.Int(W - 2);
                int h = tall ? r.Range(6, 12) : r.Range(3, 6);
                int lean = r.Int(3) - 1;
                for (int k = 0; k < h; k++)
                {
                    int x = bx + (k > h / 2 ? lean : 0);
                    cv.Set(x, H - 1 - k, k >= h - 2 ? light : mid);
                }
            }
            return cv.ToSpriteTrim(W / 2, H);
        }

        public static Sprite Flower(Rng r, int petal, int center)
        {
            Cv cv = new Cv(7, 9);
            int h = r.Range(3, 5);
            for (int k = 0; k < h; k++) cv.Set(3, 8 - k, Pal.Gr4);
            if (r.Chance(0.6f)) cv.Set(2, 8 - h / 2, Pal.Gr4);
            int y = 8 - h;
            cv.Set(3, y, center); cv.Set(2, y, petal); cv.Set(4, y, petal); cv.Set(3, y - 1, petal);
            if (r.Chance(0.5f)) cv.Set(3, y + 1, petal);
            return cv.ToSpriteTrim(3, 9);
        }

        /// <summary>The glowing night-flower used by the herbalist's quest.</summary>
        public static Sprite Moonpetal(bool picked)
        {
            Cv cv = new Cv(13, 15);
            int g = Pal.Lf4, gd = Pal.Lf3;
            cv.Line(6, 14, 6, 7, g); cv.Line(6, 11, 3, 9, gd); cv.Line(6, 12, 9, 10, gd);
            cv.Set(2, 9, g); cv.Set(10, 10, g);
            if (!picked)
            {
                int a = Col.E(Pal.Gl5), b = Col.E(Pal.Gl6), c = Col.E(Pal.Fr3);
                cv.Ascii(2, 1, new[]
                {
                    "...a.a...",
                    "..abbba..",
                    ".abbcbba.",
                    "..abcba..",
                    ".a.bbb.a.",
                    "....a....",
                }, Cv.Map('a', a, 'b', b, 'c', c));
            }
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(6, 15);
        }

        public static Sprite Mushroom(Rng r, bool glowing)
        {
            Cv cv = new Cv(9, 8);
            int cap = glowing ? Col.E(Pal.Fr2) : Pal.Cr2, dot = glowing ? Col.E(Pal.Fr4) : Pal.Bn3;
            cv.Rect(4, 4, 1, 3, Pal.Bn2);
            cv.Rect(2, 3, 5, 1, cap); cv.Rect(3, 2, 3, 1, cap); cv.Set(3, 2, dot); cv.Set(5, 3, dot);
            if (r.Chance(0.6f)) { cv.Rect(7, 5, 1, 2, Pal.Bn2); cv.Rect(6, 4, 3, 1, cap); }
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(4, 8);
        }

        public static Sprite Reeds(Rng r)
        {
            Cv cv = new Cv(12, 20);
            for (int i = 0; i < 5; i++)
            {
                int bx = 1 + r.Int(10), h = r.Range(9, 17), lean = r.Int(3) - 1;
                for (int k = 0; k < h; k++) cv.Set(bx + (k > h * 2 / 3 ? lean : 0), 19 - k, k > h - 4 && (i & 1) == 0 ? Pal.Ea3 : Pal.Ol2);
            }
            return cv.ToSpriteTrim(6, 20);
        }

        public static Sprite Stump(Rng r)
        {
            Cv cv = new Cv(16, 13);
            cv.Ell(8, 8, 5.5f, 3.4f, Pal.Ea2);
            cv.Rect(3, 5, 10, 4, Pal.Ea2);
            cv.Ell(8, 5, 5.5f, 3f, Pal.Ea5);
            cv.Ell(8, 5, 3.4f, 1.7f, Pal.Ea4);
            cv.Set(8, 5, Pal.Ea3);
            cv.Rect(3, 6, 1, 4, Pal.Ea3); cv.Rect(12, 6, 1, 4, Pal.Ea1);
            cv.Set(2, 10, Pal.Ea2); cv.Set(13, 10, Pal.Ea1);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(8, 12);
        }

        public static Sprite Log(Rng r)
        {
            Cv cv = new Cv(28, 13);
            cv.Rect(4, 4, 19, 6, Pal.Ea2);
            cv.Rect(4, 4, 19, 1, Pal.Ea4); cv.Rect(4, 5, 19, 1, Pal.Ea3); cv.Rect(4, 9, 19, 1, Pal.Ea1);
            cv.Ell(23, 7, 2.6f, 3.2f, Pal.Ea5); cv.Set(23, 7, Pal.Ea3);
            cv.Ell(4, 7, 2f, 3.2f, Pal.Ea1);
            for (int i = 0; i < 4; i++) cv.Set(7 + i * 4, 7, Pal.Ea1);
            cv.Set(10, 3, Pal.Lf3); cv.Set(11, 3, Pal.Lf3); cv.Set(15, 3, Pal.Lf4);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(14, 12);
        }

        public static Sprite LilyPad(Rng r)
        {
            Cv cv = new Cv(9, 6);
            cv.Ell(4.5f, 3f, 3.6f, 2.2f, Pal.Lf3);
            cv.Set(4, 3, Pal.Wa1); cv.Set(5, 3, Pal.Wa1); cv.Set(6, 3, Pal.Wa1);
            cv.Set(2, 2, Pal.Lf4); cv.Set(3, 1, Pal.Lf4);
            if (r.Chance(0.35f)) cv.Set(5, 1, Pal.Gl5);
            return cv.ToSpriteTrim(4, 3);
        }
    }
}
