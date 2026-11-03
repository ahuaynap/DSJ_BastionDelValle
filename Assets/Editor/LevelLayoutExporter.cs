using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LevelLayoutExporter
{
    private const string MenuPath =
        "Bastion del Valle/Incrementos/16 - Exportar Distribucion del Mapa";

    private const string ScenePath = "Assets/Scenes/Level_01.unity";
    private const int ReferenceWidth = 1920;
    private const int ReferenceHeight = 1080;
    private const float WidthTolerance = 0.001f;

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenu()
    {
        return !IsPlayMode() && !EditorApplication.isCompiling;
    }

    [MenuItem(MenuPath)]
    private static void Export()
    {
        if (IsPlayMode())
        {
            Report(
                "Detenido",
                "Sal de Play Mode antes de exportar la distribución."
            );
            return;
        }

        if (!TryGetOpenScene(out Scene scene, out string error))
        {
            Report("Detenido", error);
            return;
        }

        if (!TryResolveCamera(scene, out Camera gameplayCamera, out string cameraNote, out error))
        {
            Report("Detenido", error);
            return;
        }

        if (!TryResolveView(gameplayCamera, out MapView view, out error))
        {
            Report("Detenido", error);
            return;
        }

        if (!TryResolvePath(scene, out RouteData route, out error))
        {
            Report("Detenido", error);
            return;
        }

        LayoutDocument document = BuildDocument(
            scene,
            gameplayCamera,
            view,
            route,
            cameraNote
        );
        DiagramImage diagram = null;

        try
        {
            diagram = DiagramImage.Draw(document);
            string folder = GetOutputFolder();
            Directory.CreateDirectory(folder);
            string jsonPath = Path.Combine(folder, "level01_layout.json");
            string pngPath = Path.Combine(folder, "level01_layout.png");
            string notesPath = Path.Combine(folder, "level01_layout_notes.md");
            File.WriteAllText(
                jsonPath,
                JsonUtility.ToJson(document, true),
                new UTF8Encoding(false)
            );
            File.WriteAllBytes(pngPath, diagram.EncodePng());
            File.WriteAllText(
                notesPath,
                BuildNotes(document),
                new UTF8Encoding(false)
            );
            Report(
                "Distribución exportada",
                "La escena no se modificó ni se guardó.\n" + folder
            );
        }
        finally
        {
            if (diagram != null)
            {
                diagram.Release();
            }
        }
    }

    private static bool IsPlayMode()
    {
        return EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static bool TryGetOpenScene(out Scene scene, out string error)
    {
        scene = SceneManager.GetSceneByPath(ScenePath);
        error = null;

        if (!scene.IsValid() || !scene.isLoaded)
        {
            error =
                "Abre " + ScenePath + " antes de exportar. " +
                "No se cambió de escena.";
            return false;
        }

        return true;
    }

    private static bool TryResolveCamera(
        Scene scene,
        out Camera gameplayCamera,
        out string cameraNote,
        out string error)
    {
        gameplayCamera = null;
        cameraNote = string.Empty;
        error = null;
        List<Camera> baseCameras = new();
        List<string> overlays = new();
        List<string> disabled = new();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera[] cameras = root.GetComponentsInChildren<Camera>(true);

            foreach (Camera camera in cameras)
            {
                if (!camera.gameObject.activeInHierarchy || !camera.enabled)
                {
                    disabled.Add(camera.gameObject.name);
                    continue;
                }

                UniversalAdditionalCameraData data =
                    camera.GetComponent<UniversalAdditionalCameraData>();

                if (data != null &&
                    data.renderType == CameraRenderType.Overlay)
                {
                    overlays.Add(camera.gameObject.name);
                    continue;
                }

                baseCameras.Add(camera);
            }
        }

        if (baseCameras.Count != 1)
        {
            error = DescribeCameraAmbiguity(baseCameras, overlays, disabled);
            return false;
        }

        gameplayCamera = baseCameras[0];

        if (overlays.Count > 0 || disabled.Count > 0)
        {
            cameraNote = DescribeExtraCameras(overlays, disabled);
        }

        return true;
    }

    private static string DescribeCameraAmbiguity(
        List<Camera> baseCameras,
        List<string> overlays,
        List<string> disabled)
    {
        StringBuilder text = new();
        text.AppendLine(
            "No hay una sola cámara de gameplay en Level_01. " +
            "No se escribió ningún archivo."
        );
        text.AppendLine("Cámaras base activas: " + baseCameras.Count + ".");

        foreach (Camera camera in baseCameras)
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

    private static bool TryResolveView(
        Camera camera,
        out MapView view,
        out string error)
    {
        view = null;
        error = null;
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
                " no mira el plano XY en ortográfica, con arriba en +Y. " +
                "No se produjo un diagrama para no deformar el plano.\n" +
                "Ortográfica: " + camera.orthographic +
                ". Forward: " + forward.ToString("0.###") +
                ". Up: " + up.ToString("0.###") + ".";
            return false;
        }

        if (camera.orthographicSize <= WidthTolerance)
        {
            error =
                "La cámara tiene un tamaño ortográfico no válido. " +
                "No se escribió ningún archivo.";
            return false;
        }

        float aspect = ReferenceWidth / (float)ReferenceHeight;
        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * aspect;
        Vector3 position = camera.transform.position;
        view = new MapView
        {
            minX = position.x - halfWidth,
            maxX = position.x + halfWidth,
            minY = position.y - halfHeight,
            maxY = position.y + halfHeight,
            aspect = aspect
        };
        return true;
    }

    private static bool TryResolvePath(
        Scene scene,
        out RouteData route,
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
                "No se puede resolver la ruta.";
            return false;
        }

        EnemyPath path = null;

        foreach (EnemySpawner spawner in spawners)
        {
            EnemyPath candidate = ReadPath(spawner);

            if (candidate == null)
            {
                error =
                    spawner.gameObject.name +
                    " no tiene una ruta asignada. " +
                    "No se escribió ningún archivo.";
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
                    "No se escribió ningún archivo.\n- " +
                    path.gameObject.name + "\n- " +
                    candidate.gameObject.name;
                return false;
            }
        }

        if (path.Count == 0)
        {
            error =
                path.gameObject.name +
                " no tiene waypoints. No se escribió ningún archivo.";
            return false;
        }

        List<Transform> points = new();

        for (int index = 0; index < path.Count; index++)
        {
            Transform point = path.GetWaypoint(index);

            if (point == null)
            {
                error =
                    "El waypoint " + index +
                    " de la ruta está vacío. " +
                    "No se escribió ningún archivo.";
                return false;
            }

            points.Add(point);
        }

        route = new RouteData
        {
            spawners = spawners,
            path = path,
            waypoints = points
        };
        return true;
    }

    private static EnemyPath ReadPath(EnemySpawner spawner)
    {
        SerializedObject serialized = new(spawner);
        SerializedProperty property = serialized.FindProperty("enemyPath");

        if (property == null)
        {
            return null;
        }

        return property.objectReferenceValue as EnemyPath;
    }

    private static LayoutDocument BuildDocument(
        Scene scene,
        Camera camera,
        MapView view,
        RouteData route,
        string cameraNote)
    {
        LayoutDocument document = new()
        {
            scenePath = scene.path,
            referenceWidth = ReferenceWidth,
            referenceHeight = ReferenceHeight,
            camera = BuildCamera(camera, view),
            path = BuildPath(route, view),
            plots = BuildPlots(scene, view),
            heart = BuildHeart(scene, view),
            hud = BuildHud(scene),
            cameraNote = cameraNote
        };
        document.findings = BuildFindings(document, view, route);
        return document;
    }

    private static CameraRecord BuildCamera(Camera camera, MapView view)
    {
        Vector3 position = camera.transform.position;
        Quaternion rotation = camera.transform.rotation;
        Vector3 euler = camera.transform.eulerAngles;

        return new CameraRecord
        {
            name = camera.gameObject.name,
            positionX = position.x,
            positionY = position.y,
            positionZ = position.z,
            rotationX = rotation.x,
            rotationY = rotation.y,
            rotationZ = rotation.z,
            rotationW = rotation.w,
            eulerX = euler.x,
            eulerY = euler.y,
            eulerZ = euler.z,
            projection = "Orthographic",
            orthographicSize = camera.orthographicSize,
            referenceAspect = view.aspect,
            nearClip = camera.nearClipPlane,
            farClip = camera.farClipPlane,
            viewMinX = view.minX,
            viewMinY = view.minY,
            viewMaxX = view.maxX,
            viewMaxY = view.maxY,
            viewWidth = view.Width,
            viewHeight = view.Height
        };
    }

    private static PathRecord BuildPath(RouteData route, MapView view)
    {
        PathRecord record = new()
        {
            pathName = route.path.gameObject.name,
            spawnerNames = JoinNames(route.spawners),
            totalLength = 0f
        };
        Vector3 spawnerPosition = route.spawners[0].transform.position;
        record.spawnerX = spawnerPosition.x;
        record.spawnerY = spawnerPosition.y;
        record.spawnerZ = spawnerPosition.z;

        for (int index = 0; index < route.waypoints.Count; index++)
        {
            Transform point = route.waypoints[index];
            Vector3 world = point.position;
            Vector2 viewport = view.Viewport(world);
            record.waypoints.Add(new WaypointRecord
            {
                order = index,
                name = point.gameObject.name,
                worldX = world.x,
                worldY = world.y,
                worldZ = world.z,
                viewportX = viewport.x,
                viewportY = viewport.y
            });

            if (index == 0)
            {
                continue;
            }

            Vector3 previous = route.waypoints[index - 1].position;
            float length = Vector2.Distance(
                new Vector2(previous.x, previous.y),
                new Vector2(world.x, world.y)
            );
            record.totalLength += length;
            record.segments.Add(new SegmentRecord
            {
                from = index - 1,
                to = index,
                length = length
            });
        }

        return record;
    }

    private static List<PlotRecord> BuildPlots(Scene scene, MapView view)
    {
        List<BuildPlot> plots = new();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            plots.AddRange(root.GetComponentsInChildren<BuildPlot>(true));
        }

        plots.Sort((left, right) =>
            string.CompareOrdinal(
                left.gameObject.name,
                right.gameObject.name
            ));
        List<PlotRecord> records = new();

        foreach (BuildPlot plot in plots)
        {
            Vector3 position = plot.transform.position;
            PlotRecord record = new()
            {
                name = plot.gameObject.name,
                positionX = position.x,
                positionY = position.y,
                positionZ = position.z,
                rendererBounds = ReadRendererBounds(
                    ReadObject<SpriteRenderer>(plot, "plotRenderer"),
                    view
                ),
                colliderBounds = ReadColliderBounds(
                    ReadObject<Collider2D>(plot, "plotCollider"),
                    view
                )
            };
            records.Add(record);
        }

        return records;
    }

    private static HeartRecord BuildHeart(Scene scene, MapView view)
    {
        List<BastionHealth> hearts = new();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            hearts.AddRange(
                root.GetComponentsInChildren<BastionHealth>(true)
            );
        }

        HeartRecord record = new();

        if (hearts.Count != 1)
        {
            record.known = false;
            record.unknownReason = hearts.Count == 0
                ? "Level_01 no tiene BastionHealth."
                : "Hay " + hearts.Count +
                    " componentes BastionHealth: " +
                    JoinNames(hearts) + ".";
            return record;
        }

        BastionHealth heart = hearts[0];
        Vector3 position = heart.transform.position;
        record.known = true;
        record.name = heart.gameObject.name;
        record.positionX = position.x;
        record.positionY = position.y;
        record.positionZ = position.z;
        SpriteRenderer[] renderers =
            heart.GetComponentsInChildren<SpriteRenderer>(true);

        if (renderers.Length != 1)
        {
            record.visualKnown = false;
            record.visualUnknownReason = renderers.Length == 0
                ? "El corazón no tiene SpriteRenderer."
                : "El corazón tiene " + renderers.Length +
                    " SpriteRenderer. No se eligió uno.";
            return record;
        }

        record.visual = ReadRendererBounds(renderers[0], view);
        record.visualKnown = record.visual.known;
        record.visualUnknownReason = record.visual.unknownReason;
        return record;
    }

    private static HudRecord BuildHud(Scene scene)
    {
        HudRecord record = new();
        List<HUDController> controllers = new();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            controllers.AddRange(
                root.GetComponentsInChildren<HUDController>(true)
            );
        }

        record.excludedModals = DescribeExcludedModals(scene);

        if (controllers.Count != 1)
        {
            record.known = false;
            record.unknownReason = controllers.Count == 0
                ? "No hay HUDController."
                : "Hay " + controllers.Count +
                    " HUDController. No se eligió un HUD.";
            return record;
        }

        HUDController controller = controllers[0];
        Canvas canvas = controller.GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            record.known = false;
            record.unknownReason =
                "El HUD no está dentro de un Canvas.";
            return record;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        record.known = true;
        record.canvasName = canvas.gameObject.name;
        record.renderMode = canvas.renderMode.ToString();
        record.hudName = controller.gameObject.name;

        if (scaler == null)
        {
            record.scalerKnown = false;
            record.scalerUnknownReason = "El Canvas no tiene CanvasScaler.";
        }
        else
        {
            record.scalerKnown = true;
            record.scaleMode = scaler.uiScaleMode.ToString();
            record.scalerReferenceWidth = scaler.referenceResolution.x;
            record.scalerReferenceHeight = scaler.referenceResolution.y;
            record.matchWidthOrHeight = scaler.matchWidthOrHeight;
        }

        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            record.areasKnown = false;
            record.areasUnknownReason =
                "El Canvas no es Screen Space Overlay. " +
                "No se inventaron límites de pantalla.";
            return record;
        }

        if (!record.scalerKnown ||
            !Approximately(record.scalerReferenceWidth, ReferenceWidth) ||
            !Approximately(record.scalerReferenceHeight, ReferenceHeight))
        {
            record.areasKnown = false;
            record.areasUnknownReason =
                "La resolución del CanvasScaler no es 1920x1080. " +
                "No se reescaló el HUD por aproximación.";
            return record;
        }

        if (!CanMeasureRect(controller.transform as RectTransform, canvas))
        {
            record.areasKnown = false;
            record.areasUnknownReason =
                "La jerarquía del HUD tiene escala o rotación. " +
                "No se calcularon sus áreas.";
            return record;
        }

        record.areasKnown = true;
        HashSet<int> seen = new();
        AddReferencedHudArea(
            record,
            controller,
            canvas,
            seen,
            "bastionText"
        );
        AddReferencedHudArea(
            record,
            controller,
            canvas,
            seen,
            "resourcesText"
        );
        AddReferencedHudArea(
            record,
            controller,
            canvas,
            seen,
            "waveText"
        );
        AddReferencedHudArea(
            record,
            controller,
            canvas,
            seen,
            "enemiesText"
        );
        AddReferencedHudArea(
            record,
            controller,
            canvas,
            seen,
            "waveStateText"
        );
        AddReferencedHudArea(
            record,
            controller,
            canvas,
            seen,
            "statusText"
        );
        AddReferencedHudArea(
            record,
            controller,
            canvas,
            seen,
            "discoveriesText"
        );
        AddActiveHudGraphics(record, controller, canvas, seen);
        return record;
    }

    private static void AddReferencedHudArea(
        HudRecord record,
        HUDController controller,
        Canvas canvas,
        HashSet<int> seen,
        string fieldName)
    {
        Component component = ReadObject<Component>(controller, fieldName);

        if (component == null)
        {
            record.areas.Add(UnknownArea(
                fieldName,
                "La referencia " + fieldName + " está vacía."
            ));
            return;
        }

        AddHudArea(
            record,
            component.transform as RectTransform,
            canvas,
            seen,
            fieldName,
            component.gameObject
        );
    }

    private static void AddActiveHudGraphics(
        HudRecord record,
        HUDController controller,
        Canvas canvas,
        HashSet<int> seen)
    {
        Graphic[] graphics =
            controller.GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            if (!graphic.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (HasModalController(graphic.transform, controller.transform))
            {
                continue;
            }

            AddHudArea(
                record,
                graphic.rectTransform,
                canvas,
                seen,
                "grafico activo de " + controller.gameObject.name,
                graphic.gameObject
            );
        }
    }

    private static void AddHudArea(
        HudRecord record,
        RectTransform rect,
        Canvas canvas,
        HashSet<int> seen,
        string source,
        GameObject owner)
    {
        if (owner == null || !seen.Add(owner.GetInstanceID()))
        {
            return;
        }

        if (!owner.activeInHierarchy)
        {
            record.areas.Add(UnknownArea(
                owner.name,
                source + " está inactivo. No se activó para medirlo."
            ));
            return;
        }

        if (rect == null || !CanMeasureRect(rect, canvas))
        {
            record.areas.Add(UnknownArea(
                owner.name,
                source + " no tiene un rectángulo medible."
            ));
            return;
        }

        PixelRect pixels = MeasureRect(rect, canvas);
        record.areas.Add(new HudAreaRecord
        {
            name = owner.name,
            source = source,
            known = true,
            xMin = pixels.xMin,
            yMin = pixels.yMin,
            xMax = pixels.xMax,
            yMax = pixels.yMax
        });
    }

    private static HudAreaRecord UnknownArea(string name, string reason)
    {
        return new HudAreaRecord
        {
            name = name,
            known = false,
            unknownReason = reason
        };
    }

    private static bool HasModalController(
        Transform start,
        Transform hudRoot)
    {
        Transform current = start;

        while (current != null && current != hudRoot)
        {
            if (current.GetComponent<EducationalModalController>() != null ||
                current.GetComponent<CodexController>() != null ||
                current.GetComponent<EndGameUI>() != null)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static string DescribeExcludedModals(Scene scene)
    {
        List<string> names = new();
        CollectModalNames<EducationalModalController>(scene, names);
        CollectModalNames<CodexController>(scene, names);
        CollectModalNames<EndGameUI>(scene, names);

        if (names.Count == 0)
        {
            return "No se encontraron paneles modales.";
        }

        return string.Join(", ", names);
    }

    private static void CollectModalNames<T>(
        Scene scene,
        List<string> names)
        where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] components = root.GetComponentsInChildren<T>(true);

            foreach (T component in components)
            {
                string label =
                    component.GetType().Name + " en " +
                    component.gameObject.name;

                if (!names.Contains(label))
                {
                    names.Add(label);
                }
            }
        }
    }

    private static bool CanMeasureRect(RectTransform rect, Canvas canvas)
    {
        if (rect == null)
        {
            return false;
        }

        Transform current = rect;

        while (current != null && current != canvas.transform)
        {
            if (Quaternion.Angle(current.localRotation, Quaternion.identity) >
                0.1f)
            {
                return false;
            }

            Vector3 scale = current.localScale;

            if (Mathf.Abs(scale.x - 1f) > 0.001f ||
                Mathf.Abs(scale.y - 1f) > 0.001f)
            {
                return false;
            }

            current = current.parent;
        }

        return true;
    }

    private static PixelRect MeasureRect(RectTransform rect, Canvas canvas)
    {
        PixelRect parent = new()
        {
            xMin = 0f,
            yMin = 0f,
            xMax = ReferenceWidth,
            yMax = ReferenceHeight,
            pivotX = canvas.GetComponent<RectTransform>().pivot.x,
            pivotY = canvas.GetComponent<RectTransform>().pivot.y
        };
        List<RectTransform> chain = new();
        Transform current = rect;

        while (current != null && current != canvas.transform)
        {
            chain.Add(current as RectTransform);
            current = current.parent;
        }

        for (int index = chain.Count - 1; index >= 0; index--)
        {
            parent = ChildRect(chain[index], parent);
        }

        return parent;
    }

    private static PixelRect ChildRect(RectTransform rect, PixelRect parent)
    {
        Vector2 anchorMin = rect.anchorMin;
        Vector2 anchorMax = rect.anchorMax;
        Vector2 pivot = rect.pivot;
        Vector2 anchored = rect.anchoredPosition;
        Vector2 sizeDelta = rect.sizeDelta;
        float width = parent.Width * (anchorMax.x - anchorMin.x) + sizeDelta.x;
        float height = parent.Height * (anchorMax.y - anchorMin.y) + sizeDelta.y;
        float parentPivotX = parent.xMin + parent.Width * parent.pivotX;
        float parentPivotY = parent.yMin + parent.Height * parent.pivotY;
        float referenceX =
            parent.Width * (anchorMin.x + (anchorMax.x - anchorMin.x) * pivot.x) -
            parent.Width * parent.pivotX +
            anchored.x;
        float referenceY =
            parent.Height * (anchorMin.y + (anchorMax.y - anchorMin.y) * pivot.y) -
            parent.Height * parent.pivotY +
            anchored.y;
        float pivotX = parentPivotX + referenceX;
        float pivotY = parentPivotY + referenceY;

        return new PixelRect
        {
            xMin = pivotX - pivot.x * width,
            yMin = pivotY - pivot.y * height,
            xMax = pivotX + (1f - pivot.x) * width,
            yMax = pivotY + (1f - pivot.y) * height,
            pivotX = pivot.x,
            pivotY = pivot.y
        };
    }

    private static BoundsRecord ReadRendererBounds(
        SpriteRenderer renderer,
        MapView view)
    {
        if (renderer == null)
        {
            return UnknownBounds("No hay SpriteRenderer asignado.");
        }

        if (renderer.sprite == null)
        {
            return UnknownBounds(
                renderer.gameObject.name + " no tiene sprite."
            );
        }

        BoundsRecord record = BoundsFrom(renderer.bounds, view);
        record.visible =
            renderer.enabled && renderer.gameObject.activeInHierarchy;
        record.unknownReason = record.visible
            ? string.Empty
            : "El renderer existe, pero está inactivo.";
        return record;
    }

    private static BoundsRecord ReadColliderBounds(
        Collider2D collider,
        MapView view)
    {
        if (collider == null)
        {
            return UnknownBounds("No hay Collider2D asignado.");
        }

        BoundsRecord record = BoundsFrom(collider.bounds, view);
        record.visible =
            collider.enabled && collider.gameObject.activeInHierarchy;
        record.unknownReason = record.visible
            ? string.Empty
            : "El collider existe, pero está inactivo.";
        return record;
    }

    private static BoundsRecord BoundsFrom(Bounds bounds, MapView view)
    {
        if (bounds.size.x <= WidthTolerance ||
            bounds.size.y <= WidthTolerance)
        {
            return UnknownBounds("Los límites tienen tamaño cero.");
        }

        Vector2 minViewport = view.Viewport(new Vector3(
            bounds.min.x,
            bounds.min.y,
            0f
        ));
        Vector2 maxViewport = view.Viewport(new Vector3(
            bounds.max.x,
            bounds.max.y,
            0f
        ));

        return new BoundsRecord
        {
            known = true,
            visible = true,
            minX = bounds.min.x,
            minY = bounds.min.y,
            maxX = bounds.max.x,
            maxY = bounds.max.y,
            viewportMinX = Mathf.Min(minViewport.x, maxViewport.x),
            viewportMinY = Mathf.Min(minViewport.y, maxViewport.y),
            viewportMaxX = Mathf.Max(minViewport.x, maxViewport.x),
            viewportMaxY = Mathf.Max(minViewport.y, maxViewport.y)
        };
    }

    private static BoundsRecord UnknownBounds(string reason)
    {
        return new BoundsRecord
        {
            known = false,
            unknownReason = reason
        };
    }

    private static FindingsRecord BuildFindings(
        LayoutDocument document,
        MapView view,
        RouteData route)
    {
        FindingsRecord findings = new();
        AddOutsideWaypoint(findings, document.path);
        AddOutsideBounds(findings, "Parcela", document.plots);
        AddOutsideHeart(findings, document);
        AddOutsideHud(findings, document.hud);
        AddRouteOverlaps(findings, document, route);
        AddActorOverlaps(findings, document);
        AddHudOverlaps(findings, document, view);
        AddUnknowns(findings, document);
        return findings;
    }

    private static void AddOutsideWaypoint(
        FindingsRecord findings,
        PathRecord path)
    {
        foreach (WaypointRecord point in path.waypoints)
        {
            string coverage = Coverage(
                point.viewportX,
                point.viewportY,
                point.viewportX,
                point.viewportY
            );

            if (coverage != "dentro")
            {
                findings.outsideFrame.Add(
                    point.name + " queda " + coverage + " del encuadre."
                );
            }
        }
    }

    private static void AddOutsideBounds(
        FindingsRecord findings,
        string label,
        List<PlotRecord> plots)
    {
        foreach (PlotRecord plot in plots)
        {
            AddOutsideBound(
                findings,
                label + " " + plot.name + " renderer",
                plot.rendererBounds
            );
            AddOutsideBound(
                findings,
                label + " " + plot.name + " collider",
                plot.colliderBounds
            );
        }
    }

    private static void AddOutsideHeart(
        FindingsRecord findings,
        LayoutDocument document)
    {
        HeartRecord heart = document.heart;

        if (!heart.known)
        {
            return;
        }

        if (heart.visualKnown && heart.visual.known)
        {
            AddOutsideBound(findings, "Corazón visual", heart.visual);
            return;
        }

        float width = document.camera.viewWidth;
        float height = document.camera.viewHeight;

        if (width <= WidthTolerance || height <= WidthTolerance)
        {
            return;
        }

        float viewportX = (heart.positionX - document.camera.viewMinX) / width;
        float viewportY = (heart.positionY - document.camera.viewMinY) / height;
        string coverage = Coverage(
            viewportX,
            viewportY,
            viewportX,
            viewportY
        );

        if (coverage != "dentro")
        {
            findings.outsideFrame.Add(
                "La posición del corazón queda " + coverage +
                " del encuadre."
            );
        }
    }

    private static void AddOutsideBound(
        FindingsRecord findings,
        string label,
        BoundsRecord bounds)
    {
        if (bounds == null || !bounds.known)
        {
            return;
        }

        string coverage = Coverage(
            bounds.viewportMinX,
            bounds.viewportMinY,
            bounds.viewportMaxX,
            bounds.viewportMaxY
        );

        if (coverage != "dentro")
        {
            findings.outsideFrame.Add(
                label + " queda " + coverage + " del encuadre."
            );
        }
    }

    private static void AddOutsideHud(
        FindingsRecord findings,
        HudRecord hud)
    {
        if (!hud.areasKnown)
        {
            return;
        }

        foreach (HudAreaRecord area in hud.areas)
        {
            if (!area.known)
            {
                continue;
            }

            bool outside =
                area.xMin < -0.5f ||
                area.yMin < -0.5f ||
                area.xMax > ReferenceWidth + 0.5f ||
                area.yMax > ReferenceHeight + 0.5f;
            bool complete =
                area.xMax < 0f ||
                area.yMax < 0f ||
                area.xMin > ReferenceWidth ||
                area.yMin > ReferenceHeight;

            if (complete)
            {
                findings.outsideFrame.Add(
                    "HUD " + area.name +
                    " queda completamente fuera de 1920x1080."
                );
            }
            else if (outside)
            {
                findings.outsideFrame.Add(
                    "HUD " + area.name +
                    " queda parcialmente fuera de 1920x1080."
                );
            }
        }
    }

    private static void AddRouteOverlaps(
        FindingsRecord findings,
        LayoutDocument document,
        RouteData route)
    {
        for (int index = 1; index < route.waypoints.Count; index++)
        {
            Vector2 start = Flat(route.waypoints[index - 1].position);
            Vector2 end = Flat(route.waypoints[index].position);
            string segment = "Segmento " + (index - 1) + "-" + index;

            foreach (PlotRecord plot in document.plots)
            {
                if (Hits(start, end, plot.rendererBounds))
                {
                    findings.overlaps.Add(
                        segment + " cruza el renderer de " + plot.name + "."
                    );
                }

                if (Hits(start, end, plot.colliderBounds))
                {
                    findings.overlaps.Add(
                        segment + " cruza el collider de " + plot.name + "."
                    );
                }
            }

            if (document.heart.visualKnown &&
                Hits(start, end, document.heart.visual))
            {
                findings.overlaps.Add(
                    segment + " cruza el corazón visual."
                );
            }
        }
    }

    private static void AddActorOverlaps(
        FindingsRecord findings,
        LayoutDocument document)
    {
        if (!document.heart.visualKnown || !document.heart.visual.known)
        {
            return;
        }

        foreach (PlotRecord plot in document.plots)
        {
            if (Intersects(plot.rendererBounds, document.heart.visual))
            {
                findings.overlaps.Add(
                    plot.name + " solapa su renderer con el corazón."
                );
            }

            if (Intersects(plot.colliderBounds, document.heart.visual))
            {
                findings.overlaps.Add(
                    plot.name + " solapa su collider con el corazón."
                );
            }
        }
    }

    private static void AddHudOverlaps(
        FindingsRecord findings,
        LayoutDocument document,
        MapView view)
    {
        if (!document.hud.areasKnown)
        {
            return;
        }

        foreach (HudAreaRecord area in document.hud.areas)
        {
            if (!area.known)
            {
                continue;
            }

            BoundsRecord hudWorld = HudToWorld(area, view);

            for (int index = 1; index < document.path.waypoints.Count; index++)
            {
                WaypointRecord previous = document.path.waypoints[index - 1];
                WaypointRecord current = document.path.waypoints[index];

                if (Hits(
                        new Vector2(previous.worldX, previous.worldY),
                        new Vector2(current.worldX, current.worldY),
                        hudWorld))
                {
                    findings.overlaps.Add(
                        "HUD " + area.name + " cubre el segmento " +
                        (index - 1) + "-" + index + "."
                    );
                }
            }

            foreach (PlotRecord plot in document.plots)
            {
                if (Intersects(hudWorld, plot.rendererBounds))
                {
                    findings.overlaps.Add(
                        "HUD " + area.name + " cubre el renderer de " +
                        plot.name + "."
                    );
                }
            }

            if (document.heart.visualKnown &&
                Intersects(hudWorld, document.heart.visual))
            {
                findings.overlaps.Add(
                    "HUD " + area.name + " cubre el corazón visual."
                );
            }
        }
    }

    private static BoundsRecord HudToWorld(HudAreaRecord area, MapView view)
    {
        Vector2 min = view.World(area.xMin, area.yMin);
        Vector2 max = view.World(area.xMax, area.yMax);

        return new BoundsRecord
        {
            known = true,
            visible = true,
            minX = Mathf.Min(min.x, max.x),
            minY = Mathf.Min(min.y, max.y),
            maxX = Mathf.Max(min.x, max.x),
            maxY = Mathf.Max(min.y, max.y)
        };
    }

    private static void AddUnknowns(
        FindingsRecord findings,
        LayoutDocument document)
    {
        if (!document.heart.known)
        {
            findings.unknown.Add(document.heart.unknownReason);
        }
        else if (!document.heart.visualKnown)
        {
            findings.unknown.Add(document.heart.visualUnknownReason);
        }

        foreach (PlotRecord plot in document.plots)
        {
            if (!plot.rendererBounds.known)
            {
                findings.unknown.Add(
                    plot.name + " renderer: " +
                    plot.rendererBounds.unknownReason
                );
            }

            if (!plot.colliderBounds.known)
            {
                findings.unknown.Add(
                    plot.name + " collider: " +
                    plot.colliderBounds.unknownReason
                );
            }
        }

        if (!document.hud.known)
        {
            findings.unknown.Add(document.hud.unknownReason);
        }
        else if (!document.hud.areasKnown)
        {
            findings.unknown.Add(document.hud.areasUnknownReason);
        }

        foreach (HudAreaRecord area in document.hud.areas)
        {
            if (!area.known)
            {
                findings.unknown.Add(area.name + ": " + area.unknownReason);
            }
        }
    }

    private static string BuildNotes(LayoutDocument document)
    {
        StringBuilder notes = new();
        notes.AppendLine("# Distribución de Level_01");
        notes.AppendLine();
        notes.AppendLine("## Dimensiones visibles");
        notes.AppendLine();
        notes.AppendLine(
            "- Encuadre de referencia: " + ReferenceWidth + "×" +
            ReferenceHeight + "."
        );
        notes.AppendLine(
            "- Ancho visible: " +
            Format(document.camera.viewWidth) + " unidades."
        );
        notes.AppendLine(
            "- Alto visible: " +
            Format(document.camera.viewHeight) + " unidades."
        );
        notes.AppendLine(
            "- Origen del encuadre: (" +
            Format(document.camera.viewMinX) + ", " +
            Format(document.camera.viewMinY) + ")."
        );
        notes.AppendLine(
            "- Extremo opuesto: (" +
            Format(document.camera.viewMaxX) + ", " +
            Format(document.camera.viewMaxY) + ")."
        );
        notes.AppendLine(
            "- Longitud de la ruta en XY: " +
            Format(document.path.totalLength) + " unidades, en " +
            document.path.segments.Count + " segmentos."
        );
        notes.AppendLine();
        notes.AppendLine("## Elementos fuera del encuadre");
        notes.AppendLine();
        AppendLines(
            notes,
            document.findings.outsideFrame,
            "Ningún waypoint, parcela, corazón o HUD medido queda fuera."
        );
        notes.AppendLine();
        notes.AppendLine("## Posibles solapamientos");
        notes.AppendLine();
        AppendLines(
            notes,
            document.findings.overlaps,
            "No se detectaron cruces entre la ruta, las parcelas, el corazón y el HUD medido."
        );
        notes.AppendLine();
        notes.AppendLine("## Limitaciones");
        notes.AppendLine();
        notes.AppendLine(
            "- El PNG es un plano calculado sobre XY. No es una captura de Scene View ni arte del juego."
        );
        notes.AppendLine(
            "- El encuadre usa 1920×1080, aunque Game View tenga otro tamaño."
        );
        notes.AppendLine(
            "- Los waypoints siguen el arreglo de EnemyPath referenciado por EnemySpawner, no el orden alfabético."
        );
        notes.AppendLine(
            "- La longitud ignora Z. Las coordenadas Z quedan en el JSON."
        );
        notes.AppendLine(
            "- El spawner está en (" +
            Format(document.path.spawnerX) + ", " +
            Format(document.path.spawnerY) +
            "). Ese punto de aparición no forma un segmento de EnemyPath."
        );
        notes.AppendLine(
            "- Los límites visuales son el AABB del SpriteRenderer, no el contorno opaco."
        );
        notes.AppendLine(
            "- El HUD se midió por anclas sobre la resolución de referencia. No se activó ni se desactivó interfaz."
        );
        notes.AppendLine(
            "- Paneles excluidos del HUD persistente: " +
            document.hud.excludedModals + "."
        );

        if (!string.IsNullOrEmpty(document.cameraNote))
        {
            notes.AppendLine("- " + document.cameraNote.Replace("\n", " "));
        }

        notes.AppendLine(
            "- La leyenda tapa la esquina inferior izquierda del PNG. Esa coordenada también está en el JSON."
        );
        notes.AppendLine(
            "- La escena no se modificó ni se guardó al exportar."
        );
        notes.AppendLine();
        notes.AppendLine("## Datos que no pudieron obtenerse");
        notes.AppendLine();
        AppendLines(
            notes,
            document.findings.unknown,
            "Se obtuvieron cámara, ruta, parcelas, corazón y HUD persistente."
        );
        notes.AppendLine();
        return notes.ToString();
    }

    private static string Coverage(
        float minX,
        float minY,
        float maxX,
        float maxY)
    {
        bool minInside = Inside(minX, minY);
        bool maxInside = Inside(maxX, maxY);

        if (minInside && maxInside)
        {
            return "dentro";
        }

        if (!minInside && !maxInside && !OverlapsUnitSquare(minX, minY, maxX, maxY))
        {
            return "completamente fuera";
        }

        return "parcialmente fuera";
    }

    private static bool Inside(float x, float y)
    {
        return x >= -WidthTolerance &&
            y >= -WidthTolerance &&
            x <= 1f + WidthTolerance &&
            y <= 1f + WidthTolerance;
    }

    private static bool OverlapsUnitSquare(
        float minX,
        float minY,
        float maxX,
        float maxY)
    {
        return maxX >= 0f && minX <= 1f && maxY >= 0f && minY <= 1f;
    }

    private static bool Hits(
        Vector2 start,
        Vector2 end,
        BoundsRecord bounds)
    {
        if (bounds == null || !bounds.known || !bounds.visible)
        {
            return false;
        }

        float t0 = 0f;
        float t1 = 1f;
        float dx = end.x - start.x;
        float dy = end.y - start.y;
        return Clip(dx, bounds.minX - start.x, ref t0, ref t1) &&
            Clip(-dx, start.x - bounds.maxX, ref t0, ref t1) &&
            Clip(dy, bounds.minY - start.y, ref t0, ref t1) &&
            Clip(-dy, start.y - bounds.maxY, ref t0, ref t1);
    }

    private static bool Clip(float direction, float distance, ref float t0, ref float t1)
    {
        if (Mathf.Abs(direction) <= 0.0000001f)
        {
            return distance >= 0f;
        }

        float ratio = distance / direction;

        if (direction < 0f)
        {
            if (ratio > t1)
            {
                return false;
            }

            if (ratio > t0)
            {
                t0 = ratio;
            }
        }
        else
        {
            if (ratio < t0)
            {
                return false;
            }

            if (ratio < t1)
            {
                t1 = ratio;
            }
        }

        return true;
    }

    private static bool Intersects(BoundsRecord left, BoundsRecord right)
    {
        if (left == null || right == null ||
            !left.known || !left.visible ||
            !right.known || !right.visible)
        {
            return false;
        }

        return left.maxX >= right.minX &&
            left.minX <= right.maxX &&
            left.maxY >= right.minY &&
            left.minY <= right.maxY;
    }

    private static T ReadObject<T>(UnityEngine.Object owner, string fieldName)
        where T : UnityEngine.Object
    {
        SerializedObject serialized = new(owner);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            return null;
        }

        return property.objectReferenceValue as T;
    }

    private static string GetOutputFolder()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folder = Path.Combine(
            projectRoot,
            "Documentation",
            "Level01Layout"
        );

        if (folder.StartsWith(Application.dataPath))
        {
            throw new IOException(
                "La salida no puede quedar dentro de Assets."
            );
        }

        return folder;
    }

    private static void AppendLines(
        StringBuilder notes,
        List<string> lines,
        string empty)
    {
        if (lines == null || lines.Count == 0)
        {
            notes.AppendLine("- " + empty);
            return;
        }

        foreach (string line in lines)
        {
            notes.AppendLine("- " + line);
        }
    }

    private static void AppendNames(
        StringBuilder text,
        string label,
        List<string> names)
    {
        if (names.Count == 0)
        {
            return;
        }

        text.AppendLine(label + ":");

        foreach (string name in names)
        {
            text.AppendLine("- " + name);
        }
    }

    private static string JoinNames<T>(List<T> components)
        where T : Component
    {
        List<string> names = new();

        foreach (T component in components)
        {
            names.Add(component.gameObject.name);
        }

        return string.Join(", ", names);
    }

    private static Vector2 Flat(Vector3 point)
    {
        return new Vector2(point.x, point.y);
    }

    private static string Format(float value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static bool Approximately(float value, float target)
    {
        return Mathf.Abs(value - target) <= 0.01f;
    }

    private static void Report(string title, string message)
    {
        Debug.Log(title + "\n" + message);
        EditorUtility.DisplayDialog(title, message, "Cerrar");
    }

    private sealed class MapView
    {
        public float minX;
        public float maxX;
        public float minY;
        public float maxY;
        public float aspect;

        public float Width => maxX - minX;
        public float Height => maxY - minY;

        public Vector2 Viewport(Vector3 world)
        {
            return new Vector2(
                (world.x - minX) / Width,
                (world.y - minY) / Height
            );
        }

        public Vector2 World(float pixelX, float pixelY)
        {
            return new Vector2(
                Mathf.Lerp(minX, maxX, pixelX / ReferenceWidth),
                Mathf.Lerp(minY, maxY, pixelY / ReferenceHeight)
            );
        }
    }

    private sealed class RouteData
    {
        public List<EnemySpawner> spawners;
        public EnemyPath path;
        public List<Transform> waypoints;
    }

    private struct PixelRect
    {
        public float xMin;
        public float yMin;
        public float xMax;
        public float yMax;
        public float pivotX;
        public float pivotY;
        public float Width => xMax - xMin;
        public float Height => yMax - yMin;
    }
}

