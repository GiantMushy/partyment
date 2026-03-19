using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Attach to each group button card in the Voting screen.
///
/// Handles both interactions:
///   Click  — short press that never exceeds <see cref="ClickDistanceThreshold"/>.
///            Calls <see cref="VotingController.OnGroupClicked"/> to toggle
///            the card between the grid and the topmost free vote slot.
///   Drag   — press that moves beyond the threshold before release.
///            Spawns a semi-transparent ghost on the drag layer, moves it with the
///            pointer, highlights any <see cref="IVotingDropTarget"/> underneath, and
///            commits the drop (or snaps the card home) on release.
///
/// Setup checklist
/// ───────────────
/// • A CanvasGroup is required on this GameObject (enforced by RequireComponent).
/// • <see cref="controller"/> and <see cref="homePosition"/> are injected at runtime
///   by <see cref="VotingController.InitializeHandlers"/>; do not set them manually.
/// • <see cref="slotIndex"/> identifies which group button (0-based) this handler
///   represents in the active group list.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class VotingDragHandler : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // ================================================================
    //  Injected at runtime by VotingController.InitializeHandlers()
    // ================================================================

    /// <summary>Owning controller — injected before first use.</summary>
    [HideInInspector] public VotingController controller;

    /// <summary>
    /// World-space position of this card inside the grid.
    /// Cached once in <see cref="VotingController.InitializeHandlers"/> and
    /// used to snap the card home when it is deselected.
    /// </summary>
    [HideInInspector] public Vector3 homePosition;

    /// <summary>0-based index into VotingController's active group list.</summary>
    [HideInInspector] public int slotIndex;

    /// <summary>
    /// True if this handler is a DM-mode clone (instantiated so the same group
    /// can be voted for a second time). Clones are destroyed when the original
    /// voted copy is removed from its slot.
    /// </summary>
    [HideInInspector] public bool isClone;

    // ================================================================
    //  Constants
    // ================================================================

    private const float ClickDistanceThreshold = 10f;

    // ================================================================
    //  Private state
    // ================================================================

    private CanvasGroup   canvasGroup;
    private RectTransform rectTransform;
    private RectTransform ghostRect;
    private IVotingDropTarget hoveredTarget;
    private bool isDragging;
    private bool wasDrag;
    private Vector2 pointerDownPosition;

    // ================================================================
    //  Unity lifecycle
    // ================================================================

    void Awake()
    {
        canvasGroup   = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
    }

    void OnDisable()
    {
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

    public void OnPointerUp(PointerEventData eventData)
    {
        if (wasDrag) return;

        float distance = Vector2.Distance(eventData.position, pointerDownPosition);
        if (distance < ClickDistanceThreshold)
            controller?.OnGroupClicked(this);
    }

    // ================================================================
    //  Drag events
    // ================================================================

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

    // ================================================================
    //  Hover detection
    // ================================================================

    private void UpdateHoveredTarget(PointerEventData eventData)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, hits);

        IVotingDropTarget newTarget = null;
        foreach (var hit in hits)
        {
            if (ghostRect != null && hit.gameObject.transform.IsChildOf(ghostRect)) continue;

            var comp = hit.gameObject.GetComponentInParent(typeof(IVotingDropTarget));
            if (comp is IVotingDropTarget target)
            {
                newTarget = target;
                break;
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
