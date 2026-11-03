using System;
using System.Collections.Generic;
using UnityEngine;

public class EducationalCollectionManager :
    MonoBehaviour
{
    [SerializeField]
    private CollectibleSpawner collectibleSpawner;

    [SerializeField]
    private GameManager gameManager;

    [SerializeField]
    private bool menuHost;

    private readonly HashSet<string>
        discoveredIds = new();

    private readonly List<string>
        discoveredOrder = new();

    private DiscoveryProgressStore progressStore;
    private int runDiscoveryCount;
    private bool savePending;
    private bool writesBlocked;
    private bool isPersisting;
    private bool unsupportedSaveReported;
    private bool hasStoredAudio;
    private string lastPersistDetail;

    public int DiscoveryCount =>
        discoveredIds.Count;

    public int RunDiscoveryCount =>
        runDiscoveryCount;

    public bool HasStoredAudio => hasStoredAudio;

    public string LastPersistDetail => lastPersistDetail;

    public bool CampaignStarted =>
        progressStore != null && progressStore.CampaignStarted;

    public bool CanContinue =>
        CampaignRules.CanContinue(CampaignStarted);

    public bool SecondMapCleared =>
        progressStore != null &&
        CampaignRules.SecondMapCleared(progressStore.CompletedMaps);

    public string ContinueDestination =>
        progressStore == null
            ? null
            : CampaignRules.Destination(
                progressStore.CampaignStarted,
                progressStore.CompletedMaps);

    public bool NeedsNewGameConfirmation =>
        progressStore != null &&
        CampaignRules.NeedsConfirmation(
            progressStore.CampaignStarted,
            progressStore.CompletedMaps.Count,
            DiscoveryCount,
            progressStore.ClearedTutorials.Count,
            progressStore.Evaluations.Count);

    public float MusicPreference =>
        progressStore != null
            ? progressStore.MusicVolume
            : PlayerProgressCodec.DefaultMusicVolume;

    public float EffectsPreference =>
        progressStore != null
            ? progressStore.EffectsVolume
            : PlayerProgressCodec.DefaultEffectsVolume;

    public bool TrySaveAudioPreferences(
        float music,
        float effects)
    {
        if (progressStore == null ||
            writesBlocked ||
            progressStore.WritesBlocked)
        {
            return false;
        }

        progressStore.SetAudioPreferences(music, effects);
        savePending = true;
        PersistDiscoveries();
        return !savePending && !writesBlocked;
    }

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
            if (!menuHost)
            {
                Debug.LogError(
                    "EducationalCollectionManager has no " +
                    "CollectibleSpawner.",
                    this
                );
            }

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

    private void OnEnable()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged += HandleCampaignState;
        }
    }

    private void HandleCampaignState(GameState state)
    {
        if (state != GameState.Victory)
        {
            return;
        }

        TryRecordVictory(gameObject.scene.name, out _);
    }

    public bool TryResetCampaign(out string detail)
    {
        detail = null;

        if (progressStore == null || writesBlocked)
        {
            detail = "No se pudo borrar la campaña.";
            lastPersistDetail = detail;
            return false;
        }

        ProgressSaveResult result = progressStore.ResetCampaign();

        if (result.Status != ProgressSaveStatus.Saved)
        {
            detail = string.IsNullOrEmpty(result.Detail)
                ? "No se pudo borrar la campaña."
                : result.Detail;
            lastPersistDetail = detail;
            return false;
        }

        discoveredIds.Clear();
        discoveredOrder.Clear();
        runDiscoveryCount = 0;
        savePending = false;
        lastPersistDetail = null;
        OnDiscoveryCountChanged?.Invoke(DiscoveryCount);
        return true;
    }

    public bool TryBeginCampaign(out string detail)
    {
        detail = null;

        if (progressStore == null)
        {
            detail = "No se pudo guardar el inicio de la campaña.";
            lastPersistDetail = detail;
            return false;
        }

        progressStore.NoteStarted();
        savePending = true;
        PersistDiscoveries();

        if (savePending || writesBlocked)
        {
            detail = "No se pudo guardar el inicio de la campaña.";
            lastPersistDetail = detail;
            return false;
        }

        return true;
    }

    public bool TryRecordVictory(string sceneName, out string detail)
    {
        detail = null;

        if (progressStore == null ||
            !CampaignRules.IsPlayableMap(sceneName))
        {
            detail = "No se pudo guardar el mapa completado.";
            lastPersistDetail = detail;
            return false;
        }

        if (writesBlocked || progressStore.WritesBlocked)
        {
            progressStore.NoteMap(sceneName);
            detail = "No se pudo guardar el mapa completado.";
            lastPersistDetail = detail;
            return false;
        }

        bool added = progressStore.NoteMap(sceneName);

        if (!added && !savePending)
        {
            return true;
        }

        savePending = true;
        PersistDiscoveries();

        if (savePending || writesBlocked)
        {
            detail = string.IsNullOrEmpty(lastPersistDetail)
                ? "No se pudo guardar el mapa completado."
                : lastPersistDetail;
            lastPersistDetail = detail;
            return false;
        }

        return true;
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
        hasStoredAudio = !loaded.AudioUsesDefaults &&
            (loaded.Status == ProgressLoadStatus.Loaded ||
             loaded.Status ==
                ProgressLoadStatus.RecoveredFromBackup);

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
            runDiscoveryCount++;
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
                lastPersistDetail = null;
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
            lastPersistDetail = string.IsNullOrEmpty(result.Detail)
                ? "No se pudo guardar el mapa completado."
                : result.Detail;
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

    public bool TryFlushPending(out string detail)
    {
        detail = null;

        if (!savePending)
        {
            return true;
        }

        if (progressStore == null ||
            writesBlocked ||
            progressStore.WritesBlocked)
        {
            detail =
                "No se pudieron guardar las memorias de esta partida.";
            return false;
        }

        PersistDiscoveries();

        if (savePending || writesBlocked)
        {
            detail =
                "No se pudieron guardar las memorias de esta partida.";
            return false;
        }

        return true;
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
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= HandleCampaignState;
        }

        if (collectibleSpawner != null)
        {
            collectibleSpawner.OnCollectibleCaptured -=
                HandleCollectibleCaptured;
        }

        PersistIfPending();
    }
}
