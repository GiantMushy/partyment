using UnityEngine;

/// <summary>
/// Drop target attached to the group grid panel on the Voting screen. Dropping a
/// group card here removes it from any vote slot it occupied and returns it to the
/// grid. Requires a raycast-receiving Graphic on the GameObject.
/// </summary>
public class VotingGridDropTarget : MonoBehaviour, IVotingDropTarget
{
    /// <summary>Set by <see cref="VotingController.InitializeHandlers"/>.</summary>
    [HideInInspector] public VotingController controller;

    public void OnDragHoverEnter(VotingDragHandler dragHandler) { }
    public void OnDragHoverExit() { }

    public void OnVotingDropped(VotingDragHandler dragHandler)
    {
        controller?.ReturnToGrid(dragHandler);
    }
}
