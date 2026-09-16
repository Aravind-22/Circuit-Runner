using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shortest obstacle-free path between two points, computed with a
/// visibility graph (obstacle corners as nodes, connected when line-of-
/// sight is clear, solved with Dijkstra) instead of a grid. This gives
/// direct, diagonal, natural-looking routes and guarantees the path
/// never cuts through an obstacle, since nodes sit on the inflated
/// (agent-radius-padded) obstacle boundary.
/// </summary>
public class VisibilityGraphPathfinder : MonoBehaviour
{
    public static VisibilityGraphPathfinder Instance { get; private set; }

    [SerializeField] private float agentRadius = 0.6f;
    [SerializeField] private float cornerPadding = 0.08f; // keeps corner nodes off the AABB boundary (avoids float-tangency rejects)

    public float AgentRadius => agentRadius;

    private readonly List<ObstacleBounds> obstacles = new List<ObstacleBounds>();

    private struct ObstacleBounds
    {
        public Vector2 center;
        public Vector2 halfExtents; // already inflated by agentRadius
    }

    private void Awake()
    {
        Instance = this;
    }

    public void ClearObstacles()
    {
        obstacles.Clear();
    }

    // worldCenter / worldHalfExtents are the RAW obstacle box (world space, XZ only, no rotation).
    public void RegisterObstacle(Vector3 worldCenter, Vector3 worldHalfExtents)
    {
        obstacles.Add(new ObstacleBounds
        {
            center = new Vector2(worldCenter.x, worldCenter.z),
            halfExtents = new Vector2(
                worldHalfExtents.x + agentRadius,
                worldHalfExtents.z + agentRadius)
        });
    }

    public List<Vector3> FindPath(Vector3 start, Vector3 end)
    {
        float y = start.y;
        Vector2 s = new Vector2(start.x, start.z);
        Vector2 e = new Vector2(end.x, end.z);

        List<Vector2> nodes = BuildNodes(s, e);
        int nodeCount = nodes.Count;

        float[,] weights = new float[nodeCount, nodeCount];

        for (int i = 0; i < nodeCount; i++)
        {
            for (int j = i + 1; j < nodeCount; j++)
            {
                if (IsSegmentClear(nodes[i], nodes[j]))
                {
                    float dist = Vector2.Distance(nodes[i], nodes[j]);
                    weights[i, j] = dist;
                    weights[j, i] = dist;
                }
                else
                {
                    weights[i, j] = -1f;
                    weights[j, i] = -1f;
                }
            }
        }

        List<int> indexPath = Dijkstra(weights, 0, 1, nodeCount);

        if (indexPath == null)
            return null;

        List<Vector3> worldPath = new List<Vector3>(indexPath.Count);

        foreach (int index in indexPath)
        {
            Vector2 p = nodes[index];
            worldPath.Add(new Vector3(p.x, y, p.y));
        }

        return worldPath;
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

    // Chains FindPath across each leg of a pickup route, dropping the
    // duplicate join point between legs.
    public List<Vector3> FindRoute(Vector3 start, List<Vector3> waypoints)
    {
        List<Vector3> fullPath = new List<Vector3> { start };
        Vector3 current = start;

        foreach (Vector3 destination in waypoints)
        {
            List<Vector3> leg = FindPath(current, destination);

            if (leg == null || leg.Count < 2)
                continue;

            for (int i = 1; i < leg.Count; i++)
                fullPath.Add(leg[i]);

            current = destination;
        }

        return fullPath;
    }

    // index 0 = start, index 1 = end, rest = inflated obstacle corners.
    private List<Vector2> BuildNodes(Vector2 start, Vector2 end)
    {
        List<Vector2> nodes = new List<Vector2> { start, end };

        Vector2[] signs =
        {
            new Vector2(-1, -1), new Vector2(1, -1),
            new Vector2(1, 1), new Vector2(-1, 1)
        };

        foreach (ObstacleBounds obstacle in obstacles)
        {
            foreach (Vector2 sign in signs)
            {
                Vector2 corner = obstacle.center + Vector2.Scale(obstacle.halfExtents, sign);
                Vector2 outward = sign.normalized * cornerPadding;
                nodes.Add(corner + outward);
            }
        }

        return nodes;
    }

    private bool IsSegmentClear(Vector2 a, Vector2 b)
    {
        foreach (ObstacleBounds obstacle in obstacles)
        {
            if (SegmentIntersectsBoxInterior(a, b, obstacle))
                return false;
        }

        return true;
    }

    // Slab method. Only rejects the segment if it crosses the box's
    // INTERIOR — grazing along an obstacle's own boundary (e.g. between
    // two of its own adjacent corners) is allowed.
    private bool SegmentIntersectsBoxInterior(Vector2 a, Vector2 b, ObstacleBounds box)
    {
        const float eps = 0.001f;

        float minX = box.center.x - box.halfExtents.x + eps;
        float maxX = box.center.x + box.halfExtents.x - eps;
        float minY = box.center.y - box.halfExtents.y + eps;
        float maxY = box.center.y + box.halfExtents.y - eps;

        Vector2 d = b - a;
        float tMin = 0f;
        float tMax = 1f;

        if (!ClipSegment(-d.x, a.x - minX, ref tMin, ref tMax)) return false;
        if (!ClipSegment(d.x, maxX - a.x, ref tMin, ref tMax)) return false;
        if (!ClipSegment(-d.y, a.y - minY, ref tMin, ref tMax)) return false;
        if (!ClipSegment(d.y, maxY - a.y, ref tMin, ref tMax)) return false;

        return tMin < tMax;
    }

    private bool ClipSegment(float p, float q, ref float tMin, ref float tMax)
    {
        if (Mathf.Abs(p) < 1e-8f)
            return q >= 0f;

        float r = q / p;

        if (p < 0f)
        {
            if (r > tMax) return false;
            if (r > tMin) tMin = r;
        }
        else
        {
            if (r < tMin) return false;
            if (r < tMax) tMax = r;
        }

        return true;
    }

    private List<int> Dijkstra(float[,] weights, int startIndex, int endIndex, int count)
    {
        float[] dist = new float[count];
        int[] previous = new int[count];
        bool[] visited = new bool[count];

        for (int i = 0; i < count; i++)
        {
            dist[i] = float.MaxValue;
            previous[i] = -1;
        }

        dist[startIndex] = 0f;

        for (int iteration = 0; iteration < count; iteration++)
        {
            int current = -1;
            float best = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (!visited[i] && dist[i] < best)
                {
                    best = dist[i];
                    current = i;
                }
            }

            if (current == -1 || current == endIndex)
                break;

            visited[current] = true;

            for (int neighbor = 0; neighbor < count; neighbor++)
            {
                float weight = weights[current, neighbor];

                if (weight < 0f)
                    continue;

                float newDist = dist[current] + weight;

                if (newDist < dist[neighbor])
                {
                    dist[neighbor] = newDist;
                    previous[neighbor] = current;
                }
            }
        }

        if (dist[endIndex] >= float.MaxValue)
            return null;

        List<int> path = new List<int>();
        int node = endIndex;

        while (node != -1)
        {
            path.Add(node);
            node = previous[node];
        }

        path.Reverse();
        return path;
    }
}