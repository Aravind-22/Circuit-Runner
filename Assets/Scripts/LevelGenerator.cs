using System.Collections.Generic;
using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    [Header("Pickups")]
    [SerializeField] private Pickup pickupPrefab;
    [SerializeField] private Transform pickupParent;
    [SerializeField] private int minPickups = 8;
    [SerializeField] private int maxPickups = 12;

    [Header("Obstacles")]
    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private Transform obstacleParent;
    [SerializeField] private int minObstacles = 3;
    [SerializeField] private int maxObstacles = 6;

    [Header("Spawn Area")]
    [SerializeField] private float spawnMin = -8f;
    [SerializeField] private float spawnMax = 8f;
    [SerializeField] private float spawnY = 0.1f;

    [Header("Obstacle Settings")]
    [SerializeField] private float obstacleMinSize = 1.5f;
    [SerializeField] private float obstacleMaxSize = 3.5f;

    [Header("Pickup Clearance")]
    [SerializeField] private float pickupRadius = 0.6f;
    [SerializeField] private float minSpacing = 1.5f;

    public List<Pickup> Pickups { get; private set; } = new List<Pickup>();

    public Vector3 PlayerStart { get; private set; }
    public Vector3 GhostStart { get; private set; }

    private readonly List<GameObject> generatedObstacles = new List<GameObject>();

    // Plain-math XZ bounds for every live obstacle. Used for spawn-overlap
    // checks AND fed straight into the pathfinder — one source of truth,
    // no dependency on colliders/layers/physics query timing.
    private readonly List<ObstacleBounds2D> obstacleBounds = new List<ObstacleBounds2D>();

    private struct ObstacleBounds2D
    {
        public Vector2 center;
        public Vector2 halfExtents;

        public ObstacleBounds2D(Vector2 center, Vector2 halfExtents)
        {
            this.center = center;
            this.halfExtents = halfExtents;
        }
    }

    // Saved layout
    private readonly List<Vector3> savedPickupPositions = new List<Vector3>();
    private readonly List<ObstacleData> savedObstacles = new List<ObstacleData>();

    private Vector3 savedPlayerStart;
    private Vector3 savedGhostStart;

    private bool hasSavedLevel;

    [System.Serializable]
    private struct ObstacleData
    {
        public Vector3 position;
        public Vector3 scale;
        public Quaternion rotation;

        public ObstacleData(Vector3 position, Vector3 scale, Quaternion rotation)
        {
            this.position = position;
            this.scale = scale;
            this.rotation = rotation;
        }
    }

    // =========================================================
    // NEW LEVEL
    // =========================================================

    public void GenerateNewLevel()
    {
        ClearCurrentLevel();

        GenerateObstacles();
        GenerateStartPositions();
        GeneratePickups();

        SaveCurrentLayout();
    }

    // =========================================================
    // REPLAY
    // =========================================================

    public void ReplayLevel()
    {
        if (!hasSavedLevel)
        {
            GenerateNewLevel();
            return;
        }

        ClearCurrentLevel();

        foreach (ObstacleData data in savedObstacles)
        {
            GameObject obstacle = Instantiate(
                obstaclePrefab,
                data.position,
                data.rotation,
                obstacleParent);

            obstacle.transform.localScale = data.scale;

            generatedObstacles.Add(obstacle);
            RegisterObstacleBounds(obstacle);
        }

        PlayerStart = savedPlayerStart;
        GhostStart = savedGhostStart;

        for (int i = 0; i < savedPickupPositions.Count; i++)
        {
            Pickup pickup = Instantiate(
                pickupPrefab,
                savedPickupPositions[i],
                Quaternion.identity,
                pickupParent);

            pickup.index = i;
            Pickups.Add(pickup);
        }
    }

    // =========================================================
    // OBSTACLES
    // =========================================================

    private void GenerateObstacles()
    {
        int count = Random.Range(minObstacles, maxObstacles + 1);

        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < 100; attempt++)
            {
                Vector3 position = new Vector3(
                    Random.Range(spawnMin, spawnMax),
                    1f,
                    Random.Range(spawnMin, spawnMax));

                float width = Random.Range(obstacleMinSize, obstacleMaxSize);
                float depth = Random.Range(obstacleMinSize, obstacleMaxSize);
                Vector3 scale = new Vector3(width, 2f, depth);

                if (new Vector2(position.x, position.z).magnitude < 2f)
                    continue;

                Vector2 candidateCenter = new Vector2(position.x, position.z);
                Vector2 candidateHalfExtents = new Vector2(width, depth) * 0.5f + Vector2.one * 0.2f;

                if (BoxOverlapsAnyObstacle(candidateCenter, candidateHalfExtents))
                    continue;

                GameObject obstacle = Instantiate(
                    obstaclePrefab,
                    position,
                    Quaternion.identity,
                    obstacleParent);

                obstacle.transform.localScale = scale;

                generatedObstacles.Add(obstacle);
                RegisterObstacleBounds(obstacle);

                break;
            }
        }
    }

    private void RegisterObstacleBounds(GameObject obstacle)
    {
        Vector3 position = obstacle.transform.position;
        Vector3 halfExtents = obstacle.transform.localScale * 0.5f;

        obstacleBounds.Add(new ObstacleBounds2D(
            new Vector2(position.x, position.z),
            new Vector2(halfExtents.x, halfExtents.z)));

        VisibilityGraphPathfinder.Instance?.RegisterObstacle(position, halfExtents);
    }

    private bool BoxOverlapsAnyObstacle(Vector2 center, Vector2 halfExtents)
    {
        foreach (ObstacleBounds2D bounds in obstacleBounds)
        {
            bool overlapX = Mathf.Abs(center.x - bounds.center.x) < (halfExtents.x + bounds.halfExtents.x);
            bool overlapZ = Mathf.Abs(center.y - bounds.center.y) < (halfExtents.y + bounds.halfExtents.y);

            if (overlapX && overlapZ)
                return true;
        }

        return false;
    }

    private bool CircleOverlapsAnyObstacle(Vector2 point, float radius)
    {
        foreach (ObstacleBounds2D bounds in obstacleBounds)
        {
            Vector2 closest = new Vector2(
                Mathf.Clamp(point.x, bounds.center.x - bounds.halfExtents.x, bounds.center.x + bounds.halfExtents.x),
                Mathf.Clamp(point.y, bounds.center.y - bounds.halfExtents.y, bounds.center.y + bounds.halfExtents.y));

            if ((point - closest).sqrMagnitude < radius * radius)
                return true;
        }

        return false;
    }

    // =========================================================
    // START POSITIONS
    // =========================================================

    private void GenerateStartPositions()
    {
        PlayerStart = GetValidPosition();
        GhostStart = PlayerStart;
    }

    // =========================================================
    // PICKUPS
    // =========================================================

    private void GeneratePickups()
    {
        int count = Random.Range(minPickups, maxPickups + 1);

        for (int i = 0; i < count; i++)
        {
            Vector3 position = GetValidPosition();

            Pickup pickup = Instantiate(
                pickupPrefab,
                position,
                Quaternion.identity,
                pickupParent);

            pickup.index = i;
            Pickups.Add(pickup);
        }
    }

    // =========================================================
    // VALID POSITION
    // =========================================================

    private Vector3 GetValidPosition()
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            Vector3 position = new Vector3(
                Random.Range(spawnMin, spawnMax),
                spawnY,
                Random.Range(spawnMin, spawnMax));

            Vector2 point2D = new Vector2(position.x, position.z);

            if (CircleOverlapsAnyObstacle(point2D, pickupRadius))
                continue;

            if (Vector3.Distance(position, PlayerStart) < minSpacing)
                continue;

            if (Vector3.Distance(position, GhostStart) < minSpacing)
                continue;

            bool overlapsPickup = false;

            foreach (Pickup pickup in Pickups)
            {
                if (pickup == null)
                    continue;

                if (Vector3.Distance(position, pickup.transform.position) < minSpacing)
                {
                    overlapsPickup = true;
                    break;
                }
            }

            if (overlapsPickup)
                continue;

            return position;
        }

        // Should almost never happen.
        return new Vector3(
            Random.Range(spawnMin, spawnMax),
            spawnY,
            Random.Range(spawnMin, spawnMax));
    }

    // =========================================================
    // SAVE LEVEL
    // =========================================================

    private void SaveCurrentLayout()
    {
        savedPickupPositions.Clear();
        savedObstacles.Clear();

        savedPlayerStart = PlayerStart;
        savedGhostStart = GhostStart;

        foreach (Pickup pickup in Pickups)
        {
            savedPickupPositions.Add(pickup.transform.position);
        }

        foreach (GameObject obstacle in generatedObstacles)
        {
            savedObstacles.Add(new ObstacleData(
                obstacle.transform.position,
                obstacle.transform.localScale,
                obstacle.transform.rotation));
        }

        hasSavedLevel = true;
    }

    // =========================================================
    // CLEAR
    // =========================================================

    private void ClearCurrentLevel()
    {
        foreach (Pickup pickup in Pickups)
        {
            if (pickup != null)
                Destroy(pickup.gameObject);
        }

        Pickups.Clear();

        foreach (GameObject obstacle in generatedObstacles)
        {
            if (obstacle != null)
                Destroy(obstacle);
        }

        generatedObstacles.Clear();
        obstacleBounds.Clear();

        VisibilityGraphPathfinder.Instance?.ClearObstacles();
    }
}