using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float turnSharpness = 10f; // higher = snappier turns; frame-rate independent (1 - e^-kt), not deg/sec like before
    [SerializeField] private float lookaheadDistance = 0.5f; // kept <= pathfinder.AgentRadius at runtime so corner-cutting never eats into the obstacle buffer
    [SerializeField] private float arrivalDistance = 0.15f;

    private VisibilityGraphPathfinder pathfinder;
    private Coroutine movementRoutine;

    private List<Vector3> currentPath;
    private float[] cumulativeDistance;
    private float pathLength;
    private float progress;

    public void StartRoute(Vector3 start, List<Vector3> pickupRoute)
    {
        if (pathfinder == null)
            pathfinder = VisibilityGraphPathfinder.Instance;

        if (movementRoutine != null)
            StopCoroutine(movementRoutine);

        transform.position = start;

        lookaheadDistance = Mathf.Min(lookaheadDistance, pathfinder.AgentRadius);

        currentPath = pathfinder.FindRoute(start, pickupRoute);

        if (currentPath == null || currentPath.Count < 2)
        {
            GameManager.Instance.GhostFinished();
            return;
        }

        BuildArcLengthTable();

        movementRoutine = StartCoroutine(FollowPath());
    }

    private void BuildArcLengthTable()
    {
        cumulativeDistance = new float[currentPath.Count];
        cumulativeDistance[0] = 0f;

        for (int i = 1; i < currentPath.Count; i++)
        {
            float segment = Vector3.Distance(currentPath[i - 1], currentPath[i]);
            cumulativeDistance[i] = cumulativeDistance[i - 1] + segment;
        }

        pathLength = cumulativeDistance[cumulativeDistance.Length - 1];
        progress = 0f;
    }

    // Pure-pursuit follower: steer toward a point a fixed arc-length ahead
    // on the path rather than snapping onto each waypoint in turn. That's
    // what produces continuous curved turns through corners instead of
    // stop-pivot-go movement.
    private IEnumerator FollowPath()
    {
        while (progress < pathLength - arrivalDistance)
        {
            Vector3 target = GetPointAtDistance(progress + lookaheadDistance);
            target.y = transform.position.y;

            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
            }

            transform.position += transform.forward * (moveSpeed * Time.deltaTime);

            progress = ClosestProgressOnPath(transform.position);

            yield return null;
        }

        // Snap to the exact final point so the last pickup lines up precisely.
        transform.position = currentPath[currentPath.Count - 1];

        GameManager.Instance.GhostFinished();
    }

    private Vector3 GetPointAtDistance(float distance)
    {
        distance = Mathf.Clamp(distance, 0f, pathLength);

        for (int i = 1; i < cumulativeDistance.Length; i++)
        {
            if (distance <= cumulativeDistance[i])
            {
                float segmentLength = cumulativeDistance[i] - cumulativeDistance[i - 1];
                float t = segmentLength > 0.0001f
                    ? (distance - cumulativeDistance[i - 1]) / segmentLength
                    : 0f;

                return Vector3.Lerp(currentPath[i - 1], currentPath[i], t);
            }
        }

        return currentPath[currentPath.Count - 1];
    }

    // Re-projects the ghost's actual position onto the path's arc length
    // so steering deviation near corners doesn't let progress drift or
    // stall.
    private float ClosestProgressOnPath(Vector3 position)
    {
        float best = progress;
        float bestSqrDist = float.MaxValue;

        int startIndex = 0;

        for (int i = 0; i < cumulativeDistance.Length; i++)
        {
            if (cumulativeDistance[i] >= progress - 1f)
            {
                startIndex = Mathf.Max(0, i - 1);
                break;
            }
        }

        for (int i = startIndex; i < currentPath.Count - 1; i++)
        {
            Vector3 a = currentPath[i];
            Vector3 b = currentPath[i + 1];
            Vector3 ab = b - a;
            float segLength = ab.magnitude;

            float t = segLength > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(position - a, ab) / (segLength * segLength))
                : 0f;

            Vector3 closest = a + ab * t;
            float sqrDist = (position - closest).sqrMagnitude;

            if (sqrDist < bestSqrDist)
            {
                bestSqrDist = sqrDist;
                best = cumulativeDistance[i] + t * segLength;
            }
        }

        return Mathf.Max(best, progress); // never move backward along the path
    }

    public void ResetGhost(Vector3 position)
    {
        if (movementRoutine != null)
            StopCoroutine(movementRoutine);

        transform.position = position;
        transform.rotation = Quaternion.identity;

        currentPath = null;
        progress = 0f;
    }
}