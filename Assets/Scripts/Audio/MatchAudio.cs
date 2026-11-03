using System.Collections.Generic;
using UnityEngine;

public class MatchAudio : MonoBehaviour
{
    private const int GameplayVoiceCount = 6;

    [Header("Clips")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField] private AudioClip buildClip;
    [SerializeField] private AudioClip shotClip;
    [SerializeField] private AudioClip impactClip;
    [SerializeField] private AudioClip enemyDefeatClip;
    [SerializeField] private AudioClip heartDamageClip;
    [SerializeField] private AudioClip captureClip;
    [SerializeField] private AudioClip waveStartClip;
    [SerializeField] private AudioClip victoryClip;
    [SerializeField] private AudioClip defeatClip;
    [SerializeField] private AudioClip interfaceClickClip;
    [SerializeField] private AudioClip waterBuildClip;
    [SerializeField] private AudioClip waterShotClip;
    [SerializeField] private AudioClip waterHitClip;
    [SerializeField] private AudioClip pollinatorShotClip;
    [SerializeField] private AudioClip pollinatorHitClip;
    [SerializeField] private AudioClip rootsAttackClip;
    [SerializeField] private AudioClip condorShotClip;

    [Header("Voices")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource interfaceSource;
    [SerializeField] private AudioSource[] gameplaySources;

    [Header("Levels")]
    [SerializeField, Range(0f, 1f)]
    private float musicVolume = PlayerProgressCodec.DefaultMusicVolume;

    [SerializeField, Range(0f, 1f)]
    private float effectsVolume = PlayerProgressCodec.DefaultEffectsVolume;

    [SerializeField, Range(0f, 1f)]
    private float pausedMusicScale = 0.4f;

    [SerializeField, Min(1)]
    private int maxSimultaneousImpacts = 3;

    [SerializeField, Min(0.02f)]
    private float impactWindow = 0.08f;

    [SerializeField] private bool volumeDefaultsApplied;

    [Header("Dependencies")]
    [SerializeField] private PauseManager pauseManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private BastionHealth bastionHealth;
    [SerializeField] private CollectibleSpawner collectibleSpawner;
    [SerializeField] private EducationalCollectionManager collection;
    [SerializeField] private BuildPlot[] buildPlots;

    private static MatchAudio current;
    private static bool missingPlaybackReported;

    private readonly List<float> impactTimes = new();
    private int nextVoice;
    private bool resultPlayed;
    private bool subscribed;
    private bool missingPauseReported;
    private bool missingGameReported;
    private bool reportedWaterBuild;
    private bool reportedWaterShot;
    private bool reportedWaterHit;
    private bool reportedPollinatorShot;
    private bool reportedPollinatorHit;
    private bool reportedRootsAttack;
    private bool reportedCondorShot;

    public float MusicVolume => musicVolume;
    public float EffectsVolume => effectsVolume;

    public static void NotifyInterfaceClick()
    {
        if (current == null)
        {
            if (!missingPlaybackReported)
            {
                missingPlaybackReported = true;
                Debug.LogWarning(
                    "No MatchAudio is active. Interface clicks stay silent."
                );
            }

            return;
        }

        current.PlayInterface(current.interfaceClickClip);
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = PlayerProgressCodec.ClampVolume(value);
        ApplyMusicLevel();
    }

    public void SetEffectsVolume(float value)
    {
        effectsVolume = PlayerProgressCodec.ClampVolume(value);
        ApplyEffectsLevel();
    }

    private void Awake()
    {
        EnsureVoices();
        ConfigureSources();
    }

    private void OnEnable()
    {
        if (current != null && current != this)
        {
            Debug.LogWarning(
                "More than one MatchAudio is active. " +
                "Interface clicks use the latest one.",
                this
            );
        }

        current = this;
        Subscribe();
    }

    private void Start()
    {
        ApplyStoredPreferences();
        ApplyMusicLevel();
        ApplyEffectsLevel();
        BeginMusic();
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (current == this)
        {
            current = null;
        }
    }

    private void OnValidate()
    {
        musicVolume = PlayerProgressCodec.ClampVolume(musicVolume);
        effectsVolume = PlayerProgressCodec.ClampVolume(effectsVolume);
        pausedMusicScale = PlayerProgressCodec.ClampVolume(pausedMusicScale);

        if (maxSimultaneousImpacts < 1)
        {
            maxSimultaneousImpacts = 1;
        }

        if (impactWindow < 0.02f)
        {
            impactWindow = 0.02f;
        }
    }

    private void ApplyStoredPreferences()
    {
        if (collection == null)
        {
            return;
        }

        if (collection.HasStoredAudio)
        {
            musicVolume = collection.MusicPreference;
            effectsVolume = collection.EffectsPreference;
            return;
        }

        bool customized =
            !Mathf.Approximately(
                musicVolume,
                PlayerProgressCodec.DefaultMusicVolume) ||
            !Mathf.Approximately(
                effectsVolume,
                PlayerProgressCodec.DefaultEffectsVolume);

        if (customized)
        {
            collection.TrySaveAudioPreferences(
                musicVolume,
                effectsVolume
            );
        }
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        subscribed = true;
        TowerAttack.ConfirmedShot += HandleShot;
        Projectile.ConfirmedImpact += HandleImpact;

        if (pauseManager != null)
        {
            pauseManager.OnPauseStateChanged += HandlePause;
        }
        else if (!missingPauseReported)
        {
            missingPauseReported = true;
            Debug.LogWarning(
                "MatchAudio has no PauseManager. " +
                "Gameplay sounds stay silent.",
                this
            );
        }

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged += HandleGameState;
        }
        else if (!missingGameReported)
        {
            missingGameReported = true;
            Debug.LogWarning(
                "MatchAudio has no GameManager. " +
                "Result sounds stay silent.",
                this
            );
        }

        if (waveManager != null)
        {
            waveManager.OnWaveStateChanged += HandleWaveState;
            waveManager.OnEnemyResolved += HandleEnemyResolved;
        }

        if (bastionHealth != null)
        {
            bastionHealth.OnHealthChanged += HandleHeartDamaged;
        }

        if (collectibleSpawner != null)
        {
            collectibleSpawner.OnCollectibleCaptured +=
                HandleCapture;
        }

        if (buildPlots == null)
        {
            return;
        }

        for (int index = 0; index < buildPlots.Length; index++)
        {
            if (buildPlots[index] == null)
            {
                continue;
            }

            buildPlots[index].OnTowerBuilt -= HandleBuild;
            buildPlots[index].OnTowerBuilt += HandleBuild;
        }
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        subscribed = false;
        TowerAttack.ConfirmedShot -= HandleShot;
        Projectile.ConfirmedImpact -= HandleImpact;

        if (pauseManager != null)
        {
            pauseManager.OnPauseStateChanged -= HandlePause;
        }

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= HandleGameState;
        }

