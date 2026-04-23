using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the Accusation sub-panel on the DMDisplay page.
/// Attach to the root of the VerticalLayoutGroup that contains:
///   [0] Header (TMP)
///   [1] Description (TMP)
///   [2-9] Button (Player1-Player8)
///   [10] Button (Incorrect)  — disabled by default
/// </summary>
public class AccusationController : MonoBehaviour
{
    // ===================================================================
    //  Serialized Inner Type
    // ===================================================================

    [System.Serializable]
    public class PlayerButtonUI
    {
        public Button        button;
        public Image         borderImage;
        public Image         backgroundImage;
        public TextMeshProUGUI nameText;
        public Image         iconImage;
    }

    // ===================================================================
    //  Inspector References
    // ===================================================================

    [Header("Layout Elements")]
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button          incorrectButton;

    [Header("Player Buttons (assign Player1 → Player8 in order)")]
    [SerializeField] private PlayerButtonUI[] playerButtons = new PlayerButtonUI[8];

    [Header("Descriptions")]
    [SerializeField] private string defaultDescription         = "Select a player to begin an accusation.";
    [SerializeField] private string playerSelectedDescription  = "Select the player you believe this player is accusing of Corruption. Tap the selected player again to cancel.";

    [Header("Visuals")]
    [Tooltip("Icon displayed on non-selected player buttons while a player is selected.")]
    [SerializeField] private Sprite pointIcon;
    [Tooltip("Color applied to the Border, Background, and (white) Text of non-selected buttons during PlayerSelected state.")]
    [SerializeField] private Color  accusedColor = new Color(0.80f, 0.10f, 0.10f, 1f);

    [Header("Accusation Settings")]
    [Tooltip("Points deducted from the accusing player on an incorrect accusation.")]
    [SerializeField] private int incorrectPenalty = 20;

    [Header("Animation")]
    [Tooltip("Duration in seconds for the selected button slide animation.")]
    [SerializeField] private float moveDuration = 0.3f;

    // ===================================================================
    //  Private State
    // ===================================================================

    private GameManager            gameManager;
    private PlayerManager          PlayerManager          => gameManager.playerManager;
    private SecretObjectiveManager SecretObjectiveManager => gameManager.secretObjectiveManager;

    private enum AccusationState { Default, PlayerSelected }
    private AccusationState currentState = AccusationState.Default;

    // Per-slot cached data (indexed parallel to playerButtons[])
    private int[]    buttonPlayerIds;          // actual Player.id, or -1 if slot unused
    private Color[]  originalBorderColors;
    private Color[]  originalBackgroundColors;
    private Color[]  originalTextColors;
    private Sprite[] originalIcons;
    private int[]    originalSiblingIndices;   // sibling indices recorded at init time

    /// <summary>Index into playerButtons[] of the currently selected (accusing) player, or -1.</summary>
    private int selectedButtonIndex = -1;

    private Coroutine moveCoroutine;

    // ===================================================================
    //  Unity Lifecycle
    // ===================================================================

    private void Awake()
    {
        gameManager = GameManager.Instance;
    }

