using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Privacy hand-off screen shown when the device passes between players. Displays a
/// name and a button labelled with a prefix plus that name; pressing the button
/// advances to the configured <see cref="GameManager.GameState"/> via
/// <see cref="GameManager.ExitMutex"/>. Configured by
/// <see cref="SetNameAndNextState(string, GameManager.GameState)"/> (default prefix
/// "I am ") or the overload that accepts a custom prefix.
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
        string localizedPrefix = pendingPrefix;
        if (gameManager != null && gameManager.selectedLanguage == GameManager.Language.Icelandic)
        {
            if (pendingPrefix == "I am ") localizedPrefix = "Ég er ";
            else if (pendingPrefix == "We are ") localizedPrefix = "Við erum ";
        }
        if (nameDisplay != null) nameDisplay.text = pendingName;
        if (buttonText != null) buttonText.text = localizedPrefix + pendingName;
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
