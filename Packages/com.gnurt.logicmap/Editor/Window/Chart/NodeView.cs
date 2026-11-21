using UnityEngine;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>Một ô trên chart. Hình dạng theo loại ô (USS), riêng ô thoi vẽ bằng Painter2D.</summary>
    internal sealed class NodeView : VisualElement
    {
        public readonly LogicNode Node;
        readonly VisualElement _body;
        bool _selected;

        public NodeView(LogicNode node)
        {
            Node = node;
            AddToClassList("lm-node");
            AddToClassList("lm-node--" + node.Kind.ToString().ToLowerInvariant());
            AddToClassList("lm-tone--" + node.Tone.ToString().ToLowerInvariant());
            EnableInClassList("lm-node--highlight", node.Highlight);

            _body = new VisualElement { pickingMode = PickingMode.Ignore };
            _body.AddToClassList("lm-node__body");
            _body.Add(Text(node.Title, "lm-node__title"));
            if (!string.IsNullOrEmpty(node.Subtitle)) _body.Add(Text(node.Subtitle, "lm-node__subtitle"));
            Add(_body);

            tooltip = string.IsNullOrEmpty(node.AssetPath) ? node.Title : node.Title + "\n" + node.AssetPath;
            if (IsDecision) generateVisualContent += DrawDiamond;
        }

        static Label Text(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        bool IsDecision => Node.Kind == NodeKind.Decision;

        /// <summary>Unity đã đo xong kích thước chữ chưa.</summary>
        public bool HasMeasured =>
            !float.IsNaN(layout.width) && layout.width > 0 && (!IsDecision || (!float.IsNaN(_body.layout.width) && _body.layout.width > 0));

        /// <summary>Kích thước dùng để xếp chart. Ô thoi lớn hơn phần chữ để chữ nằm gọn bên trong.</summary>
        public Vector2 MeasuredSize => IsDecision ? DiamondSize(_body.layout.size) : layout.size;

        /// <summary>
        /// Hình thoi tỉ lệ 2:1 chứa vừa khung chữ w×h: nửa trục ngang a = w/2 + h, nửa trục dọc b = a/2
        /// (thoả (w/2)/a + (h/2)/b = 1).
        /// </summary>
        public static Vector2 DiamondSize(Vector2 inner)
        {
            float a = inner.x / 2 + inner.y;
            return new Vector2(2 * a + 16, a + 8);
        }

        public void Place(Rect rect)
        {
            style.left = rect.x;
            style.top = rect.y;
            // Đặt cứng kích thước để viền dày lên khi chọn không làm ô xê dịch.
            style.width = rect.width;
            style.height = rect.height;
        }

        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                EnableInClassList("lm-node--selected", value);
                if (IsDecision) MarkDirtyRepaint();
            }
        }

        void DrawDiamond(MeshGenerationContext context)
        {
            var r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            var p = context.painter2D;
            float inset = _selected ? 1.5f : 1f;

            p.BeginPath();
            p.MoveTo(new Vector2(r.center.x, r.yMin + inset));
            p.LineTo(new Vector2(r.xMax - inset, r.center.y));
            p.LineTo(new Vector2(r.center.x, r.yMax - inset));
            p.LineTo(new Vector2(r.xMin + inset, r.center.y));
            p.ClosePath();
            p.fillColor = ChartColors.NodeFill;
            p.Fill();
            p.strokeColor = _selected ? ChartColors.Selected : Node.Highlight ? ChartColors.Accent : ChartColors.Script;
            p.lineWidth = _selected ? 2.5f : 1.5f;
            p.lineJoin = LineJoin.Round;
            p.Stroke();
        }
    }
}
