using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>Posing everything that moves: heroes, foes, villagers, loot and missiles, every frame.</summary>
    public sealed unsafe partial class Renderer3D
    {
        sealed class ActorView { public float Yaw, LX, LY, Move, Spd; public int Seen; }
        readonly Dictionary<object, ActorView> views = new Dictionary<object, ActorView>();
        int frameNo; float lastFrameT = -1, fdt = 1f / 60f;
        V3 camR, camU;

        // where posed vertices go and how they are coloured
        MeshB tgt; int tTint, tAmt, tEm = -1; bool tFx; byte tAlpha = 255; int tFxCol = -1;

        struct Pose
        {
            public HumanSpec S;
            public float X, Z, Ground, Lift, Yaw, Scale;
            public float Phase, Move, Bob, Lean, Twist, HeadPitch, Tumble, Fall, Tuck, CapeK;
            public V3 HandR, HandL, PoleR, PoleL;
            public MeshB WeapR, WeapL; public V3 WR, WRUp, WL, WLUp;
        }

        Pose heroPose; bool heroPoseValid;

        void Paint(MeshB target, int tint, int amt, int em)
        {
            tgt = target; tTint = tint; tAmt = amt; tEm = em; tFx = false; tAlpha = 255; tFxCol = -1;
        }

        void PaintFx(MeshB target, byte alpha, int flatCol)
        {
            tgt = target; tFx = true; tAlpha = alpha; tFxCol = flatCol;
        }

        void Put(MeshB src, Xf xf)
        {
            if (src == null || src.N == 0) return;
            int start = tgt.N;
            tgt.AppendTint(src, xf, tTint, tAmt, tEm);
            if (!tFx) return;
            byte r = (byte)((tFxCol >> 16) & 255), gg = (byte)((tFxCol >> 8) & 255), b = (byte)(tFxCol & 255);
            for (int i = start; i < tgt.N; i++)
            {
                tgt.V[i].L = 1; tgt.V[i].E = tAlpha;
                if (tFxCol >= 0) { tgt.V[i].R = r; tgt.V[i].G = gg; tgt.V[i].B = b; }
            }
        }

        ActorView GetView(object o, float x, float y, float yaw)
        {
            ActorView v;
            if (!views.TryGetValue(o, out v)) { v = new ActorView(); v.Yaw = yaw; v.LX = x; v.LY = y; views[o] = v; }
            v.Seen = frameNo;
            return v;
        }

        static float Turn(float cur, float want, float step)
        {
            float d = M.AngDiff(cur, want);
            if (d > step) d = step; else if (d < -step) d = -step;
            return cur + d;
        }

        static V3 Lerp(V3 a, V3 b, float t) { return V3.Lerp(a, b, M.Clamp01(t)); }

        static Pose NewPose(HumanSpec s, float x, float z, float ground, float scale)
        {
            Pose p = new Pose();
            p.S = s; p.X = x; p.Z = z; p.Ground = ground; p.Scale = scale;
            p.PoleR = new V3(-1f, -0.25f, 0.7f); p.PoleL = new V3(-1f, -0.25f, -0.7f);
            float b = s.Bulk;
            p.HandR = new V3(0.9f, Rigs.ShoulderY - 8.7f, Rigs.ShoulderZ * b + 0.9f);
            p.HandL = new V3(0.9f, Rigs.ShoulderY - 8.7f, -Rigs.ShoulderZ * b - 0.9f);
            p.WRUp = new V3(0, 0, 1); p.WLUp = new V3(0, 0, -1);
            return p;
        }

        static V3 ShR(HumanSpec s) { return new V3(0, Rigs.ShoulderY, Rigs.ShoulderZ * s.Bulk); }
        static V3 ShL(HumanSpec s) { return new V3(0, Rigs.ShoulderY, -Rigs.ShoulderZ * s.Bulk); }

        /// <summary>Walk cycle extras: arm swing and body bob.</summary>
        static void Gait(ref Pose p, bool swingR, bool swingL)
        {
            float s = M.Sin(p.Phase) * p.Move;
            if (swingR) p.HandR = p.HandR + new V3(-s * 2.6f, Math.Abs(s) * 0.5f, 0);
            if (swingL) p.HandL = p.HandL + new V3(s * 2.6f, Math.Abs(s) * 0.5f, 0);
            p.Bob += -Math.Abs(M.Cos(p.Phase)) * 0.55f * p.Move + 0.2f * p.Move;
        }

        // frame entry
        void BuildActors(bool play)
        {
            frameNo++;
            float t = g.UiT;
            fdt = lastFrameT < 0 ? 1f / 60f : M.Clamp(t - lastFrameT, 0f, 0.1f);
            lastFrameT = t;
            camR = new V3(View[0], View[4], View[8]); camU = new V3(View[1], View[5], View[9]);
            Array.Clear(push, 0, push.Length);
            cutOn = false;
            Paint(dyn, 0, 0, -1);
            heroPoseValid = false;
            if (!play) return;
            Game G = g;
            int pi = 0;
            if (G.P != null)
            {
                DrawHero(G.P, true);
                if (!G.P.Dead) { push[0] = G.P.X; push[2] = G.P.Y; push[3] = 13; pi = 1; }
            }
            for (int k = 0; k < G.Others.Count; k++)
            {
                Player o = G.Others[k];
                if (o == G.P) continue;
                DrawHero(o, false);
                if (pi < 4 && !o.Dead) { push[pi * 4] = o.X; push[pi * 4 + 2] = o.Y; push[pi * 4 + 3] = 13; pi++; }
            }
            if (G.P != null && !G.P.Dead && !Fp) { cutHero = new V3(G.P.X, HeightAt(G.P.X, G.P.Y) + 16, G.P.Y); cutOn = true; }
            for (int i = 0; i < G.Foes.Count; i++) DrawFoe(G.Foes[i]);
            for (int i = 0; i < G.Npcs.Count; i++) DrawVillager(G.Npcs[i]);
            for (int i = 0; i < G.Drops.Count; i++) DrawDrop(G.Drops[i]);
            for (int i = 0; i < G.Shots.Count; i++) DrawMissile(G.Shots[i]);
            if (heroPoseValid && !Fp)
                for (int i = 0; i < G.Ghosts.Count; i++)
                {
                    Ghost gh = G.Ghosts[i];
                    Pose ps = heroPose;
                    ps.X = gh.X; ps.Z = gh.Y; ps.Ground = HeightAt(gh.X, gh.Y);
                    PaintFx(fxAdd, (byte)M.Clamp((int)(120 * gh.Life / Math.Max(0.01f, gh.Max)), 0, 255), gh.Color);
                    tTint = 0; tAmt = 0; tEm = -1;
                    DrawHuman(ref ps);
                }
            Paint(dyn, 0, 0, -1);
            if (frameNo % 120 == 0)
            {
                List<object> dead = null;
                foreach (KeyValuePair<object, ActorView> kv in views)
                    if (frameNo - kv.Value.Seen > 240) { if (dead == null) dead = new List<object>(); dead.Add(kv.Key); }
                if (dead != null) foreach (object o in dead) views.Remove(o);
            }
        }

        bool Near(float x, float y, float gy, float r, float h)
        {
            return BoxVisible(x - r, gy - 6, y - r, x + r, gy + h, y + r);
        }

        // heroes
        void DrawHero(Player p, bool local)
        {
            float gy = HeightAt(p.X, p.Y);
            bool own = local && Fp;             // seen from inside: the body only casts its shadow
            if (!own && !Near(p.X, p.Y, gy, 30, 60)) return;
            HumanSpec s = Rigs.Hero(p.Class);
            ActorView v = GetView(p, p.X, p.Y, p.AimAngle);
            float want = p.AimAngle;
            if (p.DodgeT > 0 && Math.Abs(p.DodgeDX) + Math.Abs(p.DodgeDY) > 0.01f) want = M.Atan2(p.DodgeDY, p.DodgeDX);
            else if (p.DashT > 0 && Math.Abs(p.DashDX) + Math.Abs(p.DashDY) > 0.01f) want = M.Atan2(p.DashDY, p.DashDX);
            if (!p.Dead) v.Yaw = Turn(v.Yaw, want, 24f * fdt);
            v.Move += ((p.Moving && !p.Dead ? 1f : 0f) - v.Move) * Math.Min(1f, 10f * fdt);

            Pose ps = NewPose(s, p.X, p.Y, gy, Rigs.HumanScale);
            ps.Yaw = v.Yaw; ps.Move = v.Move; ps.Phase = p.AnimT * 14.1f;
            if (!p.Moving) ps.Bob = M.Sin(p.AnimT * 5f) * 0.18f;
            Item it = p.Equip[Player.SlotWeapon];
            int type = it != null ? it.Base : p.Def.WeaponTypes[0], tier = it != null ? it.Tier : 0;
            WeaponType wt = Weapons.Types[type];
            V3 SR = ShR(s), SL = ShL(s);
            bool swingR = true, swingL = true;

            switch (p.Class)
            {
                case ClassId.Warrior:
                    {
                        ps.WeapR = Rigs.Weapon(type, tier);
                        bool axe = type == 1;
                        if (p.WhirlT > 0)
                        {
                            ps.Yaw += (1f - p.WhirlT / 0.5f) * M.TAU * 2f;
                            ps.HandR = SR + new V3(1.2f, -0.6f, 8.4f); ps.WR = new V3(0.35f, 0.05f, 1); ps.WRUp = new V3(1, 0, 0);
                            ps.HandL = axe ? ps.HandR - new V3(0.35f, 0.05f, 1).Norm * 3f : SL + new V3(0.6f, -1.8f, -8.2f);
                            ps.Lean = 0.12f; swingR = swingL = false;
                        }
                        else if (p.AtkT > 0 && !wt.Ranged)
                        {
                            float pr = 1f - p.AtkT / Math.Max(0.01f, p.AtkDur);
                            float th = M.Lerp(p.SwingFrom, p.SwingTo, M.EaseOut(pr));
                            // the torso turns with the swing; the arm covers the rest so the blade ends up at aim + th
                            ps.Twist = th * 0.3f;
                            float ha = th - ps.Twist;
                            V3 d = new V3(M.Cos(ha), -0.1f, M.Sin(ha));
                            ps.HandR = SR + d * 8.7f;
                            ps.WR = new V3(M.Cos(ha), 0.06f, M.Sin(ha));
                            float dir = p.SwingTo >= p.SwingFrom ? 1 : -1;
                            ps.WRUp = new V3(-M.Sin(ha) * dir, 0, M.Cos(ha) * dir);
                            ps.Lean = 0.1f + (p.SwingStep == 2 ? 0.12f : 0);
                            ps.PoleR = new V3(-0.4f, -1f, 0.5f);
                            ps.HandL = axe ? ps.HandR - ps.WR.Norm * 3.2f : SL + new V3(-2.2f, -7.2f, -2.6f);
                            swingR = false; swingL = false;
                        }
                        else if (p.DashT > 0)
                        {
                            ps.Lean = 0.35f;
                            ps.HandR = SR + new V3(7.6f, -3.6f, -1.6f); ps.WR = new V3(1, 0.12f, -0.15f); ps.WRUp = new V3(0, 0, 1);
                            ps.HandL = axe ? ps.HandR - new V3(1, 0.12f, -0.15f).Norm * 3f : SL + new V3(-3f, -6.5f, -2f);
                            swingR = swingL = false;
                        }
                        else if (p.Blocking)
                        {
                            ps.HandL = SL + new V3(7.0f, -2.6f, 2.4f); ps.PoleL = new V3(-0.3f, -1, -0.6f);
                            ps.WeapL = Rigs.Shield(); ps.WL = new V3(1, 0, 0.12f); ps.WLUp = new V3(0, 1, 0);
                            ps.HandR = SR + new V3(4.2f, -5.6f, 0.4f); ps.WR = new V3(0.5f, 0.7f, 0.25f); ps.WRUp = new V3(0, 0, 1);
                            ps.Lean = 0.14f; swingR = swingL = false;
                        }
                        else
                        {
                            ps.HandR = SR + new V3(2.8f, -7.4f, 1.0f); ps.WR = new V3(0.78f, 0.6f, -0.18f); ps.WRUp = new V3(0, 0, 1);
                            if (axe) { ps.HandL = ps.HandR - new V3(0.78f, 0.6f, -0.18f).Norm * 3.4f; swingL = false; }
                            swingR = false;
                            ps.HandR = ps.HandR + new V3(0, Math.Abs(M.Sin(ps.Phase)) * 0.6f * ps.Move, 0);
                        }
                    }
                    break;
                case ClassId.Mage:
                    {
                        ps.WeapR = Rigs.Weapon(type, tier);
                        bool staff = type == 2;
                        if (p.CastT > 0 || p.AtkT > 0)
                        {
                            ps.HandR = SR + new V3(7.4f, 3.6f, -0.6f); ps.PoleR = new V3(-0.2f, -1, 0.6f);
                            ps.WR = staff ? new V3(0.42f, 0.9f, 0.05f) : new V3(0.95f, 0.18f, -0.12f); ps.WRUp = new V3(0, 0, 1);
                            ps.HandL = SL + new V3(6.2f, 1.2f, 2.4f); ps.PoleL = new V3(-0.2f, -1, -0.6f);
                            ps.Lean = 0.06f; swingR = swingL = false;
                        }
                        else
                        {
                            ps.HandR = SR + new V3(1.8f, -7.0f, 1.2f);
                            ps.WR = staff ? new V3(0.14f, 1f, 0.06f) : new V3(0.6f, -0.75f, 0.12f); ps.WRUp = new V3(1, 0, 0);
                            if (staff) ps.WRUp = new V3(0, 0, 1);
                            swingR = false;
                        }
                    }
                    break;
                case ClassId.Rogue:
                    {
                        ps.WeapR = Rigs.Weapon(type, tier);
                        bool dual = type == 4;
                        V3 restR = SR + new V3(3.4f, -6.4f, 0.6f), restL = SL + new V3(3.0f, -6.6f, -0.4f);
                        ps.HandR = restR; ps.WR = new V3(0.85f, 0.32f, 0.22f); ps.WRUp = new V3(0, 0, 1);
                        if (dual) { ps.WeapL = Rigs.Weapon(type, tier); ps.HandL = restL; ps.WL = new V3(0.45f, -0.85f, -0.2f); ps.WLUp = new V3(1, 0, 0); swingL = false; }
                        swingR = false;
                        if (p.AtkT > 0 && !wt.Ranged)
                        {
                            float pr = 1f - p.AtkT / Math.Max(0.01f, p.AtkDur);
                            float th = M.Sin(pr * M.PI);
                            bool left = dual && p.SwingStep == 1;
                            if (!left) { ps.HandR = Lerp(restR, SR + new V3(8.8f, -1.2f, -2.4f), th); ps.WR = Lerp(ps.WR, new V3(1, 0.04f, -0.1f), th); }
                            else { ps.HandL = Lerp(restL, SL + new V3(8.8f, -1.2f, 2.4f), th); ps.WL = Lerp(ps.WL, new V3(1, 0.04f, 0.1f), th); ps.WLUp = new V3(0, 0, -1); }
                            ps.Lean = 0.12f + th * 0.12f; ps.Twist = (left ? 0.3f : -0.3f) * th;
                        }
                        else if (p.DashT > 0)
                        {
                            ps.Lean = 0.42f;
                            ps.HandR = SR + new V3(-3.6f, -6.6f, 2.0f); ps.WR = new V3(-0.4f, -0.4f, 0.8f);
                            if (dual) ps.HandL = SL + new V3(-3.6f, -6.6f, -2.0f);
                        }
                        if (p.StealthT > 0) { ps.Lean += 0.22f; ps.Bob -= 1.0f; }
                    }
                    break;
                default:
                    {
                        bool aiming = p.AtkT > 0 || p.BowT > 0 || p.CastT > 0 || p.CombatT < 1.6f;
                        float half = type == 6 ? 13.5f : 9.8f;
                        float draw = 0;
                        if (aiming)
                        {
                            draw = 0.8f;
                            if (p.AtkT > 0) { float pr = 1f - p.AtkT / Math.Max(0.01f, p.AtkDur); draw = pr < 0.18f ? 0 : M.Clamp01((pr - 0.18f) / 0.5f); }
                            ps.HandL = SL + new V3(8.9f, 1.0f, 1.3f); ps.PoleL = new V3(0, -1, -0.5f);
                            ps.WL = new V3(1, 0, 0); ps.WLUp = new V3(0, 1, -0.22f);
                            float tipX = 0.4f - 4.2f * (half / 13.5f) + 0.9f, nock = tipX - 0.4f - (float)Math.Round(draw * 4) / 4f * 7.5f;
                            ps.HandR = ps.HandL + new V3((float)nock - 0.6f, 0.3f, 0.7f); ps.PoleR = new V3(-0.4f, -0.3f, 1);
                            ps.Twist = -0.28f; ps.Lean = 0.04f; ps.HeadPitch = -0.05f;
                            swingR = swingL = false;
                        }
                        else
                        {
                            ps.HandL = SL + new V3(1.8f, -7.6f, -0.9f);
                            ps.WL = new V3(0.85f, -0.3f, 0.1f); ps.WLUp = new V3(0.3f, 0.95f, 0);
                            swingL = false;
                        }
                        ps.WeapL = Rigs.Bow(half, tier, draw, false);
                    }
                    break;
            }
            Gait(ref ps, swingR, swingL);
            if (p.DodgeT > 0)
            {
                float pr = 1f - p.DodgeT / 0.26f;
                ps.Tumble = pr * M.TAU; ps.Tuck = M.Sin(pr * M.PI); ps.Move = 0;
                ps.HandR = SR + new V3(3.0f, -4.4f, -1.2f); ps.HandL = SL + new V3(3.0f, -4.4f, 1.2f);
            }
            int tint = 0, amt = 0, em = -1;
            if (p.FlashT > 0) { tint = 0xFFFFFF; amt = 200; em = 110; }
            else if (p.FreezeT > 0) { tint = 0x9BDCF4; amt = 140; }
            else if (p.OathT > 0) { tint = 0xFFD070; amt = 50 + (int)(30 * M.Sin(g.UiT * 8)); em = 50; }
            else if (p.PoisonT > 0) { tint = 0x6FA82E; amt = 70; }
            else if (p.NextCrit) { tint = 0xC08CF0; amt = 50; em = 30; }
            if (p.IFrames > 0 && p.DodgeT <= 0 && p.DashT <= 0 && ((int)(g.UiT * 30) & 1) == 0) { tint = 0xFFFFFF; amt = Math.Max(amt, 100); }
            if (p.Dead)
            {
                float dt = M.Clamp01((local ? g.DeathT : p.DeadT) / 0.45f);
                ps.Fall = dt; ps.Move = 0; tint = 0x3A1418; amt = (int)(120 * dt);
            }
            if (own) Paint(selfShadow, 0, 0, -1);
            else if (p.StealthT > 0 && !p.Dead)
            {
                Paint(fxAlpha, 0x2A2448, 150, -1);
                PaintFx(fxAlpha, 120, -1);
            }
            else Paint(dyn, tint, amt, em);
            DrawHuman(ref ps);
            Paint(dyn, 0, 0, -1);
            if (local) { heroPose = ps; heroPose.WeapL = ps.WeapL; heroPoseValid = true; }
        }

        // bodies
        void DrawHuman(ref Pose ps)
        {
            HumanSpec s = ps.S;
            if (!s.Built) Rigs.Build(s);
            float b = s.Bulk;
            Xf root = Xf.Move(ps.X, ps.Ground + ps.Lift, ps.Z) * Xf.Yaw(ps.Yaw) * Xf.Scale(ps.Scale);
            if (ps.Fall > 0) root = root * Xf.Move(-ps.Fall * 1.2f, ps.Fall * 2.4f, 0) * Xf.RotZ(ps.Fall * 1.45f);
            if (ps.Tumble != 0) root = root * Xf.Move(0, 9.5f, 0) * Xf.RotZ(-ps.Tumble) * Xf.Move(0, -9.5f, 0);
            float mv = ps.Move;
            float hipY = Rigs.HipY + ps.Bob - ps.Tuck * 2.5f;
            Xf torso = root * Xf.Move(0, hipY, 0) * Xf.Yaw(ps.Twist) * Xf.RotZ(-(s.Lean + ps.Lean));
            V3 fwd = new V3(1, 0, 0);
            if (!s.Robe)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float ph = ps.Phase + (side > 0 ? 0 : M.PI);
                    float fx = M.Sin(ph) * 3.4f * mv + ps.Tuck * 2.6f;
                    float fy = Math.Max(0, M.Cos(ph)) * 2.3f * mv + ps.Tuck * 5.5f;
                    V3 hip = new V3(0, hipY, side * 1.85f * b);
                    V3 ankle = new V3(fx, fy + Rigs.AnkleY, side * 2.0f * b);
                    V3 knee, end;
                    Rigs.Ik(hip, ankle, Rigs.ThighLen, Rigs.ShinLen, new V3(1, 0, 0.12f * side), out knee, out end);
                    Put(s.Thigh, root * Xf.Move(hip) * Rigs.Seg(knee - hip, fwd));
                    Put(s.Shin, root * Xf.Move(knee) * Rigs.Seg(end - knee, fwd));
                    float toe = -M.Sin(ph) * 0.22f * mv;
                    Put(s.Boot, root * Xf.Move(end.X, end.Y - Rigs.AnkleY, end.Z) * Xf.RotZ(toe));
                }
            }
            else
            {
                float sway = M.Sin(ps.Phase) * 0.06f * mv;
                Put(s.Skirt, root * Xf.Move(0, hipY, 0) * Xf.RotZ(-0.06f * mv) * Xf.RotX(sway));
            }
            Put(s.TorsoM, torso);
            Xf head = torso * Xf.Move(0.2f, Rigs.NeckY, 0) * Xf.RotZ(-ps.HeadPitch);
            if (s.HeadScale != 1f) head = head * Xf.Scale(s.HeadScale);
            Put(s.HeadM, head);
            V3 SR = ShR(s), SL = ShL(s);
            V3 elbow, wrist;
            Rigs.Ik(SR, ps.HandR, Rigs.UpperLen, Rigs.ForeLen, ps.PoleR, out elbow, out wrist);
            Put(s.UpperArmR, torso * Xf.Move(SR) * Rigs.Seg(elbow - SR, fwd));
            Put(s.ForeArm, torso * Xf.Move(elbow) * Rigs.Seg(wrist - elbow, fwd));
            V3 hand = wrist + (wrist - elbow).Norm * 0.75f;
            if (ps.WeapR != null) Put(ps.WeapR, torso * Xf.Move(hand) * Rigs.Orient(ps.WR, ps.WRUp));
            Rigs.Ik(SL, ps.HandL, Rigs.UpperLen, Rigs.ForeLen, ps.PoleL, out elbow, out wrist);
            Put(s.UpperArmL, torso * Xf.Move(SL) * Rigs.Seg(elbow - SL, fwd));
            Put(s.ForeArm, torso * Xf.Move(elbow) * Rigs.Seg(wrist - elbow, fwd));
            hand = wrist + (wrist - elbow).Norm * 0.75f;
            if (ps.WeapL != null) Put(ps.WeapL, torso * Xf.Move(hand) * Rigs.Orient(ps.WL, ps.WLUp));
            if (s.CapeM != null)
            {
                float flare = 0.1f + 0.42f * mv + ps.CapeK + 0.05f * M.Sin(ps.Phase * 2f) * mv + 0.03f * M.Sin(g.UiT * 1.7f + ps.X * 0.05f);
                Put(s.CapeM, torso * Xf.Move(-2.3f * b, 8.9f, 0) * Xf.RotZ(-flare));
            }
            if (s.ScarfM != null)
                Put(s.ScarfM, torso * Xf.Move(-1.6f * b, 9.6f, 0.6f) * Xf.Yaw(M.Sin(g.UiT * 5f + ps.X * 0.1f) * 0.25f) * Xf.RotZ(0.1f + 0.25f * mv));
        }

        // foes
        void DrawFoe(Enemy e)
        {
            float gy = HeightAt(e.X, e.Y);
            float big = e.Spr != null ? e.Spr.Height / 24f : 1f;
            if (!Near(e.X, e.Y, gy, 40 * big, 70 * big)) return;
            ActorView v = GetView(e, e.X, e.Y, e.Facing > 0 ? 0.3f : M.PI - 0.3f);
            if (fdt > 0.0005f)
            {
                float vx = (e.X - v.LX) / fdt, vz = (e.Y - v.LY) / fdt;
                float sp = (float)Math.Sqrt(vx * vx + vz * vz);
                if (sp > 400) sp = 0;            // teleported
                v.Spd += (sp - v.Spd) * Math.Min(1f, 12f * fdt);
                if (sp > 5 && sp < 400) v.Move = M.Atan2(vz, vx);   // reuse Move as the heading of travel
                v.LX = e.X; v.LY = e.Y;
            }
            bool attacking = !e.Dead && (e.State == Enemy.Windup || e.WeaponSwing > 0 || (e.State == Enemy.Special && (e.Kind == EnemyKind.Wolf || e.Kind == EnemyKind.AlphaWolf || e.Kind == EnemyKind.Knight)));
            float want = v.Yaw;
            if (attacking) want = e.AttackAng;
            else if (v.Spd > 8) want = v.Move;
            else if (e.Aggro)
            {
                Player tp = e.Target ?? g.P;
                if (tp != null) want = M.Atan2(tp.Y - e.Y, tp.X - e.X);
            }
            else want = e.Facing > 0 ? 0.3f : M.PI - 0.3f;
            if (!e.Dead) v.Yaw = Turn(v.Yaw, want, (attacking ? 16f : 9f) * fdt);
            float moving = M.Clamp01(v.Spd / Math.Max(10f, e.Speed * 0.6f));

            int tint = 0, amt = 0, em = -1;
            float alpha = 1f;
            float deadT = 0;
            if (e.Dead) { deadT = M.Clamp01(e.DeathT / 0.7f); tint = 0x584688; amt = 120 + (int)(120 * deadT); em = (int)(80 * deadT); }
            else
            {
                if (e.FlashT > 0) { tint = 0xFFFFFF; amt = 220; em = 120; }
                else if (e.FreezeT > 0) { tint = 0x9BDCF4; amt = 150; }
                else if (e.State == Enemy.Windup)
                {
                    float pr = e.StateT / Math.Max(0.01f, e.StateDur);
                    if (pr > 0.7f || ((int)(pr * 9) & 1) == 0) { tint = 0xFF7060; amt = 60 + (int)(pr * 70); em = 30; }
                }
                else if (e.BurnT > 0) { tint = 0xEC8A32; amt = 80; }
                else if (e.PoisonT > 0) { tint = 0x6FA82E; amt = 70; }
                else if (e.StunT > 0) { tint = 0xFFF8DC; amt = 40; }
                if (e.State == Enemy.Vanish) { float tt = e.StateT / Math.Max(0.01f, e.StateDur); alpha = Math.Abs(1 - tt * 2); }
            }
            if (alpha < 0.98f) { Paint(fxAlpha, tint, amt, em); PaintFx(fxAlpha, (byte)(alpha * 230), -1); }
            else Paint(dyn, tint, amt, em);
            switch (e.Kind)
            {
                case EnemyKind.Wolf: case EnemyKind.AlphaWolf: DrawWolf(e, v, gy, moving, deadT); break;
                case EnemyKind.Crawler: DrawCrawler(e, v, gy, moving, deadT); break;
                case EnemyKind.Wisp: DrawWisp(e, v, gy, deadT); break;
                default: DrawFoeHuman(e, v, gy, moving, deadT, big); break;
            }
            Paint(dyn, 0, 0, -1);
        }

        void DrawFoeHuman(Enemy e, ActorView v, float gy, float moving, float deadT, float big)
        {
            HumanSpec s = Rigs.Foe(e);
            float scale = Rigs.HumanScale * (1f + (big - 1f) * 0.8f);
            Pose ps = NewPose(s, e.X, e.Y, gy, scale);
            ps.Yaw = v.Yaw; ps.Move = moving; ps.Phase = e.AnimT * (e.Kind == EnemyKind.Hollow ? 10f : 13f);
            if (moving < 0.1f) ps.Bob = M.Sin(e.AnimT * 4f) * 0.2f;
            if (e.FadeIn < 1) ps.Lift = -(1 - e.FadeIn) * 22f;
            V3 SR = ShR(s), SL = ShL(s);
            float pr = e.State == Enemy.Windup ? M.Clamp01(e.StateT / Math.Max(0.01f, e.StateDur)) : 0;
            bool swingR = true, swingL = true;
            float sw = e.WeaponSwing > 0 ? 1f - e.WeaponSwing : -1f;
            switch (e.Kind)
            {
                case EnemyKind.Hollow:
                    {
                        ps.WeapR = Rigs.FoeWeapon(EnemyKind.Hollow);
                        V3 rest = SR + new V3(5.4f, -5.8f, 0.4f);
                        V3 up = SR + new V3(-1.2f, 8.0f, 1.6f);
                        ps.HandR = rest; ps.WR = new V3(0.75f, -0.45f, 0.35f); ps.WRUp = new V3(0, 1, 0);
                        ps.HandL = SL + new V3(6.4f, -4.6f, 0.8f); ps.PoleL = new V3(-0.3f, -1, -0.5f);
                        ps.Twist = M.Sin(ps.Phase * 0.5f) * 0.12f * moving;
                        if (e.State == Enemy.Windup && e.AttackKind == Game.AkArc)
                        {
                            float k = M.EaseOut(pr);
                            ps.HandR = Lerp(rest, up, k); ps.WR = Lerp(ps.WR, new V3(-0.35f, 0.9f, 0.2f), k); ps.Lean = -0.25f * k;
                        }
                        else if (sw >= 0)
                        {
                            float k = M.EaseOut(sw);
                            ps.HandR = Lerp(up, SR + new V3(7.6f, -3.6f, -1.4f), k); ps.WR = Lerp(new V3(-0.35f, 0.9f, 0.2f), new V3(0.6f, -0.8f, -0.1f), k);
                            ps.Lean = 0.3f * k;
                        }
                        swingR = swingL = false;
                    }
                    break;
                case EnemyKind.Archer:
                    {
                        bool aim = e.Aggro || e.State == Enemy.Windup;
                        float draw = e.State == Enemy.Windup && e.AttackKind == Game.AkAim ? pr : 0.25f;
                        if (aim)
                        {
                            ps.HandL = SL + new V3(8.9f, 1.0f, 1.3f); ps.PoleL = new V3(0, -1, -0.5f);
                            ps.WL = new V3(1, 0, 0); ps.WLUp = new V3(0, 1, -0.2f);
                            float tipX = 0.4f - 4.2f * (9.8f / 13.5f) + 0.9f, nock = tipX - 0.4f - (float)Math.Round(draw * 4) / 4f * 7.5f;
                            ps.HandR = ps.HandL + new V3(nock - 0.6f, 0.3f, 0.7f); ps.PoleR = new V3(-0.4f, -0.3f, 1);
                            ps.Twist = -0.28f; swingR = swingL = false;
                        }
                        else { ps.HandL = SL + new V3(1.8f, -7.6f, -0.9f); ps.WL = new V3(0.85f, -0.3f, 0.1f); ps.WLUp = new V3(0.3f, 0.95f, 0); swingL = false; }
                        ps.WeapL = Rigs.Bow(9.8f, 0, draw, true);
                    }
                    break;
                case EnemyKind.Acolyte:
                    {
                        ps.WeapR = Rigs.FoeWeapon(EnemyKind.Acolyte);
                        V3 rest = SR + new V3(1.8f, -6.6f, 1.0f);
                        ps.HandR = rest; ps.WR = new V3(0.1f, 1, 0.05f); ps.WRUp = new V3(0, 0, 1);
                        ps.HandL = SL + new V3(3.0f, -6.0f, 1.2f);
                        if (e.State == Enemy.Windup)
                        {
                            float k = M.EaseOut(pr);
                            ps.HandR = Lerp(rest, SR + new V3(3.6f, 7.6f, 0.4f), k); ps.PoleR = new V3(-0.2f, -1, 0.6f);
                            ps.WR = Lerp(ps.WR, new V3(0.3f, 1f, 0), k);
                            ps.HandL = Lerp(ps.HandL, SL + new V3(6.0f, 2.8f, 1.6f), k); ps.PoleL = new V3(-0.2f, -1, -0.6f);
                            ps.HeadPitch = -0.2f * k;
                        }
                        swingR = swingL = false;
                    }
                    break;
                case EnemyKind.Knight:
                    {
                        ps.WeapR = Rigs.FoeWeapon(EnemyKind.Knight);
                        V3 rest = SR + new V3(4.4f, -6.0f, -2.6f);
                        ps.HandR = rest; ps.WR = new V3(0.6f, 0.74f, -0.3f); ps.WRUp = new V3(0, 0, 1);
                        bool twoHands = true;
                        if (e.State == Enemy.Windup && e.AttackKind == Game.AkArc)
                        {
                            float k = M.EaseOut(pr);
                            ps.HandR = Lerp(rest, SR + new V3(-0.6f, 5.6f, 1.4f), k); ps.WR = Lerp(ps.WR, new V3(-0.55f, 0.55f, 0.62f), k);
                            ps.Twist = 0.45f * k;
                        }
                        else if (sw >= 0)
                        {
                            float th = M.Lerp(1.4f, -1.5f, M.EaseOut(sw));
                            ps.HandR = SR + new V3(M.Cos(th), -0.15f, M.Sin(th)) * 8.2f; ps.WR = new V3(M.Cos(th), 0, M.Sin(th)); ps.WRUp = new V3(M.Sin(th), 0, -M.Cos(th));
                            ps.Twist = th * 0.4f; ps.Lean = 0.15f; ps.PoleR = new V3(-0.4f, -1, 0.5f);
                        }
                        else if ((e.State == Enemy.Windup && e.AttackKind == Game.AkLine) || (e.State == Enemy.Special && e.AttackKind == Game.AkLine))
                        {
                            ps.Lean = e.State == Enemy.Special ? 0.45f : 0.3f * pr;
                            ps.HandR = SR + new V3(6.6f, -5.0f, -2.2f); ps.WR = new V3(1, -0.08f, -0.12f);
                            if (e.State == Enemy.Special) { ps.Move = 1; ps.Phase = e.StateT * 30f; }
                        }
                        else if (e.State == Enemy.Windup && e.AttackKind == Game.AkCircle)
                        {
                            float k = M.EaseOut(pr);
                            ps.HandR = Lerp(rest, new V3(2.2f, 15.5f, 0.6f), k); ps.WR = Lerp(ps.WR, new V3(-0.15f, 1, 0), k); ps.PoleR = new V3(0, -1, 1);
                        }
                        else if (e.State == Enemy.Recover && e.AttackKind == Game.AkCircle)
                        {
                            ps.HandR = SR + new V3(6.0f, -5.4f, -3.4f); ps.WR = new V3(0.3f, -0.95f, 0); ps.WRUp = new V3(1, 0, 0); ps.Lean = 0.3f;
                        }
                        if (twoHands) { ps.HandL = ps.HandR - ps.WR.Norm * 2.6f; ps.PoleL = new V3(-0.4f, -1, -0.6f); }
                        swingR = swingL = false;
                    }
                    break;
                default:
                    {
                        ps.WeapR = Rigs.FoeWeapon(EnemyKind.Prior);
                        V3 rest = SR + new V3(2.2f, -4.0f, 1.6f);
                        ps.HandR = rest; ps.WR = new V3(0.12f, 1, 0.1f); ps.WRUp = new V3(1, 0, 0);
                        ps.HandL = SL + new V3(3.4f, -5.4f, 0.8f);
                        if (e.State == Enemy.Windup && e.AttackKind == Game.AkArc)
                        {
                            float k = M.EaseOut(pr);
                            ps.HandR = Lerp(rest, SR + new V3(-0.8f, 6.0f, 2.2f), k); ps.WR = Lerp(ps.WR, new V3(-0.5f, 0.7f, 0.5f), k);
                        }
                        else if (sw >= 0)
                        {
                            float th = M.Lerp(1.4f, -1.4f, M.EaseOut(sw));
                            ps.HandR = SR + new V3(M.Cos(th), -0.1f, M.Sin(th)) * 8.3f; ps.WR = new V3(M.Cos(th), 0.15f, M.Sin(th)); ps.WRUp = new V3(0, 1, 0);
                            ps.Twist = th * 0.35f;
                        }
                        else if (e.State == Enemy.Windup || e.State == Enemy.Special)
                        {
                            float k = e.State == Enemy.Special ? 1f : M.EaseOut(pr);
                            float wave = e.State == Enemy.Special ? M.Sin(g.UiT * 3f) * 0.8f : 0;
                            ps.HandR = Lerp(rest, SR + new V3(2.6f, 8.0f, 2.6f + wave), k); ps.PoleR = new V3(0, -1, 1);
                            ps.HandL = Lerp(ps.HandL, SL + new V3(2.6f, 8.0f, -2.6f - wave), k); ps.PoleL = new V3(0, -1, -1);
                            ps.WR = Lerp(ps.WR, new V3(0.15f, 1, 0.2f), k); ps.HeadPitch = -0.25f * k;
                        }
                        if (e.LurchT > 0) ps.Lean = 0.3f;
                        swingR = swingL = false;
                    }
                    break;
            }
            Gait(ref ps, swingR, swingL);
            if (e.Dead) { ps.Fall = deadT; ps.Move = 0; ps.Lift -= deadT * deadT * 6f; }
            DrawHuman(ref ps);
        }

        void DrawWolf(Enemy e, ActorView v, float gy, float moving, float deadT)
        {
            bool alpha = e.Kind == EnemyKind.AlphaWolf;
            Rigs.Beast b = Rigs.Wolf(alpha);
            float sc = alpha ? 1.32f : 1.08f;
            float ph = e.AnimT * 13f;
            float pr = e.State == Enemy.Windup ? M.Clamp01(e.StateT / Math.Max(0.01f, e.StateDur)) : 0;
            bool leap = e.State == Enemy.Special;
            float crouch = pr * 2.8f, lift = 0, pitch = -0.1f * pr;
            if (leap) { float u = M.Clamp01(e.StateT / Math.Max(0.01f, e.StateDur)); lift = M.Sin(u * M.PI) * 6f; pitch = 0.12f - u * 0.24f; moving = 0; }
            if (e.FadeIn < 1) lift -= (1 - e.FadeIn) * 16f;
            Xf root = Xf.Move(e.X, gy + lift, e.Y) * Xf.Yaw(v.Yaw) * Xf.Scale(sc);
            if (deadT > 0) root = root * Xf.Move(0, deadT * 3.5f - deadT * deadT * 5f, 0) * Xf.RotX(deadT * 1.5f);
            float bob = Math.Abs(M.Sin(ph)) * 0.9f * moving;
            Xf body = root * Xf.Move(0, -crouch + bob, 0) * Xf.RotZ(pitch);
            Put(b.Body, body);
            float hp = -0.35f * pr + (leap ? 0.15f : 0) + M.Sin(ph * 2) * 0.05f * moving;
            Put(b.Head, body * Xf.Move(9.0f, 12.6f, 0) * Xf.RotZ(hp));
            V3 tail = new V3(-0.75f, -0.5f + 0.25f * moving + (leap ? 0.4f : 0), M.Sin(g.UiT * (e.Aggro ? 3f : 7f) + e.X) * 0.35f).Norm;
            Put(b.Tail, body * Xf.Move(-10.2f, 12.2f, 0) * Rigs.Seg(tail, new V3(0, 1, 0)));
            float[] lx = { 6.0f, 6.0f, -7.2f, -7.2f }, lz = { 2.6f, -2.6f, 2.8f, -2.8f }, off = { 0, 0.7f, M.PI, M.PI + 0.7f };
            for (int i = 0; i < 4; i++)
            {
                float a = M.Sin(ph + off[i]) * 0.55f * moving;
                if (leap) a = i < 2 ? 1.0f : -0.9f;
                else if (pr > 0) a = i < 2 ? -0.25f * pr : 0.35f * pr;
                Put(b.LegF, body * Xf.Move(lx[i], 9.4f + crouch * 0.3f, lz[i]) * Xf.RotZ(a));
            }
        }

        void DrawCrawler(Enemy e, ActorView v, float gy, float moving, float deadT)
        {
            Rigs.Beast b = Rigs.Crawler();
            float sc = 1.1f;
            float ph = e.AnimT * 16f;
            float pr = e.State == Enemy.Windup ? M.Clamp01(e.StateT / Math.Max(0.01f, e.StateDur)) : 0;
            float rear = e.State == Enemy.Windup && e.AttackKind == Game.AkArc ? 0.32f * pr : 0;
            float tail = e.State == Enemy.Windup && e.AttackKind == Game.AkCast ? -0.25f * pr : 0;
            float lift = e.FadeIn < 1 ? -(1 - e.FadeIn) * 12f : 0;
            Xf root = Xf.Move(e.X, gy + lift, e.Y) * Xf.Yaw(v.Yaw) * Xf.Scale(sc);
            if (deadT > 0) root = root * Xf.Move(0, deadT * 2f, 0) * Xf.RotX(deadT * M.PI * 0.9f);
            Xf pitch = Xf.Move(0, Math.Abs(M.Sin(ph)) * 0.5f * moving, 0) * Xf.RotZ(rear + tail);
            Put(b.Body, root * pitch);
            float[] hx = { 3.4f, 1.8f, 0.2f, -1.4f }, fx = { 9.5f, 4.5f, -1.5f, -7.5f };
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 4; i++)
                {
                    float p0 = ph + i * 1.7f + (side > 0 ? 0 : M.PI);
                    V3 hip = pitch.P(hx[i], 5.2f, side * 2.4f);
                    V3 foot = new V3(fx[i] + M.Sin(p0) * 2.4f * moving, Math.Max(0, M.Cos(p0)) * 1.8f * moving, side * (10.5f - Math.Abs(i - 1.5f) * 0.8f));
                    if (deadT > 0) foot = new V3(hx[i] + 2, 10, side * 4);
                    V3 knee, end;
                    Rigs.Ik(hip, foot, 6.4f, 7.6f, new V3(0, 1, 0), out knee, out end);
                    Put(b.LegU, root * Xf.Move(hip) * Rigs.Seg(knee - hip, new V3(1, 0, 0)));
                    Put(b.LegL, root * Xf.Move(knee) * Rigs.Seg(end - knee, new V3(1, 0, 0)));
                }
        }

        void DrawWisp(Enemy e, ActorView v, float gy, float deadT)
        {
            float t = g.UiT;
            float s = Math.Max(0.05f, e.FadeIn) * (1f - deadT * 0.8f);
            V3 c = new V3(e.X, gy + 13 + e.Z + M.Sin(t * 2.2f + e.X * 0.1f) * 2f, e.Y);
            Put(Rigs.WispCore(), Xf.Move(c) * Xf.Yaw(v.Yaw) * Xf.Scale(s));
            float glow = e.State == Enemy.Windup ? 1.3f : 0.85f;
            Bill(fxAdd, c, 15f * s, 0x7F6CB6, 0.55f * glow * (1 - deadT), 0);
            Bill(fxAdd, c, 7f * s, 0xEAF3FF, 0.6f * (1 - deadT), 0);
            for (int k = 0; k < 3; k++)
            {
                V3 prev = c + new V3(0, -1.5f * s, 0);
                for (int j = 1; j <= 4; j++)
                {
                    float a = t * 3f + k * 2.1f + j * 0.7f;
                    V3 p = c + new V3((M.Sin(a) * 1.4f + (k - 1) * 1.8f) * s, -(1.5f + j * 2.6f) * s, M.Cos(a * 0.8f) * 1.2f * s);
                    Ribbon(fxAdd, prev, p, (2.4f - j * 0.45f) * s, 0x9C8CE8, 0.5f * (1 - j / 5f) * (1 - deadT));
                    prev = p;
                }
            }
        }

        // villagers
        void DrawVillager(Npc n)
        {
            float gy = HeightAt(n.X, n.Y);
            if (!Near(n.X, n.Y, gy, 30, 50)) return;
            HumanSpec s = Rigs.Villager(n.Spr);
            ActorView v = GetView(n, n.X, n.Y, n.Facing > 0 ? 0.35f : M.PI - 0.35f);
            float want = n.Facing > 0 ? 0.35f : M.PI - 0.35f;
            float spd = 0;
            if (fdt > 0.0005f)
            {
                float vx = (n.X - v.LX) / fdt, vz = (n.Y - v.LY) / fdt;
                spd = (float)Math.Sqrt(vx * vx + vz * vz);
                if (spd > 3 && spd < 300) want = M.Atan2(vz, vx);
                v.LX = n.X; v.LY = n.Y;
            }
            Player p = g.P;
            if (p != null && (g.NearNpc == n || M.Dist2(p.X, p.Y, n.X, n.Y) < 46 * 46) && spd < 3) want = M.Atan2(p.Y - n.Y, p.X - n.X);
            v.Yaw = Turn(v.Yaw, want, 6f * fdt);
            v.Move += ((n.Moving ? 1f : 0f) - v.Move) * Math.Min(1f, 8f * fdt);
            float scale = Rigs.HumanScale * (n.Spr == Assets.Child ? 0.74f : 1f);
            Pose ps = NewPose(s, n.X, n.Y, gy, scale);
            ps.Yaw = v.Yaw; ps.Move = v.Move; ps.Phase = n.AnimT * 12f;
            if (v.Move < 0.1f) ps.Bob = M.Sin(n.AnimT * 3.2f) * 0.2f;
            if (n.TalkBob > 0) ps.HeadPitch = M.Sin(n.TalkBob * 18f) * 0.12f;
            if (n.Spr == Assets.Smith)
            {
                ps.WeapR = Rigs.FoeWeapon(EnemyKind.Hollow) == null ? null : SmithHammer();
                ps.HandR = ShR(s) + new V3(2.0f, -6.8f, 1.0f); ps.WR = new V3(0.5f, -0.85f, 0.1f); ps.WRUp = new V3(1, 0, 0);
            }
            if (n.Spr == Assets.Elder) { ps.HandL = ps.HandR + new V3(0, 0, -2) - new V3(0, 0, 0); ps.HandR = ShR(s) + new V3(3.4f, -6.0f, -1.6f); ps.HandL = ShL(s) + new V3(3.4f, -6.0f, 1.6f); }
            Gait(ref ps, n.Spr != Assets.Smith && n.Spr != Assets.Elder, n.Spr != Assets.Elder);
            if (s.Spirit)
            {
                ps.Lift = 2.5f + M.Sin(g.UiT * 1.6f) * 1.2f;
                PaintFx(fxAlpha, 150, -1);
                tTint = 0; tAmt = 0; tEm = -1;
                DrawHuman(ref ps);
                Bill(fxAdd, new V3(n.X, gy + 18, n.Y), 26f, 0x9C8CE8, 0.35f, 0);
                Paint(dyn, 0, 0, -1);
                return;
            }
            DrawHuman(ref ps);
        }

        MeshB hammer;
        MeshB SmithHammer()
        {
            if (hammer != null) return hammer;
            MeshB m = new MeshB();
            m.Layer = Mat3.Bark; m.Col = 0x6A4A30;
            m.T = Xf.RotZ(-M.PI / 2); m.Cyl(0, 0, -1.5f, 8.5f, 0.45f, 0.45f, 5, true, true, 0); m.T = Xf.Id;
            m.Layer = Mat3.Metal; m.Col = 0x5A5C68;
            m.Box(7.0f, -1.2f, -1.6f, 9.6f, 1.2f, 1.6f);
            hammer = m;
            return m;
        }

        // loot and missiles
        void DrawDrop(Pickup d)
        {
            float gy = HeightAt(d.X, d.Y);
            if (!Near(d.X, d.Y, gy, 16, 40)) return;
            Paint(dyn, 0, 0, -1);
            float hover = d.Z > 0 ? d.Z : 0;
            float bob = d.Z <= 0 ? M.Sin(d.T * 4f) * 1.2f : 0;
            switch (d.Kind)
            {
                case Pickup.Gold:
                    Put(Rigs.Coin(), Xf.Move(d.X, gy + 2.7f + hover + bob * 0.4f, d.Y) * Xf.Yaw(d.T * 5f) * Xf.RotX(M.PI / 2));
                    break;
                case Pickup.Health:
                case Pickup.Spirit:
                    Put(Rigs.Potion(d.Kind == Pickup.Health), Xf.Move(d.X, gy + 0.6f + hover + bob, d.Y) * Xf.Yaw(d.T * 1.5f) * Xf.Scale(1.15f));
                    break;
                default:
                    if (d.It != null)
                    {
                        // the item itself, turning slowly over the spot where it fell
                        Xf pose; float half;
                        MeshB lm = LootRigs.For(d.It, out pose, out half);
                        float spin = d.T * 1.1f + d.X * 0.013f + d.Y * 0.029f;
                        Put(lm, Xf.Move(d.X, gy + 2.6f + half + hover + bob, d.Y) * Xf.Yaw(spin) * pose);
                    }
                    break;
            }
        }

        void DrawMissile(Projectile s)
        {
            float gy = HeightAt(s.X, s.Y);
            if (!Near(s.X, s.Y, gy, 20, 40)) return;
            float sp = (float)Math.Sqrt(s.VX * s.VX + s.VY * s.VY);
            float ang = M.Atan2(s.VY, s.VX);
            float pitch = s.Lob ? M.Atan2(s.VZ, Math.Max(1f, sp)) : 0;
            V3 at = new V3(s.X, gy + s.Z, s.Y);
            if (Fp && s.FromPlayer && !s.Lob) at.Y += 9f;       // through the hero's eyes shots leave from the hand, near the crosshair
            MeshB m = Rigs.Missile(s.Kind);
            if (m != null)
            {
                Put(m, Xf.Move(at) * Xf.Yaw(ang) * Xf.RotZ(pitch) * Xf.Scale(1.15f));
                if (s.LightR > 0) Bill(fxAdd, at + new V3(M.Cos(ang) * 6, 0, M.Sin(ang) * 6), 5f, s.LightColor, 0.5f, 0);
                return;
            }
            int core, glow; float r;
            if (Rigs.Orb(s.Kind, out core, out glow, out r))
            {
                int keep = tTint, keepA = tAmt;
                tTint = core; tAmt = 256;
                Put(Rigs.Bead(), Xf.Move(at) * Xf.Scale(r));
                tTint = keep; tAmt = keepA;
                Bill(fxAdd, at, r * 4.2f, glow, 0.75f, 0);
                Bill(fxAdd, at, r * 1.9f, core, 0.7f, 0);
                if (sp > 1)
                {
                    V3 back = new V3(-s.VX / sp, 0, -s.VY / sp);
                    Ribbon(fxAdd, at, at + back * (r * 6f), r * 1.6f, glow, 0.55f);
                }
            }
        }

        // camera-facing pieces
        /// <summary>A camera-facing square of half-size r (shape 0 soft glow, 1 solid, 4 smoke).</summary>
        void Bill(MeshB m, V3 c, float r, int col, float alpha, int shape)
        {
            byte e = (byte)M.Clamp((int)(alpha * 255), 0, 255), l = (byte)shape;
            if (e == 0) return;
            V3 a = camR * r, b = camU * r;
            V3 p0 = c - a - b, p1 = c + a - b, p2 = c + a + b, p3 = c - a + b;
            m.Raw(p0, -1, -1, col, e, l); m.Raw(p1, 1, -1, col, e, l); m.Raw(p2, 1, 1, col, e, l);
            m.Raw(p0, -1, -1, col, e, l); m.Raw(p2, 1, 1, col, e, l); m.Raw(p3, -1, 1, col, e, l);
        }

        /// <summary>A camera-facing strip from a to b (shape 2: bright along its middle).</summary>
        void Ribbon(MeshB m, V3 a, V3 b, float w, int col, float alpha) { RibbonS(m, a, b, w, col, alpha, 2); }

        void RibbonS(MeshB m, V3 a, V3 b, float w, int col, float alpha, int shape)
        {
            byte e = (byte)M.Clamp((int)(alpha * 255), 0, 255), l = (byte)shape;
            if (e == 0) return;
            V3 d = b - a;
            V3 toCam = Eye - a;
            V3 side = V3.Cross(d, toCam);
            if (side.Len < 1e-4f) return;
            side = side.Norm * (w * 0.5f);
            m.Raw(a - side, 0, -1, col, e, l); m.Raw(b - side, 1, -1, col, e, l); m.Raw(b + side, 1, 1, col, e, l);
            m.Raw(a - side, 0, -1, col, e, l); m.Raw(b + side, 1, 1, col, e, l); m.Raw(a + side, 0, 1, col, e, l);
        }

        /// <summary>A sprite standing upright and facing the camera (loot icons), drawn lit through the world shader.</summary>
        void CardBill(MeshB m, Sprite s, V3 c, float size, int glow)
        {
            Mat3.AtlasRect r;
            if (s == null || !Mat3.Rects.TryGetValue(s, out r)) return;
            float hw = size * 0.5f, hh = size * 0.5f * s.H / Math.Max(1, s.W);
            V3 right = new V3(camR.X, 0, camR.Z).Norm * hw;
            V3 up = new V3(0, hh * 1.0f, 0) + new V3(-camU.X, 0, -camU.Z) * 0;
            byte e = (byte)M.Clamp(glow, 0, 255);
            V3 p0 = c - right - up, p1 = c + right - up, p2 = c + right + up, p3 = c - right + up;
            m.Raw(p0, r.U0, r.V1, 0xFFFFFF, e, Mat3.AtlasLayer); m.Raw(p1, r.U1, r.V1, 0xFFFFFF, e, Mat3.AtlasLayer); m.Raw(p2, r.U1, r.V0, 0xFFFFFF, e, Mat3.AtlasLayer);
            m.Raw(p0, r.U0, r.V1, 0xFFFFFF, e, Mat3.AtlasLayer); m.Raw(p2, r.U1, r.V0, 0xFFFFFF, e, Mat3.AtlasLayer); m.Raw(p3, r.U0, r.V0, 0xFFFFFF, e, Mat3.AtlasLayer);
        }
    }
}
