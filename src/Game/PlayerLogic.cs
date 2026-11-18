using System;
using System.Collections.Generic;

namespace Vesperfell
{
    public struct Ghost { public float X, Y, Life, Max; public Sprite S; public bool Flip; public int Color; }

    /// <summary>Player control: movement, dodging, basic attacks and every class ability.</summary>
    public sealed partial class Game
    {
        public float IntroT;
        public readonly List<Ghost> Ghosts = new List<Ghost>();
        float errT, inputLockT;

        void Err(string msg)
        {
            if (errT > 0) return;
            errT = 0.7f;
            Snd(Sfx.UiError, 0.6f, 1f);
            if (msg != null) Float(P.X, P.Y - 34, msg, 0xE0584E, false);
        }

        void AddGhost(int color)
        {
            Ghost g = new Ghost();
            g.X = P.X; g.Y = P.Y; g.S = P.Frame(); g.Flip = P.Facing < 0; g.Life = g.Max = 0.22f; g.Color = color;
            if (Ghosts.Count < 24) Ghosts.Add(g);
        }

        void UpdatePlayer(float dt)
        {
            Player p = P;
            if (errT > 0) errT -= dt;
            if (inputLockT > 0) inputLockT -= dt;
            for (int i = Ghosts.Count - 1; i >= 0; i--) { Ghost g = Ghosts[i]; g.Life -= dt; if (g.Life <= 0) Ghosts.RemoveAt(i); else Ghosts[i] = g; }
            if (p.Dead)
            {
                DeathT += dt;
                // online the world keeps moving, so whatever was open gives way to the death screen
                if (Online && Ov != Overlay.None && Ov != Overlay.Death && Ov != Overlay.Pause && Ov != Overlay.Settings) { Ov = Overlay.None; Dlg = null; }
                if (DeathT > 1.4f && Ov == Overlay.None) { Ov = Overlay.Death; }
                return;
            }
            UpdateStatuses(p, dt);
            if (p.Dead) return;
            for (int i = 0; i < 5; i++) if (p.Cd[i] > 0) p.Cd[i] -= dt;
            if (p.AtkCd > 0) p.AtkCd -= dt;
            if (p.PotionCd > 0) p.PotionCd -= dt;
            if (p.IFrames > 0) p.IFrames -= dt;
            if (p.HurtT > 0) p.HurtT -= dt;
            if (p.CastT > 0) p.CastT -= dt;
            if (p.BowT > 0) p.BowT -= dt;
            if (p.OathT > 0) { p.OathT -= dt; if (Rnd.Chance(0.3f)) Motes(p.X, p.Y, 2, 0xFDE999, 0xD2AC52, 1, 7, 30, 0.6f); }
            p.CombatT += dt; p.ComboT += dt;
            if (p.Blocking) p.BlockT += dt;
            if (p.NextCrit) { p.NextCritT -= dt; if (p.NextCritT <= 0) p.NextCrit = false; }
            if (p.StealthT > 0) { p.StealthT -= dt; if (p.StealthT <= 0) { p.StealthT = 0; Smoke(p.X, p.Y, 8, 4, 0x444E70, 3f); } }
            if (p.DodgeCharges < p.Def.DodgeCharges)
            {
                p.DodgeRecharge -= dt;
                if (p.DodgeRecharge <= 0) { p.DodgeCharges++; p.DodgeRecharge = p.Def.DodgeCharges > 1 ? 1.5f : 1.1f; }
            }
            // resources
            if (p.Class == ClassId.Warrior)
            {
                if (p.CombatT > 4f && p.Res > 0) p.Res = Math.Max(0, p.Res - 6f * dt);
            }
            else p.Res = Math.Min(p.MaxRes, p.Res + p.ResRegen * dt);
            if (p.HP < p.MaxHP) p.HP = Math.Min(p.MaxHP, p.HP + p.HpRegen * dt * (p.CombatT > 6f ? 5f : 1f));
            if (p.HealT > 0)
            {
                p.HealT -= dt; Heal(p.HealPerSec * dt, false);
                if (Rnd.Chance(0.35f)) Motes(p.X, p.Y, 2, 0xA8E04A, 0x3F6A24, 1, 6, 30, 0.5f);
            }

            bool control = !p.Incapacitated && !Fading && inputLockT <= 0 && Ov == Overlay.None && !chatOpen;
            bool fp = FirstPerson;
            if (fp)
            {
                // through the hero's eyes the view is the aim; a foe under the crosshair is aimed at dead centre
                FpLook(dt, control);
                PickFp();
                if (control)
                {
                    p.AimAngle = LookYaw;
                    if (AimFoe != null && M.Dist(AimFoe.X, AimFoe.Y, p.X, p.Y) > 6) p.AimAngle = M.Atan2(AimFoe.Y - p.Y, AimFoe.X - p.X);
                }
            }
            else
            {
                // aim follows the cursor
                float aimX, aimY; MouseAim(out aimX, out aimY);
                float ax = aimX - p.X, ay = aimY - p.Y;
                if (control && ax * ax + ay * ay > 9) p.AimAngle = M.Atan2(ay, ax);
            }
            if (control && p.DodgeT <= 0 && p.DashT <= 0) p.Facing = M.Cos(p.AimAngle) >= 0 ? 1 : -1;

            float mx = 0, my = 0;
            if (control && fp)
            {
                // forward is where the hero looks; A and D step sideways, the arrow keys turn instead
                float f = 0, sd = 0;
                if (In.Down[Key.W] || In.Down[Key.Up]) f += 1;
                if (In.Down[Key.S] || In.Down[Key.Down]) f -= 1;
                if (In.Down[Key.D]) sd += 1;
                if (In.Down[Key.A]) sd -= 1;
                float c = M.Cos(LookYaw), s = M.Sin(LookYaw);
                mx = c * f - s * sd; my = s * f + c * sd;
                float l = M.Len(mx, my);
                if (l > 0) { mx /= l; my /= l; }
            }
            else if (control)
            {
                if (In.Down[Key.A] || In.Down[Key.Left]) mx -= 1;
                if (In.Down[Key.D] || In.Down[Key.Right]) mx += 1;
                if (In.Down[Key.W] || In.Down[Key.Up]) my -= 1;
                if (In.Down[Key.S] || In.Down[Key.Down]) my += 1;
                float l = M.Len(mx, my);
                if (l > 0) { mx /= l; my /= l; }
            }

            p.Moving = false;
            if (p.DodgeT > 0)
            {
                p.DodgeT -= dt;
                Cur.Move(p, p.DodgeDX * dt, p.DodgeDY * dt, 1);
                p.Moving = true;
                if (((int)(p.DodgeT * 60) & 3) == 0) AddGhost(0xC8D0E0);
            }
            else if (p.DashT > 0) UpdateDash(dt);
            else if (control)
            {
                float speed = p.MoveSpeed;
                if (p.Blocking) speed *= 0.45f;
                if (p.SlowT > 0) speed *= 1f - p.SlowAmt;
                if (p.StealthT > 0) speed *= 1.3f;
                if (p.AtkT > 0) speed *= p.Class == ClassId.Warrior ? 0.5f : 0.8f;
                if (p.WhirlT > 0) speed *= 0.75f;
                if (p.CastT > 0) speed *= 0.72f;
                if (p.BowT > 0) speed *= 0.6f;
                if (p.RootT > 0) speed = 0;
                if ((mx != 0 || my != 0) && speed > 0)
                {
                    float bx = p.X, by = p.Y;
                    Cur.Move(p, mx * speed * dt, my * speed * Surface.YMove * dt, 1);
                    float moved = M.Dist(bx, by, p.X, p.Y);
                    p.Moving = moved > 0.05f;
                    p.StepAcc += moved;
                    if (p.StepAcc > 21f) { p.StepAcc = 0; Footstep(); }
                }
            }
            ApplyKnock(p, dt, 1);
            if (control && p.DodgeT <= 0 && p.DashT <= 0) HandleActions(mx, my);
            else if (p.Class == ClassId.Warrior) p.Blocking = false;
            UpdateAttack(dt);
            UpdateWhirl(dt);
            p.AnimT += dt;
        }

