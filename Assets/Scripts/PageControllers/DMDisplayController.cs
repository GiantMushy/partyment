using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// Discussion-Moderator screen shown after every non-DM player has seen their corruption.
/// Owns three responsibilities:
///
///   1. <b>Group turn order &amp; card display</b> — one card per group is displayed at a time,
///      sliding in from the right (with bounce) when Stop is pressed or the timer expires.
///      The last group silently enables the Next button instead of animating.
///   2. <b>Timer</b> — three always-visible buttons (Play / Pause / Stop) act as mutually-
///      exclusive toggles via Unity's Selected animator state. Stop is toggled by default
///      (between-rounds state). During the slide animation the timer display rewinds
///      back to MaxTime simultaneously.
///   3. <b>Corruption card layout</b> — same three-panel slide system as before:
///      Active, Inactive, and Accusation panels on a horizontal <see cref="slideTrack"/>.
///
/// Inspector wiring required:
///   Topic Display   → topicTypeText, topicDescriptionText, topicTypeIcon, versusSprite, scenarioSprite
///   Group Card      → groupCardArea (RectTransform parent), groupCardPrefab, slideInCurve
///   Timer           → timerText, playToggle, pauseToggle, stopToggle (ToggleButton), nextButton
///   Objectives      → objectivesContainer, slideTrack, activeObjectiveContainer,
///                     inactiveObjectiveContainer, corruptionPrefab
/// </summary>
public class DMDisplayController : MonoBehaviour
{
    // ===================================================================
    //  Inspector References
    // ===================================================================

    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;
    private CorruptionManager CorruptionManager => gameManager.corruptionManager;
    private TopicManager TopicManager => gameManager.topicManager;

    [Header("Topic Display")]
    [SerializeField] private TextMeshProUGUI topicTypeText;
    [SerializeField] private TextMeshProUGUI topicDescriptionText;
    [SerializeField] private Image topicTypeIcon;
    [SerializeField] private Sprite versusSprite;
    [SerializeField] private Sprite scenarioSprite;

    [Header("Group Card")]
    [Tooltip("Parent RectTransform where group cards are instantiated.")]
    [SerializeField] private RectTransform groupCardArea;
    [SerializeField] private GameObject groupCardPrefab;
    [Tooltip("Duration of the card slide-in animation in seconds.")]
    [SerializeField] private float slideDuration = 0.45f;
    [Tooltip("Curve for the incoming card (x = time 0-1, y = position fraction 0-1). " +
             "Overshoot y past 1.0 to produce a bounce/settle effect. Configured with a default bounce if left empty.")]
    [SerializeField] private AnimationCurve slideInCurve;
    [Tooltip("Pixels the old card exits to the left (and the new card enters from the right). " +
             "Set to roughly the card's canvas-unit width so it slides cleanly off-screen.")]
    [SerializeField] private float groupCardSlideDistance = 1200f;

    [Header("Timer")]
    [SerializeField] private TextMeshProUGUI timerText;
    [Tooltip("Always visible. Toggled on while timer is running.")]
    [SerializeField] private ToggleButton playToggle;
    [Tooltip("Always visible. Toggled on while timer is paused. Has no effect between rounds.")]
    [SerializeField] private ToggleButton pauseToggle;
    [Tooltip("Always visible. Toggled on by default (between rounds). Pressing it while Running or Paused advances to the next group.")]
    [SerializeField] private ToggleButton stopToggle;
    [Tooltip("Hidden until all groups have presented. Wire onClick to ProceedToVoting().")]
    [SerializeField] private GameObject nextButton;
    [SerializeField] private float timerDuration = 60f;

    [Header("Objective Containers")]
    [Tooltip("RectTransform owned by the VerticalLayoutGroup — do NOT slide this directly.")]
    [SerializeField] private RectTransform objectivesContainer;
    [Tooltip("Inner RectTransform that slides horizontally between Accusation / Active / Inactive panels.")]
    [SerializeField] private RectTransform slideTrack;
    [Tooltip("Active panel: Speech(current group) + Interruption(all other groups).")]
    [SerializeField] private Transform activeObjectiveContainer;
    [Tooltip("Inactive panel: Speech(all other groups) + Interruption(current group).")]
    [SerializeField] private Transform inactiveObjectiveContainer;
    [SerializeField] private GameObject corruptionPrefab;
    [SerializeField] private GameObject noCorruptionsPrefab;

