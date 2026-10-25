using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GJ.Game
{
    // The game's words in the player's language. Every line is written in English in the code and the scenes, and
    // looked up here by that English text when it is shown; the translations live in LocStrings. A line that isn't in
    // the table is shown as it is, and a block of several lines is looked up line by line (keeping any <size=…> tag
    // round a line), so a message put together from known lines still comes out translated.
    public static partial class Loc
    {
        public static readonly string[] Codes = { "en", "ru", "uk", "pt", "sk" };
        public static readonly string[] Names = { "ENGLISH", "РУССКИЙ", "УКРАЇНСЬКА", "PORTUGUÊS (BR)", "SLOVENČINA" };

        public static int Current { get; private set; }
        public static event System.Action Changed;

        // Lines asked for that have no translation, collected while checking a language (off in play).
        public static bool LogMissing;
        public static readonly HashSet<string> Missing = new HashSet<string>();

        static bool _loaded;
        static HashSet<string> _translated;
        static readonly Regex Sized = new Regex(@"^<size=(\d+)>(.*)</size>$", RegexOptions.Singleline);

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            Current = Mathf.Clamp(PlayerPrefs.GetInt("gj.language", FromSystem()), 0, Codes.Length - 1);
        }

        // First launch: the language the computer is set to, if the game has it.
        static int FromSystem()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Russian: return 1;
                case SystemLanguage.Ukrainian: return 2;
                case SystemLanguage.Portuguese: return 3;
                case SystemLanguage.Slovak: return 4;
                default: return 0;
            }
        }

        public static void Set(int index)
        {
            Load();
            Current = (index % Codes.Length + Codes.Length) % Codes.Length;
            PlayerPrefs.SetInt("gj.language", Current);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static string Name => Names[Current];

        // The line in the current language.
        public static string T(string en)
        {
            Load();
            if (Current == 0 || string.IsNullOrEmpty(en)) return en;
            if (Table.TryGetValue(en, out var row) && row.Length >= Current && !string.IsNullOrEmpty(row[Current - 1])) return row[Current - 1];
            if (en.IndexOf('\n') >= 0)
            {
                var lines = en.Split('\n');
                for (int i = 0; i < lines.Length; i++) lines[i] = T(lines[i]);
                return string.Join("\n", lines);
            }
            var m = Sized.Match(en);
            if (m.Success) return "<size=" + m.Groups[1].Value + ">" + T(m.Groups[2].Value) + "</size>";
            if (LogMissing && !IsTranslated(en)) Missing.Add(en);
            return en;
        }

        // A line with blanks ({0}, {1}) filled in after it is translated.
        public static string F(string en, params object[] args) => string.Format(T(en), args);

        static bool IsTranslated(string s)
        {
            if (_translated == null)
            {
                _translated = new HashSet<string>();
                foreach (var row in Table.Values) foreach (var t in row) if (!string.IsNullOrEmpty(t)) _translated.Add(t);
            }
            return _translated.Contains(s) || s.Trim().Length == 0 || !Regex.IsMatch(s, "[a-z]{3}");
        }
    }
}
