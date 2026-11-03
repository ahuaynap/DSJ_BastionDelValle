using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SceneUiRepair
{
    private const string MenuPath =
        "Bastion del Valle/Consolidacion/29B - Reparar Escenas y UI";

    private const string UndoName = "Reparar Escenas y UI";
    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string Level01Path = "Assets/Scenes/Level_01.unity";
    private const string Level02Path = "Assets/Scenes/Level_02.unity";

    [MenuItem(MenuPath, false, 41)]
    private static void Repair()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
        {
            return;
        }

        var report = new StringBuilder();

        if (!Prevalidate(report))
        {
            Debug.Log(report.ToString());
            return;
        }

        int group = Undo.GetCurrentGroup();
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);

        try
        {
            RepairLoaded(report);
            bool save = EditorUtility.DisplayDialog(
                "Escenas reparadas",
                "Se corrigieron las escenas que ya estaban abiertas.\n" +
                "Guardarlas y dejar abierta solo MainMenu evita que Play " +
                "dibuje el menú y los dos mapas a la vez.\n" +
                "No se guardan escenas ajenas.",
                "Guardar y dejar el menú",
                "Revisar sin guardar");

            if (save)
            {
                SaveRepaired(report);
                OpenMissingAndSave(report);
                EnsureBuildList(report);

                if (CanCloseToMenu())
                {
                    EditorSceneManager.OpenScene(
                        MenuScenePath,
                        OpenSceneMode.Single);
                    report.AppendLine("Quedó abierta solo MainMenu.");
                }
            }
            else
            {
                report.AppendLine(
                    "No se guardó nada. Mientras las tres escenas sigan " +
                    "abiertas, Play las dibuja juntas.");
            }

            Undo.CollapseUndoOperations(group);
            Debug.Log(report.ToString());
        }
        catch (System.Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogError("La reparación se revirtió. " + exception.Message);
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool CanRepair()
    {
        return !EditorApplication.isPlaying && !EditorApplication.isCompiling;
    }

    private static bool Prevalidate(StringBuilder report)
    {
        bool ready = true;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) == null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(Level01Path) == null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(Level02Path) == null)
        {
            report.AppendLine("Falta MainMenu, Level_01 o Level_02.");
            ready = false;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.isDirty && !IsTarget(scene.path))
            {
                report.AppendLine(
                    "Hay cambios sin guardar fuera de las tres escenas: " +
                    scene.path + ". No se descartó nada.");
                ready = false;
            }
        }

        int loadedTargets = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (IsTarget(SceneManager.GetSceneAt(i).path))
            {
                loadedTargets++;
            }
        }

        report.AppendLine(
            "Escenas de trabajo abiertas a la vez: " + loadedTargets +
            ". La causa de la captura es esa carga simultánea.");
        return ready;
    }

    private static void RepairLoaded(StringBuilder report)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!IsTarget(scene.path))
            {
                continue;
            }

            RepairScene(scene, report);
        }
    }

    private static void RepairScene(Scene scene, StringBuilder report)
    {
        int removed = RemoveForeign(scene) + KeepOne(scene);
        bool hidden = HideModals(scene);
        bool framed = FramePlayfield(scene);
        bool covered = CoverFrame(scene, report);

        if (removed > 0 || hidden || framed || covered)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        report.AppendLine(
            scene.name + ": objetos retirados " + removed + ".");
    }

    private static int RemoveForeign(Scene scene)
    {
        int removed = 0;

        if (scene.path == MenuScenePath)
        {
            removed += DestroyAll<HUDController>(scene);
            removed += DestroyAll<BuildChoiceView>(scene);
            removed += DestroyAll<HabitatSession>(scene);
            removed += DestroyAll<WaveManager>(scene);
            removed += DestroyAll<BuildPlot>(scene);
            removed += DestroyAll<EnemySpawner>(scene);
            return removed;
        }

        removed += DestroyMenuCanvas(scene);

        if (scene.path == Level01Path)
        {
            removed += DestroyAll<BuildChoiceView>(scene);
            removed += DestroyAll<HabitatSession>(scene);
            removed += DestroyNamed(scene, "HabitatRow");
            removed += DestroyNamed(scene, "BuildChoiceRow");
            removed += DestroyNamed(scene, "BuildChoiceRowUpper");
        }

        return removed;
    }

    private static int DestroyMenuCanvas(Scene scene)
    {
        int removed = 0;
        List<MainMenuController> menus = Find<MainMenuController>(scene);

        for (int i = 0; i < menus.Count; i++)
        {
            if (menus[i] == null)
            {
                continue;
            }

            Canvas canvas = menus[i].GetComponentInParent<Canvas>();

            if (canvas != null &&
                canvas.GetComponentInChildren<HUDController>(true) == null)
            {
                Undo.DestroyObjectImmediate(canvas.gameObject);
            }
            else
            {
                Undo.DestroyObjectImmediate(menus[i].gameObject);
            }

            removed++;
        }

        List<Transform> transforms = Find<Transform>(scene);

        for (int i = 0; i < transforms.Count; i++)
        {
            Transform transform = transforms[i];

            if (transform == null || transform.name != "ConfirmNewGame")
            {
                continue;
            }

            if (transform.GetComponentInParent<MainMenuController>(true) != null)
            {
                continue;
            }

            Undo.DestroyObjectImmediate(transform.gameObject);
            removed++;
        }

        return removed;
    }

    private static int KeepOne(Scene scene)
    {
        int removed = 0;
        removed += KeepConnectedHud(scene);
        removed += KeepFirst<EventSystem>(scene);
        removed += KeepMainListener(scene);
        removed += KeepMainCamera(scene);
        removed += RemoveFitterConflict(scene);
        return removed;
    }

    private static int KeepConnectedHud(Scene scene)
    {
        List<HUDController> huds = Find<HUDController>(scene);

        if (huds.Count <= 1)
        {
            return 0;
        }

        HUDController keeper = huds[0];

        for (int i = 0; i < huds.Count; i++)
        {
            if (SameScene(huds[i].gameObject, scene) &&
                Connected(huds[i]))
            {
                keeper = huds[i];
                break;
            }
        }

        int removed = 0;

        for (int i = 0; i < huds.Count; i++)
        {
            if (huds[i] == null || huds[i] == keeper)
            {
                continue;
            }

            if (keeper.transform.IsChildOf(huds[i].transform))
            {
                Undo.DestroyObjectImmediate(huds[i]);
            }
            else
            {
                Undo.DestroyObjectImmediate(huds[i].gameObject);
            }

            removed++;
        }

        return removed;
    }

    private static bool Connected(HUDController hud)
    {
        SerializedObject serialized = new SerializedObject(hud);
        SerializedProperty game = serialized.FindProperty("gameManager");
        return game != null &&
            game.objectReferenceValue is Component component &&
            component.gameObject.scene == hud.gameObject.scene;
    }

    private static int KeepFirst<T>(Scene scene) where T : Component
    {
        List<T> found = Find<T>(scene);

        if (found.Count <= 1)
        {
            return 0;
        }

        int removed = 0;

        for (int i = 1; i < found.Count; i++)
        {
            if (found[i] == null)
            {
                continue;
            }

            Undo.DestroyObjectImmediate(found[i].gameObject);
            removed++;
        }

        return removed;
    }

    private static int KeepMainListener(Scene scene)
    {
        List<AudioListener> listeners = Find<AudioListener>(scene);
        AudioListener keeper = null;

        for (int i = 0; i < listeners.Count; i++)
        {
            if (listeners[i].CompareTag("MainCamera"))
            {
                keeper = listeners[i];
                break;
            }
        }

        if (keeper == null && listeners.Count > 0)
        {
            keeper = listeners[0];
        }

        int removed = 0;

        for (int i = 0; i < listeners.Count; i++)
        {
            if (listeners[i] == null || listeners[i] == keeper)
            {
                continue;
            }

            Undo.DestroyObjectImmediate(listeners[i]);
            removed++;
        }

        return removed;
    }

    private static int KeepMainCamera(Scene scene)
    {
        List<Camera> cameras = Find<Camera>(scene);
        Camera keeper = null;

        for (int i = 0; i < cameras.Count; i++)
        {
            if (cameras[i].CompareTag("MainCamera"))
            {
                keeper = cameras[i];
                break;
            }
        }

        if (keeper == null && cameras.Count > 0)
        {
            keeper = cameras[0];
        }

        int removed = 0;

        for (int i = 0; i < cameras.Count; i++)
        {
            if (cameras[i] == null || cameras[i] == keeper)
            {
                continue;
            }

            Undo.DestroyObjectImmediate(cameras[i].gameObject);
            removed++;
        }

        return removed;
    }

    private static int RemoveFitterConflict(Scene scene)
    {
        int removed = 0;
        List<ContentSizeFitter> fitters = Find<ContentSizeFitter>(scene);

        for (int i = 0; i < fitters.Count; i++)
        {
            if (fitters[i] == null)
            {
                continue;
            }

            if (fitters[i].GetComponent<HorizontalLayoutGroup>() == null &&
                fitters[i].GetComponent<VerticalLayoutGroup>() == null &&
                fitters[i].GetComponent<GridLayoutGroup>() == null)
            {
                continue;
            }

            Undo.DestroyObjectImmediate(fitters[i]);
            removed++;
        }

        return removed;
    }

    private static bool HideModals(Scene scene)
    {
        changed = false;
        Hide<MainMenuController>(scene, "confirmRoot");
        Hide<CodexController>(scene, "panelRoot");
        Hide<AudioSettingsPanel>(scene, "panelRoot");
        Hide<LevelPausePanel>(scene, "panelRoot");
        Hide<EducationalModalController>(scene, "modalRoot");
        changed |= HideResult(scene);
        return changed;
    }

    private static bool changed;

    private static void Hide<T>(Scene scene, string propertyName) where T : Component
    {
        List<T> hosts = Find<T>(scene);

        for (int i = 0; i < hosts.Count; i++)
        {
            SerializedObject serialized = new SerializedObject(hosts[i]);
            SerializedProperty property = serialized.FindProperty(propertyName);

            if (property == null ||
                property.objectReferenceValue is not GameObject panel ||
                !panel.activeSelf)
            {
                continue;
            }

            Undo.RecordObject(panel, UndoName);
            panel.SetActive(false);
            changed = true;
        }
    }

    private static bool HideResult(Scene scene)
    {
        bool hidden = false;
        List<EndGameUI> results = Find<EndGameUI>(scene);

        for (int i = 0; i < results.Count; i++)
        {
            SerializedObject serialized = new SerializedObject(results[i]);
            hidden |= Deactivate(serialized, "resultText");
            hidden |= Deactivate(serialized, "retryButton");
            hidden |= Deactivate(serialized, "menuButton");
        }

        return hidden;
    }

    private static bool Deactivate(
        SerializedObject serialized,
        string propertyName)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null ||
            property.objectReferenceValue is not Component widget ||
            !widget.gameObject.activeSelf)
        {
            return false;
        }

        Undo.RecordObject(widget.gameObject, UndoName);
        widget.gameObject.SetActive(false);
        return true;
    }

    private static bool FramePlayfield(Scene scene)
    {
        if (scene.path != Level02Path)
        {
            return false;
        }

        Camera camera = MainCamera(scene);

        if (camera == null || !TryContentBounds(scene, out float minX, out float maxX, out float minY, out float maxY))
        {
            return false;
        }

        FrameFit fit = PlayfieldFrame.Fit(
            minX,
            maxX,
            minY,
            maxY,
            HudReserve.TopFraction(true),
            HudReserve.BottomFraction(true),
            16f / 9f,
            1.2f);
        Vector3 position = camera.transform.position;
        bool same =
            camera.orthographic &&
            Mathf.Abs(camera.orthographicSize - fit.OrthographicSize) < 0.02f &&
            Mathf.Abs(position.x - fit.CenterX) < 0.02f &&
            Mathf.Abs(position.y - fit.CenterY) < 0.02f;

        if (same)
        {
            return false;
        }

        Undo.RecordObject(camera, UndoName);
        Undo.RecordObject(camera.transform, UndoName);
        camera.orthographic = true;
        camera.orthographicSize = fit.OrthographicSize;
        float z = position.z < 0f ? position.z : -10f;
        camera.transform.position = new Vector3(fit.CenterX, fit.CenterY, z);
        return true;
    }

    private static bool TryContentBounds(
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
        Include(Find<BuildPlot>(scene), ref minX, ref maxX, ref minY, ref maxY, ref count);
        Include(Find<BastionHealth>(scene), ref minX, ref maxX, ref minY, ref maxY, ref count);
        Include(Find<EnemySpawner>(scene), ref minX, ref maxX, ref minY, ref maxY, ref count);
        List<Renderer> renderers = Find<Renderer>(scene);

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
            Vector3 position = components[i].transform.position;
            minX = Mathf.Min(minX, position.x);
            maxX = Mathf.Max(maxX, position.x);
            minY = Mathf.Min(minY, position.y);
            maxY = Mathf.Max(maxY, position.y);
            count++;
        }
    }

    private static bool CoverFrame(Scene scene, StringBuilder report)
    {
        Camera camera = MainCamera(scene);

        if (camera == null || !camera.orthographic)
        {
            return false;
        }

        Renderer terrain = Background(scene);

        if (terrain == null)
        {
            report.AppendLine(scene.name + " no tiene terreno de fondo.");
            return false;
        }

        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * (16f / 9f);
        Vector3 center = camera.transform.position;
        Bounds bounds = terrain.bounds;
        bool covers =
            bounds.min.x <= center.x - halfWidth &&
            bounds.max.x >= center.x + halfWidth &&
            bounds.min.y <= center.y - halfHeight &&
            bounds.max.y >= center.y + halfHeight;

        if (covers)
        {
            return false;
        }

        float widthFactor = bounds.size.x <= 0.01f
            ? 1f
            : (halfWidth * 2f + 1f) / bounds.size.x;
        float heightFactor = bounds.size.y <= 0.01f
            ? 1f
            : (halfHeight * 2f + 1f) / bounds.size.y;
        float factor = Mathf.Max(1f, widthFactor, heightFactor);
        Undo.RecordObject(terrain.transform, UndoName);
        terrain.transform.localScale = new Vector3(
            terrain.transform.localScale.x * factor,
            terrain.transform.localScale.y * factor,
            terrain.transform.localScale.z);
        Bounds grown = terrain.bounds;
        Vector3 position = terrain.transform.position;
        terrain.transform.position = new Vector3(
            position.x + center.x - grown.center.x,
            position.y + center.y - grown.center.y,
            position.z);
        report.AppendLine(
            scene.name + ": el terreno cubre el encuadre sin estirar su proporción.");
        return true;
    }

    private static Renderer Background(Scene scene)
    {
        Renderer best = null;
        float bestArea = -1f;
        List<Renderer> renderers = Find<Renderer>(scene);

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            string name = renderer.gameObject.name;

            if (name != "Terreno" && name != "Ground")
            {
                continue;
            }

            float area = renderer.bounds.size.x * renderer.bounds.size.y;

            if (area > bestArea)
            {
                best = renderer;
                bestArea = area;
            }
        }

        return best != null ? best : null;
    }

    private static void SaveRepaired(StringBuilder report)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!IsTarget(scene.path) ||
                string.IsNullOrEmpty(scene.path) ||
                !scene.isDirty)
            {
                continue;
            }

            if (EditorSceneManager.SaveScene(scene))
            {
                report.AppendLine("Guardada " + scene.path + ".");
            }
            else
            {
                report.AppendLine("No se pudo guardar " + scene.path + ".");
            }
        }
    }

    private static void OpenMissingAndSave(StringBuilder report)
    {
        string[] paths = { MenuScenePath, Level01Path, Level02Path };

        for (int i = 0; i < paths.Length; i++)
        {
            Scene loaded = SceneManager.GetSceneByPath(paths[i]);

            if (loaded.IsValid() && loaded.isLoaded)
            {
                continue;
            }

            if (!CanCloseToMenu())
            {
                report.AppendLine(
                    "No se abrió " + paths[i] +
                    " porque otra escena tiene cambios sin guardar.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(
                paths[i],
                OpenSceneMode.Single);
            RepairScene(scene, report);

            if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
            {
                report.AppendLine("No se pudo guardar " + paths[i] + ".");
            }
        }
    }

    private static bool CanCloseToMenu()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.isDirty && scene.path != MenuScenePath)
            {
                return false;
            }
        }

        return AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) != null;
    }

    private static void EnsureBuildList(StringBuilder report)
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(Level01Path, true),
            new EditorBuildSettingsScene(Level02Path, true)
        };

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == MenuScenePath ||
                scene.path == Level01Path ||
                scene.path == Level02Path)
            {
                continue;
            }

            bool enabled = scene.path != "Assets/Scenes/SampleScene.unity" &&
                scene.enabled;
            scenes.Add(new EditorBuildSettingsScene(scene.path, enabled));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        report.AppendLine("La entrada del build es MainMenu.");
    }

    private static Camera MainCamera(Scene scene)
    {
        List<Camera> cameras = Find<Camera>(scene);

        for (int i = 0; i < cameras.Count; i++)
        {
            if (cameras[i].CompareTag("MainCamera"))
            {
                return cameras[i];
            }
        }

        return cameras.Count == 0 ? null : cameras[0];
    }

    private static int DestroyNamed(Scene scene, string objectName)
    {
        int removed = 0;
        List<Transform> transforms = Find<Transform>(scene);

        for (int i = 0; i < transforms.Count; i++)
        {
            Transform transform = transforms[i];

            if (transform == null || transform.name != objectName)
            {
                continue;
            }

            Undo.DestroyObjectImmediate(transform.gameObject);
            removed++;
        }

        return removed;
    }

    private static int DestroyAll<T>(Scene scene) where T : Component
    {
        List<T> found = Find<T>(scene);
        int removed = 0;

        for (int i = 0; i < found.Count; i++)
        {
            if (found[i] == null)
            {
                continue;
            }

            Undo.DestroyObjectImmediate(found[i].gameObject);
            removed++;
        }

        return removed;
    }

    private static bool SameScene(GameObject gameObject, Scene scene)
    {
        return gameObject != null && gameObject.scene == scene;
    }

    private static bool IsTarget(string path)
    {
        return path == MenuScenePath ||
            path == Level01Path ||
            path == Level02Path;
    }

    private static List<T> Find<T>(Scene scene) where T : Component
    {
        var list = new List<T>();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            return list;
        }

        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            list.AddRange(roots[i].GetComponentsInChildren<T>(true));
        }

        return list;
    }
}
