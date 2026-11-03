using System;
using System.Collections.Generic;
using UnityEngine;

public class PauseManager : MonoBehaviour
{
    private readonly Dictionary<
        PauseReason,
        int
    > pauseRequests = new();

    private float previousTimeScale = 1f;

    public bool IsPaused =>
        GetTotalPauseRequests() > 0;

    public event Action<bool>
        OnPauseStateChanged;

    public void RequestPause(
        PauseReason reason)
    {
        bool wasPaused = IsPaused;

        if (pauseRequests.ContainsKey(reason))
        {
            pauseRequests[reason]++;
        }
        else
        {
            pauseRequests.Add(reason, 1);
        }

        if (wasPaused)
        {
            return;
        }

        previousTimeScale = Time.timeScale;

        if (previousTimeScale <= 0f)
        {
            previousTimeScale = 1f;
        }

        Time.timeScale = 0f;

        OnPauseStateChanged?.Invoke(true);

        Debug.Log(
            $"Game paused. Reason: {reason}"
        );
    }

    public void ReleasePause(
        PauseReason reason)
    {
        if (!pauseRequests.TryGetValue(
                reason,
                out int requestCount))
        {
            return;
        }

        if (requestCount > 1)
        {
            pauseRequests[reason] =
                requestCount - 1;
        }
        else
        {
            pauseRequests.Remove(reason);
        }

        if (IsPaused)
        {
            return;
        }

        Time.timeScale = previousTimeScale;

        OnPauseStateChanged?.Invoke(false);

        Debug.Log(
            $"Game pause released. Reason: {reason}"
        );
    }

    public bool HasPauseRequest(
        PauseReason reason)
    {
        return pauseRequests.ContainsKey(reason);
    }

    private int GetTotalPauseRequests()
    {
        int total = 0;

        foreach (
            KeyValuePair<PauseReason, int> request
            in pauseRequests)
        {
            total += request.Value;
        }

        return total;
    }

    private void OnDestroy()
    {
        if (!IsPaused || SessionBoundary.IsTraveling)
        {
            return;
        }

        pauseRequests.Clear();

        if (SessionReload.IsReloading || previousTimeScale <= 0f)
        {
            Time.timeScale = 1f;
            return;
        }

        Time.timeScale = previousTimeScale;
    }
}