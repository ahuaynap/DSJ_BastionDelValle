public enum TargetPriority
{
    Closest = 0,
    AirThenGround = 1
}

public enum TravelMask
{
    Ground = 1,
    Air = 2,
    Both = 3
}

public struct AimCandidate
{
    public float X;
    public float Y;
    public EnemyTravel Travel;
}

public static class TargetRules
{
    public static TravelMask Normalize(int stored)
    {
        if (stored == 0)
        {
            return TravelMask.Ground;
        }

        int bits = stored & (int)TravelMask.Both;
        return bits == 0 ? TravelMask.Ground : (TravelMask)bits;
    }

    public static bool Allows(TravelMask allowed, EnemyTravel travel)
    {
        TravelMask bit = travel == EnemyTravel.Air
            ? TravelMask.Air
            : TravelMask.Ground;
        return (Normalize((int)allowed) & bit) != 0;
    }

    public static bool SharesSeparation(EnemyTravel self, EnemyTravel other)
    {
        return self == other;
    }

    public static int Pick(
        AimCandidate[] candidates,
        int count,
        float originX,
        float originY,
        float range,
        TravelMask allowed,
        TargetPriority priority)
    {
        if (candidates == null || count <= 0 || range < 0f)
        {
            return -1;
        }

        int limit = count < candidates.Length ? count : candidates.Length;
        float rangeSq = range * range;
        int air = -1;
        int ground = -1;
        float airDistance = float.MaxValue;
        float groundDistance = float.MaxValue;

        for (int i = 0; i < limit; i++)
        {
            if (!Allows(allowed, candidates[i].Travel))
            {
                continue;
            }

            float dx = candidates[i].X - originX;
            float dy = candidates[i].Y - originY;
            float distance = dx * dx + dy * dy;

            if (distance > rangeSq)
            {
                continue;
            }

            if (candidates[i].Travel == EnemyTravel.Air)
            {
                if (distance < airDistance)
                {
                    airDistance = distance;
                    air = i;
                }
            }
            else if (distance < groundDistance)
            {
                groundDistance = distance;
                ground = i;
            }
        }

        if (priority == TargetPriority.AirThenGround && air >= 0)
        {
            return air;
        }

        if (air < 0)
        {
            return ground;
        }

        if (ground < 0)
        {
            return air;
        }

        return airDistance <= groundDistance ? air : ground;
    }
}
