using UnityEngine;

public class LocalVsOnlineController : MonoBehaviour
{
    private GameManager gameManager;

    // Awake is called when the script instance is being loaded
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
