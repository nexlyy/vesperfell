using System;
using System.Threading.Tasks;

namespace Vesperfell
{
    /// <summary>Terrain ids stored in the tile map.</summary>
    public static class T
    {
        public const byte Void = 0, Grass = 1, Forest = 2, Dirt = 3, Cobble = 4, Water = 5, Mud = 6, Stone = 7, Ash = 8,
            Plank = 9, PaleGrass = 10, DFloor = 11, DWall = 12, DCarpet = 13, Thicket = 14, PlankV = 15, DRitual = 16;

        public static bool Soft(byte t) { return t == Grass || t == Forest || t == Ash || t == PaleGrass || t == Thicket || t == Mud; }
        public static bool GrassLike(byte t) { return t == Grass || t == Forest || t == PaleGrass || t == Thicket; }
        public static bool DungeonFloor(byte t) { return t == DFloor || t == DCarpet || t == DRitual; }
    }

    /// <summary>
    /// Paints the whole ground layer of a map, pixel by pixel, into one big bitmap. Terrain borders are warped with
    /// noise so nothing looks like it sits on a tile grid, then edges are shaded and small decals are stamped on top.
    /// </summary>
    public static class TerrainArt
    {
        public const int TS = 16;

        static int Hx(int rgb) { return rgb & 0xFFFFFF; }

        // ---- palettes (ground pixels are stored without alpha: the alpha byte is the emissive flag)
        static readonly int[] GrassC = { 0x263F33, 0x2E4C39, 0x385A40, 0x46703F };
        static readonly int[] ForestC = { 0x17292B, 0x1C3330, 0x233E35, 0x2C4C3A };
        static readonly int[] PaleC = { 0x33453F, 0x3D5348, 0x496252, 0x5C7860 };
        static readonly int[] AshC = { 0x2B2D34, 0x34373C, 0x3E4245, 0x55584F };
        static readonly int[] ThickC = { 0x0F1A1E, 0x132126, 0x17282B, 0x1C3330 };
        static readonly int[] DirtC = { 0x3A2A2A, 0x48352F, 0x574035, 0x6B5140 };
        static readonly int[] MudC = { 0x241F20, 0x2C2623, 0x362E28, 0x1B2A33 };
        static readonly int[] CobC = { 0x1F2230, 0x343848, 0x444A5C, 0x565E70, 0x6C7586 };
        static readonly int[] StoneC = { 0x1E2130, 0x30354A, 0x3C435A, 0x4B546D, 0x5E6A82 };
        static readonly int[] WaterC = { 0x0F1E2C, 0x14293A, 0x1A3548, 0x26505F, 0x3F7682 };
        static readonly int[] PlankC = { 0x2E2126, 0x4A342E, 0x5F4337, 0x76563F };
        static readonly int[] DFloorC = { 0x16172A, 0x232638, 0x2A2E43, 0x33384F, 0x3D435B };
        static readonly int[] DWallC = { 0x0B0A12, 0x15121F, 0x2A2D40, 0x3A3F56, 0x4F5670, 0x67708A };

        static int bakeCell = 16;

