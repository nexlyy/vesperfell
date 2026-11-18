using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>Enemy behaviour. Every kind has its own attack pattern, all built from the same small state machine.</summary>
    public sealed partial class Game
    {
        // attack kinds (drive the telegraph that is drawn on the ground)
        public const int AkArc = 1, AkLine = 2, AkCircle = 3, AkAim = 4, AkCast = 5, AkSummon = 6, AkSigils = 7;

        bool PlayerVisible { get { return !P.Dead && P.StealthT <= 0; } }

        void UpdateEnemies(float dt)
        {
            for (int i = Foes.Count - 1; i >= 0; i--)
            {
                if (i >= Foes.Count) continue;
                Enemy e = Foes[i];
                if (e.Dead) { e.DeathT += dt; if (e.DeathT > 0.7f) { foeById.Remove(e.NetId); Lists.RemoveAt(Foes, i, e); } continue; }
                // each enemy thinks about one hero at a time: for the rest of this pass, P is that hero
                Player p = PickTarget(e);
                P = p; Cur.FlowSlot = p.Slot;
                float dist = M.Dist(e.X, e.Y, p.X, p.Y);
                e.Awake = dist <= 470 || e.Aggro;
                if (!e.Awake && !e.Def.Boss) continue;        // asleep
                UpdateStatuses(e, dt);
                if (e.Dead) continue;
                e.AnimT += dt; e.StateT += dt;
                if (e.HpBarT > 0) e.HpBarT -= dt;
                if (e.AlertShow > 0) e.AlertShow -= dt;
                if (e.AtkCd > 0) e.AtkCd -= dt;
                if (e.AtkCd2 > 0) e.AtkCd2 -= dt;
                if (e.AtkCd3 > 0) e.AtkCd3 -= dt;
                if (e.SpecialCd > 0) e.SpecialCd -= dt;
                if (e.WeaponSwing > 0) e.WeaponSwing -= dt * 4f;
                if (e.FadeIn < 1) e.FadeIn = Math.Min(1, e.FadeIn + dt * 3f);
                int mask = e.Def.Flying ? 2 : 1;
                if (e.State != Enemy.Special) ApplyKnock(e, dt, mask); else { e.KX = e.KY = 0; }
                e.Moving = false;
                if (e.Incapacitated)
                {
                    // stuns interrupt wind-ups (but not the boss)
                    if ((e.State == Enemy.Windup || e.State == Enemy.Special) && !e.Def.Boss) { e.SetState(Enemy.Chase, 0); e.AtkCd = Math.Max(e.AtkCd, 0.6f); }
                    continue;
                }
                if (AiCommon(e, dist, dt)) continue;
                switch (e.Kind)
                {
                    case EnemyKind.Wolf: case EnemyKind.AlphaWolf: AiWolf(e, dist, dt); break;
                    case EnemyKind.Crawler: AiCrawler(e, dist, dt); break;
                    case EnemyKind.Hollow: AiHollow(e, dist, dt); break;
                    case EnemyKind.Archer: AiArcher(e, dist, dt); break;
                    case EnemyKind.Wisp: AiWisp(e, dist, dt); break;
                    case EnemyKind.Acolyte: AiAcolyte(e, dist, dt); break;
                    case EnemyKind.Knight: AiKnight(e, dist, dt); break;
                    case EnemyKind.Prior: AiPrior(e, dist, dt); break;
                }
            }
            P = Me; Cur.FlowSlot = 0;
            Separate();
        }

        // shared pieces
        void AlertSound(Enemy e)
        {
            switch (e.Kind)
            {
                case EnemyKind.Wolf: case EnemyKind.AlphaWolf: SndAt(Sfx.WolfGrowl, e.X, e.Y, 0.9f); break;
                case EnemyKind.Crawler: SndAt(Sfx.Hiss, e.X, e.Y, 0.8f); break;
                case EnemyKind.Hollow: SndAt(Sfx.Groan, e.X, e.Y, 0.8f); break;
                case EnemyKind.Archer: SndAt(Sfx.BoneRattle, e.X, e.Y, 0.8f); break;
                case EnemyKind.Wisp: SndAt(Sfx.WispCast, e.X, e.Y, 0.5f); break;
                case EnemyKind.Acolyte: SndAt(Sfx.Chant, e.X, e.Y, 0.7f); break;
                case EnemyKind.Knight: SndAt(Sfx.BossRoar, e.X, e.Y, 1f); break;
            }
        }

        /// <summary>Idle wandering, noticing the player, giving up. Returns true when the enemy is not in combat.</summary>
        bool AiCommon(Enemy e, float dist, float dt)
        {
            Player p = P;
            if (e.Def.Boss) return !e.Aggro;        // the boss only wakes when the fight starts
            if (!e.Aggro)
            {
                if (PlayerVisible && dist < e.Def.Aggro && (dist < e.Def.Aggro * 0.45f || Cur.Los(e.X, e.Y - 4, p.X, p.Y - 4)))
                {
                    e.SetState(Enemy.Alert, 0.4f);
                    AlertSound(e);
                    Aggravate(e);
                    return true;
                }
                if (e.State == Enemy.Search)
                {
                    if (e.StateT > e.StateDur) e.SetState(Enemy.Return, 0);
                    return true;
                }
                float hd = M.Dist(e.X, e.Y, e.HomeX, e.HomeY);
                if (e.State == Enemy.Return || hd > 46)
                {
                    if (e.State != Enemy.Return) e.SetState(Enemy.Return, 0);
                    MoveToward(e, e.HomeX, e.HomeY, e.Speed * 0.8f, dt, false);
                    e.HP = Math.Min(e.MaxHP, e.HP + e.MaxHP * 0.2f * dt);
                    if (hd < 8 || e.StateT > 12) { e.SetState(Enemy.Idle, 0); if (hd > 30) { e.X = e.HomeX; e.Y = e.HomeY; } }
                }
                else
                {
                    if (e.State != Enemy.Idle) e.SetState(Enemy.Idle, 0);
                    e.WanderT -= dt;
                    if (e.WanderT <= 0)
                    {
                        e.WanderT = 2f + Rnd.F() * 4f;
                        float a = Rnd.F() * M.TAU, d = 8 + Rnd.F() * 22;
                        e.TX = e.HomeX + M.Cos(a) * d; e.TY = e.HomeY + M.Sin(a) * d;
                    }
                    if (M.Dist(e.X, e.Y, e.TX, e.TY) > 4 && e.WanderT > 0.8f) MoveToward(e, e.TX, e.TY, e.Speed * 0.35f, dt, false);
                }
                return true;
            }
            if (!PlayerVisible)
            {
                e.Aggro = false; e.SetState(Enemy.Search, 2.2f);
                return true;
            }
            if (!e.Def.Elite && M.Dist(e.X, e.Y, e.HomeX, e.HomeY) > 340 && dist > 150)
            {
                e.Aggro = false; e.SetState(Enemy.Return, 0);
                return true;
            }
            if (e.State == Enemy.Alert)
            {
                Face(e, p.X);
                if (e.StateT >= e.StateDur) e.SetState(Enemy.Chase, 0);
                return true;
            }
            if (e.State == Enemy.Idle || e.State == Enemy.Return || e.State == Enemy.Search) e.SetState(Enemy.Chase, 0);
            return false;
        }

        void Face(Enemy e, float tx) { e.Facing = tx >= e.X ? 1 : -1; }

        void MoveToward(Enemy e, float tx, float ty, float speed, float dt, bool path)
        {
            if (e.RootT > 0) return;
            if (e.SlowT > 0) speed *= 1f - e.SlowAmt;
            float dx = tx - e.X, dy = ty - e.Y, len = M.Len(dx, dy);
            if (len < 0.5f) return;
            dx /= len; dy /= len;
            int mask = e.Def.Flying ? 2 : 1;
            if (path && !e.Def.Flying && len > 22)
            {
                float fx, fy;
                if (Cur.FlowDir(e.X, e.Y, out fx, out fy)) { dx = fx; dy = fy; }
            }
            // unstick: if we have been pushing against something, slide sideways for a moment
            if (e.StuckT > 0.5f)
            {
                float t = dx; dx = -dy * e.StrafeDir; dy = t * e.StrafeDir;
                if (e.StuckT > 0.95f) e.StuckT = 0;
            }
            float bx = e.X, by = e.Y;
            Cur.Move(e, dx * speed * dt, dy * speed * Surface.YMove * dt, mask);
            float moved = M.Dist(bx, by, e.X, e.Y);
            if (moved < speed * dt * 0.25f) e.StuckT += dt; else if (e.StuckT <= 0.5f) e.StuckT = 0; else e.StuckT += dt;
            e.Moving = true;
            if (Math.Abs(dx) > 0.25f) e.Facing = dx > 0 ? 1 : -1;
        }

        void Orbit(Enemy e, float radius, float speed, float dt)
        {
            Player p = P;
            e.StrafeT -= dt;
            if (e.StrafeT <= 0) { e.StrafeDir = Rnd.Chance(0.5f) ? 1 : -1; e.StrafeT = 0.8f + Rnd.F() * 1.2f; }
            float ang = M.Atan2(e.Y - p.Y, e.X - p.X) + e.StrafeDir * 0.5f;
            MoveToward(e, p.X + M.Cos(ang) * radius, p.Y + M.Sin(ang) * radius * 0.8f, speed, dt, false);
            Face(e, p.X);
        }

        void Flee(Enemy e, float speed, float dt)
        {
            Player p = P;
            float dx = e.X - p.X, dy = e.Y - p.Y, len = Math.Max(1, M.Len(dx, dy));
            float bx = e.X, by = e.Y;
            MoveToward(e, e.X + dx / len * 30 + (-dy / len) * e.StrafeDir * 14, e.Y + dy / len * 30 + (dx / len) * e.StrafeDir * 14, speed, dt, false);
            if (M.Dist(bx, by, e.X, e.Y) < speed * dt * 0.3f) e.StrafeDir = -e.StrafeDir;
            Face(e, p.X);
        }

        float AngleToPlayer(Enemy e) { return M.Atan2(P.Y - e.Y, P.X - e.X); }

        bool InArc(Enemy e, float reach, float halfArc)
        {
            Player p = P;
            float dx = p.X - e.X, dy = (p.Y - e.Y) * Surface.Squash, d = M.Len(dx, dy);
            if (d > reach + p.Radius) return false;
            if (d < 8) return true;
            return Math.Abs(M.AngDiff(e.AttackAng, M.Atan2(dy, dx))) <= halfArc;
        }

        void Separate()
        {
            for (int i = 0; i < Foes.Count; i++)
            {
                Enemy a = Foes[i];
                if (a.Dead || a.State == Enemy.Special || a.State == Enemy.Vanish) continue;
                int maskA = a.Def.Flying ? 2 : 1;
                for (int j = i + 1; j < Foes.Count; j++)
                {
                    Enemy b = Foes[j];
                    if (b.Dead || b.State == Enemy.Special || b.State == Enemy.Vanish) continue;
                    float dx = b.X - a.X, dy = b.Y - a.Y;
                    float rr = a.Radius + b.Radius + 2;
                    if (Math.Abs(dx) > rr || Math.Abs(dy) > rr) continue;
                    float d = M.Len(dx, dy);
                    if (d >= rr) continue;
                    if (d < 0.01f) { dx = 1; dy = 0; d = 1; }
                    float push = (rr - d) * 0.5f;
                    float wa = b.Def.KnockRes >= 0.9f ? 1f : 0.5f, wb = a.Def.KnockRes >= 0.9f ? 1f : 0.5f;
                    if (a.Def.KnockRes < 0.9f) Cur.Move(a, -dx / d * push * wa, -dy / d * push * wa, maskA);
                    if (b.Def.KnockRes < 0.9f) Cur.Move(b, dx / d * push * wb, dy / d * push * wb, b.Def.Flying ? 2 : 1);
                }
                if (a.Def.Flying) continue;
                for (int k = 0; k < Party.Count; k++)
                {
                    Player p = Party[k];
                    if (p.Dead || p.DashT > 0 || p.DodgeT > 0) continue;
                    float dx = a.X - p.X, dy = a.Y - p.Y, rr = a.Radius + p.Radius;
                    float d = M.Len(dx, dy);
                    if (d >= rr || d <= 0.01f) continue;
                    if (a.Def.KnockRes >= 0.9f) { if (!p.Remote) Cur.Move(p, -dx / d * (rr - d), -dy / d * (rr - d), 1); }   // a companion moves themselves
                    else Cur.Move(a, dx / d * (rr - d), dy / d * (rr - d), maskA);
                }
            }
        }

        // wolves
        void AiWolf(Enemy e, float dist, float dt)
        {
            Player p = P;
            bool alpha = e.Kind == EnemyKind.AlphaWolf;
            switch (e.State)
            {
                case Enemy.Chase:
                    if (dist > 84) { MoveToward(e, p.X, p.Y, e.Speed, dt, true); }
                    else
                    {
                        Orbit(e, 58, e.Speed * 0.8f, dt);
                        if (e.AtkCd <= 0 && dist < 100 && Cur.Los(e.X, e.Y, p.X, p.Y))
                        {
                            e.SetState(Enemy.Windup, alpha ? 0.5f : 0.42f); e.AttackKind = AkLine; e.AttackAng = AngleToPlayer(e);
                            SndAt(Sfx.WolfGrowl, e.X, e.Y, 0.7f);
                        }
                    }
                    break;
                case Enemy.Windup:
                    Face(e, p.X);
                    if (e.StateT < e.StateDur * 0.6f) e.AttackAng = AngleToPlayer(e);
                    if (e.StateT >= e.StateDur) { e.SetState(Enemy.Special, 0.34f); e.DirX = M.Cos(e.AttackAng); e.DirY = M.Sin(e.AttackAng); }
                    break;
                case Enemy.Special:
                    Cur.Move(e, e.DirX * 270 * dt, e.DirY * 270 * Surface.YMove * dt, 1);
                    e.Moving = true; e.Facing = e.DirX >= 0 ? 1 : -1;
                    if (!e.HitDone && dist < e.Radius + p.Radius + 7)
                    {
                        e.HitDone = true;
                        DamagePlayer(e.Dmg, e.X, e.Y, true, e, 0, 120);
                        SndAt(Sfx.WolfBite, e.X, e.Y, 1f);
                    }
                    if (e.StateT >= e.StateDur) { e.SetState(Enemy.Recover, 0.55f); e.AtkCd = 1.3f + Rnd.F() * 0.9f; }
                    break;
                case Enemy.Recover:
                    if (e.StateT >= e.StateDur) e.SetState(Enemy.Chase, 0);
                    break;
                default: e.SetState(Enemy.Chase, 0); break;
            }
        }

        // mire crawlers
        void AiCrawler(Enemy e, float dist, float dt)
        {
            Player p = P;
            switch (e.State)
            {
                case Enemy.Chase:
                    if (dist < 27 && e.AtkCd2 <= 0)
                    {
                        e.SetState(Enemy.Windup, 0.32f); e.AttackKind = AkArc; e.AttackAng = AngleToPlayer(e);
                    }
                    else if (dist < 140 && dist > 46 && e.AtkCd <= 0 && Cur.Los(e.X, e.Y, p.X, p.Y))
                    {
                        e.SetState(Enemy.Windup, 0.5f); e.AttackKind = AkCast; e.AttackAng = AngleToPlayer(e);
                        SndAt(Sfx.Hiss, e.X, e.Y, 0.7f);
                    }
                    else if (dist > 96) MoveToward(e, p.X, p.Y, e.Speed, dt, true);
                    else if (dist < 40 && e.AtkCd2 > 0.3f) Flee(e, e.Speed * 0.9f, dt);
                    else if (dist < 30) { MoveToward(e, p.X, p.Y, e.Speed, dt, false); }
                    else Orbit(e, 78, e.Speed * 0.75f, dt);
                    break;
                case Enemy.Windup:
                    Face(e, p.X);
                    if (e.StateT >= e.StateDur)
                    {
                        if (e.AttackKind == AkCast)
                        {
                            float tx = p.X + Rnd.Sym(6), ty = p.Y + Rnd.Sym(6);
                            float t = 0.5f + M.Dist(e.X, e.Y, tx, ty) / 420f;
                            Projectile pr = Shoot(Projectile.KGlob, e.X, e.Y, 0, 0, e.Dmg, false, t);
                            pr.Lob = true; pr.TXg = tx; pr.TYg = ty; pr.VX = (tx - e.X) / t; pr.VY = (ty - e.Y) / t; pr.Z = 8; pr.VZ = 110 * t - 8 / t;
                            SndAt(Sfx.Spit, e.X, e.Y, 0.9f);
                            e.AtkCd = 2.4f + Rnd.F() * 1.2f;
                            e.SetState(Enemy.Recover, 0.4f);
                        }
                        else
                        {
                            if (InArc(e, 24, 1.1f)) DamagePlayer(e.Dmg * 0.9f, e.X, e.Y, true, e, 1, 70);
                            SndAt(Sfx.WolfBite, e.X, e.Y, 0.7f);
                            e.WeaponSwing = 1; e.AtkCd2 = 1.3f;
                            e.SetState(Enemy.Recover, 0.45f);
                        }
                    }
                    break;
                case Enemy.Recover:
                    if (e.StateT >= e.StateDur) e.SetState(Enemy.Chase, 0);
                    break;
                default: e.SetState(Enemy.Chase, 0); break;
            }
        }

        // hollows
        void AiHollow(Enemy e, float dist, float dt)
        {
            Player p = P;
            switch (e.State)
            {
                case Enemy.Chase:
                    e.LurchT -= dt;
                    if (e.LurchT < -0.55f) e.LurchT = 2.4f + Rnd.F() * 2.6f;
                    if (dist < 25 && e.AtkCd <= 0)
                    {
                        e.SetState(Enemy.Windup, 0.55f); e.AttackKind = AkArc; e.AttackAng = AngleToPlayer(e);
                        SndAt(Sfx.Groan, e.X, e.Y, 0.6f);
                    }
                    else if (dist > 17) MoveToward(e, p.X, p.Y, e.Speed * (e.LurchT < 0 ? 2.4f : 1f), dt, true);
                    else Face(e, p.X);
                    break;
                case Enemy.Windup:
                    if (e.StateT >= e.StateDur)
                    {
                        if (InArc(e, 30, 1.05f)) DamagePlayer(e.Dmg, e.X, e.Y, true, e, 0, 95);
                        SndAt(Sfx.Swing, e.X, e.Y, 0.7f);
                        e.WeaponSwing = 1; e.AtkCd = 0.9f + Rnd.F() * 0.6f;
                        e.SetState(Enemy.Recover, 0.7f);
                    }
                    break;
                case Enemy.Recover:
                    if (e.StateT >= e.StateDur) e.SetState(Enemy.Chase, 0);
                    break;
                default: e.SetState(Enemy.Chase, 0); break;
            }
        }

        // barrow archers
        void AiArcher(Enemy e, float dist, float dt)
        {
            Player p = P;
            bool los = Cur.Los(e.X, e.Y - 2, p.X, p.Y - 2);
            switch (e.State)
            {
                case Enemy.Chase:
                    if (dist < 62) Flee(e, e.Speed * 1.15f, dt);
                    else if (dist > 150 || !los) MoveToward(e, p.X, p.Y, e.Speed, dt, true);
                    else Orbit(e, dist, e.Speed * 0.45f, dt);
                    if (e.AtkCd <= 0 && los && dist < 180 && dist > 26)
                    {
                        e.SetState(Enemy.Windup, 0.75f); e.AttackKind = AkAim; e.AttackAng = AngleToPlayer(e);
                        SndAt(Sfx.BoneRattle, e.X, e.Y, 0.5f);
                    }
                    break;
                case Enemy.Windup:
                    Face(e, p.X);
                    if (e.StateT < e.StateDur * 0.72f) e.AttackAng = AngleToPlayer(e);
                    if (e.StateT >= e.StateDur)
                    {
                        Shoot(Projectile.KBone, e.X + M.Cos(e.AttackAng) * 8, e.Y + M.Sin(e.AttackAng) * 6, e.AttackAng, 300, e.Dmg, false, 0.95f);
                        SndAt(Sfx.Bow, e.X, e.Y, 0.8f);
                        e.AtkCd = 2.0f + Rnd.F() * 0.9f;
                        e.SetState(Enemy.Recover, 0.35f);
                    }
                    break;
                case Enemy.Recover:
                    if (e.StateT >= e.StateDur) e.SetState(Enemy.Chase, 0);
                    break;
                default: e.SetState(Enemy.Chase, 0); break;
            }
        }

        // gloam wisps
        void AiWisp(Enemy e, float dist, float dt)
        {
            Player p = P;
            e.Z = 6 + M.Sin(e.AnimT * 3f) * 2.5f;
            switch (e.State)
            {
                case Enemy.Chase:
                    if (dist > 120) MoveToward(e, p.X, p.Y, e.Speed, dt, false);
                    else Orbit(e, 92, e.Speed * 0.8f, dt);
                    if (e.SpecialCd <= 0 && (dist < 58 || e.HP < e.MaxHP * 0.6f)) { e.SetState(Enemy.Vanish, 0.5f); e.SpecialCd = 4.5f + Rnd.F() * 2.5f; SndAt(Sfx.Teleport, e.X, e.Y, 0.6f); break; }
                    if (e.AtkCd <= 0 && dist < 160 && Cur.Los(e.X, e.Y, p.X, p.Y))
                    {
                        e.SetState(Enemy.Windup, 0.5f); e.AttackKind = AkCast;
                        SndAt(Sfx.WispCast, e.X, e.Y, 0.7f);
                    }
                    break;
                case Enemy.Windup:
                    Face(e, p.X);
                    if (e.StateT >= e.StateDur)
                    {
                        float a = AngleToPlayer(e);
                        Projectile pr = Shoot(Projectile.KWispOrb, e.X, e.Y, a, 80, e.Dmg, false, 3.6f);
                        pr.Target = p;
                        e.AtkCd = 2.6f + Rnd.F() * 1.2f;
                        e.SetState(Enemy.Recover, 0.3f);
                    }
                    break;
                case Enemy.Vanish:
                    if (!e.HitDone && e.StateT >= e.StateDur * 0.5f)
                    {
                        e.HitDone = true;
                        Motes(e.X, e.Y, 8, 0xC9D3F5, 0x584688, 10, 6, 40, 0.5f);
                        for (int tries = 0; tries < 12; tries++)
                        {
                            float a = Rnd.F() * M.TAU, d = 84 + Rnd.F() * 34;
                            float nx = p.X + M.Cos(a) * d, ny = p.Y + M.Sin(a) * d * 0.8f;
                            if (Cur.Blocked(nx, ny, e.Radius, 2)) continue;
                            if (M.Dist(nx, ny, e.HomeX, e.HomeY) > 300) continue;
                            e.X = nx; e.Y = ny; break;
                        }
                        Motes(e.X, e.Y, 8, 0xEAF3FF, 0x7F6CB6, 10, 6, 40, 0.5f);
                    }
                    if (e.StateT >= e.StateDur) { e.SetState(Enemy.Chase, 0); e.FadeIn = 0; }
                    break;
                case Enemy.Recover:
                    if (e.StateT >= e.StateDur) e.SetState(Enemy.Chase, 0);
                    break;
                default: e.SetState(Enemy.Chase, 0); break;
            }
        }

        // acolytes
        void AiAcolyte(Enemy e, float dist, float dt)
        {
            Player p = P;
            bool los = Cur.Los(e.X, e.Y - 2, p.X, p.Y - 2);
            switch (e.State)
            {
                case Enemy.Chase:
                    if (dist < 58) Flee(e, e.Speed * 1.1f, dt);
                    else if (dist > 140 || !los) MoveToward(e, p.X, p.Y, e.Speed, dt, true);
                    else Orbit(e, dist, e.Speed * 0.4f, dt);
                    if (e.AtkCd <= 0 && los && dist < 175)
                    {
                        e.SetState(Enemy.Windup, 0.9f); e.AttackKind = AkCast;
                        SndAt(Sfx.Chant, e.X, e.Y, 0.7f);
                    }
                    break;
                case Enemy.Windup:
                    Face(e, p.X);
                    if (Rnd.Chance(0.4f)) Motes(e.X, e.Y, 14, 0xA7A0DC, 0x3A2E5E, 1, 5, 30, 0.4f);
                    if (e.StateT >= e.StateDur)
                    {
                        if ((e.Counter++ & 1) == 0) AddSigil(p.X, p.Y, 26, 0.9f, e.Dmg * 1.15f);
                        else
                        {
                            float a = AngleToPlayer(e);
                            for (int k = -1; k <= 1; k++) Shoot(Projectile.KGloam, e.X + M.Cos(a) * 8, e.Y + M.Sin(a) * 6, a + k * 0.24f, 178, e.Dmg * 0.7f, false, 1.2f);
                            SndAt(Sfx.WispCast, e.X, e.Y, 0.9f);
                        }
                        e.AtkCd = 2.8f + Rnd.F() * 1.0f;
                        e.SetState(Enemy.Recover, 0.5f);
                    }
                    break;
                case Enemy.Recover:
                    if (e.StateT >= e.StateDur) e.SetState(Enemy.Chase, 0);
                    break;
                default: e.SetState(Enemy.Chase, 0); break;
            }
        }

        // Sir Caldus, the Barrow Knight
        void AiKnight(Enemy e, float dist, float dt)
        {
            Player p = P;
            float hpf = e.HP / e.MaxHP;
            bool p2 = hpf < 0.6f, p3 = hpf < 0.35f;
            if (Boss != e) { Boss = e; BossIntroT = 0; }
            switch (e.State)
            {
                case Enemy.Chase:
                    if (dist > 30) MoveToward(e, p.X, p.Y, e.Speed * (p3 ? 1.25f : 1f), dt, true); else Face(e, p.X);
                    if (e.AtkCd > 0) break;
                    if (p2 && dist < 72 && e.AtkCd3 <= 0)
                    {
                        e.SetState(Enemy.Windup, 0.9f); e.AttackKind = AkCircle;
                        SndAt(Sfx.BossRoar, e.X, e.Y, 0.7f);
                    }
                    else if (dist < 44)
                    {
                        e.SetState(Enemy.Windup, p3 ? 0.5f : 0.6f); e.AttackKind = AkArc; e.AttackAng = AngleToPlayer(e);
                    }
                    else if (dist > 74 && dist < 240 && e.AtkCd2 <= 0 && Cur.Los(e.X, e.Y, p.X, p.Y))
                    {
                        e.SetState(Enemy.Windup, 0.8f); e.AttackKind = AkLine; e.AttackAng = AngleToPlayer(e);
                        SndAt(Sfx.KnightCharge, e.X, e.Y, 1f);
                    }
                    break;
                case Enemy.Windup:
                    if (e.AttackKind == AkLine && e.StateT < e.StateDur * 0.6f) { e.AttackAng = AngleToPlayer(e); Face(e, p.X); }
                    if (e.StateT < e.StateDur) break;
                    if (e.AttackKind == AkArc)
                    {
                        if (InArc(e, 46, 1.4f)) DamagePlayer(e.Dmg, e.X, e.Y, true, e, 0, 170);
                        e.WeaponSwing = 1; SndAt(Sfx.SwingHeavy, e.X, e.Y, 1f); Shake(1.5f, 0.1f);
                        e.AtkCd = 0.7f; e.SetState(Enemy.Recover, 0.65f);
                    }
                    else if (e.AttackKind == AkLine)
                    {
                        e.DirX = M.Cos(e.AttackAng); e.DirY = M.Sin(e.AttackAng);
                        e.AtkCd2 = 5f; e.SetState(Enemy.Special, 0.72f);
                    }
                    else
                    {
                        if (GDist(p.X, p.Y, e.X, e.Y) < 58 + 2) DamagePlayer(e.Dmg * 1.2f, e.X, e.Y, true, e, 0, 210);
                        RingBurst(e.X, e.Y, 0x58A8D8, 26, 170, 0.45f); Dust(e.X, e.Y, 10, 0x8A8A9A);
                        Area a = new Area(); a.Kind = Area.Ring; a.X = e.X; a.Y = e.Y; a.R = 58; a.Dur = 0.4f; a.Color = 0x58A8D8; AddArea(a);
                        SndAt(Sfx.KnightSlam, e.X, e.Y, 1f); Shake(5f, 0.3f);
                        NetFx(FxSlam, e.X, e.Y);
                        if (p3) for (int k = 0; k < 10; k++) Shoot(Projectile.KGloam, e.X, e.Y, k * M.TAU / 10, 130, e.Dmg * 0.6f, false, 1.6f);
                        e.AtkCd3 = 6f; e.AtkCd = 1f; e.SetState(Enemy.Recover, 0.9f);
                    }
                    break;
                case Enemy.Special:
                    {
                        int hit = Cur.Move(e, e.DirX * 300 * dt, e.DirY * 300 * Surface.YMove * dt, 1);
                        e.Moving = true; e.Facing = e.DirX >= 0 ? 1 : -1;
                        if (Rnd.Chance(0.5f)) Dust(e.X, e.Y, 1, 0x8A8A9A);
                        if (!e.HitDone && dist < e.Radius + p.Radius + 9)
                        {
                            e.HitDone = true;
                            DamagePlayer(e.Dmg * 1.35f, e.X, e.Y, true, e, 0, 230);
                        }
                        if (hit != 0 && e.StateT > 0.12f)
                        {
                            e.StunT = 1.6f; e.SetState(Enemy.Recover, 0.2f);
                            SndAt(Sfx.KnightSlam, e.X, e.Y, 0.9f); Shake(4f, 0.25f); Dust(e.X, e.Y, 8, 0x8A8A9A);
                            Float(e.X, e.Y - 40, "Staggered!", 0xFFF8DC, false);
                            NetFx(FxStagger, e.X, e.Y);
                        }
                        else if (e.StateT >= e.StateDur) e.SetState(Enemy.Recover, 0.7f);
                    }
                    break;
                case Enemy.Recover:
                    if (e.StateT >= e.StateDur) e.SetState(Enemy.Chase, 0);
                    break;
                default: e.SetState(Enemy.Chase, 0); break;
            }
        }

        // The Silent Prior
        void TollRing(Enemy e, int count, float offset, float speed)
        {
            for (int k = 0; k < count; k++)
                Shoot(Projectile.KGloam, e.X, e.Y, offset + k * M.TAU / count, speed, e.Dmg * 0.75f, false, 2.6f);
            RingBurst(e.X, e.Y, 0xA7A0DC, 20, 150, 0.4f);
            NetFx(FxToll, e.X, e.Y);
        }

        void AiPrior(Enemy e, float dist, float dt)
        {
            Player p = P;
            float hpf = e.HP / e.MaxHP;
            int phase = hpf > 0.66f ? 0 : (hpf > 0.33f ? 1 : 2);
            e.Z = 3 + M.Sin(e.AnimT * 2.2f) * 1.5f;
            // keep him inside the crypt
            e.X = M.Clamp(e.X, DungeonGen.ArenaX0, DungeonGen.ArenaX1); e.Y = M.Clamp(e.Y, DungeonGen.ArenaY0, DungeonGen.ArenaY1);
            // delayed second ring of a toll
            if (e.LurchT > 0)
            {
                e.LurchT -= dt;
                if (e.LurchT <= 0) { TollRing(e, 12 + phase * 4, e.TX + M.PI / (12 + phase * 4), 118 + phase * 10); SndAt(Sfx.BellToll, e.X, e.Y, 0.7f); }
            }
            if (phase > e.Phase && e.State != Enemy.Vanish)
            {
                e.Phase = phase;
                e.SetState(Enemy.Windup, 1.1f); e.AttackKind = AkSummon;
                SndAt(Sfx.BossRoar, e.X, e.Y, 1f); Shake(4f, 0.4f); Flash(0x7F6CB6, 0.3f);
                NetFx(FxRoar, e.X, e.Y);
                NoticeAll(phase == 1 ? "^pThe Prior calls the faithful to rise.^0" : "^pThe silence deepens. He is desperate now.^0");
                return;
            }
            switch (e.State)
            {
                case Enemy.Alert:
                    Face(e, p.X);
                    if (e.StateT >= e.StateDur) { e.SetState(Enemy.Chase, 0); e.AtkCd = 1.2f; e.SpecialCd = 6f; e.AtkCd3 = 4f; e.StrafeT = 5f; }
                    break;
                case Enemy.Chase:
                    if (dist > 40) MoveToward(e, p.X, p.Y, e.Speed * (phase == 2 ? 1.35f : 1f), dt, false); else Face(e, p.X);
                    e.StrafeT -= dt;
                    if (dist < 48 && e.AtkCd2 <= 0)
                    {
                        e.SetState(Enemy.Windup, 0.55f); e.AttackKind = AkArc; e.AttackAng = AngleToPlayer(e);
                    }
                    else if (phase == 2 && e.StrafeT <= 0)
                    {
                        e.SetState(Enemy.Special, 3.0f); e.TX = Rnd.F() * M.TAU; e.TY = 0;
                        SndAt(Sfx.Chant, e.X, e.Y, 1f);
                    }
                    else if (e.AtkCd <= 0)
                    {
                        e.SetState(Enemy.Windup, 0.8f); e.AttackKind = AkCast;
                        SndAt(Sfx.Chant, e.X, e.Y, 0.9f);
                    }
                    else if (phase >= 1 && e.AtkCd3 <= 0)
                    {
                        e.SetState(Enemy.Windup, 0.7f); e.AttackKind = AkSigils;
                        SndAt(Sfx.Chant, e.X, e.Y, 0.8f);
                    }
                    else if (e.SpecialCd <= 0 && (dist < 64 || e.StateT > 3.5f))
                    {
                        e.SetState(Enemy.Vanish, 0.55f); e.SpecialCd = 7f - phase;
                        SndAt(Sfx.Teleport, e.X, e.Y, 0.9f);
                    }
                    break;
                case Enemy.Windup:
                    if (Rnd.Chance(0.5f)) Motes(e.X, e.Y, 20, 0xA7A0DC, 0x3A2E5E, 1, 8, 40, 0.5f);
                    if (e.StateT < e.StateDur) break;
                    if (e.AttackKind == AkArc)
                    {
                        if (InArc(e, 52, 1.5f)) DamagePlayer(e.Dmg * 1.2f, e.X, e.Y, true, e, 0, 180);
                        e.WeaponSwing = 1; SndAt(Sfx.SwingHeavy, e.X, e.Y, 1f);
                        e.AtkCd2 = 1.6f; e.SetState(Enemy.Recover, 0.6f);
                    }
                    else if (e.AttackKind == AkCast)
                    {
                        e.TX = Rnd.F() * M.TAU;
                        TollRing(e, 12 + phase * 4, e.TX, 112 + phase * 10);
                        if (phase >= 1) e.LurchT = 0.38f;
                        SndAt(Sfx.BellToll, e.X, e.Y, 1f); Shake(2.5f, 0.2f);
                        e.AtkCd = 4.2f - phase * 0.7f; e.SetState(Enemy.Recover, 0.5f);
                    }
                    else if (e.AttackKind == AkSigils)
                    {
                        int n = phase == 2 ? 5 : 3;
                        for (int k = 0; k < n; k++)
                        {
                            float a = Rnd.F() * M.TAU, d = k == 0 ? 0 : 18 + Rnd.F() * 36;
                            float sx = M.Clamp(p.X + M.Cos(a) * d, DungeonGen.ArenaX0, DungeonGen.ArenaX1), sy = M.Clamp(p.Y + M.Sin(a) * d * 0.7f, DungeonGen.ArenaY0, DungeonGen.ArenaY1);
                            AddSigil(sx, sy, 27, 0.9f + k * 0.28f, e.Dmg * 1.1f);
                        }
                        e.AtkCd3 = 6.5f - phase; e.SetState(Enemy.Recover, 0.4f);
                    }
                    else
                    {
                        // summon the faithful
                        for (int k = 0; k < 3; k++)
                        {
                            float a = k * M.TAU / 3 + Rnd.F(), d = 46 + Rnd.F() * 20;
                            float sx = M.Clamp(e.X + M.Cos(a) * d, DungeonGen.ArenaX0, DungeonGen.ArenaX1), sy = M.Clamp(e.Y + M.Sin(a) * d * 0.7f, DungeonGen.ArenaY0, DungeonGen.ArenaY1);
                            SpawnDef sd = new SpawnDef(); sd.Kind = EnemyKind.Hollow; sd.Summoned = true; sd.Variant = k; sd.Id = "summon";
                            Enemy h = SpawnEnemy(EnemyKind.Hollow, sx, sy, 6, sd);
                            h.Aggro = true; h.SetState(Enemy.Alert, 0.7f); h.FadeIn = 0;
                            Motes(h.X, h.Y, 0, 0xA7A0DC, 0x3A2E5E, 12, 8, 50, 0.7f);
                            Smoke(h.X, h.Y, 4, 4, 0x2E2843, 4f);
                            NetFx(FxSummon, h.X, h.Y);
                        }
                        e.SetState(Enemy.Recover, 0.6f);
                    }
                    break;
                case Enemy.Special:
                    // the spiral
                    e.TY += dt;
                    while (e.TY > 0.075f)
                    {
                        e.TY -= 0.075f;
                        float a = e.TX + e.StateT * 4.4f;
                        Shoot(Projectile.KGloam, e.X, e.Y, a, 122, e.Dmg * 0.7f, false, 2.4f);
                        Shoot(Projectile.KGloam, e.X, e.Y, a + M.PI, 122, e.Dmg * 0.7f, false, 2.4f);
                    }
                    if (Rnd.Chance(0.5f)) Motes(e.X, e.Y, 20, 0xC9D3F5, 0x3A2E5E, 1, 10, 50, 0.5f);
                    if (e.StateT >= e.StateDur) { e.StrafeT = 9f; e.SetState(Enemy.Recover, 1.3f); }
                    break;
                case Enemy.Vanish:
                    if (!e.HitDone && e.StateT >= e.StateDur * 0.5f)
                    {
                        e.HitDone = true;
                        Motes(e.X, e.Y, 14, 0xC9D3F5, 0x584688, 18, 10, 50, 0.6f);
                        for (int tries = 0; tries < 20; tries++)
                        {
                            float nx = DungeonGen.ArenaX0 + Rnd.F() * (DungeonGen.ArenaX1 - DungeonGen.ArenaX0);
                            float ny = DungeonGen.ArenaY0 + 30 + Rnd.F() * (DungeonGen.ArenaY1 - DungeonGen.ArenaY0 - 30);
                            if (M.Dist(nx, ny, p.X, p.Y) < 90 || Cur.Blocked(nx, ny, e.Radius, 1)) continue;
                            e.X = nx; e.Y = ny; break;
                        }
                        Motes(e.X, e.Y, 14, 0xEAF3FF, 0x7F6CB6, 18, 10, 50, 0.6f);
                    }
                    if (e.StateT >= e.StateDur) { e.SetState(Enemy.Chase, 0); e.FadeIn = 0; if (e.AtkCd > 0.5f) e.AtkCd = 0.5f; }
                    break;
                case Enemy.Recover:
                    if (e.StateT >= e.StateDur) e.SetState(Enemy.Chase, 0);
                    break;
                default: e.SetState(Enemy.Chase, 0); break;
            }
        }

        // villagers
        void UpdateNpcs(float dt)
        {
            Player p = P;
            for (int i = 0; i < Npcs.Count; i++)
            {
                Npc n = Npcs[i];
                n.AnimT += dt; n.Moving = false;
                float dist = M.Dist(n.X, n.Y, p.X, p.Y);
                if (dist < 46) { n.Facing = p.X >= n.X ? 1 : -1; continue; }
                if (!n.Wander) continue;
                n.WanderT -= dt;
                if (n.WanderT <= 0)
                {
                    n.WanderT = 3f + Rnd.F() * 5f;
                    float a = Rnd.F() * M.TAU, d = 14 + Rnd.F() * 40;
                    n.TX = n.HomeX + M.Cos(a) * d; n.TY = n.HomeY + M.Sin(a) * d * 0.7f;
                }
                float dx = n.TX - n.X, dy = n.TY - n.Y, len = M.Len(dx, dy);
                if (len > 3 && n.WanderT > 1f)
                {
                    float bx = n.X, by = n.Y;
                    Cur.Move(n, dx / len * 22 * dt, dy / len * 22 * dt, 1);
                    n.Moving = M.Dist(bx, by, n.X, n.Y) > 0.05f;
                    if (!n.Moving) n.WanderT = 0.5f;
                    if (Math.Abs(dx) > 1) n.Facing = dx > 0 ? 1 : -1;
                }
            }
        }
    }
}
