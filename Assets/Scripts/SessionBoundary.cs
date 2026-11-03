using UnityEngine;

public class SessionBoundary : MonoBehaviour
{
    private static bool traveling;

    public static bool IsTraveling => traveling;

    public static void BeginTravel()
    {
        traveling = true;
        Time.timeScale = 1f;
        ReleaseStatics();
    }

    public static void FinishTravel()
    {
        traveling = false;
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying || traveling)
        {
            return;
        }

        ReleaseStatics();
    }

    public static void ReleaseStatics()
    {
        Projectile.ReleaseSceneListeners();
        TowerAttack.ReleaseSceneListeners();
    }
}
