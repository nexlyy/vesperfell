using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Vesperfell
{
    /// <summary>
    /// The slice of OpenGL 3.3 the 3D renderer uses. Version 1.1 entry points come straight from opengl32.dll;
    /// everything newer is fetched from the driver with wglGetProcAddress once a context exists.
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    internal static unsafe class GL
    {
        const string Lib = "opengl32.dll";

        // constants
        public const uint COLOR_BUFFER_BIT = 0x4000, DEPTH_BUFFER_BIT = 0x100, STENCIL_BUFFER_BIT = 0x400;
        public const uint POINTS = 0, LINES = 1, TRIANGLES = 4, TRIANGLE_STRIP = 5;
        public const uint DEPTH_TEST = 0x0B71, BLEND = 0x0BE2, CULL_FACE = 0x0B44, SCISSOR_TEST = 0x0C11, POLYGON_OFFSET_FILL = 0x8037,
            MULTISAMPLE = 0x809D, FRAMEBUFFER_SRGB = 0x8DB9, DEPTH_CLAMP = 0x864F;
        public const uint ZERO = 0, ONE = 1, SRC_COLOR = 0x0300, ONE_MINUS_SRC_COLOR = 0x0301, SRC_ALPHA = 0x0302, ONE_MINUS_SRC_ALPHA = 0x0303,
            DST_ALPHA = 0x0304, ONE_MINUS_DST_ALPHA = 0x0305, DST_COLOR = 0x0306;
        public const uint FUNC_ADD = 0x8006, MAX = 0x8008;
        public const uint NEVER = 0x0200, LESS = 0x0201, EQUAL = 0x0202, LEQUAL = 0x0203, GREATER = 0x0204, GEQUAL = 0x0206, ALWAYS = 0x0207;
        public const uint FRONT = 0x0404, BACK = 0x0405, CW = 0x0900, CCW = 0x0901;
        public const uint TEXTURE_2D = 0x0DE1, TEXTURE0 = 0x84C0, TEXTURE_2D_MULTISAMPLE = 0x9100;
        public const uint RED = 0x1903, RGB = 0x1907, RGBA = 0x1908, BGRA = 0x80E1, DEPTH_COMPONENT = 0x1902;
        public const int R8 = 0x8229, RGB8 = 0x8051, RGBA8 = 0x8058, RGBA16F = 0x881A, R11F_G11F_B10F = 0x8C3A, DEPTH_COMPONENT24 = 0x81A6,
            DEPTH_COMPONENT32F = 0x8CAC, DEPTH24_STENCIL8 = 0x88F0;
        public const uint UNSIGNED_BYTE = 0x1401, BYTE = 0x1400, SHORT = 0x1402, UNSIGNED_SHORT = 0x1403, INT = 0x1404, UNSIGNED_INT = 0x1405,
            FLOAT = 0x1406, HALF_FLOAT = 0x140B, UNSIGNED_INT_8_8_8_8_REV = 0x8367;
        public const uint TEXTURE_MAG_FILTER = 0x2800, TEXTURE_MIN_FILTER = 0x2801, TEXTURE_WRAP_S = 0x2802, TEXTURE_WRAP_T = 0x2803,
            TEXTURE_MAX_LEVEL = 0x813D, TEXTURE_COMPARE_MODE = 0x884C, TEXTURE_COMPARE_FUNC = 0x884D, COMPARE_REF_TO_TEXTURE = 0x884E,
            TEXTURE_BORDER_COLOR = 0x1004, TEXTURE_MAX_ANISOTROPY = 0x84FE, MAX_TEXTURE_MAX_ANISOTROPY = 0x84FF;
        public const int NEAREST = 0x2600, LINEAR = 0x2601, NEAREST_MIPMAP_NEAREST = 0x2700, LINEAR_MIPMAP_NEAREST = 0x2701,
            NEAREST_MIPMAP_LINEAR = 0x2702, LINEAR_MIPMAP_LINEAR = 0x2703, REPEAT = 0x2901, CLAMP_TO_EDGE = 0x812F, CLAMP_TO_BORDER = 0x812D;
        public const uint ARRAY_BUFFER = 0x8892, ELEMENT_ARRAY_BUFFER = 0x8893, STATIC_DRAW = 0x88E4, DYNAMIC_DRAW = 0x88E8, STREAM_DRAW = 0x88E0;
        public const uint PIXEL_UNPACK_BUFFER = 0x88EC;
        public const uint VERTEX_SHADER = 0x8B31, FRAGMENT_SHADER = 0x8B30, COMPILE_STATUS = 0x8B81, LINK_STATUS = 0x8B82, INFO_LOG_LENGTH = 0x8B84;
        public const uint FRAMEBUFFER = 0x8D40, READ_FRAMEBUFFER = 0x8CA8, DRAW_FRAMEBUFFER = 0x8CA9, RENDERBUFFER = 0x8D41,
            COLOR_ATTACHMENT0 = 0x8CE0, DEPTH_ATTACHMENT = 0x8D00, DEPTH_STENCIL_ATTACHMENT = 0x821A, FRAMEBUFFER_COMPLETE = 0x8CD5;
        public const uint UNPACK_ALIGNMENT = 0x0CF5, PACK_ALIGNMENT = 0x0D05, UNPACK_ROW_LENGTH = 0x0CF2;
        public const uint VENDOR = 0x1F00, RENDERER = 0x1F01, VERSION = 0x1F02, SHADING_LANGUAGE_VERSION = 0x8B8C;
        public const uint MAX_SAMPLES = 0x8D57, MAX_TEXTURE_SIZE = 0x0D33, NO_ERROR = 0;
        public const uint BACK_LEFT = 0x0402;
        public const uint TEXTURE_2D_ARRAY = 0x8C1A, TEXTURE_WRAP_R = 0x8072, TEXTURE_BASE_LEVEL = 0x813C, TEXTURE_LOD_BIAS = 0x8501;
        public const uint READ_FRAMEBUFFER_BINDING = 0x8CAA, DRAW_FRAMEBUFFER_BINDING = 0x8CA6, LINEAR_U = 0x2601, NEAREST_U = 0x2600;

        // 1.1, exported by opengl32.dll
        [DllImport(Lib, EntryPoint = "glClear")] public static extern void Clear(uint mask);
        [DllImport(Lib, EntryPoint = "glClearColor")] public static extern void ClearColor(float r, float g, float b, float a);
        [DllImport(Lib, EntryPoint = "glClearDepth")] public static extern void ClearDepth(double d);
        [DllImport(Lib, EntryPoint = "glViewport")] public static extern void Viewport(int x, int y, int w, int h);
        [DllImport(Lib, EntryPoint = "glScissor")] public static extern void Scissor(int x, int y, int w, int h);
        [DllImport(Lib, EntryPoint = "glEnable")] public static extern void Enable(uint cap);
        [DllImport(Lib, EntryPoint = "glDisable")] public static extern void Disable(uint cap);
        [DllImport(Lib, EntryPoint = "glBlendFunc")] public static extern void BlendFunc(uint s, uint d);
        [DllImport(Lib, EntryPoint = "glDepthFunc")] public static extern void DepthFunc(uint f);
        [DllImport(Lib, EntryPoint = "glDepthMask")] public static extern void DepthMask(byte flag);
        [DllImport(Lib, EntryPoint = "glColorMask")] public static extern void ColorMask(byte r, byte g, byte b, byte a);
        [DllImport(Lib, EntryPoint = "glCullFace")] public static extern void CullFace(uint mode);
        [DllImport(Lib, EntryPoint = "glFrontFace")] public static extern void FrontFace(uint mode);
        [DllImport(Lib, EntryPoint = "glPolygonOffset")] public static extern void PolygonOffset(float factor, float units);
        [DllImport(Lib, EntryPoint = "glDrawArrays")] public static extern void DrawArrays(uint mode, int first, int count);
        [DllImport(Lib, EntryPoint = "glDrawElements")] public static extern void DrawElements(uint mode, int count, uint type, IntPtr indices);
        [DllImport(Lib, EntryPoint = "glGenTextures")] public static extern void GenTextures(int n, uint* tex);
        [DllImport(Lib, EntryPoint = "glDeleteTextures")] public static extern void DeleteTextures(int n, uint* tex);
        [DllImport(Lib, EntryPoint = "glBindTexture")] public static extern void BindTexture(uint target, uint tex);
        [DllImport(Lib, EntryPoint = "glTexImage2D")] public static extern void TexImage2D(uint target, int level, int internalFormat, int w, int h, int border, uint format, uint type, IntPtr data);
        [DllImport(Lib, EntryPoint = "glTexSubImage2D")] public static extern void TexSubImage2D(uint target, int level, int x, int y, int w, int h, uint format, uint type, IntPtr data);
        [DllImport(Lib, EntryPoint = "glTexParameteri")] public static extern void TexParameteri(uint target, uint pname, int param);
        [DllImport(Lib, EntryPoint = "glTexParameterf")] public static extern void TexParameterf(uint target, uint pname, float param);
        [DllImport(Lib, EntryPoint = "glTexParameterfv")] public static extern void TexParameterfv(uint target, uint pname, float* param);
        [DllImport(Lib, EntryPoint = "glPixelStorei")] public static extern void PixelStorei(uint pname, int param);
        [DllImport(Lib, EntryPoint = "glReadPixels")] public static extern void ReadPixels(int x, int y, int w, int h, uint format, uint type, IntPtr data);
        [DllImport(Lib, EntryPoint = "glGetError")] public static extern uint GetError();
        [DllImport(Lib, EntryPoint = "glGetString")] static extern IntPtr GetStringRaw(uint name);
        [DllImport(Lib, EntryPoint = "glGetIntegerv")] public static extern void GetIntegerv(uint pname, int* data);
        [DllImport(Lib, EntryPoint = "glGetFloatv")] public static extern void GetFloatv(uint pname, float* data);
        [DllImport(Lib, EntryPoint = "glFinish")] public static extern void Finish();
        [DllImport(Lib, EntryPoint = "glFlush")] public static extern void Flush();
        [DllImport(Lib, EntryPoint = "glDrawBuffer")] public static extern void DrawBuffer(uint mode);
        [DllImport(Lib, EntryPoint = "glReadBuffer")] public static extern void ReadBuffer(uint mode);

        public static string GetString(uint name)
        {
            IntPtr p = GetStringRaw(name);
            return p == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(p);
        }

        // wgl / gdi
        [StructLayout(LayoutKind.Sequential)]
        public struct PIXELFORMATDESCRIPTOR
        {
            public ushort nSize, nVersion; public uint dwFlags;
            public byte iPixelType, cColorBits, cRedBits, cRedShift, cGreenBits, cGreenShift, cBlueBits, cBlueShift, cAlphaBits, cAlphaShift,
                cAccumBits, cAccumRedBits, cAccumGreenBits, cAccumBlueBits, cAccumAlphaBits, cDepthBits, cStencilBits, cAuxBuffers, iLayerType, bReserved;
            public uint dwLayerMask, dwVisibleMask, dwDamageMask;
        }
        public const uint PFD_DOUBLEBUFFER = 1, PFD_DRAW_TO_WINDOW = 4, PFD_SUPPORT_OPENGL = 0x20;

        [DllImport(Lib)] public static extern IntPtr wglCreateContext(IntPtr hdc);
        [DllImport(Lib)] public static extern int wglMakeCurrent(IntPtr hdc, IntPtr ctx);
        [DllImport(Lib)] public static extern int wglDeleteContext(IntPtr ctx);
        [DllImport(Lib, CharSet = CharSet.Ansi)] public static extern IntPtr wglGetProcAddress(string name);
        [DllImport("gdi32.dll")] public static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR pfd);
        [DllImport("gdi32.dll")] public static extern int SetPixelFormat(IntPtr hdc, int format, ref PIXELFORMATDESCRIPTOR pfd);
        [DllImport("gdi32.dll")] public static extern int SwapBuffers(IntPtr hdc);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr LoadLibrary(string name);

        // 1.2+ through the driver
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_GenIds(int n, uint* ids);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_Bind(uint target, uint id);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_Uint(uint a);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_BufferData(uint target, IntPtr size, IntPtr data, uint usage);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_BufferSubData(uint target, IntPtr offset, IntPtr size, IntPtr data);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_VertexAttribPointer(uint index, int size, uint type, byte normalized, int stride, IntPtr offset);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_VertexAttrib4f(uint index, float x, float y, float z, float w);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_UintUint(uint a, uint b);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate uint D_CreateShader(uint type);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate uint D_CreateProgram();
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_ShaderSource(uint shader, int count, IntPtr* strings, int* lengths);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_GetObjectiv(uint obj, uint pname, int* value);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_GetInfoLog(uint obj, int maxLen, int* len, byte* log);
        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)] public delegate void D_BindAttribLocation(uint program, uint index, string name);
        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)] public delegate int D_GetUniformLocation(uint program, string name);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_Uniform1i(int loc, int v);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_Uniform1f(int loc, float v);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_Uniform2f(int loc, float x, float y);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_Uniform3f(int loc, float x, float y, float z);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_Uniform4f(int loc, float x, float y, float z, float w);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_Uniformfv(int loc, int count, float* v);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_UniformMatrix(int loc, int count, byte transpose, float* v);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_FramebufferTexture2D(uint target, uint attachment, uint textarget, uint texture, int level);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_FramebufferRenderbuffer(uint target, uint attachment, uint rbtarget, uint rb);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_RenderbufferStorage(uint target, uint format, int w, int h);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_RenderbufferStorageMultisample(uint target, int samples, uint format, int w, int h);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate uint D_CheckFramebufferStatus(uint target);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_BlitFramebuffer(int sx0, int sy0, int sx1, int sy1, int dx0, int dy0, int dx1, int dy1, uint mask, uint filter);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_BlendFuncSeparate(uint srgb, uint drgb, uint sa, uint da);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_DrawElementsInstanced(uint mode, int count, uint type, IntPtr indices, int instances);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate int D_SwapInterval(int interval);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_TexImage3D(uint target, int level, int internalFormat, int w, int h, int depth, int border, uint format, uint type, IntPtr data);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_TexSubImage3D(uint target, int level, int x, int y, int z, int w, int h, int depth, uint format, uint type, IntPtr data);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate void D_DrawBuffers(int n, uint* bufs);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] public delegate IntPtr D_CreateContextAttribs(IntPtr hdc, IntPtr share, int* attribs);

        public static D_GenIds GenBuffers, DeleteBuffers, GenVertexArrays, DeleteVertexArrays, GenFramebuffers, DeleteFramebuffers, GenRenderbuffers, DeleteRenderbuffers;
        public static D_Bind BindBuffer, BindFramebuffer, BindRenderbuffer;
        public static D_Uint BindVertexArray, EnableVertexAttribArray, DisableVertexAttribArray, UseProgram, CompileShader, LinkProgram, DeleteShader, DeleteProgram,
            ActiveTexture, GenerateMipmap, BlendEquation;
        public static D_BufferData BufferData;
        public static D_BufferSubData BufferSubData;
        public static D_VertexAttribPointer VertexAttribPointer;
        public static D_VertexAttrib4f VertexAttrib4f;
        public static D_UintUint AttachShader, VertexAttribDivisor;
        public static D_CreateShader CreateShader;
        public static D_CreateProgram CreateProgram;
        public static D_ShaderSource ShaderSource;
        public static D_GetObjectiv GetShaderiv, GetProgramiv;
        public static D_GetInfoLog GetShaderInfoLog, GetProgramInfoLog;
        public static D_BindAttribLocation BindAttribLocation, BindFragDataLocation;
        public static D_GetUniformLocation GetUniformLocation;
        public static D_Uniform1i Uniform1i;
        public static D_Uniform1f Uniform1f;
        public static D_Uniform2f Uniform2f;
        public static D_Uniform3f Uniform3f;
        public static D_Uniform4f Uniform4f;
        public static D_Uniformfv Uniform1fv, Uniform3fv, Uniform4fv;
        public static D_UniformMatrix UniformMatrix4fv;
        public static D_FramebufferTexture2D FramebufferTexture2D;
        public static D_FramebufferRenderbuffer FramebufferRenderbuffer;
        public static D_RenderbufferStorage RenderbufferStorage;
        public static D_RenderbufferStorageMultisample RenderbufferStorageMultisample;
        public static D_CheckFramebufferStatus CheckFramebufferStatus;
        public static D_BlitFramebuffer BlitFramebuffer;
        public static D_BlendFuncSeparate BlendFuncSeparate;
        public static D_DrawElementsInstanced DrawElementsInstanced;
        public static D_SwapInterval SwapInterval;
        public static D_TexImage3D TexImage3D;
        public static D_TexSubImage3D TexSubImage3D;
        public static D_DrawBuffers DrawBuffers;

        static IntPtr glModule;

        static IntPtr Proc(string name)
        {
            IntPtr p = wglGetProcAddress(name);
            long v = p.ToInt64();
            if (v == 0 || v == 1 || v == 2 || v == 3 || v == -1)
            {
                if (glModule == IntPtr.Zero) glModule = LoadLibrary(Lib);
                p = GetProcAddress(glModule, name);
            }
            return p;
        }

        static T Fn<T>(string name, ref string missing) where T : class
        {
            IntPtr p = Proc(name);
            if (p == IntPtr.Zero) { missing = missing == null ? name : missing + ", " + name; return null; }
            return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }

        public static D_CreateContextAttribs LoadCreateContextAttribs()
        {
            IntPtr p = Proc("wglCreateContextAttribsARB");
            return p == IntPtr.Zero ? null : (Marshal.GetDelegateForFunctionPointer(p, typeof(D_CreateContextAttribs)) as D_CreateContextAttribs);
        }

        /// <summary>Fetches every entry point newer than 1.1. Returns the names that were missing, or null when all are there.</summary>
        public static string Load()
        {
            string miss = null;
            GenBuffers = Fn<D_GenIds>("glGenBuffers", ref miss); DeleteBuffers = Fn<D_GenIds>("glDeleteBuffers", ref miss);
            GenVertexArrays = Fn<D_GenIds>("glGenVertexArrays", ref miss); DeleteVertexArrays = Fn<D_GenIds>("glDeleteVertexArrays", ref miss);
            GenFramebuffers = Fn<D_GenIds>("glGenFramebuffers", ref miss); DeleteFramebuffers = Fn<D_GenIds>("glDeleteFramebuffers", ref miss);
            GenRenderbuffers = Fn<D_GenIds>("glGenRenderbuffers", ref miss); DeleteRenderbuffers = Fn<D_GenIds>("glDeleteRenderbuffers", ref miss);
            BindBuffer = Fn<D_Bind>("glBindBuffer", ref miss); BindFramebuffer = Fn<D_Bind>("glBindFramebuffer", ref miss); BindRenderbuffer = Fn<D_Bind>("glBindRenderbuffer", ref miss);
            BindVertexArray = Fn<D_Uint>("glBindVertexArray", ref miss);
            EnableVertexAttribArray = Fn<D_Uint>("glEnableVertexAttribArray", ref miss); DisableVertexAttribArray = Fn<D_Uint>("glDisableVertexAttribArray", ref miss);
            UseProgram = Fn<D_Uint>("glUseProgram", ref miss); CompileShader = Fn<D_Uint>("glCompileShader", ref miss); LinkProgram = Fn<D_Uint>("glLinkProgram", ref miss);
            DeleteShader = Fn<D_Uint>("glDeleteShader", ref miss); DeleteProgram = Fn<D_Uint>("glDeleteProgram", ref miss);
            ActiveTexture = Fn<D_Uint>("glActiveTexture", ref miss); GenerateMipmap = Fn<D_Uint>("glGenerateMipmap", ref miss); BlendEquation = Fn<D_Uint>("glBlendEquation", ref miss);
            BufferData = Fn<D_BufferData>("glBufferData", ref miss); BufferSubData = Fn<D_BufferSubData>("glBufferSubData", ref miss);
            VertexAttribPointer = Fn<D_VertexAttribPointer>("glVertexAttribPointer", ref miss); VertexAttrib4f = Fn<D_VertexAttrib4f>("glVertexAttrib4f", ref miss);
            AttachShader = Fn<D_UintUint>("glAttachShader", ref miss); VertexAttribDivisor = Fn<D_UintUint>("glVertexAttribDivisor", ref miss);
            CreateShader = Fn<D_CreateShader>("glCreateShader", ref miss); CreateProgram = Fn<D_CreateProgram>("glCreateProgram", ref miss);
            ShaderSource = Fn<D_ShaderSource>("glShaderSource", ref miss);
            GetShaderiv = Fn<D_GetObjectiv>("glGetShaderiv", ref miss); GetProgramiv = Fn<D_GetObjectiv>("glGetProgramiv", ref miss);
            GetShaderInfoLog = Fn<D_GetInfoLog>("glGetShaderInfoLog", ref miss); GetProgramInfoLog = Fn<D_GetInfoLog>("glGetProgramInfoLog", ref miss);
            BindAttribLocation = Fn<D_BindAttribLocation>("glBindAttribLocation", ref miss); BindFragDataLocation = Fn<D_BindAttribLocation>("glBindFragDataLocation", ref miss);
            GetUniformLocation = Fn<D_GetUniformLocation>("glGetUniformLocation", ref miss);
            Uniform1i = Fn<D_Uniform1i>("glUniform1i", ref miss); Uniform1f = Fn<D_Uniform1f>("glUniform1f", ref miss);
            Uniform2f = Fn<D_Uniform2f>("glUniform2f", ref miss); Uniform3f = Fn<D_Uniform3f>("glUniform3f", ref miss); Uniform4f = Fn<D_Uniform4f>("glUniform4f", ref miss);
            Uniform1fv = Fn<D_Uniformfv>("glUniform1fv", ref miss); Uniform3fv = Fn<D_Uniformfv>("glUniform3fv", ref miss); Uniform4fv = Fn<D_Uniformfv>("glUniform4fv", ref miss);
            UniformMatrix4fv = Fn<D_UniformMatrix>("glUniformMatrix4fv", ref miss);
            FramebufferTexture2D = Fn<D_FramebufferTexture2D>("glFramebufferTexture2D", ref miss);
            FramebufferRenderbuffer = Fn<D_FramebufferRenderbuffer>("glFramebufferRenderbuffer", ref miss);
            RenderbufferStorage = Fn<D_RenderbufferStorage>("glRenderbufferStorage", ref miss);
            RenderbufferStorageMultisample = Fn<D_RenderbufferStorageMultisample>("glRenderbufferStorageMultisample", ref miss);
            CheckFramebufferStatus = Fn<D_CheckFramebufferStatus>("glCheckFramebufferStatus", ref miss);
            BlitFramebuffer = Fn<D_BlitFramebuffer>("glBlitFramebuffer", ref miss);
            BlendFuncSeparate = Fn<D_BlendFuncSeparate>("glBlendFuncSeparate", ref miss);
            DrawElementsInstanced = Fn<D_DrawElementsInstanced>("glDrawElementsInstanced", ref miss);
            TexImage3D = Fn<D_TexImage3D>("glTexImage3D", ref miss);
            TexSubImage3D = Fn<D_TexSubImage3D>("glTexSubImage3D", ref miss);
            DrawBuffers = Fn<D_DrawBuffers>("glDrawBuffers", ref miss);
            string ignore = null;
            SwapInterval = Fn<D_SwapInterval>("wglSwapIntervalEXT", ref ignore);      // optional
            return miss;
        }
    }
}
