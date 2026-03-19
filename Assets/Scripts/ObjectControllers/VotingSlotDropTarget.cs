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

    [HideInInspector] public VotingController controller;

    private const float HoverAlpha = 0.55f;
    private CanvasGroup canvasGroup;
    private float idleAlpha;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        idleAlpha   = canvasGroup != null ? canvasGroup.alpha : 1f;
    }

    public void OnDragHoverEnter(VotingDragHandler dragHandler)
    {
        if (canvasGroup != null) canvasGroup.alpha = HoverAlpha;
    }

    public void OnDragHoverExit()
    {
        if (canvasGroup != null) canvasGroup.alpha = idleAlpha;
    }

    public void OnVotingDropped(VotingDragHandler dragHandler)
    {
        OnDragHoverExit();
        controller?.PlaceGroupInSlot(dragHandler, slotIndex);
    }
}
