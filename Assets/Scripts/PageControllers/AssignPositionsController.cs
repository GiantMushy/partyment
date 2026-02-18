using UnityEngine;

public class AssignPositionsController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void Next()
    {
        gameManager.StartMutex(gameManager.playerManager.players[0], GameManager.GameState.SecretObjectiveMutexDisplay);
    }

    public void Back()
    {
        gameManager.SetState(GameManager.GameState.MetricSelection);
    }
}