        /// <summary>terr is a grid of tw x th cells, each 'cs' pixels square.</summary>
        public static int[] Bake(byte[] terr, int tw, int th, int cs, int seed, out byte[] pixTerr)
        {
            bakeCell = cs;
            int W = tw * cs, H = th * cs;
            int[] px = new int[W * H];
            byte[] pt = new byte[W * H];

            Parallel.For(0, H, delegate(int y)
            {
                for (int x = 0; x < W; x++)
                {
                    byte raw = terr[(y / cs) * tw + (x / cs)];
                    byte t;
                    if (raw == T.Plank || raw == T.PlankV) t = raw;
                    else
                    {
                        float nx = x * 0.045f, ny = y * 0.045f;
                        float wx = x + (Noise.Value(nx, ny, seed + 11) - 0.5f) * 15f + (Noise.Value(x * 0.17f, y * 0.17f, seed + 12) - 0.5f) * 5f;
                        float wy = y + (Noise.Value(nx, ny, seed + 13) - 0.5f) * 15f + (Noise.Value(x * 0.17f, y * 0.17f, seed + 14) - 0.5f) * 5f;
                        t = At(terr, tw, th, wx, wy);
                        if (t == T.Plank || t == T.PlankV) t = raw;
                        if (T.Soft(t))
                        {
                            // dithered blend band between soft terrains
                            float b = Cv.Bayer(x, y);
                            byte t2 = At(terr, tw, th, wx + 5, wy + 3);
                            if (t2 != t && T.Soft(t2) && b < 0.5f) t = t2;
                            else
                            {
                                byte t3 = At(terr, tw, th, wx - 4, wy - 5);
                                if (t3 != t && T.Soft(t3) && b > 0.75f) t = t3;
                            }
                        }
                    }
                    pt[y * W + x] = t;
                    px[y * W + x] = BasePixel(t, x, y, seed);
                }
            });

            EdgePass(px, pt, W, H, seed);
            StampPass(px, pt, W, H, seed);
            pixTerr = pt;
            return px;
        }

        static byte At(byte[] terr, int tw, int th, float wx, float wy)
        {
            int tx = (int)(wx / bakeCell), ty = (int)(wy / bakeCell);
            if (tx < 0) tx = 0; else if (tx >= tw) tx = tw - 1;
            if (ty < 0) ty = 0; else if (ty >= th) ty = th - 1;
            return terr[ty * tw + tx];
        }

        static int Tone(int[] c, int x, int y, int seed, float freq, float lo, float hi)
        {
            float n = Noise.Fbm(x * freq, y * freq, seed, 3) + (Cv.Bayer(x, y) - 0.5f) * 0.07f;
            return n < lo ? c[0] : (n < hi ? c[1] : c[2]);
        }

        static int BasePixel(byte t, int x, int y, int seed)
        {
            switch (t)
            {
                case T.Grass: return Tone(GrassC, x, y, seed, 0.021f, 0.44f, 0.58f);
                case T.Forest:
                    {
                        int c = Tone(ForestC, x, y, seed + 3, 0.026f, 0.45f, 0.6f);
                        float h = Noise.H(x, y, seed + 40);
                        if (h < 0.006f) c = 0x4A342E; else if (h < 0.010f) c = 0x5A2A22;
                        return c;
                    }
                case T.PaleGrass: return Tone(PaleC, x, y, seed + 5, 0.023f, 0.43f, 0.6f);
                case T.Ash:
                    {
                        int c = Tone(AshC, x, y, seed + 7, 0.03f, 0.42f, 0.6f);
                        if (Noise.H(x, y, seed + 41) < 0.006f) c = 0x6F6756;
                        return c;
                    }
                case T.Thicket: return Tone(ThickC, x, y, seed + 9, 0.03f, 0.45f, 0.6f);
                case T.Dirt:
                    {
                        float n = Noise.Fbm(x * 0.05f, y * 0.05f, seed + 2, 2) + (Cv.Bayer(x, y) - 0.5f) * 0.1f;
                        return n < 0.4f ? DirtC[0] : (n < 0.62f ? DirtC[1] : DirtC[2]);
                    }
                case T.Mud:
                    {
                        float n = Noise.Fbm(x * 0.04f, y * 0.04f, seed + 4, 2) + (Cv.Bayer(x, y) - 0.5f) * 0.06f;
                        float p = Noise.Fbm(x * 0.06f, y * 0.085f, seed + 6, 2);
                        if (p > 0.64f) return p > 0.67f ? 0x14293A : 0x22404C;
                        return n < 0.45f ? MudC[0] : (n < 0.6f ? MudC[1] : MudC[2]);
                    }
                case T.Cobble: return Cobble(x, y, seed);
                case T.Stone: return Flag(x, y, seed, StoneC, 18, 12);
                case T.Water:
                    {
                        float n = Noise.Fbm(x * 0.03f, y * 0.045f, seed + 8, 2) + (Cv.Bayer(x, y) - 0.5f) * 0.08f;
                        int c = n < 0.47f ? WaterC[0] : (n < 0.6f ? WaterC[1] : WaterC[2]);
                        int row = y, sh = (int)(Noise.H(0, row, seed + 20) * 7);
                        if (Noise.H((x + sh) / 7, row, seed + 21) < 0.045f && ((x + sh) % 7) < 4) c = WaterC[2];
                        return c;
                    }
                case T.Plank:
                    {
                        int ly = y % 5;
                        if (ly == 4) return PlankC[0];
                        int plank = y / 5;
                        int c = (Noise.H(plank, 0, seed + 30) < 0.5f) ? PlankC[1] : PlankC[2];
                        if (ly == 0) c = PlankC[3];
                        if (Noise.H(x / 3, plank, seed + 31) < 0.08f) c = PlankC[1];
                        return c;
                    }
                case T.PlankV:
                    {
                        int lx = x % 5;
                        if (lx == 4) return PlankC[0];
                        int plank = x / 5;
                        int c = (Noise.H(plank, 1, seed + 30) < 0.5f) ? PlankC[1] : PlankC[2];
                        if (lx == 0) c = PlankC[3];
                        if (Noise.H(plank, y / 3, seed + 31) < 0.08f) c = PlankC[1];
                        return c;
                    }
                default: return 0x05050A;
            }
        }

