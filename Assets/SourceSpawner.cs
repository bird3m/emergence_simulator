//Manages spawning of resource objects in the terrain
using UnityEngine;
using System.Collections.Generic;

public class SourceSpawner : MonoBehaviour
{
    
    public GameObject sourcePrefab;
    public global::Terrain terrain;

    public float respawnDelay = 8f;
    public int maxTries = 200;

  
    public bool spawnOnStart = true;
    public int initialCount = 50;
    
    public float nutrition = 10f;

    public LayerMask sourceLayerMask; // only sources

    private struct Pending
    {
        public float time;
        public float nutrition;
    }

    private readonly List<Pending> pending = new List<Pending>();

  
    // Initializes terrain reference if not assigned
    private void Awake()
    {
        if (terrain == null)
            terrain = FindObjectOfType<global::Terrain>();

    }

    // Time: O(n) because we are spawning n initial resources
    // Space: O(1)
    // Reads settings and spawns initial food resources
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

    // Time: O(p) because we are checking p pending respawns
    // Space: O(1)
    // Processes scheduled resource respawns
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

    // Time: O(1) 
    // Space: O(1)
    // Schedules a resource to respawn after delay
    public void ScheduleRespawn(float nutrition)
    {
        Pending p;
        p.time = Time.time + respawnDelay;
        p.nutrition = nutrition;
        pending.Add(p);
    }

    // Time: O(1) 
    // Space: O(1)
    // Spawns one resource at random valid location
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

    // Time: O(n) , n is maxTries
    // Space: O(1)
    // Finds random empty cell position without colliding the resources
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
    
    // Time: O(n) because we are destroying n existing resources
    // Space: O(n) because FindObjectsOfType creates array
    public void ResetAllResources()
    {
    
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
    
    // Time: O(1) 
    // Space: O(1)
    public void SetResourceCount(float value)
    {
        initialCount = Mathf.RoundToInt(value);
    }
    
    // Time: O(1) 
    // Space: O(1)
    public void SetNutrition(float value)
    {
        nutrition = value;
    }
}
