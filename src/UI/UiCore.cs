using System;
using System.Collections.Generic;

namespace Vesperfell
{
    public enum Overlay { None, Pause, Settings, Window, Dialogue, Death, Ending, Text }

    /// <summary>
    /// Immediate-mode interface toolkit. Every screen is written once and run twice per frame: a logic pass during
    /// Update (clicks are handled, drawing goes to a scratch surface) and a draw pass during Render.
    /// </summary>
    public sealed partial class Game
    {
        public Overlay Ov; Overlay settingsReturn = Overlay.None;
        public int WinTab;
        Surface G;                                   // current UI target
        bool uiInput;                                // true during the logic pass
        readonly Surface uiScratch = new Surface(W, H);
        public readonly Dictionary<string, int[]> UiRects = new Dictionary<string, int[]>();
        List<string> tipLines; int tipW;
        string hotNow = "", hotLast = "";
        string focusField = "";                      // id of the text field that has the keyboard
        public bool chatOpen; string chatText = "";

        public const int CInk = 0x0B0A12, CPanel = 0x15121F, CPanel2 = 0x1F1B33, CLine = 0x2E2843, CBronze = 0x7A5A2C, CGold = 0xD2AC52,
            CGoldHi = 0xEED58A, CText = 0xD9D2BE, CDim = 0x8A8A9A, CWhite = 0xF2ECD8, CSlot = 0x0E0C16;

        // passes
        void UiLogic(Action screen)
        {
            G = uiScratch; uiInput = true; hotNow = "";
            UiRects.Clear();
            screen();
            uiInput = false;
            if (hotNow != hotLast) { if (hotNow.Length > 0) Snd(Sfx.UiHover, 0.35f, 1f); hotLast = hotNow; }
        }

        void UiDraw(Action screen)
        {
            G = Final; uiInput = false; tipLines = null;
            screen();
            DrawTooltip();
        }

        public bool Hot(int x, int y, int w, int h) { return In.MX >= x && In.MX < x + w && In.MY >= y && In.MY < y + h; }

        bool Clicked(int x, int y, int w, int h, int button)
        {
            if (!uiInput || !Hot(x, y, w, h) || !In.MPressed[button]) return false;
            In.MPressed[button] = false;
            return true;
        }

        void Remember(string id, int x, int y, int w, int h) { if (uiInput) UiRects[id] = new[] { x, y, w, h }; }

        // chrome
        public void Panel(int x, int y, int w, int h)
        {
            G.FillA(x + 3, y + 4, w, h, 0x000000, 100);
            G.GradV(x, y, w, h, CPanel2, CPanel);
            G.Frame(x, y, w, h, CInk);
            G.Frame(x + 1, y + 1, w - 2, h - 2, CBronze);
            G.Frame(x + 2, y + 2, w - 4, h - 4, CInk);
            G.HLine(x + 3, y + 3, w - 6, CLine);
            // corner studs
            for (int k = 0; k < 4; k++)
            {
                int cx = (k & 1) == 0 ? x : x + w - 5, cy = (k & 2) == 0 ? y : y + h - 5;
                G.Fill(cx, cy, 5, 5, CInk); G.Fill(cx + 1, cy + 1, 3, 3, CGold); G.Fill(cx + 2, cy + 2, 1, 1, CInk);
            }
        }

        /// <summary>A centred title with ornamental rules either side.</summary>
        public void Header(int x, int y, int w, string title, PixFont f)
        {
            int tw = f.Width(title), cx = x + w / 2;
            G.Text(f, title, cx - tw / 2, y, CGoldHi, 0x000000);
            int ly = y + f.LineH / 2;
            int l0 = x + 10, l1 = cx - tw / 2 - 8, r0 = cx + tw / 2 + 8, r1 = x + w - 10;
            if (l1 > l0) { G.HLine(l0, ly, l1 - l0, CBronze); G.Fill(l1 - 2, ly - 1, 3, 3, CGold); }
            if (r1 > r0) { G.HLine(r0, ly, r1 - r0, CBronze); G.Fill(r0, ly - 1, 3, 3, CGold); }
        }

