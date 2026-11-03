using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlotSpriteConfigurator
{
    private const string MenuPath =
        "Bastion del Valle/Incrementos/14 - Configurar Parcelas";

    private const string SpritePath =
        "Assets/Art/Environment/build_plot_idle_v001.png";

    private const string ScenePath =
        "Assets/Scenes/Level_01.unity";

    private const string VisualChildName = "Visual";
    private const string UndoName = "Configurar parcelas";

    private const string BuiltinSquareGuid =
        "311925a002f4447b3a28927169b83ea6";

    private const string BuiltinKnobGuid =
        "a86470a33a6bf42c4b3595704624658b";

    [MenuItem(MenuPath, true)]
    private static bool ValidateMenu()
    {
        return !IsPlayMode() && !EditorApplication.isCompiling;
    }

    [MenuItem(MenuPath)]
    private static void Configure()
    {
        if (IsPlayMode())
        {
            Report(
                "Detenido",
                "Sal de Play Mode antes de configurar las parcelas."
            );
            return;
        }

        if (!TryGetOpenScene(out Scene scene, out string error))
        {
            Report("Detenido", error);
            return;
        }

        if (!ValidateSpriteAsset(out error))
        {
            Report("Detenido", error);
            return;
        }

        if (!TryInspectPlots(
                scene,
                out List<PlotPlan> plans,
                out TowerSorting towerSorting,
                out error))
        {
            Report("Detenido", error);
            return;
        }

        bool needsSprite = false;

        foreach (PlotPlan plan in plans)
        {
            if (plan.assignSprite)
            {
                needsSprite = true;
                break;
            }
        }

        bool importChanged = false;

        if (needsSprite)
        {
            importChanged = ConfigureImporter(out error);

            if (error != null)
            {
                Report("Detenido", error);
                return;
            }

            if (!LoadPlotSprite(out Sprite plotSprite, out error))
            {
                Report(
                    "Importación actualizada",
                    error + "\nLas parcelas no se modificaron."
                );
                return;
            }

            ApplyPlans(scene, plans, plotSprite, importChanged, towerSorting);
            return;
        }

        ApplyPlans(scene, plans, null, importChanged, towerSorting);
    }

    private static bool IsPlayMode()
    {
        return EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static bool TryGetOpenScene(
        out Scene scene,
        out string error)
    {
        scene = SceneManager.GetSceneByPath(ScenePath);
        error = null;

        if (!scene.IsValid() || !scene.isLoaded)
        {
            error =
                "Abre " + ScenePath + " antes de configurar las parcelas. " +
                "No se cambió de escena.";
            return false;
        }

        return true;
    }

    private static bool ValidateSpriteAsset(out string error)
    {
        error = null;

        if (AssetImporter.GetAtPath(SpritePath) is not TextureImporter)
        {
            error =
                "No se encontró la imagen importable en " +
                SpritePath + ".";
            return false;
        }

        return true;
    }

    private static bool TryInspectPlots(
        Scene scene,
        out List<PlotPlan> plans,
        out TowerSorting towerSorting,
        out string error)
    {
        plans = new List<PlotPlan>();
        towerSorting = default;
        error = null;
        List<BuildPlot> plots = new();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            plots.AddRange(
                root.GetComponentsInChildren<BuildPlot>(true)
            );
        }

        if (plots.Count == 0)
        {
            error = "Level_01 no tiene componentes BuildPlot.";
            return false;
        }

        plots.Sort(ComparePlots);

        if (!TryReadTowerSorting(plots, out towerSorting, out error))
        {
            return false;
        }

        HashSet<int> renderers = new();
        HashSet<int> colliders = new();

        foreach (BuildPlot plot in plots)
        {
            if (!TryBuildPlan(
                    plot,
                    towerSorting,
                    renderers,
                    colliders,
                    out PlotPlan plan,
                    out error))
            {
                plans.Clear();
                return false;
            }

            plans.Add(plan);
        }

        return true;
    }

    private static int ComparePlots(BuildPlot left, BuildPlot right)
    {
        return string.CompareOrdinal(
            left.gameObject.name,
            right.gameObject.name
        );
    }

    private static bool TryReadTowerSorting(
        List<BuildPlot> plots,
        out TowerSorting sorting,
        out string error)
    {
        sorting = default;
        error = null;
        bool found = false;

        foreach (BuildPlot plot in plots)
        {
            TowerController tower = ReadTowerPrefab(plot);

            if (tower == null)
            {
                error =
                    plot.gameObject.name +
                    " no tiene una torre asignada. " +
                    "No se modificó nada.";
                return false;
            }

            SpriteRenderer[] renderers =
                tower.GetComponentsInChildren<SpriteRenderer>(true);

            if (renderers.Length == 0)
            {
                error =
                    tower.gameObject.name +
                    " no tiene SpriteRenderer. " +
                    "No se puede colocar la parcela detrás de la torre.";
                return false;
            }

            foreach (SpriteRenderer renderer in renderers)
            {
                if (!found)
                {
                    sorting.layerId = renderer.sortingLayerID;
                    sorting.order = renderer.sortingOrder;
                    sorting.layerName = SortingLayer.IDToName(
                        renderer.sortingLayerID
                    );
                    found = true;
                    continue;
                }

                if (renderer.sortingLayerID != sorting.layerId)
                {
                    error =
                        "Las torres usan capas de dibujo distintas. " +
                        "No se modificó nada.";
                    return false;
                }

                if (renderer.sortingOrder < sorting.order)
                {
                    sorting.order = renderer.sortingOrder;
                }
            }
        }

        return found;
    }

    private static TowerController ReadTowerPrefab(BuildPlot plot)
    {
        SerializedObject serialized = new(plot);
        SerializedProperty property =
            serialized.FindProperty("towerPrefab");

        if (property == null)
        {
            return null;
        }

        return property.objectReferenceValue as TowerController;
    }

    private static bool TryBuildPlan(
        BuildPlot plot,
        TowerSorting towerSorting,
        HashSet<int> usedRenderers,
        HashSet<int> usedColliders,
        out PlotPlan plan,
        out string error)
    {
        plan = null;
        error = null;
        string name = plot.gameObject.name;

        if (plot.GetComponents<BuildPlot>().Length != 1)
        {
            error = name + " tiene más de un BuildPlot. No se modificó nada.";
            return false;
        }

        Collider2D collider = ReadCollider(plot);

        if (collider == null)
        {
            error =
                name + " no tiene plotCollider asignado. " +
                "No se modificó nada.";
            return false;
        }

        if (collider.gameObject != plot.gameObject)
        {
            error =
                name + ": plotCollider no está en la raíz. " +
                "No se modificó nada.";
            return false;
        }

        if (!usedColliders.Add(collider.GetInstanceID()))
        {
            error =
                name + " comparte plotCollider con otra parcela. " +
                "No se modificó nada.";
            return false;
        }

        SpriteRenderer plotRenderer = ReadRenderer(plot);

        if (plotRenderer == null)
        {
            error =
                name + " no tiene plotRenderer asignado. " +
                "No se modificó nada.";
            return false;
        }

        if (!usedRenderers.Add(plotRenderer.GetInstanceID()))
        {
            error =
                name + " comparte plotRenderer con otra parcela. " +
                "No se modificó nada.";
            return false;
        }

        if (plotRenderer.drawMode != SpriteDrawMode.Simple)
        {
            error =
                name + " no usa el modo Simple del SpriteRenderer. " +
                "No se modificó nada.";
            return false;
        }

        if (!IsAligned(plot.transform))
        {
            error =
                name + " tiene rotación. " +
                "No se modificó nada para no alterar su orientación.";
            return false;
        }

        Transform root = plot.transform;
        SpriteRenderer[] rootRenderers =
            plot.GetComponents<SpriteRenderer>();

        if (rootRenderers.Length > 1)
        {
            error =
                name + " tiene más de un SpriteRenderer en la raíz. " +
                "No se modificó nada.";
            return false;
        }

        List<Transform> namedVisuals = new();
        List<Transform> spriteChildren = new();

        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);

            if (child.name == VisualChildName)
            {
                namedVisuals.Add(child);
            }

            if (child.GetComponent<SpriteRenderer>() != null)
            {
                spriteChildren.Add(child);
            }
        }

        if (namedVisuals.Count > 1)
        {
            error =
                name + " tiene más de un hijo llamado Visual. " +
                "No se modificó nada.";
            return false;
        }

        bool rendererOnRoot = plotRenderer.transform == root;

        if (!rendererOnRoot && plotRenderer.transform.parent != root)
        {
            error =
                name + ": plotRenderer no está en la raíz ni en un hijo directo. " +
                "No se modificó nada.";
            return false;
        }

        Transform visual = null;

        if (namedVisuals.Count == 1)
        {
            visual = namedVisuals[0];

            if (rendererOnRoot || plotRenderer.transform != visual)
            {
                error =
                    name +
                    " tiene un hijo Visual que no coincide con plotRenderer. " +
                    "No se modificó nada.";
                return false;
            }
        }
        else if (!rendererOnRoot)
        {
            visual = plotRenderer.transform;
        }

        if (visual == null && spriteChildren.Count > 0)
        {
            error =
                name +
                " tiene un dibujo hijo y plotRenderer sigue en la raíz. " +
                "No se modificó nada.";
            return false;
        }

        if (visual != null)
        {
            if (!IsAligned(visual))
            {
                error =
                    name + ": el hijo visual tiene rotación. " +
                    "No se modificó nada.";
                return false;
            }

            int otherSpriteChildren = 0;

            foreach (Transform child in spriteChildren)
            {
                if (child != visual)
                {
                    otherSpriteChildren++;
                }
            }

            if (otherSpriteChildren > 0)
            {
                error =
                    name + " tiene otro dibujo además del hijo visual. " +
                    "No se modificó nada.";
                return false;
            }

            if (visual.GetComponents<SpriteRenderer>().Length > 1)
            {
                error =
                    name + ": el hijo visual tiene más de un SpriteRenderer. " +
                    "No se modificó nada.";
                return false;
            }
        }

        SpriteRenderer rootRenderer = rootRenderers.Length == 1
            ? rootRenderers[0]
            : null;

        if (rootRenderer != null &&
            !IsPlaceholder(rootRenderer.sprite) &&
            !IsPlot(rootRenderer.sprite))
        {
            error =
                name +
                " tiene en la raíz un sprite que no es el cuadrado " +
                "temporal ni la parcela. No se modificó nada.";
            return false;
        }

        if (!IsPlaceholder(plotRenderer.sprite) &&
            !IsPlot(plotRenderer.sprite))
        {
            error =
                name +
                " ya usa un sprite distinto de la parcela. " +
                "No se modificó nada.";
            return false;
        }

        if (rootRenderer != null &&
            IsPlot(rootRenderer.sprite) &&
            visual != null)
        {
            error =
                name +
                " muestra la parcela en la raíz y también tiene un hijo visual. " +
                "No se modificó nada.";
            return false;
        }

        if (plotRenderer.sprite == null)
        {
            error =
                name + " no tiene sprite y no se puede medir su ancho. " +
                "No se modificó nada.";
            return false;
        }

        float worldWidth = MeasureWorldWidth(plotRenderer);

        if (worldWidth <= 0.0001f)
        {
            error =
                name + " tiene un ancho visible no válido. " +
                "No se modificó nada.";
            return false;
        }

        if (Mathf.Abs(root.lossyScale.x) <= 0.0001f)
        {
            error =
                name + " tiene escala de raíz no válida. " +
                "No se modificó nada.";
            return false;
        }

        bool separated = visual != null &&
            plotRenderer.transform == visual;
        bool integrated = separated &&
            IsIntegratedSprite(plotRenderer.sprite);

        if (!TryChooseSorting(
                plotRenderer,
                towerSorting,
                out int sortingLayerId,
                out int sortingOrder,
                out string sortingNote,
                out error))
        {
            error = name + ": " + error;
            return false;
        }

        plan = new PlotPlan
        {
            plot = plot,
            createVisual = visual == null,
            removeRootRenderer = rootRenderer != null,
            assignSprite = !integrated,
            applyScale = !integrated,
            applySorting = !integrated,
            assignReference = !separated,
            worldWidth = worldWidth,
            sortingLayerId = sortingLayerId,
            sortingOrder = sortingOrder,
            sortingNote = sortingNote,
            sourceEnabled = plotRenderer.enabled
        };

        return true;
    }

    private static bool TryChooseSorting(
        SpriteRenderer source,
        TowerSorting towerSorting,
        out int layerId,
        out int order,
        out string note,
        out string error)
    {
        layerId = source.sortingLayerID;
        order = source.sortingOrder;
        note = string.Empty;
        error = null;
        int sourceLayer = SortingLayer.GetLayerValueFromID(layerId);
        int towerLayer = SortingLayer.GetLayerValueFromID(
            towerSorting.layerId
        );
        string sourceName = SortingLayer.IDToName(layerId);

        if (sourceLayer < towerLayer)
        {
            note =
                "Capa " + sourceName +
                ", orden " + order +
                ". Ya queda detrás de la torre (" +
                towerSorting.layerName + " " + towerSorting.order + ").";
            return true;
        }

        if (sourceLayer > towerLayer)
        {
            error =
                "su capa " + sourceName +
                " se dibuja por delante de la torre (" +
                towerSorting.layerName +
                "). No se modificó nada.";
            return false;
        }

        if (order < towerSorting.order)
        {
            note =
                "Capa " + sourceName +
                ", orden " + order +
                ". Ya queda detrás de la torre (orden " +
                towerSorting.order + ").";
            return true;
        }

        order = towerSorting.order - 1;
        note =
            "Capa " + sourceName +
            ", orden " + order +
            " para quedar detrás de la torre (orden " +
            towerSorting.order + ").";
        return true;
    }

    private static void ApplyPlans(
        Scene scene,
        List<PlotPlan> plans,
        Sprite plotSprite,
        bool importChanged,
        TowerSorting towerSorting)
    {
        foreach (PlotPlan plan in plans)
        {
            if (!plan.applyScale)
            {
                continue;
            }

            float scale = CalculateVisualScale(
                plotSprite,
                plan.plot.transform.lossyScale,
                plan.worldWidth
            );

            if (scale <= 0f || plotSprite == null)
            {
                Report(
                    "Detenido",
                    plan.plot.gameObject.name +
                    " no pudo calcular su escala. " +
                    "Las parcelas no se modificaron." +
                    (importChanged
                        ? "\nLa importación de la imagen sí se actualizó."
                        : string.Empty)
                );
                return;
            }

            plan.appliedScale = scale;
        }

        bool sceneWillChange = false;

        foreach (PlotPlan plan in plans)
        {
            if (plan.createVisual ||
                plan.assignSprite ||
                plan.applyScale ||
                plan.applySorting ||
                plan.assignReference ||
                plan.removeRootRenderer)
            {
                sceneWillChange = true;
                break;
            }
        }

        if (!sceneWillChange)
        {
            Report(
                importChanged ? "Configuración aplicada" : "Sin cambios",
                BuildSummary(plans, importChanged, false, towerSorting)
            );
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        bool sceneChanged = false;

        try
        {
            foreach (PlotPlan plan in plans)
            {
                if (!ApplyPlan(plan, plotSprite))
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    Report(
                        "Detenido",
                        plan.plot.gameObject.name +
                        " no tiene un hijo visual utilizable. " +
                        "Se revirtieron los cambios de la escena." +
                        (importChanged
                            ? "\nLa importación de la imagen sí se actualizó."
                            : string.Empty)
                    );
                    return;
                }

                if (plan.changed)
                {
                    sceneChanged = true;
                }
            }
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }

        Undo.CollapseUndoOperations(undoGroup);

        if (sceneChanged)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Report(
            sceneChanged || importChanged
                ? "Configuración aplicada"
                : "Sin cambios",
            BuildSummary(
                plans,
                importChanged,
                sceneChanged,
                towerSorting
            )
        );
    }

    private static bool ApplyPlan(PlotPlan plan, Sprite plotSprite)
    {
        BuildPlot plot = plan.plot;
        Transform root = plot.transform;
        Transform visual = plan.createVisual
            ? CreateVisual(root)
            : FindVisual(root, plot);

        if (visual == null)
        {
            return false;
        }

        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        bool createdRenderer = false;

        if (renderer == null)
        {
            renderer = Undo.AddComponent<SpriteRenderer>(visual.gameObject);
            createdRenderer = true;
            renderer.enabled = plan.sourceEnabled;
        }

        if (plan.assignSprite || plan.applySorting)
        {
            Undo.RecordObject(renderer, UndoName);
        }

        if (plan.assignSprite)
        {
            renderer.sprite = plotSprite;
            renderer.color = Color.white;
        }

        if (plan.applySorting)
        {
            renderer.sortingLayerID = plan.sortingLayerId;
            renderer.sortingOrder = plan.sortingOrder;
        }

        if (plan.createVisual)
        {
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
        }

        if (plan.applyScale)
        {
            Undo.RecordObject(visual, UndoName);
            visual.localScale = new Vector3(
                plan.appliedScale,
                plan.appliedScale,
                1f
            );
        }

        if (plan.assignReference)
        {
            SerializedObject serialized = new(plot);
            SerializedProperty property =
                serialized.FindProperty("plotRenderer");

            if (property == null)
            {
                return false;
            }

            property.objectReferenceValue = renderer;
            serialized.ApplyModifiedProperties();
        }

        if (plan.removeRootRenderer)
        {
            SpriteRenderer rootRenderer =
                plot.GetComponent<SpriteRenderer>();

            if (rootRenderer != null && rootRenderer != renderer)
            {
                Undo.DestroyObjectImmediate(rootRenderer);
            }
        }

        plan.changed = plan.createVisual ||
            createdRenderer ||
            plan.assignSprite ||
            plan.applyScale ||
            plan.applySorting ||
            plan.assignReference ||
            plan.removeRootRenderer;

        if (plan.changed && PrefabUtility.IsPartOfPrefabInstance(plot))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(plot);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        }

        plan.visualName = visual.name;
        return true;
    }

    private static Transform CreateVisual(Transform root)
    {
        GameObject visualObject = new(VisualChildName);
        visualObject.layer = root.gameObject.layer;
        Undo.RegisterCreatedObjectUndo(visualObject, UndoName);
        Undo.SetTransformParent(visualObject.transform, root, UndoName);
        return visualObject.transform;
    }

    private static Transform FindVisual(Transform root, BuildPlot plot)
    {
        SpriteRenderer renderer = ReadRenderer(plot);

        if (renderer != null && renderer.transform.parent == root)
        {
            return renderer.transform;
        }

        Transform named = root.Find(VisualChildName);

        if (named != null && named.parent == root)
        {
            return named;
        }

        return null;
    }

    private static float CalculateVisualScale(
        Sprite sprite,
        Vector3 rootLossyScale,
        float worldWidth)
    {
        float spriteWidth = sprite.bounds.size.x;
        float parentScale = Mathf.Abs(rootLossyScale.x);

        if (spriteWidth <= 0.0001f || parentScale <= 0.0001f)
        {
            return 0f;
        }

        return worldWidth / (spriteWidth * parentScale);
    }

    private static float MeasureWorldWidth(SpriteRenderer renderer)
    {
        if (renderer.sprite == null)
        {
            return 0f;
        }

        return renderer.sprite.bounds.size.x *
            Mathf.Abs(renderer.transform.lossyScale.x);
    }

    private static bool ConfigureImporter(out string error)
    {
        error = null;
        TextureImporter importer =
            AssetImporter.GetAtPath(SpritePath) as TextureImporter;

        if (importer == null)
        {
            error = "La imagen dejó de ser una textura importable.";
            return false;
        }

        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);
        Vector2 center = new(0.5f, 0.5f);
        bool alreadyConfigured =
            importer.textureType == TextureImporterType.Sprite &&
            importer.spriteImportMode == SpriteImportMode.Single &&
            !importer.mipmapEnabled &&
            importer.alphaIsTransparency &&
            importer.filterMode == FilterMode.Bilinear &&
            settings.spriteMode == (int)SpriteImportMode.Single &&
            settings.spriteAlignment == (int)SpriteAlignment.Center &&
            Approximately(settings.spritePivot, center);

        if (alreadyConfigured)
        {
            return false;
        }

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

    private static bool LoadPlotSprite(
        out Sprite plotSprite,
        out string error)
    {
        plotSprite = null;
        error = null;
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(SpritePath);
        int spriteCount = 0;

        foreach (Object asset in assets)
        {
            if (asset is not Sprite sprite)
            {
                continue;
            }

            plotSprite = sprite;
            spriteCount++;
        }

        if (spriteCount != 1 || plotSprite == null)
        {
            error =
                "La parcela debe quedar como un solo sprite. " +
                "Se encontraron " + spriteCount + ".";
            return false;
        }

        if (plotSprite.bounds.size.x <= 0f ||
            plotSprite.bounds.size.y <= 0f)
        {
            error = "El sprite de la parcela no tiene límites válidos.";
            return false;
        }

        return true;
    }

    private static SpriteRenderer ReadRenderer(BuildPlot plot)
    {
        SerializedObject serialized = new(plot);
        SerializedProperty property =
            serialized.FindProperty("plotRenderer");

        if (property == null)
        {
            return null;
        }

        return property.objectReferenceValue as SpriteRenderer;
    }

    private static Collider2D ReadCollider(BuildPlot plot)
    {
        SerializedObject serialized = new(plot);
        SerializedProperty property =
            serialized.FindProperty("plotCollider");

        if (property == null)
        {
            return null;
        }

        return property.objectReferenceValue as Collider2D;
    }

    private static bool IsPlaceholder(Sprite sprite)
    {
        if (sprite == null)
        {
            return true;
        }

        string path = AssetDatabase.GetAssetPath(sprite);

        if (string.IsNullOrEmpty(path) ||
            path.StartsWith("Resources/"))
        {
            return true;
        }

        string guid = AssetDatabase.AssetPathToGUID(path);
        return guid == BuiltinSquareGuid || guid == BuiltinKnobGuid;
    }

    private static bool IsPlot(Sprite sprite)
    {
        return sprite != null &&
            AssetDatabase.GetAssetPath(sprite) == SpritePath;
    }

    private static bool IsIntegratedSprite(Sprite sprite)
    {
        if (!IsPlot(sprite))
        {
            return false;
        }

        TextureImporter importer =
            AssetImporter.GetAtPath(SpritePath) as TextureImporter;

        return importer != null &&
            importer.spriteImportMode == SpriteImportMode.Single;
    }

    private static bool IsAligned(Transform transform)
    {
        return Quaternion.Angle(transform.localRotation, Quaternion.identity) <=
                0.1f &&
            Quaternion.Angle(transform.rotation, Quaternion.identity) <= 0.1f;
    }

    private static bool Approximately(Vector2 value, Vector2 target)
    {
        return Mathf.Abs(value.x - target.x) <= 0.001f &&
            Mathf.Abs(value.y - target.y) <= 0.001f;
    }

    private static string BuildSummary(
        List<PlotPlan> plans,
        bool importChanged,
        bool sceneChanged,
        TowerSorting towerSorting)
    {
        StringBuilder summary = new();
        summary.AppendLine(
            importChanged
                ? "Importación: Sprite, Single, pivote central, transparencia, bilinear, sin mipmaps."
                : "Importación: ya estaba configurada."
        );
        summary.AppendLine(
            "Torre inspeccionada: capa " + towerSorting.layerName +
            ", orden " + towerSorting.order + "."
        );

        foreach (PlotPlan plan in plans)
        {
            summary.Append(plan.plot.gameObject.name);
            summary.Append(": ");

            if (!plan.changed)
            {
                summary.AppendLine(
                    "integración existente. Escala y posición visual conservadas."
                );
                continue;
            }

            summary.Append(
                plan.createVisual
                    ? "hijo " + plan.visualName + " creado. "
                    : "hijo " + plan.visualName + " reutilizado. "
            );

            if (plan.applyScale)
            {
                summary.Append(
                    "Ancho mundial " + plan.worldWidth.ToString("0.####") +
                    ", escala visual " + plan.appliedScale.ToString("0.####") +
                    ". "
                );
            }
            else
            {
                summary.Append("Escala y posición visual conservadas. ");
            }

            if (plan.applySorting)
            {
                summary.Append(plan.sortingNote + " ");
            }

            if (plan.removeRootRenderer)
            {
                summary.Append("Cuadrado de la raíz retirado. ");
            }

            summary.AppendLine("Raíz, collider y referencias de construcción conservados.");
        }

        summary.AppendLine(
            sceneChanged
                ? "La escena quedó modificada y no se guardó. Puedes deshacerla con un solo Undo."
                : "La escena no necesitó cambios."
        );
        return summary.ToString();
    }

    private static void Report(string title, string message)
    {
        Debug.Log(title + "\n" + message);
        EditorUtility.DisplayDialog(title, message, "Cerrar");
    }

    private sealed class PlotPlan
    {
        public BuildPlot plot;
        public bool createVisual;
        public bool removeRootRenderer;
        public bool assignSprite;
        public bool applyScale;
        public bool applySorting;
        public bool assignReference;
        public float worldWidth;
        public float appliedScale;
        public int sortingLayerId;
        public int sortingOrder;
        public string sortingNote;
        public bool sourceEnabled;
        public bool changed;
        public string visualName;
    }

    private struct TowerSorting
    {
        public int layerId;
        public int order;
        public string layerName;
    }
}
