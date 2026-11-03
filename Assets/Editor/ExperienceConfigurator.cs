using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ExperienceConfigurator
{
    private const string MenuPath =
        "Bastion del Valle/Consolidacion/29 - Configurar Experiencia";

    private const string UndoName = "Configurar Experiencia";
    private const string Level01Path = "Assets/Scenes/Level_01.unity";
    private const string Level02Path = "Assets/Scenes/Level_02.unity";
    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string LayoutPath = "Assets/Data/Maps/ConfluenceLayout.asset";
    private const string PathMaterialPath =
        "Assets/Art/Generated/Confluence/ConfluencePath.mat";
    private const string BridgeMaterialPath =
        "Assets/Art/Generated/Confluence/ConfluenceBridge.mat";
    private const string ChannelMaterialPath =
        "Assets/Art/Generated/Confluence/ConfluenceChannel.mat";
    private const string GroundMaterialPath =
        "Assets/Art/Generated/Confluence/ConfluenceBase.mat";

    [MenuItem(MenuPath, false, 40)]
    private static void Configure()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
        {
            return;
        }

        string problem = ExperienceLayout.Validate();

        if (problem != null)
        {
            EditorUtility.DisplayDialog("Experiencia", problem, "Cerrar");
            return;
        }

        ConfluenceLayout layout =
            AssetDatabase.LoadAssetAtPath<ConfluenceLayout>(LayoutPath);
        int previousPlots = layout != null && layout.Plots != null
            ? layout.Plots.Length
            : 0;
        string summary =
            "Mapa 2: 14 parcelas (4 arriba, 4 abajo, 4 en la confluencia " +
            "y 2 junto al tramo final).\n" +
            "Dos accesos, corazón, canales, puentes y una alternativa.\n" +
            "La distribución actual tiene " + previousPlots + " parcelas.\n" +
            "Level_01 conserva sus oleadas. Las escenas abiertas no se descartan.\n" +
            "Hay que guardar Level_02 después de aplicar.";

        if (!EditorUtility.DisplayDialog(
                "Distribución del mapa 2",
                summary,
                "Aplicar",
                "Cancelar"))
        {
            Debug.Log("Experiencia: no se aplicó.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Level02Path) == null)
        {
            Debug.LogError("No está Level_02. No se copió Level_01 ni se modificó el mapa.");
            return;
        }

        int group = Undo.GetCurrentGroup();
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        var report = new StringBuilder();

        try
        {
            if (!TryOpenAlone(Level02Path, out Scene level02, report))
            {
                Undo.CollapseUndoOperations(group);
                Debug.Log(report.ToString());
                return;
            }

            WriteLayout(report);
            ApplyMap(level02, report);
            ApplyMatchChrome(level02, report);

            if (!TryOpenAlone(Level01Path, out Scene level01, report))
            {
                EditorSceneManager.MarkSceneDirty(level02);
                Undo.CollapseUndoOperations(group);
                Debug.Log(report.ToString());
                return;
            }

            ApplyMatchChrome(level01, report);
            EnsureMenu(report);
            EnsureProjectiles(report);
            EnsureBuildList(report);
            if (level02.IsValid() && level02.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(level02);
            }

            if (level01.IsValid() && level01.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(level01);
            }
            Undo.CollapseUndoOperations(group);
            Debug.Log(report.ToString());
        }
        catch (System.Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogError("Experiencia revertida. " + exception.Message);
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool CanConfigure()
    {
        return !EditorApplication.isPlaying && !EditorApplication.isCompiling;
    }

    private static void WriteLayout(StringBuilder report)
    {
        ConfluenceLayout layout =
            AssetDatabase.LoadAssetAtPath<ConfluenceLayout>(LayoutPath);

        if (layout == null)
        {
            layout = ScriptableObject.CreateInstance<ConfluenceLayout>();
            AssetDatabase.CreateAsset(layout, LayoutPath);
        }

        Undo.RecordObject(layout, UndoName);
        SerializedObject serialized = new SerializedObject(layout);
        serialized.FindProperty("cellSize").floatValue = ExperienceLayout.CellSize;
        serialized.FindProperty("origin").vector2Value =
            new Vector2(ExperienceLayout.OriginX, ExperienceLayout.OriginY);
        serialized.FindProperty("width").intValue = ExperienceLayout.Width;
        serialized.FindProperty("height").intValue = ExperienceLayout.Height;
        serialized.FindProperty("topSpawn").vector2IntValue =
            new Vector2Int(ExperienceLayout.TopSpawnX, ExperienceLayout.TopSpawnY);
        serialized.FindProperty("bottomSpawn").vector2IntValue =
            new Vector2Int(ExperienceLayout.BottomSpawnX, ExperienceLayout.BottomSpawnY);
        serialized.FindProperty("goal").vector2IntValue =
            new Vector2Int(ExperienceLayout.GoalX, ExperienceLayout.GoalY);
        serialized.FindProperty("passage").vector2IntValue =
            new Vector2Int(ExperienceLayout.PassageX, ExperienceLayout.PassageY);
        ExperienceCell[] cells = ExperienceLayout.Cells();
        SerializedProperty property = serialized.FindProperty("cells");
        property.arraySize = cells.Length;

        for (int i = 0; i < cells.Length; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("x").intValue = cells[i].X;
            element.FindPropertyRelative("y").intValue = cells[i].Y;
            element.FindPropertyRelative("walkable").boolValue = cells[i].Walkable;
            element.FindPropertyRelative("cost").intValue = cells[i].Cost;
            element.FindPropertyRelative("kind").enumValueIndex = cells[i].Kind;
        }

        int[] plots = ExperienceLayout.Plots();
        SerializedProperty plotProperty = serialized.FindProperty("plots");
        plotProperty.arraySize = plots.Length / 2;

        for (int i = 0; i < plots.Length; i += 2)
        {
            plotProperty.GetArrayElementAtIndex(i / 2).vector2IntValue =
                new Vector2Int(plots[i], plots[i + 1]);
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(layout);
        AssetDatabase.SaveAssetIfDirty(layout);
        report.AppendLine("Cuadrícula de confluencia: 14 parcelas.");
    }

    private static void ApplyMap(Scene scene, StringBuilder report)
    {
        ConfluenceLayout layout =
            AssetDatabase.LoadAssetAtPath<ConfluenceLayout>(LayoutPath);
        ConfluenceMap confluence = First<ConfluenceMap>(scene);
        Transform map = confluence != null
            ? confluence.transform
            : FindOrCreate(scene, "Confluence").transform;
        RebuildGround(map, layout);
        MoveActors(scene, layout);
        PlacePlots(scene, layout);
        FrameCamera(scene, layout);
        ShiftDressing(scene, layout);
        report.AppendLine("Level_02 muestra camino continuo, canales y puentes.");
    }

    private static void RebuildGround(Transform map, ConfluenceLayout layout)
    {
        Transform tiles = Child(map, "Celdas");
        ClearChildren(tiles);
        Material ground = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        Material path = AssetDatabase.LoadAssetAtPath<Material>(PathMaterialPath);
        Material bridge = AssetDatabase.LoadAssetAtPath<Material>(BridgeMaterialPath);
        Material channel = AssetDatabase.LoadAssetAtPath<Material>(ChannelMaterialPath);
        CreateBase(map, layout, ground);
        var centers = new List<Vector3>();
        ConfluenceCell[] cells = layout.Cells;

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].walkable && cells[i].kind != ConfluenceCellKind.Bridge)
            {
                centers.Add(layout.CenterOf(cells[i].x, cells[i].y));
            }
        }

        CreateRibbon(tiles, "Camino", centers, layout.CellSize * 1.08f, path, -10);
        centers.Clear();

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].kind == ConfluenceCellKind.Channel)
            {
                centers.Add(layout.CenterOf(cells[i].x, cells[i].y));
            }
        }

        CreateRibbon(tiles, "Canales", centers, layout.CellSize * 1.08f, channel, -20);

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].kind != ConfluenceCellKind.Bridge)
            {
                continue;
            }

            CreateQuad(
                tiles,
                "Bridge_" + cells[i].x + "_" + cells[i].y,
                layout.CenterOf(cells[i].x, cells[i].y),
                layout.CellSize * 1.08f,
                bridge,
                -8);
        }
    }

    private static void MoveActors(Scene scene, ConfluenceLayout layout)
    {
        List<BastionHealth> hearts = FindAll<BastionHealth>(scene);

        if (hearts.Count == 1)
        {
            Vector3 center = layout.CenterOf(layout.Goal.x, layout.Goal.y);
            Undo.RecordObject(hearts[0].transform, UndoName);
            hearts[0].transform.position = new Vector3(center.x, center.y, 0f);
        }

        List<ConfluenceAccess> accesses = FindAll<ConfluenceAccess>(scene);
        ChannelNavigator navigator = First<ChannelNavigator>(scene);
        MoveAccess(accesses, 0, layout.TopSpawn, layout, navigator);
        MoveAccess(accesses, 1, layout.BottomSpawn, layout, navigator);
        AssignAir(FindAccess(accesses, 0), layout, true);
        AssignAir(FindAccess(accesses, 1), layout, false);
    }

    private static void MoveAccess(
        List<ConfluenceAccess> accesses,
        int index,
        Vector2Int cell,
        ConfluenceLayout layout,
        ChannelNavigator navigator)
    {
        ConfluenceAccess access = FindAccess(accesses, index);

        if (access == null)
        {
            return;
        }

        Vector3 center = layout.CenterOf(cell.x, cell.y);
        Undo.RecordObject(access.transform, UndoName);
        access.transform.position = new Vector3(center.x, center.y, 0f);
        EnemySpawner spawner = access.GetComponent<EnemySpawner>();

        if (spawner != null && navigator != null)
        {
            SerializedObject serialized = new SerializedObject(spawner);
            SerializedProperty property = serialized.FindProperty("navigator");

            if (property != null && property.objectReferenceValue == null)
            {
                property.objectReferenceValue = navigator;
                serialized.ApplyModifiedProperties();
            }
        }
    }

    private static void AssignAir(
        ConfluenceAccess access,
        ConfluenceLayout layout,
        bool upper)
    {
        if (access == null)
        {
            return;
        }

        NavigationGrid grid = layout.CreateGrid(true);
        int startX = upper ? layout.TopSpawn.x : layout.BottomSpawn.x;
        int startY = upper ? layout.TopSpawn.y : layout.BottomSpawn.y;
        GridPathResult route = GridPathfinder.Find(
            grid,
            startX,
            startY,
            layout.Goal.x,
            layout.Goal.y);

        if (route.Status != GridPathStatus.Found)
        {
            return;
        }

        AirLane lane = access.GetComponent<AirLane>();

        if (lane == null)
        {
            lane = Undo.AddComponent<AirLane>(access.gameObject);
        }

        Transform holder = Child(access.transform, "AirRoute");
        ClearChildren(holder);
        var points = new Transform[route.Steps.Count];

        for (int i = 0; i < route.Steps.Count; i++)
        {
            Vector3 center = layout.CenterOf(route.Steps[i].X, route.Steps[i].Y);
            GameObject point = new GameObject("AirPoint_" + i);
            Undo.RegisterCreatedObjectUndo(point, UndoName);
            point.transform.SetParent(holder, false);
            point.transform.position = new Vector3(center.x, center.y, 0f);
            points[i] = point.transform;
        }

        SerializedObject serialized = new SerializedObject(lane);
        SerializedProperty property = serialized.FindProperty("points");
        property.arraySize = points.Length;

        for (int i = 0; i < points.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
        }

        serialized.ApplyModifiedProperties();
    }

    private static void PlacePlots(Scene scene, ConfluenceLayout layout)
    {
        Vector2Int[] requested = layout.Plots ?? System.Array.Empty<Vector2Int>();
        List<ConfluencePlot> owned = FindAll<ConfluencePlot>(scene);
        List<BuildPlot> spare = new List<BuildPlot>();

        foreach (BuildPlot plot in FindAll<BuildPlot>(scene))
        {
            if (plot.GetComponent<ConfluencePlot>() == null)
            {
                spare.Add(plot);
            }
        }

        int cursor = 0;

        while (owned.Count < requested.Length && cursor < spare.Count)
        {
            ConfluencePlot marker = Undo.AddComponent<ConfluencePlot>(spare[cursor].gameObject);
            SetInt(marker, "index", owned.Count);
            owned.Add(marker);
            cursor++;
        }

        while (owned.Count < requested.Length && owned.Count > 0)
        {
            BuildPlot template = owned[0].GetComponent<BuildPlot>();
            GameObject clone = Object.Instantiate(template.gameObject, template.transform.parent);
            clone.name = "BuildPlot_" + owned.Count.ToString("00");
            Undo.RegisterCreatedObjectUndo(clone, UndoName);
            ConfluencePlot marker = clone.GetComponent<ConfluencePlot>();

            if (marker == null)
            {
                marker = Undo.AddComponent<ConfluencePlot>(clone);
            }

            SetInt(marker, "index", owned.Count);
            owned.Add(marker);
        }

        for (int i = 0; i < requested.Length && i < owned.Count; i++)
        {
            ConfluencePlot plot = FindPlot(owned, i);

            if (plot == null)
            {
                continue;
            }

            Vector3 center = layout.CenterOf(requested[i].x, requested[i].y);
            Undo.RecordObject(plot.transform, UndoName);
            plot.transform.position = new Vector3(center.x, center.y, 0f);
        }
    }

    private static FrameFit FitMap(ConfluenceLayout layout)
    {
        float minX = layout.Origin.x;
        float minY = layout.Origin.y;
        return PlayfieldFrame.Fit(
            minX,
            minX + layout.Width * layout.CellSize,
            minY,
            minY + layout.Height * layout.CellSize,
            HudReserve.TopFraction(true),
            HudReserve.BottomFraction(true),
            16f / 9f,
            1.2f);
    }

    private static void FrameCamera(Scene scene, ConfluenceLayout layout)
    {
        Camera camera = First<Camera>(scene);

        if (camera == null)
        {
            return;
        }

        FrameFit fit = FitMap(layout);
        Undo.RecordObject(camera, UndoName);
        Undo.RecordObject(camera.transform, UndoName);
        camera.orthographic = true;
        camera.orthographicSize = fit.OrthographicSize;
        float z = camera.transform.position.z < 0f
            ? camera.transform.position.z
            : -10f;
        camera.transform.position = new Vector3(fit.CenterX, fit.CenterY, z);
    }

    private static void ShiftDressing(Scene scene, ConfluenceLayout layout)
    {
        List<ValleyDressingItem> items = FindAll<ValleyDressingItem>(scene);
        int shifted = 0;

        for (int i = 0; i < items.Count; i++)
        {
            Vector3 position = items[i].transform.position;

            if (!layout.TryCell(position, out Vector2Int cell) ||
                !Walkable(layout, cell))
            {
                continue;
            }

            Undo.RecordObject(items[i].transform, UndoName);
            items[i].transform.position = new Vector3(
                layout.Origin.x - 0.8f,
                layout.Origin.y + 0.6f + shifted * 0.2f,
                position.z);
            shifted++;
        }
    }

    private static void ApplyMatchChrome(Scene scene, StringBuilder report)
    {
        if (!scene.IsValid())
        {
            return;
        }

        GameObject boundary = FindOrCreate(scene, "SessionBoundary");

        if (boundary.GetComponent<SessionBoundary>() == null)
        {
            Undo.AddComponent<SessionBoundary>(boundary);
        }

        GameObject previewObject = FindOrCreate(scene, "RangePreview");
        RangePreview preview = previewObject.GetComponent<RangePreview>();

        if (preview == null)
        {
            preview = Undo.AddComponent<RangePreview>(previewObject);
        }

        SerializedObject previewData = new SerializedObject(preview);
        Assign(previewData, "selection", First<BuildSelection>(scene));
        Assign(previewData, "economy", First<EconomyManager>(scene));
        Assign(previewData, "gameManager", First<GameManager>(scene));
        Assign(previewData, "pauseManager", First<PauseManager>(scene));
        SerializedProperty plots = previewData.FindProperty("plots");
        List<BuildPlot> found = FindAll<BuildPlot>(scene);
        plots.arraySize = found.Count;

        for (int i = 0; i < found.Count; i++)
        {
            plots.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        }

        previewData.ApplyModifiedProperties();
        EducationalCollectionManager collection =
            First<EducationalCollectionManager>(scene);

        if (collection != null)
        {
            SerializedObject serialized = new SerializedObject(collection);
            SerializedProperty game = serialized.FindProperty("gameManager");

            if (game != null && game.objectReferenceValue == null)
            {
                game.objectReferenceValue = First<GameManager>(scene);
                serialized.ApplyModifiedProperties();
            }
        }

        EnsureMenuButton(First<LevelPausePanel>(scene), "menuButton", "Volver al menú");
        EnsureMenuButton(First<EndGameUI>(scene), "menuButton", "Volver al menú");
        CompactMatchHud.Apply(scene, report);
        report.AppendLine(scene.name + ": menú, vista previa y cierre de sesión.");
    }

    private static void EnsureMenu(StringBuilder report)
    {
        bool created = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) == null;
        Scene scene;

        if (!created)
        {
            if (!TryOpenAlone(MenuScenePath, out scene, report))
            {
                return;
            }
        }
        else if (!CanReplaceLoadedScenes(report))
        {
            return;
        }
        else
        {
            scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
        }

        if (SceneCamera(scene) == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, UndoName);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.2f, 0.14f, 1f);
            cameraObject.tag = "MainCamera";
        }

        if (First<EventSystem>(scene) == null)
        {
            GameObject events = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(events, UndoName);
            SceneManager.MoveGameObjectToScene(events, scene);
            events.AddComponent<EventSystem>();
            events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        GameObject canvasObject = FindOrCreate(scene, "MenuCanvas");
        Canvas canvas = canvasObject.GetComponent<Canvas>();

        if (canvas == null)
        {
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        TMP_Text title = EnsureLabel(canvasObject.transform, "Title", "Bastión del Valle", font, 48);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -120f);
        titleRect.sizeDelta = new Vector2(900f, 80f);
        Button continueButton = EnsureUiButton(canvasObject.transform, "ContinueButton", "Continuar", font, 0);
        Button newButton = EnsureUiButton(canvasObject.transform, "NewGameButton", "Nueva partida", font, 1);
        Button codexButton = EnsureUiButton(canvasObject.transform, "CodexButton", "Códice", font, 2);
        Button soundButton = EnsureUiButton(canvasObject.transform, "SoundButton", "Sonido", font, 3);
        Button quitButton = EnsureUiButton(canvasObject.transform, "QuitButton", "Salir", font, 4);
        TMP_Text status = EnsureLabel(canvasObject.transform, "Status", "Aún no hay una campaña para continuar.", font, 24);
        RectTransform statusRect = status.rectTransform;
        statusRect.anchorMin = new Vector2(0.5f, 0f);
        statusRect.anchorMax = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 80f);
        statusRect.sizeDelta = new Vector2(1100f, 70f);
        GameObject confirm = FindOrCreate(scene, "ConfirmNewGame");
        SceneManager.MoveGameObjectToScene(confirm, scene);
        confirm.transform.SetParent(canvasObject.transform, false);
        RectTransform confirmRect = confirm.GetComponent<RectTransform>();

        if (confirmRect == null)
        {
            confirmRect = confirm.AddComponent<RectTransform>();
        }

        confirmRect.anchorMin = new Vector2(0.5f, 0.5f);
        confirmRect.anchorMax = new Vector2(0.5f, 0.5f);
        confirmRect.sizeDelta = new Vector2(760f, 220f);
        Image confirmImage = confirm.GetComponent<Image>();

        if (confirmImage == null)
        {
            confirmImage = confirm.AddComponent<Image>();
            confirmImage.color = new Color(0.1f, 0.12f, 0.09f, 0.94f);
        }

        EnsureLabel(confirm.transform, "Question", "¿Borrar la campaña y conservar el sonido?", font, 28);
        Button confirmButton = EnsureUiButton(confirm.transform, "ConfirmButton", "Borrar campaña", font, 0);
        Button cancelButton = EnsureUiButton(confirm.transform, "CancelButton", "Cancelar", font, 1);
        confirm.SetActive(false);
        EducationalCollectionManager collection = First<EducationalCollectionManager>(scene);

        if (collection == null)
        {
            GameObject host = FindOrCreate(scene, "Campaign");
            collection = Undo.AddComponent<EducationalCollectionManager>(host);
        }

        SerializedObject collectionData = new SerializedObject(collection);
        collectionData.FindProperty("menuHost").boolValue = true;
        collectionData.ApplyModifiedProperties();
        MainMenuController controller = First<MainMenuController>(scene);

        if (controller == null)
        {
            controller = Undo.AddComponent<MainMenuController>(collection.gameObject);
        }

        CodexController codex = EnsureCodex(scene, canvasObject.transform, font);
        AudioSettingsPanel audio = EnsureAudio(scene, canvasObject.transform, collection, font);
        SerializedObject codexAudio = new SerializedObject(codex);
        Assign(codexAudio, "audioSettings", audio);
        codexAudio.ApplyModifiedProperties();
        SerializedObject menu = new SerializedObject(controller);
        Assign(menu, "collection", collection);
        Assign(menu, "codex", codex);
        Assign(menu, "audioSettings", audio);
        Assign(menu, "continueButton", continueButton);
        Assign(menu, "newGameButton", newButton);
        Assign(menu, "confirmButton", confirmButton);
        Assign(menu, "cancelButton", cancelButton);
        Assign(menu, "confirmRoot", confirm);
        Assign(menu, "statusText", status);
        menu.ApplyModifiedProperties();
        Bind(codexButton, controller, "OpenCodex");
        Bind(soundButton, controller, "OpenSound");
        Bind(quitButton, controller, "QuitGame");

        if (created)
        {
            EditorSceneManager.SaveScene(scene, MenuScenePath);
            report.AppendLine("MainMenu creado y guardado.");
        }
        else
        {
            EditorSceneManager.MarkSceneDirty(scene);
            report.AppendLine("MainMenu actualizado. Falta guardar la escena.");
        }
    }

    private static CodexController EnsureCodex(
        Scene scene,
        Transform canvas,
        TMP_FontAsset font)
    {
        CodexController existing = First<CodexController>(scene);

        if (existing != null)
        {
            return existing;
        }

        GameObject panel = FindOrCreate(scene, "CodexPanel");
        panel.transform.SetParent(canvas, false);
        RectTransform rect = panel.GetComponent<RectTransform>();

        if (rect == null)
        {
            rect = panel.AddComponent<RectTransform>();
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(80f, 80f);
        rect.offsetMax = new Vector2(-80f, -80f);
        Image image = panel.GetComponent<Image>();

        if (image == null)
        {
            image = panel.AddComponent<Image>();
            image.color = new Color(0.08f, 0.1f, 0.08f, 0.96f);
        }

        Button close = EnsureUiButton(panel.transform, "CloseCodex", "Cerrar", font, 0);
        CodexController codex = panel.AddComponent<CodexController>();
        SerializedObject serialized = new SerializedObject(codex);
        serialized.FindProperty("menuConsultation").boolValue = true;
        Assign(serialized, "collectionManager", First<EducationalCollectionManager>(scene));
        Assign(serialized, "panelRoot", panel);
        Assign(serialized, "closeButton", close);
        Assign(serialized, "titleText", EnsureLabel(panel.transform, "CodexTitle", "Memorias del valle", font, 36));
        Assign(serialized, "messageText", EnsureLabel(panel.transform, "CodexMessage", string.Empty, font, 24));
        Assign(serialized, "detailTitleText", EnsureLabel(panel.transform, "CodexDetail", string.Empty, font, 28));
        Assign(serialized, "detailDescriptionText", EnsureLabel(panel.transform, "CodexBody", string.Empty, font, 22));
        GameObject list = FindOrCreate(scene, "CodexList");
        list.transform.SetParent(panel.transform, false);
        RectTransform listRect = list.GetComponent<RectTransform>();

        if (listRect == null)
        {
            listRect = list.AddComponent<RectTransform>();
        }

        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(0.4f, 1f);
        listRect.offsetMin = new Vector2(24f, 100f);
        listRect.offsetMax = new Vector2(-12f, -90f);
        Assign(serialized, "listContent", listRect);
        GameObject entryObject =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CodexEntry.prefab");
        serialized.FindProperty("entryPrefab").objectReferenceValue =
            entryObject != null ? entryObject.GetComponent<CodexEntryView>() : null;
        AssignCatalog(serialized.FindProperty("catalog"));
        serialized.ApplyModifiedProperties();
        panel.SetActive(false);
        return codex;
    }

    private static void AssignCatalog(SerializedProperty catalog)
    {
        string[] paths =
        {
            "Assets/Data/CollectibleData_Water.asset",
            "Assets/Data/CollectibleDat_Pollinator.asset",
            "Assets/Data/CollectibleData_Soil.asset"
        };
        var found = new List<CollectibleData>();

        for (int i = 0; i < paths.Length; i++)
        {
            CollectibleData data =
                AssetDatabase.LoadAssetAtPath<CollectibleData>(paths[i]);

            if (data != null)
            {
                found.Add(data);
            }
        }

        catalog.arraySize = found.Count;

        for (int i = 0; i < found.Count; i++)
        {
            catalog.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        }
    }

    private static AudioSettingsPanel EnsureAudio(
        Scene scene,
        Transform canvas,
        EducationalCollectionManager collection,
        TMP_FontAsset font)
    {
        AudioSettingsPanel existing = First<AudioSettingsPanel>(scene);

        if (existing != null)
        {
            return existing;
        }

        GameObject panel = FindOrCreate(scene, "SoundPanel");
        panel.transform.SetParent(canvas, false);
        RectTransform rect = panel.GetComponent<RectTransform>();

        if (rect == null)
        {
            rect = panel.AddComponent<RectTransform>();
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(720f, 320f);
        Image image = panel.GetComponent<Image>();

        if (image == null)
        {
            image = panel.AddComponent<Image>();
            image.color = new Color(0.08f, 0.1f, 0.08f, 0.96f);
        }

        Slider music = EnsureSlider(panel.transform, "MusicSlider", 180f);
        Slider effects = EnsureSlider(panel.transform, "EffectsSlider", 40f);
        Button close = EnsureUiButton(panel.transform, "CloseSound", "Cerrar", font, 0);
        AudioSettingsPanel audio = panel.AddComponent<AudioSettingsPanel>();
        SerializedObject serialized = new SerializedObject(audio);
        serialized.FindProperty("menuHost").boolValue = true;
        Assign(serialized, "collection", collection);
        Assign(serialized, "panelRoot", panel);
        Assign(serialized, "closeButton", close);
        Assign(serialized, "musicSlider", music);
        Assign(serialized, "effectsSlider", effects);
        serialized.ApplyModifiedProperties();
        panel.SetActive(false);
        return audio;
    }

    private static void EnsureProjectiles(StringBuilder report)
    {
        FillSprite(
            "Assets/Prefabs/Projectile_Water.prefab",
            "Assets/Art/Projectiles/projectile_water_v001.png",
            report);
        FillSprite(
            "Assets/Prefabs/Projectile_Pollinator.prefab",
            "Assets/Art/Projectiles/projectile_pollen_v001.png",
            report);
        FillSprite(
            "Assets/Prefabs/Projectile_Condor.prefab",
            "Assets/Art/Projectiles/projectile_condor_v001.png",
            report);
        report.AppendLine("El Guardián y las Raíces conservan su presentación.");
    }

    private static void FillSprite(string prefabPath, string spritePath, StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        if (root == null)
        {
            report.AppendLine("No está " + prefabPath + ".");
            return;
        }

        SpriteRenderer renderer = root.GetComponentInChildren<SpriteRenderer>(true);
        bool changed = false;

        if (renderer == null)
        {
            report.AppendLine(root.name + " no tiene sprite.");
        }
        else if (renderer.sprite == null)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

            if (sprite == null)
            {
                report.AppendLine("Falta " + spritePath + ". Se conserva el prefab.");
            }
            else
            {
                renderer.sprite = sprite;
                changed = true;
                report.AppendLine("Asignado " + spritePath + ".");
            }
        }
        else
        {
            report.AppendLine(root.name + " conserva su sprite.");
        }

        if (renderer != null && renderer.sortingOrder < 0)
        {
            renderer.sortingOrder = 5;
            changed = true;
        }

        if (changed)
        {
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }

        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void EnsureBuildList(StringBuilder report)
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MenuScenePath, true)
        };
        bool keptSample = false;

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == MenuScenePath)
            {
                continue;
            }

            if (scene.path == "Assets/Scenes/SampleScene.unity")
            {
                scenes.Add(new EditorBuildSettingsScene(scene.path, false));
                keptSample = true;
                continue;
            }

            scenes.Add(new EditorBuildSettingsScene(scene.path, scene.enabled));
        }

        if (!keptSample &&
            AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/SampleScene.unity") != null)
        {
            scenes.Add(new EditorBuildSettingsScene(
                "Assets/Scenes/SampleScene.unity",
                false));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        report.AppendLine("La entrada del build es MainMenu. Los mapas siguen en la lista.");
    }

    private static bool TryOpenAlone(
        string path,
        out Scene scene,
        StringBuilder report)
    {
        scene = SceneManager.GetSceneByPath(path);
        bool already = scene.IsValid() && scene.isLoaded;

        if (already && SceneManager.sceneCount == 1)
        {
            return true;
        }

        if (!CanReplaceLoadedScenes(report))
        {
            return false;
        }

        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        return scene.IsValid();
    }

    private static bool CanReplaceLoadedScenes(StringBuilder report)
    {
        var dirty = new List<Scene>();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);

            if (loaded.isDirty)
            {
                dirty.Add(loaded);
            }
        }

        for (int i = 0; i < dirty.Count; i++)
        {
            Scene loaded = dirty[i];

            if (string.IsNullOrEmpty(loaded.path))
            {
                report.AppendLine(
                    "Hay una escena nueva sin ruta. No se descartó ni se cargó otra encima.");
                return false;
            }

            bool save = EditorUtility.DisplayDialog(
                "Guardar antes de cambiar de escena",
                loaded.name + " tiene cambios. Guardarla permite abrir la siguiente sola, sin montar las interfaces.",
                "Guardar",
                "Detener");

            if (!save || !EditorSceneManager.SaveScene(loaded))
            {
                report.AppendLine(
                    "No se cambió de escena. " + loaded.path + " sigue sin guardar.");
                return false;
            }
        }

        return true;
    }

    private static void EnsureMenuButton(Component host, string propertyName, string label)
    {
        if (host == null)
        {
            return;
        }

        SerializedObject serialized = new SerializedObject(host);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null || property.objectReferenceValue != null)
        {
            return;
        }

        Button style = host.GetComponentInChildren<Button>(true);

        if (style == null)
        {
            return;
        }

        GameObject button = Object.Instantiate(style.gameObject, style.transform.parent);
        button.name = label;
        Undo.RegisterCreatedObjectUndo(button, UndoName);
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);

        if (text != null)
        {
            text.text = label;
        }

        RectTransform rect = button.GetComponent<RectTransform>();

        if (rect != null)
        {
            rect.anchoredPosition += new Vector2(rect.sizeDelta.x + 16f, 0f);
        }

        property.objectReferenceValue = button.GetComponent<Button>();
        serialized.ApplyModifiedProperties();
    }

    private static Button EnsureUiButton(
        Transform parent,
        string name,
        string label,
        TMP_FontAsset font,
        int row)
    {
        Transform existing = parent.Find(name);
        GameObject buttonObject = existing != null
            ? existing.gameObject
            : new GameObject(name);
        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(buttonObject, UndoName);
            buttonObject.transform.SetParent(parent, false);
        }

        RectTransform rect = buttonObject.GetComponent<RectTransform>();

        if (rect == null)
        {
            rect = buttonObject.AddComponent<RectTransform>();
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 64f);
        rect.anchoredPosition = new Vector2(0f, 150f - row * 78f);
        Image image = buttonObject.GetComponent<Image>();

        if (image == null)
        {
            image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.24f, 0.32f, 0.2f, 1f);
        }

        Button button = buttonObject.GetComponent<Button>();

        if (button == null)
        {
            button = buttonObject.AddComponent<Button>();
        }

        EnsureLabel(buttonObject.transform, "Label", label, font, 26);
        return button;
    }

    private static Slider EnsureSlider(Transform parent, string name, float y)
    {
        Transform existing = parent.Find(name);

        if (existing != null)
        {
            return existing.GetComponent<Slider>();
        }

        GameObject sliderObject = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(sliderObject, UndoName);
        sliderObject.transform.SetParent(parent, false);
        RectTransform rect = sliderObject.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(460f, 28f);
        rect.anchoredPosition = new Vector2(0f, y);
        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        return slider;
    }

    private static TMP_Text EnsureLabel(
        Transform parent,
        string name,
        string value,
        TMP_FontAsset font,
        float size)
    {
        Transform existing = parent.Find(name);
        GameObject label = existing != null ? existing.gameObject : new GameObject(name);

        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(label, UndoName);
            label.transform.SetParent(parent, false);
        }

        RectTransform rect = label.GetComponent<RectTransform>();

        if (rect == null)
        {
            rect = label.AddComponent<RectTransform>();
        }

        rect.sizeDelta = new Vector2(640f, 70f);
        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();

        if (text == null)
        {
            text = label.AddComponent<TextMeshProUGUI>();
        }

        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.95f, 0.93f, 0.84f, 1f);
        text.raycastTarget = false;

        if (font != null)
        {
            text.font = font;
        }

        return text;
    }

    private static void Bind(Button button, MainMenuController controller, string method)
    {
        if (button == null)
        {
            return;
        }

        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (button.onClick.GetPersistentMethodName(i) == method &&
                button.onClick.GetPersistentTarget(i) == controller)
            {
                return;
            }
        }

        if (method == "OpenCodex")
        {
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                button.onClick,
                controller.OpenCodex);
        }
        else if (method == "OpenSound")
        {
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                button.onClick,
                controller.OpenSound);
        }
        else if (method == "QuitGame")
        {
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                button.onClick,
                controller.QuitGame);
        }
    }

    private static void CreateBase(Transform map, ConfluenceLayout layout, Material material)
    {
        if (material == null)
        {
            return;
        }

        Transform existing = map.Find("Terreno");

        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        FrameFit fit = FitMap(layout);
        float span = Mathf.Max(
            fit.ViewMaxX - fit.ViewMinX,
            fit.ViewMaxY - fit.ViewMinY) + 1f;
        CreateQuad(
            map,
            "Terreno",
            new Vector3(fit.CenterX, fit.CenterY, 0f),
            span,
            material,
            -30);
    }

    private static void CreateRibbon(
        Transform parent,
        string name,
        List<Vector3> centers,
        float size,
        Material material,
        int order)
    {
        if (material == null || centers.Count == 0)
        {
            return;
        }

        GameObject ribbon = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(ribbon, UndoName);
        ribbon.transform.SetParent(parent, false);
        MeshFilter filter = ribbon.AddComponent<MeshFilter>();
        MeshRenderer renderer = ribbon.AddComponent<MeshRenderer>();
        var mesh = new Mesh();
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        float half = size * 0.5f;

        for (int i = 0; i < centers.Count; i++)
        {
            int start = vertices.Count;
            Vector3 center = centers[i];
            vertices.Add(new Vector3(center.x - half, center.y - half, 0f));
            vertices.Add(new Vector3(center.x - half, center.y + half, 0f));
            vertices.Add(new Vector3(center.x + half, center.y + half, 0f));
            vertices.Add(new Vector3(center.x + half, center.y - half, 0f));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void CreateQuad(
        Transform parent,
        string name,
        Vector3 center,
        float size,
        Material material,
        int order,
        float width = 0f,
        float height = 0f)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Undo.RegisterCreatedObjectUndo(quad, UndoName);
        quad.transform.SetParent(parent, false);
        quad.transform.position = center;
        float quadWidth = width > 0f ? width : size;
        float quadHeight = height > 0f ? height : size;
        quad.transform.localScale = new Vector3(quadWidth, quadHeight, 1f);
        Object collider = quad.GetComponent<Collider>();

        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static bool Walkable(ConfluenceLayout layout, Vector2Int cell)
    {
        ConfluenceCell[] cells = layout.Cells;

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].x == cell.x &&
                cells[i].y == cell.y &&
                cells[i].walkable)
            {
                return true;
            }
        }

        return false;
    }

    private static ConfluencePlot FindPlot(List<ConfluencePlot> plots, int index)
    {
        for (int i = 0; i < plots.Count; i++)
        {
            if (plots[i].Index == index)
            {
                return plots[i];
            }
        }

        return plots.Count > index ? plots[index] : null;
    }

    private static ConfluenceAccess FindAccess(List<ConfluenceAccess> accesses, int index)
    {
        for (int i = 0; i < accesses.Count; i++)
        {
            if (accesses[i].Index == index)
            {
                return accesses[i];
            }
        }

        return null;
    }

    private static void SetInt(Object target, string propertyName, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).intValue = value;
        serialized.ApplyModifiedProperties();
    }

    private static void Assign(
        SerializedObject serialized,
        string propertyName,
        Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform child = parent.Find(name);

        if (child != null)
        {
            return child;
        }

        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, UndoName);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
        }
    }

    private static GameObject FindOrCreate(Scene scene, string name)
    {
        Transform found = FindNamed(scene, name);

        if (found != null)
        {
            return found.gameObject;
        }

        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, UndoName);
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    private static Transform FindNamed(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == name)
                {
                    return children[i];
                }
            }
        }

        return null;
    }

    private static Camera SceneCamera(Scene scene)
    {
        foreach (Camera camera in FindAll<Camera>(scene))
        {
            return camera;
        }

        return null;
    }

    private static T First<T>(Scene scene) where T : Component
    {
        List<T> all = FindAll<T>(scene);
        return all.Count == 0 ? null : all[0];
    }

    private static List<T> FindAll<T>(Scene scene) where T : Component
    {
        var list = new List<T>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            list.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return list;
    }
}
