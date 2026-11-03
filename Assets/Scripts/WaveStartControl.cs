using UnityEngine;
using UnityEngine.UI;

public class WaveStartControl : MonoBehaviour
{
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private PauseManager pauseManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Button button;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(StartWave);
            button.onClick.AddListener(StartWave);
        }

        if (waveManager != null)
        {
            waveManager.OnWaveStateChanged += Refresh;
        }

        if (pauseManager != null)
        {
            pauseManager.OnPauseStateChanged += RefreshPause;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged += RefreshResult;
        }

        Refresh(WaveState.Waiting);
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(StartWave);
        }

        if (waveManager != null)
        {
            waveManager.OnWaveStateChanged -= Refresh;
        }

        if (pauseManager != null)
        {
            pauseManager.OnPauseStateChanged -= RefreshPause;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= RefreshResult;
        }
    }

    private void StartWave()
    {
        if (waveManager != null)
        {
            waveManager.RequestStart();
        }

        Refresh(WaveState.Waiting);
    }

    private void RefreshPause(bool paused)
    {
        Refresh(WaveState.Waiting);
    }

    private void RefreshResult(GameState state)
    {
        Refresh(WaveState.Waiting);
    }

    private void Refresh(WaveState state)
    {
        if (button == null || waveManager == null)
        {
            return;
        }

        button.interactable = waveManager.CanRequestStart;
    }
}
