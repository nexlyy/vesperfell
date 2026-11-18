using System;

namespace Vesperfell
{
    /// <summary>
    /// The atmosphere pass. Lights are accumulated into a half-resolution buffer on top of a coloured ambient,
    /// drifting fog is lit by that same buffer, and the result is multiplied over the unlit scene.
    /// Pixels flagged emissive in the scene (flames, windows, glowing eyes) ignore the darkness.
    /// </summary>
    public sealed unsafe class Lighting
    {
        public const int LW = 320, LH = 180;

        readonly int[] lr = new int[LW * LH], lg = new int[LW * LH], lb = new int[LW * LH];
        readonly uint[] mul = new uint[LW * LH], add = new uint[LW * LH];
        readonly byte[] fogTex = new byte[256 * 256];
        readonly int[] vig = new int[LW * LH];

        public float AmbR = 0.5f, AmbG = 0.55f, AmbB = 0.8f;
        public float FogDensity = 0.35f;
        public int FogColor = 0x8FA0C8;
        public float Vignette = 1f;
        public float Time;
        public bool Enabled = true;

        public Lighting()
        {
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float n = Noise.Tile(x / 32f, y / 32f, 8, 901) * 0.55f + Noise.Tile(x / 16f, y / 16f, 16, 902) * 0.3f + Noise.Tile(x / 8f, y / 8f, 32, 903) * 0.15f;
                    fogTex[y * 256 + x] = (byte)M.Clamp((int)(n * 255), 0, 255);
                }
            for (int y = 0; y < LH; y++)
                for (int x = 0; x < LW; x++)
                {
                    float dx = (x + 0.5f - LW / 2f) / (LW / 2f), dy = (y + 0.5f - LH / 2f) / (LH / 2f);
                    float d = dx * dx * 0.85f + dy * dy * 0.95f;
                    float v = 1f - M.Smooth((d - 0.35f) / 1.25f) * 0.62f;
                    vig[y * LW + x] = (int)(v * 256);
                }
        }

        public void Begin()
        {
            int r = (int)(AmbR * 256), g = (int)(AmbG * 256), b = (int)(AmbB * 256);
            for (int i = 0; i < lr.Length; i++) { lr[i] = r; lg[i] = g; lb[i] = b; }
        }

