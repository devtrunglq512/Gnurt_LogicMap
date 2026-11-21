using System;
using System.Collections.Generic;
using System.Linq;
using Gnurt.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>
    /// Vùng xem flow chart. Chỉ để xem: kéo nền để di chuyển, lăn chuột để phóng to/thu nhỏ, bấm ô để chọn,
    /// bấm đúp ô có trang riêng để mở trang đó. Không kéo được ô nên không ai sửa nhầm logic game.
    /// </summary>
    internal sealed class FlowChartView : VisualElement
    {
        const float MinZoom = 0.2f;
        const float MaxZoom = 2f;
        const float DragThreshold = 4f;

        readonly LogicGraph _graph;
        readonly VisualElement _content; // phần được phóng to và di chuyển
        readonly EdgeLayer _edges = new EdgeLayer();
        readonly VisualElement _labels;
        readonly Dictionary<string, NodeView> _views = new Dictionary<string, NodeView>();
        readonly List<(EdgeRoute route, Label label)> _edgeLabels = new List<(EdgeRoute, Label)>();
        Label _zoomText;

        GraphLayoutResult _layout;
        bool _laidOut, _fitted;
        float _zoom = 1f;
        Vector2 _pan;
        string _selected;

        int _pointerId = -1;
        Vector2 _pressPosition, _panAtPress;
        bool _dragging, _justDragged;

        /// <summary>Chọn ô khác (null = bỏ chọn).</summary>
        public event Action<LogicNode> SelectionChanged;

        /// <summary>Bấm đúp vào ô.</summary>
        public event Action<LogicNode> NodeOpened;

        public FlowChartView(LogicGraph graph, string emptyText)
        {
            _graph = graph;
            AddToClassList("lm-chart");
            focusable = true;
            generateVisualContent += DrawGrid;

            _content = new VisualElement { pickingMode = PickingMode.Ignore };
            _content.AddToClassList("lm-chart__content");
            _content.style.transformOrigin = new TransformOrigin(0, 0, 0);
            _content.style.visibility = Visibility.Hidden; // ẩn tới khi xếp xong để không thấy ô nhảy chỗ
            _labels = new VisualElement { pickingMode = PickingMode.Ignore };
            _labels.AddToClassList("lm-chart__layer");
            var nodes = new VisualElement { pickingMode = PickingMode.Ignore };
            nodes.AddToClassList("lm-chart__layer");
            _content.Add(_edges);
            _content.Add(_labels);
            _content.Add(nodes);
            Add(_content);

            if (graph.Nodes.Count == 0)
            {
                var empty = GnurtUI.Hint(emptyText);
                empty.AddToClassList("lm-chart__empty");
                Add(empty);
                return;
            }

            foreach (var node in graph.Nodes)
            {
                var view = new NodeView(node);
                view.RegisterCallback<ClickEvent>(e => OnNodeClick(view, e));
                view.RegisterCallback<GeometryChangedEvent>(_ => TryLayout());
                _views[node.Id] = view;
                nodes.Add(view);
            }

            Add(BuildToolbar());
            Add(Hint());

            RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (_laidOut && !_fitted) Fit();
            });
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<ClickEvent>(OnBackgroundClick);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            // Phòng khi Unity không báo đo xong (ô bị ẩn...): thử lại vài lần.
            schedule.Execute(TryLayout).Every(200).Until(() => _laidOut);
        }

        public LogicNode Selected => _graph.Find(_selected);

        VisualElement BuildToolbar()
        {
            var bar = GnurtUI.Element("lm-chart__toolbar");
            bar.RegisterCallback<PointerDownEvent>(e => e.StopPropagation()); // bấm nút không tính là kéo nền
            bar.Add(Tool("−", Loc.T("Zoom out", "Thu nhỏ"), () => ZoomAt(_zoom / 1.25f, ViewCenter)));
            _zoomText = Tool("100%", Loc.T("Actual size", "Cỡ thật"), () => ZoomAt(1f, ViewCenter));
            _zoomText.AddToClassList("lm-chart__zoom");
            bar.Add(_zoomText);
            bar.Add(Tool("+", Loc.T("Zoom in", "Phóng to"), () => ZoomAt(_zoom * 1.25f, ViewCenter)));
            bar.Add(Tool(Loc.T("Fit", "Vừa khung"), Loc.T("Fit the whole chart (F)", "Thu cả chart vào khung (F)"), Fit));
            return bar;
        }

        static Label Tool(string text, string tip, Action onClick)
        {
            var tool = new Label(text) { tooltip = tip };
            tool.AddToClassList("lm-chart__tool");
            tool.RegisterCallback<ClickEvent>(e =>
            {
                onClick();
                e.StopPropagation();
            });
            return tool;
        }

        static VisualElement Hint()
        {
            var hint = GnurtUI.Hint(Loc.T(
                "Drag to move · Scroll to zoom · Double-click a box to open its page",
                "Kéo để di chuyển · Lăn chuột để phóng to · Bấm đúp một ô để mở trang của nó"));
            hint.AddToClassList("lm-chart__hint");
            hint.pickingMode = PickingMode.Ignore;
            return hint;
        }

        Vector2 ViewCenter => contentRect.size / 2;

        void TryLayout()
        {
            if (_laidOut || _views.Count == 0) return;
            if (_views.Values.Any(v => !v.HasMeasured)) return;
            _laidOut = true;

            var sizes = _views.ToDictionary(pair => pair.Key, pair => pair.Value.MeasuredSize);
            _layout = GraphLayout.Run(_graph, node => sizes[node.Id]);
            foreach (var pair in _views) pair.Value.Place(_layout.Nodes[pair.Key]);

            _edges.Routes.AddRange(_layout.Edges);
            _edges.style.width = _layout.Size.x;
            _edges.style.height = _layout.Size.y;
            _edges.MarkDirtyRepaint();

            foreach (var route in _layout.Edges)
            {
                string text = route.Edge.Label;
                if (string.IsNullOrEmpty(text)) continue;
                var label = new Label(text) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("lm-edge-label");
                label.style.left = route.LabelPosition.x;
                label.style.top = route.LabelPosition.y;
                _labels.Add(label);
                _edgeLabels.Add((route, label));
            }

            _content.style.width = _layout.Size.x;
            _content.style.height = _layout.Size.y;
            _content.style.visibility = Visibility.Visible;
            Fit();
            // Cần biết kích thước chữ của nhãn nên chờ Unity đo xong ở lượt vẽ sau.
            if (_edgeLabels.Count > 0) _edgeLabels[_edgeLabels.Count - 1].label.RegisterCallback<GeometryChangedEvent>(SpreadLabels);
        }

        // Nhãn chồng lên nhau (nhiều đường đi qua cùng một khe) thì đẩy xuống dưới cho dễ đọc.
        void SpreadLabels(GeometryChangedEvent e)
        {
            ((VisualElement)e.target).UnregisterCallback<GeometryChangedEvent>(SpreadLabels);
            var placed = new List<Rect>();
            foreach (var (_, label) in _edgeLabels.OrderBy(l => l.route.LabelPosition.y).ThenBy(l => l.route.LabelPosition.x))
            {
                var rect = new Rect(label.resolvedStyle.left, label.resolvedStyle.top, label.layout.width, label.layout.height);
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    int hit = placed.FindIndex(r => r.Overlaps(rect));
                    if (hit < 0) break;
                    rect.y = placed[hit].yMax + 1;
                }
                label.style.top = rect.y;
                placed.Add(rect);
            }
        }

        /// <summary>Thu cả chart vào khung, không phóng to quá cỡ thật.</summary>
        public void Fit()
        {
            if (_layout == null) return;
            var view = contentRect.size;
            if (float.IsNaN(view.x) || view.x <= 0 || view.y <= 0) return;
            _fitted = true;

            float zoom = Mathf.Min((view.x - 24) / _layout.Size.x, (view.y - 48) / _layout.Size.y);
            _zoom = Mathf.Clamp(zoom, MinZoom, 1f);
            var scaled = _layout.Size * _zoom;
            _pan = new Vector2((view.x - scaled.x) / 2, Mathf.Max(8, (view.y - scaled.y) / 2));
            Apply();
        }

        void ZoomAt(float zoom, Vector2 viewPoint)
        {
            zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            var contentPoint = (viewPoint - _pan) / _zoom;
            _zoom = zoom;
            _pan = viewPoint - contentPoint * _zoom;
            Apply();
        }

        void Apply()
        {
            _content.style.translate = new Translate(_pan.x, _pan.y, 0);
            _content.style.scale = new Scale(new Vector3(_zoom, _zoom, 1));
            if (_zoomText != null) _zoomText.text = Mathf.RoundToInt(_zoom * 100) + "%";
            MarkDirtyRepaint(); // lưới nền đi theo
        }

        // Lưới chấm trên nền, di chuyển và phóng to cùng chart cho cảm giác đang kéo một tấm giấy.
        void DrawGrid(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width <= 0 || rect.height <= 0) return;
            float spacing = 24f * _zoom;
            while (spacing < 14f) spacing *= 2;
            float dot = Mathf.Clamp(1.2f * _zoom, 0.6f, 1.4f);

            var p = context.painter2D;
            p.fillColor = ChartColors.Grid;
            p.BeginPath();
            for (float x = Mod(_pan.x, spacing); x < rect.width; x += spacing)
            {
                for (float y = Mod(_pan.y, spacing); y < rect.height; y += spacing)
                {
                    p.MoveTo(new Vector2(x - dot, y - dot));
                    p.LineTo(new Vector2(x + dot, y - dot));
                    p.LineTo(new Vector2(x + dot, y + dot));
                    p.LineTo(new Vector2(x - dot, y + dot));
                    p.ClosePath();
                }
            }
            p.Fill();
        }

        static float Mod(float value, float m) => ((value % m) + m) % m;

        public void Select(string id)
        {
            if (id != null && !_views.ContainsKey(id)) id = null;
            if (id == _selected) return;
            _selected = id;
            foreach (var pair in _views) pair.Value.Selected = pair.Key == id;
            _edges.ActiveNode = id;
            _edges.MarkDirtyRepaint();
            foreach (var (route, label) in _edgeLabels)
                label.EnableInClassList("lm-edge-label--active", _edges.IsActive(route));
            SelectionChanged?.Invoke(Selected);
        }

        void OnNodeClick(NodeView view, ClickEvent e)
        {
            e.StopPropagation();
            if (_justDragged) return;
            Select(view.Node.Id);
            if (e.clickCount == 2) NodeOpened?.Invoke(view.Node);
        }

        void OnBackgroundClick(ClickEvent e)
        {
            if (_justDragged) return;
            Select(null);
        }

        void OnWheel(WheelEvent e)
        {
            if (_layout == null) return;
            float factor = e.delta.y > 0 ? 1 / 1.12f : 1.12f;
            ZoomAt(_zoom * factor, this.WorldToLocal(e.mousePosition));
            e.StopPropagation(); // không cho trang cuộn theo
        }

        void OnPointerDown(PointerDownEvent e)
        {
            if (e.button != 0 && e.button != 2) return;
            Focus(); // để phím F, +, - hoạt động
            _pointerId = e.pointerId;
            _pressPosition = this.WorldToLocal(e.position);
            _panAtPress = _pan;
            _dragging = false;
            _justDragged = false;
        }

        void OnPointerMove(PointerMoveEvent e)
        {
            if (e.pointerId != _pointerId || e.pressedButtons == 0) return;
            var delta = (Vector2)this.WorldToLocal(e.position) - _pressPosition;
            if (!_dragging)
            {
                if (delta.magnitude < DragThreshold) return;
                _dragging = true;
                this.CapturePointer(e.pointerId);
                AddToClassList("lm-chart--panning");
            }
            _pan = _panAtPress + delta;
            Apply();
        }

        void OnPointerUp(PointerUpEvent e)
        {
            if (e.pointerId != _pointerId) return;
            if (_dragging)
            {
                this.ReleasePointer(e.pointerId);
                RemoveFromClassList("lm-chart--panning");
                _justDragged = true; // click sinh ra ngay sau khi kéo thì bỏ qua
            }
            _dragging = false;
            _pointerId = -1;
        }

        void OnKeyDown(KeyDownEvent e)
        {
            switch (e.keyCode)
            {
                case KeyCode.F:
                    Fit();
                    break;
                case KeyCode.Equals:
                case KeyCode.KeypadPlus:
                    ZoomAt(_zoom * 1.25f, ViewCenter);
                    break;
                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    ZoomAt(_zoom / 1.25f, ViewCenter);
                    break;
                case KeyCode.Escape:
                    Select(null);
                    break;
                default:
                    return;
            }
            e.StopPropagation();
        }
    }
}
