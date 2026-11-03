using UnityEngine;

[CreateAssetMenu(
    fileName = "TowerData_New",
    menuName = "Bastion del Valle/Towers/Tower Data"
)]
public class TowerData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string towerId = "tower_basic";
    [SerializeField] private string displayName = "Basic Tower";

    [TextArea(2, 4)]
    [SerializeField] private string description;

    [Header("Economy")]
    [SerializeField, Min(0)]
    private int buildCost = 30;

    [Header("Combat")]
    [SerializeField, Min(0.1f)]
    private float attackRange = 3f;

    [SerializeField, Min(0.05f)]
    private float attackInterval = 1f;

    [SerializeField, Min(1)]
    private int projectileDamage = 10;

    [SerializeField, Min(0.1f)]
    private float projectileSpeed = 8f;

    [Header("Slow")]
    [SerializeField, Range(0.05f, 1f)]
    private float slowMultiplier = 1f;

    [SerializeField, Min(0f)]
    private float slowDuration;

    [Header("Area")]
    [SerializeField] private ImpactMode impactMode = ImpactMode.Direct;

    [SerializeField, Min(0f)]
    private float impactRadius;

    [Header("Zone")]
    [SerializeField, Min(0f)]
    private float zoneRadius;

    [SerializeField, Min(0f)]
    private float zoneDuration;

    [SerializeField, Min(0f)]
    private float zonePulseDelay;

    [Header("Targets")]
    [SerializeField] private int allowedTargets;

    [SerializeField] private TargetPriority aim;

    [Header("Presentation")]
    [SerializeField] private Sprite worldSprite;
    [SerializeField] private Sprite icon;

    public string TowerId => towerId;
    public string DisplayName => displayName;
    public string Description => description;

    public int BuildCost => buildCost;
    public float AttackRange => attackRange;
    public float AttackInterval => attackInterval;
    public int ProjectileDamage => projectileDamage;
    public float ProjectileSpeed => projectileSpeed;

    public float SlowMultiplier => slowMultiplier;
    public float SlowDuration => slowDuration;
    public ImpactMode Strike => impactMode;
    public float ImpactRadius => impactRadius;
    public float ZoneRadius => zoneRadius;
    public float ZoneDuration => zoneDuration;
    public float ZonePulseDelay => zonePulseDelay;
    public TravelMask AllowedTargets => TargetRules.Normalize(allowedTargets);
    public TargetPriority Aim => aim;

    public bool AppliesArea =>
        impactMode == ImpactMode.Area &&
        impactRadius > 0f;

    public bool AppliesZone =>
        zoneRadius > 0f &&
        zoneDuration > 0f &&
        zonePulseDelay > 0f;

    public float ZoneSlow =>
        slowMultiplier > 0f && slowMultiplier < 1f
            ? slowMultiplier
            : 1f;

    public bool AppliesSlow =>
        slowDuration > 0f &&
        slowMultiplier > 0f &&
        slowMultiplier < 1f;

    public Sprite WorldSprite => worldSprite;
    public Sprite Icon => icon;
}