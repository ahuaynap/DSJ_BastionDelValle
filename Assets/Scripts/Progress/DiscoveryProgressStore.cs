using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public sealed class DiscoveryProgressStore
{
    public const string FileName = "player_progress.json";

    private const string BackupSuffix = ".bak";
    private const string TemporarySuffix = ".tmp";
    private const string CorruptSuffix = ".corrupt";

    private static readonly UTF8Encoding Utf8 =
        new UTF8Encoding(false);

    private readonly string directoryPath;

    public DiscoveryProgressStore(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException(
                "A progress directory is required.",
                nameof(directoryPath));
        }

        this.directoryPath = directoryPath;
    }

    public string MainFilePath =>
        Path.Combine(directoryPath, FileName);

    public string BackupFilePath =>
        MainFilePath + BackupSuffix;

    public string TemporaryFilePath =>
        MainFilePath + TemporarySuffix;

    public bool WritesBlocked { get; private set; }

    public float MusicVolume { get; private set; } =
        PlayerProgressCodec.DefaultMusicVolume;

    public float EffectsVolume { get; private set; } =
        PlayerProgressCodec.DefaultEffectsVolume;

    private bool campaignStarted;
    private readonly List<string> completedMaps = new();
    private readonly List<string> clearedTutorials = new();
    private readonly List<string> evaluations = new();

    public bool CampaignStarted => campaignStarted;

    public IReadOnlyList<string> CompletedMaps => completedMaps;

    public IReadOnlyList<string> ClearedTutorials => clearedTutorials;

    public IReadOnlyList<string> Evaluations => evaluations;

    public CampaignSnapshot Snapshot()
    {
        return new CampaignSnapshot(
            campaignStarted,
            completedMaps,
            clearedTutorials,
            evaluations);
    }

    public void NoteStarted()
    {
        campaignStarted = true;
    }

    public bool NoteMap(string sceneName)
    {
        if (!CampaignRules.IsPlayableMap(sceneName))
        {
            return false;
        }

        campaignStarted = true;

        for (int i = 0; i < completedMaps.Count; i++)
        {
            if (completedMaps[i] == sceneName)
            {
                return false;
            }
        }

        completedMaps.Add(sceneName);
        return true;
    }

    public void SetAudioPreferences(float music, float effects)
    {
        MusicVolume = PlayerProgressCodec.ClampVolume(music);
        EffectsVolume = PlayerProgressCodec.ClampVolume(effects);
    }

    public ProgressLoadResult Load()
    {
        WritesBlocked = false;
        FileRead main = Read(MainFilePath);

        if (main.Problem == ProgressFileProblem.AccessFailed)
        {
            WritesBlocked = true;
            return Result(
                ProgressLoadStatus.AccessFailed,
                main.Problem,
                Array.Empty<string>(),
                true,
                false,
                "The progress file could not be read and will " +
                "not be overwritten this session. " +
                main.Detail);
        }

        if (main.Problem ==
            ProgressFileProblem.UnsupportedSchema)
        {
            WritesBlocked = true;
            return Result(
                ProgressLoadStatus.UnsupportedSchema,
                main.Problem,
                Array.Empty<string>(),
                true,
                false,
                "The progress file uses a newer schema and " +
                "was left unchanged. " + main.Detail);
        }

        if (main.Problem == ProgressFileProblem.None)
        {
            FileRead existingBackup = Read(BackupFilePath);

            if (existingBackup.Problem ==
                ProgressFileProblem.UnsupportedSchema)
            {
                WritesBlocked = true;
            return Result(
                ProgressLoadStatus.Loaded,
                ProgressFileProblem.None,
                main.Ids,
                true,
                false,
                "Discoveries were loaded, but the backup uses " +
                "a newer schema. Progress will not be overwritten.",
                main.MusicVolume,
                main.EffectsVolume,
                true,
                main.AudioUsesDefaults,
                false,
                main.Campaign);
            }

            if (existingBackup.Problem ==
                ProgressFileProblem.AccessFailed)
            {
                WritesBlocked = true;
            return Result(
                ProgressLoadStatus.Loaded,
                ProgressFileProblem.None,
                main.Ids,
                true,
                false,
                "Discoveries were loaded, but the backup could " +
                "not be read. Progress will not be overwritten. " +
                existingBackup.Detail,
                main.MusicVolume,
                main.EffectsVolume,
                true,
                main.AudioUsesDefaults,
                false,
                main.Campaign);
            }

            return Result(
                ProgressLoadStatus.Loaded,
                ProgressFileProblem.None,
                main.Ids,
                false,
                main.NeedsMigration,
                string.Empty,
                main.MusicVolume,
                main.EffectsVolume,
                true,
                main.AudioUsesDefaults,
                main.NeedsMigration,
                main.Campaign);
        }

        string preservedMain = string.Empty;

        if (main.Problem != ProgressFileProblem.Missing)
        {
            if (!TryQuarantine(
                    MainFilePath,
                    out string quarantinedPath))
            {
                WritesBlocked = true;
                return Result(
                    ProgressLoadStatus.AccessFailed,
                    ProgressFileProblem.AccessFailed,
                    Array.Empty<string>(),
                    true,
                    false,
                    "The unusable progress file could not be " +
                    "preserved, so it was not replaced.");
            }

            preservedMain = quarantinedPath;
        }

        FileRead backup = Read(BackupFilePath);

        if (backup.Problem == ProgressFileProblem.None)
        {
            bool restored = TryRestoreMainFromBackup();
            string detail =
                "The progress file was " +
                Describe(main.Problem) +
                ". Discoveries were restored from the backup.";

            if (!string.IsNullOrEmpty(preservedMain))
            {
                detail += " Kept " + preservedMain + ".";
            }

            if (!restored)
            {
                detail +=
                    " The backup could not be copied back " +
                    "to the main file.";
            }

            return Result(
                ProgressLoadStatus.RecoveredFromBackup,
                main.Problem,
                backup.Ids,
                false,
                !restored || backup.NeedsMigration,
                detail,
                backup.MusicVolume,
                backup.EffectsVolume,
                true,
                backup.AudioUsesDefaults,
                !restored || backup.NeedsMigration,
                backup.Campaign);
        }

        if (backup.Problem ==
            ProgressFileProblem.UnsupportedSchema)
        {
            WritesBlocked = true;
            return Result(
                ProgressLoadStatus.UnsupportedSchema,
                main.Problem,
                Array.Empty<string>(),
                true,
                false,
                "The progress backup uses a newer schema and " +
                "was left unchanged. " + backup.Detail);
        }

        if (backup.Problem == ProgressFileProblem.AccessFailed)
        {
            WritesBlocked = true;
            return Result(
                ProgressLoadStatus.AccessFailed,
                main.Problem,
                Array.Empty<string>(),
                true,
                false,
                "The progress backup could not be read. " +
                backup.Detail);
        }

        if (main.Problem == ProgressFileProblem.Missing &&
            backup.Problem == ProgressFileProblem.Missing)
        {
            return Result(
                ProgressLoadStatus.Missing,
                ProgressFileProblem.Missing,
                Array.Empty<string>(),
                false,
                false,
                string.Empty);
        }

        string preservedBackup = string.Empty;

        if (backup.Problem != ProgressFileProblem.Missing)
        {
            if (!TryQuarantine(
                    BackupFilePath,
                    out string quarantinedBackup))
            {
                WritesBlocked = true;
                return Result(
                    ProgressLoadStatus.AccessFailed,
                    ProgressFileProblem.AccessFailed,
                    Array.Empty<string>(),
                    true,
                    false,
                    "The unusable progress backup could not be " +
                    "preserved.");
            }

            preservedBackup = quarantinedBackup;
        }

        ProgressFileProblem reportedProblem =
            main.Problem == ProgressFileProblem.Missing
                ? backup.Problem
                : main.Problem;

        ProgressLoadStatus status =
            reportedProblem == ProgressFileProblem.InvalidJson
                ? ProgressLoadStatus.InvalidJson
                : ProgressLoadStatus.InvalidStructure;

        string failure =
            "Discovery progress could not be used because " +
            "the main file is " + Describe(main.Problem) +
            " and the backup is " + Describe(backup.Problem) +
            ". Starting an empty collection.";

        if (!string.IsNullOrEmpty(preservedMain))
        {
            failure += " Kept " + preservedMain + ".";
        }

        if (!string.IsNullOrEmpty(preservedBackup))
        {
            failure += " Kept " + preservedBackup + ".";
        }

        return Result(
            status,
            reportedProblem,
            Array.Empty<string>(),
            false,
            false,
            failure);
    }

    public ProgressSaveResult Save(
        IReadOnlyList<string> discoveredIds)
    {
        if (WritesBlocked)
        {
            return new ProgressSaveResult(
                ProgressSaveStatus.BlockedUnsupportedSchema,
                "Progress writes are blocked for this session.");
        }

        FileRead main = Read(MainFilePath);

        if (main.Problem ==
            ProgressFileProblem.UnsupportedSchema)
        {
            WritesBlocked = true;
            return new ProgressSaveResult(
                ProgressSaveStatus.BlockedUnsupportedSchema,
                main.Detail);
        }

        FileRead backup = Read(BackupFilePath);

        if (backup.Problem ==
            ProgressFileProblem.UnsupportedSchema)
        {
            WritesBlocked = true;
            return new ProgressSaveResult(
                ProgressSaveStatus.BlockedUnsupportedSchema,
                backup.Detail);
        }

        if (main.Problem == ProgressFileProblem.AccessFailed ||
            backup.Problem == ProgressFileProblem.AccessFailed)
        {
            string detail = main.Problem ==
                ProgressFileProblem.AccessFailed
                    ? main.Detail
                    : backup.Detail;

            return new ProgressSaveResult(
                ProgressSaveStatus.AccessFailed,
                detail);
        }

        if (WritesBlocked)
        {
            return new ProgressSaveResult(
                ProgressSaveStatus.BlockedUnsupportedSchema,
                "Progress writes are blocked.");
        }

        List<string> normalized =
            PlayerProgressCodec.NormalizeIds(discoveredIds);
        string json = PlayerProgressCodec.Serialize(
            normalized,
            MusicVolume,
            EffectsVolume,
            Snapshot());
        ProgressParseResult serialized =
            PlayerProgressCodec.Parse(json);

        if (!serialized.IsValid ||
            !SameIds(serialized.DiscoveredIds, normalized) ||
            !SameVolume(serialized.MusicVolume, MusicVolume) ||
            !SameVolume(serialized.EffectsVolume, EffectsVolume) ||
            !SameCampaign(serialized.Campaign, Snapshot()))
        {
            return new ProgressSaveResult(
                ProgressSaveStatus.ValidationFailed,
                "Serialized progress failed validation.");
        }

        try
        {
            Directory.CreateDirectory(directoryPath);
            File.WriteAllText(TemporaryFilePath, json, Utf8);

            string readBack = File.ReadAllText(
                TemporaryFilePath,
                Utf8);
            ProgressParseResult written =
                PlayerProgressCodec.Parse(readBack);

            if (!written.IsValid ||
                !SameIds(written.DiscoveredIds, normalized) ||
                !SameVolume(written.MusicVolume, MusicVolume) ||
                !SameVolume(written.EffectsVolume, EffectsVolume) ||
                !SameCampaign(written.Campaign, Snapshot()))
            {
                DeleteTempFile();
                return new ProgressSaveResult(
                    ProgressSaveStatus.ValidationFailed,
                    "Temporary progress did not match the discoveries.");
            }

            ProgressSaveResult committed = Commit(main.Problem);
            DeleteTempFile();
            return committed;
        }
        catch (IOException exception)
        {
            return RecoverOrFail(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return RecoverOrFail(exception.Message);
        }
    }

    private ProgressSaveResult Commit(
        ProgressFileProblem mainProblem)
    {
        if (!File.Exists(MainFilePath))
        {
            File.Move(TemporaryFilePath, MainFilePath);
            return new ProgressSaveResult(
                ProgressSaveStatus.Saved,
                string.Empty);
        }

        if (mainProblem != ProgressFileProblem.None)
        {
            if (!TryQuarantine(MainFilePath, out string quarantined))
            {
                return new ProgressSaveResult(
                    ProgressSaveStatus.AccessFailed,
                    "The unusable progress file could not be preserved.");
            }

            File.Move(TemporaryFilePath, MainFilePath);
            return new ProgressSaveResult(
                ProgressSaveStatus.Saved,
                "Kept " + quarantined + ".");
        }

        if (!PrepareBackupForReplacement())
        {
            return new ProgressSaveResult(
                ProgressSaveStatus.AccessFailed,
                "The existing progress backup could not be preserved.");
        }

        // File.Replace is the desktop replace used by this project
        // (.NET Standard 2.1). On Windows it replaces the main file
        // and stores the previous valid main as the backup. macOS
        // and Linux use the same API. If the runtime does not
        // support it, the fallback copies that previous valid main
        // to the backup and then moves the validated temp into place.
        try
        {
            File.Replace(
                TemporaryFilePath,
                MainFilePath,
                BackupFilePath);
        }
        catch (PlatformNotSupportedException)
        {
            File.Copy(MainFilePath, BackupFilePath, true);
            File.Delete(MainFilePath);
            File.Move(TemporaryFilePath, MainFilePath);
        }

        return new ProgressSaveResult(
            ProgressSaveStatus.Saved,
            string.Empty);
    }

    public ProgressSaveResult ResetCampaign()
    {
        if (WritesBlocked)
        {
            return new ProgressSaveResult(
                ProgressSaveStatus.BlockedUnsupportedSchema,
                "Progress writes are blocked for this session.");
        }

        FileRead main = Read(MainFilePath);
        FileRead backup = Read(BackupFilePath);

        if (main.Problem == ProgressFileProblem.UnsupportedSchema ||
            backup.Problem == ProgressFileProblem.UnsupportedSchema)
        {
            WritesBlocked = true;
            return new ProgressSaveResult(
                ProgressSaveStatus.BlockedUnsupportedSchema,
                "The progress file uses a newer schema and was left unchanged.");
        }

        if (main.Problem == ProgressFileProblem.AccessFailed ||
            backup.Problem == ProgressFileProblem.AccessFailed)
        {
            return new ProgressSaveResult(
                ProgressSaveStatus.AccessFailed,
                main.Problem == ProgressFileProblem.AccessFailed
                    ? main.Detail
                    : backup.Detail);
        }

        string json = PlayerProgressCodec.Serialize(
            Array.Empty<string>(),
            MusicVolume,
            EffectsVolume,
            CampaignSnapshot.Empty);
        ProgressParseResult serialized = PlayerProgressCodec.Parse(json);

        if (!serialized.IsValid ||
            serialized.DiscoveredIds.Count != 0 ||
            serialized.Campaign.Started ||
            serialized.Campaign.CompletedMaps.Count != 0)
        {
            return new ProgressSaveResult(
                ProgressSaveStatus.ValidationFailed,
                "The reset progress failed validation.");
        }

        byte[] previousMain = ReadBytes(MainFilePath);
        byte[] previousBackup = ReadBytes(BackupFilePath);

        try
        {
            Directory.CreateDirectory(directoryPath);
            File.WriteAllBytes(TemporaryFilePath, Utf8.GetBytes(json));
            File.Copy(TemporaryFilePath, MainFilePath, true);
            File.Copy(TemporaryFilePath, BackupFilePath, true);
            ProgressParseResult writtenMain =
                PlayerProgressCodec.Parse(File.ReadAllText(MainFilePath, Utf8));
            ProgressParseResult writtenBackup =
                PlayerProgressCodec.Parse(File.ReadAllText(BackupFilePath, Utf8));

            if (!writtenMain.IsValid ||
                !writtenBackup.IsValid ||
                writtenMain.DiscoveredIds.Count != 0 ||
                writtenBackup.DiscoveredIds.Count != 0 ||
                !SameVolume(writtenMain.MusicVolume, MusicVolume) ||
                !SameVolume(writtenBackup.MusicVolume, MusicVolume))
            {
                RestoreBytes(MainFilePath, previousMain);
                RestoreBytes(BackupFilePath, previousBackup);
                DeleteTempFile();
                return new ProgressSaveResult(
                    ProgressSaveStatus.ValidationFailed,
                    "The reset progress did not stay on disk.");
            }

            DeleteTempFile();
            campaignStarted = false;
            completedMaps.Clear();
            clearedTutorials.Clear();
            evaluations.Clear();
            return new ProgressSaveResult(
                ProgressSaveStatus.Saved,
                string.Empty);
        }
        catch (IOException exception)
        {
            RestoreBytes(MainFilePath, previousMain);
            RestoreBytes(BackupFilePath, previousBackup);
            DeleteTempFile();
            return new ProgressSaveResult(
                ProgressSaveStatus.AccessFailed,
                exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            RestoreBytes(MainFilePath, previousMain);
            RestoreBytes(BackupFilePath, previousBackup);
            DeleteTempFile();
            return new ProgressSaveResult(
                ProgressSaveStatus.AccessFailed,
                exception.Message);
        }
    }

    private bool PrepareBackupForReplacement()
    {
        if (!File.Exists(BackupFilePath))
        {
            return true;
        }

        FileRead backup = Read(BackupFilePath);

        if (backup.Problem == ProgressFileProblem.None)
        {
            return true;
        }

        if (backup.Problem ==
                ProgressFileProblem.UnsupportedSchema ||
            backup.Problem == ProgressFileProblem.AccessFailed)
        {
            return false;
        }

        return TryQuarantine(BackupFilePath, out _);
    }

    private ProgressSaveResult RecoverOrFail(string detail)
    {
        if (!File.Exists(MainFilePath) &&
            File.Exists(TemporaryFilePath))
        {
            try
            {
                File.Move(TemporaryFilePath, MainFilePath);
                return new ProgressSaveResult(
                    ProgressSaveStatus.Saved,
                    string.Empty);
            }
            catch (IOException exception)
            {
                return new ProgressSaveResult(
                    ProgressSaveStatus.AccessFailed,
                    exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                return new ProgressSaveResult(
                    ProgressSaveStatus.AccessFailed,
                    exception.Message);
            }
        }

        DeleteTempFile();
        return new ProgressSaveResult(
            ProgressSaveStatus.AccessFailed,
            detail);
    }

    private bool TryRestoreMainFromBackup()
    {
        try
        {
            if (File.Exists(MainFilePath) ||
                Directory.Exists(MainFilePath))
            {
                return false;
            }

            File.Copy(BackupFilePath, MainFilePath, false);
            FileRead restored = Read(MainFilePath);

            if (restored.Problem != ProgressFileProblem.None)
            {
                if (File.Exists(MainFilePath))
                {
                    File.Delete(MainFilePath);
                }

                return false;
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private FileRead Read(string path)
    {
        if (Directory.Exists(path))
        {
            return FileRead.Failed(
                ProgressFileProblem.AccessFailed,
                "The progress path is a directory.");
        }

        string json;

        try
        {
            if (!File.Exists(path))
            {
                return FileRead.Failed(
                    ProgressFileProblem.Missing,
                    "The progress file is absent.");
            }

            json = File.ReadAllText(path);
        }
        catch (FileNotFoundException)
        {
            return FileRead.Failed(
                ProgressFileProblem.Missing,
                "The progress file is absent.");
        }
        catch (DirectoryNotFoundException)
        {
            return FileRead.Failed(
                ProgressFileProblem.Missing,
                "The progress directory is absent.");
        }
        catch (IOException exception)
        {
            return FileRead.Failed(
                ProgressFileProblem.AccessFailed,
                exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return FileRead.Failed(
                ProgressFileProblem.AccessFailed,
                exception.Message);
        }

        ProgressParseResult parsed =
            PlayerProgressCodec.Parse(json);

        if (parsed.Problem == ProgressFileProblem.None)
        {
            return FileRead.Loaded(
                parsed.DiscoveredIds,
                parsed.MusicVolume,
                parsed.EffectsVolume,
                parsed.AudioUsesDefaults,
                parsed.NeedsMigration,
                parsed.Campaign);
        }

        return FileRead.Failed(parsed.Problem, parsed.Detail);
    }

    private bool TryQuarantine(
        string sourcePath,
        out string quarantinedPath)
    {
        quarantinedPath = string.Empty;

        try
        {
            if (!File.Exists(sourcePath))
            {
                return false;
            }

            string candidate = sourcePath + CorruptSuffix;
            int suffix = 1;

            while (File.Exists(candidate))
            {
                candidate = sourcePath + CorruptSuffix + "." + suffix;
                suffix++;
            }

            File.Move(sourcePath, candidate);
            quarantinedPath = candidate;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void DeleteTempFile()
    {
        try
        {
            if (File.Exists(TemporaryFilePath))
            {
                File.Delete(TemporaryFilePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool SameIds(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        if (left == null || right == null ||
            left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            if (!string.Equals(
                    left[index],
                    right[index],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameVolume(float left, float right)
    {
        return Math.Abs(left - right) <= 0.0001f;
    }

    private static string Describe(ProgressFileProblem problem)
    {
        switch (problem)
        {
            case ProgressFileProblem.Missing:
                return "missing";
            case ProgressFileProblem.InvalidJson:
                return "invalid JSON";
            case ProgressFileProblem.InvalidStructure:
                return "invalid structure";
            case ProgressFileProblem.UnsupportedSchema:
                return "an unsupported schema";
            case ProgressFileProblem.AccessFailed:
                return "inaccessible";
            default:
                return "usable";
        }
    }

    private ProgressLoadResult Result(
        ProgressLoadStatus status,
        ProgressFileProblem mainProblem,
        IReadOnlyList<string> discoveredIds,
        bool writesBlocked,
        bool requiresSave,
        string detail,
        float music = PlayerProgressCodec.DefaultMusicVolume,
        float effects = PlayerProgressCodec.DefaultEffectsVolume,
        bool rememberAudio = false,
        bool audioUsesDefaults = true,
        bool needsMigration = false,
        CampaignSnapshot campaign = default)
    {
        float safeMusic = PlayerProgressCodec.ClampVolume(music);
        float safeEffects = PlayerProgressCodec.ClampVolume(effects);

        if (rememberAudio)
        {
            MusicVolume = safeMusic;
            EffectsVolume = safeEffects;
            Remember(campaign);
        }

        return new ProgressLoadResult(
            status,
            mainProblem,
            discoveredIds,
            writesBlocked,
            requiresSave,
            detail,
            safeMusic,
            safeEffects,
            audioUsesDefaults,
            needsMigration,
            campaign);
    }

    private void Remember(CampaignSnapshot campaign)
    {
        campaignStarted = campaign.Started;
        Replace(completedMaps, campaign.CompletedMaps);
        Replace(clearedTutorials, campaign.ClearedTutorials);
        Replace(evaluations, campaign.Evaluations);
    }

    private static void Replace(
        List<string> target,
        IReadOnlyList<string> values)
    {
        target.Clear();

        if (values == null)
        {
            return;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                target.Add(values[i]);
            }
        }
    }

    private static bool SameCampaign(
        CampaignSnapshot left,
        CampaignSnapshot right)
    {
        return left.Started == right.Started &&
            SameIds(left.CompletedMaps, right.CompletedMaps) &&
            SameIds(left.ClearedTutorials, right.ClearedTutorials) &&
            SameIds(left.Evaluations, right.Evaluations);
    }

    private static byte[] ReadBytes(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void RestoreBytes(string path, byte[] bytes)
    {
        try
        {
            if (bytes == null)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }

            File.WriteAllBytes(path, bytes);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private readonly struct FileRead
    {
        public ProgressFileProblem Problem { get; }
        public IReadOnlyList<string> Ids { get; }
        public float MusicVolume { get; }
        public float EffectsVolume { get; }
        public bool AudioUsesDefaults { get; }
        public bool NeedsMigration { get; }
        public CampaignSnapshot Campaign { get; }
        public string Detail { get; }

        private FileRead(
            ProgressFileProblem problem,
            IReadOnlyList<string> ids,
            string detail,
            float musicVolume,
            float effectsVolume,
            bool audioUsesDefaults,
            bool needsMigration,
            CampaignSnapshot campaign)
        {
            Problem = problem;
            Ids = ids ?? Array.Empty<string>();
            Detail = detail ?? string.Empty;
            MusicVolume = musicVolume;
            EffectsVolume = effectsVolume;
            AudioUsesDefaults = audioUsesDefaults;
            NeedsMigration = needsMigration;
            Campaign = campaign;
        }

        public static FileRead Loaded(
            IReadOnlyList<string> ids,
            float musicVolume,
            float effectsVolume,
            bool audioUsesDefaults,
            bool needsMigration,
            CampaignSnapshot campaign)
        {
            return new FileRead(
                ProgressFileProblem.None,
                ids,
                string.Empty,
                musicVolume,
                effectsVolume,
                audioUsesDefaults,
                needsMigration,
                campaign);
        }

        public static FileRead Failed(
            ProgressFileProblem problem,
            string detail)
        {
            return new FileRead(
                problem,
                Array.Empty<string>(),
                detail,
                PlayerProgressCodec.DefaultMusicVolume,
                PlayerProgressCodec.DefaultEffectsVolume,
                true,
                false,
                CampaignSnapshot.Empty);
        }
    }
}
