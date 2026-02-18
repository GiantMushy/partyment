using UnityEngine;
using TMPro;

public class PlayerMutexController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    [SerializeField] private TextMeshProUGUI nameDisplay;
    [SerializeField] private TextMeshProUGUI buttonText;

    private GameManager.GameState nextState;
    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void SetNameAndNextState(string name, GameManager.GameState nextState)
    {
        if (nameDisplay != null)
        {
            nameDisplay.text = name;
            buttonText.text = "I am " + name;
        }
        this.nextState = nextState;
    }

    public void IAmButton()
    {
        gameManager.ExitMutex(nextState);
    }
}