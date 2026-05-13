using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// First real screen of the game. Presents the Pack options as a horizontal swipe carousel.
///
/// Cards are pre-placed in the scene as children of <see cref="carouselRoot"/> and assigned
/// to <see cref="cards"/> in the Inspector — one per pack, in carousel order. Each card's
/// <see cref="PackCardController.packType"/> and <see cref="PackCardController.backgroundColor"/>
/// fields drive the game logic and screen background tint. All text, icons, and visual styling
/// are authored directly on the scene objects so each can carry its own localization string events.
///
/// The visual position of the carousel is driven by a single float <see cref="displayIndex"/>:
/// drag sets it directly, release seeds a damped spring that pulls it toward
/// <see cref="currentIndex"/>. This unifies the drag and commit phases.
/// </summary>
public class PackSelectionController : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private RectTransform carouselRoot;

    [Header("Pack Cards (pre-placed in scene, in carousel order)")]
    [Tooltip("Assign each pre-placed PackCard here in the order they should appear in the carousel.")]
    [SerializeField] private List<PackCardController> cards = new List<PackCardController>();

    [Header("Slot Layout (anchored)")]
    [SerializeField] private Vector2 currentSlotOffset = Vector2.zero;
    [SerializeField] private float currentSlotScale = 1f;
    [SerializeField] private Vector2 nextSlotOffset = new Vector2(180f, -60f);
    [SerializeField] private float nextSlotScale = 0.78f;
    [SerializeField] private Vector2 prevSlotOffset = new Vector2(-180f, -60f);
    [SerializeField] private float prevSlotScale = 0.78f;
    [SerializeField] private Vector2 offscreenRightOffset = new Vector2(1400f, -120f);
    [SerializeField] private Vector2 offscreenLeftOffset = new Vector2(-1400f, -120f);
    [SerializeField] private float offscreenScale = 0.5f;

    [Header("Swipe Tuning")]
    [Tooltip("Drag distance (pixels) past which release commits to the next/previous index.")]
    [SerializeField] private float swipeDistanceThreshold = 120f;
    [Tooltip("How far the cards can be pushed past the first/last index. 0 = hard stop, 0.3 = some give.")]
    [SerializeField, Range(0f, 0.5f)] private float boundaryRubberband = 0.3f;
    [Tooltip("Release-velocity (pixels/sec) above which a flick commits even if drag distance is below threshold.")]
    [SerializeField] private float flickVelocityThreshold = 600f;

    [Header("Spring (commit & settle)")]
    [Tooltip("Spring stiffness — higher = faster pull toward target. 80–200 typical.")]
    [SerializeField] private float springStiffness = 120f;
    [Tooltip("Damping ratio. 1 = critically damped (no bounce), <1 = bouncy, >1 = sluggish. 0.5–0.8 is the sweet spot for fluid bounce.")]
    [SerializeField, Range(0.1f, 1.5f)] private float springDampingRatio = 0.6f;
    [Tooltip("Cap on velocity (in index-units/sec) seeded from a flick. Prevents extreme overshoot on aggressive swipes.")]
    [SerializeField] private float maxSeedVelocity = 12f;

    [Header("Background Color Transition")]
    [Tooltip("Exponent applied to the color-lerp t. 1 = linear (snappy), higher = more gradual / lags behind the cards.")]
    [SerializeField, Range(1f, 4f)] private float colorTransitionPower = 2f;

    private struct SlotPose
    {
        public Vector2 pos;
        public float scale;
        public float alpha;
    }

    private GameManager gameManager;
    private TopicManager TopicManager => gameManager.topicManager;

    private int currentIndex;
    private float displayIndex;
    private float displayVelocity;

    private bool isDragging;
    private float dragDeltaX;
    private float dragVelocityX;

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        InitCarousel();
        currentIndex = 0;
        displayIndex = 0f;
        displayVelocity = 0f;
        UpdateLayout();
    }

    void OnDisable()
    {
        isDragging = false;
        dragDeltaX = 0f;
        dragVelocityX = 0f;
        displayVelocity = 0f;
    }

    // ─── Init ─────────────────────────────────────────────────────────────────

    void InitCarousel()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null) continue;

            // Ensure centered anchors so anchoredPosition is a clean offset-from-center.
            card.Rect.anchorMin = new Vector2(0.5f, 0.5f);
            card.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            card.Rect.pivot    = new Vector2(0.5f, 0.5f);

            int captured = i;
            card.BindSelectListener(() => OnSelect(captured));
            card.SetLocked(!gameManager.OwnedPacks.Contains(card.packType));
        }
    }

    // ─── Layout ───────────────────────────────────────────────────────────────

    void UpdateLayout()
    {
        if (cards.Count == 0) return;
        for (int i = 0; i < cards.Count; i++)
            ApplyPose(cards[i], ComputeSlot(i - displayIndex));

        UpdateSiblingOrderAnchoredAt(currentIndex);
        RefreshSelectInteractability();
        UpdateBackgroundColor();
    }

    void UpdateBackgroundColor()
    {
        if (backgroundImage == null || cards.Count == 0) return;
        float diff = displayIndex - currentIndex;
        int neighbor = currentIndex;
        if (diff > 0f)      neighbor = Mathf.Min(currentIndex + 1, cards.Count - 1);
        else if (diff < 0f) neighbor = Mathf.Max(currentIndex - 1, 0);

        float linearT = Mathf.Clamp01(Mathf.Abs(diff));
        float colorT  = Mathf.Pow(linearT, colorTransitionPower);
        backgroundImage.color = Color.Lerp(cards[currentIndex].backgroundColor,
                                           cards[neighbor].backgroundColor,
                                           colorT);
    }

    void RefreshSelectInteractability()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            bool owned = gameManager.OwnedPacks.Contains(cards[i].packType);
            cards[i].SetSelectInteractable(owned && i == currentIndex);
        }
    }

    void ApplyPose(PackCardController card, SlotPose pose)
    {
        card.Rect.anchoredPosition = pose.pos;
        card.Rect.localScale       = new Vector3(pose.scale, pose.scale, 1f);
        card.CanvasGroup.alpha     = pose.alpha;
        card.CanvasGroup.blocksRaycasts = pose.alpha > 0.2f;
    }

    SlotPose ComputeSlot(float offset)
    {
        float abs = Mathf.Abs(offset);
        SlotPose s;
        if (offset >= 0f)
        {
            if (abs <= 1f)
            {
                float t = abs;
                s.pos   = Vector2.Lerp(currentSlotOffset, nextSlotOffset, t);
                s.scale = Mathf.Lerp(currentSlotScale, nextSlotScale, t);
                s.alpha = 1f;
            }
            else
            {
                float t = Mathf.Clamp01(abs - 1f);
                s.pos   = Vector2.Lerp(nextSlotOffset, offscreenRightOffset, t);
                s.scale = Mathf.Lerp(nextSlotScale, offscreenScale, t);
                s.alpha = Mathf.Lerp(1f, 0f, t);
            }
        }
        else
        {
            if (abs <= 1f)
            {
                float t = abs;
                s.pos   = Vector2.Lerp(currentSlotOffset, prevSlotOffset, t);
                s.scale = Mathf.Lerp(currentSlotScale, prevSlotScale, t);
                s.alpha = 1f;
            }
            else
            {
                float t = Mathf.Clamp01(abs - 1f);
                s.pos   = Vector2.Lerp(prevSlotOffset, offscreenLeftOffset, t);
                s.scale = Mathf.Lerp(prevSlotScale, offscreenScale, t);
                s.alpha = Mathf.Lerp(1f, 0f, t);
            }
        }
        return s;
    }

    void UpdateSiblingOrderAnchoredAt(int anchorIndex)
    {
        var ordered = new List<(PackCardController c, int dist)>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
            ordered.Add((cards[i], Mathf.Abs(i - anchorIndex)));
        ordered.Sort((a, b) => b.dist.CompareTo(a.dist));
        for (int i = 0; i < ordered.Count; i++)
            ordered[i].c.transform.SetAsLastSibling();
    }

    // ─── Spring (Update tick) ────────────────────────────────────────────────

    void Update()
    {
        if (isDragging) return;
        if (cards.Count == 0) return;

        float displacement = displayIndex - currentIndex;
        if (Mathf.Abs(displacement) < 0.0005f && Mathf.Abs(displayVelocity) < 0.01f)
        {
            if (displayIndex != currentIndex || displayVelocity != 0f)
            {
                displayIndex  = currentIndex;
                displayVelocity = 0f;
                UpdateLayout();
            }
            return;
        }

        float dt     = Mathf.Min(Time.deltaTime, 1f / 30f);
        float omega  = Mathf.Sqrt(Mathf.Max(0.001f, springStiffness));
        float damping = 2f * Mathf.Max(0f, springDampingRatio) * omega;
        float accel  = -springStiffness * displacement - damping * displayVelocity;
        displayVelocity += accel * dt;
        displayIndex    += displayVelocity * dt;

        UpdateLayout();
    }

    // ─── Drag input ──────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging    = true;
        dragDeltaX    = 0f;
        dragVelocityX = 0f;
        displayVelocity = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        dragDeltaX += eventData.delta.x;

        float dt = Mathf.Max(0.001f, Time.unscaledDeltaTime);
        float instantVel = eventData.delta.x / dt;
        dragVelocityX = Mathf.Lerp(dragVelocityX, instantVel, 0.4f);

        float progress = ComputeProgress(dragDeltaX);
        displayIndex = currentIndex - progress;
        UpdateLayout();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        isDragging = false;

        int targetIndex = currentIndex;
        bool flickedNext = dragVelocityX <= -flickVelocityThreshold;
        bool flickedPrev = dragVelocityX >=  flickVelocityThreshold;

        if ((dragDeltaX <= -swipeDistanceThreshold || flickedNext) && currentIndex < cards.Count - 1)
            targetIndex = currentIndex + 1;
        else if ((dragDeltaX >= swipeDistanceThreshold || flickedPrev) && currentIndex > 0)
            targetIndex = currentIndex - 1;

        currentIndex = targetIndex;

        float seeded = -dragVelocityX / Mathf.Max(1f, swipeDistanceThreshold);
        displayVelocity = Mathf.Clamp(seeded, -maxSeedVelocity, maxSeedVelocity);

        dragDeltaX    = 0f;
        dragVelocityX = 0f;
    }

    float ComputeProgress(float deltaX)
    {
        float raw = deltaX / Mathf.Max(1f, swipeDistanceThreshold);
        if (currentIndex == 0 && raw > 0f)
            return Mathf.Min(raw * boundaryRubberband, boundaryRubberband);
        if (currentIndex == cards.Count - 1 && raw < 0f)
            return Mathf.Max(raw * boundaryRubberband, -boundaryRubberband);
        return Mathf.Clamp(raw, -1f, 1f);
    }

    // ─── Selection ───────────────────────────────────────────────────────────

    void OnSelect(int packIndex)
    {
        if (packIndex != currentIndex) return;
        if (isDragging) return;

        var pack = cards[packIndex].packType;
        gameManager.SetPack(pack);
        TopicManager.LoadTopicsFromPack();
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }
}
