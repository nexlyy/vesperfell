using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>
    /// Low-poly models for every piece of scenery, built procedurally the first time a sprite is met. Colours are
    /// sampled from the sprite itself, so the 3D world keeps the palette of the pixel art. Models face +Z (the camera)
    /// with their origin at the prop's base point; anything without a dedicated model becomes an upright sprite card.
    /// </summary>
    internal static class PropModels
    {
        static readonly Dictionary<long, MeshB> cache = new Dictionary<long, MeshB>();
        static Dictionary<Sprite, int> kinds;

        // model families
        const int KOak = 1, KPine = 2, KDead = 3, KBush = 4, KBramble = 5, KRock = 6, KRockMoss = 7, KTuft = 8, KFlower = 9, KMushroom = 10,
            KMushGlow = 11, KReeds = 12, KLily = 13, KMoonpetal = 14, KMoonPicked = 15, KStump = 16, KLog = 17, KCampLit = 18, KCampOut = 19,
            KLanternLit = 20, KLanternOut = 21, KBrazierEmber = 22, KBrazierGloam = 23, KBrazierOut = 24, KTorchEmber = 25, KTorchGloam = 26,
            KCandles = 27, KShrineLit = 28, KShrineOut = 29, KAltar = 30, KChest = 31, KChestOpen = 32, KChestOrn = 33, KChestOrnOpen = 34,
            KBarrel = 35, KCrate = 36, KUrn = 37, KUrnStone = 38, KSack = 39, KCart = 40, KHay = 41, KWoodpile = 42, KAnvil = 43, KSign = 44,
            KNotice = 45, KFenceH = 46, KFenceV = 47, KBench = 48, KBanner = 49, KWell = 50, KStall = 51, KStall2 = 52, KBedroll = 53, KTent = 54,
            KPew = 55, KBones = 56, KGateClosed = 58, KGateOpen = 59, KSecretWall = 60, KStairs = 61, KBellSilent = 62, KBellRung = 63,
            KSkulls = 64, KWallBanner = 65, KCoffin = 66, KSarcClosed = 67, KSarcOpen = 68, KBookshelf = 69, KAbbey = 70, KWatchtower = 71,
            KBarrowSealed = 72, KBarrowRune = 73, KBarrowOpen = 74, KBarrowDeco = 75, KStatue = 76, KArch = 77, KPalisade = 78, KRuinWall = 79,
            KPillar = 80, KPillarBroken = 81, KGrave = 82, KRubble = 83, KStone = 84, KStoneLit = 85, KStoneDeco = 86, KHouse = 87, KSmith = 88;

        static void Map(Sprite s, int k) { if (s != null && !kinds.ContainsKey(s)) kinds[s] = k; }
        static void Map(Sprite[] a, int k) { if (a != null) foreach (Sprite s in a) Map(s, k); }

        static void BuildKinds()
        {
            kinds = new Dictionary<Sprite, int>();
            Map(Assets.OakTeal, KOak); Map(Assets.OakGreen, KOak); Map(Assets.OakRust, KOak); Map(Assets.OakViolet, KOak);
            Map(Assets.Pine, KPine); Map(Assets.PineDark, KPine);
            Map(Assets.Dead, KDead); Map(Assets.DeadPale, KDead); Map(Assets.DeadBurnt, KDead);
            Map(Assets.Bush, KBush); Map(Assets.BushBerry, KBush); Map(Assets.Bramble, KBramble);
            Map(Assets.RockS, KRock); Map(Assets.RockM, KRock); Map(Assets.RockL, KRock); Map(Assets.RockMossM, KRockMoss); Map(Assets.RockMossL, KRockMoss);
            Map(Assets.Tuft, KTuft); Map(Assets.TuftTall, KTuft); Map(Assets.TuftPale, KTuft); Map(Assets.TuftPaleTall, KTuft); Map(Assets.TuftDark, KTuft);
            Map(Assets.Flower, KFlower); Map(Assets.Mushroom, KMushroom); Map(Assets.MushroomGlow, KMushGlow); Map(Assets.Reeds, KReeds); Map(Assets.Lily, KLily);
            Map(Assets.Moonpetal, KMoonpetal); Map(Assets.MoonpetalPicked, KMoonPicked); Map(Assets.Stump, KStump); Map(Assets.Log, KLog);
            Map(Assets.CampfireLit, KCampLit); Map(Assets.CampfireOut, KCampOut); Map(Assets.LanternLit, KLanternLit); Map(Assets.LanternOut, KLanternOut);
            Map(Assets.BrazierEmber, KBrazierEmber); Map(Assets.BrazierGloam, KBrazierGloam); Map(Assets.BrazierOut, KBrazierOut);
            Map(Assets.TorchEmber, KTorchEmber); Map(Assets.TorchGloam, KTorchGloam); Map(Assets.Candles, KCandles);
            Map(Assets.ShrineLit, KShrineLit); Map(Assets.ShrineOut, KShrineOut); Map(Assets.Altar, KAltar);
            Map(Assets.ChestWood, KChest); Map(Assets.ChestWoodOpen, KChestOpen); Map(Assets.ChestOrnate, KChestOrn); Map(Assets.ChestOrnateOpen, KChestOrnOpen);
            Map(Assets.Barrel, KBarrel); Map(Assets.Crate, KCrate); Map(Assets.Urn, KUrn); Map(Assets.UrnStone, KUrnStone); Map(Assets.Sack, KSack);
            Map(Assets.Cart, KCart); Map(Assets.HayBale, KHay); Map(Assets.Woodpile, KWoodpile); Map(Assets.Anvil, KAnvil);
            Map(Assets.SignR, KSign); Map(Assets.SignL, KSign); Map(Assets.NoticeBoard, KNotice); Map(Assets.FenceH, KFenceH); Map(Assets.FenceV, KFenceV);
            Map(Assets.Bench, KBench); Map(Assets.Banner, KBanner); Map(Assets.Well, KWell); Map(Assets.Stall, KStall); Map(Assets.Stall2, KStall2);
            Map(Assets.Bedroll, KBedroll); Map(Assets.Tent, KTent); Map(Assets.Pew, KPew); Map(Assets.Bones, KBones);
            Map(Assets.GateClosed, KGateClosed); Map(Assets.GateOpen, KGateOpen); Map(Assets.SecretWall, KSecretWall); Map(Assets.Stairs, KStairs);
            Map(Assets.BellSilent, KBellSilent); Map(Assets.BellRung, KBellRung); Map(Assets.SkullPile, KSkulls); Map(Assets.WallBanner, KWallBanner);
            Map(Assets.Coffin, KCoffin); Map(Assets.SarcClosed, KSarcClosed); Map(Assets.SarcOpen, KSarcOpen); Map(Assets.Bookshelf, KBookshelf); Map(Assets.Bookshelf2, KBookshelf);
            Map(Assets.Abbey, KAbbey); Map(Assets.Watchtower, KWatchtower);
            Map(Assets.BarrowSealed, KBarrowSealed); Map(Assets.BarrowRune, KBarrowRune); Map(Assets.BarrowOpen, KBarrowOpen); Map(Assets.BarrowDeco, KBarrowDeco);
            Map(Assets.StatueA, KStatue); Map(Assets.StatueB, KStatue); Map(Assets.Arch, KArch);
            Map(Assets.Palisade, KPalisade); Map(Assets.RuinWall, KRuinWall); Map(Assets.PillarTall, KPillar); Map(Assets.PillarBroken, KPillarBroken);
            Map(Assets.Grave, KGrave); Map(Assets.Rubble, KRubble); Map(Assets.StoneUnlit, KStone); Map(Assets.StoneLit, KStoneLit); Map(Assets.StoneDeco, KStoneDeco);
            Map(Assets.HouseElder, KHouse); Map(Assets.HouseInn, KHouse); Map(Assets.HouseHerb, KHouse); Map(Assets.HouseA, KHouse); Map(Assets.HouseB, KHouse);
            Map(Assets.HouseC, KHouse); Map(Assets.HouseSmith, KSmith);
        }

        public static int KindOf(Sprite s)
        {
            if (kinds == null) BuildKinds();
            int k;
            return s != null && kinds.TryGetValue(s, out k) ? k : 0;
        }

        // placing
        public static void Place(MeshB b, Prop p, MapGeo geo)
        {
            Sprite s = p.Spr;
            int kind = KindOf(s);
            if (p.Flat || kind == 0 && s.H <= 6)
            {
                FlatCard(b, s, p.X, p.Y, geo.HeightAt(p.X, p.Y) + 0.35f, p.Flip);
                return;
            }
            V3 at = Anchor(p, geo);
            MeshB m = Get(s, p.ColW, p.ColH, p.OnWall, geo.Ceil);
            Xf xf = Xf.Move(at);
            if (p.Flip) xf = xf * Xf.Scale(-1, 1, 1);
            b.Append(m, xf);
        }

        /// <summary>Where a prop's model stands: on the ground, on the lowest corner of a big footprint, or against a wall.</summary>
        public static V3 Anchor(Prop p, MapGeo geo)
        {
            int kind = KindOf(p.Spr);
            float x = p.X, z = p.Y;
            float y = geo.HeightAt(x, z);
            if (kind == KLily) y = MapGeo.WaterY + 0.25f;
            else if (p.ColW > 12 && p.ColH > 8)
            {
                // big footprints sit on the lowest corner and their foundations reach down
                float a = geo.HeightAt(x - p.ColW * 0.5f, z), c = geo.HeightAt(x + p.ColW * 0.5f, z);
                float d = geo.HeightAt(x - p.ColW * 0.5f, z - p.ColH), e = geo.HeightAt(x + p.ColW * 0.5f, z - p.ColH);
                y = Math.Min(Math.Min(a, c), Math.Min(d, e));
            }
            if (p.OnWall && geo.M.Dungeon)
            {
                // find the face of the wall behind it
                int cx = (int)Math.Floor(x / Vesperfell.Map.CS), cz = (int)Math.Floor(z / Vesperfell.Map.CS);
                for (int k = 0; k < 8; k++)
                {
                    if (geo.WallH(cx, cz - k) >= 30) { z = (cz - k + 1) * Vesperfell.Map.CS + 0.2f; break; }
                }
                y = 0;
            }
            return new V3(x, y, z);
        }

        /// <summary>
        /// Points where a burning prop's flames sit (model space offsets added to its anchor). Returns how many were
        /// written, with the flame size and whether it burns with gloam-light.
        /// </summary>
        public static int Flames(Prop p, MapGeo geo, V3[] pts, out float size, out bool gloam)
        {
            int kind = KindOf(p.Spr);
            size = 3; gloam = p.LightColor == WorldGen.GloamLight;
            int n = 0;
            switch (kind)
            {
                case KCampLit: pts[n++] = new V3(0, 2.0f, 0); size = 5.5f; break;
                case KBrazierEmber: pts[n++] = new V3(0, 14.6f, 0); size = 4.2f; break;
                case KBrazierGloam: pts[n++] = new V3(0, 14.6f, 0); size = 4.2f; gloam = true; break;
                case KTorchEmber: case KTorchGloam:
                    gloam = kind == KTorchGloam;
                    if (p.OnWall) pts[n++] = new V3(0, TorchWallY + 9.6f * M.Cos(0.35f) + 1.2f, 2.2f + 9.6f * M.Sin(0.35f));
                    else pts[n++] = new V3(0, 17.4f, 0);
                    size = 2.6f; break;
                case KCandles: pts[n++] = new V3(-4.5f, 20.2f, 0); pts[n++] = new V3(0, 21.2f, 0); pts[n++] = new V3(4.5f, 20.2f, 0); size = 1.3f; break;
                case KAltar: pts[n++] = new V3(-11, 16.2f, -2); pts[n++] = new V3(11, 16.2f, -2); size = 1.3f; break;
                case KLanternLit: pts[n++] = new V3(5, LampY, 0); size = 1.8f; break;
                default: return 0;
            }
            V3 at = Anchor(p, geo);
            for (int i = 0; i < n; i++) pts[i] = new V3(at.X + (p.Flip ? -pts[i].X : pts[i].X), at.Y + pts[i].Y, at.Z + pts[i].Z);
            return n;
        }

        /// <summary>How high above its base a lamp or wall torch burns (where its light comes from), or -1 for anything else.</summary>
        public static float LightY(Prop p)
        {
            switch (KindOf(p.Spr))
            {
                case KLanternLit: case KLanternOut: return LampY + 1;
                case KTorchEmber: case KTorchGloam: return p.OnWall ? TorchWallY + 11 : 19;
            }
            return -1;
        }

        public static void PlaceSprite(MeshB b, Sprite s, float x, float z, float y, bool flip, float scale)
        {
            MeshB m = Get(s, 0, 0, false);
            Xf xf = Xf.Move(x, y, z) * Xf.Scale(flip ? -scale : scale, scale, scale);
            b.Append(m, xf);
        }

        public static MeshB Get(Sprite s, int colW, int colH, bool onWall) { return Get(s, colW, colH, onWall, 0); }

        /// <summary>The model for a sprite; ceil is the height of the dungeon ceiling (0 = open sky), which gates, pillars and walls reach up to.</summary>
        public static MeshB Get(Sprite s, int colW, int colH, bool onWall, float ceil)
        {
            int kind = KindOf(s);
            bool sized = kind == KHouse || kind == KSmith || kind == KPalisade || kind == KRuinWall || kind == KGateClosed || kind == KGateOpen || kind == KAbbey;
            bool roofed = kind == KGateClosed || kind == KGateOpen || kind == KSecretWall || kind == KStairs || kind == KPillar;
            if (!roofed) ceil = 0;
            long key = ((long)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s) << 24) ^ (sized ? (colW * 1000 + colH) : 0) ^ (onWall ? 1L << 23 : 0) ^ ((long)ceil << 56);
            MeshB m;
            if (cache.TryGetValue(key, out m)) return m;
            m = new MeshB();
            Build(m, s, kind, colW, colH, onWall, ceil);
            cache[key] = m;
            return m;
        }

        // colour sampling
        /// <summary>Average colour of the sprite's opaque, non-outline pixels inside a box (fractions of its size).</summary>
        static int Avg(Sprite s, float x0, float y0, float x1, float y1, int fallback)
        {
            long r = 0, g = 0, b = 0; int n = 0;
            int ax = (int)(x0 * s.W), bx = Math.Max(ax + 1, (int)(x1 * s.W)), ay = (int)(y0 * s.H), by = Math.Max(ay + 1, (int)(y1 * s.H));
            for (int y = Math.Max(0, ay); y < Math.Min(s.H, by); y++)
                for (int x = Math.Max(0, ax); x < Math.Min(s.W, bx); x++)
                {
                    int c = s.Px[y * s.W + x];
                    if (((c >> 24) & 255) < 200) continue;
                    int cr = (c >> 16) & 255, cg = (c >> 8) & 255, cb = c & 255;
                    if (cr + cg + cb < 70) continue;
                    r += cr; g += cg; b += cb; n++;
                }
            if (n == 0) return fallback;
            return (int)(r / n) << 16 | (int)(g / n) << 8 | (int)(b / n);
        }

        static int Mul(int c, float k)
        {
            int r = M.Clamp((int)(((c >> 16) & 255) * k), 0, 255), g = M.Clamp((int)(((c >> 8) & 255) * k), 0, 255), b = M.Clamp((int)((c & 255) * k), 0, 255);
            return r << 16 | g << 8 | b;
        }

        static int Mix(int a, int b, float t) { return Col.Lerp(a | Col.Opaque, b | Col.Opaque, t) & 0xFFFFFF; }

        // the models are lit by the scene, so surface colours are pushed a little brighter than the pre-shaded pixel art
        static int Lift(int c) { return Mul(c, 1.18f); }

        // building
        static void Build(MeshB m, Sprite s, int kind, int colW, int colH, bool onWall, float ceil)
        {
            uint seed = (uint)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s);
            m.Tex = 1f / 32f;
            switch (kind)
            {
                case KOak: Oak(m, s, seed); break;
                case KPine: Pine(m, s, seed); break;
                case KDead: DeadTree(m, s, seed); break;
                case KBush: Bush(m, s, seed); break;
                case KBramble: Bramble(m, s, seed); break;
                case KRock: Rock(m, s, seed, false); break;
                case KRockMoss: Rock(m, s, seed, true); break;
                case KTuft: Tuft(m, s, seed, 0); break;
                case KFlower: Tuft(m, s, seed, 1); break;
                case KReeds: Tuft(m, s, seed, 2); break;
                case KMushroom: Mushrooms(m, s, seed, false); break;
                case KMushGlow: Mushrooms(m, s, seed, true); break;
                case KLily: Lily(m, s, seed); break;
                case KMoonpetal: Moonpetal(m, true); break;
                case KMoonPicked: Moonpetal(m, false); break;
                case KStump: Stump(m, s); break;
                case KLog: LogModel(m, s); break;
                case KCampLit: Campfire(m, true); break;
                case KCampOut: Campfire(m, false); break;
                case KLanternLit: Lantern(m, true); break;
                case KLanternOut: Lantern(m, false); break;
                case KBrazierEmber: Brazier(m, 0xFFA040); break;
                case KBrazierGloam: Brazier(m, 0xA890FF); break;
                case KBrazierOut: Brazier(m, -1); break;
                case KTorchEmber: Torch(m, 0xFFA040, onWall); break;
                case KTorchGloam: Torch(m, 0xA890FF, onWall); break;
                case KCandles: Candles(m, seed); break;
                case KShrineLit: Shrine(m, true); break;
                case KShrineOut: Shrine(m, false); break;
                case KAltar: Altar(m); break;
                case KChest: Chest(m, false, false); break;
                case KChestOpen: Chest(m, false, true); break;
                case KChestOrn: Chest(m, true, false); break;
                case KChestOrnOpen: Chest(m, true, true); break;
                case KBarrel: Barrel(m); break;
                case KCrate: Crate(m, 10); break;
                case KUrn: Urn(m, Lift(Avg(s, 0.2f, 0.3f, 0.8f, 0.8f, 0xB06A3A)), false); break;
                case KUrnStone: Urn(m, Lift(Avg(s, 0.2f, 0.3f, 0.8f, 0.8f, 0x5A6A80)), true); break;
                case KSack: Sack(m); break;
                case KCart: Cart(m); break;
                case KHay: Hay(m); break;
                case KWoodpile: Woodpile(m); break;
                case KAnvil: Anvil(m); break;
                case KSign: Sign(m); break;
                case KNotice: Notice(m); break;
                case KFenceH: Fence(m, false); break;
                case KFenceV: Fence(m, true); break;
                case KBench: Bench(m, false); break;
                case KPew: Bench(m, true); break;
                case KBanner: Banner(m, s); break;
                case KWell: Well(m); break;
                case KStall: Stall(m, 0xD03A3A); break;
                case KStall2: Stall(m, 0x3A8A5A); break;
                case KBedroll: Bedroll(m, s); break;
                case KTent: Tent(m, s); break;
                case KBones: Bones(m, seed); break;
                case KGateClosed: Gate(m, colW, false, ceil); break;
                case KGateOpen: Gate(m, colW, true, ceil); break;
                case KSecretWall: SecretWall(m, s, ceil); break;
                case KStairs: Stairs(m, s, ceil); break;
                case KBellSilent: Bell(m, s, false); break;
                case KBellRung: Bell(m, s, true); break;
                case KSkulls: Skulls(m, seed); break;
                case KWallBanner: WallBanner(m, s); break;
                case KCoffin: Coffin(m, s); break;
                case KSarcClosed: Sarcophagus(m, false); break;
                case KSarcOpen: Sarcophagus(m, true); break;
                case KBookshelf: Bookshelf(m, s); break;
                case KAbbey: Abbey(m, colW, colH); break;
                case KWatchtower: Watchtower(m); break;
                case KBarrowSealed: Barrow(m, 0); break;
                case KBarrowRune: Barrow(m, 1); break;
                case KBarrowOpen: Barrow(m, 2); break;
                case KBarrowDeco: Barrow(m, 3); break;
                case KStatue: Statue(m, s); break;
                case KArch: ArchModel(m, s); break;
                case KPalisade: Palisade(m, colW, colH, seed); break;
                case KRuinWall: RuinWall(m, s, colW, seed); break;
                case KPillar: Pillar(m, s, false, seed, ceil); break;
                case KPillarBroken: Pillar(m, s, true, seed, 0); break;
                case KGrave: Grave(m, s, seed); break;
                case KRubble: Rubble(m, seed); break;
                case KStone: StandingStone(m, s, 1, seed); break;
                case KStoneLit: StandingStone(m, s, 2, seed); break;
                case KStoneDeco: StandingStone(m, s, 0, seed); break;
                case KHouse: House(m, s, colW, colH, false); break;
                case KSmith: House(m, s, colW, colH, true); break;
                default: Card(m, s); break;
            }
            m.T = Xf.Id; m.Layer = 0; m.Wind = 0; m.Em = 0; m.Occ = 0;
        }

        // sprite cards (anything without a model)
        /// <summary>An upright card with the sprite on it, leaned back toward the camera so it reads at full height.</summary>
        static void Card(MeshB m, Sprite s)
        {
            Mat3.AtlasRect r;
            if (!Mat3.Rects.TryGetValue(s, out r)) return;
            float w = s.W, h = s.H, lean = 0.55f;
            float l = -s.OX, rr = s.W - s.OX;
            float c = M.Cos(lean), sn = M.Sin(lean);
            V3 a = new V3(l, 0, 0), b = new V3(rr, 0, 0), cc = new V3(rr, h * c, -h * sn), d = new V3(l, h * c, -h * sn);
            m.Layer = Mat3.AtlasLayer; m.Col = 0xFFFFFF;
            m.QuadUv(a, b, cc, d, r.U0, r.V0, r.U1, r.V1);
            m.Layer = 0;
        }

        public static void FlatCard(MeshB b, Sprite s, float x, float z, float y, bool flip)
        {
            Mat3.AtlasRect r;
            if (!Mat3.Rects.TryGetValue(s, out r)) return;
            float l = x - s.OX, rr = x - s.OX + s.W, t = z - s.OY, bt = z - s.OY + s.H;
            float u0 = flip ? r.U1 : r.U0, u1 = flip ? r.U0 : r.U1;
            b.T = Xf.Id; b.Layer = Mat3.AtlasLayer; b.Col = 0xFFFFFF;
            b.QuadUv(new V3(l, y, bt), new V3(rr, y, bt), new V3(rr, y, t), new V3(l, y, t), u0, r.V0, u1, r.V1);
            b.Layer = 0;
        }

        // vegetation
        static float Hr(uint seed, int k) { return (Noise.Hash((int)seed, k, 777) & 1023) / 1023f; }

        static void Oak(MeshB m, Sprite s, uint seed)
        {
            int leaf = Lift(Avg(s, 0.1f, 0.05f, 0.9f, 0.55f, 0x2F6A5A));
            int bark = Lift(Avg(s, 0.4f, 0.8f, 0.6f, 1f, 0x5A4030));
            float H = s.H * 0.95f, R = s.W * 0.42f;
            m.Occ = 1;
            m.Layer = Mat3.Bark; m.Col = bark;
            m.Cyl(0, 0, -2, H * 0.5f, 2.6f, 1.7f, 6, false, false, Hr(seed, 1));
            // two boughs into the crown
            m.T = Xf.Move(0, H * 0.36f, 0) * Xf.Rot(new V3(0.3f, 0, 1).Norm, -0.7f);
            m.Cyl(0, 0, 0, H * 0.28f, 1.3f, 0.7f, 5, false, false, 0);
            m.T = Xf.Move(0, H * 0.4f, 0) * Xf.Rot(new V3(-0.4f, 0, 1).Norm, 0.75f);
            m.Cyl(0, 0, 0, H * 0.25f, 1.2f, 0.6f, 5, false, false, 0);
            m.T = Xf.Id;
            m.Layer = Mat3.Leaf;
            float cy = H * 0.66f;
            for (int i = 0; i < 6; i++)
            {
                float a = i * 2.1f + Hr(seed, 10 + i) * 1.5f;
                float d = i == 0 ? 0 : R * (0.42f + Hr(seed, 20 + i) * 0.2f);
                float rr = R * (i == 0 ? 0.78f : 0.52f + Hr(seed, 30 + i) * 0.15f);
                float y = cy + (i == 0 ? 2 : (Hr(seed, 40 + i) - 0.45f) * R * 0.6f);
                m.Col = Mix(Mul(leaf, 0.82f), Mul(leaf, 1.12f), M.Clamp01((y - cy + R * 0.3f) / (R * 0.8f)));
                m.Wind = (byte)(70 + i * 8);
                m.Blob(M.Cos(a) * d, y, M.Sin(a) * d * 0.85f, rr, rr * 0.82f, rr, 1, seed + (uint)i * 31, 0.18f);
            }
            m.Wind = 0;
        }

        static void Pine(MeshB m, Sprite s, uint seed)
        {
            int leaf = Lift(Avg(s, 0.2f, 0.1f, 0.8f, 0.8f, 0x24504A));
            int bark = Lift(Avg(s, 0.4f, 0.88f, 0.6f, 1f, 0x4A3628));
            float H = s.H * 1.02f, R = s.W * 0.5f;
            m.Occ = 1;
            m.Layer = Mat3.Bark; m.Col = bark;
            m.Cyl(0, 0, -2, H * 0.3f, 1.9f, 1.4f, 6, false, false, 0);
            m.Layer = Mat3.Needle;
            int tiers = 4;
            for (int i = 0; i < tiers; i++)
            {
                float t = i / (float)(tiers - 1);
                float y0 = H * (0.18f + t * 0.2f), y1 = H * (0.5f + t * 0.5f);
                float r = R * (1f - t * 0.55f);
                m.Col = Mix(Mul(leaf, 0.78f), Mul(leaf, 1.12f), t);
                m.Wind = (byte)(30 + i * 22);
                m.Cyl(0, 0, y0, y1, r, 0, 7, false, true, Hr(seed, i) * 3f);
            }
            m.Wind = 0;
        }

        static void DeadTree(MeshB m, Sprite s, uint seed)
        {
            int bark = Lift(Avg(s, 0.3f, 0.2f, 0.7f, 1f, 0x6A6070));
            float H = s.H * 0.95f;
            m.Occ = 1;
            m.Layer = Mat3.Bark; m.Col = bark;
            m.Cyl(0, 0, -2, H * 0.62f, 2.4f, 1.1f, 6, false, false, 0);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.9f + Hr(seed, i) * 1.2f;
                float y = H * (0.3f + Hr(seed, 10 + i) * 0.35f);
                float len = H * (0.25f + Hr(seed, 20 + i) * 0.2f);
                m.T = Xf.Move(0, y, 0) * Xf.Rot(new V3(-M.Sin(a), 0, M.Cos(a)), 0.7f + Hr(seed, 30 + i) * 0.5f);
                m.Cyl(0, 0, 0, len, 0.9f, 0.25f, 4, false, false, 0);
                m.T = m.T * Xf.Move(0, len * 0.6f, 0) * Xf.Rot(new V3(M.Cos(a), 0, M.Sin(a)), 0.6f);
                m.Cyl(0, 0, 0, len * 0.5f, 0.5f, 0.15f, 4, false, false, 0);
            }
            m.T = Xf.Id;
        }

        static void Bush(MeshB m, Sprite s, uint seed)
        {
            int leaf = Lift(Avg(s, 0.1f, 0.1f, 0.9f, 0.9f, 0x3A6A3A));
            float R = s.W * 0.33f;
            m.Layer = Mat3.Leaf; m.Wind = 40;
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.7f + Hr(seed, i);
                float d = i == 0 ? 0 : R * 0.55f;
                m.Col = Mul(leaf, 0.9f + i * 0.06f);
                m.Blob(M.Cos(a) * d, R * 0.65f + (i == 0 ? 1 : 0), M.Sin(a) * d * 0.7f, R * (i == 0 ? 0.95f : 0.7f), R * 0.7f, R * 0.8f, 1, seed + (uint)i, 0.2f);
            }
            // berries: any bright red-ish pixels in the sprite get a few beads
            int berry = Avg(s, 0.2f, 0.2f, 0.8f, 0.6f, 0);
            bool berries = false;
            for (int i = 0; i < s.Px.Length; i++) { int c = s.Px[i]; if (((c >> 24) & 255) > 200 && ((c >> 16) & 255) > 150 && ((c >> 8) & 255) < 90) { berries = true; berry = c & 0xFFFFFF; break; } }
            if (berries)
            {
                m.Layer = 0; m.Col = berry; m.Em = 40;
                for (int i = 0; i < 7; i++)
                {
                    float a = i * 0.9f + Hr(seed, 50 + i) * 2;
                    m.Blob(M.Cos(a) * R * 0.8f, R * (0.5f + Hr(seed, 60 + i) * 0.6f), M.Sin(a) * R * 0.7f + 1, 0.9f, 0.9f, 0.9f, 0, seed + (uint)i, 0);
                }
                m.Em = 0;
            }
            m.Wind = 0;
        }

        static void Bramble(MeshB m, Sprite s, uint seed)
        {
            int stem = Lift(Avg(s, 0.1f, 0.1f, 0.9f, 0.9f, 0x5A2A3A));
            m.Layer = Mat3.Bark;
            float W = Math.Max(20, s.W * 0.9f), H = s.H * 0.8f;
            for (int i = 0; i < 14; i++)
            {
                float x = (Hr(seed, i) - 0.5f) * W * 0.85f, z = (Hr(seed, 20 + i) - 0.5f) * 10;
                float tilt = (Hr(seed, 40 + i) - 0.5f) * 1.6f;
                m.Col = Mul(stem, 0.75f + Hr(seed, 60 + i) * 0.5f);
                Xf b = Xf.Move(x, 0, z) * Xf.Rot(new V3(0.2f, 0, 1).Norm, tilt);
                m.T = b;
                float len = H * (0.6f + Hr(seed, 80 + i) * 0.5f);
                m.Cyl(0, 0, 0, len, 1.0f, 0.35f, 4, false, false, 0);
                m.Col = 0xD8C8B0;
                for (int k = 1; k < 4; k++)
                {
                    m.T = b * Xf.Move(0, len * k / 4f, 0) * Xf.RotZ(k % 2 == 0 ? 1.2f : -1.2f);
                    m.Cyl(0, 0, 0, 2.2f, 0.35f, 0, 3, false, false, 0);
                }
            }
            m.T = Xf.Id;
            m.Layer = Mat3.Leaf; m.Col = Mul(stem, 0.6f);
            m.Blob(0, 2.5f, 0, W * 0.45f, 3.5f, 6, 0, seed, 0.3f);
        }

        static void Rock(MeshB m, Sprite s, uint seed, bool moss)
        {
            int c = Lift(Avg(s, 0.1f, 0.3f, 0.9f, 1f, 0x6A6A78));
            float R = s.W * 0.45f, H = s.H * 0.75f;
            m.Layer = Mat3.Rock; m.Col = c;
            m.Blob(0, H * 0.32f, -R * 0.15f, R, H * 0.62f, R * 0.75f, 1, seed, 0.22f);
            if (s.W > 14)
            {
                m.Col = Mul(c, 0.9f);
                m.Blob(R * 0.6f, H * 0.18f, R * 0.1f, R * 0.45f, H * 0.35f, R * 0.4f, 0, seed + 7, 0.25f);
            }
            if (moss)
            {
                m.Layer = Mat3.Moss; m.Col = Lift(Avg(s, 0.2f, 0f, 0.8f, 0.4f, 0x4A7A3A));
                m.Blob(-R * 0.1f, H * 0.62f, -R * 0.2f, R * 0.75f, H * 0.28f, R * 0.55f, 0, seed + 3, 0.2f);
            }
        }

        static void Tuft(MeshB m, Sprite s, uint seed, int type)
        {
            int c = Lift(Avg(s, 0f, 0f, 1f, 1f, 0x4A7A3A));
            int blades = type == 2 ? 9 : 7;
            float H = type == 2 ? Math.Max(13, s.H * 1.0f) : Math.Max(4.5f, s.H * 0.9f);
            m.Wind = 255;
            for (int i = 0; i < blades; i++)
            {
                float a = i * 2.4f + Hr(seed, i) * 1.2f;
                float r = 0.5f + Hr(seed, 10 + i) * s.W * 0.25f;
                float bx = M.Cos(a) * r, bz = M.Sin(a) * r * 0.6f;
                float h = H * (0.6f + Hr(seed, 20 + i) * 0.5f);
                float lx = bx + M.Cos(a) * h * 0.35f, lz = bz + M.Sin(a) * h * 0.25f;
                m.Col = Mul(c, 0.75f + Hr(seed, 30 + i) * 0.45f);
                float w = type == 2 ? 0.9f : 1.1f;
                m.Tri(new V3(bx - w * M.Sin(a), 0, bz + w * M.Cos(a)), new V3(bx + w * M.Sin(a), 0, bz - w * M.Cos(a)), new V3(lx, h, lz));
                if (type == 2 && i % 3 == 0)
                {
                    m.Col = 0x6A4028;
                    m.BoxC(lx * 0.9f, h * 0.85f, lz * 0.9f, 1.4f, 3.2f, 1.4f);
                }
            }
            if (type == 1)
            {
                int petal = 0xE0C060;
                for (int i = 0; i < s.Px.Length; i++) { int q = s.Px[i]; int r2 = (q >> 16) & 255, g2 = (q >> 8) & 255, b2 = q & 255; if (((q >> 24) & 255) > 200 && (Math.Abs(r2 - g2) > 50 || b2 > g2 + 30)) { petal = q & 0xFFFFFF; break; } }
                m.Em = 25;
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 2.1f + Hr(seed, 50 + i);
                    m.Col = Lift(petal);
                    m.Blob(M.Cos(a) * 2.2f, H * (0.75f + i * 0.12f), M.Sin(a) * 1.5f, 1.3f, 0.8f, 1.3f, 0, seed + (uint)i, 0);
                }
                m.Em = 0;
            }
            m.Wind = 0;
        }

        static void Mushrooms(MeshB m, Sprite s, uint seed, bool glow)
        {
            int cap = Lift(Avg(s, 0.1f, 0f, 0.9f, 0.5f, glow ? 0x60D0C0 : 0xB05040));
            int stem = 0xD8D0C0;
            int n = 2 + (int)(Hr(seed, 0) * 2);
            for (int i = 0; i < n; i++)
            {
                float x = (Hr(seed, 1 + i) - 0.5f) * s.W * 0.6f, z = (Hr(seed, 5 + i) - 0.5f) * 4;
                float h = 2.5f + Hr(seed, 9 + i) * 3, r = 1.6f + Hr(seed, 13 + i) * 1.4f;
                m.Col = stem; m.Em = (byte)(glow ? 60 : 0);
                m.Cyl(x, z, 0, h, 0.6f, 0.5f, 5, false, false, 0);
                m.Col = cap; m.Em = (byte)(glow ? 200 : 0);
                m.Lathe(x, z, new float[] { h - 0.6f, r * 0.9f, h + 0.3f, r, h + r * 0.55f, r * 0.55f, h + r * 0.8f, 0 }, 7, false, 0);
            }
            m.Em = 0;
        }

        static void Lily(MeshB m, Sprite s, uint seed)
        {
            int c = Lift(Avg(s, 0f, 0f, 1f, 1f, 0x3A7A4A));
            m.Col = c; m.Layer = Mat3.Moss;
            float r = Math.Max(3.5f, s.W * 0.4f);
            m.Cyl(0, 0, 0, 0.4f, r, r, 9, true, false, 0.4f);
            if (Hr(seed, 1) > 0.5f) { m.Layer = 0; m.Col = 0xF0D0E0; m.Em = 60; m.Blob(r * 0.3f, 1.2f, 0, 1.4f, 1f, 1.4f, 0, seed, 0); m.Em = 0; }
            m.Layer = 0;
        }

        static void Moonpetal(MeshB m, bool lit)
        {
            m.Col = 0x4A8A6A; m.Wind = 120;
            m.Cyl(0, 0, 0, 7, 0.4f, 0.3f, 4, false, false, 0);
            m.Tri(new V3(0, 2, 0), new V3(3, 4, 0.5f), new V3(0.5f, 3, 0));
            m.Tri(new V3(0, 3, 0), new V3(-3, 5, -0.5f), new V3(-0.5f, 4, 0));
            if (lit)
            {
                m.Col = 0xD8F0FF; m.Em = 230;
                for (int i = 0; i < 5; i++)
                {
                    float a = i * M.TAU / 5;
                    m.Tri(new V3(0, 7, 0), new V3(M.Cos(a) * 3.2f, 7.6f, M.Sin(a) * 3.2f), new V3(M.Cos(a + 0.6f) * 3.2f, 7.9f, M.Sin(a + 0.6f) * 3.2f));
                }
                m.Blob(0, 7.6f, 0, 1, 1, 1, 0, 1, 0);
                m.Em = 0;
            }
            m.Wind = 0;
        }

        static void Stump(MeshB m, Sprite s)
        {
            m.Layer = Mat3.Bark; m.Col = Lift(Avg(s, 0f, 0.3f, 1f, 1f, 0x6A4A30));
            m.Cyl(0, -2, -1, 4, 5, 4.3f, 8, false, false, 0);
            m.Layer = Mat3.Plank; m.Col = 0xC8A070;
            m.Cyl(0, -2, 4, 4, 4.3f, 0, 8, false, false, 0);
        }

        static void LogModel(MeshB m, Sprite s)
        {
            m.Layer = Mat3.Bark; m.Col = Lift(Avg(s, 0f, 0.3f, 1f, 1f, 0x6A4A30));
            m.T = Xf.Move(-8, 3, -1) * Xf.RotZ(-M.PI / 2);
            m.Cyl(0, 0, 0, 16, 3, 2.6f, 7, true, true, 0);
            m.T = Xf.Id;
        }

        // light sources
        static void Ember(MeshB m, int color, float x, float y, float z, float r, float h)
        {
            m.Layer = 0; m.Col = color; m.Em = 255;
            m.Blob(x, y + h * 0.3f, z, r, h * 0.45f, r, 0, 3, 0.15f);
            m.Em = 0;
        }

        static void Campfire(MeshB m, bool lit)
        {
            m.Layer = Mat3.Rock;
            for (int i = 0; i < 8; i++)
            {
                float a = i * M.TAU / 8;
                m.Col = 0x6A6A78 + (i % 2) * 0x080808;
                m.Blob(M.Cos(a) * 8, 1.2f, M.Sin(a) * 6, 2.4f, 1.8f, 2.2f, 0, (uint)i, 0.2f);
            }
            m.Layer = Mat3.Bark; m.Col = lit ? 0x5A3A28 : 0x2A2420;
            for (int i = 0; i < 3; i++)
            {
                m.T = Xf.Move(0, 1, 0) * Xf.Yaw(i * 1.05f) * Xf.Move(-6, 0, 0) * Xf.RotZ(-1.35f);
                m.Cyl(0, 0, 0, 12, 1.2f, 1.0f, 5, true, true, 0);
            }
            m.T = Xf.Id;
            if (lit) Ember(m, 0xFF9030, 0, 0.5f, 0, 3.5f, 3);
        }

        // lamp posts hang their lanterns above a walker's head; wall torches burn just over eye height
        const float LampY = 36.5f, TorchWallY = 22f;

        static void Lantern(MeshB m, bool lit)
        {
            m.Occ = 1;
            m.Layer = Mat3.Timber; m.Col = 0x4A3A30;
            m.BoxC(0, 21.5f, 0, 2.4f, 43, 2.4f);
            m.Box(-2.2f, -1, -2.2f, 2.2f, 3, 2.2f);
            m.BoxC(2.8f, 42.2f, 0, 8, 1.8f, 1.8f);
            m.T = Xf.Move(0.6f, 38.6f, 0) * Xf.RotZ(0.75f); m.BoxC(2.4f, 0, 0, 6, 1.2f, 1.2f); m.T = Xf.Id;
            m.Layer = Mat3.Metal; m.Col = 0x30303A;
            m.BoxC(5, 40.5f, 0, 0.5f, 2.4f, 0.5f);
            m.BoxC(5, LampY + 2.6f, 0, 4.6f, 1, 4.6f);
            m.BoxC(5, LampY - 2.4f, 0, 4.6f, 1, 4.6f);
            m.Cyl(5, 0, LampY + 3, LampY + 5.2f, 2.8f, 0.5f, 4, false, false, M.PI / 4);
            m.Layer = 0; m.Col = lit ? 0xFFD080 : 0x403A40; m.Em = (byte)(lit ? 255 : 0);
            m.BoxC(5, LampY, 0, 3.4f, 4.2f, 3.4f);
            m.Em = 0;
        }

        static void Brazier(MeshB m, int glow)
        {
            m.Layer = Mat3.Metal; m.Col = 0x4A4650;
            for (int i = 0; i < 3; i++)
            {
                float a = i * M.TAU / 3 + 0.5f;
                m.T = Xf.Move(M.Cos(a) * 3, 0, M.Sin(a) * 3) * Xf.Rot(new V3(-M.Sin(a), 0, M.Cos(a)), 0.25f);
                m.Cyl(0, 0, 0, 11, 0.7f, 0.6f, 4, false, false, 0);
            }
            m.T = Xf.Id;
            m.Lathe(0, 0, new float[] { 10, 1.5f, 11, 4.5f, 14, 6.5f, 15, 6.8f }, 8, false, 0);
            if (glow >= 0) Ember(m, glow, 0, 13.6f, 0, 5, 2.4f);
            else { m.Col = 0x2A2420; m.Cyl(0, 0, 13.4f, 14, 6, 5, 8, true, false, 0); }
        }

        static void Torch(MeshB m, int glow, bool onWall)
        {
            float y0 = onWall ? TorchWallY : 0;
            m.Layer = Mat3.Metal; m.Col = 0x3A3640;
            if (onWall) m.BoxC(0, y0 + 2, 1, 2, 4, 2.4f);
            m.Layer = Mat3.Timber; m.Col = 0x5A4030;
            m.T = Xf.Move(0, y0, onWall ? 2.2f : 0) * Xf.RotX(onWall ? 0.35f : 0);
            m.Cyl(0, 0, onWall ? 0 : -1, onWall ? 9 : 16, 0.9f, 1.2f, 5, false, false, 0);
            m.Layer = 0; m.Col = 0x2A2420;
            m.Cyl(0, 0, onWall ? 8 : 15, onWall ? 10 : 17, 1.6f, 1.8f, 6, true, false, 0);
            Ember(m, glow, 0, onWall ? 9.6f : 16.4f, 0, 1.8f, 2.4f);
            m.T = Xf.Id;
        }

        static void Candles(MeshB m, uint seed)
        {
            m.Layer = Mat3.Metal; m.Col = 0xC8A040;
            m.Cyl(0, 0, 0, 1, 3.5f, 3, 8, true, false, 0);
            m.Cyl(0, 0, 1, 16, 0.6f, 0.6f, 5, false, false, 0);
            m.BoxC(0, 16, 0, 10, 0.8f, 1);
            float[] xs = { -4.5f, 0, 4.5f };
            for (int i = 0; i < 3; i++)
            {
                m.Layer = 0; m.Col = 0xEDE4CC;
                float hh = i == 1 ? 4 : 3;
                m.Cyl(xs[i], 0, 16.4f, 16.4f + hh, 0.8f, 0.8f, 5, true, false, 0);
                Ember(m, 0xFFC070, xs[i], 16.6f + hh, 0, 0.7f, 2.2f);
            }
        }

        static void Shrine(MeshB m, bool lit)
        {
            m.Occ = 1;
            m.Layer = Mat3.Brick; m.Col = 0x8088A0;
            m.Box(-10, -2, -7, 10, 5, 5);
            m.Layer = Mat3.Rock; m.Col = 0x6A7088;
            float[] px = { -8, 8 }, pz = { -5, 3 };
            foreach (float x in px) foreach (float z in pz) m.BoxC(x, 15, z, 3, 20, 3);
            m.Layer = Mat3.Slate; m.Col = 0x4A5068;
            m.Cyl(0, -1, 25, 36, 15, 0, 4, false, true, M.PI / 4);
            m.Layer = Mat3.Metal; m.Col = 0x3A3A44;
            m.BoxC(0, 23, -1, 1, 3, 1);
            m.Col = lit ? 0xF0C060 : 0x9A8A5A; m.Em = (byte)(lit ? 140 : 0);
            m.Lathe(0, -1, new float[] { 13, 4.6f, 14, 4.2f, 17, 3, 20, 2.2f, 21.5f, 0 }, 9, false, 0);
            m.Em = 0;
            if (lit) { m.Layer = 0; m.Col = 0xFFE0A0; m.Em = 255; m.Blob(0, 6.5f, -1, 4, 0.8f, 3, 0, 1, 0); m.Em = 0; }
        }

        static void Altar(MeshB m)
        {
            m.Layer = Mat3.Brick; m.Col = 0x8A90A8;
            m.Box(-14, -1, -6, 14, 9, 5);
            m.Box(-15, 9, -7, 15, 10.5f, 6);
            m.Layer = Mat3.Cloth; m.Col = 0xA02838;
            m.Box(-4, 2, 5, 4, 10.6f, 6.2f);
            m.Box(-4, 10.5f, -7, 4, 10.9f, 6.2f);
            m.Layer = 0; m.Col = 0xE8C050; m.Em = 120;
            m.BoxC(0, 7, 6.4f, 2.4f, 3, 0.4f);
            m.Em = 0;
            for (int i = -1; i <= 1; i += 2)
            {
                m.Col = 0xEDE4CC; m.Cyl(i * 11, -2, 10.5f, 15, 0.9f, 0.9f, 5, true, false, 0);
                Ember(m, 0xFFC070, i * 11, 15.2f, -2, 0.7f, 2.2f);
            }
        }

        // containers and village clutter
        static void Chest(MeshB m, bool ornate, bool open)
        {
            int wood = ornate ? 0x5A3A8A : 0x8A5A34, band = ornate ? 0xE8C050 : 0x6A6A78;
            m.Layer = Mat3.Plank; m.Col = wood;
            m.Box(-8, 0, -6, 8, 8, 4);
            m.Layer = Mat3.Metal; m.Col = band;
            m.Box(-8.3f, 0, -6.3f, -6.3f, 8.3f, 4.3f); m.Box(6.3f, 0, -6.3f, 8.3f, 8.3f, 4.3f);
            Xf lid = open ? Xf.Move(0, 8, -6) * Xf.RotX(-1.9f) * Xf.Move(0, -8, 6) : Xf.Id;
            m.T = lid;
            m.Layer = Mat3.Plank; m.Col = Mul(wood, 1.08f);
            m.T = lid * Xf.Move(0, 8, -1) * Xf.RotZ(M.PI / 2);
            m.Cyl(0, 0, -8, 8, 5, 5, 8, true, true, 0);
            m.T = lid;
            m.Layer = Mat3.Metal; m.Col = band;
            m.BoxC(0, 9, 4.4f, 2.4f, 3.4f, 0.8f);
            m.T = Xf.Id;
            if (open)
            {
                m.Layer = 0; m.Col = 0xF0C850; m.Em = 120;
                m.Blob(0, 7, -1, 6, 1.4f, 4, 0, 5, 0.3f);
                m.Em = 0;
            }
        }

        static void Barrel(MeshB m)
        {
            m.Layer = Mat3.Bark; m.Col = 0x9A6A40;
            m.Lathe(0, -1, new float[] { 0, 4.6f, 3, 5.4f, 7, 5.8f, 11, 5.4f, 14, 4.6f }, 10, true, 0);
            m.Layer = Mat3.Metal; m.Col = 0x4A4A55;
            m.Cyl(0, -1, 2.2f, 3.4f, 5.45f, 5.6f, 10, false, false, 0);
            m.Cyl(0, -1, 10.6f, 11.8f, 5.55f, 5.35f, 10, false, false, 0);
        }

        static void Crate(MeshB m, float s)
        {
            m.Layer = Mat3.Plank; m.Col = 0xA0784A;
            m.Box(-s / 2, 0, -s / 2 - 1, s / 2, s, s / 2 - 1);
            m.Layer = Mat3.Timber; m.Col = 0x6A4A30;
            m.Box(-s / 2 - 0.3f, 0, s / 2 - 1.2f, -s / 2 + 1.4f, s, s / 2 - 0.7f);
            m.Box(s / 2 - 1.4f, 0, s / 2 - 1.2f, s / 2 + 0.3f, s, s / 2 - 0.7f);
            m.Box(-s / 2, s - 1.4f, s / 2 - 1.2f, s / 2, s + 0.3f, s / 2 - 0.7f);
            m.Box(-s / 2, -0.3f, s / 2 - 1.2f, s / 2, 1.4f, s / 2 - 0.7f);
        }

        static void Urn(MeshB m, int c, bool stone)
        {
            m.Layer = (byte)(stone ? Mat3.Rock : Mat3.Plaster); m.Col = c;
            m.Lathe(0, -1, new float[] { 0, 2.6f, 2, 4.2f, 5, 4.8f, 8, 3.6f, 10, 2, 11, 2.4f, 11.6f, 2.6f }, 9, false, 0);
            m.Col = Mul(c, 0.4f);
            m.Cyl(0, -1, 11.6f, 11.6f, 2, 0, 9, false, false, 0);
        }

        static void Sack(MeshB m)
        {
            m.Layer = Mat3.Cloth; m.Col = 0xB89A6A;
            m.Blob(0, 3.6f, 0, 4.6f, 3.8f, 4, 1, 9, 0.12f);
            m.Col = 0x8A6A4A; m.Cyl(0, 0, 6.5f, 8, 1.2f, 1.8f, 6, true, false, 0);
        }

        static void Cart(MeshB m)
        {
            m.Occ = 1;
            m.Layer = Mat3.Plank; m.Col = 0x8A6038;
            m.Box(-14, 6, -7, 10, 8, 5);
            m.Box(-14, 8, -7, 10, 12, -6); m.Box(-14, 8, 4, 10, 12, 5); m.Box(-14, 8, -7, -13, 12, 5);
            m.Layer = Mat3.Straw; m.Col = 0xD8B060;
            m.Blob(-2, 11, -1, 10, 3.6f, 5, 1, 2, 0.2f);
            m.Layer = Mat3.Timber; m.Col = 0x5A3A26;
            for (int s = -1; s <= 1; s += 2)
            {
                m.T = Xf.Move(-3, 6, s * 6.5f) * Xf.RotX(M.PI / 2);
                m.Cyl(0, 0, -0.9f, 0.9f, 6, 6, 10, true, true, 0);
            }
            m.T = Xf.Move(10, 7, 0) * Xf.RotZ(-1.35f);
            m.Cyl(-2.5f, 0, 0, 14, 0.7f, 0.7f, 4, false, false, 0);
            m.Cyl(2.5f, 0, 0, 14, 0.7f, 0.7f, 4, false, false, 0);
            m.T = Xf.Id;
        }

        static void Hay(MeshB m)
        {
            m.Layer = Mat3.Straw; m.Col = 0xD8B060;
            m.Box(-8, 0, -6, 8, 8, 4);
            m.Layer = Mat3.Cloth; m.Col = 0x7A5A3A;
            m.Box(-4, -0.2f, -6.2f, -3, 8.2f, 4.2f); m.Box(3, -0.2f, -6.2f, 4, 8.2f, 4.2f);
        }

        static void Woodpile(MeshB m)
        {
            m.Layer = Mat3.Bark;
            int row = 0;
            for (int layer = 0; layer < 3; layer++)
                for (int i = 0; i < 4 - layer; i++)
                {
                    m.Col = 0x8A6038 + ((row++ * 0x070503) & 0x0F0F0F);
                    m.T = Xf.Move(-7.5f + layer * 2.5f + i * 5, 2.4f + layer * 4.2f, -6) * Xf.RotX(M.PI / 2);
                    m.Cyl(0, 0, -10, 0, 2.3f, 2.3f, 6, true, true, 0.5f);
                }
            m.T = Xf.Id;
        }

        static void Anvil(MeshB m)
        {
            m.Layer = Mat3.Bark; m.Col = 0x6A4A30;
            m.Cyl(0, 0, 0, 5, 4.5f, 4.2f, 8, true, false, 0);
            m.Layer = Mat3.Metal; m.Col = 0x5A5C68;
            m.Box(-3, 5, -2, 3, 7, 2);
            m.Box(-5, 7, -2.6f, 5, 10, 2.6f);
            m.T = Xf.Move(5, 8.5f, 0) * Xf.RotZ(-M.PI / 2);
            m.Cyl(0, 0, 0, 5, 1.6f, 0, 6, false, false, 0);
            m.T = Xf.Id;
        }

        static void Sign(MeshB m)
        {
            m.Layer = Mat3.Timber; m.Col = 0x6A4A30;
            m.BoxC(0, 9, 0, 1.8f, 18, 1.8f);
            m.Layer = Mat3.Plank; m.Col = 0xA07850;
            m.Box(-2, 12, 0.6f, 10, 15, 1.6f);
            m.Tri(new V3(10, 11, 1.6f), new V3(13, 13.5f, 1.6f), new V3(10, 16, 1.6f));
            m.Box(-10, 7.5f, -0.4f, 2, 10.5f, 0.6f);
            m.Tri(new V3(-10, 6.5f, 0.6f), new V3(-10, 11.5f, 0.6f), new V3(-13, 9, 0.6f));
        }

        static void Notice(MeshB m)
        {
            m.Occ = 1;
            m.Layer = Mat3.Timber; m.Col = 0x5A4030;
            m.BoxC(-11, 10, 0, 2, 20, 2); m.BoxC(11, 10, 0, 2, 20, 2);
            m.Layer = Mat3.Plank; m.Col = 0x9A7048;
            m.Box(-11, 8, -0.6f, 11, 19, 0.6f);
            m.Layer = Mat3.Slate; m.Col = 0x5A3A30;
            m.Box(-13, 19, -3, 13, 20.5f, 3);
            m.Layer = 0;
            int[] paper = { 0xE8E0C8, 0xD8D0B8, 0xF0E8D8, 0xE0D4B0 };
            for (int i = 0; i < 4; i++) { m.Col = paper[i]; m.Box(-9 + i * 4.8f, 10.5f + (i % 2) * 2, 0.6f, -6 + i * 4.8f, 15.5f + (i % 2) * 2, 0.9f); }
        }

        static void Fence(MeshB m, bool vertical)
        {
            m.Layer = Mat3.Timber; m.Col = 0x6A4A32;
            if (!vertical)
            {
                m.BoxC(-7, 5, -1.5f, 2, 10, 2); m.BoxC(7, 5, -1.5f, 2, 10, 2);
                m.Col = 0x7A5838;
                m.Box(-9, 6.5f, -1.2f, 9, 8, -0.2f); m.Box(-9, 2.8f, -1.2f, 9, 4.2f, -0.2f);
            }
            else
            {
                m.BoxC(0, 5, -7, 2, 10, 2); m.BoxC(0, 5, 7, 2, 10, 2);
                m.Col = 0x7A5838;
                m.Box(-0.5f, 6.5f, -9, 0.5f, 8, 9); m.Box(-0.5f, 2.8f, -9, 0.5f, 4.2f, 9);
            }
        }

        static void Bench(MeshB m, bool pew)
        {
            m.Layer = Mat3.Plank; m.Col = pew ? 0x6A4430 : 0x8A6038;
            float w = pew ? 22 : 12;
            m.Box(-w, 5, -4, w, 6.4f, 1);
            m.Layer = Mat3.Timber;
            m.Box(-w + 1, 0, -3.5f, -w + 2.6f, 5, 0.5f); m.Box(w - 2.6f, 0, -3.5f, w - 1, 5, 0.5f);
            if (pew) { m.Layer = Mat3.Plank; m.Box(-w, 6.4f, -4.4f, w, 15, -3); m.Box(-w - 1, 0, -4.4f, -w, 13, 1.4f); m.Box(w, 0, -4.4f, w + 1, 13, 1.4f); }
        }

        static void Banner(MeshB m, Sprite s)
        {
            m.Occ = 1;
            m.Layer = Mat3.Timber; m.Col = 0x5A4030;
            m.Cyl(0, 0, 0, 32, 0.9f, 0.8f, 5, false, false, 0);
            m.Layer = Mat3.Metal; m.Col = 0xE0B050;
            m.Blob(0, 32.5f, 0, 1.2f, 1.2f, 1.2f, 0, 1, 0);
            m.Layer = Mat3.Cloth; m.Col = Lift(Avg(s, 0.3f, 0.2f, 1f, 0.8f, 0xB02838)); m.Wind = 120;
            m.Box(1, 13, -0.4f, 10, 30, 0.4f);
            m.Tri(new V3(1, 13, 0.4f), new V3(10, 13, 0.4f), new V3(5.5f, 9, 0.4f));
            m.Tri(new V3(10, 13, -0.4f), new V3(1, 13, -0.4f), new V3(5.5f, 9, -0.4f));
            m.Layer = 0; m.Col = 0xF0C050; m.Em = 60;
            m.BoxC(5.5f, 23, 0.5f, 3.2f, 3.2f, 0.3f);
            m.Em = 0; m.Wind = 0;
        }

        static void Well(MeshB m)
        {
            m.Occ = 1;
            m.Layer = Mat3.Brick; m.Col = 0x7A8098;
            for (int i = 0; i < 12; i++)
            {
                float a0 = i * M.TAU / 12, a1 = (i + 1) * M.TAU / 12;
                V3 o0 = new V3(M.Cos(a0) * 10, 0, M.Sin(a0) * 7 - 5), o1 = new V3(M.Cos(a1) * 10, 0, M.Sin(a1) * 7 - 5);
                V3 i0 = new V3(M.Cos(a0) * 7.5f, 0, M.Sin(a0) * 5 - 5), i1 = new V3(M.Cos(a1) * 7.5f, 0, M.Sin(a1) * 5 - 5);
                V3 up = new V3(0, 8, 0);
                m.Quad(o1, o0, o0 + up, o1 + up);
                m.Quad(i0, i1, i1 + up, i0 + up);
                m.Quad(o0 + up, i0 + up, i1 + up, o1 + up);
            }
            m.Layer = 0; m.Col = 0x18303A; m.Em = 30;
            m.Cyl(0, -5, 4, 4, 7.5f, 0, 12, false, false, 0);
            m.Em = 0;
            m.Layer = Mat3.Timber; m.Col = 0x5A4030;
            m.BoxC(-9, 13, -5, 2, 26, 2); m.BoxC(9, 13, -5, 2, 26, 2);
            m.T = Xf.Move(-9, 21, -5) * Xf.RotZ(-M.PI / 2);
            m.Cyl(0, 0, 0, 18, 1, 1, 6, false, false, 0);
            m.T = Xf.Id;
            m.Layer = Mat3.Slate; m.Col = 0x6A4A38;
            m.Quad(new V3(-13, 24, 3), new V3(13, 24, 3), new V3(13, 31, -5), new V3(-13, 31, -5));
            m.Quad(new V3(13, 24, -13), new V3(-13, 24, -13), new V3(-13, 31, -5), new V3(13, 31, -5));
            m.Layer = Mat3.Plank; m.Col = 0x8A6038;
            m.Tri(new V3(-12, 24, 2.5f), new V3(-12, 31, -5), new V3(-12, 24, -12.5f));
            m.Tri(new V3(12, 24, -12.5f), new V3(12, 31, -5), new V3(12, 24, 2.5f));
            m.Layer = Mat3.Bark; m.Col = 0x7A5838;
            m.Cyl(2, -5, 12, 16, 2, 2.4f, 7, false, true, 0);
        }

        static void Stall(MeshB m, int stripe)
        {
            m.Occ = 1;
            m.Layer = Mat3.Plank; m.Col = 0x8A6038;
            m.Box(-17, 6, -6, 17, 8, 3);
            m.Box(-17, 0, 2, 17, 6, 3);
            m.Layer = Mat3.Timber; m.Col = 0x5A4030;
            foreach (float x in new[] { -16f, 16f }) { m.BoxC(x, 12, 2, 1.6f, 24, 1.6f); m.BoxC(x, 14, -6, 1.6f, 28, 1.6f); }
            m.Layer = Mat3.Cloth;
            for (int i = 0; i < 8; i++)
            {
                float x0 = -18 + i * 4.5f, x1 = x0 + 4.5f;
                m.Col = i % 2 == 0 ? stripe : 0xEDE4D0; m.Wind = 25;
                m.Quad(new V3(x0, 23, 5), new V3(x1, 23, 5), new V3(x1, 28.5f, -7), new V3(x0, 28.5f, -7));
                m.Quad(new V3(x0, 19.5f, 5.2f), new V3(x1, 19.5f, 5.2f), new V3(x1, 23, 5.2f), new V3(x0, 23, 5.2f));
            }
            m.Wind = 0; m.Layer = 0;
            int[] goods = { 0xC84A3A, 0x6AA04A, 0xE0A040, 0x8A5AB0, 0xD8C060, 0x6A8AC0 };
            for (int i = 0; i < 6; i++) { m.Col = goods[i]; m.Blob(-13 + i * 5.2f, 9.4f, -1.5f, 2, 1.6f, 2, 0, (uint)i, 0.15f); }
        }

        static void Bedroll(MeshB m, Sprite s)
        {
            m.Layer = Mat3.Cloth; m.Col = Lift(Avg(s, 0f, 0f, 1f, 1f, 0x5A7A4A));
            m.Box(-9, 0, -4, 7, 1.6f, 3);
            m.T = Xf.Move(9, 2.4f, 0) * Xf.RotX(M.PI / 2);
            m.Cyl(0, 0, -4, 3, 2.4f, 2.4f, 8, true, true, 0);
            m.T = Xf.Id;
        }

        static void Tent(MeshB m, Sprite s)
        {
            m.Occ = 1;
            int c = Lift(Avg(s, 0.2f, 0.3f, 0.8f, 0.9f, 0xC8B890));
            m.Layer = Mat3.Cloth; m.Col = c; m.Wind = 18;
            V3 a = new V3(-20, 0, 6), b = new V3(20, 0, 6), cc = new V3(20, 0, -18), d = new V3(-20, 0, -18);
            V3 t0 = new V3(0, 26, 6), t1 = new V3(0, 26, -18);
            m.Quad(a, new V3(0, 0, 6), t0, new V3(-20, 0, 6));
            m.Quad(d, a, t0, t1);
            m.Quad(b, cc, t1, t0);
            m.Wind = 0;
            m.Col = Mul(c, 0.8f);
            m.Tri(new V3(-20, 0, 6), new V3(-4, 0, 6), t0);
            m.Tri(new V3(4, 0, 6), new V3(20, 0, 6), t0);
            m.Layer = 0; m.Col = 0x141018;
            m.Tri(new V3(-4, 0, 6.2f), new V3(4, 0, 6.2f), new V3(0, 18, 6.2f));
            m.Layer = Mat3.Timber; m.Col = 0x5A4030;
            m.BoxC(0, 28, 6, 1.4f, 6, 1.4f);
        }

        static void Bones(MeshB m, uint seed)
        {
            m.Layer = Mat3.Bone; m.Col = 0xE0D8C0;
            for (int i = 0; i < 5; i++)
            {
                float a = Hr(seed, i) * 6;
                m.T = Xf.Move((Hr(seed, 10 + i) - 0.5f) * 12, 0.6f, (Hr(seed, 20 + i) - 0.5f) * 6) * Xf.Yaw(a) * Xf.RotZ(-M.PI / 2);
                m.Cyl(0, 0, -3, 3, 0.6f, 0.6f, 4, true, true, 0);
            }
            m.T = Xf.Id;
            m.Blob(3, 1.8f, 1, 2.2f, 2, 2.2f, 0, seed, 0.1f);
            m.Layer = 0; m.Col = 0x141018;
            m.BoxC(4.2f, 2, 2.6f, 0.8f, 0.8f, 0.4f); m.BoxC(2.8f, 2, 2.8f, 0.8f, 0.8f, 0.4f);
        }

        static void Skulls(MeshB m, uint seed)
        {
            m.Layer = Mat3.Bone;
            int k = 0;
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 4 - row; i++)
                {
                    float x = -6 + i * 4 + row * 2, y = 2 + row * 3.4f;
                    m.Col = Mix(0xD8D0B8, 0xB8B098, Hr(seed, k++));
                    m.Blob(x, y, -2 + row * 0.5f, 2.2f, 2, 2.2f, 0, seed + (uint)k, 0.1f);
                    m.Layer = 0; m.Col = 0x141018;
                    m.BoxC(x - 0.8f, y + 0.2f, 0.1f + row * 0.5f, 0.8f, 0.8f, 0.3f); m.BoxC(x + 0.8f, y + 0.2f, 0.1f + row * 0.5f, 0.8f, 0.8f, 0.3f);
                    m.Layer = Mat3.Bone;
                }
        }

        // dungeon furniture
        static void Gate(MeshB m, int colW, bool open, float ceil)
        {
            float w = colW > 0 ? Math.Max(24, colW) : 32;
            // under a ceiling the portcullis stands a head above the hero and drops from a stone archway
            float bh = ceil > 0 ? 50 : 30, y0 = open ? (ceil > 0 ? 44 : 26) : 0;
            m.Occ = 1;
            m.Layer = Mat3.Metal; m.Col = 0x4A4C58;
            int bars = (int)(w / 4.5f);
            for (int i = 0; i <= bars; i++)
            {
                float x = -w / 2 + i * w / bars;
                m.Box(x - 0.8f, y0, -8.6f, x + 0.8f, y0 + bh, -7);
                if (!open || ceil > 0) m.Cyl(x, -7.8f, y0 - 2.4f, y0, 0, 0.8f, 4, false, false, 0);
            }
            m.Box(-w / 2, y0 + bh * 0.27f, -8.8f, w / 2, y0 + bh * 0.27f + 1.6f, -6.8f);
            m.Box(-w / 2, y0 + bh * 0.68f, -8.8f, w / 2, y0 + bh * 0.68f + 1.6f, -6.8f);
            if (ceil > 0)
            {
                m.Layer = Mat3.DBrick; m.Col = 0x8A84A0;
                m.Box(-w / 2, 52, -32, w / 2, ceil + 1, 0);
                m.Layer = Mat3.Brick; m.Col = 0x6A6480;
                m.Box(-w / 2 - 1, 50, -2, w / 2 + 1, 53, 1.2f);
            }
        }

        static void SecretWall(MeshB m, Sprite s, float ceil)
        {
            m.Layer = Mat3.DBrick; m.Col = 0x8A84A0; m.Occ = 1;
            float w = ceil > 0 ? Math.Max(32, s.W) : s.W;
            m.Box(-w / 2f, 0, -10, w / 2f, ceil > 0 ? ceil : 34, 0);
            m.Layer = 0; m.Col = 0x18141E;
            m.Box(-2, 6, 0, -1, 20, 0.3f); m.Box(-1, 18, 0, 4, 19, 0.3f); m.Box(3, 9, 0, 4, 19, 0.3f); m.Box(-6, 12, 0, -1.5f, 13, 0.3f);
        }

        static void Stairs(MeshB m, Sprite s, float ceil)
        {
            m.Layer = Mat3.Rock; m.Col = 0x7A7890;
            float w = Math.Max(20, s.W * 0.9f);
            if (ceil > 0)
            {
                // seen from inside: a stair climbing into a dark archway in the wall, faint moonlight from above
                float hw = Math.Max(11, w * 0.5f), ah = 50;
                for (int i = 0; i < 3; i++) m.Box(-hw, 0, 9 - (i + 1) * 3, hw, 2.4f + i * 2.4f, 9 - i * 3);
                m.Layer = 0; m.Col = 0x07060A;
                m.Box(-hw, 7.2f, -0.6f, hw, ah, 0.05f);
                for (int i = 0; i < 8; i++)
                {
                    float a0 = i * M.PI / 8, a1 = (i + 1) * M.PI / 8;
                    m.Tri(new V3(0, ah, 0.05f), new V3(M.Cos(a0) * hw, ah + M.Sin(a0) * hw, 0.05f), new V3(M.Cos(a1) * hw, ah + M.Sin(a1) * hw, 0.05f));
                }
                m.Col = 0x141A2C; m.Em = 40;
                m.Box(-hw + 3, ah - 4, 0.06f, hw - 3, ah + hw * 0.45f, 0.1f);
                m.Col = 0x161A28; m.Em = 30;
                m.Box(-hw + 1, 7.2f, 0.06f, hw - 1, 16, 0.1f);
                m.Em = 0;
                m.Layer = Mat3.DBrick; m.Col = 0x6A6480;
                m.Box(-hw - 4, 0, -0.5f, -hw, ah, 2.5f); m.Box(hw, 0, -0.5f, hw + 4, ah, 2.5f);
                const int segs = 7;
                for (int i = 0; i < segs; i++)
                {
                    float a0 = M.PI - i * M.PI / segs, a1 = M.PI - (i + 1) * M.PI / segs, r0 = hw + 2;
                    V3 p0 = new V3(M.Cos(a0) * r0, ah + M.Sin(a0) * r0, 0), p1 = new V3(M.Cos(a1) * r0, ah + M.Sin(a1) * r0, 0);
                    m.T = Xf.Move((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2, 1f) * Xf.RotZ(M.Atan2(p1.Y - p0.Y, p1.X - p0.X));
                    m.BoxC(0, 0, 0, (p1 - p0).Len + 1, 4, 3);
                }
                m.T = Xf.Id;
                return;
            }
            for (int i = 0; i < 6; i++)
                m.Box(-w / 2, 0, -2 - i * 4, w / 2, 2 + i * 4.5f, 2 - i * 4);
            m.Layer = Mat3.DBrick; m.Col = 0x6A6480;
            m.Box(-w / 2 - 4, 0, -26, -w / 2, 30, 2); m.Box(w / 2, 0, -26, w / 2 + 4, 30, 2);
            m.Layer = 0; m.Col = 0x8A8AC0; m.Em = 90;
            m.Box(-w / 2 + 1, 26, -24, w / 2 - 1, 30, -22);
            m.Em = 0;
        }

        static void Bell(MeshB m, Sprite s, bool rung)
        {
            int c = Lift(Avg(s, 0.2f, 0.3f, 0.8f, 0.9f, rung ? 0xD8B050 : 0x8A9A6A));
            float H = s.H * 0.95f, R = s.W * 0.5f;
            m.Occ = 1;
            m.Layer = Mat3.Metal; m.Col = c; m.Em = (byte)(rung ? 70 : 0);
            m.Lathe(0, -4, new float[] { 0, R, H * 0.08f, R * 0.96f, H * 0.2f, R * 0.78f, H * 0.45f, R * 0.62f, H * 0.75f, R * 0.56f, H * 0.9f, R * 0.45f, H, R * 0.2f }, 14, true, 0);
            m.Col = Mul(c, 0.75f);
            m.Cyl(0, -4, H * 0.14f, H * 0.17f, R * 0.88f, R * 0.86f, 14, false, false, 0);
            m.Cyl(0, -4, H * 0.6f, H * 0.63f, R * 0.6f, R * 0.58f, 14, false, false, 0);
            m.Col = 0x3A3A44; m.Em = 0;
            m.BoxC(0, H + 2, -4, 4, 4, 2);
            if (!rung)
            {
                m.Layer = Mat3.Moss; m.Col = 0x5A7A4A;
                m.Blob(-R * 0.4f, H * 0.35f, -4 + R * 0.5f, R * 0.3f, H * 0.12f, R * 0.25f, 0, 3, 0.3f);
            }
        }

        static void WallBanner(MeshB m, Sprite s)
        {
            m.Layer = Mat3.Metal; m.Col = 0x3A3640;
            m.BoxC(0, 30, 0.8f, 12, 1.4f, 1.6f);
            m.Layer = Mat3.Cloth; m.Col = Lift(Avg(s, 0.2f, 0.1f, 0.8f, 0.8f, 0xA02838)); m.Wind = 20;
            m.Box(-5, 12, 0.6f, 5, 30, 1.4f);
            m.Tri(new V3(-5, 12, 1.4f), new V3(5, 12, 1.4f), new V3(0, 8, 1.4f));
            m.Layer = 0; m.Col = 0xE8B850; m.Em = 70;
            m.BoxC(0, 23, 1.5f, 3.4f, 3.4f, 0.3f);
            m.Em = 0; m.Wind = 0;
        }

        static void Coffin(MeshB m, Sprite s)
        {
            m.Layer = Mat3.Plank; m.Col = Lift(Avg(s, 0.2f, 0.2f, 0.8f, 0.9f, 0x6A4430));
            V3[] o = { new V3(-3.5f, 0, 0), new V3(3.5f, 0, 0), new V3(5.5f, 17, 0), new V3(3, 26, 0), new V3(-3, 26, 0), new V3(-5.5f, 17, 0) };
            for (int i = 0; i < 6; i++)
            {
                V3 a = o[i], b = o[(i + 1) % 6];
                m.Quad(new V3(a.X, a.Y, 5), new V3(b.X, b.Y, 5), new V3(b.X, b.Y, 0.5f), new V3(a.X, a.Y, 0.5f));
            }
            for (int i = 1; i < 5; i++) m.Tri(new V3(o[0].X, o[0].Y, 5), new V3(o[i].X, o[i].Y, 5), new V3(o[i + 1].X, o[i + 1].Y, 5));
            m.Layer = Mat3.Metal; m.Col = 0xC8A050;
            m.BoxC(0, 16, 5.2f, 1.2f, 9, 0.4f); m.BoxC(0, 18, 5.2f, 5, 1.2f, 0.4f);
        }

        static void Sarcophagus(MeshB m, bool open)
        {
            m.Occ = 1;
            m.Layer = Mat3.Rock; m.Col = 0x8A90A8;
            m.Box(-16, 0, -10, 16, 10, 4);
            m.Layer = Mat3.Brick; m.Col = 0x7A8098;
            Xf lid = open ? Xf.Move(6, 3, 3) * Xf.Yaw(0.25f) * Xf.RotZ(-0.12f) : Xf.Id;
            m.T = lid;
            m.Box(-17, 10, -11, 17, 13, 5);
            m.Box(-12, 13, -8, 12, 14.5f, 2);
            m.T = Xf.Id;
            if (open) { m.Layer = 0; m.Col = 0x141018; m.Box(-14, 9.8f, -8, 6, 10.1f, 2); }
        }

        static void Bookshelf(MeshB m, Sprite s)
        {
            m.Occ = 1;
            m.Layer = Mat3.Plank; m.Col = 0x6A4430;
            float w = Math.Max(16, s.W * 0.48f), h = Math.Max(30, s.H * 0.9f);
            m.Box(-w, 0, -6, w, h, -5); m.Box(-w, 0, -6, -w + 1.6f, h, 2); m.Box(w - 1.6f, 0, -6, w, h, 2);
            m.Box(-w, h - 1.6f, -6, w, h, 2);
            m.Layer = Mat3.Books; m.Col = 0xFFFFFF;
            for (int k = 0; k < 3; k++)
            {
                float y0 = 1 + k * (h - 2) / 3f;
                m.Layer = Mat3.Plank; m.Col = 0x6A4430; m.Box(-w, y0 - 1, -5, w, y0, 2);
                m.Layer = Mat3.Books; m.Col = 0xFFFFFF;
                m.QuadUv(new V3(-w + 1.6f, y0, 1), new V3(w - 1.6f, y0, 1), new V3(w - 1.6f, y0 + (h - 2) / 3f - 2, 1), new V3(-w + 1.6f, y0 + (h - 2) / 3f - 2, 1), k * 0.3f, 0, k * 0.3f + w / 16f, 0.5f);
            }
        }

        // landmarks
        static void Abbey(MeshB m, int colW, int colH)
        {
            float w = Math.Max(160, colW), d = Math.Max(48, colH) + 30;
            m.Occ = 1;
            int stone = 0x80889E, dark = 0x60687E;
            // heights for a church seen from the ground: nave wall and ridge, aisle wall and roof, bell tower
            const float NH = 118, RH = 172, AH = 62, AR = 90, TH = 214;
            // nave
            m.Layer = Mat3.Brick; m.Col = stone;
            float nw = w * 0.34f;
            m.Box(-nw, -3, -d, nw, NH, 0);
            m.Layer = Mat3.Slate; m.Col = 0x4A5068;
            m.Quad(new V3(-nw - 4, NH - 2, 2), new V3(0, RH, 2), new V3(0, RH, -d - 2), new V3(-nw - 4, NH - 2, -d - 2));
            m.Quad(new V3(0, RH, 2), new V3(nw + 4, NH - 2, 2), new V3(nw + 4, NH - 2, -d - 2), new V3(0, RH, -d - 2));
            m.Layer = Mat3.Brick; m.Col = stone;
            m.Tri(new V3(-nw, NH, 0.1f), new V3(nw, NH, 0.1f), new V3(0, RH - 2, 0.1f));
            m.Tri(new V3(nw, NH, -d - 0.1f), new V3(-nw, NH, -d - 0.1f), new V3(0, RH - 2, -d - 0.1f));
            // a string course under the gable
            m.Col = Mul(stone, 0.85f);
            m.Box(-nw - 1, NH - 4, -1, nw + 1, NH, 1.6f);
            // aisles
            m.Layer = Mat3.Brick; m.Col = dark;
            m.Box(-w / 2, -3, -d + 8, -nw, AH, -2); m.Box(nw, -3, -d + 8, w / 2, AH, -2);
            m.Layer = Mat3.Slate; m.Col = 0x4A5068;
            m.Quad(new V3(-w / 2 - 2, AH - 2, 0), new V3(-nw, AR, 0), new V3(-nw, AR, -d + 6), new V3(-w / 2 - 2, AH - 2, -d + 6));
            m.Quad(new V3(nw, AR, 0), new V3(w / 2 + 2, AH - 2, 0), new V3(w / 2 + 2, AH - 2, -d + 6), new V3(nw, AR, -d + 6));
            m.Layer = Mat3.Brick; m.Col = dark;
            m.Tri(new V3(-w / 2, AH, -1.9f), new V3(-nw, AH, -1.9f), new V3(-nw, AR - 2, -1.9f));
            m.Tri(new V3(nw, AH, -1.9f), new V3(w / 2, AH, -1.9f), new V3(nw, AR - 2, -1.9f));
            // buttresses, stepped
            m.Layer = Mat3.Brick; m.Col = Mul(stone, 1.05f);
            foreach (float x in new[] { -nw - 6, nw - 1, -w / 2 + 2, w / 2 - 9 })
            {
                bool nave = x > -nw - 7 && x < nw;
                float top = nave ? NH - 10 : AH - 6;
                m.Box(x, -3, -2, x + 7, top * 0.55f, 6);
                m.Box(x + 0.6f, top * 0.55f, -2, x + 6.4f, top, 3.5f);
                m.Quad(new V3(x, top * 0.55f, 6), new V3(x + 7, top * 0.55f, 6), new V3(x + 6.4f, top * 0.55f + 4, 3.5f), new V3(x + 0.6f, top * 0.55f + 4, 3.5f));
            }
            // bell tower
            float tx = nw + 4, tw = w * 0.2f;
            m.Col = stone;
            m.Box(tx, -3, -d + 6, tx + tw, TH, -d * 0.45f);
            for (int i = 0; i < 4; i++) m.Box(tx + i * tw / 3.5f, TH, -d + 6, tx + i * tw / 3.5f + tw / 7, TH + 9, -d * 0.45f);
            m.Col = Mul(stone, 0.85f);
            m.Box(tx - 1, TH - 5, -d + 5, tx + tw + 1, TH, -d * 0.45f + 1);
            m.Layer = 0; m.Col = 0x0E0C14;
            m.Box(tx + tw * 0.3f, 160, -d * 0.45f, tx + tw * 0.7f, 194, -d * 0.45f + 0.4f);
            for (int i = 0; i < 8; i++)
            {
                float a0 = i * M.PI / 8, a1 = (i + 1) * M.PI / 8, bx = tx + tw * 0.5f, br = tw * 0.2f, bz = -d * 0.45f + 0.4f;
                m.Tri(new V3(bx, 194, bz), new V3(bx + M.Cos(a0) * br, 194 + M.Sin(a0) * br * 1.3f, bz), new V3(bx + M.Cos(a1) * br, 194 + M.Sin(a1) * br * 1.3f, bz));
            }
            // portal: a deep pointed doorway, open and dark
            m.Layer = Mat3.Brick; m.Col = Mul(stone, 0.9f);
            for (int k = 0; k < 3; k++)
            {
                float hw = 21 - k * 3, ph = 46 - k * 2;
                m.Box(-hw - 3, -3, 0.6f + (2 - k) * 1.6f, -hw, ph, 2.2f + (2 - k) * 1.6f);
                m.Box(hw, -3, 0.6f + (2 - k) * 1.6f, hw + 3, ph, 2.2f + (2 - k) * 1.6f);
                for (int i = 0; i < 6; i++)
                {
                    float a0 = M.PI - i * M.PI / 6, a1 = M.PI - (i + 1) * M.PI / 6;
                    V3 p0 = new V3(M.Cos(a0) * (hw + 1.5f), ph + M.Sin(a0) * (hw + 1.5f) * 1.25f, 0), p1 = new V3(M.Cos(a1) * (hw + 1.5f), ph + M.Sin(a1) * (hw + 1.5f) * 1.25f, 0);
                    m.T = Xf.Move((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2, 1.4f + (2 - k) * 1.6f) * Xf.RotZ(M.Atan2(p1.Y - p0.Y, p1.X - p0.X));
                    m.BoxC(0, 0, 0, (p1 - p0).Len + 1, 3, 1.6f);
                }
                m.T = Xf.Id;
            }
            m.Layer = 0; m.Col = 0x0E0C14;
            m.Box(-15, -3, 0, 15, 42, 0.6f);
            for (int i = 0; i < 10; i++)
            {
                float a0 = i * M.PI / 10, a1 = (i + 1) * M.PI / 10;
                m.Tri(new V3(0, 42, 0.6f), new V3(M.Cos(a0) * 15, 42 + M.Sin(a0) * 18.75f, 0.6f), new V3(M.Cos(a1) * 15, 42 + M.Sin(a1) * 18.75f, 0.6f));
            }
            // rose window
            float ry = 94;
            m.Layer = 0; m.Col = 0x8A70E0; m.Em = 200;
            m.T = Xf.Move(0, ry, 0.7f) * Xf.RotX(M.PI / 2);
            m.Cyl(0, 0, 0, 0.4f, 16, 16, 16, true, false, 0);
            m.T = Xf.Id; m.Em = 0;
            m.Layer = Mat3.Rock; m.Col = 0x3A3A48;
            for (int i = 0; i < 6; i++) { m.T = Xf.Move(0, ry, 1.2f) * Xf.RotZ(i * M.PI / 6); m.BoxC(0, 0, 0, 32, 1.4f, 0.6f); }
            m.T = Xf.Move(0, ry, 1.0f) * Xf.RotX(M.PI / 2);
            m.Cyl(0, 0, 0, 1.2f, 18, 18, 16, false, false, 0);
            m.T = Xf.Id;
            // tall lancet windows in the aisles, and pairs high in the nave
            m.Layer = 0; m.Col = 0x5A4AA0; m.Em = 120;
            foreach (float x in new[] { -w / 2 + 12, -w / 2 + 26, w / 2 - 31, w / 2 - 17 })
            {
                m.Box(x, 14, -1.9f, x + 6, 44, -1.5f);
                m.Cyl(x + 3, -1.7f, 44, 49, 3, 0, 6, false, false, 0);
            }
            foreach (float x in new[] { -nw * 0.62f, nw * 0.62f - 6 })
            {
                m.Box(x, 70, 0.6f, x + 6, 104, 0.9f);
                m.Tri(new V3(x, 104, 0.9f), new V3(x + 6, 104, 0.9f), new V3(x + 3, 110, 0.9f));
            }
            m.Em = 0;
            // ivy creeping up the aisle walls (never across the portal)
            m.Layer = Mat3.Leaf;
            for (int i = 0; i < 9; i++)
            {
                float x = -w / 2 + 10 + i * w / 9f;
                if (Math.Abs(x) < 30) continue;
                for (int k = 0; k < 4; k++)
                {
                    m.Col = k % 2 == 0 ? 0x3A6A3A : 0x2E5A34;
                    m.Blob(x + (Hr((uint)i, k) - 0.5f) * 7, 4 + k * (5 + (i % 3) * 3), 0.2f, 3.4f - k * 0.4f, 4.2f, 1.1f, 1, (uint)(i * 7 + k), 0.12f);
                }
            }
            // steps
            m.Layer = Mat3.Rock; m.Col = 0x6A6A80;
            m.Box(-26, -3, 0, 26, 1, 10); m.Box(-22, 1, 0, 22, 2.8f, 5);
        }

        static void Watchtower(MeshB m)
        {
            m.Occ = 1;
            m.Layer = Mat3.Brick; m.Col = 0x78809A;
            m.Box(-16, -3, -22, 16, 86, 0);
            float[] tops = { 92, 102, 86, 97, 84, 104, 90 };
            for (int i = 0; i < 7; i++) m.Box(-16 + i * 32f / 7, 86, -22, -16 + (i + 1) * 32f / 7, tops[i], -18.5f + (i % 2) * 15);
            m.Box(-16, 86, -3, 16, 92, 0);
            m.Layer = 0; m.Col = 0x0E0C14;
            m.Box(-1, -1, 0, 13, 32, 0.4f);
            for (int i = 0; i < 6; i++)
            {
                float a0 = i * M.PI / 6, a1 = (i + 1) * M.PI / 6;
                m.Tri(new V3(6, 32, 0.4f), new V3(6 + M.Cos(a0) * 7, 32 + M.Sin(a0) * 7, 0.4f), new V3(6 + M.Cos(a1) * 7, 32 + M.Sin(a1) * 7, 0.4f));
            }
            m.Box(-10, 52, 0, -5, 66, 0.4f);
            m.Layer = Mat3.Leaf; m.Col = 0x3A6A3A;
            m.Blob(-7, 14, 0.6f, 9, 18, 2, 0, 5, 0.3f); m.Blob(10, 62, 0.6f, 5, 12, 1.6f, 0, 6, 0.3f);
            m.Layer = Mat3.Rock; m.Col = 0x6A7088;
            for (int i = 0; i < 5; i++) m.Blob(-12 + i * 6, 1.5f, 4 + (i % 2) * 2, 2.6f, 2, 2.2f, 0, (uint)i, 0.3f);
        }

        static void Barrow(MeshB m, int state)
        {
            m.Occ = 1;
            float R = state == 3 ? 24 : 40;
            m.Layer = Mat3.Moss; m.Col = 0x4A6A4A;
            m.Blob(0, 0, -R * 0.5f, R, R * 0.62f, R * 0.7f, 1, 77, 0.06f);
            if (state == 3) return;
            m.Layer = Mat3.Brick; m.Col = 0x8088A0;
            m.Box(-11, -2, 0, -7, 26, 5); m.Box(7, -2, 0, 11, 26, 5); m.Box(-12, 24, -1, 12, 29, 5.5f);
            if (state == 2) { m.Layer = 0; m.Col = 0x08070C; m.Box(-7, -2, 1, 7, 24, 1.4f); }
            else
            {
                m.Layer = Mat3.Rune; m.Col = 0x7A8098;
                m.Box(-7, -2, 1, 7, 24, 3);
                if (state == 1) { m.Layer = 0; m.Col = 0x80D0FF; m.Em = 255; m.Box(-1, 4, 3, 1, 20, 3.4f); m.Box(-5, 14, 3, 5, 15.6f, 3.4f); m.Em = 0; }
            }
        }

        static void Statue(MeshB m, Sprite s)
        {
            m.Occ = 1;
            int c = Lift(Avg(s, 0.2f, 0.2f, 0.8f, 0.9f, 0x7A8098));
            m.Layer = Mat3.Rock; m.Col = Mul(c, 0.9f);
            m.Box(-8, -2, -7, 8, 6, 5);
            m.Col = c;
            m.Lathe(0, -1, new float[] { 6, 7, 20, 5, 30, 3.6f, 34, 2.6f }, 8, false, 0);
            m.Blob(0, 37, -1, 3.6f, 4, 3.6f, 0, 8, 0.05f);
            m.Cyl(0, -1, 36, 42, 4.2f, 0.6f, 6, false, false, 0);
            m.T = Xf.Move(4, 28, 1) * Xf.RotZ(0.8f);
            m.Cyl(0, 0, -10, 0, 1.2f, 1.6f, 5, false, false, 0);
            m.T = Xf.Id;
            m.Layer = Mat3.Moss; m.Col = 0x4A6A4A;
            m.Blob(-3, 8, 2, 4, 3, 2, 0, 3, 0.3f);
        }

        static void ArchModel(MeshB m, Sprite s)
        {
            m.Occ = 1;
            m.Layer = Mat3.Brick; m.Col = 0x7A8098;
            float w = Math.Max(30, s.W * 0.9f), h = Math.Max(30, s.H * 0.75f);
            m.Box(-w / 2, -2, -5, -w / 2 + 7, h, 2); m.Box(w / 2 - 7, -2, -5, w / 2, h, 2);
            int segs = 7;
            for (int i = 0; i < segs; i++)
            {
                float a0 = M.PI - i * M.PI / segs, a1 = M.PI - (i + 1) * M.PI / segs;
                float r0 = w / 2 - 3.5f;
                V3 p0 = new V3(M.Cos(a0) * r0, h + M.Sin(a0) * r0 * 0.7f, 0), p1 = new V3(M.Cos(a1) * r0, h + M.Sin(a1) * r0 * 0.7f, 0);
                m.T = Xf.Move((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2, -1.5f) * Xf.RotZ(M.Atan2(p1.Y - p0.Y, p1.X - p0.X));
                m.BoxC(0, 0, 0, (p1 - p0).Len + 1, 6, 7);
            }
            m.T = Xf.Id;
            m.Layer = Mat3.Leaf; m.Col = 0x3A6A3A;
            m.Blob(w / 2 - 4, 10, 2.5f, 3, 8, 1.4f, 0, 4, 0.3f);
        }

        static void Palisade(MeshB m, int colW, int colH, uint seed)
        {
            m.Occ = 1;
            bool vert = colH > colW;
            int n = vert ? 3 : 4;
            float span = vert ? 16 : 16;
            for (int i = 0; i < n; i++)
            {
                float o = -span / 2 + (i + 0.5f) * span / n;
                float h = 40 + Hr(seed, i) * 7, r = 2.2f + Hr(seed, 10 + i) * 0.35f;
                float x = vert ? 0 : o, z = vert ? o - 4 : -3;
                m.Layer = Mat3.Bark; m.Col = 0x7A5234 + ((i * 0x050302) & 0x0F0F0F);
                m.Cyl(x, z, -2, h, r, r * 0.95f, 7, false, false, i);
                m.Layer = Mat3.Plank; m.Col = 0xA07850;
                m.Cyl(x, z, h, h + 6, r * 0.95f, 0, 7, false, false, i);
            }
            m.Layer = Mat3.Timber; m.Col = 0x5A3A26;
            if (vert) { m.Box(1.6f, 12, -12, 2.8f, 14.5f, 4); m.Box(1.6f, 31, -12, 2.8f, 33.5f, 4); }
            else { m.Box(-8, 12, -0.8f, 8, 14.5f, 0.4f); m.Box(-8, 31, -0.8f, 8, 33.5f, 0.4f); }
        }

        static void RuinWall(MeshB m, Sprite s, int colW, uint seed)
        {
            m.Occ = 1;
            float w = Math.Max(16, colW);
            int cols = Math.Max(2, (int)(w / 6));
            for (int i = 0; i < cols; i++)
            {
                float x0 = -w / 2 + i * w / cols, x1 = x0 + w / cols;
                float h = 10 + Hr(seed, i) * 16 * (i == 0 || i == cols - 1 ? 0.6f : 1f);
                m.Layer = Mat3.Brick; m.Col = Mix(0x7A8098, 0x6A7088, Hr(seed, 30 + i));
                m.Box(x0, -2, -7, x1, h, 0);
                if (Hr(seed, 50 + i) > 0.6f) { m.Layer = Mat3.Rock; m.Blob(x0 + 2, 1, 3, 2.4f, 1.8f, 2, 0, seed + (uint)i, 0.3f); }
            }
            m.Layer = Mat3.Leaf; m.Col = 0x3A6A3A;
            m.Blob(-w * 0.2f, 6, 0.6f, w * 0.12f + 2, 6, 1.6f, 0, seed, 0.3f);
        }

        static void Pillar(MeshB m, Sprite s, bool broken, uint seed, float ceil)
        {
            m.Occ = 1;
            int c = Lift(Avg(s, 0.2f, 0.2f, 0.8f, 0.9f, 0x7A8098));
            float h = broken ? 10 + Hr(seed, 0) * 14 : (ceil > 0 ? ceil - 3.5f : Math.Max(36, s.H * 0.95f));
            m.Layer = Mat3.Brick; m.Col = Mul(c, 0.9f);
            m.Box(-5, -2, -7, 5, 3, 3);
            m.Layer = Mat3.Rock; m.Col = c;
            m.Cyl(0, -2, 3, h, 3.6f, 3.3f, 8, !broken, false, 0);
            if (!broken) { m.Layer = Mat3.Brick; m.Col = Mul(c, 0.92f); m.Box(-5, h, -7, 5, h + 3.5f, 3); }
            else
            {
                m.Layer = Mat3.Rock;
                m.Blob(0, h, -2, 3.4f, 2.2f, 3.4f, 0, seed, 0.4f);
                m.Blob(6, 1.4f, 2, 3, 2, 2.4f, 0, seed + 1, 0.3f);
            }
        }

        static void Grave(MeshB m, Sprite s, uint seed)
        {
            int c = Lift(Avg(s, 0.2f, 0.2f, 0.8f, 0.9f, 0x7A8098));
            m.Layer = Mat3.Rock; m.Col = c;
            int type = (int)(seed % 3);
            if (s.H < 12) type = 2;
            if (type == 0)
            {
                m.Box(-4, -1, -3, 4, 9, -0.5f);
                m.T = Xf.Move(0, 9, -1.75f) * Xf.RotX(M.PI / 2);
                m.Cyl(0, 0, -1.25f, 1.25f, 4, 4, 8, true, true, 0);
                m.T = Xf.Id;
            }
            else if (type == 1)
            {
                m.Box(-1.2f, -1, -2.5f, 1.2f, 14, -0.5f); m.Box(-4.5f, 8, -2.5f, 4.5f, 10.4f, -0.5f);
            }
            else m.Box(-4, -1, -3, 4, 5, 0);
            m.Layer = Mat3.Moss; m.Col = 0x4A5A3A;
            m.Box(-5, -0.5f, 0, 5, 0.6f, 9);
        }

        static void Rubble(MeshB m, uint seed)
        {
            m.Layer = Mat3.Rock;
            for (int i = 0; i < 5; i++)
            {
                m.Col = Mix(0x6A7088, 0x5A6070, Hr(seed, i));
                m.Blob((Hr(seed, 10 + i) - 0.5f) * 14, 1.2f, (Hr(seed, 20 + i) - 0.5f) * 6, 2 + Hr(seed, 30 + i) * 2, 1.6f + Hr(seed, 40 + i), 2 + Hr(seed, 50 + i), 0, seed + (uint)i, 0.3f);
            }
        }

        static void StandingStone(MeshB m, Sprite s, int glow, uint seed)
        {
            m.Occ = 1;
            int c = Lift(Avg(s, 0.2f, 0.2f, 0.8f, 0.9f, 0x6A7088));
            float h = Math.Max(16, s.H * 0.92f), w = Math.Max(5, s.W * 0.3f);
            m.Layer = glow > 0 ? Mat3.Rune : Mat3.Rock; m.Col = c;
            m.Lathe(0, -1.5f, new float[] { -2, w, h * 0.5f, w * 0.95f, h * 0.85f, w * 0.75f, h, w * 0.3f }, 6, true, Hr(seed, 1));
            if (glow == 2)
            {
                int gc = 0x80D0FF;
                for (int i = 0; i < s.Px.Length; i++) { int q = s.Px[i]; if (((q >> 24) & 255) == 254) { gc = q & 0xFFFFFF; break; } }
                m.Layer = 0; m.Col = gc; m.Em = 255;
                m.Box(-0.8f, h * 0.25f, w * 0.85f - 1.5f, 0.8f, h * 0.7f, w * 0.85f + 0.2f);
                m.Box(-w * 0.45f, h * 0.48f, w * 0.85f - 1.5f, w * 0.45f, h * 0.48f + 1.4f, w * 0.85f + 0.2f);
                m.Em = 0;
            }
        }

        // houses
        // Built to the hero's size (he stands 33 tall, eyes at 27): doors a head taller than him, windows at eye level,
        // eaves well overhead. The ridge runs along the front; the gable ends face east and west.
        static void House(MeshB m, Sprite s, int colW, int colH, bool smith)
        {
            float w = Math.Max(36, colW), d = Math.Max(24, colH);
            int roof = Lift(Avg(s, 0.25f, 0.12f, 0.75f, 0.35f, 0x4A5068));
            int wall = Lift(Avg(s, 0.15f, 0.78f, 0.85f, 0.95f, 0xD0C0A0));
            bool stone = smith || ((wall >> 16) & 255) < 150;
            bool upper = !smith && w >= 80;                       // the elder's hall and the inn have an upper storey
            bool inn = s == Assets.HouseInn;
            float wh = upper ? 68 : 48, ridge = wh + d * 0.5f + 8, floor2 = 40;
            m.Occ = 1;
            // walls, with the gable ends closed up under the roof
            m.Layer = stone ? Mat3.Brick : Mat3.Plaster; m.Col = wall;
            m.BoxOpen(-w / 2, -4, -d, w / 2, wh, 0);
            m.Tri(new V3(-w / 2, wh, -d), new V3(-w / 2, wh, 0), new V3(-w / 2, ridge - 2, -d / 2));
            m.Tri(new V3(w / 2, wh, 0), new V3(w / 2, wh, -d), new V3(w / 2, ridge - 2, -d / 2));
            // stone footing
            m.Layer = Mat3.Brick; m.Col = 0x6A6A7A;
            m.Box(-w / 2 - 0.8f, -4, -d - 0.8f, w / 2 + 0.8f, 4, 0.8f);
            // the four faces: front, back, east and west, each in its own frame (+z out of the wall)
            Xf front = Xf.Id, back = Xf.Move(0, 0, -d) * Xf.Yaw(M.PI), east = Xf.Move(w / 2, 0, -d / 2) * Xf.Yaw(-M.PI / 2), west = Xf.Move(-w / 2, 0, -d / 2) * Xf.Yaw(M.PI / 2);
            if (!stone)
            {
                int bays = Math.Max(3, (int)Math.Round(w / 15f)), sideBays = Math.Max(2, (int)Math.Round(d / 15f));
                float top1 = upper ? floor2 : wh;
                Frame(m, front, w / 2, 4, top1, bays, true); Frame(m, back, w / 2, 4, top1, bays, true);
                Frame(m, east, d / 2, 4, top1, sideBays, true); Frame(m, west, d / 2, 4, top1, sideBays, true);
                if (upper)
                {
                    Frame(m, front, w / 2, floor2, wh, bays, false); Frame(m, back, w / 2, floor2, wh, bays, false);
                    Frame(m, east, d / 2, floor2, wh, sideBays, false); Frame(m, west, d / 2, floor2, wh, sideBays, false);
                }
                // gable timbers
                m.Layer = Mat3.Timber; m.Col = 0x4A3226;
                foreach (Xf f in new[] { east, west })
                {
                    m.T = f; m.Box(-0.9f, wh, -0.2f, 0.9f, ridge - 3, 0.7f);
                    m.T = f * Xf.Move(0, wh, 0); m.Box(-d / 2, 0, -0.2f, d / 2, 1.8f, 0.7f);
                }
                m.T = Xf.Id;
            }
            else
            {
                // dressed corner stones
                m.Layer = Mat3.Rock; m.Col = Mul(wall, 1.12f);
                for (int i = 0; i * 7 + 4 < wh; i++)
                {
                    float y = 4 + i * 7, a = i % 2 == 0 ? 7 : 4.5f, b = i % 2 == 0 ? 4.5f : 7;
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        m.Box(sx > 0 ? w / 2 - a : -w / 2 - 0.6f, y, -0.4f, sx > 0 ? w / 2 + 0.6f : -w / 2 + a, y + 6.4f, 0.6f);
                        m.Box(sx > 0 ? w / 2 - 0.4f : -w / 2 - 0.6f, y, -b, sx > 0 ? w / 2 + 0.6f : -w / 2 + 0.4f, y + 6.4f, 0.6f);
                    }
                }
            }
            if (upper)
            {
                // the jettied floor between the storeys
                m.Layer = Mat3.Timber; m.Col = 0x3A2820;
                m.Box(-w / 2 - 1.2f, floor2 - 1, -0.4f, w / 2 + 1.2f, floor2 + 2.2f, 2.4f);
                for (float x = -w / 2 + 4; x < w / 2 - 2; x += 9) m.Box(x - 1, floor2 - 3, 0, x + 1, floor2 - 1, 2.2f);
            }
            // roof
            m.Layer = (byte)(((roof >> 16) & 255) > ((roof & 255) + 30) ? Mat3.Tile : Mat3.Slate); m.Col = roof;
            float ov = 6;
            V3 eL = new V3(-w / 2 - ov, wh - 3, ov + 1), eR = new V3(w / 2 + ov, wh - 3, ov + 1), rL = new V3(-w / 2 - ov, ridge, -d / 2), rR = new V3(w / 2 + ov, ridge, -d / 2);
            V3 bL = new V3(-w / 2 - ov, wh - 3, -d - ov - 1), bR = new V3(w / 2 + ov, wh - 3, -d - ov - 1);
            m.Quad(eL, eR, rR, rL);
            m.Quad(bR, bL, rL, rR);
            V3 down = new V3(0, -1.6f, 0);
            m.Col = Mul(roof, 0.7f);
            m.Quad(eL + down, eR + down, eR, eL);
            m.Quad(bR + down, bR, bL, bL + down);
            m.Quad(eL + down, rL + down, rR + down, eR + down);
            m.Quad(bR + down, rR + down, rL + down, bL + down);
            m.Quad(rL + down, eL + down, eL, rL); m.Quad(bL + down, rL + down, rL, bL);
            m.Quad(eR + down, rR + down, rR, eR); m.Quad(rR + down, bR + down, bR, rR);
            m.Col = Mul(roof, 0.85f);
            m.Box(-w / 2 - ov - 0.5f, ridge - 1, -d / 2 - 1.4f, w / 2 + ov + 0.5f, ridge + 1.4f, -d / 2 + 1.4f);
            // barge boards along the gable edges
            m.Layer = Mat3.Timber; m.Col = 0x3A2820;
            foreach (float x in new[] { -w / 2 - ov, w / 2 + ov })
            {
                V3 a0 = new V3(x, wh - 3, ov + 1), a1 = new V3(x, ridge, -d / 2), a2 = new V3(x, wh - 3, -d - ov - 1);
                foreach (V3[] e in new[] { new[] { a0, a1 }, new[] { a1, a2 } })
                {
                    V3 mid = (e[0] + e[1]) * 0.5f, dv = e[1] - e[0];
                    m.T = Xf.Move(mid) * Xf.Rot(new V3(1, 0, 0), M.Atan2(-dv.Y, dv.Z));
                    m.BoxC(0, 0, 0, 1.4f, 2.6f, dv.Len + 1);
                }
            }
            m.T = Xf.Id;
            // chimney
            m.Layer = Mat3.Brick; m.Col = 0x6A6A7A;
            float cx = w * 0.28f;
            m.Box(cx - 3.5f, ridge - 14, -d * 0.62f, cx + 3.5f, ridge + 10, -d * 0.62f + 7);
            m.Col = 0x2A2A30; m.Box(cx - 4, ridge + 10, -d * 0.62f - 0.5f, cx + 4, ridge + 11.8f, -d * 0.62f + 7.5f);
            // door: a head taller than the hero, framed, with iron straps and a ring
            float doorX = smith ? -w * 0.28f : (w < 60 ? -w * 0.22f : -w * 0.12f), dw = 8.5f, dh = 36;
            m.Layer = Mat3.Plank; m.Col = 0x6A4430;
            m.Box(doorX - dw, -1, -0.4f, doorX + dw, dh, 1.0f);
            m.Layer = Mat3.Timber; m.Col = 0x3A2820;
            m.Box(doorX - dw - 2, -1, -0.2f, doorX - dw, dh + 1, 1.8f); m.Box(doorX + dw, -1, -0.2f, doorX + dw + 2, dh + 1, 1.8f);
            m.Box(doorX - dw - 3, dh, -0.2f, doorX + dw + 3, dh + 3, 2.4f);
            m.Layer = Mat3.Metal; m.Col = 0x34323C;
            m.Box(doorX - dw + 0.6f, 7, 1.0f, doorX + dw - 0.6f, 8.6f, 1.3f); m.Box(doorX - dw + 0.6f, dh - 9, 1.0f, doorX + dw - 0.6f, dh - 7.4f, 1.3f);
            m.Col = 0xC8A050; m.BoxC(doorX + dw - 3.2f, 17, 1.5f, 1.4f, 1.4f, 1);
            m.Layer = Mat3.Rock; m.Col = 0x7A7A88;
            m.Box(doorX - dw - 3, -3, 0, doorX + dw + 3, 1.4f, 6);
            // windows at eye height, lit from inside; shuttered where there is room for the shutters
            float hwWin = w < 60 ? 4.5f : 5.5f, full = hwWin * 4 + 3.4f, bare = hwWin * 2 + 2.4f;
            float xr = doorX + dw + 3, limit = smith ? w * 0.05f - 3 : w / 2 - 1.5f;
            while (true)
            {
                if (xr + full <= limit) { Window(m, front, xr + full / 2, 15, 31, hwWin, true, true); xr += full + 4; }
                else if (xr + bare <= limit) { Window(m, front, xr + bare / 2, 15, 31, hwWin, true, false); xr += bare + 4; }
                else break;
            }
            if (!smith)
            {
                float xl = doorX - dw - 3;
                if (xl - full >= -w / 2 + 1.5f) Window(m, front, xl - full / 2, 15, 31, hwWin, true, true);
                else if (xl - bare >= -w / 2 + 1.5f) Window(m, front, xl - bare / 2, 15, 31, hwWin, true, false);
            }
            if (upper)
                for (int i = -1; i <= 1; i++) Window(m, front, i * w / 3f, floor2 + 8, floor2 + 21, 4.5f, false, true);
            if (d >= 40) { Window(m, east, 0, 15, 29, 4.5f, true, true); Window(m, west, 0, 15, 29, 4.5f, false, true); }
            if (inn)
            {
                // the inn's sign hangs from an iron bracket over the street, clear of a walker's head
                float sx = doorX + dw + 6;
                m.Layer = Mat3.Metal; m.Col = 0x2E2C34;
                m.Box(sx - 0.7f, 51, 0, sx + 0.7f, 52.4f, 16);
                m.T = Xf.Move(sx, 45, 0) * Xf.RotX(0.85f); m.Box(-0.5f, 0, -0.5f, 0.5f, 8, 0.5f); m.T = Xf.Id;
                m.Box(sx - 0.3f, 46.6f, 6.2f, sx + 0.3f, 51, 6.8f); m.Box(sx - 0.3f, 46.6f, 13.2f, sx + 0.3f, 51, 13.8f);
                m.Layer = Mat3.Plank; m.Col = 0x7A5234;
                m.Box(sx - 0.8f, 36, 5, sx + 0.8f, 47, 15);
                m.Layer = 0; m.Col = 0xD8A040; m.Em = 40;
                m.Box(sx + 0.8f, 39, 7.8f, sx + 1.1f, 44, 11.6f); m.Box(sx - 1.1f, 39, 7.8f, sx - 0.8f, 44, 11.6f);
                m.Col = 0xEDE4CC; m.Box(sx + 0.8f, 44, 7.8f, sx + 1.15f, 45.2f, 11.6f); m.Box(sx - 1.15f, 44, 7.8f, sx - 0.8f, 45.2f, 11.6f);
                m.Em = 0;
            }
            if (smith)
            {
                // the forge: a wide opening glowing from within, under a plank awning
                m.Layer = 0;
                m.Col = 0x1A1210; m.Box(w * 0.05f, -1, -0.3f, w * 0.38f, 26, 0.5f);
                m.Col = 0xFF8A30; m.Em = 255; m.Box(w * 0.09f, 0, 0.5f, w * 0.34f, 9, 0.8f);
                m.Col = 0x8A3A18; m.Em = 120; m.Box(w * 0.09f, 9, 0.5f, w * 0.34f, 18, 0.8f); m.Em = 0;
                m.Layer = Mat3.Timber; m.Col = 0x3A2820;
                m.Box(w * 0.05f - 2, -1, -0.2f, w * 0.05f, 28, 1.6f); m.Box(w * 0.38f, -1, -0.2f, w * 0.38f + 2, 28, 1.6f);
                m.Box(w * 0.05f - 3, 26, -0.2f, w * 0.38f + 3, 29, 2.2f);
                m.Layer = Mat3.Plank; m.Col = 0x5A4030;
                m.T = Xf.Move(0, 40, 0) * Xf.RotX(0.22f); m.Box(-w / 2, -0.6f, 0, w / 2, 0.6f, 11); m.T = Xf.Id;
                m.Layer = Mat3.Timber; m.Col = 0x3A2820;
                foreach (float x in new[] { -w / 2 + 3, 0f, w / 2 - 3 }) { m.T = Xf.Move(x, 30, 0) * Xf.RotX(0.8f); m.Box(-0.8f, 0, -0.8f, 0.8f, 12, 0.8f); }
                m.T = Xf.Id;
            }
        }

        /// <summary>Half-timbering on one face of a house: sill, top plate, posts and (lower storey) braces.</summary>
        static void Frame(MeshB m, Xf face, float hw, float y0, float y1, int bays, bool braces)
        {
            m.Layer = Mat3.Timber; m.Col = 0x4A3226;
            m.T = face;
            m.Box(-hw - 0.4f, y1 - 2, -0.2f, hw + 0.4f, y1, 0.7f);
            m.Box(-hw - 0.4f, y0, -0.2f, hw + 0.4f, y0 + 1.8f, 0.7f);
            float bw = hw * 2 / bays;
            for (int i = 0; i <= bays; i++) { float x = -hw + i * bw; m.Box(x - 1, y0, -0.2f, x + 1, y1, 0.7f); }
            if (braces)
            {
                float rail = y0 + (y1 - y0) * 0.5f;
                m.Box(-hw, rail - 0.8f, -0.2f, hw, rail + 0.8f, 0.6f);
                for (int i = 0; i < bays; i++)
                {
                    float x0 = -hw + i * bw + 1, x1 = x0 + bw - 2;
                    V3 a = new V3(i % 2 == 0 ? x0 : x1, y0 + 1.8f, 0.2f), b = new V3(i % 2 == 0 ? x1 : x0, rail, 0.2f);
                    V3 dv = b - a;
                    m.T = face * Xf.Move((a + b) * 0.5f) * Xf.RotZ(M.Atan2(dv.Y, dv.X));
                    m.BoxC(0, 0, 0, dv.Len, 1.6f, 0.8f);
                }
            }
            m.T = Xf.Id;
        }

        /// <summary>A shuttered window on a face of a house, glowing when lit from inside.</summary>
        static void Window(MeshB m, Xf face, float x, float y0, float y1, float hw, bool lit, bool shutters)
        {
            m.T = face;
            m.Layer = 0; m.Col = 0x2A1C14;
            m.Box(x - hw - 1.2f, y0 - 1.2f, -0.2f, x + hw + 1.2f, y1 + 1.2f, 0.8f);
            m.Col = lit ? 0xFFC060 : 0x2A2C3A; m.Em = (byte)(lit ? 220 : 0);
            m.Box(x - hw, y0, 0.8f, x + hw, y1, 1.0f);
            m.Em = 0; m.Col = 0x2A1C14;
            m.Box(x - 0.5f, y0, 1.0f, x + 0.5f, y1, 1.4f);
            m.Box(x - hw, (y0 + y1) * 0.5f - 0.5f, 1.0f, x + hw, (y0 + y1) * 0.5f + 0.5f, 1.4f);
            m.Layer = Mat3.Plank; m.Col = 0x5A3A26;
            if (shutters)
            {
                m.Box(x - hw * 2 - 1.4f, y0 - 0.6f, 0.4f, x - hw - 1.4f, y1 + 0.6f, 1.2f);
                m.Box(x + hw + 1.4f, y0 - 0.6f, 0.4f, x + hw * 2 + 1.4f, y1 + 0.6f, 1.2f);
            }
            m.Layer = Mat3.Timber; m.Col = 0x3A2820;
            m.Box(x - hw - 2, y0 - 2.6f, -0.2f, x + hw + 2, y0 - 1.2f, 2.6f);
            m.T = Xf.Id;
        }
    }
}
