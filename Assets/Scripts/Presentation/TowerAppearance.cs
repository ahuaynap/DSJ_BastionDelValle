using UnityEngine;

public class TowerAppearance : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField, Min(0.01f)] private float duration = 0.18f;

    private Vector3 baseScale;
    private bool captured;
    private bool playing;
    private float age;
    private bool rootWarningReported;

    public void Play()
    {
        if (visual == null || visual == transform)
        {
            if (!rootWarningReported)
            {
                rootWarningReported = true;
                Debug.LogWarning(
                    gameObject.name +
                    " has no visual child. The build pop " +
                    "does not scale the logical root.",
                    this
                );
            }

            return;
        }

        if (!captured)
        {
            baseScale = visual.localScale;
            captured = true;
        }

        age = 0f;
        playing = true;
        visual.localScale = baseScale * 0.9f;
    }

    private void OnDisable()
    {
        Restore();
    }

    private void Update()
    {
        if (!playing || visual == null)
        {
            return;
        }

        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / duration);
        float scale = Mathf.Lerp(0.9f, 1f, progress);
        visual.localScale = baseScale * scale;

        if (progress >= 1f)
        {
            visual.localScale = baseScale;
            playing = false;
        }
    }

    private void Restore()
    {
        if (!captured || visual == null)
        {
            playing = false;
            return;
        }

        visual.localScale = baseScale;
        playing = false;
    }
}
