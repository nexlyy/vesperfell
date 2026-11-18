using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Vesperfell
{
    /// <summary>
    /// Saving and loading. Progress is written as a small text file; a loaded game resumes at the last wayshrine
    /// the player rested at, with the world rebuilt from its seed and the saved flags re-applied on top.
    /// </summary>
    public sealed partial class Game
    {
        public string SavePath { get { return Path.Combine(Path.Combine(DataDir, "save"), "vesperfell.sav"); } }
        public string LastSaveError = "";

        public bool HasSave() { try { return File.Exists(SavePath); } catch { return false; } }

        static string Pack(bool[] bits)
        {
            byte[] b = new byte[(bits.Length + 7) / 8];
            for (int i = 0; i < bits.Length; i++) if (bits[i]) b[i >> 3] |= (byte)(1 << (i & 7));
            return Convert.ToBase64String(b);
        }

        static void Unpack(string s, bool[] bits)
        {
            byte[] b = Convert.FromBase64String(s);
            for (int i = 0; i < bits.Length && (i >> 3) < b.Length; i++) bits[i] = (b[i >> 3] & (1 << (i & 7))) != 0;
        }

        public bool SaveGame()
        {
            Player p = Me;
            if (p == null || Over == null || IsClient) return false;      // a guest's journey lives in the host's save
            try
            {
                CultureInfo ci = CultureInfo.InvariantCulture;
                List<string> l = new List<string>();
                l.Add("version=1");
                l.Add("class=" + (int)p.Class);
                l.Add("level=" + p.Level); l.Add("xp=" + p.XP); l.Add("gold=" + p.Gold); l.Add("points=" + p.AttrPoints);
                l.Add("might=" + p.Might); l.Add("finesse=" + p.Finesse); l.Add("arcana=" + p.Arcana); l.Add("vigor=" + p.Vigor);
                l.Add("hpot=" + p.HealthPotions); l.Add("spot=" + p.SpiritPotions);
                l.Add("kills=" + p.Kills); l.Add("deaths=" + p.Deaths);
                l.Add("time=" + PlayTime.ToString("0.0", ci));
                l.Add("respawn=" + p.RespawnMap + "|" + p.RespawnX.ToString("0.0", ci) + "|" + p.RespawnY.ToString("0.0", ci));
                l.Add("dawn=" + (Dawn ? 1 : 0));
                l.Add("tracked=" + Q.Tracked);
                l.Add("rumour=" + rumour);
                foreach (string f in Flags) l.Add("flag=" + f);
                foreach (Quest q in Q.All) l.Add("quest=" + q.Id + "|" + q.Stage + "|" + (q.Done ? 1 : 0) + "|" + q.Count);
                for (int i = 0; i < p.Equip.Length; i++) if (p.Equip[i] != null) l.Add("equip" + i + "=" + p.Equip[i].Serialize());
                for (int i = 0; i < p.Bag.Length; i++) if (p.Bag[i] != null) l.Add("bag" + i + "=" + p.Bag[i].Serialize());
                foreach (Item k in p.Keepsakes) l.Add("keep=" + k.QuestId + "|" + k.Count);
                l.Add("seen_over=" + Pack(Over.Seen));
                l.Add("seen_abbey=" + Pack(Abbey.Seen));
                string dir = Path.GetDirectoryName(SavePath);
                Directory.CreateDirectory(dir);
                string tmp = SavePath + ".tmp";
                File.WriteAllLines(tmp, l.ToArray(), Encoding.UTF8);
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(tmp, SavePath);
                LastSaveError = "";
                return true;
            }
            catch (Exception ex)
            {
                LastSaveError = ex.Message;
                Notify(Loc.F("^rCould not save: {0}^0", ex.Message));
                return false;
            }
        }

        sealed class SaveData
        {
            public Dictionary<string, string> Kv = new Dictionary<string, string>();
            public List<string> Flags = new List<string>(), Quests = new List<string>(), Keeps = new List<string>();
            public int Int(string k, int def) { string v; int r; return Kv.TryGetValue(k, out v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out r) ? r : def; }
            public string Str(string k, string def) { string v; return Kv.TryGetValue(k, out v) ? v : def; }
        }

        SaveData ReadSave()
        {
            SaveData d = new SaveData();
            foreach (string line in File.ReadAllLines(SavePath, Encoding.UTF8))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                if (k == "flag") d.Flags.Add(v);
                else if (k == "quest") d.Quests.Add(v);
                else if (k == "keep") d.Keeps.Add(v);
                else d.Kv[k] = v;
            }
            if (d.Int("version", 0) != 1) throw new InvalidDataException("Unknown save version");
            int cls = d.Int("class", -1);
            if (cls < 0 || cls > 3) throw new InvalidDataException("Save is missing its class");
            return d;
        }

        public void LoadGame()
        {
            SaveData d;
            try { d = ReadSave(); }
            catch (Exception ex)
            {
                LastSaveError = ex.Message;
                hasSave = false;
                Snd(Sfx.UiError);
                return;
            }
            RebuildMapsThen(delegate
            {
                mapsDirty = true;
                CultureInfo ci = CultureInfo.InvariantCulture;
                Flags.Clear();
                foreach (string f in d.Flags) Flags.Add(f);
                Dawn = d.Int("dawn", 0) != 0; DawnT = 99;
                double pt; double.TryParse(d.Str("time", "0"), NumberStyles.Float, ci, out pt); PlayTime = pt;
                WorldT = 0;
                rumour = d.Int("rumour", 0);

                Player p = CreatePlayer((ClassId)d.Int("class", 0));
                p.Level = M.Clamp(d.Int("level", 1), 1, Player.MaxLevel); p.XP = Math.Max(0, d.Int("xp", 0)); p.Gold = Math.Max(0, d.Int("gold", 0)); p.AttrPoints = Math.Max(0, d.Int("points", 0));
                p.Might = d.Int("might", p.Might); p.Finesse = d.Int("finesse", p.Finesse); p.Arcana = d.Int("arcana", p.Arcana); p.Vigor = d.Int("vigor", p.Vigor);
                p.HealthPotions = Math.Max(0, d.Int("hpot", 0)); p.SpiritPotions = Math.Max(0, d.Int("spot", 0));
                p.Kills = d.Int("kills", 0); p.Deaths = d.Int("deaths", 0);
                for (int i = 0; i < p.Equip.Length; i++) { string s = d.Str("equip" + i, null); p.Equip[i] = s == null ? null : Item.Parse(s); }
                for (int i = 0; i < p.Bag.Length; i++) { string s = d.Str("bag" + i, null); p.Bag[i] = s == null ? null : Item.Parse(s); }
                p.Keepsakes.Clear();
                foreach (string k in d.Keeps)
                {
                    string[] parts = k.Split('|');
                    int n; if (parts.Length == 2 && int.TryParse(parts[1], out n) && n > 0) p.AddKeepsake(parts[0], n);
                }
                p.Recalc(true);
                P = Me = p;
                Me.NetName = CleanName(Cfg.PlayerName);
                RebuildParty();

                Q = new QuestLog();
                foreach (string qs in d.Quests)
                {
                    string[] parts = qs.Split('|');
                    if (parts.Length < 4) continue;
                    Quest q = Q.Get(parts[0]);
                    if (q == null) continue;
                    int.TryParse(parts[1], out q.Stage); q.Done = parts[2] == "1"; int.TryParse(parts[3], out q.Count);
                }
                string tr = d.Str("tracked", "bell");
                Q.Tracked = Q.Get(tr) != null ? tr : "bell";

                try { Unpack(d.Str("seen_over", ""), Over.Seen); Unpack(d.Str("seen_abbey", ""), Abbey.Seen); } catch { }

                string[] rs = d.Str("respawn", "overworld|0|0").Split('|');
                Map m = MapById(rs.Length > 0 ? rs[0] : "overworld");
                float rx = 0, ry = 0;
                if (rs.Length >= 3) { float.TryParse(rs[1], NumberStyles.Float, ci, out rx); float.TryParse(rs[2], NumberStyles.Float, ci, out ry); }
                if (rx <= 0 || ry <= 0 || rx >= m.PW || ry >= m.PH) { Marker s = Over.Find("start"); m = Over; rx = s.X; ry = s.Y; }
                p.RespawnMap = m.Id; p.RespawnX = rx; p.RespawnY = ry;

                Scr = Screen.Play; Ov = Overlay.None;
                ApplyFlags(Over); ApplyFlags(Abbey);
                if (Flags.Contains("wren_lantern")) AddWrenLantern();
                float px = rx, py = ry + 14;
                m.FindFree(ref px, ref py, p.Radius, 1);
                EnterMap(m, px, py);
                IntroT = 999; hintStep = 4;
                FadeT = FadeDur; FadeDir = -1;
                Notify("^yWelcome back, Warden.^0");
            });
        }
    }
}
