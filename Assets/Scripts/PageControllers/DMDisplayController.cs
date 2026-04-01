using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DMDisplayController : MonoBehaviour
{
    // ===================================================================
    //  Inspector References
    // ===================================================================

    [Header("Managers (auto-assigned)")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;
    private SecretObjectiveManager SecretObjectiveManager => gameManager.secretObjectiveManager;
    private TopicManager TopicManager => gameManager.topicManager;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI topicDescription;
    [SerializeField] private TextMeshProUGUI nextGroupNameText;
    [SerializeField] private TextMeshProUGUI nextGroupPlayersText;
    [SerializeField] private TextMeshProUGUI currPositionText;

    [Header("Objective Containers")]
    [Tooltip("RectTransform with HorizontalLayoutGroup. Slides left/right to reveal active or inactive panel.")]
    [SerializeField] private RectTransform objectivesContainer;
    [Tooltip("Left child of objectivesContainer — Speech(current group) + Interruption(all other groups).")]
    [SerializeField] private Transform activeObjectiveContainer;
    [Tooltip("Right child of objectivesContainer — Speech(all other groups) + Interruption(current group).")]
    [SerializeField] private Transform inactiveObjectiveContainer;
    [SerializeField] private GameObject secretObjectivePrefab;

    [Header("Objectives Slide Settings")]
    [Tooltip("How many pixels the container shifts left to bring the inactive panel into view.")]
    [SerializeField] private float objectivesSlideOffset = 800f;
    [Tooltip("Lerp speed of the slide animation. Higher = faster.")]
    [SerializeField] private float objectivesSlideSpeed = 8f;

    [Header("Timer")]
    [SerializeField] private TextMeshProUGUI timerText;
    [Tooltip("Starts (Idle) or resumes (Paused) the timer. Disabled while running.")]
    [SerializeField] private Button startTimerButton;
    [Tooltip("Pauses the running timer. Initially disabled; enabled only while running.")]
    [SerializeField] private Button pauseTimerButton;
    [Tooltip("Resets the timer and advances to the next group's turn.")]
    [SerializeField] private Button stopTimerButton;
    [Tooltip("Starts disabled. Enabled after the last group's Stop is pressed. Wire onClick to ProceedToVoting().")]
    [SerializeField] private GameObject nextButton;
    [SerializeField] private float timerDuration = 60f;

    // ===================================================================
    //  Private State
    // ===================================================================

    private enum TimerState { Idle, Running, Paused, Expired, AllGroupsDone }
    private TimerState currentTimerState = TimerState.Idle;
    private Coroutine timerCoroutine;
    private float timeRemaining;

    private List<Group> groupTurnOrder = new List<Group>();
    private int currentGroupIndex = 0;

    /// <summary>Maps each group_id to its instantiated Speech objective cards.</summary>
    private Dictionary<int, List<GameObject>> speechCardsByGroupId = new Dictionary<int, List<GameObject>>();
    /// <summary>Maps each group_id to its instantiated Interruption objective cards.</summary>
    private Dictionary<int, List<GameObject>> interruptionCardsByGroupId = new Dictionary<int, List<GameObject>>();
    private List<GameObject> allInstantiatedCards = new List<GameObject>();

    /// <summary>Anchored position of objectivesContainer when the Active panel is centred on screen.</summary>
    private Vector2 defaultAnchoredPos;
    /// <summary>The position the container is currently lerping towards.</summary>
    private Vector2 targetAnchoredPos;

    // ===================================================================
    //  Unity Lifecycle
    // ===================================================================

    void Awake()
    {
        // Cache before any layout or code can shift the container
        if (objectivesContainer != null)
        {
            defaultAnchoredPos = objectivesContainer.anchoredPosition;
            targetAnchoredPos  = defaultAnchoredPos;
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
        CleanupCards();
        StopTimerCoroutine();
    }

    void Update()
    {
        if (objectivesContainer == null) return;
        objectivesContainer.anchoredPosition = Vector2.Lerp(
            objectivesContainer.anchoredPosition,
            targetAnchoredPos,
            Time.deltaTime * objectivesSlideSpeed
        );
    }

    // ===================================================================
    //  Initialization
    // ===================================================================

    private void InitializeDisplay()
    {
        DisplayActiveTopic();

        DetermineGroupTurnOrder();
        currentGroupIndex = 0;

        InstantiateSecretObjectiveCards();

        if (groupTurnOrder.Count > 0)
        {
            DistributeCardsForCurrentGroup();
            UpdateGroupInfoText();
        }

        ResetTimerToIdle();

        if (nextButton != null) nextButton.SetActive(false);

        // Snap — no animation — back to active panel on each entry
        SnapToActiveObjectives();

        // Force layout rebuild after all cards are placed
        ForceObjectiveLayoutRebuild();
    }

    private void DisplayActiveTopic()
    {
        if (topicDescription == null) return;
        Topic currentTopic = TopicManager.currentTopic;
        if (currentTopic != null)
            topicDescription.text = currentTopic.description;
        else
        {
            topicDescription.text = "No topic selected.";
            Debug.LogWarning("DMDisplayController: No active topic found.");
        }
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
    //  Secret Objective Cards
    // ===================================================================

    /// <summary>
    /// Instantiates one SecObjCardController card per non-DM player that has a
    /// Speech or Interruption objective and stores it in the tracking dictionaries.
    /// Betrayal objectives are intentionally omitted from the DM screen.
    /// </summary>
    private void InstantiateSecretObjectiveCards()
    {
        CleanupCards();

        int dmId = PlayerManager.dmId;
        int playerCount = PlayerManager.players.Count;
        Debug.Log($"DMDisplay: InstantiateSecretObjectiveCards — {playerCount} player(s), dmId={dmId}");
        Debug.Log($"DMDisplay: activeObjectiveContainer={(activeObjectiveContainer != null ? activeObjectiveContainer.name : "NULL")}, secretObjectivePrefab={(secretObjectivePrefab != null ? secretObjectivePrefab.name : "NULL")}");

        int cardsCreated = 0;

        foreach (var player in PlayerManager.players.Values)
        {
            if (player.id == dmId)
            {
                Debug.Log($"DMDisplay: Skipping DM player {player.name} (ID {player.id})");
                continue;
            }
            if (player.secretObjectiveId < 0)
            {
                Debug.Log($"DMDisplay: Skipping {player.name} (ID {player.id}) — no objective (Civilian)");
                continue;
            }

            SecretObjective objective = SecretObjectiveManager.GetSecretObjectiveByPlayerId(player.id);
            if (objective == null)
            {
                Debug.LogWarning($"DMDisplay: Skipping {player.name} (ID {player.id}) — GetSecretObjectiveByPlayerId returned null despite secretObjectiveId={player.secretObjectiveId}");
                continue;
            }

            Debug.Log($"DMDisplay: Player {player.name} has objective '{objective.title}' type={objective.type} group={player.group_id}");

            switch (objective.type)
            {
                case GameManager.SecretObjectiveType.Speech:
                {
                    GameObject card = CreateCard(player);
                    if (card == null) break;
                    if (!speechCardsByGroupId.ContainsKey(player.group_id))
                        speechCardsByGroupId[player.group_id] = new List<GameObject>();
                    speechCardsByGroupId[player.group_id].Add(card);
                    cardsCreated++;
                    break;
                }
                case GameManager.SecretObjectiveType.Interruption:
                {
                    GameObject card = CreateCard(player);
                    if (card == null) break;
                    if (!interruptionCardsByGroupId.ContainsKey(player.group_id))
                        interruptionCardsByGroupId[player.group_id] = new List<GameObject>();
                    interruptionCardsByGroupId[player.group_id].Add(card);
                    cardsCreated++;
                    break;
                }
                default:
                    Debug.Log($"DMDisplay: Skipping {player.name} — objective type {objective.type} not shown on DM screen");
                    break;
            }
        }

        Debug.Log($"DMDisplay: Created {cardsCreated} objective card(s). Speech groups: {speechCardsByGroupId.Count}, Interruption groups: {interruptionCardsByGroupId.Count}");
    }

    private GameObject CreateCard(Player player)
    {
        if (secretObjectivePrefab == null)
        {
            Debug.LogError("DMDisplayController: secretObjectivePrefab is NULL!");
            return null;
        }
        if (activeObjectiveContainer == null)
        {
            Debug.LogError("DMDisplayController: activeObjectiveContainer is NULL!");
            return null;
        }

        try
        {
            GameObject card = Instantiate(secretObjectivePrefab, activeObjectiveContainer);
            card.SetActive(true);

            var controller = card.GetComponent<SecObjCardController>();
            if (controller == null)
            {
                Debug.LogError($"DMDisplay: Instantiated prefab has no SecObjCardController! Prefab name: {secretObjectivePrefab.name}");
                return card;
            }

            controller.Initialize(player.id);
            allInstantiatedCards.Add(card);
            Debug.Log($"DMDisplay: Created card for {player.name} (ID {player.id}, group {player.group_id}) — now {activeObjectiveContainer.childCount} children in active container");
            return card;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"DMDisplay: Exception creating card for {player.name}: {e.Message}\n{e.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Re-parents all cards into the correct container for whichever group is currently presenting.
    ///
    ///   Active   = Speech(current group)  +  Interruption(all other groups)
    ///   Inactive = Speech(all other groups)  +  Interruption(current group)
    ///
    /// Within each container Speech cards always appear before Interruption cards.
    /// </summary>
    private void DistributeCardsForCurrentGroup()
    {
        if (currentGroupIndex >= groupTurnOrder.Count) return;
        int currentGroupId = groupTurnOrder[currentGroupIndex].id;

        // Route Speech cards — current group → active, everyone else → inactive
        foreach (var kvp in speechCardsByGroupId)
        {
            Transform target = kvp.Key == currentGroupId
                ? activeObjectiveContainer
                : inactiveObjectiveContainer;
            foreach (var card in kvp.Value)
                card.transform.SetParent(target, false);
        }

        // Route Interruption cards — opposite of Speech
        foreach (var kvp in interruptionCardsByGroupId)
        {
            Transform target = kvp.Key == currentGroupId
                ? inactiveObjectiveContainer
                : activeObjectiveContainer;
            foreach (var card in kvp.Value)
                card.transform.SetParent(target, false);
        }

        // Enforce speech-before-interruption ordering inside each container
        EnforceCardOrder(activeObjectiveContainer);
        EnforceCardOrder(inactiveObjectiveContainer);
    }

    /// <summary>
    /// Reorders children of <paramref name="container"/> so every Speech card
    /// precedes every Interruption card. Uses SetSiblingIndex — no re-instantiation.
    /// </summary>
    private void EnforceCardOrder(Transform container)
    {
        // Build a fast lookup set of all speech GameObjects
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

    private void CleanupCards()
    {
        foreach (var card in allInstantiatedCards)
            if (card != null) Destroy(card);
        allInstantiatedCards.Clear();
        speechCardsByGroupId.Clear();
        interruptionCardsByGroupId.Clear();

        // Clear any remaining children in the objective containers
        // (handles scene-placed prefabs or untracked leftovers from a previous layout)
        DestroyAllChildren(activeObjectiveContainer);
        DestroyAllChildren(inactiveObjectiveContainer);
    }

    private void DestroyAllChildren(Transform container)
    {
        if (container == null) return;
        int count = container.childCount;
        for (int i = count - 1; i >= 0; i--)
            DestroyImmediate(container.GetChild(i).gameObject);
        if (count > 0)
            Debug.Log($"DMDisplay: Cleared {count} leftover child(ren) from {container.name}");
    }

    // ===================================================================
    //  Timer — Button Callbacks  (wire each to its Button's onClick)
    // ===================================================================

    /// <summary>
    /// Starts the timer from full duration (Idle) or resumes from where it paused (Paused).
    /// Wire to the Start button's onClick event.
    /// </summary>
    public void OnStartPressed()
    {
        EventSystem.current.SetSelectedGameObject(null);
        switch (currentTimerState)
        {
            case TimerState.Idle:
                timeRemaining = timerDuration;
                RunTimer();
                break;
            case TimerState.Paused:
                RunTimer();
                break;
        }
    }

    /// <summary>
    /// Pauses the running timer, preserving the remaining time for resumption.
    /// Wire to the Pause button's onClick event.
    /// </summary>
    public void OnPausePressed()
    {
        EventSystem.current.SetSelectedGameObject(null);
        if (currentTimerState != TimerState.Running) return;

        StopTimerCoroutine();
        currentTimerState = TimerState.Paused;
        RefreshTimerButtons();
    }

    /// <summary>
    /// Resets the timer and advances to the next group's turn (or proceeds to Voting
    /// once every group has presented).
    /// Wire to the Stop button's onClick event.
    /// </summary>
    public void OnStopPressed()
    {
        EventSystem.current.SetSelectedGameObject(null);
        switch (currentTimerState)
        {
            case TimerState.Running:
            case TimerState.Paused:
            case TimerState.Expired:
                StopTimerCoroutine();
                AdvanceToNextGroup();
                break;
            case TimerState.AllGroupsDone:
                ProceedToVoting();
                break;
        }
    }

    // ===================================================================
    //  Timer — Internal
    // ===================================================================

    private void RunTimer()
    {
        currentTimerState = TimerState.Running;
        RefreshTimerButtons();
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

        currentTimerState = TimerState.Expired;
        RefreshTimerButtons();
        UpdateTimerText(0f);
        Debug.Log("DMDisplay: Timer expired. Press Stop to advance to the next group.");
    }

    private void AdvanceToNextGroup()
    {
        currentGroupIndex++;

        if (currentGroupIndex < groupTurnOrder.Count)
        {
            DistributeCardsForCurrentGroup();
            UpdateGroupInfoText();
            ResetTimerToIdle();
            ForceObjectiveLayoutRebuild();
            Debug.Log($"DMDisplay: Now showing group '{groupTurnOrder[currentGroupIndex].name}'.");
        }
        else
        {
            currentTimerState = TimerState.AllGroupsDone;
            RefreshTimerButtons();
            ClearGroupInfoText();
            if (nextButton != null) nextButton.SetActive(true);
            Debug.Log("DMDisplay: All groups have presented. Press Next to proceed to Voting.");
        }
    }

    public void ProceedToVoting()
    {
        Debug.Log("DMDisplay: Proceeding to Voting.");
        gameManager.StartVotingSequence();
    }

    private void ResetTimerToIdle()
    {
        StopTimerCoroutine();
        currentTimerState = TimerState.Idle;
        timeRemaining = timerDuration;
        UpdateTimerText(timerDuration);
        RefreshTimerButtons();
    }

    /// <summary>
    /// Sets each timer button's interactable state for the current TimerState.
    ///
    ///   State          | Start | Pause | Stop
    ///   Idle           |   ✓   |   ✗   |   ✗
    ///   Running        |   ✗   |   ✓   |   ✓
    ///   Paused         |   ✓   |   ✗   |   ✓
    ///   Expired        |   ✗   |   ✗   |   ✓
    ///   AllGroupsDone  |   ✗   |   ✗   |   ✓
    /// </summary>
    private void RefreshTimerButtons()
    {
        bool start = currentTimerState == TimerState.Idle
                  || currentTimerState == TimerState.Paused;
        bool pause = currentTimerState == TimerState.Running;
        bool stop  = currentTimerState == TimerState.Running
                  || currentTimerState == TimerState.Paused
                  || currentTimerState == TimerState.Expired
                  || currentTimerState == TimerState.AllGroupsDone;

        if (startTimerButton != null) startTimerButton.gameObject.SetActive(start);
        if (pauseTimerButton != null) pauseTimerButton.gameObject.SetActive(pause);
        if (stopTimerButton  != null) stopTimerButton.interactable  = stop;
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

    // ===================================================================
    //  Group Info Text
    // ===================================================================

    private void UpdateGroupInfoText()
    {
        if (currentGroupIndex >= groupTurnOrder.Count)
        {
            ClearGroupInfoText();
            return;
        }

        Group currentGroup   = groupTurnOrder[currentGroupIndex];
        var   groupPlayers   = PlayerManager.players.Values
                                   .Where(p => p.group_id == currentGroup.id)
                                   .ToList();

        if (nextGroupNameText    != null) nextGroupNameText.text    = currentGroup.name;
        if (currPositionText     != null) currPositionText.text     = currentGroup.position == GameManager.Position.For ? "For" : "Against";
        if (nextGroupPlayersText != null) nextGroupPlayersText.text = FormatPlayerNames(groupPlayers);
    }

    private void ClearGroupInfoText()
    {
        if (nextGroupNameText    != null) nextGroupNameText.text    = "";
        if (currPositionText     != null) currPositionText.text     = "";
        if (nextGroupPlayersText != null) nextGroupPlayersText.text = "";
    }

    private string FormatPlayerNames(List<Player> players)
    {
        if (players.Count == 0) return "None";
        if (players.Count == 1) return players[0].name;
        if (players.Count == 2) return $"{players[0].name} & {players[1].name}";

        var names = players.Select(p => p.name).ToList();
        return $"{string.Join(", ", names.Take(names.Count - 1))}, & {names.Last()}";
    }

    // ===================================================================
    //  Objectives Slide  (wire each to its Button's onClick in Inspector)
    // ===================================================================

    /// <summary>
    /// Smoothly slides objectivesContainer back to its default position,
    /// bringing the Active panel into view.
    /// Wire to the "Active" button's onClick event.
    /// </summary>
    public void ShowActiveObjectives()
    {
        targetAnchoredPos = defaultAnchoredPos;
    }

    /// <summary>
    /// Smoothly slides objectivesContainer right by <see cref="objectivesSlideOffset"/>,
    /// bringing the Accusation panel into view (left panel).
    /// Wire to the "Accusation" button's onClick event.
    /// </summary>
    public void ShowAccusation()
    {
        targetAnchoredPos = defaultAnchoredPos + new Vector2(objectivesSlideOffset, 0f);
    }

    /// <summary>
    /// Smoothly slides objectivesContainer left by <see cref="objectivesSlideOffset"/>,
    /// bringing the Inactive panel into view.
    /// Wire to the "Inactive" button's onClick event.
    /// </summary>
    public void ShowInactiveObjectives()
    {
        targetAnchoredPos = defaultAnchoredPos + new Vector2(-objectivesSlideOffset, 0f);
    }

    /// <summary>Snaps the container to the active position instantly — used on screen entry.</summary>
    private void SnapToActiveObjectives()
    {
        targetAnchoredPos = defaultAnchoredPos;
        if (objectivesContainer != null)
            objectivesContainer.anchoredPosition = defaultAnchoredPos;
    }

    // ===================================================================
    //  Layout Rebuild
    // ===================================================================

    /// <summary>
    /// Forces Unity's layout system to recalculate sizes bottom-up for
    /// the objective containers, then the parent objectivesContainer.
    /// Fixes ContentSizeFitter overflow when cards are instantiated.
    /// </summary>
    private void ForceObjectiveLayoutRebuild()
    {
        if (activeObjectiveContainer is RectTransform activeRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(activeRect);
        if (inactiveObjectiveContainer is RectTransform inactiveRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(inactiveRect);
        if (objectivesContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(objectivesContainer);
    }
}
