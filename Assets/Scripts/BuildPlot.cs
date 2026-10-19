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

    private bool isBuilding;
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

        if (isBuilding)
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

        int towerCost = towerPrefab.BuildCost;

        if (towerCost < 0)
        {
            Debug.LogError(
                $"{gameObject.name} cannot build. " +
                $"Tower cost is invalid: {towerCost}.",
                this
            );

            return;
        }

        if (!economyManager.CanAfford(towerCost))
        {
            Debug.Log(
                $"Insufficient resources. " +
                $"Required: {towerCost} | " +
                $"Available: {economyManager.CurrentResources}"
            );

            return;
        }

        BuildTower(towerCost);
    }

    private void BuildTower(int towerCost)
    {
        isBuilding = true;

        TowerController tower = null;
        int chargedAmount = 0;
        bool succeeded = false;

        try
        {
            if (!economyManager.TrySpend(towerCost))
            {
                return;
            }

            chargedAmount = towerCost;

            tower = Instantiate(
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

            bool initialized = tower.Initialize(
                projectileManager,
                gameManager
            );

            if (!initialized || !tower.IsInitialized)
            {
                Debug.LogError(
                    $"Could not initialize tower on " +
                    $"{gameObject.name}.",
                    this
                );

                return;
            }

            ConfirmBuild(tower);
            succeeded = true;
        }
        finally
        {
            if (!succeeded)
            {
                CancelBuild(tower, chargedAmount);
            }

            isBuilding = false;
        }
    }

    private void ConfirmBuild(TowerController tower)
    {
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

    private void CancelBuild(
        TowerController tower,
        int chargedAmount)
    {
        if (tower != null)
        {
            tower.gameObject.SetActive(false);
            Destroy(tower.gameObject);
        }

        if (chargedAmount > 0)
        {
            economyManager.Refund(chargedAmount);
        }
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