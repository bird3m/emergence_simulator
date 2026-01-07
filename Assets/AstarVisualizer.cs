using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]

/**
Visualizes the path thats calculated by the A* algorithm for an organism.
*/
public class AStarVisualizer : MonoBehaviour
{
    public OrganismBehaviour organism;
    public global::Terrain terrain;

    public float lineWidth = 0.12f;
    public int sortingOrder = 500;
    public string sortingLayerName = "Default";

    public float zOffset = -0.2f; 

    private LineRenderer lr; //a tool that unity uses for rendering lines on visuals.

    // Time: O(1) 
    // Space: O(1)
    private void Awake()
    {
        lr = GetComponent<LineRenderer>();

        // Core settings
        lr.useWorldSpace = true;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.positionCount = 0;
        lr.numCapVertices = 6;
        lr.numCornerVertices = 6;

        //visible above sprites
        lr.sortingLayerName = sortingLayerName;
        lr.sortingOrder = sortingOrder;

        if (lr.material == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null)
            {
                sh = Shader.Find("Unlit/Color");
                if (sh == null)
                {
                    sh = Shader.Find("Universal Render Pipeline/Unlit");
                }
            }

            if (sh != null)
            {
                lr.material = new Material(sh);
            }
        }

        lr.startColor = Color.green;
        lr.endColor = Color.green;
    }

    // Time: O(n) because iterating through path, Space: O(1)
    private void LateUpdate()
    {
        if (organism == null)
        {
            lr.positionCount = 0;
            return;
        }

        if (terrain == null)
            terrain = FindObjectOfType<global::Terrain>();

        if (terrain == null || organism.lastPath == null || organism.lastPath.Count == 0)
        {
            lr.positionCount = 0;
            return;
        }

        List<PathfindingAstar.GraphNode> path = organism.lastPath;

        lr.positionCount = path.Count;

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 w = NodeToWorld(path[i]);
            w.z = zOffset;
            lr.SetPosition(i, w);
        }
    }

    // Time: O(1)
    //  Space: O(1)
    private Vector3 NodeToWorld(PathfindingAstar.GraphNode node)
    {
        string[] p = node.name.Split(',');
        int x = int.Parse(p[0]);
        int y = int.Parse(p[1]);
        return terrain.CellCenterWorld(x, y);
    }
}
