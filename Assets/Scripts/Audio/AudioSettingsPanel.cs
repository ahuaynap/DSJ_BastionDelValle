using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AudioSettingsPanel : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private PauseManager pauseManager;
    [SerializeField] private EducationalModalController educationalModal;
    [SerializeField] private CodexController codex;
    [SerializeField] private MatchAudio matchAudio;
    [SerializeField] private EducationalCollectionManager collection;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider effectsSlider;
    [SerializeField] private bool menuHost;

    private bool isOpen;
    private bool ownsPause;
    private bool missingReported;

    public bool IsOpen => isOpen;

    public void OpenFromButton()
    {
        if (!CanOpen())
        {
            return;
        }

        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
        isOpen = true;
        MatchConsultation.NotifyOpened();
        SyncSliders();

        if (!menuHost && pauseManager != null)
        {
            pauseManager.RequestPause(PauseReason.UserMenu);
            ownsPause = true;
        }
    }

    public void CloseFromButton()
    {
        WorldInteractionBlock.SuppressCurrentClick();
        CommitPreferences();
        Hide();
    }

    public void CommitPreferences()
    {
        if (matchAudio == null || collection == null)
        {
            return;
        }

        collection.TrySaveAudioPreferences(
            matchAudio.MusicVolume,
            matchAudio.EffectsVolume
        );
    }

    private void OnEnable()
    {
        Bind();

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged += HandleGameState;
        }
    }

    private void OnDisable()
    {
        Unbind();

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= HandleGameState;
        }

        if (isOpen)
        {
            CommitPreferences();
        }

        Hide();
    }

    private void Bind()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(OpenFromButton);
            openButton.onClick.AddListener(OpenFromButton);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseFromButton);
            closeButton.onClick.AddListener(CloseFromButton);
        }

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(HandleMusic);
            musicSlider.onValueChanged.AddListener(HandleMusic);
        }

        if (effectsSlider != null)
        {
            effectsSlider.onValueChanged.RemoveListener(HandleEffects);
            effectsSlider.onValueChanged.AddListener(HandleEffects);
        }
    }

    private void Unbind()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(OpenFromButton);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseFromButton);
        }

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(HandleMusic);
        }

        if (effectsSlider != null)
        {
            effectsSlider.onValueChanged.RemoveListener(HandleEffects);
        }
    }

    private bool CanOpen()
    {
        if (!HasDependencies())
        {
            return false;
        }

        if (isOpen)
        {
            return false;
        }

        if (menuHost)
        {
            return true;
        }

        if (!gameManager.IsPlaying)
        {
            return false;
        }

        if (educationalModal.IsModalOpen || codex.IsOpen)
        {
            return false;
        }

        return true;
    }

    private bool HasDependencies()
    {
        bool valid = menuHost
            ? collection != null &&
                panelRoot != null &&
                closeButton != null &&
                musicSlider != null &&
                effectsSlider != null
            : gameManager != null &&
                pauseManager != null &&
                educationalModal != null &&
                codex != null &&
                matchAudio != null &&
                panelRoot != null &&
                openButton != null &&
                closeButton != null &&
                musicSlider != null &&
                effectsSlider != null;

        if (valid || missingReported)
        {
            return valid;
        }

        missingReported = true;
        Debug.LogError(
            "Audio settings are missing a reference and stay closed.",
            this
        );
        return false;
    }

    private void SyncSliders()
    {
        float music = matchAudio != null
            ? matchAudio.MusicVolume
            : collection != null
                ? collection.MusicPreference
                : PlayerProgressCodec.DefaultMusicVolume;
        float effects = matchAudio != null
            ? matchAudio.EffectsVolume
            : collection != null
                ? collection.EffectsPreference
                : PlayerProgressCodec.DefaultEffectsVolume;

        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(music);
        }

        if (effectsSlider != null)
        {
            effectsSlider.SetValueWithoutNotify(effects);
        }
    }

    private void HandleMusic(float value)
    {
        if (matchAudio != null)
        {
            matchAudio.SetMusicVolume(value);
        }

        if (menuHost && collection != null)
        {
            collection.TrySaveAudioPreferences(
                value,
                effectsSlider != null
                    ? effectsSlider.value
                    : collection.EffectsPreference);
        }
    }

    private void HandleEffects(float value)
    {
        if (matchAudio != null)
        {
            matchAudio.SetEffectsVolume(value);
        }

        if (menuHost && collection != null)
        {
            collection.TrySaveAudioPreferences(
                musicSlider != null
                    ? musicSlider.value
                    : collection.MusicPreference,
                value);
        }
    }

    private void HandleGameState(GameState state)
    {
        if (state == GameState.Playing || !isOpen)
        {
            return;
        }

        CommitPreferences();
        Hide();
    }

    private void Hide()
    {
        bool wasOpen = isOpen;

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        isOpen = false;
        ReleaseOwnedPause();

        if (wasOpen)
        {
            MatchConsultation.NotifyClosed();
        }
    }

    private void ReleaseOwnedPause()
    {
        if (!ownsPause || pauseManager == null)
        {
            ownsPause = false;
            return;
        }

        pauseManager.ReleasePause(PauseReason.UserMenu);
        ownsPause = false;
    }
}

public class AudioSliderCommit :
    MonoBehaviour,
    IPointerUpHandler
{
    [SerializeField] private AudioSettingsPanel panel;

    public void OnPointerUp(PointerEventData eventData)
    {
        if (panel != null)
        {
            panel.CommitPreferences();
        }
    }
}
