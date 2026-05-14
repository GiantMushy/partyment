using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamically attached to a metric card while it occupies a vote slot, or to an
/// empty-slot placeholder while that slot is vacant. Makes the slot content a
/// direct-hit <see cref="IMetricDropTarget"/> so <see cref="MetricDragHandler"/>'s
/// first-pass GetComponent lookup finds it before the parent grid target. Managed
/// by <see cref="MetricSelectionController"/>; not added in the Inspector.
/// </summary>
public class MetricSlotDropProxy : MonoBehaviour, IMetricDropTarget
{
    [HideInInspector] public MetricSelectionController controller;
    [HideInInspector] public int slotIndex;

    private static readonly Color SlotHoverTint = new Color(0.8f, 0.95f, 1f);

    private Image             image;
    private Color             originalColor;
    private MetricDragHandler highlightedHandler;

    void Awake()
    {
        image = GetComponent<Image>();
        if (image != null) originalColor = image.color;
    }

    public void OnDragHoverEnter(MetricDragHandler dragHandler)
    {
        var selfHandler = GetComponent<MetricDragHandler>();
        if (selfHandler != null)
        {
            // Proxy lives on a card that occupies this slot.
            if (selfHandler == dragHandler) return;
            selfHandler.SetDisplacedVisual(true);
            highlightedHandler = selfHandler;
        }
        else if (image != null)
        {
            // Proxy lives on the empty-slot placeholder.
            originalColor = image.color;
            image.color   = SlotHoverTint;
        }
    }

    public void OnDragHoverExit()
    {
        if (highlightedHandler != null)
        {
            highlightedHandler.SetDisplacedVisual(false);
            highlightedHandler = null;
        }
        else if (image != null)
        {
            image.color = originalColor;
        }
    }

    public void OnMetricDropped(MetricDragHandler dragHandler)
    {
        OnDragHoverExit();
        controller?.PlaceMetricInSlot(dragHandler, slotIndex);
    }
}
