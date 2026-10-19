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
    private Transform collectibleContainer;

    [Header("Available Content")]
    [SerializeField]
    private CollectibleData[] availableCollectibles;

    [Header("Spawn Timing")]
    [SerializeField, Min(0f)]
    private float minimumSpawnDelay = 2f;

    [SerializeField, Min(0f)]
    private float maximumSpawnDelay = 5f;

    [Header("Collectible")]
    [SerializeField, Min(0.1f)]
    private float collectibleLifetime = 12f;

    private readonly HashSet<int>
        scheduledWaves = new();

    private EducationalCollectible
        activeCollectible;

    private Coroutine spawnRoutine;
    private int pendingWave = -1;

    public bool HasActiveCollectible =>
        activeCollectible != null;

    public event Action<CollectibleData>
        OnCollectibleCaptured;

    private void Start()
    {
        if (!ValidateDependencies())
        {
            return;
        }

        waveManager.OnWaveChanged +=
            HandleWaveChanged;

        gameManager.OnGameStateChanged +=
            HandleGameStateChanged;

        StartCoroutine(
            RegisterInitialWaveAfterFrame()
        );
    }

    private IEnumerator
        RegisterInitialWaveAfterFrame()
    {
        yield return null;

        if (gameManager.IsPlaying)
        {
            RequestSpawnForWave(
                waveManager.CurrentWave
            );
        }
    }

    private void HandleWaveChanged(
        int currentWave,
        int totalWaves)
    {
        RequestSpawnForWave(currentWave);
    }

    private void RequestSpawnForWave(
        int waveNumber)
    {
        if (!gameManager.IsPlaying ||
            waveNumber <= 0)
        {
            return;
        }

        if (scheduledWaves.Contains(waveNumber))
        {
            return;
        }

        scheduledWaves.Add(waveNumber);

        if (activeCollectible != null ||
            spawnRoutine != null)
        {
            pendingWave = waveNumber;
            return;
        }

        StartSpawnRoutine(waveNumber);
    }

    private void StartSpawnRoutine(
        int waveNumber)
    {
        spawnRoutine = StartCoroutine(
            SpawnAfterDelay(waveNumber)
        );
    }

    private IEnumerator SpawnAfterDelay(
        int waveNumber)
    {
        float delay =
            UnityEngine.Random.Range(
                minimumSpawnDelay,
                maximumSpawnDelay
            );

        yield return new WaitForSeconds(delay);

        spawnRoutine = null;

        if (!gameManager.IsPlaying)
        {
            yield break;
        }

        if (activeCollectible != null)
        {
            pendingWave = waveNumber;
            yield break;
        }

        SpawnCollectible();
    }

    private void SpawnCollectible()
    {
        CollectibleData selectedData =
            SelectRandomCollectible();

        if (selectedData == null)
        {
            Debug.LogError(
                "CollectibleSpawner could not select " +
                "valid CollectibleData.",
                this
            );

            return;
        }

        Vector2 spawnPosition =
            wanderingArea.GetRandomPoint();

        EducationalCollectible collectible =
            Instantiate(
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
            collectible.GetComponent<
                WanderingMovement2D
            >();

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

        collectible.OnCaptured +=
            HandleCollectibleCaptured;

        collectible.OnExpired +=
            HandleCollectibleExpired;

        movement.Initialize(
            wanderingArea,
            gameManager
        );

        collectible.Initialize(
            gameManager,
            pauseManager,
            selectedData,
            collectibleLifetime
        );

        Debug.Log(
            $"Spawned collectible: " +
            $"{selectedData.DisplayName}."
        );
    }

    private CollectibleData
        SelectRandomCollectible()
    {
        if (availableCollectibles == null ||
            availableCollectibles.Length == 0)
        {
            return null;
        }

        int startIndex =
            UnityEngine.Random.Range(
                0,
                availableCollectibles.Length
            );

        for (
            int offset = 0;
            offset < availableCollectibles.Length;
            offset++)
        {
            int index =
                (startIndex + offset) %
                availableCollectibles.Length;

            if (availableCollectibles[index] != null)
            {
                return availableCollectibles[index];
            }
        }

        return null;
    }

    private void HandleCollectibleCaptured(
        EducationalCollectible collectible,
        CollectibleData data)
    {
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

        collectible.OnCaptured -=
            HandleCollectibleCaptured;

        collectible.OnExpired -=
            HandleCollectibleExpired;

        if (activeCollectible == collectible)
        {
            activeCollectible = null;
        }

        TrySchedulePendingWave();
    }

    private void TrySchedulePendingWave()
    {
        if (pendingWave <= 0)
        {
            return;
        }

        if (!gameManager.IsPlaying)
        {
            pendingWave = -1;
            return;
        }

        int waveToSchedule = pendingWave;

        pendingWave = -1;

        StartSpawnRoutine(waveToSchedule);
    }

    private void HandleGameStateChanged(
        GameState state)
    {
        if (state == GameState.Playing)
        {
            return;
        }

        CancelPendingSpawn();
        CancelActiveCollectible();
    }

    private void CancelPendingSpawn()
    {
        if (spawnRoutine == null)
        {
            return;
        }

        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
        pendingWave = -1;
    }

    private void CancelActiveCollectible()
    {
        if (activeCollectible == null)
        {
            return;
        }

        activeCollectible.OnCaptured -=
            HandleCollectibleCaptured;

        activeCollectible.OnExpired -=
            HandleCollectibleExpired;

        activeCollectible.Cancel();
        activeCollectible = null;
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
        if (waveManager != null)
        {
            waveManager.OnWaveChanged -=
                HandleWaveChanged;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -=
                HandleGameStateChanged;
        }

        if (activeCollectible != null)
        {
            activeCollectible.OnCaptured -=
                HandleCollectibleCaptured;

            activeCollectible.OnExpired -=
                HandleCollectibleExpired;
        }
    }

    private void OnValidate()
    {
        minimumSpawnDelay =
            Mathf.Max(0f, minimumSpawnDelay);

        maximumSpawnDelay =
            Mathf.Max(
                minimumSpawnDelay,
                maximumSpawnDelay
            );

        collectibleLifetime =
            Mathf.Max(0.1f, collectibleLifetime);
    }
}