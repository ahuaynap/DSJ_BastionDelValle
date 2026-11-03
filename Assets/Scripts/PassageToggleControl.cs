using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PassageToggleControl : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private ChannelNavigator navigator;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private PauseManager pauseManager;

    private void OnEnable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(Toggle);
            button.onClick.AddListener(Toggle);
        }

        if (navigator != null)
        {
            navigator.PassageChanged += Refresh;
        }

        if (pauseManager != null)
        {
            pauseManager.OnPauseStateChanged += HandlePause;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged += HandleGameState;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(Toggle);
        }

        if (navigator != null)
        {
            navigator.PassageChanged -= Refresh;
        }

        if (pauseManager != null)
        {
            pauseManager.OnPauseStateChanged -= HandlePause;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= HandleGameState;
        }
    }

    private void Update()
    {
        if (button == null)
        {
            return;
        }

        bool allowed = CanUse();

        if (button.interactable != allowed)
        {
            button.interactable = allowed;
        }
    }

    private void Toggle()
    {
        if (!CanUse() || navigator == null)
        {
            return;
        }

        bool accepted = navigator.TryToggle(
            gameManager,
            pauseManager,
            out string detail);

        if (!accepted && label != null)
        {
            label.text =
                "Demostración de navegación: " +
                navigator.PassageLabel +
                ". " +
                detail;
        }
    }

    private void HandlePause(bool paused)
    {
        Refresh();
    }

    private void HandleGameState(GameState state)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (button != null)
        {
            button.interactable = CanUse();
        }

        if (label != null && navigator != null)
        {
            label.text =
                "Demostración de navegación: " +
                navigator.PassageLabel;
        }
    }

    private bool CanUse()
    {
        if (navigator == null || gameManager == null || !gameManager.IsPlaying)
        {
            return false;
        }

        if (pauseManager == null || pauseManager.IsPaused)
        {
            return false;
        }

        return true;
    }
}
