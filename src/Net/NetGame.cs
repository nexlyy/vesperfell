using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Vesperfell
{
    public enum NetMode { Off, Host, Client }

    /// <summary>One connected guest, as the host sees them.</summary>
    public sealed class Peer
    {
        public Conn C; public int Id = -1; public string Name = "";
        public Player Pl;               // null until the guest has chosen a class and stepped into the world
        public float IdleT;
    }

    /// <summary>
    /// Co-operative play for up to four. One player hosts their own world and the others join it as guests.
    /// The host decides everything about the world (enemies, quests, doors, chests). Every machine looks after its
    /// own hero, reports the blows it lands, and rolls its own loot.
    /// </summary>
    public sealed partial class Game
    {
        public const int NetVersion = 1, MaxParty = 4, DefaultPort = 27015;
        // guest -> host
        const int CHello = 1, CEnter = 2, CState = 3, CHit = 4, CStatus = 5, CShot = 6, CArea = 7, CUse = 8, CHitProp = 9, CQuest = 10, CKeep = 11, CChat = 12;
        // host -> guest
        const int SHelloOk = 20, SKick = 21, SWelcome = 22, SSnap = 23, SFoeDied = 24, SDamage = 25, SPropUsed = 26, SPropBroken = 27, SQuests = 28,
            SMap = 29, SFoesReset = 30, SJoined = 31, SLeft = 32, SNotice = 33, SShot = 34, SArea = 35, SKeep = 36, SChat = 37, SFx = 38, SPull = 39;
        public const int JoinIdle = 0, JoinConnecting = 1, JoinLobby = 2, JoinEntering = 3, JoinPlaying = 4;
        public const int FxSlam = 1, FxToll = 2, FxSummon = 3, FxRoar = 4, FxStagger = 5;

        public NetMode Net = NetMode.Off;
        public bool IsHost { get { return Net == NetMode.Host; } }
        public bool IsClient { get { return Net == NetMode.Client; } }
        public bool Online { get { return Net != NetMode.Off; } }
        public Player Me;                                               // the hero played on this machine
        public int MyNetId;
        public readonly List<Player> Others = new List<Player>();       // everyone else's heroes, as reported over the network
        public readonly List<Player> Party = new List<Player>();        // Me first, then Others
        public int JoinState; public string JoinError = "", HostName = ""; public int HostLevel = 1, HostPlayers = 1;
        public List<string> HostAddresses = new List<string>();
        public Func<string> ClipboardGet;
        public int GuestCount { get { int n = 0; foreach (Peer p in peers) if (p.Pl != null) n++; return n; } }

        readonly List<Peer> peers = new List<Peer>();
        TcpListener listener; Conn hostConn;
        volatile Socket joinSocket; volatile string joinFail; volatile bool joinDone; volatile int joinAttempt;
        float joinT; string kickReason; ClassId joinClass;
        readonly Dictionary<int, Enemy> foeById = new Dictionary<int, Enemy>();
        int nextFoeId = 1, netSpawnId, netTick;
        float coopScale = 1f, boltSndT, shotSndT;
        bool netApplying;                                               // true while applying news from the network, which must not be sent back out
        readonly List<Projectile> newShots = new List<Projectile>();
        readonly List<Area> newAreas = new List<Area>();
        readonly MsgW wr = new MsgW();

        // the party
        public List<Player> AllPlayers() { return Party; }

        public void RebuildParty()
        {
            Party.Clear();
            if (Me != null) { Me.Slot = 0; Me.Remote = false; Party.Add(Me); }
            for (int i = 0; i < Others.Count; i++) { Others[i].Slot = Math.Min(Map.FlowSlots - 1, i + 1); Party.Add(Others[i]); }
            if (!IsClient) RescaleFoes();
            if (Cur != null) Cur.FlowValid = false;
        }

        /// <summary>Enemies are hardier the more heroes there are to fight them.</summary>
        void RescaleFoes()
        {
            float want = 1f + 0.55f * Others.Count;
            if (Math.Abs(want - coopScale) < 0.001f) return;
            float k = want / coopScale;
            coopScale = want;
            for (int i = 0; i < Foes.Count; i++)
            {
                Enemy e = Foes[i];
                if (e.Dead) continue;
                e.MaxHP = (float)Math.Round(e.MaxHP * k); e.HP = Math.Max(1, (float)Math.Round(e.HP * k));
            }
        }

        public void ClearFoes() { Foes.Clear(); foeById.Clear(); }

        Player FindPlayer(int id)
        {
            for (int i = 0; i < Party.Count; i++) if (Party[i].NetId == id) return Party[i];
            return null;
        }

        Peer PeerOf(Player pl)
        {
            for (int i = 0; i < peers.Count; i++) if (peers[i].Pl == pl) return peers[i];
            return null;
        }

        Player NewProxy(int id, string name, ClassId cls)
        {
            Player p = new Player();
            p.Remote = true; p.NetId = id; p.NetName = name;
            p.Class = cls; p.Def = Classes.Get(cls); p.Spr = Assets.Hero[(int)cls]; p.Name = p.Def.Name;
            p.Radius = 5; p.HP = p.MaxHP = p.Def.BaseHP;
            Item w = new Item(); w.Kind = ItemKind.Weapon; w.Base = p.Def.WeaponTypes[0]; p.Equip[Player.SlotWeapon] = w;
            return p;
        }

        /// <summary>The enemy's quarry: the nearest hero it can see, with some loyalty to the one it is already hunting.</summary>
        Player PickTarget(Enemy e)
        {
            if (Party.Count <= 1) { e.Target = Me; return Me; }
            Player best = null; float bd = float.MaxValue;
            for (int i = 0; i < Party.Count; i++)
            {
                Player pl = Party[i];
                if (pl.Dead) continue;
                float d = M.Dist2(e.X, e.Y, pl.X, pl.Y);
                if (pl == e.Target) d *= 0.4f;
                if (pl.StealthT > 0) d += 1e9f;
                if (d < bd) { bd = d; best = pl; }
            }
            if (best == null) best = Me;
            e.Target = best;
            return best;
        }

        /// <summary>A guest's hero is brought up to the host's level, with gear to match, so friends can drop in at any point.</summary>
        void BoostGuest(Player p, int level)
        {
            level = M.Clamp(level, 1, Player.MaxLevel);
            for (int l = 2; l <= level; l++)
            {
                p.Level = l; p.AttrPoints += 3; p.Vigor++;
                if (p.Def.Primary == 0) p.Might++; else if (p.Def.Primary == 1) p.Finesse++; else p.Arcana++;
            }
            if (level >= 2)
            {
                Rng r = new Rng(Environment.TickCount ^ 0x5bd1e995);
                Rarity rar = level >= 6 ? Rarity.Rare : Rarity.Fine;
                p.Equip[Player.SlotWeapon] = Items.Weapon(r, p.Def.WeaponTypes[0], level, rar);
                p.Equip[Player.SlotArmor] = Items.Armor(r, p.Class, level, rar);
                if (level >= 4) p.Equip[Player.SlotRing] = Items.Trinket(r, true, level, Rarity.Fine);
                if (level >= 7) p.Equip[Player.SlotAmulet] = Items.Trinket(r, false, level, Rarity.Rare);
                p.HealthPotions = 4; p.SpiritPotions = 2; p.Gold = 20 + level * 15;
            }
            p.Recalc(true);
        }

        public static string CleanText(string s, int max)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length && sb.Length < max; i++)
            {
                char c = s[i];
                if (c < ' ' || c == '^' || c == 127 || c == Loc.RawMark) continue;
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        public static string CleanName(string s) { s = CleanText(s, 14); return s.Length == 0 ? "Warden" : s; }

        // hosting
        public bool NetHost()
        {
            if (Net != NetMode.Off || JoinState != JoinIdle || Scr != Screen.Play || Me == null) return false;
            try
            {
                // the automated playtest only ever talks to itself, so it listens on the loopback address alone
                if (Headless) { listener = new TcpListener(IPAddress.Loopback, Cfg.Port); listener.Start(); }
                else
                {
                    try { listener = new TcpListener(IPAddress.IPv6Any, Cfg.Port); listener.Server.DualMode = true; listener.Start(); }
                    catch { listener = new TcpListener(IPAddress.Any, Cfg.Port); listener.Start(); }
                }
            }
            catch (Exception ex)
            {
                listener = null;
                Notify(Loc.F("^rCould not open port {0}: {1}^0", Cfg.Port, ex.Message));
                Snd(Sfx.UiError);
                return false;
            }
            Net = NetMode.Host; MyNetId = 0; Me.NetId = 0; Me.NetName = CleanName(Cfg.PlayerName);
            HostAddresses = LocalAddresses();
            RebuildParty();
            return true;
        }

        static List<string> LocalAddresses()
        {
            List<string> l = new List<string>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string s = ua.Address.ToString();
                        if (s.StartsWith("169.254.") || l.Contains(s)) continue;
                        l.Add(s);
                    }
                }
            }
            catch { }
            return l;
        }

        /// <summary>The addresses friends can type in, one per line.</summary>
        public string HostAddressText()
        {
            string s = "";
            for (int i = 0; i < HostAddresses.Count && i < 4; i++)
                s += (s.Length > 0 ? "\n" : "") + "^y" + HostAddresses[i] + (Cfg.Port != DefaultPort ? ":" + Cfg.Port : "") + "^0";
            return s.Length > 0 ? s : "^y127.0.0.1^0";
        }

        /// <summary>Ends the session, whichever side of it this machine is on.</summary>
        public void NetStop()
        {
            joinAttempt++;
            if (listener != null) { try { listener.Stop(); } catch { } listener = null; }
            for (int i = 0; i < peers.Count; i++)
            {
                try { wr.Begin(SKick).Str("The host has closed the game."); peers[i].C.Send(wr); peers[i].C.Close(); } catch { }
            }
            peers.Clear();
            if (hostConn != null) { hostConn.Close(); hostConn = null; }
            Net = NetMode.Off; JoinState = JoinIdle; MyNetId = 0; kickReason = null;
            Others.Clear(); newShots.Clear(); newAreas.Clear();
            if (Me != null) Me.NetId = 0;
            RebuildParty();
        }

        int FreePeerId()
        {
            for (int id = 1; id < MaxParty; id++)
            {
                bool used = false;
                foreach (Peer p in peers) if (p.Id == id) used = true;
                if (!used) return id;
            }
            return -1;
        }

        string UniqueName(string name)
        {
            for (int n = 1; n < 9; n++)
            {
                string cand = n == 1 ? name : (name.Length > 12 ? name.Substring(0, 12) : name) + " " + n;
                bool clash = Me != null && string.Equals(Me.NetName, cand, StringComparison.OrdinalIgnoreCase);
                foreach (Peer p in peers) if (p.Id >= 0 && string.Equals(p.Name, cand, StringComparison.OrdinalIgnoreCase)) clash = true;
                if (!clash) return cand;
            }
            return name;
        }

        void Kick(Peer p, string reason)
        {
            wr.Begin(SKick).Str(reason); p.C.Send(wr);
            p.C.Close();
        }

        void SendAll(MsgW w, Peer except)
        {
            for (int i = 0; i < peers.Count; i++)
                if (peers[i].Pl != null && peers[i] != except) peers[i].C.Send(w);
        }

        void HostPump(float dt)
        {
            try
            {
                while (listener != null && listener.Pending())
                {
                    Peer np = new Peer(); np.C = new Conn(listener.AcceptSocket());
                    peers.Add(np);
                }
            }
            catch { }
            for (int i = peers.Count - 1; i >= 0; i--)
            {
                Peer p = peers[i];
                byte[] m; int guard = 0;
                while (guard++ < 800 && !p.C.Closed && p.C.TryRead(out m))
                {
                    try { HostMsg(p, new MsgR(m)); }
                    catch (Exception ex) { Log.Line("net: dropped a guest: " + ex.Message); p.C.Close(); }
                }
                p.IdleT += dt;
                if (p.Pl == null && p.IdleT > 240) p.C.Close();     // connected but never came in
                if (p.C.Closed) { peers.RemoveAt(i); PeerLeft(p); }
            }
        }

        void PeerLeft(Peer p)
        {
            if (p.Pl == null) return;
            Others.Remove(p.Pl);
            foreach (Enemy e in Foes) if (e.Target == p.Pl) e.Target = null;
            RebuildParty();
            wr.Begin(SLeft).Byte(p.Id); SendAll(wr, null);
            NoticeAll("^y{0}^0 has left the party.", p.Name);
        }

        void HostMsg(Peer p, MsgR r)
        {
            int type = r.Byte();
            if (p.Id < 0 && type != CHello) { p.C.Close(); return; }
            if (p.Pl == null && type != CHello && type != CEnter) return;
            p.IdleT = 0;
            switch (type)
            {
                case CHello:
                    {
                        if (p.Id >= 0) return;
                        int ver = r.Int(); string name = CleanName(r.Str());
                        if (ver != NetVersion) { Kick(p, "That copy of the game is a different version from the host's."); return; }
                        int id = FreePeerId();
                        if (id < 0) { Kick(p, "The party is full."); return; }
                        p.Id = id; p.Name = UniqueName(name);
                        wr.Begin(SHelloOk).Byte(id).Str(Me.NetName).Byte(Me.Level).Byte(Party.Count); p.C.Send(wr);
                    }
                    break;
                case CEnter:
                    {
                        if (p.Pl != null) return;
                        int cls = M.Clamp(r.Byte(), 0, 3);
                        Player pl = NewProxy(p.Id, p.Name, (ClassId)cls);
                        pl.X = pl.NetX = Me.X; pl.Y = pl.NetY = Me.Y + 10;
                        wr.Begin(SJoined).Byte(p.Id).Str(p.Name).Byte(cls); SendAll(wr, null);
                        SendWelcome(p);
                        NoticeAll("^g{0}^0 has joined the party.", p.Name);        // told to those already here; the newcomer gets a welcome of their own
                        p.Pl = pl; Others.Add(pl); RebuildParty();
                        Snd(Sfx.QuestStart, 0.7f, 1.3f);
                    }
                    break;
                case CState: ReadPlayerState(r, p.Pl); break;
                case CHit:
                    {
                        int id = r.Int(); int dmg = M.Clamp(r.Int(), 1, 20000);
                        float dx = M.Clamp(r.Float(), -1, 1), dy = M.Clamp(r.Float(), -1, 1), knock = M.Clamp(r.Float(), 0, 400);
                        int fx = r.Byte(); bool crit = r.Bool();
                        Enemy e;
                        if (!foeById.TryGetValue(id, out e) || e.Dead || e.State == Enemy.Vanish) return;
                        HitFx(e, fx, dx, dy, crit);
                        HurtEnemy(e, dmg, dx, dy, knock, p.Pl);
                    }
                    break;
                case CStatus:
                    {
                        int id = r.Int(); int kind = r.Byte(); float t = M.Clamp(r.Float(), 0, 12), pw = M.Clamp(r.Float(), 0, 600);
                        Enemy e;
                        if (foeById.TryGetValue(id, out e) && !e.Dead) ApplyStatus(e, kind, t, pw);
                    }
                    break;
                case CShot:
                    {
                        Projectile pr = ReadShot(r, true);
                        pr.OwnerId = p.Id;
                        wr.Begin(SShot); WriteShot(wr, pr, p.Id); SendAll(wr, p);
                    }
                    break;
                case CArea:
                    {
                        Area a = ReadArea(r, true);
                        wr.Begin(SArea); WriteArea(wr, a, p.Id); SendAll(wr, p);
                    }
                    break;
                case CUse:
                    {
                        int mapIx = r.Byte(); int uid = r.Int();
                        Map m = mapIx == 1 ? Abbey : Over; Prop q;
                        if (m != Cur || !m.ByUid.TryGetValue(uid, out q) || !q.Interactive || !m.Props.Contains(q)) return;
                        if (M.Dist2(q.X, q.Y, p.Pl.X, p.Pl.Y) > 140 * 140) return;
                        if (q.Kind == PropKind.Stairs || q.Kind == PropKind.AbbeyDoor || q.Kind == PropKind.Sign || q.Kind == PropKind.Well) return;
                        if (q.Kind == PropKind.Cache && !AllRunes) return;
                        if (q.Kind == PropKind.Bell && !Flags.Contains("dead_prior")) return;
                        UseProp(q, p.Id);
                    }
                    break;
                case CHitProp:
                    {
                        int mapIx = r.Byte(); int uid = r.Int(); int power = M.Clamp(r.Byte(), 1, 3);
                        Map m = mapIx == 1 ? Abbey : Over; Prop q;
                        if (m != Cur || !m.ByUid.TryGetValue(uid, out q) || !m.Props.Contains(q)) return;
                        if (q.Kind != PropKind.Breakable && q.Kind != PropKind.Bramble && q.Kind != PropKind.CrackedWall) return;
                        ShakeProp(q);
                        DamageProp(q, power);
                    }
                    break;
                case CQuest:
                    {
                        int op = r.Byte(); string id = r.Str(); int stage = r.Byte();
                        if (Q.Get(id) == null) return;
                        if (op == QuestLog.OpStart) Q.Start(id, this);
                        else if (op == QuestLog.OpStage) Q.SetStage(id, Math.Min(stage, 7), this);
                        else if (op == QuestLog.OpComplete) Q.Complete(id, this);
                    }
                    break;
                case CKeep:
                    {
                        string id = r.Str();
                        if (id != "sigil" && id != "tongue") return;
                        GrantKeepsake(id); RemoveQuestDrops(id);
                        Q.OnItem(id, this);
                        wr.Begin(SKeep).Str(id); SendAll(wr, p);
                    }
                    break;
                case CChat:
                    {
                        string t = CleanText(r.Str(), 120);
                        if (t.Length == 0) return;
                        ShowChat(p.Name, t);
                        wr.Begin(SChat).Str(p.Name).Str(t); SendAll(wr, p);
                    }
                    break;
            }
        }

        void SendWelcome(Peer p)
        {
            wr.Begin(SWelcome).Byte(p.Id).Str(Me.NetName).Byte(Me.Level).Byte(Cur == Abbey ? 1 : 0).Float(Me.X).Float(Me.Y).Bool(Dawn).Bool(BossFight);
            wr.Int(Over.Checksum).Int(Abbey.Checksum);
            int countAt = wr.N; wr.Short(0);
            int n = 0;
            foreach (string f in Flags)
            {
                // some flags are personal (signs read, wishes made, places seen): a guest earns those for themselves
                if (f.StartsWith("read_") || f.StartsWith("wish_") || f.StartsWith("seen_zone")) continue;
                wr.Str(f); n++;
            }
            wr.B[countAt] = (byte)n; wr.B[countAt + 1] = (byte)(n >> 8);
            WriteQuests(wr);
            wr.Byte(Me.Keepsakes.Count);
            foreach (Item k in Me.Keepsakes) { wr.Str(k.QuestId); wr.Byte(Math.Min(250, k.Count)); }
            wr.Byte(Party.Count);
            foreach (Player pl in Party) { wr.Byte(pl.NetId).Str(pl.NetName).Byte((int)pl.Class).Float(pl.X).Float(pl.Y); }
            p.C.Send(wr);
        }

        void SendSnapshots()
        {
            for (int i = 0; i < peers.Count; i++)
            {
                Peer p = peers[i];
                if (p.Pl == null) continue;
                wr.Begin(SSnap).Byte(BossFight ? 1 : 0).Int(Boss != null && !Boss.Dead ? Boss.NetId : 0);
                wr.Byte(Party.Count - 1);
                for (int k = 0; k < Party.Count; k++) if (Party[k] != p.Pl) WritePlayerState(wr, Party[k]);
                int countAt = wr.N; wr.Short(0);
                int n = 0;
                for (int k = 0; k < Foes.Count; k++)
                {
                    Enemy e = Foes[k];
                    if (e.Dead || !e.Awake) continue;
                    WriteFoe(wr, e); n++;
                }
                wr.B[countAt] = (byte)n; wr.B[countAt + 1] = (byte)(n >> 8);
                p.C.Send(wr);
            }
        }

        // joining
        static bool ParseAddress(string a, out string host, out int port)
        {
            host = ""; port = DefaultPort;
            if (a == null) return false;
            a = a.Trim();
            if (a.Length == 0) return false;
            if (a.StartsWith("["))
            {
                int close = a.IndexOf(']');
                if (close < 2) return false;
                host = a.Substring(1, close - 1);
                if (close + 2 < a.Length && a[close + 1] == ':' && !int.TryParse(a.Substring(close + 2), out port)) return false;
            }
            else
            {
                int colon = a.LastIndexOf(':');
                if (colon > 0 && a.IndexOf(':') == colon)
                {
                    host = a.Substring(0, colon);
                    if (!int.TryParse(a.Substring(colon + 1), out port)) return false;
                }
                else host = a;
            }
            return host.Length > 0 && port > 0 && port < 65536;
        }

        public void NetJoin(string address)
        {
            if (JoinState != JoinIdle || Net != NetMode.Off) return;
            string host; int port;
            if (!ParseAddress(address, out host, out port)) { JoinError = "Type the host's address first - for example 192.168.1.20"; Snd(Sfx.UiError); return; }
            JoinError = ""; JoinState = JoinConnecting; joinDone = false; joinFail = null; joinSocket = null; joinT = 0; kickReason = null;
            Cfg.LastServer = address.Trim(); SaveSettings();
            int attempt = ++joinAttempt;
            Thread t = new Thread(delegate ()
            {
                Socket s = null; string fail = null;
                try
                {
                    IPAddress ip;
                    if (!IPAddress.TryParse(host, out ip))
                    {
                        IPAddress[] all = Dns.GetHostAddresses(host);
                        foreach (IPAddress a in all) if (a.AddressFamily == AddressFamily.InterNetwork) { ip = a; break; }
                        if (ip == null && all.Length > 0) ip = all[0];
                    }
                    if (ip == null) fail = "That address could not be found.";
                    else
                    {
                        s = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        IAsyncResult ar = s.BeginConnect(ip, port, null, null);
                        if (!ar.AsyncWaitHandle.WaitOne(7000)) fail = "No answer from that address.";
                        else s.EndConnect(ar);
                    }
                }
                catch (SocketException) { fail = "Nobody is hosting at that address."; }
                catch (Exception) { fail = "That address could not be reached."; }
                if (fail != null || attempt != joinAttempt)
                {
                    try { if (s != null) s.Close(); } catch { }
                    if (attempt != joinAttempt) return;
                    joinFail = fail;
                }
                else joinSocket = s;
                joinDone = true;
            });
            t.IsBackground = true; t.Name = "Vesperfell connect";
            t.Start();
        }

        void JoinFailed(string why)
        {
            joinAttempt++;
            if (hostConn != null) { hostConn.Close(); hostConn = null; }
            JoinState = JoinIdle; JoinError = why;
            Snd(Sfx.UiError);
        }

        /// <summary>A guest has chosen a class: ask the host for the world.</summary>
        public void NetEnter(ClassId cls)
        {
            if (JoinState != JoinLobby || hostConn == null) return;
            joinClass = cls; JoinState = JoinEntering;
            wr.Begin(CEnter).Byte((int)cls); hostConn.Send(wr); hostConn.Flush();
        }

        /// <summary>The session ended without our asking: back to the title, with the reason.</summary>
        void NetLost(string why)
        {
            NetStop();
            GoTitle();
            titleJoin = true; JoinError = why;
            Snd(Sfx.UiError);
        }

        // per-tick plumbing
        /// <summary>Reads whatever has arrived. Runs every tick, on every screen.</summary>
        void NetPump(float dt)
        {
            if (JoinState == JoinConnecting)
            {
                joinT += dt;
                if (hostConn == null && joinDone)
                {
                    joinDone = false;
                    if (joinSocket != null)
                    {
                        hostConn = new Conn(joinSocket); joinSocket = null; joinT = 0;
                        wr.Begin(CHello).Int(NetVersion).Str(CleanName(Cfg.PlayerName)); hostConn.Send(wr); hostConn.Flush();
                    }
                    else JoinFailed(joinFail ?? "That address could not be reached.");
                }
                else if (hostConn != null && joinT > 8f) JoinFailed("The host did not answer.");
            }
            if (Net == NetMode.Host) HostPump(dt);
            else if (hostConn != null) ClientPump();
        }

        void ClientPump()
        {
            if (JoinState == JoinEntering && Scr == Screen.Loading) return;         // the world is being built: leave the news queued
            byte[] m; int guard = 0;
            while (hostConn != null && guard++ < 4000 && hostConn.TryRead(out m))
            {
                try { ClientMsg(new MsgR(m)); }
                catch (Exception ex) { Log.Line("net: " + ex); NetLost("The game lost step with the host."); return; }
                if (JoinState == JoinEntering && Scr == Screen.Loading) return;
            }
            if (hostConn != null && hostConn.Closed)
            {
                string why = kickReason ?? "The connection to the host was lost.";
                if (JoinState == JoinConnecting) JoinFailed(why); else NetLost(why);
            }
        }

        /// <summary>Sends this tick's news. Runs at the end of every tick spent in the world.</summary>
        void NetTick()
        {
            netTick++;
            if (boltSndT > 0) boltSndT -= DT;
            if (shotSndT > 0) shotSndT -= DT;
            if (IsHost)
            {
                SendNew();
                if (netTick % 3 == 0) SendSnapshots();
            }
            else if (IsClient && hostConn != null && JoinState == JoinPlaying)
            {
                SendNew();
                if ((netTick & 1) == 0) { wr.Begin(CState); WritePlayerState(wr, Me); hostConn.Send(wr); }
            }
        }

        void NetFlush()
        {
            for (int i = 0; i < peers.Count; i++) peers[i].C.Flush();
            if (hostConn != null) hostConn.Flush();
        }

        void SendNew()
        {
            for (int i = 0; i < newShots.Count; i++)
            {
                Projectile pr = newShots[i];
                if (IsHost) { wr.Begin(SShot); WriteShot(wr, pr, pr.FromPlayer ? 0 : 255); SendAll(wr, null); }
                else if (pr.FromPlayer) { wr.Begin(CShot); WriteShot(wr, pr, MyNetId); hostConn.Send(wr); }
            }
            newShots.Clear();
            for (int i = 0; i < newAreas.Count; i++)
            {
                Area a = newAreas[i];
                if (IsHost) { wr.Begin(SArea); WriteArea(wr, a, a.Kind == Area.Sigil ? 255 : 0); SendAll(wr, null); }
                else if (a.Kind != Area.Sigil) { wr.Begin(CArea); WriteArea(wr, a, MyNetId); hostConn.Send(wr); }
            }
            newAreas.Clear();
        }

        /// <summary>Adds a ground effect and lets the rest of the party see it.</summary>
        public void AddArea(Area a)
        {
            Areas.Add(a);
            if (Online && !netApplying) newAreas.Add(a);
        }

        // what a guest hears from the host
        void ClientMsg(MsgR r)
        {
            int type = r.Byte();
            if (JoinState < JoinPlaying && type != SHelloOk && type != SKick && type != SWelcome) return;
            switch (type)
            {
                case SHelloOk:
                    if (JoinState != JoinConnecting) return;
                    MyNetId = r.Byte(); HostName = CleanName(r.Str()); HostLevel = M.Clamp(r.Byte(), 1, Player.MaxLevel); HostPlayers = r.Byte();
                    JoinState = JoinLobby; JoinError = "";
                    Scr = Screen.ClassSelect; csSel = 0; titleJoin = false;
                    Snd(Sfx.UiOpen, 0.7f, 1f);
                    break;
                case SKick: kickReason = r.Str(); break;
                case SWelcome: if (JoinState == JoinEntering) ReadWelcome(r); break;
                case SSnap: ReadSnapshot(r); break;
                case SFoeDied:
                    {
                        int id = r.Int(); int kind = M.Clamp(r.Byte(), 0, 8), level = r.Byte(); float x = r.Float(), y = r.Float(), dx = r.Float(), dy = r.Float();
                        bool summoned = r.Bool(); string once = r.Str();
                        Enemy e;
                        if (!foeById.TryGetValue(id, out e))
                        {
                            // it fell somewhere out of sight: a brief stand-in, so the spoils and the smoke still appear
                            netSpawnId = id; e = SpawnEnemy((EnemyKind)kind, x, y, level, null); netSpawnId = 0;
                        }
                        if (e.Dead) return;
                        e.Dead = true; e.DeathT = 0; e.HP = 0; e.Aggro = false; e.X = x; e.Y = y;
                        if (once.Length > 0) Flags.Add("dead_" + once);
                        FoeDeathLocal(e, dx, dy, summoned);
                    }
                    break;
                case SDamage:
                    {
                        float dmg = r.Float(), sx = r.Float(), sy = r.Float(); bool melee = r.Bool(); int src = r.Int(); int status = r.Byte(); float knock = r.Float();
                        Enemy e; foeById.TryGetValue(src, out e);
                        P = Me;
                        DamagePlayer(dmg, sx, sy, melee, e, status, knock);
                    }
                    break;
                case SPropUsed:
                    {
                        Prop q = ReadProp(r); int by = r.Byte();
                        if (q != null) UseProp(q, by);
                    }
                    break;
                case SPropBroken:
                    {
                        Prop q = ReadProp(r);
                        if (q != null) BreakProp(q);
                    }
                    break;
                case SQuests: ReadQuests(r, false); break;
                case SMap:
                    {
                        Map m = r.Byte() == 1 ? Abbey : Over; float x = r.Float(), y = r.Float();
                        Action go = delegate
                        {
                            float px = M.Clamp(x, 8, m.PW - 8), py = M.Clamp(y + 10, 8, m.PH - 8);
                            m.FindFree(ref px, ref py, Me.Radius, 1);
                            EnterMap(m, px, py);
                        };
                        Snd(Sfx.Gate, 0.5f, 1.2f);
                        if (Ov == Overlay.Dialogue || Ov == Overlay.Text || Ov == Overlay.Window) { Ov = Overlay.None; Dlg = null; }
                        if (Fading) go(); else FadeThrough(go);
                    }
                    break;
                case SFoesReset:
                    ClearFoes(); Shots.Clear(); Boss = null;
                    for (int i = Areas.Count - 1; i >= 0; i--) if (!Areas[i].FromPlayer) Areas.RemoveAt(i);
                    break;
                case SJoined:
                    {
                        int id = r.Byte(); string name = CleanName(r.Str()); int cls = M.Clamp(r.Byte(), 0, 3);
                        if (id == MyNetId || FindPlayer(id) != null) return;
                        Player o = NewProxy(id, name, (ClassId)cls);
                        Player host = FindPlayer(0);
                        o.X = o.NetX = host != null ? host.X : Me.X; o.Y = o.NetY = host != null ? host.Y : Me.Y;
                        Others.Add(o); RebuildParty();
                    }
                    break;
                case SLeft:
                    {
                        Player o = FindPlayer(r.Byte());
                        if (o != null && o != Me) { Others.Remove(o); RebuildParty(); }
                    }
                    break;
                case SNotice:
                    {
                        string pattern = r.Str(); int n = Math.Min(r.Byte(), 6);
                        object[] args = new object[n];
                        for (int i = 0; i < n; i++) args[i] = Loc.Tr(r.Str());
                        try { Notify(Loc.F(pattern, args)); } catch (FormatException) { }
                    }
                    break;
                case SShot: ShotCue(ReadShot(r, false)); break;
                case SArea: AreaCue(ReadArea(r, false)); break;
                case SKeep:
                    {
                        string id = r.Str();
                        if (Items.Quest(id).Name.Length == 0) return;
                        GrantKeepsake(id); RemoveQuestDrops(id);
                    }
                    break;
                case SChat: { string name = CleanName(r.Str()); ShowChat(name, CleanText(r.Str(), 120)); } break;
                case SFx: { int kind = r.Byte(); float x = r.Float(), y = r.Float(); DoFx(kind, x, y); } break;
                case SPull: { float x = r.Float(), y = r.Float(); PullTo(x, y); } break;
            }
        }

        Prop ReadProp(MsgR r)
        {
            Map m = r.Byte() == 1 ? Abbey : Over; int uid = r.Int();
            Prop q;
            if (m == null || !m.ByUid.TryGetValue(uid, out q) || !m.Props.Contains(q)) return null;
            return q;
        }

        void ReadWelcome(MsgR r)
        {
            int myId = r.Byte(); string hostName = CleanName(r.Str()); int hostLevel = M.Clamp(r.Byte(), 1, Player.MaxLevel);
            int mapIx = r.Byte(); float hx = r.Float(), hy = r.Float(); bool dawn = r.Bool(); r.Bool();
            int sumOver = r.Int(), sumAbbey = r.Int();
            RebuildMapsThen(delegate
            {
                mapsDirty = true;
                netApplying = true;
                Flags.Clear();
                int nf = Math.Min(r.UShort(), 6000);
                for (int i = 0; i < nf; i++) Flags.Add(r.Str());
                Dawn = dawn; DawnT = 99; PlayTime = 0; WorldT = 0;
                Q = new QuestLog();
                ReadQuests(r, true);
                Net = NetMode.Client; MyNetId = myId; HostName = hostName;
                P = Me = CreatePlayer(joinClass);
                Me.NetId = myId; Me.NetName = CleanName(Cfg.PlayerName);
                BoostGuest(Me, hostLevel);
                int nk = Math.Min(r.Byte(), 16);
                for (int i = 0; i < nk; i++)
                {
                    string id = r.Str(); int n = r.Byte();
                    if (n > 0 && Items.Quest(id).Name.Length > 0) Me.AddKeepsake(id, n);
                }
                Others.Clear();
                int np = Math.Min(r.Byte(), MaxParty);
                for (int i = 0; i < np; i++)
                {
                    int id = r.Byte(); string name = CleanName(r.Str()); int cls = M.Clamp(r.Byte(), 0, 3); float x = r.Float(), y = r.Float();
                    if (id == myId) continue;
                    Player o = NewProxy(id, name, (ClassId)cls);
                    o.X = o.NetX = x; o.Y = o.NetY = y;
                    Others.Add(o);
                }
                RebuildParty();
                Scr = Screen.Play; Ov = Overlay.None;
                ApplyFlags(Over); ApplyFlags(Abbey);
                Map m = mapIx == 1 ? Abbey : Over;
                float px = M.Clamp(hx, 8, m.PW - 8), py = M.Clamp(hy + 10, 8, m.PH - 8);
                m.FindFree(ref px, ref py, Me.Radius, 1);
                Me.RespawnMap = m.Id; Me.RespawnX = px; Me.RespawnY = py;
                EnterMap(m, px, py);
                netApplying = false;
                IntroT = 0; hintStep = 0;
                FadeT = FadeDur; FadeDir = -1;
                JoinState = JoinPlaying;
                Notify(Loc.F("^gYou have joined {0}'s vigil.^0", hostName));
                Notify("^yEnter^0 to speak to the party  -  ^yF^0 to interact  -  ^yI^0 inventory");
                if (Over.Checksum != sumOver || Abbey.Checksum != sumAbbey)
                    Notify("^rThis copy of the game builds the world differently from the host's. Things may be out of place.^0");
            });
        }

        void ReadSnapshot(MsgR r)
        {
            int fl = r.Byte(); int bossId = r.Int();
            int np = Math.Min(r.Byte(), MaxParty);
            for (int i = 0; i < np; i++) ReadPlayerState(r, null);
            int nf = r.UShort();
            for (int i = 0; i < nf && !r.End; i++) ReadFoe(r);
            bool bf = (fl & 1) != 0;
            if (bf && !BossFight)
            {
                BossFight = true; BossIntroT = 0;
                SetBossGate(true);
                Snd(Sfx.Gate, 0.9f, 0.8f); Snd(Sfx.BossRoar);
                Shake(4f, 0.6f);
                SetMusic(Zones.MusBoss);
            }
            else if (!bf && BossFight)
            {
                BossFight = false;
                SetBossGate(false);
                SetMusic(Dawn && !Zone.Indoor ? Zones.MusDawn : Zone.Music);
            }
            Enemy b;
            Boss = bossId != 0 && foeById.TryGetValue(bossId, out b) && !b.Dead ? b : null;
        }

        // heroes on the wire
        static int T10(float t) { return t <= 0 ? 0 : Math.Min(255, (int)(t * 10) + 1); }
        static int T100(float t) { return t <= 0 ? 0 : Math.Min(255, (int)(t * 100) + 1); }
        static float D10(int b) { return b == 0 ? 0 : (b - 1) / 10f + 0.05f; }
        static float D100(int b) { return b == 0 ? 0 : (b - 1) / 100f + 0.005f; }

        void WritePlayerState(MsgW w, Player p)
        {
            int fl = (p.Dead ? 1 : 0) | (p.Moving ? 2 : 0) | (p.Blocking ? 4 : 0) | (p.Facing < 0 ? 8 : 0) | (p.NextCrit ? 16 : 0);
            Item wp = p.Equip[Player.SlotWeapon];
            w.Byte(p.NetId).Byte((int)p.Class).Byte(p.Level).Byte(fl).Float(p.Remote ? p.NetX : p.X).Float(p.Remote ? p.NetY : p.Y).Float(p.AimAngle);
            w.Short((int)Math.Min(65000, Math.Ceiling(Math.Max(0, p.HP)))).Short((int)Math.Min(65000, p.MaxHP));
            w.Float(p.AtkT).Float(p.AtkDur).Float(p.SwingFrom).Float(p.SwingTo).Byte(p.SwingStep);
            w.Byte(T100(p.WhirlT)).Byte(T100(p.DodgeT)).Byte(T100(p.DashT)).Byte(T100(p.CastT)).Byte(T100(p.BowT)).Byte(T100(p.IFrames));
            w.Byte(T10(p.StealthT)).Byte(T10(p.OathT)).Byte(T10(p.PoisonT)).Byte(T10(p.FreezeT));
            w.Byte(p.DashKind).Byte(wp != null ? wp.Base : p.Def.WeaponTypes[0]).Byte(wp != null ? wp.Tier : 0);
        }

        /// <summary>Reads one hero's state. 'known' is the hero it must belong to (host side), or null to look them up (guest side).</summary>
        void ReadPlayerState(MsgR r, Player known)
        {
            int id = r.Byte(), cls = M.Clamp(r.Byte(), 0, 3), level = M.Clamp(r.Byte(), 1, Player.MaxLevel), fl = r.Byte();
            float x = r.Float(), y = r.Float(), aim = r.Float();
            int hp = r.UShort(), maxhp = r.UShort();
            float atkT = M.Clamp(r.Float(), 0, 2), atkDur = M.Clamp(r.Float(), 0.01f, 2), sf = M.Clamp(r.Float(), -7, 7), st = M.Clamp(r.Float(), -7, 7); int step = r.Byte() & 3;
            float whirl = D100(r.Byte()), dodge = D100(r.Byte()), dash = D100(r.Byte()), cast = D100(r.Byte()), bow = D100(r.Byte()), ifr = D100(r.Byte());
            float stealth = D10(r.Byte()), oath = D10(r.Byte()), poison = D10(r.Byte()), freeze = D10(r.Byte());
            int dashKind = r.Byte(), wbase = M.Clamp(r.Byte(), 0, Weapons.Types.Length - 1), wtier = M.Clamp(r.Byte(), 0, 3);
            Player p = known;
            if (p == null) { p = FindPlayer(id); if (p == Me) p = null; }
            if (p == null || Cur == null) return;
            if ((int)p.Class != cls) { p.Class = (ClassId)cls; p.Def = Classes.Get(p.Class); p.Spr = Assets.Hero[cls]; }
            p.Level = level;
            x = M.Clamp(x, 0, Cur.PW); y = M.Clamp(y, 0, Cur.PH);
            bool dead = (fl & 1) != 0;
            if (dead && !p.Dead) { p.DeadT = 0; SndAt(Sfx.PlayerDie, x, y, 0.7f); }
            p.Dead = dead;
            p.Moving = (fl & 2) != 0; p.Blocking = (fl & 4) != 0; p.Facing = (fl & 8) != 0 ? -1 : 1; p.NextCrit = (fl & 16) != 0;
            p.NetX = x; p.NetY = y;
            if (M.Dist2(p.X, p.Y, x, y) > 90 * 90) { p.X = x; p.Y = y; }
            p.AimAngle = aim; p.HP = hp; p.MaxHP = Math.Max(1, maxhp);
            if (atkT > p.AtkT + 0.05f) SndAt(p.Class == ClassId.Rogue ? Sfx.Stab : Sfx.Swing, x, y, 0.5f);
            if (dodge > p.DodgeT + 0.08f) SndAt(Sfx.Dodge, x, y, 0.5f);
            p.AtkT = atkT; p.AtkDur = atkDur; p.SwingFrom = sf; p.SwingTo = st; p.SwingStep = step;
            p.WhirlT = whirl; p.DodgeT = dodge; p.DashT = dash; p.CastT = cast; p.BowT = bow; p.IFrames = ifr;
            p.StealthT = stealth; p.OathT = oath; p.PoisonT = poison; p.FreezeT = freeze; p.DashKind = dashKind;
            Item w = p.Equip[Player.SlotWeapon];
            if (w == null) { w = new Item(); w.Kind = ItemKind.Weapon; p.Equip[Player.SlotWeapon] = w; }
            w.Base = wbase; w.Rar = (Rarity)wtier;
        }

        /// <summary>Other heroes glide between reports, and their short timers run down locally so swings and rolls look whole.</summary>
        void UpdateOthers(float dt)
        {
            float k = M.Damp(16f, dt);
            for (int i = 0; i < Others.Count; i++)
            {
                Player o = Others[i];
                o.X += (o.NetX - o.X) * k; o.Y += (o.NetY - o.Y) * k;
                o.AnimT += dt;
                if (o.Dead) o.DeadT += dt;
                if (o.AtkT > 0) o.AtkT -= dt;
                if (o.WhirlT > 0) o.WhirlT -= dt;
                if (o.DodgeT > 0) o.DodgeT -= dt;
                if (o.DashT > 0) o.DashT -= dt;
                if (o.CastT > 0) o.CastT -= dt;
                if (o.BowT > 0) o.BowT -= dt;
                if (o.IFrames > 0) o.IFrames -= dt;
                if (o.StealthT > 0) o.StealthT -= dt;
                if (o.OathT > 0) o.OathT -= dt;
                if (o.FlashT > 0) o.FlashT -= dt;
            }
        }

        // enemies on the wire
        void WriteFoe(MsgW w, Enemy e)
        {
            w.Int(e.NetId).Byte((int)e.Kind).Byte(e.Level).Byte(e.Variant).Float(e.X).Float(e.Y);
            w.Short((int)Math.Min(65000, Math.Ceiling(Math.Max(0, e.HP)))).Short((int)Math.Min(65000, e.MaxHP));
            w.Byte(e.State).Float(e.StateT).Float(e.StateDur).Byte(e.AttackKind).Float(e.AttackAng);
            w.Byte((e.Aggro ? 1 : 0) | (e.Moving ? 2 : 0) | (e.Facing < 0 ? 4 : 0));
            w.Byte(T10(e.StunT)).Byte(T10(e.FreezeT)).Byte(T10(e.SlowT)).Byte(T10(e.RootT)).Byte(T10(e.BurnT)).Byte(T10(e.PoisonT)).Byte(T10(e.MarkT));
            w.Byte((int)(M.Clamp01(e.FadeIn) * 255)).Byte((int)(M.Clamp01(e.WeaponSwing) * 255));
            w.Byte(e.Target != null ? e.Target.NetId : 255);
        }

        void ReadFoe(MsgR r)
        {
            int id = r.Int(), kind = r.Byte(), level = r.Byte(), variant = r.Byte(); float x = r.Float(), y = r.Float();
            int hp = r.UShort(), maxhp = r.UShort(); int state = r.Byte(); float st = r.Float(), sd = r.Float(); int ak = r.Byte(); float aa = r.Float();
            int fl = r.Byte();
            float stun = D10(r.Byte()), freeze = D10(r.Byte()), slow = D10(r.Byte()), root = D10(r.Byte()), burn = D10(r.Byte()), poison = D10(r.Byte()), mark = D10(r.Byte());
            float fade = r.Byte() / 255f, swing = r.Byte() / 255f; int tgt = r.Byte();
            if (kind > 8 || Cur == null || hp <= 0) return;
            Enemy e;
            if (!foeById.TryGetValue(id, out e))
            {
                netSpawnId = id; e = SpawnEnemy((EnemyKind)kind, x, y, level, null); netSpawnId = 0;
                e.Variant = variant;
                if (e.Kind == EnemyKind.Hollow) e.Spr = Assets.Hollow[variant % 3];
                e.X = x; e.Y = y; e.FadeIn = fade; e.State = state; e.StateT = st; e.HP = hp; e.MaxHP = Math.Max(1, maxhp);
            }
            else if (e.Dead) return;
            e.NetSeen = 0; e.NetX = x; e.NetY = y;
            if (hp < e.HP - 0.5f)
            {
                e.HpBarT = 5f;
                if (e.FlashT <= 0) { e.FlashT = 0.1f; Splat(e.X, e.Y, 8, e.Def.BloodColor, 3, 0, -1, 55); }
            }
            e.HP = hp; e.MaxHP = Math.Max(1, maxhp);
            bool aggro = (fl & 1) != 0;
            if (aggro && !e.Aggro) { e.AlertShow = 0.7f; AlertSound(e); }
            e.Aggro = aggro; e.NetMoving = (fl & 2) != 0; e.Facing = (fl & 4) != 0 ? -1 : 1;
            e.AttackKind = ak; e.AttackAng = aa; e.StateDur = sd;
            if (state != e.State) { e.State = state; e.StateT = st; EnemyCue(e); }
            else if (Math.Abs(e.StateT - st) > 0.15f) e.StateT = st;
            e.StunT = stun; e.FreezeT = freeze; e.SlowT = slow; e.RootT = root; e.BurnT = burn; e.PoisonT = poison; e.MarkT = mark;
            if (swing > e.WeaponSwing + 0.3f)
            {
                e.WeaponSwing = swing;
                SndAt(e.Kind == EnemyKind.Knight || e.Kind == EnemyKind.Prior ? Sfx.SwingHeavy : (e.Kind == EnemyKind.Crawler ? Sfx.WolfBite : Sfx.Swing), e.X, e.Y, 0.8f);
            }
            if (fade < e.FadeIn - 0.3f) e.FadeIn = fade;
            e.Target = tgt == 255 ? null : FindPlayer(tgt);
        }

        /// <summary>The sounds a guest should hear as an enemy changes what it is doing.</summary>
        void EnemyCue(Enemy e)
        {
            if (e.State == Enemy.Vanish)
            {
                SndAt(Sfx.Teleport, e.X, e.Y, 0.7f);
                Motes(e.X, e.Y, 10, 0xC9D3F5, 0x584688, 12, 8, 45, 0.55f);
                return;
            }
            if (e.State != Enemy.Windup) return;
            switch (e.Kind)
            {
                case EnemyKind.Wolf: case EnemyKind.AlphaWolf: SndAt(Sfx.WolfGrowl, e.X, e.Y, 0.7f); break;
                case EnemyKind.Crawler: if (e.AttackKind == AkCast) SndAt(Sfx.Hiss, e.X, e.Y, 0.7f); break;
                case EnemyKind.Hollow: SndAt(Sfx.Groan, e.X, e.Y, 0.6f); break;
                case EnemyKind.Archer: SndAt(Sfx.BoneRattle, e.X, e.Y, 0.5f); break;
                case EnemyKind.Wisp: SndAt(Sfx.WispCast, e.X, e.Y, 0.6f); break;
                case EnemyKind.Acolyte: case EnemyKind.Prior: SndAt(Sfx.Chant, e.X, e.Y, 0.8f); break;
                case EnemyKind.Knight: SndAt(e.AttackKind == AkLine ? Sfx.KnightCharge : Sfx.BossRoar, e.X, e.Y, e.AttackKind == AkArc ? 0f : 0.8f); break;
            }
        }

        /// <summary>A guest does not think for the enemies: it glides them between the host's reports.</summary>
        void UpdateEnemiesClient(float dt)
        {
            float k = M.Damp(14f, dt);
            for (int i = Foes.Count - 1; i >= 0; i--)
            {
                if (i >= Foes.Count) continue;
                Enemy e = Foes[i];
                if (e.Dead) { e.DeathT += dt; if (e.DeathT > 0.7f) { foeById.Remove(e.NetId); Lists.RemoveAt(Foes, i, e); } continue; }
                e.NetSeen += dt;
                if (e.NetSeen > 1.5f) { foeById.Remove(e.NetId); Foes.RemoveAt(i); continue; }      // out of everyone's sight
                e.AnimT += dt; e.StateT += dt;
                if (e.HpBarT > 0) e.HpBarT -= dt;
                if (e.AlertShow > 0) e.AlertShow -= dt;
                if (e.FlashT > 0) e.FlashT -= dt;
                if (e.WeaponSwing > 0) e.WeaponSwing -= dt * 4f;
                if (e.FadeIn < 1) e.FadeIn = Math.Min(1, e.FadeIn + dt * 3f);
                if (e.StunT > 0) e.StunT -= dt;
                if (e.FreezeT > 0) e.FreezeT -= dt;
                if (e.SlowT > 0) e.SlowT -= dt;
                if (e.RootT > 0) e.RootT -= dt;
                if (e.BurnT > 0) e.BurnT -= dt;
                if (e.PoisonT > 0) e.PoisonT -= dt;
                if (e.MarkT > 0) e.MarkT -= dt;
                float dx = e.NetX - e.X, dy = e.NetY - e.Y;
                if (dx * dx + dy * dy > 70 * 70) { e.X = e.NetX; e.Y = e.NetY; }
                else { e.X += dx * k; e.Y += dy * k; }
                e.Moving = e.NetMoving;
                if (e.Kind == EnemyKind.Wisp) e.Z = 6 + M.Sin(e.AnimT * 3f) * 2.5f;
                else if (e.Kind == EnemyKind.Prior) e.Z = 3 + M.Sin(e.AnimT * 2.2f) * 1.5f;
                // the big ones cannot be walked through
                if (e.Def.KnockRes >= 0.9f && !Me.Dead && Me.DashT <= 0 && Me.DodgeT <= 0)
                {
                    float px = e.X - Me.X, py = e.Y - Me.Y, rr = e.Radius + Me.Radius, d = M.Len(px, py);
                    if (d < rr && d > 0.01f) Cur.Move(Me, -px / d * (rr - d), -py / d * (rr - d), 1);
                }
            }
        }

        // shots and ground effects on the wire
        void WriteShot(MsgW w, Projectile pr, int owner)
        {
            int tgt = 0;
            Enemy te = pr.Target as Enemy; Player tp = pr.Target as Player;
            if (te != null) tgt = te.NetId; else if (tp != null) tgt = tp.NetId + 1;
            w.Byte(owner).Byte(pr.Kind).Float(pr.X).Float(pr.Y).Float(pr.VX).Float(pr.VY).Float(pr.Z).Float(pr.VZ).Float(pr.Life).Float(pr.Dmg);
            w.Byte(pr.Lob ? 1 : 0).Float(pr.TXg).Float(pr.TYg).Int(tgt);
        }

        /// <summary>Creates a projectile sent by another machine. A hero's shot arrives as a ghost: it is seen here, but its owner decides what it hits.</summary>
        Projectile ReadShot(MsgR r, bool fromGuest)
        {
            int owner = r.Byte(), kind = r.Byte(); float x = r.Float(), y = r.Float(), vx = M.Clamp(r.Float(), -900, 900), vy = M.Clamp(r.Float(), -900, 900);
            float z = M.Clamp(r.Float(), 0, 200), vz = M.Clamp(r.Float(), -900, 900), life = M.Clamp(r.Float(), 0.02f, 8), dmg = M.Clamp(r.Float(), 0, 5000);
            int fl = r.Byte(); float txg = r.Float(), tyg = r.Float(); int tgt = r.Int();
            bool fromPlayer = fromGuest || owner != 255;
            netApplying = true;
            Projectile pr = Shoot(kind, x, y, M.Atan2(vy, vx), M.Len(vx, vy), dmg, fromPlayer, life);
            netApplying = false;
            pr.VX = vx; pr.VY = vy; pr.Z = z; pr.VZ = vz; pr.TXg = txg; pr.TYg = tyg;
            pr.Lob = (fl & 1) != 0 && !fromPlayer;
            pr.OwnerId = owner; pr.Ghost = fromPlayer;
            if (fromPlayer) { Enemy e; if (tgt != 0 && foeById.TryGetValue(tgt, out e)) pr.Target = e; }
            else if (tgt != 0) pr.Target = FindPlayer(tgt - 1);
            return pr;
        }

        void ShotCue(Projectile pr)
        {
            if (shotSndT > 0) return;
            shotSndT = 0.06f;
            switch (pr.Kind)
            {
                case Projectile.KArrow: case Projectile.KPierce: case Projectile.KMarked: case Projectile.KBone: SndAt(Sfx.Bow, pr.X, pr.Y, 0.6f); break;
                case Projectile.KKnife: case Projectile.KPoisonKnife: SndAt(Sfx.Knife, pr.X, pr.Y, 0.5f); break;
                case Projectile.KEmber: SndAt(Sfx.CastEmber, pr.X, pr.Y, 0.5f); break;
                case Projectile.KFireball: SndAt(Sfx.Fireball, pr.X, pr.Y, 0.8f); break;
                case Projectile.KGlob: SndAt(Sfx.Spit, pr.X, pr.Y, 0.8f); break;
                case Projectile.KGloam: case Projectile.KWispOrb: SndAt(Sfx.WispCast, pr.X, pr.Y, 0.6f); break;
            }
        }

        void WriteArea(MsgW w, Area a, int owner)
        {
            w.Byte(owner).Byte(a.Kind).Float(a.X).Float(a.Y).Float(a.R).Float(a.Dur).Float(a.Delay).Float(a.Dmg).Int(a.Color).Float(a.X2).Float(a.Y2).Int(a.Seed);
        }

        Area ReadArea(MsgR r, bool fromGuest)
        {
            Area a = new Area();
            int owner = r.Byte();
            a.Kind = r.Byte(); a.X = r.Float(); a.Y = r.Float(); a.R = M.Clamp(r.Float(), 0, 200); a.Dur = M.Clamp(r.Float(), 0, 30); a.Delay = M.Clamp(r.Float(), 0, 10);
            a.Dmg = M.Clamp(r.Float(), 0, 2000); a.Color = r.Int() & 0xFFFFFF; a.X2 = r.Float(); a.Y2 = r.Float(); a.Seed = r.Int();
            // only the host's own sigils can hurt; everything else that arrives is for show
            a.Ghost = fromGuest || owner != 255 || a.Kind != Area.Sigil;
            if (a.Ghost && a.Kind == Area.Sigil) a.Kind = Area.Ring;
            a.FromPlayer = a.Ghost && (a.Kind == Area.Trap || a.Kind == Area.Rain);
            Areas.Add(a);
            return a;
        }

        void AreaCue(Area a)
        {
            switch (a.Kind)
            {
                case Area.Burst:
                    Sparks(a.X, a.Y, 10, 0xF7BE52, 22, 150, 0.55f); Smoke(a.X, a.Y, 8, 8, 0x3A2A2A, 5f); RingBurst(a.X, a.Y, 0xEC8A32, 18, 120, 0.4f);
                    SndAt(Sfx.Explode, a.X, a.Y, 1f);
                    break;
                case Area.Ring:
                    if (a.Color == 0x9BDCF4)
                    {
                        RingBurst(a.X, a.Y, 0x9BDCF4, 28, 170, 0.45f); Motes(a.X, a.Y, 2, 0xE2F8FF, 0x2F6A9A, 20, a.R * 0.6f, 20, 0.8f);
                        SndAt(Sfx.Frost, a.X, a.Y, 1f);
                    }
                    break;
                case Area.Bolt:
                    Sparks(a.X2, a.Y2, 10, 0xE2F8FF, 8, 90, 0.3f);
                    if (boltSndT <= 0) { boltSndT = 0.25f; SndAt(Sfx.Lightning, a.X2, a.Y2, 0.9f); }
                    break;
                case Area.Trap: SndAt(Sfx.TrapSet, a.X, a.Y, 0.5f); break;
                case Area.Rain: SndAt(Sfx.ArrowRain, a.X, a.Y, 0.8f); break;
            }
        }

        // small messages
        void WriteQuests(MsgW w)
        {
            w.Byte(Q.All.Count);
            foreach (Quest q in Q.All) { w.Str(q.Id).Byte(q.Stage + 1).Bool(q.Done).Short(q.Count); }
        }

        void ReadQuests(MsgR r, bool silent)
        {
            int n = Math.Min(r.Byte(), 32);
            for (int i = 0; i < n; i++)
            {
                string id = r.Str(); int stage = r.Byte() - 1; bool done = r.Bool(); int count = r.Short();
                Quest q = Q.Get(id);
                if (q == null) continue;
                int os = q.Stage; bool od = q.Done;
                q.Stage = Math.Min(stage, q.Objectives.Length - 1); q.Done = done; q.Count = count;
                if (!silent && (os != q.Stage || od != done)) OnQuestProgress(q, os, od);
            }
        }

        public void NetQuestsChanged()
        {
            if (!IsHost) return;
            wr.Begin(SQuests); WriteQuests(wr); SendAll(wr, null);
        }

        public void NetQuestOp(int op, string id, int stage)
        {
            if (!IsClient || hostConn == null) return;
            wr.Begin(CQuest).Byte(op).Str(id).Byte(stage); hostConn.Send(wr);
        }

        /// <summary>A message for the whole party. Each machine words it in its own language.</summary>
        public void NoticeAll(string pattern, params object[] args)
        {
            object[] shown = new object[args.Length];
            for (int i = 0; i < args.Length; i++) shown[i] = args[i] is string ? Loc.Tr((string)args[i]) : args[i];
            Notify(Loc.F(pattern, shown));
            if (!IsHost) return;
            wr.Begin(SNotice).Str(pattern).Byte(args.Length);
            for (int i = 0; i < args.Length; i++) wr.Str(Convert.ToString(args[i], CultureInfo.InvariantCulture));
            SendAll(wr, null);
        }

        void NetHit(Enemy e, int dmg, float dx, float dy, float knock, int fx, bool crit)
        {
            if (hostConn == null) return;
            wr.Begin(CHit).Int(e.NetId).Int(dmg).Float(dx).Float(dy).Float(knock).Byte(fx).Bool(crit); hostConn.Send(wr);
        }

        void NetStatus(Enemy e, int kind, float time, float power)
        {
            if (hostConn == null) return;
            wr.Begin(CStatus).Int(e.NetId).Byte(kind).Float(time).Float(power); hostConn.Send(wr);
        }

        void NetDamage(Player target, float dmg, float sx, float sy, bool melee, Enemy src, int status, float knock)
        {
            Peer p = PeerOf(target);
            if (p == null) return;
            wr.Begin(SDamage).Float(dmg).Float(sx).Float(sy).Bool(melee).Int(src != null ? src.NetId : 0).Byte(status).Float(knock); p.C.Send(wr);
        }

        void NetFoeDied(Enemy e, float dx, float dy, bool summoned)
        {
            if (!IsHost) return;
            wr.Begin(SFoeDied).Int(e.NetId).Byte((int)e.Kind).Byte(e.Level).Float(e.X).Float(e.Y).Float(dx).Float(dy).Bool(summoned);
            wr.Str(e.Spawn != null && e.Spawn.Once ? e.Spawn.Id : "");
            SendAll(wr, null);
        }

        void NetUseProp(Prop q)
        {
            if (hostConn == null || q.Uid == 0) return;
            wr.Begin(CUse).Byte(Cur == Abbey ? 1 : 0).Int(q.Uid); hostConn.Send(wr);
        }

        void NetPropUsed(Prop q, int by)
        {
            if (!IsHost || q.Uid == 0) return;
            wr.Begin(SPropUsed).Byte(Cur == Abbey ? 1 : 0).Int(q.Uid).Byte(by); SendAll(wr, null);
        }

        void NetHitProp(Prop q, int power)
        {
            if (hostConn == null || q.Uid == 0) return;
            wr.Begin(CHitProp).Byte(Cur == Abbey ? 1 : 0).Int(q.Uid).Byte(power); hostConn.Send(wr);
        }

        void NetPropBroken(Prop q)
        {
            if (!IsHost || q.Uid == 0) return;
            wr.Begin(SPropBroken).Byte(Cur == Abbey ? 1 : 0).Int(q.Uid); SendAll(wr, null);
        }

        void NetFoesReset()
        {
            if (!IsHost) return;
            wr.Begin(SFoesReset); SendAll(wr, null);
        }

        void NetMapEntered(bool changed)
        {
            if (!IsHost) return;
            if (changed) wr.Begin(SMap).Byte(Cur == Abbey ? 1 : 0).Float(Me.X).Float(Me.Y);
            else wr.Begin(SFoesReset);
            SendAll(wr, null);
        }

        /// <summary>Set-piece effects the host plays for itself and reports to the guests.</summary>
        void NetFx(int kind, float x, float y)
        {
            if (!IsHost) return;
            wr.Begin(SFx).Byte(kind).Float(x).Float(y); SendAll(wr, null);
        }

        void DoFx(int kind, float x, float y)
        {
            switch (kind)
            {
                case FxSlam: RingBurst(x, y, 0x58A8D8, 26, 170, 0.45f); Dust(x, y, 10, 0x8A8A9A); SndAt(Sfx.KnightSlam, x, y, 1f); Shake(5f, 0.3f); break;
                case FxToll: RingBurst(x, y, 0xA7A0DC, 20, 150, 0.4f); SndAt(Sfx.BellToll, x, y, 1f); Shake(2.5f, 0.2f); break;
                case FxSummon: Motes(x, y, 0, 0xA7A0DC, 0x3A2E5E, 12, 8, 50, 0.7f); Smoke(x, y, 4, 4, 0x2E2843, 4f); break;
                case FxRoar: SndAt(Sfx.BossRoar, x, y, 1f); Shake(4f, 0.4f); Flash(0x7F6CB6, 0.3f); break;
                case FxStagger: SndAt(Sfx.KnightSlam, x, y, 0.9f); Shake(4f, 0.25f); Dust(x, y, 8, 0x8A8A9A); Float(x, y - 40, "Staggered!", 0xFFF8DC, false); break;
            }
        }

        // shared keepsakes, the pull, chat
        /// <summary>Quest items belong to the whole party: when one hero finds one, everyone carries it.</summary>
        public void GrantKeepsake(string id)
        {
            Player p = Me;
            if (p == null || p.KeepsakeCount(id) > 0) return;
            p.AddKeepsake(id, 1);
            Notify("^b" + Loc.Tr(Items.Quest(id).Name) + "^0");
            Snd(Sfx.PickItem, 1f, 1.2f);
        }

        void RemoveQuestDrops(string id)
        {
            for (int i = Drops.Count - 1; i >= 0; i--)
                if (Drops[i].Kind == Pickup.Quest && Drops[i].It != null && Drops[i].It.QuestId == id) Drops.RemoveAt(i);
        }

        void NetKeepsake(string id)
        {
            if (IsHost) { wr.Begin(SKeep).Str(id); SendAll(wr, null); }
            else if (IsClient && hostConn != null) { wr.Begin(CKeep).Str(id); hostConn.Send(wr); }
        }

        /// <summary>Host side: brings a hero to a place (the Bell Crypt seals with the whole party inside).</summary>
        void NetPull(Player pl, float x, float y)
        {
            if (pl == Me) { PullTo(x, y); return; }
            Peer p = PeerOf(pl);
            if (p != null) { wr.Begin(SPull).Float(x).Float(y); p.C.Send(wr); }
        }

        void PullTo(float x, float y)
        {
            Player p = Me;
            float px = x + (MyNetId * 10 - 14), py = y;
            Cur.FindFree(ref px, ref py, p.Radius, 1);
            p.X = px; p.Y = py; p.KX = p.KY = 0; p.DashT = 0; p.DodgeT = 0; p.IFrames = Math.Max(p.IFrames, 1f);
            SnapCamera(); Flash(0x7F6CB6, 0.3f);
            Motes(p.X, p.Y, 4, 0xEAF3FF, 0x7F6CB6, 16, 7, 60, 0.5f);
            Notify("^pThe crypt draws the whole party in.^0");
        }

        public void NetChat(string text)
        {
            text = CleanText(text, 120);
            if (text.Length == 0 || !Online || Me == null) return;
            ShowChat(Me.NetName, text);
            if (IsHost) { wr.Begin(SChat).Str(Me.NetName).Str(text); SendAll(wr, null); }
            else if (hostConn != null) { wr.Begin(CChat).Str(text); hostConn.Send(wr); }
        }

        void ShowChat(string name, string text)
        {
            Notify(Loc.Raw("^c" + name + ":^0 " + text));
            Snd(Sfx.UiHover, 0.5f, 1.3f);
        }
    }
}
