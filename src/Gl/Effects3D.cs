using System;

namespace Vesperfell
{
    /// <summary>Everything that glows, flies or marks the ground: flames, particles, slashes, telegraphs and spell areas.</summary>
    public sealed unsafe partial class Renderer3D
    {
        MapGeo fxGeo;
        readonly V3[] flamePts = new V3[4];
        MeshB trapMesh;
        const float DecalLift = 0.3f, AddLift = 0.9f;
        const int Red = 0xE0483E;

        float Gh(float x, float z) { return fxGeo != null ? fxGeo.HeightAt(x, z) : 0; }
        V3 Gp(float x, float z, float lift) { return new V3(x, Gh(x, z) + lift, z); }
        static byte A(float a) { return (byte)M.Clamp((int)(a * 255f + 0.5f), 0, 255); }

        void BuildEffects(bool play, MapGeo geo)
        {
            fxGeo = geo;
            Paint(dyn, 0, 0, -1);
            BuildFlames(geo);
            BuildParticles();
            if (play)
            {
                BlobShadows();
                GroundFx();
                Slashes();
                AreasPost();
                LootBeams();
            }
            Paint(dyn, 0, 0, -1);
            dyn.T = Xf.Id; dyn.Col = 0xFFFFFF; dyn.Em = 0; dyn.Layer = 0;
        }

        // fire
        void BuildFlames(MapGeo geo)
        {
            float t = g.UiT;
            for (int i = 0; i < lightProps.Count; i++)
            {
                Prop p = lightProps[i];
                if (p.LightR <= 0 || p.Hidden) continue;
                float size; bool gloam;
                int n = PropModels.Flames(p, geo, flamePts, out size, out gloam);
                for (int k = 0; k < n; k++)
                {
                    V3 c = flamePts[k];
                    if (!BoxVisible(c.X - 14, c.Y - 4, c.Z - 14, c.X + 14, c.Y + 26, c.Z + 14)) continue;
                    Flame(c, size, gloam, t, p.X * 0.37f + p.Y * 0.11f + k * 1.7f);
                }
            }
        }

        void Flame(V3 c, float s, bool gloam, float t, float seed)
        {
            float fl = 0.86f + 0.14f * M.Sin(t * 11f + seed) * M.Sin(t * 17.3f + seed * 1.3f);
            int halo = gloam ? 0x584688 : 0xCF5327, body = gloam ? 0x8A7CE8 : 0xF0862A, tip = gloam ? 0xC9D3F5 : 0xFFD878;
            Bill(fxAdd, c + new V3(0, s * 1.0f, 0), s * 3.6f * fl, halo, 0.3f, 0);
            Bill(fxAdd, c + new V3(0, s * 0.75f, 0), s * 1.7f, body, 0.7f * fl, 0);
            for (int k = 0; k < 3; k++)
            {
                float ph = t * (7f + k * 2.3f) + seed + k * 2.1f;
                V3 o = new V3(M.Sin(ph) * s * 0.22f, s * (0.55f + k * 0.62f + 0.18f * M.Sin(ph * 1.7f)), M.Cos(ph * 0.8f) * s * 0.16f);
                Bill(fxAdd, c + o, s * (1.05f - k * 0.26f) * fl, k == 0 ? body : tip, 0.8f - k * 0.18f, 0);
            }
            Bill(fxAdd, c + new V3(0, s * 0.4f, 0), s * 0.6f, 0xFFF6E0, 0.85f, 0);
        }

