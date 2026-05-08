using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamically attached to a metric card while it occupies a vote slot, or to an
/// empty-slot placeholder while that slot is vacant.
///
/// This makes the visual content at each slot a DIRECT-HIT IMetricDropTarget, so
/// MetricDragHandler's Pass-1 (GetComponent, no parent walk) reliably finds it.
/// Without this, hovering over a card that is in a slot would trigger the parent-level
/// MetricGridDropTarget via GetComponentInParent, sending the dragged card back to the
/// grid instead of swapping/replacing.
///
/// Managed entirely by MetricSelectionController — do not add manually in the Inspector.
/// </summary>
public class MetricSlotDropProxy : MonoBehaviour, IMetricDropTarget
{
    [HideInInspector] public MetricSelectionController controller;
    [HideInInspector] public int slotIndex; // 1-based

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
            // Proxy is on a card that occupies this slot.
            if (selfHandler == dragHandler) return; // hovering own slot — snap-back, no visual needed
            selfHandler.SetDisplacedVisual(true);
            highlightedHandler = selfHandler;
        }
        else if (image != null)
        {
            // Proxy is on the empty-slot placeholder — light it up.
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
