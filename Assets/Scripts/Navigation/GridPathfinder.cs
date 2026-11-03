using System.Collections.Generic;

public static class GridPathfinder
{
    private static readonly int[] NeighborX = { 1, 0, -1, 0 };
    private static readonly int[] NeighborY = { 0, 1, 0, -1 };

    public static GridPathResult Find(
        NavigationGrid grid,
        int startX,
        int startY,
        int goalX,
        int goalY)
    {
        if (grid == null)
        {
            return GridPathResult.Invalid(
                "No hay cuadrícula.");
        }

        if (!grid.InBounds(startX, startY))
        {
            return GridPathResult.Invalid(
                "El inicio está fuera de la cuadrícula.");
        }

        if (!grid.InBounds(goalX, goalY))
        {
            return GridPathResult.Invalid(
                "El objetivo está fuera de la cuadrícula.");
        }

        if (!grid.IsWalkable(startX, startY))
        {
            return GridPathResult.Invalid(
                "El inicio no es transitable.");
        }

        if (!grid.IsWalkable(goalX, goalY))
        {
            return GridPathResult.Invalid(
                "El objetivo no es transitable.");
        }

        int minimumCost = grid.MinimumStepCost();

        if (minimumCost <= 0)
        {
            return GridPathResult.Invalid(
                "Hay un coste que no es positivo.");
        }

        if (startX == goalX && startY == goalY)
        {
            var sameCell = new List<GridStep>
            {
                new GridStep(startX, startY, 0)
            };

            return GridPathResult.Found(sameCell, 0);
        }

        int count = grid.Width * grid.Height;
        int[] gScore = new int[count];
        int[] parent = new int[count];
        bool[] closed = new bool[count];

        for (int i = 0; i < count; i++)
        {
            gScore[i] = int.MaxValue;
            parent[i] = -1;
        }

        int startIndex = startY * grid.Width + startX;
        int goalIndex = goalY * grid.Width + goalX;
        gScore[startIndex] = 0;

        var open = new BinaryHeap();
        int sequence = 0;
        int startHeuristic = Heuristic(
            startX,
            startY,
            goalX,
            goalY,
            minimumCost);

        open.Push(new OpenNode(
            startX,
            startY,
            0,
            startHeuristic,
            startHeuristic,
            sequence++));

        int expansions = 0;

        while (open.Count > 0)
        {
            OpenNode current = open.Pop();
            int currentIndex = current.Y * grid.Width + current.X;

            if (closed[currentIndex] || gScore[currentIndex] != current.G)
            {
                continue;
            }

            if (expansions >= count)
            {
                return GridPathResult.NoPath();
            }

            expansions++;
            closed[currentIndex] = true;

            if (currentIndex == goalIndex)
            {
                return Reconstruct(
                    grid,
                    parent,
                    gScore,
                    goalIndex);
            }

            for (int direction = 0; direction < 4; direction++)
            {
                int nextX = current.X + NeighborX[direction];
                int nextY = current.Y + NeighborY[direction];

                if (!grid.IsWalkable(nextX, nextY))
                {
                    continue;
                }

                int nextIndex = nextY * grid.Width + nextX;

                if (closed[nextIndex])
                {
                    continue;
                }

                int stepCost = grid.CostAt(nextX, nextY);
                int nextG = current.G + stepCost;

                if (nextG < 0 || nextG >= gScore[nextIndex])
                {
                    continue;
                }

                gScore[nextIndex] = nextG;
                parent[nextIndex] = currentIndex;

                int heuristic = Heuristic(
                    nextX,
                    nextY,
                    goalX,
                    goalY,
                    minimumCost);

                open.Push(new OpenNode(
                    nextX,
                    nextY,
                    nextG,
                    nextG + heuristic,
                    heuristic,
                    sequence++));
            }
        }

        return GridPathResult.NoPath();
    }

    private static int Heuristic(
        int x,
        int y,
        int goalX,
        int goalY,
        int minimumCost)
    {
        int manhattan = Abs(x - goalX) + Abs(y - goalY);
        return manhattan * minimumCost;
    }

    private static int Abs(int value)
    {
        return value < 0 ? -value : value;
    }

    private static GridPathResult Reconstruct(
        NavigationGrid grid,
        int[] parent,
        int[] gScore,
        int goalIndex)
    {
        var chain = new List<int>();
        int cursor = goalIndex;
        int guard = 0;
        int limit = grid.Width * grid.Height;

        while (cursor >= 0 && guard <= limit)
        {
            chain.Add(cursor);
            cursor = parent[cursor];
            guard++;
        }

        if (guard > limit)
        {
            return GridPathResult.NoPath();
        }

        chain.Reverse();
        var steps = new List<GridStep>(chain.Count);

        for (int i = 0; i < chain.Count; i++)
        {
            int index = chain[i];
            int x = index % grid.Width;
            int y = index / grid.Width;
            steps.Add(new GridStep(x, y, gScore[index]));
        }

        return GridPathResult.Found(
            steps,
            gScore[goalIndex]);
    }

    private readonly struct OpenNode
    {
        public OpenNode(
            int x,
            int y,
            int g,
            int f,
            int h,
            int sequence)
        {
            X = x;
            Y = y;
            G = g;
            F = f;
            H = h;
            Sequence = sequence;
        }

        public int X { get; }
        public int Y { get; }
        public int G { get; }
        public int F { get; }
        public int H { get; }
        public int Sequence { get; }
    }

    private sealed class BinaryHeap
    {
        private readonly List<OpenNode> items = new();

        public int Count => items.Count;

        public void Push(OpenNode node)
        {
            items.Add(node);
            int index = items.Count - 1;

            while (index > 0)
            {
                int parent = (index - 1) / 2;

                if (Compare(items[index], items[parent]) >= 0)
                {
                    break;
                }

                (items[index], items[parent]) =
                    (items[parent], items[index]);
                index = parent;
            }
        }

        public OpenNode Pop()
        {
            OpenNode root = items[0];
            int last = items.Count - 1;
            items[0] = items[last];
            items.RemoveAt(last);

            int index = 0;

            while (true)
            {
                int left = index * 2 + 1;
                int right = left + 1;
                int smallest = index;

                if (left < items.Count &&
                    Compare(items[left], items[smallest]) < 0)
                {
                    smallest = left;
                }

                if (right < items.Count &&
                    Compare(items[right], items[smallest]) < 0)
                {
                    smallest = right;
                }

                if (smallest == index)
                {
                    break;
                }

                (items[index], items[smallest]) =
                    (items[smallest], items[index]);
                index = smallest;
            }

            return root;
        }

        private static int Compare(OpenNode left, OpenNode right)
        {
            if (left.F != right.F)
            {
                return left.F.CompareTo(right.F);
            }

            if (left.H != right.H)
            {
                return left.H.CompareTo(right.H);
            }

            if (left.X != right.X)
            {
                return left.X.CompareTo(right.X);
            }

            if (left.Y != right.Y)
            {
                return left.Y.CompareTo(right.Y);
            }

            return left.Sequence.CompareTo(right.Sequence);
        }
    }
}
