using System.Collections.Generic;
using NUnit.Framework;

public class RootsZoneTests
{
    [Test]
    public void TwoPulsesCapDamageAndLateEntryMissesTheFirst()
    {
        AreaBody[] bodies =
        {
            new AreaBody { X = 0f, Y = 0f, Health = 12, Alive = true, Id = 1 },
            new AreaBody { X = 2f, Y = 0f, Health = 12, Alive = true, Id = 2 }
        };
        int[] firstHits = new int[2];
        int first = AreaStrike.Collect(0f, 0f, 0.6f, bodies, 2, firstHits);
        Assert.AreEqual(1, first);
        Assert.AreEqual(1, AreaStrike.Apply(bodies, firstHits, first, 3));

        bodies[1].X = 0.2f;
        int[] secondHits = new int[2];
        int second = AreaStrike.Collect(0f, 0f, 0.6f, bodies, 2, secondHits);
        Assert.AreEqual(2, second);
        Assert.AreEqual(2, AreaStrike.Apply(bodies, secondHits, second, 3));
        Assert.AreEqual(6, bodies[0].Health);
        Assert.AreEqual(9, bodies[1].Health);
        Assert.AreEqual(1, ZoneSchedule.DuePulses(0f, 0.75f));
        Assert.AreEqual(2, ZoneSchedule.DuePulses(0.75f, 0.75f));
    }

    [Test]
    public void PresenceLeavesWaterAndDoesNotStack()
    {
        var effects = new List<SlowStamp>();
        SlowSchedule.Apply(effects, 0.65f, 2f, 0f);
        float waterExpiry = effects[0].ExpiresAt;

        Assert.IsTrue(SlowSchedule.SetPresence(effects, 4, 0.75f));
        Assert.IsFalse(SlowSchedule.SetPresence(effects, 4, 0.75f));
        Assert.IsTrue(SlowSchedule.SetPresence(effects, 5, 0.75f));
        Assert.AreEqual(3, effects.Count);
        Assert.AreEqual(0.65f, SlowSchedule.Multiplier(effects, 1f), 0.0001f);
        Assert.AreEqual(waterExpiry, effects[0].ExpiresAt, 0.0001f);

        Assert.IsTrue(SlowSchedule.RemovePresence(effects, 4));
        Assert.AreEqual(0.65f, SlowSchedule.Multiplier(effects, 1f), 0.0001f);
        Assert.AreEqual(waterExpiry, effects[0].ExpiresAt, 0.0001f);

        Assert.IsTrue(SlowSchedule.RemovePresence(effects, 5));
        Assert.AreEqual(0.65f, SlowSchedule.Multiplier(effects, 1f), 0.0001f);
        Assert.AreEqual(1, effects.Count);

        effects.Clear();
        SlowSchedule.SetPresence(effects, 1, 0.75f);
        SlowSchedule.SetPresence(effects, 2, 0.75f);
        float combined = SlowSchedule.Multiplier(effects, 0f);
        Assert.AreEqual(0.75f, combined, 0.0001f);
        Assert.Greater(System.Math.Abs(combined - 0.5625f), 0.01f);
    }

    [Test]
    public void PauseStopsTheZoneAndTheEndCancelsThePendingPulse()
    {
        bool advanced = ZoneSchedule.Tick(0.2f, 0f, 1.5f, out float next, out bool finished);
        Assert.IsFalse(advanced);
        Assert.AreEqual(0.2f, next, 0.0001f);
        Assert.IsFalse(finished);
        Assert.AreEqual(1, ZoneSchedule.DuePulses(next, 0.75f));
        Assert.IsFalse(ZoneSchedule.AllowPulse(false, false, false));
        Assert.IsFalse(ZoneSchedule.AllowPulse(true, true, false));
        Assert.IsFalse(ZoneSchedule.AllowPulse(true, false, true));

        var effects = new List<SlowStamp>();
        SlowSchedule.SetPresence(effects, 8, 0.75f);
        SlowSchedule.RemovePresence(effects, 8);
        Assert.AreEqual(0, effects.Count);
        Assert.IsTrue(GroundRules.AcceptsZone(EnemyTravel.Ground));
        Assert.IsFalse(GroundRules.AcceptsZone(EnemyTravel.Air));
    }

    [Test]
    public void SeveralDeathsStillApplyInsideTheSnapshot()
    {
        AreaBody[] bodies =
        {
            new AreaBody { X = 0f, Y = 0f, Health = 3, Alive = true, Id = 1 },
            new AreaBody { X = 0.1f, Y = 0f, Health = 3, Alive = true, Id = 2 },
            new AreaBody { X = 0.2f, Y = 0f, Health = 3, Alive = true, Id = 3 }
        };
        int[] hits = { 0, 1, 2 };
        Assert.AreEqual(3, AreaStrike.Apply(bodies, hits, 3, 3));
        Assert.IsFalse(bodies[0].Alive);
        Assert.IsFalse(bodies[1].Alive);
        Assert.IsFalse(bodies[2].Alive);
    }
}
