using UnityEngine;
using System;
using System.Collections.Generic;

public class OrganismBehaviour : MonoBehaviour
{
   
    public float border = 10f;
    public float reachTolerance = 0.5f;
    public float speed = 0.0f;
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

    [HideInInspector] // Unity Inspector'ın bu listeyi okumaya çalışmasını engeller
    public List<PathfindingAstar.GraphNode> lastPath = new List<PathfindingAstar.GraphNode>();


    private List<Vector2> pathPoints = new List<Vector2>();
    private int pathIndex = 0;

    // Target lock
    private GameObject currentTarget = null;
    private Vector2 lastPlannedTargetPos;
    public Traits traits;
    private Vector2 lastPos;
    [Header("Sprite (fly)")]
    public Sprite flyingSprite;
    [Header("Sprite (carnivore)")]
    public Sprite carnivoreSprite;
    private Sprite originalSprite;
    private bool prevCanFly = false;
    private bool prevIsCarnivore = false;


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
        if (traits == null) 
            traits = GetComponent<Traits>();

        speed = traits.GetSpeed(traits.PowerToWeight);

            // cache original sprite and initialize sprite state (carnivore has priority)
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                originalSprite = sr.sprite;
                prevCanFly = (traits != null && traits.can_fly);
                prevIsCarnivore = (traits != null && traits.is_carnivore);

                // priority: carnivore sprite -> flying sprite -> original
                if (prevIsCarnivore && carnivoreSprite != null)
                    sr.sprite = carnivoreSprite;
                else if (prevCanFly && flyingSprite != null)
                    sr.sprite = flyingSprite;
                else if (originalSprite != null)
                    sr.sprite = originalSprite;

                if (prevCanFly && debugCosts)
                    Debug.Log(gameObject.name + ": trait can_fly = true (speed=" + speed.ToString("F2") + ")");

