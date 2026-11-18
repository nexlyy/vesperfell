using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Vesperfell
{
    internal static class Program
    {
        public static string BaseDir;

        [STAThread]
        static int Main(string[] args)
        {
            BaseDir = AppDomain.CurrentDomain.BaseDirectory;
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
            bool quiet = false;
            try
            {
                bool windowed = false, full = false, mute = false; int gfx = 0;
                for (int i = 0; i + 1 < args.Length; i++)
                {
                    int us;
                    if (args[i].ToLowerInvariant() == "--uiscale" && int.TryParse(args[i + 1], out us)) GameWindow.ForceUiScale = Math.Max(1, Math.Min(4, us));
                }
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i].ToLowerInvariant();
                    string next = i + 1 < args.Length ? args[i + 1] : null;
                    switch (a)
                    {
                        case "--windowed": windowed = true; break;
                        case "--fullscreen": full = true; break;
                        case "--mute": mute = true; break;
                        case "--2d": gfx = 2; break;
                        case "--3d": gfx = 3; break;
                        case "--artdump": quiet = true; ArtDump.Run(next ?? Path.Combine(BaseDir, "artdump")); return 0;
                        case "--worlddump": quiet = true; WorldDump.Run(next ?? Path.Combine(BaseDir, "worlddump")); return 0;
                        case "--selftest": quiet = true; return SelfTest.Run(next ?? Path.Combine(BaseDir, "selftest"), i + 2 < args.Length ? args[i + 2] : "all");
                        case "--matdump": quiet = true; MatGen.Dump(next ?? Path.Combine(BaseDir, "matdump")); return 0;
                        case "--makeicon": quiet = true; WriteIcon(next ?? Path.Combine(BaseDir, "vesperfell.ico")); return 0;
                        case "--smoke":
                            {
                                quiet = true;
                                string dir = next != null && !next.StartsWith("--") ? next : Path.Combine(BaseDir, "smoke");
                                for (int j = i + 1; j < args.Length; j++)
                                {
                                    if (args[j] == "--2d") gfx = 2; if (args[j] == "--3d") gfx = 3; if (args[j] == "--top") smokeTop = true; if (args[j] == "--mute") mute = true;
                                }
                                return RunGame(true, false, mute, true, dir, gfx);
                            }
                        case "--tour":
                            {
                                // walks the hero past the landmarks and saves what the window shows at each (visual review)
                                quiet = true;
                                string dir = next != null && !next.StartsWith("--") ? next : Path.Combine(BaseDir, "tour");
                                tourClass = "Ranger";
                                for (int j = i + 1; j < args.Length; j++)
                                {
                                    string t = args[j].ToLowerInvariant();
                                    if (t == "warrior" || t == "mage" || t == "rogue" || t == "ranger") tourClass = char.ToUpper(t[0]) + t.Substring(1);
                                    if (t == "--top") smokeTop = true; if (t == "--ru") tourRu = true; if (t == "--ui") tourUiOnly = true;
                                }
                                return RunGame(true, false, true, true, dir, 3);
                            }
                    }
                }
                return RunGame(windowed, full, mute, false, null, gfx);
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(BaseDir, "crash.log"), DateTime.Now + "\r\n" + ex); } catch { }
                if (!quiet) { try { MessageBox.Show(Loc.Tr("Vesperfell ran into a problem and has to close.") + "\n\n" + ex.Message + "\n\n" + Loc.Tr("Details were written to crash.log."), "Vesperfell", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { } }
                return 1;
            }
        }

        /// <summary>Saves go next to the executable when that folder is writable, otherwise to the user's local app data.</summary>
        static string PickDataDir()
        {
            try
            {
                string probe = Path.Combine(BaseDir, ".write-test");
                File.WriteAllText(probe, "x"); File.Delete(probe);
                return BaseDir;
            }
            catch
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vesperfell");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        static bool smokeTop, tourRu;
        internal static bool tourUiOnly;
        static string tourClass;

        static int RunGame(bool forceWindowed, bool forceFull, bool mute, bool smoke, string smokeDir, int gfx)
        {
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Input input = new Input();
            Game game = new Game(input, false);
            game.ClipboardGet = delegate { try { return Clipboard.ContainsText() ? Clipboard.GetText() : ""; } catch { return ""; } };
            game.DataDir = smoke ? smokeDir : PickDataDir();
            if (smoke) Directory.CreateDirectory(smokeDir);
            game.Cfg.Load(Path.Combine(game.DataDir, "settings.ini"));
            if (gfx == 3 || (gfx == 0 && game.Cfg.Renderer != "2d")) MatGen.Prewarm();      // paints the 3D surfaces while the rest starts
            if (forceWindowed) game.Cfg.Fullscreen = false;
            if (forceFull) game.Cfg.Fullscreen = true;
            if (smoke) { game.Cfg.Camera = smokeTop ? "top" : "fp"; game.Cfg.Language = tourRu ? "ru" : "en"; Loc.Set(game.Cfg.Language); }
            input.Scripted = smoke;
            Fonts.Build();
            AudioEngine audio = null;
            if (!mute) { audio = new AudioEngine(); audio.Start(); game.Audio = audio; }
            SmokeScript script = smoke ? new SmokeScript(smokeDir, tourClass) : null;
            using (GameWindow win = new GameWindow(game, input))
            {
                win.ApplyDisplay(game.Cfg.Fullscreen, game.Cfg.WindowScale);
                win.Show(); win.Activate();
                bool want3d = gfx == 3 || (gfx == 0 && game.Cfg.Renderer != "2d");
                if (want3d)
                {
                    string err;
                    if (!win.StartGl(out err))
                    {
                        game.GfxNote = "2D view: OpenGL 3.3 is not available";
                        try { File.WriteAllText(Path.Combine(game.DataDir, "graphics.log"), DateTime.Now + "  3D view unavailable, using 2D: " + err + "\r\n"); } catch { }
                    }
                }
                if (script != null) script.Note("renderer: " + (game.R3 != null ? "3D (" + game.R3.Info + ")" : "2D software"));
                game.StartInitialLoad();
                Native.timeBeginPeriod(1);
                Stopwatch sw = Stopwatch.StartNew();
                double last = sw.Elapsed.TotalSeconds, acc = 0, fpsT = last;
                int frames = 0;
                while (!game.WantQuit && !win.IsDisposed)
                {
                    Application.DoEvents();
                    if (win.IsDisposed) break;
                    win.UpdateMouseLock(game.WantMouseLook());
                    double now = sw.Elapsed.TotalSeconds, ft = now - last;
                    last = now;
                    if (ft > 0.25) ft = 0.25;
                    acc += ft;
                    int steps = 0;
                    while (acc >= Game.DT && steps < 4)
                    {
                        if (script != null) script.Tick(game, input, win, audio);
                        game.Update(Game.DT);
                        input.EndTick();
                        acc -= Game.DT; steps++;
                    }
                    if (steps >= 4) acc = 0;
                    if (game.WantApplyDisplay) { game.WantApplyDisplay = false; win.ApplyDisplay(game.Cfg.Fullscreen, game.Cfg.WindowScale); }
                    if (steps > 0)
                    {
                        double r0 = sw.Elapsed.TotalSeconds;
                        if (win.Gl) win.Begin3D();
                        game.SetUiScale(win.UiScale);
                        game.Render();
                        double r1 = sw.Elapsed.TotalSeconds;
                        if (win.Gl) win.Present3D(); else win.Present(game.Final);
                        frames++;
                        if (script != null) script.FrameMs((sw.Elapsed.TotalSeconds - r0) * 1000.0, (r1 - r0) * 1000.0, game.Ms3D, GameWindow.MsComposite);
                    }
                    if (now - fpsT >= 0.5) { game.Fps = (float)(frames / (now - fpsT)); frames = 0; fpsT = now; }
                    double wait = Game.DT - acc - (sw.Elapsed.TotalSeconds - now);
                    if (wait > 0.002) Thread.Sleep(Math.Max(1, (int)(wait * 1000) - 1)); else Thread.Sleep(0);
                }
                Native.timeEndPeriod(1);
                if (!smoke)
                {
                    if (game.Scr == Screen.Play && game.P != null && !game.P.Dead) game.SaveGame();
                    game.SaveSettings();
                }
                game.NetStop();
                win.DisposeGl();
            }
            if (audio != null) audio.Dispose();
            return script != null ? script.ExitCode : 0;
        }

        /// <summary>Writes a multi-size .ico built from the game's own art; the build embeds it in the executable.</summary>
        static void WriteIcon(string path)
        {
            int[] sizes = { 16, 24, 32, 48, 64 };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using (BinaryWriter w = new BinaryWriter(File.Create(path)))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                byte[][] images = new byte[sizes.Length][];
                for (int i = 0; i < sizes.Length; i++)
                {
                    int s = sizes[i];
                    using (Bitmap bmp = GameWindow.RenderIcon(s))
                    using (MemoryStream ms = new MemoryStream())
                    using (BinaryWriter b = new BinaryWriter(ms))
                    {
                        int maskRow = ((s + 31) / 32) * 4;
                        b.Write(40); b.Write(s); b.Write(s * 2); b.Write((short)1); b.Write((short)32); b.Write(0); b.Write(s * s * 4 + maskRow * s);
                        b.Write(0); b.Write(0); b.Write(0); b.Write(0);
                        for (int y = s - 1; y >= 0; y--)
                            for (int x = 0; x < s; x++)
                            {
                                Color c = bmp.GetPixel(x, y);
                                b.Write(c.B); b.Write(c.G); b.Write(c.R); b.Write(c.A);
                            }
                        for (int y = s - 1; y >= 0; y--)
                        {
                            byte[] row = new byte[maskRow];
                            for (int x = 0; x < s; x++) if (bmp.GetPixel(x, y).A == 0) row[x >> 3] |= (byte)(0x80 >> (x & 7));
                            b.Write(row);
                        }
                        b.Flush();
                        images[i] = ms.ToArray();
                    }
                }
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)sizes[i]); w.Write((byte)sizes[i]); w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32); w.Write(images[i].Length); w.Write(offset);
                    offset += images[i].Length;
                }
                for (int i = 0; i < sizes.Length; i++) w.Write(images[i]);
            }
        }
    }

    /// <summary>
    /// Drives the real windowed game for a few seconds (menu, class select, movement, combat, inventory) and records
    /// what ended up on the actual window, so the shipped executable itself is exercised - not just the simulation.
    /// </summary>
    internal sealed class SmokeScript
    {
        readonly string dir; readonly StringBuilder log = new StringBuilder();
        int tick, phase, phaseTick; double msSum; int msCount; double msMax, msRender, msScene, msComp;
        int releaseAt = -1;
        float yaw0;
        public int ExitCode = 1;
        readonly string tour;            // class to tour the landmarks with, or null for the regular smoke run
        int stop;

        public SmokeScript(string dir, string tourClass) { this.dir = dir; tour = tourClass; }

        // map, landmark, offset x/y, view heading and pitch (degrees; 0 = east, 90 = south, pitch up positive), picture name
        static readonly object[] Stops =
        {
            "over", "start", 0f, 0f, -90f, -3f, "t01_start",
            "over", "emberwick", 0f, 96f, -90f, 3f, "t02_plaza",
            "over", "emberwick", -186f, -24f, -90f, 5f, "t03_inn",
            "over", "emberwick", 150f, 30f, -40f, 0f, "t04_forge",
            "over", "knight", 0f, 130f, -90f, 0f, "t05_east",
            "over", "abbey_door", 0f, 150f, -90f, 9f, "t06_abbey",
            "abbey", "abbey_entry", 0f, 64f, -90f, 3f, "t07_crypt",
            "abbey", "brazier_w", 30f, 50f, -120f, 2f, "t08_hall",
            "over", "start", 0f, 0f, -90f, -14f, "t09_loot",
            "over", "start", 0f, 0f, -90f, -14f, "t10_loot",
            "over", "start", 0f, 0f, -90f, -14f, "t11_loot",
            // the interface: every window over the world, then a conversation and the death screen
            "over", "start", 0f, 0f, -90f, -3f, "u01_inventory",
            "over", "start", 0f, 0f, -90f, -3f, "u02_character",
            "over", "start", 0f, 0f, -90f, -3f, "u03_skills",
            "over", "start", 0f, 0f, -90f, -3f, "u04_journal",
            "over", "start", 0f, 0f, -90f, -3f, "u05_map",
            "over", "start", 0f, 0f, -90f, -3f, "u06_pause",
            "over", "start", 0f, 0f, -90f, -3f, "u07_settings",
            "over", "emberwick", 150f, 30f, -40f, 0f, "u08_dialogue",
            "over", "start", 0f, 0f, -90f, -3f, "u09_death",
        };

        bool gaveKit;

        /// <summary>Opens the window a "u" stop is named for (and fills the pack once, so the windows have content).</summary>
        void OpenUiStop(Game g, Input input, string name)
        {
            if (g.P == null) return;
            if (!gaveKit)
            {
                gaveKit = true;
                Rng r = new Rng(91);
                for (int t = 0; t < 6; t++) g.Give(Items.Weapon(r, t, 4 + t / 2, (Rarity)(t % 4)));
                g.Give(Items.Armor(r, g.P.Class, 5, Rarity.Rare));
                g.Give(Items.Trinket(r, true, 5, Rarity.Fine));
                g.Give(Items.Trinket(r, false, 5, Rarity.Rare));
                g.P.Gold += 240;
            }
            switch (name)
            {
                case "u01_inventory": g.OpenWindow(0); break;
                case "u02_character": g.OpenWindow(1); break;
                case "u03_skills": g.OpenWindow(2); break;
                case "u04_journal": g.OpenWindow(3); break;
                case "u05_map": g.OpenWindow(4); break;
                case "u06_pause": g.Ov = Overlay.Pause; break;
                case "u07_settings": g.Ov = Overlay.Settings; break;
                case "u08_dialogue":
                    {
                        Npc best = null; float bd = 1e9f;
                        for (int i = 0; i < g.Npcs.Count; i++)
                        {
                            float d = Math.Abs(g.Npcs[i].X - g.P.X) + Math.Abs(g.Npcs[i].Y - g.P.Y);
                            if (d < bd) { bd = d; best = g.Npcs[i]; }
                        }
                        if (best != null) { g.StartDialogue(best); Say("talking to " + best.Id); }
                        break;
                    }
                case "u09_death": g.P.IFrames = 0; g.P.Blocking = false; g.DamagePlayer(g.P.MaxHP * 20, g.P.X + 10, g.P.Y, false, null, 0, 0); break;
            }
        }

        /// <summary>Points the mouse at the middle of a remembered interface element (to show its tooltip).</summary>
        void HoverUi(Game g, Input input, string id)
        {
            int[] r;
            if (g.UiRects.TryGetValue(id, out r)) { input.MX = r[0] + r[2] / 2; input.MY = r[1] + r[3] / 2; }
        }

        /// <summary>Every kind of loot laid out in rows ahead of the hero (north), to review the models.</summary>
        static void ShowLoot(Game g, int set)
        {
            if (g.P == null) return;
            g.Drops.Clear();
            Rng r = new Rng(77);
            Item[] row1 = new Item[8], row2 = new Item[8], row3 = new Item[4];
            for (int t = 0; t < 8; t++) row1[t] = Items.Weapon(r, t, 5, (Rarity)(t % 4));
            for (int c = 0; c < 4; c++) row2[c] = Items.Armor(r, (ClassId)c, 5, (Rarity)c);
            for (int k = 0; k < 4; k++)
            {
                Item it = Items.Trinket(r, k < 2, 5, (Rarity)(1 + k % 3));
                it.Base = k & 1;
                row2[4 + k] = it;
            }
            string[] q = { "sigil", "moonpetal", "lantern", "tongue" };
            for (int k = 0; k < 4; k++) row3[k] = Items.Quest(q[k]);
            Item[][] rows = { row1, row2, row3 };
            int ri = M.Clamp(set, 0, 2);
            float gap = ri == 2 ? 16 : 12;
            for (int k = 0; k < rows[ri].Length; k++)
            {
                Pickup d = new Pickup();
                d.Kind = ri == 2 ? Pickup.Quest : Pickup.Equip; d.Amount = 1; d.It = rows[ri][k];
                d.X = g.P.X + (k - (rows[ri].Length - 1) * 0.5f) * gap;
                d.Y = g.P.Y - (ri == 2 ? 54 : 40);         // quest relics fly to the hero inside 44
                d.T = 0.5f + k * 0.15f;
                g.Drops.Add(d);
            }
        }

        void TourTick(Game g, Input input, GameWindow win)
        {
            int n = Stops.Length / 7;
            if (Program.tourUiOnly) while (stop < n && !((string)Stops[stop * 7 + 6]).StartsWith("u")) stop++;
            if (stop >= n) { Next(); return; }
            int b = stop * 7;
            string name = (string)Stops[b + 6];
            bool uiStop = name.StartsWith("u");
            if (g.P != null && !(name == "u09_death" && phaseTick > 2)) { g.P.HP = g.P.MaxHP; g.P.Res = g.P.MaxRes; }
            if (uiStop)
            {
                if (phaseTick == 1)
                {
                    g.CloseOverlay();
                    if (!g.TourTo((string)Stops[b], (string)Stops[b + 1], (float)Stops[b + 2], (float)Stops[b + 3], (float)Stops[b + 4], (float)Stops[b + 5]))
                        Say("tour: no landmark " + Stops[b + 1]);
                }
                if (phaseTick == 12) OpenUiStop(g, input, name);
                if (phaseTick == 30 && name == "u01_inventory") HoverUi(g, input, "bag0");
                if (phaseTick == 30 && name == "u03_skills") { input.MX = 200; input.MY = 140; }
                if (phaseTick == (name == "u09_death" ? 150 : 50)) Capture(win, g, name);
                if (phaseTick == (name == "u09_death" ? 160 : 60))
                {
                    input.MX = 320; input.MY = 180;
                    if (name == "u09_death") { stop = n; phaseTick = 0; return; }
                    if (g.Ov == Overlay.Dialogue) g.CloseDialogue(); else if (g.Ov == Overlay.Settings) g.Ov = Overlay.None; else g.CloseOverlay();
                    stop++; phaseTick = 0;
                }
                return;
            }
            if (phaseTick == 1)
            {
                if (!g.TourTo((string)Stops[b], (string)Stops[b + 1], (float)Stops[b + 2], (float)Stops[b + 3], (float)Stops[b + 4], (float)Stops[b + 5]))
                    Say("tour: no landmark " + Stops[b + 1]);
            }
            if (phaseTick == 2 && name.EndsWith("_loot")) ShowLoot(g, name == "t09_loot" ? 0 : (name == "t10_loot" ? 1 : 2));
            if (phaseTick == 40) Capture(win, g, name);
            if (phaseTick > 44 && phaseTick < 60) input.SetMouse(0, true);
            if (phaseTick == 52) Capture(win, g, name + "_attack");
            if (phaseTick == 60) input.SetMouse(0, false);
            if (phaseTick == 90) { stop++; phaseTick = 0; }
        }

        int slowLogged, capFrames;
        bool capFrame;

        public void FrameMs(double ms, double render, double scene, double composite)
        {
            if (capFrame) { capFrame = false; capFrames++; return; }      // saving a screenshot is not the game's time
            msSum += ms; msCount++; if (ms > msMax) msMax = ms; msRender += render; msScene += scene; msComp += composite;
            if (ms > 34 && slowLogged < 16)
            {
                slowLogged++;
                Say("slow frame " + msCount + " (phase " + phase + ", stop " + stop + ", tick " + phaseTick + "): " + ms.ToString("0.0") + " ms; render " + render.ToString("0.0") + ", 3D " + scene.ToString("0.0") + ", composite " + composite.ToString("0.0"));
            }
        }

        void Say(string s) { log.AppendLine(s); }
        public void Note(string s) { Say(s); }

        void Next() { phase++; phaseTick = 0; }

        void Capture(GameWindow win, Game g, string name)
        {
            try
            {
                capFrame = true;
                if (win.Gl) win.CapturePath = Path.Combine(dir, name + "_window.png");      // saved as the frame is shown
                else
                {
                    g.Final.SavePng(Path.Combine(dir, name + "_frame.png"), 1);
                    using (Bitmap b = win.CaptureClient()) b.Save(Path.Combine(dir, name + "_window.png"), ImageFormat.Png);
                }
                Say("captured " + name);
            }
            catch (Exception ex) { Say("capture failed " + name + ": " + ex.Message); }
        }

        bool ClickUi(Game g, Input input, string id)
        {
            int[] r;
            if (!g.UiRects.TryGetValue(id, out r)) return false;
            input.MX = r[0] + r[2] / 2; input.MY = r[1] + r[3] / 2;
            input.SetMouse(0, true);
            releaseAt = tick + 2;
            return true;
        }

        public void Tick(Game g, Input input, GameWindow win, AudioEngine audio)
        {
            tick++; phaseTick++;
            if (tick == releaseAt) input.SetMouse(0, false);
            if (tick > 60 * 90) { Say("TIMEOUT in phase " + phase); Finish(g, audio, false); return; }
            switch (phase)
            {
                case 0:
                    if (g.Scr == Screen.Title && phaseTick > 30) { Say("reached title after " + tick + " ticks"); Capture(win, g, "01_title"); Next(); }
                    break;
                case 1:
                    if (phaseTick == 20) { if (!ClickUi(g, input, "New Game")) Say("FAIL: New Game button missing"); }
                    if (g.Scr == Screen.ClassSelect && phaseTick > 40) { Capture(win, g, "02_class"); Next(); }
                    break;
                case 2:
                    if (phaseTick == 15) ClickUi(g, input, "class:" + (tour ?? "Ranger"));
                    if (phaseTick == 40) Capture(win, g, "03_class_ranger");
                    if (phaseTick == 55) ClickUi(g, input, "Begin the Vigil");
                    if (g.Scr == Screen.Play && g.P != null && phaseTick > 75) { Say("in game as " + g.P.Def.Name + (g.FirstPerson ? ", first person" : (g.R3 != null ? ", overhead 3D" : ", 2D"))); Next(); }
                    break;
                case 3:
                    if (tour != null) { TourTick(g, input, win); break; }
                    // each capture is taken a few ticks before the next input, so the saved frame shows what it is named for
                    if (phaseTick == 60) Capture(win, g, "04_play");
                    input.SetKey(Key.D, phaseTick > 70 && phaseTick < 130);
                    input.SetKey(Key.W, phaseTick > 130 && phaseTick < 170);
                    if (phaseTick == 174) yaw0 = g.LookYaw;
                    if (phaseTick > 175 && phaseTick <= 205) { input.MDX += 12; input.MDY -= 2; }
                    if (phaseTick == 208 && g.FirstPerson) Say("mouse look turned the view by " + M.AngDiff(yaw0, g.LookYaw).ToString("0.00") + " rad (expected 0.54)");
                    if (phaseTick == 210) Capture(win, g, "05_play_look");
                    if (phaseTick > 215 && phaseTick < 262) { if (!g.FirstPerson) { input.MX = 500; input.MY = 150; } input.SetMouse(0, true); }
                    if (phaseTick == 262) { input.SetMouse(0, false); input.SetKey(Key.Q, true); }
                    if (phaseTick == 264) input.SetKey(Key.Q, false);
                    if (phaseTick == 280) input.SetKey(Key.Space, true);
                    if (phaseTick == 282) input.SetKey(Key.Space, false);
                    if (phaseTick == 300) Capture(win, g, "06_play_moved");
                    if (phaseTick == 315) input.SetKey(Key.I, true);
                    if (phaseTick == 317) input.SetKey(Key.I, false);
                    if (phaseTick == 345) { Capture(win, g, "07_inventory"); Say("overlay=" + g.Ov); }
                    if (phaseTick == 350) input.SetKey(Key.Esc, true);
                    if (phaseTick == 352) input.SetKey(Key.Esc, false);
                    if (phaseTick == 380) Next();
                    break;
                default:
                    Finish(g, audio, g.Scr == Screen.Play && g.P != null);
                    break;
            }
        }

        void Finish(Game g, AudioEngine audio, bool ok)
        {
            Say("audio: " + (audio == null ? "muted" : audio.Status));
            Say("render+present ms: avg " + (msCount > 0 ? (msSum / msCount).ToString("0.00") : "n/a") + ", max " + msMax.ToString("0.00") + " over " + msCount + " frames (" + capFrames + " screenshot frames not counted)");
            if (msCount > 0)
                Say("  of which 3D scene " + (msScene / msCount).ToString("0.00") + ", interface " + ((msRender - msScene) / msCount).ToString("0.00")
                    + ", composite+swap " + ((msSum - msRender) / msCount).ToString("0.00") + " (upload " + (msComp / msCount).ToString("0.00") + ", interface x" + g.Final.S + ")");
            if (msCount > 0)
            {
                string parts = "  interface parts:";
                for (int i = 0; i < Prof.Name.Length; i++) parts += " " + Prof.Name[i] + " " + (Prof.Sum[i] / msCount).ToString("0.00");
                Say(parts);
            }
            Say(ok ? "SMOKE PASS" : "SMOKE FAIL");
            try { File.WriteAllText(Path.Combine(dir, "smoke.log"), log.ToString()); } catch { }
            ExitCode = ok ? 0 : 1;
            g.WantQuit = true;
        }
    }
}
