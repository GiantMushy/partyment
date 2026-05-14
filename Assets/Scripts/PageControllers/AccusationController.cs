using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Accusation sub-panel on the DMDisplay page. A non-DM player selects their own
/// button to declare an accusation, then either selects another player to accuse or
/// the Incorrect button to abort. Each non-DM player may accuse once per round, gated
/// by <c>Player.hasAccused</c>. A correct accusation transfers points equal to the
/// accused player's <c>Corruption.points</c> via <see cref="PlayerManager.AddStolenScore"/>;
/// an incorrect accusation costs <see cref="incorrectPenalty"/> points via
/// <see cref="PlayerManager.AddPenaltyScore"/>. The default and player-selected
/// descriptions each own a separate LocalizeStringEvent and are toggled active rather
/// than overwritten so localization stays consistent.
/// </summary>
public class AccusationController : MonoBehaviour
{
    [System.Serializable]
    public class PlayerButtonUI
    {
        public Button        button;
        public Image         borderImage;
        public Image         backgroundImage;
        public TextMeshProUGUI nameText;
        public Image         iconImage;
    }

    [Header("Layout Elements")]
    [SerializeField] private TextMeshProUGUI headerText;
    [Tooltip("Description shown in the Default state. Owns its own LocalizeStringEvent.")]
    [SerializeField] private GameObject defaultDescription;
    [Tooltip("Description shown in the PlayerSelected state. Owns its own LocalizeStringEvent.")]
    [SerializeField] private GameObject playerSelectedDescription;
    [SerializeField] private Button          incorrectButton;
    /// <summary>Cached CanvasGroup on incorrectButton. Used to hide the button without SetActive, which would dirty the layout.</summary>
    private CanvasGroup incorrectButtonGroup;

    [Header("Player Buttons (assign Player1 → Player8 in order)")]
    [SerializeField] private PlayerButtonUI[] playerButtons = new PlayerButtonUI[8];

    [Header("Visuals")]
    [Tooltip("Icon displayed on non-selected player buttons while a player is selected.")]
    [SerializeField] private Sprite pointIcon;

    [Header("Accusation Settings")]
    [Tooltip("Points deducted from the accusing player on an incorrect accusation.")]
    [SerializeField] private int incorrectPenalty = 20;

    private GameManager            gameManager;
    private PlayerManager          PlayerManager          => gameManager.playerManager;
    private CorruptionManager CorruptionManager => gameManager.corruptionManager;

    private enum AccusationState { Default, PlayerSelected }
    private AccusationState currentState = AccusationState.Default;

    private int[]    buttonPlayerIds;
    private Color[]  originalTextColors;
    private Sprite[] originalIcons;
    /// <summary>Index into playerButtons of the currently selected accusing player, or -1.</summary>
    private int selectedButtonIndex = -1;

    private void Awake()
    {
        gameManager = GameManager.Instance;
    }