    [Header("Objectives Slide Settings")]
    [Tooltip("Pixels the slide track shifts per panel (left = Inactive, right = Accusation).")]
    [SerializeField] private float objectivesSlideOffset = 800f;
    [SerializeField] private float objectivesSlideSpeed = 8f;

    // ===================================================================
    //  Private State
    // ===================================================================

    private enum TimerState { Stopped, Running, Paused, AllGroupsDone }
    private TimerState currentTimerState = TimerState.Stopped;
    private Coroutine timerCoroutine;
    private Coroutine slideCoroutine;
    private float timeRemaining;
    private bool isTransitioning;

    private List<Group> groupTurnOrder = new List<Group>();
    private int currentGroupIndex;
    private GameObject currentGroupCard;

    private Dictionary<int, List<GameObject>> speechCardsByGroupId       = new Dictionary<int, List<GameObject>>();
    private Dictionary<int, List<GameObject>> interruptionCardsByGroupId = new Dictionary<int, List<GameObject>>();
    private List<GameObject> allInstantiatedCards = new List<GameObject>();
    private GameObject noCorruptionsCardInstance;

    private Vector2 defaultAnchoredPos;
    private Vector2 targetAnchoredPos;

    // ===================================================================
    //  Unity Lifecycle
    // ===================================================================

