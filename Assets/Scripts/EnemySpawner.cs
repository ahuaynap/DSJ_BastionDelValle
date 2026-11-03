using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemyMovement enemyPrefab;
    [SerializeField] private EnemyPath enemyPath;
    [SerializeField] private ChannelNavigator navigator;
    [SerializeField] private BastionHealth bastion;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Transform enemyContainer;

    public string LastFailure { get; private set; }

    public string RouteBlock()
    {
        return RouteBlock(null);
    }

    public string RouteBlock(EnemyMovement prefab)
    {
        if (prefab != null &&
            prefab.Data != null &&
            prefab.Data.Travel == EnemyTravel.Air)
        {
            AirLane lane = GetComponent<AirLane>();
            return lane != null && lane.IsValid
                ? null
                : "El acceso no tiene corredor aéreo.";
        }

        if (enemyPath != null)
        {
            return enemyPath.Count > 0
                ? null
                : "La ruta de waypoints está vacía.";
        }

        if (navigator == null)
        {
            return "El acceso no tiene navegador de confluencia.";
        }

        return navigator.RouteBlock(transform.position);
    }

    public bool HasConnectedRoute()
    {
        return RouteBlock() == null;
    }

    public EnemyMovement SpawnEnemy()
    {
        return SpawnEnemy(null);
    }

    public EnemyMovement SpawnEnemy(EnemyMovement prefabOverride)
    {
        return SpawnEnemy(prefabOverride, 0);
    }

    public EnemyMovement SpawnEnemy(EnemyMovement prefabOverride, int groupId)
    {
        LastFailure = null;
        if (gameManager == null)
        {
            LastFailure = gameObject.name + " no tiene GameManager.";
            Debug.LogError(LastFailure, this);
            return null;
        }

        if (!gameManager.IsPlaying)
        {
            return null;
        }

        EnemyMovement source = prefabOverride != null
            ? prefabOverride
            : enemyPrefab;

        if (source == null)
        {
            LastFailure = gameObject.name + " no tiene prefab de enemigo.";
            Debug.LogError(LastFailure, this);
            return null;
        }

        EnemyMovement enemy =
            Instantiate(
                source,
                transform.position,
                Quaternion.identity,
                enemyContainer
            );
        enemy.gameObject.SetActive(true);

        if (source.Data != null && source.Data.Travel == EnemyTravel.Air)
        {
            AirLane lane = GetComponent<AirLane>();

            if (lane == null || !lane.IsValid || navigator == null)
            {
                LastFailure = lane == null || !lane.IsValid
                    ? "El acceso no tiene corredor aéreo."
                    : "El acceso aéreo no tiene navegador.";
                Debug.LogError(LastFailure, this);
                Destroy(enemy.gameObject);
                return null;
            }

            EnemyMovement flying = navigator.AcceptAir(
                enemy,
                lane.Copy(),
                bastion,
                gameManager,
                groupId
            );
            LastFailure = navigator.LastFailure;
            return flying;
        }

        if (enemyPath != null)
        {
            enemy.Initialize(
                enemyPath,
                bastion,
                gameManager
            );

            return enemy;
        }

        if (navigator != null)
        {
            EnemyMovement launched = navigator.Launch(
                enemy,
                bastion,
                gameManager,
                groupId
            );
            LastFailure = navigator.LastFailure;
            return launched;
        }

        LastFailure =
            "El acceso no tiene ruta ni navegador de confluencia.";
        Debug.LogError(LastFailure, this);
        Destroy(enemy.gameObject);
        return null;
    }
}