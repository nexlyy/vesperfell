using System;
using System.Runtime.InteropServices;

namespace Vesperfell
{
    /// <summary>Thin P/Invoke layer: GDI presentation, timers and the waveOut audio device.</summary>
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public int biSize; public int biWidth; public int biHeight; public short biPlanes; public short biBitCount;
            public int biCompression; public int biSizeImage; public int biXPelsPerMeter; public int biYPelsPerMeter;
            public int biClrUsed; public int biClrImportant;
            public int c0, c1, c2, c3;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WAVEFORMATEX
        {
            public ushort wFormatTag; public ushort nChannels; public uint nSamplesPerSec; public uint nAvgBytesPerSec;
            public ushort nBlockAlign; public ushort wBitsPerSample; public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WAVEHDR
        {
            public IntPtr lpData; public uint dwBufferLength; public uint dwBytesRecorded; public IntPtr dwUser;
            public uint dwFlags; public uint dwLoops; public IntPtr lpNext; public IntPtr reserved;
        }

        public const uint SRCCOPY = 0x00CC0020;
        public const int COLORONCOLOR = 3;
        public const int HALFTONE = 4;
        public const uint WHDR_DONE = 1, WHDR_PREPARED = 2, WHDR_INQUEUE = 0x10;
        public const uint WAVE_MAPPER = 0xFFFFFFFF;

        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("user32.dll")] public static extern int ShowCursor(bool show);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);

        // mouse capture for looking around: raw mouse counts, and a cursor held inside the window
        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE { public ushort UsagePage; public ushort Usage; public uint Flags; public IntPtr Target; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        public const int WM_INPUT = 0x00FF;
        public const uint RID_INPUT = 0x10000003;
        [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);
        [DllImport("user32.dll")] public static extern uint GetRawInputData(IntPtr hRawInput, uint command, byte[] data, ref uint size, uint headerSize);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern bool ClipCursor(ref RECT r);
        [DllImport("user32.dll", EntryPoint = "ClipCursor")] public static extern bool ClipCursorOff(IntPtr none);

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int ptX, ptY; }
        [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG msg);

        [DllImport("gdi32.dll")]
        public static extern int StretchDIBits(IntPtr hdc, int xd, int yd, int wd, int hd, int xs, int ys, int ws, int hs,
            IntPtr bits, ref BITMAPINFO bmi, uint usage, uint rop);
        [DllImport("gdi32.dll")]
        public static extern int SetDIBitsToDevice(IntPtr hdc, int xd, int yd, uint w, uint h, int xs, int ys, uint startScan, uint scanLines,
            IntPtr bits, ref BITMAPINFO bmi, uint usage);
        [DllImport("gdi32.dll")] public static extern int SetStretchBltMode(IntPtr hdc, int mode);
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr hdcDst, int x, int y, int w, int h, IntPtr hdcSrc, int sx, int sy, uint rop);

        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);

        [DllImport("winmm.dll")] public static extern int waveOutOpen(out IntPtr hwo, uint deviceId, ref WAVEFORMATEX fmt, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] public static extern int waveOutPrepareHeader(IntPtr hwo, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] public static extern int waveOutUnprepareHeader(IntPtr hwo, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] public static extern int waveOutWrite(IntPtr hwo, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] public static extern int waveOutReset(IntPtr hwo);
        [DllImport("winmm.dll")] public static extern int waveOutClose(IntPtr hwo);
        [DllImport("winmm.dll")] public static extern uint waveOutGetNumDevs();

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")] public static extern void CopyMemory(IntPtr dst, IntPtr src, UIntPtr len);
    }
}
