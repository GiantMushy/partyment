using UnityEngine;

/// <summary>
/// Host-vs-Join picker for the online flow. Pressing Host generates a room code via
/// <see cref="GameManager.HostOnlineGame"/> and advances to the host lobby; Join goes
/// straight to the room-code input screen.
/// </summary>
public class HostVsJoinController : MonoBehaviour
{
    private GameManager gameManager;

    void Start()
    {
        gameManager = GameManager.Instance;
    }
    public void Host()
    {
        Debug.Log("Host Button Pressed");
        gameManager.HostOnlineGame();
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
