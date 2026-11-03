public static class IntroWaveRules
{
    public static bool CanStartWave(
        bool sessionActive,
        bool preparing,
        bool paused,
        bool spawnRunning)
    {
        return sessionActive &&
               preparing &&
               !paused &&
               !spawnRunning;
    }

    public static bool IsVictory(
        bool sessionActive,
        bool heartDestroyed,
        int waveIndex,
        int waveCount,
        bool allSpawned,
        bool noActiveEnemies)
    {
        if (!sessionActive || heartDestroyed || waveCount <= 0)
        {
            return false;
        }

        bool lastWave = waveIndex == waveCount - 1;

        return lastWave && allSpawned && noActiveEnemies;
    }
}
