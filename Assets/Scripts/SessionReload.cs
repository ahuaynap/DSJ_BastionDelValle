using UnityEngine;
using UnityEngine.SceneManagement;

public static class SessionReload
{
    private static bool reloading;

    public static bool IsReloading => reloading;

    public static string Notice { get; private set; }

    public static void Reload(EducationalCollectionManager collection)
    {
        if (reloading)
        {
            return;
        }

        string notice = null;

        if (collection != null &&
            !collection.TryFlushPending(out notice) &&
            string.IsNullOrEmpty(notice))
        {
            notice =
                "No se pudieron guardar las memorias de esta partida.";
        }

        reloading = true;
        Notice = notice;
        SessionBoundary.BeginTravel();
        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex,
            LoadSceneMode.Single);
    }

    public static void Acknowledge()
    {
        reloading = false;
    }

    public static string ConsumeNotice()
    {
        string notice = Notice;
        Notice = null;
        return notice;
    }
}