    void Awake()
    {
        if (slideTrack != null)
        {
            defaultAnchoredPos = slideTrack.anchoredPosition;
            targetAnchoredPos  = defaultAnchoredPos;
        }

        if (slideInCurve == null || slideInCurve.length == 0)
            slideInCurve = BuildDefaultBounceCurve();

        if (groupCardArea != null)
        {
            // A LayoutGroup on groupCardArea overrides anchoredPosition every frame, breaking
            // the slide animation. Disable it automatically and warn so it can be removed.
            var lg = groupCardArea.GetComponent<LayoutGroup>();
            if (lg != null)
            {
                lg.enabled = false;
                Debug.LogWarning("DMDisplay: groupCardArea has a LayoutGroup — it has been disabled at runtime. " +
                                 "Remove it from the scene to clean this up.");
            }

            // RectMask2D clips cards to the groupCardArea bounds so the horizontal slide
            // animation is visible even inside the scroll view's masked viewport.
            if (groupCardArea.GetComponent<RectMask2D>() == null)
                groupCardArea.gameObject.AddComponent<RectMask2D>();
        }
    }

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        InitializeDisplay();
    }

    void OnDisable()
    {
        CleanupCorruptionCards();
        StopTimerCoroutine();
        if (slideCoroutine != null) { StopCoroutine(slideCoroutine); slideCoroutine = null; }
        CleanupCurrentGroupCard();
    }

    void Update()
    {
        if (slideTrack == null) return;
        slideTrack.anchoredPosition = Vector2.Lerp(
            slideTrack.anchoredPosition,
            targetAnchoredPos,
            Time.deltaTime * objectivesSlideSpeed
        );
    }

    // ===================================================================
    //  Initialization
    // ===================================================================

    private void InitializeDisplay()
    {
        isTransitioning = false;

        DisplayActiveTopic();
        DetermineGroupTurnOrder();
        currentGroupIndex = 0;

        InstantiateCorruptionCards();

        CleanupCurrentGroupCard();
        foreach (Transform child in groupCardArea)
            Destroy(child.gameObject);

        if (groupTurnOrder.Count > 0)
        {
            currentGroupCard = BuildGroupCard(groupTurnOrder[0]);
            ApplyGroupCardAreaHeight(MeasureCardHeight(currentGroupCard.GetComponent<RectTransform>()));
            DistributeCardsForCurrentGroup();
        }

        if (nextButton != null) nextButton.SetActive(false);
        SnapToActiveObjectives();
        ForceObjectiveLayoutRebuild();

        timeRemaining = timerDuration;
        UpdateTimerText(timerDuration);
        currentTimerState = TimerState.Stopped;
        SelectToggle(stopToggle);
        SetTimerInteractable(true);
    }

    private void DisplayActiveTopic()
    {
        // Disable any LocalizeStringEvent on these objects so our runtime text isn't overwritten.
        DisableLocalizer(topicTypeText);
        DisableLocalizer(topicDescriptionText);

        Topic topic = TopicManager.currentTopic;
        if (topic == null)
        {
            if (topicDescriptionText != null) topicDescriptionText.text = "No topic selected.";
            if (topicTypeText != null)        topicTypeText.text        = "";
            Debug.LogWarning("DMDisplayController: No active topic found.");
            return;
        }

        bool isVersus = topic.type == global::TopicManager.TopicType.Versus;
        if (topicDescriptionText != null) topicDescriptionText.text  = topic.description;
        if (topicTypeText        != null) topicTypeText.text         = isVersus ? "Versus" : "Scenarios";
        if (topicTypeIcon        != null) topicTypeIcon.sprite       = isVersus ? versusSprite : scenarioSprite;
    }

    private static void DisableLocalizer(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        var loc = tmp.GetComponent<LocalizeStringEvent>();
        if (loc != null) loc.enabled = false;
    }

    // ===================================================================
    //  Turn Order
    // ===================================================================

    private void DetermineGroupTurnOrder()
    {
        groupTurnOrder = PlayerManager.groups.Values
            .OrderBy(g => g.id)
            .ToList();
        Debug.Log($"DMDisplay: {groupTurnOrder.Count} group(s) in turn order.");
    }

    // ===================================================================
    //  Group Cards
    // ===================================================================

    /// <summary>
    /// Instantiates a group card for <paramref name="group"/>, stretches it to fill
    /// <see cref="groupCardArea"/>, then populates position text, group name, and player rows.
    /// </summary>
    private GameObject BuildGroupCard(Group group)
    {
        if (groupCardPrefab == null || groupCardArea == null) return null;

        GameObject card = Instantiate(groupCardPrefab, groupCardArea);

        var rt = card.GetComponent<RectTransform>();
        if (rt != null)
        {
            // X: stretch across the full parent width (enables anchoredPosition.x slide).
            // Y: top-anchored so ContentSizeFitter can set sizeDelta.y = actual content height.
            //    Full-stretch on Y would make the card's height = parent height, preventing resize.
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.sizeDelta        = new Vector2(0f, 0f);
            rt.anchoredPosition = Vector2.zero;
        }

        // Position text — disable the prefab's hardcoded LocalizeStringEvent so we can set it dynamically.
        Transform headerTextTransform = card.transform.Find("Header/Text");
        if (headerTextTransform != null)
        {
            var localizer = headerTextTransform.GetComponent<LocalizeStringEvent>();
            if (localizer != null) localizer.enabled = false;
            var tmp = headerTextTransform.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = PositionLabel(group.position);
        }

        // Group name
        var groupNameTmp = FindTMP(card, "Content/Group Name");
        if (groupNameTmp != null) groupNameTmp.text = group.name;

        // Player name rows — use the prefab's existing field for the first player,
        // then instantiate copies for each additional player.
        // A LayoutElement is added to each row so the parent VLG (childControlHeight=0)
        // can correctly measure row heights when computing the card's total preferred height.
        Transform firstField = card.transform.Find("Content/Player Name Field");
        if (firstField != null)
        {
            List<Player> players = PlayerManager.GetPlayersWithGroupId(group.id);
            if (players.Count > 0)
            {
                float fieldHeight = firstField.GetComponent<RectTransform>().sizeDelta.y;
                SetPlayerNameField(firstField, players[0].name);
                EnsureLayoutElementHeight(firstField.gameObject, fieldHeight);
                for (int i = 1; i < players.Count; i++)
                {
                    // Instantiate clones the LayoutElement, so preferredHeight is already set.
                    GameObject copy = Instantiate(firstField.gameObject, firstField.parent);
                    SetPlayerNameField(copy.transform, players[i].name);
                }
            }
        }

        return card;
    }

    private void SetPlayerNameField(Transform field, string playerName)
    {
        Transform nameText = field.Find("Name Text");
        if (nameText == null) return;
        var tmp = nameText.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = playerName;
    }

    private void CleanupCurrentGroupCard()
    {
        if (currentGroupCard != null)
        {
            Destroy(currentGroupCard);
            currentGroupCard = null;
        }
    }

    private static string PositionLabel(GameManager.Position pos)
        => pos == GameManager.Position.For ? "For" : "Against";

    // ===================================================================
    //  Timer — Button Callbacks  (wire each Button's onClick in Inspector)
    // ===================================================================

    /// <summary>
    /// Starts the timer from full duration (Stopped) or resumes it from the paused time (Paused).
    /// Has no effect between rounds while transitioning, when already Running, or when all groups are done.
    /// Wire to the Play button's onClick event.
    /// </summary>
    public void OnPlayPressed()
    {
        EventSystem.current.SetSelectedGameObject(null);
        if (isTransitioning) return;

        switch (currentTimerState)
        {
            case TimerState.Stopped:
            case TimerState.Paused:
                StartTimer();
                break;
        }
    }

    /// <summary>
    /// Pauses the running timer. Has no effect between rounds (Stopped) or when all groups are done —
    /// the button stays un-toggled and clickable but is intentionally a no-op in those states.
    /// Wire to the Pause button's onClick event.
    /// </summary>
    public void OnPausePressed()
    {
        EventSystem.current.SetSelectedGameObject(null);
        if (isTransitioning || currentTimerState != TimerState.Running) return;

        StopTimerCoroutine();
        currentTimerState = TimerState.Paused;
        SelectToggle(pauseToggle);
    }

    /// <summary>
    /// Ends the current group's turn and advances to the next, triggering the slide animation
    /// and timer rewind. Has no effect while transitioning, when already Stopped, or when all
    /// groups are done.
    /// Wire to the Stop button's onClick event.
    /// </summary>
    public void OnStopPressed()
    {
        EventSystem.current.SetSelectedGameObject(null);
        if (isTransitioning) return;

        if (currentTimerState == TimerState.Running || currentTimerState == TimerState.Paused)
        {
            StopTimerCoroutine();
            AdvanceToNextGroup();
        }
    }

    // ===================================================================
    //  Timer — Internal
    // ===================================================================

    private void StartTimer()
    {
        currentTimerState = TimerState.Running;
        SelectToggle(playToggle);
        timerCoroutine = StartCoroutine(TimerCountdown());
    }

    private IEnumerator TimerCountdown()
    {
        while (timeRemaining > 0f)
        {
            timeRemaining -= Time.deltaTime;
            if (timeRemaining < 0f) timeRemaining = 0f;
            UpdateTimerText(timeRemaining);
            yield return null;
        }

        timerCoroutine = null;
        UpdateTimerText(0f);
        AdvanceToNextGroup();
    }

    private void AdvanceToNextGroup()
    {
        int nextIndex = currentGroupIndex + 1;

        if (nextIndex < groupTurnOrder.Count)
        {
            slideCoroutine = StartCoroutine(SlideInNextGroup(nextIndex));
        }
        else
        {
            currentTimerState = TimerState.AllGroupsDone;
            SelectToggle(stopToggle);
            SetTimerInteractable(false);
            if (nextButton != null) nextButton.SetActive(true);
            Debug.Log("DMDisplay: All groups have presented. Next button enabled.");
        }
    }

    /// <summary>
    /// Slides the current group card out to the left while the next slides in from the right
    /// (with bounce). The timer display rewinds back to MaxTime simultaneously.
    /// After the animation the corruption objectives are redistributed for the new group.
    /// </summary>
    private IEnumerator SlideInNextGroup(int nextIndex)
    {
        isTransitioning   = true;
        currentTimerState = TimerState.Stopped;
        SelectToggle(stopToggle);

        // Use the card area's live width so cards slide exactly one panel-width and stay
        // within the RectMask2D boundary rather than starting outside the masked viewport.
        float slideWidth = groupCardArea != null && groupCardArea.rect.width > 0f
            ? groupCardArea.rect.width
            : groupCardSlideDistance;

        GameObject newCard = BuildGroupCard(groupTurnOrder[nextIndex]);
        RectTransform newRT = newCard.GetComponent<RectTransform>();
        newRT.anchoredPosition = new Vector2(slideWidth, 0f);

        RectTransform oldRT = currentGroupCard != null
            ? currentGroupCard.GetComponent<RectTransform>()
            : null;

        float newCardHeight      = MeasureCardHeight(newRT);
        float elapsed            = 0f;
        float startTimeRemaining = timeRemaining;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / slideDuration);
            float eased = slideInCurve.Evaluate(t);

            if (oldRT != null)
                oldRT.anchoredPosition = new Vector2(Mathf.Lerp(0f, -slideWidth, t), 0f);

            newRT.anchoredPosition = new Vector2(
                Mathf.Lerp(slideWidth, 0f, eased),
                0f
            );

            // Rewind timer display while the card slides in
            timeRemaining = Mathf.Lerp(startTimeRemaining, timerDuration, t);
            UpdateTimerText(timeRemaining);

            yield return null;
        }

        // Settle
        if (currentGroupCard != null) Destroy(currentGroupCard);
        currentGroupCard       = newCard;
        newRT.anchoredPosition = Vector2.zero;
        ApplyGroupCardAreaHeight(newCardHeight);
        timeRemaining          = timerDuration;
        UpdateTimerText(timerDuration);

        currentGroupIndex = nextIndex;
        DistributeCardsForCurrentGroup();
        ForceObjectiveLayoutRebuild();

        slideCoroutine  = null;
        isTransitioning = false;
        Debug.Log($"DMDisplay: Now showing group '{groupTurnOrder[currentGroupIndex].name}'.");
    }

    /// <summary>
    /// Returns the card's preferred height. Two rebuild passes are used because TMP text
    /// components may defer their size calculation to the second pass.
    /// </summary>
    private float MeasureCardHeight(RectTransform cardRT)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardRT);
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardRT);
        float h = LayoutUtility.GetPreferredHeight(cardRT);
        return h > 0f ? h : cardRT.sizeDelta.y;
    }

    /// <summary>
    /// Sets groupCardArea's height directly via sizeDelta.y (groupCardArea uses fixed-point
    /// anchors so sizeDelta.y IS the absolute height), then rebuilds the scroll content so
    /// the parent VLG re-stacks all children at the new size.
    /// </summary>
    private void ApplyGroupCardAreaHeight(float height)
    {
        if (groupCardArea == null) return;
        groupCardArea.sizeDelta = new Vector2(groupCardArea.sizeDelta.x, height);
        var contentParent = groupCardArea.parent as RectTransform;
        if (contentParent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);
    }

    private static void EnsureLayoutElementHeight(GameObject go, float height)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
    }

    private void UpdateTimerText(float seconds)
    {
        if (timerText == null) return;
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs    = Mathf.FloorToInt(seconds % 60f);
        timerText.text = $"{minutes:00}:{secs:00}";
    }

    private void StopTimerCoroutine()
    {
        if (timerCoroutine == null) return;
        StopCoroutine(timerCoroutine);
        timerCoroutine = null;
    }

    private void SetTimerInteractable(bool on)
    {
        if (playToggle  != null) playToggle.interactable  = on;
        if (pauseToggle != null) pauseToggle.interactable = on;
        if (stopToggle  != null) stopToggle.interactable  = on;
    }

    /// <summary>Toggles <paramref name="active"/> on and the other two off.</summary>
    private void SelectToggle(ToggleButton active)
    {
        playToggle?.SetToggled(active == playToggle);
        pauseToggle?.SetToggled(active == pauseToggle);
        stopToggle?.SetToggled(active == stopToggle);
    }

    /// <summary>Advances from the DM screen to the Voting sequence. Wire to the Next button's onClick.</summary>
    public void ProceedToVoting()
    {
        Debug.Log("DMDisplay: Proceeding to Voting.");
        gameManager.StartVotingSequence();
    }

    // ===================================================================
    //  Corruption Cards
    // ===================================================================

    /// <summary>
    /// Instantiates one CorruptionCardController per non-DM player that has a Speech or
    /// Interruption corruption and stores references in the tracking dictionaries.
    /// Betrayal corruptions are intentionally omitted from the DM screen.
    /// </summary>
    private void InstantiateCorruptionCards()
    {
        CleanupCorruptionCards();

        int dmId = PlayerManager.dmId;
        int cardsCreated = 0;

        foreach (var player in PlayerManager.players.Values)
        {
            if (player.id == dmId)           continue;
            if (player.corruptionId < 0)     continue;

            Corruption objective = CorruptionManager.GetCorruptionByPlayerId(player.id);
            if (objective == null)
            {
                Debug.LogWarning($"DMDisplay: No corruption found for {player.name} (corruptionId={player.corruptionId})");
                continue;
            }

            switch (objective.type)
            {
                case GameManager.CorruptionType.Speech:
                {
                    GameObject card = CreateCorruptionCard(player);
                    if (card == null) break;
                    if (!speechCardsByGroupId.ContainsKey(player.group_id))
                        speechCardsByGroupId[player.group_id] = new List<GameObject>();
                    speechCardsByGroupId[player.group_id].Add(card);
                    cardsCreated++;
                    break;
                }
                case GameManager.CorruptionType.Interruption:
                {
                    GameObject card = CreateCorruptionCard(player);
                    if (card == null) break;
                    if (!interruptionCardsByGroupId.ContainsKey(player.group_id))
                        interruptionCardsByGroupId[player.group_id] = new List<GameObject>();
                    interruptionCardsByGroupId[player.group_id].Add(card);
                    cardsCreated++;
                    break;
                }
            }
        }

        Debug.Log($"DMDisplay: Created {cardsCreated} corruption card(s).");

        if (noCorruptionsPrefab != null && activeObjectiveContainer != null)
        {
            noCorruptionsCardInstance = Instantiate(noCorruptionsPrefab, activeObjectiveContainer);
            noCorruptionsCardInstance.SetActive(false);
        }
    }

    private GameObject CreateCorruptionCard(Player player)
    {
        if (corruptionPrefab == null || activeObjectiveContainer == null) return null;

        try
        {
            GameObject card = Instantiate(corruptionPrefab, activeObjectiveContainer);
            card.SetActive(true);
            var controller = card.GetComponent<CorruptionCardController>();
            if (controller != null) controller.Initialize(player.id);
            allInstantiatedCards.Add(card);
            return card;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"DMDisplay: Exception creating card for {player.name}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Re-parents all corruption cards for the current group.
    ///   Active   = Speech(current group)  + Interruption(other groups)
    ///   Inactive = Speech(other groups)   + Interruption(current group)
    /// </summary>
    private void DistributeCardsForCurrentGroup()
    {
        if (currentGroupIndex >= groupTurnOrder.Count) return;
        int currentGroupId = groupTurnOrder[currentGroupIndex].id;

        foreach (var kvp in speechCardsByGroupId)
        {
            Transform target = kvp.Key == currentGroupId ? activeObjectiveContainer : inactiveObjectiveContainer;
            foreach (var card in kvp.Value)
                card.transform.SetParent(target, false);
        }

        foreach (var kvp in interruptionCardsByGroupId)
        {
            Transform target = kvp.Key == currentGroupId ? inactiveObjectiveContainer : activeObjectiveContainer;
            foreach (var card in kvp.Value)
                card.transform.SetParent(target, false);
        }

        EnforceCardOrder(activeObjectiveContainer);
        EnforceCardOrder(inactiveObjectiveContainer);
        RefreshNoCorruptionsCard();
    }

    /// <summary>Reorders children so Speech cards always precede Interruption cards within a container.</summary>
    private void EnforceCardOrder(Transform container)
    {
        var speechSet = new HashSet<GameObject>();
        foreach (var list in speechCardsByGroupId.Values)
            foreach (var c in list)
                if (c != null) speechSet.Add(c);

        var speechCards       = new List<Transform>();
        var interruptionCards = new List<Transform>();

        foreach (Transform child in container)
        {
            if (speechSet.Contains(child.gameObject)) speechCards.Add(child);
            else interruptionCards.Add(child);
        }

        int idx = 0;
        foreach (var card in speechCards)       card.SetSiblingIndex(idx++);
        foreach (var card in interruptionCards)  card.SetSiblingIndex(idx++);
    }

    private void RefreshNoCorruptionsCard()
    {
        if (noCorruptionsCardInstance == null) return;
        int realCards = 0;
        foreach (Transform child in activeObjectiveContainer)
            if (child.gameObject != noCorruptionsCardInstance) realCards++;
        noCorruptionsCardInstance.SetActive(realCards == 0);
    }

    private void CleanupCorruptionCards()
    {
        foreach (var card in allInstantiatedCards)
            if (card != null) Destroy(card);
        allInstantiatedCards.Clear();
        speechCardsByGroupId.Clear();
        interruptionCardsByGroupId.Clear();

        if (noCorruptionsCardInstance != null)
        {
            Destroy(noCorruptionsCardInstance);
            noCorruptionsCardInstance = null;
        }

        DestroyAllChildren(activeObjectiveContainer);
        DestroyAllChildren(inactiveObjectiveContainer);
    }

    private void DestroyAllChildren(Transform container)
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
            DestroyImmediate(container.GetChild(i).gameObject);
    }

    // ===================================================================
    //  Objectives Slide  (wire each to its Button's onClick in Inspector)
    // ===================================================================

    /// <summary>Slides the objective track to show the Active (centre) panel.</summary>
    public void ShowActiveObjectives()   => targetAnchoredPos = defaultAnchoredPos;

    /// <summary>Slides the objective track left to reveal the Accusation panel.</summary>
    public void ShowAccusation()         => targetAnchoredPos = defaultAnchoredPos + new Vector2(objectivesSlideOffset, 0f);

    /// <summary>Slides the objective track right to reveal the Inactive panel.</summary>
    public void ShowInactiveObjectives() => targetAnchoredPos = defaultAnchoredPos + new Vector2(-objectivesSlideOffset, 0f);

    private void SnapToActiveObjectives()
    {
        targetAnchoredPos = defaultAnchoredPos;
        if (slideTrack != null) slideTrack.anchoredPosition = defaultAnchoredPos;
    }

    // ===================================================================
    //  Layout Rebuild
    // ===================================================================

    private void ForceObjectiveLayoutRebuild()
    {
        if (activeObjectiveContainer   is RectTransform ar) LayoutRebuilder.ForceRebuildLayoutImmediate(ar);
        if (inactiveObjectiveContainer is RectTransform ir) LayoutRebuilder.ForceRebuildLayoutImmediate(ir);
        if (objectivesContainer != null)                     LayoutRebuilder.ForceRebuildLayoutImmediate(objectivesContainer);
    }

    // ===================================================================
    //  Helpers
    // ===================================================================

    private TextMeshProUGUI FindTMP(GameObject root, string path)
    {
        Transform t = root.transform.Find(path);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    /// <summary>
    /// Builds a default slide-in curve with a slight overshoot and settle (cassette-snap feel).
    /// Used when no curve is configured in the Inspector.
    /// </summary>
    private static AnimationCurve BuildDefaultBounceCurve()
    {
        var curve = new AnimationCurve();
        curve.AddKey(new Keyframe(0f,    0f,    0f,   3.5f));
        curve.AddKey(new Keyframe(0.65f, 1.08f, 0f,   0f));
        curve.AddKey(new Keyframe(0.82f, 0.96f, 0f,   0f));
        curve.AddKey(new Keyframe(1f,    1f,    0f,   0f));
        return curve;
    }
}
