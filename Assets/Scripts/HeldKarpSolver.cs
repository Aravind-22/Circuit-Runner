using System;
using System.Collections.Generic;

public class HeldKarpSolver
{
    private float[,] costs;
    private int nodeCount;

    private float[,] dp;
    private int[,] parent;

    public HeldKarpSolver(float[,] costs)
    {
        this.costs = costs;
        nodeCount = costs.GetLength(0);
    }

    public List<int> Solve()
    {
        int pickupCount = nodeCount - 1;
        int stateCount = 1 << pickupCount;

        dp = new float[stateCount, pickupCount];
        parent = new int[stateCount, pickupCount];

        for (int mask = 0; mask < stateCount; mask++)
        {
            for (int i = 0; i < pickupCount; i++)
            {
                dp[mask, i] = float.PositiveInfinity;
                parent[mask, i] = -1;
            }
        }

        // Start -> each pickup
        for (int i = 0; i < pickupCount; i++)
        {
            int mask = 1 << i;

            dp[mask, i] = costs[0, i + 1];
        }

        // DP
        for (int mask = 1; mask < stateCount; mask++)
        {
            for (int current = 0; current < pickupCount; current++)
            {
                if ((mask & (1 << current)) == 0)
                    continue;

                float currentCost = dp[mask, current];

                if (float.IsInfinity(currentCost))
                    continue;

                for (int next = 0; next < pickupCount; next++)
                {
                    if ((mask & (1 << next)) != 0)
                        continue;

                    int newMask = mask | (1 << next);

                    float newCost =
                        currentCost +
                        costs[current + 1, next + 1];

                    if (newCost < dp[newMask, next])
                    {
                        dp[newMask, next] = newCost;
                        parent[newMask, next] = current;
                    }
                }
            }
        }

        int finalMask = stateCount - 1;

        float bestCost = float.PositiveInfinity;
        int bestLast = -1;

        for (int i = 0; i < pickupCount; i++)
        {
            if (dp[finalMask, i] < bestCost)
            {
                bestCost = dp[finalMask, i];
                bestLast = i;
            }
        }

        // Reconstruct
        List<int> route = new List<int>();

        int maskState = finalMask;
        int currentNode = bestLast;

        while (currentNode != -1)
        {
            route.Add(currentNode + 1);

            int previous = parent[maskState, currentNode];

            maskState &= ~(1 << currentNode);

            currentNode = previous;
        }

        route.Reverse();

        return route;
    }
}