using UnityEngine;
using System;
using System.Collections.Generic;

public class OrganismBehaviour : MonoBehaviour
{
   
    public float border = 10f;
    public float reachTolerance = 0.5f;
    public float speed = 5f;
    public float wanderRadius = 8f;

  [Header("Energy vs Slope")]
    public float uphillExtra = 1.5f;        
    public float downhillDiscount = 0.5f;  
    public float maxAbsSlopeForNorm = 1.0f; 
    public float minEnergyMultiplier = 0.1f; 

    public SourceSpawner spawner;

    public bool repathIfTargetMoved = true;
    public float repathMoveThreshold = 1.0f;

    private global::Terrain terrain;

    private static List<PathfindingAstar.GraphNode> allNodes;
    private static Dictionary<string, PathfindingAstar.GraphNode> nodeByName;
    private static bool graphBuilt = false;


    public List<PathfindingAstar.GraphNode> lastPath = new List<PathfindingAstar.GraphNode>();


    private List<Vector2> pathPoints = new List<Vector2>();
    private int pathIndex = 0;

    // Target lock
    private GameObject currentTarget = null;
    private Vector2 lastPlannedTargetPos;
    public Traits traits;
    private Vector2 lastPos;


    [Header("Debug")]
    public bool debugCosts = true;
    public float debugLogChance = 0.02f; // %2 of frames

    private PathfindingAstar.GraphNode lastCellNode = null;
    private float accumulatedRealEffort = 0f;

    [Header("AI Tick (Performance)")]
    public float repathInterval = 0.35f;      // 0.25 - 0.75 iyi
    public float targetSearchInterval = 0.35f; // source arama aralığı
    public float thinkJitter = 0.15f;          // canlılar aynı frame düşünmesin

    private float nextRepathTime = 0f;
    private float nextSearchTime = 0f;



    private void Start()
    {
        lastPos = transform.position;

        terrain = FindObjectOfType<global::Terrain>();
        if (terrain == null)
        {
            Debug.LogError("Terrain not found in the scene!");
            enabled = false;
            return;
        }

        // build graph ONCE
        if (!graphBuilt)
        {
            BuildSharedGraph();
            graphBuilt = true;
        }

        SetRandomWanderTarget();
        lastCellNode = NodeFromWorld((Vector2)transform.position);
        accumulatedRealEffort = 0f;

        // traits cache (sende public ama null kalabiliyor)
        if (traits == null) traits = GetComponent<Traits>();

        float j = UnityEngine.Random.Range(0f, thinkJitter);
        nextRepathTime = Time.time + j;
        nextSearchTime = Time.time + j;

    }

    private void BuildSharedGraph()
    {
        allNodes = new List<PathfindingAstar.GraphNode>(terrain.width * terrain.height);
        nodeByName = new Dictionary<string, PathfindingAstar.GraphNode>(terrain.width * terrain.height);

        // Create nodes
        for (int x = 0; x < terrain.width; x++)
        {
            for (int y = 0; y < terrain.height; y++)
            {
                var node = new PathfindingAstar.GraphNode(x + "," + y);
                allNodes.Add(node);
                nodeByName[node.name] = node;
            }
        }

        // Connect nodes (4-neighborhood)
        for (int i = 0; i < allNodes.Count; i++)
        {
            PathfindingAstar.GraphNode node = allNodes[i];
            node.links.Clear();

            string[] parts = node.name.Split(',');
            int x = int.Parse(parts[0]);
            int y = int.Parse(parts[1]);

            AddLinkIfValid(node, x + 1, y);
            AddLinkIfValid(node, x - 1, y);
            AddLinkIfValid(node, x, y + 1);
            AddLinkIfValid(node, x, y - 1);
        }
    }


