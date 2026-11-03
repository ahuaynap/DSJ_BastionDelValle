using UnityEngine;

[CreateAssetMenu(
    fileName = "LocalMotionSettings",
    menuName = "Bastion del Valle/Mapas/Movimiento local"
)]
public class LocalMotionSettings : ScriptableObject
{
    [SerializeField] private bool steeringEnabled = true;

    [SerializeField, Min(0.1f)]
    private float acceleration = 8f;

    [SerializeField, Min(0.02f)]
    private float waypointTolerance = 0.16f;

    [SerializeField, Min(0.05f)]
    private float arrivalRadius = 0.45f;

    [SerializeField, Min(0.05f)]
    private float agentRadius = 0.22f;

    [SerializeField, Min(0f)]
    private float separationSpeed = 1.1f;

    [SerializeField, Min(0f)]
    private float cohesionSpeed = 0.55f;

    [SerializeField, Min(0f)]
    private float alignmentSpeed = 0.4f;

    [SerializeField, Min(0f)]
    private float flockRadius = 1.35f;

    [SerializeField, Min(0f)]
    private float scatterSpeed = 1.35f;

    [SerializeField, Min(0f)]
    private float scatterDuration = 0.55f;

    [SerializeField]
    private bool drawDiagnostics;

    public bool SteeringEnabled => steeringEnabled;
    public float Acceleration => acceleration;
    public float WaypointTolerance => waypointTolerance;
    public float ArrivalRadius => arrivalRadius;
    public float AgentRadius => agentRadius;
    public float SeparationSpeed => separationSpeed;
    public float CohesionSpeed => cohesionSpeed;
    public float AlignmentSpeed => alignmentSpeed;
    public float FlockRadius => flockRadius;
    public float ScatterSpeed => scatterSpeed;
    public float ScatterDuration => scatterDuration;
    public bool DrawDiagnostics => drawDiagnostics;
}
