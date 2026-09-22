using System;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EducationalCollectible : MonoBehaviour
{
    [Header("Presentation")]
    [SerializeField]
    private SpriteRenderer worldRenderer;

    private Collider2D interactionCollider;
    private GameManager gameManager;
    private CollectibleData collectibleData;

    private float remainingLifetime;

    private bool isInitialized;
    private bool isFinished;

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
    }

    public void Initialize(
        GameManager manager,
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
        collectibleData = data;
        remainingLifetime = lifetime;

        isFinished = false;
        isInitialized = true;

        ApplyPresentation();

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