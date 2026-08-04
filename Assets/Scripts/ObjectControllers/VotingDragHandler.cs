using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Attached to each group button card on the Voting screen. A short press is treated
/// as a click and forwarded to <see cref="VotingController.OnGroupClicked"/>. A press
/// that moves beyond <see cref="ClickDistanceThreshold"/> spawns a ghost on the drag
/// layer, highlights any <see cref="IVotingDropTarget"/> underneath, and commits the
/// drop on release or snaps back to the grid.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class VotingDragHandler : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public VotingController controller;

    /// <summary>
    /// World-space position of this card inside the grid, cached by
    /// <see cref="VotingController.InitializeHandlers"/> and used to snap the card
    /// home when deselected.
    /// </summary>
    [HideInInspector] public Vector3 homePosition;

    /// <summary>0-based index into the controller's active group list.</summary>
    [HideInInspector] public int slotIndex;

    /// <summary>
    /// True if this handler is a DM-mode clone, instantiated so the same group can be
    /// voted for twice. Clones are destroyed when the original voted copy leaves its slot.
    /// </summary>
    [HideInInspector] public bool isClone;

    private const float ClickDistanceThreshold = 10f;

    private CanvasGroup       canvasGroup;
    private RectTransform     rectTransform;
    private Image             cardImage;
    private Color             originalCardColor;

    private static readonly Color DisplacedTint = new Color(0.82f, 0.82f, 0.82f);

    private RectTransform     ghostRect;
    private IVotingDropTarget hoveredTarget;
    private bool isDragging;
    private bool wasDrag;
    private Vector2 pointerDownPosition;

    void Awake()
    {
        canvasGroup   = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        cardImage     = GetComponent<Image>();
        if (cardImage != null) originalCardColor = cardImage.color;
    }

    /// <summary>
    /// Tints the card to indicate displacement when another card is hovering over the
    /// slot it currently occupies. Invoked by <see cref="VotingSlotDropTarget"/>.
    /// </summary>
    public void SetDisplacedVisual(bool displaced)
    {
        if (cardImage != null)
            cardImage.color = displaced ? DisplacedTint : originalCardColor;
    }

    void OnDisable()
    {
        ClearHover();
        CleanupDrag(restoreVisuals: true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDownPosition = eventData.position;
        isDragging = false;
        wasDrag    = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (wasDrag) return;

        float distance = Vector2.Distance(eventData.position, pointerDownPosition);
        if (distance < ClickDistanceThreshold)
            controller?.OnGroupClicked(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Vector2.Distance(eventData.position, pointerDownPosition) < ClickDistanceThreshold)
            return;

        isDragging = true;
        wasDrag    = true;

        canvasGroup.alpha          = 0.4f;
        canvasGroup.blocksRaycasts = false;

        ghostRect = CreateGhost();

        controller?.OnDragBegin(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || ghostRect == null || controller?.dragLayer == null) return;

        Canvas rootCanvas = controller.dragLayer.GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas != null)
        {
            Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera;

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    controller.dragLayer, eventData.position, cam, out Vector3 worldPoint))
            {
                ghostRect.position = worldPoint;
            }
        }

        UpdateHoveredTarget(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        IVotingDropTarget dropTarget = hoveredTarget;
        ClearHover();
        CleanupDrag(restoreVisuals: true);

        if (dropTarget != null)
            dropTarget.OnVotingDropped(this);
        else
            controller?.ReturnToHome(this);
    }

    private void UpdateHoveredTarget(PointerEventData eventData)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, hits);

        // Two-pass detection avoids a false positive: group cards placed in slots are
        // still parented under the button grid, so a parent-walk from a card would hit
        // VotingGridDropTarget and fire ReturnToGrid instead of PlaceInSlot.

        // Pass 1: direct hits only. VotingSlotDropTarget lives on its own GameObject.
        IVotingDropTarget newTarget = null;
        foreach (var hit in hits)
        {
            if (ghostRect != null && hit.gameObject.transform.IsChildOf(ghostRect)) continue;
            var direct = hit.gameObject.GetComponent<IVotingDropTarget>();
            if (direct != null) { newTarget = direct; break; }
        }

        // Pass 2: parent-walk fallback, catching VotingGridDropTarget when the ghost is
        // over empty grid space.
        if (newTarget == null)
        {
            foreach (var hit in hits)
            {
                if (ghostRect != null && hit.gameObject.transform.IsChildOf(ghostRect)) continue;
                var comp = hit.gameObject.GetComponentInParent(typeof(IVotingDropTarget));
                if (comp is IVotingDropTarget target) { newTarget = target; break; }
            }
        }

        if (newTarget == hoveredTarget) return;

        hoveredTarget?.OnDragHoverExit();
        hoveredTarget = newTarget;
        hoveredTarget?.OnDragHoverEnter(this);
    }

    private void ClearHover()
    {
        hoveredTarget?.OnDragHoverExit();
        hoveredTarget = null;
    }

    private void CleanupDrag(bool restoreVisuals)
    {
        isDragging = false;

        if (ghostRect != null)
        {
            Destroy(ghostRect.gameObject);
            ghostRect = null;
        }

        if (restoreVisuals && canvasGroup != null)
        {
            canvasGroup.alpha          = 1f;
            canvasGroup.blocksRaycasts = true;
        }
    }

    private RectTransform CreateGhost()
    {
        if (controller?.dragLayer == null) return null;

        GameObject ghost = Instantiate(gameObject, controller.dragLayer);
        ghost.name = "VotingDragGhost";

        foreach (var handler in ghost.GetComponentsInChildren<VotingDragHandler>(includeInactive: true))
            DestroyImmediate(handler);
        foreach (var cg in ghost.GetComponentsInChildren<CanvasGroup>(includeInactive: true))
            DestroyImmediate(cg);

        CanvasGroup ghostCG = ghost.AddComponent<CanvasGroup>();
        ghostCG.alpha          = 0.75f;
        ghostCG.blocksRaycasts = false;
        ghostCG.interactable   = false;

        RectTransform rt = ghost.GetComponent<RectTransform>();
        rt.sizeDelta = rectTransform.sizeDelta;
        rt.position  = rectTransform.position;

        return rt;
    }
}
