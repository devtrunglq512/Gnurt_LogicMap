using System.Collections.Generic;
using System.Linq;
using Gnurt.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>Window chính của LogicMap. Mở bằng menu Gnurt > LogicMap.</summary>
    public sealed class LogicMapWindow : GnurtWindow
    {
        // Thư mục có nhiều scene hơn số này thì sidebar chỉ hiện thư mục, scene xem trong trang thư mục.
        const int MaxSceneLeaves = 20;

        readonly List<GnurtPage> _pages = new List<GnurtPage>();
        readonly List<SidebarEntry> _sidebar = new List<SidebarEntry>();
        SidebarTree _tree;
        string _currentPageId;
        bool _refreshQueued;

        [MenuItem("Gnurt/LogicMap", priority = 20)]
        public static void OpenWindow() => Open<LogicMapWindow>();

        protected override string HeaderTitle => "Gnurt LogicMap";
        protected override string Version => UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(LogicMapWindow).Assembly)?.version;

        protected override string HeaderSubtitle
        {
            get
            {
                var analysis = ProjectScanner.Analysis;
                if (analysis == null) return Loc.T("Game structure as flow charts", "Cấu trúc game dưới dạng flow chart");
                return $"{analysis.Scan.projectName} · {LogicMapUI.StructureName(analysis.Structure)}";
            }
        }

        // Sidebar: Tổng quan | SCENE và PREFAB chia theo đúng cây thư mục của project | Khác.
        protected override IEnumerable<GnurtPage> CreatePages()
        {
            _pages.Clear();
            _sidebar.Clear();

            var overview = Header(Loc.T("Overview", "Tổng quan"));
            AddPage(new OverviewPage(), overview, GnurtIconKind.Home);
            AddPage(new GameFlowPage(), overview, GnurtIconKind.Tag);

            var analysis = ProjectScanner.Analysis;
            if (analysis != null)
            {
                AddPage(new SharedScriptsPage(), overview, GnurtIconKind.Log);

                var scenes = analysis.Scan.sections.Where(s => s.kind == SectionKind.Scene).Select(s => s.path).ToList();
                var sceneHeader = Header("Scene", scenes.Count.ToString());
                foreach (var node in FolderTree.Build(scenes)) AddFolder(node, SectionKind.Scene, sceneHeader, analysis);

                var prefabs = LogicMapUI.AllPrefabPaths(analysis);
                var prefabHeader = Header("Prefab", prefabs.Count.ToString());
                foreach (var node in FolderTree.Build(prefabs)) AddFolder(node, SectionKind.Prefab, prefabHeader, analysis);
            }

            var other = Header(Loc.T("Other", "Khác"));
            AddPage(new SettingsPage(), other, GnurtIconKind.Settings);
            return _pages;
        }

        SidebarEntry Header(string label, string meta = null)
        {
            var header = new SidebarEntry { Header = true, Label = label, Meta = meta };
            _sidebar.Add(header);
            return header;
        }

        void AddPage(GnurtPage page, SidebarEntry parent, GnurtIconKind icon)
        {
            _pages.Add(page);
            parent.Add(new SidebarEntry { PageId = page.Id, Label = page.Title, Icon = icon });
        }

        // Mỗi thư mục là một trang. Bên dưới là thư mục con, rồi tới scene (nếu thư mục không quá đông)
        // và prefab là map/minigame. Prefab thường không hiện từng cái mà xem trong trang thư mục.
        void AddFolder(FolderNode node, SectionKind kind, SidebarEntry parent, ProjectAnalysis analysis)
        {
            var page = new FolderPage(kind, node);
            _pages.Add(page);
            var sections = node.AllItems().Select(analysis.Find).Where(s => s != null).ToList();
            var entry = parent.Add(new SidebarEntry
            {
                PageId = page.Id,
                Label = node.Label,
                Meta = node.Total.ToString(),
                // Mặc định mở thư mục có scene trong build hoặc có map/minigame; thư mục khác (plugin...) gập lại.
                Expanded = sections.Any(s => s.inBuild || s.kind == SectionKind.Prefab),
            });

            foreach (var child in node.Children) AddFolder(child, kind, entry, analysis);

            var leaves = ProjectAnalysis.Ordered(node.Items.Select(analysis.Find).Where(s => s != null)).ToList();
            bool listLeaves = kind == SectionKind.Prefab || node.Items.Count <= MaxSceneLeaves;
            foreach (var section in leaves)
            {
                var sectionPage = new SectionPage(section, analysis.RoleOf(section));
                _pages.Add(sectionPage);
                if (!listLeaves) continue;
                entry.Add(new SidebarEntry
                {
                    PageId = sectionPage.Id,
                    Label = section.name,
                    Meta = section.kind == SectionKind.Scene && section.inBuild ? "#" + section.buildIndex : null,
                    Muted = section.kind == SectionKind.Scene && !section.inBuild,
                });
            }
        }

        protected override void AddHeaderControls(VisualElement controls)
        {
            controls.Add(new ClaudeBadge());
            controls.Add(GnurtUI.Button(Loc.T("Rescan", "Quét lại"), ProjectScanner.Rescan));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Loc.Changed += Decorate; // đăng ký sau window gốc nên chạy sau khi window đã vẽ lại
            ProjectScanner.Changed += QueueRefresh;
        }

        protected override void OnDisable()
        {
            ProjectScanner.Changed -= QueueRefresh;
            Loc.Changed -= Decorate;
            base.OnDisable();
        }

        protected override void CreateGUI()
        {
            ProjectScanner.EnsureLoaded();
            base.CreateGUI();
            Decorate();
            // Lần đầu mở trong project: quét sau khi window hiện lên, để không phải nhìn màn hình trống.
            if (ProjectScanner.Current == null) EditorApplication.delayCall += ProjectScanner.Rescan;
        }

        /// <summary>Trang vừa được mở: tô mục tương ứng trên sidebar.</summary>
        internal void OnPageShown(string pageId)
        {
            _currentPageId = pageId;
            _tree?.Select(pageId);
        }

        void QueueRefresh()
        {
            if (_refreshQueued) return;
            _refreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                _refreshQueued = false;
                if (this == null) return;
                RefreshAll(); // danh sách trang đổi theo kết quả quét
                Decorate();
            };
        }

        // Thay sidebar phẳng của GnurtWindow bằng cây cuộn được (game có thể có hàng trăm scene/prefab).
        void Decorate()
        {
            var root = rootVisualElement;
            var style = LogicMapUI.Style;
            if (style != null && !root.styleSheets.Contains(style)) root.styleSheets.Add(style);

            var sidebar = root.Q(className: "gnurt-sidebar");
            if (sidebar == null) return;
            if (sidebar.GetFirstAncestorOfType<ScrollView>() == null)
            {
                var parent = sidebar.parent;
                int index = parent.IndexOf(sidebar);
                var scroll = new ScrollView(ScrollViewMode.Vertical);
                scroll.AddToClassList("lm-sidebar-scroll");
                parent.Insert(index, scroll);
                scroll.Add(sidebar);
            }

            sidebar.Clear();
            _tree = new SidebarTree(_sidebar, SelectPage);
            sidebar.Add(_tree);
            _tree.Select(_currentPageId);
        }
    }
}
