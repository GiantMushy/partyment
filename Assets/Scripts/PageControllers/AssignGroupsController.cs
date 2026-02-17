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
    public int numberOfGroups;


    // Group assignment result: list of groups, each group is a list of player models
    private List<List<PlayerManager.PlayerModel>> assignedGroups = new List<List<PlayerManager.PlayerModel>>();
    private int dmId;

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void Next()
    {
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }

    public void Back()
    {
        gameManager.SetState(GameManager.GameState.StartLocalGame);
    }

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        AssignPlayersToRandomGroups();
        DisplayGroups();
    }

    // -------------------- Assignment Logic --------------------

    private void AssignPlayersToRandomGroups()
    {
        dmId = GetLowestPlayerId();
        AssignDM(dmId);
        var nonDMPlayers = GetShuffledNonDMPlayers(dmId);
        assignedGroups = SplitIntoGroups(nonDMPlayers);
        ApplyGroupAssignments();
    }

    private int GetLowestPlayerId()
    {
        return PlayerManager.players.Keys.Min();
    }

    private void AssignDM(int id)
    {
        if (PlayerManager.players.ContainsKey(id))
        {
            PlayerManager.UpdatePlayerGroup(id, PlayerManager.PlayerGroup.DM);
        }
    }

    private List<PlayerManager.PlayerModel> GetShuffledNonDMPlayers(int excludeId)
    {
        return PlayerManager.players.Values
            .Where(p => p.id != excludeId)
            .OrderBy(_ => Random.Range(0f, 1f))
            .ToList();
    }

    private List<List<PlayerManager.PlayerModel>> SplitIntoGroups(List<PlayerManager.PlayerModel> players)
    {
        var groups = new List<List<PlayerManager.PlayerModel>>();
        int index = 0;

        while (index < players.Count)
        {
            int remaining = players.Count - index;
            int groupSize = GetNextGroupSize(remaining);

            groups.Add(players.GetRange(index, groupSize));
            index += groupSize;
        }

        EnsureMinimumGroups(groups);
        return groups;
    }

    private void EnsureMinimumGroups(List<List<PlayerManager.PlayerModel>> groups, int minimum = 2)
    {
        while (groups.Count < minimum && groups.Count > 0 && groups[0].Count > 1)
        {
            var splitPlayer = groups[0][groups[0].Count - 1];
            groups[0].RemoveAt(groups[0].Count - 1);
            groups.Add(new List<PlayerManager.PlayerModel> { splitPlayer });
        }
    }

    private int GetNextGroupSize(int remainingPlayers)
    {
        // Avoid leaving exactly 4 remaining after this group, which would force a group of 4 or a lone group of 1 after a 3
        // Instead prefer (2, 2) over (3, 1)
        if (remainingPlayers == 4) return 2;
        if (remainingPlayers <= 3) return remainingPlayers;
        return 3;
    }

    private void ApplyGroupAssignments()
    {
        PlayerManager.PlayerGroup[] groupEnums = {
            PlayerManager.PlayerGroup.Group_1,
            PlayerManager.PlayerGroup.Group_2,
            PlayerManager.PlayerGroup.Group_3,
            PlayerManager.PlayerGroup.Group_4,
            PlayerManager.PlayerGroup.Group_5
        };

        for (int i = 0; i < assignedGroups.Count && i < groupEnums.Length; i++)
        {
            foreach (var player in assignedGroups[i])
            {
                PlayerManager.UpdatePlayerGroup(player.id, groupEnums[i]);
            }
        }

        numberOfGroups = assignedGroups.Count;
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
        for (int i = 0; i < assignedGroups.Count; i++)
        {
            var group = assignedGroups[i];
            GameObject prefab = GetPrefabForGroupSize(group.Count);
            if (prefab == null) continue;

            GameObject display = Instantiate(prefab, groupDisplayParent.transform);
            SetGroupLabel(display, $"Group {i + 1}");
            SetGroupPlayerNames(display, group);
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

    private void SetGroupPlayerNames(GameObject display, List<PlayerManager.PlayerModel> players)
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