[Serializable]
public class LayoutDocument
{
    public string scenePath;
    public int referenceWidth;
    public int referenceHeight;
    public CameraRecord camera;
    public PathRecord path;
    public List<PlotRecord> plots = new();
    public HeartRecord heart;
    public HudRecord hud;
    public string cameraNote;
    public FindingsRecord findings = new();
}

[Serializable]
public class CameraRecord
{
    public string name;
    public float positionX;
    public float positionY;
    public float positionZ;
    public float rotationX;
    public float rotationY;
    public float rotationZ;
    public float rotationW;
    public float eulerX;
    public float eulerY;
    public float eulerZ;
    public string projection;
    public float orthographicSize;
    public float referenceAspect;
    public float nearClip;
    public float farClip;
    public float viewMinX;
    public float viewMinY;
    public float viewMaxX;
    public float viewMaxY;
    public float viewWidth;
    public float viewHeight;
}

[Serializable]
public class PathRecord
{
    public string pathName;
    public string spawnerNames;
    public float spawnerX;
    public float spawnerY;
    public float spawnerZ;
    public float totalLength;
    public List<WaypointRecord> waypoints = new();
    public List<SegmentRecord> segments = new();
}

[Serializable]
public class WaypointRecord
{
    public int order;
    public string name;
    public float worldX;
    public float worldY;
    public float worldZ;
    public float viewportX;
    public float viewportY;
}

