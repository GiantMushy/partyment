using UnityEngine;

/// <summary>
/// Attach to each vote-slot area GameObject on the Voting screen (the region
/// the player drags a group card onto).
///
/// Setup checklist
/// ───────────────
/// • The GameObject needs an Image (or any raycast-receiving Graphic) so the
///   EventSystem detects pointer hits.  Set it transparent if you only want an
///   invisible hit area.
/// • A CanvasGroup is required — the component highlights it on hover.
/// • Set <see cref="slotIndex"/> to 1, 2, or 3 directly in the Inspector,
///   OR let <see cref="VotingController.InitializeHandlers"/> inject it.
/// • <see cref="controller"/> is injected automatically by the controller.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class VotingSlotDropTarget : MonoBehaviour, IVotingDropTarget
{
    /// <summary>1 = first vote slot, 2 = second, 3 = third.</summary>
    [Tooltip("Which vote slot this represents (1 = first, 2 = second, 3 = third).")]
    public int slotIndex = 1;

    /// <summary>Set automatically by <see cref="VotingController.InitializeHandlers"/>.</summary>
    [HideInInspector] public VotingController controller;

    /// <summary>Alpha applied to the CanvasGroup while a card hovers over this slot.</summary>
    private const float HoverAlpha = 0.55f;

    private CanvasGroup canvasGroup;
    private float idleAlpha;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        idleAlpha   = canvasGroup != null ? canvasGroup.alpha : 1f;
    }

    /// <summary>Dim the slot to signal it is a valid drop target.</summary>
    public void OnDragHoverEnter(VotingDragHandler dragHandler)
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
    /// dragged group card into this slot (handling swaps and DM clones automatically).
    /// </summary>
    public void OnVotingDropped(VotingDragHandler dragHandler)
    {
        OnDragHoverExit();
        controller?.PlaceGroupInSlot(dragHandler, slotIndex);
    }
}
