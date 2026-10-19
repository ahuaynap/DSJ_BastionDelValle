using UnityEngine;

public class TowerTargeting : MonoBehaviour
{
    private TowerData towerData;
    private GameManager gameManager;
    private EnemyHealth currentTarget;
    private bool isInitialized;

    public EnemyHealth CurrentTarget =>
        currentTarget;

    public float DetectionRange =>
        towerData != null
            ? towerData.AttackRange
            : 0f;

    public bool Initialize(
        TowerData data,
        GameManager manager)
    {
        if (data == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "TowerTargeting without TowerData.",
                this
            );

            return false;
        }

        if (manager == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "TowerTargeting without a GameManager.",
                this
            );

            return false;
        }

        towerData = data;
        gameManager = manager;
        currentTarget = null;
        isInitialized = true;
        return true;
    }

    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        if (gameManager == null ||
            !gameManager.IsPlaying)
        {
            currentTarget = null;
            return;
        }

        FindTarget();

        if (currentTarget != null)
        {
            Debug.DrawLine(
                transform.position,
                currentTarget.transform.position,
                Color.red
            );
        }
    }

    private void FindTarget()
    {
        EnemyHealth[] enemies =
            FindObjectsByType<EnemyHealth>(
                FindObjectsSortMode.None
            );

        EnemyHealth closestEnemy = null;
        float closestDistance = Mathf.Infinity;

        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy == null ||
                enemy.IsDead ||
                !enemy.IsInitialized)
            {
                continue;
            }

            float distance =
                Vector2.Distance(
                    transform.position,
                    enemy.transform.position
                );

            if (distance > DetectionRange)
            {
                continue;
            }

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy;
            }
        }

        currentTarget = closestEnemy;
    }

    private void OnDrawGizmosSelected()
    {
        if (towerData == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            towerData.AttackRange
        );
    }
}