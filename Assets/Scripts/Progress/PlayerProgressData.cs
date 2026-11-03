using System;
using System.Collections.Generic;

[Serializable]
public class PlayerProgressData
{
    public int schemaVersion;
    public List<string> discoveredIds;
    public float musicVolume;
    public float effectsVolume;
    public bool campaignStarted;
    public List<string> completedMaps;
    public List<string> clearedTutorials;
    public List<string> evaluations;
}

public enum ProgressFileProblem
{
    None,
    Missing,
    InvalidJson,
    InvalidStructure,
    UnsupportedSchema,
    AccessFailed
}

public enum ProgressLoadStatus
{
    Missing,
    Loaded,
    RecoveredFromBackup,
    UnsupportedSchema,
    InvalidJson,
    InvalidStructure,
    AccessFailed
}

public enum ProgressSaveStatus
{
    Saved,
    AccessFailed,
    ValidationFailed,
    BlockedUnsupportedSchema
}

public readonly struct ProgressParseResult
{
    public ProgressFileProblem Problem { get; }
    public IReadOnlyList<string> DiscoveredIds { get; }
    public float MusicVolume { get; }
    public float EffectsVolume { get; }
    public bool AudioUsesDefaults { get; }
    public bool NeedsMigration { get; }
    public CampaignSnapshot Campaign { get; }
    public string Detail { get; }

    public bool IsValid =>
        Problem == ProgressFileProblem.None;

    private ProgressParseResult(
        ProgressFileProblem problem,
        IReadOnlyList<string> discoveredIds,
        float musicVolume,
        float effectsVolume,
        bool audioUsesDefaults,
        bool needsMigration,
        CampaignSnapshot campaign,
        string detail)
    {
        Problem = problem;
        DiscoveredIds = discoveredIds ??
            Array.Empty<string>();
        MusicVolume = musicVolume;
        EffectsVolume = effectsVolume;
        AudioUsesDefaults = audioUsesDefaults;
        NeedsMigration = needsMigration;
        Campaign = campaign;
        Detail = detail ?? string.Empty;
    }

    public static ProgressParseResult Valid(
        IReadOnlyList<string> discoveredIds,
        float musicVolume,
        float effectsVolume,
        bool audioUsesDefaults,
        bool needsMigration = false,
        CampaignSnapshot campaign = default)
    {
        return new ProgressParseResult(
            ProgressFileProblem.None,
            discoveredIds,
            musicVolume,
            effectsVolume,
            audioUsesDefaults,
            needsMigration,
            campaign,
            string.Empty);
    }

    public static ProgressParseResult Failed(
        ProgressFileProblem problem,
        string detail)
    {
        return new ProgressParseResult(
            problem,
            Array.Empty<string>(),
            PlayerProgressCodec.DefaultMusicVolume,
            PlayerProgressCodec.DefaultEffectsVolume,
            true,
            false,
            CampaignSnapshot.Empty,
            detail);
    }
}

public readonly struct ProgressLoadResult
{
    public ProgressLoadStatus Status { get; }
    public ProgressFileProblem MainProblem { get; }
    public IReadOnlyList<string> DiscoveredIds { get; }
    public bool WritesBlocked { get; }
    public bool RequiresSave { get; }
    public float MusicVolume { get; }
    public float EffectsVolume { get; }
    public bool AudioUsesDefaults { get; }
    public bool NeedsMigration { get; }
    public CampaignSnapshot Campaign { get; }
    public string Detail { get; }

    public ProgressLoadResult(
        ProgressLoadStatus status,
        ProgressFileProblem mainProblem,
        IReadOnlyList<string> discoveredIds,
        bool writesBlocked,
        bool requiresSave,
        string detail,
        float musicVolume,
        float effectsVolume,
        bool audioUsesDefaults,
        bool needsMigration = false,
        CampaignSnapshot campaign = default)
    {
        Status = status;
        MainProblem = mainProblem;
        DiscoveredIds = discoveredIds ??
            Array.Empty<string>();
        WritesBlocked = writesBlocked;
        RequiresSave = requiresSave;
        Detail = detail ?? string.Empty;
        MusicVolume = musicVolume;
        EffectsVolume = effectsVolume;
        AudioUsesDefaults = audioUsesDefaults;
        NeedsMigration = needsMigration;
        Campaign = campaign;
    }
}

public readonly struct ProgressSaveResult
{
    public ProgressSaveStatus Status { get; }
    public string Detail { get; }

    public ProgressSaveResult(
        ProgressSaveStatus status,
        string detail)
    {
        Status = status;
        Detail = detail ?? string.Empty;
    }
}
