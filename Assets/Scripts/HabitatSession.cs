using System;
using System.Collections.Generic;
using UnityEngine;

public class HabitatSession : MonoBehaviour
{
    private sealed class Record
    {
        public TowerController Tower;
        public float Interval = 1f;
        public float Range = 1f;
    }

    [SerializeField] private HabitatSettings settings;
    [SerializeField] private GameManager gameManager;

    private readonly List<Record> towers = new();
    private readonly HashSet<int> recoveredWaves = new();

    private int water;
    private int soil;
    private int life;
    private bool closed;
    private int batchArrivals;
    private int batchWater;
    private int batchSoil;
    private int batchLife;
    private float batchUntil;
    private bool waterSynergyUsed;
    private bool lifeSynergyUsed;
    private bool soilPaid;
    private bool waterRecovered;
    private bool soilRecovered;
    private bool lifeRecovered;
    private string lastCause = "Umbral 60.";
    private static Sprite markerSprite;

    public static HabitatSession Active { get; private set; }

    public int Water => water;
    public int Soil => soil;
    public int Life => life;
    public string LastCause => lastCause;
    public bool WaterSynergyUsed => waterSynergyUsed;
    public bool LifeSynergyUsed => lifeSynergyUsed;
    public bool SoilPaid => soilPaid;

    public event Action<HabitatChange> Changed;
    public event Action<string> CauseReady;
    public event Action SynergiesChanged;

    private void Awake()
    {
        water = HabitatRules.Restored(Initial(true), Minimum, Maximum);
        soil = HabitatRules.Restored(Initial(false), Minimum, Maximum);
        life = HabitatRules.Restored(InitialLife(), Minimum, Maximum);
    }

    private void OnEnable()
    {
        Active = this;

        if (gameManager != null)
        {
            gameManager.OnGameStateChanged += HandleState;
        }
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= HandleState;
        }

        if (Active == this)
        {
            Active = null;
        }

