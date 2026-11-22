using System.Collections.Generic;
using System.Linq;
using Gnurt.Editor;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>Panel bên phải trang thư mục: scene/prefab đang chọn dùng script nào, chứa gì, được dùng ở đâu.</summary>
    internal sealed class AssetPanel : VisualElement
    {
        const int MaxRows = 12;
        readonly GnurtWindow _window;

        public AssetPanel(GnurtWindow window)
        {
            _window = window;
            Show(null);
        }

        public void Show(string path)
        {
            Clear();
            var card = GnurtUI.Card(Loc.T("Details", "Chi tiết"));
            Add(card);
            var analysis = ProjectScanner.Analysis;
            if (path == null || analysis == null)
            {
                card.Add(GnurtUI.Hint(Loc.T(
                    "Click a scene or prefab to see which scripts it uses and where it is used. Double-click to open it.",
                    "Bấm vào một scene hoặc prefab để xem nó dùng script nào và được dùng ở đâu. Bấm đúp để mở.")));
                return;
            }

            var section = analysis.Find(path);
            analysis.Prefabs.TryGetValue(path, out var prefab);
            bool isScene = path.EndsWith(".unity");

            card.Add(GnurtUI.Text(FolderTree.FileName(path), "lm-detail__title"));
            card.Add(LogicMapUI.AssetBadges(path, analysis));
            card.Add(LogicMapUI.PathLine(path));

            var scripts = section?.scripts ?? prefab?.scripts ?? new List<string>();
            AddLinks(card, Loc.T($"Scripts ({scripts.Count})", $"Script ({scripts.Count})"), scripts, p => DetailsPanel.Open(p));

            if (!isScene)
            {
                var usedBy = analysis.UsedBy(path).ToList();
                AddLinks(card, Loc.T($"Used in ({usedBy.Count})", $"Được dùng ở ({usedBy.Count})"), usedBy,
                    p => LogicMapUI.ShowAsset(p, analysis, _window),
                    Loc.T("Not referenced by any scene or prefab (may be loaded by code, Resources or Addressables).",
                        "Không scene/prefab nào tham chiếu (có thể được nạp bằng code, Resources hoặc Addressables)."));
            }

            var uses = section?.uses ?? prefab?.uses ?? new List<string>();
            AddLinks(card, Loc.T($"Contains prefabs ({uses.Count})", $"Chứa prefab ({uses.Count})"), uses,
                p => LogicMapUI.ShowAsset(p, analysis, _window));

            var actions = GnurtUI.Row();
            if (section != null)
                actions.Add(GnurtUI.Button(Loc.T("Open page", "Mở trang"), () => _window.SelectPage(FlowGraphs.PageIdOf(section)), primary: true));
            actions.Add(GnurtUI.Button(Loc.T("Open", "Mở"), () => DetailsPanel.Open(path)));
            actions.Add(GnurtUI.Button(Loc.T("Show in Project", "Chỉ trong Project"), () => DetailsPanel.Ping(path)));
            card.Add(actions);
        }

        // Danh sách đường dẫn bấm được; dài quá thì cắt bớt.
        static void AddLinks(VisualElement card, string title, List<string> paths, System.Action<string> onClick, string emptyText = null)
        {
            card.Add(GnurtUI.Text(title, "lm-detail__subhead"));
            if (paths.Count == 0)
            {
                if (emptyText != null) card.Add(GnurtUI.Hint(emptyText));
                return;
            }
            foreach (string path in paths.Take(MaxRows))
            {
                var link = GnurtUI.Text(FolderTree.FileName(path), "lm-link");
                link.tooltip = path;
                string target = path;
                link.RegisterCallback<ClickEvent>(_ => onClick(target));
                card.Add(link);
            }
            if (paths.Count > MaxRows) card.Add(GnurtUI.Hint(Loc.T($"+{paths.Count - MaxRows} more", $"+{paths.Count - MaxRows} nữa")));
        }
    }
}
