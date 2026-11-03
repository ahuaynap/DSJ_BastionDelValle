public enum MatchView
{
    Preparation = 0,
    Spawning = 1,
    Combat = 2,
    Result = 3,
    SpawnFailure = 4
}

public static class MatchPhase
{
    public static MatchView View(bool sessionActive, WaveState state)
    {
        if (!sessionActive)
        {
            return MatchView.Result;
        }

        if (state == WaveState.SpawnError)
        {
            return MatchView.SpawnFailure;
        }

        if (state == WaveState.Waiting)
        {
            return MatchView.Preparation;
        }

        if (state == WaveState.Spawning)
        {
            return MatchView.Spawning;
        }

        return MatchView.Combat;
    }

    public static bool AllowStart(
        bool sessionActive,
        WaveState state,
        bool paused,
        bool spawnRunning)
    {
        return IntroWaveRules.CanStartWave(
            sessionActive,
            state == WaveState.Waiting,
            paused,
            spawnRunning);
    }

    public static bool DeclareVictory(
        bool sessionActive,
        bool heartDestroyed,
        int waveIndex,
        int waveCount,
        bool allSpawned,
        bool noActiveEnemies,
        bool generationFailed)
    {
        if (generationFailed)
        {
            return false;
        }

        return IntroWaveRules.IsVictory(
            sessionActive,
            heartDestroyed,
            waveIndex,
            waveCount,
            allSpawned,
            noActiveEnemies);
    }
}
