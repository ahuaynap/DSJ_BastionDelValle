using UnityEngine;
using System;

public class EconomyManager : MonoBehaviour
{
    [SerializeField] private int startingResources = 100;

    private int currentResources;

    public int CurrentResources => currentResources;

    public event Action<int> OnResourcesChanged;

    private void Awake()
    {
        currentResources = startingResources;

        Debug.Log(
            $"Starting resources: {currentResources}"
        );
    }

    public bool CanAfford(int amount)
    {
        return amount >= 0 &&
               currentResources >= amount;
    }

    public bool TrySpend(int amount)
    {
        if (amount < 0)
        {
            return false;
        }

        if (!CanAfford(amount))
        {
            Debug.Log(
                $"Insufficient resources. " +
                $"Required: {amount} | " +
                $"Available: {currentResources}"
            );

            return false;
        }

        currentResources -= amount;

        OnResourcesChanged?.Invoke(currentResources);

        Debug.Log(
            $"Spent {amount} resources. " +
            $"Remaining: {currentResources}"
        );

        return true;
    }

    public void AddResources(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentResources += amount;

        OnResourcesChanged?.Invoke(currentResources);

        Debug.Log(
            $"Received {amount} resources. " +
            $"Total: {currentResources}"
        );
    }

    public bool Refund(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        currentResources += amount;

        OnResourcesChanged?.Invoke(currentResources);

        Debug.Log(
            $"Refunded {amount} resources. " +
            $"Total: {currentResources}"
        );

        return true;
    }
}