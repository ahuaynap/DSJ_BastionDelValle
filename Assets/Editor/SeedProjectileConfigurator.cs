using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SeedProjectileConfigurator
{
    private const string MenuPath =
        "Bastion del Valle/Incrementos/13 - Configurar Proyectil Semilla";

    private const string SpritePath =
        "Assets/Art/Projectiles/projectile_seed_v001.png";

    private const string PrefabPath =
        "Assets/Prefabs/Projectile_Basic.prefab";

    private const string VisualChildName = "Visual";

    private const string BuiltinSpriteGuid =
        "a86470a33a6bf42c4b3595704624658b";

    private const float TargetWorldSize = 0.12f;

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
                "Sal de Play Mode antes de configurar el proyectil."
            );
            return;
        }

        if (!ValidateAssets(out string error))
        {
            Report("Detenido", error);
            return;
        }

        if (!InspectPrefab(out error, out _))
        {
            Report("Detenido", error);
            return;
        }

        bool importChanged = ConfigureImporter(out error);

        if (error != null)
        {
            Report("Detenido", error);
            return;
        }

        if (!LoadSeedSprite(out Sprite seedSprite, out error))
        {
            Report(
                "Importación actualizada",
                error + "\nEl prefab no se modificó."
            );
            return;
        }

        ApplyPrefab(seedSprite, importChanged);
    }

    private static bool IsPlayMode()
    {
        return EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static bool ValidateAssets(out string error)
    {
        error = null;

        if (AssetImporter.GetAtPath(SpritePath) is not TextureImporter)
        {
            error =
                "No se encontró la imagen importable en " +
                SpritePath + ".";
            return false;
        }

        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        if (prefab == null)
        {
            error = "No se encontró el prefab en " + PrefabPath + ".";
            return false;
        }

        return true;
    }

    private static bool InspectPrefab(
        out string error,
        out SetupPlan plan)
    {
        error = null;
        plan = null;
        GameObject root = null;

        try
        {
            root = PrefabUtility.LoadPrefabContents(PrefabPath);
            return TryBuildPlan(root, out error, out plan);
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static bool TryBuildPlan(
        GameObject root,
        out string error,
        out SetupPlan plan)
    {
        error = null;
        plan = null;

        if (root == null)
        {
            error = "No se pudo abrir Projectile_Basic.";
            return false;
        }

        if (root.GetComponent<Transform>() == null)
        {
            error = "La raíz del proyectil no tiene Transform.";
            return false;
        }

        Projectile[] projectiles = root.GetComponents<Projectile>();

        if (projectiles.Length != 1)
        {
            error =
                "La raíz debe tener un solo componente Projectile. " +
                "No se modificó nada.";
            return false;
        }

        SpriteRenderer[] rootRenderers =
            root.GetComponents<SpriteRenderer>();

        if (rootRenderers.Length > 1)
        {
            error =
                "La raíz tiene más de un SpriteRenderer. " +
                "No se modificó nada.";
            return false;
        }

        List<Transform> namedVisuals = new();
        List<Transform> spriteChildren = new();

        for (int index = 0; index < root.transform.childCount; index++)
        {
            Transform child = root.transform.GetChild(index);

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
                "Hay más de un hijo llamado Visual. " +
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
                "Hay varios hijos con SpriteRenderer. " +
                "No se modificó nada.";
            return false;
        }

        if (visual != null)
        {
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
                    "Hay otro dibujo además del hijo visual. " +
                    "No se modificó nada.";
                return false;
            }

            if (visual.GetComponents<SpriteRenderer>().Length > 1)
            {
                error =
                    "El hijo visual tiene más de un SpriteRenderer. " +
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
            !IsSeed(rootRenderer.sprite))
        {
            error =
                "La raíz tiene un sprite que no es el cuadrado " +
                "temporal ni la semilla. No se modificó nada.";
            return false;
        }

        if (rootRenderer != null &&
            IsSeed(rootRenderer.sprite) &&
            visualRenderer != null)
        {
            error =
                "La semilla ya está en la raíz y también hay un " +
                "hijo visual. No se modificó nada.";
            return false;
        }

        bool alreadyConfigured = visualRenderer != null &&
            IsSeed(visualRenderer.sprite);

        plan = new SetupPlan
        {
            visualName = visual != null
                ? visual.name
                : VisualChildName,
            createVisual = visual == null,
            removeRootRenderer = rootRenderer != null &&
                (IsPlaceholder(rootRenderer.sprite) ||
                    visual == null),
            assignSprite = !alreadyConfigured,
            applyInitialScale = !alreadyConfigured
        };

        return true;
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

    private static bool LoadSeedSprite(
        out Sprite seedSprite,
        out string error)
    {
        seedSprite = null;
        error = null;
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(SpritePath);
        int spriteCount = 0;

        foreach (Object asset in assets)
        {
            if (asset is not Sprite sprite)
            {
                continue;
            }

            seedSprite = sprite;
            spriteCount++;
        }

        if (spriteCount != 1 || seedSprite == null)
        {
            error =
                "La semilla debe quedar como un solo sprite. " +
                "Se encontraron " + spriteCount + ".";
            return false;
        }

        if (seedSprite.bounds.size.x <= 0f ||
            seedSprite.bounds.size.y <= 0f)
        {
            error = "El sprite de la semilla no tiene límites válidos.";
            return false;
        }

        return true;
    }

    private static void ApplyPrefab(
        Sprite seedSprite,
        bool importChanged)
    {
        GameObject root = null;
        bool savePrefab = false;

        try
        {
            root = PrefabUtility.LoadPrefabContents(PrefabPath);

            if (!TryBuildPlan(root, out string error, out SetupPlan plan))
            {
                Report(
                    "Detenido",
                    error +
                    (importChanged
                        ? "\nLa importación de la imagen sí se actualizó."
                        : string.Empty)
                );
                return;
            }

            StringBuilder summary = new();
            summary.AppendLine("Proyectil Semilla");
            summary.AppendLine(
                importChanged
                    ? "Importación: Sprite, Single, pivote central, transparencia, bilinear, sin mipmaps."
                    : "Importación: ya estaba configurada."
            );

            Transform visual = plan.createVisual
                ? CreateVisual(root.transform)
                : root.transform.Find(plan.visualName);
            bool createdVisual = plan.createVisual;

            if (visual == null)
            {
                Report(
                    "Detenido",
                    "No se encontró el hijo visual al aplicar el prefab."
                );
                return;
            }

            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            bool createdRenderer = false;

            if (renderer == null)
            {
                renderer = visual.gameObject.AddComponent<SpriteRenderer>();
                createdRenderer = true;
                CopySorting(root, renderer);
            }

            if (plan.assignSprite)
            {
                renderer.sprite = seedSprite;
                renderer.color = Color.white;
            }

            if (createdVisual)
            {
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
            }

            if (plan.applyInitialScale)
            {
                float scale = CalculateVisualScale(
                    seedSprite,
                    root.transform.localScale
                );

                if (scale <= 0f)
                {
                    Report(
                        "Detenido",
                        "No se pudo calcular la escala visual. " +
                        "El prefab no se guardó." +
                        (importChanged
                            ? "\nLa importación de la imagen sí se actualizó."
                            : string.Empty)
                    );
                    return;
                }

                visual.localScale = new Vector3(scale, scale, 1f);
                summary.AppendLine(
                    "Escala visual inicial: " + scale.ToString("0.####") +
                    " (lado mayor visible ≈ " +
                    TargetWorldSize.ToString("0.##") +
                    " unidades)."
                );
            }
            else
            {
                summary.AppendLine(
                    "Escala y posición visual: se conservaron."
                );
            }

            if (plan.removeRootRenderer)
            {
                SpriteRenderer rootRenderer =
                    root.GetComponent<SpriteRenderer>();

                if (rootRenderer != null)
                {
                    Object.DestroyImmediate(rootRenderer);
                    summary.AppendLine(
                        "Se retiró el SpriteRenderer geométrico de la raíz."
                    );
                }
            }

            summary.AppendLine(
                createdVisual
                    ? "Se creó el hijo " + visual.name + "."
                    : "Se reutilizó el hijo " + visual.name + "."
            );
            summary.AppendLine(
                createdRenderer
                    ? "Se añadió su SpriteRenderer."
                    : "Se reutilizó su SpriteRenderer."
            );
            summary.AppendLine(
                "Raíz sin cambios de escala: " +
                root.transform.localScale.ToString() + "."
            );
            summary.AppendLine(
                "Projectile, velocidad, daño y distancia de impacto no se tocaron."
            );

            bool prefabChanged = createdVisual ||
                createdRenderer ||
                plan.assignSprite ||
                plan.applyInitialScale ||
                plan.removeRootRenderer;

            if (!prefabChanged && !importChanged)
            {
                summary.AppendLine("No hubo cambios nuevos.");
                Report("Sin cambios", summary.ToString());
                return;
            }

            if (prefabChanged)
            {
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    PrefabPath
                );

                if (saved == null)
                {
                    Report(
                        "Detenido",
                        "No se pudo guardar Projectile_Basic."
                    );
                    return;
                }

                savePrefab = true;
                summary.AppendLine("Prefab guardado.");
            }
            else
            {
                summary.AppendLine("El prefab ya estaba integrado.");
            }

            Report(
                savePrefab || importChanged
                    ? "Configuración aplicada"
                    : "Sin cambios",
                summary.ToString()
            );
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static Transform CreateVisual(Transform root)
    {
        GameObject visualObject = new(VisualChildName);
        visualObject.transform.SetParent(root, false);
        return visualObject.transform;
    }

    private static void CopySorting(
        GameObject root,
        SpriteRenderer destination)
    {
        SpriteRenderer source = root.GetComponent<SpriteRenderer>();

        if (source == null)
        {
            destination.sortingLayerID = 0;
            destination.sortingOrder = 0;
            return;
        }

        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder;
    }

    private static float CalculateVisualScale(
        Sprite sprite,
        Vector3 rootScale)
    {
        float largest =
            Mathf.Max(
                sprite.bounds.size.x * Mathf.Abs(rootScale.x),
                sprite.bounds.size.y * Mathf.Abs(rootScale.y)
            );

        if (largest <= 0.0001f)
        {
            return 0f;
        }

        return TargetWorldSize / largest;
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

        return AssetDatabase.AssetPathToGUID(path) ==
            BuiltinSpriteGuid;
    }

    private static bool IsSeed(Sprite sprite)
    {
        return sprite != null &&
            AssetDatabase.GetAssetPath(sprite) == SpritePath;
    }

    private static bool Approximately(
        Vector2 value,
        Vector2 target)
    {
        return Mathf.Abs(value.x - target.x) <= 0.001f &&
            Mathf.Abs(value.y - target.y) <= 0.001f;
    }

    private static void Report(string title, string message)
    {
        Debug.Log(title + "\n" + message);
        EditorUtility.DisplayDialog(title, message, "Cerrar");
    }

    private sealed class SetupPlan
    {
        public string visualName;
        public bool createVisual;
        public bool removeRootRenderer;
        public bool assignSprite;
        public bool applyInitialScale;
    }
}
