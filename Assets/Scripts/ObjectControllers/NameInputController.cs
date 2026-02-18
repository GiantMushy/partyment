using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class NameInputController : MonoBehaviour
{
    [Header("Settings")]
    private int maxPlayers;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerEntryPrefab; // Prefab containing input field and kick button

    [Header("References")]
    [SerializeField] private TMP_InputField defaultInputField;

    // Reference to PlayerManager for data access
    private PlayerManager PlayerManager => GameManager.Instance.playerManager;

    // Internal dictionary to track saved name GameObjects by player ID
    private Dictionary<int, GameObject> playerEntries = new Dictionary<int, GameObject>();

    // Counter for generating unique player IDs
    private int nextPlayerId = 0;

    // Reference to the scroll content parent (vertical layout group)
    private Transform scrollContent;

    void Start()
    {
        // The scroll content is the parent of the defaultInputField
        scrollContent = defaultInputField.transform.parent;

        // Subscribe to the default input field events
        defaultInputField.onEndEdit.AddListener(OnDefaultInputEndEdit);

        maxPlayers = PlayerManager.maxPlayers;
    }

    void OnEnable()
    {
        // Refresh display every time the state becomes active
        // Delay by one frame to ensure GameManager.Instance is ready
        StartCoroutine(RefreshOnEnable());
    }

    private System.Collections.IEnumerator RefreshOnEnable()
    {
        // Wait one frame to ensure GameManager is initialized
        yield return null;
        
        // Ensure scrollContent is set (in case OnEnable runs before Start)
        if (scrollContent == null && defaultInputField != null)
        {
            scrollContent = defaultInputField.transform.parent;
        }

        RefreshDisplayFromPlayerManager();
    }

    private int PlayerCount => PlayerManager.players.Count;

    private void OnDefaultInputEndEdit(string inputText)
    {
        // Ignore empty input or if max players reached
        if (string.IsNullOrWhiteSpace(inputText) || PlayerCount >= maxPlayers)
        {
            return;
        }

        // Add the player through PlayerManager
        AddPlayer(inputText);

        // Clear the default input field
        defaultInputField.text = "";

        // Optionally refocus the default input field for quick entry
        if (PlayerCount < maxPlayers)
        {
            defaultInputField.ActivateInputField();
        }
    }

    private void AddPlayer(string playerName)
    {
        // Generate a unique ID for the new player
        int playerId = nextPlayerId++;

        // Add to PlayerManager
        PlayerManager.AddPlayer(playerId, playerName);

        // Create the UI entry for this player
        CreatePlayerEntry(playerId, playerName);

        // Reorder entries to maintain correct order
        ReorderEntries();

        // Hide the default input field if max players reached
        if (PlayerCount >= maxPlayers)
        {
            defaultInputField.gameObject.SetActive(false);
        }

        Debug.Log($"Player added: {playerName}. Total players: {PlayerCount}");
    }

    private void CreatePlayerEntry(int playerId, string playerName)
    {
        // Instantiate the player entry prefab as child of scroll content
        GameObject entry = Instantiate(playerEntryPrefab, scrollContent);
        entry.name = $"PlayerEntry_{playerId}";
        playerEntries[playerId] = entry;

        // Get the input field component and set its value
        TMP_InputField entryInputField = entry.GetComponentInChildren<TMP_InputField>();
        if (entryInputField != null)
        {
            entryInputField.text = playerName;

            // Subscribe to changes in the saved input field
            entryInputField.onEndEdit.AddListener((newText) => OnPlayerNameEdited(playerId, newText));
        }

        // Get the kick button and set up its click event
        Button kickButton = entry.GetComponentInChildren<Button>();
        if (kickButton != null)
        {
            kickButton.onClick.AddListener(() => DeletePlayer(playerId));
        }
    }

    private void OnPlayerNameEdited(int playerId, string newName)
    {
        if (!PlayerManager.players.ContainsKey(playerId))
        {
            return;
        }

        // If the name is empty, delete the entry
        if (string.IsNullOrWhiteSpace(newName))
        {
            DeletePlayer(playerId);
            return;
        }

        // Update the player name in PlayerManager
        PlayerManager.players[playerId].name = newName;
        Debug.Log($"Player {playerId} name updated to: {newName}");
    }

    private void DeletePlayer(int playerId)
    {
        if (!PlayerManager.players.ContainsKey(playerId))
        {
            return;
        }

        // Remove from PlayerManager
        PlayerManager.RemovePlayer(playerId);

        // Destroy the corresponding GameObject
        if (playerEntries.ContainsKey(playerId))
        {
            Destroy(playerEntries[playerId]);
            playerEntries.Remove(playerId);
        }

        // Show the default input field if it was hidden
        if (PlayerCount < maxPlayers)
        {
            defaultInputField.gameObject.SetActive(true);
        }

        Debug.Log($"Player {playerId} deleted. Total players: {PlayerCount}");
    }

    private void RefreshDisplayFromPlayerManager()
    {
        // Clear existing UI entries
        foreach (var entry in playerEntries.Values)
        {
            Destroy(entry);
        }
        playerEntries.Clear();

        // Ensure defaultInputField is visible first (in case PlayerManager isn't ready)
        if (defaultInputField != null)
        {
            defaultInputField.gameObject.SetActive(true);
        }

        // Skip if PlayerManager isn't ready
        if (GameManager.Instance == null || PlayerManager == null || PlayerManager.players == null)
        {
            return;
        }

        // Rebuild from PlayerManager data
        foreach (var kvp in PlayerManager.players)
        {
            int playerId = kvp.Key;
            string playerName = kvp.Value.name;

            // Track the highest ID to avoid collisions
            if (playerId >= nextPlayerId)
            {
                nextPlayerId = playerId + 1;
            }

            CreatePlayerEntry(playerId, playerName);
        }

        // Reorder entries to maintain correct order
        ReorderEntries();

        // Update input field visibility
        defaultInputField.gameObject.SetActive(PlayerCount < maxPlayers);
    }

    private void ReorderEntries()
    {
        // Ensure the default input field is always at the top
        if (defaultInputField != null)
        {
            defaultInputField.transform.SetSiblingIndex(0);
        }

        // Sort player entries by player ID and set sibling indices below the input field
        var sortedPlayerIds = playerEntries.Keys.OrderBy(id => id).ToList();

        for (int i = 0; i < sortedPlayerIds.Count; i++)
        {
            int playerId = sortedPlayerIds[i];
            if (playerEntries.TryGetValue(playerId, out GameObject entry))
            {
                entry.transform.SetSiblingIndex(i + 1); // Start after the input field
            }
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (defaultInputField != null)
        {
            defaultInputField.onEndEdit.RemoveListener(OnDefaultInputEndEdit);
        }
    }
}
