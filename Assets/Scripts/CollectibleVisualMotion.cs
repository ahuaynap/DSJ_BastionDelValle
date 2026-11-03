using UnityEngine;

public class CollectibleVisualMotion : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField]
    private Transform visualRoot;

    [Header("Motion")]
    [SerializeField]
    private bool motionEnabled = true;

    [SerializeField, Min(0f)]
    private float floatAmplitude = 0.04f;

    [SerializeField, Min(0f)]
    private float floatFrequency = 0.8f;

    [SerializeField, Min(0f)]
    private float pulseAmplitude = 0.03f;

    [SerializeField, Min(0f)]
    private float pulseFrequency = 0.6f;

    private GameManager gameManager;
    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;
    private float animationTime;
    private bool baseCaptured;
    private bool isInitialized;
    private bool invalidTargetReported;

    public bool MotionEnabled => motionEnabled;

    public void Initialize(GameManager manager)
    {
        gameManager = manager;
        isInitialized = manager != null;
        CaptureBaseOnce();

        if (!motionEnabled)
        {
            animationTime = 0f;
            RestoreBasePose();
        }
    }

    private void Awake()
    {
        CaptureBaseOnce();
    }

    private void OnEnable()
    {
        CaptureBaseOnce();

        if (motionEnabled && CanAnimate())
        {
            ApplyPose();
        }
    }

    private void OnDisable()
    {
        RestoreBasePose();
    }

    private void Update()
    {
        if (!baseCaptured)
        {
            return;
        }

        if (!motionEnabled)
        {
            animationTime = 0f;
            RestoreBasePose();
            return;
        }

        if (!CanAnimate())
        {
            return;
        }

        animationTime += Time.deltaTime;
        ApplyPose();
    }

    private bool CanAnimate()
    {
        return isInitialized &&
            gameManager != null &&
            gameManager.IsPlaying;
    }

    private void CaptureBaseOnce()
    {
        if (baseCaptured || !HasValidVisual())
        {
            return;
        }

        baseLocalPosition = visualRoot.localPosition;
        baseLocalScale = visualRoot.localScale;
        baseCaptured = true;
    }

    private void ApplyPose()
    {
        if (!HasValidVisual())
        {
            return;
        }

        float floatOffset =
            Mathf.Sin(Phase(floatFrequency)) *
            floatAmplitude;

        float pulse =
            1f +
            Mathf.Sin(Phase(pulseFrequency)) *
            pulseAmplitude;

        Vector3 position = baseLocalPosition;
        position.y += floatOffset;
        visualRoot.localPosition = position;
        visualRoot.localScale = baseLocalScale * pulse;
    }

    private float Phase(float frequency)
    {
        return frequency *
            animationTime *
            Mathf.PI *
            2f;
    }

    private void RestoreBasePose()
    {
        if (!baseCaptured || visualRoot == null)
        {
            return;
        }

        visualRoot.localPosition = baseLocalPosition;
        visualRoot.localScale = baseLocalScale;
    }

    private bool HasValidVisual()
    {
        if (visualRoot == null)
        {
            return false;
        }

        if (visualRoot != transform)
        {
            return true;
        }

        if (!invalidTargetReported)
        {
            invalidTargetReported = true;
            Debug.LogError(
                $"{name} cannot animate its own root. " +
                "Assign the Visual child.",
                this
            );
        }

        return false;
    }

    private void OnValidate()
    {
        floatAmplitude =
            Mathf.Max(0f, floatAmplitude);

        floatFrequency =
            Mathf.Max(0f, floatFrequency);

        pulseAmplitude =
            Mathf.Max(0f, pulseAmplitude);

        pulseFrequency =
            Mathf.Max(0f, pulseFrequency);

        if (!motionEnabled)
        {
            animationTime = 0f;
            RestoreBasePose();
        }
    }
}
