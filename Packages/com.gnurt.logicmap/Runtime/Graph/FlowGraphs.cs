using System;
using System.Collections.Generic;
using System.Linq;

namespace Gnurt.LogicMap
{
    /// <summary>
    /// Dựng flow chart từ kết quả quét. <c>t(english, vietnamese)</c> chọn ngôn ngữ cho chữ trên chart,
    /// để assembly này không phải phụ thuộc Editor.
    /// </summary>
    internal static class FlowGraphs
    {
        public const string EntryId = "entry";
        public const string UnknownId = "unknown";
        const int FoldMinimum = 3; // từ 3 mục cùng luồng trở lên thì gộp thành một ô

        public static string PageIdOf(SectionInfo section) => "section:" + section.path;

        /// <summary>Luồng cả game: scene/prefab nào dẫn tới scene/prefab nào.</summary>
        public static LogicGraph Overview(ProjectAnalysis analysis, Func<string, string, string> t)
        {
            var graph = new LogicGraph();
            bool Included(SectionInfo s) => s.kind == SectionKind.Prefab || s.inBuild;

            if (analysis.Entry != null)
                graph.Add(EntryNode(EntryId, t));
            foreach (var section in ProjectAnalysis.Ordered(analysis.Scan.sections).Where(Included))
                graph.Add(SectionNode(section, section.path, t));
            if (analysis.Entry != null)
                graph.Connect(EntryId, analysis.Entry.path);

            foreach (var section in analysis.Scan.sections.Where(Included))
                foreach (string prefab in section.prefabs)
                    if (graph.Find(prefab) != null)
                        graph.Connect(section.path, prefab, t("contains", "chứa"), EdgeStyle.Dashed);

            foreach (var load in analysis.Loads)
            {
                string from;
                if (load.Source == null)
                {
                    from = "script:" + load.Script.path;
                    graph.Add(ScriptNode(load.Script, from, t));
                }
                else
                {
                    if (!Included(load.Source)) continue;
                    from = load.Source.path;
                }
                string label = load.Source == null ? load.Call.condition : load.Script.name;

                if (load.Targets.Count == 0)
                {
                    graph.Add(new LogicNode
                    {
                        Id = UnknownId,
                        Kind = NodeKind.Unknown,
                        Tone = NodeTone.Muted,
                        Title = t("Decided at runtime", "Tính lúc chạy"),
                        Subtitle = t("scene name not readable from code", "không đọc được tên scene từ code"),
                    });
                    graph.Connect(from, UnknownId, label, EdgeStyle.Dashed);
                    continue;
                }
                foreach (var target in load.Targets)
                    if (graph.Find(target.path) != null)
                        graph.Connect(from, target.path, label);
            }

            FoldSimilar(graph, analysis, t);
            return graph;
        }

