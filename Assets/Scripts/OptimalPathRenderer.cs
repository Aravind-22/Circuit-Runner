using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class OptimalPathRenderer : MonoBehaviour
{
    [SerializeField] private int smoothPointsPerCorner = 6;
    [SerializeField] private float cornerCutDistance = 0.3f; // clamped to pathfinder.AgentRadius at runtime so rounding never dips back into the obstacle buffer
    [SerializeField] private float lineHeight = 0.05f;

    private LineRenderer line;
    private VisibilityGraphPathfinder pathfinder;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.numCornerVertices = 8;
        line.numCapVertices = 8;
    }

    // Draws the exact route the ghost will travel — same pathfinder, same
    // start/route — so this can never diverge from, or cut through
    // anything, that the ghost's own path avoids.
    public void ShowPath(Vector3 start, List<Vector3> route)
    {
        if (pathfinder == null)
            pathfinder = VisibilityGraphPathfinder.Instance;

        cornerCutDistance = Mathf.Min(cornerCutDistance, pathfinder.AgentRadius * 0.8f);

        List<Vector3> rawPath = pathfinder.FindRoute(start, route);

        if (rawPath == null || rawPath.Count == 0)
        {
            line.positionCount = 0;
            return;
        }

        List<Vector3> smoothPath = SmoothCorners(rawPath);

        line.positionCount = smoothPath.Count;

        for (int i = 0; i < smoothPath.Count; i++)
        {
            Vector3 position = smoothPath[i];
            position.y += lineHeight;
            line.SetPosition(i, position);
        }
    }

    private List<Vector3> SmoothCorners(List<Vector3> path)
    {
        if (path.Count < 3)
            return path;

        List<Vector3> result = new List<Vector3> { path[0] };

        for (int i = 1; i < path.Count - 1; i++)
        {
            Vector3 previous = path[i - 1];
            Vector3 current = path[i];
            Vector3 next = path[i + 1];

            // Cut in by an ABSOLUTE distance (not a fraction of segment
            // length) so long segments can't produce a rounding radius
            // bigger than the obstacle clearance we planned for.
            float cutIn = Mathf.Min(cornerCutDistance, Vector3.Distance(current, previous) * 0.5f);
            float cutOut = Mathf.Min(cornerCutDistance, Vector3.Distance(current, next) * 0.5f);

            Vector3 beforeCorner = current + (previous - current).normalized * cutIn;
            Vector3 afterCorner = current + (next - current).normalized * cutOut;

            result.Add(beforeCorner);

            for (int j = 1; j <= smoothPointsPerCorner; j++)
            {
                float t = j / (float)(smoothPointsPerCorner + 1);
                result.Add(QuadraticBezier(beforeCorner, current, afterCorner, t));
            }

            result.Add(afterCorner);
        }

        result.Add(path[path.Count - 1]);
        return result;
    }

    private Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    public void SetVisible(bool visible) => line.enabled = visible;

    public void Clear() => line.positionCount = 0;
}