using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{

    [HideInInspector] public enum PlayerGroup { Unassigned, DM, Group_1, Group_2, Group_3, Group_4, Group_5}
    public Dictionary<int, PlayerModel> players = new Dictionary<int, PlayerModel>();
    [SerializeField, Tooltip("Enter names of players here for testing purposes when development mode is ON")] private List<string> devModePlayerNames = new List<string>();

    public class PlayerModel
    {
        public int id;
        public string name;
        public Color favouredColor;
        public PlayerGroup group;
    }

    public void AddPlayer(int id, string name, Color favouredColor = default, PlayerGroup group = PlayerGroup.Unassigned)
    {
        if (!players.ContainsKey(id))
        {
            if (favouredColor == default) favouredColor = Color.white;
            players.Add(id, new PlayerModel { id = id, name = name, favouredColor = favouredColor, group = group });
            Debug.Log($"Added player {name} with ID {id}");
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
        }
        else
        {
            Debug.LogWarning($"Player with ID {id} does not exist.");
        }
    }

    public void UpdatePlayerGroup(int id, PlayerGroup newGroup)
    {
        if (players.ContainsKey(id))
        {
            players[id].group = newGroup;
            Debug.Log($"Updated player {players[id].name}'s group to {newGroup}");
        }
        else
        {
            Debug.LogWarning($"Player with ID {id} does not exist.");
        }
    }

    public void UpdatePlayerColor(int id, Color newColor)
    {
        if (players.ContainsKey(id))
        {
            players[id].favouredColor = newColor;
            Debug.Log($"Updated player {players[id].name}'s favoured color.");
        }
        else
        {
            Debug.LogWarning($"Player with ID {id} does not exist.");
        }
    }

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
}