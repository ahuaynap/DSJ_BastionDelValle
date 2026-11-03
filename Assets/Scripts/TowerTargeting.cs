using UnityEngine;

public class TowerTargeting : MonoBehaviour
{
    private TowerData towerData;
    private GameManager gameManager;
    private EnemyHealth currentTarget;
    private bool isInitialized;
    private bool listening;
    private float rangeMultiplier = 1f;

    public EnemyHealth CurrentTarget =>
        currentTarget;

    public float DetectionRange =>
        towerData != null
            ? HabitatRules.Scale(towerData.AttackRange, rangeMultiplier)
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
        Listen();
        return true;
    }

    private void OnDestroy()
    {
        if (listening && HabitatSession.Active != null)
        {
            HabitatSession.Active.SynergiesChanged -= RefreshRange;
        }

        listening = false;
    }

    private void Listen()
    {
        if (listening || HabitatSession.Active == null)
        {
            return;
        }

        HabitatSession.Active.SynergiesChanged += RefreshRange;
        listening = true;
        RefreshRange();
    }

    private void RefreshRange()
    {
        TowerController owner = GetComponent<TowerController>();
        rangeMultiplier = HabitatSession.Active == null || owner == null
            ? 1f
            : HabitatSession.Active.RangeMultiplierFor(owner);
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

        EnemyHealth closest = null;
        EnemyHealth closestAir = null;
        EnemyHealth closestGround = null;
        float closestDistance = Mathf.Infinity;
        float airDistance = Mathf.Infinity;
        float groundDistance = Mathf.Infinity;
        TravelMask allowed = towerData.AllowedTargets;
        bool preferAir = towerData.Aim == TargetPriority.AirThenGround;

        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy == null ||
                enemy.IsDead ||
                !enemy.IsInitialized)
            {
                continue;
            }

            EnemyTravel travel = enemy.Data == null
                ? EnemyTravel.Ground
                : enemy.Data.Travel;

            if (!TargetRules.Allows(allowed, travel))
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
                closest = enemy;
            }

            if (travel == EnemyTravel.Air && distance < airDistance)
            {
                airDistance = distance;
                closestAir = enemy;
            }
            else if (travel != EnemyTravel.Air && distance < groundDistance)
            {
                groundDistance = distance;
                closestGround = enemy;
            }
        }

        if (preferAir)
        {
            currentTarget = closestAir != null ? closestAir : closestGround;
            return;
        }

        currentTarget = closest;
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
            DetectionRange
        );
    }
}