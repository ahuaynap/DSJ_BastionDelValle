using System;
using UnityEngine;

[Serializable]
public class WaveConfig
{
    [SerializeField, Min(1)]
    private int enemyCount = 5;

    [SerializeField, Min(0.1f)]
    private float spawnInterval = 1.5f;

    public int EnemyCount => enemyCount;
    public float SpawnInterval => spawnInterval;
}