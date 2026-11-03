using UnityEngine;

public class LaunchMark : MonoBehaviour
{
    private const float Life = 0.12f;

    private static Sprite diamond;
    private float age;

    public static void Show(Vector3 position)
    {
        var mark = new GameObject("LaunchMark");
        mark.transform.position = position;
        mark.transform.localScale = new Vector3(0.28f, 0.28f, 1f);
        SpriteRenderer renderer = mark.AddComponent<SpriteRenderer>();
        renderer.sprite = Diamond();
        renderer.color = new Color(0.95f, 0.9f, 0.7f, 0.85f);
        renderer.sortingOrder = 12;
        mark.AddComponent<LaunchMark>();
    }

    private void Update()
    {
        float delta = Time.deltaTime;

        if (delta <= 0f)
        {
            return;
        }

        age += delta;
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();

        if (renderer != null)
        {
            Color color = renderer.color;
            color.a = 0.85f * (1f - Mathf.Clamp01(age / Life));
            renderer.color = color;
        }

        if (age >= Life)
        {
            Destroy(gameObject);
        }
    }

    private static Sprite Diamond()
    {
        if (diamond != null)
        {
            return diamond;
        }

        var texture = new Texture2D(8, 8);
        var clear = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int dx = x < 4 ? 3 - x : x - 4;
                int dy = y < 4 ? 3 - y : y - 4;
                texture.SetPixel(x, y, dx + dy <= 3 ? Color.white : clear);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Point;
        diamond = Sprite.Create(
            texture,
            new Rect(0f, 0f, 8f, 8f),
            new Vector2(0.5f, 0.5f),
            8f);
        return diamond;
    }
}
