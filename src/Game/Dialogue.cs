using System;
using System.Collections.Generic;

namespace Vesperfell
{
    public sealed class DChoice
    {
        public string Text, Key; public Action Act;      // Key: the untranslated wording, so scripts can find a choice in any language
        public DChoice(string text, Action act) { Text = text; Key = text; Act = act; }
    }

    public sealed class DNode
    {
        public string Speaker = "", Text = "";
        public List<DChoice> Choices = new List<DChoice>();
    }

    /// <summary>
    /// Conversations. Each villager is a small tree of topics; choices can start quests, trade or hand quests in.
    /// What a quest step is worth is paid out by OnQuestProgress, so in co-op the whole party is rewarded.
    /// </summary>
    public sealed partial class Game
    {
        public DNode Dlg; public Npc DlgNpc; public float DlgT;
        int rumour;

        public void StartDialogue(Npc n)
        {
            DlgNpc = n; Ov = Overlay.Dialogue;
            In.Pressed[Key.F] = false;
            n.Facing = Me.X >= n.X ? 1 : -1;
            Me.Blocking = false;
            Snd(Sfx.UiOpen, 0.6f, 1f);
            Talk("root");
        }

        public void CloseDialogue()
        {
            Ov = Overlay.None; Dlg = null; inputLockT = 0.25f;
            Snd(Sfx.UiClose, 0.6f, 1f);
        }

        void Node(string text, params DChoice[] choices)
        {
            DNode d = new DNode();
            d.Speaker = DlgNpc != null ? DlgNpc.Name : "";
            d.Text = text;
            for (int i = 0; i < choices.Length; i++) if (choices[i] != null) d.Choices.Add(choices[i]);
            if (d.Choices.Count == 0) d.Choices.Add(Bye());
            Dlg = d; DlgT = 0;
        }

        DChoice C(string text, string topic) { return new DChoice(text, delegate { Talk(topic); }); }
        DChoice C(string text, Action act) { return new DChoice(text, act); }
        /// <summary>A choice whose wording has numbers in it.</summary>
        DChoice CF(string pattern, string topic, params object[] args)
        {
            DChoice c = new DChoice(Loc.F(pattern, args), delegate { Talk(topic); });
            c.Key = pattern;
            return c;
        }
        DChoice Bye() { return new DChoice("Farewell.", CloseDialogue); }
        DChoice Back() { return new DChoice("Something else.", delegate { Talk("root"); }); }

        /// <summary>Hands an item to the player, or drops it at their feet when the pack is full.</summary>
        public void Give(Item it)
        {
            Player p = Me;
            if (it.IsEquip && p.AddToBag(it)) { Notify(Loc.F("Received ^{0}{1}^0", Items.RarityCode[(int)it.Rar], it.DisplayName)); Snd(Sfx.PickItem, 1f, 1.1f); }
            else { Pickup d = SpawnPickup(Pickup.Equip, p.X, p.Y + 6, 1, it); d.T = -1.5f; Notify(Loc.F("^rPack full^0 - {0} lies at your feet", it.DisplayName)); }
        }

        void Talk(string topic)
        {
            if (DlgNpc == null) { CloseDialogue(); return; }
            switch (DlgNpc.Id)
            {
                case "warden": TalkWarden(topic); break;
                case "elder": TalkElder(topic); break;
                case "smith": TalkSmith(topic); break;
                case "herbalist": TalkHerbalist(topic); break;
                case "innkeeper": TalkInnkeeper(topic); break;
                case "wren": TalkWren(topic); break;
                case "ilse": TalkIlse(topic); break;
                default: TalkVillager(topic); break;
            }
        }