        public bool Button(int x, int y, int w, int h, string text, bool enabled)
        {
            return ButtonId(text, x, y, w, h, text, enabled);
        }

        public bool ButtonId(string id, int x, int y, int w, int h, string text, bool enabled)
        {
            bool hot = enabled && Hot(x, y, w, h);
            Remember(id, x, y, w, h);
            if (hot) hotNow = id + x + "," + y;
            bool down = hot && In.MDown[0];
            G.Fill(x, y, w, h, CInk);
            G.GradV(x + 1, y + 1, w - 2, h - 2, hot ? (down ? 0x2A2046 : 0x3A2E5E) : 0x262038, hot ? 0x241E36 : 0x1A1626);
            G.Frame(x + 1, y + 1, w - 2, h - 2, hot ? CGold : CBronze);
            if (hot) { G.Fill(x + 1, y + 1, 2, 2, CGoldHi); G.Fill(x + w - 3, y + 1, 2, 2, CGoldHi); G.Fill(x + 1, y + h - 3, 2, 2, CGoldHi); G.Fill(x + w - 3, y + h - 3, 2, 2, CGoldHi); }
            PixFont f = h >= 18 ? Fonts.Body : Fonts.Small;
            G.Text(f, text, x + (w - f.Width(text)) / 2, y + (h - f.LineH) / 2 + (down ? 1 : 0), enabled ? (hot ? CGoldHi : CText) : 0x5A5A6A, 0x000000);
            if (Clicked(x, y, w, h, 0))
            {
                if (enabled) { Snd(Sfx.UiClick, 0.7f, 1f); return true; }
                Snd(Sfx.UiError, 0.5f, 1f);
            }
            return false;
        }

        public bool Slider(int x, int y, int w, string label, ref float v)
        {
            PixFont f = Fonts.Small;
            int le = G.Text(f, label, x, y, CText, 0x000000);
            int bx = Math.Max(x + 78, le + 6), bw = w - (bx - x) - 30, by = y + 3;      // a longer (Russian) label pushes the bar over
            bool hot = Hot(bx - 4, y - 2, bw + 8, 14);
            Remember("slider:" + label, bx, y, bw, 12);
            G.Fill(bx, by, bw, 6, CInk); G.Fill(bx + 1, by + 1, bw - 2, 4, 0x241E36);
            int fill = (int)((bw - 2) * M.Clamp01(v));
            G.GradV(bx + 1, by + 1, fill, 4, CGoldHi, CBronze);
            int kx = bx + 1 + fill;
            G.Fill(kx - 2, by - 2, 5, 10, CInk); G.Fill(kx - 1, by - 1, 3, 8, hot ? CGoldHi : CGold);
            G.TextRight(f, ((int)Math.Round(v * 100)) + "%", x + w, y, CDim, 0x000000);
            if (uiInput && hot && In.MDown[0])
            {
                float nv = M.Clamp01((In.MX - bx - 1) / (float)(bw - 2));
                nv = (float)Math.Round(nv * 20) / 20f;
                if (Math.Abs(nv - v) > 0.001f) { v = nv; return true; }
            }
            return false;
        }

