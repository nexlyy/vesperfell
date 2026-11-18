using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>The Sunken Abbey: a hand-laid undercroft of nave, transepts, chancel and the Bell Crypt.</summary>
    public static class DungeonGen
    {
        public const int TW = 72, TH = 66, Seed = 9931;
        static byte[] tt;

        // arena bounds (pixels) used by the boss
        public const float ArenaX0 = 24.4f * 16, ArenaX1 = 47.6f * 16, ArenaY0 = 4.6f * 16, ArenaY1 = 14.2f * 16;

        static void Carve(int x, int y, int w, int h, byte t)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    if (i >= 0 && j >= 0 && i < TW && j < TH) tt[j * TW + i] = t;
        }

        public static Map BuildAbbey(Action<float, string> progress)
        {
            Map m = new Map("abbey", TW, TH, true);
            Rng r = new Rng(Seed);
            WorldGen.Begin(m, r);
            tt = new byte[TW * TH];
            for (int i = 0; i < tt.Length; i++) tt[i] = T.DWall;

            Carve(30, 55, 12, 7, T.DFloor);         // narthex
            Carve(31, 30, 10, 23, T.DFloor);        // nave
            Carve(35, 53, 2, 2, T.DFloor);          // nave door
            Carve(24, 39, 7, 3, T.DFloor);          // west transept
            Carve(9, 33, 15, 13, T.DFloor);         // scriptorium
            Carve(41, 39, 7, 3, T.DFloor);          // east transept
            Carve(48, 33, 15, 13, T.DFloor);        // ossuary
            Carve(24, 46, 5, 5, T.DFloor);          // west chapel
            Carve(29, 48, 2, 2, T.DFloor);
            Carve(43, 46, 5, 5, T.DFloor);          // east chapel
            Carve(41, 48, 2, 2, T.DFloor);
            Carve(31, 21, 10, 7, T.DFloor);         // chancel
            Carve(35, 28, 2, 2, T.DFloor);          // bellgate
            Carve(35, 15, 2, 6, T.DFloor);          // stair passage
            Carve(23, 3, 26, 12, T.DRitual);        // bell crypt
            Carve(51, 25, 8, 6, T.DFloor);          // sealed vault
            Carve(54, 31, 2, 2, T.DFloor);          // secret passage
            Carve(35, 15, 2, 40, T.DCarpet);        // the processional carpet

            for (int ty = 0; ty < TH; ty++)
                for (int tx = 0; tx < TW; tx++)
                {
                    byte t = tt[ty * TW + tx];
                    for (int k = 0; k < 4; k++)
                    {
                        int ci = (ty * 2 + (k >> 1)) * m.CW + tx * 2 + (k & 1);
                        m.Cell[ci] = t; m.Solid[ci] = Map.TerrainSolid(t);
                    }
                    m.ZoneMap[ty * TW + tx] = (byte)(ty <= 14 ? Zones.BellCrypt : Zones.Abbey);
                }
            if (progress != null) progress(0.9f, "Descending");
            m.Ground = TerrainArt.BakeDungeon(tt, TW, TH, Seed);

            const int Ember = WorldGen.EmberLight, Gloam = WorldGen.GloamLight;

            // narthex
            Prop stairs = WorldGen.Mk(Assets.Stairs, 32.5f, 55f, PropKind.Stairs, 0, 0, false);
            stairs.OnWall = true; WorldGen.Act(stairs, "abbey_exit", "Climb to the surface", 26);
            WorldGen.Glow(stairs, 50, WorldGen.MoonLight, 0.5f, -20, false);
            m.Mark("abbey_entry", 32.5f * 16, 56.6f * 16, 16);
            Torch(30.7f, 55f, false); Torch(38.4f, 55f, false); Torch(41.3f, 55f, false);
            Prop sh = WorldGen.MkAnim(Assets.ShrineOut, 39.4f, 57.6f, PropKind.Shrine, 20, 8, false);
            WorldGen.Act(sh, "shrine_undercroft", "Rest", 24).Text = "The Narthex";
            WorldGen.Breakable(Assets.UrnStone, 30.7f, 61.5f, 8); WorldGen.Breakable(Assets.UrnStone, 31.5f, 61.7f, 8); WorldGen.Breakable(Assets.UrnStone, 41.3f, 61.5f, 8);
            Web(30f, 55f); Web(31f, 30f); Web(9f, 33f); Web(48f, 33f); Web(51f, 25f); Web(23f, 3f);
            WorldGen.Npc("ilse", "Sister Ilse", Assets.Ghost, 35f, 58.4f, false, false);
            WorldGen.Sign(Assets.Bones, 40.2f, 60.6f, "A pilgrim's remains", "A hand still clutches a slip of vellum:\n\n^wTwo vigil-flames, west and east, kept the Bellgate. The Prior put them out himself. I watched him do it. He was smiling.^0");

            // nave
            float[] pewY = { 34f, 36.5f, 43.6f, 46.2f, 51f };
            foreach (float py in pewY) { WorldGen.Mk(Assets.Pew, 33.1f, py, PropKind.Deco, 28, 6, false); WorldGen.Mk(Assets.Pew, 38.9f, py, PropKind.Deco, 28, 6, false); }
            Pillar(31.6f, 33f); Pillar(40.4f, 33f); Pillar(31.6f, 45f); Pillar(40.4f, 45f);
            Candle(34.2f, 31.3f, Ember); Candle(37.8f, 31.3f, Ember);
            WorldGen.Glow(WorldGen.MkAnim(Assets.BrazierEmber, 32f, 52.6f, PropKind.Deco, 8, 5, false), 96, Ember, 1.05f, -16, true);
            WorldGen.Glow(WorldGen.MkAnim(Assets.BrazierEmber, 40f, 52.6f, PropKind.Deco, 8, 5, false), 96, Ember, 1.05f, -16, true);
            Torch(32.4f, 30f, true); Torch(39.6f, 30f, true);
            Banner(33.7f, 30f); Banner(38.3f, 30f);
            WorldGen.Spawn(EnemyKind.Hollow, 33.2f, 38f, 5); WorldGen.Spawn(EnemyKind.Hollow, 38.8f, 40.6f, 5);
            WorldGen.Spawn(EnemyKind.Hollow, 34f, 47.6f, 5); WorldGen.Spawn(EnemyKind.Hollow, 38.6f, 32.6f, 5);
            WorldGen.Spawn(EnemyKind.Acolyte, 36f, 33f, 5);

            // scriptorium (west)
            float[] shelves = { 10.4f, 12.8f, 15.2f, 19.8f, 22.2f };
            for (int i = 0; i < shelves.Length; i++) WorldGen.Mk(i % 2 == 0 ? Assets.Bookshelf : Assets.Bookshelf2, shelves[i], 34.1f, PropKind.Deco, 30, 6, true);
            Torch(17.5f, 33f, true);
            Prop bw = WorldGen.MkAnim(Assets.BrazierOut, 11f, 43.8f, PropKind.Brazier, 8, 5, false);
            WorldGen.Act(bw, "brazier_w", "Kindle the vigil-flame", 22); m.Mark("brazier_w", bw.X, bw.Y, 20);
            WorldGen.Mk(Assets.Bench, 14f, 39.4f, PropKind.Deco, 18, 4, false); WorldGen.Mk(Assets.Bench, 18.6f, 41.6f, PropKind.Deco, 18, 4, false);
            Candle(12.6f, 39.2f, Ember); Candle(20.2f, 41.4f, Ember); Candle(22.4f, 36f, Ember);
            WorldGen.Sign(Assets.Bench, 16.2f, 37.4f, "The Prior's ledger",
                "The last pages are written in a hand that grows steadily smaller.\n\n^w...the bell is a wound in the evening. Each stroke tears the quiet open again. I have prayed on it. Saint Orrin does not answer, but the dusk does.^0\n\n" +
                "^wI have taken its tongue. I will keep it close, where it cannot speak.^0");
            WorldGen.Chest(22f, 44.6f, "chest_script", false, 1);
            WorldGen.Spawn(EnemyKind.Acolyte, 13f, 37.8f, 6); WorldGen.Spawn(EnemyKind.Acolyte, 19.4f, 37f, 6);
            WorldGen.Spawn(EnemyKind.Hollow, 15f, 43f, 5); WorldGen.Spawn(EnemyKind.Hollow, 21f, 40f, 5);
            WorldGen.Spawn(EnemyKind.Wisp, 11.4f, 36.6f, 5);
            WorldGen.Spawn(EnemyKind.Hollow, 27f, 40.4f, 5);
            WorldGen.Breakable(Assets.UrnStone, 9.7f, 45.4f, 8); WorldGen.Breakable(Assets.Crate, 23f, 34.6f, 12);

            // ossuary (east)
            float[] sarc = { 51f, 37.6f, 59f, 37.6f, 51f, 42.6f, 55f, 42.6f, 59f, 42.6f };
            for (int i = 0; i < sarc.Length; i += 2) WorldGen.Mk(i == 4 ? Assets.SarcOpen : Assets.SarcClosed, sarc[i], sarc[i + 1], PropKind.Deco, 34, 10, false);
            float[] coffins = { 49.5f, 52f, 58.2f, 61f };
            foreach (float cx in coffins) { Prop c = WorldGen.Mk(Assets.Coffin, cx, 33f, PropKind.Deco, 0, 0, false); c.OnWall = true; c.Y -= 1; }
            Torch(50.8f, 33f, true); Torch(59.6f, 33f, true);
            float[] bones = { 49f, 36f, 53.4f, 40f, 57.4f, 35.2f, 61.6f, 40.6f, 54.6f, 44.8f, 49.2f, 44.6f, 60.6f, 35f };
            for (int i = 0; i < bones.Length; i += 2) WorldGen.Mk(i % 4 == 0 ? Assets.SkullPile : Assets.Bones, bones[i], bones[i + 1], PropKind.Deco, 0, 0, false);
            Prop be = WorldGen.MkAnim(Assets.BrazierOut, 61f, 43.8f, PropKind.Brazier, 8, 5, false);
            WorldGen.Act(be, "brazier_e", "Kindle the vigil-flame", 22); m.Mark("brazier_e", be.X, be.Y, 20);
            Prop sw = WorldGen.Mk(Assets.SecretWall, 55f, 33f, PropKind.CrackedWall, 32, 32, true);
            sw.Id = "secret_wall"; sw.HP = 4; sw.OnWall = false;
            WorldGen.Spawn(EnemyKind.Archer, 50f, 35.8f, 6); WorldGen.Spawn(EnemyKind.Archer, 61.2f, 38.6f, 6); WorldGen.Spawn(EnemyKind.Archer, 57f, 44.6f, 6);
            WorldGen.Spawn(EnemyKind.Wisp, 53.2f, 40f, 6); WorldGen.Spawn(EnemyKind.Wisp, 59f, 35.6f, 6);
            WorldGen.Spawn(EnemyKind.Hollow, 57f, 40f, 6);
            WorldGen.Spawn(EnemyKind.Hollow, 44f, 40.4f, 5);
            WorldGen.Breakable(Assets.UrnStone, 48.7f, 45.4f, 8); WorldGen.Breakable(Assets.UrnStone, 62.3f, 45.3f, 8); WorldGen.Breakable(Assets.UrnStone, 62.3f, 34.6f, 8);

            // sealed vault
            WorldGen.Chest(55f, 27.4f, "chest_vault", true, 3);
            Candle(52.4f, 26.4f, Ember); Candle(57.6f, 26.4f, Ember);
            Banner(53f, 25f); Banner(57f, 25f);
            WorldGen.Breakable(Assets.Urn, 51.6f, 29.6f, 8); WorldGen.Breakable(Assets.Urn, 52.4f, 30.2f, 8); WorldGen.Breakable(Assets.Urn, 58.4f, 29.6f, 8); WorldGen.Breakable(Assets.Urn, 57.6f, 30.3f, 8);
            WorldGen.Mk(Assets.SkullPile, 58f, 27f, PropKind.Deco, 0, 0, false);

            // side chapels
            WorldGen.Chest(26.4f, 47.6f, "chest_chapel", false, 1);
            Banner(26.5f, 46f); Candle(24.6f, 47f, Ember);
            WorldGen.Spawn(EnemyKind.Hollow, 26.6f, 49.6f, 5);
            WorldGen.Mk(Assets.SarcOpen, 45.4f, 48f, PropKind.Deco, 34, 10, false);
            WorldGen.Breakable(Assets.UrnStone, 43.6f, 50.5f, 8); WorldGen.Breakable(Assets.UrnStone, 47.3f, 50.5f, 8); WorldGen.Breakable(Assets.UrnStone, 47.3f, 46.8f, 8);
            WorldGen.Spawn(EnemyKind.Wisp, 45.4f, 49.8f, 5);
            Candle(43.6f, 46.8f, Gloam);

            // chancel
            Prop altar = WorldGen.MkAnim(Assets.Altar, 36f, 23.6f, PropKind.Deco, 38, 10, false);
            WorldGen.Glow(altar, 60, Gloam, 0.7f, -16, true);
            Candle(33f, 23.2f, Gloam); Candle(39f, 23.2f, Gloam);
            Banner(32.6f, 21f); Banner(39.4f, 21f);
            Torch(34f, 21f, true); Torch(38f, 21f, true);
            WorldGen.Spawn(EnemyKind.Acolyte, 32.8f, 25f, 6); WorldGen.Spawn(EnemyKind.Acolyte, 39.2f, 25f, 6);
            WorldGen.Spawn(EnemyKind.Hollow, 34.4f, 26.6f, 6); WorldGen.Spawn(EnemyKind.Hollow, 37.6f, 26.6f, 6);
            Prop gate = WorldGen.Mk(Assets.GateClosed, 36f, 30f, PropKind.Gate, 32, 32, true);
            gate.Id = "bellgate"; m.Mark("bellgate", gate.X, gate.Y + 12, 30);
            Prop bg = WorldGen.Mk(Assets.GateOpen, 36f, 21f, PropKind.BossGate, 0, 0, true);
            bg.Id = "bossgate";

            // the Bell Crypt
            Prop bell = WorldGen.Mk(Assets.BellSilent, 36f, 6.6f, PropKind.Bell, 46, 14, true);
            bell.Id = "bell"; bell.Tall = false; bell.InteractR = 34;
            m.Mark("bell", bell.X, bell.Y + 14, 34);
            Pillar(26.6f, 5.6f); Pillar(45.4f, 5.6f); Pillar(26.6f, 12.8f); Pillar(45.4f, 12.8f);
            WorldGen.Mk(Assets.PillarBroken[0], 31f, 4.8f, PropKind.Deco, 10, 6, true); WorldGen.Mk(Assets.PillarBroken[2], 41f, 4.8f, PropKind.Deco, 10, 6, true);
            float[] br = { 24.7f, 4.8f, 47.3f, 4.8f, 24.7f, 14f, 47.3f, 14f };
            for (int i = 0; i < br.Length; i += 2) WorldGen.Glow(WorldGen.MkAnim(Assets.BrazierGloam, br[i], br[i + 1], PropKind.Deco, 8, 5, false), 110, Gloam, 1.0f, -16, true);
            Banner(29f, 3f); Banner(33f, 3f); Banner(39f, 3f); Banner(43f, 3f);
            Torch(31f, 3f, true); Torch(41f, 3f, true);
            WorldGen.Spawn(EnemyKind.Prior, 36f, 9.8f, 8, "prior", true);
            m.Mark("boss", 36 * 16, 9.8f * 16, 120);

            m.BuildMini();
            return m;
        }

        static void Torch(float tx, float ty, bool gloam)
        {
            Prop p = WorldGen.MkAnim(gloam ? Assets.TorchGloam : Assets.TorchEmber, tx, ty, PropKind.Deco, 0, 0, false);
            p.OnWall = true; p.Y -= 5;
            WorldGen.Glow(p, gloam ? 84 : 92, gloam ? WorldGen.GloamLight : WorldGen.EmberLight, gloam ? 0.9f : 1.05f, -4, true);
        }

        static void Banner(float tx, float ty)
        {
            Prop p = WorldGen.Mk(Assets.WallBanner, tx, ty, PropKind.Deco, 0, 0, false);
            p.OnWall = true; p.Y += 4;
        }

        static void Candle(float tx, float ty, int color)
        {
            WorldGen.Glow(WorldGen.MkAnim(Assets.Candles, tx, ty, PropKind.Deco, 4, 3, false), 52, color, 0.75f, -20, true);
        }

        static void Pillar(float tx, float ty)
        {
            WorldGen.Mk(Assets.PillarTall[0], tx, ty, PropKind.Deco, 10, 6, true).Tall = true;
        }

        static void Web(float tx, float ty)
        {
            Prop p = WorldGen.Mk(Assets.Cobweb, tx, ty, PropKind.Deco, 0, 0, false);
            p.Flat = true;
        }
    }
}
