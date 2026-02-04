using UnityEngine;

public class JoinOnlineGameController : MonoBehaviour
{
    private GameManager gameManager;

    // Awake is called when the script instance is being loaded
    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void Join()
    {
        Debug.Log("Second Join Button Pressed");
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }

    public void Back()
    {
        Debug.Log("Back Button Pressed");
        gameManager.SetState(GameManager.GameState.HostVsJoin);
    }
}
