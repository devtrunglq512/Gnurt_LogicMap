using Gnurt.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>Trang có flow chart ở giữa và panel chi tiết + hỏi Claude bên phải.</summary>
    internal abstract class ChartPage : GnurtPage
    {
        // Phần header, footer, tên trang và khoảng đệm chiếm chỗ; chart lấy phần chiều cao còn lại.
        const float ReservedHeight = 230f;
        const float MinChartHeight = 340f;

        FlowChartView _chart;

        protected void AddChart(VisualElement root, LogicGraph graph, string emptyText)
        {
            var details = new DetailsPanel(pageId => Window.SelectPage(pageId));
            _chart = new FlowChartView(graph, emptyText);
            _chart.SelectionChanged += details.Show;
            _chart.NodeOpened += node =>
            {
                if (node.PageId != null) Window.SelectPage(node.PageId);
            };
            UpdateHeight();

            var main = GnurtUI.Element("lm-split__main", _chart);
            var side = GnurtUI.Element("lm-side", details, new AskClaudePanel());
            root.Add(GnurtUI.Element("lm-split", main, side));
        }

        public override void OnShow()
        {
            Window.rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnWindowResized);
            UpdateHeight();
        }

        public override void OnHide()
        {
            if (Window != null) Window.rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnWindowResized);
            _chart = null;
        }

        void OnWindowResized(GeometryChangedEvent _) => UpdateHeight();

        void UpdateHeight()
        {
            if (_chart == null || Window == null) return;
            _chart.style.height = Mathf.Max(MinChartHeight, Window.position.height - ReservedHeight);
        }
    }
}
