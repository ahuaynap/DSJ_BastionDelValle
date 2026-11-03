using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SceneBaseConfigurator
{
    private const string MenuPath =
        "Bastion del Valle/Incrementos/17 - Integrar Escenario Base";

    private const string ScenePath = "Assets/Scenes/Level_01.unity";
    private const string UndoName = "Integrar Escenario Base";
    private const string GeneratedFolder = "Assets/Art/Generated/Level01";
    private const string TerrainTexturePath =
        GeneratedFolder + "/level01_terrain.png";
    private const string WhiteTexturePath =
        GeneratedFolder + "/level01_white.png";
    private const string TerrainMaterialPath =
        GeneratedFolder + "/Level01Terrain.mat";
    private const string PathFillMaterialPath =
        GeneratedFolder + "/Level01PathFill.mat";
    private const string PathBorderMaterialPath =
        GeneratedFolder + "/Level01PathBorder.mat";
    private const string TerrainMeshPath =
        GeneratedFolder + "/Level01Terrain.asset";
    private const string PathFillMeshPath =
        GeneratedFolder + "/Level01PathFill.asset";
    private const string PathBorderMeshPath =
        GeneratedFolder + "/Level01PathBorder.asset";
    private const string LayoutJsonPath =
        "Documentation/Level01Layout/level01_layout.json";
    private const string MeshShaderName =
        "Universal Render Pipeline/2D/Mesh2D-Lit-Default";

    private const float ReferenceAspect = 16f / 9f;
    private const float DefaultPathWidth = 0.45f;
    private const float PathEdge = 0.08f;
    private const float ContentMargin = 0.4f;
    private const float TerrainMargin = 1.5f;
    private const float HudStripHeight = 136f;
    private const float MinimumPathWidth = 0.05f;
    private const int TextureSize = 256;

    private static readonly Color TerrainTint = Color.white;
    private static readonly Color PathFillTint =
        new(0.769f, 0.588f, 0.384f, 1f);
    private static readonly Color PathBorderTint =
        new(0.643f, 0.471f, 0.298f, 1f);
    private static readonly Color HudBandColor =
        new(0.149f, 0.239f, 0.188f, 0.92f);
    private static readonly Color HudTextColor =
        new(1f, 0.945f, 0.776f, 1f);

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenu()
    {
        return !IsPlayMode() && !EditorApplication.isCompiling;
    }

    [MenuItem(MenuPath)]
    private static void Integrate()
    {
        if (IsPlayMode())
        {
            Report(
                "Detenido",
                "Sal de Play Mode antes de integrar el escenario."
            );
            return;
        }

        if (!TryInspect(out Inspection inspection, out string error))
        {
            Report("Detenido", error);
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        bool changed = false;

        try
        {
            if (!TryEnsureAssets(inspection.Shader, out error))
            {
                Undo.RevertAllDownToGroup(group);
                Report("Detenido", error);
                return;
            }

            changed = Apply(inspection);
        }
        catch (System.Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Report(
                "Detenido",
                "La integración se revirtió.\n" + exception.Message
            );
            return;
        }

        Undo.CollapseUndoOperations(group);

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(inspection.Scene);
        }

        string message = BuildReport(inspection, changed);
        Report(
            changed ? "Escenario base integrado" : "Escenario base al día",
            message
        );
    }

    private static bool IsPlayMode()
    {
        return EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static bool TryInspect(
        out Inspection inspection,
        out string error)
    {
        inspection = null;
        error = null;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            error =
                "Abre " + ScenePath + " antes de integrar el escenario. " +
                "No se cambió de escena.";
            return false;
        }

        if (!TryResolveCamera(scene, out Camera camera, out string cameraNote, out error))
        {
            return false;
        }

        if (!TryResolveRoute(scene, out Route route, out error))
        {
            return false;
        }

        if (!TryResolveHeart(scene, out HeartInfo heart, out error))
        {
            return false;
        }

        if (!TryResolvePlots(scene, out List<PlotInfo> plots, out error))
        {
            return false;
        }

        if (!TryResolveHud(scene, out HudInfo hud, out error))
        {
            return false;
        }

        if (!TryResolveActors(
                route,
                heart,
                plots,
                out ActorOrder actors,
                out error))
        {
            return false;
        }

        Shader shader = Shader.Find(MeshShaderName);
        if (shader == null)
        {
            error =
                "No está el shader " + MeshShaderName +
                ". No se modificó la escena.";
            return false;
        }

        LevelVisualSettings settings = FindSettings(scene, out error);
        if (error != null)
        {
            return false;
        }

        float pathWidth = settings != null
            ? settings.PathWidth
            : DefaultPathWidth;

        if (float.IsNaN(pathWidth) ||
            float.IsInfinity(pathWidth) ||
            pathWidth < MinimumPathWidth)
        {
            error =
                "El ancho del camino debe ser al menos " +
                MinimumPathWidth.ToString("0.00", CultureInfo.InvariantCulture) +
                " unidades. Está en LevelVisuals. No se modificó la escena.";
            return false;
        }

        if (!TryBuildStroke(
                route.Points,
                pathWidth * 0.5f + PathEdge,
                out List<Triangle> border,
                out error))
        {
            return false;
        }

        if (!TryBuildStroke(
                route.Points,
                pathWidth * 0.5f,
                out List<Triangle> fill,
                out error))
        {
            return false;
        }

        Bounds2 content = MeasureContent(route, heart, plots, border);
        content.Expand(ContentMargin);

        if (!content.Valid || content.Width <= 0.001f || content.Height <= 0.001f)
        {
            error =
                "No se pudo medir el contenido del nivel. " +
                "No se modificó la escena.";
            return false;
        }

        if (!TryHudFraction(hud.Scaler, out float hudFraction, out string fractionNote, out error))
        {
            return false;
        }

        Frame frame = FitFrame(content, hudFraction);
        if (!FrameFits(frame, content, ReferenceAspect) ||
            !FrameFits(frame, content, 1280f / 720f))
        {
            error =
                "El encuadre calculado no contiene el contenido a 16:9. " +
                "No se modificó la escena.";
            return false;
        }

        string bakeNote = null;
        if (settings != null &&
            !CanBake(settings.transform, out bakeNote))
        {
            bakeNote =
                bakeNote +
                " Conserva ese ajuste y no se reconstruyó el camino.";
        }

        inspection = new Inspection
        {
            Scene = scene,
            Camera = camera,
            CameraNote = cameraNote,
            Route = route,
            Heart = heart,
            Plots = plots,
            Hud = hud,
            Actors = actors,
            Settings = settings,
            Shader = shader,
            PathWidth = pathWidth,
            Border = border,
            Fill = fill,
            Frame = frame,
            FractionNote = fractionNote,
            BakeNote = bakeNote,
            ExportNote = CompareExport(scene, camera, route, heart, plots)
        };
        return true;
    }

    private static bool TryResolveCamera(
        Scene scene,
        out Camera camera,
        out string note,
        out string error)
    {
        camera = null;
        note = null;
        error = null;
        List<Camera> bases = new();
        List<string> overlays = new();
        List<string> disabled = new();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera candidate in root.GetComponentsInChildren<Camera>(true))
            {
                if (!candidate.enabled || !candidate.gameObject.activeInHierarchy)
                {
                    disabled.Add(candidate.gameObject.name);
                    continue;
                }

                UniversalAdditionalCameraData data =
                    candidate.GetComponent<UniversalAdditionalCameraData>();

                if (data != null && data.renderType == CameraRenderType.Overlay)
                {
                    overlays.Add(candidate.gameObject.name);
                    continue;
                }

                bases.Add(candidate);
            }
        }

        if (bases.Count != 1)
        {
            error = DescribeCameras(bases, overlays, disabled);
            return false;
        }

        camera = bases[0];
        Vector3 forward = camera.transform.forward;
        Vector3 up = camera.transform.up;
        bool looksAlongZ =
            Mathf.Abs(forward.x) <= 0.02f &&
            Mathf.Abs(forward.y) <= 0.02f &&
            forward.z > 0.98f;
        bool upIsWorldY =
            Mathf.Abs(up.x) <= 0.02f &&
            up.y > 0.98f &&
            Mathf.Abs(up.z) <= 0.02f;

        if (!camera.orthographic || !looksAlongZ || !upIsWorldY)
        {
            error =
                "La cámara " + camera.gameObject.name +
                " no mira el plano XY en ortográfica, con arriba en +Y.\n" +
                "Ortográfica: " + camera.orthographic +
                ". Forward: " + forward.ToString("0.###") +
                ". Up: " + up.ToString("0.###") +
                ".\nNo se modificó la escena.";
            return false;
        }

        if (overlays.Count > 0 || disabled.Count > 0)
        {
            note = DescribeExtraCameras(overlays, disabled);
        }

        return true;
    }

    private static string DescribeCameras(
        List<Camera> bases,
        List<string> overlays,
        List<string> disabled)
    {
        StringBuilder text = new();
        text.AppendLine(
            "No hay una sola cámara de gameplay en Level_01. " +
            "No se modificó la escena."
        );
        text.AppendLine("Cámaras base activas: " + bases.Count + ".");

        foreach (Camera camera in bases)
        {
            text.AppendLine("- " + camera.gameObject.name);
        }

        AppendNames(text, "Cámaras overlay", overlays);
        AppendNames(text, "Cámaras inactivas", disabled);
        return text.ToString();
    }

    private static string DescribeExtraCameras(
        List<string> overlays,
        List<string> disabled)
    {
        StringBuilder text = new();
        AppendNames(text, "Overlay no usado como encuadre", overlays);
        AppendNames(text, "Cámaras inactivas", disabled);
        return text.ToString().Trim();
    }

    private static bool TryResolveRoute(
        Scene scene,
        out Route route,
        out string error)
    {
        route = null;
        error = null;
        List<EnemySpawner> spawners = new();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            spawners.AddRange(
                root.GetComponentsInChildren<EnemySpawner>(true)
            );
        }

        if (spawners.Count == 0)
        {
            error =
                "Level_01 no tiene EnemySpawner. " +
                "No se modificó la escena.";
            return false;
        }

        EnemyPath path = null;
        List<string> names = new();

        foreach (EnemySpawner spawner in spawners)
        {
            names.Add(spawner.gameObject.name);
            EnemyPath candidate = ReadReference<EnemyPath>(spawner, "enemyPath");

            if (candidate == null)
            {
                error =
                    spawner.gameObject.name +
                    " no tiene una ruta asignada. No se modificó la escena.";
                return false;
            }

            if (path == null)
            {
                path = candidate;
                continue;
            }

            if (path != candidate)
            {
                error =
                    "Los spawners apuntan a rutas distintas. " +
                    "No se modificó la escena.\n- " +
                    path.gameObject.name + "\n- " +
                    candidate.gameObject.name;
                return false;
            }
        }

        if (path.Count < 2)
        {
            error =
                path.gameObject.name +
                " necesita al menos dos waypoints. No se modificó la escena.";
            return false;
        }

        List<Vector3> points = new();
        List<string> pointNames = new();

        for (int index = 0; index < path.Count; index++)
        {
            Transform point = path.GetWaypoint(index);

            if (point == null)
            {
                error =
                    path.gameObject.name +
                    " tiene un waypoint vacío en el orden " + index +
                    ". No se modificó la escena.";
                return false;
            }

            points.Add(point.position);
            pointNames.Add(point.name);
        }

        EnemyMovement enemyPrefab = ReadReference<EnemyMovement>(
            spawners[0],
            "enemyPrefab"
        );

        route = new Route
        {
            Path = path,
            Spawners = spawners,
            SpawnerNames = string.Join(", ", names),
            Points = points,
            PointNames = pointNames,
            EnemyPrefab = enemyPrefab
        };
        return true;
    }

    private static bool TryResolveHeart(
        Scene scene,
        out HeartInfo heart,
        out string error)
    {
        heart = null;
        error = null;
        List<BastionHealth> hearts = new();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            hearts.AddRange(
                root.GetComponentsInChildren<BastionHealth>(true)
            );
        }

        if (hearts.Count != 1)
        {
            error = hearts.Count == 0
                ? "Level_01 no tiene BastionHealth. No se modificó la escena."
                : "Hay varias BastionHealth. No se eligió una. " +
                  Names(hearts) + " No se modificó la escena.";
            return false;
        }

        BastionHealth bastion = hearts[0];
        List<SpriteRenderer> renderers = new();
        renderers.AddRange(
            bastion.GetComponentsInChildren<SpriteRenderer>(true)
        );

        if (renderers.Count != 1)
        {
            error = renderers.Count == 0
                ? bastion.gameObject.name +
                  " no tiene un SpriteRenderer. No se modificó la escena."
                : bastion.gameObject.name +
                  " tiene varios SpriteRenderer. No se modificó la escena.";
            return false;
        }

        SpriteRenderer renderer = renderers[0];

        if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
        {
            error =
                "El visual del corazón está inactivo. " +
                "No se modificó la escena.";
            return false;
        }

        if (renderer.sprite == null || renderer.bounds.size.sqrMagnitude < 0.0001f)
        {
            error =
                "El visual del corazón no tiene un sprite medible. " +
                "No se modificó la escena.";
            return false;
        }

        heart = new HeartInfo
        {
            Health = bastion,
            Renderer = renderer
        };
        return true;
    }

    private static bool TryResolvePlots(
        Scene scene,
        out List<PlotInfo> plots,
        out string error)
    {
        plots = new List<PlotInfo>();
        error = null;
        List<BuildPlot> sources = new();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            sources.AddRange(root.GetComponentsInChildren<BuildPlot>(true));
        }

        if (sources.Count == 0)
        {
            error = "Level_01 no tiene parcelas. No se modificó la escena.";
            return false;
        }

        sources.Sort((left, right) =>
            string.CompareOrdinal(left.gameObject.name, right.gameObject.name));

        foreach (BuildPlot plot in sources)
        {
            SpriteRenderer renderer = ReadReference<SpriteRenderer>(
                plot,
                "plotRenderer"
            );
            Collider2D collider = ReadReference<Collider2D>(plot, "plotCollider");
            TowerController prefab = ReadReference<TowerController>(
                plot,
                "towerPrefab"
            );
            Transform container = ReadReference<Transform>(plot, "towerContainer");

            if (renderer == null || collider == null || prefab == null)
            {
                error =
                    plot.gameObject.name +
                    " no tiene renderer, collider o prefab de torre. " +
                    "No se modificó la escena.";
                return false;
            }

            if (!TryMeasureTower(
                    plot,
                    prefab,
                    container,
                    out Bounds towerBounds,
                    out error))
            {
                return false;
            }

            plots.Add(new PlotInfo
            {
                Plot = plot,
                Renderer = renderer,
                Collider = collider,
                TowerPrefab = prefab,
                TowerBounds = towerBounds
            });
        }

        return true;
    }

    private static bool TryMeasureTower(
        BuildPlot plot,
        TowerController prefab,
        Transform container,
        out Bounds bounds,
        out string error)
    {
        bounds = new Bounds();
        error = null;
        SpriteRenderer renderer = ReadReference<SpriteRenderer>(
            prefab,
            "worldRenderer"
        );
        Sprite sprite = prefab.Data != null
            ? prefab.Data.WorldSprite
            : null;

        if (sprite == null && renderer != null)
        {
            sprite = renderer.sprite;
        }

        if (sprite == null)
        {
            error =
                plot.gameObject.name +
                " no tiene un sprite de torre medible. " +
                "No se modificó la escena.";
            return false;
        }

        if (prefab.transform.localRotation != Quaternion.identity)
        {
            error =
                prefab.gameObject.name +
                " está rotado. No se calculó su tamaño. " +
                "No se modificó la escena.";
            return false;
        }

        if (container != null &&
            container.rotation != Quaternion.identity)
        {
            error =
                plot.gameObject.name +
                " tiene un contenedor de torres rotado. " +
                "No se modificó la escena.";
            return false;
        }

        Vector3 scale = prefab.transform.localScale;

        if (container != null)
        {
            scale = Vector3.Scale(scale, container.lossyScale);
        }

        if (Mathf.Abs(scale.x) < 0.0001f || Mathf.Abs(scale.y) < 0.0001f)
        {
            error =
                prefab.gameObject.name +
                " tiene una escala no válida. No se modificó la escena.";
            return false;
        }

        Vector3 size = Vector3.Scale(
            sprite.bounds.size,
            new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 1f)
        );
        Vector3 center = plot.transform.position + Vector3.Scale(
            sprite.bounds.center,
            scale
        );
        bounds = new Bounds(center, size);
        return true;
    }

    private static bool TryResolveHud(
        Scene scene,
        out HudInfo hud,
        out string error)
    {
        hud = null;
        error = null;
        List<HUDController> controllers = new();
        List<CodexController> codices = new();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            controllers.AddRange(
                root.GetComponentsInChildren<HUDController>(true)
            );
            codices.AddRange(
                root.GetComponentsInChildren<CodexController>(true)
            );
        }

        if (controllers.Count != 1)
        {
            error = controllers.Count == 0
                ? "Level_01 no tiene HUDController. No se modificó la escena."
                : "Hay varios HUDController. No se modificó la escena.";
            return false;
        }

        if (codices.Count != 1)
        {
            error = codices.Count == 0
                ? "Level_01 no tiene CodexController. No se modificó la escena."
                : "Hay varios CodexController. No se modificó la escena.";
            return false;
        }

        HUDController controller = controllers[0];
        Button button = ReadReference<Button>(codices[0], "openButton");
        TMP_Text bastion = ReadReference<TMP_Text>(controller, "bastionText");
        TMP_Text resources = ReadReference<TMP_Text>(controller, "resourcesText");
        TMP_Text wave = ReadReference<TMP_Text>(controller, "waveText");
        TMP_Text enemies = ReadReference<TMP_Text>(controller, "enemiesText");
        TMP_Text waveState = ReadReference<TMP_Text>(controller, "waveStateText");
        TMP_Text status = ReadReference<TMP_Text>(controller, "statusText");
        TMP_Text discoveries = ReadReference<TMP_Text>(
            controller,
            "discoveriesText"
        );

        if (button == null ||
            bastion == null ||
            resources == null ||
            wave == null ||
            enemies == null ||
            waveState == null ||
            status == null ||
            discoveries == null)
        {
            error =
                "Faltan textos del HUD o el botón del códice. " +
                "No se modificó la escena.";
            return false;
        }

        RectTransform hudRect = controller.GetComponent<RectTransform>();
        Canvas canvas = controller.GetComponentInParent<Canvas>();
        CanvasScaler scaler = canvas != null
            ? canvas.GetComponent<CanvasScaler>()
            : null;

        if (hudRect == null || canvas == null || scaler == null)
        {
            error =
                "El HUD no está en un Canvas con Canvas Scaler. " +
                "No se modificó la escena.";
            return false;
        }

        if (controller.transform.parent != canvas.transform)
        {
            error =
                "HUD debe ser hijo directo del Canvas. " +
                "No se modificó la escena.";
            return false;
        }

        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            error =
                "El Canvas Scaler no usa Scale With Screen Size. " +
                "No se modificó la escena ni el scaler.";
            return false;
        }

        if (scaler.referenceResolution.x <= 1f ||
            scaler.referenceResolution.y <= 1f)
        {
            error =
                "La resolución de referencia del Canvas no es válida. " +
                "No se modificó la escena.";
            return false;
        }

        TMP_Text[] texts =
        {
            bastion,
            resources,
            wave,
            enemies,
            waveState,
            status,
            discoveries
        };

        foreach (TMP_Text text in texts)
        {
            if (!text.transform.IsChildOf(controller.transform))
            {
                error =
                    text.gameObject.name +
                    " no pertenece al HUD. No se modificó la escena.";
                return false;
            }
        }

        if (!button.transform.IsChildOf(controller.transform))
        {
            error =
                "El botón del códice no pertenece al HUD. " +
                "No se modificó la escena.";
            return false;
        }

        hud = new HudInfo
        {
            Controller = controller,
            Rect = hudRect,
            Canvas = canvas,
            Scaler = scaler,
            Button = button,
            Texts = texts
        };
        return true;
    }

    private static bool TryResolveActors(
        Route route,
        HeartInfo heart,
        List<PlotInfo> plots,
        out ActorOrder actors,
        out string error)
    {
        actors = null;
        error = null;
        List<SpriteRenderer> renderers = new();
        renderers.Add(heart.Renderer);

        foreach (PlotInfo plot in plots)
        {
            renderers.Add(plot.Renderer);
            renderers.AddRange(
                plot.TowerPrefab.GetComponentsInChildren<SpriteRenderer>(true)
            );
        }

        if (route.EnemyPrefab == null)
        {
            error =
                "EnemySpawner no tiene prefab de enemigo. " +
                "No se modificó la escena.";
            return false;
        }

        renderers.AddRange(
            route.EnemyPrefab.GetComponentsInChildren<SpriteRenderer>(true)
        );

        Projectile projectile = null;

        foreach (PlotInfo plot in plots)
        {
            TowerAttack attack = plot.TowerPrefab.GetComponent<TowerAttack>();

            if (attack == null)
            {
                attack = plot.TowerPrefab.GetComponentInChildren<TowerAttack>(true);
            }

            Projectile candidate = attack != null
                ? ReadReference<Projectile>(attack, "projectilePrefab")
                : null;

            if (candidate == null)
            {
                error =
                    plot.TowerPrefab.gameObject.name +
                    " no tiene prefab de proyectil. No se modificó la escena.";
                return false;
            }

            if (projectile == null)
            {
                projectile = candidate;
                continue;
            }

            if (projectile.gameObject != candidate.gameObject)
            {
                error =
                    "Las torres usan proyectiles distintos. " +
                    "No se cambió su orden. No se modificó la escena.";
                return false;
            }
        }

        List<SpriteRenderer> projectileRenderers = new();
        projectileRenderers.AddRange(
            projectile.GetComponentsInChildren<SpriteRenderer>(true)
        );

        if (projectileRenderers.Count == 0)
        {
            error =
                projectile.gameObject.name +
                " no tiene SpriteRenderer. No se modificó la escena.";
            return false;
        }

        int layer = renderers[0].sortingLayerID;

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.sortingLayerID != layer)
            {
                error =
                    "Los actores no comparten la misma sorting layer. " +
                    renderer.gameObject.name +
                    " difiere. No se modificó la escena.";
                return false;
            }
        }

        foreach (SpriteRenderer renderer in projectileRenderers)
        {
            if (renderer.sortingLayerID != layer)
            {
                error =
                    "El proyectil no está en la misma sorting layer que los actores. " +
                    "No se modificó la escena.";
                return false;
            }
        }

        int minimum = int.MaxValue;
        int maximum = int.MinValue;

        foreach (SpriteRenderer renderer in renderers)
        {
            minimum = Mathf.Min(minimum, renderer.sortingOrder);
            maximum = Mathf.Max(maximum, renderer.sortingOrder);
        }

        actors = new ActorOrder
        {
            SortingLayerId = layer,
            Minimum = minimum,
            Maximum = maximum,
            Projectile = projectile
        };
        return true;
    }

    private static bool TryHudFraction(
        CanvasScaler scaler,
        out float fraction,
        out string note,
        out string error)
    {
        fraction = 0f;
        note = null;
        error = null;
        Vector2 reference = scaler.referenceResolution;
        float at1080 = StripFraction(1920f, 1080f, reference, scaler.matchWidthOrHeight);
        float at720 = StripFraction(1280f, 720f, reference, scaler.matchWidthOrHeight);

        if (float.IsNaN(at1080) ||
            float.IsNaN(at720) ||
            at1080 <= 0.02f ||
            at1080 >= 0.45f)
        {
            error =
                "No se pudo reservar una franja HUD válida. " +
                "No se modificó la escena.";
            return false;
        }

        if (Mathf.Abs(at1080 - at720) > 0.001f)
        {
            error =
                "La franja superior no ocupa la misma parte de la pantalla " +
                "en 1920×1080 y en 1280×720. No se modificó la escena.";
            return false;
        }

        fraction = at1080;
        note =
            "1920×1080 y 1280×720 reservan " +
            (fraction * 100f).ToString("0.0", CultureInfo.InvariantCulture) +
            "% superior. Referencia del scaler: " +
            reference.x.ToString("0", CultureInfo.InvariantCulture) + "×" +
            reference.y.ToString("0", CultureInfo.InvariantCulture) +
            ", match " +
            scaler.matchWidthOrHeight.ToString("0.##", CultureInfo.InvariantCulture) +
            ".";
        return true;
    }

    private static float StripFraction(
        float screenWidth,
        float screenHeight,
        Vector2 reference,
        float match)
    {
        float logWidth = Mathf.Log(screenWidth / reference.x);
        float logHeight = Mathf.Log(screenHeight / reference.y);
        float scale = Mathf.Exp(Mathf.Lerp(logWidth, logHeight, match));

        if (scale <= 0.0001f)
        {
            return float.NaN;
        }

        return HudStripHeight / (screenHeight / scale);
    }

    private static Frame FitFrame(Bounds2 content, float hudFraction)
    {
        float gameplay = 1f - hudFraction;
        float fromHeight = content.Height / (2f * gameplay);
        float fromWidth = content.Width / (2f * ReferenceAspect);
        float size = Mathf.Max(fromHeight, fromWidth);
        float centerX = (content.MinX + content.MaxX) * 0.5f;
        float centerY = (content.MinY + content.MaxY) * 0.5f;

        return new Frame
        {
            CenterX = centerX,
            CenterY = centerY + size * hudFraction,
            OrthographicSize = size,
            HudFraction = hudFraction,
            Content = content
        };
    }

    private static bool FrameFits(Frame frame, Bounds2 content, float aspect)
    {
        float halfHeight = frame.OrthographicSize;
        float halfWidth = halfHeight * aspect;
        float left = frame.CenterX - halfWidth;
        float right = frame.CenterX + halfWidth;
        float bottom = frame.CenterY - halfHeight;
        float top = frame.CenterY + halfHeight * (1f - 2f * frame.HudFraction);
        const float epsilon = 0.002f;

        return content.MinX >= left - epsilon &&
            content.MaxX <= right + epsilon &&
            content.MinY >= bottom - epsilon &&
            content.MaxY <= top + epsilon;
    }

    private static Bounds2 MeasureContent(
        Route route,
        HeartInfo heart,
        List<PlotInfo> plots,
        List<Triangle> border)
    {
        Bounds2 content = default;

        foreach (Triangle triangle in border)
        {
            content.Add(triangle.A);
            content.Add(triangle.B);
            content.Add(triangle.C);
        }

        foreach (Vector3 point in route.Points)
        {
            content.Add(point.x, point.y);
        }

        content.Add(heart.Renderer.bounds);

        foreach (PlotInfo plot in plots)
        {
            if (plot.Renderer.sprite != null)
            {
                content.Add(plot.Renderer.bounds);
            }

            content.Add(plot.Collider.bounds);
            content.Add(plot.TowerBounds);

            foreach (TowerController tower in plot.Plot.GetComponentsInChildren<TowerController>(true))
            {
                foreach (SpriteRenderer renderer in tower.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.sprite != null)
                    {
                        content.Add(renderer.bounds);
                    }
                }
            }
        }

        return content;
    }

    private static bool TryEnsureAssets(Shader shader, out string error)
    {
        error = null;
        EnsureFolder();

        if (!File.Exists(ToAbsolute(WhiteTexturePath)))
        {
            WriteTexture(WhiteTexturePath, CreateWhite());
        }

        if (!File.Exists(ToAbsolute(TerrainTexturePath)))
        {
            WriteTexture(TerrainTexturePath, CreateTerrain());
        }

        Texture2D white = AssetDatabase.LoadAssetAtPath<Texture2D>(WhiteTexturePath);
        Texture2D terrain = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainTexturePath);

        if (white == null || terrain == null)
        {
            error =
                "No se pudieron importar las texturas del escenario. " +
                "No se modificó la escena.";
            return false;
        }

        EnsureMaterial(PathFillMaterialPath, shader, white, PathFillTint);
        EnsureMaterial(PathBorderMaterialPath, shader, white, PathBorderTint);
        EnsureMaterial(TerrainMaterialPath, shader, terrain, TerrainTint);
        EnsureTerrainMesh();
        return true;
    }

    private static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(GeneratedFolder))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Art"))
        {
            AssetDatabase.CreateFolder("Assets", "Art");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Art/Generated"))
        {
            AssetDatabase.CreateFolder("Assets/Art", "Generated");
        }

        AssetDatabase.CreateFolder("Assets/Art/Generated", "Level01");
    }

    private static void WriteTexture(string assetPath, Texture2D texture)
    {
        try
        {
            File.WriteAllBytes(ToAbsolute(assetPath), texture.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer =
            AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    private static Texture2D CreateWhite()
    {
        Texture2D texture = new(4, 4, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[16];

        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = new Color32(255, 255, 255, 255);
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D CreateTerrain()
    {
        Texture2D texture = new(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[TextureSize * TextureSize];

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float u = x / (float)(TextureSize - 1);
                float v = y / (float)(TextureSize - 1);
                float fine = ValueNoise(u * 4f, v * 4f);
                float broad = ValueNoise(u * 1.4f + 8f, v * 1.4f + 3f);
                float red = 0.34f + (fine - 0.5f) * 0.05f + (broad - 0.5f) * 0.035f;
                float green = 0.40f + (fine - 0.5f) * 0.045f + (broad - 0.5f) * 0.03f;
                float blue = 0.28f + (fine - 0.5f) * 0.035f + (broad - 0.5f) * 0.02f;
                pixels[y * TextureSize + x] = new Color32(
                    ToByte(red),
                    ToByte(green),
                    ToByte(blue),
                    255
                );
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static byte ToByte(float value)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
    }

    private static float ValueNoise(float x, float y)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float tx = Smooth(x - x0);
        float ty = Smooth(y - y0);
        float a = Hash01(x0, y0);
        float b = Hash01(x0 + 1, y0);
        float c = Hash01(x0, y0 + 1);
        float d = Hash01(x0 + 1, y0 + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    private static float Smooth(float value)
    {
        return value * value * (3f - 2f * value);
    }

    private static float Hash01(int x, int y)
    {
        unchecked
        {
            uint hash = (uint)(x * 374761393 + y * 668265263);
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            hash ^= hash >> 16;
            return (hash & 65535) / 65535f;
        }
    }

    private static void EnsureMaterial(
        string assetPath,
        Shader shader,
        Texture texture,
        Color tint)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

        if (material != null)
        {
            return;
        }

        material = new Material(shader)
        {
            name = Path.GetFileNameWithoutExtension(assetPath)
        };
        material.SetTexture("_MainTex", texture);
        material.SetColor("_White", tint);
        AssetDatabase.CreateAsset(material, assetPath);
    }

    private static void EnsureTerrainMesh()
    {
        if (AssetDatabase.LoadAssetAtPath<Mesh>(TerrainMeshPath) != null)
        {
            return;
        }

        Mesh mesh = new()
        {
            name = "Level01Terrain"
        };
        mesh.SetVertices(new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f)
        });
        mesh.SetUVs(0, new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        });
        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        mesh.SetNormals(new[]
        {
            Vector3.back,
            Vector3.back,
            Vector3.back,
            Vector3.back
        });
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, TerrainMeshPath);
    }

    private static bool Apply(Inspection inspection)
    {
        LevelVisualSettings settings = EnsureRoot(inspection);
        inspection.Settings = settings;
        bool changed = false;

        if (!settings.CameraApplied)
        {
            ApplyCamera(inspection);
            Record(settings);
            settings.RememberCamera(
                inspection.Camera.transform.position,
                inspection.Camera.orthographicSize
            );
            inspection.CameraUpdated = true;
            changed = true;
        }

        if (EnsureTerrain(inspection))
        {
            changed = true;
        }

        if (EnsurePath(inspection))
        {
            changed = true;
        }

        if (!settings.HudApplied)
        {
            ApplyHud(inspection.Hud);
            Record(settings);
            settings.RememberHud();
            changed = true;
        }
        else if (EnsureMissingHudBand(inspection.Hud))
        {
            changed = true;
        }

        if (!settings.ProjectileSortingApplied)
        {
            bool updated = ApplyProjectileOrder(inspection);
            bool unresolved =
                inspection.ProjectileNote != null &&
                inspection.ProjectileNote.StartsWith("No se encontró");

            if (!unresolved)
            {
                Record(settings);
                settings.RememberProjectileSorting();
                changed = true;
            }

            if (updated)
            {
                changed = true;
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(settings);

            if (PrefabUtility.IsPartOfPrefabInstance(settings))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(settings);
            }
        }

        return changed;
    }

    private static LevelVisualSettings EnsureRoot(Inspection inspection)
    {
        if (inspection.Settings != null)
        {
            return inspection.Settings;
        }

        GameObject root = new("LevelVisuals");
        SceneManager.MoveGameObjectToScene(root, inspection.Scene);
        Undo.RegisterCreatedObjectUndo(root, UndoName);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        return Undo.AddComponent<LevelVisualSettings>(root);
    }

    private static void ApplyCamera(Inspection inspection)
    {
        Transform transform = inspection.Camera.transform;
        Record(transform);
        Record(inspection.Camera);
        Vector3 position = transform.position;
        position.x = inspection.Frame.CenterX;
        position.y = inspection.Frame.CenterY;
        transform.position = position;
        inspection.Camera.orthographicSize = inspection.Frame.OrthographicSize;
        PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(inspection.Camera);
    }

    private static bool EnsureTerrain(Inspection inspection)
    {
        Transform terrain = FindChild(inspection.Settings.transform, "Terrain");
        bool created = terrain == null;

        if (inspection.Settings.TerrainApplied && !created)
        {
            return false;
        }

        if (created)
        {
            GameObject child = CreateChild(
                "Terrain",
                inspection.Settings.transform,
                inspection.Scene
            );
            terrain = child.transform;
            Undo.AddComponent<MeshFilter>(child);
            Undo.AddComponent<MeshRenderer>(child);
        }

        Camera camera = inspection.Camera;
        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * ReferenceAspect;
        Record(terrain);
        terrain.localRotation = Quaternion.identity;
        terrain.localPosition = inspection.Settings.transform.InverseTransformPoint(
            new Vector3(camera.transform.position.x, camera.transform.position.y, 1f)
        );
        Vector3 parentScale = inspection.Settings.transform.lossyScale;
        float scaleX = Mathf.Abs(parentScale.x) < 0.0001f
            ? 1f
            : (halfWidth * 2f + TerrainMargin * 2f) / parentScale.x;
        float scaleY = Mathf.Abs(parentScale.y) < 0.0001f
            ? 1f
            : (halfHeight * 2f + TerrainMargin * 2f) / parentScale.y;
        terrain.localScale = new Vector3(Mathf.Abs(scaleX), Mathf.Abs(scaleY), 1f);

        MeshFilter filter = terrain.GetComponent<MeshFilter>();
        MeshRenderer renderer = terrain.GetComponent<MeshRenderer>();

        if (filter == null)
        {
            filter = Undo.AddComponent<MeshFilter>(terrain.gameObject);
        }

        if (renderer == null)
        {
            renderer = Undo.AddComponent<MeshRenderer>(terrain.gameObject);
        }
        Record(filter);
        Record(renderer);
        filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(TerrainMeshPath);

        if (renderer.sharedMaterial == null)
        {
            renderer.sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
        }

        PrepareRenderer(
            renderer,
            inspection.Actors.SortingLayerId,
            inspection.Actors.Minimum - 30
        );
        Record(inspection.Settings);
        inspection.Settings.RememberTerrain();
        return true;
    }

    private static bool EnsurePath(Inspection inspection)
    {
        if (!CanBake(inspection.Settings.transform, out _))
        {
            return false;
        }

        string signature = BuildSignature(inspection.Route.Points, inspection.PathWidth);
        bool missing = FindChild(inspection.Settings.transform, "Path") == null;
        bool changedSignature =
            signature != inspection.Settings.AppliedPathSignature;
        inspection.PathCreated =
            string.IsNullOrEmpty(inspection.Settings.AppliedPathSignature);

        if (!missing && !changedSignature)
        {
            return false;
        }

        Transform path = FindChild(inspection.Settings.transform, "Path");

        if (path == null)
        {
            path = CreateChild("Path", inspection.Settings.transform, inspection.Scene)
                .transform;
        }

        Record(path);
        path.localPosition = Vector3.zero;
        path.localRotation = Quaternion.identity;
        path.localScale = Vector3.one;

        WriteStroke(
            inspection,
            path,
            "PathBorder",
            inspection.Border,
            PathBorderMeshPath,
            PathBorderMaterialPath,
            inspection.Actors.Minimum - 20
        );
        WriteStroke(
            inspection,
            path,
            "PathFill",
            inspection.Fill,
            PathFillMeshPath,
            PathFillMaterialPath,
            inspection.Actors.Minimum - 10
        );
        Record(inspection.Settings);
        inspection.Settings.RememberPath(signature, inspection.PathWidth);
        inspection.PathRebuilt = true;
        inspection.PathWasMissing = missing && !changedSignature;
        return true;
    }

    private static void WriteStroke(
        Inspection inspection,
        Transform parent,
        string name,
        List<Triangle> triangles,
        string meshPath,
        string materialPath,
        int sortingOrder)
    {
        Transform child = FindChild(parent, name);
        bool created = child == null;

        if (created)
        {
            child = CreateChild(name, parent, inspection.Scene).transform;
            Undo.AddComponent<MeshFilter>(child.gameObject);
            Undo.AddComponent<MeshRenderer>(child.gameObject);
        }

        Record(child);
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;

        Mesh mesh = LoadOrCreateMesh(meshPath);
        FillMesh(mesh, triangles, parent);
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssetIfDirty(mesh);

        MeshFilter filter = child.GetComponent<MeshFilter>();
        MeshRenderer renderer = child.GetComponent<MeshRenderer>();
        Record(filter);
        Record(renderer);
        filter.sharedMesh = mesh;

        if (created || renderer.sharedMaterial == null)
        {
            renderer.sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            PrepareRenderer(
                renderer,
                inspection.Actors.SortingLayerId,
                sortingOrder
            );
        }
    }

    private static void FillMesh(
        Mesh mesh,
        List<Triangle> triangles,
        Transform space)
    {
        Vector3[] vertices = new Vector3[triangles.Count * 3];
        Vector2[] uvs = new Vector2[vertices.Length];
        Vector3[] normals = new Vector3[vertices.Length];
        int[] indices = new int[vertices.Length];
        Vector3 localNormal = space.InverseTransformDirection(Vector3.back).normalized;

        for (int index = 0; index < triangles.Count; index++)
        {
            Triangle triangle = triangles[index];
            int vertex = index * 3;
            vertices[vertex] = space.InverseTransformPoint(
                new Vector3(triangle.A.x, triangle.A.y, 1f)
            );
            vertices[vertex + 1] = space.InverseTransformPoint(
                new Vector3(triangle.B.x, triangle.B.y, 1f)
            );
            vertices[vertex + 2] = space.InverseTransformPoint(
                new Vector3(triangle.C.x, triangle.C.y, 1f)
            );
            uvs[vertex] = new Vector2(0.5f, 0.5f);
            uvs[vertex + 1] = uvs[vertex];
            uvs[vertex + 2] = uvs[vertex];
            normals[vertex] = localNormal;
            normals[vertex + 1] = localNormal;
            normals[vertex + 2] = localNormal;
            indices[vertex] = vertex;
            indices[vertex + 1] = vertex + 1;
            indices[vertex + 2] = vertex + 2;
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(normals);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
    }

    private static bool ApplyProjectileOrder(Inspection inspection)
    {
        string assetPath = AssetDatabase.GetAssetPath(inspection.Actors.Projectile);
        int target = inspection.Actors.Maximum + 1;

        if (string.IsNullOrEmpty(assetPath) ||
            !assetPath.EndsWith(".prefab"))
        {
            inspection.ProjectileNote =
                "No se encontró el prefab del proyectil para ordenarlo.";
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(assetPath);

        try
        {
            bool changed = false;

            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sortingOrder >= target)
                {
                    continue;
                }

                renderer.sortingOrder = target;
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                inspection.ProjectileNote =
                    "El proyectil quedó en sorting order " + target +
                    ". Su prefab se guardó. La escena no.";
            }
            else
            {
                inspection.ProjectileNote =
                    "El proyectil ya estaba por encima de los actores.";
            }

            return changed;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ApplyHud(HudInfo hud)
    {
        Record(hud.Rect);
        hud.Rect.anchorMin = Vector2.zero;
        hud.Rect.anchorMax = Vector2.one;
        hud.Rect.pivot = new Vector2(0.5f, 0.5f);
        hud.Rect.offsetMin = Vector2.zero;
        hud.Rect.offsetMax = Vector2.zero;
        hud.Rect.anchoredPosition = Vector2.zero;
        hud.Rect.localScale = Vector3.one;
        hud.Rect.localRotation = Quaternion.identity;
        EnsureHudBand(hud);
        Place(hud.Texts[0], 0.015f, 0.255f, 14f, 50f, "Integridad");
        Place(hud.Texts[1], 0.255f, 0.495f, 14f, 50f, "Semillas");
        Place(hud.Texts[2], 0.495f, 0.735f, 14f, 50f, "Oleada");
        Place(hud.Texts[3], 0.735f, 0.985f, 14f, 50f, "Enemigos");
        Place(hud.Texts[4], 0.015f, 0.30f, 72f, 50f, "Fase");
        Place(hud.Texts[5], 0.30f, 0.55f, 72f, 50f, "Partida");
        Place(hud.Texts[6], 0.55f, 0.76f, 72f, 50f, "Descubrimientos");
        PlaceButton(hud.Button);
    }

    private static bool EnsureMissingHudBand(HudInfo hud)
    {
        if (FindChild(hud.Controller.transform, "HudBand") != null)
        {
            return false;
        }

        EnsureHudBand(hud);
        return true;
    }

    private static void EnsureHudBand(HudInfo hud)
    {
        Transform existing = FindChild(hud.Controller.transform, "HudBand");
        Image image;

        if (existing == null)
        {
            GameObject band = new("HudBand", typeof(RectTransform), typeof(Image));
            band.layer = hud.Controller.gameObject.layer;
            SceneManager.MoveGameObjectToScene(band, hud.Controller.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(band, UndoName);
            band.transform.SetParent(hud.Controller.transform, false);
            existing = band.transform;
            image = band.GetComponent<Image>();
        }
        else
        {
            image = existing.GetComponent<Image>();

            if (image == null)
            {
                image = Undo.AddComponent<Image>(existing.gameObject);
            }
        }

        existing.SetAsFirstSibling();
        RectTransform rect = existing.GetComponent<RectTransform>();
        Record(rect);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, HudStripHeight);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        Record(image);
        image.color = HudBandColor;
        image.raycastTarget = false;
    }

    private static void Place(
        TMP_Text text,
        float xMin,
        float xMax,
        float top,
        float height,
        string preview)
    {
        RectTransform rect = text.rectTransform;
        Record(rect);
        Record(text);
        rect.anchorMin = new Vector2(xMin, 1f);
        rect.anchorMax = new Vector2(xMax, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(0f, height);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        text.fontSize = 28f;
        text.color = HudTextColor;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = false;
        text.raycastTarget = false;
        text.margin = new Vector4(10f, 0f, 10f, 0f);
        text.text = preview;
    }

    private static void PlaceButton(Button button)
    {
        RectTransform rect = button.GetComponent<RectTransform>();
        Record(rect);
        Record(button);
        rect.anchorMin = new Vector2(0.78f, 1f);
        rect.anchorMax = new Vector2(0.985f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -72f);
        rect.sizeDelta = new Vector2(0f, 50f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        if (button.targetGraphic != null)
        {
            Record(button.targetGraphic);
            button.targetGraphic.raycastTarget = true;
        }

        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            RectTransform labelRect = label.rectTransform;
            Record(labelRect);
            Record(label);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            labelRect.localScale = Vector3.one;
            labelRect.localRotation = Quaternion.identity;
            label.fontSize = 28f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableAutoSizing = false;
            label.raycastTarget = false;
            label.color = HudTextColor;
            label.text = "Códice";
        }
    }

    private static bool TryBuildStroke(
        List<Vector3> points,
        float halfWidth,
        out List<Triangle> triangles,
        out string error)
    {
        triangles = new List<Triangle>();
        error = null;
        List<Vector2> clean = new();

        foreach (Vector3 point in points)
        {
            Vector2 flat = new(point.x, point.y);

            if (clean.Count == 0 ||
                (flat - clean[clean.Count - 1]).sqrMagnitude > 0.00000001f)
            {
                clean.Add(flat);
            }
        }

        if (clean.Count < 2)
        {
            error =
                "La ruta no tiene un segmento medible. " +
                "No se modificó la escena.";
            return false;
        }

        for (int index = 0; index < clean.Count - 1; index++)
        {
            Vector2 direction = (clean[index + 1] - clean[index]).normalized;
            Vector2 left = new Vector2(-direction.y, direction.x) * halfWidth;
            AddTriangle(
                triangles,
                clean[index] + left,
                clean[index + 1] + left,
                clean[index + 1] - left
            );
            AddTriangle(
                triangles,
                clean[index] + left,
                clean[index + 1] - left,
                clean[index] - left
            );
        }

        for (int index = 1; index < clean.Count - 1; index++)
        {
            Vector2 incoming = (clean[index] - clean[index - 1]).normalized;
            Vector2 outgoing = (clean[index + 1] - clean[index]).normalized;
            AddJoin(triangles, clean[index], incoming, outgoing, halfWidth);
        }

        return triangles.Count > 0;
    }

    private static void AddJoin(
        List<Triangle> triangles,
        Vector2 point,
        Vector2 incoming,
        Vector2 outgoing,
        float halfWidth)
    {
        float cross = incoming.x * outgoing.y - incoming.y * outgoing.x;
        float dot = Vector2.Dot(incoming, outgoing);

        if (dot > 0.999f && Mathf.Abs(cross) < 0.02f)
        {
            return;
        }

        if (dot < -0.999f)
        {
            AddCircle(triangles, point, halfWidth);
            return;
        }

        Vector2 normalIn = new(-incoming.y, incoming.x);
        Vector2 normalOut = new(-outgoing.y, outgoing.x);
        Vector2 outerIn = cross > 0f ? -normalIn : normalIn;
        Vector2 outerOut = cross > 0f ? -normalOut : normalOut;
        float start = Mathf.Atan2(outerIn.y, outerIn.x);
        float end = Mathf.Atan2(outerOut.y, outerOut.x);
        float delta = Mathf.DeltaAngle(
            start * Mathf.Rad2Deg,
            end * Mathf.Rad2Deg
        ) * Mathf.Deg2Rad;
        int steps = Mathf.Max(
            1,
            Mathf.CeilToInt(Mathf.Abs(delta) / (Mathf.PI / 8f))
        );
        Vector2 previous = point + outerIn * halfWidth;

        for (int step = 1; step <= steps; step++)
        {
            float angle = start + delta * step / steps;
            Vector2 next = point + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * halfWidth;
            AddTriangle(triangles, point, previous, next);
            previous = next;
        }
    }

    private static void AddCircle(
        List<Triangle> triangles,
        Vector2 point,
        float halfWidth)
    {
        const int steps = 12;
        Vector2 previous = point + Vector2.right * halfWidth;

        for (int step = 1; step <= steps; step++)
        {
            float angle = Mathf.PI * 2f * step / steps;
            Vector2 next = point + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * halfWidth;
            AddTriangle(triangles, point, previous, next);
            previous = next;
        }
    }

    private static void AddTriangle(
        List<Triangle> triangles,
        Vector2 a,
        Vector2 b,
        Vector2 c)
    {
        float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        if (Mathf.Abs(cross) < 0.0000001f)
        {
            return;
        }

        if (cross > 0f)
        {
            triangles.Add(new Triangle(a, c, b));
            return;
        }

        triangles.Add(new Triangle(a, b, c));
    }

    private static string BuildSignature(List<Vector3> points, float width)
    {
        StringBuilder text = new();
        text.Append(width.ToString("0.0000", CultureInfo.InvariantCulture));
        text.Append('|');
        text.Append(PathEdge.ToString("0.0000", CultureInfo.InvariantCulture));

        foreach (Vector3 point in points)
        {
            text.Append('|');
            text.Append(point.x.ToString("0.0000", CultureInfo.InvariantCulture));
            text.Append(',');
            text.Append(point.y.ToString("0.0000", CultureInfo.InvariantCulture));
            text.Append(',');
            text.Append(point.z.ToString("0.0000", CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    private static string BuildReport(Inspection inspection, bool changed)
    {
        LevelVisualSettings settings = inspection.Settings;
        Camera camera = inspection.Camera;
        Frame frame = inspection.Frame;
        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * ReferenceAspect;
        StringBuilder text = new();
        text.AppendLine("Encuadre activo a 16:9");
        text.AppendLine(
            "Centro (" +
            Format(camera.transform.position.x) + ", " +
            Format(camera.transform.position.y) +
            "), Z " + Format(camera.transform.position.z) + "."
        );
        text.AppendLine(
            "Tamaño ortográfico " + Format(camera.orthographicSize) +
            ". Mundo visible " + Format(halfWidth * 2f) +
            " × " + Format(halfHeight * 2f) + "."
        );
        text.AppendLine(
            "Origen (" + Format(camera.transform.position.x - halfWidth) +
            ", " + Format(camera.transform.position.y - halfHeight) +
            "), extremo (" +
            Format(camera.transform.position.x + halfWidth) + ", " +
            Format(camera.transform.position.y + halfHeight) + ")."
        );
        text.AppendLine(inspection.FractionNote);

        if (inspection.CameraUpdated)
        {
            text.AppendLine(
                "Se aplicó este encuadre. Z y orientación de la cámara se conservaron."
            );
        }
        else if (settings != null && settings.CameraApplied)
        {
            bool custom =
                Mathf.Abs(camera.transform.position.x - frame.CenterX) > 0.02f ||
                Mathf.Abs(camera.transform.position.y - frame.CenterY) > 0.02f ||
                Mathf.Abs(camera.orthographicSize - frame.OrthographicSize) > 0.02f;
            text.AppendLine(
                custom
                    ? "La cámara conserva un ajuste posterior. El cálculo actual sería centro (" +
                      Format(frame.CenterX) + ", " + Format(frame.CenterY) +
                      ") y tamaño " + Format(frame.OrthographicSize) + "."
                    : "La cámara ya tenía el encuadre calculado y se conservó."
            );
        }

        text.AppendLine(
            "Contenido medido (" + Format(frame.Content.MinX) + ", " +
            Format(frame.Content.MinY) + ") a (" +
            Format(frame.Content.MaxX) + ", " + Format(frame.Content.MaxY) +
            "), margen " + Format(ContentMargin) + "."
        );
        text.AppendLine(
            "Terreno: verde desaturado, margen " + Format(TerrainMargin) +
            " alrededor del encuadre. Camino: ancho " +
            Format(inspection.PathWidth) +
            ", borde " + Format(PathEdge) + " a cada lado."
        );
        text.AppendLine(
            "Ruta " + inspection.Route.Path.gameObject.name +
            ", " + inspection.Route.Points.Count + " puntos, longitud XY " +
            Format(PathLength(inspection.Route.Points)) + "."
        );
        text.AppendLine(
            "Spawner en (" +
            Format(inspection.Route.Spawners[0].transform.position.x) + ", " +
            Format(inspection.Route.Spawners[0].transform.position.y) +
            "). Ese punto no forma un segmento."
        );

        if (inspection.PathRebuilt)
        {
            text.AppendLine(
                inspection.PathCreated
                    ? "Se creó el camino sobre los waypoints, sin suavizar el eje."
                    : inspection.PathWasMissing
                        ? "Se regeneró la malla del camino porque faltaba."
                        : "Se reconstruyó el camino porque cambió la ruta o su ancho. La malla se guardó."
            );
        }
        else if (!string.IsNullOrEmpty(inspection.BakeNote))
        {
            text.AppendLine(inspection.BakeNote);
        }
        else
        {
            text.AppendLine("El camino ya seguía esta ruta y este ancho.");
        }

        AppendOverlaps(text, inspection);
        AppendOutside(text, inspection, camera);
        text.AppendLine(
            "Orden: terreno " + (inspection.Actors.Minimum - 30) +
            ", borde " + (inspection.Actors.Minimum - 20) +
            ", camino " + (inspection.Actors.Minimum - 10) +
            ", actores desde " + inspection.Actors.Minimum +
            ". No se cambió el orden entre actores."
        );

        if (!string.IsNullOrEmpty(inspection.ProjectileNote))
        {
            text.AppendLine(inspection.ProjectileNote);
        }

        text.AppendLine(
            settings != null && settings.HudApplied
                ? "HUD en franja superior de dos filas. Infografía, códice y resultado no se tocaron."
                : "El HUD aún no quedó marcado como aplicado."
        );

        if (!string.IsNullOrEmpty(inspection.CameraNote))
        {
            text.AppendLine(inspection.CameraNote);
        }

        if (!string.IsNullOrEmpty(inspection.ExportNote))
        {
            text.AppendLine(inspection.ExportNote);
        }

        text.AppendLine(
            changed
                ? "La escena quedó modificada y no se guardó. Esta base no es el arte final."
                : "No hubo cambios nuevos en la escena. Esta base no es el arte final."
        );
        return text.ToString().Trim();
    }

    private static void AppendOverlaps(StringBuilder text, Inspection inspection)
    {
        bool any = false;

        foreach (PlotInfo plot in inspection.Plots)
        {
            if (plot.Renderer.sprite != null &&
                Hits(inspection.Border, plot.Renderer.bounds))
            {
                text.AppendLine(
                    "Conflicto: el camino solapa el renderer de " +
                    plot.Plot.gameObject.name + "."
                );
                any = true;
            }

            if (Hits(inspection.Border, plot.Collider.bounds))
            {
                text.AppendLine(
                    "Conflicto: el camino solapa el collider de " +
                    plot.Plot.gameObject.name + "."
                );
                any = true;
            }
        }

        if (Hits(inspection.Border, inspection.Heart.Renderer.bounds))
        {
            text.AppendLine(
                "El camino llega al visual del corazón. " +
                "No se movió ninguno de los dos."
            );
            any = true;
        }

        if (!any)
        {
            text.AppendLine("El camino no solapa parcelas ni el corazón.");
        }
    }

    private static void AppendOutside(
        StringBuilder text,
        Inspection inspection,
        Camera camera)
    {
        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * ReferenceAspect;
        Rect view = Rect.MinMaxRect(
            camera.transform.position.x - halfWidth,
            camera.transform.position.y - halfHeight,
            camera.transform.position.x + halfWidth,
            camera.transform.position.y + halfHeight
        );
        bool raisedZ = false;

        for (int index = 0; index < inspection.Route.Points.Count; index++)
        {
            if (Mathf.Abs(inspection.Route.Points[index].z) > 0.001f)
            {
                raisedZ = true;
            }
        }

        if (raisedZ)
        {
            text.AppendLine(
                "Hay waypoints con Z distinta de 0. El camino usa solo XY."
            );
        }

        foreach (GameObject root in inspection.Scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "Ground")
                {
                    continue;
                }

                SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();

                if (renderer == null || renderer.sprite == null)
                {
                    continue;
                }

                if (view.Overlaps(Rect.MinMaxRect(
                        renderer.bounds.min.x,
                        renderer.bounds.min.y,
                        renderer.bounds.max.x,
                        renderer.bounds.max.y)))
                {
                    text.AppendLine(
                        "Ground queda dentro del encuadre y conserva su sprite actual."
                    );
                }
            }
        }
    }

    private static string CompareExport(
        Scene scene,
        Camera camera,
        Route route,
        HeartInfo heart,
        List<PlotInfo> plots)
    {
        string path = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            LayoutJsonPath
        );

        if (!File.Exists(path))
        {
            return "No se encontró el plano exportado. Se usó la escena.";
        }

        LayoutDocument document;

        try
        {
            document = JsonUtility.FromJson<LayoutDocument>(File.ReadAllText(path));
        }
        catch (System.Exception)
        {
            return "El plano exportado no se pudo leer. Se usó la escena.";
        }

        if (document == null || document.camera == null || document.path == null)
        {
            return "El plano exportado está incompleto. Se usó la escena.";
        }

        List<string> differences = new();

        if (Mathf.Abs(document.camera.positionX - camera.transform.position.x) > 0.02f ||
            Mathf.Abs(document.camera.positionY - camera.transform.position.y) > 0.02f ||
            Mathf.Abs(document.camera.orthographicSize - camera.orthographicSize) > 0.02f)
        {
            differences.Add(
                "cámara exportada (" +
                Format(document.camera.positionX) + ", " +
                Format(document.camera.positionY) +
                ", tamaño " + Format(document.camera.orthographicSize) + ")"
            );
        }

        if (document.heart != null &&
            (Mathf.Abs(document.heart.positionX - heart.Health.transform.position.x) > 0.02f ||
             Mathf.Abs(document.heart.positionY - heart.Health.transform.position.y) > 0.02f))
        {
            differences.Add("posición del corazón");
        }

        if (document.heart != null &&
            document.heart.visualKnown &&
            document.heart.visual != null &&
            Mathf.Abs(document.heart.visual.minY - heart.Renderer.bounds.min.y) > 0.02f)
        {
            differences.Add("límite inferior del corazón");
        }

        if (document.path.waypoints == null ||
            document.path.waypoints.Count != route.Points.Count)
        {
            differences.Add("cantidad de waypoints");
        }

        if (plots.Count != (document.plots == null ? 0 : document.plots.Count))
        {
            differences.Add("cantidad de parcelas");
        }

        if (differences.Count == 0)
        {
            return
                "El plano exportado coincide con la escena antes de esta acción.";
        }

        return
            "Antes de integrar, la escena difería del plano en: " +
            string.Join(", ", differences) +
            ". Se usaron los datos de la escena. Escena abierta: " +
            scene.path + ".";
    }

    private static bool Hits(List<Triangle> triangles, Bounds bounds)
    {
        Rect rect = Rect.MinMaxRect(
            bounds.min.x,
            bounds.min.y,
            bounds.max.x,
            bounds.max.y
        );

        foreach (Triangle triangle in triangles)
        {
            if (TriangleHits(triangle, rect))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TriangleHits(Triangle triangle, Rect rect)
    {
        if (Contains(rect, triangle.A) ||
            Contains(rect, triangle.B) ||
            Contains(rect, triangle.C))
        {
            return true;
        }

        Vector2 p0 = new(rect.xMin, rect.yMin);
        Vector2 p1 = new(rect.xMax, rect.yMin);
        Vector2 p2 = new(rect.xMax, rect.yMax);
        Vector2 p3 = new(rect.xMin, rect.yMax);

        if (PointInTriangle(p0, triangle) ||
            PointInTriangle(p1, triangle) ||
            PointInTriangle(p2, triangle) ||
            PointInTriangle(p3, triangle))
        {
            return true;
        }

        return SegmentsHit(triangle.A, triangle.B, p0, p1) ||
            SegmentsHit(triangle.A, triangle.B, p1, p2) ||
            SegmentsHit(triangle.A, triangle.B, p2, p3) ||
            SegmentsHit(triangle.A, triangle.B, p3, p0) ||
            SegmentsHit(triangle.B, triangle.C, p0, p1) ||
            SegmentsHit(triangle.B, triangle.C, p1, p2) ||
            SegmentsHit(triangle.B, triangle.C, p2, p3) ||
            SegmentsHit(triangle.B, triangle.C, p3, p0) ||
            SegmentsHit(triangle.C, triangle.A, p0, p1) ||
            SegmentsHit(triangle.C, triangle.A, p1, p2) ||
            SegmentsHit(triangle.C, triangle.A, p2, p3) ||
            SegmentsHit(triangle.C, triangle.A, p3, p0);
    }

    private static bool Contains(Rect rect, Vector2 point)
    {
        return point.x >= rect.xMin &&
            point.x <= rect.xMax &&
            point.y >= rect.yMin &&
            point.y <= rect.yMax;
    }

    private static bool PointInTriangle(Vector2 point, Triangle triangle)
    {
        float left = Cross(triangle.A, triangle.B, point);
        float middle = Cross(triangle.B, triangle.C, point);
        float right = Cross(triangle.C, triangle.A, point);
        bool negative = left < 0f || middle < 0f || right < 0f;
        bool positive = left > 0f || middle > 0f || right > 0f;
        return !(negative && positive);
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 point)
    {
        return (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
    }

    private static bool SegmentsHit(
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Vector2 d)
    {
        float first = Cross(a, b, c);
        float second = Cross(a, b, d);
        float third = Cross(c, d, a);
        float fourth = Cross(c, d, b);
        return ((first > 0f && second < 0f) || (first < 0f && second > 0f)) &&
            ((third > 0f && fourth < 0f) || (third < 0f && fourth > 0f));
    }

    private static float PathLength(List<Vector3> points)
    {
        float length = 0f;

        for (int index = 1; index < points.Count; index++)
        {
            length += Vector2.Distance(
                new Vector2(points[index - 1].x, points[index - 1].y),
                new Vector2(points[index].x, points[index].y)
            );
        }

        return length;
    }

    private static bool CanBake(Transform transform, out string reason)
    {
        reason = null;
        Vector3 scale = transform.lossyScale;

        if (Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.1f)
        {
            reason = "LevelVisuals está rotado.";
            return false;
        }

        if (scale.x <= 0.001f ||
            scale.y <= 0.001f ||
            Mathf.Abs(scale.x - scale.y) > 0.001f)
        {
            reason = "LevelVisuals tiene una escala XY no uniforme.";
            return false;
        }

        return true;
    }

    private static LevelVisualSettings FindSettings(Scene scene, out string error)
    {
        error = null;
        List<LevelVisualSettings> settings = new();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            settings.AddRange(
                root.GetComponentsInChildren<LevelVisualSettings>(true)
            );
        }

        if (settings.Count > 1)
        {
            error =
                "Hay varios LevelVisualSettings. No se modificó la escena.";
            return null;
        }

        return settings.Count == 1 ? settings[0] : null;
    }

    private static GameObject CreateChild(
        string name,
        Transform parent,
        Scene scene)
    {
        GameObject child = new(name);
        SceneManager.MoveGameObjectToScene(child, scene);
        Undo.RegisterCreatedObjectUndo(child, UndoName);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);

            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static Mesh LoadOrCreateMesh(string assetPath)
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

        if (mesh != null)
        {
            return mesh;
        }

        mesh = new Mesh
        {
            name = Path.GetFileNameWithoutExtension(assetPath)
        };
        AssetDatabase.CreateAsset(mesh, assetPath);
        return mesh;
    }

    private static void PrepareRenderer(
        MeshRenderer renderer,
        int sortingLayerId,
        int sortingOrder)
    {
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode =
            MotionVectorGenerationMode.ForceNoMotion;
    }

    private static void Record(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        Undo.RecordObject(target, UndoName);
    }

    private static T ReadReference<T>(UnityEngine.Object owner, string fieldName)
        where T : UnityEngine.Object
    {
        if (owner == null)
        {
            return null;
        }

        SerializedObject serialized = new(owner);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            return null;
        }

        return property.objectReferenceValue as T;
    }

    private static string ToAbsolute(string assetPath)
    {
        return Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            assetPath
        );
    }

    private static string Format(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Names(List<BastionHealth> hearts)
    {
        StringBuilder text = new();

        foreach (BastionHealth heart in hearts)
        {
            text.Append(heart.gameObject.name);
            text.Append(", ");
        }

        return text.ToString();
    }

    private static void AppendNames(
        StringBuilder text,
        string label,
        List<string> names)
    {
        if (names == null || names.Count == 0)
        {
            return;
        }

        text.AppendLine(label + ": " + string.Join(", ", names) + ".");
    }

    private static void Report(string title, string message)
    {
        Debug.Log(title + "\n" + message);
        EditorUtility.DisplayDialog(title, message, "Cerrar");
    }

    private sealed class Inspection
    {
        public Scene Scene;
        public Camera Camera;
        public string CameraNote;
        public Route Route;
        public HeartInfo Heart;
        public List<PlotInfo> Plots;
        public HudInfo Hud;
        public ActorOrder Actors;
        public LevelVisualSettings Settings;
        public Shader Shader;
        public float PathWidth;
        public List<Triangle> Border;
        public List<Triangle> Fill;
        public Frame Frame;
        public string FractionNote;
        public string BakeNote;
        public string ExportNote;
        public string ProjectileNote;
        public bool PathRebuilt;
        public bool PathWasMissing;
        public bool PathCreated;
        public bool CameraUpdated;
    }

    private sealed class Route
    {
        public EnemyPath Path;
        public List<EnemySpawner> Spawners;
        public string SpawnerNames;
        public List<Vector3> Points;
        public List<string> PointNames;
        public EnemyMovement EnemyPrefab;
    }

    private sealed class HeartInfo
    {
        public BastionHealth Health;
        public SpriteRenderer Renderer;
    }

    private sealed class PlotInfo
    {
        public BuildPlot Plot;
        public SpriteRenderer Renderer;
        public Collider2D Collider;
        public TowerController TowerPrefab;
        public Bounds TowerBounds;
    }

    private sealed class HudInfo
    {
        public HUDController Controller;
        public RectTransform Rect;
        public Canvas Canvas;
        public CanvasScaler Scaler;
        public Button Button;
        public TMP_Text[] Texts;
    }

    private sealed class ActorOrder
    {
        public int SortingLayerId;
        public int Minimum;
        public int Maximum;
        public Projectile Projectile;
    }

    private struct Frame
    {
        public float CenterX;
        public float CenterY;
        public float OrthographicSize;
        public float HudFraction;
        public Bounds2 Content;
    }

    private struct Bounds2
    {
        public float MinX;
        public float MinY;
        public float MaxX;
        public float MaxY;
        public bool Valid;
        public float Width => MaxX - MinX;
        public float Height => MaxY - MinY;

        public void Add(Vector2 point)
        {
            Add(point.x, point.y);
        }

        public void Add(Vector3 point)
        {
            Add(point.x, point.y);
        }

        public void Add(float x, float y)
        {
            if (!Valid)
            {
                MinX = MaxX = x;
                MinY = MaxY = y;
                Valid = true;
                return;
            }

            MinX = Mathf.Min(MinX, x);
            MinY = Mathf.Min(MinY, y);
            MaxX = Mathf.Max(MaxX, x);
            MaxY = Mathf.Max(MaxY, y);
        }

        public void Add(Bounds bounds)
        {
            Add(bounds.min.x, bounds.min.y);
            Add(bounds.max.x, bounds.max.y);
        }

        public void Expand(float amount)
        {
            MinX -= amount;
            MinY -= amount;
            MaxX += amount;
            MaxY += amount;
        }
    }

    private readonly struct Triangle
    {
        public Triangle(Vector2 a, Vector2 b, Vector2 c)
        {
            A = a;
            B = b;
            C = c;
        }

        public Vector2 A { get; }
        public Vector2 B { get; }
        public Vector2 C { get; }
    }
}
