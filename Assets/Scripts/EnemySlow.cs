using System.Collections.Generic;
using UnityEngine;

public class EnemySlow : MonoBehaviour
{
    private const float IndicatorScale = 0.16f;

    private readonly List<SlowStamp> effects = new();
    private Transform indicator;
    private float baseSpeed;
    private float scaledNow;
    private bool bound;
    private static Sprite dot;

    public bool IsSlowed =>
        bound && SlowSchedule.Multiplier(effects, scaledNow) < 1f;

    public float EffectiveSpeed =>
        bound
            ? SlowSchedule.Speed(
                baseSpeed,
                SlowSchedule.Multiplier(effects, scaledNow))
            : baseSpeed;

    public void Bind(float speed)
    {
        if (bound)
        {
            return;
        }

        baseSpeed = speed < 0f ? 0f : speed;
        bound = true;
    }

    public void Prepare(float speed)
    {
        effects.Clear();
        scaledNow = 0f;
        baseSpeed = speed < 0f ? 0f : speed;
        bound = true;
        HideIndicator();
    }

    public void Apply(float multiplier, float duration)
    {
        if (!bound)
        {
            return;
        }

        SlowSchedule.Apply(effects, multiplier, duration, scaledNow);
        RefreshIndicator();
    }

    public void SetPresence(int sourceId, float multiplier)
    {
        if (!bound)
        {
            return;
        }

        if (SlowSchedule.SetPresence(effects, sourceId, multiplier))
        {
            RefreshIndicator();
        }
    }

    public void RemovePresence(int sourceId)
    {
        if (!bound)
        {
            return;
        }

        if (SlowSchedule.RemovePresence(effects, sourceId))
        {
            RefreshIndicator();
        }
    }

    public void Clear()
    {
        effects.Clear();
        scaledNow = 0f;
        HideIndicator();
    }

    private void Update()
    {
        if (!bound || effects.Count == 0)
        {
            return;
        }

        scaledNow += Time.deltaTime;
        SlowSchedule.Prune(effects, scaledNow);
        RefreshIndicator();
    }

    private void OnDisable()
    {
        effects.Clear();
        scaledNow = 0f;
        HideIndicator();
    }

    private void HideIndicator()
    {
        if (indicator != null)
        {
            indicator.gameObject.SetActive(false);
        }
    }

    private void RefreshIndicator()
    {
        bool visible = IsSlowed;

        if (!visible && indicator == null)
        {
            return;
        }

        EnsureIndicator();
        indicator.gameObject.SetActive(visible);
    }

    private void EnsureIndicator()
    {
        if (indicator != null)
        {
            return;
        }

        Transform existing = transform.Find("SlowIndicator");

        if (existing != null)
        {
            indicator = existing;
            return;
        }

        GameObject child = new GameObject("SlowIndicator");
        child.transform.SetParent(transform, false);
        child.transform.localPosition = new Vector3(0f, 0.62f, 0f);
        child.transform.localScale = new Vector3(
            IndicatorScale,
            IndicatorScale,
            1f
        );
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = Dot();
        renderer.color = new Color(0.25f, 0.86f, 0.84f, 0.9f);
        renderer.sortingOrder = BodyOrder() + 2;
        indicator = child.transform;
        child.SetActive(false);
    }

    private int BodyOrder()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        int order = 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].gameObject.name == "SlowIndicator")
            {
                continue;
            }

            if (renderers[i].sortingOrder > order)
            {
                order = renderers[i].sortingOrder;
            }
        }

        return order;
    }

    private static Sprite Dot()
    {
        if (dot != null)
        {
            return dot;
        }

        const int size = 16;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = (size - 1) * 0.5f;
        float radius = size * 0.42f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = distance <= radius
                    ? 1f
                    : 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        dot = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
        return dot;
    }
}
