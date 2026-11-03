using System.Collections.Generic;
using NUnit.Framework;

public class GridPathTests
{
    [Test]
    public void ValidRouteAccumulatesCostAndOrthogonalSteps()
    {
        NavigationGrid grid = Make(
            3,
            1,
            Cell(0, 0, 1),
            Cell(1, 0, 1),
            Cell(2, 0, 1));

        GridPathResult result = GridPathfinder.Find(grid, 0, 0, 2, 0);

        Assert.AreEqual(GridPathStatus.Found, result.Status);
        Assert.AreEqual(2, result.TotalCost);
        Assert.AreEqual(3, result.Steps.Count);
        Assert.AreEqual(0, result.Steps[0].CumulativeCost);
        Assert.AreEqual(1, result.Steps[1].CumulativeCost);
        Assert.AreEqual(2, result.Steps[2].CumulativeCost);
        AssertOrthogonal(result.Steps);
    }

    [Test]
    public void MissingConnectionReturnsNoRoute()
    {
        NavigationGrid grid = Make(
            3,
            1,
            Cell(0, 0, 1),
            Cell(2, 0, 1));

        GridPathResult result = GridPathfinder.Find(grid, 0, 0, 2, 0);

        Assert.AreEqual(GridPathStatus.NoPath, result.Status);
        Assert.AreEqual(0, result.Steps.Count);
    }

    [Test]
    public void StartEqualToGoalIsAZeroCostRoute()
    {
        NavigationGrid grid = Make(2, 1, Cell(0, 0, 4), Cell(1, 0, 4));

        GridPathResult result = GridPathfinder.Find(grid, 1, 0, 1, 0);

        Assert.AreEqual(GridPathStatus.Found, result.Status);
        Assert.AreEqual(0, result.TotalCost);
        Assert.AreEqual(1, result.Steps.Count);
        Assert.AreEqual(1, result.Steps[0].X);
        Assert.AreEqual(0, result.Steps[0].Y);
    }

    [Test]
    public void UnwalkableStartOrGoalIsRejected()
    {
        NavigationGrid blockedStart = Make(2, 1, Cell(1, 0, 1));
        GridPathResult start = GridPathfinder.Find(blockedStart, 0, 0, 1, 0);

        Assert.AreEqual(GridPathStatus.InvalidRequest, start.Status);
        StringAssert.Contains("inicio", start.Detail);

        NavigationGrid blockedGoal = Make(2, 1, Cell(0, 0, 1));
        GridPathResult goal = GridPathfinder.Find(blockedGoal, 0, 0, 1, 0);

        Assert.AreEqual(GridPathStatus.InvalidRequest, goal.Status);
        StringAssert.Contains("objetivo", goal.Detail);

        NavigationGrid blockedSame = Make(1, 1);
        GridPathResult same = GridPathfinder.Find(blockedSame, 0, 0, 0, 0);

        Assert.AreEqual(GridPathStatus.InvalidRequest, same.Status);
        StringAssert.Contains("inicio", same.Detail);
    }

    [Test]
    public void NonPositiveCostIsRejected()
    {
        NavigationGrid grid = Make(
            2,
            1,
            Cell(0, 0, 1),
            Cell(1, 0, 0));

        GridPathResult result = GridPathfinder.Find(grid, 0, 0, 1, 0);

        Assert.AreEqual(GridPathStatus.InvalidRequest, result.Status);
        StringAssert.Contains("coste", result.Detail);
    }

    [Test]
    public void DifferentCostsChooseTheLowerTotal()
    {
        NavigationGrid grid = Make(
            3,
            2,
            Cell(0, 0, 1),
            Cell(1, 0, 10),
            Cell(2, 0, 1),
            Cell(0, 1, 1),
            Cell(1, 1, 1),
            Cell(2, 1, 1));

        GridPathResult result = GridPathfinder.Find(grid, 0, 0, 2, 0);

        Assert.AreEqual(GridPathStatus.Found, result.Status);
        Assert.AreEqual(4, result.TotalCost);
        Assert.IsFalse(Contains(result, 1, 0));
        Assert.IsTrue(Contains(result, 1, 1));
    }

