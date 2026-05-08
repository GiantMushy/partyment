using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to each vote-slot area GameObject (the region the DM drags a metric card onto).
///
/// Setup checklist
/// ───────────────
/// • The GameObject needs an Image (or any raycast-receiving Graphic) so the
///   EventSystem detects pointer hits.
/// • A CanvasGroup is kept for legacy compat but is no longer used for highlighting.
/// • Set <see cref="slotIndex"/> to 1 for the first slot and 2 for the second
///   directly in the Inspector, OR let <see cref="MetricSelectionController"/>
///   inject it via <c>InitializeHandlers()</c>.
/// • <see cref="controller"/> is injected automatically by the controller.
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

    private static readonly Color SlotHoverTint = new Color(0.8f, 0.95f, 1f);

    // ---- State ----

    private MetricDragHandler highlightedOccupant;
    private Image             highlightedPlaceholder;
    private Color             placeholderOriginalColor;

    // ================================================================
    //  IMetricDropTarget
    // ================================================================

    public void OnDragHoverEnter(MetricDragHandler dragHandler)
    {
        var occupant = controller?.GetSlotOccupant(slotIndex - 1);

        if (occupant != null && occupant != dragHandler)
        {
            // Slot is occupied by a different card — tint it to show it will be displaced.
            highlightedOccupant = occupant;
            highlightedOccupant.SetDisplacedVisual(true);
        }
        else if (occupant == null)
        {
            // Slot is empty — tint the placeholder so it lights up as a drop target.
            var placeholder = controller?.GetSlotEmptyPlaceholder(slotIndex - 1);
            if (placeholder != null)
            {
                highlightedPlaceholder = placeholder.GetComponent<Image>();
                if (highlightedPlaceholder != null)
                {
                    placeholderOriginalColor = highlightedPlaceholder.color;
                    highlightedPlaceholder.color = SlotHoverTint;
                }
            }
        }
    }

    public void OnDragHoverExit()
    {
        highlightedOccupant?.SetDisplacedVisual(false);
        highlightedOccupant = null;

        if (highlightedPlaceholder != null)
        {
            highlightedPlaceholder.color = placeholderOriginalColor;
            highlightedPlaceholder = null;
        }
    }

    public void OnMetricDropped(MetricDragHandler dragHandler)
    {
        // OnDragHoverExit was already called by MetricDragHandler.ClearHover; call again
        // defensively so visuals are always reset before the state change.
        OnDragHoverExit();
        controller?.PlaceMetricInSlot(dragHandler, slotIndex);
    }
}
