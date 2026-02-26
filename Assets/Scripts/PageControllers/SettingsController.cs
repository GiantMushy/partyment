using UnityEngine;

public class SettingsController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void Back()
    {
        gameManager.BackToSavedState();
    }
}
