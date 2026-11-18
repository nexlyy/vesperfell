using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Vesperfell
{
    /// <summary>An OpenGL 3.3 core context on a window (the game window, or a hidden one for automated tests).</summary>
    internal sealed unsafe class GlContext : IDisposable
    {
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        public IntPtr Hwnd, Hdc, Ctx;
        public string Renderer = "", Version = "";
        public int MaxSamples, MaxTex;

        static GL.PIXELFORMATDESCRIPTOR Format()
        {
            GL.PIXELFORMATDESCRIPTOR pfd = new GL.PIXELFORMATDESCRIPTOR();
            pfd.nSize = (ushort)Marshal.SizeOf(typeof(GL.PIXELFORMATDESCRIPTOR)); pfd.nVersion = 1;
            pfd.dwFlags = GL.PFD_DRAW_TO_WINDOW | GL.PFD_SUPPORT_OPENGL | GL.PFD_DOUBLEBUFFER;
            pfd.iPixelType = 0; pfd.cColorBits = 32; pfd.cAlphaBits = 8; pfd.cDepthBits = 24; pfd.cStencilBits = 8;
            return pfd;
        }

        /// <summary>
        /// Moves the context onto a new window (WinForms rebuilds a window's handle for some style changes). The context and
        /// everything uploaded to it survive; only the device context changes.
        /// </summary>
        public bool Rebind(IntPtr hwnd)
        {
            IntPtr hdc = GetDC(hwnd);
            if (hdc == IntPtr.Zero) return false;
            GL.PIXELFORMATDESCRIPTOR pfd = Format();
            int fmt = GL.ChoosePixelFormat(hdc, ref pfd);
            if (fmt == 0 || GL.SetPixelFormat(hdc, fmt, ref pfd) == 0) { ReleaseDC(hwnd, hdc); return false; }
            Hwnd = hwnd; Hdc = hdc;
            return GL.wglMakeCurrent(hdc, Ctx) != 0;
        }

        /// <summary>Lets go of the window's device context before the window goes away.</summary>
        public void Release() { GL.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero); Hdc = IntPtr.Zero; }

        public static GlContext Create(IntPtr hwnd, out string error)
        {
            error = null;
            IntPtr hdc = GetDC(hwnd);
            if (hdc == IntPtr.Zero) { error = "no device context"; return null; }
            GL.PIXELFORMATDESCRIPTOR pfd = Format();
            int fmt = GL.ChoosePixelFormat(hdc, ref pfd);
            if (fmt == 0) { error = "no OpenGL pixel format"; ReleaseDC(hwnd, hdc); return null; }
            if (GL.SetPixelFormat(hdc, fmt, ref pfd) == 0) { error = "could not set the pixel format"; ReleaseDC(hwnd, hdc); return null; }
            IntPtr legacy = GL.wglCreateContext(hdc);
            if (legacy == IntPtr.Zero) { error = "OpenGL is not available"; ReleaseDC(hwnd, hdc); return null; }
            GL.wglMakeCurrent(hdc, legacy);
            GL.D_CreateContextAttribs create = GL.LoadCreateContextAttribs();
            IntPtr ctx = IntPtr.Zero;
            if (create != null)
            {
                int* attribs = stackalloc int[9];
                attribs[0] = 0x2091; attribs[1] = 3;        // WGL_CONTEXT_MAJOR_VERSION_ARB
                attribs[2] = 0x2092; attribs[3] = 3;        // WGL_CONTEXT_MINOR_VERSION_ARB
                attribs[4] = 0x9126; attribs[5] = 1;        // WGL_CONTEXT_PROFILE_MASK_ARB = core
                attribs[6] = 0x2094; attribs[7] = 0;        // WGL_CONTEXT_FLAGS_ARB
                attribs[8] = 0;
                ctx = create(hdc, IntPtr.Zero, attribs);
            }
            string legacyVer = GL.GetString(GL.VERSION);
            if (ctx == IntPtr.Zero)
            {
                GL.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero); GL.wglDeleteContext(legacy); ReleaseDC(hwnd, hdc);
                error = "OpenGL 3.3 is not supported (driver reports " + legacyVer + ")";
                return null;
            }
            GL.wglMakeCurrent(hdc, ctx);
            GL.wglDeleteContext(legacy);
            string miss = GL.Load();
            if (miss != null)
            {
                GL.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero); GL.wglDeleteContext(ctx); ReleaseDC(hwnd, hdc);
                error = "OpenGL functions missing: " + miss;
                return null;
            }
            GlContext c = new GlContext();
            c.Hwnd = hwnd; c.Hdc = hdc; c.Ctx = ctx;
            c.Renderer = GL.GetString(GL.RENDERER); c.Version = GL.GetString(GL.VERSION);
            int v;
            GL.GetIntegerv(GL.MAX_SAMPLES, &v); c.MaxSamples = v;
            GL.GetIntegerv(GL.MAX_TEXTURE_SIZE, &v); c.MaxTex = v;
            while (GL.GetError() != GL.NO_ERROR) { }
            return c;
        }

        public void MakeCurrent() { GL.wglMakeCurrent(Hdc, Ctx); }
        public void Swap() { GL.SwapBuffers(Hdc); }
        public void VSync(bool on) { if (GL.SwapInterval != null) GL.SwapInterval(on ? 1 : 0); }

        public void Dispose()
        {
            if (Ctx != IntPtr.Zero) { GL.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero); GL.wglDeleteContext(Ctx); Ctx = IntPtr.Zero; }
            if (Hdc != IntPtr.Zero) { ReleaseDC(Hwnd, Hdc); Hdc = IntPtr.Zero; }
        }
    }

    /// <summary>A window that is never shown: it only carries an OpenGL context for headless 3D tests.</summary>
    internal sealed class GlHostWindow : Form
    {
        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ClassStyle |= 0x20; return cp; }     // CS_OWNDC
        }
        public GlHostWindow() { ShowInTaskbar = false; FormBorderStyle = FormBorderStyle.None; Width = 64; Height = 64; }
    }

    /// <summary>A linked GLSL program with cached uniform locations.</summary>
    internal sealed unsafe class Prog
    {
        public readonly uint Id; public readonly string Name;
        readonly Dictionary<string, int> locs = new Dictionary<string, int>();

        public Prog(string name, string vs, string fs)
        {
            Name = name;
            uint v = Compile(GL.VERTEX_SHADER, vs, name + ".vs"), f = Compile(GL.FRAGMENT_SHADER, fs, name + ".fs");
            Id = GL.CreateProgram();
            GL.AttachShader(Id, v); GL.AttachShader(Id, f);
            GL.BindAttribLocation(Id, 0, "aPos"); GL.BindAttribLocation(Id, 1, "aNrm"); GL.BindAttribLocation(Id, 2, "aCol");
            GL.BindAttribLocation(Id, 3, "aUv"); GL.BindAttribLocation(Id, 4, "aExt");
            GL.BindFragDataLocation(Id, 0, "o");
            GL.LinkProgram(Id);
            int ok;
            GL.GetProgramiv(Id, GL.LINK_STATUS, &ok);
            if (ok == 0) throw new Exception("link " + name + ": " + Log(Id, false));
            GL.DeleteShader(v); GL.DeleteShader(f);
        }

        static uint Compile(uint type, string src, string name)
        {
            uint s = GL.CreateShader(type);
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(src);
            fixed (byte* b = bytes)
            {
                IntPtr* arr = stackalloc IntPtr[1]; arr[0] = (IntPtr)b;
                int len = bytes.Length;
                GL.ShaderSource(s, 1, arr, &len);
            }
            GL.CompileShader(s);
            int ok;
            GL.GetShaderiv(s, GL.COMPILE_STATUS, &ok);
            if (ok == 0) throw new Exception("compile " + name + ": " + Log(s, true));
            return s;
        }

        static string Log(uint obj, bool shader)
        {
            int len = 0;
            if (shader) GL.GetShaderiv(obj, GL.INFO_LOG_LENGTH, &len); else GL.GetProgramiv(obj, GL.INFO_LOG_LENGTH, &len);
            if (len <= 1) return "(no log)";
            byte[] buf = new byte[len];
            fixed (byte* b = buf)
            {
                int got;
                if (shader) GL.GetShaderInfoLog(obj, len, &got, b); else GL.GetProgramInfoLog(obj, len, &got, b);
                return System.Text.Encoding.ASCII.GetString(buf, 0, Math.Max(0, got));
            }
        }

        public void Use() { GL.UseProgram(Id); }
        public int Loc(string n)
        {
            int l;
            if (!locs.TryGetValue(n, out l)) { l = GL.GetUniformLocation(Id, n); locs[n] = l; }
            return l;
        }
        public void I(string n, int v) { int l = Loc(n); if (l >= 0) GL.Uniform1i(l, v); }
        public void F(string n, float v) { int l = Loc(n); if (l >= 0) GL.Uniform1f(l, v); }
        public void F2(string n, float x, float y) { int l = Loc(n); if (l >= 0) GL.Uniform2f(l, x, y); }
        public void F3(string n, float x, float y, float z) { int l = Loc(n); if (l >= 0) GL.Uniform3f(l, x, y, z); }
        public void F3(string n, V3 v) { F3(n, v.X, v.Y, v.Z); }
        public void F4(string n, float x, float y, float z, float w) { int l = Loc(n); if (l >= 0) GL.Uniform4f(l, x, y, z, w); }
        public void Rgb(string n, int rgb, float k) { F3(n, ((rgb >> 16) & 255) / 255f * k, ((rgb >> 8) & 255) / 255f * k, (rgb & 255) / 255f * k); }
        public void Mat(string n, float[] m) { int l = Loc(n); if (l >= 0) fixed (float* p = m) GL.UniformMatrix4fv(l, 1, 0, p); }
        public void F4v(string n, float[] v, int count) { int l = Loc(n); if (l >= 0 && count > 0) fixed (float* p = v) GL.Uniform4fv(l, count, p); }
        public void F3v(string n, float[] v, int count) { int l = Loc(n); if (l >= 0 && count > 0) fixed (float* p = v) GL.Uniform3fv(l, count, p); }
        public void F1v(string n, float[] v, int count) { int l = Loc(n); if (l >= 0 && count > 0) fixed (float* p = v) GL.Uniform1fv(l, count, p); }
    }

    internal static unsafe class Tex
    {
        public static uint Gen()
        {
            uint t;
            GL.GenTextures(1, &t);
            return t;
        }

        public static void Delete(ref uint t) { if (t != 0) { uint x = t; GL.DeleteTextures(1, &x); t = 0; } }

        public static void Params(uint target, int minF, int magF, int wrap)
        {
            GL.TexParameteri(target, GL.TEXTURE_MIN_FILTER, minF);
            GL.TexParameteri(target, GL.TEXTURE_MAG_FILTER, magF);
            GL.TexParameteri(target, GL.TEXTURE_WRAP_S, wrap);
            GL.TexParameteri(target, GL.TEXTURE_WRAP_T, wrap);
        }

        /// <summary>Uploads 0xAARRGGBB pixels (the engine's own format) as an RGBA texture.</summary>
        public static uint FromArgb(int[] px, int w, int h, bool nearest, bool mips, bool repeat)
        {
            uint t = Gen();
            GL.BindTexture(GL.TEXTURE_2D, t);
            GL.PixelStorei(GL.UNPACK_ALIGNMENT, 4);
            fixed (int* p = px) GL.TexImage2D(GL.TEXTURE_2D, 0, GL.RGBA8, w, h, 0, GL.BGRA, GL.UNSIGNED_BYTE, (IntPtr)p);
            int min = mips ? (nearest ? GL.NEAREST_MIPMAP_LINEAR : GL.LINEAR_MIPMAP_LINEAR) : (nearest ? GL.NEAREST : GL.LINEAR);
            Params(GL.TEXTURE_2D, min, nearest ? GL.NEAREST : GL.LINEAR, repeat ? GL.REPEAT : GL.CLAMP_TO_EDGE);
            if (mips) GL.GenerateMipmap(GL.TEXTURE_2D);
            return t;
        }

        public static uint Empty(int w, int h, int internalFmt, uint fmt, uint type, bool linear)
        {
            uint t = Gen();
            GL.BindTexture(GL.TEXTURE_2D, t);
            GL.TexImage2D(GL.TEXTURE_2D, 0, internalFmt, w, h, 0, fmt, type, IntPtr.Zero);
            Params(GL.TEXTURE_2D, linear ? GL.LINEAR : GL.NEAREST, linear ? GL.LINEAR : GL.NEAREST, GL.CLAMP_TO_EDGE);
            return t;
        }
    }

    /// <summary>A framebuffer: a colour texture (or multisampled renderbuffer) with an optional depth attachment.</summary>
    internal sealed unsafe class Fbo
    {
        public uint Id, ColorTex, ColorRb, DepthTex, DepthRb;
        public int W, H, Samples;

        /// <summary>colorFmt 0 = no colour; depth 0 = none, 1 = renderbuffer, 2 = sampleable depth texture (shadow maps).</summary>
        public static Fbo Create(int w, int h, int colorFmt, int depth, int samples, bool linear)
        {
            Fbo f = new Fbo(); f.W = w; f.H = h; f.Samples = samples;
            uint id; GL.GenFramebuffers(1, &id); f.Id = id;
            GL.BindFramebuffer(GL.FRAMEBUFFER, id);
            if (colorFmt != 0)
            {
                if (samples > 1)
                {
                    uint rb; GL.GenRenderbuffers(1, &rb); f.ColorRb = rb;
                    GL.BindRenderbuffer(GL.RENDERBUFFER, rb);
                    GL.RenderbufferStorageMultisample(GL.RENDERBUFFER, samples, (uint)colorFmt, w, h);
                    GL.FramebufferRenderbuffer(GL.FRAMEBUFFER, GL.COLOR_ATTACHMENT0, GL.RENDERBUFFER, rb);
                }
                else
                {
                    uint fmt = GL.RGBA, type = colorFmt == GL.RGBA16F || colorFmt == GL.R11F_G11F_B10F ? GL.HALF_FLOAT : GL.UNSIGNED_BYTE;
                    f.ColorTex = Tex.Empty(w, h, colorFmt, fmt, type, linear);
                    GL.FramebufferTexture2D(GL.FRAMEBUFFER, GL.COLOR_ATTACHMENT0, GL.TEXTURE_2D, f.ColorTex, 0);
                }
            }
            else
            {
                GL.DrawBuffer(0); GL.ReadBuffer(0);
            }
            if (depth == 1)
            {
                uint rb; GL.GenRenderbuffers(1, &rb); f.DepthRb = rb;
                GL.BindRenderbuffer(GL.RENDERBUFFER, rb);
                if (samples > 1) GL.RenderbufferStorageMultisample(GL.RENDERBUFFER, samples, (uint)GL.DEPTH_COMPONENT24, w, h);
                else GL.RenderbufferStorage(GL.RENDERBUFFER, (uint)GL.DEPTH_COMPONENT24, w, h);
                GL.FramebufferRenderbuffer(GL.FRAMEBUFFER, GL.DEPTH_ATTACHMENT, GL.RENDERBUFFER, rb);
            }
            else if (depth == 2)
            {
                f.DepthTex = Tex.Gen();
                GL.BindTexture(GL.TEXTURE_2D, f.DepthTex);
                GL.TexImage2D(GL.TEXTURE_2D, 0, GL.DEPTH_COMPONENT24, w, h, 0, GL.DEPTH_COMPONENT, GL.UNSIGNED_INT, IntPtr.Zero);
                Tex.Params(GL.TEXTURE_2D, GL.LINEAR, GL.LINEAR, GL.CLAMP_TO_EDGE);
                GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_COMPARE_MODE, (int)GL.COMPARE_REF_TO_TEXTURE);
                GL.TexParameteri(GL.TEXTURE_2D, GL.TEXTURE_COMPARE_FUNC, (int)GL.LEQUAL);
                GL.FramebufferTexture2D(GL.FRAMEBUFFER, GL.DEPTH_ATTACHMENT, GL.TEXTURE_2D, f.DepthTex, 0);
            }
            uint st = GL.CheckFramebufferStatus(GL.FRAMEBUFFER);
            GL.BindFramebuffer(GL.FRAMEBUFFER, 0);
            if (st != GL.FRAMEBUFFER_COMPLETE) { f.Dispose(); throw new Exception("framebuffer incomplete (0x" + st.ToString("X") + ")"); }
            return f;
        }

        public void Bind() { GL.BindFramebuffer(GL.FRAMEBUFFER, Id); GL.Viewport(0, 0, W, H); }

        public void Dispose()
        {
            uint x;
            if (Id != 0) { x = Id; GL.DeleteFramebuffers(1, &x); Id = 0; }
            if (ColorRb != 0) { x = ColorRb; GL.DeleteRenderbuffers(1, &x); ColorRb = 0; }
            if (DepthRb != 0) { x = DepthRb; GL.DeleteRenderbuffers(1, &x); DepthRb = 0; }
            Tex.Delete(ref ColorTex); Tex.Delete(ref DepthTex);
        }
    }
}