    private void Update()
    {
        if (!enabled) return;
        if (allNodes == null || allNodes.Count == 0) return;

        Vector2 beforeMove = transform.position;

        // -------------------------
        // THINK (only sometimes)
        // -------------------------
        float nowTime = Time.time;

        // 1) Acquire target occasionally (NOT every frame)
        if (currentTarget == null)
        {
            if (nowTime >= nextSearchTime)
            {
                nextSearchTime = nowTime + targetSearchInterval + UnityEngine.Random.Range(0f, thinkJitter);

                currentTarget = FindClosestSourceInRange();

                if (currentTarget != null)
                {
                    // plan once when acquired
                    CalculateAStarPath(currentTarget.transform.position);
                    lastPlannedTargetPos = currentTarget.transform.position;

                    // also schedule next repath time
                    nextRepathTime = nowTime + repathInterval + UnityEngine.Random.Range(0f, thinkJitter);
                }
            }
        }
        else
        {
            // if target destroyed
            if (!currentTarget.activeInHierarchy)
            {
                currentTarget = null;
                pathPoints.Clear();
                pathIndex = 0;
            }
            else
            {
                // 2) Repath occasionally, and only if target moved enough
                if (nowTime >= nextRepathTime)
                {
                    nextRepathTime = nowTime + repathInterval + UnityEngine.Random.Range(0f, thinkJitter);

                    if (repathIfTargetMoved)
                    {
                        Vector2 tpos = currentTarget.transform.position;

                        if (Vector2.Distance(tpos, lastPlannedTargetPos) > repathMoveThreshold)
                        {
                            CalculateAStarPath(tpos);
                            lastPlannedTargetPos = tpos;
                        }
                    }
                }
            }
        }

        // -------------------------
        // ACT (every frame)
        // -------------------------
        if (currentTarget == null)
            Wander();

        FollowPath();

        // If reached end and have target: consume + clear
        if (pathIndex == pathPoints.Count && currentTarget != null)
        {
            var res = currentTarget.GetComponent<resource>();
            float nut = (res != null) ? res.nutrition : 0f;

            if (spawner != null) spawner.ScheduleRespawn(nut);

            Destroy(currentTarget);

            if (traits != null) traits.Eat(nut);

            currentTarget = null;
            pathPoints.Clear();
            pathIndex = 0;
        }

        // -------------------------
        // VITALS (every frame)
        // -------------------------
        float movedDistance = Vector2.Distance((Vector2)transform.position, beforeMove);

        float slope = CurrentCellSlope();
        float slope01 = Mathf.Clamp01(Mathf.Abs(slope) / Mathf.Max(0.0001f, terrain.maxAbsSlope));

        float mult = 1f;
        if (slope > 0f) mult = 1f + slope01 * uphillExtra;
        else if (slope < 0f) mult = 1f - slope01 * downhillDiscount;

        mult = Mathf.Max(minEnergyMultiplier, mult);

        float effort = movedDistance * mult;

        if (traits != null)
            traits.UpdateVitals(effort, Time.deltaTime);
    }



    private uint DebugPlannedPathCost()
    {
        if (lastPath == null || lastPath.Count < 2) return 0;

        uint sum = 0;

        for (int i = 1; i < lastPath.Count; i++)
        {
            ParseNodeXY(lastPath[i], out int x, out int y);
            sum += PerceivedStepCost(x, y);
        }

        return sum;
    }


    private void AddLinkIfValid(PathfindingAstar.GraphNode node, int targetX, int targetY)
    {
        if (targetX < 0 || targetX >= terrain.width || targetY < 0 || targetY >= terrain.height)
            return;

        string key = targetX + "," + targetY;

        if (nodeByName.TryGetValue(key, out PathfindingAstar.GraphNode targetNode))
        {
            // shared graph => cost must NOT depend on per-organism traits
            node.links.Add(new PathfindingAstar.Link(10, targetNode));
        }
    }

    // ---------------- PATHFINDING ----------------

