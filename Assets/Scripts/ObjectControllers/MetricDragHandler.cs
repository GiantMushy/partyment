using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using Metric = GameManager.Metric;

/// <summary>
/// Attached to each metric card. A short press (within <see cref="ClickDistanceThreshold"/>)
/// is treated as a click and forwarded to
/// <see cref="MetricSelectionController.OnMetricClicked"/>. A press that exceeds the
/// threshold spawns a semi-transparent ghost on the drag layer, highlights any
/// <see cref="IMetricDropTarget"/> beneath the pointer, and commits the drop on release
/// or snaps back to the grid.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class MetricDragHandler : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>Which metric this card represents.</summary>
    [Tooltip("Which metric this card represents.")]
    public Metric metric;

    [HideInInspector] public MetricSelectionController controller;

    /// <summary>
    /// World-space position of this card inside the grid, cached by
    /// <see cref="MetricSelectionController.InitializeHandlers"/> and used to snap the
    /// card home when deselected.
    /// </summary>
    [HideInInspector] public Vector3 homePosition;

    /// <summary>
    /// Pixel distance the pointer must travel before the interaction is classified as
    /// a drag instead of a click. Used as a guard on top of Unity's own EventSystem
    /// drag threshold.
    /// </summary>
    private const float ClickDistanceThreshold = 10f;

    private CanvasGroup   canvasGroup;
    private RectTransform rectTransform;
    private Image         cardImage;
    private Color         originalCardColor;

    private static readonly Color DisplacedTint = new Color(0.82f, 0.82f, 0.82f);

    private RectTransform ghostRect;
    private IMetricDropTarget hoveredTarget;
    private bool isDragging;

    /// <summary>
    /// Set when OnBeginDrag fires, cleared on the next OnPointerDown. Lets OnPointerUp
    /// distinguish a completed drag from a click regardless of event ordering between
    /// OnEndDrag and OnPointerUp.
    /// </summary>
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
    /// slot it currently occupies. Invoked by <see cref="VoteSlotDropTarget"/>.
    /// </summary>
    public void SetDisplacedVisual(bool displaced)
    {
        if (cardImage != null)
            cardImage.color = displaced ? DisplacedTint : originalCardColor;
    }

    void OnDisable()
    {
        // Recovers ghost and visuals when the screen is deactivated mid-drag.
        ClearHover();
        CleanupDrag(restoreVisuals: true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDownPosition = eventData.position;
        isDragging = false;
        wasDrag    = false;
    }

    /// <summary>
    /// Fires before OnEndDrag on the same frame as pointer release. Skips click
    /// handling when a drag was in progress so OnEndDrag manages the drop.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (wasDrag) return;

        float distance = Vector2.Distance(eventData.position, pointerDownPosition);
        if (distance < ClickDistanceThreshold)
            controller?.OnMetricClicked(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Confirms the pointer moved far enough before initiating a drag.
        if (Vector2.Distance(eventData.position, pointerDownPosition) < ClickDistanceThreshold)
            return;

        isDragging = true;
        wasDrag    = true;

        // Fades the original so the slot or grid position remains as a visual anchor.
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

        IMetricDropTarget dropTarget = hoveredTarget;
        ClearHover();
        CleanupDrag(restoreVisuals: true);

        if (dropTarget != null)
            dropTarget.OnMetricDropped(this);
        else
            controller?.ReturnToHome(this);
    }

    private void UpdateHoveredTarget(PointerEventData eventData)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, hits);

        // Two-pass detection avoids a false positive: metric cards placed in slots are
        // still parented under the MetricGrid, so a parent-walk from a card would hit
        // MetricGridDropTarget and fire ReturnToGrid instead of PlaceInSlot.

        // Pass 1: direct hits only. VoteSlotDropTarget lives on its own GameObject.
        IMetricDropTarget newTarget = null;
        foreach (var hit in hits)
        {
            if (ghostRect != null && hit.gameObject.transform.IsChildOf(ghostRect)) continue;
            var direct = hit.gameObject.GetComponent<IMetricDropTarget>();
            if (direct != null) { newTarget = direct; break; }
        }

        // Pass 2: parent-walk fallback, catching MetricGridDropTarget when the ghost is
        // over empty grid space.
        if (newTarget == null)
        {
            foreach (var hit in hits)
            {
                if (ghostRect != null && hit.gameObject.transform.IsChildOf(ghostRect)) continue;
                var comp = hit.gameObject.GetComponentInParent(typeof(IMetricDropTarget));
                if (comp is IMetricDropTarget target) { newTarget = target; break; }
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

    /// <summary>Destroys the ghost and optionally restores the original card's visuals.</summary>
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

    /// <summary>
    /// Creates a semi-transparent, non-interactive clone of this card on the drag
    /// layer so it renders above all other UI. All MetricDragHandler and CanvasGroup
    /// components are stripped from the clone so it cannot initiate or receive drag
    /// events.
    /// </summary>
    private RectTransform CreateGhost()
    {
        if (controller?.dragLayer == null) return null;

        GameObject ghost = Instantiate(gameObject, controller.dragLayer);
        ghost.name = "MetricDragGhost";

        foreach (var handler in ghost.GetComponentsInChildren<MetricDragHandler>(includeInactive: true))
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
