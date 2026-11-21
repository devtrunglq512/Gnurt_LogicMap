using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gnurt.LogicMap
{
    /// <summary>Lớp vẽ đường nối: góc bo tròn, mũi tên ở đầu, nét đứt cho quan hệ "chứa" và đích chưa rõ.</summary>
    internal sealed class EdgeLayer : VisualElement
    {
        const float ArrowLength = 8f;
        const float ArrowHalfWidth = 4.5f;
        const float CornerRadius = 6f;

        public readonly List<EdgeRoute> Routes = new List<EdgeRoute>();

        /// <summary>Ô đang chọn: đường nối của nó được tô đậm và vẽ đè lên trên.</summary>
        public string ActiveNode;

        public EdgeLayer()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("lm-chart__edges");
            generateVisualContent += Draw;
        }

        public bool IsActive(EdgeRoute route) =>
            ActiveNode != null && (route.Edge.From == ActiveNode || route.Edge.To == ActiveNode);

        void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Butt;
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var route in Routes)
                {
                    bool active = IsActive(route);
                    if (active != (pass == 1)) continue;
                    var color = active ? ChartColors.EdgeActive : ChartColors.Edge;
                    if (route.IsBack && !active) color.a *= 0.55f; // đường quay về vẽ nhạt để luồng chính nổi lên
                    DrawRoute(p, route.Points, route.Edge.Style == EdgeStyle.Dashed, color, active ? 2f : 1.3f);
                }
            }
        }

        static void DrawRoute(Painter2D p, List<Vector2> points, bool dashed, Color color, float width)
        {
            if (points.Count < 2) return;
            var tip = points[points.Count - 1];
            var direction = (tip - points[points.Count - 2]).normalized;
            var arrowBase = tip - direction * ArrowLength;
            var normal = new Vector2(-direction.y, direction.x) * ArrowHalfWidth;

            // Đường dừng ở chân mũi tên để nét không lòi ra trước đầu mũi tên.
            var line = new List<Vector2>(points);
            if ((tip - points[points.Count - 2]).magnitude > ArrowLength) line[line.Count - 1] = arrowBase;

            p.strokeColor = color;
            p.lineWidth = width;
            if (dashed) DrawDashed(p, line);
            else DrawRounded(p, line);

            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(tip);
            p.LineTo(arrowBase + normal);
            p.LineTo(arrowBase - normal);
            p.ClosePath();
            p.Fill();
        }

        static void DrawRounded(Painter2D p, List<Vector2> points)
        {
            p.BeginPath();
            p.MoveTo(points[0]);
            for (int i = 1; i < points.Count - 1; i++)
            {
                float radius = Mathf.Min(CornerRadius,
                    (points[i] - points[i - 1]).magnitude / 2,
                    (points[i + 1] - points[i]).magnitude / 2);
                if (radius < 0.5f) p.LineTo(points[i]);
                else p.ArcTo(points[i], points[i + 1], radius);
            }
            p.LineTo(points[points.Count - 1]);
            p.Stroke();
        }

        static void DrawDashed(Painter2D p, List<Vector2> points)
        {
            const float dash = 5f, gap = 4f;
            bool on = true;
            float used = 0; // phần đã đi của nét/khoảng hiện tại
            p.BeginPath();
            for (int i = 0; i < points.Count - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                float length = (b - a).magnitude;
                if (length < 0.01f) continue;
                var direction = (b - a) / length;
                float t = 0;
                while (t < length)
                {
                    float limit = on ? dash : gap;
                    float step = Mathf.Min(limit - used, length - t);
                    if (on)
                    {
                        p.MoveTo(a + direction * t);
                        p.LineTo(a + direction * (t + step));
                    }
                    t += step;
                    used += step;
                    if (used >= limit - 0.001f)
                    {
                        on = !on;
                        used = 0;
                    }
                }
            }
            p.Stroke();
        }
    }
}
