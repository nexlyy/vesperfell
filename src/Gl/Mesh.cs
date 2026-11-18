using System;
using System.Runtime.InteropServices;

namespace Vesperfell
{
    /// <summary>
    /// One vertex: position, normal, colour (alpha = how much it glows), texture coordinates and four extra bytes:
    /// material layer, wind sway weight, "may be cut away to show the hero" flag, and a spare.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vtx
    {
        public float X, Y, Z;
        public sbyte NX, NY, NZ, NW;
        public byte R, G, B, E;
        public float U, V;
        public byte L, Wd, Oc, F;
        public const int Size = 32;
    }

    /// <summary>Builds flat-shaded triangle lists on the CPU: boxes, tapered cylinders, lathed shapes, faceted blobs.</summary>
    public sealed class MeshB
    {
        public Vtx[] V = new Vtx[512];
        public int N;
        public Xf T = Xf.Id;
        public int Col = 0xFFFFFF;
        public byte Em, Layer, Wind, Occ;
        public float Tex = 1f / 32f;            // texture repeats per world unit
        /// <summary>Cylinders and blobs get rounded (per-corner) normals instead of flat facets.</summary>
        public bool Smooth;
        public float MinX = 1e9f, MinY = 1e9f, MinZ = 1e9f, MaxX = -1e9f, MaxY = -1e9f, MaxZ = -1e9f;
        public bool TrackBounds;

        public void Clear() { N = 0; T = Xf.Id; Col = 0xFFFFFF; Em = 0; Layer = 0; Wind = 0; Occ = 0; MinX = MinY = MinZ = 1e9f; MaxX = MaxY = MaxZ = -1e9f; }

        void Grow(int more)
        {
            if (N + more <= V.Length) return;
            int n = V.Length * 2;
            while (n < N + more) n *= 2;
            Vtx[] a = new Vtx[n];
            Array.Copy(V, a, N);
            V = a;
        }

        static sbyte Sn(float v) { int i = (int)Math.Round(v * 127f); return (sbyte)(i < -127 ? -127 : (i > 127 ? 127 : i)); }

        void Put(V3 p, V3 n, float u, float v, int col, byte em, byte wind)
        {
            Vtx x;
            x.X = p.X; x.Y = p.Y; x.Z = p.Z;
            x.NX = Sn(n.X); x.NY = Sn(n.Y); x.NZ = Sn(n.Z); x.NW = 0;
            x.R = (byte)((col >> 16) & 255); x.G = (byte)((col >> 8) & 255); x.B = (byte)(col & 255); x.E = em;
            x.U = u; x.V = v; x.L = Layer; x.Wd = wind; x.Oc = Occ; x.F = 0;
            V[N++] = x;
            if (TrackBounds)
            {
                if (p.X < MinX) MinX = p.X; if (p.Y < MinY) MinY = p.Y; if (p.Z < MinZ) MinZ = p.Z;
                if (p.X > MaxX) MaxX = p.X; if (p.Y > MaxY) MaxY = p.Y; if (p.Z > MaxZ) MaxZ = p.Z;
            }
        }

        /// <summary>A triangle in local coordinates (it goes through T). Texture coordinates come from a planar projection.</summary>
        public void Tri(V3 a, V3 b, V3 c)
        {
            V3 n = V3.Cross(b - a, c - a);
            if (n.X * n.X + n.Y * n.Y + n.Z * n.Z < 1e-12f) return;
            n = n.Norm;
            Grow(3);
            float ax = Math.Abs(n.X), ay = Math.Abs(n.Y), az = Math.Abs(n.Z);
            int axis = ay >= ax && ay >= az ? 1 : (ax >= az ? 0 : 2);
            V3 wn = T.D(n).Norm;
            byte w = Wind;
            UvPut(a, wn, axis, w); UvPut(b, wn, axis, w); UvPut(c, wn, axis, w);
        }

