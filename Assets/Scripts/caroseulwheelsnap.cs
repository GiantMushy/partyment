using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PackCarouselSnap : MonoBehaviour
{
    [Serializable]
    public class PackData
    {
        public string packName;
        public Color backgroundColor;
    }

    [Header("Data")]
    public List<PackData> packs = new List<PackData>(5);

    [Header("References")]
    public ScrollRect scrollRect;
    public RectTransform content;
    public RectTransform viewport;
    public PackItemView packItemPrefab;

    [Header("Background (optional crossfade)")]
    public Image backgroundA;
    public Image backgroundB;
    public float backgroundFadeTime = 0.25f;

    [Header("Snap")]
    public float snapDuration = 0.18f;         // how fast it snaps
    public float snapVelocityThreshold = 200f; // wait until swipe slows
    public float endDragDelay = 0.05f;         // small delay after finger up

    [Header("Wheel / scale feel")]
    public float centerScale = 1.15f;
    public float sideScale = 0.9f;
    public float scaleLerp = 10f;

    private readonly List<RectTransform> itemRects = new();
    private bool isDragging = false;
    private bool isSnapping = false;
    private int currentIndex = 0;

    private bool bgUsingA = true;
    private Coroutine bgRoutine;

    void Start()
    {
        BuildItems();
        Canvas.ForceUpdateCanvases();

        // start centered on first
        JumpToIndex(0);
        ApplyBackground(0, immediate: true);
    }

    void Update()
    {
        if (itemRects.Count == 0) return;

        UpdateScaleEffect();

        // If not dragging and not snapping, and velocity is low → snap
        if (!isDragging && !isSnapping)
        {
            if (Mathf.Abs(scrollRect.velocity.x) < snapVelocityThreshold)
            {
                int nearest = FindNearestIndex();
                if (nearest != currentIndex)
                {
                    currentIndex = nearest;
                    ApplyBackground(currentIndex, immediate: false);
                }

                StartCoroutine(SnapToIndex(nearest));
            }
        }
    }

    // Hook these from EventTrigger BeginDrag/EndDrag
    public void OnBeginDrag()
    {
        isDragging = true;
        isSnapping = false;
        StopAllCoroutines();          // stop any snap in progress
    }

    public void OnEndDrag()
    {
        isDragging = false;
        StartCoroutine(SnapAfterDelay());
    }

    IEnumerator SnapAfterDelay()
    {
        yield return new WaitForSeconds(endDragDelay);
        // snapping will happen in Update() once velocity drops
    }

    void BuildItems()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        itemRects.Clear();

        for (int i = 0; i < packs.Count; i++)
        {
            var item = Instantiate(packItemPrefab, content);
            item.Set(packs[i].packName);

            int indexCopy = i;
            if (item.button != null)
            {
                item.button.onClick.AddListener(() =>
                {
                    if (!isSnapping)
                    {
                        currentIndex = indexCopy;
                        ApplyBackground(currentIndex, immediate: false);
                        StartCoroutine(SnapToIndex(indexCopy));
                    }
                });
            }

            itemRects.Add(item.GetComponent<RectTransform>());
        }
    }

    int FindNearestIndex()
    {
        Vector3 viewportCenterWorld = viewport.TransformPoint(viewport.rect.center);

        float best = float.MaxValue;
        int bestIndex = 0;

        for (int i = 0; i < itemRects.Count; i++)
        {
            Vector3 itemCenterWorld = itemRects[i].TransformPoint(itemRects[i].rect.center);
            float dist = Mathf.Abs(itemCenterWorld.x - viewportCenterWorld.x);
            if (dist < best)
            {
                best = dist;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    IEnumerator SnapToIndex(int index)
    {
        isSnapping = true;

        // stop inertia so snap is clean
        scrollRect.velocity = Vector2.zero;

        float targetX = GetContentXToCenterItem(index);

        Vector2 start = content.anchoredPosition;
        Vector2 end = new Vector2(targetX, start.y);

        float t = 0f;
        float dur = Mathf.Max(0.01f, snapDuration);

        while (t < 1f)
        {
            // If user starts dragging again, abort snap
            if (isDragging)
            {
                isSnapping = false;
                yield break;
            }

            t += Time.deltaTime / dur;
            float s = Mathf.SmoothStep(0f, 1f, t);
            content.anchoredPosition = Vector2.Lerp(start, end, s);
            yield return null;
        }

        content.anchoredPosition = end;
        isSnapping = false;
    }

    void JumpToIndex(int index)
    {
        content.anchoredPosition = new Vector2(GetContentXToCenterItem(index), content.anchoredPosition.y);
        scrollRect.velocity = Vector2.zero;
        currentIndex = index;
    }

    float GetContentXToCenterItem(int index)
    {
        // Calculate content movement so item center aligns with viewport center
        Vector3 viewportCenterLocal = content.InverseTransformPoint(viewport.TransformPoint(viewport.rect.center));
        float itemCenterX = itemRects[index].anchoredPosition.x;
        float delta = viewportCenterLocal.x - itemCenterX;
        return content.anchoredPosition.x + delta;
    }

    void UpdateScaleEffect()
    {
        Vector3 viewportCenterWorld = viewport.TransformPoint(viewport.rect.center);

        for (int i = 0; i < itemRects.Count; i++)
        {
            var rt = itemRects[i];
            Vector3 itemCenterWorld = rt.TransformPoint(rt.rect.center);

            float dist = Mathf.Abs(itemCenterWorld.x - viewportCenterWorld.x);
            float t = Mathf.Clamp01(dist / (viewport.rect.width * 0.5f));

            float targetScale = Mathf.Lerp(centerScale, sideScale, t);
            rt.localScale = Vector3.Lerp(rt.localScale, Vector3.one * targetScale, Time.deltaTime * scaleLerp);
        }
    }

    void ApplyBackground(int index, bool immediate)
    {
        if (backgroundA == null || backgroundB == null) return;

        Color next = packs[index].backgroundColor;

        if (bgRoutine != null) StopCoroutine(bgRoutine);

        if (immediate)
        {
            backgroundA.color = next;
            backgroundB.color = new Color(next.r, next.g, next.b, 0f);
            bgUsingA = true;
        }
        else
        {
            bgRoutine = StartCoroutine(CrossFadeBackground(next));
        }
    }

    IEnumerator CrossFadeBackground(Color next)
    {
        Image from = bgUsingA ? backgroundA : backgroundB;
        Image to = bgUsingA ? backgroundB : backgroundA;

        to.color = new Color(next.r, next.g, next.b, 0f);

        float t = 0f;
        float dur = Mathf.Max(0.01f, backgroundFadeTime);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float a = Mathf.Clamp01(t);

            from.color = new Color(from.color.r, from.color.g, from.color.b, 1f - a);
            to.color = new Color(next.r, next.g, next.b, a);

            yield return null;
        }

        bgUsingA = !bgUsingA;
        bgRoutine = null;
    }
}
