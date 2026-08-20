using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Accusation sub-panel on the DMDisplay page. Three-step flow: the DM selects the
/// accusing player's button, then the button of the player being accused, and finally
/// resolves the accusation with the Correct or Incorrect button. Each non-DM player
/// may accuse once per round, gated by <c>Player.hasAccused</c>. A correct accusation
/// transfers points equal to the accused player's <c>Corruption.points</c> via
/// <see cref="PlayerManager.AddStolenScore"/>; an incorrect accusation costs
/// <see cref="incorrectPenalty"/> points via <see cref="PlayerManager.AddPenaltyScore"/>.
/// The default description owns a LocalizeStringEvent; the player-selected description
/// is written from code because it embeds player names.
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
    [Tooltip("Description shown in the PlayerSelected/TargetSelected states. Text is written from code (embeds player names).")]
    [SerializeField] private GameObject playerSelectedDescription;
    [SerializeField] private Button          incorrectButton;
    [SerializeField] private Button          correctButton;
    /// <summary>Cached CanvasGroups on the accept buttons. Used to hide the buttons without SetActive, which would dirty the layout.</summary>
    private CanvasGroup incorrectButtonGroup;
    private CanvasGroup correctButtonGroup;
    /// <summary>Cached TMP text on playerSelectedDescription.</summary>
    private TextMeshProUGUI playerSelectedText;

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

    private enum AccusationState { Default, PlayerSelected, TargetSelected }
    private AccusationState currentState = AccusationState.Default;

    private int[]    buttonPlayerIds;
    private Color[]  originalTextColors;
    private Sprite[] originalIcons;
    /// <summary>Index into playerButtons of the currently selected accusing player, or -1.</summary>
    private int selectedButtonIndex = -1;
    /// <summary>Index into playerButtons of the player being accused, or -1.</summary>
    private int targetButtonIndex = -1;

    private void Awake()
    {
        gameManager = GameManager.Instance;
    }

    private void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        GameManager.OnLanguageChanged += UpdateStateDescription;
        InitializePanel();
    }

    private void OnDisable()
    {
        GameManager.OnLanguageChanged -= UpdateStateDescription;
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
        }

        if (correctButton != null)
        {
            correctButtonGroup = correctButton.GetComponent<CanvasGroup>();
            if (correctButtonGroup == null)
                correctButtonGroup = correctButton.gameObject.AddComponent<CanvasGroup>();

            correctButton.onClick.RemoveAllListeners();
            correctButton.onClick.AddListener(OnCorrectButtonClicked);
        }

        HideAcceptanceButtons();

        if (playerSelectedDescription != null)
            playerSelectedText = playerSelectedDescription.GetComponent<TextMeshProUGUI>();

        currentState        = AccusationState.Default;
        selectedButtonIndex = -1;
        targetButtonIndex   = -1;

        UpdateStateDescription();
    }

    /// <summary>
    /// Shows the description matching the current state. The Default state uses the
    /// localized static description; the other states write the text here because it
    /// embeds player names.
    /// </summary>
    private void UpdateStateDescription()
    {
        bool isDefault = currentState == AccusationState.Default;
        if (defaultDescription        != null) defaultDescription.SetActive(isDefault);
        if (playerSelectedDescription != null) playerSelectedDescription.SetActive(!isDefault);

        if (isDefault || playerSelectedText == null || selectedButtonIndex < 0) return;

        bool isIcelandic = gameManager != null
            && gameManager.selectedLanguage == GameManager.Language.Icelandic;
        string accuserName = PlayerManager.players[buttonPlayerIds[selectedButtonIndex]].name;

        if (currentState == AccusationState.PlayerSelected)
        {
            playerSelectedText.text = isIcelandic
                ? $"Smelltu á leikmanninn sem {accuserName} er að ásaka!"
                : $"Click on the player that {accuserName} is accusing!";
        }
        else
        {
            string targetName = PlayerManager.players[buttonPlayerIds[targetButtonIndex]].name;
            playerSelectedText.text = isIcelandic
                ? $"{accuserName} ásakar {targetName}!\nVar ágiskunin rétt?"
                : $"{accuserName} is accusing {targetName}!\nWas the guess correct?";
        }
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
                    EnterTargetSelected(buttonIndex);
                break;

            case AccusationState.TargetSelected:
                if (buttonIndex == selectedButtonIndex)
                    CancelAccusation();
                else if (buttonIndex == targetButtonIndex)
                    DeselectTarget();
                else
                    EnterTargetSelected(buttonIndex);
                break;
        }
    }

    private void OnCorrectButtonClicked()
    {
        if (currentState != AccusationState.TargetSelected) return;
        ResolveCorrectAccusation(selectedButtonIndex, targetButtonIndex);
    }

    private void OnIncorrectButtonClicked()
    {
        if (currentState != AccusationState.TargetSelected) return;
        ResolveIncorrectAccusation(selectedButtonIndex);
    }

    private void EnterPlayerSelected(int accusingButtonIndex)
    {
        currentState        = AccusationState.PlayerSelected;
        selectedButtonIndex = accusingButtonIndex;

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

            // Every other player is a valid target — accusing someone without a
            // corruption is simply an incorrect accusation, resolved by the buttons.
            ui.button.interactable = true;
        }

        UpdateStateDescription();
    }

    /// <summary>
    /// Marks a player as the one being accused: only their button keeps the crossfire
    /// tint, and the Correct/Incorrect buttons appear. Correct is disabled when the
    /// target has no active corruption — such an accusation can only be incorrect.
    /// </summary>
    private void EnterTargetSelected(int accusedButtonIndex)
    {
        currentState      = AccusationState.TargetSelected;
        targetButtonIndex = accusedButtonIndex;

        for (int i = 0; i < playerButtons.Length; i++)
        {
            if (i == selectedButtonIndex) continue;
            if (buttonPlayerIds[i] < 0)   continue;

            var ui = playerButtons[i];
            if (!ui.button.gameObject.activeSelf) continue;

            GetAnimator(ui).SetBool("InCrossfire", i == accusedButtonIndex);
        }

        UpdateStateDescription();
        ShowAcceptanceButtons();
    }

    /// <summary>Backs out of the target choice, returning to the PlayerSelected state.</summary>
    private void DeselectTarget()
    {
        currentState      = AccusationState.PlayerSelected;
        targetButtonIndex = -1;

        for (int i = 0; i < playerButtons.Length; i++)
        {
            if (i == selectedButtonIndex) continue;
            if (buttonPlayerIds[i] < 0)   continue;

            var ui = playerButtons[i];
            if (!ui.button.gameObject.activeSelf) continue;

            GetAnimator(ui).SetBool("InCrossfire", true);
        }

        UpdateStateDescription();
        HideAcceptanceButtons();
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
        currentState        = AccusationState.Default;
        selectedButtonIndex = -1;
        targetButtonIndex   = -1;

        UpdateStateDescription();

        RestoreAllButtons();

        HideAcceptanceButtons();
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

    // CanvasGroups are used instead of SetActive so the accept buttons stay in the
    // layout and never dirty the parent HorizontalLayoutGroup.

    /// <summary>
    /// Reveals both accept buttons. Correct is disabled (and dimmed) when the selected
    /// target has no active corruption, since that accusation can only be incorrect.
    /// </summary>
    private void ShowAcceptanceButtons()
    {
        SetButtonGroupVisible(incorrectButtonGroup, true);
        SetButtonGroupVisible(correctButtonGroup, true);

        bool targetHasCorruption = targetButtonIndex >= 0
            && PlayerManager.players.ContainsKey(buttonPlayerIds[targetButtonIndex])
            && PlayerManager.players[buttonPlayerIds[targetButtonIndex]].corruptionId >= 0;

        if (correctButton != null) correctButton.interactable = targetHasCorruption;
        if (correctButtonGroup != null && !targetHasCorruption)
            correctButtonGroup.alpha = 0.55f;
    }

    private void HideAcceptanceButtons()
    {
        SetButtonGroupVisible(incorrectButtonGroup, false);
        SetButtonGroupVisible(correctButtonGroup, false);
    }

    private static void SetButtonGroupVisible(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha          = visible ? 1f : 0f;
        group.interactable   = visible;
        group.blocksRaycasts = visible;
    }
}

