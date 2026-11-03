using UnityEngine;
using UnityEngine.UI;

public class CaptureConfirmation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer worldRenderer;
    [SerializeField] private EducationalCollectible collectible;
    [SerializeField] private CollectibleVisualMotion visualMotion;
    [SerializeField] private Camera gameplayCamera;

    private void Awake()
    {
        if (collectible == null)
        {
            collectible = GetComponent<EducationalCollectible>();
        }

        if (visualMotion == null)
        {
            visualMotion = GetComponent<CollectibleVisualMotion>();
        }
    }

    private void OnEnable()
    {
        if (collectible != null)
        {
            collectible.OnCaptured += HandleCaptured;
        }
    }

    private void OnDisable()
    {
        if (collectible != null)
        {
            collectible.OnCaptured -= HandleCaptured;
        }
    }

    private void HandleCaptured(
        EducationalCollectible source,
        CollectibleData data)
    {
        if (visualMotion != null && !visualMotion.MotionEnabled)
        {
            return;
        }

        Camera camera = gameplayCamera != null
            ? gameplayCamera
            : Camera.main;

        CaptureConfirmationBurst.Spawn(worldRenderer, camera);
    }
}

public class CaptureConfirmationBurst : MonoBehaviour
{
    private const float Duration = 0.4f;
    private const int OverlayOrder = 1000;

    private Image image;
    private Color baseColor;
    private float age;

    public static void Spawn(SpriteRenderer source, Camera camera)
    {
        if (source == null || source.sprite == null || camera == null)
        {
            return;
        }

        Vector3 screen = camera.WorldToScreenPoint(source.transform.position);

        if (screen.z < 0f)
        {
            return;
        }

        GameObject overlay = new GameObject("CaptureConfirmation");
        Canvas canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OverlayOrder;

        GameObject mark = new GameObject("Mark");
        mark.transform.SetParent(overlay.transform, false);
        Image image = mark.AddComponent<Image>();
        image.sprite = source.sprite;
        image.color = source.color;
        image.raycastTarget = false;
        image.preserveAspect = true;

        RectTransform rect = image.rectTransform;
        rect.sizeDelta = new Vector2(72f, 72f);
        rect.position = new Vector3(screen.x, screen.y, 0f);

        CaptureConfirmationBurst burst =
            overlay.AddComponent<CaptureConfirmationBurst>();
        burst.image = image;
        burst.baseColor = image.color;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(age / Duration);

        if (image != null)
        {
            image.rectTransform.localScale =
                Vector3.one * Mathf.Lerp(1f, 1.18f, progress);
            Color color = baseColor;
            color.a = baseColor.a * (1f - progress);
            image.color = color;
        }

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
