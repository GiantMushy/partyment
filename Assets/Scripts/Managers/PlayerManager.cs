using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class Player
{
    public int id;
    public string name;

    /// <summary>
    /// Personal running score for the CURRENT round.
    /// Includes: +corruption completed, +stolen via accusation, −penalty from incorrect accusation,
    /// −points lost when correctly accused. Group score is tracked separately on the Group object.
    /// Reset to 0 by <see cref="PlayerManager.CommitRoundScores"/> at the end of each round.
    /// </summary>
    public int score = 0;

    /// <summary>
    /// Corruption earned this round via the toggle. Never decreased by accusations, so it
    /// can be displayed as the gross "Corruption Score" bar on the Scoreboard. Reset on round commit.
    /// </summary>
    public int roundCorruptionScore = 0;

    public int stolenScore = 0;  // Points earned by successfully accusing another player (per-round, reset on commit)
    public int penaltyScore = 0; // Points lost from incorrect accusations (per-round, reset on commit)

    /// <summary>
    /// Sum of all *committed* prior rounds' net earnings (group + corruption + stolen − penalty − accusedLoss).
    /// Used as the animation start point for the Scoreboard in rounds 2+.
    /// </summary>
    public int oldScore = 0;

    public int group_id = -1; // -1 means unassigned
    public int corruptionId = -1; // ID of the assigned corruption, -1 if none
    public bool hasAccused = false; // True once this player has made an accusation this round
    public bool isAccused = false;  // True once this player has been successfully accused this round
}

/// <summary>
/// One debate group. Players reference their group by <see cref="id"/>; the DM is NOT in
/// any group. <see cref="score"/> and <see cref="votingPhasePoints"/> are per-round values —
/// reset by <see cref="PlayerManager.CommitRoundScores"/>.
/// </summary>
[System.Serializable]
public class Group
{
    public int id;
    public string name = "";

    /// <summary>
    /// Total round score for this group. Always equal to <c>voteScore + metric1Score + metric2Score</c>;
    /// kept as a stand-alone field so existing code paths (commit, queries) need no changes.
    /// </summary>
    public int score = 0;

    /// <summary>Points earned this round from the group-voting phase ranking (1st/2nd/3rd place).</summary>
    public int voteScore = 0;

    /// <summary>Points awarded by the DM for the FIRST chosen metric this round.</summary>
    public int metric1Score = 0;

    /// <summary>Points awarded by the DM for the SECOND chosen metric this round.</summary>
    public int metric2Score = 0;

    public GameManager.Position position; // For or Against
    public int corruptionId = -1; // ID of the assigned corruption, -1 if none
    public int votingPhasePoints = 0; // Accumulated local vote points during the voting phase
}

/// <summary>
/// Owns the game's <see cref="Player"/> and <see cref="Group"/> dictionaries. The DM is
/// always the player with the lowest ID unless explicitly overridden via <see cref="dmId"/>.
///
/// Score model: every score field on Player and Group is per-round and zeroed by
/// <see cref="CommitRoundScores"/> at the end of each round; the only field that
/// accumulates across rounds is <see cref="Player.oldScore"/>. See CLAUDE.md → Scoring
/// for the full breakdown and the helper methods that keep <c>score</c> /
/// <c>roundCorruptionScore</c> / <c>stolenScore</c> / <c>penaltyScore</c> consistent.
///
/// Always mutate scores through the helper methods (<see cref="AddRoundCorruptionScore"/>,
/// <see cref="AddStolenScore"/>, <see cref="AddPenaltyScore"/>) — bypassing them desyncs
/// the breakdown bars on the Scoreboard.
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

    // -------------------- Score Manipulation --------------------

    /// <summary>Adds <paramref name="amount"/> to the player's regular score.</summary>
    public void AddScore(int playerId, int amount)
    {
        if (!players.ContainsKey(playerId)) { Debug.LogWarning($"AddScore: Player {playerId} not found."); return; }
        players[playerId].score += amount;
        SyncPlayersList();
    }

    /// <summary>Subtracts <paramref name="amount"/> from the player's regular score (can go negative).</summary>
    public void SubtractScore(int playerId, int amount)
    {
        if (!players.ContainsKey(playerId)) { Debug.LogWarning($"SubtractScore: Player {playerId} not found."); return; }
        players[playerId].score -= amount;
        SyncPlayersList();
    }

    /// <summary>
    /// Adds <paramref name="amount"/> to BOTH the player's <c>roundCorruptionScore</c> and total <c>score</c>.
    /// Use when the player completes their corruption (toggle on).
    /// </summary>
    public void AddRoundCorruptionScore(int playerId, int amount)
    {
        if (!players.ContainsKey(playerId)) { Debug.LogWarning($"AddRoundCorruptionScore: Player {playerId} not found."); return; }
        players[playerId].roundCorruptionScore += amount;
        players[playerId].score                += amount;
        SyncPlayersList();
    }

    /// <summary>
    /// Subtracts <paramref name="amount"/> from BOTH the player's <c>roundCorruptionScore</c> and total <c>score</c>.
    /// Use when the player un-toggles their corruption (toggle off).
    /// </summary>
    public void SubtractRoundCorruptionScore(int playerId, int amount)
    {
        if (!players.ContainsKey(playerId)) { Debug.LogWarning($"SubtractRoundCorruptionScore: Player {playerId} not found."); return; }
        players[playerId].roundCorruptionScore -= amount;
        players[playerId].score                -= amount;
        SyncPlayersList();
    }

    /// <summary>
    /// Records <paramref name="amount"/> as stolen points for <paramref name="accusingPlayerId"/> and
    /// adds them to that player's regular score. Does NOT deduct from the accused player — call
    /// <see cref="SubtractScore"/> separately on the accused player before calling this.
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
    /// Records <paramref name="amount"/> as a penalty for <paramref name="playerId"/> (incorrect accusation)
    /// and deducts it from their score.
    /// </summary>
    public void AddPenaltyScore(int playerId, int amount)
    {
        if (!players.ContainsKey(playerId)) { Debug.LogWarning($"AddPenaltyScore: Player {playerId} not found."); return; }
        players[playerId].penaltyScore += amount;
        players[playerId].score        -= amount;
        SyncPlayersList();
    }

    /// <summary>
    /// Folds the current round's earnings into each player's <c>oldScore</c> and resets all
    /// per-round counters (player score / roundCorruptionScore / stolenScore / penaltyScore,
    /// and each Group.score / votingPhasePoints). Call once per round, AFTER the Scoreboard
    /// is dismissed and BEFORE the new round's setup begins.
    /// </summary>
    public void CommitRoundScores()
    {
        // Per-player: roll up this round's net into oldScore, then zero out per-round fields.
        foreach (var p in players.Values)
        {
            int groupScore = (p.group_id >= 0 && groups.ContainsKey(p.group_id)) ? groups[p.group_id].score : 0;
            p.oldScore += groupScore + p.score;

            p.score                = 0;
            p.roundCorruptionScore = 0;
            p.stolenScore          = 0;
            p.penaltyScore         = 0;
        }

        // Per-group: reset accumulator fields (including the breakdown components).
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

    /// <summary>
    /// Wipes ALL score state on every player and group — both per-round counters and
    /// committed <c>oldScore</c>. Call when starting a brand-new game.
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

    /// <summary>Clears per-round accusation flags. Call at the start of each new round.</summary>
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