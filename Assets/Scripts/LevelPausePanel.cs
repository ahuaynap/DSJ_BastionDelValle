using System;
using UnityEngine;
using UnityEngine.UI;

public static class MatchConsultation
{
    public static event Action Opened;
    public static event Action Closed;

    public static void NotifyOpened()
    {
        Opened?.Invoke();
    }

    public static void NotifyClosed()
    {
        Closed?.Invoke();
    }
}

public class LevelPausePanel : MonoBehaviour
{
    [SerializeField] private PauseManager pauseManager;
    [SerializeField] private EducationalCollectionManager collectionManager;
    [SerializeField] private Button pauseButton;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton;

    private bool concealed;

    private void OnEnable()
    {
        MatchConsultation.Opened += ConcealForConsultation;
        MatchConsultation.Closed += RestoreAfterConsultation;
        Bind(pauseButton, TogglePause);
        Bind(continueButton, Continue);
        Bind(restartButton, Restart);
        Bind(menuButton, ReturnToMenu);
    }

    private void OnDisable()
    {
        MatchConsultation.Opened -= ConcealForConsultation;
        MatchConsultation.Closed -= RestoreAfterConsultation;
        Unbind(pauseButton, TogglePause);
        Unbind(continueButton, Continue);
        Unbind(restartButton, Restart);
        Unbind(menuButton, ReturnToMenu);
    }

    private void TogglePause()
    {
        if (pauseManager == null || panelRoot == null)
        {
            return;
        }

        if (pauseManager.HasPauseRequest(PauseReason.LevelPause))
        {
            return;
        }

        if (pauseManager.IsPaused)
        {
            return;
        }

        pauseManager.RequestPause(PauseReason.LevelPause);
        panelRoot.SetActive(true);
    }

    private void ConcealForConsultation()
    {
        if (pauseManager == null ||
            panelRoot == null ||
            !panelRoot.activeSelf ||
            !pauseManager.HasPauseRequest(PauseReason.LevelPause))
        {
            return;
        }

        concealed = true;
        panelRoot.SetActive(false);
    }

    private void RestoreAfterConsultation()
    {
        if (!concealed)
        {
            return;
        }

        concealed = false;

        if (pauseManager != null &&
            panelRoot != null &&
            pauseManager.HasPauseRequest(PauseReason.LevelPause))
        {
            panelRoot.SetActive(true);
        }
    }

    private void Continue()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        if (pauseManager != null)
        {
            pauseManager.ReleasePause(PauseReason.LevelPause);
        }
    }

    private void Restart()
    {
        SessionReload.Reload(collectionManager);
    }

    private void ReturnToMenu()
    {
        CampaignTravel.ToMenu(collectionManager);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }
}
