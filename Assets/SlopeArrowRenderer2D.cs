using UnityEngine;

public class SlopeArrowRenderer2D : MonoBehaviour
{
    public Terrain terrain;

    public float minArrowLength = 0.10f;   // when slope is tiny but not zero
    public float maxArrowLength = 0.50f;   // max length at abs(slope)==maxAbsSlope
    public float lineWidth = 0.05f;
    public float headSize = 0.12f;         // base head size (will be clamped by length)
    public float zOffset = 0f;

    public string sortingLayerName = "Default";
    public int sortingOrder = 0;

    public bool updateEveryFrame = false;

    public float flatEpsilon = 0.0001f;

    private LineRenderer[,] lines;
    private Material lineMaterial;

   private void Awake()
{
    if (terrain == null)
        terrain = GetComponent<Terrain>();

    Shader sh = Shader.Find("Sprites/Default");
    lineMaterial = new Material(sh);
}

private void Start()
{
    if (terrain == null)
    {
        // Debug log removed
        return;
    }

    // Terrain Awake should have generated slopes by now
    BuildOrRebuild();
    UpdateAllArrows();
}

    private void Update()
    {
        if (updateEveryFrame)
            UpdateAllArrows();
    }

    [ContextMenu("Build/Rebuild Arrows")]
    public void BuildOrRebuild()
    {
        if (terrain == null)
        {
            // Debug log removed
            return;
        }

        // Destroy old arrows (children)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        lines = new LineRenderer[terrain.width, terrain.height];

        for (int x = 0; x < terrain.width; x++)
        {
            for (int y = 0; y < terrain.height; y++)
            {
                GameObject go = new GameObject("Arrow_" + x + "_" + y);
                go.transform.SetParent(transform, false);

                LineRenderer lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.material = lineMaterial;
                lr.positionCount = 5; // start -> end -> headA -> end -> headB
                lr.startWidth = lineWidth;
                lr.endWidth = lineWidth;
                lr.numCapVertices = 2; // smoother line ends

                // Sorting for 2D
                lr.sortingLayerName = sortingLayerName;
                lr.sortingOrder = sortingOrder;

                lines[x, y] = lr;
            }
        }
    }

    [ContextMenu("Update Arrows")]
    public void UpdateAllArrows()
    {
        if (terrain == null || lines == null)
            return;

        float maxAbs = terrain.maxAbsSlope;
        if (maxAbs <= 0.000001f) maxAbs = 1f;

        for (int x = 0; x < terrain.width; x++)
        {
            for (int y = 0; y < terrain.height; y++)
            {
                float s = terrain.GetSlope(x, y);
                LineRenderer lr = lines[x, y];

                if (Mathf.Abs(s) < flatEpsilon)
                {
                    lr.enabled = false;
                    continue;
                }

                lr.enabled = true;

                // Down for negative slope, up for positive slope
                Vector3 dir = (s > 0f) ? Vector3.up : Vector3.down;

                // 0..1 based on abs(slope)
                float t = Mathf.Clamp01(Mathf.Abs(s) / maxAbs);

                // Length increases with abs(slope)
                float len = Mathf.Lerp(minArrowLength, maxArrowLength, t);

                // Color intensity based on abs(slope) (your great idea)
                Color col = Color.Lerp(Color.white, Color.red, t);
                lr.startColor = col;
                lr.endColor = col;

                // Compute geometry in XY plane
                Vector3 start = terrain.CellCenterWorld(x, y);
                start.z += zOffset;

                Vector3 end = start + dir * len;

                // Arrow head size clamped so it doesn't exceed arrow length
                float hs = headSize;
                if (hs > len * 0.7f) hs = len * 0.7f;

                // Perpendicular in XY
                Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

                Vector3 headA = end - dir * hs + perp * (hs * 0.6f);
                Vector3 headB = end - dir * hs - perp * (hs * 0.6f);

                lr.SetPosition(0, start);
                lr.SetPosition(1, end);
                lr.SetPosition(2, headA);
                lr.SetPosition(3, end);
                lr.SetPosition(4, headB);
            }
        }
    }
}
