using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AudioResponseConfigurator
{
    private const string MenuPath =
        "Bastion del Valle/Incrementos/18 - Integrar Audio y Respuesta";

    private const string ScenePath = "Assets/Scenes/Level_01.unity";
    private const string UndoName = "Integrar Audio y Respuesta";
    private const int GameplayVoices = 6;

    private const string TowerPath = "Assets/Prefabs/Tower_Basic.prefab";
    private const string EnemyPath = "Assets/Prefabs/Enemy_Basic.prefab";
    private const string CollectiblePath =
        "Assets/Prefabs/EducationalCollectible_Graybox.prefab";
    private const string CodexEntryPath = "Assets/Prefabs/CodexEntry.prefab";

    private static readonly ClipSlot[] Clips =
    {
        new ClipSlot("Assets/Audio/Music/music_level01.wav", "musicClip", true),
        new ClipSlot("Assets/Audio/SFX/sfx_build.wav", "buildClip", false),
        new ClipSlot("Assets/Audio/SFX/sfx_seed_shot.wav", "shotClip", false),
        new ClipSlot("Assets/Audio/SFX/sfx_seed_hit.wav", "impactClip", false),
        new ClipSlot("Assets/Audio/SFX/sfx_enemy_defeat.wav", "enemyDefeatClip", false),
        new ClipSlot("Assets/Audio/SFX/sfx_heart_damage.wav", "heartDamageClip", false),
        new ClipSlot("Assets/Audio/SFX/sfx_memory_capture.wav", "captureClip", false),
        new ClipSlot("Assets/Audio/SFX/sfx_wave_start.wav", "waveStartClip", false),
        new ClipSlot("Assets/Audio/SFX/sfx_victory.wav", "victoryClip", false),
        new ClipSlot("Assets/Audio/SFX/sfx_defeat.wav", "defeatClip", false),
        new ClipSlot("Assets/Audio/SFX/sfx_ui_click.wav", "interfaceClickClip", false)
    };

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenu()
    {
        return !IsPlayMode() && !EditorApplication.isCompiling;
    }

    [MenuItem(MenuPath)]
    private static void Execute()
    {
        if (IsPlayMode() || EditorApplication.isCompiling)
        {
            Report(
                "Detenido",
                "Sal de Play Mode y espera a que termine la compilación."
            );
            return;
        }

        if (!TryGetOpenScene(out Scene scene, out string error))
        {
            Report("Detenido", error);
            return;
        }

        if (!TryInspect(scene, out string inspectionError))
        {
            Report("Detenido", inspectionError);
            return;
        }

        List<LoadedPrefab> loaded = new List<LoadedPrefab>();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);

        try
        {
            OpenPrefabs(loaded);
            List<string> notes = new List<string>();
            bool sceneChanged = ApplyScene(scene, notes);
            bool prefabsChanged = EditPrefabs(loaded, notes);
            SavePrefabs(loaded, notes);
            ConfigureImports(notes);
            Undo.CollapseUndoOperations(group);

            if (sceneChanged || prefabsChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            Report(
                "Audio y respuesta",
                BuildReport(notes, sceneChanged, prefabsChanged)
            );
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(exception);
            Report(
                "Detenido",
                "La configuración de la escena se revirtió.\n" +
                exception.Message
            );
        }
        finally
        {
            ClosePrefabs(loaded);
        }
    }

    private static bool ApplyScene(Scene scene, List<string> notes)
    {
        bool changed = false;
        GameManager gameManager = One<GameManager>(scene);
        PauseManager pauseManager = One<PauseManager>(scene);
        WaveManager waveManager = One<WaveManager>(scene);
        BastionHealth bastion = One<BastionHealth>(scene);
        CollectibleSpawner spawner = One<CollectibleSpawner>(scene);
        EducationalCollectionManager collection =
            One<EducationalCollectionManager>(scene);
        EducationalModalController modal =
            One<EducationalModalController>(scene);
        CodexController codex = One<CodexController>(scene);
        HUDController hud = One<HUDController>(scene);
        Canvas canvas = hud.GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            throw new InvalidOperationException(
                "El HUD no está dentro de un Canvas."
            );
        }

        MatchAudio audio = EnsureMatchAudio(scene, notes, ref changed);
        EnsureVoices(audio, notes, ref changed);
        AssignClips(audio, notes, ref changed);
        AssignDependencies(
            audio,
            gameManager,
            pauseManager,
            waveManager,
            bastion,
            spawner,
            collection,
            SceneComponents<BuildPlot>(scene),
            ref changed
        );
        EnsureListener(scene, notes, ref changed);
        EnsureHeartPulse(bastion, notes, ref changed);
        EnsureSettings(
            canvas,
            hud,
            audio,
            gameManager,
            pauseManager,
            modal,
            codex,
            collection,
            notes,
            ref changed
        );
        AssignCodex(codex, notes, ref changed);
        EnsureSceneClicks(scene, notes, ref changed);
        var compact = new StringBuilder();
        CompactMatchHud.Apply(scene, compact);
        notes.Add(compact.ToString());
        return changed;
    }

    private static MatchAudio EnsureMatchAudio(
        Scene scene,
        List<string> notes,
        ref bool changed)
    {
        List<MatchAudio> found = SceneComponents<MatchAudio>(scene);

        if (found.Count == 1)
        {
            notes.Add("MatchAudio reutilizado.");
            return found[0];
        }

        GameObject host = new GameObject("MatchAudio");
        SceneManager.MoveGameObjectToScene(host, scene);
        Undo.RegisterCreatedObjectUndo(host, UndoName);
        MatchAudio audio = Undo.AddComponent<MatchAudio>(host);
        notes.Add("MatchAudio creado.");
        changed = true;
        return audio;
    }

    private static void EnsureVoices(
        MatchAudio audio,
        List<string> notes,
        ref bool changed)
    {
        SerializedObject serialized = new SerializedObject(audio);
        AudioSource music = EnsureSource(
            audio.transform,
            serialized.FindProperty("musicSource"),
            "MusicSource",
            true,
            ref changed
        );
        AudioSource interfaceSource = EnsureSource(
            audio.transform,
            serialized.FindProperty("interfaceSource"),
            "InterfaceSource",
            false,
            ref changed
        );
        SerializedProperty voices = serialized.FindProperty("gameplaySources");

        if (voices.arraySize < GameplayVoices)
        {
            voices.arraySize = GameplayVoices;
            changed = true;
        }

        for (int index = 0; index < voices.arraySize; index++)
        {
            EnsureSource(
                audio.transform,
                voices.GetArrayElementAtIndex(index),
                "GameplayVoice_" + index,
                false,
                ref changed
            );
        }

        EnforceVoicePolicy(music, true, ref changed);
        EnforceVoicePolicy(interfaceSource, false, ref changed);

        for (int index = 0; index < voices.arraySize; index++)
        {
            AudioSource source = voices
                .GetArrayElementAtIndex(index)
                .objectReferenceValue as AudioSource;
            EnforceVoicePolicy(source, false, ref changed);
        }

        SerializedProperty defaults =
            serialized.FindProperty("volumeDefaultsApplied");

        if (!defaults.boolValue)
        {
            serialized.FindProperty("musicVolume").floatValue =
                PlayerProgressCodec.DefaultMusicVolume;
            serialized.FindProperty("effectsVolume").floatValue =
                PlayerProgressCodec.DefaultEffectsVolume;
            serialized.FindProperty("pausedMusicScale").floatValue = 0.4f;
            serialized.FindProperty("maxSimultaneousImpacts").intValue = 3;
            serialized.FindProperty("impactWindow").floatValue = 0.08f;
            defaults.boolValue = true;

            if (music != null)
            {
                Undo.RecordObject(music, UndoName);
                music.volume = PlayerProgressCodec.DefaultMusicVolume;
            }

            if (interfaceSource != null)
            {
                Undo.RecordObject(interfaceSource, UndoName);
                interfaceSource.volume =
                    PlayerProgressCodec.DefaultEffectsVolume;
            }

            notes.Add(
                "Volúmenes iniciales: música " +
                PlayerProgressCodec.DefaultMusicVolume.ToString("0.00") +
                ", efectos " +
                PlayerProgressCodec.DefaultEffectsVolume.ToString("0.00") +
                ". La música baja al 40 % durante la pausa."
            );
            changed = true;
        }
        else
        {
            notes.Add(
                "Volúmenes y límites ya configurados: se conservan."
            );
        }

        serialized.ApplyModifiedProperties();
    }

    private static AudioSource EnsureSource(
        Transform parent,
        SerializedProperty property,
        string voiceName,
        bool loop,
        ref bool changed)
    {
        AudioSource source = property.objectReferenceValue as AudioSource;

        if (source != null)
        {
            return source;
        }

        Transform existing = parent.Find(voiceName);

        if (existing != null)
        {
            source = existing.GetComponent<AudioSource>();
        }

        if (source == null)
        {
            GameObject voice = new GameObject(voiceName);
            SceneManager.MoveGameObjectToScene(
                voice,
                parent.gameObject.scene
            );
            Undo.RegisterCreatedObjectUndo(voice, UndoName);
            Undo.SetTransformParent(voice.transform, parent, UndoName);
            source = Undo.AddComponent<AudioSource>(voice);
            changed = true;
        }

        property.objectReferenceValue = source;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        changed = true;
        return source;
    }

    private static void EnforceVoicePolicy(
        AudioSource source,
        bool loop,
        ref bool changed)
    {
        if (source == null)
        {
            return;
        }

        if (Mathf.Approximately(source.spatialBlend, 0f) &&
            source.playOnAwake == false &&
            source.loop == loop &&
            Mathf.Approximately(source.dopplerLevel, 0f))
        {
            return;
        }

        Undo.RecordObject(source, UndoName);
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.loop = loop;
        source.dopplerLevel = 0f;
        changed = true;
    }

    private static void AssignClips(
        MatchAudio audio,
        List<string> notes,
        ref bool changed)
    {
        SerializedObject serialized = new SerializedObject(audio);

        foreach (ClipSlot slot in Clips)
        {
            SerializedProperty property =
                serialized.FindProperty(slot.Property);

            if (!ClipExists(slot.Path))
            {
                notes.Add(
                    "Falta " + slot.Path +
                    ". La partida puede continuar sin ese audio."
                );
                continue;
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                slot.Path
            );

            if (clip == null)
            {
                notes.Add(
                    slot.Path + " no se importó como AudioClip."
                );
                continue;
            }

            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = clip;
                notes.Add("Asignado " + Path.GetFileName(slot.Path) + ".");
                changed = true;
            }
        }

        serialized.ApplyModifiedProperties();
    }

    private static void AssignDependencies(
        MatchAudio audio,
        GameManager gameManager,
        PauseManager pauseManager,
        WaveManager waveManager,
        BastionHealth bastion,
        CollectibleSpawner spawner,
        EducationalCollectionManager collection,
        List<BuildPlot> plots,
        ref bool changed)
    {
        SerializedObject serialized = new SerializedObject(audio);
        changed |= Assign(serialized, "gameManager", gameManager);
        changed |= Assign(serialized, "pauseManager", pauseManager);
        changed |= Assign(serialized, "waveManager", waveManager);
        changed |= Assign(serialized, "bastionHealth", bastion);
        changed |= Assign(serialized, "collectibleSpawner", spawner);
        changed |= Assign(serialized, "collection", collection);

        SerializedProperty plotProperty =
            serialized.FindProperty("buildPlots");
        HashSet<BuildPlot> present = new HashSet<BuildPlot>();

        for (int index = 0; index < plotProperty.arraySize; index++)
        {
            BuildPlot plot = plotProperty
                .GetArrayElementAtIndex(index)
                .objectReferenceValue as BuildPlot;

            if (plot != null)
            {
                present.Add(plot);
            }
        }

        foreach (BuildPlot plot in plots)
        {
            if (present.Contains(plot))
            {
                continue;
            }

            int index = plotProperty.arraySize;
            plotProperty.InsertArrayElementAtIndex(index);
            plotProperty.GetArrayElementAtIndex(index)
                .objectReferenceValue = plot;
            changed = true;
        }

        serialized.ApplyModifiedProperties();
    }

    private static void EnsureListener(
        Scene scene,
        List<string> notes,
        ref bool changed)
    {
        if (SceneComponents<AudioListener>(scene).Count > 0)
        {
            notes.Add("AudioListener existente conservado.");
            return;
        }

        Camera camera = SceneComponents<Camera>(scene)[0];
        Undo.AddComponent<AudioListener>(camera.gameObject);
        notes.Add(
            "AudioListener añadido a " + camera.gameObject.name + "."
        );
        changed = true;
    }

    private static void EnsureHeartPulse(
        BastionHealth bastion,
        List<string> notes,
        ref bool changed)
    {
        HeartDamagePulse pulse =
            bastion.GetComponent<HeartDamagePulse>();

        if (pulse == null)
        {
            pulse = Undo.AddComponent<HeartDamagePulse>(bastion.gameObject);
            changed = true;
            notes.Add("Pulso del corazón añadido.");
        }

        SpriteRenderer renderer = SelectChildRenderer(bastion.gameObject);
        SerializedObject serialized = new SerializedObject(pulse);
        changed |= Assign(serialized, "worldRenderer", renderer);
        changed |= Assign(serialized, "health", bastion);
        serialized.ApplyModifiedProperties();

        if (ChildRenderers(bastion.gameObject).Count > 1)
        {
            notes.Add(
                "El corazón tiene varios hijos visuales. " +
                "No se eligió el sprite del pulso."
            );
        }
        else if (renderer == null)
        {
            notes.Add(
                "El corazón no tiene un hijo visual. El pulso queda sin sprite."
            );
        }
    }

    private static void EnsureSettings(
        Canvas canvas,
        HUDController hud,
        MatchAudio audio,
        GameManager gameManager,
        PauseManager pauseManager,
        EducationalModalController modal,
        CodexController codex,
        EducationalCollectionManager collection,
        List<string> notes,
        ref bool changed)
    {
        List<AudioSettingsPanel> panels =
            SceneComponents<AudioSettingsPanel>(canvas.gameObject.scene);
        AudioSettingsPanel panel;
        TMP_FontAsset font = FindFont();

        if (panels.Count == 1)
        {
            panel = panels[0];
            notes.Add("Panel de sonido reutilizado.");
        }
        else
        {
            panel = CreateSettings(canvas, font, notes, ref changed);
        }

        Button openButton = EnsureOpenButton(
            hud,
            codex,
            font,
            notes,
            ref changed
        );
        PlaceSoundPanel(panel, ref changed);
        WireSettings(
            panel,
            openButton,
            audio,
            gameManager,
            pauseManager,
            modal,
            codex,
            collection,
            ref changed
        );
        EnsureSliderCommits(panel, ref changed);
        PlaceAboveHud(panel.transform, hud.transform, ref changed);
    }

    private static AudioSettingsPanel CreateSettings(
        Canvas canvas,
        TMP_FontAsset font,
        List<string> notes,
        ref bool changed)
    {
        GameObject root = CreateUi(
            "AudioSettings",
            canvas.transform,
            canvas.gameObject.scene
        );
        AudioSettingsPanel panel =
            Undo.AddComponent<AudioSettingsPanel>(root);
        GameObject window = CreateUi(
            "AudioPanel",
            root.transform,
            canvas.gameObject.scene
        );
        window.SetActive(false);
        Stretch(root.GetComponent<RectTransform>());
        Stretch(window.GetComponent<RectTransform>());

        GameObject dimmer = CreateImage(
            "AudioDimmer",
            window.transform,
            canvas.gameObject.scene,
            new Color(0.149f, 0.239f, 0.188f, 0.45f),
            true
        );
        Stretch(dimmer.GetComponent<RectTransform>());

        GameObject card = CreateImage(
            "AudioCard",
            window.transform,
            canvas.gameObject.scene,
            new Color(0.149f, 0.239f, 0.188f, 0.98f),
            true
        );
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(640f, 460f);
        cardRect.localScale = Vector3.one;
        cardRect.localRotation = Quaternion.identity;

        CreateLabel(
            "AudioTitle",
            card.transform,
            canvas.gameObject.scene,
            font,
            "Sonido",
            36f,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -28f),
            new Vector2(560f, 52f)
        );
        CreateLabel(
            "MusicVolumeLabel",
            card.transform,
            canvas.gameObject.scene,
            font,
            "Música",
            28f,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -110f),
            new Vector2(520f, 40f)
        );
        Slider music = CreateSlider(
            "MusicVolumeSlider",
            card.transform,
            canvas.gameObject.scene,
            new Vector2(0f, -168f),
            PlayerProgressCodec.DefaultMusicVolume
        );
        CreateLabel(
            "EffectsVolumeLabel",
            card.transform,
            canvas.gameObject.scene,
            font,
            "Efectos",
            28f,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -230f),
            new Vector2(520f, 40f)
        );
        Slider effects = CreateSlider(
            "EffectsVolumeSlider",
            card.transform,
            canvas.gameObject.scene,
            new Vector2(0f, -288f),
            PlayerProgressCodec.DefaultEffectsVolume
        );
        Button close = CreateButton(
            "AudioCloseButton",
            card.transform,
            canvas.gameObject.scene,
            font,
            "Cerrar",
            new Vector2(0.5f, 0f),
            new Vector2(0f, 36f),
            new Vector2(220f, 52f)
        );

        SerializedObject serialized = new SerializedObject(panel);
        serialized.FindProperty("panelRoot").objectReferenceValue = window;
        serialized.FindProperty("closeButton").objectReferenceValue = close;
        serialized.FindProperty("musicSlider").objectReferenceValue = music;
        serialized.FindProperty("effectsSlider").objectReferenceValue = effects;
        serialized.ApplyModifiedProperties();
        notes.Add("Panel de sonido creado.");
        changed = true;
        return panel;
    }

    private static Button EnsureOpenButton(
        HUDController hud,
        CodexController codex,
        TMP_FontAsset font,
        List<string> notes,
        ref bool changed)
    {
        Button button = null;

        foreach (Button candidate in SceneComponents<Button>(
            hud.gameObject.scene))
        {
            if (candidate.gameObject.name == "AudioButton")
            {
                button = candidate;
                break;
            }
        }

        if (button == null)
        {
            button = CreateButton(
                "AudioButton",
                hud.transform,
                hud.gameObject.scene,
                font,
                "Sonido",
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(220f, 56f)
            );
            notes.Add("Botón Sonido creado.");
            changed = true;
        }
        else
        {
            notes.Add("Botón Sonido reubicado.");
        }

        PlaceSoundButton(button, codex, ref changed);
        return button;
    }

    private static void PlaceSoundButton(
        Button button,
        CodexController codex,
        ref bool changed)
    {
        SerializedObject codexObject = new SerializedObject(codex);
        Button codexButton = codexObject
            .FindProperty("openButton")
            .objectReferenceValue as Button;

        if (codexButton == null)
        {
            return;
        }

        RectTransform source =
            codexButton.GetComponent<RectTransform>();
        RectTransform rect = button.GetComponent<RectTransform>();

        if (rect.parent != source.parent)
        {
            Undo.SetTransformParent(rect, source.parent, UndoName);
            rect.SetParent(source.parent, false);
        }

        Undo.RecordObject(rect, UndoName);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;

        bool stretched =
            Mathf.Abs(source.anchorMax.x - source.anchorMin.x) > 0.02f;

        if (stretched)
        {
            float width = source.anchorMax.x - source.anchorMin.x;
            float gap = 0.008f;
            float max = source.anchorMin.x - gap;
            rect.anchorMin = new Vector2(
                Mathf.Max(0.01f, max - width),
                source.anchorMin.y
            );
            rect.anchorMax = new Vector2(max, source.anchorMax.y);
            rect.anchoredPosition = new Vector2(
                0f,
                source.anchoredPosition.y
            );
            rect.sizeDelta = new Vector2(0f, source.sizeDelta.y);
        }
        else
        {
            Vector2 size = new Vector2(220f, source.sizeDelta.y);
            float left = source.anchoredPosition.x -
                source.sizeDelta.x * source.pivot.x;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(
                left - 16f - size.x * rect.pivot.x,
                source.anchoredPosition.y
            );
        }

        changed = true;
    }

    private static void PlaceSoundPanel(
        AudioSettingsPanel panel,
        ref bool changed)
    {
        RectTransform root = panel.GetComponent<RectTransform>();
        FitToParent(root, ref changed);

        Transform window = FindNamed(panel.transform, "AudioPanel");

        if (window is RectTransform windowRect)
        {
            FitToParent(windowRect, ref changed);
        }

        Transform dimmer = FindNamed(panel.transform, "AudioDimmer");

        if (dimmer is RectTransform dimmerRect)
        {
            FitToParent(dimmerRect, ref changed);
        }

        Transform card = FindNamed(panel.transform, "AudioCard");

        if (card is RectTransform cardRect)
        {
            Undo.RecordObject(cardRect, UndoName);
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(640f, 460f);
            cardRect.localScale = Vector3.one;
            cardRect.localRotation = Quaternion.identity;
            changed = true;
        }
    }

    private static void FitToParent(RectTransform rect, ref bool changed)
    {
        if (rect == null)
        {
            return;
        }

        Undo.RecordObject(rect, UndoName);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        changed = true;
    }

    private static void WireSettings(
        AudioSettingsPanel panel,
        Button openButton,
        MatchAudio audio,
        GameManager gameManager,
        PauseManager pauseManager,
        EducationalModalController modal,
        CodexController codex,
        EducationalCollectionManager collection,
        ref bool changed)
    {
        SerializedObject serialized = new SerializedObject(panel);
        changed |= Assign(serialized, "openButton", openButton);
        changed |= Assign(serialized, "matchAudio", audio);
        changed |= Assign(serialized, "gameManager", gameManager);
        changed |= Assign(serialized, "pauseManager", pauseManager);
        changed |= Assign(serialized, "educationalModal", modal);
        changed |= Assign(serialized, "codex", codex);
        changed |= Assign(serialized, "collection", collection);
        changed |= AssignChild(
            serialized,
            panel.transform,
            "closeButton",
            "AudioCloseButton"
        );
        changed |= AssignChild(
            serialized,
            panel.transform,
            "musicSlider",
            "MusicVolumeSlider"
        );
        changed |= AssignChild(
            serialized,
            panel.transform,
            "effectsSlider",
            "EffectsVolumeSlider"
        );
        changed |= AssignChildObject(
            serialized,
            panel.transform,
            "panelRoot",
            "AudioPanel"
        );
        serialized.ApplyModifiedProperties();
    }

    private static void EnsureSliderCommits(
        AudioSettingsPanel panel,
        ref bool changed)
    {
        SerializedObject serialized = new SerializedObject(panel);
        CommitSlider(serialized.FindProperty("musicSlider"), panel, ref changed);
        CommitSlider(serialized.FindProperty("effectsSlider"), panel, ref changed);
    }

    private static void CommitSlider(
        SerializedProperty property,
        AudioSettingsPanel panel,
        ref bool changed)
    {
        Slider slider = property.objectReferenceValue as Slider;

        if (slider == null)
        {
            return;
        }

        AudioSliderCommit commit =
            slider.GetComponent<AudioSliderCommit>();

        if (commit == null)
        {
            commit = Undo.AddComponent<AudioSliderCommit>(slider.gameObject);
            changed = true;
        }

        SerializedObject serialized = new SerializedObject(commit);
        changed |= Assign(serialized, "panel", panel);
        serialized.ApplyModifiedProperties();
    }

    private static void PlaceAboveHud(
        Transform panel,
        Transform hud,
        ref bool changed)
    {
        if (panel.parent != hud.parent || panel.parent == null)
        {
            return;
        }

        if (panel.GetSiblingIndex() > hud.GetSiblingIndex())
        {
            return;
        }

        Undo.RecordObject(panel, UndoName);
        panel.SetSiblingIndex(hud.GetSiblingIndex() + 1);
        changed = true;
    }

    private static void AssignCodex(
        CodexController codex,
        List<string> notes,
        ref bool changed)
    {
        AudioSettingsPanel panel =
            SceneComponents<AudioSettingsPanel>(
                codex.gameObject.scene
            )[0];
        SerializedObject serialized = new SerializedObject(codex);

        if (Assign(serialized, "audioSettings", panel))
        {
            notes.Add("El códice queda cerrado mientras el sonido está abierto.");
            changed = true;
        }

        serialized.ApplyModifiedProperties();
    }

    private static void EnsureSceneClicks(
        Scene scene,
        List<string> notes,
        ref bool changed)
    {
        int added = 0;

        foreach (Button button in SceneComponents<Button>(scene))
        {
            if (button.GetComponent<UiClickSound>() != null)
            {
                continue;
            }

            Undo.AddComponent<UiClickSound>(button.gameObject);

            if (PrefabUtility.IsPartOfPrefabInstance(button))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    button.gameObject
                );
            }

            added++;
            changed = true;
        }

        if (added > 0)
        {
            notes.Add(
                "Clic de interfaz añadido a " + added + " botones de la escena."
            );
        }
    }

    private static void OpenPrefabs(List<LoadedPrefab> loaded)
    {
        string[] paths =
        {
            TowerPath,
            EnemyPath,
            CollectiblePath,
            CodexEntryPath
        };

        try
        {
            foreach (string path in paths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                loaded.Add(new LoadedPrefab(path, root));
            }
        }
        catch
        {
            ClosePrefabs(loaded);
            throw;
        }
    }

    private static bool EditPrefabs(
        List<LoadedPrefab> loaded,
        List<string> notes)
    {
        bool changed = false;

        foreach (LoadedPrefab prefab in loaded)
        {
            if (prefab.Path == TowerPath)
            {
                changed |= EditTower(prefab, notes);
            }
            else if (prefab.Path == EnemyPath)
            {
                changed |= EditEnemy(prefab, notes);
            }
            else if (prefab.Path == CollectiblePath)
            {
                changed |= EditCollectible(prefab, notes);
            }
            else if (prefab.Path == CodexEntryPath)
            {
                changed |= EditCodexEntry(prefab, notes);
            }
        }

        return changed;
    }

    private static bool EditTower(LoadedPrefab prefab, List<string> notes)
    {
        TowerController controller =
            prefab.Root.GetComponent<TowerController>();
        TowerAppearance appearance =
            prefab.Root.GetComponent<TowerAppearance>();
        bool changed = false;

        if (appearance == null)
        {
            appearance = prefab.Root.AddComponent<TowerAppearance>();
            changed = true;
        }

        SpriteRenderer renderer = PreferredRenderer(
            prefab.Root,
            ReadRenderer(controller, "worldRenderer"),
            false
        );
        SerializedObject serialized = new SerializedObject(appearance);

        if (renderer != null &&
            Assign(serialized, "visual", renderer.transform))
        {
            changed = true;
        }
        else if (renderer == null)
        {
            notes.Add(
                "La torre no tiene un hijo visual. " +
                "La aparición no escala la raíz."
            );
        }

        serialized.ApplyModifiedProperties();
        prefab.Changed |= changed;

        if (changed)
        {
            notes.Add("Aparición de torre preparada en el prefab.");
        }

        return changed;
    }

    private static bool EditEnemy(LoadedPrefab prefab, List<string> notes)
    {
        EnemyMovement movement =
            prefab.Root.GetComponent<EnemyMovement>();
        EnemyHealth health = prefab.Root.GetComponent<EnemyHealth>();
        EnemyDamageFlash flash =
            prefab.Root.GetComponent<EnemyDamageFlash>();
        bool changed = false;

        if (flash == null)
        {
            flash = prefab.Root.AddComponent<EnemyDamageFlash>();
            changed = true;
        }

        SpriteRenderer renderer = PreferredRenderer(
            prefab.Root,
            ReadRenderer(movement, "worldRenderer"),
            true
        );
        SerializedObject serialized = new SerializedObject(flash);
        changed |= Assign(serialized, "worldRenderer", renderer);
        changed |= Assign(serialized, "health", health);
        serialized.ApplyModifiedProperties();
        prefab.Changed |= changed;

        if (renderer == null)
        {
            notes.Add("El enemigo no tiene sprite para el destello.");
        }
        else if (changed)
        {
            notes.Add("Destello de impacto preparado en el enemigo.");
        }

        return changed;
    }

    private static bool EditCollectible(
        LoadedPrefab prefab,
        List<string> notes)
    {
        EducationalCollectible collectible =
            prefab.Root.GetComponent<EducationalCollectible>();
        CollectibleVisualMotion motion =
            prefab.Root.GetComponent<CollectibleVisualMotion>();
        CaptureConfirmation confirmation =
            prefab.Root.GetComponent<CaptureConfirmation>();
        bool changed = false;

        if (confirmation == null)
        {
            confirmation = prefab.Root.AddComponent<CaptureConfirmation>();
            changed = true;
        }

        SpriteRenderer renderer = PreferredRenderer(
            prefab.Root,
            ReadRenderer(collectible, "worldRenderer"),
            true
        );
        SerializedObject serialized = new SerializedObject(confirmation);
        changed |= Assign(serialized, "worldRenderer", renderer);
        changed |= Assign(serialized, "collectible", collectible);
        changed |= Assign(serialized, "visualMotion", motion);
        serialized.ApplyModifiedProperties();
        prefab.Changed |= changed;

        if (changed)
        {
            notes.Add("Confirmación de captura preparada.");
        }

        return changed;
    }

    private static bool EditCodexEntry(
        LoadedPrefab prefab,
        List<string> notes)
    {
        bool changed = false;

        foreach (Button button in prefab.Root.GetComponentsInChildren<Button>(true))
        {
            if (button.GetComponent<UiClickSound>() != null)
            {
                continue;
            }

            button.gameObject.AddComponent<UiClickSound>();
            changed = true;
        }

        prefab.Changed |= changed;

        if (changed)
        {
            notes.Add("Clic de interfaz añadido a las filas del códice.");
        }

        return changed;
    }

    private static void SavePrefabs(
        List<LoadedPrefab> loaded,
        List<string> notes)
    {
        foreach (LoadedPrefab prefab in loaded)
        {
            if (!prefab.Changed)
            {
                continue;
            }

            PrefabUtility.SaveAsPrefabAsset(prefab.Root, prefab.Path);
            notes.Add("Prefab guardado: " + prefab.Path);
        }
    }

    private static void ClosePrefabs(List<LoadedPrefab> loaded)
    {
        for (int index = loaded.Count - 1; index >= 0; index--)
        {
            if (loaded[index].Root != null)
            {
                PrefabUtility.UnloadPrefabContents(loaded[index].Root);
            }
        }

        loaded.Clear();
    }

    private static void ConfigureImports(List<string> notes)
    {
        foreach (ClipSlot slot in Clips)
        {
            if (!ClipExists(slot.Path))
            {
                continue;
            }

            AudioImporter importer =
                AssetImporter.GetAtPath(slot.Path) as AudioImporter;

            if (importer == null)
            {
                notes.Add("Sin importador de audio: " + slot.Path);
                continue;
            }

            AudioImporterSampleSettings settings =
                importer.defaultSampleSettings;
            AudioClipLoadType loadType = slot.Music
                ? AudioClipLoadType.Streaming
                : AudioClipLoadType.DecompressOnLoad;
            AudioCompressionFormat format = slot.Music
                ? AudioCompressionFormat.Vorbis
                : AudioCompressionFormat.ADPCM;
            bool preload = !slot.Music;
            bool profileMatches =
                settings.loadType == loadType &&
                settings.compressionFormat == format &&
                settings.preloadAudioData == preload &&
                importer.loadInBackground == slot.Music;

            if (profileMatches)
            {
                continue;
            }

            settings.loadType = loadType;
            settings.compressionFormat = format;
            settings.preloadAudioData = preload;
            settings.sampleRateSetting =
                AudioSampleRateSetting.PreserveSampleRate;

            if (slot.Music && settings.quality <= 0f)
            {
                settings.quality = 0.6f;
            }

            importer.defaultSampleSettings = settings;
            importer.loadInBackground = slot.Music;
            importer.forceToMono = !slot.Music;
            importer.ambisonic = false;
            try
            {
                importer.SaveAndReimport();
            }
            catch (Exception exception)
            {
                notes.Add(
                    Path.GetFileName(slot.Path) +
                    " no pudo reimportarse: " + exception.Message
                );
                continue;
            }

            notes.Add(
                Path.GetFileName(slot.Path) + ": " +
                (slot.Music
                    ? "streaming Vorbis para reproducción prolongada."
                    : "ADPCM descomprimido para un efecto corto.")
            );
        }
    }

    private static bool TryInspect(Scene scene, out string error)
    {
        List<string> errors = new List<string>();
        RequireOne<GameManager>(scene, "GameManager", errors);
        RequireOne<PauseManager>(scene, "PauseManager", errors);
        RequireOne<WaveManager>(scene, "WaveManager", errors);
        RequireOne<BastionHealth>(scene, "BastionHealth", errors);
        RequireOne<CollectibleSpawner>(scene, "CollectibleSpawner", errors);
        RequireOne<EducationalCollectionManager>(
            scene,
            "EducationalCollectionManager",
            errors
        );
        RequireOne<EducationalModalController>(
            scene,
            "EducationalModalController",
            errors
        );
        RequireOne<CodexController>(scene, "CodexController", errors);
        HUDController hud = RequireOne<HUDController>(
            scene,
            "HUDController",
            errors
        );

        if (hud != null && hud.GetComponentInParent<Canvas>() == null)
        {
            errors.Add("El HUD no tiene Canvas.");
        }

        if (SceneComponents<BuildPlot>(scene).Count == 0)
        {
            errors.Add("Level_01 no tiene parcelas.");
        }

        if (SceneComponents<MatchAudio>(scene).Count > 1)
        {
            errors.Add("Hay más de un MatchAudio.");
        }

        if (SceneComponents<AudioSettingsPanel>(scene).Count > 1)
        {
            errors.Add("Hay más de un panel de sonido.");
        }

        InspectCameras(scene, errors);
        InspectPrefabFiles(errors);

        if (errors.Count == 0)
        {
            error = null;
            return true;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("No se modificó la escena.");

        foreach (string item in errors)
        {
            builder.AppendLine(item);
        }

        error = builder.ToString();
        return false;
    }

    private static void InspectCameras(Scene scene, List<string> errors)
    {
        List<Camera> cameras = SceneComponents<Camera>(scene);
        List<AudioListener> listeners =
            SceneComponents<AudioListener>(scene);

        if (listeners.Count > 0)
        {
            return;
        }

        if (cameras.Count != 1)
        {
            errors.Add(
                "No hay AudioListener y hay " + cameras.Count +
                " cámaras. No se eligió una."
            );
        }
    }

    private static void InspectPrefabFiles(List<string> errors)
    {
        string[] paths =
        {
            TowerPath,
            EnemyPath,
            CollectiblePath,
            CodexEntryPath
        };

        foreach (string path in paths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                errors.Add("No está el prefab " + path + ".");
            }
        }
    }

    private static string BuildReport(
        List<string> notes,
        bool sceneChanged,
        bool prefabsChanged)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(
            sceneChanged
                ? "La escena quedó modificada. Guárdala con Cmd+S. No se guardó sola."
                : "La escena no necesitó cambios nuevos."
        );

        if (prefabsChanged)
        {
            builder.AppendLine(
                "Los prefabs modificados sí se guardaron para la build."
            );
        }

        builder.AppendLine(
            "Voces 2D: 1 música, 1 interfaz y 6 de gameplay. " +
            "Los impactos simultáneos se limitan a 3."
        );

        foreach (string note in notes)
        {
            builder.AppendLine(note);
        }

        if (!ContainsMissing(notes))
        {
            builder.AppendLine("No faltan los clips previstos.");
        }

        return builder.ToString();
    }

    private static bool ContainsMissing(List<string> notes)
    {
        foreach (string note in notes)
        {
            if (note.StartsWith("Falta ", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static T RequireOne<T>(
        Scene scene,
        string label,
        List<string> errors) where T : Component
    {
        List<T> found = SceneComponents<T>(scene);

        if (found.Count == 1)
        {
            return found[0];
        }

        errors.Add(label + ": se encontraron " + found.Count + ".");
        return null;
    }

    private static T One<T>(Scene scene) where T : Component
    {
        return SceneComponents<T>(scene)[0];
    }

    private static List<T> SceneComponents<T>(Scene scene)
        where T : Component
    {
        List<T> result = new List<T>();

        foreach (T item in UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            if (item == null ||
                EditorUtility.IsPersistent(item) ||
                item.gameObject.scene != scene)
            {
                continue;
            }

            result.Add(item);
        }

        return result;
    }

    private static bool Assign(
        SerializedObject serialized,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null ||
            value == null ||
            property.objectReferenceValue != null)
        {
            return false;
        }

        property.objectReferenceValue = value;
        return true;
    }

    private static bool AssignChild(
        SerializedObject serialized,
        Transform root,
        string propertyName,
        string childName)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null || property.objectReferenceValue != null)
        {
            return false;
        }

        Transform child = FindNamed(root, childName);

        if (child == null)
        {
            return false;
        }

        Component component = child.GetComponent<Slider>();

        if (propertyName == "closeButton")
        {
            component = child.GetComponent<Button>();
        }

        if (component == null)
        {
            return false;
        }

        property.objectReferenceValue = component;
        return true;
    }

    private static bool AssignChildObject(
        SerializedObject serialized,
        Transform root,
        string propertyName,
        string childName)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null || property.objectReferenceValue != null)
        {
            return false;
        }

        Transform child = FindNamed(root, childName);

        if (child == null)
        {
            return false;
        }

        property.objectReferenceValue = child.gameObject;
        return true;
    }

    private static Transform FindNamed(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static SpriteRenderer ReadRenderer(
        Component component,
        string propertyName)
    {
        if (component == null)
        {
            return null;
        }

        SerializedObject serialized = new SerializedObject(component);
        SerializedProperty property =
            serialized.FindProperty(propertyName);
        return property == null
            ? null
            : property.objectReferenceValue as SpriteRenderer;
    }

    private static SpriteRenderer PreferredRenderer(
        GameObject root,
        SpriteRenderer preferred,
        bool allowRoot)
    {
        if (preferred != null &&
            (allowRoot || preferred.gameObject != root))
        {
            return preferred;
        }

        return SelectChildRenderer(root);
    }

    private static SpriteRenderer SelectChildRenderer(GameObject root)
    {
        List<SpriteRenderer> children = ChildRenderers(root);

        if (children.Count == 1)
        {
            return children[0];
        }

        return null;
    }

    private static List<SpriteRenderer> ChildRenderers(GameObject root)
    {
        List<SpriteRenderer> children = new List<SpriteRenderer>();

        foreach (SpriteRenderer renderer in
            root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.gameObject != root)
            {
                children.Add(renderer);
            }
        }

        return children;
    }

    private static GameObject CreateUi(
        string objectName,
        Transform parent,
        Scene scene,
        params Type[] extra)
    {
        List<Type> types = new List<Type> { typeof(RectTransform) };
        types.AddRange(extra);
        GameObject item = new GameObject(objectName, types.ToArray());
        SceneManager.MoveGameObjectToScene(item, scene);
        Undo.RegisterCreatedObjectUndo(item, UndoName);
        item.transform.SetParent(parent, false);
        item.layer = parent.gameObject.layer;
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = Vector2.zero;
        return item;
    }

    private static GameObject CreateImage(
        string objectName,
        Transform parent,
        Scene scene,
        Color color,
        bool raycast)
    {
        GameObject item = CreateUi(
            objectName,
            parent,
            scene,
            typeof(Image)
        );
        Image image = item.GetComponent<Image>();
        image.sprite = UiSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = raycast;
        return item;
    }

    private static void CreateLabel(
        string objectName,
        Transform parent,
        Scene scene,
        TMP_FontAsset font,
        string text,
        float size,
        Vector2 anchor,
        Vector2 position,
        Vector2 dimensions)
    {
        GameObject item = CreateUi(
            objectName,
            parent,
            scene,
            typeof(TextMeshProUGUI)
        );
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        TextMeshProUGUI label = item.GetComponent<TextMeshProUGUI>();

        if (font != null)
        {
            label.font = font;
        }

        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.945f, 0.776f, 1f);
        label.raycastTarget = false;
    }

    private static Slider CreateSlider(
        string objectName,
        Transform parent,
        Scene scene,
        Vector2 position,
        float value)
    {
        GameObject root = CreateUi(
            objectName,
            parent,
            scene,
            typeof(Slider)
        );
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = position;
        rootRect.sizeDelta = new Vector2(520f, 32f);

        GameObject background = CreateImage(
            "Background",
            root.transform,
            scene,
            new Color(0.102f, 0.165f, 0.125f, 1f),
            true
        );
        Stretch(background.GetComponent<RectTransform>());

        GameObject fillArea = CreateUi("Fill Area", root.transform, scene);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        Stretch(fillAreaRect);
        fillAreaRect.offsetMin = new Vector2(10f, 8f);
        fillAreaRect.offsetMax = new Vector2(-10f, -8f);

        GameObject fill = CreateImage(
            "Fill",
            fillArea.transform,
            scene,
            new Color(0.247f, 0.561f, 0.306f, 1f),
            false
        );
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        Stretch(fillRect);

        GameObject handleArea = CreateUi(
            "Handle Slide Area",
            root.transform,
            scene
        );
        RectTransform handleAreaRect =
            handleArea.GetComponent<RectTransform>();
        Stretch(handleAreaRect);
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject handle = CreateImage(
            "Handle",
            handleArea.transform,
            scene,
            new Color(1f, 0.945f, 0.776f, 1f),
            true
        );
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(22f, 32f);
        Image handleImage = handle.GetComponent<Image>();
        Sprite knob = KnobSprite();

        if (knob != null)
        {
            handleImage.sprite = knob;
            handleImage.type = Image.Type.Simple;
        }

        Slider slider = root.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;
        return slider;
    }

    private static Button CreateButton(
        string objectName,
        Transform parent,
        Scene scene,
        TMP_FontAsset font,
        string text,
        Vector2 anchor,
        Vector2 position,
        Vector2 dimensions)
    {
        GameObject root = CreateImage(
            objectName,
            parent,
            scene,
            Color.white,
            true
        );
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        Button button = Undo.AddComponent<Button>(root);
        button.targetGraphic = root.GetComponent<Image>();
        Color secondary = new(0.247f, 0.561f, 0.306f, 1f);
        ColorBlock states = button.colors;
        states.normalColor = secondary;
        states.highlightedColor = Color.Lerp(secondary, Color.white, 0.28f);
        states.selectedColor = Color.Lerp(
            secondary,
            new Color(1f, 0.945f, 0.776f, 1f),
            0.45f
        );
        states.pressedColor = Color.Lerp(secondary, Color.black, 0.22f);
        Color disabled = Color.Lerp(
            secondary,
            new Color(0.15f, 0.15f, 0.15f, 1f),
            0.55f
        );
        disabled.a = 0.45f;
        states.disabledColor = disabled;
        states.colorMultiplier = 1f;
        states.fadeDuration = 0.08f;
        button.colors = states;

        GameObject labelObject = CreateUi(
            "Text",
            root.transform,
            scene,
            typeof(TextMeshProUGUI)
        );
        Stretch(labelObject.GetComponent<RectTransform>());
        TextMeshProUGUI label =
            labelObject.GetComponent<TextMeshProUGUI>();
        if (font != null)
        {
            label.font = font;
        }

        label.text = text;
        label.fontSize = 26f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.945f, 0.776f, 1f);
        label.raycastTarget = false;
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static Sprite UiSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/UISprite.psd"
        );
    }

    private static Sprite KnobSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/Knob.psd"
        );
    }

    private static TMP_FontAsset FindFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
        {
            return TMP_Settings.defaultFontAsset;
        }

        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");

        if (guids.Length == 0)
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            AssetDatabase.GUIDToAssetPath(guids[0])
        );
    }

    private static bool ClipExists(string path)
    {
        string project = Directory.GetParent(Application.dataPath).FullName;
        return File.Exists(Path.Combine(project, path));
    }

    private static bool TryGetOpenScene(out Scene scene, out string error)
    {
        scene = SceneManager.GetSceneByPath(ScenePath);
        error = null;

        if (!scene.IsValid() || !scene.isLoaded)
        {
            error =
                "Abre " + ScenePath +
                " antes de integrar el audio. No se cambió de escena.";
            return false;
        }

        return true;
    }

    private static bool IsPlayMode()
    {
        return EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void Report(string title, string message)
    {
        EditorUtility.DisplayDialog(title, message, "Cerrar");
    }

    private readonly struct ClipSlot
    {
        public ClipSlot(string path, string property, bool music)
        {
            Path = path;
            Property = property;
            Music = music;
        }

        public string Path { get; }
        public string Property { get; }
        public bool Music { get; }
    }

    private sealed class LoadedPrefab
    {
        public LoadedPrefab(string path, GameObject root)
        {
            Path = path;
            Root = root;
        }

        public string Path { get; }
        public GameObject Root { get; }
        public bool Changed { get; set; }
    }
}
