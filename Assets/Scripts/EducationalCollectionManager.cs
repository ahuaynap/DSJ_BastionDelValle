using System;
using System.Collections.Generic;
using UnityEngine;

public class EducationalCollectionManager :
    MonoBehaviour
{
    [SerializeField]
    private CollectibleSpawner collectibleSpawner;

    private readonly HashSet<string>
        discoveredIds = new();

    public int DiscoveryCount =>
        discoveredIds.Count;

    public event Action<int>
        OnDiscoveryCountChanged;

    public event Action<
        CollectibleData,
        bool
    > OnDiscoveryRegistered;

    private void Start()
    {
        if (collectibleSpawner == null)
        {
            Debug.LogError(
                "EducationalCollectionManager has no " +
                "CollectibleSpawner.",
                this
            );

            return;
        }

        collectibleSpawner.OnCollectibleCaptured +=
            HandleCollectibleCaptured;
    }

    public bool HasDiscovered(
        string collectibleId)
    {
        if (string.IsNullOrWhiteSpace(
                collectibleId))
        {
            return false;
        }

        return discoveredIds.Contains(
            collectibleId
        );
    }

    private void HandleCollectibleCaptured(
        CollectibleData data)
    {
        if (data == null)
        {
            return;
        }

        bool isNewDiscovery =
            discoveredIds.Add(
                data.CollectibleId
            );

        if (isNewDiscovery)
        {
            OnDiscoveryCountChanged?.Invoke(
                DiscoveryCount
            );

            Debug.Log(
                $"New discovery registered: " +
                $"{data.DisplayName}. " +
                $"Total: {DiscoveryCount}"
            );
        }
        else
        {
            Debug.Log(
                $"Discovery already registered: " +
                $"{data.DisplayName}."
            );
        }

        OnDiscoveryRegistered?.Invoke(
            data,
            isNewDiscovery
        );
    }

    private void OnDestroy()
    {
        if (collectibleSpawner != null)
        {
            collectibleSpawner.OnCollectibleCaptured -=
                HandleCollectibleCaptured;
        }
    }
}