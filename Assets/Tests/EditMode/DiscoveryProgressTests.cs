using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

public class DiscoveryProgressTests
{
    private string directory;
    private DiscoveryProgressStore store;
    private string persistentSnapshot;

    [SetUp]
    public void SetUp()
    {
        persistentSnapshot = SnapshotPersistentProgress();
        directory = Path.Combine(
            Path.GetTempPath(),
            "bastion-progress-" + Guid.NewGuid().ToString("N"));
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

        Assert.AreEqual(
            persistentSnapshot,
            SnapshotPersistentProgress());
    }

    [Test]
    public void SaveWritesCurrentSchemaAndPreservesExistingIds()
    {
        string[] ids =
        {
            "BIO_POLINIZADOR_01",
            "AGUA_RIEGO_02",
            "bio_polinizador_01"
        };

        ProgressSaveResult saved = store.Save(ids);

        Assert.AreEqual(ProgressSaveStatus.Saved, saved.Status);
        Assert.IsTrue(store.MainFilePath.EndsWith(
            "player_progress.json",
            StringComparison.Ordinal));
        Assert.IsFalse(File.Exists(store.TemporaryFilePath));
        Assert.IsFalse(File.Exists(store.BackupFilePath));

        ProgressParseResult parsed =
            PlayerProgressCodec.Parse(
                File.ReadAllText(store.MainFilePath));

        Assert.IsTrue(parsed.IsValid);
        Assert.IsFalse(parsed.AudioUsesDefaults);
        Assert.AreEqual(
            PlayerProgressCodec.DefaultMusicVolume,
            parsed.MusicVolume,
            0.0001f);
        Assert.AreEqual(
            PlayerProgressCodec.DefaultEffectsVolume,
            parsed.EffectsVolume,
            0.0001f);
        CollectionAssert.AreEqual(ids, parsed.DiscoveredIds);

        string text = File.ReadAllText(store.MainFilePath);
        StringAssert.Contains("\"schemaVersion\": 3", text);
        StringAssert.Contains("\"campaignStarted\"", text);
        StringAssert.Contains("\"discoveredIds\"", text);
        StringAssert.Contains("\"musicVolume\"", text);
        StringAssert.Contains("\"effectsVolume\"", text);
        StringAssert.Contains("BIO_POLINIZADOR_01", text);
    }

    [Test]
    public void NormalizeDropsBlankIdsAndDuplicatesWithoutChangingCase()
    {
        List<string> normalized = PlayerProgressCodec.NormalizeIds(
            new string[]
            {
                null,
                "",
                "   ",
                " AGUA ",
                "BIO_POLINIZADOR_01",
                "BIO_POLINIZADOR_01",
                "bio_polinizador_01",
                "UNKNOWN_FUTURO"
            });

        CollectionAssert.AreEqual(
            new[]
            {
                " AGUA ",
                "BIO_POLINIZADOR_01",
                "bio_polinizador_01",
                "UNKNOWN_FUTURO"
            },
            normalized);
    }

    [Test]
    public void MissingFileLoadsAnEmptyCollectionWithoutCreatingIt()
    {
        ProgressLoadResult loaded = store.Load();

        Assert.AreEqual(ProgressLoadStatus.Missing, loaded.Status);
        Assert.AreEqual(0, loaded.DiscoveredIds.Count);
        Assert.IsFalse(loaded.WritesBlocked);
        Assert.IsFalse(File.Exists(store.MainFilePath));
        Assert.AreEqual(0, Directory.GetFiles(directory).Length);
    }

    [Test]
    public void ExplicitEmptyProgressIsDistinctFromArbitraryJson()
    {
        File.WriteAllText(
            store.MainFilePath,
            "{\"schemaVersion\":1,\"discoveredIds\":[]}");

        ProgressLoadResult empty = store.Load();

        Assert.AreEqual(ProgressLoadStatus.Loaded, empty.Status);
        Assert.AreEqual(0, empty.DiscoveredIds.Count);
        Assert.IsTrue(empty.RequiresSave);
        Assert.IsTrue(empty.AudioUsesDefaults);
        Assert.AreEqual(
            PlayerProgressCodec.DefaultMusicVolume,
            empty.MusicVolume,
            0.0001f);
        Assert.AreEqual(
            PlayerProgressCodec.DefaultEffectsVolume,
            empty.EffectsVolume,
            0.0001f);
        Assert.AreEqual(
            "{\"schemaVersion\":1,\"discoveredIds\":[]}",
            File.ReadAllText(store.MainFilePath));
        Assert.IsTrue(File.Exists(store.MainFilePath));

        File.WriteAllText(
            store.MainFilePath,
            "{\"coins\":10}");
        ProgressLoadResult arbitrary = store.Load();

        Assert.AreEqual(
            ProgressLoadStatus.InvalidStructure,
            arbitrary.Status);
        Assert.AreEqual(0, arbitrary.DiscoveredIds.Count);
        Assert.IsFalse(File.Exists(store.MainFilePath));
        Assert.IsNotNull(FileContaining("{\"coins\":10}"));
    }

