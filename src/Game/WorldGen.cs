using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>
    /// Builds the vale of Vesperfell. Landmarks, roads and encounters are placed by hand (in tile coordinates);
    /// noise fills in the forests, undergrowth and ragged terrain borders between them. Fully deterministic.
    /// </summary>
    public static class WorldGen
    {
        public const int TW = 176, TH = 128, Seed = 4177;
        static Map m; static Rng r;
        static bool[] keep;                 // per cell: nothing large may grow here
        static List<float> treeX = new List<float>(), treeY = new List<float>();
        static List<int>[] treeGrid;

        /// <summary>Points the shared prop helpers at another map (used by the dungeon builder).</summary>
        internal static void Begin(Map map, Rng rng)
        {
            m = map; r = rng;
            keep = new bool[m.CW * m.CH];
            treeX.Clear(); treeY.Clear();
            treeGrid = new List<int>[m.TW * m.TH];
        }

        public static Map BuildOverworld(Action<float, string> progress)
        {
            Begin(new Map("overworld", TW, TH, false), new Rng(Seed));

            if (progress != null) progress(0.05f, "Charting the vale");
            PaintBase();
            PaintFeatures();
            for (int ty = 0; ty < TH; ty++)
                for (int tx = 0; tx < TW; tx++)
                    m.ZoneMap[ty * TW + tx] = (byte)ZoneOf(tx + 0.5f, ty + 0.5f);

            if (progress != null) progress(0.15f, "Painting the dusk");
            byte[] pt;
            m.Ground = TerrainArt.Bake(m.Cell, m.CW, m.CH, Map.CS, Seed, out pt);
            // collision follows the painted shoreline, not the coarse grid
            for (int cy = 0; cy < m.CH; cy++)
                for (int cx = 0; cx < m.CW; cx++)
                {
                    byte t = m.Cell[cy * m.CW + cx];
                    byte s = Map.TerrainSolid(t);
                    if (t != T.Plank && t != T.PlankV && t != T.Thicket)
                    {
                        int water = 0;
                        for (int y = 0; y < 8; y++)
                            for (int x = 0; x < 8; x++)
                                if (pt[(cy * 8 + y) * m.PW + cx * 8 + x] == T.Water) water++;
                        s = (byte)(water >= 34 ? 1 : 0);
                    }
                    m.Solid[cy * m.CW + cx] = s;
                }

            if (progress != null) progress(0.5f, "Raising walls and stones");
            Village(); Camp(); Thornwood(); Barrowfield(); AbbeyRise(); Mirefen(); AshenFields();
            if (progress != null) progress(0.65f, "Planting the Thornwood");
            Population();
            Vegetation();
            if (progress != null) progress(0.85f, "Waking the dead");
            BakeShadows();
            m.BuildMini();
            keep = null; treeGrid = null;
            return m;
        }

        // zones
        public static int ZoneOf(float tx, float ty)
        {
            if (tx >= 67 && tx <= 109 && ty >= 49 && ty <= 83) return Zones.Emberwick;
            if (ty < 49 && tx >= 62 && tx <= 114) return Zones.AbbeyRise;
            if (tx < 70 && ty < 99) return Zones.Thornwood;
            if (tx < 76 && ty >= 99) return Zones.Mirefen;
            if (tx > 108 && ty < 101) return Zones.Barrowfield;
            if (tx > 112 && ty >= 101) return Zones.AshenFields;
            return Zones.LowRoad;
        }

        // painting helpers (tile units)
        static void Set(int cx, int cy, byte t) { if (cx >= 0 && cy >= 0 && cx < m.CW && cy < m.CH) m.Cell[cy * m.CW + cx] = t; }

        static void Blob(float tx, float ty, float rx, float ry, byte t, float rough)
        {
            int x0 = (int)((tx - rx - 2) * 2), x1 = (int)((tx + rx + 2) * 2), y0 = (int)((ty - ry - 2) * 2), y1 = (int)((ty + ry + 2) * 2);
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    float u = (cx * 0.5f + 0.25f - tx) / rx, v = (cy * 0.5f + 0.25f - ty) / ry;
                    float d = u * u + v * v + (Noise.Fbm(cx * 0.11f, cy * 0.11f, Seed + 50, 2) - 0.5f) * rough;
                    if (d < 1f) Set(cx, cy, t);
                }
        }

        static void RectT(float x0, float y0, float x1, float y1, byte t)
        {
            for (int cy = (int)(y0 * 2); cy < (int)(y1 * 2); cy++)
                for (int cx = (int)(x0 * 2); cx < (int)(x1 * 2); cx++) Set(cx, cy, t);
        }

        static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
            float t = l2 <= 0 ? 0 : M.Clamp01(((px - ax) * dx + (py - ay) * dy) / l2);
            return M.Dist(px, py, ax + dx * t, ay + dy * t);
        }

        /// <summary>Paints a thick, slightly ragged polyline and (optionally) reserves a clear margin beside it.</summary>
        static void Road(byte t, float width, float clear, params float[] pts)
        {
            for (int s = 0; s + 3 < pts.Length; s += 2)
            {
                float ax = pts[s], ay = pts[s + 1], bx = pts[s + 2], by = pts[s + 3];
                float pad = width * 0.5f + clear + 1.5f;
                int x0 = (int)((Math.Min(ax, bx) - pad) * 2), x1 = (int)((Math.Max(ax, bx) + pad) * 2);
                int y0 = (int)((Math.Min(ay, by) - pad) * 2), y1 = (int)((Math.Max(ay, by) + pad) * 2);
                for (int cy = y0; cy <= y1; cy++)
                    for (int cx = x0; cx <= x1; cx++)
                    {
                        if (cx < 0 || cy < 0 || cx >= m.CW || cy >= m.CH) continue;
                        float d = SegDist(cx * 0.5f + 0.25f, cy * 0.5f + 0.25f, ax, ay, bx, by);
                        float w = width * 0.5f + (Noise.Value(cx * 0.19f, cy * 0.19f, Seed + 60) - 0.5f) * 0.55f;
                        if (d < w && t != T.Void) m.Cell[cy * m.CW + cx] = t;
                        if (clear > 0 && d < width * 0.5f + clear) keep[cy * m.CW + cx] = true;
                    }
            }
        }

        internal static void Keep(float tx, float ty, float rTiles)
        {
            int x0 = (int)((tx - rTiles) * 2), x1 = (int)((tx + rTiles) * 2), y0 = (int)((ty - rTiles) * 2), y1 = (int)((ty + rTiles) * 2);
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    if (cx < 0 || cy < 0 || cx >= m.CW || cy >= m.CH) continue;
                    if (M.Dist(cx * 0.5f, cy * 0.5f, tx, ty) <= rTiles) keep[cy * m.CW + cx] = true;
                }
        }

        static void KeepRect(float x0, float y0, float x1, float y1)
        {
            for (int cy = (int)(y0 * 2); cy <= (int)(y1 * 2); cy++)
                for (int cx = (int)(x0 * 2); cx <= (int)(x1 * 2); cx++)
                    if (cx >= 0 && cy >= 0 && cx < m.CW && cy < m.CH) keep[cy * m.CW + cx] = true;
        }

        // terrain
        static void PaintBase()
        {
            for (int cy = 0; cy < m.CH; cy++)
                for (int cx = 0; cx < m.CW; cx++)
                {
                    float tx = cx * 0.5f, ty = cy * 0.5f;
                    float w1 = (Noise.Fbm(tx * 0.07f, ty * 0.07f, Seed + 1, 3) - 0.5f) * 9f;
                    float w2 = (Noise.Fbm(tx * 0.07f, ty * 0.07f, Seed + 2, 3) - 0.5f) * 9f;
                    int z = ZoneOf(tx + w1, ty + w2);
                    byte t = T.Grass;
                    float n = Noise.Fbm(tx * 0.12f, ty * 0.12f, Seed + 3, 2);
                    switch (z)
                    {
                        case Zones.Thornwood: t = T.Forest; break;
                        case Zones.Mirefen: t = n > 0.57f ? T.Grass : T.Mud; break;
                        case Zones.Barrowfield: t = n > 0.66f ? T.Ash : T.PaleGrass; break;
                        case Zones.AbbeyRise: t = n > 0.6f ? T.PaleGrass : T.Ash; break;
                        case Zones.AshenFields: t = n > 0.62f ? T.PaleGrass : T.Ash; break;
                    }
                    float edge = Math.Min(Math.Min(tx, ty), Math.Min(TW - tx, TH - ty)) + (Noise.Fbm(tx * 0.15f, ty * 0.15f, Seed + 4, 2) - 0.5f) * 3.2f;
                    if (edge < 4.6f) t = T.Thicket;
                    m.Cell[cy * m.CW + cx] = t;
                }
        }

        static void PaintFeatures()
        {
            // ---- the hidden glade is walled in by impassable thicket; a single spur cuts through it
            Blob(53f, 23.5f, 8.2f, 6.8f, T.Thicket, 0.3f);
            Blob(53f, 23.5f, 4.6f, 3.6f, T.Grass, 0.25f);
            Keep(53f, 23.5f, 5f);

            // ---- water
            float[] river = { -2, 84, 14, 90, 26, 95, 40, 98, 56, 101, 72, 98, 88, 97, 104, 99, 120, 104, 140, 108, 160, 106, 178, 110 };
            Road(T.Mud, 5.6f, 0, river);
            Road(T.Water, 3.3f, 0.4f, river);
            Blob(20f, 44f, 4.2f, 3.1f, T.Water, 0.45f); Keep(20f, 44f, 5f);
            float[] pools = { 14, 108, 3, 2, 30, 119.5f, 4, 2.2f, 50, 109.5f, 3, 2, 60, 120, 3.5f, 2.2f, 25, 104, 2, 1.4f, 45, 120.5f, 2.5f, 1.5f, 66, 110, 2.6f, 1.8f };
            for (int i = 0; i < pools.Length; i += 4) { Blob(pools[i], pools[i + 1], pools[i + 2], pools[i + 3], T.Water, 0.5f); Keep(pools[i], pools[i + 1], pools[i + 2] + 0.5f); }

            // ---- special ground
            Blob(140f, 58f, 5.4f, 4.5f, T.Stone, 0.35f); Keep(140f, 58f, 6.5f);          // stone circle
            Blob(160f, 74f, 7f, 5.6f, T.Stone, 0.3f); Keep(160f, 73f, 8.5f);              // King's Barrow
            Blob(27.5f, 42.5f, 3.6f, 2.8f, T.Stone, 0.4f); Keep(27.5f, 42f, 5f);          // watchtower
            Blob(88f, 21f, 10f, 4.2f, T.Stone, 0.25f); KeepRect(72, 6, 104, 25);          // abbey forecourt
            Blob(37.5f, 114.5f, 2.6f, 2f, T.Stone, 0.3f); Keep(37.5f, 114.5f, 3.5f);      // rune islet
            Blob(133f, 113f, 6.5f, 4.2f, T.Dirt, 0.45f); Keep(133f, 113f, 7.5f);          // burnt farm
            Blob(17f, 20.5f, 3.8f, 2.9f, T.Dirt, 0.4f); Keep(17f, 20.5f, 4.6f);           // lamplighter's camp
            Blob(166.5f, 45f, 3.8f, 2.8f, T.Stone, 0.4f); Keep(166.5f, 44.5f, 5f);        // king's cache
            Blob(60f, 84.5f, 2.4f, 1.8f, T.Dirt, 0.4f); Keep(60f, 84.5f, 3.2f);           // hunter's cache
            Blob(88.5f, 111.5f, 6.5f, 4.6f, T.Grass, 0.3f); Blob(90f, 113.2f, 2.3f, 1.6f, T.Dirt, 0.35f); Keep(88.5f, 111.5f, 7.5f);   // Warden's Rest
            Blob(88f, 33.5f, 12f, 7.5f, T.Ash, 0.3f); KeepRect(76, 27, 100, 41);          // graveyard

            // ---- roads
            Road(T.Dirt, 2.3f, 1.3f, 88, 127, 88, 112, 87, 104, 88.2f, 97, 88, 90, 88, 80);
            Road(T.Dirt, 2.2f, 1.3f, 70, 66, 62, 64.5f, 54, 66, 46, 62.5f, 38, 60, 30, 54, 28, 46.5f, 28, 44.5f);
            Road(T.Dirt, 1.9f, 1.2f, 46, 62.5f, 48, 50, 44, 40, 40, 30, 30, 24, 20, 21);
            Road(T.Dirt, 2.1f, 1.0f, 44, 38, 47, 31.5f, 52, 25.5f);
            Road(T.Dirt, 1.9f, 1.2f, 38, 60, 34, 70, 30, 80, 25, 90, 23.6f, 95, 22, 100, 20, 106, 28, 112, 36, 114.5f);
            Road(T.Dirt, 1.5f, 1.0f, 20, 106, 13.5f, 114.5f);
            Road(T.Dirt, 2.2f, 1.3f, 106, 66, 116, 64, 126, 66, 136, 62.5f, 148, 66, 155, 71.5f);
            Road(T.Dirt, 1.9f, 1.2f, 126, 66, 130, 78, 134, 88, 130, 98, 128, 105.5f, 130, 111);
            Road(T.Dirt, 1.8f, 1.2f, 136, 62.5f, 150, 50, 161, 46.5f);
            Road(T.Dirt, 2.2f, 1.3f, 88, 52, 88, 44, 86.5f, 36, 88, 27);
            Road(T.Stone, 3.2f, 1.2f, 88, 27, 88, 19);
            Road(T.Dirt, 1.4f, 1.0f, 58, 65.5f, 60, 76, 60, 83.5f);

            // ---- Emberwick
            KeepRect(66, 48, 110, 84);
            Blob(88f, 66f, 9.4f, 6.4f, T.Cobble, 0.25f);
            Road(T.Cobble, 3.0f, 0, 88, 81, 88, 51);
            Road(T.Cobble, 3.0f, 0, 69, 66, 107, 66);
            Blob(99f, 66f, 3.4f, 1.9f, T.Dirt, 0.4f);
            Blob(80f, 75.5f, 3.2f, 2f, T.Dirt, 0.4f);

            // ---- crossings
            RectT(86.4f, 94.2f, 89.8f, 100.4f, T.Plank);
            RectT(126.4f, 102.4f, 129.8f, 109f, T.Plank);
            Blob(23.6f, 94.8f, 2.0f, 2.9f, T.Stone, 0.3f);
        }

        // prop helpers
        internal static Prop Mk(Sprite s, float tx, float ty, PropKind kind, int colW, int colH, bool shots)
        {
            Prop p = new Prop();
            p.Spr = s; p.X = (float)Math.Round(tx * 16f); p.Y = (float)Math.Round(ty * 16f); p.Kind = kind;
            p.ColW = colW; p.ColH = colH; p.BlocksShots = shots;
            m.Add(p);
            return p;
        }

        internal static Prop MkAnim(Sprite[] a, float tx, float ty, PropKind kind, int colW, int colH, bool shots)
        {
            Prop p = Mk(a[0], tx, ty, kind, colW, colH, shots);
            p.Anim = a.Length > 1 ? a : null;
            p.AnimOff = r.Int(4);
            return p;
        }

        internal static Prop Glow(Prop p, float radius, int color, float intensity, float oy, bool flicker)
        {
            p.LightR = radius; p.LightColor = color; p.LightI = intensity; p.LightOY = oy; p.Flicker = flicker;
            return p;
        }

        internal static Prop Act(Prop p, string id, string prompt, float radius)
        {
            p.Id = id; p.Interactive = true; p.Prompt = prompt; p.InteractR = radius;
            return p;
        }

        public const int EmberLight = 0xFFB060, GloamLight = 0xA890FF, MoonLight = 0xB8D8FF, WindowLight = 0xFFC070;

        internal static Prop Lantern(float tx, float ty) { return Glow(MkAnim(Assets.LanternLit, tx, ty, PropKind.Deco, 4, 4, false), 78, EmberLight, 0.95f, -24, true); }
        internal static Prop Sign(Sprite s, float tx, float ty, string title, string text)
        {
            Prop p = Mk(s, tx, ty, PropKind.Sign, 6, 4, false);
            p.Text = title + "\n" + text; p.Interactive = true; p.Prompt = "Read"; p.InteractR = 20; p.Id = "sign_" + m.Props.Count;
            return p;
        }

        internal static Prop Chest(float tx, float ty, string id, bool ornate, int tier)
        {
            Prop p = Mk(ornate ? Assets.ChestOrnate : Assets.ChestWood, tx, ty, PropKind.Chest, 16, 8, false);
            p.Param = tier + (ornate ? 100 : 0);
            Keep(tx, ty, 1.5f);
            return Act(p, id, "Open", 20);
        }

        internal static Prop Breakable(Sprite s, float tx, float ty, int colW)
        {
            Prop p = Mk(s, tx, ty, PropKind.Breakable, colW, 6, false);
            p.HP = 1; p.Id = "brk_" + m.Id + "_" + m.Props.Count;
            return p;
        }

        static Prop Tree(Sprite s, float tx, float ty)
        {
            Prop p = Mk(s, tx, ty, PropKind.Tree, 8, 6, true);
            p.Tall = true; p.ShadowR = Math.Min(16, s.W / 3f);
            p.Flip = r.Chance(0.5f);
            int gx = M.Clamp((int)tx, 0, TW - 1), gy = M.Clamp((int)ty, 0, TH - 1);
            if (treeGrid[gy * TW + gx] == null) treeGrid[gy * TW + gx] = new List<int>();
            treeGrid[gy * TW + gx].Add(treeX.Count);
            treeX.Add(tx); treeY.Add(ty);
            return p;
        }

        static bool TreeNear(float tx, float ty, float minDist)
        {
            int gx = (int)tx, gy = (int)ty;
            for (int j = -2; j <= 2; j++)
                for (int i = -2; i <= 2; i++)
                {
                    int ax = gx + i, ay = gy + j;
                    if (ax < 0 || ay < 0 || ax >= TW || ay >= TH) continue;
                    List<int> l = treeGrid[ay * TW + ax];
                    if (l == null) continue;
                    for (int k = 0; k < l.Count; k++)
                        if (M.Dist(tx, ty, treeX[l[k]], treeY[l[k]]) < minDist) return true;
                }
            return false;
        }

        internal static void Spawn(EnemyKind k, float tx, float ty, int level) { Spawn(k, tx, ty, level, null, false); }
        internal static void Spawn(EnemyKind k, float tx, float ty, int level, string id, bool once)
        {
            SpawnDef s = new SpawnDef();
            s.Kind = k; s.X = tx * 16; s.Y = ty * 16; s.Level = level; s.Once = once; s.Variant = r.Int(3);
            s.Id = id ?? (m.Id + "_s" + m.Spawns.Count);
            m.Spawns.Add(s);
            Keep(tx, ty, 1.6f);
        }

        internal static void Pack(EnemyKind k, float tx, float ty, int count, int level, float spread)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * M.TAU / count + r.F();
                Spawn(k, tx + M.Cos(a) * spread * (0.6f + r.F() * 0.5f), ty + M.Sin(a) * spread * 0.7f, level);
            }
        }

        internal static void Npc(string id, string name, ActorSprites spr, float tx, float ty, bool wander, bool faceLeft)
        {
            NpcDef n = new NpcDef();
            n.Id = id; n.Name = name; n.Spr = spr; n.X = tx * 16; n.Y = ty * 16; n.Wander = wander; n.FaceLeft = faceLeft;
            m.NpcDefs.Add(n);
            m.Mark(id, n.X, n.Y, 20);
            Keep(tx, ty, 1.5f);
        }

        // Emberwick
        static void Village()
        {
            // footprints run back as deep as the 3D houses (there is open ground behind every one of them)
            Mk(Assets.HouseElder, 88f, 58.2f, PropKind.Deco, 90, 60, true).Tall = true;
            Mk(Assets.HouseInn, 76.4f, 61.2f, PropKind.Deco, 82, 56, true).Tall = true;
            Glow(Mk(Assets.HouseSmith, 100.6f, 62.6f, PropKind.Deco, 66, 52, true), 70, EmberLight, 0.9f, -10, true).Tall = true;
            Mk(Assets.HouseHerb, 76.6f, 73.6f, PropKind.Deco, 44, 46, true).Tall = true;
            Mk(Assets.HouseA, 98.8f, 76.6f, PropKind.Deco, 58, 52, true).Tall = true;
            Mk(Assets.HouseB, 103.4f, 55.6f, PropKind.Deco, 50, 46, true).Tall = true;
            Mk(Assets.HouseC, 72.4f, 54.4f, PropKind.Deco, 54, 48, true).Tall = true;
            Mk(Assets.HouseB, 73.6f, 80.8f, PropKind.Deco, 50, 44, true).Tall = true;
            Mk(Assets.HouseA, 96.4f, 54.2f, PropKind.Deco, 58, 46, true).Tall = true;

            // window glow for the lit houses
            float[] glows = { 88, 58, 76.4f, 61, 76.6f, 73.4f, 98.8f, 76.4f, 103.4f, 55.4f, 73.6f, 80.6f, 96.4f, 54 };
            for (int i = 0; i < glows.Length; i += 2)
            {
                Prop g = new Prop(); g.Hidden = true; g.X = glows[i] * 16; g.Y = glows[i + 1] * 16 + 6;
                Glow(g, 64, WindowLight, 0.55f, -14, false); m.Add(g);
            }

            Mk(Assets.Well, 84.2f, 67.8f, PropKind.Well, 26, 12, false);
            Prop sh = MkAnim(Assets.ShrineOut, 92.6f, 70.4f, PropKind.Shrine, 20, 8, false);
            Act(sh, "shrine_emberwick", "Rest", 24).Text = "Emberwick";
            Sign(Assets.NoticeBoard, 91.8f, 62.2f, "Emberwick Notice Board",
                "^yBY ORDER OF ELDER MAREN^0\nNo one walks the roads after the lamps are lit. That is, no one walks the roads.\n\n" +
                "^yWANTED^0  Wolf pelts, any condition. See Bram at the forge.\n\n^yMISSING^0  Tobin the lamplighter. Went west to tend the Thornwood lamps. Ask for Wren.\n\n" +
                "^dSomeone has scratched underneath: the bell isn't broken. it was silenced.^0");
            Mk(Assets.Stall, 82.6f, 62.4f, PropKind.Deco, 36, 8, false);
            Mk(Assets.Stall2, 80.6f, 77.4f, PropKind.Deco, 36, 8, false);
            Mk(Assets.Anvil, 97.0f, 65.6f, PropKind.Deco, 12, 6, false);
            Mk(Assets.Woodpile, 104.6f, 63.4f, PropKind.Deco, 18, 6, false);
            Mk(Assets.Cart, 94.2f, 71.6f, PropKind.Deco, 30, 8, false);
            Mk(Assets.Bench, 85.6f, 62.2f, PropKind.Deco, 18, 4, false);
            Mk(Assets.Bench, 90.4f, 72.8f, PropKind.Deco, 18, 4, false);
            Mk(Assets.Banner, 83.2f, 58.9f, PropKind.Deco, 4, 4, false);
            Mk(Assets.Banner, 92.8f, 58.9f, PropKind.Deco, 4, 4, false);
            float[] clutter = { 102.4f, 64.6f, 0, 103.2f, 65.2f, 1, 79.6f, 62.6f, 0, 73.6f, 62.4f, 2, 93.6f, 77.4f, 1, 94.4f, 78f, 0, 78.4f, 74.6f, 2, 83.6f, 82f, 3, 100.2f, 56.4f, 3, 71.2f, 71.4f, 0, 106f, 76f, 3, 105.4f, 76.6f, 1 };
            for (int i = 0; i < clutter.Length; i += 3)
            {
                int k = (int)clutter[i + 2];
                if (k == 0) Breakable(Assets.Barrel, clutter[i], clutter[i + 1], 10);
                else if (k == 1) Breakable(Assets.Crate, clutter[i], clutter[i + 1], 12);
                else if (k == 2) Mk(Assets.Sack, clutter[i], clutter[i + 1], PropKind.Deco, 8, 4, false);
                else Mk(Assets.HayBale, clutter[i], clutter[i + 1], PropKind.Deco, 14, 6, false);
            }
            // lamps: gates and plaza
            float[] lamps = { 85.4f, 81.4f, 90.6f, 81.4f, 85.4f, 51.2f, 90.6f, 51.2f, 69.2f, 63.6f, 69.2f, 68.6f, 106.8f, 63.6f, 106.8f, 68.6f, 81f, 71.4f, 95.2f, 62f, 95.4f, 70.6f, 80.6f, 65f };
            for (int i = 0; i < lamps.Length; i += 2) Lantern(lamps[i], lamps[i + 1]);

            // palisade with four gates
            for (float x = 67; x <= 109; x += 1f)
            {
                if (Math.Abs(x - 88) > 2.6f) { Stake(x, 49.4f); Stake(x, 83.4f); }
            }
            for (float y = 49.9f; y <= 83.4f; y += 0.5f)
            {
                if (Math.Abs(y - 66.2f) > 2.7f) { StakeV(67f, y); StakeV(109f, y); }
            }
            // a few garden trees
            float[] gt = { 70.5f, 78.5f, 105.5f, 80f, 70.4f, 59.6f, 106.2f, 60.4f, 92.6f, 80.6f };
            for (int i = 0; i < gt.Length; i += 2) Tree(r.Pick(Assets.OakGreen), gt[i], gt[i + 1]);
            // herb garden
            for (int i = 0; i < 9; i++) Mk(r.Pick(Assets.Flower), 78.4f + (i % 3) * 0.9f, 75f + (i / 3) * 0.7f, PropKind.Deco, 0, 0, false);

            Npc("elder", "Elder Maren", Assets.Elder, 88f, 60.6f, false, false);
            Npc("smith", "Bram the Smith", Assets.Smith, 98.4f, 66.6f, false, true);
            Npc("herbalist", "Ysolde", Assets.Herbalist, 83.2f, 78.2f, false, true);
            Npc("innkeeper", "Odo", Assets.Innkeeper, 79.2f, 63.4f, false, false);
            Npc("wren", "Wren", Assets.Child, 72.6f, 66.9f, false, true);
            Npc("villager1", "Villager", Assets.VillagerA, 90.5f, 68.6f, true, false);
            Npc("villager2", "Villager", Assets.VillagerB, 85f, 64.4f, true, false);
            m.Mark("emberwick", 88 * 16, 66 * 16, 60);
        }

        static void Stake(float tx, float ty) { Mk(r.Pick(Assets.Palisade), tx, ty, PropKind.Deco, 16, 6, true); }
        static void StakeV(float tx, float ty) { Mk(r.Pick(Assets.Palisade), tx, ty, PropKind.Deco, 10, 8, true); }

        // Warden's Rest
        static void Camp()
        {
            Prop sh = MkAnim(Assets.ShrineOut, 84.6f, 110.6f, PropKind.Shrine, 20, 8, false);
            Act(sh, "shrine_rest", "Rest", 24).Text = "Warden's Rest";
            Glow(MkAnim(Assets.CampfireLit, 90f, 113.4f, PropKind.Campfire, 14, 6, false), 100, EmberLight, 1.15f, -8, true);
            Mk(Assets.Tent, 94.2f, 109.8f, PropKind.Deco, 36, 16, true).Tall = true;
            Mk(Assets.Bedroll, 87.4f, 114.2f, PropKind.Deco, 0, 0, false).Flat = true;
            Mk(Assets.Banner, 92.2f, 108.2f, PropKind.Deco, 4, 4, false);
            Breakable(Assets.Crate, 95.6f, 111.2f, 12); Breakable(Assets.Barrel, 96.2f, 112f, 10);
            Mk(Assets.Sack, 95.2f, 112.4f, PropKind.Deco, 8, 4, false);
            Mk(Assets.Log, 88.4f, 115.2f, PropKind.Deco, 20, 5, false);
            Sign(Assets.SignR, 86.2f, 107.4f, "Waymarker", "^wNORTH^0 - Emberwick, a short walk.\n\nThe other arms of the post have been snapped off. Someone has carved a bell into the wood, and then scratched it out.");
            // the road south is choked
            Mk(Assets.RockL[0], 87f, 121.4f, PropKind.Deco, 22, 10, true); Mk(Assets.RockL[1], 89.2f, 122f, PropKind.Deco, 22, 10, true);
            Mk(Assets.Log, 88f, 120.2f, PropKind.Deco, 22, 5, false);
            Npc("warden", "Brother Halvard", Assets.Warden, 91.8f, 111.6f, false, true);
            m.Mark("start", 88.4f * 16, 112.6f * 16, 20);
        }

        // The Thornwood
        static void Thornwood()
        {
            Mk(Assets.Watchtower, 27.4f, 42.6f, PropKind.Deco, 40, 24, true).Tall = true;
            Prop sh = MkAnim(Assets.ShrineOut, 31.2f, 45.6f, PropKind.Shrine, 20, 8, false);
            Act(sh, "shrine_watch", "Rest", 24).Text = "The Old Watchtower";
            Rune(24f, 42.2f, 0);
            Chest(30.8f, 42.4f, "chest_watch", false, 1);
            Sign(Assets.Grave[0], 25.2f, 45.4f, "A soldier's marker", "HERE THE WATCH ENDED.\n\nWe lit the beacon three nights running. No answer came from the abbey. On the fourth night the wolves' eyes were the wrong colour.");

            // pond dressing
            for (int i = 0; i < 6; i++) { Prop l = Mk(r.Pick(Assets.Lily), 20f + r.Sym(2.6f), 44f + r.Sym(1.8f), PropKind.Deco, 0, 0, false); l.Flat = true; }
            for (int i = 0; i < 9; i++) { float a = i * 0.7f; Mk(r.Pick(Assets.Reeds), 20f + M.Cos(a) * 4.5f, 44f + M.Sin(a) * 3.4f, PropKind.Deco, 0, 0, false).Sway = true; }

            // the hidden glade
            Chest(53.4f, 22.6f, "chest_glade", true, 2);
            for (int i = 0; i < 9; i++) { float a = i * M.TAU / 9; Glow(Mk(r.Pick(Assets.MushroomGlow), 53.4f + M.Cos(a) * 2.6f, 23.2f + M.Sin(a) * 1.8f, PropKind.Deco, 0, 0, false), 22, MoonLight, 0.45f, -3, false); }
            Mk(Assets.StoneDeco[1], 55.8f, 21.4f, PropKind.Deco, 10, 6, true);
            Sign(Assets.StoneDeco[4], 50.6f, 21.6f, "A mossy stone", "The carving is older than the abbey:\n\n^cWHAT THE BELL KEEPS OUT, THE STONES KEEP IN. WAKE US THREE AND THE KING'S DOOR OPENS.^0");
            Bramble(49.06f, 27.95f, "bramble_glade1"); Bramble(50.14f, 28.85f, "bramble_glade2");       // side by side where the spur pierces the thicket

            // the lamplighter's last camp
            MkAnim(Assets.CampfireOut, 17f, 21.2f, PropKind.Deco, 14, 6, false);
            Mk(Assets.Bedroll, 15.4f, 20.4f, PropKind.Deco, 0, 0, false).Flat = true;
            Sprite lanSpr = Assets.Icon[ItemArt.IcLantern, 1].Clone(); lanSpr.OX = 8; lanSpr.OY = 15;
            Prop lan = Mk(lanSpr, 18.6f, 20.4f, PropKind.Keepsake, 0, 0, false);
            Act(lan, "lantern", "Take the lantern", 20); lan.Text = "lantern";
            Glow(lan, 60, EmberLight, 0.9f, -6, true);
            Sign(Assets.Sack, 16.2f, 22.6f, "Tobin's satchel", "A folded scrap inside, in a careful hand:\n\n^wWren - the west lamps are lit as far as the old tower. I can hear them in the trees now, the ones with too many legs. If I am late, do not come looking. Keep the house lamp burning. - T.^0");
            Keep(17f, 20.5f, 4f);

            // hunter's cache by the river
            Chest(60.2f, 84.6f, "chest_hunter", false, 1);
            Breakable(Assets.Crate, 58.8f, 85f, 12); Mk(Assets.HayBale, 61.6f, 85.2f, PropKind.Deco, 14, 6, false);
            Sign(Assets.SignL, 46.4f, 64.6f, "Waymarker", "^wWEST^0 - the old watchtower.\n^wNORTH^0 - the deep wood. ^rNo lamps beyond this point.^0\n^wSOUTH^0 - the river ford and Mirefen.");
            Sign(Assets.SignR, 59f, 67.2f, "A hunter's blaze", "Three notches, then an arrow pointing south toward the river.");

            // moonpetals
            float[] mp = { 52.4f, 58.6f, 42.6f, 46.4f, 35.6f, 71.4f, 25.4f, 51f, 45.6f, 34.4f, 15.6f, 40.4f, 29.6f, 85f, 56.4f, 76.4f, 35.4f, 27f };
            for (int i = 0; i < mp.Length; i += 2)
            {
                Prop p = Mk(Assets.Moonpetal, mp[i], mp[i + 1], PropKind.Moonpetal, 0, 0, false);
                Act(p, "moon" + (i / 2), "Gather", 18);
                Glow(p, 34, MoonLight, 0.6f, -8, false);
                Keep(mp[i], mp[i + 1], 1.4f);
            }
            // forest lamps lit by Tobin along the west road
            Lantern(62.2f, 63f); Lantern(53.6f, 67.4f); Lantern(40f, 59f);
        }

        static void Rune(float tx, float ty, int index)
        {
            Prop p = Mk(Assets.StoneUnlit[index], tx, ty, PropKind.RuneStone, 10, 6, true);
            p.Param = index;
            Act(p, "rune" + index, "Touch the stone", 22);
            Keep(tx, ty, 2f);
            m.Mark("rune" + index, p.X, p.Y, 20);
        }

        static void Bramble(float tx, float ty, string id)
        {
            // together the two close the whole spur: even a slim body must not slip past at the edge
            Prop p = Mk(r.Pick(Assets.Bramble), tx, ty, PropKind.Bramble, 44, 24, true);
            p.Id = id; p.HP = 3;
        }

        // The Barrowfield
        static void Barrowfield()
        {
            for (int i = 0; i < 7; i++)
            {
                float a = i * M.TAU / 7 + 0.2f;
                Mk(Assets.StoneDeco[i % Assets.StoneDeco.Length], 140f + M.Cos(a) * 4.6f, 58.4f + M.Sin(a) * 3.6f, PropKind.Deco, 10, 6, true);
            }
            Sign(Assets.StoneDeco[2], 140f, 57.2f, "The King-Stone", "Worn lettering circles the base:\n\n^cSEVEN KINGS LAY DOWN THEIR CROWNS WHEN THE FIRST BELL RANG. THEY HAVE BEEN WAITING FOR IT TO STOP.^0");
            Prop sh = MkAnim(Assets.ShrineOut, 135.4f, 64.2f, PropKind.Shrine, 20, 8, false);
            Act(sh, "shrine_barrow", "Rest", 24).Text = "The Stone Circle";

            float[] mounds = { 121.6f, 56.4f, 131.4f, 75f, 147.2f, 47.6f, 152.4f, 87f, 118.6f, 91f, 168f, 86f };
            for (int i = 0; i < mounds.Length; i += 2) { Mk(Assets.BarrowDeco, mounds[i], mounds[i + 1], PropKind.Deco, 70, 26, true).Tall = true; Keep(mounds[i], mounds[i + 1] - 1, 4f); }

            // King's Barrow: a ring of broken columns before the great mound
            Mk(Assets.BarrowDeco, 160.4f, 67.2f, PropKind.Deco, 70, 26, true).Tall = true;
            for (int i = 0; i < 10; i++)
            {
                float a = i * M.TAU / 10 + 0.31f;
                float px = 160f + M.Cos(a) * 7.2f, py = 74f + M.Sin(a) * 5.4f;
                if (Math.Abs(a - M.PI) < 0.5f) continue;          // entrance from the west road
                if (i % 3 == 0) Mk(Assets.PillarTall[i % 2], px, py, PropKind.Deco, 10, 6, true).Tall = true;
                else Mk(Assets.PillarBroken[i % 3], px, py, PropKind.Deco, 10, 6, true);
            }
            Mk(Assets.RuinWall[0], 156.4f, 68.6f, PropKind.Deco, 44, 8, true);
            Mk(Assets.RuinWall[1], 164.6f, 68.8f, PropKind.Deco, 30, 8, true);
            Glow(MkAnim(Assets.BrazierGloam, 155.2f, 70.6f, PropKind.Deco, 8, 5, false), 70, GloamLight, 0.9f, -16, true);
            Glow(MkAnim(Assets.BrazierGloam, 165.2f, 70.8f, PropKind.Deco, 8, 5, false), 70, GloamLight, 0.9f, -16, true);
            m.Mark("knight", 160 * 16, 74 * 16, 60);

            Rune(136.6f, 90.4f, 1);
            Mk(Assets.RuinWall[3], 138.4f, 89.4f, PropKind.Deco, 22, 8, true);

            // the sealed hoard
            Prop cache = Mk(Assets.BarrowSealed, 166.6f, 44f, PropKind.Cache, 78, 26, true);
            cache.Tall = true; Act(cache, "cache", "Examine the door", 30);
            m.Mark("cache", cache.X, cache.Y, 30);
            Mk(Assets.StoneDeco[3], 162.8f, 45.4f, PropKind.Deco, 10, 6, true); Mk(Assets.StoneDeco[5], 170.2f, 45.6f, PropKind.Deco, 10, 6, true);

            Chest(118.6f, 84.6f, "chest_barrow", false, 1);
            Mk(Assets.RuinWall[1], 118.2f, 83.4f, PropKind.Deco, 30, 8, true);
            Mk(Assets.Arch, 116f, 64.6f, PropKind.Deco, 0, 0, false).Tall = true;
            Sign(Assets.SignR, 126.4f, 64.4f, "Waymarker", "^wEAST^0 - the King's Barrow. ^rDo not disturb.^0\n^wSOUTH^0 - Garrow's farm.\n^wNORTH-EAST^0 - the old hoard-mound. Sealed since before the abbey.");
            for (int i = 0; i < 16; i++)
            {
                float gx = 112 + r.F() * 56, gy = 46 + r.F() * 50;
                if (keep[(int)(gy * 2) * m.CW + (int)(gx * 2)]) continue;
                Mk(r.Pick(Assets.Grave), gx, gy, PropKind.Deco, 8, 4, false);
            }
        }

        // Abbey Rise
        static void AbbeyRise()
        {
            Mk(Assets.Abbey, 88f, 17.6f, PropKind.Deco, 216, 56, true);
            Prop door = new Prop();
            door.Kind = PropKind.AbbeyDoor; door.Hidden = true; door.X = 88 * 16; door.Y = 17.9f * 16;
            m.Add(door); Act(door, "abbey_door", "Enter the Sunken Abbey", 26);
            Glow(door, 60, GloamLight, 0.7f, -16, true);
            m.Mark("abbey_door", door.X, door.Y + 10, 26);
            Glow(MkAnim(Assets.BrazierGloam, 83.6f, 19.2f, PropKind.Deco, 8, 5, false), 80, GloamLight, 1.0f, -16, true);
            Glow(MkAnim(Assets.BrazierGloam, 92.4f, 19.2f, PropKind.Deco, 8, 5, false), 80, GloamLight, 1.0f, -16, true);
            Prop sh = MkAnim(Assets.ShrineOut, 93.6f, 24.2f, PropKind.Shrine, 20, 8, false);
            Act(sh, "shrine_abbey", "Rest", 24).Text = "The Abbey Steps";
            Mk(Assets.StatueA, 82.2f, 25f, PropKind.Deco, 16, 8, true).Tall = true;
            Mk(Assets.StatueB, 94.6f, 26.2f, PropKind.Deco, 16, 8, true).Tall = true;
            Sign(Assets.Grave[3], 80.4f, 24.2f, "Founder's plaque", "^wSAINT ORRIN^0 hung the Vesper Bell in this place and set seven wardens to ring it at each day's end.\n\n^d'So long as it sounds, the Gloam shall not cross the vale.'^0\n\nBelow, a newer and cruder inscription: ^rHE WHO LOVES SILENCE NEED NOT FEAR IT.^0");
            Mk(Assets.Arch, 88f, 45.6f, PropKind.Deco, 0, 0, false).Tall = true;

            // graveyard rows either side of the road
            for (float gy = 29.5f; gy <= 40f; gy += 2.3f)
                for (float gx = 77.5f; gx <= 99f; gx += 2.2f)
                {
                    if (Math.Abs(gx - (86.5f + (gy - 36) * 0.15f)) < 2.8f) continue;
                    if (r.Chance(0.2f)) continue;
                    Mk(r.Pick(Assets.Grave), gx + r.Sym(0.4f), gy + r.Sym(0.4f), PropKind.Deco, 8, 4, false);
                }
            Sign(Assets.Grave[1], 83.2f, 33.6f, "A fresh grave", "The earth is newly turned. A warden's tabard is folded on top, and a single white flower.\n\n^wBROTHER ANSEL. HE RANG IT ALONE FOR NINE NIGHTS.^0");
            for (float x = 76.5f; x <= 99.5f; x += 1.1f) { if (Math.Abs(x - 86.8f) > 2.2f) Mk(Assets.FenceH, x, 41.6f, PropKind.Deco, 16, 3, false); }
            for (int i = 0; i < 3; i++) Tree(Assets.OakViolet[i], 78f + i * 9.4f + (i == 1 ? 3 : 0), 27.4f + (i % 2) * 14.4f);
            Glow(MkAnim(Assets.Candles, 85.2f, 30.4f, PropKind.Deco, 4, 3, false), 44, EmberLight, 0.7f, -18, true);
        }

        // Mirefen
        static void Mirefen()
        {
            Rune(37.4f, 114.4f, 2);
            Chest(12.6f, 116.2f, "chest_mire", false, 1);
            Mk(Assets.StatueB, 48.4f, 116.4f, PropKind.Deco, 16, 8, true).Tall = true;
            Sign(Assets.SignL, 22.4f, 101.6f, "A leaning post", "^wMIREFEN^0\n\nThe paint has run. Underneath, older letters: ^dmind the lights. they are not lamps.^0");
            float[] pools = { 14, 108, 30, 119.5f, 50, 109.5f, 60, 120, 45, 120.5f, 66, 110 };
            for (int i = 0; i < pools.Length; i += 2)
                for (int k = 0; k < 4; k++) { Prop l = Mk(r.Pick(Assets.Lily), pools[i] + r.Sym(1.8f), pools[i + 1] + r.Sym(1.1f), PropKind.Deco, 0, 0, false); l.Flat = true; }
        }

        // The Ashen Fields
        static void AshenFields()
        {
            Mk(Assets.RuinWall[2], 133f, 110.6f, PropKind.Deco, 60, 8, true);
            Mk(Assets.RuinWall[1], 129.4f, 113.6f, PropKind.Deco, 30, 8, true);
            Mk(Assets.RuinWall[3], 137.6f, 114.2f, PropKind.Deco, 22, 8, true);
            Chest(135.4f, 112.6f, "chest_ash", false, 1);
            Breakable(Assets.Barrel, 131.2f, 112.2f, 10); Breakable(Assets.Crate, 132.2f, 112.8f, 12); Breakable(Assets.Barrel, 138.6f, 116f, 10); Breakable(Assets.Crate, 128.2f, 115.4f, 12);
            Mk(Assets.Cart, 136.6f, 117.2f, PropKind.Deco, 30, 8, false);
            Mk(Assets.HayBale, 130f, 116.6f, PropKind.Deco, 14, 6, false);
            Mk(Assets.Well, 140.6f, 111.4f, PropKind.Well, 26, 12, false);
            Sign(Assets.SignR, 129.2f, 109f, "Garrow's Farm", "The board is charred at the edges.\n\nNailed beneath it, a child's drawing: a house, four figures, and above them a great black circle with no clapper.");
            for (int i = 0; i < 6; i++) Tree(r.Pick(Assets.DeadBurnt), 124f + i * 3.4f + r.Sym(0.8f), 118.4f + r.Sym(1.6f));
        }

        // scatter
        static void Vegetation()
        {
            // dense, impassable belts wherever the ground is thicket
            for (float ty = 0.6f; ty < TH; ty += 1.15f)
                for (float tx = 0.6f; tx < TW; tx += 1.15f)
                {
                    float px = tx + r.Sym(0.42f), py = ty + r.Sym(0.42f);
                    int cx = (int)(px * 2), cy = (int)(py * 2);
                    if (cx < 0 || cy < 0 || cx >= m.CW || cy >= m.CH) continue;
                    if (m.Cell[cy * m.CW + cx] != T.Thicket) continue;
                    float k = r.F();
                    Tree(k < 0.5f ? r.Pick(Assets.PineDark) : (k < 0.9f ? r.Pick(Assets.OakTeal) : r.Pick(Assets.Dead)), px, py);
                }

            int attempts = 21000;
            for (int i = 0; i < attempts; i++)
            {
                float tx = 4 + r.F() * (TW - 8), ty = 4 + r.F() * (TH - 8);
                int cx = (int)(tx * 2), cy = (int)(ty * 2);
                byte t = m.Cell[cy * m.CW + cx];
                bool clear = keep[cy * m.CW + cx];
                bool village = tx > 67 && tx < 109 && ty > 49.5f && ty < 83.5f;
                float k = r.F();
                switch (t)
                {
                    case T.Forest:
                        if (k < 0.30f)
                        {
                            if (clear || TreeNear(tx, ty, 1.45f)) break;
                            float s = r.F();
                            Tree(s < 0.46f ? r.Pick(Assets.OakTeal) : (s < 0.8f ? r.Pick(Assets.Pine) : (s < 0.89f ? r.Pick(Assets.PineDark) : (s < 0.95f ? r.Pick(Assets.OakRust) : r.Pick(Assets.Dead)))), tx, ty);
                        }
                        else if (k < 0.35f) { if (!clear) Mk(r.Pick(Assets.Bush), tx, ty, PropKind.Deco, 0, 0, false); }
                        else if (k < 0.38f) { if (!clear && !TreeNear(tx, ty, 0.9f)) { bool big = r.Chance(0.4f); Mk(big ? r.Pick(Assets.RockMossL) : r.Pick(Assets.RockMossM), tx, ty, PropKind.Deco, big ? 18 : 10, big ? 8 : 5, big); } }
                        else if (k < 0.54f) Mk(r.Pick(Assets.TuftDark), tx, ty, PropKind.Deco, 0, 0, false).Sway = true;
                        else if (k < 0.57f) Mk(r.Chance(0.4f) ? r.Pick(Assets.MushroomGlow) : r.Pick(Assets.Mushroom), tx, ty, PropKind.Deco, 0, 0, false);
                        else if (k < 0.58f) { if (!clear) Mk(r.Chance(0.5f) ? Assets.Stump : Assets.Log, tx, ty, PropKind.Deco, 12, 5, false); }
                        break;
                    case T.Grass:
                        if (k < 0.035f) { if (!clear && !village && !TreeNear(tx, ty, 2.2f)) Tree(r.Pick(Assets.OakGreen), tx, ty); }
                        else if (k < 0.07f) { if (!clear && !village) Mk(r.Chance(0.4f) ? r.Pick(Assets.BushBerry) : r.Pick(Assets.Bush), tx, ty, PropKind.Deco, 0, 0, false); }
                        else if (k < 0.085f) { if (!clear && !village) Mk(r.Pick(Assets.RockM), tx, ty, PropKind.Deco, 10, 5, false); }
                        else if (k < 0.235f) Mk(r.Chance(0.3f) ? r.Pick(Assets.TuftTall) : r.Pick(Assets.Tuft), tx, ty, PropKind.Deco, 0, 0, false).Sway = true;
                        else if (k < 0.295f) Mk(r.Pick(Assets.Flower), tx, ty, PropKind.Deco, 0, 0, false);
                        else if (k < 0.315f) Mk(r.Pick(Assets.RockS), tx, ty, PropKind.Deco, 0, 0, false);
                        break;
                    case T.PaleGrass:
                        if (k < 0.02f) { if (!clear && !TreeNear(tx, ty, 3f)) Tree(r.Chance(0.6f) ? r.Pick(Assets.Dead) : r.Pick(Assets.DeadPale), tx, ty); }
                        else if (k < 0.045f) { if (!clear) Mk(r.Chance(0.3f) ? r.Pick(Assets.RockL) : r.Pick(Assets.RockM), tx, ty, PropKind.Deco, 12, 6, false); }
                        else if (k < 0.245f) Mk(r.Chance(0.5f) ? r.Pick(Assets.TuftPaleTall) : r.Pick(Assets.TuftPale), tx, ty, PropKind.Deco, 0, 0, false).Sway = true;
                        else if (k < 0.275f) Mk(Assets.Flower[r.Chance(0.5f) ? 0 : 1], tx, ty, PropKind.Deco, 0, 0, false);
                        else if (k < 0.295f) Mk(r.Pick(Assets.RockS), tx, ty, PropKind.Deco, 0, 0, false);
                        break;
                    case T.Ash:
                        if (k < 0.022f) { if (!clear && !TreeNear(tx, ty, 2.6f)) Tree(ty > 100 ? r.Pick(Assets.DeadBurnt) : r.Pick(Assets.DeadPale), tx, ty); }
                        else if (k < 0.05f) { if (!clear) Mk(r.Pick(Assets.RockM), tx, ty, PropKind.Deco, 10, 5, false); }
                        else if (k < 0.15f) Mk(r.Pick(Assets.TuftPale), tx, ty, PropKind.Deco, 0, 0, false).Sway = true;
                        else if (k < 0.17f) Mk(r.Pick(Assets.RockS), tx, ty, PropKind.Deco, 0, 0, false);
                        break;
                    case T.Mud:
                        if (k < 0.03f) { if (!clear && !TreeNear(tx, ty, 2.4f)) Tree(r.Pick(Assets.Dead), tx, ty); }
                        else if (k < 0.17f) Mk(r.Pick(Assets.Reeds), tx, ty, PropKind.Deco, 0, 0, false).Sway = true;
                        else if (k < 0.2f) { if (!clear) Mk(r.Pick(Assets.RockMossM), tx, ty, PropKind.Deco, 10, 5, false); }
                        else if (k < 0.23f) Glow(Mk(r.Pick(Assets.MushroomGlow), tx, ty, PropKind.Deco, 0, 0, false), 20, MoonLight, 0.4f, -3, false);
                        break;
                    case T.Water:
                        if (k < 0.035f) Mk(r.Pick(Assets.Lily), tx, ty, PropKind.Deco, 0, 0, false).Flat = true;
                        break;
                }
            }
        }

        // enemies
        static void Population()
        {
            // Thornwood
            Pack(EnemyKind.Wolf, 58.5f, 61f, 2, 1, 1.6f);
            Pack(EnemyKind.Wolf, 45f, 54f, 3, 1, 2f);
            Pack(EnemyKind.Wolf, 33.5f, 65.5f, 3, 2, 2f);
            Pack(EnemyKind.Wolf, 50f, 39f, 3, 2, 2f);
            Pack(EnemyKind.Wolf, 31f, 33f, 3, 3, 2.2f);
            Spawn(EnemyKind.AlphaWolf, 27f, 30f, 3, "alpha", true);
            Pack(EnemyKind.Wolf, 30f, 82f, 2, 2, 1.8f);
            Pack(EnemyKind.Crawler, 39f, 27f, 2, 2, 1.8f);
            Pack(EnemyKind.Crawler, 20.5f, 23.5f, 3, 3, 2.2f);
            Pack(EnemyKind.Crawler, 53f, 25.5f, 2, 3, 1.6f);
            Pack(EnemyKind.Crawler, 12f, 60f, 2, 2, 1.8f);
            Pack(EnemyKind.Crawler, 14f, 47f, 2, 2, 1.6f);
            // Mirefen
            Pack(EnemyKind.Crawler, 22f, 110f, 2, 3, 2f);
            Pack(EnemyKind.Crawler, 36f, 110.5f, 3, 3, 2.2f);
            Pack(EnemyKind.Wisp, 46f, 114f, 2, 4, 2.4f);
            Spawn(EnemyKind.Wisp, 16f, 113f, 4);
            Pack(EnemyKind.Crawler, 58f, 113f, 2, 3, 2f);
            // Barrowfield
            Pack(EnemyKind.Hollow, 118f, 62f, 2, 3, 2f);
            Pack(EnemyKind.Hollow, 131f, 70f, 3, 3, 2.4f);
            Pack(EnemyKind.Hollow, 144f, 54.5f, 2, 4, 2f);
            Spawn(EnemyKind.Archer, 137f, 55.5f, 4);
            Pack(EnemyKind.Hollow, 149f, 79f, 3, 4, 2.4f);
            Pack(EnemyKind.Archer, 151f, 61f, 2, 4, 2.4f);
            Pack(EnemyKind.Hollow, 139f, 87f, 2, 4, 2f);
            Spawn(EnemyKind.Archer, 144f, 85f, 4);
            Pack(EnemyKind.Hollow, 158f, 50f, 2, 4, 2f);
            Spawn(EnemyKind.Wisp, 163f, 48f, 4);
            Spawn(EnemyKind.Knight, 160f, 73.4f, 5, "knight", true);
            // Abbey Rise
            Pack(EnemyKind.Hollow, 82f, 35f, 2, 4, 2f);
            Pack(EnemyKind.Hollow, 93f, 33f, 2, 5, 2f);
            Pack(EnemyKind.Wisp, 88f, 30f, 2, 5, 3f);
            Spawn(EnemyKind.Archer, 80f, 22.6f, 5); Spawn(EnemyKind.Archer, 96f, 22.6f, 5);
            Spawn(EnemyKind.Wisp, 75f, 38f, 5);
            // Ashen Fields
            Pack(EnemyKind.Hollow, 134f, 116f, 3, 4, 2.6f);
            Spawn(EnemyKind.Archer, 142f, 114f, 4);
            Pack(EnemyKind.Wolf, 108f, 108f, 2, 3, 2f);
            // Low Road stragglers
            Pack(EnemyKind.Wolf, 98f, 92f, 2, 1, 1.8f);
            Spawn(EnemyKind.Wolf, 78f, 104f, 1);
        }

        // finishing
        static void BakeShadows()
        {
            int W = m.PW, H = m.PH;
            foreach (Prop p in m.Props)
            {
                float sr = p.ShadowR;
                if (sr <= 0 && p.ColW >= 40) sr = -1;
                if (sr == 0) continue;
                if (sr > 0)
                {
                    float rx = sr, ry = sr * 0.42f;
                    int x0 = (int)(p.X - rx), x1 = (int)(p.X + rx), y0 = (int)(p.Y - ry - 1), y1 = (int)(p.Y + ry);
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            if (x < 0 || y < 0 || x >= W || y >= H) continue;
                            float u = (x + 0.5f - p.X) / rx, v = (y + 0.5f - p.Y + 1) / ry;
                            float d = u * u + v * v;
                            if (d > 1f) continue;
                            if (d > 0.72f && Cv.Bayer(x, y) > (1f - d) * 3.2f) continue;
                            m.Ground[y * W + x] = Mul(m.Ground[y * W + x], 168);
                        }
                }
                else
                {
                    // buildings: a soft band of shade along the foot of the walls
                    int x0 = (int)(p.X - p.ColW * 0.5f) - 2, x1 = (int)(p.X + p.ColW * 0.5f) + 2;
                    for (int y = (int)p.Y; y < (int)p.Y + 7; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            if (x < 0 || y < 0 || x >= W || y >= H) continue;
                            int k = y - (int)p.Y;
                            if (k > 3 && Cv.Bayer(x, y) > (7 - k) * 0.25f) continue;
                            m.Ground[y * W + x] = Mul(m.Ground[y * W + x], 150 + k * 10);
                        }
                }
            }
        }

        static int Mul(int c, int f)
        {
            int rr = ((c >> 16) & 255) * f >> 8, g = ((c >> 8) & 255) * f >> 8, b = (c & 255) * f >> 8;
            return (c & Col.Opaque) | (rr << 16) | (g << 8) | b;
        }
    }
}
