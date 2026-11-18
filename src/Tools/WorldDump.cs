using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Vesperfell
{
    /// <summary>Developer tool: renders maps to PNG and proves every objective can be reached on foot.</summary>
    public static class WorldDump
    {
        public static void Run(string dir)
        {
            Directory.CreateDirectory(dir);
            Fonts.Build();
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            Assets.Build(null);
            long tAssets = sw.ElapsedMilliseconds; sw.Restart();
            Map over = WorldGen.BuildOverworld(null);
            long tOver = sw.ElapsedMilliseconds; sw.Restart();
            Map abbey = DungeonGen.BuildAbbey(null);
            long tAbbey = sw.ElapsedMilliseconds;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("assets ms " + tAssets + ", overworld ms " + tOver + ", abbey ms " + tAbbey);
            sb.AppendLine("overworld props " + over.Props.Count + ", spawns " + over.Spawns.Count + "; abbey props " + abbey.Props.Count + ", spawns " + abbey.Spawns.Count);
            Check(over, "start", sb);
            Check(abbey, "abbey_entry", sb);
            File.WriteAllText(Path.Combine(dir, "worldcheck.txt"), sb.ToString());

            Overview(over, Path.Combine(dir, "overworld_map.png"), 5);
            Overview(abbey, Path.Combine(dir, "abbey_map.png"), 8);
            Region(over, 66, 47, 44, 38, Path.Combine(dir, "r_village.png"), 2);
            Region(over, 74, 100, 30, 24, Path.Combine(dir, "r_camp.png"), 2);
            Region(over, 14, 32, 30, 22, Path.Combine(dir, "r_watchtower.png"), 2);
            Region(over, 146, 62, 28, 20, Path.Combine(dir, "r_knight.png"), 2);
            Region(over, 70, 4, 36, 44, Path.Combine(dir, "r_abbey.png"), 2);
            Region(over, 40, 14, 26, 22, Path.Combine(dir, "r_glade.png"), 2);
            ReachOverlay(over, 36, 12, 30, 28, Path.Combine(dir, "r_glade_reach.png"), 2);
            Region(over, 122, 46, 30, 22, Path.Combine(dir, "r_circle.png"), 2);
            Region(abbey, 0, 0, DungeonGen.TW, DungeonGen.TH, Path.Combine(dir, "r_dungeon.png"), 1);
        }

        static void Check(Map m, string startMarker, StringBuilder sb)
        {
            Marker s = m.Find(startMarker);
            if (s == null) { sb.AppendLine(m.Id + ": missing start marker " + startMarker); return; }
            bool[] reach = m.Reachable(s.X, s.Y);
            int bad = 0;
            foreach (KeyValuePair<string, Marker> kv in m.Markers)
                if (!m.ReachableNear(reach, kv.Value.X, kv.Value.Y, 4)) { sb.AppendLine(m.Id + ": UNREACHABLE marker " + kv.Key); bad++; }
            foreach (Prop p in m.Props)
            {
                if (!p.Interactive) continue;
                if (p.Id == "chest_vault" || p.Id == "chest_glade") continue;       // behind deliberate barriers
                if (!m.ReachableNear(reach, p.X, p.Y + 6, 4)) { sb.AppendLine(m.Id + ": UNREACHABLE prop " + p.Kind + " " + p.Id + " at " + (p.X / 16f).ToString("0.0") + "," + (p.Y / 16f).ToString("0.0")); bad++; }
            }
            foreach (SpawnDef sp in m.Spawns)
            {
                if (m.Blocked(sp.X, sp.Y, 6, 1)) { sb.AppendLine(m.Id + ": spawn inside solid " + sp.Kind + " at " + (sp.X / 16f).ToString("0.0") + "," + (sp.Y / 16f).ToString("0.0")); bad++; }
                else if (!m.ReachableNear(reach, sp.X, sp.Y, 3) && sp.Kind != EnemyKind.Prior) { sb.AppendLine(m.Id + ": UNREACHABLE spawn " + sp.Kind + " at " + (sp.X / 16f).ToString("0.0") + "," + (sp.Y / 16f).ToString("0.0")); bad++; }
            }
            foreach (NpcDef n in m.NpcDefs)
                if (m.Blocked(n.X, n.Y, 5, 1)) { sb.AppendLine(m.Id + ": npc inside solid " + n.Id); bad++; }
            sb.AppendLine(m.Id + ": reachability problems = " + bad);
        }

        static void Overview(Map m, string path, int scale)
        {
            Surface s = new Surface(m.TW, m.TH);
            for (int i = 0; i < m.Mini.Length; i++) s.Px[i] = m.Mini[i];
            foreach (SpawnDef sp in m.Spawns) s.Pixel((int)(sp.X / 16), (int)(sp.Y / 16), sp.Kind == EnemyKind.Knight || sp.Kind == EnemyKind.Prior ? 0xFF00FF : 0xFF4040);
            foreach (NpcDef n in m.NpcDefs) s.Pixel((int)(n.X / 16), (int)(n.Y / 16), 0xFFFF40);
            foreach (Prop p in m.Props)
            {
                if (p.Kind == PropKind.Chest) s.Pixel((int)(p.X / 16), (int)(p.Y / 16), 0xFFC040);
                else if (p.Kind == PropKind.Shrine) s.Pixel((int)(p.X / 16), (int)(p.Y / 16), 0x40FFFF);
                else if (p.Kind == PropKind.RuneStone) s.Pixel((int)(p.X / 16), (int)(p.Y / 16), 0xFFFFFF);
                else if (p.Kind == PropKind.Moonpetal) s.Pixel((int)(p.X / 16), (int)(p.Y / 16), 0xC0C0FF);
            }
            s.SavePng(path, scale);
        }

        /// <summary>Paints where a hero's body can really stand, as reached on foot from the start.</summary>
        static void ReachOverlay(Map m, int tx, int ty, int tw, int th, string path, int scale)
        {
            Region(m, tx, ty, tw, th, path, 1);
            Surface s = new Surface(tw * 16, th * 16);
            int ox = tx * 16, oy = ty * 16;
            s.CopyFrom(m.Ground, m.PW, m.PH, ox, oy);
            List<Prop> props = new List<Prop>();
            m.Query(ox - 140, oy - 60, ox + s.W + 140, oy + s.H + 220, props);
            props.Sort(delegate(Prop a, Prop b) { return a.Y.CompareTo(b.Y); });
            foreach (Prop p in props) if (!p.Hidden && p.Spr != null) s.Blit(p.Spr, (int)p.X - ox, (int)p.Y - oy, p.Flip);
            Marker st = m.Find("start");
            bool[] reach = m.ReachableBody(st.X, st.Y, 4);
            int w = m.PW / Map.RS;
            for (int y = 0; y < s.H; y += Map.RS)
                for (int x = 0; x < s.W; x += Map.RS)
                {
                    int gx = (ox + x) / Map.RS, gy = (oy + y) / Map.RS;
                    if (reach[gy * w + gx]) s.FillA(x + 1, y + 1, 2, 2, 0x30FF60, 200);
                }
            // trace the route the flood took into the glade, so a leak can be seen
            Prop chest = m.FindProp("chest_glade");
            int[] from = new int[reach.Length];
            for (int i = 0; i < from.Length; i++) from[i] = -1;
            int sx0 = (int)(st.X / Map.RS), sy0 = (int)(st.Y / Map.RS), h = m.PH / Map.RS;
            float fx = st.X, fy = st.Y; m.FindFree(ref fx, ref fy, 4, 1); sx0 = (int)(fx / Map.RS); sy0 = (int)(fy / Map.RS);
            Queue<int> q = new Queue<int>(); q.Enqueue(sy0 * w + sx0); from[sy0 * w + sx0] = sy0 * w + sx0;
            int goal = ((int)((chest.Y + 10) / Map.RS)) * w + (int)(chest.X / Map.RS), hit = -1;
            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                if (Math.Abs(cur % w - goal % w) <= 3 && Math.Abs(cur / w - goal / w) <= 3) { hit = cur; break; }
                int cx = cur % w, cy = cur / w;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + (k == 0 ? 1 : (k == 1 ? -1 : 0)), ny = cy + (k == 2 ? 1 : (k == 3 ? -1 : 0));
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (from[ni] >= 0 || !reach[ni]) continue;
                    from[ni] = cur; q.Enqueue(ni);
                }
            }
            StringBuilder sb = new StringBuilder();
            for (int c = hit; c >= 0 && from[c] != c; c = from[c])
            {
                int x = (c % w) * Map.RS - ox, y = (c / w) * Map.RS - oy;
                if (x >= 0 && y >= 0 && x < s.W && y < s.H) { s.Fill(x, y, 3, 3, 0xFF2020); sb.AppendLine(((c % w) * Map.RS / 16f).ToString("0.00") + "," + ((c / w) * Map.RS / 16f).ToString("0.00")); }
            }
            File.WriteAllText(path + ".txt", sb.ToString());
            s.SavePng(path, scale);
        }

        public static void Region(Map m, int tx, int ty, int tw, int th, string path, int scale)
        {
            Surface s = new Surface(tw * 16, th * 16);
            int ox = tx * 16, oy = ty * 16;
            s.CopyFrom(m.Ground, m.PW, m.PH, ox, oy);
            List<Prop> props = new List<Prop>();
            m.Query(ox - 140, oy - 60, ox + s.W + 140, oy + s.H + 220, props);
            props.Sort(delegate(Prop a, Prop b) { int c = a.Flat.CompareTo(b.Flat); if (c != 0) return -c; return a.Y.CompareTo(b.Y); });
            foreach (Prop p in props)
            {
                if (p.Hidden || p.Spr == null) continue;
                s.Blit(p.Spr, (int)p.X - ox, (int)p.Y - oy, p.Flip);
            }
            foreach (NpcDef n in m.NpcDefs) s.Blit(n.Spr.Idle[0], (int)n.X - ox, (int)n.Y - oy, n.FaceLeft);
            foreach (SpawnDef sp in m.Spawns)
            {
                int x = (int)sp.X - ox, y = (int)sp.Y - oy;
                s.Fill(x - 2, y - 2, 5, 5, 0xFF3030);
                s.Text(Fonts.Small, sp.Kind.ToString().Substring(0, 2), x + 4, y - 8, 0xFFFFFF, 0);
            }
            // collision overlay (faint) to check footprints
            for (int cy = 0; cy < th * 2; cy++)
                for (int cx = 0; cx < tw * 2; cx++)
                {
                    int gx = tx * 2 + cx, gy = ty * 2 + cy;
                    if (gx >= m.CW || gy >= m.CH) continue;
                    byte b = m.Solid[gy * m.CW + gx];
                    if ((b & 1) != 0) s.FillA(cx * 8 + 2, cy * 8 + 2, 4, 4, (b & 2) != 0 ? 0xFF2020 : 0x40A0FF, 70);
                }
            s.SavePng(path, scale);
        }
    }
}
