using UnityEngine;
using System.Collections.Generic;

public class SourceSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject sourcePrefab;
    public global::Terrain terrain;

    public float respawnDelay = 8f;
    public int maxTries = 200;

    [Header("Initial Spawn")]
    public bool spawnOnStart = true;
    public int initialCount = 50;
    
    [Header("Nutrition Settings")]
    public float nutrition = 10f;

    [Header("Collision")]
    public LayerMask sourceLayerMask; // only sources

    private struct Pending
    {
        public float time;
        public float nutrition;
    }

    private readonly List<Pending> pending = new List<Pending>();

    private void Awake()
    {
        if (terrain == null)
            terrain = FindObjectOfType<global::Terrain>();

      
    }

    private void Start()
    {
        // Read values from singleton if available
        if (stats_for_simulation.Instance != null)
        {
            initialCount = stats_for_simulation.Instance.resourceCount;
            nutrition = stats_for_simulation.Instance.resourceNutrition;
        }
        
        if (!spawnOnStart) return;

        for (int i = 0; i < initialCount; i++)
        {
            SpawnOne(nutrition);
        }
    }

    private void Update()
    {
        if (pending.Count == 0) return;

        float now = Time.time;

        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (now >= pending[i].time)
            {
                SpawnOne(pending[i].nutrition);
                pending.RemoveAt(i);
            }
        }
    }

    public void ScheduleRespawn(float nutrition)
    {
        Pending p;
        p.time = Time.time + respawnDelay;
        p.nutrition = nutrition;
        pending.Add(p);
    }

    private void SpawnOne(float nutrition)
    {
        if (sourcePrefab == null || terrain == null) 
            return;

        Vector3 pos;
        if (!TryGetRandomCellCenter(out pos))
            return;

        pos.z = 0f; 

        GameObject go = Instantiate(sourcePrefab, pos, Quaternion.identity);

        var r = go.GetComponent<resource>();
        if (r != null)
            r.nutrition = nutrition;


    }

    private bool TryGetRandomCellCenter(out Vector3 worldPos)
    {
        worldPos = Vector3.zero;

        int w = terrain.width;
        int h = terrain.height;

        for (int i = 0; i < maxTries; i++)
        {
            int x = Random.Range(0, w);
            int y = Random.Range(0, h);

            worldPos = terrain.CellCenterWorld(x, y);

            Collider2D hit = Physics2D.OverlapCircle(
                new Vector2(worldPos.x, worldPos.y),
                terrain.cellSize * 0.35f,
                sourceLayerMask
            );

            if (hit == null)
                return true;
        }

        return false;
    }
    
    public void ResetAllResources()
    {
        // Clear pending respawns
        pending.Clear();
        
        // Destroy all existing resources
        resource[] allResources = FindObjectsOfType<resource>();
        foreach (resource r in allResources)
        {
            if (r != null && r.gameObject != null)
            {
                Destroy(r.gameObject);
            }
        }
        
        // Spawn fresh resources
        for (int i = 0; i < initialCount; i++)
        {
            SpawnOne(nutrition);
        }
        
        Debug.Log($"Resources reset: {initialCount} new resources spawned");
    }
    
    // UI Slider metodları
    public void SetResourceCount(float value)
    {
        initialCount = Mathf.RoundToInt(value);
    }
    
    public void SetNutrition(float value)
    {
        nutrition = value;
    }
}
