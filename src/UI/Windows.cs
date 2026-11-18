using System;
using System.Collections.Generic;
using System.Globalization;

namespace Vesperfell
{
    /// <summary>The tabbed character window: inventory and equipment, attributes, skills, quest journal and map.</summary>
    public sealed partial class Game
    {
        static readonly string[] TabNames = { "Inventory", "Character", "Skills", "Journal", "Map" };
        static readonly string[] TabKeys = { "I", "C", "K", "J", "M" };
        const int WX = 96, WY = 34, WW = 448, WH = 272;
        string selQuest;

        void WindowUi()
        {
            G.FillA(0, 0, W, H, 0x05050A, 120);
            Panel(WX, WY, WW, WH);
            for (int i = 0; i < TabNames.Length; i++)
            {
                int tx = WX + 10 + i * 84, ty = WY + 7, tw = 82, th = 17;
                bool sel = WinTab == i, hot = Hot(tx, ty, tw, th);
                Remember("tab:" + TabNames[i], tx, ty, tw, th);
                if (hot) hotNow = "tab" + i;
                G.Fill(tx, ty, tw, th, CInk);
                G.GradV(tx + 1, ty + 1, tw - 2, th - 1, sel ? 0x3A2E5E : (hot ? 0x2E2843 : 0x1D1930), sel ? 0x241E36 : 0x15121F);
                if (sel) G.Fill(tx + 1, ty + th - 2, tw - 2, 2, CGold);
                string label = TabNames[i];
                G.Text(Fonts.Small, label, tx + 6, ty + 3, sel ? CGoldHi : (hot ? CWhite : CText), 0x000000);
                G.TextRight(Fonts.Small, TabKeys[i], tx + tw - 5, ty + 3, sel ? CGold : CDim, 0x000000);
                if (Clicked(tx, ty, tw, th, 0) && !sel) { WinTab = i; Snd(Sfx.UiTab, 0.6f, 1f); }
            }
            if (ButtonId("close", WX + WW - 22, WY + 7, 15, 15, "x", true)) { CloseOverlay(); return; }
            G.HLine(WX + 6, WY + 27, WW - 12, CBronze);
            switch (WinTab)
            {
                case 0: InventoryUi(); break;
                case 1: CharacterUi(); break;
                case 2: SkillsUi(); break;
                case 3: JournalUi(); break;
                case 4: MapUi(); break;
            }
        }

        // inventory
        public bool EquipFromBag(int idx)
        {
            Player p = P;
            Item it = p.Bag[idx];
            if (it == null) return false;
            int slot = Player.SlotFor(it);
            if (slot < 0 || !p.CanUse(it)) { Snd(Sfx.UiError); Notify("^rYou cannot use that.^0"); return false; }
            p.Bag[idx] = p.Equip[slot];
            p.Equip[slot] = it;
            p.Recalc(false);
            Snd(Sfx.Equip);
            return true;
        }

        public bool Unequip(int slot)
        {
            Player p = P;
            if (p.Equip[slot] == null) return false;
            int free = p.FreeBagSlot();
            if (free < 0) { Snd(Sfx.UiError); Notify("^rYour pack is full.^0"); return false; }
            p.Bag[free] = p.Equip[slot]; p.Equip[slot] = null;
            p.Recalc(false);
            Snd(Sfx.Equip, 0.8f, 0.85f);
            return true;
        }

        public void DropFromBag(int idx)
        {
            Player p = P;
            Item it = p.Bag[idx];
            if (it == null) return;
            p.Bag[idx] = null;
            Pickup d = SpawnPickup(Pickup.Equip, p.X, p.Y + 6, 1, it);
            d.T = -2.5f;
            Snd(Sfx.Equip, 0.6f, 0.7f);
        }

