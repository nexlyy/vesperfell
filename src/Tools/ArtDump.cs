using System;
using System.Collections.Generic;
using System.IO;

namespace Vesperfell
{
    /// <summary>Lays sprites out on a contact sheet so the art can be reviewed outside the game.</summary>
    public sealed class Sheet
    {
        readonly List<Sprite> items = new List<Sprite>();
        readonly List<string> labels = new List<string>();
        readonly int cellW, cellH, cols, bg;

        public Sheet(int cellW, int cellH, int cols, int bg) { this.cellW = cellW; this.cellH = cellH; this.cols = cols; this.bg = bg; }
        public void Add(Sprite s) { items.Add(s); labels.Add(null); }
        public void Add(Sprite s, string label) { items.Add(s); labels.Add(label); }
        public void AddAll(Sprite[] arr) { if (arr != null) foreach (Sprite s in arr) Add(s); }

        public void Save(string path, int scale)
        {
            int rows = (items.Count + cols - 1) / cols;
            if (rows < 1) rows = 1;
            Surface sf = new Surface(cols * cellW, rows * cellH);
            sf.Clear(bg);
            for (int i = 0; i < items.Count; i++)
            {
                int cx = (i % cols) * cellW, cy = (i / cols) * cellH;
                if (((i % cols) + (i / cols)) % 2 == 1) sf.FillA(cx, cy, cellW, cellH, 0xFFFFFF, 10);
                Sprite s = items[i];
                if (s == null) continue;
                // centre the sprite's bitmap in the cell
                sf.Blit(s, cx + (cellW - s.W) / 2 + s.OX, cy + (cellH - s.H) / 2 + s.OY);
                if (labels[i] != null && Fonts.Tiny != null && Fonts.Small != null) sf.Text(Fonts.Small, labels[i], cx + 1, cy, 0xC8BE9F);
            }
            sf.SavePng(path, scale);
        }
    }

    public static class ArtDump
    {
        public static void Run(string dir)
        {
            Directory.CreateDirectory(dir);
            Fonts.Build();

            Surface s = new Surface(640, 200);
            s.Clear(0x15121F);
            int y = 4;
            PixFont[] fs = { Fonts.Small, Fonts.Body, Fonts.BodyBold, Fonts.Title, Fonts.Big, Fonts.Huge };
            string[] names = { "Small", "Body", "BodyBold", "Title", "Big", "Huge" };
            for (int i = 0; i < fs.Length; i++)
            {
                string sample = i < 4 ? "The Vesper Bell fell silent. 0123456789 +15% Crit - [F] Talk (" + names[i] + " / " + fs[i].SourceName + ")" : "Vesperfell 123";
                s.Text(fs[i], sample, 4, y, 0xE8E0C4, 0x000000);
                y += fs[i].LineH + 3;
            }
            s.Text(Fonts.Small, "^yGold^0 ^gFine^0 ^bRare^0 ^oRelic^0 ^rDanger^0 ^dDim^0 ^pArcane^0 ^cFrost^0 normal", 4, y, 0xE8E0C4, 0); y += 14;
            s.Text(Fonts.Tiny, "0123456789 x12 +5 3.5s 50%", 4, y, 0xFFFFFF, 0);
            s.SavePng(Path.Combine(dir, "fonts.png"), 2);

            Sheet sh = new Sheet(26, 34, 6, 0x33543F);
            CharacterArt.HumanDef[] defs = { CharacterArt.Warrior(), CharacterArt.Mage(), CharacterArt.Rogue(), CharacterArt.Ranger() };
            foreach (CharacterArt.HumanDef d in defs)
            {
                ActorSprites a = CharacterArt.Build(d);
                sh.AddAll(a.Idle); sh.AddAll(a.Run);
            }
            sh.Save(Path.Combine(dir, "chars.png"), 6);

            SceneTest(dir);
            StructureSheets(dir);
            CreatureSheets(dir);
        }

