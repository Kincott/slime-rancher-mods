using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SRMarket
{
    // A small line chart drawn as a mesh: the recorded history in `color`, then the forecast in `forecastColor`,
    // with a dot on today's value. Values are scaled to the rect between their own min and max.
    internal class Sparkline : MaskableGraphic
    {
        internal Color ForecastColor = new Color(1f, 1f, 1f, 0.35f);
        internal float Thickness = 2f;
        // When > 0, the thickness is this fraction of the rect height (world-space screens have no pixel unit).
        internal float RelativeThickness;

        private readonly List<float> values = new List<float>();
        private int historyCount;

        // history: oldest to today. forecast: the following days.
        internal void SetData(IList<int> history, IList<int> forecast)
        {
            values.Clear();
            if (history != null) foreach (int v in history) values.Add(v);
            historyCount = values.Count;
            if (forecast != null) foreach (int v in forecast) values.Add(v);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (values.Count == 0) return;
            Rect r = GetPixelAdjustedRect();
            if (RelativeThickness > 0f) Thickness = r.height * RelativeThickness;

            float min = float.MaxValue, max = float.MinValue;
            foreach (float v in values)
            {
                if (v < min) min = v;
                if (v > max) max = v;
            }
            float range = max - min < 1f ? 1f : max - min;
            float pad = Thickness * 1.5f;
            int n = values.Count;

            var points = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float x = n == 1 ? r.center.x : r.xMin + pad + (r.width - 2f * pad) * i / (n - 1);
                float y = r.yMin + pad + (r.height - 2f * pad) * (values[i] - min) / range;
                points[i] = new Vector2(x, y);
            }

            for (int i = 0; i < n - 1; i++)
            {
                // Segments leading into the future are drawn in the forecast color.
                Segment(vh, points[i], points[i + 1], i + 1 >= historyCount ? ForecastColor : color);
            }

            if (historyCount > 0)
            {
                Dot(vh, points[historyCount - 1], Thickness * 1.8f, color);
            }
        }

        private void Segment(VertexHelper vh, Vector2 a, Vector2 b, Color c)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 n = new Vector2(-dir.y, dir.x) * (Thickness * 0.5f);
            Quad(vh, a - n, a + n, b + n, b - n, c);
        }

        private void Dot(VertexHelper vh, Vector2 p, float size, Color c)
        {
            float h = size * 0.5f;
            Quad(vh, p + new Vector2(-h, -h), p + new Vector2(-h, h), p + new Vector2(h, h), p + new Vector2(h, -h), c);
        }

        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c2, Vector2 d, Color c)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, c, Vector2.zero);
            vh.AddVert(b, c, Vector2.zero);
            vh.AddVert(c2, c, Vector2.zero);
            vh.AddVert(d, c, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