[Serializable]
public class SegmentRecord
{
    public int from;
    public int to;
    public float length;
}

[Serializable]
public class PlotRecord
{
    public string name;
    public float positionX;
    public float positionY;
    public float positionZ;
    public BoundsRecord rendererBounds;
    public BoundsRecord colliderBounds;
}

[Serializable]
public class HeartRecord
{
    public bool known;
    public string name;
    public string unknownReason;
    public float positionX;
    public float positionY;
    public float positionZ;
    public bool visualKnown;
    public string visualUnknownReason;
    public BoundsRecord visual;
}

[Serializable]
public class BoundsRecord
{
    public bool known;
    public bool visible;
    public string unknownReason;
    public float minX;
    public float minY;
    public float maxX;
    public float maxY;
    public float viewportMinX;
    public float viewportMinY;
    public float viewportMaxX;
    public float viewportMaxY;
}

[Serializable]
public class HudRecord
{
    public bool known;
    public string unknownReason;
    public string canvasName;
    public string renderMode;
    public string hudName;
    public bool scalerKnown;
    public string scalerUnknownReason;
    public string scaleMode;
    public float scalerReferenceWidth;
    public float scalerReferenceHeight;
    public float matchWidthOrHeight;
    public bool areasKnown;
    public string areasUnknownReason;
    public string excludedModals;
    public List<HudAreaRecord> areas = new();
}