        // particles
        void BuildParticles()
        {
            Game G = g;
            float kr = Math.Min(1.4f, ambR * 1.5f), kg = Math.Min(1.4f, ambG * 1.5f), kb = Math.Min(1.4f, ambB * 1.5f);
            float vx0 = Look.X - 760, vx1 = Look.X + 760, vz0 = Look.Z - 900, vz1 = Look.Z + 420;
            for (int i = 0; i < G.PartCount; i++)
            {
                Particle p = G.Parts[i];
                if (p.X < vx0 || p.X > vx1 || p.Y < vz0 || p.Y > vz1) continue;
                float t = p.Life / Math.Max(0.001f, p.Max);
                int c = t > 0.5f ? p.Color : Col.Lerp(p.Color2 | Col.Opaque, p.Color | Col.Opaque, t * 2f) & 0xFFFFFF;
                V3 at = new V3(p.X, Gh(p.X, p.Y) + p.Z + 0.6f, p.Y);
                switch (p.Kind)
                {
                    case Game.PPixel:
                    case Game.PSquare:
                        {
                            float h = p.Kind == Game.PSquare ? 0.95f : 0.55f;
                            if (p.Lit) Cube(dyn, at, h, c);
                            else Bill(fxAlpha, at, h * 1.1f, c, Math.Min(1f, t * 2f), 1);
                        }
                        break;
                    case Game.PGlow:
                        {
                            float fade = Math.Min(1f, t * 2.5f) * Math.Min(1f, (1f - t) * 8f);
                            Bill(fxAdd, at, p.Size * 1.25f, c, 0.62f * fade, 0);
                            Bill(fxAdd, at, 1.0f, c, fade, 0);
                        }
                        break;
                    case Game.PSmoke:
                        Bill(fxAlpha, at + new V3(0, p.Size * 0.5f, 0), p.Size * 1.25f, p.Lit ? ScaleCol(c, kr, kg, kb) : c, 0.45f * t, 4);
                        break;
                    case Game.PStreak:
                        RibbonS(fxAlpha, at - new V3(0, 7, 0), at, 0.8f, c, (220 * t + 30) / 255f, 1);
                        break;
                }
            }
        }

        static int ScaleCol(int c, float kr, float kg, float kb)
        {
            int r = Math.Min(255, (int)(((c >> 16) & 255) * kr)), gg = Math.Min(255, (int)(((c >> 8) & 255) * kg)), b = Math.Min(255, (int)((c & 255) * kb));
            return (r << 16) | (gg << 8) | b;
        }

        static void Cube(MeshB m, V3 c, float h, int col)
        {
            m.T = Xf.Id; m.Col = col; m.Layer = 0; m.Em = 0;
            m.Box(c.X - h, c.Y - h, c.Z - h, c.X + h, c.Y + h, c.Z + h);
        }

        // ground
        void BlobShadows()
        {
            Game G = g;
            float k = G.Zone.Indoor ? 1f : 0.6f;
            for (int i = 0; i < G.Foes.Count; i++)
            {
                Enemy e = G.Foes[i];
                if (e.Dead || e.State == Enemy.Vanish) continue;
                float w = (e.Spr != null ? e.Spr.ShadowW : 6) * 1.35f;
                if (e.Kind == EnemyKind.Wisp) w *= 0.7f;
                Blob(e.X, e.Y, w, 0.42f * e.FadeIn * k);
            }
            for (int i = 0; i < G.Npcs.Count; i++) Blob(G.Npcs[i].X, G.Npcs[i].Y, 7.5f, 0.4f * k);
            for (int i = 0; i < G.Party.Count; i++)
            {
                Player p = G.Party[i];
                if (!p.Dead) Blob(p.X, p.Y, 8.2f, (p.StealthT > 0 ? 0.18f : 0.45f) * k);
            }
            for (int i = 0; i < G.Drops.Count; i++) Blob(G.Drops[i].X, G.Drops[i].Y, 4.5f, 0.35f * k);
            for (int i = 0; i < G.Shots.Count; i++) Blob(G.Shots[i].X, G.Shots[i].Y, 3.2f, 0.25f * k);
        }

        void Blob(float x, float z, float r, float alpha)
        {
            float gy = Gh(x, z);
            if (!BoxVisible(x - r, gy - 3, z - r, x + r, gy + 3, z + r)) return;
            GDisc(decal, DecalLift, x, z, r, 0x000000, alpha, 4, 2);
        }

