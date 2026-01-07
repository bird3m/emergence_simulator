using UnityEngine;
using System;
using System.Collections.Generic;

/*
*Class that implements the A* algorithm. It has 2 overload versions of the SolveAstar function. 
*/
public class PathfindingAstar : MonoBehaviour
{
    

    public class GraphNode
    {
        public string name;
        public List<Link> links = new List<Link>(); //neighboring nodes

        public GraphNode(string name)
        {
            this.name = name;
        }
    }

    public struct Link //symbolizes the edges
    {
        public uint cost; //cost of the edges
        public GraphNode node; //target nodes

        public Link(uint edgeCost, GraphNode targetNode)
        {
            cost = edgeCost;
            node = targetNode;
        }
    }


    public class TreeNode
    {
        public GraphNode node;
        public uint gCost; //real cost until the current node from the starting node
        public uint hCost; //heuristic cost from the current to the goal node
    }

    public struct AStarResult //the struct that the SolveAstar function will return
    {
        public bool found;
        public uint totalCost;
        public List<GraphNode> path; 
    }

    /*
     * A* Pathfinding Algorithm
     * Time: O((V + E) * log V) because visiting V nodes with E edges and heap operations
     * Space: O(V) because storing open list, closed set and dictionaries
     */
    public static AStarResult SolveAstar(GraphNode start, GraphNode goal, Func<GraphNode, uint> heuristicFunc, Func<GraphNode, GraphNode, uint> costFunc)  
    {
            AStarResult result = new AStarResult();
            result.found = false;
            result.totalCost = 0;
            result.path = new List<GraphNode>();

            if (start == null || goal == null) 
                return result;

            if (heuristicFunc == null) 
                throw new Exception("heuristicFunc is null.");

            MinHeap<TreeNode> open = new MinHeap<TreeNode>(new CompareNode_Astar()); //nodes which will be explored
            HashSet<GraphNode> closed = new HashSet<GraphNode>(); //nodes which have already been explored
            Dictionary<GraphNode, GraphNode> cameFrom = new Dictionary<GraphNode, GraphNode>(); //to find which node led to another node
            Dictionary<GraphNode, uint> gScore = new Dictionary<GraphNode, uint>(); //stores the actual cost to reach every node

            //add the starting node 
            gScore[start] = 0;

            TreeNode initialTreeNode = new TreeNode();
            initialTreeNode.node = start;
            initialTreeNode.gCost = 0;
            initialTreeNode.hCost = heuristicFunc(start);
            open.Push(initialTreeNode);

            while (!open.Empty())
            {
                TreeNode currentTreeNode = open.ExtractTop();
                GraphNode current = currentTreeNode.node;

                if (closed.Contains(current)) //if its explored already, skip it
                    continue;

                if (current == goal)
                {
                    result.found = true;
                    result.totalCost = gScore[current];
                    result.path = ReconstructPath(cameFrom, current);
                    return result;
                }

                closed.Add(current);

                for (int i = 0; i < current.links.Count; i++) //checks the neighbors
                {
                    Link currentLink = current.links[i];
                    GraphNode neighbor = currentLink.node;

                    if (neighbor == null || closed.Contains(neighbor)) 
                        continue;

                    uint stepCost = costFunc(current, neighbor); 
                    uint tentativeGCost = gScore[current] + stepCost;

                    bool isFirstVisit = !gScore.ContainsKey(neighbor);
                    
                    bool isShorterPath = false;
                    if (gScore.ContainsKey(neighbor))
                    {
                        isShorterPath = tentativeGCost < gScore[neighbor];
                    }
                    
                    bool isBetterPath = isFirstVisit || isShorterPath;
                    
                    if (isBetterPath)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGCost; //calculates the cost to reach the neighbor

                        TreeNode neighborTreeNode = new TreeNode();
                        neighborTreeNode.node = neighbor;
                        neighborTreeNode.gCost = tentativeGCost;
                        neighborTreeNode.hCost = heuristicFunc(neighbor);

                        open.Push(neighborTreeNode);
                    }
                }
            }

