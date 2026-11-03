using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfluenceContentConfigurator
{
    private const string MenuPath =
        "Bastion del Valle/Mapas/22 - Integrar Agua y Langostas";

    private const string UndoName = "Integrar Agua y Langostas";
    private const string Level02Path = "Assets/Scenes/Level_02.unity";
    private const string WaterDataPath = "Assets/Data/TowerData_Water.asset";
    private const string LocustDataPath = "Assets/Data/EnemyData_Locust.asset";
    private const string AphidDataPath = "Assets/Data/EnemyData_Basic.asset";
    private const string SeedDataPath = "Assets/Data/TowerData_Basic.asset";
    private const string SeedTowerPath = "Assets/Prefabs/Tower_Basic.prefab";
    private const string AphidPath = "Assets/Prefabs/Enemy_Basic.prefab";
    private const string SeedShotPath = "Assets/Prefabs/Projectile_Basic.prefab";
    private const string WaterTowerPath = "Assets/Prefabs/Tower_Water.prefab";
    private const string LocustPath = "Assets/Prefabs/Enemy_Locust.prefab";
    private const string WaterShotPath = "Assets/Prefabs/Projectile_Water.prefab";
    private const string WaterSpritePath =
        "Assets/Art/Projectiles/projectile_water_v001.png";
    private const string TowerSpritePath =
        "Assets/Art/Towers/tower_water_idle_v001.png";
    private const string LocustSpritePath =
        "Assets/Art/Enemies/enemy_locust_idle_v001.png";
    private const string WaterShotAudio = "Assets/Audio/SFX/sfx_water_shot.wav";
    private const string WaterHitAudio = "Assets/Audio/SFX/sfx_water_hit.wav";
    private const string WaterBuildAudio = "Assets/Audio/SFX/sfx_build_water.wav";
    private const string WaterId = "tower_water";

    private static readonly Color Band = new(0.149f, 0.239f, 0.188f, 0.94f);
    private static readonly Color Cream = new(1f, 0.945f, 0.776f, 1f);
    private static readonly Color Turquoise = new(0.25f, 0.86f, 0.84f, 1f);

    [MenuItem(MenuPath, false, 22)]
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
            Report(
                "Abre Level_02 y vuelve a ejecutar la acción. " +
                "No se modificó ninguna escena."
            );
            return;
        }

        if (!Prevalidate(out string error))
        {
            Report(error);
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        var report = new StringBuilder();

        try
        {
            EnemyData aphid = AssetDatabase.LoadAssetAtPath<EnemyData>(AphidDataPath);
            TowerData seed = AssetDatabase.LoadAssetAtPath<TowerData>(SeedDataPath);
            float locustSpeed = aphid.MovementSpeed * 1.6f;
            TowerData water = EnsureWaterData(report);
            EnemyData locust = EnsureLocustData(locustSpeed, report);
            Sprite waterSprite = EnsureWaterSprite();
            Projectile shot = EnsureWaterProjectile(waterSprite, report);
            TowerController waterTower = EnsureWaterTower(water, shot, report);
            EnemyMovement locustEnemy = EnsureLocust(locust, report);
            TowerController seedTower =
                AssetDatabase.LoadAssetAtPath<TowerController>(SeedTowerPath);
            EnemyMovement aphidEnemy =
                AssetDatabase.LoadAssetAtPath<EnemyMovement>(AphidPath);
            WireScene(
                scene,
                seed,
                seedTower,
                waterTower,
                aphidEnemy,
                locustEnemy,
                report
            );
            CompactMatchHud.Apply(scene, report);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            SaveOwnedAssets(water, locust, shot, waterTower, locustEnemy);
            report.AppendLine(
                "Level_02 quedó marcada como modificada. " +
                "Guárdala desde el editor. No se guardó la escena ni Level_01."
            );
            report.AppendLine(
                "Guardián Semilla conserva el coste de sus datos: " +
                seed.BuildCost + "."
            );
            Report(report.ToString());
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(exception);
            Report("No se completó la integración.\n" + exception.Message);
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenu()
    {
        return !EditorApplication.isPlaying &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling;
    }

    private static bool Prevalidate(out string error)
    {
        string[] required =
        {
            AphidDataPath,
            SeedDataPath,
            SeedTowerPath,
            AphidPath,
            SeedShotPath,
            TowerSpritePath,
            LocustSpritePath,
            WaterShotAudio,
            WaterHitAudio,
            WaterBuildAudio
        };

        for (int i = 0; i < required.Length; i++)
        {
            if (AssetDatabase.LoadMainAssetAtPath(required[i]) == null)
            {
                error = "Falta el recurso " + required[i] + ". No se modificó la escena.";
                return false;
            }
        }

        Scene scene = SceneManager.GetActiveScene();

        if (Count<WaveManager>(scene) != 1 ||
            Count<MatchAudio>(scene) != 1 ||
            Count<HUDController>(scene) != 1 ||
            Count<BuildPlot>(scene) < 1)
        {
            error =
                "Level_02 necesita un WaveManager, un MatchAudio, " +
                "un HUD y al menos una parcela. No se modificó la escena.";
            return false;
        }

        if (FindNamed(scene, "HUD") == null)
        {
            error = "Level_02 no tiene HUD. No se modificó la escena.";
            return false;
        }

        error = null;
        return true;
    }

    private static TowerData EnsureWaterData(StringBuilder report)
    {
        TowerData data = AssetDatabase.LoadAssetAtPath<TowerData>(WaterDataPath);
        Sprite sprite = LoadSprite(TowerSpritePath);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<TowerData>();
            AssetDatabase.CreateAsset(data, WaterDataPath);
            WriteWater(data, sprite);
            report.AppendLine("Se creó TowerData_Water.");
            return data;
        }

        NoteFloat(report, "Canal Vivo coste", data.BuildCost, 40);
        NoteFloat(report, "Canal Vivo alcance", data.AttackRange, 2.1f);
        NoteFloat(report, "Canal Vivo intervalo", data.AttackInterval, 1.2f);
        NoteFloat(report, "Canal Vivo daño", data.ProjectileDamage, 2);
        NoteFloat(report, "Canal Vivo proyectil", data.ProjectileSpeed, 8f);
        NoteFloat(report, "Canal Vivo ralentización", data.SlowMultiplier, 0.65f);
        NoteFloat(report, "Canal Vivo duración", data.SlowDuration, 2f);

        if (data.WorldSprite == null && sprite != null)
        {
            SetObject(data, "worldSprite", sprite);
            report.AppendLine("Se asignó el sprite faltante de Canal Vivo.");
        }

        report.AppendLine("Se reutilizó TowerData_Water.");
        return data;
    }

    private static void WriteWater(TowerData data, Sprite sprite)
    {
        SerializedObject serialized = new(data);
        serialized.FindProperty("towerId").stringValue = WaterId;
        serialized.FindProperty("displayName").stringValue = "Canal Vivo";
        serialized.FindProperty("description").stringValue =
            "Canal que golpea suave y ralentiza al enemigo impactado.";
        serialized.FindProperty("buildCost").intValue = 40;
        serialized.FindProperty("attackRange").floatValue = 2.1f;
        serialized.FindProperty("attackInterval").floatValue = 1.2f;
        serialized.FindProperty("projectileDamage").intValue = 2;
        serialized.FindProperty("projectileSpeed").floatValue = 8f;
        serialized.FindProperty("slowMultiplier").floatValue = 0.65f;
        serialized.FindProperty("slowDuration").floatValue = 2f;
        serialized.FindProperty("worldSprite").objectReferenceValue = sprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }

    private static EnemyData EnsureLocustData(float initialSpeed, StringBuilder report)
    {
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(LocustDataPath);
        Sprite sprite = LoadSprite(LocustSpritePath);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<EnemyData>();
            AssetDatabase.CreateAsset(data, LocustDataPath);
            SerializedObject serialized = new(data);
            serialized.FindProperty("enemyId").stringValue = "enemy_locust";
            serialized.FindProperty("displayName").stringValue = "Langosta Veloz";
            serialized.FindProperty("description").stringValue =
                "Langosta que recorre el canal más rápido que el pulgón.";
            serialized.FindProperty("maxHealth").intValue = 20;
            serialized.FindProperty("movementSpeed").floatValue = initialSpeed;
            serialized.FindProperty("reward").intValue = 10;
            serialized.FindProperty("goalDamage").intValue = 10;
            serialized.FindProperty("worldSprite").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            report.AppendLine(
                "Se creó EnemyData_Locust con velocidad " +
                initialSpeed.ToString("0.00") + "."
            );
            return data;
        }

        NoteFloat(report, "Langosta vida", data.MaxHealth, 20);
        NoteFloat(report, "Langosta velocidad", data.MovementSpeed, initialSpeed);
        NoteFloat(report, "Langosta recompensa", data.Reward, 10);
        NoteFloat(report, "Langosta daño al corazón", data.GoalDamage, 10);

        if (data.WorldSprite == null && sprite != null)
        {
            SetObject(data, "worldSprite", sprite);
            report.AppendLine("Se asignó el sprite faltante de la langosta.");
        }

        report.AppendLine("Se reutilizó EnemyData_Locust.");
        return data;
    }

    private static Sprite EnsureWaterSprite()
    {
        if (AssetDatabase.LoadMainAssetAtPath(WaterSpritePath) == null)
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(WaterSpritePath) ?? "Assets/Art/Projectiles"
            );
            File.WriteAllBytes(WaterSpritePath, CirclePng(32));
            AssetDatabase.ImportAsset(WaterSpritePath);
        }

        TextureImporter importer =
            AssetImporter.GetAtPath(WaterSpritePath) as TextureImporter;

        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = 32f;
            importer.SaveAndReimport();
        }

        return LoadSprite(WaterSpritePath);
    }

    private static byte[] CirclePng(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = (size - 1) * 0.5f;
        float radius = size * 0.42f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = distance <= radius ? 1f : 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        byte[] bytes = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);
        return bytes;
    }

    private static Projectile EnsureWaterProjectile(Sprite sprite, StringBuilder report)
    {
        Projectile source = AssetDatabase.LoadAssetAtPath<Projectile>(SeedShotPath);
        EnsurePrefab(WaterShotPath, source, "Projectile_Water");
        Sprite seedSprite = VisualRenderer(source.gameObject).sprite;
        GameObject contents = PrefabUtility.LoadPrefabContents(WaterShotPath);

        try
        {
            contents.name = "Projectile_Water";
            SpriteRenderer renderer = VisualRenderer(contents);

            if (renderer != null && sprite != null &&
                (renderer.sprite == null || renderer.sprite == seedSprite))
            {
                renderer.sprite = sprite;
                renderer.color = Turquoise;
            }
            else if (renderer != null && renderer.sprite != sprite)
            {
                report.AppendLine(
                    "El sprite del pulso de agua está personalizado y se conservó."
                );
            }

            PrefabUtility.SaveAsPrefabAsset(contents, WaterShotPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return AssetDatabase.LoadAssetAtPath<Projectile>(WaterShotPath);
    }

    private static TowerController EnsureWaterTower(
        TowerData data,
        Projectile shot,
        StringBuilder report)
    {
        TowerController source = AssetDatabase.LoadAssetAtPath<TowerController>(SeedTowerPath);
        EnsurePrefab(WaterTowerPath, source, "Tower_Water");
        Sprite seedSprite = VisualRenderer(source.gameObject).sprite;
        Projectile seedShot = PrefabShot(source);
        GameObject contents = PrefabUtility.LoadPrefabContents(WaterTowerPath);

        try
        {
            contents.name = "Tower_Water";
            TowerController tower = contents.GetComponent<TowerController>();
            AssignOwned(
                tower,
                "towerData",
                data,
                tower.Data,
                source.Data,
                report,
                "datos de Canal Vivo"
            );
            TowerAttack attack = contents.GetComponent<TowerAttack>();
            AssignOwned(
                attack,
                "projectilePrefab",
                shot,
                PrefabShot(tower),
                seedShot,
                report,
                "proyectil de Canal Vivo"
            );
            SpriteRenderer renderer = VisualRenderer(contents);

            if (renderer != null && data.WorldSprite != null &&
                (renderer.sprite == null || renderer.sprite == seedSprite))
            {
                renderer.sprite = data.WorldSprite;
            }

            PrefabUtility.SaveAsPrefabAsset(contents, WaterTowerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return AssetDatabase.LoadAssetAtPath<TowerController>(WaterTowerPath);
    }

    private static EnemyMovement EnsureLocust(EnemyData data, StringBuilder report)
    {
        EnemyMovement source = AssetDatabase.LoadAssetAtPath<EnemyMovement>(AphidPath);
        EnsurePrefab(LocustPath, source, "Enemy_Locust");
        Sprite seedSprite = VisualRenderer(source.gameObject).sprite;
        GameObject contents = PrefabUtility.LoadPrefabContents(LocustPath);

        try
        {
            contents.name = "Enemy_Locust";
            EnemyMovement enemy = contents.GetComponent<EnemyMovement>();
            AssignOwned(
                enemy,
                "enemyData",
                data,
                enemy.Data,
                source.Data,
                report,
                "datos de la langosta"
            );
            SpriteRenderer renderer = VisualRenderer(contents);

            if (renderer != null && data.WorldSprite != null &&
                (renderer.sprite == null || renderer.sprite == seedSprite))
            {
                renderer.sprite = data.WorldSprite;
            }

            if (contents.GetComponent<EnemySlow>() == null)
            {
                contents.AddComponent<EnemySlow>();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, LocustPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return AssetDatabase.LoadAssetAtPath<EnemyMovement>(LocustPath);
    }

    private static T EnsurePrefab<T>(string path, T source, string objectName)
        where T : Component
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        if (source == null)
        {
            throw new InvalidOperationException("No está el prefab de origen para " + objectName + ".");
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source.gameObject);
        PrefabUtility.UnpackPrefabInstance(
            instance,
            PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction
        );
        instance.name = objectName;
        PrefabUtility.SaveAsPrefabAsset(instance, path);
        UnityEngine.Object.DestroyImmediate(instance);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static void WireScene(
        Scene scene,
        TowerData seedData,
        TowerController seedTower,
        TowerController waterTower,
        EnemyMovement aphid,
        EnemyMovement locust,
        StringBuilder report)
    {
        BuildSelection selection = EnsureSelection(scene, seedTower, waterTower, report);
        EnsureChoiceHud(scene, selection);
        AssignPlots(scene, selection);
        AssignAudio(scene, report);
        AssignWaves(scene, aphid, locust, report);
        report.AppendLine(
            "Selección inicial disponible: " + seedData.DisplayName +
            " y Canal Vivo."
        );
    }

    private static BuildSelection EnsureSelection(
        Scene scene,
        TowerController seedTower,
        TowerController waterTower,
        StringBuilder report)
    {
        var found = FindAll<BuildSelection>(scene);

        if (found.Count > 1)
        {
            throw new InvalidOperationException(
                "Level_02 tiene más de un BuildSelection."
            );
        }

        BuildSelection selection;

        if (found.Count == 1)
        {
            selection = found[0];
        }
        else
        {
            GameObject host = new GameObject("BuildSelection");
            Undo.RegisterCreatedObjectUndo(host, UndoName);
            selection = Undo.AddComponent<BuildSelection>(host);
        }

        SerializedObject serialized = new(selection);
        SerializedProperty defenses = serialized.FindProperty("defenses");
        bool empty = defenses.arraySize < 2 ||
            defenses.GetArrayElementAtIndex(0).objectReferenceValue == null ||
            defenses.GetArrayElementAtIndex(1).objectReferenceValue == null;

        if (empty)
        {
            defenses.arraySize = 2;
            defenses.GetArrayElementAtIndex(0).objectReferenceValue = seedTower;
            defenses.GetArrayElementAtIndex(1).objectReferenceValue = waterTower;
            serialized.FindProperty("selectedIndex").intValue = 0;
            report.AppendLine("La selección empieza en el Guardián Semilla.");
        }
        else
        {
            TowerController first =
                defenses.GetArrayElementAtIndex(0).objectReferenceValue as TowerController;
            TowerController second =
                defenses.GetArrayElementAtIndex(1).objectReferenceValue as TowerController;

            if (first != seedTower || second != waterTower)
            {
                report.AppendLine(
                    "La selección de defensas está personalizada y se conservó."
                );
            }
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(selection);
        return selection;
    }

    private static void EnsureChoiceHud(Scene scene, BuildSelection selection)
    {
        Transform hud = FindNamed(scene, "HUD");
        Transform row = FindNamed(scene, "BuildChoiceRow");

        if (row == null)
        {
            GameObject created = new GameObject(
                "BuildChoiceRow",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            Undo.RegisterCreatedObjectUndo(created, UndoName);
            created.transform.SetParent(hud, false);
            row = created.transform;
        }

        RectTransform rect = (RectTransform)row;
        Undo.RecordObject(rect, UndoName);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 196f);
        rect.sizeDelta = new Vector2(-32f, 84f);
        rect.localScale = Vector3.one;
        Image background = row.GetComponent<Image>();

        if (background == null)
        {
            background = Undo.AddComponent<Image>(row.gameObject);
        }

        background.color = Band;
        background.raycastTarget = true;
        HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();

        if (group == null)
        {
            group = Undo.AddComponent<HorizontalLayoutGroup>(row.gameObject);
        }

        group.padding = new RectOffset(16, 16, 8, 8);
        group.spacing = 12f;
        group.childAlignment = TextAnchor.MiddleLeft;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
        BuildChoiceView view = row.GetComponent<BuildChoiceView>();

        if (view == null)
        {
            view = Undo.AddComponent<BuildChoiceView>(row.gameObject);
        }

        Button seedButton = EnsureChoiceButton(row, "SeedChoice", selection, 0);
        Button waterButton = EnsureChoiceButton(row, "WaterChoice", selection, 1);
        TMP_Text description = EnsureDescription(row, hud.GetComponent<HUDController>());
        SerializedObject viewObject = new(view);
        viewObject.FindProperty("selection").objectReferenceValue = selection;
        AssignButton(viewObject.FindProperty("buttons"), seedButton, waterButton);
        AssignLabel(
            viewObject.FindProperty("labels"),
            seedButton.GetComponentInChildren<TMP_Text>(true),
            waterButton.GetComponentInChildren<TMP_Text>(true)
        );
        viewObject.FindProperty("description").objectReferenceValue = description;
        viewObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(view);
    }

    private static Button EnsureChoiceButton(
        Transform row,
        string name,
        BuildSelection selection,
        int index)
    {
        Transform existing = row.Find(name);
        GameObject root;

        if (existing != null)
        {
            root = existing.gameObject;
        }
        else
        {
            root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            Undo.RegisterCreatedObjectUndo(root, UndoName);
            root.transform.SetParent(row, false);
            root.transform.localScale = Vector3.one;
        }

        Image image = root.GetComponent<Image>();
        image.color = index == 0
            ? new Color(0.22f, 0.62f, 0.58f, 1f)
            : new Color(0.25f, 0.36f, 0.32f, 1f);
        image.raycastTarget = true;
        Button button = root.GetComponent<Button>();
        button.targetGraphic = image;
        LayoutElement element = root.GetComponent<LayoutElement>();

        if (element == null)
        {
            element = Undo.AddComponent<LayoutElement>(root);
        }

        element.minWidth = 280f;
        element.preferredWidth = 320f;
        element.flexibleWidth = 0f;
        element.minHeight = 52f;
        element.preferredHeight = 56f;
        element.layoutPriority = 1;

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
        serialized.FindProperty("index").intValue = index;
        serialized.ApplyModifiedProperties();
        TMP_Text label = root.GetComponentInChildren<TMP_Text>(true);

        if (label == null)
        {
            GameObject textObject = new GameObject("Label", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(textObject, UndoName);
            textObject.transform.SetParent(root.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);
            textRect.localScale = Vector3.one;
            label = Undo.AddComponent<TextMeshProUGUI>(textObject);
            label.fontSize = 22f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Cream;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
        }

        return button;
    }

    private static TMP_Text EnsureDescription(Transform row, HUDController hud)
    {
        Transform existing = row.Find("BuildChoiceDescription");
        GameObject root;

        if (existing != null)
        {
            root = existing.gameObject;
        }
        else
        {
            root = new GameObject("BuildChoiceDescription", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, UndoName);
            root.transform.SetParent(row, false);
            root.transform.localScale = Vector3.one;
        }

        LayoutElement element = root.GetComponent<LayoutElement>();

        if (element == null)
        {
            element = Undo.AddComponent<LayoutElement>(root);
        }

        element.minWidth = 420f;
        element.preferredWidth = 640f;
        element.flexibleWidth = 1f;
        element.minHeight = 56f;
        element.preferredHeight = 64f;
        element.layoutPriority = 1;
        TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>();

        if (text == null)
        {
            text = Undo.AddComponent<TextMeshProUGUI>(root);
        }

        text.fontSize = 22f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = Cream;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        TMP_FontAsset font = FindFont(hud);

        if (font != null && text.font == null)
        {
            text.font = font;
        }

        return text;
    }

    private static void AssignPlots(Scene scene, BuildSelection selection)
    {
        foreach (BuildPlot plot in FindAll<BuildPlot>(scene))
        {
            SerializedObject serialized = new(plot);
            SerializedProperty property = serialized.FindProperty("buildSelection");

            if (property.objectReferenceValue == selection)
            {
                continue;
            }

            if (property.objectReferenceValue != null)
            {
                continue;
            }

            property.objectReferenceValue = selection;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(plot);
        }
    }

    private static void AssignAudio(Scene scene, StringBuilder report)
    {
        MatchAudio audio = FindAll<MatchAudio>(scene)[0];
        AssignClip(audio, "waterBuildClip", WaterBuildAudio, "construcción de agua", report);
        AssignClip(audio, "waterShotClip", WaterShotAudio, "disparo de agua", report);
        AssignClip(audio, "waterHitClip", WaterHitAudio, "impacto de agua", report);
    }

    private static void AssignClip(
        MatchAudio audio,
        string fieldName,
        string path,
        string label,
        StringBuilder report)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        SerializedObject serialized = new(audio);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property.objectReferenceValue == null)
        {
            property.objectReferenceValue = clip;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(audio);
            return;
        }

        if (property.objectReferenceValue != clip)
        {
            report.AppendLine(
                "El audio de " + label + " está personalizado y se conservó."
            );
        }
    }

    private static void AssignWaves(
        Scene scene,
        EnemyMovement aphid,
        EnemyMovement locust,
        StringBuilder report)
    {
        WaveManager waves = FindAll<WaveManager>(scene)[0];
        SerializedObject serialized = new(waves);
        SerializedProperty list = serialized.FindProperty("waves");

        if (list.arraySize >= 4)
        {
            report.AppendLine(
                "Level_02 ya tiene las cuatro oleadas. No se restauró la composición de dos."
            );
            return;
        }

        if (list.arraySize < 2)
        {
            report.AppendLine(
                "Level_02 no tiene dos oleadas. No se reescribió la composición."
            );
            return;
        }

        WriteWaveIfUntouched(
            list.GetArrayElementAtIndex(0),
            6,
            1.8f,
            WaveComposition.FirstWave(),
            aphid,
            locust,
            "Oleada 1",
            report
        );
        WriteWaveIfUntouched(
            list.GetArrayElementAtIndex(1),
            8,
            1.5f,
            LegacySecondWave(),
            aphid,
            locust,
            "Oleada 2",
            report
        );
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(waves);
    }

    private static void WriteWaveIfUntouched(
        SerializedProperty wave,
        int count,
        float interval,
        int[] kinds,
        EnemyMovement aphid,
        EnemyMovement locust,
        string label,
        StringBuilder report)
    {
        SerializedProperty sequence = wave.FindPropertyRelative("sequence");
        bool empty = sequence.arraySize == 0;
        bool block21 =
            empty &&
            wave.FindPropertyRelative("enemyCount").intValue == count &&
            Mathf.Approximately(
                wave.FindPropertyRelative("spawnInterval").floatValue,
                interval
            );

        if (MatchesComposition(sequence, kinds, aphid, locust))
        {
            report.AppendLine(label + " ya tiene la composición de agua y langostas.");
            return;
        }

        if (!block21 && !empty)
        {
            report.AppendLine(
                label + " tiene una secuencia personalizada y se conservó."
            );
            return;
        }

        if (!block21)
        {
            report.AppendLine(
                label + " tiene cantidad o intervalo personalizados y se conservó."
            );
            return;
        }

        wave.FindPropertyRelative("enemyCount").intValue = kinds.Length;
        wave.FindPropertyRelative("spawnInterval").floatValue = interval;
        sequence.arraySize = kinds.Length;

        for (int i = 0; i < kinds.Length; i++)
        {
            sequence
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("enemyPrefab")
                .objectReferenceValue = kinds[i] == WaveComposition.Locust
                    ? locust
                    : aphid;
        }

        report.AppendLine(label + " quedó en " + kinds.Length + " apariciones.");
    }

    private static bool MatchesComposition(
        SerializedProperty sequence,
        int[] kinds,
        EnemyMovement aphid,
        EnemyMovement locust)
    {
        if (sequence.arraySize != kinds.Length)
        {
            return false;
        }

        for (int i = 0; i < kinds.Length; i++)
        {
            UnityEngine.Object prefab = sequence
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("enemyPrefab")
                .objectReferenceValue;
            UnityEngine.Object expected = kinds[i] == WaveComposition.Locust
                ? locust
                : aphid;

            if (prefab != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static void SaveOwnedAssets(
        TowerData water,
        EnemyData locust,
        Projectile shot,
        TowerController tower,
        EnemyMovement enemy)
    {
        AssetDatabase.SaveAssetIfDirty(water);
        AssetDatabase.SaveAssetIfDirty(locust);
        AssetDatabase.SaveAssetIfDirty(shot);
        AssetDatabase.SaveAssetIfDirty(tower);
        AssetDatabase.SaveAssetIfDirty(enemy);
        AssetDatabase.SaveAssetIfDirty(
            AssetDatabase.LoadMainAssetAtPath(WaterSpritePath)
        );
    }

    private static void AssignButton(
        SerializedProperty property,
        Button first,
        Button second)
    {
        if (property.arraySize < 2)
        {
            property.arraySize = 2;
        }

        property.GetArrayElementAtIndex(0).objectReferenceValue = first;
        property.GetArrayElementAtIndex(1).objectReferenceValue = second;
    }

    private static int[] LegacySecondWave()
    {
        return new[]
        {
            WaveComposition.Aphid,
            WaveComposition.Aphid,
            WaveComposition.Aphid,
            WaveComposition.Aphid,
            WaveComposition.Locust,
            WaveComposition.Locust,
            WaveComposition.Locust,
            WaveComposition.Locust
        };
    }

    private static void AssignLabel(
        SerializedProperty property,
        TMP_Text first,
        TMP_Text second)
    {
        if (property.arraySize < 2)
        {
            property.arraySize = 2;
        }

        property.GetArrayElementAtIndex(0).objectReferenceValue = first;
        property.GetArrayElementAtIndex(1).objectReferenceValue = second;
    }

    private static Projectile PrefabShot(TowerController tower)
    {
        TowerAttack attack = tower.GetComponent<TowerAttack>();
        SerializedObject serialized = new(attack);
        return serialized.FindProperty("projectilePrefab").objectReferenceValue as Projectile;
    }

    private static void AssignOwned(
        UnityEngine.Object target,
        string fieldName,
        UnityEngine.Object desired,
        UnityEngine.Object current,
        UnityEngine.Object seed,
        StringBuilder report,
        string label)
    {
        if (current == null || current == seed)
        {
            SetObject(target, fieldName, desired);
            return;
        }

        if (current != desired)
        {
            report.AppendLine(
                "La referencia de " + label + " está personalizada y se conservó."
            );
        }
    }

    private static SpriteRenderer VisualRenderer(GameObject root)
    {
        Transform visual = root.transform.Find("Visual");

        if (visual != null)
        {
            return visual.GetComponent<SpriteRenderer>();
        }

        return root.GetComponentInChildren<SpriteRenderer>(true);
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (sprite != null)
        {
            return sprite;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite found)
            {
                return found;
            }
        }

        return null;
    }

    private static TMP_FontAsset FindFont(HUDController hud)
    {
        if (hud == null)
        {
            return TMP_Settings.defaultFontAsset;
        }

        foreach (TMP_Text text in hud.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font != null)
            {
                return text.font;
            }
        }

        return TMP_Settings.defaultFontAsset;
    }

    private static void SetObject(
        UnityEngine.Object target,
        string fieldName,
        UnityEngine.Object value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            throw new InvalidOperationException(
                target.name + " no tiene el campo " + fieldName + "."
            );
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void NoteFloat(
        StringBuilder report,
        string label,
        float actual,
        float initial)
    {
        if (!Mathf.Approximately(actual, initial))
        {
            report.AppendLine(
                label + " personalizado: " + actual +
                ". Inicial: " + initial + "."
            );
        }
    }

    private static int Count<T>(Scene scene) where T : Component
    {
        return FindAll<T>(scene).Count;
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

    private static Transform FindNamed(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == objectName)
                {
                    return transform;
                }
            }
        }

        return null;
    }

    private static void Report(string message)
    {
        EditorUtility.DisplayDialog("Bastión del Valle", message, "Entendido");
        Debug.Log(message);
    }
}
