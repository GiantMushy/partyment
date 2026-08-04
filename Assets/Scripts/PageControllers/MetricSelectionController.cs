using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Metric = GameManager.Metric;

/// <summary>
/// Manages the Metric Selection screen. The DM selects exactly two metrics, either
/// via clicks or drag-and-drop, before advancing to AssignPositions.
/// </summary>
public class MetricSelectionController : MonoBehaviour
{
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
    [SerializeField] private VoteSlotDropTarget firstSlotDropTarget;
    [SerializeField] private VoteSlotDropTarget secondSlotDropTarget;
    [SerializeField] private MetricGridDropTarget gridDropTarget;

    [Header("Metric Buttons")]
    [Tooltip("All five metric card GameObjects. Each needs a MetricDragHandler with its 'metric' field set.")]
    [SerializeField] private GameObject[] metricButtons;

    [Header("Navigation")]
    [Tooltip("Disabled until both vote slots contain a metric card.")]
    [SerializeField] private Button nextButton;

    [Tooltip("Top-level RectTransform on the Canvas used to parent drag ghosts above all UI.")]
    public RectTransform dragLayer;

    private GameManager gameManager;

    /// <summary>
    /// Indices 0 and 1 hold the handlers occupying vote slots 1 and 2. A null entry
    /// means the slot is empty.
    /// </summary>
    private readonly MetricDragHandler[] slotOccupants = new MetricDragHandler[2];

    private bool isInitialized;

    void Start()
    {
        gameManager = GameManager.Instance;
        InitializeHandlers();
        isInitialized = true;
        ClearAllSelections();
    }

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (isInitialized) ClearAllSelections();
    }

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

    /// <summary>
    /// Toggles the card between the grid and the topmost free vote slot. Invoked by
    /// <see cref="MetricDragHandler"/> on a short press-release.
    /// </summary>
    public void OnMetricClicked(MetricDragHandler handler)
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

    public void OnDragBegin(MetricDragHandler handler) { }

    /// <summary>
    /// Snaps the card back to its logical position when a drag ends with no valid drop target.
    /// </summary>
    public void ReturnToHome(MetricDragHandler handler)
    {
        int slotIdx = IndexOf(handler);
        handler.transform.position = slotIdx >= 0
            ? GetSlotWorldPosition(slotIdx)
            : handler.homePosition;
    }

    /// <summary>
    /// Places <paramref name="handler"/> into a 1-based <paramref name="slotIndex"/>.
    /// Dropping on the slot the card already occupies snaps it back; dropping on the
    /// other occupied slot swaps the two cards; dropping from the grid on an occupied
    /// slot displaces the occupant back to the grid.
    /// </summary>
    public void PlaceMetricInSlot(MetricDragHandler handler, int slotIndex)
    {
        int idx      = slotIndex - 1;
        int otherIdx = 1 - idx;

        MetricDragHandler targetOccupant     = slotOccupants[idx];
        int               handlerCurrentSlot = IndexOf(handler);

        if (targetOccupant == handler)
        {
            handler.transform.position = GetSlotWorldPosition(idx);
            return;
        }

        if (handlerCurrentSlot >= 0)
            slotOccupants[handlerCurrentSlot] = null;

        if (targetOccupant != null)
        {
            if (handlerCurrentSlot == otherIdx)
                MoveToSlot(targetOccupant, otherIdx);
            else
                MoveToGrid(targetOccupant);
        }

        MoveToSlot(handler, idx);

        UpdatePlaceholders();
        UpdateNextButton();
    }

    /// <summary>
    /// Returns the handler to its grid home position and clears it from any slot.
    /// Invoked by <see cref="MetricGridDropTarget.OnMetricDropped"/>.
    /// </summary>
    public void ReturnToGrid(MetricDragHandler handler)
    {
        MoveToGrid(handler);
        UpdatePlaceholders();
        UpdateNextButton();
    }

    /// <summary>
    /// Wires every metric button's MetricDragHandler with this controller and caches
    /// each card's world position as its home position. Also injects this controller
    /// into the slot and grid drop targets.
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
                        $"[MetricSelectionController] '{btnObj.name}' has no MetricDragHandler component; skipping.");
                    continue;
                }

                handler.controller    = this;
                handler.homePosition  = btnObj.transform.position;
            }
        }

        if (firstSlotDropTarget  != null) { firstSlotDropTarget.controller  = this; firstSlotDropTarget.slotIndex  = 1; }
        if (secondSlotDropTarget != null) { secondSlotDropTarget.controller = this; secondSlotDropTarget.slotIndex = 2; }
        if (gridDropTarget       != null)   gridDropTarget.controller       = this;

        // Empty-slot placeholders carry a permanent proxy so they act as direct-hit
        // drop targets when active; raycasts ignore them while inactive.
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

    /// <summary>Returns every selected metric to the grid and clears both vote slots.</summary>
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

    private void MoveToSlot(MetricDragHandler handler, int slotIdx)
    {
        slotOccupants[slotIdx]     = handler;
        handler.transform.position = GetSlotWorldPosition(slotIdx);

        // Attaches or updates the proxy so the card is a direct-hit drop target while
        // occupying this slot.
        var proxy = handler.gameObject.GetComponent<MetricSlotDropProxy>();
        if (proxy == null) proxy = handler.gameObject.AddComponent<MetricSlotDropProxy>();
        proxy.controller = this;
        proxy.slotIndex  = slotIdx + 1;
    }

    private void MoveToGrid(MetricDragHandler handler)
    {
        for (int i = 0; i < slotOccupants.Length; i++)
            if (slotOccupants[i] == handler) slotOccupants[i] = null;

        handler.transform.position = handler.homePosition;

        var proxy = handler.gameObject.GetComponent<MetricSlotDropProxy>();
        if (proxy != null) Destroy(proxy);
    }

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