            return result;
        }


    /*
    * Compares treenodes based on their f cost (where f = g + h)
    * Time: O(1) because simple arithmetic and comparison
    * Space: O(1) because no extra memory allocation
    */
    public class CompareNode_Astar : IComparer<TreeNode>
    {
        public int Compare(TreeNode a, TreeNode b)
        {
            uint firstFCost = a.gCost + a.hCost;
            uint secondFCost = b.gCost + b.hCost;

            if (firstFCost < secondFCost) 
                return -1;
            if (firstFCost > secondFCost)
                return 1;

            return 0;
        }
    }



    /*
     * Priority Queue implementation (because there was no priority queue in C# standard library...)
     * Space: O(n) because storing n elements in list
     */
    public class MinHeap<T>
    {
        private List<T> data = new List<T>();
        private IComparer<T> comparer;

        public MinHeap(IComparer<T> comparer)
        {
            this.comparer = comparer;
        }

        // Time: O(1) because single property access, Space: O(1)
        public bool Empty()
        {
            return data.Count == 0;
        }

        // Time: O(1) because array access, Space: O(1)
        public T Top()
        {
            if (data.Count == 0)
                throw new InvalidOperationException("Heap is empty");
            return data[0];
        }

        // Time: O(log n) because sifting up through heap levels, Space: O(1)
        public void Push(T item)
        {
            data.Add(item);
            SiftUp(data.Count - 1);
        }

        // Time: O(log n) because sifting down through heap levels, Space: O(1)
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

        // Time: O(log n) because calling Pop, Space: O(1)
        public T ExtractTop()
        {
            T topElement = Top();
            Pop();
            return topElement;
        }

        // Time: O(log n) because traversing height of tree, Space: O(1)
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

        // Time: O(log n) because traversing height of tree, Space: O(1)
        private void SiftDown(int i)
        {
            int heapSize = data.Count;

            while (true)
            {
                int left = 2 * i + 1;
                int right = 2 * i + 2;
                int best = i;

                if (left < heapSize && comparer.Compare(data[left], data[best]) < 0)
                    best = left;

                if (right < heapSize && comparer.Compare(data[right], data[best]) < 0)
                    best = right;

                if (best == i)
                    break;

                Swap(i, best);
                i = best;
            }
        }

        // Time: O(1) 
        // Space: O(1)
        private void Swap(int a, int b)
        {
            T temporaryElement = data[a];
            data[a] = data[b];
            data[b] = temporaryElement;
        }
    }

    

    /*
     * A* Pathfinding Algorithm 
     * Time Complexity: O((V + E) * log V) where V = number of nodes, E is the number of edges
     * Space Complexity: O(V) for storing open list, closed set, and dictionaries
     */
    public static AStarResult SolveAstar(GraphNode start, GraphNode goal, Dictionary<GraphNode, uint> heuristic)
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

        TreeNode initialTreeNode = new TreeNode();
        initialTreeNode.node = start;
        initialTreeNode.gCost = 0;
        initialTreeNode.hCost = heuristic[start];

        open.Push(initialTreeNode);

        while (!open.Empty())
        {
            TreeNode currentTreeNode = open.ExtractTop();
            GraphNode current = currentTreeNode.node;

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
                Link currentLink = current.links[i];
                GraphNode neighbor = currentLink.node;

                if (neighbor == null)
                    continue;

                if (closed.Contains(neighbor))
                    continue;

                uint currentGCost = gScore[current];
                uint tentativeGCost = currentGCost + currentLink.cost;

                bool isBetterPath = !gScore.ContainsKey(neighbor) || tentativeGCost < gScore[neighbor];

                if (isBetterPath)
                {
                    if (!heuristic.ContainsKey(neighbor))
                        throw new Exception("Heuristic missing for node: " + neighbor.name);

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGCost;

                    TreeNode neighborTreeNode = new TreeNode();
                    neighborTreeNode.node = neighbor;
                    neighborTreeNode.gCost = tentativeGCost;
                    neighborTreeNode.hCost = heuristic[neighbor];

                    open.Push(neighborTreeNode);
                }
            }
        }

        return result; // not found
    }

    /*
     * Reconstructs the path from start to goal by backtracking through cameFrom dictionary
     * Time: O(n) 
     * Space: O(n) because it stores path list
     */
    private static List<GraphNode> ReconstructPath(Dictionary<GraphNode, GraphNode> cameFrom, GraphNode current)
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
