using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>Damage rules, projectiles, ground effects, loot and experience.</summary>
    public sealed partial class Game
    {
        public const int FxSlash = 0, FxPierce = 1, FxFire = 2, FxFrost = 3, FxShock = 4, FxPoison = 5, FxBlunt = 6;
        public bool GodMode;
        public double DealtTotal;        // all damage the local hero has dealt (the automated playtest measures abilities with it)

        /// <summary>Distance on the ground plane, matching the squashed circles the telegraphs are drawn with.</summary>
        public static float GDist(float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = (y1 - y0) * Surface.Squash;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        // player -> enemy
        public float RollDamage(float mult, out bool crit)
        {
            Player p = P;
            float d = (p.WMin + Rnd.F() * (p.WMax - p.WMin)) * p.DmgMult * mult;
            if (p.OathT > 0) d *= 1.25f;
            crit = p.NextCrit || Rnd.Chance(p.CritChance);
            if (crit) d *= p.CritMult;
            return d;
        }

        bool Behind(Enemy e) { return e.Facing > 0 ? P.X < e.X - 3 : P.X > e.X + 3; }

        public void Aggravate(Enemy e)
        {
            if (e.Dead) return;
            if (!e.Aggro)
            {
                e.Aggro = true; e.AlertShow = 0.7f;
                if (e.State == Enemy.Idle || e.State == Enemy.Return || e.State == Enemy.Search) e.SetState(Enemy.Chase, 0);
            }
            // the rest of the pack notices
            for (int i = 0; i < Foes.Count; i++)
            {
                Enemy o = Foes[i];
                if (o == e || o.Dead || o.Aggro || o.Def.Boss) continue;
                if (M.Dist2(o.X, o.Y, e.X, e.Y) < 110 * 110)
                {
                    o.Aggro = true; o.AlertShow = 0.7f;
                    if (o.State == Enemy.Idle || o.State == Enemy.Return || o.State == Enemy.Search) o.SetState(Enemy.Alert, 0.3f + Rnd.F() * 0.3f);
                }
            }
        }

        public const int StStun = 0, StFreeze = 1, StSlow = 2, StRoot = 3, StBurn = 4, StPoison = 5, StBleed = 6, StMark = 7;

        /// <summary>Puts a status on an enemy. A guest shows it at once and asks the host to make it real.</summary>
        public void SetStatus(Enemy e, int kind, float time, float power)
        {
            if (e == null || e.Dead) return;
            ApplyStatus(e, kind, time, power);
            if (IsClient) NetStatus(e, kind, time, power);
        }

        static void ApplyStatus(Enemy e, int kind, float t, float pw)
        {
            switch (kind)
            {
                case StStun: e.StunT = Math.Max(e.StunT, t); break;
                case StFreeze: e.FreezeT = Math.Max(e.FreezeT, t); break;
                case StSlow: e.SlowT = Math.Max(e.SlowT, t); e.SlowAmt = M.Clamp(pw, 0, 0.9f); break;
                case StRoot: e.RootT = Math.Max(e.RootT, t); break;
                case StBurn: e.BurnT = t; e.BurnDps = Math.Max(e.BurnDps, pw); break;
                case StPoison: e.PoisonT = t; e.PoisonDps = Math.Max(e.PoisonDps, pw); break;
                case StBleed: e.BleedT = t; e.BleedDps = Math.Max(e.BleedDps, pw); break;
                case StMark: e.MarkT = t; break;
            }
        }

        /// <summary>The local hero strikes an enemy. The numbers are worked out here; the host owns the enemy's health.</summary>
        public bool HitEnemy(Enemy e, float dmg, bool crit, float dirX, float dirY, float knock, int fx, bool melee)
        {
            if (e.Dead || e.State == Enemy.Vanish) return false;
            Player p = Me;
            string tag = null;
            if (p.StealthT > 0) { dmg *= 2.5f; crit = true; tag = "Ambush!"; BreakStealth(); }
            else if (melee && p.Class == ClassId.Rogue && Behind(e)) { dmg *= 1.5f; tag = "Backstab"; }
            if (e.MarkT > 0) dmg *= 1.35f;
            int idmg = Math.Max(1, (int)Math.Round(dmg));
            DealtTotal += idmg;
            int top = e.Spr != null ? e.Spr.Height : 20;
            if (Cfg.DamageNumbers) Float(e.X, e.Y - top - 2, crit ? idmg + "!" : idmg.ToString(), crit ? 0xFFC040 : 0xF2ECD8, crit);
            if (tag != null) Float(e.X, e.Y - top - 13, tag, 0xC08CF0, false);
            HitFx(e, fx, dirX, dirY, crit);
            if (p.Leech > 0) Heal(Math.Max(1, idmg * p.Leech), false);
            p.CombatT = 0;
            if (crit) { HitStop = Math.Max(HitStop, 0.05f); Shake(2.2f, 0.14f); }
            else if (melee) HitStop = Math.Max(HitStop, 0.018f);
            if (IsClient) { e.FlashT = 0.12f; e.HpBarT = 5f; NetHit(e, idmg, dirX, dirY, knock, fx, crit); }
            else HurtEnemy(e, idmg, dirX, dirY, knock, p);
            return true;
        }

        /// <summary>Host side: takes health off an enemy, whoever struck it.</summary>
        public void HurtEnemy(Enemy e, int idmg, float dirX, float dirY, float knock, Player by)
        {
            if (e.Dead) return;
            e.HP -= idmg; e.FlashT = 0.12f; e.HpBarT = 5f;
            float kr = 1f - e.Def.KnockRes;
            e.KX += dirX * knock * kr; e.KY += dirY * knock * kr;
            if (by != null && (e.Target == null || e.Target.Dead || !e.Aggro || Rnd.Chance(0.3f))) e.Target = by;
            Aggravate(e);
            if (e.HP <= 0) KillEnemy(e, dirX, dirY);
        }

        void HitFx(Enemy e, int fx, float dx, float dy, bool crit)
        {
            float cy = e.Y - 8;
            Splat(e.X, e.Y, 8, e.Def.BloodColor, crit ? 9 : 5, dx, dy, 70);
            switch (fx)
            {
                case FxFire: Sparks(e.X, e.Y, 8, 0xF7BE52, 6, 60, 0.4f); SndAt(Sfx.HitFlesh, e.X, e.Y, 0.6f); break;
                case FxFrost: Sparks(e.X, e.Y, 8, 0x9BDCF4, 6, 50, 0.45f); break;
                case FxShock: Sparks(e.X, e.Y, 10, 0xE2F8FF, 8, 90, 0.3f); break;
                case FxPoison: Sparks(e.X, e.Y, 8, 0xA8E04A, 4, 40, 0.4f); SndAt(Sfx.HitFlesh, e.X, e.Y, 0.6f); break;
                case FxPierce: SndAt(Sfx.ArrowHit, e.X, e.Y, 0.9f); break;
                default:
                    SndAt(e.Kind == EnemyKind.Archer || e.Kind == EnemyKind.Knight ? Sfx.HitBone : (fx == FxBlunt ? Sfx.HitHeavy : Sfx.HitFlesh), e.X, e.Y, 0.9f);
                    break;
            }
            if (crit) { SndAt(Sfx.Crit, e.X, e.Y, 0.9f); Sparks(e.X, cy + 8, 10, 0xFFF8DC, 7, 95, 0.3f); }
        }

        void DotDamage(Enemy e, float amount, int color)
        {
            if (e.Dead) return;
            int idmg = Math.Max(1, (int)Math.Round(amount * (e.MarkT > 0 ? 1.35f : 1f)));
            e.HP -= idmg; e.HpBarT = 5f;
            if (Cfg.DamageNumbers) Float(e.X, e.Y - (e.Spr != null ? e.Spr.Height : 20), idmg.ToString(), color, false);
            if (e.HP <= 0) KillEnemy(e, 0, -1);
        }

        /// <summary>Host side (or playing alone): an enemy dies. Every machine then runs FoeDeathLocal for its own hero.</summary>
        public void KillEnemy(Enemy e, float dx, float dy)
        {
            if (e.Dead) return;
            e.Dead = true; e.DeathT = 0; e.HP = 0; e.Aggro = false;
            bool summoned = e.Spawn != null && e.Spawn.Summoned;
            if (e.Spawn != null && e.Spawn.Once) Flags.Add("dead_" + e.Spawn.Id);
            NetFoeDied(e, dx, dy, summoned);
            FoeDeathLocal(e, dx, dy, summoned);
            Q.OnKill(e.Kind, this);
            if (e.Kind == EnemyKind.Prior) OnPriorDefeated(e);
        }

        /// <summary>What the local hero sees and earns when an enemy dies: experience for all, and each their own spoils.</summary>
        public void FoeDeathLocal(Enemy e, float dx, float dy, bool summoned)
        {
            Player keep = P; P = Me;
            Me.Kills++;
            int xp = (int)Math.Round(e.Def.XP * Math.Max(0.5f, 1f + 0.2f * (e.Level - e.Def.Level)));
            if (summoned) xp /= 4;
            GainXp(xp, true);
            SndAt(e.Def.Undead ? Sfx.UndeadDie : Sfx.EnemyDie, e.X, e.Y, 1f);
            Smoke(e.X, e.Y, 8, 6, 0x2E2843, 4f);
            Motes(e.X, e.Y, 6, e.Def.Undead ? 0xC9D3F5 : 0xA7A0DC, 0x3A2E5E, 10, 8, 26, 0.9f);
            Splat(e.X, e.Y, 8, e.Def.BloodColor, 8, dx, dy, 80);
            if (!summoned) DropLoot(e);
            if (e.Def.Elite || e.Def.Boss)
            {
                Shake(5f, 0.5f); HitStop = 0.14f; Flash(0xFFFFFF, 0.35f);
                BossSpoils(e.Kind, e.X, e.Y);
            }
            P = keep;
        }

        // enemy -> player
        public void DamagePlayer(float dmg, float sx, float sy, bool melee, Enemy src, int status, float knock)
        {
            Player p = P;
            // a companion's hero is hurt on their own machine, where their block and dodge are known
            if (p.Remote) { if (!p.Dead) NetDamage(p, dmg, sx, sy, melee, src, status, knock); return; }
            if (p.Dead || p.IFrames > 0) return;
            float dx = p.X - sx, dy = p.Y - sy, len = M.Len(dx, dy);
            if (len < 0.01f) { dx = 0; dy = 1; len = 1; }
            dx /= len; dy /= len;
            bool blocked = false;
            if (p.Blocking)
            {
                float toSrc = M.Atan2(-dy, -dx);
                if (Math.Abs(M.AngDiff(p.AimAngle, toSrc)) < 1.5f)
                {
                    blocked = true; dmg *= 0.15f; knock *= 0.3f;
                    p.Res = Math.Min(p.MaxRes, p.Res + 7);
                    Sparks(p.X + M.Cos(p.AimAngle) * 8, p.Y, 10, 0xFFF8DC, 6, 80, 0.25f);
                    Snd(Sfx.Block, 0.9f, 0.95f + Rnd.F() * 0.1f);
                    if (melee && src != null && p.BlockT < 0.3f && !src.Def.Boss) { SetStatus(src, StStun, src.Def.Elite ? 0.6f : 1.1f, 0); Float(p.X, p.Y - 30, "Perfect block!", 0xFFF8DC, false); }
                }
            }
            dmg *= 1f - p.ArmorReduction;
            if (p.OathT > 0) dmg *= 0.5f;
            int idmg = Math.Max(1, (int)Math.Round(dmg));
            if (GodMode) idmg = 0;
            p.HP -= idmg; p.CombatT = 0;
            if (!blocked)
            {
                p.HurtT = 0.4f; p.FlashT = 0.12f;
                Snd(Sfx.PlayerHurt, 0.9f, 0.95f + Rnd.F() * 0.1f);
                Shake(2.5f + Math.Min(4, idmg * 0.08f), 0.2f);
                Splat(p.X, p.Y, 9, 0x9E3123, 5, dx, dy, 60);
                BreakStealth();
                if (p.Class == ClassId.Warrior) p.Res = Math.Min(p.MaxRes, p.Res + 5);
                if (status == 1) { p.PoisonT = Math.Max(p.PoisonT, 4f); p.PoisonDps = Math.Max(p.PoisonDps, 2.5f + (src != null ? src.Level : 2)); }
                else if (status == 2) { p.SlowT = Math.Max(p.SlowT, 1.6f); p.SlowAmt = 0.35f; }
            }
            p.KX += dx * knock; p.KY += dy * knock;
            if (idmg > 0) Float(p.X, p.Y - 28, "-" + idmg, blocked ? 0xBAC6D2 : 0xE0584E, !blocked && idmg > p.MaxHP * 0.2f);
            if (p.HP <= 0) PlayerDies();
        }

        public void Heal(float amount, bool show)
        {
            Player p = P;
            if (p.Dead || amount <= 0) return;
            float before = p.HP;
            p.HP = Math.Min(p.MaxHP, p.HP + amount);
            int got = (int)Math.Round(p.HP - before);
            if (show && got > 0) Float(p.X, p.Y - 28, "+" + got, 0x8FCB6B, false);
        }

        public void BreakStealth()
        {
            if (P.StealthT <= 0) return;
            P.StealthT = 0;
            Smoke(P.X, P.Y, 8, 5, 0x444E70, 3f);
        }

        void PlayerDies()
        {
            Player p = P;
            if (p.Dead) return;
            p.HP = 0; p.Dead = true; p.Deaths++;
            p.Blocking = false; p.DashT = 0; p.DodgeT = 0; p.AtkT = 0; p.WhirlT = 0; p.StealthT = 0;
            DeathT = 0;
            Snd(Sfx.PlayerDie);
            Shake(6, 0.5f); HitStop = 0.2f;
            Splat(p.X, p.Y, 10, 0x9E3123, 14, 0, -1, 90);
            SetMusic(-1);
        }

        public float DeathT;

        public void Respawn()
        {
            // in a party nobody resets the world by dying: you rise beside a companion
            Player ally = null;
            if (Online)
            {
                foreach (Player o in Others) if (!o.Dead) { ally = o; break; }
                if (ally == null && IsClient && Others.Count > 0) ally = Others[0];
            }
            if (ally != null)
            {
                FadeThrough(delegate
                {
                    Player p = Me;
                    p.Dead = false; p.Recalc(true);
                    int lost = p.Gold / 10; p.Gold -= lost;
                    Ov = Overlay.None;
                    float x = ally.X, y = ally.Y + 8;
                    Cur.FindFree(ref x, ref y, p.Radius, 1);
                    p.X = x; p.Y = y; p.KX = p.KY = 0; p.ClearStatus();
                    p.IFrames = 2f;
                    SnapCamera();
                    ZoneId = -1; UpdateZone(true);
                    if (BossFight) SetMusic(Zones.MusBoss);
                    Motes(p.X, p.Y, 0, 0xFDE999, 0xD2AC52, 24, 12, 50, 1f);
                    Snd(Sfx.Shrine, 0.7f, 1.1f);
                    if (lost > 0) Notify(Loc.F("The Gloam took its tithe: ^y{0} gold^0", lost));
                });
                return;
            }
            FadeThrough(delegate
            {
                Player p = Me;
                p.Dead = false; p.Recalc(true);
                int lost = p.Gold / 10; p.Gold -= lost;
                Map m = MapById(p.RespawnMap);
                Ov = Overlay.None;
                // the bossgate must not trap a returning player outside the arena
                SetBossGate(false);
                EnterMap(m, p.RespawnX, p.RespawnY + 14);
                ApplyFlags(m);
                p.IFrames = 1.5f;
                if (lost > 0) Notify(Loc.F("The Gloam took its tithe: ^y{0} gold^0", lost));
                ZoneBannerT = 0;
            });
        }

        // statuses
        void UpdateStatuses(Actor a, float dt)
        {
            if (a.StunT > 0) a.StunT -= dt;
            if (a.RootT > 0) a.RootT -= dt;
            if (a.SlowT > 0) a.SlowT -= dt;
            if (a.MarkT > 0) a.MarkT -= dt;
            if (a.FreezeT > 0) a.FreezeT -= dt;
            if (a.FlashT > 0) a.FlashT -= dt;
            bool any = a.BurnT > 0 || a.PoisonT > 0 || a.BleedT > 0;
            if (!any) { a.DotAcc = 0; return; }
            a.DotAcc += dt;
            if (a.DotAcc >= 0.5f)
            {
                a.DotAcc -= 0.5f;
                Enemy e = a as Enemy;
                if (a.BurnT > 0)
                {
                    if (e != null) DotDamage(e, a.BurnDps * 0.5f, 0xF09040); else PlayerDot(a.BurnDps * 0.5f, 0xF09040);
                    Sparks(a.X, a.Y, 8, 0xEC8A32, 2, 30, 0.4f);
                }
                if (a.PoisonT > 0 && !a.Dead)
                {
                    if (e != null) DotDamage(e, a.PoisonDps * 0.5f, 0xA8E04A); else PlayerDot(a.PoisonDps * 0.5f, 0xA8E04A);
                    Motes(a.X, a.Y, 6, 0xA8E04A, 0x3F6A24, 2, 5, 14, 0.5f);
                }
                if (a.BleedT > 0 && !a.Dead)
                {
                    if (e != null) DotDamage(e, a.BleedDps * 0.5f, 0xE0584E); else PlayerDot(a.BleedDps * 0.5f, 0xE0584E);
                }
            }
            if (a.BurnT > 0) a.BurnT -= dt;
            if (a.PoisonT > 0) a.PoisonT -= dt;
            if (a.BleedT > 0) a.BleedT -= dt;
        }

        void PlayerDot(float amount, int color)
        {
            Player p = P;
            if (p.Dead || GodMode) return;
            int idmg = Math.Max(1, (int)Math.Round(amount));
            p.HP -= idmg; p.CombatT = 0; p.HurtT = Math.Max(p.HurtT, 0.15f);
            Float(p.X, p.Y - 28, "-" + idmg, color, false);
            if (p.HP <= 0) PlayerDies();
        }

        void ApplyKnock(Actor a, float dt, int mask)
        {
            if (Math.Abs(a.KX) + Math.Abs(a.KY) < 2f) { a.KX = a.KY = 0; return; }
            Cur.Move(a, a.KX * dt, a.KY * dt, mask);
            float k = Math.Max(0, 1f - 9f * dt);
            a.KX *= k; a.KY *= k;
        }

        // projectiles
        public Projectile Shoot(int kind, float x, float y, float ang, float speed, float dmg, bool fromPlayer, float life)
        {
            Projectile pr = new Projectile();
            pr.Kind = kind; pr.X = x; pr.Y = y; pr.Angle = ang; pr.VX = M.Cos(ang) * speed; pr.VY = M.Sin(ang) * speed;
            pr.Dmg = dmg; pr.FromPlayer = fromPlayer; pr.Life = life; pr.Z = 10;
            switch (kind)
            {
                case Projectile.KArrow: pr.Spr = Assets.Arrow; break;
                case Projectile.KPierce: pr.Spr = Assets.Arrow; pr.Pierce = true; pr.Hit = new List<Actor>(); pr.LightColor = 0xFFE0A0; pr.LightR = 26; pr.Radius = 4; break;
                case Projectile.KMarked: pr.Spr = Assets.Arrow; pr.LightColor = 0xFF6060; pr.LightR = 26; pr.Turn = 9f; break;
                case Projectile.KKnife: pr.Spr = Assets.Knife; break;
                case Projectile.KPoisonKnife: pr.Spr = Assets.Knife; pr.LightColor = 0x80FF60; pr.LightR = 14; break;
                case Projectile.KEmber: pr.Spr = Assets.EmberBolt; pr.LightColor = 0xFFB060; pr.LightR = 40; break;
                case Projectile.KFireball: pr.Spr = Assets.Fireball; pr.LightColor = 0xFFA040; pr.LightR = 74; pr.Radius = 5; break;
                case Projectile.KBone: pr.Spr = Assets.BoneArrow; pr.LightColor = 0x80D0FF; pr.LightR = 18; break;
                case Projectile.KGloam: pr.Spr = Assets.GloamBolt; pr.LightColor = 0xA890FF; pr.LightR = 34; break;
                case Projectile.KGlob: pr.Spr = Assets.VenomOrb; pr.LightColor = 0x80FF60; pr.LightR = 22; break;
                case Projectile.KWispOrb: pr.Spr = Assets.WispOrb; pr.LightColor = 0xC0C8FF; pr.LightR = 34; pr.Turn = 1.9f; break;
            }
            Shots.Add(pr);
            return pr;
        }

        void UpdateShots(float dt)
        {
            for (int i = Shots.Count - 1; i >= 0; i--)
            {
                if (i >= Shots.Count) continue;         // the last shot cleared the list (a boss fell)
                Projectile pr = Shots[i];
                pr.Life -= dt;
                if (pr.Lob)
                {
                    // lobbed: glides to its landing point, ignoring everything on the way
                    pr.X += pr.VX * dt; pr.Y += pr.VY * dt;
                    pr.VZ -= 220 * dt; pr.Z += pr.VZ * dt;
                    if (pr.Life <= 0 || pr.Z <= 0) { LandGlob(pr); Lists.RemoveAt(Shots, i, pr); }
                    continue;
                }
                if (pr.Target != null && !pr.Target.Dead)
                {
                    float want = M.Atan2(pr.Target.Y - pr.Y, pr.Target.X - pr.X);
                    float diff = M.AngDiff(pr.Angle, want);
                    float step = pr.Turn * dt;
                    pr.Angle += M.Clamp(diff, -step, step);
                    float sp = M.Len(pr.VX, pr.VY);
                    pr.VX = M.Cos(pr.Angle) * sp; pr.VY = M.Sin(pr.Angle) * sp;
                }
                float nx = pr.X + pr.VX * dt, ny = pr.Y + pr.VY * dt;
                if (Cur.PointBlocked(nx, ny, 2))
                {
                    WallImpact(pr);
                    Lists.RemoveAt(Shots, i, pr); continue;
                }
                pr.X = nx; pr.Y = ny;
                // trails
                pr.TrailAcc += dt;
                if (pr.TrailAcc > 0.03f)
                {
                    pr.TrailAcc = 0;
                    if (pr.Kind == Projectile.KEmber) Trail(pr.X, pr.Y, pr.Z, 0xEC8A32, 0.22f);
                    else if (pr.Kind == Projectile.KFireball) { Trail(pr.X, pr.Y, pr.Z, 0xF7BE52, 0.35f); Trail(pr.X, pr.Y, pr.Z, 0xCF5327, 0.3f); }
                    else if (pr.Kind == Projectile.KGloam || pr.Kind == Projectile.KWispOrb) Trail(pr.X, pr.Y, pr.Z, 0x7F6CB6, 0.3f);
                    else if (pr.Kind == Projectile.KPierce) Trail(pr.X, pr.Y, pr.Z, 0xFDE999, 0.18f);
                    else if (pr.Kind == Projectile.KMarked) Trail(pr.X, pr.Y, pr.Z, 0xE0584E, 0.2f);
                    else if (pr.Kind == Projectile.KPoisonKnife) Trail(pr.X, pr.Y, pr.Z, 0x6FA82E, 0.2f);
                }
                bool gone = false;
                if (pr.FromPlayer)
                {
                    for (int k = 0; k < Foes.Count && !gone; k++)
                    {
                        Enemy e = Foes[k];
                        if (e.Dead || e.State == Enemy.Vanish) continue;
                        float rr = e.Radius + pr.Radius + 3;
                        float ddx = e.X - pr.X, ddy = (e.Y - 2) - pr.Y;
                        if (ddx * ddx + ddy * ddy > rr * rr) continue;
                        if (pr.Hit != null) { if (pr.Hit.Contains(e)) continue; pr.Hit.Add(e); }
                        if (!pr.Ghost) ProjectileHit(pr, e);            // a companion's shot: their game decides what it did
                        if (!pr.Pierce) gone = true;
                    }
                    if (!gone && !pr.Ghost) HitPropsAt(pr.X, pr.Y, 7, 1);
                }
                else
                {
                    Player p = Me;
                    if (!p.Dead)
                    {
                        float rr = p.Radius + pr.Radius + 2;
                        float ddx = p.X - pr.X, ddy = (p.Y - 2) - pr.Y;
                        if (ddx * ddx + ddy * ddy <= rr * rr && p.IFrames <= 0)
                        {
                            int status = pr.Kind == Projectile.KWispOrb ? 2 : 0;
                            P = p;
                            DamagePlayer(pr.Dmg, pr.X - pr.VX * 0.05f, pr.Y - pr.VY * 0.05f, false, null, status, 40);
                            Sparks(pr.X, pr.Y, 10, pr.LightColor != 0 ? pr.LightColor : 0xFFFFFF, 5, 60, 0.3f);
                            gone = true;
                        }
                    }
                    // it struck a companion: their own game decides what it did to them
                    for (int k = 0; k < Others.Count && !gone; k++)
                    {
                        Player o = Others[k];
                        if (o.Dead || o.IFrames > 0) continue;
                        float rr = o.Radius + pr.Radius + 2;
                        float ddx = o.X - pr.X, ddy = (o.Y - 2) - pr.Y;
                        if (ddx * ddx + ddy * ddy > rr * rr) continue;
                        Sparks(pr.X, pr.Y, 10, pr.LightColor != 0 ? pr.LightColor : 0xFFFFFF, 5, 60, 0.3f);
                        gone = true;
                    }
                }
                if (gone) { Lists.RemoveAt(Shots, i, pr); continue; }
                if (pr.Life <= 0)
                {
                    if (pr.Kind == Projectile.KFireball && !pr.Ghost) Explode(pr.X, pr.Y, pr.Dmg, pr.Crit);
                    Lists.RemoveAt(Shots, i, pr);
                }
            }
        }

        void WallImpact(Projectile pr)
        {
            if (pr.Kind == Projectile.KFireball) { if (!pr.Ghost) Explode(pr.X, pr.Y, pr.Dmg, pr.Crit); return; }
            if (pr.FromPlayer && !pr.Ghost) HitPropsAt(pr.X + pr.VX * 0.02f, pr.Y + pr.VY * 0.02f, 9, 1);
            int c = pr.LightColor != 0 ? pr.LightColor : 0xC8BE9F;
            Sparks(pr.X, pr.Y, pr.Z, c, 4, 50, 0.25f);
            SndAt(Sfx.ArrowHit, pr.X, pr.Y, 0.35f);
        }

        void ProjectileHit(Projectile pr, Enemy e)
        {
            float sp = M.Len(pr.VX, pr.VY);
            float dx = sp > 0 ? pr.VX / sp : 1, dy = sp > 0 ? pr.VY / sp : 0;
            switch (pr.Kind)
            {
                case Projectile.KFireball: Explode(pr.X, pr.Y, pr.Dmg, pr.Crit); break;
                case Projectile.KEmber: HitEnemy(e, pr.Dmg, pr.Crit, dx, dy, 40, FxFire, false); break;
                case Projectile.KPoisonKnife:
                    HitEnemy(e, pr.Dmg, pr.Crit, dx, dy, 30, FxPoison, false);
                    SetStatus(e, StPoison, 4f, (Me.WMin + Me.WMax) * 0.5f * Me.DmgMult * 0.3f);
                    break;
                case Projectile.KKnife: HitEnemy(e, pr.Dmg, pr.Crit, dx, dy, 40, FxPierce, false); break;
                default: HitEnemy(e, pr.Dmg, pr.Crit, dx, dy, pr.Knock, FxPierce, false); break;
            }
        }

        /// <summary>Fireball detonation.</summary>
        public void Explode(float x, float y, float dmg, bool crit)
        {
            const float R = 40;
            for (int k = 0; k < Foes.Count; k++)
            {
                Enemy e = Foes[k];
                if (e.Dead) continue;
                float d = M.Dist(e.X, e.Y, x, y);
                if (d > R + e.Radius) continue;
                float dx = d > 0.5f ? (e.X - x) / d : 0, dy = d > 0.5f ? (e.Y - y) / d : -1;
                HitEnemy(e, dmg, crit, dx, dy, 110, FxFire, false);
                SetStatus(e, StBurn, 3f, (Me.WMin + Me.WMax) * 0.5f * Me.DmgMult * 0.25f);
            }
            HitPropsAt(x, y, R, 3);
            Area a = new Area(); a.Kind = Area.Burst; a.X = x; a.Y = y; a.R = R; a.Dur = 0.35f; a.Color = 0xFFA040; AddArea(a);
            Sparks(x, y, 10, 0xF7BE52, 22, 150, 0.55f);
            Smoke(x, y, 8, 8, 0x3A2A2A, 5f);
            RingBurst(x, y, 0xEC8A32, 18, 120, 0.4f);
            SndAt(Sfx.Explode, x, y, 1f);
            Shake(4f, 0.25f);
        }

        void LandGlob(Projectile pr)
        {
            float x = pr.TXg, y = pr.TYg;
            Area a = new Area(); a.Kind = Area.Pool; a.X = x; a.Y = y; a.R = 15; a.Dur = 3.2f; a.Dmg = pr.Dmg * 0.35f; a.Color = 0x6FA82E; Areas.Add(a);
            Sparks(x, y, 4, 0xA8E04A, 8, 60, 0.4f);
            SndAt(Sfx.Splash, x, y, 0.8f);
            if (!P.Dead && M.Dist(P.X, P.Y, x, y) < 15 + P.Radius) DamagePlayer(pr.Dmg, x, y, false, null, 1, 30);
        }

        // ground effects
        void UpdateAreas(float dt)
        {
            Player me = Me;
            for (int i = Areas.Count - 1; i >= 0; i--)
            {
                if (i >= Areas.Count) continue;
                Area a = Areas[i];
                a.T += dt;
                switch (a.Kind)
                {
                    case Area.Trap:
                        if (!a.Armed && a.T >= 0.4f) { a.Armed = true; SndAt(Sfx.TrapSet, a.X, a.Y, 0.5f); }
                        if (a.Armed)
                        {
                            for (int k = 0; k < Foes.Count; k++)
                            {
                                Enemy e = Foes[k];
                                if (e.Dead || e.Def.Flying || e.State == Enemy.Vanish) continue;
                                if (M.Dist2(e.X, e.Y, a.X, a.Y) > (14 + e.Radius) * (14 + e.Radius)) continue;
                                if (!a.Ghost)
                                {
                                    bool crit; float d = RollDamage(1.2f, out crit);
                                    HitEnemy(e, d, crit, 0, -0.2f, 0, FxPierce, false);
                                    SetStatus(e, StRoot, e.Def.Elite || e.Def.Boss ? 1.2f : 3f, 0);
                                    SetStatus(e, StBleed, 3f, (me.WMin + me.WMax) * 0.5f * me.DmgMult * 0.15f);
                                }
                                SndAt(Sfx.TrapSnap, a.X, a.Y, 1f);
                                Sparks(a.X, a.Y, 4, 0xDCE3E6, 10, 80, 0.3f);
                                a.Done = true; break;
                            }
                        }
                        if (a.T > a.Dur) a.Done = true;
                        break;
                    case Area.Rain:
                        if (a.T >= a.Delay)
                        {
                            // visual: streaks of falling arrows
                            for (int n = 0; n < 2; n++)
                            {
                                float ang = Rnd.F() * M.TAU, rd = M.Sqrt(Rnd.F()) * a.R;
                                Particle p = new Particle();
                                p.X = a.X + M.Cos(ang) * rd; p.Y = a.Y + M.Sin(ang) * rd / Surface.Squash; p.Z = 70 + Rnd.F() * 30; p.VZ = -420;
                                p.Life = p.Max = 0.22f; p.Color = 0xE8E0C4; p.Color2 = 0xC8BE9F; p.Kind = PStreak;
                                AddPart(p);
                            }
                            a.TickT -= dt;
                            if (a.TickT <= 0)
                            {
                                a.TickT = 0.4f;
                                SndAt(Sfx.ArrowHit, a.X, a.Y, 0.5f);
                                if (!a.Ghost)
                                {
                                    for (int k = 0; k < Foes.Count; k++)
                                    {
                                        Enemy e = Foes[k];
                                        if (e.Dead || e.State == Enemy.Vanish) continue;
                                        float ddx = e.X - a.X, ddy = (e.Y - a.Y) * Surface.Squash;
                                        if (ddx * ddx + ddy * ddy > (a.R + e.Radius) * (a.R + e.Radius)) continue;
                                        bool crit; float d = RollDamage(0.45f, out crit);
                                        HitEnemy(e, d, crit, 0, 0, 0, FxPierce, false);
                                        SetStatus(e, StSlow, 1.2f, 0.4f);
                                    }
                                    HitPropsAt(a.X, a.Y, a.R * 0.8f, 1);
                                }
                            }
                        }
                        if (a.T > a.Delay + a.Dur) a.Done = true;
                        break;
                    case Area.Pool:
                        if (!me.Dead && me.IFrames <= 0 && GDist(me.X, me.Y, a.X, a.Y) < a.R + 2)
                        {
                            me.PoisonT = Math.Max(me.PoisonT, 1.2f); me.PoisonDps = Math.Max(me.PoisonDps, a.Dmg);
                        }
                        if (Rnd.Chance(0.12f)) Motes(a.X + Rnd.Sym(a.R * 0.7f), a.Y + Rnd.Sym(a.R * 0.4f), 1, 0xA8E04A, 0x3F6A24, 1, 1, 12, 0.5f);
                        if (a.T > a.Dur) a.Done = true;
                        break;
                    case Area.Sigil:
                        if (!a.Armed && a.T >= a.Delay)
                        {
                            a.Armed = true;
                            if (!a.Ghost && !me.Dead && GDist(me.X, me.Y, a.X, a.Y) < a.R + 2) { P = me; DamagePlayer(a.Dmg, a.X, a.Y, false, null, 0, 70); }
                            RingBurst(a.X, a.Y, 0xA7A0DC, 16, 110, 0.4f);
                            Motes(a.X, a.Y, 2, 0xC9D3F5, 0x584688, 14, a.R * 0.8f, 60, 0.6f);
                            SndAt(Sfx.SigilBlast, a.X, a.Y, 1f);
                            Shake(2.5f, 0.15f);
                        }
                        if (a.T > a.Delay + 0.4f) a.Done = true;
                        break;
                    default:
                        if (a.T > a.Dur) a.Done = true;
                        break;
                }
                if (a.Done) Lists.RemoveAt(Areas, i, a);
            }
        }

        public Area AddSigil(float x, float y, float r, float delay, float dmg)
        {
            Area a = new Area(); a.Kind = Area.Sigil; a.X = x; a.Y = y; a.R = r; a.Delay = delay; a.Dmg = dmg; a.Color = 0xA7A0DC; a.Seed = Rnd.Int(1000);
            AddArea(a);
            return a;
        }

        // loot
        public Pickup SpawnPickup(int kind, float x, float y, int amount, Item it)
        {
            Pickup d = new Pickup();
            d.Kind = kind; d.X = x; d.Y = y; d.Amount = amount; d.It = it;
            float a = Rnd.F() * M.TAU, s = 30 + Rnd.F() * 50;
            d.VX = M.Cos(a) * s; d.VY = M.Sin(a) * s * 0.6f; d.VZ = 110 + Rnd.F() * 60; d.Z = 4;
            Drops.Add(d);
            return d;
        }

        void DropGold(float x, float y, int total)
        {
            if (total <= 0) return;
            int piles = total > 40 ? 5 : (total > 12 ? 3 : (total > 4 ? 2 : 1));
            int left = total;
            for (int i = 0; i < piles; i++)
            {
                int n = i == piles - 1 ? left : Math.Max(1, total / piles);
                left -= n;
                if (n > 0) SpawnPickup(Pickup.Gold, x, y, n, null);
            }
        }

        void DropLoot(Enemy e)
        {
            EnemyDef d = e.Def;
            DropGold(e.X, e.Y, Rnd.Range(d.GoldMin, d.GoldMax) + e.Level / 2);
            if (Rnd.Chance(d.Elite || d.Boss ? 1f : 0.13f)) SpawnPickup(Pickup.Health, e.X, e.Y, 1, null);
            if (Rnd.Chance(d.Elite || d.Boss ? 1f : 0.06f)) SpawnPickup(Pickup.Spirit, e.X, e.Y, 1, null);
            int ilvl = Math.Max(1, e.Level + Rnd.Int(2));
            if (d.Boss)
            {
                SpawnPickup(Pickup.Equip, e.X, e.Y, 1, Items.Unique("boss", Me.Class, ilvl + 1));
                SpawnPickup(Pickup.Equip, e.X, e.Y, 1, Items.RollEquip(Rnd, ilvl, Me.Class, 2));
                SpawnPickup(Pickup.Health, e.X, e.Y, 1, null);
            }
            else if (d.Elite)
            {
                SpawnPickup(Pickup.Equip, e.X, e.Y, 1, Items.Weapon(Rnd, Me.Def.WeaponTypes[0], ilvl + 1, Rarity.Rare));
                SpawnPickup(Pickup.Equip, e.X, e.Y, 1, Items.Unique("signet", Me.Class, ilvl));
            }
            else if (e.Kind == EnemyKind.AlphaWolf)
            {
                SpawnPickup(Pickup.Equip, e.X, e.Y, 1, Items.RollEquip(Rnd, ilvl + 1, Me.Class, 1));
            }
            else if (Rnd.Chance(d.LootChance))
            {
                SpawnPickup(Pickup.Equip, e.X, e.Y, 1, Items.RollEquip(Rnd, ilvl, Me.Class, 0));
            }
        }

        void UpdateDrops(float dt)
        {
            Player p = P;
            for (int i = Drops.Count - 1; i >= 0; i--)
            {
                if (i >= Drops.Count) continue;
                Pickup d = Drops[i];
                d.T += dt;
                if (d.Z > 0 || d.VZ != 0)
                {
                    float nx = d.X + d.VX * dt, ny = d.Y + d.VY * dt;
                    if (!Cur.PointBlocked(nx, ny, 1)) { d.X = nx; d.Y = ny; } else { d.VX = -d.VX * 0.4f; d.VY = -d.VY * 0.4f; }
                    d.Z += d.VZ * dt; d.VZ -= 520 * dt;
                    if (d.Z <= 0)
                    {
                        d.Z = 0;
                        if (d.VZ < -90) { d.VZ = -d.VZ * 0.35f; d.VX *= 0.5f; d.VY *= 0.5f; }
                        else { d.VZ = 0; d.VX = 0; d.VY = 0; }
                    }
                }
                if (p.Dead || d.T < 0.45f) continue;
                float dist = M.Dist(d.X, d.Y, p.X, p.Y);
                bool auto = d.Kind != Pickup.Equip || p.FreeBagSlot() >= 0;
                if (auto && dist < 44 && d.Kind != Pickup.Equip)
                {
                    // small things are drawn toward the player
                    float pull = 220 * dt / Math.Max(4, dist);
                    d.X += (p.X - d.X) * pull; d.Y += (p.Y - d.Y) * pull;
                }
                if (dist < 12 && auto) { Collect(d); Lists.RemoveAt(Drops, i, d); }
                else if (dist < 12 && d.Kind == Pickup.Equip && bagFullT <= 0) { Notify("^rYour pack is full.^0"); bagFullT = 3f; }
            }
            if (bagFullT > 0) bagFullT -= dt;
        }
        float bagFullT;

        void Collect(Pickup d)
        {
            Player p = Me;
            switch (d.Kind)
            {
                case Pickup.Gold:
                    p.Gold += d.Amount; Float(p.X, p.Y - 26, "+" + d.Amount, 0xF0C060, false); Snd(Sfx.Coin, 0.7f, 0.95f + Rnd.F() * 0.15f); break;
                case Pickup.Health: p.HealthPotions += d.Amount; Notify(Loc.F("Health Draught  ^d({0})^0", p.HealthPotions)); Snd(Sfx.PickPotion); break;
                case Pickup.Spirit: p.SpiritPotions += d.Amount; Notify(Loc.F("Spirit Draught  ^d({0})^0", p.SpiritPotions)); Snd(Sfx.PickPotion); break;
                case Pickup.Equip:
                    p.AddToBag(d.It);
                    Notify("^" + Items.RarityCode[(int)d.It.Rar] + d.It.DisplayName + "^0");
                    Snd(Sfx.PickItem, 1f, 1f + (int)d.It.Rar * 0.08f);
                    if (d.It.Rar >= Rarity.Rare) Sparks(p.X, p.Y, 12, Items.RarityColor[(int)d.It.Rar], 10, 70, 0.5f);
                    break;
                case Pickup.Quest:
                    // quest items are the party's: whoever finds one, everyone carries it
                    GrantKeepsake(d.It.QuestId);
                    Q.OnItem(d.It.QuestId, this);
                    NetKeepsake(d.It.QuestId);
                    break;
            }
        }

        // experience
        public void GainXp(int amount, bool show)
        {
            Player p = Me;
            if (p == null || amount <= 0 || p.Level >= Player.MaxLevel) return;
            p.XP += amount;
            if (show) Float(p.X, p.Y - 38, Loc.F("+{0} xp", amount), 0x7FE0D8, false);
            while (p.Level < Player.MaxLevel && p.XP >= p.XpToNext)
            {
                p.XP -= p.XpToNext;
                p.Level++;
                p.AttrPoints += 3;
                p.Vigor += 1;
                if (p.Def.Primary == 0) p.Might++; else if (p.Def.Primary == 1) p.Finesse++; else p.Arcana++;
                p.Recalc(false);
                p.HP = p.MaxHP; if (p.Class != ClassId.Warrior) p.Res = p.MaxRes;
                LevelUpT = 0;
                Snd(Sfx.LevelUp);
                Motes(p.X, p.Y, 0, 0xFDE999, 0xEC8A32, 40, 14, 70, 1.2f);
                RingBurst(p.X, p.Y, 0xF7BE52, 26, 140, 0.6f);
                Flash(0xFFE8A0, 0.25f);
                Notify(Loc.F("^yLevel {0}!^0  3 attribute points to spend (^yC^0)", p.Level));
                foreach (AbilityDef a in p.Def.Abilities)
                    if (a.UnlockLevel == p.Level) Notify(Loc.F("^cNew ability:^0 {0}  ^d[{1}]^0", Loc.Tr(a.Name), Loc.Tr(a.Key)));
            }
            if (p.Level >= Player.MaxLevel) p.XP = 0;
        }
        public float LevelUpT = 99;
    }
}
