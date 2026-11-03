using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfluenceRootsConfigurator
{
    private const string MenuPath =
        "Bastion del Valle/Mapas/25 - Integrar Terraza de Raices";

    private const string UndoName = "Integrar Terraza de Raices";
    private const string Level02Path = "Assets/Scenes/Level_02.unity";
    private const string DataPath = "Assets/Data/TowerData_Roots.asset";
    private const string TowerPath = "Assets/Prefabs/Tower_Roots.prefab";
    private const string SeedTowerPath = "Assets/Prefabs/Tower_Basic.prefab";
    private const string SpritePath =
        "Assets/Art/Towers/tower_roots_idle_v001.png";
    private const string AudioPath = "Assets/Audio/SFX/sfx_roots_attack.wav";
    private const string PlaceholderPath =
        "Assets/Art/Generated/Roots/placeholder_tower.png";

    private const string TowerId = "tower_roots";
    private static readonly Color Earth = new(0.62f, 0.4f, 0.18f, 1f);
    private static readonly Color Cream = new(1f, 0.945f, 0.776f, 1f);

    [MenuItem(MenuPath, false, 25)]
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
            Count<MatchAudio>(scene) != 1 ||
            Count<BuildSelection>(scene) != 1 ||
            FindNamed(scene, "HUD") == null ||
            FindNamed(scene, "BuildChoiceRow") == null)
        {
            Report(
                "Level_02 necesita un WaveManager, un MatchAudio, " +
                "una selección y la fila de construcción. No se modificó la escena."
            );
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        var report = new StringBuilder();

        try
        {
            bool realArt = AssetDatabase.LoadMainAssetAtPath(SpritePath) != null;
            Sprite sprite = realArt
                ? LoadSprite(SpritePath)
                : EnsurePlaceholder();

            if (!realArt)
            {
                report.AppendLine("Falta el sprite de la terraza. Se usó una marca temporal.");
            }

            if (AssetDatabase.LoadMainAssetAtPath(AudioPath) == null)
            {
                report.AppendLine("Falta sfx_roots_attack. La terraza sigue, sin ese sonido.");
            }

            TowerData data = EnsureData(sprite, realArt, report);
            TowerController tower = EnsureTower(data, report);
            Wire(scene, tower, report);
            CompactMatchHud.Apply(scene, report);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssetIfDirty(data);
            AssetDatabase.SaveAssetIfDirty(tower);
            report.AppendLine("Las oleadas del bloque 24 se conservaron.");
            report.AppendLine("Level_02 quedó marcada como modificada. Guárdala desde el editor.");
            Report(report.ToString());
        }
        catch (System.Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(exception);
            Report("No se integró la Terraza de Raíces.\n" + exception.Message);
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenu()
    {
        return !EditorApplication.isPlaying &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling;
    }

    private static TowerData EnsureData(Sprite sprite, bool realArt, StringBuilder report)
    {
        TowerData data = AssetDatabase.LoadAssetAtPath<TowerData>(DataPath);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<TowerData>();
            AssetDatabase.CreateAsset(data, DataPath);
            WriteData(data, sprite);
            report.AppendLine("Se creó TowerData_Roots.");
            return data;
        }

        Note(report, "Terraza coste", data.BuildCost, 50);
        Note(report, "Terraza alcance", data.AttackRange, 1.8f);
        Note(report, "Terraza intervalo", data.AttackInterval, 2f);
        Note(report, "Terraza daño", data.ProjectileDamage, 3);
        Note(report, "Terraza radio", data.ZoneRadius, 0.6f);
        Note(report, "Terraza duración", data.ZoneDuration, 1.5f);
        Note(report, "Terraza segundo pulso", data.ZonePulseDelay, 0.75f);
        Note(report, "Terraza ralentización", data.SlowMultiplier, 0.75f);
        FillSprite(data, data.WorldSprite, sprite, realArt, report);

        if (data.ZoneRadius <= 0f &&
            data.ZoneDuration <= 0f &&
            data.ZonePulseDelay <= 0f)
        {
            SerializedObject serialized = new(data);
            serialized.FindProperty("zoneRadius").floatValue = 0.6f;
            serialized.FindProperty("zoneDuration").floatValue = 1.5f;
            serialized.FindProperty("zonePulseDelay").floatValue = 0.75f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            report.AppendLine("Se completó la zona de la terraza.");
        }

        report.AppendLine("Se reutilizó TowerData_Roots.");
        return data;
    }

    private static void WriteData(TowerData data, Sprite sprite)
    {
        SerializedObject serialized = new(data);
        serialized.FindProperty("towerId").stringValue = TowerId;
        serialized.FindProperty("displayName").stringValue = "Terraza de Raíces";
        serialized.FindProperty("description").stringValue =
            "Daña y frena enemigos terrestres en una zona";
        serialized.FindProperty("buildCost").intValue = 50;
        serialized.FindProperty("attackRange").floatValue = 1.8f;
        serialized.FindProperty("attackInterval").floatValue = 2f;
        serialized.FindProperty("projectileDamage").intValue = 3;
        serialized.FindProperty("projectileSpeed").floatValue = 6f;
        serialized.FindProperty("slowMultiplier").floatValue = 0.75f;
        serialized.FindProperty("slowDuration").floatValue = 0f;
        serialized.FindProperty("impactMode").enumValueIndex = (int)ImpactMode.Direct;
        serialized.FindProperty("impactRadius").floatValue = 0f;
        serialized.FindProperty("zoneRadius").floatValue = 0.6f;
        serialized.FindProperty("zoneDuration").floatValue = 1.5f;
        serialized.FindProperty("zonePulseDelay").floatValue = 0.75f;
        serialized.FindProperty("worldSprite").objectReferenceValue = sprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }

    private static TowerController EnsureTower(TowerData data, StringBuilder report)
    {
        TowerController source = AssetDatabase.LoadAssetAtPath<TowerController>(SeedTowerPath);

        if (AssetDatabase.LoadAssetAtPath<TowerController>(TowerPath) == null)
        {
            if (source == null)
            {
                throw new System.InvalidOperationException("No está el prefab del Guardián Semilla.");
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source.gameObject);
            PrefabUtility.UnpackPrefabInstance(
                instance,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction
            );
            instance.name = "Tower_Roots";
            PrefabUtility.SaveAsPrefabAsset(instance, TowerPath);
            Object.DestroyImmediate(instance);
        }

        Sprite seedSprite = source == null ? null : Visual(source.gameObject).sprite;
        GameObject contents = PrefabUtility.LoadPrefabContents(TowerPath);

        try
        {
            contents.name = "Tower_Roots";
            TowerController tower = contents.GetComponent<TowerController>();
            SerializedObject serialized = new(tower);
            SerializedProperty property = serialized.FindProperty("towerData");
            Object current = property.objectReferenceValue;

            if (current == null || (source != null && current == source.Data))
            {
                property.objectReferenceValue = data;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else if (current != data)
            {
                report.AppendLine("Los datos de la terraza están personalizados y se conservaron.");
            }

            SpriteRenderer renderer = Visual(contents);

            if (renderer != null && data.WorldSprite != null &&
                (renderer.sprite == null || renderer.sprite == seedSprite))
            {
                renderer.sprite = data.WorldSprite;
            }

            PrefabUtility.SaveAsPrefabAsset(contents, TowerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return AssetDatabase.LoadAssetAtPath<TowerController>(TowerPath);
    }

    private static void Wire(Scene scene, TowerController tower, StringBuilder report)
    {
        BuildSelection selection = FindAll<BuildSelection>(scene)[0];
        SerializedObject serialized = new(selection);
        SerializedProperty defenses = serialized.FindProperty("defenses");
        bool listed = false;

        for (int i = 0; i < defenses.arraySize; i++)
        {
            if (defenses.GetArrayElementAtIndex(i).objectReferenceValue == tower)
            {
                listed = true;
            }
        }

        if (!listed)
        {
            int index = defenses.arraySize;
            defenses.arraySize++;
            defenses.GetArrayElementAtIndex(index).objectReferenceValue = tower;
            report.AppendLine("La terraza entró en la selección, sin cambiar la defensa inicial.");
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(selection);
        int defenseIndex = 0;
        serialized = new SerializedObject(selection);
        defenses = serialized.FindProperty("defenses");

        for (int i = 0; i < defenses.arraySize; i++)
        {
            if (defenses.GetArrayElementAtIndex(i).objectReferenceValue == tower)
            {
                defenseIndex = i;
            }
        }

        Transform row = FindNamed(scene, "BuildChoiceRow");
        Button button = EnsureButton(row, selection, defenseIndex);
        BuildChoiceView view = row.GetComponent<BuildChoiceView>();

        if (view == null)
        {
            view = Undo.AddComponent<BuildChoiceView>(row.gameObject);
        }

        SerializedObject viewObject = new(view);
        viewObject.FindProperty("selection").objectReferenceValue = selection;
        Append(viewObject.FindProperty("buttons"), button);
        Append(viewObject.FindProperty("labels"), button.GetComponentInChildren<TMP_Text>(true));
        viewObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(view);
        AssignClip(FindAll<MatchAudio>(scene)[0], report);
    }

    private static Button EnsureButton(
        Transform row,
        BuildSelection selection,
        int defenseIndex)
    {
        Transform existing = row.Find("RootsChoice");
        GameObject root;

        if (existing != null)
        {
            root = existing.gameObject;
        }
        else
        {
            root = new GameObject(
                "RootsChoice",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            Undo.RegisterCreatedObjectUndo(root, UndoName);
            root.transform.SetParent(row, false);
            root.transform.localScale = Vector3.one;
            GameObject textObject = new GameObject("Label", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(textObject, UndoName);
            textObject.transform.SetParent(root.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);
            TextMeshProUGUI label = Undo.AddComponent<TextMeshProUGUI>(textObject);
            label.fontSize = 22f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Cream;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
        }

        Image image = root.GetComponent<Image>();

        if (existing == null)
        {
            image.color = new Color(0.25f, 0.36f, 0.32f, 1f);
        }

        image.raycastTarget = true;
        Button button = root.GetComponent<Button>();
        button.targetGraphic = image;
        LayoutElement element = root.GetComponent<LayoutElement>();

        if (element == null)
        {
            element = Undo.AddComponent<LayoutElement>(root);
        }

        if (element.minWidth < 180f)
        {
            element.minWidth = 220f;
            element.preferredWidth = 260f;
            element.minHeight = 52f;
            element.preferredHeight = 56f;
            element.layoutPriority = 1;
        }

        if (root.GetComponent<UiClickSound>() == null)
        {
            Undo.AddComponent<UiClickSound>(root);
        }

        BuildChoiceButton choice = root.GetComponent<BuildChoiceButton>();

        if (choice == null)
        {
            choice = Undo.AddComponent<BuildChoiceButton>(root);
        }

        SerializedObject serialized = new(choice);
        serialized.FindProperty("selection").objectReferenceValue = selection;
        serialized.FindProperty("index").intValue = defenseIndex;
        serialized.ApplyModifiedProperties();
        return button;
    }

    private static void AssignClip(MatchAudio audio, StringBuilder report)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath);
        SerializedObject serialized = new(audio);
        SerializedProperty property = serialized.FindProperty("rootsAttackClip");

        if (property.objectReferenceValue == null && clip != null)
        {
            property.objectReferenceValue = clip;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(audio);
            return;
        }

        if (clip != null && property.objectReferenceValue != clip)
        {
            report.AppendLine("El audio de la terraza está personalizado y se conservó.");
        }
    }

    private static void FillSprite(
        TowerData data,
        Sprite current,
        Sprite sprite,
        bool realArt,
        StringBuilder report)
    {
        if (sprite == null)
        {
            return;
        }

        bool placeholder = current != null &&
            AssetDatabase.GetAssetPath(current).Contains("placeholder_");

        if (current == null || (placeholder && realArt))
        {
            SerializedObject serialized = new(data);
            serialized.FindProperty("worldSprite").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            report.AppendLine("Se completó el sprite de la terraza.");
        }
    }

    private static Sprite EnsurePlaceholder()
    {
        if (AssetDatabase.LoadMainAssetAtPath(PlaceholderPath) == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PlaceholderPath));
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            float center = 15.5f;

            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float alpha = Mathf.Sqrt(dx * dx + dy * dy) <= 12f ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(Earth.r, Earth.g, Earth.b, alpha));
                }
            }

            texture.Apply();
            File.WriteAllBytes(PlaceholderPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(PlaceholderPath);
        }

        TextureImporter importer = AssetImporter.GetAtPath(PlaceholderPath) as TextureImporter;

        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return LoadSprite(PlaceholderPath);
    }

    private static void Append(SerializedProperty property, Object value)
    {
        if (value == null)
        {
            return;
        }

        for (int i = 0; i < property.arraySize; i++)
        {
            if (property.GetArrayElementAtIndex(i).objectReferenceValue == value)
            {
                return;
            }
        }

        int index = property.arraySize;
        property.arraySize++;
        property.GetArrayElementAtIndex(index).objectReferenceValue = value;
    }

    private static Sprite LoadSprite(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Sprite sprite)
            {
                return sprite;
            }
        }

        return null;
    }

    private static SpriteRenderer Visual(GameObject root)
    {
        Transform visual = root.transform.Find("Visual");
        return visual != null
            ? visual.GetComponent<SpriteRenderer>()
            : root.GetComponentInChildren<SpriteRenderer>(true);
    }

    private static int Count<T>(Scene scene) where T : Component
    {
        return FindAll<T>(scene).Count;
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

    private static void Note(StringBuilder report, string label, float actual, float initial)
    {
        if (!Mathf.Approximately(actual, initial))
        {
            report.AppendLine(
                "Ajuste personalizado de " + label + ": " + actual +
                ". Inicial sugerido: " + initial + "."
            );
        }
    }

    private static void Report(string message)
    {
        EditorUtility.DisplayDialog("Bastión del Valle", message, "Entendido");
        Debug.Log(message);
    }
}
