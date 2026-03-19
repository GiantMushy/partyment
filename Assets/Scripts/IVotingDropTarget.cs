/// <summary>
/// Implemented by any UI region that can receive a dropped voting group card.
///
/// Attach an implementing MonoBehaviour — <see cref="VotingSlotDropTarget"/> for each
/// vote slot, and <see cref="VotingGridDropTarget"/> for the group grid panel — to
/// the relevant GameObjects.  The GameObject must have a raycast-receiving Graphic
/// (Image, etc.) so the EventSystem detects pointer hits on it.
///
/// <see cref="VotingDragHandler"/> discovers these targets via EventSystem raycasting
/// and calls the interface methods without needing a direct reference to the concrete
/// type, keeping drop logic fully decoupled.
/// </summary>
public interface IVotingDropTarget
{
    /// <summary>
    /// Called once when a drag enters this target's hit area.
    /// Use for visual feedback such as alpha or colour changes.
    /// </summary>
    void OnDragHoverEnter(VotingDragHandler dragHandler);

    /// <summary>
    /// Called once when the drag leaves this target without dropping, or when
    /// the drop is finalised.  Revert any visual changes made in OnDragHoverEnter.
    /// </summary>
    void OnDragHoverExit();

    /// <summary>
    /// Called when the user releases the pointer over this target.
    /// The implementation is responsible for calling the appropriate method on
    /// <see cref="VotingController"/> to commit the state change.
    /// </summary>
    void OnVotingDropped(VotingDragHandler dragHandler);
}
