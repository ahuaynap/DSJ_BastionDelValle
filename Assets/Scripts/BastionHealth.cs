using UnityEngine;
using System;

public class BastionHealth : MonoBehaviour
{
    private const int MinimumHealth = 1;

    [SerializeField] private int maxHealth = 100;
    [SerializeField] private GameManager gameManager;

    private int currentHealth;

    private bool isDestroyed;
    private bool canResolveDamage;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    public event Action<int, int> OnHealthChanged;

    private void Awake()
    {
        ConfigureHealth();
        ConfigureDamageResolution();
    }

    public void TakeDamage(int damage)
    {
        if (isDestroyed || !canResolveDamage)
        {
            return;
        }

        if (!gameManager.IsPlaying)
        {
            return;
        }

        if (damage <= 0)
        {
            return;
        }

        int updatedHealth = damage >= currentHealth
            ? 0
            : currentHealth - damage;

        bool reachedZero = updatedHealth == 0;

        if (reachedZero)
        {
            isDestroyed = true;
        }

        currentHealth = updatedHealth;

        OnHealthChanged?.Invoke(
            currentHealth,
            maxHealth
        );

        Debug.Log(
            $"Bastion received {damage} damage. " +
            $"Health: {currentHealth}/{maxHealth}"
        );

        if (reachedZero)
        {
            ResolveDestruction();
        }
    }

    private void ConfigureHealth()
    {
        if (maxHealth < MinimumHealth)
        {
            Debug.LogError(
                $"{gameObject.name} has invalid max health " +
                $"{maxHealth}. Using {MinimumHealth}.",
                this
            );

            maxHealth = MinimumHealth;
        }

        currentHealth = maxHealth;
    }

    private void ConfigureDamageResolution()
    {
        if (gameManager != null)
        {
            canResolveDamage = true;
            return;
        }

        canResolveDamage = false;

        Debug.LogError(
            $"{gameObject.name} has no GameManager. " +
            "Bastion damage is disabled.",
            this
        );
    }

    private void ResolveDestruction()
    {
        Debug.Log("BASTION DESTROYED");

        gameManager.SetDefeat();
    }

    private void OnValidate()
    {
        if (maxHealth < MinimumHealth)
        {
            maxHealth = MinimumHealth;
        }
    }
}