[Serializable]
public class HudAreaRecord
{
    public string name;
    public string source;
    public bool known;
    public string unknownReason;
    public float xMin;
    public float yMin;
    public float xMax;
    public float yMax;
}

[Serializable]
public class FindingsRecord
{
    public List<string> outsideFrame = new();
    public List<string> overlaps = new();
    public List<string> unknown = new();
}

public sealed class DiagramImage
{
    private readonly Color32[] pixels;
    private Texture2D texture;

    private DiagramImage()
    {
        pixels = new Color32[1920 * 1080];
        texture = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
    }

    public static DiagramImage Draw(LayoutDocument document)
    {
        DiagramImage image = new();

        try
        {
            image.Paint(document);
            return image;
        }
        catch
        {
            image.Release();
            throw;
        }
    }

    private void Paint(LayoutDocument document)
    {
        Fill(new Color32(22, 30, 42, 255));
        DrawGrid(document);
        DrawFrame();

        foreach (PlotRecord plot in document.plots)
        {
            DrawWorldBox(
                document,
                plot.colliderBounds,
                new Color32(210, 170, 60, 40),
                new Color32(230, 190, 70, 255)
            );
            DrawWorldBox(
                document,
                plot.rendererBounds,
                new Color32(70, 170, 95, 70),
                new Color32(110, 220, 140, 255)
            );
            LabelWorld(
                document,
                plot.positionX,
                plot.positionY,
                plot.name
            );
        }

        if (document.heart.visualKnown)
        {
            DrawWorldBox(
                document,
                document.heart.visual,
                new Color32(190, 60, 70, 80),
                new Color32(240, 90, 90, 255)
            );
        }

        if (document.heart.known)
        {
            LabelWorld(
                document,
                document.heart.positionX,
                document.heart.positionY,
                "CORAZON"
            );
        }

        DrawRoute(document);

        if (document.hud.areasKnown)
        {
            foreach (HudAreaRecord area in document.hud.areas)
            {
                if (!area.known)
                {
                    continue;
                }

                FillRect(
                    Mathf.RoundToInt(area.xMin),
                    Mathf.RoundToInt(area.yMin),
                    Mathf.RoundToInt(area.xMax),
                    Mathf.RoundToInt(area.yMax),
                    new Color32(70, 130, 210, 55)
                );
                StrokeRect(
                    Mathf.RoundToInt(area.xMin),
                    Mathf.RoundToInt(area.yMin),
                    Mathf.RoundToInt(area.xMax),
                    Mathf.RoundToInt(area.yMax),
                    new Color32(150, 190, 245, 255)
                );
                Text(
                    Mathf.RoundToInt(area.xMin) + 4,
                    Mathf.RoundToInt(area.yMax) - 16,
                    area.name,
                    new Color32(230, 240, 255, 255),
                    1
                );
            }
        }

        DrawLegend(document);
    }

