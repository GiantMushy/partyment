using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Attach this directly to the NameInGroupPrefab root.
/// Dragging can be initiated from anywhere on the card.
/// Communicates drag lifecycle events back to AssignGroupsController.
/// </summary>
public class DragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // ---- Runtime Reference ----
    /// <summary>The RectTransform of this card (set automatically on Initialize).</summary>
    [HideInInspector] public RectTransform nameCard;

    // ---- Runtime State ----
    private AssignGroupsController controller;
    private Canvas rootCanvas;
    private RectTransform ghostRect;
    private CanvasGroup cardCanvasGroup;
    private Transform originalParent;
    private int originalSiblingIndex;

    /// <summary>Lazily resolves the CanvasGroup on the name card.</summary>
    private CanvasGroup GetCardCanvasGroup()
    {
        if (cardCanvasGroup == null && nameCard != null)
        {
            cardCanvasGroup = nameCard.GetComponent<CanvasGroup>();
            if (cardCanvasGroup == null)
                cardCanvasGroup = nameCard.gameObject.AddComponent<CanvasGroup>();
        }
        return cardCanvasGroup;
    }

    /// <summary>
    /// Allow the controller to inject itself if the handle is instantiated
    /// before being parented under the controller hierarchy.
    /// </summary>
    public void Initialize(AssignGroupsController ctrl)
    {
        controller = ctrl;
    }

    // ----------------------------------------------------------------
    // IDragHandler implementation
    // ----------------------------------------------------------------

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (controller == null || nameCard == null) return;

        // Cache the root canvas from the drag layer (where the ghost lives)
        rootCanvas = controller.dragLayer.GetComponentInParent<Canvas>();
        if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;

        // Remember where the card came from so we can return it on invalid drop
        originalParent = nameCard.parent;
        originalSiblingIndex = nameCard.GetSiblingIndex();

        // Fade the original card and let raycasts pass through it
        var cg = GetCardCanvasGroup();
        if (cg != null)
        {
            cg.alpha = 0.4f;
            cg.blocksRaycasts = false;
        }

        // Create a ghost clone parented to the drag layer
        ghostRect = CreateGhost();

        // Notify the controller
        controller.OnCardDragBegin(nameCard, originalParent);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ghostRect == null || rootCanvas == null) return;

        // Determine the correct camera for the canvas render mode
        Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : rootCanvas.worldCamera;

        // Move the ghost to follow the pointer using world-space coordinates
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                controller.dragLayer, eventData.position, cam, out Vector3 worldPoint))
        {
            ghostRect.position = worldPoint;
        }

        // Let the controller highlight whichever group is under the pointer
        controller.OnCardDragUpdate(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Restore card visuals
        var cg = GetCardCanvasGroup();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
        }

        // Destroy the ghost
        if (ghostRect != null)
        {
            Destroy(ghostRect.gameObject);
            ghostRect = null;
        }

        // Let the controller decide where the card lands
        if (controller != null && nameCard != null)
            controller.OnCardDrop(nameCard, originalParent, originalSiblingIndex, eventData);
    }

    /// <summary>
    /// Safety net: if the page is disabled mid-drag (e.g. state change),
    /// clean up the ghost so it doesn't linger on screen.
    /// </summary>
    void OnDisable()
    {
        if (ghostRect != null)
        {
            Destroy(ghostRect.gameObject);
            ghostRect = null;
        }

        var cg = GetCardCanvasGroup();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
        }
    }

    // ----------------------------------------------------------------
    // Ghost creation helper
    // ----------------------------------------------------------------

    /// <summary>
    /// Instantiates a semi-transparent copy of the name card
    /// parented to the top-level drag layer so it renders above everything.
    /// </summary>
    private RectTransform CreateGhost()
    {
        GameObject ghost = Instantiate(nameCard.gameObject, controller.dragLayer);
        ghost.name = "DragGhost";

        // Strip interactivity from the ghost immediately
        // (DestroyImmediate prevents the cloned handlers from interfering this frame)
        foreach (var handler in ghost.GetComponentsInChildren<DragHandle>(true))
            DestroyImmediate(handler);
        foreach (var cg in ghost.GetComponentsInChildren<CanvasGroup>(true))
            DestroyImmediate(cg);

        // Make the ghost semi-transparent
        CanvasGroup ghostCG = ghost.AddComponent<CanvasGroup>();
        ghostCG.alpha = 0.7f;
        ghostCG.blocksRaycasts = false;
        ghostCG.interactable = false;

        // Match the source card size
        RectTransform rt = ghost.GetComponent<RectTransform>();
        rt.sizeDelta = nameCard.sizeDelta;

        // Position at the card's current screen position
        rt.position = nameCard.position;

        return rt;
    }
}
