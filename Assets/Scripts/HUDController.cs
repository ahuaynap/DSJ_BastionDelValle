using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    private const string OpeningMessage =
        "Construye tus guardianes y comienza cuando estés listo";

    private const string NextWaveMessage =
        "Prepara la siguiente oleada";

    private const string LastWaveMessage =
        "Preparación de la última oleada";

    private const string ExhaustedMessage =
        "Ya descubriste todas las memorias de este nivel";

    [Header("Game Systems")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private BastionHealth bastionHealth;
    [SerializeField] private EconomyManager economyManager;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private EducationalCollectionManager collectionManager;
    [SerializeField] private CollectibleSpawner collectibleSpawner;
    [SerializeField] private HabitatSession habitat;

    [Header("Texts")]
    [SerializeField] private TMP_Text bastionText;
    [SerializeField] private TMP_Text resourcesText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text enemiesText;
    [SerializeField] private TMP_Text waveStateText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text discoveriesText;
    [SerializeField] private TMP_Text lessonText;
    [SerializeField] private TMP_Text habitatWaterText;
    [SerializeField] private TMP_Text habitatSoilText;
    [SerializeField] private TMP_Text habitatLifeText;
    [SerializeField] private TMP_Text habitatCauseText;
    [SerializeField] private Button habitatHelpButton;
    [SerializeField] private GameObject habitatNoteRoot;
    [SerializeField] private GameObject preparationRoot;

    private string reloadNotice;
    private bool showHabitatNote;
    private float noticeUntil;
    private int seenDiscoveries = -1;
    private int toastedReward = -1;
    private bool toastedReload;

    private void Start()
    {
        reloadNotice = SessionReload.ConsumeNotice();

        waveManager.OnWaveChanged += UpdateWave;
        bastionHealth.OnHealthChanged += UpdateBastion;
        economyManager.OnResourcesChanged += UpdateResources;
        waveManager.OnRemainingEnemiesChanged += UpdateEnemies;
        gameManager.OnGameStateChanged += UpdateStatus;
        waveManager.OnWaveStateChanged += UpdateWaveState;

        UpdateWave(waveManager.CurrentWave, waveManager.TotalWaves);
        UpdateBastion(
            bastionHealth.CurrentHealth,
            bastionHealth.MaxHealth
        );
        UpdateResources(economyManager.CurrentResources);
        UpdateEnemies(waveManager.RemainingEnemies);
        BindDiscoveries();

        if (waveStateText != null)
        {
            waveStateText.gameObject.SetActive(false);
        }

        if (discoveriesText != null)
        {
            discoveriesText.gameObject.SetActive(false);
        }

        if (statusText != null)
        {
            statusText.gameObject.SetActive(false);
        }

        BindHabitat();
        RefreshContext();
    }

    private void OnDestroy()
    {
        if (waveManager != null)
        {
            waveManager.OnWaveChanged -= UpdateWave;
            waveManager.OnRemainingEnemiesChanged -= UpdateEnemies;
            waveManager.OnWaveStateChanged -= UpdateWaveState;
        }

        if (bastionHealth != null)
        {
            bastionHealth.OnHealthChanged -= UpdateBastion;
        }

        if (economyManager != null)
        {
            economyManager.OnResourcesChanged -= UpdateResources;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= UpdateStatus;
        }

        if (habitat != null)
        {
            habitat.Changed -= RefreshHabitat;
            habitat.CauseReady -= ShowHabitatCause;
        }

        if (habitatHelpButton != null)
        {
            habitatHelpButton.onClick.RemoveListener(ToggleHabitatNote);
        }

        if (collectionManager != null)
        {
            collectionManager.OnDiscoveryCountChanged -=
                UpdateDiscoveries;
        }
    }

    private void Update()
    {
        if (noticeUntil <= 0f || Time.unscaledTime < noticeUntil)
        {
            return;
        }

        noticeUntil = 0f;

        if (statusText != null)
        {
            statusText.text = string.Empty;
            statusText.gameObject.SetActive(false);
        }
    }

    private void BindDiscoveries()
    {
        if (collectionManager == null)
        {
            Debug.LogError(
                "HUDController cannot follow the memories " +
                "of this match. Assign the collection manager.",
                this
            );
            return;
        }

        collectionManager.OnDiscoveryCountChanged += UpdateDiscoveries;
        UpdateDiscoveries(collectionManager.RunDiscoveryCount);
    }

    private void UpdateBastion(int currentHealth, int maxHealth)
    {
        bastionText.text =
            "Integridad " + currentHealth + "/" + maxHealth;
    }

    private void UpdateResources(int resources)
    {
        resourcesText.text = "Semillas " + resources;
    }

    private void UpdateEnemies(int enemies)
    {
        if (enemiesText == null || waveManager == null)
        {
            return;
        }

        int pending = waveManager.PendingEnemies;
        enemiesText.text = pending > 0
            ? "Enemigos " + waveManager.ActiveEnemies + "+" + pending
            : "Enemigos " + waveManager.ActiveEnemies;
    }

    private void UpdateStatus(GameState state)
    {
        RefreshContext();
    }

    private void UpdateWave(int currentWave, int totalWaves)
    {
        waveText.text = "Oleada " + currentWave + "/" + totalWaves;
    }

    private void UpdateWaveState(WaveState state)
    {
        if (waveStateText != null)
        {
            waveStateText.text = string.Empty;
        }

        UpdateEnemies(waveManager.RemainingEnemies);
        RefreshContext();
    }

    private void UpdateDiscoveries(int discoveries)
    {
        if (collectionManager == null)
        {
            return;
        }

        if (seenDiscoveries >= 0 && discoveries > seenDiscoveries)
        {
            ShowToast(
                MemoriesExhausted()
                    ? ExhaustedMessage
                    : "Memoria registrada");
        }

        seenDiscoveries = discoveries;
        RefreshContext();
    }

    private void RefreshContext()
    {
        if (statusText == null || waveManager == null)
        {
            return;
        }

        MatchView view = MatchPhase.View(
            gameManager == null || gameManager.IsPlaying,
            waveManager.CurrentWaveState);

        bool preparing = view == MatchView.Preparation ||
            waveManager.CurrentWaveState == WaveState.Waiting;

        if (preparationRoot != null)
        {
            preparationRoot.SetActive(
                preparing &&
                (gameManager == null || gameManager.IsPlaying));
        }

        if (view == MatchView.Result)
        {
            ClearLesson();
            return;
        }

        if (view == MatchView.SpawnFailure)
        {
            ClearLesson();
            ShowToast(string.IsNullOrEmpty(waveManager.SpawnFailure)
                ? "No se pudo crear la oleada. Abre Pausa y reinicia el nivel."
                : waveManager.SpawnFailure +
                  " Abre Pausa y reinicia el nivel.");
            return;
        }

        if (!toastedReload &&
            !string.IsNullOrEmpty(reloadNotice) &&
            waveManager.ClearedWaves == 0 &&
            preparing)
        {
            toastedReload = true;
            ShowToast(reloadNotice);
        }

        if (preparing)
        {
            if (waveManager.LastBaseReward > 0 &&
                toastedReward != waveManager.CurrentWave)
            {
                toastedReward = waveManager.CurrentWave;
                ShowToast(HabitatRules.RewardBreakdown(
                    waveManager.LastBaseReward,
                    waveManager.LastSoilBonus));
            }

            ShowBrief();
            return;
        }

        ClearLesson();
    }

    private void ShowBrief()
    {
        string briefing = PreparationStatus();

        if (waveManager.TryPreparationBrief(
                out string threat,
                out int count,
                out string advice))
        {
            briefing = LessonRules.Briefing(threat, count, advice);
        }
        else if (waveManager.PreparingAirThreat)
        {
            briefing =
                "Se acercan amenazas aéreas. El Guardián y el Vigía pueden atacarlas";
        }

        if (lessonText != null)
        {
            lessonText.text = briefing;
        }
    }

    private void ShowToast(string message)
    {
        if (statusText == null || string.IsNullOrEmpty(message))
        {
            return;
        }

        statusText.gameObject.SetActive(true);
        statusText.text = message;
        noticeUntil = Time.unscaledTime + 2.4f;
    }

    private string PreparationStatus()
    {
        if (waveManager.CurrentWave == waveManager.TotalWaves &&
            waveManager.ClearedWaves > 0)
        {
            return LastWaveMessage;
        }

        if (waveManager.ClearedWaves <= 0)
        {
            return MemoriesExhausted() ? ExhaustedMessage : OpeningMessage;
        }

        return NextWaveMessage;
    }

    private void ClearLesson()
    {
        if (lessonText != null)
        {
            lessonText.text = string.Empty;
        }
    }

    private void BindHabitat()
    {
        if (habitat == null)
        {
            return;
        }

        habitat.Changed += RefreshHabitat;
        habitat.CauseReady += ShowHabitatCause;

        if (habitatHelpButton != null)
        {
            habitatHelpButton.onClick.RemoveListener(ToggleHabitatNote);
            habitatHelpButton.onClick.AddListener(ToggleHabitatNote);
        }

        RefreshHabitat(default);
        ShowHabitatCause(habitat.LastCause);
    }

    private void RefreshHabitat(HabitatChange change)
    {
        if (habitat == null)
        {
            return;
        }

        if (habitatWaterText != null)
        {
            habitatWaterText.text = "Agua " + habitat.Water;
        }

        if (habitatSoilText != null)
        {
            habitatSoilText.text = "Suelo " + habitat.Soil;
        }

        if (habitatLifeText != null)
        {
            habitatLifeText.text = "Vida " + habitat.Life;
        }
    }

    private void ShowHabitatCause(string cause)
    {
        showHabitatNote = false;
        SetHabitatNote(false, null);
    }

    private void ToggleHabitatNote()
    {
        if (habitat == null)
        {
            return;
        }

        showHabitatNote = !showHabitatNote;
        string note = habitat.Consultation();

        if (!string.IsNullOrEmpty(habitat.LastCause))
        {
            note += "\n" + habitat.LastCause;
        }

        SetHabitatNote(showHabitatNote, showHabitatNote ? note : null);
    }

    private void SetHabitatNote(bool open, string text)
    {
        GameObject root = habitatNoteRoot != null
            ? habitatNoteRoot
            : habitatCauseText != null
                ? habitatCauseText.gameObject
                : null;

        if (root != null)
        {
            root.SetActive(open);
        }

        if (open && habitatCauseText != null)
        {
            habitatCauseText.text = text;
        }
    }

    private bool MemoriesExhausted()
    {
        return collectibleSpawner != null &&
            collectionManager != null &&
            !collectibleSpawner.HasUndiscoveredMemories();
    }
}
