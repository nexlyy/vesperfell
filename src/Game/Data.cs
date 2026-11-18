using System;
using System.Collections.Generic;

namespace Vesperfell
{
    public enum ClassId { Warrior = 0, Mage = 1, Rogue = 2, Ranger = 3 }

    public sealed class AbilityDef
    {
        public string Name, Desc, Key;
        public int Icon;
        public float Cooldown;
        public int Cost;
        public int UnlockLevel = 1;
        public AbilityDef(string key, string name, int icon, float cd, int cost, int unlock, string desc)
        { Key = key; Name = name; Icon = icon; Cooldown = cd; Cost = cost; UnlockLevel = unlock; Desc = desc; }
    }

    public sealed class ClassDef
    {
        public ClassId Id;
        public string Name, Title, Blurb, Lore, Tag;
        public string ResName; public int ResColor, ResColorDark;
        public int BaseHP, HPPerLevel, BaseRes; public float ResRegen;
        public int Might, Finesse, Arcana, Vigor;
        public int Primary;                 // 0 might, 1 finesse, 2 arcana
        public float MoveSpeed; public int BaseArmor; public float Crit, CritMult;
        public AbilityDef[] Abilities;      // 0 LMB, 1 RMB, 2 Q, 3 E, 4 R
        public int[] Bars;                  // health, power, defence, speed, range (1..5)
        public int Accent;
        public int[] WeaponTypes;
        public int ArmorIcon; public string[] ArmorNouns;
        public string Passive;
        public int DodgeCharges = 1;
    }

    public static class Classes
    {
        public const int SlotLMB = 0, SlotRMB = 1, SlotQ = 2, SlotE = 3, SlotR = 4;
        public static readonly ClassDef[] All = Build();
        public static ClassDef Get(ClassId id) { return All[(int)id]; }

