using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Gnurt.LogicMap.Tests
{
    public class GraphLayoutTests
    {
        static readonly Vector2 Size = new Vector2(100, 40);

        // "A>B, B>C": mỗi cặp là một đường nối. Tên bắt đầu bằng '?' là ô thoi.
        static LogicGraph Graph(string spec, params string[] extraNodes)
        {
            var graph = new LogicGraph();
            void Node(string id) => graph.Add(new LogicNode
            {
                Id = id,
                Title = id,
                Kind = id.StartsWith("?") ? NodeKind.Decision : NodeKind.Process,
            });
            foreach (string pair in spec.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var ends = pair.Trim().Split('>');
                Node(ends[0]);
                Node(ends[1]);
                graph.Connect(ends[0], ends[1]);
            }
            foreach (string id in extraNodes) Node(id);
            return graph;
        }

        static GraphLayoutResult Layout(LogicGraph graph) => GraphLayout.Run(graph, _ => Size);

        static void AssertSane(LogicGraph graph, GraphLayoutResult layout)
        {
            var rects = layout.Nodes.Values.ToList();
            Assert.AreEqual(graph.Nodes.Count, rects.Count, "mỗi ô có một vị trí");
            for (int i = 0; i < rects.Count; i++)
            {
                Assert.GreaterOrEqual(rects[i].xMin, 0);
                Assert.GreaterOrEqual(rects[i].yMin, 0);
                Assert.LessOrEqual(rects[i].xMax, layout.Size.x);
                Assert.LessOrEqual(rects[i].yMax, layout.Size.y);
                for (int j = i + 1; j < rects.Count; j++)
                    Assert.IsFalse(Shrink(rects[i]).Overlaps(Shrink(rects[j])), $"hai ô chồng nhau: {rects[i]} và {rects[j]}");
            }

            Assert.AreEqual(graph.Edges.Count, layout.Edges.Count, "mỗi đường nối có một đường vẽ");
            foreach (var route in layout.Edges)
            {
                for (int i = 1; i < route.Points.Count; i++)
                {
                    var a = route.Points[i - 1];
                    var b = route.Points[i];
                    Assert.IsTrue(Mathf.Abs(a.x - b.x) < 0.05f || Mathf.Abs(a.y - b.y) < 0.05f, "chỉ có đoạn ngang hoặc dọc");
                }
                if (route.Edge.From == route.Edge.To) continue;
                var target = layout.Nodes[route.Edge.To];
                var end = route.Points[route.Points.Count - 1];
                Assert.AreEqual(target.yMin, end.y, 0.01f, "đường nối chạm cạnh trên của ô đích");
                Assert.AreEqual(target.center.x, end.x, 0.01f);
            }
        }

        static Rect Shrink(Rect r) => new Rect(r.x + 0.5f, r.y + 0.5f, r.width - 1, r.height - 1);

        [Test]
        public void Chain_GoesStraightDown()
        {
            var graph = Graph("A>B, B>C");
            var layout = Layout(graph);
            AssertSane(graph, layout);
            var a = layout.Nodes["A"];
            var b = layout.Nodes["B"];
            var c = layout.Nodes["C"];
            Assert.Less(a.yMax, b.yMin);
            Assert.Less(b.yMax, c.yMin);
            Assert.AreEqual(a.center.x, b.center.x, 0.5f);
            Assert.AreEqual(b.center.x, c.center.x, 0.5f);
        }

        [Test]
        public void Branches_SitSideBySideUnderTheParent()
        {
            var graph = Graph("A>B, A>C, B>D, C>D");
            var layout = Layout(graph);
            AssertSane(graph, layout);
            var a = layout.Nodes["A"];
            var b = layout.Nodes["B"];
            var c = layout.Nodes["C"];
            var d = layout.Nodes["D"];
            Assert.AreEqual(b.yMin, c.yMin, 0.01f);
            Assert.AreEqual(a.center.x, (b.center.x + c.center.x) / 2, 1f, "ô cha nằm giữa hai ô con");
            Assert.AreEqual(a.center.x, d.center.x, 1f);
        }

        [Test]
        public void LongEdge_GoesAroundTheMiddleNode()
        {
            var graph = Graph("A>B, B>C, A>C");
            var layout = Layout(graph);
            AssertSane(graph, layout);
            var b = Shrink(layout.Nodes["B"]);
            var route = layout.Edges.Single(r => r.Edge.From == "A" && r.Edge.To == "C");
            for (int i = 1; i < route.Points.Count; i++)
                Assert.IsFalse(SegmentHits(route.Points[i - 1], route.Points[i], b), "đường A→C không được cắt qua ô B");
        }

        static bool SegmentHits(Vector2 a, Vector2 b, Rect r)
        {
            var min = Vector2.Min(a, b);
            var max = Vector2.Max(a, b);
            return max.x >= r.xMin && min.x <= r.xMax && max.y >= r.yMin && min.y <= r.yMax;
        }

        [Test]
        public void Cycle_IsDrawnAsBackEdge()
        {
            var graph = Graph("A>B, B>C, C>A");
            var layout = Layout(graph);
            AssertSane(graph, layout);
            var back = layout.Edges.Single(r => r.IsBack);
            Assert.AreEqual("C", back.Edge.From);
            Assert.Less(layout.Nodes["A"].yMax, layout.Nodes["C"].yMin);
            float lane = back.Points.Max(p => p.x);
            Assert.Greater(lane, layout.Nodes.Values.Max(r => r.xMax), "đường quay lên đi vòng bên phải");
        }

        [Test]
        public void IsolatedNodes_GoBelowTheChart()
        {
            var graph = Graph("A>B", "Lonely1", "Lonely2");
            var layout = Layout(graph);
            AssertSane(graph, layout);
            float bottom = Mathf.Max(layout.Nodes["A"].yMax, layout.Nodes["B"].yMax);
            Assert.Greater(layout.Nodes["Lonely1"].yMin, bottom);
            Assert.AreEqual(layout.Nodes["Lonely1"].yMin, layout.Nodes["Lonely2"].yMin, 0.01f);
        }

        [Test]
        public void SeparateClusters_SitSideBySide()
        {
            var graph = Graph("A>B, X>Y");
            var layout = Layout(graph);
            AssertSane(graph, layout);
            Assert.Less(layout.Nodes["B"].xMax, layout.Nodes["X"].xMin);
        }

        [Test]
        public void Decision_FarBranchLeavesFromTheSide()
        {
            var graph = Graph("?Q>A, ?Q>B, ?Q>C");
            var layout = Layout(graph);
            AssertSane(graph, layout);
            var q = layout.Nodes["?Q"];
            var right = layout.Edges.Single(r => r.Edge.To == "C").Points[0];
            Assert.AreEqual(q.xMax, right.x, 0.01f);
            Assert.AreEqual(q.center.y, right.y, 0.01f);
            var middle = layout.Edges.Single(r => r.Edge.To == "B").Points[0];
            Assert.AreEqual(q.yMax, middle.y, 0.01f, "nhánh giữa đi thẳng xuống");
        }

        [Test]
        public void SelfLoopAndEmptyGraph_DoNotBreak()
        {
            var loop = Graph("A>A");
            var layout = Layout(loop);
            Assert.AreEqual(1, layout.Edges.Count);
            Assert.AreEqual(0, Layout(new LogicGraph()).Nodes.Count);
        }

        [Test]
        public void RandomGraphs_NeverOverlap([NUnit.Framework.Range(0, 24)] int seed)
        {
            var random = new System.Random(seed);
            int count = 4 + random.Next(14);
            var edges = new List<string>();
            for (int i = 0; i < count * 1.4f; i++)
            {
                int a = random.Next(count);
                int b = random.Next(count);
                if (a == b) continue;
                // phần lớn đi xuôi, thỉnh thoảng có vòng
                if (a > b && random.NextDouble() < 0.8) (a, b) = (b, a);
                edges.Add($"N{a}>N{b}");
            }
            if (edges.Count == 0) edges.Add("N0>N1");
            var graph = Graph(string.Join(",", edges.Distinct()));
            var layout = GraphLayout.Run(graph, n => new Vector2(80 + n.Id.Length * 13 % 70, 34 + n.Id.Length % 3 * 10));
            AssertSane(graph, layout);
        }
    }
}