        /// <summary>A triangle with its own normal at each corner (local space), for rounded shading.</summary>
        public void TriN(V3 a, V3 b, V3 c, V3 na, V3 nb, V3 nc)
        {
            V3 n = V3.Cross(b - a, c - a);
            if (n.X * n.X + n.Y * n.Y + n.Z * n.Z < 1e-12f) return;
            n = n.Norm;
            Grow(3);
            float ax = Math.Abs(n.X), ay = Math.Abs(n.Y), az = Math.Abs(n.Z);
            int axis = ay >= ax && ay >= az ? 1 : (ax >= az ? 0 : 2);
            byte w = Wind;
            UvPut(a, T.D(na).Norm, axis, w); UvPut(b, T.D(nb).Norm, axis, w); UvPut(c, T.D(nc).Norm, axis, w);
        }

        void UvPut(V3 p, V3 wn, int axis, byte wind)
        {
            float u, v;
            if (axis == 1) { u = p.X * Tex; v = p.Z * Tex; }
            else if (axis == 0) { u = p.Z * Tex; v = -p.Y * Tex; }
            else { u = p.X * Tex; v = -p.Y * Tex; }
            Put(T.P(p), wn, u, v, Col, Em, wind);
        }

        /// <summary>A triangle with explicit texture coordinates (in texture repeats).</summary>
        public void TriUv(V3 a, V3 b, V3 c, float ua, float va, float ub, float vb, float uc, float vc)
        {
            V3 n = V3.Cross(b - a, c - a);
            if (n.X * n.X + n.Y * n.Y + n.Z * n.Z < 1e-12f) return;
            V3 wn = T.D(n.Norm).Norm;
            Grow(3);
            Put(T.P(a), wn, ua, va, Col, Em, Wind); Put(T.P(b), wn, ub, vb, Col, Em, Wind); Put(T.P(c), wn, uc, vc, Col, Em, Wind);
        }

        /// <summary>A quad a-b-c-d, counter-clockwise seen from the side it faces.</summary>
        public void Quad(V3 a, V3 b, V3 c, V3 d) { Tri(a, b, c); Tri(a, c, d); }

        public void QuadUv(V3 a, V3 b, V3 c, V3 d, float u0, float v0, float u1, float v1)
        {
            TriUv(a, b, c, u0, v1, u1, v1, u1, v0);
            TriUv(a, c, d, u0, v1, u1, v0, u0, v0);
        }

        public void Box(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            V3 a = new V3(x0, y0, z0), b = new V3(x1, y0, z0), c = new V3(x1, y0, z1), d = new V3(x0, y0, z1);
            V3 e = new V3(x0, y1, z0), f = new V3(x1, y1, z0), g = new V3(x1, y1, z1), h = new V3(x0, y1, z1);
            Quad(e, h, g, f);       // top
            Quad(a, b, c, d);       // bottom
            Quad(d, c, g, h);       // south (+z)
            Quad(b, a, e, f);       // north
            Quad(c, b, f, g);       // east
            Quad(a, d, h, e);       // west
        }

        /// <summary>Axis-aligned box from its centre and full size.</summary>
        public void BoxC(float cx, float cy, float cz, float sx, float sy, float sz)
        {
            Box(cx - sx * 0.5f, cy - sy * 0.5f, cz - sz * 0.5f, cx + sx * 0.5f, cy + sy * 0.5f, cz + sz * 0.5f);
        }

        /// <summary>Box without its bottom face (things that stand on the ground).</summary>
        public void BoxOpen(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            V3 a = new V3(x0, y0, z0), b = new V3(x1, y0, z0), c = new V3(x1, y0, z1), d = new V3(x0, y0, z1);
            V3 e = new V3(x0, y1, z0), f = new V3(x1, y1, z0), g = new V3(x1, y1, z1), h = new V3(x0, y1, z1);
            Quad(e, h, g, f); Quad(d, c, g, h); Quad(b, a, e, f); Quad(c, b, f, g); Quad(a, d, h, e);
        }

