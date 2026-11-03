using System;
using System.Collections.Generic;

public struct ExperienceCell
{
    public int X;
    public int Y;
    public bool Walkable;
    public int Kind;
    public int Cost;
}

public static class ExperienceLayout
{
    public const int Path = 0;
    public const int Bridge = 1;
    public const int Channel = 2;

    public const int Width = 16;
    public const int Height = 10;
    public const float CellSize = 1f;
    public const float OriginX = -8f;
    public const float OriginY = -5f;
    public const float AgentRadius = 0.22f;
    public const float SynergyDistance = 2.5f;
    public const float MinimumPlotGap = 1.75f;
    public const float GuardianRange = 2.1f;
    public const float WatcherRange = 3.8f;

    public const int TopSpawnX = 1;
    public const int TopSpawnY = 8;
    public const int BottomSpawnX = 1;
    public const int BottomSpawnY = 1;
    public const int GoalX = 14;
    public const int GoalY = 5;
    public const int PassageX = 8;
    public const int PassageY = 5;

    private static readonly int[] PathCells =
    {
        1, 8, 2, 8, 3, 8, 4, 8,
        4, 6, 5, 6, 6, 6, 6, 5, 7, 5, 8, 5, 9, 5, 10, 5, 11, 5, 14, 5,
        1, 1, 2, 1, 3, 1, 4, 1, 4, 3, 5, 3, 6, 3, 6, 4
    };

    private static readonly int[] BridgeCells =
    {
        4, 7, 4, 2, 7, 6, 8, 6, 9, 6, 12, 5, 13, 5
    };

    private static readonly int[] ChannelCells =
    {
        2, 7, 3, 7, 5, 7, 6, 7, 7, 7, 8, 7,
        2, 2, 3, 2, 5, 2, 6, 2, 7, 2,
        8, 4, 9, 4, 10, 4, 11, 4, 12, 4, 13, 4, 14, 4,
        10, 6, 11, 6, 12, 6, 13, 6, 14, 6
    };

    private static readonly int[] PlotCells =
    {
        0, 9, 2, 9, 5, 9, 8, 9,
        0, 0, 2, 0, 5, 0, 8, 0,
        5, 4, 7, 3, 10, 7, 11, 3,
        14, 7, 14, 3
    };

    public static ExperienceCell[] Cells()
    {
        var cells = new List<ExperienceCell>();
        Add(cells, PathCells, Path, true);
        Add(cells, BridgeCells, Bridge, true);
        Add(cells, ChannelCells, Channel, false);
        return cells.ToArray();
    }

    public static int[] Plots()
    {
        var copy = new int[PlotCells.Length];
        Array.Copy(PlotCells, copy, PlotCells.Length);
        return copy;
    }

