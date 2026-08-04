using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drop target attached to each vote-slot region on the Voting screen. Requires a
/// raycast-receiving Graphic on the GameObject. <see cref="slotIndex"/> identifies
/// which slot the target represents; <see cref="controller"/> is injected by
/// <see cref="VotingController.InitializeHandlers"/>.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class VotingSlotDropTarget : MonoBehaviour, IVotingDropTarget
{
    /// <summary>1 = first vote slot, 2 = second, 3 = third.</summary>
    [Tooltip("Which vote slot this represents (1 = first, 2 = second, 3 = third).")]
    public int slotIndex = 1;

    [HideInInspector] public VotingController controller;

    private static readonly Color SlotHoverTint = new Color(0.8f, 0.95f, 1f);

    private VotingDragHandler highlightedOccupant;
    private Image             highlightedPlaceholder;
    private Color             placeholderOriginalColor;

    public void OnDragHoverEnter(VotingDragHandler dragHandler)
    {
        var occupant = controller?.GetSlotOccupant(slotIndex - 1);

        if (occupant != null && occupant != dragHandler)
        {
            // Slot occupied by a different card; tint it to indicate displacement.
            highlightedOccupant = occupant;
            highlightedOccupant.SetDisplacedVisual(true);
        }
        else if (occupant == null)
        {
            // Slot empty; tint the placeholder to light it up as a drop target.
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

    public void OnVotingDropped(VotingDragHandler dragHandler)
    {
        // Defensive: ClearHover already invoked this, but the second call guarantees
        // visuals are reset before the state change.
        OnDragHoverExit();
        controller?.PlaceGroupInSlot(dragHandler, slotIndex);
    }
}
