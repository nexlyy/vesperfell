using System;
using System.Collections.Generic;

namespace Vesperfell
{
    public enum PropKind
    {
        Deco, Tree, Chest, Shrine, Moonpetal, Bramble, Breakable, RuneStone, Cache, Brazier, Gate, CrackedWall, Bell, Stairs,
        AbbeyDoor, Sign, Keepsake, Campfire, Well, BossGate
    }

    /// <summary>A static object in the world: scenery, light source or something to interact with.</summary>
    public sealed class Prop
    {
        public PropKind Kind;
        public float X, Y;                  // base point in world pixels
        public Sprite Spr;
        public Sprite[] Anim;
        public int AnimOff;
        public bool Tall, Flat, Sway, Hidden, Flip, OnWall;
        public int ColW, ColH;              // solid footprint: centred on X, rising from Y
        public bool BlocksShots;
        public float LightR, LightI, LightOY; public int LightColor; public bool Flicker;
        public string Id = "";
        public string Text;
        public int State, Param;
        public float HP, ShakeT;
        public bool Interactive;
        public float InteractR = 22;
        public string Prompt = "";
        public float ShadowR;
        public int Uid;                     // stable number, the same on every machine (0 for things added during play)
    }

    public sealed class SpawnDef
    {
        public EnemyKind Kind; public float X, Y; public int Level; public string Id = ""; public bool Once; public int Variant;
        public bool Summoned;
    }

    public sealed class NpcDef
    {
        public string Id, Name; public float X, Y; public ActorSprites Spr; public bool Wander; public bool FaceLeft;
    }

    public sealed class Marker { public string Id; public float X, Y; public float R; }

    /// <summary>One playable map: terrain, collision, props and what lives there.</summary>
    public sealed class Map
    {
        public const int CS = 8;            // collision / terrain cell size
        public const int TS = 16;           // logical tile size
        const int BS = 64;                  // prop bucket size

        public string Id; public bool Dungeon;
        public int TW, TH, CW, CH, PW, PH;
        public byte[] Cell;                 // terrain id per cell
        public byte[] Solid;                // bit0 blocks walking, bit1 blocks shots, sight and flyers
        public byte[] ZoneMap;              // per tile
        public int[] Ground;
        public List<Prop> Props = new List<Prop>();
        public List<SpawnDef> Spawns = new List<SpawnDef>();
        public List<NpcDef> NpcDefs = new List<NpcDef>();
        public Dictionary<string, Marker> Markers = new Dictionary<string, Marker>();
        public int[] Mini; public bool[] Seen;
        List<Prop>[] buckets; int bw, bh;

        // flow fields: a window of walking distances around each hero (an enemy follows the field of whoever it hunts)
        const int FW = 96;
        public const int FlowSlots = 4;
        readonly ushort[][] flows = new ushort[FlowSlots][];
        readonly int[] flowX0s = new int[FlowSlots], flowY0s = new int[FlowSlots];
        readonly int[] queue = new int[FW * FW];
        public int FlowSlot; public bool FlowValid;
        public Dictionary<int, Prop> ByUid = new Dictionary<int, Prop>();
        public int Checksum;
        byte[] wide;                        // walk-blocking cells dilated by one, so paths keep clear of corners

        public Map(string id, int tw, int th, bool dungeon)
        {
            Id = id; TW = tw; TH = th; Dungeon = dungeon;
            CW = tw * 2; CH = th * 2; PW = tw * TS; PH = th * TS;
            Cell = new byte[CW * CH]; Solid = new byte[CW * CH]; ZoneMap = new byte[TW * TH];
            Mini = new int[TW * TH]; Seen = new bool[TW * TH];
            bw = (PW + BS - 1) / BS; bh = (PH + BS - 1) / BS;
            buckets = new List<Prop>[bw * bh];
            for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<Prop>();
        }

        public void Mark(string id, float x, float y, float r) { Marker k = new Marker(); k.Id = id; k.X = x; k.Y = y; k.R = r; Markers[id] = k; }
        public Marker Find(string id) { Marker k; return Markers.TryGetValue(id, out k) ? k : null; }

        // terrain / zones
        public byte TerrainAt(float x, float y)
        {
            int cx = (int)(x / CS), cy = (int)(y / CS);
            if (cx < 0 || cy < 0 || cx >= CW || cy >= CH) return T.Void;
            return Cell[cy * CW + cx];
        }

        public int ZoneAt(float x, float y)
        {
            int tx = M.Clamp((int)(x / TS), 0, TW - 1), ty = M.Clamp((int)(y / TS), 0, TH - 1);
            return ZoneMap[ty * TW + tx];
        }

