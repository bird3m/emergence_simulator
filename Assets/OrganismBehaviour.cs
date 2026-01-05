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
  


   

    public bool repathIfTargetMoved = true;
    public float repathMoveThreshold = 1.0f;

    private global::Terrain terrain;

    private List<PathfindingAstar.GraphNode> allNodes = new List<PathfindingAstar.GraphNode>();
    private Dictionary<string, PathfindingAstar.GraphNode> nodeByName = new Dictionary<string, PathfindingAstar.GraphNode>();

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

        InitializeGrid();
        SetRandomWanderTarget();
        lastCellNode = FindClosestNode((Vector2)transform.position);
        accumulatedRealEffort = 0f;
    }

    private void Update()
    {
        if (allNodes.Count == 0) return;
        Vector2 beforeMove = transform.position;

        // Acquire target ONLY if none
        if (currentTarget == null)
        {
            currentTarget = FindClosestSourceInRange();
            if (currentTarget != null)
            {
                CalculateAStarPath(currentTarget.transform.position);
                lastPlannedTargetPos = currentTarget.transform.position;
            }
        }
        else
        {
            // If target got destroyed / disabled
            if (!currentTarget.activeInHierarchy)
            {
                currentTarget = null;
                pathPoints.Clear();
                pathIndex = 0;
            }
            else if (repathIfTargetMoved)
            {
                Vector2 now = currentTarget.transform.position;
                if (Vector2.Distance(now, lastPlannedTargetPos) > repathMoveThreshold)
                {
                    CalculateAStarPath(now);
                    lastPlannedTargetPos = now;
                }
            }
        }

        // If no target: wander
        if (currentTarget == null)
        {
            Wander();
        }

        FollowPath();

        // Fallback: if path ended but still have target, go direct
        if (pathIndex == pathPoints.Count && currentTarget != null)
        {
            var res = currentTarget.GetComponent<resource>();
            float nut = (res != null) ? res.nutrition : 0f;

            // respawn schedule
            SourceSpawner spwn = FindObjectOfType<SourceSpawner>();
            if (spwn != null)
                spwn.ScheduleRespawn(nut);

            Destroy(currentTarget);

            if (traits != null)
                traits.Eat(nut);

            currentTarget = null;
        }

        float movedDistance = Vector2.Distance((Vector2)transform.position, beforeMove);

        float slope = CurrentCellSlope(); // + uphill, - downhill
        float slope01 = Mathf.Clamp01(Mathf.Abs(slope) / Mathf.Max(0.0001f, terrain.maxAbsSlope));


        float mult = 1f;
        if (slope > 0f)
        {
            mult = 1f + slope01 * uphillExtra; 
        }
        else if (slope < 0f)
        {
            mult = 1f - slope01 * downhillDiscount; 
        }

        mult = Mathf.Max(minEnergyMultiplier, mult);

        float effort = movedDistance * mult;
        traits.UpdateVitals(effort, Time.deltaTime);

        accumulatedRealEffort += effort;

        PathfindingAstar.GraphNode currentCell = FindClosestNode((Vector2)transform.position);

        if (currentCell != null && lastCellNode != null && currentCell != lastCellNode)
        {
            // We entered a new cell: compare REAL vs PERCEIVED for this step
            ParseNodeXY(currentCell, out int cx, out int cy);

            uint perceivedStepCost = PerceivedStepCost(cx, cy);
            float realStepCost = accumulatedRealEffort;

            float ratio = (perceivedStepCost > 0) ? (realStepCost / perceivedStepCost) : 0f;

            if (debugCosts)
            {
                float s = terrain.GetSlope(cx, cy);
                Debug.Log(
                    $"[STEP DEBUG] {lastCellNode.name} -> {currentCell.name} " +
                    $"slope={s:F2} | PERCEIVED={perceivedStepCost} | REAL={realStepCost:F2} | real/perceived={ratio:F3}"
                );
            }

            // reset for next cell transition
            accumulatedRealEffort = 0f;
            lastCellNode = currentCell;
        }
        else if (currentCell != null && lastCellNode == null)
        {
            lastCellNode = currentCell;
            accumulatedRealEffort = 0f;
        }

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

    // ---------------- GRID SETUP ----------------

    private void InitializeGrid()
    {
        allNodes.Clear();
        nodeByName.Clear();

        for (int x = 0; x < terrain.width; x++)
        {
            for (int y = 0; y < terrain.height; y++)
            {
                // IMPORTANT: your GraphNode in PathfindingAstar has a constructor GraphNode(string name)
                var node = new PathfindingAstar.GraphNode(x + "," + y);

                allNodes.Add(node);
                nodeByName[node.name] = node;
            }
        }

        ConnectNodes();
    }

    private void ConnectNodes()
    {
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

    private void AddLinkIfValid(PathfindingAstar.GraphNode node, int targetX, int targetY)
    {
        if (targetX < 0 || targetX >= terrain.width || targetY < 0 || targetY >= terrain.height)
            return;

        string key = targetX + "," + targetY;

        if (nodeByName.TryGetValue(key, out PathfindingAstar.GraphNode targetNode))
        {
            uint cost = PerceivedStepCost(targetX, targetY);
            node.links.Add(new PathfindingAstar.Link(cost, targetNode));
        }
    }

    // ---------------- PATHFINDING ----------------

    private void CalculateAStarPath(Vector2 destination)
    {
        pathPoints.Clear();
        pathIndex = 0;

        PathfindingAstar.GraphNode startNode = FindClosestNode((Vector2)transform.position);
        PathfindingAstar.GraphNode goalNode = FindClosestNode(destination);

        if (startNode == null || goalNode == null)
            return;

        // Heuristic dictionary must be GraphNode -> uint for the new A*
        Dictionary<PathfindingAstar.GraphNode, uint> heuristic =
            new Dictionary<PathfindingAstar.GraphNode, uint>(allNodes.Count);

        for (int i = 0; i < allNodes.Count; i++)
        {
            PathfindingAstar.GraphNode n = allNodes[i];

            ParseNodeXY(n, out int nx, out int ny);

            // base distance (in cells)
            Vector2 np = GetNodePosition(n);
            float dist = Vector2.Distance(np, destination); // world distance, OK

            // use slope at node as a cheap proxy for what's ahead
            float s = terrain.GetSlope(nx, ny);
            float x = NormalizedAbsSlope(s);
            float bias = SlopeBiasFactor(s);

            // If on uphill cell and organism has good bias, h becomes larger (more conservative)
            // If organism is wrong, h is too optimistic/pessimistic and A* chooses worse routes.
            float h = dist * 10f * (1f + ( (s > 0f) ? (0.8f * x) : (0.2f * x) )) * (1f + bias);

            h = Mathf.Clamp(h, 1f, 100000f);
            heuristic[n] = (uint)Mathf.RoundToInt(h);
        }


        // New A*: SolveAstar(startNode, goalNode, heuristic)
        PathfindingAstar.AStarResult result = PathfindingAstar.SolveAstar(startNode, goalNode, heuristic);

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
            Debug.Log($"[PATH DEBUG] nodes={lastPath.Count} plannedCost={planned}");
        }

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
        PathfindingAstar.GraphNode n = FindClosestNode((Vector2)transform.position);
        if (n == null) return 0f;

        string[] p = n.name.Split(',');
        int x = int.Parse(p[0]);
        int y = int.Parse(p[1]);

        return terrain.GetSlope(x, y);
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
        string[] parts = node.name.Split(',');
        x = int.Parse(parts[0]);
        y = int.Parse(parts[1]);
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
        float s = terrain.GetSlope(toX, toY);              // true slope for that cell
        float t = NormalizedAbsSlope(s);                  // 0..1
        float bias = SlopeBiasFactor(s);                  // e.g. -0.10..+0.10

        // Base cost: 10 per step (so we have resolution as uint)
        // Extra penalty: uphill costs more than downhill (tuned by sign)
        float uphillExtra = (s > 0f) ? (1.0f + 2.0f * t) : (1.0f + 0.5f * t);

        // Apply organism's evolved bias: if it "knows hills are costly", bias>0 increases costs on hills
        // If it underestimates, bias<0 decreases perceived cost and it may choose bad routes.
        float perceived = 10f * uphillExtra * (1f + bias);

        // clamp and convert to uint
        perceived = Mathf.Clamp(perceived, 1f, 100000f);
        return (uint)Mathf.RoundToInt(perceived);
    }

}
