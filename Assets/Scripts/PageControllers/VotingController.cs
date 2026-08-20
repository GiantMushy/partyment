using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the Voting screen with click and drag-and-drop interactions. Operates in
/// two phases: GroupVoting, where each group ranks the other groups, and
/// DMMetricVoting, where the DM assigns two metric awards. In DM voting the same
/// group may be picked for both metrics, so a clone is spawned when an original card
/// enters a slot and destroyed when removed.
/// </summary>
public class VotingController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;

    [Header("Scoring")]
    public int firstPlacePoints = 3;
    public int secondPlacePoints = 2;
    public int thirdPlacePoints = 1;
    [Header("Local Vote Points (per individual group vote)")]
    public int localFirstPlacePoints = 3;
    public int localSecondPlacePoints = 2;
    public int localThirdPlacePoints = 1;
    public int metricPoints = 3;

    [Header("UI Elements")]
    [SerializeField] private GameObject nextButton;
    [SerializeField] private Image number1;
    [SerializeField] private Image number2;
    [SerializeField] private Image number3;
    [SerializeField] private Image metric1Image;
    [SerializeField] private Image metric2Image;
    [SerializeField] private Sprite comedySprite;
    [SerializeField] private Sprite creativitySprite;
    [SerializeField] private Sprite onTopicSprite;
    [SerializeField] private Sprite factualSprite;
    [SerializeField] private Sprite enthusiasmSprite;

    [Header("Vote Slots")]
    [Tooltip("World-position anchor for the card placed in slot 1.")]
    [SerializeField] private Transform firstVoteSlot;
    [Tooltip("World-position anchor for the card placed in slot 2.")]
    [SerializeField] private Transform secondVoteSlot;
    [Tooltip("World-position anchor for the card placed in slot 3.")]
    [SerializeField] private Transform thirdVoteSlot;
    [Tooltip("Placeholder shown when slot 1 has no card.")]
    [SerializeField] private GameObject firstVoteEmpty;
    [Tooltip("Placeholder shown when slot 2 has no card.")]
    [SerializeField] private GameObject secondVoteEmpty;
    [Tooltip("Placeholder shown when slot 3 has no card.")]
    [SerializeField] private GameObject thirdVoteEmpty;

    [Header("Drop Targets")]
    [SerializeField] private VotingSlotDropTarget firstSlotDropTarget;
    [SerializeField] private VotingSlotDropTarget secondSlotDropTarget;
    [SerializeField] private VotingSlotDropTarget thirdSlotDropTarget;
    [SerializeField] private VotingGridDropTarget gridDropTarget;

    [Header("Group Buttons")]
    [SerializeField] private GameObject group1Button;
    [SerializeField] private GameObject group2Button;
    [SerializeField] private GameObject group3Button;
    [SerializeField] private GameObject group4Button;
    [SerializeField] private GameObject group5Button;
    [SerializeField] private GameObject group6Button;

    [Header("Navigation")]
    [Tooltip("Top-level RectTransform on the Canvas; used to parent drag ghosts above all UI.")]
    public RectTransform dragLayer;

    public enum VotingPhase { GroupVoting, DMMetricVoting }
    private VotingPhase currentPhase;
    private int maxSelections;

    private GameObject[] allGroupButtons;
    private List<Group> activeGroups = new List<Group>();

    /// <summary>The group currently casting votes. Excluded from its own ballot so a group
    /// cannot vote for itself. Null during DM metric voting.</summary>
    private Group currentVotingGroup;

    /// <summary>
    /// slotOccupants[0..2] = handler in vote slot 1/2/3. null = empty.
    /// During DM voting only slots 0 and 1 are used.
    /// </summary>
    private readonly VotingDragHandler[] slotOccupants = new VotingDragHandler[3];

    /// <summary>
    /// DM-mode clones, keyed by the original handler's slotIndex. When the DM places a
    /// group in a slot, a clone is spawned at the home position so the same group can
    /// be picked again for the other slot.
    /// </summary>
    private readonly Dictionary<int, VotingDragHandler> dmClones = new Dictionary<int, VotingDragHandler>();

    private bool isInitialized;

    void Start()
    {
        gameManager = GameManager.Instance;
        InitButtonArray();
        InitializeHandlers();
        isInitialized = true;
    }

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (allGroupButtons == null) { InitButtonArray(); InitializeHandlers(); isInitialized = true; }
        SetupUI();
    }

    private void InitButtonArray()
    {
        allGroupButtons = new GameObject[]
        {
            group1Button, group2Button, group3Button,
            group4Button, group5Button, group6Button
        };
    }

    public void PrepareForGroupVoting(Group votingGroup)
    {
        currentPhase = VotingPhase.GroupVoting;
        currentVotingGroup = votingGroup;
    }

    public void PrepareForDMMetricVoting()
    {
        currentPhase = VotingPhase.DMMetricVoting;
        currentVotingGroup = null;
    }

    /// <summary>
    /// Wires every group button's VotingDragHandler with controller references and
    /// initializes the drop targets.
    /// </summary>
    public void InitializeHandlers()
    {
        for (int i = 0; i < allGroupButtons.Length; i++)
        {
            var btn = allGroupButtons[i];
            if (btn == null) continue;

            var handler = btn.GetComponent<VotingDragHandler>();
            if (handler == null)
            {
                Debug.LogWarning(
                    $"[VotingController] '{btn.name}' has no VotingDragHandler component; skipping.");
                continue;
            }

            handler.controller   = this;
            handler.slotIndex    = i;
            handler.homePosition = btn.transform.position;
        }

        if (firstSlotDropTarget  != null) { firstSlotDropTarget.controller  = this; firstSlotDropTarget.slotIndex  = 1; }
        if (secondSlotDropTarget != null) { secondSlotDropTarget.controller = this; secondSlotDropTarget.slotIndex = 2; }
        if (thirdSlotDropTarget  != null) { thirdSlotDropTarget.controller  = this; thirdSlotDropTarget.slotIndex  = 3; }
        if (gridDropTarget       != null)   gridDropTarget.controller       = this;

        SetupPlaceholderProxy(firstVoteEmpty,  1);
        SetupPlaceholderProxy(secondVoteEmpty, 2);
        SetupPlaceholderProxy(thirdVoteEmpty,  3);
    }

    private void SetupPlaceholderProxy(GameObject placeholder, int oneBasedSlotIndex)
    {
        if (placeholder == null) return;
        var proxy = placeholder.GetComponent<VotingSlotDropProxy>();
        if (proxy == null) proxy = placeholder.AddComponent<VotingSlotDropProxy>();
        proxy.controller = this;
        proxy.slotIndex  = oneBasedSlotIndex;
    }

    private void SetupUI()
    {
        ClearAllSelections();

        activeGroups = PlayerManager.groups.Values
            .OrderBy(g => g.id)
            .ToList();

        // A group cannot vote for itself, so drop it from its own list of choices.
        if (currentPhase == VotingPhase.GroupVoting && currentVotingGroup != null)
            activeGroups.RemoveAll(g => g.id == currentVotingGroup.id);

        int groupCount = activeGroups.Count;

        for (int i = 0; i < allGroupButtons.Length; i++)
        {
            if (i < groupCount)
            {
                allGroupButtons[i].SetActive(true);
                var label = allGroupButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = activeGroups[i].name;

                // Re-caches home positions in case the layout has changed.
                var handler = allGroupButtons[i].GetComponent<VotingDragHandler>();
                if (handler != null)
                {
                    handler.homePosition = allGroupButtons[i].transform.position;
                    handler.slotIndex = i;
                }
            }
            else
            {
                allGroupButtons[i].SetActive(false);
            }
        }

        if (currentPhase == VotingPhase.GroupVoting)
            SetupGroupVotingUI(groupCount);
        else
            SetupDMMetricVotingUI();

        UpdatePlaceholders();
        UpdateNextButton();
    }

    private void SetupGroupVotingUI(int groupCount)
    {
        maxSelections = groupCount <= 2 ? 2 : 3;

        if (firstVoteEmpty  != null) firstVoteEmpty.SetActive(true);
        if (secondVoteEmpty != null) secondVoteEmpty.SetActive(true);
        if (thirdVoteEmpty  != null) thirdVoteEmpty.SetActive(groupCount > 2);

        if (thirdSlotDropTarget != null)
            thirdSlotDropTarget.gameObject.SetActive(groupCount > 2);

        if (number1 != null) number1.gameObject.SetActive(true);
        if (number2 != null) number2.gameObject.SetActive(true);
        if (number3 != null) number3.gameObject.SetActive(groupCount > 2);

        if (metric1Image != null) metric1Image.gameObject.SetActive(false);
        if (metric2Image != null) metric2Image.gameObject.SetActive(false);
    }

    private void SetupDMMetricVotingUI()
    {
        maxSelections = 2;

        if (firstVoteEmpty  != null) firstVoteEmpty.SetActive(true);
        if (secondVoteEmpty != null) secondVoteEmpty.SetActive(true);
        if (thirdVoteEmpty  != null) thirdVoteEmpty.SetActive(false);

        if (thirdSlotDropTarget != null)
            thirdSlotDropTarget.gameObject.SetActive(false);

        if (number1 != null) number1.gameObject.SetActive(false);
        if (number2 != null) number2.gameObject.SetActive(false);
        if (number3 != null) number3.gameObject.SetActive(false);

        var metrics = gameManager.selectedMetrics;
        if (metric1Image != null && metrics.Count > 0)
        {
            metric1Image.sprite = GetMetricSprite(metrics[0]);
            metric1Image.gameObject.SetActive(true);
        }
        if (metric2Image != null && metrics.Count > 1)
        {
            metric2Image.sprite = GetMetricSprite(metrics[1]);
            metric2Image.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Toggles a card between the grid and the topmost free vote slot. Invoked by
    /// <see cref="VotingDragHandler"/> on a short click.
    /// </summary>
    public void OnGroupClicked(VotingDragHandler handler)
    {
        int slotIdx = IndexOf(handler);

        if (slotIdx >= 0)
        {
            MoveToGrid(handler);
        }
        else
        {
            int emptySlot = FirstEmptySlot();
            if (emptySlot >= 0) MoveToSlot(handler, emptySlot);
        }

        UpdatePlaceholders();
        UpdateNextButton();
    }

    public void OnDragBegin(VotingDragHandler handler) { }

    /// <summary>
    /// Snaps the card back to its logical position when a drag ends with no valid
    /// drop target.
    /// </summary>
    public void ReturnToHome(VotingDragHandler handler)
    {
        int slotIdx = IndexOf(handler);
        handler.transform.position = slotIdx >= 0
            ? GetSlotWorldPosition(slotIdx)
            : handler.homePosition;
    }

    /// <summary>
    /// Places <paramref name="handler"/> into a 1-based <paramref name="slotIndex"/>.
    /// Group voting forbids duplicates: dropping onto an occupied slot from another
    /// slot swaps, dropping from the grid displaces the occupant back to the grid.
    /// DM metric voting allows the same group in both slots and uses clones to
    /// represent the duplicate.
    /// </summary>
    public void PlaceGroupInSlot(VotingDragHandler handler, int slotIndex)
    {
        int idx      = slotIndex - 1;
        int handlerCurrentSlot = IndexOf(handler);

        VotingDragHandler targetOccupant = slotOccupants[idx];

        if (targetOccupant == handler)
        {
            handler.transform.position = GetSlotWorldPosition(idx);
            return;
        }

        if (handlerCurrentSlot >= 0)
            slotOccupants[handlerCurrentSlot] = null;

        if (targetOccupant != null)
        {
            if (handlerCurrentSlot >= 0)
                MoveToSlot(targetOccupant, handlerCurrentSlot);
            else
                MoveToGrid(targetOccupant);
        }

        MoveToSlot(handler, idx);

        UpdatePlaceholders();
        UpdateNextButton();
    }

    /// <summary>
    /// Returns a handler to the grid and clears it from any slot. Invoked by
    /// <see cref="VotingGridDropTarget.OnVotingDropped"/>.
    /// </summary>
    public void ReturnToGrid(VotingDragHandler handler)
    {
        MoveToGrid(handler);
        UpdatePlaceholders();
        UpdateNextButton();
    }

    private void ClearAllSelections()
    {
        DestroyAllDMClones();

        for (int i = 0; i < slotOccupants.Length; i++)
        {
            if (slotOccupants[i] == null) continue;
            slotOccupants[i].transform.position = slotOccupants[i].homePosition;
            var proxy = slotOccupants[i].gameObject.GetComponent<VotingSlotDropProxy>();
            if (proxy != null) Destroy(proxy);
            slotOccupants[i] = null;
        }

        UpdatePlaceholders();
        UpdateNextButton();
    }

    private void MoveToSlot(VotingDragHandler handler, int slotIdx)
    {
        slotOccupants[slotIdx]     = handler;
        handler.transform.position = GetSlotWorldPosition(slotIdx);

        // Attaches or updates the proxy so the card itself becomes a direct-hit drop target.
        var proxy = handler.gameObject.GetComponent<VotingSlotDropProxy>();
        if (proxy == null) proxy = handler.gameObject.AddComponent<VotingSlotDropProxy>();
        proxy.controller = this;
        proxy.slotIndex  = slotIdx + 1;

        if (currentPhase == VotingPhase.DMMetricVoting && !handler.isClone)
            SpawnDMClone(handler);
    }

    private void MoveToGrid(VotingDragHandler handler)
    {
        for (int i = 0; i < slotOccupants.Length; i++)
            if (slotOccupants[i] == handler) slotOccupants[i] = null;

        if (currentPhase == VotingPhase.DMMetricVoting)
        {
            if (handler.isClone)
            {
                int origIdx = handler.slotIndex;
                if (dmClones.ContainsKey(origIdx) && dmClones[origIdx] == handler)
                    dmClones.Remove(origIdx);
                Destroy(handler.gameObject);
                return;
            }
            else
            {
                DestroyDMClone(handler.slotIndex);
            }
        }

        handler.transform.position = handler.homePosition;

        var proxy = handler.gameObject.GetComponent<VotingSlotDropProxy>();
        if (proxy != null) Destroy(proxy);
    }

    /// <summary>
    /// Spawns a clone of the original group button at its home position so the DM
    /// can pick the same group for the other metric slot.
    /// </summary>
    private void SpawnDMClone(VotingDragHandler original)
    {
        if (dmClones.ContainsKey(original.slotIndex)) return;

        GameObject cloneObj = Instantiate(original.gameObject, original.transform.parent);
        cloneObj.name = original.gameObject.name + " (DMClone)";
        cloneObj.transform.position = original.homePosition;

        var cloneHandler = cloneObj.GetComponent<VotingDragHandler>();
        cloneHandler.controller   = this;
        cloneHandler.slotIndex    = original.slotIndex;
        cloneHandler.homePosition = original.homePosition;
        cloneHandler.isClone      = true;

        dmClones[original.slotIndex] = cloneHandler;

        // Clone starts in the grid; strip any proxy copied from the original.
        var cloneProxy = cloneObj.GetComponent<VotingSlotDropProxy>();
        if (cloneProxy != null) Destroy(cloneProxy);
    }

    /// <summary>Destroys the DM clone for the given slotIndex, if one exists.</summary>
    private void DestroyDMClone(int slotIndex)
    {
        if (!dmClones.ContainsKey(slotIndex)) return;

        var clone = dmClones[slotIndex];

        for (int i = 0; i < slotOccupants.Length; i++)
            if (slotOccupants[i] == clone) slotOccupants[i] = null;

        dmClones.Remove(slotIndex);
        if (clone != null) Destroy(clone.gameObject);
    }

    /// <summary>Destroys every DM clone on clear or reset.</summary>
    private void DestroyAllDMClones()
    {
        foreach (var kvp in dmClones)
        {
            if (kvp.Value != null)
            {
                for (int i = 0; i < slotOccupants.Length; i++)
                    if (slotOccupants[i] == kvp.Value) slotOccupants[i] = null;
                Destroy(kvp.Value.gameObject);
            }
        }
        dmClones.Clear();
    }

    /// <summary>Returns the handler currently occupying the given 0-based slot, or null if empty.</summary>
    public VotingDragHandler GetSlotOccupant(int zeroBasedIdx) =>
        (zeroBasedIdx >= 0 && zeroBasedIdx < slotOccupants.Length) ? slotOccupants[zeroBasedIdx] : null;

    /// <summary>Returns the empty-placeholder GameObject for the given 0-based slot.</summary>
    public GameObject GetSlotEmptyPlaceholder(int zeroBasedIdx) => zeroBasedIdx switch
    {
        0 => firstVoteEmpty,
        1 => secondVoteEmpty,
        2 => thirdVoteEmpty,
        _ => null
    };

    private int IndexOf(VotingDragHandler handler)
    {
        for (int i = 0; i < slotOccupants.Length; i++)
            if (slotOccupants[i] == handler) return i;
        return -1;
    }

    private int FirstEmptySlot()
    {
        for (int i = 0; i < maxSelections; i++)
            if (slotOccupants[i] == null) return i;
        return -1;
    }

    private Vector3 GetSlotWorldPosition(int slotIdx)
    {
        Transform anchor = slotIdx switch
        {
            0 => firstVoteSlot,
            1 => secondVoteSlot,
            2 => thirdVoteSlot,
            _ => null
        };
        return anchor != null ? anchor.position : Vector3.zero;
    }

    private void UpdatePlaceholders()
    {
        if (firstVoteEmpty  != null) firstVoteEmpty.SetActive(slotOccupants[0]  == null);
        if (secondVoteEmpty != null) secondVoteEmpty.SetActive(slotOccupants[1] == null);
        if (thirdVoteEmpty  != null && maxSelections > 2) thirdVoteEmpty.SetActive(slotOccupants[2] == null);
    }

    private void UpdateNextButton()
    {
        if (nextButton != null)
        {
            var btn = nextButton.GetComponent<Button>();
            if (btn != null)
            {
                int filledCount = 0;
                for (int i = 0; i < maxSelections; i++)
                    if (slotOccupants[i] != null) filledCount++;
                btn.interactable = filledCount >= maxSelections;
            }
        }
    }

    public void Next()
    {
        ApplyVotes();

        if (currentPhase == VotingPhase.GroupVoting)
            gameManager.AdvanceVotingSequence();
        else
            gameManager.SetState(GameManager.GameState.Scoreboard);
    }

    private void ApplyVotes()
    {
        if (currentPhase == VotingPhase.GroupVoting)
        {
            int[] points = { localFirstPlacePoints, localSecondPlacePoints, localThirdPlacePoints };
            for (int i = 0; i < maxSelections && i < points.Length; i++)
            {
                if (slotOccupants[i] == null) continue;
                int groupIdx = slotOccupants[i].slotIndex;
                if (groupIdx < activeGroups.Count)
                {
                    int groupId = activeGroups[groupIdx].id;
                    if (PlayerManager.groups.ContainsKey(groupId))
                        PlayerManager.groups[groupId].votingPhasePoints += points[i];
                }
            }
        }
        else
        {
            // With only two teams the group-voting round is skipped (see
            // GameManager.StartVotingSequence), leaving metric awards as the only
            // guaranteed group points — double them to compensate.
            int awardedPoints = PlayerManager.groups.Count <= 2 ? metricPoints * 2 : metricPoints;

            // Slot 0 maps to metric1Score, slot 1 to metric2Score.
            for (int i = 0; i < maxSelections; i++)
            {
                if (slotOccupants[i] == null) continue;
                int groupIdx = slotOccupants[i].slotIndex;
                if (groupIdx < activeGroups.Count)
                {
                    int groupId = activeGroups[groupIdx].id;
                    if (PlayerManager.groups.ContainsKey(groupId))
                    {
                        var g = PlayerManager.groups[groupId];
                        if (i == 0) g.metric1Score += awardedPoints;
                        else        g.metric2Score += awardedPoints;
                        g.score += awardedPoints;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Ranks groups by their accumulated <c>votingPhasePoints</c> and awards the
    /// configured first/second/third-place points to the top-ranked groups. Resets
    /// <c>votingPhasePoints</c> afterwards. Invoked by GameManager after all groups
    /// have voted.
    /// </summary>
    public void FinalizeGroupVoting()
    {
        // Zero vote points for groups where an uncaught player completed a
        // vote-sabotaging Betrayal corruption.
        foreach (var group in PlayerManager.groups.Values)
        {
            if (GroupHasCompletedVoteKillingBetrayal(group.id))
            {
                Debug.Log($"[Betrayal] '{group.name}' had an active vote-sabotage betrayal; resetting vote points to 0.");
                group.votingPhasePoints = 0;
            }
        }

        var rankedGroups = PlayerManager.groups.Values
            .OrderByDescending(g => g.votingPhasePoints)
            .ToList();

        int groupCount = rankedGroups.Count;
        if (groupCount == 2)
        {
            if (groupCount > 0)
            {
                AwardVotePoints(rankedGroups[0], secondPlacePoints);
                Debug.Log($"Group '{rankedGroups[0].name}' finished #1 with {rankedGroups[0].votingPhasePoints} local votes, awarded {secondPlacePoints} points");
            }
            if (groupCount > 1)
            {
                Debug.Log($"Group '{rankedGroups[1].name}' finished #2 with {rankedGroups[1].votingPhasePoints} local votes, awarded 0 points");
            }
        }
        else if (groupCount == 3)
        {
            if (groupCount > 0)
            {
                AwardVotePoints(rankedGroups[0], secondPlacePoints);
                Debug.Log($"Group '{rankedGroups[0].name}' finished #1 with {rankedGroups[0].votingPhasePoints} local votes, awarded {secondPlacePoints} points");
            }
            if (groupCount > 1)
            {
                AwardVotePoints(rankedGroups[1], thirdPlacePoints);
                Debug.Log($"Group '{rankedGroups[1].name}' finished #2 with {rankedGroups[1].votingPhasePoints} local votes, awarded {thirdPlacePoints} points");
            }
            if (groupCount > 2)
            {
                Debug.Log($"Group '{rankedGroups[2].name}' finished #3 with {rankedGroups[2].votingPhasePoints} local votes, awarded 0 points");
            }
        }
        else
        {
            int[] finalPoints = { firstPlacePoints, secondPlacePoints, thirdPlacePoints };
            for (int i = 0; i < rankedGroups.Count && i < finalPoints.Length; i++)
            {
                AwardVotePoints(rankedGroups[i], finalPoints[i]);
                Debug.Log($"Group '{rankedGroups[i].name}' finished #{i + 1} with {rankedGroups[i].votingPhasePoints} local votes, awarded {finalPoints[i]} points");
            }
        }

        foreach (var group in PlayerManager.groups.Values)
            group.votingPhasePoints = 0;
    }

    /// <summary>
    /// Adds <paramref name="points"/> to both the group's vote-rank component and its
    /// rolling total so the Scoreboard can separate vote points from metric awards.
    /// </summary>
    private static void AwardVotePoints(Group g, int points)
    {
        if (points <= 0) return;
        g.voteScore += points;
        g.score     += points;
    }

    /// <summary>
    /// Returns true when at least one non-accused player in the group has completed a
    /// Betrayal corruption that requires the group to receive zero vote points.
    /// </summary>
    private bool GroupHasCompletedVoteKillingBetrayal(int groupId)
    {
        foreach (var player in PlayerManager.GetPlayersWithGroupId(groupId))
        {
            if (player.isAccused) continue;
            var corruption = gameManager.corruptionManager.GetCorruptionByPlayerId(player.id);
            if (corruption != null && corruption.requiresZeroGroupVotes && corruption.completeted)
                return true;
        }
        return false;
    }

    private Sprite GetMetricSprite(GameManager.Metric metric)
    {
        return metric switch
        {
            GameManager.Metric.Comedy     => comedySprite,
            GameManager.Metric.Creativity => creativitySprite,
            GameManager.Metric.OnTopic    => onTopicSprite,
            GameManager.Metric.Factual    => factualSprite,
            GameManager.Metric.Enthusiasm => enthusiasmSprite,
            _ => null
        };
    }
}
