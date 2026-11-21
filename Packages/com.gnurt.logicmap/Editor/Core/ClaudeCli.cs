using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Gnurt.LogicMap
{
    /// <summary>
    /// Kiểm tra máy có Claude Code CLI (lệnh <c>claude</c>) không, để bật vùng hỏi Claude.
    /// Chạy <c>claude --version</c> ở thread nền nên không làm đứng Unity. Kết quả nhớ trong phiên làm việc.
    /// </summary>
    internal static class ClaudeCli
    {
        public enum Status
        {
            Unknown,
            Checking,
            Ready,
            Missing,
        }

        const string SessionKey = "Gnurt.LogicMap.ClaudeCli"; // "đường dẫn|version", hoặc "-" khi không có
        const string PathSetting = "Gnurt.LogicMap.ClaudePath";
        const int TimeoutMs = 10000;

        public static Status State { get; private set; }
        public static string Version { get; private set; }
        public static string ExecutablePath { get; private set; }

        /// <summary>Bắn khi trạng thái đổi (bắt đầu kiểm tra, có kết quả).</summary>
        public static event Action Changed;

        /// <summary>Đường dẫn tự chọn tới lệnh claude, lưu riêng cho từng người (không commit).</summary>
        public static string CustomPath
        {
            get
            {
                string value = EditorUserSettings.GetConfigValue(PathSetting);
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
            set => EditorUserSettings.SetConfigValue(PathSetting, value?.Trim() ?? "");
        }

        /// <summary>Lần đầu trong phiên thì kiểm tra, các lần sau dùng kết quả đã có.</summary>
        public static void EnsureChecked()
        {
            if (State != Status.Unknown) return;
            string cached = SessionState.GetString(SessionKey, null);
            if (cached != null) Apply(cached);
            else Check();
        }

        public static void Check()
        {
            if (State == Status.Checking) return;
            State = Status.Checking;
            Changed?.Invoke();

            var candidates = new List<string>();
            if (CustomPath != null) candidates.Add(CustomPath);
            candidates.AddRange(ClaudeCliLocator.Candidates(
                Environment.GetEnvironmentVariable("PATH"),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Application.platform == RuntimePlatform.WindowsEditor));

            Task.Run(() => Probe(candidates)).ContinueWith(task =>
            {
                string result = task.Status == TaskStatus.RanToCompletion ? task.Result : "-";
                MainThread.Run(() =>
                {
                    SessionState.SetString(SessionKey, result);
                    Apply(result);
                });
            });
        }

        static void Apply(string value)
        {
            int bar = value.IndexOf('|');
            if (bar > 0)
            {
                ExecutablePath = value.Substring(0, bar);
                Version = value.Substring(bar + 1);
                State = Status.Ready;
            }
            else
            {
                ExecutablePath = null;
                Version = null;
                State = Status.Missing;
            }
            Changed?.Invoke();
        }

        // Chạy ở thread nền: không gọi API của Unity ở đây.
        static string Probe(List<string> candidates)
        {
            foreach (string path in candidates)
            {
                if (!File.Exists(path)) continue;
                string version = ClaudeCliLocator.ParseVersion(Run(path, "--version"));
                if (version != null) return path + "|" + version;
            }
            return "-";
        }

        static string Run(string executable, string arguments)
        {
            var output = new System.Text.StringBuilder();
            var (fileName, args) = ProcessRunner.Wrap(executable, arguments);
            var result = ProcessRunner.RunAsync(fileName, args, line =>
            {
                lock (output) output.AppendLine(line);
            }, TimeoutMs).Result;
            if (result.Error != null || result.TimedOut) return null; // không chạy được: coi như không có
            lock (output) return output.ToString();
        }
    }
}
