using System;
using System.Collections;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private EconomyManager economyManager;

    [Header("Waves")]
    [SerializeField] private WaveConfig[] waves;

    [Header("Timing")]
    [SerializeField] private float intermissionDuration = 5f;

    private int currentWaveIndex;

    private int activeEnemies;
    private int spawnedEnemies;

    private int killedEnemies;
    private int leakedEnemies;

    private WaveState currentWaveState;

    public int CurrentWave => currentWaveIndex + 1;
    public int TotalWaves => waves.Length;

    public int ActiveEnemies => activeEnemies;
    public int SpawnedEnemies => spawnedEnemies;

    public int TotalEnemies
    {
        get
        {
            if (!HasValidCurrentWave())
            {
                return 0;
            }

            return waves[currentWaveIndex].EnemyCount;
        }
    }

    public int RemainingEnemies =>
        Mathf.Max(
            0,
            (TotalEnemies - spawnedEnemies) + activeEnemies
        );

    public WaveState CurrentWaveState =>
        currentWaveState;

    public event Action<int> OnRemainingEnemiesChanged;

    public event Action<int, int> OnWaveChanged;

    public event Action<WaveState> OnWaveStateChanged;

    private void Start()
    {
        if (waves == null || waves.Length == 0)
        {
            Debug.LogError(
                "WaveManager has no waves configured."
            );

            return;
        }

        currentWaveIndex = 0;

        StartCurrentWave();
    }

    private void StartCurrentWave()
    {
        if (!gameManager.IsPlaying)
        {
            return;
        }

        if (!HasValidCurrentWave())
        {
            return;
        }

        StopAllCoroutines();

        StartCoroutine(
            SpawnWave(waves[currentWaveIndex])
        );
    }

    private IEnumerator SpawnWave(
        WaveConfig wave)
    {
        spawnedEnemies = 0;
        activeEnemies = 0;

        SetWaveState(WaveState.Spawning);

        OnWaveChanged?.Invoke(
            CurrentWave,
            TotalWaves
        );

        OnRemainingEnemiesChanged?.Invoke(
            RemainingEnemies
        );

        for (int i = 0; i < wave.EnemyCount; i++)
        {
            if (!gameManager.IsPlaying)
            {
                yield break;
            }

            EnemyMovement enemy =
                enemySpawner.SpawnEnemy();

            if (enemy != null)
            {
                activeEnemies++;
                spawnedEnemies++;

                enemy.OnEnemyFinished +=
                    HandleEnemyFinished;
            }

            yield return new WaitForSeconds(
                wave.SpawnInterval
            );
        }

        SetWaveState(WaveState.Active);

        CheckWaveCompletion();
    }

    private void HandleEnemyFinished(
        EnemyMovement enemy,
        EnemyFinishReason reason)
    {
        enemy.OnEnemyFinished -=
            HandleEnemyFinished;

        activeEnemies--;

        switch (reason)
        {
            case EnemyFinishReason.Killed:

                killedEnemies++;

                if (enemy.Health != null &&
                    economyManager != null)
                {
                    economyManager.AddResources(
                        enemy.Health.Reward
                    );
                }

                break;

            case EnemyFinishReason.ReachedGoal:

                leakedEnemies++;

                break;
        }

        OnRemainingEnemiesChanged?.Invoke(
            RemainingEnemies
        );

        CheckWaveCompletion();
    }

    private void CheckWaveCompletion()
    {
        if (!gameManager.IsPlaying)
        {
            return;
        }

        bool allSpawned =
            spawnedEnemies >= TotalEnemies;

        bool noActiveEnemies =
            activeEnemies <= 0;

        if (!allSpawned || !noActiveEnemies)
        {
            return;
        }

        CompleteCurrentWave();
    }

    private void CompleteCurrentWave()
    {
        SetWaveState(WaveState.Completed);

        bool isLastWave =
            currentWaveIndex >= waves.Length - 1;

        if (isLastWave)
        {
            gameManager.SetVictory();

            return;
        }

        StartCoroutine(
            IntermissionRoutine()
        );
    }

    private IEnumerator IntermissionRoutine()
    {
        SetWaveState(WaveState.Intermission);

        yield return new WaitForSeconds(
            intermissionDuration
        );

        if (!gameManager.IsPlaying)
        {
            yield break;
        }

        currentWaveIndex++;

        StartCurrentWave();
    }

    private void SetWaveState(
        WaveState newState)
    {
        currentWaveState = newState;

        OnWaveStateChanged?.Invoke(
            currentWaveState
        );

        Debug.Log(
            $"WAVE STATE -> {currentWaveState}"
        );
    }

    private bool HasValidCurrentWave()
    {
        return waves != null &&
               currentWaveIndex >= 0 &&
               currentWaveIndex < waves.Length;
    }

    private void OnValidate()
    {
        if (intermissionDuration < 0f)
        {
            intermissionDuration = 0f;
        }
    }
}