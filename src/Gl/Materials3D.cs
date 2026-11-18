using System;
using System.Collections.Generic;
using System.Reflection;

namespace Vesperfell
{
    /// <summary>
    /// Pixel-art surface textures for the 3D models (one layer of a texture array each, tinted by vertex colour),
    /// the drifting-mist noise, and an atlas of every sprite for cards, icons and anything without a model.
    /// </summary>
    internal static unsafe class Mat3
    {
        public const int S = 32;                    // texels per layer side (1 texel = 1 world unit at Tex = 1/32)
        public const byte Plank = 1, Bark = 2, Brick = 3, Rock = 4, Slate = 5, Tile = 6, Plaster = 7, Thatch = 8, Leaf = 9, Cloth = 10,
            Metal = 11, Cobble = 12, Moss = 13, DBrick = 14, Books = 15, Carpet = 16, Timber = 17, Straw = 18, Bone = 19, Rune = 20,
            Needle = 21, Fur = 22, Count = 23;
        public const byte AtlasLayer = 255;

        static float Rnd(int x, int y, int seed) { return (Noise.Hash(x, y, seed) & 0xFFFF) / 65535f; }
        static float N(float x, float y, int period, int seed) { return Noise.Tile(x, y, period, seed); }

        static int G(float v) { int c = M.Clamp((int)(v * 255), 0, 255); return (0xFF << 24) | (c << 16) | (c << 8) | c; }
        static int C(float r, float g, float b) { return (0xFF << 24) | (M.Clamp((int)(r * 255), 0, 255) << 16) | (M.Clamp((int)(g * 255), 0, 255) << 8) | M.Clamp((int)(b * 255), 0, 255); }

