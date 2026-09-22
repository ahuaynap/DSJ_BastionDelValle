using System;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private EnemyData enemyData;

    private EnemyPath path;
    private BastionHealth bastion;
    private GameManager gameManager;
    private EnemyHealth enemyHealth;

    private int currentWaypointIndex;
    private bool hasFinished;
    private bool isInitialized;

    public EnemyData Data => enemyData;
    public EnemyHealth Health => enemyHealth;

    public event Action<
        EnemyMovement,
        EnemyFinishReason
    > OnEnemyFinished;

    public void Initialize(
        EnemyPath enemyPath,
        BastionHealth bastionHealth,
        GameManager manager)
    {
        if (enemyData == null)
        {
            Debug.LogError(
                $"{gameObject.name} has no EnemyData assigned.",
                this
            );

            return;
        }

        if (enemyPath == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "without an EnemyPath.",
                this
            );

            return;
        }

        if (manager == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "without a GameManager.",
                this
            );

            return;
        }

        path = enemyPath;
        bastion = bastionHealth;
        gameManager = manager;

        currentWaypointIndex = 0;
        hasFinished = false;

        enemyHealth = GetComponent<EnemyHealth>();

        if (enemyHealth == null)
        {
            Debug.LogError(
                $"{gameObject.name} requires an EnemyHealth component.",
                this
            );

            return;
        }

        enemyHealth.Initialize(enemyData);
        enemyHealth.OnDeath += HandleDeath;

        Transform startWaypoint =
            path.GetWaypoint(0);

        if (startWaypoint != null)
        {
            transform.position =
                startWaypoint.position;
        }

        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        if (hasFinished)
        {
            return;
        }

        if (!gameManager.IsPlaying)
        {
            return;
        }

        Move();
    }

    private void Move()
    {
        Transform targetWaypoint =
            path.GetWaypoint(currentWaypointIndex);

        if (targetWaypoint == null)
        {
            ReachGoal();
            return;
        }

        transform.position =
            Vector3.MoveTowards(
                transform.position,
                targetWaypoint.position,
                enemyData.MovementSpeed *
                Time.deltaTime
            );

        float distance =
            Vector3.Distance(
                transform.position,
                targetWaypoint.position
            );

        if (distance < 0.05f)
        {
            currentWaypointIndex++;
        }
    }

    private void ReachGoal()
    {
        if (hasFinished)
        {
            return;
        }

        hasFinished = true;

        UnsubscribeFromHealth();

        if (bastion != null)
        {
            bastion.TakeDamage(
                enemyData.GoalDamage
            );
        }

        OnEnemyFinished?.Invoke(
            this,
            EnemyFinishReason.ReachedGoal
        );

        Destroy(gameObject);
    }

    private void HandleDeath(
        EnemyHealth health)
    {
        if (hasFinished)
        {
            return;
        }

        hasFinished = true;

        UnsubscribeFromHealth();

        OnEnemyFinished?.Invoke(
            this,
            EnemyFinishReason.Killed
        );

        Destroy(gameObject);
    }

    private void UnsubscribeFromHealth()
    {
        if (enemyHealth == null)
        {
            return;
        }

        enemyHealth.OnDeath -= HandleDeath;
    }

    private void OnDestroy()
    {
        UnsubscribeFromHealth();
    }

    private void OnValidate()
    {
        if (enemyData == null)
        {
            Debug.LogWarning(
                $"{gameObject.name} has no EnemyData assigned.",
                this
            );
        }
    }
}