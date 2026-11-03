using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private EducationalCollectionManager collection;
    [SerializeField] private CodexController codex;
    [SerializeField] private AudioSettingsPanel audioSettings;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private GameObject confirmRoot;
    [SerializeField] private TMP_Text statusText;

    private void OnEnable()
    {
        Bind(continueButton, Continue);
        Bind(newGameButton, AskNewGame);
        Bind(confirmButton, ConfirmNewGame);
        Bind(cancelButton, HideConfirm);
        Refresh();
    }

    private void OnDisable()
    {
        Unbind(continueButton, Continue);
        Unbind(newGameButton, AskNewGame);
        Unbind(confirmButton, ConfirmNewGame);
        Unbind(cancelButton, HideConfirm);
    }

    public void Continue()
    {
        if (collection == null || !collection.CanContinue)
        {
            Refresh();
            return;
        }

        CampaignTravel.ToMap(collection.ContinueDestination);
    }

    public void AskNewGame()
    {
        if (collection != null && collection.NeedsNewGameConfirmation)
        {
            if (confirmRoot != null)
            {
                confirmRoot.SetActive(true);
            }

            return;
        }

        ConfirmNewGame();
    }

    public void ConfirmNewGame()
    {
        HideConfirm();

        if (collection == null)
        {
            SetStatus("No se pudo preparar la campaña.");
            return;
        }

        if (!collection.TryResetCampaign(out string detail))
        {
            SetStatus(string.IsNullOrEmpty(detail)
                ? "No se pudo borrar la campaña."
                : detail);
            return;
        }

        if (!collection.TryBeginCampaign(out detail))
        {
            SetStatus(string.IsNullOrEmpty(detail)
                ? "No se pudo guardar el inicio de la campaña."
                : detail);
        }

        CampaignTravel.ToMap(CampaignRules.FirstMap);
    }

    public void OpenCodex()
    {
        if (codex != null)
        {
            codex.OpenCodex();
        }
    }

    public void OpenSound()
    {
        if (audioSettings != null)
        {
            audioSettings.OpenFromButton();
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void HideConfirm()
    {
        if (confirmRoot != null)
        {
            confirmRoot.SetActive(false);
        }
    }

    private void Refresh()
    {
        bool canContinue = collection != null && collection.CanContinue;

        if (continueButton != null)
        {
            continueButton.interactable = canContinue;
        }

        if (collection != null && collection.SecondMapCleared)
        {
            SetStatus(CampaignRules.ContinueLabel(true));
        }
        else if (canContinue)
        {
            SetStatus(CampaignRules.ContinueLabel(false));
        }
        else
        {
            SetStatus("Aún no hay una campaña para continuar.");
        }

        HideConfirm();
    }

    private void SetStatus(string value)
    {
        if (statusText != null)
        {
            statusText.text = value;
        }
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
