using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CodexController : MonoBehaviour
{
    private const string LockedEntryLabel =
        "Memoria por descubrir";

    private const string NoDiscoveriesMessage =
        "Aún no has descubierto memorias del valle. " +
        "Cuando captures una, podrás leerla aquí.";

    private const string EmptyCatalogMessage =
        "El códice no tiene memorias configuradas.";

    [Header("Dependencies")]
    [SerializeField]
    private GameManager gameManager;

    [SerializeField]
    private PauseManager pauseManager;

    [SerializeField]
    private EducationalCollectionManager collectionManager;

    [SerializeField]
    private EducationalModalController educationalModal;

    [SerializeField]
    private AudioSettingsPanel audioSettings;

    [SerializeField]
    private bool menuConsultation;

    [Header("Catalog")]
    [SerializeField]
    private CollectibleData[] catalog;

    [Header("HUD")]
    [SerializeField]
    private Button openButton;

    [Header("Panel")]
    [SerializeField]
    private GameObject panelRoot;

    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text counterText;

    [SerializeField]
    private ScrollRect entryScroll;

    [SerializeField]
    private RectTransform listContent;

    [SerializeField]
    private CodexEntryView entryPrefab;

    [SerializeField]
    private Button closeButton;

    [Header("Detail")]
    [SerializeField]
    private TMP_Text detailTitleText;

    [SerializeField]
    private TMP_Text detailCategoryText;

    [SerializeField]
    private Image detailImage;

    [SerializeField]
    private TMP_Text detailDescriptionText;

    [SerializeField]
    private TMP_Text detailContentText;

    [SerializeField]
    private TMP_Text messageText;

    private readonly HashSet<string> reportedCatalogProblems =
        new();

    private readonly List<CatalogEntry> visibleEntries =
        new();

    private GameObject initialSelection;
    private bool isOpen;
    private bool ownsCodexPause;
    private bool listenersBound;
    private bool missingDependenciesReported;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (!isOpen)
        {
            HidePanel();
        }
    }

    private void OnEnable()
    {
        BindListeners();
    }

    private void OnDisable()
    {
        HideAndRelease();
        UnbindListeners();
    }

    private void OnDestroy()
    {
        HideAndRelease();
        UnbindListeners();
    }

    public void OpenCodex()
    {
        if (isOpen || ownsCodexPause)
        {
            return;
        }

        if (!CanOpen())
        {
            return;
        }

        isOpen = true;
        ownsCodexPause = pauseManager != null && !menuConsultation;
        MatchConsultation.NotifyOpened();

        if (openButton != null)
        {
            openButton.interactable = false;
        }

        if (ownsCodexPause)
        {
            pauseManager.RequestPause(PauseReason.Codex);
        }

        ShowPanel();
        Rebuild(null);
        FocusInitialControl();
    }

    public bool TryOpenForCollectedMemory(
        CollectibleData data,
        Action releaseCurrentConsultation)
    {
        if (isOpen || ownsCodexPause)
        {
            return false;
        }

        if (releaseCurrentConsultation == null)
        {
            Debug.LogError(
                "Codex handoff has no way to release the " +
                "educational card. The card stays open.",
                this
            );

            return false;
        }

        if (!TryFindConsultableEntry(
                data,
                out CollectibleData entry))
        {
            return false;
        }

        isOpen = true;
        ownsCodexPause = true;

        if (openButton != null)
        {
            openButton.interactable = false;
        }

        pauseManager.RequestPause(PauseReason.Codex);
        releaseCurrentConsultation.Invoke();
        ShowPanel();
        Rebuild(entry);
        FocusInitialControl();
        return true;
    }

    public void CloseFromButton()
    {
        WorldInteractionBlock.SuppressCurrentClick();
        HideAndRelease();
        RestoreOpenerFocus();
    }

    private bool CanOpen()
    {
        if (!HasRequiredDependencies())
        {
            return false;
        }

        if (!menuConsultation && !gameManager.IsPlaying)
        {
            Debug.Log(
                "Codex can open only while the match is playing."
            );

            return false;
        }

        if (!menuConsultation && educationalModal.IsModalOpen)
        {
            Debug.Log(
                "Codex stays closed while the educational card is open."
            );

            return false;
        }

        if (audioSettings != null && audioSettings.IsOpen)
        {
            Debug.Log(
                "Codex stays closed while sound settings are open."
            );

            return false;
        }

        return true;
    }

    private bool HasRequiredDependencies()
    {
        bool valid = menuConsultation
            ? collectionManager != null &&
                panelRoot != null &&
                closeButton != null
            : gameManager != null &&
                pauseManager != null &&
                collectionManager != null &&
                educationalModal != null &&
                panelRoot != null &&
                openButton != null &&
                closeButton != null;

        if (valid || missingDependenciesReported)
        {
            return valid;
        }

        missingDependenciesReported = true;
        Debug.LogError(
            "CodexController is missing a required reference. " +
            "The codex cannot open.",
            this
        );

        return false;
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.Playing)
        {
            return;
        }

        HideAndRelease();
    }

    private void ShowPanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }

        if (titleText != null)
        {
            titleText.text = "Memorias del valle";
        }
    }

    private bool TryFindConsultableEntry(
        CollectibleData requested,
        out CollectibleData entry)
    {
        entry = null;

        if (!HasRequiredDependencies())
        {
            return false;
        }

        if (!gameManager.IsPlaying)
        {
            Debug.Log(
                "Codex handoff was rejected because the " +
                "match is not playing. The educational " +
                "card stays open."
            );

            return false;
        }

        if (!educationalModal.IsModalOpen)
        {
            Debug.Log(
                "Codex handoff is only available while the " +
                "educational card is open."
            );

            return false;
        }

        if (requested == null ||
            string.IsNullOrWhiteSpace(
                requested.CollectibleId))
        {
            Debug.LogWarning(
                "Codex handoff has no collected memory. " +
                "The educational card stays open.",
                this
            );

            return false;
        }

        if (entryPrefab == null || listContent == null)
        {
            Debug.LogWarning(
                "Codex cannot show the collected memory " +
                "because its list is not configured. " +
                "The educational card stays open.",
                this
            );

            return false;
        }

        entry = FindCatalogEntry(requested);

        if (entry == null)
        {
            Debug.LogWarning(
                "Codex has no entry for " +
                $"{requested.CollectibleId}. " +
                "The educational card stays open.",
                this
            );

            return false;
        }

        if (!collectionManager.HasDiscovered(
                entry.CollectibleId))
        {
            Debug.LogWarning(
                "Codex cannot consult " +
                $"{entry.CollectibleId} because it is " +
                "not discovered. The educational card " +
                "stays open.",
                this
            );

            entry = null;
            return false;
        }

        return true;
    }

    private CollectibleData FindCatalogEntry(
        CollectibleData requested)
    {
        if (catalog == null)
        {
            return null;
        }

        HashSet<string> seenIds = new();

        for (int index = 0; index < catalog.Length; index++)
        {
            CollectibleData candidate = catalog[index];

            if (!TryAcceptCatalogEntry(
                    candidate,
                    index,
                    seenIds))
            {
                continue;
            }

            if (SameMemory(candidate, requested))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool SameMemory(
        CollectibleData candidate,
        CollectibleData requested)
    {
        if (candidate == null || requested == null)
        {
            return false;
        }

        if (ReferenceEquals(candidate, requested))
        {
            return true;
        }

        string candidateId = candidate.CollectibleId;
        string requestedId = requested.CollectibleId;

        if (string.IsNullOrWhiteSpace(candidateId) ||
            string.IsNullOrWhiteSpace(requestedId))
        {
            return false;
        }

        return candidateId == requestedId;
    }

    private void Rebuild(CollectibleData preferredEntry)
    {
        ClearRows();
        visibleEntries.Clear();
        initialSelection = null;

        int availableCount = 0;
        int discoveredCount = 0;
        CollectibleData firstDiscovered = null;

        if (catalog != null)
        {
            HashSet<string> seenIds = new();

            for (int index = 0; index < catalog.Length; index++)
            {
                CollectibleData data = catalog[index];

                if (!TryAcceptCatalogEntry(
                        data,
                        index,
                        seenIds))
                {
                    continue;
                }

                bool discovered = collectionManager.HasDiscovered(
                    data.CollectibleId
                );

                availableCount++;

                if (discovered)
                {
                    discoveredCount++;

                    if (firstDiscovered == null)
                    {
                        firstDiscovered = data;
                    }
                }

                visibleEntries.Add(new CatalogEntry
                {
                    Data = data,
                    Discovered = discovered
                });
            }
        }

        if (counterText != null)
        {
            counterText.text =
                "Memorias " + discoveredCount + " / " + availableCount;
        }

        CollectibleData selected = firstDiscovered;

        if (preferredEntry != null)
        {
            for (
                int index = 0;
                index < visibleEntries.Count;
                index++)
            {
                CatalogEntry visible = visibleEntries[index];

                if (!visible.Discovered ||
                    !SameMemory(
                        visible.Data,
                        preferredEntry))
                {
                    continue;
                }

                selected = visible.Data;
                break;
            }
        }

        BuildRows(selected);
        Canvas.ForceUpdateCanvases();

        if (selected != null)
        {
            ShowDiscovery(selected);
            return;
        }

        HideDiscovery();
        ShowMessage(
            availableCount == 0
                ? EmptyCatalogMessage
                : NoDiscoveriesMessage
        );
    }

    private bool TryAcceptCatalogEntry(
        CollectibleData data,
        int index,
        HashSet<string> seenIds)
    {
        if (data == null)
        {
            ReportCatalogProblem(
                "null:" + index,
                $"Codex catalog entry {index} is missing."
            );

            return false;
        }

        if (string.IsNullOrWhiteSpace(data.CollectibleId))
        {
            ReportCatalogProblem(
                "blank:" + index,
                $"Codex catalog entry {index} has a blank id."
            );

            return false;
        }

        if (!seenIds.Add(data.CollectibleId))
        {
            ReportCatalogProblem(
                "duplicate:" + data.CollectibleId,
                "Codex catalog repeats " +
                $"{data.CollectibleId}. " +
                "The repeated entry is ignored."
            );

            return false;
        }

        return true;
    }

    private void ReportCatalogProblem(
        string key,
        string message)
    {
        if (!reportedCatalogProblems.Add(key))
        {
            return;
        }

        Debug.LogWarning(message, this);
    }

    private void BuildRows(CollectibleData firstDiscovered)
    {
        if (entryPrefab == null || listContent == null)
        {
            ReportCatalogProblem(
                "list",
                "CodexController has no entry prefab or list content."
            );

            return;
        }

        for (int index = 0; index < visibleEntries.Count; index++)
        {
            CatalogEntry entry = visibleEntries[index];
            CodexEntryView view = Instantiate(
                entryPrefab,
                listContent
            );

            if (entry.Discovered)
            {
                CollectibleData data = entry.Data;
                view.Bind(
                    data.DisplayName,
                    true,
                    () => ShowDiscovery(data)
                );

                if (data == firstDiscovered)
                {
                    initialSelection = view.SelectableObject;
                }
            }
            else
            {
                view.Bind(LockedEntryLabel, false, null);
            }
        }

        if (entryScroll != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                listContent
            );
            entryScroll.verticalNormalizedPosition = 1f;
        }
    }

    private void ShowDiscovery(CollectibleData data)
    {
        if (data == null)
        {
            return;
        }

        ShowMessage(null);

        SetText(detailTitleText, data.DisplayName);
        SetText(
            detailCategoryText,
            GetCategoryLabel(data.Category)
        );
        SetText(
            detailDescriptionText,
            data.ShortDescription
        );
        SetText(
            detailContentText,
            data.EducationalContent
        );
        ShowText(detailTitleText, true);
        ShowText(detailCategoryText, true);
        ShowText(detailDescriptionText, true);
        ShowText(detailContentText, true);

        if (detailImage == null)
        {
            return;
        }

        bool hasIllustration = data.InfographicSprite != null;
        detailImage.sprite = hasIllustration
            ? data.InfographicSprite
            : null;
        detailImage.gameObject.SetActive(hasIllustration);
    }

    private void HideDiscovery()
    {
        ShowText(detailTitleText, false);
        ShowText(detailCategoryText, false);
        ShowText(detailDescriptionText, false);
        ShowText(detailContentText, false);

        if (detailImage != null)
        {
            detailImage.sprite = null;
            detailImage.gameObject.SetActive(false);
        }
    }

    private void ShowMessage(string message)
    {
        if (messageText == null)
        {
            return;
        }

        bool visible = !string.IsNullOrEmpty(message);
        messageText.gameObject.SetActive(visible);
        messageText.text = visible ? message : string.Empty;
    }

    private void FocusInitialControl()
    {
        GameObject target = initialSelection;

        if (target == null && closeButton != null)
        {
            target = closeButton.gameObject;
        }

        SetSelection(target);
    }

    private void RestoreOpenerFocus()
    {
        if (openButton == null ||
            !openButton.isActiveAndEnabled ||
            !openButton.interactable)
        {
            return;
        }

        SetSelection(openButton.gameObject);
    }

    private static void SetSelection(GameObject target)
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null || target == null)
        {
            return;
        }

        eventSystem.SetSelectedGameObject(target);
    }

    private void HideAndRelease()
    {
        bool wasOpen = isOpen;
        isOpen = false;

        if (openButton != null)
        {
            openButton.interactable = true;
        }

        HidePanel();
        ClearRows();
        ReleaseCodexPause();

        if (wasOpen)
        {
            MatchConsultation.NotifyClosed();
        }
    }

    private void HidePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void ReleaseCodexPause()
    {
        if (!ownsCodexPause || pauseManager == null)
        {
            ownsCodexPause = false;
            return;
        }

        ownsCodexPause = false;
        pauseManager.ReleasePause(PauseReason.Codex);
    }

    private void ClearRows()
    {
        if (listContent == null)
        {
            return;
        }

        for (
            int index = listContent.childCount - 1;
            index >= 0;
            index--)
        {
            Transform child = listContent.GetChild(index);
            CodexEntryView view =
                child.GetComponent<CodexEntryView>();

            if (view != null)
            {
                view.Clear();
            }

            Destroy(child.gameObject);
        }
    }

    private void BindListeners()
    {
        if (listenersBound)
        {
            return;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged +=
                HandleGameStateChanged;
        }

        if (openButton != null)
        {
            openButton.onClick.AddListener(OpenCodex);
        }

        if (closeButton != null)
        {
            if (closeButton.targetGraphic != null)
            {
                closeButton.targetGraphic.raycastTarget = true;
            }

            closeButton.onClick.AddListener(CloseFromButton);
        }

        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (!listenersBound)
        {
            return;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -=
                HandleGameStateChanged;
        }

        if (openButton != null)
        {
            openButton.onClick.RemoveListener(OpenCodex);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseFromButton);
        }

        listenersBound = false;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }

    private static void ShowText(TMP_Text text, bool visible)
    {
        if (text != null)
        {
            text.gameObject.SetActive(visible);
        }
    }

    private static string GetCategoryLabel(
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

    private struct CatalogEntry
    {
        public CollectibleData Data;
        public bool Discovered;
    }
}
