using UnityEngine;

public class ProjectileManager : MonoBehaviour
{
    [SerializeField] private Transform projectileContainer;

    private GameManager gameManager;
    private bool isSubscribed;

    public bool Initialize(GameManager manager)
    {
        if (manager == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "without a GameManager.",
                this
            );

            return false;
        }

        if (projectileContainer == null)
        {
            Debug.LogError(
                $"{gameObject.name} requires a " +
                "projectile container.",
                this
            );

            return false;
        }

        if (isSubscribed)
        {
            return gameManager == manager;
        }

        gameManager = manager;
        gameManager.OnGameStateChanged +=
            HandleGameStateChanged;
        isSubscribed = true;

        return true;
    }

    public Projectile SpawnProjectile(
        Projectile projectilePrefab,
        Vector3 position)
    {
        if (gameManager == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot spawn projectiles " +
                "without a GameManager.",
                this
            );

            return null;
        }

        if (projectileContainer == null)
        {
            Debug.LogError(
                $"{gameObject.name} requires a " +
                "projectile container.",
                this
            );

            return null;
        }

        if (!gameManager.IsPlaying)
        {
            return null;
        }

        if (projectilePrefab == null)
        {
            return null;
        }

        return Instantiate(
            projectilePrefab,
            position,
            Quaternion.identity,
            projectileContainer
        );
    }

    private void HandleGameStateChanged(
        GameState state)
    {
        if (state == GameState.Playing)
        {
            return;
        }

        WithdrawActiveProjectiles();
    }

    private void WithdrawActiveProjectiles()
    {
        if (projectileContainer == null)
        {
            return;
        }

        for (
            int i = projectileContainer.childCount - 1;
            i >= 0;
            i--)
        {
            Projectile projectile =
                projectileContainer
                    .GetChild(i)
                    .GetComponent<Projectile>();

            if (projectile == null)
            {
                continue;
            }

            projectile.WithdrawWithoutDamage();
        }
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -=
                HandleGameStateChanged;
        }
    }
}