        void InventoryUi()
        {
            Player p = P;
            PixFont fs = Fonts.Small;
            int lx = WX + 14, ty = WY + 34;
            Header(lx, ty, 150, "Equipment", Fonts.BodyBold);
            // paper doll
            int dx = lx + 34, dy = ty + 20;
            G.Fill(dx, dy, 82, 104, CInk); G.GradV(dx + 1, dy + 1, 80, 102, 0x241E36, 0x15121F);
            G.Ellipse(dx + 41, dy + 92, 26, 7, p.Def.Accent, 60);
            G.Frame(dx, dy, 82, 104, CBronze);
            G.BlitScaled(p.Spr.Idle[(int)(UiT * 1.6f) & 1], dx + 8, dy + 8, 3, false);
            string[] slotNames = { "Weapon", "Armour", "Amulet", "Ring" };
            int[] sxs = { lx + 6, lx + 124, lx + 6, lx + 124 }, sys = { dy + 52, dy + 52, dy + 14, dy + 14 };
            for (int s = 0; s < 4; s++)
            {
                Item it = p.Equip[s];
                int act = Slot("equip" + s, sxs[s], sys[s], it, false);
                if (it == null) G.Text(Fonts.Tiny, "", sxs[s], sys[s], CDim);
                if (Hot(sxs[s], sys[s], 20, 20))
                {
                    if (it != null) Tip(Items.Describe(it, null, true, "Click to unequip"), 200);
                    else Tip(Loc.F("{0} slot", Loc.Tr(slotNames[s])), "Empty.");
                }
                if (act != 0 && it != null) Unequip(s);
            }
            // summary
            int sy = dy + 110;
            G.Text(fs, "Damage", lx + 2, sy, CDim, 0); G.TextRight(fs, (int)(p.WMin * p.DmgMult) + " - " + (int)(p.WMax * p.DmgMult), lx + 148, sy, CWhite, 0);
            G.Text(fs, "Armour", lx + 2, sy + 12, CDim, 0); G.TextRight(fs, p.ArmorRating + "  (" + (int)(p.ArmorReduction * 100) + "%)", lx + 148, sy + 12, CWhite, 0);
            G.Text(fs, "Health", lx + 2, sy + 24, CDim, 0); G.TextRight(fs, ((int)p.HP) + " / " + (int)p.MaxHP, lx + 148, sy + 24, CWhite, 0);
            G.Text(fs, "Gold", lx + 2, sy + 36, CDim, 0); G.TextRight(fs, p.Gold.ToString(), lx + 148, sy + 36, 0xF0C060, 0);
            // draughts
            Item hp = new Item(); hp.Kind = ItemKind.Potion; hp.Base = 0; hp.Count = p.HealthPotions; hp.Name = "Health Draught";
            Item sp = new Item(); sp.Kind = ItemKind.Potion; sp.Base = 1; sp.Count = p.SpiritPotions; sp.Name = "Spirit Draught";
            int ps = sy + 52;
            if (Slot("potion0", lx + 52, ps, p.HealthPotions > 0 ? hp : null, false) == 1) UsePotion(0);
            if (Slot("potion1", lx + 78, ps, p.SpiritPotions > 0 ? sp : null, false) == 1) UsePotion(1);
            if (Hot(lx + 52, ps, 20, 20)) Tip(Loc.F("Health Draught  ({0})", p.HealthPotions), "Restores 45% of your health. Press ^y1^0 in the field, or click here.");
            if (Hot(lx + 78, ps, 20, 20)) Tip(Loc.F("Spirit Draught  ({0})", p.SpiritPotions), Loc.F("Restores your {0}. Press ^y2^0 in the field, or click here.", Loc.Tr(p.Def.ResName)));

            // pack
            int rx = WX + 184;
            int used = 0; foreach (Item b in p.Bag) if (b != null) used++;
            Header(rx, ty, 250, Loc.F("Pack  {0} / {1}", used, p.Bag.Length), Fonts.BodyBold);
            int gx = rx + 10, gy = ty + 22;
            for (int i = 0; i < p.Bag.Length; i++)
            {
                int cx = gx + (i % 10) * 23, cy = gy + (i / 10) * 23;
                Item it = p.Bag[i];
                bool better = false;
                if (it != null && it.IsEquip && p.CanUse(it))
                {
                    Item eq = p.Equip[Player.SlotFor(it)];
                    better = eq == null || it.Score > eq.Score * 1.02f;
                }
                int act = Slot("bag" + i, cx, cy, it, false);
                if (it != null && better) { G.Fill(cx + 14, cy + 2, 4, 1, 0x8FCB6B); G.Fill(cx + 15, cy + 1, 2, 3, 0x8FCB6B); }
                if (it != null && !p.CanUse(it)) G.FillA(cx + 1, cy + 1, 18, 18, 0x6E1730, 110);
                if (it != null && Hot(cx, cy, 20, 20))
                {
                    Item eq = it.IsEquip ? p.Equip[Player.SlotFor(it)] : null;
                    List<string> t = Items.Describe(it, eq, true, p.CanUse(it) ? "Left-click: equip    Right-click: drop" : "Right-click: drop");
                    if (!p.CanUse(it)) t.Insert(2, "^rYour class cannot use this.^0");
                    Tip(t, 210);
                }
                if (act == 1 && it != null) EquipFromBag(i);
                else if (act == 2 && it != null) DropFromBag(i);
            }
            // keepsakes
            int ky = gy + 3 * 23 + 10;
            Header(rx, ky, 250, "Keepsakes", Fonts.BodyBold);
            ky += 20;
            if (p.Keepsakes.Count == 0) G.Text(fs, "Nothing yet.", rx + 12, ky + 4, CDim, 0);
            for (int i = 0; i < p.Keepsakes.Count; i++)
            {
                Item it = p.Keepsakes[i];
                int cx = gx + i * 23;
                Slot("keep" + i, cx, ky, it, false);
                if (Hot(cx, ky, 20, 20)) Tip(Items.Describe(it, null, false, null), 200);
            }
            G.Text(fs, "Left-click to equip or unequip.  Right-click to drop.", rx + 10, WY + WH - 34, CDim, 0);
            G.Text(fs, "A green mark means stronger than what you wear.", rx + 10, WY + WH - 22, CDim, 0);
        }

