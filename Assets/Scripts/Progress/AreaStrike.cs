public struct AreaBody
{
    public float X;
    public float Y;
    public int Health;
    public bool Alive;
    public int Id;
}

public static class AreaStrike
{
    public static bool ShouldDetonate(bool targetPresent)
    {
        return targetPresent;
    }

    public static int Collect(
        float centerX,
        float centerY,
        float radius,
        AreaBody[] bodies,
        int count,
        int[] hits)
    {
        if (bodies == null || hits == null || count <= 0 || radius < 0f)
        {
            return 0;
        }

        int limit = count < bodies.Length ? count : bodies.Length;
        float radiusSq = radius * radius;
        int found = 0;

        for (int i = 0; i < limit && found < hits.Length; i++)
        {
            if (!bodies[i].Alive || bodies[i].Health <= 0)
            {
                continue;
            }

            float dx = bodies[i].X - centerX;
            float dy = bodies[i].Y - centerY;

            if (dx * dx + dy * dy <= radiusSq)
            {
                hits[found] = i;
                found++;
            }
        }

        return found;
    }

    public static int Apply(
        AreaBody[] bodies,
        int[] hits,
        int hitCount,
        int damage)
    {
        if (bodies == null || hits == null || hitCount <= 0 || damage <= 0)
        {
            return 0;
        }

        int applied = 0;
        int limit = hitCount < hits.Length ? hitCount : hits.Length;

        for (int i = 0; i < limit; i++)
        {
            int index = hits[i];
            bool repeated = false;

            for (int previous = 0; previous < i; previous++)
            {
                if (hits[previous] == index)
                {
                    repeated = true;
                    break;
                }
            }

            if (repeated || index < 0 || index >= bodies.Length)
            {
                continue;
            }

            if (!bodies[index].Alive || bodies[index].Health <= 0)
            {
                continue;
            }

            bodies[index].Health -= damage;

            if (bodies[index].Health <= 0)
            {
                bodies[index].Health = 0;
                bodies[index].Alive = false;
            }

            applied++;
        }

        return applied;
    }
}
