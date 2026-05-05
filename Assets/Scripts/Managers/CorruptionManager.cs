using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class Corruption
{
    public int id;
    public int assignedPlayerId = -1; // -1 means unassigned
    public string title;
    public string description;
    public string shortDescription;
    public int points;
    public int? neededCount; // How many times the player needs to achieve this objective, if applicable (null if not count-based)
    public int? achievedCount; // How many times the player has achieved this objective so far (null if not count-based)
    public bool completeted;
    public GameManager.CorruptionType type;
}

public class CorruptionManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CorruptionDatabase corruptionDatabase;

    public List<Corruption> allCorruptions = new List<Corruption>();
    public List<int> usedCorruptionIds = new List<int>(); // Track used corruptions by their IDs

    // Map from playerId -> assigned Corruption for the current round
    private Dictionary<int, Corruption> playerAssignments = new Dictionary<int, Corruption>();

    void Start()
    {
        LoadCorruptions();
    }

    public void LoadCorruptions()
    {
        allCorruptions = corruptionDatabase.LoadCorruptions();
        Debug.Log($"CorruptionManager: Loaded {allCorruptions.Count} corruptions.");
    }

    // -------------------- Assignment --------------------

    /// <summary>
    /// Assigns a random corruption to each non-DM player.
    /// The DM (lowest player ID) is excluded and gets the Civilian type.
    /// </summary>
    public void AssignCorruptionsToPlayers(Dictionary<int, Player> players, int dmId)
    {
        playerAssignments.Clear();

        foreach (var player in players.Values)
        {
            if (player.id == dmId)
            {
                // DM gets no corruption
                player.corruptionId = -1;
                continue;
            }

            // Pick a weighted random type: 40% Speech, 15% Interruption, 40% Civilian, 5% Betrayal
            float roll = Random.value;
            GameManager.CorruptionType randomType;
            if (roll < 0.4f)
                randomType = GameManager.CorruptionType.Speech;
            else if (roll < 0.55f)
                randomType = GameManager.CorruptionType.Interruption;
            else if (roll < 0.6f)
                randomType = GameManager.CorruptionType.Betrayal;
            else
            {
                // Civilian — no corruption
                player.corruptionId = -1;
                continue;
            }

            var objective = GetRandomUnusedCorruption(randomType);

            if (objective != null)
            {
                objective.assignedPlayerId = player.id;
                player.corruptionId = objective.id;
                playerAssignments[player.id] = objective;
                usedCorruptionIds.Add(objective.id);
                Debug.Log($"Assigned \"{objective.title}\" ({objective.type}) to player {player.name}");
            }
            else
            {
                Debug.LogWarning($"No unused corruption of type {randomType} available for player {player.name}.");
                player.corruptionId = -1;
            }
        }
    }

    // -------------------- Query --------------------

    public Corruption GetRandomUnusedCorruption(GameManager.CorruptionType type)
    {
        var candidates = allCorruptions
            .Where(o => o.type == type && !usedCorruptionIds.Contains(o.id))
            .ToList();

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"No unused corruptions of type {type} remaining.");
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>
    /// Returns the Corruption assigned to a given player for this round, or null.
    /// </summary>
    public Corruption GetCorruptionByPlayerId(int playerId)
    {
        if (playerAssignments.TryGetValue(playerId, out var objective))
            return objective;

        // Fallback: look up by player's stored corruptionId
        var player = GameManager.Instance.playerManager.players.ContainsKey(playerId)
            ? GameManager.Instance.playerManager.players[playerId]
            : null;

        if (player != null && player.corruptionId >= 0)
            return allCorruptions.FirstOrDefault(o => o.id == player.corruptionId);

        return null;
    }

    // -------------------- Reset --------------------

    public void ResetCorruptions()
    {
        usedCorruptionIds.Clear();
        playerAssignments.Clear();

        foreach (var objective in allCorruptions)
        {
            objective.assignedPlayerId = -1;
            objective.completeted = false;
            objective.achievedCount = null;
        }

        Debug.Log("All corruptions have been reset.");
    }
}