    [Test]
    public void InvalidJsonIsDistinctFromInvalidStructure()
    {
        Assert.AreEqual(
            ProgressFileProblem.InvalidJson,
            PlayerProgressCodec.Parse("{").Problem);
        Assert.AreEqual(
            ProgressFileProblem.InvalidJson,
            PlayerProgressCodec.Parse("").Problem);
        Assert.AreEqual(
            ProgressFileProblem.InvalidStructure,
            PlayerProgressCodec.Parse("{}").Problem);
        Assert.AreEqual(
            ProgressFileProblem.InvalidStructure,
            PlayerProgressCodec.Parse(
                "{\"schemaVersion\":1}").Problem);
        Assert.AreEqual(
            ProgressFileProblem.InvalidStructure,
            PlayerProgressCodec.Parse(
                "{\"schemaVersion\":0,\"discoveredIds\":[]}").Problem);
        Assert.AreEqual(
            ProgressFileProblem.InvalidStructure,
            PlayerProgressCodec.Parse(
                "{\"schemaVersion\":\"1\",\"discoveredIds\":[]}").Problem);
        Assert.AreEqual(
            ProgressFileProblem.InvalidStructure,
            PlayerProgressCodec.Parse(
                "{\"schemaVersion\":1,\"discoveredIds\":[1]}").Problem);
        Assert.AreEqual(
            ProgressFileProblem.UnsupportedSchema,
            PlayerProgressCodec.Parse(
                "{\"schemaVersion\":4,\"future\":true}").Problem);
        Assert.AreEqual(
            ProgressFileProblem.UnsupportedSchema,
            PlayerProgressCodec.Parse(
                "{\"schemaVersion\":4.0,\"discoveredIds\":[]}").Problem);
        Assert.AreEqual(
            ProgressFileProblem.InvalidStructure,
            PlayerProgressCodec.Parse(
                "{\"schemaVersion\":2.0,\"discoveredIds\":[]}").Problem);
        Assert.AreEqual(
            ProgressFileProblem.InvalidStructure,
            PlayerProgressCodec.Parse(
                "{\"schemaVersion\":2,\"discoveredIds\":[]}").Problem);
        Assert.AreEqual(
            ProgressFileProblem.InvalidStructure,
            PlayerProgressCodec.Parse(
                "{\"schemaVersion\":2,\"discoveredIds\":[]," +
                "\"musicVolume\":1.5,\"effectsVolume\":0.5}").Problem);
        Assert.AreEqual(
            ProgressFileProblem.InvalidStructure,
            PlayerProgressCodec.Parse(
                "{\"schemaVersion\":1,\"schemaVersion\":1," +
                "\"discoveredIds\":[]}").Problem);
    }

    [Test]
    public void LoadDropsBlankAndDuplicateIdsAndKeepsUnknownIds()
    {
        File.WriteAllText(
            store.MainFilePath,
            "{\"schemaVersion\":1,\"discoveredIds\":" +
            "[\"BIO_POLINIZADOR_01\",\" \",null," +
            "\"BIO_POLINIZADOR_01\",\"UNKNOWN_FUTURO\"," +
            "\"bio_polinizador_01\"]}");

        ProgressLoadResult loaded = store.Load();

        Assert.AreEqual(ProgressLoadStatus.Loaded, loaded.Status);
        CollectionAssert.AreEqual(
            new[]
            {
                "BIO_POLINIZADOR_01",
                "UNKNOWN_FUTURO",
                "bio_polinizador_01"
            },
            loaded.DiscoveredIds);
    }

    [Test]
    public void SecondSaveKeepsThePreviousValidFileAsBackup()
    {
        Assert.AreEqual(
            ProgressSaveStatus.Saved,
            store.Save(new[] { "BIO_POLINIZADOR_01" }).Status);

        string first = File.ReadAllText(store.MainFilePath);

        Assert.AreEqual(
            ProgressSaveStatus.Saved,
            store.Save(new[]
            {
                "BIO_POLINIZADOR_01",
                "UNKNOWN_FUTURO"
            }).Status);

        Assert.AreEqual(first, File.ReadAllText(store.BackupFilePath));
        CollectionAssert.AreEqual(
            new[] { "BIO_POLINIZADOR_01", "UNKNOWN_FUTURO" },
            PlayerProgressCodec.Parse(
                File.ReadAllText(store.MainFilePath)).DiscoveredIds);
        Assert.IsFalse(File.Exists(store.TemporaryFilePath));
    }

