using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Vesperfell
{
    public enum ItemKind { Weapon = 0, Armor = 1, Amulet = 2, Ring = 3, Potion = 4, Quest = 5 }
    public enum Rarity { Common = 0, Fine = 1, Rare = 2, Relic = 3 }

    public sealed class Item
    {
        public ItemKind Kind; public int Base; public string Name = ""; public Rarity Rar; public int Level = 1;
        public int DmgMin, DmgMax, Armor;
        public int Might, Finesse, Arcana, Vigor, HP, Res, Crit, Move, Leech;
        public int Upgrade; public int Count = 1; public bool Unique;
        public string QuestId = "", Flavor = "";
        public int IconOverride = -1;

        public int Tier { get { return (int)Rar; } }

        public int Icon
        {
            get
            {
                if (IconOverride >= 0) return IconOverride;
                switch (Kind)
                {
                    case ItemKind.Weapon: return Weapons.Types[Base].Icon;
                    case ItemKind.Armor: return Classes.All[M.Clamp(Base, 0, 3)].ArmorIcon;
                    case ItemKind.Amulet: return (Base & 1) == 0 ? ItemArt.IcAmulet : ItemArt.IcCharm;
                    case ItemKind.Ring: return (Base & 1) == 0 ? ItemArt.IcRing : ItemArt.IcSignet;
                    case ItemKind.Potion: return Base == 0 ? ItemArt.IcHealth : ItemArt.IcMana;
                    default: return ItemArt.IcSigil;
                }
            }
        }

        public string DisplayName { get { string n = Loc.ItemName(Name); return Upgrade > 0 ? n + " +" + Upgrade : n; } }
        public int EffMin { get { return (int)Math.Round(DmgMin * (1 + 0.09f * Upgrade)); } }
        public int EffMax { get { return (int)Math.Round(DmgMax * (1 + 0.09f * Upgrade)); } }
        public int EffArmor { get { return (int)Math.Round(Armor * (1 + 0.09f * Upgrade)); } }

        public bool IsEquip { get { return Kind == ItemKind.Weapon || Kind == ItemKind.Armor || Kind == ItemKind.Amulet || Kind == ItemKind.Ring; } }

        public int Value
        {
            get
            {
                int v = 4 + Level * 3;
                v = (int)(v * (1 + (int)Rar * 0.9f) * (1 + Upgrade * 0.3f));
                return v;
            }
        }

        /// <summary>A rough single number used to show whether an item is an upgrade.</summary>
        public float Score
        {
            get
            {
                float s = (EffMin + EffMax) * 0.5f * (Kind == ItemKind.Weapon ? 1f / Weapons.Types[Base].Speed * 0.45f : 0f);
                s += EffArmor * 0.9f + (Might + Finesse + Arcana + Vigor) * 2.2f + HP * 0.16f + Res * 0.12f + Crit * 1.6f + Move * 1.1f + Leech * 2.4f;
                return s;
            }
        }

        public string Serialize()
        {
            StringBuilder sb = new StringBuilder();
            object[] f = { (int)Kind, Base, Name, (int)Rar, Level, DmgMin, DmgMax, Armor, Might, Finesse, Arcana, Vigor, HP, Res, Crit, Move, Leech, Upgrade, Count, Unique ? 1 : 0, QuestId, Flavor, IconOverride };
            for (int i = 0; i < f.Length; i++) { if (i > 0) sb.Append('|'); sb.Append(Convert.ToString(f[i], CultureInfo.InvariantCulture).Replace('|', '/')); }
            return sb.ToString();
        }

        public static Item Parse(string s)
        {
            string[] p = s.Split('|');
            if (p.Length < 22) return null;
            Item it = new Item();
            it.Kind = (ItemKind)I(p[0]); it.Base = I(p[1]); it.Name = p[2]; it.Rar = (Rarity)M.Clamp(I(p[3]), 0, 3); it.Level = I(p[4]);
            it.DmgMin = I(p[5]); it.DmgMax = I(p[6]); it.Armor = I(p[7]); it.Might = I(p[8]); it.Finesse = I(p[9]); it.Arcana = I(p[10]); it.Vigor = I(p[11]);
            it.HP = I(p[12]); it.Res = I(p[13]); it.Crit = I(p[14]); it.Move = I(p[15]); it.Leech = I(p[16]); it.Upgrade = I(p[17]); it.Count = I(p[18]);
            it.Unique = I(p[19]) != 0; it.QuestId = p[20]; it.Flavor = p[21];
            if (p.Length > 22) it.IconOverride = I(p[22]);
            if ((int)it.Kind < 0 || (int)it.Kind > 5) return null;
            if (it.Kind == ItemKind.Weapon) it.Base = M.Clamp(it.Base, 0, Weapons.Types.Length - 1);
            return it;
        }

        static int I(string s) { int v; return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0; }
    }

    public static class Items
    {
        public static readonly int[] RarityColor = { 0xD9D2BE, 0x8FCB6B, 0x7FB2F0, 0xF09040 };
        public static readonly string[] RarityName = { "Common", "Fine", "Rare", "Relic" };
        public static readonly char[] RarityCode = { 'w', 'g', 'b', 'o' };
        static readonly float[] RarMult = { 1f, 1.13f, 1.28f, 1.46f };

        static readonly string[] FinePrefix = { "Sturdy", "Keen", "Tempered", "Trusty", "Honed", "Wayfarer's" };
        static readonly string[] RarePrefix = { "Moonforged", "Gloamtouched", "Vigilant", "Warden's", "Bellwrought", "Dusk-bound" };
        static readonly string[] RelicPrefix = { "Saint-blessed", "Kingsworn", "Emberheart", "Evensong" };

        public static Item Weapon(Rng r, int type, int level, Rarity rar)
        {
            WeaponType wt = Weapons.Types[type];
            Item it = new Item();
            it.Kind = ItemKind.Weapon; it.Base = type; it.Level = level; it.Rar = rar;
            float avg = (7f + level * 2.4f) * RarMult[(int)rar] * wt.DmgMult;
            it.DmgMin = Math.Max(1, (int)Math.Round(avg * 0.82f)); it.DmgMax = Math.Max(it.DmgMin + 1, (int)Math.Round(avg * 1.18f));
            string noun = wt.Nouns[Math.Min(wt.Nouns.Length - 1, level >= 6 ? 2 : (level >= 3 ? 1 : 0))];
            AddAffixes(r, it, noun);
            return it;
        }

        public static Item Armor(Rng r, ClassId cls, int level, Rarity rar)
        {
            Item it = new Item();
            it.Kind = ItemKind.Armor; it.Base = (int)cls; it.Level = level; it.Rar = rar;
            float[] baseA = { 22, 5, 11, 13 }, perL = { 3.2f, 1.1f, 1.9f, 2.1f };
            it.Armor = (int)Math.Round((baseA[(int)cls] + level * perL[(int)cls]) * RarMult[(int)rar]);
            string[] nouns = Classes.Get(cls).ArmorNouns;
            AddAffixes(r, it, nouns[Math.Min(nouns.Length - 1, level >= 6 ? 2 : (level >= 3 ? 1 : 0))]);
            return it;
        }

        public static Item Trinket(Rng r, bool ring, int level, Rarity rar)
        {
            Item it = new Item();
            it.Kind = ring ? ItemKind.Ring : ItemKind.Amulet; it.Base = r.Int(2); it.Level = level;
            it.Rar = rar == Rarity.Common ? Rarity.Fine : rar;
            AddAffixes(r, it, ring ? (it.Base == 0 ? "Ring" : "Signet") : (it.Base == 0 ? "Amulet" : "Charm"));
            return it;
        }

        static void AddAffixes(Rng r, Item it, string noun)
        {
            int n = (int)it.Rar;
            if (it.Kind == ItemKind.Amulet || it.Kind == ItemKind.Ring) n = Math.Max(1, n);
            string suffix = "";
            int lv = it.Level;
            bool[] used = new bool[9];
            for (int i = 0; i < n; i++)
            {
                int a = r.Int(9), guard = 0;
                while (used[a] && guard++ < 20) a = r.Int(9);
                used[a] = true;
                int attr = 1 + r.Int(2 + lv / 2);
                string sfx = "";
                switch (a)
                {
                    case 0: it.Might += attr; sfx = "of the Wolf"; break;
                    case 1: it.Finesse += attr; sfx = "of the Fox"; break;
                    case 2: it.Arcana += attr; sfx = "of the Owl"; break;
                    case 3: it.Vigor += attr; sfx = "of the Bear"; break;
                    case 4: it.HP += 8 + r.Int(6 + lv * 3); sfx = "of the Vale"; break;
                    case 5: it.Res += 6 + r.Int(6 + lv * 2); sfx = "of Vespers"; break;
                    case 6: it.Crit += 2 + r.Int(3 + lv / 3); sfx = "of Embers"; break;
                    case 7: it.Move += 3 + r.Int(4); sfx = "of the Hart"; break;
                    case 8: it.Leech += 1 + r.Int(2 + lv / 4); sfx = "of Thirst"; break;
                }
                if (i == 0) suffix = sfx;
            }
            string prefix = "";
            if (it.Rar == Rarity.Fine) prefix = r.Pick(FinePrefix);
            else if (it.Rar == Rarity.Rare) prefix = r.Pick(RarePrefix);
            else if (it.Rar == Rarity.Relic) prefix = r.Pick(RelicPrefix);
            else if (it.Kind == ItemKind.Weapon || it.Kind == ItemKind.Armor) prefix = r.Pick(new[] { "Worn", "Iron", "Plain", "Old" });
            it.Name = (prefix.Length > 0 ? prefix + " " : "") + noun + (suffix.Length > 0 && it.Rar >= Rarity.Fine ? " " + suffix : "");
        }

        public static Rarity RollRarity(Rng r, int boost)
        {
            // boost: 0 common drop, 1 chest, 2 elite/secret, 3 guaranteed relic
            float x = r.F();
            if (boost >= 3) return Rarity.Relic;
            if (boost == 2) return x < 0.25f ? Rarity.Relic : Rarity.Rare;
            if (boost == 1) return x < 0.06f ? Rarity.Relic : (x < 0.34f ? Rarity.Rare : (x < 0.8f ? Rarity.Fine : Rarity.Common));
            return x < 0.02f ? Rarity.Relic : (x < 0.13f ? Rarity.Rare : (x < 0.45f ? Rarity.Fine : Rarity.Common));
        }

        /// <summary>Random equipment the given class can actually use.</summary>
        public static Item RollEquip(Rng r, int level, ClassId cls, int boost)
        {
            Rarity rar = RollRarity(r, boost);
            level = Math.Max(1, level);
            float k = r.F();
            if (k < 0.42f) return Weapon(r, Classes.Get(cls).WeaponTypes[r.Int(2)], level, rar);
            if (k < 0.74f) return Armor(r, cls, level, rar);
            return Trinket(r, k < 0.87f, level, rar);
        }

        public static Item Quest(string id)
        {
            Item it = new Item();
            it.Kind = ItemKind.Quest; it.QuestId = id; it.Rar = Rarity.Rare;
            switch (id)
            {
                case "sigil": it.Name = "Abbey Sigil"; it.IconOverride = ItemArt.IcSigil; it.Flavor = "A seal of grey stone. The abbey doors answer to it."; break;
                case "moonpetal": it.Name = "Moonpetal"; it.IconOverride = ItemArt.IcMoonpetal; it.Flavor = "Cold to the touch and faintly bright."; break;
                case "lantern": it.Name = "Tobin's Lantern"; it.IconOverride = ItemArt.IcLantern; it.Flavor = "Still warm. He kept it lit to the end."; break;
                case "tongue": it.Name = "The Bell's Tongue"; it.IconOverride = ItemArt.IcTongue; it.Rar = Rarity.Relic; it.Flavor = "The clapper of the Vesper Bell, torn out by the Prior's own hand."; break;
            }
            return it;
        }

        public static Item Starter(ClassId cls, bool weapon)
        {
            Rng r = new Rng(7 + (int)cls);
            Item it;
            if (weapon)
            {
                it = Weapon(r, Classes.Get(cls).WeaponTypes[0], 1, Rarity.Common);
                string[] names = { "Oathguard's Longsword", "Lamp-keeper's Staff", "Pair of Dusk Knives", "Fenwarden's Longbow" };
                it.Name = names[(int)cls];
                it.Flavor = "It has seen you this far.";
            }
            else
            {
                it = Armor(r, cls, 1, Rarity.Common);
                string[] names = { "Dented Hauberk", "Singed Vestment", "Patched Leathers", "Travel-worn Jerkin" };
                it.Name = names[(int)cls];
            }
            return it;
        }

        /// <summary>Named relics handed out by bosses, secrets and quests.</summary>
        public static Item Unique(string id, ClassId cls, int level)
        {
            Rng r = new Rng(id.GetHashCode() ^ ((int)cls * 7919));
            Item it;
            int c = (int)cls;
            switch (id)
            {
                case "boss":
                    it = Weapon(r, Classes.Get(cls).WeaponTypes[0], level, Rarity.Relic);
                    it.Name = new[] { "Vespertine, the Evening Blade", "Cindersong", "The Quiet Pair", "Duskwhisper" }[c];
                    it.Flavor = new[] { "It rings, very softly, when drawn.", "The ember at its head has never gone out.", "They have never once been heard.", "Its string hums a note like a far-off bell." }[c];
                    it.Might = it.Finesse = it.Arcana = 0;
                    if (cls == ClassId.Warrior) it.Might = 6; else if (cls == ClassId.Mage) it.Arcana = 6; else it.Finesse = 6;
                    it.Crit = 8; it.Leech = 3; it.HP = 30; it.Vigor = 3; it.Move = 0; it.Res = 0;
                    break;
                case "vault":
                    it = Armor(r, cls, level, Rarity.Relic);
                    it.Name = new[] { "Orrin's Vigil", "Shroud of the Last Lamp", "Hushwalker's Garb", "Fenwarden's Oathcoat" }[c];
                    it.Flavor = "Laid away beneath the abbey against a darker day. This is that day.";
                    it.Might = it.Finesse = it.Arcana = 0; it.Vigor = 5; it.HP = 45; it.Res = 20; it.Move = 5; it.Crit = 0; it.Leech = 0;
                    break;
                case "cache":
                    it = Trinket(r, false, level, Rarity.Relic);
                    it.Name = "Crown-Shard of the Barrow Kings"; it.Base = 1;
                    it.Flavor = "A splinter of a crown no one living remembers.";
                    it.Might = 4; it.Finesse = 4; it.Arcana = 4; it.Vigor = 4; it.HP = 0; it.Res = 0; it.Crit = 6; it.Move = 0; it.Leech = 0;
                    break;
                case "signet":
                    it = Trinket(r, true, level, Rarity.Rare);
                    it.Name = "Sir Caldus' Signet"; it.Base = 1;
                    it.Flavor = "Cold iron. It tightens when the dead are near.";
                    it.Might = it.Finesse = it.Arcana = 0; it.Vigor = 3; it.HP = 25; it.Crit = 4; it.Leech = 2; it.Move = 0; it.Res = 0;
                    break;
                case "lanterncharm":
                    it = Trinket(r, false, level, Rarity.Rare);
                    it.Name = "Lamplighter's Charm"; it.Base = 0;
                    it.Flavor = "Wren tied the cord herself. 'So you don't get lost too.'";
                    it.Might = it.Finesse = it.Arcana = 2; it.Vigor = 2; it.HP = 20; it.Move = 6; it.Crit = 0; it.Leech = 0; it.Res = 10;
                    break;
                case "sprigring":
                    it = Trinket(r, true, level, Rarity.Fine);
                    it.Name = "Ysolde's Sprig-Ring"; it.Base = 0;
                    it.Flavor = "A band of woven moonpetal stem that never wilts.";
                    it.Might = it.Finesse = it.Arcana = 0; it.Vigor = 3; it.HP = 20; it.Res = 15; it.Crit = 0; it.Move = 0; it.Leech = 0;
                    break;
                case "bramwork":
                    it = Weapon(r, Classes.Get(cls).WeaponTypes[1], level, Rarity.Rare);
                    it.Name = "Bram's Best " + Weapons.Types[it.Base].Name;
                    it.Flavor = "'Bring it back in one piece. Or don't - just bring yourself.'";
                    break;
                default:
                    it = RollEquip(r, level, cls, 2);
                    break;
            }
            it.Unique = true;
            return it;
        }

        // tooltips
        static void Stat(List<string> l, string label, int v, int other, bool hasOther, string unit)
        {
            if (v == 0 && (!hasOther || other == 0)) return;
            string lab = Loc.Tr(label);
            string s = "^g+" + v + unit + " " + lab + "^0";
            if (v == 0) s = "^d+0" + unit + " " + lab + "^0";
            if (hasOther && v != other) s += v > other ? "  ^g(+" + (v - other) + ")^0" : "  ^r(" + (v - other) + ")^0";
            l.Add(s);
        }

        /// <summary>Tooltip lines (with colour codes). 'eq' is the currently equipped item in the same slot, for comparison.</summary>
        public static List<string> Describe(Item it, Item eq, bool showHints, string hint)
        {
            List<string> l = new List<string>();
            l.Add("^" + RarityCode[(int)it.Rar] + it.DisplayName + "^0");
            if (it.Kind == ItemKind.Quest) { l.Add("^dKeepsake^0"); if (it.Count > 1) l.Add(Loc.F("Carried: {0}", it.Count)); if (it.Flavor.Length > 0) l.Add("^d\"" + Loc.Tr(it.Flavor) + "\"^0"); return l; }
            string kind = it.Kind == ItemKind.Weapon ? Weapons.Types[it.Base].Name : it.Kind.ToString();
            l.Add(Loc.F("^d{0} {1}  -  item level {2}^0", Loc.Tr(RarityName[(int)it.Rar]), Loc.Tr(kind), it.Level));
            bool cmp = eq != null && eq != it;
            if (it.Kind == ItemKind.Weapon)
            {
                WeaponType wt = Weapons.Types[it.Base];
                string d = Loc.F("^w{0} - {1} damage^0", it.EffMin, it.EffMax);
                float dps = (it.EffMin + it.EffMax) * 0.5f / wt.Speed;
                if (cmp)
                {
                    float odps = (eq.EffMin + eq.EffMax) * 0.5f / Weapons.Types[eq.Base].Speed;
                    int diff = (int)Math.Round(dps - odps);
                    if (diff != 0) d += diff > 0 ? Loc.F("  ^g(+{0} dps)^0", diff) : Loc.F("  ^r({0} dps)^0", diff);
                }
                l.Add(d);
                l.Add(Loc.F("^d{0} attacks per second^0", (1f / wt.Speed).ToString("0.0", CultureInfo.InvariantCulture)));
            }
            if (it.Kind == ItemKind.Armor) Stat(l, "Armour", it.EffArmor, cmp ? eq.EffArmor : 0, cmp, "");
            Stat(l, "Might", it.Might, cmp ? eq.Might : 0, cmp, "");
            Stat(l, "Finesse", it.Finesse, cmp ? eq.Finesse : 0, cmp, "");
            Stat(l, "Arcana", it.Arcana, cmp ? eq.Arcana : 0, cmp, "");
            Stat(l, "Vigor", it.Vigor, cmp ? eq.Vigor : 0, cmp, "");
            Stat(l, "Health", it.HP, cmp ? eq.HP : 0, cmp, "");
            Stat(l, "Resource", it.Res, cmp ? eq.Res : 0, cmp, "");
            Stat(l, "Critical chance", it.Crit, cmp ? eq.Crit : 0, cmp, "%");
            Stat(l, "Movement speed", it.Move, cmp ? eq.Move : 0, cmp, "%");
            Stat(l, "Life on hit", it.Leech, cmp ? eq.Leech : 0, cmp, "%");
            if (it.Flavor.Length > 0) l.Add("^d\"" + Loc.Tr(it.Flavor) + "\"^0");
            if (showHints)
            {
                l.Add(Loc.F("^dWorth {0} gold^0", it.Value));
                if (hint != null) l.Add("^y" + Loc.Tr(hint) + "^0");
            }
            return l;
        }
    }
}
