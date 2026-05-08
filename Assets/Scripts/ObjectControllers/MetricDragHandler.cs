using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using Metric = GameManager.Metric;

/// <summary>
/// Attach to each metric card GameObject (Comedy, Creativity, OnTopic, Factual, Enthusiasm).
///
/// Handles both interactions:
///   Click  — short press that never exceeds <see cref="ClickDistanceThreshold"/>.
///            Calls <see cref="MetricSelectionController.OnMetricClicked"/> to toggle
///            the card between the grid and the topmost free vote slot.
///   Drag   — press that moves beyond the threshold before release.
///            Spawns a semi-transparent ghost on the drag layer, moves it with the
///            pointer, highlights any <see cref="IMetricDropTarget"/> underneath, and
///            commits the drop (or snaps the card home) on release.
///
/// Setup checklist
/// ───────────────
/// • A CanvasGroup is required on this GameObject (enforced by RequireComponent).
/// • Set <see cref="metric"/> in the Inspector to the correct GameManager.Metric value.
/// • Remove any onClick listeners that were previously wired to Toggle* methods
///   on <see cref="MetricSelectionController"/> — this handler owns click logic now.
/// • <see cref="controller"/> and <see cref="homePosition"/> are injected at runtime
///   by <see cref="MetricSelectionController.InitializeHandlers"/>; do not set them manually.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class MetricDragHandler : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // ================================================================
    //  Inspector
    // ================================================================

    /// <summary>Which metric this card represents. Set this in the Inspector.</summary>
    [Tooltip("Which metric this card represents.")]
    public Metric metric;

    // ================================================================
    //  Injected at runtime by MetricSelectionController.InitializeHandlers()
    // ================================================================

    /// <summary>Owning controller — injected before first use.</summary>
    [HideInInspector] public MetricSelectionController controller;

    /// <summary>
    /// World-space position of this card inside the grid.
    /// Cached once in <see cref="MetricSelectionController.InitializeHandlers"/> and
    /// used to snap the card home when it is deselected.
    /// </summary>
    [HideInInspector] public Vector3 homePosition;

    // ================================================================
    //  Constants
    // ================================================================

    /// <summary>
    /// Screen-space pixel distance the pointer must travel before the interaction
    /// is classified as a drag instead of a click.  Unity's own EventSystem drag
    /// threshold must also be exceeded for OnBeginDrag to fire; this is an
    /// additional guard used in OnPointerUp.
    /// </summary>
    private const float ClickDistanceThreshold = 10f;

    // ================================================================
    //  Private state
    // ================================================================

    private CanvasGroup   canvasGroup;
    private RectTransform rectTransform;
    private Image         cardImage;
    private Color         originalCardColor;

    private static readonly Color DisplacedTint = new Color(0.82f, 0.82f, 0.82f);

    /// <summary>Semi-transparent clone parented to the drag layer during a drag.</summary>
    private RectTransform ghostRect;

    /// <summary>The <see cref="IMetricDropTarget"/> currently under the ghost; null if none.</summary>
    private IMetricDropTarget hoveredTarget;

    /// <summary>True while a drag is in progress (set by OnBeginDrag, cleared by OnEndDrag).</summary>
    private bool isDragging;

    /// <summary>
    /// Set true the moment OnBeginDrag fires and cleared on OnPointerDown.
    /// Used by OnPointerUp to distinguish a completed drag from a pure click,
    /// regardless of the event ordering between OnEndDrag and OnPointerUp.
    /// </summary>
    private bool wasDrag;

    /// <summary>Screen position recorded on pointer-down for click/drag classification.</summary>
    private Vector2 pointerDownPosition;

    // ================================================================
    //  Unity lifecycle
    // ================================================================

    void Awake()
    {
        canvasGroup   = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        cardImage     = GetComponent<Image>();
        if (cardImage != null) originalCardColor = cardImage.color;
    }

    /// <summary>
    /// Tints the card to indicate it will be displaced when another card is hovering
    /// over the slot it currently occupies.  Called by <see cref="VoteSlotDropTarget"/>.
    /// </summary>
    public void SetDisplacedVisual(bool displaced)
    {
        if (cardImage != null)
            cardImage.color = displaced ? DisplacedTint : originalCardColor;
    }

    void OnDisable()
    {
        // Safety net: if the screen is deactivated mid-drag, destroy the ghost
        // and restore the card so nothing lingers on screen.
        ClearHover();
        CleanupDrag(restoreVisuals: true);
    }

    // ================================================================
    //  Pointer events
    // ================================================================

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDownPosition = eventData.position;
        isDragging = false;
        wasDrag    = false;
    }

    /// <summary>
    /// Called by the EventSystem BEFORE OnEndDrag on the same frame the pointer
    /// is released.  If a drag was in progress (<see cref="wasDrag"/> is true) we
    /// skip click handling entirely — OnEndDrag will manage the drop.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (wasDrag) return;

        float distance = Vector2.Distance(eventData.position, pointerDownPosition);
        if (distance < ClickDistanceThreshold)
            controller?.OnMetricClicked(this);
    }

    // ================================================================
    //  Drag events
    // ================================================================

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Belt-and-suspenders: ensure the pointer actually moved enough.
        if (Vector2.Distance(eventData.position, pointerDownPosition) < ClickDistanceThreshold)
            return;

        isDragging = true;
        wasDrag    = true;

        // Fade the original so the slot / grid position remains as a visual anchor.
        canvasGroup.alpha          = 0.4f;
        canvasGroup.blocksRaycasts = false;

        // Spawn the ghost on the drag layer so it renders above all other UI.
        ghostRect = CreateGhost();

        controller?.OnDragBegin(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || ghostRect == null || controller?.dragLayer == null) return;

        // Move the ghost to follow the pointer in world space.
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

        // Highlight whatever IMetricDropTarget is under the ghost.
        UpdateHoveredTarget(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        // Capture target before ClearHover nulls it.
        IMetricDropTarget dropTarget = hoveredTarget;
        ClearHover();
        CleanupDrag(restoreVisuals: true);

        if (dropTarget != null)
            dropTarget.OnMetricDropped(this);       // valid drop → delegate to target
        else
            controller?.ReturnToHome(this);          // invalid drop → snap back
    }

    // ================================================================
    //  Hover detection (called every frame during a drag)
    // ================================================================

    private void UpdateHoveredTarget(PointerEventData eventData)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, hits);

        // Two-pass detection prevents a false-positive: metric cards placed in slots are
        // still parented under the MetricGrid, so GetComponentInParent from a card would
        // climb up to MetricGridDropTarget and fire ReturnToGrid instead of PlaceInSlot.
        //
        // Pass 1 — direct hits only (VoteSlotDropTarget is always directly on its GameObject).
        IMetricDropTarget newTarget = null;
        foreach (var hit in hits)
        {
            if (ghostRect != null && hit.gameObject.transform.IsChildOf(ghostRect)) continue;
            var direct = hit.gameObject.GetComponent<IMetricDropTarget>();
            if (direct != null) { newTarget = direct; break; }
        }

        // Pass 2 — parent-walk fallback (catches MetricGridDropTarget when the ghost is
        //           over empty grid space that has no metric card directly under the pointer).
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

    // ================================================================
    //  Internal helpers
    // ================================================================

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
    /// Creates a semi-transparent, non-interactive clone of this card parented
    /// to the drag layer so it renders above all other UI panels.
    /// All <see cref="MetricDragHandler"/> components and CanvasGroups are stripped
    /// from the clone so it cannot itself initiate or receive drag events.
    /// </summary>
    private RectTransform CreateGhost()
    {
        if (controller?.dragLayer == null) return null;

        GameObject ghost = Instantiate(gameObject, controller.dragLayer);
        ghost.name = "MetricDragGhost";

        // Strip logic that must not run on the clone.
        foreach (var handler in ghost.GetComponentsInChildren<MetricDragHandler>(includeInactive: true))
            DestroyImmediate(handler);
        foreach (var cg in ghost.GetComponentsInChildren<CanvasGroup>(includeInactive: true))
            DestroyImmediate(cg);

        // Semi-transparent, passes all raycasts through so it never interferes
        // with hover detection on the targets beneath it.
        CanvasGroup ghostCG = ghost.AddComponent<CanvasGroup>();
        ghostCG.alpha          = 0.75f;
        ghostCG.blocksRaycasts = false;
        ghostCG.interactable   = false;

        // Match source card size and start at its current world position.
        RectTransform rt = ghost.GetComponent<RectTransform>();
        rt.sizeDelta = rectTransform.sizeDelta;
        rt.position  = rectTransform.position;

        return rt;
    }
}
