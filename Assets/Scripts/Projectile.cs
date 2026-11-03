using System;
using UnityEngine;

public enum ImpactMode
{
    Direct = 0,
    Area = 1
}

public class Projectile : MonoBehaviour
{
    [SerializeField, Min(0.01f)]
    private float hitDistance = 0.1f;

    private EnemyHealth target;

    private int damage;
    private float moveSpeed;
    private float slowMultiplier = 1f;
    private float slowDuration;
    private string impactCue = string.Empty;
    private ImpactMode impactMode = ImpactMode.Direct;
    private float areaRadius;
    private TravelMask allowedTargets = TravelMask.Ground;
    private static EnemyHealth[] areaBuffer = new EnemyHealth[32];
    private static bool missingRosterReported;

    private bool hasHit;
    private bool isWithdrawn;
    private bool isInitialized;

    public bool Launched => isInitialized;

    public static event Action<string> ConfirmedImpact;

    public void Initialize(
        EnemyHealth targetEnemy,
        int projectileDamage,
        float projectileSpeed)
    {
        Initialize(
            targetEnemy,
            projectileDamage,
            projectileSpeed,
            1f,
            0f,
            string.Empty
        );
    }

    public void Initialize(
        EnemyHealth targetEnemy,
        int projectileDamage,
        float projectileSpeed,
        float appliedSlowMultiplier,
        float appliedSlowDuration,
        string cueId)
    {
        Initialize(
            targetEnemy,
            projectileDamage,
            projectileSpeed,
            appliedSlowMultiplier,
            appliedSlowDuration,
            cueId,
            ImpactMode.Direct,
            0f
        );
    }

    public void Initialize(
        EnemyHealth targetEnemy,
        int projectileDamage,
        float projectileSpeed,
        float appliedSlowMultiplier,
        float appliedSlowDuration,
        string cueId,
        ImpactMode mode,
        float radius,
        int allowedTargets = 0)
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
        slowMultiplier = appliedSlowMultiplier;
        slowDuration = appliedSlowDuration;
        impactCue = cueId ?? string.Empty;
        impactMode = mode;
        areaRadius = radius > 0f ? radius : 0f;
        this.allowedTargets = TargetRules.Normalize(allowedTargets);

        hasHit = false;
        isWithdrawn = false;
        isInitialized = true;

        float distance = Vector2.Distance(
            transform.position,
            target.transform.position);
        float step = moveSpeed * Time.deltaTime;

        if (distance <= hitDistance ||
            (Time.deltaTime > 0f && distance <= step))
        {
            LaunchMark.Show(transform.position);
            HitTarget();
        }
    }

    public static void ReleaseSceneListeners()
    {
        ConfirmedImpact = null;
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

        if (!AreaStrike.ShouldDetonate(target != null && !target.IsDead))
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
        Vector2 center = transform.position;

        if (impactMode == ImpactMode.Area && areaRadius > 0f)
        {
            Detonate(center);
        }
        else if (target != null && !target.IsDead && Accepts(target))
        {
            ConfirmedImpact?.Invoke(impactCue);
            target.TakeDamage(damage);
            ApplySlow(target);
        }

        Destroy(gameObject);
    }

    private void Detonate(Vector2 center)
    {
        ConfirmedImpact?.Invoke(impactCue);
        CopyArea(center, out int used);

        for (int i = 0; i < used; i++)
        {
            EnemyHealth health = areaBuffer[i];

            if (health == null || health.IsDead)
            {
                continue;
            }

            health.TakeDamage(damage);
        }

        for (int i = 0; i < used; i++)
        {
            EnemyHealth health = areaBuffer[i];
            areaBuffer[i] = null;

            if (health == null || health.IsDead)
            {
                continue;
            }

            EnemyMovement movement = health.GetComponent<EnemyMovement>();

            if (movement != null)
            {
                movement.ReceiveScatter(center);
            }
        }

        AreaPulse.Show(center, areaRadius);
    }

    private int CopyArea(Vector2 center, out int used)
    {
        used = 0;
        ChannelNavigator roster = ChannelNavigator.Current;

        if (roster == null)
        {
            if (!missingRosterReported)
            {
                missingRosterReported = true;
                Debug.LogWarning(
                    "El impacto en área no tiene registro de enemigos.",
                    this
                );
            }
        }
        else
        {
            var agents = roster.ActiveAgents;
            EnsureAreaBuffer(agents.Count);

            for (int i = 0; i < agents.Count; i++)
            {
                EnemyMovement agent = agents[i];

                if (agent == null || !agent.IsReady || agent.Health == null)
                {
                    continue;
                }

                EnemyHealth health = agent.Health;

                if (health.IsDead || !health.IsInitialized || !Accepts(health))
                {
                    continue;
                }

                float distance = Vector2.Distance(
                    center,
                    agent.transform.position
                );

                if (distance > areaRadius)
                {
                    continue;
                }

                areaBuffer[used] = health;
                used++;
            }
        }

        IncludePrimary(center, ref used);
        return used;
    }

    private void IncludePrimary(Vector2 center, ref int used)
    {
        if (target == null || target.IsDead || !target.IsInitialized || !Accepts(target))
        {
            return;
        }

        for (int i = 0; i < used; i++)
        {
            if (areaBuffer[i] == target)
            {
                return;
            }
        }

        if (Vector2.Distance(center, target.transform.position) > areaRadius)
        {
            return;
        }

        EnsureAreaBuffer(used + 1);
        areaBuffer[used] = target;
        used++;
    }

    private bool Accepts(EnemyHealth health)
    {
        EnemyTravel travel = health == null || health.Data == null
            ? EnemyTravel.Ground
            : health.Data.Travel;
        return TargetRules.Allows(allowedTargets, travel);
    }

    private static void EnsureAreaBuffer(int count)
    {
        if (areaBuffer.Length >= count)
        {
            return;
        }

        var next = new EnemyHealth[count];
        Array.Copy(areaBuffer, next, areaBuffer.Length);
        areaBuffer = next;
    }

    private void ApplySlow(EnemyHealth health)
    {
        if (!SlowSchedule.ShouldApply(
                true,
                false,
                slowDuration,
                slowMultiplier) ||
            health.Data == null)
        {
            return;
        }

        EnemySlow slow = health.GetComponent<EnemySlow>();

        if (slow == null)
        {
            slow = health.gameObject.AddComponent<EnemySlow>();
        }

        slow.Bind(health.Data.MovementSpeed);
        slow.Apply(slowMultiplier, slowDuration);
    }

    private void OnValidate()
    {
        if (hitDistance <= 0f)
        {
            hitDistance = 0.01f;
        }
    }
}