        /// <summary>Telegraphs and spell marks painted on the ground (they sit under everything and ignore the dark).</summary>
        void GroundFx()
        {
            Game G = g;
            for (int i = 0; i < G.Areas.Count; i++)
            {
                Area a = G.Areas[i];
                switch (a.Kind)
                {
                    case Area.Trap:
                        {
                            bool blink = !a.Armed && ((int)(a.T * 14) & 1) == 0;
                            if (blink) break;
                            float gy = Gh(a.X, a.Y);
                            GSector(decal, DecalLift, a.X, a.Y, 7, 10, 0, M.PI, 0x8C9BB2, 200 / 255f);
                            Paint(dyn, 0, 0, -1);
                            Put(TrapMesh(), Xf.Move(a.X, gy, a.Y) * Xf.Yaw(a.Seed * 0.7f));
                            Bill(fxAdd, new V3(a.X, gy + 1.6f, a.Y), 2.4f, 0xCF4150, 0.8f, 0);
                        }
                        break;
                    case Area.Rain:
                        GSector(decal, DecalLift, a.X, a.Y, 0, a.R, 0, M.PI, 0xE8E0C4, (a.T < a.Delay ? 26 : 34) / 255f);
                        GSector(decal, DecalLift, a.X, a.Y, a.R - 1.6f, a.R, 0, M.PI, 0xE8E0C4, 150 / 255f);
                        break;
                    case Area.Pool:
                        {
                            float fade = M.Clamp01((a.Dur - a.T) / 0.5f);
                            GSector(decal, DecalLift, a.X, a.Y, 0, a.R, 0, M.PI, 0x3F6A24, 150 * fade / 255f);
                            GSector(decal, DecalLift, a.X, a.Y, a.R - 2.5f, a.R, 0, M.PI, 0x6FA82E, 190 * fade / 255f);
                        }
                        break;
                    case Area.Sigil:
                        if (!a.Armed)
                        {
                            float pr = M.Clamp01(a.T / Math.Max(0.01f, a.Delay));
                            GSector(decal, DecalLift, a.X, a.Y, 0, a.R, 0, M.PI, 0x7F6CB6, (40 + 70 * pr) / 255f);
                            GSector(decal, DecalLift, a.X, a.Y, 0, a.R * pr, 0, M.PI, 0xA7A0DC, 80 / 255f);
                            GSector(decal, DecalLift, a.X, a.Y, a.R - 1.6f, a.R, 0, M.PI, 0xEAF3FF, 220 / 255f);
                            for (int k = 0; k < 6; k++)
                            {
                                float an = k * M.TAU / 6 + a.T * 2.2f + a.Seed;
                                float rx = a.X + M.Cos(an) * a.R * 0.62f, rz = a.Y + M.Sin(an) * a.R * 0.62f;
                                GDisc(decal, DecalLift, rx, rz, 1.4f, 0xEAF3FF, 1f, 1, 1);
                                Bill(fxAdd, new V3(rx, Gh(rx, rz) + 2, rz), 3.4f, 0xA7A0DC, 0.5f, 0);
                            }
                        }
                        break;
                }
            }
            for (int i = 0; i < G.Foes.Count; i++)
            {
                Enemy e = G.Foes[i];
                if (e.Dead || e.State != Enemy.Windup) continue;
                float pr = M.Clamp01(e.StateT / Math.Max(0.01f, e.StateDur));
                float al = (46 + 70 * pr) / 255f;
                switch (e.AttackKind)
                {
                    case Game.AkArc:
                        {
                            float reach = e.Kind == EnemyKind.Knight ? 46 : (e.Kind == EnemyKind.Prior ? 52 : (e.Kind == EnemyKind.Crawler ? 24 : 30));
                            float half = e.Kind == EnemyKind.Knight ? 1.4f : (e.Kind == EnemyKind.Prior ? 1.5f : (e.Kind == EnemyKind.Crawler ? 1.1f : 1.05f));
                            reach += 5;
                            GSector(decal, DecalLift, e.X, e.Y, 0, reach, e.AttackAng, half, Red, al);
                            GSector(decal, DecalLift, e.X, e.Y, 0, reach * pr, e.AttackAng, half, Red, 70 / 255f);
                            GSector(decal, DecalLift, e.X, e.Y, reach - 1.5f, reach, e.AttackAng, half, 0xFFB0A0, 170 / 255f);
                        }
                        break;
                    case Game.AkLine:
                        {
                            bool knight = e.Kind == EnemyKind.Knight;
                            if (e.Kind != EnemyKind.Wolf && e.Kind != EnemyKind.AlphaWolf && !knight) break;
                            float len = knight ? 216 : 92, hw = knight ? 13 : 8;
                            float ex = e.X + M.Cos(e.AttackAng) * len, ez = e.Y + M.Sin(e.AttackAng) * len * Surface.YMove;
                            GStrip(decal, DecalLift, e.X, e.Y, ex, ez, hw, Red, al - 14 / 255f);
                            GStrip(decal, DecalLift, e.X, e.Y, e.X + (ex - e.X) * pr, e.Y + (ez - e.Y) * pr, hw, Red, 60 / 255f);
                        }
                        break;
                    case Game.AkCircle:
                        GSector(decal, DecalLift, e.X, e.Y, 0, 58, 0, M.PI, Red, al);
                        GSector(decal, DecalLift, e.X, e.Y, 0, 58 * pr, 0, M.PI, Red, 70 / 255f);
                        GSector(decal, DecalLift, e.X, e.Y, 56.5f, 58, 0, M.PI, 0xFFB0A0, 180 / 255f);
                        break;
                    case Game.AkAim:
                        {
                            float ex = e.X + M.Cos(e.AttackAng) * 190, ez = e.Y + M.Sin(e.AttackAng) * 190;
                            int col = pr > 0.72f ? 0xFFE0D0 : Red;
                            GStrip(decal, DecalLift, e.X, e.Y, ex, ez, 0.7f, col, (40 + 90 * pr) / 255f);
                            float h = 13.5f * (1f + (e.Spr != null ? e.Spr.Height / 24f - 1f : 0) * 0.8f);
                            V3 a0 = new V3(e.X + M.Cos(e.AttackAng) * 8, Gh(e.X, e.Y) + h, e.Y + M.Sin(e.AttackAng) * 8);
                            V3 a1 = new V3(ex, a0.Y, ez);
                            Ribbon(fxAdd, a0, a1, 1.4f, col, 0.25f + 0.55f * pr);
                        }
                        break;
                }
            }
        }

