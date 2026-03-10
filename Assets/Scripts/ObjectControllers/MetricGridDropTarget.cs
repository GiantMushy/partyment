using UnityEngine;

/// <summary>
/// Attach to the metric grid panel (the parent that holds all five metric card GameObjects).
///
/// Setup checklist
/// ───────────────
/// • The GameObject needs a raycast-receiving Graphic (Image, etc.) — set it to
///   fully transparent if you only want it as an invisible hit area.
/// • No CanvasGroup needed; there is no hover visual change on the grid.
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

    /// <summary>No visual feedback when hovering the grid.</summary>
    public void OnDragHoverEnter(MetricDragHandler dragHandler) { }

    /// <summary>No visual feedback when leaving the grid.</summary>
    public void OnDragHoverExit() { }

    /// <summary>Return the dragged card to the grid and clear its slot.</summary>
    public void OnMetricDropped(MetricDragHandler dragHandler)
    {
        controller?.ReturnToGrid(dragHandler);
    }
}
