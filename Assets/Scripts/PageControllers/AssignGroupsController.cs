using UnityEngine;

public class AssignGroupsController : MonoBehaviour
{
    private GameManager gameManager;

    // Awake is called when the script instance is being loaded
    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void Next()
    {
        Debug.Log("Save Local Names Button Pressed");

        if (gameManager.players == null || gameManager.players.Count < 3)
        {
            Debug.LogWarning($"Need at least 3 players to continue. Current count: {(gameManager.players?.Count ?? 0)}");
            return;
        }

        Debug.Log($"Proceeding with {gameManager.players.Count} players");
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }
}
