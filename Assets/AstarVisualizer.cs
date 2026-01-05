using UnityEngine;
using System.Collections.Generic;

public class AStarVisualizer : MonoBehaviour
{
    public OrganismBehaviour selected;
    public global::Terrain terrain;

    [Header("Path Visual")]
    public Color pathColor = new Color(0.2f, 1f, 0.2f, 1f);
    public float sizeFactor = 0.6f;
    public bool drawLines = true;
    public bool drawNodes = true;

    private void OnDrawGizmos()
    {
        if (selected == null)
            return;

        if (terrain == null)
            terrain = FindObjectOfType<global::Terrain>();

        if (terrain == null)
            return;

        if (selected.lastPath == null || selected.lastPath.Count == 0)
            return;

        DrawPath(selected.lastPath);
    }

    private void DrawPath(List<PathfindingAstar.GraphNode> path)
    {
        float s = terrain.cellSize * sizeFactor;

        // Draw nodes
        if (drawNodes)
        {
            Gizmos.color = pathColor;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 p = NodeToWorld(path[i]);
                Gizmos.DrawCube(p, Vector3.one * s);
            }
        }

        // Draw connecting lines
        if (drawLines && path.Count >= 2)
        {
            Gizmos.color = new Color(pathColor.r, pathColor.g, pathColor.b, 0.9f);
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Gizmos.DrawLine(NodeToWorld(path[i]), NodeToWorld(path[i + 1]));
            }
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
