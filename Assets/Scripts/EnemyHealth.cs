using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    private EnemyData enemyData;

    private int currentHealth;
    private bool isDead;
    private bool isInitialized;

    public EnemyData Data => enemyData;

    public int CurrentHealth => currentHealth;

    public int MaxHealth =>
        enemyData != null
            ? enemyData.MaxHealth
            : 0;

    public int Reward =>
        enemyData != null
            ? enemyData.Reward
            : 0;

    public bool IsDead => isDead;
    public bool IsInitialized => isInitialized;

    public event Action<EnemyHealth> OnDeath;
    public event Action<EnemyHealth> OnDamaged;

    public void Initialize(EnemyData data)
    {
        if (data == null)
        {
            Debug.LogError(
                $"{gameObject.name} cannot initialize " +
                "EnemyHealth without EnemyData.",
                this
            );

            return;
        }

        enemyData = data;
        currentHealth = enemyData.MaxHealth;
        isDead = false;
        isInitialized = true;
    }

    public void TakeDamage(int damage)
    {
        if (!isInitialized)
        {
            Debug.LogWarning(
                $"{gameObject.name} received damage before " +
                "EnemyHealth was initialized.",
                this
            );

            return;
        }

        if (isDead)
        {
            return;
        }

        if (damage <= 0)
        {
            return;
        }

        currentHealth -= damage;

        if (currentHealth < 0)
        {
            currentHealth = 0;
        }

        Debug.Log(
            $"{gameObject.name} received {damage} damage. " +
            $"Health: {currentHealth}/{MaxHealth}"
        );

        OnDamaged?.Invoke(this);

        if (currentHealth == 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        Debug.Log($"{gameObject.name} died.");

        OnDeath?.Invoke(this);
    }
}