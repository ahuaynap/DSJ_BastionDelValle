using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ValleyFinishConfigurator
{
    private const string ApplyMenu =
        "Bastion del Valle/Incrementos/19 - Aplicar Acabado Visual";

    private const string RegenerateMenu =
        "Bastion del Valle/Incrementos/19 - Regenerar Decoracion";

    private const string ScenePath = "Assets/Scenes/Level_01.unity";
    private const string UndoName = "Aplicar Acabado Visual";
    private const string RegenerateUndoName = "Regenerar Decoracion";
    private static string activeUndo = UndoName;

    private const string TerrainTexturePath =
        "Assets/Art/Environment/terrain_valley_base_v001.png";

    private const string GrassPath =
        "Assets/Art/Environment/Decoration/decor_grass_cluster_v001.png";

    private const string StonePath =
        "Assets/Art/Environment/Decoration/decor_terrace_stones_v001.png";

    private const string GeneratedFolder = "Assets/Art/Generated/Level01";
    private const string CoverMeshPath =
        GeneratedFolder + "/Level01TerrainCover.asset";

    private const string CoverMaterialPath =
        GeneratedFolder + "/Level01TerrainCover.mat";

    private const string MeshShaderName =
        "Universal Render Pipeline/2D/Mesh2D-Lit-Default";

    private const string BuiltinSquareGuid =
        "311925a002f4447b3a28927169b83ea6";

    private const float ReferenceAspect = 16f / 9f;
    private const float PathBorderWidth = 0.08f;

    private static readonly Color Background = Hex(0x26, 0x3D, 0x30);
    private static readonly Color BackgroundDeep = Hex(0x1A, 0x2A, 0x20);
    private static readonly Color Cream = Hex(0xFF, 0xF1, 0xC6);
    private static readonly Color Gold = Hex(0xE2, 0xB8, 0x4B);
    private static readonly Color Green = Hex(0x3F, 0x8F, 0x4E);
    private static readonly Color Coral = Hex(0xD9, 0x52, 0x4A);
    private static readonly Color PathFill = Hex(0xC4, 0x96, 0x62);
    private static readonly Color PathBorder = Hex(0xA4, 0x78, 0x4C);

    [MenuItem(ApplyMenu, false, 19)]
    private static void ApplyFinish()
    {
        Run(false);
    }

    [MenuItem(RegenerateMenu, false, 20)]
    private static void RegenerateDressing()
    {
        Run(true);
    }

    [MenuItem(ApplyMenu, true)]
    private static bool ValidateApply()
    {
        return !IsBlocked();
    }

    [MenuItem(RegenerateMenu, true)]
    private static bool ValidateRegenerate()
    {
        return !IsBlocked();
    }

    private static void Run(bool regenerateOnly)
    {
        if (IsBlocked())
        {
            Report(
                "Detenido",
                "Level_01 debe estar abierta y Unity fuera de Play Mode. " +
                "No se modificó la escena."
            );
            return;
        }

        if (!TryInspect(out Scene scene, out Inspection inspection, out string error))
        {
            Report("Detenido", error);
            return;
        }

        ValleyFinishSettings existingFinish =
            inspection.Settings.GetComponent<ValleyFinishSettings>();
        bool skipTerrain =
            regenerateOnly ||
            (existingFinish != null && existingFinish.TerrainApplied);

        if (!PrepareArt(skipTerrain, out Art art, out error))
        {
            Report("Detenido", error);
            return;
        }

        activeUndo = regenerateOnly ? RegenerateUndoName : UndoName;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(activeUndo);
        GameObject prefabRoot = null;
        string prefabPath = null;

        try
        {
            bool changed = regenerateOnly
                ? Regenerate(scene, inspection, art)
                : Apply(scene, inspection, art, ref prefabRoot, ref prefabPath);

            if (prefabRoot != null && inspection.PrefabChanged)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }

            Undo.CollapseUndoOperations(group);

            if (changed || inspection.PrefabChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            Report(
                regenerateOnly ? "Decoración" : "Acabado visual",
                inspection.Report.ToString()
            );
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(exception);
            Report(
                "Detenido",
                "Los cambios de escena se revirtieron.\n" + exception.Message
            );
        }
        finally
        {
            if (prefabRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }

    private static bool Apply(
        Scene scene,
        Inspection inspection,
        Art art,
        ref GameObject prefabRoot,
        ref string prefabPath)
    {
        ValleyFinishSettings finish = EnsureFinish(inspection);
        inspection.Dressing = EnsureDressing(inspection);
        bool changed = false;

        if (!finish.TerrainApplied)
        {
            ApplyTerrain(inspection, art);
            SetBool(finish, "terrainApplied", true);
            inspection.Report.AppendLine(
                "Terreno: la textura del valle cubre el encuadre, " +
                "con la proporción original y tintura blanca. " +
                "El renderer del fondo generado quedó desactivado."
            );
            changed = true;
        }
        else
        {
            inspection.Report.AppendLine(
                "Terreno: se conservó el acabado ya aplicado."
            );
        }

        if (!finish.PathTintApplied)
        {
            ApplyPathTint(inspection, true);
            NoteUnreadablePath(inspection);
            SetBool(finish, "pathTintApplied", true);
            inspection.Report.AppendLine(
                "Camino: tierra y borde cálidos, con variación suave. " +
                "La geometría y el centro sobre los waypoints no cambiaron."
            );
            changed = true;
        }
        else if (RestorePathVariation(inspection))
        {
            inspection.Report.AppendLine(
                "Camino: se rehízo solo la variación porque la malla " +
                "la había perdido. El tinte personalizado se conservó."
            );
            changed = true;
        }
        else
        {
            inspection.Report.AppendLine(
                "Camino: se conservaron el tinte y la variación actuales."
            );
        }

        if (!inspection.Dressing.Generated)
        {
            int placed = PlaceDressing(scene, inspection, art, true);
            inspection.Report.AppendLine(DressingLine(placed, inspection));
            changed = true;
        }
        else
        {
            inspection.Report.AppendLine(
                "Decoración: ya estaba generada (" +
                inspection.Dressing.PlacedCount +
                " elementos). La acción Regenerar Decoracion " +
                "vuelve a crear solo las piezas de este generador."
            );
        }

        if (!finish.InterfaceApplied)
        {
            int buttons = PaintInterface(inspection);
            PaintEntryPrefab(inspection, ref prefabRoot, ref prefabPath);
            SetBool(finish, "interfaceApplied", true);
            inspection.Report.AppendLine(
                "Interfaz: paleta unificada en " + buttons +
                " botones, sin cambiar fuentes, listeners ni composición."
            );
            changed = true;
        }
        else
        {
            inspection.Report.AppendLine(
                "Interfaz: se conservaron los colores ya aplicados."
            );
        }

        if (!finish.TemporarySurfaceRetired)
        {
            int retired = RetireTemporaryGround(scene, inspection);
            SetBool(finish, "temporarySurfaceRetired", true);
            inspection.Report.AppendLine(
                retired > 0
                    ? "Superficie temporal: se desactivó el renderer de " +
                      retired + " cuadrado Ground."
                    : "Superficie temporal: no había un cuadrado Ground " +
                      "pendiente. No se desactivó ningún otro renderer."
            );
            changed = true;
        }

        AppendWarnings(inspection);
        inspection.Report.AppendLine(
            changed
                ? "La escena quedó marcada como modificada y no se guardó sola."
                : "No hubo cambios nuevos. La escena no se guardó."
        );
        return changed;
    }

    private static bool Regenerate(
        Scene scene,
        Inspection inspection,
        Art art)
    {
        inspection.Dressing = EnsureDressing(inspection);
        int placed = PlaceDressing(scene, inspection, art, true);
        inspection.Report.AppendLine(DressingLine(placed, inspection));
        AppendWarnings(inspection);
        inspection.Report.AppendLine(
            "Solo se tocaron las piezas marcadas por el generador. " +
            "La escena no se guardó sola."
        );
        return true;
    }

    private static void ApplyTerrain(Inspection inspection, Art art)
    {
        EnsureGeneratedFolder();
        Transform terrain = inspection.Terrain;
        float aspect = TerrainAspect(terrain);
        Mesh mesh = EnsureCoverMesh(aspect, art.TerrainAspect);
        Material material = EnsureCoverMaterial(art.Terrain, art.Shader);
        Transform surface = FindChild(terrain, "ValleySurface");

        if (surface == null)
        {
            GameObject child = CreateChild(
                "ValleySurface",
                terrain,
                inspection.Scene
            );
            surface = child.transform;
        }

        Record(surface);
        surface.localPosition = Vector3.zero;
        surface.localRotation = Quaternion.identity;
        surface.localScale = Vector3.one;

        MeshFilter filter = surface.GetComponent<MeshFilter>();
        MeshRenderer renderer = surface.GetComponent<MeshRenderer>();

        if (filter == null)
        {
            filter = Undo.AddComponent<MeshFilter>(surface.gameObject);
        }

        if (renderer == null)
        {
            renderer = Undo.AddComponent<MeshRenderer>(surface.gameObject);
        }

        Record(filter);
        Record(renderer);
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        renderer.sortingLayerID = inspection.OldTerrain.sortingLayerID;
        renderer.sortingOrder = inspection.OldTerrain.sortingOrder;
        renderer.enabled = true;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode =
            MotionVectorGenerationMode.ForceNoMotion;
        Commit(filter);
        Commit(renderer);

        Record(inspection.OldTerrain);
        inspection.OldTerrain.enabled = false;
        Commit(inspection.OldTerrain);
    }

    private static void ApplyPathTint(Inspection inspection, bool tintMaterials)
    {
        if (tintMaterials)
        {
            Tint(inspection.PathFill.sharedMaterial, PathFill);
            Tint(inspection.PathBorder.sharedMaterial, PathBorder);
        }

        Vary(inspection.PathFillMesh);
        Vary(inspection.PathBorderMesh);
    }

    private static bool RestorePathVariation(Inspection inspection)
    {
        bool missing =
            !HasVertexColors(inspection.PathFillMesh) ||
            !HasVertexColors(inspection.PathBorderMesh);

        if (!missing)
        {
            return false;
        }

        Vary(inspection.PathFillMesh);
        Vary(inspection.PathBorderMesh);
        return true;
    }

    private static int PlaceDressing(
        Scene scene,
        Inspection inspection,
        Art art,
        bool replaceOwned)
    {
        ValleyDressing dressing = inspection.Dressing;
        Transform root = FindChild(inspection.Settings.transform, "Dressing");

        if (root == null)
        {
            GameObject container = CreateChild(
                "Dressing",
                inspection.Settings.transform,
                scene
            );
            root = container.transform;
            Record(root);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }

        if (replaceOwned)
        {
            int foreign = RemoveOwned(root, inspection);
            if (foreign > 0)
            {
                inspection.Warnings.Add(
                    "Dressing conserva " + foreign +
                    " objetos que no pertenecen al generador."
                );
            }
        }

        Rect view = VisibleFrame(inspection.Camera);
        float band = inspection.Dressing.EdgeBand;
        float maxBand = Mathf.Min(view.width, view.height) * 0.22f;

        if (band > maxBand)
        {
            band = Mathf.Max(0.05f, maxBand);
            inspection.Warnings.Add(
                "La franja de borde se acotó a " +
                band.ToString("0.00") +
                " unidades en esta pasada para dejar espacio interior."
            );
        }

        List<Zone> zones = BuildZones(inspection, view);
        List<Placed> placed = new();
        System.Random random = new(dressing.Seed);
        int limit = dressing.ElementLimit;
        int budget = limit == 0 ? 0 : Mathf.Clamp(limit * 40, 64, 1600);
        int attempts = 0;
        int stoneTarget = StoneTarget(limit);
        int count = 0;

        while (count < stoneTarget && attempts < budget)
        {
            attempts++;
            float scale = NextScale(random, dressing);
            float radius = SpriteRadius(art.Stone, scale);
            Vector2 point = EdgePoint(view, band, radius, random);

            if (!CanPlace(
                    point,
                    radius,
                    view,
                    zones,
                    inspection,
                    placed,
                    dressing))
            {
                continue;
            }

            SpawnPiece(
                scene,
                root,
                art.Stone,
                "Stone",
                "stone",
                point,
                scale,
                inspection
            );
            placed.Add(new Placed(point, radius));
            count++;

            if (count >= stoneTarget || attempts >= budget)
            {
                break;
            }

            attempts++;
            Vector2 companion = point + Offset(random, dressing.ClusterRadius * 0.7f);
            float companionScale = NextScale(random, dressing);
            float companionRadius = SpriteRadius(art.Stone, companionScale);

            if (EdgeDistance(view, companion) > band ||
                !CanPlace(
                    companion,
                    companionRadius,
                    view,
                    zones,
                    inspection,
                    placed,
                    dressing))
            {
                continue;
            }

            SpawnPiece(
                scene,
                root,
                art.Stone,
                "Stone",
                "stone",
                companion,
                companionScale,
                inspection
            );
            placed.Add(new Placed(companion, companionRadius));
            count++;
        }

        while (count < limit && attempts < budget)
        {
            attempts++;
            float scale = NextScale(random, dressing);
            float radius = SpriteRadius(art.Grass, scale);
            Vector2 point = InteriorPoint(view, band, random);

            if (!CanPlace(
                    point,
                    radius,
                    view,
                    zones,
                    inspection,
                    placed,
                    dressing))
            {
                continue;
            }

            SpawnPiece(
                scene,
                root,
                art.Grass,
                "Grass",
                "grass",
                point,
                scale,
                inspection
            );
            placed.Add(new Placed(point, radius));
            count++;
            int extras = 1 + random.Next(2);

            for (int extra = 0;
                 extra < extras && count < limit && attempts < budget;
                 extra++)
            {
                attempts++;
                Vector2 cluster = point + Offset(random, dressing.ClusterRadius);
                float clusterScale = NextScale(random, dressing);
                float clusterRadius = SpriteRadius(art.Grass, clusterScale);

                if (EdgeDistance(view, cluster) <= band ||
                    !CanPlace(
                        cluster,
                        clusterRadius,
                        view,
                        zones,
                        inspection,
                        placed,
                        dressing))
                {
                    continue;
                }

                SpawnPiece(
                    scene,
                    root,
                    art.Grass,
                    "Grass",
                    "grass",
                    cluster,
                    clusterScale,
                    inspection
                );
                placed.Add(new Placed(cluster, clusterRadius));
                count++;
            }
        }

        if (count < limit)
        {
            inspection.Warnings.Add(
                "Se agotaron los " + budget +
                " intentos acotados. Se colocaron " + count +
                " de " + limit + " piezas."
            );
        }

        SetBool(dressing, "generated", true);
        SetInt(dressing, "placedCount", count);
        inspection.PlacedCount = count;
        inspection.Attempts = attempts;
        return count;
    }

    private static int RemoveOwned(Transform root, Inspection inspection)
    {
        int foreign = 0;

        for (int index = root.childCount - 1; index >= 0; index--)
        {
            Transform child = root.GetChild(index);

            if (child.GetComponent<ValleyDressingItem>() != null)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
                continue;
            }

            foreign++;
        }

        return foreign;
    }

    private static void SpawnPiece(
        Scene scene,
        Transform parent,
        Sprite sprite,
        string prefix,
        string kind,
        Vector2 point,
        float scale,
        Inspection inspection)
    {
        GameObject item = new(
            prefix + "_" + inspection.PlacedCount.ToString("00"),
            typeof(SpriteRenderer),
            typeof(ValleyDressingItem)
        );
        SceneManager.MoveGameObjectToScene(item, scene);
        Undo.RegisterCreatedObjectUndo(item, activeUndo);
        item.transform.SetParent(parent, false);
        Record(item.transform);
        item.transform.position = new Vector3(point.x, point.y, 0f);
        item.transform.rotation = Quaternion.identity;
        Vector3 parentScale = parent.lossyScale;
        float scaleX = Mathf.Abs(parentScale.x) < 0.0001f ? 1f : parentScale.x;
        float scaleY = Mathf.Abs(parentScale.y) < 0.0001f ? 1f : parentScale.y;
        item.transform.localScale = new Vector3(scale / scaleX, scale / scaleY, 1f);

        SpriteRenderer renderer = item.GetComponent<SpriteRenderer>();
        Record(renderer);
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.flipX = false;
        renderer.flipY = false;
        renderer.sortingLayerID = inspection.SortingLayerId;
        renderer.sortingOrder = inspection.DecorationOrder;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ValleyDressingItem marker = item.GetComponent<ValleyDressingItem>();
        Record(marker);
        SetString(marker, "pieceKind", kind);
        inspection.PlacedCount++;
        Commit(renderer);
        Commit(marker);
    }

    private static bool CanPlace(
        Vector2 point,
        float radius,
        Rect view,
        List<Zone> zones,
        Inspection inspection,
        List<Placed> placed,
        ValleyDressing dressing)
    {
        if (!Contains(view, point, radius))
        {
            return false;
        }

        float corridor =
            inspection.PathWidth * 0.5f +
            PathBorderWidth +
            dressing.PathMargin;

        if (DistanceToPath(point, inspection.Waypoints) < corridor + radius)
        {
            return false;
        }

        foreach (Vector2 spawn in inspection.Spawns)
        {
            if (Vector2.Distance(point, spawn) < dressing.SpawnClearance + radius)
            {
                return false;
            }
        }

        foreach (Zone zone in zones)
        {
            Bounds grown = zone.Bounds;
            float pad = zone.Padding + radius;
            grown.Expand(new Vector3(pad * 2f, pad * 2f, 2f));
            Vector3 probe = new(point.x, point.y, grown.center.z);

            if (grown.Contains(probe))
            {
                return false;
            }
        }

        foreach (Placed other in placed)
        {
            float gap = other.Radius + radius + dressing.MinSpacing * 0.35f;

            if (Vector2.Distance(point, other.Point) < gap)
            {
                return false;
            }
        }

        return true;
    }

    private static List<Zone> BuildZones(Inspection inspection, Rect view)
    {
        List<Zone> zones = new();
        float margin = inspection.Dressing.ActorMargin;
        zones.Add(new Zone(inspection.HeartBounds, margin));

        foreach (Bounds bounds in inspection.PlotBounds)
        {
            zones.Add(new Zone(bounds, margin));
        }

        foreach (Bounds bounds in inspection.TowerBounds)
        {
            zones.Add(new Zone(bounds, margin));
        }

        foreach (Bounds bounds in inspection.LiveActorBounds)
        {
            zones.Add(new Zone(bounds, margin));
        }

        float viewArea = Mathf.Max(0.01f, view.width * view.height);

        foreach (Bounds bounds in inspection.MemoryBounds)
        {
            float area = Mathf.Abs(bounds.size.x * bounds.size.y);

            if (area > viewArea * 0.4f)
            {
                inspection.Warnings.Add(
                    "El área de memorias ocupa gran parte del encuadre. " +
                    "No se usó como exclusión completa. La decoración queda " +
                    "debajo de su orden de dibujo."
                );
                continue;
            }

            zones.Add(new Zone(bounds, margin));
        }

        return zones;
    }

    private static int PaintInterface(Inspection inspection)
    {
        int buttons = 0;
        HashSet<int> discovery = DiscoveryTexts(inspection.Scene);
        HashSet<int> handled = new();

        foreach (Canvas canvas in inspection.Canvases)
        {
            foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (PaintButton(button))
                {
                    buttons++;
                }

                if (button.targetGraphic != null)
                {
                    handled.Add(button.targetGraphic.GetInstanceID());
                }
            }

            foreach (Slider slider in canvas.GetComponentsInChildren<Slider>(true))
            {
                PaintSlider(slider, handled);
            }

            foreach (Image image in canvas.GetComponentsInChildren<Image>(true))
            {
                if (handled.Contains(image.GetInstanceID()))
                {
                    continue;
                }

                PaintPanel(image);
            }

            foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                Color color = discovery.Contains(text.GetInstanceID())
                    ? Coral
                    : Cream;
                AssignGraphic(text, color);
            }
        }

        return buttons;
    }

    private static void PaintEntryPrefab(
        Inspection inspection,
        ref GameObject prefabRoot,
        ref string prefabPath)
    {
        if (inspection.EntryPrefab == null)
        {
            inspection.Warnings.Add(
                "El códice no tiene prefab de fila. " +
                "Las filas que ya existan en escena sí recibieron color."
            );
            return;
        }

        prefabPath = AssetDatabase.GetAssetPath(inspection.EntryPrefab.gameObject);

        if (string.IsNullOrEmpty(prefabPath))
        {
            return;
        }

        prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        bool changed = false;

        foreach (Button button in prefabRoot.GetComponentsInChildren<Button>(true))
        {
            changed |= PaintButton(button, false);
        }

        foreach (TMP_Text text in prefabRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            changed |= AssignGraphic(text, Cream, false);
        }

        inspection.PrefabChanged = changed;

        if (!changed)
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            prefabRoot = null;
        }
    }

    private static bool PaintButton(Button button)
    {
        return PaintButton(button, true);
    }

    private static bool PaintButton(Button button, bool record)
    {
        Image image = button.targetGraphic as Image;
        bool purple = image != null && IsPurple(image.color);
        bool uiSprite = image == null || IsUiSprite(image.sprite);

        if (!uiSprite && !purple)
        {
            return false;
        }

        string name = button.gameObject.name;
        bool primary =
            purple ||
            name == "CodexButton" ||
            name == "ContinueButton" ||
            name == "ViewCodexButton";
        Color normal = primary ? Gold : Green;
        bool changed = false;

        if (record)
        {
            Record(button);
        }

        ColorBlock states = States(normal);

        if (!Same(button.colors, states))
        {
            button.colors = states;
            changed = true;
        }

        if (image != null)
        {
            changed |= AssignGraphic(image, Color.white, record);
        }

        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            changed |= AssignGraphic(label, Cream, record);
        }

        if (record)
        {
            Commit(button);
        }

        return changed;
    }

    private static void PaintSlider(Slider slider, HashSet<int> handled)
    {
        foreach (Image image in slider.GetComponentsInChildren<Image>(true))
        {
            handled.Add(image.GetInstanceID());
            string name = image.gameObject.name;

            if (name == "Fill")
            {
                AssignGraphic(image, Green);
            }
            else if (name == "Handle")
            {
                AssignGraphic(image, Cream);
            }
            else if (name == "Background")
            {
                AssignGraphic(image, BackgroundDeep);
            }
        }
    }

    private static void PaintPanel(Image image)
    {
        if (image.sprite != null && !IsUiSprite(image.sprite))
        {
            return;
        }

        string name = image.gameObject.name;
        bool named =
            name.Contains("Panel") ||
            name.Contains("Card") ||
            name.Contains("Band") ||
            name.Contains("Dimmer") ||
            name.Contains("Background") ||
            name.Contains("Modal");
        bool dark = image.color.maxColorComponent < 0.45f;
        bool purple = IsPurple(image.color);

        if (!named && !dark && !purple)
        {
            return;
        }

        Color color = name.Contains("Dimmer") ? Background : Background;
        if (!name.Contains("Dimmer") && name.Contains("Background") &&
            image.GetComponentInParent<Slider>() != null)
        {
            color = BackgroundDeep;
        }

        color.a = image.color.a;
        AssignGraphic(image, color);
    }

    private static int RetireTemporaryGround(Scene scene, Inspection inspection)
    {
        int retired = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (SpriteRenderer renderer in
                     root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.gameObject.name != "Ground")
                {
                    continue;
                }

                if (renderer.GetComponent<MonoBehaviour>() != null ||
                    renderer.GetComponent<Collider2D>() != null)
                {
                    inspection.Warnings.Add(
                        renderer.gameObject.name +
                        " tiene componentes de juego y se dejó activo."
                    );
                    continue;
                }

                if (!IsTemporarySquare(renderer.sprite))
                {
                    continue;
                }

                if (!renderer.enabled)
                {
                    continue;
                }

                Record(renderer);
                renderer.enabled = false;
                Commit(renderer);
                retired++;
            }
        }

        return retired;
    }

    private static bool TryInspect(
        out Scene scene,
        out Inspection inspection,
        out string error)
    {
        scene = SceneManager.GetSceneByPath(ScenePath);
        inspection = null;
        error = null;

        if (!scene.IsValid() || !scene.isLoaded)
        {
            error =
                "Abre Level_01 fuera de Play Mode. " +
                "No se modificó la escena.";
            return false;
        }

        inspection = new Inspection(scene);
        LevelVisualSettings[] settings = FindAll<LevelVisualSettings>(scene);

        if (settings.Length != 1)
        {
            error = settings.Length == 0
                ? "Level_01 no tiene el encuadre de LevelVisuals. " +
                  "Ejecuta antes el incremento 17. No se modificó la escena."
                : "Hay varios LevelVisualSettings. No se modificó la escena.";
            return false;
        }

        inspection.Settings = settings[0];
        Transform terrain = FindChild(settings[0].transform, "Terrain");
        Transform path = FindChild(settings[0].transform, "Path");

        if (terrain == null || terrain.GetComponent<MeshRenderer>() == null)
        {
            error =
                "Falta el terreno de LevelVisuals. " +
                "No se modificó la escena.";
            return false;
        }

        if (path == null ||
            FindChild(path, "PathFill") == null ||
            FindChild(path, "PathBorder") == null)
        {
            error =
                "Falta el camino generado sobre los waypoints. " +
                "No se modificó la escena.";
            return false;
        }

        inspection.Terrain = terrain;
        inspection.OldTerrain = terrain.GetComponent<MeshRenderer>();
        inspection.PathFill = FindChild(path, "PathFill").GetComponent<MeshRenderer>();
        inspection.PathBorder =
            FindChild(path, "PathBorder").GetComponent<MeshRenderer>();
        inspection.PathFillMesh =
            FindChild(path, "PathFill").GetComponent<MeshFilter>().sharedMesh;
        inspection.PathBorderMesh =
            FindChild(path, "PathBorder").GetComponent<MeshFilter>().sharedMesh;

        if (inspection.PathFill == null ||
            inspection.PathBorder == null ||
            inspection.PathFill.sharedMaterial == null ||
            inspection.PathBorder.sharedMaterial == null ||
            inspection.PathFillMesh == null ||
            inspection.PathBorderMesh == null)
        {
            error =
                "El camino no tiene malla o material. " +
                "No se modificó la escena.";
            return false;
        }

        if (!TryCamera(scene, out Camera camera, out error))
        {
            return false;
        }

        inspection.Camera = camera;

        if (!inspection.Camera.orthographic || inspection.Camera.orthographicSize <= 0.01f)
        {
            error =
                "La cámara base no tiene un encuadre ortográfico válido. " +
                "No se modificó la escena.";
            return false;
        }

        inspection.PathWidth = ReadFloat(settings[0], "pathWidth", 0.45f);

        if (inspection.PathWidth < 0.05f)
        {
            error =
                "El ancho del camino no es válido. No se modificó la escena.";
            return false;
        }

        if (!TryRoute(scene, inspection, out error) ||
            !TryPlots(scene, inspection, out error) ||
            !TryHeart(scene, inspection, out error) ||
            !TryMemories(scene, inspection, out error))
        {
            return false;
        }

        inspection.Dressing = settings[0].GetComponent<ValleyDressing>();
        Canvas[] canvases = FindAll<Canvas>(scene);
        List<Canvas> screens = new();

        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            screens.Add(canvas);
        }

        if (screens.Count == 0)
        {
            error = "Level_01 no tiene un Canvas de interfaz. No se modificó la escena.";
            return false;
        }

        inspection.Canvases = screens;
        CodexController[] codices = FindAll<CodexController>(scene);

        if (codices.Length == 1)
        {
            inspection.EntryPrefab = ReadReference<CodexEntryView>(
                codices[0],
                "entryPrefab"
            );
        }

        int minimum = inspection.MinimumOrder;

        if (inspection.LayerConflict)
        {
            error =
                "Los actores no comparten la misma sorting layer. " +
                "No se modificó la escena.";
            return false;
        }

        if (minimum == int.MaxValue)
        {
            error =
                "No se pudo medir el orden de los actores. " +
                "No se modificó la escena.";
            return false;
        }

        inspection.DecorationOrder = minimum - 1;

        if (inspection.DecorationOrder <= inspection.OldTerrain.sortingOrder)
        {
            inspection.Warnings.Add(
                "Los actores usan un orden muy bajo. La decoración queda " +
                "detrás del terreno para no tapar enemigos ni memorias."
            );
        }

        return true;
    }

    private static bool TryCamera(Scene scene, out Camera camera, out string error)
    {
        camera = null;
        error = null;
        List<Camera> bases = new();

        foreach (Camera candidate in FindAll<Camera>(scene))
        {
            if (!candidate.enabled || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            UniversalAdditionalCameraData data =
                candidate.GetComponent<UniversalAdditionalCameraData>();

            if (data != null && data.renderType == CameraRenderType.Overlay)
            {
                continue;
            }

            bases.Add(candidate);
        }

        if (bases.Count != 1)
        {
            error =
                "Level_01 necesita una sola cámara base activa. " +
                "No se modificó la escena.";
            return false;
        }

        camera = bases[0];
        return true;
    }

    private static bool TryRoute(
        Scene scene,
        Inspection inspection,
        out string error)
    {
        error = null;
        EnemySpawner[] spawners = FindAll<EnemySpawner>(scene);

        if (spawners.Length == 0)
        {
            error = "Level_01 no tiene EnemySpawner. No se modificó la escena.";
            return false;
        }

        EnemyPath path = null;

        foreach (EnemySpawner spawner in spawners)
        {
            EnemyPath candidate = ReadReference<EnemyPath>(spawner, "enemyPath");

            if (candidate == null)
            {
                error =
                    spawner.gameObject.name +
                    " no tiene ruta. No se modificó la escena.";
                return false;
            }

            if (path == null)
            {
                path = candidate;
            }
            else if (path != candidate)
            {
                error =
                    "Los spawners usan rutas distintas. " +
                    "No se modificó la escena.";
                return false;
            }

            inspection.Spawns.Add(spawner.transform.position);
            EnemyMovement enemy = ReadReference<EnemyMovement>(
                spawner,
                "enemyPrefab"
            );

            if (enemy == null)
            {
                error =
                    spawner.gameObject.name +
                    " no tiene prefab de enemigo. No se modificó la escena.";
                return false;
            }

            IncludeOrder(inspection, enemy.gameObject);
        }

        if (path.Count < 2)
        {
            error = "La ruta no tiene suficientes waypoints. No se modificó la escena.";
            return false;
        }

        for (int index = 0; index < path.Count; index++)
        {
            Transform waypoint = path.GetWaypoint(index);

            if (waypoint == null)
            {
                error = "La ruta tiene un waypoint vacío. No se modificó la escena.";
                return false;
            }

            inspection.Waypoints.Add(waypoint.position);
        }

        return true;
    }

    private static bool TryPlots(
        Scene scene,
        Inspection inspection,
        out string error)
    {
        error = null;
        BuildPlot[] plots = FindAll<BuildPlot>(scene);

        if (plots.Length == 0)
        {
            error = "Level_01 no tiene parcelas. No se modificó la escena.";
            return false;
        }

        foreach (BuildPlot plot in plots)
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
                    out Bounds tower,
                    out error))
            {
                return false;
            }

            inspection.PlotBounds.Add(renderer.bounds);
            inspection.PlotBounds.Add(collider.bounds);
            inspection.TowerBounds.Add(tower);
            IncludeRenderer(inspection, renderer);
            IncludeOrder(inspection, prefab.gameObject);
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
        Sprite sprite = prefab.Data != null ? prefab.Data.WorldSprite : null;

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

        if (prefab.transform.localRotation != Quaternion.identity ||
            (container != null && container.rotation != Quaternion.identity))
        {
            error =
                plot.gameObject.name +
                " o su torre está rotada. No se modificó la escena.";
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

    private static bool TryHeart(
        Scene scene,
        Inspection inspection,
        out string error)
    {
        error = null;
        BastionHealth[] hearts = FindAll<BastionHealth>(scene);

        if (hearts.Length != 1)
        {
            error = hearts.Length == 0
                ? "Level_01 no tiene BastionHealth. No se modificó la escena."
                : "Hay varios BastionHealth. No se modificó la escena.";
            return false;
        }

        SpriteRenderer[] renderers =
            hearts[0].GetComponentsInChildren<SpriteRenderer>(true);

        if (renderers.Length == 0)
        {
            error =
                "El corazón no tiene un sprite medible. " +
                "No se modificó la escena.";
            return false;
        }

        Bounds bounds = renderers[0].bounds;

        foreach (SpriteRenderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
            IncludeRenderer(inspection, renderer);
        }

        inspection.HeartBounds = bounds;
        return true;
    }

    private static bool TryMemories(
        Scene scene,
        Inspection inspection,
        out string error)
    {
        error = null;
        CollectibleSpawner[] spawners = FindAll<CollectibleSpawner>(scene);

        if (spawners.Length == 0)
        {
            error =
                "Level_01 no tiene CollectibleSpawner. " +
                "No se modificó la escena.";
            return false;
        }

        foreach (CollectibleSpawner spawner in spawners)
        {
            EducationalCollectible prefab = ReadReference<EducationalCollectible>(
                spawner,
                "collectiblePrefab"
            );
            WanderingArea2D area = ReadReference<WanderingArea2D>(
                spawner,
                "wanderingArea"
            );

            if (prefab == null)
            {
                error =
                    spawner.gameObject.name +
                    " no tiene prefab de memoria. No se modificó la escena.";
                return false;
            }

            IncludeOrder(inspection, prefab.gameObject);

            if (area == null)
            {
                inspection.Warnings.Add(
                    spawner.gameObject.name +
                    " no tiene área de deambulación. " +
                    "La decoración solo quedará detrás del orden de las memorias."
                );
                continue;
            }

            Collider2D collider = area.GetComponent<Collider2D>();

            if (collider != null)
            {
                inspection.MemoryBounds.Add(collider.bounds);
            }
        }

        foreach (EducationalCollectible live in FindAll<EducationalCollectible>(scene))
        {
            foreach (SpriteRenderer renderer in
                     live.GetComponentsInChildren<SpriteRenderer>(true))
            {
                inspection.LiveActorBounds.Add(renderer.bounds);
                IncludeRenderer(inspection, renderer);
            }
        }

        foreach (EnemyMovement live in FindAll<EnemyMovement>(scene))
        {
            if (PrefabUtility.IsPartOfPrefabAsset(live))
            {
                continue;
            }

            foreach (SpriteRenderer renderer in
                     live.GetComponentsInChildren<SpriteRenderer>(true))
            {
                inspection.LiveActorBounds.Add(renderer.bounds);
                IncludeRenderer(inspection, renderer);
            }
        }

        return true;
    }

    private static bool PrepareArt(
        bool decorationOnly,
        out Art art,
        out string error)
    {
        art = null;
        error = null;
        Shader shader = null;
        Texture2D terrain = null;

        if (!decorationOnly)
        {
            shader = Shader.Find(MeshShaderName);

            if (shader == null)
            {
                error =
                    "No está el shader 2D del terreno. No se modificó la escena.";
                return false;
            }

            if (!PrepareTerrainTexture(out terrain, out error))
            {
                return false;
            }
        }

        if (!PrepareSprite(GrassPath, out Sprite grass, out error) ||
            !PrepareSprite(StonePath, out Sprite stone, out error))
        {
            return false;
        }

        art = new Art(terrain, grass, stone, shader);
        return true;
    }

    private static bool PrepareTerrainTexture(out Texture2D texture, out string error)
    {
        texture = null;
        error = null;

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainTexturePath) == null)
        {
            error =
                "Falta terrain_valley_base_v001.png. No se modificó la escena.";
            return false;
        }

        TextureImporter importer =
            AssetImporter.GetAtPath(TerrainTexturePath) as TextureImporter;

        if (importer == null)
        {
            error = "La textura del valle no es importable. No se modificó la escena.";
            return false;
        }

        bool ready =
            !importer.mipmapEnabled &&
            importer.wrapMode == TextureWrapMode.Clamp &&
            importer.filterMode == FilterMode.Bilinear &&
            importer.alphaIsTransparency;

        if (!ready)
        {
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainTexturePath);

        if (texture == null || texture.width < 2 || texture.height < 2)
        {
            error =
                "La textura del valle no tiene un tamaño utilizable. " +
                "No se modificó la escena.";
            return false;
        }

        return true;
    }

    private static bool PrepareSprite(
        string assetPath,
        out Sprite sprite,
        out string error)
    {
        sprite = null;
        error = null;

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath) == null)
        {
            error = assetPath + " no está en el proyecto. No se modificó la escena.";
            return false;
        }

        if (!LoadSingleSprite(assetPath, out sprite))
        {
            if (!ConfigureSpriteImporter(assetPath, out error) ||
                !LoadSingleSprite(assetPath, out sprite))
            {
                error ??=
                    assetPath +
                    " no quedó como un solo sprite. No se modificó la escena.";
                return false;
            }
        }

        return sprite != null;
    }

    private static bool LoadSingleSprite(string assetPath, out Sprite sprite)
    {
        sprite = null;
        int count = 0;

        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (asset is not Sprite candidate)
            {
                continue;
            }

            sprite = candidate;
            count++;
        }

        return count == 1 && sprite != null;
    }

    private static bool ConfigureSpriteImporter(string assetPath, out string error)
    {
        error = null;
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
        {
            error = assetPath + " dejó de ser una textura importable.";
            return false;
        }

        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);
        Vector2 center = new(0.5f, 0.5f);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.spritePivot = center;
        settings.textureType = TextureImporterType.Sprite;
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = center;
        settings.mipmapEnabled = false;
        settings.alphaIsTransparency = true;
        settings.filterMode = FilterMode.Bilinear;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return true;
    }

    private static ValleyFinishSettings EnsureFinish(Inspection inspection)
    {
        ValleyFinishSettings finish =
            inspection.Settings.GetComponent<ValleyFinishSettings>();

        if (finish != null)
        {
            return finish;
        }

        return Undo.AddComponent<ValleyFinishSettings>(inspection.Settings.gameObject);
    }

    private static ValleyDressing EnsureDressing(Inspection inspection)
    {
        if (inspection.Dressing != null)
        {
            return inspection.Dressing;
        }

        ValleyDressing dressing =
            inspection.Settings.GetComponent<ValleyDressing>();

        if (dressing == null)
        {
            dressing = Undo.AddComponent<ValleyDressing>(
                inspection.Settings.gameObject
            );
        }

        inspection.Dressing = dressing;
        return dressing;
    }

    private static void NoteUnreadablePath(Inspection inspection)
    {
        if (inspection.PathFillMesh.isReadable &&
            inspection.PathBorderMesh.isReadable)
        {
            return;
        }

        inspection.Warnings.Add(
            "Una malla del camino no admite colores de vértice. " +
            "Se ajustó el tinte y se conservó la geometría."
        );
    }

    private static Mesh EnsureCoverMesh(float quadAspect, float textureAspect)
    {
        float u0 = 0f;
        float u1 = 1f;
        float v0 = 0f;
        float v1 = 1f;
        float safeQuad = Mathf.Max(0.01f, quadAspect);
        float safeTexture = Mathf.Max(0.01f, textureAspect);

        if (safeTexture > safeQuad)
        {
            float span = safeQuad / safeTexture;
            u0 = (1f - span) * 0.5f;
            u1 = u0 + span;
        }
        else
        {
            float span = safeTexture / safeQuad;
            v0 = (1f - span) * 0.5f;
            v1 = v0 + span;
        }

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CoverMeshPath);
        bool created = mesh == null;

        if (created)
        {
            mesh = new Mesh
            {
                name = "Level01TerrainCover"
            };
            AssetDatabase.CreateAsset(mesh, CoverMeshPath);
        }
        else
        {
            Undo.RecordObject(mesh, activeUndo);
        }

        mesh.Clear();
        mesh.SetVertices(new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f)
        });
        mesh.SetUVs(0, new[]
        {
            new Vector2(u0, v0),
            new Vector2(u0, v1),
            new Vector2(u1, v1),
            new Vector2(u1, v0)
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
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssetIfDirty(mesh);
        return mesh;
    }

    private static Material EnsureCoverMaterial(Texture2D texture, Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(CoverMaterialPath);

        if (material == null)
        {
            material = new Material(shader)
            {
                name = "Level01TerrainCover"
            };
            AssetDatabase.CreateAsset(material, CoverMaterialPath);
        }
        else
        {
            Undo.RecordObject(material, activeUndo);
        }

        material.shader = shader;
        material.SetTexture("_MainTex", texture);

        if (material.HasProperty("_White"))
        {
            material.SetColor("_White", Color.white);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    private static void Tint(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        Undo.RecordObject(material, activeUndo);

        if (material.HasProperty("_White"))
        {
            material.SetColor("_White", color);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
    }

    private static void Vary(Mesh mesh)
    {
        if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0)
        {
            return;
        }

        Undo.RecordObject(mesh, activeUndo);
        Vector3[] vertices = mesh.vertices;
        Color[] colors = new Color[vertices.Length];

        for (int index = 0; index < vertices.Length; index++)
        {
            float noise = Hash01(vertices[index].x, vertices[index].y);
            float value = Mathf.Lerp(0.93f, 1f, noise);
            colors[index] = new Color(value, value, value, 1f);
        }

        mesh.SetColors(colors);
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssetIfDirty(mesh);
    }

    private static bool HasVertexColors(Mesh mesh)
    {
        if (mesh == null || !mesh.isReadable)
        {
            return true;
        }

        Color[] colors = mesh.colors;
        return colors != null && colors.Length == mesh.vertexCount;
    }

    private static void EnsureGeneratedFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art/Generated"))
        {
            AssetDatabase.CreateFolder("Assets/Art", "Generated");
        }

        if (!AssetDatabase.IsValidFolder(GeneratedFolder))
        {
            AssetDatabase.CreateFolder("Assets/Art/Generated", "Level01");
        }
    }

    private static float TerrainAspect(Transform terrain)
    {
        Vector3 scale = terrain.lossyScale;
        float width = Mathf.Abs(scale.x);
        float height = Mathf.Abs(scale.y);

        if (height < 0.0001f)
        {
            return ReferenceAspect;
        }

        return width / height;
    }

    private static Rect VisibleFrame(Camera camera)
    {
        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * ReferenceAspect;
        Vector3 origin = camera.transform.position;
        return new Rect(
            origin.x - halfWidth,
            origin.y - halfHeight,
            halfWidth * 2f,
            halfHeight * 2f
        );
    }

    private static int StoneTarget(int limit)
    {
        if (limit <= 1)
        {
            return limit;
        }

        return Mathf.Clamp(Mathf.RoundToInt(limit * 0.42f), 1, limit - 1);
    }

    private static float NextScale(System.Random random, ValleyDressing dressing)
    {
        return Mathf.Lerp(dressing.MinScale, dressing.MaxScale, Next01(random));
    }

    private static float SpriteRadius(Sprite sprite, float scale)
    {
        Vector3 size = sprite.bounds.size;
        return 0.5f * Mathf.Max(size.x, size.y) * Mathf.Abs(scale);
    }

    private static Vector2 EdgePoint(
        Rect view,
        float band,
        float radius,
        System.Random random)
    {
        int side = random.Next(4);
        float depth = Mathf.Lerp(Mathf.Min(radius, band), band, Next01(random));
        float alongX = Mathf.Lerp(view.xMin, view.xMax, Next01(random));
        float alongY = Mathf.Lerp(view.yMin, view.yMax, Next01(random));

        return side switch
        {
            0 => new Vector2(alongX, view.yMin + depth),
            1 => new Vector2(alongX, view.yMax - depth),
            2 => new Vector2(view.xMin + depth, alongY),
            _ => new Vector2(view.xMax - depth, alongY)
        };
    }

    private static Vector2 InteriorPoint(Rect view, float band, System.Random random)
    {
        return new Vector2(
            Mathf.Lerp(view.xMin + band, view.xMax - band, Next01(random)),
            Mathf.Lerp(view.yMin + band, view.yMax - band, Next01(random))
        );
    }

    private static Vector2 Offset(System.Random random, float radius)
    {
        float angle = Next01(random) * Mathf.PI * 2f;
        float distance = Mathf.Lerp(radius * 0.45f, radius, Next01(random));
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    }

    private static float EdgeDistance(Rect view, Vector2 point)
    {
        return Mathf.Min(
            point.x - view.xMin,
            view.xMax - point.x,
            point.y - view.yMin,
            view.yMax - point.y
        );
    }

    private static bool Contains(Rect view, Vector2 point, float radius)
    {
        return point.x >= view.xMin + radius &&
               point.x <= view.xMax - radius &&
               point.y >= view.yMin + radius &&
               point.y <= view.yMax - radius;
    }

    private static float DistanceToPath(Vector2 point, List<Vector2> path)
    {
        float best = float.MaxValue;

        for (int index = 1; index < path.Count; index++)
        {
            best = Mathf.Min(
                best,
                DistanceToSegment(point, path[index - 1], path[index])
            );
        }

        return best;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 edge = end - start;
        float length = edge.sqrMagnitude;

        if (length < 0.0001f)
        {
            return Vector2.Distance(point, start);
        }

        float amount = Mathf.Clamp01(Vector2.Dot(point - start, edge) / length);
        return Vector2.Distance(point, start + edge * amount);
    }

    private static void IncludeOrder(Inspection inspection, GameObject root)
    {
        foreach (SpriteRenderer renderer in
                 root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            IncludeRenderer(inspection, renderer);
        }
    }

    private static void IncludeRenderer(Inspection inspection, SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        if (!inspection.LayerReady)
        {
            inspection.SortingLayerId = renderer.sortingLayerID;
            inspection.LayerReady = true;
        }
        else if (renderer.sortingLayerID != inspection.SortingLayerId)
        {
            inspection.LayerConflict = true;
        }

        inspection.MinimumOrder = Mathf.Min(
            inspection.MinimumOrder,
            renderer.sortingOrder
        );
    }

    private static HashSet<int> DiscoveryTexts(Scene scene)
    {
        HashSet<int> ids = new();

        foreach (EducationalModalController modal in FindAll<EducationalModalController>(scene))
        {
            TMP_Text text = ReadReference<TMP_Text>(modal, "newDiscoveryText");

            if (text != null)
            {
                ids.Add(text.GetInstanceID());
            }
        }

        return ids;
    }

    private static bool IsTemporarySquare(Sprite sprite)
    {
        if (sprite == null || sprite.name != "Square")
        {
            return false;
        }

        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
            sprite,
            out string guid,
            out long _
        );
        return guid == BuiltinSquareGuid;
    }

    private static bool IsUiSprite(Sprite sprite)
    {
        if (sprite == null)
        {
            return true;
        }

        string path = AssetDatabase.GetAssetPath(sprite);
        return string.IsNullOrEmpty(path) || path.StartsWith("Resources/");
    }

    private static bool IsPurple(Color color)
    {
        return color.b > 0.35f &&
               color.b > color.r + 0.08f &&
               color.b > color.g + 0.08f &&
               color.a > 0.2f;
    }

    private static ColorBlock States(Color normal)
    {
        ColorBlock block = ColorBlock.defaultColorBlock;
        block.normalColor = normal;
        block.highlightedColor = Color.Lerp(normal, Color.white, 0.28f);
        block.selectedColor = Color.Lerp(normal, Cream, 0.45f);
        block.pressedColor = Color.Lerp(normal, Color.black, 0.22f);
        Color disabled = Color.Lerp(normal, new Color(0.15f, 0.15f, 0.15f, 1f), 0.55f);
        disabled.a = 0.45f;
        block.disabledColor = disabled;
        block.colorMultiplier = 1f;
        block.fadeDuration = 0.08f;
        return block;
    }

    private static bool Same(ColorBlock left, ColorBlock right)
    {
        return left.normalColor == right.normalColor &&
               left.highlightedColor == right.highlightedColor &&
               left.selectedColor == right.selectedColor &&
               left.pressedColor == right.pressedColor &&
               left.disabledColor == right.disabledColor;
    }

    private static bool AssignGraphic(Graphic graphic, Color color)
    {
        return AssignGraphic(graphic, color, true);
    }

    private static bool AssignGraphic(Graphic graphic, Color color, bool record)
    {
        if (graphic == null || graphic.color == color)
        {
            return false;
        }

        if (record)
        {
            Record(graphic);
        }

        graphic.color = color;

        if (record)
        {
            Commit(graphic);
        }

        return true;
    }

    private static void SetBool(UnityEngine.Object target, string fieldName, bool value)
    {
        SetProperty(target, fieldName, property => property.boolValue = value);
    }

    private static void SetInt(UnityEngine.Object target, string fieldName, int value)
    {
        SetProperty(target, fieldName, property => property.intValue = value);
    }

    private static void SetString(UnityEngine.Object target, string fieldName, string value)
    {
        SetProperty(target, fieldName, property => property.stringValue = value);
    }

    private static void SetProperty(
        UnityEngine.Object target,
        string fieldName,
        Action<SerializedProperty> assign)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            throw new InvalidOperationException(
                target.GetType().Name + " no tiene el campo " + fieldName + "."
            );
        }

        assign(property);
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static float ReadFloat(
        UnityEngine.Object owner,
        string fieldName,
        float fallback)
    {
        SerializedObject serialized = new(owner);
        SerializedProperty property = serialized.FindProperty(fieldName);
        return property == null ? fallback : property.floatValue;
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
        return property == null ? null : property.objectReferenceValue as T;
    }

    private static T[] FindAll<T>(Scene scene) where T : Component
    {
        List<T> results = new();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            results.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return results.ToArray();
    }

    private static GameObject CreateChild(string name, Transform parent, Scene scene)
    {
        GameObject child = new(name);
        SceneManager.MoveGameObjectToScene(child, scene);
        Undo.RegisterCreatedObjectUndo(child, activeUndo);
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

    private static float Hash01(float x, float y)
    {
        int ix = Mathf.FloorToInt(x * 100f);
        int iy = Mathf.FloorToInt(y * 100f);
        uint hash = (uint)(ix * 374761393 + iy * 668265263);
        hash = (hash ^ (hash >> 13)) * 1274126177u;
        hash ^= hash >> 16;
        return (hash & 65535) / 65535f;
    }

    private static float Next01(System.Random random)
    {
        return (float)random.NextDouble();
    }

    private static Color Hex(byte red, byte green, byte blue)
    {
        return new Color32(red, green, blue, 255);
    }

    private static string DressingLine(int placed, Inspection inspection)
    {
        return "Decoración: " + placed +
               " elementos, " + inspection.Attempts +
               " intentos, semilla " + inspection.Dressing.Seed +
               ". Sin colliders ni cambios de navegación.";
    }

    private static void AppendWarnings(Inspection inspection)
    {
        if (inspection.Warnings.Count == 0)
        {
            inspection.Report.AppendLine("Advertencias: ninguna.");
            return;
        }

        inspection.Report.AppendLine("Advertencias:");

        foreach (string warning in inspection.Warnings)
        {
            inspection.Report.AppendLine("- " + warning);
        }
    }

    private static void Record(UnityEngine.Object target)
    {
        if (target != null)
        {
            Undo.RecordObject(target, activeUndo);
        }
    }

    private static void Commit(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        EditorUtility.SetDirty(target);

        if (PrefabUtility.IsPartOfPrefabInstance(target))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }

    private static bool IsBlocked()
    {
        return EditorApplication.isPlaying ||
               EditorApplication.isPlayingOrWillChangePlaymode ||
               EditorApplication.isCompiling;
    }

    private static void Report(string title, string message)
    {
        EditorUtility.DisplayDialog("Bastión del Valle", title + "\n\n" + message, "Cerrar");
    }

    private sealed class Inspection
    {
        public Inspection(Scene scene)
        {
            Scene = scene;
            Waypoints = new List<Vector2>();
            Spawns = new List<Vector2>();
            PlotBounds = new List<Bounds>();
            TowerBounds = new List<Bounds>();
            MemoryBounds = new List<Bounds>();
            LiveActorBounds = new List<Bounds>();
            Warnings = new List<string>();
            Report = new StringBuilder();
            MinimumOrder = int.MaxValue;
        }

        public Scene Scene { get; }
        public LevelVisualSettings Settings { get; set; }
        public ValleyDressing Dressing { get; set; }
        public Camera Camera { get; set; }
        public Transform Terrain { get; set; }
        public MeshRenderer OldTerrain { get; set; }
        public MeshRenderer PathFill { get; set; }
        public MeshRenderer PathBorder { get; set; }
        public Mesh PathFillMesh { get; set; }
        public Mesh PathBorderMesh { get; set; }
        public float PathWidth { get; set; }
        public Bounds HeartBounds { get; set; }
        public List<Vector2> Waypoints { get; }
        public List<Vector2> Spawns { get; }
        public List<Bounds> PlotBounds { get; }
        public List<Bounds> TowerBounds { get; }
        public List<Bounds> MemoryBounds { get; }
        public List<Bounds> LiveActorBounds { get; }
        public List<Canvas> Canvases { get; set; }
        public CodexEntryView EntryPrefab { get; set; }
        public int SortingLayerId { get; set; }
        public int MinimumOrder { get; set; }
        public int DecorationOrder { get; set; }
        public bool LayerReady { get; set; }
        public bool LayerConflict { get; set; }
        public int PlacedCount { get; set; }
        public int Attempts { get; set; }
        public bool PrefabChanged { get; set; }
        public List<string> Warnings { get; }
        public StringBuilder Report { get; }
    }

    private sealed class Art
    {
        public Art(Texture2D terrain, Sprite grass, Sprite stone, Shader shader)
        {
            Terrain = terrain;
            Grass = grass;
            Stone = stone;
            Shader = shader;
            TerrainAspect = terrain == null || terrain.height == 0
                ? 1f
                : terrain.width / (float)terrain.height;
        }

        public Texture2D Terrain { get; }
        public Sprite Grass { get; }
        public Sprite Stone { get; }
        public Shader Shader { get; }
        public float TerrainAspect { get; }
    }

    private readonly struct Zone
    {
        public Zone(Bounds bounds, float padding)
        {
            Bounds = bounds;
            Padding = padding;
        }

        public Bounds Bounds { get; }
        public float Padding { get; }
    }

    private readonly struct Placed
    {
        public Placed(Vector2 point, float radius)
        {
            Point = point;
            Radius = radius;
        }

        public Vector2 Point { get; }
        public float Radius { get; }
    }
}
