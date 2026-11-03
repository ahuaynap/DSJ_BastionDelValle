using UnityEngine;

public class EnemyDamageFlash : MonoBehaviour
{
    [SerializeField] private SpriteRenderer worldRenderer;
    [SerializeField] private EnemyHealth health;
    [SerializeField, Min(0.01f)] private float duration = 0.12f;
    [SerializeField] private Color flashColor = new Color(0.72f, 0.38f, 0.34f, 1f);

    private Color baseColor = Color.white;
    private bool captured;
    private bool playing;
    private float age;
    private bool missingReported;

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<EnemyHealth>();
        }
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDamaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
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
        worldRenderer.color = Color.Lerp(flashColor, baseColor, progress);

        if (progress >= 1f)
        {
            worldRenderer.color = baseColor;
            playing = false;
        }
    }

    private void HandleDamaged(EnemyHealth damaged)
    {
        if (worldRenderer == null)
        {
            if (!missingReported)
            {
                missingReported = true;
                Debug.LogWarning(
                    gameObject.name +
                    " has no sprite for the damage flash.",
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

        flashColor.a = baseColor.a;
        age = 0f;
        playing = true;
        worldRenderer.color = flashColor;
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