        static void CreatureSheets(string dir)
        {
            Sheet sh = new Sheet(44, 50, 12, 0x33543F);
            ActorSprites[] all =
            {
                CreatureArt.Wolf(false), CreatureArt.Wolf(true), CreatureArt.Crawler(), CreatureArt.Wisp(),
                CreatureArt.Hollow(0), CreatureArt.Hollow(1), CreatureArt.Skeleton(), CreatureArt.Acolyte(),
                CreatureArt.BarrowKnight(), CreatureArt.Prior(),
                CreatureArt.Elder(), CreatureArt.Smith(), CreatureArt.Herbalist(), CreatureArt.Innkeeper(), CreatureArt.Warden(),
                CreatureArt.VillagerA(), CreatureArt.VillagerB(), CreatureArt.Child(), CreatureArt.Ghost(),
            };
            foreach (ActorSprites a in all) { sh.AddAll(a.Idle); sh.AddAll(a.Run); }
            sh.Save(Path.Combine(dir, "creatures.png"), 4);

            Sheet it = new Sheet(40, 30, 12, 0x2A2E43);
            for (int t = 0; t < 4; t++) { it.Add(ItemArt.Sword(t)); it.Add(ItemArt.Axe(t)); it.Add(ItemArt.Staff(t)); it.Add(ItemArt.Wand(t)); it.Add(ItemArt.Dagger(t, false)); it.Add(ItemArt.Dagger(t, true)); it.Add(ItemArt.Bow(t, true)); it.Add(ItemArt.Bow(t, false)); }
            it.Add(ItemArt.Shield()); it.Add(ItemArt.Cleaver()); it.Add(ItemArt.Greatsword()); it.Add(ItemArt.Crozier()); it.Add(ItemArt.CandleStaff());
            it.Add(ItemArt.Arrow(false)); it.Add(ItemArt.Arrow(true)); it.Add(ItemArt.Knife());
            it.Add(ItemArt.Bolt(ItemArt.RampEmber, 9, 5)); it.Add(ItemArt.Bolt(ItemArt.RampGloam, 11, 5)); it.Add(ItemArt.Bolt(ItemArt.RampFrost, 9, 5));
            it.Add(ItemArt.Orb(ItemArt.RampEmber, 5)); it.Add(ItemArt.Orb(ItemArt.RampGloam, 4)); it.Add(ItemArt.Orb(ItemArt.RampVenom, 3));
            it.AddAll(ItemArt.Coin());
            it.AddAll(ItemArt.Slash(14, 2.4f, Pal.Bn4, Pal.St5));
            it.AddAll(ItemArt.Spark(Pal.Em5));
            it.Add(ItemArt.Cursor(false)); it.Add(ItemArt.Cursor(true));
            it.Save(Path.Combine(dir, "items.png"), 4);

            Sheet ic = new Sheet(22, 22, 23, 0x2A2E43);
            for (int t = 0; t < 4; t++) for (int i = 0; i < ItemArt.IconCount; i++) ic.Add(ItemArt.Icon(i, t));
            for (int i = 0; i < ItemArt.SkCount; i++) ic.Add(ItemArt.Skill(i));
            ic.Save(Path.Combine(dir, "icons.png"), 4);
        }