        static int Cobble(int x, int y, int seed)
        {
            float fx = x / 7.5f, fy = y / 6f;
            int cxi = M.Floor(fx), cyi = M.Floor(fy);
            float best = 1e9f, second = 1e9f; int bi = 0, bj = 0; float bdx = 0, bdy = 0;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int gx = cxi + i, gy = cyi + j;
                    float px = gx + 0.5f + (Noise.H(gx, gy, seed + 50) - 0.5f) * 0.72f;
                    float py = gy + 0.5f + (Noise.H(gx, gy, seed + 51) - 0.5f) * 0.72f;
                    float dx = fx - px, dy = fy - py, dd = dx * dx + dy * dy;
                    if (dd < best) { second = best; best = dd; bi = gx; bj = gy; bdx = dx; bdy = dy; }
                    else if (dd < second) second = dd;
                }
            float edge = M.Sqrt(second) - M.Sqrt(best);
            if (edge < 0.17f) return CobC[0];
            float h = Noise.H(bi, bj, seed + 52);
            int k = h < 0.3f ? 1 : (h < 0.8f ? 2 : 3);
            if (bdx + bdy < -0.22f && edge < 0.55f) k++;
            else if (bdx + bdy > 0.3f && edge < 0.5f) k--;
            if (k < 1) k = 1;
            return CobC[k];
        }

        static int Flag(int x, int y, int seed, int[] c, int sw, int shh)
        {
            int row = y / shh;
            int off = ((row & 1) * (sw / 2)) + (int)(Noise.H(row, 3, seed + 60) * 5);
            int col = (x + off) / sw, lx = (x + off) % sw, ly = y % shh;
            if (lx == 0 || ly == 0)
                return Noise.Fbm(x * 0.05f, y * 0.05f, seed + 61, 2) > 0.56f ? 0x234B41 : c[0];
            float h = Noise.H(col, row, seed + 62);
            int k = h < 0.35f ? 1 : (h < 0.85f ? 2 : 3);
            if (lx == 1 || ly == 1) k++;
            else if (lx == sw - 1 || ly == shh - 1) k = Math.Max(1, k - 1);
            if (h > 0.9f)
            {
                int d = lx - ly - (int)(h * 40) % 9;
                if (d == 0 || (d == 1 && (ly & 1) == 0)) return c[0];
            }
            if (Noise.H(x, y, seed + 63) < 0.02f) k = Math.Max(1, k - 1);
            return c[Math.Min(k, c.Length - 1)];
        }

