using System;

public sealed class NavigationGrid
{
    private readonly bool[] walkable;
    private readonly int[] costs;

    public NavigationGrid(
        int width,
        int height,
        bool[] walkable,
        int[] costs)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        int count = width * height;

        if (walkable == null ||
            costs == null ||
            walkable.Length != count ||
            costs.Length != count)
        {
            throw new ArgumentException(
                "La cuadrícula no coincide con sus celdas.");
        }

        Width = width;
        Height = height;
        this.walkable = (bool[])walkable.Clone();
        this.costs = (int[])costs.Clone();
    }

    public int Width { get; }
    public int Height { get; }

    public bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }

    public bool IsWalkable(int x, int y)
    {
        if (!InBounds(x, y))
        {
            return false;
        }

        return walkable[Index(x, y)];
    }

    public int CostAt(int x, int y)
    {
        if (!InBounds(x, y))
        {
            return 0;
        }

        return costs[Index(x, y)];
    }

    public int MinimumStepCost()
    {
        int minimum = 0;

        for (int i = 0; i < walkable.Length; i++)
        {
            if (!walkable[i])
            {
                continue;
            }

            if (costs[i] <= 0)
            {
                return -1;
            }

            if (minimum == 0 || costs[i] < minimum)
            {
                minimum = costs[i];
            }
        }

        return minimum;
    }

    public NavigationGrid WithBlocked(int x, int y)
    {
        bool[] nextWalkable = (bool[])walkable.Clone();
        int[] nextCosts = (int[])costs.Clone();

        if (InBounds(x, y))
        {
            nextWalkable[Index(x, y)] = false;
        }

        return new NavigationGrid(
            Width,
            Height,
            nextWalkable,
            nextCosts);
    }

    private int Index(int x, int y)
    {
        return y * Width + x;
    }
}
