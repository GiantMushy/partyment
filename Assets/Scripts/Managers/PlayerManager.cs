using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class Player
{
    public int id;
    public string name;
    public int score = 0;
    public int group_id = -1; // -1 means unassigned
    public int secretObjectiveId = -1; // ID of the assigned secret objective, -1 if none
}

[System.Serializable]
public class Group
{
    public int id;
    public string name = "";
    public int score = 0;
    public GameManager.Position position; // For or Against
    public int secretObjectiveId = -1; // ID of the assigned secret objective, -1 if none
}

public class PlayerManager : MonoBehaviour
{
    public Dictionary<int, Player> players = new Dictionary<int, Player>();
    public int maxPlayers = 16;
    [SerializeField, Tooltip("Enter names of players here for testing purposes when development mode is ON")] private List<string> devModePlayerNames = new List<string>();
    [SerializeField, Tooltip("Visible representation of players in the Inspector")] 
    private List<Player> playersList = new List<Player>();

    public Dictionary<int, Group> groups = new Dictionary<int, Group>();
    private int nextGroupId = 0;
    [SerializeField, Tooltip("Visible representation of groups in the Inspector")]
    private List<Group> groupsList = new List<Group>();

    // -------------------- Player Management --------------------

    public void AddPlayer(int id, string name)
    {
        if (!players.ContainsKey(id))
        {
            players.Add(id, new Player { id = id, name = name});
            Debug.Log($"Added player {name} with ID {id}");
            SyncPlayersList();
        }
        else
        {
            Debug.LogWarning($"Player with ID {id} already exists.");
        }
    }

    public void RemovePlayer(int id)
    {
        if (players.ContainsKey(id))
        {
            players.Remove(id);
            Debug.Log($"Removed player with ID {id}");
            SyncPlayersList();
        }
        else
        {
            Debug.LogWarning($"Player with ID {id} does not exist.");
        }
    }

    public void UpdatePlayerGroup(int id, int newGroupId)
    {
        if (players.ContainsKey(id))
        {
            players[id].group_id = newGroupId;
            Debug.Log($"Updated player {players[id].name}'s group to {newGroupId}");
        }
        else
        {
            Debug.LogWarning($"Player with ID {id} does not exist.");
        }
    }

    // -------------------- Group Management --------------------

    public Group CreateGroup(string name = "")
    {
        int id = nextGroupId++;
        var group = new Group { id = id, name = name };
        groups.Add(id, group);
        Debug.Log($"Created group '{name}' with ID {id}");
        SyncGroupsList();
        return group;
    }

    public bool RemoveGroupById(int groupId)
    {
        if (groups.ContainsKey(groupId))
        {
            foreach (var player in players.Values)
            {
                if (player.group_id == groupId)
                    player.group_id = -1;
            }
            groups.Remove(groupId);
            Debug.Log($"Removed group with ID {groupId}");
            SyncGroupsList();
            SyncPlayersList();
            return true;
        }
        Debug.LogWarning($"Group with ID {groupId} does not exist.");
        return false;
    }

    public void ClearAllGroups()
    {
        groups.Clear();
        nextGroupId = 0;
        foreach (var player in players.Values)
            player.group_id = -1;
        Debug.Log("All groups have been cleared.");
        SyncGroupsList();
        SyncPlayersList();
    }

    public void SwapPosition(int groupId, GameManager.Position newPosition)
    {
        if (groups.ContainsKey(groupId))
        {
            groups[groupId].position = newPosition;
            Debug.Log($"Group {groups[groupId].name} is now in position {newPosition}");
        }
        else
        {
            Debug.LogWarning($"Group with ID {groupId} does not exist.");
        }
    }

    // -------------------- Query Functions --------------------

    public List<Player> GetPlayersWithGroupId(int groupId)
    {
        return players.Values.Where(p => p.group_id == groupId).ToList();
    }

    public Group GetGroupWithPlayerId(int playerId)
    {
        if (players.ContainsKey(playerId))
        {
            int groupId = players[playerId].group_id;
            if (groups.ContainsKey(groupId))
                return groups[groupId];
        }
        return null;
    }

    public int GetPlayerScore(int playerId)
    {
        if (players.ContainsKey(playerId))
            return players[playerId].score;
        Debug.LogWarning($"Player with ID {playerId} does not exist.");
        return 0;
    }

    public int GetGroupScore(int groupId)
    {
        if (groups.ContainsKey(groupId))
            return groups[groupId].score;
        Debug.LogWarning($"Group with ID {groupId} does not exist.");
        return 0;
    }

    public int GetGroupSecObjId(int groupId)
    {
        if (groups.ContainsKey(groupId))
            return groups[groupId].secretObjectiveId;
        Debug.LogWarning($"Group with ID {groupId} does not exist.");
        return -1;
    }

    public int GetPlayerSecObjId(int playerId)
    {
        if (players.ContainsKey(playerId))
            return players[playerId].secretObjectiveId;
        Debug.LogWarning($"Player with ID {playerId} does not exist.");
        return -1;
    }

    // -------------------- Dev Mode --------------------

    public void InitializeDevModePlayers()
    {
        if (devModePlayerNames.Count > 3)
        {
            Debug.Log("Initializing development mode players...");
            for (int i = 0; i < devModePlayerNames.Count; i++)
            {
                AddPlayer(i, devModePlayerNames[i]);
            }
        }
        else
        {
            Debug.Log("Not enough player names provided for development mode. Please add more names to the devModePlayerNames list.");
        }
    }

    private void SyncPlayersList()
    {
        playersList = players.Values.ToList();
    }

    private void SyncGroupsList()
    {
        groupsList = groups.Values.ToList();
    }

    private void OnValidate()
    {
        SyncPlayersList();
        SyncGroupsList();
    }

    public void ResetPlayerGroups()
    {
        ClearAllGroups();
        Debug.Log("All player groups have been reset.");
    }
}