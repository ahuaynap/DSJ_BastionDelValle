using NUnit.Framework;

public class HabitatRulesTests
{
    [Test]
    public void ValuesStayInsideInclusiveLimits()
    {
        Assert.IsFalse(HabitatRules.TryChange(0, -1, 0, 100, out int floor));
        Assert.AreEqual(0, floor);
        Assert.IsFalse(HabitatRules.TryChange(100, 10, 0, 100, out int ceiling));
        Assert.AreEqual(100, ceiling);
        Assert.IsTrue(HabitatRules.TryChange(50, -1, 0, 100, out int next));
        Assert.AreEqual(49, next);
        Assert.AreEqual(50, HabitatRules.Restored(50, 0, 100));
    }

    [Test]
    public void OneArrivalDropsEachIndicatorOnce()
    {
        Assert.AreEqual(1, HabitatRules.ArrivalLoss(true, false, 1));
        Assert.AreEqual(0, HabitatRules.ArrivalLoss(true, true, 1));
        Assert.AreEqual(0, HabitatRules.ArrivalLoss(false, false, 1));
        Assert.AreEqual(
            "2 llegadas: Agua -2, Suelo -2, Biodiversidad -2",
            HabitatRules.ArrivalLine(2, 2, 2, 2));
        Assert.AreEqual(1, HabitatRules.GrowBatch(0, false));
        Assert.AreEqual(2, HabitatRules.GrowBatch(1, true));
    }

    [Test]
    public void RecoveryAppliesOnceAndDoesNotStackCopies()
    {
        Assert.AreEqual(10, HabitatRules.Recovery(true, true, false, false, true, 10));
        Assert.AreEqual(0, HabitatRules.Recovery(true, true, false, true, true, 10));
        Assert.AreEqual(0, HabitatRules.Recovery(true, true, false, false, false, 10));
        Assert.AreEqual(0, HabitatRules.Recovery(true, false, false, false, true, 10));
        Assert.AreEqual(0, HabitatRules.Recovery(true, true, true, false, true, 10));
        Assert.AreEqual(10, HabitatRules.Recovery(true, true, false, false, true, 10));
    }

    [Test]
    public void ThresholdAndDistanceDoNotStackSources()
    {
        Assert.AreEqual(1f, HabitatRules.IntervalMultiplier(59, 60, true, 0.85f));
        Assert.AreEqual(0.85f, HabitatRules.IntervalMultiplier(60, 60, true, 0.85f));
        Assert.AreEqual(1f, HabitatRules.IntervalMultiplier(60, 60, false, 0.85f));
        Assert.AreEqual(1.10f, HabitatRules.RangeMultiplier(60, 60, true, 1.10f), 0.001f);
        Assert.AreEqual(1f, HabitatRules.RangeMultiplier(59, 60, true, 1.10f));
        Assert.IsTrue(HabitatRules.Within(0f, 0f, 2.5f, 0f, 2.5f));
        Assert.IsFalse(HabitatRules.Within(0f, 0f, 2.51f, 0f, 2.5f));
        bool twoChannels = HabitatRules.NearAny(
            0f,
            0f,
            new[] { 1f, 2f },
            new[] { 0f, 0f },
            2,
            2.5f);
        Assert.IsTrue(twoChannels);
        Assert.AreEqual(0.85f, HabitatRules.IntervalMultiplier(60, 60, twoChannels, 0.85f));
        Assert.IsTrue(HabitatRules.AcceptsHaste("tower_basic", "tower_basic"));
        Assert.IsFalse(HabitatRules.AcceptsHaste("tower_condor", "tower_basic"));
        Assert.IsFalse(HabitatRules.AcceptsReach("tower_water", "tower_basic", "tower_condor"));
        Assert.IsFalse(HabitatRules.AcceptsReach("tower_roots", "tower_basic", "tower_condor"));
        Assert.IsFalse(HabitatRules.AcceptsReach("tower_pollinator", "tower_basic", "tower_condor"));
        Assert.IsTrue(HabitatRules.AcceptsReach("tower_basic", "tower_basic", "tower_condor"));
        Assert.IsTrue(HabitatRules.AcceptsReach("tower_condor", "tower_basic", "tower_condor"));
    }

