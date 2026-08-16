using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class Player
{
    public int id;
    public string name;

    /// <summary>
    /// Personal running score for the current round. Combines corruption completed,
    /// stolen via accusation, penalty from incorrect accusation, and points lost when
    /// correctly accused. Reset by <see cref="PlayerManager.CommitRoundScores"/>.
    /// </summary>
    public int score = 0;

    /// <summary>
    /// Gross corruption earned this round via the toggle. Not decreased by accusations
    /// so the Scoreboard can display it as the corruption bar. Reset on round commit.
    /// </summary>
    public int roundCorruptionScore = 0;

    public int stolenScore = 0;
    public int penaltyScore = 0;

    /// <summary>
    /// Committed net earnings from all prior rounds. Used as the Scoreboard's
    /// animation start point from round 2 onward.
    /// </summary>
    public int oldScore = 0;

    public int group_id = -1;
    public int corruptionId = -1;
    public bool hasAccused = false;
    public bool isAccused = false;
}

/// <summary>
/// A debate group. Players reference their group by <see cref="id"/>; the DM is not a
/// member of any group. Per-round score fields are reset by
/// <see cref="PlayerManager.CommitRoundScores"/>.
/// </summary>
[System.Serializable]
public class Group
{
    public int id;
    public string name = "";

    /// <summary>
    /// Total round score for this group. Equals <c>voteScore + metric1Score + metric2Score</c>.
    /// </summary>
    public int score = 0;

    /// <summary>Points from the group-voting phase ranking (1st/2nd/3rd place).</summary>
    public int voteScore = 0;

    /// <summary>Points awarded by the DM for the first chosen metric this round.</summary>
    public int metric1Score = 0;

    /// <summary>Points awarded by the DM for the second chosen metric this round.</summary>
    public int metric2Score = 0;

    public GameManager.Position position;
    public int corruptionId = -1;
    public int votingPhasePoints = 0;
}

