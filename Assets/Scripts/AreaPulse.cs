using UnityEngine;

public class AreaPulse : MonoBehaviour
{
    private const float Life = 0.28f;

    private static Sprite circle;
    private SpriteRenderer graphic;
    private float age;

    public static void Show(Vector2 center, float radius)
    {
        var pulse = new GameObject("AreaPulse");
        pulse.transform.position = new Vector3(center.x, center.y, 0f);
        float scale = Mathf.Max(0.05f, radius * 2f);
        pulse.transform.localScale = new Vector3(scale, scale, 1f);
        SpriteRenderer renderer = pulse.AddComponent<SpriteRenderer>();
        renderer.sprite = Circle();
        renderer.color = new Color(0.95f, 0.78f, 0.32f, 0.55f);
        renderer.sortingOrder = 20;
        pulse.AddComponent<AreaPulse>();
    }

    private void Awake()
    {
        graphic = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        float delta = Time.deltaTime;

        if (delta <= 0f)
        {
            return;
        }

        age += delta;
        float progress = Mathf.Clamp01(age / Life);

        if (graphic != null)
        {
            Color color = graphic.color;
            color.a = 0.55f * (1f - progress);
            graphic.color = color;
        }

        if (age >= Life)
        {
            Destroy(gameObject);
        }
    }

    private static Sprite Circle()
    {
        if (circle != null)
        {
            return circle;
        }

        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float middle = (size - 1) * 0.5f;
        float ring = size * 0.42f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - middle;
                float dy = y - middle;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = distance <= ring && distance >= ring * 0.72f ? 1f : 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        circle = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
        return circle;
    }
}