                if (prevIsCarnivore && debugCosts)
                    Debug.Log(gameObject.name + ": trait is_carnivore = true");
            }

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


        if (lastPath != null && lastPath.Count > 0) DebugPathCosts();

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

                // If carnivore, try to find prey first
                if (traits != null && traits.is_carnivore)
                {
                    currentTarget = FindClosestPreyInRange();

                    if (currentTarget != null && debugCosts)
                        Debug.Log(gameObject.name + ": acquired prey target " + currentTarget.name);
                }

                // fallback to sources if no prey or not carnivore
                // If carnivore: do NOT fallback to sources (only prey)
                if (currentTarget == null && (traits == null || !traits.is_carnivore))
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
            // If target is an organism and we're carnivore, convert it to resource first
            var targetTraits = currentTarget.GetComponent<Traits>();
            if (targetTraits != null && traits != null && traits.is_carnivore)
            {
                // Kill prey and convert to resource
                try
                {
                    targetTraits.DieIntoResource();
                    if (debugCosts) Debug.Log(gameObject.name + ": killed prey " + currentTarget.name);
                }
                catch (Exception)
                {
                    // ignore
                }
            }

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

        float mult = 1f;

        // Flying organisms ignore terrain slope for energy cost
        if (traits == null || !traits.can_fly)
        {
            float slope = CurrentCellSlope();
            float slope01 = Mathf.Clamp01(Mathf.Abs(slope) / Mathf.Max(0.0001f, terrain.maxAbsSlope));

            if (slope > 0f) mult = 1f + slope01 * uphillExtra;
            else if (slope < 0f) mult = 1f - slope01 * downhillDiscount;

            mult = Mathf.Max(minEnergyMultiplier, mult);
        }
            else
            {
                if (debugCosts && UnityEngine.Random.value < debugLogChance)
                    Debug.Log(gameObject.name + ": flying - ignoring slope for energy cost (mult=1)");
            }

        float effort = movedDistance * mult;

        if (traits != null)
            traits.UpdateVitals(effort, Time.deltaTime);

        // Check for can_fly / is_carnivore state changes and swap sprite accordingly
        if (traits != null)
        {
            bool nowCanFly = traits.can_fly;
            bool nowIsCarnivore = traits.is_carnivore;

            if (nowCanFly != prevCanFly || nowIsCarnivore != prevIsCarnivore)
            {
                var sr2 = GetComponent<SpriteRenderer>();
                if (sr2 != null)
                {
                    // priority: carnivore -> flying -> original
                    if (nowIsCarnivore && carnivoreSprite != null)
                    {
                        sr2.sprite = carnivoreSprite;
                        if (debugCosts) Debug.Log(gameObject.name + ": switched to carnivore sprite");
                    }
                    else if (nowCanFly && flyingSprite != null)
                    {
                        sr2.sprite = flyingSprite;
                        if (debugCosts) Debug.Log(gameObject.name + ": switched to flying sprite");
                    }
                    else
                    {
                        sr2.sprite = originalSprite;
                        if (debugCosts) Debug.Log(gameObject.name + ": reverted to original sprite");
                    }
                }

                prevCanFly = nowCanFly;
                prevIsCarnivore = nowIsCarnivore;
            }
        }
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
        PathfindingAstar.GraphNode goalNode = NodeFromWorld(destination);

        if (startNode == null || goalNode == null) return;

        // 4 ARGÜMAN GÖNDERİYORUZ: Artık hata vermeyecek
        PathfindingAstar.AStarResult result = PathfindingAstar.SolveAstar(
            startNode, 
            goalNode, 
            (n) => HeuristicForNode(n, destination),
            (from, to) => PerceivedStepCost(to) // G maliyeti için alt metodu çağırır
        );

        if (result.found && result.path != null)
        {
            LogImmediatePathAnalysis(result);
            lastPath.Clear();
            lastPath.AddRange(result.path);
            foreach (var node in result.path) pathPoints.Add(GetNodePosition(node));
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
        if (pathPoints.Count == 0 || pathIndex >= pathPoints.Count)
             return;

        Vector2 target = pathPoints[pathIndex];
        transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime); 

        if (Vector2.Distance(transform.position, target) < reachTolerance)
        {
            pathIndex++;
        }

        if (pathIndex == pathPoints.Count && currentTarget != null)
        {
            var res = currentTarget.GetComponent<resource>();
            float nut = (res != null) ? res.nutrition : 0f;

            if (spawner != null) 
                spawner.ScheduleRespawn(nut);

            Destroy(currentTarget);

            if (traits != null) traits.Eat(nut);

            currentTarget = null;
            pathPoints.Clear();
            pathIndex = 0;
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

<<<<<<< HEAD
    private GameObject FindClosestPreyInRange()
    {
        OrganismBehaviour[] organisms = GameObject.FindObjectsOfType<OrganismBehaviour>();

        OrganismBehaviour closestOb = null;
        float minDist = border;

        for (int i = 0; i < organisms.Length; i++)
        {
            OrganismBehaviour ob = organisms[i];

            if (ob == this) continue;
            if (ob.traits == null) continue;
            if (ob.traits.IsDead()) continue;
            if (ob.traits.hasBecomeCarcass) continue;

            float d = Vector2.Distance(transform.position, ob.transform.position);
            if (d < minDist)
            {
                minDist = d;
                closestOb = ob;
            }
        }

        if (closestOb != null) return closestOb.gameObject;
        return null;
    }

=======
    /// <summary>
    /// Heuristic for A* - estimates remaining cost.
    /// This includes the organism's GENETIC PREFERENCE (bias) for uphill/downhill.
    /// Good genes = better estimates = more efficient paths = more energy saved.
    /// </summary>
>>>>>>> bb9d7c5ef2c7d527b247aacb44eb9b6a8c457c78
    private uint HeuristicForNode(PathfindingAstar.GraphNode n, Vector2 destination)
    {
        ParseNodeXY(n, out int nx, out int ny);
        Vector2 np = GetNodePosition(n);
        float dist = Vector2.Distance(np, destination);

<<<<<<< HEAD

        // If organism can fly, ignore slope in heuristic (use straight distance)
        if (traits != null && traits.can_fly)
        {
            float hFly = dist * 10f * (1f + SlopeBiasFactor(0f));
            hFly = Mathf.Clamp(hFly, 1f, 100000f);

            if (debugCosts && UnityEngine.Random.value < debugLogChance)
                Debug.Log(gameObject.name + ": Heuristic - flying branch at node " + n.name + ", dist=" + dist.ToString("F2") + ", h=" + hFly.ToString("F2"));

            return (uint)Mathf.RoundToInt(hFly);
        }

        // Slope ve gene bias hesaplamalarını yap
=======
>>>>>>> bb9d7c5ef2c7d527b247aacb44eb9b6a8c457c78
        float s = terrain.GetSlope(nx, ny);
        float slopeNorm = NormalizedAbsSlope(s);
        float bias = SlopeBiasFactor(s);  // Genetic preference [-maxBias, +maxBias]

        // Base heuristic: Manhattan-like distance in cost units
        float baseH = dist * 10f;

        // Slope influence on estimate
        // Uphill is more costly, downhill is less costly
        float slopeInfluence = (s > 0f) ? (1.0f + 1.5f * slopeNorm) : (1.0f - 0.3f * slopeNorm);

        // Apply genetic bias
        // Positive bias (gene closer to +1) = organism PREFERS this slope direction
        //   -> LOWER heuristic (thinks path is cheaper than it is)
        // Negative bias (gene closer to -1) = organism AVOIDS this slope direction  
        //   -> HIGHER heuristic (thinks path is more expensive than it is)
        float biasFactor = 1.0f - bias;  // Reverse sign: positive gene = lower h

        float h = baseH * slopeInfluence * biasFactor;

        return (uint)Mathf.Clamp(h, 1f, 100000f);
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
    /// This is the REAL physical cost - no genetic bias here.
    /// The organism will pay this cost regardless of preferences.
    /// Must return >= 1 so cost is never zero.
    /// </summary>
    private uint PerceivedStepCost(PathfindingAstar.GraphNode toNode)
    {
<<<<<<< HEAD
        // If organism can fly, ignore slope and use straight distance base cost
        if (traits != null && traits.can_fly)
        {
            if (debugCosts && UnityEngine.Random.value < debugLogChance)
                Debug.Log(gameObject.name + ": PerceivedStepCost - flying at cell " + toX + "," + toY + " -> cost=10");

            return 10u; // base cost per step without slope penalty
        }

        float s = terrain.GetSlope(toX, toY);              // gerçek eğim
        float t = NormalizedAbsSlope(s);                  // 0..1 arası normalize edilmiş eğim
        float bias = SlopeBiasFactor(s);                  // genetik bias (eğim yönüne göre)
=======
        ParseNodeXY(toNode, out int tx, out int ty);
        float s = terrain.GetSlope(tx, ty);
        float t = NormalizedAbsSlope(s);
>>>>>>> bb9d7c5ef2c7d527b247aacb44eb9b6a8c457c78

        // Temel yol maliyeti
        float baseStep = 10f; 

        // Yokuş yukarı (s > 0) ise maliyet katlanır, yokuş aşağı ise azalır
        // Bu FİZİKSEL gerçek - tüm organizmalar için aynı
        float slopeMultiplier = (s > 0f) ? (1.0f + 2.0f * t) : (1.0f - 0.5f * t);
        
        // NO BIAS - This is real physical cost
        float totalPerceived = baseStep * slopeMultiplier;

        return (uint)Mathf.Clamp(totalPerceived, 1f, 100000f);
    }

    private void DebugPathCosts()
    {
        if (!debugCosts) return;
        if (lastPath == null || lastPath.Count < 2) return;

        // Random sampling - only log occasionally
        if (UnityEngine.Random.value > debugLogChance) return;

        uint totalRealCost = 0;
        float totalPhysicalSlope = 0f;
        int uphillSteps = 0;
        int downhillSteps = 0;

        for (int i = 0; i < lastPath.Count; i++)
        {
            ParseNodeXY(lastPath[i], out int x, out int y);
            float slope = terrain.GetSlope(x, y);
            totalPhysicalSlope += slope;

            if (i > 0)
            {
                uint realCost = PerceivedStepCost(lastPath[i]);
                totalRealCost += realCost;
            }

            if (slope > 0.05f) uphillSteps++;
            else if (slope < -0.05f) downhillSteps++;
        }

        float avgSlope = totalPhysicalSlope / lastPath.Count;

        Debug.Log($"<color=cyan>[PATH ANALYSIS]</color> " +
                  $"Steps:{lastPath.Count} | " +
                  $"RealCost:{totalRealCost} | " +
                  $"AvgSlope:{avgSlope:F3} | " +
                  $"Uphill:{uphillSteps} Down:{downhillSteps} | " +
                  $"Genes [U:{traits.upperSlopeHeuristic:F2} L:{traits.lowerSlopeHeuristic:F2}] | " +
                  $"Energy:{traits.currentEnergy:F1}/{traits.maxEnergy:F1}");
    }
    private void LogImmediatePathAnalysis(PathfindingAstar.AStarResult result)
    {
        // KONTROL 1: Fonksiyonun tetiklendiğini görmek için (Siyah renk)
        // Debug.Log("[Tetik] LogImmediatePathAnalysis başladı.");

        if (result.path == null) {
            Debug.LogWarning("[Hata] Result Path null geldi!");
            return;
        }

        if (result.path.Count < 2) return;

        // Verileri topluyoruz
        int count = result.path.Count;
        float totalSlope = 0f;
        uint totalCost = 0;

        for (int i = 0; i < count; i++)
        {
            var node = result.path[i];
            totalCost += PerceivedStepCost(node);
            
            ParseNodeXY(node, out int x, out int y);
            totalSlope += terrain.GetSlope(x, y);
        }

        // LORDUM, BU LOG ÇIKMAZSA UNITY BOZUKTUR:
        // Renk kodunu kaldırıp en basit haliyle yazdırıyoruz.
        string finalReport = "PATH_REPORT -> ID:" + GetHashCode() + 
                            " Nodes:" + count + 
                            " Cost:" + totalCost + 
                            " AvgSlope:" + (totalSlope / count).ToString("F3") +
                            " Genes: " + traits.upperSlopeHeuristic.ToString("F2");

        // LogType.Log kullanarak en yüksek öncelikle bastırıyoruz
        Debug.unityLogger.Log(LogType.Log, finalReport);
    }
}