    private void CalculateAStarPath(Vector2 destination)
    {
        pathPoints.Clear();
        pathIndex = 0;

        PathfindingAstar.GraphNode startNode = NodeFromWorld((Vector2)transform.position);
        PathfindingAstar.GraphNode goalNode  = NodeFromWorld(destination);

        if (startNode == null || goalNode == null)
            return;

        // Heuristic dictionary must be GraphNode -> uint for the new A*
        PathfindingAstar.AStarResult result = PathfindingAstar.SolveAstar(startNode, goalNode, (n) => HeuristicForNode(n, destination));

        if (result.found && result.path != null)
        {
            lastPath.Clear();
            lastPath.AddRange(result.path);
        }


        if (!result.found || result.path == null || result.path.Count == 0)
            return;

        // Convert node path to world points
        for (int i = 0; i < result.path.Count; i++)
        {
            pathPoints.Add(GetNodePosition(result.path[i]));
        }


    
        if (debugCosts && result.found)
        {
            uint planned = DebugPlannedPathCost();
            
        }

    }


    private PathfindingAstar.GraphNode GetNodeAtWorld(Vector2 world)
    {
        if (!terrain.WorldToCell(world, out int x, out int y))
            return null;

        // (Şimdilik string key ile, hızlı fix)
        string key = x + "," + y;

        if (nodeByName.TryGetValue(key, out var node))
            return node;

        return null;
    }


