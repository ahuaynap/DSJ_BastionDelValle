public struct WaveSlot
{
    public int Kind;
    public int GroupId;
    public int Member;
    public float WaitBefore;
}

public static class WaveSchedule
{
    public static int CountIndividuals(int[] kinds, int groupSize)
    {
        if (kinds == null)
        {
            return 0;
        }

        if (groupSize < 1)
        {
            groupSize = 1;
        }

        int total = 0;

        for (int i = 0; i < kinds.Length; i++)
        {
            total += kinds[i] == WaveComposition.Swarm ? groupSize : 1;
        }

        return total;
    }

    public static WaveSlot[] Expand(
        int[] kinds,
        float entryInterval,
        float memberInterval,
        int groupSize)
    {
        int count = CountIndividuals(kinds, groupSize);
        var slots = new WaveSlot[count];

        if (kinds == null)
        {
            return slots;
        }

        if (groupSize < 1)
        {
            groupSize = 1;
        }

        if (entryInterval < 0f)
        {
            entryInterval = 0f;
        }

        if (memberInterval < 0f)
        {
            memberInterval = 0f;
        }

        int cursor = 0;
        int nextGroup = 1;
        int previousGroup = 0;

        for (int i = 0; i < kinds.Length; i++)
        {
            int party = kinds[i] == WaveComposition.Swarm ? groupSize : 1;
            int group = party > 1 ? nextGroup++ : 0;

            for (int member = 0; member < party; member++)
            {
                float wait = 0f;

                if (cursor > 0)
                {
                    wait = group != 0 && group == previousGroup
                        ? memberInterval
                        : entryInterval;
                }

                slots[cursor] = new WaveSlot
                {
                    Kind = kinds[i],
                    GroupId = group,
                    Member = member,
                    WaitBefore = wait
                };
                previousGroup = group;
                cursor++;
            }
        }

        return slots;
    }

    public static bool CanComplete(
        int spawned,
        int planned,
        int active,
        bool generating)
    {
        return !generating &&
            planned > 0 &&
            spawned >= planned &&
            active <= 0;
    }
}
