using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Privacy hand-off screen shown whenever the device passes between players (e.g. before
/// each player views their secret corruption, or before a group casts its votes). Displays
/// "<i>name</i>" and a button labelled "<i>prefix + name</i>" (e.g. "I am Þorri"); pressing
/// it advances to the configured <see cref="GameManager.GameState"/> via
/// <see cref="GameManager.ExitMutex"/>.
/// Configure with <see cref="SetNameAndNextState(string, GameManager.GameState)"/>
/// (defaults the prefix to "I am ") or the overload that accepts a custom prefix.
/// </summary>
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
