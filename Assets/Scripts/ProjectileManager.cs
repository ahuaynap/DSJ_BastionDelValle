using UnityEngine;

public class ProjectileManager : MonoBehaviour
{
    [SerializeField] private Transform projectileContainer;

    public Projectile SpawnProjectile(
        Projectile projectilePrefab,
        Vector3 position)
    {
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
}