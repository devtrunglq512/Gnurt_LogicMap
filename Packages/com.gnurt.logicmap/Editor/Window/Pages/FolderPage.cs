using System;
using System.Collections.Generic;
using System.Linq;
using Gnurt.Editor;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>
    /// Trang một thư mục scene hoặc prefab: thư mục con, các file bên trong (thư mục đông thì chia nhóm theo tên),
    /// ô tìm kiếm, và panel bên phải cho biết file đang chọn dùng gì, được dùng ở đâu.
    /// </summary>
    internal sealed class FolderPage : LogicMapPage
    {
        const int GroupThreshold = 24;     // từ số file này trở lên thì chia nhóm theo tên
        const int ExpandAllThreshold = 60; // ít hơn số này thì mở sẵn mọi nhóm
        const int MaxSearchResults = 200;

        /// <summary>Chọn sẵn file này khi trang mở (vd bấm vào một prefab ở mục "Được dùng ở").</summary>
        internal static string PendingSelection;

        readonly SectionKind _kind;
        readonly FolderNode _node;
        AssetPanel _details;
        VisualElement _selectedTile;
        readonly Dictionary<string, VisualElement> _tiles = new Dictionary<string, VisualElement>();

        public FolderPage(SectionKind kind, FolderNode node)
        {
            _kind = kind;
            _node = node;
        }

        public static string IdFor(SectionKind kind, string folderPath) => $"folder:{kind.ToString().ToLowerInvariant()}:{folderPath}";

        /// <summary>Trang thư mục chứa file này (file luôn nằm trong thư mục cuối của đường dẫn).</summary>
        public static string IdOfFile(SectionKind kind, string assetPath)
        {
            int slash = assetPath.LastIndexOf('/');
            return IdFor(kind, slash < 0 ? "" : assetPath.Substring(0, slash));
        }

        public override string Id => IdFor(_kind, _node.Path);
        public override string Title => _node.Label;

        public override string Subtitle
        {
            get
            {
                string files = _kind == SectionKind.Scene
                    ? Loc.T($"{_node.Total} scenes", $"{_node.Total} scene")
                    : Loc.T($"{_node.Total} prefabs", $"{_node.Total} prefab");
                string folders = _node.Children.Count > 0 ? Loc.T($", {_node.Children.Count} subfolders", $", {_node.Children.Count} thư mục con") : "";
                return $"{_node.Path} · {files}{folders}";
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

            _tiles.Clear();
            _selectedTile = null;
            _details = new AssetPanel(Window);
            var body = GnurtUI.Element("lm-folder-body");
            var search = new TextField(Loc.T("Find", "Tìm"));
            search.AddToClassList("lm-search");
            search.RegisterValueChangedCallback(e => Fill(body, analysis, e.newValue));

            var main = GnurtUI.Element("lm-split__main", search, body);
            root.Add(GnurtUI.Element("lm-split", main, GnurtUI.Element("lm-side", _details)));
            _expandFor = PendingSelection;
            Fill(body, analysis, "");

            if (PendingSelection != null)
            {
                Select(PendingSelection);
                PendingSelection = null;
            }
            _expandFor = null;
        }

        string _expandFor; // file cần thấy ngay: nhóm chứa nó được mở sẵn

        void Fill(VisualElement body, ProjectAnalysis analysis, string query)
        {
            body.Clear();
            _tiles.Clear();
            _selectedTile = null;

            if (!string.IsNullOrWhiteSpace(query))
            {
                var matches = _node.AllItems()
                    .Where(p => FolderTree.FileName(p).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                body.Add(GnurtUI.Hint(matches.Count > MaxSearchResults
                    ? Loc.T($"{matches.Count} found, showing the first {MaxSearchResults}.", $"Tìm thấy {matches.Count}, hiện {MaxSearchResults} cái đầu.")
                    : Loc.T($"{matches.Count} found.", $"Tìm thấy {matches.Count}.")));
                body.Add(Grid(matches.Take(MaxSearchResults), analysis, showFolder: true));
                return;
            }

            if (_node.Children.Count > 0)
            {
                body.Add(GnurtUI.SectionTitle(Loc.T($"Folders ({_node.Children.Count})", $"Thư mục ({_node.Children.Count})")));
                var folders = GnurtUI.Element("lm-chips");
                foreach (var child in _node.Children) folders.Add(LogicMapUI.FolderChip(child, _kind, Window));
                body.Add(folders);
            }

            // Scene xếp theo thứ tự build cho khớp sidebar; prefab theo tên.
            var items = _kind == SectionKind.Scene
                ? _node.Items.OrderBy(p => analysis.Find(p)?.inBuild == true ? 0 : 1)
                    .ThenBy(p => analysis.Find(p)?.buildIndex ?? 0)
                    .ThenBy(FolderTree.FileName, NameRules.Natural).ToList()
                : _node.Items;
            if (items.Count == 0)
            {
                if (_node.Children.Count == 0) body.Add(GnurtUI.Hint(Loc.T("Empty.", "Trống.")));
                return;
            }

            body.Add(GnurtUI.SectionTitle(Loc.T($"In this folder ({items.Count})", $"Trong thư mục này ({items.Count})")));
            if (items.Count < GroupThreshold)
            {
                body.Add(Grid(items, analysis, showFolder: false));
                return;
            }

            // Thư mục đông: chia nhóm theo tên để không phải lướt qua hàng trăm ô.
            var byName = items.ToDictionary(FolderTree.FileName, p => p);
            var groups = NameGroups.Split(byName.Keys.ToList());
            body.Add(GnurtUI.Hint(Loc.T(
                $"Many files, grouped by name into {groups.Count} groups. Use Find to look one up.",
                $"Nhiều file nên được chia thành {groups.Count} nhóm theo tên. Dùng ô Tìm để tìm nhanh.")));
            foreach (var group in groups)
            {
                var paths = group.Names.Select(n => byName[n]).ToList();
                var foldout = new Foldout
                {
                    text = $"{group.Key ?? Loc.T("Other", "Khác")} ({group.Names.Count})",
                    value = items.Count < ExpandAllThreshold || (_expandFor != null && paths.Contains(_expandFor)),
                };
                foldout.AddToClassList("gnurt-foldout");
                foldout.AddToClassList("lm-group");
                // Nhóm đang gập thì chưa dựng ô, mở ra mới dựng: thư mục vài trăm prefab vẫn mở nhanh.
                if (foldout.value) foldout.Add(Grid(paths, analysis, showFolder: false));
                else foldout.RegisterValueChangedCallback(e =>
                {
                    if (e.target == foldout && e.newValue && foldout.contentContainer.childCount == 0)
                        foldout.Add(Grid(paths, analysis, showFolder: false));
                });
                body.Add(foldout);
            }
        }

        VisualElement Grid(IEnumerable<string> paths, ProjectAnalysis analysis, bool showFolder)
        {
            var grid = GnurtUI.Element("lm-asset-grid");
            foreach (string path in paths)
            {
                var tile = LogicMapUI.AssetTile(path, analysis, showFolder);
                tile.RegisterCallback<ClickEvent>(e =>
                {
                    Select(path);
                    if (e.clickCount == 2) LogicMapUI.OpenAsset(path, analysis, Window);
                });
                _tiles[path] = tile;
                grid.Add(tile);
            }
            return grid;
        }

        void Select(string path)
        {
            _selectedTile?.RemoveFromClassList("lm-asset-tile--selected");
            _tiles.TryGetValue(path, out _selectedTile);
            _selectedTile?.AddToClassList("lm-asset-tile--selected");
            _details.Show(path);
        }
    }
}