        static int Darker(int c, int f256)
        {
            int r = ((c >> 16) & 255) * f256 >> 8, g = ((c >> 8) & 255) * f256 >> 8, b = (c & 255) * f256 >> 8;
            return (c & Col.Opaque) | (r << 16) | (g << 8) | b;
        }

        static int Lighter(int c, int amt)
        {
            int r = Math.Min(255, ((c >> 16) & 255) + amt), g = Math.Min(255, ((c >> 8) & 255) + amt), b = Math.Min(255, (c & 255) + amt);
            return (c & Col.Opaque) | (r << 16) | (g << 8) | b;
        }

        static void EdgePass(int[] px, byte[] pt, int W, int H, int seed)
        {
            int[] src = (int[])px.Clone();
            Parallel.For(1, H - 1, delegate(int y)
            {
                for (int x = 1; x < W - 1; x++)
                {
                    int i = y * W + x;
                    byte t = pt[i];
                    byte up = pt[i - W], dn = pt[i + W], lf = pt[i - 1], rt = pt[i + 1];
                    if (t == T.Water)
                    {
                        // shoreline: foam against land, darker further in
                        bool land = up != T.Water || dn != T.Water || lf != T.Water || rt != T.Water;
                        if (land) { px[i] = (Noise.H(x / 2, y / 2, seed + 70) < 0.7f) ? WaterC[4] : WaterC[3]; continue; }
                        bool near = false;
                        for (int k = 2; k <= 3 && !near; k++)
                        {
                            if (y - k >= 0 && pt[i - W * k] != T.Water) near = true;
                            else if (y + k < H && pt[i + W * k] != T.Water) near = true;
                            else if (x - k >= 0 && pt[i - k] != T.Water) near = true;
                            else if (x + k < W && pt[i + k] != T.Water) near = true;
                        }
                        if (near) px[i] = WaterC[3];
                        continue;
                    }
                    if (T.GrassLike(t))
                    {
                        // a lit lip where grass overhangs lower ground to the south
                        if (!T.Soft(dn) && dn != T.Void) px[i] = Lighter(src[i], 18);
                        continue;
                    }
                    // lower ground: shadow under grass to the north / at the sides
                    if (t == T.Dirt || t == T.Cobble || t == T.Stone || t == T.Mud || t == T.Ash || t == T.Plank || t == T.PlankV)
                    {
                        if (T.GrassLike(up)) px[i] = Darker(src[i], 150);
                        else if (y >= 2 && T.GrassLike(pt[i - 2 * W])) px[i] = Darker(src[i], 205);
                        else if ((T.GrassLike(lf) || T.GrassLike(rt)) && Noise.H(x, y, seed + 71) < 0.6f) px[i] = Darker(src[i], 185);
                        else if (up == T.Water || dn == T.Water || lf == T.Water || rt == T.Water) px[i] = Darker(src[i], 150);
                        else if ((t == T.Plank || t == T.PlankV) && (up != t && dn != t || lf != t && rt != t)) px[i] = PlankC[0];
                    }
                }
            });
        }

        static readonly int[][] TuftStamps =
        {
            new[]{0,0, 0,-1, 2,0, 2,-1, 2,-2, 4,0, 4,-1},
            new[]{0,0, 0,-1, 0,-2, 2,0, 2,-1},
            new[]{0,0, 1,-1, 3,0, 3,-1},
            new[]{0,0, 0,-1},
            new[]{0,0, 2,0, 2,-1, 4,0},
        };

