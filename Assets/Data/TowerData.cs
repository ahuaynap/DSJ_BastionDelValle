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

    public Sprite WorldSprite => worldSprite;
    public Sprite Icon => icon;
}