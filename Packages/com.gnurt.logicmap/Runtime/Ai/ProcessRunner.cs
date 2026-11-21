using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Gnurt.LogicMap
{
    internal readonly struct ProcessResult
    {
        public readonly int ExitCode;
        public readonly bool TimedOut;
        public readonly string Error; // không chạy được lệnh (không có file...), null nếu chạy được

        public ProcessResult(int exitCode, bool timedOut, string error)
        {
            ExitCode = exitCode;
            TimedOut = timedOut;
            Error = error;
        }

        public bool Succeeded => Error == null && !TimedOut && ExitCode == 0;
    }

    /// <summary>
    /// Chạy một lệnh ẩn (không mở cửa sổ console), đọc từng dòng output, không làm đứng Unity.
    /// <c>onLine</c> được gọi ở thread nền: không gọi API của Unity trong đó.
    /// </summary>
    internal static class ProcessRunner
    {
        public static Task<ProcessResult> RunAsync(string fileName, string arguments, Action<string> onLine, int timeoutMs)
        {
            return Task.Run(() =>
            {
                try
                {
                    var info = new ProcessStartInfo(fileName, arguments ?? "")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                    };
                    using (var process = new Process { StartInfo = info })
                    {
                        process.OutputDataReceived += (_, e) => { if (e.Data != null) onLine?.Invoke(e.Data); };
                        process.ErrorDataReceived += (_, e) => { if (e.Data != null) onLine?.Invoke(e.Data); };
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        if (!process.WaitForExit(timeoutMs))
                        {
                            try { process.Kill(); } catch { /* đã thoát */ }
                            process.WaitForExit(2000);
                            return new ProcessResult(-1, true, null);
                        }
                        process.WaitForExit(); // chờ đọc hết output còn lại
                        return new ProcessResult(process.ExitCode, false, null);
                    }
                }
                catch (Exception e)
                {
                    return new ProcessResult(-1, false, e.Message);
                }
            });
        }

        static readonly System.Text.RegularExpressions.Regex AnsiRegex =
            new System.Text.RegularExpressions.Regex(@"\x1B(?:\[[0-?]*[ -/]*[@-~]|\][^\x07]*\x07|[@-Z\\-_])");

        /// <summary>Bỏ mã màu và điều khiển con trỏ (ANSI) mà trình cài in ra cho terminal.</summary>
        public static string StripAnsi(string line) => line == null ? null : AnsiRegex.Replace(line, "").Replace("\r", "");

        /// <summary>File .cmd/.bat (bản cài qua npm trên Windows) phải chạy qua cmd.exe.</summary>
        public static (string fileName, string arguments) Wrap(string executable, string arguments)
        {
            if (executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                return ("cmd.exe", $"/c \"\"{executable}\" {arguments}\"");
            return (executable, arguments);
        }
    }
}
