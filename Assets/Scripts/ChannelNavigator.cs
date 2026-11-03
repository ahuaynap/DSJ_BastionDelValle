using System;
using System.Collections.Generic;
using UnityEngine;

public class ChannelNavigator : MonoBehaviour
{
    [SerializeField] private ConfluenceLayout layout;
    [SerializeField] private PassageMarker marker;
    [SerializeField] private LocalMotionSettings motion;

    private NavigationGrid grid;
    private bool passageOpen = true;
    private int version;
    private readonly List<EnemyMovement> agents = new();
    private bool reportedMissingLayout;
    private int nextAppearance = 1;

    public int Version => version;
    public bool PassageOpen => passageOpen;
    public string LastFailure { get; private set; }
    public LocalMotionSettings Motion => motion;
    public IReadOnlyList<EnemyMovement> ActiveAgents => agents;

    public string PassageLabel =>
        passageOpen ? "Paso abierto" : "Paso cerrado";

    public event Action PassageChanged;

    public static ChannelNavigator Current { get; private set; }

    private void Awake()
    {
        RebuildGrid();
        if (marker != null)
        {
            marker.Show(passageOpen);
        }
    }

    private void OnEnable()
    {
        Current = this;
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    public EnemyMovement Launch(
        EnemyMovement enemy,
        BastionHealth bastion,
        GameManager manager,
        int groupId = 0)
    {
        LastFailure = null;

        if (enemy == null)
        {
            return null;
        }

        if (!EnsureGrid())
        {
            LastFailure = "La confluencia no tiene cuadrícula asignada.";
            Destroy(enemy.gameObject);
            return null;
        }

        if (!layout.TryCell(enemy.transform.position, out Vector2Int cell))
        {
            LastFailure = "La aparición no cae en una celda de la confluencia.";
            Debug.LogError(LastFailure, this);
            Destroy(enemy.gameObject);
            return null;
        }

        GridPathResult result = GridPathfinder.Find(
            grid,
            cell.x,
            cell.y,
            layout.Goal.x,
            layout.Goal.y);

        if (result.Status != GridPathStatus.Found)
        {
            LastFailure = result.Detail;
            Debug.LogError(LastFailure, this);
            Destroy(enemy.gameObject);
            return null;
        }

        enemy.BeginAlongRoute(
            BuildRoute(enemy.transform.position, result),
            bastion,
            manager,
            this,
            version);

        if (!enemy.IsReady)
        {
            LastFailure = "El enemigo no quedó listo para recorrer la ruta.";
            Destroy(enemy.gameObject);
            return null;
        }

        enemy.AssignAppearance(nextAppearance++);
        enemy.AssignGroup(groupId);
        agents.Add(enemy);
        return enemy;
    }

    public EnemyMovement AcceptAir(
        EnemyMovement enemy,
        Vector3[] corridor,
        BastionHealth bastion,
        GameManager manager,
        int groupId = 0)
    {
        LastFailure = null;

        if (enemy == null)
        {
            return null;
        }

        if (corridor == null || corridor.Length < 2)
        {
            LastFailure = "El corredor aéreo no tiene un trazado utilizable.";
            Destroy(enemy.gameObject);
            return null;
        }

        enemy.BeginAlongRoute(
            corridor,
            bastion,
            manager,
            this,
            version,
            true);

        if (!enemy.IsReady)
        {
            LastFailure = "La polilla no quedó lista para el corredor aéreo.";
            Destroy(enemy.gameObject);
            return null;
        }

        enemy.AssignAppearance(nextAppearance++);
        enemy.AssignGroup(groupId);
        agents.Add(enemy);
        return enemy;
    }

    public bool AllowsDisplacement(Vector3 from, Vector3 to, float radius)
    {
        if (grid == null || layout == null)
        {
            return false;
        }

        return CorridorMotion.SegmentAllowed(
            grid,
            layout.Origin.x,
            layout.Origin.y,
            layout.CellSize,
            from.x,
            from.y,
            to.x,
            to.y,
            radius);
    }

    public NavigationGrid Navigation => grid;
    public float OriginX => layout == null ? 0f : layout.Origin.x;
    public float OriginY => layout == null ? 0f : layout.Origin.y;
    public float CellSize => layout == null ? 1f : layout.CellSize;

    public string RouteBlock(Vector3 worldPosition)
    {
        if (!EnsureGrid())
        {
            return "La confluencia no tiene cuadrícula asignada.";
        }

        if (!layout.TryCell(worldPosition, out Vector2Int cell))
        {
            return "La aparición no cae en una celda de la confluencia.";
        }

        GridPathResult result = GridPathfinder.Find(
            grid,
            cell.x,
            cell.y,
            layout.Goal.x,
            layout.Goal.y);

        if (result.Status == GridPathStatus.Found)
        {
            return null;
        }

        return result.Detail;
    }

    public bool HasRouteFrom(Vector3 worldPosition)
    {
        return RouteBlock(worldPosition) == null;
    }

    public void Release(EnemyMovement enemy)
    {
        if (enemy == null)
        {
            return;
        }

        agents.Remove(enemy);
    }

    public bool TryToggle(
        GameManager game,
        PauseManager pause,
        out string detail)
    {
        if (pause == null || pause.IsPaused)
        {
            detail = "La navegación no cambia durante la pausa o una consulta.";
            return false;
        }

        if (game == null || !game.IsPlaying)
        {
            detail = "La navegación no cambia cuando la partida ya terminó.";
            return false;
        }

        if (!EnsureGrid())
        {
            detail = "No hay cuadrícula de confluencia.";
            return false;
        }

        bool closing = passageOpen;
        var origins = new List<GridPoint>
        {
            new GridPoint(layout.TopSpawn.x, layout.TopSpawn.y),
            new GridPoint(layout.BottomSpawn.x, layout.BottomSpawn.y)
        };

        bool occupied = false;

        for (int i = agents.Count - 1; i >= 0; i--)
        {
            EnemyMovement agent = agents[i];

            if (agent == null)
            {
                agents.RemoveAt(i);
                continue;
            }

            if (!layout.TryCell(agent.transform.position, out Vector2Int cell))
            {
                continue;
            }

            origins.Add(new GridPoint(cell.x, cell.y));

            if (closing &&
                cell.x == layout.Passage.x &&
                cell.y == layout.Passage.y)
            {
                occupied = true;
            }
        }

        PassageVerdict verdict = PassageChangeRules.Evaluate(
            grid,
            closing,
            layout.Passage.x,
            layout.Passage.y,
            origins,
            layout.Goal.x,
            layout.Goal.y,
            occupied);

        if (!verdict.Accepted)
        {
            detail = verdict.Detail;
            return false;
        }

        passageOpen = !passageOpen;
        RebuildGrid();
        version++;
        RepathAgents();

        if (marker != null)
        {
            marker.Show(passageOpen);
        }

        PassageChanged?.Invoke();
        detail = PassageLabel;
        return true;
    }

    private void RepathAgents()
    {
        for (int i = agents.Count - 1; i >= 0; i--)
        {
            EnemyMovement agent = agents[i];

            if (agent == null)
            {
                agents.RemoveAt(i);
                continue;
            }

            if (agent.IsAir)
            {
                continue;
            }

            if (!layout.TryCell(agent.transform.position, out Vector2Int cell))
            {
                continue;
            }

            GridPathResult result = GridPathfinder.Find(
                grid,
                cell.x,
                cell.y,
                layout.Goal.x,
                layout.Goal.y);

            if (result.Status != GridPathStatus.Found)
            {
                continue;
            }

            agent.Retarget(
                BuildRoute(agent.transform.position, result),
                version);
        }
    }

    private bool EnsureGrid()
    {
        if (grid != null)
        {
            return true;
        }

        return RebuildGrid();
    }

    private bool RebuildGrid()
    {
        RestoreLayout();

        if (layout == null || layout.Width <= 0 || layout.Height <= 0)
        {
            grid = null;

            if (!reportedMissingLayout)
            {
                reportedMissingLayout = true;
                Debug.LogError(
                    "ChannelNavigator no tiene ConfluenceLayout. " +
                    "La aparición se cancela y la oleada no continúa.",
                    this
                );
            }

            return false;
        }

        grid = layout.CreateGrid(passageOpen);
        return true;
    }

    private void RestoreLayout()
    {
        if (layout != null)
        {
            return;
        }

#if UNITY_EDITOR
        layout = UnityEditor.AssetDatabase.LoadAssetAtPath<ConfluenceLayout>(
            "Assets/Data/Maps/ConfluenceLayout.asset"
        );
#endif
    }

    private Vector3[] BuildRoute(Vector3 position, GridPathResult result)
    {
        List<RoutePoint> raw = RouteContinuity.FromCurrentPosition(
            position.x,
            position.y,
            result.Steps,
            (x, y) =>
            {
                Vector3 center = layout.CenterOf(x, y);
                return new RoutePoint(center.x, center.y);
            });

        var points = new Vector3[raw.Count];

        for (int i = 0; i < raw.Count; i++)
        {
            points[i] = new Vector3(raw[i].X, raw[i].Y, position.z);
        }

        return points;
    }
}
