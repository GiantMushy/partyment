using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StartLocalGameController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;
    [SerializeField] private GameObject nameInputParent;
    [SerializeField] private Button nextButton;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerEntryPrefab;

    [Header("Input")]
    [SerializeField] private TMP_InputField defaultInputField;

    private const int MaxNameLength = 12;

    private int maxPlayers;
    private Dictionary<int, GameObject> playerEntries = new Dictionary<int, GameObject>();
    private int nextPlayerId = 0;
    private Transform scrollContent;

    private int PlayerCount => PlayerManager.players.Count;

    void Start()
    {
        gameManager = GameManager.Instance;
        scrollContent = defaultInputField.transform.parent;
        defaultInputField.characterLimit = MaxNameLength;
        defaultInputField.onEndEdit.AddListener(OnDefaultInputEndEdit);
        maxPlayers = PlayerManager.maxPlayers;
    }

    void OnEnable()
    {
        StartCoroutine(RefreshOnEnable());
    }

    private IEnumerator RefreshOnEnable()
    {
        yield return null;
        if (gameManager == null) gameManager = GameManager.Instance;
        if (scrollContent == null && defaultInputField != null)
            scrollContent = defaultInputField.transform.parent;
        RefreshDisplayFromPlayerManager();
    }

    // -------------------- Button Logic --------------------

    public void Next()
    {
        Debug.Log("Assign Groups Next Button Pressed");
        gameManager.SetState(GameManager.GameState.AssignGroups);
    }

    public void Back()
    {
        Debug.Log("Start Local Game Back Button Pressed");
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }

    // -------------------- Name Input Logic --------------------

    private void OnDefaultInputEndEdit(string inputText)
    {
        if (string.IsNullOrWhiteSpace(inputText) || PlayerCount >= maxPlayers)
            return;

        AddPlayer(ClampName(inputText));
        defaultInputField.text = "";

        if (PlayerCount < maxPlayers)
            defaultInputField.ActivateInputField();
    }

    private void AddPlayer(string playerName)
    {
        int playerId = nextPlayerId++;
        PlayerManager.AddPlayer(playerId, playerName);
        CreatePlayerEntry(playerId, playerName);
        ReorderEntries();

        if (PlayerCount >= maxPlayers)
            defaultInputField.gameObject.SetActive(false);

        UpdateNextButtonState();
        Debug.Log($"Player added: {playerName}. Total players: {PlayerCount}");
    }

    private void CreatePlayerEntry(int playerId, string playerName)
    {
        GameObject entry = Instantiate(playerEntryPrefab, scrollContent);
        entry.name = $"PlayerEntry_{playerId}";
        playerEntries[playerId] = entry;

        TMP_InputField entryInputField = entry.GetComponentInChildren<TMP_InputField>();
        if (entryInputField != null)
        {
            entryInputField.characterLimit = MaxNameLength;
            entryInputField.text = playerName;
            entryInputField.onEndEdit.AddListener((newText) => OnPlayerNameEdited(playerId, newText));
        }

        Button kickButton = entry.GetComponentInChildren<Button>();
        if (kickButton != null)
        {
            kickButton.onClick.AddListener(() => DeletePlayer(playerId));
        }
    }

    private void OnPlayerNameEdited(int playerId, string newName)
    {
        if (!PlayerManager.players.ContainsKey(playerId))
            return;

        if (string.IsNullOrWhiteSpace(newName))
        {
            DeletePlayer(playerId);
            return;
        }

        PlayerManager.players[playerId].name = ClampName(newName);
        Debug.Log($"Player {playerId} name updated to: {newName}");
    }

    private void DeletePlayer(int playerId)
    {
        if (!PlayerManager.players.ContainsKey(playerId))
            return;

        PlayerManager.RemovePlayer(playerId);

        if (playerEntries.ContainsKey(playerId))
        {
            Destroy(playerEntries[playerId]);
            playerEntries.Remove(playerId);
        }

        if (PlayerCount < maxPlayers)
            defaultInputField.gameObject.SetActive(true);

        UpdateNextButtonState();
        Debug.Log($"Player {playerId} deleted. Total players: {PlayerCount}");
    }

    private void RefreshDisplayFromPlayerManager()
    {
        foreach (var entry in playerEntries.Values)
            Destroy(entry);
        playerEntries.Clear();

        if (defaultInputField != null)
            defaultInputField.gameObject.SetActive(true);

        if (GameManager.Instance == null || PlayerManager == null || PlayerManager.players == null)
        {
            UpdateNextButtonState();
            return;
        }

        foreach (var kvp in PlayerManager.players)
        {
            int playerId = kvp.Key;
            string playerName = kvp.Value.name;

            if (playerId >= nextPlayerId)
                nextPlayerId = playerId + 1;

            CreatePlayerEntry(playerId, playerName);
        }

        ReorderEntries();
        defaultInputField.gameObject.SetActive(PlayerCount < maxPlayers);
        UpdateNextButtonState();
    }

    private void ReorderEntries()
    {
        if (defaultInputField != null)
            defaultInputField.transform.SetSiblingIndex(0);

        var sortedPlayerIds = playerEntries.Keys.OrderBy(id => id).ToList();
        for (int i = 0; i < sortedPlayerIds.Count; i++)
        {
            if (playerEntries.TryGetValue(sortedPlayerIds[i], out GameObject entry))
                entry.transform.SetSiblingIndex(i + 1);
        }
    }

    private void UpdateNextButtonState()
    {
        if (nextButton != null)
            nextButton.interactable = PlayerCount >= 3;
    }

    private static string ClampName(string name)
    {
        name = name.Trim();
        return name.Length > MaxNameLength ? name[..MaxNameLength] : name;
    }

    void OnDestroy()
    {
        if (defaultInputField != null)
            defaultInputField.onEndEdit.RemoveListener(OnDefaultInputEndEdit);
    }
}
