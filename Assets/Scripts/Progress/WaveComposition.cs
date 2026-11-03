public static class WaveComposition
{
    public const int Aphid = 0;
    public const int Locust = 1;
    public const int Swarm = 2;
    public const int Moth = 3;
    public const int SwarmGroupSize = 5;
    public const float MemberInterval = 0.2f;
    public const float SwarmSpeedFactor = 0.9f;
    public const float MothSpeedFactor = 1.2f;

    public static int[] FirstWave()
    {
        return new[]
        {
            Aphid, Aphid, Aphid, Aphid, Locust, Locust
        };
    }

    public static int[] SecondWave()
    {
        return new[]
        {
            Aphid, Aphid, Aphid, Locust, Locust, Locust, Swarm, Swarm, Moth, Moth
        };
    }

    public static float SwarmSpeed(float aphidSpeed)
    {
        return aphidSpeed * SwarmSpeedFactor;
    }

    public static float MothSpeed(float aphidSpeed)
    {
        return aphidSpeed * MothSpeedFactor;
    }

    public static void Count(int[] wave, out int aphids, out int locusts)
    {
        Count(wave, out aphids, out locusts, out _, out _);
    }

    public static void Count(
        int[] wave,
        out int aphids,
        out int locusts,
        out int swarmGroups)
    {
        Count(wave, out aphids, out locusts, out swarmGroups, out _);
    }

    public static void Count(
        int[] wave,
        out int aphids,
        out int locusts,
        out int swarmGroups,
        out int moths)
    {
        aphids = 0;
        locusts = 0;
        swarmGroups = 0;
        moths = 0;

        if (wave == null)
        {
            return;
        }

        for (int i = 0; i < wave.Length; i++)
        {
            if (wave[i] == Aphid)
            {
                aphids++;
            }
            else if (wave[i] == Locust)
            {
                locusts++;
            }
            else if (wave[i] == Swarm)
            {
                swarmGroups++;
            }
            else if (wave[i] == Moth)
            {
                moths++;
            }
        }
    }

    public static bool OpensWithAphids(int[] wave, int count)
    {
        if (wave == null || wave.Length < count)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            if (wave[i] != Aphid)
            {
                return false;
            }
        }

        return true;
    }
}