        /// <summary>A slider over lo..hi showing its value as a multiplier (mouse speed).</summary>
        public bool SliderRange(int x, int y, int w, string label, ref float v, float lo, float hi)
        {
            float t = M.Clamp01((v - lo) / (hi - lo));
            PixFont f = Fonts.Small;
            int le = G.Text(f, label, x, y, CText, 0x000000);
            int bx = Math.Max(x + 78, le + 6), bw = w - (bx - x) - 30, by = y + 3;      // a longer (Russian) label pushes the bar over
            bool hot = Hot(bx - 4, y - 2, bw + 8, 14);
            Remember("slider:" + label, bx, y, bw, 12);
            G.Fill(bx, by, bw, 6, CInk); G.Fill(bx + 1, by + 1, bw - 2, 4, 0x241E36);
            int fill = (int)((bw - 2) * t);
            G.GradV(bx + 1, by + 1, fill, 4, CGoldHi, CBronze);
            int kx = bx + 1 + fill;
            G.Fill(kx - 2, by - 2, 5, 10, CInk); G.Fill(kx - 1, by - 1, 3, 8, hot ? CGoldHi : CGold);
            G.TextRight(f, Loc.Raw(v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "x"), x + w, y, CDim, 0x000000);
            if (uiInput && hot && In.MDown[0])
            {
                float nt = M.Clamp01((In.MX - bx - 1) / (float)(bw - 2));
                float nv = (float)Math.Round((lo + (hi - lo) * nt) * 10) / 10f;
                if (Math.Abs(nv - v) > 0.001f) { v = nv; return true; }
            }
            return false;
        }

        public bool Check(int x, int y, string label, ref bool v)
        {
            PixFont f = Fonts.Small;
            int w = 14 + f.Width(label);
            bool hot = Hot(x, y - 1, w, 13);
            Remember("check:" + label, x, y, w, 12);
            if (hot) hotNow = "chk" + label;
            G.Fill(x, y, 10, 10, CInk); G.Fill(x + 1, y + 1, 8, 8, 0x241E36); G.Frame(x, y, 10, 10, hot ? CGold : CBronze);
            if (v) { G.Fill(x + 2, y + 2, 6, 6, CGoldHi); G.Fill(x + 3, y + 3, 4, 4, CGold); }
            G.Text(f, label, x + 14, y - 1, hot ? CWhite : CText, 0x000000);
            if (Clicked(x, y - 1, w, 13, 0)) { v = !v; Snd(Sfx.UiClick, 0.6f, v ? 1.1f : 0.9f); return true; }
            return false;
        }

        /// <summary>A one-line text box. Click to focus; typing, Backspace and Ctrl+V work while it has the keyboard.</summary>
        public bool TextField(string id, int x, int y, int w, ref string value, int maxLen)
        {
            if (value == null) value = "";
            PixFont f = Fonts.Small;
            bool hot = Hot(x, y, w, 16);
            Remember(id, x, y, w, 16);
            if (hot) hotNow = id;
            if (Clicked(x, y, w, 16, 0)) { focusField = id; Snd(Sfx.UiClick, 0.5f, 1.2f); }
            bool focus = focusField == id;
            bool changed = false;
            if (uiInput && focus && In.Typed.Length > 0)
            {
                string typed = In.Typed;
                In.Typed = "";
                for (int i = 0; i < typed.Length; i++)
                {
                    char c = typed[i];
                    if (c == '\b') { if (value.Length > 0) { value = value.Substring(0, value.Length - 1); changed = true; } }
                    else if (c == (char)22)
                    {
                        string clip = ClipboardGet != null ? CleanText(ClipboardGet(), maxLen) : "";
                        for (int k = 0; k < clip.Length && value.Length < maxLen; k++) { value += clip[k]; changed = true; }
                    }
                    else if (c >= ' ' && c != '^' && c != '|' && c != '=' && c != Loc.RawMark && value.Length < maxLen) { value += c; changed = true; }
                }
            }
            G.Fill(x, y, w, 16, CInk); G.Fill(x + 1, y + 1, w - 2, 14, 0x0E0C16);
            G.Frame(x, y, w, 16, focus ? CGoldHi : (hot ? CGold : CBronze));
            string shown = value;
            while (shown.Length > 0 && f.Width(Loc.Raw(shown)) > w - 12) shown = shown.Substring(1);
            G.Text(f, Loc.Raw(shown), x + 5, y + 2, CWhite, 0x000000);
            if (focus && ((int)(UiT * 2.5f) & 1) == 0) G.Fill(x + 6 + f.Width(Loc.Raw(shown)), y + 3, 1, 10, CGoldHi);
            return changed;
        }