    private void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        InitializePanel();
    }

    // ===================================================================
    //  Initialization
    // ===================================================================

    private void InitializePanel()
    {
        if (gameManager == null) return;

        int maxButtons = playerButtons.Length;
        buttonPlayerIds          = new int[maxButtons];
        originalBorderColors     = new Color[maxButtons];
        originalBackgroundColors = new Color[maxButtons];
        originalTextColors       = new Color[maxButtons];
        originalIcons            = new Sprite[maxButtons];
        originalSiblingIndices   = new int[maxButtons];

        // Collect non-DM players sorted by ID
        int dmId = PlayerManager.dmId;
        var nonDmPlayers = new List<Player>();
        foreach (var player in PlayerManager.players.Values)
            if (player.id != dmId) nonDmPlayers.Add(player);
        nonDmPlayers.Sort((a, b) => a.id.CompareTo(b.id));

        for (int i = 0; i < maxButtons; i++)
        {
            var ui = playerButtons[i];
            if (ui == null || ui.button == null) continue;

            if (i < nonDmPlayers.Count)
            {
                Player p = nonDmPlayers[i];
                buttonPlayerIds[i] = p.id;

                // Populate name
                if (ui.nameText != null) ui.nameText.text = p.name;

                // Cache original visuals
                originalBorderColors[i]     = ui.borderImage     != null ? ui.borderImage.color      : Color.white;
                originalBackgroundColors[i] = ui.backgroundImage != null ? ui.backgroundImage.color  : Color.white;
                originalTextColors[i]       = ui.nameText        != null ? ui.nameText.color          : Color.black;
                originalIcons[i]            = ui.iconImage       != null ? ui.iconImage.sprite        : null;
                originalSiblingIndices[i]   = ui.button.transform.GetSiblingIndex();

                // Show and wire click listener (remove old to avoid duplicates)
                ui.button.gameObject.SetActive(true);
                ui.button.interactable = true;
                int capturedIndex = i;
                ui.button.onClick.RemoveAllListeners();
                ui.button.onClick.AddListener(() => OnPlayerButtonClicked(capturedIndex));
            }
            else
            {
                // No player for this slot — hide it
                buttonPlayerIds[i] = -1;
                ui.button.gameObject.SetActive(false);
            }
        }

        // Setup Incorrect button
        if (incorrectButton != null)
        {
            incorrectButton.gameObject.SetActive(false);
            incorrectButton.onClick.RemoveAllListeners();
            incorrectButton.onClick.AddListener(OnIncorrectButtonClicked);
        }

        if (descriptionText != null) descriptionText.text = defaultDescription;

        currentState        = AccusationState.Default;
        selectedButtonIndex = -1;
    }

    // ===================================================================
    //  Button Callbacks
    // ===================================================================

    private void OnPlayerButtonClicked(int buttonIndex)
    {
        if (buttonPlayerIds == null || buttonPlayerIds[buttonIndex] < 0) return;

        switch (currentState)
        {
            case AccusationState.Default:
                EnterPlayerSelected(buttonIndex);
                break;

            case AccusationState.PlayerSelected:
                if (buttonIndex == selectedButtonIndex)
                    CancelAccusation();           // Tap selected player again → cancel
                else
                    ResolveCorrectAccusation(selectedButtonIndex, buttonIndex); // Red button → correct accusation
                break;
        }
    }

    private void OnIncorrectButtonClicked()
    {
        if (currentState != AccusationState.PlayerSelected) return;
        ResolveIncorrectAccusation(selectedButtonIndex);
    }

    // ===================================================================
    //  State Transitions
    // ===================================================================

    private void EnterPlayerSelected(int accusingButtonIndex)
    {
        currentState        = AccusationState.PlayerSelected;
        selectedButtonIndex = accusingButtonIndex;

        // Update description
        if (descriptionText != null) descriptionText.text = playerSelectedDescription;

        // Smoothly move the selected button to sibling index 1 (between Header and Description)
        ScheduleMove(accusingButtonIndex, targetSiblingIndex: 1);

        // Style all other visible buttons: red + point icon, disable those without a SecretObjective
        for (int i = 0; i < playerButtons.Length; i++)
        {
            if (i == accusingButtonIndex)   continue; // Leave the selected button alone
            if (buttonPlayerIds[i] < 0)     continue; // Unused slot

            var ui = playerButtons[i];
            if (!ui.button.gameObject.activeSelf) continue;

            // Apply accusation colour (border, background, text)
            SetButtonColor(ui, accusedColor, Color.white);

            // Swap icon to point icon
            if (ui.iconImage != null && pointIcon != null)
                ui.iconImage.sprite = pointIcon;

            // Players without an active SecretObjective cannot be correctly accused —
            // disable their button so the DM cannot select them as a valid target.
            int pid = buttonPlayerIds[i];
            bool hasSecretObjective = PlayerManager.players.ContainsKey(pid)
                                   && PlayerManager.players[pid].secretObjectiveId >= 0;
            ui.button.interactable = hasSecretObjective;
        }

        // Reveal the Incorrect button
        if (incorrectButton != null) incorrectButton.gameObject.SetActive(true);
    }

    private void CancelAccusation()
    {
        // Move selected button back to its original position before returning to Default
        ScheduleMove(selectedButtonIndex, originalSiblingIndices[selectedButtonIndex]);

        FinishAndReturnToDefault();
    }

    // ===================================================================
    //  Accusation Resolution
    // ===================================================================

    /// <summary>
    /// The accusing player correctly identified the accused player's Secret Objective target.
    /// They steal that objective's points.
    /// </summary>
    private void ResolveCorrectAccusation(int accusingButtonIndex, int accusedButtonIndex)
    {
        int accusingPlayerId = buttonPlayerIds[accusingButtonIndex];
        int accusedPlayerId  = buttonPlayerIds[accusedButtonIndex];

        // ----------------------------------------------------------------
        //  POINT CALCULATION — Correct Accusation
        //  ▶ Currently: accusing player steals ALL SecretObjective.points
        //    from the accused player.
        //
        //  TO ADJUST, change `stolenPoints` here. Options:
        //    All points:   stolenPoints = accusedObjective.points          ← current
        //    Half points:  stolenPoints = accusedObjective.points / 2
        //    Flat reward:  stolenPoints = flatAccusationReward             (add SerializeField)
        // ----------------------------------------------------------------
        SecretObjective accusedObjective = SecretObjectiveManager.GetSecretObjectiveByPlayerId(accusedPlayerId);
        int stolenPoints = accusedObjective != null ? accusedObjective.points : 0;

        // Deduct from accused player's score
        PlayerManager.SubtractScore(accusedPlayerId, stolenPoints);

        // Add stolen points to accusing player (tracked separately as stolenScore, also added to regular score)
        PlayerManager.AddStolenScore(accusingPlayerId, stolenPoints);

        Debug.Log($"[Accusation] {PlayerManager.players[accusingPlayerId].name} correctly accused " +
                  $"{PlayerManager.players[accusedPlayerId].name} and stole {stolenPoints} point(s). " +
                  $"Their stolenScore is now {PlayerManager.players[accusingPlayerId].stolenScore}.");

        // Move selected button back before returning to default
        ScheduleMove(selectedButtonIndex, originalSiblingIndices[selectedButtonIndex]);
        FinishAndReturnToDefault();
    }

    /// <summary>
    /// The accusing player made a wrong guess. They lose <see cref="incorrectPenalty"/> points.
    /// </summary>
    private void ResolveIncorrectAccusation(int accusingButtonIndex)
    {
        int accusingPlayerId = buttonPlayerIds[accusingButtonIndex];

        // ----------------------------------------------------------------
        //  POINT CALCULATION — Incorrect Accusation
        //  ▶ Currently: accusing player loses `incorrectPenalty` points (default 20).
        //    Adjust the "Incorrect Penalty" SerializeField in the Inspector.
        // ----------------------------------------------------------------
        PlayerManager.SubtractScore(accusingPlayerId, incorrectPenalty);

        Debug.Log($"[Accusation] {PlayerManager.players[accusingPlayerId].name} made an incorrect accusation " +
                  $"and lost {incorrectPenalty} point(s). Score is now {PlayerManager.players[accusingPlayerId].score}.");

        // Move selected button back before returning to default
        ScheduleMove(selectedButtonIndex, originalSiblingIndices[selectedButtonIndex]);
        FinishAndReturnToDefault();
    }

    // ===================================================================
    //  Shared Cleanup
    // ===================================================================

    /// <summary>Restores all button visuals and resets controller state to Default.</summary>
    private void FinishAndReturnToDefault()
    {
        currentState = AccusationState.Default;

        if (descriptionText != null) descriptionText.text = defaultDescription;

        RestoreAllButtons();

        if (incorrectButton != null) incorrectButton.gameObject.SetActive(false);

        selectedButtonIndex = -1;
    }

    private void RestoreAllButtons()
    {
        for (int i = 0; i < playerButtons.Length; i++)
        {
            if (buttonPlayerIds[i] < 0) continue;

            var ui = playerButtons[i];
            if (!ui.button.gameObject.activeSelf) continue;

            // Restore colours
            if (ui.borderImage     != null) ui.borderImage.color     = originalBorderColors[i];
            if (ui.backgroundImage != null) ui.backgroundImage.color = originalBackgroundColors[i];
            if (ui.nameText        != null) ui.nameText.color        = originalTextColors[i];

            // Restore icon
            if (ui.iconImage != null) ui.iconImage.sprite = originalIcons[i];

            // Re-enable interactivity
            ui.button.interactable = true;
        }
    }

    // ===================================================================
    //  Button Color Helper
    // ===================================================================

    private void SetButtonColor(PlayerButtonUI ui, Color bgAndBorderColor, Color textColor)
    {
        if (ui.borderImage     != null) ui.borderImage.color     = bgAndBorderColor;
        if (ui.backgroundImage != null) ui.backgroundImage.color = bgAndBorderColor;
        if (ui.nameText        != null) ui.nameText.color        = textColor;
    }

    // ===================================================================
    //  Smooth Button Movement
    // ===================================================================

    private void ScheduleMove(int buttonIndex, int targetSiblingIndex)
    {
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        var buttonRect = playerButtons[buttonIndex].button.transform as RectTransform;
        moveCoroutine = StartCoroutine(MoveButtonSmoothly(buttonRect, targetSiblingIndex, moveDuration));
    }

    /// <summary>
    /// Smoothly slides <paramref name="buttonRect"/> to the given sibling index within its
    /// parent VerticalLayoutGroup by temporarily opting it out of the layout,
    /// measuring the destination, then lerping the world position before re-enabling.
    /// </summary>
    private IEnumerator MoveButtonSmoothly(RectTransform buttonRect, int targetSiblingIndex, float duration)
    {
        if (buttonRect == null) yield break;

        RectTransform parent = buttonRect.parent as RectTransform;
        if (parent == null) yield break;

        // Get or add a LayoutElement so we can temporarily opt this button out of layout control
        LayoutElement le = buttonRect.GetComponent<LayoutElement>();
        if (le == null) le = buttonRect.gameObject.AddComponent<LayoutElement>();

        // Step 1 — record starting world position
        Vector3 startWorld = buttonRect.position;

        // Step 2 — opt out of layout and force rebuild so other buttons fill the gap
        le.ignoreLayout = true;
        LayoutRebuilder.ForceRebuildLayoutImmediate(parent);

        // Step 3 — move to target sibling and briefly re-enable layout to measure landing position
        buttonRect.SetSiblingIndex(targetSiblingIndex);
        le.ignoreLayout = false;
        LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        Vector3 endWorld = buttonRect.position;

        // Step 4 — opt back out and hold at start position, then lerp
        le.ignoreLayout = true;
        buttonRect.position = startWorld;
        LayoutRebuilder.ForceRebuildLayoutImmediate(parent);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            buttonRect.position = Vector3.Lerp(startWorld, endWorld, t);
            yield return null;
        }

        // Step 5 — snap to final position and hand control back to the layout
        buttonRect.position = endWorld;
        le.ignoreLayout = false;
        LayoutRebuilder.ForceRebuildLayoutImmediate(parent);

        moveCoroutine = null;
    }
}

