using UnityEngine;

public class TowerController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private TowerData towerData;

    [Header("Components")]
    [SerializeField] private TowerTargeting towerTargeting;
    [SerializeField] private TowerAttack towerAttack;

    [Header("Presentation")]
    [SerializeField] private SpriteRenderer worldRenderer;

    private bool isInitialized;
    private bool missingRendererReported;
    private bool synergyListening;
    private SpriteRenderer synergyMark;

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

        ApplyPresentation();

        isInitialized = true;
        ListenSynergy();
        HabitatSession.Active?.Register(this);
        return true;
    }

    private void OnDestroy()
    {
        if (HabitatSession.Active == null)
        {
            return;
        }

        if (synergyListening)
        {
            HabitatSession.Active.SynergiesChanged -= RefreshMark;
        }

        HabitatSession.Active.Unregister(this);
    }

    private void ListenSynergy()
    {
        if (synergyListening || HabitatSession.Active == null)
        {
            return;
        }

        HabitatSession.Active.SynergiesChanged += RefreshMark;
        synergyListening = true;
        EnsureMark();
    }

    private void RefreshMark()
    {
        if (synergyMark == null)
        {
            return;
        }

        synergyMark.enabled = HabitatSession.Active != null &&
            HabitatSession.Active.HasSynergy(this);
    }

    private void EnsureMark()
    {
        if (synergyMark != null)
        {
            return;
        }

        var marker = new GameObject("SynergyMark");
        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = new Vector3(0.42f, 0.42f, 0f);
        marker.transform.localScale = new Vector3(0.22f, 0.22f, 1f);
        synergyMark = marker.AddComponent<SpriteRenderer>();
        synergyMark.sprite = HabitatSession.MarkerSprite();
        synergyMark.color = new Color(0.95f, 0.84f, 0.42f, 0.95f);
        synergyMark.sortingOrder = worldRenderer != null
            ? worldRenderer.sortingOrder + 2
            : 8;
        synergyMark.enabled = false;
    }

    private void ApplyPresentation()
    {
        if (worldRenderer == null)
        {
            if (missingRendererReported)
            {
                return;
            }

            missingRendererReported = true;
            Debug.LogWarning(
                $"{gameObject.name} has no world " +
                "SpriteRenderer. The temporary tower " +
                "graphic stays.",
                this
            );

            return;
        }

        if (towerData.WorldSprite == null)
        {
            return;
        }

        worldRenderer.sprite = towerData.WorldSprite;
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