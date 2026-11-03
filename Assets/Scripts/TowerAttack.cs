using System;
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
    private TowerController owner;

    public static event Action<string> ConfirmedShot;

    public static void ReleaseSceneListeners()
    {
        ConfirmedShot = null;
    }

    private float attackCooldown;
    private bool isInitialized;

    public bool Initialize(
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

            return false;
        }

        if (data == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "TowerAttack without TowerData.",
                this
            );

            return false;
        }

        if (targeting == null)
        {
            Debug.LogError(
                $"{gameObject.name} requires a " +
                "TowerTargeting reference.",
                this
            );

            return false;
        }

        if (projectilePrefab == null)
        {
            Debug.LogError(
                $"{gameObject.name} requires a " +
                "Projectile prefab.",
                this
            );

            return false;
        }

        if (firePoint == null)
        {
            Debug.LogError(
                $"{gameObject.name} requires a FirePoint.",
                this
            );

            return false;
        }

        if (gameState == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "TowerAttack without a GameManager.",
                this
            );

            return false;
        }

        projectileManager = manager;
        towerData = data;
        gameManager = gameState;
        owner = GetComponent<TowerController>();

        attackCooldown = 0f;
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

        if (towerData.AppliesZone && Time.timeScale <= 0f)
        {
            return;
        }

        if (!InRange(target))
        {
            return;
        }

        Attack(target);

        attackCooldown = HabitatRules.NextCooldown(
            attackCooldown,
            towerData.AttackInterval,
            IntervalMultiplier(),
            true);
    }

    private bool InRange(EnemyHealth target)
    {
        float range = targeting.DetectionRange;
        float distance = Vector2.Distance(
            transform.position,
            target.transform.position);
        return distance <= range;
    }

    private float IntervalMultiplier()
    {
        if (owner == null || HabitatSession.Active == null)
        {
            return 1f;
        }

        return HabitatSession.Active.IntervalMultiplierFor(owner);
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

        if (!Accepts(target))
        {
            return;
        }

        if (towerData.AppliesZone)
        {
            PlaceZone(target);
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

        float slowMultiplier = towerData.AppliesSlow
            ? towerData.SlowMultiplier
            : 1f;
        float slowDuration = towerData.AppliesSlow
            ? towerData.SlowDuration
            : 0f;
        projectile.Initialize(
            target,
            towerData.ProjectileDamage,
            towerData.ProjectileSpeed,
            slowMultiplier,
            slowDuration,
            towerData.TowerId,
            towerData.AppliesArea ? ImpactMode.Area : ImpactMode.Direct,
            towerData.ImpactRadius,
            (int)towerData.AllowedTargets
        );

        if (!projectile.Launched)
        {
            return;
        }

        ConfirmedShot?.Invoke(towerData.TowerId);
    }

    private void PlaceZone(EnemyHealth target)
    {
        if (Time.timeScale <= 0f || !Accepts(target))
        {
            return;
        }

        RootZone zone = RootZone.Create(
            target.transform.position,
            towerData.ZoneRadius,
            towerData.ZoneDuration,
            towerData.ZonePulseDelay,
            towerData.ProjectileDamage,
            towerData.ZoneSlow,
            gameManager,
            towerData.AllowedTargets
        );

        if (zone == null)
        {
            return;
        }

        ConfirmedShot?.Invoke(towerData.TowerId);
    }

    private bool Accepts(EnemyHealth target)
    {
        EnemyTravel travel = target == null || target.Data == null
            ? EnemyTravel.Ground
            : target.Data.Travel;
        return TargetRules.Allows(towerData.AllowedTargets, travel);
    }
}