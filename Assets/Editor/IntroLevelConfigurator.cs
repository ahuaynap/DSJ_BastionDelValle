using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class IntroLevelConfigurator
{
    private const string MenuPath =
        "Bastion del Valle/Incrementos/20 - Finalizar Nivel Introductorio";

    private const string ScenePath = "Assets/Scenes/Level_01.unity";
    private const string UndoName = "Finalizar Nivel Introductorio";
    private const string ApproachMeshPath =
        "Assets/Art/Generated/Level01/Level01HeartApproach.asset";

    private static readonly string[] MemoryIds =
    {
        "BIO_POLINIZADOR_01",
        "memory_water_01",
        "memory_soil_01"
    };

    private static readonly Color Gold = new(0.886f, 0.722f, 0.294f, 1f);
    private static readonly Color Green = new(0.247f, 0.561f, 0.306f, 1f);
    private static readonly Color Cream = new(1f, 0.945f, 0.776f, 1f);
    private static readonly Color Background = new(0.149f, 0.239f, 0.188f, 0.96f);

    [MenuItem(MenuPath, false, 20)]
    private static void Configure()
    {
        if (IsBlocked())
        {
            Report("Level_01 debe estar abierta y fuera de Play Mode.");
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);

        if (!scene.IsValid() || !scene.isLoaded)
        {
            Report("Abre Level_01. No se modificó la escena.");
            return;
        }

        if (!TryCollect(scene, out Context context, out string error))
        {
            Report(error);
            return;
        }

        List<string> proposals = BuildProposals(context);

        if (proposals.Count > 0)
        {
            bool apply = EditorUtility.DisplayDialog(
                "Bastión del Valle",
                "La configuración actual difiere del cierre:\n\n- " +
                string.Join("\n- ", proposals) +
                "\n\nSi aceptas, también se preparan el HUD, la pausa " +
                "y el resultado que todavía no existan.\n\n¿Aplicar?",
                "Aplicar",
                "Cancelar"
            );

            if (!apply)
            {
                Report(
                    "No se aplicaron cambios. " +
                    "Los ajustes actuales se conservaron."
                );
                return;
            }
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);

        try
        {
            Apply(context);
            CompactMatchHud.Apply(scene, context.Report);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Report(context.Report.ToString());
        }
        catch (System.Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(exception);
            Report(
                "Los cambios de escena se revirtieron.\n" +
                exception.Message
            );
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool Validate()
    {
        return !IsBlocked();
    }

    private static void Apply(Context context)
    {
        ApplyWaves(context);
        ApplyMemories(context);
        ApplyReferences(context);
        ApplyHud(context);
        ApplyPause(context);
        ApplyResult(context);
        ApplyApproach(context);
        context.Report.AppendLine(
            "Integridad " + context.Health.MaxHealth +
            ", semillas iniciales " +
            ReadInt(context.Economy, "startingResources") +
            " y coste de torre " + context.TowerCost +
            " no se modificaron."
        );
        AppendWarnings(context);
        context.Report.AppendLine(
            "La escena quedó marcada y no se guardó sola."
        );
    }

    private static void ApplyWaves(Context context)
    {
        SerializedObject serialized = new(context.Waves);
        SerializedProperty waves = serialized.FindProperty("waves");
        waves.arraySize = 2;
        WriteWave(waves.GetArrayElementAtIndex(0), 4, 1.8f);
        WriteWave(waves.GetArrayElementAtIndex(1), 6, 1.5f);
        serialized.ApplyModifiedProperties();
        context.Report.AppendLine(
            "Oleadas: 4 enemigos cada 1.8 s y 6 enemigos cada 1.5 s. " +
            "El jugador las inicia desde preparación."
        );
    }

    private static void ApplyMemories(Context context)
    {
        SerializedObject serialized = new(context.Spawner);
        serialized.FindProperty("maxDiscoveriesPerRun").intValue = 2;
        serialized.FindProperty("minimumSpawnDelay").floatValue = 1f;
        serialized.FindProperty("maximumSpawnDelay").floatValue = 2f;
        SerializedProperty list =
            serialized.FindProperty("availableCollectibles");
        list.arraySize = context.Memories.Count;

        for (int index = 0; index < context.Memories.Count; index++)
        {
            list.GetArrayElementAtIndex(index).objectReferenceValue =
                context.Memories[index];
        }

        serialized.ApplyModifiedProperties();
        context.Report.AppendLine(
            "Memorias del nivel: 3 entradas, máximo 2 por partida, " +
            "una activa y una captura por oleada."
        );
    }

    private static void ApplyReferences(Context context)
    {
        AssignIfEmpty(context.Waves, "pauseManager", context.Pause);
        AssignIfEmpty(context.Spawner, "collectionManager", context.Collection);
        AssignIfEmpty(context.Hud, "collectibleSpawner", context.Spawner);
        AssignIfEmpty(context.EndGame, "bastionHealth", context.Health);
        AssignIfEmpty(context.EndGame, "waveManager", context.Waves);
        AssignIfEmpty(context.EndGame, "collectionManager", context.Collection);
    }

    private static void ApplyHud(Context context)
    {
        IntroClosureSettings settings = EnsureSettings(context);

        if (settings.HudArranged &&
            FindChild(context.Hud.transform, "TopRow") != null &&
            FindChild(context.Hud.transform, "BottomRow") != null)
        {
            EnsureButtons(context);
            context.Report.AppendLine(
                "HUD: se conservó la organización ya aplicada."
            );
            return;
        }

        RectTransform top = EnsureRow(context, "TopRow", true);
        RectTransform bottom = EnsureRow(context, "BottomRow", false);
        Place(context.Hud.GetComponent<HUDController>(), top, bottom, context);
        HideTechnicalPhase(context);
        SetBool(settings, "hudArranged", true);
        context.Report.AppendLine(
            "HUD: dos filas con anclas y grupos de diseño, " +
            "válidas para el lienzo de referencia 1920×1080 y su escala a 1280×720."
        );
    }

    private static void EnsureButtons(Context context)
    {
        RectTransform bottom = FindChild(
            context.Hud.transform,
            "BottomRow"
        ) as RectTransform;

        if (bottom == null)
        {
            return;
        }

        if (FindButton(context.Scene, "StartWaveButton") == null)
        {
            EnsureStartButton(context, bottom);
        }

        if (FindButton(context.Scene, "PauseButton") == null)
        {
            EnsurePauseButton(context, bottom);
        }
    }

    private static void Place(
        HUDController hud,
        RectTransform top,
        RectTransform bottom,
        Context context)
    {
        SerializedObject serialized = new(hud);
        MoveText(serialized, "bastionText", top);
        MoveText(serialized, "resourcesText", top);
        MoveText(serialized, "waveText", top);
        MoveText(serialized, "enemiesText", top);
        MoveText(serialized, "statusText", bottom);
        MoveText(serialized, "discoveriesText", bottom);

        Button codex = serialized.FindProperty("openButton") == null
            ? FindButton(context.Scene, "CodexButton")
            : null;

        if (codex == null)
        {
            codex = FindButton(context.Scene, "CodexButton");
        }

        if (codex != null)
        {
            Parent(codex.transform, bottom);
        }

        Button sound = FindButton(context.Scene, "AudioButton");

        if (sound == null)
        {
            sound = FindButtonByLabel(context.Scene, "Sonido");
        }

        if (sound != null)
        {
            Parent(sound.transform, bottom);
        }
        else
        {
            context.Warnings.Add(
                "No está el botón Sonido. La fila inferior reserva el resto de acciones."
            );
        }

        EnsureStartButton(context, bottom);
        EnsurePauseButton(context, bottom);
    }

    private static void ApplyPause(Context context)
    {
        IntroClosureSettings settings = EnsureSettings(context);
        Transform existing = FindInScene(context.Scene, "LevelPause");

        if (settings.PausePrepared && existing != null)
        {
            WirePause(context, existing.GetComponent<LevelPausePanel>());
            context.Report.AppendLine("Pausa: se conservó el panel existente.");
            return;
        }

        Transform canvas = context.Hud.transform.parent;
        GameObject root = existing != null
            ? existing.gameObject
            : CreateUi("LevelPause", canvas, context.Scene);
        Stretch(root.GetComponent<RectTransform>());
        LevelPausePanel panel = root.GetComponent<LevelPausePanel>();

        if (panel == null)
        {
            panel = Undo.AddComponent<LevelPausePanel>(root);
        }

        Button pause = FindButton(context.Scene, "PauseButton");
        GameObject window = FindChild(root.transform, "PauseWindow") != null
            ? FindChild(root.transform, "PauseWindow").gameObject
            : CreateUi("PauseWindow", root.transform, context.Scene);
        window.SetActive(false);
        Stretch(window.GetComponent<RectTransform>());

        if (FindChild(window.transform, "PauseDimmer") == null)
        {
            GameObject dimmer = CreateImage(
                "PauseDimmer",
                window.transform,
                context.Scene,
                new Color(Background.r, Background.g, Background.b, 0.45f)
            );
            Stretch(dimmer.GetComponent<RectTransform>());
        }

        Transform card = FindChild(window.transform, "PauseCard");

        if (card == null)
        {
            GameObject cardObject = CreateImage(
                "PauseCard",
                window.transform,
                context.Scene,
                Background
            );
            card = cardObject.transform;
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(520f, 280f);
        }

        Button continueButton = EnsureLabeledButton(
            card,
            context,
            "ContinueButton",
            "Continuar",
            Gold,
            new Vector2(0f, 24f)
        );
        Button restartButton = EnsureLabeledButton(
            card,
            context,
            "RestartLevelButton",
            "Reiniciar nivel",
            Green,
            new Vector2(0f, -56f)
        );
        AssignObject(panel, "pauseManager", context.Pause);
        AssignObject(panel, "collectionManager", context.Collection);
        AssignObject(panel, "pauseButton", pause);
        AssignObject(panel, "panelRoot", window);
        AssignObject(panel, "continueButton", continueButton);
        AssignObject(panel, "restartButton", restartButton);
        root.transform.SetAsLastSibling();
        SetBool(settings, "pausePrepared", true);
        context.Report.AppendLine(
            "Pausa: botón Pausa, Continuar y Reiniciar nivel."
        );
    }

    private static void WirePause(Context context, LevelPausePanel panel)
    {
        if (panel == null)
        {
            return;
        }

        AssignIfEmpty(panel, "pauseManager", context.Pause);
        AssignIfEmpty(panel, "collectionManager", context.Collection);
        AssignIfEmpty(panel, "pauseButton", FindButton(context.Scene, "PauseButton"));
    }

    private static void ApplyResult(Context context)
    {
        IntroClosureSettings settings = EnsureSettings(context);
        Button retry = FindButton(context.Scene, "RetryButton");

        if (settings.ResultPrepared && retry != null)
        {
            AssignIfEmpty(context.EndGame, "retryButton", retry);
            context.Report.AppendLine(
                "Resultado: se conservó el panel de cierre."
            );
            return;
        }

        SerializedObject endGame = new(context.EndGame);
        TMP_Text result = endGame.FindProperty("resultText").objectReferenceValue
            as TMP_Text;

        if (result == null)
        {
            context.Warnings.Add(
                "EndGameUI no tiene texto de resultado."
            );
            return;
        }

        RectTransform parent = result.transform.parent as RectTransform;
        retry = EnsureLabeledButton(
            parent,
            context,
            "RetryButton",
            "Reintentar",
            Gold,
            new Vector2(0f, 36f)
        );
        Record(result);
        result.textWrappingMode = TextWrappingModes.Normal;
        result.alignment = TextAlignmentOptions.Center;
        AssignObject(context.EndGame, "retryButton", retry);
        SetBool(settings, "resultPrepared", true);
        context.Report.AppendLine(
            "Resultado: Valle protegido o nuevo intento, con Reintentar."
        );
    }

    private static void ApplyApproach(Context context)
    {
        IntroClosureSettings settings = EnsureSettings(context);
        LevelVisualSettings visuals =
            FindOne<LevelVisualSettings>(context.Scene);

        if (visuals == null)
        {
            context.Warnings.Add(
                "No está LevelVisuals. No se revisó la unión visual con el corazón."
            );
            return;
        }

        Transform path = FindChild(visuals.transform, "Path");

        if (path != null && FindChild(path, "HeartApproach") != null)
        {
            context.Report.AppendLine(
                "Camino: la conexión decorativa ya existe."
            );
            return;
        }

        if (settings.ApproachChecked)
        {
            context.Report.AppendLine(
                "Camino: la revisión visual anterior se conservó."
            );
            return;
        }

        if (!TryMeasureGap(context, visuals, path, out Vector3 start, out Vector3 end, out float width))
        {
            SetBool(settings, "approachChecked", true);
            return;
        }

        CreateApproach(context, path, start, end, width);
        SetBool(settings, "approachChecked", true);
        context.Report.AppendLine(
            "Camino: se añadió una conexión decorativa corta hacia el corazón. " +
            "El último waypoint y la ruta lógica no cambiaron."
        );
    }

    private static bool TryMeasureGap(
        Context context,
        LevelVisualSettings visuals,
        Transform path,
        out Vector3 start,
        out Vector3 end,
        out float width)
    {
        start = Vector3.zero;
        end = Vector3.zero;
        width = 0.45f;

        if (path == null)
        {
            context.Warnings.Add("No está el camino visual.");
            return false;
        }

        EnemyPath route = ReadReference<EnemyPath>(
            context.SpawnerEnemy,
            "enemyPath"
        );

        if (route == null || route.Count < 2)
        {
            context.Warnings.Add("No se pudo leer el último waypoint.");
            return false;
        }

        Transform last = route.GetWaypoint(route.Count - 1);
        SpriteRenderer heart = context.Health.GetComponentInChildren<SpriteRenderer>(true);
        MeshRenderer fill = FindChild(path, "PathFill") != null
            ? FindChild(path, "PathFill").GetComponent<MeshRenderer>()
            : null;

        if (last == null || heart == null || heart.sprite == null || fill == null)
        {
            context.Warnings.Add(
                "Faltan el corazón visible o el material del camino."
            );
            return false;
        }

        SerializedObject settings = new(visuals);
        SerializedProperty pathWidth = settings.FindProperty("pathWidth");
        width = pathWidth != null ? pathWidth.floatValue : 0.45f;
        Vector3 closest = heart.bounds.ClosestPoint(last.position);
        float gap = Vector2.Distance(last.position, closest);

        if (gap <= width * 0.5f + 0.05f)
        {
            context.Report.AppendLine(
                "Camino: el trazo ya llega al borde visual del corazón."
            );
            return false;
        }

        float length = Mathf.Min(gap, 1.25f);
        Vector3 direction = closest - last.position;
        direction.z = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        start = last.position;
        end = last.position + direction.normalized * length;

        if (gap > 1.25f)
        {
            context.Warnings.Add(
                "La separación supera 1.25 unidades. " +
                "La conexión se limitó a ese tramo y la ruta lógica no se alargó."
            );
        }

        context.ApproachMaterial = fill.sharedMaterial;
        context.ApproachSortingLayer = fill.sortingLayerID;
        context.ApproachSortingOrder = fill.sortingOrder;
        return context.ApproachMaterial != null;
    }

    private static void CreateApproach(
        Context context,
        Transform path,
        Vector3 start,
        Vector3 end,
        float width)
    {
        EnsureFolder();
        Mesh mesh = BuildStrip(path, start, end, width);
        GameObject approach = new(
            "HeartApproach",
            typeof(MeshFilter),
            typeof(MeshRenderer)
        );
        SceneManager.MoveGameObjectToScene(approach, context.Scene);
        Undo.RegisterCreatedObjectUndo(approach, UndoName);
        approach.transform.SetParent(path, false);
        approach.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = approach.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = context.ApproachMaterial;
        renderer.sortingLayerID = context.ApproachSortingLayer;
        renderer.sortingOrder = context.ApproachSortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Mesh BuildStrip(
        Transform path,
        Vector3 start,
        Vector3 end,
        float width)
    {
        Vector3 localStart = path.InverseTransformPoint(start);
        Vector3 localEnd = path.InverseTransformPoint(end);
        localStart.z = 1f;
        localEnd.z = 1f;
        Vector3 direction = localEnd - localStart;
        Vector3 side = new Vector3(-direction.y, direction.x, 0f).normalized *
                       (width * 0.5f);
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ApproachMeshPath);

        if (mesh == null)
        {
            mesh = new Mesh { name = "Level01HeartApproach" };
            AssetDatabase.CreateAsset(mesh, ApproachMeshPath);
        }
        else
        {
            Undo.RecordObject(mesh, UndoName);
        }

        mesh.Clear();
        mesh.SetVertices(new[]
        {
            localStart - side,
            localStart + side,
            localEnd + side,
            localEnd - side
        });
        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        mesh.SetNormals(new[]
        {
            Vector3.back,
            Vector3.back,
            Vector3.back,
            Vector3.back
        });
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssetIfDirty(mesh);
        return mesh;
    }

    private static List<string> BuildProposals(Context context)
    {
        List<string> proposals = new();
        string current = DescribeWaves(context.Waves);

        if (current != "4 × 1.8 s; 6 × 1.5 s")
        {
            proposals.Add("Oleadas: " + current + " → 4 × 1.8 s; 6 × 1.5 s");
        }

        SerializedObject spawner = new(context.Spawner);
        int max = spawner.FindProperty("maxDiscoveriesPerRun").intValue;
        float minimum = spawner.FindProperty("minimumSpawnDelay").floatValue;
        float maximum = spawner.FindProperty("maximumSpawnDelay").floatValue;

        if (max != 2)
        {
            proposals.Add("Memorias por partida: " + max + " → 2");
        }

        if (!Mathf.Approximately(minimum, 1f) ||
            !Mathf.Approximately(maximum, 2f))
        {
            proposals.Add(
                "Aparición de memorias: " + minimum.ToString("0.0") +
                "–" + maximum.ToString("0.0") + " s → 1–2 s"
            );
        }

        string ids = DescribeMemories(context.Spawner);

        if (ids != string.Join(", ", MemoryIds))
        {
            proposals.Add(
                "Memorias habilitadas: " + ids + " → " +
                string.Join(", ", MemoryIds)
            );
        }

        return proposals;
    }

    private static bool TryCollect(
        Scene scene,
        out Context context,
        out string error)
    {
        context = null;
        error = null;
        WaveManager[] waves = FindAll<WaveManager>(scene);
        HUDController[] huds = FindAll<HUDController>(scene);
        CollectibleSpawner[] spawners = FindAll<CollectibleSpawner>(scene);
        EducationalCollectionManager[] collections =
            FindAll<EducationalCollectionManager>(scene);
        PauseManager[] pauses = FindAll<PauseManager>(scene);
        EndGameUI[] ends = FindAll<EndGameUI>(scene);
        BastionHealth[] health = FindAll<BastionHealth>(scene);
        EconomyManager[] economies = FindAll<EconomyManager>(scene);
        EnemySpawner[] enemies = FindAll<EnemySpawner>(scene);

        if (waves.Length != 1 || huds.Length != 1 || spawners.Length != 1 ||
            collections.Length != 1 || pauses.Length != 1 || ends.Length != 1 ||
            health.Length != 1 || economies.Length != 1 || enemies.Length != 1)
        {
            error =
                "Level_01 no tiene una sola copia de los coordinadores " +
                "de oleada, HUD, memorias, pausa, resultado, corazón o economía. " +
                "No se modificó la escena.";
            return false;
        }

        List<CollectibleData> memories = new();

        foreach (string id in MemoryIds)
        {
            CollectibleData data = FindMemory(id);

            if (data == null)
            {
                error = "No está la memoria " + id + ". No se modificó la escena.";
                return false;
            }

            memories.Add(data);
        }

        int towerCost = ReadTowerCost(scene);
        context = new Context
        {
            Scene = scene,
            Waves = waves[0],
            Hud = huds[0],
            Spawner = spawners[0],
            Collection = collections[0],
            Pause = pauses[0],
            EndGame = ends[0],
            Health = health[0],
            Economy = economies[0],
            SpawnerEnemy = enemies[0],
            Memories = memories,
            TowerCost = towerCost,
            Report = new StringBuilder(),
            Warnings = new List<string>()
        };

        if (health[0].MaxHealth != 100 ||
            ReadInt(economies[0], "startingResources") != 100 ||
            towerCost != 40)
        {
            context.Warnings.Add(
                "La integridad, las semillas o el coste no están en " +
                "100, 100 y 40. Se dejaron como están."
            );
        }

        return true;
    }

    private static RectTransform EnsureRow(
        Context context,
        string name,
        bool top)
    {
        Transform existing = FindChild(context.Hud.transform, name);
        GameObject row = existing != null
            ? existing.gameObject
            : CreateUi(name, context.Hud.transform, context.Scene);
        RectTransform rect = row.GetComponent<RectTransform>();
        Record(rect);
        rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rect.offsetMin = top ? new Vector2(16f, -72f) : new Vector2(16f, 12f);
        rect.offsetMax = top ? new Vector2(-16f, -8f) : new Vector2(-16f, 84f);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();

        if (layout == null)
        {
            layout = Undo.AddComponent<HorizontalLayoutGroup>(row);
        }

        Record(layout);
        layout.spacing = 8f;
        layout.padding = new RectOffset(8, 8, 4, 4);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return rect;
    }

    private static void MoveText(
        SerializedObject hud,
        string fieldName,
        RectTransform row)
    {
        TMP_Text text = hud.FindProperty(fieldName).objectReferenceValue as TMP_Text;

        if (text == null)
        {
            return;
        }

        Parent(text.transform, row);
        Record(text);
        text.textWrappingMode = fieldName == "statusText"
            ? TextWrappingModes.Normal
            : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        LayoutElement element = text.GetComponent<LayoutElement>();

        if (element == null)
        {
            element = Undo.AddComponent<LayoutElement>(text.gameObject);
        }

        Record(element);
        element.flexibleWidth = fieldName == "statusText" ? 1.6f : 1f;
        element.minHeight = 44f;
    }

    private static void HideTechnicalPhase(Context context)
    {
        SerializedObject hud = new(context.Hud);
        TMP_Text phase = hud.FindProperty("waveStateText").objectReferenceValue
            as TMP_Text;

        if (phase == null)
        {
            return;
        }

        Record(phase.gameObject);
        phase.gameObject.SetActive(false);
    }

    private static Button EnsureStartButton(Context context, RectTransform row)
    {
        Button button = FindButton(context.Scene, "StartWaveButton");

        if (button == null)
        {
            button = CreateButton(
                "StartWaveButton",
                row,
                context,
                "Iniciar oleada",
                Gold
            );
        }
        else
        {
            Parent(button.transform, row);
        }

        WaveStartControl control = button.GetComponent<WaveStartControl>();

        if (control == null)
        {
            control = Undo.AddComponent<WaveStartControl>(button.gameObject);
        }

        AssignObject(control, "waveManager", context.Waves);
        AssignObject(control, "pauseManager", context.Pause);
        AssignObject(control, "button", button);
        return button;
    }

    private static Button EnsurePauseButton(Context context, RectTransform row)
    {
        Button button = FindButton(context.Scene, "PauseButton");

        if (button == null)
        {
            button = CreateButton(
                "PauseButton",
                row,
                context,
                "Pausa",
                Green
            );
        }
        else
        {
            Parent(button.transform, row);
        }

        return button;
    }

    private static Button EnsureLabeledButton(
        Transform parent,
        Context context,
        string name,
        string label,
        Color color,
        Vector2 position)
    {
        Button button = FindChild(parent, name) != null
            ? FindChild(parent, name).GetComponent<Button>()
            : null;

        if (button != null)
        {
            return button;
        }

        button = CreateButton(name, parent, context, label, color);
        RectTransform rect = button.GetComponent<RectTransform>();
        Record(rect);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(280f, 52f);
        return button;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        Context context,
        string label,
        Color color)
    {
        GameObject root = CreateImage(name, parent, context.Scene, Color.white);
        Button button = Undo.AddComponent<Button>(root);
        Image image = root.GetComponent<Image>();
        button.targetGraphic = image;
        ColorBlock states = button.colors;
        states.normalColor = color;
        states.highlightedColor = Color.Lerp(color, Color.white, 0.28f);
        states.selectedColor = Color.Lerp(color, Cream, 0.45f);
        states.pressedColor = Color.Lerp(color, Color.black, 0.22f);
        Color disabled = Color.Lerp(color, new Color(0.15f, 0.15f, 0.15f, 1f), 0.55f);
        disabled.a = 0.45f;
        states.disabledColor = disabled;
        states.colorMultiplier = 1f;
        states.fadeDuration = 0.08f;
        button.colors = states;
        TMP_FontAsset font = FindFont(context.Hud);
        GameObject textObject = CreateUi(
            "Text",
            root.transform,
            context.Scene,
            typeof(TextMeshProUGUI)
        );
        Stretch(textObject.GetComponent<RectTransform>());
        TextMeshProUGUI view = textObject.GetComponent<TextMeshProUGUI>();
        view.text = label;
        view.font = font;
        view.fontSize = 24f;
        view.alignment = TextAlignmentOptions.Center;
        view.color = Cream;
        view.raycastTarget = false;

        if (root.GetComponent<UiClickSound>() == null)
        {
            Undo.AddComponent<UiClickSound>(root);
        }

        LayoutElement element = Undo.AddComponent<LayoutElement>(root);
        element.preferredWidth = 190f;
        element.flexibleWidth = 0f;
        element.minHeight = 48f;
        return button;
    }

    private static IntroClosureSettings EnsureSettings(Context context)
    {
        IntroClosureSettings settings =
            context.Waves.GetComponent<IntroClosureSettings>();

        if (settings == null)
        {
            settings = Undo.AddComponent<IntroClosureSettings>(
                context.Waves.gameObject
            );
        }

        return settings;
    }

    private static void WriteWave(SerializedProperty wave, int count, float interval)
    {
        wave.FindPropertyRelative("enemyCount").intValue = count;
        wave.FindPropertyRelative("spawnInterval").floatValue = interval;
    }

    private static string DescribeWaves(WaveManager manager)
    {
        SerializedObject serialized = new(manager);
        SerializedProperty waves = serialized.FindProperty("waves");

        if (waves == null || waves.arraySize == 0)
        {
            return "sin oleadas";
        }

        List<string> parts = new();

        for (int index = 0; index < waves.arraySize; index++)
        {
            SerializedProperty wave = waves.GetArrayElementAtIndex(index);
            parts.Add(
                wave.FindPropertyRelative("enemyCount").intValue +
                " × " +
                wave.FindPropertyRelative("spawnInterval").floatValue.ToString("0.0") +
                " s"
            );
        }

        return string.Join("; ", parts);
    }

    private static string DescribeMemories(CollectibleSpawner spawner)
    {
        SerializedObject serialized = new(spawner);
        SerializedProperty list = serialized.FindProperty("availableCollectibles");
        List<string> ids = new();

        for (int index = 0; index < list.arraySize; index++)
        {
            CollectibleData data = list.GetArrayElementAtIndex(index)
                .objectReferenceValue as CollectibleData;
            ids.Add(data == null ? "(vacía)" : data.CollectibleId);
        }

        return ids.Count == 0 ? "(ninguna)" : string.Join(", ", ids);
    }

    private static CollectibleData FindMemory(string id)
    {
        string[] guids = AssetDatabase.FindAssets("t:CollectibleData");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CollectibleData data = AssetDatabase.LoadAssetAtPath<CollectibleData>(path);

            if (data != null && data.CollectibleId == id)
            {
                return data;
            }
        }

        return null;
    }

    private static int ReadTowerCost(Scene scene)
    {
        foreach (BuildPlot plot in FindAll<BuildPlot>(scene))
        {
            TowerController tower = ReadReference<TowerController>(plot, "towerPrefab");

            if (tower != null && tower.Data != null)
            {
                return tower.Data.BuildCost;
            }
        }

        return -1;
    }

    private static int ReadInt(UnityEngine.Object target, string fieldName)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(fieldName);
        return property == null ? -1 : property.intValue;
    }

    private static void AssignIfEmpty(
        UnityEngine.Object target,
        string fieldName,
        UnityEngine.Object value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property != null && property.objectReferenceValue == null && value != null)
        {
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }
    }

    private static void AssignObject(
        UnityEngine.Object target,
        string fieldName,
        UnityEngine.Object value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            return;
        }

        if (property.objectReferenceValue == value)
        {
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
    }

    private static void SetBool(
        UnityEngine.Object target,
        string fieldName,
        bool value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(fieldName);
        property.boolValue = value;
        serialized.ApplyModifiedProperties();
    }

    private static T ReadReference<T>(UnityEngine.Object owner, string fieldName)
        where T : UnityEngine.Object
    {
        SerializedObject serialized = new(owner);
        SerializedProperty property = serialized.FindProperty(fieldName);
        return property == null ? null : property.objectReferenceValue as T;
    }

    private static TMP_FontAsset FindFont(HUDController hud)
    {
        foreach (TMP_Text text in hud.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font != null)
            {
                return text.font;
            }
        }

        return null;
    }

    private static Button FindButton(Scene scene, string name)
    {
        foreach (Button button in FindAll<Button>(scene))
        {
            if (button.gameObject.name == name)
            {
                return button;
            }
        }

        return null;
    }

    private static Button FindButtonByLabel(Scene scene, string label)
    {
        foreach (Button button in FindAll<Button>(scene))
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);

            if (text != null && text.text == label)
            {
                return button;
            }
        }

        return null;
    }

    private static T FindOne<T>(Scene scene) where T : Component
    {
        T[] all = FindAll<T>(scene);
        return all.Length == 1 ? all[0] : null;
    }

    private static T[] FindAll<T>(Scene scene) where T : Component
    {
        List<T> results = new();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            results.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return results.ToArray();
    }

    private static Transform FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }
        }

        return null;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);

            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static void Parent(Transform child, RectTransform parent)
    {
        if (child.parent == parent)
        {
            return;
        }

        Undo.SetTransformParent(child, parent, UndoName);
        RectTransform rect = child as RectTransform;

        if (rect == null)
        {
            return;
        }

        Record(rect);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static GameObject CreateUi(
        string name,
        Transform parent,
        Scene scene,
        System.Type extra = null)
    {
        GameObject item = extra == null
            ? new GameObject(name, typeof(RectTransform))
            : new GameObject(name, typeof(RectTransform), extra);
        SceneManager.MoveGameObjectToScene(item, scene);
        Undo.RegisterCreatedObjectUndo(item, UndoName);
        item.transform.SetParent(parent, false);
        item.layer = parent.gameObject.layer;
        return item;
    }

    private static GameObject CreateImage(
        string name,
        Transform parent,
        Scene scene,
        Color color)
    {
        GameObject item = CreateUi(name, parent, scene, typeof(Image));
        Image image = item.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/UISprite.psd"
        );
        image.type = Image.Type.Sliced;
        image.color = color;
        return item;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art/Generated"))
        {
            AssetDatabase.CreateFolder("Assets/Art", "Generated");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Art/Generated/Level01"))
        {
            AssetDatabase.CreateFolder("Assets/Art/Generated", "Level01");
        }
    }

    private static void Record(UnityEngine.Object target)
    {
        if (target != null)
        {
            Undo.RecordObject(target, UndoName);
        }
    }

    private static void AppendWarnings(Context context)
    {
        if (context.Warnings.Count == 0)
        {
            context.Report.AppendLine("Advertencias: ninguna.");
            return;
        }

        context.Report.AppendLine("Advertencias:");

        foreach (string warning in context.Warnings)
        {
            context.Report.AppendLine("- " + warning);
        }
    }

    private static bool IsBlocked()
    {
        return EditorApplication.isPlaying ||
               EditorApplication.isPlayingOrWillChangePlaymode ||
               EditorApplication.isCompiling;
    }

    private static void Report(string message)
    {
        EditorUtility.DisplayDialog(
            "Bastión del Valle",
            message,
            "Cerrar"
        );
    }

    private sealed class Context
    {
        public Scene Scene;
        public WaveManager Waves;
        public HUDController Hud;
        public CollectibleSpawner Spawner;
        public EducationalCollectionManager Collection;
        public PauseManager Pause;
        public EndGameUI EndGame;
        public BastionHealth Health;
        public EconomyManager Economy;
        public EnemySpawner SpawnerEnemy;
        public List<CollectibleData> Memories;
        public int TowerCost;
        public StringBuilder Report;
        public List<string> Warnings;
        public Material ApproachMaterial;
        public int ApproachSortingLayer;
        public int ApproachSortingOrder;
    }
}
