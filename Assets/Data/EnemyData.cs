using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyData_New",
    menuName = "Bastion del Valle/Enemies/Enemy Data"
)]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string enemyId = "enemy_basic";
    [SerializeField] private string displayName = "Basic Enemy";

    [TextArea(2, 4)]
    [SerializeField] private string description;

    [Header("Gameplay")]
    [SerializeField, Min(1)]
    private int maxHealth = 30;

    [SerializeField, Min(0.1f)]
    private float movementSpeed = 2f;

    [SerializeField, Min(0)]
    private int reward = 10;

    [SerializeField, Min(1)]
    private int goalDamage = 10;

    [SerializeField, Min(0f)]
    private float bodyRadius;

    [SerializeField] private EnemyTravel travel = EnemyTravel.Ground;

    [Header("Presentation")]
    [SerializeField] private Sprite worldSprite;
    [SerializeField] private Sprite icon;

    public string EnemyId => enemyId;
    public string DisplayName => displayName;
    public string Description => description;

    public int MaxHealth => maxHealth;
    public float MovementSpeed => movementSpeed;
    public int Reward => reward;
    public int GoalDamage => goalDamage;
    public float BodyRadius => bodyRadius;
    public EnemyTravel Travel => travel;
    public bool IsGround => GroundRules.AcceptsZone(travel);

    public Sprite WorldSprite => worldSprite;
    public Sprite Icon => icon;
}