using UnityEngine;

public class HostVsJoinController : MonoBehaviour
{
    private GameManager gameManager;

    // Awake is called when the script instance is being loaded
    void Start()
    {
        gameManager = GameManager.Instance;
    }
    public void Host()
    {
        Debug.Log("Host Button Pressed");
        gameManager.SetState(GameManager.GameState.HostOnlineGame);
    }
    public void Join()
    {
        Debug.Log("Join Button Pressed");
        gameManager.SetState(GameManager.GameState.JoinOnlineGame);
    }

    public void Back()
    {
        Debug.Log("Back Button Pressed");
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }
}