        /// <summary>Luồng của một mục: từ đâu tới, script nào chuyển đi đâu, chứa prefab nào.</summary>
        public static LogicGraph Section(ProjectAnalysis analysis, SectionInfo section, Func<string, string, string> t)
        {
            var graph = new LogicGraph();
            var self = SectionNode(section, section.path, t);
            self.Highlight = true;
            self.PageId = null; // đang ở trang này rồi

            if (section == analysis.Entry) graph.Add(EntryNode(EntryId, t));
            var entries = analysis.EntriesOf(section).ToList();
            foreach (var load in entries)
            {
                if (load.Source == null) graph.Add(ScriptNode(load.Script, "in:script:" + load.Script.path, t));
                else graph.Add(SectionNode(load.Source, "in:" + load.Source.path, t));
            }
            var containers = analysis.ContainersOf(section).ToList();
            foreach (var container in containers)
                graph.Add(SectionNode(container, "in:" + container.path, t));
            graph.Add(self);

            if (section == analysis.Entry) graph.Connect(EntryId, self.Id);
            foreach (var load in entries)
            {
                string from = load.Source == null ? "in:script:" + load.Script.path : "in:" + load.Source.path;
                graph.Connect(from, self.Id, load.Source == null ? load.Call.condition : load.Script.name);
            }
            foreach (var container in containers)
                graph.Connect("in:" + container.path, self.Id, t("contains", "chứa"), EdgeStyle.Dashed);

            // Mỗi hàm có lệnh chuyển scene là một ô; hàm có nhiều đích thì là ô rẽ nhánh (hình thoi).
            foreach (var group in analysis.ExitsOf(section).GroupBy(l => l.Script.path + "|" + l.Call.member))
            {
                var loads = group.OrderBy(l => l.Call.line).ToList();
                var script = loads[0].Script;
                string member = loads[0].Call.member;
                string callId = "call:" + group.Key;
                int distinctTargets = loads.Select(TargetKey).Distinct().Count();

                graph.Add(new LogicNode
                {
                    Id = callId,
                    Kind = loads.Count >= 2 && distinctTargets >= 2 ? NodeKind.Decision : NodeKind.Process,
                    Tone = NodeTone.Script,
                    Title = member != null ? member + "()" : script.name,
                    Subtitle = member != null ? script.name : $"{script.name}:{loads[0].Call.line}",
                    Detail = string.Join("\n", loads.Select(l => $"{l.Call.line}: {l.Call.code}")),
                    AssetPath = script.path,
                    Line = loads[0].Call.line,
                });
                graph.Connect(self.Id, callId);

                for (int i = 0; i < loads.Count; i++)
                {
                    var load = loads[i];
                    string label = load.Call.condition;
                    if (load.Targets.Count == 0)
                    {
                        string unknownId = $"out:unknown:{callId}:{i}";
                        graph.Add(new LogicNode
                        {
                            Id = unknownId,
                            Kind = NodeKind.Unknown,
                            Tone = NodeTone.Muted,
                            Title = t("Decided at runtime", "Tính lúc chạy"),
                            Subtitle = CSharpSource.Shorten(load.Call.argument ?? "", 36),
                            Detail = load.Call.code,
                            AssetPath = script.path,
                            Line = load.Call.line,
                        });
                        graph.Connect(callId, unknownId, label, EdgeStyle.Dashed);
                    }
                    else if (load.Targets.Count >= FoldMinimum)
                    {
                        string groupId = "out:group:" + load.Call.target;
                        var node = graph.Add(new LogicNode
                        {
                            Id = groupId,
                            Kind = NodeKind.Group,
                            Tone = NodeTone.Scene,
                            Title = load.Call.target,
                            Subtitle = $"{load.Targets.Count} scene",
                        });
                        if (node.Items.Count == 0) node.Items.AddRange(load.Targets.Select(ItemOf));
                        graph.Connect(callId, groupId, label);
                    }
                    else
                    {
                        foreach (var target in load.Targets)
                        {
                            var node = graph.Add(SectionNode(target, "out:" + target.path, t));
                            if (target == section) node.Subtitle = t("reload this scene", "tải lại scene này");
                            graph.Connect(callId, node.Id, label);
                        }
                    }
                }
            }

            foreach (string prefabPath in section.prefabs)
            {
                var prefab = analysis.Find(prefabPath);
                if (prefab == null) continue;
                graph.Add(SectionNode(prefab, "out:" + prefab.path, t));
                graph.Connect(self.Id, "out:" + prefab.path, t("contains", "chứa"), EdgeStyle.Dashed);
            }
            return graph;
        }

        static string TargetKey(ResolvedLoad load) =>
            load.Targets.Count == 0 ? "?" + load.Call.argument : string.Join("|", load.Targets.Select(s => s.path));

        static LogicNode EntryNode(string id, Func<string, string, string> t) => new LogicNode
        {
            Id = id,
            Kind = NodeKind.Start,
            Tone = NodeTone.Neutral,
            Title = t("Game starts", "Mở game"),
            Subtitle = t("first scene in Build Settings", "scene đầu trong Build Settings"),
        };

