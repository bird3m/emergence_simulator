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
    public bool debugCosts = false;
    public float debugLogChance = 0.02f; // %2 of frames

    private PathfindingAstar.GraphNode lastCellNode = null;
    private float accumulatedRealEffort = 0f;

    [Header("AI Tick (Performance)")]
    public float repathInterval = 0.35f;      // 0.25 - 0.75 iyi
    public float targetSearchInterval = 0.35f; // source arama aralığı
    public float thinkJitter = 0.15f;          // canlılar aynı frame düşünmesin

    private float nextRepathTime = 0f;
    private float nextSearchTime = 0f;
    
    [Header("Herding")]
    public float herdDetectionRadius = 8f;     // yakın carnivore algılama yarıçapı
    public float herdFleeDistance = 10f;      // sürü kaçış hedefi için öne taşınacak mesafe
    public float herdSpeedMultiplier = 1.5f;  // sürü halinde hız çarpanı
    public float herdDuration = 6f;           // kaç saniye sürü davranışı sürsün

    private bool isHerding = false;
    private float herdStartTime = 0f;
    private Vector2 herdTarget = Vector2.zero;
    private float baseSpeed = 0f;
    [Header("Debug Seed")]
    [Tooltip("If true, randomly mark some organisms as carnivores at Start for testing")]
    public bool seedInitialCarnivores = true;
    [Range(0f,1f)] public float initialCarnivoreFraction = 0.10f;

    [Header("Group Clustering")]
    public int herdGroupMinSize = 5;          // minimum group size to start tight clustering
    public float herdGroupRadius = 5f;        // radius to search neighbours for group (uses 'border' if you prefer)
    public float herdClusterRadius = 0.8f;    // how tight the cluster is (small = dip dibe)
    public float herdClusterInterval = 2f;    // how often cluster members pick a nearby spot

    private bool isClustered = false;
    private Vector2 clusterCenter = Vector2.zero;
    private float nextClusterMoveTime = 0f;
    [Tooltip("Write herd start/stop messages to Console when clustering occurs")]
    public bool logHerding = true;



    private void Start()
    {
        lastPos = transform.position;

        terrain = FindObjectOfType<global::Terrain>();
        if (terrain == null)
        {
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
        baseSpeed = speed;

        // Optional: seed a fraction of organisms as carnivores at start for visual testing
        if (seedInitialCarnivores && traits != null)
        {
            try
            {
                if (UnityEngine.Random.value < initialCarnivoreFraction)
                {
                    traits.is_carnivore = true;
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

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

        // -------------------------
        // HERDING (detect nearby carnivores and form group)
        // Only animals with can_herd participate
        // -------------------------
        if (traits != null && !traits.is_carnivore && traits.can_herd && !traits.IsDead())
        {
            GameObject nearbyCarn = FindNearbyCarnivore(herdDetectionRadius);

            if (nearbyCarn != null)
            {
                // compute group center of nearby non-carnivore organisms
                OrganismBehaviour[] organisms = GameObject.FindObjectsOfType<OrganismBehaviour>();
                Vector2 groupCenter = Vector2.zero;
                int groupCount = 0;

                for (int i = 0; i < organisms.Length; i++)
                {
                    OrganismBehaviour ob = organisms[i];
                    if (ob == null) continue;
                    if (ob.traits == null) continue;
                    if (ob.traits.IsDead()) continue;
                    if (ob.traits.is_carnivore) continue;
                    if (!ob.traits.can_herd) continue;

                    float d = Vector2.Distance(transform.position, ob.transform.position);
                    if (d <= herdDetectionRadius)
                    {
                        groupCenter += (Vector2)ob.transform.position;
                        groupCount++;
                    }
                }

                if (groupCount == 0) groupCenter = transform.position;
                else groupCenter /= groupCount;

                Vector2 fleeDir = groupCenter - (Vector2)nearbyCarn.transform.position;
                if (fleeDir.sqrMagnitude < 0.001f) fleeDir = UnityEngine.Random.insideUnitCircle.normalized;
                else fleeDir.Normalize();

                Vector2 herdMoveTarget = groupCenter + fleeDir * herdFleeDistance;

                // instruct each nearby non-carnivore to begin herding toward herdMoveTarget
                for (int i = 0; i < organisms.Length; i++)
                {
                    OrganismBehaviour ob = organisms[i];
                    if (ob == null) continue;
                    if (ob.traits == null) continue;
                    if (ob.traits.IsDead()) continue;
                    if (ob.traits.is_carnivore) continue;
                    if (!ob.traits.can_herd) continue;

                    float d = Vector2.Distance((Vector2)ob.transform.position, groupCenter);
                    if (d <= herdDetectionRadius)
                    {
                        ob.BeginHerding(herdMoveTarget);
                    }
                }
            }
            else
            {
                if (isHerding)
                    StopHerding();
            }
        }

        // If currently herding, stop after herdDuration
        if (isHerding && (Time.time - herdStartTime) > herdDuration)
        {
            StopHerding();
        }

        // --- Group clustering detection/maintenance ---
        // If not already clustered, detect neighbours and possibly start clustering
        if (!isClustered)
        {
            if (traits != null && traits.can_herd && !traits.is_carnivore && !traits.IsDead())
            {
                OrganismBehaviour[] organisms = GameObject.FindObjectsOfType<OrganismBehaviour>();
                Vector2 sum = Vector2.zero;
                int count = 0;

                for (int i = 0; i < organisms.Length; i++)
                {
                    var ob = organisms[i];
                    if (ob == null) continue;
                    if (ob.traits == null) continue;
                    if (!ob.traits.can_herd) continue;
                    if (ob.traits.IsDead()) continue;
                    if (ob.traits.is_carnivore) continue;

                    float d = Vector2.Distance(transform.position, ob.transform.position);
                    if (d <= herdGroupRadius)
                    {
                        sum += (Vector2)ob.transform.position;
                        count++;
                    }
                }

                if (count >= herdGroupMinSize)
                {
                    Vector2 center = (count > 0) ? (sum / count) : (Vector2)transform.position;

                    // instruct all nearby eligible organisms to begin clustering
                    if (logHerding)
                    {
                        Debug.Log("[HERD] Group clustering started at " + center + " size=" + count);
                    }
                    for (int i = 0; i < organisms.Length; i++)
                    {
                        var ob = organisms[i];
                        if (ob == null) continue;
                        if (ob.traits == null) continue;
                        if (!ob.traits.can_herd) continue;
                        if (ob.traits.IsDead()) continue;
                        if (ob.traits.is_carnivore) continue;

                        float d = Vector2.Distance(center, ob.transform.position);
                        if (d <= herdGroupRadius)
                        {
                            ob.BeginClustering(center);
                        }
                    }
                }
            }
        }
        else
        {
            // Maintain clustering: pick new close target occasionally
            if (isClustered && Time.time >= nextClusterMoveTime)
            {
                nextClusterMoveTime = Time.time + herdClusterInterval + UnityEngine.Random.Range(0f, 0.5f);
                Vector2 target = clusterCenter + UnityEngine.Random.insideUnitCircle * herdClusterRadius;
                pathPoints.Clear();
                pathPoints.Add(target);
                pathIndex = 0;
            }

            // If cluster broken (not enough neighbours), stop clustering
            if (traits == null || !traits.can_herd || traits.IsDead() || traits.is_carnivore)
            {
                StopClustering();
            }
            else
            {
                // verify cluster still has enough members
                OrganismBehaviour[] organismsCheck = GameObject.FindObjectsOfType<OrganismBehaviour>();
                int nearbyCount = 0;
                for (int i = 0; i < organismsCheck.Length; i++)
                {
                    var ob = organismsCheck[i];
                    if (ob == null) continue;
                    if (ob.traits == null) continue;
                    if (!ob.traits.can_herd) continue;
                    if (ob.traits.IsDead()) continue;
                    if (ob.traits.is_carnivore) continue;

                    float d = Vector2.Distance(clusterCenter, ob.transform.position);
                    if (d <= herdGroupRadius) nearbyCount++;
                }

                if (nearbyCount < herdGroupMinSize)
                    StopClustering();
            }
        }

        // Handle current target only if it exists (avoid NullReference)
        if (currentTarget != null)
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
                    }
                    else if (nowCanFly && flyingSprite != null)
                    {
                        sr2.sprite = flyingSprite;
                    }
                    else
                    {
                        sr2.sprite = originalSprite;
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

    private GameObject FindNearbyCarnivore(float radius)
    {
        OrganismBehaviour[] organisms = GameObject.FindObjectsOfType<OrganismBehaviour>();

        GameObject closest = null;
        float minDist = radius;

        for (int i = 0; i < organisms.Length; i++)
        {
            OrganismBehaviour ob = organisms[i];
            if (ob == null) continue;
            if (ob == this) continue;
            if (ob.traits == null) continue;
            if (!ob.traits.is_carnivore) continue;
            if (ob.traits.IsDead()) continue;

            float d = Vector2.Distance(transform.position, ob.transform.position);
            if (d < minDist)
            {
                minDist = d;
                closest = ob.gameObject;
            }
        }

        return closest;
    }

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

    // --- Herd control helpers ---
    public void BeginHerding(Vector2 target)
    {
        if (traits == null || !traits.can_herd || traits.IsDead() || traits.is_carnivore) return;

        herdTarget = target;
        isHerding = true;
        herdStartTime = Time.time;

        // temporarily increase speed
        speed = baseSpeed * herdSpeedMultiplier;

        // plan path to herd target
        CalculateAStarPath(herdTarget);
        lastPlannedTargetPos = herdTarget;
        nextRepathTime = Time.time + repathInterval + UnityEngine.Random.Range(0f, thinkJitter);
    }

    public void StopHerding()
    {
        isHerding = false;
        speed = baseSpeed;
        herdTarget = Vector2.zero;
    }

    // --- Group clustering helpers ---
    public void BeginClustering(Vector2 center)
    {
        if (traits == null || !traits.can_herd || traits.IsDead() || traits.is_carnivore) return;

        isClustered = true;
        clusterCenter = center;
        nextClusterMoveTime = Time.time + UnityEngine.Random.Range(0f, herdClusterInterval);

        // slightly reduce speed so they stay close
        speed = baseSpeed * 0.9f;

        // set a close target near center
        Vector2 target = clusterCenter + UnityEngine.Random.insideUnitCircle * herdClusterRadius;
        pathPoints.Clear();
        pathPoints.Add(target);
        pathIndex = 0;
        if (logHerding)
            Debug.Log(gameObject.name + ": BeginClustering near " + clusterCenter.ToString());
    }

    public void StopClustering()
    {
        isClustered = false;
        speed = baseSpeed;
        clusterCenter = Vector2.zero;
        if (logHerding)
            Debug.Log(gameObject.name + ": StopClustering");
    }

    private uint HeuristicForNode(PathfindingAstar.GraphNode n, Vector2 destination)
    {
        ParseNodeXY(n, out int nx, out int ny);
        Vector2 np = GetNodePosition(n);
        float dist = Vector2.Distance(np, destination);


        // If organism can fly, ignore slope in heuristic (use straight distance with lower cost)
        if (traits != null && traits.can_fly)
        {
            float hFly = dist * 5f * (1f + SlopeBiasFactor(0f));
            hFly = Mathf.Clamp(hFly, 1f, 100000f);

            if (debugCosts && UnityEngine.Random.value < debugLogChance)
                //Debug.Log(gameObject.name + ": Heuristic - flying branch at node " + n.name + ", dist=" + dist.ToString("F2") + ", h=" + hFly.ToString("F2"));

            return (uint)Mathf.RoundToInt(hFly);
        }

        // Slope hesaplaması (genetic bias disabled)
        float s = terrain.GetSlope(nx, ny);
        float slopeNorm = NormalizedAbsSlope(s);

        // Base heuristic: Manhattan-like distance in cost units
        float baseH = dist * 10f;

        // Slope influence on estimate
        // Uphill is more costly, downhill is less costly
        float slopeInfluence = (s > 0f) ? (1.0f + 1.5f * slopeNorm) : (1.0f - 0.3f * slopeNorm);

        // Heuristic genes disabled - no genetic bias applied
        float h = baseH * slopeInfluence;

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
    /// DISABLED: Heuristic genes have no effect.
    /// </summary>
    private float SlopeBiasFactor(float slope)
    {
        // Heuristic effect disabled - always return 0
        return 0f;
    }

    /// <summary>
    /// Perceived step cost in "A* units".
    /// This is the REAL physical cost - no genetic bias here.
    /// The organism will pay this cost regardless of preferences.
    /// Must return >= 1 so cost is never zero.
    /// </summary>
    private uint PerceivedStepCost(PathfindingAstar.GraphNode toNode)
    {
        ParseNodeXY(toNode, out int toX, out int toY);
        
        // If organism can fly, ignore slope and use much lower cost
        if (traits != null && traits.can_fly)
        {
            if (debugCosts && UnityEngine.Random.value < debugLogChance)
                //Debug.Log(gameObject.name + ": PerceivedStepCost - flying at cell " + toX + "," + toY + " -> cost=5");

            return 5u; // Flying costs half of base cost - much cheaper than walking
        }

        float s = terrain.GetSlope(toX, toY);              // gerçek eğim
        float t = NormalizedAbsSlope(s);                  // 0..1 arası normalize edilmiş eğim
        float bias = SlopeBiasFactor(s);                  // genetik bias (eğim yönüne göre)

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

        // (debug logs removed)
    }
}