        // Brother Halvard
        void TalkWarden(string topic)
        {
            Player p = Me;
            switch (topic)
            {
                case "root":
                    if (Q.Stage("bell") == 0)
                        Node("So. You came. I had begun to think the road had taken you as well.\n\nThirty nights ago the Vesper Bell fell silent, and the dusk has not ended since. The Gloam is in the vale now. You will have seen the wolves' eyes.",
                            C("What must I do?", "task"), C("What is the Vesper Bell?", "bell"));
                    else if (Dawn)
                        Node("Listen.\n\n...Four hundred years the Order rang it, and I never once just stood still and listened. Thank you, Warden. Go and be warm somewhere.",
                            C("Tell me about the bell.", "bell"), Bye());
                    else
                        Node("Still breathing? Good. This fire is the only thing left on the road that knows what hour it is.",
                            C("Tell me about the bell.", "bell"), C("What is the Gloam?", "gloam"), C("Any advice for the road?", "advice"), Bye());
                    break;
                case "task":
                    if (Q.Stage("bell") == 0) Q.SetStage("bell", 1, this);
                    Node("Go north to Emberwick and find Elder Maren. She kept the abbey's keys for thirty years, until the Prior turned her out.\n\nTake these draughts. I am too old to need them, and you are about to need them badly.",
                        C("Any advice for the road?", "advice"), C("What is the Gloam?", "gloam"), Bye());
                    break;
                case "bell":
                    Node("Saint Orrin hung it four hundred years ago. Rung at each day's end, it told the dark where the vale stopped.\n\nSeven wardens to ring it. I was the seventh. The Prior dismissed us one by one - gently, always gently - and not one of us thought to ask him why.",
                        Q.Stage("bell") == 0 ? C("What must I do?", "task") : Back(), Bye());
                    break;
                case "gloam":
                    Node("It is not fog and it is not night. It is what was here before the bell - older than the barrow-kings.\n\nIt gets into things. Beasts first. Then the dead. Then, I think, anyone who listens to it for too long.", Back(), Bye());
                    break;
                case "advice":
                    {
                        string tip;
                        switch (p.Class)
                        {
                            case ClassId.Warrior: tip = "Your shield is half your sword. Raise it (right mouse) just as a blow lands and the brute who swung will stagger. Every strike feeds your Fury - spend it."; break;
                            case ClassId.Mage: tip = "Keep your distance and keep moving. Blink (right mouse) when they close in, and remember that frost holds a foe still long enough to burn it."; break;
                            case ClassId.Rogue: tip = "Never fight from the front if you can help it. Step through them (Q) and cut from behind - and when it turns bad, you have two rolls where the rest of us have one."; break;
                            default: tip = "Let them come to you across open ground. Lay a snare (Q) on the path they will take, and loose the piercing shot (right mouse) when they line up."; break;
                        }
                        Node(Loc.Tr(tip) + "\n\n" + Loc.Tr("Rest at the wayshrines - they still hold a little of the bell's blessing. And when the ground turns red beneath you, be somewhere else."), Back(), Bye());
                    }
                    break;
                default: Talk("root"); break;
            }
        }

