using TMPro;
using UnityEngine;

public class EndGameUI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private TMP_Text resultText;

    private void Start()
    {
        gameManager.OnGameStateChanged += HandleGameStateChanged;

        HandleGameStateChanged(
            gameManager.CurrentState
        );
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }
    }

    private void HandleGameStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.Playing:

                resultText.gameObject.SetActive(false);

                break;

            case GameState.Victory:

                resultText.gameObject.SetActive(true);
                resultText.text = "VICTORY";

                break;

            case GameState.Defeat:

                resultText.gameObject.SetActive(true);
                resultText.text = "DEFEAT";

                break;
        }
    }
}