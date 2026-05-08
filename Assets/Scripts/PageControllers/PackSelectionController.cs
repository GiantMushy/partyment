using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// First real screen of the game. Presents the Pack options as a horizontal swipe carousel:
/// one centered card with the next pack peeking behind it. Swiping commits to the new index
/// and tints the screen background to that pack's color. Tapping the centered card's Select
/// button records the pack on <see cref="GameManager"/>, asks
/// <see cref="TopicManager.LoadTopicsFromPack"/> to filter the topic list, and advances to
/// <see cref="GameManager.GameState.LocalVsOnline"/>.
///
/// Cards are spawned from <see cref="cardPrefab"/> (one per <see cref="packs"/> entry) into
/// <see cref="carouselRoot"/>. Packs not in <see cref="GameManager.OwnedPacks"/> still appear
/// in the carousel but their Select button stays non-interactable.
///
/// The visual position of the carousel is driven by a single float
/// <see cref="displayIndex"/>: drag sets it directly, release seeds a damped spring that
/// pulls it toward <see cref="currentIndex"/>. This unifies the drag and commit phases —
/// fast flicks carry through into a velocity-seeded settle, and the spring's damping ratio
/// gives a tunable bounce on land.
/// </summary>
public class PackSelectionController : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [System.Serializable]
    public class PackEntry
    {
        public GameManager.Pack pack;
        public string title;
        [TextArea(2, 4)] public string description;
        public Color backgroundColor = Color.white;
    }

    [Header("References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private RectTransform carouselRoot;
    [SerializeField] private PackCardController cardPrefab;

    [Header("Pack Order & Colors")]
    [Tooltip("Carousel order. Each entry's backgroundColor tints the screen when its card is centered.")]
    [SerializeField] private List<PackEntry> packs = new List<PackEntry>
    {
        new PackEntry { pack = GameManager.Pack.Default,      title = "Classic",
            description = "The original mix. Broad debates and corruptions for any party — start here if you're not sure.",
            backgroundColor = new Color(0.95f, 0.95f, 0.95f) },
        new PackEntry { pack = GameManager.Pack.Icelandic,    title = "Icelandic",
            description = "Local humour and topics straight from heima. Made for Icelanders, by Icelanders.",
            backgroundColor = new Color32(0x1B, 0x3A, 0x6B, 0xFF) },
        new PackEntry { pack = GameManager.Pack.PopCulture,   title = "Pop Culture",
            description = "Movies, music, memes, and the celebrities everyone pretends not to follow.",
            backgroundColor = new Color32(0xC2, 0x18, 0x5B, 0xFF) },
        new PackEntry { pack = GameManager.Pack.Political,    title = "Political",
            description = "Real-world political debates and ideologies. Pick your friends carefully.",
            backgroundColor = new Color32(0x7A, 0x60, 0x33, 0xFF) },
        new PackEntry { pack = GameManager.Pack.EighteenPlus, title = "18+",
            description = "Adults only. Taboo, risqué, and unapologetically naughty — keep the kids out.",
            backgroundColor = new Color32(0x8B, 0x1A, 0x1A, 0xFF) },
    };

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

    [Header("Card Body Tint")]
    [Tooltip("Multiplier applied to each pack's backgroundColor to fill the card body. 1 = same as background, 0.7 = 30% darker.")]
    [SerializeField, Range(0f, 1f)] private float cardBodyDarken = 0.7f;

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
    private readonly List<PackCardController> cards = new List<PackCardController>();

    // currentIndex is the committed pack. displayIndex is what's actually on screen — a float
    // that the spring chases toward currentIndex. They diverge during drag and the post-drag settle.
    private int currentIndex;
    private float displayIndex;
    private float displayVelocity;     // index-units per second

    private bool isDragging;
    private float dragDeltaX;          // pixels accumulated this drag (for distance threshold)
    private float dragVelocityX;       // smoothed pixels/sec, sampled during OnDrag

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        BuildCarousel();
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

    // ─── Build / layout ───────────────────────────────────────────────────────

    void BuildCarousel()
    {
        for (int i = 0; i < cards.Count; i++)
            if (cards[i] != null) Destroy(cards[i].gameObject);
        cards.Clear();

        if (cardPrefab == null || carouselRoot == null) return;

        for (int i = 0; i < packs.Count; i++)
        {
            var entry = packs[i];
            var card = Instantiate(cardPrefab, carouselRoot);
            // Force centered anchors so anchoredPosition is a clean offset-from-center.
            card.Rect.anchorMin = new Vector2(0.5f, 0.5f);
            card.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            card.Rect.pivot = new Vector2(0.5f, 0.5f);

            int captured = i;
            card.Bind(entry.pack, entry.title, entry.description, () => OnSelect(captured));
            // Body fill = pack's screen tint, slightly darker, so the card reads as "of this pack"
            // without losing contrast against the matching backdrop.
            Color bodyColor = entry.backgroundColor * cardBodyDarken;
            bodyColor.a = 1f;
            card.SetBodyColor(bodyColor);
            // Locked overlay shows when the pack isn't owned. RefreshSelectInteractability
            // additionally keeps the Select button non-interactable for these.
            card.SetLocked(!gameManager.OwnedPacks.Contains(entry.pack));
            cards.Add(card);
        }
    }

    /// <summary>Push the visual state of every card from displayIndex.</summary>
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
        if (backgroundImage == null || packs.Count == 0) return;
        // Color picks the neighbor on the side displayIndex has drifted toward, then mixes
        // with the gradual power curve. During post-commit overshoot, |diff| is small so the
        // bleed into the further neighbor is negligible — invisible at colorTransitionPower≥2.
        float diff = displayIndex - currentIndex;
        int neighbor = currentIndex;
        if (diff > 0f)      neighbor = Mathf.Min(currentIndex + 1, packs.Count - 1);
        else if (diff < 0f) neighbor = Mathf.Max(currentIndex - 1, 0);

        float linearT = Mathf.Clamp01(Mathf.Abs(diff));
        float colorT = Mathf.Pow(linearT, colorTransitionPower);
        backgroundImage.color = Color.Lerp(packs[currentIndex].backgroundColor,
                                           packs[neighbor].backgroundColor,
                                           colorT);
    }

    /// <summary>Only the centered card is selectable, and only if its pack is owned.</summary>
    void RefreshSelectInteractability()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            bool owned = gameManager.OwnedPacks.Contains(packs[i].pack);
            cards[i].SetSelectInteractable(owned && i == currentIndex);
        }
    }

    void ApplyPose(PackCardController card, SlotPose pose)
    {
        card.Rect.anchoredPosition = pose.pos;
        card.Rect.localScale = new Vector3(pose.scale, pose.scale, 1f);
        card.CanvasGroup.alpha = pose.alpha;
        card.CanvasGroup.blocksRaycasts = pose.alpha > 0.2f;
    }

    /// <summary>
    /// Resolve a real-valued offset (0 = current, ±1 = peek, ≥|2| = offscreen) to a pose.
    /// In-between values lerp between adjacent slots — this is what powers the live swipe.
    /// </summary>
    SlotPose ComputeSlot(float offset)
    {
        float abs = Mathf.Abs(offset);
        SlotPose s;
        if (offset >= 0f)
        {
            if (abs <= 1f)
            {
                float t = abs;
                s.pos = Vector2.Lerp(currentSlotOffset, nextSlotOffset, t);
                s.scale = Mathf.Lerp(currentSlotScale, nextSlotScale, t);
                s.alpha = 1f;
            }
            else
            {
                float t = Mathf.Clamp01(abs - 1f);
                s.pos = Vector2.Lerp(nextSlotOffset, offscreenRightOffset, t);
                s.scale = Mathf.Lerp(nextSlotScale, offscreenScale, t);
                s.alpha = Mathf.Lerp(1f, 0f, t);
            }
        }
        else
        {
            if (abs <= 1f)
            {
                float t = abs;
                s.pos = Vector2.Lerp(currentSlotOffset, prevSlotOffset, t);
                s.scale = Mathf.Lerp(currentSlotScale, prevSlotScale, t);
                s.alpha = 1f;
            }
            else
            {
                float t = Mathf.Clamp01(abs - 1f);
                s.pos = Vector2.Lerp(prevSlotOffset, offscreenLeftOffset, t);
                s.scale = Mathf.Lerp(prevSlotScale, offscreenScale, t);
                s.alpha = Mathf.Lerp(1f, 0f, t);
            }
        }
        return s;
    }

    /// <summary>
    /// Card with the smallest distance to <paramref name="anchorIndex"/> renders on top.
    /// Done by sorting descending then SetAsLastSibling() so the closest ends up last.
    /// </summary>
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
            // Settled — snap to clean values once and idle.
            if (displayIndex != currentIndex || displayVelocity != 0f)
            {
                displayIndex = currentIndex;
                displayVelocity = 0f;
                UpdateLayout();
            }
            return;
        }

        // Semi-implicit Euler on a damped spring. Critical damping = 2·sqrt(k); we scale
        // by the user-tunable ratio so <1 bounces and >1 over-damps.
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f); // protect against frame hitches
        float omega = Mathf.Sqrt(Mathf.Max(0.001f, springStiffness));
        float damping = 2f * Mathf.Max(0f, springDampingRatio) * omega;
        float accel = -springStiffness * displacement - damping * displayVelocity;
        displayVelocity += accel * dt;
        displayIndex += displayVelocity * dt;

        UpdateLayout();
    }

    // ─── Drag input ──────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        dragDeltaX = 0f;
        dragVelocityX = 0f;
        // The user has grabbed the carousel — halt any in-flight spring motion.
        // displayIndex is left where it is so the drag continues from the visible spot.
        displayVelocity = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        dragDeltaX += eventData.delta.x;

        // Smoothed velocity (pixels/sec). Exponential moving average — enough memory to
        // ride out a single noisy frame without lagging the actual gesture.
        float dt = Mathf.Max(0.001f, Time.unscaledDeltaTime);
        float instantVel = eventData.delta.x / dt;
        dragVelocityX = Mathf.Lerp(dragVelocityX, instantVel, 0.4f);

        float progress = ComputeProgress(dragDeltaX);
        // displayIndex sits 'progress' units away from currentIndex on the side being revealed.
        // progress < 0 (drag left) → reveal next → displayIndex > currentIndex.
        displayIndex = currentIndex - progress;
        UpdateLayout();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        isDragging = false;

        // Decide commit. Distance threshold OR a fast flick in the matching direction commits.
        int targetIndex = currentIndex;
        bool flickedNext = dragVelocityX <= -flickVelocityThreshold;
        bool flickedPrev = dragVelocityX >= flickVelocityThreshold;

        if ((dragDeltaX <= -swipeDistanceThreshold || flickedNext) && currentIndex < packs.Count - 1)
            targetIndex = currentIndex + 1;
        else if ((dragDeltaX >= swipeDistanceThreshold || flickedPrev) && currentIndex > 0)
            targetIndex = currentIndex - 1;

        currentIndex = targetIndex;

        // Hand off the user's flick to the spring as initial velocity. Convert pixels/sec
        // → index/sec by dividing by the swipe threshold; negate because positive index
        // direction (advancing) corresponds to leftward (negative) pixel velocity.
        float seeded = -dragVelocityX / Mathf.Max(1f, swipeDistanceThreshold);
        displayVelocity = Mathf.Clamp(seeded, -maxSeedVelocity, maxSeedVelocity);

        dragDeltaX = 0f;
        dragVelocityX = 0f;
        // Update() will pick up from here and settle the spring.
    }

    /// <summary>Map raw pixel drag delta to a [-1..1] progress with rubber-band at boundaries.</summary>
    float ComputeProgress(float deltaX)
    {
        float raw = deltaX / Mathf.Max(1f, swipeDistanceThreshold);
        if (currentIndex == 0 && raw > 0f)
            return Mathf.Min(raw * boundaryRubberband, boundaryRubberband);
        if (currentIndex == packs.Count - 1 && raw < 0f)
            return Mathf.Max(raw * boundaryRubberband, -boundaryRubberband);
        return Mathf.Clamp(raw, -1f, 1f);
    }

    // ─── Selection ───────────────────────────────────────────────────────────

    void OnSelect(int packIndex)
    {
        // Defensive: only the centered card is meant to be tappable, and never mid-drag.
        if (packIndex != currentIndex) return;
        if (isDragging) return;

        var pack = packs[packIndex].pack;
        gameManager.SetPack(pack);
        TopicManager.LoadTopicsFromPack();
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }
}
