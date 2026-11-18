using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Vesperfell
{
    /// <summary>
    /// A font rasterised once from fonts that ship with Windows. The base set is crisp 1-bit at interface size (it
    /// defines the layout and serves 1:1 surfaces); for the high-resolution interface each font also carries an
    /// anti-aliased copy (Hi) drawn at the window's scale, which then also does the measuring.
    /// </summary>
    public sealed class PixFont
    {
        public sealed class Glyph { public int W, H, XOff, YOff, Adv; public byte[] Bits; public byte[] Cov; }

        public const int GlyphCount = 0x460;
        public Glyph[] Glyphs = new Glyph[GlyphCount];
        public int LineH, SpaceW, Tracking;
        /// <summary>Height of the ink box of the Latin letters (LineH without the extra leading).</summary>
        public int BoxH;
        public string SourceName;
        /// <summary>The anti-aliased copy for the current interface scale (null on 1:1 surfaces), and that scale.</summary>
        public PixFont Hi;
        public int HiScale = 1;
        /// <summary>On a Hi font: how far its glyph boxes move down to sit centred where the base font's would.</summary>
        public int YAdj;
        string[] families; float size; FontStyle style; int lead;

        /// <summary>Glyph table index for a character: Latin-1, Cyrillic, and a few typographic extras.</summary>
        public static int Slot(char ch)
        {
            if (ch < GlyphCount) return ch;
            switch (ch)
            {
                case '—': case '–': return 0x45C;      // dashes
                case '…': return 0x45D;                     // ellipsis
                case '№': return 0x45E;                     // numero sign
                case '’': case '‘': return '\'';
                case '“': case '”': return '"';
            }
            return '?';
        }

        static char SlotChar(int slot)
        {
            switch (slot) { case 0x45C: return '—'; case 0x45D: return '…'; case 0x45E: return '№'; }
            return (char)slot;
        }

        /// <summary>Width in interface pixels (measured with the anti-aliased copy when there is one, as it is drawn).</summary>
        public int Width(string s)
        {
            if (s == null) return 0;
            s = Loc.Tr(s);
            PixFont f = Hi ?? this;
            int w = 0, n = s.Length;
            for (int i = 0; i < n; i++)
            {
                char ch = s[i];
                if (ch == '^' && i + 1 < n) { if (s[i + 1] != '^') { i++; continue; } i++; }
                Glyph g = f.Glyphs[Slot(ch)];
                w += (g == null ? f.SpaceW : g.Adv + f.Tracking);
            }
            return Hi != null ? (w + HiScale - 1) / HiScale : w;
        }

        /// <summary>Greedy word wrap. Colour codes survive across line breaks.</summary>
        public List<string> Wrap(string text, int maxW)
        {
            List<string> lines = new List<string>();
            if (text == null) return lines;
            text = Loc.Tr(text);
            string[] paras = text.Split('\n');
            string activeCode = "";
            for (int p = 0; p < paras.Length; p++)
            {
                string[] words = paras[p].Split(' ');
                StringBuilder cur = new StringBuilder(activeCode);
                int curW = 0;
                bool empty = true;
                for (int i = 0; i < words.Length; i++)
                {
                    string w = words[i];
                    int ww = Width(w);
                    int add = empty ? ww : ww + SpaceAdv();
                    if (!empty && curW + add > maxW)
                    {
                        lines.Add(cur.ToString());
                        cur = new StringBuilder(activeCode);
                        curW = 0; empty = true; add = ww;
                    }
                    if (!empty) cur.Append(' ');
                    cur.Append(w);
                    curW += add; empty = false;
                    // track the last colour code seen in this word
                    for (int k = 0; k + 1 < w.Length; k++)
                        if (w[k] == '^' && w[k + 1] != '^') { activeCode = w[k + 1] == '0' ? "" : "^" + w[k + 1]; k++; }
                }
                lines.Add(cur.ToString());
            }
            return lines;
        }

        int SpaceAdv()
        {
            PixFont f = Hi ?? this;
            Glyph g = f.Glyphs[' '];
            int a = g == null ? f.SpaceW : g.Adv + f.Tracking;
            return Hi != null ? (a + HiScale - 1) / HiScale : a;
        }

        public int WrappedHeight(string text, int maxW, int lineGap)
        {
            return Wrap(text, maxW).Count * (LineH + lineGap);
        }

        // generation
        public static PixFont FromSystem(string[] families, float px, FontStyle style, int tracking, int extraLead)
        {
            PixFont pf = Rasterize(families, px, style, tracking, extraLead, false);
            pf.families = families; pf.size = px; pf.style = style; pf.lead = extraLead;
            return pf;
        }

        /// <summary>Builds (or drops, for scale 1) the anti-aliased copy for an interface drawn s times larger.</summary>
        public void MakeHi(int s)
        {
            if (s <= 1) { Hi = null; HiScale = 1; return; }
            PixFont h = families != null
                ? Rasterize(families, size * s, style, Tracking * s, 0, true)
                : Rasterize(new[] { "Tahoma", "Verdana", "Segoe UI" }, 7f * s, FontStyle.Bold, 0, 0, true);    // the tiny digits
            h.YAdj = (BoxH * s - h.BoxH) / 2;
            Hi = h; HiScale = s;
        }

        static PixFont Rasterize(string[] families, float px, FontStyle style, int tracking, int extraLead, bool aa)
        {
            PixFont pf = new PixFont();
            pf.Tracking = tracking;
            Font font = null;
            for (int i = 0; i < families.Length && font == null; i++)
            {
                try
                {
                    Font f = new Font(families[i], px, style, GraphicsUnit.Pixel);
                    if (string.Equals(f.Name, families[i], StringComparison.OrdinalIgnoreCase)) font = f; else f.Dispose();
                }
                catch { }
            }
            if (font == null) font = new Font(FontFamily.GenericSerif, px, style, GraphicsUnit.Pixel);
            pf.SourceName = font.Name;

            int cw = (int)(px * 3) + 24, chh = (int)(px * 2.5f) + 16, ox = 10, oy = 6;
            int top = int.MaxValue, bottom = int.MinValue;
            using (Bitmap bmp = new Bitmap(cw, chh, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            using (SolidBrush white = new SolidBrush(Color.White))
            using (StringFormat fmt = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoClip;
                g.TextRenderingHint = aa ? TextRenderingHint.AntiAliasGridFit : TextRenderingHint.SingleBitPerPixelGridFit;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                int[] buf = new int[cw * chh];
                int cut = aa ? 0 : 127;
                for (int code = 32; code < GlyphCount; code++)
                {
                    if (code >= 127 && code < 161) continue;
                    if (code >= 256 && code != 0x401 && code != 0x451 && !(code >= 0x410 && code <= 0x44F) && !(code >= 0x45C && code <= 0x45E)) continue;
                    string s = SlotChar(code).ToString();
                    g.Clear(Color.Black);
                    g.DrawString(s, font, white, ox, oy, fmt);
                    float adv = g.MeasureString(s, font, 9999, fmt).Width;
                    BitmapData bd = bmp.LockBits(new Rectangle(0, 0, cw, chh), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    Marshal.Copy(bd.Scan0, buf, 0, buf.Length);
                    bmp.UnlockBits(bd);

                    int x0 = cw, y0 = chh, x1 = -1, y1 = -1;
                    for (int y = 0; y < chh; y++)
                    {
                        int row = y * cw;
                        for (int x = 0; x < cw; x++)
                            if (((buf[row + x] >> 8) & 0xFF) > cut)
                            {
                                if (x < x0) x0 = x; if (x > x1) x1 = x;
                                if (y < y0) y0 = y; if (y > y1) y1 = y;
                            }
                    }
                    Glyph gl = new Glyph();
                    gl.Adv = Math.Max(1, (int)Math.Round(adv));
                    if (x1 >= x0)
                    {
                        gl.W = x1 - x0 + 1; gl.H = y1 - y0 + 1;
                        gl.XOff = x0 - ox; gl.YOff = y0 - oy;
                        if (aa)
                        {
                            gl.Cov = new byte[gl.W * gl.H];
                            for (int y = 0; y < gl.H; y++)
                                for (int x = 0; x < gl.W; x++)
                                    gl.Cov[y * gl.W + x] = (byte)((buf[(y0 + y) * cw + x0 + x] >> 8) & 0xFF);
                        }
                        else
                        {
                            gl.Bits = new byte[gl.W * gl.H];
                            for (int y = 0; y < gl.H; y++)
                                for (int x = 0; x < gl.W; x++)
                                    gl.Bits[y * gl.W + x] = (byte)(((buf[(y0 + y) * cw + x0 + x] >> 8) & 0xFF) > 127 ? 1 : 0);
                        }
                        if (!aa)
                        {
                            if (gl.XOff + gl.W + 1 > gl.Adv) gl.Adv = gl.XOff + gl.W + 1;
                            if (gl.XOff < 0) { gl.Adv -= gl.XOff; gl.XOff = 0; }
                        }
                        if (code < 127)
                        {
                            if (gl.YOff < top) top = gl.YOff;
                            if (gl.YOff + gl.H > bottom) bottom = gl.YOff + gl.H;
                        }
                    }
                    pf.Glyphs[code] = gl;
                }
            }
            if (top == int.MaxValue) { top = 0; bottom = (int)px; }
            for (int i = 0; i < GlyphCount; i++) if (pf.Glyphs[i] != null) pf.Glyphs[i].YOff -= top;
            pf.BoxH = bottom - top + 1;
            pf.LineH = pf.BoxH + extraLead;
            pf.SpaceW = pf.Glyphs[' '] != null ? pf.Glyphs[' '].Adv : (int)(px / 3);
            font.Dispose();
            return pf;
        }

        /// <summary>Tiny hand-made 3x5 digit font used for stack counts and cooldown numbers.</summary>
        public static PixFont Tiny()
        {
            PixFont pf = new PixFont();
            pf.SourceName = "tiny";
            string[] rows =
            {
                // 0    1    2    3    4    5    6    7    8    9
                "###..#..###.###.#.#.###.###.###.###.###.",
                "#.#.##....#...#.#.#.#...#.....#.#.#.#.#.",
                "#.#..#..###.###.###.###.###...#.###.###.",
                "#.#..#..#.....#...#...#.#.#...#.#.#...#.",
                "###.###.###.###...#.###.###...#.###.###.",
            };
            for (int d = 0; d < 10; d++)
            {
                Glyph g = new Glyph(); g.W = 3; g.H = 5; g.Adv = 4; g.Bits = new byte[15];
                for (int y = 0; y < 5; y++) for (int x = 0; x < 3; x++) g.Bits[y * 3 + x] = (byte)(rows[y][d * 4 + x] == '#' ? 1 : 0);
                pf.Glyphs['0' + d] = g;
            }
            AddTiny(pf, 'x', new[] { "...", "#.#", ".#.", "#.#", "..." });
            AddTiny(pf, '+', new[] { "...", ".#.", "###", ".#.", "..." });
            AddTiny(pf, '-', new[] { "...", "...", "###", "...", "..." });
            AddTiny(pf, '.', new[] { "...", "...", "...", "...", ".#." });
            AddTiny(pf, '/', new[] { "..#", "..#", ".#.", "#..", "#.." });
            AddTiny(pf, '%', new[] { "#.#", "..#", ".#.", "#..", "#.#" });
            AddTiny(pf, 's', new[] { "...", ".##", ".#.", "##.", "..." });
            Glyph sp = new Glyph(); sp.Adv = 3; pf.Glyphs[' '] = sp;
            pf.LineH = 6; pf.SpaceW = 3; pf.BoxH = 5;
            return pf;
        }

        static void AddTiny(PixFont pf, char ch, string[] rows)
        {
            Glyph g = new Glyph(); g.W = 3; g.H = 5; g.Adv = 4; g.Bits = new byte[15];
            for (int y = 0; y < 5; y++) for (int x = 0; x < 3; x++) g.Bits[y * 3 + x] = (byte)(rows[y][x] == '#' ? 1 : 0);
            pf.Glyphs[ch] = g;
        }
    }

    /// <summary>The fonts used by the whole game.</summary>
    public static class Fonts
    {
        public static PixFont Small, Body, BodyBold, Title, Big, Huge, Tiny;
        static int scale = 1;
        public static long HiMs;

        public static int Scale { get { return scale; } }

        public static void Build()
        {
            string[] serif = { "Georgia", "Palatino Linotype", "Times New Roman" };
            string[] display = { "Palatino Linotype", "Georgia", "Times New Roman" };
            string[] sans = { "Tahoma", "Verdana", "Segoe UI", "Microsoft Sans Serif" };
            Tiny = PixFont.Tiny();
            Small = PixFont.FromSystem(sans, 11f, FontStyle.Regular, 0, 0);
            Body = PixFont.FromSystem(serif, 12f, FontStyle.Regular, 0, 0);
            BodyBold = PixFont.FromSystem(serif, 12f, FontStyle.Bold, 0, 0);
            Title = PixFont.FromSystem(display, 17f, FontStyle.Bold, 0, 0);
            Big = PixFont.FromSystem(display, 26f, FontStyle.Bold, 1, 0);
            Huge = PixFont.FromSystem(display, 46f, FontStyle.Bold, 2, 0);
            scale = 1;
        }

        /// <summary>Prepares the anti-aliased copies for an interface drawn s physical pixels per interface pixel.</summary>
        public static void SetScale(int s)
        {
            if (Small == null) Build();
            s = Math.Max(1, s);
            if (s == scale) return;
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            PixFont[] all = { Small, Body, BodyBold, Title, Big, Huge, Tiny };
            Parallel.ForEach(all, delegate(PixFont f) { f.MakeHi(s); });
            scale = s;
            HiMs = sw.ElapsedMilliseconds;
            Log.Line("Interface fonts drawn at x" + s + " in " + HiMs + " ms");
        }
    }
}
