using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class OrganismBehaviour : MonoBehaviour
{
    public float border = 10f; 
    public float reachTolerance = 0.5f;
    public float speed = 5f;            
    public float wanderRadius = 8f; 

    private Vector2 currentTargetPosition;
    private bool isHunting = false;

    private List<Vector2> pathPoints = new List<Vector2>(); 
    private int pathIndex = 0;
   
    public List<PathfindingAstar.GraphNode> allNodes; 

    private void Start()
    {
        SetRandomWanderTarget();
    }

    private void Update()
    {
        GameObject closestSource = findClosestSource();

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

    private void CalculateAStarPath(Vector2 destination)
    {
        pathPoints.Clear();
        pathIndex = 0;

        var startNode = FindClosestNode(transform.position);
        var goalNode = FindClosestNode(destination);

        if (startNode == null || goalNode == null) return;

        Dictionary<string, uint> heuristic = new Dictionary<string, uint>();
        foreach (var node in allNodes)
        {
            
            float dist = Vector2.Distance(GetNodePosition(node), destination);
            heuristic[node.name] = (uint)(dist * 10); 
        }

        //run A*
        var result = PathfindingAstar.AStar.SolveAstar(startNode, goalNode.name, heuristic);

        if (result.node != null)
        {
            // "Node1, Node2, Node3" şeklindeki string'i Vector2 listesine çevir
            string[] nodeNames = result.path.Split(new string[] { ", " }, System.StringSplitOptions.None);
            foreach (string name in nodeNames)
            {
                var foundNode = allNodes.Find(n => n.name == name);
                if (foundNode != null) pathPoints.Add(GetNodePosition(foundNode));
            }
        }
    }

    private void FollowPath()
    {
        if (pathPoints.Count == 0 || pathIndex >= pathPoints.Count) return;

        Vector2 nextPoint = pathPoints[pathIndex];
        transform.position = Vector2.MoveTowards(transform.position, nextPoint, speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, nextPoint) < reachTolerance)
        {
            pathIndex++;
        }
    }

    //helper functions
    private PathfindingAstar.GraphNode FindClosestNode(Vector2 pos)
    {
        PathfindingAstar.GraphNode closestNode = null;
        float minDistance = float.MaxValue;

        //find the closest node 
        foreach (PathfindingAstar.GraphNode node in allNodes)
        {
            //get the position of the node
            Vector2 nodePos = GetNodePosition(node);
            
            //calculate the distance
            float distance = Vector2.Distance(nodePos, pos);

            //if its the min node, its the closest.
            if (distance < minDistance)
            {
                minDistance = distance;
                closestNode = node;
            }
        }

        return closestNode;
    }

    private Vector2 GetNodePosition(PathfindingAstar.GraphNode node)
    {
        // Düğüm isimlerinin "x,y" formatında olduğunu varsayıyoruz veya bir lookup table
        return Vector2.zero; // Burayı kendi grid yapına göre doldurmalısın
    }

    private GameObject findClosestSource()
    {
        GameObject[] sources = GameObject.FindGameObjectsWithTag("Source");
        GameObject closest = null;
        float minDist = border;
        foreach (var s in sources)
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
        currentTargetPosition = (Vector2)transform.position + Random.insideUnitCircle * wanderRadius;
        pathPoints.Clear();
        pathPoints.Add(currentTargetPosition);
        pathIndex = 0;
    }

    private void Wander()
    {
        if (pathPoints.Count == 0 || pathIndex >= pathPoints.Count) 
            SetRandomWanderTarget();
    }
}
