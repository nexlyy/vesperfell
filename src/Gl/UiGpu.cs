using System;
using System.Collections.Generic;

namespace Vesperfell
{
    /// <summary>
    /// Draws the interface the frame recorded (UiRec) straight onto the finished 3D image: one indexed batch per clip or
    /// atlas page, with the atlas pages and the small images kept in textures here. Nothing is rasterised on the processor
    /// and no full-screen layer has to be copied to the card, so the interface costs the same at any window size.
    /// </summary>
    internal sealed unsafe class UiGpu : IDisposable
    {
        public readonly UiRec Rec = new UiRec();
        readonly Prog prog;
        uint vao, vbo, ibo, imgTex;
        int vboBytes, iboQuads;
        readonly List<uint> pages = new List<uint>();

        public UiGpu()
        {
            prog = new Prog("uiquads", Shaders.UiQuadVs, Shaders.UiQuadFs);
            prog.Use(); prog.I("uAtlas", 0); prog.I("uImg", 1);
            uint a, b, c;
            GL.GenVertexArrays(1, &a); GL.GenBuffers(1, &b); GL.GenBuffers(1, &c);
            vao = a; vbo = b; ibo = c;
            GL.BindVertexArray(vao);
            GL.BindBuffer(GL.ARRAY_BUFFER, vbo);
            int st = UiVert.Size;
            GL.EnableVertexAttribArray(0); GL.VertexAttribPointer(0, 2, GL.FLOAT, 0, st, (IntPtr)0);
            GL.EnableVertexAttribArray(1); GL.VertexAttribPointer(1, 2, GL.FLOAT, 0, st, (IntPtr)8);
            GL.EnableVertexAttribArray(2); GL.VertexAttribPointer(2, 4, GL.UNSIGNED_BYTE, 1, st, (IntPtr)16);
            GL.EnableVertexAttribArray(3); GL.VertexAttribPointer(3, 1, GL.FLOAT, 0, st, (IntPtr)20);
            GL.EnableVertexAttribArray(4); GL.VertexAttribPointer(4, 4, GL.FLOAT, 0, st, (IntPtr)24);
            GL.EnableVertexAttribArray(5); GL.VertexAttribPointer(5, 4, GL.FLOAT, 0, st, (IntPtr)40);
            GL.BindBuffer(GL.ELEMENT_ARRAY_BUFFER, ibo);
            GrowIndices(4096);
            GL.BindVertexArray(0);
            imgTex = Tex.Empty(UiRec.ImgSize, UiRec.ImgSize, GL.RGBA8, GL.BGRA, GL.UNSIGNED_BYTE, true);
            GL.UseProgram(0);
        }

        /// <summary>Two triangles per quad, for at least n quads (the element buffer must be bound).</summary>
        void GrowIndices(int n)
        {
            if (n <= iboQuads) return;
            int q = Math.Max(n, iboQuads * 2);
            uint[] idx = new uint[q * 6];
            for (int i = 0; i < q; i++)
            {
                uint v = (uint)(i * 4);
                idx[i * 6] = v; idx[i * 6 + 1] = v + 1; idx[i * 6 + 2] = v + 2;
                idx[i * 6 + 3] = v; idx[i * 6 + 4] = v + 2; idx[i * 6 + 5] = v + 3;
            }
            fixed (uint* p = idx) GL.BufferData(GL.ELEMENT_ARRAY_BUFFER, (IntPtr)(idx.Length * 4), (IntPtr)p, GL.STATIC_DRAW);
            iboQuads = q;
        }

        uint Page(int p)
        {
            while (pages.Count <= p)
            {
                uint t = Tex.Empty(UiRec.PageSize, UiRec.PageSize, GL.RGBA8, GL.BGRA, GL.UNSIGNED_BYTE, false);
                pages.Add(t);
            }
            return pages[p];
        }

