using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>Animation frames for one actor. All frames face right; the renderer mirrors them.</summary>
    public sealed class ActorSprites
    {
        public Sprite[] Idle;
        public Sprite[] Run;
        public int HandX = 5, HandY = -9;   // weapon anchor relative to the feet origin (facing right)
        public int Height = 24;             // used for health bars and floating text
        public int ShadowW = 7;
    }

    /// <summary>Hand-pixelled humanoids assembled from parts (head, torso, legs or robe, back layer).</summary>
    public static class CharacterArt
    {
        public const int FW = 22, FH = 30;      // frame canvas
        public const int OX = 11, OY = 28;      // feet origin

        public delegate void LayerFn(Cv cv, int frame, int bob, bool running);

        public sealed class HumanDef
        {
            public string[] Head, Body;
            public bool Robe;
            public string[][] RobeFrames;       // optional custom robe (stand, sway a, sway b)
            public int[] Map;
            public LayerFn Back, Front;
            public int Outline = Pal.Ink0;
            public int HeadDX = 0, HeadDY = 0;
            public bool Hunched;
        }

        // legs: 10 wide, 7 tall. l/b = near leg & boot, L/B = far leg & boot.
        static readonly string[] LegsStand =
        {
            "..LL.ll...",
            "..LL.ll...",
            "..LL.ll...",
            "..LL.ll...",
            "..LL.ll...",
            "..BB.bb...",
            "..BBBbbb..",
        };
        static readonly string[][] LegsRun =
        {
            new[]{
            "..LLlll...",
            "..LLLll...",
            ".LL..lll..",
            ".LL...ll..",
            "BB....ll..",
            "BB....bb..",
            "......bbb.",
            },
            new[]{
            "..LLll....",
            "..LLll....",
            "...Lll....",
            "..BBll....",
            "..BBll....",
            "....bb....",
            "....bbb...",
            },
            new[]{
            "..lllLL...",
            "..lllLL...",
            ".ll..LLL..",
            ".ll...LL..",
            "bb....LL..",
            "bb....BB..",
            "......BBB.",
            },
            new[]{
            "..llLL....",
            "..llLL....",
            "...lLL....",
            "..bbLL....",
            "..bbLL....",
            "....BB....",
            "....BBB...",
            },
        };

        static readonly string[] RobeStand =
        {
            ".ccccttcccc.",
            ".cccCttCccc.",
            ".cccCttCccc.",
            "ccccCttCcccc",
            "ccccCttCcccc",
            "cCccCttCccCc",
            "tttttttttttt",
        };
        static readonly string[] RobeSwayA =
        {
            ".ccccttcccc.",
            ".cccCttCccc.",
            "cccCcttCccc.",
            "cccCcttcCccc",
            "ccCccttcCccc",
            "cCcccttccCcc",
            "ttttttttttt.",
        };
        static readonly string[] RobeSwayB =
        {
            ".ccccttcccc.",
            ".cccCttCccc.",
            ".cccCttcCccc",
            "ccccCttccCcc",
            "cccCcttccCcc",
            "ccCccttcccCc",
            ".ttttttttttt",
        };

        public static ActorSprites Build(HumanDef d)
        {
            ActorSprites a = new ActorSprites();
            a.Idle = new Sprite[2];
            a.Run = new Sprite[4];
            for (int i = 0; i < 2; i++) a.Idle[i] = Frame(d, false, i);
            for (int i = 0; i < 4; i++) a.Run[i] = Frame(d, true, i);
            return a;
        }

        static Sprite Frame(HumanDef d, bool running, int f)
        {
            Cv cv = new Cv(FW, FH);
            int bob = running ? ((f & 1) == 1 ? -1 : 0) : (f == 1 ? 1 : 0);
            if (d.Back != null) d.Back(cv, f, bob, running);
            if (d.Robe)
            {
                string[] rb = RobeStand;
                if (d.RobeFrames != null) rb = running ? d.RobeFrames[1 + (f & 1)] : d.RobeFrames[0];
                else if (running) rb = (f & 2) == 0 ? ((f & 1) == 0 ? RobeSwayA : RobeStand) : ((f & 1) == 0 ? RobeSwayB : RobeStand);
                cv.Ascii(5, 21, rb, d.Map);
            }
            else
            {
                cv.Ascii(6, 21, running ? LegsRun[f] : LegsStand, d.Map);
            }
            cv.Ascii(5, 12 + bob, d.Body, d.Map);
            int headBob = bob;
            if (!running && f == 1) headBob = 1;
            cv.Ascii(5 + d.HeadDX, 2 + headBob + d.HeadDY, d.Head, d.Map);
            if (d.Front != null) d.Front(cv, f, bob, running);
            cv.Outline(d.Outline);
            return cv.ToSprite(OX, OY);
        }

        // player classes
        public static HumanDef Warrior()
        {
            HumanDef d = new HumanDef();
            d.Head = new[]
            {
                "......pp....",
                ".....pppP...",
                "...mmmmpP...",
                "..mnnmmmmP..",
                "..mnmmmmmm..",
                "..mmmmmmmmm.",
                "..mMsssssss.",
                "..mMssesse..",
                "..MMsssssS..",
                "...MMssss...",
            };
            d.Body = new[]
            {
                "..mmccccmm..",
                ".mnmccccmnm.",
                ".mmMcaacMmm.",
                ".mM.caac.Mm.",
                "....cccc....",
                "...gggggg...",
                "...cccccc...",
                "...cCccCc...",
                "...cC..Cc...",
            };
            d.Map = Cv.Map('m', Pal.St4, 'n', Pal.St6, 'M', Pal.St2, 'p', Pal.Cr3, 'P', Pal.Cr1, 'c', Pal.Cr2, 'C', Pal.Cr1,
                'a', Pal.Go3, 'g', Pal.Ea3, 's', Pal.Sk3, 'S', Pal.Sk2, 'e', Pal.Ink1,
                'l', Pal.St3, 'L', Pal.St1, 'b', Pal.St2, 'B', Pal.St0);
            d.Back = delegate(Cv cv, int f, int bob, bool run)
            {
                // short crimson cape
                int sway = run ? ((f == 0 || f == 3) ? 1 : 0) : 0;
                cv.Poly(Pal.Cr1, 7, 13 + bob, 12, 13 + bob, 11, 23, 5 - sway, 23);
                cv.Poly(Pal.Cr0, 6 - sway, 19, 9, 19, 9, 23, 5 - sway, 23);
            };
            return d;
        }

        public static HumanDef Mage()
        {
            HumanDef d = new HumanDef();
            d.Head = new[]
            {
                ".hh.........",
                ".hhhh.......",
                "..hhhhhhh...",
                "..hjhhhhhh..",
                "..hjhhhhhhh.",
                "..hhhxxxxxh.",
                "..hhxxgxxgh.",
                "..hHxxxxxxh.",
                "..hHHxxxxH..",
                "...HHHHHH...",
            };
            d.Body = new[]
            {
                "...cccccc...",
                "..cccttccc..",
                "..ccCttccc..",
                "..ccCttCcc..",
                "..aaaaaaaa..",
                "..ccCttccc..",
                "..cccttccc..",
                ".ccccttcccc.",
                ".cccCttCccc.",
            };
            d.Robe = true;
            d.Map = Cv.Map('h', Pal.Gl1, 'j', Pal.Gl3, 'H', Pal.Gl0, 'x', Pal.Ink0, 'g', Col.E(Pal.Em5),
                'c', Pal.Gl1, 'C', Pal.Gl0, 't', Pal.Em3, 'a', Pal.Em2);
            return d;
        }

        public static HumanDef Rogue()
        {
            HumanDef d = new HumanDef();
            d.Head = new[]
            {
                "............",
                "....hhhhh...",
                "...hhhhhhh..",
                "..hjhhhhhhh.",
                "..hhhhhhhhh.",
                "..hhHssssss.",
                "..hhHsesse..",
                "..hHkkkkkk..",
                "..hHkkkkkK..",
                "...HHkkkK...",
            };
            d.Body = new[]
            {
                "...cccccc...",
                "..ccdccccc..",
                "..cccdcccc..",
                "..cCccdcCc..",
                "..cCcccdCc..",
                "...gggggg...",
                "...cccccc...",
                "...cCccCc...",
                "...cc..cc...",
            };
            d.Map = Cv.Map('h', Pal.St1, 'j', Pal.St3, 'H', Pal.St0, 's', Pal.Sk3, 'e', Pal.Ink0, 'k', Pal.Cr2, 'K', Pal.Cr1,
                'c', Pal.St1, 'C', Pal.St0, 'd', Pal.Ea4, 'g', Pal.Ea3,
                'l', Pal.St1, 'L', Pal.St0, 'b', Pal.Ink3, 'B', Pal.Ink2);
            d.Back = delegate(Cv cv, int f, int bob, bool run)
            {
                // trailing scarf
                int[] wave = run ? new[] { 0, -1, 0, 1 } : new[] { 0, 1, 0, 0 };
                int w = wave[f & 3];
                int y = 11 + bob;
                cv.Rect(5, y, 3, 2, Pal.Cr2);
                cv.Rect(3, y + 1 + w, 3, 2, Pal.Cr2);
                cv.Rect(1, y + 2 + w * 2, 3, 2, Pal.Cr1);
                if (run) cv.Set(0, y + 3 + w * 2, Pal.Cr1);
            };
            return d;
        }

        public static HumanDef Ranger()
        {
            HumanDef d = new HumanDef();
            d.Head = new[]
            {
                "............",
                "....hhhhh...",
                "...hhjhhhh..",
                "..hhjhhhhhh.",
                "..hhhhhhhhhh",
                "..hhHsssssh.",
                "..hhHsesse..",
                "..hHHssssS..",
                "..hHHHsss...",
                "...HHHHH....",
            };
            d.Body = new[]
            {
                "...kcccck...",
                "..kkcccckk..",
                "..kcccccck..",
                "..kcCddCck..",
                "..kcCddCck..",
                "..kggggggk..",
                "..kkccccKk..",
                "..Kk.cc.kK..",
                "..K......K..",
            };
            d.Map = Cv.Map('h', Pal.Lf3, 'j', Pal.Lf5, 'H', Pal.Lf2, 'k', Pal.Lf3, 'K', Pal.Lf1, 's', Pal.Sk3, 'S', Pal.Sk2, 'e', Pal.Ink0,
                'c', Pal.Ea4, 'C', Pal.Ea3, 'd', Pal.Ea6, 'g', Pal.Ea2,
                'l', Pal.Ea3, 'L', Pal.Ea1, 'b', Pal.Ea2, 'B', Pal.Ea0);
            d.Back = delegate(Cv cv, int f, int bob, bool run)
            {
                int sway = run ? ((f == 0 || f == 3) ? 1 : 0) : 0;
                // cloak
                cv.Poly(Pal.Lf2, 7, 13 + bob, 13, 13 + bob, 13, 24, 5 - sway, 24);
                cv.Poly(Pal.Lf1, 5 - sway, 20, 9, 20, 9, 24, 5 - sway, 24);
                // quiver with fletchings
                int qy = 9 + bob;
                cv.Rect(4, qy + 2, 3, 5, Pal.Ea3);
                cv.Rect(4, qy + 2, 1, 5, Pal.Ea2);
                cv.Set(4, qy, Pal.Bn3); cv.Set(4, qy + 1, Pal.Bn3);
                cv.Set(5, qy - 1, Pal.Cr3); cv.Set(5, qy, Pal.Cr3); cv.Set(5, qy + 1, Pal.Bn2);
                cv.Set(6, qy, Pal.Bn3); cv.Set(6, qy + 1, Pal.Bn3);
            };
            return d;
        }
    }
}
