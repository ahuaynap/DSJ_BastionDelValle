using UnityEngine;
using System;

public class BastionHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private GameManager gameManager;

    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private bool isDestroyed;

    public event Action<int, int> OnHealthChanged;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (isDestroyed)
        {
            return;
        }

        if (damage <= 0)
        {
            return;
        }

        currentHealth -= damage;
        OnHealthChanged?.Invoke(
            currentHealth,
            maxHealth
        );

        if (currentHealth < 0)
        {
            currentHealth = 0;
        }

        Debug.Log(
            $"Bastion received {damage} damage. " +
            $"Health: {currentHealth}/{maxHealth}"
        );

        if (currentHealth == 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDestroyed)
        {
            return;
        }

        isDestroyed = true;

        Debug.Log("BASTION DESTROYED");

        if (gameManager != null)
        {
            gameManager.SetDefeat();
        }
    }
}