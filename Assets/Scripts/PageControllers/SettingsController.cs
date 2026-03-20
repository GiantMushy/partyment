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

    public void Icelandic()
    {
        gameManager.SetLanguage("is");
    }

    public void English()
    {
        gameManager.SetLanguage("en");
    }
}
