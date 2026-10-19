using System;
using System.Collections.Generic;

[Serializable]
public class PlayerProgressData
{
    public int schemaVersion;
    public List<string> discoveredIds;
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
    public string Detail { get; }

    public bool IsValid =>
        Problem == ProgressFileProblem.None;

    private ProgressParseResult(
        ProgressFileProblem problem,
        IReadOnlyList<string> discoveredIds,
        string detail)
    {
        Problem = problem;
        DiscoveredIds = discoveredIds ??
            Array.Empty<string>();
        Detail = detail ?? string.Empty;
    }

    public static ProgressParseResult Valid(
        IReadOnlyList<string> discoveredIds)
    {
        return new ProgressParseResult(
            ProgressFileProblem.None,
            discoveredIds,
            string.Empty);
    }

    public static ProgressParseResult Failed(
        ProgressFileProblem problem,
        string detail)
    {
        return new ProgressParseResult(
            problem,
            Array.Empty<string>(),
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
    public string Detail { get; }

    public ProgressLoadResult(
        ProgressLoadStatus status,
        ProgressFileProblem mainProblem,
        IReadOnlyList<string> discoveredIds,
        bool writesBlocked,
        bool requiresSave,
        string detail)
    {
        Status = status;
        MainProblem = mainProblem;
        DiscoveredIds = discoveredIds ??
            Array.Empty<string>();
        WritesBlocked = writesBlocked;
        RequiresSave = requiresSave;
        Detail = detail ?? string.Empty;
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
