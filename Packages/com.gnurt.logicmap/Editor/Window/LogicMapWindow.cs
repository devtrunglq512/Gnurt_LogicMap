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
        readonly List<GnurtPage> _pages = new List<GnurtPage>();
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

        protected override IEnumerable<GnurtPage> CreatePages()
        {
            _pages.Clear();
            _pages.Add(new OverviewPage());
            _pages.Add(new GameFlowPage());

            var analysis = ProjectScanner.Analysis;
            if (analysis != null)
            {
                // Core: màn chung + script dùng chung. Gameplay: từng map/minigame.
                _pages.AddRange(analysis.Sections(SectionRole.Core).Select(s => new SectionPage(s, SectionRole.Core)));
                _pages.Add(new SharedScriptsPage());
                _pages.AddRange(analysis.Sections(SectionRole.Gameplay).Select(s => new SectionPage(s, SectionRole.Gameplay)));
            }
            _pages.Add(new SettingsPage());
            return _pages;
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

        // Bổ sung cho khung GnurtWindow: sidebar cuộn được (game có nhiều map), nhãn cho scene ngoài build.
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

            var items = sidebar.Query(className: "gnurt-nav-item").ToList();
            for (int i = 0; i < items.Count && i < _pages.Count; i++)
            {
                if (!(_pages[i] is SectionPage section) || section.InBuild) continue;
                if (items[i].Q(className: "lm-nav-badge") != null) continue;
                var badge = GnurtUI.Badge(Loc.T("off build", "ngoài build"));
                badge.AddToClassList("lm-nav-badge");
                items[i].Add(badge);
                items[i].AddToClassList("lm-nav-item--off");
            }
        }
    }
}
