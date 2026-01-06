using UnityEngine;
using System;
using System.Collections.Generic;

public class PathfindingAstar : MonoBehaviour
{
    // -------------------- Graph Structures --------------------

    public class GraphNode
    {
        public string name;
        public List<Link> links = new List<Link>();

        public GraphNode(string name)
        {
            this.name = name;
        }
    }

    public struct Link
    {
        public uint cost;
        public GraphNode node;

        public Link(uint c, GraphNode n)
        {
            cost = c;
            node = n;
        }
    }

    // Only used inside the heap (fringe)
    public class TreeNode
    {
        public GraphNode node;
        public uint gCost;
        public uint hCost;
    }

    public struct AStarResult
    {
        public bool found;
        public uint totalCost;
        public List<GraphNode> path; // start -> goal
    }

    public static AStarResult SolveAstar(
    GraphNode start,
    GraphNode goal,
    Func<GraphNode, uint> heuristicFunc,
    Func<GraphNode, GraphNode, uint> costFunc) // Yeni: İki düğüm arası maliyet fonksiyonu
{
        AStarResult result = new AStarResult();
        result.found = false;
        result.totalCost = 0;
        result.path = new List<GraphNode>();

        if (start == null || goal == null) return result;
        if (heuristicFunc == null) throw new Exception("heuristicFunc is null.");

        MinHeap<TreeNode> open = new MinHeap<TreeNode>(new CompareNode_Astar());
        HashSet<GraphNode> closed = new HashSet<GraphNode>();
        Dictionary<GraphNode, GraphNode> cameFrom = new Dictionary<GraphNode, GraphNode>();
        Dictionary<GraphNode, uint> gScore = new Dictionary<GraphNode, uint>();

        gScore[start] = 0;

        TreeNode root = new TreeNode();
        root.node = start;
        root.gCost = 0;
        root.hCost = heuristicFunc(start);
        open.Push(root);

        while (!open.Empty())
        {
            TreeNode currentTN = open.ExtractTop();
            GraphNode current = currentTN.node;

            if (closed.Contains(current))
                continue;

            if (current == goal)
            {
                result.found = true;
                result.totalCost = gScore[current];
                result.path = ReconstructPath(cameFrom, current);
                return result;
            }

            closed.Add(current);

            for (int i = 0; i < current.links.Count; i++)
            {
                Link l = current.links[i];
                GraphNode neighbor = l.node;

                if (neighbor == null || closed.Contains(neighbor)) continue;

                // HATA BURADAYDI: l.cost yerine dışarıdan gelen costFunc kullanılıyor
                uint stepCost = costFunc(current, neighbor); 
                uint tentativeG = gScore[current] + stepCost;

                bool better = !gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor];
                if (better)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;

                    TreeNode tn = new TreeNode();
                    tn.node = neighbor;
                    tn.gCost = tentativeG;
                    tn.hCost = heuristicFunc(neighbor);

                    open.Push(tn);
                }
            }
        }

        return result;
    }



    // -------------------- Comparator (C++ style) --------------------

    public class CompareNode_Astar : IComparer<TreeNode>
    {
        public int Compare(TreeNode a, TreeNode b)
        {
            uint f1 = a.gCost + a.hCost;
            uint f2 = b.gCost + b.hCost;

            if (f1 < f2) return -1;
            if (f1 > f2) return 1;
            return 0;
        }
    }

    // -------------------- MinHeap --------------------

    public class MinHeap<T>
    {
        private List<T> data = new List<T>();
        private IComparer<T> comparer;

        public MinHeap(IComparer<T> comparer)
        {
            this.comparer = comparer;
        }

        public bool Empty()
        {
            return data.Count == 0;
        }

        public T Top()
        {
            if (data.Count == 0)
                throw new InvalidOperationException("Heap is empty");
            return data[0];
        }

        public void Push(T item)
        {
            data.Add(item);
            SiftUp(data.Count - 1);
        }

        public void Pop()
        {
            if (data.Count == 0)
                throw new InvalidOperationException("Heap is empty");

            int last = data.Count - 1;
            data[0] = data[last];
            data.RemoveAt(last);

            if (data.Count > 0)
                SiftDown(0);
        }

        public T ExtractTop()
        {
            T t = Top();
            Pop();
            return t;
        }

        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;

                if (comparer.Compare(data[parent], data[i]) <= 0)
                    break;

                Swap(parent, i);
                i = parent;
            }
        }

        private void SiftDown(int i)
        {
            int n = data.Count;

            while (true)
            {
                int left = 2 * i + 1;
                int right = 2 * i + 2;
                int best = i;

                if (left < n && comparer.Compare(data[left], data[best]) < 0)
                    best = left;

                if (right < n && comparer.Compare(data[right], data[best]) < 0)
                    best = right;

                if (best == i)
                    break;

                Swap(i, best);
                i = best;
            }
        }

        private void Swap(int a, int b)
        {
            T tmp = data[a];
            data[a] = data[b];
            data[b] = tmp;
        }
    }

    // -------------------- A* (returns only final path) --------------------

    public static AStarResult SolveAstar(
        GraphNode start,
        GraphNode goal,
        Dictionary<GraphNode, uint> heuristic
    )
    {
        AStarResult result = new AStarResult();
        result.found = false;
        result.totalCost = 0;
        result.path = new List<GraphNode>();

        if (start == null || goal == null)
            return result;

        if (heuristic == null)
            throw new Exception("Heuristic dictionary is null.");

        if (!heuristic.ContainsKey(start))
            throw new Exception("Heuristic missing for start node: " + start.name);

        if (!heuristic.ContainsKey(goal))
            throw new Exception("Heuristic missing for goal node: " + goal.name);

        MinHeap<TreeNode> open = new MinHeap<TreeNode>(new CompareNode_Astar());
        HashSet<GraphNode> closed = new HashSet<GraphNode>();
        Dictionary<GraphNode, GraphNode> cameFrom = new Dictionary<GraphNode, GraphNode>();
        Dictionary<GraphNode, uint> gScore = new Dictionary<GraphNode, uint>();

        gScore[start] = 0;

        TreeNode root = new TreeNode();
        root.node = start;
        root.gCost = 0;
        root.hCost = heuristic[start];

        open.Push(root);

        while (!open.Empty())
        {
            TreeNode currentTN = open.ExtractTop();
            GraphNode current = currentTN.node;

            if (closed.Contains(current))
                continue;

            if (current == goal)
            {
                result.found = true;
                result.totalCost = gScore[current];
                result.path = ReconstructPath(cameFrom, current);
                return result;
            }

            closed.Add(current);

            for (int i = 0; i < current.links.Count; i++)
            {
                Link l = current.links[i];
                GraphNode neighbor = l.node;

                if (neighbor == null)
                    continue;

                if (closed.Contains(neighbor))
                    continue;

                uint currentG = gScore[current];
                uint tentativeG = currentG + l.cost;

                bool better = !gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor];

                if (better)
                {
                    if (!heuristic.ContainsKey(neighbor))
                        throw new Exception("Heuristic missing for node: " + neighbor.name);

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;

                    TreeNode tn = new TreeNode();
                    tn.node = neighbor;
                    tn.gCost = tentativeG;
                    tn.hCost = heuristic[neighbor];

                    open.Push(tn);
                }
            }
        }

        return result; // not found
    }

    private static List<GraphNode> ReconstructPath(
        Dictionary<GraphNode, GraphNode> cameFrom,
        GraphNode current
    )
    {
        List<GraphNode> path = new List<GraphNode>();
        path.Add(current);

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
