using System;
using System.Collections.Generic;
using System.Globalization;

namespace Vesperfell
{
    /// <summary>
    /// Localisation. English is the source language: every string in the code is English, and is looked up in a
    /// translation table at the moment it is measured or drawn. Strings built from pieces use Loc.F with a pattern.
    /// </summary>
    public static class Loc
    {
        public const int EN = 0, RU = 1;
        public static int Lang;
        public static bool Collect;
        public static readonly HashSet<string> Missing = new HashSet<string>();
        static Dictionary<string, string> ru;

        static Dictionary<string, string> Table { get { if (ru == null) ru = LocRu.Build(); return ru; } }

        public static string Code { get { return Lang == RU ? "ru" : "en"; } }

        public static void Set(string code) { Lang = code == "ru" ? RU : EN; }

        /// <summary>Russian by default on a Russian-language Windows, English everywhere else.</summary>
        public static string DefaultCode()
        {
            try { return CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en"; }
            catch { return "en"; }
        }

        public static string Tr(string s)
        {
            if (s == null || s.Length == 0) return s;
            if (s[0] == RawMark) return s.Substring(1);
            if (Lang == EN) return s;
            string r;
            if (Table.TryGetValue(s, out r)) return r;
            if (Collect && Wants(s)) Missing.Add(s);
            return s;
        }

        /// <summary>Marks text that must be shown exactly as it is (names people typed, chat).</summary>
        public const char RawMark = '\u0001';
        public static string Raw(string s) { return RawMark + (s ?? ""); }

        /// <summary>Translates a pattern, then fills in its {0}, {1}... arguments.</summary>
        public static string F(string pattern, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, Tr(pattern), args);
        }

        static bool Wants(string s)
        {
            int run = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '^' && i + 1 < s.Length) { i++; run = 0; continue; }
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) { if (++run >= 3) return true; }
                else run = 0;
            }
            return false;
        }

        // item names
        // Generated item names are English phrases ("Sturdy Longsword of the Wolf"). In Russian the adjective must
        // agree with the noun, so the phrase is taken apart and rebuilt.
        public sealed class Noun { public string Ru; public int Gender; public Noun(string ru, int g) { Ru = ru; Gender = g; } }   // 0 m, 1 f, 2 n, 3 plural
        public sealed class Adj { public string[] Forms; public string After; public Adj(string m, string f, string n, string pl) { Forms = new[] { m, f, n, pl }; } public Adj(string after) { After = after; } }

        public static string ItemName(string en)
        {
            if (Lang == EN || string.IsNullOrEmpty(en)) return en;
            string r;
            if (Table.TryGetValue(en, out r)) return r;
            string rest = en, suffix = "";
            foreach (KeyValuePair<string, string> kv in LocRu.Suffixes)
                if (rest.EndsWith(" " + kv.Key, StringComparison.Ordinal)) { suffix = " " + kv.Value; rest = rest.Substring(0, rest.Length - kv.Key.Length - 1); break; }
            Adj adj = null;
            foreach (KeyValuePair<string, Adj> kv in LocRu.Prefixes)
                if (rest.StartsWith(kv.Key + " ", StringComparison.Ordinal)) { adj = kv.Value; rest = rest.Substring(kv.Key.Length + 1); break; }
            Noun noun;
            if (!LocRu.Nouns.TryGetValue(rest, out noun))
            {
                if (Collect) Missing.Add("[item] " + en);
                return en;
            }
            string name = noun.Ru;
            if (adj != null)
            {
                if (adj.After != null) name = name + " " + adj.After;
                else name = adj.Forms[noun.Gender] + " " + LowerFirst(name);
            }
            return name + suffix;
        }

        static string LowerFirst(string s) { return s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s.Substring(1); }
    }
}
