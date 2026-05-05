using System.Collections;
using UnityEngine;
using TMPro;

public class PlayerMutexController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    [SerializeField] private TextMeshProUGUI nameDisplay;
    [SerializeField] private TextMeshProUGUI buttonText;

    private GameManager.GameState nextState;
    private string pendingName;
    private string pendingPrefix;

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    void OnEnable()
    {
        if (pendingName != null)
            StartCoroutine(ApplyTextNextFrame());
    }

    private IEnumerator ApplyTextNextFrame()
    {
        yield return null;
        if (nameDisplay != null) nameDisplay.text = pendingName;
        if (buttonText != null) buttonText.text = pendingPrefix + pendingName;
    }

    public void SetNameAndNextState(string name, GameManager.GameState nextState)
    {
        pendingName = name;
        pendingPrefix = "I am ";
        this.nextState = nextState;
    }

    public void SetNameAndNextState(string name, string buttonPrefix, GameManager.GameState nextState)
    {
        pendingName = name;
        pendingPrefix = buttonPrefix;
        this.nextState = nextState;
    }

    public void IAmButton()
    {
        gameManager.ExitMutex(nextState);
    }
}
