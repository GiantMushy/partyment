using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

public class AssignGroupsController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;
    [SerializeField] private TextMeshProUGUI dmNameFieldDisplay;
    [SerializeField] private GameObject groupDisplayParent;

    [Header("Prefabs")]
    [SerializeField] private GameObject onePlayerGroupPrefab;
    [SerializeField] private GameObject twoPlayerGroupPrefab;
    [SerializeField] private GameObject threePlayerGroupPrefab;

    [Header("Settings")]
    public int numberOfGroups = 2;

    private int dmId;

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        AssignPlayersToRandomGroups();
        DisplayGroups();
    }

    // -------------------- Button Logic --------------------
    public void Next()
    {
        // Assign a random secret objective to each non-DM player
        gameManager.secretObjectiveManager.AssignSecretObjectivesToPlayers(PlayerManager.players, dmId);

        gameManager.StartMutex(PlayerManager.players[dmId], GameManager.GameState.TopicSelection);
    }

    public void Back()
    {
        gameManager.SetState(GameManager.GameState.StartLocalGame);
    }

    public void AddGroup()
    {
        int nonDMPlayerCount = PlayerManager.players.Count - 1;
        if (numberOfGroups < nonDMPlayerCount)
        {
            numberOfGroups++;
            AssignPlayersToRandomGroups();
            DisplayGroups();
        }
    }

    public void RemoveGroup()
    {
        int nonDMPlayerCount = PlayerManager.players.Count - 1;
        int minGroups = Mathf.Max(2, Mathf.CeilToInt(nonDMPlayerCount / 3f));
        if (numberOfGroups > minGroups)
        {
            numberOfGroups--;
            AssignPlayersToRandomGroups();
            DisplayGroups();
        }
    }

    // -------------------- Assignment Logic --------------------

    private void AssignPlayersToRandomGroups()
    {
        PlayerManager.ClearAllGroups();
        dmId = GetLowestPlayerId();

        var nonDMPlayers = GetShuffledNonDMPlayers(dmId);

        if (nonDMPlayers.Count < 2)
        {
            numberOfGroups = Mathf.Max(1, nonDMPlayers.Count);
        }
        else
        {
            int minGroups = Mathf.Max(2, Mathf.CeilToInt(nonDMPlayers.Count / 3f));
            numberOfGroups = Mathf.Clamp(numberOfGroups, minGroups, nonDMPlayers.Count);
        }

        var playerGroups = DistributePlayersIntoGroups(nonDMPlayers, numberOfGroups);
        ApplyGroupAssignments(playerGroups);
    }

    private int GetLowestPlayerId()
    {
        return PlayerManager.players.Keys.Min();
    }

    private List<Player> GetShuffledNonDMPlayers(int excludeId)
    {
        return PlayerManager.players.Values
            .Where(p => p.id != excludeId)
            .OrderBy(_ => Random.Range(0f, 1f))
            .ToList();
    }

    private List<List<Player>> DistributePlayersIntoGroups(List<Player> players, int targetGroupCount)
    {
        targetGroupCount = Mathf.Clamp(targetGroupCount, 1, players.Count);

        var groups = new List<List<Player>>();
        for (int i = 0; i < targetGroupCount; i++)
            groups.Add(new List<Player>());

        for (int i = 0; i < players.Count; i++)
            groups[i % targetGroupCount].Add(players[i]);

        return groups;
    }

    private void ApplyGroupAssignments(List<List<Player>> playerGroups)
    {
        for (int i = 0; i < playerGroups.Count; i++)
        {
            var group = PlayerManager.CreateGroup($"Group {i + 1}");
            foreach (var player in playerGroups[i])
            {
                PlayerManager.UpdatePlayerGroup(player.id, group.id);
            }
        }
        numberOfGroups = playerGroups.Count;
    }

    // -------------------- Display Logic --------------------

    private void DisplayGroups()
    {
        ClearGroupDisplays();
        DisplayDMName();
        InstantiateGroupDisplays();
    }

    private void ClearGroupDisplays()
    {
        foreach (Transform child in groupDisplayParent.transform)
        {
            Destroy(child.gameObject);
        }
    }

    private void DisplayDMName()
    {
        if (PlayerManager.players.ContainsKey(dmId))
        {
            dmNameFieldDisplay.text = PlayerManager.players[dmId].name;
        }
    }

    private void InstantiateGroupDisplays()
    {
        foreach (var group in PlayerManager.groups.Values)
        {
            var groupPlayers = PlayerManager.GetPlayersWithGroupId(group.id);
            GameObject prefab = GetPrefabForGroupSize(groupPlayers.Count);
            if (prefab == null) continue;

            GameObject display = Instantiate(prefab, groupDisplayParent.transform);
            SetGroupLabel(display, group.name);
            SetGroupPlayerNames(display, groupPlayers);
        }
    }

    private GameObject GetPrefabForGroupSize(int size)
    {
        return size switch
        {
            1 => onePlayerGroupPrefab,
            2 => twoPlayerGroupPrefab,
            3 => threePlayerGroupPrefab,
            _ => null
        };
    }

    private void SetGroupLabel(GameObject display, string label)
    {
        Transform labelTransform = display.transform.Find("Label");
        if (labelTransform == null) return;

        Transform titleTransform = labelTransform.Find("Title");
        if (titleTransform == null) return;

        var tmp = titleTransform.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = label;
    }

    private void SetGroupPlayerNames(GameObject display, List<Player> players)
    {
        string[] nameFieldNames = { "First Name Field", "Second Name Field", "Third Name Field" };

        for (int i = 0; i < players.Count && i < nameFieldNames.Length; i++)
        {
            SetPlayerNameField(display, nameFieldNames[i], players[i].name);
        }
    }

    private void SetPlayerNameField(GameObject display, string fieldName, string playerName)
    {
        Transform field = display.transform.Find(fieldName);
        if (field == null) return;

        Transform nameTransform = field.Find("Name");
        if (nameTransform == null) return;

        var tmp = nameTransform.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = playerName;
    }
}
