using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class NameInputController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int maxPlayers = 8;
    [SerializeField] private float spacingPercent = 0.2f; // 20% spacing between entries

    [Header("Prefabs")]
    [SerializeField] private GameObject playerEntryPrefab; // Prefab containing input field and kick button

    [Header("References")]
    [SerializeField] private TMP_InputField defaultInputField;

    [Header("Player Names")]
    public List<string> playerNames = new List<string>();

    // Internal list to track saved name GameObjects
    private List<GameObject> savedNameEntries = new List<GameObject>();
    private float entryHeight;
    private float entrySpacing;

    void Start()
    {
        // Calculate entry height and spacing based on the input field in the prefab
        Transform inputFieldTransform = playerEntryPrefab.transform.Find("Input Field");
        if (inputFieldTransform != null)
        {
            RectTransform inputRect = inputFieldTransform.GetComponent<RectTransform>();
            if (inputRect != null)
            {
                entryHeight = inputRect.sizeDelta.y;
                entrySpacing = entryHeight * spacingPercent;
            }
        }
        else
        {
            Debug.LogError("Could not find 'Input Field' child in playerEntryPrefab");
            entryHeight = 50f; // Fallback
            entrySpacing = entryHeight * spacingPercent;
        }

        // Subscribe to the default input field events
        defaultInputField.onEndEdit.AddListener(OnDefaultInputEndEdit);
    }

    private void OnDefaultInputEndEdit(string inputText)
    {
        // Ignore empty input or if max players reached
        if (string.IsNullOrWhiteSpace(inputText) || playerNames.Count >= maxPlayers)
        {
            return;
        }

        // Add the name to the list
        AddPlayerName(inputText);

        // Clear the default input field
        defaultInputField.text = "";

        // Optionally refocus the default input field for quick entry
        if (playerNames.Count < maxPlayers)
        {
            defaultInputField.ActivateInputField();
        }
    }

    private void AddPlayerName(string playerName)
    {
        // Add to the names list
        playerNames.Add(playerName);

        // Save to GameManager immediately
        SaveToGameManager();

        // Instantiate the player entry prefab
        GameObject entry = Instantiate(playerEntryPrefab, transform);
        entry.name = $"PlayerEntry_{playerNames.Count}";
        savedNameEntries.Add(entry);

        // Get the input field component and set its value
        TMP_InputField entryInputField = entry.GetComponentInChildren<TMP_InputField>();
        if (entryInputField != null)
        {
            entryInputField.text = playerName;

            // Store the index for this entry
            int index = playerNames.Count - 1;

            // Subscribe to changes in the saved input field
            entryInputField.onEndEdit.AddListener((newText) => OnSavedNameEdited(index, newText));
        }

        // Get the kick button and set up its click event
        Button kickButton = entry.GetComponentInChildren<Button>();
        if (kickButton != null)
        {
            int index = playerNames.Count - 1;
            kickButton.onClick.AddListener(() => DeletePlayerName(index));
        }

        // Position all entries
        RepositionAllEntries();

        // Hide the default input field if max players reached
        if (playerNames.Count >= maxPlayers)
        {
            defaultInputField.gameObject.SetActive(false);
        }

        Debug.Log($"Player added: {playerName}. Total players: {playerNames.Count}");
    }

    private void OnSavedNameEdited(int index, string newName)
    {
        if (index >= 0 && index < playerNames.Count)
        {
            // If the name is empty, delete the entry
            if (string.IsNullOrWhiteSpace(newName))
            {
                DeletePlayerName(index);
                return;
            }

            playerNames[index] = newName;
            Debug.Log($"Player {index + 1} name updated to: {newName}");
            
            // Save to GameManager when name is edited
            SaveToGameManager();
        }
    }

    private void DeletePlayerName(int index)
    {
        if (index < 0 || index >= playerNames.Count)
        {
            return;
        }

        // Remove from the names list
        playerNames.RemoveAt(index);

        // Destroy the corresponding GameObject
        Destroy(savedNameEntries[index]);
        savedNameEntries.RemoveAt(index);

        // Rebuild the indices for remaining entries and reposition them
        RebuildEntryIndices();
        RepositionAllEntries();

        // Show the default input field if it was hidden
        if (playerNames.Count < maxPlayers)
        {
            defaultInputField.gameObject.SetActive(true);
        }

        Debug.Log($"Player {index + 1} deleted. Total players: {playerNames.Count}");
        
        // Save to GameManager after deletion
        SaveToGameManager();
    }

    private void SaveToGameManager()
    {
        GameManager.Instance.players.Clear();
        
        for (int i = 0; i < playerNames.Count; i++)
        {
            string name = playerNames[i];
            PlayerModel player = new PlayerModel(i, name);
            GameManager.Instance.players[i] = player;
        }
        
        Debug.Log($"Saved {GameManager.Instance.players.Count} players to GameManager");
    }

    private void RepositionAllEntries()
    {
        // Position each entry vertically with spacing
        for (int i = 0; i < savedNameEntries.Count; i++)
        {
            RectTransform entryRect = savedNameEntries[i].GetComponent<RectTransform>();
            if (entryRect != null)
            {
                // Calculate Y position: first entry at top (0), each subsequent entry below
                float yPosition = -i * (entryHeight + entrySpacing);
                entryRect.anchoredPosition = new Vector2(0, yPosition);
            }
        }
    }

    private void RebuildEntryIndices()
    {
        // Rebuild listeners with correct indices after deletion
        for (int i = 0; i < savedNameEntries.Count; i++)
        {
            GameObject entry = savedNameEntries[i];

            // Rename the entry for clarity
            entry.name = $"PlayerEntry_{i + 1}";

            // Update the input field listener
            TMP_InputField entryInputField = entry.GetComponentInChildren<TMP_InputField>();
            if (entryInputField != null)
            {
                entryInputField.onEndEdit.RemoveAllListeners();
                int index = i;
                entryInputField.onEndEdit.AddListener((newText) => OnSavedNameEdited(index, newText));
            }

            // Update the delete button listener
            Button deleteButton = entry.GetComponentInChildren<Button>();
            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                int index = i;
                deleteButton.onClick.AddListener(() => DeletePlayerName(index));
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
