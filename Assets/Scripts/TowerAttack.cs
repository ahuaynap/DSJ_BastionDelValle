using UnityEngine;

public class TowerAttack : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TowerTargeting targeting;

    [Header("Projectile")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform firePoint;

    private ProjectileManager projectileManager;
    private TowerData towerData;
    private GameManager gameManager;

    private float attackCooldown;
    private bool isInitialized;

    public void Initialize(
        ProjectileManager manager,
        TowerData data,
        GameManager gameState)
    {
        if (manager == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "TowerAttack without ProjectileManager.",
                this
            );

            return;
        }

        if (data == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "TowerAttack without TowerData.",
                this
            );

            return;
        }

        if (targeting == null)
        {
            Debug.LogError(
                $"{gameObject.name} requires a " +
                "TowerTargeting reference.",
                this
            );

            return;
        }

        if (projectilePrefab == null)
        {
            Debug.LogError(
                $"{gameObject.name} requires a " +
                "Projectile prefab.",
                this
            );

            return;
        }

        if (firePoint == null)
        {
            Debug.LogError(
                $"{gameObject.name} requires a FirePoint.",
                this
            );

            return;
        }

        if (gameState == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "TowerAttack without a GameManager.",
                this
            );

            return;
        }

        projectileManager = manager;
        towerData = data;
        gameManager = gameState;

        attackCooldown = 0f;
        isInitialized = true;
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
            return;
        }

        if (attackCooldown > 0f)
        {
            attackCooldown -= Time.deltaTime;
        }

        EnemyHealth target =
            targeting.CurrentTarget;

        if (target == null ||
            target.IsDead ||
            !target.IsInitialized)
        {
            return;
        }

        if (attackCooldown > 0f)
        {
            return;
        }

        Attack(target);

        attackCooldown =
            towerData.AttackInterval;
    }

    private void Attack(EnemyHealth target)
    {
        if (gameManager == null ||
            !gameManager.IsPlaying)
        {
            return;
        }

        if (target == null ||
            target.IsDead ||
            !target.IsInitialized)
        {
            return;
        }

        Projectile projectile =
            projectileManager.SpawnProjectile(
                projectilePrefab,
                firePoint.position
            );

        if (projectile == null)
        {
            return;
        }

        projectile.Initialize(
            target,
            towerData.ProjectileDamage,
            towerData.ProjectileSpeed
        );
    }
}