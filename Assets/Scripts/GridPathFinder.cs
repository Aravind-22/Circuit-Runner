using System.Collections.Generic;
using UnityEngine;

public class GridPathfinder : MonoBehaviour
{
    [SerializeField] private float cellSize = 0.5f;
    [SerializeField] private Vector2 worldMin = new Vector2(-9f, -9f);
    [SerializeField] private Vector2 worldMax = new Vector2(9f, 9f);
    [SerializeField] private LayerMask obstacleMask;

    private bool[,] blocked;

    private int width;
    private int height;

    private void Awake()
    {
        BuildGrid();
    }

    public void BuildGrid()
    {
        width = Mathf.RoundToInt((worldMax.x - worldMin.x) / cellSize);
        height = Mathf.RoundToInt((worldMax.y - worldMin.y) / cellSize);

        blocked = new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector3 world = GridToWorld(new Vector2Int(x, z));

                blocked[x, z] = Physics.CheckBox(
                    world,
                    Vector3.one * cellSize * 0.4f,
                    Quaternion.identity,
                    obstacleMask
                );
            }
        }
    }

    public List<Vector3> FindPath(Vector3 startWorld, Vector3 endWorld)
    {
        Vector2Int start = WorldToGrid(startWorld);
        Vector2Int end = WorldToGrid(endWorld);

        if (!IsValid(start) || !IsValid(end))
            return null;

        var open = new List<Node>();
        var closed = new HashSet<Vector2Int>();

        Node startNode = new Node(start, 0, Heuristic(start, end), null);

        open.Add(startNode);

        while (open.Count > 0)
        {
            Node current = GetLowestCostNode(open);

            open.Remove(current);
            closed.Add(current.position);

            if (current.position == end)
                return BuildPath(current);

            foreach (Vector2Int neighbour in GetNeighbours(current.position))
            {
                if (!IsValid(neighbour) || closed.Contains(neighbour))
                    continue;

                float newCost = current.g + Vector2Int.Distance(
                    current.position,
                    neighbour
                );

                Node existing = open.Find(n => n.position == neighbour);

                if (existing == null)
                {
                    Node node = new Node(
                        neighbour,
                        newCost,
                        newCost + Heuristic(neighbour, end),
                        current
                    );

                    open.Add(node);
                }
                else if (newCost < existing.g)
                {
                    existing.g = newCost;
                    existing.f = newCost + Heuristic(neighbour, end);
                    existing.parent = current;
                }
            }
        }

        return null;
    }

    public float GetPathCost(Vector3 start, Vector3 end)
    {
        List<Vector3> path = FindPath(start, end);

        if (path == null)
            return float.PositiveInfinity;

        float cost = 0f;

        for (int i = 1; i < path.Count; i++)
            cost += Vector3.Distance(path[i - 1], path[i]);

        return cost;
    }

    private Node GetLowestCostNode(List<Node> nodes)
    {
        Node best = nodes[0];

        for (int i = 1; i < nodes.Count; i++)
        {
            if (nodes[i].f < best.f)
                best = nodes[i];
        }

        return best;
    }

    private List<Vector2Int> GetNeighbours(Vector2Int position)
    {
        return new List<Vector2Int>
        {
            position + Vector2Int.up,
            position + Vector2Int.down,
            position + Vector2Int.left,
            position + Vector2Int.right
        };
    }

    private float Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private List<Vector3> BuildPath(Node node)
    {
        List<Vector3> path = new List<Vector3>();

        while (node != null)
        {
            path.Add(GridToWorld(node.position));
            node = node.parent;
        }

        path.Reverse();
        return path;
    }

    private bool IsValid(Vector2Int position)
    {
        return position.x >= 0 &&
               position.x < width &&
               position.y >= 0 &&
               position.y < height &&
               !blocked[position.x, position.y];
    }

    private Vector2Int WorldToGrid(Vector3 world)
    {
        int x = Mathf.RoundToInt((world.x - worldMin.x) / cellSize);
        int z = Mathf.RoundToInt((world.z - worldMin.y) / cellSize);

        return new Vector2Int(x, z);
    }

    private Vector3 GridToWorld(Vector2Int grid)
    {
        return new Vector3(
            worldMin.x + grid.x * cellSize,
            0.1f,
            worldMin.y + grid.y * cellSize
        );
    }

    private class Node
    {
        public Vector2Int position;
        public float g;
        public float f;
        public Node parent;

        public Node(Vector2Int position, float g, float f, Node parent)
        {
            this.position = position;
            this.g = g;
            this.f = f;
            this.parent = parent;
        }
    }
}