        // Elder Maren
        void TalkElder(string topic)
        {
            int s = Q.Stage("bell");
            switch (topic)
            {
                case "root":
                    if (Q.IsDone("bell"))
                        Node("Look at the sky, Warden. I had forgotten it could be that colour.\n\nStay as long as you like. There is still a great deal of dusk left in the corners of this vale, and you seem to be good at corners.", Bye());
                    else if (s == 7)
                        Node("I heard it. We all heard it. Odo dropped a full tray and nobody laughed.\n\nYou rang the Vesper Bell.", C("It is done. The Prior is gone.", "finish"));
                    else if (s <= 1)
                        Node("Halvard sent you? Then the Order has one blade left - or whatever it is you carry.\n\nI am Maren. I held the abbey's keys for thirty years, until Prior Aldous took them from me with a smile and a blessing.",
                            C("How do I get into the abbey?", "sigil"), C("Who is the Prior?", "prior"), Bye());
                    else if (s == 2)
                        Node("East, past the standing stones, to the King's Barrow. Sir Caldus was a hard man when he was alive.", C("Tell me about Sir Caldus.", "caldus"), C("Who is the Prior?", "prior"), Bye());
                    else if (s == 3)
                        Node("You have it. I can feel it from here - cold, like a coin left on a windowsill.\n\nNorth, through the graveyard. Press it to the stone and the doors will know you.", C("What waits inside?", "inside"), Bye());
                    else
                        Node("Still breathing. Good. Do not let me keep you - but do not hurry so much that you forget to rest at the shrines.", C("What waits inside the abbey?", "inside"), C("Who is the Prior?", "prior"), Bye());
                    break;
                case "sigil":
                    if (s <= 1) Q.SetStage("bell", 2, this);
                    Node("The doors are sealed with a sigil-stone, and he has the only one I ever held. But there was a second. It was buried with Sir Caldus, the abbey's first champion, in the King's Barrow out east.\n\nHe will not have stayed buried. Nothing does, now. Here - coin for the smith and the herbalist. Spend it before you go.",
                        C("Tell me about Sir Caldus.", "caldus"), C("Who is the Prior?", "prior"), Bye());
                    break;
                case "caldus":
                    Node("He charges like a bull and, like a bull, he is poor at stopping. Let him find a wall.\n\nWhen he raises that great sword over his head, do not be near him when it comes down.", Back(), Bye());
                    break;
                case "prior":
                    Node("Aldous. A gentle man. He used to say the bell hurt him - that every stroke went through him like a nail. We thought it was only age.\n\nOn the last night he sent the ringers away and climbed the tower alone. In the morning there was no morning.", Back(), Bye());
                    break;
                case "inside":
                    Node("The undercroft. Two vigil-flames kept the Bellgate, one in the west wing and one in the east - he will have put them out. Light them both and the way to the crypt will open.\n\nAnd if you meet Sister Ilse... be kind. She stayed when the rest of us ran.", Back(), Bye());
                    break;
                case "finish":
                    if (!Q.IsDone("bell")) Q.Complete("bell", this);
                    Node("Then he has his silence at last, poor man, and we have our evenings back.\n\nThis was laid by for the Order's champion. There has not been one in my lifetime. There is now.", Bye());
                    break;
                default: Talk("root"); break;
            }
        }

        // Bram the Smith
        int TemperCost(Item it) { return it == null ? 0 : 25 * (it.Upgrade + 1) * (1 + (int)it.Rar); }

        void SpareGear(out int count, out int gold)
        {
            count = 0; gold = 0;
            foreach (Item it in Me.Bag) if (it != null && it.IsEquip && it.Rar <= Rarity.Fine && !it.Unique) { count++; gold += it.Value; }
        }

