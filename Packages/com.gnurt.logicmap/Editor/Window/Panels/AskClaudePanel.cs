using Gnurt.Editor;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>
    /// Vùng hỏi Claude. Bản 0.1.0 chỉ báo máy có Claude Code CLI hay không;
    /// hỏi đáp thật làm ở bản sau, nên ô nhập luôn bị khoá.
    /// </summary>
    internal sealed class AskClaudePanel : VisualElement
    {
        public const string InstallCommand = "npm install -g @anthropic-ai/claude-code";

        public AskClaudePanel()
        {
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ClaudeCli.Changed += Rebuild;
                ClaudeCli.EnsureChecked();
                Rebuild();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => ClaudeCli.Changed -= Rebuild);
        }

        void Rebuild()
        {
            Clear();
            var card = GnurtUI.Card(Loc.T("Ask Claude", "Hỏi Claude"));
            Add(card);
            card.Add(GnurtUI.Inline(StatusBadge()));

            switch (ClaudeCli.State)
            {
                case ClaudeCli.Status.Ready:
                    card.Add(GnurtUI.Hint(Loc.T(
                        "Claude Code is installed. Asking questions about this chart comes in LogicMap 0.2.0.",
                        "Máy đã có Claude Code. Hỏi đáp về chart ngay trong window sẽ có ở LogicMap 0.2.0.")));
                    break;
                case ClaudeCli.Status.Missing:
                    card.Add(GnurtUI.Hint(Loc.T(
                        "Claude Code CLI not found, so chat is off. Install it, sign in once in a terminal, then press Check again:",
                        "Chưa thấy Claude Code CLI nên vùng chat đang tắt. Cài bằng lệnh dưới, đăng nhập một lần trong terminal, rồi bấm Kiểm tra lại:")));
                    var command = GnurtUI.Text(InstallCommand, "lm-code");
                    command.selection.isSelectable = true;
                    card.Add(command);
                    break;
                default:
                    card.Add(GnurtUI.Hint(Loc.T("Looking for Claude Code…", "Đang tìm Claude Code…")));
                    break;
            }

            var input = new TextField { multiline = true };
            input.AddToClassList("lm-chat__input");
            input.SetEnabled(false);
            card.Add(input);

            var send = GnurtUI.Button(Loc.T("Send", "Gửi"), null, primary: true);
            send.SetEnabled(false);
            var check = GnurtUI.Button(Loc.T("Check again", "Kiểm tra lại"), ClaudeCli.Check);
            check.SetEnabled(ClaudeCli.State != ClaudeCli.Status.Checking);
            card.Add(GnurtUI.Row(send, check));
        }

        public static Label StatusBadge()
        {
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

    /// <summary>Badge trạng thái Claude trên header, tự cập nhật mà không phải vẽ lại cả window.</summary>
    internal sealed class ClaudeBadge : VisualElement
    {
        public ClaudeBadge()
        {
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ClaudeCli.Changed += Rebuild;
                ClaudeCli.EnsureChecked();
                Rebuild();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => ClaudeCli.Changed -= Rebuild);
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
