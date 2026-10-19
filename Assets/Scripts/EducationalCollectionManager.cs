using System;
using System.Collections.Generic;
using UnityEngine;

public class EducationalCollectionManager :
    MonoBehaviour
{
    [SerializeField]
    private CollectibleSpawner collectibleSpawner;

    private readonly HashSet<string>
        discoveredIds = new();

    private readonly List<string>
        discoveredOrder = new();

    private DiscoveryProgressStore progressStore;
    private bool savePending;
    private bool writesBlocked;
    private bool isPersisting;
    private bool unsupportedSaveReported;

    public int DiscoveryCount =>
        discoveredIds.Count;

    public event Action<int>
        OnDiscoveryCountChanged;

    public event Action<
        CollectibleData,
        bool
    > OnDiscoveryRegistered;

    private void Awake()
    {
        LoadDiscoveries();

        if (collectibleSpawner == null)
        {
            Debug.LogError(
                "EducationalCollectionManager has no " +
                "CollectibleSpawner.",
                this
            );

            return;
        }

        collectibleSpawner.OnCollectibleCaptured +=
            HandleCollectibleCaptured;
    }

    public bool HasDiscovered(
        string collectibleId)
    {
        if (string.IsNullOrWhiteSpace(
                collectibleId))
        {
            return false;
        }

        return discoveredIds.Contains(
            collectibleId
        );
    }

    private void LoadDiscoveries()
    {
        string directory = Application.persistentDataPath;

        if (string.IsNullOrWhiteSpace(directory))
        {
            writesBlocked = true;
            Debug.LogError(
                "Discovery progress has no persistent " +
                "directory. Discoveries will not be saved.",
                this
            );

            return;
        }

        progressStore = new DiscoveryProgressStore(
            directory
        );

        ProgressLoadResult loaded = progressStore.Load();
        writesBlocked = loaded.WritesBlocked;

        foreach (string id in loaded.DiscoveredIds)
        {
            Remember(id);
        }

        Debug.Log(
            "Discovery progress file: " +
            progressStore.MainFilePath,
            this
        );

        switch (loaded.Status)
        {
            case ProgressLoadStatus.Missing:
                Debug.Log(
                    "No discovery progress file. Starting " +
                    "an empty collection.",
                    this
                );
                break;

            case ProgressLoadStatus.Loaded:
                Debug.Log(
                    $"Loaded {DiscoveryCount} educational " +
                    "discoveries.",
                    this
                );

                if (writesBlocked)
                {
                    Debug.LogWarning(
                        loaded.Detail,
                        this
                    );
                }

                break;

            case ProgressLoadStatus.RecoveredFromBackup:
                Debug.LogWarning(
                    loaded.Detail,
                    this
                );
                break;

            case ProgressLoadStatus.UnsupportedSchema:
                Debug.LogWarning(
                    loaded.Detail,
                    this
                );
                break;

            case ProgressLoadStatus.AccessFailed:
                Debug.LogError(
                    loaded.Detail,
                    this
                );
                break;

            default:
                Debug.LogError(
                    loaded.Detail,
                    this
                );
                break;
        }

        if (loaded.RequiresSave)
        {
            savePending = true;
            PersistDiscoveries();
        }
    }

    private void HandleCollectibleCaptured(
        CollectibleData data)
    {
        if (data == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(
                data.CollectibleId))
        {
            Debug.LogError(
                $"Rejected discovery '{data.DisplayName}' " +
                "because its id is blank.",
                this
            );

            return;
        }

        bool isNewDiscovery = Remember(
            data.CollectibleId
        );

        if (isNewDiscovery)
        {
            savePending = true;

            Debug.Log(
                "New discovery registered this session: " +
                $"{data.DisplayName}. " +
                $"Total: {DiscoveryCount}"
            );

            PersistDiscoveries();

            OnDiscoveryCountChanged?.Invoke(
                DiscoveryCount
            );
        }
        else
        {
            Debug.Log(
                $"Discovery already registered: " +
                $"{data.DisplayName}."
            );

            if (savePending)
            {
                PersistDiscoveries();
            }
        }

        OnDiscoveryRegistered?.Invoke(
            data,
            isNewDiscovery
        );
    }

    private bool Remember(string collectibleId)
    {
        if (string.IsNullOrWhiteSpace(collectibleId))
        {
            return false;
        }

        if (!discoveredIds.Add(collectibleId))
        {
            return false;
        }

        discoveredOrder.Add(collectibleId);
        return true;
    }

    private void PersistDiscoveries()
    {
        if (progressStore == null)
        {
            return;
        }

        if (writesBlocked || progressStore.WritesBlocked)
        {
            savePending = false;
            ReportUnsupportedSave();
            return;
        }

        if (isPersisting)
        {
            return;
        }

        isPersisting = true;

        try
        {
            ProgressSaveResult result =
                progressStore.Save(discoveredOrder);

            if (result.Status == ProgressSaveStatus.Saved)
            {
                savePending = false;
                Debug.Log(
                    $"Saved {DiscoveryCount} educational " +
                    "discoveries to " +
                    progressStore.MainFilePath
                );

                return;
            }

            if (result.Status ==
                ProgressSaveStatus.BlockedUnsupportedSchema)
            {
                writesBlocked = true;
                savePending = false;
                ReportUnsupportedSave();
                return;
            }

            savePending = true;
            Debug.LogError(
                "Could not persist educational discoveries. " +
                "They remain available this session and will " +
                "be saved at the next save point. " +
                result.Detail,
                this
            );
        }
        finally
        {
            isPersisting = false;
        }
    }

    private void ReportUnsupportedSave()
    {
        if (unsupportedSaveReported)
        {
            return;
        }

        unsupportedSaveReported = true;
        Debug.LogWarning(
            "Educational discoveries stay available this " +
            "session, but the progress file was not " +
            "overwritten because its schema is unsupported " +
            "or the file could not be accessed safely.",
            this
        );
    }

    private void PersistIfPending()
    {
        if (!savePending)
        {
            return;
        }

        PersistDiscoveries();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            PersistIfPending();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            PersistIfPending();
        }
    }

    private void OnApplicationQuit()
    {
        PersistIfPending();
    }

    private void OnDestroy()
    {
        if (collectibleSpawner != null)
        {
            collectibleSpawner.OnCollectibleCaptured -=
                HandleCollectibleCaptured;
        }

        PersistIfPending();
    }
}