    [Test]
    public void CorruptMainRestoresBackupWithoutReplacingIt()
    {
        store.Save(new[] { "BIO_POLINIZADOR_01" });
        string valid = File.ReadAllText(store.MainFilePath);
        File.Copy(store.MainFilePath, store.BackupFilePath);
        File.WriteAllText(store.MainFilePath, "{broken-main");

        ProgressLoadResult loaded = store.Load();

        Assert.AreEqual(
            ProgressLoadStatus.RecoveredFromBackup,
            loaded.Status);
        Assert.AreEqual(
            ProgressFileProblem.InvalidJson,
            loaded.MainProblem);
        CollectionAssert.AreEqual(
            new[] { "BIO_POLINIZADOR_01" },
            loaded.DiscoveredIds);
        Assert.AreEqual(valid, File.ReadAllText(store.BackupFilePath));
        Assert.AreEqual(valid, File.ReadAllText(store.MainFilePath));
        Assert.AreEqual(
            "{broken-main",
            File.ReadAllText(FileContaining("{broken-main")));
    }

    [Test]
    public void SavingOverCorruptMainDoesNotReplaceValidBackup()
    {
        store.Save(new[] { "BIO_POLINIZADOR_01" });
        string valid = File.ReadAllText(store.MainFilePath);
        File.Copy(store.MainFilePath, store.BackupFilePath);
        File.WriteAllText(store.MainFilePath, "CORRUPT_MAIN");

        ProgressSaveResult saved = store.Save(new[]
        {
            "BIO_POLINIZADOR_01",
            "UNKNOWN_FUTURO"
        });

        Assert.AreEqual(ProgressSaveStatus.Saved, saved.Status);
        Assert.AreEqual(valid, File.ReadAllText(store.BackupFilePath));
        CollectionAssert.AreEqual(
            new[] { "BIO_POLINIZADOR_01", "UNKNOWN_FUTURO" },
            store.Load().DiscoveredIds);
        Assert.AreEqual(
            "CORRUPT_MAIN",
            File.ReadAllText(FileContaining("CORRUPT_MAIN")));
    }

    [Test]
    public void InvalidMainAndBackupStartEmptyAndKeepBothCopies()
    {
        File.WriteAllText(store.MainFilePath, "CORRUPT_MAIN");
        File.WriteAllText(store.BackupFilePath, "CORRUPT_BAK");

        ProgressLoadResult loaded = store.Load();

        Assert.AreEqual(ProgressLoadStatus.InvalidJson, loaded.Status);
        Assert.AreEqual(0, loaded.DiscoveredIds.Count);
        Assert.IsFalse(loaded.WritesBlocked);
        Assert.IsFalse(File.Exists(store.MainFilePath));
        Assert.IsFalse(File.Exists(store.BackupFilePath));
        Assert.AreEqual(
            "CORRUPT_MAIN",
            File.ReadAllText(FileContaining("CORRUPT_MAIN")));
        Assert.AreEqual(
            "CORRUPT_BAK",
            File.ReadAllText(FileContaining("CORRUPT_BAK")));
    }

    [Test]
    public void NewerSchemaIsPreservedAndNotOverwritten()
    {
        string future =
            "{\"schemaVersion\":4,\"discoveredIds\":[\"BIO_POLINIZADOR_01\"]," +
            "\"musicVolume\":0.2,\"effectsVolume\":0.4}";
        File.WriteAllText(store.MainFilePath, future);

        ProgressLoadResult loaded = store.Load();
        ProgressSaveResult saved =
            store.Save(new[] { "OTHER_ID" });

        Assert.AreEqual(
            ProgressLoadStatus.UnsupportedSchema,
            loaded.Status);
        Assert.IsTrue(loaded.WritesBlocked);
        Assert.AreEqual(0, loaded.DiscoveredIds.Count);
        Assert.AreEqual(
            ProgressSaveStatus.BlockedUnsupportedSchema,
            saved.Status);
        Assert.AreEqual(future, File.ReadAllText(store.MainFilePath));
        Assert.IsFalse(File.Exists(store.BackupFilePath));
        Assert.IsFalse(File.Exists(store.TemporaryFilePath));
    }

    [Test]
    public void FailedWriteLeavesThePreviousProgressUntouched()
    {
        store.Save(new[] { "BIO_POLINIZADOR_01" });
        string before = File.ReadAllText(store.MainFilePath);
        Directory.CreateDirectory(store.TemporaryFilePath);

        ProgressSaveResult saved = store.Save(new[]
        {
            "BIO_POLINIZADOR_01",
            "UNKNOWN_FUTURO"
        });

        Assert.AreEqual(ProgressSaveStatus.AccessFailed, saved.Status);
        Assert.AreEqual(before, File.ReadAllText(store.MainFilePath));
        Assert.IsFalse(File.Exists(store.BackupFilePath));
    }

