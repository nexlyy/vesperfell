using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Vesperfell
{
    /// <summary>A square of the world: its terrain mesh and one merged mesh of every model standing in it.</summary>
    internal sealed class Chunk
    {
        public float X0, Z0, X1, Z1, MinY = -10, MaxY = 40;
        public int Gx0, Gz0, Gx1, Gz1;              // vertex grid range
        public GpuMesh Terrain, Props;
        public readonly List<Prop> List = new List<Prop>();
        public readonly List<Sprite> Spr = new List<Sprite>();
        public readonly List<bool> Hid = new List<bool>();
        public readonly List<Prop> Next = new List<Prop>();     // scratch: what the chunk holds after the map changed
        public bool Dirty = true;
    }

    /// <summary>
    /// Everything static about one map in 3D: a height field shaped from the terrain types (river beds sink, thickets
    /// rise, roads flatten), the painted ground as its texture, water, bridges, dungeon walls, and the prop models.
    /// </summary>
    internal sealed class MapGeo
    {
        public const float WaterY = -2.4f;
        public const int CC = 32;                  // cells per chunk side
        public readonly Map M;
        readonly Renderer3D R;
        public readonly int Margin;                // cells of filler scenery around the map
        public readonly int GW, GH;                // vertex grid (cells + 1)
        public readonly float[] Hgt;
        /// <summary>
        /// The ground under every spot, for the world shader to blend its materials: red = layer code (MatGen.G* plus flags:
        /// 16 sigils, 32 boards laid along z, 128 laid by hand), green = how enclosed the spot is (darkens by walls and brush).
        /// </summary>
        public uint TypeTex;
        public float TypeScale, TypeOffX, TypeOffZ;     // world x/z to type texels
        /// <summary>Per ground layer, the factor that brings its painted colour to this map's palette (r, g, b).</summary>
        public readonly float[] GTint = new float[MatGen.GCount * 3];
        public Chunk[] Chunks; readonly int ncx, ncz;
        public GpuMesh Water;
        readonly float ox, oz;                      // world position of grid vertex (0, 0)
        int seenVersion = -1;
        readonly byte[] wallH;                       // dungeon wall height per cell (0 = floor)
        /// <summary>Walls built for walking through at eye height: every dungeon wall reaches a ceiling closed over the rooms.</summary>
        public readonly bool TallWalls;
        public readonly float Ceil;                  // dungeon ceiling height, 0 under open sky
        public const int FpWall = 84;
        readonly bool[] bed;                         // river bed under water and bridges
        static readonly MeshB build = new MeshB();

        public MapGeo(Renderer3D r, Map m)
        {
            R = r; M = m;
            TallWalls = m.Dungeon && r.FpWorld;
            Ceil = TallWalls ? FpWall : 0;
            Margin = m.Dungeon ? 8 : 32;
            GW = m.CW + Margin * 2 + 1; GH = m.CH + Margin * 2 + 1;
            ox = -Margin * Map.CS; oz = -Margin * Map.CS;
            Hgt = new float[GW * GH];
            bed = new bool[m.CW * m.CH];
            wallH = new byte[m.CW * m.CH];
            Shape();
            BuildTypes();
            BuildTints();
            ncx = (GW - 1 + CC - 1) / CC; ncz = (GH - 1 + CC - 1) / CC;
            Chunks = new Chunk[ncx * ncz];
            for (int cz = 0; cz < ncz; cz++)
                for (int cx = 0; cx < ncx; cx++)
                {
                    Chunk c = new Chunk();
                    c.Gx0 = cx * CC; c.Gz0 = cz * CC; c.Gx1 = Math.Min(GW - 1, c.Gx0 + CC); c.Gz1 = Math.Min(GH - 1, c.Gz0 + CC);
                    c.X0 = ox + c.Gx0 * Map.CS; c.Z0 = oz + c.Gz0 * Map.CS; c.X1 = ox + c.Gx1 * Map.CS; c.Z1 = oz + c.Gz1 * Map.CS;
                    Chunks[cz * ncx + cx] = c;
                    BuildTerrain(c);
                }
            BuildWater();
            Refresh();
        }

        // heights
        byte CellAt(int cx, int cz)
        {
            if (cx < 0 || cz < 0 || cx >= M.CW || cz >= M.CH) return M.Dungeon ? T.DWall : T.Thicket;
            return M.Cell[cz * M.CW + cx];
        }

        bool Bed(int cx, int cz)
        {
            if (cx < 0 || cz < 0 || cx >= M.CW || cz >= M.CH) return false;
            return bed[cz * M.CW + cx];
        }

        public int WallH(int cx, int cz)
        {
            if (cx < 0 || cz < 0 || cx >= M.CW || cz >= M.CH) return M.Dungeon ? (TallWalls ? FpWall : 34) : 0;
            return wallH[cz * M.CW + cx];
        }

        void Shape()
        {
            Map m = M;
            // river beds: open water, and bridges where they cross it
            for (int cz = 0; cz < m.CH; cz++)
                for (int cx = 0; cx < m.CW; cx++)
                {
                    byte t = m.Cell[cz * m.CW + cx];
                    if (t == T.Water) bed[cz * m.CW + cx] = true;
                    else if (t == T.Plank || t == T.PlankV)
                    {
                        bool near = false;
                        for (int dz = -3; dz <= 3 && !near; dz++)
                            for (int dx = -3; dx <= 3 && !near; dx++)
                                if (CellAt(cx + dx, cz + dz) == T.Water) near = true;
                        bed[cz * m.CW + cx] = near;
                    }
                }
            // dungeon walls: tall at the back of a room, low where they stand between the camera and a room
            if (m.Dungeon)
                for (int cz = 0; cz < m.CH; cz++)
                    for (int cx = 0; cx < m.CW; cx++)
                    {
                        if (m.Cell[cz * m.CW + cx] != T.DWall) continue;
                        bool roomNorth = false;
                        for (int k = 1; k <= 5 && !roomNorth; k++)
                        {
                            byte t = CellAt(cx, cz - k);
                            if (t != T.DWall && t != T.Void) roomNorth = true;
                        }
                        wallH[cz * m.CW + cx] = (byte)(TallWalls ? FpWall : (roomNorth ? 7 : 34));
                    }

            for (int gz = 0; gz < GH; gz++)
                for (int gx = 0; gx < GW; gx++)
                {
                    float wx = ox + gx * Map.CS, wz = oz + gz * Map.CS;
                    int cx = gx - Margin, cz = gz - Margin;      // the vertex is the top-left corner of cell (cx, cz)
                    float n = (Noise.Value(wx / 170f, wz / 170f, 11) - 0.5f) * 5.5f + (Noise.Value(wx / 52f, wz / 52f, 12) - 0.5f) * 1.8f;
                    float sum = 0; int water = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        int qx = cx - 1 + (k & 1), qz = cz - 1 + (k >> 1);
                        byte t = CellAt(qx, qz);
                        if (Bed(qx, qz)) { water++; sum += -7.5f; continue; }
                        sum += Base(t, n, qx < 0 || qz < 0 || qx >= m.CW || qz >= m.CH);
                    }
                    Hgt[gz * GW + gx] = sum / 4f;
                }
            // soften twice, but keep river beds and dungeon floors where they are
            float[] tmp = new float[Hgt.Length];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int gz = 0; gz < GH; gz++)
                    for (int gx = 0; gx < GW; gx++)
                    {
                        if (m.Dungeon) { tmp[gz * GW + gx] = Hgt[gz * GW + gx]; continue; }
                        float s = 0; int c = 0;
                        for (int dz = -1; dz <= 1; dz++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int x = gx + dx, z = gz + dz;
                                if (x < 0 || z < 0 || x >= GW || z >= GH) continue;
                                float w = dx == 0 && dz == 0 ? 2 : 1;
                                s += Hgt[z * GW + x] * w; c += (int)w;
                            }
                        tmp[gz * GW + gx] = s / c;
                    }
                Array.Copy(tmp, Hgt, Hgt.Length);
            }
        }

        static float Base(byte t, float n, bool outside)
        {
            switch (t)
            {
                case T.Thicket: return 3.5f + n * 1.5f + (outside ? 2 : 0);
                case T.Grass: case T.PaleGrass: return n;
                case T.Forest: return n * 1.15f + 0.6f;
                case T.Ash: case T.Stone: return n * 1.25f;
                case T.Dirt: case T.Mud: return n * 0.35f - 0.5f;
                case T.Cobble: return n * 0.22f - 0.25f;
                case T.Plank: case T.PlankV: return n * 0.2f;
                default: return 0;
            }
        }

        // ground materials
        static bool Crisp(byte t) { return t == T.Plank || t == T.PlankV || t == T.DCarpet; }

        static int Code(byte t, bool dungeon)
        {
            switch (t)
            {
                case T.Grass: return MatGen.GGrass;
                case T.Forest: return MatGen.GForest;
                case T.PaleGrass: return MatGen.GPale;
                case T.Ash: return MatGen.GAsh;
                case T.Thicket: return MatGen.GThicket;
                case T.Dirt: return MatGen.GDirt;
                case T.Mud: return MatGen.GMud;
                case T.Water: return MatGen.GBed;
                case T.Cobble: return MatGen.GCobble | 128;
                case T.Stone: return MatGen.GStone | 128;
                case T.Plank: return MatGen.GPlank | 128;
                case T.PlankV: return MatGen.GPlank | 128 | 32;
                case T.DCarpet: return MatGen.GCarpet | 128;
                case T.DRitual: return MatGen.GDFloor | 128 | 16;
                default: return dungeon ? (MatGen.GDFloor | 128) : MatGen.GThicket;
            }
        }

        float DungeonAo(int cx, int cz)
        {
            byte t = CellAt(cx, cz);
            if (t == T.DWall || t == T.Void) return 0.42f;
            float o = 1;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    byte n = CellAt(cx + dx, cz + dz);
                    if (n == T.DWall || n == T.Void) o -= (dx == 0 || dz == 0) ? 0.09f : 0.05f;
                }
            return Math.Max(0.55f, o);
        }

        /// <summary>
        /// Which ground lies where: outdoors four texels to a cell, with the borders of natural ground wandering by the same
        /// noise the painted 2D ground uses; boards and carpets keep straight edges. Indoors one texel to a cell.
        /// </summary>
        void BuildTypes()
        {
            bool dg = M.Dungeon;
            int per = dg ? 1 : 4;
            int w = (GW - 1) * per, h = (GH - 1) * per;
            float step = Map.CS / (float)per;
            int seed = 4242;
            int[] px = new int[w * h];
            Parallel.For(0, h, delegate(int ty)
            {
                for (int tx = 0; tx < w; tx++)
                {
                    float wx = ox + (tx + 0.5f) * step, wz = oz + (ty + 0.5f) * step;
                    int cx = (int)Math.Floor(wx / Map.CS), cz = (int)Math.Floor(wz / Map.CS);
                    byte raw = CellAt(cx, cz), t = raw;
                    if (!dg && !Crisp(raw))
                    {
                        float nx = wx * 0.045f, nz = wz * 0.045f;
                        float qx = wx + (Noise.Value(nx, nz, seed + 11) - 0.5f) * 15f + (Noise.Value(wx * 0.17f, wz * 0.17f, seed + 12) - 0.5f) * 5f;
                        float qz = wz + (Noise.Value(nx, nz, seed + 13) - 0.5f) * 15f + (Noise.Value(wx * 0.17f, wz * 0.17f, seed + 14) - 0.5f) * 5f;
                        t = CellAt((int)Math.Floor(qx / Map.CS), (int)Math.Floor(qz / Map.CS));
                        if (Crisp(t)) t = raw;
                    }
                    float ao = dg ? DungeonAo(cx, cz) : (t == T.Thicket ? 0.82f : 1f);
                    px[ty * w + tx] = (0xFF << 24) | (Code(t, dg) << 16) | (Vesperfell.M.Clamp((int)(ao * 255), 0, 255) << 8);
                }
            });
            TypeTex = Tex.FromArgb(px, w, h, false, false, false);
            TypeScale = per / (float)Map.CS;
            TypeOffX = -ox * TypeScale; TypeOffZ = -oz * TypeScale;
        }

        /// <summary>Tints each ground layer to the average colour this map's painted ground has where that ground lies.</summary>
        void BuildTints()
        {
            Map m = M;
            int n = MatGen.GCount;
            double[] sum = new double[n * 3]; int[] cnt = new int[n];
            if (m.Ground != null && m.PW == m.CW * Map.CS && m.PH == m.CH * Map.CS)
                for (int y = 0; y < m.PH; y += 2)
                {
                    int row = (y / Map.CS) * m.CW;
                    for (int x = 0; x < m.PW; x += 2)
                    {
                        byte t = m.Cell[row + x / Map.CS];
                        if (m.Dungeon && (t == T.DWall || t == T.Void)) continue;
                        int k = Code(t, m.Dungeon) & 15, c = m.Ground[y * m.PW + x];
                        sum[k * 3] += (c >> 16) & 255; sum[k * 3 + 1] += (c >> 8) & 255; sum[k * 3 + 2] += c & 255; cnt[k]++;
                    }
                }
            for (int k = 0; k < n; k++)
                for (int ch = 0; ch < 3; ch++)
                {
                    float target;
                    if (k == MatGen.GBed) target = (ch == 0 ? 0x2C : (ch == 1 ? 0x2A : 0x22)) / 255f;     // silt seen through the water
                    else if (cnt[k] > 40) target = (float)(sum[k * 3 + ch] / cnt[k] / 255.0);
                    else target = MatGen.GroundMean[k * 3 + ch];
                    GTint[k * 3 + ch] = Vesperfell.M.Clamp(target / Math.Max(0.01f, MatGen.GroundMean[k * 3 + ch]), 0.25f, 4f);
                }
        }

        /// <summary>Ground height under a map position (bilinear over the vertex grid).</summary>
        public float HeightAt(float x, float y)
        {
            float fx = (x - ox) / Map.CS, fz = (y - oz) / Map.CS;
            int ix = (int)Math.Floor(fx), iz = (int)Math.Floor(fz);
            if (ix < 0) { ix = 0; fx = 0; } if (iz < 0) { iz = 0; fz = 0; }
            if (ix >= GW - 1) { ix = GW - 2; fx = GW - 1; } if (iz >= GH - 1) { iz = GH - 2; fz = GH - 1; }
            float tx = fx - ix, tz = fz - iz;
            float a = Hgt[iz * GW + ix], b = Hgt[iz * GW + ix + 1], c = Hgt[(iz + 1) * GW + ix], d = Hgt[(iz + 1) * GW + ix + 1];
            float h = a + (b - a) * tx + (c - a) * tz + (a - b - c + d) * tx * tz;
            // stand on bridge decks rather than in the river under them
            int cx = (int)Math.Floor(x / Map.CS), cz = (int)Math.Floor(y / Map.CS);
            byte t = CellAt(cx, cz);
            if ((t == T.Plank || t == T.PlankV) && Bed(cx, cz)) h = Math.Max(h, DeckY);
            return h;
        }

        public const float DeckY = 1.4f;

        V3 Normal(int gx, int gz)
        {
            float l = Hgt[gz * GW + Math.Max(0, gx - 1)], r = Hgt[gz * GW + Math.Min(GW - 1, gx + 1)];
            float u = Hgt[Math.Max(0, gz - 1) * GW + gx], d = Hgt[Math.Min(GH - 1, gz + 1) * GW + gx];
            return new V3(l - r, 2 * Map.CS, u - d).Norm;
        }

        // meshes
        void BuildTerrain(Chunk c)
        {
            MeshB b = build;
            b.Clear(); b.TrackBounds = true;
            for (int gz = c.Gz0; gz < c.Gz1; gz++)
                for (int gx = c.Gx0; gx < c.Gx1; gx++)
                {
                    int cx = gx - Margin, cz = gz - Margin;
                    bool outside = cx < 0 || cz < 0 || cx >= M.CW || cz >= M.CH;
                    if (M.Dungeon && WallH(cx, cz) >= 30) continue;          // under a solid wall: never seen
                    int col = outside ? (M.Dungeon ? 0x202020 : 0x6C7480) : 0xFFFFFF;
                    TerrainQuad(b, gx, gz, col);
                }
            if (b.N > 0)
            {
                c.Terrain = new GpuMesh(); c.Terrain.Upload(b, false);
                c.MinY = b.MinY - 1; c.MaxY = b.MaxY + 1;
            }
            b.TrackBounds = false;
        }

        void TerrainQuad(MeshB b, int gx, int gz, int col)
        {
            V3 p00 = new V3(ox + gx * Map.CS, Hgt[gz * GW + gx], oz + gz * Map.CS);
            V3 p10 = new V3(p00.X + Map.CS, Hgt[gz * GW + gx + 1], p00.Z);
            V3 p01 = new V3(p00.X, Hgt[(gz + 1) * GW + gx], p00.Z + Map.CS);
            V3 p11 = new V3(p00.X + Map.CS, Hgt[(gz + 1) * GW + gx + 1], p00.Z + Map.CS);
            V3 n00 = Normal(gx, gz), n10 = Normal(gx + 1, gz), n01 = Normal(gx, gz + 1), n11 = Normal(gx + 1, gz + 1);
            Put(b, p00, n00, col); Put(b, p01, n01, col); Put(b, p11, n11, col);
            Put(b, p00, n00, col); Put(b, p11, n11, col); Put(b, p10, n10, col);
        }

        static void Put(MeshB b, V3 p, V3 n, int col)
        {
            if (b.N + 1 > b.V.Length) { Vtx[] a = new Vtx[b.V.Length * 2]; Array.Copy(b.V, a, b.N); b.V = a; }
            Vtx v = new Vtx();
            v.X = p.X; v.Y = p.Y; v.Z = p.Z;
            v.NX = (sbyte)(n.X * 127); v.NY = (sbyte)(n.Y * 127); v.NZ = (sbyte)(n.Z * 127);
            v.R = (byte)(col >> 16); v.G = (byte)(col >> 8); v.B = (byte)col;
            b.V[b.N++] = v;
            if (p.Y < b.MinY) b.MinY = p.Y; if (p.Y > b.MaxY) b.MaxY = p.Y;
        }

        void BuildWater()
        {
            MeshB b = build;
            b.Clear();
            for (int cz = -2; cz < M.CH + 2; cz++)
                for (int cx = -2; cx < M.CW + 2; cx++)
                {
                    bool w = Bed(cx, cz) || (cx < 0 || cz < 0 || cx >= M.CW || cz >= M.CH ? false : false);
                    if (!w)
                    {
                        // a lip of water under the banks, so the shoreline is where the ground rises out of it
                        bool near = false;
                        for (int k = 0; k < 4 && !near; k++) if (Bed(cx + (k == 0 ? 1 : (k == 1 ? -1 : 0)), cz + (k == 2 ? 1 : (k == 3 ? -1 : 0)))) near = true;
                        if (!near) continue;
                    }
                    float x0 = cx * Map.CS, z0 = cz * Map.CS;
                    b.Col = 0xFFFFFF; b.Em = 255;
                    b.Quad(new V3(x0, WaterY, z0 + Map.CS), new V3(x0 + Map.CS, WaterY, z0 + Map.CS), new V3(x0 + Map.CS, WaterY, z0), new V3(x0, WaterY, z0));
                }
            if (b.N > 0) { Water = new GpuMesh(); Water.Upload(b, false); }
            b.Em = 0;
        }

        // props
        Chunk ChunkAt(float x, float y)
        {
            int cx = Vesperfell.M.Clamp((int)((x - ox) / Map.CS) / CC, 0, ncx - 1), cz = Vesperfell.M.Clamp((int)((y - oz) / Map.CS) / CC, 0, ncz - 1);
            return Chunks[cz * ncx + cx];
        }

        /// <summary>Notices props that were added, removed, hidden or changed sprite, and rebuilds those chunks.</summary>
        public void Refresh()
        {
            if (M.Version != seenVersion)
            {
                seenVersion = M.Version;
                // sort the props into chunks again, but rebuild only the chunks whose contents changed: a barrel
                // smashed by a spell is one chunk's work, not the whole map's
                foreach (Chunk c in Chunks) c.Next.Clear();
                foreach (Prop p in M.Props) ChunkAt(p.X, p.Y).Next.Add(p);
                foreach (Chunk c in Chunks)
                {
                    bool same = c.Next.Count == c.List.Count;
                    for (int k = 0; same && k < c.Next.Count; k++) if (!ReferenceEquals(c.Next[k], c.List[k])) same = false;
                    if (same) continue;
                    c.List.Clear(); c.Spr.Clear(); c.Hid.Clear();
                    foreach (Prop p in c.Next) { c.List.Add(p); c.Spr.Add(p.Spr); c.Hid.Add(p.Hidden); }
                    c.Dirty = true;
                }
            }
            for (int i = 0; i < Chunks.Length; i++)
            {
                Chunk c = Chunks[i];
                if (!c.Dirty)
                    for (int k = 0; k < c.List.Count; k++)
                    {
                        Prop p = c.List[k];
                        if (p.Spr != c.Spr[k] || p.Hidden != c.Hid[k]) { c.Dirty = true; break; }
                    }
                if (c.Dirty) BuildProps(c);
            }
        }

        void BuildProps(Chunk c)
        {
            c.Dirty = false;
            MeshB b = build;
            b.Clear(); b.TrackBounds = true;
            for (int k = 0; k < c.List.Count; k++)
            {
                Prop p = c.List[k];
                c.Spr[k] = p.Spr; c.Hid[k] = p.Hidden;
                if (p.Hidden || p.Spr == null) continue;
                PropModels.Place(b, p, this);
            }
            if (M.Dungeon) BuildWalls(c, b);
            else if (Margin > 0) BuildFiller(c, b);
            if (c.Props == null) c.Props = new GpuMesh();
            c.Props.Upload(b, false);
            b.TrackBounds = false;
        }

        // dungeon walls: brick sides wherever a wall cell meets something lower, dark stone on top
        void BuildWalls(Chunk c, MeshB b)
        {
            for (int gz = c.Gz0; gz < c.Gz1; gz++)
                for (int gx = c.Gx0; gx < c.Gx1; gx++)
                {
                    int cx = gx - Margin, cz = gz - Margin;
                    int h = WallH(cx, cz);
                    if (CellAt(cx, cz) != T.DWall) continue;
                    float x0 = cx * Map.CS, z0 = cz * Map.CS, x1 = x0 + Map.CS, z1 = z0 + Map.CS;
                    b.T = Xf.Id; b.Occ = (byte)(h < 30 ? 0 : 1);
                    b.Layer = Mat3.Rock; b.Col = h < 30 ? 0x3A3448 : 0x1C1824; b.Tex = 1f / 32f;
                    b.Quad(new V3(x0, h, z1), new V3(x1, h, z1), new V3(x1, h, z0), new V3(x0, h, z0));
                    b.Layer = Mat3.DBrick; b.Col = 0x8A84A0;
                    int hs = WallH(cx, cz + 1), hn = WallH(cx, cz - 1), he = WallH(cx + 1, cz), hw = WallH(cx - 1, cz);
                    if (CellAt(cx, cz + 1) != T.DWall) hs = 0; if (CellAt(cx, cz - 1) != T.DWall) hn = 0;
                    if (CellAt(cx + 1, cz) != T.DWall) he = 0; if (CellAt(cx - 1, cz) != T.DWall) hw = 0;
                    if (hs < h) b.Quad(new V3(x0, hs, z1), new V3(x1, hs, z1), new V3(x1, h, z1), new V3(x0, h, z1));
                    if (hn < h) b.Quad(new V3(x1, hn, z0), new V3(x0, hn, z0), new V3(x0, h, z0), new V3(x1, h, z0));
                    if (he < h) b.Quad(new V3(x1, he, z1), new V3(x1, he, z0), new V3(x1, h, z0), new V3(x1, h, z1));
                    if (hw < h) b.Quad(new V3(x0, hw, z0), new V3(x0, hw, z1), new V3(x0, h, z1), new V3(x0, h, z0));
                    // a lighter coping stone along the rim of the low walls
                    if (h < 30 && hn == 0)
                    {
                        b.Layer = Mat3.Brick; b.Col = 0x6A6480;
                        b.Box(x0, h, z0 - 0.5f, x1, h + 1.6f, z0 + 3f);
                    }
                }
            if (TallWalls) BuildCeiling(c, b);
            b.Occ = 0; b.Layer = 0;
        }

        // first person: a stone vault closed over every room and passage, with ribs across it and a plinth along the walls
        void BuildCeiling(Chunk c, MeshB b)
        {
            b.T = Xf.Id; b.Occ = 1; b.Tex = 1f / 32f;
            for (int gz = c.Gz0; gz < c.Gz1; gz++)
            {
                int cz = gz - Margin;
                if (cz < 0 || cz >= M.CH) continue;
                int run = -1;
                for (int gx = c.Gx0; gx <= c.Gx1; gx++)
                {
                    int cx = gx - Margin;
                    bool open = gx < c.Gx1 && cx >= 0 && cx < M.CW && CellAt(cx, cz) != T.DWall;
                    if (open && run < 0) run = cx;
                    if (!open && run >= 0)
                    {
                        float x0 = run * Map.CS, x1 = cx * Map.CS, z0 = cz * Map.CS, z1 = z0 + Map.CS;
                        b.Layer = Mat3.DBrick; b.Col = 0x4A4660;
                        b.Quad(new V3(x0, Ceil, z0), new V3(x1, Ceil, z0), new V3(x1, Ceil, z1), new V3(x0, Ceil, z1));
                        if (cz % 6 == 0)
                        {
                            b.Layer = Mat3.Rock; b.Col = 0x5A5670;
                            b.Box(x0, Ceil - 4, z0 + 2.5f, x1, Ceil, z0 + 5.5f);
                        }
                        run = -1;
                    }
                }
            }
            // a dark plinth where walls meet the floor
            for (int gz = c.Gz0; gz < c.Gz1; gz++)
                for (int gx = c.Gx0; gx < c.Gx1; gx++)
                {
                    int cx = gx - Margin, cz = gz - Margin;
                    if (cx < 0 || cz < 0 || cx >= M.CW || cz >= M.CH || CellAt(cx, cz) == T.DWall) continue;
                    float x0 = cx * Map.CS, z0 = cz * Map.CS, x1 = x0 + Map.CS, z1 = z0 + Map.CS;
                    b.Layer = Mat3.Rock; b.Col = 0x3A3648;
                    if (CellAt(cx, cz - 1) == T.DWall) b.Box(x0, 0, z0, x1, 3.2f, z0 + 1.4f);
                    if (CellAt(cx, cz + 1) == T.DWall) b.Box(x0, 0, z1 - 1.4f, x1, 3.2f, z1);
                    if (CellAt(cx - 1, cz) == T.DWall) b.Box(x0, 0, z0, x0 + 1.4f, 3.2f, z1);
                    if (CellAt(cx + 1, cz) == T.DWall) b.Box(x1 - 1.4f, 0, z0, x1, 3.2f, z1);
                }
        }

        // dark woods beyond the edge of the vale (only scenery: nothing can walk there)
        void BuildFiller(Chunk c, MeshB b)
        {
            for (int gz = c.Gz0; gz < c.Gz1; gz += 2)
                for (int gx = c.Gx0; gx < c.Gx1; gx += 2)
                {
                    int cx = gx - Margin, cz = gz - Margin;
                    if (cx >= -1 && cz >= -1 && cx <= M.CW && cz <= M.CH) continue;
                    uint h = Noise.Hash(gx, gz, 4242);
                    if ((h & 3) == 0) continue;
                    float x = cx * Map.CS + ((h >> 4) & 15) - 4, z = cz * Map.CS + ((h >> 8) & 15) - 4;
                    Sprite s = (h >> 12) % 5 < 3 ? Assets.PineDark[(int)((h >> 16) % (uint)Assets.PineDark.Length)] : Assets.OakTeal[(int)((h >> 16) % (uint)Assets.OakTeal.Length)];
                    PropModels.PlaceSprite(b, s, x, z, HeightAt(x, z), ((h >> 20) & 1) == 1, 0.9f + ((h >> 21) & 7) * 0.05f);
                }
        }

        public void Dispose()
        {
            foreach (Chunk c in Chunks) { if (c.Terrain != null) c.Terrain.Dispose(); if (c.Props != null) c.Props.Dispose(); }
            if (Water != null) Water.Dispose();
            Tex.Delete(ref TypeTex);
        }
    }
}
