using UnityEngine;
using System;
using System.Collections.Generic;

public class PathfindingAstar : MonoBehaviour
{
    //Priority queue implementation
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

        public int Size()
        {
            return data.Count;
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

                // if parent <= child, ok
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

    public class GraphNode
    {
        public string name;
        public List<Link> links = new List<Link>();
    }

    public class Link
    {
        public uint cost;
        public GraphNode node;

        public Link(uint cost, GraphNode node)
        {
            this.cost = cost;
            this.node = node;
        }
    }

    public class TreeNode
    {
        public GraphNode node;   // current node
        public string path;      // full path so far
        public uint cost;        // g(n)
        public uint hCost;       // h(n)
        public uint depth;       // optional
    }

    //Comparator
    public class CompareNode_Astar : IComparer<TreeNode>
    {
        public int Compare(TreeNode a, TreeNode b)
        {
            uint f1 = a.cost + a.hCost;
            uint f2 = b.cost + b.hCost;

            if (f1 < f2)
                return -1;
            if (f1 > f2) 
                return 1;

            return 0;
        }
    }
    public static class AStar
    {
        public static TreeNode SolveAstar(GraphNode graph, string goal, Dictionary<string, uint> heuristic)
        {
            Debug.Log("A* Search");

            TreeNode ans = new TreeNode();
            ans.node = null;
            ans.cost = 0;
            ans.hCost = 0;
            ans.path = "NO SOLUTION";
            ans.depth = 0;

            uint expandedNodes = 0;
            HashSet<GraphNode> visited = new HashSet<GraphNode>();

            if (graph != null)
            {
                TreeNode root = new TreeNode();
                root.node = graph;
                root.path = graph.name;
                root.cost = 0;

                root.hCost = heuristic[root.node.name];

            
                root.depth = 0;

                MinHeap<TreeNode> fringe = new MinHeap<TreeNode>(new CompareNode_Astar());
                fringe.Push(root);

                while (!fringe.Empty())
                {
                    TreeNode stateToExpand = fringe.ExtractTop();

                    if (!visited.Contains(stateToExpand.node))
                    {
                        visited.Add(stateToExpand.node);
                        expandedNodes++;

                        if (stateToExpand.node.name == goal)
                        {
                            ans.node = stateToExpand.node;
                            ans.cost = stateToExpand.cost;
                            ans.path = stateToExpand.path;
                            break;
                        }
                        else
                        {
                            for (int i = 0; i < stateToExpand.node.links.Count; i++)
                            {
                                Link link = stateToExpand.node.links[i];

                                TreeNode state = new TreeNode();
                                state.node = link.node;
                                state.path = stateToExpand.path + ", " + link.node.name;
                                state.cost = link.cost + stateToExpand.cost;

                                uint hn = 0;
                                if (heuristic != null && heuristic.ContainsKey(state.node.name))
                                    hn = heuristic[state.node.name];

                                state.hCost = hn;
                                state.depth = stateToExpand.depth + 1;

                                fringe.Push(state);
                            }
                        }
                    }
                }
            }

            Debug.Log("Expanded nodes: " + expandedNodes);
            return ans;
        }
    }
}