    public byte[] EncodePng()
    {
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture.EncodeToPNG();
    }

    public void Release()
    {
        if (texture == null)
        {
            return;
        }

        UnityEngine.Object.DestroyImmediate(texture);
        texture = null;
    }

    private void DrawGrid(LayoutDocument document)
    {
        CameraRecord camera = document.camera;
        Color32 line = new(42, 54, 72, 255);
        Color32 label = new(150, 165, 185, 255);
        int firstX = Mathf.CeilToInt(camera.viewMinX);
        int lastX = Mathf.FloorToInt(camera.viewMaxX);

        for (int x = firstX; x <= lastX; x++)
        {
            int pixel = WorldX(document, x);
            Vertical(pixel, line);

            if (x % 2 == 0)
            {
                Text(pixel + 3, 1080 - 18, x.ToString(), label, 1);
            }
        }

        int firstY = Mathf.CeilToInt(camera.viewMinY);
        int lastY = Mathf.FloorToInt(camera.viewMaxY);

        for (int y = firstY; y <= lastY; y++)
        {
            int pixel = WorldY(document, y);
            Horizontal(pixel, line);

            if (y % 2 == 0)
            {
                Text(8, pixel + 3, y.ToString(), label, 1);
            }
        }
    }

    private void DrawFrame()
    {
        StrokeRect(0, 0, 1919, 1079, new Color32(235, 235, 235, 255));
    }

