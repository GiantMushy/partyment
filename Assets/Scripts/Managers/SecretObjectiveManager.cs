using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class SecretObjective
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
    public GameManager.SecretObjectiveType type;
}

public class SecretObjectiveManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SecretObjectiveDatabase secretObjectiveDatabase;

    public List<SecretObjective> allSecretObjectives = new List<SecretObjective>();
    public List<int> usedSecretObjectiveIds = new List<int>(); // Track used objectives by their IDs

    // Map from playerId -> assigned SecretObjective for the current round
    private Dictionary<int, SecretObjective> playerAssignments = new Dictionary<int, SecretObjective>();

    void Start()
    {
        LoadSecretObjectives();
    }

    public void LoadSecretObjectives()
    {
        allSecretObjectives = secretObjectiveDatabase.LoadDevSecretObjectives();
        Debug.Log($"SecretObjectiveManager: Loaded {allSecretObjectives.Count} secret objectives.");
    }

    // -------------------- Assignment --------------------

    /// <summary>
    /// Assigns a random secret objective to each non-DM player.
    /// The DM (lowest player ID) is excluded and gets the Civilian type.
    /// </summary>
    public void AssignSecretObjectivesToPlayers(Dictionary<int, Player> players, int dmId)
    {
        playerAssignments.Clear();

        foreach (var player in players.Values)
        {
            if (player.id == dmId)
            {
                // DM gets no secret objective
                player.secretObjectiveId = -1;
                continue;
            }

            // Pick a weighted random type: 40% Speech, 15% Interruption, 40% Civilian, 5% Betrayal
            float roll = Random.value;
            GameManager.SecretObjectiveType randomType;
            if (roll < 0.40f)
                randomType = GameManager.SecretObjectiveType.Speech;
            else if (roll < 0.55f)
                randomType = GameManager.SecretObjectiveType.Interruption;
            else if (roll < 0.95f)
            {
                // Civilian — no secret objective, same as DM
                player.secretObjectiveId = -1;
                continue;
            }
            else
                randomType = GameManager.SecretObjectiveType.Betrayal;

            var objective = GetRandomUnusedSecretObjective(randomType);

            if (objective != null)
            {
                objective.assignedPlayerId = player.id;
                player.secretObjectiveId = objective.id;
                playerAssignments[player.id] = objective;
                usedSecretObjectiveIds.Add(objective.id);
                Debug.Log($"Assigned \"{objective.title}\" ({objective.type}) to player {player.name}");
            }
            else
            {
                Debug.LogWarning($"No unused secret objective of type {randomType} available for player {player.name}.");
                player.secretObjectiveId = -1;
            }
        }
    }

    // -------------------- Query --------------------

    public SecretObjective GetRandomUnusedSecretObjective(GameManager.SecretObjectiveType type)
    {
        var candidates = allSecretObjectives
            .Where(o => o.type == type && !usedSecretObjectiveIds.Contains(o.id))
            .ToList();

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"No unused secret objectives of type {type} remaining.");
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>
    /// Returns the SecretObjective assigned to a given player for this round, or null.
    /// </summary>
    public SecretObjective GetSecretObjectiveByPlayerId(int playerId)
    {
        if (playerAssignments.TryGetValue(playerId, out var objective))
            return objective;

        // Fallback: look up by player's stored secretObjectiveId
        var player = GameManager.Instance.playerManager.players.ContainsKey(playerId)
            ? GameManager.Instance.playerManager.players[playerId]
            : null;

        if (player != null && player.secretObjectiveId >= 0)
            return allSecretObjectives.FirstOrDefault(o => o.id == player.secretObjectiveId);

        return null;
    }

    // -------------------- Reset --------------------

    public void ResetSecretObjectives()
    {
        usedSecretObjectiveIds.Clear();
        playerAssignments.Clear();

        foreach (var objective in allSecretObjectives)
        {
            objective.assignedPlayerId = -1;
            objective.completeted = false;
            objective.achievedCount = null;
        }

        Debug.Log("All secret objectives have been reset.");
    }
}