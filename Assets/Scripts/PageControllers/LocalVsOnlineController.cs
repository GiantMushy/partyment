using UnityEngine;

/// <summary>
/// The game's first screen: pick Local or Online. Local sets
/// <see cref="GameManager.GameMode.Local"/> and heads to Pack Selection; Online goes to the
/// Host-vs-Join picker. This is the root screen, so its Back button is hidden — <see cref="Back"/>
/// is kept only so any stale scene wiring resolves.
/// </summary>
public class LocalVsOnlineController : MonoBehaviour
{
    private GameManager gameManager;

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void Local()
    {
        Debug.Log("Local Button Pressed");
        gameManager.currentGameMode = GameManager.GameMode.Local;
        gameManager.SetState(GameManager.GameState.PackSelection);
    }
    public void Online()
    {
        Debug.Log("Online Button Pressed");
        gameManager.SetState(GameManager.GameState.HostVsJoin);
    }
    public void Back()
    {
        Debug.Log("Back Button Pressed");
        gameManager.SetState(GameManager.GameState.PackSelection);
    }
}
