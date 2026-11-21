using Gnurt.Editor;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>Flow chart của cả game: mở game → các scene/prefab → nơi chúng dẫn tới.</summary>
    internal sealed class GameFlowPage : ChartPage
    {
        public const string PageId = "flow";

        public override string Id => PageId;
        public override string Title => Loc.T("Game flow", "Luồng game");
        public override string Subtitle => Loc.T(
            "Which scene or prefab leads where. Maps with the same flow are grouped into one box.",
            "Scene, prefab nào dẫn tới đâu. Các map cùng luồng được gộp thành một ô.");
        public override string Group => Loc.T("Overview", "Tổng quan");
        public override GnurtIconKind Icon => GnurtIconKind.Tag;

        public override void Build(VisualElement root)
        {
            var analysis = ProjectScanner.Analysis;
            if (analysis == null)
            {
                LogicMapUI.AddNoScan(root);
                return;
            }
            AddChart(root, FlowGraphs.Overview(analysis, Loc.T),
                Loc.T("Nothing to draw: no scene in Build Settings.", "Chưa có gì để vẽ: Build Settings chưa có scene nào."));
        }
    }
}
