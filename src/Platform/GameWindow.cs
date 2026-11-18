using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Vesperfell
{
    /// <summary>
    /// The game window. The 640x360 frame is software-rendered and stretched to the client area by an
    /// integer factor (so pixels stay square), with black bars for whatever does not divide evenly.
    /// </summary>
    internal sealed class GameWindow : Form
    {
        readonly Game game; readonly Input input;
        Native.BITMAPINFO bmi;
        int scale = 2, offX, offY;
        bool needClear = true;
        Cursor blank;
        GlContext gl;
        /// <summary>When set, the next 3D frame is also saved to this file (the smoke test reads what was really shown).</summary>
        public string CapturePath;

        // mouse look: the cursor is held in the middle of the window and its movement turns the view
        bool active, locked, skipDelta, rawSeen;
        readonly byte[] rawBuf = new byte[64];
        static readonly int RawHeader = 8 + 2 * IntPtr.Size;
        public bool MouseLocked { get { return locked; } }

        public bool Gl { get { return gl != null; } }
        /// <summary>Physical pixels per interface pixel on screen right now (the interface is drawn at this scale).</summary>
        public int UiScale { get { if (ForceUiScale > 0) return ForceUiScale; return gl != null && game.R3 != null ? game.R3.UiS : scale; } }
        /// <summary>Set from the command line (--uiscale N) to pin the interface scale, e.g. 1 for the classic pixel look.</summary>
        public static int ForceUiScale;
        /// <summary>How long the last frame took to hand the interface layer to OpenGL (frame-time breakdown).</summary>
        public static double MsComposite;

        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ClassStyle |= 0x20; return cp; }      // CS_OWNDC, which OpenGL wants
        }

        /// <summary>Starts the OpenGL renderer on this window. On failure the game keeps the software renderer.</summary>
        public bool StartGl(out string error)
        {
            error = null;
            try
            {
                gl = GlContext.Create(Handle, out error);
                if (gl == null) { RebuildClean(); return false; }
                game.R3 = new Renderer3D(game, gl);
                gl.VSync(true);
                Surface.Squash = 1f; Surface.YMove = 1f;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                game.R3 = null;
                if (gl != null) { try { gl.Dispose(); } catch { } gl = null; }
                RebuildClean();
                return false;
            }
        }

        /// <summary>A window that once had an OpenGL pixel format cannot go back to plain GDI drawing, so it gets a new handle.</summary>
        void RebuildClean() { try { RecreateHandle(); } catch { } needClear = true; }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (gl != null) gl.Release();
            base.OnHandleDestroyed(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterRawMouse();
            if (gl != null && !gl.Rebind(Handle))
            {
                // the context cannot follow the new window: carry on in software
                game.R3 = null; gl = null;
                Surface.Squash = 1.3f; Surface.YMove = 0.86f;
            }
        }

        /// <summary>Sizes the 3D target to the window before a frame is drawn into it.</summary>
        public void Begin3D()
        {
            if (gl == null || game.R3 == null) return;
            game.R3.SetTarget(Math.Max(64, ClientSize.Width), Math.Max(64, ClientSize.Height), false);
        }

        /// <summary>Lays the interface over the 3D frame and shows it.</summary>
        public void Present3D()
        {
            if (gl == null || game.R3 == null || IsDisposed || !IsHandleCreated || WindowState == FormWindowState.Minimized) return;
            Renderer3D r = game.R3;
            if (!game.WorldDrawn3D) r.Blank();
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            r.Composite(game.Final);
            MsComposite = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (CapturePath != null) { try { r.SavePng(CapturePath); } catch { } CapturePath = null; }
            gl.Swap();
        }

        /// <summary>Asks Windows for raw mouse movement (unaffected by pointer acceleration) for this window.</summary>
        void RegisterRawMouse()
        {
            try
            {
                Native.RAWINPUTDEVICE[] d = new Native.RAWINPUTDEVICE[1];
                d[0].UsagePage = 1; d[0].Usage = 2; d[0].Flags = 0; d[0].Target = Handle;
                Native.RegisterRawInputDevices(d, 1, (uint)Marshal.SizeOf(typeof(Native.RAWINPUTDEVICE)));
            }
            catch { }
        }

        void ReadRawMouse(IntPtr h)
        {
            uint size = (uint)rawBuf.Length;
            uint got = Native.GetRawInputData(h, Native.RID_INPUT, rawBuf, ref size, (uint)RawHeader);
            if (got == uint.MaxValue || got < RawHeader + 20) return;
            if (BitConverter.ToInt32(rawBuf, 0) != 0) return;                      // not the mouse
            int flags = BitConverter.ToUInt16(rawBuf, RawHeader);
            if ((flags & 1) != 0) return;                                          // absolute (tablet, remote desktop): the cursor fallback covers it
            int dx = BitConverter.ToInt32(rawBuf, RawHeader + 12), dy = BitConverter.ToInt32(rawBuf, RawHeader + 16);
            if (dx == 0 && dy == 0) return;
            rawSeen = true;
            if (locked && !input.Scripted) { input.MDX += dx; input.MDY += dy; }
        }

        /// <summary>
        /// Holds the mouse for looking around while the game wants it (first-person play with no window open) and gives
        /// it back otherwise. Called once per loop, before the simulation ticks.
        /// </summary>
        public void UpdateMouseLock(bool want)
        {
            bool can = want && active && !input.Scripted && IsHandleCreated && !IsDisposed && WindowState != FormWindowState.Minimized;
            if (can != locked)
            {
                locked = can;
                if (!locked)
                {
                    try { Native.ClipCursorOff(IntPtr.Zero); } catch { }
                    CenterCursor();
                    input.MX = Game.W / 2; input.MY = Game.H / 2;
                    return;
                }
                skipDelta = true;
            }
            if (!locked) return;
            Rectangle r = RectangleToScreen(ClientRectangle);
            Point c = new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
            Native.RECT rc; rc.Left = r.Left; rc.Top = r.Top; rc.Right = r.Right; rc.Bottom = r.Bottom;
            Native.ClipCursor(ref rc);
            Native.POINT p;
            if (Native.GetCursorPos(out p))
            {
                // without raw input (remote desktop, some tablets) the cursor's own travel is the movement
                if (!rawSeen && !skipDelta) { input.MDX += p.X - c.X; input.MDY += p.Y - c.Y; }
                if (p.X != c.X || p.Y != c.Y) Native.SetCursorPos(c.X, c.Y);
            }
            skipDelta = false;
            input.MX = Game.W / 2; input.MY = Game.H / 2;
        }

        void CenterCursor()
        {
            try
            {
                Rectangle r = RectangleToScreen(ClientRectangle);
                Native.SetCursorPos(r.Left + r.Width / 2, r.Top + r.Height / 2);
            }
            catch { }
        }

        public void DisposeGl()
        {
            if (game.R3 != null) { try { game.R3.Dispose(); } catch { } }
            if (gl != null) { try { gl.Dispose(); } catch { } gl = null; }
        }

        public GameWindow(Game g, Input inp)
        {
            game = g; input = inp;
            Text = "Vesperfell";
            BackColor = Color.Black;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
            ClientSize = new Size(Game.W * 2, Game.H * 2);
            bmi = new Native.BITMAPINFO();
            bmi.biSize = 40; bmi.biWidth = Game.W; bmi.biHeight = -Game.H; bmi.biPlanes = 1; bmi.biBitCount = 32; bmi.biCompression = 0;
            try
            {
                using (Bitmap b = new Bitmap(32, 32, PixelFormat.Format32bppArgb)) blank = new Cursor(b.GetHicon());
                Cursor = blank;
            }
            catch { }
            try { Icon = MakeIcon(); } catch { }
        }

        /// <summary>Builds the window icon from the game's own art (the bell on a dusk-blue field).</summary>
        public static Icon MakeIcon()
        {
            using (Bitmap b = RenderIcon(32)) return Icon.FromHandle(b.GetHicon());
        }

        public static Bitmap RenderIcon(int size)
        {
            Surface s = new Surface(32, 32);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float d = M.Dist(x, y, 15.5f, 15.5f);
                    s.Px[y * 32 + x] = d > 15.6f ? 0 : (Col.Lerp(Pal.Gl1, Pal.Ink1, y / 31f) & 0xFFFFFF) | Col.Opaque;
                }
            // bell silhouette
            Cv cv = new Cv(32, 32);
            for (int y = 7; y <= 23; y++)
            {
                float t = (y - 7) / 16f;
                float sh = t < 0.25f ? M.Sqrt(1f - ((0.25f - t) / 0.25f) * ((0.25f - t) / 0.25f)) : 1f;
                float hw = (5.5f + 2.5f * t) * (0.35f + 0.65f * sh) + (t > 0.8f ? (t - 0.8f) * 14f : 0);
                for (int x = -(int)hw; x <= (int)hw; x++) cv.Set(16 + x, y, x < -hw * 0.3f ? Pal.Go4 : (x < hw * 0.4f ? Pal.Go3 : Pal.Go1));
            }
            cv.Rect(15, 4, 2, 3, Pal.Go3); cv.Rect(15, 24, 2, 3, Pal.Go2); cv.Circle(16, 27.5f, 1.8f, Pal.Em5);
            cv.Outline(Pal.Ink0);
            Sprite sp = cv.ToSprite(0, 0);
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int sx = x * 32 / size, sy = y * 32 / size;
                    int c = sp.Px[sy * 32 + sx];
                    if (((c >> 24) & 255) == 0) c = s.Px[sy * 32 + sx];
                    bmp.SetPixel(x, y, Color.FromArgb(c));
                }
            return bmp;
        }

        public void ApplyDisplay(bool fullscreen, int windowScale)
        {
            System.Windows.Forms.Screen sc = System.Windows.Forms.Screen.FromControl(this);
            if (fullscreen)
            {
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Normal;
                Bounds = sc.Bounds;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.FixedSingle;
                WindowState = FormWindowState.Normal;
                int maxScale = Math.Max(1, Math.Min((sc.WorkingArea.Width - 16) / Game.W, (sc.WorkingArea.Height - 48) / Game.H));
                int s = M.Clamp(windowScale, 1, maxScale);
                ClientSize = new Size(Game.W * s, Game.H * s);
                Location = new Point(sc.WorkingArea.X + (sc.WorkingArea.Width - Width) / 2, sc.WorkingArea.Y + (sc.WorkingArea.Height - Height) / 2);
            }
            Relayout();
        }

        void Relayout()
        {
            int cw = ClientSize.Width, ch = ClientSize.Height;
            scale = Math.Max(1, Math.Min(cw / Game.W, ch / Game.H));
            offX = (cw - Game.W * scale) / 2; offY = (ch - Game.H * scale) / 2;
            needClear = true;
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Relayout(); }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { if (gl != null) return; needClear = true; Present(game.Final); }

        public unsafe void Present(Surface s)
        {
            if (gl != null || IsDisposed || !IsHandleCreated || WindowState == FormWindowState.Minimized) return;
            if (needClear)
            {
                needClear = false;
                try { using (Graphics g = CreateGraphics()) g.Clear(Color.Black); } catch { }
            }
            IntPtr hdc = Native.GetDC(Handle);
            if (hdc == IntPtr.Zero) return;
            Native.SetStretchBltMode(hdc, Native.COLORONCOLOR);
            bmi.biWidth = s.PW; bmi.biHeight = -s.PH;
            fixed (int* p = s.Px)
                Native.StretchDIBits(hdc, offX, offY, Game.W * scale, Game.H * scale, 0, 0, s.PW, s.PH, (IntPtr)p, ref bmi, 0, Native.SRCCOPY);
            Native.ReleaseDC(Handle, hdc);
        }

        /// <summary>Copies what is actually on the window back into a bitmap (used by the smoke test).</summary>
        public Bitmap CaptureClient()
        {
            Bitmap bmp = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                IntPtr dst = g.GetHdc();
                IntPtr src = Native.GetDC(Handle);
                Native.BitBlt(dst, 0, 0, bmp.Width, bmp.Height, src, 0, 0, Native.SRCCOPY);
                Native.ReleaseDC(Handle, src);
                g.ReleaseHdc(dst);
            }
            return bmp;
        }

        // input
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.F4 && (keyData & Keys.Alt) != 0) return base.ProcessCmdKey(ref msg, keyData);
            if (input.Scripted) return true;
            input.SetKey((int)k, true);
            if (k == Keys.Menu) input.SetKey(Key.Alt, true);
            if (k == Keys.ShiftKey) input.SetKey(Key.Shift, true);
            if (k == Keys.ControlKey) input.SetKey(Key.Ctrl, true);
            // the key message stops here, so ask Windows for the character it stands for (text fields and chat)
            if (msg.Msg == 0x0100)
            {
                Native.MSG m = new Native.MSG(); m.hwnd = msg.HWnd; m.message = (uint)msg.Msg; m.wParam = msg.WParam; m.lParam = msg.LParam;
                Native.TranslateMessage(ref m);
            }
            return true;
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            char c = e.KeyChar;
            if (input.Scripted) { e.Handled = true; return; }
            if ((c == '\b' || c == (char)22 || c >= ' ') && c != (char)127 && input.Typed.Length < 64) input.Typed += c;
            e.Handled = true;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (input.Scripted) { e.Handled = true; return; }
            input.SetKey((int)e.KeyCode, false);
            if (e.KeyCode == Keys.Menu) input.SetKey(Key.Alt, false);
            if (e.KeyCode == Keys.ShiftKey) input.SetKey(Key.Shift, false);
            if (e.KeyCode == Keys.ControlKey) input.SetKey(Key.Ctrl, false);
            e.Handled = true;
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112, SC_KEYMENU = 0xF100, WM_SYSKEYUP = 0x0105;
            if (m.Msg == WM_SYSCOMMAND && ((int)m.WParam & 0xFFF0) == SC_KEYMENU) return;      // Alt must not open the window menu
            if (m.Msg == WM_SYSKEYUP && !input.Scripted) { int vk = (int)m.WParam; input.SetKey(vk, false); if (vk == 18) input.SetKey(Key.Alt, false); }
            if (m.Msg == Native.WM_INPUT) { try { ReadRawMouse(m.LParam); } catch { } }
            base.WndProc(ref m);
        }

        void SetMouse(MouseEventArgs e)
        {
            if (locked) { input.MX = Game.W / 2; input.MY = Game.H / 2; return; }
            input.MX = M.Clamp((e.X - offX) / scale, 0, Game.W - 1);
            input.MY = M.Clamp((e.Y - offY) / scale, 0, Game.H - 1);
        }

        static int Btn(MouseButtons b) { return b == MouseButtons.Left ? 0 : (b == MouseButtons.Right ? 1 : (b == MouseButtons.Middle ? 2 : -1)); }

        protected override void OnMouseMove(MouseEventArgs e) { if (!input.Scripted) SetMouse(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (input.Scripted) return; SetMouse(e); input.SetMouse(Btn(e.Button), true); }
        protected override void OnMouseUp(MouseEventArgs e) { if (input.Scripted) return; SetMouse(e); input.SetMouse(Btn(e.Button), false); }
        protected override void OnMouseWheel(MouseEventArgs e) { if (!input.Scripted) input.Wheel += e.Delta / 120; }

        protected override void OnActivated(EventArgs e) { base.OnActivated(e); active = true; }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            active = false;
            if (locked) { locked = false; try { Native.ClipCursorOff(IntPtr.Zero); } catch { } }
            if (input.Scripted) return;          // the smoke test keeps its own input whatever else happens on the desktop
            input.ReleaseAll();
            game.OnFocusLost();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            game.WantQuit = true;
            base.OnFormClosing(e);
        }
    }
}
