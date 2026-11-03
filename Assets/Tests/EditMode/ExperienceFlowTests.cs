using System.IO;
using NUnit.Framework;

public class ExperienceFlowTests
{
    private string directory;
    private DiscoveryProgressStore store;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(
            Path.GetTempPath(),
            "bastion-campaign-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        store = new DiscoveryProgressStore(directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void LayoutKeepsBothRoutesAndFourteenPlots()
    {
        Assert.IsNull(ExperienceLayout.Validate());
    }

    [Test]
    public void ContinueUsesCompletedMapsAndIgnoresDiscoveries()
    {
        Assert.IsFalse(CampaignRules.CanContinue(false));
        Assert.IsNull(CampaignRules.Destination(false, new[] { "Level_01" }));
        Assert.AreEqual(
            "Level_01",
            CampaignRules.Destination(true, new string[0]));
        Assert.AreEqual(
            "Level_02",
            CampaignRules.Destination(true, new[] { "Level_01" }));
        Assert.AreEqual(
            "Level_02",
            CampaignRules.Destination(
                true,
                new[] { "Level_01", "Level_02" }));
        Assert.IsFalse(CampaignRules.IsPlayableMap("Level_03"));
        Assert.IsTrue(CampaignRules.NeedsConfirmation(
            false,
            0,
            1,
            0,
            0));
    }

    [Test]
    public void VictoryRecordsAMapOnce()
    {
        CampaignSnapshot once = CampaignRules.RecordVictory(
            CampaignSnapshot.Empty,
            "Level_01",
            true);
        CampaignSnapshot twice = CampaignRules.RecordVictory(
            once,
            "Level_01",
            true);
        CampaignSnapshot ignored = CampaignRules.RecordVictory(
            once,
            "Level_03",
            true);

        Assert.IsTrue(once.Started);
        Assert.AreEqual(1, once.CompletedMaps.Count);
        Assert.AreEqual(1, twice.CompletedMaps.Count);
        Assert.AreEqual(1, ignored.CompletedMaps.Count);
        Assert.AreEqual(
            CampaignSnapshot.Empty.Started,
            CampaignRules.RecordVictory(
                CampaignSnapshot.Empty,
                "Level_01",
                false).Started);
    }

    [Test]
    public void ResetClearsDiscoveriesAndKeepsAudioInBothCopies()
    {
        store.SetAudioPreferences(0.2f, 0.4f);
        Assert.AreEqual(
            ProgressSaveStatus.Saved,
            store.Save(new[] { "BIO_POLINIZADOR_01" }).Status);
        Assert.IsTrue(store.NoteMap("Level_01"));
        Assert.AreEqual(
            ProgressSaveStatus.Saved,
            store.Save(new[] { "BIO_POLINIZADOR_01" }).Status);
        Assert.IsFalse(store.NoteMap("Level_01"));

        ProgressSaveResult reset = store.ResetCampaign();

        Assert.AreEqual(ProgressSaveStatus.Saved, reset.Status);
        Assert.IsFalse(store.CampaignStarted);
        Assert.AreEqual(0, store.CompletedMaps.Count);
        ProgressParseResult main = PlayerProgressCodec.Parse(
            File.ReadAllText(store.MainFilePath));
        ProgressParseResult backup = PlayerProgressCodec.Parse(
            File.ReadAllText(store.BackupFilePath));
        Assert.AreEqual(0, main.DiscoveredIds.Count);
        Assert.AreEqual(0, backup.DiscoveredIds.Count);
        Assert.AreEqual(0.2f, main.MusicVolume, 0.0001f);
        Assert.AreEqual(0.4f, backup.EffectsVolume, 0.0001f);
        Assert.IsFalse(main.Campaign.Started);
        Assert.IsFalse(File.Exists(store.TemporaryFilePath));
    }

    [Test]
    public void SchemaTwoMigratesWithoutInventingCompletedMaps()
    {
        File.WriteAllText(
            store.MainFilePath,
            "{\"schemaVersion\":2,\"discoveredIds\":[\"BIO_POLINIZADOR_01\"]," +
            "\"musicVolume\":0.3,\"effectsVolume\":0.6}");

        ProgressLoadResult loaded = store.Load();

        Assert.AreEqual(ProgressLoadStatus.Loaded, loaded.Status);
        Assert.IsTrue(loaded.NeedsMigration);
        Assert.IsFalse(loaded.AudioUsesDefaults);
        Assert.IsFalse(loaded.Campaign.Started);
        Assert.AreEqual(0, loaded.Campaign.CompletedMaps.Count);
        CollectionAssert.AreEqual(
            new[] { "BIO_POLINIZADOR_01" },
            loaded.DiscoveredIds);
    }

    [Test]
    public void NewerSchemaStillBlocksReset()
    {
        string future =
            "{\"schemaVersion\":4,\"discoveredIds\":[\"BIO_POLINIZADOR_01\"]," +
            "\"musicVolume\":0.2,\"effectsVolume\":0.4}";
        File.WriteAllText(store.MainFilePath, future);

        Assert.AreEqual(
            ProgressSaveStatus.BlockedUnsupportedSchema,
            store.ResetCampaign().Status);
        Assert.AreEqual(future, File.ReadAllText(store.MainFilePath));
        Assert.IsFalse(File.Exists(store.BackupFilePath));
    }
}
