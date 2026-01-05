using UnityEngine;

public class SourceSpawner : MonoBehaviour
{
    [Header("References")]
    public Terrain terrain;      // your terrain script
    public GameObject sourcePrefab;           // assign your "source" prefab

    [Header("Density")]
    [Range(0f, 1f)]
    public float baseDensity = 0.08f;         // overall probability per cell

    [Tooltip("If true, uses Perlin noise to create clusters.")]
    public bool useNoise = true;

    [Tooltip("Bigger -> larger clusters. Smaller -> more frequent variation.")]
    public float noiseScale = 0.12f;

    [Tooltip("How much noise affects density (0=no effect, 1=full effect).")]
    [Range(0f, 1f)]
    public float noiseStrength = 0.7f;

    [Header("Slope Bias (optional)")]
    [Tooltip("If >0, prefer peaks (positive slopes). If <0, prefer pits (negative slopes). 0 = ignore slope.")]
    [Range(-1f, 1f)]
    public float preferPitsOrPeaks = -0.3f;

    [Tooltip("How strongly slope influences density.")]
    [Range(0f, 2f)]
    public float slopeInfluence = 0.8f;

    [Header("Spawn Limits")]
    public int maxSpawn = 200;
    public bool allowMultiplePerCell = false;

    [Header("Determinism")]
    public bool useSeed = true;
    public int seed = 999;

    private void Start()
    {
        SpawnAll();
    }

    [ContextMenu("Spawn All")]
    public void SpawnAll()
    {
        if (terrain == null) terrain = FindObjectOfType<Terrain>();
        if (terrain == null)
        {
            Debug.LogError("SourceSpawner2D: terrain not set.");
            return;
        }

        if (sourcePrefab == null)
        {
            Debug.LogError("SourceSpawner2D: sourcePrefab not set.");
            return;
        }

        if (useSeed)
            Random.InitState(seed);

        int spawned = 0;

        for (int x = 0; x < terrain.width; x++)
        {
            for (int y = 0; y < terrain.height; y++)
            {
                if (spawned >= maxSpawn)
                    return;

                if (!allowMultiplePerCell && CellAlreadyHasSource(x, y))
                    continue;

                float p = DensityAtCell(x, y);

                // roll
                float r = Random.value;
                if (r <= p)
                {
                    Vector3 pos = terrain.CellCenterWorld(x, y);
                    Instantiate(sourcePrefab, pos, Quaternion.identity, transform);
                    spawned++;
                }
            }
        }
    }

    private float DensityAtCell(int x, int y)
    {
        float p = baseDensity;

        // 1) Noise clustering
        if (useNoise)
        {
            float n = Mathf.PerlinNoise(x * noiseScale, y * noiseScale); // 0..1
            // mix base density with noise
            p *= Mathf.Lerp(1f, n, noiseStrength);
        }

        // 2) Slope bias (optional)
        if (Mathf.Abs(preferPitsOrPeaks) > 0.0001f || slopeInfluence > 0.0001f)
        {
            float s = terrain.GetSlope(x, y); // can be negative or positive
            float maxAbs = Mathf.Max(terrain.maxAbsSlope, 0.0001f);

            // Normalize slope to -1..+1
            float sn = Mathf.Clamp(s / maxAbs, -1f, 1f);

            // preferPitsOrPeaks:
            //  -1 => pits, +1 => peaks
            // Map preference into a multiplier:
            // If prefer=-1 and sn=-1 => boost
            // If prefer=-1 and sn=+1 => reduce
            float alignment = sn * preferPitsOrPeaks; // -1..+1
            float mult = 1f + alignment * slopeInfluence;

            // keep sane
            if (mult < 0f) mult = 0f;

            p *= mult;
        }

        // clamp to probability
        return Mathf.Clamp01(p);
    }

    private bool CellAlreadyHasSource(int x, int y)
    {
        // Simple check: if you spawn all sources as children of this spawner,
        // we can approximate by distance to cell center.
        Vector3 center = terrain.CellCenterWorld(x, y);
        float eps = terrain.cellSize * 0.2f;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform t = transform.GetChild(i);
            if ((t.position - center).sqrMagnitude <= eps * eps)
                return true;
        }

        return false;
    }
}
