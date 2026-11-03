using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfluenceAirConfigurator
{
    private const string MenuPath =
        "Bastion del Valle/Mapas/26 - Integrar Cobertura Aerea";

    private const string UndoName = "Integrar Cobertura Aerea";
    private const string Level02Path = "Assets/Scenes/Level_02.unity";
    private const string CondorDataPath = "Assets/Data/TowerData_Condor.asset";
    private const string MothDataPath = "Assets/Data/EnemyData_Moth.asset";
    private const string AphidDataPath = "Assets/Data/EnemyData_Basic.asset";
    private const string CondorTowerPath = "Assets/Prefabs/Tower_Condor.prefab";
    private const string MothPath = "Assets/Prefabs/Enemy_Moth.prefab";
    private const string ShotPath = "Assets/Prefabs/Projectile_Condor.prefab";
    private const string SeedTowerPath = "Assets/Prefabs/Tower_Basic.prefab";
    private const string SeedEnemyPath = "Assets/Prefabs/Enemy_Basic.prefab";
    private const string SeedShotPath = "Assets/Prefabs/Projectile_Basic.prefab";
    private const string AphidPath = "Assets/Prefabs/Enemy_Basic.prefab";
    private const string LocustPath = "Assets/Prefabs/Enemy_Locust.prefab";
    private const string SwarmPath = "Assets/Prefabs/Enemy_Swarm.prefab";
    private const string CondorSpritePath = "Assets/Art/Towers/tower_condor_idle_v001.png";
    private const string MothSpritePath = "Assets/Art/Enemies/enemy_moth_idle_v001.png";
    private const string AudioPath = "Assets/Audio/SFX/sfx_condor_shot.wav";
    private const string CondorPlaceholder = "Assets/Art/Generated/Condor/placeholder_tower.png";
    private const string MothPlaceholder = "Assets/Art/Generated/Moth/placeholder_enemy.png";
    private const string ShotMarkPath = "Assets/Art/Generated/Condor/shot_mark.png";

    private static readonly Color Stone = new(0.45f, 0.58f, 0.72f, 1f);
    private static readonly Color Sky = new(0.62f, 0.78f, 0.9f, 1f);
    private static readonly Color Cream = new(1f, 0.945f, 0.776f, 1f);
    private static readonly Color Ash = new(0.72f, 0.66f, 0.58f, 1f);

    [MenuItem(MenuPath, false, 26)]
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
            bool condorArt = AssetDatabase.LoadMainAssetAtPath(CondorSpritePath) != null;
            bool mothArt = AssetDatabase.LoadMainAssetAtPath(MothSpritePath) != null;
            Sprite condorSprite = condorArt ? LoadSprite(CondorSpritePath) : EnsureMark(CondorPlaceholder, Stone);
            Sprite mothSprite = mothArt ? LoadSprite(MothSpritePath) : EnsureMark(MothPlaceholder, Ash);

            if (!condorArt || condorSprite == null)
            {
                report.AppendLine("Falta el sprite del Vigía. Se usó una marca temporal.");
            }

            if (!mothArt || mothSprite == null)
            {
                report.AppendLine("Falta el sprite de la polilla. Se usó una marca temporal.");
            }

            if (AssetDatabase.LoadMainAssetAtPath(AudioPath) == null)
            {
                report.AppendLine("Falta sfx_condor_shot. El Vigía sigue, sin ese sonido.");
            }

            float aphidSpeed = AphidSpeed();
            TowerData condor = EnsureCondorData(condorSprite, condorArt, report);
            EnemyData moth = EnsureMothData(mothSprite, mothArt, aphidSpeed, report);
            Projectile shot = EnsureShot(report);
            TowerController tower = EnsureTower(condor, shot, report);
            EnemyMovement mothPrefab = EnsureMoth(moth, report);
            AssignMasks(report);
            Wire(scene, tower, report);
            EnsureLanes(scene, report);
            EnsureSecondWave(scene, mothPrefab, report);
            ConfluenceInterfaceRepair.Apply(scene, report, false);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssetIfDirty(condor);
            AssetDatabase.SaveAssetIfDirty(moth);
            AssetDatabase.SaveAssetIfDirty(tower);
            AssetDatabase.SaveAssetIfDirty(mothPrefab);
            AssetDatabase.SaveAssetIfDirty(shot);
            report.AppendLine("Level_02 quedó marcada como modificada. Guárdala desde el editor.");
            Report(report.ToString());
        }
        catch (System.Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(exception);
            Report("No se integró la cobertura aérea.\n" + exception.Message);
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenu()
    {
        return !EditorApplication.isPlaying &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling;
    }

    private static float AphidSpeed()
    {
        EnemyData aphid = AssetDatabase.LoadAssetAtPath<EnemyData>(AphidDataPath);
        return aphid == null ? 2f : aphid.MovementSpeed;
    }

    private static TowerData EnsureCondorData(Sprite sprite, bool realArt, StringBuilder report)
    {
        TowerData data = AssetDatabase.LoadAssetAtPath<TowerData>(CondorDataPath);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<TowerData>();
            AssetDatabase.CreateAsset(data, CondorDataPath);
            SerializedObject serialized = new(data);
            serialized.FindProperty("towerId").stringValue = "tower_condor";
            serialized.FindProperty("displayName").stringValue = "Vigía del Cóndor";
            serialized.FindProperty("description").stringValue =
                "Prioriza amenazas aéreas y también alcanza a las terrestres";
            serialized.FindProperty("buildCost").intValue = 70;
            serialized.FindProperty("attackRange").floatValue = 3.8f;
            serialized.FindProperty("attackInterval").floatValue = 1.6f;
            serialized.FindProperty("projectileDamage").intValue = 20;
            serialized.FindProperty("projectileSpeed").floatValue = 10f;
            serialized.FindProperty("allowedTargets").intValue = (int)TravelMask.Both;
            serialized.FindProperty("aim").enumValueIndex = (int)TargetPriority.AirThenGround;
            serialized.FindProperty("worldSprite").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            report.AppendLine("Se creó TowerData_Condor: coste 70, alcance 3.8, intervalo 1.6, daño 20.");
            return data;
        }

        Note(report, "Vigía coste", data.BuildCost, 70);
        Note(report, "Vigía alcance", data.AttackRange, 3.8f);
        Note(report, "Vigía intervalo", data.AttackInterval, 1.6f);
        Note(report, "Vigía daño", data.ProjectileDamage, 20);
        Note(report, "Vigía proyectil", data.ProjectileSpeed, 10f);
        FillSprite(data, sprite, realArt, report, "del Vigía");
        FillMask(data, (int)TravelMask.Both, report, "El Vigía");
        report.AppendLine("Se reutilizó TowerData_Condor.");
        return data;
    }

    private static EnemyData EnsureMothData(
        Sprite sprite,
        bool realArt,
        float aphidSpeed,
        StringBuilder report)
    {
        float speed = WaveComposition.MothSpeed(aphidSpeed);
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(MothDataPath);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<EnemyData>();
            AssetDatabase.CreateAsset(data, MothDataPath);
            SerializedObject serialized = new(data);
            serialized.FindProperty("enemyId").stringValue = "enemy_moth";
            serialized.FindProperty("displayName").stringValue = "Polilla de Ceniza";
            serialized.FindProperty("description").stringValue =
                "Cruza por su corredor aéreo hasta el corazón";
            serialized.FindProperty("maxHealth").intValue = 30;
            serialized.FindProperty("movementSpeed").floatValue = speed;
            serialized.FindProperty("reward").intValue = 10;
            serialized.FindProperty("goalDamage").intValue = 10;
            serialized.FindProperty("travel").enumValueIndex = (int)EnemyTravel.Air;
            serialized.FindProperty("worldSprite").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            report.AppendLine(
                "Se creó EnemyData_Moth: vida 30, velocidad " +
                speed.ToString("0.00") + ", recompensa 10, daño al corazón 10."
            );
            return data;
        }

        Note(report, "Polilla vida", data.MaxHealth, 30);
        Note(report, "Polilla velocidad", data.MovementSpeed, speed);
        Note(report, "Polilla recompensa", data.Reward, 10);
        Note(report, "Polilla daño al corazón", data.GoalDamage, 10);

        if (data.Travel != EnemyTravel.Air)
        {
            report.AppendLine("La categoría de la polilla está personalizada y se conservó.");
        }

        FillEnemySprite(data, sprite, realArt, report);
        report.AppendLine("Se reutilizó EnemyData_Moth.");
        return data;
    }

    private static Projectile EnsureShot(StringBuilder report)
    {
        Projectile seed = AssetDatabase.LoadAssetAtPath<Projectile>(SeedShotPath);

        if (AssetDatabase.LoadAssetAtPath<Projectile>(ShotPath) == null)
        {
            if (seed == null)
            {
                throw new System.InvalidOperationException("No está el proyectil del Guardián Semilla.");
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(seed.gameObject);
            PrefabUtility.UnpackPrefabInstance(
                instance,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction
            );
            instance.name = "Projectile_Condor";
            PrefabUtility.SaveAsPrefabAsset(instance, ShotPath);
            UnityEngine.Object.DestroyImmediate(instance);
            report.AppendLine("Se creó Projectile_Condor.");
        }

        Sprite mark = EnsureMark(ShotMarkPath, Sky);
        Sprite seedSprite = seed == null ? null : seed.GetComponentInChildren<SpriteRenderer>(true)?.sprite;
        GameObject contents = PrefabUtility.LoadPrefabContents(ShotPath);

        try
        {
            contents.name = "Projectile_Condor";
            SpriteRenderer renderer = contents.GetComponentInChildren<SpriteRenderer>(true);

            if (renderer != null && mark != null &&
                (renderer.sprite == null || renderer.sprite == seedSprite))
            {
                renderer.sprite = mark;
                renderer.color = Sky;
            }

            PrefabUtility.SaveAsPrefabAsset(contents, ShotPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        report.AppendLine("El proyectil del Vigía usa una marca propia.");
        return AssetDatabase.LoadAssetAtPath<Projectile>(ShotPath);
    }

    private static TowerController EnsureTower(
        TowerData data,
        Projectile shot,
        StringBuilder report)
    {
        TowerController source = AssetDatabase.LoadAssetAtPath<TowerController>(SeedTowerPath);
        EnsureIndependent(CondorTowerPath, source == null ? null : source.gameObject, "Tower_Condor");
        Sprite seedSprite = source == null ? null : Visual(source.gameObject)?.sprite;
        Projectile seedShot = source == null ? null : SeedProjectile(source.gameObject);
        GameObject contents = PrefabUtility.LoadPrefabContents(CondorTowerPath);

        try
        {
            contents.name = "Tower_Condor";
            TowerController tower = contents.GetComponent<TowerController>();
            AssignReference(tower, "towerData", data, source == null ? null : source.Data, report,
                "Los datos del Vigía están personalizados y se conservaron.");
            TowerAttack attack = contents.GetComponent<TowerAttack>();

            if (attack != null)
            {
                AssignReference(
                    attack,
                    "projectilePrefab",
                    shot,
                    seedShot,
                    report,
                    "El proyectil del Vigía está personalizado y se conservó.");
            }

            SpriteRenderer renderer = Visual(contents);

            if (renderer != null && data.WorldSprite != null &&
                (renderer.sprite == null || renderer.sprite == seedSprite))
            {
                renderer.sprite = data.WorldSprite;
            }

            PrefabUtility.SaveAsPrefabAsset(contents, CondorTowerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        report.AppendLine("Se reutilizó Tower_Condor.");
        return AssetDatabase.LoadAssetAtPath<TowerController>(CondorTowerPath);
    }

    private static EnemyMovement EnsureMoth(EnemyData data, StringBuilder report)
    {
        EnemyMovement source = AssetDatabase.LoadAssetAtPath<EnemyMovement>(SeedEnemyPath);
        EnsureIndependent(MothPath, source == null ? null : source.gameObject, "Enemy_Moth");
        Sprite seedSprite = source == null ? null : Visual(source.gameObject)?.sprite;
        GameObject contents = PrefabUtility.LoadPrefabContents(MothPath);

        try
        {
            contents.name = "Enemy_Moth";
            EnemyMovement enemy = contents.GetComponent<EnemyMovement>();
            AssignReference(
                enemy,
                "enemyData",
                data,
                source == null ? null : source.Data,
                report,
                "Los datos de la polilla están personalizados y se conservaron.");
            SpriteRenderer renderer = Visual(contents);

            if (renderer != null && data.WorldSprite != null &&
                (renderer.sprite == null || renderer.sprite == seedSprite))
            {
                renderer.sprite = data.WorldSprite;
            }

            if (contents.transform.Find("FlightShadow") == null)
            {
                var shadow = new GameObject("FlightShadow");
                shadow.transform.SetParent(contents.transform, false);
                shadow.transform.localPosition = new Vector3(0.1f, -0.42f, 0f);
                shadow.transform.localScale = new Vector3(0.7f, 0.28f, 1f);
                SpriteRenderer shade = shadow.AddComponent<SpriteRenderer>();
                shade.sprite = renderer == null ? null : renderer.sprite;
                shade.color = new Color(0.08f, 0.1f, 0.08f, 0.28f);
                shade.sortingOrder = (renderer == null ? 0 : renderer.sortingOrder) - 3;
            }

            PrefabUtility.SaveAsPrefabAsset(contents, MothPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        report.AppendLine("Se reutilizó Enemy_Moth.");
        return AssetDatabase.LoadAssetAtPath<EnemyMovement>(MothPath);
    }

    private static void AssignMasks(StringBuilder report)
    {
        FillStoredMask("Assets/Data/TowerData_Basic.asset", (int)TravelMask.Both, report, "El Guardián");
        FillStoredMask("Assets/Data/TowerData_Water.asset", (int)TravelMask.Ground, report, "El canal");
        FillStoredMask("Assets/Data/TowerData_Pollinator.asset", (int)TravelMask.Ground, report, "El refugio");
        FillStoredMask("Assets/Data/TowerData_Roots.asset", (int)TravelMask.Ground, report, "La terraza");
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
            report.AppendLine("El Vigía entró en la selección, sin cambiar la defensa inicial.");
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

        Transform buildRow = FindNamed(scene, "BuildChoiceRow");
        Button button = EnsureButton(buildRow, selection, defenseIndex);
        BuildChoiceView view = buildRow.GetComponent<BuildChoiceView>();

        if (view == null)
        {
            view = Undo.AddComponent<BuildChoiceView>(buildRow.gameObject);
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
        Transform existing = FindNamed(row.gameObject.scene, "CondorChoice");
        GameObject root;

        if (existing != null)
        {
            root = existing.gameObject;

            if (existing.parent != row)
            {
                Undo.SetTransformParent(existing, row, UndoName);
            }
        }
        else
        {
            root = new GameObject(
                "CondorChoice",
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
            image.color = new Color(0.24f, 0.34f, 0.46f, 1f);
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
            element.minWidth = 240f;
            element.preferredWidth = 280f;
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

    private static void EnsureLanes(Scene scene, StringBuilder report)
    {
        int lanes = 0;

        foreach (EnemySpawner spawner in FindAll<EnemySpawner>(scene))
        {
            SerializedObject serialized = new(spawner);

            if (serialized.FindProperty("navigator").objectReferenceValue == null)
            {
                continue;
            }

            AirLane lane = spawner.GetComponent<AirLane>();

            if (lane == null)
            {
                lane = Undo.AddComponent<AirLane>(spawner.gameObject);
            }

            SerializedObject laneObject = new(lane);
            SerializedProperty points = laneObject.FindProperty("points");

            if (points.arraySize >= 2 && PointsAssigned(points))
            {
                lanes++;
                continue;
            }

            if (points.arraySize > 0)
            {
                report.AppendLine(
                    spawner.name + " tiene un corredor incompleto y se conservó."
                );
                continue;
            }

            Vector3 start = spawner.transform.position;
            bool upper = start.y >= 0f;
            Vector3[] arc =
            {
                start,
                upper ? new Vector3(-1.5f, 3.4f, 0f) : new Vector3(-1.5f, -3.4f, 0f),
                upper ? new Vector3(2.5f, 2.2f, 0f) : new Vector3(2.5f, -1.8f, 0f),
                new Vector3(6.5f, 0.5f, 0f)
            };
            points.arraySize = arc.Length;

            for (int i = 0; i < arc.Length; i++)
            {
                var marker = new GameObject("AirPoint_" + i);
                Undo.RegisterCreatedObjectUndo(marker, UndoName);
                marker.transform.SetParent(spawner.transform, false);
                marker.transform.position = arc[i];
                points.GetArrayElementAtIndex(i).objectReferenceValue = marker.transform;
            }

            laneObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(lane);
            lanes++;
            report.AppendLine("Se trazó el corredor aéreo de " + spawner.name + ".");
        }

        if (lanes < 2)
        {
            report.AppendLine("Level_02 no tiene dos accesos con navegador para los corredores.");
        }
    }

    private static void EnsureSecondWave(
        Scene scene,
        EnemyMovement moth,
        StringBuilder report)
    {
        WaveManager manager = FindAll<WaveManager>(scene)[0];
        SerializedObject serialized = new(manager);
        SerializedProperty waves = serialized.FindProperty("waves");

        if (waves.arraySize >= 4)
        {
            report.AppendLine(
                "Level_02 ya tiene las cuatro oleadas. No se restauró la oleada 2."
            );
            return;
        }

        if (waves.arraySize < 2)
        {
            report.AppendLine("No hay segunda oleada. No se añadieron polillas.");
            return;
        }

        SerializedProperty wave = waves.GetArrayElementAtIndex(1);
        SerializedProperty sequence = wave.FindPropertyRelative("sequence");
        EnemyMovement aphid = AssetDatabase.LoadAssetAtPath<EnemyMovement>(AphidPath);
        EnemyMovement locust = AssetDatabase.LoadAssetAtPath<EnemyMovement>(LocustPath);
        EnemyMovement swarm = AssetDatabase.LoadAssetAtPath<EnemyMovement>(SwarmPath);

        if (MatchesAir(sequence, aphid, locust, swarm, moth))
        {
            report.AppendLine("La oleada 2 ya incluye dos polillas.");
            return;
        }

        if (Contains(sequence, moth))
        {
            report.AppendLine("La oleada 2 está personalizada y se conservó.");
            return;
        }

        if (!MatchesRefuge(sequence, aphid, locust, swarm))
        {
            float interval = wave.FindPropertyRelative("spawnInterval").floatValue;

            if (swarm == null ||
                !MatchesLegacy(sequence, aphid, locust) ||
                !Mathf.Approximately(interval, 1.5f))
            {
                report.AppendLine("La oleada 2 está personalizada y se conservó.");
                return;
            }

            WriteKnownSecond(sequence, aphid, locust, swarm, moth);
            wave.FindPropertyRelative("enemyCount").intValue = 18;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
            report.AppendLine(
                "La oleada 2 de prueba pasó de 4 y 4 a 18 individuos, " +
                "conservando el intervalo de 1.5 s."
            );
            return;
        }

        int count = sequence.arraySize;
        sequence.arraySize = count + 2;

        for (int i = 0; i < 2; i++)
        {
            SerializedProperty entry = sequence.GetArrayElementAtIndex(count + i);
            entry.FindPropertyRelative("enemyPrefab").objectReferenceValue = moth;
            entry.FindPropertyRelative("partySize").intValue = 1;
        }

        wave.FindPropertyRelative("enemyCount").intValue = 18;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
        report.AppendLine("La oleada 2 quedó en 18 individuos, con dos polillas al final.");
    }

    private static bool MatchesRefuge(
        SerializedProperty sequence,
        EnemyMovement aphid,
        EnemyMovement locust,
        EnemyMovement swarm)
    {
        return sequence.arraySize == 8 &&
            MatchesPrefix(sequence, 8, aphid, locust, swarm);
    }

    private static bool MatchesPrefix(
        SerializedProperty sequence,
        int length,
        EnemyMovement aphid,
        EnemyMovement locust,
        EnemyMovement swarm)
    {
        if (sequence.arraySize < length)
        {
            return false;
        }

        for (int i = 0; i < length; i++)
        {
            SerializedProperty entry = sequence.GetArrayElementAtIndex(i);
            UnityEngine.Object prefab = entry.FindPropertyRelative("enemyPrefab").objectReferenceValue;
            int party = entry.FindPropertyRelative("partySize").intValue;

            if (party < 1)
            {
                party = 1;
            }

            UnityEngine.Object expected = i < 3 ? aphid : i < 6 ? locust : swarm;
            int expectedParty = i < 6 ? 1 : 5;

            if (prefab != expected || party != expectedParty)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesLegacy(
        SerializedProperty sequence,
        EnemyMovement aphid,
        EnemyMovement locust)
    {
        if (sequence.arraySize != 8 || aphid == null || locust == null)
        {
            return false;
        }

        for (int i = 0; i < 8; i++)
        {
            SerializedProperty entry = sequence.GetArrayElementAtIndex(i);
            int party = entry.FindPropertyRelative("partySize").intValue;

            if (party < 1)
            {
                party = 1;
            }

            UnityEngine.Object expected = i < 4 ? aphid : locust;

            if (entry.FindPropertyRelative("enemyPrefab").objectReferenceValue != expected ||
                party != 1)
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteKnownSecond(
        SerializedProperty sequence,
        EnemyMovement aphid,
        EnemyMovement locust,
        EnemyMovement swarm,
        EnemyMovement moth)
    {
        int[] kinds =
        {
            WaveComposition.Aphid, WaveComposition.Aphid, WaveComposition.Aphid,
            WaveComposition.Locust, WaveComposition.Locust, WaveComposition.Locust,
            WaveComposition.Swarm, WaveComposition.Swarm,
            WaveComposition.Moth, WaveComposition.Moth
        };
        sequence.arraySize = kinds.Length;

        for (int i = 0; i < kinds.Length; i++)
        {
            SerializedProperty entry = sequence.GetArrayElementAtIndex(i);
            EnemyMovement prefab = aphid;

            if (kinds[i] == WaveComposition.Locust)
            {
                prefab = locust;
            }
            else if (kinds[i] == WaveComposition.Swarm)
            {
                prefab = swarm;
            }
            else if (kinds[i] == WaveComposition.Moth)
            {
                prefab = moth;
            }

            entry.FindPropertyRelative("enemyPrefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("partySize").intValue =
                kinds[i] == WaveComposition.Swarm
                    ? WaveComposition.SwarmGroupSize
                    : 1;
        }
    }

    private static bool MatchesAir(
        SerializedProperty sequence,
        EnemyMovement aphid,
        EnemyMovement locust,
        EnemyMovement swarm,
        EnemyMovement moth)
    {
        if (sequence.arraySize != 10 || moth == null)
        {
            return false;
        }

        SerializedProperty first = sequence.GetArrayElementAtIndex(8);
        SerializedProperty second = sequence.GetArrayElementAtIndex(9);
        return sequence.arraySize == 10 &&
            MatchesPrefix(sequence, 8, aphid, locust, swarm) &&
            first.FindPropertyRelative("enemyPrefab").objectReferenceValue == moth &&
            second.FindPropertyRelative("enemyPrefab").objectReferenceValue == moth &&
            first.FindPropertyRelative("partySize").intValue == 1 &&
            second.FindPropertyRelative("partySize").intValue == 1;
    }

    private static bool Contains(SerializedProperty sequence, UnityEngine.Object prefab)
    {
        if (prefab == null)
        {
            return false;
        }

        for (int i = 0; i < sequence.arraySize; i++)
        {
            if (sequence.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("enemyPrefab").objectReferenceValue == prefab)
            {
                return true;
            }
        }

        return false;
    }

    private static void AssignClip(MatchAudio audio, StringBuilder report)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath);
        SerializedObject serialized = new(audio);
        SerializedProperty property = serialized.FindProperty("condorShotClip");

        if (property.objectReferenceValue == null && clip != null)
        {
            property.objectReferenceValue = clip;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(audio);
            return;
        }

        if (clip != null && property.objectReferenceValue != clip)
        {
            report.AppendLine("El audio del Vigía está personalizado y se conservó.");
        }
    }

    private static void FillMask(TowerData data, int desired, StringBuilder report, string label)
    {
        SerializedObject serialized = new(data);
        SerializedProperty property = serialized.FindProperty("allowedTargets");

        if (property.intValue == 0)
        {
            property.intValue = desired;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            report.AppendLine(label + " cubre las categorías iniciales.");
        }
    }

    private static void FillStoredMask(string path, int desired, StringBuilder report, string label)
    {
        TowerData data = AssetDatabase.LoadAssetAtPath<TowerData>(path);

        if (data == null)
        {
            return;
        }

        SerializedObject serialized = new(data);
        SerializedProperty property = serialized.FindProperty("allowedTargets");

        if (property.intValue == 0)
        {
            property.intValue = desired;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            report.AppendLine(label + " interpreta su cobertura anterior como la máscara inicial.");
            return;
        }

        if (property.intValue != desired)
        {
            report.AppendLine(label + " conserva una cobertura personalizada.");
        }
    }

    private static void FillSprite(
        TowerData data,
        Sprite sprite,
        bool realArt,
        StringBuilder report,
        string label)
    {
        if (sprite == null || data.WorldSprite != null &&
            !(realArt && AssetDatabase.GetAssetPath(data.WorldSprite).Contains("placeholder_")))
        {
            return;
        }

        SerializedObject serialized = new(data);
        serialized.FindProperty("worldSprite").objectReferenceValue = sprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        report.AppendLine("Se completó el sprite " + label + ".");
    }

    private static void FillEnemySprite(EnemyData data, Sprite sprite, bool realArt, StringBuilder report)
    {
        if (sprite == null || data.WorldSprite != null &&
            !(realArt && AssetDatabase.GetAssetPath(data.WorldSprite).Contains("placeholder_")))
        {
            return;
        }

        SerializedObject serialized = new(data);
        serialized.FindProperty("worldSprite").objectReferenceValue = sprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        report.AppendLine("Se completó el sprite de la polilla.");
    }

    private static void EnsureIndependent(string path, GameObject source, string name)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
        {
            return;
        }

        if (source == null)
        {
            throw new System.InvalidOperationException("No está el prefab de origen para " + name + ".");
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(
            instance,
            PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction
        );
        instance.name = name;
        PrefabUtility.SaveAsPrefabAsset(instance, path);
        UnityEngine.Object.DestroyImmediate(instance);
    }

    private static void AssignReference(
        Component target,
        string field,
        UnityEngine.Object desired,
        UnityEngine.Object seed,
        StringBuilder report,
        string customMessage)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(field);
        UnityEngine.Object current = property.objectReferenceValue;

        if (current == null || (seed != null && current == seed))
        {
            property.objectReferenceValue = desired;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return;
        }

        if (current != desired)
        {
            report.AppendLine(customMessage);
        }
    }

    private static Projectile SeedProjectile(GameObject root)
    {
        TowerAttack attack = root.GetComponent<TowerAttack>();

        if (attack == null)
        {
            return null;
        }

        SerializedObject serialized = new(attack);
        return serialized.FindProperty("projectilePrefab").objectReferenceValue as Projectile;
    }

    private static bool PointsAssigned(SerializedProperty points)
    {
        for (int i = 0; i < points.arraySize; i++)
        {
            if (points.GetArrayElementAtIndex(i).objectReferenceValue == null)
            {
                return false;
            }
        }

        return true;
    }

    private static Sprite EnsureMark(string path, Color color)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            float center = 15.5f;

            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dx = Mathf.Abs(x - center);
                    float dy = Mathf.Abs(y - center);
                    float alpha = dx + dy <= 12f ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
                }
            }

            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return LoadSprite(path);
    }

    private static void Append(SerializedProperty property, UnityEngine.Object value)
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
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
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

    private static void Note(StringBuilder report, string label, float current, float expected)
    {
        if (Mathf.Abs(current - expected) > 0.001f)
        {
            report.AppendLine(label + " está personalizado y se conservó.");
        }
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

    private static void Report(string message)
    {
        Debug.Log(message);
        EditorUtility.DisplayDialog("Cobertura aérea", message, "Cerrar");
    }
}