        static ClassDef[] Build()
        {
            ClassDef w = new ClassDef();
            w.Id = ClassId.Warrior; w.Name = "Warrior"; w.Title = "Oathguard of the Bell";
            w.Blurb = "A wall of steel. Builds Fury by striking and blocking, then spends it on crushing blows.";
            w.Tag = "Steel and shield. Every blow builds Fury.";
            w.Lore = "The Oathguard once stood at the abbey doors while the Vesper Bell was rung. They are the last who remember the rite.";
            w.ResName = "Fury"; w.ResColor = 0xE0584E; w.ResColorDark = 0x6E1730;
            w.BaseHP = 90; w.HPPerLevel = 12; w.BaseRes = 100; w.ResRegen = 0;
            w.Might = 10; w.Finesse = 5; w.Arcana = 3; w.Vigor = 9; w.Primary = 0;
            w.MoveSpeed = 78; w.BaseArmor = 14; w.Crit = 0.05f; w.CritMult = 1.8f;
            w.Bars = new[] { 5, 3, 5, 2, 1 }; w.Accent = 0xCF4150;
            w.WeaponTypes = new[] { 0, 1 }; w.ArmorIcon = ItemArt.IcPlate; w.ArmorNouns = new[] { "Hauberk", "Cuirass", "Warplate" };
            w.Passive = "Bulwark: strikes and blocked blows build Fury. Heavy armour.";
            w.Abilities = new[]
            {
                new AbilityDef("LMB", "Cleave", ItemArt.SkSlash, 0, 0, 1, "A three-swing combo. The third swing is wider, hits harder and knocks foes back. Each foe struck builds Fury."),
                new AbilityDef("RMB", "Bulwark", ItemArt.SkBlock, 0, 0, 1, "Hold to raise your shield. Blocks 85% of damage from the front, stops projectiles and builds Fury."),
                new AbilityDef("Q", "Shield Rush", ItemArt.SkCharge, 7f, 0, 1, "Charge forward, damaging and stunning every foe in your path. Builds Fury."),
                new AbilityDef("E", "Whirlwind", ItemArt.SkWhirl, 5f, 30, 2, "Spin with your weapon outstretched, striking all nearby foes twice."),
                new AbilityDef("R", "Vesper Oath", ItemArt.SkOath, 25f, 50, 4, "Swear the old oath. For 6 seconds take half damage, deal 25% more and mend your wounds."),
            };

            ClassDef m = new ClassDef();
            m.Id = ClassId.Mage; m.Name = "Mage"; m.Title = "Cinderwright";
            m.Blurb = "Fragile and devastating. Hurls embers from afar, freezes what comes close and blinks out of reach.";
            m.Tag = "Fragile but deadly. Fire, frost and storm.";
            m.Lore = "Cinderwrights kept the abbey's lamps burning with captured embers. Now they turn that fire on the dark.";
            m.ResName = "Mana"; m.ResColor = 0x7FB2F0; m.ResColorDark = 0x1E3A5C;
            m.BaseHP = 60; m.HPPerLevel = 7; m.BaseRes = 100; m.ResRegen = 6.5f;
            m.Might = 3; m.Finesse = 5; m.Arcana = 11; m.Vigor = 5; m.Primary = 2;
            m.MoveSpeed = 76; m.BaseArmor = 3; m.Crit = 0.06f; m.CritMult = 1.8f;
            m.Bars = new[] { 2, 5, 1, 3, 5 }; m.Accent = 0xEC8A32;
            m.WeaponTypes = new[] { 2, 3 }; m.ArmorIcon = ItemArt.IcRobe; m.ArmorNouns = new[] { "Vestment", "Robe", "Mantle" };
            m.Passive = "Kindled: mana returns steadily. Spells light the dark around them.";
            m.Abilities = new[]
            {
                new AbilityDef("LMB", "Ember Bolt", ItemArt.SkBolt, 0, 0, 1, "Fling a bolt of living ember. Costs no mana."),
                new AbilityDef("RMB", "Blink", ItemArt.SkBlink, 3.5f, 15, 1, "Vanish and reappear a short distance toward your cursor. Briefly untouchable."),
                new AbilityDef("Q", "Fireball", ItemArt.SkFireball, 3.5f, 22, 1, "A roaring sphere that explodes on impact and leaves foes burning."),
                new AbilityDef("E", "Frost Nova", ItemArt.SkNova, 9f, 28, 2, "A ring of killing frost. Freezes nearby foes solid, then leaves them slowed."),
                new AbilityDef("R", "Stormcall", ItemArt.SkStorm, 12f, 40, 4, "Lightning strikes the foe nearest your cursor and leaps between up to four more."),
            };

            ClassDef r = new ClassDef();
            r.Id = ClassId.Rogue; r.Name = "Rogue"; r.Title = "Duskblade";
            r.Blurb = "Quick, cruel and hard to pin down. Strikes from behind for bonus damage and vanishes when cornered.";
            r.Tag = "Quick and cruel. Strikes from behind, then vanishes.";
            r.Lore = "When the long dusk fell, the Duskblades stopped stealing from the living and started hunting the dead.";
            r.ResName = "Energy"; r.ResColor = 0xF0C060; r.ResColorDark = 0x7A5A2C;
            r.BaseHP = 70; r.HPPerLevel = 9; r.BaseRes = 100; r.ResRegen = 14f;
            r.Might = 6; r.Finesse = 11; r.Arcana = 4; r.Vigor = 6; r.Primary = 1;
            r.MoveSpeed = 90; r.BaseArmor = 7; r.Crit = 0.2f; r.CritMult = 2.2f;
            r.Bars = new[] { 3, 4, 2, 5, 2 }; r.Accent = 0xA7A0DC; r.DodgeCharges = 2;
            r.WeaponTypes = new[] { 4, 5 }; r.ArmorIcon = ItemArt.IcLeather; r.ArmorNouns = new[] { "Leathers", "Shadowgarb", "Harness" };
            r.Passive = "Backstab: +50% damage from behind. High critical chance. Two dodge charges.";
            r.Abilities = new[]
            {
                new AbilityDef("LMB", "Twin Fangs", ItemArt.SkStab, 0, 0, 1, "A flurry of quick stabs. Strikes from behind deal 50% more damage."),
                new AbilityDef("RMB", "Thrown Knife", ItemArt.SkKnife, 0.8f, 12, 1, "Flick a knife at range."),
                new AbilityDef("Q", "Shadowstep", ItemArt.SkStep, 5f, 25, 1, "Dash through foes, cutting each one. Your next strike is a guaranteed critical hit."),
                new AbilityDef("E", "Veil", ItemArt.SkVeil, 14f, 30, 2, "Melt into smoke for 4 seconds. Foes lose track of you; your next attack is a devastating ambush."),
                new AbilityDef("R", "Fan of Knives", ItemArt.SkFan, 10f, 40, 4, "Hurl a ring of poisoned knives in every direction."),
            };

            ClassDef g = new ClassDef();
            g.Id = ClassId.Ranger; g.Name = "Ranger"; g.Title = "Fenwarden";
            g.Blurb = "Controls the field from a distance with bow, snare and falling arrows.";
            g.Tag = "Bow, snare and falling arrows. Rules the open ground.";
            g.Lore = "Fenwardens walk the wild edge of the vale. They were the first to see the wolves change.";
            g.ResName = "Focus"; g.ResColor = 0x8FCB6B; g.ResColorDark = 0x2A5244;
            g.BaseHP = 72; g.HPPerLevel = 9; g.BaseRes = 100; g.ResRegen = 9f;
            g.Might = 6; g.Finesse = 10; g.Arcana = 5; g.Vigor = 6; g.Primary = 1;
            g.MoveSpeed = 84; g.BaseArmor = 8; g.Crit = 0.12f; g.CritMult = 2.0f;
            g.Bars = new[] { 3, 4, 2, 4, 5 }; g.Accent = 0x72A05A;
            g.WeaponTypes = new[] { 6, 7 }; g.ArmorIcon = ItemArt.IcJerkin; g.ArmorNouns = new[] { "Jerkin", "Huntcoat", "Wildhide" };
            g.Passive = "Steady hand: long reach and reliable critical hits.";
            g.Abilities = new[]
            {
                new AbilityDef("LMB", "Loose", ItemArt.SkShot, 0, 0, 1, "Draw and loose an arrow. Hold to keep firing."),
                new AbilityDef("RMB", "Piercing Shot", ItemArt.SkPierce, 3f, 18, 1, "A heavy arrow that punches through every foe in a line."),
                new AbilityDef("Q", "Snare", ItemArt.SkTrap, 6f, 20, 1, "Set a hidden trap at your cursor. The first foe to step in is wounded and rooted in place."),
                new AbilityDef("E", "Arrow Rain", ItemArt.SkVolley, 11f, 35, 2, "Call a volley onto an area. Arrows fall for a few seconds, wounding and slowing all within."),
                new AbilityDef("R", "Hunter's Mark", ItemArt.SkMark, 14f, 30, 4, "Mark the foe nearest your cursor - it takes 35% more damage - and send a seeking arrow after it."),
            };
            return new[] { w, m, r, g };
        }
    }

