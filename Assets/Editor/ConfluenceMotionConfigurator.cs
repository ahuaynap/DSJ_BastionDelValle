using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ConfluenceMotionConfigurator
{
    private const string MenuPath =
        "Bastion del Valle/Mapas/23 - Configurar Movimiento Local";

    private const string UndoName = "Configurar Movimiento Local";
    private const string Level02Path = "Assets/Scenes/Level_02.unity";
    private const string SettingsPath = "Assets/Data/Maps/LocalMotionSettings.asset";
    private const string LayoutPath = "Assets/Data/Maps/ConfluenceLayout.asset";

    [MenuItem(MenuPath, false, 23)]
    private static void Configure()
    {
        if (EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling)
        {
            Report("La configuración solo puede ejecutarse fuera de Play Mode.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (scene.path != Level02Path)
        {
            Report(
                "Abre Level_02 y vuelve a ejecutar la acción. " +
                "No se modificó ninguna escena."
            );
            return;
        }

        ConfluenceLayout layout =
            AssetDatabase.LoadAssetAtPath<ConfluenceLayout>(LayoutPath);
        var navigators = FindAll<ChannelNavigator>(scene);

        if (layout == null || layout.CellSize <= 0f || navigators.Count != 1)
        {
            Report(
                "Level_02 necesita un ChannelNavigator y la cuadrícula de confluencia. " +
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
            float cell = layout.CellSize;
            LocalMotionSettings settings = EnsureSettings(cell, report);
            AssignNavigator(navigators[0], settings, report);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssetIfDirty(settings);
            report.AppendLine(
                "Level_02 quedó marcada como modificada. " +
                "Guárdala desde el editor. No se guardó la escena."
            );
            Report(report.ToString());
        }
        catch (System.Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(exception);
            Report("No se configuró el movimiento local.\n" + exception.Message);
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenu()
    {
        return !EditorApplication.isPlaying &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling;
    }

    private static LocalMotionSettings EnsureSettings(float cell, StringBuilder report)
    {
        float acceleration = 8f;
        float tolerance = cell * 0.16f;
        float arrival = cell * 0.45f;
        float radius = cell * 0.22f;
        float separation = cell * 1.1f;
        LocalMotionSettings settings =
            AssetDatabase.LoadAssetAtPath<LocalMotionSettings>(SettingsPath);

        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LocalMotionSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            SerializedObject serialized = new(settings);
            serialized.FindProperty("steeringEnabled").boolValue = true;
            serialized.FindProperty("acceleration").floatValue = acceleration;
            serialized.FindProperty("waypointTolerance").floatValue = tolerance;
            serialized.FindProperty("arrivalRadius").floatValue = arrival;
            serialized.FindProperty("agentRadius").floatValue = radius;
            serialized.FindProperty("separationSpeed").floatValue = separation;
            serialized.FindProperty("drawDiagnostics").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            report.AppendLine(
                "Se creó LocalMotionSettings. Radio " +
                radius.ToString("0.00") +
                " para celdas de " + cell.ToString("0.00") + "."
            );
            return settings;
        }

        Note(report, "aceleración", settings.Acceleration, acceleration);
        Note(report, "tolerancia de waypoint", settings.WaypointTolerance, tolerance);
        Note(report, "radio de llegada", settings.ArrivalRadius, arrival);
        Note(report, "radio del agente", settings.AgentRadius, radius);
        Note(report, "separación", settings.SeparationSpeed, separation);
        report.AppendLine("Se reutilizó LocalMotionSettings.");
        return settings;
    }

    private static void AssignNavigator(
        ChannelNavigator navigator,
        LocalMotionSettings settings,
        StringBuilder report)
    {
        SerializedObject serialized = new(navigator);
        SerializedProperty property = serialized.FindProperty("motion");

        if (property == null)
        {
            throw new System.InvalidOperationException(
                "ChannelNavigator no tiene el campo de movimiento local."
            );
        }

        if (property.objectReferenceValue == null)
        {
            property.objectReferenceValue = settings;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(navigator);
            report.AppendLine("Se conectó el movimiento local al navegador.");
            return;
        }

        if (property.objectReferenceValue != settings)
        {
            report.AppendLine(
                "El navegador ya usa otra configuración de movimiento y se conservó."
            );
        }
    }

    private static void Note(
        StringBuilder report,
        string label,
        float actual,
        float initial)
    {
        if (!Mathf.Approximately(actual, initial))
        {
            report.AppendLine(
                "Ajuste personalizado de " + label + ": " + actual +
                ". Inicial sugerido: " + initial + "."
            );
        }
    }

    private static System.Collections.Generic.List<T> FindAll<T>(Scene scene)
        where T : Component
    {
        var result = new System.Collections.Generic.List<T>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            result.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return result;
    }

    private static void Report(string message)
    {
        EditorUtility.DisplayDialog("Bastión del Valle", message, "Entendido");
        Debug.Log(message);
    }
}