    [Test]
    public void EqualRoutesPreferTheLowerCoordinate()
    {
        NavigationGrid grid = Make(
            2,
            2,
            Cell(0, 0, 1),
            Cell(1, 0, 1),
            Cell(0, 1, 1),
            Cell(1, 1, 1));

        GridPathResult result = GridPathfinder.Find(grid, 0, 0, 1, 1);

        Assert.AreEqual(GridPathStatus.Found, result.Status);
        Assert.AreEqual(2, result.TotalCost);
        Assert.AreEqual(0, result.Steps[1].X);
        Assert.AreEqual(1, result.Steps[1].Y);
    }

    [Test]
    public void BlockingChangeIsRejectedAndKeepsTheGrid()
    {
        NavigationGrid grid = Make(
            4,
            1,
            Cell(0, 0, 1),
            Cell(1, 0, 1),
            Cell(2, 0, 1),
            Cell(3, 0, 1));

        PassageVerdict verdict = PassageChangeRules.Evaluate(
            grid,
            true,
            2,
            0,
            new[] { new GridPoint(0, 0) },
            3,
            0,
            false);

        Assert.AreEqual(PassageDecision.Rejected, verdict.Decision);
        Assert.IsTrue(grid.IsWalkable(2, 0));
        GridPathResult stillOpen = GridPathfinder.Find(grid, 0, 0, 3, 0);
        Assert.AreEqual(GridPathStatus.Found, stillOpen.Status);
    }

    [Test]
    public void OccupiedPassageIsRejected()
    {
        NavigationGrid grid = AlternativeGrid();

        PassageVerdict verdict = PassageChangeRules.Evaluate(
            grid,
            true,
            2,
            1,
            new[]
            {
                new GridPoint(0, 2),
                new GridPoint(0, 0)
            },
            4,
            1,
            true);

        Assert.AreEqual(PassageDecision.Rejected, verdict.Decision);
        StringAssert.Contains("ocupa", verdict.Detail);
        Assert.IsTrue(grid.IsWalkable(2, 1));
    }

    [Test]
    public void ValidChangeKeepsAnAlternative()
    {
        NavigationGrid grid = AlternativeGrid();

        PassageVerdict verdict = PassageChangeRules.Evaluate(
            grid,
            true,
            2,
            1,
            new[]
            {
                new GridPoint(0, 2),
                new GridPoint(0, 0)
            },
            4,
            1,
            false);

        Assert.AreEqual(PassageDecision.Accepted, verdict.Decision);

        NavigationGrid closed = grid.WithBlocked(2, 1);
        Assert.IsTrue(grid.IsWalkable(2, 1));
        Assert.IsFalse(closed.IsWalkable(2, 1));

        GridPathResult rerouted = GridPathfinder.Find(closed, 0, 2, 4, 1);

        Assert.AreEqual(GridPathStatus.Found, rerouted.Status);
        Assert.IsFalse(Contains(rerouted, 2, 1));
        Assert.IsTrue(Contains(rerouted, 2, 2));
    }

    [Test]
    public void ConfluencePassageCanCloseWithoutStrandingEitherAccess()
    {
        NavigationGrid grid = ConfluenceGrid();
        var origins = new[]
        {
            new GridPoint(1, 7),
            new GridPoint(1, 2)
        };

        Assert.AreEqual(
            GridPathStatus.Found,
            GridPathfinder.Find(grid, 1, 7, 14, 5).Status);
        Assert.AreEqual(
            GridPathStatus.Found,
            GridPathfinder.Find(grid, 1, 2, 14, 5).Status);

        PassageVerdict verdict = PassageChangeRules.Evaluate(
            grid,
            true,
            8,
            5,
            origins,
            14,
            5,
            false);

        Assert.AreEqual(PassageDecision.Accepted, verdict.Decision);
        Assert.IsTrue(grid.IsWalkable(8, 5));

        NavigationGrid closed = grid.WithBlocked(8, 5);
        GridPathResult upper = GridPathfinder.Find(closed, 1, 7, 14, 5);
        GridPathResult lower = GridPathfinder.Find(closed, 1, 2, 14, 5);

        Assert.AreEqual(GridPathStatus.Found, upper.Status);
        Assert.AreEqual(GridPathStatus.Found, lower.Status);
        Assert.IsFalse(Contains(upper, 8, 5));
        Assert.IsTrue(Contains(upper, 8, 7));
        Assert.IsFalse(grid.IsWalkable(2, 8));
        Assert.IsFalse(grid.IsWalkable(13, 6));
    }