        void Footstep()
        {
            if (P.StealthT > 0) return;
            byte t = Cur.TerrainAt(P.X, P.Y);
            int s = Sfx.StepGrass; float v = 0.3f;
            switch (t)
            {
                case T.Dirt: case T.Ash: s = Sfx.StepDirt; if (Rnd.Chance(0.5f)) Dust(P.X, P.Y, 1, 0x6B5140); break;
                case T.Cobble: case T.Stone: case T.DFloor: case T.DRitual: s = Sfx.StepStone; v = 0.34f; break;
                case T.Plank: case T.PlankV: s = Sfx.StepWood; v = 0.4f; break;
                case T.Mud: case T.Water: s = Sfx.StepMud; v = 0.36f; break;
                case T.DCarpet: v = 0.18f; break;
            }
            Snd(s, v, 0.9f + Rnd.F() * 0.2f);
        }

        void HandleActions(float mx, float my)
        {
            Player p = P;
            if (In.Pressed[Key.Space]) TryDodge(mx, my);
            if (p.DodgeT > 0) return;
            if (p.Class == ClassId.Warrior)
            {
                bool was = p.Blocking;
                p.Blocking = In.MDown[1] && p.AtkT <= 0 && p.WhirlT <= 0;
                if (p.Blocking && !was) { p.BlockT = 0; Snd(Sfx.Equip, 0.5f, 0.8f); }
            }
            else if (In.MPressed[1]) UseAbility(Classes.SlotRMB);
            if (In.MDown[0] && !p.Blocking) BasicAttack();
            if (In.Pressed[Key.Q]) UseAbility(Classes.SlotQ);
            if (In.Pressed[Key.E]) UseAbility(Classes.SlotE);
            if (In.Pressed[Key.R]) UseAbility(Classes.SlotR);
            if (In.Pressed[Key.N1]) UsePotion(0);
            if (In.Pressed[Key.N2]) UsePotion(1);
        }

