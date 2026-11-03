using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfluenceInterfaceRepair
{
    private const string MenuPath =
        "Bastion del Valle/Mapas/26B - Corregir Interfaz y Cierre";

    private const string UndoName = "Corregir Interfaz y Cierre";
    private const string Level02Path = "Assets/Scenes/Level_02.unity";
    private const string GuardianPath = "Assets/Data/TowerData_Basic.asset";
    private const float CardGap = 12f;
    private const float MinimumCard = 220f;
    private const float CardHeight = 84f;
    public const float LessonBandHeight = 96f;
    public const float HabitatBandHeight = 84f;
    private const float DescriptionHeight = 52f;
    private const float StackGap = 8f;
    private const float ActorPadding = 1f;
    private const float FrameSlack = 0.35f;

    [MenuItem(MenuPath, false, 27)]
    private static void Repair()
    {
        if (EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling)
        {
            Report("La corrección solo puede ejecutarse fuera de Play Mode.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (scene.path != Level02Path)
        {
            Report("Abre Level_02 y vuelve a ejecutar la acción. No se modificó ninguna escena.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        var report = new StringBuilder();

        try
        {
            Apply(scene, report, true);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            report.AppendLine("Level_02 quedó marcada como modificada. Guárdala desde el editor.");
            report.AppendLine("Level_01 no se abrió ni se modificó.");
            Report(report.ToString());
        }
        catch (System.Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(exception);
            Report("No se corrigió la interfaz.\n" + exception.Message);
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenu()
    {
        return !EditorApplication.isPlaying &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling;
    }

    public static void Apply(Scene scene, StringBuilder report, bool restoreGuardianCost)
    {
        if (restoreGuardianCost)
        {
            RestoreGuardianCost(report);
        }

        ArrangeSelector(scene, report);
        AssignStartGame(scene);
        ReportWaves(scene, report);
        CompactMatchHud.Apply(scene, report);
    }

    private static void RestoreGuardianCost(StringBuilder report)
    {
        TowerData data = AssetDatabase.LoadAssetAtPath<TowerData>(GuardianPath);

        if (data == null)
        {
            report.AppendLine("No está TowerData_Basic.");
            return;
        }

        if (data.BuildCost == 30)
        {
            report.AppendLine("El Guardián ya cuesta 30. La tarjeta lee ese dato.");
            return;
        }

        if (data.BuildCost != 40)
        {
            report.AppendLine(
                "El Guardián conserva un coste personalizado de " +
                data.BuildCost + "."
            );
            return;
        }

        SerializedObject serialized = new(data);
        serialized.FindProperty("buildCost").intValue = 30;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        report.AppendLine(
            "TowerData_Basic guardaba 40 desde el cierre del bloque 06, " +
            "sin una decisión de balance. La tarjeta lee BuildCost. Se restauró 30. " +
            "Las demás defensas no cambiaron."
        );
    }

    private static void ArrangeSelector(Scene scene, StringBuilder report)
    {
        Transform row = FindNamed(scene, "BuildChoiceRow");
        Transform hud = FindNamed(scene, "HUD");
        BuildSelection selection = FindOne<BuildSelection>(scene);

        if (row == null || hud == null || selection == null)
        {
            report.AppendLine("Falta la fila de construcción, el HUD o la selección.");
            return;
        }

        List<BuildChoiceButton> choices = FindAll<BuildChoiceButton>(scene);
        var kept = new BuildChoiceButton[selection.Count];

        foreach (BuildChoiceButton choice in choices)
        {
            SerializedObject serialized = new(choice);
            int index = serialized.FindProperty("index").intValue;

            if (index < 0 || index >= kept.Length)
            {
                report.AppendLine(choice.name + " tiene un índice fuera de la selección y se conservó.");
                continue;
            }

            if (kept[index] != null)
            {
                Undo.DestroyObjectImmediate(choice.gameObject);
                report.AppendLine("Se retiró una tarjeta duplicada de " + choice.name + ".");
                continue;
            }

            kept[index] = choice;
        }

        int present = 0;

        for (int i = 0; i < kept.Length; i++)
        {
            if (kept[i] != null)
            {
                present++;
            }
        }

        CanvasScaler scaler = hud.GetComponentInParent<CanvasScaler>();
        float width = scaler != null &&
            scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
            ? scaler.referenceResolution.x
            : hud.GetComponent<RectTransform>().rect.width;

        if (width < 100f)
        {
            width = 1920f;
        }

        RowPlan plan = SelectorLayout.Plan(width - 64f, present, CardGap, MinimumCard);
        Transform upper = plan.Rows == 2
            ? EnsureRow(hud, "BuildChoiceRowUpper", row)
            : FindNamed(scene, "BuildChoiceRowUpper");
        int placed = 0;

        for (int i = 0; i < kept.Length; i++)
        {
            if (kept[i] == null)
            {
                report.AppendLine("La defensa " + i + " no tiene tarjeta.");
                continue;
            }

            Transform parent = plan.Rows == 2 && placed < plan.FirstCount && upper != null
                ? upper
                : row;
            placed++;

            if (kept[i].transform.parent != parent)
            {
                Undo.SetTransformParent(kept[i].transform, parent, UndoName);
            }

            PrepareCard(kept[i], plan.CardWidth);
        }

        if (upper != null && plan.Rows == 1)
        {
            Undo.DestroyObjectImmediate(upper.gameObject);
            upper = null;
            report.AppendLine("Las tarjetas volvieron a una sola fila.");
        }

        ConfigureRow(row, row.GetComponent<Image>() != null);
        if (upper != null)
        {
            ConfigureRow(upper, true);
        }

        TMP_Text description = EnsureDescription(scene, hud);
        float selectorTop = Stack(scene, hud, row, upper, description);
        BindView(row, selection, kept, description);
        RemoveEmptyRow(scene, "AirChoiceRow");
        report.AppendLine(
            present + " tarjetas en " + plan.Rows +
            (plan.Rows == 1 ? " fila" : " filas") +
            ", con la descripción aparte."
        );
        FramePlayfield(scene, selectorTop, report);
    }

    private static void PrepareCard(BuildChoiceButton choice, float width)
    {
        LayoutElement element = choice.GetComponent<LayoutElement>();

        if (element == null)
        {
            element = Undo.AddComponent<LayoutElement>(choice.gameObject);
        }

        Undo.RecordObject(element, UndoName);
        element.minWidth = 0f;
        element.preferredWidth = width;
        element.flexibleWidth = 0f;
        element.minHeight = 64f;
        element.preferredHeight = 68f;
        element.flexibleHeight = 0f;
        TMP_Text label = choice.GetComponentInChildren<TMP_Text>(true);

        if (label == null)
        {
            return;
        }

        Undo.RecordObject(label, UndoName);
        label.enableAutoSizing = false;
        label.enableWordWrapping = true;
        label.alignment = TextAlignmentOptions.Center;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;

        if (label.fontSize < 18f)
        {
            label.fontSize = 22f;
        }
    }

    private static void ConfigureRow(Transform row, bool paint)
    {
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();

        if (layout == null)
        {
            layout = Undo.AddComponent<HorizontalLayoutGroup>(row.gameObject);
        }

        Undo.RecordObject(layout, UndoName);
        layout.padding = new RectOffset(16, 16, 8, 8);
        layout.spacing = CardGap;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = row.GetComponent<ContentSizeFitter>();

        if (fitter != null)
        {
            Undo.RecordObject(fitter, UndoName);
            fitter.enabled = false;
        }

        if (!paint)
        {
            return;
        }

        Image image = row.GetComponent<Image>();

        if (image == null)
        {
            image = Undo.AddComponent<Image>(row.gameObject);
            image.color = new Color(0.149f, 0.239f, 0.188f, 0.94f);
        }

        Undo.RecordObject(image, UndoName);
        image.raycastTarget = true;
    }

    private static TMP_Text EnsureDescription(Scene scene, Transform hud)
    {
        Transform existing = FindNamed(scene, "BuildChoiceDescription");

        if (existing == null)
        {
            var created = new GameObject("BuildChoiceDescription", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(created, UndoName);
            created.transform.SetParent(hud, false);
            existing = created.transform;
        }
        else if (existing.parent != hud)
        {
            Undo.SetTransformParent(existing, hud, UndoName);
        }

        LayoutElement element = existing.GetComponent<LayoutElement>();

        if (element != null)
        {
            Undo.RecordObject(element, UndoName);
            element.ignoreLayout = true;
            element.flexibleWidth = 0f;
        }

        TMP_Text text = existing.GetComponent<TMP_Text>();

        if (text == null)
        {
            text = Undo.AddComponent<TextMeshProUGUI>(existing.gameObject);
        }

        Undo.RecordObject(text, UndoName);
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        text.alignment = TextAlignmentOptions.Center;
        text.overflowMode = TextOverflowModes.Ellipsis;

        if (text.fontSize < 18f)
        {
            text.fontSize = 22f;
        }

        Image image = existing.GetComponent<Image>();

        if (image != null)
        {
            Undo.RecordObject(image, UndoName);
            image.raycastTarget = false;
        }

        return text;
    }

    private static float Stack(
        Scene scene,
        Transform hud,
        Transform cards,
        Transform upper,
        TMP_Text description)
    {
        float floor = 0f;
        floor = Mathf.Max(floor, BarTop(FindNamed(scene, "BottomRow"), hud));
        floor = Mathf.Max(floor, BarTop(FindNamed(scene, "MessageRow"), hud));
        float y = floor + StackGap;
        Place(cards, y, CardHeight);
        y += CardHeight + StackGap;

        if (upper != null)
        {
            Place(upper, y, CardHeight);
            y += CardHeight + StackGap;
        }

        Place(description.transform, y, DescriptionHeight);
        float top = y + DescriptionHeight;
        Transform lesson = FindNamed(scene, "LessonRow");

        if (lesson != null)
        {
            float lessonY = top + StackGap;
            Place(lesson, lessonY, LessonBandHeight);
            top = lessonY + LessonBandHeight;
        }

        Transform habitat = FindNamed(scene, "HabitatRow");

        if (habitat != null)
        {
            float habitatY = top + StackGap;
            Place(habitat, habitatY, HabitatBandHeight);
            top = habitatY + HabitatBandHeight;
        }

        return top;
    }

    private static void Place(Transform target, float y, float height)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        Undo.RecordObject(rect, UndoName);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(-32f, height);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.localScale = Vector3.one;
    }

    private static void BindView(
        Transform row,
        BuildSelection selection,
        BuildChoiceButton[] kept,
        TMP_Text description)
    {
        BuildChoiceView view = row.GetComponent<BuildChoiceView>();

        if (view == null)
        {
            view = Undo.AddComponent<BuildChoiceView>(row.gameObject);
        }

        SerializedObject serialized = new(view);
        serialized.FindProperty("selection").objectReferenceValue = selection;
        serialized.FindProperty("description").objectReferenceValue = description;
        SerializedProperty buttons = serialized.FindProperty("buttons");
        SerializedProperty labels = serialized.FindProperty("labels");
        buttons.arraySize = kept.Length;
        labels.arraySize = kept.Length;

        for (int i = 0; i < kept.Length; i++)
        {
            Button button = kept[i] == null ? null : kept[i].GetComponent<Button>();
            TMP_Text label = kept[i] == null
                ? null
                : kept[i].GetComponentInChildren<TMP_Text>(true);
            buttons.GetArrayElementAtIndex(i).objectReferenceValue = button;
            labels.GetArrayElementAtIndex(i).objectReferenceValue = label;
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(view);
    }

    private static void FramePlayfield(Scene scene, float selectorTop, StringBuilder report)
    {
        Camera camera = FindCamera(scene);
        Transform hud = FindNamed(scene, "HUD");
        Transform top = FindNamed(scene, "TopRow");

        if (camera == null || hud == null)
        {
            report.AppendLine("No se ajustó la cámara: falta la cámara o el HUD.");
            return;
        }

        if (!TryContent(scene, out float minX, out float maxX, out float minY, out float maxY))
        {
            report.AppendLine("No se ajustó la cámara: no hay parcelas ni accesos.");
            return;
        }

        CanvasScaler scaler = hud.GetComponentInParent<CanvasScaler>();
        float height = scaler != null ? scaler.referenceResolution.y : 1080f;

        if (height < 100f)
        {
            height = 1080f;
        }

        float bottom = selectorTop / height;
        float topFraction = TopFraction(top, height);
        FrameFit frame = PlayfieldFrame.Fit(
            minX,
            maxX,
            minY,
            maxY,
            topFraction,
            bottom,
            16f / 9f,
            FrameSlack);
        Undo.RecordObject(camera, UndoName);
        Undo.RecordObject(camera.transform, UndoName);
        camera.orthographic = true;
        camera.orthographicSize = frame.OrthographicSize;
        Vector3 position = camera.transform.position;
        camera.transform.position = new Vector3(
            frame.CenterX,
            frame.CenterY,
            position.z < 0f ? position.z : -10f);
        RecordInstance(camera);
        RecordInstance(camera.transform);
        CoverTerrain(scene, frame, report);
        report.AppendLine(
            "La cámara cubre parcelas, accesos y corazón entre las barras. " +
            "Tamaño " + frame.OrthographicSize.ToString("0.00") + "."
        );
    }

    private static void CoverTerrain(Scene scene, FrameFit frame, StringBuilder report)
    {
        Transform terrain = FindNamed(scene, "Terrain");

        if (terrain == null)
        {
            report.AppendLine("No está el terreno visual.");
            return;
        }

        Renderer renderer = terrain.GetComponentInChildren<Renderer>(true);

        if (renderer != null &&
            renderer.bounds.min.x <= frame.ViewMinX &&
            renderer.bounds.max.x >= frame.ViewMaxX &&
            renderer.bounds.min.y <= frame.ViewMinY &&
            renderer.bounds.max.y >= frame.ViewMaxY)
        {
            report.AppendLine("El terreno ya cubre el encuadre.");
            return;
        }

        Undo.RecordObject(terrain, UndoName);
        float width = frame.ViewMaxX - frame.ViewMinX + 1f;
        float height = frame.ViewMaxY - frame.ViewMinY + 1f;
        Vector3 position = terrain.position;
        terrain.position = new Vector3(frame.CenterX, frame.CenterY, position.z);
        terrain.localScale = new Vector3(width, height, terrain.localScale.z);
        RecordInstance(terrain);
        report.AppendLine("El terreno visual cubre el nuevo encuadre.");
    }

    private static bool TryContent(
        Scene scene,
        out float minX,
        out float maxX,
        out float minY,
        out float maxY)
    {
        bool any = false;
        minX = float.MaxValue;
        maxX = float.MinValue;
        minY = float.MaxValue;
        maxY = float.MinValue;
        IncludeAll(FindAll<BuildPlot>(scene), ref any, ref minX, ref maxX, ref minY, ref maxY);
        IncludeAll(FindAll<EnemySpawner>(scene), ref any, ref minX, ref maxX, ref minY, ref maxY);
        IncludeAll(FindAll<BastionHealth>(scene), ref any, ref minX, ref maxX, ref minY, ref maxY);
        return any;
    }

    private static void IncludeAll<T>(
        List<T> actors,
        ref bool any,
        ref float minX,
        ref float maxX,
        ref float minY,
        ref float maxY) where T : Component
    {
        foreach (T actor in actors)
        {
            any = true;
            Vector3 point = actor.transform.position;
            minX = Mathf.Min(minX, point.x - ActorPadding);
            maxX = Mathf.Max(maxX, point.x + ActorPadding);
            minY = Mathf.Min(minY, point.y - ActorPadding);
            maxY = Mathf.Max(maxY, point.y + ActorPadding);
        }
    }

    private static void AssignStartGame(Scene scene)
    {
        GameManager manager = FindOne<GameManager>(scene);

        foreach (WaveStartControl control in FindAll<WaveStartControl>(scene))
        {
            SerializedObject serialized = new(control);
            SerializedProperty property = serialized.FindProperty("gameManager");

            if (property.objectReferenceValue == null && manager != null)
            {
                property.objectReferenceValue = manager;
                serialized.ApplyModifiedProperties();
            }
        }
    }

    private static void ReportWaves(Scene scene, StringBuilder report)
    {
        WaveManager manager = FindOne<WaveManager>(scene);

        if (manager == null)
        {
            return;
        }

        SerializedObject serialized = new(manager);
        SerializedProperty waves = serialized.FindProperty("waves");

        for (int i = 0; i < waves.arraySize; i++)
        {
            report.AppendLine(
                "Oleada " + (i + 1) + ": " +
                CountWave(waves.GetArrayElementAtIndex(i)) + " individuos."
            );
        }
    }

    private static int CountWave(SerializedProperty wave)
    {
        SerializedProperty sequence = wave.FindPropertyRelative("sequence");

        if (sequence == null || sequence.arraySize == 0)
        {
            return wave.FindPropertyRelative("enemyCount").intValue;
        }

        int total = 0;

        for (int i = 0; i < sequence.arraySize; i++)
        {
            int party = sequence.GetArrayElementAtIndex(i)
                .FindPropertyRelative("partySize").intValue;
            total += party < 1 ? 1 : party;
        }

        return total;
    }

    private static Transform EnsureRow(Transform hud, string name, Transform style)
    {
        Transform existing = hud.Find(name);

        if (existing != null)
        {
            return existing;
        }

        var root = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, UndoName);
        root.transform.SetParent(hud, false);
        Image source = style.GetComponent<Image>();

        if (source != null)
        {
            Image image = root.AddComponent<Image>();
            image.color = source.color;
            image.raycastTarget = true;
        }

        return root.transform;
    }

    private static void RemoveEmptyRow(Scene scene, string name)
    {
        Transform row = FindNamed(scene, name);

        if (row == null || row.GetComponentInChildren<BuildChoiceButton>(true) != null)
        {
            return;
        }

        Undo.DestroyObjectImmediate(row.gameObject);
    }

    private static float TopFraction(Transform top, float height)
    {
        RectTransform rect = top as RectTransform;

        if (rect == null || rect.rect.height < 1f)
        {
            return 88f / height;
        }

        float occupied = -rect.anchoredPosition.y + rect.pivot.y * rect.rect.height;

        if (occupied < 20f || occupied > height * 0.4f)
        {
            return 88f / height;
        }

        return occupied / height;
    }

    private static float BarTop(Transform target, Transform space)
    {
        RectTransform rect = target as RectTransform;

        if (rect != null && rect.anchorMax.y <= 0.01f)
        {
            float height = rect.rect.height > 1f ? rect.rect.height : rect.sizeDelta.y;
            return rect.anchoredPosition.y + (1f - rect.pivot.y) * height;
        }

        if (target == null)
        {
            return 0f;
        }

        return Edge(target, space, true);
    }

    private static float Edge(Transform target, Transform space, bool top)
    {
        RectTransform rect = space.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        target.GetComponent<RectTransform>().GetWorldCorners(corners);
        Vector3 point = rect.InverseTransformPoint(top ? corners[1] : corners[0]);
        return point.y - rect.rect.yMin;
    }

    private static Camera FindCamera(Scene scene)
    {
        foreach (Camera camera in FindAll<Camera>(scene))
        {
            if (camera.CompareTag("MainCamera") || camera.name == "Main Camera")
            {
                return camera;
            }
        }

        List<Camera> cameras = FindAll<Camera>(scene);
        return cameras.Count == 0 ? null : cameras[0];
    }

    private static void RecordInstance(Object target)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(target))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }

    private static T FindOne<T>(Scene scene) where T : Component
    {
        List<T> all = FindAll<T>(scene);
        return all.Count == 0 ? null : all[0];
    }

    private static List<T> FindAll<T>(Scene scene) where T : Component
    {
        var result = new List<T>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            result.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return result;
    }

    private static Transform FindNamed(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == objectName)
                {
                    return children[i];
                }
            }
        }

        return null;
    }

    private static void Report(string message)
    {
        Debug.Log(message);
        EditorUtility.DisplayDialog("Interfaz y cierre", message, "Cerrar");
    }
}
