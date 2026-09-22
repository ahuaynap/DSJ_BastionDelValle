using UnityEngine;

[CreateAssetMenu(
    fileName = "CollectibleData_New",
    menuName =
        "Bastion del Valle/Education/Collectible Data"
)]
public class CollectibleData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string collectibleId;

    [SerializeField]
    private string displayName;

    [SerializeField]
    private EducationalCategory category;

    [Header("Educational Content")]
    [SerializeField, TextArea(2, 4)]
    private string shortDescription;

    [SerializeField, TextArea(4, 10)]
    private string educationalContent;

    [Header("Presentation")]
    [SerializeField]
    private Sprite worldSprite;

    [SerializeField]
    private Sprite infographicSprite;

    public string CollectibleId =>
        collectibleId;

    public string DisplayName =>
        displayName;

    public EducationalCategory Category =>
        category;

    public string ShortDescription =>
        shortDescription;

    public string EducationalContent =>
        educationalContent;

    public Sprite WorldSprite =>
        worldSprite;

    public Sprite InfographicSprite =>
        infographicSprite;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(
                collectibleId))
        {
            Debug.LogWarning(
                $"{name} has no Collectible ID.",
                this
            );
        }

        if (string.IsNullOrWhiteSpace(
                displayName))
        {
            Debug.LogWarning(
                $"{name} has no display name.",
                this
            );
        }
    }
}