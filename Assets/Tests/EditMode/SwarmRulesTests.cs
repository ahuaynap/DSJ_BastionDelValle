using NUnit.Framework;

public class SwarmRulesTests
{
    [Test]
    public void AreaDamagesEachEnemyInsideOnce()
    {
        AreaBody[] bodies =
        {
            Body(0f, 0f, 20, 1),
            Body(0.4f, 0f, 12, 2),
            Body(0.6f, 0.2f, 12, 3),
            Body(2f, 0f, 30, 4)
        };
        int[] hits = new int[4];
        int found = AreaStrike.Collect(0f, 0f, 0.65f, bodies, bodies.Length, hits);

        Assert.AreEqual(3, found);
        Assert.AreEqual(3, AreaStrike.Apply(bodies, hits, found, 8));
        Assert.AreEqual(12, bodies[0].Health);
        Assert.AreEqual(4, bodies[1].Health);
        Assert.AreEqual(4, bodies[2].Health);
        Assert.AreEqual(30, bodies[3].Health);

        int[] repeated = { 0, 0, 1 };
        AreaBody[] once =
        {
            Body(0f, 0f, 12, 1),
            Body(0.2f, 0f, 12, 2)
        };
        Assert.AreEqual(2, AreaStrike.Apply(once, repeated, repeated.Length, 8));
        Assert.AreEqual(4, once[0].Health);
        Assert.AreEqual(4, once[1].Health);
    }

    [Test]
    public void DeathsInsideTheSnapshotDoNotSkipLaterEnemies()
    {
        AreaBody[] bodies =
        {
            Body(0f, 0f, 8, 1),
            Body(0.2f, 0f, 8, 2),
            Body(0.4f, 0f, 8, 3)
        };
        int[] hits = { 0, 1, 2 };
        Assert.AreEqual(3, AreaStrike.Apply(bodies, hits, 3, 8));
        Assert.IsFalse(bodies[0].Alive);
        Assert.IsFalse(bodies[1].Alive);
        Assert.IsFalse(bodies[2].Alive);
        Assert.AreEqual(0, bodies[2].Health);
    }

    [Test]
    public void LostTargetDoesNotDetonate()
    {
        Assert.IsFalse(AreaStrike.ShouldDetonate(false));
        Assert.IsTrue(AreaStrike.ShouldDetonate(true));
    }

    [Test]
    public void WavesExpandToIndividualsAndBlockEarlyVictory()
    {
        WaveSlot[] first = WaveSchedule.Expand(
            WaveComposition.FirstWave(),
            1.8f,
            WaveComposition.MemberInterval,
            WaveComposition.SwarmGroupSize);
        WaveSlot[] second = WaveSchedule.Expand(
            WaveComposition.SecondWave(),
            1.5f,
            WaveComposition.MemberInterval,
            WaveComposition.SwarmGroupSize);

        Assert.AreEqual(6, first.Length);
        Assert.AreEqual(18, second.Length);
        Assert.AreEqual(WaveComposition.Aphid, second[0].Kind);
        Assert.AreEqual(WaveComposition.Swarm, second[6].Kind);
        Assert.AreEqual(0.2f, second[7].WaitBefore, 0.0001f);
        Assert.AreEqual(4, second[10].Member);
        Assert.AreEqual(second[6].GroupId, second[10].GroupId);
        Assert.AreNotEqual(second[6].GroupId, second[11].GroupId);
        Assert.AreEqual(0, second[11].Member);
        Assert.AreEqual(1.5f, second[11].WaitBefore, 0.0001f);
        Assert.AreEqual(WaveComposition.Moth, second[16].Kind);
        Assert.AreEqual(WaveComposition.Moth, second[17].Kind);
        Assert.AreEqual(0, second[16].GroupId);
        Assert.AreEqual(1.5f, second[16].WaitBefore, 0.0001f);
        Assert.IsFalse(WaveSchedule.CanComplete(16, 18, 0, false));
        Assert.IsFalse(WaveSchedule.CanComplete(18, 18, 0, true));
        Assert.IsFalse(WaveSchedule.CanComplete(18, 18, 2, false));
        Assert.IsTrue(WaveSchedule.CanComplete(18, 18, 0, false));
        Assert.IsFalse(IntroWaveRules.IsVictory(true, false, 1, 2, false, true));
        Assert.AreEqual(1.8f, WaveComposition.SwarmSpeed(2f), 0.0001f);
    }

