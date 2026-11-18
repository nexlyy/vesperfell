using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Vesperfell
{
    /// <summary>One corner of an interface quad as the graphics card reads it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct UiVert
    {
        public float X, Y, U, V;                  // physical interface pixels; atlas coordinates
        public uint Col;                          // premultiplied colour, red in the low byte
        public float Mode;                        // 0 textured, 1 tinted sprite, 2..7 shapes, 8 image
        public float A0, A1, A2, A3, B0, B1, B2, B3;   // shape parameters / tint
        public const int Size = 56;
    }

    /// <summary>
    /// The interface of one frame written down as quads for the graphics card instead of being drawn pixel by pixel:
    /// rectangles and gradients, glyphs and sprites out of a shared atlas, small images, and anti-aliased shapes that the
    /// shader evaluates for every pixel. The order of drawing is kept; a new segment starts where the clip or the atlas
    /// page changes. Holds no graphics objects itself: what has to reach the card is listed in Pending and Images.
    /// </summary>
    public sealed class UiRec
    {
        public const int PageSize = 2048, MaxPages = 6, ImgSize = 1024;
        const float WhiteUv = 2f / PageSize;      // centre of the white block every page keeps in its corner

        public UiVert[] V = new UiVert[4 * 4096];
        public int Quads, PW, PH;
        public int Segs;
        public int[] SegStart = new int[64], SegPage = new int[64], SegClip = new int[64 * 4];
        int cx0, cy0, cx1, cy1, curPage;
        bool clipChanged;

        public sealed class Upload { public int Page, X, Y, W, H; public int[] Px; }
        /// <summary>Atlas pieces waiting to be copied to the card (engine ARGB, premultiplied).</summary>
        public readonly List<Upload> Pending = new List<Upload>();
        /// <summary>This frame's small images (the minimap), placed in the image texture.</summary>
        public readonly List<Upload> Images = new List<Upload>();
        readonly List<Upload> imgPool = new List<Upload>();
        public int Pages;                         // atlas pages opened so far

        struct Slot { public int Page, X, Y, W, H; }
        readonly Dictionary<object, Slot> slots = new Dictionary<object, Slot>();
        readonly int[] shelfX = new int[MaxPages], shelfY = new int[MaxPages], shelfH = new int[MaxPages];
        int imgX, imgY, imgRow;
        public int AtlasWipes;

        // frame and clip
        public void Begin(int pw, int ph)
        {
            PW = pw; PH = ph; Quads = 0; Segs = 0;
            cx0 = 0; cy0 = 0; cx1 = pw; cy1 = ph; clipChanged = true;
            imgX = 0; imgY = 0; imgRow = 0;
            imgPool.AddRange(Images); Images.Clear();
            if (Pages == 0) OpenPage();
        }

        public void Clip(int x0, int y0, int x1, int y1)
        {
            if (x0 == cx0 && y0 == cy0 && x1 == cx1 && y1 == cy1) return;
            cx0 = x0; cy0 = y0; cx1 = x1; cy1 = y1; clipChanged = true;
        }

        /// <summary>Index of the first corner of a new quad. page &lt; 0: the quad samples no atlas page.</summary>
        int Next(int page)
        {
            if (Segs == 0 || clipChanged || (page >= 0 && page != curPage))
            {
                if (page >= 0) curPage = page;
                if (Segs > 0 && SegStart[Segs - 1] == Quads) Segs--;          // an empty segment is simply replaced
                if (Segs == SegStart.Length)
                {
                    Array.Resize(ref SegStart, Segs * 2); Array.Resize(ref SegPage, Segs * 2); Array.Resize(ref SegClip, Segs * 8);
                }
                SegStart[Segs] = Quads; SegPage[Segs] = curPage;
                SegClip[Segs * 4] = cx0; SegClip[Segs * 4 + 1] = cy0; SegClip[Segs * 4 + 2] = cx1; SegClip[Segs * 4 + 3] = cy1;
                Segs++; clipChanged = false;
            }
            if ((Quads + 1) * 4 > V.Length) Array.Resize(ref V, V.Length * 2);
            return (Quads++) * 4;
        }

        /// <summary>Premultiplied colour as four bytes (red first). add: the colour is added rather than laid over.</summary>
        public static uint Pm(int rgb, float a, bool add)
        {
            if (a <= 0) return 0;
            if (a > 1) a = 1;
            uint r = (uint)(((rgb >> 16) & 255) * a + 0.5f), g = (uint)(((rgb >> 8) & 255) * a + 0.5f), b = (uint)((rgb & 255) * a + 0.5f);
            uint al = add ? 0u : (uint)(a * 255f + 0.5f);
            return r | (g << 8) | (b << 16) | (al << 24);
        }

        void Corner(int i, float x, float y, float u, float v, uint col, float mode)
        {
            V[i].X = x; V[i].Y = y; V[i].U = u; V[i].V = v; V[i].Col = col; V[i].Mode = mode;
        }

        void Params(int i, float a0, float a1, float a2, float a3, float b0, float b1, float b2, float b3)
        {
            for (int k = i; k < i + 4; k++)
            {
                V[k].A0 = a0; V[k].A1 = a1; V[k].A2 = a2; V[k].A3 = a3;
                V[k].B0 = b0; V[k].B1 = b1; V[k].B2 = b2; V[k].B3 = b3;
            }
        }

        void Box(int i, float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1, uint ct, uint cb, float mode)
        {
            Corner(i, x0, y0, u0, v0, ct, mode); Corner(i + 1, x1, y0, u1, v0, ct, mode);
            Corner(i + 2, x1, y1, u1, v1, cb, mode); Corner(i + 3, x0, y1, u0, v1, cb, mode);
        }

        // what can be drawn
        public void Rect(float x, float y, float w, float h, int rgb, float a, bool add)
        {
            if (w <= 0 || h <= 0 || a <= 0) return;
            uint c = Pm(rgb, a, add);
            int i = Next(-1);
            Box(i, x, y, x + w, y + h, WhiteUv, WhiteUv, WhiteUv, WhiteUv, c, c, 0);
            Params(i, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        public void GradV(float x, float y, float w, float h, int top, int bottom)
        {
            if (w <= 0 || h <= 0) return;
            int i = Next(-1);
            Box(i, x, y, x + w, y + h, WhiteUv, WhiteUv, WhiteUv, WhiteUv, Pm(top, 1, false), Pm(bottom, 1, false), 0);
            Params(i, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        /// <summary>An anti-aliased shape covering the box x0..x1, y0..y1 (see the interface shader for the modes).</summary>
        public void Shape(int mode, float x0, float y0, float x1, float y1, int rgb, float a, bool add,
            float a0, float a1, float a2, float a3, float b0, float b1, float b2, float b3)
        {
            x0 = (float)Math.Floor(x0); y0 = (float)Math.Floor(y0); x1 = (float)Math.Ceiling(x1); y1 = (float)Math.Ceiling(y1);
            if (x1 <= x0 || y1 <= y0 || a <= 0) return;
            uint c = Pm(rgb, a, add);
            int i = Next(-1);
            Box(i, x0, y0, x1, y1, 0, 0, 0, 0, c, c, mode);
            Params(i, a0, a1, a2, a3, b0, b1, b2, b3);
        }

        /// <summary>A sprite at its own size or stretched to w by h. col multiplies it (premultiplied); tintAmt &gt; 0 pulls it toward tint.</summary>
        public void Sprite(Sprite s, float x, float y, float w, float h, bool flip, uint col, int tint, float tintAmt)
        {
            if (s == null || w <= 0 || h <= 0 || col == 0) return;
            Slot sl = SlotFor(s);
            int i = Next(sl.Page);
            float u0 = sl.X / (float)PageSize, v0 = sl.Y / (float)PageSize, u1 = (sl.X + sl.W) / (float)PageSize, v1 = (sl.Y + sl.H) / (float)PageSize;
            if (flip) { float t = u0; u0 = u1; u1 = t; }
            bool tinted = tintAmt > 0;
            Box(i, x, y, x + w, y + h, u0, v0, u1, v1, col, col, tinted ? 1 : 0);
            Params(i, ((tint >> 16) & 255) / 255f, ((tint >> 8) & 255) / 255f, (tint & 255) / 255f, Math.Min(1f, tintAmt), 0, 0, 0, 0);
        }

        /// <summary>A sprite on an arbitrary quad: corners clockwise from the sprite's top-left.</summary>
        public void SpriteQuad(Sprite s, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, uint col, int tint, float tintAmt)
        {
            if (s == null || col == 0) return;
            Slot sl = SlotFor(s);
            int i = Next(sl.Page);
            float u0 = sl.X / (float)PageSize, v0 = sl.Y / (float)PageSize, u1 = (sl.X + sl.W) / (float)PageSize, v1 = (sl.Y + sl.H) / (float)PageSize;
            float mode = tintAmt > 0 ? 1 : 0;
            Corner(i, x0, y0, u0, v0, col, mode); Corner(i + 1, x1, y1, u1, v0, col, mode);
            Corner(i + 2, x2, y2, u1, v1, col, mode); Corner(i + 3, x3, y3, u0, v1, col, mode);
            Params(i, ((tint >> 16) & 255) / 255f, ((tint >> 8) & 255) / 255f, (tint & 255) / 255f, Math.Min(1f, tintAmt), 0, 0, 0, 0);
        }

        /// <summary>One glyph of an anti-aliased font (cov: its coverage, gw by gh) in colour rgb.</summary>
        public void Glyph(object key, byte[] cov, int gw, int gh, float x, float y, int rgb)
        {
            if (gw <= 0 || gh <= 0) return;
            Slot sl;
            if (!slots.TryGetValue(key, out sl))
            {
                int[] px = new int[gw * gh];
                for (int k = 0; k < px.Length; k++) { int c = cov[k]; px[k] = (c << 24) | (c << 16) | (c << 8) | c; }
                sl = Place(key, gw, gh, px);
            }
            int i = Next(sl.Page);
            uint col = Pm(rgb, 1, false);
            Box(i, x, y, x + gw, y + gh, sl.X / (float)PageSize, sl.Y / (float)PageSize, (sl.X + gw) / (float)PageSize, (sl.Y + gh) / (float)PageSize, col, col, 0);
            Params(i, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        /// <summary>A small opaque RGB image (sw by sh) stretched smoothly over x, y, w, h.</summary>
        public void Image(int[] src, int sw, int sh, float x, float y, float w, float h) { Image(src, sw, sh, x, y, w, h, false); }

        /// <summary>masked: the source's alpha says how much shows; the edge is drawn as a smooth contour (mode 9).</summary>
        public void Image(int[] src, int sw, int sh, float x, float y, float w, float h, bool masked)
        {
            if (sw <= 0 || sh <= 0 || sw > ImgSize || sh > ImgSize) return;
            if (imgX + sw + 1 > ImgSize) { imgX = 0; imgY += imgRow + 1; imgRow = 0; }
            if (imgY + sh > ImgSize) return;                                     // out of room this frame
            Upload u = null;
            for (int k = 0; k < imgPool.Count; k++)
                if (imgPool[k].Px.Length >= sw * sh) { u = imgPool[k]; imgPool.RemoveAt(k); break; }
            if (u == null) { u = new Upload(); u.Px = new int[sw * sh]; }
            u.X = imgX; u.Y = imgY; u.W = sw; u.H = sh;
            if (masked) Array.Copy(src, u.Px, sw * sh);
            else for (int k = 0; k < sw * sh; k++) u.Px[k] = src[k] | unchecked((int)0xFF000000);
            Images.Add(u);
            float u0 = imgX / (float)ImgSize, v0 = imgY / (float)ImgSize, u1 = (imgX + sw) / (float)ImgSize, v1 = (imgY + sh) / (float)ImgSize;
            imgX += sw + 1; imgRow = Math.Max(imgRow, sh);
            int i = Next(-1);
            uint c = Pm(0xFFFFFF, 1, false);
            Box(i, x, y, x + w, y + h, u0, v0, u1, v1, c, c, masked ? 9 : 8);
            Params(i, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        // atlas
        Slot SlotFor(Sprite s)
        {
            Slot sl;
            if (slots.TryGetValue(s, out sl)) return sl;
            int[] px = new int[s.W * s.H];
            for (int k = 0; k < px.Length; k++)
            {
                int c = s.Px[k], a = (c >> 24) & 255;
                if (a == 0) continue;
                if (a >= 254) { px[k] = c | unchecked((int)0xFF000000); continue; }
                int r = ((c >> 16) & 255) * a / 255, g = ((c >> 8) & 255) * a / 255, b = (c & 255) * a / 255;
                px[k] = (a << 24) | (r << 16) | (g << 8) | b;
            }
            return Place(s, s.W, s.H, px);
        }

        Slot Place(object key, int w, int h, int[] px)
        {
            w = Math.Min(w, PageSize - 8); h = Math.Min(h, PageSize - 8);
            Slot sl = new Slot();
            int p = Pages - 1;
            if (!Fit(p, w, h, out sl.X, out sl.Y))
            {
                if (Pages < MaxPages) { OpenPage(); p = Pages - 1; }
                else { Wipe(); p = 0; }
                Fit(p, w, h, out sl.X, out sl.Y);
            }
            sl.Page = p; sl.W = w; sl.H = h;
            slots[key] = sl;
            Upload u = new Upload(); u.Page = p; u.X = sl.X; u.Y = sl.Y; u.W = w; u.H = h; u.Px = px;
            Pending.Add(u);
            return sl;
        }

        /// <summary>Shelf packing: left to right along the current shelf, a new shelf below when the row is full.</summary>
        bool Fit(int p, int w, int h, out int x, out int y)
        {
            x = 0; y = 0;
            if (shelfX[p] + w + 1 > PageSize) { shelfY[p] += shelfH[p] + 1; shelfX[p] = 0; shelfH[p] = 0; }
            if (shelfY[p] + h + 1 > PageSize) return false;
            x = shelfX[p]; y = shelfY[p];
            shelfX[p] += w + 1; shelfH[p] = Math.Max(shelfH[p], h);
            return true;
        }

        void OpenPage()
        {
            int p = Pages++;
            shelfX[p] = 6; shelfY[p] = 0; shelfH[p] = 5;                      // the white block sits at 0..4
            Upload u = new Upload(); u.Page = p; u.X = 0; u.Y = 0; u.W = 4; u.H = 4; u.Px = new int[16];
            for (int k = 0; k < 16; k++) u.Px[k] = -1;
            Pending.Add(u);
        }

        /// <summary>Every page is full: start over (what is still in use is placed again as it is drawn).</summary>
        void Wipe()
        {
            slots.Clear(); Pending.Clear(); Pages = 0; AtlasWipes++;
            OpenPage();
        }
    }
}