        MeshB TrapMesh()
        {
            if (trapMesh != null) return trapMesh;
            MeshB m = new MeshB();
            m.Layer = Mat3.Metal;
            m.Col = 0x6E7686;
            for (int k = 0; k < 14; k++)
            {
                float a = k * M.TAU / 14;
                m.T = Xf.Move(M.Cos(a) * 8.2f, 0, M.Sin(a) * 8.2f) * Xf.Yaw(a);
                m.Box(-0.7f, 0, -2.0f, 0.7f, 1.2f, 2.0f);
            }
            m.Col = 0xDCE3E6;
            for (int k = 0; k < 8; k++)
            {
                float a = k * M.TAU / 8 + 0.2f;
                m.T = Xf.Move(M.Cos(a) * 7.6f, 0.8f, M.Sin(a) * 7.6f) * Xf.Yaw(a) * Xf.RotZ(0.5f);
                m.Box(-0.45f, 0, -0.55f, 0.45f, 3.4f, 0.55f);
            }
            m.T = Xf.Id;
            m.Col = 0x4A4C58;
            m.Cyl(0, 0, 0, 0.6f, 3.2f, 2.8f, 10, true, false, 0);
            m.Layer = 0; m.Col = 0xCF4150; m.Em = 140;
            m.Box(-0.9f, 0.6f, -0.9f, 0.9f, 1.3f, 0.9f);
            m.Em = 0;
            trapMesh = m;
            return m;
        }