        public void TryDodge(float mx, float my)
        {
            Player p = P;
            if (p.DodgeCharges <= 0 || p.DodgeT > 0 || p.RootT > 0 || p.WhirlT > 0) return;
            float dx = mx, dy = my;
            // standing still: overhead the roll goes where the hero aims, through the hero's eyes it is a step back
            if (dx == 0 && dy == 0) { float a = p.AimAngle + (FirstPerson ? M.PI : 0); dx = M.Cos(a); dy = M.Sin(a); }
            if (p.DodgeCharges == p.Def.DodgeCharges) p.DodgeRecharge = p.Def.DodgeCharges > 1 ? 1.5f : 1.1f;
            p.DodgeCharges--;
            p.DodgeT = 0.26f; p.DodgeDX = dx * 222; p.DodgeDY = dy * 222 * Surface.YMove;
            p.IFrames = Math.Max(p.IFrames, 0.25f);
            p.AtkT = 0; p.Blocking = false;
            if (dx != 0) p.Facing = dx > 0 ? 1 : -1;
            Dust(p.X, p.Y, 4, 0x8A8A9A);
            Snd(Sfx.Dodge, 0.7f, 0.95f + Rnd.F() * 0.1f);
        }

        // basic attacks
        void BasicAttack()
        {
            Player p = P;
            if (p.AtkCd > 0 || p.WhirlT > 0) return;
            WeaponType wt = p.WType;
            float interval = p.AtkInterval;
            float ca = M.Cos(p.AimAngle), sa = M.Sin(p.AimAngle);
            switch (p.Class)
            {
                case ClassId.Warrior:
                    {
                        if (p.ComboT > 0.95f) p.Combo = 0;
                        int step = p.Combo; p.SwingStep = step; p.Combo = (step + 1) % 3; p.ComboT = 0;
                        if (step == 2) interval *= 1.25f;
                        p.AtkDur = interval * 0.62f; p.AtkT = p.AtkDur; p.AtkCd = interval; p.SwingHit = false;
                        float half = wt.Arc * 0.5f + (step == 2 ? 0.5f : 0.15f);
                        if (step == 1) { p.SwingFrom = half; p.SwingTo = -half; } else { p.SwingFrom = -half; p.SwingTo = half; }
                        p.KX += ca * 34; p.KY += sa * 34;
                        Snd(step == 2 || wt == Weapons.Types[1] ? Sfx.SwingHeavy : Sfx.Swing, 0.8f, 0.92f + Rnd.F() * 0.16f);
                    }
                    break;
                case ClassId.Rogue:
                    {
                        p.SwingStep = p.Combo; p.Combo ^= 1;
                        p.AtkDur = interval * 0.7f; p.AtkT = p.AtkDur; p.AtkCd = interval; p.SwingHit = false;
                        p.SwingFrom = p.SwingStep == 0 ? -0.5f : 0.5f; p.SwingTo = -p.SwingFrom * 0.4f;
                        p.KX += ca * 22; p.KY += sa * 22;
                        Snd(Sfx.Stab, 0.7f, 0.95f + Rnd.F() * 0.2f);
                    }
                    break;
                case ClassId.Mage:
                    {
                        bool crit; float d = RollDamage(0.85f, out crit);
                        Projectile pr = Shoot(Projectile.KEmber, p.X + ca * 9, p.Y + sa * 6, p.AimAngle, 265, d, true, 0.9f);
                        pr.Crit = crit;
                        if (p.NextCrit) p.NextCrit = false;
                        p.AtkCd = interval; p.CastT = 0.16f;
                        Snd(Sfx.CastEmber, 0.6f, 0.95f + Rnd.F() * 0.15f);
                        if (p.StealthT > 0) BreakStealth();
                    }
                    break;
                case ClassId.Ranger:
                    {
                        bool crit; float d = RollDamage(1f, out crit);
                        Projectile pr = Shoot(Projectile.KArrow, p.X + ca * 9, p.Y + sa * 6, p.AimAngle, 350, d, true, 0.8f);
                        pr.Crit = crit; pr.Knock = 50;
                        p.AtkCd = interval; p.BowT = Math.Min(0.2f, interval * 0.5f);
                        Snd(Sfx.Bow, 0.7f, 0.95f + Rnd.F() * 0.12f);
                    }
                    break;
            }
        }