    [Test]
    public void EffectiveStatsRestartFromTheBaseValue()
    {
        float hastened = HabitatRules.Scale(1f, 0.85f);
        float restored = HabitatRules.Scale(
            1f,
            HabitatRules.IntervalMultiplier(59, 60, true, 0.85f));

        Assert.AreEqual(0.85f, hastened, 0.001f);
        Assert.AreEqual(1f, restored);
        Assert.AreEqual(1.10f, HabitatRules.Scale(2f, 1.10f), 0.001f);
        Assert.Greater(System.Math.Abs(HabitatRules.Scale(hastened, 0.85f) - hastened), 0.01f);
        Assert.AreEqual(0.4f, HabitatRules.NextCooldown(0.4f, 1f, 0.85f, false));
        Assert.AreEqual(0.85f, HabitatRules.NextCooldown(0.4f, 1f, 0.85f, true), 0.001f);
    }

    [Test]
    public void SoilAddsFiveOnlyAfterRecoveryOnTheFirstWaves()
    {
        Assert.AreEqual(25, HabitatRules.Payout(20, 60, 60, 5, false));
        Assert.AreEqual(20, HabitatRules.Payout(20, 59, 60, 5, false));
        Assert.AreEqual(5, HabitatRules.SoilExtra(20, 60, 60, 5, false));
        Assert.AreEqual(0, HabitatRules.SoilExtra(20, 59, 60, 5, false));
        Assert.AreEqual(0, HabitatRules.Payout(20, 100, 60, 5, true));
        Assert.AreEqual(0, HabitatRules.SoilExtra(20, 100, 60, 5, true));
        Assert.AreEqual(
            "Oleada superada: +20 semillas\nSuelo: +5 semillas",
            HabitatRules.RewardBreakdown(20, 5));
        Assert.AreEqual(
            "Oleada superada: +20 semillas",
            HabitatRules.RewardBreakdown(20, 0));
    }

    [Test]
    public void ResultNamesOnlyBonusesThatActuallyHappened()
    {
        string report = HabitatRules.ResultReport(
            42,
            70,
            60,
            100,
            false,
            true,
            false,
            false,
            true,
            false);

        Assert.IsTrue(report.Contains("Agua 42/100"));
        Assert.IsTrue(report.Contains("Suelo 70/100"));
        Assert.IsTrue(report.Contains("Biodiversidad 60/100"));
        Assert.IsTrue(report.Contains("La Terraza de Raíces recuperó el suelo."));
        Assert.IsTrue(report.Contains("La biodiversidad amplió el alcance."));
        Assert.IsTrue(report.Contains(HabitatRules.SimplifiedModel));
        Assert.IsFalse(report.Contains("aceleró"));
        Assert.IsFalse(report.Contains("añadió semillas"));
        Assert.IsTrue(HabitatRules.Explanation.Contains("línea de visión"));
        const float top = 88f / 1080f;
        const float bottom = 532f / 1080f;
        FrameFit frame = PlayfieldFrame.Fit(
            -7.5f, 7.5f, -4.5f, 4.5f, top, bottom, 16f / 9f, 0.35f);
        PlayableBand band = PlayfieldFrame.Playable(frame, top, bottom);
        Assert.LessOrEqual(band.MinY, -3.5f);
        Assert.GreaterOrEqual(band.MaxY, 3.5f);
    }

    [Test]
    public void MapOneRulesStayUntouched()
    {
        Assert.AreEqual(TravelMask.Ground, TargetRules.Normalize(0));
        Assert.IsFalse(TargetRules.Allows(TravelMask.Ground, EnemyTravel.Air));
        Assert.IsFalse(TargetRules.SharesSeparation(EnemyTravel.Air, EnemyTravel.Ground));
        Assert.IsTrue(SlowSchedule.ShouldApply(true, false, 2f, 0.65f));
        Assert.AreEqual(6, WaveSchedule.CountIndividuals(WaveComposition.FirstWave(), 5));
        Assert.IsFalse(MatchPhase.DeclareVictory(true, false, 0, 2, true, true, false));
        Assert.IsTrue(MatchPhase.DeclareVictory(true, false, 1, 2, true, true, false));
        var neighbors = new[] { new NeighborSample { X = 0f, Y = 0f, Identity = 2 } };
        LocalSteer.Separate(0f, 0f, 1, neighbors, 1, 0.55f, 1.1f, out float pushX, out float pushY);
        Assert.IsFalse(float.IsNaN(pushX) || float.IsInfinity(pushX));
        Assert.IsFalse(float.IsNaN(pushY) || float.IsInfinity(pushY));
    }
}
