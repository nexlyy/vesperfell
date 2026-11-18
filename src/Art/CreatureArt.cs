using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>Enemies and villagers.</summary>
    public static class CreatureArt
    {
        // beasts
        static Sprite WolfFrame(float phase, bool moving, int fur, int furD, int furL, int eye)
        {
            Cv cv = new Cv(40, 26);
            int by = 22;
            float[] hipX = { 10, 13, 24, 27 };
            float[] off = { 0f, 0.5f, 0.55f, 0.05f };
            for (int i = 0; i < 4; i++)
            {
                bool far = (i == 1 || i == 2);
                float a = moving ? M.Sin((phase + off[i]) * M.TAU) : 0f;
                float lift = moving ? Math.Max(0, M.Cos((phase + off[i]) * M.TAU)) * 2.5f : 0;
                float hx = hipX[i], hy = by - 9;
                float kx = hx + a * 2.2f + (i < 2 ? -1 : 1), ky = by - 5 - lift * 0.5f;
                float fx = hx + a * 6f, fy = by - lift;
                cv.Thick(hx, hy, kx, ky, 3f, 2.2f, far ? furD : fur);
                cv.Thick(kx, ky, fx, fy, 2.2f, 1.7f, far ? furD : fur);
            }
            float bob = moving ? M.Sin(phase * M.TAU * 2) * 0.8f : 0;
            // tail
            cv.Thick(8, by - 12 + bob, 2, by - 9 + bob + (moving ? M.Sin(phase * M.TAU) * 2f : 1.5f), 3f, 1.4f, furD);
            // body
            cv.Ell(11, by - 11 + bob, 5.2f, 4.4f, fur);
            cv.Ell(18, by - 11 + bob, 8f, 4.2f, fur);
            cv.Ell(25, by - 12 + bob, 5.4f, 5.2f, fur);
            // shaggy back
            for (int x = 8; x < 28; x += 2) cv.Set(x, (int)(by - 16 + bob + (x % 4 == 0 ? 0 : 1)), furL);
            // belly shade
            for (int x = 10; x < 27; x++) cv.Set(x, (int)(by - 7 + bob), furD);
            // neck + head
            cv.Thick(27, by - 14 + bob, 31, by - 15 + bob, 6f, 5f, fur);
            cv.Ell(32, by - 15.5f + bob, 3.8f, 3.2f, fur);
            cv.Thick(33, by - 14.5f + bob, 37.5f, by - 13.5f + bob, 2.8f, 1.8f, furL);
            cv.Set(38, (int)(by - 14 + bob), Pal.Ink0);
            // jaw
            cv.Thick(33, by - 12.5f + bob, 36.5f, by - 12 + bob, 1.4f, 1f, furD);
            cv.Set(35, (int)(by - 12 + bob), Pal.Bn3);
            // ears
            cv.Poly(fur, 28.5f, by - 17 + bob, 29.5f, by - 22 + bob, 31.5f, by - 18 + bob);
            cv.Poly(furD, 31, by - 18 + bob, 32.5f, by - 22 + bob, 33.5f, by - 18 + bob);
            cv.RimLight(1, 1);
            cv.Set(33, (int)(by - 17 + bob), eye); cv.Set(34, (int)(by - 17 + bob), eye);
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(19, by + 1);
        }

        public static ActorSprites Wolf(bool alpha)
        {
            ActorSprites a = new ActorSprites();
            int fur = alpha ? Pal.Ink3 : Pal.St1, furD = alpha ? Pal.Ink2 : Pal.St0, furL = alpha ? Pal.Gl1 : Pal.St2;
            int eye = Col.E(alpha ? Pal.Cr4 : Pal.Gl5);
            a.Idle = new[] { WolfFrame(0, false, fur, furD, furL, eye), WolfFrame(0.5f, false, fur, furD, furL, eye) };
            a.Run = new Sprite[4];
            for (int i = 0; i < 4; i++) a.Run[i] = WolfFrame(i / 4f, true, fur, furD, furL, eye);
            a.Height = 18; a.ShadowW = 12; a.HandX = 14; a.HandY = -12;
            return a;
        }

        static Sprite CrawlerFrame(int f)
        {
            Cv cv = new Cv(34, 24);
            int by = 21;
            float cx = 15, cy = by - 8;
            // legs
            for (int i = 0; i < 4; i++)
            {
                float t = (i - 1.5f) * 0.42f;
                float step = ((i + f) % 2 == 0) ? 1.6f : -1.2f;
                // left side (far), right side (near) in 3/4 view
                float kx = cx + 3 + t * 9 + step, ky = cy - 9 - (i % 2);
                float fx = kx + t * 5 + step, fy = by - 10 + i;
                cv.Thick(cx + 2 + t * 4, cy - 2, kx, ky, 1.6f, 1.4f, Pal.Ink3);
                cv.Thick(kx, ky, fx - 1, cy - 4, 1.4f, 1f, Pal.Ink3);
                float kx2 = cx + 3 + t * 10 - step, ky2 = cy - 5;
                float fx2 = kx2 + t * 6 - step, fy2 = by;
                cv.Thick(cx + 2 + t * 4, cy, kx2, ky2 - 2, 1.8f, 1.5f, Pal.Gl1);
                cv.Thick(kx2, ky2 - 2, fx2, fy2, 1.5f, 1.1f, Pal.Gl1);
            }
            // abdomen
            cv.Ell(cx - 5, cy - 1, 7.2f, 6f, Pal.Gl1);
            cv.Ell(cx - 6, cy - 2.5f, 5f, 3.8f, Pal.Gl2);
            cv.Ell(cx - 7, cy - 4f, 2.4f, 1.6f, Pal.Gl3);
            // venom markings
            cv.Set((int)cx - 5, (int)cy - 1, Col.E(Pal.Vn3)); cv.Set((int)cx - 7, (int)cy + 1, Col.E(Pal.Vn2)); cv.Set((int)cx - 3, (int)cy + 1, Col.E(Pal.Vn2));
            // head
            cv.Ell(cx + 5, cy, 4.2f, 3.6f, Pal.Ink3);
            cv.Ell(cx + 4.5f, cy - 1, 2.8f, 2f, Pal.Gl1);
            cv.Set((int)cx + 7, (int)cy - 1, Col.E(Pal.Vn3)); cv.Set((int)cx + 8, (int)cy, Col.E(Pal.Vn3)); cv.Set((int)cx + 6, (int)cy - 2, Col.E(Pal.Vn4));
            // fangs
            cv.Set((int)cx + 9, (int)cy + 2, Pal.Bn3); cv.Set((int)cx + 8, (int)cy + 3, Pal.Bn3);
            cv.Outline(Pal.Ink0);
            return cv.ToSprite(15, by + 1);
        }

        public static ActorSprites Crawler()
        {
            ActorSprites a = new ActorSprites();
            a.Idle = new[] { CrawlerFrame(0), CrawlerFrame(0) };
            a.Run = new[] { CrawlerFrame(0), CrawlerFrame(1), CrawlerFrame(0), CrawlerFrame(1) };
            a.Height = 16; a.ShadowW = 11; a.HandX = 10; a.HandY = -8;
            return a;
        }

        static Sprite WispFrame(int f)
        {
            Cv cv = new Cv(20, 28);
            float cx = 10, cy = 10;
            // trailing tatters
            for (int i = 0; i < 3; i++)
            {
                float x = cx + (i - 1) * 3.2f;
                float sway = M.Sin(f * 1.57f + i * 2f) * 1.6f;
                cv.Thick(x, cy + 2, x + sway, cy + 11 + (i == 1 ? 4 : 0), 3.2f, 0.8f, Col.E(Pal.Gl2));
            }
            cv.Ell(cx, cy, 6.2f, 6.6f, Col.E(Pal.Gl2));
            cv.Ell(cx, cy - 0.5f, 5f, 5.4f, Col.E(Pal.Gl3));
            cv.Ell(cx, cy - 1f, 3.6f, 3.8f, Col.E(Pal.Gl5));
            cv.Ell(cx, cy - 1.5f, 2f, 2.2f, Col.E(Pal.Gl6));
            // hollow eyes
            cv.Set(8, 9, Col.E(Pal.Ink3)); cv.Set(8, 10, Col.E(Pal.Ink3)); cv.Set(12, 9, Col.E(Pal.Ink3)); cv.Set(12, 10, Col.E(Pal.Ink3));
            cv.Set(10, 13, Col.E(Pal.Gl1));
            return cv.ToSprite(10, 27);
        }

        public static ActorSprites Wisp()
        {
            ActorSprites a = new ActorSprites();
            a.Idle = new[] { WispFrame(0), WispFrame(2) };
            a.Run = new[] { WispFrame(0), WispFrame(1), WispFrame(2), WispFrame(3) };
            a.Height = 26; a.ShadowW = 5; a.HandX = 0; a.HandY = -16;
            return a;
        }

        // humanoid foes
        public static ActorSprites Hollow(int variant)
        {
            CharacterArt.HumanDef d = new CharacterArt.HumanDef();
            d.Head = new[]
            {
                "............",
                ".....sssss..",
                "....sssssss.",
                "...ssSsssss.",
                "...sSsssssss",
                "...sSsxxsxxs",
                "...sSsxgsxgs",
                "...sSSsssss.",
                "....SSxxxs..",
                ".....SSss...",
            };
            d.Body = new[]
            {
                "...ccccccc..",
                "..cccCccccs.",
                "..ccCcccccss",
                "..cCccrcc.ss",
                "..cCccrcc..s",
                "...qqqqqqq..",
                "...ccCcccc..",
                "...cC.cCcc..",
                "...c...c.c..",
            };
            int cloth = variant == 0 ? Pal.Ea2 : (variant == 1 ? Pal.St1 : Pal.Ol1);
            d.Map = Cv.Map('s', Col.Hex(0x93A396), 'S', Col.Hex(0x6B7C74), 'x', Pal.Ink1, 'q', Pal.Ea1,
                'c', cloth, 'C', Col.Shade(cloth, -1), 'r', Pal.Cr1, 'g', Col.E(Pal.Gl5),
                'l', Col.Hex(0x6B7C74), 'L', Col.Hex(0x4A5A56), 'b', Col.Hex(0x6B7C74), 'B', Col.Hex(0x4A5A56));
            d.Map['g'] = Col.E(Pal.Gl5);
            d.HeadDX = 1; d.HeadDY = 2;
            ActorSprites a = CharacterArt.Build(d);
            a.Height = 24; a.HandX = 6; a.HandY = -10;
            return a;
        }

        public static ActorSprites Skeleton()
        {
            CharacterArt.HumanDef d = new CharacterArt.HumanDef();
            d.Head = new[]
            {
                "............",
                "....hhhhh...",
                "...hhhhhhh..",
                "..hhwwwwwwh.",
                "..hwwwwwwww.",
                "..hwwxxwxxw.",
                "..hHwxgwxgw.",
                "..hHwwwxww..",
                "...HWwxwxw..",
                "....WWwww...",
            };
            d.Body = new[]
            {
                "...hhwwhh...",
                "..hhwWwWwh..",
                "..hwxwwwxw..",
                "..h.wWwWw...",
                "..H.xwwwx...",
                "...HxWwWx...",
                "...H.www....",
                "..HH.wWw....",
                "..H..w.w....",
            };
            d.Map = Cv.Map('w', Pal.Bn3, 'W', Pal.Bn1, 'x', Pal.Ink1, 'g', Col.E(Pal.Fr3), 'h', Pal.Gl0, 'H', Pal.Ink2,
                'l', Pal.Bn2, 'L', Pal.Bn0, 'b', Pal.Bn2, 'B', Pal.Bn0);
            d.Map['g'] = Col.E(Pal.Fr3);
            ActorSprites a = CharacterArt.Build(d);
            a.HandX = 6; a.HandY = -11;
            return a;
        }

        public static ActorSprites Acolyte()
        {
            CharacterArt.HumanDef d = new CharacterArt.HumanDef();
            d.Head = new[]
            {
                ".....hh.....",
                "....hhhh....",
                "...hhhhhh...",
                "..hhjhhhhh..",
                "..hhmmmmmmh.",
                "..hhmmmmmmh.",
                "..hHmgmmgmh.",
                "..hHmmmxmmH.",
                "..hHHmmxmH..",
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
            d.Map = Cv.Map('h', Pal.Cr0, 'j', Pal.Cr1, 'H', Pal.Ink2, 'm', Pal.Bn2, 'x', Pal.Ink1, 'g', Col.E(Pal.Gl4),
                'c', Pal.Cr0, 'C', Pal.Ink2, 't', Pal.Gl1, 'a', Pal.Go1);
            ActorSprites a = CharacterArt.Build(d);
            a.HandX = 6; a.HandY = -10;
            return a;
        }

        /// <summary>Elite: a barrow-king's champion in horned iron.</summary>
        public static ActorSprites BarrowKnight()
        {
            ActorSprites a = new ActorSprites();
            string[] art =
            {
                "....n...............n...",
                "...nn...............nn..",
                "...nm...mmmmmmm.....mn..",
                "...nmm.mmnnmmmmmm..mmn..",
                "....nmmmnnmmmmmmmmmmn...",
                ".....mmmnmmmmmmmmmmm....",
                "......mmmmmmmmmmmmm.....",
                "......mmMggggggggMm.....",
                "......mmMMxxgxxgMMm.....",
                "......mMMmmmmmmmMMm.....",
                "......MMMMmmMmmMMM......",
                "....nnmmMMMMMMMMmmnn....",
                "...nmmmmmkkkkkkmmmmmM...",
                "..nmmmmmMkkrrkkMmmmmmM..",
                "..mmmmmMMkkrrkkMMmmmmM..",
                "..mmMMMMckkkkkkcMMMMmM..",
                "..mMM.ccckkrrkkccc.MMM..",
                "..mmM.cccqqqqqqccc.mmM..",
                "..mmM.cCckkkkkkcCc.mmM..",
                "...mM.cCckkrrkkcCc.mM...",
                "...ss.cCCkkkkkkCCc.ss...",
                "......cCCkkkkkkCCc......",
                "......cCCmmM.mmMCc......",
                "......CC.mmM.mmM.C......",
                ".....CC..mmM.mmM..C.....",
                ".........mmM.mmM........",
                ".........mMM.mMM........",
                "........mmMM.mmMM.......",
                "........bbbb.bbbbb......",
            };
            int[] map = Cv.Map('m', Pal.St1, 'M', Pal.St0, 'n', Pal.St3, 'g', Col.E(Pal.Fr3), 'x', Pal.Ink0,
                'k', Pal.Ink3, 'r', Pal.Gl1, 'c', Pal.Gl0, 'C', Pal.Ink2, 's', Col.Hex(0x93A396), 'b', Pal.Ink3, 'q', Pal.Go1);
            map['g'] = Col.E(Pal.Fr3);
            a.Idle = new Sprite[2]; a.Run = new Sprite[4];
            for (int f = 0; f < 6; f++)
            {
                Cv cv = new Cv(28, 34);
                bool run = f >= 2; int rf = f - 2;
                int bob = run ? ((rf & 1) == 1 ? -1 : 0) : (f == 1 ? 1 : 0);
                // legs (rows 22..28) drawn unbobbed, upper body bobbed
                string[] upper = new string[22], lower = new string[7];
                Array.Copy(art, 0, upper, 0, 22); Array.Copy(art, 22, lower, 0, 7);
                int legShift = run ? (rf == 0 ? 1 : (rf == 2 ? -1 : 0)) : 0;
                cv.Ascii(2 + legShift, 4 + 22, lower, map);
                if (run && (rf == 0 || rf == 2)) { cv.Rect(11 - legShift * 2, 31, 4, 1, Pal.Ink3); }
                cv.Ascii(2, 4 + bob, upper, map);
                cv.Outline(Pal.Ink0);
                Sprite s = cv.ToSprite(14, 33);
                if (run) a.Run[rf] = s; else a.Idle[f] = s;
            }
            a.Height = 32; a.ShadowW = 12; a.HandX = 9; a.HandY = -13;
            return a;
        }

        /// <summary>Boss: the Silent Prior, his head sealed inside a bronze bell.</summary>
        public static ActorSprites Prior()
        {
            ActorSprites a = new ActorSprites();
            string[] art =
            {
                "..............oo..............",
                ".............oOOo.............",
                "............oOooOo............",
                "...........ooOOOOoo...........",
                "..........oOOOooOOOo..........",
                "..........oOOOOOOOOd..........",
                ".........oOOOOOOOOOOd.........",
                ".........oOOxxxxxxOOd.........",
                ".........oOxxgxxgxxOd.........",
                "........oOOOxxxxxxOOOd........",
                "........oOOOOOOOOOOOOd........",
                ".......oOOOOOOOOOOOOOdd.......",
                "......oooOOOOOOOOOOOdddd......",
                ".......rrrrrrrrrrrrrrrr.......",
                "......rrRrrrrrttrrrrrRrr......",
                ".....rrrRrrrrttttrrrrRrrr.....",
                "....rrrrRrrrrtggtrrrrRrrrr....",
                "....rrrRRrrrrttttrrrrRRrrr....",
                "...rrrrR.rrrrrttrrrrr.Rrrrr...",
                "...rrrR..rrrrrrrrrrrr..Rrrr...",
                "...rrR...rrrRrrrrRrrr...Rrr...",
                "...ww....rrrRrrrrRrrr....ww...",
                "...ww....rrrRrrrrRrrr....ww...",
                ".........rrrRrrrrRrrr.........",
                "........rrrrRrrrrRrrrr........",
                "........rrrRRrrrrRRrrr........",
                "........rrrRrrrrrrRrrr........",
                ".......rrrrRrrrrrrRrrrr.......",
                ".......rrrRRrrrrrrRRrrr.......",
                ".......rrrRrrrrrrrrRrrr.......",
                "......rrrrRrrrrrrrrRrrrr......",
                "......rrrRRrrrrrrrrRRrrr......",
                "......rrrRrrrrrrrrrrRrrr......",
                ".....rrrrRrrrrrrrrrrRrrrr.....",
                ".....rrRRrrrRrrrrRrrrRRrr.....",
                ".....rR.rrrR.rrrr.Rrrr.Rr.....",
                ".....r...rR...rr...Rr...r.....",
                ".........r....r.....r.........",
            };
            int[] map = Cv.Map('o', Pal.Go2, 'O', Pal.Go1, 'd', Pal.Go0, 'x', Pal.Ink0, 'g', Col.E(Pal.Gl5),
                'r', Pal.Ink2, 'R', Pal.Ink1, 't', Pal.Gl1, 'w', Col.Hex(0x93A396));
            map['g'] = Col.E(Pal.Gl5);
            a.Idle = new Sprite[2]; a.Run = new Sprite[4];
            for (int f = 0; f < 6; f++)
            {
                Cv cv = new Cv(34, 46);
                bool run = f >= 2; int rf = f - 2;
                int bob = run ? ((rf & 1) == 1 ? -1 : 0) : (f == 1 ? 1 : 0);
                cv.Ascii(2, 3 + bob, art, map);
                // verdigris streaks on the bell
                cv.Set(13, 9 + bob, Pal.Lf2); cv.Set(13, 10 + bob, Pal.Lf2); cv.Set(20, 14 + bob, Pal.Lf2); cv.Set(11, 14 + bob, Pal.Lf3);
                // ragged hem flutter
                int sway = (f % 2 == 0) ? 0 : 1;
                for (int x = 6; x < 28; x += 3) cv.Set(x + sway, 41 + bob, Pal.Ink2);
                cv.Outline(Pal.Ink0);
                Sprite s = cv.ToSprite(17, 44);
                if (run) a.Run[rf] = s; else a.Idle[f] = s;
            }
            a.Height = 44; a.ShadowW = 14; a.HandX = 12; a.HandY = -20;
            return a;
        }

        // villagers
        static readonly string[] HeadHair =
        {
            "............",
            "....hhhhh...",
            "...hhhhhhh..",
            "..hhjhhhhhh.",
            "..hhhhhsssh.",
            "..hhHssssss.",
            "..hhHsesse..",
            "..hHHssssS..",
            "...HHssss...",
            "....Sss.....",
        };
        static readonly string[] HeadBaldBeard =
        {
            "............",
            "....sssss...",
            "...sssssss..",
            "..ssSssssss.",
            "..sSsssssss.",
            "..sSsssssss.",
            "..sSssesse..",
            "..sSbbbbbb..",
            "...bbbbbbb..",
            "....bbbbb...",
        };
        static readonly string[] HeadBun =
        {
            "............",
            ".hh.hhhhh...",
            "hhhhhhhhhh..",
            ".hhhjhhhhhh.",
            "..hhhhsssss.",
            "..hhHssssss.",
            "..hhHsesse..",
            "..hHHssssS..",
            "...HHssss...",
            "....Sss.....",
        };
        static readonly string[] HeadCap =
        {
            "............",
            "....ppppp...",
            "...ppppppp..",
            "..pppppppppp",
            "..hhsssssss.",
            "..hhHssssss.",
            "..hhHsesse..",
            "..hHHsssbS..",
            "...HHsbbb...",
            "....Sss.....",
        };
        static readonly string[] HeadHood =
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
        static readonly string[] HeadOldBeard =
        {
            "............",
            "....hhhhh...",
            "...hhhhhhh..",
            "..hhjhhhhhh.",
            "..hhhssssss.",
            "..hhHssssss.",
            "..hhHsesse..",
            "..hHbbbbbb..",
            "...bbbbbbb..",
            "....bbbbb...",
        };
        static readonly string[] BodyTunic =
        {
            "...cccccc...",
            "..cccccccc..",
            "..cCccccCc..",
            "..cCccccCc..",
            "..sCccccCs..",
            "...gggggg...",
            "...cccccc...",
            "...cCccCc...",
            "...cc..cc...",
        };
        static readonly string[] BodyApron =
        {
            "...cccccc...",
            "..ccaaaacc..",
            "..sCaaaaCs..",
            "..sCaaaaCs..",
            "..s.aaaa.s..",
            "...gaaaag...",
            "...caaaac...",
            "...cAaaAc...",
            "...cc..cc...",
        };
        static readonly string[] BodyRobe =
        {
            "...cccccc...",
            "..ccccccccc.",
            "..ccCcccccc.",
            "..ccCcccCcc.",
            "..aaaaaaaas.",
            "..ccCcccccc.",
            "..ccccccccc.",
            ".cccccccccc.",
            ".cccCccCccc.",
        };
        static readonly string[] BodyDress =
        {
            "...cccccc...",
            "..cccccccc..",
            "..cCaaaaCc..",
            "..cCaaaaCc..",
            "..sCaaaaCs..",
            "...gggggg...",
            "..cccccccc..",
            "..cCccccCc..",
            ".ccCccccCcc.",
        };

        static ActorSprites Npc(string[] head, string[] body, bool robe, int hair, int skin, int cloth, int accent, int extra)
        {
            CharacterArt.HumanDef d = new CharacterArt.HumanDef();
            d.Head = head; d.Body = body; d.Robe = robe;
            d.Map = Cv.Map('h', hair, 'j', Col.Shade(hair, 1), 'H', Col.Shade(hair, -1), 's', skin, 'S', Col.Shade(skin, -1), 'e', Pal.Ink1,
                'c', cloth, 'C', Col.Shade(cloth, -1), 'a', accent, 'A', Col.Shade(accent, -1), 'g', Pal.Ea2, 'b', extra, 'p', extra, 't', cloth,
                'l', Pal.Ea3, 'L', Pal.Ea1);
            d.Map['b'] = extra;
            d.Map['B'] = Pal.Ea0;
            // boots share the letter b/B in the leg frames; keep them dark leather
            ActorSprites a = CharacterArt.Build(BootFix(d, extra));
            return a;
        }

        // The leg frames use 'b'/'B' for boots while heads use 'b' for beards. Build legs with a boot colour by
        // temporarily remapping: we render with beard colour, then recolour boot rows.
        static CharacterArt.HumanDef BootFix(CharacterArt.HumanDef d, int beard)
        {
            CharacterArt.LayerFn prev = d.Front;
            d.Front = delegate(Cv cv, int f, int bob, bool run)
            {
                if (prev != null) prev(cv, f, bob, run);
                if (d.Robe) return;
                for (int y = 25; y < CharacterArt.FH; y++)
                    for (int x = 0; x < CharacterArt.FW; x++)
                    {
                        int c = cv.Get(x, y);
                        if (c == beard) cv.Set(x, y, Pal.Ea2);
                        else if (c == Pal.Ea0) cv.Set(x, y, Pal.Ea1);
                    }
            };
            return d;
        }

        public static ActorSprites Elder() { return Npc(HeadBun, BodyRobe, true, Pal.St5, Pal.Sk3, Pal.Lf1, Pal.Go2, Pal.St5); }
        public static ActorSprites Smith() { return Npc(HeadBaldBeard, BodyApron, false, Pal.Ea2, Pal.Sk2, Pal.St1, Pal.Ea3, Pal.Ea2); }
        public static ActorSprites Herbalist() { return Npc(HeadHood, BodyDress, false, Pal.Ol2, Pal.Sk3, Pal.Ol1, Pal.Bn2, Pal.Ea2); }
        public static ActorSprites Innkeeper() { return Npc(HeadCap, BodyApron, false, Pal.Ea3, Pal.Sk3, Pal.Em1, Pal.Bn2, Pal.Ea3); }
        public static ActorSprites Warden() { return Npc(HeadOldBeard, BodyTunic, false, Pal.St4, Pal.Sk2, Pal.Cr1, Pal.Go2, Pal.St5); }
        public static ActorSprites VillagerA() { return Npc(HeadCap, BodyTunic, false, Pal.Ea2, Pal.Sk3, Pal.Ol1, Pal.Go2, Pal.St2); }
        public static ActorSprites VillagerB() { return Npc(HeadBun, BodyDress, false, Pal.Em1, Pal.Sk4, Pal.St2, Pal.Bn2, Pal.Em1); }

        public static ActorSprites Child()
        {
            CharacterArt.HumanDef d = new CharacterArt.HumanDef();
            d.Head = new[]
            {
                "............",
                "....hhhhh...",
                "...hhhhhhh..",
                "..hhjhhhhhh.",
                "..hhhhsssss.",
                "..hhHsesses.",
                "..hHHssssS..",
                "...HHssss...",
            };
            d.Body = new[]
            {
                "............",
                "............",
                "............",
                "...cccccc...",
                "..cCccccCc..",
                "..sCccccCs..",
                "...gggggg...",
                "...cccccc...",
                "...cc..cc...",
            };
            d.HeadDY = 5;
            d.Map = Cv.Map('h', Pal.Em2, 'j', Pal.Em3, 'H', Pal.Em1, 's', Pal.Sk4, 'S', Pal.Sk3, 'e', Pal.Ink1,
                'c', Pal.Bn1, 'C', Pal.Bn0, 'g', Pal.Ea2, 'l', Pal.Ea3, 'L', Pal.Ea1, 'b', Pal.Ea2, 'B', Pal.Ea1);
            ActorSprites a = CharacterArt.Build(d);
            a.Height = 19;
            return a;
        }

        /// <summary>Sister Ilse – a translucent, softly glowing spirit.</summary>
        public static ActorSprites Ghost()
        {
            CharacterArt.HumanDef d = new CharacterArt.HumanDef();
            d.Head = HeadHood; d.Body = BodyRobe; d.Robe = true;
            int a = Col.E(Pal.Gl5), b = Col.E(Pal.Gl4), c = Col.E(Pal.Gl3), w = Col.E(Pal.Gl6);
            d.Map = Cv.Map('h', b, 'j', a, 'H', c, 's', w, 'S', a, 'e', Col.E(Pal.Gl1), 'c', b, 'C', c, 'a', a, 't', a);
            d.Outline = Col.E(Pal.Gl2);
            ActorSprites s = CharacterArt.Build(d);
            // fade the hem into nothing
            Sprite[][] sets = { s.Idle, s.Run };
            foreach (Sprite[] set in sets)
                foreach (Sprite sp in set)
                    for (int y = 0; y < sp.H; y++)
                        for (int x = 0; x < sp.W; x++)
                        {
                            int px = sp.Px[y * sp.W + x];
                            if (((px >> 24) & 255) == 0) continue;
                            int al = y > 20 ? Math.Max(0, 200 - (y - 20) * 26) : 200;
                            if (y > 20 && ((x + y) & 1) == 0) al = al * 2 / 3;
                            sp.Px[y * sp.W + x] = Col.WithA(px, Math.Max(0, Math.Min(253, al)));
                        }
            return s;
        }
    }
}
