using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndGameUI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private BastionHealth bastionHealth;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private EducationalCollectionManager collectionManager;
    [SerializeField] private HabitatSession habitat;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button menuButton;

    private bool traveling;
    private string saveDetail;

    private void OnEnable()
    {
        Bind(nextButton, Advance);
        Bind(retryButton, Retry);
        Bind(menuButton, ReturnToMenu);
    }

    private void Start()
    {
        gameManager.OnGameStateChanged += HandleGameStateChanged;
        HandleGameStateChanged(gameManager.CurrentState);
    }

    private void OnDisable()
    {
        Unbind(nextButton, Advance);
        Unbind(retryButton, Retry);
        Unbind(menuButton, ReturnToMenu);
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }
    }

    private void HandleGameStateChanged(GameState state)
    {
        bool victory = state == GameState.Victory;
        bool showResult = victory || state == GameState.Defeat;
        string next = victory
            ? CampaignRules.NextMap(gameObject.scene.name)
            : null;

        SetVisible(resultText, showResult);
        SetVisible(retryButton, showResult);
        SetVisible(menuButton, showResult);
        SetVisible(nextButton, showResult && next != null);

        if (nextButton != null)
        {
            nextButton.interactable = !traveling;
            SetLabel(nextButton, "Siguiente nivel");
        }

        SetLabel(retryButton, "Reintentar");
        SetLabel(menuButton, "Menú");

        if (!showResult || resultText == null)
        {
            return;
        }

        string title = victory
            ? next == null
                ? "Contenido disponible completado"
                : "Valle protegido"
            : "El corazón necesita otro intento";
        int health = bastionHealth != null
            ? bastionHealth.CurrentHealth
            : 0;
        int maxHealth = bastionHealth != null
            ? bastionHealth.MaxHealth
            : 0;
        int cleared = waveManager != null
            ? waveManager.ClearedWaves
            : 0;
        int memories = collectionManager != null
            ? collectionManager.RunDiscoveryCount
            : 0;
        resultText.text = title + "\n" +
            "Integridad " + health + "/" + maxHealth +
            "   Oleadas " + cleared +
            "   Memorias " + memories;

        if (habitat != null && victory)
        {
            resultText.text += "\nAgua " + habitat.Water +
                "   Suelo " + habitat.Soil +
                "   Vida " + habitat.Life;
        }

        if (!string.IsNullOrEmpty(saveDetail))
        {
            resultText.text += "\n" + saveDetail;
        }

        PresentCard();
    }

    private void PresentCard()
    {
        if (resultText == null)
        {
            return;
        }

        resultText.fontSize = 26f;
        resultText.enableAutoSizing = false;
        resultText.enableWordWrapping = true;
        resultText.alignment = TextAlignmentOptions.Center;
        resultText.overflowMode = TextOverflowModes.Overflow;
        resultText.raycastTarget = false;

        RectTransform rect = resultText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(640f, 132f);

        if (retryButton != null)
        {
            RectTransform button = retryButton.transform as RectTransform;

            if (button != null && rect.parent == button.parent)
            {
                rect.anchoredPosition = button.anchoredPosition + new Vector2(0f, 150f);
            }
            else if (button != null)
            {
                rect.position = button.position + new Vector3(0f, 150f, 0f);
            }
        }

        Transform plate = resultText.transform.Find("ResultPlate");

        if (plate == null)
        {
            var background = new GameObject(
                "ResultPlate",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            background.transform.SetParent(resultText.transform, false);
            background.transform.SetAsFirstSibling();
            Image image = background.GetComponent<Image>();
            image.color = new Color(0.08f, 0.12f, 0.1f, 0.92f);
            image.raycastTarget = true;
            RectTransform plateRect = background.GetComponent<RectTransform>();
            plateRect.anchorMin = Vector2.zero;
            plateRect.anchorMax = Vector2.one;
            plateRect.offsetMin = new Vector2(-28f, -18f);
            plateRect.offsetMax = new Vector2(28f, 18f);
        }
    }

    private void Advance()
    {
        if (traveling || gameManager == null ||
            gameManager.CurrentState != GameState.Victory)
        {
            return;
        }

        string sceneName = gameObject.scene.name;
        string next = CampaignRules.NextMap(sceneName);

        if (next == null)
        {
            return;
        }

        string detail = null;

        if (collectionManager == null ||
            !collectionManager.TryRecordVictory(sceneName, out detail))
        {
            saveDetail = string.IsNullOrEmpty(detail)
                ? "No se pudo guardar el mapa completado."
                : detail;
            HandleGameStateChanged(gameManager.CurrentState);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(next))
        {
            saveDetail = next + " no está en el build activo.";
            HandleGameStateChanged(gameManager.CurrentState);
            return;
        }

        saveDetail = null;
        traveling = true;

        if (nextButton != null)
        {
            nextButton.interactable = false;
        }

        CampaignTravel.ToMap(next);
    }

    private void Retry()
    {
        if (traveling)
        {
            return;
        }

        traveling = true;
        SessionReload.Reload(collectionManager);
    }

    private void ReturnToMenu()
    {
        if (traveling)
        {
            return;
        }

        traveling = true;
        CampaignTravel.ToMenu(collectionManager);
    }

    private static void SetVisible(Component widget, bool visible)
    {
        if (widget != null)
        {
            widget.gameObject.SetActive(visible);
        }
    }

    private static void SetLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);

        if (text != null)
        {
            text.text = label;
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
