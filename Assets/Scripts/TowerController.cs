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

    public bool Initialize(
        ProjectileManager projectileManager,
        GameManager manager)
    {
        if (isInitialized)
        {
            return true;
        }

        if (towerData == null)
        {
            Debug.LogError(
                $"{gameObject.name} has no TowerData assigned.",
                this
            );

            return false;
        }

        if (towerTargeting == null)
        {
            Debug.LogError(
                $"{gameObject.name} requires a " +
                "TowerTargeting reference.",
                this
            );

            return false;
        }

        if (towerAttack == null)
        {
            Debug.LogError(
                $"{gameObject.name} requires a " +
                "TowerAttack reference.",
                this
            );

            return false;
        }

        if (projectileManager == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "without a ProjectileManager.",
                this
            );

            return false;
        }

        if (manager == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "without a GameManager.",
                this
            );

            return false;
        }

        if (!projectileManager.Initialize(manager))
        {
            return false;
        }

        if (!towerTargeting.Initialize(
                towerData,
                manager))
        {
            return false;
        }

        if (!towerAttack.Initialize(
                projectileManager,
                towerData,
                manager))
        {
            return false;
        }

        isInitialized = true;
        return true;
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