        static void StampPass(int[] px, byte[] pt, int W, int H, int seed)
        {
            Rng r = new Rng(seed * 31 + 7);
            int count = W * H / 46;
            for (int n = 0; n < count; n++)
            {
                int x = 4 + r.Int(W - 8), y = 4 + r.Int(H - 8);
                int i = y * W + x;
                byte t = pt[i];
                if (pt[i - 2] != t || pt[i + 4] != t || pt[i - 2 * W] != t || pt[i + W] != t) continue;
                switch (t)
                {
                    case T.Grass: Tuft(px, W, x, y, r, r.Chance(0.6f) ? GrassC[3] : GrassC[0]); break;
                    case T.PaleGrass: Tuft(px, W, x, y, r, r.Chance(0.7f) ? PaleC[3] : PaleC[0]); break;
                    case T.Forest:
                        if (r.Chance(0.45f)) Tuft(px, W, x, y, r, r.Chance(0.5f) ? ForestC[3] : ThickC[1]);
                        else if (r.Chance(0.3f)) { px[i] = 0x3A2A2A; px[i + 1] = 0x3A2A2A; if (r.Chance(0.5f)) px[i + 2 - W] = 0x3A2A2A; }
                        break;
                    case T.Thicket: if (r.Chance(0.3f)) Tuft(px, W, x, y, r, ThickC[3]); break;
                    case T.Ash:
                        if (r.Chance(0.3f)) Tuft(px, W, x, y, r, AshC[3]);
                        else if (r.Chance(0.2f)) { px[i] = 0x55584F; px[i + W + 1] = 0x1E2130; }
                        break;
                    case T.Dirt:
                        if (r.Chance(0.5f))
                        {
                            px[i] = DirtC[3]; px[i + W + 1] = 0x2E2126;
                            if (r.Chance(0.4f)) { px[i + 1] = DirtC[3]; px[i + W + 2] = 0x2E2126; }
                        }
                        else if (r.Chance(0.25f)) { px[i] = 0x2E2126; px[i + 1 + W] = 0x2E2126; px[i + 2 + W] = 0x2E2126; }
                        break;
                    case T.Mud: if (r.Chance(0.25f)) { px[i] = MudC[2]; px[i + 1] = MudC[2]; px[i + W] = 0x1A1617; } break;
                    case T.Cobble: if (r.Chance(0.06f) && px[i] == CobC[0]) { px[i] = 0x385A40; px[i - W] = 0x46703F; } break;
                    case T.Stone: if (r.Chance(0.05f)) { px[i] = 0x234B41; px[i + 1] = 0x2A5244; } break;
                }
            }
        }

        static void Tuft(int[] px, int W, int x, int y, Rng r, int c)
        {
            int[] st = TuftStamps[r.Int(TuftStamps.Length)];
            for (int k = 0; k < st.Length; k += 2) px[(y + st[k + 1]) * W + x + st[k]] = c;
        }