        void TalkSmith(string topic)
        {
            Player p = Me;
            Quest w = Q.Get("wolves");
            Item wp = p.Equip[Player.SlotWeapon], ar = p.Equip[Player.SlotArmor];
            int spareN, spareG; SpareGear(out spareN, out spareG);
            switch (topic)
            {
                case "root":
                    {
                        DChoice quest = null;
                        if (w.Stage < 0) quest = C("Need any work done?", "offer");
                        else if (!w.Done && w.Stage == 0) quest = C("About those wolves...", "progress");
                        else if (!w.Done && w.Stage == 1) quest = C("The pack is thinned.", "reward");
                        Node(Dawn ? "Would you look at that. I can see what I'm hitting. Bram - and I owe you an ale, Warden." : "Mind the sparks. Bram. I'd shake your hand, but it would take the skin off.",
                            quest,
                            wp != null && wp.Upgrade < 5 ? CF("Temper my weapon to +{0}  ({1} gold)", "temper_w", wp.Upgrade + 1, TemperCost(wp)) : null,
                            ar != null && ar.Upgrade < 5 ? CF("Temper my armour to +{0}  ({1} gold)", "temper_a", ar.Upgrade + 1, TemperCost(ar)) : null,
                            spareN > 0 ? CF("Sell all Common and Fine gear in my pack  ({0} for {1} gold)", "sell", spareN, spareG) : null,
                            Bye());
                    }
                    break;
                case "offer":
                    Node("Wolves. They come up to the palisade at lamp-lighting now and just... stand there. Looking in. Their eyes are the wrong colour.\n\nThin the pack - six should do it - and you can have the best thing I've made this year.",
                        C("I'll hunt them.", delegate { Q.Start("wolves", this); Talk("accepted"); }), C("Not now.", "root"));
                    break;
                case "accepted":
                    Node("West gate, follow the lamps. You'll hear them before you see them.", Back(), Bye());
                    break;
                case "progress":
                    Node(Loc.F("{0} of {1}. Keep at it. And mind the big one - the mother. She's cleverer than the rest.", w.Count, w.Need), Back(), Bye());
                    break;
                case "reward":
                    if (!w.Done) Q.Complete("wolves", this);
                    Node("Six? I slept last night. First time in a month. Here - I meant it for the Elder's nephew, but he ran off south, and you didn't.", Back(), Bye());
                    break;
                case "temper_w":
                case "temper_a":
                    {
                        Item it = topic == "temper_w" ? wp : ar;
                        int cost = TemperCost(it);
                        if (it == null || it.Upgrade >= 5) { Talk("root"); break; }
                        if (p.Gold < cost) { Snd(Sfx.UiError); Node(Loc.F("That's {0} gold of work, friend, and you've {1}. Steel doesn't temper itself.", cost, p.Gold), Back(), Bye()); break; }
                        p.Gold -= cost; it.Upgrade++; p.Recalc(false);
                        Snd(Sfx.Block, 0.9f, 0.8f); Snd(Sfx.Equip);
                        Notify(Loc.F("^y{0}^0 tempered", it.DisplayName));
                        Node(Loc.Tr(topic == "temper_w" ? "There. Sharper than my wife's opinion of me." : "There. That'll turn a blade that would have opened you yesterday.") + "\n\n^g" + it.DisplayName + "^0", Back(), Bye());
                    }
                    break;
                case "sell":
                    {
                        int n = 0, g = 0;
                        for (int i = 0; i < p.Bag.Length; i++)
                        {
                            Item it = p.Bag[i];
                            if (it != null && it.IsEquip && it.Rar <= Rarity.Fine && !it.Unique) { n++; g += it.Value; p.Bag[i] = null; }
                        }
                        p.Gold += g;
                        if (n > 0) { Snd(Sfx.Coin); Notify(Loc.F("Sold {0} items for ^y{1} gold^0", n, g)); }
                        Node(n > 0 ? Loc.F("I'll melt the lot down. {0} gold, and that's generous.", g) : "You've nothing I'd buy.", Back(), Bye());
                    }
                    break;
                default: Talk("root"); break;
            }
        }

        // Ysolde
        void TalkHerbalist(string topic)
        {
            Player p = Me;
            Quest q = Q.Get("petals");
            const int Price = 20;
            switch (topic)
            {
                case "root":
                    {
                        DChoice quest = null;
                        if (q.Stage < 0) quest = C("You look worried.", "offer");
                        else if (!q.Done && q.Stage == 0) quest = C("About the moonpetals...", "progress");
                        else if (!q.Done && q.Stage == 1) quest = C("I have the moonpetals.", "reward");
                        Node(Dawn ? "Sunlight on the herb beds. They won't know what to do with themselves. Neither do I." : "Careful where you step - that's feverfew. I'm Ysolde. If you're bleeding, I have draughts. If you're not, give it an hour.",
                            quest,
                            CF("Buy a Health Draught  ({0} gold)", "buy_h", Price),
                            CF("Buy a Spirit Draught  ({0} gold)", "buy_s", Price),
                            Bye());
                    }
                    break;
                case "offer":
                    Node("Half the village has the dusk-fever. Moonpetal breaks it, but it only grows in the Thornwood now, and I can't send anyone there.\n\nFive blooms would see us through. They glow - you can't miss them in the dark, and it's always dark.",
                        C("I'll gather them.", delegate { Q.Start("petals", this); Talk("accepted"); }), C("Not now.", "root"));
                    break;
                case "accepted":
                    Node(Q.Stage("petals") == 1 ? "You have them already? Then hand them over, quick." : "Bless you. Take the stem as well as the bloom, if you can.",
                        Q.Stage("petals") == 1 ? C("Here.", "reward") : Back(), Bye());
                    break;
                case "progress":
                    Node(Loc.F("{0} of {1}. They like the roots of the big oaks, and the wet ground near the pond.", p.KeepsakeCount("moonpetal"), q.Need), Back(), Bye());
                    break;
                case "reward":
                    if (!q.Done) Q.Complete("petals", this);
                    Node("Oh, these are lovely - look how bright. That's the Fuller children and old Hask sorted by tomorrow.\n\nI wove this from the first stem I ever picked. It never wilted. I think it wants to be worn.", Back(), Bye());
                    break;
                case "buy_h":
                case "buy_s":
                    if (p.Gold < Price) { Snd(Sfx.UiError); Node(Loc.F("That's {0} gold, love. I can't brew on kindness.", Price), Back(), Bye()); break; }
                    p.Gold -= Price;
                    if (topic == "buy_h") p.HealthPotions++; else p.SpiritPotions++;
                    Snd(Sfx.PickPotion);
                    Node(topic == "buy_h"
                            ? Loc.F("There you are. Health Draughts: ^r{0}^0\n\n^dYou have {1} gold.^0", p.HealthPotions, p.Gold)
                            : Loc.F("There you are. Spirit Draughts: ^b{0}^0\n\n^dYou have {1} gold.^0", p.SpiritPotions, p.Gold),
                        CF("Another Health Draught  ({0} gold)", "buy_h", Price), CF("Another Spirit Draught  ({0} gold)", "buy_s", Price), Back(), Bye());
                    break;
                default: Talk("root"); break;
            }
        }

