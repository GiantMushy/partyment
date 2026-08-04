/// <summary>
/// Implemented by any UI region that can receive a dropped metric card. The target
/// GameObject must have a raycast-receiving Graphic so the EventSystem can detect
/// pointer hits on it. <see cref="MetricDragHandler"/> discovers targets via raycasting.
/// </summary>
public interface IMetricDropTarget
{
    /// <summary>Called when a drag enters this target's hit area.</summary>
    void OnDragHoverEnter(MetricDragHandler dragHandler);

    /// <summary>
    /// Called when the drag leaves this target without dropping or when the drop is
    /// finalized. Reverts any visuals applied in <see cref="OnDragHoverEnter"/>.
    /// </summary>
    void OnDragHoverExit();

    /// <summary>
    /// Called when the pointer is released over this target. The implementation
    /// forwards to <see cref="MetricSelectionController"/> to commit the change.
    /// </summary>
    void OnMetricDropped(MetricDragHandler dragHandler);
}
