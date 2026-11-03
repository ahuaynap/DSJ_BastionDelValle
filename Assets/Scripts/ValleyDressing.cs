using UnityEngine;

public class ValleyDressing : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Semilla fija de la distribución.")]
    private int seed = 19;

    [SerializeField]
    [Min(0)]
    [Tooltip("Máximo de piedras y matas. Si no caben, se colocan menos.")]
    private int elementLimit = 24;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Margen extra alrededor del corredor del camino, incluido su borde.")]
    private float pathMargin = 0.32f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Margen alrededor de parcelas, torres, corazón y memorias.")]
    private float actorMargin = 0.28f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Radio libre alrededor del punto de aparición.")]
    private float spawnClearance = 1.05f;

    [SerializeField]
    [Min(0.05f)]
    [Tooltip("Franja del borde del encuadre donde se concentran las piedras.")]
    private float edgeBand = 1.2f;

    [SerializeField]
    [Min(0.05f)]
    [Tooltip("Radio de los grupos de piedras y de vegetación.")]
    private float clusterRadius = 0.62f;

    [SerializeField]
    [Min(0.05f)]
    [Tooltip("Escala uniforme mínima.")]
    private float minScale = 0.86f;

    [SerializeField]
    [Min(0.05f)]
    [Tooltip("Escala uniforme máxima.")]
    private float maxScale = 1.14f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Separación mínima entre piezas.")]
    private float minSpacing = 0.5f;

    [HideInInspector]
    [SerializeField]
    private bool generated;

    [HideInInspector]
    [SerializeField]
    private int placedCount;

    public int Seed => seed;

    public int ElementLimit => Mathf.Max(0, elementLimit);

    public float PathMargin => Mathf.Max(0f, pathMargin);

    public float ActorMargin => Mathf.Max(0f, actorMargin);

    public float SpawnClearance => Mathf.Max(0f, spawnClearance);

    public float EdgeBand => Mathf.Max(0.05f, edgeBand);

    public float ClusterRadius => Mathf.Max(0.05f, clusterRadius);

    public float MinScale => Mathf.Max(0.05f, Mathf.Min(minScale, maxScale));

    public float MaxScale => Mathf.Max(MinScale, maxScale);

    public float MinSpacing => Mathf.Max(0f, minSpacing);

    public bool Generated => generated;

    public int PlacedCount => Mathf.Max(0, placedCount);
}
