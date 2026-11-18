using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>In-game heads-up display: vitals, ability bar, quest tracker, minimap, boss bar and banners.</summary>
    public sealed partial class Game
    {
        float hudHpGhost = 1f;
        readonly float[] cdFlash = new float[5];
        readonly bool[] cdWas = new bool[5];
        static readonly string[] SlotKeys = { "LMB", "RMB", "Q", "E", "R" };

        /// <summary>The first-person crosshair: a dot and four ticks, warm red while it rests on a foe.</summary>
        void DrawCrosshair(Surface g)
        {
            int cx = W / 2, cy = H / 2;
            bool foe = AimFoe != null;
            int col = foe ? 0xF2705E : 0xEDE6D2, rim = 0x0B0A12;
            int gap = foe ? 3 : 4, len = 3;
            g.FillA(cx - 1, cy - 1, 3, 3, rim, 150);
            g.Fill(cx, cy, 1, 1, col);
            for (int k = 0; k < 4; k++)
            {
                int dx = k == 0 ? -1 : (k == 1 ? 1 : 0), dy = k == 2 ? -1 : (k == 3 ? 1 : 0);
                int x0 = cx + dx * (gap + len - 1), y0 = cy + dy * (gap + len - 1);
                int x = Math.Min(x0, cx + dx * gap), y = Math.Min(y0, cy + dy * gap);
                int w = dx != 0 ? len : 1, h = dy != 0 ? len : 1;
                g.FillA(x - 1, y - 1, w + 2, h + 2, rim, 130);
                g.FillA(x, y, w, h, col, 235);
            }
        }

        void DrawHud()
        {
            Player p = P;
            if (p == null) return;
            G = Final; tipLines = null;
            Surface g = Final;
            PixFont fs = Fonts.Small;
            if (FirstPerson && Ov == Overlay.None && !p.Dead) DrawCrosshair(g);

            // vitals
            int vx = 6, vy = H - 50;
            g.FillA(vx, vy, 180, 44, CInk, 170);
            g.Frame(vx, vy, 180, 44, CBronze); g.Frame(vx + 1, vy + 1, 178, 42, CInk);
            int bx = vx + 5, by = vy + 5;
            g.Fill(bx, by, 34, 34, CInk); g.Fill(bx + 1, by + 1, 32, 32, 0x1D1930); g.Frame(bx, by, 34, 34, CGold);
            g.SetClip(bx + 1, by + 1, 32, 32);
            g.FillA(bx + 1, by + 18, 32, 15, p.Def.Accent, 50);
            g.BlitScaled(p.Spr.Idle[(int)(UiT * 1.6f) & 1], bx - 5, by + 1, 2, false);
            g.ResetClip();
            string lv = p.Level.ToString();
            g.Fill(bx + 22, by + 24, 13, 11, CInk); g.Frame(bx + 22, by + 24, 13, 11, CGold);
            g.Text(Fonts.Tiny, lv, bx + 29 - Fonts.Tiny.Width(lv) / 2, by + 27, CGoldHi);
            if (p.AttrPoints > 0 && ((int)(UiT * 3) & 1) == 0) g.Text(fs, "+" + p.AttrPoints, bx + 1, by, CGoldHi, 0x000000);

            float hpf = M.Clamp01(p.HP / Math.Max(1, p.MaxHP));
            if (hudHpGhost < hpf) hudHpGhost = hpf; else hudHpGhost = Math.Max(hpf, hudHpGhost - DT * 0.45f);
            int hx = vx + 46, hw = 128;
            g.Fill(hx - 1, vy + 6, hw + 2, 11, CInk); g.Fill(hx, vy + 7, hw, 9, 0x2A1018);
            g.Fill(hx, vy + 7, (int)(hw * hudHpGhost), 9, 0xF2D0A0);
            int fw = (int)Math.Round(hw * hpf);
            if (fw > 0) { g.GradV(hx, vy + 7, fw, 9, 0xE0584E, 0x8F2F22); g.HLine(hx, vy + 7, fw, 0xF08A7A); }
            if (p.OathT > 0) g.Frame(hx - 1, vy + 6, hw + 2, 11, CGoldHi);
            string hpt = ((int)Math.Ceiling(Math.Max(0, p.HP))) + "/" + (int)p.MaxHP;
            g.Text(Fonts.Tiny, hpt, hx + hw / 2 - Fonts.Tiny.Width(hpt) / 2, vy + 9, 0xFFFFFF, 0x000000);

            float rf = M.Clamp01(p.Res / Math.Max(1, p.MaxRes));
            g.Fill(hx - 1, vy + 20, hw + 2, 9, CInk); g.Fill(hx, vy + 21, hw, 7, 0x16172A);
            int rw = (int)Math.Round(hw * rf);
            if (rw > 0) { g.GradV(hx, vy + 21, rw, 7, p.Def.ResColor, p.Def.ResColorDark); g.HLine(hx, vy + 21, rw, Col.Lerp(p.Def.ResColor | Col.Opaque, Col.Opaque | 0xFFFFFF, 0.4f) & 0xFFFFFF); }
            string rt = ((int)p.Res).ToString();
            g.Text(Fonts.Tiny, rt, hx + hw / 2 - Fonts.Tiny.Width(rt) / 2, vy + 22, 0xFFFFFF, 0x000000);

            float xf = p.Level >= Player.MaxLevel ? 1f : M.Clamp01(p.XP / (float)p.XpToNext);
            g.Fill(hx - 1, vy + 32, hw + 2, 5, CInk); g.Fill(hx, vy + 33, hw, 3, 0x16172A);
            g.Fill(hx, vy + 33, (int)(hw * xf), 3, 0x7FE0D8);
            for (int k = 1; k < 8; k++) g.Fill(hx + hw * k / 8, vy + 33, 1, 3, CInk);
            if (Hot(hx, vy + 30, hw, 8)) Tip("Experience", p.Level >= Player.MaxLevel ? "Maximum level reached." : Loc.F("{0} / {1} to level {2}", p.XP, p.XpToNext, p.Level + 1));
            else if (Hot(hx, vy + 5, hw, 12)) Tip("Health", "Rest at a wayshrine or drink a draught (^y1^0) to recover.");
            else if (Hot(hx, vy + 19, hw, 10)) Tip(p.Def.ResName, p.Def.Passive);

            // status pips
            int px = vx + 182, py = vy + 4;
            if (p.OathT > 0) StatusPip(ref px, py, 0xF7BE52, "O", p.OathT);
            if (p.StealthT > 0) StatusPip(ref px, py, 0xA7A0DC, "V", p.StealthT);
            if (p.NextCrit) StatusPip(ref px, py, 0xC08CF0, "!", p.NextCritT);
            if (p.PoisonT > 0) StatusPip(ref px, py, 0x8FCB6B, "P", p.PoisonT);
            if (p.SlowT > 0) StatusPip(ref px, py, 0x7FB2F0, "S", p.SlowT);

            // ability bar
            int ax = W / 2 - 96, ay = H - 34;
            g.FillA(ax - 34, ay - 15, 262, 46, CInk, 150);
            g.Frame(ax - 34, ay - 15, 262, 46, CBronze); g.Frame(ax - 33, ay - 14, 260, 44, CInk);
            // dodge pips
            g.Text(fs, Loc.Raw("Space"), ax - 30, ay - 13, CDim, 0x000000);
            for (int k = 0; k < p.Def.DodgeCharges; k++)
            {
                bool have = k < p.DodgeCharges;
                int dx = ax - 26 + k * 12, dy = ay + 6;
                for (int j = 0; j < 4; j++) { g.Fill(dx - j, dy - 3 + j, j * 2 + 1, 1, have ? CGoldHi : 0x3A3448); g.Fill(dx - j, dy + 4 - j, j * 2 + 1, 1, have ? CGold : 0x3A3448); }
            }
            if (Hot(ax - 32, ay - 14, 30, 40)) Tip("Dodge Roll  [Space]", "Tumble in the direction you are moving. You cannot be hit for a moment.");

            for (int i = 0; i < 5; i++)
            {
                AbilityDef ab = p.Def.Abilities[i];
                int sx = ax + i * 27, sy = ay;
                bool locked = p.Level < ab.UnlockLevel;
                float cd = p.Cd[i], cdMax = Math.Max(0.01f, ab.Cooldown * p.CdMult);
                bool ready = !locked && cd <= 0 && p.Res >= ab.Cost;
                if (ready && !cdWas[i] && ab.Cooldown > 0) cdFlash[i] = 0.3f;
                cdWas[i] = ready;
                if (cdFlash[i] > 0) cdFlash[i] -= DT;
                g.Fill(sx, sy, 24, 24, CInk); g.Fill(sx + 1, sy + 1, 22, 22, CSlot);
                g.Blit(Assets.Skill[ab.Icon], sx + 3, sy + 3);
                bool active = (i == 1 && p.Blocking) || (p.Class == ClassId.Warrior && i == 4 && p.OathT > 0) || (p.Class == ClassId.Rogue && i == 3 && p.StealthT > 0) || (p.Class == ClassId.Warrior && i == 3 && p.WhirlT > 0);
                if (locked)
                {
                    g.FillA(sx + 1, sy + 1, 22, 22, CInk, 215);
                    string l = Loc.F("L{0}", ab.UnlockLevel);
                    g.Text(fs, l, sx + 12 - fs.Width(l) / 2, sy + 6, CDim, 0x000000);
                }
                else if (cd > 0)
                {
                    int hh = Math.Max(1, (int)(22 * M.Clamp01(cd / cdMax)));
                    g.FillA(sx + 1, sy + 1, 22, 22, CInk, 120);
                    g.FillA(sx + 1, sy + 23 - hh, 22, hh, CInk, 150);
                    string s = cd >= 1 ? ((int)Math.Ceiling(cd)).ToString() : "";
                    if (s.Length > 0) g.Text(Fonts.BodyBold, s, sx + 12 - Fonts.BodyBold.Width(s) / 2, sy + 5, CWhite, 0x000000);
                }
                else if (p.Res < ab.Cost) g.FillA(sx + 1, sy + 1, 22, 22, 0x300A14, 170);
                g.Frame(sx, sy, 24, 24, active ? CGoldHi : (cdFlash[i] > 0 ? 0xFFFFFF : (ready ? CBronze : CLine)));
                if (cdFlash[i] > 0) g.FillA(sx + 1, sy + 1, 22, 22, 0xFFFFFF, (int)(160 * cdFlash[i] / 0.3f));
                string kl = SlotKeys[i];
                g.Text(fs, kl, sx + 12 - fs.Width(kl) / 2, sy - 12, ready ? CText : CDim, 0x000000);
                if (ab.Cost > 0 && !locked) g.TextRight(Fonts.Tiny, ab.Cost.ToString(), sx + 23, sy + 17, p.Def.ResColor, 0x000000);
                if (Hot(sx, sy, 24, 24))
                {
                    List<string> t = new List<string>();
                    t.Add("^y" + Loc.Tr(ab.Name) + "^0  ^d[" + Loc.Tr(ab.Key) + "]^0");
                    string meta = "";
                    if (ab.Cost > 0) meta += ab.Cost + " " + Loc.Tr(p.Def.ResName);
                    if (ab.Cooldown > 0) meta += (meta.Length > 0 ? "   " : "") + Loc.F("{0}s cooldown", (ab.Cooldown * p.CdMult).ToString("0.#"));
                    if (meta.Length > 0) t.Add("^c" + meta + "^0");
                    t.Add(ab.Desc);
                    if (locked) t.Add(Loc.F("^rUnlocks at level {0}^0", ab.UnlockLevel));
                    Tip(t, 200);
                }
            }
            // potions
            for (int k = 0; k < 2; k++)
            {
                int sx = ax + 5 * 27 + 10 + k * 27, sy = ay;
                int count = k == 0 ? p.HealthPotions : p.SpiritPotions;
                g.Fill(sx, sy, 24, 24, CInk); g.Fill(sx + 1, sy + 1, 22, 22, CSlot);
                g.Blit(Assets.Icon[k == 0 ? ItemArt.IcHealth : ItemArt.IcMana, 0], sx + 4, sy + 4);
                if (count <= 0) g.FillA(sx + 1, sy + 1, 22, 22, CInk, 200);
                else if (p.PotionCd > 0) g.FillA(sx + 1, sy + 23 - (int)(22 * p.PotionCd / 3f), 22, (int)(22 * p.PotionCd / 3f), CInk, 170);
                bool urge = k == 0 && count > 0 && p.HP < p.MaxHP * 0.35f && ((int)(UiT * 4) & 1) == 0;
                g.Frame(sx, sy, 24, 24, urge ? 0xFF8070 : CBronze);
                g.TextRight(Fonts.Tiny, count.ToString(), sx + 23, sy + 17, 0xFFFFFF, 0x000000);
                string kl = (k + 1).ToString();
                g.Text(fs, kl, sx + 12 - fs.Width(kl) / 2, sy - 12, urge ? 0xFF8070 : CText, 0x000000);
                if (Hot(sx, sy, 24, 24)) Tip(k == 0 ? "Health Draught  [1]" : "Spirit Draught  [2]", k == 0 ? "Restores 45% of your health over a moment." : (p.Class == ClassId.Warrior ? "Restores 50 Fury." : Loc.F("Restores 60% of your {0}.", Loc.Tr(p.Def.ResName))));
            }

            // quest tracker
            Quest q = Q.Get(Q.Tracked);
            if (q == null || !q.Active) q = Q.Get("bell");
            int partyY = 12;
            if (q != null && q.Active)
            {
                int qy = 8;
                g.Fill(8, qy + 3, 3, 3, CGold);
                g.Text(Fonts.BodyBold, q.Title, 15, qy - 2, CGoldHi, 0x000000);
                qy += Fonts.BodyBold.LineH;
                List<string> lines = fs.Wrap(q.Objective, Boss != null && !Boss.Dead && Boss.Aggro ? 158 : 190);     // keep clear of the boss bar
                for (int i = 0; i < lines.Count; i++) { g.Text(fs, lines[i], 15, qy, CText, 0x000000); qy += fs.LineH; }
                partyY = qy + 8;
            }

            // the party
            for (int i = 0; i < Others.Count; i++)
            {
                Player o = Others[i];
                int fy = partyY + i * 21;
                g.FillA(8, fy, 100, 18, CInk, 150);
                g.Fill(8, fy, 2, 18, o.Def.Accent);
                g.Text(fs, Loc.Raw(o.NetName), 14, fy, o.Dead ? CDim : CWhite, 0x000000);
                g.TextRight(fs, o.Level.ToString(), 105, fy, CGold, 0x000000);
                g.Fill(14, fy + 13, 91, 3, 0x2A1018);
                if (!o.Dead) g.Fill(14, fy + 13, (int)(91 * M.Clamp01(o.HP / Math.Max(1, o.MaxHP))), 3, 0xCF4150);
            }
            if (chatOpen)
            {
                int cw = 300, cx0 = W / 2 - cw / 2, cy0 = H - 72;
                g.FillA(cx0, cy0, cw, 16, CInk, 225); g.Frame(cx0, cy0, cw, 16, CGold);
                g.Text(fs, "Say:", cx0 + 5, cy0 + 2, CGold, 0x000000);
                int tx0 = cx0 + 10 + fs.Width("Say:");
                string shown = chatText;
                while (shown.Length > 0 && fs.Width(Loc.Raw(shown)) > cx0 + cw - 8 - tx0) shown = shown.Substring(1);
                g.Text(fs, Loc.Raw(shown), tx0, cy0 + 2, CWhite, 0x000000);
                if (((int)(UiT * 2.5f) & 1) == 0) g.Fill(tx0 + fs.Width(Loc.Raw(shown)) + 1, cy0 + 3, 1, 10, CGoldHi);
            }

            DrawMinimap();
            DrawBanners();
            DrawNotices();
            if (Ov == Overlay.None) DrawTooltip(); else tipLines = null;
        }

        void StatusPip(ref int x, int y, int color, string letter, float time)
        {
            Final.Fill(x, y, 13, 13, CInk); Final.Fill(x + 1, y + 1, 11, 11, Col.Mul(color | Col.Opaque, 0.35f) & 0xFFFFFF); Final.Frame(x, y, 13, 13, color);
            Final.Text(Fonts.Small, letter, x + 7 - Fonts.Small.Width(letter) / 2, y + 1, color);
            string t = ((int)Math.Ceiling(time)).ToString();
            Final.Text(Fonts.Tiny, t, x + 7 - Fonts.Tiny.Width(t) / 2, y + 15, CText, 0x000000);
            x += 15;
        }

        readonly int[] miniBuf = new int[64 * 64], miniBig = new int[128 * 128];

        static void MiniDot(Surface g, float x, float y, float r, int col)
        {
            g.Ellipse(x, y, r + 0.8f, r + 0.8f, CInk, 220);
            g.Ellipse(x, y, r, r, col, 255);
        }

        void DrawMinimap()
        {
            Player p = P; Map m = Cur; Surface g = Final;
            const int MS = 64;
            int mx = W - MS - 10, my = 8;
            g.Fill(mx - 2, my - 2, MS + 4, MS + 4, CInk);
            g.Frame(mx - 2, my - 2, MS + 4, MS + 4, CBronze);
            int ptx = (int)(p.X / 16), pty = (int)(p.Y / 16);
            int ox = ptx - MS / 2, oy = pty - MS / 2;
            for (int j = 0; j < MS; j++)
            {
                int ty = oy + j;
                for (int i = 0; i < MS; i++)
                {
                    int tx = ox + i;
                    int c = 0x08080C;
                    if (tx >= 0 && ty >= 0 && tx < m.TW && ty < m.TH && m.Seen[ty * m.TW + tx]) c = (m.Mini[ty * m.TW + tx] & 0xFFFFFF) | unchecked((int)0xFF000000);
                    miniBuf[j * MS + i] = c;
                }
            }
            g.Fill(mx, my, MS, MS, 0x08080C);
            if (g.S > 1) { HiArt.Epx2(miniBuf, MS, MS, miniBig); HiArt.SoftenAlpha(miniBig, MS * 2, MS * 2); g.ImageScaled(miniBig, MS * 2, MS * 2, mx, my, MS, MS, true); }
            else g.ImageScaled(miniBuf, MS, MS, mx, my, MS, MS, true);
            g.SetClip(mx, my, MS, MS);
            for (int i = 0; i < m.Props.Count; i++)
            {
                Prop q = m.Props[i];
                if (q.Kind != PropKind.Shrine) continue;
                int x = mx + (int)(q.X / 16) - ox, y = my + (int)(q.Y / 16) - oy;
                if (x < mx || y < my || x >= mx + MS || y >= my + MS) continue;
                if (!m.Seen[M.Clamp((int)(q.Y / 16), 0, m.TH - 1) * m.TW + M.Clamp((int)(q.X / 16), 0, m.TW - 1)]) continue;
                MiniDot(g, x + 0.5f, y + 0.5f, 1.6f, Flags.Contains(q.Id) ? 0x7FE0D8 : 0x4A6A70);
            }
            for (int i = 0; i < Npcs.Count; i++) MiniDot(g, mx + Npcs[i].X / 16 - ox, my + Npcs[i].Y / 16 - oy, 1.2f, 0xF0C060);
            for (int i = 0; i < Foes.Count; i++)
            {
                Enemy e = Foes[i];
                if (e.Dead || !e.Aggro) continue;
                MiniDot(g, mx + e.X / 16 - ox, my + e.Y / 16 - oy, 1.2f, 0xE0584E);
            }
            for (int i = 0; i < Others.Count; i++) MiniDot(g, mx + Others[i].X / 16 - ox, my + Others[i].Y / 16 - oy, 1.6f, 0x7FE0D8);
            g.ResetClip();
            // quest target (clamped to the frame edge when out of range)
            Quest tq = Q.Get(Q.Tracked);
            if (tq == null || !tq.Active) tq = Q.Get("bell");
            float qx, qy;
            if (Q.Target(this, tq, out qx, out qy))
            {
                int x = M.Clamp(mx + (int)(qx / 16) - ox, mx + 1, mx + MS - 2), y = M.Clamp(my + (int)(qy / 16) - oy, my + 1, my + MS - 2);
                if (((int)(UiT * 3) & 1) == 0) { g.Fill(x - 1, y - 2, 3, 5, CInk); g.Fill(x - 2, y - 1, 5, 3, CInk); g.Fill(x, y - 1, 1, 3, CGoldHi); g.Fill(x - 1, y, 3, 1, CGoldHi); }
            }
            int cx = mx + MS / 2, cy = my + MS / 2;
            g.Fill(cx - 1, cy - 1, 3, 3, CInk); g.Fill(cx, cy, 1, 1, 0xFFFFFF); g.Fill(cx - 1, cy, 3, 1, 0xFFFFFF); g.Fill(cx, cy - 1, 1, 3, 0xFFFFFF);
            // zone name and purse
            string zn = Zone.Name;
            g.TextRight(Fonts.Small, zn, W - 8, my + MS + 5, CText, 0x000000);
            string gold = p.Gold.ToString();
            g.TextRight(Fonts.Small, gold, W - 8, my + MS + 17, 0xF0C060, 0x000000);
            g.Blit(Assets.Coin[0], W - 14 - Fonts.Small.Width(gold), my + MS + 27);
            if (Hot(mx, my, MS, MS)) Tip("Map", "Press ^yM^0 for the full map.");
        }

        void DrawBanners()
        {
            Surface g = Final;
            // zone name
            if (ZoneBannerT < 4.2f && ZoneBannerName.Length > 0 && BossIntroT > 3.5f && (P == null || !P.Dead))
            {
                float a = Math.Min(1f, ZoneBannerT * 2f) * M.Clamp01((4.2f - ZoneBannerT) * 1.5f);
                PixFont bf = Fonts.Big;
                int tw = bf.Width(ZoneBannerName), y = 100;        // between the quest banner (54-90) and the notice feed (from 160)
                g.FillA(W / 2 - tw / 2 - 50, y - 4, tw + 100, ZoneBannerSub.Length > 0 ? 52 : 40, CInk, (int)(120 * a));      // the subtitle sits inside it too
                if (a > 0.45f)
                {
                    g.TextCentered(bf, ZoneBannerName, W / 2, y, CGoldHi, 0x000000);
                    int ly = y + bf.LineH + 1;
                    g.Fill(W / 2 - tw / 2 - 20, ly, tw + 40, 1, CBronze); g.Fill(W / 2 - 2, ly - 1, 5, 3, CGold);
                    if (ZoneBannerSub.Length > 0) g.TextCentered(Fonts.Small, ZoneBannerSub, W / 2, ly + 4, CText, 0x000000);
                }
            }
            // boss / elite bar
            Enemy b = Boss;
            if (b != null && !b.Dead && b.Aggro)
            {
                int bw = 280, bx = W / 2 - bw / 2, by = 30;
                g.TextCentered(Fonts.BodyBold, b.Name, W / 2, by - 15, 0xF2ECD8, 0x000000);
                g.Fill(bx - 2, by - 2, bw + 4, 12, CInk); g.Frame(bx - 2, by - 2, bw + 4, 12, CBronze);
                g.Fill(bx, by, bw, 8, 0x2A1018);
                int fw = (int)Math.Ceiling(bw * M.Clamp01(b.HP / b.MaxHP));
                bool prior = b.Kind == EnemyKind.Prior;
                g.GradV(bx, by, fw, 8, prior ? 0xA7A0DC : 0xE0584E, prior ? 0x3A2E5E : 0x6E1730);
                g.HLine(bx, by, fw, prior ? 0xEAF3FF : 0xF08A7A);
                float[] marks = prior ? new[] { 0.66f, 0.33f } : new[] { 0.6f, 0.35f };
                foreach (float mk in marks) g.Fill(bx + (int)(bw * mk), by - 1, 1, 10, CGoldHi);
                if (b.FlashT > 0) g.FillA(bx, by, fw, 8, 0xFFFFFF, 120);
            }
            if (BossFight && BossIntroT < 3.2f && Boss != null)
            {
                float a = Math.Min(1f, BossIntroT * 2f) * M.Clamp01((3.2f - BossIntroT) * 2f);
                g.FillA(0, 118, W, 62, CInk, (int)(200 * a));
                g.FillA(0, 118, W, 1, 0x7F6CB6, (int)(255 * a)); g.FillA(0, 179, W, 1, 0x7F6CB6, (int)(255 * a));
                if (a > 0.4f)
                {
                    g.TextCentered(Fonts.Big, "THE SILENT PRIOR", W / 2, 126, 0xC9D3F5, 0x000000);
                    g.TextCentered(Fonts.Body, "keeper of the unrung bell", W / 2, 158, 0x8A7FC0, 0x000000);
                }
            }
            if (LevelUpT < 2.2f)
            {
                float a = Math.Min(1f, LevelUpT * 4f) * M.Clamp01((2.2f - LevelUpT) * 2f);
                if (a > 0.4f) g.TextCentered(Fonts.Big, Loc.F("LEVEL {0}", P.Level), W / 2, 222 - (int)(LevelUpT * 6), CGoldHi, 0x000000);
            }
        }
    }
}
