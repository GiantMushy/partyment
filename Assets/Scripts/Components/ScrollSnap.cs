using UnityEngine;

/// <summary>
/// ⚠️ <b>BROKEN — does not actually snap.</b> Intended to attach to a <see cref="UnityEngine.UI.ScrollRect"/>
/// and snap its normalized position to one of <c>numSnapPositions</c> evenly-spaced points
/// when the user releases the drag. The current implementation never produces a snap;
/// rewrite with a better model (e.g. detect drag-end via <see cref="UnityEngine.EventSystems.IEndDragHandler"/>
/// and lerp explicitly instead of polling velocity).
/// </summary>
// TODO: THIS DOES NOT WORK AT ALL, NO SNAPING OCCURS, FIX WITH BETTER MODEL
public class ScrollSnap : MonoBehaviour
{
    [Header("Snapping")]
    [Range(0.01f, 1f)]
    public float snapSpeed = 0.2f;
    public float snapThreshold = 0.1f; // How close to a snap point before snapping

    public enum SnapDirection { Horizontal, Vertical }

    [Header("ScrollSnap Settings")]
    public SnapDirection direction = SnapDirection.Horizontal;
    public int numSnapPositions = 3;

    [Header("References")]
    public RectTransform content; // The content RectTransform (with LayoutGroup)

    private UnityEngine.UI.ScrollRect scrollRect;
    private float[] snapPoints; // Normalized positions
    private bool isDragging = false;
    private float targetNormalizedPos;
    private float contentLength; // width or height depending on direction

    void Awake()
    {
        scrollRect = GetComponent<UnityEngine.UI.ScrollRect>();
    }

    void Start()
    {
        if (content == null)
        {
            var sr = GetComponent<UnityEngine.UI.ScrollRect>();
            if (sr != null)
                content = sr.content;
        }
        if (content != null)
        {
            contentLength = (direction == SnapDirection.Horizontal)
                ? content.rect.width
                : content.rect.height;

            // Calculate snap points in normalized space
            snapPoints = new float[numSnapPositions];
            for (int i = 0; i < numSnapPositions; i++)
            {
                snapPoints[i] = (numSnapPositions == 1) ? 0f : (float)i / (numSnapPositions - 1);
            }
        }
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }
        targetNormalizedPos = GetCurrentNormalizedPos();
    }

    void OnDestroy()
    {
        if (scrollRect != null)
            scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
    }

    private void OnScrollValueChanged(Vector2 val)
    {
        // Detect drag state
        isDragging = scrollRect != null && scrollRect.velocity.magnitude > 0.01f;
    }

    void Update()
    {
        if (scrollRect == null || snapPoints == null || snapPoints.Length == 0)
            return;

        // If not dragging and not already at a snap point, lerp to nearest
        if (!isDragging && !IsAtSnapPoint())
        {
            float current = GetCurrentNormalizedPos();
            float newVal = Mathf.Lerp(current, targetNormalizedPos, Time.deltaTime / snapSpeed);
            SetNormalizedPos(newVal);
        }

        // If user just released drag, snap to nearest
        if (!isDragging && !IsAtSnapPoint() && Application.isFocused)
        {
            targetNormalizedPos = FindNearestSnapPoint();
        }
    }

    private float GetCurrentNormalizedPos()
    {
        return (direction == SnapDirection.Horizontal)
            ? scrollRect.horizontalNormalizedPosition
            : scrollRect.verticalNormalizedPosition;
    }

    private void SetNormalizedPos(float val)
    {
        if (direction == SnapDirection.Horizontal)
            scrollRect.horizontalNormalizedPosition = val;
        else
            scrollRect.verticalNormalizedPosition = val;
    }

    private float FindNearestSnapPoint()
    {
        float current = GetCurrentNormalizedPos();
        float minDist = float.MaxValue;
        float nearest = current;
        foreach (var snap in snapPoints)
        {
            float dist = Mathf.Abs(current - snap);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = snap;
            }
        }
        return nearest;
    }

    private bool IsAtSnapPoint()
    {
        float current = GetCurrentNormalizedPos();
        foreach (var snap in snapPoints)
        {
            if (Mathf.Abs(current - snap) < snapThreshold)
                return true;
        }
        return false;
    }
}