        // character sheet
        void StatLine(int x, ref int y, string label, string value, string tipTitle, string tipBody)
        {
            PixFont f = Fonts.Small;
            G.Text(f, label, x, y, CDim, 0);
            G.TextRight(f, value, x + 186, y, CWhite, 0);
            if (tipBody != null && Hot(x, y, 186, 12)) Tip(tipTitle, tipBody);
            y += 13;
        }

        void CharacterUi()
        {
            Player p = P;
            PixFont fs = Fonts.Small;
            CultureInfo ci = CultureInfo.InvariantCulture;
            int lx = WX + 16, y = WY + 34;
            G.Text(Fonts.Title, p.Def.Name, lx, y, p.Def.Accent, 0x000000);
            G.Text(fs, p.Def.Title, lx, y + 20, CDim, 0);
            if (Hot(lx, y, 120, 32)) Tip(p.Def.Title, p.Def.Lore);
            G.TextRight(Fonts.BodyBold, Loc.F("Level {0}", p.Level), lx + 196, y + 4, CGoldHi, 0);
            int by = y + 36;
            float xf = p.Level >= Player.MaxLevel ? 1 : p.XP / (float)p.XpToNext;
            Bar(lx, by, 196, 5, xf, 0x7FE0D8, 0x26505F, 0x16172A);
            G.Text(fs, p.Level >= Player.MaxLevel ? "Maximum level" : Loc.F("{0} / {1} experience", p.XP, p.XpToNext), lx, by + 8, CDim, 0);

            int ay = by + 28;
            Header(lx, ay, 196, "Attributes", Fonts.BodyBold);
            ay += 20;
            string[] names = { "Might", "Finesse", "Arcana", "Vigor" };
            string[] tips =
            {
                "Raw strength. +2% damage per point for Warriors, a little for everyone else, and half a point of Armour.",
                "Speed and precision. +2% damage per point for Rogues and Rangers. +0.5% critical chance and slightly faster attacks for all.",
                "Command of the unseen. +2% damage per point for Mages, more Mana, and shorter ability cooldowns for all.",
                "Hardiness. +8 Health per point and faster natural recovery.",
            };
            int[] baseV = { p.Might, p.Finesse, p.Arcana, p.Vigor }, tot = { p.TMight, p.TFinesse, p.TArcana, p.TVigor };
            for (int i = 0; i < 4; i++)
            {
                int ry = ay + i * 22;
                bool primary = (i == 0 && p.Def.Primary == 0) || (i == 1 && p.Def.Primary == 1) || (i == 2 && p.Def.Primary == 2);
                G.Fill(lx, ry, 196, 19, primary ? 0x241E36 : 0x1A1626); G.Frame(lx, ry, 196, 19, CLine);
                G.Text(Fonts.Body, names[i], lx + 6, ry + 3, primary ? CGoldHi : CText, 0);
                if (primary) G.Text(fs, "primary", lx + 62, ry + 4, CDim, 0);
                string v = tot[i].ToString();
                G.TextRight(Fonts.BodyBold, v, lx + 150, ry + 3, CWhite, 0);
                if (tot[i] > baseV[i]) G.Text(fs, "+" + (tot[i] - baseV[i]), lx + 153, ry + 4, 0x8FCB6B, 0);
                if (Hot(lx, ry, 172, 19)) Tip(names[i], tips[i]);
                if (p.AttrPoints > 0 && ButtonId("attr" + i, lx + 176, ry + 2, 16, 15, "+", true))
                {
                    p.AttrPoints--;
                    if (i == 0) p.Might++; else if (i == 1) p.Finesse++; else if (i == 2) p.Arcana++; else p.Vigor++;
                    p.Recalc(false);
                }
            }
            int py = ay + 4 * 22 + 6;
            if (p.AttrPoints > 0) G.Text(Fonts.BodyBold, Loc.F("Points to spend: {0}", p.AttrPoints), lx, py, CGoldHi, 0x000000);
            else G.Text(fs, "Each level grants 3 points to spend here.", lx, py + 1, CDim, 0);

            int rx = WX + 238, ry2 = WY + 34;
            Header(rx, ry2, 196, "Combat", Fonts.BodyBold);
            ry2 += 20;
            WeaponType wt = p.WType;
            float avg = (p.WMin + p.WMax) * 0.5f * p.DmgMult;
            StatLine(rx + 4, ref ry2, "Health", ((int)p.HP) + " / " + (int)p.MaxHP, "Health", "When it reaches zero you fall, and wake at the last wayshrine you rested at.");
            StatLine(rx + 4, ref ry2, p.Def.ResName, ((int)p.Res) + " / " + (int)p.MaxRes, p.Def.ResName, p.Def.Passive);
            StatLine(rx + 4, ref ry2, "Damage per hit", (int)(p.WMin * p.DmgMult) + " - " + (int)(p.WMax * p.DmgMult), "Damage", Loc.F("Weapon damage multiplied by your attributes (x{0}). Abilities scale from this.", p.DmgMult.ToString("0.00", ci)));
            StatLine(rx + 4, ref ry2, "Attacks per second", (1f / p.AtkInterval).ToString("0.00", ci), "Attack speed", "Set by your weapon type and quickened by Finesse.");
            StatLine(rx + 4, ref ry2, "Damage per second", ((int)(avg / p.AtkInterval * (1 + p.CritChance * (p.CritMult - 1)))).ToString(), "Sustained damage", "Basic attacks only, including critical hits.");
            StatLine(rx + 4, ref ry2, "Critical chance", (p.CritChance * 100).ToString("0.#", ci) + "%", "Critical chance", Loc.F("Chance for any hit to deal x{0} damage.", p.CritMult.ToString("0.0", ci)));
            StatLine(rx + 4, ref ry2, "Armour", p.ArmorRating + "  (-" + (int)(p.ArmorReduction * 100) + "%)", "Armour", "Reduces every blow you take by the percentage shown.");
            StatLine(rx + 4, ref ry2, "Movement", ((int)p.MoveSpeed).ToString(), "Movement speed", "Paces per second, more or less.");
            StatLine(rx + 4, ref ry2, "Life on hit", (p.Leech * 100).ToString("0.#", ci) + "%", "Life on hit", "Part of the damage you deal returns to you as health.");
            StatLine(rx + 4, ref ry2, "Cooldown rate", "-" + ((1f - p.CdMult) * 100).ToString("0", ci) + "%", "Cooldowns", "Arcana shortens the wait between abilities.");
            ry2 += 4;
            Header(rx, ry2, 196, "Chronicle", Fonts.BodyBold);
            ry2 += 20;
            int mins = (int)(PlayTime / 60);
            string none = null;
            StatLine(rx + 4, ref ry2, "Foes slain", p.Kills.ToString(), none, none);
            StatLine(rx + 4, ref ry2, "Times fallen", p.Deaths.ToString(), none, none);
            StatLine(rx + 4, ref ry2, "Time in the vale", Loc.F("{0}h {1}m", mins / 60, (mins % 60).ToString("00")), none, none);
        }

