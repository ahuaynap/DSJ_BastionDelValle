using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CollectibleSpawner : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private EducationalCollectible collectiblePrefab;

    [SerializeField]
    private WanderingArea2D wanderingArea;

    [SerializeField]
    private GameManager gameManager;

    [SerializeField]
    private PauseManager pauseManager;

    [SerializeField]
    private WaveManager waveManager;

    [SerializeField]
    private EducationalCollectionManager collectionManager;

    [SerializeField]
    private Transform collectibleContainer;

    [Header("Available Content")]
    [SerializeField]
    private CollectibleData[] availableCollectibles;

    [SerializeField, Min(0)]
    private int maxDiscoveriesPerRun = 2;

    [Header("Spawn Timing")]
    [SerializeField, Min(0f)]
    private float minimumSpawnDelay = 1f;

    [SerializeField, Min(0f)]
    private float maximumSpawnDelay = 2f;

    [Header("Collectible")]
    [SerializeField, Min(0.1f)]
    private float collectibleLifetime = 12f;

    private readonly HashSet<int> scheduledWaves = new();
    private EducationalCollectible activeCollectible;
    private Coroutine spawnRoutine;
    private bool capturedThisWave;
    private bool missingCollectionReported;

    public bool HasActiveCollectible =>
        activeCollectible != null;

    public int MaxDiscoveriesPerRun =>
        Mathf.Max(0, maxDiscoveriesPerRun);

    public event Action<CollectibleData> OnCollectibleCaptured;

    public bool HasUndiscoveredMemories()
    {
        if (collectionManager == null)
        {
            return false;
        }

        return MemoryCandidateRules.Undiscovered(
            EnabledIds(),
            collectionManager.HasDiscovered
        ).Count > 0;
    }

    private void Start()
    {
        if (!ValidateDependencies())
        {
            return;
        }

        waveManager.OnWaveStateChanged += HandleWaveState;
        gameManager.OnGameStateChanged += HandleGameStateChanged;

        if (waveManager.CurrentWaveState == WaveState.Spawning)
        {
            HandleWaveState(WaveState.Spawning);
        }
    }

    private void HandleWaveState(WaveState state)
    {
        if (state == WaveState.Spawning)
        {
            capturedThisWave = false;
            RequestSpawnForWave(waveManager.CurrentWave);
            return;
        }

        if (state == WaveState.Completed ||
            state == WaveState.Waiting ||
            state == WaveState.SpawnError)
        {
            CancelPendingSpawn();
            WithdrawActive();
        }
    }

    private void RequestSpawnForWave(int waveNumber)
    {
        bool matchOver =
            gameManager == null || !gameManager.IsPlaying;
        bool runFull =
            collectionManager != null &&
            collectionManager.RunDiscoveryCount >= MaxDiscoveriesPerRun;

        if (!MemoryCandidateRules.CanSchedule(
                !matchOver,
                true,
                scheduledWaves.Contains(waveNumber),
                capturedThisWave,
                runFull,
                matchOver))
        {
            return;
        }

        if (MemoryCandidateRules.Eligible(
                EnabledIds(),
                IsDiscovered,
                CapturesThisRun(),
                MaxDiscoveriesPerRun).Count == 0)
        {
            scheduledWaves.Add(waveNumber);
            return;
        }

        if (activeCollectible != null || spawnRoutine != null)
        {
            return;
        }

        scheduledWaves.Add(waveNumber);
        spawnRoutine = StartCoroutine(SpawnAfterDelay(waveNumber));
    }

    private IEnumerator SpawnAfterDelay(int waveNumber)
    {
        float delay = UnityEngine.Random.Range(
            minimumSpawnDelay,
            maximumSpawnDelay
        );

        yield return new WaitForSeconds(delay);
        spawnRoutine = null;

        if (waveManager == null ||
            waveManager.CurrentWave != waveNumber ||
            waveManager.CurrentWaveState == WaveState.Waiting ||
            waveManager.CurrentWaveState == WaveState.Completed ||
            waveManager.CurrentWaveState == WaveState.SpawnError)
        {
            yield break;
        }

        if (!gameManager.IsPlaying ||
            capturedThisWave ||
            activeCollectible != null)
        {
            yield break;
        }

        SpawnCollectible();
    }

    private void SpawnCollectible()
    {
        CollectibleData selectedData = SelectEligibleCollectible();

        if (selectedData == null)
        {
            return;
        }

        Vector2 spawnPosition = wanderingArea.GetRandomPoint();
        EducationalCollectible collectible = Instantiate(
            collectiblePrefab,
            spawnPosition,
            Quaternion.identity,
            collectibleContainer
        );

        if (collectible == null)
        {
            return;
        }

        WanderingMovement2D movement =
            collectible.GetComponent<WanderingMovement2D>();

        if (movement == null)
        {
            Debug.LogError(
                $"{collectible.gameObject.name} requires " +
                "WanderingMovement2D.",
                collectible
            );
            Destroy(collectible.gameObject);
            return;
        }

        activeCollectible = collectible;
        collectible.OnCaptured += HandleCollectibleCaptured;
        collectible.OnExpired += HandleCollectibleExpired;
        movement.Initialize(wanderingArea, gameManager);
        collectible.Initialize(
            gameManager,
            pauseManager,
            selectedData,
            collectibleLifetime
        );
    }

    private CollectibleData SelectEligibleCollectible()
    {
        List<CollectibleData> pool = BuildPool();

        if (pool.Count == 0)
        {
            return null;
        }

        CollectibleData chosen =
            pool[UnityEngine.Random.Range(0, pool.Count)];
        List<CollectibleData> fresh = BuildPool();

        for (int index = 0; index < fresh.Count; index++)
        {
            if (fresh[index].CollectibleId == chosen.CollectibleId)
            {
                return fresh[index];
            }
        }

        if (fresh.Count == 0)
        {
            return null;
        }

        return fresh[UnityEngine.Random.Range(0, fresh.Count)];
    }

    private List<CollectibleData> BuildPool()
    {
        List<string> ids = MemoryCandidateRules.Eligible(
            EnabledIds(),
            IsDiscovered,
            CapturesThisRun(),
            MaxDiscoveriesPerRun
        );
        List<CollectibleData> pool = new();

        for (int index = 0; index < ids.Count; index++)
        {
            CollectibleData data = FindData(ids[index]);

            if (data != null)
            {
                pool.Add(data);
            }
        }

        return pool;
    }

    private CollectibleData FindData(string id)
    {
        if (availableCollectibles == null)
        {
            return null;
        }

        for (int index = 0; index < availableCollectibles.Length; index++)
        {
            CollectibleData data = availableCollectibles[index];

            if (data != null && data.CollectibleId == id)
            {
                return data;
            }
        }

        return null;
    }

    private List<string> EnabledIds()
    {
        List<string> ids = new();

        if (availableCollectibles == null)
        {
            return ids;
        }

        for (int index = 0; index < availableCollectibles.Length; index++)
        {
            CollectibleData data = availableCollectibles[index];
            ids.Add(data == null ? null : data.CollectibleId);
        }

        return ids;
    }

    private bool IsDiscovered(string id)
    {
        return collectionManager != null &&
               collectionManager.HasDiscovered(id);
    }

    private int CapturesThisRun()
    {
        return collectionManager == null
            ? 0
            : collectionManager.RunDiscoveryCount;
    }

    private void HandleCollectibleCaptured(
        EducationalCollectible collectible,
        CollectibleData data)
    {
        capturedThisWave = true;
        OnCollectibleCaptured?.Invoke(data);
        ReleaseCollectible(collectible);
    }

    private void HandleCollectibleExpired(
        EducationalCollectible collectible)
    {
        ReleaseCollectible(collectible);
    }

    private void ReleaseCollectible(
        EducationalCollectible collectible)
    {
        if (collectible == null)
        {
            return;
        }

        collectible.OnCaptured -= HandleCollectibleCaptured;
        collectible.OnExpired -= HandleCollectibleExpired;

        if (activeCollectible == collectible)
        {
            activeCollectible = null;
        }
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.Playing)
        {
            return;
        }

        CancelPendingSpawn();
        WithdrawActive();
    }

    private void CancelPendingSpawn()
    {
        if (spawnRoutine == null)
        {
            return;
        }

        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    private void WithdrawActive()
    {
        if (activeCollectible == null)
        {
            return;
        }

        EducationalCollectible collectible = activeCollectible;
        ReleaseCollectible(collectible);
        collectible.Cancel();
    }

    private bool ValidateDependencies()
    {
        bool isValid = true;

        if (collectiblePrefab == null)
        {
            Debug.LogError(
                "CollectibleSpawner has no prefab.",
                this
            );
            isValid = false;
        }

        if (wanderingArea == null)
        {
            Debug.LogError(
                "CollectibleSpawner has no wandering area.",
                this
            );
            isValid = false;
        }

        if (gameManager == null)
        {
            Debug.LogError(
                "CollectibleSpawner has no GameManager.",
                this
            );
            isValid = false;
        }

        if (pauseManager == null)
        {
            Debug.LogError(
                "CollectibleSpawner has no PauseManager. " +
                "Collectible capture is disabled.",
                this
            );
            isValid = false;
        }

        if (waveManager == null)
        {
            Debug.LogError(
                "CollectibleSpawner has no WaveManager.",
                this
            );
            isValid = false;
        }

        if (collectionManager == null && !missingCollectionReported)
        {
            missingCollectionReported = true;
            Debug.LogError(
                "CollectibleSpawner has no collection. " +
                "Memories will not appear.",
                this
            );
            isValid = false;
        }

        if (availableCollectibles == null ||
            availableCollectibles.Length == 0)
        {
            Debug.LogError(
                "CollectibleSpawner has no " +
                "CollectibleData configured.",
                this
            );
            isValid = false;
        }

        return isValid;
    }

    private void OnDestroy()
    {
        CancelPendingSpawn();

        if (waveManager != null)
        {
            waveManager.OnWaveStateChanged -= HandleWaveState;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        if (activeCollectible != null)
        {
            activeCollectible.OnCaptured -= HandleCollectibleCaptured;
            activeCollectible.OnExpired -= HandleCollectibleExpired;
        }
    }

    private void OnValidate()
    {
        minimumSpawnDelay = Mathf.Max(0f, minimumSpawnDelay);
        maximumSpawnDelay = Mathf.Max(
            minimumSpawnDelay,
            maximumSpawnDelay
        );
        collectibleLifetime = Mathf.Max(0.1f, collectibleLifetime);
        maxDiscoveriesPerRun = Mathf.Max(0, maxDiscoveriesPerRun);
    }
}
