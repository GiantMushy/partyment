using UnityEngine;

public class RulebookController : MonoBehaviour
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
