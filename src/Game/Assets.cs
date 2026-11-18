using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>Every sprite in the game, generated once at start-up.</summary>
    public static class Assets
    {
        public static bool Built;

        public static ActorSprites[] Hero = new ActorSprites[4];
        public static ActorSprites Wolf, AlphaWolf, Crawler, Wisp, Skeleton, Acolyte, Knight, Prior;
        public static ActorSprites[] Hollow;
        public static ActorSprites Elder, Smith, Herbalist, Innkeeper, Warden, VillagerA, VillagerB, Child, Ghost;

        public static Sprite[,] Weapon = new Sprite[8, 4];
        public static Sprite Shield, Cleaver, Greatsword, Crozier, CandleStaff, BoneBow;
        public static Sprite Arrow, BoneArrow, Knife, EmberBolt, GloamBolt, FrostBolt, Fireball, GloamOrb, VenomOrb, WispOrb;
        public static Sprite[] Coin, SparkEmber, SparkWhite, SparkGloam, SparkFrost;
        public static Sprite[] SlashS, SlashM, SlashL, SlashRed, SlashGloam, SlashHuge;
        public static Sprite[,] Icon = new Sprite[ItemArt.IconCount, 4];
        public static Sprite[] Skill = new Sprite[ItemArt.SkCount];
        public static Sprite CursorArrow, CursorAim;

        // nature
        public static Sprite[] OakTeal, OakGreen, OakRust, OakViolet, Pine, PineDark, Dead, DeadPale, DeadBurnt, Bush, BushBerry, Bramble,
            RockS, RockM, RockL, RockMossM, RockMossL, Tuft, TuftTall, TuftPale, TuftPaleTall, TuftDark, Flower, Mushroom, MushroomGlow, Reeds, Lily;
        public static Sprite Moonpetal, MoonpetalPicked, Stump, Log;

        // light sources and interactive props
        public static Sprite[] CampfireLit, CampfireOut, LanternLit, LanternOut, BrazierEmber, BrazierGloam, BrazierOut, TorchEmber, TorchGloam, Candles, ShrineLit, ShrineOut, Altar;
        public static Sprite ChestWood, ChestWoodOpen, ChestOrnate, ChestOrnateOpen, Barrel, Crate, Urn, UrnStone, Sack, Cart, HayBale, Woodpile, Anvil, SignR, SignL,
            NoticeBoard, FenceH, FenceV, Bench, Banner, Well, Stall, Stall2, Bedroll, Tent, Pew, Bones, Cobweb, GateClosed, GateOpen, SecretWall, Stairs,
            BellSilent, BellRung, SkullPile, WallBanner, Coffin, SarcClosed, SarcOpen, Bookshelf, Bookshelf2, Abbey, Watchtower, BarrowSealed, BarrowRune, BarrowOpen,
            BarrowDeco, StatueA, StatueB, Arch;
        public static Sprite[] Palisade, RuinWall, PillarTall, PillarBroken, Grave, Rubble, StoneUnlit, StoneLit, StoneDeco;
        public static Sprite HouseElder, HouseInn, HouseSmith, HouseHerb, HouseA, HouseB, HouseC;

        static Sprite[] Many(int n, Func<int, Sprite> f)
        {
            Sprite[] a = new Sprite[n];
            for (int i = 0; i < n; i++) a[i] = f(i);
            return a;
        }

        public static void Build(Action<float, string> progress)
        {
            if (Built) return;
            Rng r = new Rng(20261115);
            if (progress != null) progress(0.02f, "Sharpening steel");

            Hero[0] = CharacterArt.Build(CharacterArt.Warrior());
            Hero[1] = CharacterArt.Build(CharacterArt.Mage());
            Hero[2] = CharacterArt.Build(CharacterArt.Rogue());
            Hero[3] = CharacterArt.Build(CharacterArt.Ranger());
            Wolf = CreatureArt.Wolf(false); AlphaWolf = CreatureArt.Wolf(true); Crawler = CreatureArt.Crawler(); Wisp = CreatureArt.Wisp();
            Hollow = new[] { CreatureArt.Hollow(0), CreatureArt.Hollow(1), CreatureArt.Hollow(2) };
            Skeleton = CreatureArt.Skeleton(); Acolyte = CreatureArt.Acolyte(); Knight = CreatureArt.BarrowKnight(); Prior = CreatureArt.Prior();
            Elder = CreatureArt.Elder(); Smith = CreatureArt.Smith(); Herbalist = CreatureArt.Herbalist(); Innkeeper = CreatureArt.Innkeeper();
            Warden = CreatureArt.Warden(); VillagerA = CreatureArt.VillagerA(); VillagerB = CreatureArt.VillagerB(); Child = CreatureArt.Child(); Ghost = CreatureArt.Ghost();

            for (int t = 0; t < 4; t++)
            {
                Weapon[0, t] = ItemArt.Sword(t); Weapon[1, t] = ItemArt.Axe(t); Weapon[2, t] = ItemArt.Staff(t); Weapon[3, t] = ItemArt.Wand(t);
                Weapon[4, t] = ItemArt.Dagger(t, false); Weapon[5, t] = ItemArt.Dagger(t, true); Weapon[6, t] = ItemArt.Bow(t, true); Weapon[7, t] = ItemArt.Bow(t, false);
                for (int i = 0; i < ItemArt.IconCount; i++) Icon[i, t] = ItemArt.Icon(i, t);
            }
            for (int i = 0; i < ItemArt.SkCount; i++) Skill[i] = ItemArt.Skill(i);
            Shield = ItemArt.Shield(); Cleaver = ItemArt.Cleaver(); Greatsword = ItemArt.Greatsword(); Crozier = ItemArt.Crozier(); CandleStaff = ItemArt.CandleStaff();
            BoneBow = ItemArt.Bow(2, false);
            Arrow = ItemArt.Arrow(false); BoneArrow = ItemArt.Arrow(true); Knife = ItemArt.Knife();
            EmberBolt = ItemArt.Bolt(ItemArt.RampEmber, 9, 5); GloamBolt = ItemArt.Bolt(ItemArt.RampGloam, 10, 5); FrostBolt = ItemArt.Bolt(ItemArt.RampFrost, 9, 5);
            Fireball = ItemArt.Orb(ItemArt.RampEmber, 5); GloamOrb = ItemArt.Orb(ItemArt.RampGloam, 4); VenomOrb = ItemArt.Orb(ItemArt.RampVenom, 3); WispOrb = ItemArt.Orb(ItemArt.RampGloam, 3);
            Coin = ItemArt.Coin();
            SparkEmber = ItemArt.Spark(Pal.Em5); SparkWhite = ItemArt.Spark(Pal.Bn4); SparkGloam = ItemArt.Spark(Pal.Gl4); SparkFrost = ItemArt.Spark(Pal.Fr3);
            SlashS = ItemArt.Slash(14, 1.5f, Pal.Bn4, Pal.St5);
            SlashM = ItemArt.Slash(22, 2.2f, Pal.Bn4, Pal.St5);
            SlashL = ItemArt.Slash(28, 2.8f, Pal.Bn4, Pal.St5);
            SlashRed = ItemArt.Slash(24, 2.0f, Pal.Cr4, Pal.Cr2);
            SlashGloam = ItemArt.Slash(36, 2.9f, Pal.Gl6, Pal.Gl3);
            SlashHuge = ItemArt.Slash(40, 2.8f, Pal.Fr4, Pal.Fr2);
            CursorArrow = ItemArt.Cursor(false); CursorAim = ItemArt.Cursor(true);

            if (progress != null) progress(0.2f, "Growing the Thornwood");
            OakTeal = Many(10, delegate(int i) { return NatureArt.Oak(r, NatureArt.RampTeal, 0.85f + (i % 4) * 0.08f); });
            OakGreen = Many(6, delegate(int i) { return NatureArt.Oak(r, NatureArt.RampGreen, 0.85f + (i % 3) * 0.1f); });
            OakRust = Many(4, delegate(int i) { return NatureArt.Oak(r, NatureArt.RampRust, 0.9f + (i % 2) * 0.1f); });
            OakViolet = Many(3, delegate(int i) { return NatureArt.Oak(r, NatureArt.RampViolet, 0.95f); });
            Pine = Many(8, delegate(int i) { return NatureArt.Pine(r, NatureArt.RampTeal, 0.85f + (i % 4) * 0.08f); });
            PineDark = Many(5, delegate(int i) { return NatureArt.Pine(r, new[] { Pal.Ink2, Pal.Lf0, Pal.Lf1, Pal.Lf2, Pal.Lf3 }, 0.95f + (i % 3) * 0.08f); });
            Dead = Many(6, delegate(int i) { return NatureArt.DeadTree(r, Pal.Ea2, 0.85f + (i % 3) * 0.12f); });
            DeadPale = Many(5, delegate(int i) { return NatureArt.DeadTree(r, Pal.St3, 0.85f + (i % 3) * 0.12f); });
            DeadBurnt = Many(4, delegate(int i) { return NatureArt.DeadTree(r, Pal.Ink3, 0.8f + (i % 2) * 0.15f); });
            Bush = Many(5, delegate(int i) { return NatureArt.Bush(r, i % 2 == 0 ? NatureArt.RampTeal : NatureArt.RampGreen, false); });
            BushBerry = Many(3, delegate(int i) { return NatureArt.Bush(r, NatureArt.RampGreen, true); });
            Bramble = Many(3, delegate(int i) { return NatureArt.Bramble(r); });
            RockS = Many(4, delegate(int i) { return NatureArt.Rock(r, 0, i % 2 == 0); });
            RockM = Many(4, delegate(int i) { return NatureArt.Rock(r, 1, false); });
            RockL = Many(3, delegate(int i) { return NatureArt.Rock(r, 2, false); });
            RockMossM = Many(3, delegate(int i) { return NatureArt.Rock(r, 1, true); });
            RockMossL = Many(3, delegate(int i) { return NatureArt.Rock(r, 2, true); });
            Tuft = Many(8, delegate(int i) { return NatureArt.GrassTuft(r, Pal.Gr5, Pal.Gr4, false); });
            TuftTall = Many(6, delegate(int i) { return NatureArt.GrassTuft(r, Pal.Gr5, Pal.Gr4, true); });
            TuftPale = Many(6, delegate(int i) { return NatureArt.GrassTuft(r, Pal.Bn1, Col.Hex(0x6E8A70), false); });
            TuftPaleTall = Many(6, delegate(int i) { return NatureArt.GrassTuft(r, Pal.Bn2, Col.Hex(0x6E8A70), true); });
            TuftDark = Many(6, delegate(int i) { return NatureArt.GrassTuft(r, Pal.Lf3, Pal.Lf2, i % 2 == 0); });
            int[] petals = { Pal.Bn3, Pal.Gl4, Pal.Cr3, Pal.Em5, Pal.Gl5 };
            Flower = Many(10, delegate(int i) { return NatureArt.Flower(r, petals[i % petals.Length], i % 2 == 0 ? Pal.Em5 : Pal.Bn4); });
            Mushroom = Many(4, delegate(int i) { return NatureArt.Mushroom(r, false); });
            MushroomGlow = Many(4, delegate(int i) { return NatureArt.Mushroom(r, true); });
            Reeds = Many(5, delegate(int i) { return NatureArt.Reeds(r); });
            Lily = Many(4, delegate(int i) { return NatureArt.LilyPad(r); });
            Moonpetal = NatureArt.Moonpetal(false); MoonpetalPicked = NatureArt.Moonpetal(true);
            Stump = NatureArt.Stump(r); Log = NatureArt.Log(r);

            if (progress != null) progress(0.4f, "Raising Emberwick");
            CampfireLit = PropArt.Campfire(true); CampfireOut = PropArt.Campfire(false);
            LanternLit = PropArt.LanternPost(true); LanternOut = PropArt.LanternPost(false);
            BrazierEmber = PropArt.Brazier(true, 0); BrazierGloam = PropArt.Brazier(true, 1); BrazierOut = PropArt.Brazier(false, 0);
            TorchEmber = PropArt.WallTorch(0); TorchGloam = PropArt.WallTorch(1); Candles = PropArt.CandleStand();
            ShrineLit = PropArt.Shrine(true); ShrineOut = PropArt.Shrine(false); Altar = PropArt.Altar();
            ChestWood = PropArt.Chest(false, false); ChestWoodOpen = PropArt.Chest(false, true); ChestOrnate = PropArt.Chest(true, false); ChestOrnateOpen = PropArt.Chest(true, true);
            Barrel = PropArt.Barrel(); Crate = PropArt.Crate(); Urn = PropArt.Urn(false); UrnStone = PropArt.Urn(true); Sack = PropArt.Sack(); Cart = PropArt.Cart();
            HayBale = PropArt.HayBale(); Woodpile = PropArt.Woodpile(); Anvil = PropArt.Anvil(); SignR = PropArt.Signpost(1); SignL = PropArt.Signpost(-1);
            NoticeBoard = PropArt.NoticeBoard(); FenceH = PropArt.FenceH(); FenceV = PropArt.FenceV(); Bench = PropArt.Bench(); Banner = PropArt.Banner(Pal.Cr2);
            Well = PropArt.Well(); Stall = PropArt.Stall(Pal.Cr2, Pal.Bn2); Stall2 = PropArt.Stall(Pal.Lf3, Pal.Bn2); Bedroll = PropArt.Bedroll();
            Tent = StructureArt.Tent(r, Pal.Bn1);
            Palisade = Many(4, delegate(int i) { return PropArt.Palisade(r); });

            HouseElder = StructureArt.House(r, 96, 30, 44, StructureArt.RoofTile, 0, 42, new[] { 10, 24, 64, 78 }, true, true, -1);
            HouseInn = StructureArt.House(r, 88, 28, 40, StructureArt.RoofSlate, 0, 14, new[] { 36, 52, 70 }, true, true, -1);
            HouseSmith = StructureArt.House(r, 72, 26, 30, StructureArt.RoofSlate, 1, -1, new[] { 8 }, true, false, 30);
            HouseHerb = StructureArt.House(r, 50, 24, 30, StructureArt.RoofThatch, 0, 8, new[] { 30 }, false, true, -1);
            HouseA = StructureArt.House(r, 64, 26, 34, StructureArt.RoofSlate, 0, 12, new[] { 34, 48 }, true, true, -1);
            HouseB = StructureArt.House(r, 56, 24, 32, StructureArt.RoofThatch, 0, 32, new[] { 10 }, true, true, -1);
            HouseC = StructureArt.House(r, 60, 26, 32, StructureArt.RoofMoss, 0, 8, new[] { 30, 44 }, false, false, -1);

            if (progress != null) progress(0.6f, "Unearthing the Barrowfield");
            RuinWall = new[] { StructureArt.RuinWall(r, 44, 26), StructureArt.RuinWall(r, 30, 22), StructureArt.RuinWall(r, 60, 30), StructureArt.RuinWall(r, 22, 18) };
            PillarTall = Many(2, delegate(int i) { return StructureArt.Pillar(r, 34 + i * 6, false); });
            PillarBroken = Many(3, delegate(int i) { return StructureArt.Pillar(r, 14 + i * 7, true); });
            Grave = Many(6, delegate(int i) { return StructureArt.Gravestone(r, i); });
            Rubble = Many(3, delegate(int i) { return PropArt.Rubble(r); });
            StoneUnlit = Many(3, delegate(int i) { return StructureArt.StandingStone(new Rng(900 + i), 32, i, false); });
            StoneLit = Many(3, delegate(int i) { return StructureArt.StandingStone(new Rng(900 + i), 32, i, true); });
            StoneDeco = Many(6, delegate(int i) { return StructureArt.StandingStone(r, 24 + (i * 5) % 14, i, false); });
            StatueA = StructureArt.Statue(r, false); StatueB = StructureArt.Statue(r, true); Arch = StructureArt.Arch(r);
            Watchtower = StructureArt.Watchtower(r);
            BarrowSealed = StructureArt.Barrow(r, false, false); BarrowRune = StructureArt.Barrow(r, false, true); BarrowOpen = StructureArt.Barrow(r, true, false);
            BarrowDeco = StructureArt.Barrow(new Rng(12), false, false);
            Abbey = StructureArt.Abbey();

            if (progress != null) progress(0.8f, "Lighting the undercroft");
            Pew = PropArt.Pew(); Bones = PropArt.Bones(r); Cobweb = PropArt.Cobweb(); GateClosed = PropArt.Gate(false); GateOpen = PropArt.Gate(true);
            Stairs = PropArt.Stairs(); BellSilent = PropArt.Bell(0); BellRung = PropArt.Bell(1); SkullPile = PropArt.SkullPile(r); WallBanner = PropArt.WallBanner();
            Coffin = PropArt.Coffin(); SarcClosed = PropArt.Sarcophagus(r, false); SarcOpen = PropArt.Sarcophagus(r, true);
            Bookshelf = PropArt.Bookshelf(r); Bookshelf2 = PropArt.Bookshelf(r);
            SecretWall = BuildSecretWall();

            Built = true;
            if (progress != null) progress(1f, "Ready");
        }

        /// <summary>A two-tile chunk of dungeon wall that matches the baked walls, used to hide secret passages.</summary>
        static Sprite BuildSecretWall()
        {
            int tw = 4, th = 5;
            byte[] terr = new byte[tw * th];
            for (int i = 0; i < terr.Length; i++) terr[i] = T.DWall;
            for (int x = 0; x < tw; x++) terr[4 * tw + x] = T.DFloor;
            int[] g = TerrainArt.BakeDungeon(terr, tw, th, 4242);
            // 8 px of wall top + the 24 px brick face = exactly the height of a two-tile wall
            Cv cv = new Cv(32, 32);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    cv.Set(x, y, Col.Hex(g[(32 + y) * (tw * 16) + 16 + x]));
            // hairline cracks and a glint of something beyond
            int c = Pal.Ink0;
            cv.Line(15, 9, 13, 16, c); cv.Line(13, 16, 18, 22, c); cv.Line(18, 22, 15, 31, c);
            cv.Line(13, 16, 8, 19, c); cv.Line(18, 22, 24, 24, c); cv.Line(20, 11, 22, 17, c);
            cv.Set(16, 18, Col.E(Pal.Go3)); cv.Set(14, 24, Col.E(Pal.Go2));
            return cv.ToSprite(16, 32);
        }
    }
}