        public static LogicNode SectionNode(SectionInfo section, string id, Func<string, string, string> t) => new LogicNode
        {
            Id = id,
            Kind = NodeKind.Section,
            Tone = section.kind == SectionKind.Scene ? NodeTone.Scene : NodeTone.Prefab,
            Title = section.name,
            Subtitle = Describe(section, t),
            AssetPath = section.path,
            PageId = PageIdOf(section),
        };

        static LogicNode ScriptNode(ScriptInfo script, string id, Func<string, string, string> t) => new LogicNode
        {
            Id = id,
            Kind = NodeKind.Process,
            Tone = NodeTone.Script,
            Title = script.name,
            Subtitle = script.dontDestroyOnLoad
                ? t("lives across scenes", "sống xuyên scene (DontDestroyOnLoad)")
                : t("not attached to any scene", "không gắn vào scene nào"),
            AssetPath = script.path,
        };

        /// <summary>"Scene · #2", "Prefab", "Scene · ngoài build".</summary>
        public static string Describe(SectionInfo section, Func<string, string, string> t)
        {
            if (section.kind == SectionKind.Prefab) return "Prefab";
            return section.inBuild ? $"Scene · #{section.buildIndex}" : "Scene · " + t("not in build", "ngoài build");
        }

        static NodeItem ItemOf(SectionInfo section) => new NodeItem
        {
            Title = section.name,
            AssetPath = section.path,
            PageId = PageIdOf(section),
        };

        // Gộp các map có đúng cùng đầu vào và đầu ra (vd 24 map đều từ Menu tới và về Menu) thành một ô.
        static void FoldSimilar(LogicGraph graph, ProjectAnalysis analysis, Func<string, string, string> t)
        {
            var candidates = graph.Nodes
                .Where(n => n.Kind == NodeKind.Section)
                .Select(n => analysis.Find(n.Id))
                .Where(s => s != null && s != analysis.Entry && analysis.RoleOf(s) == SectionRole.Gameplay)
                .ToList();

            string Signature(SectionInfo s)
            {
                var ins = graph.Edges.Where(e => e.To == s.path).Select(e => "<" + e.From + ":" + e.Style);
                var outs = graph.Edges.Where(e => e.From == s.path).Select(e => ">" + e.To + ":" + e.Style);
                return s.kind + "|" + string.Join(",", ins.Concat(outs).OrderBy(x => x, StringComparer.Ordinal));
            }

            foreach (var group in candidates.GroupBy(Signature).Where(g => g.Count() >= FoldMinimum).ToList())
            {
                var members = ProjectAnalysis.Ordered(group).ToList();
                var first = graph.Find(members[0].path);
                string groupId = "group:" + members[0].path;
                var node = graph.Insert(graph.Nodes.IndexOf(first), new LogicNode
                {
                    Id = groupId,
                    Kind = NodeKind.Group,
                    Tone = first.Tone,
                    Title = $"{members[0].name} … {members[members.Count - 1].name}",
                    Subtitle = members[0].kind == SectionKind.Scene
                        ? t($"{members.Count} scenes, same flow", $"{members.Count} scene cùng luồng")
                        : t($"{members.Count} prefabs, same flow", $"{members.Count} prefab cùng luồng"),
                });
                node.Items.AddRange(members.Select(ItemOf));

                var memberIds = new HashSet<string>(members.Select(m => m.path));
                foreach (var edge in graph.Edges.Where(e => memberIds.Contains(e.To) || memberIds.Contains(e.From)).ToList())
                {
                    string from = memberIds.Contains(edge.From) ? groupId : edge.From;
                    string to = memberIds.Contains(edge.To) ? groupId : edge.To;
                    if (edge.Labels.Count == 0) graph.Connect(from, to, null, edge.Style);
                    foreach (string label in edge.Labels) graph.Connect(from, to, label, edge.Style);
                }
                foreach (string id in memberIds) graph.Remove(id);
            }
        }
    }
}