        // swings
        void Slashes()
        {
            Game G = g;
            V3 U = new V3(1, 0, 0), V = new V3(0, 0, 1);
            for (int n = 0; n < G.Party.Count; n++)
            {
                Player p = G.Party[n];
                if (p.Dead) continue;
                WeaponType wt = p.WType;
                float gy = Gh(p.X, p.Y);
                if (!BoxVisible(p.X - 50, gy, p.Y - 50, p.X + 50, gy + 40, p.Y + 50)) continue;
                V3 c = new V3(p.X, gy + 20f, p.Y);
                bool own = Fp && p == G.P;                  // first person: the hands draw this hero's swings
                if (p.AtkT > 0 && !wt.Ranged && !own)
                {
                    float pr = 1f - p.AtkT / Math.Max(0.01f, p.AtkDur);
                    float reach = wt.Reach;
                    if (p.Class == ClassId.Rogue)
                    {
                        float th = M.Sin(pr * M.PI);
                        bool dual = p.Equip[Player.SlotWeapon] != null && p.Equip[Player.SlotWeapon].Base == 4;
                        bool left = dual && p.SwingStep == 1;
                        float a = p.AimAngle + (left ? 0.2f : -0.2f);
                        V3 d = new V3(M.Cos(a), -0.04f, M.Sin(a));
                        V3 s0 = c + d * (reach * 0.25f), s1 = c + d * (reach * (0.45f + 0.65f * th));
                        Ribbon(fxAdd, s0, s1, 6f * th + 1f, 0xB8C8F0, 0.5f * th);
                        Ribbon(fxAdd, s0, s1, 1.6f, 0xFFFFFF, 0.9f * th);
                    }
                    else
                    {
                        float from = p.AimAngle + p.SwingFrom, to = p.AimAngle + p.SwingTo;
                        float cur = M.Lerp(from, to, M.EaseOut(pr));
                        float dir = to >= from ? 1 : -1;
                        float tail = from;
                        if (Math.Abs(cur - tail) > 2.4f) tail = cur - dir * 2.4f;
                        float fade = 1f - M.Clamp01((pr - 0.55f) / 0.45f);
                        bool heavy = p.SwingStep == 2 || reach > 29;
                        float tilt = (p.SwingStep == 1 ? -2.2f : 2.2f) * dir;
                        Arc(fxAdd, c, U, V, tail, cur, reach * 0.42f, reach * 1.02f, tilt, heavy ? 0xFFC890 : 0xC8DCFF, 0, 0.55f * fade, 1, 0);
                        Arc(fxAdd, c, U, V, tail, cur, reach * 0.9f, reach * 1.05f, tilt, 0xFFFFFF, 0, 0.9f * fade, 1, -1);
                    }
                }
                if (p.WhirlT > 0 && !own)
                {
                    float pr = 1f - p.WhirlT / 0.5f;
                    float fade = Math.Min(1f, (1f - pr) * 4f);
                    for (int k = 0; k < 3; k++)
                    {
                        float head = pr * M.TAU * 2f + k * M.TAU / 3 + p.AimAngle;
                        Arc(fxAdd, c - new V3(0, 3, 0), U, V, head - 1.7f, head, 12f, 41f, 0, 0xFFC890, 0, 0.5f * fade, 1, 0);
                        Arc(fxAdd, c - new V3(0, 3, 0), U, V, head - 1.7f, head, 37f, 42f, 0, 0xFFFFFF, 0, 0.85f * fade, 1, -1);
                    }
                }
            }
            for (int i = 0; i < G.Foes.Count; i++)
            {
                Enemy e = G.Foes[i];
                if (e.Dead || e.WeaponSwing <= 0) continue;
                float u = M.Clamp01(1f - e.WeaponSwing);
                float gy = Gh(e.X, e.Y);
                float big = e.Spr != null ? 1f + (e.Spr.Height / 24f - 1f) * 0.8f : 1f;
                float reach; int core, glow; float h;
                switch (e.Kind)
                {
                    case EnemyKind.Knight: reach = 46; core = 0xFFD8C8; glow = Red; h = 22; break;
                    case EnemyKind.Prior: reach = 52; core = 0xEAF3FF; glow = 0x8A7CE8; h = 24; break;
                    case EnemyKind.Crawler: reach = 24; core = 0xFFB0A0; glow = Red; h = 8; break;
                    default: reach = 30; core = 0xFFB0A0; glow = Red; h = 17; break;
                }
                h *= big;
                float fade = 0.65f * (1f - u * u);
                if (e.Kind == EnemyKind.Hollow)
                {
                    // an overhead chop: the band stands upright along the line of the attack
                    V3 fwd = new V3(M.Cos(e.AttackAng), 0, M.Sin(e.AttackAng)), up = new V3(0, 1, 0);
                    float cur = M.Lerp(1.7f, -0.5f, M.EaseOut(u)), tail = Math.Min(1.7f, cur + 2.2f);
                    V3 c = new V3(e.X, gy + h, e.Y);
                    Arc(fxAdd, c, fwd, up, tail, cur, reach * 0.35f, reach * 0.95f, 0, glow, 0, fade, 1, 0);
                    Arc(fxAdd, c, fwd, up, tail, cur, reach * 0.84f, reach * 0.98f, 0, core, 0, fade * 1.3f, 1, -1);
                }
                else
                {
                    float from = e.AttackAng + 1.4f, to = e.AttackAng - 1.5f;
                    float cur = M.Lerp(from, to, M.EaseOut(u)), tail = Math.Min(from, cur + 2.4f);
                    V3 c = new V3(e.X, gy + h, e.Y);
                    Arc(fxAdd, c, U, V, tail, cur, reach * 0.4f, reach, 1.6f, glow, 0, fade, 1, 0);
                    Arc(fxAdd, c, U, V, tail, cur, reach * 0.88f, reach * 1.03f, 1.6f, core, 0, fade * 1.3f, 1, -1);
                }
            }
        }

