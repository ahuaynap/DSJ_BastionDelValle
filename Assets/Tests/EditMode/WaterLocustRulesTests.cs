using System.Collections.Generic;
using NUnit.Framework;

public class WaterLocustRulesTests
{
    [Test]
    public void WaterHitSlowsOnceAndDoesNotStack()
    {
        var effects = new List<SlowStamp>();
        Assert.IsTrue(SlowSchedule.ShouldApply(true, false, 2f, 0.65f));
        Assert.IsFalse(SlowSchedule.ShouldApply(true, true, 2f, 0.65f));
        Assert.IsFalse(SlowSchedule.ShouldApply(false, false, 2f, 0.65f));
        Assert.IsFalse(SlowSchedule.ShouldApply(true, false, 0f, 0.65f));

        SlowSchedule.Apply(effects, 0.65f, 2f, 0f);
        SlowSchedule.Apply(effects, 0.65f, 2f, 1f);

        Assert.AreEqual(1, effects.Count);
        Assert.AreEqual(3f, effects[0].ExpiresAt, 0.0001f);
        Assert.AreEqual(0.65f, SlowSchedule.Multiplier(effects, 1f), 0.0001f);
        Assert.AreEqual(1.3f, SlowSchedule.Speed(2f, 0.65f), 0.0001f);
    }

    [Test]
    public void StrongerSlowExpiresBeforeWeakerOne()
    {
        var effects = new List<SlowStamp>();
        SlowSchedule.Apply(effects, 0.65f, 4f, 0f);
        float weakExpiry = effects[0].ExpiresAt;
        SlowSchedule.Apply(effects, 0.5f, 1f, 0f);

        Assert.AreEqual(weakExpiry, effects[0].ExpiresAt, 0.0001f);
        Assert.AreEqual(0.5f, SlowSchedule.Multiplier(effects, 0.5f), 0.0001f);
        Assert.AreEqual(0.65f, SlowSchedule.Multiplier(effects, 1f), 0.0001f);
        Assert.AreEqual(1f, SlowSchedule.Multiplier(effects, 4f), 0.0001f);
    }

    [Test]
    public void ExpiredSlowRestoresBaseSpeedAndSurvivesRouteChange()
    {
        var effects = new List<SlowStamp>();
        SlowSchedule.Apply(effects, 0.65f, 2f, 0f);
        Assert.AreEqual(0.65f, SlowSchedule.Multiplier(effects, 1.5f), 0.0001f);
        SlowSchedule.Prune(effects, 1.5f);
        Assert.AreEqual(1, effects.Count);
        SlowSchedule.Prune(effects, 2f);
        Assert.AreEqual(0, effects.Count);
        Assert.AreEqual(3.2f, SlowSchedule.Speed(3.2f, SlowSchedule.Multiplier(effects, 2f)), 0.0001f);
    }

    [Test]
    public void MixedWavesKeepCountsAndOpenWithAphids()
    {
        int[] first = WaveComposition.FirstWave();
        int[] second = WaveComposition.SecondWave();
        WaveComposition.Count(first, out int firstAphids, out int firstLocusts);
        WaveComposition.Count(second, out int secondAphids, out int secondLocusts);

        WaveComposition.Count(second, out _, out _, out int swarmGroups);
        Assert.AreEqual(6, first.Length);
        Assert.AreEqual(4, firstAphids);
        Assert.AreEqual(2, firstLocusts);
        WaveComposition.Count(
            second,
            out _,
            out _,
            out _,
            out int moths);
        Assert.AreEqual(10, second.Length);
        Assert.AreEqual(3, secondAphids);
        Assert.AreEqual(3, secondLocusts);
        Assert.AreEqual(2, swarmGroups);
        Assert.AreEqual(2, moths);
        Assert.AreEqual(6, WaveSchedule.CountIndividuals(first, WaveComposition.SwarmGroupSize));
        Assert.AreEqual(18, WaveSchedule.CountIndividuals(second, WaveComposition.SwarmGroupSize));
        Assert.IsTrue(WaveComposition.OpensWithAphids(first, 2));
        Assert.IsTrue(WaveComposition.OpensWithAphids(second, 2));
    }

    [Test]
    public void PurchaseChargesOnlyTheSelectedDefense()
    {
        int[] costs = { 40, 40 };
        Assert.IsTrue(DefenseOffer.TryCharge(120, costs, 1, out int balance, out int charged));
        Assert.AreEqual(40, charged);
        Assert.AreEqual(80, balance);
        Assert.IsFalse(DefenseOffer.TryCharge(30, costs, 0, out int unchanged, out int none));
        Assert.AreEqual(30, unchanged);
        Assert.AreEqual(0, none);
    }
}