        static void StructureSheets(string dir)
        {
            Rng r = new Rng(5);
            Sheet big = new Sheet(240, 204, 3, 0x2E4C39);
            big.Add(StructureArt.Abbey());
            big.Add(StructureArt.House(r, 64, 26, 34, StructureArt.RoofSlate, 0, 12, new[] { 34, 48 }, true, true, -1));
            big.Add(StructureArt.House(r, 88, 28, 40, StructureArt.RoofTile, 0, 38, new[] { 10, 22, 62, 74 }, true, true, -1));
            big.Add(StructureArt.House(r, 72, 26, 30, StructureArt.RoofSlate, 1, -1, new[] { 8 }, true, false, 30));
            big.Add(StructureArt.House(r, 50, 24, 30, StructureArt.RoofThatch, 0, 8, new[] { 30 }, false, true, -1));
            big.Add(StructureArt.Watchtower(r));
            big.Add(StructureArt.Barrow(r, false, false));
            big.Add(StructureArt.Barrow(r, true, false));
            big.Add(PropArt.Bell(0));
            big.Add(PropArt.Bell(1));
            big.Save(Path.Combine(dir, "structures.png"), 2);

            Sheet sm = new Sheet(48, 50, 12, 0x2E4C39);
            sm.Add(StructureArt.Tent(r, Pal.Bn1));
            sm.Add(StructureArt.RuinWall(r, 40, 26));
            sm.Add(StructureArt.RuinWall(r, 28, 20));
            sm.Add(StructureArt.Pillar(r, 34, false)); sm.Add(StructureArt.Pillar(r, 22, true));
            sm.Add(StructureArt.Arch(r));
            sm.Add(StructureArt.StandingStone(r, 30, 0, false)); sm.Add(StructureArt.StandingStone(r, 34, 1, true)); sm.Add(StructureArt.StandingStone(r, 26, 2, true));
            sm.Add(StructureArt.Gravestone(r, 0)); sm.Add(StructureArt.Gravestone(r, 1)); sm.Add(StructureArt.Gravestone(r, 2));
            sm.Add(StructureArt.Statue(r, false)); sm.Add(StructureArt.Statue(r, true));
            sm.AddAll(PropArt.Campfire(true)); sm.AddAll(PropArt.Campfire(false));
            sm.Add(PropArt.LanternPost(true)[0]); sm.Add(PropArt.LanternPost(false)[0]);
            sm.Add(PropArt.Brazier(true, 0)[0]); sm.Add(PropArt.Brazier(true, 1)[1]); sm.Add(PropArt.Brazier(false, 0)[0]);
            sm.Add(PropArt.WallTorch(0)[0]); sm.Add(PropArt.WallTorch(1)[2]); sm.Add(PropArt.CandleStand()[0]);
            sm.Add(PropArt.Shrine(true)[0]); sm.Add(PropArt.Shrine(false)[0]);
            sm.Add(PropArt.Chest(false, false)); sm.Add(PropArt.Chest(false, true)); sm.Add(PropArt.Chest(true, false)); sm.Add(PropArt.Chest(true, true));
            sm.Add(PropArt.Barrel()); sm.Add(PropArt.Crate()); sm.Add(PropArt.Urn(false)); sm.Add(PropArt.Urn(true)); sm.Add(PropArt.Sack());
            sm.Add(PropArt.Cart()); sm.Add(PropArt.HayBale()); sm.Add(PropArt.Woodpile()); sm.Add(PropArt.Anvil());
            sm.Add(PropArt.Signpost(1)); sm.Add(PropArt.NoticeBoard()); sm.Add(PropArt.FenceH()); sm.Add(PropArt.FenceV()); sm.Add(PropArt.Palisade(r));
            sm.Add(PropArt.Bench()); sm.Add(PropArt.Banner(Pal.Cr2)); sm.Add(PropArt.Well()); sm.Add(PropArt.Stall(Pal.Cr2, Pal.Bn2)); sm.Add(PropArt.Bedroll());
            sm.Add(PropArt.Sarcophagus(r, false)); sm.Add(PropArt.Sarcophagus(r, true)); sm.Add(PropArt.Pew()); sm.Add(PropArt.Bookshelf(r));
            sm.Add(PropArt.Altar()[0]); sm.Add(PropArt.Bones(r)); sm.Add(PropArt.Cobweb()); sm.Add(PropArt.Gate(false)); sm.Add(PropArt.Gate(true));
            sm.Add(PropArt.Lever(false)); sm.Add(PropArt.Lever(true)); sm.Add(PropArt.CrackedWall()); sm.Add(PropArt.Rubble(r)); sm.Add(PropArt.Stairs());
            sm.Add(PropArt.SkullPile(r)); sm.Add(PropArt.WallBanner()); sm.Add(PropArt.Coffin());
            sm.Save(Path.Combine(dir, "props.png"), 3);

            // dungeon bake preview
            int tw = 40, th = 23;
            byte[] terr = new byte[tw * th];
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                {
                    byte t = T.DWall;
                    if (x >= 3 && x <= 16 && y >= 4 && y <= 12) t = T.DFloor;
                    if (x >= 17 && x <= 22 && y >= 7 && y <= 9) t = T.DFloor;
                    if (x >= 23 && x <= 36 && y >= 3 && y <= 19) t = T.DFloor;
                    if (x >= 28 && x <= 31 && y >= 3 && y <= 19) t = T.DCarpet;
                    if (x >= 6 && x <= 9 && y >= 13 && y <= 19) t = T.DFloor;
                    if (x >= 24 && x <= 35 && y >= 14 && y <= 18 && !(x >= 28 && x <= 31)) t = T.DRitual;
                    if ((x == 8 || x == 12) && (y == 6 || y == 10)) t = T.DWall;
                    terr[y * tw + x] = t;
                }
            int[] g = TerrainArt.BakeDungeon(terr, tw, th, 99);
            Surface s = new Surface(640, 360);
            s.CopyFrom(g, tw * 16, th * 16, 0, 0);
            s.Blit(PropArt.Brazier(true, 0)[0], 80, 130); s.Blit(PropArt.Sarcophagus(r, false), 180, 150); s.Blit(PropArt.Bell(0), 480, 150);
            s.Blit(CharacterArt.Build(CharacterArt.Warrior()).Idle[0], 140, 150);
            s.Blit(PropArt.WallTorch(0)[0], 120, 60); s.Blit(PropArt.WallBanner(), 200, 62); s.Blit(PropArt.Gate(false), 320, 144);
            s.SavePng(Path.Combine(dir, "dungeon_unlit.png"), 2);
            Lighting lt = new Lighting();
            lt.AmbR = 0.2f; lt.AmbG = 0.2f; lt.AmbB = 0.32f; lt.FogDensity = 0.25f; lt.FogColor = 0x7060A0;
            lt.Begin();
            lt.Light(80, 110, 90, 0xFFA050, 1.2f); lt.Light(120, 50, 70, 0xFFA050, 1.0f); lt.Light(140, 140, 60, 0xFFD0A0, 0.6f); lt.Light(480, 120, 80, 0x9080FF, 0.7f);
            Surface fin = new Surface(640, 360);
            lt.Compose(s, fin, 0, 0);
            fin.SavePng(Path.Combine(dir, "dungeon_lit.png"), 2);
        }

