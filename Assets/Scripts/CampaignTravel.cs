using UnityEngine;
using UnityEngine.SceneManagement;

public static class CampaignTravel
{
    public static void ToMenu(EducationalCollectionManager collection)
    {
        Prepare();

        if (collection != null)
        {
            collection.TryFlushPending(out _);
        }

        SceneManager.LoadScene(
            CampaignRules.MenuScene,
            LoadSceneMode.Single);
    }

    public static void ToMap(string sceneName)
    {
        if (!CampaignRules.IsPlayableMap(sceneName))
        {
            return;
        }

        Prepare();
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    private static void Prepare()
    {
        SessionReload.Acknowledge();
        SessionBoundary.BeginTravel();
    }
}
