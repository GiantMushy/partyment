using UnityEngine;

/// <summary>
/// Attach to the group grid panel on the Voting screen (the parent that holds
/// all group button cards).
///
/// Setup checklist
/// ───────────────
/// • The GameObject needs a raycast-receiving Graphic (Image, etc.) — set it to
///   fully transparent if you only want it as an invisible hit area.
/// • No hover visual on the grid — drop detection is the only purpose here.
/// • <see cref="controller"/> is injected automatically by the controller.
///
/// Dropping a group card here removes it from any vote slot it occupied
/// and returns it to its original grid position.
/// </summary>
public class VotingGridDropTarget : MonoBehaviour, IVotingDropTarget
{
    /// <summary>Set automatically by <see cref="VotingController.InitializeHandlers"/>.</summary>
    [HideInInspector] public VotingController controller;

    public void OnDragHoverEnter(VotingDragHandler dragHandler) { }
    public void OnDragHoverExit() { }

    public void OnVotingDropped(VotingDragHandler dragHandler)
    {
        controller?.ReturnToGrid(dragHandler);
    }
}
