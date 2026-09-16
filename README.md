# Circuit Runner

A best-path routing system solved with Held-Karp bitmask dynamic programming.

## Features
- Cost matrix built between waypoints/nodes
- Held-Karp bitmask DP computes the exact optimal route (not a heuristic approximation)
- A ghost entity follows the computed best path in real time

## Why this approach
Held-Karp guarantees the mathematically optimal path over a bounded set of nodes — used here instead of greedy or heuristic pathing to demonstrate exact DP-based routing under a defined cost structure.

## Stack
Unity, C#, custom DP solver
