using UnityEngine;

public class HabitatSettings : ScriptableObject
{
    [SerializeField] private bool configured;
    [SerializeField] private int initialWater = HabitatRules.StartValue;
    [SerializeField] private int initialSoil = HabitatRules.StartValue;
    [SerializeField] private int initialLife = HabitatRules.StartValue;
    [SerializeField] private int minimum = HabitatRules.MinimumValue;
    [SerializeField] private int maximum = HabitatRules.MaximumValue;
    [SerializeField] private int arrivalLoss = HabitatRules.ArrivalDrop;
    [SerializeField] private int recoveryGain = HabitatRules.WaveRecovery;
    [SerializeField] private int threshold = HabitatRules.BenefitThreshold;
    [SerializeField] private float auraDistance = HabitatRules.AuraDistance;
    [SerializeField] private float hasteMultiplier = HabitatRules.Haste;
    [SerializeField] private float reachMultiplier = HabitatRules.Reach;
    [SerializeField] private int soilBonus = HabitatRules.SoilBonus;
    [SerializeField] private string waterTowerId = "tower_water";
    [SerializeField] private string soilTowerId = "tower_roots";
    [SerializeField] private string lifeTowerId = "tower_pollinator";
    [SerializeField] private string guardianTowerId = "tower_basic";
    [SerializeField] private string watcherTowerId = "tower_condor";

    public bool Configured => configured;
    public int InitialWater => initialWater;
    public int InitialSoil => initialSoil;
    public int InitialLife => initialLife;
    public int Minimum => minimum;
    public int Maximum => maximum;
    public int ArrivalLoss => arrivalLoss;
    public int RecoveryGain => recoveryGain;
    public int Threshold => threshold;
    public float AuraDistance => auraDistance;
    public float HasteMultiplier => hasteMultiplier;
    public float ReachMultiplier => reachMultiplier;
    public int SoilBonus => soilBonus;
    public string WaterTowerId => waterTowerId;
    public string SoilTowerId => soilTowerId;
    public string LifeTowerId => lifeTowerId;
    public string GuardianTowerId => guardianTowerId;
    public string WatcherTowerId => watcherTowerId;
}
