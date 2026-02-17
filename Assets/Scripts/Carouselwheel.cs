using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PackCarouselLoop : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [Header("Scroll")]
    public ScrollRect scrollRect;
    public RectTransform viewport;
    public RectTransform content;

    [Header("Data")]
    public PackData[] packs;

    [Header("UI")]
    public PackItemUI itemPrefab;
    public Image background;

    [Header("Loop")]
    [Tooltip("How many items duplicated on EACH side. For 1-2-3-4-5-1-2 set this to 2.")]
    public int buffer = 2;

    [Header("Feel")]
    public float snapSpeed = 14f;
    public float colorLerpSpeed = 6f;
    public float scaleLerpSpeed = 12f;
    public float selectedScale = 1.15f;
    public float unselectedScale = 0.85f;

    private readonly List<PackItemUI> items = new();
    private bool dragging;

    private float step;          // item width + spacing
    private int n;               // packs length
    private int selectedVirtual; // index inside items list
    private HorizontalLayoutGroup hlg;

    void Start()
    {
        hlg = content.GetComponent<HorizontalLayoutGroup>();
        n = (packs == null) ? 0 : packs.Length;
        if (n <= 0 || itemPrefab == null) return;

        Build();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        ComputeStep();

        // Start centered on first real item (buffer)
        CenterOnVirtualIndex(buffer);
        UpdateVisuals(true);
    }

void Update()
{
    if (n <= 0 || items.Count == 0) return;

    // Find closest-to-center item FIRST
    selectedVirtual = ClosestToCenterVirtualIndex();

    // Then keep the illusion of infinite scrolling
    SeamlessLoop();

    // Background + scaling
    UpdateVisuals(false);

    // Snap when NOT dragging
    if (!dragging)
        SnapTo(selectedVirtual);
}


    public void OnBeginDrag(PointerEventData eventData) => dragging = true;
    public void OnEndDrag(PointerEventData eventData) => dragging = false;

    void Build()
    {
        // Clear old
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
        items.Clear();

        // Create virtual list: [-buffer ... n+buffer-1]
        for (int v = -buffer; v < n + buffer; v++)
        {
            int real = Mod(v, n);
            var it = Instantiate(itemPrefab, content);
            it.Setup(packs[real]);
            items.Add(it);
        }
    }

void ComputeStep()
{
    Canvas.ForceUpdateCanvases();
    LayoutRebuilder.ForceRebuildLayoutImmediate(content);

    var rt = items[0].GetComponent<RectTransform>();

    float w = rt.rect.width;
    if (w <= 0.01f) w = rt.sizeDelta.x;

    step = w + (hlg != null ? hlg.spacing : 0f);
    if (step <= 0.01f) step = 500f;
}


    int ClosestToCenterVirtualIndex()
    {
        Vector3 viewCenterWorld = viewport.TransformPoint(viewport.rect.center);

        float best = float.MaxValue;
        int bestIndex = 0;

        for (int i = 0; i < items.Count; i++)
        {
            RectTransform rt = items[i].GetComponent<RectTransform>();
            Vector3 itemCenterWorld = rt.TransformPoint(rt.rect.center);
            float d = Mathf.Abs(itemCenterWorld.x - viewCenterWorld.x);
            if (d < best)
            {
                best = d;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    void UpdateVisuals(bool instant)
    {
        // Map selected virtual -> real pack index
        int realSelected = Mod(selectedVirtual - buffer, n);

        // Background color
        if (background != null)
        {
            Color target = packs[realSelected].backgroundColor;
            if (instant) background.color = target;
            else background.color = Color.Lerp(background.color, target, Time.deltaTime * colorLerpSpeed);
        }

        // Scale by distance to center
        Vector3 viewCenterWorld = viewport.TransformPoint(viewport.rect.center);

        for (int i = 0; i < items.Count; i++)
        {
            RectTransform rt = items[i].GetComponent<RectTransform>();
            Vector3 itemCenterWorld = rt.TransformPoint(rt.rect.center);
            float d = Mathf.Abs(itemCenterWorld.x - viewCenterWorld.x);

            // 0 distance -> selectedScale, far -> unselectedScale
            float t = Mathf.InverseLerp(0f, viewport.rect.width * 0.5f, d);
            float targetScale = Mathf.Lerp(selectedScale, unselectedScale, t);

            Vector3 target = Vector3.one * targetScale;
            if (instant) rt.localScale = target;
            else rt.localScale = Vector3.Lerp(rt.localScale, target, Time.deltaTime * scaleLerpSpeed);
        }
    }

    void SnapTo(int virtualIndex)
    {
        Vector2 target = TargetAnchoredPosForVirtualIndex(virtualIndex);
        content.anchoredPosition = Vector2.Lerp(content.anchoredPosition, target, Time.deltaTime * snapSpeed);
    }

    Vector2 TargetAnchoredPosForVirtualIndex(int virtualIndex)
    {
        // Move content so that item[virtualIndex] center aligns with viewport center
        RectTransform item = items[virtualIndex].GetComponent<RectTransform>();

        Vector3 viewCenterWorld = viewport.TransformPoint(viewport.rect.center);
        Vector3 itemCenterWorld = item.TransformPoint(item.rect.center);

        // Convert world delta into content local anchoredPosition shift
        Vector3 deltaWorld = itemCenterWorld - viewCenterWorld;
        Vector3 deltaLocal = content.InverseTransformVector(deltaWorld);

        return content.anchoredPosition - new Vector2(deltaLocal.x, 0f);
    }

    void CenterOnVirtualIndex(int virtualIndex)
    {
        content.anchoredPosition = TargetAnchoredPosForVirtualIndex(virtualIndex);
    }

    void SeamlessLoop()
    {
        // If we scroll too far into the left buffer, jump right by n steps.
        // If too far into the right buffer, jump left by n steps.
        // This keeps the visual position consistent but wraps indices.

        // How far have we moved in "item steps" from the first real item?
        // We use the selectedVirtual to decide if we are in a buffer zone.
        int leftEdge = buffer - 1;
        int rightEdge = buffer + n;

        if (selectedVirtual <= leftEdge)
        {
            // Jump right by n items
            content.anchoredPosition += new Vector2(n * step, 0f);
            selectedVirtual += n;
        }
        else if (selectedVirtual >= rightEdge)
        {
            // Jump left by n items
            content.anchoredPosition -= new Vector2(n * step, 0f);
            selectedVirtual -= n;
        }
    }

    int Mod(int a, int m)
    {
        int r = a % m;
        return r < 0 ? r + m : r;
    }
}