    [Test]
    public void SaveToAnUnusableDirectoryFailsWithoutAnotherProgressFile()
    {
        string blocker = Path.Combine(directory, "not-a-directory");
        File.WriteAllText(blocker, "blocked");
        DiscoveryProgressStore blockedStore =
            new DiscoveryProgressStore(blocker);

        ProgressSaveResult saved =
            blockedStore.Save(new[] { "BIO_POLINIZADOR_01" });

        Assert.AreEqual(ProgressSaveStatus.AccessFailed, saved.Status);
        Assert.AreEqual("blocked", File.ReadAllText(blocker));
        Assert.AreEqual(1, Directory.GetFiles(directory).Length);
    }

    [Test]
    public void CurrentSchemaRoundTripsAudioPreferences()
    {
        store.SetAudioPreferences(0.2f, 0.4f);

        Assert.AreEqual(
            ProgressSaveStatus.Saved,
            store.Save(new[] { "BIO_POLINIZADOR_01" }).Status);

        DiscoveryProgressStore reloaded =
            new DiscoveryProgressStore(directory);
        ProgressLoadResult loaded = reloaded.Load();

        Assert.AreEqual(ProgressLoadStatus.Loaded, loaded.Status);
        Assert.IsFalse(loaded.AudioUsesDefaults);
        Assert.IsFalse(loaded.RequiresSave);
        Assert.IsFalse(loaded.WritesBlocked);
        Assert.AreEqual(0.2f, loaded.MusicVolume, 0.0001f);
        Assert.AreEqual(0.4f, loaded.EffectsVolume, 0.0001f);
        CollectionAssert.AreEqual(
            new[] { "BIO_POLINIZADOR_01" },
            loaded.DiscoveredIds);
    }

    [Test]
    public void SaveKeepsAudioWhenDiscoveriesChange()
    {
        store.SetAudioPreferences(0.25f, 0.5f);
        store.Save(new[] { "BIO_POLINIZADOR_01" });
        store.Save(new[]
        {
            "BIO_POLINIZADOR_01",
            "UNKNOWN_FUTURO"
        });

        ProgressParseResult parsed = PlayerProgressCodec.Parse(
            File.ReadAllText(store.MainFilePath));

        Assert.IsTrue(parsed.IsValid);
        Assert.AreEqual(0.25f, parsed.MusicVolume, 0.0001f);
        Assert.AreEqual(0.5f, parsed.EffectsVolume, 0.0001f);
        CollectionAssert.AreEqual(
            new[] { "BIO_POLINIZADOR_01", "UNKNOWN_FUTURO" },
            parsed.DiscoveredIds);
    }

    [Test]
    public void SchemaTwoWithoutAudioIsQuarantinedAndNotTreatedAsEmpty()
    {
        string json =
            "{\"schemaVersion\":2,\"discoveredIds\":[\"BIO_POLINIZADOR_01\"]}";
        File.WriteAllText(store.MainFilePath, json);

        ProgressLoadResult loaded = store.Load();

        Assert.AreEqual(
            ProgressLoadStatus.InvalidStructure,
            loaded.Status);
        Assert.IsFalse(loaded.WritesBlocked);
        Assert.AreEqual(0, loaded.DiscoveredIds.Count);
        Assert.IsFalse(File.Exists(store.MainFilePath));
        Assert.AreEqual(json, File.ReadAllText(FileContaining(json)));
    }

    private string FileContaining(string content)
    {
        foreach (string path in Directory.GetFiles(directory))
        {
            if (File.ReadAllText(path).Contains(content))
            {
                return path;
            }
        }

        Assert.Fail("Did not find preserved content: " + content);
        return null;
    }

    private static string SnapshotPersistentProgress()
    {
#if UNITY_2019_1_OR_NEWER
        string persistentPath;

        try
        {
            persistentPath = UnityEngine.Application.persistentDataPath;
        }
        catch (UnityEngine.UnityException)
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(persistentPath) ||
            !Directory.Exists(persistentPath))
        {
            return string.Empty;
        }

        StringBuilder snapshot = new StringBuilder();
        string[] files = Directory.GetFiles(
            persistentPath,
            "*",
            SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);

        foreach (string path in files)
        {
            snapshot.Append(path);
            snapshot.Append(':');
            snapshot.Append(new FileInfo(path).Length);
            snapshot.Append(':');
            snapshot.Append(File.GetLastWriteTimeUtc(path).Ticks);
            snapshot.Append('\n');
        }

        return snapshot.ToString();
#else
        return string.Empty;
#endif
    }
}