        // spells, after the world
        void AreasPost()
        {
            Game G = g;
            for (int i = 0; i < G.Areas.Count; i++)
            {
                Area a = G.Areas[i];
                float t = a.Dur > 0 ? M.Clamp01(a.T / a.Dur) : 0;
                float gy = Gh(a.X, a.Y);
                switch (a.Kind)
                {
                    case Area.Ring:
                        {
                            float r = a.R * M.EaseOut(t);
                            GSector(fxAdd, AddLift, a.X, a.Y, Math.Max(0, r - 5), r, 0, M.PI, a.Color, 0.9f * (1 - t));
                            GSector(fxAdd, AddLift, a.X, a.Y, 0, r, 0, M.PI, a.Color, 0.2f * (1 - t));
                        }
                        break;
                    case Area.Burst:
                        Bill(fxAdd, new V3(a.X, gy + 8, a.Y), a.R * (0.8f + t * 0.9f), 0xFFC070, 0.9f * (1 - t), 0);
                        GSector(fxAdd, AddLift, a.X, a.Y, a.R * t * 0.8f, a.R * (0.25f + t), 0, M.PI, 0xFFA040, 0.78f * (1 - t));
                        break;
                    case Area.Bolt:
                        Bolt(a, t);
                        break;
                    case Area.Sigil:
                        if (a.Armed)
                        {
                            float u = M.Clamp01((a.T - a.Delay) / 0.4f);
                            GSector(fxAdd, AddLift, a.X, a.Y, 0, a.R, 0, M.PI, 0xC9D3F5, 0.75f * (1 - u));
                            Bill(fxAdd, new V3(a.X, gy + 10, a.Y), a.R * 1.4f, 0xA7A0DC, 0.8f * (1 - u), 0);
                            Beam(new V3(a.X, gy, a.Y), 70 * (0.5f + u), a.R * 0.9f, 0xC9D3F5, 0.7f * (1 - u), 0);
                        }
                        break;
                }
            }
        }

        void Bolt(Area a, float t)
        {
            Rng r = new Rng(a.Seed + (int)(a.T * 40));
            V3 p0 = new V3(a.X, Gh(a.X, a.Y) + 20, a.Y), p1 = new V3(a.X2, Gh(a.X2, a.Y2) + 8, a.Y2);
            float al = 1 - t;
            V3 prev = p0;
            const int segs = 7;
            for (int k = 1; k <= segs; k++)
            {
                float u = k / (float)segs;
                V3 n = V3.Lerp(p0, p1, u);
                if (k < segs) n = n + new V3(r.Sym(7), r.Sym(5), r.Sym(7));
                Ribbon(fxAdd, prev, n, 6f, 0x58A8D8, 0.55f * al);
                Ribbon(fxAdd, prev, n, 1.8f, 0xE2F8FF, al);
                prev = n;
            }
            Bill(fxAdd, p1, 16, 0xC8E8FF, 0.85f * al, 0);
            Bill(fxAdd, p0, 7, 0xC8E8FF, 0.6f * al, 0);
        }

