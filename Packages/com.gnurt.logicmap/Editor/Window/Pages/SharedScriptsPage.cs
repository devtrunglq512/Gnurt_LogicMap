using System.Collections.Generic;
using System.Linq;
using Gnurt.Editor;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>Script chạy ở nhiều nơi: manager sống xuyên scene, script dùng ở nhiều mục, class không gắn vào scene.</summary>
    internal sealed class SharedScriptsPage : LogicMapPage
    {
        public const string PageId = "shared";

        public override string Id => PageId;
        public override string Title => Loc.T("Shared scripts", "Script dùng chung");
        public override string Subtitle => Loc.T(
            "Scripts that run in more than one place. Changing them affects many maps at once.",
            "Script chạy ở nhiều nơi. Sửa chúng là ảnh hưởng nhiều map cùng lúc.");
        public override string Group => "Core";
        public override GnurtIconKind Icon => GnurtIconKind.Log;

        public override void Build(VisualElement root)
        {
            var analysis = ProjectScanner.Analysis;
            if (analysis == null)
            {
                LogicMapUI.AddNoScan(root);
                return;
            }

            var scripts = analysis.Scan.scripts;
            AddGroup(root, analysis,
                Loc.T("Live across scenes", "Sống xuyên scene"),
                Loc.T("Call DontDestroyOnLoad: created once, kept while scenes change. Usually managers.",
                    "Có gọi DontDestroyOnLoad: tạo một lần, giữ lại khi đổi scene. Thường là các manager."),
                scripts.Where(s => s.dontDestroyOnLoad));
            AddGroup(root, analysis,
                Loc.T("Used by several sections", "Dùng ở nhiều mục"),
                Loc.T("Attached in two or more scenes or prefabs.", "Được gắn trong từ hai scene/prefab trở lên."),
                scripts.Where(s => !s.dontDestroyOnLoad && analysis.UsersOf(s.path).Count >= 2)
                    .OrderByDescending(s => analysis.UsersOf(s.path).Count));
            AddGroup(root, analysis,
                Loc.T("Not attached, but switch scenes", "Không gắn vào đâu nhưng có chuyển scene"),
                Loc.T("Plain classes or static helpers called from other scripts.", "Class thường hoặc hàm tĩnh được script khác gọi."),
                scripts.Where(s => !s.dontDestroyOnLoad && analysis.UsersOf(s.path).Count == 0 && s.loads.Count > 0));
        }

        static void AddGroup(VisualElement root, ProjectAnalysis analysis, string title, string subtitle, IEnumerable<ScriptInfo> scripts)
        {
            var list = scripts.ToList();
            var card = GnurtUI.Card($"{title} ({list.Count})", subtitle);
            if (list.Count == 0) card.Add(GnurtUI.Hint(Loc.T("None.", "Không có.")));
            foreach (var script in list) card.Add(LogicMapUI.ScriptRow(script, analysis));
            root.Add(card);
        }
    }
}
