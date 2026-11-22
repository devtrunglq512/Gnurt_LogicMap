using System;
using System.Collections.Generic;
using System.Linq;

namespace Gnurt.LogicMap
{
    /// <summary>Một thư mục trong cây: thư mục con và các file nằm ngay bên trong.</summary>
    internal sealed class FolderNode
    {
        public string Path;  // "Assets/Prefabs/Items"
        public string Label; // tên hiện ra; thư mục trung gian được gộp: "Prefabs/Items"
        public readonly List<FolderNode> Children = new List<FolderNode>();
        public readonly List<string> Items = new List<string>();
        public int Total; // số file trong cả cây con

        public IEnumerable<FolderNode> SelfAndDescendants()
        {
            yield return this;
            foreach (var child in Children)
                foreach (var node in child.SelfAndDescendants())
                    yield return node;
        }

        public IEnumerable<string> AllItems() => SelfAndDescendants().SelectMany(n => n.Items);
    }

    /// <summary>Xếp danh sách file theo đúng cây thư mục của project, để xem theo cách team đặt file.</summary>
    internal static class FolderTree
    {
        /// <summary>
        /// Trả về các thư mục cấp đầu bên trong Assets (đã bỏ chữ "Assets"). Thư mục chỉ có đúng một thư mục con
        /// và không có file thì gộp với con của nó, để cây không bị sâu vô ích ("_Game/Prefabs").
        /// </summary>
        public static List<FolderNode> Build(IEnumerable<string> paths)
        {
            var root = new FolderNode { Path = "", Label = "" };
            var byPath = new Dictionary<string, FolderNode> { [""] = root };

            foreach (string path in paths.Where(p => !string.IsNullOrEmpty(p)).Distinct())
            {
                string normalized = path.Replace('\\', '/');
                int slash = normalized.LastIndexOf('/');
                var folder = Ensure(byPath, slash < 0 ? "" : normalized.Substring(0, slash));
                folder.Items.Add(normalized);
            }

            Finish(root);
            var result = new List<FolderNode>();
            foreach (var top in root.Children)
            {
                if (top.Path != "Assets")
                {
                    result.Add(Collapse(top)); // scene nằm ngoài Assets (hiếm): giữ nguyên tên gốc
                    continue;
                }
                if (top.Items.Count > 0)
                {
                    var loose = new FolderNode { Path = "Assets", Label = "Assets", Total = top.Items.Count };
                    loose.Items.AddRange(top.Items);
                    result.Add(loose);
                }
                result.AddRange(top.Children.Select(Collapse));
            }
            return result;
        }

        static FolderNode Ensure(Dictionary<string, FolderNode> byPath, string path)
        {
            if (byPath.TryGetValue(path, out var node)) return node;
            int slash = path.LastIndexOf('/');
            var parent = Ensure(byPath, slash < 0 ? "" : path.Substring(0, slash));
            node = new FolderNode { Path = path, Label = slash < 0 ? path : path.Substring(slash + 1) };
            parent.Children.Add(node);
            byPath[path] = node;
            return node;
        }

        static int Finish(FolderNode node)
        {
            node.Children.Sort((a, b) => NameRules.NaturalCompare(a.Label, b.Label));
            node.Items.Sort((a, b) => NameRules.NaturalCompare(FileName(a), FileName(b)));
            node.Total = node.Items.Count + node.Children.Sum(Finish);
            return node.Total;
        }

        static FolderNode Collapse(FolderNode node)
        {
            while (node.Items.Count == 0 && node.Children.Count == 1)
            {
                var child = node.Children[0];
                child.Label = node.Label + "/" + child.Label;
                node = child;
            }
            for (int i = 0; i < node.Children.Count; i++) node.Children[i] = Collapse(node.Children[i]);
            return node;
        }

        /// <summary>Tên file không có đuôi.</summary>
        public static string FileName(string path)
        {
            int slash = path.LastIndexOf('/');
            string name = slash < 0 ? path : path.Substring(slash + 1);
            int dot = name.LastIndexOf('.');
            return dot > 0 ? name.Substring(0, dot) : name;
        }
    }

    /// <summary>
    /// Chia một thư mục quá đông thành các nhóm nhỏ theo tên: bỏ phần đầu chung ("Item_"), nhóm theo từ tiếp theo
    /// (Sword, Shield...). Tên không theo quy luật thì nhóm theo chữ cái đầu, cuối cùng là cắt theo khoảng.
    /// </summary>
    internal static class NameGroups
    {
        internal sealed class Group
        {
            public string Key; // null = nhóm "Khác"
            public readonly List<string> Names = new List<string>();
        }

        public static List<Group> Split(IList<string> names, int minSize = 2, int maxGroups = 40, int maxPerGroup = 60)
        {
            var groups = ByWord(names, minSize);
            if (groups.Count(g => g.Key != null) < 2 || groups.Count > maxGroups) groups = ByLetter(names);
            if (groups.Count == 1 && names.Count > maxPerGroup) groups = ByRange(names, maxPerGroup);
            return groups;
        }

        static List<Group> ByWord(IList<string> names, int minSize)
        {
            var tokens = names.Select(NameRules.Tokens).ToList();

            // Bỏ các từ mở đầu mà gần như mọi tên đều có (vd "Item", "Prefab").
            int skip = 0;
            while (true)
            {
                var top = tokens.Where(t => t.Count > skip)
                    .GroupBy(t => t[skip], StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();
                if (top == null || top.Count() < names.Count * 0.8) break;
                skip++;
            }

            var byKey = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
            var other = new Group();
            for (int i = 0; i < names.Count; i++)
            {
                if (tokens[i].Count <= skip)
                {
                    other.Names.Add(names[i]);
                    continue;
                }
                string key = tokens[i][skip];
                if (!byKey.TryGetValue(key, out var group)) byKey[key] = group = new Group { Key = key };
                group.Names.Add(names[i]);
            }

            var result = new List<Group>();
            foreach (var group in byKey.Values)
            {
                if (group.Names.Count >= minSize) result.Add(group);
                else other.Names.AddRange(group.Names);
            }
            result.Sort((a, b) => NameRules.NaturalCompare(a.Key, b.Key));
            if (other.Names.Count > 0) result.Add(other);
            return result;
        }

        static List<Group> ByLetter(IList<string> names)
        {
            var byKey = new SortedDictionary<string, Group>(StringComparer.Ordinal);
            foreach (string name in names)
            {
                char first = string.IsNullOrEmpty(name) ? '#' : char.ToUpperInvariant(name[0]);
                string key = char.IsLetter(first) ? first.ToString() : "#";
                if (!byKey.TryGetValue(key, out var group)) byKey[key] = group = new Group { Key = key };
                group.Names.Add(name);
            }
            return byKey.Values.ToList();
        }

        static List<Group> ByRange(IList<string> names, int size)
        {
            var sorted = names.OrderBy(n => n, NameRules.Natural).ToList();
            var result = new List<Group>();
            for (int i = 0; i < sorted.Count; i += size)
            {
                var chunk = sorted.Skip(i).Take(size).ToList();
                var group = new Group { Key = $"{chunk[0]} … {chunk[chunk.Count - 1]}" };
                group.Names.AddRange(chunk);
                result.Add(group);
            }
            return result;
        }
    }
}
