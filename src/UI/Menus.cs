using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>Loading, title, class selection, pause, settings, dialogue, reading, death and ending screens.</summary>
    public sealed partial class Game
    {
        float titleCam; int menuSel, csSel; bool titleSettings, titleJoin, hasSave; float titleDim;
        string joinName = "", joinAddr = "";
        // slow pans across the vale behind the title: x0,y0 -> x1,y1 in tiles
        static readonly float[] Vistas =
        {
            88, 12, 88, 21,
            80, 63, 96, 66,
            23, 41, 33, 45,
            134, 56, 146, 60,
            84, 108, 93, 112,
            152, 70, 164, 74,
        };

        // loading
        void DrawLoading()
        {
            Surface g = Final;
            g.Clear(0x08070E);
            for (int i = 0; i < 70; i++)
            {
                uint h = Noise.Hash(i, 7, 3);
                int x = (int)(h % W), y = (int)((h >> 10) % 200);
                int tw = (int)((h >> 20) & 3);
                if (((int)(UiT * 2 + i) & 3) != tw) g.Pixel(x, y, 0x3A2E5E + (tw == 0 ? 0x303030 : 0));
            }
            for (int k = 0; k < 40; k++) g.FillA(0, H - 40 + k, W, 1, 0x231C3A, k * 3);
            G = g;
            if (Fonts.Huge != null)
            {
                DrawLogo(W / 2 - LogoWidth() / 2, 86);
                int bw = 260, bx = W / 2 - bw / 2, by = 214;
                g.Fill(bx - 2, by - 2, bw + 4, 9, CInk); g.Frame(bx - 2, by - 2, bw + 4, 9, CBronze);
                g.Fill(bx, by, bw, 5, 0x1A1626);
                int fw = (int)(bw * M.Clamp01(LoadProgress));
                if (fw > 0) g.GradV(bx, by, fw, 5, CGoldHi, CBronze);
                g.TextCentered(Fonts.Small, Loc.Tr(LoadLabel) + "...", W / 2, by + 12, CDim, 0x000000);
                List<string> tip = Fonts.Body.Wrap(Lore.Tips[LoadTip % Lore.Tips.Length], 380);
                int ty = 262;
                foreach (string s in tip) { g.TextCentered(Fonts.Body, s, W / 2, ty, CText, 0x000000); ty += Fonts.Body.LineH + 1; }
            }
        }

        // logo
        void GradText(PixFont f, string s, int x, int y, int top, int bottom, int shadow)
        {
            G.Text(f, s, x + 2, y + 2, shadow, -1);
            G.Text(f, s, x + 1, y + 1, shadow, -1);
            int h = f.LineH; const int bands = 7;
            for (int b = 0; b < bands; b++)
            {
                int ya = y + b * h / bands, yb = y + (b + 1) * h / bands;
                G.SetClip(0, ya, W, yb - ya);
                G.Text(f, s, x, y, Col.Lerp(top | Col.Opaque, bottom | Col.Opaque, b / (bands - 1f)) & 0xFFFFFF, -1);
            }
            G.ResetClip();
        }

        int LogoWidth() { return Fonts.Huge.Width("Vesperfell"); }

        void DrawLogo(int x, int y)
        {
            PixFont hf = Fonts.Huge;
            int w = LogoWidth();
            GradText(hf, "Vesperfell", x, y, 0xFFF4C8, 0xB07A2C, 0x08070E);
            int ly = y + hf.LineH + 3;
            G.Fill(x + 4, ly, w - 8, 1, CBronze);
            G.Fill(x + w / 2 - 3, ly - 2, 7, 5, CInk); G.Fill(x + w / 2 - 2, ly - 1, 5, 3, CGold); G.Fill(x + w / 2, ly, 1, 1, CInk);
            string sub = "T H E   S I L E N T   B E L L";
            G.Text(Fonts.Small, sub, x + w / 2 - Fonts.Small.Width(sub) / 2, ly + 6, 0xA7A0DC, 0x000000);
        }

        // title
        void AmbientToward(int zone, float k)
        {
            ZoneDef d = Zones.All[zone];
            AmbR = M.Lerp(AmbR, d.R, k); AmbG = M.Lerp(AmbG, d.G, k); AmbB = M.Lerp(AmbB, d.B, k); FogD = M.Lerp(FogD, d.Fog, k);
            FogCol = Col.Lerp(FogCol | Col.Opaque, d.FogColor | Col.Opaque, k) & 0xFFFFFF;
        }

        void UpdateTitleCamera(float dt)
        {
            titleCam += dt;
            const float seg = 13f;
            int n = Vistas.Length / 4;
            int i = (int)(titleCam / seg) % n;
            float t = (titleCam % seg) / seg;
            float x = M.Lerp(Vistas[i * 4], Vistas[i * 4 + 2], t) * 16, y = M.Lerp(Vistas[i * 4 + 1], Vistas[i * 4 + 3], t) * 16;
            CamX = x - W / 2; CamY = y - H / 2;
            float maxX = Cur.PW - W, maxY = Cur.PH - H;
            CamX = M.Clamp(CamX, 0, maxX); CamY = M.Clamp(CamY, 0, maxY);
            CamXi = (int)CamX; CamYi = (int)CamY;
            if (R3 != null) { R3.TitleMode = true; R3.UpdateCamera(dt); }
            float edge = Math.Min(t, 1f - t) * seg;
            titleDim = 1f - M.Clamp01(edge / 0.9f);
            int z = Cur.ZoneAt(x, y);
            AmbientToward(z, edge < 0.2f ? 1f : M.Damp(2f, dt));
            ZoneId = z;
        }

        void UpdateTitle(float dt)
        {
            if (Cur == null) return;
            UpdateTitleCamera(dt);
            UpdateAmbientTitle(dt);
            if (!titleSettings && !titleJoin)
            {
                int count = hasSave ? 5 : 4;
                if (In.Take(Key.Up) || In.Take(Key.W)) { menuSel = (menuSel + count - 1) % count; Snd(Sfx.UiHover, 0.4f, 1f); }
                if (In.Take(Key.Down) || In.Take(Key.S)) { menuSel = (menuSel + 1) % count; Snd(Sfx.UiHover, 0.4f, 1f); }
            }
            else if (In.Take(Key.Esc))
            {
                if (titleJoin) { if (JoinState != JoinIdle) NetStop(); titleJoin = false; }
                else { titleSettings = false; Cfg.PlayerName = CleanName(Cfg.PlayerName); SaveSettings(); }
            }
            UiLogic(TitleUi);
        }

        void UpdateAmbientTitle(float dt)
        {
            UpdateParticles(dt);
            ambAcc += dt;
            if (ambAcc > 0.12f && PartCount < 200)
            {
                ambAcc = 0;
                Particle p = new Particle();
                p.X = CamXi + Rnd.F() * W; p.Y = CamYi + Rnd.F() * H; p.Z = 6 + Rnd.F() * 30; p.VX = Rnd.Sym(8); p.VY = Rnd.Sym(4); p.VZ = Rnd.Sym(4);
                p.Life = p.Max = 3 + Rnd.F() * 3; p.Color = 0xDCFA9A; p.Color2 = 0x6FA82E; p.Kind = PGlow; p.Size = 2.4f;
                AddPart(p);
            }
        }

        void DrawTitle() { UiDraw(TitleUi); }

        void TitleUi()
        {
            for (int k = 0; k < 14; k++) G.FillA(0, 0, 330 - k * 22, H, 0x05050A, 17);
            G.FillA(0, 0, W, H, 0x05050A, 40 + (int)(215 * titleDim));
            G.Fill(0, 0, W, 10, 0x05050A); G.Fill(0, H - 10, W, 10, 0x05050A);
            if (titleSettings) { SettingsUi(); return; }
            if (titleJoin) { JoinUi(); return; }
            DrawLogo(34, 54);
            string[] items = hasSave ? new[] { "Continue", "New Game", "Join Game", "Settings", "Quit" } : new[] { "New Game", "Join Game", "Settings", "Quit" };
            int choice = -1;
            for (int i = 0; i < items.Length; i++)
            {
                int bx = 46, by = 166 + i * 25, bw = 150, bh = 21;
                if (uiInput && Hot(bx, by, bw, bh) && menuSel != i && (In.MX != lastMX || In.MY != lastMY)) menuSel = i;
                if (menuSel == i) for (int k = 0; k < 4; k++) { G.Fill(bx - 12 + k, by + 7 + k, 1, 7 - k * 2, CGoldHi); }
                if (Button(bx, by, bw, bh, items[i], true)) choice = i;
                if (uiInput && menuSel == i && (In.Take(Key.Enter) || In.Take(Key.Space))) { choice = i; Snd(Sfx.UiClick, 0.7f, 1f); }
            }
            if (uiInput) { lastMX = In.MX; lastMY = In.MY; }
            G.Text(Fonts.Small, "A dark-fantasy action RPG demo", 46, H - 44, CDim, 0x000000);
            G.Text(Fonts.Small, R3 != null && Cfg.Camera != "top" ? "WASD move  -  mouse look  -  F interact  -  F11 fullscreen" : "WASD move  -  mouse aim  -  F interact  -  F11 fullscreen", 46, H - 31, 0x6A6A7A, 0x000000);
            G.TextRight(Fonts.Small, "v2.0", W - 12, H - 31, 0x6A6A7A, 0x000000);
            if (GfxNote.Length > 0) G.TextRight(Fonts.Small, GfxNote, W - 12, 14, 0x8A7FC0, 0x000000);
            if (hasSave && menuSel == 1) G.Text(Fonts.Small, "A new journey replaces the saved one at its first wayshrine.", 46, 166 + items.Length * 25 + 3, 0x8A7FC0, 0x000000);
            // language: one click, right on the title screen
            if (ButtonId("lang", W - 86, H - 52, 74, 16, Loc.Raw(Loc.Lang == Loc.RU ? "English" : "\u0420\u0443\u0441\u0441\u043a\u0438\u0439"), true)) SetLanguage(Loc.Lang == Loc.RU ? "en" : "ru");
            if (choice < 0) return;
            string c = items[choice];
            if (c == "Continue") LoadGame();
            else if (c == "New Game") { Scr = Screen.ClassSelect; csSel = 0; }
            else if (c == "Join Game") { titleJoin = true; joinName = Cfg.PlayerName; joinAddr = Cfg.LastServer; JoinError = ""; focusField = "joinaddr"; }
            else if (c == "Settings") titleSettings = true;
            else if (c == "Quit") WantQuit = true;
        }

        public void SetLanguage(string code)
        {
            Cfg.Language = code == "ru" ? "ru" : "en";
            Loc.Set(Cfg.Language);
            SaveSettings();
        }

        // joining a friend's game
        void JoinUi()
        {
            PixFont fs = Fonts.Small;
            int pw = 340, ph = 212, px = W / 2 - pw / 2, py = 70;
            Panel(px, py, pw, ph);
            Header(px, py + 9, pw, "Join a Friend's Game", Fonts.Title);
            int lx = px + 22, y = py + 40;
            bool busy = JoinState == JoinConnecting;
            G.Text(fs, "Your name", lx, y + 2, CText, 0x000000);
            TextField("joinname", lx + 104, y, 192, ref joinName, 14);
            y += 24;
            G.Text(fs, "Host's address", lx, y + 2, CText, 0x000000);
            TextField("joinaddr", lx + 104, y, 192, ref joinAddr, 60);
            y += 26;
            y = G.TextWrapped(fs, "The host opens their game from the pause menu (Esc, then Host Co-op) and tells you the address shown there.", lx, y, pw - 44, CDim, 0x000000, 0) + 6;
            if (busy) G.Text(fs, "Knocking at the door...", lx, y, CGoldHi, 0x000000);
            else if (JoinError.Length > 0) G.TextWrapped(fs, "^r" + Loc.Tr(JoinError) + "^0", lx, y, pw - 44, CText, 0x000000, 0);
            if (Button(px + 22, py + ph - 32, 90, 20, "Back", true)) { if (JoinState != JoinIdle) NetStop(); titleJoin = false; return; }
            bool go = Button(px + pw - 152, py + ph - 34, 130, 23, "Connect", !busy);
            if (uiInput && !busy && In.Take(Key.Enter)) { go = true; Snd(Sfx.UiClick, 0.7f, 1f); }
            if (go)
            {
                Cfg.PlayerName = CleanName(joinName); joinName = Cfg.PlayerName;
                NetJoin(joinAddr);
            }
        }
        int lastMX, lastMY;

        // class selection
        void UpdateClassSelect(float dt)
        {
            UpdateTitleCamera(dt);
            UpdateAmbientTitle(dt);
            if (In.Take(Key.Esc))
            {
                if (JoinState != JoinIdle) { NetStop(); titleJoin = true; }
                Scr = Screen.Title; Snd(Sfx.UiClose, 0.6f, 1f); return;
            }
            if (In.Take(Key.Up) || In.Take(Key.W) || In.Take(Key.Left) || In.Take(Key.A)) { csSel = (csSel + 3) % 4; Snd(Sfx.UiTab, 0.6f, 1f); }
            if (In.Take(Key.Down) || In.Take(Key.S) || In.Take(Key.Right) || In.Take(Key.D)) { csSel = (csSel + 1) % 4; Snd(Sfx.UiTab, 0.6f, 1f); }
            UiLogic(ClassSelectUi);
        }

        void DrawClassSelect() { UiDraw(ClassSelectUi); }

        static readonly string[] BarNames = { "Health", "Power", "Defence", "Speed", "Range" };

        void ClassSelectUi()
        {
            PixFont fs = Fonts.Small;
            G.FillA(0, 0, W, H, 0x05050A, 165);
            Header(120, 10, 400, "Choose Your Calling", Fonts.Title);
            bool joining = JoinState >= JoinLobby;
            if (joining) G.TextCentered(fs, Loc.F("Joining {0}'s vigil  -  level {1}  -  {2} in the party", Loc.Raw(HostName).Substring(1), HostLevel, HostPlayers), 130, 326, 0x7FE0D8, 0x000000);
            // class list
            for (int i = 0; i < 4; i++)
            {
                ClassDef d = Classes.All[i];
                int x = 16, y = 38 + i * 70, w = 228, h = 64;
                bool sel = csSel == i, hot = Hot(x, y, w, h);
                Remember("class:" + d.Name, x, y, w, h);
                if (hot) hotNow = "class" + i;
                G.FillA(x + 2, y + 3, w, h, 0x000000, 90);
                G.GradV(x, y, w, h, sel ? 0x2E2843 : (hot ? 0x241E36 : 0x1D1930), 0x15121F);
                G.Frame(x, y, w, h, CInk); G.Frame(x + 1, y + 1, w - 2, h - 2, sel ? CGoldHi : (hot ? CGold : CBronze));
                if (sel) G.Fill(x + 1, y + 1, 3, h - 2, d.Accent);
                G.Fill(x + 8, y + 6, 46, 52, CInk); G.GradV(x + 9, y + 7, 44, 50, 0x241E36, 0x0E0C16);
                G.Ellipse(x + 31, y + 50, 16, 4, d.Accent, 70);
                G.SetClip(x + 9, y + 7, 44, 50);
                G.BlitScaled(Assets.Hero[i].Idle[sel ? ((int)(UiT * 1.6f) & 1) : 0], x + 9, y + 1, 2, false);
                G.ResetClip();
                G.Text(Fonts.Title, d.Name, x + 62, y + 5, sel ? d.Accent : CText, 0x000000);
                G.Text(fs, d.Title, x + 62, y + 25, CDim, 0);
                List<string> bl = fs.Wrap(d.Tag, w - 70);
                for (int k = 0; k < bl.Count && k < 2; k++) G.Text(fs, bl[k], x + 62, y + 37 + k * 11, sel ? CText : 0x9A96A8, 0);
                if (Clicked(x, y, w, h, 0) && !sel) { csSel = i; Snd(Sfx.UiTab, 0.7f, 1f); }
            }
            // detail panel
            ClassDef c = Classes.All[csSel];
            int px = 256, py = 36, pw = 370, ph = 284;
            Panel(px, py, pw, ph);
            int ax = px + 12, ay = py + 12;
            G.Fill(ax, ay, 104, 136, CInk); G.GradV(ax + 1, ay + 1, 102, 134, 0x2E2843, 0x0E0C16);
            G.Glow(ax + 52, ay + 70, 60, c.Accent, 46);
            G.Ellipse(ax + 52, ay + 122, 34, 8, c.Accent, 80);
            G.Frame(ax, ay, 104, 136, CGold);
            G.BlitScaled(Assets.Hero[csSel].Idle[(int)(UiT * 1.6f) & 1], ax + 8, ay + 10, 4, false);
            // name and lore
            int tx = ax + 116;
            G.Text(Fonts.Big, c.Name, tx, ay - 4, c.Accent, 0x000000);
            G.Text(Fonts.BodyBold, c.Title, tx, ay + 26, CGoldHi, 0x000000);
            int ly = G.TextWrapped(fs, c.Blurb, tx, ay + 42, 236, CText, 0x000000, 0) + 5;
            // stat pips
            for (int i = 0; i < 5; i++)
            {
                int ry = ly + i * 12;
                G.Text(fs, BarNames[i], tx, ry, CDim, 0);
                for (int k = 0; k < 5; k++)
                {
                    int bx = tx + 58 + k * 14;
                    G.Fill(bx, ry + 2, 12, 7, CInk);
                    G.Fill(bx + 1, ry + 3, 10, 5, k < c.Bars[i] ? c.Accent : 0x241E36);
                    if (k < c.Bars[i]) G.Fill(bx + 1, ry + 3, 10, 1, Col.Lerp(c.Accent | Col.Opaque, Col.Opaque | 0xFFFFFF, 0.4f) & 0xFFFFFF);
                }
            }
            G.Text(fs, "Resource", tx + 138, ly, CDim, 0);
            G.Text(Fonts.BodyBold, c.ResName, tx + 138, ly + 11, c.ResColor, 0x000000);
            G.Text(fs, "Dodges", tx + 138, ly + 28, CDim, 0);
            G.Text(Fonts.BodyBold, c.DodgeCharges.ToString(), tx + 138, ly + 39, CWhite, 0x000000);
            // abilities
            int by2 = ay + 144;
            Header(px + 8, by2, pw - 16, "Abilities", Fonts.BodyBold);
            by2 += 18;
            for (int i = 0; i < 5; i++)
            {
                AbilityDef ab = c.Abilities[i];
                int sx = ax + i * 70, sy = by2;
                bool hot = Hot(sx, sy, 68, 40);
                G.Fill(sx, sy, 24, 24, CInk); G.Fill(sx + 1, sy + 1, 22, 22, CSlot);
                G.Blit(Assets.Skill[ab.Icon], sx + 3, sy + 3);
                G.Frame(sx, sy, 24, 24, hot ? CGoldHi : CBronze);
                G.Text(fs, SlotKeys[i], sx + 28, sy, CGold, 0);
                if (ab.UnlockLevel > 1) G.Text(fs, Loc.F("Lv {0}", ab.UnlockLevel), sx + 28, sy + 11, CDim, 0);
                List<string> nm = fs.Wrap(ab.Name, 66);
                for (int k = 0; k < nm.Count && k < 2; k++) G.Text(fs, nm[k], sx, sy + 26 + k * 10, hot ? CWhite : CText, 0);
                if (hot)
                {
                    List<string> t = new List<string>();
                    t.Add("^y" + Loc.Tr(ab.Name) + "^0  ^d[" + Loc.Tr(ab.Key) + "]^0");
                    string meta = "";
                    if (ab.Cost > 0) meta += ab.Cost + " " + Loc.Tr(c.ResName);
                    if (ab.Cooldown > 0) meta += (meta.Length > 0 ? "   " : "") + Loc.F("{0}s cooldown", ab.Cooldown.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
                    if (meta.Length > 0) t.Add("^c" + meta + "^0");
                    t.Add(ab.Desc);
                    if (ab.UnlockLevel > 1) t.Add(Loc.F("^dLearned at level {0}.^0", ab.UnlockLevel));
                    Tip(t, 200);
                }
            }
            // starting gear and passive
            int gy = by2 + 50;
            Item w0 = Items.Starter(c.Id, true), a0 = Items.Starter(c.Id, false);
            int kitX = Math.Max(ax + 62, G.Text(fs, "Begins with", ax, gy + 5, CDim, 0) + 5);     // the label is longer in Russian
            Slot("startw", kitX, gy, w0, false); Slot("starta", kitX + 24, gy, a0, false);
            if (Hot(kitX, gy, 20, 20)) Tip(Items.Describe(w0, null, false, null), 200);
            if (Hot(kitX + 24, gy, 20, 20)) Tip(Items.Describe(a0, null, false, null), 200);
            List<string> pl = fs.Wrap(c.Passive, 230 - (kitX - ax - 62));
            for (int k = 0; k < pl.Count && k < 2; k++) G.Text(fs, pl[k], kitX + 54, gy + k * 11, 0xA7A0DC, 0);

            if (Button(px + 12, py + ph - 30, 84, 20, "Back", true))
            {
                if (JoinState != JoinIdle) { NetStop(); titleJoin = true; }
                Scr = Screen.Title; return;
            }
            bool entering = JoinState == JoinEntering;
            bool go = Button(px + pw - 152, py + ph - 32, 140, 23, entering ? "Entering..." : (joining ? "Join the Vigil" : "Begin the Vigil"), !entering);
            if (uiInput && !entering && In.Take(Key.Enter)) { go = true; Snd(Sfx.UiClick, 0.7f, 1f); }
            if (go) { if (joining) NetEnter(c.Id); else StartNewGame(c.Id); }
        }

        // pause
        void PauseUi()
        {
            G.FillA(0, 0, W, H, 0x05050A, 140);
            int rows = IsClient ? 4 : 6;
            int pw = 216, ph = 76 + rows * 25 + (IsHost ? 46 : 0), px = W / 2 - pw / 2, py = Math.Max(16, 160 - ph / 2);
            Panel(px, py, pw, ph);
            Header(px, py + 10, pw, Online ? "The Party" : "Paused", Fonts.Title);
            int by = py + 40, bw = 176, bx = px + 20;
            if (Button(bx, by, bw, 20, "Resume", true)) { CloseOverlay(); return; }
            by += 25;
            if (!IsClient)
            {
                if (Button(bx, by, bw, 20, "Save Game", !P.Dead)) { SaveGame(); Notify("^gProgress saved.^0 You will return at your last wayshrine."); CloseOverlay(); return; }
                by += 25;
            }
            if (Button(bx, by, bw, 20, "Settings", true)) { settingsReturn = Overlay.Pause; Ov = Overlay.Settings; return; }
            by += 25;
            if (IsClient)
            {
                if (Button(bx, by, bw, 20, "Leave the Party", true)) { FadeThrough(GoTitle); return; }
                by += 25;
            }
            else
            {
                if (!IsHost)
                {
                    if (Button(bx, by, bw, 20, "Host Co-op", !P.Dead)) { if (NetHost()) ShowHostInfo(); return; }
                }
                else if (Button(bx, by, bw, 20, "Stop Hosting", true))
                {
                    NetStop(); Notify("^yThe party is disbanded.^0 The vigil is yours alone again."); CloseOverlay(); return;
                }
                by += 25;
                if (Button(bx, by, bw, 20, "Save and Exit to Title", true)) { if (!P.Dead) SaveGame(); FadeThrough(GoTitle); return; }
                by += 25;
            }
            if (Button(bx, by, bw, 20, "Quit to Desktop", true)) { if (!P.Dead) SaveGame(); WantQuit = true; return; }
            by += 28;
            if (IsHost)
            {
                G.TextCentered(Fonts.Small, Loc.F("In the party: {0} of {1}. Your address:", Party.Count, MaxParty), W / 2, by, CText, 0x000000);
                string addr = HostAddresses.Count > 0 ? HostAddresses[0] : "127.0.0.1";
                if (HostAddresses.Count > 1) addr += "   " + HostAddresses[1];
                G.TextCentered(Fonts.Small, Loc.Raw(addr), W / 2, by + 13, CGoldHi, 0x000000);
                if (ButtonId("hostinfo", bx + 18, by + 27, bw - 36, 15, "How friends join", true)) { ShowHostInfo(); return; }
            }
            int mins = (int)(PlayTime / 60);
            G.TextCentered(Fonts.Small, Loc.F("{0}  -  level {1}  -  {2}h {3}m", Loc.Tr(Zone.Name), P.Level, mins / 60, (mins % 60).ToString("00")), W / 2, py + ph - 18, CDim, 0x000000);
        }

        /// <summary>Tells the host what to pass on to their friends.</summary>
        public void ShowHostInfo()
        {
            ShowText("The Vigil is Open", Loc.F("Friends join from the title screen: ^yJoin Game^0, then one of these addresses.\n\n{0}\n\nOn the same home network, use the address that begins with 192.168 or 10. Over the internet, either forward TCP port {1} on your router, or set up a virtual network with your friends (Radmin VPN, ZeroTier, Tailscale) and use its address (Radmin VPN addresses begin with 26).\n\nIf Windows Firewall asks, allow Vesperfell.", HostAddressText(), Cfg.Port));
        }

        // settings
        static readonly string[] ControlList =
        {
            "W A S D", "Move",
            "Mouse", "Look and aim",
            "Left mouse", "Basic attack (hold)",
            "Right mouse", "Class secondary",
            "Q / E / R", "Abilities",
            "Space", "Dodge roll",
            "F", "Interact / talk",
            "1 / 2", "Health / Spirit draught",
            "I  or  Tab", "Inventory",
            "C", "Character",
            "K", "Skills",
            "J", "Journal",
            "M", "Map",
            "Esc", "Pause / close",
            "F11", "Toggle fullscreen",
            "Arrows", "Turn",
        };

        void SettingsUi()
        {
            if (Scr == Screen.Play) G.FillA(0, 0, W, H, 0x05050A, 140);
            int pw = 440, ph = 350, px = W / 2 - pw / 2, py = 5;
            Panel(px, py, pw, ph);
            Header(px, py + 9, pw, "Settings", Fonts.Title);
            PixFont fs = Fonts.Small;
            int lx = px + 18, y = py + 36;
            G.Text(Fonts.BodyBold, "Sound", lx, y, CGold, 0x000000); y += 17;
            bool ch = false;
            ch |= Slider(lx, y, 200, "Master", ref Cfg.Master); y += 16;
            ch |= Slider(lx, y, 200, "Music", ref Cfg.Music); y += 16;
            bool sfxCh = Slider(lx, y, 200, "Effects", ref Cfg.Sfx); y += 16;
            ch |= Slider(lx, y, 200, "Ambience", ref Cfg.Amb); y += 21;
            if (sfxCh) { ch = true; ApplyAudioVolumes(); Snd(Sfx.Coin, 0.8f, 1f); }
            if (ch) ApplyAudioVolumes();
            G.Text(Fonts.BodyBold, "Display", lx, y, CGold, 0x000000); y += 17;
            if (Check(lx, y, "Fullscreen", ref Cfg.Fullscreen)) WantApplyDisplay = true;
            y += 16;
            G.Text(fs, "Window size", lx, y, Cfg.Fullscreen ? 0x5A5A6A : CText, 0);
            if (ButtonId("scale-", lx + 80, y - 2, 16, 15, "-", !Cfg.Fullscreen && Cfg.WindowScale > 1)) { Cfg.WindowScale--; WantApplyDisplay = true; }
            G.Text(fs, Cfg.WindowScale + "x", lx + 104, y, Cfg.Fullscreen ? 0x5A5A6A : CWhite, 0);
            if (ButtonId("scale+", lx + 124, y - 2, 16, 15, "+", !Cfg.Fullscreen && Cfg.WindowScale < 6)) { Cfg.WindowScale++; WantApplyDisplay = true; }
            G.Text(fs, (640 * Cfg.WindowScale) + " x " + (360 * Cfg.WindowScale), lx + 148, y, CDim, 0);
            y += 17;
            G.Text(fs, "Graphics", lx, y, CText, 0);
            bool want3d = Cfg.Renderer != "2d";
            if (ButtonId("gfx3d", lx + 80, y - 2, 34, 15, Loc.Raw("3D"), true) && !want3d) { Cfg.Renderer = "3d"; SaveSettings(); }
            if (ButtonId("gfx2d", lx + 118, y - 2, 34, 15, Loc.Raw("2D"), true) && want3d) { Cfg.Renderer = "2d"; SaveSettings(); }
            G.Fill(lx + (want3d ? 82 : 120), y + 14, 30, 1, CGoldHi);
            if (want3d != (R3 != null)) G.Text(fs, "after restart", lx + 158, y, CGoldHi, 0);
            y += 17;
            // the 3D view: through the hero's eyes or from above (switches at once)
            bool can3d = R3 != null, fpCam = Cfg.Camera != "top";
            G.Text(fs, "Camera", lx, y, can3d ? CText : 0x5A5A6A, 0);
            if (ButtonId("camfp", lx + 80, y - 2, 62, 15, "First person", can3d) && !fpCam) { Cfg.Camera = "fp"; SaveSettings(); }
            if (ButtonId("camtop", lx + 146, y - 2, 62, 15, "Overhead", can3d) && fpCam) { Cfg.Camera = "top"; SaveSettings(); }
            if (can3d) G.Fill(lx + (fpCam ? 82 : 148), y + 14, 58, 1, CGoldHi);
            y += 19;
            SliderRange(lx, y, 208, "Mouse speed", ref Cfg.MouseSens, 0.2f, 3f);
            y += 18;
            G.Text(Fonts.BodyBold, "Game", lx, y, CGold, 0x000000); y += 17;
            Check(lx, y, "Screen shake", ref Cfg.Shake);
            Check(lx + 104, y, "Damage numbers", ref Cfg.DamageNumbers); y += 15;
            Check(lx, y, "Show frame rate", ref Cfg.ShowFps);
            Check(lx + 104, y, "Invert mouse", ref Cfg.InvertY); y += 19;
            G.Text(fs, "Language", lx, y + 1, CText, 0);
            bool ru = Loc.Lang == Loc.RU;
            if (ButtonId("lang_en", lx + 80, y - 1, 58, 15, Loc.Raw("English"), true) && ru) SetLanguage("en");
            if (ButtonId("lang_ru", lx + 142, y - 1, 58, 15, Loc.Raw("Русский"), true) && !ru) SetLanguage("ru");
            G.Fill(lx + (ru ? 144 : 82), y + 14, 54, 1, CGoldHi);
            y += 20;
            G.Text(fs, "Co-op name", lx, y + 2, CText, 0);
            TextField("cfgname", lx + 80, y, 120, ref Cfg.PlayerName, 14);

            int rx = px + 240, ry = py + 36;
            G.VLine(rx - 12, py + 34, ph - 74, CLine);
            G.Text(Fonts.BodyBold, "Controls", rx, ry, CGold, 0x000000); ry += 17;
            bool topView = R3 != null && Cfg.Camera == "top";
            for (int i = 0; i < ControlList.Length; i += 2)
            {
                string key = ControlList[i], what = ControlList[i + 1];
                // the overhead view aims with the cursor and zooms with the wheel instead
                if ((topView || R3 == null) && key == "Mouse") what = "Aim";
                if (key == "Arrows")
                {
                    if (topView) { key = "Wheel"; what = "Zoom (overhead)"; }
                    else if (R3 == null) continue;
                }
                G.Text(fs, key, rx, ry, CGoldHi, 0);
                G.Text(fs, what, rx + 70, ry, CText, 0);
                ry += 12;
            }
            ry += 4;
            G.Text(fs, "Enter", rx, ry, CGoldHi, 0);
            G.Text(fs, "Party chat (co-op)", rx + 70, ry, CText, 0);
            if (Button(px + pw / 2 - 50, py + ph - 28, 100, 20, "Done", true))
            {
                Cfg.PlayerName = CleanName(Cfg.PlayerName);
                if (Me != null && !Online) Me.NetName = Cfg.PlayerName;
                SaveSettings();
                if (Scr == Screen.Title) titleSettings = false; else Ov = settingsReturn;
            }
        }

        // reading
        void TextUi()
        {
            G.FillA(0, 0, W, H, 0x05050A, 110);
            PixFont f = Fonts.Body;
            int pw = 400;
            List<string> lines = f.Wrap(textBody, pw - 44);
            int ph = 58 + lines.Count * (f.LineH + 1);
            int px = W / 2 - pw / 2, py = Math.Max(16, H / 2 - ph / 2 - 10);
            Panel(px, py, pw, ph);
            Header(px, py + 10, pw, textTitle, Fonts.Title);
            int y = py + 36;
            foreach (string s in lines) { G.Text(f, s, px + 22, y, CText, 0x000000); y += f.LineH + 1; }
            if (ButtonId("textclose", px + pw / 2 - 40, py + ph - 20, 80, 15, "Close  [F]", true)) CloseOverlay();
        }

        // dialogue
        void DialogueUi()
        {
            if (Dlg == null) { if (uiInput) CloseDialogue(); return; }
            if (uiInput) DlgT += DT;
            PixFont f = Fonts.Body, fs = Fonts.Small;
            int pw = 520, px = W / 2 - pw / 2;
            int textW = pw - 86;
            List<string> lines = f.Wrap(Dlg.Text, textW);
            int total = 0; foreach (string s in lines) total += s.Length;
            int shown = (int)(DlgT * 90f);
            bool done = shown >= total;
            int ph = 34 + lines.Count * (f.LineH + 1) + 8 + Dlg.Choices.Count * 15 + 8;
            if (ph < 96) ph = 96;
            int py = H - ph - 10;
            Panel(px, py, pw, ph);
            // portrait
            int bx = px + 12, by = py + 12;
            G.Fill(bx, by, 50, 54, CInk); G.GradV(bx + 1, by + 1, 48, 52, 0x2E2843, 0x15121F); G.Frame(bx, by, 50, 54, CGold);
            if (DlgNpc != null && DlgNpc.Spr != null)
            {
                G.SetClip(bx + 1, by + 1, 48, 52);
                G.BlitScaled(DlgNpc.Spr.Idle[(int)(UiT * 1.6f) & 1], bx - 8, by - 6, 3, false);
                G.ResetClip();
            }
            int tx = px + 74, ty = py + 10;
            G.Text(Fonts.Title, Dlg.Speaker, tx, ty, CGoldHi, 0x000000);
            ty += 22;
            int left = shown;
            foreach (string s in lines)
            {
                if (left <= 0 && !done) break;
                string part = done || left >= s.Length ? s : SafePrefix(s, left);
                G.Text(f, part, tx, ty, CText, 0x000000);
                left -= s.Length; ty += f.LineH + 1;
            }
            ty = py + 34 + lines.Count * (f.LineH + 1) + 8;
            if (!done)
            {
                if (uiInput && (In.TakeMouse(0) || In.Take(Key.F) || In.Take(Key.Space) || In.Take(Key.Enter))) DlgT = 999;
                return;
            }
            for (int i = 0; i < Dlg.Choices.Count; i++)
            {
                DChoice c = Dlg.Choices[i];
                int cy = ty + i * 15;
                bool hot = Hot(tx - 4, cy - 1, textW + 8, 14);
                Remember("choice" + i, tx - 4, cy - 1, textW + 8, 14);
                if (hot) { hotNow = "choice" + i; G.Fill(tx - 4, cy - 1, textW + 8, 14, 0x2E2843); G.Frame(tx - 4, cy - 1, textW + 8, 14, CBronze); }
                G.Text(fs, (i + 1) + ".", tx, cy, CGold, 0);
                G.Text(fs, c.Text, tx + 14, cy, hot ? CGoldHi : CWhite, 0);
                bool pick = Clicked(tx - 4, cy - 1, textW + 8, 14, 0);
                if (uiInput && i < 5 && In.Take(Key.N1 + i)) pick = true;
                if (uiInput && Dlg.Choices.Count == 1 && (In.Take(Key.F) || In.Take(Key.Space) || In.Take(Key.Enter))) pick = true;
                if (pick)
                {
                    Snd(Sfx.UiClick, 0.6f, 1f);
                    c.Act();
                    return;
                }
            }
        }

        /// <summary>Cuts a string for the typewriter effect without splitting a colour code.</summary>
        static string SafePrefix(string s, int n)
        {
            if (n >= s.Length) return s;
            if (n > 0 && s[n - 1] == '^') n--;
            return s.Substring(0, Math.Max(0, n));
        }

        // death
        void DeathUi()
        {
            float t = M.Clamp01((DeathT - 1.4f) / 1.2f);
            if (uiInput) DeathT += DT;
            G.FillA(0, 0, W, H, 0x12060A, (int)(190 * t));
            if (t < 0.5f) return;
            GradText(Fonts.Big, "YOU HAVE FALLEN", W / 2 - Fonts.Big.Width("YOU HAVE FALLEN") / 2, 110, 0xF08A7A, 0x8F2F22, 0x000000);
            G.TextCentered(Fonts.Body, "The Gloam closes over you. Somewhere, a wayshrine remembers your name.", W / 2, 148, CText, 0x000000);
            if (t < 1f) return;
            bool party = Online && Others.Count > 0;
            bool rise = Button(W / 2 - 100, 184, 200, 22, party ? "Rise beside your companions" : "Rise at the last wayshrine", true);
            if (uiInput && !chatOpen && (In.Take(Key.Enter) || In.Take(Key.Space))) rise = true;
            if (rise) { Respawn(); return; }
            if (Button(W / 2 - 100, 212, 200, 20, IsClient ? "Leave the Party" : "Exit to Title", true)) FadeThrough(GoTitle);
            int lost = P.Gold / 10;
            if (lost > 0) G.TextCentered(Fonts.Small, Loc.F("The Gloam will keep {0} of your gold.", lost), W / 2, 240, CDim, 0x000000);
        }

        // ending
        void EndingUi()
        {
            if (uiInput) EndingT += DT;
            float t = M.Clamp01(EndingT / 1.5f);
            G.FillA(0, 0, W, H, 0x1A1408, (int)(150 * t));
            if (t < 0.6f) return;
            int pw = 430, ph = 232, px = W / 2 - pw / 2, py = 54;
            Panel(px, py, pw, ph);
            GradText(Fonts.Big, "The Bell Rings", W / 2 - Fonts.Big.Width("The Bell Rings") / 2, py + 12, 0xFFF4C8, 0xD2AC52, 0x000000);
            string body = "It rings out over the abbey roofs and the standing stones, over the Thornwood and the drowned fen, and wherever the sound goes the dusk draws back like a tide.\n\nFor the first time in thirty nights, there is an east.\n\n^yElder Maren will want to hear it from you.^0";
            int y = G.TextWrapped(Fonts.Body, body, px + 26, py + 50, pw - 52, CText, 0x000000, 1);
            int mins = (int)(PlayTime / 60);
            G.TextCentered(Fonts.Small, Loc.F("Level {0}   -   {1} foes slain   -   fallen {2} times   -   {3}h {4}m", P.Level, P.Kills, P.Deaths, mins / 60, (mins % 60).ToString("00")), W / 2, py + ph - 56, CDim, 0x000000);
            if (EndingT < 2.2f) return;
            if (Button(W / 2 - 150, py + ph - 34, 140, 22, "Step into the dawn", true) || (uiInput && In.Take(Key.Enter))) { CloseOverlay(); return; }
            if (Button(W / 2 + 10, py + ph - 34, 140, 22, "Exit to Title", true)) { SaveGame(); FadeThrough(GoTitle); }
        }
    }
}
