public static class LessonRules
{
    public const int CompletionReward = 20;
    public const float GroupGap = 2f;

    public static int[][] Rosters()
    {
        return new[]
        {
            Repeat(WaveComposition.Aphid, 6),
            Repeat(WaveComposition.Locust, 8),
            Repeat(WaveComposition.Swarm, 3),
            Repeat(WaveComposition.Moth, 6)
        };
    }

    public static float[] Intervals()
    {
        return new[] { 1.8f, 1.5f, GroupGap, 1.8f };
    }

    public static string[] Threats()
    {
        return new[] { "Pulgones", "Langostas", "Enjambre", "Polillas" };
    }

    public static string[] Advice()
    {
        return new[]
        {
            "Cubre ambos accesos con tus guardianes",
            "El agua reduce su velocidad y da tiempo a otras defensas",
            "El Refugio y las Raíces pueden afectar a varios enemigos",
            "Necesitas cobertura aérea: Guardián Semilla o Vigía del Cóndor"
        };
    }

    public static int Individuals(int[] kinds)
    {
        return WaveSchedule.CountIndividuals(
            kinds,
            WaveComposition.SwarmGroupSize);
    }

    public static bool OnlyKind(int[] kinds, int kind)
    {
        if (kinds == null || kinds.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < kinds.Length; i++)
        {
            if (kinds[i] != kind)
            {
                return false;
            }
        }

        return true;
    }

    public static int[] GroupIds(int[] kinds)
    {
        if (kinds == null)
        {
            return new int[0];
        }

        var ids = new int[kinds.Length];
        int next = 1;

        for (int i = 0; i < kinds.Length; i++)
        {
            int party = kinds[i] == WaveComposition.Swarm
                ? WaveComposition.SwarmGroupSize
                : 1;
            ids[i] = party > 1 ? next++ : 0;
        }

        return ids;
    }

    public static int DistinctGroups(int[] ids)
    {
        if (ids == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] <= 0)
            {
                continue;
            }

            bool seen = false;

            for (int earlier = 0; earlier < i; earlier++)
            {
                if (ids[earlier] == ids[i])
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
            {
                count++;
            }
        }

        return count;
    }

    public static int Grant(
        int waveIndex,
        int waveCount,
        int configured,
        bool completed,
        bool sessionActive,
        bool generationFailed)
    {
        if (!completed || !sessionActive || generationFailed || configured <= 0)
        {
            return 0;
        }

        if (waveCount <= 1 || waveIndex < 0 || waveIndex >= waveCount - 1)
        {
            return 0;
        }

        return configured;
    }

    public static int Pay(int grant, bool alreadyPaid)
    {
        if (alreadyPaid || grant <= 0)
        {
            return 0;
        }

        return grant;
    }

    public static string Briefing(string threat, int individuals, string advice)
    {
        if (string.IsNullOrEmpty(threat))
        {
            return string.Empty;
        }

        return threat + "\n" + individuals + " individuos\n" + advice;
    }

    public static string RewardNotice(int amount)
    {
        return "Oleada superada: +" + amount + " semillas";
    }

    public static string EnemyReadout(MatchView view, int active, int pending)
    {
        if (active < 0)
        {
            active = 0;
        }

        if (pending < 0)
        {
            pending = 0;
        }

        if (view != MatchView.Spawning && view != MatchView.Combat)
        {
            return "Enemigos  " + active;
        }

        if (pending > 0)
        {
            return "Enemigos  " + active + " activos · " + pending + " pendientes";
        }

        return "Enemigos  " + active;
    }

    public static int MemoryCap(int configured, int waveCount)
    {
        if (configured < 0)
        {
            return 0;
        }

        return configured;
    }

    public static bool IsCanonical(int[] counts, float[] intervals)
    {
        int[] expected = { 6, 8, 15, 6 };
        float[] gaps = Intervals();

        if (counts == null ||
            intervals == null ||
            counts.Length != expected.Length ||
            intervals.Length != gaps.Length)
        {
            return false;
        }

        for (int i = 0; i < expected.Length; i++)
        {
            if (counts[i] != expected[i] ||
                System.Math.Abs(intervals[i] - gaps[i]) > 0.02f)
            {
                return false;
            }
        }

        return true;
    }

    public static bool OpensAnotherWave(int index, int count)
    {
        return index >= 0 && count > 0 && index < count - 1;
    }

    private static int[] Repeat(int kind, int count)
    {
        var kinds = new int[count];

        for (int i = 0; i < count; i++)
        {
            kinds[i] = kind;
        }

        return kinds;
    }
}
