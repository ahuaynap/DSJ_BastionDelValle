using UnityEngine;

public class WanderingMovement2D : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private WanderingArea2D wanderingArea;

    [SerializeField]
    private GameManager gameManager;

    [Header("Movement")]
    [SerializeField, Min(0.1f)]
    private float maxSpeed = 1.5f;

    [SerializeField, Min(0.1f)]
    private float acceleration = 3f;

    [Header("Wandering")]
    [SerializeField, Min(0.01f)]
    private float wanderRadius = 0.8f;

    [SerializeField, Min(0.01f)]
    private float wanderDistance = 1.25f;

    [SerializeField, Min(0f)]
    private float wanderJitter = 2.5f;

    [Header("Boundaries")]
    [SerializeField, Min(0f)]
    private float boundaryForce = 5f;

    [Header("Orientation")]
    [SerializeField]
    private bool rotateTowardsVelocity = true;

    [SerializeField, Min(0f)]
    private float rotationSpeed = 360f;

    [SerializeField]
    private float rotationOffset = -90f;

    private Vector2 velocity;
    private float wanderAngle;
    private bool isInitialized;

    public Vector2 Velocity => velocity;

    public void Initialize(
        WanderingArea2D area,
        GameManager manager)
    {
        wanderingArea = area;
        gameManager = manager;

        InitializeMovement();
    }

    private void Start()
    {
        if (!isInitialized)
        {
            InitializeMovement();
        }
    }

    private void Update()
    {
        if (!CanMove())
        {
            return;
        }

        CalculateMovement();
        ApplyMovement();
        ApplyRotation();
    }

    private void InitializeMovement()
    {
        if (wanderingArea == null)
        {
            Debug.LogError(
                $"{gameObject.name} requires a " +
                "WanderingArea2D.",
                this
            );

            return;
        }

        if (gameManager == null)
        {
            Debug.LogError(
                $"{gameObject.name} requires a " +
                "GameManager.",
                this
            );

            return;
        }

        Vector2 initialDirection =
            Random.insideUnitCircle.normalized;

        if (initialDirection.sqrMagnitude <= 0f)
        {
            initialDirection = Vector2.right;
        }

        velocity =
            initialDirection *
            maxSpeed *
            0.5f;

        wanderAngle =
            Random.Range(
                0f,
                Mathf.PI * 2f
            );

        transform.position =
            wanderingArea.ClampPoint(
                transform.position
            );

        isInitialized = true;
    }

    private bool CanMove()
    {
        if (!isInitialized)
        {
            return false;
        }

        if (gameManager == null ||
            !gameManager.IsPlaying)
        {
            return false;
        }

        return true;
    }

    private void CalculateMovement()
    {
        wanderAngle +=
            Random.Range(-1f, 1f) *
            wanderJitter *
            Time.deltaTime;

        Vector2 forward =
            velocity.sqrMagnitude > 0.001f
                ? velocity.normalized
                : Vector2.right;

        Vector2 circleCenter =
            forward * wanderDistance;

        Vector2 displacement =
            new Vector2(
                Mathf.Cos(wanderAngle),
                Mathf.Sin(wanderAngle)
            ) * wanderRadius;

        Vector2 wanderTarget =
            circleCenter + displacement;

        Vector2 desiredVelocity =
            wanderTarget.normalized *
            maxSpeed;

        Vector2 steering =
            desiredVelocity - velocity;

        Vector2 boundaryDirection =
            wanderingArea.GetBoundaryDirection(
                transform.position
            );

        steering +=
            boundaryDirection *
            boundaryForce;

        steering =
            Vector2.ClampMagnitude(
                steering,
                acceleration
            );

        velocity +=
            steering * Time.deltaTime;

        velocity =
            Vector2.ClampMagnitude(
                velocity,
                maxSpeed
            );
    }

    private void ApplyMovement()
    {
        Vector2 nextPosition =
            (Vector2)transform.position +
            velocity * Time.deltaTime;

        transform.position =
            wanderingArea.ClampPoint(
                nextPosition
            );
    }

    private void ApplyRotation()
    {
        if (!rotateTowardsVelocity)
        {
            return;
        }

        if (velocity.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float targetAngle =
            Mathf.Atan2(
                velocity.y,
                velocity.x
            ) *
            Mathf.Rad2Deg +
            rotationOffset;

        Quaternion targetRotation =
            Quaternion.Euler(
                0f,
                0f,
                targetAngle
            );

        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed *
                Time.deltaTime
            );
    }

    private void OnValidate()
    {
        maxSpeed =
            Mathf.Max(0.1f, maxSpeed);

        acceleration =
            Mathf.Max(0.1f, acceleration);

        wanderRadius =
            Mathf.Max(0.01f, wanderRadius);

        wanderDistance =
            Mathf.Max(0.01f, wanderDistance);

        wanderJitter =
            Mathf.Max(0f, wanderJitter);

        boundaryForce =
            Mathf.Max(0f, boundaryForce);

        rotationSpeed =
            Mathf.Max(0f, rotationSpeed);
    }
}