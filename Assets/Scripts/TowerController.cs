using UnityEngine;

public class TowerController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private TowerData towerData;

    [Header("Components")]
    [SerializeField] private TowerTargeting towerTargeting;
    [SerializeField] private TowerAttack towerAttack;

    private bool isInitialized;

    public TowerData Data => towerData;

    public int BuildCost =>
        towerData != null
            ? towerData.BuildCost
            : 0;

    public bool HasValidConfiguration =>
        towerData != null;

    public bool IsInitialized => isInitialized;

    public void Initialize(
        ProjectileManager projectileManager)
    {
        if (towerData == null)
        {
            Debug.LogError(
                $"{gameObject.name} has no TowerData assigned.",
                this
            );

            return;
        }

        if (towerTargeting == null)
        {
            Debug.LogError(
                $"{gameObject.name} requires a " +
                "TowerTargeting reference.",
                this
            );

            return;
        }

        if (towerAttack == null)
        {
            Debug.LogError(
                $"{gameObject.name} requires a " +
                "TowerAttack reference.",
                this
            );

            return;
        }

        if (projectileManager == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "without a ProjectileManager.",
                this
            );

            return;
        }

        towerTargeting.Initialize(towerData);

        towerAttack.Initialize(
            projectileManager,
            towerData
        );

        isInitialized = true;
    }

    private void OnValidate()
    {
        if (towerData == null)
        {
            Debug.LogWarning(
                $"{gameObject.name} has no TowerData assigned.",
                this
            );
        }
    }
}