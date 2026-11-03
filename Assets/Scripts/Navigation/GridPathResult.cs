using System;
using System.Collections.Generic;

public sealed class GridPathResult
{
    private static readonly GridStep[] Empty = Array.Empty<GridStep>();

    private GridPathResult(
        GridPathStatus status,
        IReadOnlyList<GridStep> steps,
        int totalCost,
        string detail)
    {
        Status = status;
        Steps = steps;
        TotalCost = totalCost;
        Detail = detail;
    }

    public GridPathStatus Status { get; }
    public IReadOnlyList<GridStep> Steps { get; }
    public int TotalCost { get; }
    public string Detail { get; }

    public static GridPathResult Found(
        List<GridStep> steps,
        int totalCost)
    {
        return new GridPathResult(
            GridPathStatus.Found,
            steps,
            totalCost,
            "Ruta encontrada.");
    }

    public static GridPathResult NoPath()
    {
        return new GridPathResult(
            GridPathStatus.NoPath,
            Empty,
            0,
            "No hay ruta.");
    }

    public static GridPathResult Invalid(string detail)
    {
        return new GridPathResult(
            GridPathStatus.InvalidRequest,
            Empty,
            0,
            detail);
    }
}