        // dungeon
        /// <summary>Tile-aligned bake for interiors: flagstones, carpet, and walls with a raised brick face.</summary>
        public static int[] BakeDungeon(byte[] terr, int tw, int th, int seed)
        {
            int W = tw * TS, H = th * TS;
            int[] px = new int[W * H];
            Parallel.For(0, H, delegate(int y)
            {
                int ty = y / TS, ly = y % TS;
                for (int x = 0; x < W; x++)
                {
                    int tx = x / TS, lx = x % TS;
                    byte t = terr[ty * tw + tx];
                    int c;
                    if (t == T.DWall) c = WallPixel(terr, tw, th, tx, ty, lx, ly, x, y, seed);
                    else if (T.DungeonFloor(t)) c = FloorPixel(terr, tw, th, t, tx, ty, lx, ly, x, y, seed);
                    else c = 0x030305;
                    px[y * W + x] = c;
                }
            });
            // decals
            Rng r = new Rng(seed * 17 + 3);
            int count = W * H / 900;
            for (int n = 0; n < count; n++)
            {
                int x = 8 + r.Int(W - 16), y = 8 + r.Int(H - 16);
                byte t = terr[(y / TS) * tw + x / TS];
                if (t != T.DFloor) continue;
                int kind = r.Int(10);
                if (kind < 4)
                {
                    // crack
                    int cx = x, cy = y, len = r.Range(4, 11), dx = r.Chance(0.5f) ? 1 : -1;
                    for (int k = 0; k < len; k++)
                    {
                        if (terr[(cy / TS) * tw + cx / TS] != T.DFloor) break;
                        px[cy * W + cx] = DFloorC[0];
                        if (r.Chance(0.6f)) cx += dx; if (r.Chance(0.7f)) cy++;
                        if (cx < 1 || cy < 1 || cx >= W - 1 || cy >= H - 1) break;
                    }
                }
                else if (kind < 7)
                {
                    // rubble
                    for (int k = 0; k < 4; k++)
                    {
                        int rx = x + r.Range(-3, 3), ry = y + r.Range(-2, 2);
                        if (terr[(ry / TS) * tw + rx / TS] != T.DFloor) continue;
                        px[ry * W + rx] = DFloorC[4]; px[(ry + 1) * W + rx] = DFloorC[0];
                    }
                }
                else if (kind < 9)
                {
                    // moss blotch
                    for (int k = 0; k < 9; k++)
                    {
                        int rx = x + r.Range(-3, 3), ry = y + r.Range(-2, 2);
                        if (terr[(ry / TS) * tw + rx / TS] != T.DFloor) continue;
                        px[ry * W + rx] = r.Chance(0.5f) ? 0x1B3536 : 0x234B41;
                    }
                }
                else
                {
                    // old stain
                    for (int k = 0; k < 14; k++)
                    {
                        int rx = x + r.Range(-4, 4), ry = y + r.Range(-3, 3);
                        if (terr[(ry / TS) * tw + rx / TS] != T.DFloor) continue;
                        px[ry * W + rx] = Darker(px[ry * W + rx], 190);
                    }
                }
            }
            return px;
        }

        static byte TerrAt(byte[] terr, int tw, int th, int tx, int ty)
        {
            if (tx < 0 || ty < 0 || tx >= tw || ty >= th) return T.Void;
            return terr[ty * tw + tx];
        }

        static bool IsOpen(byte t) { return T.DungeonFloor(t); }

        const int FaceH = 24;

        static int WallPixel(byte[] terr, int tw, int th, int tx, int ty, int lx, int ly, int x, int y, int seed)
        {
            byte s1 = TerrAt(terr, tw, th, tx, ty + 1), s2 = TerrAt(terr, tw, th, tx, ty + 2);
            int faceRow = -1;   // row index inside the brick face (0 = top)
            if (IsOpen(s1)) faceRow = (FaceH - TS) + ly;
            else if (s1 == T.DWall && IsOpen(s2) && ly >= TS - (FaceH - TS)) faceRow = ly - (TS - (FaceH - TS));
            if (faceRow >= 0)
            {
                if (faceRow < 2) return faceRow == 0 ? DWallC[5] : DWallC[4];          // lit cap
                int by = faceRow - 2, row = by / 5, off = (row & 1) * 5;
                int bx = (x + off) % 10;
                if (by % 5 == 4 || bx == 9) return DWallC[1];
                float h = Noise.H((x + off) / 10, row + ty * 7, seed + 80);
                int c = h < 0.25f ? DWallC[2] : (h < 0.85f ? DWallC[3] : DWallC[4]);
                if (by % 5 == 0 && h > 0.3f) c = Lighter(c, 10);
                // grime towards the floor
                int f = 256 - (faceRow * 90 / FaceH);
                c = Darker(c, f);
                if (Noise.Fbm(x * 0.09f, y * 0.05f, seed + 81, 2) > 0.66f) c = Darker(c, 170);
                return c;
            }
            // wall top
            int ct = DWallC[0];
            bool nOpen = TerrAt(terr, tw, th, tx, ty - 1) != T.DWall;
            bool wOpen = TerrAt(terr, tw, th, tx - 1, ty) != T.DWall;
            bool eOpen = TerrAt(terr, tw, th, tx + 1, ty) != T.DWall;
            if ((nOpen && ly < 2) || (wOpen && lx < 2) || (eOpen && lx > 13)) ct = DWallC[2];
            else if ((nOpen && ly < 3) || (wOpen && lx < 3) || (eOpen && lx > 12)) ct = DWallC[1];
            else if (((x / 8 + y / 6) & 1) == 0 && Noise.H(x / 8, y / 6, seed + 82) < 0.3f) ct = DWallC[1];
            return ct;
        }

