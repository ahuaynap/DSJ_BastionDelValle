public enum HabitatKind
{
    Water = 0,
    Soil = 1,
    Life = 2
}

public enum HabitatCause
{
    Arrival = 0,
    WaveRecovery = 1
}

public struct HabitatChange
{
    public HabitatKind Kind;
    public int Previous;
    public int Current;
    public HabitatCause Cause;
}

public static class HabitatRules
{
    public const int StartValue = 50;
    public const int MinimumValue = 0;
    public const int MaximumValue = 100;
    public const int ArrivalDrop = 1;
    public const int WaveRecovery = 10;
    public const int BenefitThreshold = 60;
    public const float AuraDistance = 2.5f;
    public const float Haste = 0.85f;
    public const float Reach = 1.10f;
    public const int SoilBonus = 5;
    public const float ArrivalWindow = 0.4f;
    public const string SimplifiedModel = "Modelo educativo simplificado";

    public const string Explanation =
        "Umbral 60. El aura no requiere línea de visión. " +
        "Un Guardián junto a un Canal ataca antes. " +
        "Un Guardián o un Vigía junto a un Refugio alcanzan más.";

    public static bool TryChange(
        int current,
        int delta,
        int minimum,
        int maximum,
        out int next)
    {
        if (maximum < minimum)
        {
            maximum = minimum;
        }

        next = current + delta;

        if (next < minimum)
        {
            next = minimum;
        }

        if (next > maximum)
        {
            next = maximum;
        }

        return next != current;
    }

    public static int Restored(int initial, int minimum, int maximum)
    {
        TryChange(initial, 0, minimum, maximum, out int next);
        return next;
    }

    public static int ArrivalLoss(bool reachedHeart, bool alreadyCounted, int loss)
    {
        if (!reachedHeart || alreadyCounted || loss <= 0)
        {
            return 0;
        }

        return loss;
    }

    public static int Recovery(
        bool waveCleared,
        bool playing,
        bool generationFailed,
        bool alreadyApplied,
        bool hasTower,
        int gain)
    {
        if (!waveCleared ||
            !playing ||
            generationFailed ||
            alreadyApplied ||
            !hasTower ||
            gain <= 0)
        {
            return 0;
        }

        return gain;
    }

    // Distance between logical tower positions. This version does not
    // require line of sight or a clear path between the towers.
    public static bool Within(
        float x,
        float y,
        float otherX,
        float otherY,
        float distance)
    {
        if (distance < 0f)
        {
            return false;
        }

        float dx = x - otherX;
        float dy = y - otherY;
        return dx * dx + dy * dy <= distance * distance;
    }

    public static bool NearAny(
        float x,
        float y,
        float[] otherX,
        float[] otherY,
        int count,
        float distance)
    {
        if (otherX == null || otherY == null || count <= 0)
        {
            return false;
        }

        int available = count;

        if (available > otherX.Length)
        {
            available = otherX.Length;
        }

        if (available > otherY.Length)
        {
            available = otherY.Length;
        }

        for (int i = 0; i < available; i++)
        {
            if (Within(x, y, otherX[i], otherY[i], distance))
            {
                return true;
            }
        }

        return false;
    }

    public static bool AcceptsHaste(string towerId, string guardianId)
    {
        return !string.IsNullOrEmpty(towerId) && towerId == guardianId;
    }

    public static bool AcceptsReach(
        string towerId,
        string guardianId,
        string watcherId)
    {
        return !string.IsNullOrEmpty(towerId) &&
            (towerId == guardianId || towerId == watcherId);
    }

    public static float IntervalMultiplier(
        int water,
        int threshold,
        bool guardianNearChannel,
        float bonus)
    {
        if (water < threshold || !guardianNearChannel || bonus <= 0f || bonus > 1f)
        {
            return 1f;
        }

        return bonus;
    }

    public static float RangeMultiplier(
        int life,
        int threshold,
        bool eligibleNearRefuge,
        float bonus)
    {
        if (life < threshold || !eligibleNearRefuge || bonus < 1f)
        {
            return 1f;
        }

        return bonus;
    }

    public static float Scale(float baseValue, float multiplier)
    {
        if (baseValue < 0f)
        {
            baseValue = 0f;
        }

        if (multiplier <= 0f)
        {
            multiplier = 1f;
        }

        return baseValue * multiplier;
    }

    public static float NextCooldown(
        float currentCooldown,
        float baseInterval,
        float multiplier,
        bool shotFired)
    {
        if (!shotFired)
        {
            return currentCooldown;
        }

        return Scale(baseInterval, multiplier);
    }