        void LootBeams()
        {
            Game G = g;
            for (int i = 0; i < G.Drops.Count; i++)
            {
                Pickup d = G.Drops[i];
                if (d.Z > 0) continue;
                int col = 0, strength = 0;
                if (d.Kind == Pickup.Equip && d.It != null && d.It.Rar >= Rarity.Fine) { col = Items.RarityColor[(int)d.It.Rar]; strength = 60 + (int)d.It.Rar * 40; }
                else if (d.Kind == Pickup.Quest) { col = 0xC8D8FF; strength = 190; }
                if (strength == 0) continue;
                float gy = Gh(d.X, d.Y);
                if (!BoxVisible(d.X - 12, gy, d.Y - 12, d.X + 12, gy + 60, d.Y + 12)) continue;
                V3 b = new V3(d.X, gy, d.Y);
                float s = strength / 255f;
                Bill(fxAdd, b + new V3(0, 8, 0), 13, col, 0.75f * s, 0);
                GDisc(fxAdd, AddLift, d.X, d.Y, 9, col, 0.5f * s, 0, 2);
                if (strength >= 140) Beam(b, 52, 3.4f, col, 0.85f * s, 0);
            }
        }

        // geometry helpers
        /// <summary>A square on the ground, following the terrain, with uv running -1..1 (for the round shapes 0 and 4).</summary>
        void GDisc(MeshB m, float lift, float x, float z, float r, int col, float alpha, int shape, int n)
        {
            byte e = A(alpha);
            if (e == 0) return;
            byte l = (byte)shape;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float u0 = -1 + 2f * i / n, u1 = -1 + 2f * (i + 1) / n, v0 = -1 + 2f * j / n, v1 = -1 + 2f * (j + 1) / n;
                    V3 a = Gp(x + u0 * r, z + v0 * r, lift), b = Gp(x + u1 * r, z + v0 * r, lift), c = Gp(x + u1 * r, z + v1 * r, lift), d = Gp(x + u0 * r, z + v1 * r, lift);
                    m.Raw(a, u0, v0, col, e, l); m.Raw(b, u1, v0, col, e, l); m.Raw(c, u1, v1, col, e, l);
                    m.Raw(a, u0, v0, col, e, l); m.Raw(c, u1, v1, col, e, l); m.Raw(d, u0, v1, col, e, l);
                }
        }

        /// <summary>A flat ring sector on the ground (half is the half-angle; π gives a full ring).</summary>
        void GSector(MeshB m, float lift, float x, float z, float r0, float r1, float ang, float half, int col, float alpha)
        {
            byte e = A(alpha);
            if (r0 < 0) r0 = 0;
            if (e == 0 || r1 <= r0 + 0.01f) return;
            if (!BoxVisible(x - r1, Gh(x, z) - 30, z - r1, x + r1, Gh(x, z) + 30, z + r1)) return;
            int na = M.Clamp((int)Math.Ceiling(half * 2 * Math.Max(r1, 6f) / 7f), 3, 120);
            int nr = M.Clamp((int)Math.Ceiling((r1 - r0) / 9f), 1, 16);
            for (int i = 0; i < na; i++)
            {
                float a0 = ang - half + 2 * half * i / na, a1 = ang - half + 2 * half * (i + 1) / na;
                float c0 = M.Cos(a0), s0 = M.Sin(a0), c1 = M.Cos(a1), s1 = M.Sin(a1);
                V3 pa = Gp(x + c0 * r0, z + s0 * r0, lift), pb = Gp(x + c1 * r0, z + s1 * r0, lift);
                for (int j = 1; j <= nr; j++)
                {
                    float rb = r0 + (r1 - r0) * j / nr;
                    V3 qa = Gp(x + c0 * rb, z + s0 * rb, lift), qb = Gp(x + c1 * rb, z + s1 * rb, lift);
                    m.Raw(pa, 0, 0, col, e, 1); m.Raw(qa, 0, 0, col, e, 1); m.Raw(qb, 0, 0, col, e, 1);
                    m.Raw(pa, 0, 0, col, e, 1); m.Raw(qb, 0, 0, col, e, 1); m.Raw(pb, 0, 0, col, e, 1);
                    pa = qa; pb = qb;
                }
            }
        }

