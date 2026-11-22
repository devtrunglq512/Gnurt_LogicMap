using System;
using Gnurt.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>Panel bên phải: thông tin ô đang chọn và nút mở scene/prefab/script tương ứng.</summary>
    internal sealed class DetailsPanel : VisualElement
    {
        readonly Action<string> _openPage;

        public DetailsPanel(Action<string> openPage)
        {
            _openPage = openPage;
            Show(null);
        }

        public void Show(LogicNode node)
        {
            Clear();
            var card = GnurtUI.Card(Loc.T("Details", "Chi tiết"));
            Add(card);

            if (node == null)
            {
                card.Add(GnurtUI.Hint(Loc.T("Click a box to see what it is.", "Bấm vào một ô để xem đó là gì.")));
                card.Add(GnurtUI.Bullet(Loc.T("Purple stripe: scene. Gold stripe: prefab.", "Vạch tím: scene. Vạch vàng: prefab.")));
                card.Add(GnurtUI.Bullet(Loc.T("Teal: a script function that switches scene. Diamond: it can go to several places.",
                    "Xanh ngọc: hàm trong script chuyển scene. Hình thoi: rẽ được nhiều hướng.")));
                card.Add(GnurtUI.Bullet(Loc.T("Dashed line: contains, or target not known until runtime.",
                    "Nét đứt: quan hệ chứa, hoặc đích chỉ biết lúc chạy.")));
                return;
            }

            card.Add(GnurtUI.Inline(GnurtUI.Text(node.Title, "lm-detail__title"), GnurtUI.Badge(KindName(node), BadgeFor(node))));
            if (!string.IsNullOrEmpty(node.Subtitle)) card.Add(GnurtUI.Hint(node.Subtitle));

            if (!string.IsNullOrEmpty(node.AssetPath))
                card.Add(LogicMapUI.PathLine(node.Line > 0 ? $"{node.AssetPath}:{node.Line}" : node.AssetPath));

            if (!string.IsNullOrEmpty(node.Detail))
            {
                var code = GnurtUI.Text(node.Detail, "lm-code");
                code.selection.isSelectable = true;
                card.Add(code);
            }

            if (node.Items.Count > 0)
            {
                card.Add(GnurtUI.Text(Loc.T($"{node.Items.Count} items", $"{node.Items.Count} mục"), "lm-detail__subhead"));
                foreach (var item in node.Items)
                {
                    var row = GnurtUI.Text(item.Title, "lm-link");
                    row.tooltip = item.AssetPath;
                    var target = item;
                    row.RegisterCallback<ClickEvent>(_ =>
                    {
                        if (target.PageId != null) _openPage(target.PageId);
                        else Ping(target.AssetPath);
                    });
                    card.Add(row);
                }
            }

            var actions = GnurtUI.Row();
            if (node.PageId != null)
                actions.Add(GnurtUI.Button(Loc.T("Open page", "Mở trang"), () => _openPage(node.PageId), primary: true));
            if (!string.IsNullOrEmpty(node.AssetPath))
            {
                actions.Add(GnurtUI.Button(Loc.T("Open", "Mở"), () => Open(node.AssetPath, node.Line)));
                actions.Add(GnurtUI.Button(Loc.T("Show in Project", "Chỉ trong Project"), () => Ping(node.AssetPath)));
            }
            if (actions.childCount > 0) card.Add(actions);
        }

        static string KindName(LogicNode node)
        {
            switch (node.Kind)
            {
                case NodeKind.Start: return Loc.T("Start", "Bắt đầu");
                case NodeKind.End: return Loc.T("End", "Kết thúc");
                case NodeKind.Decision: return Loc.T("Branch", "Rẽ nhánh");
                case NodeKind.Group: return Loc.T("Group", "Nhóm");
                case NodeKind.Unknown: return Loc.T("Unknown", "Chưa rõ");
                case NodeKind.Section: return node.Tone == NodeTone.Prefab ? "Prefab" : "Scene";
                default: return node.Tone == NodeTone.Script ? "Script" : Loc.T("Step", "Bước");
            }
        }

        static BadgeKind BadgeFor(LogicNode node)
        {
            switch (node.Tone)
            {
                case NodeTone.Scene: return BadgeKind.Accent;
                case NodeTone.Prefab: return BadgeKind.Warning;
                case NodeTone.Script: return BadgeKind.Success;
                default: return BadgeKind.Neutral;
            }
        }

        /// <summary>Mở scene/prefab/script; script thì mở đúng dòng.</summary>
        public static void Open(string assetPath, int line = 0)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null) return;
            if (assetPath.EndsWith(".unity"))
            {
                // Mở scene sẽ đóng scene đang mở: hỏi lưu trước như Unity vẫn làm.
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(assetPath);
                return;
            }
            AssetDatabase.OpenAsset(asset, line > 0 ? line : -1);
        }

        public static void Ping(string assetPath)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null) return;
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }
    }
}