    private void FollowPath()
    {
        if (pathPoints.Count == 0 || pathIndex >= pathPoints.Count) return;

        Vector2 target = pathPoints[pathIndex];
        transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, target) < reachTolerance)
        {
            pathIndex++;
        }
        if(pathIndex == pathPoints.Count && currentTarget != null)
        {
            float nut = currentTarget.GetComponent<resource>().nutrition;
            Destroy(currentTarget);
            GetComponent<Traits>().Eat(nut);
        }
    }

    // ---------------- HELPERS ----------------

    private float CurrentCellSlope()
    {
        if (!terrain.WorldToCell((Vector2)transform.position, out int x, out int y))
            return 0f;

        return terrain.GetSlope(x, y);
    }
    private PathfindingAstar.GraphNode NodeFromWorld(Vector2 world)
    {
        if (!terrain.WorldToCell(world, out int x, out int y))
            return null;

        string key = x + "," + y;
        nodeByName.TryGetValue(key, out var node);
        return node;
    }

    private PathfindingAstar.GraphNode FindClosestNode(Vector2 pos)
    {
        PathfindingAstar.GraphNode closest = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < allNodes.Count; i++)
        {
            PathfindingAstar.GraphNode node = allNodes[i];
            float dist = Vector2.Distance(GetNodePosition(node), pos);

            if (dist < minDistance)
            {
                minDistance = dist;
                closest = node;
            }
        }

        return closest;
    }

    public Vector3 GetNodeWorldPosition(int x, int y)
    {
        return terrain.CellCenterWorld(x, y);
    }


    private Vector2 GetNodePosition(PathfindingAstar.GraphNode node)
    {
        string[] p = node.name.Split(',');
        int x = int.Parse(p[0]);
        int y = int.Parse(p[1]);

        Vector3 worldPos = terrain.CellCenterWorld(x, y);
        return new Vector2(worldPos.x, worldPos.y);
    }

    private GameObject FindClosestSourceInRange()
    {
        GameObject[] sources = GameObject.FindGameObjectsWithTag("Source");

        GameObject closest = null;
        float minDist = border;

        for (int i = 0; i < sources.Length; i++)
        {
            GameObject s = sources[i];
            float d = Vector2.Distance(transform.position, s.transform.position);

            if (d < minDist)
            {
                minDist = d;
                closest = s;
            }
        }

        return closest;
    }

    private uint HeuristicForNode(PathfindingAstar.GraphNode n, Vector2 destination)
    {
        // Node'un x, y koordinatlarını çözümle
        ParseNodeXY(n, out int nx, out int ny);

        // Gerçek mesafeyi (dünya) hesapla
        Vector2 np = GetNodePosition(n);
        float dist = Vector2.Distance(np, destination);

        // Slope ve gene bias hesaplamalarını yap
        float s = terrain.GetSlope(nx, ny);
        float x = NormalizedAbsSlope(s);          // Normalize edilmiş slope
        float bias = SlopeBiasFactor(s);         // Genetik offset

        // Heuristic hesaplama (mesafe * eğilim + bias + gerçek maliyet etkisi)
        float h = dist * 10f * (1f + ( (s > 0f) ? (0.8f * x) : (0.2f * x) )) * (1f + bias);

        // Yüksek hıristik değerlerinin penalize edilmemesi gerektiğini göz önünde bulundur
        h = Mathf.Clamp(h, 1f, 100000f);  // Heuristic değerini mantıklı bir aralıkta tut

        return (uint)Mathf.RoundToInt(h);
    }


    private void SetRandomWanderTarget()
    {
        pathPoints.Clear();

        Vector2 randomPoint = (Vector2)transform.position + UnityEngine.Random.insideUnitCircle * wanderRadius;
        pathPoints.Add(randomPoint);

        pathIndex = 0;
    }

    private void Wander()
    {
        if (pathPoints.Count == 0 || pathIndex >= pathPoints.Count)
        {
            SetRandomWanderTarget();
        }
    }

    //---Slop Calculation

    private void ParseNodeXY(PathfindingAstar.GraphNode node, out int x, out int y)
    {
        string name = node.name;
        int comma = name.IndexOf(',');
        x = int.Parse(name.Substring(0, comma));
        y = int.Parse(name.Substring(comma + 1));
    }

    private float NormalizedAbsSlope(float s)
    {
        // s is in [-maxAbsSlope, +maxAbsSlope]
        return Mathf.Clamp01(Mathf.Abs(s) / Mathf.Max(terrain.maxAbsSlope, 1e-4f));
    }

    /// <summary>
    /// Returns signed bias multiplier from traits depending on slope sign.
    /// uphill -> use upperSlopeHeuristic
    /// downhill -> use lowerSlopeHeuristic
    /// Then scale by maxHeuristicBias (e.g. 0.10 => +-10%).
    /// </summary>
    private float SlopeBiasFactor(float slope)
    {
        float gene = (slope >= 0f) ? traits.upperSlopeHeuristic : traits.lowerSlopeHeuristic;
        // gene in [-1,1], scale to [-maxHeuristicBias, +maxHeuristicBias]
        return gene * traits.maxHeuristicBias;
    }

    /// <summary>
    /// Perceived step cost in "A* units".
    /// Must return >= 1 so cost is never zero.
    /// </summary>
    private uint PerceivedStepCost(int toX, int toY)
    {
        float s = terrain.GetSlope(toX, toY);              // gerçek eğim
        float t = NormalizedAbsSlope(s);                  // 0..1 arası normalize edilmiş eğim
        float bias = SlopeBiasFactor(s);                  // genetik bias (eğim yönüne göre)

        // Adım başına harcanan enerji hesaplaması (gerçek maliyet)
        // Adım başına **gerçek maliyet**: her cell (düğüm) için tahmin edilen enerji kaybı
        // Base cost: 10 per step
        float uphillExtra = (s > 0f) ? (1.0f + 2.0f * t) : (1.0f + 0.5f * t); // yokuş tırmanma maliyeti

        // Gerçek maliyetin hesaba katılması (gerçek enerji kaybı)
        float realEnergyCost = 10f * uphillExtra * (1f + bias); // Yokuş yukarı daha pahalı

        // Gerçek maliyetin `PerceivedStepCost`'a yansıması
        realEnergyCost = Mathf.Clamp(realEnergyCost, 1f, 100000f);

        return (uint)Mathf.RoundToInt(realEnergyCost);  // Gerçek maliyet değerini döndür
    }


}
