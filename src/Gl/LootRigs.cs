using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>
    /// Loot as real objects: every weapon, armour, trinket and quest relic lying in the world is a small model,
    /// centred on its middle and standing up along +Y, instead of a flat icon.
    /// </summary>
    internal static class LootRigs
    {
        static readonly Dictionary<int, MeshB> cache = new Dictionary<int, MeshB>();

        static MeshB Cached(int key, Action<MeshB> build)
        {
            MeshB m;
            if (cache.TryGetValue(key, out m)) return m;
            m = new MeshB(); build(m);
            m.T = Xf.Id; m.Em = 0; m.Layer = 0; m.Smooth = false;
            cache[key] = m;
            return m;
        }

        /// <summary>The model for an item, the transform that stands it up centred on the origin, and its half height.</summary>
        public static MeshB For(Item it, out Xf pose, out float half)
        {
            int tier = M.Clamp(it.Tier, 0, 3);
            pose = Xf.Id; half = 6;
            switch (it.Kind)
            {
                case ItemKind.Weapon: return WeaponDrop(M.Clamp(it.Base, 0, 7), tier, out pose, out half);
                case ItemKind.Armor: half = 7.4f; return Armour(M.Clamp(it.Base, 0, 3), tier);
                case ItemKind.Ring: half = 4.1f; return Ring(it.Base & 1, tier);
                case ItemKind.Amulet: half = 5.8f; return Amulet(it.Base & 1, tier);
                case ItemKind.Potion: pose = Xf.Scale(1.15f) * Xf.Move(0, -3.6f, 0); half = 4.2f; return Rigs.Potion(it.Base == 0);
                default: return Relic(it.Icon, out half);
            }
        }

        // weapons
        static MeshB WeaponDrop(int type, int tier, out Xf pose, out float half)
        {
            // weapons are built along +X with the grip at the origin: blades hang point down, hafted ones stand head up
            float x0, x1, k; bool headUp;
            switch (type)
            {
                case 0: x0 = -4.7f; x1 = 18.2f; k = 0.66f; headUp = false; break;
                case 1: x0 = -6.9f; x1 = 19.8f; k = 0.58f; headUp = true; break;
                case 2: x0 = -12f; x1 = 24.1f; k = 0.44f; headUp = true; break;
                case 3: x0 = -2.5f; x1 = 11.3f; k = 1.0f; headUp = true; break;
                case 4: pose = Xf.Id; half = 5.6f; return DaggerPair(tier);
                case 5: x0 = -2.8f; x1 = 11.6f; k = 0.95f; headUp = false; break;
                default:
                {
                    // bows already stand on end (limbs along Y); shown strung but without an arrow
                    float hb = type == 6 ? 13.5f : 9.8f, kb = type == 6 ? 0.6f : 0.8f;
                    pose = Xf.Scale(kb) * Xf.Move(type == 6 ? 1.05f : 0.45f, 0, 0);
                    half = hb * kb;
                    return Rigs.Bow(hb, tier, 0f, false);
                }
            }
            pose = Xf.Scale(k) * Xf.RotZ(headUp ? M.PI / 2 : -M.PI / 2) * Xf.Move(-(x0 + x1) * 0.5f, 0, 0);
            half = (x1 - x0) * 0.5f * k;
            return Rigs.Weapon(type, tier);
        }

        static MeshB DaggerPair(int tier)
        {
            MeshB d = Rigs.Weapon(4, tier);
            return Cached(7600 + tier, delegate(MeshB m)
            {
                // two daggers crossed point down, as a pair is carried
                for (int s = -1; s <= 1; s += 2)
                    m.Append(d, Xf.Move(0, 0, s * 0.45f) * Xf.RotZ(-M.PI / 2 + s * 0.42f) * Xf.Scale(1.05f) * Xf.Move(-3.1f, 0, 0));
            });
        }

        // armour
        static readonly int[] PlateCol = { 0x8C9299, 0xA9B1BB, 0x98A3B0, 0x51555F };
        static readonly int[] RobeCol = { 0x4A4470, 0x37467E, 0x5B2E70, 0x23264C };
        static readonly int[] GarbCol = { 0x3B3531, 0x2F3B35, 0x3B2C40, 0x1F1F27 };
        static readonly int[] CoatCol = { 0x5C4B31, 0x4B5A33, 0x3B5A45, 0x2F3F2B };
        const int Gold = 0xD8A840, Iron = 0x5E5E68, Leather = 0x4A3226, Glow = 0xB8A0F0;

        static MeshB Armour(int cls, int tier)
        {
            return Cached(7200 + cls * 4 + tier, delegate(MeshB m)
            {
                m.Smooth = true;
                int trim = tier >= 2 ? Gold : Iron;
                switch (cls)
                {
                    case 0: Cuirass(m, tier, trim); break;
                    case 1: Robe(m, tier, trim); break;
                    case 2: Garb(m, tier, trim); break;
                    default: Coat(m, tier, trim); break;
                }
            });
        }

        static void Cuirass(MeshB m, int tier, int trim)
        {
            Xf b = Xf.Move(0, -4.5f, 0);
            m.T = b;
            m.Layer = Mat3.Metal; m.Col = PlateCol[tier];
            // breast and back as one shell from the waist to the collar, flattened front to back
            ELathe(m, new float[] { 0f, 3.7f, 1.4f, 3.9f, 3.6f, 4.5f, 6.0f, 4.95f, 8.2f, 5.1f, 9.4f, 4.7f, 10.4f, 3.6f, 11.0f, 2.3f }, 0.64f, 18, false, false);
            // the gorget closes the collar
            ELathe(m, new float[] { 10.6f, 2.65f, 11.5f, 2.4f, 11.9f, 2.0f }, 0.8f, 14, false, true);
            // fauld: three lames lapping downwards
            for (int i = 0; i < 3; i++)
            {
                float y = -i * 0.95f;
                ELathe(m, new float[] { y - 1.05f, 4.05f + i * 0.22f, y + 0.05f, 3.78f + i * 0.2f }, 0.68f, 18, true, false);
            }
            // pauldrons, two lames each
            for (int s = -1; s <= 1; s += 2)
            {
                m.Blob(s * 4.6f, 9.5f, 0, 2.5f, 1.7f, 2.4f, 1, 0, 0);
                m.Blob(s * 5.1f, 8.3f, 0, 2.3f, 1.3f, 2.2f, 1, 0, 0);
            }
            // a band and rivets at the waist, rivets on each pauldron
            m.Col = trim;
            ELathe(m, new float[] { 0.15f, 3.86f, 0.75f, 3.93f }, 0.66f, 18, false, false);
            for (int i = 0; i < 10; i++)
            {
                float a = i * M.TAU / 10;
                m.Blob(M.Cos(a) * 3.95f, 0.45f, M.Sin(a) * 3.95f * 0.67f, 0.26f, 0.26f, 0.26f, 0, 0, 0);
            }
            for (int s = -1; s <= 1; s += 2)
                for (int i = -1; i <= 1; i++)
                    m.Blob(s * 4.75f, 11.0f, i * 1.2f, 0.25f, 0.25f, 0.25f, 0, 0, 0);
            if (tier == 3)
            {
                // a line of witch-light across the breast
                m.Layer = 0; m.Col = Glow; m.Em = 210;
                ELathe(m, new float[] { 6.9f, 5.03f, 7.25f, 5.06f }, 0.645f, 18, false, false);
                m.Em = 0;
            }
        }

        static void Robe(MeshB m, int tier, int trim)
        {
            Xf b = Xf.Move(0, -0.4f, 0);
            m.T = b;
            int cloth = RobeCol[tier];
            m.Layer = Mat3.Cloth; m.Col = cloth;
            // a long robe from the hem to the shoulders, flared a little at the foot
            ELathe(m, new float[] { -6.6f, 5.3f, -3.8f, 4.6f, -0.9f, 3.8f, 1.8f, 4.1f, 4.4f, 4.5f, 5.6f, 3.9f, 6.3f, 2.4f, 6.6f, 1.6f }, 0.78f, 18, true, true);
            // the hood lies back between the shoulders
            m.Blob(0, 5.9f, 2.0f, 2.7f, 2.2f, 2.1f, 1, 3, 0.05f);
            // bell sleeves hanging from the shoulders
            for (int s = -1; s <= 1; s += 2)
            {
                m.T = b * Xf.Move(s * 4.4f, 5.0f, 0) * Xf.RotZ(s * 0.16f);
                m.Layer = Mat3.Cloth; m.Col = cloth;
                m.Cyl(0, 0, -6.4f, 0, 1.95f, 1.25f, 12, false, false, 0);
                m.Col = trim;
                m.Cyl(0, 0, -6.6f, -5.9f, 2.02f, 1.98f, 12, false, false, 0);
                m.Layer = 0; m.Col = 0x120E16;
                m.Cyl(0, 0, -6.55f, -6.55f, 1.95f, 0, 12, false, false, 0);
            }
            m.T = b;
            // hem and sash
            m.Layer = Mat3.Cloth; m.Col = trim;
            ELathe(m, new float[] { -6.62f, 5.34f, -5.9f, 5.12f }, 0.785f, 18, false, false);
            ELathe(m, new float[] { -1.0f, 3.86f, -0.2f, 3.86f }, 0.79f, 18, false, false);
            m.BoxC(0.9f, -2.2f, -3.15f, 0.7f, 3.2f, 0.25f);
            if (tier == 3)
            {
                m.Layer = 0; m.Col = Glow; m.Em = 210;
                ELathe(m, new float[] { -4.75f, 4.84f, -4.45f, 4.75f }, 0.785f, 18, false, false);
                m.Em = 0;
            }
        }

        static void Garb(MeshB m, int tier, int trim)
        {
            Xf b = Xf.Move(0, -4.6f, 0);
            m.T = b;
            int cloth = GarbCol[tier];
            m.Layer = Mat3.Cloth; m.Col = cloth;
            // a fitted jerkin cut long at the hips
            ELathe(m, new float[] { -2.4f, 3.95f, 0f, 3.6f, 2.8f, 3.9f, 6.0f, 4.55f, 8.4f, 4.7f, 9.6f, 4.3f, 10.6f, 3.0f, 11.2f, 2.1f }, 0.62f, 18, true, false);
            // a cowl gathered round the neck
            m.Col = Mul(cloth, 0.85f);
            ELathe(m, new float[] { 9.9f, 3.5f, 10.9f, 3.3f, 11.8f, 2.5f, 12.4f, 1.3f }, 0.86f, 16, false, true);
            // two belts with iron buckles, and a pouch on each hip
            m.Col = Leather;
            ELathe(m, new float[] { 0.3f, 3.68f, 1.0f, 3.74f }, 0.64f, 18, false, false);
            ELathe(m, new float[] { 4.4f, 4.28f, 5.0f, 4.38f }, 0.64f, 18, false, false);
            m.BoxC(-3.3f, 0.2f, -0.9f, 1.3f, 1.6f, 1.2f);
            m.BoxC(3.3f, 0.2f, 0.9f, 1.3f, 1.6f, 1.2f);
            m.Layer = Mat3.Metal; m.Col = trim;
            m.BoxC(0, 0.65f, -2.4f, 1.0f, 0.9f, 0.3f);
            m.BoxC(0, 4.7f, -2.8f, 1.0f, 0.9f, 0.3f);
            if (tier == 3)
            {
                m.Layer = 0; m.Col = Glow; m.Em = 200;
                ELathe(m, new float[] { 7.2f, 4.65f, 7.45f, 4.67f }, 0.625f, 18, false, false);
                m.Em = 0;
            }
        }

        static void Coat(MeshB m, int tier, int trim)
        {
            Xf b = Xf.Move(0, -3.5f, 0);
            m.T = b;
            int cloth = CoatCol[tier];
            m.Layer = Mat3.Cloth; m.Col = cloth;
            // a hunting coat with skirts to mid thigh
            ELathe(m, new float[] { -4.2f, 4.6f, -1.6f, 4.0f, 0.4f, 3.7f, 3.2f, 4.0f, 6.2f, 4.6f, 8.4f, 4.75f, 9.6f, 4.35f, 10.6f, 3.0f, 11.2f, 2.1f }, 0.66f, 18, true, true);
            // a ruff of fur at the collar
            m.Layer = Mat3.Fur; m.Col = tier == 3 ? 0x8A8A96 : 0x8A7458;
            for (int i = 0; i < 9; i++)
            {
                float a = i * M.TAU / 9;
                m.Blob(M.Cos(a) * 2.7f, 10.8f, M.Sin(a) * 2.2f, 1.5f, 1.1f, 1.5f, 1, (uint)(31 + i), 0.12f);
            }
            // belt and buckle
            m.Layer = Mat3.Cloth; m.Col = Leather;
            ELathe(m, new float[] { 0.0f, 3.74f, 0.9f, 3.78f }, 0.68f, 18, false, false);
            m.Layer = Mat3.Metal; m.Col = trim;
            m.BoxC(0, 0.45f, -2.6f, 1.1f, 1.0f, 0.3f);
            // a quiver slung across the back, fletching showing
            m.T = b * Xf.Move(1.1f, 5.8f, 3.3f) * Xf.RotZ(-0.45f);
            m.Layer = Mat3.Cloth; m.Col = Leather;
            m.Cyl(0, 0, -4.2f, 4.2f, 1.15f, 1.3f, 12, false, true, 0);
            m.Layer = 0; m.Col = 0xD8D0C0;
            for (int i = 0; i < 3; i++) m.BoxC(-0.5f + i * 0.5f, 5.0f, (i - 1) * 0.3f, 0.12f, 1.6f, 0.7f);
            m.Col = 0x8A6A40;
            for (int i = 0; i < 3; i++) m.BoxC(-0.5f + i * 0.5f, 4.3f, (i - 1) * 0.3f, 0.2f, 0.4f, 0.2f);
            m.T = b;
            if (tier == 3)
            {
                m.Layer = 0; m.Col = Glow; m.Em = 200;
                ELathe(m, new float[] { -3.2f, 4.42f, -2.95f, 4.36f }, 0.665f, 18, false, false);
                m.Em = 0;
            }
        }

        // rings and amulets (shown large enough to read)
        static readonly int[] GemCol = { 0x9AB4D0, 0x6FD86B, 0x5F9CFF, 0xFF8A30 };

        static int BandCol(int tier, bool iron)
        {
            if (tier == 3) return 0xD8C8F8;
            if (iron) return tier >= 2 ? 0x9098A4 : 0xA8A8B0;
            return tier >= 1 ? 0xE0B850 : 0xC0C4CC;
        }

        static MeshB Ring(int signet, int tier)
        {
            return Cached(7400 + signet * 4 + tier, delegate(MeshB m)
            {
                m.Smooth = true;
                m.T = Xf.Move(0, -0.7f, 0);
                m.Layer = Mat3.Metal; m.Col = BandCol(tier, signet == 1);
                // the band stands on edge, the stone or the seal on top
                Torus(m, 2.7f, 2.7f, 0.55f, 22, 8);
                if (signet == 0)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float a = M.PI / 4 + i * M.PI / 2;
                        m.Blob(M.Cos(a) * 0.78f, 3.45f, M.Sin(a) * 0.78f, 0.22f, 0.55f, 0.22f, 0, 0, 0);
                    }
                    m.Smooth = false; m.Layer = 0; m.Col = GemCol[tier]; m.Em = 120;
                    m.Blob(0, 3.75f, 0, 1.05f, 0.85f, 1.05f, 0, 5, 0);
                }
                else
                {
                    m.Cyl(0, 0, 2.9f, 3.6f, 1.5f, 1.6f, 16, true, false, 0);
                    m.Layer = 0; m.Col = tier == 3 ? 0x5A2E8A : 0x3A1E22; m.Em = (byte)(tier == 3 ? 120 : 25);
                    m.Cyl(0, 0, 3.6f, 3.68f, 1.15f, 1.15f, 16, true, false, 0);
                }
                m.Em = 0;
            });
        }

        static MeshB Amulet(int charm, int tier)
        {
            return Cached(7500 + charm * 4 + tier, delegate(MeshB m)
            {
                m.Smooth = true;
                if (charm == 0)
                {
                    // a fine chain, the bail, and a pendant with its stone
                    Xf b = Xf.Move(0, 2.15f, 0);
                    m.T = b;
                    m.Layer = Mat3.Metal; m.Col = BandCol(tier, false);
                    Torus(m, 2.4f, 3.6f, 0.2f, 26, 5);
                    m.T = b * Xf.Move(0, -3.85f, 0) * Xf.Yaw(M.PI / 2);
                    Torus(m, 0.5f, 0.5f, 0.17f, 10, 5);
                    m.T = b * Xf.Move(0, -5.9f, 0) * Xf.RotX(M.PI / 2);
                    m.Cyl(0, 0, -0.35f, 0.35f, 2.0f, 2.0f, 18, true, true, 0);
                    m.T = b * Xf.Move(0, -5.9f, 0);
                    Torus(m, 2.0f, 2.0f, 0.3f, 20, 6);
                    m.Smooth = false; m.Layer = 0; m.Col = GemCol[tier]; m.Em = 130;
                    m.Blob(0, 0, 0, 1.15f, 1.15f, 0.75f, 0, 9, 0);
                }
                else
                {
                    // a cord with a carved bone crescent, a bead and two black feathers
                    Xf b = Xf.Move(0, 3.05f, 0);
                    m.T = b;
                    m.Layer = Mat3.Cloth; m.Col = Leather;
                    Torus(m, 2.0f, 2.6f, 0.16f, 22, 5);
                    m.T = b * Xf.Move(0, -4.6f, 0);
                    m.Layer = Mat3.Bone; m.Col = tier == 3 ? 0xE8E0F8 : 0xD8CCAA;
                    for (int i = 0; i <= 10; i++)
                    {
                        float t = i / 10f, a = M.PI * (1.08f + 0.84f * t), r = 0.22f + 0.75f * M.Sin(M.PI * t);
                        m.Blob(M.Cos(a) * 2.1f, M.Sin(a) * 2.1f, 0, r, r, r * 0.55f, 0, 0, 0);
                    }
                    m.Layer = 0; m.Col = GemCol[tier]; m.Em = 120;
                    m.Blob(0, -0.3f, 0, 0.62f, 0.62f, 0.62f, 1, 0, 0);
                    m.Em = 0; m.Col = 0x26262C;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        m.T = b * Xf.Move(s * 0.9f, -7.4f, 0) * Xf.RotZ(s * 0.18f);
                        m.Blob(0, 0, 0, 0.45f, 1.4f, 0.1f, 0, 0, 0);
                    }
                }
                m.Em = 0;
            });
        }

        // quest relics
        static MeshB Relic(int icon, out float half)
        {
            switch (icon)
            {
                case ItemArt.IcMoonpetal: half = 5.4f; return Cached(7700, Moonpetal);
                case ItemArt.IcLantern: half = 5.9f; return Cached(7701, Lantern);
                case ItemArt.IcTongue: half = 7.9f; return Cached(7702, Tongue);
                default: half = 5.0f; return Cached(7703, Sigil);
            }
        }

        static void Sigil(MeshB m)
        {
            // a seal of grey stone with a bell cut into both faces, the cuts full of pale light
            m.Smooth = true;
            m.T = Xf.RotX(M.PI / 2);
            m.Layer = Mat3.Rock; m.Col = 0x9A9890;
            m.Cyl(0, 0, -0.7f, 0.7f, 4.4f, 4.4f, 28, true, true, 0);
            m.T = Xf.Id;
            m.Col = 0x86847C;
            Torus(m, 4.4f, 4.4f, 0.55f, 30, 8);
            m.Layer = 0; m.Col = 0xBFD4FF; m.Em = 230;
            float[] bell = { -1.9f, -1.8f, -1.3f, 0.9f, -0.6f, 1.9f, 0.6f, 1.9f, 1.3f, 0.9f, 1.9f, -1.8f };
            V3 nz = new V3(0, 0, 1);
            for (int f = -1; f <= 1; f += 2)
            {
                float z = f * 0.74f;
                for (int i = 0; i + 3 < bell.Length; i += 2)
                    Rigs.Strip(m, new V3(bell[i], bell[i + 1] - 0.3f, z), new V3(bell[i + 2], bell[i + 3] - 0.3f, z), 0.34f, nz);
                Rigs.Strip(m, new V3(-2.4f, -2.1f, z), new V3(2.4f, -2.1f, z), 0.34f, nz);
                m.Blob(0, -2.8f, z, 0.42f, 0.42f, 0.1f, 0, 0, 0);
                for (int k = 0; k < 12; k++)
                {
                    float a = k * M.TAU / 12;
                    Rigs.Strip(m, new V3(M.Cos(a) * 3.25f, M.Sin(a) * 3.25f, z), new V3(M.Cos(a) * 3.85f, M.Sin(a) * 3.85f, z), 0.28f, nz);
                }
            }
            m.Em = 0;
        }

        static void Moonpetal(MeshB m)
        {
            // a pale flower on a short stem with two leaves, faintly alight
            m.Smooth = true;
            Xf b = Xf.Scale(1.3f) * Xf.Move(0, 0.4f, 0);
            m.T = b;
            m.Layer = 0; m.Col = 0x4E6E44;
            m.Cyl(0, 0, -4.4f, 2.3f, 0.28f, 0.2f, 7, false, true, 0);
            m.Col = 0x5A8A4A;
            for (int s = -1; s <= 1; s += 2)
            {
                m.T = b * Xf.Move(s * 1.25f, -2.1f, 0) * Xf.RotZ(s * 0.55f);
                m.Blob(0, 0, 0, 1.6f, 0.28f, 0.72f, 1, 0, 0);
            }
            m.Col = 0xDCE6FF; m.Em = 150;
            for (int i = 0; i < 6; i++)
            {
                m.T = b * Xf.Move(0, 2.4f, 0) * Xf.Yaw(i * M.TAU / 6) * Xf.RotZ(0.55f) * Xf.Move(1.45f, 0, 0);
                m.Blob(0, 0, 0, 1.6f, 0.26f, 0.82f, 1, 0, 0);
            }
            m.T = b;
            m.Col = 0xFFF2C0; m.Em = 230;
            m.Blob(0, 2.55f, 0, 0.7f, 0.55f, 0.7f, 1, 0, 0);
            m.Em = 0;
        }

        static void Lantern(MeshB m)
        {
            // a hooded iron lantern, still lit
            m.Smooth = true;
            Xf b = Xf.Move(0, -0.45f, 0);
            m.T = b;
            m.Layer = Mat3.Metal; m.Col = 0x4A4A52;
            m.Cyl(0, 0, -5.4f, -4.6f, 2.5f, 2.4f, 14, true, true, 0);
            m.Layer = 0; m.Col = 0xFFC468; m.Em = 215;
            m.Cyl(0, 0, -4.6f, 1.2f, 1.95f, 1.95f, 14, false, false, 0);
            m.Em = 0; m.Layer = Mat3.Metal; m.Col = 0x4A4A52;
            for (int i = 0; i < 4; i++)
            {
                float a = M.PI / 4 + i * M.PI / 2;
                m.BoxC(M.Cos(a) * 2.05f, -1.7f, M.Sin(a) * 2.05f, 0.5f, 5.9f, 0.5f);
            }
            m.Cyl(0, 0, 1.2f, 1.9f, 2.5f, 2.4f, 14, false, true, 0);
            m.Cyl(0, 0, 1.9f, 3.6f, 2.4f, 0.7f, 14, false, false, 0);
            m.Cyl(0, 0, 3.6f, 4.1f, 0.7f, 0.6f, 10, true, false, 0);
            m.T = b * Xf.Move(0, 5.1f, 0);
            Torus(m, 1.2f, 1.0f, 0.22f, 16, 6);
        }

        static void Tongue(MeshB m)
        {
            // the bell's clapper: a bronze bob on an iron shank, with the loop it hung from
            m.Smooth = true;
            Xf b = Xf.Scale(0.85f) * Xf.Move(0, -1.33f, 0);
            m.T = b;
            m.Layer = Mat3.Metal; m.Col = 0x6A6258;
            m.Cyl(0, 0, -3.0f, 7.4f, 0.62f, 0.5f, 12, false, false, 0);
            m.Cyl(0, 0, 6.9f, 7.6f, 0.9f, 0.9f, 12, true, true, 0);
            m.Col = 0xA88452;
            m.Lathe(0, 0, new float[] { -8.0f, 0f, -7.7f, 1.3f, -6.9f, 2.25f, -5.6f, 2.6f, -4.2f, 2.2f, -3.1f, 1.3f, -2.5f, 0.7f }, 18, false, 0);
            m.Layer = 0; m.Col = 0xC0A0FF; m.Em = 220;
            m.Cyl(0, 0, -5.75f, -5.35f, 2.64f, 2.62f, 18, false, false, 0);
            m.Em = 0;
            m.Layer = Mat3.Metal; m.Col = 0x6A6258;
            m.T = b * Xf.Move(0, 8.9f, 0);
            Torus(m, 1.35f, 1.35f, 0.42f, 18, 8);
        }

        // shapes
        /// <summary>A lathed shape squashed front to back (depth zk) with true normals; prof holds (y, radius) pairs from the bottom up.</summary>
        static void ELathe(MeshB m, float[] prof, float zk, int sides, bool capBottom, bool capTop)
        {
            int n = prof.Length / 2;
            for (int k = 0; k + 1 < n; k++)
            {
                float y0 = prof[k * 2], r0 = prof[k * 2 + 1], y1 = prof[k * 2 + 2], r1 = prof[k * 2 + 3];
                float sl = (r0 - r1) / Math.Max(1e-4f, y1 - y0);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * M.TAU / sides, a1 = (i + 1) * M.TAU / sides;
                    float c0 = M.Cos(a0), s0 = M.Sin(a0), c1 = M.Cos(a1), s1 = M.Sin(a1);
                    V3 p0 = new V3(c0 * r0, y0, s0 * r0 * zk), p1 = new V3(c1 * r0, y0, s1 * r0 * zk);
                    V3 q0 = new V3(c0 * r1, y1, s0 * r1 * zk), q1 = new V3(c1 * r1, y1, s1 * r1 * zk);
                    V3 n0 = new V3(c0 * zk, sl * zk, s0), n1 = new V3(c1 * zk, sl * zk, s1);
                    m.TriN(p1, p0, q0, n1, n0, n0);
                    m.TriN(p1, q0, q1, n1, n0, n1);
                }
            }
            if (capBottom) Cap(m, prof[0], prof[1], zk, sides, false);
            if (capTop) Cap(m, prof[n * 2 - 2], prof[n * 2 - 1], zk, sides, true);
        }

        static void Cap(MeshB m, float y, float r, float zk, int sides, bool up)
        {
            V3 c = new V3(0, y, 0);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * M.TAU / sides, a1 = (i + 1) * M.TAU / sides;
                V3 p0 = new V3(M.Cos(a0) * r, y, M.Sin(a0) * r * zk), p1 = new V3(M.Cos(a1) * r, y, M.Sin(a1) * r * zk);
                if (up) m.Tri(c, p1, p0); else m.Tri(c, p0, p1);
            }
        }

        /// <summary>A ring standing in the XY plane: rx, ry the radii of its centre line, r the thickness of the tube.</summary>
        static void Torus(MeshB m, float rx, float ry, float r, int seg, int sides)
        {
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < sides; j++)
                {
                    V3 n00, n10, n11, n01;
                    V3 p00 = TorusAt(rx, ry, r, i, j, seg, sides, out n00), p10 = TorusAt(rx, ry, r, i + 1, j, seg, sides, out n10);
                    V3 p11 = TorusAt(rx, ry, r, i + 1, j + 1, seg, sides, out n11), p01 = TorusAt(rx, ry, r, i, j + 1, seg, sides, out n01);
                    m.TriN(p00, p10, p11, n00, n10, n11);
                    m.TriN(p00, p11, p01, n00, n11, n01);
                }
        }

        static V3 TorusAt(float rx, float ry, float r, int i, int j, int seg, int sides, out V3 n)
        {
            float a = i * M.TAU / seg, b = j * M.TAU / sides;
            float ca = M.Cos(a), sa = M.Sin(a);
            V3 o = new V3(ry * ca, rx * sa, 0).Norm;
            n = o * M.Cos(b) + new V3(0, 0, M.Sin(b));
            return new V3(rx * ca, ry * sa, 0) + n * r;
        }

        static int Mul(int c, float k)
        {
            int r = M.Clamp((int)(((c >> 16) & 255) * k), 0, 255), g = M.Clamp((int)(((c >> 8) & 255) * k), 0, 255), b = M.Clamp((int)((c & 255) * k), 0, 255);
            return (r << 16) | (g << 8) | b;
        }
    }
}