        // collision
        public bool SolidCell(int cx, int cy, int mask)
        {
            if (cx < 0 || cy < 0 || cx >= CW || cy >= CH) return true;
            return (Solid[cy * CW + cx] & mask) != 0;
        }

        /// <summary>Is a body of half-width r blocked at this position? (Bodies are flattened boxes on the ground plane.)</summary>
        public bool Blocked(float x, float y, float r, int mask)
        {
            int x0 = M.Floor((x - r) / CS), x1 = M.Floor((x + r - 0.01f) / CS);
            int y0 = M.Floor((y - r * 0.7f) / CS), y1 = M.Floor((y + r * 0.5f - 0.01f) / CS);
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                    if (SolidCell(cx, cy, mask)) return true;
            return false;
        }

        public bool PointBlocked(float x, float y, int mask) { return SolidCell(M.Floor(x / CS), M.Floor(y / CS), mask); }

        /// <summary>Moves an actor with sliding collision. Returns flags: 1 = hit something on X, 2 = on Y.</summary>
        public int Move(Actor a, float dx, float dy, int mask)
        {
            int hit = 0;
            float m = Math.Max(Math.Abs(dx), Math.Abs(dy));
            int steps = (int)(m / 3f) + 1;
            float sx = dx / steps, sy = dy / steps, r = a.Radius;
            for (int i = 0; i < steps; i++)
            {
                if (sx != 0)
                {
                    if (!Blocked(a.X + sx, a.Y, r, mask)) a.X += sx;
                    else
                    {
                        hit |= 1;
                        // nudge around corners
                        if (Math.Abs(sy) < 0.01f)
                        {
                            if (!Blocked(a.X + sx, a.Y - 3, r, mask) && !Blocked(a.X, a.Y - 1, r, mask)) a.Y -= Math.Min(1f, Math.Abs(sx));
                            else if (!Blocked(a.X + sx, a.Y + 3, r, mask) && !Blocked(a.X, a.Y + 1, r, mask)) a.Y += Math.Min(1f, Math.Abs(sx));
                        }
                    }
                }
                if (sy != 0)
                {
                    if (!Blocked(a.X, a.Y + sy, r, mask)) a.Y += sy;
                    else
                    {
                        hit |= 2;
                        if (Math.Abs(sx) < 0.01f)
                        {
                            if (!Blocked(a.X - 3, a.Y + sy, r, mask) && !Blocked(a.X - 1, a.Y, r, mask)) a.X -= Math.Min(1f, Math.Abs(sy));
                            else if (!Blocked(a.X + 3, a.Y + sy, r, mask) && !Blocked(a.X + 1, a.Y, r, mask)) a.X += Math.Min(1f, Math.Abs(sy));
                        }
                    }
                }
            }
            return hit;
        }

        /// <summary>Line of sight / line of fire test against shot-blocking cells.</summary>
        public bool Los(float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float len = M.Len(dx, dy);
            int n = (int)(len / 4f) + 1;
            for (int i = 1; i < n; i++)
            {
                float t = i / (float)n;
                if (PointBlocked(x0 + dx * t, y0 + dy * t, 2)) return false;
            }
            return true;
        }

        /// <summary>Finds the nearest free spot to (x, y) for a body of radius r, spiralling outwards.</summary>
        public bool FindFree(ref float x, ref float y, float r, int mask)
        {
            if (!Blocked(x, y, r, mask)) return true;
            for (int ring = 1; ring < 24; ring++)
                for (int k = 0; k < ring * 8; k++)
                {
                    float a = k * M.TAU / (ring * 8);
                    float nx = x + M.Cos(a) * ring * 5, ny = y + M.Sin(a) * ring * 5;
                    if (!Blocked(nx, ny, r, mask)) { x = nx; y = ny; return true; }
                }
            return false;
        }

        // props
        /// <summary>Bumped whenever a prop is added or removed (the 3D view rebuilds what changed).</summary>
        public int Version;

        public Prop Add(Prop p)
        {
            Version++;
            Props.Add(p);
            BucketOf(p.X, p.Y).Add(p);
            Stamp(p, true);
            return p;
        }

        public void Remove(Prop p)
        {
            if (!Props.Remove(p)) return;
            Version++;
            BucketOf(p.X, p.Y).Remove(p);
            Unstamp(p);
        }

        List<Prop> BucketOf(float x, float y)
        {
            int bx = M.Clamp((int)(x / BS), 0, bw - 1), by = M.Clamp((int)(y / BS), 0, bh - 1);
            return buckets[by * bw + bx];
        }

