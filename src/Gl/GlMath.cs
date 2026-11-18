using System;

namespace Vesperfell
{
    /// <summary>A point or direction in the 3D world. X = east, Y = up, Z = south (the 2D map's Y axis).</summary>
    public struct V3
    {
        public float X, Y, Z;
        public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static V3 operator +(V3 a, V3 b) { return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static V3 operator -(V3 a, V3 b) { return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static V3 operator -(V3 a) { return new V3(-a.X, -a.Y, -a.Z); }
        public static V3 operator *(V3 a, float s) { return new V3(a.X * s, a.Y * s, a.Z * s); }
        public static float Dot(V3 a, V3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
        public static V3 Cross(V3 a, V3 b) { return new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X); }
        public float Len { get { return (float)Math.Sqrt(X * X + Y * Y + Z * Z); } }
        public V3 Norm
        {
            get
            {
                float l = (float)Math.Sqrt(X * X + Y * Y + Z * Z);
                return l > 1e-7f ? new V3(X / l, Y / l, Z / l) : new V3(0, 1, 0);
            }
        }
        public static V3 Lerp(V3 a, V3 b, float t) { return new V3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t); }
    }

    /// <summary>
    /// An affine transform (three axis images plus an origin) used to build and pose models. Kept rigid or uniformly
    /// scaled, so normals can go through the same 3x3 part and be renormalised.
    /// </summary>
    public struct Xf
    {
        public float Ax, Ay, Az, Bx, By, Bz, Cx, Cy, Cz, Tx, Ty, Tz;

        public static Xf Id { get { Xf x = new Xf(); x.Ax = 1; x.By = 1; x.Cz = 1; return x; } }

        public static Xf Move(float x, float y, float z) { Xf r = Id; r.Tx = x; r.Ty = y; r.Tz = z; return r; }
        public static Xf Move(V3 p) { return Move(p.X, p.Y, p.Z); }

        /// <summary>Turns +X toward the map direction a (radians, measured like the 2D game: 0 = east, +pi/2 = south).</summary>
        public static Xf Yaw(float a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            Xf r = new Xf(); r.Ax = c; r.Az = s; r.By = 1; r.Cx = -s; r.Cz = c; return r;
        }

        /// <summary>Rotation about the X axis: +Y turns toward +Z.</summary>
        public static Xf RotX(float a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            Xf r = new Xf(); r.Ax = 1; r.By = c; r.Bz = s; r.Cy = -s; r.Cz = c; return r;
        }

        /// <summary>Rotation about the Z axis: +X turns toward +Y.</summary>
        public static Xf RotZ(float a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            Xf r = new Xf(); r.Ax = c; r.Ay = s; r.Bx = -s; r.By = c; r.Cz = 1; return r;
        }

        /// <summary>Rotation about any unit axis.</summary>
        public static Xf Rot(V3 k, float a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a), t = 1 - c;
            Xf r = new Xf();
            r.Ax = t * k.X * k.X + c; r.Ay = t * k.X * k.Y + s * k.Z; r.Az = t * k.X * k.Z - s * k.Y;
            r.Bx = t * k.X * k.Y - s * k.Z; r.By = t * k.Y * k.Y + c; r.Bz = t * k.Y * k.Z + s * k.X;
            r.Cx = t * k.X * k.Z + s * k.Y; r.Cy = t * k.Y * k.Z - s * k.X; r.Cz = t * k.Z * k.Z + c;
            return r;
        }

        public static Xf Scale(float s) { Xf r = new Xf(); r.Ax = s; r.By = s; r.Cz = s; return r; }
        public static Xf Scale(float sx, float sy, float sz) { Xf r = new Xf(); r.Ax = sx; r.By = sy; r.Cz = sz; return r; }

        /// <summary>a * b: apply b first, then a.</summary>
        public static Xf operator *(Xf a, Xf b)
        {
            Xf r;
            r.Ax = a.Ax * b.Ax + a.Bx * b.Ay + a.Cx * b.Az; r.Ay = a.Ay * b.Ax + a.By * b.Ay + a.Cy * b.Az; r.Az = a.Az * b.Ax + a.Bz * b.Ay + a.Cz * b.Az;
            r.Bx = a.Ax * b.Bx + a.Bx * b.By + a.Cx * b.Bz; r.By = a.Ay * b.Bx + a.By * b.By + a.Cy * b.Bz; r.Bz = a.Az * b.Bx + a.Bz * b.By + a.Cz * b.Bz;
            r.Cx = a.Ax * b.Cx + a.Bx * b.Cy + a.Cx * b.Cz; r.Cy = a.Ay * b.Cx + a.By * b.Cy + a.Cy * b.Cz; r.Cz = a.Az * b.Cx + a.Bz * b.Cy + a.Cz * b.Cz;
            r.Tx = a.Ax * b.Tx + a.Bx * b.Ty + a.Cx * b.Tz + a.Tx; r.Ty = a.Ay * b.Tx + a.By * b.Ty + a.Cy * b.Tz + a.Ty; r.Tz = a.Az * b.Tx + a.Bz * b.Ty + a.Cz * b.Tz + a.Tz;
            return r;
        }

