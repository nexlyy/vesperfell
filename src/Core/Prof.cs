using System.Diagnostics;

namespace Vesperfell
{
    /// <summary>Accumulates where interface frames spend their time (read out by the smoke test).</summary>
    public static class Prof
    {
        public static readonly string[] Name = { "clear", "world marks", "screen fx", "hud", "overlay", "cursor/fade" };
        public static readonly double[] Sum = new double[Name.Length];
        static long t0;
        public static void Mark() { t0 = Stopwatch.GetTimestamp(); }
        public static void Add(int slot) { long t = Stopwatch.GetTimestamp(); Sum[slot] += (t - t0) * 1000.0 / Stopwatch.Frequency; t0 = t; }
    }
}