    // weapons
    public sealed class WeaponType
    {
        public string Name; public int Icon; public float Speed, DmgMult, Reach, Arc; public bool Ranged; public string[] Nouns;
        public WeaponType(string name, int icon, float interval, float dmgMult, float reach, float arcDeg, bool ranged, string[] nouns)
        { Name = name; Icon = icon; Speed = interval; DmgMult = dmgMult; Reach = reach; Arc = arcDeg * M.PI / 180f; Ranged = ranged; Nouns = nouns; }
    }

    public static class Weapons
    {
        // Speed is the seconds between basic attacks.
        public static readonly WeaponType[] Types =
        {
            new WeaponType("Longsword", ItemArt.IcSword, 0.40f, 1.00f, 27, 115, false, new[]{ "Longsword", "Arming Sword", "Warblade" }),
            new WeaponType("Greataxe", ItemArt.IcAxe, 0.60f, 1.48f, 31, 150, false, new[]{ "Greataxe", "Headsman's Axe", "Cleaver" }),
            new WeaponType("Staff", ItemArt.IcStaff, 0.44f, 1.00f, 0, 0, true, new[]{ "Staff", "Ember Staff", "Brandstaff" }),
            new WeaponType("Wand", ItemArt.IcWand, 0.30f, 0.70f, 0, 0, true, new[]{ "Wand", "Cinder Wand", "Kindling Rod" }),
            new WeaponType("Daggers", ItemArt.IcDagger, 0.24f, 0.60f, 21, 75, false, new[]{ "Daggers", "Twin Fangs", "Stilettos" }),
            new WeaponType("Kris", ItemArt.IcKris, 0.32f, 0.82f, 22, 80, false, new[]{ "Kris", "Serpent Blade", "Flamberge Knife" }),
            new WeaponType("Longbow", ItemArt.IcLongbow, 0.56f, 1.00f, 0, 0, true, new[]{ "Longbow", "Warbow", "Yew Bow" }),
            new WeaponType("Shortbow", ItemArt.IcShortbow, 0.38f, 0.70f, 0, 0, true, new[]{ "Shortbow", "Hunting Bow", "Horn Bow" }),
        };
    }

