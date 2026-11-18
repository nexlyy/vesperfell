using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>Buildings and the big set-piece architecture.</summary>
    public static class StructureArt
    {
        public static readonly int[] RoofSlate = { Pal.Ink3, Pal.St1, Pal.St2, Pal.St3 };
        public static readonly int[] RoofThatch = { Pal.Ea1, Pal.Ea3, Pal.Ea4, Pal.Ea5 };
        public static readonly int[] RoofTile = { Pal.Em0, Pal.Em1, Pal.Em2, Pal.Cr3 };
        public static readonly int[] RoofMoss = { Pal.Lf0, Pal.Lf1, Pal.Lf2, Pal.Lf3 };

        static int Dk(int c, float f) { return Col.Mul(c, f); }

        public static void StoneBlocks(Cv cv, int x0, int y0, int w, int h, int seed, int cDark, int cMid, int cLight, int mortar, int bw, int bh)
        {
            for (int y = 0; y < h; y++)
            {
                int row = y / bh, off = (row & 1) * (bw / 2) + (int)(Noise.H(row, seed, 5) * 3);
                for (int x = 0; x < w; x++)
                {
                    int lx = (x + off) % bw, ly = y % bh;
                    int c;
                    if (lx == bw - 1 || ly == bh - 1) c = mortar;
                    else
                    {
                        float hsh = Noise.H((x + off) / bw, row, seed);
                        c = hsh < 0.3f ? cDark : (hsh < 0.86f ? cMid : cLight);
                        if (ly == 0 && hsh > 0.4f) c = Col.Shade(c, 1);
                    }
                    cv.Set(x0 + x, y0 + y, c);
                }
            }
        }

        public static void Window(Cv cv, int x, int y, int w, int h, bool lit, int frame)
        {
            cv.Rect(x - 1, y - 1, w + 2, h + 2, frame);
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    int c;
                    if (lit) c = Col.E(j < h / 3 ? Pal.Em6 : (j < h * 2 / 3 ? Pal.Em5 : Pal.Em4));
                    else c = (i + j) % 5 == 0 ? Pal.St1 : Pal.Ink2;
                    cv.Set(x + i, y + j, c);
                }
            cv.Rect(x + w / 2, y, 1, h, frame);
            cv.Rect(x, y + h / 2, w, 1, frame);
            cv.Rect(x - 2, y + h + 1, w + 4, 1, Col.Shade(frame, 1));
        }

        public static void Door(Cv cv, int x, int y, int w, int h, int wood)
        {
            cv.Rect(x - 1, y - 1, w + 2, h + 1, Pal.Ea1);
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    int c = (i % 3 == 2) ? Col.Shade(wood, -1) : wood;
                    if (j == 3 || j == h - 4) c = Pal.St2;
                    cv.Set(x + i, y + j, c);
                }
            cv.Set(x + 1, y, Pal.Ea1); cv.Set(x + w - 2, y, Pal.Ea1); cv.Set(x, y, Pal.Ea1); cv.Set(x + w - 1, y, Pal.Ea1);
            cv.Set(x + w - 3, y + h / 2, Pal.Go3);
            cv.Rect(x - 2, y + h, w + 4, 2, Pal.St3);
            cv.Rect(x - 2, y + h + 1, w + 4, 1, Pal.St1);
        }

        /// <summary>
        /// A cottage: timbered plaster (style 0) or fieldstone (style 1) under a shingled roof.
        /// doorX / windows are offsets from the left wall edge; doorX &lt; 0 means no door.
        /// </summary>
        public static Sprite House(Rng r, int w, int wallH, int roofH, int[] roof, int style, int doorX, int[] windows, bool chimney, bool lit, int forgeX)
        {
            const int over = 5;
            int W = w + over * 2, H = roofH + wallH + 14;
            Cv cv = new Cv(W, H + 3);
            int wx = over, wy = H - wallH, seed = r.Int(9999);

            // ---- wall
            if (style == 1) StoneBlocks(cv, wx, wy, w, wallH, seed, Pal.St1, Pal.St2, Pal.St3, Pal.St0, 9, 5);
            else
            {
                for (int y = 0; y < wallH; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int c = Pal.Bn1;
                        float n = Noise.Fbm((x + seed) * 0.15f, y * 0.2f, seed, 2);
                        if (n < 0.4f) c = Pal.Bn0; else if (n > 0.66f) c = Pal.Bn2;
                        cv.Set(wx + x, wy + y, c);
                    }
                // foundation
                StoneBlocks(cv, wx, wy + wallH - 5, w, 5, seed + 1, Pal.St1, Pal.St2, Pal.St3, Pal.St0, 8, 5);
                // timber frame
                int beam = Pal.Ea2, beamD = Pal.Ea1;
                cv.Rect(wx, wy, 2, wallH - 5, beam); cv.Rect(wx + w - 2, wy, 2, wallH - 5, beamD);
                cv.Rect(wx, wy + (wallH - 5) / 2, w, 2, beam);
                int studs = Math.Max(2, w / 15);
                for (int i = 1; i < studs; i++) cv.Rect(wx + i * w / studs, wy, 2, wallH - 5, beam);
                // corner braces
                cv.Line(wx + 2, wy + (wallH - 5) / 2, wx + 2 + (wallH - 5) / 2, wy, beamD);
                cv.Line(wx + w - 3, wy + (wallH - 5) / 2, wx + w - 3 - (wallH - 5) / 2, wy, beamD);
            }
            // ---- openings
            if (windows != null)
                foreach (int wxo in windows) Window(cv, wx + wxo, wy + 6, 7, 8, lit, Pal.Ea1);
            if (doorX >= 0) Door(cv, wx + doorX, wy + wallH - 19, 11, 17, Pal.Ea3);
            if (forgeX >= 0)
            {
                // open forge arch with a glowing hearth
                int fx = wx + forgeX, fy = wy + wallH - 22;
                cv.Rect(fx - 1, fy - 1, 24, 22, Pal.St0);
                cv.Rect(fx, fy, 22, 21, Pal.Ink1);
                cv.Set(fx, fy, Pal.St0); cv.Set(fx + 21, fy, Pal.St0); cv.Set(fx + 1, fy, Pal.St0); cv.Set(fx + 20, fy, Pal.St0); cv.Set(fx, fy + 1, Pal.St0); cv.Set(fx + 21, fy + 1, Pal.St0);
                StoneBlocks(cv, fx + 5, fy + 9, 12, 12, seed + 3, Pal.St1, Pal.St2, Pal.St3, Pal.St0, 6, 4);
                cv.Rect(fx + 7, fy + 12, 8, 6, Pal.Ink0);
                for (int j = 0; j < 5; j++)
                    for (int i = 0; i < 6; i++)
                        cv.Set(fx + 8 + i, fy + 13 + j, Col.E(j < 1 ? Pal.Em3 : (j < 3 ? Pal.Em4 : Pal.Em5)));
                cv.Set(fx + 10, fy + 12, Col.E(Pal.Em4)); cv.Set(fx + 12, fy + 11, Col.E(Pal.Em3));
            }
            // ---- roof
            int eaveY = wy + 3, ridgeY = wy - roofH, ridgeInset = Math.Max(6, W / 7);
            for (int y = ridgeY; y <= eaveY; y++)
            {
                float t = (y - ridgeY) / (float)(eaveY - ridgeY);
                int inset = (int)((1 - t) * ridgeInset + 0.5f);
                int row = y - ridgeY, band = row / 4, inBand = row % 4;
                for (int x = inset; x < W - inset; x++)
                {
                    int c = roof[1];
                    if (inBand == 3) c = roof[0];
                    else if (inBand == 0) c = roof[2];
                    if (inBand != 3 && ((x + (band & 1) * 3) % 6) == 0) c = roof[0];
                    float hsh = Noise.H((x + (band & 1) * 3) / 6, band, seed + 9);
                    if (c == roof[1] && hsh < 0.16f) c = roof[2];
                    else if (c == roof[2] && hsh > 0.86f) c = roof[3];
                    if (x == inset || x == W - inset - 1) c = roof[0];
                    if (row < 2) c = row == 0 ? roof[3] : roof[2];
                    cv.Set(x, y, c);
                }
            }
            // eave shadow on the wall
            for (int y = eaveY + 1; y <= eaveY + 4; y++)
                for (int x = wx; x < wx + w; x++)
                {
                    int c = cv.Get(x, y);
                    if (((c >> 24) & 255) == 255) cv.Set(x, y, Dk(c, y <= eaveY + 2 ? 0.55f : 0.78f));
                }
            // moss and wear
            for (int i = 0; i < W / 3; i++)
            {
                int x = r.Int(W), y = ridgeY + r.Int(Math.Max(1, eaveY - ridgeY));
                if (cv.Get(x, y) == roof[0] && roof != RoofMoss) { cv.Set(x, y, Pal.Lf2); if (r.Chance(0.5f)) cv.Set(x + 1, y, Pal.Lf3); }
            }
            if (chimney)
            {
                int cx0 = (int)(W * (r.Chance(0.5f) ? 0.68f : 0.24f)), cy0 = ridgeY - 7;
                StoneBlocks(cv, cx0, cy0, 8, 17, seed + 5, Pal.St1, Pal.St2, Pal.St3, Pal.St0, 5, 4);
                cv.Rect(cx0 - 1, cy0, 10, 2, Pal.St4);
                cv.Rect(cx0 + 2, cy0 - 1, 4, 1, Pal.Ink1);
            }
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(W / 2, H);
        }

        public static Sprite Tent(Rng r, int cloth)
        {
            Cv cv = new Cv(48, 36);
            int cl = Col.Shade(cloth, 1), cd = Col.Shade(cloth, -1);
            cv.Poly(cloth, 24, 3, 44, 32, 4, 32);
            cv.Poly(cl, 24, 3, 24, 32, 4, 32);
            cv.Poly(Pal.Ink1, 24, 12, 31, 32, 18, 32);
            cv.Poly(cd, 24, 12, 27, 32, 24, 32);
            for (int y = 8; y < 32; y += 5) cv.Line(24 - (y - 3) * 20 / 29, y, 24, y, cd);
            cv.Rect(23, 0, 2, 5, Pal.Ea2);
            cv.Line(4, 32, 1, 34, Pal.Ea3); cv.Line(44, 32, 47, 34, Pal.Ea3);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(24, 34);
        }

        /// <summary>Broken stone wall with a ragged top edge.</summary>
        public static Sprite RuinWall(Rng r, int w, int h)
        {
            Cv cv = new Cv(w + 4, h + 6);
            int seed = r.Int(9999);
            StoneBlocks(cv, 2, 3, w, h, seed, Pal.St1, Pal.St2, Pal.St3, Pal.St0, 9, 5);
            // bite chunks out of the top
            for (int x = 0; x < w; x++)
            {
                float n = Noise.Fbm((x + seed) * 0.16f, 0.3f, seed, 2);
                int cut = (int)(n * h * 0.9f) - h / 5;
                cut = (cut / 5) * 5;
                for (int y = 0; y < cut && y < h - 6; y++) cv.Set(2 + x, 3 + y, 0);
            }
            // lit top of the remaining wall
            for (int x = 0; x < w + 4; x++)
                for (int y = 0; y < h + 5; y++)
                    if (cv.Solid(x, y) && !cv.Solid(x, y - 1)) { cv.Set(x, y, Pal.St4); break; }
            Moss(cv, r, 0.25f);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(2 + w / 2, h + 3);
        }

        public static void Moss(Cv cv, Rng r, float amount)
        {
            for (int y = 1; y < cv.H; y++)
                for (int x = 0; x < cv.W; x++)
                {
                    if (!cv.Solid(x, y)) continue;
                    float n = Noise.Fbm(x * 0.13f, y * 0.13f, 311, 2);
                    float grow = (y / (float)cv.H) * 0.35f;
                    if (n + grow > 1f - amount * 0.55f)
                    {
                        int c = cv.Get(x, y);
                        if ((c >> 24 & 255) != 255) continue;
                        cv.Set(x, y, Noise.H(x, y, 3) < 0.5f ? Pal.Lf2 : Pal.Lf3);
                    }
                }
        }

        public static Sprite Pillar(Rng r, int h, bool broken)
        {
            Cv cv = new Cv(16, h + 8);
            int by = h + 5;
            cv.Rect(2, by - 3, 12, 4, Pal.St2); cv.Rect(2, by - 3, 12, 1, Pal.St4);
            for (int y = by - 4; y > by - h; y--)
                for (int x = 4; x < 12; x++)
                {
                    int c = x < 6 ? Pal.St4 : (x < 9 ? Pal.St3 : (x < 11 ? Pal.St2 : Pal.St1));
                    if ((by - y) % 9 == 0) c = Col.Shade(c, -1);
                    cv.Set(x, y, c);
                }
            if (!broken) { cv.Rect(2, by - h - 2, 12, 3, Pal.St3); cv.Rect(2, by - h - 2, 12, 1, Pal.St5); }
            else
            {
                for (int x = 4; x < 12; x++) { int cut = (int)(Noise.H(x, h, 4) * 5); for (int y = 0; y < cut; y++) cv.Set(x, by - h + 1 + y, 0); }
            }
            Moss(cv, r, 0.15f);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(8, by + 1);
        }

        public static Sprite Arch(Rng r)
        {
            Cv cv = new Cv(44, 46);
            StoneBlocks(cv, 2, 8, 9, 34, 5, Pal.St1, Pal.St2, Pal.St3, Pal.St0, 9, 5);
            StoneBlocks(cv, 33, 8, 9, 34, 6, Pal.St1, Pal.St2, Pal.St3, Pal.St0, 9, 5);
            for (int x = 2; x < 42; x++)
            {
                float t = (x - 22) / 20f;
                int top = 2 + (int)(t * t * 9);
                for (int y = top; y < top + 7; y++) cv.Set(x, y, (x % 6 == 0) ? Pal.St0 : (y == top ? Pal.St4 : Pal.St2));
            }
            for (int x = 24; x < 34; x++) for (int y = 0; y < 12; y++) if (Noise.H(x, y, 9) < 0.4f + (x - 24) * 0.05f) cv.Set(x, y, 0);
            Moss(cv, r, 0.2f);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(22, 43);
        }

        /// <summary>Ruined round watchtower.</summary>
        public static Sprite Watchtower(Rng r)
        {
            int W = 60, H = 92;
            Cv cv = new Cv(W, H);
            int by = H - 4;
            for (int y = 12; y <= by; y++)
                for (int x = 8; x < 52; x++)
                {
                    float u = (x - 30) / 22f;
                    // rounded base
                    if (y > by - 4 && Math.Abs(u) > 1f - (by - y) * 0.02f) continue;
                    int row = y / 5, off = (row & 1) * 5;
                    int lx = (x + off) % 10, ly = y % 5;
                    int c;
                    if (lx == 9 || ly == 4) c = Pal.St0;
                    else
                    {
                        float hsh = Noise.H((x + off) / 10, row, 91);
                        c = hsh < 0.3f ? Pal.St1 : (hsh < 0.9f ? Pal.St2 : Pal.St3);
                        if (u < -0.45f) c = Col.Shade(c, 1); else if (u > 0.5f) c = Col.Shade(c, -1);
                    }
                    cv.Set(x, y, c);
                }
            // ragged top
            for (int x = 8; x < 52; x++)
            {
                int cut = (int)(Noise.Fbm(x * 0.11f, 1.7f, 93, 2) * 30) - 9 + (x > 34 ? (x - 34) : 0);
                cut = Math.Max(0, (cut / 5) * 5);
                for (int y = 12; y < 12 + cut; y++) cv.Set(x, y, 0);
            }
            for (int x = 8; x < 52; x++) for (int y = 10; y < H; y++) if (cv.Solid(x, y)) { cv.Set(x, y, Pal.St4); break; }
            // doorway and slits
            cv.Rect(24, by - 20, 12, 20, Pal.Ink0); cv.Rect(25, by - 22, 10, 2, Pal.Ink0); cv.Rect(27, by - 23, 6, 1, Pal.Ink0);
            cv.Rect(23, by - 21, 1, 21, Pal.St4);
            cv.Rect(18, 44, 2, 7, Pal.Ink0); cv.Rect(40, 52, 2, 7, Pal.Ink0);
            Moss(cv, r, 0.3f);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(30, by + 1);
        }

        /// <summary>Burial mound with a stone door, sealed or open.</summary>
        public static Sprite Barrow(Rng r, bool open, bool rune)
        {
            int W = 84, H = 52;
            Cv cv = new Cv(W, H);
            int by = H - 3;
            for (int y = 4; y <= by; y++)
                for (int x = 2; x < W - 2; x++)
                {
                    float u = (x - W / 2f) / (W / 2f - 3), v = (by - y) / (float)(by - 5);
                    if (u * u + v * v > 1f) continue;
                    float n = Noise.Fbm(x * 0.1f, y * 0.14f, 61, 2);
                    float lightT = (-u * 0.5f + v * 0.9f) + (n - 0.5f) * 0.5f + (Cv.Bayer(x, y) - 0.5f) * 0.12f;
                    int c = lightT < 0.2f ? 0x33453F : (lightT < 0.55f ? 0x3D5348 : (lightT < 0.85f ? 0x496252 : 0x5C7860));
                    cv.Set(x, y, Col.Hex(c));
                }
            // stone portal
            int px = W / 2 - 11, py = by - 24;
            cv.Rect(px, py, 22, 25, Pal.St2);
            cv.Rect(px, py, 22, 4, Pal.St3); cv.Rect(px, py, 22, 1, Pal.St5);
            cv.Rect(px, py + 4, 4, 21, Pal.St3); cv.Rect(px + 18, py + 4, 4, 21, Pal.St1);
            if (open)
            {
                cv.Rect(px + 4, py + 4, 14, 21, Pal.Ink0);
                for (int i = 0; i < 4; i++) cv.Rect(px + 5 + i, py + 21 - i, 12 - i * 2, 1, Pal.Ink2);
            }
            else
            {
                StoneBlocks(cv, px + 4, py + 4, 14, 21, 67, Pal.St1, Pal.St2, Pal.St2, Pal.St0, 14, 7);
                int g = rune ? Col.E(Pal.Fr3) : Pal.St0;
                cv.Ascii(px + 7, py + 8, new[] { "..g.g..", ".g.g.g.", "g..g..g", ".g.g.g.", "..ggg..", "...g...", "..g.g.." }, Cv.Map('g', g));
            }
            // stones at the foot
            cv.Ell(px - 5, by - 2, 4, 2.6f, Pal.St2); cv.Ell(px + 27, by - 2, 3.5f, 2.4f, Pal.St1);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(W / 2, by + 1);
        }

        public static Sprite StandingStone(Rng r, int h, int glyph, bool lit)
        {
            Cv cv = new Cv(20, h + 8);
            int by = h + 5;
            for (int y = by; y > by - h; y--)
            {
                float t = (by - y) / (float)h;
                int hw = (int)(M.Lerp(6f, 3.2f, t * t) + Noise.H(y, 0, 20 + glyph) * 1.3f);
                int lean = (int)(t * (glyph % 3 - 1) * 2);
                for (int x = -hw; x <= hw; x++)
                {
                    int c = x < -hw + 2 ? Pal.St4 : (x < 1 ? Pal.St3 : (x < hw - 1 ? Pal.St2 : Pal.St1));
                    if (Noise.H(x + 40, y, 21) < 0.08f) c = Col.Shade(c, -1);
                    cv.Set(10 + x + lean, y, c);
                }
            }
            int g = lit ? Col.E(Pal.Fr3) : Pal.St0;
            string[][] glyphs =
            {
                new[]{ ".ggg.", "g...g", "g.g.g", "g...g", ".ggg.", "..g..", "..g.." },
                new[]{ "g...g", ".g.g.", "..g..", ".g.g.", "g...g", "..g..", "ggggg" },
                new[]{ "..g..", ".ggg.", "g.g.g", "..g..", "..g..", ".g.g.", "g...g" },
            };
            cv.Ascii(8, by - h + h / 3, glyphs[glyph % 3], Cv.Map('g', g));
            Moss(cv, r, 0.2f);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(10, by + 1);
        }

        public static Sprite Gravestone(Rng r, int variant)
        {
            Cv cv = new Cv(14, 18);
            int by = 15;
            if (variant % 3 == 0)
            {
                cv.Rect(3, 5, 8, 11, Pal.St2); cv.Rect(4, 3, 6, 2, Pal.St2); cv.Rect(5, 2, 4, 1, Pal.St2);
                cv.Rect(3, 5, 1, 11, Pal.St4); cv.Rect(4, 3, 1, 2, Pal.St4); cv.Rect(10, 5, 1, 11, Pal.St1);
                cv.Rect(5, 7, 4, 1, Pal.St0); cv.Rect(5, 9, 3, 1, Pal.St0);
            }
            else if (variant % 3 == 1)
            {
                // bell-and-line marker: the sign of the Vesper faith
                cv.Rect(6, 4, 2, 12, Pal.St2); cv.Rect(6, 4, 1, 12, Pal.St4);
                cv.Ell(7, 4.5f, 3.4f, 3.4f, Pal.St2); cv.Ell(7, 4.5f, 1.4f, 1.4f, 0);
                cv.Set(4, 3, Pal.St4); cv.Set(5, 2, Pal.St4);
            }
            else
            {
                // leaning slab
                for (int y = 0; y < 11; y++) cv.Rect(3 + y / 4, 5 + y, 7, 1, y < 1 ? Pal.St4 : Pal.St2);
                cv.Rect(6, 8, 3, 1, Pal.St0);
            }
            cv.Rect(2, by, 10, 1, Pal.St1);
            Moss(cv, r, 0.22f);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(7, by + 1);
        }

        /// <summary>Weathered statue of a hooded bell-warden.</summary>
        public static Sprite Statue(Rng r, bool headless)
        {
            Cv cv = new Cv(24, 46);
            int[] m = Cv.Map('a', Pal.St3, 'b', Pal.St2, 'c', Pal.St1, 'l', Pal.St5, 'd', Pal.St0);
            cv.Ascii(2, 2, new[]
            {
                "........laa.........",
                ".......laaab........",
                "......laaaabc.......",
                "......ladddbc.......",
                "......laddabc.......",
                ".......aaabc........",
                "......laaaabc.......",
                ".....laaaaaabc......",
                "....laaaaaaabbc.....",
                "....laabaaabbbc.....",
                "....labbaaabbbc.....",
                "....labaaaaabbc..l..",
                "....laaaadaabbcclal.",
                "....laaaadaabbbllbc.",
                ".....aaaadaabb.labc.",
                ".....laaadaabc.lbbc.",
                ".....laaadaabc..bc..",
                ".....laaaaaabc..b...",
                ".....laaaaaabc......",
                "....laaaaaaabbc.....",
                "....laaabaaabbc.....",
                "....laaabaaabbc.....",
                "...laaaabaaabbbc....",
                "...laaaabaaaabbc....",
                "...laaaabaaaabbc....",
                "...laaabbaaaabbc....",
                "..laaaabbaaaabbbc...",
                "..laaaabaaaaabbbc...",
                "..llaaaaaaaabbbcc...",
                "..bbbbbbbbbbbbbbc...",
                ".llaaaaaaaaaaaabbc..",
                ".laaaaaaaaaaaaabbc..",
                ".laaaaaaaaaaaaabbc..",
                ".bbbbbbbbbbbbbbbcc..",
                "llaaaaaaaaaaaaaabbc.",
                "laaaaaaaaaaaaaaabbc.",
                "bbbbbbbbbbbbbbbbbcc.",
            }, m);
            if (headless) for (int y = 0; y < 8; y++) for (int x = 0; x < 24; x++) if (y < 6 + (x % 3)) cv.Set(x, y, 0);
            Moss(cv, r, 0.28f);
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(11, 40);
        }

        /// <summary>The Sunken Abbey: gabled nave with a rose window, side aisles and a broken bell tower.</summary>
        public static Sprite Abbey()
        {
            int W = 232, H = 196;
            Cv cv = new Cv(W, H);
            int by = H - 6, cx = W / 2;
            int[] st = { Pal.St0, Pal.St1, Pal.St2, Pal.St3 };

            // bell tower (behind, right)
            int tx0 = cx + 34, tw = 46, tTop = 8;
            StoneBlocks(cv, tx0, tTop, tw, by - tTop - 40, 401, st[1], st[2], st[2], st[0], 11, 6);
            for (int x = tx0; x < tx0 + tw; x++)
            {
                int cut = (int)(Noise.Fbm(x * 0.09f, 0.5f, 402, 2) * 44) - 8;
                cut = Math.Max(0, (cut / 6) * 6);
                for (int y = tTop; y < tTop + cut; y++) cv.Set(x, y, 0);
            }
            // belfry opening (empty – the bell fell)
            for (int y = 0; y < 30; y++)
                for (int x = 0; x < 20; x++)
                {
                    float u = (x - 9.5f) / 10f;
                    int top = (int)(u * u * 12);
                    if (y >= top) cv.Set(tx0 + 13 + x, tTop + 36 + y, Pal.Ink0);
                }
            cv.Rect(tx0 + 12, tTop + 66, 22, 2, st[3]);
            cv.Rect(tx0, tTop + 80, tw, 2, st[3]); cv.Rect(tx0, tTop + 82, tw, 1, st[0]);

            // side aisles
            int aisleH = 70, aw = 62;
            StoneBlocks(cv, cx - 52 - aw, by - aisleH, aw + 6, aisleH, 403, st[1], st[2], st[3], st[0], 11, 6);
            StoneBlocks(cv, cx + 46, by - aisleH, aw + 6, aisleH, 404, st[1], st[2], st[3], st[0], 11, 6);
            for (int i = 0; i < 2; i++)
            {
                int ax = i == 0 ? cx - 52 - aw : cx + 46;
                // sloping lean-to roof
                for (int x = 0; x < aw + 6; x++)
                {
                    int dist = i == 0 ? (aw + 6 - x) : x;
                    int top = by - aisleH - 16 + dist / 4;
                    for (int y = top; y < by - aisleH + 2; y++)
                    {
                        int row = y - top;
                        cv.Set(ax + x, y, row == 0 ? Pal.St3 : ((row % 4 == 3 || (x + (row / 4) * 3) % 7 == 0) ? Pal.Ink2 : Pal.St1));
                    }
                }
                // lancet windows
                for (int k = 0; k < 2; k++)
                {
                    int lx = ax + 14 + k * 26;
                    for (int y = 0; y < 30; y++)
                        for (int x = 0; x < 8; x++)
                        {
                            float u = (x - 3.5f) / 4f;
                            if (y >= (int)(u * u * 8)) cv.Set(lx + x, by - aisleH + 16 + y, (k + i) % 2 == 0 ? Pal.Ink0 : ((y / 5 + x / 3) % 2 == 0 ? Pal.Gl0 : Pal.Ink1));
                        }
                    cv.Rect(lx - 1, by - aisleH + 46, 10, 2, st[3]);
                }
                // collapsed outer end
                for (int x = 0; x < 20; x++)
                {
                    int xx = i == 0 ? ax + x : ax + aw + 5 - x;
                    int cut = (int)((20 - x) * 2.6f + Noise.H(x, i, 405) * 10);
                    cut = (cut / 6) * 6;
                    for (int y = by - aisleH - 18; y < by - aisleH + cut; y++) cv.Set(xx, y, 0);
                }
            }

            // central nave gable
            int nw = 104, nx = cx - nw / 2, wallTop = by - 112, peak = wallTop - 46;
            StoneBlocks(cv, nx, wallTop, nw, by - wallTop, 406, st[1], st[2], st[3], st[0], 12, 6);
            for (int y = peak; y < wallTop; y++)
            {
                float t = (y - peak) / (float)(wallTop - peak);
                int hw = (int)(t * nw / 2);
                for (int x = cx - hw; x < cx + hw; x++)
                {
                    int row = y / 6, off = (row & 1) * 6;
                    int lx = (x + off + 600) % 12, ly = (y + 600) % 6;
                    int c = (lx == 11 || ly == 5) ? st[0] : (Noise.H((x + off) / 12, row, 407) < 0.3f ? st[1] : st[2]);
                    if (x == cx - hw || x == cx + hw - 1 || x == cx - hw + 1 || x == cx + hw - 2) c = st[3];
                    cv.Set(x, y, c);
                }
            }
            // bell-and-line finial
            cv.Rect(cx - 1, peak - 12, 2, 12, st[3]); cv.Ell(cx, peak - 14, 4, 4, st[3]); cv.Ell(cx, peak - 14, 2, 2, 0);
            // buttresses
            for (int i = 0; i < 2; i++)
            {
                int bx = i == 0 ? nx - 8 : nx + nw - 6;
                for (int y = wallTop + 6; y <= by; y++)
                {
                    int step = (y - wallTop) / 34;
                    int w = 10 + step * 3;
                    int x0 = i == 0 ? bx - step * 3 : bx;
                    for (int x = 0; x < w + 4; x++)
                    {
                        int c = x < 2 ? Pal.St4 : (x > w + 1 ? st[0] : ((y % 7 == 0) ? st[0] : st[2]));
                        cv.Set(x0 + x, y, c);
                    }
                }
                cv.Rect(i == 0 ? bx : bx, wallTop + 2, 14, 4, Pal.St4);
            }
            // rose window
            int rcx = cx, rcy = wallTop + 26;
            cv.Circle(rcx, rcy, 19, st[0]);
            cv.Circle(rcx, rcy, 17, Pal.St4);
            cv.Circle(rcx, rcy, 15, Pal.Ink0);
            for (int y = -14; y <= 14; y++)
                for (int x = -14; x <= 14; x++)
                {
                    float d = M.Sqrt(x * x + y * y);
                    if (d > 14.2f) continue;
                    float a = M.Atan2(y, x);
                    int seg = (int)((a + M.PI) / M.TAU * 12);
                    bool spoke = Math.Abs(((a + M.PI) / M.TAU * 12) - seg - 0.5f) > 0.4f;
                    if (d < 3.2f) { cv.Set(rcx + x, rcy + y, Col.E(Pal.Gl4)); continue; }
                    if (spoke || (d > 8.2f && d < 9.4f)) { cv.Set(rcx + x, rcy + y, Pal.St3); continue; }
                    bool broken = Noise.H(seg, d > 9 ? 1 : 0, 408) < 0.3f;
                    if (broken) continue;
                    cv.Set(rcx + x, rcy + y, Col.E(d > 9 ? ((seg & 1) == 0 ? Pal.Gl2 : Pal.Gl1) : ((seg & 1) == 0 ? Pal.Gl3 : Pal.Gl2)));
                }
            // great door
            int dw = 34, dh = 52, dx = cx - dw / 2, dy = by - dh;
            for (int y = 0; y < dh + 6; y++)
                for (int x = -5; x < dw + 5; x++)
                {
                    float u = (x - dw / 2f + 0.5f) / (dw / 2f + 5);
                    int top = (int)(u * u * 26) - 6;
                    if (y - 6 < top) continue;
                    float ui = (x - dw / 2f + 0.5f) / (dw / 2f);
                    int topIn = (int)(ui * ui * 22);
                    bool inner = x >= 0 && x < dw && y - 6 >= topIn;
                    int c;
                    if (inner)
                    {
                        int depth = dh - (y - 6);
                        c = depth < 14 ? Col.E(Col.Lerp(Pal.Ink0, Pal.Gl1, (14 - depth) / 14f * 0.9f)) : Pal.Ink0;
                        if (depth < 5 && (x + depth) % 3 == 0) c = Col.E(Pal.Gl2);
                    }
                    else c = ((x + 60) % 6 == 0) ? st[0] : (x < dw / 2 ? Pal.St4 : st[3]);
                    cv.Set(dx + x, dy + y - 6, c);
                }
            // steps
            for (int i = 0; i < 3; i++)
            {
                cv.Rect(dx - 10 - i * 5, by + i * 2 - 1, dw + 20 + i * 10, 2, i == 0 ? Pal.St4 : Pal.St3);
                cv.Rect(dx - 10 - i * 5, by + i * 2, dw + 20 + i * 10, 1, st[1]);
            }
            // string course
            cv.Rect(nx, wallTop + 54, nw, 2, Pal.St4); cv.Rect(nx, wallTop + 56, nw, 1, st[0]);
            // weathering: darker toward the ground, cracks, ivy
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int c = cv.Get(x, y);
                    if (((c >> 24) & 255) != 255) continue;
                    float g = (y - (by - 60)) / 60f;
                    if (g > 0) cv.Set(x, y, Col.Mul(c, 1f - g * 0.22f));
                    if (Noise.Fbm(x * 0.045f, y * 0.03f, 409, 3) > 0.69f) cv.Set(x, y, Col.Mul(cv.Get(x, y), 0.72f));
                }
            Rng r = new Rng(410);
            for (int i = 0; i < 16; i++)
            {
                int vx = 14 + r.Int(W - 28), vy = by, len = r.Range(14, 60);
                for (int k = 0; k < len; k++)
                {
                    int c0 = cv.Get(vx, vy);
                    if (((c0 >> 24) & 255) == 255) { cv.Set(vx, vy, r.Chance(0.3f) ? Pal.Lf3 : Pal.Lf2); if (r.Chance(0.35f)) cv.Set(vx + (r.Chance(0.5f) ? 1 : -1), vy, Pal.Lf3); }
                    vy--; if (r.Chance(0.4f)) vx += r.Chance(0.5f) ? 1 : -1;
                }
            }
            for (int i = 0; i < 7; i++)
            {
                int kx = nx + r.Int(nw), ky = wallTop + r.Int(90);
                for (int k = 0; k < r.Range(6, 16); k++) { if (cv.Solid(kx, ky)) cv.Set(kx, ky, Pal.Ink1); ky++; if (r.Chance(0.5f)) kx += r.Chance(0.5f) ? 1 : -1; }
            }
            cv.Outline(Pal.Ink0);
            return cv.ToSpriteTrim(cx, by + 5);
        }
    }
}
