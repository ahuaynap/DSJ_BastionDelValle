using System.Collections.Generic;
using NUnit.Framework;

public class IntroLevelRulesTests
{
    [Test]
    public void WaveStartsOnceAndOnlyFromPreparation()
    {
        Assert.IsTrue(IntroWaveRules.CanStartWave(
            true,
            true,
            false,
            false));
        Assert.IsFalse(IntroWaveRules.CanStartWave(
            true,
            true,
            false,
            true));
        Assert.IsFalse(IntroWaveRules.CanStartWave(
            true,
            false,
            false,
            false));
        Assert.IsFalse(IntroWaveRules.CanStartWave(
            true,
            true,
            true,
            false));
        Assert.IsFalse(IntroWaveRules.CanStartWave(
            false,
            true,
            false,
            false));
    }

    [Test]
    public void VictoryRequiresTheSecondWaveToBeFullyResolved()
    {
        Assert.IsFalse(IntroWaveRules.IsVictory(
            true,
            false,
            0,
            2,
            true,
            true));
        Assert.IsFalse(IntroWaveRules.IsVictory(
            true,
            false,
            1,
            2,
            false,
            true));
        Assert.IsFalse(IntroWaveRules.IsVictory(
            true,
            false,
            1,
            2,
            true,
            false));
        Assert.IsTrue(IntroWaveRules.IsVictory(
            true,
            false,
            1,
            2,
            true,
            true));
    }

    [Test]
    public void DefeatBlocksVictory()
    {
        Assert.IsFalse(IntroWaveRules.IsVictory(
            false,
            true,
            1,
            2,
            true,
            true));
        Assert.IsFalse(IntroWaveRules.IsVictory(
            true,
            true,
            1,
            2,
            true,
            true));
    }

    [Test]
    public void EligibleMemoriesSkipBlanksDuplicatesAndDiscoveries()
    {
        HashSet<string> discovered = new HashSet<string>
        {
            "memory_water_01"
        };
        List<string> enabled = new()
        {
            "BIO_POLINIZADOR_01",
            " ",
            "BIO_POLINIZADOR_01",
            "memory_water_01",
            "memory_soil_01"
        };

        List<string> eligible = MemoryCandidateRules.Eligible(
            enabled,
            discovered.Contains,
            0,
            2);

        CollectionAssert.AreEqual(
            new[] { "BIO_POLINIZADOR_01", "memory_soil_01" },
            eligible);
    }

    [Test]
    public void RunLimitDoesNotHideMemoriesStillAvailableLater()
    {
        List<string> enabled = new()
        {
            "BIO_POLINIZADOR_01",
            "memory_water_01",
            "memory_soil_01"
        };

        Assert.AreEqual(
            0,
            MemoryCandidateRules.Eligible(
                enabled,
                _ => false,
                2,
                2).Count);
        Assert.AreEqual(
            3,
            MemoryCandidateRules.Undiscovered(
                enabled,
                _ => false).Count);
    }

    [Test]
    public void DiscoveredSetRemovesEveryEnabledMemory()
    {
        HashSet<string> discovered = new HashSet<string>
        {
            "BIO_POLINIZADOR_01",
            "memory_water_01",
            "memory_soil_01"
        };

        Assert.AreEqual(
            0,
            MemoryCandidateRules.Undiscovered(
                new[]
                {
                    "BIO_POLINIZADOR_01",
                    "memory_water_01",
                    "memory_soil_01"
                },
                discovered.Contains).Count);
    }

    [Test]
    public void OneCapturePerWaveAndNoScheduleWithoutCombat()
    {
        Assert.IsFalse(MemoryCandidateRules.CanSchedule(
            true,
            false,
            false,
            false,
            false,
            false));
        Assert.IsFalse(MemoryCandidateRules.CanSchedule(
            true,
            true,
            true,
            false,
            false,
            false));
        Assert.IsFalse(MemoryCandidateRules.CanSchedule(
            true,
            true,
            false,
            true,
            false,
            false));
        Assert.IsFalse(MemoryCandidateRules.CanSchedule(
            true,
            true,
            false,
            false,
            true,
            false));
        Assert.IsFalse(MemoryCandidateRules.CanSchedule(
            false,
            true,
            false,
            false,
            false,
            true));
        Assert.IsTrue(MemoryCandidateRules.CanSchedule(
            true,
            true,
            false,
            false,
            false,
            false));
    }
}
