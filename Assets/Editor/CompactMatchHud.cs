using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CompactMatchHud
{
    public const string MenuPath =
        "Bastion del Valle/Consolidacion/29C - Compactar HUD y Conectar Mapas";

    private const string UndoName = "Compactar HUD y Conectar Mapas";
    private const string Level01Path = "Assets/Scenes/Level_01.unity";
    private const string Level02Path = "Assets/Scenes/Level_02.unity";

    [MenuItem(MenuPath, false, 42)]
    private static void Repair()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
        {
            return;
        }

        var report = new StringBuilder();

        if (!Ready(report))
        {
            Debug.Log(report.ToString());
            return;
        }

        int group = Undo.GetCurrentGroup();
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);

        try
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (IsLevel(scene.path))
                {
                    Apply(scene, report);
                }
            }

            bool save = EditorUtility.DisplayDialog(
                "HUD compacto",
                "Se ajustaron las escenas de mapa que ya estaban abiertas.\n" +
                "Guardarlas deja el resultado revisable y no escribe escenas ajenas.",
                "Guardar escenas reparadas",
                "Revisar sin guardar");

            if (save)
            {
                SaveOpenLevels(report);
                OpenMissing(report);
            }
            else
            {
                report.AppendLine("No se guardó ninguna escena.");
            }

            Undo.CollapseUndoOperations(group);
            Debug.Log(report.ToString());
        }
        catch (System.Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogError("La compactación se revirtió. " + exception.Message);
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool CanRepair()
    {
        return !EditorApplication.isPlaying && !EditorApplication.isCompiling;
    }

    public static void Apply(Scene scene, StringBuilder report)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        HUDController hud = One<HUDController>(scene);

        if (hud == null)
        {
            return;
        }

        bool mapTwo = scene.path == Level02Path;
        Canvas canvasHost = hud.GetComponentInParent<Canvas>(true);

        if (canvasHost == null)
        {
            report.AppendLine(scene.name + " no tiene Canvas del HUD.");
            return;
        }

        RectTransform canvas = canvasHost.transform as RectTransform;
        SerializedObject serialized = new SerializedObject(hud);
        RectTransform top = Bar(canvas, "TopBar", HudReserve.TopBar, false, 0f);
        Place(
            top,
            Widget(serialized, "bastionText"),
            Widget(serialized, "resourcesText"),
            Widget(serialized, "waveText"),
            Widget(serialized, "enemiesText"),
            PauseButton(scene));
        Hide(Widget(serialized, "discoveriesText"));
        Hide(Widget(serialized, "waveStateText"));
        RectTransform notice = Bar(
            canvas,
            "NoticeBar",
            36f,
            false,
            -(HudReserve.TopBar + (mapTwo ? HudReserve.Habitat : 0f) + 8f));
        Place(notice, Widget(serialized, "statusText"));
        PrepareTexts(top);
        PrepareTexts(notice);

        if (mapTwo)
        {
            RectTransform habitat = FindRect(scene, "HabitatRow") ??
                Bar(canvas, "HabitatBar", HudReserve.Habitat, false, -HudReserve.TopBar);
            Adopt(canvas, habitat);
            Strip(habitat, HudReserve.Habitat, false, -HudReserve.TopBar);
            Plate(habitat, true);
            Place(
                habitat,
                Widget(serialized, "habitatWaterText"),
                Widget(serialized, "habitatSoilText"),
                Widget(serialized, "habitatLifeText"),
                Widget(serialized, "habitatHelpButton"));
            Assign(serialized, "habitatNoteRoot", Note(serialized, canvas));
            RectTransform tray = FindRect(scene, "BuildChoiceRow") ??
                Bar(canvas, "BuildTray", HudReserve.Tray, true, 0f);
            Adopt(canvas, tray);
            Strip(tray, HudReserve.Tray, true, 0f);
            Plate(tray, true);
            PlaceCards(scene, tray);
            AssignDetail(scene, canvas, tray);
            FoldUpperRow(scene, tray);
        }
        else
        {
            Hide(FindRect(scene, "HabitatRow"));
            Hide(FindRect(scene, "BuildChoiceRow"));
            Hide(FindRect(scene, "BuildChoiceRowUpper"));
        }

        float prepY = mapTwo ? HudReserve.Tray : 0f;
        RectTransform prep = Bar(canvas, "PrepBar", HudReserve.Preparation, true, prepY);
        Place(prep, FindRect(scene, "LessonRow"), FindRect(scene, "StartWaveButton"));
        Assign(serialized, "preparationRoot", prep.gameObject);
        serialized.ApplyModifiedProperties();
        MoveConsultations(scene);
        EnsureNext(scene);
        SilenceDecorations(canvas);
        Frame(scene, mapTwo, report);
        Cover(scene, report);
        report.AppendLine(scene.name + ": HUD compacto y resultado conectado.");
    }

    private static void PlaceCards(Scene scene, RectTransform tray)
    {
        List<BuildChoiceButton> cards = All<BuildChoiceButton>(scene);

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].transform.parent != tray)
            {
                Undo.SetTransformParent(cards[i].transform, tray, UndoName);
            }

            RectTransform rect = cards[i].GetComponent<RectTransform>();
            LayoutElement element = cards[i].GetComponent<LayoutElement>();

            if (element == null)
            {
                element = Undo.AddComponent<LayoutElement>(cards[i].gameObject);
            }

            element.minHeight = 96f;
            element.preferredHeight = 100f;
            element.flexibleWidth = 1f;
            Image icon = Icon(cards[i]);

            if (icon != null)
            {
                icon.rectTransform.anchorMin = new Vector2(0.5f, 1f);
                icon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                icon.rectTransform.pivot = new Vector2(0.5f, 1f);
                icon.rectTransform.sizeDelta = new Vector2(48f, 48f);
                icon.rectTransform.anchoredPosition = new Vector2(0f, -6f);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
        }
    }

    private static Image Icon(BuildChoiceButton card)
    {
        SerializedObject serialized = new SerializedObject(card);
        int index = serialized.FindProperty("index").intValue;
        SerializedProperty selection = serialized.FindProperty("selection");
        Sprite sprite = null;

        if (selection != null &&
            selection.objectReferenceValue is BuildSelection build &&
            build.DefenseAt(index) != null &&
            build.DefenseAt(index).Data != null)
        {
            TowerData data = build.DefenseAt(index).Data;
            sprite = data.Icon != null ? data.Icon : data.WorldSprite;
        }

        Transform child = card.transform.Find("CardIcon");
        Image image = child != null ? child.GetComponent<Image>() : null;

        if (sprite == null)
        {
            return image;
        }

        if (image == null)
        {
            var created = new GameObject("CardIcon", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(created, UndoName);
            created.transform.SetParent(card.transform, false);
            image = created.GetComponent<Image>();
        }

        image.sprite = sprite;
        return image;
    }

    private static void AssignDetail(
        Scene scene,
        RectTransform canvas,
        RectTransform tray)
    {
        BuildChoiceView view = tray.GetComponent<BuildChoiceView>();

        if (view == null)
        {
            view = One<BuildChoiceView>(scene);
        }

        if (view == null)
        {
            return;
        }

        RectTransform detail = FindRect(scene, "BuildChoiceDescription");

        if (detail != null)
        {
            Adopt(canvas, detail);
            Strip(detail, 120f, true, HudReserve.Tray + HudReserve.Preparation);
            detail.gameObject.SetActive(false);
        }

        Button button = null;
        Transform existing = tray.Find("DetalleButton");

        if (existing != null)
        {
            button = existing.GetComponent<Button>();
        }

        if (button == null)
        {
            Button source = tray.GetComponentInChildren<Button>(true);

            if (source != null)
            {
                GameObject clone = Object.Instantiate(source.gameObject, tray);
                Undo.RegisterCreatedObjectUndo(clone, UndoName);
            clone.name = "DetalleButton";
            Button cloned = clone.GetComponent<Button>();
            SerializedObject calls = new SerializedObject(cloned);
            SerializedProperty persistent = calls.FindProperty(
                "m_OnClick.m_PersistentCalls.m_Calls");

            if (persistent != null)
            {
                persistent.ClearArray();
                calls.ApplyModifiedProperties();
            }

            BuildChoiceButton choice = clone.GetComponent<BuildChoiceButton>();

                if (choice != null)
                {
                    Undo.DestroyObjectImmediate(choice);
                }

                button = clone.GetComponent<Button>();
                TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);

                if (label != null)
                {
                    label.text = "Detalle";
                }
            }
        }

        SerializedObject serialized = new SerializedObject(view);
        SerializedProperty root = serialized.FindProperty("detailRoot");
        SerializedProperty toggle = serialized.FindProperty("detailButton");

        if (root != null && detail != null)
        {
            root.objectReferenceValue = detail.gameObject;
        }

        if (toggle != null && button != null)
        {
            toggle.objectReferenceValue = button;
            LayoutElement element = button.GetComponent<LayoutElement>();

            if (element == null)
            {
                element = Undo.AddComponent<LayoutElement>(button.gameObject);
            }

            element.preferredWidth = 150f;
            element.flexibleWidth = 0f;
            element.minHeight = 48f;
        }

        serialized.ApplyModifiedProperties();
    }

    private static void FoldUpperRow(Scene scene, RectTransform tray)
    {
        RectTransform upper = FindRect(scene, "BuildChoiceRowUpper");

        if (upper == null)
        {
            return;
        }

        List<BuildChoiceButton> cards = new List<BuildChoiceButton>();
        cards.AddRange(upper.GetComponentsInChildren<BuildChoiceButton>(true));

        for (int i = 0; i < cards.Count; i++)
        {
            Undo.SetTransformParent(cards[i].transform, tray, UndoName);
        }

        if (upper.GetComponentsInChildren<BuildChoiceButton>(true).Length == 0)
        {
            Undo.DestroyObjectImmediate(upper.gameObject);
        }
    }

    private static GameObject Note(SerializedObject hud, RectTransform canvas)
    {
        SerializedProperty cause = hud.FindProperty("habitatCauseText");
        GameObject root = cause != null && cause.objectReferenceValue is Component widget
            ? widget.gameObject
            : null;

        if (root == null)
        {
            return null;
        }

        RectTransform rect = root.GetComponent<RectTransform>();

        if (rect != null && rect.parent != canvas)
        {
            Undo.SetTransformParent(rect, canvas, UndoName);
        }

        if (rect != null)
        {
            Strip(rect, 88f, false, -(HudReserve.TopBar + HudReserve.Habitat + 8f));
        }

        root.SetActive(false);
        return root;
    }

    private static void MoveConsultations(Scene scene)
    {
        LevelPausePanel pause = One<LevelPausePanel>(scene);

        if (pause == null)
        {
            return;
        }

        SerializedObject serialized = new SerializedObject(pause);
        SerializedProperty panel = serialized.FindProperty("panelRoot");

        if (panel == null || panel.objectReferenceValue is not GameObject root)
        {
            return;
        }

        RectTransform extras = root.transform.Find("PauseExtras") as RectTransform;

        if (extras == null)
        {
            var created = new GameObject(
                "PauseExtras",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup));
            Undo.RegisterCreatedObjectUndo(created, UndoName);
            created.transform.SetParent(root.transform, false);
            extras = created.GetComponent<RectTransform>();
        }

        Strip(extras, 56f, false, -12f);
        HorizontalLayoutGroup layout = extras.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        MoveButton(scene, One<CodexController>(scene), "openButton", extras, "Códice");
        MoveButton(scene, One<AudioSettingsPanel>(scene), "openButton", extras, "Sonido");
    }

    private static void MoveButton(
        Scene scene,
        Component host,
        string propertyName,
        RectTransform parent,
        string label)
    {
        if (host == null)
        {
            return;
        }

        SerializedObject serialized = new SerializedObject(host);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null || property.objectReferenceValue is not Button button)
        {
            return;
        }

        if (button.transform.parent != parent)
        {
            Undo.SetTransformParent(button.transform, parent, UndoName);
        }

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);

        if (text != null)
        {
            text.text = label;
            text.fontSize = Mathf.Max(18f, text.fontSize);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        LayoutElement element = button.GetComponent<LayoutElement>();

        if (element == null)
        {
            element = Undo.AddComponent<LayoutElement>(button.gameObject);
        }

        element.minHeight = 48f;
        element.preferredWidth = 180f;
    }

    private static void EnsureNext(Scene scene)
    {
        EndGameUI end = One<EndGameUI>(scene);

        if (end == null)
        {
            return;
        }

        SerializedObject serialized = new SerializedObject(end);
        SerializedProperty next = serialized.FindProperty("nextButton");
        Button retry = serialized.FindProperty("retryButton").objectReferenceValue as Button;

        if (next != null && next.objectReferenceValue == null && retry != null)
        {
            GameObject clone = Object.Instantiate(retry.gameObject, retry.transform.parent);
            Undo.RegisterCreatedObjectUndo(clone, UndoName);
            clone.name = "NextLevelButton";
            RectTransform source = retry.GetComponent<RectTransform>();
            RectTransform rect = clone.GetComponent<RectTransform>();
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.sizeDelta = source.sizeDelta;
            rect.anchoredPosition = source.anchoredPosition +
                new Vector2(0f, source.sizeDelta.y + 12f);
            TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                label.text = "Siguiente nivel";
            }

            clone.SetActive(false);
            next.objectReferenceValue = clone.GetComponent<Button>();
        }

        AssignSameScene(serialized, "collectionManager", One<EducationalCollectionManager>(scene));
        serialized.ApplyModifiedProperties();
    }

    private static void Frame(Scene scene, bool mapTwo, StringBuilder report)
    {
        Camera camera = MainCamera(scene);

        if (camera == null || !BoundsOf(scene, out float minX, out float maxX, out float minY, out float maxY))
        {
            return;
        }

        FrameFit fit = PlayfieldFrame.Fit(
            minX,
            maxX,
            minY,
            maxY,
            HudReserve.TopFraction(mapTwo),
            HudReserve.BottomFraction(mapTwo),
            16f / 9f,
            1.2f);
        Vector3 position = camera.transform.position;

        if (camera.orthographic &&
            Mathf.Abs(camera.orthographicSize - fit.OrthographicSize) < 0.02f &&
            Mathf.Abs(position.x - fit.CenterX) < 0.02f &&
            Mathf.Abs(position.y - fit.CenterY) < 0.02f)
        {
            return;
        }

        Undo.RecordObject(camera, UndoName);
        Undo.RecordObject(camera.transform, UndoName);
        camera.orthographic = true;
        camera.orthographicSize = fit.OrthographicSize;
        float z = position.z < 0f ? position.z : -10f;
        camera.transform.position = new Vector3(fit.CenterX, fit.CenterY, z);
        report.AppendLine(scene.name + ": la cámara reserva el HUD compacto.");
    }

    private static void Cover(Scene scene, StringBuilder report)
    {
        Camera camera = MainCamera(scene);
        Renderer terrain = Background(scene);

        if (camera == null || !camera.orthographic || terrain == null)
        {
            return;
        }

        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * (16f / 9f);
        Vector3 center = camera.transform.position;
        Bounds bounds = terrain.bounds;

        if (bounds.min.x <= center.x - halfWidth &&
            bounds.max.x >= center.x + halfWidth &&
            bounds.min.y <= center.y - halfHeight &&
            bounds.max.y >= center.y + halfHeight)
        {
            return;
        }

        float factor = Mathf.Max(
            1f,
            bounds.size.x <= 0.01f ? 1f : (halfWidth * 2f + 1f) / bounds.size.x,
            bounds.size.y <= 0.01f ? 1f : (halfHeight * 2f + 1f) / bounds.size.y);
        Undo.RecordObject(terrain.transform, UndoName);
        Vector3 scale = terrain.transform.localScale;
        terrain.transform.localScale = new Vector3(
            scale.x * factor,
            scale.y * factor,
            scale.z);
        Bounds grown = terrain.bounds;
        Vector3 position = terrain.transform.position;
        terrain.transform.position = new Vector3(
            position.x + center.x - grown.center.x,
            position.y + center.y - grown.center.y,
            position.z);
        report.AppendLine(scene.name + ": el terreno cubre el encuadre.");
    }

    private static void SilenceDecorations(RectTransform canvas)
    {
        Image[] images = canvas.GetComponentsInChildren<Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            Selectable selectable = images[i].GetComponent<Selectable>();

            if (selectable != null && selectable.targetGraphic == images[i])
            {
                continue;
            }

            string parentName = images[i].transform.parent != null
                ? images[i].transform.parent.name
                : string.Empty;

            if (images[i].gameObject.name == "TopBar" ||
                images[i].gameObject.name == "PrepBar" ||
                images[i].gameObject.name == "HabitatBar" ||
                images[i].gameObject.name == "HabitatRow" ||
                images[i].gameObject.name == "BuildChoiceRow" ||
                images[i].gameObject.name == "BuildTray" ||
                parentName == "PauseExtras")
            {
                continue;
            }

            if (images[i].GetComponent<Button>() != null ||
                images[i].GetComponent<Slider>() != null)
            {
                continue;
            }

            if (images[i].raycastTarget)
            {
                Undo.RecordObject(images[i], UndoName);
                images[i].raycastTarget = false;
            }
        }
    }

    private static RectTransform Bar(
        RectTransform parent,
        string name,
        float height,
        bool bottom,
        float y)
    {
        Transform found = parent.Find(name);
        RectTransform rect = found as RectTransform;

        if (rect == null)
        {
            var created = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            Undo.RegisterCreatedObjectUndo(created, UndoName);
            created.transform.SetParent(parent, false);
            rect = created.GetComponent<RectTransform>();
        }

        Strip(rect, height, bottom, y);
        HorizontalLayoutGroup layout = rect.GetComponent<HorizontalLayoutGroup>();

        if (layout == null)
        {
            layout = Undo.AddComponent<HorizontalLayoutGroup>(rect.gameObject);
        }

        layout.padding = new RectOffset(12, 12, 6, 6);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        Plate(rect, name != "NoticeBar");
        ContentSizeFitter fitter = rect.GetComponent<ContentSizeFitter>();

        if (fitter != null)
        {
            Undo.DestroyObjectImmediate(fitter);
        }

        return rect;
    }

    private static void Adopt(RectTransform canvas, RectTransform child)
    {
        if (child != null && child.parent != canvas)
        {
            Undo.SetTransformParent(child, canvas, UndoName);
        }
    }

    private static void Plate(RectTransform rect, bool block)
    {
        Image image = rect.GetComponent<Image>();

        if (image == null && block)
        {
            image = Undo.AddComponent<Image>(rect.gameObject);
        }

        if (image == null)
        {
            return;
        }

        Color plate = new Color(0.1f, 0.14f, 0.12f, 0.92f);

        if (image.raycastTarget == block &&
            (!block || image.sprite != null || image.color == plate))
        {
            return;
        }

        Undo.RecordObject(image, UndoName);
        image.raycastTarget = block;

        if (block && image.sprite == null)
        {
            image.color = plate;
        }
    }

    private static void Strip(RectTransform rect, float height, bool bottom, float y)
    {
        Vector2 min = new Vector2(0f, bottom ? 0f : 1f);
        Vector2 max = new Vector2(1f, bottom ? 0f : 1f);
        Vector2 pivot = new Vector2(0.5f, bottom ? 0f : 1f);

        if (rect.anchorMin == min &&
            rect.anchorMax == max &&
            rect.pivot == pivot &&
            Mathf.Abs(rect.sizeDelta.y - height) < 0.1f &&
            Mathf.Abs(rect.anchoredPosition.y - y) < 0.1f &&
            rect.localScale == Vector3.one)
        {
            return;
        }

        Undo.RecordObject(rect, UndoName);
        rect.anchorMin = new Vector2(0f, bottom ? 0f : 1f);
        rect.anchorMax = new Vector2(1f, bottom ? 0f : 1f);
        rect.pivot = new Vector2(0.5f, bottom ? 0f : 1f);
        rect.sizeDelta = new Vector2(0f, height);
        rect.anchoredPosition = new Vector2(0f, bottom ? y : y);
        rect.localScale = Vector3.one;
    }

    private static void Place(RectTransform parent, params Component[] widgets)
    {
        for (int i = 0; i < widgets.Length; i++)
        {
            if (widgets[i] == null)
            {
                continue;
            }

            if (widgets[i].transform.parent != parent)
            {
                Undo.SetTransformParent(widgets[i].transform, parent, UndoName);
            }

            LayoutElement element = widgets[i].GetComponent<LayoutElement>();

            if (element == null)
            {
                element = Undo.AddComponent<LayoutElement>(widgets[i].gameObject);
            }

            element.minHeight = 48f;
            element.flexibleWidth = widgets[i].GetComponent<Button>() != null ? 0f : 1f;

            if (widgets[i].GetComponent<Button>() != null)
            {
                element.preferredWidth = 170f;
            }
        }
    }

    private static void PrepareTexts(RectTransform parent)
    {
        TMP_Text[] texts = parent.GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            Undo.RecordObject(texts[i], UndoName);
            texts[i].fontSize = Mathf.Max(20f, texts[i].fontSize);
            texts[i].enableAutoSizing = false;
            texts[i].enableWordWrapping = false;
            texts[i].overflowMode = TextOverflowModes.Ellipsis;
            texts[i].raycastTarget = false;
        }
    }

    private static void Hide(Component widget)
    {
        if (widget != null && widget.gameObject.activeSelf)
        {
            Undo.RecordObject(widget.gameObject, UndoName);
            widget.gameObject.SetActive(false);
        }
    }

    private static Component Widget(SerializedObject serialized, string name)
    {
        SerializedProperty property = serialized.FindProperty(name);
        return property == null ? null : property.objectReferenceValue as Component;
    }

    private static Button PauseButton(Scene scene)
    {
        LevelPausePanel pause = One<LevelPausePanel>(scene);

        if (pause == null)
        {
            return null;
        }

        SerializedObject serialized = new SerializedObject(pause);
        SerializedProperty property = serialized.FindProperty("pauseButton");
        return property == null ? null : property.objectReferenceValue as Button;
    }

    private static void Assign(SerializedObject serialized, string name, Object value)
    {
        SerializedProperty property = serialized.FindProperty(name);

        if (property != null && property.objectReferenceValue != value)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void AssignSameScene(
        SerializedObject serialized,
        string name,
        Component value)
    {
        SerializedProperty property = serialized.FindProperty(name);

        if (property != null &&
            property.objectReferenceValue == null &&
            value != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static bool BoundsOf(
        Scene scene,
        out float minX,
        out float maxX,
        out float minY,
        out float maxY)
    {
        minX = float.PositiveInfinity;
        maxX = float.NegativeInfinity;
        minY = float.PositiveInfinity;
        maxY = float.NegativeInfinity;
        int count = 0;
        Include(All<BuildPlot>(scene), ref minX, ref maxX, ref minY, ref maxY, ref count);
        Include(All<BastionHealth>(scene), ref minX, ref maxX, ref minY, ref maxY, ref count);
        Include(All<TowerController>(scene), ref minX, ref maxX, ref minY, ref maxY, ref count);
        List<Renderer> renderers = All<Renderer>(scene);

        for (int i = 0; i < renderers.Count; i++)
        {
            string name = renderers[i].gameObject.name;

            if (!name.StartsWith("Cell_") &&
                !name.StartsWith("Channel_") &&
                !name.StartsWith("Bridge_") &&
                name != "Camino" &&
                name != "Canales")
            {
                continue;
            }

            Bounds bounds = renderers[i].bounds;
            minX = Mathf.Min(minX, bounds.min.x);
            maxX = Mathf.Max(maxX, bounds.max.x);
            minY = Mathf.Min(minY, bounds.min.y);
            maxY = Mathf.Max(maxY, bounds.max.y);
            count++;
        }

        return count >= 2 && maxX > minX && maxY > minY;
    }

    private static void Include<T>(
        List<T> components,
        ref float minX,
        ref float maxX,
        ref float minY,
        ref float maxY,
        ref int count) where T : Component
    {
        for (int i = 0; i < components.Count; i++)
        {
            Renderer renderer = components[i].GetComponentInChildren<Renderer>(true);
            Bounds bounds = renderer != null
                ? renderer.bounds
                : new Bounds(components[i].transform.position, Vector3.one);
            minX = Mathf.Min(minX, bounds.min.x);
            maxX = Mathf.Max(maxX, bounds.max.x);
            minY = Mathf.Min(minY, bounds.min.y);
            maxY = Mathf.Max(maxY, bounds.max.y);
            count++;
        }
    }

    private static Renderer Background(Scene scene)
    {
        Renderer best = null;
        float area = -1f;
        List<Renderer> renderers = All<Renderer>(scene);

        for (int i = 0; i < renderers.Count; i++)
        {
            string name = renderers[i].gameObject.name;

            if (name != "Terreno" && name != "Ground")
            {
                continue;
            }

            float size = renderers[i].bounds.size.x * renderers[i].bounds.size.y;

            if (size > area)
            {
                best = renderers[i];
                area = size;
            }
        }

        return best;
    }

    private static Camera MainCamera(Scene scene)
    {
        List<Camera> cameras = All<Camera>(scene);

        for (int i = 0; i < cameras.Count; i++)
        {
            if (cameras[i].CompareTag("MainCamera"))
            {
                return cameras[i];
            }
        }

        return cameras.Count == 0 ? null : cameras[0];
    }

    private static bool Ready(StringBuilder report)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.isDirty && !IsLevel(scene.path) &&
                scene.path != "Assets/Scenes/MainMenu.unity")
            {
                report.AppendLine(
                    "Hay cambios sin guardar fuera de los mapas: " + scene.path);
                return false;
            }
        }

        return true;
    }

    private static void SaveOpenLevels(StringBuilder report)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!IsLevel(scene.path) || !scene.isDirty)
            {
                continue;
            }

            report.AppendLine(EditorSceneManager.SaveScene(scene)
                ? "Guardada " + scene.path
                : "No se pudo guardar " + scene.path);
        }
    }

    private static void OpenMissing(StringBuilder report)
    {
        string[] paths = { Level01Path, Level02Path };

        for (int i = 0; i < paths.Length; i++)
        {
            Scene loaded = SceneManager.GetSceneByPath(paths[i]);

            if (loaded.IsValid() && loaded.isLoaded)
            {
                continue;
            }

            if (!CanSwitch(report))
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(paths[i], OpenSceneMode.Single);
            Apply(scene, report);

            if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
            {
                report.AppendLine("No se pudo guardar " + paths[i]);
                return;
            }
        }
    }

    private static bool CanSwitch(StringBuilder report)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!scene.isDirty)
            {
                continue;
            }

            if (string.IsNullOrEmpty(scene.path) ||
                !EditorUtility.DisplayDialog(
                    "Guardar antes de cambiar de escena",
                    scene.name + " tiene cambios. Guardarla evita descartarlos.",
                    "Guardar",
                    "Detener") ||
                !EditorSceneManager.SaveScene(scene))
            {
                report.AppendLine("No se abrió otra escena.");
                return false;
            }
        }

        return true;
    }

    private static bool IsLevel(string path)
    {
        return path == Level01Path || path == Level02Path;
    }

    private static T One<T>(Scene scene) where T : Component
    {
        List<T> found = All<T>(scene);
        return found.Count == 0 ? null : found[0];
    }

    private static RectTransform FindRect(Scene scene, string name)
    {
        List<Transform> transforms = All<Transform>(scene);

        for (int i = 0; i < transforms.Count; i++)
        {
            if (transforms[i].name == name)
            {
                return transforms[i] as RectTransform;
            }
        }

        return null;
    }

    private static List<T> All<T>(Scene scene) where T : Component
    {
        var list = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            list.AddRange(roots[i].GetComponentsInChildren<T>(true));
        }

        return list;
    }
}
