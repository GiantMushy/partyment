using UnityEngine;

public class MenuController : MonoBehaviour
{
    private GameManager gameManager;

    // Awake is called when the script instance is being loaded
    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void ToggleMenu()
    {
        Debug.Log("Assign Groups Next Button Pressed");
        gameManager.ToggleMenu();
    }
}
