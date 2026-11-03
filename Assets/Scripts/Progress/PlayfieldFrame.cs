public struct FrameFit
{
    public float CenterX;
    public float CenterY;
    public float OrthographicSize;
    public float ViewMinX;
    public float ViewMaxX;
    public float ViewMinY;
    public float ViewMaxY;
}

public struct PlayableBand
{
    public float MinX;
    public float MaxX;
    public float MinY;
    public float MaxY;
}

public static class PlayfieldFrame
{
    public static FrameFit Fit(
        float minX,
        float maxX,
        float minY,
        float maxY,
        float topFraction,
        float bottomFraction,
        float aspect,
        float slack)
    {
        if (aspect < 0.1f)
        {
            aspect = 16f / 9f;
        }

        if (slack < 0f)
        {
            slack = 0f;
        }

        float top = Clamp01(topFraction);
        float bottom = Clamp01(bottomFraction);

        if (top + bottom > 0.8f)
        {
            float scale = 0.8f / (top + bottom);
            top *= scale;
            bottom *= scale;
        }

        float clear = 1f - top - bottom;

        if (clear < 0.2f)
        {
            clear = 0.2f;
        }

        float contentWidth = System.Math.Max(0.1f, maxX - minX) + slack * 2f;
        float contentHeight = System.Math.Max(0.1f, maxY - minY) + slack * 2f;
        float size = contentHeight / (2f * clear);
        float widthSize = contentWidth / (2f * aspect);

        if (widthSize > size)
        {
            size = widthSize;
        }

        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f - size * (bottom - top);
        float halfWidth = size * aspect;

        return new FrameFit
        {
            CenterX = centerX,
            CenterY = centerY,
            OrthographicSize = size,
            ViewMinX = centerX - halfWidth,
            ViewMaxX = centerX + halfWidth,
            ViewMinY = centerY - size,
            ViewMaxY = centerY + size
        };
    }

    public static PlayableBand Playable(
        FrameFit frame,
        float topFraction,
        float bottomFraction)
    {
        float top = Clamp01(topFraction);
        float bottom = Clamp01(bottomFraction);
        float size = frame.OrthographicSize;

        return new PlayableBand
        {
            MinX = frame.ViewMinX,
            MaxX = frame.ViewMaxX,
            MinY = frame.CenterY - size * (1f - 2f * bottom),
            MaxY = frame.CenterY + size * (1f - 2f * top)
        };
    }

    private static float Clamp01(float value)
    {
        if (value < 0f)
        {
            return 0f;
        }

        if (value > 1f)
        {
            return 1f;
        }

        return value;
    }
}
