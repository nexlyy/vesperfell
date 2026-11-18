using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>Anything that stands in the world and can be hit.</summary>
    public abstract class Actor
    {
        public float X, Y;              // feet position
        public float KX, KY;            // knockback velocity
        public float Radius = 5;
        public float HP, MaxHP;
        public float Z;                 // hover height (visual)
        public bool Dead;
        public int Facing = 1;
        public float AnimT; public bool Moving;
        public float FlashT;
        public float StunT, RootT, SlowT, SlowAmt, BurnT, BurnDps, PoisonT, PoisonDps, BleedT, BleedDps, MarkT, FreezeT, DotAcc;
        public ActorSprites Spr;
        public string Name = "";

        public bool Incapacitated { get { return StunT > 0 || FreezeT > 0; } }

        public Sprite Frame()
        {
            if (Spr == null) return null;
            if (Moving) return Spr.Run[(int)(AnimT * 9f) & 3];
            return Spr.Idle[(int)(AnimT * 1.6f) & 1];
        }

        public void ClearStatus()
        {
            StunT = RootT = SlowT = BurnT = PoisonT = BleedT = MarkT = FreezeT = 0; KX = KY = 0;
        }
    }

    public sealed class Player : Actor
    {
        public ClassId Class; public ClassDef Def;
        public int Level = 1, XP, Gold, AttrPoints;
        public int Might, Finesse, Arcana, Vigor;         // base attributes, including spent points
        public float Res, MaxRes;
        public const int SlotWeapon = 0, SlotArmor = 1, SlotAmulet = 2, SlotRing = 3, BagSize = 30;
        public Item[] Equip = new Item[4];
        public Item[] Bag = new Item[BagSize];
        public List<Item> Keepsakes = new List<Item>();
        public int HealthPotions, SpiritPotions;

        // derived
        public int TMight, TFinesse, TArcana, TVigor, ArmorRating, WMin, WMax;
        public float CritChance, CritMult, MoveSpeed, DmgMult, Leech, CdMult = 1, AtkInterval = 0.4f, ResRegen, HpRegen;

        // action state
        public float AimAngle;
        public float[] Cd = new float[5];
        public float AtkT, AtkDur, AtkCd; public int Combo, SwingStep; public float ComboT; public float SwingFrom, SwingTo; public bool SwingHit;
        public bool Blocking; public float BlockT;
        public float DodgeT, DodgeDX, DodgeDY; public int DodgeCharges = 1; public float DodgeRecharge; public float IFrames;
        public float DashT, DashDur, DashDX, DashDY; public int DashKind;
        public List<Actor> DashHit = new List<Actor>();
        public float WhirlT; public int WhirlHits; public float OathT; public float StealthT; public bool NextCrit; public float NextCritT;
        public float PotionCd, HealT, HealPerSec;
        public float CombatT;            // seconds since last dealt/taken damage
        public float StepAcc; public float HurtT; public float CastT; public float BowT;
        public float RespawnX, RespawnY; public string RespawnMap = "overworld";
        public float FuryDecayT;
        public int Kills, Deaths;
        // co-op
        public int NetId, Slot; public string NetName = ""; public bool Remote;
        public float NetX, NetY, DeadT;

        public int XpToNext { get { return XpFor(Level); } }
        public static int XpFor(int level) { return 55 + 40 * (level - 1) + 12 * (level - 1) * (level - 1); }
        public const int MaxLevel = 15;

        public Item Weapon { get { return Equip[SlotWeapon]; } }
        public WeaponType WType { get { return Weapons.Types[Equip[SlotWeapon] != null ? Equip[SlotWeapon].Base : Def.WeaponTypes[0]]; } }

        public static int SlotFor(Item it)
        {
            switch (it.Kind)
            {
                case ItemKind.Weapon: return SlotWeapon;
                case ItemKind.Armor: return SlotArmor;
                case ItemKind.Amulet: return SlotAmulet;
                case ItemKind.Ring: return SlotRing;
            }
            return -1;
        }

        public bool CanUse(Item it)
        {
            if (it.Kind == ItemKind.Weapon) return it.Base == Def.WeaponTypes[0] || it.Base == Def.WeaponTypes[1];
            if (it.Kind == ItemKind.Armor) return it.Base == (int)Class;
            return it.Kind == ItemKind.Amulet || it.Kind == ItemKind.Ring;
        }

        public int FreeBagSlot() { for (int i = 0; i < Bag.Length; i++) if (Bag[i] == null) return i; return -1; }

        public bool AddToBag(Item it)
        {
            int s = FreeBagSlot();
            if (s < 0) return false;
            Bag[s] = it; return true;
        }

        public int KeepsakeCount(string id)
        {
            foreach (Item k in Keepsakes) if (k.QuestId == id) return k.Count;
            return 0;
        }

        public void AddKeepsake(string id, int n)
        {
            foreach (Item k in Keepsakes) if (k.QuestId == id) { k.Count += n; return; }
            Item it = Items.Quest(id); it.Count = n; Keepsakes.Add(it);
        }

        public void RemoveKeepsake(string id)
        {
            for (int i = Keepsakes.Count - 1; i >= 0; i--) if (Keepsakes[i].QuestId == id) Keepsakes.RemoveAt(i);
        }

        /// <summary>Rebuilds every derived number from class, level, attributes and gear.</summary>
        public void Recalc(bool refill)
        {
            int m = Might, f = Finesse, a = Arcana, v = Vigor, hp = 0, res = 0, crit = 0, move = 0, leech = 0, armor = Def.BaseArmor;
            for (int i = 0; i < Equip.Length; i++)
            {
                Item it = Equip[i];
                if (it == null) continue;
                m += it.Might; f += it.Finesse; a += it.Arcana; v += it.Vigor; hp += it.HP; res += it.Res; crit += it.Crit; move += it.Move; leech += it.Leech;
                armor += it.EffArmor;
            }
            TMight = m; TFinesse = f; TArcana = a; TVigor = v;
            float oldMax = MaxHP, oldRes = MaxRes;
            MaxHP = Def.BaseHP + v * 8 + (Level - 1) * Def.HPPerLevel + hp;
            MaxRes = Def.BaseRes + res + (Class == ClassId.Mage ? a * 3 : 0);
            ArmorRating = armor + m / 2;
            int primary = Def.Primary == 0 ? m : (Def.Primary == 1 ? f : a);
            DmgMult = 1f + primary * 0.02f + (Def.Primary != 0 ? m * 0.004f : 0f);
            CritChance = Def.Crit + f * 0.005f + crit * 0.01f;
            if (CritChance > 0.75f) CritChance = 0.75f;
            CritMult = Def.CritMult;
            MoveSpeed = Def.MoveSpeed * (1f + move * 0.01f);
            Leech = leech * 0.01f;
            CdMult = Math.Max(0.65f, 1f - a * 0.006f);
            ResRegen = Def.ResRegen + (Class == ClassId.Mage ? a * 0.14f : (Class == ClassId.Warrior ? 0 : a * 0.08f));
            HpRegen = 0.2f + v * 0.05f;
            Item w = Equip[SlotWeapon];
            if (w != null) { WMin = w.EffMin; WMax = w.EffMax; AtkInterval = Weapons.Types[w.Base].Speed / (1f + f * 0.004f); }
            else { WMin = 3; WMax = 5; AtkInterval = Weapons.Types[Def.WeaponTypes[0]].Speed; }
            if (refill) { HP = MaxHP; Res = Class == ClassId.Warrior ? 0 : MaxRes; }
            else
            {
                if (MaxHP > oldMax && oldMax > 0) HP += MaxHP - oldMax;
                if (HP > MaxHP) HP = MaxHP;
                if (Res > MaxRes) Res = MaxRes;
            }
        }

        public float ArmorReduction { get { return ArmorRating / (ArmorRating + 60f); } }
    }

    public sealed class Enemy : Actor
    {
        public const int Idle = 0, Alert = 1, Chase = 2, Windup = 3, Strike = 4, Recover = 5, Return = 6, Search = 7, Special = 8, Vanish = 9;

        public EnemyDef Def; public EnemyKind Kind; public int Level; public SpawnDef Spawn;
        public float HomeX, HomeY;
        public int State; public float StateT, StateDur;
        public float AtkCd, AtkCd2, AtkCd3, SpecialCd;
        public float DirX, DirY, TX, TY, AttackAng;
        public int AttackKind;
        public bool HitDone;
        public float Dmg, Speed;
        public float WanderT, StrafeDir = 1, StrafeT, LurchT;
        public int Phase, Counter, Variant;
        public float DeathT, HpBarT, AlertShow, FadeIn = 1f;
        public bool Aggro;
        public float StuckT, LastX, LastY;
        public float WeaponSwing;
        public List<Enemy> Minions;
        // co-op
        public int NetId; public Player Target; public float NetX, NetY, NetSeen; public bool Awake, NetMoving;

        public void SetState(int s, float dur) { State = s; StateT = 0; StateDur = dur; HitDone = false; }
    }

    public sealed class Npc : Actor
    {
        public string Id = "";
        public float HomeX, HomeY, TX, TY, WanderT;
        public bool Wander;
        public float TalkBob;
    }

    public sealed class Projectile
    {
        public const int KArrow = 0, KPierce = 1, KKnife = 2, KEmber = 3, KFireball = 4, KPoisonKnife = 5, KMarked = 6,
            KBone = 10, KGloam = 11, KGlob = 12, KWispOrb = 13;

        public int Kind;
        public float X, Y, VX, VY, Life, Radius = 3, Dmg, Angle;
        public bool FromPlayer, Pierce, Crit, Dead;
        public Sprite Spr;
        public List<Actor> Hit;
        public Actor Target; public float Turn;
        public int LightColor; public float LightR;
        public float Z, VZ; public bool Lob; public float TXg, TYg;
        public float Knock = 60;
        public float TrailAcc;
        public float Mult = 1f;         // ability multiplier label (for crits / ambush)
        public bool Ambush;
        public bool Ghost; public int OwnerId = -1;      // Ghost: another hero's shot - drawn here, decided on their machine
    }

    public sealed class Area
    {
        public const int Trap = 0, Rain = 1, Pool = 2, Sigil = 3, Ring = 4, Bolt = 5, Burst = 6;

        public int Kind;
        public float X, Y, R, T, Dur, Delay, TickT, Dmg;
        public bool FromPlayer, Done, Armed;
        public int Color;
        public float X2, Y2;            // lightning segment end
        public int Seed;
        public bool Ghost;               // another hero's effect - drawn here, decided on their machine
    }

    public sealed class Pickup
    {
        public const int Gold = 0, Health = 1, Spirit = 2, Equip = 3, Quest = 4;
        public int Kind, Amount;
        public Item It;
        public float X, Y, VX, VY, Z, VZ, T;
        public bool Dead;
    }

    public sealed class Floater
    {
        public float X, Y, T, Dur = 0.9f, VX, VY = -26;
        public string Text; public int Color; public bool Big;
    }

    public struct Particle
    {
        public float X, Y, VX, VY, Z, VZ, Life, Max, Size, Drag;
        public int Color, Color2;
        public byte Kind;       // 0 pixel, 1 soft glow (additive), 2 smoke (alpha), 3 square 2px
        public bool Grav, Lit;  // Lit = drawn before lighting (affected by darkness)
    }
}
