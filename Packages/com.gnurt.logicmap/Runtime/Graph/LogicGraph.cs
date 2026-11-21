using System.Collections.Generic;
using System.Linq;

namespace Gnurt.LogicMap
{
    /// <summary>Loại ô trên flow chart, quyết định hình vẽ.</summary>
    internal enum NodeKind
    {
        Start,    // điểm bắt đầu: viên thuốc tô màu
        End,      // điểm kết thúc: viên thuốc chỉ có viền
        Process,  // một bước: hình chữ nhật
        Decision, // rẽ nhánh: hình thoi
        Section,  // một scene hoặc prefab (map, minigame...): thẻ có vạch màu
        Group,    // nhiều mục giống nhau gộp lại: thẻ xếp chồng
        Unknown,  // chưa rõ là gì: viền nét đứt
    }

    /// <summary>Màu nhấn của ô.</summary>
    internal enum NodeTone
    {
        Neutral,
        Scene,  // tím
        Prefab, // vàng
        Script, // xanh
        Muted,
    }

    internal enum EdgeStyle
    {
        Solid,
        Dashed,
    }

    internal sealed class LogicNode
    {
        public string Id;
        public NodeKind Kind;
        public NodeTone Tone;
        public string Title;
        public string Subtitle;  // dòng nhỏ dưới tên
        public string Detail;    // chỉ hiện ở panel chi tiết
        public string AssetPath; // scene, prefab hoặc script để mở
        public int Line;         // dòng trong script, 0 = không có
        public string PageId;    // bấm đúp thì mở trang này
        public bool Highlight;   // ô chính của trang
        public readonly List<NodeItem> Items = new List<NodeItem>(); // các mục bên trong một nhóm
    }

    /// <summary>Một mục bên trong ô nhóm, hiện thành danh sách ở panel chi tiết.</summary>
    internal sealed class NodeItem
    {
        public string Title;
        public string AssetPath;
        public string PageId;
    }

    internal sealed class LogicEdge
    {
        public string From;
        public string To;
        public EdgeStyle Style;
        public readonly List<string> Labels = new List<string>();

        /// <summary>Chữ trên đường nối. Nhiều nhãn thì hiện hai cái đầu và số còn lại.</summary>
        public string Label
        {
            get
            {
                if (Labels.Count == 0) return null;
                if (Labels.Count <= 2) return string.Join(", ", Labels);
                return $"{Labels[0]}, {Labels[1]} +{Labels.Count - 2}";
            }
        }
    }

    /// <summary>Một flow chart: các ô và đường nối có hướng. Chỉ để xem, không sửa được logic game.</summary>
    internal sealed class LogicGraph
    {
        public readonly List<LogicNode> Nodes = new List<LogicNode>();
        public readonly List<LogicEdge> Edges = new List<LogicEdge>();
        readonly Dictionary<string, LogicNode> _byId = new Dictionary<string, LogicNode>();

        public LogicNode Find(string id) => id != null && _byId.TryGetValue(id, out var node) ? node : null;

        /// <summary>Thêm ô. Đã có ô cùng Id thì trả về ô cũ.</summary>
        public LogicNode Add(LogicNode node)
        {
            if (_byId.TryGetValue(node.Id, out var existing)) return existing;
            _byId[node.Id] = node;
            Nodes.Add(node);
            return node;
        }

        /// <summary>Chèn ô vào đúng vị trí trong danh sách (thứ tự ảnh hưởng cách xếp).</summary>
        public LogicNode Insert(int index, LogicNode node)
        {
            if (_byId.TryGetValue(node.Id, out var existing)) return existing;
            _byId[node.Id] = node;
            Nodes.Insert(index, node);
            return node;
        }

        /// <summary>Nối hai ô. Đã có đường nối giữa hai ô thì gộp nhãn vào đường cũ.</summary>
        public LogicEdge Connect(string from, string to, string label = null, EdgeStyle style = EdgeStyle.Solid)
        {
            var edge = Edges.FirstOrDefault(e => e.From == from && e.To == to);
            if (edge == null)
            {
                edge = new LogicEdge { From = from, To = to, Style = style };
                Edges.Add(edge);
            }
            else if (style == EdgeStyle.Solid)
            {
                edge.Style = EdgeStyle.Solid; // có ít nhất một đường chắc chắn thì vẽ nét liền
            }

            if (!string.IsNullOrEmpty(label) && !edge.Labels.Contains(label)) edge.Labels.Add(label);
            return edge;
        }

        /// <summary>Xoá ô cùng mọi đường nối của nó.</summary>
        public void Remove(string id)
        {
            if (!_byId.Remove(id)) return;
            Nodes.RemoveAll(n => n.Id == id);
            Edges.RemoveAll(e => e.From == id || e.To == id);
        }
    }
}
