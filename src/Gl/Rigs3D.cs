using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>Body plan and colours of one kind of humanoid, plus its parts once they are built.</summary>
    internal sealed class HumanSpec
    {
        public const int HHair = 0, HHelm = 1, HHood = 2, HPointHood = 3, HSkull = 4, HMaskHood = 5, HHorned = 6, HBell = 7,
            HCap = 8, HBeard = 9, HBun = 10, HOldBeard = 11, HHollow = 12;

        public float Bulk = 1f, Lean, HeadScale = 1f;
        public int Head = HHair;
        public int Skin = 0xE0AE86, HeadCol = 0x5F4337, HeadCol2 = 0xCF4150, HeadDark = 0x363C52, Eye = 0x15121F; public bool EyeGlow;
        public int Torso = 0x7E5D45, Belt = 0x44302E, Buckle = 0xD2AC52;
        public int Sleeve = 0x7E5D45, Hand = 0xE0AE86, Legs = 0x5F4337, Boots = 0x2E2126;
        public int Shoulder = -1, Tabard = -1, Emblem = -1, Strap = -1, Mantle = -1, Apron = -1, Scarf = -1, Cape = -1;
        public byte TorsoLayer = Mat3.Cloth;
        public bool Robe, Quiver, Thin, Spirit;
        public int RobeCol = 0x3A2E5E, RobeTrim = -1;
        public float CapeLen = 12.5f;

        internal MeshB TorsoM, HeadM, UpperArmR, UpperArmL, ForeArm, Thigh, Shin, Boot, Skirt, CapeM, ScarfM;
        internal bool Built;
    }

    /// <summary>
    /// Models for everything that moves: humanoids assembled from a handful of parts and posed every frame, beasts,
    /// weapons, loot and missiles. Every model faces +X with +Z on its right, the way the posing code expects.
    /// </summary>
    internal static class Rigs
    {
        public const float ThighLen = 5.7f, ShinLen = 5.0f, AnkleY = 1.3f, HipY = ThighLen + ShinLen + AnkleY;
        public const float UpperLen = 4.7f, ForeLen = 4.5f, ShoulderY = 8.6f, ShoulderZ = 4.1f, NeckY = 9.5f;
        public const float HumanScale = 1.2f;

        // geometry helpers
        /// <summary>A box that narrows (or widens) from its bottom to its top: half sizes bx,bz at y0 and tx,tz at y1.</summary>
        public static void Taper(MeshB m, float cx, float cz, float y0, float y1, float bx, float bz, float tx, float tz)
        {
            V3 a = new V3(cx - bx, y0, cz - bz), b = new V3(cx + bx, y0, cz - bz), c = new V3(cx + bx, y0, cz + bz), d = new V3(cx - bx, y0, cz + bz);
            V3 e = new V3(cx - tx, y1, cz - tz), f = new V3(cx + tx, y1, cz - tz), gg = new V3(cx + tx, y1, cz + tz), h = new V3(cx - tx, y1, cz + tz);
            m.Quad(e, h, gg, f); m.Quad(a, b, c, d); m.Quad(d, c, gg, h); m.Quad(b, a, e, f); m.Quad(c, b, f, gg); m.Quad(a, d, h, e);
        }

        /// <summary>A flat ribbon from a to b, w wide, lying across the direction n.</summary>
        public static void Strip(MeshB m, V3 a, V3 b, float w, V3 n)
        {
            V3 side = V3.Cross(b - a, n).Norm * (w * 0.5f);
            m.Quad(a - side, b - side, b + side, a + side);
        }

        /// <summary>A convex outline in the XY plane given a thickness along Z.</summary>
        public static void Extrude(MeshB m, float[] xy, float z0, float z1)
        {
            int n = xy.Length / 2;
            for (int i = 1; i + 1 < n; i++)
            {
                m.Tri(new V3(xy[0], xy[1], z1), new V3(xy[i * 2], xy[i * 2 + 1], z1), new V3(xy[i * 2 + 2], xy[i * 2 + 3], z1));
                m.Tri(new V3(xy[0], xy[1], z0), new V3(xy[i * 2 + 2], xy[i * 2 + 3], z0), new V3(xy[i * 2], xy[i * 2 + 1], z0));
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                m.Quad(new V3(xy[i * 2], xy[i * 2 + 1], z0), new V3(xy[j * 2], xy[j * 2 + 1], z0), new V3(xy[j * 2], xy[j * 2 + 1], z1), new V3(xy[i * 2], xy[i * 2 + 1], z1));
            }
        }

        /// <summary>A double-edged blade along +X with a diamond cross-section: w half-width (along Y), t half-thickness.</summary>
        public static void Blade(MeshB m, float x0, float x1, float xTip, float w0, float w1, float t)
        {
            V3 t0 = new V3(x0, w0, 0), f0 = new V3(x0, 0, t), b0 = new V3(x0, -w0, 0), k0 = new V3(x0, 0, -t);
            V3 t1 = new V3(x1, w1, 0), f1 = new V3(x1, 0, t * 0.8f), b1 = new V3(x1, -w1, 0), k1 = new V3(x1, 0, -t * 0.8f);
            V3 tip = new V3(xTip, 0, 0);
            m.Quad(t0, t1, f1, f0); m.Quad(f0, f1, b1, b0); m.Quad(b0, b1, k1, k0); m.Quad(k0, k1, t1, t0);
            m.Tri(t1, tip, f1); m.Tri(f1, tip, b1); m.Tri(b1, tip, k1); m.Tri(k1, tip, t1);
            m.Quad(t0, f0, b0, k0);
        }

        /// <summary>Rotation whose +X runs along fwd and +Y as close to up as possible.</summary>
        public static Xf Orient(V3 fwd, V3 up)
        {
            V3 a = fwd.Norm;
            V3 b = up - a * V3.Dot(up, a);
            if (b.Len < 1e-3f) { b = new V3(0, 0, 1) - a * a.Z; if (b.Len < 1e-3f) b = new V3(1, 0, 0) - a * a.X; }
            b = b.Norm;
            V3 c = V3.Cross(a, b);
            Xf r = new Xf();
            r.Ax = a.X; r.Ay = a.Y; r.Az = a.Z; r.Bx = b.X; r.By = b.Y; r.Bz = b.Z; r.Cx = c.X; r.Cy = c.Y; r.Cz = c.Z;
            return r;
        }

        /// <summary>Rotation that sends a limb built hanging along -Y toward dir, keeping its front toward hint.</summary>
        public static Xf Seg(V3 dir, V3 hint)
        {
            V3 b = (-dir).Norm;
            V3 a = hint - b * V3.Dot(hint, b);
            if (a.Len < 1e-3f) { a = new V3(0, 0, 1) - b * b.Z; if (a.Len < 1e-3f) a = new V3(1, 0, 0) - b * b.X; }
            a = a.Norm;
            V3 c = V3.Cross(a, b);
            Xf r = new Xf();
            r.Ax = a.X; r.Ay = a.Y; r.Az = a.Z; r.Bx = b.X; r.By = b.Y; r.Bz = b.Z; r.Cx = c.X; r.Cy = c.Y; r.Cz = c.Z;
            return r;
        }

        /// <summary>Two-bone reach: from root a toward target t with segment lengths l1, l2, bending toward pole.</summary>
        public static void Ik(V3 a, V3 t, float l1, float l2, V3 pole, out V3 joint, out V3 end)
        {
            V3 d = t - a;
            float len = d.Len;
            V3 dn = len > 1e-4f ? d * (1f / len) : new V3(0, -1, 0);
            float maxL = l1 + l2 - 0.02f, minL = Math.Abs(l1 - l2) + 0.3f;
            if (len > maxL) len = maxL; if (len < minL) len = minL;
            end = a + dn * len;
            float ca = M.Clamp((l1 * l1 + len * len - l2 * l2) / (2 * l1 * len), -1f, 1f);
            float sa = (float)Math.Sqrt(Math.Max(0, 1 - ca * ca));
            V3 p = pole - dn * V3.Dot(pole, dn);
            if (p.Len < 1e-4f) { p = new V3(1, 0, 0) - dn * dn.X; if (p.Len < 1e-4f) p = new V3(0, 0, 1); }
            p = p.Norm;
            joint = a + dn * (l1 * ca) + p * (l1 * sa);
        }

        static int Mul(int c, float k)
        {
            int r = M.Clamp((int)(((c >> 16) & 255) * k), 0, 255), g = M.Clamp((int)(((c >> 8) & 255) * k), 0, 255), b = M.Clamp((int)((c & 255) * k), 0, 255);
            return r << 16 | g << 8 | b;
        }

        // humanoids
        public static void Build(HumanSpec s)
        {
            if (s.Built) return;
            s.Built = true;
            s.TorsoM = new MeshB(); BuildTorso(s, s.TorsoM);
            s.HeadM = new MeshB(); BuildHead(s, s.HeadM);
            float b = s.Bulk, th = s.Thin ? 0.55f : 1f;
            // arms hang along -Y from the shoulder joint
            for (int side = 0; side < 2; side++)
            {
                MeshB u = new MeshB();
                u.Layer = Mat3.Cloth; u.Col = s.Sleeve;
                u.Cyl(0, 0, -UpperLen, 0.4f, 1.0f * b * th, 1.22f * b * th, 6, false, false, 0);
                if (s.Shoulder >= 0)
                {
                    u.Layer = Mat3.Metal; u.Col = s.Shoulder;
                    u.Blob(0, 0.2f, 0, 2.0f * b, 1.6f * b, 2.0f * b, 1, 11, 0.03f);
                    u.Layer = Mat3.Metal; u.Col = Mul(s.Shoulder, 0.75f);
                    u.Cyl(0, 0, -1.6f, -0.8f, 2.05f * b, 1.95f * b, 8, false, false, 0);
                }
                else
                {
                    u.Blob(0, 0.1f, 0, 1.35f * b * th, 1.35f * b * th, 1.35f * b * th, 0, 5, 0);
                }
                if (side == 0) s.UpperArmR = u; else s.UpperArmL = u;
            }
            s.ForeArm = new MeshB();
            s.ForeArm.Layer = Mat3.Cloth; s.ForeArm.Col = s.Sleeve;
            s.ForeArm.Cyl(0, 0, -ForeLen, 0.3f, 0.85f * b * th, 1.0f * b * th, 6, false, false, 0);
            s.ForeArm.Blob(0, 0, 0, 1.05f * b * th, 1.05f * b * th, 1.05f * b * th, 0, 3, 0);
            s.ForeArm.Layer = 0; s.ForeArm.Col = s.Hand;
            s.ForeArm.Blob(0.15f, -ForeLen - 0.75f, 0, 1.0f * Math.Max(0.8f, th), 1.05f * Math.Max(0.8f, th), 0.95f * Math.Max(0.8f, th), 0, 9, 0.05f);
            if (!s.Robe)
            {
                s.Thigh = new MeshB();
                s.Thigh.Layer = Mat3.Cloth; s.Thigh.Col = s.Legs;
                s.Thigh.Cyl(0, 0, -ThighLen, 0.6f, 1.15f * b * th, 1.5f * b * th, 6, false, false, 0);
                s.Thigh.Blob(0, -ThighLen, 0, 1.18f * b * th, 1.18f * b * th, 1.18f * b * th, 0, 4, 0);
                s.Shin = new MeshB();
                s.Shin.Layer = Mat3.Cloth; s.Shin.Col = s.Legs;
                s.Shin.Cyl(0, 0, -ShinLen, 0.2f, 0.95f * b * th, 1.15f * b * th, 6, false, false, 0);
                s.Boot = new MeshB();
                s.Boot.Layer = Mat3.Cloth; s.Boot.Col = s.Boots;
                float bw = 1.15f * Math.Max(0.75f, b * th);
                Taper(s.Boot, 0.5f, 0, 0, 1.0f, 1.9f, bw, 1.75f, bw * 0.95f);
                Taper(s.Boot, 0.1f, 0, 1.0f, 2.9f, 1.25f, bw * 0.95f, 1.15f, bw * 0.9f);
            }
            else
            {
                s.Skirt = new MeshB();
                s.Skirt.Layer = Mat3.Cloth; s.Skirt.Col = s.RobeCol;
                float bot = -HipY + 0.25f;
                s.Skirt.Lathe(0, 0, new float[] { bot, 4.9f * b, -6f, 4.1f * b, 0.8f, 3.1f * b }, 10, false, M.PI / 10);
                if (s.RobeTrim >= 0)
                {
                    s.Skirt.Col = s.RobeTrim;
                    s.Skirt.Cyl(0, 0, bot - 0.05f, bot + 1.1f, 4.98f * b, 4.9f * b, 10, false, false, M.PI / 10);
                    float k = (float)Math.Cos(M.PI / 10);
                    s.Skirt.Quad(new V3(4.95f * b * k + 0.12f, bot + 0.6f, -0.85f), new V3(4.95f * b * k + 0.12f, bot + 0.6f, 0.85f),
                                 new V3(3.15f * b * k + 0.12f, 0.6f, 0.75f), new V3(3.15f * b * k + 0.12f, 0.6f, -0.75f));
                }
            }
            if (s.Cape >= 0)
            {
                s.CapeM = new MeshB();
                s.CapeM.Layer = Mat3.Cloth; s.CapeM.Col = s.Cape;
                float w0 = 3.4f * b, w1 = 4.3f * b, L = s.CapeLen;
                V3 a0 = new V3(0, 0, -w0), a1 = new V3(0, 0, w0), m0 = new V3(-0.7f, -L * 0.5f, -w1 * 0.95f), m1 = new V3(-0.7f, -L * 0.5f, w1 * 0.95f);
                V3 e0 = new V3(-1.6f, -L, -w1), e1 = new V3(-1.6f, -L, w1);
                s.CapeM.Quad(a1, a0, m0, m1); s.CapeM.Quad(m1, m0, e0, e1);
                s.CapeM.Col = Mul(s.Cape, 0.8f);
                s.CapeM.Cyl(0.6f, 0, -0.6f, 0.7f, 2.7f * b, 2.9f * b, 8, false, false, 0);
            }
            if (s.Scarf >= 0)
            {
                s.ScarfM = new MeshB();
                s.ScarfM.Layer = Mat3.Cloth; s.ScarfM.Col = s.Scarf;
                V3[] pts = { new V3(0, 0, 0), new V3(-2.4f, -0.9f, 0.5f), new V3(-4.6f, -2.1f, 1.1f), new V3(-6.6f, -3.3f, 1.7f) };
                for (int i = 0; i + 1 < pts.Length; i++) Strip(s.ScarfM, pts[i], pts[i + 1], 1.7f - i * 0.25f, new V3(0, 1, 0.4f));
            }
        }

        static void BuildTorso(HumanSpec s, MeshB m)
        {
            float b = s.Bulk;
            bool skel = s.Thin;
            // pelvis
            m.Layer = Mat3.Cloth; m.Col = s.Robe ? s.RobeCol : s.Legs;
            Taper(m, 0, 0, -1.4f, 0.8f, 1.8f * b, 2.5f * b, 1.95f * b, 2.8f * b);
            // chest
            m.Layer = s.TorsoLayer; m.Col = s.Torso;
            if (skel)
            {
                m.Col = s.Legs; m.Cyl(0, 0, 0.6f, 4.0f, 0.8f, 0.8f, 5, false, false, 0);
                m.Col = s.Torso;
                m.Lathe(0, 0, new float[] { 3.6f, 1.9f * b, 5.5f, 2.6f * b, 7.6f, 2.8f * b, 8.9f, 2.2f * b }, 8, false, M.PI / 8);
                m.Layer = 0; m.Col = 0x1A1722;
                for (int k = 0; k < 3; k++) m.Box(1.0f, 4.6f + k * 1.3f, -2.2f * b, 2.9f * b, 5.1f + k * 1.3f, 2.2f * b);
                m.Layer = s.TorsoLayer; m.Col = s.Torso;
            }
            else
            {
                Taper(m, 0, 0, 0.5f, 5.5f, 1.95f * b, 2.85f * b, 2.3f * b, 3.6f * b);
                Taper(m, 0, 0, 5.5f, 8.3f, 2.3f * b, 3.6f * b, 2.25f * b, 3.9f * b);
                Taper(m, 0, 0, 8.3f, 9.4f, 2.25f * b, 3.9f * b, 1.5f * b, 2.4f * b);
            }
            // neck
            m.Layer = 0; m.Col = s.Skin;
            m.Cyl(0.25f, 0, 8.6f, 10.4f, 1.15f * Math.Max(0.8f, b * 0.9f), 1.05f * Math.Max(0.8f, b * 0.9f), 6, false, false, 0);
            // belt
            if (!skel)
            {
                m.Layer = Mat3.Cloth; m.Col = s.Belt;
                Taper(m, 0, 0, 0.3f, 1.5f, 2.08f * b, 2.98f * b, 2.12f * b, 3.04f * b);
                m.Layer = Mat3.Metal; m.Col = s.Buckle;
                m.Box(2.0f * b, 0.45f, -0.7f, 2.4f * b, 1.35f, 0.7f);
            }
            if (s.Tabard >= 0)
            {
                m.Layer = Mat3.Cloth; m.Col = s.Tabard;
                float x = 2.32f * b;
                m.Box(x - 0.15f, -5.2f, -1.9f, x + 0.14f, 7.6f, 1.9f);
                m.Box(-x - 0.14f, -4.6f, -1.9f, -x + 0.15f, 7.6f, 1.9f);
                if (s.Emblem >= 0)
                {
                    m.Layer = 0; m.Col = s.Emblem; m.Em = 50;
                    m.Box(x + 0.1f, 3.6f, -0.9f, x + 0.35f, 6.0f, 0.9f);
                    m.Box(x + 0.1f, 4.4f, -1.4f, x + 0.35f, 5.2f, 1.4f);
                    m.Em = 0;
                }
            }
            else if (s.Emblem >= 0)
            {
                m.Layer = 0; m.Col = s.Emblem; m.Em = 120;
                float x = 2.33f * b;
                m.Box(x, 2.6f, -0.5f, x + 0.3f, 6.6f, 0.5f);
                m.Box(x, 4.8f, -1.6f, x + 0.3f, 5.6f, 1.6f);
                m.Em = 0;
            }
            if (s.Strap >= 0)
            {
                m.Layer = Mat3.Cloth; m.Col = s.Strap;
                Strip(m, new V3(2.0f * b + 0.25f, 1.2f, 2.6f * b), new V3(2.3f * b + 0.25f, 8.4f, -2.9f * b), 1.3f, new V3(1, 0, 0));
                Strip(m, new V3(-2.3f * b - 0.25f, 8.4f, -2.9f * b), new V3(-2.0f * b - 0.25f, 1.2f, 2.6f * b), 1.3f, new V3(-1, 0, 0));
                m.Layer = Mat3.Metal; m.Col = s.Buckle;
                m.Box(2.2f * b + 0.2f, 4.4f, -0.5f, 2.2f * b + 0.55f, 5.4f, 0.5f);
            }
            if (s.Apron >= 0)
            {
                m.Layer = Mat3.Cloth; m.Col = s.Apron;
                float x = 2.38f * b;
                m.Quad(new V3(x + 0.1f, -6.5f, -2.4f * b), new V3(x + 0.1f, -6.5f, 2.4f * b), new V3(x, 6.8f, 1.8f * b), new V3(x, 6.8f, -1.8f * b));
                Strip(m, new V3(x - 0.2f, 6.8f, 1.6f * b), new V3(0.2f, 9.6f, 1.9f * b), 0.7f, new V3(0, 0, 1));
                Strip(m, new V3(x - 0.2f, 6.8f, -1.6f * b), new V3(0.2f, 9.6f, -1.9f * b), 0.7f, new V3(0, 0, 1));
            }
            if (s.Mantle >= 0)
            {
                m.Layer = Mat3.Cloth; m.Col = s.Mantle;
                m.Lathe(0, 0, new float[] { 5.6f, 4.6f * b, 7.6f, 4.25f * b, 9.2f, 3.1f * b, 10.1f, 1.7f }, 9, false, 0.2f);
            }
            if (s.Scarf >= 0)
            {
                m.Layer = Mat3.Cloth; m.Col = s.Scarf;
                m.Cyl(0.2f, 0, 8.7f, 10.3f, 2.5f * b, 2.0f, 8, true, false, 0);
            }
            if (s.Quiver)
            {
                m.Layer = Mat3.Bark; m.Col = 0x6A4A34;
                m.T = Xf.Move(-2.9f * b, 1.6f, -0.6f) * Xf.RotX(0.42f);
                m.Cyl(0, 0, 0, 9.2f, 1.35f, 1.55f, 7, false, true, 0);
                m.Layer = Mat3.Metal; m.Col = 0x9A8058;
                m.Cyl(0, 0, 8.6f, 9.4f, 1.62f, 1.62f, 7, false, false, 0);
                m.Layer = 0;
                int[] fl = { 0xCF4150, 0xE8E0C4, 0xCF4150, 0xE8E0C4 };
                for (int k = 0; k < 4; k++)
                {
                    m.Col = fl[k];
                    float a = k * 1.6f;
                    m.BoxC(M.Cos(a) * 0.7f, 10.6f + (k % 2) * 0.5f, M.Sin(a) * 0.7f, 0.5f, 2.6f, 1.2f);
                }
                m.T = Xf.Id;
            }
        }

        static void Face(MeshB m, HumanSpec s, float cx, float cy, float r, int skin)
        {
            m.Layer = 0; m.Col = skin;
            m.Blob(cx, cy, 0, r * 0.95f, r, r * 0.92f, 1, 21, 0.02f);
            m.Col = Mul(skin, 0.9f);
            m.Blob(cx + r * 0.9f, cy - 0.42f, 0, 0.5f, 0.66f, 0.4f, 0, 22, 0);
            float ex = cx + r * 0.86f, ey = cy + 0.36f;
            if (s.EyeGlow)
            {
                m.Col = s.Eye; m.Em = 255;
                m.Blob(ex + 0.05f, ey, 1.0f, 0.3f, 0.34f, 0.4f, 0, 23, 0);
                m.Blob(ex + 0.05f, ey, -1.0f, 0.3f, 0.34f, 0.4f, 0, 24, 0);
            }
            else
            {
                m.Col = 0xE2DAD0;
                m.Blob(ex, ey, 1.0f, 0.22f, 0.27f, 0.42f, 0, 23, 0);
                m.Blob(ex, ey, -1.0f, 0.22f, 0.27f, 0.42f, 0, 24, 0);
                m.Col = s.Eye;
                m.Blob(ex + 0.1f, ey - 0.02f, 0.94f, 0.2f, 0.24f, 0.22f, 0, 25, 0);
                m.Blob(ex + 0.1f, ey - 0.02f, -0.94f, 0.2f, 0.24f, 0.22f, 0, 26, 0);
                m.Col = Mul(skin, 0.52f);
                m.BoxC(ex + 0.05f, ey + 0.55f, 1.02f, 0.3f, 0.2f, 0.8f);
                m.BoxC(ex + 0.05f, ey + 0.55f, -1.02f, 0.3f, 0.2f, 0.8f);
            }
            m.Em = 0;
        }

        static void HairCap(MeshB m, int col, float cx, float cy, float r)
        {
            m.Layer = Mat3.Fur; m.Col = col;
            m.Blob(cx - 0.6f, cy + 0.75f, 0, r * 1.02f, r * 0.85f, r * 1.05f, 1, 31, 0.05f);
            m.Blob(cx - 1.4f, cy - 0.4f, 0, r * 0.75f, r * 0.8f, r * 0.95f, 0, 33, 0.05f);
        }

        static void Hood(MeshB m, int col, float cx, float cy)
        {
            m.Layer = Mat3.Cloth; m.Col = col;
            m.Blob(cx - 1.3f, cy + 0.45f, 0, 3.7f, 3.85f, 3.75f, 1, 41, 0.03f);
            m.Blob(cx - 1.9f, cy - 2.4f, 0, 2.5f, 2.2f, 3.3f, 0, 43, 0.04f);
            m.Col = Mul(col, 0.7f);
            m.Cyl(cx - 0.4f, 0, cy - 3.1f, cy - 2.2f, 3.2f, 3.0f, 9, false, false, 0);
        }

        static void BuildHead(HumanSpec s, MeshB m)
        {
            float cx = 0.25f, cy = 3.1f;
            switch (s.Head)
            {
                case HumanSpec.HHelm:
                    Face(m, s, cx, cy, 2.95f, s.Skin);
                    m.Layer = Mat3.Metal; m.Col = s.HeadCol;
                    m.Blob(cx - 0.3f, cy + 1.6f, 0, 3.55f, 2.5f, 3.5f, 1, 51, 0.02f);
                    m.Col = s.HeadDark;
                    m.Cyl(cx - 0.3f, 0, cy + 0.75f, cy + 1.25f, 3.62f, 3.56f, 12, false, false, 0);
                    m.Col = s.HeadCol;
                    m.Box(cx + 0.2f, cy - 1.9f, 2.45f, cx + 2.6f, cy + 0.6f, 3.1f);
                    m.Box(cx + 0.2f, cy - 1.9f, -3.1f, cx + 2.6f, cy + 0.6f, -2.45f);
                    m.Col = s.HeadDark;
                    m.Box(cx + 2.7f, cy - 1.3f, -0.32f, cx + 3.3f, cy + 1.0f, 0.32f);
                    m.Layer = Mat3.Cloth; m.Col = s.HeadCol2;
                    for (int k = 0; k <= 6; k++)
                    {
                        float a = k / 6f;
                        float px = cx + 1.5f - a * 5.6f, py = cy + 3.95f - a * a * 2.9f;
                        m.BoxC(px, py, 0, 1.35f, 1.7f - a * 0.4f, 0.95f);
                    }
                    break;
                case HumanSpec.HHood:
                    Face(m, s, cx, cy, 2.95f, s.Skin);
                    Hood(m, s.HeadCol, cx, cy);
                    if (s.HeadCol2 >= 0 && s.Scarf >= 0)
                    {
                        m.Layer = Mat3.Cloth; m.Col = s.HeadCol2;
                        m.Blob(cx + 1.25f, cy - 1.05f, 0, 1.85f, 1.3f, 2.6f, 0, 45, 0.04f);
                    }
                    break;
                case HumanSpec.HPointHood:
                    Face(m, s, cx, cy, 2.85f, 0x0E0C16);
                    Hood(m, s.HeadCol, cx, cy);
                    m.Layer = Mat3.Cloth; m.Col = s.HeadCol;
                    m.T = Xf.Move(cx - 1.4f, cy + 3.0f, 0) * Xf.RotZ(0.8f);
                    m.Cyl(0, 0, 0, 5.6f, 2.5f, 0, 7, false, false, 0);
                    m.T = Xf.Id;
                    break;
                case HumanSpec.HSkull:
                    m.Layer = Mat3.Bone; m.Col = s.Skin;
                    m.Blob(cx, cy + 0.3f, 0, 2.85f, 3.0f, 2.75f, 1, 61, 0.04f);
                    m.Col = Mul(s.Skin, 0.8f);
                    m.Box(cx + 0.5f, cy - 2.6f, -1.6f, cx + 2.75f, cy - 1.3f, 1.6f);
                    m.Layer = 0; m.Col = 0x0E0C14;
                    m.BoxC(cx + 2.45f, cy + 0.45f, 1.1f, 0.9f, 1.15f, 1.1f);
                    m.BoxC(cx + 2.45f, cy + 0.45f, -1.1f, 0.9f, 1.15f, 1.1f);
                    m.BoxC(cx + 2.65f, cy - 0.9f, 0, 0.6f, 0.8f, 0.6f);
                    m.Col = s.Eye; m.Em = 255;
                    m.BoxC(cx + 2.75f, cy + 0.45f, 1.1f, 0.4f, 0.45f, 0.45f);
                    m.BoxC(cx + 2.75f, cy + 0.45f, -1.1f, 0.4f, 0.45f, 0.45f);
                    m.Em = 0;
                    m.Layer = Mat3.Cloth; m.Col = s.HeadCol;
                    m.Blob(cx - 1.4f, cy + 0.9f, 0, 3.0f, 3.45f, 3.45f, 1, 63, 0.12f);
                    break;
                case HumanSpec.HMaskHood:
                    Hood(m, s.HeadCol, cx, cy);
                    m.Layer = Mat3.Bone; m.Col = s.HeadCol2;
                    m.Blob(cx + 0.85f, cy - 0.1f, 0, 2.45f, 3.0f, 2.55f, 1, 65, 0.02f);
                    m.Layer = 0; m.Col = 0x0E0C14;
                    m.BoxC(cx + 3.0f, cy + 0.4f, 1.0f, 0.6f, 0.9f, 1.0f);
                    m.BoxC(cx + 3.0f, cy + 0.4f, -1.0f, 0.6f, 0.9f, 1.0f);
                    m.Col = s.Eye; m.Em = 255;
                    m.BoxC(cx + 3.2f, cy + 0.4f, 1.0f, 0.35f, 0.5f, 0.5f);
                    m.BoxC(cx + 3.2f, cy + 0.4f, -1.0f, 0.35f, 0.5f, 0.5f);
                    m.Em = 0;
                    m.Col = 0x2A1C14; m.BoxC(cx + 3.15f, cy - 1.6f, 0, 0.35f, 1.4f, 0.3f);
                    break;
                case HumanSpec.HHorned:
                    m.Layer = Mat3.Metal; m.Col = s.HeadCol;
                    m.Blob(cx - 0.1f, cy + 0.4f, 0, 3.5f, 3.75f, 3.5f, 1, 71, 0.03f);
                    m.Col = s.HeadDark;
                    m.Box(cx + 1.0f, cy - 3.2f, -2.6f, cx + 3.2f, cy - 0.8f, 2.6f);
                    m.Layer = 0; m.Col = s.Eye; m.Em = 255;
                    m.Box(cx + 3.1f, cy + 0.05f, -1.9f, cx + 3.55f, cy + 0.75f, 1.9f);
                    m.Em = 0;
                    m.Layer = Mat3.Bone; m.Col = s.HeadCol2;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        m.T = Xf.Move(cx - 0.4f, cy + 2.0f, side * 2.7f) * Xf.RotX(side * 1.15f);
                        m.Cyl(0, 0, 0, 3.8f, 1.15f, 0.75f, 6, false, false, 0);
                        m.T = m.T * Xf.Move(0, 3.6f, 0) * Xf.RotX(-side * 1.05f);
                        m.Cyl(0, 0, 0, 3.4f, 0.78f, 0, 6, false, false, 0);
                    }
                    m.T = Xf.Id;
                    break;
                case HumanSpec.HBell:
                    m.Layer = Mat3.Metal; m.Col = s.HeadCol;
                    m.Lathe(cx, 0, new float[] { -0.9f, 4.5f, 0.4f, 4.25f, 2.6f, 3.7f, 5.5f, 3.15f, 7.6f, 2.45f, 8.9f, 1.2f }, 12, true, 0);
                    m.Col = s.HeadDark;
                    m.Cyl(cx, 0, -1.0f, 0.3f, 4.62f, 4.5f, 12, false, false, 0);
                    m.Cyl(cx, 0, 4.2f, 4.7f, 3.5f, 3.45f, 12, false, false, 0);
                    m.Col = s.HeadCol; m.BoxC(cx, 9.6f, 0, 1.0f, 1.8f, 2.4f);
                    m.Layer = 0; m.Col = 0x0B0A10;
                    m.Box(cx + 3.05f, 2.2f, -2.2f, cx + 4.05f, 3.6f, 2.2f);
                    m.Col = s.Eye; m.Em = 255;
                    m.BoxC(cx + 3.95f, 2.9f, 1.15f, 0.5f, 0.55f, 0.9f);
                    m.BoxC(cx + 3.95f, 2.9f, -1.15f, 0.5f, 0.55f, 0.9f);
                    m.Em = 0;
                    m.Col = 0x4A8152;
                    m.Box(cx + 2.6f, 4.0f, 1.4f, cx + 3.75f, 6.2f, 1.9f);
                    m.Box(cx + 3.6f, 0.4f, -2.1f, cx + 4.3f, 2.0f, -1.5f);
                    break;
                case HumanSpec.HCap:
                    Face(m, s, cx, cy, 2.95f, s.Skin);
                    HairCap(m, Mul(s.HeadCol, 0.8f), cx, cy - 0.3f, 2.9f);
                    m.Layer = Mat3.Cloth; m.Col = s.HeadCol;
                    m.Cyl(cx - 0.3f, 0, cy + 1.3f, cy + 3.0f, 3.2f, 2.9f, 9, true, false, 0);
                    m.Box(cx + 1.2f, cy + 1.25f, -2.3f, cx + 4.3f, cy + 1.7f, 2.3f);
                    break;
                case HumanSpec.HBeard:
                    Face(m, s, cx, cy, 2.95f, s.Skin);
                    m.Layer = Mat3.Fur; m.Col = s.HeadCol;
                    m.Blob(cx + 1.2f, cy - 1.45f, 0, 2.15f, 2.05f, 2.6f, 0, 81, 0.08f);
                    m.Blob(cx - 1.2f, cy - 0.2f, 0, 1.6f, 1.3f, 2.9f, 0, 83, 0.06f);
                    m.BoxC(cx + 2.6f, cy + 1.2f, 1.0f, 0.5f, 0.45f, 1.2f);
                    m.BoxC(cx + 2.6f, cy + 1.2f, -1.0f, 0.5f, 0.45f, 1.2f);
                    break;
                case HumanSpec.HBun:
                    Face(m, s, cx, cy, 2.95f, s.Skin);
                    HairCap(m, s.HeadCol, cx, cy, 2.95f);
                    m.Blob(cx - 2.5f, cy + 2.0f, 0, 1.55f, 1.55f, 1.55f, 0, 85, 0.05f);
                    break;
                case HumanSpec.HOldBeard:
                    Face(m, s, cx, cy, 2.95f, s.Skin);
                    m.Layer = Mat3.Fur; m.Col = s.HeadCol;
                    m.Blob(cx - 0.8f, cy + 0.9f, 0, 2.9f, 2.4f, 3.05f, 1, 87, 0.05f);
                    m.Blob(cx + 1.35f, cy - 1.9f, 0, 1.95f, 2.7f, 2.45f, 0, 89, 0.08f);
                    break;
                case HumanSpec.HHollow:
                    Face(m, s, cx + 0.2f, cy - 0.2f, 2.8f, s.Skin);
                    m.Layer = 0; m.Col = 0x16141E;
                    m.BoxC(cx + 2.35f, cy - 1.5f, 0, 0.6f, 0.6f, 1.6f);
                    m.Layer = Mat3.Fur; m.Col = 0x3A3A34;
                    m.Blob(cx - 1.2f, cy + 1.6f, 0.8f, 1.5f, 0.9f, 1.4f, 0, 91, 0.3f);
                    m.Blob(cx - 1.8f, cy + 0.6f, -1.6f, 1.2f, 1.2f, 1.0f, 0, 93, 0.3f);
                    break;
                default:
                    Face(m, s, cx, cy, 2.95f, s.Skin);
                    HairCap(m, s.HeadCol, cx, cy, 2.95f);
                    break;
            }
        }

        // the cast
        static HumanSpec[] heroes;
        public static HumanSpec Hero(ClassId c)
        {
            if (heroes == null)
            {
                heroes = new HumanSpec[4];
                HumanSpec w = new HumanSpec();
                w.Head = HumanSpec.HHelm; w.HeadCol = 0x9AA6BA; w.HeadDark = 0x56607A; w.HeadCol2 = 0xD8384A;
                w.Torso = 0x8996AA; w.TorsoLayer = Mat3.Metal; w.Tabard = 0xB02840; w.Emblem = 0xE8BC50; w.Shoulder = 0xB8C2D0;
                w.Sleeve = 0x6A768E; w.Hand = 0x4B546D; w.Belt = 0x6A4A38; w.Legs = 0x5A6680; w.Boots = 0x3A4054; w.Cape = 0x8A1E36; w.CapeLen = 13.5f;
                w.Bulk = 1.08f;
                heroes[0] = w;
                HumanSpec m = new HumanSpec();
                m.Head = HumanSpec.HPointHood; m.HeadCol = 0x4E3E84; m.Eye = 0xF7BE52; m.EyeGlow = true;
                m.Robe = true; m.RobeCol = 0x4A3A7C; m.RobeTrim = 0xDA5A2A; m.Torso = 0x4A3A7C; m.Belt = 0xA83424; m.Buckle = 0xF7BE52;
                m.Sleeve = 0x4A3A7C; m.Hand = 0xE0AE86; m.Mantle = 0x3E3068; m.Bulk = 0.95f;
                heroes[1] = m;
                HumanSpec r = new HumanSpec();
                r.Head = HumanSpec.HHood; r.HeadCol = 0x464E68; r.HeadCol2 = 0xB02840; r.Scarf = 0xB02840;
                r.Torso = 0x464E68; r.Strap = 0x8E6A4E; r.Belt = 0x6A4A38; r.Buckle = 0xB8C2D0;
                r.Sleeve = 0x3E4560; r.Hand = 0x2E2843; r.Legs = 0x3A3F55; r.Boots = 0x2A2438; r.Bulk = 0.95f;
                heroes[2] = r;
                HumanSpec g = new HumanSpec();
                g.Head = HumanSpec.HHood; g.HeadCol = 0x3A7454; g.HeadCol2 = -1;
                g.Torso = 0x8A6A4E; g.Mantle = 0x3A7454; g.Belt = 0x4A3430; g.Buckle = 0xC8A050;
                g.Sleeve = 0x7A5A44; g.Hand = 0xE0AE86; g.Legs = 0x6A4C3C; g.Boots = 0x44302E; g.Cape = 0x2E5E50; g.CapeLen = 14f; g.Quiver = true;
                heroes[3] = g;
            }
            return heroes[(int)c & 3];
        }

        static HumanSpec[] hollows;
        static HumanSpec skeleton, acolyte, knight, prior;
        public static HumanSpec Foe(Enemy e)
        {
            switch (e.Kind)
            {
                case EnemyKind.Hollow:
                    if (hollows == null)
                    {
                        hollows = new HumanSpec[3];
                        int[] cloth = { 0x5A4038, 0x434A64, 0x4E5E38 };
                        for (int i = 0; i < 3; i++)
                        {
                            HumanSpec h = new HumanSpec();
                            h.Head = HumanSpec.HHollow; h.Skin = 0x93A396; h.Eye = 0xC9D3F5; h.EyeGlow = true;
                            h.Torso = cloth[i]; h.Sleeve = cloth[i]; h.Hand = 0x93A396; h.Belt = 0x2E2126; h.Buckle = 0x6A6050;
                            h.Legs = 0x6B7C74; h.Boots = 0x4A5A56; h.Strap = i == 1 ? 0x7A2030 : -1; h.Lean = 0.38f; h.Bulk = 0.95f;
                            hollows[i] = h;
                        }
                    }
                    return hollows[Math.Abs(e.Variant) % 3];
                case EnemyKind.Archer:
                    if (skeleton == null)
                    {
                        HumanSpec k = new HumanSpec();
                        k.Head = HumanSpec.HSkull; k.Skin = 0xE8E0C4; k.Eye = 0x9BDCF4; k.EyeGlow = true; k.HeadCol = 0x2E2648;
                        k.Torso = 0xD8D0B4; k.TorsoLayer = Mat3.Bone; k.Sleeve = 0xD0C8AC; k.Hand = 0xD8D0B4; k.Legs = 0xC8BE9F; k.Boots = 0x9C927A;
                        k.Thin = true; k.Bulk = 0.85f; k.Mantle = 0x2A2240;
                        skeleton = k;
                    }
                    return skeleton;
                case EnemyKind.Acolyte:
                    if (acolyte == null)
                    {
                        HumanSpec a = new HumanSpec();
                        a.Head = HumanSpec.HMaskHood; a.HeadCol = 0x5E1A32; a.HeadCol2 = 0xD8CCAA; a.Eye = 0xC8C0FF; a.EyeGlow = true;
                        a.Robe = true; a.RobeCol = 0x4E1628; a.RobeTrim = 0x6A58A8; a.Torso = 0x4E1628; a.Belt = 0x8A6A34; a.Buckle = 0xD2AC52;
                        a.Sleeve = 0x4E1628; a.Hand = 0xC8BE9F; a.Mantle = 0x5E1A32;
                        acolyte = a;
                    }
                    return acolyte;
                case EnemyKind.Knight:
                    if (knight == null)
                    {
                        HumanSpec n = new HumanSpec();
                        n.Head = HumanSpec.HHorned; n.HeadCol = 0x4E5872; n.HeadDark = 0x2A2E40; n.HeadCol2 = 0x9AA0B0; n.Eye = 0x9BDCF4; n.EyeGlow = true;
                        n.Torso = 0x4E5872; n.TorsoLayer = Mat3.Metal; n.Tabard = 0x342C4C; n.Emblem = 0x8A74D8; n.Shoulder = 0x5E6A86;
                        n.Sleeve = 0x46506A; n.Hand = 0x363C52; n.Belt = 0x8A6A34; n.Legs = 0x46506A; n.Boots = 0x2E2843;
                        n.Cape = 0x2A2240; n.CapeLen = 14.5f; n.Bulk = 1.25f;
                        knight = n;
                    }
                    return knight;
                default:
                    if (prior == null)
                    {
                        HumanSpec p = new HumanSpec();
                        p.Head = HumanSpec.HBell; p.HeadCol = 0xB08A44; p.HeadDark = 0x6A4E28; p.Eye = 0xDDE6FF; p.EyeGlow = true;
                        p.Robe = true; p.RobeCol = 0x2C2644; p.RobeTrim = 0x6A58A8; p.Torso = 0x2C2644; p.Belt = 0x6A4E28; p.Buckle = 0xD2AC52;
                        p.Sleeve = 0x2C2644; p.Hand = 0x93A396; p.Mantle = 0x221C36; p.Bulk = 1.15f;
                        prior = p;
                    }
                    return prior;
            }
        }

        static readonly Dictionary<ActorSprites, HumanSpec> npcs = new Dictionary<ActorSprites, HumanSpec>();
        public static HumanSpec Villager(ActorSprites spr)
        {
            HumanSpec s;
            if (spr != null && npcs.TryGetValue(spr, out s)) return s;
            s = new HumanSpec();
            if (spr == Assets.Elder)
            {
                s.Head = HumanSpec.HBun; s.HeadCol = 0xC8D0DA; s.Skin = 0xE0AE86;
                s.Robe = true; s.RobeCol = 0x2E5450; s.RobeTrim = 0xC8A050; s.Torso = 0x2E5450; s.Sleeve = 0x2E5450; s.Belt = 0xA8823C; s.Mantle = 0x3A6460;
            }
            else if (spr == Assets.Smith)
            {
                s.Head = HumanSpec.HBeard; s.HeadCol = 0x5A3A2E; s.Skin = 0xC8906C; s.Bulk = 1.2f;
                s.Torso = 0x434A64; s.Apron = 0x6E4E3A; s.Sleeve = 0xC8906C; s.Hand = 0xC8906C; s.Legs = 0x4A3A34; s.Boots = 0x2E2126;
            }
            else if (spr == Assets.Herbalist)
            {
                s.Head = HumanSpec.HHood; s.HeadCol = 0x62763E; s.HeadCol2 = -1; s.Skin = 0xE0AE86;
                s.Robe = true; s.RobeCol = 0x4A5A34; s.Torso = 0x4A5A34; s.Sleeve = 0x4A5A34; s.Apron = 0xC8BE9F; s.Belt = 0x6A4A38;
            }
            else if (spr == Assets.Innkeeper)
            {
                s.Head = HumanSpec.HCap; s.HeadCol = 0x6E4E3A; s.Skin = 0xE0AE86; s.Bulk = 1.25f;
                s.Torso = 0x7A2828; s.Sleeve = 0x7A2828; s.Apron = 0xD8CCAA; s.Legs = 0x5F4337; s.Boots = 0x2E2126;
            }
            else if (spr == Assets.Warden)
            {
                s.Head = HumanSpec.HOldBeard; s.HeadCol = 0xA8B2C2; s.Skin = 0xC8906C;
                s.Torso = 0x7A2236; s.Sleeve = 0x5A6680; s.Belt = 0xA8823C; s.Legs = 0x4B546D; s.Boots = 0x2E2126; s.Cape = 0x4B546D; s.CapeLen = 13f;
            }
            else if (spr == Assets.VillagerA)
            {
                s.Head = HumanSpec.HCap; s.HeadCol = 0x5A4038; s.Skin = 0xE0AE86;
                s.Torso = 0x4E5E38; s.Sleeve = 0x4E5E38; s.Belt = 0x8A6A34; s.Legs = 0x4B546D; s.Boots = 0x2E2126;
            }
            else if (spr == Assets.VillagerB)
            {
                s.Head = HumanSpec.HBun; s.HeadCol = 0x7A2C24; s.Skin = 0xF0CCA8;
                s.Robe = true; s.RobeCol = 0x56607A; s.Torso = 0x56607A; s.Sleeve = 0x56607A; s.Apron = 0xC8BE9F; s.Belt = 0x6A4A38;
            }
            else if (spr == Assets.Child)
            {
                s.Head = HumanSpec.HHair; s.HeadCol = 0xB03A28; s.Skin = 0xF0CCA8; s.HeadScale = 1.3f;
                s.Torso = 0x8E6A4E; s.Sleeve = 0x8E6A4E; s.Belt = 0x4A3430; s.Legs = 0x5A4038; s.Boots = 0x2E2126; s.Scarf = 0xC8A050;
            }
            else if (spr == Assets.Ghost)
            {
                s.Head = HumanSpec.HHood; s.HeadCol = 0xC9D3F5; s.HeadCol2 = -1; s.Skin = 0xEAF3FF; s.Eye = 0x3A2E5E; s.Spirit = true;
                s.Robe = true; s.RobeCol = 0xA7A0DC; s.Torso = 0xA7A0DC; s.Sleeve = 0xB8B4E8; s.Hand = 0xEAF3FF; s.Belt = 0x7F6CB6; s.Mantle = 0xC9D3F5;
            }
            if (spr != null) npcs[spr] = s;
            return s;
        }

        // weapons (along +X, grip at the origin, blade width along Y)
        public static readonly int[] TierCol = { 0x9AA0A8, 0xC8D0DA, 0xE0C070, 0xB8A0F0 };
        static readonly Dictionary<int, MeshB> weapons = new Dictionary<int, MeshB>();

        static MeshB Cached(int key, Action<MeshB> build)
        {
            MeshB m;
            if (weapons.TryGetValue(key, out m)) return m;
            m = new MeshB(); build(m);
            m.T = Xf.Id; m.Em = 0; m.Layer = 0;
            weapons[key] = m;
            return m;
        }

        static void AlongX(MeshB m, float x0, float x1, float r0, float r1, int sides)
        {
            Xf keep = m.T;
            m.T = keep * Xf.RotZ(-M.PI / 2);
            m.Cyl(0, 0, x0, x1, r0, r1, sides, true, true, 0);
            m.T = keep;
        }

        public static MeshB Weapon(int type, int tier)
        {
            tier = M.Clamp(tier, 0, 3);
            int key = type * 8 + tier;
            int blade = TierCol[tier];
            byte em = (byte)(tier == 3 ? 110 : 0);
            int accent = tier >= 2 ? 0xE8B84A : 0x6A6A78;
            switch (type)
            {
                case 0: return Cached(key, delegate(MeshB m)
                {
                    m.Layer = Mat3.Cloth; m.Col = 0x4A3226; AlongX(m, -3.3f, 0, 0.55f, 0.55f, 6);
                    m.Layer = Mat3.Metal; m.Col = accent; m.Blob(-3.8f, 0, 0, 0.85f, 0.85f, 0.85f, 0, 1, 0);
                    m.BoxC(0, 0, 0, 0.9f, 5.2f, 1.1f);
                    m.Col = blade; m.Em = em;
                    Blade(m, 0.4f, 15.5f, 18.2f, 1.1f, 0.9f, 0.35f);
                    m.Em = 0;
                });
                case 1: return Cached(key, delegate(MeshB m)
                {
                    m.Layer = Mat3.Bark; m.Col = 0x6A4A30; AlongX(m, -6.5f, 19.5f, 0.68f, 0.62f, 6);
                    m.Layer = Mat3.Metal; m.Col = accent; AlongX(m, -6.9f, -5.8f, 0.85f, 0.85f, 6); AlongX(m, 12.4f, 13.2f, 0.82f, 0.82f, 6);
                    m.Col = blade; m.Em = em;
                    Extrude(m, new float[] { 13.2f, 0.3f, 18.2f, 0.3f, 19.8f, 3.8f, 18.6f, 7.2f, 14.6f, 6.4f, 13.2f, 2.6f }, -0.45f, 0.45f);
                    Extrude(m, new float[] { 14.2f, -0.3f, 17.4f, -0.3f, 15.8f, -3.0f }, -0.35f, 0.35f);
                    m.Em = 0;
                });
                case 2: return Cached(key, delegate(MeshB m)
                {
                    m.Layer = Mat3.Bark; m.Col = tier == 3 ? 0x3A2E5E : 0x5A3E2C; AlongX(m, -12f, 20.5f, 0.6f, 0.68f, 6);
                    m.Layer = Mat3.Metal; m.Col = accent;
                    AlongX(m, 19.6f, 20.8f, 0.9f, 0.9f, 6);
                    for (int k = 0; k < 3; k++)
                    {
                        float a = k * M.TAU / 3;
                        m.T = Xf.Move(20.4f, 0, 0) * Xf.Rot(new V3(0, -M.Sin(a), M.Cos(a)), 0.5f) * Xf.RotZ(-M.PI / 2);
                        m.Cyl(0, 0, 0, 3.6f, 0.38f, 0.1f, 4, false, false, a);
                    }
                    m.T = Xf.Id;
                    m.Layer = 0; m.Col = tier == 3 ? 0xC8A8FF : 0xFFA040; m.Em = 255;
                    m.Blob(22.6f, 0, 0, 1.45f, 1.45f, 1.45f, 1, 7, 0.05f);
                    m.Em = 0;
                });
                case 3: return Cached(key, delegate(MeshB m)
                {
                    m.Layer = Mat3.Bark; m.Col = 0x5A3E2C; AlongX(m, -2.5f, 9.5f, 0.5f, 0.32f, 6);
                    m.Layer = Mat3.Metal; m.Col = accent; AlongX(m, -0.4f, 0.6f, 0.6f, 0.6f, 6);
                    m.Layer = 0; m.Col = tier == 3 ? 0xC8A8FF : 0xFFB060; m.Em = 255;
                    m.Blob(10.3f, 0, 0, 0.95f, 0.95f, 0.95f, 0, 3, 0);
                    m.Em = 0;
                });
                case 4: return Cached(key, delegate(MeshB m)
                {
                    m.Layer = Mat3.Cloth; m.Col = 0x3A2A22; AlongX(m, -2.6f, 0, 0.45f, 0.45f, 5);
                    m.Layer = Mat3.Metal; m.Col = accent; m.BoxC(0, 0, 0, 0.7f, 2.8f, 0.8f);
                    m.Col = blade; m.Em = em;
                    Blade(m, 0.35f, 6.8f, 8.8f, 0.75f, 0.6f, 0.25f);
                    m.Em = 0;
                });
                case 5: return Cached(key, delegate(MeshB m)
                {
                    m.Layer = Mat3.Cloth; m.Col = 0x3A2A22; AlongX(m, -2.8f, 0, 0.48f, 0.48f, 5);
                    m.Layer = Mat3.Metal; m.Col = accent; m.BoxC(0, 0, 0, 0.7f, 3.2f, 0.9f);
                    m.Col = blade; m.Em = em;
                    float x = 0.35f;
                    for (int k = 0; k < 5; k++)
                    {
                        float x1 = x + 1.8f, off = (k % 2 == 0 ? 0.35f : -0.35f);
                        m.T = Xf.Move(0, off, 0);
                        Blade(m, x, x1, x1 + 0.01f, 0.8f - k * 0.05f, 0.75f - k * 0.05f, 0.24f);
                        x = x1;
                    }
                    m.T = Xf.Id;
                    Blade(m, x, x + 0.5f, x + 2.2f, 0.55f, 0.5f, 0.22f);
                    m.Em = 0;
                });
                default:
                    return Bow(type == 6 ? 13.5f : 9.8f, tier, 0.6f, false);
            }
        }

        /// <summary>A bow held at its grip: limbs along ±Y bowing forward (+X); draw pulls the string back (0..1).</summary>
        public static MeshB Bow(float half, int tier, float draw, bool bone)
        {
            int d = M.Clamp((int)Math.Round(draw * 4), 0, 4);
            int key = 1000 + (int)(half * 10) * 20 + tier * 5 + d + (bone ? 50000 : 0);
            float drawAmt = d / 4f;
            return Cached(key, delegate(MeshB m)
            {
                int wood = bone ? 0xC8BE9F : (tier == 3 ? 0x5A4A8A : (tier == 2 ? 0x8A5A34 : 0x7A5232));
                m.Layer = bone ? Mat3.Bone : Mat3.Bark; m.Col = wood;
                int n = 6;
                V3 prev = new V3(0, 0, 0);
                for (int side = -1; side <= 1; side += 2)
                {
                    prev = new V3(0.4f, 0, 0);
                    for (int k = 1; k <= n; k++)
                    {
                        float t = k / (float)n;
                        float y = side * half * t;
                        float x = 0.4f - 4.2f * (half / 13.5f) * t * t + (k == n ? 0.9f : 0);
                        V3 p = new V3(x, y, 0);
                        V3 dir = p - prev;
                        m.T = Xf.Move(prev) * Seg(-dir, new V3(1, 0, 0));
                        m.Cyl(0, 0, 0, dir.Len, 0.5f - t * 0.22f, 0.5f - t * 0.22f, 5, false, false, 0);
                        prev = p;
                    }
                }
                m.T = Xf.Id;
                m.Layer = Mat3.Cloth; m.Col = bone ? 0x6F6756 : 0x4A3226;
                AlongX(m, -0.4f, 0.9f, 0, 0, 3);
                m.BoxC(0.2f, 0, 0, 1.4f, 2.6f, 1.2f);
                if (tier == 3 && !bone) { m.Layer = 0; m.Col = 0xB8A0F0; m.Em = 200; m.Blob(0.6f, half * 0.55f, 0, 0.5f, 0.8f, 0.5f, 0, 1, 0); m.Blob(0.6f, -half * 0.55f, 0, 0.5f, 0.8f, 0.5f, 0, 2, 0); m.Em = 0; }
                // string
                float tipX = 0.4f - 4.2f * (half / 13.5f) + 0.9f;
                float nock = tipX - 0.4f - drawAmt * 7.5f;
                m.Layer = 0; m.Col = 0xE8E0C4;
                Strip(m, new V3(tipX, half, 0), new V3(nock, 0, 0), 0.3f, new V3(0, 0, 1));
                Strip(m, new V3(nock, 0, 0), new V3(tipX, -half, 0), 0.3f, new V3(0, 0, 1));
                if (drawAmt > 0.3f)
                {
                    // nocked arrow
                    m.Layer = Mat3.Bark; m.Col = 0x8A6A40; AlongX(m, nock, nock + 13f, 0.28f, 0.28f, 4);
                    m.Layer = Mat3.Metal; m.Col = 0xC8D0DA;
                    m.T = Xf.Move(nock + 13f, 0, 0) * Xf.RotZ(-M.PI / 2); m.Cyl(0, 0, 0, 2.2f, 0.65f, 0, 4, false, true, 0); m.T = Xf.Id;
                }
            });
        }

        public static MeshB Shield()
        {
            return Cached(5001, delegate(MeshB m)
            {
                // a heater shield facing +X
                float[] yz = { 5.6f, -4.3f, 5.6f, 4.3f, 1.0f, 4.4f, -3.8f, 2.6f, -7.2f, 0f, -3.8f, -2.6f, 1.0f, -4.4f };
                m.Layer = Mat3.Plank; m.Col = 0xB02840;
                int n = yz.Length / 2;
                for (int i = 1; i + 1 < n; i++)
                {
                    m.Tri(new V3(0.5f, yz[0], yz[1]), new V3(0.5f, yz[i * 2], yz[i * 2 + 1]), new V3(0.5f, yz[i * 2 + 2], yz[i * 2 + 3]));
                    m.Tri(new V3(-0.5f, yz[0], yz[1]), new V3(-0.5f, yz[i * 2 + 2], yz[i * 2 + 3]), new V3(-0.5f, yz[i * 2], yz[i * 2 + 1]));
                }
                m.Layer = Mat3.Metal; m.Col = 0xB8C2D0;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    V3 a = new V3(0, yz[i * 2], yz[i * 2 + 1]), b = new V3(0, yz[j * 2], yz[j * 2 + 1]);
                    V3 dir = b - a;
                    m.T = Xf.Move(a) * Seg(-dir, new V3(1, 0, 0));
                    m.Cyl(0, 0, 0, dir.Len, 0.7f, 0.7f, 4, false, false, M.PI / 4);
                }
                m.T = Xf.Id;
                m.Layer = 0; m.Col = 0xE8BC50; m.Em = 40;
                m.Box(0.5f, -2.5f, -0.5f, 0.85f, 3.8f, 0.5f);
                m.Box(0.5f, 1.4f, -2.2f, 0.85f, 2.3f, 2.2f);
                m.Em = 0;
            });
        }

        public static MeshB FoeWeapon(EnemyKind k)
        {
            switch (k)
            {
                case EnemyKind.Hollow: return Cached(6001, delegate(MeshB m)
                {
                    m.Layer = Mat3.Bark; m.Col = 0x4A3226; AlongX(m, -3.2f, 0.4f, 0.55f, 0.55f, 5);
                    m.Layer = Mat3.Metal; m.Col = 0x8A7868;
                    Extrude(m, new float[] { 0.3f, -0.6f, 9.6f, -0.2f, 10.2f, 4.0f, 0.3f, 3.6f }, -0.35f, 0.35f);
                    m.Col = 0x6A3A2E; m.Box(5.5f, 2.6f, -0.38f, 9.2f, 3.6f, 0.38f);
                });
                case EnemyKind.Knight: return Cached(6002, delegate(MeshB m)
                {
                    m.Layer = Mat3.Cloth; m.Col = 0x2A2438; AlongX(m, -5.6f, 0, 0.62f, 0.62f, 6);
                    m.Layer = Mat3.Metal; m.Col = 0x4E5872; m.Blob(-6.2f, 0, 0, 1.0f, 1.0f, 1.0f, 0, 3, 0);
                    m.BoxC(0, 0, 0, 1.2f, 7.4f, 1.4f);
                    m.Col = 0x6A7488; Blade(m, 0.6f, 22.5f, 26.5f, 1.65f, 1.3f, 0.45f);
                    m.Layer = 0; m.Col = 0x9BDCF4; m.Em = 255;
                    for (int i = 0; i < 4; i++) m.BoxC(4.5f + i * 4.2f, 0, 0.4f, 1.6f, 0.45f, 0.2f);
                    m.Em = 0;
                });
                case EnemyKind.Prior: return Cached(6003, delegate(MeshB m)
                {
                    m.Layer = Mat3.Bark; m.Col = 0x3A2C28; AlongX(m, -17f, 22f, 0.75f, 0.65f, 6);
                    m.Layer = Mat3.Metal; m.Col = 0xB08A44;
                    AlongX(m, 21.4f, 22.6f, 1.0f, 1.0f, 6);
                    V3 prev = new V3(22.4f, 0, 0);
                    for (int i = 1; i <= 8; i++)
                    {
                        float a = i / 8f * M.PI * 1.25f;
                        V3 p = new V3(22.4f + M.Sin(a) * 3.6f, (1 - M.Cos(a)) * 3.6f, 0);
                        V3 dir = p - prev;
                        m.T = Xf.Move(prev) * Seg(-dir, new V3(0, 0, 1));
                        m.Cyl(0, 0, 0, dir.Len + 0.2f, 0.72f - i * 0.04f, 0.72f - i * 0.04f, 5, false, false, 0);
                        prev = p;
                    }
                    // a little bell hangs from the end of the crook
                    m.T = Xf.Move(prev.X - 0.6f, prev.Y, 0) * Xf.RotZ(M.PI / 2);
                    m.Col = 0xC8A050;
                    m.Lathe(0, 0, new float[] { 0, 0.5f, 1.2f, 1.15f, 2.6f, 1.6f }, 8, false, 0);
                    m.Layer = 0; m.Col = 0xC9D3F5; m.Em = 255;
                    m.Blob(0, 2.2f, 0, 0.6f, 0.6f, 0.6f, 0, 2, 0);
                    m.T = Xf.Id; m.Em = 0;
                });
                case EnemyKind.Acolyte: return Cached(6004, delegate(MeshB m)
                {
                    m.Layer = Mat3.Bark; m.Col = 0x3A2C28; AlongX(m, -11f, 14.5f, 0.55f, 0.5f, 6);
                    m.Layer = Mat3.Metal; m.Col = 0xA8823C;
                    AlongX(m, 14.2f, 15.4f, 1.3f, 1.6f, 7);
                    m.Layer = 0; m.Col = 0xEDE4CC;
                    AlongX(m, 15.4f, 18.6f, 0.75f, 0.75f, 6);
                    m.Col = 0xFFC070; m.Em = 255;
                    m.Blob(19.6f, 0, 0, 1.1f, 0.6f, 0.6f, 0, 3, 0);
                    m.Em = 0;
                });
                default: return Bow(9.8f, 0, 0.5f, true);
            }
        }

        // beasts
        internal sealed class Beast
        {
            public MeshB Body, Head, Tail, LegF, LegH, LegU, LegL;
            public int Eye;
        }
        static Beast wolf, alpha, crawler;

        public static Beast Wolf(bool isAlpha)
        {
            if (isAlpha ? alpha != null : wolf != null) return isAlpha ? alpha : wolf;
            Beast b = new Beast();
            int fur = isAlpha ? 0x3A3452 : 0x4A5268, furD = isAlpha ? 0x262238 : 0x363C52, furL = isAlpha ? 0x584688 : 0x66728B;
            int eye = isAlpha ? 0xFF8070 : 0xC9D3F5;
            MeshB m = new MeshB();
            m.Layer = Mat3.Fur; m.Col = fur;
            m.Blob(-1.0f, 10.2f, 0, 10.0f, 4.8f, 4.4f, 1, 101, 0.06f);
            m.Col = furL;
            m.Blob(5.4f, 10.4f, 0, 5.4f, 5.4f, 4.7f, 1, 103, 0.06f);
            m.Col = furD;
            m.Blob(-0.5f, 8.2f, 0, 7.2f, 2.4f, 3.6f, 0, 105, 0.1f);
            for (int k = 0; k < (isAlpha ? 7 : 5); k++)
            {
                float x = 7.0f - k * 3.2f;
                m.T = Xf.Move(x, 13.2f + (k == 0 ? 1.2f : 0), (k % 2 == 0 ? 0.6f : -0.6f)) * Xf.RotZ(0.95f);
                m.Cyl(0, 0, 0, isAlpha ? 4.6f : 3.4f, 1.6f, 0, 4, false, false, k);
            }
            m.T = Xf.Id;
            if (isAlpha)
            {
                m.Col = 0x2A2238;
                m.Blob(7.4f, 12.6f, 0, 4.2f, 4.8f, 5.6f, 1, 107, 0.2f);
            }
            b.Body = m;
            MeshB h = new MeshB();
            h.Layer = Mat3.Fur; h.Col = fur;
            h.Blob(1.6f, 1.4f, 0, 3.6f, 3.3f, 3.1f, 1, 111, 0.04f);
            h.Col = furL;
            Taper(h, 6.6f, 0, -0.5f, 1.9f, 2.4f, 1.45f, 2.0f, 1.15f);
            h.Col = furD;
            Taper(h, 6.2f, 0, -1.5f, -0.4f, 2.0f, 1.05f, 1.9f, 1.0f);
            h.Layer = 0; h.Col = 0x0E0C14;
            h.Box(8.4f, 0.8f, -0.75f, 9.3f, 2.0f, 0.75f);
            h.Col = 0xE8E0C4;
            h.Box(6.0f, -0.7f, 0.7f, 6.5f, -0.1f, 1.0f); h.Box(6.0f, -0.7f, -1.0f, 6.5f, -0.1f, -0.7f);
            h.Layer = Mat3.Fur; h.Col = furD;
            h.Cyl(0.6f, 1.7f, 3.6f, 7.0f, 1.2f, 0, 4, false, false, 0.4f);
            h.Cyl(0.6f, -1.7f, 3.6f, 7.0f, 1.2f, 0, 4, false, false, 0.4f);
            h.Layer = 0; h.Col = eye; h.Em = 255;
            h.BoxC(4.3f, 2.2f, 1.45f, 0.8f, 0.55f, 0.5f); h.BoxC(4.3f, 2.2f, -1.45f, 0.8f, 0.55f, 0.5f);
            h.Em = 0;
            b.Head = h;
            MeshB t = new MeshB();
            t.Layer = Mat3.Fur; t.Col = fur;
            t.Cyl(0, 0, -9.5f, 0, 0.5f, 1.8f, 6, false, false, 0);
            t.Col = furL; t.Cyl(0, 0, -10.6f, -8.8f, 0, 0.7f, 5, false, false, 0);
            b.Tail = t;
            MeshB lf = new MeshB();
            lf.Layer = Mat3.Fur; lf.Col = fur;
            lf.Cyl(0, 0, -5.5f, 0.8f, 1.15f, 1.75f, 6, false, false, 0);
            lf.Col = furD;
            lf.Cyl(0, 0, -9.0f, -5.3f, 0.85f, 1.05f, 5, false, false, 0);
            lf.Layer = 0; lf.Col = 0x1E1A28;
            lf.Box(-0.8f, -9.6f, -0.9f, 1.6f, -8.4f, 0.9f);
            b.LegF = lf; b.LegH = lf;
            b.Eye = eye;
            if (isAlpha) alpha = b; else wolf = b;
            return b;
        }

        public static Beast Crawler()
        {
            if (crawler != null) return crawler;
            Beast b = new Beast();
            MeshB m = new MeshB();
            m.Layer = Mat3.Fur; m.Col = 0x4A3A74;
            m.Blob(-5.6f, 6.4f, 0, 6.6f, 4.9f, 5.4f, 1, 121, 0.05f);
            m.Col = 0x5E4C92;
            m.Blob(-5.0f, 8.0f, 0, 4.6f, 2.9f, 3.6f, 0, 123, 0.05f);
            m.Layer = 0; m.Col = 0xA8E04A; m.Em = 255;
            m.Blob(-4.2f, 10.6f, 1.6f, 1.1f, 0.6f, 1.1f, 0, 1, 0);
            m.Blob(-7.0f, 10.0f, -1.4f, 0.9f, 0.5f, 0.9f, 0, 2, 0);
            m.Blob(-2.0f, 9.6f, -1.8f, 0.7f, 0.4f, 0.7f, 0, 3, 0);
            m.Blob(-9.4f, 8.6f, 1.2f, 0.8f, 0.5f, 0.8f, 0, 4, 0);
            m.Em = 0;
            m.Layer = Mat3.Fur; m.Col = 0x584688;
            m.Blob(1.6f, 5.4f, 0, 3.8f, 2.8f, 3.5f, 1, 125, 0.05f);
            m.Col = 0x2E2843;
            m.Blob(5.4f, 5.0f, 0, 2.5f, 2.2f, 2.6f, 1, 127, 0.04f);
            m.Layer = 0; m.Col = 0xC8F070; m.Em = 255;
            for (int i = 0; i < 3; i++)
            {
                m.BoxC(7.4f, 5.8f + (i == 1 ? 0.6f : 0), 0.7f + i * 0.45f, 0.5f, 0.5f, 0.45f);
                m.BoxC(7.4f, 5.8f + (i == 1 ? 0.6f : 0), -0.7f - i * 0.45f, 0.5f, 0.5f, 0.45f);
            }
            m.Em = 0;
            m.Layer = Mat3.Bone; m.Col = 0xE8E0C4;
            for (int side = -1; side <= 1; side += 2)
            {
                m.T = Xf.Move(7.2f, 3.6f, side * 0.9f) * Xf.RotZ(-2.4f);
                m.Cyl(0, 0, 0, 3.2f, 0.55f, 0, 4, false, false, 0);
            }
            m.T = Xf.Id;
            b.Body = m;
            MeshB u = new MeshB();
            u.Layer = Mat3.Fur; u.Col = 0x3A2E5E;
            u.Cyl(0, 0, -6.4f, 0.2f, 0.5f, 0.75f, 5, false, false, 0);
            u.Blob(0, -6.4f, 0, 0.6f, 0.6f, 0.6f, 0, 1, 0);
            b.LegU = u;
            MeshB l = new MeshB();
            l.Layer = Mat3.Fur; l.Col = 0x2E2843;
            l.Cyl(0, 0, -7.6f, 0.2f, 0.12f, 0.55f, 5, false, false, 0);
            b.LegL = l;
            b.Eye = 0xC8F070;
            crawler = b;
            return b;
        }

        // loot and missiles
        public static MeshB Coin()
        {
            return Cached(7001, delegate(MeshB m)
            {
                m.Layer = Mat3.Metal; m.Col = 0xF0C050; m.Em = 70;
                m.Cyl(0, 0, -0.35f, 0.35f, 2.3f, 2.3f, 10, true, true, 0);
                m.Col = 0xC89A38;
                m.Cyl(0, 0, -0.45f, 0.45f, 1.2f, 1.2f, 6, true, true, 0);
                m.Em = 0;
            });
        }

        public static MeshB Potion(bool health)
        {
            return Cached(health ? 7002 : 7003, delegate(MeshB m)
            {
                m.Layer = 0; m.Col = health ? 0xE03848 : 0x4A78F0; m.Em = 150;
                m.Lathe(0, 0, new float[] { 0, 1.6f, 1.2f, 2.5f, 3.2f, 2.4f, 4.4f, 1.4f, 5.2f, 0.8f }, 9, false, 0);
                m.Em = 0; m.Col = 0xD8E0E8;
                m.Cyl(0, 0, 5.2f, 6.2f, 0.85f, 0.85f, 7, false, false, 0);
                m.Col = 0x8A6040;
                m.Cyl(0, 0, 6.2f, 7.2f, 0.95f, 0.8f, 7, true, false, 0);
            });
        }

        public static MeshB Missile(int kind)
        {
            switch (kind)
            {
                case Projectile.KArrow: case Projectile.KPierce: case Projectile.KMarked:
                    return Cached(7100 + kind, delegate(MeshB m)
                    {
                        m.Layer = Mat3.Bark; m.Col = 0x9A7448; AlongX(m, -8f, 5f, 0.32f, 0.32f, 4);
                        m.Layer = Mat3.Metal; m.Col = kind == Projectile.KMarked ? 0xFF6060 : 0xD8E0E8; m.Em = (byte)(kind == Projectile.KArrow ? 0 : 200);
                        m.T = Xf.Move(5f, 0, 0) * Xf.RotZ(-M.PI / 2); m.Cyl(0, 0, 0, 2.6f, 0.85f, 0, 4, false, true, 0); m.T = Xf.Id;
                        m.Em = 0; m.Layer = 0; m.Col = kind == Projectile.KMarked ? 0xFF6060 : 0xE8E0C4;
                        m.Quad(new V3(-8f, 0, -1.1f), new V3(-5f, 0, -0.2f), new V3(-5f, 0, 0.2f), new V3(-8f, 0, 1.1f));
                        m.Quad(new V3(-8f, -1.1f, 0), new V3(-5f, -0.2f, 0), new V3(-5f, 0.2f, 0), new V3(-8f, 1.1f, 0));
                    });
                case Projectile.KBone:
                    return Cached(7110, delegate(MeshB m)
                    {
                        m.Layer = Mat3.Bone; m.Col = 0xE0D8C0; AlongX(m, -8f, 5f, 0.35f, 0.35f, 4);
                        m.Layer = 0; m.Col = 0x9BDCF4; m.Em = 255;
                        m.T = Xf.Move(5f, 0, 0) * Xf.RotZ(-M.PI / 2); m.Cyl(0, 0, 0, 2.6f, 0.9f, 0, 4, false, true, 0); m.T = Xf.Id;
                        m.Em = 0;
                    });
                case Projectile.KKnife: case Projectile.KPoisonKnife:
                    return Cached(7120 + kind, delegate(MeshB m)
                    {
                        m.Layer = Mat3.Cloth; m.Col = 0x3A2A22; AlongX(m, -2.4f, 0, 0.4f, 0.4f, 4);
                        m.Layer = Mat3.Metal; m.Col = kind == Projectile.KPoisonKnife ? 0x9AE070 : 0xD8E0E8; m.Em = (byte)(kind == Projectile.KPoisonKnife ? 160 : 40);
                        Blade(m, 0.2f, 4.6f, 6.4f, 0.7f, 0.55f, 0.22f);
                        m.Em = 0;
                    });
                default:
                    return null;
            }
        }

        /// <summary>A unit bead that glows (magic missiles, wisp cores); tinted when placed.</summary>
        public static MeshB Bead()
        {
            return Cached(7200, delegate(MeshB m)
            {
                m.Layer = 0; m.Col = 0xFFFFFF; m.Em = 255;
                m.Blob(0, 0, 0, 1, 1, 1, 1, 5, 0.04f);
                m.Em = 0;
            });
        }

        public static MeshB WispCore()
        {
            return Cached(7201, delegate(MeshB m)
            {
                m.Layer = 0; m.Col = 0xD8DCFF; m.Em = 255;
                m.Blob(0, 0, 0, 3.3f, 3.6f, 3.3f, 1, 9, 0.08f);
                m.Em = 0; m.Col = 0x1A1430;
                m.BoxC(3.0f, 0.6f, 1.0f, 0.6f, 0.9f, 0.7f); m.BoxC(3.0f, 0.6f, -1.0f, 0.6f, 0.9f, 0.7f);
            });
        }

        /// <summary>Glowing core colour and size for magic missiles (drawn as an emissive bead plus glow).</summary>
        public static bool Orb(int kind, out int core, out int glow, out float r)
        {
            core = glow = 0; r = 0;
            switch (kind)
            {
                case Projectile.KEmber: core = 0xFFE8A0; glow = 0xFF9030; r = 1.8f; return true;
                case Projectile.KFireball: core = 0xFFF0B0; glow = 0xFF8020; r = 3.6f; return true;
                case Projectile.KGloam: core = 0xEAE0FF; glow = 0x9070F0; r = 2.2f; return true;
                case Projectile.KGlob: core = 0xDCFA9A; glow = 0x70D030; r = 2.6f; return true;
                case Projectile.KWispOrb: core = 0xF0F4FF; glow = 0xA0A8FF; r = 2.4f; return true;
            }
            return false;
        }
    }
}
