using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// One secret-objective row loaded from <c>Corruptions.csv</c>. The DM never sees Betrayal
/// objectives on screen (excluded by <see cref="DMDisplayController.InstantiateCorruptionCards"/>).
/// </summary>
[System.Serializable]
public class Corruption
{
    public int id;
    public int assignedPlayerId = -1; // -1 means unassigned
    public string title;
    public string titleIs;
    public string description;
    public string descriptionIs;
    public string shortDescription;
    public string shortDescriptionIs;
    public int points;
    public int? neededCount;
    public int? achievedCount;
    public bool completeted;
    public GameManager.CorruptionType type;
    public bool requiresTeammate;
    /// <summary>True when completing this betrayal requires the player's group to receive zero vote points this round.</summary>
    public bool requiresZeroGroupVotes;
}

/// <summary>
/// Owns the master list of <see cref="Corruption"/> objectives loaded from CSV via
/// <see cref="CorruptionDatabase"/>, and the per-round weighted-random assignment
/// to non-DM players.
///
/// Type weights (per non-DM player):
///   • 42% Speech
///   • 15% Interruption
///   •  3% Betrayal
///   • 40% Civilian (no objective; <c>player.corruptionId = -1</c>)
///
/// Used IDs are tracked across rounds in <see cref="usedCorruptionIds"/> so the same
/// objective never appears twice in a single game. Reset between games via
/// <see cref="ResetCorruptions"/>.
/// </summary>
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

            // Civilian=40%, Speech=42%, Interruption=15%, Betrayal=3%
            float roll = Random.value;
            GameManager.CorruptionType randomType;
            if (roll < 0.42f)
                randomType = GameManager.CorruptionType.Speech;
            else if (roll < 0.57f)
                randomType = GameManager.CorruptionType.Interruption;
            else if (roll < 0.60f)
                randomType = GameManager.CorruptionType.Betrayal;
            else
            {
                // Civilian — no corruption
                player.corruptionId = -1;
                continue;
            }

            // Solo group players cannot receive corruptions that require a teammate
            bool isSoloGroup = player.group_id >= 0
                && players.Values.Count(p => p.group_id == player.group_id) <= 1;

            var objective = GetRandomUnusedCorruption(randomType, isSoloGroup);

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

    public Corruption GetRandomUnusedCorruption(GameManager.CorruptionType type, bool soloPlayer = false)
    {
        var candidates = allCorruptions
            .Where(o => o.type == type && !usedCorruptionIds.Contains(o.id))
            .Where(o => !soloPlayer || !o.requiresTeammate)
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