    // enemies
    public enum EnemyKind { Wolf, AlphaWolf, Crawler, Hollow, Archer, Wisp, Acolyte, Knight, Prior }

    public sealed class EnemyDef
    {
        public EnemyKind Kind; public string Name;
        public float HP, Dmg, Speed, Radius, Aggro, Range;
        public int XP, Level, GoldMin, GoldMax; public float LootChance;
        public bool Elite, Boss, Flying, Undead;
        public int BloodColor;
        public float KnockRes;      // 0 = full knockback, 1 = immovable
    }

    public static class Enemies
    {
        public static readonly EnemyDef[] Defs = Build();
        public static EnemyDef Get(EnemyKind k) { return Defs[(int)k]; }

        static EnemyDef D(EnemyKind k, string name, float hp, float dmg, float speed, float radius, float aggro, float range, int xp, int level, int gmin, int gmax, float loot)
        {
            EnemyDef d = new EnemyDef();
            d.Kind = k; d.Name = name; d.HP = hp; d.Dmg = dmg; d.Speed = speed; d.Radius = radius; d.Aggro = aggro; d.Range = range;
            d.XP = xp; d.Level = level; d.GoldMin = gmin; d.GoldMax = gmax; d.LootChance = loot; d.BloodColor = 0x8F2F22;
            return d;
        }

        static EnemyDef[] Build()
        {
            EnemyDef[] a = new EnemyDef[9];
            a[0] = D(EnemyKind.Wolf, "Gloam Wolf", 44, 11, 82, 6, 120, 60, 16, 1, 1, 4, 0.16f);
            a[1] = D(EnemyKind.AlphaWolf, "Pack Mother", 170, 17, 86, 7, 140, 60, 60, 3, 8, 16, 1f); a[1].KnockRes = 0.5f;
            a[2] = D(EnemyKind.Crawler, "Mire Crawler", 42, 10, 64, 6, 125, 120, 18, 2, 1, 5, 0.18f); a[2].BloodColor = 0x6FA82E;
            a[3] = D(EnemyKind.Hollow, "Hollow", 84, 16, 40, 5.5f, 115, 22, 22, 3, 2, 7, 0.2f); a[3].Undead = true; a[3].BloodColor = 0x3A2E5E;
            a[4] = D(EnemyKind.Archer, "Barrow Archer", 58, 15, 58, 5.5f, 170, 150, 24, 4, 2, 8, 0.22f); a[4].Undead = true; a[4].BloodColor = 0xC8BE9F;
            a[5] = D(EnemyKind.Wisp, "Gloam Wisp", 40, 14, 50, 5, 150, 110, 24, 4, 1, 6, 0.2f); a[5].Flying = true; a[5].Undead = true; a[5].BloodColor = 0xA7A0DC;
            a[6] = D(EnemyKind.Acolyte, "Acolyte of the Hush", 96, 19, 46, 5.5f, 160, 130, 34, 6, 4, 12, 0.3f); a[6].BloodColor = 0x6E1730;
            a[7] = D(EnemyKind.Knight, "Sir Caldus, the Barrow Knight", 1400, 27, 50, 9, 170, 38, 220, 5, 60, 90, 1f); a[7].Elite = true; a[7].Undead = true; a[7].KnockRes = 0.9f; a[7].BloodColor = 0x58A8D8;
            a[8] = D(EnemyKind.Prior, "The Silent Prior", 4600, 25, 44, 10, 400, 44, 600, 8, 150, 200, 1f); a[8].Boss = true; a[8].Undead = true; a[8].KnockRes = 1f; a[8].BloodColor = 0x7F6CB6;
            return a;
        }
    }