        static int FloorPixel(byte[] terr, int tw, int th, byte t, int tx, int ty, int lx, int ly, int x, int y, int seed)
        {
            int c;
            if (t == T.DCarpet)
            {
                bool eL = TerrAt(terr, tw, th, tx - 1, ty) != T.DCarpet, eR = TerrAt(terr, tw, th, tx + 1, ty) != T.DCarpet;
                bool eU = TerrAt(terr, tw, th, tx, ty - 1) != T.DCarpet, eD = TerrAt(terr, tw, th, tx, ty + 1) != T.DCarpet;
                c = (((x + y) & 3) == 0) ? 0x4E1326 : 0x5E162C;
                if (((x - y) & 7) == 0) c = 0x6E1730;
                if ((eL && lx < 3) || (eR && lx > 12) || (eU && ly < 3) || (eD && ly > 12))
                {
                    bool gold = (eL && lx == 1) || (eR && lx == 14) || (eU && ly == 1) || (eD && ly == 14);
                    c = gold ? 0x7A5A2C : 0x3F1022;
                }
                if (Noise.Fbm(x * 0.08f, y * 0.08f, seed + 90, 2) > 0.68f) c = Darker(c, 150);   // worn patches
            }
            else
            {
                int hsh = (int)(Noise.H(tx, ty, seed + 91) * 100);
                int split = hsh < 40 ? 0 : (hsh < 65 ? 1 : (hsh < 85 ? 2 : 3));     // whole, vertical halves, horizontal halves, quarters
                int sx = (split == 1 || split == 3) ? lx % 8 : lx, sy = (split == 2 || split == 3) ? ly % 8 : ly;
                int sub = ((split == 1 || split == 3) ? lx / 8 : 0) + ((split == 2 || split == 3) ? (ly / 8) * 2 : 0);
                if (sx == 0 || sy == 0) c = DFloorC[0];
                else
                {
                    float h = Noise.H(tx * 4 + sub, ty, seed + 92);
                    c = h < 0.3f ? DFloorC[1] : (h < 0.8f ? DFloorC[2] : DFloorC[3]);
                    if ((sx == 1 || sy == 1) && h > 0.2f) c = Lighter(c, 9);
                    if (Noise.H(x, y, seed + 93) < 0.03f) c = Darker(c, 215);
                }
                if (t == T.DRitual)
                {
                    // faint glowing sigil lines on the boss floor
                    float n = Noise.Fbm(x * 0.03f, y * 0.03f, seed + 94, 2);
                    if (Math.Abs(n - 0.5f) < 0.012f) c = 0x3A2E5E;
                }
            }
            // contact shadows from walls
            byte n1 = TerrAt(terr, tw, th, tx, ty - 1);
            if (n1 == T.DWall)
            {
                if (ly < 7) c = Darker(c, 120 + ly * 18 + (int)(Cv.Bayer(x, y) * 14));
            }
            if (TerrAt(terr, tw, th, tx - 1, ty) == T.DWall && lx < 3) c = Darker(c, 170 + lx * 25);
            if (TerrAt(terr, tw, th, tx + 1, ty) == T.DWall && lx > 12) c = Darker(c, 170 + (15 - lx) * 25);
            return c;
        }
    }
}