/// <summary>
/// Owns the game's <see cref="Player"/> and <see cref="Group"/> dictionaries. The DM
/// defaults to the player with the lowest ID unless overridden via <see cref="dmId"/>.
/// Every score field on Player and Group is per-round and reset by
/// <see cref="CommitRoundScores"/>; only <see cref="Player.oldScore"/> accumulates
/// across rounds. Score mutations go through <see cref="AddRoundCorruptionScore"/>,
/// <see cref="AddStolenScore"/>, and <see cref="AddPenaltyScore"/> to keep the
/// breakdown bars on the Scoreboard in sync.
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public Dictionary<int, Player> players = new Dictionary<int, Player>();
    public int maxPlayers = 16;
    [SerializeField, Tooltip("Enter names of players here for testing purposes when development mode is ON")] private List<string> devModePlayerNames = new List<string>();
    [SerializeField, Tooltip("Visible representation of players in the Inspector")] 
    private List<Player> playersList = new List<Player>();

    public Dictionary<int, Group> groups = new Dictionary<int, Group>();
    private int nextGroupId = 0;

    /// <summary>
    /// Explicitly assigned DM player ID. Falls back to the lowest player ID when unset.
    /// </summary>
    private int _dmId = -1;
    public int dmId
    {
        get => _dmId >= 0 && players.ContainsKey(_dmId) ? _dmId : (players.Count > 0 ? players.Keys.Min() : -1);
        set => _dmId = value;
    }

    [SerializeField, Tooltip("Visible representation of groups in the Inspector")]
    private List<Group> groupsList = new List<Group>();

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
        _dmId = -1;
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

    public int GetGroupCorruptionId(int groupId)
    {
        if (groups.ContainsKey(groupId))
            return groups[groupId].corruptionId;
        Debug.LogWarning($"Group with ID {groupId} does not exist.");
        return -1;
    }

    public int GetPlayerCorruptionId(int playerId)
    {
        if (players.ContainsKey(playerId))
            return players[playerId].corruptionId;
        Debug.LogWarning($"Player with ID {playerId} does not exist.");
        return -1;
    }

    /// <summary>Adds <paramref name="amount"/> to the player's score.</summary>
    public void AddScore(int playerId, int amount)
    {
        if (!players.ContainsKey(playerId)) { Debug.LogWarning($"AddScore: Player {playerId} not found."); return; }
        players[playerId].score += amount;
        SyncPlayersList();
    }

    /// <summary>Subtracts <paramref name="amount"/> from the player's score; may go negative.</summary>
    public void SubtractScore(int playerId, int amount)
    {
        if (!players.ContainsKey(playerId)) { Debug.LogWarning($"SubtractScore: Player {playerId} not found."); return; }
        players[playerId].score -= amount;
        SyncPlayersList();
    }

    /// <summary>
    /// Adds <paramref name="amount"/> to both <c>roundCorruptionScore</c> and total
    /// <c>score</c>. Called when a player toggles their corruption on.
    /// </summary>
    public void AddRoundCorruptionScore(int playerId, int amount)
    {
        if (!players.ContainsKey(playerId)) { Debug.LogWarning($"AddRoundCorruptionScore: Player {playerId} not found."); return; }
        players[playerId].roundCorruptionScore += amount;
        players[playerId].score                += amount;
        SyncPlayersList();
    }

    /// <summary>
    /// Subtracts <paramref name="amount"/> from both <c>roundCorruptionScore</c> and
    /// total <c>score</c>. Called when a player toggles their corruption off.
    /// </summary>
    public void SubtractRoundCorruptionScore(int playerId, int amount)
    {
        if (!players.ContainsKey(playerId)) { Debug.LogWarning($"SubtractRoundCorruptionScore: Player {playerId} not found."); return; }
        players[playerId].roundCorruptionScore -= amount;
        players[playerId].score                -= amount;
        SyncPlayersList();
    }

    /// <summary>
    /// Records <paramref name="amount"/> as stolen points for the accusing player and
    /// adds them to that player's score. Does not deduct from the accused player;
    /// <see cref="SubtractScore"/> must be called separately on the accused player.
    /// </summary>
    public void AddStolenScore(int accusingPlayerId, int amount)
    {
        if (!players.ContainsKey(accusingPlayerId)) { Debug.LogWarning($"AddStolenScore: Player {accusingPlayerId} not found."); return; }
        players[accusingPlayerId].stolenScore += amount;
        players[accusingPlayerId].score       += amount;
        SyncPlayersList();
    }

    /// <summary>
    /// Marks <paramref name="playerId"/> as successfully accused this round,
    /// preventing their corruption toggle from awarding further points.
    /// </summary>
    public void SetPlayerAccused(int playerId)
    {
        if (!players.ContainsKey(playerId)) { Debug.LogWarning($"SetPlayerAccused: Player {playerId} not found."); return; }
        players[playerId].isAccused = true;
        SyncPlayersList();
    }

    /// <summary>
    /// Records <paramref name="amount"/> as a penalty for an incorrect accusation and
    /// deducts it from the player's score.
    /// </summary>
    public void AddPenaltyScore(int playerId, int amount)
    {
        if (!players.ContainsKey(playerId)) { Debug.LogWarning($"AddPenaltyScore: Player {playerId} not found."); return; }
        players[playerId].penaltyScore += amount;
        players[playerId].score        -= amount;
        SyncPlayersList();
    }

    /// <summary>
    /// Folds the current round's earnings into each player's <c>oldScore</c> and resets
    /// all per-round counters on both players and groups. Called once per round, after
    /// the Scoreboard is dismissed and before the next round's setup begins.
    /// </summary>
    public void CommitRoundScores()
    {
        foreach (var p in players.Values)
        {
            int groupScore = (p.group_id >= 0 && groups.ContainsKey(p.group_id)) ? groups[p.group_id].score : 0;
            p.oldScore += groupScore + p.score;

            p.score                = 0;
            p.roundCorruptionScore = 0;
            p.stolenScore          = 0;
            p.penaltyScore         = 0;
        }

        foreach (var g in groups.Values)
        {
            g.score             = 0;
            g.voteScore         = 0;
            g.metric1Score      = 0;
            g.metric2Score      = 0;
            g.votingPhasePoints = 0;
        }

        SyncPlayersList();
        SyncGroupsList();
        Debug.Log("Round scores committed to oldScore; per-round counters reset.");
    }

    /// <summary>
    /// A player's running total for the whole game right now, including the current
    /// (not-yet-committed) round: committed <see cref="Player.oldScore"/> + their group's
    /// round score + their personal round net. Matches the value the Scoreboard animates to.
    /// </summary>
    public int GetPlayerGameTotal(Player p)
    {
        if (p == null) return 0;
        int groupScore = (p.group_id >= 0 && groups.ContainsKey(p.group_id)) ? groups[p.group_id].score : 0;
        return p.oldScore + groupScore + p.score;
    }

    /// <summary>
    /// True when any non-DM player has reached <paramref name="threshold"/> points this
    /// game (counting the current round). Drives the "first to N ends the game" condition.
    /// </summary>
    public bool HasAnyPlayerReachedScore(int threshold)
    {
        foreach (var p in players.Values)
        {
            if (p.id == dmId) continue;
            if (GetPlayerGameTotal(p) >= threshold) return true;
        }
        return false;
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

    /// <summary>
    /// Wipes all score state on every player and group, including the committed
    /// <c>oldScore</c>. Called when starting a new game.
    /// </summary>
    public void ResetAllScores()
    {
        foreach (var p in players.Values)
        {
            p.score                = 0;
            p.roundCorruptionScore = 0;
            p.stolenScore          = 0;
            p.penaltyScore         = 0;
            p.oldScore             = 0;
        }
        foreach (var g in groups.Values)
        {
            g.score             = 0;
            g.voteScore         = 0;
            g.metric1Score      = 0;
            g.metric2Score      = 0;
            g.votingPhasePoints = 0;
        }
        SyncPlayersList();
        SyncGroupsList();
        Debug.Log("All player and group scores reset for new game.");
    }

    /// <summary>Clears per-round accusation flags at the start of a new round.</summary>
    public void ResetAccusations()
    {
        foreach (var player in players.Values)
        {
            player.hasAccused = false;
            player.isAccused  = false;
        }
        Debug.Log("All player accusation flags have been reset.");
    }
}