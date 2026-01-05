using UnityEngine;
using System.Collections.Generic;

public class OrganismBehaviour : MonoBehaviour
{
    [Header("Menzil ve Hız")]
    public float border = 10f; 
    public float reachTolerance = 0.5f;
    public float speed = 5f;            
    public float wanderRadius = 8f; 

    // Referanslar
    private Terrain terrain; // Senin yeni Terrain scriptin
    private List<PathfindingAstar.GraphNode> allNodes = new List<PathfindingAstar.GraphNode>(); 
    private List<Vector2> pathPoints = new List<Vector2>(); 
    private int pathIndex = 0;
    private bool isHunting = false;

    private void Start()
    {
        // 1. ADIM: Sahnedeki Terrain scriptini bul
        terrain = FindObjectOfType<Terrain>();

        if (terrain != null)
        {
            // 2. ADIM: Terrain bilgilerine göre Grid'i oluştur
            InitializeGrid();
        }
        else
        {
            Debug.LogError("Lordum, sahnede Terrain bulunamadı!");
        }

        SetRandomWanderTarget();
    }

    private void InitializeGrid()
    {
        allNodes.Clear();

        // Terrain scriptindeki genişlik ve yüksekliği kullanıyoruz
        for (int x = 0; x < terrain.width; x++)
        {
            for (int y = 0; y < terrain.height; y++)
            {
                PathfindingAstar.GraphNode node = new PathfindingAstar.GraphNode();
                // Koordinatları "x,y" olarak isimde sakla
                node.name = x + "," + y; 
                allNodes.Add(node);
            }
        }

        // Düğümleri birbirine bağla
        ConnectNodes();
    }

    private void ConnectNodes()
    {
        foreach (var node in allNodes)
        {
            string[] parcalar = node.name.Split(','); 
            int x = int.Parse(parcalar[0]); 
            int y = int.Parse(parcalar[1]); 

            // Komşuları sınır kontrolü yaparak bağla (Sağ, Sol, Üst, Alt)
            AddLinkIfValid(node, x + 1, y);
            AddLinkIfValid(node, x - 1, y);
            AddLinkIfValid(node, x, y + 1);
            AddLinkIfValid(node, x, y - 1);
        }
    }

    private void AddLinkIfValid(PathfindingAstar.GraphNode node, int tx, int ty)
    {
        if (tx >= 0 && tx < terrain.width && ty >= 0 && ty < terrain.height)
        {
            var target = allNodes.Find(n => n.name == tx + "," + ty);
            if (target != null)
            {
                // Her adımın maliyeti 1 birim
                node.links.Add(new PathfindingAstar.Link(1, target));
            }
        }
    }

    private void Update()
    {
        if (allNodes.Count == 0) return;

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

        var startNode = FindClosestNode((Vector2)transform.position);
        var goalNode = FindClosestNode(destination);

        if (startNode == null || goalNode == null) return;

        // Sezgisel (Heuristic) hesaplama
        Dictionary<string, uint> heuristic = new Dictionary<string, uint>();
        foreach (var n in allNodes)
        {
            float d = Vector2.Distance(GetNodePosition(n), destination);
            heuristic[n.name] = (uint)d;
        }

        var result = PathfindingAstar.AStar.SolveAstar(startNode, goalNode.name, heuristic);

        if (result.node != null)
        {
            // Path string'ini parçala ve yol noktalarını ekle
            string[] names = result.path.Split(new string[] { ", " }, System.StringSplitOptions.None);
            foreach (string name in names)
            {
                var found = allNodes.Find(n => n.name == name);
                if (found != null) pathPoints.Add(GetNodePosition(found));
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

    private PathfindingAstar.GraphNode FindClosestNode(Vector2 pos)
    {
        PathfindingAstar.GraphNode closest = null;
        float min = float.MaxValue;

        foreach (var node in allNodes)
        {
            float d = Vector2.Distance(GetNodePosition(node), pos);
            if (d < min) { min = d; closest = node; }
        }
        return closest;
    }

    private Vector2 GetNodePosition(PathfindingAstar.GraphNode node)
    {
        string[] p = node.name.Split(',');
        int x = int.Parse(p[0]);
        int y = int.Parse(p[1]);
        
        // Terrain scriptindeki CellCenterWorld fonksiyonunu kullanarak gerçek dünya koordinatını alıyoruz
        Vector3 worldPos = terrain.CellCenterWorld(x, y);
        return new Vector2(worldPos.x, worldPos.y);
    }

    private GameObject findClosestSource()
    {
        GameObject[] sources = GameObject.FindGameObjectsWithTag("Source");
        GameObject closest = null;
        float minDist = border;
        foreach (var s in sources)
        {
            float d = Vector2.Distance(transform.position, s.transform.position);
            if (d < minDist) { minDist = d; closest = s; }
        }
        return closest;
    }

    private void SetRandomWanderTarget()
    {
        pathPoints.Clear();
        pathPoints.Add((Vector2)transform.position + Random.insideUnitCircle * wanderRadius);
        pathIndex = 0;
    }

    private void Wander()
    {
        if (pathPoints.Count == 0 || pathIndex >= pathPoints.Count) SetRandomWanderTarget();
    }
}