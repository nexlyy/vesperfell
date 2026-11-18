using System;
using System.Collections.Generic;

namespace Vesperfell
{
    public sealed class Quest
    {
        public string Id, Title, Giver, Summary, Reward;
        public bool Main, Done;
        public int Stage = -1;          // -1 = not yet started
        public int Count, Need;
        public string[] Objectives;

        public bool Active { get { return Stage >= 0 && !Done; } }

        public string Objective
        {
            get
            {
                if (Stage < 0 || Stage >= Objectives.Length) return "";
                return Loc.Tr(Objectives[Stage]).Replace("{0}", Math.Min(Count, Need).ToString()).Replace("{1}", Need.ToString());
            }
        }
    }

    /// <summary>
    /// All quests, their progress, and the events that advance them. In a co-op session the host owns this state:
    /// guests ask the host to change it and receive the result.
    /// </summary>
    public sealed class QuestLog
    {
        public const int OpStart = 0, OpStage = 1, OpComplete = 2;
        public readonly List<Quest> All = new List<Quest>();
        public string Tracked = "bell";

        public QuestLog()
        {
            Quest q = new Quest();
            q.Id = "bell"; q.Title = "The Silent Bell"; q.Giver = "Brother Halvard"; q.Main = true;
            q.Summary = "For four hundred years the Vesper Bell was rung at each day's end, and the Gloam kept to its side of the vale. Thirty nights ago the bell fell silent. The dusk has not ended since.";
            q.Reward = "The dawn.";
            q.Need = 2;
            q.Objectives = new[]
            {
                "Speak with Brother Halvard beside the campfire.",
                "Follow the road north to Emberwick and find Elder Maren.",
                "Take the Abbey Sigil from Sir Caldus at the King's Barrow, east of Emberwick.",
                "Carry the Sigil north, through the graveyard, to the doors of the Sunken Abbey.",
                "Kindle the two vigil-flames in the abbey's west and east wings. ({0}/{1})",
                "Pass the Bellgate and face the Silent Prior in the Bell Crypt.",
                "Hang the Bell's Tongue and ring the Vesper Bell.",
                "Return to Elder Maren in Emberwick.",
            };
            All.Add(q);

            q = new Quest();
            q.Id = "wolves"; q.Title = "Thinning the Pack"; q.Giver = "Bram the Smith";
            q.Summary = "The wolves of the Thornwood have grown gaunt and pale-eyed, and bold enough to stand at the palisade after lamp-lighting. Bram wants their numbers cut.";
            q.Reward = "Bram's finest work, and coin.";
            q.Need = 6;
            q.Objectives = new[] { "Hunt Gloam Wolves in the Thornwood, west of Emberwick. ({0}/{1})", "Return to Bram at the forge in Emberwick." };
            All.Add(q);

            q = new Quest();
            q.Id = "petals"; q.Title = "Moonpetal for the Fevered"; q.Giver = "Ysolde";
            q.Summary = "The dusk-fever is spreading through Emberwick. Moonpetal breaks it, but the flower now grows only in the Thornwood, glowing faintly among the roots.";
            q.Reward = "Draughts, and a ring of living stem.";
            q.Need = 5;
            q.Objectives = new[] { "Gather Moonpetals in the Thornwood. They glow in the dark. ({0}/{1})", "Bring the Moonpetals to Ysolde in Emberwick." };
            All.Add(q);

            q = new Quest();
            q.Id = "lamplighter"; q.Title = "The Missing Lamplighter"; q.Giver = "Wren";
            q.Summary = "Tobin the lamplighter went to tend the far lamps of the west road six nights ago. His daughter Wren is still waiting at the gate.";
            q.Reward = "A child's keepsake.";
            q.Objectives = new[] { "Search the deep Thornwood for Tobin: take the unlit path north from the fork in the west road.", "Bring Tobin's lantern home to Wren in Emberwick." };
            All.Add(q);

            q = new Quest();
            q.Id = "stones"; q.Title = "What the Stones Keep"; q.Giver = "An old carving";
            q.Summary = "Three rune stones older than the abbey stand in the vale: at the old watchtower in the Thornwood, in the southern Barrowfield, and on an islet in Mirefen. The carvings say that waking all three will open the king's door.";
            q.Reward = "Whatever the barrow-kings buried.";
            q.Need = 3;
            q.Objectives = new[] { "Wake the three rune stones: the watchtower, the Barrowfield and Mirefen. ({0}/{1})", "Open the king's door, in the far north-east of the Barrowfield." };
            All.Add(q);
        }