        // skills
        void SkillsUi()
        {
            Player p = P;
            PixFont fs = Fonts.Small;
            int x = WX + 16, y = WY + 34;
            for (int i = 0; i < 5; i++)
            {
                AbilityDef ab = p.Def.Abilities[i];
                bool locked = p.Level < ab.UnlockLevel;
                int ry = y + i * 39;
                G.Fill(x, ry, WW - 32, 38, 0x1A1626); G.Frame(x, ry, WW - 32, 38, locked ? CLine : CBronze);
                G.Fill(x + 5, ry + 7, 24, 24, CInk); G.Fill(x + 6, ry + 8, 22, 22, CSlot);
                G.Blit(Assets.Skill[ab.Icon], x + 8, ry + 10);
                if (locked) G.FillA(x + 6, ry + 8, 22, 22, CInk, 190);
                G.Frame(x + 5, ry + 7, 24, 24, locked ? CLine : CGold);
                G.Text(Fonts.BodyBold, ab.Name, x + 36, ry + 3, locked ? CDim : CGoldHi, 0x000000);
                int nw = Fonts.BodyBold.Width(ab.Name);
                string key = "[" + Loc.Tr(ab.Key) + "]";
                G.Text(fs, key, x + 42 + nw, ry + 4, CDim, 0);
                string meta = "";
                if (ab.Cost > 0) meta += ab.Cost + " " + Loc.Tr(p.Def.ResName);
                if (ab.Cooldown > 0) meta += (meta.Length > 0 ? "  -  " : "") + Loc.F("{0}s", (ab.Cooldown * p.CdMult).ToString("0.#", CultureInfo.InvariantCulture));
                if (locked) meta = Loc.F("Unlocks at level {0}", ab.UnlockLevel);
                G.TextRight(fs, meta, x + WW - 40, ry + 4, locked ? 0xE0584E : 0x7FE0D8, 0);
                List<string> lines = fs.Wrap(ab.Desc, WW - 80);
                for (int k = 0; k < lines.Count && k < 2; k++) G.Text(fs, lines[k], x + 36, ry + 16 + k * 10, locked ? 0x6A6A7A : CText, 0);
            }
            int py = y + 5 * 39 + 1;
            G.Fill(x, py, WW - 32, 34, 0x1A1626); G.Frame(x, py, WW - 32, 34, CLine);
            G.Blit(Assets.Skill[ItemArt.SkDodge], x + 8, py + 8);
            G.Text(Fonts.BodyBold, "Dodge Roll", x + 36, py + 2, CGoldHi, 0x000000);
            G.Text(fs, "[Space]", x + 42 + Fonts.BodyBold.Width("Dodge Roll"), py + 3, CDim, 0);
            G.TextRight(fs, Loc.F(p.Def.DodgeCharges > 1 ? "{0} charges" : "{0} charge", p.Def.DodgeCharges), x + WW - 40, py + 3, 0x7FE0D8, 0);
            List<string> pas = fs.Wrap(Loc.F("Passive - {0}", Loc.Tr(p.Def.Passive)), WW - 80);
            for (int k = 0; k < pas.Count && k < 2; k++) G.Text(fs, pas[k], x + 36, py + 14 + k * 10, CText, 0);
        }

