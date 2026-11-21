using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gnurt.LogicMap
{
    internal sealed class LayoutOptions
    {
        public float LayerGap = 56f;     // khoảng dọc tối thiểu giữa hai tầng
        public float NodeGap = 32f;      // khoảng ngang tối thiểu giữa hai ô
        public float ComponentGap = 72f; // khoảng giữa các cụm không nối với nhau
        public float Margin = 24f;
        public float TrackSpacing = 12f; // khoảng giữa các đường ngang chạy song song
        public float DummyWidth = 8f;
        public int Sweeps = 8;
    }

    /// <summary>Đường đi của một đường nối: các điểm gấp khúc (chỉ ngang hoặc dọc) và chỗ đặt nhãn.</summary>
    internal sealed class EdgeRoute
    {
        public LogicEdge Edge;
        public readonly List<Vector2> Points = new List<Vector2>();
        public Vector2 LabelPosition;
        public bool IsBack; // đường quay ngược lên trên (vòng lặp)
    }

    internal sealed class GraphLayoutResult
    {
        public readonly Dictionary<string, Rect> Nodes = new Dictionary<string, Rect>();
        public readonly List<EdgeRoute> Edges = new List<EdgeRoute>();
        public Vector2 Size;
    }

    /// <summary>
    /// Tự xếp flow chart theo tầng, từ trên xuống: chia tầng, đảo thứ tự trong tầng cho ít đường cắt nhau,
    /// căn ô con dưới ô cha, rồi vẽ đường nối gấp khúc. Người dùng không kéo ô được nên mọi vị trí do đây tính.
    /// </summary>
    internal static class GraphLayout
    {
        sealed class Vertex
        {
            public LogicNode Node; // null = ô ảo, để đường nối dài đi qua các tầng ở giữa
            public float Width, Height;
            public int Layer, Order;
            public float Key; // thứ tự ban đầu trong tầng
            public float X;   // tâm theo chiều ngang
            public float Top;
            public readonly List<Vertex> Up = new List<Vertex>();
            public readonly List<Vertex> Down = new List<Vertex>();

            public bool IsDummy => Node == null;
            public float Left => X - Width / 2;
            public float Right => X + Width / 2;
            public float Bottom => Top + Height;
            public float CenterY => Top + Height / 2;
        }

        sealed class Chain
        {
            public LogicEdge Edge;
            public readonly List<Vertex> Path = new List<Vertex>();
            public bool SideExit; // ô thoi rẽ nhánh sang ngang, như flow chart vẽ tay
            public Segment[] Hops;
        }

        sealed class Segment
        {
            public int Gap; // khe dưới tầng Gap; -1 là khe trên tầng đầu
            public float Min, Max;
            public string Key; // các đoạn cùng Key (cùng ô nguồn) được đi chung một làn
            public int Track;
        }

        public static GraphLayoutResult Run(LogicGraph graph, Func<LogicNode, Vector2> sizeOf, LayoutOptions options = null)
        {
            options ??= new LayoutOptions();
            var result = new GraphLayoutResult();

            var vertices = new Dictionary<string, Vertex>();
            var ordered = new List<Vertex>();
            foreach (var node in graph.Nodes)
            {
                if (vertices.ContainsKey(node.Id)) continue;
                var size = sizeOf(node);
                var vertex = new Vertex { Node = node, Width = Mathf.Max(1, size.x), Height = Mathf.Max(1, size.y) };
                vertices[node.Id] = vertex;
                ordered.Add(vertex);
            }

            var links = new List<LogicEdge>();
            var selfLoops = new List<LogicEdge>();
            foreach (var edge in graph.Edges)
            {
                if (!vertices.ContainsKey(edge.From) || !vertices.ContainsKey(edge.To)) continue;
                if (edge.From == edge.To) selfLoops.Add(edge);
                else links.Add(edge);
            }

            var linked = new HashSet<string>(links.SelectMany(e => new[] { e.From, e.To }));
            float m = options.Margin;
            float x = m, right = m, bottom = m;

            // Mỗi cụm nối với nhau xếp riêng rồi đặt cạnh nhau, cụm chứa ô đầu tiên đứng trước.
            var components = Components(ordered.Where(v => linked.Contains(v.Node.Id)).ToList(), links, vertices);
            foreach (var component in components)
            {
                var local = new GraphLayoutResult();
                var size = LayoutComponent(component.Item1, component.Item2, options, local);
                Append(result, local, new Vector2(x, m));
                right = x + size.x;
                x = right + options.ComponentGap;
                bottom = Mathf.Max(bottom, m + size.y);
            }

            // Ô không nối với ai: xếp lưới bên dưới để không làm rối phần chính.
            var isolated = ordered.Where(v => !linked.Contains(v.Node.Id)).ToList();
            if (isolated.Count > 0)
            {
                float rowWidth = Mathf.Max(right - m, 560f);
                float cx = m, cy = components.Count > 0 ? bottom + options.LayerGap : m, rowHeight = 0;
                foreach (var v in isolated)
                {
                    if (cx > m && cx + v.Width > m + rowWidth)
                    {
                        cx = m;
                        cy += rowHeight + options.NodeGap;
                        rowHeight = 0;
                    }
                    result.Nodes[v.Node.Id] = new Rect(cx, cy, v.Width, v.Height);
                    cx += v.Width + options.NodeGap;
                    rowHeight = Mathf.Max(rowHeight, v.Height);
                    right = Mathf.Max(right, cx - options.NodeGap);
                }
                bottom = cy + rowHeight;
            }

            foreach (var edge in selfLoops)
            {
                var rect = result.Nodes[edge.From];
                var route = new EdgeRoute { Edge = edge };
                float loopX = rect.xMax + 18;
                route.Points.Add(new Vector2(rect.xMax, rect.center.y - 6));
                route.Points.Add(new Vector2(loopX, rect.center.y - 6));
                route.Points.Add(new Vector2(loopX, rect.center.y + 6));
                route.Points.Add(new Vector2(rect.xMax, rect.center.y + 6));
                route.LabelPosition = new Vector2(loopX + 4, rect.center.y - 8);
                result.Edges.Add(route);
                right = Mathf.Max(right, loopX);
            }

            result.Size = new Vector2(right + m, bottom + m);
            return result;
        }

        static void Append(GraphLayoutResult target, GraphLayoutResult local, Vector2 offset)
        {
            foreach (var pair in local.Nodes)
                target.Nodes[pair.Key] = new Rect(pair.Value.position + offset, pair.Value.size);
            foreach (var route in local.Edges)
            {
                for (int i = 0; i < route.Points.Count; i++) route.Points[i] += offset;
                route.LabelPosition += offset;
                target.Edges.Add(route);
            }
        }

        // Các cụm ô nối với nhau (không tính chiều), giữ thứ tự xuất hiện.
        static List<Tuple<List<Vertex>, List<LogicEdge>>> Components(List<Vertex> nodes, List<LogicEdge> links, Dictionary<string, Vertex> byId)
        {
            var neighbors = nodes.ToDictionary(v => v, _ => new List<Vertex>());
            foreach (var edge in links)
            {
                neighbors[byId[edge.From]].Add(byId[edge.To]);
                neighbors[byId[edge.To]].Add(byId[edge.From]);
            }

            var componentOf = new Dictionary<Vertex, int>();
            var result = new List<Tuple<List<Vertex>, List<LogicEdge>>>();
            foreach (var start in nodes)
            {
                if (componentOf.ContainsKey(start)) continue;
                int id = result.Count;
                var members = new List<Vertex>();
                var stack = new Stack<Vertex>();
                stack.Push(start);
                componentOf[start] = id;
                while (stack.Count > 0)
                {
                    var v = stack.Pop();
                    members.Add(v);
                    foreach (var n in neighbors[v])
                    {
                        if (componentOf.ContainsKey(n)) continue;
                        componentOf[n] = id;
                        stack.Push(n);
                    }
                }
                var memberSet = new HashSet<Vertex>(members);
                result.Add(Tuple.Create(nodes.Where(memberSet.Contains).ToList(), new List<LogicEdge>()));
            }
            foreach (var edge in links) result[componentOf[byId[edge.From]]].Item2.Add(edge);
            return result;
        }

        static Vector2 LayoutComponent(List<Vertex> nodes, List<LogicEdge> links, LayoutOptions o, GraphLayoutResult local)
        {
            var byId = nodes.ToDictionary(v => v.Node.Id);
            var outgoing = nodes.ToDictionary(v => v, _ => new List<LogicEdge>());
            var incoming = nodes.ToDictionary(v => v, _ => 0);
            foreach (var edge in links)
            {
                outgoing[byId[edge.From]].Add(edge);
                incoming[byId[edge.To]]++;
            }

            // 1. Tìm đường quay ngược (vòng lặp) bằng DFS để phần còn lại không có vòng.
            var state = new Dictionary<Vertex, int>();
            var preorder = new Dictionary<Vertex, int>();
            var back = new HashSet<LogicEdge>();
            void Visit(Vertex v)
            {
                state[v] = 1;
                preorder[v] = preorder.Count;
                foreach (var edge in outgoing[v])
                {
                    var w = byId[edge.To];
                    state.TryGetValue(w, out int s);
                    if (s == 1) back.Add(edge);
                    else if (s == 0) Visit(w);
                }
                state[v] = 2;
            }
            foreach (var v in nodes.Where(v => incoming[v] == 0)) if (!state.ContainsKey(v)) Visit(v);
            foreach (var v in nodes) if (!state.ContainsKey(v)) Visit(v);

            // 2. Chia tầng theo đường dài nhất.
            var forward = links.Where(e => !back.Contains(e)).ToList();
            var indegree = nodes.ToDictionary(v => v, _ => 0);
            foreach (var edge in forward) indegree[byId[edge.To]]++;
            var queue = new Queue<Vertex>(nodes.Where(v => indegree[v] == 0).OrderBy(v => preorder[v]));
            foreach (var v in nodes) v.Layer = 0;
            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                foreach (var edge in outgoing[v])
                {
                    if (back.Contains(edge)) continue;
                    var w = byId[edge.To];
                    w.Layer = Mathf.Max(w.Layer, v.Layer + 1);
                    if (--indegree[w] == 0) queue.Enqueue(w);
                }
            }

            // Ô không có ai trỏ tới (vd script chung) kéo xuống ngay trên ô nó trỏ tới, thay vì nằm tít trên cùng.
            foreach (var v in nodes)
            {
                if (forward.Any(e => e.To == v.Node.Id)) continue;
                var targets = forward.Where(e => e.From == v.Node.Id).Select(e => byId[e.To].Layer).ToList();
                if (targets.Count > 0) v.Layer = Mathf.Max(0, targets.Min() - 1);
            }

            // 3. Đường nối dài hơn một tầng thì chèn ô ảo ở mỗi tầng giữa.
            var all = new List<Vertex>(nodes);
            foreach (var v in nodes) v.Key = preorder[v];
            var chains = new List<Chain>();
            foreach (var edge in forward)
            {
                var a = byId[edge.From];
                var b = byId[edge.To];
                var chain = new Chain { Edge = edge };
                chain.Path.Add(a);
                var previous = a;
                for (int layer = a.Layer + 1; layer < b.Layer; layer++)
                {
                    var dummy = new Vertex { Width = o.DummyWidth, Height = 0, Layer = layer, Key = preorder[a] + 0.5f + chains.Count * 1e-4f };
                    Link(previous, dummy);
                    chain.Path.Add(dummy);
                    all.Add(dummy);
                    previous = dummy;
                }
                Link(previous, b);
                chain.Path.Add(b);
                chains.Add(chain);
            }

            int layerCount = all.Max(v => v.Layer) + 1;
            var layers = new List<List<Vertex>>();
            for (int l = 0; l < layerCount; l++)
            {
                var row = all.Where(v => v.Layer == l).OrderBy(v => v.Key).ToList();
                for (int i = 0; i < row.Count; i++) row[i].Order = i;
                layers.Add(row);
            }

            // 4. Đảo thứ tự trong tầng (barycenter) cho ít đường cắt nhau, giữ lại lần tốt nhất.
            var best = layers.Select(r => r.ToList()).ToList();
            int bestCrossings = Crossings(layers);
            for (int sweep = 0; sweep < o.Sweeps && bestCrossings > 0; sweep++)
            {
                if (sweep % 2 == 0) for (int l = 1; l < layerCount; l++) Reorder(layers[l], up: true);
                else for (int l = layerCount - 2; l >= 0; l--) Reorder(layers[l], up: false);

                int crossings = Crossings(layers);
                if (crossings < bestCrossings)
                {
                    bestCrossings = crossings;
                    best = layers.Select(r => r.ToList()).ToList();
                }
            }
            layers = best;
            foreach (var row in layers)
                for (int i = 0; i < row.Count; i++) row[i].Order = i;

            // 5. Toạ độ ngang: ô cha nằm giữa các ô con, ô con nằm dưới ô cha, không chồng lên nhau.
            foreach (var row in layers)
            {
                float cursor = 0;
                for (int i = 0; i < row.Count; i++)
                {
                    if (i > 0) cursor += Gap(row[i - 1], row[i], o);
                    row[i].X = cursor + row[i].Width / 2;
                    cursor += row[i].Width;
                }
            }
            for (int pass = 0; pass < 6; pass++)
            {
                for (int l = 1; l < layerCount; l++) Place(layers[l], up: true, o);
                for (int l = layerCount - 2; l >= 0; l--) Place(layers[l], up: false, o);
            }
            for (int l = 1; l < layerCount; l++) Place(layers[l], up: true, o);

            float minLeft = all.Min(v => v.Left);
            foreach (var v in all) v.X -= minLeft;

            // 6. Ô thoi có nhánh nằm hẳn sang một bên thì cho nhánh đó đi ra từ góc bên.
            foreach (var chain in chains)
                chain.SideExit = CanExitSideways(chain.Path[0], chain.Path[1], layers[chain.Path[0].Layer]);

            // 7. Các đoạn ngang trong khe giữa hai tầng: chia làn để đường của các ô khác nhau không đè lên nhau.
            var segments = new List<Segment>();
            for (int c = 0; c < chains.Count; c++)
            {
                var chain = chains[c];
                chain.Hops = new Segment[chain.Path.Count - 1];
                for (int i = 0; i < chain.Path.Count - 1; i++)
                {
                    if (i == 0 && chain.SideExit) continue;
                    var a = chain.Path[i];
                    var b = chain.Path[i + 1];
                    if (Mathf.Abs(a.X - b.X) < 0.01f) continue; // đi thẳng xuống, không cần đoạn ngang
                    var segment = new Segment
                    {
                        Gap = a.Layer,
                        Min = Mathf.Min(a.X, b.X),
                        Max = Mathf.Max(a.X, b.X),
                        Key = a.IsDummy ? "c:" + c : "n:" + a.Node.Id,
                    };
                    chain.Hops[i] = segment;
                    segments.Add(segment);
                }
            }

            var backEdges = links.Where(back.Contains).ToList();
            var backLanes = new float[backEdges.Count];
            var backSegments = new Segment[backEdges.Count, 2];
            float maxRight = all.Max(v => v.Right);

            // Các đường quay về cùng một ô đi chung một làn bên phải, như một đường gom, đỡ rối hơn mỗi đường một làn.
            var laneOf = new Dictionary<string, float>();
            foreach (string targetId in backEdges.Select(e => e.To).Distinct().ToList())
            {
                int top = byId[targetId].Layer;
                int bottom = backEdges.Where(e => e.To == targetId).Max(e => byId[e.From].Layer);
                float laneBase = all.Where(v => v.Layer >= top && v.Layer <= bottom).Max(v => v.Right);
                laneOf[targetId] = laneBase + o.NodeGap * 0.75f + laneOf.Count * o.TrackSpacing;
            }

            for (int b = 0; b < backEdges.Count; b++)
            {
                var from = byId[backEdges[b].From];
                var to = byId[backEdges[b].To];
                backLanes[b] = laneOf[to.Node.Id];
                maxRight = Mathf.Max(maxRight, backLanes[b]);
                string key = "b:" + to.Node.Id;
                backSegments[b, 0] = new Segment { Gap = from.Layer, Min = from.X, Max = backLanes[b], Key = key };
                backSegments[b, 1] = new Segment { Gap = to.Layer - 1, Min = to.X, Max = backLanes[b], Key = key };
                segments.Add(backSegments[b, 0]);
                segments.Add(backSegments[b, 1]);
            }

            var trackCount = new int[layerCount + 1]; // chỉ số gap + 1
            foreach (var group in segments.GroupBy(s => s.Gap))
            {
                var tracks = new List<List<Segment>>();
                foreach (var segment in group.OrderBy(s => s.Min))
                {
                    int t = tracks.FindIndex(track => track.All(s => s.Key == segment.Key || s.Max + 6 < segment.Min || segment.Max + 6 < s.Min));
                    if (t < 0)
                    {
                        t = tracks.Count;
                        tracks.Add(new List<Segment>());
                    }
                    tracks[t].Add(segment);
                    segment.Track = t;
                }
                trackCount[group.Key + 1] = tracks.Count;
            }

            // 8. Toạ độ dọc: mỗi tầng cao bằng ô cao nhất, khe giãn ra khi nhiều làn.
            var gapTop = new float[layerCount + 1];
            var gapHeight = new float[layerCount + 1];
            var layerTop = new float[layerCount];
            var layerHeight = new float[layerCount];
            float y = 0;
            for (int g = -1; g < layerCount; g++)
            {
                if (g >= 0)
                {
                    layerTop[g] = y;
                    layerHeight[g] = layers[g].Where(v => !v.IsDummy).Select(v => v.Height).DefaultIfEmpty(0).Max();
                    y += layerHeight[g];
                }
                int tracks = trackCount[g + 1];
                bool inner = g >= 0 && g < layerCount - 1;
                gapTop[g + 1] = y;
                gapHeight[g + 1] = inner
                    ? Mathf.Max(o.LayerGap, (tracks + 1) * o.TrackSpacing + 16)
                    : tracks > 0 ? (tracks + 1) * o.TrackSpacing + 12 : 0;
                y += gapHeight[g + 1];
            }
            foreach (var v in all) v.Top = layerTop[v.Layer] + (layerHeight[v.Layer] - v.Height) / 2;

            float TrackY(Segment s)
            {
                int tracks = trackCount[s.Gap + 1];
                return gapTop[s.Gap + 1] + (gapHeight[s.Gap + 1] - (tracks - 1) * o.TrackSpacing) / 2 + s.Track * o.TrackSpacing;
            }

            // 9. Vẽ đường nối. Nhãn thường đặt ngay trên ô đích; nhưng nếu nhiều đường có nhãn cùng đổ vào
            // một ô thì nhãn dồn một chỗ, khi đó đặt nhãn ngay dưới ô nguồn (nếu ô nguồn chỉ có một đường ra).
            var outgoingCount = new Dictionary<Vertex, int>();
            var labeledIncoming = new Dictionary<Vertex, int>();
            foreach (var chain in chains)
            {
                var source = chain.Path[0];
                var target = chain.Path[chain.Path.Count - 1];
                outgoingCount[source] = outgoingCount.TryGetValue(source, out int count) ? count + 1 : 1;
                if (!string.IsNullOrEmpty(chain.Edge.Label))
                    labeledIncoming[target] = labeledIncoming.TryGetValue(target, out int labeled) ? labeled + 1 : 1;
            }

            foreach (var chain in chains)
            {
                var route = new EdgeRoute { Edge = chain.Edge };
                var points = route.Points;
                var first = chain.Path[0];
                float curX, turnY;

                if (chain.SideExit)
                {
                    var next = chain.Path[1];
                    float startX = next.X > first.X ? first.Right : first.Left;
                    points.Add(new Vector2(startX, first.CenterY));
                    points.Add(new Vector2(next.X, first.CenterY));
                    curX = next.X;
                    turnY = first.CenterY;
                }
                else
                {
                    points.Add(new Vector2(first.X, first.Bottom));
                    curX = first.X;
                    turnY = first.Bottom;
                }

                for (int i = 0; i < chain.Hops.Length; i++)
                {
                    var hop = chain.Hops[i];
                    if (hop == null)
                    {
                        curX = chain.Path[i + 1].X;
                        continue;
                    }
                    float hopY = TrackY(hop);
                    float nextX = chain.Path[i + 1].X;
                    points.Add(new Vector2(curX, hopY));
                    points.Add(new Vector2(nextX, hopY));
                    curX = nextX;
                    turnY = hopY;
                }

                var last = chain.Path[chain.Path.Count - 1];
                points.Add(new Vector2(last.X, last.Top));
                Simplify(points);
                bool nearSource = !chain.SideExit && labeledIncoming.TryGetValue(last, out int fanIn) && fanIn >= 2 && outgoingCount[first] == 1;
                route.LabelPosition = nearSource ? new Vector2(first.X + 5, first.Bottom + 2) : new Vector2(last.X + 5, turnY + 2);
                local.Edges.Add(route);
            }

            for (int b = 0; b < backEdges.Count; b++)
            {
                var from = byId[backEdges[b].From];
                var to = byId[backEdges[b].To];
                float y1 = TrackY(backSegments[b, 0]);
                float y2 = TrackY(backSegments[b, 1]);
                var route = new EdgeRoute { Edge = backEdges[b], IsBack = true };
                route.Points.Add(new Vector2(from.X, from.Bottom));
                route.Points.Add(new Vector2(from.X, y1));
                route.Points.Add(new Vector2(backLanes[b], y1));
                route.Points.Add(new Vector2(backLanes[b], y2));
                route.Points.Add(new Vector2(to.X, y2));
                route.Points.Add(new Vector2(to.X, to.Top));
                Simplify(route.Points);
                route.LabelPosition = new Vector2(from.X + 5, y1 + 2); // ngay dưới đường ngang ra khỏi ô nguồn
                local.Edges.Add(route);
            }

            foreach (var v in nodes)
                local.Nodes[v.Node.Id] = new Rect(v.Left, v.Top, v.Width, v.Height);

            return new Vector2(maxRight + (backEdges.Count > 0 ? o.TrackSpacing : 0), y);
        }

        static void Link(Vertex a, Vertex b)
        {
            a.Down.Add(b);
            b.Up.Add(a);
        }

        static float Gap(Vertex a, Vertex b, LayoutOptions o)
        {
            if (a.IsDummy && b.IsDummy) return o.TrackSpacing;
            if (a.IsDummy || b.IsDummy) return o.NodeGap / 2;
            return o.NodeGap;
        }

        static int Crossings(List<List<Vertex>> layers)
        {
            int total = 0;
            var pairs = new List<(int a, int b)>();
            for (int l = 0; l < layers.Count - 1; l++)
            {
                pairs.Clear();
                foreach (var a in layers[l])
                    foreach (var b in a.Down)
                        pairs.Add((a.Order, b.Order));
                for (int i = 0; i < pairs.Count; i++)
                    for (int j = i + 1; j < pairs.Count; j++)
                        if ((pairs[i].a - pairs[j].a) * (pairs[i].b - pairs[j].b) < 0) total++;
            }
            return total;
        }

        static void Reorder(List<Vertex> row, bool up)
        {
            var barycenter = new Dictionary<Vertex, float>();
            foreach (var v in row)
            {
                var neighbors = up ? v.Up : v.Down;
                barycenter[v] = neighbors.Count > 0 ? (float)neighbors.Average(n => n.Order) : v.Order;
            }
            var sorted = row.OrderBy(v => barycenter[v]).ThenBy(v => v.Order).ToList();
            row.Clear();
            row.AddRange(sorted);
            for (int i = 0; i < row.Count; i++) row[i].Order = i;
        }

        // Đặt mỗi ô gần tâm các ô nối với nó nhất có thể mà vẫn giữ thứ tự và khoảng cách tối thiểu.
        static void Place(List<Vertex> row, bool up, LayoutOptions o)
        {
            var desired = new float[row.Count];
            for (int i = 0; i < row.Count; i++)
            {
                var neighbors = up ? row[i].Up : row[i].Down;
                desired[i] = neighbors.Count > 0 ? neighbors.Average(n => n.X) : row[i].X;
            }
            Pack(row, desired, o);
        }

        // Hồi quy đơn điệu (pool adjacent violators): đổi biến để ràng buộc khoảng cách thành "không giảm",
        // rồi gộp các khối vi phạm. Cho vị trí gần mong muốn nhất theo bình phương sai lệch.
        static void Pack(List<Vertex> row, float[] desired, LayoutOptions o)
        {
            int n = row.Count;
            if (n == 0) return;
            var offset = new float[n];
            for (int i = 1; i < n; i++)
                offset[i] = offset[i - 1] + (row[i - 1].Width + row[i].Width) / 2 + Gap(row[i - 1], row[i], o);

            var blockStart = new List<int>();
            var blockSum = new List<float>();
            var blockCount = new List<int>();
            for (int i = 0; i < n; i++)
            {
                blockStart.Add(i);
                blockSum.Add(desired[i] - offset[i]);
                blockCount.Add(1);
                while (blockStart.Count >= 2)
                {
                    int k = blockStart.Count - 1;
                    if (blockSum[k - 1] / blockCount[k - 1] <= blockSum[k] / blockCount[k]) break;
                    blockSum[k - 1] += blockSum[k];
                    blockCount[k - 1] += blockCount[k];
                    blockStart.RemoveAt(k);
                    blockSum.RemoveAt(k);
                    blockCount.RemoveAt(k);
                }
            }

            for (int k = 0; k < blockStart.Count; k++)
            {
                float mean = blockSum[k] / blockCount[k];
                for (int i = blockStart[k]; i < blockStart[k] + blockCount[k]; i++)
                    row[i].X = mean + offset[i];
            }
        }

        static bool CanExitSideways(Vertex from, Vertex next, List<Vertex> row)
        {
            if (from.IsDummy || from.Node.Kind != NodeKind.Decision || from.Down.Count < 2) return false;
            bool toRight = next.Left > from.Right;
            bool toLeft = next.Right < from.Left;
            if (!toRight && !toLeft) return false;

            float min = toRight ? from.Right : next.X - 2;
            float max = toRight ? next.X + 2 : from.Left;
            foreach (var v in row)
            {
                if (v == from) continue;
                // đường ngang đi qua giữa tầng: không được cắt ô khác hay đường dọc khác
                if (v.IsDummy ? v.X > min && v.X < max : v.Right > min && v.Left < max) return false;
            }
            return true;
        }

        // Bỏ điểm trùng và điểm nằm giữa một đoạn thẳng.
        static void Simplify(List<Vector2> points)
        {
            for (int i = points.Count - 1; i > 0; i--)
                if ((points[i] - points[i - 1]).sqrMagnitude < 1e-6f) points.RemoveAt(i);
            for (int i = points.Count - 2; i > 0; i--)
            {
                var a = points[i - 1];
                var b = points[i];
                var c = points[i + 1];
                bool sameX = Mathf.Abs(a.x - b.x) < 0.001f && Mathf.Abs(b.x - c.x) < 0.001f;
                bool sameY = Mathf.Abs(a.y - b.y) < 0.001f && Mathf.Abs(b.y - c.y) < 0.001f;
                if (sameX || sameY) points.RemoveAt(i);
            }
        }
    }
}
