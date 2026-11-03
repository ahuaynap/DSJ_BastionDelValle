using System;
using UnityEngine;

public enum ConfluenceCellKind
{
    Path,
    Bridge,
    Channel
}

[Serializable]
public struct ConfluenceCell
{
    public int x;
    public int y;
    public bool walkable;
    public int cost;
    public ConfluenceCellKind kind;
}

public class ConfluenceLayout : ScriptableObject
{
    [SerializeField, Tooltip("Tamaño de cada celda, en unidades de mundo.")]
    private float cellSize = 1f;

    [SerializeField, Tooltip("Esquina inferior izquierda de la celda (0, 0).")]
    private Vector2 origin;

    [SerializeField] private int width;
    [SerializeField] private int height;

    [SerializeField, Tooltip("Celdas editables. Solo las marcadas como transitables entran en A*.")]
    private ConfluenceCell[] cells;

    [SerializeField] private Vector2Int topSpawn;
    [SerializeField] private Vector2Int bottomSpawn;
    [SerializeField] private Vector2Int goal;
    [SerializeField, Tooltip("Única celda que alterna la demostración de navegación.")]
    private Vector2Int passage;

    [SerializeField, Tooltip("Parcelas fuera de las celdas transitables.")]
    private Vector2Int[] plots;

    [SerializeField, Tooltip("Margen vertical extra para que el HUD no tape a los actores.")]
    private float verticalCameraMargin = 2.4f;

    public float CellSize => cellSize;
    public Vector2 Origin => origin;
    public int Width => width;
    public int Height => height;
    public Vector2Int TopSpawn => topSpawn;
    public Vector2Int BottomSpawn => bottomSpawn;
    public Vector2Int Goal => goal;
    public Vector2Int Passage => passage;
    public Vector2Int[] Plots => plots;
    public float VerticalCameraMargin => verticalCameraMargin;
    public ConfluenceCell[] Cells => cells;

    public Vector3 CenterOf(int x, int y)
    {
        return new Vector3(
            origin.x + (x + 0.5f) * cellSize,
            origin.y + (y + 0.5f) * cellSize,
            0f);
    }

    public bool TryCell(Vector3 world, out Vector2Int cell)
    {
        if (cellSize <= 0f)
        {
            cell = default;
            return false;
        }

        int x = Mathf.FloorToInt((world.x - origin.x) / cellSize);
        int y = Mathf.FloorToInt((world.y - origin.y) / cellSize);
        cell = new Vector2Int(x, y);
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    public NavigationGrid CreateGrid(bool passageOpen)
    {
        int count = Mathf.Max(0, width) * Mathf.Max(0, height);
        bool[] walkable = new bool[count];
        int[] costs = new int[count];

        if (cells != null && width > 0)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                ConfluenceCell cell = cells[i];

                if (cell.x < 0 ||
                    cell.y < 0 ||
                    cell.x >= width ||
                    cell.y >= height)
                {
                    continue;
                }

                int index = cell.y * width + cell.x;
                bool open = cell.walkable;

                if (!passageOpen &&
                    cell.x == passage.x &&
                    cell.y == passage.y)
                {
                    open = false;
                }

                walkable[index] = open;
                costs[index] = cell.cost;
            }
        }

        return new NavigationGrid(width, height, walkable, costs);
    }
}
