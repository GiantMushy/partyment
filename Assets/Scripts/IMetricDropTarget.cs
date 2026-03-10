/// <summary>
/// Implemented by any UI region that can receive a dropped metric card.
///
/// Attach an implementing MonoBehaviour — <see cref="VoteSlotDropTarget"/> for each
/// vote slot, and <see cref="MetricGridDropTarget"/> for the metric grid panel — to
/// the relevant GameObjects.  The GameObject must have a raycast-receiving Graphic
/// (Image, etc.) so the EventSystem detects pointer hits on it.
///
/// <see cref="MetricDragHandler"/> discovers these targets via EventSystem raycasting
/// and calls the interface methods without needing a direct reference to the concrete
/// type, keeping drop logic fully decoupled.
/// </summary>
public interface IMetricDropTarget
{
    /// <summary>
    /// Called once when a drag enters this target's hit area.
    /// Use for visual feedback such as alpha or colour changes.
    /// </summary>
    void OnDragHoverEnter(MetricDragHandler dragHandler);

    /// <summary>
    /// Called once when the drag leaves this target without dropping, or when
    /// the drop is finalised.  Revert any visual changes made in OnDragHoverEnter.
    /// </summary>
    void OnDragHoverExit();

    /// <summary>
    /// Called when the user releases the pointer over this target.
    /// The implementation is responsible for calling the appropriate method on
    /// <see cref="MetricSelectionController"/> to commit the state change.
    /// </summary>
    void OnMetricDropped(MetricDragHandler dragHandler);
}
