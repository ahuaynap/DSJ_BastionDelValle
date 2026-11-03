using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EducationalModalController :
    MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private EducationalCollectionManager
        collectionManager;

    [SerializeField]
    private PauseManager pauseManager;

    [SerializeField]
    private GameManager gameManager;

    [Header("Modal")]
    [SerializeField]
    private GameObject modalRoot;

    [SerializeField]
    private TMP_Text categoryText;

    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text descriptionText;

    [SerializeField]
    private TMP_Text educationalContentText;

    [SerializeField]
    private TMP_Text newDiscoveryText;

    [SerializeField]
    private Image infographicImage;

    [SerializeField]
    private Sprite fallbackInfographic;

    [SerializeField]
    private Button continueButton;

    [SerializeField]
    private Button viewCodexButton;

    [SerializeField]
    private CodexController codexController;

    private CollectibleData displayedCollectible;
    private bool isModalOpen;
    private bool missingCodexReported;

    public bool IsModalOpen =>
        isModalOpen;

    public event Action
        OnModalClosed;

    private void Start()
    {
        if (!ValidateDependencies())
        {
            return;
        }

        collectionManager.OnDiscoveryRegistered +=
            HandleDiscoveryRegistered;

        gameManager.OnGameStateChanged +=
            HandleGameStateChanged;

        continueButton.onClick.AddListener(
            CloseModal
        );

        if (viewCodexButton != null)
        {
            if (viewCodexButton.targetGraphic != null)
            {
                viewCodexButton.targetGraphic.raycastTarget =
                    true;
            }

            viewCodexButton.onClick.AddListener(
                OpenCodexFromCard
            );
        }
        else
        {
            Debug.LogError(
                "EducationalModalController has no " +
                "View Codex button.",
                this
            );
        }

        HideModalVisuals();
    }

    private void HandleDiscoveryRegistered(
        CollectibleData data,
        bool isNewDiscovery)
    {
        if (data == null)
        {
            return;
        }

        if (!gameManager.IsPlaying)
        {
            return;
        }

        OpenModal(
            data,
            isNewDiscovery
        );
    }

    private void OpenModal(
        CollectibleData data,
        bool isNewDiscovery)
    {
        if (isModalOpen)
        {
            return;
        }

        displayedCollectible = data;

        categoryText.text =
            GetCategoryLabel(data.Category);

        titleText.text =
            data.DisplayName;

        descriptionText.text =
            data.ShortDescription;

        educationalContentText.text =
            data.EducationalContent;

        newDiscoveryText.gameObject.SetActive(
            isNewDiscovery
        );

        Sprite infographic =
            data.InfographicSprite != null
                ? data.InfographicSprite
                : fallbackInfographic;

        infographicImage.sprite =
            infographic;

        infographicImage.gameObject.SetActive(
            infographic != null
        );

        modalRoot.SetActive(true);
        isModalOpen = true;

        pauseManager.RequestPause(
            PauseReason.EducationalModal
        );

        Debug.Log(
            $"Educational modal opened: " +
            $"{data.DisplayName}"
        );
    }

    public void CloseModal()
    {
        if (!isModalOpen)
        {
            return;
        }

        WorldInteractionBlock.SuppressCurrentClick();

        HideModalVisuals();

        pauseManager.ReleasePause(
            PauseReason.EducationalModal
        );

        OnModalClosed?.Invoke();

        Debug.Log(
            "Educational modal closed."
        );
    }

    private void OpenCodexFromCard()
    {
        if (!isModalOpen ||
            displayedCollectible == null)
        {
            return;
        }

        WorldInteractionBlock.SuppressCurrentClick();

        if (codexController == null)
        {
            ReportMissingCodex();
            return;
        }

        codexController.TryOpenForCollectedMemory(
            displayedCollectible,
            ReleaseModalForCodexHandoff
        );
    }

    private void ReleaseModalForCodexHandoff()
    {
        if (!isModalOpen)
        {
            return;
        }

        HideModalVisuals();

        pauseManager.ReleasePause(
            PauseReason.EducationalModal
        );
    }

    private void HandleGameStateChanged(
        GameState state)
    {
        if (state == GameState.Playing)
        {
            return;
        }

        if (!isModalOpen)
        {
            return;
        }

        HideModalVisuals();

        pauseManager.ReleasePause(
            PauseReason.EducationalModal
        );
    }

    private void HideModalVisuals()
    {
        isModalOpen = false;
        displayedCollectible = null;

        if (modalRoot != null)
        {
            modalRoot.SetActive(false);
        }
    }

    private string GetCategoryLabel(
        EducationalCategory category)
    {
        switch (category)
        {
            case EducationalCategory.Water:
                return "AGUA";

            case EducationalCategory.Soil:
                return "SUELO";

            case EducationalCategory.Biodiversity:
                return "BIODIVERSIDAD";

            case EducationalCategory.Agriculture:
                return "AGRICULTURA";

            case EducationalCategory.Ecosystem:
                return "ECOSISTEMA";

            default:
                return category
                    .ToString()
                    .ToUpperInvariant();
        }
    }

    private bool ValidateDependencies()
    {
        bool isValid = true;

        if (collectionManager == null)
        {
            Debug.LogError(
                "EducationalModalController has no " +
                "EducationalCollectionManager.",
                this
            );

            isValid = false;
        }

        if (pauseManager == null)
        {
            Debug.LogError(
                "EducationalModalController has no " +
                "PauseManager.",
                this
            );

            isValid = false;
        }

        if (gameManager == null)
        {
            Debug.LogError(
                "EducationalModalController has no " +
                "GameManager.",
                this
            );

            isValid = false;
        }

        if (modalRoot == null)
        {
            Debug.LogError(
                "EducationalModalController has no " +
                "Modal Root.",
                this
            );

            isValid = false;
        }

        if (categoryText == null ||
            titleText == null ||
            descriptionText == null ||
            educationalContentText == null ||
            newDiscoveryText == null ||
            infographicImage == null ||
            continueButton == null)
        {
            Debug.LogError(
                "EducationalModalController has missing " +
                "UI references.",
                this
            );

            isValid = false;
        }

        return isValid;
    }

    private void OnDestroy()
    {
        if (collectionManager != null)
        {
            collectionManager.OnDiscoveryRegistered -=
                HandleDiscoveryRegistered;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -=
                HandleGameStateChanged;
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(
                CloseModal
            );
        }

        if (viewCodexButton != null)
        {
            viewCodexButton.onClick.RemoveListener(
                OpenCodexFromCard
            );
        }

        ReleaseOwnPause();
    }

    private void OnDisable()
    {
        ReleaseOwnPause();
    }

    private void ReleaseOwnPause()
    {
        if (!isModalOpen ||
            pauseManager == null)
        {
            return;
        }

        HideModalVisuals();

        pauseManager.ReleasePause(
            PauseReason.EducationalModal
        );
    }

    private void ReportMissingCodex()
    {
        if (missingCodexReported)
        {
            return;
        }

        missingCodexReported = true;

        Debug.LogError(
            "EducationalModalController has no " +
            "CodexController. The educational card " +
            "stays open.",
            this
        );
    }
}