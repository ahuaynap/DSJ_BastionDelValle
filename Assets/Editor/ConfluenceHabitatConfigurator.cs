using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfluenceHabitatConfigurator
{
    private const string MenuPath =
        "Bastion del Valle/Mapas/28 - Integrar Ambiente y Sinergias";

    private const string UndoName = "Integrar Ambiente y Sinergias";
    private const string Level02Path = "Assets/Scenes/Level_02.unity";
    private const string SettingsPath = "Assets/Data/Maps/Level02Habitat.asset";

    [MenuItem(MenuPath, false, 29)]
    private static void Integrate()
    {
        if (EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling)
        {
            Report("La integración solo puede ejecutarse fuera de Play Mode.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (scene.path != Level02Path)
        {
            Report("Abre Level_02 y vuelve a ejecutar la acción. No se modificó ninguna escena.");
            return;
        }

        if (Count<WaveManager>(scene) != 1 ||
            Count<HUDController>(scene) != 1 ||
            Count<EndGameUI>(scene) != 1 ||
            Count<GameManager>(scene) != 1 ||
            FindNamed(scene, "HUD") == null)
        {
            Report(
                "Level_02 necesita el coordinador, el HUD y el resultado. " +
                "No se modificó la escena."
            );
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        var report = new StringBuilder();

        try
        {
            HabitatSettings settings = EnsureSettings(report);
            HabitatSession session = EnsureSession(scene, settings, report);
            EnsureRow(scene, session, report);
            ConfluenceInterfaceRepair.Apply(scene, report, false);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssetIfDirty(settings);
            report.AppendLine("Level_02 quedó marcada como modificada. Guárdala desde el editor.");
            report.AppendLine("Level_01 no se abrió ni se modificó.");
            Report(report.ToString());
        }
        catch (System.Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(exception);
            Report("No se integró el ambiente.\n" + exception.Message);
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenu()
    {
        return !EditorApplication.isPlaying &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling;
    }

    private static HabitatSettings EnsureSettings(StringBuilder report)
    {
        HabitatSettings settings = AssetDatabase.LoadAssetAtPath<HabitatSettings>(SettingsPath);

        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<HabitatSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            report.AppendLine("Se creó la configuración ambiental de Level_02.");
        }

        SerializedObject serialized = new(settings);
        SerializedProperty configured = serialized.FindProperty("configured");

        if (!configured.boolValue)
        {
            serialized.FindProperty("initialWater").intValue = HabitatRules.StartValue;
            serialized.FindProperty("initialSoil").intValue = HabitatRules.StartValue;
            serialized.FindProperty("initialLife").intValue = HabitatRules.StartValue;
            serialized.FindProperty("minimum").intValue = HabitatRules.MinimumValue;
            serialized.FindProperty("maximum").intValue = HabitatRules.MaximumValue;
            serialized.FindProperty("arrivalLoss").intValue = HabitatRules.ArrivalDrop;
            serialized.FindProperty("recoveryGain").intValue = HabitatRules.WaveRecovery;
            serialized.FindProperty("threshold").intValue = HabitatRules.BenefitThreshold;
            serialized.FindProperty("auraDistance").floatValue = HabitatRules.AuraDistance;
            serialized.FindProperty("hasteMultiplier").floatValue = HabitatRules.Haste;
            serialized.FindProperty("reachMultiplier").floatValue = HabitatRules.Reach;
            serialized.FindProperty("soilBonus").intValue = HabitatRules.SoilBonus;
            configured.boolValue = true;
            report.AppendLine(
                "Agua, suelo y biodiversidad empiezan en 50, con umbral 60."
            );
        }
        else
        {
            report.AppendLine("La configuración ambiental personalizada se conservó.");
        }

        FillId(serialized.FindProperty("waterTowerId"), "tower_water");
        FillId(serialized.FindProperty("soilTowerId"), "tower_roots");
        FillId(serialized.FindProperty("lifeTowerId"), "tower_pollinator");
        FillId(serialized.FindProperty("guardianTowerId"), "tower_basic");
        FillId(serialized.FindProperty("watcherTowerId"), "tower_condor");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        return settings;
    }

    private static void FillId(SerializedProperty property, string canonical)
    {
        if (string.IsNullOrEmpty(property.stringValue))
        {
            property.stringValue = canonical;
        }
    }

    private static HabitatSession EnsureSession(
        Scene scene,
        HabitatSettings settings,
        StringBuilder report)
    {
        List<HabitatSession> sessions = FindAll<HabitatSession>(scene);

        for (int i = 1; i < sessions.Count; i++)
        {
            Undo.DestroyObjectImmediate(sessions[i].gameObject);
            report.AppendLine("Se retiró un estado ambiental duplicado.");
        }

        HabitatSession session = sessions.Count > 0 ? sessions[0] : null;

        if (session == null)
        {
            var created = new GameObject("Habitat");
            Undo.RegisterCreatedObjectUndo(created, UndoName);
            session = Undo.AddComponent<HabitatSession>(created);
        }

        Undo.RecordObject(session, UndoName);
        SerializedObject serialized = new(session);
        SerializedProperty settingsProperty = serialized.FindProperty("settings");

        if (settingsProperty.objectReferenceValue == null ||
            settingsProperty.objectReferenceValue == settings)
        {
            settingsProperty.objectReferenceValue = settings;
        }
        else
        {
            report.AppendLine("El estado ya usa otra configuración y se conservó.");
        }

        serialized.FindProperty("gameManager").objectReferenceValue =
            FindOne<GameManager>(scene);
        serialized.ApplyModifiedProperties();
        Commit(session);
        Assign(FindOne<WaveManager>(scene), "habitat", session, report);
        Assign(FindOne<EndGameUI>(scene), "habitat", session, report);
        return session;
    }

    private static void Assign(
        Object target,
        string fieldName,
        Object value,
        StringBuilder report)
    {
        Undo.RecordObject(target, UndoName);
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property.objectReferenceValue != null &&
            property.objectReferenceValue != value)
        {
            report.AppendLine(target.name + " ya tiene otro ambiente y se conservó.");
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
        Commit(target);
    }

    private static void EnsureRow(
        Scene scene,
        HabitatSession session,
        StringBuilder report)
    {
        Transform hud = FindNamed(scene, "HUD");
        List<Transform> rows = FindNamedAll(scene, "HabitatRow");

        for (int i = 1; i < rows.Count; i++)
        {
            Undo.DestroyObjectImmediate(rows[i].gameObject);
            report.AppendLine("Se retiró una fila ambiental duplicada.");
        }

        Transform row = rows.Count > 0 ? rows[0] : null;

        if (row == null)
        {
            var created = new GameObject("HabitatRow", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(created, UndoName);
            created.transform.SetParent(hud, false);
            row = created.transform;
        }

        Image image = row.GetComponent<Image>();

        if (image == null)
        {
            image = Undo.AddComponent<Image>(row.gameObject);
        }

        Image sample = FindNamed(scene, "MessageRow") != null
            ? FindNamed(scene, "MessageRow").GetComponent<Image>()
            : null;
        Undo.RecordObject(image, UndoName);
        image.color = sample != null
            ? sample.color
            : new Color(0.149f, 0.239f, 0.188f, 0.94f);
        image.raycastTarget = true;
        VerticalLayoutGroup column = row.GetComponent<VerticalLayoutGroup>();

        if (column == null)
        {
            column = Undo.AddComponent<VerticalLayoutGroup>(row.gameObject);
        }

        Undo.RecordObject(column, UndoName);
        column.padding = new RectOffset(16, 16, 6, 6);
        column.spacing = 2f;
        column.childAlignment = TextAnchor.MiddleCenter;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        Transform meters = row.Find("HabitatMeters");

        if (meters == null)
        {
            var created = new GameObject("HabitatMeters", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(created, UndoName);
            created.transform.SetParent(row, false);
            meters = created.transform;
        }

        HorizontalLayoutGroup line = meters.GetComponent<HorizontalLayoutGroup>();

        if (line == null)
        {
            line = Undo.AddComponent<HorizontalLayoutGroup>(meters.gameObject);
        }

        Undo.RecordObject(line, UndoName);
        line.spacing = 12f;
        line.childAlignment = TextAnchor.MiddleCenter;
        line.childControlWidth = true;
        line.childControlHeight = true;
        line.childForceExpandWidth = true;
        line.childForceExpandHeight = false;
        TMP_Text source = FindNamed(scene, "StatusText").GetComponent<TMP_Text>();
        Prefer(meters, 36f);
        TMP_Text water = EnsureText(meters, "HabitatWater", source, new Color(0.49f, 0.71f, 0.85f));
        TMP_Text soil = EnsureText(meters, "HabitatSoil", source, new Color(0.77f, 0.63f, 0.35f));
        TMP_Text life = EnsureText(meters, "HabitatLife", source, new Color(0.56f, 0.75f, 0.48f));
        water.text = "Agua 50/100";
        soil.text = "Suelo 50/100";
        life.text = "Biodiversidad 50/100";
        Button help = EnsureHelp(meters, source);
        TMP_Text cause = EnsureText(row, "HabitatCause", source, source.color);
        Prefer(cause.transform, 32f);
        cause.text = "Umbral 60.";
        cause.alignment = TextAlignmentOptions.Center;
        HUDController controller = FindOne<HUDController>(scene);
        Undo.RecordObject(controller, UndoName);
        SerializedObject serialized = new(controller);
        serialized.FindProperty("habitat").objectReferenceValue = session;
        serialized.FindProperty("habitatWaterText").objectReferenceValue = water;
        serialized.FindProperty("habitatSoilText").objectReferenceValue = soil;
        serialized.FindProperty("habitatLifeText").objectReferenceValue = life;
        serialized.FindProperty("habitatCauseText").objectReferenceValue = cause;
        serialized.FindProperty("habitatHelpButton").objectReferenceValue = help;
        serialized.ApplyModifiedProperties();
        Commit(controller);
        report.AppendLine("La fila ambiental quedó sobre la previsión, con texto y color.");
    }

    private static void Prefer(Transform target, float height)
    {
        LayoutElement element = target.GetComponent<LayoutElement>();

        if (element == null)
        {
            element = Undo.AddComponent<LayoutElement>(target.gameObject);
        }

        Undo.RecordObject(element, UndoName);
        element.minHeight = height;
        element.preferredHeight = height;
        element.flexibleHeight = 0f;
    }

    private static TMP_Text EnsureText(
        Transform parent,
        string objectName,
        TMP_Text source,
        Color color)
    {
        Transform existing = parent.Find(objectName);

        if (existing == null)
        {
            var created = new GameObject(objectName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(created, UndoName);
            created.transform.SetParent(parent, false);
            existing = created.transform;
        }

        TMP_Text text = existing.GetComponent<TMP_Text>();

        if (text == null)
        {
            text = Undo.AddComponent<TextMeshProUGUI>(existing.gameObject);
        }

        Undo.RecordObject(text, UndoName);
        text.font = source.font;
        text.fontSharedMaterial = source.fontSharedMaterial;
        text.color = color;
        text.fontSize = 20f;
        text.enableAutoSizing = false;
        text.enableWordWrapping = true;
        text.alignment = TextAlignmentOptions.Center;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static Button EnsureHelp(Transform parent, TMP_Text source)
    {
        Transform existing = parent.Find("HabitatHelp");

        if (existing == null)
        {
            var created = new GameObject("HabitatHelp", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(created, UndoName);
            created.transform.SetParent(parent, false);
            existing = created.transform;
        }

        Image image = existing.GetComponent<Image>();

        if (image == null)
        {
            image = Undo.AddComponent<Image>(existing.gameObject);
        }

        Undo.RecordObject(image, UndoName);
        image.color = new Color(0.93f, 0.86f, 0.62f, 1f);
        image.raycastTarget = true;
        Button button = existing.GetComponent<Button>();

        if (button == null)
        {
            button = Undo.AddComponent<Button>(existing.gameObject);
        }

        button.targetGraphic = image;
        LayoutElement element = existing.GetComponent<LayoutElement>();

        if (element == null)
        {
            element = Undo.AddComponent<LayoutElement>(existing.gameObject);
        }

        Undo.RecordObject(element, UndoName);
        element.minWidth = 120f;
        element.preferredWidth = 140f;
        element.flexibleWidth = 0f;
        element.minHeight = 32f;
        element.preferredHeight = 36f;

        if (existing.GetComponent<UiClickSound>() == null)
        {
            Undo.AddComponent<UiClickSound>(existing.gameObject);
        }

        EnsureText(existing, "Label", source, new Color(0.16f, 0.2f, 0.14f));
        TMP_Text label = existing.Find("Label").GetComponent<TMP_Text>();
        label.text = "Umbral";
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        return button;
    }

    private static int Count<T>(Scene scene) where T : Component
    {
        return FindAll<T>(scene).Count;
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
        List<Transform> found = FindNamedAll(scene, objectName);
        return found.Count == 0 ? null : found[0];
    }

    private static List<Transform> FindNamedAll(Scene scene, string objectName)
    {
        var result = new List<Transform>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == objectName)
                {
                    result.Add(children[i]);
                }
            }
        }

        return result;
    }

    private static void Commit(Object target)
    {
        if (target != null && PrefabUtility.IsPartOfPrefabInstance(target))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }

    private static void Report(string message)
    {
        Debug.Log(message);
        EditorUtility.DisplayDialog("Ambiente y sinergias", message, "Cerrar");
    }
}
