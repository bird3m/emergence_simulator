using UnityEngine;
using System.Collections.Generic;

public class AStarVisualizer : MonoBehaviour
{
    public OrganismBehaviour selected;
    public global::Terrain terrain;

    [Header("Colors")]
    public Color openColor = new Color(0.2f, 0.4f, 1f, 0.35f);
    public Color closedColor = new Color(1f, 0.2f, 0.2f, 0.35f);
    public Color pathColor = new Color(0.2f, 1f, 0.2f, 1f);

    public float sizeFactor = 0.6f;

    private void OnDrawGizmos()
    {
        if (selected == null)
            return;

        if (terrain == null)
            terrain = FindObjectOfType<global::Terrain>();

        if (terrain == null)
            return;

        if (selected.debugData == null)
            return;

        DrawSet(selected.debugData.open, openColor, false);
        DrawSet(selected.debugData.closed, closedColor, false);
        DrawList(selected.debugData.finalPath, pathColor, true);
    }

    private void DrawSet(HashSet<PathfindingAstar.GraphNode> set, Color c, bool solid)
    {
        Gizmos.color = c;
        foreach (var n in set)
        {
            Vector3 p = NodeToWorld(n);
            float s = terrain.cellSize * sizeFactor;
            if (solid) Gizmos.DrawCube(p, Vector3.one * s);
            else Gizmos.DrawWireCube(p, Vector3.one * s);
        }
    }

    private void DrawList(List<PathfindingAstar.GraphNode> list, Color c, bool solid)
    {
        Gizmos.color = c;
        for (int i = 0; i < list.Count; i++)
        {
            Vector3 p = NodeToWorld(list[i]);
            float s = terrain.cellSize * sizeFactor;
            if (solid) Gizmos.DrawCube(p, Vector3.one * s);
            else Gizmos.DrawWireCube(p, Vector3.one * s);
        }

        // Optional: draw connecting lines for the final path
        Gizmos.color = new Color(c.r, c.g, c.b, 0.9f);
        for (int i = 0; i + 1 < list.Count; i++)
        {
            Gizmos.DrawLine(NodeToWorld(list[i]), NodeToWorld(list[i + 1]));
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
