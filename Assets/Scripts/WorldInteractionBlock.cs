using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public static class WorldInteractionBlock
{
    private static int suppressedFrame = -1;

    private static bool missingEventSystemReported;

    public static void SuppressCurrentClick()
    {
        suppressedFrame = Time.frameCount;
    }

    public static bool AllowsWorldAction(
        PauseManager pauseManager)
    {
        if (suppressedFrame == Time.frameCount)
        {
            return false;
        }

        if (pauseManager == null ||
            pauseManager.IsPaused)
        {
            return false;
        }

        return !IsPointerOverInteractiveUI();
    }

    private static bool IsPointerOverInteractiveUI()
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            ReportMissingEventSystem();
            return true;
        }

        if (eventSystem.IsPointerOverGameObject(-1))
        {
            return true;
        }

        Mouse mouse = Mouse.current;

        if (mouse != null &&
            eventSystem.IsPointerOverGameObject(
                mouse.deviceId))
        {
            return true;
        }

        return false;
    }

    private static void ReportMissingEventSystem()
    {
        if (missingEventSystemReported)
        {
            return;
        }

        missingEventSystemReported = true;

        Debug.LogError(
            "World interaction requires the scene EventSystem. " +
            "Building and capture are disabled."
        );
    }
}