        void UpdateAttack(float dt)
        {
            Player p = P;
            if (p.AtkT <= 0) return;
            p.AtkT -= dt;
            float prog = 1f - Math.Max(0, p.AtkT) / p.AtkDur;
            if (!p.SwingHit && prog >= 0.35f) { p.SwingHit = true; MeleeHit(); }
        }

        void MeleeHit()
        {
            Player p = P;
            WeaponType wt = p.WType;
            float reach = wt.Reach, arc = wt.Arc, mult = 1f, knock = 70;
            if (p.Class == ClassId.Warrior && p.SwingStep == 2) { mult = 1.5f; arc += 0.8f; reach += 4; knock = 150; }
            int hits = 0;
            for (int k = 0; k < Foes.Count; k++)
            {
                Enemy e = Foes[k];
                if (e.Dead || e.State == Enemy.Vanish) continue;
                float dx = e.X - p.X, dy = (e.Y - p.Y) * Surface.Squash;
                float dist = M.Len(dx, dy);
                if (dist > reach + e.Radius) continue;
                if (dist > 9 && Math.Abs(M.AngDiff(p.AimAngle, M.Atan2(dy, dx))) > arc * 0.5f + 0.15f) continue;
                bool crit; float d = RollDamage(mult, out crit);
                float nx = dist > 0.5f ? dx / dist : M.Cos(p.AimAngle), ny = dist > 0.5f ? dy / dist : M.Sin(p.AimAngle);
                if (HitEnemy(e, d, crit, nx, ny, knock, p.Class == ClassId.Warrior && wt == Weapons.Types[1] ? FxBlunt : FxSlash, true)) hits++;
            }
            if (hits > 0)
            {
                p.NextCrit = false;
                if (p.Class == ClassId.Warrior) p.Res = Math.Min(p.MaxRes, p.Res + Math.Min(hits, 2) * 8);
            }
            float cx = p.X + M.Cos(p.AimAngle) * reach * 0.6f, cy = p.Y + M.Sin(p.AimAngle) * reach * 0.6f;
            HitPropsAt(cx, cy, reach * 0.62f, p.Class == ClassId.Warrior ? 2 : 1);
        }

        // abilities
        public bool AbilityUnlocked(int slot) { return P.Level >= P.Def.Abilities[slot].UnlockLevel; }

        public void UseAbility(int slot)
        {
            Player p = P;
            AbilityDef ab = p.Def.Abilities[slot];
            if (p.Level < ab.UnlockLevel) { Err(Loc.F("Unlocks at level {0}", ab.UnlockLevel)); return; }
            if (p.Cd[slot] > 0) { if (errT <= 0) { errT = 0.3f; Snd(Sfx.UiError, 0.35f, 1.2f); } return; }
            if (p.Res < ab.Cost) { Err(Loc.F("Not enough {0}", Loc.Tr(p.Def.ResName))); return; }
            bool ok = false;
            switch (p.Class)
            {
                case ClassId.Warrior: ok = WarriorAbility(slot); break;
                case ClassId.Mage: ok = MageAbility(slot); break;
                case ClassId.Rogue: ok = RogueAbility(slot); break;
                case ClassId.Ranger: ok = RangerAbility(slot); break;
            }
            if (!ok) return;
            p.Res -= ab.Cost;
            p.Cd[slot] = ab.Cooldown * p.CdMult;
            p.Blocking = false;
        }

