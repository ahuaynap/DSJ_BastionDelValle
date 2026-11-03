using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private EnemySpawner[] accessPoints;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private EconomyManager economyManager;
    [SerializeField] private PauseManager pauseManager;

    [Header("Access Seed")]
    [SerializeField] private bool useDiagnosticSeed;
    [SerializeField] private int diagnosticSeed;

    [Header("Waves")]
    [SerializeField] private WaveConfig[] waves;
    [SerializeField] private MapLesson lesson;
    [SerializeField] private HabitatSession habitat;

    [Header("Timing")]
    [SerializeField] private float intermissionDuration = 5f;

    private int currentWaveIndex;
    private int clearedWaves;

    private int activeEnemies;
    private int spawnedEnemies;

    private int killedEnemies;
    private int leakedEnemies;

    private WaveState currentWaveState = WaveState.Waiting;

    private Coroutine spawnRoutine;
    private bool missingPauseReported;
    private string spawnFailure;
    private int recordedSeed = int.MinValue;
    private System.Random accessRandom;
    private bool reportedSeed;
    private int nextGroupId = 1;
    private int lastClearReward;
    private int lastBaseReward;
    private int lastSoilBonus;
    private readonly HashSet<int> rewardedWaves = new();

    public int CurrentWave => currentWaveIndex + 1;
    public int TotalWaves => waves == null ? 0 : waves.Length;
    public int ClearedWaves => clearedWaves;

    public int ActiveEnemies => activeEnemies;
    public int SpawnedEnemies => spawnedEnemies;
    public int LastClearReward => lastClearReward;
    public int LastBaseReward => lastBaseReward;
    public int LastSoilBonus => lastSoilBonus;

    public int PendingEnemies
    {
        get
        {
            if (currentWaveState == WaveState.Waiting &&
                spawnedEnemies == 0 &&
                activeEnemies == 0)
            {
                return 0;
            }

            return Mathf.Max(0, TotalEnemies - spawnedEnemies);
        }
    }

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

    public int RemainingEnemies
    {
        get
        {
            if (currentWaveState == WaveState.Waiting &&
                spawnedEnemies == 0 &&
                activeEnemies == 0)
            {
                return 0;
            }

            return Mathf.Max(
                0,
                (TotalEnemies - spawnedEnemies) + activeEnemies
            );
        }
    }

    public WaveState CurrentWaveState =>
        currentWaveState;

    public bool CanRequestStart =>
        MatchPhase.AllowStart(
            gameManager != null && gameManager.IsPlaying,
            currentWaveState,
            pauseManager == null || pauseManager.IsPaused,
            spawnRoutine != null
        );

    public string SpawnFailure => spawnFailure;

    public bool PreparingAirThreat
    {
        get
        {
            return currentWaveState == WaveState.Waiting &&
                WaveContainsAir(currentWaveIndex);
        }
    }

    public bool TryPreparationBrief(
        out string threat,
        out int individuals,
        out string advice)
    {
        threat = string.Empty;
        individuals = 0;
        advice = string.Empty;

        if (lesson == null ||
            currentWaveState != WaveState.Waiting ||
            !HasValidCurrentWave())
        {
            return false;
        }

        threat = lesson.ThreatAt(currentWaveIndex);
        advice = lesson.AdviceAt(currentWaveIndex);
        individuals = waves[currentWaveIndex].EnemyCount;
        return !string.IsNullOrEmpty(threat);
    }
    public int RecordedAccessSeed => recordedSeed;

    public event Action<int> OnRemainingEnemiesChanged;

    public event Action<int, int> OnWaveChanged;

    public event Action<WaveState> OnWaveStateChanged;

    public event Action<EnemyFinishReason> OnEnemyResolved;

    private void Start()
    {
        if (gameManager == null)
        {
            Debug.LogError(
                "WaveManager requires a GameManager. " +
                "Waves will not start.",
                this
            );

            return;
        }

        gameManager.OnGameStateChanged +=
            HandleGameStateChanged;

        if (pauseManager == null && !missingPauseReported)
        {
            missingPauseReported = true;
            Debug.LogError(
                "WaveManager requires a PauseManager. " +
                "The player cannot start a wave.",
                this
            );
        }

        if (waves == null || waves.Length == 0)
        {
            Debug.LogError(
                "WaveManager has no waves configured.",
                this
            );

            return;
        }

        currentWaveIndex = 0;
        SetWaveState(WaveState.Waiting);
        OnWaveChanged?.Invoke(CurrentWave, TotalWaves);
        OnRemainingEnemiesChanged?.Invoke(RemainingEnemies);
    }

    public void RequestStart()
    {
        if (!CanRequestStart)
        {
            return;
        }

        StartCurrentWave();
    }

    private void OnDestroy()
    {
        CancelWaveRoutines();

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -=
                HandleGameStateChanged;
        }
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.Playing)
        {
            return;
        }

        CancelWaveRoutines();
        OnWaveStateChanged?.Invoke(currentWaveState);
    }

    private void CancelWaveRoutines()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private void StartCurrentWave()
    {
        if (!CanRequestStart || !HasValidCurrentWave())
        {
            return;
        }

        CancelWaveRoutines();
        spawnRoutine = StartCoroutine(
            SpawnWave(waves[currentWaveIndex])
        );
    }

    private IEnumerator SpawnWave(WaveConfig wave)
    {
        spawnedEnemies = 0;
        activeEnemies = 0;
        nextGroupId = 1;
        lastClearReward = 0;
        lastBaseReward = 0;
        lastSoilBonus = 0;
        SetWaveState(WaveState.Spawning);
        OnWaveChanged?.Invoke(CurrentWave, TotalWaves);
        OnRemainingEnemiesChanged?.Invoke(RemainingEnemies);

        for (int entry = 0; entry < wave.EntryCount; entry++)
        {
            int party = wave.PartyAt(entry);
            int groupId = party > 1 ? nextGroupId++ : 0;
            EnemySpawner source = ChooseAccess(wave.PrefabAt(entry));

            if (source == null)
            {
                FailGeneration(
                    string.IsNullOrEmpty(spawnFailure)
                        ? "Ningún acceso tiene una ruta hasta el corazón."
                        : spawnFailure
                );
                yield break;
            }

            for (int member = 0; member < party; member++)
            {
                if (!gameManager.IsPlaying)
                {
                    spawnRoutine = null;
                    yield break;
                }

                string block = source.RouteBlock(wave.PrefabAt(entry));

                if (block != null)
                {
                    FailGeneration(block);
                    yield break;
                }

                EnemyMovement enemy = source.SpawnEnemy(
                    wave.PrefabAt(entry),
                    groupId
                );

                if (!gameManager.IsPlaying)
                {
                    spawnRoutine = null;
                    yield break;
                }

                if (enemy == null || !enemy.IsReady)
                {
                    if (enemy != null)
                    {
                        Destroy(enemy.gameObject);
                    }

                    FailGeneration(
                        string.IsNullOrEmpty(source.LastFailure)
                            ? "Un acceso no pudo crear al enemigo."
                            : source.LastFailure
                    );
                    yield break;
                }

                activeEnemies++;
                spawnedEnemies++;
                enemy.OnEnemyFinished += HandleEnemyFinished;

                if (member < party - 1)
                {
                    yield return new WaitForSeconds(wave.PartyInterval);
                }
            }

            yield return new WaitForSeconds(wave.SpawnInterval);
        }

        spawnRoutine = null;

        if (currentWaveState == WaveState.SpawnError)
        {
            yield break;
        }

        SetWaveState(WaveState.Active);
        CheckWaveCompletion();
    }

    private EnemySpawner ChooseAccess(EnemyMovement prefab)
    {
        if (accessPoints == null || accessPoints.Length == 0)
        {
            return enemySpawner;
        }

        var connected = new System.Collections.Generic.List<EnemySpawner>();
        string blocked = null;

        for (int i = 0; i < accessPoints.Length; i++)
        {
            EnemySpawner candidate = accessPoints[i];

            if (candidate == null)
            {
                continue;
            }

            string block = candidate.RouteBlock(prefab);

            if (block == null)
            {
                connected.Add(candidate);
            }
            else
            {
                blocked = block;
            }
        }

        if (connected.Count == 0)
        {
            spawnFailure = blocked;
            return null;
        }

        if (connected.Count == 1)
        {
            return connected[0];
        }

        int index = AccessDraw.Next(AccessRandom(), connected.Count);
        return index < 0 ? null : connected[index];
    }

    private System.Random AccessRandom()
    {
        if (accessRandom != null)
        {
            return accessRandom;
        }

        int seed = useDiagnosticSeed
            ? diagnosticSeed
            : System.Environment.TickCount;
        recordedSeed = seed;
        accessRandom = new System.Random(seed);

        if (!reportedSeed)
        {
            reportedSeed = true;
            Debug.Log(
                "Semilla de accesos de la confluencia: " + seed,
                this
            );
        }

        return accessRandom;
    }

    private void FailGeneration(string reason)
    {
        spawnRoutine = null;
        spawnFailure = reason;
        Debug.LogError(reason, this);
        SetWaveState(WaveState.SpawnError);
    }

    private void HandleEnemyFinished(
        EnemyMovement enemy,
        EnemyFinishReason reason)
    {
        enemy.OnEnemyFinished -= HandleEnemyFinished;
        activeEnemies--;

        bool combatIsActive =
            gameManager != null &&
            gameManager.IsPlaying;

        switch (reason)
        {
            case EnemyFinishReason.Killed:
                killedEnemies++;

                if (combatIsActive &&
                    enemy.Health != null &&
                    economyManager != null)
                {
                    economyManager.AddResources(enemy.Health.Reward);
                }

                break;

            case EnemyFinishReason.ReachedGoal:
                leakedEnemies++;
                break;
        }

        OnEnemyResolved?.Invoke(reason);
        OnRemainingEnemiesChanged?.Invoke(RemainingEnemies);

        if (!combatIsActive)
        {
            return;
        }

        CheckWaveCompletion();
    }

    private void CheckWaveCompletion()
    {
        if (gameManager == null ||
            !gameManager.IsPlaying ||
            currentWaveState == WaveState.SpawnError)
        {
            return;
        }

        bool allSpawned = spawnedEnemies >= TotalEnemies;
        bool noActiveEnemies = activeEnemies <= 0;

        if (!allSpawned || !noActiveEnemies || spawnRoutine != null)
        {
            return;
        }

        CompleteCurrentWave();
    }

    private void CompleteCurrentWave()
    {
        SetWaveState(WaveState.Completed);
        clearedWaves++;

        if (habitat != null &&
            gameManager != null &&
            gameManager.IsPlaying)
        {
            habitat.RecoverClearedWave(currentWaveIndex);
        }

        GrantClearReward();

        bool victory = MatchPhase.DeclareVictory(
            gameManager.IsPlaying,
            false,
            currentWaveIndex,
            waves.Length,
            spawnedEnemies >= TotalEnemies,
            activeEnemies <= 0,
            currentWaveState == WaveState.SpawnError
        );

        if (victory)
        {
            gameManager.SetVictory();
            return;
        }

        if (currentWaveIndex >= waves.Length - 1)
        {
            return;
        }

        currentWaveIndex++;
        spawnedEnemies = 0;
        activeEnemies = 0;
        SetWaveState(WaveState.Waiting);
        OnWaveChanged?.Invoke(CurrentWave, TotalWaves);
        OnRemainingEnemiesChanged?.Invoke(RemainingEnemies);
    }

    private void GrantClearReward()
    {
        int configured = lesson == null ? 0 : lesson.RewardAt(currentWaveIndex);
        bool finalWave = waves == null || currentWaveIndex >= waves.Length - 1;
        int baseReward = LessonRules.Pay(
            LessonRules.Grant(
                currentWaveIndex,
                waves == null ? 0 : waves.Length,
                configured,
                true,
                gameManager != null && gameManager.IsPlaying,
                false),
            rewardedWaves.Contains(currentWaveIndex));
        int soilBonus = baseReward > 0 && habitat != null
            ? habitat.SoilExtra(baseReward, finalWave)
            : 0;
        int grant = baseReward + soilBonus;
        lastBaseReward = baseReward;
        lastSoilBonus = soilBonus;

        if (grant <= 0 || economyManager == null)
        {
            lastClearReward = 0;
            lastBaseReward = 0;
            lastSoilBonus = 0;
            return;
        }

        rewardedWaves.Add(currentWaveIndex);

        if (soilBonus > 0)
        {
            habitat.NoteSoilPaid();
        }

        economyManager.AddResources(grant);
        lastClearReward = grant;
    }

    private void SetWaveState(WaveState newState)
    {
        currentWaveState = newState;
        OnWaveStateChanged?.Invoke(currentWaveState);
    }

    private bool WaveContainsAir(int index)
    {
        if (waves == null || index < 0 || index >= waves.Length)
        {
            return false;
        }

        WaveConfig wave = waves[index];

        for (int i = 0; i < wave.EntryCount; i++)
        {
            EnemyMovement prefab = wave.PrefabAt(i);

            if (prefab != null &&
                prefab.Data != null &&
                prefab.Data.Travel == EnemyTravel.Air)
            {
                return true;
            }
        }

        return false;
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
