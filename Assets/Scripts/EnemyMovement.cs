using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private EnemyData enemyData;

    [Header("Presentation")]
    [SerializeField] private SpriteRenderer worldRenderer;

    private EnemyPath path;
    private Vector3[] routePoints;
    private ChannelNavigator navigator;
    private BastionHealth bastion;
    private GameManager gameManager;
    private EnemyHealth enemyHealth;
    private EnemySlow slowEffect;

    private int currentWaypointIndex;
    private int followedVersion;
    private int appearanceId;
    private int groupId;
    private bool usesPoints;
    private bool airborne;
    private bool hasFinished;
    private bool isInitialized;
    private float stuckTime;
    private float scatterTime;
    private float scatterX;
    private float scatterY;
    private float cohesion = 1f;
    private Vector2 planarVelocity;
    private Vector2 lastSeparation;
    private static bool missingRendererReported;
    private readonly NeighborSample[] neighbors = new NeighborSample[32];

    public EnemyData Data => enemyData;
    public EnemyHealth Health => enemyHealth;
    public bool IsReady => isInitialized;
    public int AppearanceId => appearanceId;
    public int GroupId => groupId;
    public bool IsAir =>
        airborne ||
        (enemyData != null && enemyData.Travel == EnemyTravel.Air);
    public Vector2 PlanarVelocity => planarVelocity;
    public Vector2 LastSeparation => lastSeparation;

    public void AssignAppearance(int id)
    {
        appearanceId = id;
    }

    public void AssignGroup(int id)
    {
        groupId = id < 0 ? 0 : id;
    }

    public void ReceiveScatter(Vector2 center)
    {
        if (!UsesSteering() || navigator.Motion == null)
        {
            return;
        }

        float dx = transform.position.x - center.x;
        float dy = transform.position.y - center.y;
        float distance = Mathf.Sqrt(dx * dx + dy * dy);

        if (distance <= 0.0001f)
        {
            LocalSteer.StableDirection(appearanceId, 1, out dx, out dy);
        }
        else
        {
            dx /= distance;
            dy /= distance;
        }

        float speed = navigator.Motion.ScatterSpeed;
        scatterX = dx * speed;
        scatterY = dy * speed;
        scatterTime = navigator.Motion.ScatterDuration;
        cohesion = 0f;
    }

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
        BindSlow();

        Transform startWaypoint =
            path.GetWaypoint(0);

        if (startWaypoint != null)
        {
            transform.position =
                startWaypoint.position;
        }

        ApplyPresentation();

        isInitialized = true;
    }

    public void BeginAlongRoute(
        Vector3[] points,
        BastionHealth bastionHealth,
        GameManager manager,
        ChannelNavigator source,
        int navigationVersion,
        bool airCorridor = false)
    {
        if (enemyData == null)
        {
            Debug.LogError(
                $"{gameObject.name} has no EnemyData assigned.",
                this
            );

            return;
        }

        if (points == null || points.Length == 0)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "without a route.",
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

        usesPoints = true;
        airborne = airCorridor;
        routePoints = (Vector3[])points.Clone();
        navigator = source;
        followedVersion = navigationVersion;
        bastion = bastionHealth;
        gameManager = manager;
        currentWaypointIndex = 0;
        hasFinished = false;
        stuckTime = 0f;
        scatterTime = 0f;
        scatterX = 0f;
        scatterY = 0f;
        cohesion = 1f;
        planarVelocity = Vector2.zero;
        lastSeparation = Vector2.zero;

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
        BindSlow();
        ApplyPresentation();
        EnsureFlightShadow();
        isInitialized = true;
    }

    public void Retarget(Vector3[] points, int navigationVersion)
    {
        if (airborne || !isInitialized || hasFinished || !usesPoints)
        {
            return;
        }

        if (points == null || points.Length == 0)
        {
            return;
        }

        if (navigationVersion < followedVersion)
        {
            return;
        }

        routePoints = (Vector3[])points.Clone();
        currentWaypointIndex = 0;
        followedVersion = navigationVersion;
        RevalidatePlanar();
    }

    private void ApplyPresentation()
    {
        if (worldRenderer == null)
        {
            if (missingRendererReported)
            {
                return;
            }

            missingRendererReported = true;
            Debug.LogWarning(
                $"{gameObject.name} has no world " +
                "SpriteRenderer. The temporary enemy " +
                "graphic stays.",
                this
            );

            return;
        }

        if (enemyData.WorldSprite == null)
        {
            return;
        }

        worldRenderer.sprite = enemyData.WorldSprite;
    }

    private void EnsureFlightShadow()
    {
        if (!IsAir || transform.Find("FlightShadow") != null)
        {
            return;
        }

        var shadow = new GameObject("FlightShadow");
        shadow.transform.SetParent(transform, false);
        shadow.transform.localPosition = new Vector3(0.1f, -0.42f, 0f);
        shadow.transform.localScale = new Vector3(0.7f, 0.28f, 1f);
        SpriteRenderer renderer = shadow.AddComponent<SpriteRenderer>();
        renderer.sprite = worldRenderer != null ? worldRenderer.sprite : null;
        renderer.color = new Color(0.08f, 0.1f, 0.08f, 0.28f);
        renderer.sortingOrder = (worldRenderer != null ? worldRenderer.sortingOrder : 0) - 3;
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
        if (!TryCurrentTarget(out Vector3 target))
        {
            ReachGoal();
            return;
        }

        if (UsesSteering())
        {
            Steer();
            return;
        }

        transform.position =
            Vector3.MoveTowards(
                transform.position,
                target,
                StepSpeed() * Time.deltaTime
            );
        FaceTravel(target - transform.position);

        float distance =
            Vector3.Distance(
                transform.position,
                target
            );

        if (distance < 0.05f)
        {
            currentWaypointIndex++;
        }
    }

    private bool TryCurrentTarget(out Vector3 target)
    {
        if (usesPoints)
        {
            if (routePoints == null ||
                currentWaypointIndex < 0 ||
                currentWaypointIndex >= routePoints.Length)
            {
                target = default;
                return false;
            }

            target = routePoints[currentWaypointIndex];
            return true;
        }

        Transform waypoint = path.GetWaypoint(currentWaypointIndex);

        if (waypoint == null)
        {
            target = default;
            return false;
        }

        target = waypoint.position;
        return true;
    }

    private void ReachGoal()
    {
        if (hasFinished)
        {
            return;
        }

        hasFinished = true;

        UnsubscribeFromHealth();
        ClearSlow();
        ReleaseFromNavigator();

        HabitatSession.Active?.NoteArrival();

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
        ClearSlow();
        ReleaseFromNavigator();

        OnEnemyFinished?.Invoke(
            this,
            EnemyFinishReason.Killed
        );

        Destroy(gameObject);
    }

    private bool UsesSteering()
    {
        return usesPoints &&
            navigator != null &&
            navigator.Motion != null &&
            navigator.Motion.SteeringEnabled;
    }

    private void Steer()
    {
        LocalMotionSettings motion = navigator.Motion;
        float tolerance = motion.WaypointTolerance;
        AdvancePassedPoints(tolerance);

        if (!TryCurrentTarget(out Vector3 target))
        {
            ReachGoal();
            return;
        }

        bool finalLeg = routePoints != null &&
            currentWaypointIndex >= routePoints.Length - 1;
        float goalX = transform.position.x - target.x;
        float goalY = transform.position.y - target.y;
        bool reachedGoal = goalX * goalX + goalY * goalY <= tolerance * tolerance;

        bool passedGoal = routePoints != null &&
            routePoints.Length >= 2 &&
            PassedPoint(routePoints, routePoints.Length - 2);

        if (finalLeg && (reachedGoal || passedGoal))
        {
            ReachGoal();
            return;
        }

        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
        {
            return;
        }

        LocalSteer.AdvanceFlockResponse(
            scatterTime,
            cohesion,
            deltaTime,
            motion.ScatterDuration,
            out scatterTime,
            out cohesion);

        if (scatterTime <= 0f)
        {
            scatterX = 0f;
            scatterY = 0f;
        }

        int neighborCount = CopyNeighbors();
        float separationScale = stuckTime > 0.45f ? 0.15f : 1f;
        float flockScale = airborne ? 0f : cohesion;

        if (!airborne && stuckTime > 0.45f)
        {
            flockScale *= 0.15f;
        }

        float bodyRadius = BodyRadius(motion);
        var limits = new MotionLimits
        {
            MaxSpeed = StepSpeed(),
            Acceleration = motion.Acceleration,
            ArrivalRadius = motion.ArrivalRadius,
            SeparationSpeed = motion.SeparationSpeed,
            CohesionSpeed = motion.CohesionSpeed,
            AlignmentSpeed = motion.AlignmentSpeed,
            FlockRadius = motion.FlockRadius
        };
        SteerResult result = LocalSteer.Step(
            navigator.Navigation,
            navigator.OriginX,
            navigator.OriginY,
            navigator.CellSize,
            transform.position.x,
            transform.position.y,
            planarVelocity.x,
            planarVelocity.y,
            appearanceId,
            target.x,
            target.y,
            finalLeg,
            deltaTime,
            bodyRadius,
            limits,
            neighbors,
            neighborCount,
            separationScale,
            groupId,
            flockScale,
            scatterX,
            scatterY,
            airborne);
        planarVelocity = new Vector2(result.VelocityX, result.VelocityY);
        lastSeparation = new Vector2(result.SeparationX, result.SeparationY);
        float moved =
            Mathf.Abs(result.X - transform.position.x) +
            Mathf.Abs(result.Y - transform.position.y);

        if (moved < 0.01f)
        {
            stuckTime += deltaTime;
        }
        else
        {
            stuckTime = Mathf.Max(0f, stuckTime - deltaTime);
        }

        transform.position = new Vector3(
            result.X,
            result.Y,
            transform.position.z);
        FaceTravel(planarVelocity);
    }

    private void AdvancePassedPoints(float tolerance)
    {
        while (routePoints != null &&
            currentWaypointIndex < routePoints.Length - 1)
        {
            Vector3 point = routePoints[currentWaypointIndex];
            float dx = transform.position.x - point.x;
            float dy = transform.position.y - point.y;
            bool inside = dx * dx + dy * dy <= tolerance * tolerance;

            if (!inside && !HasPassedWaypoint(routePoints, currentWaypointIndex))
            {
                break;
            }

            currentWaypointIndex++;
        }
    }

    private bool HasPassedWaypoint(Vector3[] points, int index)
    {
        return index > 0 && PassedPoint(points, index - 1);
    }

    private bool PassedPoint(Vector3[] points, int index)
    {
        return SegmentProgress(points, index, out float along, out float lengthSq) &&
            along > lengthSq;
    }

    private bool SegmentProgress(
        Vector3[] points,
        int index,
        out float along,
        out float lengthSq)
    {
        along = 0f;
        lengthSq = 0f;

        if (points == null || index < 0 || index >= points.Length - 1)
        {
            return false;
        }

        Vector3 point = points[index];
        Vector3 next = points[index + 1];
        float segmentX = next.x - point.x;
        float segmentY = next.y - point.y;
        lengthSq = segmentX * segmentX + segmentY * segmentY;

        if (lengthSq <= 0.0001f)
        {
            return false;
        }

        along =
            (transform.position.x - point.x) * segmentX +
            (transform.position.y - point.y) * segmentY;
        return true;
    }

    private void FaceTravel(Vector2 heading)
    {
        if (worldRenderer == null || heading.sqrMagnitude < 0.0004f)
        {
            return;
        }

        float angle = Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg;
        worldRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private int CopyNeighbors()
    {
        IReadOnlyList<EnemyMovement> agents = navigator.ActiveAgents;
        int count = 0;

        if (agents == null)
        {
            return 0;
        }

        float reach = BodyRadius(navigator.Motion) * 2.5f;

        if (!airborne && groupId != 0 && navigator.Motion.FlockRadius > reach)
        {
            reach = navigator.Motion.FlockRadius;
        }

        float reachSq = reach * reach;

        for (int i = 0; i < agents.Count; i++)
        {
            EnemyMovement agent = agents[i];

            if (agent == null || agent == this || !agent.IsReady || agent.IsAir != IsAir)
            {
                continue;
            }

            float dx = transform.position.x - agent.transform.position.x;
            float dy = transform.position.y - agent.transform.position.y;

            if (dx * dx + dy * dy > reachSq)
            {
                continue;
            }

            if (count >= neighbors.Length)
            {
                break;
            }

            neighbors[count] = new NeighborSample
            {
                X = agent.transform.position.x,
                Y = agent.transform.position.y,
                Identity = agent.AppearanceId,
                GroupId = agent.GroupId,
                VelocityX = agent.PlanarVelocity.x,
                VelocityY = agent.PlanarVelocity.y
            };
            count++;
        }

        return count;
    }

    private float BodyRadius(LocalMotionSettings motion)
    {
        if (enemyData != null && enemyData.BodyRadius > 0f)
        {
            return enemyData.BodyRadius;
        }

        return motion.AgentRadius;
    }

    private void RevalidatePlanar()
    {
        float maxSpeed = StepSpeed();
        LocalSteer.Clamp(
            planarVelocity.x,
            planarVelocity.y,
            maxSpeed,
            out float velocityX,
            out float velocityY);
        planarVelocity = new Vector2(velocityX, velocityY);

        if (!UsesSteering())
        {
            return;
        }

        Vector3 ahead = transform.position +
            new Vector3(planarVelocity.x, planarVelocity.y, 0f) * 0.05f;

        if (!navigator.AllowsDisplacement(
                transform.position,
                ahead,
                BodyRadius(navigator.Motion)))
        {
            planarVelocity = Vector2.zero;
        }
    }

    private void ReleaseFromNavigator()
    {
        if (navigator == null)
        {
            return;
        }

        navigator.Release(this);
    }

    private void BindSlow()
    {
        slowEffect = GetComponent<EnemySlow>();

        if (slowEffect == null)
        {
            slowEffect = gameObject.AddComponent<EnemySlow>();
        }

        slowEffect.enabled = true;
        slowEffect.Prepare(enemyData.MovementSpeed);
    }

    private float StepSpeed()
    {
        if (slowEffect == null)
        {
            return enemyData.MovementSpeed;
        }

        return slowEffect.EffectiveSpeed;
    }

    private void ClearSlow()
    {
        if (slowEffect != null)
        {
            slowEffect.Clear();
        }
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
        ReleaseFromNavigator();

        if (navigator != null)
        {
            navigator = null;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!UsesSteering() || !navigator.Motion.DrawDiagnostics)
        {
            return;
        }

        if (routePoints != null)
        {
            Gizmos.color = new Color(0.95f, 0.86f, 0.45f, 0.9f);

            for (int i = 1; i < routePoints.Length; i++)
            {
                Gizmos.DrawLine(routePoints[i - 1], routePoints[i]);
            }
        }

        Vector3 position = transform.position;
        Gizmos.color = new Color(0.35f, 0.75f, 0.95f, 0.9f);
        Gizmos.DrawLine(
            position,
            position + new Vector3(planarVelocity.x, planarVelocity.y, 0f) * 0.35f);
        Gizmos.color = new Color(0.95f, 0.45f, 0.35f, 0.9f);
        Gizmos.DrawLine(
            position,
            position + new Vector3(lastSeparation.x, lastSeparation.y, 0f) * 0.35f);
    }
#endif

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