        static int[] Layer(int id)
        {
            int[] px = new int[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float fx = x / (float)S, fy = y / (float)S;
                    float n = N(fx * 4, fy * 4, 4, id * 31 + 1), fine = Rnd(x, y, id * 7 + 3);
                    float v = 1;
                    switch (id)
                    {
                        case Plank:
                            {
                                int row = y / 8, off = (row * 13) % 32;
                                bool seam = (y % 8) == 7 || ((x + off) % 32) == 0;
                                float grain = 0.86f + 0.1f * M.Sin((x + row * 11) * 0.7f + N(fx * 8, fy, 8, 77 + row) * 6) + fine * 0.06f;
                                v = seam ? 0.5f : grain - (row & 1) * 0.06f;
                            }
                            break;
                        case Bark:
                            {
                                float stripe = N(fx * 6, fy * 1.5f, 6, 51) * 0.5f + N(fx * 12, fy * 2, 12, 52) * 0.5f;
                                v = 0.62f + stripe * 0.38f + fine * 0.05f;
                                if (((x * 7 + (int)(fy * 9)) % 9) == 0) v *= 0.7f;
                            }
                            break;
                        case Brick:
                        case DBrick:
                            {
                                int bh = id == Brick ? 8 : 10, bw = id == Brick ? 16 : 20;
                                int row = y / bh, off = (row & 1) * (bw / 2);
                                bool mortar = (y % bh) == 0 || ((x + off) % bw) == 0;
                                float block = 0.78f + Rnd((x + off) / bw, row, id) * 0.2f;
                                v = mortar ? 0.42f : block + fine * 0.08f - N(fx * 8, fy * 8, 8, id) * 0.08f;
                                if (!mortar && ((y % bh) == 1 || ((x + off) % bw) == 1)) v += 0.07f;
                            }
                            break;
                        case Rock:
                            v = 0.7f + n * 0.25f + fine * 0.1f - (N(fx * 8, fy * 8, 8, 9) > 0.72f ? 0.15f : 0);
                            break;
                        case Slate:
                        case Tile:
                            {
                                int rh = id == Slate ? 6 : 8, rw = id == Slate ? 8 : 8;
                                int row = y / rh, off = (row & 1) * (rw / 2);
                                int ly = y % rh, lx = (x + off) % rw;
                                float shade = 0.95f - ly * (id == Slate ? 0.05f : 0.06f);
                                if (id == Tile) { float cx = lx - rw / 2f + 0.5f; if (ly > rh - 3 && Math.Abs(cx) > rw / 2f - 1.5f) shade *= 0.6f; }
                                v = (lx == 0 && id == Slate) || ly == rh - 1 ? 0.5f : shade - Rnd((x + off) / rw, row, id) * 0.12f;
                            }
                            break;
                        case Plaster:
                            v = 0.88f + n * 0.1f + fine * 0.04f - (N(fx * 3, fy * 3, 3, 11) > 0.7f ? 0.08f : 0);
                            break;
                        case Thatch:
                        case Straw:
                            v = 0.72f + 0.22f * M.Sin(x * 2.1f + N(fx * 4, fy * 8, 4, 13) * 9f) * 0.5f + fine * 0.18f;
                            if ((y % 10) == 9) v *= 0.75f;
                            break;
                        case Leaf:
                            {
                                float b = N(fx * 6, fy * 6, 6, 21);
                                v = 0.62f + b * 0.45f + fine * 0.12f;
                                if (Rnd(x / 2, y / 2, 22) > 0.86f) v += 0.18f;
                            }
                            break;
                        case Needle:
                            v = 0.62f + 0.3f * (((x + y * 2) % 5) < 2 ? 1 : 0) * N(fx * 4, fy * 4, 4, 23) + fine * 0.15f;
                            break;
                        case Cloth:
                            v = 0.86f + (((x + y) & 1) == 0 ? 0.06f : -0.04f) + n * 0.06f;
                            break;
                        case Metal:
                            v = 0.8f + N(fx * 1, fy * 16, 1, 31) * 0.18f + fine * 0.05f;
                            break;
                        case Cobble:
                            {
                                float c = N(fx * 5, fy * 5, 5, 41);
                                v = c > 0.32f ? 0.72f + c * 0.3f : 0.42f;
                            }
                            break;
                        case Moss:
                            v = 0.7f + n * 0.3f + fine * 0.12f;
                            break;
                        case Books:
                            {
                                int col = x / 3;
                                float hue = Rnd(col, y / 16, 5);
                                bool gap = (x % 3) == 2 || (y % 16) == 15 || (y % 16) < 2 + (int)(Rnd(col, y / 16, 6) * 3);
                                if (gap) px[y * S + x] = G(0.22f);
                                else
                                {
                                    float r = 0.5f + 0.5f * M.Sin(hue * 6.28f), gg = 0.5f + 0.5f * M.Sin(hue * 6.28f + 2.1f), bb = 0.5f + 0.5f * M.Sin(hue * 6.28f + 4.2f);
                                    float k = (y % 16) == 7 ? 1.25f : 1f;
                                    px[y * S + x] = C((0.35f + r * 0.5f) * k, (0.3f + gg * 0.4f) * k, (0.3f + bb * 0.45f) * k);
                                }
                                continue;
                            }
                        case Carpet:
                            {
                                int cx = Math.Abs(x - 16), cy = Math.Abs(y - 16);
                                bool border = x < 3 || x > 28;
                                bool motif = ((cx + cy) % 8) < 2;
                                v = border ? 0.62f : (motif ? 1.0f : 0.8f);
                                v += fine * 0.05f;
                            }
                            break;
                        case Timber:
                            v = 0.66f + 0.14f * M.Sin(x * 0.9f + N(fx, fy * 6, 1, 61) * 5) + fine * 0.05f;
                            break;
                        case Bone:
                            v = 0.88f + n * 0.08f + fine * 0.05f;
                            break;
                        case Rune:
                            {
                                v = 0.72f + n * 0.22f + fine * 0.08f;
                                bool carve = (x == 15 || x == 16) && y > 5 && y < 27 || (y == 11 && x > 9 && x < 23) || (Math.Abs(x - 16) == Math.Abs(y - 20) && y > 15 && y < 26);
                                if (carve) v = 0.35f;
                            }
                            break;
                        case Fur:
                            v = 0.72f + 0.25f * N(fx * 10, fy * 3, 10, 71) + fine * 0.1f;
                            break;
                    }
                    px[y * S + x] = G(v);
                }
            return px;
        }

