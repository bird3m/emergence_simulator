using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class AStarVisualizer : MonoBehaviour
{
    public OrganismBehaviour organism;
    public global::Terrain terrain;

    [Header("Render")]
    public float lineWidth = 0.12f;
    public int sortingOrder = 500;
    public string sortingLayerName = "Default";

    [Header("Z")]
    public float zOffset = -0.2f; // 2D’de üstte kalsın

    private LineRenderer lr;

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

        // Make sure it's visible above sprites
        lr.sortingLayerName = sortingLayerName;
        lr.sortingOrder = sortingOrder;

        // MATERIAL: try a few common shaders (Built-in + URP)
        if (lr.material == null)
        {
            Shader sh =
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Universal Render Pipeline/Unlit");

            if (sh != null)
            {
                lr.material = new Material(sh);
            }
        }

        // Give it a visible color even if material is Unlit/Color
        lr.startColor = Color.green;
        lr.endColor = Color.green;
    }

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

    private Vector3 NodeToWorld(PathfindingAstar.GraphNode node)
    {
        // node.name = "x,y"
        string[] p = node.name.Split(',');
        int x = int.Parse(p[0]);
        int y = int.Parse(p[1]);
        return terrain.CellCenterWorld(x, y);
    }
}