    private void DrawRoute(LayoutDocument document)
    {
        Color32 path = new(70, 215, 220, 255);
        Color32 point = new(255, 210, 80, 255);
        List<WaypointRecord> points = document.path.waypoints;

        for (int index = 1; index < points.Count; index++)
        {
            DrawLine(
                WorldX(document, points[index - 1].worldX),
                WorldY(document, points[index - 1].worldY),
                WorldX(document, points[index].worldX),
                WorldY(document, points[index].worldY),
                path
            );
        }

        foreach (WaypointRecord waypoint in points)
        {
            int x = WorldX(document, waypoint.worldX);
            int y = WorldY(document, waypoint.worldY);
            FillCircle(x, y, 8, point);
            Text(
                x + 10,
                y + 8,
                (waypoint.order + 1).ToString(),
                new Color32(255, 245, 210, 255),
                2
            );
        }
    }

    private void DrawLegend(LayoutDocument document)
    {
        FillRect(12, 12, 470, 168, new Color32(12, 16, 24, 230));
        StrokeRect(12, 12, 470, 168, new Color32(200, 205, 215, 255));
        Color32 ink = new(235, 235, 235, 255);
        Text(24, 146, "PLANO XY  1920X1080", ink, 2);
        Text(24, 124, "CIAN RUTA   NUMERO = ORDEN", ink, 1);
        Text(24, 108, "VERDE PARCELA   ORO COLLIDER", ink, 1);
        Text(24, 92, "ROJO CORAZON   AZUL HUD", ink, 1);
        Text(
            24,
            74,
            "ANCHO " + document.camera.viewWidth.ToString(
                "0.##",
                CultureInfo.InvariantCulture
            ) +
            "  ALTO " + document.camera.viewHeight.ToString(
                "0.##",
                CultureInfo.InvariantCulture
            ),
            ink,
            1
        );
        int bar = Mathf.Clamp(
            Mathf.RoundToInt(1920f / document.camera.viewWidth),
            8,
            180
        );
        HorizontalSpan(24, 24 + bar, 48, new Color32(255, 220, 120, 255));
        Text(24 + bar + 8, 42, "1 UNIDAD", ink, 1);
        Text(
            24,
            24,
            "INF IZQ " + document.camera.viewMinX.ToString(
                "0.##",
                CultureInfo.InvariantCulture
            ) +
            " " + document.camera.viewMinY.ToString(
                "0.##",
                CultureInfo.InvariantCulture
            ),
            ink,
            1
        );
    }

