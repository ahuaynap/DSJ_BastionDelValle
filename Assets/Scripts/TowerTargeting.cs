using UnityEngine;

public class TowerTargeting : MonoBehaviour
{
    private TowerData towerData;
    private EnemyHealth currentTarget;
    private bool isInitialized;

    public EnemyHealth CurrentTarget =>
        currentTarget;

    public float DetectionRange =>
        towerData != null
            ? towerData.AttackRange
            : 0f;

    public void Initialize(TowerData data)
    {
        if (data == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "TowerTargeting without TowerData.",
                this
            );

            return;
        }

        towerData = data;
        currentTarget = null;
        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized)
        {
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