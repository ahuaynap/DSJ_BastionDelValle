public enum EnemyTravel
{
    Ground = 0,
    Air = 1
}

public static class GroundRules
{
    public static bool AcceptsZone(EnemyTravel travel)
    {
        return travel == EnemyTravel.Ground;
    }
}