    public static int Payout(
        int baseReward,
        int soil,
        int threshold,
        int bonus,
        bool finalWave)
    {
        if (finalWave || baseReward <= 0)
        {
            return 0;
        }

        if (soil < threshold || bonus <= 0)
        {
            return baseReward;
        }

        return baseReward + bonus;
    }

    public static int SoilExtra(
        int baseReward,
        int soil,
        int threshold,
        int bonus,
        bool finalWave)
    {
        int total = Payout(baseReward, soil, threshold, bonus, finalWave);

        if (total <= baseReward)
        {
            return 0;
        }

        return total - baseReward;
    }

    public static int GrowBatch(int count, bool windowOpen)
    {
        if (!windowOpen || count < 1)
        {
            return 1;
        }

        return count + 1;
    }

    public static string Meters(int water, int soil, int life, int maximum)
    {
        return "Agua " + water + "/" + maximum +
            "   Suelo " + soil + "/" + maximum +
            "   Biodiversidad " + life + "/" + maximum;
    }

    public static string RewardBreakdown(int baseReward, int soilBonus)
    {
        if (baseReward <= 0)
        {
            return string.Empty;
        }

        string line = "Oleada superada: +" + baseReward + " semillas";

        if (soilBonus > 0)
        {
            line += "\nSuelo: +" + soilBonus + " semillas";
        }

        return line;
    }

    public static string ArrivalLine(
        int arrivals,
        int waterLoss,
        int soilLoss,
        int lifeLoss)
    {
        if (arrivals <= 0)
        {
            return string.Empty;
        }

        string losses = JoinLoss("Agua", waterLoss);
        losses = Append(losses, JoinLoss("Suelo", soilLoss));
        losses = Append(losses, JoinLoss("Biodiversidad", lifeLoss));

        if (losses.Length == 0)
        {
            return string.Empty;
        }

        string who = arrivals == 1 ? "1 llegada" : arrivals + " llegadas";
        return who + ": " + losses;
    }

    public static string RecoveryLine(
        int waterGain,
        int soilGain,
        int lifeGain)
    {
        string gains = JoinGain("Agua", waterGain);
        gains = Append(gains, JoinGain("Suelo", soilGain));
        gains = Append(gains, JoinGain("Biodiversidad", lifeGain));

        if (gains.Length == 0)
        {
            return string.Empty;
        }

        return "Recuperación: " + gains;
    }

    public static string Consult(bool haste, bool guardianReach, bool watcherReach)
    {
        string text = "Umbral 60. El aura no requiere línea de visión.";

        if (haste)
        {
            text += " Guardián junto al Canal: intervalo ×0.85.";
        }

        if (guardianReach)
        {
            text += " Guardián junto al Refugio: alcance ×1.10.";
        }

        if (watcherReach)
        {
            text += " Vigía junto al Refugio: alcance ×1.10.";
        }

        if (!haste && !guardianReach && !watcherReach)
        {
            text += " Ninguna torre tiene la sinergia activa.";
        }

        return text;
    }

    public static string ResultReport(
        int water,
        int soil,
        int life,
        int maximum,
        bool waterSynergy,
        bool lifeSynergy,
        bool soilPaid,
        bool waterRecovered,
        bool soilRecovered,
        bool lifeRecovered)
    {
        string text = "Agua " + water + "/" + maximum + "\n" +
            "Suelo " + soil + "/" + maximum + "\n" +
            "Biodiversidad " + life + "/" + maximum;

        if (waterRecovered)
        {
            text += "\nEl Canal Vivo recuperó el agua.";
        }

        if (soilRecovered)
        {
            text += "\nLa Terraza de Raíces recuperó el suelo.";
        }

        if (lifeRecovered)
        {
            text += "\nEl Refugio recuperó la biodiversidad.";
        }

        if (waterSynergy)
        {
            text += "\nEl agua aceleró al Guardián.";
        }

        if (lifeSynergy)
        {
            text += "\nLa biodiversidad amplió el alcance.";
        }

        if (soilPaid)
        {
            text += "\nEl suelo añadió semillas.";
        }

        return text + "\n" + SimplifiedModel;
    }

    private static string JoinLoss(string label, int loss)
    {
        if (loss <= 0)
        {
            return string.Empty;
        }

        return label + " -" + loss;
    }

    private static string JoinGain(string label, int gain)
    {
        if (gain <= 0)
        {
            return string.Empty;
        }

        return label + " +" + gain;
    }

    private static string Append(string left, string right)
    {
        if (right.Length == 0)
        {
            return left;
        }

        if (left.Length == 0)
        {
            return right;
        }

        return left + ", " + right;
    }
}
