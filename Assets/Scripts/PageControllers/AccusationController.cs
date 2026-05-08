using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Accusation sub-panel on the DMDisplay page (left slide). A non-DM player taps their
/// own button to declare they're accusing someone, then taps a second player's button to
/// finger them — or taps "Incorrect" to admit they got it wrong.
///
/// Resolution flow:
///   • Default → <see cref="EnterPlayerSelected"/>: dim other buttons red and reveal the
///     "Incorrect" button. Buttons for players without an active corruption are disabled
///     since they can't be a valid accusation target.
///   • Correct accusation → <see cref="ResolveCorrectAccusation"/>: stolen points equal
///     the accused's <c>Corruption.points</c>. Accuser gets +stolen via
///     <see cref="PlayerManager.AddStolenScore"/>; accused loses the same via
///     <see cref="PlayerManager.SubtractScore"/> (NOT <c>SubtractRoundCorruptionScore</c> —
///     the gross "Corruption" bar on the Scoreboard intentionally still shows what they
///     completed, so the loss only renders as a deduction during the animation phase).
///     <see cref="PlayerManager.SetPlayerAccused"/> then prevents further toggle awards.
///   • Incorrect accusation → <see cref="ResolveIncorrectAccusation"/>: accuser pays
///     <see cref="incorrectPenalty"/> (default 20) via
///     <see cref="PlayerManager.AddPenaltyScore"/>.
///
/// Each non-DM player can accuse exactly once per round (<c>Player.hasAccused</c>).
///
/// Attach to the root of the VerticalLayoutGroup that contains:
///   [0] Header (TMP)
///   [1] Default description (TMP, localized)        — visible while in <see cref="AccusationState.Default"/>
///   [2] Player-selected description (TMP, localized) — visible while in <see cref="AccusationState.PlayerSelected"/>
///   [3-10] Button (Player1-Player8)
///   [11] Button (Incorrect)  — hidden by default via CanvasGroup alpha
///
/// The two description text boxes each own their own LocalizeStringEvent — instead of swapping
/// strings on a single text box (which fights the localizer), we toggle which GameObject is active.
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
    [Tooltip("Description shown in the Default state (\"Click on the player who is accusing\"). " +
             "Owns its own LocalizeStringEvent — toggled active/inactive instead of having its text overwritten.")]
    [SerializeField] private GameObject defaultDescription;
    [Tooltip("Description shown in the PlayerSelected state (\"Someone is Accusing!…\"). " +
             "Owns its own LocalizeStringEvent — toggled active/inactive instead of having its text overwritten.")]
    [SerializeField] private GameObject playerSelectedDescription;
    [SerializeField] private Button          incorrectButton;
    /// <summary>Cached CanvasGroup on incorrectButton — used to show/hide without SetActive (which dirties the layout).</summary>
    private CanvasGroup incorrectButtonGroup;

    [Header("Player Buttons (assign Player1 → Player8 in order)")]
    [SerializeField] private PlayerButtonUI[] playerButtons = new PlayerButtonUI[8];

    [Header("Visuals")]
    [Tooltip("Icon displayed on non-selected player buttons while a player is selected.")]
    [SerializeField] private Sprite pointIcon;

    [Header("Accusation Settings")]
    [Tooltip("Points deducted from the accusing player on an incorrect accusation.")]
    [SerializeField] private int incorrectPenalty = 20;

    // ===================================================================
    //  Private State
    // ===================================================================

    private GameManager            gameManager;
    private PlayerManager          PlayerManager          => gameManager.playerManager;
    private CorruptionManager CorruptionManager => gameManager.corruptionManager;

    private enum AccusationState { Default, PlayerSelected }
    private AccusationState currentState = AccusationState.Default;

    // Per-slot cached data (indexed parallel to playerButtons[])
    private int[]    buttonPlayerIds; // actual Player.id, or -1 if slot unused
    private Color[]  originalTextColors;
    private Sprite[] originalIcons;
    /// <summary>Index into playerButtons[] of the currently selected (accusing) player, or -1.</summary>
    private int selectedButtonIndex = -1;

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

        int maxButtons    = playerButtons.Length;
        buttonPlayerIds   = new int[maxButtons];
        originalTextColors = new Color[maxButtons];
        originalIcons      = new Sprite[maxButtons];

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
                originalTextColors[i] = ui.nameText  != null ? ui.nameText.color  : Color.black;
                originalIcons[i]      = ui.iconImage  != null ? ui.iconImage.sprite : null;

                // Show and wire click listener (remove old to avoid duplicates)
                ui.button.gameObject.SetActive(true);
                ui.button.interactable = !p.hasAccused;
                GetAnimator(ui).SetBool("InCrossfire", false);
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
            // Get or add a CanvasGroup so we can hide without SetActive (SetActive dirties the layout
            // and causes the parent HorizontalLayoutGroup to reset/jerk).
            incorrectButtonGroup = incorrectButton.GetComponent<CanvasGroup>();
            if (incorrectButtonGroup == null)
                incorrectButtonGroup = incorrectButton.gameObject.AddComponent<CanvasGroup>();

            incorrectButton.onClick.RemoveAllListeners();
            incorrectButton.onClick.AddListener(OnIncorrectButtonClicked);
            HideIncorrectButton();
        }

        ShowDefaultDescription();

        currentState        = AccusationState.Default;
        selectedButtonIndex = -1;
    }

    /// <summary>Activates the Default-state description and hides the PlayerSelected one.</summary>
    private void ShowDefaultDescription()
    {
        if (defaultDescription        != null) defaultDescription.SetActive(true);
        if (playerSelectedDescription != null) playerSelectedDescription.SetActive(false);
    }

    /// <summary>Activates the PlayerSelected-state description and hides the Default one.</summary>
    private void ShowPlayerSelectedDescription()
    {
        if (defaultDescription        != null) defaultDescription.SetActive(false);
        if (playerSelectedDescription != null) playerSelectedDescription.SetActive(true);
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

        ShowPlayerSelectedDescription();

        // Style all other visible buttons: red + point icon, disable those without a corruption
        for (int i = 0; i < playerButtons.Length; i++)
        {
            if (i == accusingButtonIndex)   continue; // Leave the selected button alone
            if (buttonPlayerIds[i] < 0)     continue; // Unused slot

            var ui = playerButtons[i];
            if (!ui.button.gameObject.activeSelf) continue;

            // Drive red color via Crossfire animator layer
            GetAnimator(ui).SetBool("InCrossfire", true);

            // Swap icon to point icon
            if (ui.iconImage != null && pointIcon != null)
                ui.iconImage.sprite = pointIcon;

            // Players without an active corruption cannot be correctly accused —
            // disable their button so the DM cannot select them as a valid target.
            int pid = buttonPlayerIds[i];
            bool hasCorruption = PlayerManager.players.ContainsKey(pid)
                              && PlayerManager.players[pid].corruptionId >= 0;
            ui.button.interactable = hasCorruption;
        }

        // Reveal the Incorrect button
        ShowIncorrectButton();
    }

    private void CancelAccusation()
    {
        FinishAndReturnToDefault();
    }

    // ===================================================================
    //  Accusation Resolution
    // ===================================================================

    /// <summary>
    /// The accusing player correctly identified the accused player's corruption.
    /// They steal that corruption's points.
    /// </summary>
    private void ResolveCorrectAccusation(int accusingButtonIndex, int accusedButtonIndex)
    {
        int accusingPlayerId = buttonPlayerIds[accusingButtonIndex];
        int accusedPlayerId  = buttonPlayerIds[accusedButtonIndex];

        // ----------------------------------------------------------------
        //  POINT CALCULATION — Correct Accusation
        //  ▶ Currently: accusing player steals ALL Corruption.points
        //    from the accused player.
        //
        //  TO ADJUST, change `stolenPoints` here. Options:
        //    All points:   stolenPoints = accusedObjective.points          ← current
        //    Half points:  stolenPoints = accusedObjective.points / 2
        //    Flat reward:  stolenPoints = flatAccusationReward             (add SerializeField)
        // ----------------------------------------------------------------
        Corruption accusedObjective = CorruptionManager.GetCorruptionByPlayerId(accusedPlayerId);
        int stolenPoints = accusedObjective != null ? accusedObjective.points : 0;

        // Deduct from accused player's score
        PlayerManager.SubtractScore(accusedPlayerId, stolenPoints);

        // Add stolen points to accusing player (tracked separately as stolenScore, also added to regular score)
        PlayerManager.AddStolenScore(accusingPlayerId, stolenPoints);

        Debug.Log($"[Accusation] {PlayerManager.players[accusingPlayerId].name} correctly accused " +
                  $"{PlayerManager.players[accusedPlayerId].name} and stole {stolenPoints} point(s). " +
                  $"Their stolenScore is now {PlayerManager.players[accusingPlayerId].stolenScore}.");

        PlayerManager.players[accusingPlayerId].hasAccused = true;
        PlayerManager.SetPlayerAccused(accusedPlayerId);
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
        PlayerManager.AddPenaltyScore(accusingPlayerId, incorrectPenalty);

        Debug.Log($"[Accusation] {PlayerManager.players[accusingPlayerId].name} made an incorrect accusation " +
                  $"and lost {incorrectPenalty} point(s). Score is now {PlayerManager.players[accusingPlayerId].score}.");

        PlayerManager.players[accusingPlayerId].hasAccused = true;
        FinishAndReturnToDefault();
    }

    // ===================================================================
    //  Shared Cleanup
    // ===================================================================

    /// <summary>Restores all button visuals and resets controller state to Default.</summary>
    private void FinishAndReturnToDefault()
    {
        currentState = AccusationState.Default;

        ShowDefaultDescription();

        RestoreAllButtons();

        HideIncorrectButton();

        selectedButtonIndex = -1;
    }

    private void RestoreAllButtons()
    {
        for (int i = 0; i < playerButtons.Length; i++)
        {
            if (buttonPlayerIds[i] < 0) continue;

            var ui = playerButtons[i];
            if (!ui.button.gameObject.activeSelf) continue;

            // Restore color via Crossfire animator layer
            GetAnimator(ui).SetBool("InCrossfire", false);

            // Restore text color and icon
            if (ui.nameText  != null) ui.nameText.color    = originalTextColors[i];
            if (ui.iconImage != null) ui.iconImage.sprite  = originalIcons[i];

            // Re-enable interactivity only if this player hasn't used their accusation this round
            int pid = buttonPlayerIds[i];
            bool canStillAccuse = PlayerManager.players.ContainsKey(pid) && !PlayerManager.players[pid].hasAccused;
            ui.button.interactable = canStillAccuse;
        }
    }

    // ===================================================================
    //  Animator Helper
    // ===================================================================

    private Animator GetAnimator(PlayerButtonUI ui) => ui.button.GetComponent<Animator>();

    // ===================================================================
    //  Incorrect Button Visibility
    //  Using CanvasGroup instead of SetActive so the button stays in the
    //  VerticalLayoutGroup and never dirties the parent HorizontalLayoutGroup.
    // ===================================================================

    private void ShowIncorrectButton()
    {
        if (incorrectButtonGroup == null) return;
        incorrectButtonGroup.alpha          = 1f;
        incorrectButtonGroup.interactable   = true;
        incorrectButtonGroup.blocksRaycasts = true;
    }

    private void HideIncorrectButton()
    {
        if (incorrectButtonGroup == null) return;
        incorrectButtonGroup.alpha          = 0f;
        incorrectButtonGroup.interactable   = false;
        incorrectButtonGroup.blocksRaycasts = false;
    }

}