    public static string Validate()
    {
        ExperienceCell[] cells = Cells();
        var problems = new List<string>();
        var seen = new HashSet<int>();

        for (int i = 0; i < cells.Length; i++)
        {
            ExperienceCell cell = cells[i];

            if (cell.X < 0 || cell.Y < 0 || cell.X >= Width || cell.Y >= Height)
            {
                problems.Add("Celda fuera de la cuadrícula.");
                continue;
            }

            if (!seen.Add(cell.Y * Width + cell.X))
            {
                problems.Add("Celda repetida " + cell.X + "," + cell.Y + ".");
            }
        }

        int[] plots = Plots();

        if (plots.Length != 28)
        {
            problems.Add("Se esperaban 14 parcelas.");
        }

        int upper = 0;
        int lower = 0;
        int confluence = 0;
        int finalStretch = 0;

        for (int i = 0; i < plots.Length; i += 2)
        {
            int x = plots[i];
            int y = plots[i + 1];

            if (Occupied(cells, x, y))
            {
                problems.Add("La parcela " + x + "," + y + " invade el corredor.");
            }

            if (y >= 8)
            {
                upper++;
            }
            else if (y <= 1)
            {
                lower++;
            }
            else if (x >= 13)
            {
                finalStretch++;
            }
            else if (Math.Abs(x - PassageX) <= 4 && Math.Abs(y - PassageY) <= 3)
            {
                confluence++;
            }
        }

        if (upper != 4 || lower != 4 || confluence != 4 || finalStretch != 2)
        {
            problems.Add(
                "Sectores " + upper + "/" + lower + "/" +
                confluence + "/" + finalStretch + ".");
        }

        bool closePair = false;
        bool farPair = false;
        float nearest = 99f;

        for (int i = 0; i < plots.Length; i += 2)
        {
            for (int j = i + 2; j < plots.Length; j += 2)
            {
                float distance = Distance(
                    plots[i],
                    plots[i + 1],
                    plots[j],
                    plots[j + 1]);
                nearest = Math.Min(nearest, distance);

                if (distance <= SynergyDistance)
                {
                    closePair = true;
                }

                if (distance > SynergyDistance)
                {
                    farPair = true;
                }
            }
        }

        if (!closePair || !farPair || nearest < MinimumPlotGap)
        {
            problems.Add("Distancias de parcela " + nearest.ToString("0.00") + ".");
        }

        NavigationGrid open = Grid(cells, true);
        NavigationGrid closed = Grid(cells, false);
        GridPathResult topOpen = Find(open, TopSpawnX, TopSpawnY);
        GridPathResult topClosed = Find(closed, TopSpawnX, TopSpawnY);
        GridPathResult bottomOpen = Find(open, BottomSpawnX, BottomSpawnY);
        GridPathResult bottomClosed = Find(closed, BottomSpawnX, BottomSpawnY);

        if (topOpen.Status != GridPathStatus.Found ||
            bottomOpen.Status != GridPathStatus.Found ||
            topClosed.Status != GridPathStatus.Found ||
            bottomClosed.Status != GridPathStatus.Found)
        {
            problems.Add("Falta una ruta terrestre.");
        }
        else
        {
            if (!Uses(topOpen, PassageX, PassageY) ||
                !Uses(bottomOpen, PassageX, PassageY) ||
                Uses(topClosed, PassageX, PassageY) ||
                Uses(bottomClosed, PassageX, PassageY) ||
                !UsesBridge(topClosed, cells) ||
                !UsesBridge(bottomClosed, cells))
            {
                problems.Add("La alternativa no cambia el recorrido.");
            }

            if (!Corridor(open, topOpen) || !Corridor(open, bottomOpen))
            {
                problems.Add("El radio del agente no cabe en el corredor.");
            }

            if (!Covered(plots, topOpen) || !Covered(plots, bottomOpen))
            {
                problems.Add("La cobertura no deja desafío.");
            }
        }

        if (!ChannelsConnected(cells))
        {
            problems.Add("Hay un canal partido.");
        }

        return problems.Count == 0 ? null : string.Join(" ", problems);
    }

    private static void Add(
        List<ExperienceCell> cells,
        int[] coordinates,
        int kind,
        bool walkable)
    {
        for (int i = 0; i < coordinates.Length; i += 2)
        {
            cells.Add(new ExperienceCell
            {
                X = coordinates[i],
                Y = coordinates[i + 1],
                Walkable = walkable,
                Kind = kind,
                Cost = 1
            });
        }
    }