        public V3 P(float x, float y, float z) { return new V3(Ax * x + Bx * y + Cx * z + Tx, Ay * x + By * y + Cy * z + Ty, Az * x + Bz * y + Cz * z + Tz); }
        public V3 P(V3 p) { return P(p.X, p.Y, p.Z); }
        public V3 D(V3 d) { return new V3(Ax * d.X + Bx * d.Y + Cx * d.Z, Ay * d.X + By * d.Y + Cy * d.Z, Az * d.X + Bz * d.Y + Cz * d.Z); }
        public bool Mirrored
        {
            get { return Ax * (By * Cz - Bz * Cy) - Bx * (Ay * Cz - Az * Cy) + Cx * (Ay * Bz - Az * By) < 0; }
        }
    }

    /// <summary>4x4 matrices for the camera, stored column-major in float[16] the way OpenGL reads them.</summary>
    public static class M4
    {
        public static float[] New() { float[] m = new float[16]; Identity(m); return m; }

        public static void Identity(float[] m)
        {
            Array.Clear(m, 0, 16); m[0] = m[5] = m[10] = m[15] = 1;
        }

        /// <summary>r = a * b (r must be a different array).</summary>
        public static void Mul(float[] a, float[] b, float[] r)
        {
            for (int c = 0; c < 4; c++)
                for (int rr = 0; rr < 4; rr++)
                    r[c * 4 + rr] = a[rr] * b[c * 4] + a[4 + rr] * b[c * 4 + 1] + a[8 + rr] * b[c * 4 + 2] + a[12 + rr] * b[c * 4 + 3];
        }

        public static void Perspective(float[] m, float fovY, float aspect, float n, float f)
        {
            Array.Clear(m, 0, 16);
            float t = 1f / (float)Math.Tan(fovY * 0.5f);
            m[0] = t / aspect; m[5] = t; m[10] = (f + n) / (n - f); m[11] = -1; m[14] = 2 * f * n / (n - f);
        }

        /// <summary>A perspective frustum given its near-plane extents (lets the view widen with the window).</summary>
        public static void Frustum(float[] m, float l, float r, float b, float t, float n, float f)
        {
            Array.Clear(m, 0, 16);
            m[0] = 2 * n / (r - l); m[5] = 2 * n / (t - b); m[8] = (r + l) / (r - l); m[9] = (t + b) / (t - b);
            m[10] = (f + n) / (n - f); m[11] = -1; m[14] = 2 * f * n / (n - f);
        }

        public static void Ortho(float[] m, float l, float r, float b, float t, float n, float f)
        {
            Array.Clear(m, 0, 16);
            m[0] = 2 / (r - l); m[5] = 2 / (t - b); m[10] = -2 / (f - n);
            m[12] = -(r + l) / (r - l); m[13] = -(t + b) / (t - b); m[14] = -(f + n) / (f - n); m[15] = 1;
        }

        public static void LookAt(float[] m, V3 eye, V3 at, V3 up)
        {
            V3 f = (at - eye).Norm, s = V3.Cross(f, up).Norm, u = V3.Cross(s, f);
            m[0] = s.X; m[4] = s.Y; m[8] = s.Z;
            m[1] = u.X; m[5] = u.Y; m[9] = u.Z;
            m[2] = -f.X; m[6] = -f.Y; m[10] = -f.Z;
            m[3] = 0; m[7] = 0; m[11] = 0;
            m[12] = -V3.Dot(s, eye); m[13] = -V3.Dot(u, eye); m[14] = V3.Dot(f, eye); m[15] = 1;
        }

        public static void Transform(float[] m, float x, float y, float z, out float cx, out float cy, out float cz, out float cw)
        {
            cx = m[0] * x + m[4] * y + m[8] * z + m[12];
            cy = m[1] * x + m[5] * y + m[9] * z + m[13];
            cz = m[2] * x + m[6] * y + m[10] * z + m[14];
            cw = m[3] * x + m[7] * y + m[11] * z + m[15];
        }

