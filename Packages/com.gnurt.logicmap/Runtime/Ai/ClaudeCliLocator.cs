using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Gnurt.LogicMap
{
    /// <summary>
    /// Các chỗ có thể có lệnh <c>claude</c> (Claude Code CLI). Unity mở từ Unity Hub thường không có
    /// PATH đầy đủ như terminal, nên thêm cả các thư mục cài đặt quen thuộc.
    /// </summary>
    internal static class ClaudeCliLocator
    {
        public static List<string> Candidates(string pathEnv, string home, string appData, bool windows)
        {
            var dirs = new List<string>();
            foreach (string dir in (pathEnv ?? "").Split(windows ? ';' : ':'))
                dirs.Add(dir.Trim().Trim('"'));

            if (!string.IsNullOrEmpty(home))
            {
                dirs.Add(Path.Combine(home, ".local", "bin"));    // bản cài native
                dirs.Add(Path.Combine(home, ".claude", "local")); // bản cài cũ
                if (!windows) dirs.Add(Path.Combine(home, ".npm-global", "bin"));
            }
            if (windows && !string.IsNullOrEmpty(appData)) dirs.Add(Path.Combine(appData, "npm"));
            if (!windows)
            {
                dirs.Add("/usr/local/bin");
                dirs.Add("/opt/homebrew/bin");
            }

            var names = windows ? new[] { "claude.exe", "claude.cmd" } : new[] { "claude" };
            var result = new List<string>();
            var seen = new HashSet<string>(windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (string dir in dirs)
            {
                if (string.IsNullOrEmpty(dir)) continue;
                foreach (string name in names)
                {
                    string path = Path.Combine(dir, name);
                    if (seen.Add(path)) result.Add(path);
                }
            }
            return result;
        }

        static readonly Regex VersionRegex = new Regex(@"\d+\.\d+\.\d+(?:[-+][\w.]+)?");

        /// <summary>"2.1.3 (Claude Code)" → "2.1.3". null nếu không thấy số version.</summary>
        public static string ParseVersion(string output)
        {
            var match = VersionRegex.Match(output ?? "");
            return match.Success ? match.Value : null;
        }
    }
}
