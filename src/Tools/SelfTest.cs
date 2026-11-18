using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Vesperfell
{
    /// <summary>Test harness: owns a headless game and feeds it simulated keyboard and mouse input.</summary>
    public sealed class Rig
    {
        public Game G; public readonly Input In = new Input();
        public string Dir; public int Fails, Passes;
        public Rig Partner;             // a second game that must keep running whenever this one steps (co-op tests)
        public Rig Parent;              // the rig whose log this one writes into
        readonly StringBuilder log = new StringBuilder();
        readonly Dictionary<int, int> pulses = new Dictionary<int, int>();
        string section = "";

        public Rig(string dir) : this(dir, "data", "en", null) { }

        public Rig(string dir, string dataName, string lang, Rig parent)
        {
            Dir = dir; Parent = parent;
            Directory.CreateDirectory(dir);
            string data = Path.Combine(dir, dataName);
            if (Directory.Exists(data)) Directory.Delete(data, true);
            Directory.CreateDirectory(data);
            G = new Game(In, true);
            G.DataDir = data;
            G.Rnd = new Rng(12345);
            G.ForceCursor = true;
            G.Cfg.Language = lang; Loc.Set(lang);
        }

        public void Section(string name) { if (Parent != null) { Parent.Section(name); return; } section = name; log.AppendLine(); log.AppendLine("== " + name + " =="); Flush(); }
        public void Note(string s) { if (Parent != null) { Parent.Note(s); return; } log.AppendLine("   " + s); }
        public void Check(bool ok, string what)
        {
            if (Parent != null) { Parent.Check(ok, "[guest] " + what); return; }
            if (ok) Passes++; else Fails++;
            log.AppendLine((ok ? " PASS  " : " FAIL  ") + what);
            if (!ok) Flush();
        }
        public void Flush() { if (Parent != null) return; try { File.WriteAllText(Path.Combine(Dir, "selftest.log"), log + "\r\n(running: " + section + ")\r\n"); } catch { } }
        public void Finish()
        {
            log.AppendLine(); log.AppendLine("TOTAL: " + Passes + " passed, " + Fails + " failed");
            File.WriteAllText(Path.Combine(Dir, "selftest.log"), log.ToString());
        }

        public void Step() { Step(1); }
        public void Step(int n)
        {
            for (int i = 0; i < n; i++)
            {
                StepSelf();
                if (Partner != null) { Partner.StepSelf(); System.Threading.Thread.Sleep(1); }     // let the network threads deliver
            }
        }

        void StepSelf()
        {
            if (pulses.Count > 0)
            {
                List<int> keys = new List<int>(pulses.Keys);
                foreach (int k in keys)
                {
                    int left = pulses[k] - 1;
                    if (left <= 0) { pulses.Remove(k); if (k >= 1000) In.SetMouse(k - 1000, false); else In.SetKey(k, false); }
                    else pulses[k] = left;
                }
            }
            G.Update(Game.DT);
            In.EndTick();
        }

        /// <summary>Steps until the condition holds (or the step budget runs out). Returns whether it did.</summary>
        public bool Until(int maxSteps, Func<bool> cond)
        {
            for (int i = 0; i < maxSteps; i++) { if (cond()) return true; Step(); }
            return cond();
        }
        public void Wait(float sec) { Step(Math.Max(1, (int)(sec * 60))); }
        /// <summary>Presses a key for a couple of ticks without blocking the script.</summary>
        public void Pulse(int key) { if (!pulses.ContainsKey(key)) { In.SetKey(key, true); pulses[key] = 3; } }
        public void PulseMouse(int b) { if (!pulses.ContainsKey(1000 + b)) { In.SetMouse(b, true); pulses[1000 + b] = 3; } }
        public void Tap(int key) { In.SetKey(key, true); Step(2); In.SetKey(key, false); Step(2); }
        public void Hold(int key, float sec) { In.SetKey(key, true); Wait(sec); In.SetKey(key, false); Step(1); }
        public void Click(int x, int y, int button) { In.MX = x; In.MY = y; Step(2); In.SetMouse(button, true); Step(2); In.SetMouse(button, false); Step(2); }
        public bool Has(string id) { return G.UiRects.ContainsKey(id); }
        public bool ClickUi(string id) { return ClickUi(id, 0); }
        public bool ClickUi(string id, int button)
        {
            int[] r;
            if (!G.UiRects.TryGetValue(id, out r)) { Check(false, "UI element present: " + id); return false; }
            Click(r[0] + r[2] / 2, r[1] + r[3] / 2, button);
            return true;
        }
        public void HoverUi(string id) { int[] r; if (G.UiRects.TryGetValue(id, out r)) { In.MX = r[0] + r[2] / 2; In.MY = r[1] + r[3] / 2; Step(2); } }
        public void Shot(string name) { G.Render(); G.Final.SavePng(Path.Combine(Dir, name + ".png"), 2); }
        public void AimWorld(float wx, float wy)
        {
            int sx, sy;
            if (G.R3 != null) G.UiAt(wx, wy, 12, out sx, out sy);
            else { sx = (int)wx - G.CamXi; sy = (int)wy - G.CamYi; }
            In.MX = M.Clamp(sx, 2, Game.W - 3); In.MY = M.Clamp(sy, 2, Game.H - 3);
        }
        public void ReleaseAll() { In.ReleaseAll(); pulses.Clear(); }
        public void Teleport(float tx, float ty) { G.DebugTeleport(tx * 16, ty * 16); Step(3); }
        public void TeleportPx(float x, float y) { G.DebugTeleport(x, y); Step(3); }
    }

    /// <summary>
    /// Headless automated playtest. Drives the game through simulated input, checks the results against game state,
    /// and writes screenshots plus a pass/fail log.
    /// </summary>
    public static class SelfTest
    {
        public static int Run(string dir, string which)
        {
            string lang = "en";
            if (which.StartsWith("ru:")) { lang = "ru"; which = which.Substring(3); Loc.Collect = true; }
            Rig r = new Rig(dir, "data", lang, null);
            try
            {
                Boot(r);
                if (which == "all" || which == "menu") Menus(r);
                if (which == "all" || which == "classes")
                    for (int c = 0; c < 4; c++) PlayClass(r, (ClassId)c, c == 0);
                if (which.StartsWith("class:")) PlayClass(r, (ClassId)int.Parse(which.Substring(6)), true);
                if (which == "all" || which == "story") Story(r);
                if (which == "all" || which == "perf") Perf(r);
                if (which == "all" || which == "audio") AudioTest(r);
                if (which == "all" || which == "net") NetTest(r);
            }
            catch (Exception ex)
            {
                r.Check(false, "EXCEPTION: " + ex);
                try { r.Shot("zz_exception"); } catch { }
            }
            if (Loc.Collect)
            {
                List<string> miss = new List<string>(Loc.Missing);
                miss.Sort(StringComparer.Ordinal);
                File.WriteAllLines(Path.Combine(dir, "missing_ru.txt"), miss.ToArray(), Encoding.UTF8);
                r.Note("untranslated strings seen: " + miss.Count);
            }
            r.Finish();
            return r.Fails == 0 ? 0 : 1;
        }

        // boot and menus
        static void Boot(Rig r)
        {
            r.Section("boot");
            Fonts.Build();
            Stopwatch sw = Stopwatch.StartNew();
            r.G.StartInitialLoad();
            r.G.Render();
            r.G.Final.SavePng(Path.Combine(r.Dir, "00_loading.png"), 2);
            for (int i = 0; i < 400 && r.G.Scr == Screen.Loading; i++) r.Step();
            r.Note("initial load took " + sw.ElapsedMilliseconds + " ms");
            r.Check(r.G.Scr == Screen.Title, "game reaches the title screen");
            r.Check(r.G.Over != null && r.G.Abbey != null, "both maps are generated");
            r.Wait(0.5f);
        }

        static void Menus(Rig r)
        {
            r.Section("main menu");
            r.In.MX = 400; r.In.MY = 300; r.Step(3);
            r.Shot("01_title");
            r.Check(r.Has("New Game") && r.Has("Settings") && r.Has("Quit"), "title menu shows New Game / Settings / Quit");
            r.Check(!r.Has("Continue"), "no Continue entry without a save file");
            r.Wait(14f); r.Shot("01b_title_vista2");
            r.ClickUi("Settings"); r.Step(2);
            r.Check(r.Has("Done") && r.Has("slider:Music"), "settings screen opens from the title");
            int[] s = r.G.UiRects["slider:Music"];
            r.Click(s[0] + s[2] / 4, s[1] + 6, 0);
            r.Check(Math.Abs(r.G.Cfg.Music - 0.25f) < 0.08f, "music slider responds to the mouse (" + r.G.Cfg.Music + ")");
            bool shake = r.G.Cfg.Shake;
            r.ClickUi("check:Screen shake");
            r.Check(r.G.Cfg.Shake != shake, "checkbox toggles");
            r.ClickUi("check:Screen shake");
            r.HoverUi("slider:Effects");
            r.Shot("02_settings");
            r.G.Cfg.Music = 0.7f;
            r.ClickUi("Done"); r.Step(2);
            r.Check(r.Has("New Game"), "Done returns to the title menu");
            r.Check(File.Exists(Path.Combine(r.G.DataDir, "settings.ini")), "settings.ini is written");

            r.Section("class selection");
            r.ClickUi("New Game"); r.Step(2);
            r.Check(r.G.Scr == Screen.ClassSelect, "New Game opens class selection");
            string[] names = { "Warrior", "Mage", "Rogue", "Ranger" };
            for (int i = 0; i < 4; i++)
            {
                r.ClickUi("class:" + names[i]); r.Step(2);
                r.In.MX = 300 + i * 70; r.In.MY = 240; r.Step(2);
                r.Shot("03_class_" + names[i].ToLowerInvariant());
            }
            r.Check(r.Has("Begin the Vigil") && r.Has("Back"), "class screen has Begin and Back");
            r.ClickUi("Back"); r.Step(2);
            r.Check(r.G.Scr == Screen.Title, "Back returns to the title");
        }

        // helpers for play
        static bool StartClass(Rig r, ClassId c)
        {
            Game g = r.G;
            if (g.Scr != Screen.Title) { g.GoTitle(); r.Step(5); }
            r.ClickUi("New Game"); r.Step(2);
            r.ClickUi("class:" + Classes.Get(c).Name); r.Step(2);
            r.ClickUi("Begin the Vigil");
            for (int i = 0; i < 300 && g.Scr != Screen.Play; i++) r.Step();
            r.Wait(0.8f);
            return g.Scr == Screen.Play && g.P != null && g.P.Class == c;
        }

        static Enemy Nearest(Game g, float maxDist)
        {
            Enemy best = null; float bd = maxDist * maxDist;
            foreach (Enemy e in g.Foes)
            {
                if (e.Dead || e.State == Enemy.Vanish) continue;
                float d = M.Dist2(e.X, e.Y, g.P.X, g.P.Y);
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        static void MoveKeys(Input input, float dx, float dy)
        {
            input.SetKey(Key.A, dx < -4); input.SetKey(Key.D, dx > 4);
            input.SetKey(Key.W, dy < -4); input.SetKey(Key.S, dy > 4);
        }

        /// <summary>A simple combat bot: closes or keeps distance, holds attack, uses abilities, dodges and drinks.</summary>
        static void Fight(Rig r, float seconds, Func<bool> until)
        {
            Game g = r.G;
            int frames = (int)(seconds * 60);
            for (int f = 0; f < frames; f++)
            {
                Player p = g.P;
                if (until != null && until()) break;
                if (p.Dead) break;
                if (g.Ov != Overlay.None) { r.Step(); continue; }
                Enemy t = Nearest(g, 330);
                if (t == null) break;
                r.AimWorld(t.X, t.Y - 8);
                float dx = t.X - p.X, dy = t.Y - p.Y, dist = M.Len(dx, dy);
                bool ranged = p.WType.Ranged;
                float want = ranged ? 96 : 15;
                if (dist > want + 8) MoveKeys(r.In, dx, dy);
                else if (ranged && dist < want - 34) MoveKeys(r.In, -dx, -dy);
                else MoveKeys(r.In, 0, 0);
                r.In.SetMouse(0, true);
                int m = f % 24;
                if (m == 4) r.Pulse(Key.Q);
                if (m == 10) r.Pulse(Key.E);
                if (m == 16) r.Pulse(Key.R);
                if (m == 20 && p.Class != ClassId.Warrior && dist < 140) r.PulseMouse(1);
                if (t.State == Enemy.Windup && dist < 70 && (f % 9) == 0 && !g.GodMode) r.Pulse(Key.Space);
                if (p.HP < p.MaxHP * 0.45f) r.Pulse(Key.N1);
                r.Step();
            }
            r.ReleaseAll(); r.Step(2);
        }

        /// <summary>Walks over everything lying on the ground nearby.</summary>
        static void Loot(Rig r, float seconds)
        {
            Game g = r.G;
            int frames = (int)(seconds * 60);
            for (int f = 0; f < frames; f++)
            {
                Pickup best = null; float bd = 220 * 220;
                foreach (Pickup d in g.Drops)
                {
                    if (d.T < 0.45f) continue;
                    if (d.Kind == Pickup.Equip && g.P.FreeBagSlot() < 0) continue;
                    float dd = M.Dist2(d.X, d.Y, g.P.X, g.P.Y);
                    if (dd < bd) { bd = dd; best = d; }
                }
                if (best == null) { if (g.Drops.Count == 0 || f > 60) break; r.Step(); continue; }
                if (f > 0 && f % 150 == 0) g.DebugTeleport(best.X, best.Y + 2);
                MoveKeys(r.In, best.X - g.P.X, best.Y - g.P.Y);
                r.Step();
            }
            r.ReleaseAll(); r.Step(2);
        }

        static Npc FindNpc(Game g, string id) { foreach (Npc n in g.Npcs) if (n.Id == id) return n; return null; }

        /// <summary>Walks up to a villager, presses F and waits for the text to finish typing.</summary>
        static bool TalkTo(Rig r, string id)
        {
            Game g = r.G;
            Npc n = FindNpc(g, id);
            if (n == null) { r.Check(false, "NPC exists: " + id); return false; }
            g.DebugTeleport(n.X - 14, n.Y + 6); r.Wait(0.35f);
            r.Tap(Key.F);
            if (g.Ov != Overlay.Dialogue) { r.Check(false, "F opens dialogue with " + id); return false; }
            SkipTyping(r);
            return true;
        }

        /// <summary>Finishes the typewriter effect without picking a choice.</summary>
        static void SkipTyping(Rig r)
        {
            r.Wait(0.15f);
            if (r.G.Ov == Overlay.Dialogue && r.G.DlgT < 900) { r.G.DlgT = 999; r.Step(2); }
        }

        static bool Choose(Rig r, string startsWith)
        {
            Game g = r.G;
            if (g.Dlg == null) { r.Check(false, "dialogue open for choice '" + startsWith + "'"); return false; }
            for (int i = 0; i < g.Dlg.Choices.Count; i++)
                if (g.Dlg.Choices[i].Key.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase))
                {
                    r.Step(2);
                    bool ok = r.ClickUi("choice" + i);
                    SkipTyping(r);
                    return ok;
                }
            r.Check(false, "dialogue choice starting with '" + startsWith + "' (have: " + Choices(g) + ")");
            return false;
        }

        static string Choices(Game g)
        {
            StringBuilder sb = new StringBuilder();
            if (g.Dlg != null) foreach (DChoice c in g.Dlg.Choices) sb.Append("[" + c.Key + "] ");
            return sb.ToString();
        }

        static void EndTalk(Rig r)
        {
            Game g = r.G;
            for (int i = 0; i < 6 && g.Ov == Overlay.Dialogue; i++) { r.Tap(Key.Esc); r.Step(3); }
            r.Wait(0.4f);
        }

        static bool Use(Rig r, string propId, Map m)
        {
            Game g = r.G;
            Prop p = m.FindProp(propId);
            if (p == null) { r.Check(false, "prop exists: " + propId); return false; }
            g.DebugTeleport(p.X, p.Y + 9); r.Wait(0.35f);
            if (g.NearProp != p) { g.DebugTeleport(p.X - 12, p.Y + 4); r.Step(4); }
            if (g.NearProp != p) { g.DebugTeleport(p.X + 12, p.Y + 4); r.Step(4); }
            if (g.NearProp != p) { r.Check(false, "can reach prop " + propId + " (near: " + (g.NearProp == null ? "none" : g.NearProp.Id) + ")"); return false; }
            r.Tap(Key.F); r.Step(4);
            return true;
        }

        /// <summary>Puts the player a given distance from a target, somewhere with a clear line to it.</summary>
        static void StandOff(Game g, Enemy t, float off)
        {
            float[] angles = { M.PI, 0, M.PI * 0.75f, -M.PI * 0.75f, M.PI * 0.5f, -M.PI * 0.5f, M.PI * 0.25f, -M.PI * 0.25f };
            foreach (float a in angles)
            {
                float x = t.X + M.Cos(a) * off, y = t.Y + 2 + M.Sin(a) * off * 0.8f;
                if (g.Cur.Blocked(x, y, 6, 1) || !g.Cur.Los(x, y - 8, t.X, t.Y - 6) || !g.Cur.Los(x, y, t.X, t.Y)) continue;
                g.DebugTeleport(x, y);
                return;
            }
            g.DebugTeleport(t.X - off, t.Y + 2);
        }

        static int BagCount(Player p) { int n = 0; foreach (Item it in p.Bag) if (it != null) n++; return n; }

        static void Heal(Game g) { g.P.HP = g.P.MaxHP; g.P.Res = g.P.MaxRes; for (int i = 0; i < 5; i++) g.P.Cd[i] = 0; g.P.ClearStatus(); }

        // per-class playtest
        static void PlayClass(Rig r, ClassId c, bool full)
        {
            Game g = r.G;
            ClassDef def = Classes.Get(c);
            string tag = def.Name.ToLowerInvariant();
            r.Section("class: " + def.Name);
            r.Check(StartClass(r, c), "new game starts as " + def.Name);
            Player p = g.P;
            r.Check(p.Equip[0] != null && p.Equip[1] != null, "starting weapon and armour are equipped (" + (p.Equip[0] != null ? p.Equip[0].Name : "-") + ")");
            r.Note("base stats: HP " + p.MaxHP + ", " + def.ResName + " " + p.MaxRes + ", armour " + p.ArmorRating + ", dmg " + p.WMin + "-" + p.WMax + " x" + p.DmgMult.ToString("0.00") + ", speed " + p.MoveSpeed + ", crit " + p.CritChance);
            r.Check(g.Q.Stage("bell") == 0, "main quest is active at the start");
            r.Wait(1.5f);
            r.In.MX = 420; r.In.MY = 170; r.Step(2);
            r.Shot("10_" + tag + "_start");

            // ---- movement
            float x0 = p.X, y0 = p.Y;
            r.Hold(Key.A, 0.5f); r.Check(p.X < x0 - 15, "A moves left");
            x0 = p.X; r.Hold(Key.D, 0.5f); r.Check(p.X > x0 + 15, "D moves right");
            y0 = p.Y; r.Hold(Key.S, 0.4f); r.Check(p.Y > y0 + 10, "S moves down");
            y0 = p.Y; r.Hold(Key.W, 0.4f); r.Check(p.Y < y0 - 10, "W moves up");
            int ch = p.DodgeCharges; x0 = p.X;
            r.In.SetKey(Key.A, true); r.Step(2); r.Tap(Key.Space); r.Wait(0.3f); r.In.SetKey(Key.A, false); r.Step(2);
            r.Check(p.DodgeCharges < ch || p.X < x0 - 30, "Space performs a dodge roll");
            r.Wait(1.8f);
            r.Check(p.DodgeCharges == def.DodgeCharges, "dodge charges recover (" + p.DodgeCharges + "/" + def.DodgeCharges + ")");

            // ---- dialogue
            int pots = p.HealthPotions;
            if (TalkTo(r, "warden"))
            {
                r.Shot("11_" + tag + "_dialogue");
                Choose(r, "What must I do");
                r.Check(g.Q.Stage("bell") == 1, "Halvard advances the main quest");
                r.Check(p.HealthPotions == pots + 2, "Halvard hands over two draughts");
                Choose(r, "Any advice");
                EndTalk(r);
                r.Check(g.Ov == Overlay.None, "dialogue closes");
            }

            // ---- honest combat against the first wolf pack
            r.Teleport(62.5f, 63.2f);
            r.Wait(0.3f);
            int kills0 = p.Kills, xp0 = p.XP + p.Level * 100000, gold0 = p.Gold;
            float hpBefore = p.HP;
            Fight(r, 45, null);
            r.Shot("12_" + tag + "_combat");
            r.Check(p.Kills - kills0 >= 2, "defeats the wolf pack in fair combat (kills " + (p.Kills - kills0) + ", HP " + (int)p.HP + "/" + (int)p.MaxHP + ", died: " + p.Dead + ")");
            r.Check(p.XP + p.Level * 100000 > xp0, "kills grant experience");
            Loot(r, 6);
            r.Check(p.Gold > gold0, "gold drops are collected (" + gold0 + " -> " + p.Gold + ")");
            if (p.Dead) { r.Note("bot died in fair combat; reviving for the rest of the test"); r.Wait(3); if (g.Ov == Overlay.Death) { r.ClickUi("Rise at the last wayshrine"); r.Wait(2); } }

            // ---- abilities, one by one, against the second pack
            g.GodMode = true;
            g.GainXp(900, false);
            p.Recalc(true);
            r.Check(p.Level >= 4, "experience raises the level (level " + p.Level + ")");
            r.Check(p.AttrPoints > 0, "levelling grants attribute points (" + p.AttrPoints + ")");
            r.Teleport(47.5f, 56.5f);
            TestAbilities(r, c, tag);
            Fight(r, 40, delegate { return Nearest(g, 200) == null; });
            Loot(r, 6);

            // ---- loot, inventory, equipment
            if (BagCount(p) < 2)
            {
                Use(r, "chest_hunter", g.Over); r.Wait(1.2f); Loot(r, 5);
                r.Check(g.Flags.Contains("open_chest_hunter"), "a chest opens with F");
            }
            if (BagCount(p) < 2) { p.AddToBag(Items.RollEquip(g.Rnd, 3, c, 1)); p.AddToBag(Items.Weapon(g.Rnd, def.WeaponTypes[1], 3, Rarity.Rare)); }
            r.Check(BagCount(p) >= 1, "loot ends up in the pack (" + BagCount(p) + " items)");
            Inventory(r, tag, full);

            // ---- other windows
            r.Tap(Key.C); r.Check(g.Ov == Overlay.Window && g.WinTab == 1, "C opens the character sheet");
            int vig = p.Vigor; float mhp = p.MaxHP; int pts = p.AttrPoints;
            if (pts > 0) { r.ClickUi("attr3"); r.Check(p.Vigor == vig + 1 && p.MaxHP > mhp && p.AttrPoints == pts - 1, "spending a point raises Vigor and maximum health"); }
            r.HoverUi("attr0");
            r.Shot("14_" + tag + "_character");
            r.Tap(Key.K); r.Check(g.WinTab == 2, "K switches to skills"); r.In.MX = 320; r.In.MY = 330; r.Step(2); r.Shot("15_" + tag + "_skills");
            if (full)
            {
                r.Tap(Key.J); r.Check(g.WinTab == 3, "J switches to the journal"); r.Shot("16_journal");
                r.Tap(Key.M); r.Check(g.WinTab == 4, "M switches to the map"); r.Shot("17_map");
                r.Tap(Key.M); r.Check(g.Ov == Overlay.None, "pressing the tab key again closes the window");
                r.Tap(Key.Esc); r.Check(g.Ov == Overlay.Pause, "Esc opens the pause menu"); r.Shot("18_pause");
                r.ClickUi("Settings"); r.Step(2); r.Check(g.Ov == Overlay.Settings, "settings open from pause"); r.ClickUi("Done"); r.Step(2);
                r.Check(g.Ov == Overlay.Pause, "settings return to pause");
                r.ClickUi("Resume"); r.Check(g.Ov == Overlay.None, "Resume closes the pause menu");
            }
            else { r.Tap(Key.Esc); r.Step(2); }
            if (g.Ov != Overlay.None) { g.CloseOverlay(); r.Step(2); }

            // ---- shrine, save, death, respawn
            g.GodMode = false;
            r.Check(Use(r, "shrine_rest", g.Over), "wayshrine can be used");
            r.Wait(0.5f);
            r.Check(g.Flags.Contains("shrine_rest") && g.HasSave(), "resting attunes the shrine and writes a save file");
            p.HP = 5;
            g.DamagePlayer(9999, p.X + 10, p.Y, true, null, 0, 50);
            r.Check(p.Dead, "lethal damage kills the player");
            r.Wait(3.2f);
            r.Check(g.Ov == Overlay.Death, "death screen appears");
            r.Shot("19_" + tag + "_death");
            r.ClickUi("Rise at the last wayshrine"); r.Wait(1.5f);
            r.Check(!p.Dead && g.Ov == Overlay.None && p.HP > 1, "respawn restores the player at the shrine");
            r.Check(M.Dist(p.X, p.Y, p.RespawnX, p.RespawnY) < 60, "respawn position is the wayshrine");

            // ---- save / load round trip
            int lvl = p.Level, gold = p.Gold, xp = p.XP, bag = BagCount(p), stage = g.Q.Stage("bell"), might = p.Might;
            string wname = p.Equip[0] != null ? p.Equip[0].DisplayName : "";
            r.Tap(Key.Esc); r.ClickUi("Save Game"); r.Step(3);
            r.Tap(Key.Esc); r.ClickUi("Save and Exit to Title");
            r.Wait(1.5f);
            r.Check(g.Scr == Screen.Title, "Save and Exit returns to the title");
            r.Step(3);
            r.Check(r.Has("Continue"), "title offers Continue once a save exists");
            if (full) r.Shot("20_title_with_save");
            r.ClickUi("Continue");
            for (int i = 0; i < 300 && g.Scr != Screen.Play; i++) r.Step();
            r.Wait(1f);
            p = g.P;
            r.Check(g.Scr == Screen.Play && p != null, "Continue loads the game");
            if (p != null)
            {
                r.Check(p.Class == c && p.Level == lvl && p.Gold == gold && p.XP == xp && p.Might == might, "class, level, gold, experience and attributes survive (" + p.Level + ", " + p.Gold + "g)");
                r.Check(BagCount(p) == bag && (p.Equip[0] != null ? p.Equip[0].DisplayName : "") == wname, "equipment and pack survive (" + wname + ")");
                r.Check(g.Q.Stage("bell") == stage && g.Flags.Contains("shrine_rest"), "quest progress and world flags survive");
                r.Shot("21_" + tag + "_loaded");
            }
            g.GoTitle(); r.Step(5);
        }

        static void Inventory(Rig r, string tag, bool full)
        {
            Game g = r.G; Player p = g.P;
            r.Tap(Key.I);
            r.Check(g.Ov == Overlay.Window && g.WinTab == 0, "I opens the inventory");
            int idx = -1;
            for (int i = 0; i < p.Bag.Length; i++) if (p.Bag[i] != null && p.Bag[i].IsEquip && p.CanUse(p.Bag[i])) { idx = i; break; }
            if (idx < 0) { r.Check(false, "an equippable item is in the pack"); g.CloseOverlay(); return; }
            Item it = p.Bag[idx];
            int slot = Player.SlotFor(it);
            Item old = p.Equip[slot];
            r.HoverUi("bag" + idx);
            r.Shot("13_" + tag + "_inventory");
            float dmgBefore = p.WMin + p.ArmorRating + p.MaxHP;
            r.ClickUi("bag" + idx);
            r.Check(p.Equip[slot] == it, "left-click equips an item (" + it.DisplayName + ")");
            r.Check(old == null || p.Bag[idx] == old, "the replaced item returns to the pack");
            r.Note("stat change on equip: " + dmgBefore + " -> " + (p.WMin + p.ArmorRating + p.MaxHP));
            if (full)
            {
                r.ClickUi("equip" + slot);
                r.Check(p.Equip[slot] == null, "clicking an equipment slot unequips it");
                int back = -1; for (int i = 0; i < p.Bag.Length; i++) if (p.Bag[i] == it) back = i;
                r.Check(back >= 0, "the unequipped item is in the pack");
                if (back >= 0) { r.ClickUi("bag" + back); r.Check(p.Equip[slot] == it, "it can be equipped again"); }
                int spare = -1; for (int i = 0; i < p.Bag.Length; i++) if (p.Bag[i] != null) spare = i;
                if (spare >= 0)
                {
                    int drops = g.Drops.Count; Item gone = p.Bag[spare];
                    r.ClickUi("bag" + spare, 1);
                    r.Check(p.Bag[spare] == null && g.Drops.Count == drops + 1, "right-click drops an item on the ground (" + gone.DisplayName + ")");
                }
                int hp = p.HealthPotions; p.HP = p.MaxHP * 0.5f; p.PotionCd = 0;
                r.ClickUi("potion0");
                r.Check(p.HealthPotions == hp - 1, "clicking a draught in the inventory drinks it");
            }
        }

        static float FoeHp(Game g) { float s = 0; foreach (Enemy e in g.Foes) if (!e.Dead) s += e.HP; else s -= 1000; return s; }

        static void TestAbilities(Rig r, ClassId c, string tag)
        {
            Game g = r.G; Player p = g.P;
            ClassDef def = Classes.Get(c);
            int[] keys = { -1, -2, Key.Q, Key.E, Key.R };
            for (int slot = 1; slot <= 4; slot++)
            {
                AbilityDef ab = def.Abilities[slot];
                Enemy t = Nearest(g, 500);
                if (t == null) { g.RespawnFoes(); r.Step(5); t = Nearest(g, 600); }
                if (t == null) { r.Check(false, ab.Name + ": an enemy is available to test on"); continue; }
                bool melee = !p.WType.Ranged;
                float off = (c == ClassId.Warrior || c == ClassId.Rogue) ? 30 : 70;
                if (c == ClassId.Mage && slot == 3) off = 30;
                StandOff(g, t, off);
                Heal(g);
                r.Step(2);
                r.AimWorld(t.X, t.Y - 4);
                r.Step(2);
                float px = p.X, py = p.Y, res0 = p.Res;
                double d0 = g.DealtTotal;
                int areas0 = g.Areas.Count, shots0 = g.Shots.Count;
                bool ok = false; string detail = "";
                if (slot == 1 && c == ClassId.Warrior)
                {
                    r.In.SetMouse(1, true); r.Step(6);
                    bool blocking = p.Blocking;
                    g.GodMode = false; float before = p.HP;
                    g.DamagePlayer(40, t.X, t.Y, true, t, 0, 0);
                    float taken = before - p.HP;
                    g.GodMode = true;
                    r.In.SetMouse(1, false); r.Step(3);
                    ok = blocking && taken < 12 && !p.Blocking;
                    detail = "blocking=" + blocking + ", 40 damage reduced to " + taken.ToString("0.#");
                }
                else
                {
                    if (slot == 1) { r.In.SetMouse(1, true); r.Step(2); r.In.SetMouse(1, false); }
                    else r.Tap(keys[slot]);
                    bool trapSeen = false, rootSeen = false; string trapInfo = "no trap at teleport";
                    int deadBefore = 0; foreach (Enemy e in g.Foes) if (e.Dead) deadBefore++;
                    if (c == ClassId.Ranger && slot == 2)
                    {
                        // sturdy foes: one the snare kills outright can never be seen rooted (and any of them may reach it first)
                        foreach (Enemy e in g.Foes)
                            if (!e.Dead && M.Dist(e.X, e.Y, p.X, p.Y) < 400) { e.MaxHP = Math.Max(e.MaxHP, 600); e.HP = e.MaxHP; }
                        // walk the target into the armed snare
                        for (int f = 0; f < 32; f++)
                        {
                            r.Step();
                            foreach (Area ar in g.Areas) if (ar.Kind == Area.Trap) trapSeen = true;
                            foreach (Enemy e in g.Foes) if (e.RootT > 0) rootSeen = true;
                        }
                        foreach (Area ar in g.Areas) if (ar.Kind == Area.Trap) { t.X = ar.X; t.Y = ar.Y; trapInfo = "trap T " + ar.T.ToString("0.00") + " armed " + ar.Armed + " ghost " + ar.Ghost + " at " + ar.X.ToString("0") + "," + ar.Y.ToString("0") + "; wolf state " + t.State + " r " + t.Radius.ToString("0"); }
                    }
                    bool cdStarted = p.Cd[slot] > 0 || ab.Cooldown <= 0;
                    r.Shot("ab_" + tag + "_" + slot + "_" + ab.Name.Replace(" ", "").Replace("'", "").ToLowerInvariant());
                    // let the effect play out
                    bool sawArea = trapSeen, sawShot = false, sawStealth = false, sawOath = false, sawMark = false, sawRoot = rootSeen, sawFreeze = false;
                    for (int f = 0; f < 200; f++)
                    {
                        if (g.Areas.Count > areas0) sawArea = true;
                        if (g.Shots.Count > shots0) sawShot = true;
                        if (p.StealthT > 0) sawStealth = true;
                        if (p.OathT > 0) sawOath = true;
                        foreach (Enemy e in g.Foes) { if (e.MarkT > 0) sawMark = true; if (e.RootT > 0) sawRoot = true; if (e.FreezeT > 0) sawFreeze = true; }
                        r.Step();
                        if (f > 30 && g.DealtTotal > d0 + 0.5 && !(c == ClassId.Ranger && slot == 2)) break;
                    }
                    float dealt = (float)(g.DealtTotal - d0);
                    int snareDead = -deadBefore; foreach (Enemy e in g.Foes) if (e.Dead) snareDead++;
                    float moved = M.Dist(px, py, p.X, p.Y);
                    detail = "cooldown=" + cdStarted + ", damage=" + dealt.ToString("0") + ", moved=" + moved.ToString("0") + ", spent=" + (res0 - p.Res).ToString("0");
                    switch (c)
                    {
                        case ClassId.Warrior:
                            ok = slot == 2 ? (moved > 25 && dealt > 0) : (slot == 3 ? dealt > 0 : sawOath);
                            break;
                        case ClassId.Mage:
                            ok = slot == 1 ? moved > 30 : (slot == 3 ? (dealt > 0 && sawFreeze) : dealt > 0);
                            break;
                        case ClassId.Rogue:
                            ok = slot == 1 ? (sawShot && dealt > 0) : (slot == 2 ? moved > 25 : (slot == 3 ? sawStealth : dealt > 0));
                            break;
                        case ClassId.Ranger:
                            ok = slot == 1 ? (sawShot && dealt > 0) : (slot == 2 ? (sawArea && sawRoot) : (slot == 3 ? (sawArea && dealt > 0) : (sawMark && dealt > 0)));
                            if (slot == 2)
                            {
                                detail += ", trap placed=" + sawArea + ", rooted=" + sawRoot;
                                if (!sawRoot) detail += "; target " + t.Def.Name + (t.Def.Flying ? " (flying)" : "") + " hp " + t.HP.ToString("0") + (t.Dead ? " dead" : "") + ", foes dead " + snareDead + "; " + trapInfo + "; after: wolf at " + t.X.ToString("0") + "," + t.Y.ToString("0") + " state " + t.State + ", areas " + g.Areas.Count;
                            }
                            break;
                    }
                    ok = ok && cdStarted;
                }
                r.Check(ok, "ability [" + ab.Key + "] " + ab.Name + " works (" + detail + ")");
                r.ReleaseAll(); r.Step(20);
            }
            // basic attack
            {
                Enemy t = Nearest(g, 500);
                if (t == null) { g.RespawnFoes(); r.Step(5); t = Nearest(g, 600); }
                if (t != null)
                {
                    StandOff(g, t, p.WType.Ranged ? 70 : 16); Heal(g); r.Step(2);
                    double d0 = g.DealtTotal;
                    for (int f = 0; f < 150 && g.DealtTotal <= d0; f++) { r.AimWorld(t.X, t.Y - 4); r.In.SetMouse(0, true); r.Step(); }
                    r.In.SetMouse(0, false); r.Step(2);
                    r.Check(g.DealtTotal > d0, "basic attack [LMB] " + Classes.Get(c).Abilities[0].Name + " deals damage");
                }
            }
        }

        // full story run
        static void Story(Rig r)
        {
            Game g = r.G;
            r.Section("story: full playthrough (Warrior)");
            r.Check(StartClass(r, ClassId.Warrior), "new game for the story run");
            Player p = g.P;
            g.GodMode = true;
            r.Wait(1f);

            TalkTo(r, "warden"); Choose(r, "What must I do"); EndTalk(r);
            r.Check(g.Q.Stage("bell") == 1, "stage 1: sent to Emberwick");

            // Emberwick
            Npc elder = FindNpc(g, "elder");
            g.DebugTeleport(elder.X - 40, elder.Y + 40); r.Wait(1.2f);
            r.In.MX = 300; r.In.MY = 120; r.Step(2);
            r.Shot("30_emberwick");
            r.Check(g.ZoneId == Zones.Emberwick, "zone changes to Emberwick");
            TalkTo(r, "elder"); r.Shot("31_elder"); Choose(r, "How do I get into"); EndTalk(r);
            r.Check(g.Q.Stage("bell") == 2, "stage 2: sent after the Abbey Sigil");
            TalkTo(r, "smith"); Choose(r, "Need any work"); Choose(r, "I'll hunt"); EndTalk(r);
            r.Check(g.Q.IsActive("wolves"), "side quest accepted from Bram");
            TalkTo(r, "herbalist"); Choose(r, "You look worried"); Choose(r, "I'll gather"); EndTalk(r);
            r.Check(g.Q.IsActive("petals"), "side quest accepted from Ysolde");
            TalkTo(r, "wren"); Choose(r, "I'll look for him"); EndTalk(r);
            r.Check(g.Q.IsActive("lamplighter"), "side quest accepted from Wren");
            int potions = p.HealthPotions; p.Gold += 200;
            TalkTo(r, "herbalist"); Choose(r, "Buy a Health"); EndTalk(r);
            r.Check(p.HealthPotions == potions + 1, "Ysolde sells a draught");
            TalkTo(r, "smith"); Choose(r, "Temper my weapon"); EndTalk(r);
            r.Check(p.Equip[0].Upgrade == 1, "Bram tempers the weapon to +1 (" + p.Equip[0].DisplayName + ")");
            TalkTo(r, "innkeeper"); Choose(r, "Heard anything"); Choose(r, "Anything else"); r.Shot("32_innkeeper"); EndTalk(r);
            r.Check(g.Q.IsActive("stones"), "Odo's rumour starts the rune-stone quest");

            // wolves
            float[] packs = { 58.5f, 61f, 45f, 54f, 33.5f, 65.5f, 50f, 39f, 31f, 33f };
            for (int i = 0; i < packs.Length && g.Q.Stage("wolves") == 0; i += 2)
            {
                r.Teleport(packs[i] + 3, packs[i + 1] + 2);
                Fight(r, 50, delegate { return g.Q.Stage("wolves") != 0 || Nearest(g, 230) == null; });
                Loot(r, 4);
            }
            r.Check(g.Q.Stage("wolves") == 1, "six wolves hunted (count " + g.Q.Get("wolves").Count + ")");
            r.Teleport(44, 52); r.Wait(0.5f); r.Shot("33_thornwood");

            // moonpetals
            int picked = 0;
            foreach (Prop q in new List<Prop>(g.Over.Props))
            {
                if (q.Kind != PropKind.Moonpetal || picked >= 5) continue;
                if (Use(r, q.Id, g.Over)) picked++;
            }
            r.Check(g.Q.Stage("petals") == 1 && p.KeepsakeCount("moonpetal") >= 5, "five moonpetals gathered");

            // the lamplighter
            Use(r, "lantern", g.Over);
            r.Check(g.Q.Stage("lamplighter") == 1 && p.KeepsakeCount("lantern") == 1, "Tobin's lantern found");
            r.Shot("34_lamplighter_camp");

            // hidden glade
            {
                bool[] before = g.Over.ReachableBody(g.Over.Find("start").X, g.Over.Find("start").Y, g.P.Radius);
                Prop gl = g.Over.FindProp("chest_glade");
                r.Check(!g.Over.BodyNear(before, gl.X, gl.Y + 10, 3), "the hidden glade is sealed off until the thornbrake is cut");
            }
            foreach (string id in new[] { "bramble_glade1", "bramble_glade2" })
            {
                Prop b = g.Over.FindProp(id);
                if (b == null) { r.Check(g.Flags.Contains("cut_" + id), "bramble " + id + " exists or was already cut"); continue; }
                g.DebugTeleport(b.X - 18, b.Y + 12); r.Step(3);
                for (int f = 0; f < 400 && g.Over.FindProp(id) != null; f++) { r.AimWorld(b.X, b.Y - 6); r.In.SetMouse(0, true); r.Step(); }
                r.In.SetMouse(0, false); r.Step(2);
            }
            r.Check(g.Flags.Contains("cut_bramble_glade1") && g.Flags.Contains("cut_bramble_glade2"), "thornbrakes can be cut down");
            bool[] reach = g.Over.ReachableBody(g.Over.Find("start").X, g.Over.Find("start").Y, g.P.Radius);
            Prop glade = g.Over.FindProp("chest_glade");
            r.Check(g.Over.BodyNear(reach, glade.X, glade.Y + 10, 3), "the hidden glade becomes reachable on foot");
            r.Teleport(53.4f, 24.6f);
            Fight(r, 30, delegate { return Nearest(g, 150) == null; });
            Use(r, "chest_glade", g.Over); r.Wait(1f); r.Shot("35_hidden_glade"); Loot(r, 5);
            r.Check(g.Flags.Contains("open_chest_glade"), "the glade's ornate chest opens");

            // rune stones and the king's hoard
            Use(r, "rune0", g.Over); r.Shot("36_rune_stone");
            Use(r, "rune1", g.Over); Use(r, "rune2", g.Over);
            r.Check(g.AllRunes && g.Q.Stage("stones") == 1, "three rune stones wake");
            Use(r, "cache", g.Over); r.Wait(1f); r.Shot("37_kings_hoard"); Loot(r, 5);
            r.Check(g.Flags.Contains("open_cache") && g.Q.IsDone("stones"), "the king's hoard opens and the quest completes");

            // hand-ins
            int bag0 = BagCount(p);
            TalkTo(r, "smith"); Choose(r, "The pack is thinned"); EndTalk(r);
            r.Check(g.Q.IsDone("wolves") && BagCount(p) >= bag0, "Bram rewards the wolf hunt");
            TalkTo(r, "herbalist"); Choose(r, "I have the moonpetals"); EndTalk(r);
            r.Check(g.Q.IsDone("petals") && p.KeepsakeCount("moonpetal") == 0, "Ysolde takes the moonpetals");
            TalkTo(r, "wren"); Choose(r, "I found this"); r.Shot("38_wren"); EndTalk(r);
            r.Check(g.Q.IsDone("lamplighter") && g.Over.FindProp("wren_lantern") != null, "Wren's quest completes and her lantern is lit");

            // level check and gearing up
            r.Note("before the Barrowfield: level " + p.Level + ", HP " + p.MaxHP + ", dmg " + p.WMin + "-" + p.WMax + " x" + p.DmgMult.ToString("0.00"));
            for (int i = 0; i < p.Bag.Length; i++)
            {
                Item it = p.Bag[i];
                if (it == null || !it.IsEquip || !p.CanUse(it)) continue;
                Item eq = p.Equip[Player.SlotFor(it)];
                if (eq == null || it.Score > eq.Score) g.EquipFromBag(i);
            }
            while (p.AttrPoints > 0) { p.AttrPoints--; p.Might++; }
            p.Recalc(true);

            // Barrowfield and Sir Caldus
            r.Teleport(131f, 68f); r.Wait(0.6f); r.Shot("39_barrowfield");
            Fight(r, 40, delegate { return Nearest(g, 200) == null; }); Loot(r, 4);
            r.Teleport(150f, 73.4f); r.Wait(0.5f);
            for (int f = 0; f < 240; f++) { Enemy k = Nearest(g, 300); if (k != null && k.Kind == EnemyKind.Knight && k.State == Enemy.Windup) break; r.In.SetKey(Key.D, true); r.Step(); }
            r.ReleaseAll();
            r.Shot("40_barrow_knight");
            Fight(r, 240, delegate { return g.Flags.Contains("dead_knight"); });
            r.Check(g.Flags.Contains("dead_knight"), "Sir Caldus is defeated");
            r.Wait(1f); Loot(r, 6);
            r.Check(p.KeepsakeCount("sigil") == 1 && g.Q.Stage("bell") == 3, "the Abbey Sigil drops and advances the quest");

            // Abbey Rise
            r.Teleport(88f, 30f); r.Wait(0.6f); r.Shot("41_abbey_rise");
            Fight(r, 40, delegate { return Nearest(g, 170) == null; });
            Use(r, "abbey_door", g.Over);
            r.Wait(1.5f);
            r.Check(g.Cur == g.Abbey && g.Q.Stage("bell") == 4, "the sigil opens the abbey and the player descends");
            r.Wait(0.5f); r.Shot("42_narthex");
            TalkTo(r, "ilse"); r.Shot("43_ilse"); Choose(r, "Is anything hidden"); EndTalk(r);

            // nave and wings
            r.Teleport(36f, 49f); r.Wait(0.4f);
            Fight(r, 60, delegate { return Nearest(g, 190) == null; });
            r.Shot("44_nave");
            r.Teleport(15f, 41f); Fight(r, 70, delegate { return Nearest(g, 150) == null; }); Loot(r, 4);
            Use(r, "brazier_w", g.Abbey); r.Wait(0.4f); r.Shot("45_scriptorium");
            r.Check(g.Flags.Contains("lit_brazier_w"), "west vigil-flame kindled");
            r.Teleport(56f, 41f); Fight(r, 70, delegate { return Nearest(g, 150) == null; }); Loot(r, 4);
            Use(r, "brazier_e", g.Abbey); r.Wait(0.6f);
            r.Check(g.Flags.Contains("lit_brazier_e") && g.Flags.Contains("bellgate_open") && g.Q.Stage("bell") == 5, "both flames open the Bellgate");

            // secret vault
            Prop wall = g.Abbey.FindProp("secret_wall");
            r.Check(wall != null, "the cracked wall is in place");
            if (wall != null)
            {
                g.DebugTeleport(wall.X, wall.Y + 14); r.Step(3);
                r.Shot("46_cracked_wall");
                for (int f = 0; f < 500 && !g.Flags.Contains("secret_open"); f++) { r.AimWorld(wall.X, wall.Y - 12); r.In.SetMouse(0, true); r.Step(); }
                r.In.SetMouse(0, false); r.Step(3);
                r.Check(g.Flags.Contains("secret_open"), "the cracked wall breaks under attack");
                Use(r, "chest_vault", g.Abbey); r.Wait(1f); r.Shot("47_vault"); Loot(r, 5);
                r.Check(g.Flags.Contains("open_chest_vault"), "the sealed vault's chest opens");
            }
            for (int i = 0; i < p.Bag.Length; i++)
            {
                Item it = p.Bag[i];
                if (it == null || !it.IsEquip || !p.CanUse(it)) continue;
                Item eq = p.Equip[Player.SlotFor(it)];
                if (eq == null || it.Score > eq.Score) g.EquipFromBag(i);
            }
            p.Recalc(true);

            // chancel, then the Prior
            r.Teleport(36f, 26.5f); Fight(r, 70, delegate { return Nearest(g, 120) == null; });
            Use(r, "shrine_undercroft", g.Abbey);
            r.Teleport(36f, 17f); r.Wait(0.3f);
            for (int f = 0; f < 200 && !g.BossFight; f++) { r.In.SetKey(Key.W, true); r.Step(); }
            r.ReleaseAll();
            r.Check(g.BossFight, "entering the Bell Crypt starts the boss fight");
            Prop bg = g.Abbey.FindProp("bossgate");
            r.Check(bg != null && bg.ColW > 0, "the gate closes behind the player");
            r.Wait(1.2f); r.Shot("48_boss_intro");
            r.Wait(2.2f);
            int shots = 0;
            Fight(r, 14, null); r.Shot("49_boss_fight");
            Fight(r, 400, delegate
            {
                if (g.Boss != null && g.Boss.HP < g.Boss.MaxHP * 0.6f && shots == 0) { shots = 1; r.Shot("50_boss_phase2"); }
                if (g.Boss != null && g.Boss.HP < g.Boss.MaxHP * 0.25f && shots == 1) { shots = 2; r.Shot("51_boss_phase3"); }
                return g.Flags.Contains("dead_prior");
            });
            r.Check(g.Flags.Contains("dead_prior"), "the Silent Prior is defeated");
            r.Check(!g.BossFight && bg != null && bg.ColW == 0, "the fight ends and the gate reopens");
            r.Wait(1.2f); Loot(r, 8);
            r.Check(p.KeepsakeCount("tongue") == 1 && g.Q.Stage("bell") == 6, "the Bell's Tongue is recovered");
            bool gotRelic = false; foreach (Item it in p.Bag) if (it != null && it.Unique && it.Rar == Rarity.Relic) gotRelic = true;
            r.Check(gotRelic, "the boss drops a named relic");

            // the bell
            Use(r, "bell", g.Abbey);
            r.Check(g.Flags.Contains("bell_rung") && g.Dawn, "the Vesper Bell rings and dawn breaks");
            r.Wait(1.0f); r.Shot("52_bell_rung");
            r.Wait(3.5f);
            r.Check(g.Ov == Overlay.Ending, "the ending screen appears");
            r.Wait(2.5f); r.Shot("53_ending");
            r.ClickUi("Step into the dawn"); r.Wait(0.5f);
            r.Check(g.Ov == Overlay.None, "play continues after the ending");

            // back into the light
            Use(r, "abbey_exit", g.Abbey); r.Wait(1.5f);
            r.Check(g.Cur == g.Over, "the stairs lead back to the surface");
            r.Wait(1.5f); r.Shot("54_dawn_abbey_rise");
            Npc el = FindNpc(g, "elder");
            g.DebugTeleport(el.X - 30, el.Y + 50); r.Wait(2.5f); r.Shot("55_dawn_emberwick");
            TalkTo(r, "elder"); Choose(r, "It is done"); r.Shot("56_elder_finale"); EndTalk(r);
            r.Check(g.Q.IsDone("bell"), "the main quest completes with Elder Maren");
            r.Note("finished at level " + p.Level + ", " + p.Kills + " kills, play time " + (int)g.PlayTime + " s (simulated)");
            r.Tap(Key.J); r.Shot("57_journal_complete"); r.Tap(Key.J);
            r.Tap(Key.I); r.Shot("58_final_inventory"); r.Tap(Key.I);
            r.Tap(Key.M); r.Shot("59_final_map"); r.Tap(Key.M);
            g.SaveGame();
            g.GodMode = false;
            g.GoTitle(); r.Step(5);
        }

        // co-op
        static Enemy Twin(Game g, Enemy of)
        {
            foreach (Enemy e in g.Foes) if (e.NetId == of.NetId) return e;
            return null;
        }

        static bool Heard(Game g, string part)
        {
            foreach (string s in g.NoticeLog) if (s.IndexOf(part, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>Was a notice built from this pattern shown, in whatever language the game is in?</summary>
        static bool HeardPattern(Game g, string pattern)
        {
            string tr = Loc.Tr(pattern);
            List<string> parts = new List<string>();
            foreach (string piece in System.Text.RegularExpressions.Regex.Split(tr, @"\{\d+\}|\^."))
                if (piece.Trim().Length >= 4) parts.Add(piece.Trim());
            foreach (string s in g.NoticeLog)
            {
                bool all = parts.Count > 0;
                foreach (string piece in parts) if (s.IndexOf(piece, StringComparison.Ordinal) < 0) { all = false; break; }
                if (all) return true;
            }
            return false;
        }

        /// <summary>Two copies of the game in one process, talking over the loopback address: one hosts, one joins.</summary>
        static void NetTest(Rig r)
        {
            Native.timeBeginPeriod(1);
            Game h = r.G;
            r.Section("co-op: hosting");
            if (h.Scr != Screen.Title) { h.GoTitle(); r.Step(5); }
            h.Cfg.Port = 27963; h.Cfg.PlayerName = "Aldric";
            r.Check(StartClass(r, ClassId.Warrior), "host starts a new game as a Warrior");
            h.GodMode = true;
            h.GainXp(700, false); h.Me.Recalc(true);
            r.Tap(Key.Esc);
            r.Check(r.Has("Host Co-op"), "the pause menu offers Host Co-op");
            r.ClickUi("Host Co-op"); r.Step(3);
            r.Check(h.IsHost && h.Ov == Overlay.Text, "hosting starts and the address panel opens");
            r.Shot("60_host_info");
            r.Tap(Key.Esc); r.Wait(0.4f);
            r.Tap(Key.Esc); r.Step(3);
            r.Check(r.Has("Stop Hosting") && r.Has("hostinfo"), "the pause menu now shows the party and Stop Hosting");
            r.Shot("61_host_pause");
            r.ClickUi("Resume"); r.Step(3);
            r.Check(h.Ov == Overlay.None, "host returns to play");

            r.Section("co-op: joining");
            Rig g = new Rig(r.Dir, "guestdata", Loc.Code, r);
            Game c = g.G;
            c.StartInitialLoad();
            for (int i = 0; i < 400 && c.Scr == Screen.Loading; i++) g.Step();
            r.Partner = g; g.Partner = r;
            r.Check(c.Scr == Screen.Title, "a second copy of the game reaches its title screen");
            g.In.MX = 400; g.In.MY = 300; g.Step(3);
            g.ClickUi("Join Game"); g.Step(2);
            r.Check(g.Has("Connect") && g.Has("joinaddr") && g.Has("joinname"), "Join Game opens the join panel");
            g.ClickUi("joinname"); g.In.Typed = "\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\bMira"; g.Step(2);
            g.ClickUi("joinaddr"); g.In.Typed = "127.0.0.1:27963"; g.Step(2);
            g.In.MX = 500; g.In.MY = 320; g.Step(2);
            g.Shot("62_join_panel");
            g.ClickUi("Connect");
            g.Until(900, delegate { return c.Scr == Screen.ClassSelect; });
            r.Check(c.Scr == Screen.ClassSelect && c.JoinState == Game.JoinLobby, "the guest connects and is asked to choose a class (" + c.JoinError + ")");
            r.Check(c.HostName == "Aldric" && c.HostLevel == h.Me.Level, "the guest is told whose game it is and its level (" + c.HostName + ", " + c.HostLevel + ")");
            g.ClickUi("class:Mage"); g.Step(2);
            g.In.MX = 300; g.In.MY = 240; g.Step(2);
            g.Shot("63_join_class");
            r.Check(g.Has("Join the Vigil"), "the class screen offers to join");
            g.ClickUi("Join the Vigil");
            g.Until(1500, delegate { return c.Scr == Screen.Play && c.JoinState == Game.JoinPlaying; });
            g.Wait(0.6f);
            r.Check(c.Scr == Screen.Play && c.IsClient && c.Me != null, "the guest enters the host's world");
            if (c.Me == null) return;
            c.GodMode = true;
            r.Check(h.Others.Count == 1 && c.Others.Count == 1, "each side sees one companion (" + h.Others.Count + ", " + c.Others.Count + ")");
            if (h.Others.Count != 1 || c.Others.Count != 1) return;
            r.Check(h.Others[0].NetName == "Mira" && h.Others[0].Class == ClassId.Mage, "the host sees the guest's name and class (" + h.Others[0].NetName + ")");
            r.Check(c.Others[0].NetName == "Aldric" && c.Others[0].Class == ClassId.Warrior, "the guest sees the host's name and class (" + c.Others[0].NetName + ")");
            r.Check(c.Me.Level == h.Me.Level && c.Me.Equip[0] != null, "the guest is raised to the host's level and geared (level " + c.Me.Level + ", " + (c.Me.Equip[0] != null ? c.Me.Equip[0].Name : "-") + ")");
            r.Check(M.Dist(c.Me.X, c.Me.Y, h.Me.X, h.Me.Y) < 70, "the guest appears beside the host");
            r.Check(c.Q.Stage("bell") == h.Q.Stage("bell"), "quest progress is shared on arrival");

            r.Section("co-op: seeing each other");
            g.Hold(Key.D, 0.7f); g.Wait(0.4f);
            r.Check(M.Dist(h.Others[0].X, h.Others[0].Y, c.Me.X, c.Me.Y) < 6, "the host sees the guest move (" + M.Dist(h.Others[0].X, h.Others[0].Y, c.Me.X, c.Me.Y).ToString("0.0") + " px apart)");
            r.Hold(Key.A, 0.7f); r.Wait(0.4f);
            r.Check(M.Dist(c.Others[0].X, c.Others[0].Y, h.Me.X, h.Me.Y) < 6, "the guest sees the host move (" + M.Dist(c.Others[0].X, c.Others[0].Y, h.Me.X, h.Me.Y).ToString("0.0") + " px apart)");
            r.In.MX = 420; r.In.MY = 150; g.In.MX = 220; g.In.MY = 150; r.Step(3);
            r.Shot("64_host_view"); g.Shot("64_guest_view");
            g.Tap(Key.Enter);
            r.Check(c.chatOpen, "Enter opens the chat line");
            g.In.Typed = "Ready when you are."; g.Step(2);
            g.Shot("65_chat_typing");
            g.Tap(Key.Enter); g.Wait(0.3f);
            r.Check(!c.chatOpen && Heard(h, "Mira:^0 Ready when you are."), "chat reaches the host");
            h.NetChat("Stay close."); r.Wait(0.3f);
            r.Check(Heard(c, "Aldric:^0 Stay close."), "chat reaches the guest");

            r.Section("co-op: shared quests");
            int hp0 = h.Me.HealthPotions, cp0 = c.Me.HealthPotions;
            if (TalkTo(g, "warden"))
            {
                Choose(g, "What must I do"); g.Wait(0.4f);
                r.Check(h.Q.Stage("bell") == 1 && c.Q.Stage("bell") == 1, "a quest step taken by the guest advances it for everyone (" + h.Q.Stage("bell") + ", " + c.Q.Stage("bell") + ")");
                r.Check(h.Me.HealthPotions == hp0 + 2 && c.Me.HealthPotions == cp0 + 2, "both heroes receive the reward");
                EndTalk(g);
            }

            r.Section("co-op: fighting together");
            r.Teleport(62.5f, 63.2f); g.Teleport(61.6f, 63.8f);
            g.Wait(1.2f);
            r.Check(c.Foes.Count > 0 && h.Foes.Count > 0, "the guest sees the host's enemies (" + c.Foes.Count + " of " + h.Foes.Count + ")");
            Enemy he = Nearest(h, 400);
            if (he != null)
            {
                he.RootT = Math.Max(he.RootT, 2f);          // held still, so the guest's copy has caught up with it, not trailing a running wolf
                g.Wait(0.5f);
                Enemy ce = Twin(c, he);
                float gap = ce != null ? M.Dist(ce.X, ce.Y, he.X, he.Y) : -1;
                r.Check(ce != null && gap < 14, "an enemy stands in the same place on both machines (" + (ce == null ? "missing on the guest" : gap.ToString("0.0") + " px apart") + ")");
                r.Check(he.MaxHP > he.Def.HP * 1.3f, "enemies are hardier for a party of two (" + he.MaxHP + " health against " + he.Def.HP + ")");
            }
            g.In.MX = 320; g.In.MY = 120; r.Step(2);
            g.Shot("66_guest_sees_wolves");
            int hk0 = h.Me.Kills, ck0 = c.Me.Kills, hx0 = h.Me.XP + h.Me.Level * 100000, cx0 = c.Me.XP + c.Me.Level * 100000, cg0 = c.Me.Gold, hg0 = h.Me.Gold;
            Fight(g, 50, delegate { return c.Me.Kills - ck0 >= 2; });
            g.Wait(0.5f);
            g.Shot("67_guest_fights"); r.Shot("67_host_watches");
            r.Check(c.Me.Kills - ck0 >= 2, "the guest's attacks kill the host's enemies (" + (c.Me.Kills - ck0) + ")");
            r.Check(h.Me.Kills - hk0 >= 2, "the host is told of the kills");
            r.Check(h.Me.XP + h.Me.Level * 100000 > hx0 && c.Me.XP + c.Me.Level * 100000 > cx0, "both heroes gain experience");
            Loot(g, 5); Loot(r, 5);
            r.Check(c.Me.Gold > cg0 && h.Me.Gold > hg0, "each hero finds their own spoils (" + (c.Me.Gold - cg0) + " and " + (h.Me.Gold - hg0) + " gold)");

            // the host's enemies can hurt the guest
            g.Teleport(45.5f, 54.6f);
            c.GodMode = false; c.Me.HP = c.Me.MaxHP;
            g.Until(900, delegate { return c.Me.HP < c.Me.MaxHP - 1; });
            r.Check(c.Me.HP < c.Me.MaxHP - 1, "the host's enemies hunt and wound the guest (" + (int)c.Me.HP + "/" + (int)c.Me.MaxHP + ")");
            c.GodMode = true; Heal(c);
            // ...and the host can come and help
            r.Teleport(46.5f, 55.2f);
            Fight(r, 40, delegate { return Nearest(h, 200) == null; });
            Fight(g, 20, delegate { return Nearest(c, 200) == null; });

            r.Section("co-op: a shared world");
            Use(g, "chest_hunter", c.Over); g.Wait(0.8f);
            r.Check(h.Flags.Contains("open_chest_hunter") && c.Flags.Contains("open_chest_hunter"), "a chest opened by the guest is open for everyone");
            r.Check(h.Drops.Count > 0 && c.Drops.Count > 0, "and each hero has their own share inside (" + h.Drops.Count + ", " + c.Drops.Count + ")");
            Loot(g, 4);
            Use(g, "shrine_rest", c.Over); g.Wait(0.5f);
            r.Check(h.Flags.Contains("shrine_rest") && c.Flags.Contains("shrine_rest"), "a wayshrine attuned by the guest is attuned for the party");
            {
                Prop b = c.Over.FindProp("bramble_glade1");
                if (b != null)
                {
                    c.DebugTeleport(b.X - 17, b.Y + 22); g.Step(3);       // on the spur, just outside the thicket
                    for (int f = 0; f < 700 && h.Over.FindProp("bramble_glade1") != null; f++) { g.AimWorld(b.X, b.Y - 6); g.In.SetMouse(0, true); g.Step(); }
                    g.In.SetMouse(0, false); g.Wait(0.4f);
                }
                r.Check(h.Over.FindProp("bramble_glade1") == null && c.Over.FindProp("bramble_glade1") == null, "a thornbrake cut by the guest falls on both machines");
            }
            Use(r, "rune0", h.Over); r.Wait(0.4f);
            r.Check(c.Flags.Contains("rune0") && c.Q.Stage("stones") == h.Q.Stage("stones"), "a rune stone woken by the host wakes for the guest");

            r.Section("co-op: travelling together");
            h.Me.AddKeepsake("sigil", 1);
            Use(r, "abbey_door", h.Over);
            r.Until(400, delegate { return h.Cur == h.Abbey && c.Cur == c.Abbey; });
            r.Wait(1f);
            r.Check(h.Cur == h.Abbey && c.Cur == c.Abbey, "when the host goes below, the guest goes with them");
            r.Check(M.Dist(c.Me.X, c.Me.Y, h.Me.X, h.Me.Y) < 90, "and arrives beside the host");
            g.In.MX = 320; g.In.MY = 150; g.Step(2);
            g.Shot("68_guest_abbey");
            r.Teleport(36f, 17f); r.Wait(0.3f);
            for (int f = 0; f < 240 && !h.BossFight; f++) { r.In.SetKey(Key.W, true); r.Step(); }
            r.ReleaseAll();
            g.Until(200, delegate { return c.BossFight; });
            g.Wait(0.5f);
            r.Check(h.BossFight && c.BossFight, "the boss fight begins for both");
            r.Check(c.Me.Y < DungeonGen.ArenaY1 && M.Dist(c.Me.X, c.Me.Y, h.Me.X, h.Me.Y) < 120, "the guest is drawn into the crypt before the gate shuts");
            g.Wait(3.5f);
            r.Check(c.Boss != null, "the guest sees the boss's health bar");
            float boss0 = h.Boss != null ? h.Boss.HP : 0;
            Fight(g, 8, null);
            g.Shot("69_guest_boss"); r.Shot("69_host_boss");
            r.Check(h.Boss != null && h.Boss.HP < boss0, "the guest can wound the boss (" + (h.Boss != null ? (int)h.Boss.HP : 0) + " of " + (int)boss0 + ")");

            r.Section("co-op: falling and rising");
            c.GodMode = false; c.Me.HP = 1; c.P = c.Me;
            c.DamagePlayer(9999, c.Me.X + 10, c.Me.Y, true, null, 0, 50);
            r.Check(c.Me.Dead, "the guest can fall");
            g.Until(400, delegate { return c.Ov == Overlay.Death && c.DeathT > 2.7f; });
            r.Check(h.Others[0].Dead, "the host sees the guest fall");
            r.Check(g.Has("Rise beside your companions"), "the death screen offers to rise beside a companion");
            g.Shot("70_guest_death");
            g.ClickUi("Rise beside your companions"); g.Wait(1.6f);
            c.GodMode = true;
            r.Check(!c.Me.Dead && c.Ov == Overlay.None && M.Dist(c.Me.X, c.Me.Y, h.Me.X, h.Me.Y) < 60, "the guest rises beside the host");
            r.Check(h.BossFight && h.Boss != null && h.Boss.HP <= boss0, "and the fight goes on - nothing was reset");
            r.Wait(0.3f);
            r.Check(!h.Others[0].Dead, "the host sees them rise");

            r.Section("co-op: leaving");
            g.Tap(Key.Esc); g.Step(3);
            r.Check(g.Has("Leave the Party") && !g.Has("Save Game"), "a guest's pause menu offers to leave, not to save");
            g.Shot("71_guest_pause");
            g.ClickUi("Leave the Party");
            g.Until(300, delegate { return c.Scr == Screen.Title; });
            r.Check(c.Scr == Screen.Title && !c.Online, "the guest returns to their own title screen");
            r.Until(300, delegate { return h.Others.Count == 0; });
            r.Check(h.Others.Count == 0 && h.IsHost, "the host carries on alone, still hosting");
            r.Check(HeardPattern(h, "^y{0}^0 has left the party."), "and is told the guest has gone");
            r.Tap(Key.Esc); r.Step(3);
            r.ClickUi("Stop Hosting"); r.Step(3);
            r.Check(!h.Online, "Stop Hosting ends the session");
            // nobody is listening any more: a join attempt must fail cleanly
            g.Step(5);
            g.ClickUi("Join Game"); g.Step(2);
            g.ClickUi("Connect");
            g.Until(700, delegate { return c.JoinState == Game.JoinIdle && c.JoinError.Length > 0; });
            r.Check(c.JoinState == Game.JoinIdle && c.JoinError.Length > 0, "joining a game that is not there fails with a message (" + c.JoinError + ")");
            g.Shot("72_join_failed");
            r.Partner = null; g.Partner = null;
            c.GoTitle();
            h.GodMode = false;
            h.GoTitle(); r.Step(5);
            Native.timeEndPeriod(1);
        }

        // performance
        static void Perf(Rig r)
        {
            Game g = r.G;
            r.Section("performance");
            if (!StartClass(r, ClassId.Mage)) { r.Check(false, "start for perf"); return; }
            g.GodMode = true;
            float[] spots = { 88, 66, 45, 54, 140, 58, 88, 30 };
            string[] names = { "Emberwick", "Thornwood (combat)", "Barrowfield", "Abbey Rise" };
            for (int i = 0; i < 4; i++)
            {
                r.Teleport(spots[i * 2], spots[i * 2 + 1]);
                r.Wait(0.5f);
                Stopwatch up = new Stopwatch(), rd = new Stopwatch();
                double worst = 0;
                for (int f = 0; f < 240; f++)
                {
                    Enemy t = Nearest(g, 300);
                    if (t != null) { r.AimWorld(t.X, t.Y); r.In.SetMouse(0, true); if (f % 30 == 0) r.Pulse(Key.Q); if (f % 50 == 0) r.Pulse(Key.E); }
                    long a = Stopwatch.GetTimestamp();
                    up.Start(); r.Step(); up.Stop();
                    rd.Start(); g.Render(); rd.Stop();
                    double ms = (Stopwatch.GetTimestamp() - a) * 1000.0 / Stopwatch.Frequency;
                    if (f > 5 && ms > worst) worst = ms;
                }
                r.ReleaseAll();
                double uMs = up.Elapsed.TotalMilliseconds / 240, rMs = rd.Elapsed.TotalMilliseconds / 240;
                r.Note(names[i] + ": update " + uMs.ToString("0.00") + " ms, render " + rMs.ToString("0.00") + " ms, worst frame " + worst.ToString("0.0") + " ms, particles " + g.PartCount);
                r.Check(uMs + rMs < 10, names[i] + " simulates and renders within a 60 fps budget (" + (uMs + rMs).ToString("0.00") + " ms)");
            }
            g.GodMode = false;
            g.GoTitle(); r.Step(3);
        }

        // audio
        static void AudioTest(Rig r)
        {
            r.Section("audio (rendered offline)");
            AudioEngine a = new AudioEngine();
            Stopwatch sw = Stopwatch.StartNew();
            a.BuildBank();
            r.Note("sound bank synthesised in " + sw.ElapsedMilliseconds + " ms");
            float[][] bank = SfxBank.Build();
            int bad = 0; float total = 0;
            for (int i = 0; i < bank.Length; i++)
            {
                float peak = 0; bool nan = false;
                foreach (float v in bank[i]) { if (float.IsNaN(v) || float.IsInfinity(v)) nan = true; if (Math.Abs(v) > peak) peak = Math.Abs(v); }
                total += bank[i].Length / (float)AudioEngine.SR;
                if (nan || peak < 0.05f || peak > 1f) { bad++; r.Note("sfx " + i + " suspicious: peak " + peak + " nan " + nan + " len " + bank[i].Length); }
            }
            r.Check(bad == 0, "all " + bank.Length + " sound effects are finite, audible and unclipped (" + total.ToString("0.0") + " s of audio)");

            string[] names = { "menu", "village", "wild", "ruins", "dungeon", "boss", "dawn" };
            for (int t = 0; t < names.Length; t++)
            {
                AudioEngine e = new AudioEngine();
                e.BuildBank();
                e.SetMusic(t);
                e.SetAmbience(t == 4 || t == 5 ? Zones.AmbDungeon : (t == 1 ? Zones.AmbVillage : Zones.AmbForest), t == 1 ? 0.6f : 0, 0, t == 6);
                e.SetCombat(t == 2 ? 0.8f : 0);
                sw.Restart();
                float[] pcm = e.RenderOffline(24f);
                double renderMs = sw.Elapsed.TotalMilliseconds;
                double sum = 0; float peak = 0; bool nan = false; int clipped = 0;
                int from = AudioEngine.SR * 2 * 6;
                for (int i = from; i < pcm.Length; i++)
                {
                    float v = pcm[i];
                    if (float.IsNaN(v)) nan = true;
                    sum += v * v; if (Math.Abs(v) > peak) peak = Math.Abs(v);
                    if (Math.Abs(v) > 0.97f) clipped++;
                }
                double rms = Math.Sqrt(sum / Math.Max(1, pcm.Length - from));
                r.Note(names[t] + ": rms " + rms.ToString("0.000") + ", peak " + peak.ToString("0.00") + ", clipped " + clipped + ", rendered 24 s in " + renderMs.ToString("0") + " ms");
                r.Check(!nan && rms > 0.01 && rms < 0.3 && clipped < 50, "music + ambience '" + names[t] + "' is audible and clean");
                if (t == 0 || t == 5) AudioEngine.SaveWav(Path.Combine(r.Dir, "audio_" + names[t] + ".wav"), pcm, 2);
                Spectrogram(pcm, Path.Combine(r.Dir, "audio_" + names[t] + ".png"));
            }
        }

        /// <summary>Writes a small log-frequency spectrogram so the rendered audio can be inspected by eye.</summary>
        static void Spectrogram(float[] stereo, string path)
        {
            const int N = 2048, W = 600, H = 160;
            int frames = stereo.Length / 2;
            Surface s = new Surface(W, H);
            double[] re = new double[N], im = new double[N];
            for (int x = 0; x < W; x++)
            {
                int start = (int)((long)x * (frames - N) / W);
                for (int i = 0; i < N; i++)
                {
                    double w = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (N - 1));
                    re[i] = (stereo[(start + i) * 2] + stereo[(start + i) * 2 + 1]) * 0.5 * w; im[i] = 0;
                }
                Fft(re, im);
                for (int y = 0; y < H; y++)
                {
                    double f = 40 * Math.Pow(8000.0 / 40, 1 - y / (double)(H - 1));
                    int bin = Math.Min(N / 2 - 1, (int)(f * N / AudioEngine.SR));
                    double mag = Math.Sqrt(re[bin] * re[bin] + im[bin] * im[bin]) / (N / 4);
                    double db = 20 * Math.Log10(mag + 1e-9);
                    int v = M.Clamp((int)((db + 80) * 3.4), 0, 255);
                    s.Px[y * W + x] = Col.Rgb(v, v > 128 ? (v - 128) * 2 : 0, 255 - v > 200 ? 40 : v / 3) & 0xFFFFFF;
                }
            }
            s.SavePng(path, 1);
        }

        static void Fft(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { double t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len, wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        double ur = re[i + k], ui = im[i + k];
                        double vr = re[i + k + len / 2] * cr - im[i + k + len / 2] * ci, vi = re[i + k + len / 2] * ci + im[i + k + len / 2] * cr;
                        re[i + k] = ur + vr; im[i + k] = ui + vi; re[i + k + len / 2] = ur - vr; im[i + k + len / 2] = ui - vi;
                        double nr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = nr;
                    }
                }
            }
        }
    }
}
