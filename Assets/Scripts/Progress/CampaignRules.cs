using System;
using System.Collections.Generic;

public readonly struct CampaignSnapshot
{
    public bool Started { get; }
    public IReadOnlyList<string> CompletedMaps { get; }
    public IReadOnlyList<string> ClearedTutorials { get; }
    public IReadOnlyList<string> Evaluations { get; }

    public CampaignSnapshot(
        bool started,
        IReadOnlyList<string> completedMaps,
        IReadOnlyList<string> clearedTutorials,
        IReadOnlyList<string> evaluations)
    {
        Started = started;
        CompletedMaps = completedMaps ?? Array.Empty<string>();
        ClearedTutorials = clearedTutorials ?? Array.Empty<string>();
        Evaluations = evaluations ?? Array.Empty<string>();
    }

    public static CampaignSnapshot Empty =>
        new CampaignSnapshot(false, null, null, null);
}

public static class CampaignRules
{
    public const string MenuScene = "MainMenu";
    public const string FirstMap = "Level_01";
    public const string SecondMap = "Level_02";

    public const string ContinueRule =
        "Continuar solo si campaignStarted es verdadero. " +
        "El destino es Level_01 mientras no esté en completedMaps, " +
        "Level_02 cuando Level_01 ya está completado, y otra vez " +
        "Level_02 cuando el mapa 2 también lo está. No existe mapa 3. " +
        "Los descubrimientos del códice no marcan mapas completados. " +
        "Una batalla a medias no se guarda ni se restaura.";

    public static bool IsPlayableMap(string sceneName)
    {
        return sceneName == FirstMap || sceneName == SecondMap;
    }

    public static bool NeedsConfirmation(
        bool started,
        int completedMaps,
        int discoveries,
        int tutorials,
        int evaluations)
    {
        return started ||
            completedMaps > 0 ||
            discoveries > 0 ||
            tutorials > 0 ||
            evaluations > 0;
    }

    public static bool CanContinue(bool started)
    {
        return started;
    }

    public static bool SecondMapCleared(IReadOnlyList<string> completedMaps)
    {
        return Contains(completedMaps, SecondMap);
    }

    public static string Destination(
        bool started,
        IReadOnlyList<string> completedMaps)
    {
        if (!started)
        {
            return null;
        }

        if (Contains(completedMaps, SecondMap) ||
            Contains(completedMaps, FirstMap))
        {
            return SecondMap;
        }

        return FirstMap;
    }

    public static CampaignSnapshot RecordVictory(
        CampaignSnapshot current,
        string sceneName,
        bool victoryConfirmed)
    {
        if (!victoryConfirmed || !IsPlayableMap(sceneName))
        {
            return current;
        }

        if (Contains(current.CompletedMaps, sceneName))
        {
            return new CampaignSnapshot(
                true,
                current.CompletedMaps,
                current.ClearedTutorials,
                current.Evaluations);
        }

        var maps = new List<string>();

        if (current.CompletedMaps != null)
        {
            for (int i = 0; i < current.CompletedMaps.Count; i++)
            {
                maps.Add(current.CompletedMaps[i]);
            }
        }

        maps.Add(sceneName);
        return new CampaignSnapshot(
            true,
            maps,
            current.ClearedTutorials,
            current.Evaluations);
    }

    public static string NextMap(string sceneName)
    {
        return sceneName == FirstMap ? SecondMap : null;
    }

    public static string ContinueLabel(bool secondCleared)
    {
        return secondCleared
            ? "Mapa 2 completado. Puedes rejugarlo."
            : "La campaña sigue en el mapa pendiente.";
    }

    private static bool Contains(IReadOnlyList<string> values, string expected)
    {
        if (values == null)
        {
            return false;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == expected)
            {
                return true;
            }
        }

        return false;
    }
}
