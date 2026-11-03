using UnityEngine;

public class LevelVisualSettings : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Ancho del camino en unidades de mundo. El valor inicial es 0.45.")]
    private float pathWidth = 0.45f;

    [HideInInspector]
    [SerializeField]
    private bool cameraApplied;

    [HideInInspector]
    [SerializeField]
    private float appliedCameraX;

    [HideInInspector]
    [SerializeField]
    private float appliedCameraY;

    [HideInInspector]
    [SerializeField]
    private float appliedCameraZ;

    [HideInInspector]
    [SerializeField]
    private float appliedOrthographicSize;

    [HideInInspector]
    [SerializeField]
    private bool hudApplied;

    [HideInInspector]
    [SerializeField]
    private bool terrainApplied;

    [HideInInspector]
    [SerializeField]
    private bool projectileSortingApplied;

    [HideInInspector]
    [SerializeField]
    private string appliedPathSignature;

    [HideInInspector]
    [SerializeField]
    private float appliedPathWidth;

    public float PathWidth => pathWidth;

    public bool CameraApplied => cameraApplied;

    public float AppliedCameraX => appliedCameraX;

    public float AppliedCameraY => appliedCameraY;

    public float AppliedCameraZ => appliedCameraZ;

    public float AppliedOrthographicSize => appliedOrthographicSize;

    public bool HudApplied => hudApplied;

    public bool TerrainApplied => terrainApplied;

    public bool ProjectileSortingApplied => projectileSortingApplied;

    public string AppliedPathSignature => appliedPathSignature;

    public float AppliedPathWidth => appliedPathWidth;

    public void RememberCamera(Vector3 position, float orthographicSize)
    {
        cameraApplied = true;
        appliedCameraX = position.x;
        appliedCameraY = position.y;
        appliedCameraZ = position.z;
        appliedOrthographicSize = orthographicSize;
    }

    public void RememberHud()
    {
        hudApplied = true;
    }

    public void RememberTerrain()
    {
        terrainApplied = true;
    }

    public void RememberProjectileSorting()
    {
        projectileSortingApplied = true;
    }

    public void RememberPath(string signature, float width)
    {
        appliedPathSignature = signature;
        appliedPathWidth = width;
    }
}
