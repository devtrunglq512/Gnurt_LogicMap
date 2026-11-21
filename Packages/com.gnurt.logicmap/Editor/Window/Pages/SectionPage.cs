using System.Linq;
using Gnurt.Editor;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>Trang của một map/minigame/màn chung: từ đâu tới, script nào chuyển đi đâu, dùng script nào.</summary>
    internal sealed class SectionPage : ChartPage
    {
        readonly string _path;
        readonly string _name;
        readonly string _description;
        readonly SectionRole _role;

        public SectionPage(SectionInfo section, SectionRole role)
        {
            _path = section.path;
            _name = section.name;
            _description = FlowGraphs.Describe(section, Loc.T);
            _role = role;
            InBuild = section.kind == SectionKind.Prefab || section.inBuild;
        }

        /// <summary>Scene có trong build không (prefab luôn tính là có).</summary>
        public bool InBuild { get; }

        public override string Id => "section:" + _path;
        public override string Title => _name;
        public override string Subtitle => $"{_description} · {_path}";
        public override string Group => _role == SectionRole.Core ? "Core" : "Gameplay";
        public override GnurtIconKind Icon => GnurtIconKind.Dot;

        public override void Build(VisualElement root)
        {
            var analysis = ProjectScanner.Analysis;
            var section = analysis?.Find(_path);
            if (section == null)
            {
                root.Add(GnurtUI.Note(Loc.T("This section is no longer in the scan result. Press Rescan.",
                    "Mục này không còn trong kết quả quét. Bấm Quét lại."), BadgeKind.Warning));
                return;
            }

            if (section.kind == SectionKind.Scene && !section.inBuild)
                root.Add(GnurtUI.Note(Loc.T(
                    "This scene is not in Build Settings, so the game cannot load it by name or number.",
                    "Scene này chưa có trong Build Settings nên game không nạp được bằng tên hay số thứ tự."), BadgeKind.Warning));

            AddChart(root, FlowGraphs.Section(analysis, section, Loc.T), "");
            root.Add(ScriptsCard(section, analysis));
        }

        static VisualElement ScriptsCard(SectionInfo section, ProjectAnalysis analysis)
        {
            var card = GnurtUI.Card(
                Loc.T($"Scripts ({section.scripts.Count})", $"Script ({section.scripts.Count})"),
                Loc.T("Every script this scene or prefab uses, nested prefabs included. Click to open.",
                    "Mọi script mà scene/prefab này dùng, kể cả trong prefab lồng bên trong. Bấm để mở."));

            var scripts = section.scripts
                .Select(p => analysis.Scripts.TryGetValue(p, out var info) ? info : null)
                .Where(s => s != null)
                .ToList();
            if (scripts.Count == 0)
            {
                card.Add(GnurtUI.Hint(Loc.T("No script under Assets/.", "Không có script nào trong Assets/.")));
                return card;
            }

            // Script của game (Assembly-CSharp) hiện trước; script trong asmdef khác (thư viện...) gập lại.
            foreach (var group in scripts.GroupBy(s => s.assembly).OrderBy(g => g.Key.StartsWith("Assembly-CSharp") ? 0 : 1).ThenBy(g => g.Key))
            {
                var ordered = group.OrderByDescending(s => s.loads.Count).ThenBy(s => s.name, NameRules.Natural);
                if (group.Key.StartsWith("Assembly-CSharp"))
                {
                    foreach (var script in ordered) card.Add(LogicMapUI.ScriptRow(script, analysis));
                    continue;
                }
                var foldout = new Foldout { text = $"{group.Key} ({group.Count()})", value = false };
                foldout.AddToClassList("gnurt-foldout");
                foreach (var script in ordered) foldout.Add(LogicMapUI.ScriptRow(script, analysis));
                card.Add(foldout);
            }
            return card;
        }
    }
}
