using System;
using System.Collections.Generic;
using System.Linq;
using Gnurt.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>Một dòng trên sidebar: tiêu đề nhóm, thư mục (mở/đóng được) hoặc một trang.</summary>
    internal sealed class SidebarEntry
    {
        public string PageId;     // null = không mở trang nào
        public string Label;
        public string Meta;       // chữ nhỏ bên phải: số file, số thứ tự build...
        public GnurtIconKind? Icon;
        public bool Header;       // tiêu đề nhóm (TỔNG QUAN, SCENE, PREFAB...)
        public bool Muted;        // scene ngoài build
        public bool Expanded;     // trạng thái mặc định khi người dùng chưa mở/đóng lần nào
        public SidebarEntry Parent;
        public readonly List<SidebarEntry> Children = new List<SidebarEntry>();

        public string Key => PageId ?? "header:" + Label;

        public SidebarEntry Add(SidebarEntry child)
        {
            child.Parent = this;
            Children.Add(child);
            return child;
        }
    }

    /// <summary>
    /// Sidebar dạng cây thay cho danh sách phẳng của GnurtWindow: game nhiều scene/prefab vẫn gọn,
    /// thư mục nào mở hay đóng được nhớ trong phiên làm việc.
    /// </summary>
    internal sealed class SidebarTree : VisualElement
    {
        const string StateKey = "Gnurt.LogicMap.SidebarState";
        const int Indent = 14;

        readonly List<SidebarEntry> _roots;
        readonly Action<string> _open;
        readonly Dictionary<string, bool> _state;
        readonly Dictionary<string, SidebarEntry> _byPage = new Dictionary<string, SidebarEntry>();
        readonly Dictionary<string, VisualElement> _rows = new Dictionary<string, VisualElement>();
        string _selected;

        public SidebarTree(List<SidebarEntry> roots, Action<string> open)
        {
            AddToClassList("lm-tree");
            _roots = roots;
            _open = open;
            _state = LoadState();
            foreach (var entry in Walk(roots))
                if (entry.PageId != null) _byPage[entry.PageId] = entry;
            Render();
        }

        static IEnumerable<SidebarEntry> Walk(IEnumerable<SidebarEntry> entries)
        {
            foreach (var entry in entries)
            {
                yield return entry;
                foreach (var child in Walk(entry.Children)) yield return child;
            }
        }

        bool IsExpanded(SidebarEntry entry) =>
            entry.Children.Count > 0 && (_state.TryGetValue(entry.Key, out bool open) ? open : entry.Expanded);

        void SetExpanded(SidebarEntry entry, bool open)
        {
            _state[entry.Key] = open;
            SaveState();
            Render();
        }

        /// <summary>Tô mục đang mở; mở sẵn các thư mục cha để thấy được nó.</summary>
        public void Select(string pageId)
        {
            _selected = pageId;
            if (pageId != null && _byPage.TryGetValue(pageId, out var entry))
            {
                bool changed = false;
                for (var parent = entry.Parent; parent != null; parent = parent.Parent)
                {
                    if (parent.Header || IsExpanded(parent)) continue;
                    _state[parent.Key] = true;
                    changed = true;
                }
                if (changed)
                {
                    SaveState();
                    Render();
                    return;
                }
            }
            foreach (var pair in _rows) pair.Value.EnableInClassList("gnurt-nav-item--selected", pair.Key == pageId);
        }

        void Render()
        {
            Clear();
            _rows.Clear();
            foreach (var root in _roots) AddEntry(root, 0);
            foreach (var pair in _rows) pair.Value.EnableInClassList("gnurt-nav-item--selected", pair.Key == _selected);
        }

        void AddEntry(SidebarEntry entry, int depth)
        {
            if (entry.Header)
            {
                string text = entry.Label.ToUpperInvariant() + (entry.Meta != null ? "  ·  " + entry.Meta : "");
                Add(GnurtUI.Text(text, "gnurt-nav-group"));
                foreach (var child in entry.Children) AddEntry(child, 0);
                return;
            }

            bool hasChildren = entry.Children.Count > 0;
            bool open = IsExpanded(entry);
            var row = GnurtUI.Element("gnurt-nav-item");
            row.AddToClassList("lm-tree-row");
            row.EnableInClassList("lm-tree-row--leaf", !hasChildren && entry.Icon == null);
            row.EnableInClassList("lm-nav-item--off", entry.Muted);
            row.style.paddingLeft = 4 + depth * Indent;
            row.tooltip = entry.Label;

            var chevron = GnurtUI.Text(hasChildren ? (open ? "▾" : "▸") : "", "lm-tree-chevron");
            if (hasChildren)
            {
                chevron.RegisterCallback<ClickEvent>(e =>
                {
                    SetExpanded(entry, !open);
                    e.StopPropagation(); // bấm mũi tên chỉ mở/đóng, không mở trang
                });
            }
            row.Add(chevron);
            if (entry.Icon.HasValue) row.Add(new GnurtIcon(entry.Icon.Value));
            row.Add(GnurtUI.Text(entry.Label, "gnurt-nav-item__label"));
            if (entry.Meta != null) row.Add(GnurtUI.Text(entry.Meta, "lm-tree-meta"));

            row.RegisterCallback<ClickEvent>(_ =>
            {
                if (hasChildren && !open) SetExpanded(entry, true);
                if (entry.PageId != null) _open(entry.PageId);
                else if (hasChildren && open) SetExpanded(entry, false);
            });

            if (entry.PageId != null) _rows[entry.PageId] = row;
            Add(row);
            if (hasChildren && open)
                foreach (var child in entry.Children) AddEntry(child, depth + 1);
        }

        static Dictionary<string, bool> LoadState()
        {
            var state = new Dictionary<string, bool>();
            foreach (string line in SessionState.GetString(StateKey, "").Split('\n'))
            {
                int tab = line.LastIndexOf('\t');
                if (tab > 0) state[line.Substring(0, tab)] = line.Substring(tab + 1) == "1";
            }
            return state;
        }

        void SaveState() =>
            SessionState.SetString(StateKey, string.Join("\n", _state.Select(p => p.Key + "\t" + (p.Value ? "1" : "0"))));
    }
}
