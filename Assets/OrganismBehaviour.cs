using UnityEngine;
using System;
using System.Collections.Generic;

public class OrganismBehaviour : MonoBehaviour
{
   
    public float border = 10f;
    public float reachTolerance = 0.5f;
    public float speed = 0.0f;
    public float wanderRadius = 8f;

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

    [HideInInspector] 
    public List<PathfindingAstar.GraphNode> lastPath = new List<PathfindingAstar.GraphNode>();


    private List<Vector2> pathPoints = new List<Vector2>();
    private int pathIndex = 0;

    // Target lock
    private GameObject currentTarget = null;
    private Vector2 lastPlannedTargetPos;
    public Traits traits;
    private Vector2 lastPos;
  
    public Sprite flyingSprite;
   
    public Sprite carnivoreSprite;
    private Sprite originalSprite;
    private bool prevCanFly = false;
    private bool prevIsCarnivore = false;


    
    public float repathInterval = 0.35f;      
    public float targetSearchInterval = 0.35f; 
    public float thinkJitter = 0.15f; //so organisms dont fell into the same frame. 

    private float nextRepathTime = 0f;
    private float nextSearchTime = 0f;
    
    // Cache of nearby carnivores for cautious pathing (performance optimization)
    private List<OrganismBehaviour> nearbyCarnivores = new List<OrganismBehaviour>();
    private float carnivoreCheckRadius = 20f; // Only check carnivores within this radius
    
    
    //Enable prey avoidance of carnivores (performance cost)
    public bool enableCautiousPathing = false;
    
    
    //If true, randomly mark some organisms as carnivores at Start for testing
    public bool seedInitialCarnivores = false;
    [Range(0f,1f)] public float initialCarnivoreFraction = 0.01f; // 2% carnivores at start
    public float fly_advantage;
    



    // Time: O(w * h) because building graph with w*h nodes
    // Space: O(w * h) because storing all nodes
    // Initializes organism, builds shared graph, sets initial state
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

        if (traits == null)
            traits = GetComponent<Traits>();

        speed = traits.GetSpeed(traits.PowerToWeight);

        // Set fly advantage from stats_for_simulation
        if (stats_for_simulation.Instance != null)
        {
            fly_advantage = stats_for_simulation.Instance.flyAdvantage;
        }
        else
        {
            fly_advantage = 7f; // Default value
        }

