using UnityEngine;

public class StartLocalGameController : MonoBehaviour
{
    private GameManager gameManager;

    // Awake is called when the script instance is being loaded
    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void Next()
    {
        Debug.Log("Assign Groups Next Button Pressed");
        gameManager.SetState(GameManager.GameState.AssignGroups);
    }
    public void Back()
    {
        Debug.Log("Start Local Game Back Button Pressed");
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }
}
