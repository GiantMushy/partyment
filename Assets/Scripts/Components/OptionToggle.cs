using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Three-button radio-style toggle. Exactly one option is selected at a time;
/// a highlight Image lerps to the chosen button. Each option fires its own
/// <see cref="UnityEvent"/> on selection. Defaults to option 2 (middle) on Start.
/// </summary>
public class OptionToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject highlightImage;
    [SerializeField] private GameObject option1Button;
    [SerializeField] private GameObject option2Button;
    [SerializeField] private GameObject option3Button;

    [Header("Settings")]
    [SerializeField] private float highlightMoveSpeed = 10f;

    [Header("Callbacks")]
    [SerializeField] private UnityEvent onOption1Selected;
    [SerializeField] private UnityEvent onOption2Selected;
    [SerializeField] private UnityEvent onOption3Selected;

    private GameObject currentSelectedButton;
    private RectTransform highlightRect;
    private RectTransform highlightParentRect;
    private Canvas rootCanvas;
    private Camera uiCamera;
    private Vector2 highlightTargetAnchoredPosition;

    void Awake()
    {
        if (highlightImage != null)
        {
            highlightRect = highlightImage.GetComponent<RectTransform>();
            highlightParentRect = highlightRect != null ? highlightRect.parent as RectTransform : null;
            rootCanvas = highlightImage.GetComponentInParent<Canvas>();
            uiCamera = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? rootCanvas.worldCamera
                : null;
        }
    }

    void Start()
    {
        currentSelectedButton = option2Button;
        MoveHighlightInstantly(currentSelectedButton);
    }

    void Update()
    {
        if (highlightRect == null || currentSelectedButton == null)
            return;

        // Tracks selected button position in UI space to follow ScrollRect movement.
        highlightTargetAnchoredPosition = GetTargetAnchoredPosition(currentSelectedButton);
        highlightRect.anchoredPosition = Vector2.Lerp(
            highlightRect.anchoredPosition,
            highlightTargetAnchoredPosition,
            Time.deltaTime * highlightMoveSpeed
        );
    }

    private Vector2 GetTargetAnchoredPosition(GameObject target)
    {
        if (target == null || highlightParentRect == null)
            return Vector2.zero;

        RectTransform targetRect = target.GetComponent<RectTransform>();
        if (targetRect == null)
            return Vector2.zero;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, targetRect.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            highlightParentRect,
            screenPoint,
            uiCamera,
            out Vector2 localPoint
        );
        return localPoint;
    }

    private void UpdateHighlightTargetToSelected()
    {
        if (highlightRect == null || currentSelectedButton == null)
            return;

        highlightTargetAnchoredPosition = GetTargetAnchoredPosition(currentSelectedButton);
    }

    public void Option1()
    {
        if (currentSelectedButton == option1Button) return;
        currentSelectedButton = option1Button;
        UpdateHighlightTargetToSelected();
        onOption1Selected?.Invoke();
    }

    public void Option2()
    {
        if (currentSelectedButton == option2Button) return;
        currentSelectedButton = option2Button;
        UpdateHighlightTargetToSelected();
        onOption2Selected?.Invoke();
    }

    public void Option3()
    {
        if (currentSelectedButton == option3Button) return;
        currentSelectedButton = option3Button;
        UpdateHighlightTargetToSelected();
        onOption3Selected?.Invoke();
    }

    private void MoveHighlightInstantly(GameObject target)
    {
        if (highlightRect == null || target == null) return;
        highlightTargetAnchoredPosition = GetTargetAnchoredPosition(target);
        highlightRect.anchoredPosition = highlightTargetAnchoredPosition;
    }

    /// <summary>Returns true if option 1 is currently selected.</summary>
    public bool IsOption1Selected() => currentSelectedButton == option1Button;

    /// <summary>Returns true if option 2 is currently selected.</summary>
    public bool IsOption2Selected() => currentSelectedButton == option2Button;

    /// <summary>Returns true if option 3 is currently selected.</summary>
    public bool IsOption3Selected() => currentSelectedButton == option3Button;
}
