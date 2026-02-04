using UnityEngine;

public class PackSelectionController : MonoBehaviour
{
    private GameManager gameManager;

    // Awake is called when the script instance is being loaded
    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void OnPackSelectionButton()
    {
        Debug.Log("Pack Selection Button Pressed");
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }
}
