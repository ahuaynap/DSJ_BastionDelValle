using UnityEngine;

public class HeartDamagePulse : MonoBehaviour
{
    [SerializeField] private SpriteRenderer worldRenderer;
    [SerializeField] private BastionHealth health;
    [SerializeField, Min(0.01f)] private float duration = 0.18f;
    [SerializeField] private Color pulseColor = new Color(1f, 0.62f, 0.58f, 1f);

    private Color baseColor = Color.white;
    private bool captured;
    private bool playing;
    private float age;
    private bool missingReported;

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<BastionHealth>();
        }
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnHealthChanged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnHealthChanged -= HandleDamaged;
        }

        Restore();
    }

    private void Update()
    {
        if (!playing || worldRenderer == null)
        {
            return;
        }

        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / duration);
        worldRenderer.color = Color.Lerp(pulseColor, baseColor, progress);

        if (progress >= 1f)
        {
            worldRenderer.color = baseColor;
            playing = false;
        }
    }

    private void HandleDamaged(int current, int max)
    {
        if (worldRenderer == null)
        {
            if (!missingReported)
            {
                missingReported = true;
                Debug.LogWarning(
                    gameObject.name +
                    " has no sprite for the heart pulse.",
                    this
                );
            }

            return;
        }

        if (!captured)
        {
            baseColor = worldRenderer.color;
            captured = true;
        }

        pulseColor.a = baseColor.a;
        age = 0f;
        playing = true;
        worldRenderer.color = pulseColor;
    }

    private void Restore()
    {
        if (captured && worldRenderer != null)
        {
            worldRenderer.color = baseColor;
        }

        playing = false;
    }
}