    [Test]
    public void FlockStaysFiniteWithoutNeighborsOrAtTheSamePoint()
    {
        LocalSteer.Flock(
            1f, 1f, 1f, 0f, 4, null, 0, 1.35f, 0.55f, 0.4f,
            out float aloneX, out float aloneY, out float aloneAlignX, out float aloneAlignY);
        Assert.AreEqual(0f, aloneX, 0.0001f);
        Assert.AreEqual(0f, aloneY, 0.0001f);
        Assert.AreEqual(0f, aloneAlignX, 0.0001f);
        Assert.AreEqual(0f, aloneAlignY, 0.0001f);

        var stacked = new[]
        {
            new NeighborSample { X = 1f, Y = 1f, Identity = 2, GroupId = 4, VelocityX = 0.2f, VelocityY = 0f }
        };
        LocalSteer.Flock(
            1f, 1f, 0.2f, 0f, 4, stacked, 1, 1.35f, 0.55f, 0.4f,
            out float sameX, out float sameY, out float sameAlignX, out float sameAlignY);
        Assert.IsTrue(float.IsFinite(sameX) && float.IsFinite(sameY));
        Assert.IsTrue(float.IsFinite(sameAlignX) && float.IsFinite(sameAlignY));
        Assert.AreEqual(0f, sameX, 0.0001f);

        LocalSteer.Flock(
            1f, 1f, 1f, 0f, 0, stacked, 1, 1.35f, 0.55f, 0.4f,
            out float aphidX, out float aphidY, out _, out _);
        Assert.AreEqual(0f, aphidX, 0.0001f);
        Assert.AreEqual(0f, aphidY, 0.0001f);
    }

    [Test]
    public void PauseHoldsScatterAndCohesion()
    {
        LocalSteer.AdvanceFlockResponse(0.55f, 0f, 0f, 0.55f, out float time, out float cohesion);
        Assert.AreEqual(0.55f, time, 0.0001f);
        Assert.AreEqual(0f, cohesion, 0.0001f);
        LocalSteer.AdvanceFlockResponse(0.55f, 0f, 0.2f, 0.55f, out float later, out float recovered);
        Assert.AreEqual(0.35f, later, 0.0001f);
        Assert.Greater(recovered, 0f);
    }

    [Test]
    public void RemovedMembersLeaveTheGroupSample()
    {
        var roster = new[]
        {
            new NeighborSample { Identity = 1, GroupId = 3, X = 0f, Y = 0f },
            new NeighborSample { Identity = 2, GroupId = 3, X = 0.1f, Y = 0f },
            new NeighborSample { Identity = 3, GroupId = 3, X = 0.2f, Y = 0f }
        };
        var living = new NeighborSample[3];
        int count = 0;

        for (int i = 0; i < roster.Length; i++)
        {
            if (roster[i].Identity == 2)
            {
                continue;
            }

            living[count] = roster[i];
            count++;
        }

        Assert.AreEqual(2, count);
        LocalSteer.Flock(
            0f, 0f, 0f, 0f, 3, living, count, 1.35f, 0.55f, 0.4f,
            out _, out _, out _, out _);
        living = new NeighborSample[0];
        Assert.AreEqual(0, living.Length);
    }

    private static AreaBody Body(float x, float y, int health, int id)
    {
        return new AreaBody
        {
            X = x,
            Y = y,
            Health = health,
            Alive = true,
            Id = id
        };
    }
}
