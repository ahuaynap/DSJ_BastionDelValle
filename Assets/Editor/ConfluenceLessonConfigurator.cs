using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfluenceLessonConfigurator
{
    private const string MenuPath =
        "Bastion del Valle/Mapas/27 - Configurar Cuatro Oleadas";

    private const string UndoName = "Configurar Cuatro Oleadas";
    private const string Level02Path = "Assets/Scenes/Level_02.unity";
    private const string LessonPath = "Assets/Data/Maps/Level02Lesson.asset";
    private const string AphidPath = "Assets/Prefabs/Enemy_Basic.prefab";
    private const string LocustPath = "Assets/Prefabs/Enemy_Locust.prefab";
    private const string SwarmPath = "Assets/Prefabs/Enemy_Swarm.prefab";
    private const string MothPath = "Assets/Prefabs/Enemy_Moth.prefab";

    [MenuItem(MenuPath, false, 28)]
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
            Report("Abre Level_02 y vuelve a ejecutar la acción. No se modificó ninguna escena.");
            return;
        }

        if (Count<WaveManager>(scene) != 1 ||
            Count<HUDController>(scene) != 1 ||
            FindNamed(scene, "HUD") == null)
        {
            Report(
                "Level_02 necesita un WaveManager, el HUD y su controlador. " +
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
            ReportBalance(report);
            EnemyMovement[] prefabs = LoadPrefabs(report);
            bool lessonRoster = prefabs != null && WriteWaves(scene, prefabs, report);

            if (lessonRoster)
            {
                MapLesson lesson = EnsureLesson(report);
                AssignLesson(scene, lesson, report);
                EnsureLessonRow(scene, report);
                AssetDatabase.SaveAssetIfDirty(lesson);
            }

            ConfluenceInterfaceRepair.Apply(scene, report, false);
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
            Report("No se configuraron las cuatro oleadas.\n" + exception.Message);
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenu()
    {
        return !EditorApplication.isPlaying &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling;
    }

    private static void ReportBalance(StringBuilder report)
    {
        EconomyManager economy = FindOne<EconomyManager>(SceneManager.GetActiveScene());
        int seeds = ReadInt(economy, "startingResources");
        report.AppendLine(
            seeds == 120
                ? "Las semillas iniciales siguen en 120."
                : "Las semillas iniciales son " + seeds +
                  ". Se conservó ese valor de la escena."
        );

        CollectibleSpawner spawner = FindOne<CollectibleSpawner>(
            SceneManager.GetActiveScene());
        int cap = ReadInt(spawner, "maxDiscoveriesPerRun");
        report.AppendLine(
            "Memorias por partida: " + cap +
            ". El máximo no crece al pasar de dos a cuatro oleadas."
        );

        NoteCost(report, "Guardián", "Assets/Data/TowerData_Basic.asset", 30);
        NoteCost(report, "Canal Vivo", "Assets/Data/TowerData_Water.asset", 40);
        NoteCost(report, "Refugio", "Assets/Data/TowerData_Pollinator.asset", 60);
        NoteCost(report, "Raíces", "Assets/Data/TowerData_Roots.asset", 50);
        NoteCost(report, "Vigía", "Assets/Data/TowerData_Condor.asset", 70);
    }

    private static void NoteCost(
        StringBuilder report,
        string label,
        string path,
        int expected)
    {
        TowerData data = AssetDatabase.LoadAssetAtPath<TowerData>(path);

        if (data == null)
        {
            report.AppendLine("No está el dato de " + label + ".");
            return;
        }

        if (data.BuildCost == expected)
        {
            report.AppendLine(label + " cuesta " + data.BuildCost + ".");
            return;
        }

        report.AppendLine(
            label + " cuesta " + data.BuildCost +
            " en el asset compartido. Esta acción no cambia ese balance."
        );

        if (label == "Guardián" && data.BuildCost == 40)
        {
            report.AppendLine(
                "Ese 40 quedó en TowerData_Basic al cerrar el bloque 06. " +
                "La acción 26B lo devuelve a 30."
            );
        }
    }

    private static EnemyMovement[] LoadPrefabs(StringBuilder report)
    {
        EnemyMovement aphid = AssetDatabase.LoadAssetAtPath<EnemyMovement>(AphidPath);
        EnemyMovement locust = AssetDatabase.LoadAssetAtPath<EnemyMovement>(LocustPath);
        EnemyMovement swarm = AssetDatabase.LoadAssetAtPath<EnemyMovement>(SwarmPath);
        EnemyMovement moth = AssetDatabase.LoadAssetAtPath<EnemyMovement>(MothPath);

        if (aphid != null && locust != null && swarm != null && moth != null)
        {
            return new[] { aphid, locust, swarm, moth };
        }

        report.AppendLine(
            "Falta un prefab de amenaza. No se reescribió la composición."
        );
        return null;
    }

    private static bool WriteWaves(
        Scene scene,
        EnemyMovement[] prefabs,
        StringBuilder report)
    {
        WaveManager manager = FindOne<WaveManager>(scene);
        Undo.RecordObject(manager, UndoName);
        SerializedObject serialized = new(manager);
        SerializedProperty list = serialized.FindProperty("waves");
        int[] counts = Counts(list);
        float[] intervals = Intervals(list);

        if (LessonRules.IsCanonical(counts, intervals) &&
            PrefabsMatch(list, prefabs))
        {
            float party = list
                .GetArrayElementAtIndex(2)
                .FindPropertyRelative("partyInterval")
                .floatValue;
            report.AppendLine(
                Mathf.Abs(party - WaveComposition.MemberInterval) <= 0.02f
                    ? "Las cuatro oleadas ya tienen 6, 8, 15 y 6 individuos."
                    : "El intervalo interno del enjambre está personalizado y se conservó."
            );
            return true;
        }

        if (list.arraySize != 2 && list.arraySize != 0)
        {
            report.AppendLine(
                "La composición tiene " + list.arraySize +
                " oleadas personalizadas y se conservó."
            );
            return false;
        }

        int[][] rosters = LessonRules.Rosters();
        float[] gaps = LessonRules.Intervals();
        list.arraySize = rosters.Length;

        for (int wave = 0; wave < rosters.Length; wave++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(wave);
            int individuals = LessonRules.Individuals(rosters[wave]);
            entry.FindPropertyRelative("enemyCount").intValue = individuals;
            entry.FindPropertyRelative("spawnInterval").floatValue = gaps[wave];
            entry.FindPropertyRelative("partyInterval").floatValue =
                WaveComposition.MemberInterval;
            SerializedProperty sequence = entry.FindPropertyRelative("sequence");
            sequence.arraySize = rosters[wave].Length;

            for (int i = 0; i < rosters[wave].Length; i++)
            {
                SerializedProperty spawn = sequence.GetArrayElementAtIndex(i);
                int kind = rosters[wave][i];
                spawn.FindPropertyRelative("enemyPrefab").objectReferenceValue =
                    PrefabFor(kind, prefabs);
                spawn.FindPropertyRelative("partySize").intValue =
                    kind == WaveComposition.Swarm
                        ? WaveComposition.SwarmGroupSize
                        : 1;
            }
        }

        serialized.ApplyModifiedProperties();
        Commit(manager);
        report.AppendLine(
            "Level_02 quedó en cuatro oleadas: 6 pulgones, 8 langostas, " +
            "15 criaturas en tres grupos y 6 polillas."
        );
        return true;
    }

    private static bool PrefabsMatch(SerializedProperty list, EnemyMovement[] prefabs)
    {
        int[][] rosters = LessonRules.Rosters();

        if (list.arraySize != rosters.Length)
        {
            return false;
        }

        for (int wave = 0; wave < rosters.Length; wave++)
        {
            SerializedProperty sequence = list
                .GetArrayElementAtIndex(wave)
                .FindPropertyRelative("sequence");

            if (sequence.arraySize != rosters[wave].Length)
            {
                return false;
            }

            for (int i = 0; i < rosters[wave].Length; i++)
            {
                SerializedProperty spawn = sequence.GetArrayElementAtIndex(i);
                int party = spawn.FindPropertyRelative("partySize").intValue;
                int expectedParty = rosters[wave][i] == WaveComposition.Swarm
                    ? WaveComposition.SwarmGroupSize
                    : 1;

                if (party < 1)
                {
                    party = 1;
                }

                if (spawn.FindPropertyRelative("enemyPrefab").objectReferenceValue !=
                        PrefabFor(rosters[wave][i], prefabs) ||
                    party != expectedParty)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static EnemyMovement PrefabFor(int kind, EnemyMovement[] prefabs)
    {
        if (kind == WaveComposition.Locust)
        {
            return prefabs[1];
        }

        if (kind == WaveComposition.Swarm)
        {
            return prefabs[2];
        }

        if (kind == WaveComposition.Moth)
        {
            return prefabs[3];
        }

        return prefabs[0];
    }

    private static int[] Counts(SerializedProperty list)
    {
        var counts = new int[list.arraySize];

        for (int i = 0; i < list.arraySize; i++)
        {
            counts[i] = CountWave(list.GetArrayElementAtIndex(i));
        }

        return counts;
    }

    private static float[] Intervals(SerializedProperty list)
    {
        var intervals = new float[list.arraySize];

        for (int i = 0; i < list.arraySize; i++)
        {
            intervals[i] = list
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("spawnInterval")
                .floatValue;
        }

        return intervals;
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
            int party = sequence
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("partySize")
                .intValue;
            total += party < 1 ? 1 : party;
        }

        return total;
    }

    private static MapLesson EnsureLesson(StringBuilder report)
    {
        MapLesson lesson = AssetDatabase.LoadAssetAtPath<MapLesson>(LessonPath);

        if (lesson == null)
        {
            lesson = ScriptableObject.CreateInstance<MapLesson>();
            AssetDatabase.CreateAsset(lesson, LessonPath);
            report.AppendLine("Se creó la lección editable de Level_02.");
        }

        SerializedObject serialized = new(lesson);
        SerializedProperty waves = serialized.FindProperty("waves");
        string[] threats = LessonRules.Threats();
        string[] advice = LessonRules.Advice();
        waves.arraySize = threats.Length;

        for (int i = 0; i < threats.Length; i++)
        {
            SerializedProperty wave = waves.GetArrayElementAtIndex(i);
            KeepOrWrite(wave.FindPropertyRelative("threatName"), threats[i], report, i, "nombre");
            KeepOrWrite(wave.FindPropertyRelative("advice"), advice[i], report, i, "consejo");
            int reward = i < threats.Length - 1 ? LessonRules.CompletionReward : 0;
            SerializedProperty rewardProperty = wave.FindPropertyRelative("clearReward");

            if (rewardProperty.intValue == reward || rewardProperty.intValue == 0)
            {
                rewardProperty.intValue = reward;
            }
            else
            {
                report.AppendLine(
                    "La recompensa de la oleada " + (i + 1) +
                    " está personalizada y se conservó."
                );
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lesson);
        return lesson;
    }

    private static void KeepOrWrite(
        SerializedProperty property,
        string canonical,
        StringBuilder report,
        int index,
        string label)
    {
        if (string.IsNullOrEmpty(property.stringValue) ||
            property.stringValue == canonical)
        {
            property.stringValue = canonical;
            return;
        }

        report.AppendLine(
            "El " + label + " de la oleada " + (index + 1) +
            " está personalizado y se conservó."
        );
    }

    private static void AssignLesson(Scene scene, MapLesson lesson, StringBuilder report)
    {
        WaveManager manager = FindOne<WaveManager>(scene);
        Undo.RecordObject(manager, UndoName);
        SerializedObject serialized = new(manager);
        SerializedProperty property = serialized.FindProperty("lesson");

        if (property.objectReferenceValue != null &&
            property.objectReferenceValue != lesson)
        {
            report.AppendLine("El mapa ya usa otra lección y se conservó.");
            return;
        }

        property.objectReferenceValue = lesson;
        serialized.ApplyModifiedProperties();
        Commit(manager);
    }

    private static void EnsureLessonRow(Scene scene, StringBuilder report)
    {
        Transform hud = FindNamed(scene, "HUD");
        List<Transform> rows = FindNamedAll(scene, "LessonRow");

        for (int i = 1; i < rows.Count; i++)
        {
            Undo.DestroyObjectImmediate(rows[i].gameObject);
            report.AppendLine("Se retiró una fila de lección duplicada.");
        }

        Transform row = rows.Count > 0 ? rows[0] : null;

        if (row == null)
        {
            var created = new GameObject("LessonRow", typeof(RectTransform));
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
        ContentSizeFitter fitter = row.GetComponent<ContentSizeFitter>();

        if (fitter != null)
        {
            Undo.RecordObject(fitter, UndoName);
            fitter.enabled = false;
        }

        Transform label = row.Find("LessonText");

        if (label == null)
        {
            var created = new GameObject("LessonText", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(created, UndoName);
            created.transform.SetParent(row, false);
            label = created.transform;
        }

        RectTransform labelRect = label.GetComponent<RectTransform>();
        Undo.RecordObject(labelRect, UndoName);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(16f, 8f);
        labelRect.offsetMax = new Vector2(-16f, -8f);
        labelRect.localScale = Vector3.one;
        TMP_Text text = label.GetComponent<TMP_Text>();

        if (text == null)
        {
            text = Undo.AddComponent<TextMeshProUGUI>(label.gameObject);
        }

        TMP_Text source = FindNamed(scene, "StatusText") != null
            ? FindNamed(scene, "StatusText").GetComponent<TMP_Text>()
            : null;
        Undo.RecordObject(text, UndoName);

        if (source != null)
        {
            text.font = source.font;
            text.fontSharedMaterial = source.fontSharedMaterial;
            text.color = source.color;
        }
        text.fontSize = 22f;
        text.enableAutoSizing = false;
        text.enableWordWrapping = true;
        text.alignment = TextAlignmentOptions.Center;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        HUDController hudController = FindOne<HUDController>(scene);
        Undo.RecordObject(hudController, UndoName);
        SerializedObject serialized = new(hudController);
        serialized.FindProperty("lessonText").objectReferenceValue = text;
        serialized.ApplyModifiedProperties();
        Commit(row.gameObject);
        Commit(text);
        Commit(hudController);
        report.AppendLine("La previsión quedó en su propia fila, separada de los botones.");
    }

    private static int ReadInt(Object target, string fieldName)
    {
        if (target == null)
        {
            return 0;
        }

        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(fieldName);
        return property == null ? 0 : property.intValue;
    }

    private static void Commit(Object target)
    {
        if (target != null && PrefabUtility.IsPartOfPrefabInstance(target))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
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

    private static void Report(string message)
    {
        Debug.Log(message);
        EditorUtility.DisplayDialog("Cuatro oleadas", message, "Cerrar");
    }
}
