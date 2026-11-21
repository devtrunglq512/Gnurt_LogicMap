using System.Collections.Generic;
using System.Linq;
using Gnurt.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>Các khối giao diện dùng chung giữa các trang LogicMap.</summary>
    internal static class LogicMapUI
    {
        public const string PackageRoot = "Packages/com.gnurt.logicmap/";

        public static StyleSheet Style => AssetDatabase.LoadAssetAtPath<StyleSheet>(PackageRoot + "Editor/Window/LogicMap.uss");

        public static readonly Color ScriptColor = new Color(0.25f, 0.71f, 0.66f);

        public static string StructureName(ProjectStructure structure)
        {
            switch (structure)
            {
                case ProjectStructure.Scenes: return Loc.T("By scene", "Theo scene");
                case ProjectStructure.Prefabs: return Loc.T("By prefab", "Theo prefab");
                case ProjectStructure.Mixed: return Loc.T("Scene + prefab", "Scene + prefab");
                case ProjectStructure.SingleScene: return Loc.T("One scene", "Một scene");
                default: return "—";
            }
        }

        /// <summary>Câu nói rõ project chia nội dung game theo cách nào.</summary>
        public static VisualElement StructureNote(ProjectAnalysis analysis)
        {
            var gameplay = analysis.Scan.sections.Where(s => analysis.RoleOf(s) == SectionRole.Gameplay).ToList();
            int scenes = gameplay.Count(s => s.kind == SectionKind.Scene && s.inBuild);
            int prefabs = gameplay.Count(s => s.kind == SectionKind.Prefab);
            string folders = string.Join(", ", analysis.Scan.prefabFolders);
            string entry = analysis.Entry != null ? analysis.Entry.name : "?";
            string containers = string.Join(", ", analysis.Scan.sections
                .Where(s => s.kind == SectionKind.Scene && s.prefabs.Count > 0).Select(s => s.name).Distinct().Take(3));

            switch (analysis.Structure)
            {
                case ProjectStructure.Scenes:
                    return GnurtUI.Note(Loc.T(
                        $"Split by scene: each map is its own scene ({scenes} gameplay scenes in the build). The game starts at {entry}.",
                        $"Project chia theo scene: mỗi map là một scene riêng ({scenes} scene gameplay trong build). Game mở từ {entry}."), BadgeKind.Accent);
                case ProjectStructure.Prefabs:
                    return GnurtUI.Note(Loc.T(
                        $"Split by prefab: each map/minigame is a prefab ({prefabs} in {folders})" + (containers.Length > 0 ? $", loaded inside {containers}." : "."),
                        $"Project chia theo prefab: mỗi map/minigame là một prefab ({prefabs} prefab trong {folders})" + (containers.Length > 0 ? $", nạp vào scene {containers}." : ".")), BadgeKind.Accent);
                case ProjectStructure.Mixed:
                    return GnurtUI.Note(Loc.T(
                        $"Mixed: {scenes} gameplay scenes and {prefabs} prefab sections ({folders}).",
                        $"Project kết hợp: {scenes} scene gameplay và {prefabs} prefab-mục ({folders})."), BadgeKind.Accent);
                case ProjectStructure.SingleScene:
                    return GnurtUI.Note(Loc.T(
                        "Almost everything is in one gameplay scene. If each map or minigame is a prefab, add their folder in Settings.",
                        "Game gần như chỉ có một scene gameplay. Nếu mỗi map/minigame là một prefab, thêm thư mục chứa chúng ở trang Cài đặt."), BadgeKind.Neutral);
                default:
                    return GnurtUI.Note(Loc.T(
                        "No scene in Build Settings yet, so there is nothing to map.",
                        "Build Settings chưa có scene nào nên chưa có gì để vẽ."), BadgeKind.Warning);
            }
        }

        /// <summary>Thẻ của một mục trên trang Tổng quan. Bấm để mở trang của mục.</summary>
        public static VisualElement Tile(SectionInfo section, ProjectAnalysis analysis, GnurtWindow window)
        {
            var tile = GnurtUI.Element("lm-tile");
            tile.AddToClassList(section.kind == SectionKind.Scene ? "lm-tile--scene" : "lm-tile--prefab");
            tile.EnableInClassList("lm-tile--off", section.kind == SectionKind.Scene && !section.inBuild);
            tile.tooltip = section.path;

            tile.Add(GnurtUI.Text(section.name, "lm-tile__title"));
            tile.Add(GnurtUI.Text(FlowSummary(section, analysis), "lm-tile__summary"));

            var exits = analysis.ExitsOf(section).Count();
            var badges = GnurtUI.Element("lm-tile__badges",
                GnurtUI.Badge(FlowGraphs.Describe(section, Loc.T), section.kind == SectionKind.Scene && !section.inBuild ? BadgeKind.Warning : BadgeKind.Neutral),
                GnurtUI.Badge($"{section.scripts.Count} script"));
            if (exits > 0) badges.Add(GnurtUI.Badge(Loc.T($"{exits} scene loads", $"{exits} lệnh chuyển"), BadgeKind.Accent));
            tile.Add(badges);

            string pageId = FlowGraphs.PageIdOf(section);
            tile.RegisterCallback<ClickEvent>(_ => window.SelectPage(pageId));
            return tile;
        }

        /// <summary>"Từ Menu · tới Map 2, Main" — tóm tắt mục này nối với đâu.</summary>
        public static string FlowSummary(SectionInfo section, ProjectAnalysis analysis)
        {
            var from = analysis.EntriesOf(section).Select(l => l.Source?.name ?? l.Script.name)
                .Concat(analysis.ContainersOf(section).Select(s => s.name)).Distinct().ToList();
            var to = analysis.ExitsOf(section).SelectMany(l => l.Targets.Count > 0 ? l.Targets.Select(t => t.name) : new[] { "?" })
                .Concat(section.prefabs.Select(p => analysis.Find(p)?.name).Where(n => n != null)).Distinct().ToList();
            if (section == analysis.Entry) from.Insert(0, Loc.T("game start", "mở game"));

            var parts = new List<string>();
            if (from.Count > 0) parts.Add(Loc.T("From ", "Từ ") + Join(from));
            if (to.Count > 0) parts.Add(Loc.T("to ", "tới ") + Join(to));
            return parts.Count > 0
                ? string.Join(" · ", parts)
                : Loc.T("No scene switch found in its scripts.", "Không thấy lệnh chuyển scene trong script của mục này.");
        }

        static string Join(List<string> names) =>
            names.Count <= 3 ? string.Join(", ", names) : string.Join(", ", names.Take(3)) + $" +{names.Count - 3}";

        /// <summary>Một dòng script: tên, đường dẫn, các nhãn. Bấm để mở script.</summary>
        public static VisualElement ScriptRow(ScriptInfo script, ProjectAnalysis analysis)
        {
            int users = analysis.UsersOf(script.path).Count;
            var row = GnurtUI.ListRow(GnurtUI.Avatar("C#", ScriptColor), script.name, script.path,
                script.dontDestroyOnLoad ? GnurtUI.Badge("DontDestroyOnLoad", BadgeKind.Warning) : null,
                users >= 2 ? GnurtUI.Badge(Loc.T($"{users} sections", $"{users} mục")) : null,
                script.loads.Count > 0 ? GnurtUI.Badge(Loc.T($"{script.loads.Count} scene loads", $"{script.loads.Count} lệnh chuyển"), BadgeKind.Accent) : null);
            row.AddToClassList("lm-clickable");
            row.tooltip = Loc.T("Open script", "Mở script");
            row.RegisterCallback<ClickEvent>(_ => DetailsPanel.Open(script.path, script.loads.Count > 0 ? script.loads[0].line : 0));
            return row;
        }

        /// <summary>Chưa quét lần nào: lời nhắn kèm nút quét.</summary>
        public static void AddNoScan(VisualElement root)
        {
            root.Add(GnurtUI.Note(Loc.T(
                "The project has not been scanned yet. Scanning reads Build Settings, scenes, prefabs and scripts; it changes nothing.",
                "Project chưa được quét. Quét chỉ đọc Build Settings, scene, prefab và script, không sửa gì."), BadgeKind.Accent));
            root.Add(GnurtUI.Row(GnurtUI.Button(Loc.T("Scan now", "Quét ngay"), ProjectScanner.Rescan, primary: true)));
        }
    }
}
