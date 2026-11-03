using System.Collections.Generic;

public readonly struct PassageVerdict
{
    public PassageVerdict(PassageDecision decision, string detail)
    {
        Decision = decision;
        Detail = detail;
    }

    public PassageDecision Decision { get; }
    public string Detail { get; }
    public bool Accepted => Decision == PassageDecision.Accepted;
}

public static class PassageChangeRules
{
    public static PassageVerdict Evaluate(
        NavigationGrid grid,
        bool closing,
        int cellX,
        int cellY,
        IReadOnlyList<GridPoint> origins,
        int goalX,
        int goalY,
        bool occupied)
    {
        if (!closing)
        {
            return new PassageVerdict(
                PassageDecision.Accepted,
                "Paso abierto.");
        }

        if (grid == null)
        {
            return Reject("No hay cuadrícula.");
        }

        if (!grid.InBounds(cellX, cellY))
        {
            return Reject("El paso está fuera de la cuadrícula.");
        }

        if (occupied)
        {
            return Reject("Un enemigo ocupa el paso.");
        }

        if (!grid.IsWalkable(cellX, cellY))
        {
            return Reject("El paso ya está cerrado.");
        }

        if (origins == null || origins.Count == 0)
        {
            return Reject(
                "No hay accesos desde los que comprobar la salida.");
        }

        NavigationGrid proposed = grid.WithBlocked(cellX, cellY);

        for (int i = 0; i < origins.Count; i++)
        {
            GridPoint origin = origins[i];
            GridPathResult result = GridPathfinder.Find(
                proposed,
                origin.X,
                origin.Y,
                goalX,
                goalY);

            if (result.Status != GridPathStatus.Found)
            {
                return Reject(
                    "El cambio dejaría un acceso o un enemigo sin salida.");
            }
        }

        return new PassageVerdict(
            PassageDecision.Accepted,
            "Paso cerrado.");
    }

    private static PassageVerdict Reject(string detail)
    {
        return new PassageVerdict(
            PassageDecision.Rejected,
            detail);
    }
}
