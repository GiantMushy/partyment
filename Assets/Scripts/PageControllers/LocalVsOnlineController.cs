using UnityEngine;

/// <summary>
/// Three-button picker (Local / Online / Back) shown after Pack Selection.
/// Routes to <see cref="GameManager.GameState.StartLocalGame"/> or
/// <see cref="GameManager.GameState.HostVsJoin"/>.
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
        gameManager.SetState(GameManager.GameState.StartLocalGame);
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