        // journal
        void JournalUi()
        {
            PixFont fs = Fonts.Small;
            int lx = WX + 14, y = WY + 34;
            Header(lx, y, 150, "Quests", Fonts.BodyBold);
            y += 20;
            List<Quest> shown = new List<Quest>();
            foreach (Quest q in Q.All) if (q.Active) shown.Add(q);
            foreach (Quest q in Q.All) if (q.Done) shown.Add(q);
            if (selQuest == null || Q.Get(selQuest) == null || Q.Get(selQuest).Stage < 0) selQuest = Q.Tracked;
            for (int i = 0; i < shown.Count; i++)
            {
                Quest q = shown[i];
                int ry = y + i * 18;
                bool sel = q.Id == selQuest, hot = Hot(lx, ry, 150, 16);
                Remember("quest:" + q.Id, lx, ry, 150, 16);
                if (hot) hotNow = "quest" + q.Id;
                if (sel || hot) { G.Fill(lx, ry, 150, 16, sel ? 0x2E2843 : 0x241E36); G.Frame(lx, ry, 150, 16, sel ? CGold : CLine); }
                if (q.Done) { G.Fill(lx + 4, ry + 8, 2, 2, 0x8FCB6B); G.Fill(lx + 6, ry + 10, 2, 2, 0x8FCB6B); G.Fill(lx + 8, ry + 6, 2, 4, 0x8FCB6B); }
                else if (Q.Tracked == q.Id) for (int k = 0; k < 3; k++) { G.Fill(lx + 6 - k, ry + 5 + k, k * 2 + 1, 1, CGoldHi); G.Fill(lx + 6 - k, ry + 10 - k, k * 2 + 1, 1, CGoldHi); }
                else G.Fill(lx + 5, ry + 7, 3, 3, q.Main ? CGold : CDim);
                G.Text(fs, q.Title, lx + 14, ry + 2, q.Done ? CDim : (q.Main ? CGoldHi : CWhite), 0);
                if (Clicked(lx, ry, 150, 16, 0)) { selQuest = q.Id; Snd(Sfx.UiTab, 0.5f, 1.1f); }
            }
            if (shown.Count == 0) G.Text(fs, "No quests yet.", lx + 4, y + 2, CDim, 0);

            Quest s = Q.Get(selQuest);
            int rx = WX + 176, ry2 = WY + 34, rw = WW - 190;
            G.VLine(rx - 8, WY + 32, WH - 44, CLine);
            if (s == null || s.Stage < 0) return;
            G.Text(Fonts.Title, s.Title, rx, ry2, s.Done ? CDim : CGoldHi, 0x000000);
            ry2 += 21;
            G.Text(fs, Loc.Tr(s.Main ? "Main quest" : "Side quest") + "  -  " + Loc.Tr(s.Giver), rx, ry2, CDim, 0);
            ry2 += 15;
            ry2 = G.TextWrapped(fs, s.Summary, rx, ry2, rw, CText, 0x000000, 1) + 6;
            G.Text(Fonts.BodyBold, s.Done ? "Completed" : "Objectives", rx, ry2, s.Done ? 0x8FCB6B : CGold, 0x000000);
            ry2 += 15;
            int last = s.Done ? s.Objectives.Length - 1 : s.Stage;
            int first = Math.Max(0, last - 3);
            for (int k = first; k <= last && k < s.Objectives.Length; k++)
            {
                bool cur = k == s.Stage && !s.Done;
                string text = cur ? s.Objective : Loc.Tr(s.Objectives[k]).Replace(" ({0}/{1})", "");
                if (cur) { for (int j = 0; j < 3; j++) { G.Fill(rx + 3 - j, ry2 + 2 + j, j * 2 + 1, 1, CGoldHi); G.Fill(rx + 3 - j, ry2 + 7 - j, j * 2 + 1, 1, CGoldHi); } }
                else { G.Fill(rx + 1, ry2 + 5, 2, 2, 0x5A8C58); G.Fill(rx + 3, ry2 + 7, 2, 2, 0x5A8C58); G.Fill(rx + 5, ry2 + 3, 2, 4, 0x5A8C58); }
                ry2 = G.TextWrapped(fs, text, rx + 12, ry2, rw - 14, cur ? CWhite : CDim, 0x000000, 0) + 3;
            }
            ry2 += 4;
            G.Text(fs, Loc.F("^dReward:^0 {0}", Loc.Tr(s.Reward)), rx, ry2, CText, 0);
            if (s.Active && Q.Tracked != s.Id && Button(rx, WY + WH - 30, 110, 18, "Track this quest", true)) Q.Tracked = s.Id;
            else if (s.Active && Q.Tracked == s.Id) G.Text(fs, "Tracked on your compass.", rx, WY + WH - 26, CDim, 0);
        }

