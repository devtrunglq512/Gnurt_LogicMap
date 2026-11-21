namespace Gnurt.LogicMap
{
    /// <summary>
    /// Lệnh cài Claude Code CLI bằng trình cài native chính thức (cách docs khuyên dùng,
    /// https://code.claude.com/docs/en/setup). Cài vào ~/.local/bin, chỗ <see cref="ClaudeCliLocator"/> đã tìm sẵn.
    /// </summary>
    internal sealed class ClaudeInstallCommand
    {
        public const string NpmCommand = "npm install -g @anthropic-ai/claude-code@latest";

        public string FileName;
        public string Arguments;
        public string Display; // lệnh để người dùng tự gõ nếu muốn
        public string Shell;   // gõ lệnh trên ở đâu

        public static ClaudeInstallCommand For(bool windows)
        {
            if (windows)
            {
                const string script = "irm https://claude.ai/install.ps1 | iex";
                return new ClaudeInstallCommand
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
                    Display = script,
                    Shell = "PowerShell",
                };
            }

            const string bash = "curl -fsSL https://claude.ai/install.sh | bash";
            return new ClaudeInstallCommand
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{bash}\"",
                Display = bash,
                Shell = "Terminal",
            };
        }
    }
}
