using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Gnurt.LogicMap.Tests
{
    public class ClaudeInstallTests
    {
        static bool IsWindows => Path.DirectorySeparatorChar == '\\';

        [Test]
        public void InstallCommand_UsesOfficialNativeInstaller()
        {
            var windows = ClaudeInstallCommand.For(windows: true);
            Assert.AreEqual("powershell.exe", windows.FileName);
            Assert.AreEqual("irm https://claude.ai/install.ps1 | iex", windows.Display);
            StringAssert.Contains(windows.Display, windows.Arguments);

            var unix = ClaudeInstallCommand.For(windows: false);
            Assert.AreEqual("/bin/bash", unix.FileName);
            Assert.AreEqual("curl -fsSL https://claude.ai/install.sh | bash", unix.Display);
            StringAssert.Contains(unix.Display, unix.Arguments);
        }

        [Test]
        public void Wrap_RunsCmdFilesThroughCmdExe()
        {
            var wrapped = ProcessRunner.Wrap(@"C:\npm\claude.cmd", "--version");
            Assert.AreEqual("cmd.exe", wrapped.fileName);
            Assert.AreEqual("/c \"\"C:\\npm\\claude.cmd\" --version\"", wrapped.arguments);

            var direct = ProcessRunner.Wrap(@"C:\bin\claude.exe", "--version");
            Assert.AreEqual(@"C:\bin\claude.exe", direct.fileName);
            Assert.AreEqual("--version", direct.arguments);
        }

        [Test]
        public void StripAnsi_RemovesColorsAndCursorCodes()
        {
            // Dùng \u001B thay vì \x1B: \x trong C# ăn tiếp chữ số hex phía sau ("\x07D" là một ký tự).
            Assert.AreEqual("Installing Claude Code 2.1.0", ProcessRunner.StripAnsi("\u001B[?25l\u001B[32mInstalling\u001B[0m Claude Code 2.1.0\r"));
            Assert.AreEqual("Done ✔", ProcessRunner.StripAnsi("\u001B]0;title\u0007Done ✔"));
            Assert.AreEqual("plain", ProcessRunner.StripAnsi("plain"));
        }

        [Test]
        public void Run_CollectsOutputAndExitCode()
        {
            var lines = new List<string>();
            var result = IsWindows
                ? ProcessRunner.RunAsync("cmd.exe", "/c echo one& echo two 1>&2& exit /b 3", Collect(lines), 20000).Result
                : ProcessRunner.RunAsync("/bin/sh", "-c \"echo one; echo two 1>&2; exit 3\"", Collect(lines), 20000).Result;

            Assert.IsNull(result.Error);
            Assert.IsFalse(result.TimedOut);
            Assert.AreEqual(3, result.ExitCode);
            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(lines, "one");
            CollectionAssert.Contains(lines, "two");
        }

        [Test]
        public void Run_MissingProgramReportsError()
        {
            var result = ProcessRunner.RunAsync("gnurt-no-such-program-xyz", "", null, 5000).Result;
            Assert.IsNotNull(result.Error);
            Assert.IsFalse(result.Succeeded);
        }

        [Test]
        public void Run_StopsWhenTooSlow()
        {
            var result = IsWindows
                ? ProcessRunner.RunAsync("powershell.exe", "-NoProfile -NonInteractive -Command \"Start-Sleep -Seconds 20\"", null, 1500).Result
                : ProcessRunner.RunAsync("/bin/sh", "-c \"sleep 20\"", null, 1500).Result;
            Assert.IsTrue(result.TimedOut);
            Assert.IsFalse(result.Succeeded);
        }

        static System.Action<string> Collect(List<string> lines) => line =>
        {
            lock (lines) lines.Add(line.Trim());
        };
    }
}
