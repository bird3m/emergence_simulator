using UnityEngine;
using System;
using System.Collections.Generic;

public class OrganismBehaviour : MonoBehaviour
{
    [Header("Settings")]
    public float border = 10f;
    public float reachTolerance = 0.5f;
    public float speed = 5f;
    public float wanderRadius = 8f;

    [Header("Repathing")]
    public bool repathIfTargetMoved = true;
    public float repathMoveThreshold = 1.0f;

    private global::Terrain terrain;

    private List<PathfindingAstar.GraphNode> allNodes = new List<PathfindingAstar.GraphNode>();
    private Dictionary<string, PathfindingAstar.GraphNode> nodeByName = new Dictionary<string, PathfindingAstar.GraphNode>();

    private List<Vector2> pathPoints = new List<Vector2>();
    private int pathIndex = 0;

    // Target lock
    private GameObject currentTarget = null;
    private Vector2 lastPlannedTargetPos;
    Traits traits;
    private Vector2 lastPos;


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
        traits = GetComponent<Traits>();
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
        if (currentTarget != null && (pathPoints.Count == 0 || pathIndex >= pathPoints.Count))
        {
            Vector2 t = currentTarget.transform.position;
            transform.position = Vector2.MoveTowards(transform.position, t, speed * Time.deltaTime);

            if (Vector2.Distance(transform.position, t) < reachTolerance)
            {
                currentTarget = null;
                SetRandomWanderTarget();
            }
        }

        float movedDistance = Vector2.Distance((Vector2)transform.position, beforeMove);
        traits.UpdateVitals(movedDistance, Time.deltaTime);
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
            node.links.Add(new PathfindingAstar.Link(1, targetNode));
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
            float dist = Vector2.Distance(GetNodePosition(n), destination);
            heuristic[n] = (uint)(dist * 10f); // scale to reduce truncation to 0
        }

        // New A*: SolveAstar(startNode, goalNode, heuristic)
        PathfindingAstar.AStarResult result =
            PathfindingAstar.SolveAstar(startNode, goalNode, heuristic);

        if (!result.found || result.path == null || result.path.Count == 0)
            return;

        // Convert node path to world points
        for (int i = 0; i < result.path.Count; i++)
        {
            pathPoints.Add(GetNodePosition(result.path[i]));
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
}
