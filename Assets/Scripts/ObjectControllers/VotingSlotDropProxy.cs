using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamically attached to a group card while it occupies a vote slot, or to an
/// empty-slot placeholder while that slot is vacant.
///
/// Mirrors <see cref="MetricSlotDropProxy"/> for the Voting screen.
/// Managed entirely by VotingController — do not add manually in the Inspector.
/// </summary>
public class VotingSlotDropProxy : MonoBehaviour, IVotingDropTarget
{
    [HideInInspector] public VotingController controller;
    [HideInInspector] public int slotIndex; // 1-based

    private static readonly Color SlotHoverTint = new Color(0.8f, 0.95f, 1f);

    private Image             image;
    private Color             originalColor;
    private VotingDragHandler highlightedHandler;

    void Awake()
    {
        image = GetComponent<Image>();
        if (image != null) originalColor = image.color;
    }

    public void OnDragHoverEnter(VotingDragHandler dragHandler)
    {
        var selfHandler = GetComponent<VotingDragHandler>();
        if (selfHandler != null)
        {
            if (selfHandler == dragHandler) return;
            selfHandler.SetDisplacedVisual(true);
            highlightedHandler = selfHandler;
        }
        else if (image != null)
        {
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

    public void OnVotingDropped(VotingDragHandler dragHandler)
    {
        OnDragHoverExit();
        controller?.PlaceGroupInSlot(dragHandler, slotIndex);
    }
}
