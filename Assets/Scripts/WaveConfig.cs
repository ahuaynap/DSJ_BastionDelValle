using System;
using UnityEngine;

[Serializable]
public class WaveConfig
{
    [SerializeField, Min(1)]
    private int enemyCount = 5;

    [SerializeField, Min(0.1f)]
    private float spawnInterval = 1.5f;

    [SerializeField]
    private WaveSpawn[] sequence;

    [SerializeField, Min(0f)]
    private float partyInterval = 0.2f;

    public int EntryCount =>
        sequence != null && sequence.Length > 0
            ? sequence.Length
            : enemyCount;

    public int EnemyCount
    {
        get
        {
            if (sequence == null || sequence.Length == 0)
            {
                return enemyCount;
            }

            int total = 0;

            for (int i = 0; i < sequence.Length; i++)
            {
                total += PartyAt(i);
            }

            return total;
        }
    }

    public float SpawnInterval => spawnInterval;

    public float PartyInterval =>
        partyInterval >= 0.02f ? partyInterval : WaveComposition.MemberInterval;

    public EnemyMovement PrefabAt(int index)
    {
        if (sequence == null || index < 0 || index >= sequence.Length)
        {
            return null;
        }

        return sequence[index] == null
            ? null
            : sequence[index].EnemyPrefab;
    }

    public int PartyAt(int index)
    {
        if (sequence == null || index < 0 || index >= sequence.Length)
        {
            return 1;
        }

        int size = sequence[index] == null ? 1 : sequence[index].PartySize;
        return size < 1 ? 1 : size;
    }
}

[Serializable]
public class WaveSpawn
{
    [SerializeField] private EnemyMovement enemyPrefab;

    [SerializeField, Min(1)]
    private int partySize = 1;

    public EnemyMovement EnemyPrefab => enemyPrefab;
    public int PartySize => partySize < 1 ? 1 : partySize;
}