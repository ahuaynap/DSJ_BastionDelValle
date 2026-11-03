using System;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EducationalCollectible : MonoBehaviour
{
    [Header("Presentation")]
    [SerializeField]
    private SpriteRenderer worldRenderer;

    private Collider2D interactionCollider;
    private CollectibleVisualMotion visualMotion;
    private GameManager gameManager;
    private PauseManager pauseManager;
    private CollectibleData collectibleData;

    private float remainingLifetime;

    private bool isInitialized;
    private bool isFinished;
    private static bool missingVisualMotionReported;

    public CollectibleData Data =>
        collectibleData;

    public bool IsInitialized =>
        isInitialized;

    public bool IsFinished =>
        isFinished;

    public float RemainingLifetime =>
        remainingLifetime;

    public event Action<
        EducationalCollectible,
        CollectibleData
    > OnCaptured;

    public event Action<
        EducationalCollectible
    > OnExpired;

    private void Awake()
    {
        interactionCollider =
            GetComponent<Collider2D>();

        if (worldRenderer == null)
        {
            worldRenderer =
                GetComponent<SpriteRenderer>();
        }

        visualMotion =
            GetComponent<CollectibleVisualMotion>();

        if (visualMotion == null &&
            !missingVisualMotionReported)
        {
            missingVisualMotionReported = true;

            Debug.LogWarning(
                "EducationalCollectible has no " +
                "CollectibleVisualMotion. The memory " +
                "will stay still.",
                this
            );
        }
    }

    public void Initialize(
        GameManager manager,
        PauseManager pause,
        CollectibleData data,
        float lifetime)
    {
        if (manager == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "without a GameManager.",
                this
            );

            return;
        }

        if (pause == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "without a PauseManager.",
                this
            );

            return;
        }

        if (data == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "without CollectibleData.",
                this
            );

            return;
        }

        if (lifetime <= 0f)
        {
            Debug.LogError(
                $"{gameObject.name} received an invalid " +
                $"lifetime: {lifetime}.",
                this
            );

            return;
        }

        gameManager = manager;
        pauseManager = pause;
        collectibleData = data;
        remainingLifetime = lifetime;

        isFinished = false;
        isInitialized = true;

        ApplyPresentation();

        if (visualMotion != null)
        {
            visualMotion.Initialize(gameManager);
        }

        if (interactionCollider != null)
        {
            interactionCollider.enabled = true;
        }
    }

    private void Update()
    {
        if (!isInitialized || isFinished)
        {
            return;
        }

        if (gameManager == null ||
            !gameManager.IsPlaying)
        {
            return;
        }

        remainingLifetime -= Time.deltaTime;

        if (remainingLifetime <= 0f)
        {
            Expire();
        }
    }

    private void OnMouseDown()
    {
        TryCapture();
    }

    public void TryCapture()
    {
        if (!isInitialized || isFinished)
        {
            return;
        }

        if (gameManager == null ||
            !gameManager.IsPlaying)
        {
            return;
        }

        if (pauseManager == null ||
            !WorldInteractionBlock.AllowsWorldAction(
                pauseManager))
        {
            return;
        }

        FinishAsCaptured();
    }

    public void Cancel()
    {
        if (isFinished)
        {
            return;
        }

        isFinished = true;

        DisableInteraction();

        Destroy(gameObject);
    }

    private void ApplyPresentation()
    {
        if (worldRenderer == null)
        {
            return;
        }

        if (collectibleData.WorldSprite == null)
        {
            return;
        }

        worldRenderer.sprite =
            collectibleData.WorldSprite;
    }

    private void FinishAsCaptured()
    {
        if (!TryBeginFinish())
        {
            return;
        }

        Debug.Log(
            $"{collectibleData.DisplayName} was captured."
        );

        OnCaptured?.Invoke(
            this,
            collectibleData
        );

        Destroy(gameObject);
    }

    private void Expire()
    {
        if (!TryBeginFinish())
        {
            return;
        }

        Debug.Log(
            $"{collectibleData.DisplayName} expired."
        );

        OnExpired?.Invoke(this);

        Destroy(gameObject);
    }

    private bool TryBeginFinish()
    {
        if (isFinished)
        {
            return false;
        }

        isFinished = true;

        DisableInteraction();

        return true;
    }

    private void DisableInteraction()
    {
        if (interactionCollider != null)
        {
            interactionCollider.enabled = false;
        }
    }
}