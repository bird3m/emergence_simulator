/*
    This creates a background. nothing else
*/


using UnityEngine;

public class Terrain : MonoBehaviour
{
    [Header("Grid Size")]
    public int width = 20;
    public int height = 20;

    [Header("Cell Settings")]
    public float cellSize = 1f;

    [Header("Slope Generation")]
    [Tooltip("Slope values will be generated in range [-maxAbsSlope, +maxAbsSlope].")]
    public float maxAbsSlope = 5f;

    [Tooltip("Scale for Perlin noise - smaller values = larger terrain features")]
    public float noiseScale = 0.08f;
    
    [Tooltip("Number of peaks and valleys - higher = more dramatic terrain")]
    public float terrainFrequency = 1.5f;

    [Tooltip("If true, use seed for repeatable terrain.")]
    public bool useSeed = true;

    public int seed = 12345;

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public float arrowLength = 0.35f;
    public float arrowHeadSize = 0.12f;

    // slope[x, y] -> positive: uphill, negative: downhill, 0: flat
    private float[,] slope;

    private void Awake()
    {
        // Read terrain size from singleton if available
        if (stats_for_simulation.Instance != null)
        {
            width = stats_for_simulation.Instance.terrainSize;
            height = stats_for_simulation.Instance.terrainSize;
        }
        else 
        {
            Debug.Log("is null");
        }
        
        Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (width <= 0 || height <= 0)
        {
            // Debug log removed
            return;
        }

        slope = new float[width, height];

        if (useSeed)
        {
            Random.InitState(seed);
        }

        // Random offset for Perlin noise
        float offsetX = Random.Range(0f, 10000f);
        float offsetY = Random.Range(0f, 10000f);

        // Generate terrain using multi-octave Perlin noise for realistic hills and valleys
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float xCoord = (float)x * noiseScale * terrainFrequency + offsetX;
                float yCoord = (float)y * noiseScale * terrainFrequency + offsetY;
                
                // Multi-octave Perlin noise - creates natural terrain with multiple detail levels
                float value = 0f;
                float amplitude = 1f;
                float frequency = 1f;
                float maxValue = 0f;
                
                // 4 octaves for rich terrain detail
                for (int i = 0; i < 4; i++)
                {
                    value += Mathf.PerlinNoise(xCoord * frequency, yCoord * frequency) * amplitude;
                    maxValue += amplitude;
                    amplitude *= 0.5f;
                    frequency *= 2f;
                }
                
                // Normalize and map to slope range
                value /= maxValue;
                
                // Transform from [0,1] to [-maxAbsSlope, +maxAbsSlope]
                // Apply power curve to make peaks sharper and valleys deeper
                value = Mathf.Pow(value, 0.8f); // Slight curve for more dramatic features
                float terrainSlope = (value * 2f - 1f) * maxAbsSlope;
                
                // Add some sharp peaks and deep valleys randomly
                if (Random.value < 0.03f) // 3% chance for extreme features
                {
                    terrainSlope *= 1.8f;
                }
                
                slope[x, y] = Mathf.Clamp(terrainSlope, -maxAbsSlope, maxAbsSlope);
            }
        }

        Debug.Log($"Terrain generated with deep valleys and high peaks (slope range: {-maxAbsSlope} to {maxAbsSlope})");
    }

    /// <summary>
    /// Returns slope value at grid coordinate (x,y).
    /// Caller is responsible for providing non-negative coordinates.
    /// </summary>
    public float GetSlope(int x, int y)
    {
        if (slope == null)
        {
            // Debug log removed
            return 0f;
        }

        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            // Debug log removed
            return 0f;
        }

        return slope[x, y];
    }

    /// <summary>
    /// Converts (x,y) cell to world center position in 2D (XY plane).
    /// </summary>
    public Vector3 CellCenterWorld(int x, int y)
    {
        Vector3 origin = transform.position;
        float wx = origin.x + (x + 0.5f) * cellSize;
        float wy = origin.y + (y + 0.5f) * cellSize;
        return new Vector3(wx, wy, origin.z);
    }
    public Vector3 originWorld = Vector3.zero; // grid 0,0'ın world origin'i

    public bool WorldToCell(Vector2 world, out int x, out int y)
    {
        // Birdview XY: x->world.x, y->world.y
        float lx = world.x - originWorld.x;
        float ly = world.y - originWorld.y;

        x = Mathf.FloorToInt(lx / cellSize);
        y = Mathf.FloorToInt(ly / cellSize);

        // clamp / bounds check
        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;

        return true;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        if (width <= 0 || height <= 0) return;

        if (slope == null || slope.GetLength(0) != width || slope.GetLength(1) != height)
            return;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 c = CellCenterWorld(x, y);

                DrawCellOutline(c, cellSize);

                float s = slope[x, y];

                if (Mathf.Abs(s) < 0.0001f)
                {
                    Gizmos.color = Color.gray;
                    Gizmos.DrawSphere(c, 0.03f);
                    continue;
                }

                // DOWN for negative slope, UP for positive slope
                Vector3 dir = (s > 0f) ? Vector3.up : Vector3.down;

                // Normalize magnitude to 0..1
                float t = Mathf.Clamp01(Mathf.Abs(s) / Mathf.Max(maxAbsSlope, 0.0001f));

                // Color intensity based on magnitude (keep your nice idea)
                Gizmos.color = Color.Lerp(Color.white, Color.red, t);

                // Length increases with abs(slope)
                float len = Mathf.Lerp(0.10f, arrowLength, t); // 0.10 min so it's always visible

                DrawArrow(c, dir, len, arrowHeadSize);
            }
        }
    }


    // ---- Gizmo helpers ----

    private void DrawCellOutline(Vector3 center, float size)
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.08f);

        float half = size * 0.5f;
        Vector3 a = new Vector3(center.x - half, center.y - half, center.z);
        Vector3 b = new Vector3(center.x + half, center.y - half, center.z);
        Vector3 c = new Vector3(center.x + half, center.y + half, center.z);
        Vector3 d = new Vector3(center.x - half, center.y + half, center.z);

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);
    }

    private void DrawArrow(Vector3 start, Vector3 dir, float length, float headSize)
    {
        Vector3 end = start + dir.normalized * length;

        Gizmos.DrawLine(start, end);

        // Arrow head: two short lines at the end
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f).normalized; // perpendicular in XY plane

        Vector3 headA = end - dir.normalized * headSize + perp * headSize * 0.6f;
        Vector3 headB = end - dir.normalized * headSize - perp * headSize * 0.6f;

        Gizmos.DrawLine(end, headA);
        Gizmos.DrawLine(end, headB);
    }
}
