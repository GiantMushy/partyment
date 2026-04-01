using System.Collections.Generic;
using TMPro;
using UnityEngine;
using AppUIButton = Unity.AppUI.UI.Button;

public class HostOnlineGameController : MonoBehaviour
{
    private GameManager gameManager;
    [SerializeField] private GameObject waitingForPlayersPanel;
    [SerializeField] private GameObject playerContainer;
    [SerializeField] private GameObject playerNamePrefab;
    [SerializeField] private TMP_InputField hostNameInputField;
    [SerializeField] private AppUIButton nextButton;
    private Dictionary<int, string> playerNames = new Dictionary<int, string>();
    private Dictionary<int, GameObject> playerEntries = new Dictionary<int, GameObject>();
    private int nextPlayerId = 0;

    // Awake is called when the script instance is being loaded
    void Start()
    {
        gameManager = GameManager.Instance;

        if (hostNameInputField != null)
            hostNameInputField.onValueChanged.AddListener(_ => RefreshNextButtonState());

        RefreshNextButtonState();
    }

    public void Next()
    {
        Debug.Log("Host has pressed the Start Button");
        // Validate Host Name
        string hostName = ValidateNameInput(hostNameInputField.text);
        if (hostName == null) return; // Invalid name, error already logged
        playerNames[nextPlayerId] = hostName;
        nextPlayerId++;
        
        gameManager.AddPlayersToOnlineGame(new List<string>(playerNames.Values));
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }

    public void Back()
    {
        Debug.Log("Host Online Game Back Button Pressed");
        gameManager.SetState(GameManager.GameState.HostVsJoin);
    }

    public void AddPlayer(string playerName)
    {
        string validatedName = ValidateNameInput(playerName);
        if (validatedName == null) return; // Invalid name, error already logged
        playerNames[nextPlayerId] = validatedName;
        nextPlayerId++;

        AddPlayerToContainer(validatedName);
        RefreshNextButtonState();
    }

    public void KickPlayer(int playerId)
    {
        if (playerNames.ContainsKey(playerId))
        {
            playerNames.Remove(playerId);
            RefreshNextButtonState();
        }
        else
        {
            Debug.LogError("Attempted to kick player with ID " + playerId + " but they were not found.");
        }
        RemoveFromPlayerContainer(playerId);
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
        return input;
    }

    private void AddPlayerToContainer(string playerName)
    {
        waitingForPlayersPanel.SetActive(false);
        GameObject newPlayerEntry = Instantiate(playerNamePrefab, playerContainer.transform);
        TMP_Text nameText = newPlayerEntry.GetComponentInChildren<TMP_Text>();
        if (nameText != null)
        {
            nameText.text = playerName;
        }
        else
        {
            Debug.LogError("Player Name Prefab is missing a TMP_Text component.");
        }

        // Connect the Kick button to KickPlayer with the current player ID
        int playerId = nextPlayerId - 1; // ID was just incremented in AddPlayer()
        playerEntries[playerId] = newPlayerEntry;
        UnityEngine.UI.Button kickButton = newPlayerEntry.GetComponentInChildren<UnityEngine.UI.Button>();
        if (kickButton != null)
        {
            kickButton.onClick.AddListener(() => KickPlayer(playerId));
        }
        else
        {
            Debug.LogError("Player Entry Prefab is missing a Button (Kick) component.");
        }
    }

    private void RemoveFromPlayerContainer(int playerId)
    {
        if (playerEntries.ContainsKey(playerId))
        {
            GameObject entry = playerEntries[playerId];
            playerEntries.Remove(playerId);
            Destroy(entry);
        }
        else
        {
            Debug.LogWarning($"Attempted to remove player {playerId} from container but entry not found.");
        }

        if (playerNames.Count == 0)
        {
            waitingForPlayersPanel.SetActive(true);
        }
    }

    private void RefreshNextButtonState()
    {
        if (nextButton == null) return;

        bool hasHostName = !string.IsNullOrWhiteSpace(hostNameInputField != null ? hostNameInputField.text : string.Empty);
        bool hasEnoughPlayers = playerNames.Count >= 3;

        nextButton.SetEnabled(hasHostName && hasEnoughPlayers);
    }

    public void DevModeAddRandomPlayer()
    {
        string randomName = "Player " + Random.Range(1, 10000);
        AddPlayer(randomName);
    }
}