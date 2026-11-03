using UnityEngine;
using UnityEngine.UI;

public static class PlayfieldView
{
    public static void Apply()
    {
        Camera camera = Camera.main;

        if (camera == null || !camera.orthographic)
        {
            return;
        }

        SettleHabitatRow();

        if (!TryContentBounds(out float minX, out float maxX, out float minY, out float maxY))
        {
            return;
        }

        ReservedBands(out float top, out float bottom);
        float aspect = camera.pixelHeight > 0
            ? camera.pixelWidth / (float)camera.pixelHeight
            : camera.aspect;
        FrameFit fit = PlayfieldFrame.Fit(
            minX,
            maxX,
            minY,
            maxY,
            top,
            bottom,
            aspect,
            0.35f);
        Vector3 position = camera.transform.position;
        camera.orthographicSize = fit.OrthographicSize;
        camera.transform.position = new Vector3(fit.CenterX, fit.CenterY, position.z);
    }

    private static void SettleHabitatRow()
    {
        VerticalLayoutGroup[] groups =
            Object.FindObjectsByType<VerticalLayoutGroup>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        for (int i = 0; i < groups.Length; i++)
        {
            VerticalLayoutGroup group = groups[i];

            if (group == null || group.gameObject.name != "HabitatRow")
            {
                continue;
            }

            RectTransform row = group.transform as RectTransform;
            RectOffset padding = group.padding;
            float spacing = group.spacing;
            Object.DestroyImmediate(group);

            if (row == null)
            {
                continue;
            }

            HorizontalLayoutGroup horizontal = row.GetComponent<HorizontalLayoutGroup>();

            if (horizontal == null)
            {
                horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            horizontal.padding = padding;
            horizontal.spacing = spacing;
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = true;
            horizontal.childForceExpandHeight = true;
            horizontal.childScaleWidth = false;
            horizontal.childScaleHeight = false;

            Graphic[] graphics = row.GetComponentsInChildren<Graphic>(false);

            for (int g = 0; g < graphics.Length; g++)
            {
                if (graphics[g] != null && graphics[g].gameObject != row.gameObject)
                {
                    graphics[g].raycastTarget = false;
                }
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    private static bool TryContentBounds(
        out float minX,
        out float maxX,
        out float minY,
        out float maxY)
    {
        bool any = false;
        float leftBound = 0f;
        float rightBound = 0f;
        float bottomBound = 0f;
        float topBound = 0f;

        void Include(float x, float y, float pad)
        {
            float left = x - pad;
            float right = x + pad;
            float bottom = y - pad;
            float top = y + pad;

            if (!any)
            {
                leftBound = left;
                rightBound = right;
                bottomBound = bottom;
                topBound = top;
                any = true;
                return;
            }

            if (left < leftBound)
            {
                leftBound = left;
            }

            if (right > rightBound)
            {
                rightBound = right;
            }

            if (bottom < bottomBound)
            {
                bottomBound = bottom;
            }

            if (top > topBound)
            {
                topBound = top;
            }
        }

        BuildPlot[] plots = Object.FindObjectsByType<BuildPlot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < plots.Length; i++)
        {
            if (plots[i] != null)
            {
                Vector3 point = plots[i].transform.position;
                Include(point.x, point.y, 0.55f);
            }
        }

        BastionHealth[] hearts = Object.FindObjectsByType<BastionHealth>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < hearts.Length; i++)
        {
            if (hearts[i] != null)
            {
                Vector3 point = hearts[i].transform.position;
                Include(point.x, point.y, 0.7f);
            }
        }

        EnemySpawner[] spawners = Object.FindObjectsByType<EnemySpawner>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null)
            {
                Vector3 point = spawners[i].transform.position;
                Include(point.x, point.y, 0.45f);
            }
        }

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || !renderer.enabled || !IsRoute(renderer.transform))
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            float span = Mathf.Max(bounds.size.x, bounds.size.y);

            if (span < 0.05f || span > 24f)
            {
                continue;
            }

            Include(bounds.min.x, bounds.min.y, 0f);
            Include(bounds.max.x, bounds.max.y, 0f);
        }

        minX = leftBound;
        maxX = rightBound;
        minY = bottomBound;
        maxY = topBound;
        return any;
    }

    private static bool IsRoute(Transform node)
    {
        while (node != null)
        {
            string name = node.name;

            if (name == "Camino" ||
                name == "Canales" ||
                name.StartsWith("Bridge_") ||
                name.StartsWith("Cell_") ||
                name.StartsWith("Channel_"))
            {
                return true;
            }

            node = node.parent;
        }

        return false;
    }

    private static void ReservedBands(out float top, out float bottom)
    {
        top = 0f;
        bottom = 0f;
        float screenHeight = Mathf.Max(1f, Screen.height);
        float screenWidth = Mathf.Max(1f, Screen.width);
        Graphic[] graphics = Object.FindObjectsByType<Graphic>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        var corners = new Vector3[4];

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic == null || !graphic.raycastTarget || graphic.canvas == null)
            {
                continue;
            }

            RectTransform rect = graphic.rectTransform;
            rect.GetWorldCorners(corners);
            Camera eventCamera = graphic.canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : graphic.canvas.worldCamera;
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;

            for (int c = 0; c < 4; c++)
            {
                Vector2 screen = graphic.canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? (Vector2)corners[c]
                    : (Vector2)RectTransformUtility.WorldToScreenPoint(eventCamera, corners[c]);

                if (screen.x < minX)
                {
                    minX = screen.x;
                }

                if (screen.x > maxX)
                {
                    maxX = screen.x;
                }

                if (screen.y < minY)
                {
                    minY = screen.y;
                }

                if (screen.y > maxY)
                {
                    maxY = screen.y;
                }
            }

            if (maxX - minX < screenWidth * 0.55f || maxY - minY > screenHeight * 0.4f)
            {
                continue;
            }

            if (minY > screenHeight * 0.5f)
            {
                float fraction = (screenHeight - minY) / screenHeight;

                if (fraction > top)
                {
                    top = fraction;
                }
            }

            if (maxY < screenHeight * 0.5f)
            {
                float fraction = maxY / screenHeight;

                if (fraction > bottom)
                {
                    bottom = fraction;
                }
            }
        }

        bool habitat = Object.FindAnyObjectByType<HabitatSession>(FindObjectsInactive.Exclude) != null;
        bool tray = Object.FindAnyObjectByType<BuildChoiceView>(FindObjectsInactive.Exclude) != null;
        float reserveTop = HudReserve.TopFraction(habitat);
        float reserveBottom = HudReserve.BottomFraction(tray);

        if (top < reserveTop)
        {
            top = reserveTop;
        }

        if (bottom < reserveBottom)
        {
            bottom = reserveBottom;
        }
    }
}
