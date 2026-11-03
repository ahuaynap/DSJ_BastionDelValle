using System;

public static class AccessDraw
{
    public static int Next(Random random, int validCount)
    {
        if (random == null || validCount <= 0)
        {
            return -1;
        }

        return random.Next(validCount);
    }

    public static int[] Sequence(int seed, int validCount, int draws)
    {
        var random = new Random(seed);
        var result = new int[Math.Max(0, draws)];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = Next(random, validCount);
        }

        return result;
    }
}
