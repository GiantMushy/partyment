using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Metric = GameManager.Metric;

/// <summary>
/// Manages the Metric Selection screen.
/// The DM selects exactly two metrics to judge players by using either taps/clicks
/// or drag-and-drop.
///
/// Inspector setup
/// ───────────────
/// Vote Slots
///   firstVoteSlot      — Transform whose world position marks where slot-1's card lives.
///   secondVoteSlot     — Transform whose world position marks where slot-2's card lives.
///   firstVoteEmpty     — Placeholder GameObject shown when slot 1 is empty.
///   secondVoteEmpty    — Placeholder GameObject shown when slot 2 is empty.
///
/// Drop Targets (each needs an Image + CanvasGroup so raycasts register)
///   firstSlotDropTarget  — VoteSlotDropTarget on the slot-1 hit area.
///   secondSlotDropTarget — VoteSlotDropTarget on the slot-2 hit area.
///   gridDropTarget       — MetricGridDropTarget on the metric grid panel.
///
/// Metric Buttons
///   metricButtons — Array of all five metric card GameObjects.
///                   Each MUST have a MetricDragHandler with its 'metric' field set.
///
/// Navigation
///   nextButton — Disabled until both slots are filled.
///   dragLayer  — Top-level RectTransform on the Canvas used to parent drag ghosts.
///
/// Migration note
/// ──────────────
/// Remove any onClick listeners previously wired to ToggleComedy / ToggleCreativity /
/// ToggleOnTopic / ToggleFactual / ToggleEnthusiasm — MetricDragHandler now owns
/// click logic for metric cards.
/// </summary>
public class MetricSelectionController : MonoBehaviour
{
    // ================================================================
    //  Inspector References
    // ================================================================

    [Header("Vote Slots")]
    [Tooltip("World-position anchor for the card placed in slot 1.")]
    [SerializeField] private Transform firstVoteSlot;

    [Tooltip("World-position anchor for the card placed in slot 2.")]
    [SerializeField] private Transform secondVoteSlot;

    [Tooltip("Placeholder shown when slot 1 has no card.")]
    [SerializeField] private GameObject firstVoteEmpty;

    [Tooltip("Placeholder shown when slot 2 has no card.")]
    [SerializeField] private GameObject secondVoteEmpty;

    [Header("Drop Targets")]
    [Tooltip("VoteSlotDropTarget on the slot-1 hit area. Controller injects itself on Start.")]
    [SerializeField] private VoteSlotDropTarget firstSlotDropTarget;

    [Tooltip("VoteSlotDropTarget on the slot-2 hit area. Controller injects itself on Start.")]
    [SerializeField] private VoteSlotDropTarget secondSlotDropTarget;

    [Tooltip("MetricGridDropTarget on the metric grid panel. Controller injects itself on Start.")]
    [SerializeField] private MetricGridDropTarget gridDropTarget;

    [Header("Metric Buttons")]
    [Tooltip("All five metric card GameObjects. Each must have a MetricDragHandler with 'metric' set.")]
    [SerializeField] private GameObject[] metricButtons;

    [Header("Navigation")]
    [Tooltip("Disabled until both vote slots contain a metric card.")]
    [SerializeField] private Button nextButton;

    [Tooltip("Top-level RectTransform on the Canvas; used to parent drag ghosts above all UI.")]
    public RectTransform dragLayer;

    // ================================================================
    //  Runtime State
    // ================================================================

    private GameManager gameManager;

    /// <summary>
    /// slotOccupants[0] = handler in vote slot 1, slotOccupants[1] = handler in vote slot 2.
    /// null means the slot is empty.
    /// </summary>
    private readonly MetricDragHandler[] slotOccupants = new MetricDragHandler[2];

    /// <summary>True once Start() has run and handlers have been initialised.</summary>
    private bool isInitialized;

    // ================================================================
    //  Unity Lifecycle
    // ================================================================

