using UnityEngine;
using System;
using System.Collections.Generic;

public class PathfindingAstar : MonoBehaviour
{
    // Priority queue implementation for efficient node selection
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

        public void Push(T item)
        {
            data.Add(item);
            SiftUp(data.Count - 1);
        }

        public T ExtractTop()
        {
            if (data.Count == 0) throw new InvalidOperationException("Heap is empty");
            T topItem = data[0];
            int lastIndex = data.Count - 1;
            data[0] = data[lastIndex];
            data.RemoveAt(lastIndex);

            if (data.Count > 0) SiftDown(0);
            return topItem;
        }

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (comparer.Compare(data[parentIndex], data[index]) <= 0) break;
                Swap(parentIndex, index);
                index = parentIndex;
            }
        }

        private void SiftDown(int index)
        {
            int count = data.Count;
            while (true)
            {
                int leftChild = 2 * index + 1;
                int rightChild = 2 * index + 2;
                int bestIndex = index;

                if (leftChild < count && comparer.Compare(data[leftChild], data[bestIndex]) < 0)
                    bestIndex = leftChild;

                if (rightChild < count && comparer.Compare(data[rightChild], data[bestIndex]) < 0)
                    bestIndex = rightChild;

                if (bestIndex == index) break;

                Swap(index, bestIndex);
                index = bestIndex;
            }
        }

        private void Swap(int a, int b)
        {
            T temp = data[a];
            data[a] = data[b];
            data[b] = temp;
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

    // Node structure for the search tree
    public class TreeNode
    {
        public GraphNode node;   
        public TreeNode parent;  // Pointer to the previous node to reconstruct the path
        public uint cost;        // g(n): cost from start to current node
        public uint hCost;       // h(n): estimated cost to goal
    }

    // Result structure to fix the CS1061 errors in OrganismBehaviour
    public struct AStarResult
    {
        public GraphNode endNode; 
        public string pathStr;    
        public uint totalCost;
    }

    public class CompareNode_Astar : IComparer<TreeNode>
    {
        public int Compare(TreeNode a, TreeNode b)
        {
            uint f1 = a.cost + a.hCost;
            uint f2 = b.cost + b.hCost;

            if (f1 < f2) return -1;
            if (f1 > f2) return 1;
            return 0;
        }
    }

    public static class AStar
    {
        public static AStarResult SolveAstar(GraphNode startGraphNode, string goalName, Dictionary<string, uint> heuristic)
        {
            Debug.Log("Starting A* Search...");

            AStarResult result = new AStarResult();
            result.endNode = null;
            result.pathStr = "NO SOLUTION";
            result.totalCost = 0;

            uint expandedNodesCount = 0;
            // Dictionary to keep track of the minimum cost to reach a node
            Dictionary<GraphNode, uint> costTracker = new Dictionary<GraphNode, uint>();
            MinHeap<TreeNode> fringe = new MinHeap<TreeNode>(new CompareNode_Astar());

            if (startGraphNode != null)
            {
                TreeNode root = new TreeNode();
                root.node = startGraphNode;
                root.parent = null;
                root.cost = 0;
                root.hCost = heuristic.ContainsKey(root.node.name) ? heuristic[root.node.name] : 0;

                fringe.Push(root);
                costTracker[startGraphNode] = 0;

                while (!fringe.Empty())
                {
                    TreeNode currentTreeNode = fringe.ExtractTop();
                    expandedNodesCount++;

                    // Goal check
                    if (currentTreeNode.node.name == goalName)
                    {
                        result.endNode = currentTreeNode.node;
                        result.pathStr = ReconstructPathString(currentTreeNode);
                        result.totalCost = currentTreeNode.cost;
                        break;
                    }

                    // Expand neighbors
                    for (int i = 0; i < currentTreeNode.node.links.Count; i++)
                    {
                        Link connection = currentTreeNode.node.links[i];
                        uint newMovementCost = currentTreeNode.cost + connection.cost;

                        // Only add to fringe if we found a cheaper way to reach this node
                        if (!costTracker.ContainsKey(connection.node) || newMovementCost < costTracker[connection.node])
                        {
                            costTracker[connection.node] = newMovementCost;

                            TreeNode nextNode = new TreeNode();
                            nextNode.node = connection.node;
                            nextNode.parent = currentTreeNode;
                            nextNode.cost = newMovementCost;
                            nextNode.hCost = heuristic.ContainsKey(nextNode.node.name) ? heuristic[nextNode.node.name] : 0;

                            fringe.Push(nextNode);
                        }
                    }
                }
            }

            Debug.Log("Nodes Expanded: " + expandedNodesCount);
            return result;
        }

        // Helper function to build the path string by backtracking through parents
        private static string ReconstructPathString(TreeNode leafNode)
        {
            List<string> pathList = new List<string>();
            TreeNode current = leafNode;

            while (current != null)
            {
                pathList.Add(current.node.name);
                current = current.parent;
            }

            pathList.Reverse();
            return string.Join(", ", pathList);
        }
    }
}