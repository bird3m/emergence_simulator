using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LineGraph : Graphic
{
    public List<float> seriesA = new List<float>();
    public List<float> seriesB = new List<float>();

    public float yMin = -1f;
    public float yMax =  1f;
    public float thickness = 2f;
    public float padding = 8f;

    // Unity doesn't let us set per-vertex colors easily without extra work,
    // so we draw A using this Graphic color, and B with a second LineGraph.
    // (Simplest/cleanest)
    public bool useSeriesA = true; // if false, uses seriesB

    public void SetData(List<float> a, List<float> b)
    {
        seriesA = a;
        seriesB = b;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = rectTransform.rect;
        float w = r.width - 2f * padding;
        float h = r.height - 2f * padding;

        List<float> s = useSeriesA ? seriesA : seriesB;
        if (s == null || s.Count < 2) return;

        int n = s.Count;
        float xStep = (n <= 1) ? 0f : (w / (n - 1));

        Vector2 PrevPoint(int i)
        {
            float x = r.xMin + padding + i * xStep;
            float t = Mathf.InverseLerp(yMin, yMax, Mathf.Clamp(s[i], yMin, yMax));
            float y = r.yMin + padding + t * h;
            return new Vector2(x, y);
        }

        for (int i = 0; i < n - 1; i++)
        {
            Vector2 p0 = PrevPoint(i);
            Vector2 p1 = PrevPoint(i + 1);
            AddLineSegment(vh, p0, p1, thickness, color);
        }
    }

    private void AddLineSegment(VertexHelper vh, Vector2 p0, Vector2 p1, float t, Color c)
    {
        Vector2 dir = (p1 - p0).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x) * (t * 0.5f);

        UIVertex v = UIVertex.simpleVert;
        v.color = c;

        int idx = vh.currentVertCount;

        v.position = p0 - normal; vh.AddVert(v);
        v.position = p0 + normal; vh.AddVert(v);
        v.position = p1 + normal; vh.AddVert(v);
        v.position = p1 - normal; vh.AddVert(v);

        vh.AddTriangle(idx + 0, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 3, idx + 0);
    }
}