        public void Stamp(Prop p, bool set)
        {
            if (p.ColW <= 0 || p.ColH <= 0) return;
            int x0 = M.Floor((p.X - p.ColW * 0.5f) / CS), x1 = M.Floor((p.X + p.ColW * 0.5f - 0.5f) / CS);
            int y0 = M.Floor((p.Y - p.ColH) / CS), y1 = M.Floor((p.Y - 0.5f) / CS);
            byte bits = (byte)(p.BlocksShots ? 3 : 1);
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    if (cx < 0 || cy < 0 || cx >= CW || cy >= CH) continue;
                    if (set) Solid[cy * CW + cx] |= bits;
                }
            wide = null;
        }

        /// <summary>Clears a prop's footprint, then re-stamps whatever else still stands on those cells.</summary>
        public void Unstamp(Prop p)
        {
            if (p.ColW <= 0 || p.ColH <= 0) return;
            int x0 = M.Floor((p.X - p.ColW * 0.5f) / CS), x1 = M.Floor((p.X + p.ColW * 0.5f - 0.5f) / CS);
            int y0 = M.Floor((p.Y - p.ColH) / CS), y1 = M.Floor((p.Y - 0.5f) / CS);
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    if (cx < 0 || cy < 0 || cx >= CW || cy >= CH) continue;
                    Solid[cy * CW + cx] = TerrainSolid(Cell[cy * CW + cx]);
                }
            List<Prop> near = new List<Prop>();
            Query(p.X - 160, p.Y - 160, p.X + 160, p.Y + 160, near);
            foreach (Prop q in near) if (q != p) Stamp(q, true);
            wide = null;
        }

        public static byte TerrainSolid(byte t)
        {
            if (t == T.Water) return 1;
            if (t == T.Thicket || t == T.DWall || t == T.Void) return 3;
            return 0;
        }

        public void Query(float x0, float y0, float x1, float y1, List<Prop> result)
        {
            int bx0 = M.Clamp((int)(x0 / BS), 0, bw - 1), bx1 = M.Clamp((int)(x1 / BS), 0, bw - 1);
            int by0 = M.Clamp((int)(y0 / BS), 0, bh - 1), by1 = M.Clamp((int)(y1 / BS), 0, bh - 1);
            for (int by = by0; by <= by1; by++)
                for (int bx = bx0; bx <= bx1; bx++)
                {
                    List<Prop> b = buckets[by * bw + bx];
                    for (int i = 0; i < b.Count; i++) result.Add(b[i]);
                }
        }

        /// <summary>
        /// Numbers every generated prop. The world is built from a fixed seed, so the numbers agree on every machine;
        /// the checksum lets two machines notice if they somehow do not.
        /// </summary>
        public void AssignUids()
        {
            ByUid.Clear();
            uint h = 2166136261;
            for (int i = 0; i < Props.Count; i++)
            {
                Prop p = Props[i];
                p.Uid = i + 1; ByUid[p.Uid] = p;
                h = (h ^ (uint)((int)p.Kind * 31 + (int)p.X * 7 + (int)p.Y)) * 16777619;
            }
            Checksum = (int)h;
        }

        public Prop FindProp(string id)
        {
            for (int i = 0; i < Props.Count; i++) if (Props[i].Id == id) return Props[i];
            return null;
        }

        // pathing
        void BuildWide()
        {
            wide = new byte[CW * CH];
            for (int y = 0; y < CH; y++)
                for (int x = 0; x < CW; x++)
                {
                    bool s = (Solid[y * CW + x] & 1) != 0;
                    if (!s && x > 0 && (Solid[y * CW + x - 1] & 1) != 0) s = true;
                    if (!s && x < CW - 1 && (Solid[y * CW + x + 1] & 1) != 0) s = true;
                    wide[y * CW + x] = (byte)(s ? 1 : 0);
                }
        }

        /// <summary>Breadth-first distances from the target, used by ground enemies to route around obstacles.</summary>
        public void BuildFlow(float px, float py)
        {
            if (wide == null) BuildWide();
            ushort[] flow = flows[FlowSlot];
            if (flow == null) flow = flows[FlowSlot] = new ushort[FW * FW];
            int pcx = (int)(px / CS), pcy = (int)(py / CS);
            int flowX0 = flowX0s[FlowSlot] = pcx - FW / 2, flowY0 = flowY0s[FlowSlot] = pcy - FW / 2;
            for (int i = 0; i < flow.Length; i++) flow[i] = ushort.MaxValue;
            int head = 0, tail = 0;
            int sx = FW / 2, sy = FW / 2;
            flow[sy * FW + sx] = 0; queue[tail++] = sy * FW + sx;
            while (head < tail)
            {
                int cur = queue[head++];
                int lx = cur % FW, ly = cur / FW;
                ushort d = flow[cur];
                for (int k = 0; k < 4; k++)
                {
                    int nx = lx + (k == 0 ? 1 : (k == 1 ? -1 : 0)), ny = ly + (k == 2 ? 1 : (k == 3 ? -1 : 0));
                    if (nx < 0 || ny < 0 || nx >= FW || ny >= FW) continue;
                    int ni = ny * FW + nx;
                    if (flow[ni] != ushort.MaxValue) continue;
                    int gx = flowX0 + nx, gy = flowY0 + ny;
                    if (gx < 0 || gy < 0 || gx >= CW || gy >= CH) continue;
                    if (wide[gy * CW + gx] != 0) continue;
                    flow[ni] = (ushort)(d + 1);
                    queue[tail++] = ni;
                }
            }
            FlowValid = true;
        }

        /// <summary>Direction of steepest descent toward the flow target. False when off the field or unreachable.</summary>
        public bool FlowDir(float x, float y, out float dx, out float dy)
        {
            dx = 0; dy = 0;
            ushort[] flow = flows[FlowSlot];
            if (!FlowValid || flow == null) return false;
            int flowX0 = flowX0s[FlowSlot], flowY0 = flowY0s[FlowSlot];
            int lx = (int)(x / CS) - flowX0, ly = (int)(y / CS) - flowY0;
            if (lx < 1 || ly < 1 || lx >= FW - 1 || ly >= FW - 1) return false;
            ushort here = flow[ly * FW + lx];
            int best = here; int bx = 0, by = 0;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    if (i == 0 && j == 0) continue;
                    ushort v = flow[(ly + j) * FW + lx + i];
                    if (v == ushort.MaxValue) continue;
                    // no diagonal corner cutting
                    if (i != 0 && j != 0 && (flow[ly * FW + lx + i] == ushort.MaxValue || flow[(ly + j) * FW + lx] == ushort.MaxValue)) continue;
                    if (v < best) { best = v; bx = i; by = j; }
                }
            if (here == ushort.MaxValue)
            {
                // standing in a "too tight" cell: head for any reachable neighbour
                if (best == ushort.MaxValue) return false;
            }
            if (bx == 0 && by == 0) return false;
            // aim at the centre of the next cell so actors do not scrape along walls
            float tx = ((int)(x / CS) + bx + 0.5f) * CS, ty = ((int)(y / CS) + by + 0.5f) * CS;
            dx = tx - x; dy = ty - y;
            float len = M.Len(dx, dy);
            if (len < 0.001f) return false;
            dx /= len; dy /= len;
            return true;
        }

        public int FlowDist(float x, float y)
        {
            ushort[] flow = flows[FlowSlot];
            if (!FlowValid || flow == null) return int.MaxValue;
            int flowX0 = flowX0s[FlowSlot], flowY0 = flowY0s[FlowSlot];
            int lx = (int)(x / CS) - flowX0, ly = (int)(y / CS) - flowY0;
            if (lx < 0 || ly < 0 || lx >= FW || ly >= FW) return int.MaxValue;
            ushort v = flow[ly * FW + lx];
            return v == ushort.MaxValue ? int.MaxValue : v;
        }

        /// <summary>Flood fill from a point over walkable cells; used by tests to prove every objective is reachable.</summary>
        public bool[] Reachable(float x, float y) { return Reachable(x, y, true); }

        public bool[] Reachable(float x, float y, bool keepClear)
        {
            bool[] seen = new bool[CW * CH];
            int sx = (int)(x / CS), sy = (int)(y / CS);
            if (sx < 0 || sy < 0 || sx >= CW || sy >= CH) return seen;
            Queue<int> q = new Queue<int>();
            q.Enqueue(sy * CW + sx); seen[sy * CW + sx] = true;
            if (wide == null) BuildWide();
            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                int cx = cur % CW, cy = cur / CW;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + (k == 0 ? 1 : (k == 1 ? -1 : 0)), ny = cy + (k == 2 ? 1 : (k == 3 ? -1 : 0));
                    if (nx < 0 || ny < 0 || nx >= CW || ny >= CH) continue;
                    int ni = ny * CW + nx;
                    if (seen[ni] || (keepClear ? wide[ni] != 0 : (Solid[ni] & 1) != 0)) continue;
                    seen[ni] = true; q.Enqueue(ni);
                }
            }
            return seen;
        }

        public const int RS = 4;        // step of the body-accurate reachability grid, in pixels

        /// <summary>Flood fill over the places a body of the given radius can really stand (finer and truer than the cell fill).</summary>
        public bool[] ReachableBody(float x, float y, float r)
        {
            int w = PW / RS, h = PH / RS;
            bool[] seen = new bool[w * h];
            float fx = x, fy = y;
            FindFree(ref fx, ref fy, r, 1);
            int sx = (int)(fx / RS), sy = (int)(fy / RS);
            if (sx < 0 || sy < 0 || sx >= w || sy >= h) return seen;
            Queue<int> q = new Queue<int>();
            seen[sy * w + sx] = true; q.Enqueue(sy * w + sx);
            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                int cx = cur % w, cy = cur / w;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + (k == 0 ? 1 : (k == 1 ? -1 : 0)), ny = cy + (k == 2 ? 1 : (k == 3 ? -1 : 0));
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (seen[ni] || Blocked(nx * RS, ny * RS, r, 1)) continue;
                    seen[ni] = true; q.Enqueue(ni);
                }
            }
            return seen;
        }

        public bool BodyNear(bool[] reach, float x, float y, int steps)
        {
            int w = PW / RS, h = PH / RS, cx = (int)(x / RS), cy = (int)(y / RS);
            for (int j = -steps; j <= steps; j++)
                for (int i = -steps; i <= steps; i++)
                {
                    int nx = cx + i, ny = cy + j;
                    if (nx >= 0 && ny >= 0 && nx < w && ny < h && reach[ny * w + nx]) return true;
                }
            return false;
        }

        public bool ReachableNear(bool[] reach, float x, float y, int cells)
        {
            int cx = (int)(x / CS), cy = (int)(y / CS);
            for (int j = -cells; j <= cells; j++)
                for (int i = -cells; i <= cells; i++)
                {
                    int nx = cx + i, ny = cy + j;
                    if (nx < 0 || ny < 0 || nx >= CW || ny >= CH) continue;
                    if (reach[ny * CW + nx]) return true;
                }
            return false;
        }

        // minimap
        public void BuildMini()
        {
            for (int ty = 0; ty < TH; ty++)
                for (int tx = 0; tx < TW; tx++)
                {
                    byte t = Cell[(ty * 2) * CW + tx * 2];
                    int c;
                    switch (t)
                    {
                        case T.Grass: c = 0x3F6648; break;
                        case T.Forest: c = 0x24443A; break;
                        case T.PaleGrass: c = 0x55705E; break;
                        case T.Ash: c = 0x4A4D52; break;
                        case T.Thicket: c = 0x14262A; break;
                        case T.Dirt: c = 0x7A5A44; break;
                        case T.Cobble: c = 0x767E90; break;
                        case T.Stone: c = 0x6A7288; break;
                        case T.Water: c = 0x2A5C74; break;
                        case T.Mud: c = 0x3C3630; break;
                        case T.Plank: case T.PlankV: c = 0x8A6848; break;
                        case T.DFloor: c = 0x4A506A; break;
                        case T.DCarpet: c = 0x7A2038; break;
                        case T.DRitual: c = 0x5A4A8A; break;
                        case T.DWall: c = 0x14131E; break;
                        default: c = 0x08080C; break;
                    }
                    Mini[ty * TW + tx] = c;
                }
            foreach (Prop p in Props)
            {
                int tx = (int)(p.X / TS), ty = (int)((p.Y - 4) / TS);
                if (tx < 0 || ty < 0 || tx >= TW || ty >= TH) continue;
                if (p.Kind == PropKind.Tree) Mini[ty * TW + tx] = Col.Lerp(Mini[ty * TW + tx], 0x16302C, 0.55f) & 0xFFFFFF;
                else if (p.ColW >= 40)
                {
                    int w = p.ColW / TS / 2;
                    for (int j = 0; j <= p.ColH / TS; j++)
                        for (int i = -w; i <= w; i++)
                        {
                            int ax = tx + i, ay = ty - j;
                            if (ax >= 0 && ay >= 0 && ax < TW && ay < TH) Mini[ay * TW + ax] = 0x8E6A50;
                        }
                }
            }
        }

        public void Reveal(float x, float y, int radiusTiles)
        {
            int tx = (int)(x / TS), ty = (int)(y / TS);
            int r2 = radiusTiles * radiusTiles;
            for (int j = -radiusTiles; j <= radiusTiles; j++)
                for (int i = -radiusTiles; i <= radiusTiles; i++)
                {
                    if (i * i + j * j > r2) continue;
                    int ax = tx + i, ay = ty + j;
                    if (ax >= 0 && ay >= 0 && ax < TW && ay < TH) Seen[ay * TW + ax] = true;
                }
        }
    }
}
