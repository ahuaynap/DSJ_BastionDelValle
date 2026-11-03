using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfluenceSwarmConfigurator
{
    private const string MenuPath =
        "Bastion del Valle/Mapas/24 - Integrar Refugio y Enjambre";

    private const string UndoName = "Integrar Refugio y Enjambre";
    private const string Level02Path = "Assets/Scenes/Level_02.unity";
    private const string TowerDataPath = "Assets/Data/TowerData_Pollinator.asset";
    private const string SwarmDataPath = "Assets/Data/EnemyData_Swarm.asset";
    private const string AphidDataPath = "Assets/Data/EnemyData_Basic.asset";
    private const string TowerPath = "Assets/Prefabs/Tower_Pollinator.prefab";
    private const string SwarmPath = "Assets/Prefabs/Enemy_Swarm.prefab";
    private const string ShotPath = "Assets/Prefabs/Projectile_Pollinator.prefab";
    private const string SeedTowerPath = "Assets/Prefabs/Tower_Basic.prefab";
    private const string AphidPath = "Assets/Prefabs/Enemy_Basic.prefab";
    private const string SeedShotPath = "Assets/Prefabs/Projectile_Basic.prefab";
    private const string TowerSpritePath =
        "Assets/Art/Towers/tower_pollinator_idle_v001.png";
    private const string SwarmSpritePath =
        "Assets/Art/Enemies/enemy_swarm_unit_idle_v001.png";
    private const string ShotSpritePath =
        "Assets/Art/Projectiles/projectile_pollinator_v001.png";
    private const string ShotAudio = "Assets/Audio/SFX/sfx_pollinator_shot.wav";
    private const string HitAudio = "Assets/Audio/SFX/sfx_pollinator_hit.wav";
    private const string TowerPlaceholder =
        "Assets/Art/Generated/Pollinator/placeholder_tower.png";
    private const string SwarmPlaceholder =
        "Assets/Art/Generated/Pollinator/placeholder_swarm.png";

    private const string TowerId = "tower_pollinator";
    private const string SwarmId = "enemy_swarm_unit";
    private static readonly Color Gold = new(0.95f, 0.78f, 0.32f, 1f);
    private static readonly Color Cream = new(1f, 0.945f, 0.776f, 1f);

    [MenuItem(MenuPath, false, 24)]
    private static void Integrate()
    {
        if (EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling)
        {
            Report("La integración solo puede ejecutarse fuera de Play Mode.");
            return;
        }

        if (SceneManager.GetActiveScene().path != Level02Path)
        {
            Report("Abre Level_02 y vuelve a ejecutar la acción. No se modificó ninguna escena.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (Count<WaveManager>(scene) != 1 ||
            Count<MatchAudio>(scene) != 1 ||
            Count<ChannelNavigator>(scene) != 1 ||
            FindNamed(scene, "HUD") == null)
        {
            Report(
                "Level_02 necesita un WaveManager, un MatchAudio, " +
                "un ChannelNavigator y el HUD. No se modificó la escena."
            );
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        var report = new StringBuilder();

        try
        {
            EnemyData aphid = AssetDatabase.LoadAssetAtPath<EnemyData>(AphidDataPath);
            float swarmSpeed = aphid == null
                ? 1.8f
                : WaveComposition.SwarmSpeed(aphid.MovementSpeed);
            bool towerArt = AssetDatabase.LoadMainAssetAtPath(TowerSpritePath) != null;
            bool swarmArt = AssetDatabase.LoadMainAssetAtPath(SwarmSpritePath) != null;
            Sprite towerSprite = towerArt
                ? LoadSprite(TowerSpritePath)
                : EnsurePlaceholder(TowerPlaceholder, new Color(0.85f, 0.62f, 0.18f, 1f));
            Sprite swarmSprite = swarmArt
                ? LoadSprite(SwarmSpritePath)
                : EnsurePlaceholder(SwarmPlaceholder, new Color(0.55f, 0.28f, 0.62f, 1f));

            if (!towerArt)
            {
                report.AppendLine(
                    "Falta la imagen del refugio. Se usó una marca dorada temporal."
                );
            }

            if (!swarmArt)
            {
                report.AppendLine(
                    "Falta la imagen del enjambre. Se usó una marca temporal."
                );
            }

            if (AssetDatabase.LoadMainAssetAtPath(ShotAudio) == null ||
                AssetDatabase.LoadMainAssetAtPath(HitAudio) == null)
            {
                report.AppendLine(
                    "Falta un audio del refugio. El combate sigue sin ese sonido."
                );
            }

            TowerData towerData = EnsureTowerData(towerSprite, towerArt, report);
            EnemyData swarmData = EnsureSwarmData(swarmSprite, swarmArt, swarmSpeed, report);
            Sprite shotSprite = EnsureShotSprite();
            Projectile shot = EnsureShot(shotSprite, report);
            TowerController tower = EnsureTower(towerData, shot, report);
            EnemyMovement swarm = EnsureSwarm(swarmData, report);
            WireScene(scene, tower, swarm, report);
            CompactMatchHud.Apply(scene, report);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssetIfDirty(towerData);
            AssetDatabase.SaveAssetIfDirty(swarmData);
            AssetDatabase.SaveAssetIfDirty(shot);
            AssetDatabase.SaveAssetIfDirty(tower);
            AssetDatabase.SaveAssetIfDirty(swarm);
            report.AppendLine(
                "Level_02 quedó marcada como modificada. Guárdala desde el editor."
            );
            Report(report.ToString());
        }
        catch (System.Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(exception);
            Report("No se integró el refugio ni el enjambre.\n" + exception.Message);
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenu()
    {
        return !EditorApplication.isPlaying &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling;
    }

    private static TowerData EnsureTowerData(
        Sprite sprite,
        bool realArt,
        StringBuilder report)
    {
        TowerData data = AssetDatabase.LoadAssetAtPath<TowerData>(TowerDataPath);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<TowerData>();
            AssetDatabase.CreateAsset(data, TowerDataPath);
            WriteTower(data, sprite);
            report.AppendLine("Se creó TowerData_Pollinator.");
            return data;
        }

        Note(report, "Refugio coste", data.BuildCost, 60);
        Note(report, "Refugio alcance", data.AttackRange, 2.5f);
        Note(report, "Refugio intervalo", data.AttackInterval, 1.8f);
        Note(report, "Refugio daño", data.ProjectileDamage, 8);
        Note(report, "Refugio radio", data.ImpactRadius, 0.65f);
        Note(report, "Refugio proyectil", data.ProjectileSpeed, 6f);
        FillSprite(data, "worldSprite", data.WorldSprite, sprite, realArt, report, "refugio");

        if (!data.AppliesArea &&
            data.Strike == ImpactMode.Direct &&
            data.ImpactRadius <= 0f)
        {
            SerializedObject serialized = new(data);
            serialized.FindProperty("impactMode").enumValueIndex = (int)ImpactMode.Area;
            serialized.FindProperty("impactRadius").floatValue = 0.65f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            report.AppendLine("Se completó el daño en área del refugio.");
        }

        report.AppendLine("Se reutilizó TowerData_Pollinator.");
        return data;
    }

    private static void WriteTower(TowerData data, Sprite sprite)
    {
        SerializedObject serialized = new(data);
        serialized.FindProperty("towerId").stringValue = TowerId;
        serialized.FindProperty("displayName").stringValue = "Refugio de Polinizadores";
        serialized.FindProperty("description").stringValue =
            "Daña en área a los enemigos que quedan dentro del impacto.";
        serialized.FindProperty("buildCost").intValue = 60;
        serialized.FindProperty("attackRange").floatValue = 2.5f;
        serialized.FindProperty("attackInterval").floatValue = 1.8f;
        serialized.FindProperty("projectileDamage").intValue = 8;
        serialized.FindProperty("projectileSpeed").floatValue = 6f;
        serialized.FindProperty("slowMultiplier").floatValue = 1f;
        serialized.FindProperty("slowDuration").floatValue = 0f;
        serialized.FindProperty("impactMode").enumValueIndex = (int)ImpactMode.Area;
        serialized.FindProperty("impactRadius").floatValue = 0.65f;
        serialized.FindProperty("worldSprite").objectReferenceValue = sprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }

    private static EnemyData EnsureSwarmData(
        Sprite sprite,
        bool realArt,
        float speed,
        StringBuilder report)
    {
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(SwarmDataPath);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<EnemyData>();
            AssetDatabase.CreateAsset(data, SwarmDataPath);
            SerializedObject serialized = new(data);
            serialized.FindProperty("enemyId").stringValue = SwarmId;
            serialized.FindProperty("displayName").stringValue = "Criatura de la Marchitez";
            serialized.FindProperty("description").stringValue =
                "Individuo del enjambre. Cuenta, cobra y daña por separado.";
            serialized.FindProperty("maxHealth").intValue = 12;
            serialized.FindProperty("movementSpeed").floatValue = speed;
            serialized.FindProperty("reward").intValue = 2;
            serialized.FindProperty("goalDamage").intValue = 2;
            serialized.FindProperty("bodyRadius").floatValue = 0.12f;
            serialized.FindProperty("worldSprite").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            report.AppendLine(
                "Se creó EnemyData_Swarm con velocidad " + speed.ToString("0.00") + "."
            );
            return data;
        }

        Note(report, "Enjambre vida", data.MaxHealth, 12);
        Note(report, "Enjambre velocidad", data.MovementSpeed, speed);
        Note(report, "Enjambre recompensa", data.Reward, 2);
        Note(report, "Enjambre daño al corazón", data.GoalDamage, 2);
        Note(report, "Enjambre radio", data.BodyRadius, 0.12f);
        FillSprite(data, "worldSprite", data.WorldSprite, sprite, realArt, report, "enjambre");

        if (data.BodyRadius <= 0f)
        {
            SerializedObject serialized = new(data);
            serialized.FindProperty("bodyRadius").floatValue = 0.12f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            report.AppendLine("Se completó el radio lógico del enjambre.");
        }

        report.AppendLine("Se reutilizó EnemyData_Swarm.");
        return data;
    }

    private static Projectile EnsureShot(Sprite sprite, StringBuilder report)
    {
        Projectile source = AssetDatabase.LoadAssetAtPath<Projectile>(SeedShotPath);
        EnsurePrefab(ShotPath, source, "Projectile_Pollinator");
        Sprite seedSprite = Visual(source.gameObject).sprite;
        GameObject contents = PrefabUtility.LoadPrefabContents(ShotPath);

        try
        {
            contents.name = "Projectile_Pollinator";
            SpriteRenderer renderer = Visual(contents);

            if (renderer != null &&
                (renderer.sprite == null || renderer.sprite == seedSprite))
            {
                renderer.sprite = sprite;
                renderer.color = Gold;
            }
            else if (renderer != null && renderer.sprite != sprite)
            {
                report.AppendLine("El sprite del proyectil del refugio está personalizado y se conservó.");
            }

            PrefabUtility.SaveAsPrefabAsset(contents, ShotPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return AssetDatabase.LoadAssetAtPath<Projectile>(ShotPath);
    }

    private static TowerController EnsureTower(
        TowerData data,
        Projectile shot,
        StringBuilder report)
    {
        TowerController source = AssetDatabase.LoadAssetAtPath<TowerController>(SeedTowerPath);
        EnsurePrefab(TowerPath, source, "Tower_Pollinator");
        Sprite seedSprite = Visual(source.gameObject).sprite;
        GameObject contents = PrefabUtility.LoadPrefabContents(TowerPath);

        try
        {
            contents.name = "Tower_Pollinator";
            AssignIfEmpty(
                contents.GetComponent<TowerController>(),
                "towerData",
                data,
                source.Data,
                report,
                "datos del refugio"
            );
            AssignIfEmpty(
                contents.GetComponent<TowerAttack>(),
                "projectilePrefab",
                shot,
                PrefabShot(source),
                report,
                "proyectil del refugio"
            );
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

    private static EnemyMovement EnsureSwarm(EnemyData data, StringBuilder report)
    {
        EnemyMovement source = AssetDatabase.LoadAssetAtPath<EnemyMovement>(AphidPath);
        EnsurePrefab(SwarmPath, source, "Enemy_Swarm");
        Sprite seedSprite = Visual(source.gameObject).sprite;
        GameObject contents = PrefabUtility.LoadPrefabContents(SwarmPath);

        try
        {
            contents.name = "Enemy_Swarm";
            AssignIfEmpty(
                contents.GetComponent<EnemyMovement>(),
                "enemyData",
                data,
                source.Data,
                report,
                "datos del enjambre"
            );
            SpriteRenderer renderer = Visual(contents);

            if (renderer != null && data.WorldSprite != null &&
                (renderer.sprite == null || renderer.sprite == seedSprite))
            {
                renderer.sprite = data.WorldSprite;
            }

            if (renderer != null &&
                Mathf.Approximately(renderer.transform.localScale.x, 1f) &&
                Mathf.Approximately(renderer.transform.localScale.y, 1f))
            {
                renderer.transform.localScale = new Vector3(0.68f, 0.68f, 1f);
            }

            if (contents.GetComponent<EnemySlow>() == null)
            {
                contents.AddComponent<EnemySlow>();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, SwarmPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return AssetDatabase.LoadAssetAtPath<EnemyMovement>(SwarmPath);
    }

    private static void WireScene(
        Scene scene,
        TowerController tower,
        EnemyMovement swarm,
        StringBuilder report)
    {
        BuildSelection selection = EnsureSelection(scene, tower, report);
        EnsureChoice(scene, selection, tower);
        AssignAudio(scene, report);
        AssignWaves(scene, swarm, report);
    }

    private static BuildSelection EnsureSelection(
        Scene scene,
        TowerController tower,
        StringBuilder report)
    {
        var found = FindAll<BuildSelection>(scene);

        if (found.Count != 1)
        {
            throw new System.InvalidOperationException(
                "Level_02 necesita una sola selección de construcción."
            );
        }

        SerializedObject serialized = new(found[0]);
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
            report.AppendLine("El refugio entró en la selección, sin cambiar la defensa inicial.");
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(found[0]);
        return found[0];
    }

    private static int DefenseIndex(BuildSelection selection, TowerController tower)
    {
        SerializedObject serialized = new(selection);
        SerializedProperty defenses = serialized.FindProperty("defenses");

        for (int i = 0; i < defenses.arraySize; i++)
        {
            if (defenses.GetArrayElementAtIndex(i).objectReferenceValue == tower)
            {
                return i;
            }
        }

        return 0;
    }

    private static void EnsureChoice(
        Scene scene,
        BuildSelection selection,
        TowerController tower)
    {
        Transform row = FindNamed(scene, "BuildChoiceRow");

        if (row == null)
        {
            throw new System.InvalidOperationException(
                "Level_02 no tiene la fila de selección de construcción."
            );
        }

        Button button = EnsureButton(row, selection, DefenseIndex(selection, tower));
        BuildChoiceView view = row.GetComponent<BuildChoiceView>();

        if (view == null)
        {
            view = Undo.AddComponent<BuildChoiceView>(row.gameObject);
        }

        SerializedObject serialized = new(view);
        serialized.FindProperty("selection").objectReferenceValue = selection;
        AppendReference(serialized.FindProperty("buttons"), button);
        AppendReference(
            serialized.FindProperty("labels"),
            button.GetComponentInChildren<TMP_Text>(true)
        );
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(view);
    }

    private static Button EnsureButton(
        Transform row,
        BuildSelection selection,
        int defenseIndex)
    {
        Transform existing = row.Find("PollinatorChoice");
        GameObject root;

        if (existing != null)
        {
            root = existing.gameObject;
        }
        else
        {
            root = new GameObject(
                "PollinatorChoice",
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

    private static void AssignAudio(Scene scene, StringBuilder report)
    {
        MatchAudio audio = FindAll<MatchAudio>(scene)[0];
        AssignClip(audio, "pollinatorShotClip", ShotAudio, "disparo del refugio", report);
        AssignClip(audio, "pollinatorHitClip", HitAudio, "impacto del refugio", report);
    }

    private static void AssignWaves(
        Scene scene,
        EnemyMovement swarm,
        StringBuilder report)
    {
        WaveManager waves = FindAll<WaveManager>(scene)[0];
        EnemyMovement aphid = AssetDatabase.LoadAssetAtPath<EnemyMovement>(AphidPath);
        EnemyMovement locust = AssetDatabase.LoadAssetAtPath<EnemyMovement>(
            "Assets/Prefabs/Enemy_Locust.prefab"
        );
        SerializedObject serialized = new(waves);
        SerializedProperty list = serialized.FindProperty("waves");

        if (list.arraySize >= 4)
        {
            report.AppendLine(
                "Level_02 ya tiene las cuatro oleadas. No se restauró la oleada del enjambre."
            );
            return;
        }

        if (list.arraySize < 2 || aphid == null || locust == null || swarm == null)
        {
            report.AppendLine("No se reescribió la composición: faltan oleadas o prefabs.");
            return;
        }

        KeepOrWriteFirst(list.GetArrayElementAtIndex(0), aphid, locust, report);
        WriteSecondIfRecognized(
            list.GetArrayElementAtIndex(1),
            aphid,
            locust,
            swarm,
            report
        );
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(waves);
    }

    private static void KeepOrWriteFirst(
        SerializedProperty wave,
        EnemyMovement aphid,
        EnemyMovement locust,
        StringBuilder report)
    {
        SerializedProperty sequence = wave.FindPropertyRelative("sequence");

        if (Matches(sequence, WaveComposition.FirstWave(), aphid, locust, null))
        {
            report.AppendLine("La oleada 1 ya tiene 4 pulgones y 2 langostas.");
            return;
        }

        bool untouched =
            sequence.arraySize == 0 &&
            wave.FindPropertyRelative("enemyCount").intValue == 6 &&
            Mathf.Approximately(
                wave.FindPropertyRelative("spawnInterval").floatValue,
                1.8f
            );

        if (!untouched)
        {
            report.AppendLine("La oleada 1 está personalizada y se conservó.");
            return;
        }

        WriteEntries(wave, WaveComposition.FirstWave(), 6, 1.8f, aphid, locust, null);
        report.AppendLine("La oleada 1 quedó en 6 individuos.");
    }

    private static void WriteSecondIfRecognized(
        SerializedProperty wave,
        EnemyMovement aphid,
        EnemyMovement locust,
        EnemyMovement swarm,
        StringBuilder report)
    {
        SerializedProperty sequence = wave.FindPropertyRelative("sequence");
        int[] legacy =
        {
            WaveComposition.Aphid, WaveComposition.Aphid,
            WaveComposition.Aphid, WaveComposition.Aphid,
            WaveComposition.Locust, WaveComposition.Locust,
            WaveComposition.Locust, WaveComposition.Locust
        };

        int[] refuge =
        {
            WaveComposition.Aphid, WaveComposition.Aphid, WaveComposition.Aphid,
            WaveComposition.Locust, WaveComposition.Locust, WaveComposition.Locust,
            WaveComposition.Swarm, WaveComposition.Swarm
        };

        if (Matches(sequence, refuge, aphid, locust, swarm))
        {
            FillEmptySwarm(sequence, swarm);
            report.AppendLine("La oleada 2 ya expande a 16 individuos.");
            return;
        }

        bool known =
            Matches(sequence, legacy, aphid, locust, null) &&
            Mathf.Approximately(
                wave.FindPropertyRelative("spawnInterval").floatValue,
                1.5f
            );

        if (!known)
        {
            report.AppendLine("La oleada 2 está personalizada y se conservó.");
            return;
        }

        WriteEntries(
            wave,
            refuge,
            16,
            1.5f,
            aphid,
            locust,
            swarm
        );
        report.AppendLine("La oleada 2 quedó en 16 individuos, con dos grupos de 5.");
    }

    private static void WriteEntries(
        SerializedProperty wave,
        int[] kinds,
        int individuals,
        float interval,
        EnemyMovement aphid,
        EnemyMovement locust,
        EnemyMovement swarm)
    {
        SerializedProperty sequence = wave.FindPropertyRelative("sequence");
        wave.FindPropertyRelative("enemyCount").intValue = individuals;
        wave.FindPropertyRelative("spawnInterval").floatValue = interval;
        wave.FindPropertyRelative("partyInterval").floatValue = WaveComposition.MemberInterval;
        sequence.arraySize = kinds.Length;

        for (int i = 0; i < kinds.Length; i++)
        {
            SerializedProperty entry = sequence.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("enemyPrefab").objectReferenceValue =
                PrefabFor(kinds[i], aphid, locust, swarm);
            entry.FindPropertyRelative("partySize").intValue =
                kinds[i] == WaveComposition.Swarm
                    ? WaveComposition.SwarmGroupSize
                    : 1;
        }
    }

    private static bool Matches(
        SerializedProperty sequence,
        int[] kinds,
        EnemyMovement aphid,
        EnemyMovement locust,
        EnemyMovement swarm)
    {
        if (sequence.arraySize != kinds.Length)
        {
            return false;
        }

        for (int i = 0; i < kinds.Length; i++)
        {
            SerializedProperty entry = sequence.GetArrayElementAtIndex(i);
            UnityEngine.Object prefab =
                entry.FindPropertyRelative("enemyPrefab").objectReferenceValue;
            UnityEngine.Object expected = PrefabFor(kinds[i], aphid, locust, swarm);
            int party = entry.FindPropertyRelative("partySize").intValue;
            int expectedParty = kinds[i] == WaveComposition.Swarm
                ? WaveComposition.SwarmGroupSize
                : 1;

            if (party < 1)
            {
                party = 1;
            }

            if (prefab != expected || party != expectedParty)
            {
                return false;
            }
        }

        return true;
    }

    private static void FillEmptySwarm(SerializedProperty sequence, EnemyMovement swarm)
    {
        for (int i = 0; i < sequence.arraySize; i++)
        {
            SerializedProperty prefab = sequence
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("enemyPrefab");

            int party = sequence
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("partySize")
                .intValue;

            if (prefab.objectReferenceValue == null && party > 1)
            {
                prefab.objectReferenceValue = swarm;
            }
        }
    }

    private static EnemyMovement PrefabFor(
        int kind,
        EnemyMovement aphid,
        EnemyMovement locust,
        EnemyMovement swarm)
    {
        if (kind == WaveComposition.Locust)
        {
            return locust;
        }

        if (kind == WaveComposition.Swarm)
        {
            return swarm;
        }

        return aphid;
    }

    private static void AssignIfEmpty(
        Component target,
        string fieldName,
        UnityEngine.Object desired,
        UnityEngine.Object seed,
        StringBuilder report,
        string label)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(fieldName);
        UnityEngine.Object current = property.objectReferenceValue;

        if (current == null || current == seed)
        {
            property.objectReferenceValue = desired;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            return;
        }

        if (current != desired)
        {
            report.AppendLine("Los " + label + " están personalizados y se conservaron.");
        }
    }

    private static void FillSprite(
        UnityEngine.Object data,
        string fieldName,
        Sprite current,
        Sprite sprite,
        bool realArt,
        StringBuilder report,
        string label)
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
            serialized.FindProperty(fieldName).objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            report.AppendLine("Se completó el sprite de " + label + ".");
        }
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

        if (property.objectReferenceValue == null && clip != null)
        {
            property.objectReferenceValue = clip;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(audio);
            return;
        }

        if (clip != null && property.objectReferenceValue != clip)
        {
            report.AppendLine("El audio de " + label + " está personalizado y se conservó.");
        }
    }

    private static void AppendReference(SerializedProperty property, UnityEngine.Object value)
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

    private static T EnsurePrefab<T>(string path, T source, string objectName)
        where T : Component
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        if (source == null)
        {
            throw new System.InvalidOperationException(
                "No está el prefab de origen para " + objectName + "."
            );
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

    private static Sprite EnsureShotSprite()
    {
        if (AssetDatabase.LoadMainAssetAtPath(ShotSpritePath) == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ShotSpritePath));
            File.WriteAllBytes(ShotSpritePath, CirclePng(32, Gold));
            AssetDatabase.ImportAsset(ShotSpritePath);
        }

        TextureImporter importer = AssetImporter.GetAtPath(ShotSpritePath) as TextureImporter;

        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = 32f;
            importer.SaveAndReimport();
        }

        return LoadSprite(ShotSpritePath);
    }

    private static Sprite EnsurePlaceholder(string path, Color color)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, CirclePng(32, color));
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

    private static byte[] CirclePng(int size, Color color)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = (size - 1) * 0.5f;
        float radius = size * 0.4f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float alpha = Mathf.Sqrt(dx * dx + dy * dy) <= radius ? color.a : 0f;
                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
            }
        }

        texture.Apply();
        byte[] bytes = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);
        return bytes;
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

    private static Projectile PrefabShot(TowerController tower)
    {
        TowerAttack attack = tower.GetComponent<TowerAttack>();
        SerializedObject serialized = new(attack);
        return serialized.FindProperty("projectilePrefab").objectReferenceValue as Projectile;
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
