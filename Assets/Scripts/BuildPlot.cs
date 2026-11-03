using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
    [SerializeField] private BuildSelection buildSelection;

    private TowerController currentTower;

    public event Action<TowerController> OnTowerBuilt;

    private bool isBuilding;
    private bool missingPauseManagerReported;
    private static readonly List<BuildPlot> activePlots = new List<BuildPlot>();
    private static int handledFrame = -1;

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
        FitCollider();
    }

    private void OnEnable()
    {
        if (!activePlots.Contains(this))
        {
            activePlots.Add(this);
        }

        FitCollider();
    }

    private void OnDisable()
    {
        activePlots.Remove(this);
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (handledFrame == Time.frameCount)
        {
            return;
        }

        handledFrame = Time.frameCount;
        BuildPlot plot = PlotUnder(mouse.position.ReadValue());

        if (plot != null)
        {
            plot.TryBuild();
        }
    }

    private static BuildPlot PlotUnder(Vector2 screen)
    {
        Camera camera = Camera.main;

        if (camera == null)
        {
            return null;
        }

        Vector3 world = camera.ScreenToWorldPoint(screen);
        var point = new Vector2(world.x, world.y);
        Collider2D[] hits = Physics2D.OverlapPointAll(point);
        BuildPlot best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            BuildPlot plot = hits[i] != null
                ? hits[i].GetComponent<BuildPlot>()
                : null;

            if (plot == null || !activePlots.Contains(plot))
            {
                continue;
            }

            Vector3 spot = plot.BuildSpot();
            float dx = spot.x - point.x;
            float dy = spot.y - point.y;
            float distance = dx * dx + dy * dy;

            if (distance < bestDistance)
            {
                best = plot;
                bestDistance = distance;
            }
        }

        return best;
    }

    private Vector3 BuildSpot()
    {
        if (plotRenderer != null)
        {
            return plotRenderer.bounds.center;
        }

        return transform.position;
    }

    private void FitCollider()
    {
        BoxCollider2D box = plotCollider as BoxCollider2D;

        if (box == null || plotRenderer == null || plotRenderer.sprite == null)
        {
            return;
        }

        Bounds bounds = plotRenderer.bounds;
        float diameter = Mathf.Max(bounds.size.x, bounds.size.y);

        if (diameter > 2.2f)
        {
            diameter = 1.6f;
        }

        if (diameter < 0.8f)
        {
            diameter = 0.8f;
        }

        Vector3 local = transform.InverseTransformPoint(bounds.center);
        float scale = Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y));

        if (scale < 0.01f)
        {
            scale = 0.01f;
        }

        box.offset = new Vector2(local.x, local.y);
        float size = diameter / scale;
        box.size = new Vector2(size, size);
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

        TowerController chosen = ChosenDefense();

        if (chosen == null)
        {
            Debug.LogError(
                $"{gameObject.name} has no tower prefab.",
                this
            );

            return;
        }

        if (!chosen.HasValidConfiguration)
        {
            Debug.LogError(
                $"{chosen.gameObject.name} has no " +
                "valid TowerData configuration.",
                chosen
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

        int towerCost = chosen.BuildCost;

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

        BuildTower(chosen, towerCost);
    }

    private TowerController ChosenDefense()
    {
        if (buildSelection != null && buildSelection.Selected != null)
        {
            return buildSelection.Selected;
        }

        return towerPrefab;
    }

    private void BuildTower(TowerController chosen, int towerCost)
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

            Vector3 spot = BuildSpot();
            tower = Instantiate(
                chosen,
                spot,
                Quaternion.identity,
                towerContainer
            );
            tower.transform.position = spot;

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

        TowerAppearance appearance =
            tower.GetComponent<TowerAppearance>();

        if (appearance != null)
        {
            appearance.Play();
        }

        OnTowerBuilt?.Invoke(tower);
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