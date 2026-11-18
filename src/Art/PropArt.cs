using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>Small world furniture: village clutter, light sources, shrines, chests and dungeon dressing.</summary>
    public static class PropArt
    {
        // fire
        /// <summary>Four animated frames of a flame. kind: 0 ember, 1 gloam (violet), 2 frost (cyan).</summary>
        public static Cv[] FlameCv(int w, int h, int kind)
        {
            int[][] ramps =
            {
                new[]{ Pal.Em3, Pal.Em4, Pal.Em5, Pal.Em6 },
                new[]{ Pal.Gl2, Pal.Gl3, Pal.Gl4, Pal.Gl6 },
                new[]{ Pal.Fr1, Pal.Fr2, Pal.Fr3, Pal.Fr4 },
            };
            int[] rp = ramps[kind];
            Cv[] fr = new Cv[4];
            for (int f = 0; f < 4; f++)
            {
                Cv cv = new Cv(w, h);
                for (int y = 0; y < h; y++)
                {
                    float ny = (h - 1 - y) / (float)(h - 1);
                    float hw = (w * 0.5f) * M.Sqrt(1f - ny) * (0.72f + 0.28f * M.Sin(ny * 3.4f + 0.4f));
                    float wob = M.Sin(ny * 4.6f + f * 1.571f) * ny * w * 0.2f;
                    if (hw < 0.45f) hw = 0.45f;
                    for (int x = 0; x < w; x++)
                    {
                        float d = Math.Abs(x + 0.5f - w * 0.5f - wob) / hw;
                        if (d > 1f) continue;
                        float heat = (1f - d) * (1f - ny * 0.82f) + (Noise.H(x, y + f * 9, 3) - 0.5f) * 0.12f;
                        int c = heat > 0.6f ? rp[3] : (heat > 0.36f ? rp[2] : (heat > 0.16f ? rp[1] : rp[0]));
                        cv.Set(x, y, Col.E(c));
                    }
                }
                fr[f] = cv;
            }
            return fr;
        }

        public static Sprite[] Flame(int w, int h, int kind)
        {
            Cv[] cvs = FlameCv(w, h, kind);
            Sprite[] s = new Sprite[4];
            for (int i = 0; i < 4; i++) s[i] = cvs[i].ToSprite(w / 2, h);
            return s;
        }

        static Sprite[] WithFlame(Cv baseCv, int ox, int oy, int fx, int fy, int fw, int fh, int kind)
        {
            Cv[] fl = FlameCv(fw, fh, kind);
            Sprite[] res = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                Cv c = baseCv.Copy();
                c.Draw(fl[i], fx - fw / 2, fy - fh);
                res[i] = c.ToSpriteTrim(ox, oy);
            }
            return res;
        }

        public static Sprite[] Campfire(bool lit)
        {
            Cv cv = new Cv(26, 30);
            int by = 26;
            // stone ring
            for (int i = 0; i < 9; i++)
            {
                float a = i * M.TAU / 9f;
                float x = 13 + M.Cos(a) * 9.5f, y = by - 4 + M.Sin(a) * 3.6f;
                cv.Ell(x, y, 2.2f, 1.7f, (i % 3 == 0) ? Pal.St3 : Pal.St2);
            }
            cv.Thick(6, by - 3, 20, by - 6, 3, 3, Pal.Ea2);
            cv.Thick(7, by - 6, 19, by - 3, 3, 3, Pal.Ea3);
            cv.Set(8, by - 6, Pal.Ea5); cv.Set(19, by - 6, Pal.Ea5);
            if (lit) { cv.Set(12, by - 5, Col.E(Pal.Em4)); cv.Set(14, by - 4, Col.E(Pal.Em3)); cv.Set(13, by - 5, Col.E(Pal.Em5)); }
            cv.Outline(Pal.Ink0);
            if (!lit) return new[] { cv.ToSpriteTrim(13, by) };
            return WithFlame(cv, 13, by, 13, by - 4, 11, 17, 0);
        }

        public static Sprite[] LanternPost(bool lit)
        {
            Cv cv = new Cv(16, 36);
            int by = 33;
            cv.Rect(6, 5, 2, by - 5, Pal.Ea2); cv.Rect(6, 5, 1, by - 5, Pal.Ea3);
            cv.Rect(5, by - 1, 4, 2, Pal.Ea1);
            cv.Rect(6, 4, 7, 2, Pal.Ea2); cv.Line(8, 9, 11, 6, Pal.Ea1);
            cv.Rect(11, 6, 1, 2, Pal.St2);
            cv.Rect(9, 8, 5, 1, Pal.St1); cv.Rect(9, 14, 5, 1, Pal.St1);
            cv.Rect(9, 9, 1, 5, Pal.St1); cv.Rect(13, 9, 1, 5, Pal.St1);
            Sprite[] res = new Sprite[lit ? 4 : 1];
            for (int f = 0; f < res.Length; f++)
            {
                Cv c = cv.Copy();
                for (int y = 9; y < 14; y++)
                    for (int x = 10; x < 13; x++)
                        c.Set(x, y, lit ? Col.E((y + f) % 4 == 0 ? Pal.Em6 : (y < 11 ? Pal.Em5 : Pal.Em4)) : Pal.Ink2);
                c.Outline(Pal.Ink0);
                res[f] = c.ToSpriteTrim(7, by + 1);
            }
            return res;
        }

        /// <summary>Iron brazier on a tripod. kind: 0 ember, 1 gloam.</summary>
        public static Sprite[] Brazier(bool lit, int kind)
        {
            Cv cv = new Cv(18, 34);
            int by = 30;
            cv.Line(5, by, 8, by - 10, Pal.St1); cv.Line(12, by, 9, by - 10, Pal.St1); cv.Rect(8, by - 10, 2, 9, Pal.St0);
            cv.Poly(Pal.St2, 3, by - 16, 15, by - 16, 12, by - 10, 6, by - 10);
            cv.Rect(3, by - 17, 12, 2, Pal.St3); cv.Rect(3, by - 17, 12, 1, Pal.St4);
            cv.Rect(5, by - 17, 8, 1, lit ? Col.E(kind == 0 ? Pal.Em3 : Pal.Gl2) : Pal.Ink1);
            cv.Outline(Pal.Ink0);
            if (!lit) return new[] { cv.ToSpriteTrim(9, by + 1) };
            return WithFlame(cv, 9, by + 1, 9, by - 16, 9, 13, kind);
        }

        public static Sprite[] WallTorch(int kind)
        {
            Cv cv = new Cv(11, 24);
            cv.Rect(5, 12, 2, 8, Pal.Ea2); cv.Rect(4, 11, 4, 2, Pal.St2); cv.Rect(3, 19, 6, 1, Pal.St1); cv.Rect(5, 20, 2, 2, Pal.St1);
            cv.Outline(Pal.Ink0);
            return WithFlame(cv, 5, 22, 6, 12, 7, 10, kind);
        }

        public static Sprite[] CandleStand()
        {
            Cv cv = new Cv(11, 30);
            int by = 27;
            cv.Rect(5, 10, 1, by - 10, Pal.Go2); cv.Rect(3, by - 1, 5, 2, Pal.Go1); cv.Rect(3, 10, 5, 1, Pal.Go3);
            cv.Rect(3, 7, 1, 3, Pal.Bn3); cv.Rect(5, 6, 1, 4, Pal.Bn3); cv.Rect(7, 7, 1, 3, Pal.Bn3);
            cv.Outline(Pal.Ink0);
            Sprite[] res = new Sprite[4];
            for (int f = 0; f < 4; f++)
            {
                Cv c = cv.Copy();
                c.Set(3, 5 + (f == 1 ? 0 : 1), Col.E(Pal.Em5)); c.Set(3, 6, Col.E(Pal.Em6));
                c.Set(5, 4 + (f == 2 ? 0 : 1), Col.E(Pal.Em5)); c.Set(5, 5, Col.E(Pal.Em6));
                c.Set(7, 5 + (f == 3 ? 0 : 1), Col.E(Pal.Em5)); c.Set(7, 6, Col.E(Pal.Em6));
                res[f] = c.ToSpriteTrim(5, by + 1);
            }
            return res;
        }

        /// <summary>Wayshrine: a roofed stone niche with a small vesper bell and candles. Lit once attuned.</summary>
        public static Sprite[] Shrine(bool lit)
        {
            Cv cv = new Cv(30, 44);
            int by = 40;
            StructureArt.StoneBlocks(cv, 5, by - 8, 20, 9, 55, Pal.St1, Pal.St2, Pal.St3, Pal.St0, 7, 4);
            cv.Rect(4, by - 9, 22, 2, Pal.St4);
            cv.Rect(6, by - 29, 3, 20, Pal.St2); cv.Rect(6, by - 29, 1, 20, Pal.St4);
            cv.Rect(21, by - 29, 3, 20, Pal.St2); cv.Rect(23, by - 29, 1, 20, Pal.St0);
            cv.Rect(9, by - 28, 12, 19, Pal.Ink1);
            cv.Poly(Pal.St1, 15, by - 39, 28, by - 28, 2, by - 28);
            cv.Poly(Pal.St3, 15, by - 39, 15, by - 28, 2, by - 28);
            cv.Rect(2, by - 29, 26, 2, Pal.St3); cv.Rect(2, by - 28, 26, 1, Pal.St0);
            // bell
            cv.Rect(14, by - 27, 2, 3, Pal.Go1);
            int[] bm = Cv.Map('a', Pal.Go3, 'b', Pal.Go2, 'c', Pal.Go1, 'l', Pal.Go4);
            cv.Ascii(11, by - 24, new[] { "..lab...", ".laabc..", ".laabc..", "llaabbc.", "laaabbcc", "...c...." }, bm);
            cv.Outline(Pal.Ink0);
            Sprite[] res = new Sprite[lit ? 4 : 1];
            for (int f = 0; f < res.Length; f++)
            {
                Cv c = cv.Copy();
                c.Rect(10, by - 12, 1, 3, Pal.Bn3); c.Rect(19, by - 13, 1, 4, Pal.Bn3); c.Rect(12, by - 11, 1, 2, Pal.Bn2);
                if (lit)
                {
                    c.Set(10, by - 13 - (f == 1 ? 1 : 0), Col.E(Pal.Em5)); c.Set(10, by - 13, Col.E(Pal.Em6));
                    c.Set(19, by - 14 - (f == 2 ? 1 : 0), Col.E(Pal.Em5)); c.Set(19, by - 14, Col.E(Pal.Em6));
                    c.Set(12, by - 12, Col.E(Pal.Em5));
                    c.Set(14, by - 20, Col.E(Pal.Em6)); c.Set(13, by - 22, Col.E(Pal.Go4));
                }
                res[f] = c.ToSpriteTrim(15, by + 1);
            }
            return res;
        }

        // containers
        public static Sprite Chest(bool ornate, bool open)
        {
            Cv cv = new Cv(22, 22);
            int by = 19;
            int wood = ornate ? Pal.Gl1 : Pal.Ea3, woodD = ornate ? Pal.Gl0 : Pal.Ea2, woodL = ornate ? Pal.Gl2 : Pal.Ea4;
            int band = ornate ? Pal.Go3 : Pal.St3, bandD = ornate ? Pal.Go1 : Pal.St1;
            cv.Rect(3, by - 8, 16, 9, wood);
            cv.Rect(3, by - 8, 16, 1, woodD);
            cv.Rect(3, by - 1, 16, 1, woodD);
            cv.Rect(3, by - 8, 2, 9, band); cv.Rect(17, by - 8, 2, 9, bandD); cv.Rect(10, by - 8, 2, 9, band);
            if (!open)
            {
                cv.Rect(3, by - 14, 16, 6, woodL);
                cv.Rect(4, by - 15, 14, 1, woodL);
                cv.Rect(3, by - 14, 2, 6, band); cv.Rect(17, by - 14, 2, 6, bandD); cv.Rect(10, by - 15, 2, 7, band);
                cv.Rect(3, by - 9, 16, 1, bandD);
                cv.Rect(10, by - 9, 2, 3, ornate ? Pal.Go4 : Pal.St5);
            }
            else
            {
                cv.Rect(4, by - 11, 14, 3, Pal.Ink1);
                cv.Rect(3, by - 19, 16, 8, woodD);
                cv.Rect(3, by - 19, 16, 1, woodL);
                cv.Rect(3, by - 19, 2, 8, band); cv.Rect(17, by - 19, 2, 8, bandD); cv.Rect(10, by - 19, 2, 8, band);
                cv.Set(7, by - 10, Col.E(Pal.Go4)); cv.Set(13, by - 10, Col.E(Pal.Go4)); cv.Set(9, by - 9, Pal.Go3);
            }
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(11, by + 1);
        }

        public static Sprite Barrel()
        {
            Cv cv = new Cv(16, 20);
            for (int y = 3; y < 17; y++)
            {
                int bulge = (y > 5 && y < 14) ? 1 : 0;
                for (int x = 3 - bulge; x < 13 + bulge; x++)
                {
                    int c = x < 5 ? Pal.Ea4 : (x > 10 ? Pal.Ea2 : Pal.Ea3);
                    if ((x & 3) == 1) c = Col.Shade(c, -1);
                    cv.Set(x, y, c);
                }
            }
            cv.Rect(2, 5, 12, 1, Pal.St3); cv.Rect(2, 13, 12, 1, Pal.St2);
            cv.Ell(8, 3.5f, 5f, 1.8f, Pal.Ea5); cv.Ell(8, 3.5f, 3.4f, 1f, Pal.Ea4);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(8, 17);
        }

        public static Sprite Crate()
        {
            Cv cv = new Cv(18, 19);
            cv.Rect(2, 5, 14, 11, Pal.Ea4);
            cv.Rect(2, 2, 14, 4, Pal.Ea5);
            cv.Rect(2, 5, 14, 1, Pal.Ea2);
            cv.Rect(2, 5, 1, 11, Pal.Ea2); cv.Rect(15, 5, 1, 11, Pal.Ea2); cv.Rect(2, 15, 14, 1, Pal.Ea2);
            cv.Line(3, 6, 14, 14, Pal.Ea3); cv.Line(14, 6, 3, 14, Pal.Ea3);
            cv.Rect(2, 2, 14, 1, Pal.Ea6);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(9, 16);
        }

        public static Sprite Urn(bool stone)
        {
            Cv cv = new Cv(13, 17);
            int a = stone ? Pal.St3 : Pal.Ea5, b = stone ? Pal.St2 : Pal.Ea4, c = stone ? Pal.St1 : Pal.Ea3, l = stone ? Pal.St5 : Pal.Ea6;
            cv.Ell(6.5f, 9.5f, 4.6f, 4.6f, b);
            cv.Ell(5.5f, 8.5f, 3f, 3.2f, a);
            cv.Rect(4, 3, 5, 3, b); cv.Rect(3, 2, 7, 1, a); cv.Rect(4, 14, 5, 1, c);
            cv.Rect(2, 9, 9, 1, stone ? Pal.Gl1 : Pal.Cr1);
            cv.Set(4, 7, l); cv.Set(4, 8, l);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(6, 15);
        }

        public static Sprite Sack()
        {
            Cv cv = new Cv(14, 13);
            cv.Ell(7, 8, 5, 4, Pal.Ea5); cv.Ell(6, 7, 3.4f, 2.6f, Pal.Ea6);
            cv.Rect(6, 2, 3, 2, Pal.Ea5); cv.Rect(5, 4, 5, 1, Pal.Ea2);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(7, 12);
        }

        public static Sprite Cart()
        {
            Cv cv = new Cv(42, 26);
            cv.Rect(6, 8, 26, 8, Pal.Ea3); cv.Rect(6, 8, 26, 1, Pal.Ea5); cv.Rect(6, 15, 26, 1, Pal.Ea1);
            for (int x = 9; x < 32; x += 5) cv.Rect(x, 9, 1, 6, Pal.Ea2);
            cv.Line(32, 13, 40, 18, Pal.Ea2); cv.Line(32, 14, 40, 19, Pal.Ea2);
            cv.Circle(13, 18, 5.2f, Pal.Ea1); cv.Circle(13, 18, 3.6f, Pal.Ea3); cv.Circle(13, 18, 1.3f, Pal.St2);
            cv.Line(13, 14, 13, 22, Pal.Ea1); cv.Line(9, 18, 17, 18, Pal.Ea1);
            cv.Ell(15, 6, 6, 3, Pal.Ea5); cv.Ell(24, 6, 5, 2.6f, Pal.Ea6);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(18, 24);
        }

        public static Sprite HayBale()
        {
            Cv cv = new Cv(20, 15);
            cv.Rect(2, 4, 16, 9, Pal.Go2); cv.Rect(2, 3, 16, 2, Pal.Go3);
            for (int i = 0; i < 26; i++) { int x = 2 + (i * 7) % 16, y = 4 + (i * 5) % 9; cv.Set(x, y, (i & 1) == 0 ? Pal.Go4 : Pal.Go1); }
            cv.Rect(6, 3, 1, 10, Pal.Ea2); cv.Rect(13, 3, 1, 10, Pal.Ea2);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(10, 14);
        }

        public static Sprite Woodpile()
        {
            Cv cv = new Cv(24, 16);
            int[] xs = { 4, 9, 14, 19, 6, 11, 16, 9, 14 };
            int[] ys = { 11, 11, 11, 11, 7, 7, 7, 3, 3 };
            for (int i = 0; i < xs.Length; i++)
            {
                cv.Circle(xs[i] + 0.5f, ys[i] + 0.5f, 2.6f, Pal.Ea2);
                cv.Circle(xs[i] + 0.5f, ys[i] + 0.5f, 1.7f, Pal.Ea5);
                cv.Set(xs[i], ys[i], Pal.Ea3);
            }
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(12, 15);
        }

        public static Sprite Anvil()
        {
            Cv cv = new Cv(20, 18);
            cv.Ell(10, 13, 5, 3, Pal.Ea2); cv.Rect(5, 10, 10, 4, Pal.Ea2); cv.Ell(10, 10, 5, 2.4f, Pal.Ea4);
            int[] m = Cv.Map('a', Pal.St2, 'b', Pal.St1, 'l', Pal.St5, 'd', Pal.St0);
            cv.Ascii(3, 2, new[] { "llllllllllll..", "aaaaaaaaaaaaal", ".baaaaaaaaab..", "...baaaab.....", "...baaaab.....", "..bbaaaabb....", "..dddddddd...." }, m);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(10, 16);
        }

        public static Sprite Signpost(int dir)
        {
            Cv cv = new Cv(22, 28);
            cv.Rect(10, 6, 2, 19, Pal.Ea2); cv.Rect(10, 6, 1, 19, Pal.Ea3);
            cv.Rect(4, 7, 14, 5, Pal.Ea4); cv.Rect(4, 7, 14, 1, Pal.Ea5); cv.Rect(4, 11, 14, 1, Pal.Ea2);
            if (dir >= 0) { cv.Set(18, 8, Pal.Ea4); cv.Set(18, 9, Pal.Ea4); cv.Set(18, 10, Pal.Ea4); cv.Set(19, 9, Pal.Ea4); }
            else { cv.Set(3, 8, Pal.Ea4); cv.Set(3, 9, Pal.Ea4); cv.Set(3, 10, Pal.Ea4); cv.Set(2, 9, Pal.Ea4); }
            cv.Rect(6, 9, 3, 1, Pal.Ea1); cv.Rect(10, 9, 5, 1, Pal.Ea1);
            cv.Rect(6, 14, 11, 4, Pal.Ea4); cv.Rect(6, 17, 11, 1, Pal.Ea2); cv.Rect(8, 15, 6, 1, Pal.Ea1);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(11, 26);
        }

        public static Sprite NoticeBoard()
        {
            Cv cv = new Cv(28, 28);
            cv.Rect(4, 8, 2, 17, Pal.Ea2); cv.Rect(22, 8, 2, 17, Pal.Ea1);
            cv.Rect(3, 5, 22, 13, Pal.Ea3); cv.Rect(3, 5, 22, 1, Pal.Ea5); cv.Rect(3, 17, 22, 1, Pal.Ea1);
            cv.Poly(Pal.Ea2, 14, 0, 27, 5, 1, 5);
            cv.Rect(5, 7, 6, 7, Pal.Bn3); cv.Rect(6, 9, 4, 1, Pal.Bn0); cv.Rect(6, 11, 3, 1, Pal.Bn0);
            cv.Rect(13, 8, 5, 6, Pal.Bn2); cv.Rect(14, 10, 3, 1, Pal.Bn0);
            cv.Rect(19, 7, 4, 5, Pal.Bn3); cv.Set(20, 9, Pal.Cr2); cv.Set(21, 9, Pal.Cr2);
            cv.Set(7, 7, Pal.Cr3); cv.Set(15, 8, Pal.Cr3);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(14, 26);
        }

        public static Sprite FenceH()
        {
            Cv cv = new Cv(18, 14);
            cv.Rect(1, 3, 2, 9, Pal.Ea2); cv.Rect(1, 3, 1, 9, Pal.Ea4);
            cv.Rect(0, 5, 18, 2, Pal.Ea3); cv.Rect(0, 5, 18, 1, Pal.Ea4);
            cv.Rect(0, 9, 18, 1, Pal.Ea2);
            cv.Set(2, 2, Pal.Ea3);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(1, 13);
        }

        public static Sprite FenceV()
        {
            Cv cv = new Cv(8, 26);
            cv.Rect(3, 2, 2, 22, Pal.Ea2); cv.Rect(3, 2, 1, 22, Pal.Ea4);
            cv.Rect(2, 8, 4, 2, Pal.Ea3); cv.Rect(2, 18, 4, 2, Pal.Ea3);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(4, 25);
        }

        /// <summary>Sharpened log palisade segment (16 px wide).</summary>
        public static Sprite Palisade(Rng r)
        {
            Cv cv = new Cv(18, 34);
            for (int i = 0; i < 3; i++)
            {
                int x = 1 + i * 5, top = 4 + r.Int(4);
                for (int y = top; y < 31; y++)
                    for (int k = 0; k < 5; k++)
                    {
                        if (y - top < 3 && (k < 2 - (y - top) || k > 2 + (y - top))) continue;
                        cv.Set(x + k, y, k == 0 ? Pal.Ea4 : (k == 4 ? Pal.Ea1 : (k == 2 && y % 6 == 0 ? Pal.Ea2 : Pal.Ea3)));
                    }
            }
            cv.Rect(1, 14, 16, 1, Pal.Ea1); cv.Rect(1, 24, 16, 1, Pal.Ea1);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(9, 32);
        }

        public static Sprite Bench()
        {
            Cv cv = new Cv(24, 13);
            cv.Rect(2, 4, 20, 3, Pal.Ea4); cv.Rect(2, 4, 20, 1, Pal.Ea5); cv.Rect(2, 6, 20, 1, Pal.Ea2);
            cv.Rect(4, 7, 2, 4, Pal.Ea2); cv.Rect(18, 7, 2, 4, Pal.Ea2);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(12, 12);
        }

        public static Sprite Banner(int cloth)
        {
            Cv cv = new Cv(18, 44);
            int by = 41;
            cv.Rect(3, 2, 2, by - 2, Pal.Ea2); cv.Rect(3, 2, 1, by - 2, Pal.Ea4); cv.Rect(2, 1, 4, 2, Pal.Go3);
            cv.Rect(5, 4, 10, 1, Pal.Ea2);
            int cd = Col.Shade(cloth, -1);
            for (int y = 5; y < 26; y++)
                for (int x = 6; x < 15; x++)
                {
                    if (y > 21 && Math.Abs(x - 10) < (y - 21)) continue;
                    cv.Set(x, y, (x > 12) ? cd : cloth);
                }
            int[] m = Cv.Map('g', Pal.Go3, 'h', Pal.Go4);
            cv.Ascii(7, 9, new[] { "..hg...", ".hggg..", ".hggg..", "hggggg.", "...g...", "..g.g.." }, m);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(4, by + 1);
        }

        public static Sprite Well()
        {
            Cv cv = new Cv(36, 44);
            int by = 40;
            cv.Ell(18, by - 6, 13, 6, Pal.St1);
            StructureArt.StoneBlocks(cv, 5, by - 12, 26, 7, 71, Pal.St1, Pal.St2, Pal.St3, Pal.St0, 7, 4);
            cv.Ell(18, by - 12, 13, 5, Pal.St3);
            cv.Ell(18, by - 12, 10, 3.4f, Pal.Wa0);
            cv.Ell(16, by - 12, 4, 1.4f, Pal.Wa2);
            cv.Rect(6, by - 34, 2, 23, Pal.Ea2); cv.Rect(28, by - 34, 2, 23, Pal.Ea1);
            cv.Poly(Pal.Ea3, 18, by - 42, 34, by - 33, 2, by - 33);
            cv.Poly(Pal.Ea4, 18, by - 42, 18, by - 33, 2, by - 33);
            cv.Rect(2, by - 34, 32, 2, Pal.Ea2);
            cv.Rect(8, by - 30, 20, 1, Pal.Ea2);
            cv.Rect(18, by - 30, 1, 10, Pal.Bn1);
            cv.Rect(16, by - 21, 5, 4, Pal.Ea3); cv.Rect(16, by - 21, 5, 1, Pal.St3);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(18, by + 1);
        }

        public static Sprite Stall(int stripeA, int stripeB)
        {
            Cv cv = new Cv(44, 40);
            int by = 37;
            cv.Rect(4, 10, 2, by - 10, Pal.Ea2); cv.Rect(38, 10, 2, by - 10, Pal.Ea1);
            cv.Rect(4, by - 13, 36, 9, Pal.Ea3); cv.Rect(4, by - 13, 36, 2, Pal.Ea5); cv.Rect(4, by - 5, 36, 1, Pal.Ea1);
            for (int x = 7; x < 40; x += 6) cv.Rect(x, by - 11, 1, 7, Pal.Ea2);
            // goods
            cv.Ell(11, by - 15, 3, 2, Pal.Cr3); cv.Ell(19, by - 15, 3, 2, Pal.Lf4); cv.Ell(27, by - 15, 2.4f, 2, Pal.Em4); cv.Rect(32, by - 18, 4, 5, Pal.Gl3);
            cv.Set(33, by - 19, Pal.Bn3);
            // awning
            for (int y = 0; y < 11; y++)
                for (int x = 0; x < 42; x++)
                {
                    int xx = 1 + x;
                    if (y < 2 && (x < 2 - y || x > 39 + y)) continue;
                    bool a = ((x + y / 3) / 6) % 2 == 0;
                    int c = a ? stripeA : stripeB;
                    if (y > 7) c = Col.Shade(c, -1);
                    if (y == 10 && (x % 6) > 2) continue;
                    cv.Set(xx, 3 + y, c);
                }
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(22, by + 1);
        }

        public static Sprite Bedroll()
        {
            Cv cv = new Cv(22, 10);
            cv.Rect(2, 3, 18, 5, Pal.Ol2); cv.Rect(2, 3, 18, 1, Pal.Ol3); cv.Rect(2, 7, 18, 1, Pal.Ol0);
            cv.Ell(4, 5.5f, 2.6f, 2.8f, Pal.Ol3); cv.Set(4, 5, Pal.Ol1);
            cv.Rect(14, 3, 1, 5, Pal.Ea2);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(11, 9);
        }

        // dungeon
        public static Sprite Sarcophagus(Rng r, bool open)
        {
            Cv cv = new Cv(42, 30);
            int by = 26;
            StructureArt.StoneBlocks(cv, 4, by - 12, 34, 13, 81, Pal.St1, Pal.St2, Pal.St2, Pal.St0, 17, 13);
            cv.Rect(4, by - 12, 34, 1, Pal.St0);
            if (!open)
            {
                cv.Rect(3, by - 19, 36, 8, Pal.St3); cv.Rect(3, by - 19, 36, 1, Pal.St5); cv.Rect(3, by - 12, 36, 1, Pal.St1);
                // effigy
                cv.Ell(11, by - 15.5f, 3, 2.4f, Pal.St4); cv.Rect(14, by - 17, 20, 4, Pal.St4); cv.Rect(14, by - 14, 20, 1, Pal.St2);
                cv.Rect(22, by - 18, 2, 6, Pal.St2);
            }
            else
            {
                cv.Rect(5, by - 17, 32, 5, Pal.Ink0);
                cv.Rect(3, by - 18, 36, 1, Pal.St4); cv.Rect(3, by - 17, 2, 6, Pal.St3); cv.Rect(37, by - 17, 2, 6, Pal.St1);
                // lid slid aside
                cv.Poly(Pal.St3, 20, by - 23, 41, by - 21, 41, by - 16, 22, by - 18);
                cv.Rect(8, by - 15, 5, 2, Pal.Bn2); cv.Set(9, by - 15, Pal.Ink0);
            }
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(21, by + 1);
        }

        public static Sprite Pew()
        {
            Cv cv = new Cv(34, 18);
            cv.Rect(2, 3, 30, 7, Pal.Ea2); cv.Rect(2, 3, 30, 1, Pal.Ea4);
            cv.Rect(2, 10, 30, 3, Pal.Ea3); cv.Rect(2, 10, 30, 1, Pal.Ea4);
            cv.Rect(2, 13, 2, 3, Pal.Ea1); cv.Rect(30, 13, 2, 3, Pal.Ea1); cv.Rect(16, 13, 2, 3, Pal.Ea1);
            for (int x = 6; x < 30; x += 7) cv.Rect(x, 4, 1, 6, Pal.Ea1);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(17, 17);
        }

        public static Sprite Bookshelf(Rng r)
        {
            Cv cv = new Cv(36, 38);
            int by = 35;
            cv.Rect(2, 3, 32, by - 2, Pal.Ea2);
            cv.Rect(2, 3, 32, 2, Pal.Ea4);
            cv.Rect(2, 3, 2, by - 2, Pal.Ea3); cv.Rect(32, 3, 2, by - 2, Pal.Ea1);
            int[] spines = { Pal.Cr1, Pal.Gl1, Pal.Lf2, Pal.Ea4, Pal.St2, Pal.Go1, Pal.Cr2, Pal.Gl2 };
            for (int s = 0; s < 3; s++)
            {
                int sy = 6 + s * 10;
                cv.Rect(4, sy, 28, 8, Pal.Ink1);
                int x = 4;
                while (x < 31)
                {
                    int w = r.Range(2, 3), h = r.Range(5, 8);
                    if (r.Chance(0.14f)) { x += w + 1; continue; }
                    int c = spines[r.Int(spines.Length)];
                    cv.Rect(x, sy + 8 - h, w, h, c);
                    cv.Set(x, sy + 8 - h + 1, Col.Shade(c, 2));
                    x += w;
                }
                cv.Rect(4, sy + 8, 28, 1, Pal.Ea4);
            }
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(18, by + 1);
        }

        public static Sprite[] Altar()
        {
            Cv cv = new Cv(44, 30);
            int by = 26;
            StructureArt.StoneBlocks(cv, 5, by - 11, 34, 12, 85, Pal.St1, Pal.St2, Pal.St3, Pal.St0, 9, 5);
            cv.Rect(3, by - 15, 38, 5, Pal.St3); cv.Rect(3, by - 15, 38, 1, Pal.St5);
            cv.Rect(14, by - 15, 16, 12, Pal.Cr1); cv.Rect(14, by - 15, 16, 1, Pal.Cr2); cv.Rect(15, by - 4, 14, 1, Pal.Go2);
            cv.Rect(21, by - 12, 2, 6, Pal.Go3); cv.Ell(22, by - 12.5f, 2.4f, 2.4f, Pal.Go3); cv.Ell(22, by - 12.5f, 1f, 1f, Pal.Cr1);
            cv.Rect(7, by - 20, 1, 5, Pal.Bn3); cv.Rect(36, by - 21, 1, 6, Pal.Bn3);
            cv.Outline(Pal.Ink0);
            Sprite[] res = new Sprite[4];
            for (int f = 0; f < 4; f++)
            {
                Cv c = cv.Copy();
                c.Set(7, by - 21 - (f == 1 ? 1 : 0), Col.E(Pal.Gl4)); c.Set(7, by - 21, Col.E(Pal.Gl6));
                c.Set(36, by - 22 - (f == 3 ? 1 : 0), Col.E(Pal.Gl4)); c.Set(36, by - 22, Col.E(Pal.Gl6));
                res[f] = c.ToSpriteTrim(22, by + 1);
            }
            return res;
        }

        public static Sprite Bones(Rng r)
        {
            Cv cv = new Cv(18, 11);
            cv.Ell(5, 5, 3, 2.6f, Pal.Bn3); cv.Set(4, 5, Pal.Ink1); cv.Set(6, 5, Pal.Ink1); cv.Rect(4, 7, 3, 1, Pal.Bn1);
            cv.Line(9, 7, 15, 4, Pal.Bn2); cv.Line(10, 4, 15, 8, Pal.Bn3);
            cv.Set(9, 8, Pal.Bn3); cv.Set(16, 3, Pal.Bn3); cv.Set(12, 8, Pal.Bn1);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(9, 10);
        }

        public static Sprite Cobweb()
        {
            Cv cv = new Cv(16, 16);
            int c = Col.WithA(Pal.St5, 110);
            for (int i = 0; i < 5; i++) cv.Line(0, 0, 15 - i * 3, i * 3 + (i == 0 ? 0 : 3), c);
            for (int k = 4; k < 16; k += 4) for (int i = 0; i < k; i++) cv.Set(k - i, i, c);
            return cv.ToSprite(0, 0);
        }

        /// <summary>Iron portcullis. open = raised.</summary>
        public static Sprite Gate(bool open)
        {
            Cv cv = new Cv(36, 40);
            int by = 37;
            int bottom = open ? 12 : by;
            for (int x = 3; x < 34; x += 4) { cv.Rect(x, 3, 2, bottom - 3, Pal.St2); cv.Rect(x, 3, 1, bottom - 3, Pal.St4); if (!open) { cv.Set(x, by, Pal.St4); cv.Set(x + 1, by, Pal.St1); } else cv.Set(x, bottom, Pal.St4); }
            for (int y = 6; y < bottom; y += 8) cv.Rect(2, y, 32, 2, Pal.St1);
            cv.Rect(1, 1, 34, 3, Pal.St3); cv.Rect(1, 1, 34, 1, Pal.St5);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(18, by + 1);
        }

        public static Sprite Lever(bool on)
        {
            Cv cv = new Cv(14, 16);
            cv.Rect(3, 10, 8, 4, Pal.St2); cv.Rect(3, 10, 8, 1, Pal.St4); cv.Rect(6, 11, 2, 2, Pal.Ink0);
            if (on) { cv.Line(7, 11, 11, 3, Pal.Ea4); cv.Circle(11.5f, 2.5f, 1.6f, Col.E(Pal.Em5)); }
            else { cv.Line(7, 11, 3, 3, Pal.Ea4); cv.Circle(2.5f, 2.5f, 1.6f, Pal.Cr2); }
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(7, 15);
        }

        public static Sprite CrackedWall()
        {
            Cv cv = new Cv(18, 26);
            int c = Pal.Ink0;
            cv.Line(9, 1, 7, 7, c); cv.Line(7, 7, 11, 12, c); cv.Line(11, 12, 8, 18, c); cv.Line(8, 18, 10, 24, c);
            cv.Line(7, 7, 3, 10, c); cv.Line(11, 12, 15, 14, c); cv.Line(8, 18, 4, 21, c);
            cv.Set(10, 8, Col.E(Pal.Go3)); cv.Set(9, 14, Col.E(Pal.Go2));
            return cv.ToSprite(9, 25);
        }

        public static Sprite Rubble(Rng r)
        {
            Cv cv = new Cv(22, 13);
            for (int i = 0; i < 9; i++) cv.Ell(3 + r.Int(16), 6 + r.Int(5), r.F(1.4f, 3f), r.F(1.2f, 2.2f), r.Chance(0.5f) ? Pal.St2 : Pal.St1);
            cv.RimLight(1, 1);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(11, 12);
        }

        public static Sprite Stairs()
        {
            Cv cv = new Cv(40, 34);
            for (int i = 0; i < 7; i++)
            {
                int y = 30 - i * 4, inset = i;
                int c = Col.Mul(Pal.St3, 1f - i * 0.11f);
                cv.Rect(3 + inset, y - 3, 34 - inset * 2, 4, c);
                cv.Rect(3 + inset, y - 3, 34 - inset * 2, 1, Col.Mul(Pal.St5, 1f - i * 0.12f));
            }
            cv.Rect(10, 0, 20, 4, Pal.Ink0);
            cv.Rect(12, 1, 16, 2, Col.E(Col.Mul(Pal.Gl1, 0.8f)));
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(20, 32);
        }

        /// <summary>The fallen Vesper Bell. state: 0 silent, 1 rung (glowing).</summary>
        public static Sprite Bell(int state)
        {
            int W = 76, H = 82;
            Cv cv = new Cv(W, H);
            int cx = 38, by = 76;
            for (int y = 6; y <= by; y++)
            {
                float t = (y - 6) / (float)(by - 6);
                float sh = t < 0.2f ? M.Sqrt(1f - ((0.2f - t) / 0.2f) * ((0.2f - t) / 0.2f)) : 1f;   // rounded shoulder
                float flare = t > 0.8f ? M.Pow((t - 0.8f) / 0.2f, 1.6f) * 9f : 0f;                      // flared lip
                float hw = (17f + 6f * t) * (0.3f + 0.7f * sh) + flare;
                for (int x = -(int)hw; x <= (int)hw; x++)
                {
                    float u = x / hw;
                    int c = u < -0.6f ? Pal.Go4 : (u < -0.15f ? Pal.Go3 : (u < 0.45f ? Pal.Go2 : (u < 0.8f ? Pal.Go1 : Pal.Go0)));
                    if (state == 0) c = Col.Lerp(c, Pal.St1, 0.45f);
                    float n = Noise.Fbm((x + 50) * 0.12f, y * 0.12f, 601, 2);
                    if (n > 0.64f) c = Col.Lerp(c, Pal.Lf2, state == 0 ? 0.6f : 0.25f);     // verdigris
                    if (y == 30 || y == 31 || y == 62 || y == 63 || y == by - 2) c = Col.Shade(c, -1);
                    cv.Set(cx + x, y, c);
                }
            }
            // crown
            cv.Rect(cx - 5, 1, 10, 6, state == 0 ? Pal.St2 : Pal.Go2); cv.Rect(cx - 2, 2, 4, 3, Pal.Ink1);
            // the great crack
            int kx = cx + 7, ky = 20; Rng r = new Rng(602);
            for (int i = 0; i < 46; i++)
            {
                cv.Set(kx, ky, state == 0 ? Pal.Ink0 : Col.E(Pal.Em5));
                if (i % 9 == 4) cv.Set(kx + 1, ky, state == 0 ? Pal.Ink0 : Col.E(Pal.Em4));
                ky++; if (r.Chance(0.45f)) kx += r.Chance(0.6f) ? 1 : -1;
            }
            // rune band
            int g = state == 0 ? Pal.Ink3 : Col.E(Pal.Em6);
            for (int x = -18; x <= 18; x += 4) { cv.Set(cx + x, 46, g); cv.Set(cx + x + 1, 47, g); cv.Set(cx + x, 48, g); }
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(cx, by + 1);
        }

        public static Sprite SkullPile(Rng r)
        {
            Cv cv = new Cv(22, 14);
            int[] xs = { 4, 10, 16, 7, 13, 10 }; int[] ys = { 9, 9, 9, 5, 5, 2 };
            for (int i = 0; i < 6; i++)
            {
                cv.Ell(xs[i] + 1, ys[i] + 1.5f, 3f, 2.6f, i % 2 == 0 ? Pal.Bn3 : Pal.Bn2);
                cv.Set(xs[i], ys[i] + 1, Pal.Ink1); cv.Set(xs[i] + 2, ys[i] + 1, Pal.Ink1);
            }
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(11, 13);
        }

        public static Sprite WallBanner()
        {
            Cv cv = new Cv(14, 30);
            cv.Rect(1, 1, 12, 2, Pal.Go1);
            for (int y = 3; y < 28; y++)
                for (int x = 2; x < 12; x++)
                {
                    if (y > 21 && Noise.H(x, y, 13) < (y - 21) * 0.14f) continue;
                    cv.Set(x, y, x > 9 ? Pal.Cr0 : Pal.Cr1);
                }
            int[] m = Cv.Map('g', Pal.Go2);
            cv.Ascii(3, 7, new[] { "..gg...", ".gggg..", ".gggg..", "gggggg.", "...g...", "..g.g.." }, m);
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(7, 29);
        }

        public static Sprite Coffin()
        {
            Cv cv = new Cv(16, 32);
            cv.Poly(Pal.Ea2, 4, 2, 12, 2, 14, 9, 11, 29, 5, 29, 2, 9);
            cv.Poly(Pal.Ea3, 5, 3, 8, 3, 8, 28, 6, 28, 3, 9);
            cv.Rect(7, 8, 2, 12, Pal.Go1); cv.Ell(8, 7.5f, 2.2f, 2.2f, Pal.Go1); cv.Set(8, 7, Pal.Ea2);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(8, 31);
        }
    }
}