        /// <summary>The mean brightness of a layer's old 32-texel art (the vertex colours of every model were tuned against it).</summary>
        public static float OldLum(int id)
        {
            if (id == 0) return 1;
            int[] px = Layer(id);
            double s = 0;
            for (int i = 0; i < px.Length; i++) { int c = px[i]; s += ((c >> 16) & 255) * 0.3 + ((c >> 8) & 255) * 0.59 + (c & 255) * 0.11; }
            return (float)(s / px.Length / 255.0);
        }

        /// <summary>
        /// Uploads the painted surfaces (see MatGen): the model layers' colour+height and normal+gloss, and the same pair
        /// for the ground. Filtered smoothly, mipmapped, and anisotropic where the driver offers it.
        /// </summary>
        public static void BuildArrays(out uint alb, out uint nrm, out uint gAlb, out uint gNrm)
        {
            MatGen.Build();
            float aniso = 0;
            GL.GetFloatv(GL.MAX_TEXTURE_MAX_ANISOTROPY, &aniso);
            for (int k = 0; k < 4 && GL.GetError() != GL.NO_ERROR; k++) { }      // no anisotropic filtering: the driver just says so
            alb = Upload(MatGen.ModelAlb, MatGen.MS, aniso);
            nrm = Upload(MatGen.ModelNrm, MatGen.MS, aniso);
            gAlb = Upload(MatGen.GroundAlb, MatGen.GS, aniso);
            gNrm = Upload(MatGen.GroundNrm, MatGen.GS, aniso);
        }

        static uint Upload(int[][] layers, int size, float aniso)
        {
            uint t = Tex.Gen();
            GL.BindTexture(GL.TEXTURE_2D_ARRAY, t);
            GL.PixelStorei(GL.UNPACK_ALIGNMENT, 4);
            GL.TexImage3D(GL.TEXTURE_2D_ARRAY, 0, GL.RGBA8, size, size, layers.Length, 0, GL.BGRA, GL.UNSIGNED_BYTE, IntPtr.Zero);
            for (int i = 0; i < layers.Length; i++)
                fixed (int* p = layers[i]) GL.TexSubImage3D(GL.TEXTURE_2D_ARRAY, 0, 0, 0, i, size, size, 1, GL.BGRA, GL.UNSIGNED_BYTE, (IntPtr)p);
            GL.TexParameteri(GL.TEXTURE_2D_ARRAY, GL.TEXTURE_MIN_FILTER, GL.LINEAR_MIPMAP_LINEAR);
            GL.TexParameteri(GL.TEXTURE_2D_ARRAY, GL.TEXTURE_MAG_FILTER, GL.LINEAR);
            GL.TexParameteri(GL.TEXTURE_2D_ARRAY, GL.TEXTURE_WRAP_S, GL.REPEAT);
            GL.TexParameteri(GL.TEXTURE_2D_ARRAY, GL.TEXTURE_WRAP_T, GL.REPEAT);
            GL.GenerateMipmap(GL.TEXTURE_2D_ARRAY);
            if (aniso >= 2)
            {
                GL.TexParameterf(GL.TEXTURE_2D_ARRAY, GL.TEXTURE_MAX_ANISOTROPY, Math.Min(8f, aniso));
                for (int k = 0; k < 4 && GL.GetError() != GL.NO_ERROR; k++) { }
            }
            return t;
        }

        static int B8(float v) { return M.Clamp((int)(v * 255), 0, 255); }

