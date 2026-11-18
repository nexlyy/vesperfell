using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Vesperfell
{
    /// <summary>
    /// Colour helpers. Sprite pixels are ARGB ints: alpha 0 = clear, 255 = opaque, 254 = opaque + emissive
    /// (ignores world darkness), anything else is blended.
    /// </summary>
    public static class Col
    {
        public const int Opaque = unchecked((int)0xFF000000);
        public const int Emis = unchecked((int)0xFE000000);

        public static int Hex(int rgb) { return Opaque | (rgb & 0xFFFFFF); }
        public static int Rgb(int r, int g, int b)
        {
            if (r < 0) r = 0; else if (r > 255) r = 255;
            if (g < 0) g = 0; else if (g > 255) g = 255;
            if (b < 0) b = 0; else if (b > 255) b = 255;
            return Opaque | (r << 16) | (g << 8) | b;
        }
        public static int R(int c) { return (c >> 16) & 255; }
        public static int G(int c) { return (c >> 8) & 255; }
        public static int B(int c) { return c & 255; }
        public static int A(int c) { return (c >> 24) & 255; }
        public static int WithA(int c, int a) { return (c & 0xFFFFFF) | (a << 24); }
        /// <summary>Marks a colour as emissive (stays bright in the dark).</summary>
        public static int E(int c) { return (c & 0xFFFFFF) | Emis; }

        public static int Lerp(int a, int b, float t)
        {
            if (t <= 0) return a;
            if (t >= 1) return (a & Opaque) | (b & 0xFFFFFF);
            int ar = (a >> 16) & 255, ag = (a >> 8) & 255, ab = a & 255;
            int br = (b >> 16) & 255, bg = (b >> 8) & 255, bb = b & 255;
            int r = ar + (int)((br - ar) * t + 0.5f), g = ag + (int)((bg - ag) * t + 0.5f), bl = ab + (int)((bb - ab) * t + 0.5f);
            return (a & Opaque) | (r << 16) | (g << 8) | bl;
        }

        public static int Mul(int c, float f)
        {
            int r = (int)(((c >> 16) & 255) * f), g = (int)(((c >> 8) & 255) * f), b = (int)((c & 255) * f);
            if (r > 255) r = 255; if (g > 255) g = 255; if (b > 255) b = 255;
            return (c & Opaque) | (r << 16) | (g << 8) | b;
        }

        public static void ToHsv(int c, out float h, out float s, out float v)
        {
            float r = ((c >> 16) & 255) / 255f, g = ((c >> 8) & 255) / 255f, b = (c & 255) / 255f;
            float mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            v = mx; float d = mx - mn;
            s = mx <= 0 ? 0 : d / mx;
            if (d <= 0.00001f) { h = 0; return; }
            if (mx == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (mx == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60f;
        }

        public static int FromHsv(float h, float s, float v)
        {
            h = ((h % 360f) + 360f) % 360f;
            s = M.Clamp01(s); v = M.Clamp01(v);
            float c = v * s, x = c * (1 - Math.Abs((h / 60f) % 2 - 1)), m = v - c;
            float r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            return Rgb((int)((r + m) * 255 + 0.5f), (int)((g + m) * 255 + 0.5f), (int)((b + m) * 255 + 0.5f));
        }

        /// <summary>
        /// Pixel-art style ramp step: negative = darker (hue drifts to cold violet, more saturated),
        /// positive = lighter (hue drifts to warm yellow, less saturated).
        /// </summary>
        public static int Shade(int c, int steps)
        {
            if (steps == 0) return c;
            float h, s, v; ToHsv(c, out h, out s, out v);
            int n = steps < 0 ? -steps : steps;
            for (int i = 0; i < n; i++)
            {
                if (steps < 0)
                {
                    v *= 0.78f; s = s + (1 - s) * 0.12f;
                    h = HueToward(h, 255f, 9f);
                }
                else
                {
                    v = v + (1 - v) * 0.24f; s *= 0.86f;
                    h = HueToward(h, 48f, 9f);
                }
            }
            return (c & Opaque) | (FromHsv(h, s, v) & 0xFFFFFF);
        }

        static float HueToward(float h, float target, float amt)
        {
            float d = ((target - h + 540f) % 360f) - 180f;
            if (Math.Abs(d) <= amt) return target;
            return h + (d > 0 ? amt : -amt);
        }

        public static int Gray(int v) { return Rgb(v, v, v); }
    }

    /// <summary>An immutable-ish image with real alpha and an origin (the pixel that lands on the draw position).</summary>
    public sealed class Sprite
    {
        public int W, H, OX, OY;
        public int[] Px;
        public Sprite(int w, int h) { W = w; H = h; Px = new int[w * h]; }
        public Sprite(int w, int h, int ox, int oy) { W = w; H = h; OX = ox; OY = oy; Px = new int[w * h]; }

        public Sprite Clone()
        {
            Sprite s = new Sprite(W, H, OX, OY);
            Array.Copy(Px, s.Px, Px.Length);
            return s;
        }

        public void SavePng(string path, int scale)
        {
            using (Bitmap bmp = new Bitmap(W * scale, H * scale, PixelFormat.Format32bppArgb))
            {
                BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                int[] row = new int[bmp.Width];
                for (int y = 0; y < bmp.Height; y++)
                {
                    int sy = y / scale;
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        int c = Px[sy * W + x / scale];
                        int a = (c >> 24) & 255;
                        if (a == 254) c |= Col.Opaque;
                        row[x] = c;
                    }
                    Marshal.Copy(row, 0, new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), bmp.Width);
                }
                bmp.UnlockBits(bd);
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }

    /// <summary>
    /// A render target. The alpha byte of each pixel is a flag, not coverage: 0xFF marks an emissive pixel
    /// that the lighting pass leaves at full brightness.
    /// Drawing always takes interface coordinates (W by H); a surface made with a scale S above 1 holds S by S
    /// physical pixels for each of them, so shapes get smooth edges, text is drawn from large anti-aliased fonts and
    /// sprites are enlarged smoothly instead of being shown as blocks.
    /// </summary>
    public sealed unsafe class Surface
    {
        public readonly int W, H;
        /// <summary>Physical pixels per interface pixel, and the size of the pixel buffer (W*S by H*S).</summary>
        public readonly int S, PW, PH;
        public readonly int[] Px;
        public int CX0, CY0, CX1, CY1;
        int PX0, PY0, PX1, PY1;                     // the clip in physical pixels
        // What a large surface has drawn, kept per band of rows: the interface layer is cleared and handed to the
        // graphics card only where something was drawn, not in full every frame.
        const int BandRows = 8;
        readonly int nBands, bandH;
        readonly int[] drawnX0, drawnX1, pendX0, pendX1;   // since the last clear / since the last upload
        /// <summary>Set to 0xFF000000 to make blended primitives emissive (unaffected by darkness).</summary>
        public uint EmisMask;
        /// <summary>Vertical squash of circles drawn on the ground plane; hit tests use the same factor.
        /// The 3D view shows true perspective, so there it is 1 (circles are circles).</summary>
        public static float Squash = 1.3f;
        /// <summary>How much slower things move north-south than east-west: the flat view fakes depth with it, the 3D view does not need to.</summary>
        public static float YMove = 0.86f;
        /// <summary>Alpha written by opaque drawing. 0 normally (the alpha byte is the emissive flag of the 2D scene);
        /// 0xFF000000 when this surface is the interface layer laid over the 3D view, where alpha is coverage.</summary>
        public uint OpA;
        public bool Layer { get { return OpA != 0; } set { OpA = value ? 0xFF000000 : 0; } }
        /// <summary>When set (a large interface layer over the 3D view), drawing is written down for the graphics card
        /// instead of being rasterised into Px.</summary>
        public UiRec Rec;

        public Surface(int w, int h) : this(w, h, 1) { }

        public Surface(int w, int h, int s)
        {
            W = w; H = h; S = Math.Max(1, s); PW = w * S; PH = h * S;
            Px = new int[PW * PH];
            CX1 = w; CY1 = h; PX1 = PW; PY1 = PH;
            if (S > 1)
            {
                bandH = BandRows * S; nBands = (PH + bandH - 1) / bandH;
                drawnX0 = new int[nBands]; drawnX1 = new int[nBands]; pendX0 = new int[nBands]; pendX1 = new int[nBands];
                for (int b = 0; b < nBands; b++) { drawnX0[b] = PW; drawnX1[b] = 0; pendX0[b] = 0; pendX1[b] = PW; }
            }
        }

        /// <summary>Records that the physical rectangle [x0,x1) x [y0,y1) was drawn on.</summary>
        void MarkP(int x0, int y0, int x1, int y1)
        {
            if (drawnX0 == null || x1 <= x0 || y1 <= y0) return;
            int b0 = Math.Max(0, y0 / bandH), b1 = Math.Min(nBands - 1, (y1 - 1) / bandH);
            for (int b = b0; b <= b1; b++)
            {
                if (x0 < drawnX0[b]) drawnX0[b] = x0;
                if (x1 > drawnX1[b]) drawnX1[b] = x1;
                if (x0 < pendX0[b]) pendX0[b] = x0;
                if (x1 > pendX1[b]) pendX1[b] = x1;
            }
        }
        void MarkAll() { MarkP(0, 0, PW, PH); }

        /// <summary>Physical rows per band of the change record and the number of bands (0: no record, the whole buffer counts).</summary>
        public int BandH { get { return bandH; } }
        public int Bands { get { return nBands; } }
        /// <summary>The physical x span of band b changed since the last upload (nothing when x1 &lt;= x0).</summary>
        public void Changed(int b, out int x0, out int x1) { x0 = pendX0[b]; x1 = pendX1[b]; }
        /// <summary>Forgets the changes once the buffer has been handed over.</summary>
        public void Uploaded()
        {
            if (pendX0 == null) return;
            for (int b = 0; b < nBands; b++) { pendX0[b] = PW; pendX1[b] = 0; }
        }

        public void ResetClip() { CX0 = 0; CY0 = 0; CX1 = W; CY1 = H; PX0 = 0; PY0 = 0; PX1 = PW; PY1 = PH; if (Rec != null) Rec.Clip(PX0, PY0, PX1, PY1); }
        public void SetClip(int x, int y, int w, int h)
        {
            CX0 = Math.Max(0, x); CY0 = Math.Max(0, y); CX1 = Math.Min(W, x + w); CY1 = Math.Min(H, y + h);
            if (CX1 < CX0) CX1 = CX0; if (CY1 < CY0) CY1 = CY0;
            PX0 = CX0 * S; PY0 = CY0 * S; PX1 = CX1 * S; PY1 = CY1 * S;
            if (Rec != null) Rec.Clip(PX0, PY0, PX1, PY1);
        }

        public void Clear(int rgb)
        {
            if (Rec != null) { Rec.Rect(0, 0, PW, PH, rgb & 0xFFFFFF, 1f, false); return; }
            int c = (rgb & 0xFFFFFF) | (int)OpA;
            for (int i = 0; i < Px.Length; i++) Px[i] = c;
            MarkAll();
        }

        // pixel maths
        uint Blend(uint d, uint s, uint a)
        {
            uint ia = 256 - a;
            uint rb = (((d & 0xFF00FF) * ia + (s & 0xFF00FF) * a) >> 8) & 0xFF00FF;
            uint g = (((d & 0x00FF00) * ia + (s & 0x00FF00) * a) >> 8) & 0x00FF00;
            if (OpA == 0) return (d & 0xFF000000) | rb | g;
            // interface layer: colour stays premultiplied, coverage accumulates ("over")
            uint da = d >> 24;
            uint oa = da + (((255 - da) * a) >> 8);
            if (oa > 255) oa = 255;
            return (oa << 24) | rb | g;
        }

        /// <summary>Makes the whole surface transparent (the interface layer before it is drawn).</summary>
        public void ClearTransparent()
        {
            if (Rec != null) return;                     // a recorded frame starts empty anyway
            if (drawnX0 == null) { Array.Clear(Px, 0, Px.Length); return; }
            for (int b = 0; b < nBands; b++)
            {
                int x0 = drawnX0[b], x1 = drawnX1[b];
                if (x1 <= x0) continue;
                drawnX0[b] = PW; drawnX1[b] = 0;
                if (x0 < pendX0[b]) pendX0[b] = x0;
                if (x1 > pendX1[b]) pendX1[b] = x1;
                int y0 = b * bandH, y1 = Math.Min(PH, y0 + bandH), n = x1 - x0;
                for (int y = y0; y < y1; y++) Array.Clear(Px, y * PW + x0, n);
            }
        }

        static uint AddSat(uint d, uint s, uint a)
        {
            uint r = ((d >> 16) & 255) + ((((s >> 16) & 255) * a) >> 8);
            uint g = ((d >> 8) & 255) + ((((s >> 8) & 255) * a) >> 8);
            uint b = (d & 255) + (((s & 255) * a) >> 8);
            if (r > 255) r = 255; if (g > 255) g = 255; if (b > 255) b = 255;
            return (d & 0xFF000000) | (r << 16) | (g << 8) | b;
        }

        // physical pixels (surfaces with S above 1)
        void FillP(int x, int y, int w, int h, int c)
        {
            if (x < PX0) { w -= PX0 - x; x = PX0; }
            if (y < PY0) { h -= PY0 - y; y = PY0; }
            if (x + w > PX1) w = PX1 - x;
            if (y + h > PY1) h = PY1 - y;
            if (w <= 0 || h <= 0) return;
            if (Rec != null) { Rec.Rect(x, y, w, h, c & 0xFFFFFF, 1f, false); return; }
            MarkP(x, y, x + w, y + h);
            fixed (int* dp = Px)
            {
                for (int j = 0; j < h; j++)
                {
                    int* d = dp + (y + j) * PW + x;
                    for (int i = 0; i < w; i++) d[i] = c;
                }
            }
        }

        /// <summary>Blends a physical rectangle. a256 is 0..256; mode 0 blends, 1 blends and marks emissive, 2 adds.</summary>
        void BlendP(int x, int y, int w, int h, int rgb, int a256, int mode)
        {
            if (a256 <= 0) return;
            if (x < PX0) { w -= PX0 - x; x = PX0; }
            if (y < PY0) { h -= PY0 - y; y = PY0; }
            if (x + w > PX1) w = PX1 - x;
            if (y + h > PY1) h = PY1 - y;
            if (w <= 0 || h <= 0) return;
            if (Rec != null) { Rec.Rect(x, y, w, h, rgb & 0xFFFFFF, Math.Min(256, a256) / 256f, mode == 2); return; }
            MarkP(x, y, x + w, y + h);
            uint s = (uint)rgb, aa = (uint)Math.Min(256, a256);
            fixed (int* dp = Px)
            {
                for (int j = 0; j < h; j++)
                {
                    uint* d = (uint*)(dp + (y + j) * PW + x);
                    if (mode == 2) for (int i = 0; i < w; i++) d[i] = AddSat(d[i], s, aa);
                    else if (mode == 1) for (int i = 0; i < w; i++) d[i] = Blend(d[i], s, aa) | EmisMask;
                    else for (int i = 0; i < w; i++) d[i] = Blend(d[i], s, aa);
                }
            }
        }

        /// <summary>An anti-aliased stroke between two physical points, hw pixels either side of the centre line.</summary>
        void LineP(float ax, float ay, float bx, float by, float hw, int rgb, int a, bool add)
        {
            if (a <= 0) return;
            if (a > 255) a = 255;
            int x0 = Math.Max(PX0, M.Floor(Math.Min(ax, bx) - hw - 1)), x1 = Math.Min(PX1 - 1, M.Ceil(Math.Max(ax, bx) + hw + 1));
            int y0 = Math.Max(PY0, M.Floor(Math.Min(ay, by) - hw - 1)), y1 = Math.Min(PY1 - 1, M.Ceil(Math.Max(ay, by) + hw + 1));
            float dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
            if (Rec != null) { Rec.Shape(2, x0, y0, x1 + 1, y1 + 1, rgb & 0xFFFFFF, a / 255f, add, ax, ay, bx, by, hw, 0, 0, 0); return; }
            MarkP(x0, y0, x1 + 1, y1 + 1);
            uint s = (uint)rgb;
            fixed (int* dp = Px)
            {
                for (int y = y0; y <= y1; y++)
                {
                    uint* d = (uint*)(dp + y * PW);
                    float py = y + 0.5f - ay;
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = x + 0.5f - ax;
                        float t = l2 > 1e-6f ? M.Clamp01((px * dx + py * dy) / l2) : 0;
                        float ex = px - dx * t, ey = py - dy * t;
                        float cov = hw + 0.5f - M.Sqrt(ex * ex + ey * ey);
                        if (cov <= 0) continue;
                        if (cov > 1) cov = 1;
                        uint aa = (uint)(a * cov + 0.5f);
                        if (aa == 0) continue;
                        aa += aa >> 7;
                        d[x] = add ? AddSat(d[x], s, aa) : Blend(d[x], s, aa);
                    }
                }
            }
        }

        // primitives
        public void Pixel(int x, int y, int rgb)
        {
            if (x < CX0 || y < CY0 || x >= CX1 || y >= CY1) return;
            if (S != 1) { FillP(x * S, y * S, S, S, (rgb & 0xFFFFFF) | (int)OpA); return; }
            Px[y * W + x] = (rgb & 0xFFFFFF) | (int)OpA;
        }

        public void PixelE(int x, int y, int rgb)
        {
            if (x < CX0 || y < CY0 || x >= CX1 || y >= CY1) return;
            if (S != 1) { FillP(x * S, y * S, S, S, rgb | Col.Opaque); return; }
            Px[y * W + x] = rgb | Col.Opaque;
        }

        public void PixelA(int x, int y, int rgb, int a)
        {
            if (x < CX0 || y < CY0 || x >= CX1 || y >= CY1 || a <= 0) return;
            if (a > 255) a = 255;
            if (S != 1) { BlendP(x * S, y * S, S, S, rgb, a + (a >> 7), 0); return; }
            int i = y * W + x;
            Px[i] = (int)Blend((uint)Px[i], (uint)rgb, (uint)(a + (a >> 7)));
        }

        public void PixelAdd(int x, int y, int rgb, int a)
        {
            if (x < CX0 || y < CY0 || x >= CX1 || y >= CY1 || a <= 0) return;
            if (S != 1) { BlendP(x * S, y * S, S, S, rgb, a, 2); return; }
            int i = y * W + x;
            Px[i] = (int)AddSat((uint)Px[i], (uint)rgb, (uint)a);
        }

        public void Fill(int x, int y, int w, int h, int rgb)
        {
            if (S != 1) { FillP(x * S, y * S, w * S, h * S, (rgb & 0xFFFFFF) | (int)OpA); return; }
            if (x < CX0) { w -= CX0 - x; x = CX0; }
            if (y < CY0) { h -= CY0 - y; y = CY0; }
            if (x + w > CX1) w = CX1 - x;
            if (y + h > CY1) h = CY1 - y;
            if (w <= 0 || h <= 0) return;
            int c = (rgb & 0xFFFFFF) | (int)OpA;
            fixed (int* dp = Px)
            {
                for (int j = 0; j < h; j++)
                {
                    int* d = dp + (y + j) * W + x;
                    for (int i = 0; i < w; i++) d[i] = c;
                }
            }
        }

        public void FillA(int x, int y, int w, int h, int rgb, int a)
        {
            if (a <= 0) return;
            if (a >= 255) { Fill(x, y, w, h, rgb); return; }
            if (S != 1) { BlendP(x * S, y * S, w * S, h * S, rgb, a + (a >> 7), 1); return; }
            if (x < CX0) { w -= CX0 - x; x = CX0; }
            if (y < CY0) { h -= CY0 - y; y = CY0; }
            if (x + w > CX1) w = CX1 - x;
            if (y + h > CY1) h = CY1 - y;
            if (w <= 0 || h <= 0) return;
            uint s = (uint)rgb, aa = (uint)(a + (a >> 7));
            fixed (int* dp = Px)
            {
                for (int j = 0; j < h; j++)
                {
                    uint* d = (uint*)(dp + (y + j) * W + x);
                    for (int i = 0; i < w; i++) d[i] = Blend(d[i], s, aa) | EmisMask;
                }
            }
        }

        /// <summary>Darkens (multiplies) a rectangle. f256: 256 = unchanged, 0 = black.</summary>
        public void Dim(int x, int y, int w, int h, int f256)
        {
            FillA(x, y, w, h, 0, 255 - (f256 * 255 >> 8));
        }

        public void HLine(int x, int y, int w, int rgb) { Fill(x, y, w, 1, rgb); }
        public void VLine(int x, int y, int h, int rgb) { Fill(x, y, 1, h, rgb); }
        public void Frame(int x, int y, int w, int h, int rgb)
        {
            Fill(x, y, w, 1, rgb); Fill(x, y + h - 1, w, 1, rgb);
            Fill(x, y + 1, 1, h - 2, rgb); Fill(x + w - 1, y + 1, 1, h - 2, rgb);
        }

        /// <summary>Vertical gradient fill (smooth down to the physical pixel).</summary>
        public void GradV(int x, int y, int w, int h, int top, int bottom)
        {
            if (S != 1)
            {
                if (Rec != null) { Rec.GradV(x * S, y * S, w * S, h * S, top & 0xFFFFFF, bottom & 0xFFFFFF); return; }
                int hp = h * S;
                for (int j = 0; j < hp; j++)
                    FillP(x * S, y * S + j, w * S, 1, (Col.Lerp(top, bottom, hp <= 1 ? 0 : j / (float)(hp - 1)) & 0xFFFFFF) | (int)OpA);
                return;
            }
            for (int j = 0; j < h; j++)
                Fill(x, y + j, w, 1, Col.Lerp(top, bottom, h <= 1 ? 0 : j / (float)(h - 1)));
        }

        public void Line(int x0, int y0, int x1, int y1, int rgb, int a)
        {
            if (S != 1) { LineP((x0 + 0.5f) * S, (y0 + 0.5f) * S, (x1 + 0.5f) * S, (y1 + 0.5f) * S, S * 0.5f, rgb, a, false); return; }
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            int guard = 0;
            while (guard++ < 4000)
            {
                if (a >= 255) Pixel(x0, y0, rgb); else PixelA(x0, y0, rgb, a);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public void LineAdd(int x0, int y0, int x1, int y1, int rgb, int a)
        {
            if (S != 1) { LineP((x0 + 0.5f) * S, (y0 + 0.5f) * S, (x1 + 0.5f) * S, (y1 + 0.5f) * S, S * 0.5f, rgb, a, true); return; }
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            int guard = 0;
            while (guard++ < 4000)
            {
                PixelAdd(x0, y0, rgb, a);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>Filled ellipse, alpha blended (a = 255 for solid).</summary>
        public void Ellipse(float cx, float cy, float rx, float ry, int rgb, int a)
        {
            if (rx <= 0 || ry <= 0 || a <= 0) return;
            if (S != 1)
            {
                if (a > 255) a = 255;
                float pcx = cx * S, pcy = cy * S, prx = rx * S, pry = ry * S;
                if (Rec != null)
                {
                    Rec.Shape(3, Math.Max(PX0, pcx - prx - 1), Math.Max(PY0, pcy - pry - 1), Math.Min(PX1, pcx + prx + 1), Math.Min(PY1, pcy + pry + 1),
                        rgb & 0xFFFFFF, a / 255f, false, pcx, pcy, prx, pry, 0, 0, 0, 0);
                    return;
                }
                int a256 = a + (a >> 7);
                int ya = Math.Max(PY0, M.Floor(pcy - pry)), yb = Math.Min(PY1 - 1, M.Ceil(pcy + pry));
                for (int y = ya; y <= yb; y++)
                {
                    float dy = (y + 0.5f - pcy) / pry, t = 1 - dy * dy;
                    if (t <= 0) continue;
                    float hw = prx * M.Sqrt(t), xa = pcx - hw, xb = pcx + hw;
                    int ia = M.Ceil(xa), ib = M.Floor(xb);
                    if (ib < ia) { BlendP(M.Floor(xa), y, 1, 1, rgb, (int)(a256 * (xb - xa)), 1); continue; }
                    if (ib > ia) BlendP(ia, y, ib - ia, 1, rgb, a256, 1);
                    BlendP(ia - 1, y, 1, 1, rgb, (int)(a256 * (ia - xa)), 1);
                    BlendP(ib, y, 1, 1, rgb, (int)(a256 * (xb - ib)), 1);
                }
                return;
            }
            int y0 = M.Floor(cy - ry), y1 = M.Ceil(cy + ry);
            for (int y = y0; y <= y1; y++)
            {
                float dy = (y + 0.5f - cy) / ry;
                float t = 1 - dy * dy;
                if (t <= 0) continue;
                float hw = rx * M.Sqrt(t);
                int xa = M.Round(cx - hw), xb = M.Round(cx + hw);
                if (xb > xa) FillA(xa, y, xb - xa, 1, rgb, a);
            }
        }

        /// <summary>Ring (annulus) between r0 and r1, alpha blended. Used for telegraphs and shockwaves.</summary>
        public void Ring(float cx, float cy, float r0, float r1, int rgb, int a, bool additive)
        {
            if (r1 <= 0 || a <= 0) return;
            if (r0 < 0) r0 = 0;
            if (S != 1)
            {
                if (a > 255) a = 255;
                float pcx = cx * S, pcy = cy * S, pr0 = r0 * S, pr1 = r1 * S;
                int px0 = Math.Max(PX0, M.Floor(pcx - pr1 - 1)), px1 = Math.Min(PX1 - 1, M.Ceil(pcx + pr1 + 1));
                int py0 = Math.Max(PY0, M.Floor(pcy - (pr1 + 1) / Squash)), py1 = Math.Min(PY1 - 1, M.Ceil(pcy + (pr1 + 1) / Squash));
                if (Rec != null) { Rec.Shape(4, px0, py0, px1 + 1, py1 + 1, rgb & 0xFFFFFF, a / 255f, additive, pcx, pcy, pr0, pr1, Squash, 0, 0, 0); return; }
                MarkP(px0, py0, px1 + 1, py1 + 1);
                uint ps = (uint)rgb;
                fixed (int* dp = Px)
                {
                    for (int y = py0; y <= py1; y++)
                    {
                        float dy = (y + 0.5f - pcy) * Squash;
                        uint* d = (uint*)(dp + y * PW);
                        for (int x = px0; x <= px1; x++)
                        {
                            float dx = x + 0.5f - pcx;
                            float q = M.Sqrt(dx * dx + dy * dy);
                            float cov = Math.Min(1f, pr1 + 0.5f - q);
                            if (pr0 > 0) cov = Math.Min(cov, q - pr0 + 0.5f);
                            if (cov <= 0) continue;
                            uint aa = (uint)(a * cov + 0.5f);
                            if (aa == 0) continue;
                            aa += aa >> 7;
                            d[x] = (additive ? AddSat(d[x], ps, aa) : Blend(d[x], ps, aa)) | EmisMask;
                        }
                    }
                }
                return;
            }
            int x0 = Math.Max(CX0, M.Floor(cx - r1)), x1 = Math.Min(CX1 - 1, M.Ceil(cx + r1));
            int y0 = Math.Max(CY0, M.Floor(cy - r1 / Squash)), y1 = Math.Min(CY1 - 1, M.Ceil(cy + r1 / Squash));
            float r0s = r0 * r0, r1s = r1 * r1;
            uint s = (uint)rgb, a2 = (uint)(a + (a >> 7));
            fixed (int* dp = Px)
            {
                for (int y = y0; y <= y1; y++)
                {
                    float dy = (y + 0.5f - cy) * Squash;      // ground-plane ellipse
                    uint* d = (uint*)(dp + y * W);
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - cx;
                        float q = dx * dx + dy * dy;
                        if (q < r0s || q > r1s) continue;
                        d[x] = (additive ? AddSat(d[x], s, a2) : Blend(d[x], s, a2)) | EmisMask;
                    }
                }
            }
        }

        /// <summary>Thick line on the ground plane (charge / lunge telegraphs).</summary>
        public void Strip(float ax, float ay, float bx, float by, float halfW, int rgb, int a)
        {
            if (a <= 0) return;
            if (S != 1)
            {
                if (a > 255) a = 255;
                float pax = ax * S, pay = ay * S, pbx = bx * S, pby = by * S, phw = halfW * S;
                int px0 = Math.Max(PX0, M.Floor(Math.Min(pax, pbx) - phw - 1)), px1 = Math.Min(PX1 - 1, M.Ceil(Math.Max(pax, pbx) + phw + 1));
                int py0 = Math.Max(PY0, M.Floor(Math.Min(pay, pby) - phw - 1)), py1 = Math.Min(PY1 - 1, M.Ceil(Math.Max(pay, pby) + phw + 1));
                float pdx = pbx - pax, pdy = (pby - pay) * Squash, pl2 = pdx * pdx + pdy * pdy;
                if (pl2 < 0.01f) return;
                if (Rec != null) { Rec.Shape(5, px0, py0, px1 + 1, py1 + 1, rgb & 0xFFFFFF, a / 255f, false, pax, pay, pbx, pby, phw, Squash, 0, 0); return; }
                MarkP(px0, py0, px1 + 1, py1 + 1);
                uint ps = (uint)rgb;
                fixed (int* dp = Px)
                {
                    for (int y = py0; y <= py1; y++)
                    {
                        float py = (y + 0.5f - pay) * Squash;
                        uint* d = (uint*)(dp + y * PW);
                        for (int x = px0; x <= px1; x++)
                        {
                            float px = x + 0.5f - pax;
                            float t = (px * pdx + py * pdy) / pl2;
                            if (t < 0 || t > 1) continue;
                            float ex = px - pdx * t, ey = py - pdy * t;
                            float cov = Math.Min(1f, phw + 0.5f - M.Sqrt(ex * ex + ey * ey));
                            if (cov <= 0) continue;
                            uint aa = (uint)(a * cov + 0.5f);
                            if (aa == 0) continue;
                            d[x] = Blend(d[x], ps, aa + (aa >> 7)) | EmisMask;
                        }
                    }
                }
                return;
            }
            int x0 = Math.Max(CX0, M.Floor(Math.Min(ax, bx) - halfW)), x1 = Math.Min(CX1 - 1, M.Ceil(Math.Max(ax, bx) + halfW));
            int y0 = Math.Max(CY0, M.Floor(Math.Min(ay, by) - halfW)), y1 = Math.Min(CY1 - 1, M.Ceil(Math.Max(ay, by) + halfW));
            float dx = bx - ax, dy = (by - ay) * Squash, l2 = dx * dx + dy * dy;
            if (l2 < 0.01f) return;
            uint s = (uint)rgb, a2 = (uint)(a + (a >> 7));
            float hw2 = halfW * halfW;
            fixed (int* dp = Px)
            {
                for (int y = y0; y <= y1; y++)
                {
                    float py = (y + 0.5f - ay) * Squash;
                    uint* d = (uint*)(dp + y * W);
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = x + 0.5f - ax;
                        float t = (px * dx + py * dy) / l2;
                        if (t < 0 || t > 1) continue;
                        float ex = px - dx * t, ey = py - dy * t;
                        if (ex * ex + ey * ey > hw2) continue;
                        d[x] = Blend(d[x], s, a2) | EmisMask;
                    }
                }
            }
        }

        /// <summary>Ground-plane pie slice (2:1 squashed) used for attack telegraphs.</summary>
        public void Sector(float cx, float cy, float r0, float r1, float ang, float halfArc, int rgb, int a, bool additive)
        {
            if (r1 <= 0 || a <= 0) return;
            int k = S;
            if (k != 1) { cx *= k; cy *= k; r0 *= k; r1 *= k; }
            int x0 = Math.Max(PX0, M.Floor(cx - r1)), x1 = Math.Min(PX1 - 1, M.Ceil(cx + r1));
            int y0 = Math.Max(PY0, M.Floor(cy - r1 / Squash)), y1 = Math.Min(PY1 - 1, M.Ceil(cy + r1 / Squash));
            float ax = M.Cos(ang), ay = M.Sin(ang), cosHalf = M.Cos(halfArc);
            if (a > 255) a = 255;
            if (Rec != null && k != 1)
            {
                Rec.Shape(6, x0, y0, x1 + 1, y1 + 1, rgb & 0xFFFFFF, a / 255f, additive, cx, cy, r0, r1, ax, ay, halfArc < 3.1f ? cosHalf : -2f, Squash);
                return;
            }
            MarkP(x0, y0, x1 + 1, y1 + 1);
            uint s = (uint)rgb;
            fixed (int* dp = Px)
            {
                for (int y = y0; y <= y1; y++)
                {
                    float dy = (y + 0.5f - cy) * Squash;
                    uint* d = (uint*)(dp + y * PW);
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - cx;
                        float q = M.Sqrt(dx * dx + dy * dy);
                        float cov = k == 1 ? (q >= r0 && q <= r1 ? 1f : 0f) : Math.Min(1f, Math.Min(r1 + 0.5f - q, r0 > 0 ? q - r0 + 0.5f : 1f));
                        if (cov <= 0) continue;
                        if (halfArc < 3.1f && q > 0.001f && (dx * ax + dy * ay) / q < cosHalf) continue;
                        uint aa = (uint)(a * cov + 0.5f);
                        if (aa == 0) continue;
                        aa += aa >> 7;
                        d[x] = (additive ? AddSat(d[x], s, aa) : Blend(d[x], s, aa)) | EmisMask;
                    }
                }
            }
        }

        /// <summary>Soft additive radial glow (post-light effects).</summary>
        public void Glow(float cx, float cy, float r, int rgb, int a)
        {
            if (r <= 0 || a <= 0) return;
            if (S != 1) { cx *= S; cy *= S; r *= S; }
            int x0 = Math.Max(PX0, M.Floor(cx - r)), x1 = Math.Min(PX1 - 1, M.Ceil(cx + r));
            int y0 = Math.Max(PY0, M.Floor(cy - r)), y1 = Math.Min(PY1 - 1, M.Ceil(cy + r));
            float inv = 1f / (r * r);
            if (Rec != null && S != 1) { Rec.Shape(7, x0, y0, x1 + 1, y1 + 1, rgb & 0xFFFFFF, Math.Min(255, a) / 255f, true, cx, cy, r, 0, 0, 0, 0, 0); return; }
            MarkP(x0, y0, x1 + 1, y1 + 1);
            uint s = (uint)rgb;
            fixed (int* dp = Px)
            {
                for (int y = y0; y <= y1; y++)
                {
                    float dy = y + 0.5f - cy;
                    uint* d = (uint*)(dp + y * PW);
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - cx;
                        float t = 1f - (dx * dx + dy * dy) * inv;
                        if (t <= 0) continue;
                        uint aa = (uint)(a * t * t);
                        if (aa == 0) continue;
                        d[x] = AddSat(d[x], s, aa);
                    }
                }
            }
        }

        /// <summary>Lays a small opaque image (sw by sw pixels, RGB) at x, y; on a large surface it is enlarged smoothly.</summary>
        public void Image(int[] src, int sw, int sh, int x, int y) { ImageScaled(src, sw, sh, x, y, sw, sh); }

        /// <summary>A small opaque image stretched over dw by dh interface pixels (smoothly on a large surface).</summary>
        public void ImageScaled(int[] src, int sw, int sh, int x, int y, int dw, int dh) { ImageScaled(src, sw, sh, x, y, dw, dh, false); }

        /// <summary>
        /// masked: the alpha byte of each source pixel says how much of it shows (a map's explored ground). Enlarged
        /// smoothly, the edge of what shows becomes a soft, rounded contour instead of following the source's squares.
        /// </summary>
        public void ImageScaled(int[] src, int sw, int sh, int x, int y, int dw, int dh, bool masked)
        {
            if (sw <= 0 || sh <= 0 || dw <= 0 || dh <= 0) return;
            if (S == 1)
            {
                for (int j = 0; j < dh; j++)
                    for (int i = 0; i < dw; i++)
                    {
                        int c = src[(j * sh / dh) * sw + i * sw / dw];
                        if (!masked || ((c >> 24) & 255) >= 128) Pixel(x + i, y + j, c);
                    }
                return;
            }
            int ox = x * S, oy = y * S, w = dw * S, h = dh * S;
            int i0 = Math.Max(PX0, ox), i1 = Math.Min(PX1, ox + w), j0 = Math.Max(PY0, oy), j1 = Math.Min(PY1, oy + h);
            if (i1 <= i0 || j1 <= j0) return;
            if (Rec != null) { Rec.Image(src, sw, sh, ox, oy, w, h, masked); return; }
            MarkP(i0, j0, i1, j1);
            float k = sw / (float)w, kv = sh / (float)h;
            uint opa = OpA;
            if (masked)
            {
                for (int py = j0; py < j1; py++)
                {
                    float fy = (py - oy + 0.5f) * kv - 0.5f;
                    int ya = M.Clamp(M.Floor(fy), 0, sh - 1), yb = Math.Min(sh - 1, ya + 1);
                    float ty = M.Clamp01(fy - ya);
                    int ra = ya * sw, rb = yb * sw;
                    for (int px = i0; px < i1; px++)
                    {
                        float fx = (px - ox + 0.5f) * k - 0.5f;
                        int xa = M.Clamp(M.Floor(fx), 0, sw - 1), xb = Math.Min(sw - 1, xa + 1);
                        float tx = M.Clamp01(fx - xa);
                        int c00 = src[ra + xa], c01 = src[ra + xb], c10 = src[rb + xa], c11 = src[rb + xb];
                        float al = (((c00 >> 24) & 255) * (1 - tx) + ((c01 >> 24) & 255) * tx) * (1 - ty) + (((c10 >> 24) & 255) * (1 - tx) + ((c11 >> 24) & 255) * tx) * ty;
                        float m = M.Clamp01((al / 255f - 0.3f) / 0.4f);
                        m = m * m * (3 - 2 * m);
                        if (m <= 0) continue;
                        uint ti = (uint)(tx * 256f + 0.5f), tj = (uint)(ty * 256f + 0.5f);
                        uint col = Mix(Mix((uint)c00, (uint)c01, ti), Mix((uint)c10, (uint)c11, ti), tj);
                        int idx = py * PW + px;
                        Px[idx] = (int)Blend((uint)Px[idx], col, (uint)(m * 256f));
                    }
                }
                return;
            }
            for (int py = j0; py < j1; py++)
            {
                float fy = (py - oy + 0.5f) * kv - 0.5f;
                int ya = M.Clamp(M.Floor(fy), 0, sh - 1), yb = Math.Min(sh - 1, ya + 1);
                uint ty = (uint)(M.Clamp01(fy - ya) * 256f + 0.5f);
                int row = py * PW, ra = ya * sw, rb = yb * sw;
                for (int px = i0; px < i1; px++)
                {
                    float fx = (px - ox + 0.5f) * k - 0.5f;
                    int xa = M.Clamp(M.Floor(fx), 0, sw - 1), xb = Math.Min(sw - 1, xa + 1);
                    uint tx = (uint)(M.Clamp01(fx - xa) * 256f + 0.5f);
                    uint top = Mix((uint)src[ra + xa], (uint)src[ra + xb], tx), bot = Mix((uint)src[rb + xa], (uint)src[rb + xb], tx);
                    Px[row + px] = (int)(Mix(top, bot, ty) | opa);
                }
            }
        }

        /// <summary>Linear mix of two RGB colours, t in 0..256.</summary>
        static uint Mix(uint a, uint b, uint t)
        {
            uint it = 256 - t;
            return ((((a & 0xFF00FF) * it + (b & 0xFF00FF) * t) >> 8) & 0xFF00FF) | ((((a & 0x00FF00) * it + (b & 0x00FF00) * t) >> 8) & 0x00FF00);
        }

        /// <summary>Copies a 1:1 surface of the same interface size into this one, each pixel becoming an S by S block.</summary>
        public void UpscaleFrom(Surface src)
        {
            MarkAll();
            if (S == 1 || src.S != 1) { Array.Copy(src.Px, Px, Math.Min(src.Px.Length, Px.Length)); return; }
            int w = Math.Min(W, src.W), h = Math.Min(H, src.H);
            fixed (int* dp = Px) fixed (int* sp = src.Px)
            {
                for (int y = 0; y < h; y++)
                {
                    int* s = sp + y * src.W;
                    int* d0 = dp + y * S * PW;
                    for (int x = 0; x < w; x++)
                    {
                        int c = s[x];
                        int* d = d0 + x * S;
                        for (int i = 0; i < S; i++) d[i] = c;
                    }
                    for (int r = 1; r < S; r++) Buffer.MemoryCopy(d0, d0 + r * PW, PW * 4, w * S * 4);
                }
            }
        }

        // sprites
        /// <summary>The core of every sprite copy: sprite s placed with its top-left at physical (dx0, dy0).</summary>
        void BlitCore(Sprite s, int dx0, int dy0, bool flip, int stride, int c0x, int c0y, int c1x, int c1y)
        {
            if (Rec != null && stride == PW && S != 1) { Rec.Sprite(s, dx0, dy0, s.W, s.H, flip, 0xFFFFFFFF, 0, 0); return; }
            int sx0 = 0, sy0 = 0, w = s.W, h = s.H;
            if (dx0 < c0x) { sx0 = c0x - dx0; w -= sx0; dx0 = c0x; }
            if (dy0 < c0y) { sy0 = c0y - dy0; h -= sy0; dy0 = c0y; }
            if (dx0 + w > c1x) w = c1x - dx0;
            if (dy0 + h > c1y) h = c1y - dy0;
            if (w <= 0 || h <= 0) return;
            if (stride == PW) MarkP(dx0, dy0, dx0 + w, dy0 + h);
            int step = flip ? -1 : 1;
            fixed (int* dp = Px) fixed (int* sp = s.Px)
            {
                for (int j = 0; j < h; j++)
                {
                    uint* d = (uint*)(dp + (dy0 + j) * stride + dx0);
                    uint* sr = (uint*)(sp + (sy0 + j) * s.W + (flip ? (s.W - 1 - sx0) : sx0));
                    for (int i = 0; i < w; i++)
                    {
                        uint c = *sr; sr += step;
                        uint a = c >> 24;
                        if (a == 255) *d = (c & 0xFFFFFF) | OpA;
                        else if (a == 254) *d = c | 0xFF000000;
                        else if (a != 0) *d = Blend(*d, c, a + (a >> 7));
                        d++;
                    }
                }
            }
        }

        public void Blit(Sprite s, int x, int y) { Blit(s, x, y, false); }

        public void Blit(Sprite s, int x, int y, bool flip)
        {
            if (s == null) return;
            if (S != 1)
            {
                Sprite hs = HiArt.Get(s, S);
                BlitCore(hs, (x - (flip ? (s.W - 1 - s.OX) : s.OX)) * S, (y - s.OY) * S, flip, PW, PX0, PY0, PX1, PY1);
                return;
            }
            BlitCore(s, x - (flip ? (s.W - 1 - s.OX) : s.OX), y - s.OY, flip, W, CX0, CY0, CX1, CY1);
        }

        /// <summary>Blit with a horizontal shear that grows toward the top of the sprite (wind sway).</summary>
        public void BlitSway(Sprite s, int x, int y, bool flip, float sway)
        {
            if (s == null) return;
            if (S != 1) { Blit(s, x + M.Round(sway * 0.5f), y, flip); return; }
            int sx0 = CX0, sy0 = CY0, sx1 = CX1, sy1 = CY1;
            int top = y - s.OY;
            int bands = Math.Min(4, Math.Max(1, s.H / 3));
            for (int b = 0; b < bands; b++)
            {
                int ya = top + b * s.H / bands, yb = top + (b + 1) * s.H / bands;
                int cy0 = Math.Max(sy0, ya), cy1 = Math.Min(sy1, yb);
                if (cy1 <= cy0) continue;
                CY0 = cy0; CY1 = cy1;
                float k = 1f - (b + 0.5f) / bands;
                Blit(s, x + M.Round(sway * k), y, flip);
            }
            CX0 = sx0; CY0 = sy0; CX1 = sx1; CY1 = sy1;
        }

        /// <summary>
        /// General blit: optional flip, tint (lerp toward tintRgb by tintAmt/256), global alpha (0..256),
        /// and 'solid' mode which replaces all colour with the tint (silhouettes, flashes, shadows).
        /// </summary>
        public void BlitEx(Sprite s, int x, int y, bool flip, int tintRgb, int tintAmt, int alpha)
        {
            if (s == null || alpha <= 0) return;
            if (alpha > 256) alpha = 256;
            int dx0, dy0, stride, c0x, c0y, c1x, c1y;
            Sprite src = s;
            if (S != 1)
            {
                src = HiArt.Get(s, S);
                dx0 = (x - (flip ? (s.W - 1 - s.OX) : s.OX)) * S; dy0 = (y - s.OY) * S;
                stride = PW; c0x = PX0; c0y = PY0; c1x = PX1; c1y = PY1;
                if (Rec != null)
                {
                    Rec.Sprite(src, dx0, dy0, src.W, src.H, flip, UiRec.Pm(0xFFFFFF, alpha / 256f, false), tintRgb & 0xFFFFFF, M.Clamp(tintAmt, 0, 256) / 256f);
                    return;
                }
            }
            else
            {
                dx0 = x - (flip ? (s.W - 1 - s.OX) : s.OX); dy0 = y - s.OY;
                stride = W; c0x = CX0; c0y = CY0; c1x = CX1; c1y = CY1;
            }
            int sx0 = 0, sy0 = 0, w = src.W, h = src.H;
            if (dx0 < c0x) { sx0 = c0x - dx0; w -= sx0; dx0 = c0x; }
            if (dy0 < c0y) { sy0 = c0y - dy0; h -= sy0; dy0 = c0y; }
            if (dx0 + w > c1x) w = c1x - dx0;
            if (dy0 + h > c1y) h = c1y - dy0;
            if (w <= 0 || h <= 0) return;
            if (stride == PW) MarkP(dx0, dy0, dx0 + w, dy0 + h);
            int step = flip ? -1 : 1;
            uint tint = (uint)tintRgb & 0xFFFFFF, ta = (uint)M.Clamp(tintAmt, 0, 256), ga = (uint)alpha;
            fixed (int* dp = Px) fixed (int* sp = src.Px)
            {
                for (int j = 0; j < h; j++)
                {
                    uint* d = (uint*)(dp + (dy0 + j) * stride + dx0);
                    uint* sr = (uint*)(sp + (sy0 + j) * src.W + (flip ? (src.W - 1 - sx0) : sx0));
                    for (int i = 0; i < w; i++)
                    {
                        uint c = *sr; sr += step;
                        uint a = c >> 24;
                        if (a != 0)
                        {
                            bool em = a == 254;
                            if (a >= 254) a = 256; else a = a + (a >> 7);
                            if (ta != 0) c = Blend(c & 0xFFFFFF, tint, ta);
                            a = (a * ga) >> 8;
                            if (a >= 256) *d = em ? (c | 0xFF000000) : ((c & 0xFFFFFF) | OpA);
                            else if (a != 0) *d = Blend(*d, c, a);
                        }
                        d++;
                    }
                }
            }
        }

        /// <summary>Additive blit (sprite alpha scales the contribution). Used after lighting for glows.</summary>
        public void BlitAdd(Sprite s, int x, int y, int amt256)
        {
            if (s == null || amt256 <= 0) return;
            int dx0, dy0, stride, c0x, c0y, c1x, c1y;
            Sprite src = s;
            if (S != 1)
            {
                src = HiArt.Get(s, S);
                dx0 = (x - s.OX) * S; dy0 = (y - s.OY) * S;
                stride = PW; c0x = PX0; c0y = PY0; c1x = PX1; c1y = PY1;
                if (Rec != null) { Rec.Sprite(src, dx0, dy0, src.W, src.H, false, UiRec.Pm(0xFFFFFF, Math.Min(256, amt256) / 256f, true), 0, 0); return; }
            }
            else
            {
                dx0 = x - s.OX; dy0 = y - s.OY;
                stride = W; c0x = CX0; c0y = CY0; c1x = CX1; c1y = CY1;
            }
            int sx0 = 0, sy0 = 0, w = src.W, h = src.H;
            if (dx0 < c0x) { sx0 = c0x - dx0; w -= sx0; dx0 = c0x; }
            if (dy0 < c0y) { sy0 = c0y - dy0; h -= sy0; dy0 = c0y; }
            if (dx0 + w > c1x) w = c1x - dx0;
            if (dy0 + h > c1y) h = c1y - dy0;
            if (w <= 0 || h <= 0) return;
            if (stride == PW) MarkP(dx0, dy0, dx0 + w, dy0 + h);
            fixed (int* dp = Px) fixed (int* sp = src.Px)
            {
                for (int j = 0; j < h; j++)
                {
                    uint* d = (uint*)(dp + (dy0 + j) * stride + dx0);
                    uint* sr = (uint*)(sp + (sy0 + j) * src.W + sx0);
                    for (int i = 0; i < w; i++)
                    {
                        uint c = sr[i];
                        uint a = c >> 24;
                        if (a != 0) d[i] = AddSat(d[i], c, (a * (uint)amt256) >> 8);
                    }
                }
            }
        }

        /// <summary>Rotated (nearest neighbour) blit around the sprite origin. mirror flips the sprite's local Y axis.</summary>
        public void BlitRot(Sprite s, float cx, float cy, float ang, bool mirror, int tintRgb, int tintAmt, int alpha, float scale)
        {
            if (s == null || alpha <= 0) return;
            int stride = W, c0x = CX0, c0y = CY0, c1x = CX1, c1y = CY1;
            if (S != 1)
            {
                s = HiArt.Get(s, S);
                cx *= S; cy *= S;
                stride = PW; c0x = PX0; c0y = PY0; c1x = PX1; c1y = PY1;
            }
            float cs = M.Cos(ang), sn = M.Sin(ang);
            if (Rec != null && S != 1)
            {
                // the four corners of the sprite, turned and scaled about its origin
                float ax0 = -s.OX * scale, ax1 = (s.W - s.OX) * scale, ay0 = -s.OY * scale, ay1 = (s.H - s.OY) * scale;
                if (mirror) { ay0 = -ay0; ay1 = -ay1; }
                Rec.SpriteQuad(s,
                    cx + ax0 * cs - ay0 * sn, cy + ax0 * sn + ay0 * cs,
                    cx + ax1 * cs - ay0 * sn, cy + ax1 * sn + ay0 * cs,
                    cx + ax1 * cs - ay1 * sn, cy + ax1 * sn + ay1 * cs,
                    cx + ax0 * cs - ay1 * sn, cy + ax0 * sn + ay1 * cs,
                    UiRec.Pm(0xFFFFFF, Math.Min(alpha, 256) / 256f, false), tintRgb & 0xFFFFFF, M.Clamp(tintAmt, 0, 256) / 256f);
                return;
            }
            float minX = 1e9f, minY = 1e9f, maxX = -1e9f, maxY = -1e9f;
            for (int k = 0; k < 4; k++)
            {
                float lx = ((k & 1) == 0 ? -s.OX : s.W - s.OX) * scale;
                float ly = ((k & 2) == 0 ? -s.OY : s.H - s.OY) * scale;
                if (mirror) ly = -ly;
                float rx = lx * cs - ly * sn, ry = lx * sn + ly * cs;
                if (rx < minX) minX = rx; if (rx > maxX) maxX = rx;
                if (ry < minY) minY = ry; if (ry > maxY) maxY = ry;
            }
            int x0 = Math.Max(c0x, M.Floor(cx + minX)), x1 = Math.Min(c1x - 1, M.Ceil(cx + maxX));
            int y0 = Math.Max(c0y, M.Floor(cy + minY)), y1 = Math.Min(c1y - 1, M.Ceil(cy + maxY));
            if (x1 < x0 || y1 < y0) return;
            if (stride == PW) MarkP(x0, y0, x1 + 1, y1 + 1);
            float inv = 1f / scale;
            uint tint = (uint)tintRgb & 0xFFFFFF, ta = (uint)M.Clamp(tintAmt, 0, 256), ga = (uint)Math.Min(alpha, 256);
            fixed (int* dp = Px) fixed (int* sp = s.Px)
            {
                for (int y = y0; y <= y1; y++)
                {
                    float dy = y + 0.5f - cy;
                    uint* d = (uint*)(dp + y * stride);
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - cx;
                        float lx = (dx * cs + dy * sn) * inv, ly = (-dx * sn + dy * cs) * inv;
                        if (mirror) ly = -ly;
                        int ux = M.Floor(lx + s.OX), uy = M.Floor(ly + s.OY);
                        if (ux < 0 || uy < 0 || ux >= s.W || uy >= s.H) continue;
                        uint c = (uint)sp[uy * s.W + ux];
                        uint a = c >> 24;
                        if (a == 0) continue;
                        bool em = a == 254;
                        if (a >= 254) a = 256; else a = a + (a >> 7);
                        if (ta != 0) c = Blend(c & 0xFFFFFF, tint, ta);
                        a = (a * ga) >> 8;
                        if (a >= 256) d[x] = em ? (c | 0xFF000000) : ((c & 0xFFFFFF) | OpA);
                        else if (a != 0) d[x] = Blend(d[x], c, a);
                    }
                }
            }
        }

        /// <summary>Integer-scaled blit (portraits, big icons), top-left at x, y.</summary>
        public void BlitScaled(Sprite s, int x, int y, int scale, bool flip)
        {
            if (s == null) return;
            if (S != 1)
            {
                Sprite hs = HiArt.Get(s, scale * S);
                BlitCore(hs, x * S, y * S, flip, PW, PX0, PY0, PX1, PY1);
                return;
            }
            for (int j = 0; j < s.H; j++)
                for (int i = 0; i < s.W; i++)
                {
                    int c = s.Px[j * s.W + (flip ? s.W - 1 - i : i)];
                    int a = (c >> 24) & 255;
                    if (a == 0) continue;
                    if (a >= 254) Fill(x + i * scale, y + j * scale, scale, scale, c);
                    else FillA(x + i * scale, y + j * scale, scale, scale, c, a);
                }
        }

        /// <summary>Copies a rectangle out of a large pre-baked bitmap (the ground layer). Fills outside with black.</summary>
        public void CopyFrom(int[] src, int srcW, int srcH, int sx, int sy)
        {
            if (S != 1)
            {
                MarkAll();
                for (int y = 0; y < PH; y++)
                {
                    int yy = sy + y / S;
                    for (int x = 0; x < PW; x++)
                    {
                        int xx = sx + x / S;
                        Px[y * PW + x] = yy < 0 || yy >= srcH || xx < 0 || xx >= srcW ? 0 : src[yy * srcW + xx];
                    }
                }
                return;
            }
            for (int y = 0; y < H; y++)
            {
                int yy = sy + y;
                int dOff = y * W;
                if (yy < 0 || yy >= srcH) { Array.Clear(Px, dOff, W); continue; }
                int xa = sx, n = W, d = dOff;
                if (xa < 0) { Array.Clear(Px, d, Math.Min(W, -xa)); d += -xa; n -= -xa; xa = 0; }
                if (xa + n > srcW)
                {
                    int over = xa + n - srcW;
                    if (over > 0 && over <= W) Array.Clear(Px, dOff + W - over, over);
                    n -= over;
                }
                if (n > 0) Buffer.BlockCopy(src, (yy * srcW + xa) * 4, Px, d * 4, n * 4);
            }
        }

        // text
        /// <summary>Colour codes usable inside strings: ^w ^y ^g ^r ^b ^p ^d ^o ^c and ^0 to reset.</summary>
        public static int CodeColor(char code, int baseColor)
        {
            switch (code)
            {
                case 'w': return 0xF2ECD8;
                case 'y': return 0xF0C060;
                case 'g': return 0x8FCB6B;
                case 'r': return 0xE0584E;
                case 'b': return 0x7FB2F0;
                case 'p': return 0xC08CF0;
                case 'd': return 0x8A8A9A;
                case 'o': return 0xF09040;
                case 'c': return 0x7FE0D8;
                case 'k': return 0x1A1626;
                default: return baseColor;
            }
        }

        public int Text(PixFont f, string str, int x, int y, int rgb)
        {
            return Text(f, str, x, y, rgb, -1);
        }

        /// <summary>How far a drop shadow sits from its text, in physical pixels.</summary>
        int ShadowOff { get { return S == 1 ? 1 : Math.Max(1, (S * 2 + 1) / 3); } }

        /// <summary>Draws a single line. shadowRgb &lt; 0 disables the drop shadow. Returns the pen x after the text.</summary>
        public int Text(PixFont f, string str, int x, int y, int rgb, int shadowRgb)
        {
            if (str == null || f == null) return x;
            str = Loc.Tr(str);
            if (S != 1 && f.Hi != null)
            {
                int o = ShadowOff;
                if (shadowRgb >= 0) TextHi(f.Hi, str, x * S + o, y * S + o, shadowRgb, true);
                TextHi(f.Hi, str, x * S, y * S, rgb, false);
                return x + f.Width(str);
            }
            if (shadowRgb >= 0) TextRaw(f, str, x + 1, y + 1, shadowRgb, true);
            int end = TextRaw(f, str, x, y, rgb, false);
            return f.Hi != null ? x + f.Width(str) : end;
        }

        public void TextOutlined(PixFont f, string str, int x, int y, int rgb, int outlineRgb)
        {
            if (str == null || f == null) return;
            str = Loc.Tr(str);
            if (S != 1 && f.Hi != null)
            {
                int px = x * S, py = y * S, o = (S + 1) / 2;
                for (int k = 0; k < 8; k++)
                {
                    int ox = k == 0 || k == 3 || k == 5 ? -o : (k == 1 || k == 6 ? 0 : o);
                    int oy = k < 3 ? -o : (k < 5 ? 0 : o);
                    TextHi(f.Hi, str, px + ox, py + oy, outlineRgb, true);
                }
                TextHi(f.Hi, str, px, py, rgb, false);
                return;
            }
            TextRaw(f, str, x - 1, y, outlineRgb, true); TextRaw(f, str, x + 1, y, outlineRgb, true);
            TextRaw(f, str, x, y - 1, outlineRgb, true); TextRaw(f, str, x, y + 1, outlineRgb, true);
            TextRaw(f, str, x, y, rgb, false);
        }

        public void TextCentered(PixFont f, string str, int cx, int y, int rgb, int shadowRgb)
        {
            Text(f, str, cx - f.Width(str) / 2, y, rgb, shadowRgb);
        }

        public void TextRight(PixFont f, string str, int rx, int y, int rgb, int shadowRgb)
        {
            Text(f, str, rx - f.Width(str), y, rgb, shadowRgb);
        }

        int TextRaw(PixFont f, string str, int x, int y, int rgb, bool ignoreCodes)
        {
            int baseCol = rgb & 0xFFFFFF, col = baseCol;
            int n = str.Length;
            for (int i = 0; i < n; i++)
            {
                char ch = str[i];
                if (ch == '^' && i + 1 < n)
                {
                    char code = str[i + 1];
                    if (code != '^')
                    {
                        if (!ignoreCodes) col = code == '0' ? baseCol : CodeColor(code, baseCol);
                        i++; continue;
                    }
                    i++;
                }
                PixFont.Glyph g = f.Glyphs[PixFont.Slot(ch)];
                if (g == null) { x += f.SpaceW; continue; }
                if (g.Bits != null)
                {
                    int gx = x + g.XOff, gy = y + g.YOff;
                    if (gx < CX1 && gy < CY1 && gx + g.W > CX0 && gy + g.H > CY0)
                    {
                        for (int j = 0; j < g.H; j++)
                        {
                            int py = gy + j;
                            if (py < CY0 || py >= CY1) continue;
                            int row = py * W, gr = j * g.W;
                            for (int k = 0; k < g.W; k++)
                            {
                                if (g.Bits[gr + k] == 0) continue;
                                int px = gx + k;
                                if (px < CX0 || px >= CX1) continue;
                                Px[row + px] = col | (int)OpA;
                            }
                        }
                    }
                }
                x += g.Adv + f.Tracking;
            }
            return x;
        }

        /// <summary>One line from an anti-aliased font at physical position (px, py).</summary>
        void TextHi(PixFont h, string str, int px, int py, int rgb, bool ignoreCodes)
        {
            uint baseCol = (uint)rgb & 0xFFFFFF, col = baseCol;
            int n = str.Length, pen = px, top = py + h.YAdj;
            if (Rec != null) { TextRec(h, str, pen, top, (int)baseCol, ignoreCodes); return; }
            fixed (int* dp = Px)
            {
                for (int i = 0; i < n; i++)
                {
                    char ch = str[i];
                    if (ch == '^' && i + 1 < n)
                    {
                        char code = str[i + 1];
                        if (code != '^')
                        {
                            if (!ignoreCodes) col = code == '0' ? baseCol : (uint)CodeColor(code, (int)baseCol) & 0xFFFFFF;
                            i++; continue;
                        }
                        i++;
                    }
                    PixFont.Glyph g = h.Glyphs[PixFont.Slot(ch)];
                    if (g == null) { pen += h.SpaceW; continue; }
                    byte[] cov = g.Cov;
                    if (cov != null)
                    {
                        int gx = pen + g.XOff, gy = top + g.YOff;
                        if (gx < PX1 && gy < PY1 && gx + g.W > PX0 && gy + g.H > PY0)
                        {
                            int ja = Math.Max(0, PY0 - gy), jb = Math.Min(g.H, PY1 - gy);
                            int ka = Math.Max(0, PX0 - gx), kb = Math.Min(g.W, PX1 - gx);
                            MarkP(gx + ka, gy + ja, gx + kb, gy + jb);
                            for (int j = ja; j < jb; j++)
                            {
                                uint* d = (uint*)(dp + (gy + j) * PW + gx);
                                int gr = j * g.W;
                                for (int k = ka; k < kb; k++)
                                {
                                    uint a = cov[gr + k];
                                    if (a == 0) continue;
                                    if (a == 255) d[k] = col | OpA;
                                    else d[k] = Blend(d[k], col, a + (a >> 7));
                                }
                            }
                        }
                    }
                    pen += g.Adv + h.Tracking;
                }
            }
        }

        /// <summary>TextHi for a recorded frame: one atlas quad per glyph.</summary>
        void TextRec(PixFont h, string str, int pen, int top, int baseCol, bool ignoreCodes)
        {
            int col = baseCol, n = str.Length;
            for (int i = 0; i < n; i++)
            {
                char ch = str[i];
                if (ch == '^' && i + 1 < n)
                {
                    char code = str[i + 1];
                    if (code != '^')
                    {
                        if (!ignoreCodes) col = code == '0' ? baseCol : CodeColor(code, baseCol) & 0xFFFFFF;
                        i++; continue;
                    }
                    i++;
                }
                PixFont.Glyph g = h.Glyphs[PixFont.Slot(ch)];
                if (g == null) { pen += h.SpaceW; continue; }
                if (g.Cov != null)
                {
                    int gx = pen + g.XOff, gy = top + g.YOff;
                    if (gx < PX1 && gy < PY1 && gx + g.W > PX0 && gy + g.H > PY0) Rec.Glyph(g, g.Cov, g.W, g.H, gx, gy, col);
                }
                pen += g.Adv + h.Tracking;
            }
        }

        /// <summary>Draws word-wrapped text; returns the y just below the last line.</summary>
        public int TextWrapped(PixFont f, string str, int x, int y, int maxW, int rgb, int shadowRgb, int lineGap)
        {
            List<string> lines = f.Wrap(str, maxW);
            for (int i = 0; i < lines.Count; i++)
            {
                Text(f, lines[i], x, y, rgb, shadowRgb);
                y += f.LineH + lineGap;
            }
            return y;
        }

        // export
        public void SavePng(string path, int scale)
        {
            if (S != 1) scale = 1;
            using (Bitmap bmp = new Bitmap(PW * scale, PH * scale, PixelFormat.Format32bppRgb))
            {
                BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
                int[] row = new int[bmp.Width];
                for (int y = 0; y < bmp.Height; y++)
                {
                    int sy = y / scale;
                    for (int x = 0; x < bmp.Width; x++) row[x] = Px[sy * PW + x / scale] | Col.Opaque;
                    Marshal.Copy(row, 0, new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), bmp.Width);
                }
                bmp.UnlockBits(bd);
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }
}
