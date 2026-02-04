using UnityEngine;

public class HostOnlineGameController : MonoBehaviour
{
    private GameManager gameManager;

    // Awake is called when the script instance is being loaded
    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void Next()
    {
        Debug.Log("Host has pressed the Start Button");
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }

    public void Back()
    {
        Debug.Log("Host Online Game Back Button Pressed");
        gameManager.SetState(GameManager.GameState.HostVsJoin);
    }
}
