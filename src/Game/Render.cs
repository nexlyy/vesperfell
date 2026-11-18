using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>World rendering: ground, depth-sorted sprites, telegraphs, lights, and everything drawn after the lighting pass.</summary>
    public sealed partial class Game
    {
        // loot labels placed this frame (nearest first)
        readonly Pickup[] labD = new Pickup[24];
        readonly int[] labX = new int[24], labY = new int[24], labW = new int[24];
        readonly float[] labDist = new float[24];

        struct DrawItem { public float Y; public byte Kind; public Prop Pr; public Enemy En; public Npc Np; public Pickup Pk; public Projectile Sh; public int Gi; }
        readonly List<DrawItem> drawList = new List<DrawItem>(1400);
        readonly List<Prop> viewProps = new List<Prop>(1400);
        static readonly Comparison<DrawItem> byY = delegate(DrawItem a, DrawItem b) { return a.Y.CompareTo(b.Y); };

        /// <summary>True when this frame's world went to the OpenGL target (the interface is then a layer over it).</summary>
        public bool WorldDrawn3D;

        public void Render()
        {
            Final.Rec = R3 != null && Final.S > 1 && !SoftUi ? R3.UiRecorder : null;
            if (Final.Rec != null) Final.Rec.Begin(Final.PW, Final.PH);
            Final.ResetClip(); Scene.ResetClip(); Scene.EmisMask = 0; Final.EmisMask = 0;
            WorldDrawn3D = false; Ms3D = 0;
            Final.Layer = R3 != null && Scr != Screen.Loading;
            switch (Scr)
            {
                case Screen.Loading: DrawLoading(); break;
                case Screen.Title: World(false); DrawTitle(); break;
                case Screen.ClassSelect: World(false); DrawClassSelect(); break;
                case Screen.Play: World(true); Prof.Mark(); DrawScreenFx(); Prof.Add(2); DrawHud(); Prof.Add(3); DrawOverlay(); Prof.Add(4); break;
            }
            Prof.Mark();
            if (FlashT > 0) Final.FillA(0, 0, W, H, FlashColor, (int)(150 * M.Clamp01(FlashT / Math.Max(0.01f, FlashDur))));
            if (FadeT > 0) Final.FillA(0, 0, W, H, 0x000000, (int)(255 * M.Clamp01(FadeT / FadeDur)));
            DrawCursor();
            if (Cfg.ShowFps) Final.Text(Fonts.Small, ((int)Math.Round(Fps)) + " fps", W - 44, H - 12, 0x8A8A9A, 0x000000);
            Prof.Add(5);
        }

        // world
        /// <summary>The world under the interface: drawn by OpenGL when it is available, otherwise in software.</summary>
        void World(bool play)
        {
            if (R3 == null) { RenderWorld(play); return; }
            Prof.Mark();
            Final.ClearTransparent();
            Prof.Add(0);
            WorldDrawn3D = true;
            if (Cur == null) { R3.Blank(); return; }
            R3.TitleMode = !play;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            R3.Frame(play);
            Ms3D = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Prof.Mark();
            if (play) DrawWorldUi(CamXi, CamYi);
            Prof.Add(1);
        }

        /// <summary>Where a point (map x, map y, height above its ground) shows on the interface, in either view.</summary>
        public bool UiAt(float x, float y, float h, out int sx, out int sy)
        {
            if (R3 == null) { sx = (int)x - CamXi; sy = (int)(y - h) - CamYi; return true; }
            float ux, uy;
            bool ok = R3.ToUi(x, y, h, out ux, out uy);
            sx = (int)Math.Round(ux); sy = (int)Math.Round(uy);
            return ok;
        }

        /// <summary>How tall an enemy stands, for the bar and the marks over its head.</summary>
        float FoeTop(Enemy e)
        {
            float h = e.Spr != null ? e.Spr.Height : 24;
            if (R3 == null) return h + e.Z;
            switch (e.Kind)
            {
                case EnemyKind.Wolf: return 21;
                case EnemyKind.AlphaWolf: return 27;
                case EnemyKind.Crawler: return 15;
                case EnemyKind.Wisp: return 25 + e.Z;
            }
            return 34f * (1f + (h / 24f - 1f) * 0.8f);
        }

        float NpcTop(Npc n) { return R3 == null ? n.Spr.Height : (n.Spr == Assets.Child ? 26 : 35); }

        void RenderWorld(bool play)
        {
            Map m = Cur;
            if (m == null) { Final.Clear(0x05050A); return; }
            int cx = CamXi, cy = CamYi;
            Scene.CopyFrom(m.Ground, m.PW, m.PH, cx, cy);
            DrawWater(m, cx, cy);

            viewProps.Clear();
            m.Query(cx - 150, cy - 70, cx + W + 150, cy + H + 230, viewProps);
            for (int i = 0; i < viewProps.Count; i++)
            {
                Prop p = viewProps[i];
                if (p.Flat && !p.Hidden && p.Spr != null) Scene.Blit(p.Spr, (int)p.X - cx, (int)p.Y - cy, p.Flip);
            }

            if (play)
            {
                DrawShadows(cx, cy);
                Scene.EmisMask = 0xFF000000;
                DrawGroundFx(cx, cy);
                Scene.EmisMask = 0;
            }

            drawList.Clear();
            for (int i = 0; i < viewProps.Count; i++)
            {
                Prop p = viewProps[i];
                if (p.Flat || p.Hidden || p.Spr == null) continue;
                DrawItem d = new DrawItem(); d.Y = p.Y; d.Kind = 0; d.Pr = p; drawList.Add(d);
            }
            if (play)
            {
                for (int i = 0; i < Foes.Count; i++)
                {
                    Enemy e = Foes[i];
                    if (e.X < cx - 60 || e.X > cx + W + 60 || e.Y < cy - 20 || e.Y > cy + H + 70) continue;
                    DrawItem d = new DrawItem(); d.Y = e.Y; d.Kind = 1; d.En = e; drawList.Add(d);
                }
                for (int i = 0; i < Npcs.Count; i++) { DrawItem d = new DrawItem(); d.Y = Npcs[i].Y; d.Kind = 2; d.Np = Npcs[i]; drawList.Add(d); }
                if (P != null) { DrawItem d = new DrawItem(); d.Y = P.Y + 0.1f; d.Kind = 3; drawList.Add(d); }
                for (int i = 0; i < Others.Count; i++) { DrawItem d = new DrawItem(); d.Y = Others[i].Y + 0.05f; d.Kind = 7; d.Gi = i; drawList.Add(d); }
                for (int i = 0; i < Drops.Count; i++) { DrawItem d = new DrawItem(); d.Y = Drops[i].Y; d.Kind = 4; d.Pk = Drops[i]; drawList.Add(d); }
                for (int i = 0; i < Shots.Count; i++) { DrawItem d = new DrawItem(); d.Y = Shots[i].Y + 2; d.Kind = 5; d.Sh = Shots[i]; drawList.Add(d); }
                for (int i = 0; i < Ghosts.Count; i++) { DrawItem d = new DrawItem(); d.Y = Ghosts[i].Y - 0.2f; d.Kind = 6; d.Gi = i; drawList.Add(d); }
            }
            drawList.Sort(byY);
            for (int i = 0; i < drawList.Count; i++)
            {
                DrawItem d = drawList[i];
                switch (d.Kind)
                {
                    case 0: DrawProp(d.Pr, cx, cy, play); break;
                    case 1: DrawEnemy(d.En, cx, cy); break;
                    case 2: Scene.Blit(d.Np.Frame(), (int)d.Np.X - cx, (int)d.Np.Y - cy, d.Np.Facing < 0); break;
                    case 3: DrawPlayer(cx, cy); break;
                    case 7:
                        if (d.Gi < Others.Count) { P = Others[d.Gi]; DrawPlayer(cx, cy); P = Me; }
                        break;
                    case 4: DrawPickup(d.Pk, cx, cy); break;
                    case 5: DrawShot(d.Sh, cx, cy); break;
                    case 6:
                        {
                            Ghost g = Ghosts[d.Gi];
                            Scene.BlitEx(g.S, (int)g.X - cx, (int)g.Y - cy, g.Flip, g.Color, 256, (int)(130 * g.Life / g.Max));
                        }
                        break;
                }
            }
            DrawParticles(Scene, cx, cy, true);

            // lighting
            Light.AmbR = AmbR; Light.AmbG = AmbG; Light.AmbB = AmbB; Light.FogDensity = FogD; Light.FogColor = FogCol; Light.Time = UiT;
            Light.Begin();
            bool day = Dawn && !Zone.Indoor && play;
            float propScale = day ? 0.3f : 1f;
            for (int i = 0; i < viewProps.Count; i++)
            {
                Prop p = viewProps[i];
                if (p.LightR <= 0) continue;
                float fl = 1f;
                if (p.Flicker) fl = 0.93f + 0.05f * M.Sin(UiT * 9f + p.X * 0.31f) + 0.04f * M.Sin(UiT * 23f + p.Y * 0.17f);
                Light.Light(p.X - cx, p.Y - cy + p.LightOY, p.LightR * fl, p.LightColor, p.LightI * fl * propScale);
            }
            if (play && P != null)
            {
                Player p = P;
                float li = (Zone.Indoor ? 0.62f : 0.4f) * (p.StealthT > 0 ? 0.45f : 1f) * (day ? 0.2f : 1f);
                if (!p.Dead) Light.Light(p.X - cx, p.Y - cy - 10, Zone.Indoor ? 112 : 96, 0xFFD9B0, li);
                if (p.CastT > 0 && p.Class == ClassId.Mage) Light.Light(p.X - cx, p.Y - cy - 10, 60, 0xFFB060, 0.5f);
                if (p.OathT > 0) Light.Light(p.X - cx, p.Y - cy - 10, 74, 0xFFD070, 0.55f);
                if (LevelUpT < 1f) Light.Light(p.X - cx, p.Y - cy - 10, 150, 0xFFE8A0, 1.3f * (1f - LevelUpT));
                for (int i = 0; i < Others.Count; i++)
                {
                    Player o = Others[i];
                    if (o.Dead) continue;
                    Light.Light(o.X - cx, o.Y - cy - 10, Zone.Indoor ? 112 : 96, 0xFFD9B0, (Zone.Indoor ? 0.62f : 0.4f) * (o.StealthT > 0 ? 0.45f : 1f) * (day ? 0.2f : 1f));
                    if (o.OathT > 0) Light.Light(o.X - cx, o.Y - cy - 10, 74, 0xFFD070, 0.55f);
                }
                for (int i = 0; i < Shots.Count; i++)
                {
                    Projectile s = Shots[i];
                    if (s.LightR > 0) Light.Light(s.X - cx, s.Y - cy - s.Z, s.LightR, s.LightColor, 0.9f);
                }
                for (int i = 0; i < Areas.Count; i++)
                {
                    Area a = Areas[i];
                    float t = a.Dur > 0 ? M.Clamp01(a.T / a.Dur) : 0;
                    switch (a.Kind)
                    {
                        case Area.Sigil: Light.Light(a.X - cx, a.Y - cy, a.R * 2.4f, 0xA890FF, a.Armed ? 1.3f : 0.3f + 0.5f * M.Clamp01(a.T / a.Delay)); break;
                        case Area.Burst: Light.Light(a.X - cx, a.Y - cy, a.R * 3f, 0xFFA850, 1.6f * (1 - t)); break;
                        case Area.Bolt: Light.Light(a.X2 - cx, a.Y2 - cy, 90, 0xC8E8FF, 1.4f * (1 - t)); break;
                        case Area.Ring: Light.Light(a.X - cx, a.Y - cy, a.R * 2.2f, a.Color, 0.9f * (1 - t)); break;
                        case Area.Pool: Light.Light(a.X - cx, a.Y - cy, a.R * 2f, 0x80FF60, 0.35f); break;
                    }
                }
                for (int i = 0; i < Foes.Count; i++)
                {
                    Enemy e = Foes[i];
                    if (e.Dead) continue;
                    if (e.Kind == EnemyKind.Wisp) Light.Light(e.X - cx, e.Y - cy - 14, 58, 0xB0A8FF, 0.75f * e.FadeIn);
                    else if (e.Kind == EnemyKind.Prior) Light.Light(e.X - cx, e.Y - cy - 20, 84, 0x9080E0, 0.75f);
                    else if (e.Kind == EnemyKind.Knight) Light.Light(e.X - cx, e.Y - cy - 16, 46, 0x80C0FF, 0.4f);
                    else if (e.Kind == EnemyKind.Acolyte && e.State == Enemy.Windup) Light.Light(e.X - cx, e.Y - cy - 12, 54, 0xA890FF, 0.7f * e.StateT / Math.Max(0.01f, e.StateDur));
                    if (e.BurnT > 0) Light.Light(e.X - cx, e.Y - cy - 8, 38, 0xFFA050, 0.5f);
                }
                for (int i = 0; i < Drops.Count; i++)
                {
                    Pickup d = Drops[i];
                    if (d.Kind == Pickup.Equip && d.It != null && d.It.Rar >= Rarity.Fine) Light.Light(d.X - cx, d.Y - cy - 6, 26 + (int)d.It.Rar * 8, Items.RarityColor[(int)d.It.Rar], 0.55f);
                    else if (d.Kind == Pickup.Quest) Light.Light(d.X - cx, d.Y - cy - 6, 46, 0xC8D8FF, 0.9f);
                }
            }
            if (Final.S == 1) Light.Compose(Scene, Final, cx, cy);
            else { Light.Compose(Scene, lit1, cx, cy); Final.UpscaleFrom(lit1); }

            // unlit layer
            DrawParticles(Final, cx, cy, false);
            if (play)
            {
                DrawSlashes(cx, cy);
                DrawAreasPost(cx, cy);
                DrawWorldUi(cx, cy);
            }
        }

        void DrawWater(Map m, int cx, int cy)
        {
            int c0 = cx / 8, c1 = (cx + W) / 8 + 1, r0 = cy / 8, r1 = (cy + H) / 8 + 1;
            int tick = (int)(UiT * 3.2f);
            for (int r = r0; r <= r1; r++)
            {
                if (r < 0 || r >= m.CH) continue;
                for (int c = c0; c <= c1; c++)
                {
                    if (c < 0 || c >= m.CW) continue;
                    if (m.Cell[r * m.CW + c] != T.Water) continue;
                    uint h = Noise.Hash(c, r, 5);
                    if (((h + (uint)tick) & 7) != 0) continue;
                    int x = c * 8 - cx + (int)((h >> 4) & 3), y = r * 8 - cy + (int)((h >> 8) & 7);
                    if ((m.Solid[r * m.CW + c] & 1) == 0) continue;     // only on open water, not painted shore
                    int len = 2 + (int)((h >> 12) & 3);
                    Scene.FillA(x, y, len, 1, 0x4A8490, 150);
                }
            }
        }

        void DrawProp(Prop p, int cx, int cy, bool play)
        {
            Sprite s = p.Spr;
            if (p.Anim != null) s = p.Anim[((int)(UiT * 7.5f) + p.AnimOff) % p.Anim.Length];
            int x = (int)p.X - cx, y = (int)p.Y - cy;
            if (p.ShakeT > 0) x += (int)Math.Round(M.Sin(p.ShakeT * 70f) * 2f);
            if (p.Sway)
            {
                float sway = M.Sin(UiT * 1.7f + p.X * 0.06f + p.Y * 0.04f) * 1.7f;
                if (play && P != null && Math.Abs(P.X - p.X) < 9 && Math.Abs(P.Y - p.Y) < 5) sway = (P.X < p.X ? 2.5f : -2.5f);   // brushed aside
                Scene.BlitSway(s, x, y, p.Flip, sway);
                return;
            }
            if (p.Tall && play && P != null)
            {
                if (P.Y < p.Y - 2 && P.Y > p.Y - s.H + 8 && Math.Abs(P.X - p.X) < s.W * 0.5f - 2) { Scene.BlitEx(s, x, y, p.Flip, 0, 0, 100); return; }
            }
            Scene.Blit(s, x, y, p.Flip);
        }

        void DrawShadows(int cx, int cy)
        {
            for (int i = 0; i < Foes.Count; i++)
            {
                Enemy e = Foes[i];
                if (e.Dead || e.State == Enemy.Vanish) continue;
                int w = e.Spr != null ? e.Spr.ShadowW : 6;
                Scene.Ellipse(e.X - cx, e.Y - cy, w, w * 0.42f, 0x05050A, (int)(95 * e.FadeIn));
            }
            for (int i = 0; i < Npcs.Count; i++) Scene.Ellipse(Npcs[i].X - cx, Npcs[i].Y - cy, 6, 2.6f, 0x05050A, 90);
            if (P != null && !P.Dead) Scene.Ellipse(P.X - cx, P.Y - cy, 6.5f, 2.8f, 0x05050A, P.StealthT > 0 ? 40 : 100);
            for (int i = 0; i < Others.Count; i++) if (!Others[i].Dead) Scene.Ellipse(Others[i].X - cx, Others[i].Y - cy, 6.5f, 2.8f, 0x05050A, Others[i].StealthT > 0 ? 40 : 100);
            for (int i = 0; i < Drops.Count; i++) Scene.Ellipse(Drops[i].X - cx, Drops[i].Y - cy, 4, 1.6f, 0x05050A, 80);
            for (int i = 0; i < Shots.Count; i++) Scene.Ellipse(Shots[i].X - cx, Shots[i].Y - cy, 3, 1.3f, 0x05050A, 60);
        }

        /// <summary>Telegraphs and ground effects. Drawn emissive so they read clearly in the dark, but under the sprites.</summary>
        void DrawGroundFx(int cx, int cy)
        {
            const int Red = 0xE0483E;
            for (int i = 0; i < Areas.Count; i++)
            {
                Area a = Areas[i];
                float x = a.X - cx, y = a.Y - cy;
                switch (a.Kind)
                {
                    case Area.Trap:
                        {
                            bool blink = !a.Armed && ((int)(a.T * 14) & 1) == 0;
                            if (blink) break;
                            Scene.Ring(x, y, 7, 10, 0x8C9BB2, 200, false);
                            for (int k = 0; k < 8; k++) { float an = k * M.TAU / 8; Scene.Pixel((int)(x + M.Cos(an) * 6), (int)(y + M.Sin(an) * 6 / Surface.Squash), 0xDCE3E6); }
                            Scene.Pixel((int)x, (int)y, 0xCF4150);
                        }
                        break;
                    case Area.Rain:
                        Scene.Ring(x, y, 0, a.R, 0xE8E0C4, a.T < a.Delay ? 26 : 34, false);
                        Scene.Ring(x, y, a.R - 1.6f, a.R, 0xE8E0C4, 150, false);
                        break;
                    case Area.Pool:
                        {
                            float fade = M.Clamp01((a.Dur - a.T) / 0.5f);
                            Scene.Ring(x, y, 0, a.R, 0x3F6A24, (int)(150 * fade), false);
                            Scene.Ring(x, y, a.R - 2.5f, a.R, 0x6FA82E, (int)(190 * fade), false);
                        }
                        break;
                    case Area.Sigil:
                        if (!a.Armed)
                        {
                            float pr = M.Clamp01(a.T / a.Delay);
                            Scene.Ring(x, y, 0, a.R, 0x7F6CB6, 40 + (int)(70 * pr), false);
                            Scene.Ring(x, y, 0, a.R * pr, 0xA7A0DC, 80, false);
                            Scene.Ring(x, y, a.R - 1.6f, a.R, 0xEAF3FF, 220, false);
                            for (int k = 0; k < 6; k++)
                            {
                                float an = k * M.TAU / 6 + a.T * 2.2f + a.Seed;
                                Scene.Fill((int)(x + M.Cos(an) * a.R * 0.62f) - 1, (int)(y + M.Sin(an) * a.R * 0.62f / Surface.Squash) - 1, 2, 2, 0xEAF3FF);
                            }
                        }
                        break;
                }
            }
            for (int i = 0; i < Foes.Count; i++)
            {
                Enemy e = Foes[i];
                if (e.Dead || e.State != Enemy.Windup) continue;
                float pr = M.Clamp01(e.StateT / Math.Max(0.01f, e.StateDur));
                float x = e.X - cx, y = e.Y - cy;
                int al = 46 + (int)(70 * pr);
                switch (e.AttackKind)
                {
                    case AkArc:
                        {
                            float reach = e.Kind == EnemyKind.Knight ? 46 : (e.Kind == EnemyKind.Prior ? 52 : (e.Kind == EnemyKind.Crawler ? 24 : 30));
                            float half = e.Kind == EnemyKind.Knight ? 1.4f : (e.Kind == EnemyKind.Prior ? 1.5f : (e.Kind == EnemyKind.Crawler ? 1.1f : 1.05f));
                            reach += 5;
                            Scene.Sector(x, y, 0, reach, e.AttackAng, half, Red, al, false);
                            Scene.Sector(x, y, 0, reach * pr, e.AttackAng, half, Red, 70, false);
                            Scene.Sector(x, y, reach - 1.5f, reach, e.AttackAng, half, 0xFFB0A0, 170, false);
                        }
                        break;
                    case AkLine:
                        {
                            bool knight = e.Kind == EnemyKind.Knight;
                            float len = knight ? 216 : 92, hw = knight ? 13 : 8;
                            if (e.Kind == EnemyKind.Wolf || e.Kind == EnemyKind.AlphaWolf || knight)
                            {
                                float ex = x + M.Cos(e.AttackAng) * len, ey = y + M.Sin(e.AttackAng) * len * Surface.YMove;
                                Scene.Strip(x, y, ex, ey, hw, Red, al - 14);
                                Scene.Strip(x, y, x + (ex - x) * pr, y + (ey - y) * pr, hw, Red, 60);
                            }
                        }
                        break;
                    case AkCircle:
                        Scene.Ring(x, y, 0, 58, Red, al, false);
                        Scene.Ring(x, y, 0, 58 * pr, Red, 70, false);
                        Scene.Ring(x, y, 56.5f, 58, 0xFFB0A0, 180, false);
                        break;
                    case AkAim:
                        {
                            float ex = x + M.Cos(e.AttackAng) * 190, ey = y + M.Sin(e.AttackAng) * 190;
                            Scene.Line((int)x, (int)y - 6, (int)ex, (int)ey - 6, pr > 0.72f ? 0xFFE0D0 : Red, 80 + (int)(150 * pr));
                        }
                        break;
                }
            }
        }

        // actors
        void EnemyWeapon(Enemy e, int cx, int cy, out Sprite w, out float ang, out bool mirror, out float hx, out float hy)
        {
            w = null; ang = 0; mirror = false;
            int f = e.Facing;
            hx = e.X - cx + f * e.Spr.HandX; hy = e.Y - e.Z - cy + e.Spr.HandY;
            float pr = e.State == Enemy.Windup ? M.Clamp01(e.StateT / Math.Max(0.01f, e.StateDur)) : 0;
            float rest = f > 0 ? 0.9f : M.PI - 0.9f;
            switch (e.Kind)
            {
                case EnemyKind.Hollow: w = Assets.Cleaver; break;
                case EnemyKind.Archer: w = Assets.BoneBow; rest = f > 0 ? 0 : M.PI; hx += f * 2; break;
                case EnemyKind.Acolyte: w = Assets.CandleStaff; rest = f > 0 ? -1.15f : M.PI + 1.15f; break;
                case EnemyKind.Knight: w = Assets.Greatsword; rest = f > 0 ? 0.6f : M.PI - 0.6f; break;
                case EnemyKind.Prior: w = Assets.Crozier; rest = f > 0 ? -1.3f : M.PI + 1.3f; break;
                default: return;
            }
            ang = rest;
            if (e.Kind == EnemyKind.Archer) { if (e.Aggro) { Player tp = e.Target != null ? e.Target : P; ang = e.State == Enemy.Windup || tp == null ? e.AttackAng : M.Atan2(tp.Y - e.Y, tp.X - e.X); } }
            else if (e.Kind == EnemyKind.Acolyte || (e.Kind == EnemyKind.Prior && e.AttackKind != AkArc)) { if (e.State == Enemy.Windup) ang = rest - f * 0.5f * M.Sin(pr * M.PI); }
            else if (e.State == Enemy.Windup && e.AttackKind == AkArc) ang = e.AttackAng - f * (1.0f + 1.2f * pr);
            else if (e.WeaponSwing > 0) ang = e.AttackAng - f * (2.2f * e.WeaponSwing - 1.1f);
            else if (e.State == Enemy.Special && e.Kind == EnemyKind.Knight) ang = e.AttackAng;
            mirror = M.Cos(ang) < 0;
        }

        void DrawEnemy(Enemy e, int cx, int cy)
        {
            Sprite s = e.Frame();
            if (s == null) return;
            int x = (int)e.X - cx, y = (int)(e.Y - e.Z) - cy;
            bool flip = e.Facing < 0;
            int alpha = 256, tint = 0, tamt = 0;
            if (e.Dead) { float t = M.Clamp01(e.DeathT / 0.7f); alpha = (int)(256 * (1 - t)); y -= (int)(t * 7); tint = 0x584688; tamt = 120 + (int)(120 * t); }
            else if (e.State == Enemy.Vanish) { float t = e.StateT / Math.Max(0.01f, e.StateDur); alpha = (int)(256 * Math.Abs(1 - t * 2)); }
            else if (e.FadeIn < 1) alpha = (int)(256 * e.FadeIn);
            if (!e.Dead)
            {
                if (e.FlashT > 0) { tint = 0xFFFFFF; tamt = 230; }
                else if (e.FreezeT > 0) { tint = 0x9BDCF4; tamt = 150; }
                else if (e.State == Enemy.Windup)
                {
                    float pr = e.StateT / Math.Max(0.01f, e.StateDur);
                    if (pr > 0.7f || ((int)(pr * 9) & 1) == 0) { tint = 0xFF7060; tamt = 60 + (int)(pr * 70); }
                }
                else if (e.BurnT > 0) { tint = 0xEC8A32; tamt = 80; }
                else if (e.PoisonT > 0) { tint = 0x6FA82E; tamt = 70; }
                else if (e.StunT > 0) { tint = 0xFFF8DC; tamt = 40; }
            }
            // wind-up crouch / lunge stretch for beasts
            Sprite w; float wang, hx, hy; bool wm;
            EnemyWeapon(e, cx, cy, out w, out wang, out wm, out hx, out hy);
            bool behind = w != null && M.Sin(wang) < -0.5f && e.Kind != EnemyKind.Acolyte && e.Kind != EnemyKind.Prior;
            if (w != null && behind && !e.Dead) Scene.BlitRot(w, hx, hy, wang, wm, tint, tamt, alpha, 1f);
            Scene.BlitEx(s, x, y, flip, tint, tamt, alpha);
            if (w != null && !behind && !e.Dead) Scene.BlitRot(w, hx, hy, wang, wm, tint, tamt, alpha, 1f);
        }

        void DrawPlayerWeapon(int cx, int cy, int tint, int tamt, int alpha)
        {
            Player p = P;
            Item it = p.Equip[Player.SlotWeapon];
            int type = it != null ? it.Base : p.Def.WeaponTypes[0], tier = it != null ? it.Tier : 0;
            Sprite ws = Assets.Weapon[type, tier];
            WeaponType wt = Weapons.Types[type];
            float ca = M.Cos(p.AimAngle), sa = M.Sin(p.AimAngle);
            float hx = p.X - cx + ca * 3, hy = p.Y - cy - 9 + sa * 2;
            float ang = p.AimAngle;
            if (p.WhirlT > 0) ang = p.AimAngle + (1f - p.WhirlT / 0.5f) * M.TAU * 2f;
            else if (p.AtkT > 0 && !wt.Ranged)
            {
                float pr = 1f - p.AtkT / p.AtkDur;
                if (p.Class == ClassId.Rogue) { float th = M.Sin(pr * M.PI) * 6f; hx += ca * th; hy += sa * th; ang += p.SwingFrom * (1 - pr); }
                else ang += M.Lerp(p.SwingFrom, p.SwingTo, M.EaseOut(pr));
            }
            else if (!wt.Ranged && p.Class == ClassId.Warrior && !p.Blocking) ang += (ca >= 0 ? -0.35f : 0.35f);
            if (wt.Ranged)
            {
                float rec = p.Class == ClassId.Ranger ? (p.BowT > 0 ? -2f : 0) : (p.CastT > 0 ? 2.5f : 0);
                hx += ca * (4 + rec); hy += sa * (3 + rec * 0.6f);
            }
            bool mirror = M.Cos(ang) < 0;
            if (type == 4)
            {
                // off-hand dagger
                float a2 = p.AimAngle + (ca >= 0 ? 0.75f : -0.75f);
                Scene.BlitRot(ws, p.X - cx - ca * 2, p.Y - cy - 8, a2, M.Cos(a2) < 0, tint, tamt, alpha, 1f);
            }
            Scene.BlitRot(ws, hx, hy, ang, mirror, tint, tamt, alpha, 1f);
        }

        void DrawPlayer(int cx, int cy)
        {
            Player p = P;
            Sprite s = p.Frame();
            int x = (int)p.X - cx, y = (int)p.Y - cy;
            bool flip = p.Facing < 0;
            if (p.Dead)
            {
                float t = M.Clamp01((p == Me ? DeathT : p.DeadT) / 0.45f);
                float a = t * M.PI * 0.5f * (flip ? -1 : 1);
                Scene.BlitRot(s, x, y - 2, flip ? a + M.PI : a, flip, 0x3A1418, (int)(120 * t), 256, 1f);
                return;
            }
            int alpha = p.StealthT > 0 ? 92 : 256, tint = 0, tamt = 0;
            if (p.IFrames > 0 && p.DodgeT <= 0 && p.DashT <= 0 && ((int)(UiT * 30) & 1) == 0) alpha = Math.Min(alpha, 150);
            if (p.FlashT > 0) { tint = 0xFFFFFF; tamt = 220; }
            else if (p.FreezeT > 0) { tint = 0x9BDCF4; tamt = 140; }
            else if (p.OathT > 0) { tint = 0xFFD070; tamt = 50 + (int)(30 * M.Sin(UiT * 8)); }
            else if (p.PoisonT > 0) { tint = 0x6FA82E; tamt = 70; }
            else if (p.NextCrit) { tint = 0xC08CF0; tamt = 50; }
            bool behind = M.Sin(p.AimAngle) < -0.35f;
            if (p.DodgeT > 0)
            {
                // tumble: rotate about the body centre
                float pr = 1f - p.DodgeT / 0.26f;
                float a = pr * M.TAU * (flip ? -1 : 1);
                float ox = x - 12 * M.Sin(a), oy = y - 12 + 12 * M.Cos(a);
                Scene.BlitRot(s, ox, oy, flip ? a + M.PI : a, flip, tint, tamt, alpha, 1f);
                return;
            }
            if (behind) DrawPlayerWeapon(cx, cy, tint, tamt, alpha);
            Scene.BlitEx(s, x, y, flip, tint, tamt, alpha);
            if (!behind) DrawPlayerWeapon(cx, cy, tint, tamt, alpha);
            if (p.Blocking)
            {
                float ca = M.Cos(p.AimAngle), sa = M.Sin(p.AimAngle);
                if (sa > -0.6f) Scene.Blit(Assets.Shield, (int)(x + ca * 8), (int)(y - 9 + sa * 5), ca < 0);
            }
        }

        void DrawPickup(Pickup d, int cx, int cy)
        {
            int x = (int)d.X - cx, y = (int)(d.Y - d.Z) - cy;
            int bob = d.Z <= 0 ? (int)Math.Round(M.Sin(d.T * 4f) * 1.5f) : 0;
            switch (d.Kind)
            {
                case Pickup.Gold: Scene.Blit(Assets.Coin[(int)(d.T * 10) & 3], x, y); break;
                case Pickup.Health: Scene.Blit(Assets.Icon[ItemArt.IcHealth, 0], x - 8, y - 15 + bob); break;
                case Pickup.Spirit: Scene.Blit(Assets.Icon[ItemArt.IcMana, 0], x - 8, y - 15 + bob); break;
                default:
                    if (d.It != null) Scene.Blit(Assets.Icon[d.It.Icon, d.It.Tier], x - 8, y - 15 + bob);
                    break;
            }
        }

        void DrawShot(Projectile s, int cx, int cy)
        {
            if (s.Spr == null) return;
            float x = s.X - cx, y = s.Y - cy - s.Z;
            float ang = s.Lob ? M.Atan2(s.VY - s.VZ * 0.5f, s.VX) : M.Atan2(s.VY, s.VX);
            Scene.BlitRot(s.Spr, x, y, ang, false, 0, 0, 256, 1f);
        }

        void DrawParticles(Surface sf, int cx, int cy, bool lit)
        {
            for (int i = 0; i < PartCount; i++)
            {
                Particle p = Parts[i];
                if (p.Lit != lit) continue;
                int x = (int)p.X - cx, y = (int)(p.Y - p.Z) - cy;
                if (x < -8 || y < -8 || x > W + 8 || y > H + 8) continue;
                float t = p.Life / p.Max;
                int c = t > 0.5f ? p.Color : Col.Lerp(p.Color2 | Col.Opaque, p.Color | Col.Opaque, t * 2f) & 0xFFFFFF;
                switch (p.Kind)
                {
                    case PPixel:
                        if (lit) sf.Pixel(x, y, c); else sf.PixelA(x, y, c, (int)(255 * Math.Min(1f, t * 2f)));
                        break;
                    case PSquare: sf.Fill(x, y, 2, 2, c); break;
                    case PGlow:
                        {
                            float fade = Math.Min(1f, t * 2.5f) * Math.Min(1f, (1f - t) * 8f);
                            sf.Glow(x + 0.5f, y + 0.5f, p.Size, c, (int)(210 * fade));
                            sf.PixelAdd(x, y, c, (int)(255 * fade));
                        }
                        break;
                    case PSmoke: sf.Ellipse(x, y, p.Size, p.Size * 0.8f, c, (int)(110 * t)); break;
                    case PStreak: sf.Line(x, y, x, y + 7, c, (int)(220 * t + 30)); break;
                }
            }
        }

        // unlit effects
        void DrawSlashes(int cx, int cy)
        {
            for (int n = 0; n < Party.Count; n++)
            {
                Player p = Party[n];
                if (p.Dead) continue;
                WeaponType wt = p.WType;
                if (p.AtkT > 0 && !wt.Ranged)
                {
                    float pr = 1f - p.AtkT / p.AtkDur;
                    int f = M.Clamp((int)(pr * 3.2f), 0, 2);
                    Sprite[] set = p.Class == ClassId.Warrior ? (p.SwingStep == 2 || wt.Reach > 29 ? Assets.SlashL : Assets.SlashM) : Assets.SlashS;
                    bool rev = p.SwingFrom > p.SwingTo;
                    Final.BlitRot(set[f], p.X - cx + M.Cos(p.AimAngle) * 3, p.Y - cy - 8 + M.Sin(p.AimAngle) * 2, p.AimAngle, rev, 0, 0, 256, 1f);
                }
                if (p.WhirlT > 0)
                {
                    float pr = 1f - p.WhirlT / 0.5f;
                    int f = M.Clamp((int)(pr * 3f), 0, 2);
                    for (int k = 0; k < 3; k++)
                        Final.BlitRot(Assets.SlashL[f], p.X - cx, p.Y - cy - 8, pr * M.TAU * 2f + k * M.TAU / 3, false, 0, 0, 220, 1.25f);
                }
            }
            for (int i = 0; i < Foes.Count; i++)
            {
                Enemy e = Foes[i];
                if (e.Dead || e.WeaponSwing <= 0) continue;
                int f = M.Clamp((int)((1f - e.WeaponSwing) * 3f), 0, 2);
                Sprite[] set = e.Kind == EnemyKind.Knight ? Assets.SlashHuge : (e.Kind == EnemyKind.Prior ? Assets.SlashGloam : Assets.SlashRed);
                float sc = e.Kind == EnemyKind.Knight ? 1.15f : (e.Kind == EnemyKind.Prior ? 1.4f : 1.2f);
                Final.BlitRot(set[f], e.X - cx, e.Y - cy - 8, e.AttackAng, e.Facing < 0, 0, 0, 240, sc);
            }
        }

        void DrawAreasPost(int cx, int cy)
        {
            for (int i = 0; i < Areas.Count; i++)
            {
                Area a = Areas[i];
                float t = a.Dur > 0 ? M.Clamp01(a.T / a.Dur) : 0;
                float x = a.X - cx, y = a.Y - cy;
                switch (a.Kind)
                {
                    case Area.Ring:
                        {
                            float r = a.R * M.EaseOut(t);
                            Final.Ring(x, y, Math.Max(0, r - 5), r, a.Color, (int)(230 * (1 - t)), true);
                            Final.Ring(x, y, 0, r, a.Color, (int)(50 * (1 - t)), true);
                        }
                        break;
                    case Area.Burst:
                        Final.Glow(x, y - 4, a.R * (0.8f + t * 0.9f), 0xFFC070, (int)(230 * (1 - t)));
                        Final.Ring(x, y, a.R * t * 0.8f, a.R * (0.25f + t), 0xFFA040, (int)(200 * (1 - t)), true);
                        break;
                    case Area.Bolt:
                        {
                            Rng r = new Rng(a.Seed + (int)(a.T * 40));
                            float px = a.X - cx, py = a.Y - cy;
                            float ex = a.X2 - cx, ey = a.Y2 - cy;
                            int segs = 6;
                            for (int k = 1; k <= segs; k++)
                            {
                                float u = k / (float)segs;
                                float nx = M.Lerp(a.X - cx, ex, u) + (k < segs ? r.Sym(7) : 0), ny = M.Lerp(a.Y - cy, ey, u) + (k < segs ? r.Sym(7) : 0);
                                int al = (int)(255 * (1 - t));
                                Final.LineAdd((int)px, (int)py, (int)nx, (int)ny, 0xE2F8FF, al);
                                Final.LineAdd((int)px + 1, (int)py, (int)nx + 1, (int)ny, 0x58A8D8, al * 2 / 3);
                                Final.LineAdd((int)px, (int)py + 1, (int)nx, (int)ny + 1, 0x58A8D8, al / 2);
                                px = nx; py = ny;
                            }
                            Final.Glow(ex, ey, 16, 0xC8E8FF, (int)(220 * (1 - t)));
                        }
                        break;
                    case Area.Sigil:
                        if (a.Armed)
                        {
                            float u = M.Clamp01((a.T - a.Delay) / 0.4f);
                            Final.Ring(x, y, 0, a.R, 0xC9D3F5, (int)(190 * (1 - u)), true);
                            Final.Glow(x, y - 8, a.R * 1.4f, 0xA7A0DC, (int)(200 * (1 - u)));
                        }
                        break;
                }
            }
            // rarity beams over loot
            for (int i = 0; i < Drops.Count; i++)
            {
                Pickup d = Drops[i];
                if (d.Z > 0) continue;
                int col = 0; int strength = 0;
                if (d.Kind == Pickup.Equip && d.It != null && d.It.Rar >= Rarity.Fine) { col = Items.RarityColor[(int)d.It.Rar]; strength = 60 + (int)d.It.Rar * 40; }
                else if (d.Kind == Pickup.Quest) { col = 0xC8D8FF; strength = 190; }
                if (strength == 0) continue;
                int x = (int)d.X - cx, y = (int)d.Y - cy;
                Final.Glow(x, y - 7, 11, col, strength);
                if (strength >= 140)
                    for (int k = 0; k < 26; k++) Final.PixelAdd(x, y - 12 - k, col, strength - k * strength / 26);
            }
        }

        public void WorldToScreen(float wx, float wy, out int sx, out int sy) { sx = (int)wx - CamXi; sy = (int)wy - CamYi; }

        void DrawWorldUi(int cx, int cy)
        {
            Player p = P;
            // enemy bars, alerts and status marks
            for (int i = 0; i < Foes.Count; i++)
            {
                Enemy e = Foes[i];
                if (e.Dead || e.State == Enemy.Vanish) continue;
                int x, top;
                if (!UiAt(e.X, e.Y, FoeTop(e), out x, out top)) continue;
                if (x < -30 || x > W + 30 || top < -40 || top > H + 20) continue;
                if (!e.Def.Elite && !e.Def.Boss && (e.HpBarT > 0 || (e.Aggro && e.HP < e.MaxHP)))
                {
                    int bw = 20, bx = x - bw / 2, by = top - 5;
                    Final.Fill(bx - 1, by - 1, bw + 2, 4, 0x0B0A12);
                    Final.Fill(bx, by, bw, 2, 0x3F1022);
                    Final.Fill(bx, by, (int)Math.Ceiling(bw * M.Clamp01(e.HP / e.MaxHP)), 2, e.MarkT > 0 ? 0xFF8060 : 0xCF4150);
                }
                if (e.AlertShow > 0) Final.TextOutlined(Fonts.BodyBold, "!", x - 2, top - 17 - (int)(e.AlertShow * 4), 0xF7BE52, 0x0B0A12);
                if (e.StunT > 0 && e.FreezeT <= 0)
                    for (int k = 0; k < 3; k++)
                    {
                        float an = UiT * 6f + k * M.TAU / 3;
                        Final.Fill(x + (int)(M.Cos(an) * 7), top - 3 + (int)(M.Sin(an) * 2.5f), 2, 2, 0xFFF8DC);
                    }
                if (e.MarkT > 0)
                {
                    Final.Ring(x, top - 9, 3, 4.6f, 0xE0584E, 230, false);
                    Final.Fill(x, top - 10, 1, 2, 0xFFF8DC);
                }
                if (e.RootT > 0)
                {
                    int fx, fy; UiAt(e.X, e.Y, 0, out fx, out fy);
                    Final.Fill(fx - 6, fy - 1, 12, 1, 0x8C9BB2); Final.Fill(fx - 4, fy - 3, 1, 2, 0xDCE3E6); Final.Fill(fx + 3, fy - 3, 1, 2, 0xDCE3E6);
                }
            }
            // villagers: quest marks and names
            for (int i = 0; i < Npcs.Count; i++)
            {
                Npc n = Npcs[i];
                int x, top;
                if (!UiAt(n.X, n.Y, NpcTop(n), out x, out top)) continue;
                char mk = Q.NpcMark(n.Id);
                if (mk != '\0')
                {
                    int bob = (int)Math.Round(M.Sin(UiT * 3.5f + i) * 1.5f);
                    string s = mk == '.' ? "?" : mk.ToString();
                    int col = mk == '!' ? 0xF7BE52 : (mk == '?' ? 0xF7BE52 : 0x8A8A9A);
                    Final.TextOutlined(Fonts.Title, s, x - Fonts.Title.Width(s) / 2, top - 20 + bob, col, 0x0B0A12);
                }
                if (p != null && M.Dist2(n.X, n.Y, p.X, p.Y) < 70 * 70 && n != NearNpc)
                    Final.TextOutlined(Fonts.Small, n.Name, x - Fonts.Small.Width(n.Name) / 2, top - 8 - (mk != '\0' ? 14 : 0), 0xC8BE9F, 0x0B0A12);
            }
            // companions: name and health
            for (int i = 0; i < Others.Count; i++)
            {
                Player o = Others[i];
                int x, top;
                if (!UiAt(o.X, o.Y, R3 != null ? 37 : 30, out x, out top)) continue;
                if (x < -40 || x > W + 40 || top < -20 || top > H + 20) continue;
                string nm = Loc.Raw(o.NetName);
                Final.TextOutlined(Fonts.Small, nm, x - Fonts.Small.Width(nm) / 2, top - 11, o.Dead ? 0x8A8A9A : 0x9FE8DF, 0x0B0A12);
                if (!o.Dead && o.HP < o.MaxHP)
                {
                    int bw = 22, bx = x - bw / 2;
                    Final.Fill(bx - 1, top + 1, bw + 2, 4, 0x0B0A12);
                    Final.Fill(bx, top + 2, bw, 2, 0x2A1018);
                    Final.Fill(bx, top + 2, (int)Math.Ceiling(bw * M.Clamp01(o.HP / o.MaxHP)), 2, 0x8FCB6B);
                }
            }
            // loot names: the nearest item's name sits where it belongs; names that would overlap it stack upward
            if (p != null)
            {
                int nl = 0;
                for (int i = 0; i < Drops.Count && nl < labD.Length; i++)
                {
                    Pickup d = Drops[i];
                    if ((d.Kind != Pickup.Equip && d.Kind != Pickup.Quest) || d.It == null || d.Z > 0) continue;
                    float dd = M.Dist2(d.X, d.Y, p.X, p.Y);
                    if (dd > 60 * 60) continue;
                    int lx, ly;
                    if (!UiAt(d.X, d.Y, R3 != null ? 24 : 30, out lx, out ly)) continue;
                    int w = Fonts.Small.Width(d.It.DisplayName);
                    int k = nl++;
                    while (k > 0 && labDist[k - 1] > dd) { labD[k] = labD[k - 1]; labX[k] = labX[k - 1]; labY[k] = labY[k - 1]; labW[k] = labW[k - 1]; labDist[k] = labDist[k - 1]; k--; }
                    labD[k] = d; labX[k] = lx - w / 2; labY[k] = ly; labW[k] = w; labDist[k] = dd;
                }
                int lh = Fonts.Small.LineH + 1;
                for (int i = 0; i < nl; i++)
                {
                    for (int tries = 0; tries < nl; tries++)
                    {
                        bool hit = false;
                        for (int j = 0; j < i; j++)
                            if (labX[i] < labX[j] + labW[j] + 4 && labX[j] < labX[i] + labW[i] + 4 && labY[i] < labY[j] + lh && labY[j] < labY[i] + lh)
                            { labY[i] = labY[j] - lh; hit = true; break; }
                        if (!hit) break;
                    }
                    Pickup d = labD[i];
                    Final.TextOutlined(Fonts.Small, d.It.DisplayName, labX[i], labY[i], Items.RarityColor[(int)d.It.Rar], 0x0B0A12);
                    labD[i] = null;
                }
            }
            // floating combat text
            for (int i = 0; i < Floats.Count; i++)
            {
                Floater f = Floats[i];
                PixFont font = f.Big ? Fonts.BodyBold : Fonts.Small;
                float t = f.T / f.Dur;
                if (t > 0.85f && ((int)(f.T * 40) & 1) == 0) continue;
                int fx, fy;
                if (!UiAt(f.X, f.Y, 0, out fx, out fy)) continue;
                Final.TextOutlined(font, f.Text, fx - font.Width(f.Text) / 2, fy - font.LineH, f.Color, 0x0B0A12);
            }
            // interaction prompt
            if (p != null && !p.Dead && Ov == Overlay.None)
            {
                string label = null; int px = 0, py = 0;
                if (NearNpc != null)
                {
                    label = "Talk";
                    UiAt(NearNpc.X, NearNpc.Y, NpcTop(NearNpc), out px, out py);
                    py -= 12;
                    if (Q.NpcMark(NearNpc.Id) != '\0') py -= 14;
                    DrawPrompt(px, py, label, NearNpc.Name);
                }
                else if (NearProp != null)
                {
                    label = NearProp.Prompt;
                    int h = NearProp.Spr != null && !NearProp.Hidden ? Math.Min(46, NearProp.Spr.H) : 30;
                    UiAt(NearProp.X, NearProp.Y, h, out px, out py);
                    py -= 12;
                    DrawPrompt(px, py, label, null);
                }
            }
            DrawQuestMarker(cx, cy);
        }

        void DrawPrompt(int x, int y, string label, string sub)
        {
            PixFont f = Fonts.Small;
            int w = f.Width(label) + 22;
            int subW = sub != null ? f.Width(sub) : 0;
            int bx = M.Clamp(x - w / 2, 4, W - w - 4), by = M.Clamp(y, 18, H - 60);
            if (sub != null) Final.TextOutlined(f, sub, M.Clamp(x - subW / 2, 4, W - subW - 4), by - 12, 0xF2ECD8, 0x0B0A12);
            Final.FillA(bx, by, w, 13, 0x0B0A12, 210);
            Final.Frame(bx, by, w, 13, 0x7A5A2C);
            Final.Fill(bx + 2, by + 2, 12, 9, 0xD2AC52);
            Final.Text(f, "F", bx + 5, by + 1, 0x15121F);
            Final.Text(f, label, bx + 17, by + 1, 0xF2ECD8);
        }

        void DrawQuestMarker(int cx, int cy)
        {
            if (P == null || Q == null) return;
            Quest q = Q.Get(Q.Tracked);
            if (q == null || !q.Active) q = Q.Get("bell");
            float tx, ty;
            if (!Q.Target(this, q, out tx, out ty)) return;
            int sx, sy;
            bool front = UiAt(tx, ty, R3 != null ? 40 : 34, out sx, out sy);
            int col = 0xF7BE52;
            if (front && sx > 12 && sx < W - 12 && sy > 24 && sy < H - 50)
            {
                if (M.Dist(tx, ty, P.X, P.Y) < 40) return;
                int bob = (int)Math.Round(M.Sin(UiT * 4f) * 2f);
                for (int k = 0; k < 4; k++) { Final.Fill(sx - k, sy + bob - 4 + k, k * 2 + 1, 1, col); Final.Fill(sx - k, sy + bob + 4 - k, k * 2 + 1, 1, col); }
                Final.Fill(sx, sy + bob - 1, 1, 3, 0x15121F);
            }
            else
            {
                // arrow on the edge of the screen pointing toward the objective
                // through the hero's eyes the arrow points relative to the view: up means ahead, down means behind
                float ang = FirstPerson ? M.AngDiff(LookYaw, M.Atan2(ty - P.Y, tx - P.X)) - M.PI / 2 : M.Atan2((ty - P.Y) * (R3 != null ? 0.79f : 1f), tx - P.X);
                float ex = W / 2 + M.Cos(ang) * (W / 2 - 22), ey = H / 2 + M.Sin(ang) * (H / 2 - 30);
                ex = M.Clamp(ex, 16, W - 16); ey = M.Clamp(ey, 30, H - 58);
                float pulse = 0.75f + 0.25f * M.Sin(UiT * 5f);
                for (int k = 0; k < 7; k++)
                {
                    float bx = ex - M.Cos(ang) * k, by = ey - M.Sin(ang) * k;
                    int hw = k / 2 + (k > 0 ? 1 : 0);
                    for (int j = -hw; j <= hw; j++)
                        Final.PixelA((int)(bx - M.Sin(ang) * j), (int)(by + M.Cos(ang) * j), col, (int)(255 * pulse));
                }
            }
        }

        void EdgeTint(int color, float strength)
        {
            if (strength <= 0) return;
            for (int k = 0; k < 9; k++)
            {
                int a = (int)(strength * (9 - k) * 15);
                if (a <= 0) continue;
                int t = 5, o = k * t;
                Final.FillA(o, o, W - o * 2, t, color, a);
                Final.FillA(o, H - o - t, W - o * 2, t, color, a);
                Final.FillA(o, o + t, t, H - o * 2 - t * 2, color, a);
                Final.FillA(W - o - t, o + t, t, H - o * 2 - t * 2, color, a);
            }
        }

        void DrawScreenFx()
        {
            Player p = P;
            if (p == null) return;
            if (p.HurtT > 0) EdgeTint(0xA2233B, p.HurtT / 0.4f);
            float hpf = p.HP / Math.Max(1, p.MaxHP);
            if (!p.Dead && hpf < 0.3f) EdgeTint(0x6E1730, (0.3f - hpf) * 2.2f * (0.6f + 0.4f * M.Sin(UiT * 6f)));
            if (p.StealthT > 0) EdgeTint(0x231C3A, 0.8f);
            if (p.FreezeT > 0) EdgeTint(0x58A8D8, 0.7f);
            if (p.Dead) { Final.FillA(0, 0, W, H, 0x1A0A10, (int)(150 * M.Clamp01(DeathT / 1.4f))); }
        }
    }
}