    private void DrawWorldBox(
        LayoutDocument document,
        BoundsRecord bounds,
        Color32 fill,
        Color32 line)
    {
        if (bounds == null || !bounds.known || !bounds.visible)
        {
            return;
        }

        int x0 = WorldX(document, bounds.minX);
        int y0 = WorldY(document, bounds.minY);
        int x1 = WorldX(document, bounds.maxX);
        int y1 = WorldY(document, bounds.maxY);
        FillRect(x0, y0, x1, y1, fill);
        StrokeRect(x0, y0, x1, y1, line);
    }

    private void LabelWorld(
        LayoutDocument document,
        float x,
        float y,
        string label)
    {
        Text(
            WorldX(document, x) + 6,
            WorldY(document, y) + 6,
            label,
            new Color32(255, 255, 255, 255),
            1
        );
    }

    private static int WorldX(LayoutDocument document, float worldX)
    {
        CameraRecord camera = document.camera;
        return Mathf.RoundToInt(
            (worldX - camera.viewMinX) / camera.viewWidth * 1919f
        );
    }

    private static int WorldY(LayoutDocument document, float worldY)
    {
        CameraRecord camera = document.camera;
        return Mathf.RoundToInt(
            (worldY - camera.viewMinY) / camera.viewHeight * 1079f
        );
    }