        public static bool Invert(float[] m, float[] inv)
        {
            float[] t = new float[16];
            t[0] = m[5] * m[10] * m[15] - m[5] * m[11] * m[14] - m[9] * m[6] * m[15] + m[9] * m[7] * m[14] + m[13] * m[6] * m[11] - m[13] * m[7] * m[10];
            t[4] = -m[4] * m[10] * m[15] + m[4] * m[11] * m[14] + m[8] * m[6] * m[15] - m[8] * m[7] * m[14] - m[12] * m[6] * m[11] + m[12] * m[7] * m[10];
            t[8] = m[4] * m[9] * m[15] - m[4] * m[11] * m[13] - m[8] * m[5] * m[15] + m[8] * m[7] * m[13] + m[12] * m[5] * m[11] - m[12] * m[7] * m[9];
            t[12] = -m[4] * m[9] * m[14] + m[4] * m[10] * m[13] + m[8] * m[5] * m[14] - m[8] * m[6] * m[13] - m[12] * m[5] * m[10] + m[12] * m[6] * m[9];
            t[1] = -m[1] * m[10] * m[15] + m[1] * m[11] * m[14] + m[9] * m[2] * m[15] - m[9] * m[3] * m[14] - m[13] * m[2] * m[11] + m[13] * m[3] * m[10];
            t[5] = m[0] * m[10] * m[15] - m[0] * m[11] * m[14] - m[8] * m[2] * m[15] + m[8] * m[3] * m[14] + m[12] * m[2] * m[11] - m[12] * m[3] * m[10];
            t[9] = -m[0] * m[9] * m[15] + m[0] * m[11] * m[13] + m[8] * m[1] * m[15] - m[8] * m[3] * m[13] - m[12] * m[1] * m[11] + m[12] * m[3] * m[9];
            t[13] = m[0] * m[9] * m[14] - m[0] * m[10] * m[13] - m[8] * m[1] * m[14] + m[8] * m[2] * m[13] + m[12] * m[1] * m[10] - m[12] * m[2] * m[9];
            t[2] = m[1] * m[6] * m[15] - m[1] * m[7] * m[14] - m[5] * m[2] * m[15] + m[5] * m[3] * m[14] + m[13] * m[2] * m[7] - m[13] * m[3] * m[6];
            t[6] = -m[0] * m[6] * m[15] + m[0] * m[7] * m[14] + m[4] * m[2] * m[15] - m[4] * m[3] * m[14] - m[12] * m[2] * m[7] + m[12] * m[3] * m[6];
            t[10] = m[0] * m[5] * m[15] - m[0] * m[7] * m[13] - m[4] * m[1] * m[15] + m[4] * m[3] * m[13] + m[12] * m[1] * m[7] - m[12] * m[3] * m[5];
            t[14] = -m[0] * m[5] * m[14] + m[0] * m[6] * m[13] + m[4] * m[1] * m[14] - m[4] * m[2] * m[13] - m[12] * m[1] * m[6] + m[12] * m[2] * m[5];
            t[3] = -m[1] * m[6] * m[11] + m[1] * m[7] * m[10] + m[5] * m[2] * m[11] - m[5] * m[3] * m[10] - m[9] * m[2] * m[7] + m[9] * m[3] * m[6];
            t[7] = m[0] * m[6] * m[11] - m[0] * m[7] * m[10] - m[4] * m[2] * m[11] + m[4] * m[3] * m[10] + m[8] * m[2] * m[7] - m[8] * m[3] * m[6];
            t[11] = -m[0] * m[5] * m[11] + m[0] * m[7] * m[9] + m[4] * m[1] * m[11] - m[4] * m[3] * m[9] - m[8] * m[1] * m[7] + m[8] * m[3] * m[5];
            t[15] = m[0] * m[5] * m[10] - m[0] * m[6] * m[9] - m[4] * m[1] * m[10] + m[4] * m[2] * m[9] + m[8] * m[1] * m[6] - m[8] * m[2] * m[5];
            float det = m[0] * t[0] + m[1] * t[4] + m[2] * t[8] + m[3] * t[12];
            if (Math.Abs(det) < 1e-12f) return false;
            det = 1f / det;
            for (int i = 0; i < 16; i++) inv[i] = t[i] * det;
            return true;
        }
    }
}
