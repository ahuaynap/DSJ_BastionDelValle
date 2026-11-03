using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ValleyHeartConfigurator
{
    private const string MenuPath =
        "Bastion del Valle/Incrementos/15 - Configurar Corazon del Valle";

    private const string SpritePath =
        "Assets/Art/Environment/valley_heart_idle_v001.png";

    private const string PlotSpritePath =
        "Assets/Art/Environment/build_plot_idle_v001.png";

    private const string ScenePath =
        "Assets/Scenes/Level_01.unity";

    private const string VisualChildName = "Visual";
    private const string UndoName = "Configurar Corazon del Valle";

    private const string BuiltinSquareGuid =
        "311925a002f4447b3a28927169b83ea6";

    private const string BuiltinKnobGuid =
        "a86470a33a6bf42c4b3595704624658b";

    private const float PlotWidthMultiple = 1.5f;
    private const float WidthTolerance = 0.01f;

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
                "Sal de Play Mode antes de configurar el corazón."
            );
            return;
        }

        if (!TryGetOpenScene(out Scene scene, out string error))
        {
            Report("Detenido", error);
            return;
        }

        if (AssetImporter.GetAtPath(SpritePath) is not TextureImporter)
        {
            Report(
                "Detenido",
                "No se encontró la imagen importable en " +
                SpritePath + "."
            );
            return;
        }

        if (!TryInspect(
                scene,
                out HeartPlan plan,
                out float plotWidth,
                out string plotDetail,
                out error))
        {
            Report("Detenido", error);
            return;
        }

        if (!plan.needsSprite)
        {
            Apply(scene, plan, null, false, plotWidth, plotDetail);
            return;
        }

        bool importChanged = ConfigureImporter(out error);

        if (error != null)
        {
            Report("Detenido", error);
            return;
        }

        if (!LoadHeartSprite(out Sprite heartSprite, out error))
        {
            if (!importChanged && ForceReimport())
            {
                importChanged = true;
                error = null;

                if (!LoadHeartSprite(out heartSprite, out error))
                {
                    Report(
                        "Importación actualizada",
                        error + "\nEl corazón no se modificó."
                    );
                    return;
                }
            }
            else
            {
                Report(
                    importChanged
                        ? "Importación actualizada"
                        : "Detenido",
                    error + "\nEl corazón no se modificó."
                );
                return;
            }
        }

        plan.targetWidth = plotWidth * PlotWidthMultiple;
        plan.appliedScale = CalculateVisualScale(
            heartSprite,
            plan.heart.transform.lossyScale,
            plan.targetWidth
        );

        if (plan.appliedScale <= 0f)
        {
            Report(
                "Detenido",
                "No se pudo calcular la escala del corazón. " +
                "La escena no se modificó." +
                (importChanged
                    ? "\nLa importación de la imagen sí se actualizó."
                    : string.Empty)
            );
            return;
        }

        Apply(scene, plan, heartSprite, importChanged, plotWidth, plotDetail);
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
                "Abre " + ScenePath + " antes de configurar el corazón. " +
                "No se cambió de escena.";
            return false;
        }

        return true;
    }

    private static bool TryInspect(
        Scene scene,
        out HeartPlan plan,
        out float plotWidth,
        out string plotDetail,
        out string error)
    {
        plan = null;
        plotWidth = 0f;
        plotDetail = null;
        error = null;

        if (!TryFindHeart(scene, out BastionHealth heart, out error))
        {
            return false;
        }

        if (!TryMeasurePlots(scene, out plotWidth, out plotDetail, out error))
        {
            return false;
        }

        return TryBuildPlan(heart, out plan, out error);
    }

    private static bool TryFindHeart(
        Scene scene,
        out BastionHealth heart,
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

        if (hearts.Count == 1)
        {
            heart = hearts[0];
            return true;
        }

        if (hearts.Count == 0)
        {
            error = "Level_01 no tiene un componente BastionHealth.";
            return false;
        }

        StringBuilder names = new();
        names.AppendLine(
            "Hay " + hearts.Count +
            " componentes BastionHealth. No se modificó nada:"
        );

        foreach (BastionHealth candidate in hearts)
        {
            names.AppendLine("- " + candidate.gameObject.name);
        }

        error = names.ToString();
        return false;
    }

    private static bool TryMeasurePlots(
        Scene scene,
        out float width,
        out string detail,
        out string error)
    {
        width = 0f;
        detail = null;
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
            error =
                "Level_01 no tiene parcelas. " +
                "No hay una referencia de ancho.";
            return false;
        }

        List<PlotSample> samples = new();

        foreach (BuildPlot plot in plots)
        {
            SpriteRenderer renderer = ReadPlotRenderer(plot);

            if (renderer == null || !IsSpriteAtPath(
                    renderer.sprite,
                    PlotSpritePath))
            {
                continue;
            }

            if (renderer.drawMode != SpriteDrawMode.Simple)
            {
                error =
                    plot.gameObject.name +
                    " no usa el modo Simple. " +
                    "No se puede medir su ancho visible.";
                return false;
            }

            float measured = MeasureWorldWidth(renderer);

            if (measured <= 0.0001f)
            {
                error =
                    plot.gameObject.name +
                    " tiene un ancho visible no válido.";
                return false;
            }

            samples.Add(new PlotSample
            {
                name = plot.gameObject.name,
                width = measured
            });
        }

        if (samples.Count == 0)
        {
            error =
                "Ninguna parcela tiene plotRenderer con el sprite " +
                "configurado. No se adivinó el tamaño del corazón.";
            return false;
        }

        float minimum = samples[0].width;
        float maximum = samples[0].width;
        StringBuilder list = new();

        foreach (PlotSample sample in samples)
        {
            minimum = Mathf.Min(minimum, sample.width);
            maximum = Mathf.Max(maximum, sample.width);
            list.AppendLine(
                "- " + sample.name + ": " +
                sample.width.ToString("0.####")
            );
        }

        if (maximum - minimum > WidthTolerance)
        {
            error =
                "Las parcelas configuradas no comparten el mismo ancho. " +
                "No se modificó nada.\n" + list;
            return false;
        }

        width = samples[0].width;
        detail =
            samples.Count + " parcela(s), ancho " +
            width.ToString("0.####") + ".";
        return true;
    }

    private static bool TryBuildPlan(
        BastionHealth heart,
        out HeartPlan plan,
        out string error)
    {
        plan = null;
        error = null;
        string name = heart.gameObject.name;
        Transform root = heart.transform;

        if (heart.GetComponents<BastionHealth>().Length != 1)
        {
            error =
                name + " tiene más de un BastionHealth. " +
                "No se modificó nada.";
            return false;
        }

        if (!IsAligned(root))
        {
            error =
                name + " tiene rotación. " +
                "No se modificó nada para conservar su orientación.";
            return false;
        }

        if (Mathf.Abs(root.lossyScale.x) <= 0.0001f)
        {
            error =
                name + " tiene una escala de raíz no válida. " +
                "No se modificó nada.";
            return false;
        }

        SpriteRenderer[] rootRenderers =
            heart.GetComponents<SpriteRenderer>();

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

        Transform visual = namedVisuals.Count == 1
            ? namedVisuals[0]
            : null;

        if (visual == null && spriteChildren.Count == 1)
        {
            visual = spriteChildren[0];
        }
        else if (visual == null && spriteChildren.Count > 1)
        {
            error =
                name + " tiene varios hijos con dibujo. " +
                "No se modificó nada.";
            return false;
        }

        if (visual != null && spriteChildren.Count > 1)
        {
            error =
                name +
                " tiene otro dibujo además del hijo visual. " +
                "No se desactivó ningún renderer ajeno.";
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

            if (visual.GetComponents<SpriteRenderer>().Length > 1)
            {
                error =
                    name +
                    ": el hijo visual tiene más de un SpriteRenderer. " +
                    "No se modificó nada.";
                return false;
            }
        }

        SpriteRenderer rootRenderer = rootRenderers.Length == 1
            ? rootRenderers[0]
            : null;
        SpriteRenderer visualRenderer = visual != null
            ? visual.GetComponent<SpriteRenderer>()
            : null;

        if (rootRenderer != null &&
            !IsPlaceholder(rootRenderer.sprite) &&
            !IsSpriteAtPath(rootRenderer.sprite, SpritePath))
        {
            error =
                name +
                " tiene en la raíz un sprite que no es el cuadrado " +
                "temporal ni el corazón. No se modificó nada.";
            return false;
        }

        if (visualRenderer != null &&
            visualRenderer.sprite != null &&
            !IsPlaceholder(visualRenderer.sprite) &&
            !IsSpriteAtPath(visualRenderer.sprite, SpritePath))
        {
            error =
                name +
                " ya usa otro sprite en su hijo visual. " +
                "No se modificó nada.";
            return false;
        }

        if (rootRenderer != null &&
            IsSpriteAtPath(rootRenderer.sprite, SpritePath) &&
            visualRenderer != null)
        {
            error =
                name +
                " muestra el corazón en la raíz y también tiene un hijo visual. " +
                "No se modificó nada.";
            return false;
        }

        if (rootRenderer != null &&
            rootRenderer.drawMode != SpriteDrawMode.Simple)
        {
            error =
                name + " no usa el modo Simple. No se modificó nada.";
            return false;
        }

        bool spriteReady = visualRenderer != null &&
            IsIntegratedSprite(visualRenderer.sprite);
        int sortingLayerId = 0;
        int sortingOrder = 0;
        SpriteRenderer sortingSource = rootRenderer != null
            ? rootRenderer
            : visualRenderer;

        if (!spriteReady)
        {
            if (sortingSource == null)
            {
                error =
                    name +
                    " no tiene un SpriteRenderer del que copiar " +
                    "la capa y el orden. No se modificó nada.";
                return false;
            }

            sortingLayerId = sortingSource.sortingLayerID;
            sortingOrder = sortingSource.sortingOrder;
        }

        plan = new HeartPlan
        {
            heart = heart,
            createVisual = visual == null,
            removeRootRenderer = rootRenderer != null &&
                (IsPlaceholder(rootRenderer.sprite) || !spriteReady),
            assignSprite = !spriteReady,
            applyScale = !spriteReady,
            applySorting = !spriteReady,
            sortingLayerId = sortingLayerId,
            sortingOrder = sortingOrder,
            sourceEnabled = sortingSource == null || sortingSource.enabled,
            needsSprite = !spriteReady
        };
        return true;
    }

    private static void Apply(
        Scene scene,
        HeartPlan plan,
        Sprite heartSprite,
        bool importChanged,
        float plotWidth,
        string plotDetail)
    {
        bool willChange = plan.createVisual ||
            plan.assignSprite ||
            plan.applyScale ||
            plan.applySorting ||
            plan.removeRootRenderer;

        if (!willChange)
        {
            Report(
                importChanged ? "Configuración aplicada" : "Sin cambios",
                BuildSummary(
                    plan,
                    importChanged,
                    false,
                    plotWidth,
                    plotDetail
                )
            );
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);

        try
        {
            if (!ApplyPlan(plan, heartSprite))
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Report(
                    "Detenido",
                    "No se pudo preparar el hijo visual. " +
                    "Se revirtieron los cambios de la escena." +
                    (importChanged
                        ? "\nLa importación de la imagen sí se actualizó."
                        : string.Empty)
                );
                return;
            }
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Report(
            "Configuración aplicada",
            BuildSummary(plan, importChanged, true, plotWidth, plotDetail)
        );
    }

    private static bool ApplyPlan(HeartPlan plan, Sprite heartSprite)
    {
        BastionHealth heart = plan.heart;
        Transform root = heart.transform;
        Transform visual = plan.createVisual
            ? CreateVisual(root)
            : FindVisual(root);

        if (visual == null)
        {
            return false;
        }

        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();

        if (renderer == null)
        {
            renderer = Undo.AddComponent<SpriteRenderer>(visual.gameObject);
            renderer.enabled = plan.sourceEnabled;
        }

        if (plan.assignSprite || plan.applySorting)
        {
            Undo.RecordObject(renderer, UndoName);
        }

        if (plan.assignSprite)
        {
            if (heartSprite == null)
            {
                return false;
            }

            renderer.sprite = heartSprite;
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

        if (plan.removeRootRenderer)
        {
            SpriteRenderer rootRenderer = heart.GetComponent<SpriteRenderer>();

            if (rootRenderer != null && rootRenderer != renderer)
            {
                Undo.DestroyObjectImmediate(rootRenderer);
            }
        }

        plan.visualName = visual.name;

        if (PrefabUtility.IsPartOfPrefabInstance(heart))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(heart);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        }

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

    private static Transform FindVisual(Transform root)
    {
        Transform named = null;

        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);

            if (child.name == VisualChildName)
            {
                return child;
            }

            if (child.GetComponent<SpriteRenderer>() != null)
            {
                named = child;
            }
        }

        return named;
    }

    private static float CalculateVisualScale(
        Sprite sprite,
        Vector3 rootLossyScale,
        float worldWidth)
    {
        float spriteWidth = sprite.bounds.size.x;
        float parentScale = Mathf.Abs(rootLossyScale.x);

        if (spriteWidth <= 0.0001f ||
            parentScale <= 0.0001f ||
            worldWidth <= 0.0001f)
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

        if (ImporterMatches(importer))
        {
            return false;
        }

        ApplyImporterSettings(importer);
        importer.SaveAndReimport();
        return true;
    }

    private static bool ForceReimport()
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(SpritePath) as TextureImporter;

        if (importer == null)
        {
            return false;
        }

        ApplyImporterSettings(importer);
        importer.SaveAndReimport();
        return true;
    }

    private static bool ImporterMatches(TextureImporter importer)
    {
        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);
        Vector2 center = new(0.5f, 0.5f);

        return importer.textureType == TextureImporterType.Sprite &&
            importer.spriteImportMode == SpriteImportMode.Single &&
            !importer.mipmapEnabled &&
            importer.alphaIsTransparency &&
            importer.filterMode == FilterMode.Bilinear &&
            settings.spriteMode == (int)SpriteImportMode.Single &&
            settings.spriteAlignment == (int)SpriteAlignment.Center &&
            Approximately(settings.spritePivot, center);
    }

    private static void ApplyImporterSettings(TextureImporter importer)
    {
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
    }

    private static bool LoadHeartSprite(out Sprite heartSprite, out string error)
    {
        heartSprite = null;
        error = null;
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(SpritePath);
        int spriteCount = 0;

        foreach (Object asset in assets)
        {
            if (asset is not Sprite sprite)
            {
                continue;
            }

            heartSprite = sprite;
            spriteCount++;
        }

        if (spriteCount != 1 || heartSprite == null)
        {
            error =
                "El corazón debe quedar como un solo sprite. " +
                "Se encontraron " + spriteCount + ".";
            return false;
        }

        if (heartSprite.bounds.size.x <= 0f ||
            heartSprite.bounds.size.y <= 0f)
        {
            error = "El sprite del corazón no tiene límites válidos.";
            return false;
        }

        return true;
    }

    private static SpriteRenderer ReadPlotRenderer(BuildPlot plot)
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

    private static bool IsSpriteAtPath(Sprite sprite, string path)
    {
        return sprite != null &&
            AssetDatabase.GetAssetPath(sprite) == path;
    }

    private static bool IsIntegratedSprite(Sprite sprite)
    {
        if (!IsSpriteAtPath(sprite, SpritePath))
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
        return Quaternion.Angle(
                transform.localRotation,
                Quaternion.identity) <= 0.1f &&
            Quaternion.Angle(transform.rotation, Quaternion.identity) <= 0.1f;
    }

    private static bool Approximately(Vector2 value, Vector2 target)
    {
        return Mathf.Abs(value.x - target.x) <= 0.001f &&
            Mathf.Abs(value.y - target.y) <= 0.001f;
    }

    private static string BuildSummary(
        HeartPlan plan,
        bool importChanged,
        bool sceneChanged,
        float plotWidth,
        string plotDetail)
    {
        StringBuilder summary = new();
        summary.AppendLine("Objeto: " + plan.heart.gameObject.name + ".");
        summary.AppendLine(
            importChanged
                ? "Importación: Sprite, Single, pivote central, transparencia, bilinear, sin mipmaps."
                : "Importación: ya estaba configurada."
        );
        summary.AppendLine("Referencia de parcela: " + plotDetail);

        if (!sceneChanged)
        {
            summary.AppendLine(
                "Integración existente. Escala y posición visual conservadas."
            );
            summary.AppendLine("La escena no necesitó cambios.");
            return summary.ToString();
        }

        summary.AppendLine(
            plan.createVisual
                ? "Se creó el hijo " + plan.visualName +
                    ", centrado en la posición del corazón."
                : "Se reutilizó el hijo " + plan.visualName +
                    ". Su posición se conservó."
        );

        if (plan.applyScale)
        {
            summary.AppendLine(
                "Ancho de parcela: " + plotWidth.ToString("0.####") +
                ". Ancho del corazón: " +
                plan.targetWidth.ToString("0.####") +
                " (x" + PlotWidthMultiple.ToString("0.#") +
                "). Escala visual: " +
                plan.appliedScale.ToString("0.####") + "."
            );
        }

        if (plan.applySorting)
        {
            summary.AppendLine(
                "Capa " + SortingLayer.IDToName(plan.sortingLayerId) +
                ", orden " + plan.sortingOrder +
                ", copiados del dibujo actual."
            );
        }

        if (plan.removeRootRenderer)
        {
            summary.AppendLine("Se retiró el cuadrado temporal de la raíz.");
        }

        summary.AppendLine(
            "Raíz, salud, ruta y referencias de daño no se modificaron."
        );
        summary.AppendLine(
            "La escena quedó modificada y no se guardó. " +
            "Puedes deshacerla con un solo Undo."
        );
        return summary.ToString();
    }

    private static void Report(string title, string message)
    {
        Debug.Log(title + "\n" + message);
        EditorUtility.DisplayDialog(title, message, "Cerrar");
    }

    private sealed class HeartPlan
    {
        public BastionHealth heart;
        public bool createVisual;
        public bool removeRootRenderer;
        public bool assignSprite;
        public bool applyScale;
        public bool applySorting;
        public bool sourceEnabled;
        public bool needsSprite;
        public int sortingLayerId;
        public int sortingOrder;
        public float targetWidth;
        public float appliedScale;
        public string visualName;
    }

    private struct PlotSample
    {
        public string name;
        public float width;
    }
}
