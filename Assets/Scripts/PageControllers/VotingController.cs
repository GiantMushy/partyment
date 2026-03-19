using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the Voting screen.  Supports both tap/click and drag-and-drop
/// interactions, mirroring <see cref="MetricSelectionController"/>.
///
/// Two phases:
///   GroupVoting     — each group votes for their top 2 or 3 groups.
///   DMMetricVoting  — the DM assigns two metric awards to groups.
///                     The DM may pick the SAME group for both metrics,
///                     so a clone of the group card is spawned when one
///                     is placed in a slot, and destroyed when removed.
///
/// Inspector setup
/// ───────────────
/// Vote Slots
///   firstVoteSlot / secondVoteSlot / thirdVoteSlot
///       — Transforms whose world positions mark where each slot's card lives.
///   firstVoteEmpty / secondVoteEmpty / thirdVoteEmpty
///       — Placeholder GameObjects shown when the corresponding slot is empty.
///
/// Drop Targets (each needs an Image + CanvasGroup for raycasts)
///   firstSlotDropTarget / secondSlotDropTarget / thirdSlotDropTarget
///       — <see cref="VotingSlotDropTarget"/> on each slot hit area.
///   gridDropTarget
///       — <see cref="VotingGridDropTarget"/> on the group button grid panel.
///
/// Group Buttons
///   group1Button … group6Button — each MUST have a <see cref="VotingDragHandler"/>.
///
/// Navigation
///   nextButton — disabled until all required slots are filled.
///   dragLayer  — top-level RectTransform on the Canvas for drag ghosts.
///
/// Migration note
/// ──────────────
/// Remove any onClick listeners previously wired to ToggleGroup1–6.
/// <see cref="VotingDragHandler"/> now owns click logic for group cards.
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

    // ===================================================================
    //  Runtime State
    // ===================================================================

    public enum VotingPhase { GroupVoting, DMMetricVoting }
    private VotingPhase currentPhase;
    private int maxSelections;

    private GameObject[] allGroupButtons;
    private List<Group> activeGroups = new List<Group>();

    /// <summary>
    /// slotOccupants[0..2] = handler in vote slot 1/2/3. null = empty.
    /// During DM voting only slots 0 and 1 are used.
    /// </summary>
    private readonly VotingDragHandler[] slotOccupants = new VotingDragHandler[3];

    /// <summary>
    /// DM-mode clones: maps the original handler's slotIndex to its clone GameObject.
    /// When the DM places a group in a slot, we spawn a clone at the original position
    /// so the same group can be picked again for the other slot.
    /// </summary>
    private readonly Dictionary<int, VotingDragHandler> dmClones = new Dictionary<int, VotingDragHandler>();

    private bool isInitialized;

    // ===================================================================
    //  Unity Lifecycle
    // ===================================================================

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

    // ===================================================================
    //  SETUP — Called by GameManager before enabling this GameObject
    // ===================================================================

    public void PrepareForGroupVoting()
    {
        currentPhase = VotingPhase.GroupVoting;
    }

    public void PrepareForDMMetricVoting()
    {
        currentPhase = VotingPhase.DMMetricVoting;
    }

    // ===================================================================
    //  HANDLER / DROP TARGET INITIALISATION
    // ===================================================================

    /// <summary>
    /// Ensures every group button has a VotingDragHandler, injects controller refs,
    /// and wires up drop targets.
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
                    $"[VotingController] '{btn.name}' has no VotingDragHandler — skipping. " +
                    "Add the component in the Inspector.");
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
    }

    // ===================================================================
    //  UI SETUP
    // ===================================================================

    private void SetupUI()
    {
        ClearAllSelections();

        activeGroups = PlayerManager.groups.Values
            .OrderBy(g => g.id)
            .ToList();
        int groupCount = activeGroups.Count;

        for (int i = 0; i < allGroupButtons.Length; i++)
        {
            if (i < groupCount)
            {
                allGroupButtons[i].SetActive(true);
                var label = allGroupButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = activeGroups[i].name;

                // Re-cache home positions (in case layout changed)
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

        // Show/hide third slot drop target
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

    // ===================================================================
    //  VotingDragHandler Callbacks
    // ===================================================================

    /// <summary>
    /// Called by <see cref="VotingDragHandler"/> on a short click.
    /// Toggles the card between the grid and the topmost free vote slot.
    /// </summary>
    public void OnGroupClicked(VotingDragHandler handler)
    {
        int slotIdx = IndexOf(handler);

        if (slotIdx >= 0)
        {
            // Card is in a slot — return it to the grid.
            MoveToGrid(handler);
        }
        else
        {
            // Card is in the grid — place it in the first empty slot.
            int emptySlot = FirstEmptySlot();
            if (emptySlot >= 0) MoveToSlot(handler, emptySlot);
        }

        UpdatePlaceholders();
        UpdateNextButton();
    }

    public void OnDragBegin(VotingDragHandler handler) { }

    /// <summary>
    /// Called when a drag ends with no valid drop target.
    /// Snaps the card back to its logical position.
    /// </summary>
    public void ReturnToHome(VotingDragHandler handler)
    {
        int slotIdx = IndexOf(handler);
        handler.transform.position = slotIdx >= 0
            ? GetSlotWorldPosition(slotIdx)
            : handler.homePosition;
    }

    // ===================================================================
    //  Drop Target Callbacks
    // ===================================================================

    /// <summary>
    /// Places <paramref name="handler"/> into <paramref name="slotIndex"/> (1-based).
    ///
    /// Drop rules (Group Voting — no duplicates allowed):
    /// • Same slot → snap back.
    /// • Target occupied, handler from another slot → swap.
    /// • Target occupied, handler from grid → displace occupant to grid.
    /// • Target empty → place.
    ///
    /// DM Metric Voting allows the same group in both slots, so swaps are
    /// between clones and originals transparently.
    /// </summary>
    public void PlaceGroupInSlot(VotingDragHandler handler, int slotIndex)
    {
        int idx      = slotIndex - 1;
        int handlerCurrentSlot = IndexOf(handler);

        VotingDragHandler targetOccupant = slotOccupants[idx];

        // Dropping on the slot already occupied by this very handler — snap back.
        if (targetOccupant == handler)
        {
            handler.transform.position = GetSlotWorldPosition(idx);
            return;
        }

        // Vacate the handler's current slot before re-assigning.
        if (handlerCurrentSlot >= 0)
            slotOccupants[handlerCurrentSlot] = null;

        // Resolve the target slot's current occupant.
        if (targetOccupant != null)
        {
            if (handlerCurrentSlot >= 0)
                MoveToSlot(targetOccupant, handlerCurrentSlot); // swap
            else
                MoveToGrid(targetOccupant); // displace to grid
        }

        // Place the dragged card in the target slot.
        MoveToSlot(handler, idx);

        UpdatePlaceholders();
        UpdateNextButton();
    }

    /// <summary>
    /// Returns a handler to the grid and clears it from any slot.
    /// Called by <see cref="VotingGridDropTarget.OnVotingDropped"/>.
    /// </summary>
    public void ReturnToGrid(VotingDragHandler handler)
    {
        MoveToGrid(handler);
        UpdatePlaceholders();
        UpdateNextButton();
    }

    // ===================================================================
    //  State Management
    // ===================================================================

    private void ClearAllSelections()
    {
        // Destroy any DM clones first
        DestroyAllDMClones();

        for (int i = 0; i < slotOccupants.Length; i++)
        {
            if (slotOccupants[i] == null) continue;
            slotOccupants[i].transform.position = slotOccupants[i].homePosition;
            slotOccupants[i] = null;
        }

        UpdatePlaceholders();
        UpdateNextButton();
    }

    // ---- Low-level move primitives ----

    private void MoveToSlot(VotingDragHandler handler, int slotIdx)
    {
        slotOccupants[slotIdx]       = handler;
        handler.transform.position   = GetSlotWorldPosition(slotIdx);

        // DM mode: spawn a clone at the home position so the same group can be picked again
        if (currentPhase == VotingPhase.DMMetricVoting && !handler.isClone)
            SpawnDMClone(handler);
    }

    private void MoveToGrid(VotingDragHandler handler)
    {
        // Clear from any slot
        for (int i = 0; i < slotOccupants.Length; i++)
            if (slotOccupants[i] == handler) slotOccupants[i] = null;

        if (currentPhase == VotingPhase.DMMetricVoting)
        {
            if (handler.isClone)
            {
                // A clone being returned to the grid should just be destroyed
                int origIdx = handler.slotIndex;
                if (dmClones.ContainsKey(origIdx) && dmClones[origIdx] == handler)
                    dmClones.Remove(origIdx);
                Destroy(handler.gameObject);
                return;
            }
            else
            {
                // Original being returned — destroy its clone if one exists
                DestroyDMClone(handler.slotIndex);
            }
        }

        handler.transform.position = handler.homePosition;
    }

    // ---- DM Clone Management ----

    /// <summary>
    /// Spawns a clone of the original group button at its home position so
    /// the DM can pick the same group for the other metric slot.
    /// </summary>
    private void SpawnDMClone(VotingDragHandler original)
    {
        // Don't spawn if a clone already exists for this group
        if (dmClones.ContainsKey(original.slotIndex)) return;

        // Check if original is already occupying both slots (shouldn't happen, but guard)
        GameObject cloneObj = Instantiate(original.gameObject, original.transform.parent);
        cloneObj.name = original.gameObject.name + " (DMClone)";
        cloneObj.transform.position = original.homePosition;

        var cloneHandler = cloneObj.GetComponent<VotingDragHandler>();
        cloneHandler.controller   = this;
        cloneHandler.slotIndex    = original.slotIndex;
        cloneHandler.homePosition = original.homePosition;
        cloneHandler.isClone      = true;

        dmClones[original.slotIndex] = cloneHandler;
    }

    /// <summary>Destroys the DM clone for a given slotIndex, if one exists.</summary>
    private void DestroyDMClone(int slotIndex)
    {
        if (!dmClones.ContainsKey(slotIndex)) return;

        var clone = dmClones[slotIndex];

        // If the clone is currently in a vote slot, clear it
        for (int i = 0; i < slotOccupants.Length; i++)
            if (slotOccupants[i] == clone) slotOccupants[i] = null;

        dmClones.Remove(slotIndex);
        if (clone != null) Destroy(clone.gameObject);
    }

    /// <summary>Destroys all DM clones. Called on clear/reset.</summary>
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

    // ---- Query helpers ----

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

    // ---- UI refresh ----

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

    // ===================================================================
    //  NEXT BUTTON
    // ===================================================================

    public void Next()
    {
        ApplyVotes();

        if (currentPhase == VotingPhase.GroupVoting)
            gameManager.AdvanceVotingSequence();
        else
            gameManager.SetState(GameManager.GameState.Scoreboard);
    }

    // ===================================================================
    //  SCORING
    // ===================================================================

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
        else // DM metric voting
        {
            for (int i = 0; i < maxSelections; i++)
            {
                if (slotOccupants[i] == null) continue;
                int groupIdx = slotOccupants[i].slotIndex;
                if (groupIdx < activeGroups.Count)
                {
                    int groupId = activeGroups[groupIdx].id;
                    if (PlayerManager.groups.ContainsKey(groupId))
                        PlayerManager.groups[groupId].score += metricPoints;
                }
            }
        }
    }

    // ===================================================================
    //  FINALIZE GROUP VOTING
    // ===================================================================

    /// <summary>
    /// Ranks groups by their accumulated votingPhasePoints and awards
    /// the real firstPlacePoints / secondPlacePoints / thirdPlacePoints
    /// to the top-ranked groups. Resets votingPhasePoints afterwards.
    /// Called by GameManager after all groups have voted.
    /// </summary>
    public void FinalizeGroupVoting()
    {
        var rankedGroups = PlayerManager.groups.Values
            .OrderByDescending(g => g.votingPhasePoints)
            .ToList();

        int[] finalPoints = { firstPlacePoints, secondPlacePoints, thirdPlacePoints };
        for (int i = 0; i < rankedGroups.Count && i < finalPoints.Length; i++)
        {
            rankedGroups[i].score += finalPoints[i];
            Debug.Log($"Group '{rankedGroups[i].name}' finished #{i + 1} with {rankedGroups[i].votingPhasePoints} local votes — awarded {finalPoints[i]} points");
        }

        foreach (var group in PlayerManager.groups.Values)
            group.votingPhasePoints = 0;
    }

    // ===================================================================
    //  HELPERS
    // ===================================================================

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