        /// <summary>Adds a light at screen position (full-res pixels).</summary>
        public void Light(float sx, float sy, float radius, int rgb, float intensity)
        {
            if (radius <= 1 || intensity <= 0) return;
            float hx = sx * 0.5f, hy = sy * 0.5f, hr = radius * 0.5f;
            const float squash = 1.18f;
            int x0 = Math.Max(0, (int)(hx - hr)), x1 = Math.Min(LW - 1, (int)(hx + hr) + 1);
            int y0 = Math.Max(0, (int)(hy - hr / squash)), y1 = Math.Min(LH - 1, (int)(hy + hr / squash) + 1);
            if (x1 < x0 || y1 < y0) return;
            float ir = ((rgb >> 16) & 255) * intensity * (256f / 255f), ig = ((rgb >> 8) & 255) * intensity * (256f / 255f), ib = (rgb & 255) * intensity * (256f / 255f);
            float inv = 1f / (hr * hr);
            fixed (int* pr = lr, pg = lg, pb = lb)
            {
                for (int y = y0; y <= y1; y++)
                {
                    float dy = (y + 0.5f - hy) * squash;
                    float dy2 = dy * dy;
                    int row = y * LW;
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - hx;
                        float t = 1f - (dx * dx + dy2) * inv;
                        if (t <= 0) continue;
                        t = t * t;
                        int k = row + x;
                        pr[k] += (int)(ir * t); pg[k] += (int)(ig * t); pb[k] += (int)(ib * t);
                    }
                }
            }
        }

        /// <summary>Multiplies the unlit scene by the light buffer and writes the result to dst.</summary>
        public void Compose(Surface scene, Surface dst, int camX, int camY)
        {
            int W = scene.W, H = scene.H;
            if (!Enabled)
            {
                for (int i = 0; i < scene.Px.Length; i++) dst.Px[i] = scene.Px[i] & 0xFFFFFF;
                return;
            }
            int fogD = (int)(M.Clamp01(FogDensity) * 256);
            int fr = (FogColor >> 16) & 255, fg = (FogColor >> 8) & 255, fb = FogColor & 255;
            int s1x = (int)(Time * 7f), s1y = (int)(Time * 2.5f), s2x = (int)(-Time * 4f), s2y = (int)(Time * 5f);
            int vigAmt = (int)(M.Clamp01(Vignette) * 256);

            fixed (int* pr = lr, pg = lg, pb = lb, pv = vig)
            fixed (uint* pm = mul, pa = add)
            fixed (byte* ft = fogTex)
            {
                for (int j = 0; j < LH; j++)
                {
                    int wy = camY + j * 2;
                    int f1y = (((wy + s1y) >> 1) & 255) << 8, f2y = ((((wy * 5 >> 3) + s2y) >> 1) & 255) << 8;
                    for (int i = 0; i < LW; i++)
                    {
                        int k = j * LW + i;
                        int r = pr[k], g = pg[k], b = pb[k];
                        if (r > 440) r = 440; if (g > 440) g = 440; if (b > 440) b = 440;
                        int ar = 0, ag = 0, ab = 0;
                        if (fogD > 0)
                        {
                            int wx = camX + i * 2;
                            int a = ft[f1y | (((wx + s1x) >> 1) & 255)], c = ft[f2y | ((((wx * 5 >> 3) + s2x) >> 1) & 255)];
                            int f = (a * c >> 7) - 70;
                            if (f > 0)
                            {
                                if (f > 255) f = 255;
                                f = f * fogD >> 8;
                                ar = fr * f * r >> 16; ag = fg * f * g >> 16; ab = fb * f * b >> 16;
                                int keep = 256 - (f * 150 >> 8);
                                r = r * keep >> 8; g = g * keep >> 8; b = b * keep >> 8;
                            }
                        }
                        int v = 256 - ((256 - pv[k]) * vigAmt >> 8);
                        r = r * v >> 8; g = g * v >> 8; b = b * v >> 8;
                        ar = ar * v >> 8; ag = ag * v >> 8; ab = ab * v >> 8;
                        if (ar > 255) ar = 255; if (ag > 255) ag = 255; if (ab > 255) ab = 255;
                        pm[k] = ((uint)r << 20) | ((uint)g << 10) | (uint)b;
                        pa[k] = ((uint)ar << 16) | ((uint)ag << 8) | (uint)ab;
                    }
                }

                fixed (int* sp = scene.Px, dp = dst.Px)
                {
                    for (int y = 0; y < H; y++)
                    {
                        uint* s = (uint*)(sp + y * W);
                        uint* d = (uint*)(dp + y * W);
                        uint* m = pm + (y >> 1) * LW;
                        uint* a = pa + (y >> 1) * LW;
                        for (int x = 0; x < W; x++)
                        {
                            uint c = s[x];
                            uint mm = m[x >> 1], aa = a[x >> 1];
                            uint r, g, b;
                            if (c >= 0x01000000)
                            {
                                r = (c >> 16) & 255; g = (c >> 8) & 255; b = c & 255;
                            }
                            else
                            {
                                r = (((c >> 16) & 255) * (mm >> 20)) >> 8;
                                g = (((c >> 8) & 255) * ((mm >> 10) & 1023)) >> 8;
                                b = ((c & 255) * (mm & 1023)) >> 8;
                            }
                            r += (aa >> 16) & 255; g += (aa >> 8) & 255; b += aa & 255;
                            if (r > 255) r = 255; if (g > 255) g = 255; if (b > 255) b = 255;
                            d[x] = (r << 16) | (g << 8) | b;
                        }
                    }
                }
            }
        }
    }
}
