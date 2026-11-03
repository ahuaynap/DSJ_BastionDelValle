using UnityEngine;

[System.Serializable]
public class MapLessonWave
{
    [SerializeField] private string threatName;
    [SerializeField] private string advice;
    [SerializeField] private int clearReward;

    public string ThreatName => threatName;
    public string Advice => advice;
    public int ClearReward => clearReward;
}

public class MapLesson : ScriptableObject
{
    [SerializeField] private MapLessonWave[] waves;

    public int Count => waves == null ? 0 : waves.Length;

    public string ThreatAt(int index)
    {
        MapLessonWave wave = At(index);
        return wave == null || wave.ThreatName == null ? string.Empty : wave.ThreatName;
    }

    public string AdviceAt(int index)
    {
        MapLessonWave wave = At(index);
        return wave == null || wave.Advice == null ? string.Empty : wave.Advice;
    }

    public int RewardAt(int index)
    {
        MapLessonWave wave = At(index);
        return wave == null ? 0 : wave.ClearReward;
    }

    private MapLessonWave At(int index)
    {
        if (waves == null || index < 0 || index >= waves.Length)
        {
            return null;
        }

        return waves[index];
    }
}
