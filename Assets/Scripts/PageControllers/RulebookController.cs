using UnityEngine;

/// <summary>
/// Static Rulebook screen. Only logic is the Back button, which returns to whichever
/// state the player came from via <see cref="GameManager.BackToSavedState"/>.
/// </summary>
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
