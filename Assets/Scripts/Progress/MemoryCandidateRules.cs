using System;
using System.Collections.Generic;

public static class MemoryCandidateRules
{
    public static List<string> Undiscovered(
        IReadOnlyList<string> enabledIds,
        Func<string, bool> isDiscovered)
    {
        List<string> result = new();

        if (enabledIds == null)
        {
            return result;
        }

        HashSet<string> seen = new();

        for (int index = 0; index < enabledIds.Count; index++)
        {
            string id = enabledIds[index];

            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
            {
                continue;
            }

            if (isDiscovered != null && isDiscovered(id))
            {
                continue;
            }

            result.Add(id);
        }

        return result;
    }

    public static List<string> Eligible(
        IReadOnlyList<string> enabledIds,
        Func<string, bool> isDiscovered,
        int capturesThisRun,
        int maxPerRun)
    {
        if (maxPerRun <= 0 || capturesThisRun >= maxPerRun)
        {
            return new List<string>();
        }

        return Undiscovered(enabledIds, isDiscovered);
    }

    public static bool CanSchedule(
        bool sessionActive,
        bool combatStarted,
        bool alreadyScheduledThisWave,
        bool capturedThisWave,
        bool runFull,
        bool matchOver)
    {
        return sessionActive &&
               combatStarted &&
               !alreadyScheduledThisWave &&
               !capturedThisWave &&
               !runFull &&
               !matchOver;
    }
}
