using System.Linq;
using Gnurt.Editor;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>Trang đầu: project chia theo cách nào, có những map/minigame và màn chung nào.</summary>
    internal sealed class OverviewPage : LogicMapPage
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

            var scenes = analysis.Scan.sections.Where(s => s.kind == SectionKind.Scene).ToList();
            var buildScenes = ProjectAnalysis.Ordered(scenes.Where(s => s.inBuild)).ToList();
            var sectionPrefabs = ProjectAnalysis.Ordered(analysis.Scan.sections.Where(s => s.kind == SectionKind.Prefab)).ToList();
            var prefabPaths = LogicMapUI.AllPrefabPaths(analysis);
            int loads = analysis.Scan.scripts.Sum(s => s.loads.Count);
            int unknown = analysis.Scan.scripts.SelectMany(s => s.loads).Count(c => c.targetKind == TargetKind.Unknown);

            root.Add(GnurtUI.Stats(
                GnurtUI.Stat(Loc.T("SPLIT", "CÁCH CHIA"), LogicMapUI.StructureName(analysis.Structure),
                    analysis.Entry != null ? Loc.T($"starts at {analysis.Entry.name}", $"mở từ {analysis.Entry.name}") : null),
                GnurtUI.Stat("SCENE", buildScenes.Count.ToString(),
                    Loc.T($"in build, {scenes.Count - buildScenes.Count} more not in build", $"trong build, thêm {scenes.Count - buildScenes.Count} ngoài build")),
                GnurtUI.Stat("PREFAB", prefabPaths.Count.ToString(),
                    Loc.T($"{sectionPrefabs.Count} are maps / minigames", $"{sectionPrefabs.Count} là map / minigame")),
                GnurtUI.Stat(Loc.T("SCENE LOADS", "LỆNH CHUYỂN SCENE"), loads.ToString(),
                    unknown > 0 ? Loc.T($"{unknown} decided at runtime", $"{unknown} tính lúc chạy") : Loc.T("all targets known", "đều rõ đích"))));

            // Khối Scene: scene trong build theo thứ tự, scene khác xem theo thư mục.
            root.Add(GnurtUI.SectionTitle($"Scene ({scenes.Count})"));
            root.Add(GnurtUI.Hint(Loc.T(
                "Scenes in Build Settings, in order. Other scenes are listed by folder.",
                "Scene trong Build Settings, theo đúng thứ tự. Scene khác xem theo thư mục.")));
            AddFolders(root, FolderTree.Build(scenes.Select(s => s.path)), SectionKind.Scene);
            AddTiles(root, buildScenes, analysis, Loc.T("No scene in Build Settings yet.", "Build Settings chưa có scene nào."));

            // Khối Prefab: chỉ hiện prefab là map/minigame; prefab thường (item, UI...) xem theo thư mục.
            root.Add(GnurtUI.SectionTitle($"Prefab ({prefabPaths.Count})"));
            root.Add(GnurtUI.Hint(Loc.T(
                "Only maps / minigames are shown here (folders named Maps, Levels, MiniGames… or set in Settings). Other prefabs are listed by folder.",
                "Ở đây chỉ hiện prefab là map / minigame (thư mục tên kiểu Maps, Levels, MiniGames… hoặc chọn ở Cài đặt). Prefab khác xem theo thư mục.")));
            AddFolders(root, FolderTree.Build(prefabPaths), SectionKind.Prefab);
            AddTiles(root, sectionPrefabs, analysis, Loc.T("No prefab is a map or minigame.", "Chưa có prefab nào là map / minigame."));

            var guide = GnurtUI.Card(Loc.T("How to read", "Cách đọc"));
            guide.Add(GnurtUI.Bullet(Loc.T("Game flow shows how scenes and prefabs lead to each other.",
                "Trang Luồng game cho thấy scene và prefab dẫn tới nhau thế nào.")));
            guide.Add(GnurtUI.Bullet(Loc.T("The sidebar follows the project's folders. Big folders are split into groups by name.",
                "Sidebar theo đúng cây thư mục của project. Thư mục đông file được chia nhóm theo tên.")));
            guide.Add(GnurtUI.Bullet(Loc.T("LogicMap only reads the project. Press Rescan after changing scenes or scripts.",
                "LogicMap chỉ đọc project, không sửa gì. Đổi scene hay script xong thì bấm Quét lại.")));
            guide.Add(GnurtUI.Row(GnurtUI.Button(Loc.T("Open game flow", "Xem luồng game"), () => Window.SelectPage(GameFlowPage.PageId), primary: true)));
            root.Add(guide);
        }

        void AddFolders(VisualElement root, System.Collections.Generic.List<FolderNode> folders, SectionKind kind)
        {
            if (folders.Count == 0) return;
            var chips = GnurtUI.Element("lm-chips");
            foreach (var folder in folders) chips.Add(LogicMapUI.FolderChip(folder, kind, Window));
            root.Add(chips);
        }

        void AddTiles(VisualElement root, System.Collections.Generic.List<SectionInfo> sections, ProjectAnalysis analysis, string emptyText)
        {
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