        // Odo
        void TalkInnkeeper(string topic)
        {
            Player p = Me;
            switch (topic)
            {
                case "root":
                    Node(Dawn ? "Drinks are free until I remember I have a business to run. Odo. You'll be the one who rang it, then." : "Welcome to the Last Candle. Odo. We're out of nearly everything, but the fire's real.",
                        C("Heard anything worth hearing?", "rumour"), C("A bed for the night  (5 gold)", "bed"), Bye());
                    break;
                case "rumour":
                    {
                        string[] r =
                        {
                            "My grandfather laid stone at the abbey. Swore there was a vault off the ossuary the monks bricked up in a single night. 'You could hear it was hollow,' he said. 'Knock hard enough and it would answer.'",
                            "There are three old stones in this vale with signs cut in them - one by the watchtower in the Thornwood, one down the south end of the Barrowfield, one out in Mirefen on a bit of dry ground. Older than the abbey. They say if all three wake, the kings' hoard opens.",
                            "North of the fork on the west road there's a thornbrake nobody's cut in years. Tobin used to say he saw something shining behind it. A good blade would make short work of thorns.",
                            "Sir Caldus? My nan used to frighten us with him. 'Charges like a bull,' she'd say, 'and can't stop any better.' Put a wall behind you and step aside.",
                            "The Prior never liked noise. Used to wince at my shutters. Funny thing to say about a churchman, but I don't think he ever forgave the bell for being loud.",
                        };
                        string text = r[rumour % r.Length];
                        if (rumour % r.Length == 1 && Q.Stage("stones") < 0) Q.Start("stones", this);
                        rumour++;
                        Node(text, C("Anything else?", "rumour"), Back(), Bye());
                    }
                    break;
                case "bed":
                    if (p.Gold < 5) { Snd(Sfx.UiError); Node("Five gold. I'd let you have it for nothing, but then they'd all want it for nothing.", Back(), Bye()); break; }
                    p.Gold -= 5; p.HP = p.MaxHP; if (p.Class != ClassId.Warrior) p.Res = p.MaxRes; p.ClearStatus();
                    Snd(Sfx.Shrine, 0.7f, 0.9f);
                    SaveGame();
                    Node("^gYou sleep. For the first time in thirty nights, you dream of morning.^0\n\nUp already? The stew's on. It's mostly turnip.", Back(), Bye());
                    break;
                default: Talk("root"); break;
            }
        }

