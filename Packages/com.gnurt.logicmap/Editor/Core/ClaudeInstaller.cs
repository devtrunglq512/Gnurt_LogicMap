using System;
using System.Collections.Generic;
using System.Linq;
using Gnurt.Editor;
using UnityEditor;
using UnityEngine;

namespace Gnurt.LogicMap
{
    /// <summary>
    /// Cài Claude Code CLI bằng trình cài chính thức của Anthropic, chạy ẩn ở nền và giữ lại output để hiện lên panel.
    /// Luôn hỏi người dùng trước khi chạy vì lệnh tải và chạy script từ claude.ai.
    /// </summary>
    internal static class ClaudeInstaller
    {
        public enum Status
        {
            Idle,
            Running,
            Succeeded,
            Failed,
        }

        const int MaxLines = 200;
        const int TimeoutMs = 15 * 60 * 1000;
        static readonly GLogger Log = GLog.Tag("LogicMap");
        static readonly List<string> Lines = new List<string>();
        static readonly object Lock = new object();
        static bool _notifyQueued;

        public static Status State { get; private set; }
        public static string Error { get; private set; }

        /// <summary>Bắn trên main thread khi trạng thái đổi hoặc có dòng output mới.</summary>
        public static event Action Changed;

        public static ClaudeInstallCommand Command => ClaudeInstallCommand.For(Application.platform == RuntimePlatform.WindowsEditor);

        /// <summary>Mấy dòng output cuối của trình cài.</summary>
        public static List<string> Tail(int count)
        {
            lock (Lock) return Lines.Skip(Math.Max(0, Lines.Count - count)).ToList();
        }

        public static void Install()
        {
            if (State == Status.Running) return;
            var command = Command;
            bool confirmed = EditorUtility.DisplayDialog(
                Loc.T("Install Claude Code", "Cài Claude Code"),
                Loc.T(
                    $"LogicMap will run Anthropic's official installer (downloaded from claude.ai):\n\n{command.Display}\n\n" +
                    "Claude Code needs a paid Claude plan (Pro, Max, Team, Enterprise) or a Console account. " +
                    "After installing, run claude once in a terminal to sign in.",
                    $"LogicMap sẽ chạy trình cài chính thức của Anthropic (tải từ claude.ai):\n\n{command.Display}\n\n" +
                    "Claude Code cần tài khoản Claude trả phí (Pro, Max, Team, Enterprise) hoặc tài khoản Console. " +
                    "Cài xong, chạy lệnh claude một lần trong terminal để đăng nhập."),
                Loc.T("Install", "Cài"),
                Loc.T("Cancel", "Huỷ"));
            if (!confirmed) return;

            lock (Lock) Lines.Clear();
            State = Status.Running;
            Error = null;
            Changed?.Invoke();
            Log.Info("Đang cài Claude Code: " + command.Display);

            ProcessRunner.RunAsync(command.FileName, command.Arguments, AddLine, TimeoutMs)
                .ContinueWith(task => MainThread.Run(() => Finish(task.Result)));
        }

        // Chạy ở thread nền: chỉ ghi lại dòng, gom lại rồi báo giao diện ở main thread.
        static void AddLine(string line)
        {
            line = ProcessRunner.StripAnsi(line).Trim();
            if (line.Length == 0) return;
            lock (Lock)
            {
                Lines.Add(line);
                if (Lines.Count > MaxLines) Lines.RemoveAt(0);
                if (_notifyQueued) return;
                _notifyQueued = true;
            }
            MainThread.Run(() =>
            {
                lock (Lock) _notifyQueued = false;
                Changed?.Invoke();
            });
        }

        static void Finish(ProcessResult result)
        {
            if (result.Error != null)
                Error = Loc.T("Could not start the installer: ", "Không chạy được trình cài: ") + result.Error;
            else if (result.TimedOut)
                Error = Loc.T("The installer took too long and was stopped.", "Trình cài chạy quá lâu nên đã bị dừng.");
            else if (result.ExitCode != 0)
                Error = Loc.T($"The installer exited with code {result.ExitCode}.", $"Trình cài thoát với mã {result.ExitCode}.");
            else
                Error = null;

            State = Error == null ? Status.Succeeded : Status.Failed;
            if (Error == null) Log.Info("Đã cài xong Claude Code");
            else Log.Warning(Error);
            Changed?.Invoke();
            ClaudeCli.Check(); // tìm lại lệnh claude vừa cài
        }
    }
}
