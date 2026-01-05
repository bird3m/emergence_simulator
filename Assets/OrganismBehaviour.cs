using UnityEngine;
using System.Collections.Generic;

public class OrganismBehaviour : MonoBehaviour
{
    [Header("Settings")]
    public float border = 10f; 
    public float reachTolerance = 0.5f;
    public float speed = 5f;            
    public float wanderRadius = 8f; 

    // References
    private Terrain terrain; 
    private List<PathfindingAstar.GraphNode> allNodes = new List<PathfindingAstar.GraphNode>(); 
    private List<Vector2> pathPoints = new List<Vector2>(); 
    private int pathIndex = 0;
    private bool isHunting = false;

    private void Start()
    {
        // Find terrain in the scene
        terrain = FindObjectOfType<Terrain>();

        if (terrain != null)
        {
            // Build the grid system
            InitializeGrid();
        }
        else
        {
            Debug.LogError("Terrain not found in the scene!");
        }

        SetRandomWanderTarget();
    }

    private void Update()
    {
        if (allNodes.Count == 0) return;

        GameObject closestSource = FindClosestSource();

        if (closestSource != null)
        {
            if (!isHunting)
            {
                isHunting = true;
                CalculateAStarPath(closestSource.transform.position);
            } 
        }
        else
        {
            isHunting = false;
            Wander();        
        }

        FollowPath();
    }

    // --- GRID SETUP ---

    private void InitializeGrid()
    {
        allNodes.Clear();

        // Create nodes for each cell based on terrain dimensions
        for (int x = 0; x < terrain.width; x++)
        {
            for (int y = 0; y < terrain.height; y++)
            {
                PathfindingAstar.GraphNode node = new PathfindingAstar.GraphNode();
                // Store coordinates as "x,y" for parsing later
                node.name = x + "," + y; 
                allNodes.Add(node);
            }
        }

        ConnectNodes();
    }

    private void ConnectNodes()
    {
        foreach (PathfindingAstar.GraphNode node in allNodes)
        {
            // Split the name string by comma
            string[] parts = node.name.Split(','); 
            int x = int.Parse(parts[0]); 
            int y = int.Parse(parts[1]); 

            // Check and link neighbors (Right, Left, Up, Down)
            AddLinkIfValid(node, x + 1, y); 
            AddLinkIfValid(node, x - 1, y); 
            AddLinkIfValid(node, x, y + 1); 
            AddLinkIfValid(node, x, y - 1); 
        }
    }

    private void AddLinkIfValid(PathfindingAstar.GraphNode node, int targetX, int targetY)
    {
        if (targetX >= 0 && targetX < terrain.width && targetY >= 0 && targetY < terrain.height)
        {
            PathfindingAstar.GraphNode targetNode = null;
            // Find the node with the matching coordinate name
            foreach (PathfindingAstar.GraphNode n in allNodes)
            {
                if (n.name == targetX + "," + targetY)
                {
                    targetNode = n;
                    break;
                }
            }

            if (targetNode != null)
            {
                node.links.Add(new PathfindingAstar.Link(1, targetNode));
            }
        }
    }

    // --- PATHFINDING & MOVEMENT ---

    private void CalculateAStarPath(Vector2 destination)
    {
        pathPoints.Clear();
        pathIndex = 0;

        PathfindingAstar.GraphNode startNode = FindClosestNode((Vector2)transform.position);
        PathfindingAstar.GraphNode goalNode = FindClosestNode(destination);

        if (startNode == null || goalNode == null) return;

        // Calculate heuristic for each node
        Dictionary<string, uint> heuristic = new Dictionary<string, uint>();
        foreach (PathfindingAstar.GraphNode n in allNodes)
        {
            float dist = Vector2.Distance(GetNodePosition(n), destination);
            heuristic[n.name] = (uint)dist;
        }

        // Call the optimized A* (Returns: endNode, pathStr, totalCost)
        var result = PathfindingAstar.AStar.SolveAstar(startNode, goalNode.name, heuristic);

        // FIX: Use 'result.endNode' instead of 'result.node'
        if (result.endNode != null)
        {
            // FIX: Use 'result.pathStr' instead of 'result.path'
            string[] nodeNames = result.pathStr.Split(new string[] { ", " }, System.StringSplitOptions.None);
            
            foreach (string name in nodeNames)
            {
                foreach (PathfindingAstar.GraphNode found in allNodes)
                {
                    if (found.name == name)
                    {
                        pathPoints.Add(GetNodePosition(found));
                        break;
                    }
                }
            }
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
    }

    // --- HELPERS ---

    private PathfindingAstar.GraphNode FindClosestNode(Vector2 pos)
    {
        PathfindingAstar.GraphNode closest = null;
        float minDistance = float.MaxValue;

        foreach (PathfindingAstar.GraphNode node in allNodes)
        {
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
        
        // Convert grid coords to world position using your Terrain script
        Vector3 worldPos = terrain.CellCenterWorld(x, y);
        return new Vector2(worldPos.x, worldPos.y);
    }

    private GameObject FindClosestSource()
    {
        GameObject[] sources = GameObject.FindGameObjectsWithTag("Source");
        GameObject closest = null;
        float minDist = border;

        foreach (GameObject s in sources)
        {
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
        Vector2 randomPoint = (Vector2)transform.position + Random.insideUnitCircle * wanderRadius;
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