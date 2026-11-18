using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>
    /// Interactive props, world state flags and scripted story beats.
    /// World changes are written so they can run on every machine in a co-op session: the host decides,
    /// then each player's game applies the same change and hands out that player's own rewards.
    /// </summary>
    public sealed partial class Game
    {
        public Prop NearProp; public Npc NearNpc;
        readonly List<Prop> shaking = new List<Prop>();
        public float EndingPending = -1;
        int hintStep;

        void UpdateProps(float dt)
        {
            for (int i = shaking.Count - 1; i >= 0; i--)
            {
                shaking[i].ShakeT -= dt;
                if (shaking[i].ShakeT <= 0) { shaking[i].ShakeT = 0; shaking.RemoveAt(i); }
            }
        }

        void ShakeProp(Prop p) { if (p.ShakeT <= 0) shaking.Add(p); p.ShakeT = 0.25f; }

        void UpdateInteraction()
        {
            NearProp = null; NearNpc = null;
            Player p = P;
            if (p.Dead) return;
            float best = 1e9f;
            for (int i = 0; i < Npcs.Count; i++)
            {
                float d = M.Dist(Npcs[i].X, Npcs[i].Y, p.X, p.Y);
                if (d < 28 && d < best) { best = d; NearNpc = Npcs[i]; }
            }
            if (NearNpc == null)
            {
                List<Prop> near = scratchProps; near.Clear();
                Cur.Query(p.X - 70, p.Y - 70, p.X + 70, p.Y + 70, near);
                for (int i = 0; i < near.Count; i++)
                {
                    Prop q = near[i];
                    if (!q.Interactive) continue;
                    float d = M.Dist(q.X, q.Y - 3, p.X, p.Y);
                    if (d < q.InteractR && d < best) { best = d; NearProp = q; }
                }
            }
            if (Ov != Overlay.None || chatOpen) return;
            if (In.Pressed[Key.F] && inputLockT <= 0 && !Fading && p.DashT <= 0)
            {
                if (NearNpc != null) StartDialogue(NearNpc);
                else if (NearProp != null) Interact(NearProp);
            }
        }

        public bool AllRunes { get { return Flags.Contains("rune0") && Flags.Contains("rune1") && Flags.Contains("rune2"); } }
        public int RuneCount { get { return (Flags.Contains("rune0") ? 1 : 0) + (Flags.Contains("rune1") ? 1 : 0) + (Flags.Contains("rune2") ? 1 : 0); } }

        void LightProp(Prop p, Sprite[] anim, float radius, int color, float intensity, float oy)
        {
            p.Anim = anim; p.Spr = anim[0];
            p.LightR = radius; p.LightColor = color; p.LightI = intensity; p.LightOY = oy; p.Flicker = true;
        }

        // using things
        /// <summary>The local player pressed F on a prop.</summary>
        public void Interact(Prop q)
        {
            Player p = Me;
            switch (q.Kind)
            {
                case PropKind.Sign:
                    {
                        string t = q.Text ?? "";
                        int nl = t.IndexOf('\n');
                        ShowText(nl > 0 ? t.Substring(0, nl) : "Sign", nl > 0 ? t.Substring(nl + 1) : t);
                        Snd(Sfx.Page);
                        if (!Flags.Contains("read_" + q.Id)) { Flags.Add("read_" + q.Id); GainXp(8, false); }
                        if (t.Contains("WAKE US THREE") && Q.Stage("stones") < 0) Q.Start("stones", this);
                    }
                    return;
                case PropKind.Well:
                    if (p.Gold > 0 && !Flags.Contains("wish_" + q.X))
                    {
                        p.Gold--; Flags.Add("wish_" + q.X);
                        Snd(Sfx.Coin, 0.8f, 0.7f); Snd(Sfx.Splash, 0.5f, 1.2f);
                        ShowText("The well", "You let a coin fall. It is a long time before the splash - and when it comes, it sounds almost like a small bell.\n\n^gYou feel a little braver.^0");
                        GainXp(25, false);
                    }
                    else ShowText("The well", "Dark water, a long way down.");
                    return;
            }
            if (!CanUse(q)) return;
            if (IsClient)
            {
                if (q.Kind == PropKind.Shrine) ShrineRest(q);
                NetUseProp(q);
                return;
            }
            UseProp(q, MyNetId);
        }

        /// <summary>Checks a prop's preconditions and tells the player why when they are not met.</summary>
        bool CanUse(Prop q)
        {
            Player p = Me;
            switch (q.Kind)
            {
                case PropKind.RuneStone:
                    if (Flags.Contains(q.Id)) { ShowText("A waking stone", "The carved sign glows steadily. It is warm, like something breathing in its sleep."); return false; }
                    return true;
                case PropKind.Cache:
                    if (Flags.Contains("open_cache")) return false;
                    if (!AllRunes)
                    {
                        ShowText("The king's door", Loc.F("A slab of grey stone seals the mound. Three hollow signs are cut into the lintel - a ring, a crossing, and an arrow.\n\n^dSigns faintly lit: {0} of 3.^0", RuneCount));
                        if (Q.Stage("stones") < 0) Q.Start("stones", this);
                        return false;
                    }
                    return true;
                case PropKind.AbbeyDoor:
                    if (!Flags.Contains("abbey_open") && p.KeepsakeCount("sigil") <= 0)
                    {
                        ShowText("The abbey doors", "Grey stone has grown across the doorway like frost on a window. At its centre is a round hollow, the size of a man's palm, carved with the sign of the bell.\n\n^dSomething belongs here.^0");
                        Snd(Sfx.UiError, 0.5f, 0.7f);
                        return false;
                    }
                    if (IsClient) { Notify("^dOnly the host can lead the party through.^0"); return false; }
                    return true;
                case PropKind.Stairs:
                    if (IsClient) { Notify("^dOnly the host can lead the party through.^0"); return false; }
                    return true;
                case PropKind.Bell:
                    if (Flags.Contains("bell_rung")) return false;
                    if (!(Flags.Contains("dead_prior") && p.KeepsakeCount("tongue") > 0))
                    {
                        ShowText("The Vesper Bell", "It lies where it fell, taller than a man and green with age. A crack runs down its flank.\n\nInside, where the clapper should hang, there is only an iron hook.");
                        return false;
                    }
                    return true;
                case PropKind.Chest: return !Flags.Contains("open_" + q.Id);
                case PropKind.Moonpetal: return !Flags.Contains("got_" + q.Id);
                case PropKind.Brazier: return !Flags.Contains("lit_" + q.Id);
                case PropKind.Keepsake: return !Flags.Contains("got_" + q.Id);
            }
            return true;
        }

        /// <summary>
        /// Applies the use of a prop to the world and gives the local player their share.
        /// Runs on every machine; 'by' is the network id of the player who used it.
        /// </summary>
        public void UseProp(Prop q, int by)
        {
            Player p = Me;
            bool mine = by == MyNetId;
            switch (q.Kind)
            {
                case PropKind.Chest:
                    if (Flags.Contains("open_" + q.Id)) return;
                    OpenChest(q);
                    break;
                case PropKind.Shrine:
                    {
                        bool first = !Flags.Contains(q.Id);
                        Flags.Add(q.Id);
                        LightProp(q, Assets.ShrineLit, 86, WorldGen.EmberLight, 0.95f, -22);
                        if (first) { GainXp(25, true); Notify(Loc.F("^yWayshrine attuned:^0 {0}", Loc.Tr(q.Text ?? ""))); }
                        if (mine && !IsClient)
                        {
                            ShrineRest(q);
                            if (!first) Notify("^yYou rest.^0 The Gloam stirs - foes return.");
                            if (!BossFight) RespawnFoes();
                            SaveGame();
                        }
                    }
                    break;
                case PropKind.Moonpetal:
                    if (Flags.Contains("got_" + q.Id)) return;
                    Flags.Add("got_" + q.Id);
                    q.Spr = Assets.MoonpetalPicked; q.Interactive = false; q.LightR = 0;
                    p.AddKeepsake("moonpetal", 1);
                    Motes(q.X, q.Y, 8, 0xEAF3FF, 0x7F6CB6, 10, 5, 40, 0.7f);
                    SndAt(Sfx.PickItem, q.X, q.Y, 0.9f);
                    Notify(Loc.F("^bMoonpetal^0  ^d({0})^0", p.KeepsakeCount("moonpetal")));
                    Q.OnItem("moonpetal", this);
                    break;
                case PropKind.RuneStone:
                    if (Flags.Contains(q.Id)) return;
                    Flags.Add(q.Id);
                    q.Spr = Assets.StoneLit[q.Param]; q.Prompt = "The stone hums";
                    q.LightR = 70; q.LightColor = 0x80D0FF; q.LightI = 0.9f; q.LightOY = -18; q.Flicker = false;
                    Motes(q.X, q.Y, 14, 0x9BDCF4, 0x2F6A9A, 24, 8, 50, 1f);
                    RingBurst(q.X, q.Y, 0x9BDCF4, 20, 120, 0.5f);
                    SndAt(Sfx.Rune, q.X, q.Y, 1f); Shake(2f, 0.2f);
                    GainXp(35, true);
                    Q.OnRune(this);
                    if (AllRunes)
                    {
                        Notify("^cFar to the north-east, stone grinds against stone.^0");
                        Prop c = Over.FindProp("cache");
                        if (c != null && !Flags.Contains("open_cache")) { c.Spr = Assets.BarrowRune; c.Prompt = "Open the king's door"; c.LightR = 60; c.LightColor = 0x80D0FF; c.LightI = 0.7f; c.LightOY = -14; }
                    }
                    else Notify(Loc.F("^cA rune wakes.^0  {0} of 3", RuneCount));
                    break;
                case PropKind.Cache:
                    {
                        if (Flags.Contains("open_cache")) return;
                        Flags.Add("open_cache");
                        q.Spr = Assets.BarrowOpen; q.Interactive = false; q.LightR = 0;
                        SndAt(Sfx.Gate, q.X, q.Y, 1f); Snd(Sfx.ChestOpen); Shake(3f, 0.4f);
                        Dust(q.X, q.Y, 12, 0x8A8A9A);
                        float lx = q.X, ly = q.Y + 10;
                        SpawnPickup(Pickup.Equip, lx, ly, 1, Items.Unique("cache", p.Class, Math.Max(5, p.Level)));
                        SpawnPickup(Pickup.Equip, lx, ly, 1, Items.RollEquip(Rnd, Math.Max(5, p.Level), p.Class, 2));
                        DropGold(lx, ly, 120);
                        SpawnPickup(Pickup.Health, lx, ly, 2, null);
                        GainXp(150, true);
                        Q.OnCache(this);
                    }
                    break;
                case PropKind.Brazier:
                    if (Flags.Contains("lit_" + q.Id)) return;
                    Flags.Add("lit_" + q.Id);
                    LightProp(q, Assets.BrazierEmber, 110, WorldGen.EmberLight, 1.1f, -16);
                    q.Interactive = false;
                    SndAt(Sfx.Kindle, q.X, q.Y, 1f); Sparks(q.X, q.Y, 18, 0xF7BE52, 14, 80, 0.6f);
                    GainXp(30, true);
                    Q.OnBrazier(this);
                    if (Flags.Contains("lit_brazier_w") && Flags.Contains("lit_brazier_e")) OpenBellgate(true);
                    else Notify("^oA vigil-flame burns.^0  One more remains dark.");
                    break;
                case PropKind.Stairs:
                    if (IsClient) break;
                    FadeThrough(delegate
                    {
                        Marker mk = Over.Find("abbey_door");
                        EnterMap(Over, mk.X, mk.Y + 14);
                        SaveGame();
                    });
                    Snd(Sfx.Gate, 0.5f, 1.2f);
                    break;
                case PropKind.AbbeyDoor:
                    if (!Flags.Contains("abbey_open"))
                    {
                        Flags.Add("abbey_open");
                        Notify("^pThe Abbey Sigil sinks into the stone. The doors breathe out.^0");
                        Snd(Sfx.Gate); Shake(3f, 0.4f);
                    }
                    if (IsClient) break;
                    FadeThrough(delegate
                    {
                        Marker mk = Abbey.Find("abbey_entry");
                        EnterMap(Abbey, mk.X, mk.Y);
                        Q.OnEnterAbbey(this);
                        SaveGame();
                    });
                    break;
                case PropKind.Keepsake:
                    if (Flags.Contains("got_" + q.Id)) return;
                    Flags.Add("got_" + q.Id);
                    if (p.KeepsakeCount(q.Text) == 0) p.AddKeepsake(q.Text, 1);
                    Cur.Remove(q);
                    Snd(Sfx.PickItem, 1f, 1.2f);
                    Notify("^b" + Loc.Tr(Items.Quest(q.Text).Name) + "^0");
                    Q.OnItem(q.Text, this);
                    break;
                case PropKind.Bell:
                    if (Flags.Contains("bell_rung")) return;
                    RingBell(q);
                    break;
            }
            if (IsHost) NetPropUsed(q, by);
        }

        /// <summary>The personal part of resting: mend, refill, and remember this shrine.</summary>
        void ShrineRest(Prop q)
        {
            Player p = Me;
            p.RespawnMap = Cur.Id; p.RespawnX = q.X; p.RespawnY = q.Y + 4;
            p.HP = p.MaxHP; if (p.Class != ClassId.Warrior) p.Res = p.MaxRes;
            p.ClearStatus();
            if (p.HealthPotions < 3) p.HealthPotions = 3;
            if (p.SpiritPotions < 1) p.SpiritPotions = 1;
            Snd(Sfx.Shrine);
            Motes(p.X, p.Y, 0, 0xFDE999, 0xD2AC52, 24, 12, 50, 1f);
            Flash(0xFFE8B0, 0.3f);
        }

        void OpenChest(Prop q)
        {
            Player p = Me;
            bool ornate = q.Param >= 100; int tier = q.Param % 100;
            Flags.Add("open_" + q.Id);
            q.Spr = ornate ? Assets.ChestOrnateOpen : Assets.ChestWoodOpen; q.Interactive = false; q.State = 1;
            SndAt(Sfx.ChestOpen, q.X, q.Y, 1f);
            Sparks(q.X, q.Y, 10, 0xF7BE52, 12, 70, 0.6f);
            float x = q.X, y = q.Y + 8;
            int lvl = Math.Max(p.Level, Cur == Abbey ? 6 : 1);
            if (tier >= 3)
            {
                SpawnPickup(Pickup.Equip, x, y, 1, Items.Unique("vault", p.Class, Math.Max(7, p.Level)));
                DropGold(x, y, 110); SpawnPickup(Pickup.Health, x, y, 2, null); SpawnPickup(Pickup.Spirit, x, y, 1, null);
                GainXp(80, true);
            }
            else if (tier == 2)
            {
                SpawnPickup(Pickup.Equip, x, y, 1, Items.RollEquip(Rnd, lvl + 1, p.Class, 2));
                SpawnPickup(Pickup.Equip, x, y, 1, Items.RollEquip(Rnd, lvl + 1, p.Class, 1));
                DropGold(x, y, 55 + Rnd.Int(30)); SpawnPickup(Pickup.Health, x, y, 1, null); SpawnPickup(Pickup.Spirit, x, y, 1, null);
                GainXp(50, true);
            }
            else
            {
                SpawnPickup(Pickup.Equip, x, y, 1, Items.RollEquip(Rnd, lvl + Rnd.Int(2), p.Class, 1));
                DropGold(x, y, 18 + Rnd.Int(22) + lvl * 2);
                if (Rnd.Chance(0.7f)) SpawnPickup(Pickup.Health, x, y, 1, null);
                if (Rnd.Chance(0.3f)) SpawnPickup(Pickup.Spirit, x, y, 1, null);
                GainXp(20, true);
            }
        }

        public void RespawnFoes()
        {
            if (IsClient) return;
            ClearFoes(); Shots.Clear(); Boss = null;
            for (int i = Areas.Count - 1; i >= 0; i--) if (!Areas[i].FromPlayer) Areas.RemoveAt(i);
            SpawnAll();
            NetFoesReset();
        }

        void OpenBellgate(bool announce)
        {
            Prop g = Abbey.FindProp("bellgate");
            if (g == null || g.State == 1) return;
            Abbey.Unstamp(g);
            g.ColW = 0; g.ColH = 0; g.Spr = Assets.GateOpen; g.State = 1;
            Flags.Add("bellgate_open");
            if (announce)
            {
                Snd(Sfx.Gate); Shake(3.5f, 0.5f);
                Notify("^oBoth vigil-flames burn. Somewhere in the dark, the Bellgate grinds open.^0");
            }
        }

        public void SetBossGate(bool closed)
        {
            if (Abbey == null) return;
            Prop g = Abbey.FindProp("bossgate");
            if (g == null) return;
            if (closed && g.State == 0)
            {
                g.ColW = 32; g.ColH = 16; g.Spr = Assets.GateClosed; g.State = 1;
                Abbey.Stamp(g, true);
            }
            else if (!closed && g.State == 1)
            {
                Abbey.Unstamp(g);
                g.ColW = 0; g.ColH = 0; g.Spr = Assets.GateOpen; g.State = 0;
            }
            Abbey.FlowValid = false;
        }

        void RingBell(Prop bell)
        {
            Player p = Me;
            Flags.Add("bell_rung");
            p.RemoveKeepsake("tongue");
            bell.Spr = Assets.BellRung; bell.Interactive = false;
            bell.LightR = 150; bell.LightColor = 0xFFD080; bell.LightI = 1.3f; bell.LightOY = -30; bell.Flicker = false;
            Dawn = true; DawnT = 0;
            Snd(Sfx.BellGreat);
            Flash(0xFFF4D0, 1.2f); Shake(5f, 1.2f);
            RingBurst(bell.X, bell.Y, 0xFDE999, 40, 220, 0.9f);
            Motes(bell.X, bell.Y, 20, 0xFFF8DC, 0xEC8A32, 60, 30, 80, 2f);
            if (!IsClient) for (int i = Foes.Count - 1; i >= 0; i--) if (!Foes[i].Dead) KillEnemy(Foes[i], 0, -1);
            SetMusic(Zones.MusDawn);
            Q.OnBell(this);
            GainXp(300, true);
            EndingPending = 3.2f;
            SaveGame();
        }

        // breakables
        /// <summary>Strikes destructible scenery inside a circle. power: 1 light blow, 2 heavy, 3 explosive.</summary>
        public void HitPropsAt(float x, float y, float radius, int power)
        {
            List<Prop> near = scratchProps; near.Clear();
            Cur.Query(x - radius - 24, y - radius - 24, x + radius + 24, y + radius + 40, near);
            for (int i = 0; i < near.Count; i++)
            {
                Prop q = near[i];
                if (q.Kind != PropKind.Breakable && q.Kind != PropKind.Bramble && q.Kind != PropKind.CrackedWall) continue;
                float reach = radius + (q.Kind == PropKind.CrackedWall ? 18 : (q.Kind == PropKind.Bramble ? 12 : 7));
                if (M.Dist2(q.X, q.Y - (q.Kind == PropKind.CrackedWall ? 10 : 4), x, y) > reach * reach) continue;
                if (q.ShakeT > 0.12f) continue;      // one blow per swing
                ShakeProp(q);
                if (q.Kind == PropKind.Bramble) { Splat(q.X, q.Y, 8, 0xA2233B, 5, 0, -1, 60); SndAt(Sfx.Cut, q.X, q.Y, 0.9f); }
                else if (q.Kind == PropKind.CrackedWall) { Dust(q.X, q.Y - 8, 6, 0x66728B); Splat(q.X, q.Y, 14, 0x4B546D, 6, 0, 1, 60); SndAt(Sfx.HitBone, q.X, q.Y, 0.9f); }
                if (IsClient) NetHitProp(q, power); else DamageProp(q, power);
            }
        }

        /// <summary>Host side: wears a destructible prop down and breaks it when it gives way.</summary>
        public void DamageProp(Prop q, int power)
        {
            if (q.Kind != PropKind.Breakable)
            {
                q.HP -= power;
                if (q.HP > 0) return;
            }
            BreakProp(q);
            NetPropBroken(q);
        }

        /// <summary>Removes a broken prop. Runs on every machine; each player gets their own spoils.</summary>
        public void BreakProp(Prop q)
        {
            Map m = Cur.Props.Contains(q) ? Cur : (Over.Props.Contains(q) ? Over : Abbey);
            float sx = q.X, sy = q.Y;
            switch (q.Kind)
            {
                case PropKind.Breakable:
                    Splat(sx, sy, 6, 0x7E5D45, 9, 0, -1, 80); Dust(sx, sy, 4, 0x8A8A9A);
                    SndAt(Sfx.Break, sx, sy, 0.9f);
                    if (Rnd.Chance(0.5f)) DropGold(sx, sy, 1 + Rnd.Int(6 + (Cur == Abbey ? 8 : 0)));
                    if (Rnd.Chance(0.09f)) SpawnPickup(Pickup.Health, sx, sy, 1, null);
                    m.Remove(q);
                    break;
                case PropKind.Bramble:
                    Flags.Add("cut_" + q.Id);
                    Splat(sx, sy, 8, 0x6E1730, 14, 0, -1, 90);
                    m.Remove(q);
                    if (!Flags.Contains("found_glade")) { Flags.Add("found_glade"); Notify("^cThe thornbrake gives way. Something glimmers beyond.^0"); }
                    break;
                case PropKind.CrackedWall:
                    Flags.Add("secret_open");
                    m.Remove(q);
                    AddRubble(m, sx, sy);
                    Dust(sx, sy - 6, 16, 0x66728B); Splat(sx, sy, 14, 0x4B546D, 20, 0, 1, 110);
                    SndAt(Sfx.WallBreak, sx, sy, 1f); Shake(4f, 0.4f);
                    Notify("^cThe wall crumbles - a sealed passage!^0");
                    GainXp(40, true);
                    break;
            }
            m.FlowValid = false;
        }

        static void AddRubble(Map m, float sx, float sy)
        {
            Prop r1 = new Prop(); r1.Spr = Assets.Rubble[0]; r1.X = sx - 12; r1.Y = sy + 4; m.Add(r1);
            Prop r2 = new Prop(); r2.Spr = Assets.Rubble[1]; r2.X = sx + 13; r2.Y = sy + 2; m.Add(r2);
        }

        // restoring world state
        /// <summary>Re-applies saved flags to a freshly generated map.</summary>
        public void ApplyFlags(Map m)
        {
            List<Prop> all = new List<Prop>(m.Props);
            foreach (Prop q in all)
            {
                switch (q.Kind)
                {
                    case PropKind.Chest:
                        if (Flags.Contains("open_" + q.Id)) { q.Spr = q.Param >= 100 ? Assets.ChestOrnateOpen : Assets.ChestWoodOpen; q.Interactive = false; q.State = 1; }
                        break;
                    case PropKind.Shrine:
                        if (Flags.Contains(q.Id)) LightProp(q, Assets.ShrineLit, 86, WorldGen.EmberLight, 0.95f, -22);
                        break;
                    case PropKind.Moonpetal:
                        if (Flags.Contains("got_" + q.Id)) { q.Spr = Assets.MoonpetalPicked; q.Interactive = false; q.LightR = 0; }
                        break;
                    case PropKind.Bramble:
                        if (Flags.Contains("cut_" + q.Id)) m.Remove(q);
                        break;
                    case PropKind.RuneStone:
                        if (Flags.Contains(q.Id)) { q.Spr = Assets.StoneLit[q.Param]; q.Prompt = "The stone hums"; q.LightR = 70; q.LightColor = 0x80D0FF; q.LightI = 0.9f; q.LightOY = -18; }
                        break;
                    case PropKind.Cache:
                        if (Flags.Contains("open_cache")) { q.Spr = Assets.BarrowOpen; q.Interactive = false; }
                        else if (AllRunes) { q.Spr = Assets.BarrowRune; q.Prompt = "Open the king's door"; q.LightR = 60; q.LightColor = 0x80D0FF; q.LightI = 0.7f; q.LightOY = -14; }
                        break;
                    case PropKind.Brazier:
                        if (Flags.Contains("lit_" + q.Id)) { LightProp(q, Assets.BrazierEmber, 110, WorldGen.EmberLight, 1.1f, -16); q.Interactive = false; }
                        break;
                    case PropKind.Keepsake:
                        if (Flags.Contains("got_" + q.Id)) m.Remove(q);
                        break;
                    case PropKind.CrackedWall:
                        if (Flags.Contains("secret_open")) { float sx = q.X, sy = q.Y; m.Remove(q); AddRubble(m, sx, sy); }
                        break;
                    case PropKind.Bell:
                        if (Flags.Contains("bell_rung"))
                        {
                            q.Spr = Assets.BellRung; q.Interactive = false;
                            q.LightR = 150; q.LightColor = 0xFFD080; q.LightI = 1.3f; q.LightOY = -30;
                        }
                        else { q.Interactive = true; q.Prompt = "Examine the bell"; }
                        break;
                    case PropKind.Well: q.Interactive = true; q.Prompt = "Make a wish"; q.InteractR = 26; break;
                }
            }
            if (m == Abbey)
            {
                if (Flags.Contains("bellgate_open") || (Flags.Contains("lit_brazier_w") && Flags.Contains("lit_brazier_e"))) OpenBellgate(false);
            }
            if (m == Over && Flags.Contains("wren_lantern")) AddWrenLantern();
            m.FlowValid = false;
        }

        // quest progress and rewards
        /// <summary>
        /// Called on every machine whenever a quest changes: shows the banner and gives the local player
        /// whatever that step is worth.
        /// </summary>
        public void OnQuestProgress(Quest q, int oldStage, bool oldDone)
        {
            Player p = Me;
            if (p == null) return;
            if (oldStage < 0 && q.Stage >= 0 && !q.Done)
            {
                QuestBanner("New Quest", q.Title, false); Snd(Sfx.QuestStart);
                if (!q.Main) Q.Tracked = q.Id;
            }
            else if (!q.Done && q.Stage > oldStage && oldStage >= 0)
            {
                Notify(Loc.F("^yJournal updated:^0 {0}", Loc.Tr(q.Title)));
                Snd(Sfx.QuestStart, 0.7f, 1.2f);
                Q.Tracked = q.Id;
            }
            if (q.Id == "bell")
            {
                if (oldStage < 1 && q.Stage >= 1 && oldStage >= 0) { p.HealthPotions += 2; Notify("Received ^r2 Health Draughts^0"); GainXp(40, true); }
                if (oldStage < 2 && q.Stage >= 2 && oldStage >= 0) { p.Gold += 25; Notify("Received ^y25 gold^0"); GainXp(60, true); }
                if (oldStage < 3 && q.Stage >= 3 && oldStage >= 0) GainXp(80, true);
                if (q.Stage >= 6 && Abbey != null)
                {
                    Prop bell = Abbey.FindProp("bell");
                    if (bell != null && !Flags.Contains("bell_rung")) { bell.Interactive = true; bell.Prompt = "Hang the Bell's Tongue"; }
                }
            }
            if (!q.Done || oldDone) return;
            QuestBanner("Quest Complete", q.Title, true); Snd(Sfx.QuestDone);
            if (Q.Tracked == q.Id) Q.Tracked = "bell";
            switch (q.Id)
            {
                case "bell":
                    p.Gold += 150; Notify("Received ^y150 gold^0");
                    GainXp(300, true);
                    Give(Items.RollEquip(Rnd, Math.Max(8, p.Level), p.Class, 3));
                    break;
                case "wolves":
                    Give(Items.Unique("bramwork", p.Class, Math.Max(3, p.Level)));
                    p.Gold += 40; Notify("Received ^y40 gold^0");
                    GainXp(110, true);
                    break;
                case "petals":
                    p.RemoveKeepsake("moonpetal");
                    p.HealthPotions += 3; p.SpiritPotions += 2;
                    Notify("Received ^r3 Health^0 and ^b2 Spirit Draughts^0");
                    Give(Items.Unique("sprigring", p.Class, Math.Max(2, p.Level)));
                    GainXp(90, true);
                    break;
                case "lamplighter":
                    p.RemoveKeepsake("lantern");
                    Give(Items.Unique("lanterncharm", p.Class, Math.Max(3, p.Level)));
                    GainXp(130, true);
                    Flags.Add("wren_lantern");
                    AddWrenLantern();
                    break;
            }
            if (!IsClient) SaveGame();
        }

        // story beats
        void StartBossFight(Player trigger)
        {
            Enemy prior = null;
            foreach (Enemy e in Foes) if (e.Kind == EnemyKind.Prior && !e.Dead) prior = e;
            if (prior == null) return;
            BossFight = true; Boss = prior; BossIntroT = 0;
            prior.Aggro = true; prior.SetState(Enemy.Alert, 2.4f);
            SetBossGate(true);
            Snd(Sfx.Gate, 0.9f, 0.8f); Snd(Sfx.BossRoar);
            Shake(4f, 0.6f);
            SetMusic(Zones.MusBoss);
            // the gate has shut: nobody is left outside
            if (Online)
                for (int i = 0; i < Party.Count; i++)
                {
                    Player pl = Party[i];
                    if (pl == trigger || InArena(pl)) continue;
                    NetPull(pl, trigger.X, Math.Min(trigger.Y, DungeonGen.ArenaY1 - 34));
                }
        }

        static bool InArena(Player pl) { return pl.Y < DungeonGen.ArenaY1 - 6 && pl.X > DungeonGen.ArenaX0 - 12 && pl.X < DungeonGen.ArenaX1 + 12; }

        /// <summary>Host side: the world consequences of the Prior's death.</summary>
        void OnPriorDefeated(Enemy e)
        {
            BossFight = false;
            SetBossGate(false);
            for (int i = Foes.Count - 1; i >= 0; i--)
                if (!Foes[i].Dead && Foes[i].Spawn != null && Foes[i].Spawn.Summoned) KillEnemy(Foes[i], 0, -1);
            Shots.Clear();
            for (int i = Areas.Count - 1; i >= 0; i--) if (!Areas[i].FromPlayer) Areas.RemoveAt(i);
            SetMusic(Zones.MusDungeon);
        }

        /// <summary>Every machine: what the local player sees and receives when an elite or boss falls.</summary>
        void BossSpoils(EnemyKind kind, float x, float y)
        {
            if (kind == EnemyKind.Prior)
            {
                if (Me.KeepsakeCount("tongue") == 0 && !Flags.Contains("bell_rung")) SpawnPickup(Pickup.Quest, x, y, 1, Items.Quest("tongue"));
                Notify("^pThe Silent Prior is unmade.^0");
                Motes(x, y, 10, 0xEAF3FF, 0x3A2E5E, 60, 20, 70, 1.6f);
            }
            else if (kind == EnemyKind.Knight)
            {
                if (Me.KeepsakeCount("sigil") == 0 && !Flags.Contains("abbey_open")) SpawnPickup(Pickup.Quest, x, y, 1, Items.Quest("sigil"));
                Notify("^cSir Caldus kneels, and is dust.^0");
                Motes(x, y, 10, 0x9BDCF4, 0x1E3A5C, 40, 16, 60, 1.4f);
            }
        }

        void UpdateStory(float dt)
        {
            Player p = Me;
            BossIntroT += dt; LevelUpT += dt;
            if (Dawn) DawnT += dt;
            if (!IsClient && Cur == Abbey && !BossFight && !Flags.Contains("dead_prior"))
            {
                foreach (Player pl in AllPlayers())
                    if (!pl.Dead && InArena(pl)) { StartBossFight(pl); break; }
            }
            if (!IsClient && Boss != null && (Boss.Dead || (!Boss.Aggro && !Boss.Def.Boss))) { if (Boss.Dead && Boss.DeathT > 0.6f || !Boss.Dead) Boss = null; }
            if (EndingPending > 0)
            {
                EndingPending -= dt;
                if (EndingPending <= 0) { EndingPending = -1; if (Ov == Overlay.None || !Online) { Ov = Overlay.Ending; EndingT = 0; } }
            }
            // first-minute guidance
            if (hintStep < 4 && IntroT < 60)
            {
                if (hintStep == 0 && IntroT > 1.2f) { hintStep = 1; Notify(FirstPerson ? "^yWASD^0 to move  -  look with the ^ymouse^0" : "^yWASD^0 to move  -  aim with the ^ymouse^0"); }
                else if (hintStep == 1 && IntroT > 6f) { hintStep = 2; Notify(Loc.F("^yLeft mouse^0: {0}   ^yRight mouse^0: {1}", Loc.Tr(p.Def.Abilities[0].Name), Loc.Tr(p.Def.Abilities[1].Name))); }
                else if (hintStep == 2 && IntroT > 12f) { hintStep = 3; Notify(Loc.F("^yQ^0: {0}   ^ySpace^0: dodge roll   ^y1^0: health draught", Loc.Tr(p.Def.Abilities[2].Name))); }
                else if (hintStep == 3 && IntroT > 20f) { hintStep = 4; if (Q.Stage("bell") == 0 && !Online) Notify("Brother Halvard waits by the fire. Press ^yF^0 beside him."); }
            }
        }
        public float EndingT;
    }
}
