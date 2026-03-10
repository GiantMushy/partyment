using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to each vote-slot area GameObject (the region the DM drags a metric card onto).
///
/// Setup checklist
/// ───────────────
/// • The GameObject needs an Image (or any raycast-receiving Graphic) so the
///   EventSystem detects pointer hits.  The same Image can also be made
///   transparent (alpha = 0) if you don't want a visible background.
/// • A CanvasGroup is required — the component highlights it on hover.
/// • Set <see cref="slotIndex"/> to 1 for the first slot and 2 for the second
///   directly in the Inspector, OR let <see cref="MetricSelectionController"/>
///   inject it via <c>InitializeHandlers()</c>.
/// • <see cref="controller"/> is injected automatically by the controller;
///   you do not need to set it in the Inspector.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class VoteSlotDropTarget : MonoBehaviour, IMetricDropTarget
{
    // ---- Inspector ----

    /// <summary>1 = first vote slot, 2 = second vote slot.</summary>
    [Tooltip("Which vote slot this represents (1 = first, 2 = second).")]
    public int slotIndex = 1;

    // ---- Injected by MetricSelectionController ----

    /// <summary>Set automatically by <see cref="MetricSelectionController.InitializeHandlers"/>.</summary>
    [HideInInspector] public MetricSelectionController controller;

    // ---- Constants ----

    /// <summary>Alpha applied to the CanvasGroup while a card hovers over this slot.</summary>
    private const float HoverAlpha = 0.55f;

    // ---- Private ----

    private CanvasGroup canvasGroup;
    private float idleAlpha;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        idleAlpha   = canvasGroup != null ? canvasGroup.alpha : 1f;
    }

    // ================================================================
    //  IMetricDropTarget
    // ================================================================

    /// <summary>Dim the slot to signal it is a valid drop target.</summary>
    public void OnDragHoverEnter(MetricDragHandler dragHandler)
    {
        if (canvasGroup != null) canvasGroup.alpha = HoverAlpha;
    }

    /// <summary>Restore original alpha when the drag leaves.</summary>
    public void OnDragHoverExit()
    {
        if (canvasGroup != null) canvasGroup.alpha = idleAlpha;
    }

    /// <summary>
    /// Revert the hover highlight, then ask the controller to place the
    /// dragged metric into this slot (handling swaps automatically).
    /// </summary>
    public void OnMetricDropped(MetricDragHandler dragHandler)
    {
        OnDragHoverExit();
        controller?.PlaceMetricInSlot(dragHandler, slotIndex);
    }
}