        struct Item { public Sprite S; public int X, Y; public bool Flip; }

        static void SceneTest(string dir)
        {
            int tw = 44, th = 27;
            byte[] terr = new byte[tw * th];
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                {
                    byte t = T.Grass;
                    if (x < 15) t = T.Forest;
                    if (x < 3) t = T.Thicket;
                    float py = 13 + M.Sin(x * 0.35f) * 2.2f;
                    if (Math.Abs(y - py) < 1.2f) t = T.Dirt;
                    if (M.Dist(x, y, 33, 6) < 4.2f) t = T.Water;
                    if (x >= 22 && x <= 29 && y >= 17 && y <= 22) t = T.Cobble;
                    if (x >= 34 && y >= 17) t = T.PaleGrass;
                    if (x >= 38 && y >= 20) t = T.Ash;
                    if (x >= 36 && x <= 41 && y >= 22 && y <= 25) t = T.Stone;
                    terr[y * tw + x] = t;
                }
            byte[] pt;
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            int[] ground = TerrainArt.Bake(terr, tw, th, 16, 1234, out pt);
            long bakeMs = sw.ElapsedMilliseconds;

            Surface scene = new Surface(640, 360), fin = new Surface(640, 360);
            int camX = 24, camY = 36;
            scene.CopyFrom(ground, tw * 16, th * 16, camX, camY);

            Rng r = new Rng(77);
            List<Item> items = new List<Item>();
            for (int i = 0; i < 26; i++)
            {
                Item it = new Item();
                int kind = r.Int(10);
                if (kind < 5) it.S = NatureArt.Oak(r, r.Chance(0.15f) ? NatureArt.RampRust : NatureArt.RampTeal, r.F(0.85f, 1.1f));
                else if (kind < 8) it.S = NatureArt.Pine(r, NatureArt.RampTeal, r.F(0.85f, 1.1f));
                else it.S = NatureArt.DeadTree(r, Pal.Ea2, 1f);
                it.X = r.Range(10, 250); it.Y = r.Range(30, 400);
                if (Math.Abs(it.Y - (13 * 16 + 8) + camY - 36) < 26) continue;
                items.Add(it);
            }
            for (int i = 0; i < 5; i++) { Item it = new Item(); it.S = NatureArt.Oak(r, NatureArt.RampGreen, 1f); it.X = r.Range(300, 620); it.Y = r.Range(60, 380); if (Math.Abs(it.Y - 225) < 30) continue; items.Add(it); }
            for (int i = 0; i < 12; i++) { Item it = new Item(); it.S = NatureArt.Rock(r, r.Int(3), r.Chance(0.5f)); it.X = r.Range(10, 630); it.Y = r.Range(20, 350); items.Add(it); }
            for (int i = 0; i < 9; i++) { Item it = new Item(); it.S = NatureArt.Bush(r, NatureArt.RampTeal, r.Chance(0.4f)); it.X = r.Range(10, 630); it.Y = r.Range(20, 350); items.Add(it); }
            for (int i = 0; i < 3; i++) { Item it = new Item(); it.S = NatureArt.Bramble(r); it.X = 150 + i * 30; it.Y = 300; items.Add(it); }
            for (int i = 0; i < 70; i++) { Item it = new Item(); it.S = NatureArt.GrassTuft(r, Pal.Gr5, Pal.Gr4, r.Chance(0.4f)); it.X = r.Range(10, 630); it.Y = r.Range(20, 350); items.Add(it); }
            for (int i = 0; i < 14; i++) { Item it = new Item(); it.S = NatureArt.Flower(r, r.Chance(0.5f) ? Pal.Bn3 : Pal.Gl4, Pal.Em5); it.X = r.Range(260, 630); it.Y = r.Range(20, 350); items.Add(it); }
            for (int i = 0; i < 4; i++) { Item it = new Item(); it.S = NatureArt.Moonpetal(false); it.X = r.Range(40, 240); it.Y = r.Range(60, 330); items.Add(it); }
            for (int i = 0; i < 5; i++) { Item it = new Item(); it.S = NatureArt.Mushroom(r, i % 2 == 0); it.X = r.Range(20, 240); it.Y = r.Range(60, 330); items.Add(it); }
            { Item it = new Item(); it.S = NatureArt.Stump(r); it.X = 300; it.Y = 150; items.Add(it); }
            { Item it = new Item(); it.S = NatureArt.Log(r); it.X = 350; it.Y = 300; items.Add(it); }
            for (int i = 0; i < 4; i++) { Item it = new Item(); it.S = NatureArt.Reeds(r); it.X = 480 + i * 22; it.Y = 122 + (i % 2) * 6; items.Add(it); }

            CharacterArt.HumanDef[] defs = { CharacterArt.Warrior(), CharacterArt.Mage(), CharacterArt.Rogue(), CharacterArt.Ranger() };
            int[] cxs = { 300, 340, 250, 380 };
            int[] cys = { 215, 190, 200, 220 };
            for (int i = 0; i < 4; i++) { Item it = new Item(); it.S = CharacterArt.Build(defs[i]).Run[0]; it.X = cxs[i]; it.Y = cys[i]; it.Flip = i == 3; items.Add(it); }

            items.Sort(delegate(Item a, Item b) { return a.Y.CompareTo(b.Y); });
            foreach (Item it in items)
            {
                scene.Ellipse(it.X, it.Y, Math.Min(12, it.S.W / 3f), Math.Min(4, it.S.W / 8f), 0x05050A, 90);
                scene.Blit(it.S, it.X, it.Y, it.Flip);
            }
            Array.Copy(scene.Px, fin.Px, fin.Px.Length);
            fin.Text(Fonts.Small, "bake ms: " + bakeMs, 4, 4, 0xFFFFFF, 0);
            fin.SavePng(Path.Combine(dir, "scene_unlit.png"), 2);

            Lighting lt = new Lighting();
            lt.Time = 12.3f;
            lt.Begin();
            lt.Light(320, 200, 110, 0xFFB060, 1.1f);
            lt.Light(120, 250, 46, 0xB0C8FF, 0.7f);
            lt.Light(520, 120, 60, 0xFFA050, 0.8f);
            sw.Restart();
            for (int i = 0; i < 100; i++) lt.Compose(scene, fin, camX, camY);
            long compMs = sw.ElapsedMilliseconds;
            fin.Text(Fonts.Small, "compose x100 ms: " + compMs, 4, 4, 0xFFFFFF, 0);
            fin.SavePng(Path.Combine(dir, "scene_lit.png"), 2);
        }
    }
}
