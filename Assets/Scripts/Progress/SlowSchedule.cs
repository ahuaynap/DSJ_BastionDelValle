using System;
using System.Collections.Generic;

public struct SlowStamp
{
    public float Multiplier;
    public float ExpiresAt;
    public int SourceId;
}

public static class SlowSchedule
{
    public const float EquivalentEpsilon = 0.0001f;

    public static bool ShouldApply(
        bool targetValid,
        bool alreadyResolved,
        float duration,
        float multiplier)
    {
        return targetValid &&
            !alreadyResolved &&
            duration > 0f &&
            multiplier > 0f &&
            multiplier < 1f;
    }

    public static void Apply(
        List<SlowStamp> effects,
        float multiplier,
        float duration,
        float now)
    {
        if (effects == null ||
            !ShouldApply(true, false, duration, multiplier))
        {
            return;
        }

        float expiry = now + duration;

        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].SourceId != 0)
            {
                continue;
            }

            if (Math.Abs(effects[i].Multiplier - multiplier) > EquivalentEpsilon)
            {
                continue;
            }

            SlowStamp renewed = effects[i];
            renewed.ExpiresAt = expiry;
            effects[i] = renewed;
            return;
        }

        effects.Add(new SlowStamp
        {
            Multiplier = multiplier,
            ExpiresAt = expiry
        });
    }

    public static float Multiplier(List<SlowStamp> effects, float now)
    {
        float strongest = 1f;

        if (effects == null)
        {
            return strongest;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            SlowStamp effect = effects[i];

            if (effect.ExpiresAt <= now || effect.Multiplier >= strongest)
            {
                continue;
            }

            strongest = effect.Multiplier;
        }

        return strongest;
    }

    public static float Speed(float baseSpeed, float multiplier)
    {
        if (baseSpeed < 0f)
        {
            return 0f;
        }

        if (multiplier <= 0f)
        {
            return 0f;
        }

        if (multiplier > 1f)
        {
            return baseSpeed;
        }

        return baseSpeed * multiplier;
    }

    public static bool SetPresence(
        List<SlowStamp> effects,
        int sourceId,
        float multiplier)
    {
        if (effects == null ||
            sourceId == 0 ||
            multiplier <= 0f ||
            multiplier >= 1f)
        {
            return false;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].SourceId != sourceId)
            {
                continue;
            }

            if (Math.Abs(effects[i].Multiplier - multiplier) <= EquivalentEpsilon)
            {
                return false;
            }

            SlowStamp updated = effects[i];
            updated.Multiplier = multiplier;
            effects[i] = updated;
            return true;
        }

        effects.Add(new SlowStamp
        {
            Multiplier = multiplier,
            ExpiresAt = float.PositiveInfinity,
            SourceId = sourceId
        });
        return true;
    }

    public static bool RemovePresence(List<SlowStamp> effects, int sourceId)
    {
        if (effects == null || sourceId == 0)
        {
            return false;
        }

        bool removed = false;

        for (int i = effects.Count - 1; i >= 0; i--)
        {
            if (effects[i].SourceId != sourceId)
            {
                continue;
            }

            effects.RemoveAt(i);
            removed = true;
        }

        return removed;
    }

    public static void Prune(List<SlowStamp> effects, float now)
    {
        if (effects == null)
        {
            return;
        }

        for (int i = effects.Count - 1; i >= 0; i--)
        {
            if (effects[i].ExpiresAt <= now)
            {
                effects.RemoveAt(i);
            }
        }
    }
}
