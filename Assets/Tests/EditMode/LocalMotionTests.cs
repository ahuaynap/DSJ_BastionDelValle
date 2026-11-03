using System.Collections.Generic;
using NUnit.Framework;

public class LocalMotionTests
{
    [Test]
    public void AccelerationAndSlowLimitTheVelocity()
    {
        LocalSteer.Accelerate(0f, 0f, 10f, 0f, 10f, 4f, 0.5f, out float speedX, out float speedY);
        Assert.LessOrEqual(speedX, 2.001f);
        Assert.AreEqual(0f, speedY, 0.0001f);

        LocalSteer.Accelerate(3f, 0f, 1.3f, 0f, 1.3f, 0f, 0.5f, out float slowedX, out float slowedY);
        Assert.LessOrEqual(slowedX, 1.301f);
        Assert.AreEqual(0f, slowedY, 0.0001f);
    }

    [Test]
    public void CoincidentAgentsStayFinite()
    {
        var neighbors = new[]
        {
            new NeighborSample { X = 0f, Y = 0f, Identity = 2 }
        };
        LocalSteer.Separate(0f, 0f, 1, neighbors, 1, 0.55f, 1.1f, out float pushX, out float pushY);
        Assert.IsFalse(float.IsNaN(pushX) || float.IsInfinity(pushX));
        Assert.IsFalse(float.IsNaN(pushY) || float.IsInfinity(pushY));
    }

    [Test]
    public void DisplacementRejectsBlockedCellsAndCorners()
    {
        bool[] open = { true, true, false };
        int[] costs = { 1, 1, 1 };
        NavigationGrid corridor = new NavigationGrid(3, 1, open, costs);
        Assert.IsTrue(CorridorMotion.SegmentAllowed(
            corridor, 0f, 0f, 1f, 0.5f, 0.5f, 1.5f, 0.5f, 0.22f));
        Assert.IsFalse(CorridorMotion.SegmentAllowed(
            corridor, 0f, 0f, 1f, 0.5f, 0.5f, 2.5f, 0.5f, 0.22f));

        bool[] cornerCells = { true, true, false, false };
        int[] cornerCosts = { 1, 1, 1, 1 };
        NavigationGrid corner = new NavigationGrid(2, 2, cornerCells, cornerCosts);
        Assert.IsFalse(CorridorMotion.SegmentAllowed(
            corner, 0f, 0f, 1f, 0.5f, 0.5f, 1.5f, 1.5f, 0.22f));
    }

    [Test]
    public void PauseDoesNotIntegrateMovement()
    {
        NavigationGrid corridor = Grid(4, 1, 0);
        SteerResult result = LocalSteer.Step(
            corridor,
            0f,
            0f,
            1f,
            0.5f,
            0.5f,
            2f,
            0f,
            1,
            3.5f,
            0.5f,
            false,
            0f,
            0.22f,
            Limits(2f),
            null,
            0,
            1f);
        Assert.AreEqual(0.5f, result.X, 0.0001f);
        Assert.AreEqual(0.5f, result.Y, 0.0001f);
        Assert.IsFalse(result.Moved);
    }

    [Test]
    public void FastGroundStepStaysOnTheCorridor()
    {
        NavigationGrid corridor = Grid(8, 1, 0);
        SteerResult result = LocalSteer.Step(
            corridor,
            0f,
            0f,
            1f,
            0.5f,
            0.5f,
            0f,
            0f,
            1,
            6.5f,
            0.5f,
            false,
            0.5f,
            0.22f,
            Limits(3.2f),
            null,
            0,
            1f);

        Assert.Greater(result.X, 0.5f);
        Assert.AreEqual(0.5f, result.Y, 0.05f);
        Assert.IsTrue(CorridorMotion.SegmentAllowed(
            corridor,
            0f,
            0f,
            1f,
            0.5f,
            0.5f,
            result.X,
            result.Y,
            0.22f));
    }

    [Test]
    public void CrowdedBridgeClearsAndEmptiesTheRoster()
    {
        NavigationGrid bridge = Grid(8, 3, 1);
        var roster = new List<int> { 1, 2, 3, 4, 5 };
        float[] x = { 0.50f, 0.62f, 0.74f, 0.86f, 0.98f };
        float[] y = { 1.50f, 1.50f, 1.50f, 1.50f, 1.50f };
        float[] vx = new float[5];
        float[] vy = new float[5];
        var neighbors = new NeighborSample[5];
        int guard = 0;

        while (roster.Count > 0 && guard < 900)
        {
            guard++;

            for (int i = roster.Count - 1; i >= 0; i--)
            {
                int index = roster[i] - 1;
                int neighborCount = 0;

                for (int n = 0; n < roster.Count; n++)
                {
                    int other = roster[n] - 1;

                    if (other == index)
                    {
                        continue;
                    }

                    neighbors[neighborCount++] = new NeighborSample
                    {
                        X = x[other],
                        Y = y[other],
                        Identity = other + 1
                    };
                }

                SteerResult step = LocalSteer.Step(
                    bridge,
                    0f,
                    0f,
                    1f,
                    x[index],
                    y[index],
                    vx[index],
                    vy[index],
                    index + 1,
                    7.5f,
                    1.5f,
                    true,
                    0.05f,
                    0.22f,
                    Limits(2f),
                    neighbors,
                    neighborCount,
                    1f);
                Assert.IsFalse(float.IsNaN(step.X) || float.IsNaN(step.Y));
                x[index] = step.X;
                y[index] = step.Y;
                vx[index] = step.VelocityX;
                vy[index] = step.VelocityY;

                if (x[index] >= 7.2f)
                {
                    roster.RemoveAt(i);
                }
            }
        }

        Assert.AreEqual(0, roster.Count);
        Assert.Less(guard, 900);
    }

    private static MotionLimits Limits(float speed)
    {
        return new MotionLimits
        {
            MaxSpeed = speed,
            Acceleration = 8f,
            ArrivalRadius = 0.45f,
            SeparationSpeed = 1.1f
        };
    }

    private static NavigationGrid Grid(int width, int height, params int[] walkableRows)
    {
        int count = width * height;
        bool[] walkable = new bool[count];
        int[] costs = new int[count];

        for (int y = 0; y < height; y++)
        {
            bool open = false;

            for (int i = 0; i < walkableRows.Length; i++)
            {
                if (walkableRows[i] == y)
                {
                    open = true;
                }
            }

            for (int x = 0; x < width; x++)
            {
                walkable[y * width + x] = open;
                costs[y * width + x] = 1;
            }
        }

        return new NavigationGrid(width, height, walkable, costs);
    }
}