    private static bool Occupied(ExperienceCell[] cells, int x, int y)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].X == x && cells[i].Y == y)
            {
                return true;
            }
        }

        return false;
    }

    private static NavigationGrid Grid(ExperienceCell[] cells, bool passageOpen)
    {
        int count = Width * Height;
        var walkable = new bool[count];
        var costs = new int[count];

        for (int i = 0; i < count; i++)
        {
            costs[i] = 1;
        }

        for (int i = 0; i < cells.Length; i++)
        {
            ExperienceCell cell = cells[i];
            bool open = cell.Walkable;

            if (!passageOpen && cell.X == PassageX && cell.Y == PassageY)
            {
                open = false;
            }

            int index = cell.Y * Width + cell.X;
            walkable[index] = open;
            costs[index] = cell.Cost;
        }

        return new NavigationGrid(Width, Height, walkable, costs);
    }

    private static GridPathResult Find(NavigationGrid grid, int x, int y)
    {
        return GridPathfinder.Find(grid, x, y, GoalX, GoalY);
    }

    private static bool Uses(GridPathResult route, int x, int y)
    {
        for (int i = 0; i < route.Steps.Count; i++)
        {
            if (route.Steps[i].X == x && route.Steps[i].Y == y)
            {
                return true;
            }
        }

        return false;
    }

    private static bool UsesBridge(GridPathResult route, ExperienceCell[] cells)
    {
        for (int i = 0; i < route.Steps.Count; i++)
        {
            for (int c = 0; c < cells.Length; c++)
            {
                if (cells[c].Kind == Bridge &&
                    cells[c].X == route.Steps[i].X &&
                    cells[c].Y == route.Steps[i].Y)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool Corridor(NavigationGrid grid, GridPathResult route)
    {
        for (int i = 1; i < route.Steps.Count; i++)
        {
            GridStep from = route.Steps[i - 1];
            GridStep to = route.Steps[i];

            if (!CorridorMotion.SegmentAllowed(
                    grid,
                    OriginX,
                    OriginY,
                    CellSize,
                    OriginX + (from.X + 0.5f) * CellSize,
                    OriginY + (from.Y + 0.5f) * CellSize,
                    OriginX + (to.X + 0.5f) * CellSize,
                    OriginY + (to.Y + 0.5f) * CellSize,
                    AgentRadius))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Covered(int[] plots, GridPathResult route)
    {
        int outsideGuardian = 0;
        int withinWatcher = 0;
        int widest = 0;
        int steps = route.Steps.Count;

        for (int p = 0; p < plots.Length; p += 2)
        {
            int covered = 0;

            for (int i = 0; i < steps; i++)
            {
                if (Distance(
                        route.Steps[i].X,
                        route.Steps[i].Y,
                        plots[p],
                        plots[p + 1]) <= WatcherRange)
                {
                    covered++;
                }
            }

            if (covered > widest)
            {
                widest = covered;
            }
        }

        for (int i = 0; i < steps; i++)
        {
            float nearest = 99f;

            for (int p = 0; p < plots.Length; p += 2)
            {
                nearest = Math.Min(
                    nearest,
                    Distance(
                        route.Steps[i].X,
                        route.Steps[i].Y,
                        plots[p],
                        plots[p + 1]));
            }

            if (nearest <= WatcherRange)
            {
                withinWatcher++;
            }

            if (nearest > GuardianRange)
            {
                outsideGuardian++;
            }
        }

        return outsideGuardian > 0 &&
            withinWatcher > 0 &&
            widest < steps * 0.72f;
    }

    private static bool ChannelsConnected(ExperienceCell[] cells)
    {
        var water = new List<ExperienceCell>();

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].Kind == Channel)
            {
                water.Add(cells[i]);
            }
        }

        if (water.Count < 6)
        {
            return false;
        }

        var seen = new bool[water.Count];
        int groups = 0;

        for (int i = 0; i < water.Count; i++)
        {
            if (seen[i])
            {
                continue;
            }

            groups++;
            var stack = new Stack<int>();
            stack.Push(i);
            seen[i] = true;
            int size = 0;

            while (stack.Count > 0)
            {
                int current = stack.Pop();
                size++;

                for (int j = 0; j < water.Count; j++)
                {
                    if (seen[j])
                    {
                        continue;
                    }

                    int dx = Math.Abs(water[current].X - water[j].X);
                    int dy = Math.Abs(water[current].Y - water[j].Y);
                    bool beside = dx + dy == 1;
                    bool acrossBridge = false;

                    if (dx + dy == 2 && (dx == 0 || dy == 0))
                    {
                        int middleX = (water[current].X + water[j].X) / 2;
                        int middleY = (water[current].Y + water[j].Y) / 2;
                        acrossBridge = KindAt(cells, middleX, middleY) == Bridge;
                    }

                    if (beside || acrossBridge)
                    {
                        seen[j] = true;
                        stack.Push(j);
                    }
                }
            }

            if (size < 4)
            {
                return false;
            }
        }

        return groups >= 2 && groups <= 4;
    }

    private static int KindAt(ExperienceCell[] cells, int x, int y)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].X == x && cells[i].Y == y)
            {
                return cells[i].Kind;
            }
        }

        return -1;
    }

    private static float Distance(int x1, int y1, int x2, int y2)
    {
        float dx = x1 - x2;
        float dy = y1 - y2;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }
}
