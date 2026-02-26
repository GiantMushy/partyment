using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class DMDisplayController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;
    private SecretObjectiveManager SecretObjectiveManager => gameManager.secretObjectiveManager;
    private TopicManager TopicManager => gameManager.topicManager;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI topicDescription;
    [SerializeField] private TextMeshProUGUI nextGroupText;
    [SerializeField] private GameObject activeSpeechObjectiveContainer;
    [SerializeField] private GameObject interruptionObjectiveContainer;
    [SerializeField] private GameObject inactiveSpeechObjectiveContainer;
    [SerializeField] private GameObject secretObjectivePrefab;

    [Header("Timer Elements")]
    [SerializeField] private GameObject timerButton;
    [SerializeField] private TextMeshProUGUI startTimerText;
    [SerializeField] private TextMeshProUGUI stopTimerText;
    [SerializeField] private UnityEngine.UI.Image startTimerIcon;
    [SerializeField] private UnityEngine.UI.Image stopTimerIcon;
    [SerializeField] private Sprite timerOffBackgroundSprite;
    [SerializeField] private Sprite timerOnBackgroundSprite;
    [SerializeField] private float timerDuration = 60f; // Duration in seconds (soft limit)

    // Timer state machine
    private enum TimerState { Idle, Running, Expired, AllGroupsDone }
    private TimerState currentTimerState = TimerState.Idle;
    private Coroutine timerCoroutine;
    private float timeRemaining;

    // Turn management
    private List<Group> groupTurnOrder = new List<Group>();
    private int currentGroupIndex = 0;

    // Secret objective card tracking — maps group ID to its Speech cards
    private Dictionary<int, List<GameObject>> speechCardsByGroupId = new Dictionary<int, List<GameObject>>();
    private List<GameObject> allInstantiatedCards = new List<GameObject>();

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

    // ===================================================================
    //  INITIALIZATION
    // ===================================================================

    private void InitializeDisplay()
    {
        // Show the active topic
        DisplayActiveTopic();

        // Build turn order and reset index
        DetermineGroupTurnOrder();
        currentGroupIndex = 0;

        // Populate secret objective cards into their containers
        InstantiateSecretObjectiveCards();

        // Activate the first group's speech objectives
        if (groupTurnOrder.Count > 0)
        {
            ActivateGroupSpeechCards(groupTurnOrder[0].id);
            UpdateNextGroupText();
        }

        // Hide any containers that have no cards
        UpdateContainerVisibility();

        // Reset the timer button to its idle state
        SetTimerIdle();
    }

    private void DisplayActiveTopic()
    {
        Topic currentTopic = TopicManager.currentTopic;
        if (currentTopic != null)
        {
            topicDescription.text = currentTopic.description;
        }
        else
        {
            topicDescription.text = "No topic selected.";
            Debug.LogWarning("DMDisplayController: No active topic found.");
        }
    }

    // ===================================================================
    //  TURN ORDER
    // ===================================================================

    /// <summary>
    /// Determines the order in which groups take their turns.
    /// Currently ordered by ascending group ID.
    /// To randomize or change the order, modify the LINQ query below.
    /// </summary>
    private void DetermineGroupTurnOrder()
    {
        // ---- Turn ordering logic (edit here to change order) ----
        groupTurnOrder = PlayerManager.groups.Values
            .OrderBy(g => g.id)
            .ToList();
        // ---------------------------------------------------------

        Debug.Log($"DMDisplay: Turn order determined — {groupTurnOrder.Count} group(s).");
    }

    // ===================================================================
    //  SECRET OBJECTIVE CARDS
    // ===================================================================

    /// <summary>
    /// Instantiates a card for every non-DM player who has a Speech or
    /// Interruption objective. Speech cards start in the inactive container
    /// and are moved to active when their group's turn begins.
    /// Betrayal objectives are intentionally omitted from the DM display.
    /// </summary>
    private void InstantiateSecretObjectiveCards()
    {
        CleanupCards();

        int dmId = PlayerManager.players.Keys.Min();

        foreach (var player in PlayerManager.players.Values)
        {
            if (player.id == dmId) continue;
            if (player.secretObjectiveId < 0) continue;

            SecretObjective objective = SecretObjectiveManager.GetSecretObjectiveByPlayerId(player.id);
            if (objective == null) continue;

            switch (objective.type)
            {
                case GameManager.SecretObjectiveType.Speech:
                    InstantiateCard(player, inactiveSpeechObjectiveContainer.transform, player.group_id);
                    break;
                case GameManager.SecretObjectiveType.Interruption:
                    InstantiateCard(player, interruptionObjectiveContainer.transform);
                    break;
                // Betrayal objectives are NOT shown on the DM screen
            }
        }
    }

    /// <summary>
    /// Instantiates a single SecObj card under the given parent.
    /// If groupId is provided, the card is tracked as a Speech card for that group.
    /// </summary>
    private void InstantiateCard(Player player, Transform parent, int groupId = -1)
    {
        GameObject card = Instantiate(secretObjectivePrefab, parent);
        SecObjCardController controller = card.GetComponent<SecObjCardController>();
        controller.Initialize(player.id);

        if (groupId >= 0)
        {
            if (!speechCardsByGroupId.ContainsKey(groupId))
                speechCardsByGroupId[groupId] = new List<GameObject>();
            speechCardsByGroupId[groupId].Add(card);
        }

        allInstantiatedCards.Add(card);
    }

    /// <summary>
    /// Moves the given group's Speech cards into the active container.
    /// </summary>
    private void ActivateGroupSpeechCards(int groupId)
    {
        if (!speechCardsByGroupId.ContainsKey(groupId)) return;

        foreach (var card in speechCardsByGroupId[groupId])
            card.transform.SetParent(activeSpeechObjectiveContainer.transform, false);

        UpdateContainerVisibility();
        Debug.Log($"DMDisplay: Group '{GetGroupName(groupId)}' speech objectives are now active.");
    }

    /// <summary>
    /// Moves the given group's Speech cards back to the inactive container.
    /// </summary>
    private void DeactivateGroupSpeechCards(int groupId)
    {
        if (!speechCardsByGroupId.ContainsKey(groupId)) return;

        foreach (var card in speechCardsByGroupId[groupId])
            card.transform.SetParent(inactiveSpeechObjectiveContainer.transform, false);

        UpdateContainerVisibility();
    }

    private string GetGroupName(int groupId)
    {
        return PlayerManager.groups.ContainsKey(groupId)
            ? PlayerManager.groups[groupId].name
            : groupId.ToString();
    }

    /// <summary>
    /// Enables or disables each objective container based on whether it has
    /// any cards beyond the first child (the Header).
    /// </summary>
    private void UpdateContainerVisibility()
    {
        activeSpeechObjectiveContainer.SetActive(activeSpeechObjectiveContainer.transform.childCount > 1);
        inactiveSpeechObjectiveContainer.SetActive(inactiveSpeechObjectiveContainer.transform.childCount > 1);
        interruptionObjectiveContainer.SetActive(interruptionObjectiveContainer.transform.childCount > 1);
    }

    private void CleanupCards()
    {
        foreach (var card in allInstantiatedCards)
        {
            if (card != null) Destroy(card);
        }
        allInstantiatedCards.Clear();
        speechCardsByGroupId.Clear();
    }

    // ===================================================================
    //  TIMER — Public entry point (wire to Button.onClick in Inspector)
    // ===================================================================

    /// <summary>
    /// Called by the timer button's onClick event.
    /// Behaviour depends on the current timer state:
    ///   Idle         → Start the countdown
    ///   Running      → Stop early and advance to next group
    ///   Expired      → Acknowledge and advance to next group
    ///   AllGroupsDone→ Proceed to Voting
    /// </summary>
    public void TimerButtonPressed()
    {
        switch (currentTimerState)
        {
            case TimerState.Idle:
                StartTimer();
                break;

            case TimerState.Running:
                StopTimerEarly();
                break;

            case TimerState.Expired:
                AcknowledgeTimerExpired();
                break;

            case TimerState.AllGroupsDone:
                ProceedToVoting();
                break;
        }
    }

    // ===================================================================
    //  TIMER — Internal logic
    // ===================================================================

    private void StartTimer()
    {
        currentTimerState = TimerState.Running;
        timeRemaining = timerDuration;

        ShowStopTimerVisuals();
        SetTimerButtonBackground(timerOnBackgroundSprite);
        UpdateTimerText(timeRemaining);

        timerCoroutine = StartCoroutine(TimerCountdown());

        Debug.Log($"DMDisplay: Timer started for group '{groupTurnOrder[currentGroupIndex].name}'.");
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

        // Timer expired — stay at 00:00 until the DM presses the button
        currentTimerState = TimerState.Expired;
        UpdateTimerText(0f);
        Debug.Log("DMDisplay: Timer expired. Waiting for DM to acknowledge.");
    }

    /// <summary>DM stopped the timer before it expired.</summary>
    private void StopTimerEarly()
    {
        StopTimerCoroutine();
        AdvanceToNextGroup();
    }

    /// <summary>DM acknowledged the expired timer.</summary>
    private void AcknowledgeTimerExpired()
    {
        AdvanceToNextGroup();
    }

    /// <summary>
    /// Moves the current group's speech cards to inactive,
    /// activates the next group, and resets the timer —
    /// or shows "Next" if all groups have had their turn.
    /// </summary>
    private void AdvanceToNextGroup()
    {
        // Deactivate the current group's speech objectives
        if (currentGroupIndex < groupTurnOrder.Count)
            DeactivateGroupSpeechCards(groupTurnOrder[currentGroupIndex].id);

        currentGroupIndex++;

        if (currentGroupIndex < groupTurnOrder.Count)
        {
            // More groups remain
            ActivateGroupSpeechCards(groupTurnOrder[currentGroupIndex].id);
            UpdateNextGroupText();
            SetTimerIdle();
            Debug.Log($"DMDisplay: Advanced to group '{groupTurnOrder[currentGroupIndex].name}'.");
        }
        else
        {
            // All groups have gone — show "Next"
            currentTimerState = TimerState.AllGroupsDone;
            ShowStartTimerVisuals();
            startTimerText.text = "Next";
            SetTimerButtonBackground(timerOffBackgroundSprite);
            nextGroupText.text = "";
            Debug.Log("DMDisplay: All groups have presented. Ready to proceed to Voting.");
        }
    }

    private void ProceedToVoting()
    {
        Debug.Log("DMDisplay: Proceeding to Voting.");
        gameManager.StartVotingSequence();
    }

    // ===================================================================
    //  TIMER — Helpers
    // ===================================================================

    private void SetTimerIdle()
    {
        currentTimerState = TimerState.Idle;
        ShowStartTimerVisuals();
        startTimerText.text = "Start Timer";
        SetTimerButtonBackground(timerOffBackgroundSprite);
    }

    private void ShowStartTimerVisuals()
    {
        startTimerText.gameObject.SetActive(true);
        startTimerIcon.gameObject.SetActive(true);
        stopTimerText.gameObject.SetActive(false);
        stopTimerIcon.gameObject.SetActive(false);
    }

    private void ShowStopTimerVisuals()
    {
        startTimerText.gameObject.SetActive(false);
        startTimerIcon.gameObject.SetActive(false);
        stopTimerText.gameObject.SetActive(true);
        stopTimerIcon.gameObject.SetActive(true);
    }

    private void SetTimerButtonBackground(Sprite backgroundSprite)
    {
        var buttonImage = timerButton.GetComponent<UnityEngine.UI.Image>();
        if (buttonImage != null)
            buttonImage.sprite = backgroundSprite;
    }

    private void UpdateTimerText(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        stopTimerText.text = $"{minutes:00}:{secs:00}";
    }

    private void StopTimerCoroutine()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }
    }

    // ===================================================================
    //  GROUP DISPLAY
    // ===================================================================

    private void UpdateNextGroupText()
    {
        if (currentGroupIndex >= groupTurnOrder.Count)
        {
            nextGroupText.text = "";
            return;
        }

        Group currentGroup = groupTurnOrder[currentGroupIndex];

        var playersInGroup = PlayerManager.players.Values
            .Where(p => p.group_id == currentGroup.id)
            .ToList();

        string playerNames = FormatPlayerNames(playersInGroup);
        nextGroupText.text = $"Group: {currentGroup.name}\nPlayers: {playerNames}";
    }

    private string FormatPlayerNames(List<Player> players)
    {
        if (players.Count == 0) return "None";
        if (players.Count == 1) return players[0].name;
        if (players.Count == 2) return $"{players[0].name} & {players[1].name}";

        var names = players.Select(p => p.name).ToList();
        string allButLast = string.Join(", ", names.Take(names.Count - 1));
        return $"{allButLast}, & {names.Last()}";
    }
}