        /// <summary>A band of half-width hw on the ground from one point to another.</summary>
        void GStrip(MeshB m, float lift, float x0, float z0, float x1, float z1, float hw, int col, float alpha)
        {
            byte e = A(alpha);
            if (e == 0) return;
            float dx = x1 - x0, dz = z1 - z0, len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 0.5f) return;
            float nx = -dz / len * hw, nz = dx / len * hw;
            int n = M.Clamp((int)Math.Ceiling(len / 8f), 1, 64), w = M.Clamp((int)Math.Ceiling(hw * 2 / 9f), 1, 6);
            for (int i = 0; i < n; i++)
            {
                float u0 = i / (float)n, u1 = (i + 1) / (float)n;
                for (int j = 0; j < w; j++)
                {
                    float v0 = -1 + 2f * j / w, v1 = -1 + 2f * (j + 1) / w;
                    V3 a = Gp(x0 + dx * u0 + nx * v0, z0 + dz * u0 + nz * v0, lift);
                    V3 b = Gp(x0 + dx * u1 + nx * v0, z0 + dz * u1 + nz * v0, lift);
                    V3 c = Gp(x0 + dx * u1 + nx * v1, z0 + dz * u1 + nz * v1, lift);
                    V3 d = Gp(x0 + dx * u0 + nx * v1, z0 + dz * u0 + nz * v1, lift);
                    m.Raw(a, 0, 0, col, e, 1); m.Raw(b, 0, 0, col, e, 1); m.Raw(c, 0, 0, col, e, 1);
                    m.Raw(a, 0, 0, col, e, 1); m.Raw(c, 0, 0, col, e, 1); m.Raw(d, 0, 0, col, e, 1);
                }
            }
        }

        /// <summary>
        /// A swept band in the air: points c + (U cos f + V sin f) r for f from f0 (the tail, alpha aTail) to f1 (the head,
        /// alpha aHead). vIn/vOut are the band's v at the inner and outer radius (shape 2 is bright where v is 0).
        /// tilt raises the band by that much per radian away from the head.
        /// </summary>
        void Arc(MeshB m, V3 c, V3 U, V3 V, float f0, float f1, float r0, float r1, float tilt, int col, float aTail, float aHead, float vIn, float vOut)
        {
            float span = Math.Abs(f1 - f0);
            if (span < 0.01f || aHead <= 0.003f) return;
            int n = M.Clamp((int)Math.Ceiling(span * r1 / 5f), 3, 48);
            V3 pi0 = default(V3), po0 = default(V3); byte e0 = 0;
            for (int k = 0; k <= n; k++)
            {
                float u = k / (float)n, f = f0 + (f1 - f0) * u;
                V3 dir = U * M.Cos(f) + V * M.Sin(f);
                V3 lift = new V3(0, tilt * (f - f1), 0);
                V3 pi = c + dir * r0 + lift, po = c + dir * r1 + lift;
                byte e = A(M.Lerp(aTail, aHead, u));
                if (k > 0)
                {
                    m.Raw(pi0, 0, vIn, col, e0, 2); m.Raw(po0, 0, vOut, col, e0, 2); m.Raw(po, 1, vOut, col, e, 2);
                    m.Raw(pi0, 0, vIn, col, e0, 2); m.Raw(po, 1, vOut, col, e, 2); m.Raw(pi, 1, vIn, col, e, 2);
                }
                pi0 = pi; po0 = po; e0 = e;
            }
        }

        /// <summary>An upright band of light facing the camera, fading from a0 at the bottom to a1 at the top.</summary>
        void Beam(V3 b, float h, float w, int col, float a0, float a1)
        {
            V3 side = new V3(camR.X, 0, camR.Z);
            if (side.Len < 1e-3f) return;
            side = side.Norm * (w * 0.5f);
            V3 top = b + new V3(0, h, 0);
            byte e0 = A(a0), e1 = A(a1);
            fxAdd.Raw(b - side, 0, -1, col, e0, 2); fxAdd.Raw(b + side, 0, 1, col, e0, 2); fxAdd.Raw(top + side, 1, 1, col, e1, 2);
            fxAdd.Raw(b - side, 0, -1, col, e0, 2); fxAdd.Raw(top + side, 1, 1, col, e1, 2); fxAdd.Raw(top - side, 1, -1, col, e1, 2);
        }
    }
}
