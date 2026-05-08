using UnityEngine;

/// <summary>
/// Attach to the metric grid panel (the parent that holds all five metric card GameObjects).
///
/// Setup checklist
/// ───────────────
/// • The GameObject needs a raycast-receiving Graphic (Image, etc.) — set it to
///   fully transparent if you only want it as an invisible hit area.
/// • No hover visual on the grid — drop detection is the only purpose here.
/// • <see cref="controller"/> is injected automatically by the controller.
///
/// Dropping a metric card here removes it from any vote slot it occupied
/// and returns it to its original grid position.
/// </summary>
public class MetricGridDropTarget : MonoBehaviour, IMetricDropTarget
{
    // ---- Injected by MetricSelectionController ----

    /// <summary>Set automatically by <see cref="MetricSelectionController.InitializeHandlers"/>.</summary>
    [HideInInspector] public MetricSelectionController controller;

    // ================================================================
    //  IMetricDropTarget  —  no hover visuals on the grid
    // ================================================================

    public void OnDragHoverEnter(MetricDragHandler dragHandler) { }
    public void OnDragHoverExit() { }

    public void OnMetricDropped(MetricDragHandler dragHandler)
    {
        controller?.ReturnToGrid(dragHandler);
    }
}
