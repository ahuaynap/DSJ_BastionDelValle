using UnityEngine;

public class BuildPlot : MonoBehaviour
{
    [SerializeField] private TowerController towerPrefab;
    [SerializeField] private Transform towerContainer;
    [SerializeField] private ProjectileManager projectileManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SpriteRenderer plotRenderer;
    [SerializeField] private Collider2D plotCollider;
    [SerializeField] private EconomyManager economyManager;

    private TowerController currentTower;

    public bool IsOccupied =>
        currentTower != null;

    private void OnMouseDown()
    {
        TryBuild();
    }

    private void TryBuild()
    {
        if (gameManager == null ||
            !gameManager.IsPlaying)
        {
            return;
        }

        if (IsOccupied)
        {
            Debug.Log(
                $"{gameObject.name} is already occupied."
            );

            return;
        }

        if (towerPrefab == null)
        {
            Debug.LogError(
                $"{gameObject.name} has no tower prefab.",
                this
            );

            return;
        }

        if (!towerPrefab.HasValidConfiguration)
        {
            Debug.LogError(
                $"{towerPrefab.gameObject.name} has no " +
                "valid TowerData configuration.",
                towerPrefab
            );

            return;
        }

        if (economyManager == null)
        {
            Debug.LogError(
                $"{gameObject.name} has no EconomyManager.",
                this
            );

            return;
        }

        if (projectileManager == null)
        {
            Debug.LogError(
                $"{gameObject.name} has no ProjectileManager.",
                this
            );

            return;
        }

        int towerCost =
            towerPrefab.BuildCost;

        if (!economyManager.TrySpend(towerCost))
        {
            return;
        }

        BuildTower();
    }

    private void BuildTower()
    {
        TowerController tower =
            Instantiate(
                towerPrefab,
                transform.position,
                Quaternion.identity,
                towerContainer
            );

        if (tower == null)
        {
            Debug.LogError(
                $"Could not build tower on {gameObject.name}.",
                this
            );

            return;
        }

        tower.Initialize(projectileManager);

        currentTower = tower;

        if (plotRenderer != null)
        {
            plotRenderer.enabled = false;
        }

        if (plotCollider != null)
        {
            plotCollider.enabled = false;
        }

        Debug.Log(
            $"{tower.Data.DisplayName} built on " +
            $"{gameObject.name}. " +
            $"Cost: {tower.BuildCost}"
        );
    }
}