        if (waveManager != null)
        {
            waveManager.OnWaveStateChanged -= HandleWaveState;
            waveManager.OnEnemyResolved -= HandleEnemyResolved;
        }

        if (bastionHealth != null)
        {
            bastionHealth.OnHealthChanged -= HandleHeartDamaged;
        }

        if (collectibleSpawner != null)
        {
            collectibleSpawner.OnCollectibleCaptured -=
                HandleCapture;
        }

        if (buildPlots == null)
        {
            return;
        }

        for (int index = 0; index < buildPlots.Length; index++)
        {
            if (buildPlots[index] == null)
            {
                continue;
            }

            buildPlots[index].OnTowerBuilt -= HandleBuild;
        }
    }

    private void HandleBuild(TowerController tower)
    {
        if (IsWater(tower))
        {
            PlayWater(waterBuildClip, ref reportedWaterBuild, "sfx_build_water");
            return;
        }

        if (IsPollinator(tower) || IsRoots(tower) || IsCondor(tower))
        {
            return;
        }

        PlayGameplay(buildClip);
    }

    private void HandleShot(string towerId)
    {
        if (towerId == "tower_water")
        {
            PlayWater(waterShotClip, ref reportedWaterShot, "sfx_water_shot");
            return;
        }

        if (towerId == "tower_pollinator")
        {
            PlayWater(
                pollinatorShotClip,
                ref reportedPollinatorShot,
                "sfx_pollinator_shot"
            );
            return;
        }

        if (towerId == "tower_roots")
        {
            PlayWater(
                rootsAttackClip,
                ref reportedRootsAttack,
                "sfx_roots_attack"
            );
            return;
        }

        if (towerId == "tower_condor")
        {
            PlayWater(
                condorShotClip,
                ref reportedCondorShot,
                "sfx_condor_shot"
            );
            return;
        }

        PlayGameplay(shotClip);
    }

    private void HandleImpact(string towerId)
    {
        if (towerId == "tower_water")
        {
            if (!AllowImpact())
            {
                return;
            }

            PlayWater(waterHitClip, ref reportedWaterHit, "sfx_water_hit");
            return;
        }

        if (towerId == "tower_condor")
        {
            return;
        }

        if (towerId == "tower_pollinator")
        {
            if (!AllowImpact())
            {
                return;
            }

            PlayWater(
                pollinatorHitClip,
                ref reportedPollinatorHit,
                "sfx_pollinator_hit"
            );
            return;
        }

        if (impactClip == null || !CanPlayGameplay())
        {
            return;
        }

        if (!AllowImpact())
        {
            return;
        }

        PlayGameplay(impactClip);
    }

    private static bool IsWater(TowerController tower)
    {
        return tower != null &&
            tower.Data != null &&
            tower.Data.TowerId == "tower_water";
    }

    private static bool IsPollinator(TowerController tower)
    {
        return tower != null &&
            tower.Data != null &&
            tower.Data.TowerId == "tower_pollinator";
    }

    private static bool IsCondor(TowerController tower)
    {
        return tower != null &&
            tower.Data != null &&
            tower.Data.TowerId == "tower_condor";
    }

    private static bool IsRoots(TowerController tower)
    {
        return tower != null &&
            tower.Data != null &&
            tower.Data.TowerId == "tower_roots";
    }

    private void PlayWater(AudioClip clip, ref bool reported, string clipName)
    {
        if (clip == null)
        {
            if (!reported)
            {
                reported = true;
                Debug.LogWarning(
                    "Falta " + clipName +
                    ". El combate sigue, sin el sonido básico de esa acción.",
                    this
                );
            }

            return;
        }

        PlayGameplay(clip);
    }

    private void HandleEnemyResolved(EnemyFinishReason reason)
    {
        if (reason != EnemyFinishReason.Killed)
        {
            return;
        }

        PlayGameplay(enemyDefeatClip);
    }

    private void HandleHeartDamaged(int current, int max)
    {
        PlayGameplay(heartDamageClip);
    }

    private void HandleCapture(CollectibleData data)
    {
        if (data == null || resultPlayed)
        {
            return;
        }

        PlayInterface(captureClip);
    }

    private void HandleWaveState(WaveState state)
    {
        if (state != WaveState.Spawning)
        {
            return;
        }

        PlayGameplay(waveStartClip);
    }

    private void HandlePause(bool paused)
    {
        if (paused)
        {
            StopGameplay();
        }

        ApplyMusicLevel();
    }

    private void HandleGameState(GameState state)
    {
        if (state == GameState.Playing)
        {
            return;
        }

        StopGameplay();

        if (resultPlayed)
        {
            ApplyMusicLevel();
            return;
        }

        resultPlayed = true;
        ApplyMusicLevel();
        PlayInterface(
            state == GameState.Victory
                ? victoryClip
                : defeatClip
        );
    }

    private bool AllowImpact()
    {
        float now = Time.unscaledTime;
        int active = 0;

        for (int index = impactTimes.Count - 1; index >= 0; index--)
        {
            if (now - impactTimes[index] > impactWindow)
            {
                impactTimes.RemoveAt(index);
                continue;
            }

            active++;
        }

        if (active >= maxSimultaneousImpacts)
        {
            return false;
        }

        impactTimes.Add(now);
        return true;
    }

    private void PlayGameplay(AudioClip clip)
    {
        if (clip == null || !CanPlayGameplay())
        {
            return;
        }

        AudioSource source = NextGameplayVoice();

        if (source == null)
        {
            return;
        }

        source.Stop();
        source.spatialBlend = 0f;
        source.loop = false;
        source.playOnAwake = false;
        source.volume = effectsVolume;
        source.clip = clip;
        source.Play();
    }

    private void PlayInterface(AudioClip clip)
    {
        if (clip == null || interfaceSource == null)
        {
            return;
        }

        interfaceSource.spatialBlend = 0f;
        interfaceSource.loop = false;
        interfaceSource.playOnAwake = false;
        interfaceSource.volume = effectsVolume;
        interfaceSource.PlayOneShot(clip);
    }

    private bool CanPlayGameplay()
    {
        if (resultPlayed)
        {
            return false;
        }

        if (pauseManager == null || pauseManager.IsPaused)
        {
            return false;
        }

        return gameManager != null && gameManager.IsPlaying;
    }

    private void StopGameplay()
    {
        if (gameplaySources == null)
        {
            return;
        }

        for (int index = 0; index < gameplaySources.Length; index++)
        {
            AudioSource source = gameplaySources[index];

            if (source != null)
            {
                source.Stop();
            }
        }

        impactTimes.Clear();
    }

    private AudioSource NextGameplayVoice()
    {
        if (gameplaySources == null || gameplaySources.Length == 0)
        {
            return null;
        }

        for (int offset = 0; offset < gameplaySources.Length; offset++)
        {
            int index = (nextVoice + offset) % gameplaySources.Length;
            AudioSource source = gameplaySources[index];

            if (source != null && !source.isPlaying)
            {
                nextVoice = (index + 1) % gameplaySources.Length;
                return source;
            }
        }

        for (int index = 0; index < gameplaySources.Length; index++)
        {
            if (gameplaySources[index] != null)
            {
                nextVoice = (index + 1) % gameplaySources.Length;
                return gameplaySources[index];
            }
        }

        return null;
    }

    private void BeginMusic()
    {
        if (musicSource == null || musicClip == null)
        {
            return;
        }

        musicSource.spatialBlend = 0f;
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.dopplerLevel = 0f;

        if (musicSource.clip != musicClip)
        {
            musicSource.clip = musicClip;
        }

        ApplyMusicLevel();

        if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    private void ApplyMusicLevel()
    {
        if (musicSource == null)
        {
            return;
        }

        bool duck = resultPlayed ||
            (pauseManager != null && pauseManager.IsPaused);
        float scale = duck ? pausedMusicScale : 1f;
        musicSource.volume = musicVolume * scale;
    }

    private void ApplyEffectsLevel()
    {
        if (interfaceSource != null)
        {
            interfaceSource.volume = effectsVolume;
        }

        if (gameplaySources == null)
        {
            return;
        }

        for (int index = 0; index < gameplaySources.Length; index++)
        {
            if (gameplaySources[index] != null)
            {
                gameplaySources[index].volume = effectsVolume;
            }
        }
    }

    private void ConfigureSources()
    {
        ConfigureVoice(musicSource, true);
        ConfigureVoice(interfaceSource, false);

        if (gameplaySources == null)
        {
            return;
        }

        for (int index = 0; index < gameplaySources.Length; index++)
        {
            ConfigureVoice(gameplaySources[index], false);
        }
    }

    private static void ConfigureVoice(AudioSource source, bool loop)
    {
        if (source == null)
        {
            return;
        }

        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.loop = loop;
        source.dopplerLevel = 0f;
    }

    private void EnsureVoices()
    {
        if (musicSource == null)
        {
            musicSource = CreateVoice("MusicSource");
        }

        if (interfaceSource == null)
        {
            interfaceSource = CreateVoice("InterfaceSource");
        }

        if (gameplaySources == null || gameplaySources.Length == 0)
        {
            gameplaySources = new AudioSource[GameplayVoiceCount];
        }

        for (int index = 0; index < gameplaySources.Length; index++)
        {
            if (gameplaySources[index] == null)
            {
                gameplaySources[index] = CreateVoice(
                    "GameplayVoice_" + index
                );
            }
        }
    }

    private AudioSource CreateVoice(string voiceName)
    {
        Transform existing = transform.Find(voiceName);

        if (existing != null)
        {
            AudioSource found = existing.GetComponent<AudioSource>();

            if (found != null)
            {
                return found;
            }
        }

        GameObject voice = new GameObject(voiceName);
        voice.transform.SetParent(transform, false);
        return voice.AddComponent<AudioSource>();
    }
}