        // Reduce carnivore detection/interaction border so predators have smaller personal borders
        if (traits != null && traits.is_carnivore)
        {
            border *= 0.6f;
        }
        

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
            {}
        }

            // cache original sprite and initialize sprite state (carnivore has priority)
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                originalSprite = sr.sprite;
                prevCanFly = (traits != null && traits.can_fly);
                prevIsCarnivore = (traits != null && traits.is_carnivore);

                
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

        // Register in central registry for other systems to query (avoids FindObjectsOfType)
        try { GeneticAlgorithm.RegisterOrganism(this); } catch (Exception) { }
    }

    // Time: O(n) 
    // Space: O(1)
    // Unregisters organism from central registry when destroyed
    private void OnDestroy()
    {
        // Unregister from central registry when destroyed
        try { GeneticAlgorithm.UnregisterOrganism(this); } catch (Exception) { }
    }


    // Time: O(w * h) w is width h is height of the terrain.
    // Space: O(w * h) because we are storing all nodes and connections
    // Builds pathfinding graph from terrain grid with 4-neighborhood connections
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


    // Time: O(n) 
    // Space: O(1) 
    // Main game loop handling target acquisition, pathfinding, movement, and vitals
    private void Update()
    {

        if (!enabled) 
            return;

        if (allNodes == null || allNodes.Count == 0) 
            return;

        Vector2 beforeMove = transform.position;

        float nowTime = Time.time;

        //acquire target 
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
                // If carnivore: do NOT fallback to sources they only eat prey.
                if (currentTarget == null && (traits == null || !traits.is_carnivore))
                    currentTarget = FindClosestSourceInRange();
                
                //skip targets too close to carnivores
                if (currentTarget != null && traits != null && traits.can_cautiousPathing)
                {
                    if (IsTargetNearCarnivore(currentTarget.transform.position))
                    {
                        currentTarget = null; // Skip this target, will search again next cycle
                    }
                }

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

        if (currentTarget == null)
            Wander();

        FollowPath();

        if (pathIndex == pathPoints.Count && currentTarget != null)
        {
            bool shouldDestroy = true;
            
            // If target is an organism and we arre carnivore, convert it to carcass/resource first so we can eat
            var targetTraits = currentTarget.GetComponent<Traits>();
            if (targetTraits != null && traits != null && traits.is_carnivore)
            {
                // Kill prey and convert to carcass resource
                try
                {
                    targetTraits.currentEnergy = 0f;
                    targetTraits.hasBecomeCarcass = true;
                    targetTraits.DieIntoResource();

                    shouldDestroy = false; //let it become a carcass that can be eaten by scavengers
                }
                catch (Exception)
                {}
            }

            var res = currentTarget.GetComponent<resource>();
            float nut = (res != null) ? res.nutrition : 0f;

            if (spawner != null) spawner.ScheduleRespawn(nut);

            if (shouldDestroy)
            {
                Destroy(currentTarget);
            }

            if (traits != null) traits.Eat(nut);

            currentTarget = null;
            pathPoints.Clear();
            pathIndex = 0;
        }

       
        float movedDistance = Vector2.Distance((Vector2)transform.position, beforeMove);
        

        float mult = 1f;

        // Flying has energy cost but cheaper than walking
        if (traits != null && traits.can_fly)
        {
            mult = 0.5f; // Flying costs less than going on foot.
        }
        else
        {
            mult = 1f;
        }

        float effort = movedDistance * mult;

        if (traits != null)
            traits.UpdateVitals(effort, Time.deltaTime);

        // Check for can_fly and is_carnivore state changes so we can swap the sprites accordingly
        if (traits != null)
        {
            bool nowCanFly = traits.can_fly;
            bool nowIsCarnivore = traits.is_carnivore;

            if (nowCanFly != prevCanFly || nowIsCarnivore != prevIsCarnivore)
            {
                var sr2 = GetComponent<SpriteRenderer>();
                if (sr2 != null)
                {
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



    // Time: O(1), we are using dictionary lookup 
    // Space: O(1) 
    // Adds graph edge between adjacent terrain cells if valid
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


    // Time: O((V + E) * log V) because A* with heap operations
    // Space: O(V) because storing path and open/closed sets
    // Calculates shortest path from current position to destination using A* algorithm
    private void CalculateAStarPath(Vector2 destination)
    {
        pathPoints.Clear();
        pathIndex = 0;

        PathfindingAstar.GraphNode startNode = NodeFromWorld((Vector2)transform.position);
        PathfindingAstar.GraphNode goalNode = NodeFromWorld(destination);

        if (startNode == null || goalNode == null) return;


        PathfindingAstar.AStarResult result = PathfindingAstar.SolveAstar(
            startNode, 
            goalNode, 
            (n) => HeuristicForNode(n, destination),
            (from, to) => PerceivedStepCost(to) 
        );

        if (result.found && result.path != null)
        {
            lastPath.Clear();
            lastPath.AddRange(result.path);
            foreach (var node in result.path) 
                pathPoints.Add(GetNodePosition(node));
        }
    }


    // Time: O(1) because dictionary lookup
    // Space: O(1)
    // Converts world position to graph node using dictionary lookup
    private PathfindingAstar.GraphNode GetNodeAtWorld(Vector2 world)
    {
        if (!terrain.WorldToCell(world, out int x, out int y))
            return null;

        string key = x + "," + y;

        if (nodeByName.TryGetValue(key, out var node))
            return node;

        return null;
    }


   // Time: O(1) 
   // Space: O(1)
   // Moves organism along calculated path and handles target consumption
   private void FollowPath()
    {
        if (pathPoints.Count == 0 || pathIndex >= pathPoints.Count)
             return;

        Vector2 target = pathPoints[pathIndex];
        transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);
        
 
        ClampPositionToMap();

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

    // Time: O(1) 
    // Space: O(1)
    // Keeps organism position within terrain boundaries with padding
    private void ClampPositionToMap()
    {
        if (terrain == null) 
            return;
        
      
        Vector3 minCorner = terrain.CellCenterWorld(0, 0);
        Vector3 maxCorner = terrain.CellCenterWorld(terrain.width - 1, terrain.height - 1);
        
      
        float padding = terrain.cellSize * 0.5f;
        
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minCorner.x - padding, maxCorner.x + padding);
        pos.y = Mathf.Clamp(pos.y, minCorner.y - padding, maxCorner.y + padding);
        transform.position = pos;
    }


    //Helper functions 
    // Time: O(1) 
    // Space: O(1)
    // Returns slope value of terrain cell at current position
    private float CurrentCellSlope()
    {
        if (!terrain.WorldToCell((Vector2)transform.position, out int x, out int y))
            return 0f;

        return terrain.GetSlope(x, y);
    }
    // Time: O(1) because of dictionary lookup
    // Space: O(1)
    private PathfindingAstar.GraphNode NodeFromWorld(Vector2 world)
    {
        if (!terrain.WorldToCell(world, out int x, out int y))
            return null;

        string key = x + "," + y;
        nodeByName.TryGetValue(key, out var node);
        return node;
    }

    // Time: O(n) because we are checking all nodes
    // Space: O(1)
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

    // Time: O(1) 
    // Space: O(1)
    public Vector3 GetNodeWorldPosition(int x, int y)
    {
        return terrain.CellCenterWorld(x, y);
    }


    // Time: O(1) 
    // Space: O(1)
    private Vector2 GetNodePosition(PathfindingAstar.GraphNode node)
    {
        string[] p = node.name.Split(',');
        int x = int.Parse(p[0]);
        int y = int.Parse(p[1]);

        Vector3 worldPos = terrain.CellCenterWorld(x, y);
        return new Vector2(worldPos.x, worldPos.y);
    }

    // Time: O(n) because we are checking all sources
    // Space: O(n) because we are creating sources array
    // Finds closest food source within detection range
    private GameObject FindClosestSourceInRange()
    {
        GameObject[] sources = SourceManager.I.sources.ConvertAll(r => r.gameObject).ToArray();

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
    

    // Time: O(n) because checking all organisms
    // Space: O(1)
    // Finds closest prey organism for carnivores within detection range
    private GameObject FindClosestPreyInRange()
    {
        var organisms = GeneticAlgorithm.Organisms;

        OrganismBehaviour closestOb = null;
        float minDist = border;

        for (int i = 0; i < organisms.Count; i++)
        {
            var ob = organisms[i];
            if (ob == null) continue;

            if (ob == this) continue;
            if (ob.traits == null) continue;
            if (ob.traits.IsDead()) continue;
            if (ob.traits.hasBecomeCarcass) continue;
            
            //Carnivores cannot eat other carnivores
            if (ob.traits.is_carnivore)
                continue; // Skip carnivore prey

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

    

    // Time: O(1) 
    // Space: O(1)
    // Calculates A* heuristic cost estimate from node to destination
    private uint HeuristicForNode(PathfindingAstar.GraphNode n, Vector2 destination)
    {
        ParseNodeXY(n, out int nx, out int ny);
        Vector2 np = GetNodePosition(n);
        float dist = Vector2.Distance(np, destination);


        // If organism can fly, ignore slope in heuristic (use straight distance with lower cost)
        if (traits != null && traits.can_fly)
        {
            float hFly = dist * fly_advantage;
            hFly = Mathf.Clamp(hFly, 1f, 100000f);

            return (uint)Mathf.RoundToInt(hFly);
        }

        //Manhattan distance in cost units
        float baseH = dist * 10f;

        float h = baseH;

        return (uint)Mathf.Clamp(h, 1f, 100000f);
    }


    // Time: O(1)  
    // Space: O(1)
    // Sets random wander destination within radius and map bounds
    private void SetRandomWanderTarget()
    {
        pathPoints.Clear();

    
        Vector2 currentPos = transform.position;
        Vector2 randomPoint = currentPos + UnityEngine.Random.insideUnitCircle * wanderRadius;
        
        if (terrain != null)
        {
            Vector3 minCorner = terrain.CellCenterWorld(0, 0);
            Vector3 maxCorner = terrain.CellCenterWorld(terrain.width - 1, terrain.height - 1);
            float padding = terrain.cellSize * 0.5f;
            
            randomPoint.x = Mathf.Clamp(randomPoint.x, minCorner.x - padding, maxCorner.x + padding);
            randomPoint.y = Mathf.Clamp(randomPoint.y, minCorner.y - padding, maxCorner.y + padding);
        }
        
        pathPoints.Add(randomPoint);

        pathIndex = 0;
    }

    // Time: O(1) 
    // Space: O(1)
    // Initiates wandering behavior when path is complete or empty
    private void Wander()
    {
        if (pathPoints.Count == 0 || pathIndex >= pathPoints.Count)
        {
            SetRandomWanderTarget();
        }
    }

    // Time: O(n) because we are checking all organisms
    // Space: O(1)
    // Checks if target position is too close to any carnivore for cautious pathing
    private bool IsTargetNearCarnivore(Vector2 targetPos)
    {
        List<OrganismBehaviour> allOrganisms = GeneticAlgorithm.Organisms;
        if (allOrganisms == null) 
            return false;
        
        float avoidanceDistance = border * 1.5f; // Stay away from targets near carnivores
        float avoidanceDistanceSquared = avoidanceDistance * avoidanceDistance;
        
        for (int i = 0; i < allOrganisms.Count; i++)
        {
            var organism = allOrganisms[i];
            if (organism == null || organism == this) 
                continue;
            if (organism.traits == null || !organism.traits.is_carnivore) 
                continue;
            
            float distanceSquared = (targetPos - (Vector2)organism.transform.position).sqrMagnitude;
            if (distanceSquared < avoidanceDistanceSquared)
            {
                return true; // Target is too close to a carnivore
            }
        }
        
        return false;
    }


    // Time: O(1) 
    // Space: O(1)
    // Extracts x and y coordinates from node name string
    private void ParseNodeXY(PathfindingAstar.GraphNode node, out int x, out int y)
    {
        string name = node.name;
        int comma = name.IndexOf(',');
        x = int.Parse(name.Substring(0, comma));
        y = int.Parse(name.Substring(comma + 1));
    }

    // Time: O(1) 
    // Space: O(1)
    // Normalizes absolute slope value to 0-1 range
    private float NormalizedAbsSlope(float s)
    {
        // s is in [-maxAbsSlope, +maxAbsSlope]
        return Mathf.Clamp01(Mathf.Abs(s) / Mathf.Max(terrain.maxAbsSlope, 1e-4f));
    }


    // Time: O(n) because checking all carnivores for cautious pathing
    // Space: O(1)
    // Calculates movement cost for A* considering flying and carnivore avoidance
    private uint PerceivedStepCost(PathfindingAstar.GraphNode toNode)
    {
        ParseNodeXY(toNode, out int toX, out int toY);
        
        // If organism can fly, ignore slope and use lower cost
        if (traits != null && traits.can_fly)
        {
            return 7u; 
        }


        float baseStep = 10f; 

        float slopeMultiplier = 1.0f;
        
        //Carnivores are treated as obstacles
        float cautiousCostMultiplier = 1.0f;
        float carnivoreAvoidanceCost = 0f;
        
        if (traits != null && traits.can_cautiousPathing)
        {
            //Cautious organisms use 70% less energy for pathfinding
            cautiousCostMultiplier = 0.3f;
            
            //higher cost near carnivores
            Vector2 cellPos = GetNodePosition(toNode);
            float minCarnivoreDistSq = float.MaxValue;
            
            if (GeneticAlgorithm.Organisms != null)
            {
                foreach (var org in GeneticAlgorithm.Organisms)
                {
                    if (org != null && org != this && org.traits != null && org.traits.is_carnivore)
                    {
                        float distSq = ((Vector2)org.transform.position - cellPos).sqrMagnitude;
                        if (distSq < minCarnivoreDistSq)
                            minCarnivoreDistSq = distSq;
                    }
                }
            }
            
            float dangerRadius = border * 2.5f; // Larger avoidance radius
            if (minCarnivoreDistSq < dangerRadius * dangerRadius)
            {
                //calculates the ditance from the carnivore
                float distFromDanger = Mathf.Sqrt(minCarnivoreDistSq);
                float dangerFactor = 1.0f - Mathf.Clamp01(distFromDanger / dangerRadius);
                
                // Very high cost multiplier near carnivores (forces path to go around)
                carnivoreAvoidanceCost = baseStep * dangerFactor * 5.0f; 
            }
        }
        
        float totalPerceived = (baseStep * slopeMultiplier * cautiousCostMultiplier) + carnivoreAvoidanceCost;

        return (uint)Mathf.Clamp(totalPerceived, 1f, 100000f);
    }
}