        public void Bar(int x, int y, int w, int h, float frac, int light, int dark, int back)
        {
            G.Fill(x - 1, y - 1, w + 2, h + 2, CInk);
            G.Fill(x, y, w, h, back);
            int fw = (int)Math.Round(w * M.Clamp01(frac));
            if (fw > 0)
            {
                G.GradV(x, y, fw, h, light, dark);
                G.HLine(x, y, fw, Col.Lerp(light | Col.Opaque, Col.Opaque | 0xFFFFFF, 0.35f) & 0xFFFFFF);
            }
        }

        /// <summary>An inventory-style slot. Returns 1 for left click, 2 for right click.</summary>
        public int Slot(string id, int x, int y, Item it, bool highlight)
        {
            bool hot = Hot(x, y, 20, 20);
            Remember(id, x, y, 20, 20);
            if (hot) hotNow = id;
            G.Fill(x, y, 20, 20, CInk);
            G.Fill(x + 1, y + 1, 18, 18, CSlot);
            int border = it != null ? Items.RarityColor[(int)it.Rar] : CLine;
            if (it != null && it.Rar == Rarity.Common) border = 0x4B546D;
            G.Frame(x, y, 20, 20, hot ? CGoldHi : (highlight ? CGold : border));
            if (it != null)
            {
                if (it.Rar >= Rarity.Fine) G.FillA(x + 1, y + 1, 18, 18, Items.RarityColor[(int)it.Rar], 26);
                G.Blit(Assets.Icon[it.Icon, it.Tier], x + 2, y + 2);
                if (it.Upgrade > 0) G.Text(Fonts.Tiny, "+" + it.Upgrade, x + 2, y + 13, 0xFFE8A0, 0x000000);
                if (it.Count > 1) G.TextRight(Fonts.Tiny, it.Count.ToString(), x + 19, y + 13, 0xFFFFFF, 0x000000);
            }
            if (Clicked(x, y, 20, 20, 0)) return 1;
            if (Clicked(x, y, 20, 20, 1)) return 2;
            return 0;
        }

        // tooltips
        public void Tip(List<string> lines, int width)
        {
            if (uiInput) return;
            tipLines = lines; tipW = width;
        }

        public void Tip(string title, string body)
        {
            if (uiInput) return;
            List<string> l = new List<string>();
            l.Add("^y" + Loc.Tr(title) + "^0");
            if (body != null) l.Add(body);
            Tip(l, 190);
        }

        void DrawTooltip()
        {
            if (tipLines == null || tipLines.Count == 0) return;
            PixFont f = Fonts.Small;
            List<string> lines = new List<string>();
            int maxW = 0;
            foreach (string s in tipLines)
                foreach (string w in f.Wrap(s, tipW))
                {
                    lines.Add(w);
                    int lw = f.Width(w); if (lw > maxW) maxW = lw;
                }
            int pw = maxW + 14, ph = lines.Count * (f.LineH + 1) + 12;
            int x = In.MX + 14, y = In.MY + 10;
            if (x + pw > W - 4) x = In.MX - pw - 8;
            if (y + ph > H - 4) y = H - 4 - ph;
            if (x < 4) x = 4; if (y < 4) y = 4;
            Surface keep = G; G = Final;
            G.FillA(x + 2, y + 3, pw, ph, 0x000000, 110);
            G.Fill(x, y, pw, ph, CInk); G.Fill(x + 1, y + 1, pw - 2, ph - 2, 0x1A1626);
            G.Frame(x + 1, y + 1, pw - 2, ph - 2, CBronze);
            int ty = y + 6;
            for (int i = 0; i < lines.Count; i++) { G.Text(f, lines[i], x + 7, ty, CText, 0x000000); ty += f.LineH + 1; }
            G = keep;
            tipLines = null;
        }

        // notices and banners
        sealed class Notice { public string Text; public float T; }
        readonly List<Notice> notices = new List<Notice>();
        string bannerTitle = "", bannerSub = ""; float bannerT = 99; bool bannerDone;

