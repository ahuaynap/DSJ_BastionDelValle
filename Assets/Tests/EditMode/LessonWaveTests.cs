using NUnit.Framework;

public class LessonWaveTests
{
    [Test]
    public void FourWavesContainOnlyTheirOwnThreat()
    {
        int[][] waves = LessonRules.Rosters();

        Assert.AreEqual(4, waves.Length);
        Assert.AreEqual(6, LessonRules.Individuals(waves[0]));
        Assert.AreEqual(8, LessonRules.Individuals(waves[1]));
        Assert.AreEqual(15, LessonRules.Individuals(waves[2]));
        Assert.AreEqual(6, LessonRules.Individuals(waves[3]));
        Assert.IsTrue(LessonRules.OnlyKind(waves[0], WaveComposition.Aphid));
        Assert.IsTrue(LessonRules.OnlyKind(waves[1], WaveComposition.Locust));
        Assert.IsTrue(LessonRules.OnlyKind(waves[2], WaveComposition.Swarm));
        Assert.IsTrue(LessonRules.OnlyKind(waves[3], WaveComposition.Moth));
        Assert.AreEqual(3, waves[2].Length);
        Assert.AreEqual(6, waves[3].Length);
    }

    [Test]
    public void SwarmUsesThreeGroupsAndOneAccessEach()
    {
        int[] swarm = LessonRules.Rosters()[2];
        int[] ids = LessonRules.GroupIds(swarm);
        WaveSlot[] slots = WaveSchedule.Expand(
            swarm,
            LessonRules.GroupGap,
            WaveComposition.MemberInterval,
            WaveComposition.SwarmGroupSize);
        int[] draws = AccessDraw.Sequence(27, 2, swarm.Length);
        int[] again = AccessDraw.Sequence(27, 2, swarm.Length);

        Assert.AreEqual(3, LessonRules.DistinctGroups(ids));
        Assert.AreEqual(1, ids[0]);
        Assert.AreEqual(2, ids[1]);
        Assert.AreEqual(3, ids[2]);
        Assert.AreEqual(15, slots.Length);
        Assert.AreEqual(0f, slots[0].WaitBefore);
        Assert.AreEqual(WaveComposition.MemberInterval, slots[1].WaitBefore);
        Assert.AreEqual(LessonRules.GroupGap, slots[5].WaitBefore);
        Assert.AreEqual(LessonRules.GroupGap, slots[10].WaitBefore);
        Assert.AreEqual(3, draws.Length);
        CollectionAssert.AreEqual(draws, again);
        Assert.AreEqual(15, LessonRules.Individuals(swarm));
    }

    [Test]
    public void ClearRewardIsPaidOnceOnTheFirstThreeWaves()
    {
        Assert.AreEqual(20, LessonRules.Grant(0, 4, 20, true, true, false));
        Assert.AreEqual(20, LessonRules.Grant(1, 4, 20, true, true, false));
        Assert.AreEqual(20, LessonRules.Grant(2, 4, 20, true, true, false));
        Assert.AreEqual(0, LessonRules.Grant(3, 4, 20, true, true, false));
        Assert.AreEqual(0, LessonRules.Grant(0, 4, 20, false, true, false));
        Assert.AreEqual(0, LessonRules.Grant(0, 4, 20, true, false, false));
        Assert.AreEqual(0, LessonRules.Grant(0, 4, 20, true, true, true));
        Assert.AreEqual(20, LessonRules.Pay(20, false));
        Assert.AreEqual(0, LessonRules.Pay(20, true));
        Assert.AreEqual(
            "Oleada superada: +20 semillas",
            LessonRules.RewardNotice(20));
    }

    [Test]
    public void VictoryWaitsForTheFourthWaveAndMemoriesStayCapped()
    {
        Assert.IsFalse(MatchPhase.DeclareVictory(true, false, 0, 4, true, true, false));
        Assert.IsFalse(MatchPhase.DeclareVictory(true, false, 2, 4, true, true, false));
        Assert.IsFalse(MatchPhase.DeclareVictory(true, false, 3, 4, true, false, false));
        Assert.IsFalse(MatchPhase.DeclareVictory(true, false, 3, 4, true, true, true));
        Assert.IsFalse(MatchPhase.DeclareVictory(true, true, 3, 4, true, true, false));
        Assert.IsTrue(MatchPhase.DeclareVictory(true, false, 3, 4, true, true, false));
        Assert.IsFalse(LessonRules.OpensAnotherWave(3, 4));
        Assert.IsTrue(LessonRules.OpensAnotherWave(2, 4));
        Assert.AreEqual(2, LessonRules.MemoryCap(2, 4));
        Assert.AreEqual(0, MemoryCandidateRules.Eligible(
            new[] { "suelo", "agua" },
            _ => false,
            2,
            LessonRules.MemoryCap(2, 4)).Count);
        Assert.IsFalse(MatchPhase.AllowStart(true, WaveState.Waiting, true, false));
        Assert.IsTrue(MatchPhase.AllowStart(true, WaveState.Waiting, false, false));
        Assert.IsFalse(MatchPhase.AllowStart(true, WaveState.Spawning, false, true));
        Assert.IsFalse(MatchPhase.AllowStart(true, WaveState.Active, false, false));
    }

    [Test]
    public void BriefShowsTheThreatApartFromActiveEnemies()
    {
        string brief = LessonRules.Briefing(
            LessonRules.Threats()[0],
            6,
            LessonRules.Advice()[0]);

        Assert.AreEqual(
            "Pulgones\n6 individuos\nCubre ambos accesos con tus guardianes",
            brief);
        Assert.AreEqual(
            "Enemigos  0",
            LessonRules.EnemyReadout(MatchView.Preparation, 0, 6));
        Assert.AreEqual(
            "Enemigos  4 activos · 11 pendientes",
            LessonRules.EnemyReadout(MatchView.Combat, 4, 11));
        Assert.AreEqual(
            "Enemigos  3",
            LessonRules.EnemyReadout(MatchView.Combat, 3, 0));
    }

    [Test]
    public void LessonBandStillLeavesTheLowerPlotVisible()
    {
        const float top = 88f / 1080f;
        const float bottom = 440f / 1080f;
        FrameFit frame = PlayfieldFrame.Fit(
            -7.5f,
            7.5f,
            -4.5f,
            4.5f,
            top,
            bottom,
            16f / 9f,
            0.35f);
        PlayableBand band = PlayfieldFrame.Playable(frame, top, bottom);

        Assert.LessOrEqual(band.MinY, -3.5f);
        Assert.GreaterOrEqual(band.MaxY, 3.5f);
        Assert.LessOrEqual(band.MinX, -6.5f);
        Assert.GreaterOrEqual(band.MaxX, 6.5f);
    }

    [Test]
    public void MapOneCompositionHelpersStayOnTheirOriginalWaves()
    {
        Assert.AreEqual(6, WaveSchedule.CountIndividuals(
            WaveComposition.FirstWave(),
            WaveComposition.SwarmGroupSize));
        Assert.AreEqual(18, WaveSchedule.CountIndividuals(
            WaveComposition.SecondWave(),
            WaveComposition.SwarmGroupSize));
        Assert.IsFalse(LessonRules.IsCanonical(
            new[] { 6, 8 },
            new[] { 1.8f, 1.5f }));
    }
}
