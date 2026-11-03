using System;

public static class CorridorMotion
{
    public static bool SegmentAllowed(
        NavigationGrid grid,
        float originX,
        float originY,
        float cellSize,
        float fromX,
        float fromY,
        float toX,
        float toY,
        float radius)
    {
        if (grid == null || cellSize <= 0f)
        {
            return false;
        }

        if (!IsFinite(fromX) || !IsFinite(fromY) ||
            !IsFinite(toX) || !IsFinite(toY) ||
            !IsFinite(radius))
        {
            return false;
        }

        if (radius < 0f)
        {
            radius = 0f;
        }

        float deltaX = toX - fromX;
        float deltaY = toY - fromY;
        float length = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        float maxStep = cellSize * 0.25f;

        if (radius > 0f && radius * 0.5f < maxStep)
        {
            maxStep = radius * 0.5f;
        }

        if (maxStep < 0.02f)
        {
            maxStep = 0.02f;
        }

        int steps = length <= 0.00001f
            ? 1
            : (int)Math.Ceiling(length / maxStep);

        if (steps > 12)
        {
            return false;
        }

        if (!TryCell(originX, cellSize, fromX, out int previousX) ||
            !TryCell(originY, cellSize, fromY, out int previousY) ||
            !Clear(grid, originX, originY, cellSize, fromX, fromY, radius))
        {
            return false;
        }

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float x = fromX + deltaX * t;
            float y = fromY + deltaY * t;

            if (!TryCell(originX, cellSize, x, out int cellX) ||
                !TryCell(originY, cellSize, y, out int cellY) ||
                !Clear(grid, originX, originY, cellSize, x, y, radius))
            {
                return false;
            }

            int stepX = cellX - previousX;
            int stepY = cellY - previousY;
            int crossed = Math.Abs(stepX) + Math.Abs(stepY);

            if (crossed > 1)
            {
                return false;
            }

            previousX = cellX;
            previousY = cellY;
        }

        return true;
    }

    public static bool Clear(
        NavigationGrid grid,
        float originX,
        float originY,
        float cellSize,
        float x,
        float y,
        float radius)
    {
        if (!Inside(grid, originX, originY, cellSize, x, y))
        {
            return false;
        }

        if (radius <= 0f)
        {
            return true;
        }

        float diagonal = radius * 0.70710678f;
        return Inside(grid, originX, originY, cellSize, x + radius, y) &&
            Inside(grid, originX, originY, cellSize, x - radius, y) &&
            Inside(grid, originX, originY, cellSize, x, y + radius) &&
            Inside(grid, originX, originY, cellSize, x, y - radius) &&
            Inside(grid, originX, originY, cellSize, x + diagonal, y + diagonal) &&
            Inside(grid, originX, originY, cellSize, x + diagonal, y - diagonal) &&
            Inside(grid, originX, originY, cellSize, x - diagonal, y + diagonal) &&
            Inside(grid, originX, originY, cellSize, x - diagonal, y - diagonal);
    }

    private static bool Inside(
        NavigationGrid grid,
        float originX,
        float originY,
        float cellSize,
        float x,
        float y)
    {
        if (!TryCell(originX, cellSize, x, out int cellX) ||
            !TryCell(originY, cellSize, y, out int cellY))
        {
            return false;
        }

        return grid.IsWalkable(cellX, cellY);
    }

    private static bool TryCell(
        float origin,
        float cellSize,
        float world,
        out int cell)
    {
        if (cellSize <= 0f || !IsFinite(world) || !IsFinite(origin))
        {
            cell = 0;
            return false;
        }

        cell = (int)Math.Floor((world - origin) / cellSize);
        return true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
