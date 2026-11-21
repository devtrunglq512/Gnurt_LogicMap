using System.Linq;
using Gnurt.Editor;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>Trang đầu: project chia theo cách nào, có những map/minigame và màn chung nào.</summary>
    internal sealed class OverviewPage : GnurtPage
    {
        public const string PageId = "overview";

        public override string Id => PageId;
        public override string Title => Loc.T("Overview", "Tổng quan");
        public override string Group => Loc.T("Overview", "Tổng quan");
        public override GnurtIconKind Icon => GnurtIconKind.Home;

        public override string Subtitle
        {
            get
            {
                var scan = ProjectScanner.Current;
                return scan == null ? null : Loc.T($"{scan.projectName} · scanned {scan.scannedAt}", $"{scan.projectName} · quét lúc {scan.scannedAt}");
            }
        }

        public override void Build(VisualElement root)
        {
            var analysis = ProjectScanner.Analysis;
            if (analysis == null)
            {
                LogicMapUI.AddNoScan(root);
                return;
            }

            root.Add(LogicMapUI.StructureNote(analysis));

            var gameplay = analysis.Sections(SectionRole.Gameplay).ToList();
            var core = analysis.Sections(SectionRole.Core).ToList();
            int offBuild = gameplay.Count(s => s.kind == SectionKind.Scene && !s.inBuild);
            int loads = analysis.Scan.scripts.Sum(s => s.loads.Count);
            int unknown = analysis.Scan.scripts.SelectMany(s => s.loads).Count(c => c.targetKind == TargetKind.Unknown);

            root.Add(GnurtUI.Stats(
                GnurtUI.Stat(Loc.T("SPLIT", "CÁCH CHIA"), LogicMapUI.StructureName(analysis.Structure),
                    analysis.Entry != null ? Loc.T($"starts at {analysis.Entry.name}", $"mở từ {analysis.Entry.name}") : null),
                GnurtUI.Stat("GAMEPLAY", (gameplay.Count - offBuild).ToString(),
                    offBuild > 0 ? Loc.T($"+{offBuild} not in build", $"+{offBuild} ngoài build") : Loc.T("maps / minigames", "map / minigame")),
                GnurtUI.Stat("CORE", core.Count.ToString(), Loc.T("loading, menu, lobby…", "loading, menu, sảnh…")),
                GnurtUI.Stat(Loc.T("SCENE LOADS", "LỆNH CHUYỂN SCENE"), loads.ToString(),
                    unknown > 0 ? Loc.T($"{unknown} decided at runtime", $"{unknown} tính lúc chạy") : Loc.T("all targets known", "đều rõ đích"))));

            AddTiles(root, "Gameplay", gameplay, analysis,
                Loc.T("No map or minigame found. If they are prefabs, add their folder in Settings.",
                    "Chưa thấy map/minigame nào. Nếu chúng là prefab, thêm thư mục chứa chúng ở trang Cài đặt."));
            AddTiles(root, "Core", core, analysis, null);

            var guide = GnurtUI.Card(Loc.T("How to read", "Cách đọc"));
            guide.Add(GnurtUI.Bullet(Loc.T("Game flow shows how scenes and prefabs lead to each other.",
                "Trang Luồng game cho thấy scene và prefab dẫn tới nhau thế nào.")));
            guide.Add(GnurtUI.Bullet(Loc.T("Each map or minigame has its own page: where it comes from, which script sends the player where.",
                "Mỗi map/minigame có trang riêng: từ đâu tới, script nào đưa người chơi đi đâu.")));
            guide.Add(GnurtUI.Bullet(Loc.T("LogicMap only reads the project. Press Rescan after changing scenes or scripts.",
                "LogicMap chỉ đọc project, không sửa gì. Đổi scene hay script xong thì bấm Quét lại.")));
            guide.Add(GnurtUI.Row(GnurtUI.Button(Loc.T("Open game flow", "Xem luồng game"), () => Window.SelectPage(GameFlowPage.PageId), primary: true)));
            root.Add(guide);
        }

        void AddTiles(VisualElement root, string title, System.Collections.Generic.List<SectionInfo> sections, ProjectAnalysis analysis, string emptyText)
        {
            if (sections.Count == 0 && emptyText == null) return;
            root.Add(GnurtUI.SectionTitle($"{title} ({sections.Count})"));
            if (sections.Count == 0)
            {
                root.Add(GnurtUI.Hint(emptyText));
                return;
            }
            var tiles = GnurtUI.Element("lm-tiles");
            foreach (var section in sections) tiles.Add(LogicMapUI.Tile(section, analysis, Window));
            root.Add(tiles);
        }
    }
}
