using UnityEngine;
using System.Collections.Generic;
using System;


// IMPORTANT:
// If your own script is named/classed "Terrain", it conflicts with UnityEngine.Terrain.
// This script avoids that by fully qualifying UnityEngine.Terrain ONLY if needed.
// Your custom Terrain class must have: width, height, cellSize, maxAbsSlope, GetSlope(x,y), CellCenterWorld(x,y)

public class SourceSpawner2D : MonoBehaviour
{
    [Header("References")]
    public Terrain terrain;              // your custom Terrain script (grid terrain)
    public GameObject sourcePrefab;      // your source prefab

    [Header("Density")]
    [Range(0f, 1f)]
    public float density = 0.08f;        // base probability per cell

    [Header("Clustering (optional)")]
    public bool useNoise = true;
    public float noiseScale = 0.12f;     // smaller => bigger blobs, larger => more speckle
    [Range(0f, 1f)]
    public float noiseBlend = 0.75f;     // 0=ignore noise, 1=all noise

    [Header("Slope Bias (optional)")]
    [Range(-1f, 1f)]
    public float preferPitsOrPeaks = 0f; // -1 pits, +1 peaks, 0 ignore slope
    [Range(0f, 2f)]
    public float slopeInfluence = 0f;    // 0 ignore, 1 moderate, 2 strong

    [Header("Spawn Limits")]
    [Tooltip("0 = unlimited")]
    public int maxSpawn = 0;

    [Header("Determinism")]
    public bool deterministic = true;
    public int seed = 12345;

    [Header("Housekeeping")]
    public bool clearPreviousOnSpawn = true;

    private void Start()
    {
        SpawnAll();
    }

    [ContextMenu("Spawn All")]
    public void SpawnAll()
    {
        if (terrain == null)
            terrain = FindObjectOfType<Terrain>();

        if (terrain == null)
        {
            Debug.LogError("SourceSpawner2D: terrain is null.");
            return;
        }

        if (sourcePrefab == null)
        {
            Debug.LogError("SourceSpawner2D: sourcePrefab is null.");
            return;
        }

        if (clearPreviousOnSpawn)
            ClearSpawned();

        int limit = (maxSpawn <= 0) ? int.MaxValue : maxSpawn;

        // Build a list of ALL cells
        List<Vector2Int> cells = new List<Vector2Int>(terrain.width * terrain.height);
        for (int x = 0; x < terrain.width; x++)
        {
            for (int y = 0; y < terrain.height; y++)
            {
                cells.Add(new Vector2Int(x, y));
            }
        }

        // Shuffle cells so spawning is evenly distributed across the whole terrain
        Shuffle(cells, deterministic ? seed : Environment.TickCount);

        int spawned = 0;

        for (int i = 0; i < cells.Count; i++)
        {
            if (spawned >= limit)
                break;

            int x = cells[i].x;
            int y = cells[i].y;

            float p = ProbabilityAtCell(x, y);

            // roll
            float r = deterministic ? CellRandom01(x, y, seed) : UnityEngine.Random.value;

            if (r <= p)
            {
                Vector3 pos = terrain.CellCenterWorld(x, y);
                Instantiate(sourcePrefab, pos, Quaternion.identity, transform);
                spawned++;
            }
        }

        Debug.Log("Spawned: " + spawned + " / limit: " + (maxSpawn <= 0 ? "unlimited" : maxSpawn.ToString()));
    }

    private void Shuffle(List<Vector2Int> list, int s)
    {
        // Fisher-Yates shuffle
        System.Random rng = new System.Random(s);

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Vector2Int tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }


    [ContextMenu("Clear Spawned")]
    public void ClearSpawned()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    private float ProbabilityAtCell(int x, int y)
    {
        float p = density;

        // 1) Noise: blend instead of multiply-to-zero
        if (useNoise)
        {
            float n = Mathf.PerlinNoise(x * noiseScale, y * noiseScale); // 0..1
            float noiseFactor = Mathf.Lerp(1f, n, noiseBlend);           // 1..n
            p *= noiseFactor;
        }

        // 2) Slope bias (optional)
        if (slopeInfluence > 0.0001f && Mathf.Abs(preferPitsOrPeaks) > 0.0001f)
        {
            float s = terrain.GetSlope(x, y);
            float maxAbs = Mathf.Max(terrain.maxAbsSlope, 0.0001f);
            float sn = Mathf.Clamp(s / maxAbs, -1f, 1f);                 // -1..+1

            // Alignment: + when slope matches preference
            float alignment = sn * preferPitsOrPeaks;                    // -1..+1
            float mult = 1f + alignment * slopeInfluence;                // can go below 0

            if (mult < 0f) mult = 0f;
            p *= mult;
        }

        return Mathf.Clamp01(p);
    }

    // Deterministic pseudo-random in [0,1) per cell
    private float CellRandom01(int x, int y, int s)
    {
        // Simple integer hash (fast, stable)
        int h = s;
        h = h ^ (x * 374761393);
        h = h ^ (y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        h = h ^ (h >> 16);

        // Convert to 0..1
        uint uh = (uint)h;
        return (uh & 0x00FFFFFF) / 16777216f;
    }
}