    private void Fill(Color32 color)
    {
        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = color;
        }
    }

    private void FillRect(int x0, int y0, int x1, int y1, Color32 color)
    {
        Order(ref x0, ref x1);
        Order(ref y0, ref y1);

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                Plot(x, y, color);
            }
        }
    }

    private void StrokeRect(int x0, int y0, int x1, int y1, Color32 color)
    {
        Order(ref x0, ref x1);
        Order(ref y0, ref y1);
        HorizontalSpan(x0, x1, y0, color);
        HorizontalSpan(x0, x1, y1, color);
        VerticalSpan(y0, y1, x0, color);
        VerticalSpan(y0, y1, x1, color);
    }

    private void Horizontal(int y, Color32 color)
    {
        HorizontalSpan(0, 1919, y, color);
    }

    private void Vertical(int x, Color32 color)
    {
        VerticalSpan(0, 1079, x, color);
    }

    private void HorizontalSpan(int x0, int x1, int y, Color32 color)
    {
        for (int x = x0; x <= x1; x++)
        {
            Plot(x, y, color);
        }
    }

    private void VerticalSpan(int y0, int y1, int x, Color32 color)
    {
        for (int y = y0; y <= y1; y++)
        {
            Plot(x, y, color);
        }
    }

    private void DrawLine(int x0, int y0, int x1, int y1, Color32 color)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int error = dx - dy;

        while (true)
        {
            Plot(x0, y0, color);
            Plot(x0 + 1, y0, color);
            Plot(x0, y0 + 1, color);

            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int doubled = error * 2;

            if (doubled > -dy)
            {
                error -= dy;
                x0 += sx;
            }

            if (doubled < dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private void FillCircle(int centerX, int centerY, int radius, Color32 color)
    {
        int squared = radius * radius;

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= squared)
                {
                    Plot(centerX + x, centerY + y, color);
                }
            }
        }
    }

    private void Text(int x, int y, string value, Color32 color, int scale)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        value = value.Replace('_', ' ');
        int cursor = x;

        foreach (char character in value.ToUpperInvariant())
        {
            DiagramGlyphs.Draw(this, cursor, y, character, color, scale);
            cursor += 6 * scale;
        }
    }

    private void Plot(int x, int y, Color32 color)
    {
        if ((uint)x >= 1920 || (uint)y >= 1080)
        {
            return;
        }

        int index = y * 1920 + x;

        if (color.a >= 250)
        {
            pixels[index] = color;
            return;
        }

        Color32 destination = pixels[index];
        float alpha = color.a / 255f;
        float inverse = 1f - alpha;
        pixels[index] = new Color32(
            (byte)(color.r * alpha + destination.r * inverse),
            (byte)(color.g * alpha + destination.g * inverse),
            (byte)(color.b * alpha + destination.b * inverse),
            255
        );
    }

    private static void Order(ref int min, ref int max)
    {
        if (min <= max)
        {
            return;
        }

        int swap = min;
        min = max;
        max = swap;
    }

    internal void PlotGlyph(int x, int y, Color32 color)
    {
        Plot(x, y, color);
    }
}

public static class DiagramGlyphs
{
    private const string Letters = " ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-";

    private static readonly string[] Rows =
    {
        ".....", ".....", ".....", ".....", ".....", ".....", ".....",
        ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#",
        "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####.",
        ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###.",
        "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####.",
        "#####", "#....", "#....", "####.", "#....", "#....", "#####",
        "#####", "#....", "#....", "####.", "#....", "#....", "#....",
        ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###.",
        "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#",
        "..#..", "..#..", "..#..", "..#..", "..#..", "..#..", "..#..",
        "...#.", "...#.", "...#.", "...#.", "#..#.", "#..#.", ".##..",
        "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#",
        "#....", "#....", "#....", "#....", "#....", "#....", "#####",
        "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#",
        "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#",
        ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.",
        "####.", "#...#", "#...#", "####.", "#....", "#....", "#....",
        ".###.", "#...#", "#...#", "#.#.#", "#..#.", ".##.#", ".###.",
        "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#",
        ".###.", "#...#", "#....", ".###.", "....#", "#...#", ".###.",
        "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#..",
        "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.",
        "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#..",
        "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#",
        "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#",
        "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#..",
        "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####",
        ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###.",
        "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###.",
        ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####",
        ".###.", "#...#", "....#", "..##.", "....#", "#...#", ".###.",
        "#..#.", "#..#.", "#..#.", "#####", "...#.", "...#.", "...#.",
        "#####", "#....", "####.", "....#", "....#", "#...#", ".###.",
        ".##..", "#....", "#....", "####.", "#...#", "#...#", ".###.",
        "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#...",
        ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###.",
        ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###.",
        ".....", ".....", ".....", ".....", ".....", ".#...", ".#...",
        ".....", ".....", ".....", "#####", ".....", ".....", "....."
    };

    public static void Draw(
        DiagramImage image,
        int x,
        int y,
        char character,
        Color32 color,
        int scale)
    {
        int index = Letters.IndexOf(character);

        if (index < 0)
        {
            return;
        }

        for (int row = 0; row < 7; row++)
        {
            string bits = Rows[index * 7 + row];

            for (int column = 0; column < 5; column++)
            {
                if (bits[column] != '#')
                {
                    continue;
                }

                for (int sy = 0; sy < scale; sy++)
                {
                    for (int sx = 0; sx < scale; sx++)
                    {
                        image.PlotGlyph(
                            x + column * scale + sx,
                            y + (6 - row) * scale + sy,
                            color
                        );
                    }
                }
            }
        }
    }
}