        /// <summary>A tapered cylinder (or cone when r1 = 0) standing on y0, with its axis along Y.</summary>
        public void Cyl(float cx, float cz, float y0, float y1, float r0, float r1, int sides, bool capTop, bool capBottom, float rot)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = rot + i * M.TAU / sides, a1 = rot + (i + 1) * M.TAU / sides;
                float c0 = M.Cos(a0), s0 = M.Sin(a0), c1 = M.Cos(a1), s1 = M.Sin(a1);
                V3 p0 = new V3(cx + c0 * r0, y0, cz + s0 * r0), p1 = new V3(cx + c1 * r0, y0, cz + s1 * r0);
                V3 q0 = new V3(cx + c0 * r1, y1, cz + s0 * r1), q1 = new V3(cx + c1 * r1, y1, cz + s1 * r1);
                if (Smooth && Math.Abs(y1 - y0) > 1e-4f)
                {
                    float k = (r0 - r1) / (y1 - y0);
                    V3 n0 = new V3(c0, k, s0), n1 = new V3(c1, k, s1);
                    if (r1 > 0.001f) { TriN(p1, p0, q0, n1, n0, n0); TriN(p1, q0, q1, n1, n0, n1); }
                    else TriN(p1, p0, q0, n1, n0, (n0 + n1) * 0.5f);
                }
                else if (r1 > 0.001f) Quad(p1, p0, q0, q1); else Tri(p1, p0, q0);
                if (capTop && r1 > 0.001f) Tri(new V3(cx, y1, cz), q1, q0);
                if (capBottom) Tri(new V3(cx, y0, cz), p0, p1);
            }
        }

        /// <summary>A shape turned on a lathe: prof holds (y, radius) pairs from bottom to top.</summary>
        public void Lathe(float cx, float cz, float[] prof, int sides, bool capTop, float rot)
        {
            for (int k = 0; k + 3 < prof.Length; k += 2)
                Cyl(cx, cz, prof[k], prof[k + 2], prof[k + 1], prof[k + 3], sides, false, false, rot);
            if (capTop && prof[prof.Length - 1] > 0.001f)
                Cyl(cx, cz, prof[prof.Length - 2], prof[prof.Length - 2], prof[prof.Length - 1], 0, sides, false, false, rot);
        }

        static readonly float[] ico = BuildIco();
        static float[] BuildIco()
        {
            float t = (1f + (float)Math.Sqrt(5)) / 2f;
            float[] v = { -1, t, 0, 1, t, 0, -1, -t, 0, 1, -t, 0, 0, -1, t, 0, 1, t, 0, -1, -t, 0, 1, -t, t, 0, -1, t, 0, 1, -t, 0, -1, -t, 0, 1 };
            int[] f = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                        3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            float[] o = new float[f.Length * 3];
            for (int i = 0; i < f.Length; i++)
            {
                float x = v[f[i] * 3], y = v[f[i] * 3 + 1], z = v[f[i] * 3 + 2];
                float l = (float)Math.Sqrt(x * x + y * y + z * z);
                o[i * 3] = x / l; o[i * 3 + 1] = y / l; o[i * 3 + 2] = z / l;
            }
            return o;
        }

        static V3 Jit(V3 d, uint seed, float jitter)
        {
            if (jitter <= 0) return d;
            uint h = Noise.Hash((int)Math.Round(d.X * 997), (int)Math.Round(d.Y * 991 + d.Z * 613), (int)seed);
            float k = 1f + ((h & 1023) / 1023f - 0.5f) * 2f * jitter;
            return d * k;
        }

        /// <summary>A faceted ellipsoid (icosahedron, optionally split once). jitter roughens it into a rock or a clump of leaves.</summary>
        public void Blob(float cx, float cy, float cz, float rx, float ry, float rz, int detail, uint seed, float jitter)
        {
            for (int i = 0; i < ico.Length; i += 9)
            {
                V3 a = new V3(ico[i], ico[i + 1], ico[i + 2]), b = new V3(ico[i + 3], ico[i + 4], ico[i + 5]), c = new V3(ico[i + 6], ico[i + 7], ico[i + 8]);
                if (detail <= 0) BlobTri(a, b, c, cx, cy, cz, rx, ry, rz, seed, jitter);
                else
                {
                    V3 ab = (a + b).Norm, bc = (b + c).Norm, ca = (c + a).Norm;
                    BlobTri(a, ab, ca, cx, cy, cz, rx, ry, rz, seed, jitter);
                    BlobTri(ab, b, bc, cx, cy, cz, rx, ry, rz, seed, jitter);
                    BlobTri(ca, bc, c, cx, cy, cz, rx, ry, rz, seed, jitter);
                    BlobTri(ab, bc, ca, cx, cy, cz, rx, ry, rz, seed, jitter);
                }
            }
        }

        void BlobTri(V3 a, V3 b, V3 c, float cx, float cy, float cz, float rx, float ry, float rz, uint seed, float jitter)
        {
            a = Jit(a, seed, jitter); b = Jit(b, seed, jitter); c = Jit(c, seed, jitter);
            V3 pa = new V3(cx + a.X * rx, cy + a.Y * ry, cz + a.Z * rz), pb = new V3(cx + b.X * rx, cy + b.Y * ry, cz + b.Z * rz), pc = new V3(cx + c.X * rx, cy + c.Y * ry, cz + c.Z * rz);
            if (Smooth) TriN(pa, pb, pc, new V3(a.X / rx, a.Y / ry, a.Z / rz), new V3(b.X / rx, b.Y / ry, b.Z / rz), new V3(c.X / rx, c.Y / ry, c.Z / rz));
            else Tri(pa, pb, pc);
        }

        /// <summary>A flat card facing +Z (for leaves, flames and cobwebs), centred on x with its base at y.</summary>
        public void Card(float cx, float y0, float cz, float w, float h)
        {
            Quad(new V3(cx - w / 2, y0, cz), new V3(cx + w / 2, y0, cz), new V3(cx + w / 2, y0 + h, cz), new V3(cx - w / 2, y0 + h, cz));
        }

        /// <summary>One raw vertex for effects: no transform, a fixed normal, explicit colour, alpha (E) and shape (L).</summary>
        public void Raw(V3 p, float u, float v, int col, byte e, byte layer)
        {
            Grow(1);
            Vtx x;
            x.X = p.X; x.Y = p.Y; x.Z = p.Z; x.NX = 0; x.NY = 127; x.NZ = 0; x.NW = 0;
            x.R = (byte)((col >> 16) & 255); x.G = (byte)((col >> 8) & 255); x.B = (byte)(col & 255); x.E = e;
            x.U = u; x.V = v; x.L = layer; x.Wd = 0; x.Oc = 0; x.F = 0;
            V[N++] = x;
        }

        /// <summary>Copies another mesh in, through a transform (mirrored placements get their winding reversed).</summary>
        public void Append(MeshB src, Xf xf)
        {
            if (src == null || src.N == 0) return;
            Grow(src.N);
            bool mir = xf.Mirrored;
            for (int i = 0; i < src.N; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    Vtx s = src.V[i + (mir && k > 0 ? 3 - k : k)];
                    V3 p = xf.P(s.X, s.Y, s.Z);
                    V3 n = xf.D(new V3(s.NX / 127f, s.NY / 127f, s.NZ / 127f)).Norm;
                    s.X = p.X; s.Y = p.Y; s.Z = p.Z; s.NX = Sn(n.X); s.NY = Sn(n.Y); s.NZ = Sn(n.Z);
                    if (Occ != 0) s.Oc = Occ;
                    V[N++] = s;
                    if (TrackBounds)
                    {
                        if (p.X < MinX) MinX = p.X; if (p.Y < MinY) MinY = p.Y; if (p.Z < MinZ) MinZ = p.Z;
                        if (p.X > MaxX) MaxX = p.X; if (p.Y > MaxY) MaxY = p.Y; if (p.Z > MaxZ) MaxZ = p.Z;
                    }
                }
            }
        }

        /// <summary>Like Append, but tints every vertex colour toward a colour and can override the glow (posed characters).</summary>
        public void AppendTint(MeshB src, Xf xf, int tint, int amt256, int emOverride)
        {
            int start = N;
            Append(src, xf);
            if (amt256 <= 0 && emOverride < 0) return;
            int tr = (tint >> 16) & 255, tg = (tint >> 8) & 255, tb = tint & 255;
            for (int i = start; i < N; i++)
            {
                if (amt256 > 0)
                {
                    V[i].R = (byte)(V[i].R + ((tr - V[i].R) * amt256 >> 8));
                    V[i].G = (byte)(V[i].G + ((tg - V[i].G) * amt256 >> 8));
                    V[i].B = (byte)(V[i].B + ((tb - V[i].B) * amt256 >> 8));
                }
                if (emOverride >= 0) V[i].E = (byte)Math.Max(V[i].E, emOverride);
            }
        }
    }

    /// <summary>A vertex buffer on the GPU with its attribute layout.</summary>
    internal sealed unsafe class GpuMesh
    {
        public uint Vao, Vbo; public int Count; int cap;
        public float MinX, MinY, MinZ, MaxX, MaxY, MaxZ;

        public GpuMesh()
        {
            uint a, b;
            GL.GenVertexArrays(1, &a); GL.GenBuffers(1, &b);
            Vao = a; Vbo = b;
            GL.BindVertexArray(Vao);
            GL.BindBuffer(GL.ARRAY_BUFFER, Vbo);
            GL.EnableVertexAttribArray(0); GL.VertexAttribPointer(0, 3, GL.FLOAT, 0, Vtx.Size, (IntPtr)0);
            GL.EnableVertexAttribArray(1); GL.VertexAttribPointer(1, 4, GL.BYTE, 1, Vtx.Size, (IntPtr)12);
            GL.EnableVertexAttribArray(2); GL.VertexAttribPointer(2, 4, GL.UNSIGNED_BYTE, 1, Vtx.Size, (IntPtr)16);
            GL.EnableVertexAttribArray(3); GL.VertexAttribPointer(3, 2, GL.FLOAT, 0, Vtx.Size, (IntPtr)20);
            GL.EnableVertexAttribArray(4); GL.VertexAttribPointer(4, 4, GL.UNSIGNED_BYTE, 0, Vtx.Size, (IntPtr)28);
            GL.BindVertexArray(0);
        }

        public void Upload(MeshB m, bool dynamic) { Upload(m.V, m.N, dynamic); MinX = m.MinX; MinY = m.MinY; MinZ = m.MinZ; MaxX = m.MaxX; MaxY = m.MaxY; MaxZ = m.MaxZ; }

        public void Upload(Vtx[] v, int n, bool dynamic)
        {
            GL.BindBuffer(GL.ARRAY_BUFFER, Vbo);
            if (n > cap || !dynamic)
            {
                int c = dynamic ? Math.Max(n, cap * 2) : n;
                if (c < 3) c = 3;
                GL.BufferData(GL.ARRAY_BUFFER, (IntPtr)(c * Vtx.Size), IntPtr.Zero, dynamic ? GL.DYNAMIC_DRAW : GL.STATIC_DRAW);
                cap = c;
            }
            else GL.BufferData(GL.ARRAY_BUFFER, (IntPtr)(cap * Vtx.Size), IntPtr.Zero, GL.DYNAMIC_DRAW);    // orphan
            if (n > 0) fixed (Vtx* p = v) GL.BufferSubData(GL.ARRAY_BUFFER, IntPtr.Zero, (IntPtr)(n * Vtx.Size), (IntPtr)p);
            Count = n;
        }

        public void Draw()
        {
            if (Count <= 0) return;
            GL.BindVertexArray(Vao);
            GL.DrawArrays(GL.TRIANGLES, 0, Count);
        }

        public void Draw(int first, int count)
        {
            if (count <= 0) return;
            GL.BindVertexArray(Vao);
            GL.DrawArrays(GL.TRIANGLES, first, count);
        }

        public void Dispose()
        {
            uint a = Vao, b = Vbo;
            if (a != 0) GL.DeleteVertexArrays(1, &a);
            if (b != 0) GL.DeleteBuffers(1, &b);
            Vao = Vbo = 0;
        }
    }
}
