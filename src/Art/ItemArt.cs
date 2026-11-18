using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>Held weapons (drawn pointing right, origin at the grip), projectiles, inventory icons and skill icons.</summary>
    public static class ItemArt
    {
        // metal ramps per rarity tier: dark, mid, light, trim, gem
        static readonly int[][] Tier =
        {
            new[]{ Pal.St2, Pal.St3, Pal.St4, Pal.Ea3, Pal.St3 },
            new[]{ Pal.St3, Pal.St4, Pal.St6, Pal.Go2, Pal.Lf4 },
            new[]{ Pal.Fr1, Pal.Fr2, Pal.Fr4, Pal.St5, Pal.Fr3 },
            new[]{ Pal.Em4, Pal.Em5, Pal.Em7, Pal.Go3, Pal.Cr3 },
        };
        static int[] T4(int tier) { return Tier[M.Clamp(tier, 0, 3)]; }
        static int Edge(int tier, int c) { return tier >= 3 ? Col.E(c) : c; }

        // held weapons
        public static Sprite Sword(int tier)
        {
            int[] t = T4(tier);
            Cv cv = new Cv(22, 9);
            cv.Rect(1, 4, 4, 1, Pal.Ea3); cv.Set(0, 4, t[3]);
            cv.Rect(5, 1, 1, 7, t[3]); cv.Set(5, 1, Col.Shade(t[3], 1));
            cv.Rect(6, 3, 12, 1, Edge(tier, t[2])); cv.Rect(6, 4, 13, 1, t[1]); cv.Rect(6, 5, 12, 1, t[0]);
            cv.Set(19, 4, Edge(tier, t[2]));
            if (tier >= 2) cv.Set(5, 4, Col.E(t[4]));
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(3, 4);
        }

        public static Sprite Axe(int tier)
        {
            int[] t = T4(tier);
            Cv cv = new Cv(24, 15);
            cv.Rect(1, 7, 19, 1, Pal.Ea4); cv.Rect(1, 8, 19, 1, Pal.Ea2);
            cv.Poly(t[1], 13, 7, 15, 1, 21, 2, 22, 7);
            cv.Poly(t[0], 13, 8, 22, 8, 21, 13, 15, 14);
            cv.Line(15, 1, 21, 2, Edge(tier, t[2])); cv.Line(21, 2, 22, 7, Edge(tier, t[2])); cv.Line(22, 8, 21, 13, Edge(tier, t[2]));
            cv.Rect(13, 6, 2, 3, t[3]);
            if (tier >= 2) cv.Set(17, 7, Col.E(t[4]));
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(4, 7);
        }

        public static Sprite Staff(int tier)
        {
            int[] orbs = { Pal.Em4, Pal.Em5, Pal.Fr3, Pal.Gl5 };
            int orb = orbs[M.Clamp(tier, 0, 3)];
            Cv cv = new Cv(28, 11);
            cv.Rect(1, 5, 20, 1, Pal.Ea4); cv.Rect(1, 6, 20, 1, Pal.Ea2);
            cv.Rect(8, 5, 2, 2, tier > 0 ? Pal.Go2 : Pal.Ea3);
            cv.Line(20, 4, 23, 2, Pal.Ea3); cv.Line(20, 7, 23, 9, Pal.Ea3); cv.Set(24, 2, Pal.Ea4); cv.Set(24, 9, Pal.Ea2);
            cv.Circle(24, 5.5f, 2.6f, Col.E(orb));
            cv.Set(23, 4, Col.E(Col.Shade(orb, 2))); cv.Set(24, 5, Col.E(Pal.Em7));
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(7, 5);
        }

        public static Sprite Wand(int tier)
        {
            int[] orbs = { Pal.Em4, Pal.Em5, Pal.Fr3, Pal.Gl5 };
            int orb = orbs[M.Clamp(tier, 0, 3)];
            Cv cv = new Cv(17, 7);
            cv.Rect(1, 3, 10, 1, Pal.Ea5); cv.Rect(1, 4, 9, 1, Pal.Ea3); cv.Set(1, 3, tier > 0 ? Pal.Go3 : Pal.Ea2);
            cv.Circle(12.5f, 3.5f, 2f, Col.E(orb)); cv.Set(12, 3, Col.E(Pal.Em7));
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(3, 3);
        }

        public static Sprite Dagger(int tier, bool kris)
        {
            int[] t = T4(tier);
            Cv cv = new Cv(15, 7);
            cv.Rect(1, 3, 3, 1, Pal.Ea2); cv.Rect(4, 2, 1, 3, t[3]);
            if (!kris) { cv.Rect(5, 3, 7, 1, t[1]); cv.Rect(5, 2, 5, 1, Edge(tier, t[2])); cv.Set(12, 3, Edge(tier, t[2])); }
            else
            {
                for (int i = 0; i < 8; i++) { int y = 3 + ((i / 2) % 2 == 0 ? 0 : -1) + (i > 5 ? 0 : 0); cv.Set(5 + i, y, t[1]); cv.Set(5 + i, y + 1, t[0]); if (i % 2 == 0) cv.Set(5 + i, y - 1 < 1 ? 1 : y, Edge(tier, t[2])); }
            }
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(2, 3);
        }

        public static Sprite Bow(int tier, bool longbow)
        {
            int[] woods = { Pal.Ea4, Pal.Ea5, Pal.Bn2, Pal.Go3 };
            int wood = woods[M.Clamp(tier, 0, 3)], woodD = Col.Shade(wood, -1);
            int half = longbow ? 12 : 9;
            Cv cv = new Cv(11, half * 2 + 3);
            int cy = half + 1;
            for (int y = -half; y <= half; y++)
            {
                float u = y / (float)half;
                int x = 2 + (int)(5.2f * (1f - u * u) + 0.5f);
                cv.Set(x, cy + y, y < 0 ? wood : woodD);
                if (Math.Abs(y) < half - 1) cv.Set(x + 1, cy + y, woodD);
                cv.Set(2, cy + y, Pal.Bn3);
            }
            cv.Rect(7, cy - 1, 2, 3, tier > 0 ? Pal.Go2 : Pal.Ea2);
            if (tier >= 2) { cv.Set(3, cy - half, Col.E(T4(tier)[4])); cv.Set(3, cy + half, Col.E(T4(tier)[4])); }
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(6, cy);
        }

        public static Sprite Shield()
        {
            Cv cv = new Cv(12, 15);
            cv.Poly(Pal.St3, 1, 1, 11, 1, 11, 8, 6, 14, 1, 8);
            cv.Poly(Pal.Cr2, 3, 3, 9, 3, 9, 7, 6, 11, 3, 7);
            cv.Rect(5, 3, 2, 7, Pal.Go3); cv.Rect(3, 5, 6, 1, Pal.Go3);
            cv.Rect(1, 1, 10, 1, Pal.St5); cv.Rect(1, 2, 1, 6, Pal.St5);
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(6, 7);
        }

        public static Sprite Cleaver()
        {
            Cv cv = new Cv(15, 9);
            cv.Rect(1, 4, 4, 1, Pal.Ea2);
            cv.Rect(5, 2, 8, 4, Pal.St2); cv.Rect(5, 2, 8, 1, Pal.St4); cv.Set(12, 5, 0); cv.Set(8, 3, Pal.Em1); cv.Set(10, 4, Pal.Em1);
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(2, 4);
        }

        public static Sprite Greatsword()
        {
            Cv cv = new Cv(36, 11);
            cv.Rect(1, 5, 7, 1, Pal.Ea1); cv.Set(0, 5, Pal.St3);
            cv.Rect(8, 1, 2, 9, Pal.St2); cv.Set(8, 1, Pal.St4); cv.Set(8, 9, Pal.St1);
            cv.Rect(10, 3, 21, 1, Pal.St3); cv.Rect(10, 4, 23, 1, Pal.St2); cv.Rect(10, 5, 24, 1, Col.E(Pal.Fr2)); cv.Rect(10, 6, 23, 1, Pal.St1); cv.Rect(10, 7, 21, 1, Pal.St0);
            cv.Set(33, 4, Pal.St3);
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(5, 5);
        }

        /// <summary>The Prior's crozier, crowned with the stolen tongue of the Vesper Bell.</summary>
        public static Sprite Crozier()
        {
            Cv cv = new Cv(44, 15);
            cv.Rect(1, 7, 30, 1, Pal.St2); cv.Rect(1, 8, 30, 1, Pal.Ink3);
            cv.Rect(12, 6, 2, 4, Pal.Go1);
            // hook
            cv.Line(31, 7, 36, 3, Pal.St2); cv.Line(36, 3, 40, 4, Pal.St2); cv.Line(40, 4, 41, 7, Pal.St2); cv.Line(41, 7, 38, 9, Pal.St1);
            // hanging clapper
            cv.Rect(37, 6, 1, 3, Pal.Go1);
            cv.Circle(37.5f, 10.5f, 2.6f, Col.E(Pal.Gl3)); cv.Set(37, 10, Col.E(Pal.Gl6));
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(10, 7);
        }

        public static Sprite CandleStaff()
        {
            Cv cv = new Cv(24, 9);
            cv.Rect(1, 4, 17, 1, Pal.Ea2); cv.Rect(1, 5, 17, 1, Pal.Ea1);
            cv.Rect(18, 3, 2, 4, Pal.Bn2);
            cv.Circle(21.5f, 4.5f, 1.8f, Col.E(Pal.Gl4)); cv.Set(21, 4, Col.E(Pal.Gl6));
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(6, 4);
        }

        // projectiles (pointing right)
        public static Sprite Arrow(bool bone)
        {
            Cv cv = new Cv(13, 5);
            cv.Rect(2, 2, 8, 1, bone ? Pal.Bn2 : Pal.Ea5);
            cv.Set(10, 2, bone ? Col.E(Pal.Fr3) : Pal.St5); cv.Set(11, 2, bone ? Col.E(Pal.Fr4) : Pal.St6); cv.Set(10, 1, Pal.St3); cv.Set(10, 3, Pal.St3);
            cv.Set(1, 1, bone ? Pal.Gl2 : Pal.Cr3); cv.Set(1, 3, bone ? Pal.Gl2 : Pal.Cr3); cv.Set(2, 1, Pal.Bn3); cv.Set(2, 3, Pal.Bn3); cv.Set(0, 2, Pal.Bn3);
            return cv.ToSprite(6, 2);
        }

        public static Sprite Knife()
        {
            Cv cv = new Cv(10, 5);
            cv.Rect(1, 2, 3, 1, Pal.Ea2); cv.Rect(4, 2, 4, 1, Pal.St5); cv.Rect(4, 1, 3, 1, Pal.St6); cv.Set(8, 2, Pal.St6); cv.Set(4, 3, Pal.St3);
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(5, 2);
        }

        /// <summary>Glowing bolt with a tapering tail. ramp = 4 colours dark to light.</summary>
        public static Sprite Bolt(int[] ramp, int len, int thick)
        {
            Cv cv = new Cv(len + 2, thick + 2);
            float cy = (thick + 2) / 2f;
            for (int x = 0; x < len; x++)
            {
                float t = x / (float)(len - 1);
                float hw = thick * 0.5f * (0.25f + 0.75f * t * t);
                for (int y = 0; y < thick + 2; y++)
                {
                    float d = Math.Abs(y + 0.5f - cy);
                    if (d > hw + 0.3f) continue;
                    float heat = t * (1f - d / (hw + 0.5f));
                    int c = heat > 0.62f ? ramp[3] : (heat > 0.36f ? ramp[2] : (heat > 0.14f ? ramp[1] : ramp[0]));
                    cv.Set(x + 1, y, Col.E(c));
                }
            }
            return cv.ToSprite(len - thick / 2, (thick + 2) / 2);
        }

        public static Sprite Orb(int[] ramp, int r)
        {
            Cv cv = new Cv(r * 2 + 3, r * 2 + 3);
            float c = r + 1.5f;
            cv.Circle(c, c, r + 0.4f, Col.E(ramp[0]));
            cv.Circle(c, c, r * 0.78f, Col.E(ramp[1]));
            cv.Circle(c - 0.4f, c - 0.4f, r * 0.52f, Col.E(ramp[2]));
            cv.Circle(c - 0.6f, c - 0.6f, r * 0.26f, Col.E(ramp[3]));
            return cv.ToSprite((int)c, (int)c);
        }

        public static readonly int[] RampEmber = { Pal.Em2, Pal.Em4, Pal.Em5, Pal.Em7 };
        public static readonly int[] RampGloam = { Pal.Gl1, Pal.Gl3, Pal.Gl4, Pal.Gl6 };
        public static readonly int[] RampFrost = { Pal.Fr0, Pal.Fr2, Pal.Fr3, Pal.Fr4 };
        public static readonly int[] RampVenom = { Pal.Vn0, Pal.Vn2, Pal.Vn3, Pal.Vn4 };

        public static Sprite[] Coin()
        {
            Sprite[] s = new Sprite[4];
            float[] w = { 3f, 2f, 0.8f, 2f };
            for (int i = 0; i < 4; i++)
            {
                Cv cv = new Cv(9, 9);
                cv.Ell(4.5f, 4.5f, w[i], 3f, Pal.Go3);
                if (w[i] > 1.5f) { cv.Ell(4f, 4f, w[i] * 0.5f, 1.6f, Pal.Go4); cv.Set(5, 6, Pal.Go1); }
                cv.Outline(Pal.Ink0);
                s[i] = cv.ToSprite(4, 7);
            }
            return s;
        }

        // inventory icons (16x16)
        public const int IcSword = 0, IcAxe = 1, IcStaff = 2, IcWand = 3, IcDagger = 4, IcKris = 5, IcLongbow = 6, IcShortbow = 7,
            IcPlate = 8, IcRobe = 9, IcLeather = 10, IcJerkin = 11, IcAmulet = 12, IcRing = 13, IcHealth = 14, IcMana = 15,
            IcSigil = 16, IcMoonpetal = 17, IcLantern = 18, IcTongue = 19, IcGold = 20, IcCharm = 21, IcSignet = 22, IconCount = 23;

        public static Sprite Icon(int id, int tier)
        {
            int[] t = T4(tier);
            Cv cv = new Cv(16, 16);
            switch (id)
            {
                case IcSword:
                    cv.Line(5, 11, 13, 3, t[1]); cv.Line(5, 10, 12, 3, Edge(tier, t[2])); cv.Line(6, 11, 13, 4, t[0]);
                    cv.Line(3, 9, 7, 13, t[3]); cv.Line(2, 13, 4, 11, Pal.Ea3); cv.Set(1, 14, t[3]); cv.Set(13, 2, Edge(tier, t[2]));
                    if (tier >= 2) cv.Set(5, 11, Col.E(t[4]));
                    break;
                case IcAxe:
                    cv.Line(2, 14, 11, 5, Pal.Ea4); cv.Line(3, 14, 12, 5, Pal.Ea2);
                    cv.Poly(t[1], 8, 2, 13, 1, 15, 6, 12, 9, 10, 6);
                    cv.Line(13, 1, 15, 6, Edge(tier, t[2])); cv.Line(15, 6, 12, 9, Edge(tier, t[2])); cv.Set(9, 3, t[0]); cv.Set(10, 5, t[3]);
                    break;
                case IcStaff:
                    {
                        int[] orbs = { Pal.Em4, Pal.Em5, Pal.Fr3, Pal.Gl5 };
                        cv.Line(2, 14, 10, 6, Pal.Ea4); cv.Line(3, 14, 11, 6, Pal.Ea2);
                        cv.Line(9, 5, 9, 2, Pal.Ea3); cv.Line(11, 7, 14, 7, Pal.Ea3);
                        cv.Circle(12.5f, 3.5f, 2.4f, Col.E(orbs[M.Clamp(tier, 0, 3)])); cv.Set(12, 3, Col.E(Pal.Em7));
                        if (tier > 0) { cv.Set(6, 10, Pal.Go3); cv.Set(7, 10, Pal.Go2); }
                    }
                    break;
                case IcWand:
                    {
                        int[] orbs = { Pal.Em4, Pal.Em5, Pal.Fr3, Pal.Gl5 };
                        cv.Line(3, 13, 10, 6, Pal.Ea5); cv.Line(4, 13, 10, 7, Pal.Ea3); cv.Set(2, 14, tier > 0 ? Pal.Go3 : Pal.Ea2);
                        cv.Circle(11.5f, 4.5f, 2f, Col.E(orbs[M.Clamp(tier, 0, 3)])); cv.Set(11, 4, Col.E(Pal.Em7));
                        cv.Set(14, 2, Col.E(Pal.Em6)); cv.Set(8, 2, Col.E(Pal.Em5)); cv.Set(14, 7, Col.E(Pal.Em5));
                    }
                    break;
                case IcDagger:
                case IcKris:
                    for (int k = 0; k < 2; k++)
                    {
                        int ox = k == 0 ? 0 : 5, oy = k == 0 ? 0 : 3;
                        if (id == IcKris && k == 1) break;
                        int s = id == IcKris ? 2 : 0;
                        cv.Line(3 + ox, 10 + oy - s, 9 + ox + s, 4 + oy - s * 2, t[1]); cv.Line(3 + ox, 9 + oy - s, 8 + ox + s, 4 + oy - s * 2, Edge(tier, t[2]));
                        cv.Line(1 + ox, 9 + oy - s, 4 + ox, 12 + oy - s, t[3]); cv.Line(1 + ox, 12 + oy - s, 2 + ox, 11 + oy - s, Pal.Ea2);
                        if (id == IcKris) { cv.Set(6, 7, t[0]); cv.Set(8, 4, t[0]); cv.Set(10, 3, Edge(tier, t[2])); cv.Set(1, 13, Col.E(t[4])); }
                    }
                    break;
                case IcLongbow:
                case IcShortbow:
                    {
                        int[] woods = { Pal.Ea4, Pal.Ea5, Pal.Bn2, Pal.Go3 };
                        int wood = woods[M.Clamp(tier, 0, 3)];
                        int h = id == IcLongbow ? 7 : 5;
                        for (int y = -h; y <= h; y++)
                        {
                            float u = y / (float)h;
                            int x = 5 + (int)(5f * (1 - u * u) + 0.5f);
                            cv.Set(x, 8 + y, wood); cv.Set(x + 1, 8 + y, Col.Shade(wood, -1));
                            cv.Set(5, 8 + y, Pal.Bn3);
                        }
                        cv.Rect(2, 8, 11, 1, Pal.Ea5); cv.Set(13, 8, Pal.St5); cv.Set(14, 8, Pal.St6); cv.Set(2, 7, Pal.Cr3); cv.Set(2, 9, Pal.Cr3);
                    }
                    break;
                case IcPlate:
                    cv.Poly(t[1], 3, 3, 13, 3, 14, 7, 12, 14, 4, 14, 2, 7);
                    cv.Poly(t[0], 8, 3, 13, 3, 14, 7, 12, 14, 8, 14);
                    cv.Rect(6, 2, 4, 2, Pal.Ink1); cv.Rect(7, 5, 2, 8, t[2]); cv.Rect(1, 3, 3, 3, t[2]); cv.Rect(12, 3, 3, 3, t[1]);
                    cv.Rect(4, 10, 8, 1, t[3]);
                    if (tier >= 2) cv.Set(7, 7, Col.E(t[4]));
                    break;
                case IcRobe:
                    {
                        int c = tier == 0 ? Pal.Gl1 : (tier == 1 ? Pal.Gl2 : (tier == 2 ? Pal.Fr1 : Pal.Em2));
                        cv.Poly(c, 5, 2, 11, 2, 14, 14, 2, 14);
                        cv.Poly(Col.Shade(c, -1), 8, 2, 11, 2, 14, 14, 8, 14);
                        cv.Rect(7, 2, 2, 12, t[3]); cv.Rect(1, 4, 4, 2, c); cv.Rect(11, 4, 4, 2, Col.Shade(c, -1));
                        cv.Rect(4, 8, 8, 1, tier >= 3 ? Col.E(Pal.Em5) : Pal.Em3);
                    }
                    break;
                case IcLeather:
                    cv.Poly(Pal.St1, 4, 2, 12, 2, 13, 14, 3, 14);
                    cv.Poly(Pal.St0, 8, 2, 12, 2, 13, 14, 8, 14);
                    cv.Line(4, 3, 11, 12, Pal.Ea4); cv.Rect(3, 10, 10, 1, t[3]); cv.Set(8, 10, tier >= 2 ? Col.E(t[4]) : t[2]);
                    cv.Rect(2, 3, 2, 4, Pal.St2); cv.Rect(12, 3, 2, 4, Pal.St1);
                    break;
                case IcJerkin:
                    cv.Poly(Pal.Ea4, 4, 2, 12, 2, 13, 14, 3, 14);
                    cv.Poly(Pal.Ea3, 8, 2, 12, 2, 13, 14, 8, 14);
                    cv.Rect(7, 3, 1, 10, Pal.Ea6); cv.Set(8, 5, Pal.Ea6); cv.Set(8, 8, Pal.Ea6); cv.Set(8, 11, Pal.Ea6);
                    cv.Rect(1, 2, 3, 7, Pal.Lf3); cv.Rect(12, 2, 3, 7, Pal.Lf2); cv.Rect(3, 10, 10, 1, t[3]);
                    if (tier >= 2) cv.Set(5, 4, Col.E(t[4]));
                    break;
                case IcAmulet:
                case IcCharm:
                    cv.Line(3, 2, 8, 9, t[3]); cv.Line(13, 2, 8, 9, t[3]);
                    if (id == IcAmulet) { cv.Circle(8, 11, 3f, t[3]); cv.Circle(8, 11, 1.7f, tier >= 1 ? Col.E(t[4]) : t[1]); }
                    else { cv.Poly(t[3], 8, 7, 12, 11, 8, 15, 4, 11); cv.Poly(tier >= 1 ? Col.E(t[4]) : t[1], 8, 9, 10, 11, 8, 13, 6, 11); }
                    break;
                case IcRing:
                case IcSignet:
                    cv.Circle(8, 9.5f, 4.6f, t[3]); cv.Circle(8, 9.5f, 2.8f, 0);
                    cv.Set(5, 7, Col.Shade(t[3], 1)); cv.Set(4, 8, Col.Shade(t[3], 1));
                    if (id == IcRing) { cv.Rect(7, 3, 3, 3, tier >= 1 ? Col.E(t[4]) : t[1]); cv.Set(7, 3, Pal.Em7); }
                    else { cv.Rect(6, 3, 5, 3, t[3]); cv.Set(8, 4, tier >= 1 ? Col.E(t[4]) : t[0]); }
                    break;
                case IcHealth:
                case IcMana:
                    {
                        int a = id == IcHealth ? Pal.Cr3 : Pal.Fr2, b = id == IcHealth ? Pal.Cr2 : Pal.Fr1, l = id == IcHealth ? Pal.Cr4 : Pal.Fr3;
                        cv.Rect(6, 1, 4, 2, Pal.Ea4); cv.Rect(7, 3, 2, 3, Pal.St5);
                        cv.Circle(8, 10, 4.6f, Pal.St5);
                        cv.Circle(8, 10.5f, 3.6f, b); cv.Circle(7.5f, 10f, 2.6f, a);
                        cv.Rect(5, 7, 6, 1, Pal.St6); cv.Set(6, 9, l); cv.Set(6, 10, l); cv.Set(7, 8, Pal.St6);
                    }
                    break;
                case IcSigil:
                    cv.Circle(8, 8, 6.4f, Pal.St3); cv.Circle(8, 8, 5f, Pal.St1);
                    cv.Rect(7, 4, 2, 5, Col.E(Pal.Gl4)); cv.Circle(8, 4.5f, 2f, Col.E(Pal.Gl4)); cv.Set(8, 4, Pal.St1); cv.Set(7, 4, Pal.St1);
                    cv.Set(6, 11, Col.E(Pal.Gl4)); cv.Set(9, 11, Col.E(Pal.Gl4)); cv.Set(7, 10, Col.E(Pal.Gl4)); cv.Set(8, 10, Col.E(Pal.Gl4));
                    cv.Set(4, 4, Pal.St5); cv.Set(5, 3, Pal.St5);
                    break;
                case IcMoonpetal:
                    cv.Line(8, 15, 8, 8, Pal.Lf4); cv.Line(8, 12, 5, 10, Pal.Lf3); cv.Line(8, 13, 11, 11, Pal.Lf3);
                    cv.Ascii(3, 1, new[] { "...a.a...", "..abbba..", ".abbcbba.", "..abcba..", ".a.bbb.a.", "....a...." },
                        Cv.Map('a', Col.E(Pal.Gl5), 'b', Col.E(Pal.Gl6), 'c', Col.E(Pal.Fr3)));
                    break;
                case IcLantern:
                    cv.Rect(6, 1, 4, 1, Pal.St3); cv.Rect(7, 2, 2, 2, Pal.St2);
                    cv.Rect(4, 4, 8, 1, Pal.St3); cv.Rect(4, 12, 8, 2, Pal.St2);
                    cv.Rect(4, 5, 1, 7, Pal.St2); cv.Rect(11, 5, 1, 7, Pal.St1);
                    cv.Rect(5, 5, 6, 7, Col.E(Pal.Em4)); cv.Rect(6, 6, 4, 5, Col.E(Pal.Em5)); cv.Rect(7, 7, 2, 3, Col.E(Pal.Em7));
                    break;
                case IcTongue:
                    cv.Line(8, 1, 8, 9, Pal.Go2); cv.Line(9, 1, 9, 9, Pal.Go1); cv.Rect(7, 1, 3, 2, Pal.Go3);
                    cv.Circle(8.5f, 11.5f, 3.4f, Pal.Go2); cv.Circle(8f, 11f, 2f, Pal.Go3); cv.Set(7, 10, Col.E(Pal.Go4));
                    break;
                case IcGold:
                    cv.Ell(6, 11, 4, 2.4f, Pal.Go2); cv.Ell(6, 10, 4, 2.4f, Pal.Go3);
                    cv.Ell(10, 8, 4, 2.4f, Pal.Go2); cv.Ell(10, 7, 4, 2.4f, Pal.Go3);
                    cv.Ell(7, 5, 4, 2.4f, Pal.Go2); cv.Ell(7, 4, 4, 2.4f, Pal.Go4); cv.Set(6, 3, Pal.Em7);
                    break;
            }
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(0, 0);
        }

        // skill icons (18x18 glyphs)
        public const int SkSlash = 0, SkBlock = 1, SkCharge = 2, SkWhirl = 3, SkOath = 4,
            SkBolt = 5, SkBlink = 6, SkFireball = 7, SkNova = 8, SkStorm = 9,
            SkStab = 10, SkKnife = 11, SkStep = 12, SkVeil = 13, SkFan = 14,
            SkShot = 15, SkPierce = 16, SkTrap = 17, SkVolley = 18, SkMark = 19, SkDodge = 20, SkCount = 21;

        public static Sprite Skill(int id)
        {
            Cv cv = new Cv(18, 18);
            int steel = Pal.St5, steelD = Pal.St3, w = Pal.Bn4;
            switch (id)
            {
                case SkSlash:
                    for (int i = 0; i < 20; i++) { float a = -2.3f + i * 0.085f; cv.Set((int)(7 + M.Cos(a) * 9), (int)(13 + M.Sin(a) * 9), Pal.Bn4); cv.Set((int)(7 + M.Cos(a) * 8), (int)(13 + M.Sin(a) * 8), Pal.Cr4); }
                    cv.Line(3, 15, 13, 5, steel); cv.Line(4, 15, 14, 5, steelD); cv.Line(2, 12, 6, 16, Pal.Go3);
                    break;
                case SkBlock:
                    cv.Poly(Pal.St3, 3, 2, 15, 2, 15, 9, 9, 16, 3, 9); cv.Poly(Pal.Cr2, 5, 4, 13, 4, 13, 8, 9, 13, 5, 8);
                    cv.Rect(8, 4, 2, 8, Pal.Go3); cv.Rect(5, 6, 8, 1, Pal.Go3); cv.Rect(3, 2, 12, 1, Pal.St6);
                    break;
                case SkCharge:
                    cv.Poly(Pal.St4, 9, 4, 16, 9, 9, 14); cv.Poly(Pal.St6, 9, 4, 16, 9, 9, 9);
                    cv.Rect(1, 6, 7, 1, Pal.Em5); cv.Rect(3, 9, 6, 1, Pal.Em6); cv.Rect(1, 12, 7, 1, Pal.Em5);
                    break;
                case SkWhirl:
                    for (int i = 0; i < 40; i++)
                    {
                        float a = i * 0.25f, r = 2.2f + i * 0.14f;
                        cv.Set((int)(9 + M.Cos(a) * r), (int)(9 + M.Sin(a) * r), i > 26 ? Pal.Bn4 : Pal.Cr4);
                    }
                    cv.Line(5, 9, 13, 9, steel); cv.Line(9, 5, 9, 13, steelD);
                    break;
                case SkOath:
                    cv.Ascii(4, 2, new[] { "...ab.....", "..aabb....", "..aabb....", ".aaabbb...", ".aaabbb...", "aaaabbbb..", "aaaabbbbb.", "....c.....", "...c.c...." }, Cv.Map('a', Pal.Go4, 'b', Pal.Go2, 'c', Pal.Go3));
                    cv.Set(2, 4, Pal.Em6); cv.Set(15, 5, Pal.Em6); cv.Set(3, 12, Pal.Em6); cv.Set(14, 13, Pal.Em6); cv.Set(9, 15, Pal.Em6);
                    break;
                case SkBolt:
                    cv.Poly(Pal.Em4, 2, 12, 9, 5, 15, 3, 13, 9, 6, 16); cv.Poly(Pal.Em6, 6, 11, 11, 6, 14, 5, 12, 8, 8, 13); cv.Set(13, 5, Pal.Em7);
                    break;
                case SkBlink:
                    cv.Poly(Pal.Gl3, 4, 3, 7, 9, 4, 15, 1, 9); cv.Poly(Pal.Gl6, 13, 3, 16, 9, 13, 15, 10, 9);
                    cv.Set(8, 9, Pal.Gl5); cv.Set(9, 9, Pal.Gl5); cv.Set(8, 6, Pal.Gl4); cv.Set(9, 12, Pal.Gl4);
                    break;
                case SkFireball:
                    cv.Circle(9, 10, 5.6f, Pal.Em3); cv.Circle(9, 10.5f, 4.2f, Pal.Em4); cv.Circle(8.5f, 11f, 2.8f, Pal.Em5); cv.Circle(8.5f, 11.5f, 1.4f, Pal.Em7);
                    cv.Poly(Pal.Em4, 6, 6, 8, 1, 10, 5, 12, 2, 13, 7);
                    break;
                case SkNova:
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * M.TAU / 6f;
                        cv.Line(9, 9, (int)(9 + M.Cos(a) * 7.5f), (int)(9 + M.Sin(a) * 7.5f), Pal.Fr3);
                        cv.Set((int)(9 + M.Cos(a + 0.35f) * 5), (int)(9 + M.Sin(a + 0.35f) * 5), Pal.Fr4);
                        cv.Set((int)(9 + M.Cos(a - 0.35f) * 5), (int)(9 + M.Sin(a - 0.35f) * 5), Pal.Fr4);
                    }
                    cv.Circle(9.5f, 9.5f, 1.6f, Pal.Fr4);
                    break;
                case SkStorm:
                    cv.Poly(Pal.Em6, 10, 1, 4, 10, 8, 10, 6, 17, 14, 7, 10, 7, 13, 1);
                    cv.Set(2, 4, Pal.Fr3); cv.Set(15, 13, Pal.Fr3); cv.Set(3, 14, Pal.Fr4); cv.Set(15, 3, Pal.Fr4);
                    break;
                case SkStab:
                    cv.Line(3, 14, 11, 6, steel); cv.Line(4, 14, 12, 6, steelD); cv.Line(2, 11, 6, 15, Pal.Go3);
                    cv.Line(14, 14, 6, 6, steel); cv.Line(13, 14, 5, 6, steelD); cv.Line(15, 11, 11, 15, Pal.Go3);
                    cv.Set(12, 5, w); cv.Set(5, 5, w);
                    break;
                case SkKnife:
                    cv.Line(6, 12, 14, 4, steel); cv.Line(7, 12, 15, 4, steelD); cv.Line(4, 10, 8, 14, Pal.Go3); cv.Set(15, 3, w);
                    cv.Rect(1, 5, 5, 1, Pal.Gl4); cv.Rect(2, 8, 3, 1, Pal.Gl4);
                    break;
                case SkStep:
                    for (int i = 0; i < 22; i++) { float a = 0.5f + i * 0.11f; cv.Set((int)(9 + M.Cos(a) * 7), (int)(8 + M.Sin(a) * 7), Pal.Gl4); cv.Set((int)(9 + M.Cos(a) * 6), (int)(8 + M.Sin(a) * 6), Pal.Gl2); }
                    cv.Poly(Pal.Gl6, 12, 3, 16, 6, 11, 8);
                    cv.Circle(5, 12, 1.8f, Pal.Gl3);
                    break;
                case SkVeil:
                    cv.Circle(6, 11, 4f, Pal.St3); cv.Circle(11, 10, 4.6f, Pal.St4); cv.Circle(9, 7, 3.6f, Pal.St5); cv.Circle(13, 13, 2.6f, Pal.St3);
                    cv.Rect(5, 9, 4, 1, Pal.Ink1); cv.Rect(10, 9, 4, 1, Pal.Ink1); cv.Set(6, 10, Pal.Ink1); cv.Set(11, 10, Pal.Ink1);
                    break;
                case SkFan:
                    for (int k = 0; k < 5; k++)
                    {
                        float a = -M.PI * 0.5f + (k - 2) * 0.48f;
                        cv.Line(9, 15, (int)(9 + M.Cos(a) * 12), (int)(15 + M.Sin(a) * 12), k % 2 == 0 ? steel : steelD);
                        cv.Set((int)(9 + M.Cos(a) * 13), (int)(15 + M.Sin(a) * 13), w);
                    }
                    cv.Circle(9, 15, 1.6f, Pal.Go3);
                    break;
                case SkShot:
                    cv.Line(3, 14, 13, 4, Pal.Ea5); cv.Poly(steel, 15, 2, 15, 7, 10, 2); cv.Line(2, 12, 5, 15, Pal.Cr3); cv.Line(4, 11, 6, 13, Pal.Bn3);
                    break;
                case SkPierce:
                    cv.Line(1, 9, 15, 9, Pal.Em5); cv.Poly(Pal.Em7, 17, 9, 12, 6, 12, 12);
                    cv.Line(6, 3, 6, 15, Pal.St3); cv.Line(10, 4, 10, 14, Pal.St4); cv.Set(6, 9, w); cv.Set(10, 9, w);
                    break;
                case SkTrap:
                    cv.Ell(9, 11, 7.4f, 4f, Pal.St2); cv.Ell(9, 11, 5.4f, 2.6f, Pal.Ink1);
                    for (int x = 3; x < 16; x += 2) { cv.Set(x, 8, Pal.St6); cv.Set(x, 7, Pal.St5); cv.Set(x + 1, 13, Pal.St5); }
                    cv.Set(9, 11, Pal.Cr3); cv.Set(8, 11, Pal.Cr3);
                    break;
                case SkVolley:
                    for (int k = 0; k < 3; k++)
                    {
                        int x = 4 + k * 5, y = 2 + (k % 2) * 3;
                        cv.Line(x, y, x, y + 9, Pal.Ea5); cv.Set(x, y + 10, steel); cv.Set(x - 1, y + 9, steelD); cv.Set(x + 1, y + 9, steelD);
                        cv.Set(x - 1, y, Pal.Cr3); cv.Set(x + 1, y, Pal.Cr3);
                    }
                    break;
                case SkMark:
                    cv.Circle(9, 9, 6.6f, Pal.Cr3); cv.Circle(9, 9, 5f, 0); cv.Circle(9.5f, 9.5f, 2f, Pal.Cr4);
                    cv.Rect(9, 1, 1, 4, Pal.Bn4); cv.Rect(9, 14, 1, 4, Pal.Bn4); cv.Rect(1, 9, 4, 1, Pal.Bn4); cv.Rect(14, 9, 4, 1, Pal.Bn4);
                    break;
                case SkDodge:
                    cv.Poly(Pal.Bn4, 10, 5, 16, 9, 10, 13); cv.Rect(2, 6, 6, 1, Pal.St5); cv.Rect(4, 9, 6, 1, Pal.St6); cv.Rect(2, 12, 6, 1, Pal.St5);
                    break;
            }
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(0, 0);
        }

        // effects
        /// <summary>Crescent swoosh frames. Origin is the swing centre; the arc faces +x.</summary>
        public static Sprite[] Slash(int radius, float arc, int inner, int outer)
        {
            Sprite[] fr = new Sprite[3];
            for (int f = 0; f < 3; f++)
            {
                int S = radius * 2 + 6;
                Cv cv = new Cv(S, S);
                float c = S / 2f;
                float thick = radius * (0.34f - f * 0.08f);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float dx = x + 0.5f - c, dy = y + 0.5f - c;
                        float d = M.Sqrt(dx * dx + dy * dy);
                        if (d > radius || d < radius - thick) continue;
                        float a = M.Atan2(dy, dx);
                        float u = (a + arc * 0.5f) / arc;       // 0..1 along the sweep
                        if (u < 0 || u > 1) continue;
                        // taper towards the trailing end, fade with frame
                        float taper = M.Smooth(u * 1.6f) * M.Smooth((1f - u) * 6f);
                        float across = (d - (radius - thick)) / thick;
                        if (across < 1f - taper) continue;
                        int alpha = (int)(255 * (1f - f * 0.3f) * (0.45f + 0.55f * across));
                        cv.Set(x, y, Col.WithA(across > 0.6f ? inner : outer, M.Clamp(alpha, 0, 253)));
                    }
                fr[f] = cv.ToSprite((int)c, (int)c);
            }
            return fr;
        }

        public static Sprite[] Spark(int color)
        {
            Sprite[] fr = new Sprite[3];
            string[][] rows =
            {
                new[]{ ".......", "...a...", "..aba..", ".abbba.", "..aba..", "...a...", "......." },
                new[]{ "...a...", ".a.a.a.", "..aba..", "aabbbaa", "..aba..", ".a.a.a.", "...a..." },
                new[]{ "a..a..a", ".......", ".......", "a.....a", ".......", ".......", "a..a..a" },
            };
            for (int i = 0; i < 3; i++)
            {
                Cv cv = new Cv(7, 7);
                cv.Ascii(0, 0, rows[i], Cv.Map('a', Col.E(color), 'b', Col.E(Pal.White)));
                fr[i] = cv.ToSprite(3, 3);
            }
            return fr;
        }

        public static Sprite Cursor(bool aim)
        {
            Cv cv = new Cv(13, 13);
            if (aim)
            {
                cv.Rect(6, 0, 1, 4, Pal.Bn4); cv.Rect(6, 9, 1, 4, Pal.Bn4); cv.Rect(0, 6, 4, 1, Pal.Bn4); cv.Rect(9, 6, 4, 1, Pal.Bn4);
                cv.Set(6, 6, Pal.Em5);
                cv.Outline(Pal.Ink0);
                return cv.ToSprite(6, 6);
            }
            cv.Ascii(0, 0, new[]
            {
                "a..........",
                "aa.........",
                "aba........",
                "abba.......",
                "abbba......",
                "abbbba.....",
                "abbbbba....",
                "abbbbbba...",
                "abbbaaaa...",
                "abab.......",
                "aa.ab......",
                "....a......",
            }, Cv.Map('a', Pal.Go3, 'b', Pal.Bn4));
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(1, 1);
        }
    }
}
