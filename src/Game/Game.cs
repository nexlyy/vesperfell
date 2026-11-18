using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Vesperfell
{
    public enum Screen { Loading, Title, ClassSelect, Play }

    public sealed class Settings
    {
        public float Master = 0.9f, Music = 0.7f, Sfx = 0.8f, Amb = 0.7f;
        public bool Fullscreen = true, Shake = true, DamageNumbers = true, ShowFps = false, AutoPotionHint = true;
        public int WindowScale = 2;
        public string Language = Loc.DefaultCode();
        public string PlayerName = DefaultName();
        public string LastServer = "";
        public int Port = 27015;
        public string Renderer = "3d";          // "3d" (OpenGL) or "2d" (the software renderer); takes effect at the next start
        public string Camera = "fp";            // 3D view: "fp" through the hero's eyes, "top" from above
        public float MouseSens = 1f;            // mouse look speed (1 = 0.0015 rad per count)
        public bool InvertY;

        static string DefaultName()
        {
            try { string n = Environment.UserName; if (!string.IsNullOrEmpty(n)) return n.Length > 14 ? n.Substring(0, 14) : n; } catch { }
            return "Warden";
        }

        public void Load(string path)
        {
            try
            {
                if (!File.Exists(path)) { Loc.Set(Language); return; }
                foreach (string line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant(), v = line.Substring(eq + 1).Trim();
                    float f; float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
                    bool b = v == "1" || v.ToLowerInvariant() == "true";
                    switch (k)
                    {
                        case "master": Master = M.Clamp01(f); break;
                        case "music": Music = M.Clamp01(f); break;
                        case "sfx": Sfx = M.Clamp01(f); break;
                        case "ambience": Amb = M.Clamp01(f); break;
                        case "fullscreen": Fullscreen = b; break;
                        case "shake": Shake = b; break;
                        case "damagenumbers": DamageNumbers = b; break;
                        case "showfps": ShowFps = b; break;
                        case "windowscale": WindowScale = M.Clamp((int)f, 1, 8); break;
                        case "language": Language = v == "ru" ? "ru" : "en"; break;
                        case "name": if (v.Length > 0) PlayerName = v.Length > 14 ? v.Substring(0, 14) : v; break;
                        case "server": LastServer = v; break;
                        case "port": if ((int)f > 1024 && (int)f < 65535) Port = (int)f; break;
                        case "renderer": Renderer = v.ToLowerInvariant() == "2d" ? "2d" : "3d"; break;
                        case "camera": Camera = v.ToLowerInvariant() == "top" ? "top" : "fp"; break;
                        case "mousesens": if (f > 0) MouseSens = M.Clamp(f, 0.2f, 3f); break;
                        case "inverty": InvertY = b; break;
                    }
                }
            }
            catch { }
            Loc.Set(Language);
        }

        public void Save(string path)
        {
            try
            {
                CultureInfo ci = CultureInfo.InvariantCulture;
                File.WriteAllLines(path, new[]
                {
                    "; Vesperfell settings",
                    "master=" + Master.ToString("0.00", ci), "music=" + Music.ToString("0.00", ci), "sfx=" + Sfx.ToString("0.00", ci), "ambience=" + Amb.ToString("0.00", ci),
                    "fullscreen=" + (Fullscreen ? 1 : 0), "windowscale=" + WindowScale, "shake=" + (Shake ? 1 : 0),
                    "damagenumbers=" + (DamageNumbers ? 1 : 0), "showfps=" + (ShowFps ? 1 : 0),
                    "language=" + Language, "name=" + PlayerName, "server=" + LastServer, "port=" + Port, "renderer=" + Renderer,
                    "camera=" + Camera, "mousesens=" + MouseSens.ToString("0.00", ci), "inverty=" + (InvertY ? 1 : 0),
                }, System.Text.Encoding.UTF8);
            }
            catch { }
        }
    }

    /// <summary>The whole game: state, simulation and drawing. Split across several files by concern.</summary>
    public sealed partial class Game
    {
        public const int W = 640, H = 360;
        public const float DT = 1f / 60f;

        public readonly Input In;
        public readonly Surface Scene = new Surface(W, H);
        /// <summary>The frame as shown: the interface (and in the flat view the lit world) at the window's own resolution.</summary>
        public Surface Final = new Surface(W, H);
        readonly Surface lit1 = new Surface(W, H);
        /// <summary>Time the last frame spent handing the 3D scene to OpenGL (for the frame-time breakdown).</summary>
        public double Ms3D;
        /// <summary>Draw the interface in software even where the graphics card could (VF_SOFTUI=1, for comparisons).</summary>
        public static bool SoftUi = Environment.GetEnvironmentVariable("VF_SOFTUI") == "1";

        /// <summary>Matches the interface to the window: s physical pixels per interface pixel (1 = the classic 640x360 look).</summary>
        public void SetUiScale(int s)
        {
            s = M.Clamp(s, 1, 4);
            if (Final.S == s && Fonts.Scale == s) return;
            if (Final.S != s) Final = new Surface(W, H, s);
            Fonts.SetScale(s);
        }
        public readonly Lighting Light = new Lighting();
        public Rng Rnd = new Rng(Environment.TickCount);
        public Settings Cfg = new Settings();
        public AudioEngine Audio;
        public bool Headless, WantQuit, WantApplyDisplay;
        /// <summary>The OpenGL renderer, or null when the world is drawn by the software renderer.</summary>
        public Renderer3D R3;
        public string GfxNote = "";
        public string DataDir;
        public float Fps;

        public Screen Scr = Screen.Loading;
        public Map Over, Abbey, Cur;
        public Player P;
        public readonly List<Enemy> Foes = new List<Enemy>();
        public readonly List<Npc> Npcs = new List<Npc>();
        public readonly List<Projectile> Shots = new List<Projectile>();
        public readonly List<Area> Areas = new List<Area>();
        public readonly List<Pickup> Drops = new List<Pickup>();
        public readonly List<Floater> Floats = new List<Floater>();
        public readonly Particle[] Parts = new Particle[1800];
        public int PartCount;

        public float CamX, CamY; public int CamXi, CamYi;
        public float ShakeT, ShakeAmp, HitStop, FlashT, FlashDur = 0.2f; public int FlashColor;
        public float WorldT, UiT; public double PlayTime;
        public QuestLog Q = new QuestLog();
        public HashSet<string> Flags = new HashSet<string>();
        public bool Dawn; public float DawnT;
        public int ZoneId = -1; public float ZoneBannerT = 99; public string ZoneBannerName = "", ZoneBannerSub = "";
        public float AmbR = 0.5f, AmbG = 0.55f, AmbB = 0.8f, FogD = 0.3f; public int FogCol = 0x8FA0C8;
        public float CombatLevel;
        public Enemy Boss; public float BossIntroT = 99; public bool BossFight;
        float flowT;
        public float FadeT; public int FadeDir; Action fadeAction; const float FadeDur = 0.4f;
        bool mapsDirty;

        // loading jobs
        readonly List<Action> loadJobs = new List<Action>();
        readonly List<string> loadLabels = new List<string>();
        int loadIndex; bool loadPrimed; Action loadDone;
        public float LoadProgress; public string LoadLabel = ""; public int LoadTip;

        public Game(Input input, bool headless)
        {
            In = input; Headless = headless;
            DataDir = AppDomain.CurrentDomain.BaseDirectory;
        }

        // loading
        void Queue(string label, Action job) { loadLabels.Add(label); loadJobs.Add(job); }

        void BeginLoading(Action done)
        {
            loadIndex = 0; loadPrimed = false; loadDone = done;
            LoadProgress = 0; LoadLabel = loadLabels.Count > 0 ? loadLabels[0] : "";
            LoadTip = Rnd.Int(Lore.Tips.Length);
            Scr = Screen.Loading;
        }

        public void StartInitialLoad()
        {
            loadJobs.Clear(); loadLabels.Clear();
            Queue("Sharpening steel", delegate { Assets.Build(null); });
            Queue("Charting the vale", delegate { Over = WorldGen.BuildOverworld(null); Over.AssignUids(); });
            Queue("Descending into the undercroft", delegate { Abbey = DungeonGen.BuildAbbey(null); Abbey.AssignUids(); });
            Queue("Tuning the bell", delegate { if (Audio != null) Audio.BuildBank(); });
            Queue("Raising the vale", delegate { Prepare3D(); });
            BeginLoading(delegate
            {
                Cfg.Load(Path.Combine(DataDir, "settings.ini"));
                ApplyAudioVolumes();
                WantApplyDisplay = true;
                GoTitle();
            });
        }

        void UpdateLoading()
        {
            // one frame is drawn with the label before each blocking job runs
            if (!loadPrimed) { loadPrimed = true; return; }
            if (loadIndex < loadJobs.Count)
            {
                loadJobs[loadIndex]();
                loadIndex++;
                LoadProgress = loadIndex / (float)loadJobs.Count;
                LoadLabel = loadIndex < loadLabels.Count ? loadLabels[loadIndex] : "Ready";
                loadPrimed = Headless;      // headless runs do not need the extra frame
                return;
            }
            Action d = loadDone; loadDone = null;
            loadJobs.Clear(); loadLabels.Clear();
            if (d != null) d();
        }

        void Prepare3D()
        {
            if (R3 == null) return;
            R3.Prepare(Over); R3.Prepare(Abbey);
        }

        void RebuildMapsThen(Action done)
        {
            loadJobs.Clear(); loadLabels.Clear();
            if (mapsDirty || Over == null)
            {
                Queue("The dusk gathers", delegate { Over = WorldGen.BuildOverworld(null); Over.AssignUids(); });
                Queue("The abbey waits", delegate { Abbey = DungeonGen.BuildAbbey(null); Abbey.AssignUids(); });
                Queue("Raising the vale", delegate { Prepare3D(); });
            }
            else Queue("The dusk gathers", delegate { });
            BeginLoading(done);
        }

        // session
        public void GoTitle()
        {
            NetStop();
            Scr = Screen.Title;
            Ov = Overlay.None;
            P = null; Me = null; Party.Clear(); Boss = null; BossFight = false;
            ClearFoes(); Npcs.Clear(); Shots.Clear(); Areas.Clear(); Drops.Clear(); Floats.Clear(); PartCount = 0;
            Cur = Over;
            Dawn = false;
            titleCam = 0; menuSel = 0; titleSettings = false; titleJoin = false; chatOpen = false;
            hasSave = HasSave();
            ZoneId = -1;
            if (Cur != null) { CamX = 78 * 16; CamY = 6 * 16; }
            SetMusic(Zones.MusMenu);
        }

        public void StartNewGame(ClassId cls)
        {
            RebuildMapsThen(delegate
            {
                mapsDirty = true;
                Flags.Clear(); Dawn = false; DawnT = 0; PlayTime = 0; WorldT = 0;
                Q = new QuestLog();
                P = Me = CreatePlayer(cls);
                Me.NetName = CleanName(Cfg.PlayerName);
                RebuildParty();
                Marker s = Over.Find("start");
                P.RespawnMap = "overworld"; P.RespawnX = s.X; P.RespawnY = s.Y;
                Scr = Screen.Play; Ov = Overlay.None;
                EnterMap(Over, s.X, s.Y);
                ApplyFlags(Over); ApplyFlags(Abbey);
                Q.Start("bell", this);
                FadeT = FadeDur; FadeDir = -1;
                IntroT = 0; hintStep = 0;
                Notify("^yF^0 to speak  -  ^yI^0 inventory  -  ^yJ^0 journal");
            });
        }

        public Player CreatePlayer(ClassId cls)
        {
            Player p = new Player();
            p.Class = cls; p.Def = Classes.Get(cls);
            p.Spr = Assets.Hero[(int)cls];
            p.Name = p.Def.Name;
            p.Might = p.Def.Might; p.Finesse = p.Def.Finesse; p.Arcana = p.Def.Arcana; p.Vigor = p.Def.Vigor;
            p.Equip[Player.SlotWeapon] = Items.Starter(cls, true);
            p.Equip[Player.SlotArmor] = Items.Starter(cls, false);
            p.HealthPotions = 3; p.SpiritPotions = 1; p.Gold = 15;
            p.DodgeCharges = p.Def.DodgeCharges;
            p.Radius = 5;
            p.Recalc(true);
            return p;
        }

        public Map MapById(string id) { return id == "abbey" ? Abbey : Over; }

        public void EnterMap(Map m, float x, float y)
        {
            bool changed = Cur != m;
            Cur = m;
            ClearFoes(); Npcs.Clear(); Shots.Clear(); Areas.Clear(); Drops.Clear(); Floats.Clear(); PartCount = 0;
            Boss = null; BossFight = false;
            P = Me;
            P.X = x; P.Y = y; P.KX = P.KY = 0; P.ClearStatus();
            P.DashT = 0; P.DodgeT = 0; P.AtkT = 0; P.WhirlT = 0;
            if (!IsClient) SpawnAll();          // a guest's enemies arrive from the host
            foreach (NpcDef n in m.NpcDefs)
            {
                Npc a = new Npc();
                a.Id = n.Id; a.Name = n.Name; a.Spr = n.Spr; a.X = n.X; a.Y = n.Y; a.HomeX = n.X; a.HomeY = n.Y; a.Wander = n.Wander;
                a.Facing = n.FaceLeft ? -1 : 1; a.AnimT = Rnd.F() * 3; a.Radius = 5;
                Npcs.Add(a);
            }
            SnapCamera();
            FaceOnArrival();
            ZoneId = -1; UpdateZone(true);
            m.FlowValid = false; flowT = 0;
            m.Reveal(P.X, P.Y, 9);
            NetMapEntered(changed);
        }

        void SpawnAll()
        {
            foreach (SpawnDef s in Cur.Spawns)
            {
                if (s.Once && Flags.Contains("dead_" + s.Id)) continue;
                SpawnEnemy(s.Kind, s.X, s.Y, s.Level, s);
            }
        }

        public Enemy SpawnEnemy(EnemyKind kind, float x, float y, int level, SpawnDef src)
        {
            EnemyDef d = Enemies.Get(kind);
            Enemy e = new Enemy();
            e.Def = d; e.Kind = kind; e.Level = level; e.Spawn = src; e.Name = d.Name;
            float scale = 1f + 0.2f * (level - d.Level);
            if (scale < 0.6f) scale = 0.6f;
            e.MaxHP = e.HP = (float)Math.Round(d.HP * scale * (IsClient ? 1f : coopScale));
            e.Dmg = d.Dmg * (1f + 0.1f * (level - d.Level));
            e.Speed = d.Speed; e.Radius = d.Radius;
            e.X = x; e.Y = y;
            Cur.FindFree(ref e.X, ref e.Y, e.Radius, d.Flying ? 2 : 1);
            e.HomeX = e.X; e.HomeY = e.Y; e.LastX = e.X; e.LastY = e.Y;
            e.Variant = src != null ? src.Variant : Rnd.Int(3);
            switch (kind)
            {
                case EnemyKind.Wolf: e.Spr = Assets.Wolf; break;
                case EnemyKind.AlphaWolf: e.Spr = Assets.AlphaWolf; break;
                case EnemyKind.Crawler: e.Spr = Assets.Crawler; break;
                case EnemyKind.Hollow: e.Spr = Assets.Hollow[e.Variant % 3]; break;
                case EnemyKind.Archer: e.Spr = Assets.Skeleton; break;
                case EnemyKind.Wisp: e.Spr = Assets.Wisp; e.Z = 6; break;
                case EnemyKind.Acolyte: e.Spr = Assets.Acolyte; break;
                case EnemyKind.Knight: e.Spr = Assets.Knight; break;
                case EnemyKind.Prior: e.Spr = Assets.Prior; break;
            }
            e.Facing = Rnd.Chance(0.5f) ? 1 : -1;
            e.AnimT = Rnd.F() * 4; e.WanderT = Rnd.F(1, 4); e.AtkCd = Rnd.F(0.4f, 1.4f);
            e.NetId = netSpawnId != 0 ? netSpawnId : nextFoeId++;
            foeById[e.NetId] = e;
            Foes.Add(e);
            return e;
        }

        // main update
        public void Update(float dt)
        {
            UiT += dt;
            if (FlashT > 0) FlashT -= dt;
            NetPump(dt);
            switch (Scr)
            {
                case Screen.Loading: UpdateLoading(); break;
                case Screen.Title: UpdateTitle(dt); break;
                case Screen.ClassSelect: UpdateClassSelect(dt); break;
                case Screen.Play: UpdatePlay(dt); break;
            }
            UpdateFade(dt);
            UpdateAudio(dt);
            NetFlush();
            if (In.Pressed[Key.F11] || (In.Down[Key.Alt] && In.Pressed[Key.Enter])) { Cfg.Fullscreen = !Cfg.Fullscreen; WantApplyDisplay = true; SaveSettings(); }
        }

        void UpdateFade(float dt)
        {
            if (FadeDir == 0) return;
            if (FadeDir > 0)
            {
                FadeT += dt;
                if (FadeT >= FadeDur)
                {
                    FadeT = FadeDur; FadeDir = -1;
                    Action a = fadeAction; fadeAction = null;
                    if (a != null) a();
                }
            }
            else
            {
                FadeT -= dt;
                if (FadeT <= 0) { FadeT = 0; FadeDir = 0; }
            }
        }

        /// <summary>Fades to black, runs the action, then fades back in.</summary>
        public void FadeThrough(Action mid)
        {
            if (FadeDir > 0) return;
            fadeAction = mid; FadeDir = 1;
            if (Headless) { FadeT = FadeDur; }
        }

        public bool Fading { get { return FadeDir > 0; } }

        void UpdatePlay(float dt)
        {
            if (P == null) return;
            UpdateOverlayInput();
            // a shared world cannot stop for one player's menu: online, only this hero stands still
            bool paused = !Online && (Ov != Overlay.None || Fading);
            if (Online) HitStop = 0;
            if (!paused)
            {
                PlayTime += dt;
                if (HitStop > 0) { HitStop -= dt; UpdateParticles(dt * 0.25f); }
                else UpdateWorld(dt);
            }
            else
            {
                // keep ambience moving behind menus
                UpdateParticles(dt * 0.2f);
            }
            UpdateOverlay(dt);
            UpdateNotices(dt);
            if (Online && Scr == Screen.Play && P != null) NetTick();
        }

        void UpdateWorld(float dt)
        {
            if (Party.Count == 0 || Party[0] != Me) RebuildParty();
            WorldT += dt;
            IntroT += dt;
            flowT -= dt;
            if (!IsClient && (flowT <= 0 || !Cur.FlowValid))
            {
                for (int i = 0; i < Party.Count; i++) { Cur.FlowSlot = Party[i].Slot; Cur.BuildFlow(Party[i].X, Party[i].Y); }
                Cur.FlowSlot = 0; flowT = 0.2f;
            }
            UpdatePlayer(dt);
            UpdateOthers(dt);
            if (IsClient) UpdateEnemiesClient(dt); else UpdateEnemies(dt);
            UpdateNpcs(dt);
            UpdateShots(dt);
            UpdateAreas(dt);
            UpdateDrops(dt);
            UpdateParticles(dt);
            UpdateFloaters(dt);
            UpdateProps(dt);
            UpdateAmbientFx(dt);
            UpdateZone(false);
            UpdateCamera(dt);
            UpdateInteraction();
            UpdateStory(dt);
            if (ShakeT > 0) ShakeT -= dt;
            Cur.Reveal(P.X, P.Y, 9);
            // combat music follows how many foes are actively hunting nearby
            float want = 0;
            for (int i = 0; i < Foes.Count; i++)
            {
                Enemy e = Foes[i];
                if (!e.Aggro || e.Dead) continue;
                if (Online && M.Dist2(e.X, e.Y, P.X, P.Y) > 340 * 340) continue;
                want += e.Def.Elite ? 0.8f : 0.34f;
            }
            if (BossFight) want = 1;
            CombatLevel = M.Approach(CombatLevel, M.Clamp01(want), dt * (want > CombatLevel ? 1.2f : 0.25f));
        }

        // zones & atmosphere
        public ZoneDef Zone { get { return Zones.All[M.Clamp(ZoneId, 0, Zones.All.Length - 1)]; } }

        void UpdateZone(bool snap)
        {
            int z = Cur.ZoneAt(P.X, P.Y);
            if (z != ZoneId)
            {
                bool first = ZoneId < 0;
                ZoneId = z;
                ZoneDef zd = Zones.All[z];
                if (!snap || first) { ZoneBannerT = 0; ZoneBannerName = zd.Name; ZoneBannerSub = Dawn && !zd.Indoor ? "the long dusk has lifted" : zd.Sub; }
                if (!Flags.Contains("seen_zone" + z)) { Flags.Add("seen_zone" + z); if (!first && P != null) GainXp(20 + z * 4, false); }
                SetMusic(BossFight ? Zones.MusBoss : (Dawn && !zd.Indoor ? Zones.MusDawn : zd.Music));
            }
            ZoneDef d = Zones.All[ZoneId];
            float r = d.R, g = d.G, b = d.B, fog = d.Fog; int fc = d.FogColor;
            if (Dawn && !d.Indoor) { r = 1.02f; g = 0.9f; b = 0.76f; fog = d.Fog * 0.22f; fc = 0xFFE8C0; }
            float k = snap ? 1f : M.Damp(1.2f, DT);
            AmbR = M.Lerp(AmbR, r, k); AmbG = M.Lerp(AmbG, g, k); AmbB = M.Lerp(AmbB, b, k); FogD = M.Lerp(FogD, fog, k);
            FogCol = Col.Lerp(FogCol | Col.Opaque, fc | Col.Opaque, k) & 0xFFFFFF;
            ZoneBannerT += DT;
        }

        // camera
        void SnapCamera()
        {
            fpGround = float.NaN;
            CamX = P.X - W / 2; CamY = P.Y - 12 - H / 2;
            ClampCamera();
        }

        void ClampCamera()
        {
            float maxX = Cur.PW - W, maxY = Cur.PH - H;
            if (CamX < 0) CamX = 0; if (CamY < 0) CamY = 0;
            if (CamX > maxX) CamX = maxX; if (CamY > maxY) CamY = maxY;
            CamXi = (int)Math.Round(CamX); CamYi = (int)Math.Round(CamY);
        }

        void UpdateCamera(float dt)
        {
            // look slightly toward the cursor so ranged classes can see what they aim at
            float lookX = (In.MX - W / 2) * 0.16f, lookY = (In.MY - H / 2) * 0.16f;
            float tx = P.X + lookX - W / 2, ty = P.Y - 12 + lookY - H / 2;
            float k = M.Damp(9f, dt);
            CamX += (tx - CamX) * k; CamY += (ty - CamY) * k;
            ClampCamera();
            if (ShakeT > 0 && Cfg.Shake)
            {
                float a = ShakeAmp * M.Clamp01(ShakeT / 0.25f);
                CamXi += (int)Math.Round(Rnd.Sym(a)); CamYi += (int)Math.Round(Rnd.Sym(a));
            }
            if (R3 != null)
            {
                R3.TitleMode = false;
                if (FirstPerson) FeedFpCamera(dt);
                else
                {
                    R3.FirstPerson = false;
                    if (In.Wheel != 0 && Ov == Overlay.None) R3.ZoomTarget = M.Clamp(R3.ZoomTarget - In.Wheel * 0.08f, 0.7f, 1.25f);
                }
                R3.UpdateCamera(dt);
            }
        }

        public void Shake(float amp, float time) { if (amp >= ShakeAmp || ShakeT <= 0) { ShakeAmp = amp; ShakeT = time; } }
        public void Flash(int color, float dur) { FlashColor = color; FlashT = dur; FlashDur = dur; }

        /// <summary>Moves the player instantly (used by the automated playtest).</summary>
        public void DebugTeleport(float x, float y)
        {
            P.X = x; P.Y = y; P.KX = P.KY = 0; P.DashT = 0; P.DodgeT = 0;
            Cur.FindFree(ref P.X, ref P.Y, P.Radius, 1);
            SnapCamera();
            Cur.FlowValid = false;
        }

        /// <summary>The point on the ground under the cursor.</summary>
        public float MouseWX { get { float x, y; MouseGround(out x, out y); return x; } }
        public float MouseWY { get { float x, y; MouseGround(out x, out y); return y; } }

        void MouseGround(out float x, out float y)
        {
            if (R3 == null) { x = In.MX + CamXi; y = In.MY + CamYi; return; }
            if (FirstPerson && P != null) { x = AimGX; y = AimGY; return; }
            float mx = In.MX + 0.5f, my = In.MY + 0.5f;
            float h = P != null ? R3.HeightAt(P.X, P.Y) : 0;
            x = P != null ? P.X : CamX + W / 2; y = P != null ? P.Y : CamY + H / 2;
            // the ground is not flat: settle on the height under the hit point
            for (int i = 0; i < 3; i++)
            {
                if (!R3.Pick(mx, my, h, out x, out y)) break;
                h = R3.HeightAt(x, y);
            }
        }

        /// <summary>
        /// Where the hero aims: the flat view aims at the body drawn 9 px above the feet, the 3D view at the point where the
        /// cursor ray crosses chest height above the hero's ground.
        /// </summary>
        public void MouseAim(out float x, out float y)
        {
            if (R3 == null) { x = In.MX + CamXi; y = In.MY + CamYi + 9; return; }
            float h = (P != null ? R3.HeightAt(P.X, P.Y) : 0) + 12;
            if (!R3.Pick(In.MX + 0.5f, In.MY + 0.5f, h, out x, out y)) { x = P != null ? P.X + 1 : 0; y = P != null ? P.Y : 0; }
        }

        // audio glue
        int musicWanted = -1;
        public void SetMusic(int track) { musicWanted = track; if (Audio != null) Audio.SetMusic(track); }

        public void Snd(int id) { if (Audio != null) Audio.Play(id, 1f, 0f, 1f); }
        public void Snd(int id, float vol, float pitch) { if (Audio != null) Audio.Play(id, vol, 0f, pitch); }

        /// <summary>Positional sound: pans and fades with distance from the player.</summary>
        public void SndAt(int id, float x, float y, float vol)
        {
            if (Audio == null || Me == null) return;
            float dx = x - Me.X, dy = y - Me.Y;
            float d = M.Len(dx, dy);
            if (d > 340) return;
            float att = 1f - d / 340f;
            Audio.Play(id, vol * att * att, M.Clamp(dx / 220f, -1f, 1f), 0.94f + Rnd.F() * 0.12f);
        }

        public void ApplyAudioVolumes()
        {
            if (Audio == null) return;
            Audio.VolMaster = Cfg.Master; Audio.VolMusic = Cfg.Music; Audio.VolSfx = Cfg.Sfx; Audio.VolAmb = Cfg.Amb;
        }

        void UpdateAudio(float dt)
        {
            if (Audio == null) return;
            if (Scr == Screen.Play && P != null && Cur != null)
            {
                // nearest fire and water feed positional ambience layers
                float fire = 0, water = 0;
                List<Prop> near = scratchProps; near.Clear();
                Cur.Query(P.X - 200, P.Y - 200, P.X + 200, P.Y + 200, near);
                for (int i = 0; i < near.Count; i++)
                {
                    Prop p = near[i];
                    if (p.LightR < 60 || !p.Flicker) continue;
                    float d = M.Dist(p.X, p.Y, P.X, P.Y);
                    float v = 1f - d / 190f;
                    if (v > fire) fire = v;
                }
                for (int k = 0; k < 8; k++)
                {
                    float a = k * M.TAU / 8;
                    for (int s = 1; s <= 4; s++)
                        if (Cur.TerrainAt(P.X + M.Cos(a) * s * 36, P.Y + M.Sin(a) * s * 36) == T.Water) { water = Math.Max(water, 1f - s * 0.22f); break; }
                }
                ZoneDef z = Zone;
                Audio.SetAmbience(Dawn && !z.Indoor ? Zones.AmbField : z.Amb, M.Clamp01(fire), M.Clamp01(water), Dawn && !z.Indoor);
                Audio.SetCombat(Ov == Overlay.Death ? 0 : CombatLevel);
            }
            else
            {
                Audio.SetAmbience(Zones.AmbField, 0, 0, false);
                Audio.SetCombat(0);
            }
        }

        readonly List<Prop> scratchProps = new List<Prop>(512);

        public void SaveSettings() { Cfg.Save(Path.Combine(DataDir, "settings.ini")); ApplyAudioVolumes(); }
    }
}