        public Quest Get(string id) { foreach (Quest q in All) if (q.Id == id) return q; return null; }
        public int Stage(string id) { Quest q = Get(id); return q == null ? -1 : q.Stage; }
        public bool IsDone(string id) { Quest q = Get(id); return q != null && q.Done; }
        public bool IsActive(string id) { Quest q = Get(id); return q != null && q.Active; }

        /// <summary>The single place quest state changes (on the host, or in single player).</summary>
        void Change(Game g, Quest q, int stage, bool done, int count)
        {
            int os = q.Stage; bool od = q.Done;
            q.Stage = stage; q.Done = done; q.Count = count;
            g.OnQuestProgress(q, os, od);
            g.NetQuestsChanged();
        }

        public void Start(string id, Game g)
        {
            Quest q = Get(id);
            if (q == null || q.Stage >= 0) return;
            if (g.IsClient) { g.NetQuestOp(OpStart, id, 0); return; }
            int stage = 0, count = 0;
            if (id == "petals") { count = g.Me.KeepsakeCount("moonpetal"); if (count >= q.Need) stage = 1; }
            if (id == "stones") { count = g.RuneCount; if (count >= q.Need) stage = 1; }
            if (id == "lamplighter" && g.Me.KeepsakeCount("lantern") > 0) stage = 1;
            Change(g, q, stage, false, count);
        }

        public void SetStage(string id, int stage, Game g)
        {
            Quest q = Get(id);
            if (q == null || q.Done || q.Stage >= stage) return;
            if (g.IsClient) { g.NetQuestOp(OpStage, id, stage); return; }
            Change(g, q, stage, false, 0);
        }

        public void Complete(string id, Game g)
        {
            Quest q = Get(id);
            if (q == null || q.Done) return;
            if (g.IsClient) { g.NetQuestOp(OpComplete, id, 0); return; }
            Change(g, q, Math.Max(0, q.Stage), true, q.Count);
        }

        // world events (host / single player only)
        public void OnKill(EnemyKind k, Game g)
        {
            if (g.IsClient) return;
            Quest w = Get("wolves");
            if (w.Active && w.Stage == 0 && (k == EnemyKind.Wolf || k == EnemyKind.AlphaWolf))
            {
                if (w.Count + 1 >= w.Need) SetStage("wolves", 1, g);
                else
                {
                    w.Count++;
                    g.NoticeAll("Wolves hunted: ^y{0}^0 / {1}", w.Count, w.Need);
                    g.NetQuestsChanged();
                }
            }
        }

        public void OnItem(string item, Game g)
        {
            if (g.IsClient) return;
            switch (item)
            {
                case "moonpetal":
                    {
                        Quest q = Get("petals");
                        if (q.Active && q.Stage == 0)
                        {
                            int n = g.Me.KeepsakeCount("moonpetal");
                            if (n >= q.Need) SetStage("petals", 1, g);
                            else { q.Count = n; g.NetQuestsChanged(); }
                        }
                    }
                    break;
                case "lantern":
                    {
                        Quest q = Get("lamplighter");
                        if (q.Stage < 0) Change(g, q, 1, false, 0);
                        else if (q.Active && q.Stage == 0) SetStage("lamplighter", 1, g);
                    }
                    break;
                case "sigil": SetStage("bell", 3, g); break;
                case "tongue": SetStage("bell", 6, g); break;
            }
        }

        public void OnRune(Game g)
        {
            if (g.IsClient) return;
            Quest q = Get("stones");
            if (q.Stage < 0) Start("stones", g);
            if (q.Done) return;
            int n = g.RuneCount;
            if (q.Stage == 0 && n >= q.Need) SetStage("stones", 1, g);
            else if (q.Stage == 0) { q.Count = n; g.NetQuestsChanged(); }
        }

        public void OnCache(Game g)
        {
            if (g.IsClient) return;
            Quest q = Get("stones");
            if (q.Stage < 0) Change(g, q, 1, false, 3);
            Complete("stones", g);
        }

        public void OnBrazier(Game g)
        {
            if (g.IsClient) return;
            Quest q = Get("bell");
            if (q.Done) return;
            int lit = (g.Flags.Contains("lit_brazier_w") ? 1 : 0) + (g.Flags.Contains("lit_brazier_e") ? 1 : 0);
            if (lit >= 2) SetStage("bell", 5, g);
            else if (q.Stage < 4) Change(g, q, 4, false, lit);
            else if (q.Stage == 4) { q.Count = lit; g.NetQuestsChanged(); }
        }

        public void OnEnterAbbey(Game g)
        {
            if (g.IsClient) return;
            Quest q = Get("bell");
            if (!q.Done && q.Stage < 4)
                Change(g, q, 4, false, (g.Flags.Contains("lit_brazier_w") ? 1 : 0) + (g.Flags.Contains("lit_brazier_e") ? 1 : 0));
        }

