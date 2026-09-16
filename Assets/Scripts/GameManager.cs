using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private LevelGenerator levelGenerator;
    [SerializeField] private VisibilityGraphPathfinder pathfinder;
    [SerializeField] private PlayerController player;
    [SerializeField] private GhostController ghost;
    [SerializeField] private OptimalPathRenderer pathRenderer;

    [Header("UI")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text playerTimeText;
    [SerializeField] private TMP_Text ghostTimeText;
    [SerializeField] private Toggle optimalPathToggle;
    [SerializeField] private GameObject gameOverPanel;

    public bool GameRunning { get; private set; }

    private float timer;
    private float playerFinishTime;
    private float ghostFinishTime;

    private int collectedCount;

    private List<Vector3> optimalRoute = new List<Vector3>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        optimalPathToggle.onValueChanged.AddListener(
            pathRenderer.SetVisible
        );

        NewLevel();
    }

    private void Update()
    {
        if (!GameRunning)
            return;

        timer += Time.deltaTime;

        timerText.text = $"TIME: {timer:F2}";
    }

    public void NewLevel()
    {
        StartLevel(false);
    }

    public void Replay()
    {
        StartLevel(true);
    }

    private void StartLevel(bool replay)
    {
        GameRunning = false;

        timer = 0f;
        collectedCount = 0;

        playerFinishTime = 0f;
        ghostFinishTime = 0f;

        gameOverPanel.SetActive(false);

        pathRenderer.Clear();

        // Generate or restore layout.
        if (replay)
            levelGenerator.ReplayLevel();
        else
            levelGenerator.GenerateNewLevel();

        // VERY IMPORTANT:
        // Obstacles now exist, so rebuild A* grid.
        // pathfinder.BuildGrid();

        // Reset characters.
        player.ResetPosition(
            levelGenerator.PlayerStart
        );

        ghost.ResetGhost(
            levelGenerator.GhostStart
        );

        // Calculate DP route.
        CalculateOptimalRoute();

        // Start game.
        GameRunning = true;

        ghost.StartRoute(
            levelGenerator.GhostStart,
            optimalRoute
        );
    }

    private void CalculateOptimalRoute()
    {
        List<Pickup> pickups = levelGenerator.Pickups;

        int nodeCount = pickups.Count + 1;

        float[,] costs = new float[nodeCount, nodeCount];

        Vector3[] positions = new Vector3[nodeCount];

        positions[0] = levelGenerator.GhostStart;

        for (int i = 0; i < pickups.Count; i++)
            positions[i + 1] = pickups[i].transform.position;

        // Calculate every pair's real path cost.
        for (int i = 0; i < nodeCount; i++)
        {
            for (int j = 0; j < nodeCount; j++)
            {
                if (i == j)
                {
                    costs[i, j] = 0f;
                    continue;
                }

                costs[i, j] =
                    pathfinder.GetPathCost(
                        positions[i],
                        positions[j]
                    );
            }
        }

        HeldKarpSolver solver =
            new HeldKarpSolver(costs);

        List<int> routeIndices = solver.Solve();

        optimalRoute.Clear();

        foreach (int index in routeIndices)
            optimalRoute.Add(positions[index]);

        pathRenderer.ShowPath(
            levelGenerator.GhostStart,
            optimalRoute
        );

        optimalPathToggle.isOn = false;
    }

    public void CollectPickup(Pickup pickup)
    {
        if (!GameRunning)
            return;

        if (!pickup.gameObject.activeSelf)
            return;

        pickup.gameObject.SetActive(false);

        collectedCount++;

        if (collectedCount >= levelGenerator.Pickups.Count)
            PlayerFinished();
    }

    private void PlayerFinished()
    {
        playerFinishTime = timer;

        GameRunning = false;

        ShowGameOver();
    }

    public void GhostFinished()
    {
        ghostFinishTime = timer;

        if (!GameRunning)
            return;

        // If ghost finishes first, the game continues
        // until player finishes.
    }

    private void ShowGameOver()
    {
        gameOverPanel.SetActive(true);

        playerTimeText.text =
            $"PLAYER: {playerFinishTime:F2}s";

        ghostTimeText.text =
            $"GHOST: {ghostFinishTime:F2}s";
    }
}