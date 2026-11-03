public static class ZoneSchedule
{
    public static bool Tick(
        float elapsed,
        float delta,
        float duration,
        out float next,
        out bool finished)
    {
        if (delta <= 0f || float.IsNaN(delta) || float.IsInfinity(delta))
        {
            next = elapsed < 0f ? 0f : elapsed;
            finished = duration > 0f && next >= duration;
            return false;
        }

        next = (elapsed < 0f ? 0f : elapsed) + delta;
        finished = duration > 0f && next >= duration;
        return true;
    }

    public static int DuePulses(float elapsed, float secondDelay)
    {
        if (elapsed < 0f || float.IsNaN(elapsed))
        {
            return 0;
        }

        if (secondDelay <= 0f)
        {
            return 1;
        }

        return elapsed + 0.00001f >= secondDelay ? 2 : 1;
    }

    public static bool AllowPulse(bool playing, bool paused, bool closed)
    {
        return playing && !paused && !closed;
    }
}
