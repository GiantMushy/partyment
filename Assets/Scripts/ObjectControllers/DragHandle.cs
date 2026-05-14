using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Attached to the NameInGroupPrefab root. Dragging can be initiated from anywhere on
/// the card and lifecycle events are forwarded to <see cref="AssignGroupsController"/>.
/// </summary>
public class DragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>The RectTransform of this card, set on Initialize.</summary>
    [HideInInspector] public RectTransform nameCard;

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
    /// Injects the owning controller when the handle is instantiated outside the
    /// controller hierarchy.
    /// </summary>
    public void Initialize(AssignGroupsController ctrl)
    {
        controller = ctrl;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (controller == null || nameCard == null) return;

        rootCanvas = controller.dragLayer.GetComponentInParent<Canvas>();
        if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;

        // Cached so an invalid drop can return the card to its origin.
        originalParent = nameCard.parent;
        originalSiblingIndex = nameCard.GetSiblingIndex();

        var cg = GetCardCanvasGroup();
        if (cg != null)
        {
            cg.alpha = 0.4f;
            cg.blocksRaycasts = false;
        }

        ghostRect = CreateGhost();

        controller.OnCardDragBegin(nameCard, originalParent);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ghostRect == null || rootCanvas == null) return;

        Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : rootCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                controller.dragLayer, eventData.position, cam, out Vector3 worldPoint))
        {
            ghostRect.position = worldPoint;
        }

        controller.OnCardDragUpdate(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        var cg = GetCardCanvasGroup();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
        }

        if (ghostRect != null)
        {
            Destroy(ghostRect.gameObject);
            ghostRect = null;
        }

        if (controller != null && nameCard != null)
            controller.OnCardDrop(nameCard, originalParent, originalSiblingIndex, eventData);
    }

    /// <summary>
    /// Cleans up the ghost if the page is disabled mid-drag so it does not linger
    /// on screen.
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

    /// <summary>
    /// Instantiates a semi-transparent copy of the name card parented to the top-level
    /// drag layer so it renders above all other UI.
    /// </summary>
    private RectTransform CreateGhost()
    {
        GameObject ghost = Instantiate(nameCard.gameObject, controller.dragLayer);
        ghost.name = "DragGhost";

        // DestroyImmediate keeps the cloned handlers from firing on the current frame.
        foreach (var handler in ghost.GetComponentsInChildren<DragHandle>(true))
            DestroyImmediate(handler);
        foreach (var cg in ghost.GetComponentsInChildren<CanvasGroup>(true))
            DestroyImmediate(cg);

        CanvasGroup ghostCG = ghost.AddComponent<CanvasGroup>();
        ghostCG.alpha = 0.7f;
        ghostCG.blocksRaycasts = false;
        ghostCG.interactable = false;

        RectTransform rt = ghost.GetComponent<RectTransform>();
        rt.sizeDelta = nameCard.sizeDelta;
        rt.position = nameCard.position;

        return rt;
    }
}