    private void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        InitializePanel();
    }

    private void InitializePanel()
    {
        if (gameManager == null) return;

        int maxButtons    = playerButtons.Length;
        buttonPlayerIds   = new int[maxButtons];
        originalTextColors = new Color[maxButtons];
        originalIcons      = new Sprite[maxButtons];

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

                if (ui.nameText != null) ui.nameText.text = p.name;

                originalTextColors[i] = ui.nameText  != null ? ui.nameText.color  : Color.black;
                originalIcons[i]      = ui.iconImage  != null ? ui.iconImage.sprite : null;

                ui.button.gameObject.SetActive(true);
                ui.button.interactable = !p.hasAccused;
                GetAnimator(ui).SetBool("InCrossfire", false);
                int capturedIndex = i;
                ui.button.onClick.RemoveAllListeners();
                ui.button.onClick.AddListener(() => OnPlayerButtonClicked(capturedIndex));
            }
            else
            {
                buttonPlayerIds[i] = -1;
                ui.button.gameObject.SetActive(false);
            }
        }

        if (incorrectButton != null)
        {
            // CanvasGroup hides the button without SetActive, which would dirty the
            // parent HorizontalLayoutGroup.
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
                    CancelAccusation();
                else
                    ResolveCorrectAccusation(selectedButtonIndex, buttonIndex);
                break;
        }
    }

    private void OnIncorrectButtonClicked()
    {
        if (currentState != AccusationState.PlayerSelected) return;
        ResolveIncorrectAccusation(selectedButtonIndex);
    }

    private void EnterPlayerSelected(int accusingButtonIndex)
    {
        currentState        = AccusationState.PlayerSelected;
        selectedButtonIndex = accusingButtonIndex;

        ShowPlayerSelectedDescription();

        for (int i = 0; i < playerButtons.Length; i++)
        {
            if (i == accusingButtonIndex)   continue;
            if (buttonPlayerIds[i] < 0)     continue;

            var ui = playerButtons[i];
            if (!ui.button.gameObject.activeSelf) continue;

            // Drives the red tint via the Crossfire animator layer.
            GetAnimator(ui).SetBool("InCrossfire", true);

            if (ui.iconImage != null && pointIcon != null)
                ui.iconImage.sprite = pointIcon;

            // Players without an active corruption cannot be correctly accused, so
            // their button is disabled as a target.
            int pid = buttonPlayerIds[i];
            bool hasCorruption = PlayerManager.players.ContainsKey(pid)
                              && PlayerManager.players[pid].corruptionId >= 0;
            ui.button.interactable = hasCorruption;
        }

        ShowIncorrectButton();
    }

    private void CancelAccusation()
    {
        FinishAndReturnToDefault();
    }

    /// <summary>
    /// Resolves a correct accusation. The accusing player steals the accused player's
    /// corruption points; if the accused already toggled their corruption complete,
    /// those points are reversed on the accused.
    /// </summary>
    private void ResolveCorrectAccusation(int accusingButtonIndex, int accusedButtonIndex)
    {
        int accusingPlayerId = buttonPlayerIds[accusingButtonIndex];
        int accusedPlayerId  = buttonPlayerIds[accusedButtonIndex];

        Corruption accusedObjective = CorruptionManager.GetCorruptionByPlayerId(accusedPlayerId);
        int stolenPoints = accusedObjective != null ? accusedObjective.points : 0;

        if (accusedObjective != null && accusedObjective.completeted)
        {
            PlayerManager.SubtractRoundCorruptionScore(accusedPlayerId, stolenPoints);
            accusedObjective.completeted = false;
        }

        PlayerManager.AddStolenScore(accusingPlayerId, stolenPoints);

        Debug.Log($"[Accusation] {PlayerManager.players[accusingPlayerId].name} correctly accused " +
                  $"{PlayerManager.players[accusedPlayerId].name} and stole {stolenPoints} point(s). " +
                  $"Their stolenScore is now {PlayerManager.players[accusingPlayerId].stolenScore}.");

        PlayerManager.players[accusingPlayerId].hasAccused = true;
        PlayerManager.SetPlayerAccused(accusedPlayerId);
        FinishAndReturnToDefault();
    }

    /// <summary>
    /// Resolves an incorrect accusation. The accusing player loses
    /// <see cref="incorrectPenalty"/> points.
    /// </summary>
    private void ResolveIncorrectAccusation(int accusingButtonIndex)
    {
        int accusingPlayerId = buttonPlayerIds[accusingButtonIndex];

        PlayerManager.AddPenaltyScore(accusingPlayerId, incorrectPenalty);

        Debug.Log($"[Accusation] {PlayerManager.players[accusingPlayerId].name} made an incorrect accusation " +
                  $"and lost {incorrectPenalty} point(s). Score is now {PlayerManager.players[accusingPlayerId].score}.");

        PlayerManager.players[accusingPlayerId].hasAccused = true;
        FinishAndReturnToDefault();
    }

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

            GetAnimator(ui).SetBool("InCrossfire", false);

            if (ui.nameText  != null) ui.nameText.color    = originalTextColors[i];
            if (ui.iconImage != null) ui.iconImage.sprite  = originalIcons[i];

            int pid = buttonPlayerIds[i];
            bool canStillAccuse = PlayerManager.players.ContainsKey(pid) && !PlayerManager.players[pid].hasAccused;
            ui.button.interactable = canStillAccuse;
        }
    }

    private Animator GetAnimator(PlayerButtonUI ui) => ui.button.GetComponent<Animator>();

    // CanvasGroup is used instead of SetActive so the Incorrect button stays in the
    // VerticalLayoutGroup and never dirties the parent HorizontalLayoutGroup.

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