        /// <summary>The tiling noise the mist drifts on (the same recipe as the 2D fog).</summary>
        public static uint BuildNoise()
        {
            int[] px = new int[256 * 256];
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float n = Noise.Tile(x / 32f, y / 32f, 8, 901) * 0.55f + Noise.Tile(x / 16f, y / 16f, 16, 902) * 0.3f + Noise.Tile(x / 8f, y / 8f, 32, 903) * 0.15f;
                    // green: broad patches (which copy of a ground layer shows, the ground's light and dark); blue: smooth swirls (sigils)
                    float g = Noise.Tile(x / 32f, y / 32f, 8, 911) * 0.65f + Noise.Tile(x / 16f, y / 16f, 16, 912) * 0.35f;
                    float b = Noise.Tile(x / 64f, y / 64f, 4, 921) * 0.7f + Noise.Tile(x / 32f, y / 32f, 8, 922) * 0.3f;
                    px[y * 256 + x] = (0xFF << 24) | (B8(n) << 16) | (B8(g) << 8) | B8(b);
                }
            uint t = Tex.FromArgb(px, 256, 256, false, true, true);
            return t;
        }

        // sprite atlas
        public sealed class AtlasRect { public float U0, V0, U1, V1; public int W, H, OX, OY; }
        public static readonly Dictionary<Sprite, AtlasRect> Rects = new Dictionary<Sprite, AtlasRect>();
        public static int AtlasW = 2048, AtlasH = 2048;

        static void Collect(object o, List<Sprite> into, HashSet<Sprite> seen)
        {
            if (o == null) return;
            Sprite s = o as Sprite;
            if (s != null) { if (seen.Add(s)) into.Add(s); return; }
            ActorSprites a = o as ActorSprites;
            if (a != null) { Collect(a.Idle, into, seen); Collect(a.Run, into, seen); return; }
            Array arr = o as Array;
            if (arr != null) foreach (object e in arr) Collect(e, into, seen);
        }

        /// <summary>Packs every sprite the game owns into one texture (shelf packing, tallest first).</summary>
        public static uint BuildAtlas()
        {
            List<Sprite> all = new List<Sprite>();
            HashSet<Sprite> seen = new HashSet<Sprite>();
            foreach (FieldInfo f in typeof(Assets).GetFields(BindingFlags.Public | BindingFlags.Static)) Collect(f.GetValue(null), all, seen);
            all.Sort(delegate(Sprite a, Sprite b) { return b.H != a.H ? b.H.CompareTo(a.H) : b.W.CompareTo(a.W); });
            int[] px = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int x = 1, y = 1, rowH = 0;
                bool fit = true;
                Rects.Clear();
                List<int[]> places = new List<int[]>();
                foreach (Sprite s in all)
                {
                    if (x + s.W + 1 > AtlasW) { x = 1; y += rowH + 1; rowH = 0; }
                    if (y + s.H + 1 > AtlasH) { fit = false; break; }
                    places.Add(new[] { x, y });
                    AtlasRect r = new AtlasRect();
                    r.U0 = x / (float)AtlasW; r.V0 = y / (float)AtlasH; r.U1 = (x + s.W) / (float)AtlasW; r.V1 = (y + s.H) / (float)AtlasH;
                    r.W = s.W; r.H = s.H; r.OX = s.OX; r.OY = s.OY;
                    Rects[s] = r;
                    x += s.W + 1; rowH = Math.Max(rowH, s.H);
                }
                if (!fit) { AtlasH *= 2; continue; }
                px = new int[AtlasW * AtlasH];
                for (int i = 0; i < all.Count; i++)
                {
                    Sprite s = all[i]; int ox = places[i][0], oy = places[i][1];
                    for (int j = 0; j < s.H; j++)
                        for (int k = 0; k < s.W; k++)
                        {
                            int c = s.Px[j * s.W + k];
                            int a = (c >> 24) & 255;
                            px[(oy + j) * AtlasW + ox + k] = a == 0 ? 0 : c;
                        }
                }
                break;
            }
            if (px == null) px = new int[AtlasW * AtlasH];
            return Tex.FromArgb(px, AtlasW, AtlasH, true, false, false);
        }
    }
}