        Enemy EnemyNear(float x, float y, float maxDist, List<Actor> exclude)
        {
            Enemy best = null; float bd = maxDist * maxDist;
            for (int k = 0; k < Foes.Count; k++)
            {
                Enemy e = Foes[k];
                if (e.Dead || e.State == Enemy.Vanish) continue;
                if (exclude != null && exclude.Contains(e)) continue;
                float d = M.Dist2(e.X, e.Y - 6, x, y);
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        bool WarriorAbility(int slot)
        {
            Player p = P;
            float ca = M.Cos(p.AimAngle), sa = M.Sin(p.AimAngle);
            switch (slot)
            {
                case Classes.SlotQ:     // Shield Rush
                    p.DashKind = 1; p.DashDur = 0.27f; p.DashT = p.DashDur; p.DashDX = ca * 340; p.DashDY = sa * 340 * Surface.YMove; p.DashHit.Clear();
                    p.IFrames = Math.Max(p.IFrames, 0.12f); p.AtkT = 0;
                    Snd(Sfx.Charge); Dust(p.X, p.Y, 5, 0x8A8A9A);
                    return true;
                case Classes.SlotE:     // Whirlwind
                    p.WhirlT = 0.5f; p.WhirlHits = 0; p.AtkT = 0;
                    Snd(Sfx.Whirl);
                    return true;
                case Classes.SlotR:     // Vesper Oath
                    p.OathT = 6f; p.HealT = 6f; p.HealPerSec = p.MaxHP * 0.3f / 6f;
                    Snd(Sfx.Oath);
                    RingBurst(p.X, p.Y, 0xF7BE52, 22, 120, 0.6f);
                    Motes(p.X, p.Y, 0, 0xFDE999, 0xD2AC52, 26, 12, 60, 1f);
                    Flash(0xFFE0A0, 0.18f);
                    return true;
            }
            return false;
        }

        bool MageAbility(int slot)
        {
            Player p = P;
            float ca = M.Cos(p.AimAngle), sa = M.Sin(p.AimAngle);
            switch (slot)
            {
                case Classes.SlotRMB:   // Blink
                    {
                        float dx = MouseWX - p.X, dy = MouseWY - p.Y, len = M.Len(dx, dy);
                        if (len < 4) { dx = ca; dy = sa; len = 1; }
                        dx /= len; dy /= len;
                        float want = Math.Min(86, Math.Max(30, len));
                        float bestD = 0;
                        for (float d = 4; d <= want; d += 4)
                        {
                            float x = p.X + dx * d, y = p.Y + dy * d;
                            if (Cur.PointBlocked(x, y, 2)) break;
                            if (!Cur.Blocked(x, y, p.Radius, 1)) bestD = d;
                        }
                        if (bestD < 12) { Err("No room"); return false; }
                        Motes(p.X, p.Y, 4, 0xC9D3F5, 0x584688, 14, 6, 50, 0.5f);
                        AddGhost(0xA7A0DC);
                        p.X += dx * bestD; p.Y += dy * bestD;
                        p.KX = p.KY = 0; p.IFrames = Math.Max(p.IFrames, 0.28f); p.RootT = 0; p.SlowT = 0;
                        Motes(p.X, p.Y, 4, 0xEAF3FF, 0x7F6CB6, 16, 7, 60, 0.5f);
                        RingBurst(p.X, p.Y, 0xA7A0DC, 12, 70, 0.3f);
                        Snd(Sfx.Blink);
                        return true;
                    }
                case Classes.SlotQ:     // Fireball
                    {
                        bool crit; float d = RollDamage(2.2f, out crit);
                        Projectile pr = Shoot(Projectile.KFireball, p.X + ca * 10, p.Y + sa * 7, p.AimAngle, 195, d, true, 1.25f);
                        pr.Crit = crit; p.CastT = 0.25f; p.KX -= ca * 30; p.KY -= sa * 30;
                        Snd(Sfx.Fireball);
                        BreakStealth();
                        return true;
                    }
                case Classes.SlotE:     // Frost Nova
                    {
                        const float R = 64;
                        for (int k = 0; k < Foes.Count; k++)
                        {
                            Enemy e = Foes[k];
                            if (e.Dead || e.State == Enemy.Vanish) continue;
                            float dist = M.Dist(e.X, e.Y, p.X, p.Y);
                            if (dist > R + e.Radius) continue;
                            bool crit; float d = RollDamage(1.3f, out crit);
                            float nx = dist > 0.5f ? (e.X - p.X) / dist : 0, ny = dist > 0.5f ? (e.Y - p.Y) / dist : -1;
                            HitEnemy(e, d, crit, nx, ny, 60, FxFrost, false);
                            SetStatus(e, StFreeze, e.Def.Boss ? 0.4f : (e.Def.Elite ? 0.8f : 1.6f), 0);
                            SetStatus(e, StSlow, 4.5f, 0.5f);
                        }
                        Area a = new Area(); a.Kind = Area.Ring; a.X = p.X; a.Y = p.Y; a.R = R; a.Dur = 0.45f; a.Color = 0x9BDCF4; AddArea(a);
                        RingBurst(p.X, p.Y, 0x9BDCF4, 28, 170, 0.45f);
                        Motes(p.X, p.Y, 2, 0xE2F8FF, 0x2F6A9A, 20, R * 0.6f, 20, 0.8f);
                        Snd(Sfx.Frost); Shake(2f, 0.15f);
                        p.CastT = 0.25f;
                        return true;
                    }
                case Classes.SlotR:     // Stormcall
                    {
                        Enemy t = EnemyNear(MouseWX, MouseWY, 52, null);
                        if (t == null) t = EnemyNear(p.X, p.Y, 150, null);
                        if (t == null || M.Dist(t.X, t.Y, p.X, p.Y) > 230) { Err("No target"); return false; }
                        List<Actor> hit = new List<Actor>();
                        float fx = p.X + ca * 8, fy = p.Y - 16;
                        float mult = 2.6f;
                        for (int n = 0; n < 5 && t != null; n++)
                        {
                            hit.Add(t);
                            Area a = new Area(); a.Kind = Area.Bolt; a.X = fx; a.Y = fy; a.X2 = t.X; a.Y2 = t.Y - 10; a.Dur = 0.22f; a.Color = 0xE2F8FF; a.Seed = Rnd.Int(9999); AddArea(a);
                            bool crit; float d = RollDamage(mult, out crit);
                            float dist = M.Dist(fx, fy, t.X, t.Y);
                            HitEnemy(t, d, crit, dist > 1 ? (t.X - fx) / dist : 0, dist > 1 ? (t.Y - fy) / dist : 0, 30, FxShock, false);
                            SetStatus(t, StStun, t.Def.Boss ? 0.15f : 0.45f, 0);
                            fx = t.X; fy = t.Y - 10; mult *= 0.7f;
                            t = EnemyNear(fx, fy, 84, hit);
                        }
                        Snd(Sfx.Lightning); Shake(3f, 0.2f); Flash(0xE2F8FF, 0.12f);
                        p.CastT = 0.3f;
                        BreakStealth();
                        return true;
                    }
            }
            return false;
        }

        bool RogueAbility(int slot)
        {
            Player p = P;
            float ca = M.Cos(p.AimAngle), sa = M.Sin(p.AimAngle);
            switch (slot)
            {
                case Classes.SlotRMB:   // Thrown Knife
                    {
                        bool crit; float d = RollDamage(1.5f, out crit);
                        Projectile pr = Shoot(Projectile.KKnife, p.X + ca * 8, p.Y + sa * 6, p.AimAngle, 310, d, true, 0.7f);
                        pr.Crit = crit; p.CastT = 0.12f;
                        if (p.NextCrit) p.NextCrit = false;
                        Snd(Sfx.Knife, 0.7f, 1f + Rnd.F() * 0.1f);
                        return true;
                    }
                case Classes.SlotQ:     // Shadowstep
                    {
                        float dx = MouseWX - p.X, dy = MouseWY - p.Y, len = M.Len(dx, dy);
                        if (len < 4) { dx = ca; dy = sa; len = 1; }
                        dx /= len; dy /= len;
                        float dist = M.Clamp(len, 40, 78);
                        p.DashKind = 2; p.DashDur = 0.15f; p.DashT = p.DashDur; p.DashDX = dx * dist / p.DashDur; p.DashDY = dy * dist / p.DashDur; p.DashHit.Clear();
                        p.IFrames = Math.Max(p.IFrames, 0.3f); p.AtkT = 0;
                        Smoke(p.X, p.Y, 8, 3, 0x3A2E5E, 3f);
                        Snd(Sfx.Dodge, 0.9f, 1.3f);
                        return true;
                    }
                case Classes.SlotE:     // Veil
                    {
                        p.StealthT = 4f;
                        Smoke(p.X, p.Y, 8, 10, 0x444E70, 4f);
                        Snd(Sfx.Stealth);
                        // alone, every hunter loses the scent at once; in a party they turn on whoever they can still see
                        for (int k = 0; k < Foes.Count && !Online; k++)
                        {
                            Enemy e = Foes[k];
                            if (e.Dead || e.Def.Boss || !e.Aggro) continue;
                            e.Aggro = false;
                            if (e.State != Enemy.Vanish) e.SetState(Enemy.Search, 2.2f);
                        }
                        return true;
                    }
                case Classes.SlotR:     // Fan of Knives
                    {
                        const int N = 12;
                        bool stealth = p.StealthT > 0;
                        for (int i = 0; i < N; i++)
                        {
                            float a = p.AimAngle + i * M.TAU / N;
                            bool crit; float d = RollDamage(0.8f, out crit);
                            Projectile pr = Shoot(Projectile.KPoisonKnife, p.X + M.Cos(a) * 6, p.Y + M.Sin(a) * 4, a, 270, d, true, 0.55f);
                            pr.Crit = crit;
                        }
                        if (stealth) BreakStealth();
                        p.NextCrit = false;
                        RingBurst(p.X, p.Y, 0xA8E04A, 12, 90, 0.3f);
                        Snd(Sfx.Knife, 1f, 0.8f); Snd(Sfx.Whirl, 0.6f, 1.4f);
                        p.CastT = 0.2f;
                        return true;
                    }
            }
            return false;
        }

        bool RangerAbility(int slot)
        {
            Player p = P;
            float ca = M.Cos(p.AimAngle), sa = M.Sin(p.AimAngle);
            switch (slot)
            {
                case Classes.SlotRMB:   // Piercing Shot
                    {
                        bool crit; float d = RollDamage(1.7f, out crit);
                        Projectile pr = Shoot(Projectile.KPierce, p.X + ca * 9, p.Y + sa * 6, p.AimAngle, 440, d, true, 0.7f);
                        pr.Crit = crit; pr.Knock = 110; p.BowT = 0.25f; p.KX -= ca * 40; p.KY -= sa * 40;
                        Snd(Sfx.Bow, 1f, 0.75f); Snd(Sfx.SwingHeavy, 0.5f, 1.3f);
                        return true;
                    }
                case Classes.SlotQ:     // Snare
                    {
                        float dx = MouseWX - p.X, dy = MouseWY - p.Y, len = M.Len(dx, dy);
                        float d = Math.Min(len, 80);
                        float x = p.X + (len > 1 ? dx / len * d : 0), y = p.Y + (len > 1 ? dy / len * d : 0);
                        if (Cur.Blocked(x, y, 5, 1)) { x = p.X; y = p.Y; }
                        int traps = 0; Area oldest = null;
                        foreach (Area ar in Areas) if (ar.Kind == Area.Trap && !ar.Ghost) { traps++; if (oldest == null || ar.T > oldest.T) oldest = ar; }
                        if (traps >= 2 && oldest != null) oldest.Done = true;
                        Area a = new Area(); a.Kind = Area.Trap; a.X = x; a.Y = y; a.R = 13; a.Dur = 25f; a.FromPlayer = true; AddArea(a);
                        Dust(x, y, 4, 0x8A8A9A);
                        Snd(Sfx.TrapSet);
                        return true;
                    }
                case Classes.SlotE:     // Arrow Rain
                    {
                        float dx = MouseWX - p.X, dy = MouseWY - p.Y, len = M.Len(dx, dy);
                        float d = Math.Min(len, 190);
                        Area a = new Area(); a.Kind = Area.Rain; a.FromPlayer = true;
                        a.X = p.X + (len > 1 ? dx / len * d : 0); a.Y = p.Y + (len > 1 ? dy / len * d : 0);
                        a.R = 46; a.Delay = 0.5f; a.Dur = 2.4f; a.TickT = 0; a.Color = 0xE8E0C4;
                        AddArea(a);
                        p.BowT = 0.3f;
                        Snd(Sfx.ArrowRain);
                        return true;
                    }
                case Classes.SlotR:     // Hunter's Mark
                    {
                        Enemy t = EnemyNear(MouseWX, MouseWY, 64, null);
                        if (t == null) t = EnemyNear(p.X, p.Y, 180, null);
                        if (t == null || M.Dist(t.X, t.Y, p.X, p.Y) > 280) { Err("No target"); return false; }
                        SetStatus(t, StMark, 8f, 0);
                        bool crit; float d = RollDamage(2.0f, out crit);
                        Projectile pr = Shoot(Projectile.KMarked, p.X + ca * 9, p.Y + sa * 6, M.Atan2(t.Y - p.Y, t.X - p.X), 330, d, true, 1.4f);
                        pr.Crit = crit; pr.Target = t; pr.Knock = 70;
                        p.BowT = 0.25f;
                        Sparks(t.X, t.Y, 14, 0xE0584E, 8, 60, 0.4f);
                        Snd(Sfx.Mark); Snd(Sfx.Bow, 0.8f, 0.9f);
                        return true;
                    }
            }
            return false;
        }

        void UpdateDash(float dt)
        {
            Player p = P;
            p.DashT -= dt;
            int hit = Cur.Move(p, p.DashDX * dt, p.DashDY * dt, 1);
            p.Moving = true;
            AddGhost(p.DashKind == 1 ? 0xE0C8A0 : 0x7F6CB6);
            if (p.DashKind == 1 && Rnd.Chance(0.6f)) Dust(p.X, p.Y, 1, 0x8A8A9A);
            float len = M.Len(p.DashDX, p.DashDY);
            float nx = len > 0 ? p.DashDX / len : 1, ny = len > 0 ? p.DashDY / len : 0;
            for (int k = 0; k < Foes.Count; k++)
            {
                Enemy e = Foes[k];
                if (e.Dead || e.State == Enemy.Vanish || p.DashHit.Contains(e)) continue;
                if (M.Dist2(e.X, e.Y, p.X, p.Y) > (16 + e.Radius) * (16 + e.Radius)) continue;
                p.DashHit.Add(e);
                bool crit;
                if (p.DashKind == 1)
                {
                    float d = RollDamage(1.4f, out crit);
                    HitEnemy(e, d, crit, nx, ny, 170, FxBlunt, true);
                    SetStatus(e, StStun, e.Def.Boss ? 0.3f : (e.Def.Elite ? 0.7f : 1.3f), 0);
                    p.Res = Math.Min(p.MaxRes, p.Res + 14);
                    Shake(2.5f, 0.12f);
                }
                else
                {
                    float d = RollDamage(1.2f, out crit);
                    HitEnemy(e, d, crit, nx, ny, 20, FxSlash, false);
                }
            }
            if (p.DashKind == 1) HitPropsAt(p.X + nx * 8, p.Y + ny * 8, 12, 3);
            if (p.DashT <= 0 || hit == 3)
            {
                p.DashT = 0;
                // the guaranteed critical is granted once the step is finished
                if (p.DashKind == 2) { Smoke(p.X, p.Y, 8, 3, 0x3A2E5E, 3f); p.NextCrit = true; p.NextCritT = 3f; }
            }
        }

        void UpdateWhirl(float dt)
        {
            Player p = P;
            if (p.WhirlT <= 0) return;
            p.WhirlT -= dt;
            float prog = 1f - Math.Max(0, p.WhirlT) / 0.5f;
            if ((prog > 0.18f && p.WhirlHits == 0) || (prog > 0.62f && p.WhirlHits == 1))
            {
                p.WhirlHits++;
                for (int k = 0; k < Foes.Count; k++)
                {
                    Enemy e = Foes[k];
                    if (e.Dead || e.State == Enemy.Vanish) continue;
                    float dx = e.X - p.X, dy = (e.Y - p.Y) * 1.25f, dist = M.Len(dx, dy);
                    if (dist > 40 + e.Radius) continue;
                    bool crit; float d = RollDamage(1.1f, out crit);
                    HitEnemy(e, d, crit, dist > 0.5f ? dx / dist : 0, dist > 0.5f ? dy / dist : -1, 85, FxSlash, true);
                }
                HitPropsAt(p.X, p.Y, 38, 2);
                RingBurst(p.X, p.Y, 0xE8E0C4, 14, 150, 0.22f);
                Shake(1.5f, 0.1f);
            }
        }

        public void UsePotion(int kind)
        {
            Player p = P;
            if (p.Dead) return;
            if (p.PotionCd > 0) { Err(null); return; }
            if (kind == 0)
            {
                if (p.HealthPotions <= 0) { Err("No draughts"); return; }
                if (p.HP >= p.MaxHP - 0.5f) { Err("Already hale"); return; }
                p.HealthPotions--;
                p.HealT = 1.5f; p.HealPerSec = p.MaxHP * 0.45f / 1.5f;
                Float(p.X, p.Y - 28, "+" + (int)(p.MaxHP * 0.45f), 0x8FCB6B, false);
            }
            else
            {
                if (p.SpiritPotions <= 0) { Err("No draughts"); return; }
                if (p.Res >= p.MaxRes - 0.5f) { Err("Already full"); return; }
                p.SpiritPotions--;
                p.Res = Math.Min(p.MaxRes, p.Res + (p.Class == ClassId.Warrior ? 50 : p.MaxRes * 0.6f));
                Motes(p.X, p.Y, 2, p.Def.ResColor, p.Def.ResColorDark, 12, 7, 40, 0.7f);
            }
            p.PotionCd = 3f;
            Snd(Sfx.Potion);
        }
    }
}
