using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemyMovement enemyPrefab;
    [SerializeField] private EnemyPath enemyPath;
    [SerializeField] private BastionHealth bastion;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Transform enemyContainer;

    public EnemyMovement SpawnEnemy()
    {
        if (gameManager == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot spawn enemies " +
                "without a GameManager.",
                this
            );

            return null;
        }

        if (!gameManager.IsPlaying)
        {
            return null;
        }

        EnemyMovement enemy =
            Instantiate(
                enemyPrefab,
                transform.position,
                Quaternion.identity,
                enemyContainer
            );

        enemy.Initialize(
            enemyPath,
            bastion,
            gameManager
        );

        return enemy;
    }
}