        public string LastNotice = "";
        public readonly List<string> NoticeLog = new List<string>();     // recent notices, for the automated playtest

        public void Notify(string text)
        {
            LastNotice = text;
            NoticeLog.Add(text); if (NoticeLog.Count > 40) NoticeLog.RemoveAt(0);
            if (notices.Count > 0 && notices[notices.Count - 1].Text == text && notices[notices.Count - 1].T < 1f) return;
            Notice n = new Notice(); n.Text = text; notices.Add(n);
            if (notices.Count > 6) notices.RemoveAt(0);
        }

        public void QuestBanner(string title, string sub, bool done)
        {
            bannerTitle = title; bannerSub = sub; bannerT = 0; bannerDone = done;
        }

        void UpdateNotices(float dt)
        {
            for (int i = notices.Count - 1; i >= 0; i--) { notices[i].T += dt; if (notices[i].T > 5.5f) notices.RemoveAt(i); }
            bannerT += dt;
        }

        void DrawNotices()
        {
            PixFont f = Fonts.Small;
            int y = 160;            // below the zone banner
            for (int i = 0; i < notices.Count; i++)
            {
                Notice n = notices[i];
                float a = Math.Min(1f, n.T * 6f) * M.Clamp01((5.5f - n.T) * 2f);
                if (a <= 0.05f) { y += f.LineH + 3; continue; }
                int slide = (int)((1f - Math.Min(1f, n.T * 5f)) * -24);
                int w = f.Width(n.Text) + 12;
                Final.FillA(6 + slide, y - 1, w, f.LineH + 3, CInk, (int)(150 * a));
                Final.FillA(6 + slide, y - 1, 2, f.LineH + 3, CGold, (int)(255 * a));
                if (a > 0.5f) Final.Text(f, n.Text, 12 + slide, y, CWhite, 0x000000);
                y += f.LineH + 4;
            }
            if (bannerT < 3.6f)
            {
                float a = Math.Min(1f, bannerT * 4f) * M.Clamp01((3.6f - bannerT) * 2f);
                PixFont tf = Fonts.Title;
                int tw = Math.Max(tf.Width(bannerSub), Fonts.Small.Width(bannerTitle)) + 60;
                int x = W / 2 - tw / 2, by = 54;
                Final.FillA(x, by, tw, 36, CInk, (int)(190 * a));
                Final.FillA(x, by, tw, 1, bannerDone ? CGoldHi : CBronze, (int)(255 * a)); Final.FillA(x, by + 35, tw, 1, bannerDone ? CGoldHi : CBronze, (int)(255 * a));
                if (a > 0.5f)
                {
                    Final.TextCentered(Fonts.Small, Loc.Tr(bannerTitle).ToUpperInvariant(), W / 2, by + 4, bannerDone ? CGoldHi : CDim, 0x000000);
                    Final.TextCentered(tf, bannerSub, W / 2, by + 15, bannerDone ? 0xFFF8DC : CGoldHi, 0x000000);
                }
            }
        }

        // cursor
        void DrawCursor()
        {
            if (Headless && !ForceCursor) return;
            if (WantMouseLook()) return;            // the view holds the mouse: the crosshair stands in for the cursor
            bool aim = Scr == Screen.Play && Ov == Overlay.None && P != null && !P.Dead;
            Final.Blit(aim ? Assets.CursorAim : Assets.CursorArrow, In.MX, In.MY);
        }
        public bool ForceCursor;

        // overlay plumbing
        /// <summary>Alt-tabbing away pauses the game.</summary>
        public void OnFocusLost()
        {
            if (Online) return;         // a shared world does not stop
            if (Scr == Screen.Play && Ov == Overlay.None && P != null && !P.Dead && !Fading) Ov = Overlay.Pause;
        }

        public void OpenWindow(int tab)
        {
            Ov = Overlay.Window; WinTab = tab; P.Blocking = false; selQuest = null;
            Snd(Sfx.UiOpen, 0.6f, 1f);
        }