    // zones
    public sealed class ZoneDef
    {
        public int Id; public string Name, Sub;
        public float R, G, B;          // ambient light
        public float Fog; public int FogColor;
        public int Music, Amb;
        public bool Indoor;
        public ZoneDef(int id, string name, string sub, float r, float g, float b, float fog, int fogColor, int music, int amb, bool indoor)
        { Id = id; Name = name; Sub = sub; R = r; G = g; B = b; Fog = fog; FogColor = fogColor; Music = music; Amb = amb; Indoor = indoor; }
    }

    public static class Zones
    {
        public const int LowRoad = 0, Emberwick = 1, Thornwood = 2, Mirefen = 3, Barrowfield = 4, AbbeyRise = 5, AshenFields = 6, Abbey = 7, BellCrypt = 8;
        public const int MusMenu = 0, MusVillage = 1, MusWild = 2, MusRuins = 3, MusDungeon = 4, MusBoss = 5, MusDawn = 6;
        public const int AmbField = 0, AmbVillage = 1, AmbForest = 2, AmbMarsh = 3, AmbRuins = 4, AmbDungeon = 5;

        public static readonly ZoneDef[] All =
        {
            new ZoneDef(0, "The Low Road", "where the pilgrims once walked", 0.58f, 0.62f, 0.88f, 0.26f, 0x8FA0C8, MusWild, AmbField, false),
            new ZoneDef(1, "Emberwick", "the last lit hearths of the vale", 0.64f, 0.63f, 0.84f, 0.12f, 0xA8A0B8, MusVillage, AmbVillage, false),
            new ZoneDef(2, "The Thornwood", "something watches from the brakes", 0.44f, 0.52f, 0.70f, 0.46f, 0x6F98A8, MusWild, AmbForest, false),
            new ZoneDef(3, "Mirefen", "drowned ground, drowned names", 0.44f, 0.54f, 0.60f, 0.62f, 0x7FA890, MusWild, AmbMarsh, false),
            new ZoneDef(4, "The Barrowfield", "kings sleep lightly here", 0.52f, 0.53f, 0.76f, 0.40f, 0xA0A8C8, MusRuins, AmbRuins, false),
            new ZoneDef(5, "Abbey Rise", "the bell tower stands mute", 0.47f, 0.45f, 0.72f, 0.50f, 0x9488C8, MusRuins, AmbRuins, false),
            new ZoneDef(6, "The Ashen Fields", "burned the night the bell fell silent", 0.52f, 0.50f, 0.70f, 0.32f, 0xA8A0A0, MusRuins, AmbField, false),
            new ZoneDef(7, "The Sunken Abbey", "undercroft of Saint Orrin", 0.25f, 0.25f, 0.40f, 0.22f, 0x7060A0, MusDungeon, AmbDungeon, true),
            new ZoneDef(8, "The Bell Crypt", "", 0.27f, 0.22f, 0.42f, 0.30f, 0x8060B0, MusDungeon, AmbDungeon, true),
        };
    }

    /// <summary>Loading-screen and journal lore snippets.</summary>
    public static class Lore
    {
        public static readonly string[] Tips =
        {
            "Each evening the Vesper Bell was rung, and the Gloam drew back from the vale. Thirty nights ago, it fell silent.",
            "Wayshrines still hold a spark of the bell's blessing. Rest at one to mend your wounds and mark your return.",
            "Hold your ground or give it: Space rolls you clear of a telegraphed blow.",
            "The dead of the Barrowfield were kings once. They do not care for visitors.",
            "Moonpetals only open in the dark. In Vesperfell, that is no longer a short season.",
            "A red shape on the ground is a blow about to land. Be somewhere else.",
            "Bram the smith will temper worn steel for coin. Ysolde sells draughts for those who bleed often.",
            "Resting at a wayshrine stirs the Gloam: common foes will rise again.",
            "They say the Prior loved silence more than he loved the saint.",
            "Three stones remember the old kings. Wake all three and their hoard may open.",
        };
    }
}
