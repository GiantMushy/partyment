using TMPro;
using UnityEngine;

public class JoinOnlineGameController : MonoBehaviour
{
    private GameManager gameManager;
    [SerializeField] private TMP_InputField roomCodeInputField;
    [SerializeField] private TMP_InputField nameInputField;

    // Awake is called when the script instance is being loaded
    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void Join()
    {
        Debug.Log("Second Join Button Pressed");
        // Validate Name Input
        string playerName = ValidateNameInput(nameInputField.text);
        if (playerName == null) return; // Invalid name, error already logged

        int roomCode = ValudateRoomCode(roomCodeInputField.text);
        if (roomCode == -1) return; // Invalid room code, error already logged
        
        gameManager.JoinOnlineGame(roomCode, playerName);
    }

    public void Back()
    {
        Debug.Log("Back Button Pressed");
        gameManager.SetState(GameManager.GameState.HostVsJoin);
    }

    private string ValidateNameInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            Debug.LogError("Player name cannot be empty.");
            return null;
        }
        if (input.Length > 20)
        {
            Debug.LogError("Player name cannot exceed 20 characters.");
            return null;
        }
        // TODO: Maybe some profanity filter?
        return input.Trim();
    }

    private int ValudateRoomCode(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            Debug.LogError("Room code cannot be empty.");
            return -1;
        }
        if (!int.TryParse(input, out int roomCode) || roomCode < 0)
        {
            Debug.LogError("Room code must be a valid positive integer.");
            return -1;
        }
        if (roomCode < 100000 || roomCode > 999999) // room code must be 6 digits
        {
            Debug.LogError("Room code must be a 6-digit number.");
            return -1;
        }
        return roomCode;
    }
}
