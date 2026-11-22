using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gnurt.LogicMap
{
    /// <summary>Vai trò của một mục trong game.</summary>
    internal enum SectionRole
    {
        Gameplay, // map, level, minigame
        Core,     // loading, menu, sảnh chờ...
    }

    /// <summary>Đoán ý nghĩa từ tên scene, tên thư mục.</summary>
    internal static class NameRules
    {
        static readonly HashSet<string> CoreWords = new HashSet<string>
        {
            "loading", "load", "loader", "splash", "boot", "bootstrap", "init", "intro", "menu", "main", "home",
            "lobby", "waiting", "title", "start", "launcher", "setting", "settings", "shop", "logo", "persistent",
        };

        static readonly HashSet<string> SectionFolderWords = new HashSet<string>
        {
            "map", "maps", "level", "levels", "stage", "stages", "minigame", "minigames", "chapter", "chapters",
            "world", "worlds", "round", "rounds",
        };

        /// <summary>Tách tên thành các từ viết thường: "0_WaitingRoom" → waiting, room.</summary>
        public static List<string> Words(string name) => Tokens(name).Select(t => t.ToLowerInvariant()).ToList();

        /// <summary>Tách tên thành các từ, giữ nguyên chữ hoa: "Item_FireSword" → Item, Fire, Sword.</summary>
        public static List<string> Tokens(string name)
        {
            var words = new List<string>();
            var current = new StringBuilder();
            void Flush()
            {
                if (current.Length > 0) words.Add(current.ToString());
                current.Clear();
            }

            name ??= "";
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (!char.IsLetter(c))
                {
                    Flush();
                    continue;
                }
                bool upper = char.IsUpper(c);
                bool afterLower = i > 0 && char.IsLower(name[i - 1]);
                bool endOfAcronym = i > 0 && char.IsUpper(name[i - 1]) && i + 1 < name.Length && char.IsLower(name[i + 1]);
                if (upper && (afterLower || endOfAcronym)) Flush();
                current.Append(c);
            }
            Flush();
            return words;
        }

        /// <summary>Scene tên kiểu Loading, Menu, Main, Lobby là màn chung; còn lại coi là gameplay.</summary>
        public static SectionRole GuessRole(string name) =>
            Words(name).Any(CoreWords.Contains) ? SectionRole.Core : SectionRole.Gameplay;

        /// <summary>Thư mục tên kiểu Maps, Levels, MiniGames: mỗi prefab bên trong có thể là một mục của game.</summary>
        public static bool LooksLikeSectionFolder(string folderName)
        {
            var words = Words(folderName);
            return words.Any(SectionFolderWords.Contains) || SectionFolderWords.Contains(string.Concat(words));
        }

        /// <summary>So tên theo kiểu người đọc: "Map 2" đứng trước "Map 10".</summary>
        public static int NaturalCompare(string a, string b)
        {
            a ??= "";
            b ??= "";
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    int si = i, sj = j;
                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;
                    string na = a.Substring(si, i - si).TrimStart('0');
                    string nb = b.Substring(sj, j - sj).TrimStart('0');
                    if (na.Length != nb.Length) return na.Length.CompareTo(nb.Length);
                    int cmp = string.CompareOrdinal(na, nb);
                    if (cmp != 0) return cmp;
                }
                else
                {
                    int cmp = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
                    if (cmp != 0) return cmp;
                    i++;
                    j++;
                }
            }
            return (a.Length - i).CompareTo(b.Length - j);
        }

        public static readonly Comparer<string> Natural = Comparer<string>.Create(NaturalCompare);
    }
}