    [Test]
    public void FollowedRouteStartsAtTheRealPositionAndStaysOrthogonal()
    {
        var steps = new List<GridStep>
        {
            new GridStep(0, 0, 0),
            new GridStep(1, 0, 1)
        };

        List<RoutePoint> points = RouteContinuity.FromCurrentPosition(
            0.2f,
            0.8f,
            steps,
            (x, y) => new RoutePoint(x + 0.5f, y + 0.5f));

        Assert.AreEqual(0.2f, points[0].X, 0.0001f);
        Assert.AreEqual(0.8f, points[0].Y, 0.0001f);
        Assert.IsFalse(HasPoint(points, 0.5f, 0.5f));

        for (int i = 1; i < points.Count; i++)
        {
            bool sameX = System.Math.Abs(points[i].X - points[i - 1].X) <= 0.0001f;
            bool sameY = System.Math.Abs(points[i].Y - points[i - 1].Y) <= 0.0001f;
            Assert.IsTrue(sameX ^ sameY);
        }
    }

    private static NavigationGrid ConfluenceGrid()
    {
        return Make(
            16,
            10,
            Cell(1, 7, 1), Cell(2, 7, 1), Cell(3, 7, 1), Cell(4, 7, 1),
            Cell(1, 2, 1), Cell(2, 2, 1), Cell(3, 2, 1), Cell(4, 2, 1),
            Cell(4, 5, 1), Cell(5, 5, 1), Cell(6, 5, 1), Cell(7, 5, 1),
            Cell(8, 5, 1), Cell(9, 5, 1), Cell(10, 5, 1), Cell(11, 5, 1),
            Cell(12, 5, 1), Cell(13, 5, 1), Cell(14, 5, 1),
            Cell(7, 7, 1), Cell(8, 7, 1), Cell(9, 7, 1),
            Cell(4, 6, 1), Cell(4, 3, 1), Cell(4, 4, 1),
            Cell(7, 6, 1), Cell(9, 6, 1));
    }

    private static NavigationGrid AlternativeGrid()
    {
        return Make(
            5,
            3,
            Cell(0, 2, 1),
            Cell(1, 2, 1),
            Cell(2, 2, 1),
            Cell(3, 2, 1),
            Cell(0, 0, 1),
            Cell(1, 0, 1),
            Cell(1, 1, 1),
            Cell(2, 1, 1),
            Cell(3, 1, 1),
            Cell(4, 1, 1));
    }

    private static (int x, int y, int cost) Cell(int x, int y, int cost)
    {
        return (x, y, cost);
    }

    private static NavigationGrid Make(
        int width,
        int height,
        params (int x, int y, int cost)[] cells)
    {
        bool[] walkable = new bool[width * height];
        int[] costs = new int[width * height];

        for (int i = 0; i < cells.Length; i++)
        {
            (int x, int y, int cost) cell = cells[i];
            int index = cell.y * width + cell.x;
            walkable[index] = true;
            costs[index] = cell.cost;
        }

        return new NavigationGrid(width, height, walkable, costs);
    }

    private static bool Contains(GridPathResult result, int x, int y)
    {
        for (int i = 0; i < result.Steps.Count; i++)
        {
            if (result.Steps[i].X == x && result.Steps[i].Y == y)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPoint(List<RoutePoint> points, float x, float y)
    {
        for (int i = 0; i < points.Count; i++)
        {
            if (System.Math.Abs(points[i].X - x) <= 0.0001f &&
                System.Math.Abs(points[i].Y - y) <= 0.0001f)
            {
                return true;
            }
        }

        return false;
    }

    private static void AssertOrthogonal(IReadOnlyList<GridStep> steps)
    {
        for (int i = 1; i < steps.Count; i++)
        {
            int dx = System.Math.Abs(steps[i].X - steps[i - 1].X);
            int dy = System.Math.Abs(steps[i].Y - steps[i - 1].Y);
            Assert.AreEqual(1, dx + dy);
        }
    }
}