        towers.Clear();
        SynergiesChanged = null;
    }

    public void NoteArrival()
    {
        if (!CanChange())
        {
            return;
        }

        int loss = HabitatRules.ArrivalLoss(true, false, Loss);
        int waterLoss = -Apply(HabitatKind.Water, -loss, HabitatCause.Arrival);
        int soilLoss = -Apply(HabitatKind.Soil, -loss, HabitatCause.Arrival);
        int lifeLoss = -Apply(HabitatKind.Life, -loss, HabitatCause.Arrival);

        if (waterLoss == 0 && soilLoss == 0 && lifeLoss == 0)
        {
            return;
        }

        bool open = Time.unscaledTime <= batchUntil;
        batchArrivals = HabitatRules.GrowBatch(batchArrivals, open);

        if (!open)
        {
            batchWater = 0;
            batchSoil = 0;
            batchLife = 0;
        }

        batchWater += waterLoss;
        batchSoil += soilLoss;
        batchLife += lifeLoss;
        batchUntil = Time.unscaledTime + HabitatRules.ArrivalWindow;
        Publish(
            HabitatRules.ArrivalLine(
                batchArrivals,
                batchWater,
                batchSoil,
                batchLife));
        Recompute();
    }

    public void RecoverClearedWave(int waveIndex)
    {
        if (!CanChange())
        {
            return;
        }

        bool already = recoveredWaves.Contains(waveIndex);
        int waterGain = ApplyRecovery(HabitatKind.Water, already, Count(WaterId) > 0);
        int soilGain = ApplyRecovery(HabitatKind.Soil, already, Count(SoilId) > 0);
        int lifeGain = ApplyRecovery(HabitatKind.Life, already, Count(LifeId) > 0);
        recoveredWaves.Add(waveIndex);

        if (waterGain > 0)
        {
            waterRecovered = true;
        }

        if (soilGain > 0)
        {
            soilRecovered = true;
        }

        if (lifeGain > 0)
        {
            lifeRecovered = true;
        }

        string line = HabitatRules.RecoveryLine(waterGain, soilGain, lifeGain);

        if (line.Length > 0)
        {
            Publish(line);
        }

        Recompute();
    }

    public int SoilExtra(int baseReward, bool finalWave)
    {
        return HabitatRules.SoilExtra(
            baseReward,
            soil,
            Threshold,
            Bonus,
            finalWave);
    }

    public void NoteSoilPaid()
    {
        soilPaid = true;
    }

    public float IntervalMultiplierFor(TowerController tower)
    {
        Record record = Find(tower);
        return record == null ? 1f : record.Interval;
    }

    public float RangeMultiplierFor(TowerController tower)
    {
        Record record = Find(tower);
        return record == null ? 1f : record.Range;
    }

    public bool HasSynergy(TowerController tower)
    {
        Record record = Find(tower);
        return record != null && (record.Interval < 0.999f || record.Range > 1.001f);
    }

    public string Consultation()
    {
        bool haste = false;
        bool guardianReach = false;
        bool watcherReach = false;

        for (int i = 0; i < towers.Count; i++)
        {
            TowerController tower = towers[i].Tower;

            if (tower == null || tower.Data == null)
            {
                continue;
            }

            string id = tower.Data.TowerId;

            if (towers[i].Interval < 0.999f)
            {
                haste = true;
            }

            if (towers[i].Range > 1.001f && id == GuardianId)
            {
                guardianReach = true;
            }

            if (towers[i].Range > 1.001f && id == WatcherId)
            {
                watcherReach = true;
            }
        }

        return HabitatRules.Consult(haste, guardianReach, watcherReach);
    }

    public string ResultReport()
    {
        return HabitatRules.ResultReport(
            water,
            soil,
            life,
            Maximum,
            waterSynergyUsed,
            lifeSynergyUsed,
            soilPaid,
            waterRecovered,
            soilRecovered,
            lifeRecovered);
    }

    public void Register(TowerController tower)
    {
        if (tower == null || Find(tower) != null)
        {
            return;
        }

        towers.Add(new Record { Tower = tower });
        Recompute();
    }

    public void Unregister(TowerController tower)
    {
        for (int i = towers.Count - 1; i >= 0; i--)
        {
            if (towers[i].Tower == tower)
            {
                towers.RemoveAt(i);
            }
        }

        Recompute();
    }

    public static Sprite MarkerSprite()
    {
        if (markerSprite != null)
        {
            return markerSprite;
        }

        var texture = new Texture2D(8, 8);
        var clear = new Color(0f, 0f, 0f, 0f);
        var ink = Color.white;

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int dx = x < 4 ? 3 - x : x - 4;
                int dy = y < 4 ? 3 - y : y - 4;
                texture.SetPixel(x, y, dx + dy <= 3 ? ink : clear);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Point;
        markerSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 8f, 8f),
            new Vector2(0.5f, 0.5f),
            8f);
        return markerSprite;
    }

    private void HandleState(GameState state)
    {
        if (state != GameState.Playing)
        {
            closed = true;
        }
    }

    private int ApplyRecovery(HabitatKind kind, bool already, bool hasTower)
    {
        int gain = HabitatRules.Recovery(
            true,
            CanChange(),
            false,
            already,
            hasTower,
            Gain);
        return Apply(kind, gain, HabitatCause.WaveRecovery);
    }

    private int Apply(HabitatKind kind, int delta, HabitatCause cause)
    {
        if (!CanChange() || delta == 0)
        {
            return 0;
        }

        int current = Value(kind);

        if (!HabitatRules.TryChange(current, delta, Minimum, Maximum, out int next))
        {
            return 0;
        }

        Set(kind, next);
        Changed?.Invoke(new HabitatChange
        {
            Kind = kind,
            Previous = current,
            Current = next,
            Cause = cause
        });
        return next - current;
    }

    private void Publish(string cause)
    {
        if (string.IsNullOrEmpty(cause))
        {
            return;
        }

        lastCause = cause;
        CauseReady?.Invoke(cause);
    }

    private void Recompute()
    {
        var channelX = new List<float>();
        var channelY = new List<float>();
        var refugeX = new List<float>();
        var refugeY = new List<float>();

        for (int i = 0; i < towers.Count; i++)
        {
            TowerController tower = towers[i].Tower;

            if (tower == null || tower.Data == null)
            {
                continue;
            }

            string id = tower.Data.TowerId;
            Vector3 position = tower.transform.position;

            if (id == WaterId)
            {
                channelX.Add(position.x);
                channelY.Add(position.y);
            }

            if (id == LifeId)
            {
                refugeX.Add(position.x);
                refugeY.Add(position.y);
            }
        }

        float[] channelsX = channelX.ToArray();
        float[] channelsY = channelY.ToArray();
        float[] refugesX = refugeX.ToArray();
        float[] refugesY = refugeY.ToArray();

        for (int i = 0; i < towers.Count; i++)
        {
            TowerController tower = towers[i].Tower;
            string id = tower != null && tower.Data != null
                ? tower.Data.TowerId
                : string.Empty;
            Vector3 position = tower != null
                ? tower.transform.position
                : Vector3.zero;
            bool haste = HabitatRules.AcceptsHaste(id, GuardianId) &&
                HabitatRules.NearAny(
                    position.x,
                    position.y,
                    channelsX,
                    channelsY,
                    channelsX.Length,
                    Distance);
            bool reach = HabitatRules.AcceptsReach(id, GuardianId, WatcherId) &&
                HabitatRules.NearAny(
                    position.x,
                    position.y,
                    refugesX,
                    refugesY,
                    refugesX.Length,
                    Distance);
            towers[i].Interval = HabitatRules.IntervalMultiplier(
                water,
                Threshold,
                haste,
                Haste);
            towers[i].Range = HabitatRules.RangeMultiplier(
                life,
                Threshold,
                reach,
                ReachValue);

            if (towers[i].Interval < 0.999f)
            {
                waterSynergyUsed = true;
            }

            if (towers[i].Range > 1.001f)
            {
                lifeSynergyUsed = true;
            }
        }

        SynergiesChanged?.Invoke();
    }

    private bool CanChange()
    {
        return !closed && (gameManager == null || gameManager.IsPlaying);
    }

    private Record Find(TowerController tower)
    {
        for (int i = 0; i < towers.Count; i++)
        {
            if (towers[i].Tower == tower)
            {
                return towers[i];
            }
        }

        return null;
    }

    private int Count(string towerId)
    {
        int count = 0;

        for (int i = 0; i < towers.Count; i++)
        {
            TowerController tower = towers[i].Tower;

            if (tower != null &&
                tower.Data != null &&
                tower.Data.TowerId == towerId)
            {
                count++;
            }
        }

        return count;
    }

    private int Value(HabitatKind kind)
    {
        if (kind == HabitatKind.Water)
        {
            return water;
        }

        if (kind == HabitatKind.Soil)
        {
            return soil;
        }

        return life;
    }

    private void Set(HabitatKind kind, int value)
    {
        if (kind == HabitatKind.Water)
        {
            water = value;
        }
        else if (kind == HabitatKind.Soil)
        {
            soil = value;
        }
        else
        {
            life = value;
        }
    }

    private int Initial(bool waterValue)
    {
        if (settings == null)
        {
            return HabitatRules.StartValue;
        }

        return waterValue ? settings.InitialWater : settings.InitialSoil;
    }

    private int InitialLife()
    {
        return settings == null ? HabitatRules.StartValue : settings.InitialLife;
    }

    private int Minimum =>
        settings == null ? HabitatRules.MinimumValue : settings.Minimum;

    public int Maximum =>
        settings == null ? HabitatRules.MaximumValue : settings.Maximum;

    private int Loss =>
        settings == null ? HabitatRules.ArrivalDrop : settings.ArrivalLoss;

    private int Gain =>
        settings == null ? HabitatRules.WaveRecovery : settings.RecoveryGain;

    private int Threshold =>
        settings == null ? HabitatRules.BenefitThreshold : settings.Threshold;

    private int Bonus =>
        settings == null ? HabitatRules.SoilBonus : settings.SoilBonus;

    private float Distance =>
        settings == null ? HabitatRules.AuraDistance : settings.AuraDistance;

    private float Haste =>
        settings == null ? HabitatRules.Haste : settings.HasteMultiplier;

    private float ReachValue =>
        settings == null ? HabitatRules.Reach : settings.ReachMultiplier;

    private string WaterId =>
        settings == null || string.IsNullOrEmpty(settings.WaterTowerId)
            ? "tower_water"
            : settings.WaterTowerId;

    private string SoilId =>
        settings == null || string.IsNullOrEmpty(settings.SoilTowerId)
            ? "tower_roots"
            : settings.SoilTowerId;

    private string LifeId =>
        settings == null || string.IsNullOrEmpty(settings.LifeTowerId)
            ? "tower_pollinator"
            : settings.LifeTowerId;

    private string GuardianId =>
        settings == null || string.IsNullOrEmpty(settings.GuardianTowerId)
            ? "tower_basic"
            : settings.GuardianTowerId;

    private string WatcherId =>
        settings == null || string.IsNullOrEmpty(settings.WatcherTowerId)
            ? "tower_condor"
            : settings.WatcherTowerId;
}
