using TMPro;
using UnityEngine;

public class HUDController : MonoBehaviour
{
    [Header("Game Systems")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private BastionHealth bastionHealth;
    [SerializeField] private EconomyManager economyManager;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private EducationalCollectionManager collectionManager;

    [Header("Texts")]
    [SerializeField] private TMP_Text bastionText;
    [SerializeField] private TMP_Text resourcesText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text enemiesText;
    [SerializeField] private TMP_Text waveStateText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text discoveriesText;

    private void Start()
    {
        waveManager.OnWaveChanged += UpdateWave;
        bastionHealth.OnHealthChanged += UpdateBastion;
        economyManager.OnResourcesChanged += UpdateResources;
        waveManager.OnRemainingEnemiesChanged += UpdateEnemies;
        gameManager.OnGameStateChanged += UpdateStatus;
        waveManager.OnWaveStateChanged += UpdateWaveState;

        UpdateWave(
            waveManager.CurrentWave,
            waveManager.TotalWaves
        );

        UpdateBastion(
            bastionHealth.CurrentHealth,
            bastionHealth.MaxHealth
        );

        UpdateResources(
            economyManager.CurrentResources
        );

        UpdateEnemies(
            waveManager.RemainingEnemies
        );

        UpdateStatus(
            gameManager.CurrentState
        );

        UpdateWaveState(
            waveManager.CurrentWaveState
        );

        waveText.text =
            $"WAVE    {waveManager.CurrentWave}";

        BindDiscoveries();
    }

    private void OnDestroy()
    {
        if (waveManager != null)
        {
            waveManager.OnWaveChanged -= UpdateWave;
        }

        if (bastionHealth != null)
        {
            bastionHealth.OnHealthChanged -= UpdateBastion;
        }

        if (economyManager != null)
        {
            economyManager.OnResourcesChanged -= UpdateResources;
        }

        if (waveManager != null)
        {
            waveManager.OnRemainingEnemiesChanged -= UpdateEnemies;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= UpdateStatus;
        }

        if (waveManager != null)
        {
            waveManager.OnWaveStateChanged -=
                UpdateWaveState;
        }

        if (collectionManager != null)
        {
            collectionManager.OnDiscoveryCountChanged -=
                UpdateDiscoveries;
        }
    }

    private void BindDiscoveries()
    {
        if (collectionManager == null ||
            discoveriesText == null)
        {
            Debug.LogError(
                "HUDController cannot show educational " +
                "discoveries. Assign the collection manager " +
                "and the discoveries text.",
                this
            );

            return;
        }

        collectionManager.OnDiscoveryCountChanged +=
            UpdateDiscoveries;
        UpdateDiscoveries(collectionManager.DiscoveryCount);
    }

    private void UpdateBastion(
        int currentHealth,
        int maxHealth)
    {
        bastionText.text =
            $"BASTION    {currentHealth} / {maxHealth}";
    }

    private void UpdateResources(int resources)
    {
        resourcesText.text =
            $"RESOURCES    {resources}";
    }

    private void UpdateEnemies(int enemies)
    {
        enemiesText.text =
            $"ENEMIES    {enemies}";
    }

    private void UpdateStatus(GameState state)
    {
        statusText.text =
            $"STATUS    {state.ToString().ToUpper()}";
    }

    private void UpdateWave(
        int currentWave,
        int totalWaves)
    {
        waveText.text =
            $"WAVE    {currentWave} / {totalWaves}";
    }

    private void UpdateWaveState(
        WaveState state)
    {
        waveStateText.text =
            $"WAVE STATE    {state.ToString().ToUpper()}";
    }

    private void UpdateDiscoveries(int discoveries)
    {
        if (discoveriesText == null)
        {
            return;
        }

        discoveriesText.text =
            $"DISCOVERIES    {discoveries}";
    }
}