        // map
        void MapUi()
        {
            Map m = Cur; Player p = P;
            int ax = WX + 12, ay = WY + 32, aw = WW - 24, ah = WH - 58;
            int scale = m.Dungeon ? 3 : 2;
            int tw = aw / scale, th = ah / scale;
            int ptx = (int)(p.X / 16), pty = (int)(p.Y / 16);
            int ox = M.Clamp(ptx - tw / 2, 0, Math.Max(0, m.TW - tw)), oy = M.Clamp(pty - th / 2, 0, Math.Max(0, m.TH - th));
            int offx = m.TW < tw ? (tw - m.TW) * scale / 2 : 0, offy = m.TH < th ? (th - m.TH) * scale / 2 : 0;
            G.Fill(ax - 1, ay - 1, aw + 2, ah + 2, CInk);
            G.Fill(ax, ay, aw, ah, 0x08080C);
            G.SetClip(ax, ay, aw, ah);
            int vw = Math.Min(tw, m.TW - ox), vh = Math.Min(th, m.TH - oy);
            if (vw > 0 && vh > 0)
            {
                // what has been seen, as one picture enlarged smoothly (unexplored ground fades into the dark)
                if (mapBuf == null || mapBuf.Length < vw * vh) mapBuf = new int[vw * vh];
                for (int j = 0; j < vh; j++)
                    for (int i = 0; i < vw; i++)
                    {
                        int t = (oy + j) * m.TW + ox + i;
                        mapBuf[j * vw + i] = m.Seen[t] ? (m.Mini[t] & 0xFFFFFF) | unchecked((int)0xFF000000) : 0x08080C;
                    }
                if (G.S > 1)
                {
                    if (mapBig == null || mapBig.Length < vw * vh * 4) mapBig = new int[vw * vh * 4];
                    HiArt.Epx2(mapBuf, vw, vh, mapBig);
                    HiArt.SoftenAlpha(mapBig, vw * 2, vh * 2);
                    G.ImageScaled(mapBig, vw * 2, vh * 2, ax + offx, ay + offy, vw * scale, vh * scale, true);
                }
                else G.ImageScaled(mapBuf, vw, vh, ax + offx, ay + offy, vw * scale, vh * scale, true);
            }
            float half = scale * 0.5f;
            // markers
            foreach (Prop q in m.Props)
            {
                int tx = (int)(q.X / 16), ty = (int)(q.Y / 16);
                if (tx < 0 || ty < 0 || tx >= m.TW || ty >= m.TH || !m.Seen[ty * m.TW + tx]) continue;
                float x = ax + offx + (tx - ox) * scale + half, y = ay + offy + (ty - oy) * scale + half;
                if (q.Kind == PropKind.Shrine) MapDot(x, y, 2.2f, Flags.Contains(q.Id) ? 0x7FE0D8 : 0x4A6A70);
                else if (q.Kind == PropKind.RuneStone) { G.Ellipse(x, y, 1.9f, 3f, CInk, 255); G.Ellipse(x, y, 1.0f, 2.1f, Flags.Contains(q.Id) ? 0x9BDCF4 : 0x8C9BB2, 255); }
                else if (q.Kind == PropKind.Chest && q.Interactive) MapDot(x, y, 1.2f, 0xF0C060);
            }
            foreach (Npc n in Npcs)
            {
                int tx = (int)(n.X / 16), ty = (int)(n.Y / 16);
                if (tx < 0 || ty < 0 || tx >= m.TW || ty >= m.TH || !m.Seen[ty * m.TW + tx]) continue;
                MapDot(ax + offx + (tx - ox) * scale + half, ay + offy + (ty - oy) * scale + half, 1.5f, 0xF0C060);
            }
            Quest tq = Q.Get(Q.Tracked);
            if (tq == null || !tq.Active) tq = Q.Get("bell");
            float qx, qy;
            if (Q.Target(this, tq, out qx, out qy))
            {
                float x = ax + offx + ((int)(qx / 16) - ox) * scale + half, y = ay + offy + ((int)(qy / 16) - oy) * scale + half;
                x = M.Clamp(x, ax + 4, ax + aw - 5); y = M.Clamp(y, ay + 4, ay + ah - 5);
                float pulse = 0.5f + 0.5f * M.Sin(UiT * 5f);
                G.Ellipse(x, y, 4.5f + pulse * 1.5f, 4.5f + pulse * 1.5f, CGoldHi, (int)(70 + 60 * (1 - pulse)));
                MapDot(x, y, 2.6f, CGoldHi);
                G.Ellipse(x, y, 0.9f, 0.9f, CInk, 255);
            }
            {
                float x = ax + offx + (ptx - ox) * scale + half, y = ay + offy + (pty - oy) * scale + half;
                float a = p.AimAngle;
                G.Line((int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(x + M.Cos(a) * 5), (int)Math.Round(y + M.Sin(a) * 5), CInk, 255);
                MapDot(x, y, 2.4f, 0xFFFFFF);
                G.Ellipse(x + M.Cos(a) * 3.6f, y + M.Sin(a) * 3.6f, 1.1f, 1.1f, 0xFFFFFF, 255);
            }
            foreach (Player o in Others)
                MapDot(ax + offx + ((int)(o.X / 16) - ox) * scale + half, ay + offy + ((int)(o.Y / 16) - oy) * scale + half, 2.2f, 0x7FE0D8);
            G.ResetClip();
            G.Frame(ax - 1, ay - 1, aw + 2, ah + 2, CBronze);
            PixFont fs = Fonts.Small;
            int ly = WY + WH - 21;
            G.Text(Fonts.BodyBold, m.Dungeon ? "The Sunken Abbey" : "The Vale of Vesperfell", ax, ly - 2, CGoldHi, 0x000000);
            int lx = ax + 180;
            MapDot(lx + 1.5f, ly + 5.5f, 1.8f, 0xFFFFFF); lx = G.Text(fs, "You", lx + 7, ly, CText, 0) + 12;
            MapDot(lx + 1.5f, ly + 5.5f, 1.8f, 0x7FE0D8); lx = G.Text(fs, "Wayshrine", lx + 7, ly, CText, 0) + 12;
            MapDot(lx + 1.5f, ly + 5.5f, 1.8f, 0xF0C060); lx = G.Text(fs, "Folk", lx + 7, ly, CText, 0) + 12;
            MapDot(lx + 1.5f, ly + 5.5f, 1.8f, CGoldHi); G.Text(fs, "Objective", lx + 7, ly, CText, 0);
        }

        int[] mapBuf, mapBig;

        /// <summary>A round map marker with a dark rim.</summary>
        void MapDot(float x, float y, float r, int col)
        {
            G.Ellipse(x, y, r + 1f, r + 1f, CInk, 255);
            G.Ellipse(x, y, r, r, col, 255);
        }
    }
}