        // Wren
        void TalkWren(string topic)
        {
            Player p = Me;
            Quest q = Q.Get("lamplighter");
            switch (topic)
            {
                case "root":
                    if (q.Done) Node("I light it every night now. Somebody has to do the lamps.", Bye());
                    else if (q.Stage == 1 || p.KeepsakeCount("lantern") > 0)
                        Node("Did you... is that...", C("I found this in the deep wood. I'm sorry.", "reward"));
                    else if (q.Stage == 0)
                        Node("Did you find him? He has a lantern with a brass cap. He never goes anywhere without it.", C("Where did he go, again?", "where"), Bye());
                    else
                        Node("Are you a warden? A real one?\n\nMy da's Tobin. He lights the lamps on the west road. He went out six nights ago to do the far ones, and he hasn't come back yet.",
                            C("I'll look for him.", delegate { Q.Start("lamplighter", this); Talk("where"); }), C("I'm sorry. I can't.", CloseDialogue));
                    break;
                case "where":
                    Node("West road, to the fork with the signpost. Then north, where the lamps stop. He always said the deep wood lamps mattered most, because nobody else would do them.\n\nI'm not allowed past the gate. So I wait here.", Bye());
                    break;
                case "reward":
                    if (!q.Done) Q.Complete("lamplighter", this);
                    Node("(She holds it for a long time.)\n\nIt's still warm. He kept it lit so I'd know which way was home.\n\n...I made this for him, for when he came back. You have it. So you don't get lost too.", Bye());
                    break;
                default: Talk("root"); break;
            }
        }

        /// <summary>After Wren's quest, her father's lantern burns at the west gate.</summary>
        public void AddWrenLantern()
        {
            if (Over == null || Over.FindProp("wren_lantern") != null) return;
            Prop l = new Prop();
            l.Id = "wren_lantern"; l.Anim = Assets.LanternLit; l.Spr = Assets.LanternLit[0]; l.X = 71.4f * 16; l.Y = 66.2f * 16;
            l.LightR = 96; l.LightColor = WorldGen.EmberLight; l.LightI = 1.1f; l.LightOY = -24; l.Flicker = true;
            Over.Add(l);
        }

        // Sister Ilse
        void TalkIlse(string topic)
        {
            switch (topic)
            {
                case "root":
                    if (Flags.Contains("bell_rung"))
                        Node("(She has turned to face the stairs, and the light coming down them.)\n\nOh. Oh, there it is. I had forgotten the sound. Thank you. I think I can go up now.", Bye());
                    else
                        Node("(The figure does not turn.)\n\nYou are warm. It has been some time since anything warm came down those stairs. I am - I was - Sister Ilse. I rang the Vesper with the others, until he sent us away. I did not go far enough.",
                            C("What happened here?", "what"), C("How do I reach the Prior?", "how"), C("Is anything hidden in this place?", "secret"), Bye());
                    break;
                case "what":
                    Node("Prior Aldous took the bell's tongue and sealed the doors, and one by one we stopped.\n\nHe is below, in the crypt, keeping the silence. He believes he is sparing us the noise. He was always kind.", Back(), Bye());
                    break;
                case "how":
                    Node("The Bellgate bars the chancel. Two vigil-flames open it: one in the scriptorium, west of the nave, and one in the ossuary to the east. He put them out himself.\n\nYou may relight them. I cannot hold fire any more.", Back(), Bye());
                    break;
                case "secret":
                    Node("In the ossuary, the north wall. We laid Saint Orrin's own vestments behind it when the Gloam first stirred, and bricked it shut in a single night.\n\nThe mortar never set properly. Strike it.", Back(), Bye());
                    break;
                default: Talk("root"); break;
            }
        }

        // villagers
        void TalkVillager(string topic)
        {
            string[] dusk =
            {
                "I keep setting the table for supper. It's always supper-time now, isn't it?",
                "My hens have stopped laying. They don't know when morning is. Nor do I.",
                "Don't go west after... well. Don't go west.",
                "The Elder says help is coming up the Low Road. I suppose that's you. You're shorter than I hoped.",
                "I heard something scratching at the palisade last night. Slowly. All the way along.",
                "Bram says the wolves just stand and watch. I said, watch what? He didn't answer.",
            };
            string[] dawn =
            {
                "I'd forgotten shadows could point somewhere.",
                "The hens are laying again. All at once. It's chaos.",
                "My mother wept when she saw the east. Then she told me to air the bedding.",
                "They'll tell stories about you, Warden. I've already started one.",
            };
            string[] set = Dawn ? dawn : dusk;
            int idx = (int)((DlgNpc.HomeX * 7 + UiT * 0.2f) % set.Length);
            if (idx < 0) idx = 0;
            Node(set[idx % set.Length], Bye());
        }
    }
}
