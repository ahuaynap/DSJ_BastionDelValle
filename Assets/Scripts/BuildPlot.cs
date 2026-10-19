using UnityEngine;

public class BuildPlot : MonoBehaviour
{
    [SerializeField] private TowerController towerPrefab;
    [SerializeField] private Transform towerContainer;
    [SerializeField] private ProjectileManager projectileManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private PauseManager pauseManager;
    [SerializeField] private SpriteRenderer plotRenderer;
    [SerializeField] private Collider2D plotCollider;
    [SerializeField] private EconomyManager economyManager;

    private TowerController currentTower;

    private bool missingPauseManagerReported;

    public bool IsOccupied =>
        currentTower != null;

    private void Awake()
    {
        if (pauseManager == null)
        {
            ReportMissingPauseManager();
        }

        if (projectileManager == null)
        {
            Debug.LogError(
                $"{gameObject.name} has no ProjectileManager.",
                this
            );

            return;
        }

        if (gameManager == null)
        {
            Debug.LogError(
                $"{gameObject.name} has no GameManager.",
                this
            );

            return;
        }

        projectileManager.Initialize(gameManager);
    }

    private void OnMouseDown()
    {
        TryBuild();
    }

    private void TryBuild()
    {
        if (gameManager == null)
        {
            Debug.LogError(
                $"{gameObject.name} has no GameManager.",
                this
            );

            return;
        }

        if (!gameManager.IsPlaying)
        {
            return;
        }

        if (!HasPauseManager())
        {
            return;
        }

        if (!WorldInteractionBlock.AllowsWorldAction(
                pauseManager))
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

        tower.Initialize(
            projectileManager,
            gameManager
        );

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

    private bool HasPauseManager()
    {
        if (pauseManager != null)
        {
            return true;
        }

        ReportMissingPauseManager();
        return false;
    }

    private void ReportMissingPauseManager()
    {
        if (missingPauseManagerReported)
        {
            return;
        }

        missingPauseManagerReported = true;

        Debug.LogError(
            $"{gameObject.name} has no PauseManager. " +
            "Building is disabled.",
            this
        );
    }
}