    void Start()
    {
        gameManager = GameManager.Instance;
        InitializeHandlers();
        isInitialized = true;
        ClearAllSelections(); // set initial UI state after homePositions are cached
    }

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        // Start() will call ClearAllSelections on the very first enable;
        // only do it ourselves for subsequent re-enables.
        if (isInitialized) ClearAllSelections();
    }

    // ================================================================
    //  Navigation  (wire to buttons in the Inspector)
    // ================================================================

    /// <summary>Commits the two selected metrics and advances to AssignPositions.</summary>
    public void Next()
    {
        gameManager.selectedMetrics = new List<Metric>();
        if (slotOccupants[0] != null) gameManager.selectedMetrics.Add(slotOccupants[0].metric);
        if (slotOccupants[1] != null) gameManager.selectedMetrics.Add(slotOccupants[1].metric);
        gameManager.SetState(GameManager.GameState.AssignPositions);
    }

    public void Back()
    {
        gameManager.SetState(GameManager.GameState.TopicSelection);
    }

    // ================================================================
    //  MetricDragHandler Callbacks
    // ================================================================

    /// <summary>
    /// Called by <see cref="MetricDragHandler"/> when a short press-release is detected.
    /// Toggles the card between the grid and the topmost free vote slot.
    /// </summary>
    public void OnMetricClicked(MetricDragHandler handler)
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
            // Both slots full: ignore the click.
        }

        UpdatePlaceholders();
        UpdateNextButton();
    }

    /// <summary>
    /// Called by <see cref="MetricDragHandler"/> when a drag begins.
    /// Currently a no-op; reserved for future drag-begin side effects (e.g. sound).
    /// </summary>
    public void OnDragBegin(MetricDragHandler handler) { }

    /// <summary>
    /// Called by <see cref="MetricDragHandler"/> when a drag ends with no valid drop target.
    /// Snaps the card back to its logical position (slot or grid) without changing state.
    /// </summary>
    public void ReturnToHome(MetricDragHandler handler)
    {
        int slotIdx = IndexOf(handler);
        handler.transform.position = slotIdx >= 0
            ? GetSlotWorldPosition(slotIdx)
            : handler.homePosition;
    }

    // ================================================================
    //  Drop Target Callbacks  (called by VoteSlotDropTarget / MetricGridDropTarget)
    // ================================================================

    /// <summary>
    /// Places <paramref name="handler"/> into <paramref name="slotIndex"/> (1-based).
    ///
    /// Drop rules
    /// ──────────
    /// • Dropping on the same slot the card already occupies — snap back, no change.
    /// • Target slot occupied, card came from the OTHER slot — swap the two cards.
    /// • Target slot occupied, card came from the grid       — displace the occupant to the grid.
    /// • Target slot empty                                   — place the card there.
    /// </summary>
    public void PlaceMetricInSlot(MetricDragHandler handler, int slotIndex)
    {
        int idx      = slotIndex - 1; // convert to 0-based
        int otherIdx = 1 - idx;

        MetricDragHandler targetOccupant   = slotOccupants[idx];
        int               handlerCurrentSlot = IndexOf(handler); // -1 if in the grid

        // Dropping on the slot already occupied by this very card — just snap back.
        if (targetOccupant == handler)
        {
            handler.transform.position = GetSlotWorldPosition(idx);
            return;
        }

        // Vacate the handler's current slot (if it was in one) BEFORE re-assigning.
        if (handlerCurrentSlot >= 0)
            slotOccupants[handlerCurrentSlot] = null;

        // Resolve the target slot's current occupant.
        if (targetOccupant != null)
        {
            if (handlerCurrentSlot == otherIdx)
                // Swap: send the displaced card to the slot the handler just left.
                MoveToSlot(targetOccupant, otherIdx);
            else
                // Handler came from the grid: return the occupant to the grid.
                MoveToGrid(targetOccupant);
        }

        // Place the dragged card in the target slot.
        MoveToSlot(handler, idx);

        UpdatePlaceholders();
        UpdateNextButton();
    }

    /// <summary>
    /// Returns <paramref name="handler"/> to its grid home position and clears it from
    /// any slot it occupied.  Called by <see cref="MetricGridDropTarget.OnMetricDropped"/>.
    /// </summary>
    public void ReturnToGrid(MetricDragHandler handler)
    {
        MoveToGrid(handler);
        UpdatePlaceholders();
        UpdateNextButton();
    }

    // ================================================================
    //  Initialisation
    // ================================================================

    /// <summary>
    /// For each entry in <see cref="metricButtons"/>:
    ///   1. Ensures a <see cref="MetricDragHandler"/> component is present.
    ///   2. Injects this controller reference.
    ///   3. Caches the card's current world position as its <c>homePosition</c>.
    ///
    /// Also injects this controller into the three drop-target MonoBehaviours.
    /// </summary>
    public void InitializeHandlers()
    {
        if (metricButtons != null)
        {
            foreach (var btnObj in metricButtons)
            {
                if (btnObj == null) continue;

                var handler = btnObj.GetComponent<MetricDragHandler>();
                if (handler == null)
                {
                    Debug.LogWarning(
                        $"[MetricSelectionController] '{btnObj.name}' has no MetricDragHandler — skipping. " +
                        "Add the component and set its 'metric' field.");
                    continue;
                }

                handler.controller    = this;
                handler.homePosition  = btnObj.transform.position;
            }
        }

        if (firstSlotDropTarget  != null) { firstSlotDropTarget.controller  = this; firstSlotDropTarget.slotIndex  = 1; }
        if (secondSlotDropTarget != null) { secondSlotDropTarget.controller = this; secondSlotDropTarget.slotIndex = 2; }
        if (gridDropTarget       != null)   gridDropTarget.controller       = this;

        // Attach a proxy to each empty-slot placeholder so it acts as a direct-hit drop
        // target when the slot is vacant. The proxy stays on the object permanently;
        // it is harmless when the placeholder is inactive (raycasts ignore inactive objects).
        SetupPlaceholderProxy(firstVoteEmpty,  1);
        SetupPlaceholderProxy(secondVoteEmpty, 2);
    }

    private void SetupPlaceholderProxy(GameObject placeholder, int oneBasedSlotIndex)
    {
        if (placeholder == null) return;
        var proxy = placeholder.GetComponent<MetricSlotDropProxy>();
        if (proxy == null) proxy = placeholder.AddComponent<MetricSlotDropProxy>();
        proxy.controller = this;
        proxy.slotIndex  = oneBasedSlotIndex;
    }

    // ================================================================
    //  State Management
    // ================================================================

    /// <summary>Returns all selected metrics to the grid and clears both vote slots.</summary>
    private void ClearAllSelections()
    {
        for (int i = 0; i < slotOccupants.Length; i++)
        {
            var occupant = slotOccupants[i];
            if (occupant != null) MoveToGrid(occupant);
        }
        UpdatePlaceholders();
        UpdateNextButton();
    }

    // ---- Low-level move primitives (intentionally do not call UI updates) ----

    private void MoveToSlot(MetricDragHandler handler, int slotIdx)
    {
        slotOccupants[slotIdx]     = handler;
        handler.transform.position = GetSlotWorldPosition(slotIdx);

        // Attach (or update) a proxy so the card itself is a direct-hit drop target
        // while it occupies this slot.
        var proxy = handler.gameObject.GetComponent<MetricSlotDropProxy>();
        if (proxy == null) proxy = handler.gameObject.AddComponent<MetricSlotDropProxy>();
        proxy.controller = this;
        proxy.slotIndex  = slotIdx + 1; // 1-based
    }

    private void MoveToGrid(MetricDragHandler handler)
    {
        for (int i = 0; i < slotOccupants.Length; i++)
            if (slotOccupants[i] == handler) slotOccupants[i] = null;

        handler.transform.position = handler.homePosition;

        // Remove the slot proxy — card is back in the grid and no longer a drop target.
        var proxy = handler.gameObject.GetComponent<MetricSlotDropProxy>();
        if (proxy != null) Destroy(proxy);
    }

    // ---- Query helpers ----

    /// <summary>Returns the handler currently occupying the given 0-based slot, or null if empty.</summary>
    public MetricDragHandler GetSlotOccupant(int zeroBasedIdx) =>
        (zeroBasedIdx >= 0 && zeroBasedIdx < slotOccupants.Length) ? slotOccupants[zeroBasedIdx] : null;

    /// <summary>Returns the empty-placeholder GameObject for the given 0-based slot.</summary>
    public GameObject GetSlotEmptyPlaceholder(int zeroBasedIdx)
    {
        if (zeroBasedIdx == 0) return firstVoteEmpty;
        if (zeroBasedIdx == 1) return secondVoteEmpty;
        return null;
    }

    /// <summary>Returns the 0-based slot index of <paramref name="handler"/>, or -1 if it is in the grid.</summary>
    private int IndexOf(MetricDragHandler handler)
    {
        for (int i = 0; i < slotOccupants.Length; i++)
            if (slotOccupants[i] == handler) return i;
        return -1;
    }

    /// <summary>Returns the 0-based index of the first empty slot, or -1 if both are full.</summary>
    private int FirstEmptySlot()
    {
        for (int i = 0; i < slotOccupants.Length; i++)
            if (slotOccupants[i] == null) return i;
        return -1;
    }

    /// <summary>Returns the world-space position of slot <paramref name="slotIdx"/> (0-based).</summary>
    private Vector3 GetSlotWorldPosition(int slotIdx)
    {
        Transform anchor = slotIdx == 0 ? firstVoteSlot : secondVoteSlot;
        return anchor != null ? anchor.position : Vector3.zero;
    }

    // ---- UI refresh ----

    private void UpdatePlaceholders()
    {
        if (firstVoteEmpty  != null) firstVoteEmpty.SetActive(slotOccupants[0]  == null);
        if (secondVoteEmpty != null) secondVoteEmpty.SetActive(slotOccupants[1] == null);
    }

    private void UpdateNextButton()
    {
        if (nextButton != null)
            nextButton.interactable = slotOccupants[0] != null && slotOccupants[1] != null;
    }
}