        public void OnBell(Game g) { if (!g.IsClient) SetStage("bell", 7, g); }

        // where to go
        /// <summary>World position of a quest's current objective on the player's current map (false if none).</summary>
        public bool Target(Game g, Quest q, out float x, out float y)
        {
            x = 0; y = 0;
            if (q == null || !q.Active) return false;
            Map cur = g.Cur;
            bool inAbbey = cur == g.Abbey;
            string marker = null; string map = "overworld";
            switch (q.Id)
            {
                case "bell":
                    switch (q.Stage)
                    {
                        case 0: marker = "warden"; break;
                        case 1: marker = "elder"; break;
                        case 2: marker = "knight"; break;
                        case 3: marker = "abbey_door"; break;
                        case 4: map = "abbey"; marker = !g.Flags.Contains("lit_brazier_w") ? "brazier_w" : "brazier_e"; break;
                        case 5: map = "abbey"; marker = g.Flags.Contains("dead_prior") ? "bell" : "boss"; break;
                        case 6: map = "abbey"; marker = "bell"; break;
                        case 7: marker = "elder"; break;
                    }
                    break;
                case "wolves":
                    if (q.Stage == 1) marker = "smith";
                    else
                    {
                        // nearest living wolf
                        float best = 1e12f; bool found = false;
                        foreach (Enemy e in g.Foes)
                        {
                            if (e.Dead || (e.Kind != EnemyKind.Wolf && e.Kind != EnemyKind.AlphaWolf)) continue;
                            float d = M.Dist2(e.HomeX, e.HomeY, g.Me.X, g.Me.Y);
                            if (d < best) { best = d; x = e.HomeX; y = e.HomeY; found = true; }
                        }
                        if (found && !inAbbey) return true;
                        x = 58 * 16; y = 61 * 16; return !inAbbey;
                    }
                    break;
                case "petals":
                    if (q.Stage == 1) marker = "herbalist";
                    else
                    {
                        float best = 1e12f; bool found = false;
                        foreach (Prop p in g.Over.Props)
                        {
                            if (p.Kind != PropKind.Moonpetal || !p.Interactive) continue;
                            float d = M.Dist2(p.X, p.Y, g.Me.X, g.Me.Y);
                            if (d < best) { best = d; x = p.X; y = p.Y; found = true; }
                        }
                        return found && !inAbbey;
                    }
                    break;
                case "lamplighter":
                    if (q.Stage == 1) marker = "wren"; else { if (inAbbey) return false; x = 18.4f * 16; y = 20.6f * 16; return true; }
                    break;
                case "stones":
                    if (q.Stage == 1) marker = "cache";
                    else
                    {
                        float best = 1e12f; bool found = false;
                        for (int i = 0; i < 3; i++)
                        {
                            if (g.Flags.Contains("rune" + i)) continue;
                            Marker mk = g.Over.Find("rune" + i);
                            if (mk == null) continue;
                            float d = M.Dist2(mk.X, mk.Y, g.Me.X, g.Me.Y);
                            if (d < best) { best = d; x = mk.X; y = mk.Y; found = true; }
                        }
                        return found && !inAbbey;
                    }
                    break;
            }
            if (marker == null) return false;
            if (map == "abbey" && !inAbbey) marker = "abbey_door";
            else if (map == "overworld" && inAbbey) marker = "abbey_entry";
            Marker m = cur.Find(marker);
            if (m == null) return false;
            x = m.X; y = m.Y;
            return true;
        }

        /// <summary>'!' = has a quest to offer, '?' = ready to hand in, '.' = in progress, '\0' = nothing.</summary>
        public char NpcMark(string npc)
        {
            switch (npc)
            {
                case "warden": return Stage("bell") == 0 ? '!' : '\0';
                case "elder": { int s = Stage("bell"); if (IsDone("bell")) return '\0'; return s == 1 || s == 7 ? '?' : (s >= 2 ? '.' : '\0'); }
                case "smith": { Quest q = Get("wolves"); return q.Done ? '\0' : (q.Stage < 0 ? '!' : (q.Stage == 1 ? '?' : '.')); }
                case "herbalist": { Quest q = Get("petals"); return q.Done ? '\0' : (q.Stage < 0 ? '!' : (q.Stage == 1 ? '?' : '.')); }
                case "wren": { Quest q = Get("lamplighter"); return q.Done ? '\0' : (q.Stage < 0 ? '!' : (q.Stage == 1 ? '?' : '.')); }
            }
            return '\0';
        }
    }
}
