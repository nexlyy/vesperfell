using System;

namespace Vesperfell
{
    /// <summary>Virtual key codes (same numbering as Windows).</summary>
    public static class Key
    {
        public const int Back = 8, Tab = 9, Enter = 13, Shift = 16, Ctrl = 17, Alt = 18, Esc = 27, Space = 32,
            Left = 37, Up = 38, Right = 39, Down = 40,
            N1 = 49, N2 = 50, N3 = 51, N4 = 52, N5 = 53,
            A = 65, B = 66, C = 67, D = 68, E = 69, F = 70, G = 71, H = 72, I = 73, J = 74, K = 75, L = 76, M = 77, N = 78, O = 79, P = 80,
            Q = 81, R = 82, S = 83, T = 84, U = 85, V = 86, W = 87, X = 88, Y = 89, Z = 90,
            F1 = 112, F2 = 113, F3 = 114, F4 = 115, F5 = 116, F9 = 120, F10 = 121, F11 = 122, F12 = 123;
    }

    /// <summary>Keyboard and mouse state. 'Pressed' / 'Released' are edges that last exactly one simulation tick.</summary>
    public sealed class Input
    {
        public readonly bool[] Down = new bool[256], Pressed = new bool[256], Released = new bool[256];
        public readonly bool[] MDown = new bool[3], MPressed = new bool[3], MReleased = new bool[3];
        public int MX = 320, MY = 180, Wheel;
        /// <summary>How far the mouse moved since the last tick while the view holds it (raw counts, for looking around).</summary>
        public float MDX, MDY;
        /// <summary>Set while the smoke test drives the game: the real keyboard and mouse are then ignored.</summary>
        public bool Scripted;
        public bool AnyPressed;
        public string Typed = "";           // characters typed since the last tick (text fields and chat)

        public void SetKey(int k, bool down)
        {
            if (k < 0 || k > 255) return;
            if (down && !Down[k]) { Pressed[k] = true; AnyPressed = true; }
            if (!down && Down[k]) Released[k] = true;
            Down[k] = down;
        }

        public void SetMouse(int b, bool down)
        {
            if (b < 0 || b > 2) return;
            if (down && !MDown[b]) { MPressed[b] = true; AnyPressed = true; }
            if (!down && MDown[b]) MReleased[b] = true;
            MDown[b] = down;
        }

        public void EndTick()
        {
            Array.Clear(Pressed, 0, 256); Array.Clear(Released, 0, 256);
            Array.Clear(MPressed, 0, 3); Array.Clear(MReleased, 0, 3);
            Wheel = 0; AnyPressed = false; Typed = ""; MDX = MDY = 0;
        }

        public void ReleaseAll()
        {
            Array.Clear(Down, 0, 256); Array.Clear(MDown, 0, 3);
            EndTick();
        }

        /// <summary>Consumes a key press so later code in the same tick does not react to it.</summary>
        public bool Take(int k) { if (!Pressed[k]) return false; Pressed[k] = false; return true; }
        public bool TakeMouse(int b) { if (!MPressed[b]) return false; MPressed[b] = false; return true; }
    }
}