        public void CloseOverlay()
        {
            if (Ov == Overlay.None) return;
            Ov = Overlay.None; inputLockT = 0.2f;
            Snd(Sfx.UiClose, 0.6f, 1f);
        }

        string textTitle = "", textBody = "";
        public void ShowText(string title, string body)
        {
            textTitle = title; textBody = body; Ov = Overlay.Text; P.Blocking = false; textT = 0;
        }
        float textT;

        void UpdateOverlayInput()
        {
            if (Fading || P == null) return;
            if (chatOpen) { UpdateChat(); return; }
            bool esc = In.Take(Key.Esc);
            switch (Ov)
            {
                case Overlay.None:
                    if (Online && !In.Down[Key.Alt] && In.Take(Key.Enter)) { chatOpen = true; chatText = ""; In.Typed = ""; P.Blocking = false; break; }
                    if (P.Dead) break;
                    if (esc) { Ov = Overlay.Pause; Snd(Sfx.UiOpen, 0.6f, 0.9f); P.Blocking = false; }
                    else if (In.Take(Key.I) || In.Take(Key.Tab)) OpenWindow(0);
                    else if (In.Take(Key.C)) OpenWindow(1);
                    else if (In.Take(Key.K)) OpenWindow(2);
                    else if (In.Take(Key.J)) OpenWindow(3);
                    else if (In.Take(Key.M)) OpenWindow(4);
                    break;
                case Overlay.Window:
                    {
                        int[] keys = { Key.I, Key.C, Key.K, Key.J, Key.M };
                        bool close = esc || In.Take(Key.Tab);
                        for (int i = 0; i < keys.Length && !close; i++)
                            if (In.Take(keys[i])) { if (WinTab == i) close = true; else { WinTab = i; Snd(Sfx.UiTab, 0.6f, 1f); } }
                        if (close) CloseOverlay();
                    }
                    break;
                case Overlay.Pause: if (esc) CloseOverlay(); break;
                case Overlay.Settings: if (esc) { Ov = settingsReturn; SaveSettings(); Snd(Sfx.UiClose, 0.6f, 1f); } break;
                case Overlay.Dialogue: if (esc) CloseDialogue(); break;
                case Overlay.Text:
                    textT += DT;
                    if (esc || (textT > 0.2f && (In.Take(Key.F) || In.Take(Key.Space) || In.Take(Key.Enter)))) CloseOverlay();
                    break;
            }
        }

        /// <summary>The chat line: Enter sends, Esc cancels. While it is open the keyboard types and does nothing else.</summary>
        void UpdateChat()
        {
            string t = In.Typed;
            for (int i = 0; i < t.Length; i++)
            {
                char c = t[i];
                if (c == '\b') { if (chatText.Length > 0) chatText = chatText.Substring(0, chatText.Length - 1); }
                else if (c >= ' ' && c != '^' && c != Loc.RawMark && chatText.Length < 100) chatText += c;
            }
            In.Typed = "";
            if (In.Take(Key.Enter)) { NetChat(chatText); chatOpen = false; inputLockT = 0.15f; }
            else if (In.Take(Key.Esc) || !Online) chatOpen = false;
            Array.Clear(In.Pressed, 0, In.Pressed.Length);
        }

        void UpdateOverlay(float dt)
        {
            if (Ov == Overlay.None) return;
            UiLogic(OverlayUi);
        }

        void DrawOverlay()
        {
            if (Ov == Overlay.None) return;
            UiDraw(OverlayUi);
        }

        void OverlayUi()
        {
            switch (Ov)
            {
                case Overlay.Pause: PauseUi(); break;
                case Overlay.Settings: SettingsUi(); break;
                case Overlay.Window: WindowUi(); break;
                case Overlay.Dialogue: DialogueUi(); break;
                case Overlay.Death: DeathUi(); break;
                case Overlay.Ending: EndingUi(); break;
                case Overlay.Text: TextUi(); break;
            }
        }
    }
}
