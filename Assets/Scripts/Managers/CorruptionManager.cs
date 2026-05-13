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
///   • 40% Speech
///   • 20% Interruption
///   •  5% Betrayal
///   • 35% Civilian (no objective; <c>player.corruptionId = -1</c>)
///
/// After assignment a coverage check ensures at least 25% (floor) of non-DM players have
/// an objective; skipped entirely when there are only 2 non-DM players (3-player game).
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

            // Speech=40%, Interruption=20%, Betrayal=5%, Civilian=35%
            float roll = Random.value;
            GameManager.CorruptionType randomType;
            if (roll < 0.40f)
                randomType = GameManager.CorruptionType.Speech;
            else if (roll < 0.60f)
                randomType = GameManager.CorruptionType.Interruption;
            else if (roll < 0.65f)
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

        EnsureMinimumCorruptionCoverage(players, dmId);
    }

    /// <summary>
    /// If fewer than 20% of non-DM players have an objective, back-fills random players
    /// until the threshold is met. Civilian is never assigned in this pass.
    /// </summary>
    private void EnsureMinimumCorruptionCoverage(Dictionary<int, Player> players, int dmId)
    {
        var nonDmPlayers = players.Values.Where(p => p.id != dmId).ToList();
        int total = nonDmPlayers.Count;
        if (total <= 2) return;

        int withCorruption = nonDmPlayers.Count(p => p.corruptionId >= 0);
        int targetCount = Mathf.FloorToInt(total * 0.25f);
        if (withCorruption >= targetCount) return;

        int needed = targetCount - withCorruption;
        Debug.Log($"CorruptionManager: Coverage {withCorruption}/{total} below 20% threshold — assigning {needed} more.");

        var candidates = nonDmPlayers
            .Where(p => p.corruptionId < 0)
            .OrderBy(_ => Random.value)
            .Take(needed)
            .ToList();

        // Non-civilian relative weights: Speech 40, Interruption 20, Betrayal 5 (total 65)
        GameManager.CorruptionType[] fallbackOrder = {
            GameManager.CorruptionType.Speech,
            GameManager.CorruptionType.Interruption,
            GameManager.CorruptionType.Betrayal,
        };

        foreach (var player in candidates)
        {
            bool isSoloGroup = player.group_id >= 0
                && players.Values.Count(p => p.group_id == player.group_id) <= 1;

            float roll = Random.value;
            GameManager.CorruptionType type;
            if (roll < (40f / 65f))
                type = GameManager.CorruptionType.Speech;
            else if (roll < (60f / 65f))
                type = GameManager.CorruptionType.Interruption;
            else
                type = GameManager.CorruptionType.Betrayal;

            var objective = GetRandomUnusedCorruption(type, isSoloGroup);

            if (objective == null)
            {
                foreach (var fallback in fallbackOrder)
                {
                    if (fallback == type) continue;
                    objective = GetRandomUnusedCorruption(fallback, isSoloGroup);
                    if (objective != null) break;
                }
            }

            if (objective == null)
            {
                Debug.LogWarning($"CorruptionManager: No unused corruption available for coverage back-fill of player {player.name}.");
                continue;
            }

            objective.assignedPlayerId = player.id;
            player.corruptionId = objective.id;
            playerAssignments[player.id] = objective;
            usedCorruptionIds.Add(objective.id);
            Debug.Log($"[Coverage] Assigned \"{objective.title}\" ({objective.type}) to player {player.name}");
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
