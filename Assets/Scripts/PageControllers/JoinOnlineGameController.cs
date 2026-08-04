using TMPro;
using UnityEngine;

/// <summary>
/// Online-game join screen. Validates the player's name and a 6-digit numeric room
/// code before calling <see cref="GameManager.JoinOnlineGame"/>. Networking is not yet
/// implemented.
/// </summary>
public class JoinOnlineGameController : MonoBehaviour
{
    private GameManager gameManager;
    [SerializeField] private TMP_InputField roomCodeInputField;
    [SerializeField] private TMP_InputField nameInputField;

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void Join()
    {
        Debug.Log("Second Join Button Pressed");
        string playerName = ValidateNameInput(nameInputField.text);
        if (playerName == null) return;

        int roomCode = ValudateRoomCode(roomCodeInputField.text);
        if (roomCode == -1) return;

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
        // TODO: Profanity filter on player names.
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
        if (roomCode < 100000 || roomCode > 999999)
        {
            Debug.LogError("Room code must be a 6-digit number.");
            return -1;
        }
        return roomCode;
    }
}
