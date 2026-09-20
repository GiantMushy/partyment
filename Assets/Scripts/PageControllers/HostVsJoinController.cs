using UnityEngine;

/// <summary>
/// Host-vs-Join picker for the online flow. Host routes through Pack Selection first (the
/// room is created afterwards in <see cref="GameManager.ContinueAfterPackSelection"/>); Join
/// skips pack selection and goes straight to the room-code input screen.
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
        gameManager.currentGameMode = GameManager.GameMode.OnlineHost;
        // Room creation is deferred to ContinueAfterPackSelection so the host picks a pack first.
        gameManager.SetState(GameManager.GameState.PackSelection);
    }
    public void Join()
    {
        Debug.Log("Join Button Pressed");
        gameManager.currentGameMode = GameManager.GameMode.OnlineJoin;
        gameManager.SetState(GameManager.GameState.JoinOnlineGame);
    }

    public void Back()
    {
        Debug.Log("Back Button Pressed");
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }
}
