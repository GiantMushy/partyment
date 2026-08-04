/// <summary>
/// Implemented by any UI region that can receive a dropped voting group card. The
/// target GameObject must have a raycast-receiving Graphic so the EventSystem can
/// detect pointer hits. <see cref="VotingDragHandler"/> discovers targets via raycasting.
/// </summary>
public interface IVotingDropTarget
{
    /// <summary>Called when a drag enters this target's hit area.</summary>
    void OnDragHoverEnter(VotingDragHandler dragHandler);

    /// <summary>
    /// Called when the drag leaves this target without dropping or when the drop is
    /// finalized. Reverts any visuals applied in <see cref="OnDragHoverEnter"/>.
    /// </summary>
    void OnDragHoverExit();

    /// <summary>
    /// Called when the pointer is released over this target. The implementation
    /// forwards to <see cref="VotingController"/> to commit the change.
    /// </summary>
    void OnVotingDropped(VotingDragHandler dragHandler);
}
