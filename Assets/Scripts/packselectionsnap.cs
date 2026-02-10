using UnityEngine;
using UnityEngine.UI;

public class SimplePackSnap : MonoBehaviour
{
    public ScrollRect scrollRect;
    public RectTransform viewport;
    public RectTransform content;
    public Image background;

    public RectTransform[] packs;   // Pack1..Pack5 in order
    public Color[] colors;          // same length as packs

    public float snapSpeed = 12f;
    public float colorLerpSpeed = 6f;

    bool dragging;

    void Update()
    {
        if (packs == null || packs.Length == 0) return;

        int closest = ClosestToCenter();
        // background changes to selected pack color
        if (colors != null && colors.Length == packs.Length && background != null)
            background.color = Color.Lerp(background.color, colors[closest], Time.deltaTime * colorLerpSpeed);

        // snap only when NOT dragging
        if (!dragging)
            SnapTo(closest);
    }

    int ClosestToCenter()
    {
        Vector3 center = viewport.TransformPoint(viewport.rect.center);

        float best = float.MaxValue;
        int bestIndex = 0;

        for (int i = 0; i < packs.Length; i++)
        {
            Vector3 packCenter = packs[i].TransformPoint(packs[i].rect.center);
            float d = Mathf.Abs(packCenter.x - center.x);
            if (d < best) { best = d; bestIndex = i; }
        }

        return bestIndex;
    }

    void SnapTo(int index)
    {
        float target = NormalizedFor(index);
        scrollRect.horizontalNormalizedPosition =
            Mathf.Lerp(scrollRect.horizontalNormalizedPosition, target, Time.deltaTime * snapSpeed);
    }

    float NormalizedFor(int index)
    {
        float contentW = content.rect.width;
        float viewW = viewport.rect.width;
        if (contentW <= viewW) return 0f;

        float packCenterX = Mathf.Abs(content.InverseTransformPoint(
            packs[index].TransformPoint(packs[index].rect.center)
        ).x);

        float left = packCenterX - viewW * 0.5f;
        float maxLeft = contentW - viewW;

        return Mathf.Clamp01(left / maxLeft);
    }

    // Hook these to ScrollRect drag events (or EventTrigger)
    public void BeginDrag() => dragging = true;
    public void EndDrag() => dragging = false;
}
