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
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }
}
