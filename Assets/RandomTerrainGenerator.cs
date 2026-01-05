using UnityEngine;

public class RandomTerrainGenerator : MonoBehaviour
{
    [Header("Terrain")]
    public Terrain terrain;
    public int seed = 0;

    [Header("Height Settings")]
    [Range(0.0005f, 0.02f)] public float noiseScale = 0.005f;
    [Range(0f, 1f)] public float hillStrength = 0.25f;   // overall hills
    [Range(0, 30)] public int pitCount = 8;
    [Range(0.01f, 0.3f)] public float pitRadius01 = 0.08f;  // relative to terrain size
    [Range(0f, 1f)] public float pitDepth = 0.20f;       // how deep pits cut

    [Header("Obstacles")]
    public GameObject[] obstaclePrefabs;
    [Range(0, 500)] public int obstacleCount = 150;
    [Range(0.5f, 10f)] public float obstacleMinScale = 1f;
    [Range(0.5f, 10f)] public float obstacleMaxScale = 2f;

    [Header("Placement Filters")]
    [Range(0f, 45f)] public float maxSlopeForObstacle = 25f; // degrees
    [Range(0f, 1f)] public float minHeight01ForObstacle = 0.02f; // avoid water-level if you have it

    private System.Random rng;

    private void Start()
    {
        if (terrain == null)
        {
            Debug.LogError("Assign Terrain reference.");
            return;
        }

        rng = (seed == 0) ? new System.Random() : new System.Random(seed);

        GenerateHeights();
        PlaceObstacles();
    }

    private void GenerateHeights()
    {
        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;

        float[,] heights = new float[res, res];

        // Random offsets so each seed looks different
        float offX = (float)rng.NextDouble() * 10000f;
        float offY = (float)rng.NextDouble() * 10000f;

        // 1) Base hills via Perlin noise
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float nx = offX + x * noiseScale;
                float ny = offY + y * noiseScale;

                // Perlin is [0..1]. Center it around 0.5 to get hills/valleys
                float p = Mathf.PerlinNoise(nx, ny);

                // Convert to a mild hill field
                float h = (p - 0.5f) * 2f;          // [-1..1]
                h = (h * 0.5f) + 0.5f;              // back to [0..1] but shaped

                heights[y, x] = Mathf.Clamp01(h * hillStrength);
            }
        }

        // 2) Carve pits (Gaussian craters)
        for (int i = 0; i < pitCount; i++)
        {
            float cx01 = (float)rng.NextDouble();
            float cy01 = (float)rng.NextDouble();

            int cx = Mathf.RoundToInt(cx01 * (res - 1));
            int cy = Mathf.RoundToInt(cy01 * (res - 1));

            int radius = Mathf.RoundToInt(pitRadius01 * res);
            radius = Mathf.Max(2, radius);

            CarvePit(heights, res, cx, cy, radius, pitDepth);
        }

        data.SetHeights(0, 0, heights);
    }

    private void CarvePit(float[,] heights, int res, int cx, int cy, int radius, float depth)
    {
        // Gaussian-style crater: strongest at center, fades to edges
        float r2 = radius * radius;

        int minX = Mathf.Max(0, cx - radius);
        int maxX = Mathf.Min(res - 1, cx + radius);
        int minY = Mathf.Max(0, cy - radius);
        int maxY = Mathf.Min(res - 1, cy + radius);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - cx;
                int dy = y - cy;
                float d2 = dx * dx + dy * dy;

                if (d2 > r2) continue;

                float t = d2 / r2; // 0 center -> 1 edge
                // Gaussian-ish falloff
                float falloff = Mathf.Exp(-4f * t);

                float cut = depth * falloff;
                heights[y, x] = Mathf.Clamp01(heights[y, x] - cut);
            }
        }
    }

    private void PlaceObstacles()
    {
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0 || obstacleCount <= 0)
            return;

        TerrainData data = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        Vector3 size = data.size;

        for (int i = 0; i < obstacleCount; i++)
        {
            // random world XZ
            float wx = terrainPos.x + (float)rng.NextDouble() * size.x;
            float wz = terrainPos.z + (float)rng.NextDouble() * size.z;

            float normX = (wx - terrainPos.x) / size.x;
            float normZ = (wz - terrainPos.z) / size.z;

            float h01 = data.GetInterpolatedHeight(normX, normZ) / size.y;
            if (h01 < minHeight01ForObstacle) continue;

            float slope = data.GetSteepness(normX, normZ);
            if (slope > maxSlopeForObstacle) continue;

            float wy = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + terrainPos.y;

            GameObject prefab = obstaclePrefabs[rng.Next(0, obstaclePrefabs.Length)];
            GameObject obj = Instantiate(prefab, new Vector3(wx, wy, wz), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));

            float s = Mathf.Lerp(obstacleMinScale, obstacleMaxScale, (float)rng.NextDouble());
            obj.transform.localScale = Vector3.one * s;

            // Optional: parent under terrain generator for cleanliness
            obj.transform.SetParent(transform);
        }
    }
}
