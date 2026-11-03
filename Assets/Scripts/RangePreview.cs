using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class RangePreview : MonoBehaviour
{
    [SerializeField] private BuildSelection selection;
    [SerializeField] private EconomyManager economy;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private PauseManager pauseManager;
    [SerializeField] private BuildPlot[] plots;

    private Transform anchor;
    private Transform fill;
    private Transform ring;
    private SpriteRenderer fillRenderer;
    private SpriteRenderer ringRenderer;
    private Transform labelRoot;
    private TextMeshPro label;
    private static Sprite fillSprite;
    private static Sprite ringSprite;
    private static Sprite plateSprite;

    private void Awake()
    {
        anchor = new GameObject("RangePreviewAnchor").transform;
        anchor.SetParent(transform, false);

        fill = CreateDisc("RangeFill", FillSprite(), 5, out fillRenderer);
        ring = CreateDisc("RangeRing", RingSprite(), 6, out ringRenderer);

        labelRoot = new GameObject("RangeLabel").transform;
        labelRoot.SetParent(anchor, false);
        labelRoot.localPosition = new Vector3(0f, 0.82f, 0f);

        var plateObject = new GameObject("LabelPlate");
        plateObject.transform.SetParent(labelRoot, false);
        SpriteRenderer plate = plateObject.AddComponent<SpriteRenderer>();
        plate.sprite = Plate();
        plate.color = new Color(0.08f, 0.12f, 0.1f, 0.9f);
        plate.sortingOrder = 7;
        plate.transform.localScale = new Vector3(1.9f, 0.42f, 1f);

        var text = new GameObject("Label");
        text.transform.SetParent(labelRoot, false);
        label = text.AddComponent<TextMeshPro>();
        label.fontSize = 0.38f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.96f, 0.94f, 0.86f, 1f);
        label.sortingOrder = 8;
        label.overflowMode = TextOverflowModes.Overflow;
        label.enableWordWrapping = false;
        label.rectTransform.sizeDelta = new Vector2(2.6f, 0.5f);

        if (TMP_Settings.defaultFontAsset != null)
        {
            label.font = TMP_Settings.defaultFontAsset;
        }

        anchor.gameObject.SetActive(false);
    }

    private Transform CreateDisc(
        string discName,
        Sprite sprite,
        int sortingOrder,
        out SpriteRenderer renderer)
    {
        var disc = new GameObject(discName);
        disc.transform.SetParent(anchor, false);
        renderer = disc.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return disc.transform;
    }

    private void Update()
    {
        if (!CanShow(out BuildPlot plot, out TowerController defense, out bool affordable))
        {
            anchor.gameObject.SetActive(false);
            return;
        }

        float range = defense.Data.AttackRange;
        anchor.gameObject.SetActive(true);
        anchor.position = plot.transform.position;
        float diameter = Mathf.Max(0.4f, range * 2f);
        var scale = new Vector3(diameter, diameter, 1f);
        fill.localScale = scale;
        ring.localScale = scale;
        labelRoot.localScale = Vector3.one;
        Color tint = affordable
            ? new Color(0.42f, 0.74f, 0.4f, 1f)
            : new Color(0.78f, 0.36f, 0.28f, 1f);
        fillRenderer.color = new Color(tint.r, tint.g, tint.b, 0.16f);
        ringRenderer.color = new Color(tint.r, tint.g, tint.b, 0.9f);
        label.text = affordable ? "Disponible" : "Sin semillas";
    }

    private bool CanShow(
        out BuildPlot plot,
        out TowerController defense,
        out bool affordable)
    {
        plot = null;
        defense = null;
        affordable = false;

        if (gameManager == null ||
            !gameManager.IsPlaying ||
            pauseManager == null ||
            pauseManager.IsPaused ||
            selection == null ||
            selection.Selected == null ||
            selection.Selected.Data == null ||
            !WorldInteractionBlock.AllowsWorldAction(pauseManager))
        {
            return false;
        }

        defense = selection.Selected;
        plot = PlotUnderPointer();

        if (plot == null || plot.IsOccupied)
        {
            return false;
        }

        int cost = defense.BuildCost;
        affordable = economy != null && cost >= 0 && economy.CanAfford(cost);
        return true;
    }

    private BuildPlot PlotUnderPointer()
    {
        if (plots == null || Camera.main == null || Mouse.current == null)
        {
            return null;
        }

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 world = Camera.main.ScreenToWorldPoint(screen);
        var point = new Vector2(world.x, world.y);

        for (int i = 0; i < plots.Length; i++)
        {
            BuildPlot plot = plots[i];

            if (plot == null)
            {
                continue;
            }

            Collider2D collider = plot.GetComponent<Collider2D>();

            if (collider != null && collider.OverlapPoint(point))
            {
                return plot;
            }
        }

        return null;
    }

    private static Sprite FillSprite()
    {
        if (fillSprite != null)
        {
            return fillSprite;
        }

        fillSprite = Disc(false);
        return fillSprite;
    }

    private static Sprite RingSprite()
    {
        if (ringSprite != null)
        {
            return ringSprite;
        }

        ringSprite = Disc(true);
        return ringSprite;
    }

    private static Sprite Disc(bool ringOnly)
    {
        const int size = 64;
        var texture = new Texture2D(size, size);
        var clear = new Color(0f, 0f, 0f, 0f);
        float radius = size * 0.5f - 1.5f;
        var center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                bool inside = distance <= radius;
                bool edge = distance >= radius - 2.5f && distance <= radius;
                texture.SetPixel(x, y, ringOnly
                    ? edge ? Color.white : clear
                    : inside && !edge ? Color.white : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
    }

    private static Sprite Plate()
    {
        if (plateSprite != null)
        {
            return plateSprite;
        }

        var texture = new Texture2D(4, 4);
        var pixel = Color.white;

        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        plateSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 4f, 4f),
            new Vector2(0.5f, 0.5f),
            4f);
        plateSprite.name = "RangeLabelPlate";
        return plateSprite;
    }
}