        /// <summary>
        /// Draws the recorded interface into the bound framebuffer (vw by vh), the interface area being uiW by uiH at
        /// uiX, uiY from the top-left.
        /// </summary>
        public void Draw(int uiX, int uiY, int uiW, int uiH, int vw, int vh)
        {
            UiRec r = Rec;
            GL.PixelStorei(GL.UNPACK_ALIGNMENT, 4);
            GL.PixelStorei(GL.UNPACK_ROW_LENGTH, 0);
            GL.ActiveTexture(GL.TEXTURE0);
            for (int k = 0; k < r.Pending.Count; k++)
            {
                UiRec.Upload u = r.Pending[k];
                GL.BindTexture(GL.TEXTURE_2D, Page(u.Page));
                fixed (int* p = u.Px) GL.TexSubImage2D(GL.TEXTURE_2D, 0, u.X, u.Y, u.W, u.H, GL.BGRA, GL.UNSIGNED_BYTE, (IntPtr)p);
            }
            r.Pending.Clear();
            if (r.Images.Count > 0)
            {
                GL.BindTexture(GL.TEXTURE_2D, imgTex);
                for (int k = 0; k < r.Images.Count; k++)
                {
                    UiRec.Upload u = r.Images[k];
                    fixed (int* p = u.Px) GL.TexSubImage2D(GL.TEXTURE_2D, 0, u.X, u.Y, u.W, u.H, GL.BGRA, GL.UNSIGNED_BYTE, (IntPtr)p);
                }
            }
            if (r.Quads == 0 || r.Segs == 0) return;

            GL.BindVertexArray(vao);
            GL.BindBuffer(GL.ARRAY_BUFFER, vbo);
            int bytes = r.Quads * 4 * UiVert.Size;
            if (bytes > vboBytes) vboBytes = Math.Max(bytes, vboBytes * 2);
            GL.BufferData(GL.ARRAY_BUFFER, (IntPtr)vboBytes, IntPtr.Zero, GL.STREAM_DRAW);      // orphan last frame's
            fixed (UiVert* p = r.V) GL.BufferSubData(GL.ARRAY_BUFFER, IntPtr.Zero, (IntPtr)bytes, (IntPtr)p);
            if (r.Quads > iboQuads) GrowIndices(r.Quads);

            prog.Use();
            prog.F4("uRect", uiX / (float)vw * 2 - 1, 1 - uiY / (float)vh * 2, uiW / (float)vw * 2, uiH / (float)vh * 2);
            prog.F2("uSize", r.PW, r.PH);
            GL.ActiveTexture(GL.TEXTURE0 + 1); GL.BindTexture(GL.TEXTURE_2D, imgTex);
            GL.ActiveTexture(GL.TEXTURE0);
            GL.Disable(GL.DEPTH_TEST); GL.Disable(GL.CULL_FACE);
            GL.Enable(GL.BLEND); GL.BlendFunc(GL.ONE, GL.ONE_MINUS_SRC_ALPHA);
            GL.Enable(GL.SCISSOR_TEST);
            float kx = uiW / (float)r.PW, ky = uiH / (float)r.PH;
            int bound = -1;
            for (int s = 0; s < r.Segs; s++)
            {
                int q0 = r.SegStart[s], q1 = s + 1 < r.Segs ? r.SegStart[s + 1] : r.Quads;
                if (q1 <= q0) continue;
                int c0 = r.SegClip[s * 4], c1 = r.SegClip[s * 4 + 1], c2 = r.SegClip[s * 4 + 2], c3 = r.SegClip[s * 4 + 3];
                if (c2 <= c0 || c3 <= c1) continue;
                int sx0 = uiX + (int)Math.Floor(c0 * kx), sx1 = uiX + (int)Math.Ceiling(c2 * kx);
                int sy0 = uiY + (int)Math.Floor(c1 * ky), sy1 = uiY + (int)Math.Ceiling(c3 * ky);
                GL.Scissor(sx0, vh - sy1, sx1 - sx0, sy1 - sy0);
                if (r.SegPage[s] != bound) { bound = r.SegPage[s]; GL.BindTexture(GL.TEXTURE_2D, Page(bound)); }
                GL.DrawElements(GL.TRIANGLES, (q1 - q0) * 6, GL.UNSIGNED_INT, (IntPtr)(q0 * 6 * 4));
            }
            GL.Disable(GL.SCISSOR_TEST);
            GL.Disable(GL.BLEND);
            GL.BindVertexArray(0);
        }

        public void Dispose()
        {
            for (int i = 0; i < pages.Count; i++) { uint t = pages[i]; Tex.Delete(ref t); }
            pages.Clear();
            Tex.Delete(ref imgTex);
            uint a = vao, b = vbo, c = ibo;
            if (a != 0) GL.DeleteVertexArrays(1, &a);
            if (b != 0) GL.DeleteBuffers(1, &b);
            if (c != 0) GL.DeleteBuffers(1, &c);
            vao = vbo = ibo = 0;
        }
    }
}
