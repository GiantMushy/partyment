using UnityEngine;

/// <summary>
/// Drop target attached to the metric grid panel that holds the five metric cards.
/// Dropping a metric card here removes it from any vote slot it occupied and returns
/// it to the grid. Requires a raycast-receiving Graphic on the GameObject.
/// </summary>
public class MetricGridDropTarget : MonoBehaviour, IMetricDropTarget
{
    /// <summary>Set by <see cref="MetricSelectionController.InitializeHandlers"/>.</summary>
    [HideInInspector] public MetricSelectionController controller;

    public void OnDragHoverEnter(MetricDragHandler dragHandler) { }
    public void OnDragHoverExit() { }

    public void OnMetricDropped(MetricDragHandler dragHandler)
    {
        controller?.ReturnToGrid(dragHandler);
    }
}
