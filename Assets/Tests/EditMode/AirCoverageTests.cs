using NUnit.Framework;

public class AirCoverageTests
{
    [Test]
    public void LegacyMaskLoadsAsGroundAndFiltersEveryAttack()
    {
        Assert.AreEqual(TravelMask.Ground, TargetRules.Normalize(0));
        Assert.IsTrue(TargetRules.Allows(TravelMask.Ground, EnemyTravel.Ground));
        Assert.IsFalse(TargetRules.Allows(TravelMask.Ground, EnemyTravel.Air));
        Assert.IsTrue(TargetRules.Allows(TravelMask.Both, EnemyTravel.Ground));
        Assert.IsTrue(TargetRules.Allows(TravelMask.Both, EnemyTravel.Air));
        Assert.IsFalse(TargetRules.SharesSeparation(EnemyTravel.Air, EnemyTravel.Ground));
        Assert.IsTrue(TargetRules.SharesSeparation(EnemyTravel.Air, EnemyTravel.Air));
        Assert.IsTrue(TargetRules.SharesSeparation(EnemyTravel.Ground, EnemyTravel.Ground));
    }

    [Test]
    public void CondorPrefersAirAndGuardianKeepsTheCloserTarget()
    {
        var candidates = new[]
        {
            new AimCandidate { X = 1f, Y = 0f, Travel = EnemyTravel.Ground },
            new AimCandidate { X = 3f, Y = 0f, Travel = EnemyTravel.Air }
        };

        Assert.AreEqual(
            1,
            TargetRules.Pick(
                candidates, 2, 0f, 0f, 3.8f,
                TravelMask.Both, TargetPriority.AirThenGround));
        Assert.AreEqual(
            0,
            TargetRules.Pick(
                candidates, 2, 0f, 0f, 3.8f,
                TravelMask.Both, TargetPriority.Closest));
        Assert.AreEqual(
            0,
            TargetRules.Pick(
                candidates, 2, 0f, 0f, 3.8f,
                TravelMask.Ground, TargetPriority.AirThenGround));
        Assert.AreEqual(
            -1,
            TargetRules.Pick(
                candidates, 1, 0f, 0f, 3.8f,
                TravelMask.Air, TargetPriority.AirThenGround));
    }

    [Test]
    public void OpenAirCrossesABlockedCellAndGroundDoesNot()
    {
        var walkable = new[] { true, false, true };
        var costs = new[] { 1, 1, 1 };
        var grid = new NavigationGrid(3, 1, walkable, costs);
        var limits = new MotionLimits
        {
            MaxSpeed = 2f,
            Acceleration = 8f,
            ArrivalRadius = 0.45f,
            SeparationSpeed = 1.1f
        };

        SteerResult ground = Step(grid, limits, false);
        SteerResult air = Step(grid, limits, true);

        Assert.IsFalse(ground.Moved);
        Assert.AreEqual(0.5f, ground.X, 0.0001f);
        Assert.IsTrue(air.Moved);
        Assert.Greater(air.X, 1f);
        Assert.AreEqual(2.4f, WaveComposition.MothSpeed(2f), 0.0001f);
    }

    [Test]
    public void SecondWaveWaitsForMothsBeforeVictory()
    {
        WaveSlot[] second = WaveSchedule.Expand(
            WaveComposition.SecondWave(),
            1.5f,
            WaveComposition.MemberInterval,
            WaveComposition.SwarmGroupSize);

        Assert.AreEqual(6, WaveSchedule.CountIndividuals(
            WaveComposition.FirstWave(),
            WaveComposition.SwarmGroupSize));
        Assert.AreEqual(18, second.Length);
        Assert.AreEqual(WaveComposition.Moth, second[16].Kind);
        Assert.AreEqual(WaveComposition.Moth, second[17].Kind);
        Assert.Greater(second[16].WaitBefore, 0f);
        Assert.IsFalse(WaveSchedule.CanComplete(16, 18, 0, false));
        Assert.IsFalse(WaveSchedule.CanComplete(18, 18, 1, false));
        Assert.IsTrue(WaveSchedule.CanComplete(18, 18, 0, false));
    }

    private static SteerResult Step(
        NavigationGrid grid,
        MotionLimits limits,
        bool openAir)
    {
        return LocalSteer.Step(
            grid,
            0f,
            0f,
            1f,
            0.5f,
            0.5f,
            2f,
            0f,
            1,
            6f,
            0.5f,
            false,
            0.35f,
            0.22f,
            limits,
            null,
            0,
            1f,
            0,
            0f,
            0f,
            0f,
            openAir);
    }
}
