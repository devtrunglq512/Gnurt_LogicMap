using Gnurt.Editor;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>
    /// Vùng hỏi Claude. Bản 0.1.0 báo máy có Claude Code CLI hay không và cài giúp nếu chưa có;
    /// hỏi đáp thật làm ở bản sau, nên ô nhập luôn bị khoá.
    /// </summary>
    internal sealed class AskClaudePanel : VisualElement
    {
        const int LogLines = 6;

        public AskClaudePanel()
        {
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ClaudeCli.Changed += Rebuild;
                ClaudeInstaller.Changed += Rebuild;
                ClaudeCli.EnsureChecked();
                Rebuild();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                ClaudeCli.Changed -= Rebuild;
                ClaudeInstaller.Changed -= Rebuild;
            });
        }

        void Rebuild()
        {
            Clear();
            var card = GnurtUI.Card(Loc.T("Ask Claude", "Hỏi Claude"));
            Add(card);
            card.Add(GnurtUI.Inline(StatusBadge()));

            bool installing = ClaudeInstaller.State == ClaudeInstaller.Status.Running;
            switch (ClaudeCli.State)
            {
                case ClaudeCli.Status.Ready:
                    card.Add(GnurtUI.Hint(Loc.T(
                        "Claude Code is installed. First time: run claude in a terminal to sign in. Asking questions here comes in LogicMap 0.2.0.",
                        "Máy đã có Claude Code. Lần đầu dùng: chạy lệnh claude trong terminal để đăng nhập. Hỏi đáp ngay trong window có ở LogicMap 0.2.0.")));
                    break;
                case ClaudeCli.Status.Missing:
                    AddMissing(card, installing);
                    break;
                default:
                    card.Add(GnurtUI.Hint(Loc.T("Looking for Claude Code…", "Đang tìm Claude Code…")));
                    break;
            }

            // Output của trình cài: hiện khi đang cài hoặc khi cài lỗi để biết vì sao.
            if (installing || ClaudeInstaller.State == ClaudeInstaller.Status.Failed)
            {
                var tail = ClaudeInstaller.Tail(LogLines);
                if (tail.Count > 0) card.Add(GnurtUI.Text(string.Join("\n", tail), "lm-log"));
            }

            var input = new TextField { multiline = true };
            input.AddToClassList("lm-chat__input");
            input.SetEnabled(false);
            card.Add(input);

            var send = GnurtUI.Button(Loc.T("Send", "Gửi"), null, primary: true);
            send.SetEnabled(false);
            var row = GnurtUI.Row(send);
            if (ClaudeCli.State == ClaudeCli.Status.Missing)
            {
                var install = GnurtUI.Button(installing ? Loc.T("Installing…", "Đang cài…") : Loc.T("Install automatically", "Cài tự động"), ClaudeInstaller.Install);
                install.SetEnabled(!installing);
                row.Add(install);
            }
            var check = GnurtUI.Button(Loc.T("Check again", "Kiểm tra lại"), ClaudeCli.Check);
            check.SetEnabled(ClaudeCli.State != ClaudeCli.Status.Checking && !installing);
            row.Add(check);
            card.Add(row);
        }

        static void AddMissing(VisualElement card, bool installing)
        {
            var command = ClaudeInstaller.Command;
            if (installing)
            {
                card.Add(GnurtUI.Hint(Loc.T(
                    "Installing with Anthropic's official installer. This can take a few minutes…",
                    "Đang cài bằng trình cài chính thức của Anthropic, có thể mất vài phút…")));
                return;
            }

            if (ClaudeInstaller.State == ClaudeInstaller.Status.Failed)
                card.Add(GnurtUI.Note(ClaudeInstaller.Error, BadgeKind.Danger));
            else if (ClaudeInstaller.State == ClaudeInstaller.Status.Succeeded)
                card.Add(GnurtUI.Note(Loc.T(
                    "Installed, but the claude command was not found yet. Restart Unity, or pick its path in Settings.",
                    "Đã cài xong nhưng chưa thấy lệnh claude. Thử mở lại Unity, hoặc chọn đường dẫn ở trang Cài đặt."), BadgeKind.Warning));
            else
                card.Add(GnurtUI.Hint(Loc.T(
                    "Claude Code CLI not found, so chat is off. Press Install automatically, or run this yourself in " + command.Shell + ":",
                    "Chưa thấy Claude Code CLI nên vùng chat đang tắt. Bấm Cài tự động, hoặc tự chạy lệnh này trong " + command.Shell + ":")));

            var code = GnurtUI.Text(command.Display, "lm-code");
            code.selection.isSelectable = true;
            card.Add(code);
            card.Add(GnurtUI.Hint(Loc.T(
                "Needs a paid Claude plan or a Console account. After installing, run claude once in a terminal to sign in.",
                "Cần tài khoản Claude trả phí hoặc Console. Cài xong, chạy lệnh claude một lần trong terminal để đăng nhập.")));
        }

        public static Label StatusBadge()
        {
            if (ClaudeInstaller.State == ClaudeInstaller.Status.Running)
                return GnurtUI.Badge(Loc.T("Installing Claude Code…", "Đang cài Claude Code…"), BadgeKind.Accent);
            switch (ClaudeCli.State)
            {
                case ClaudeCli.Status.Ready:
                    return GnurtUI.Badge("Claude Code " + ClaudeCli.Version, BadgeKind.Success);
                case ClaudeCli.Status.Missing:
                    return GnurtUI.Badge(Loc.T("No Claude Code", "Chưa có Claude Code"), BadgeKind.Warning);
                default:
                    return GnurtUI.Badge(Loc.T("Checking…", "Đang kiểm tra…"));
            }
        }
    }

    /// <summary>Nút cài và dòng trạng thái cài cho trang Cài đặt, tự cập nhật theo trình cài.</summary>
    internal sealed class ClaudeInstallControls : VisualElement
    {
        public ClaudeInstallControls()
        {
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ClaudeCli.Changed += Rebuild;
                ClaudeInstaller.Changed += Rebuild;
                Rebuild();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                ClaudeCli.Changed -= Rebuild;
                ClaudeInstaller.Changed -= Rebuild;
            });
        }

        void Rebuild()
        {
            Clear();
            if (ClaudeCli.State == ClaudeCli.Status.Ready) return;

            bool installing = ClaudeInstaller.State == ClaudeInstaller.Status.Running;
            var command = ClaudeInstaller.Command;
            Add(GnurtUI.Text(Loc.T("Install", "Cài đặt"), "lm-detail__subhead"));
            Add(GnurtUI.Hint(Loc.T($"Official installer ({command.Shell}): ", $"Trình cài chính thức ({command.Shell}): ") + command.Display));
            if (ClaudeInstaller.State == ClaudeInstaller.Status.Failed) Add(GnurtUI.Error(ClaudeInstaller.Error));
            if (installing)
            {
                var last = ClaudeInstaller.Tail(1);
                Add(GnurtUI.Hint(last.Count > 0 ? last[0] : Loc.T("Starting…", "Đang bắt đầu…")));
            }

            var install = GnurtUI.Button(installing ? Loc.T("Installing…", "Đang cài…") : Loc.T("Install automatically", "Cài tự động"),
                ClaudeInstaller.Install, primary: true);
            install.SetEnabled(!installing);
            Add(GnurtUI.Row(install));
        }
    }

    /// <summary>Badge trạng thái Claude trên header, tự cập nhật mà không phải vẽ lại cả window.</summary>
    internal sealed class ClaudeBadge : VisualElement
    {
        public ClaudeBadge()
        {
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ClaudeCli.Changed += Rebuild;
                ClaudeInstaller.Changed += Rebuild;
                ClaudeCli.EnsureChecked();
                Rebuild();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                ClaudeCli.Changed -= Rebuild;
                ClaudeInstaller.Changed -= Rebuild;
            });
        }

        void Rebuild()
        {
            Clear();
            var badge = AskClaudePanel.StatusBadge();
            badge.style.marginLeft = 0;
            badge.tooltip = ClaudeCli.ExecutablePath ?? Loc.T("Claude Code CLI not found", "Chưa thấy Claude Code CLI");
            Add(badge);
        }
    }
}
