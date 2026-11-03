using System;
using System.Collections.Generic;

public static class RouteContinuity
{
    public static List<RoutePoint> FromCurrentPosition(
        float currentX,
        float currentY,
        IReadOnlyList<GridStep> steps,
        Func<int, int, RoutePoint> centerOf)
    {
        var points = new List<RoutePoint>
        {
            new RoutePoint(currentX, currentY)
        };

        if (steps == null || steps.Count <= 1 || centerOf == null)
        {
            return points;
        }

        float x = currentX;
        float y = currentY;

        for (int i = 1; i < steps.Count; i++)
        {
            GridStep previous = steps[i - 1];
            GridStep step = steps[i];
            RoutePoint center = centerOf(step.X, step.Y);
            bool horizontal = step.Y == previous.Y;
            bool vertical = step.X == previous.X;

            if (horizontal && !vertical)
            {
                Append(points, ref x, ref y, center.X, y);
                Append(points, ref x, ref y, x, center.Y);
            }
            else if (vertical && !horizontal)
            {
                Append(points, ref x, ref y, x, center.Y);
                Append(points, ref x, ref y, center.X, y);
            }
            else
            {
                Append(points, ref x, ref y, center.X, center.Y);
            }
        }

        return points;
    }

    private static void Append(
        List<RoutePoint> points,
        ref float x,
        ref float y,
        float nextX,
        float nextY)
    {
        if (Math.Abs(nextX - x) <= 0.0001f &&
            Math.Abs(nextY - y) <= 0.0001f)
        {
            return;
        }

        x = nextX;
        y = nextY;
        points.Add(new RoutePoint(x, y));
    }
}
