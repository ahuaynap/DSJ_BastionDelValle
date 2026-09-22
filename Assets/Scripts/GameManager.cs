using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameState initialState = GameState.Playing;

    private GameState currentState;

    public GameState CurrentState => currentState;

    public bool IsPlaying =>
        currentState == GameState.Playing;

    public event Action<GameState> OnGameStateChanged;

    private void Awake()
    {
        currentState = initialState;

        Debug.Log($"Game State: {currentState}");
    }

    public void SetVictory()
    {
        if (!IsPlaying)
        {
            return;
        }

        currentState = GameState.Victory;
        OnGameStateChanged?.Invoke(currentState);

        Debug.Log("GAME STATE -> VICTORY");
    }

    public void SetDefeat()
    {
        if (!IsPlaying)
        {
            return;
        }

        currentState = GameState.Defeat;
        OnGameStateChanged?.Invoke(currentState);
        
        Debug.Log("GAME STATE -> DEFEAT");
    }
}