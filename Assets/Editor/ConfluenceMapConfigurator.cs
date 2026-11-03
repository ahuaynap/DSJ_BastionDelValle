using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfluenceMapConfigurator
{
    private const string MenuPath =
        "Bastion del Valle/Mapas/21 - Crear Confluencia Base";

    private const string Level01Path = "Assets/Scenes/Level_01.unity";
    private const string Level02Path = "Assets/Scenes/Level_02.unity";
    private const string LayoutPath = "Assets/Data/Maps/ConfluenceLayout.asset";
    private const string PathMaterialPath =
        "Assets/Art/Generated/Level01/Level01PathFill.mat";

    private const string PathCopyPath =
        "Assets/Art/Generated/Confluence/ConfluencePath.mat";

    private const string BridgeCopyPath =
        "Assets/Art/Generated/Confluence/ConfluenceBridge.mat";

    private const string ChannelCopyPath =
        "Assets/Art/Generated/Confluence/ConfluenceChannel.mat";

    private const string ClosedCopyPath =
        "Assets/Art/Generated/Confluence/ConfluencePassageClosed.mat";

    private const string BaseCopyPath =
        "Assets/Art/Generated/Confluence/ConfluenceBase.mat";

    private const string RepairMenuPath =
        "Bastion del Valle/Mapas/21 - Reparar Confluencia Base";

    private const string DiagnoseMenuPath =
        "Bastion del Valle/Mapas/21 - Diagnosticar paso";

    private static string UndoName = "Crear Confluencia Base";

    private static readonly string[] MemoryIds =
    {
        "BIO_POLINIZADOR_01",
        "memory_water_01",
        "memory_soil_01"
    };

    private static readonly Color PathTint = new(0.769f, 0.588f, 0.384f, 1f);
    private static readonly Color BridgeTint = new(0.86f, 0.70f, 0.48f, 1f);
    private static readonly Color ChannelTint = new(0.24f, 0.43f, 0.55f, 1f);
    private static readonly Color ClosedTint = new(0.851f, 0.322f, 0.290f, 1f);

    [MenuItem(MenuPath, false, 21)]
    private static void Create()
    {
        UndoName = "Crear Confluencia Base";

        if (IsBlocked())
        {
            Report("La herramienta solo puede ejecutarse fuera de Play Mode.");
            return;
        }

        string dirty = FindDirtyScene();

        if (dirty != null)
        {
            Report(
                "Hay cambios sin guardar en " + dirty +
                ". Guárdalos o descártalos antes. No se modificó ninguna escena."
            );
            return;
        }

        if (!ValidateDependencies(out string dependencyError))
        {
            Report(dependencyError);
            return;
        }

        Material source = AssetDatabase.LoadAssetAtPath<Material>(PathMaterialPath);
        EnsureFolders();
        Material pathMaterial = EnsureTintedMaterial(PathCopyPath, source, PathTint);
        Material bridgeMaterial = EnsureTintedMaterial(BridgeCopyPath, source, BridgeTint);
        Material channelMaterial = EnsureTintedMaterial(ChannelCopyPath, source, ChannelTint);
        Material closedMaterial = EnsureTintedMaterial(ClosedCopyPath, source, ClosedTint);
        Material baseMaterial = EnsureBaseMaterial(source);
        bool layoutCreated = EnsureLayout();
        ConfluenceLayout layout = AssetDatabase.LoadAssetAtPath<ConfluenceLayout>(LayoutPath);

        if (!ValidateLayout(layout, out string layoutError))
        {
            Report(layoutError);
            return;
        }

        bool createdScene = false;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Level02Path) == null)
        {
            if (!TryCopyLevel(out string copyError))
            {
                Report(copyError);
                return;
            }

            createdScene = true;
        }

        Scene level2 = EditorSceneManager.OpenScene(
            Level02Path,
            OpenSceneMode.Single
        );

        if (!ValidateScene(level2, out string sceneError))
        {
            if (createdScene)
            {
                DiscardCreatedScene();
            }

            Report(sceneError);
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);

        try
        {
            string report = Apply(
                level2,
                layout,
                layoutCreated,
                pathMaterial,
                bridgeMaterial,
                channelMaterial,
                closedMaterial,
                baseMaterial
            );

            Undo.CollapseUndoOperations(group);

            if (!EditorSceneManager.SaveScene(level2))
            {
                Report(
                    "Level_02 quedó configurada en memoria, pero no se pudo guardar."
                );
                return;
            }

            RegisterBuildScene(out string startup);
            AssetDatabase.SaveAssetIfDirty(layout);
            Report(report + "\nEscena inicial de build: " + startup);
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(group);

            if (createdScene)
            {
                DiscardCreatedScene();
            }

            Debug.LogException(exception);
            Report(
                "No se guardó la configuración de Level_02.\n" +
                exception.Message
            );
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenu()
    {
        return !IsBlocked();
    }

    [MenuItem(RepairMenuPath, false, 22)]
    private static void Repair()
    {
        if (IsBlocked())
        {
            Report("La reparación solo puede ejecutarse fuera de Play Mode.");
            return;
        }

        string dirty = FindDirtyScene();

        if (dirty != null)
        {
            Report(
                "Hay cambios sin guardar en " + dirty +
                ". Guárdalos o descártalos antes. No se modificó ninguna escena."
            );
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Level02Path) == null)
        {
            Report(
                "No existe Level_02. Ejecuta antes " +
                "Bastion del Valle/Mapas/21 - Crear Confluencia Base. " +
                "Esta reparación no copia Level_01."
            );
            return;
        }

        ConfluenceLayout layout =
            AssetDatabase.LoadAssetAtPath<ConfluenceLayout>(LayoutPath);

        if (!ValidateLayout(layout, out string layoutError))
        {
            Report(layoutError);
            return;
        }

        Scene level2 = EditorSceneManager.OpenScene(
            Level02Path,
            OpenSceneMode.Single
        );

        if (!ValidateRepairScene(level2, out string sceneError))
        {
            Report(sceneError);
            return;
        }

        string previousUndo = UndoName;
        UndoName = "Reparar Confluencia Base";
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);

        try
        {
            string report = ApplyRepair(level2, layout);
            var compact = new StringBuilder();
            CompactMatchHud.Apply(level2, compact);
            report += "\n" + compact;
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(level2);

            if (!EditorSceneManager.SaveScene(level2))
            {
                Report(
                    report +
                    "\nLa cuadrícula quedó asignada en memoria, pero Level_02 no se pudo guardar."
                );
                return;
            }

            Report(
                report +
                "\nLevel_02 quedó guardada con ConfluenceLayout. " +
                "No se guardó ninguna otra escena."
            );
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(exception);
            Report(
                "No se aplicó la reparación de Level_02.\n" +
                exception.Message
            );
        }
        finally
        {
            UndoName = previousUndo;
        }
    }

    [MenuItem(RepairMenuPath, true)]
    private static bool ValidateRepairMenu()
    {
        return !IsBlocked();
    }

    [MenuItem(DiagnoseMenuPath, false, 23)]
    private static void DiagnosePassage()
    {
        if (EditorApplication.isCompiling)
        {
            Report("Espera a que termine la compilación.");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            DiagnosePassageInPlay();
            return;
        }

        ConfluenceLayout layout =
            AssetDatabase.LoadAssetAtPath<ConfluenceLayout>(LayoutPath);

        if (layout == null)
        {
            Report(
                "No está Assets/Data/Maps/ConfluenceLayout.asset. " +
                "No se modificó ninguna escena."
            );
            return;
        }

        NavigationGrid open = layout.CreateGrid(true);
        NavigationGrid closed = layout.CreateGrid(false);
        string topOpen = DescribeRoute(
            open,
            layout.TopSpawn,
            layout.Goal,
            "superior abierto"
        );
        string bottomOpen = DescribeRoute(
            open,
            layout.BottomSpawn,
            layout.Goal,
            "inferior abierto"
        );
        string topClosed = DescribeRoute(
            closed,
            layout.TopSpawn,
            layout.Goal,
            "superior con el paso cerrado"
        );
        string bottomClosed = DescribeRoute(
            closed,
            layout.BottomSpawn,
            layout.Goal,
            "inferior con el paso cerrado"
        );
        var origins = new List<GridPoint>
        {
            new(layout.TopSpawn.x, layout.TopSpawn.y),
            new(layout.BottomSpawn.x, layout.BottomSpawn.y)
        };
        PassageVerdict verdict = PassageChangeRules.Evaluate(
            open,
            true,
            layout.Passage.x,
            layout.Passage.y,
            origins,
            layout.Goal.x,
            layout.Goal.y,
            false
        );
        Report(
            "Diagnóstico de editor, sin cambiar la escena.\n" +
            topOpen + "\n" +
            bottomOpen + "\n" +
            topClosed + "\n" +
            bottomClosed + "\n" +
            "Cierre seguro del paso: " + verdict.Detail
        );
    }

    [MenuItem(DiagnoseMenuPath, true)]
    private static bool ValidateDiagnoseMenu()
    {
        return !EditorApplication.isCompiling;
    }

    private static string Apply(
        Scene scene,
        ConfluenceLayout layout,
        bool layoutCreated,
        Material pathMaterial,
        Material bridgeMaterial,
        Material channelMaterial,
        Material closedMaterial,
        Material baseMaterial)
    {
        var report = new StringBuilder();
        ConfluenceMap map = EnsureMap(scene);
        ChannelNavigator navigator = EnsureComponent<ChannelNavigator>(map.gameObject);
        PassageMarker marker = EnsureComponent<PassageMarker>(map.gameObject);
        AssignObject(map, "layout", layout);
        AssignObject(navigator, "layout", layout);

        int walkable = 0;
        int channels = 0;
        MeshRenderer passageTile = RebuildGeometry(
            map.transform,
            layout,
            pathMaterial,
            bridgeMaterial,
            channelMaterial,
            baseMaterial,
            ref walkable,
            ref channels
        );

        AssignObject(marker, "passageTile", passageTile);
        AssignObject(marker, "openMaterial", pathMaterial);
        AssignObject(marker, "closedMaterial", closedMaterial);
        AssignObject(navigator, "marker", marker);

        bool retired = ReadBool(map, "distributionRetired");

        if (!retired)
        {
            DisableNamed(scene, "LevelVisuals");
            DisableNamed(scene, "Ground");
            DisableNamed(scene, "Waypoints");
            DisableWaypoints(scene);
            SetBool(map, "distributionRetired", true);
            report.AppendLine(
                "Se retiró de la copia la distribución visual exclusiva de Level_01."
            );
        }

        PlaceHeart(scene, layout);
        EnemySpawner upper = PlaceAccesses(scene, layout, navigator);
        EnemySpawner lower = FindAccess(scene, 1).GetComponent<EnemySpawner>();
        PlacePlots(scene, layout, report);
        PlaceCamera(scene, layout);
        PlaceWanderArea(scene, layout);
        AssignAccesses(scene, upper, lower);

        if (!ReadBool(map, "balanceApplied"))
        {
            ApplyBalance(scene);
            SetBool(map, "balanceApplied", true);
            report.AppendLine(
                "Balance inicial: integridad 100, semillas 120, " +
                "oleadas 6 × 1.8 s y 8 × 1.5 s."
            );
        }
        else
        {
            report.AppendLine(
                "El balance ya estaba aplicado y se conservó."
            );
        }

        if (!ReadBool(map, "educationApplied"))
        {
            ApplyEducation(scene);
            SetBool(map, "educationApplied", true);
            report.AppendLine(
                "Memorias del mapa 2: " + string.Join(", ", MemoryIds) +
                ". No se creó contenido nuevo."
            );
        }

        RemovePassageControls(scene);
        report.AppendLine(
            "Celdas transitables dibujadas: " + walkable +
            ". Canales no transitables: " + channels + "."
        );
        report.AppendLine(
            layoutCreated
                ? "Se creó Assets/Data/Maps/ConfluenceLayout.asset."
                : "Se conservó la cuadrícula ya existente."
        );
        report.AppendLine(
            "Level_02 usa un WaveManager. Cada enemigo sortea un acceso " +
            "conectado, con la misma probabilidad inicial."
        );
        report.AppendLine(
            "El terreno de fondo no describe la navegación. " +
            "Las celdas nombradas Cell, Bridge, Channel y PassageCell sí."
        );
        return report.ToString();
    }

    private static ConfluenceMap EnsureMap(Scene scene)
    {
        List<ConfluenceMap> maps = FindAll<ConfluenceMap>(scene);

        if (maps.Count > 0)
        {
            return maps[0];
        }

        GameObject root = new GameObject("Confluence");
        Undo.RegisterCreatedObjectUndo(root, UndoName);
        return Undo.AddComponent<ConfluenceMap>(root);
    }

    private static MeshRenderer RebuildGeometry(
        Transform map,
        ConfluenceLayout layout,
        Material pathMaterial,
        Material bridgeMaterial,
        Material channelMaterial,
        Material baseMaterial,
        ref int walkable,
        ref int channels)
    {
        Transform tiles = Child(map, "Celdas", false);
        ClearChildren(tiles);
        CreateBase(map, layout, baseMaterial);
        MeshRenderer passage = null;
        ConfluenceCell[] cells = layout.Cells;

        if (cells == null)
        {
            return null;
        }

        for (int i = 0; i < cells.Length; i++)
        {
            ConfluenceCell cell = cells[i];
            bool isPassage =
                cell.x == layout.Passage.x &&
                cell.y == layout.Passage.y;
            Material material = channelMaterial;
            string prefix = "Channel";
            int order = -20;

            if (isPassage)
            {
                material = pathMaterial;
                prefix = "PassageCell";
                order = -8;
                walkable++;
            }
            else if (cell.walkable && cell.kind == ConfluenceCellKind.Bridge)
            {
                material = bridgeMaterial;
                prefix = "Bridge";
                order = -9;
                walkable++;
            }
            else if (cell.walkable)
            {
                material = pathMaterial;
                prefix = "Cell";
                order = -10;
                walkable++;
            }
            else
            {
                channels++;
            }

            Vector3 center = layout.CenterOf(cell.x, cell.y);
            float size = cell.walkable
                ? layout.CellSize * 1.02f
                : layout.CellSize * 0.96f;
            MeshRenderer renderer = CreateTile(
                tiles,
                prefix + "_" + cell.x + "_" + cell.y,
                center,
                size,
                1f,
                material,
                order
            );

            if (isPassage)
            {
                passage = renderer;
            }
        }

        return passage;
    }

    private static void CreateBase(
        Transform map,
        ConfluenceLayout layout,
        Material material)
    {
        if (material == null)
        {
            return;
        }

        Transform existing = map.Find("Terreno");

        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        float width = layout.Width * layout.CellSize;
        float height = layout.Height * layout.CellSize;
        Vector3 center = new Vector3(
            layout.Origin.x + width * 0.5f,
            layout.Origin.y + height * 0.5f,
            1.2f
        );
        Camera camera = FindCamera(map.gameObject.scene);

        if (camera != null && camera.orthographic)
        {
            float halfHeight = camera.orthographicSize + 1.5f;
            float halfWidth = Mathf.Max(
                camera.orthographicSize * 2f,
                camera.orthographicSize * 16f / 9f
            ) + 1.5f;
            width = Mathf.Max(width, halfWidth * 2f);
            height = Mathf.Max(height, halfHeight * 2f);
            center = new Vector3(
                camera.transform.position.x,
                camera.transform.position.y,
                1.2f
            );
            Undo.RecordObject(camera, UndoName);
            camera.backgroundColor = new Color(0.16f, 0.24f, 0.16f, 1f);
        }

        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tile.name = "Terreno";
        Undo.RegisterCreatedObjectUndo(tile, UndoName);
        tile.transform.SetParent(map, false);
        tile.transform.position = center;
        tile.transform.localScale = new Vector3(width, height, 1f);
        RemoveCollider(tile);
        MeshFilter filter = tile.GetComponent<MeshFilter>();
        filter.sharedMesh = CoverMesh(material, width, height);
        MeshRenderer renderer = tile.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = -40;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private const string GroundMeshPath =
        "Assets/Art/Generated/Confluence/ConfluenceGround.asset";

    private static Mesh CoverMesh(Material material, float width, float height)
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GroundMeshPath);

        if (mesh == null)
        {
            mesh = new Mesh { name = "ConfluenceGround" };
            AssetDatabase.CreateAsset(mesh, GroundMeshPath);
        }

        mesh.Clear();
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f)
        };
        Texture texture = material == null ? null : material.GetTexture("_MainTex");
        float textureAspect = texture != null && texture.height > 0
            ? (float)texture.width / texture.height
            : 16f / 9f;
        float quadAspect = height <= 0.01f ? 1f : width / height;
        float u0 = 0f;
        float u1 = 1f;
        float v0 = 0f;
        float v1 = 1f;

        if (quadAspect > textureAspect && quadAspect > 0f)
        {
            float visible = textureAspect / quadAspect;
            float inset = (1f - visible) * 0.5f;
            v0 = inset;
            v1 = 1f - inset;
        }
        else if (textureAspect > 0f)
        {
            float visible = quadAspect / textureAspect;
            float inset = (1f - visible) * 0.5f;
            u0 = inset;
            u1 = 1f - inset;
        }

        mesh.uv = new[]
        {
            new Vector2(u0, v0),
            new Vector2(u0, v1),
            new Vector2(u1, v1),
            new Vector2(u1, v0)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static MeshRenderer CreateTile(
        Transform parent,
        string name,
        Vector3 center,
        float size,
        float z,
        Material material,
        int sortingOrder)
    {
        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
        tile.name = name;
        Undo.RegisterCreatedObjectUndo(tile, UndoName);
        tile.transform.SetParent(parent, false);
        tile.transform.position = new Vector3(center.x, center.y, z);
        tile.transform.localScale = new Vector3(size, size, 1f);
        RemoveCollider(tile);
        MeshRenderer renderer = tile.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static void PlaceHeart(Scene scene, ConfluenceLayout layout)
    {
        BastionHealth health = FindAll<BastionHealth>(scene)[0];
        Vector3 center = layout.CenterOf(layout.Goal.x, layout.Goal.y);
        Undo.RecordObject(health.transform, UndoName);
        health.transform.position = new Vector3(center.x, center.y, 0f);
    }

    private static EnemySpawner PlaceAccesses(
        Scene scene,
        ConfluenceLayout layout,
        ChannelNavigator navigator)
    {
        List<ConfluenceAccess> accesses = FindAll<ConfluenceAccess>(scene);

        if (accesses.Count == 0)
        {
            EnemySpawner original = FindAll<EnemySpawner>(scene)[0];
            ConfluenceAccess created = Undo.AddComponent<ConfluenceAccess>(
                original.gameObject
            );
            SetInt(created, "index", 0);
            accesses.Add(created);
        }

        if (FindAccess(accesses, 1) == null)
        {
            ConfluenceAccess template = FindAccess(accesses, 0) ?? accesses[0];
            GameObject clone = UnityEngine.Object.Instantiate(
                template.gameObject,
                template.transform.parent
            );
            clone.name = "Spawn_Lower";
            Undo.RegisterCreatedObjectUndo(clone, UndoName);
            ConfluenceAccess cloned = clone.GetComponent<ConfluenceAccess>();

            if (cloned == null)
            {
                cloned = Undo.AddComponent<ConfluenceAccess>(clone);
            }

            SetInt(cloned, "index", 1);
            accesses.Add(cloned);
        }

        PlaceAccess(FindAccess(scene, 0), layout.TopSpawn, layout, navigator, "Spawn_Upper");
        PlaceAccess(FindAccess(scene, 1), layout.BottomSpawn, layout, navigator, "Spawn_Lower");
        return FindAccess(scene, 0).GetComponent<EnemySpawner>();
    }

    private static void PlaceAccess(
        ConfluenceAccess access,
        Vector2Int cell,
        ConfluenceLayout layout,
        ChannelNavigator navigator,
        string fallbackName)
    {
        if (access.name == "EnemySpawner" || access.name.EndsWith("(Clone)"))
        {
            access.name = fallbackName;
        }

        Vector3 center = layout.CenterOf(cell.x, cell.y);
        Undo.RecordObject(access.transform, UndoName);
        access.transform.position = new Vector3(center.x, center.y, 0f);
        EnemySpawner spawner = access.GetComponent<EnemySpawner>();
        AssignObject(spawner, "enemyPath", null);
        AssignObject(spawner, "navigator", navigator);
    }

    private static void PlacePlots(
        Scene scene,
        ConfluenceLayout layout,
        StringBuilder report)
    {
        Vector2Int[] requested = layout.Plots ?? Array.Empty<Vector2Int>();
        List<ConfluencePlot> owned = FindAll<ConfluencePlot>(scene);
        List<BuildPlot> available = new();

        foreach (BuildPlot plot in FindAll<BuildPlot>(scene))
        {
            if (plot.GetComponent<ConfluencePlot>() == null)
            {
                available.Add(plot);
            }
        }

        available.Sort((left, right) =>
            string.CompareOrdinal(left.name, right.name));

        int cursor = 0;

        while (owned.Count < requested.Length && cursor < available.Count)
        {
            ConfluencePlot marker = Undo.AddComponent<ConfluencePlot>(
                available[cursor].gameObject
            );
            SetInt(marker, "index", owned.Count);
            owned.Add(marker);
            cursor++;
        }

        while (owned.Count < requested.Length && owned.Count > 0)
        {
            BuildPlot template = owned[0].GetComponent<BuildPlot>();
            GameObject clone = UnityEngine.Object.Instantiate(
                template.gameObject,
                template.transform.parent
            );
            clone.name = "BuildPlot_" + owned.Count.ToString("00");
            Undo.RegisterCreatedObjectUndo(clone, UndoName);
            ConfluencePlot marker = clone.GetComponent<ConfluencePlot>();

            if (marker == null)
            {
                marker = Undo.AddComponent<ConfluencePlot>(clone);
            }

            SetInt(marker, "index", owned.Count);
            owned.Add(marker);
        }

        for (int i = 0; i < requested.Length; i++)
        {
            ConfluencePlot plot = FindPlot(owned, i);

            if (plot == null)
            {
                continue;
            }

            Vector3 center = layout.CenterOf(requested[i].x, requested[i].y);
            Undo.RecordObject(plot.transform, UndoName);
            plot.transform.position = new Vector3(center.x, center.y, 0f);
        }

        report.AppendLine(
            "Parcelas colocadas fuera de la ruta: " +
            Mathf.Min(owned.Count, requested.Length) + "."
        );
    }

    private static void PlaceCamera(Scene scene, ConfluenceLayout layout)
    {
        Camera camera = FindCamera(scene);
        float width = layout.Width * layout.CellSize;
        float height = layout.Height * layout.CellSize;
        float halfHeight = height * 0.5f + layout.VerticalCameraMargin;
        float halfWidth = width * 0.5f + 1f;
        float size = Mathf.Max(halfHeight, halfWidth * 9f / 16f);
        Undo.RecordObject(camera, UndoName);
        Undo.RecordObject(camera.transform, UndoName);
        camera.orthographic = true;
        camera.orthographicSize = size;
        Vector3 position = camera.transform.position;
        float z = position.z < 0f ? position.z : -10f;
        camera.transform.position = new Vector3(
            layout.Origin.x + width * 0.5f,
            layout.Origin.y + height * 0.5f,
            z
        );
    }

    private static void PlaceWanderArea(Scene scene, ConfluenceLayout layout)
    {
        List<WanderingArea2D> areas = FindAll<WanderingArea2D>(scene);

        if (areas.Count == 0)
        {
            return;
        }

        WanderingArea2D area = areas[0];
        BoxCollider2D box = area.GetComponent<BoxCollider2D>();
        Undo.RecordObject(area.transform, UndoName);
        area.transform.localScale = Vector3.one;
        float width = layout.Width * layout.CellSize;
        float height = layout.Height * layout.CellSize;
        area.transform.position = new Vector3(
            layout.Origin.x + width * 0.5f,
            layout.Origin.y + height * 0.5f,
            0f
        );

        if (box == null)
        {
            return;
        }

        Undo.RecordObject(box, UndoName);
        box.offset = Vector2.zero;
        box.size = new Vector2(width * 0.92f, height * 0.92f);
    }

    private static void AssignAccesses(
        Scene scene,
        EnemySpawner upper,
        EnemySpawner lower)
    {
        WaveManager waves = FindAll<WaveManager>(scene)[0];
        SerializedObject serialized = new(waves);
        SerializedProperty points = serialized.FindProperty("accessPoints");
        points.arraySize = 2;
        points.GetArrayElementAtIndex(0).objectReferenceValue = upper;
        points.GetArrayElementAtIndex(1).objectReferenceValue = lower;
        SerializedProperty single = serialized.FindProperty("enemySpawner");

        if (single != null)
        {
            single.objectReferenceValue = upper;
        }

        serialized.ApplyModifiedProperties();
    }

    private static void ApplyBalance(Scene scene)
    {
        WaveManager waves = FindAll<WaveManager>(scene)[0];
        SerializedObject serialized = new(waves);
        SerializedProperty list = serialized.FindProperty("waves");

        if (list.arraySize < 4)
        {
            list.arraySize = 2;
            WriteWave(list.GetArrayElementAtIndex(0), 6, 1.8f);
            WriteWave(list.GetArrayElementAtIndex(1), 8, 1.5f);
        }

        serialized.ApplyModifiedProperties();

        EconomyManager economy = FindAll<EconomyManager>(scene)[0];
        SetInt(economy, "startingResources", 120);
        BastionHealth health = FindAll<BastionHealth>(scene)[0];
        SetInt(health, "maxHealth", 100);
    }

    private static void ApplyEducation(Scene scene)
    {
        CollectibleSpawner spawner = FindAll<CollectibleSpawner>(scene)[0];
        SerializedObject serialized = new(spawner);
        serialized.FindProperty("maxDiscoveriesPerRun").intValue = 2;
        serialized.FindProperty("minimumSpawnDelay").floatValue = 1f;
        serialized.FindProperty("maximumSpawnDelay").floatValue = 2f;
        SerializedProperty list = serialized.FindProperty("availableCollectibles");
        list.arraySize = MemoryIds.Length;

        for (int i = 0; i < MemoryIds.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue =
                FindMemory(MemoryIds[i]);
        }

        serialized.ApplyModifiedProperties();
    }

    private static void RemovePassageControls(Scene scene)
    {
        RemoveNamed(scene, "PassageToggle");
        RemoveNamed(scene, "PassageState");
        RemoveNamed(scene, "PassageRow");
    }

    private static void RemoveNamed(Scene scene, string objectName)
    {
        Transform named = FindNamed(scene, objectName);

        if (named == null)
        {
            return;
        }

        Undo.DestroyObjectImmediate(named.gameObject);
    }

    private static bool EnsureLayout()
    {
        if (AssetDatabase.LoadAssetAtPath<ConfluenceLayout>(LayoutPath) != null)
        {
            return false;
        }

        ConfluenceLayout layout = ScriptableObject.CreateInstance<ConfluenceLayout>();
        AssetDatabase.CreateAsset(layout, LayoutPath);
        SerializedObject serialized = new(layout);
        serialized.FindProperty("cellSize").floatValue = 1f;
        serialized.FindProperty("origin").vector2Value = new Vector2(-8f, -5f);
        serialized.FindProperty("width").intValue = 16;
        serialized.FindProperty("height").intValue = 10;
        serialized.FindProperty("verticalCameraMargin").floatValue = 2.4f;
        serialized.FindProperty("topSpawn").vector2IntValue = new Vector2Int(1, 7);
        serialized.FindProperty("bottomSpawn").vector2IntValue = new Vector2Int(1, 2);
        serialized.FindProperty("goal").vector2IntValue = new Vector2Int(14, 5);
        serialized.FindProperty("passage").vector2IntValue = new Vector2Int(8, 5);
        WriteCells(serialized.FindProperty("cells"));
        WritePlots(serialized.FindProperty("plots"));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(layout);
        AssetDatabase.SaveAssetIfDirty(layout);
        return true;
    }

    private static void WriteCells(SerializedProperty cells)
    {
        var entries = new List<ConfluenceCell>();
        AddCells(
            entries,
            ConfluenceCellKind.Path,
            true,
            1, 7, 2, 7, 3, 7, 4, 7,
            1, 2, 2, 2, 3, 2, 4, 2,
            4, 5, 5, 5, 6, 5, 7, 5, 8, 5, 9, 5,
            10, 5, 11, 5, 12, 5, 13, 5, 14, 5,
            7, 7, 8, 7, 9, 7
        );
        AddCells(entries, ConfluenceCellKind.Bridge, true, 4, 6, 4, 3, 4, 4, 7, 6, 9, 6);
        AddCells(
            entries,
            ConfluenceCellKind.Channel,
            false,
            5, 6, 6, 6, 8, 6, 10, 6, 11, 6, 12, 6,
            5, 3, 6, 3, 7, 3, 8, 3, 9, 3, 10, 3, 11, 3,
            5, 4, 6, 4, 7, 4, 8, 4, 9, 4, 10, 4, 11, 4
        );
        cells.arraySize = entries.Count;

        for (int i = 0; i < entries.Count; i++)
        {
            SerializedProperty element = cells.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("x").intValue = entries[i].x;
            element.FindPropertyRelative("y").intValue = entries[i].y;
            element.FindPropertyRelative("walkable").boolValue = entries[i].walkable;
            element.FindPropertyRelative("cost").intValue = entries[i].cost;
            element.FindPropertyRelative("kind").enumValueIndex = (int)entries[i].kind;
        }
    }

    private static void WritePlots(SerializedProperty plots)
    {
        Vector2Int[] cells =
        {
            new(2, 8),
            new(3, 6),
            new(2, 1),
            new(3, 3),
            new(6, 7),
            new(13, 6)
        };
        plots.arraySize = cells.Length;

        for (int i = 0; i < cells.Length; i++)
        {
            plots.GetArrayElementAtIndex(i).vector2IntValue = cells[i];
        }
    }

    private static void AddCells(
        List<ConfluenceCell> cells,
        ConfluenceCellKind kind,
        bool walkable,
        params int[] coordinates)
    {
        for (int i = 0; i < coordinates.Length; i += 2)
        {
            cells.Add(new ConfluenceCell
            {
                x = coordinates[i],
                y = coordinates[i + 1],
                walkable = walkable,
                cost = 1,
                kind = kind
            });
        }
    }

    private static bool ValidateDependencies(out string error)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Level02Path) == null &&
            AssetDatabase.LoadAssetAtPath<SceneAsset>(Level01Path) == null)
        {
            error = "No está Level_01. No se creó Level_02.";
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<Material>(PathMaterialPath) == null)
        {
            error =
                "Falta el material de camino Level01PathFill. " +
                "No se modificó ninguna escena.";
            return false;
        }

        for (int i = 0; i < MemoryIds.Length; i++)
        {
            if (FindMemory(MemoryIds[i]) == null)
            {
                error =
                    "Falta la memoria " + MemoryIds[i] +
                    ". No se creó contenido nuevo ni se modificó la escena.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool ValidateLayout(ConfluenceLayout layout, out string error)
    {
        if (layout == null || layout.Width <= 0 || layout.Height <= 0)
        {
            error = "La cuadrícula de confluencia no tiene tamaño válido.";
            return false;
        }

        if (!IsWalkable(layout, layout.TopSpawn) ||
            !IsWalkable(layout, layout.BottomSpawn) ||
            !IsWalkable(layout, layout.Goal) ||
            !IsWalkable(layout, layout.Passage))
        {
            error =
                "Apariciones, objetivo o paso deben ser celdas transitables. " +
                "No se modificó la escena.";
            return false;
        }

        Vector2Int[] plots = layout.Plots;

        if (plots == null || plots.Length == 0)
        {
            error = "La confluencia no tiene parcelas.";
            return false;
        }

        for (int i = 0; i < plots.Length; i++)
        {
            if (IsWalkable(layout, plots[i]))
            {
                error =
                    "La parcela " + plots[i] +
                    " está sobre una celda transitable. No se modificó la escena.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool ValidateScene(Scene scene, out string error)
    {
        if (FindAll<WaveManager>(scene).Count != 1 ||
            FindAll<EconomyManager>(scene).Count != 1 ||
            FindAll<BastionHealth>(scene).Count != 1 ||
            FindAll<GameManager>(scene).Count != 1 ||
            FindAll<PauseManager>(scene).Count != 1 ||
            FindAll<HUDController>(scene).Count != 1 ||
            FindAll<CollectibleSpawner>(scene).Count != 1)
        {
            error =
                "Level_02 no tiene una sola copia de los coordinadores necesarios. " +
                "No se aplicó la distribución.";
            return false;
        }

        if (FindAll<EnemySpawner>(scene).Count < 1 ||
            FindAll<BuildPlot>(scene).Count < 1 ||
            FindCamera(scene) == null)
        {
            error =
                "Level_02 necesita aparición, parcela y cámara. " +
                "No se aplicó la distribución.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsWalkable(ConfluenceLayout layout, Vector2Int cell)
    {
        ConfluenceCell[] cells = layout.Cells;

        if (cells == null ||
            cell.x < 0 ||
            cell.y < 0 ||
            cell.x >= layout.Width ||
            cell.y >= layout.Height)
        {
            return false;
        }

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].x == cell.x &&
                cells[i].y == cell.y &&
                cells[i].walkable)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCopyLevel(out string error)
    {
        Scene source = SceneManager.GetSceneByPath(Level01Path);

        if (!source.IsValid() || !source.isLoaded)
        {
            source = EditorSceneManager.OpenScene(
                Level01Path,
                OpenSceneMode.Single
            );
        }

        if (!source.IsValid())
        {
            error = "No se pudo abrir Level_01 para crear la copia.";
            return false;
        }

        if (!EditorSceneManager.SaveScene(source, Level02Path, true))
        {
            error = "No se pudo escribir la copia inicial de Level_02.";
            return false;
        }

        error = null;
        return true;
    }

    private static void DiscardCreatedScene()
    {
        Scene active = SceneManager.GetActiveScene();

        if (active.path == Level02Path && active.isDirty)
        {
            EditorSceneManager.SaveScene(active);
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Level01Path) != null)
        {
            EditorSceneManager.OpenScene(Level01Path, OpenSceneMode.Single);
        }

        AssetDatabase.DeleteAsset(Level02Path);
    }

    private static void RegisterBuildScene(out string startup)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int index = scenes.FindIndex(scene => scene.path == Level02Path);

        if (index < 0)
        {
            scenes.Add(new EditorBuildSettingsScene(Level02Path, true));
        }
        else if (!scenes[index].enabled)
        {
            scenes[index] = new EditorBuildSettingsScene(Level02Path, true);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        startup = "(ninguna)";

        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].enabled)
            {
                startup = scenes[i].path;
                break;
            }
        }
    }

    private static void DisableWaypoints(Scene scene)
    {
        foreach (EnemyPath path in FindAll<EnemyPath>(scene))
        {
            SerializedObject serialized = new(path);
            SerializedProperty waypoints = serialized.FindProperty("waypoints");

            if (waypoints == null)
            {
                continue;
            }

            for (int i = 0; i < waypoints.arraySize; i++)
            {
                Transform point = waypoints.GetArrayElementAtIndex(i)
                    .objectReferenceValue as Transform;

                if (point == null)
                {
                    continue;
                }

                Undo.RecordObject(point.gameObject, UndoName);
                point.gameObject.SetActive(false);
            }
        }
    }

    private static void DisableNamed(Scene scene, string objectName)
    {
        Transform named = FindNamed(scene, objectName);

        if (named == null)
        {
            return;
        }

        Undo.RecordObject(named.gameObject, UndoName);
        named.gameObject.SetActive(false);
    }

    private static Transform Child(Transform parent, string name, bool rect)
    {
        Transform child = parent.Find(name);

        if (child != null)
        {
            return child;
        }

        GameObject created = rect
            ? new GameObject(name, typeof(RectTransform))
            : new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, UndoName);
        created.transform.SetParent(parent, false);
        created.transform.localScale = Vector3.one;
        return created.transform;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
        }
    }

    private static void RemoveCollider(GameObject tile)
    {
        Collider collider = tile.GetComponent<Collider>();

        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }
    }

    private static void WriteWave(SerializedProperty wave, int count, float interval)
    {
        wave.FindPropertyRelative("enemyCount").intValue = count;
        wave.FindPropertyRelative("spawnInterval").floatValue = interval;
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();

        if (component == null)
        {
            component = Undo.AddComponent<T>(target);
        }

        return component;
    }

    private static ConfluenceAccess FindAccess(Scene scene, int index)
    {
        return FindAccess(FindAll<ConfluenceAccess>(scene), index);
    }

    private static ConfluenceAccess FindAccess(
        List<ConfluenceAccess> accesses,
        int index)
    {
        for (int i = 0; i < accesses.Count; i++)
        {
            if (accesses[i].Index == index)
            {
                return accesses[i];
            }
        }

        return null;
    }

    private static ConfluencePlot FindPlot(List<ConfluencePlot> plots, int index)
    {
        for (int i = 0; i < plots.Count; i++)
        {
            if (plots[i].Index == index)
            {
                return plots[i];
            }
        }

        return null;
    }

    private static Camera FindCamera(Scene scene)
    {
        foreach (Camera camera in FindAll<Camera>(scene))
        {
            if (camera.CompareTag("MainCamera") || camera.name == "Main Camera")
            {
                return camera;
            }
        }

        List<Camera> cameras = FindAll<Camera>(scene);
        return cameras.Count == 0 ? null : cameras[0];
    }

    private static Button FindButton(Scene scene, string objectName)
    {
        Transform named = FindNamed(scene, objectName);
        return named == null ? null : named.GetComponent<Button>();
    }

    private static TMP_Text FindLabel(Scene scene, string objectName)
    {
        Transform named = FindNamed(scene, objectName);
        return named == null ? null : named.GetComponent<TMP_Text>();
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

    private static CollectibleData FindMemory(string id)
    {
        string[] guids = AssetDatabase.FindAssets("t:CollectibleData");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            CollectibleData data =
                AssetDatabase.LoadAssetAtPath<CollectibleData>(path);

            if (data != null && data.CollectibleId == id)
            {
                return data;
            }
        }

        return null;
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

    private static void AssignObject(
        UnityEngine.Object target,
        string fieldName,
        UnityEngine.Object value)
    {
        if (target == null)
        {
            throw new InvalidOperationException(
                "No hay objeto para asignar " + fieldName + "."
            );
        }

        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            throw new InvalidOperationException(
                target.name + " no tiene el campo " + fieldName + "."
            );
        }

        if (property.objectReferenceValue == value)
        {
            EditorUtility.SetDirty(target);
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);

        if (PrefabUtility.IsPartOfPrefabInstance(target))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }

    private static void SetInt(UnityEngine.Object target, string fieldName, int value)
    {
        SerializedObject serialized = new(target);
        serialized.FindProperty(fieldName).intValue = value;
        serialized.ApplyModifiedProperties();
    }

    private static void SetBool(UnityEngine.Object target, string fieldName, bool value)
    {
        SerializedObject serialized = new(target);
        serialized.FindProperty(fieldName).boolValue = value;
        serialized.ApplyModifiedProperties();
    }

    private static bool ReadBool(UnityEngine.Object target, string fieldName)
    {
        SerializedObject serialized = new(target);
        return serialized.FindProperty(fieldName).boolValue;
    }

    private static Material EnsureTintedMaterial(
        string path,
        Material source,
        Color color)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (existing != null)
        {
            return existing;
        }

        Material copy = new Material(source);
        Tint(copy, color);
        AssetDatabase.CreateAsset(copy, path);
        return copy;
    }

    private static Material EnsureBaseMaterial(Material fallback)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(BaseCopyPath);

        if (existing != null)
        {
            return existing;
        }

        Material terrain = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Art/Generated/Level01/Level01TerrainCover.mat"
        );

        if (terrain == null)
        {
            terrain = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Art/Generated/Level01/Level01Terrain.mat"
            );
        }

        Material copy = new Material(terrain != null ? terrain : fallback);

        if (terrain == null)
        {
            Tint(copy, new Color(0.45f, 0.52f, 0.38f, 1f));
        }

        AssetDatabase.CreateAsset(copy, BaseCopyPath);
        return copy;
    }

    private static void Tint(Material material, Color color)
    {
        if (material.HasProperty("_White"))
        {
            material.SetColor("_White", color);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data/Maps"))
        {
            AssetDatabase.CreateFolder("Assets/Data", "Maps");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Art/Generated/Confluence"))
        {
            AssetDatabase.CreateFolder("Assets/Art/Generated", "Confluence");
        }
    }

    private static bool IsBlocked()
    {
        return EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling;
    }

    private static string FindDirtyScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!scene.isDirty)
            {
                continue;
            }

            return string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
        }

        return null;
    }

    private static string ApplyRepair(Scene scene, ConfluenceLayout layout)
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(PathMaterialPath);
        EnsureFolders();
        Material pathMaterial = EnsureTintedMaterial(PathCopyPath, source, PathTint);
        Material bridgeMaterial = EnsureTintedMaterial(BridgeCopyPath, source, BridgeTint);
        Material channelMaterial = EnsureTintedMaterial(ChannelCopyPath, source, ChannelTint);
        Material closedMaterial = EnsureTintedMaterial(ClosedCopyPath, source, ClosedTint);
        Material baseMaterial = EnsureBaseMaterial(source);
        ConfluenceMap map = FindAll<ConfluenceMap>(scene)[0];
        ChannelNavigator navigator = FindAll<ChannelNavigator>(scene)[0];

        PassageMarker marker = EnsureComponent<PassageMarker>(map.gameObject);
        AssignObject(map, "layout", layout);
        AssignObject(map, "navigator", navigator);
        AssignObject(navigator, "layout", layout);
        RequireAssigned(map, "layout", layout);
        RequireAssigned(map, "navigator", navigator);
        RequireAssigned(navigator, "layout", layout);

        int walkable = 0;
        int channels = 0;
        MeshRenderer passageTile = RebuildGeometry(
            map.transform,
            layout,
            pathMaterial,
            bridgeMaterial,
            channelMaterial,
            baseMaterial,
            ref walkable,
            ref channels
        );
        AssignObject(marker, "passageTile", passageTile);
        AssignObject(marker, "openMaterial", pathMaterial);
        AssignObject(marker, "closedMaterial", closedMaterial);
        AssignObject(navigator, "marker", marker);
        BindSpawners(scene, navigator);
        RemovePassageControls(scene);
        RepairHud(scene);

        Camera camera = FindCamera(scene);
        float ortho = camera == null ? 0f : camera.orthographicSize;
        return
            "Se asignó ConfluenceLayout al navegador y al mapa.\n" +
            "Celdas transitables: " + walkable +
            ". Canales: " + channels + ".\n" +
            "Terreno de cobertura y HUD en tres bandas.\n" +
            "Se retiró Alternar paso del HUD. El tamaño ortográfico quedó en " +
            ortho.ToString("0.0") + ".";
    }

    private static void BindSpawners(Scene scene, ChannelNavigator navigator)
    {
        EnemyMovement prefab = LoadEnemyPrefab();
        GameManager manager = FindAll<GameManager>(scene)[0];
        BastionHealth bastion = FindAll<BastionHealth>(scene)[0];
        Transform container = FindNamed(scene, "Enemies");

        if (container != null && !container.gameObject.activeSelf)
        {
            Undo.RecordObject(container.gameObject, UndoName);
            container.gameObject.SetActive(true);
        }

        EnemySpawner upper = FindAccess(scene, 0).GetComponent<EnemySpawner>();
        EnemySpawner lower = FindAccess(scene, 1).GetComponent<EnemySpawner>();
        BindSpawner(upper, navigator, prefab, manager, bastion, container);
        BindSpawner(lower, navigator, prefab, manager, bastion, container);

        WaveManager waves = FindAll<WaveManager>(scene)[0];
        SerializedObject serialized = new(waves);
        SerializedProperty points = serialized.FindProperty("accessPoints");
        bool missingAccess =
            points.arraySize != 2 ||
            points.GetArrayElementAtIndex(0).objectReferenceValue != upper ||
            points.GetArrayElementAtIndex(1).objectReferenceValue != lower;

        if (missingAccess)
        {
            points.arraySize = 2;
            points.GetArrayElementAtIndex(0).objectReferenceValue = upper;
            points.GetArrayElementAtIndex(1).objectReferenceValue = lower;
        }

        SerializedProperty single = serialized.FindProperty("enemySpawner");

        if (single.objectReferenceValue == null)
        {
            single.objectReferenceValue = upper;
        }

        SerializedProperty list = serialized.FindProperty("waves");
        bool waveCount =
            list.arraySize == 2 &&
            list.GetArrayElementAtIndex(0).FindPropertyRelative("enemyCount").intValue == 6 &&
            Mathf.Approximately(
                list.GetArrayElementAtIndex(0).FindPropertyRelative("spawnInterval").floatValue,
                1.8f
            ) &&
            list.GetArrayElementAtIndex(1).FindPropertyRelative("enemyCount").intValue == 8 &&
            Mathf.Approximately(
                list.GetArrayElementAtIndex(1).FindPropertyRelative("spawnInterval").floatValue,
                1.5f
            );

        if (!waveCount && list.arraySize < 4)
        {
            list.arraySize = 2;
            WriteWave(list.GetArrayElementAtIndex(0), 6, 1.8f);
            WriteWave(list.GetArrayElementAtIndex(1), 8, 1.5f);
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(waves);
        RequireAssigned(upper, "navigator", navigator);
        RequireAssigned(lower, "navigator", navigator);
        RequirePresent(upper, "enemyPrefab");
        RequirePresent(lower, "enemyPrefab");
        RequirePresent(upper, "gameManager");
        RequirePresent(lower, "gameManager");
        RequirePresent(upper, "bastion");
        RequirePresent(lower, "bastion");
    }

    private static void BindSpawner(
        EnemySpawner spawner,
        ChannelNavigator navigator,
        EnemyMovement prefab,
        GameManager manager,
        BastionHealth bastion,
        Transform container)
    {
        if (spawner == null)
        {
            throw new InvalidOperationException(
                "Un acceso de Level_02 no tiene EnemySpawner."
            );
        }

        AssignIfMissing(spawner, "enemyPrefab", prefab);
        AssignIfMissing(spawner, "gameManager", manager);
        AssignIfMissing(spawner, "bastion", bastion);
        AssignIfMissing(spawner, "enemyContainer", container);
        AssignObject(spawner, "navigator", navigator);
        AssignObject(spawner, "enemyPath", null);
    }

    private static void AssignIfMissing(
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

        if (property.objectReferenceValue == null)
        {
            if (value == null)
            {
                throw new InvalidOperationException(
                    target.name + "." + fieldName + " sigue vacío."
                );
            }

            AssignObject(target, fieldName, value);
        }
    }

    private static EnemyMovement LoadEnemyPrefab()
    {
        const string guid = "22c0aaf7e6d004d718844c5abb476f4f";
        string path = AssetDatabase.GUIDToAssetPath(guid);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        EnemyMovement movement = prefab == null
            ? null
            : prefab.GetComponent<EnemyMovement>();

        if (movement == null)
        {
            throw new InvalidOperationException(
                "No se encontró el prefab de enemigo ya usado por el proyecto."
            );
        }

        return movement;
    }

    private static void RequirePresent(UnityEngine.Object target, string fieldName)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null || property.objectReferenceValue == null)
        {
            throw new InvalidOperationException(
                target.name + "." + fieldName + " sigue vacío."
            );
        }
    }

    private static void RequireAssigned(
        UnityEngine.Object target,
        string fieldName,
        UnityEngine.Object expected)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null || property.objectReferenceValue != expected)
        {
            throw new InvalidOperationException(
                target.name + "." + fieldName + " no quedó asignado."
            );
        }
    }

    private static bool ValidateRepairScene(Scene scene, out string error)
    {
        if (!ValidateScene(scene, out error))
        {
            return false;
        }

        if (FindAll<ConfluenceMap>(scene).Count != 1 ||
            FindAll<ChannelNavigator>(scene).Count != 1)
        {
            error =
                "Level_02 debe tener una ConfluenceMap y un ChannelNavigator. " +
                "No se modificó la escena.";
            return false;
        }

        if (FindAccess(scene, 0) == null || FindAccess(scene, 1) == null)
        {
            error =
                "Faltan los dos accesos. Esta reparación no copia Level_01 " +
                "ni crea accesos nuevos.";
            return false;
        }

        string[] required =
        {
            "HUD",
            "TopRow",
            "BottomRow",
            "StatusText",
            "DiscoveriesText",
            "BastionText",
            "ResourcesText",
            "WaveText",
            "EnemiesText",
            "StartWaveButton",
            "PauseButton",
            "AudioButton",
            "CodexButton"
        };

        for (int i = 0; i < required.Length; i++)
        {
            if (FindNamed(scene, required[i]) == null)
            {
                error = "Falta " + required[i] + " en Level_02. No se modificó la escena.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static void RepairHud(Scene scene)
    {
        Transform hud = FindNamed(scene, "HUD");
        Transform top = FindNamed(scene, "TopRow");
        Transform bottom = FindNamed(scene, "BottomRow");
        Transform message = Child(hud, "MessageRow", true);
        RemoveRowFitter(top);
        RemoveRowFitter(message);
        RemoveRowFitter(bottom);
        ConfigureBand(
            top,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -8f),
            new Vector2(-32f, 80f)
        );
        ConfigureBand(
            message,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 100f),
            new Vector2(-32f, 84f)
        );
        ConfigureBand(
            bottom,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 12f),
            new Vector2(-32f, 76f)
        );
        MoveUnder(FindNamed(scene, "StatusText"), message);
        MoveUnder(FindNamed(scene, "DiscoveriesText"), message);
        FitText(FindNamed(scene, "BastionText"), 240f, 320f, 1f, 52f, false);
        FitText(FindNamed(scene, "ResourcesText"), 240f, 320f, 1f, 52f, false);
        FitText(FindNamed(scene, "WaveText"), 220f, 280f, 1f, 52f, false);
        FitText(FindNamed(scene, "EnemiesText"), 220f, 280f, 1f, 52f, false);
        FitText(FindNamed(scene, "StatusText"), 640f, 880f, 1f, 64f, true);
        FitText(FindNamed(scene, "DiscoveriesText"), 460f, 520f, 0f, 64f, false);
        FitText(FindNamed(scene, "StartWaveButton"), 210f, 230f, 0f, 52f, false);
        FitText(FindNamed(scene, "PauseButton"), 160f, 180f, 0f, 52f, false);
        FitText(FindNamed(scene, "AudioButton"), 160f, 180f, 0f, 52f, false);
        FitText(FindNamed(scene, "CodexButton"), 160f, 180f, 0f, 52f, false);
    }

    private static void ConfigureBand(
        Transform row,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchored,
        Vector2 size)
    {
        RectTransform rect = (RectTransform)row;
        Undo.RecordObject(rect, UndoName);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchored;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        if (row.GetComponent<CanvasRenderer>() == null)
        {
            Undo.AddComponent<CanvasRenderer>(row.gameObject);
        }

        Image image = row.GetComponent<Image>();

        if (image == null)
        {
            image = Undo.AddComponent<Image>(row.gameObject);
        }

        Undo.RecordObject(image, UndoName);
        image.color = new Color(0.149f, 0.239f, 0.188f, 0.94f);
        image.raycastTarget = true;
        HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();

        if (group == null)
        {
            group = Undo.AddComponent<HorizontalLayoutGroup>(row.gameObject);
        }

        Undo.RecordObject(group, UndoName);
        group.padding = new RectOffset(16, 16, 8, 8);
        group.spacing = 12f;
        group.childAlignment = TextAnchor.MiddleLeft;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
        group.childScaleWidth = false;
        group.childScaleHeight = false;
        EditorUtility.SetDirty(row.gameObject);
    }

    private static void FitText(
        Transform target,
        float minWidth,
        float preferredWidth,
        float flexibleWidth,
        float minHeight,
        bool wrap)
    {
        LayoutElement element = target.GetComponent<LayoutElement>();

        if (element == null)
        {
            element = Undo.AddComponent<LayoutElement>(target.gameObject);
        }

        Undo.RecordObject(element, UndoName);
        element.minWidth = minWidth;
        element.preferredWidth = preferredWidth;
        element.flexibleWidth = flexibleWidth;
        element.minHeight = minHeight;
        element.preferredHeight = minHeight;
        element.flexibleHeight = 0f;
        element.layoutPriority = 1;
        TMP_Text text = target.GetComponent<TMP_Text>();

        if (text == null)
        {
            text = target.GetComponentInChildren<TMP_Text>(true);
        }

        if (text != null)
        {
            Undo.RecordObject(text, UndoName);
            text.enableWordWrapping = wrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.enableAutoSizing = false;
            EditorUtility.SetDirty(text);
        }

        EditorUtility.SetDirty(element);
    }

    private static void MoveUnder(Transform child, Transform parent)
    {
        if (child.parent == parent)
        {
            child.localScale = Vector3.one;
            return;
        }

        Undo.SetTransformParent(child, parent, UndoName);
        child.localScale = Vector3.one;
    }

    private static void RemoveRowFitter(Transform row)
    {
        ContentSizeFitter fitter = row.GetComponent<ContentSizeFitter>();

        if (fitter != null)
        {
            Undo.DestroyObjectImmediate(fitter);
        }
    }

    private static string DescribeRoute(
        NavigationGrid grid,
        Vector2Int from,
        Vector2Int goal,
        string label)
    {
        GridPathResult result = GridPathfinder.Find(
            grid,
            from.x,
            from.y,
            goal.x,
            goal.y
        );
        return label + ": " + result.Status + ". " + result.Detail;
    }

    private static void DiagnosePassageInPlay()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (scene.path != Level02Path)
        {
            Report(
                "En Play Mode el diagnóstico solo actúa sobre Level_02. " +
                "Escena activa: " + scene.path
            );
            return;
        }

        List<GameManager> games = FindAll<GameManager>(scene);
        List<PauseManager> pauses = FindAll<PauseManager>(scene);
        List<ChannelNavigator> navigators = FindAll<ChannelNavigator>(scene);

        if (games.Count != 1 || pauses.Count != 1 || navigators.Count != 1)
        {
            Report(
                "Hacen falta referencias únicas de GameManager, PauseManager " +
                "y ChannelNavigator en Level_02. No se cambió el paso."
            );
            return;
        }

        if (!games[0].IsPlaying || pauses[0].IsPaused)
        {
            Report(
                "La partida no está activa o está en pausa. No se cambió el paso. " +
                navigators[0].PassageLabel
            );
            return;
        }

        bool changed = navigators[0].TryToggle(
            games[0],
            pauses[0],
            out string detail
        );
        Report(
            (changed ? "El paso cambió. " : "El paso se conservó. ") +
            detail
        );
    }

    private static void Report(string message)
    {
        EditorUtility.DisplayDialog("Bastión del Valle", message, "Entendido");
        Debug.Log(message);
    }
}
