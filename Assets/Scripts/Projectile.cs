using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField, Min(0.01f)]
    private float hitDistance = 0.1f;

    private EnemyHealth target;

    private int damage;
    private float moveSpeed;

    private bool hasHit;
    private bool isWithdrawn;
    private bool isInitialized;

    public void Initialize(
        EnemyHealth targetEnemy,
        int projectileDamage,
        float projectileSpeed)
    {
        if (targetEnemy == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "without a target.",
                this
            );

            return;
        }

        if (projectileDamage <= 0)
        {
            Debug.LogError(
                $"{gameObject.name} received an invalid " +
                $"projectile damage: {projectileDamage}.",
                this
            );

            return;
        }

        if (projectileSpeed <= 0f)
        {
            Debug.LogError(
                $"{gameObject.name} received an invalid " +
                $"projectile speed: {projectileSpeed}.",
                this
            );

            return;
        }

        target = targetEnemy;
        damage = projectileDamage;
        moveSpeed = projectileSpeed;

        hasHit = false;
        isWithdrawn = false;
        isInitialized = true;
    }

    public void WithdrawWithoutDamage()
    {
        if (isWithdrawn || hasHit)
        {
            return;
        }

        isWithdrawn = true;
        hasHit = true;
        isInitialized = false;
        target = null;

        Destroy(gameObject);
    }

    private void Update()
    {
        if (!isInitialized || hasHit || isWithdrawn)
        {
            return;
        }

        if (target == null || target.IsDead)
        {
            Destroy(gameObject);
            return;
        }

        MoveTowardsTarget();
    }

    private void MoveTowardsTarget()
    {
        transform.position =
            Vector3.MoveTowards(
                transform.position,
                target.transform.position,
                moveSpeed * Time.deltaTime
            );

        float distance =
            Vector2.Distance(
                transform.position,
                target.transform.position
            );

        if (distance <= hitDistance)
        {
            HitTarget();
        }
    }

    private void HitTarget()
    {
        if (hasHit)
        {
            return;
        }

        hasHit = true;

        if (target != null &&
            !target.IsDead)
        {
            target.TakeDamage(damage);
        }

        Destroy(gameObject);
    }

    private void OnValidate()
    {
        if (hitDistance <= 0f)
        {
            hitDistance = 0.01f;
        }
    }
}