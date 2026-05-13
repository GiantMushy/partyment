using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CorruptionCardController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;
    private CorruptionManager CorruptionManager => gameManager.corruptionManager;
    private Corruption objective;
    private Player player;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI typeText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI pointsText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Toggle completedToggle;

    [Header("Type specific Sprites")]
    [SerializeField] private Sprite speechFrame;
    [SerializeField] private Sprite interruptionFrame;
    [SerializeField] private Sprite betrayalFrame;

    [Header("Toggle Container Colors")]
    [SerializeField] private Image toggleBorder;
    [SerializeField] private Image toggleBackground;
    [SerializeField] private Image checkmark;

    [SerializeField] private Color speechBorderColor = new Color(0.003921569f, 0.5803922f, 0.023529412f, 1f);
    [SerializeField] private Color speechBackgroundColor = new Color(0.3764706f, 0.9843137f, 0.5529412f, 1f);
    [SerializeField] private Color interruptionBorderColor = new Color(0.23921569f, 0f, 0.65098039f, 1f);
    [SerializeField] private Color interruptionBackgroundColor = new Color(0.5333333f, 0.32549020f, 0.93725490f, 1f);

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    void OnEnable()
    {
        GameManager.OnLanguageChanged += OnLanguageChanged;
    }

    void OnDisable()
    {
        GameManager.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        if (objective == null) return;
        if (descriptionText != null)
            descriptionText.text = GetLocalizedDescription();
    }

    private string GetLocalizedDescription()
    {
        if (gameManager != null && gameManager.selectedLanguage == GameManager.Language.Icelandic
            && !string.IsNullOrEmpty(objective.descriptionIs))
            return objective.descriptionIs;
        return objective.description;
    }

    void Update()
    {
        if (player == null || completedToggle == null || !player.isAccused) return;
        if (!completedToggle.interactable) return;

        completedToggle.interactable = false;

        // Sync the UI if the toggle was still visually on — the accusation handler already
        // reversed the score via SubtractRoundCorruptionScore and cleared objective.completeted.
        if (completedToggle.isOn)
        {
            completedToggle.SetIsOnWithoutNotify(false);
            if (objective != null) objective.completeted = false;
        }
    }

    public void Initialize(int playerId)
    {
        if (gameManager == null) gameManager = GameManager.Instance;

        player = PlayerManager.players[playerId];
        objective = CorruptionManager.GetCorruptionByPlayerId(player.id);
        if (objective == null)
        {
            Debug.LogError($"No corruption found for player ID {playerId}");
            return;
        }

        SetValues();
        SetSprites();

        // Reset toggle to unchecked without firing the event
        if (completedToggle != null)
        {
            completedToggle.SetIsOnWithoutNotify(false);
            completedToggle.onValueChanged.AddListener(_ => ToggleComplete());
        }
    }

    private void SetValues()
    {
        if (typeText != null) typeText.text = objective.type.ToString();
        else Debug.LogError("CorruptionCardController: typeText is not assigned in prefab!");

        if (descriptionText != null) descriptionText.text = GetLocalizedDescription();
        else Debug.LogError("CorruptionCardController: descriptionText is not assigned in prefab!");

        if (pointsText != null) pointsText.text = $"{objective.points}";
        else Debug.LogError("CorruptionCardController: pointsText is not assigned in prefab!");

        if (nameText != null) nameText.text = player.name;
        else Debug.LogError("CorruptionCardController: nameText is not assigned in prefab!");

        Color textColor = GetTextColorForType(objective.type);
        if (typeText != null)        typeText.color        = textColor;
        if (descriptionText != null) descriptionText.color = textColor;
        if (pointsText != null)      pointsText.color      = textColor;
        if (nameText != null)        nameText.color        = textColor;
    }

    private Color GetTextColorForType(GameManager.CorruptionType type)
    {
        return type switch
        {
            GameManager.CorruptionType.Interruption => new Color(0xF5 / 255f, 0xF5 / 255f, 0xF5 / 255f),
            GameManager.CorruptionType.Betrayal     => new Color(0xF5 / 255f, 0xF5 / 255f, 0xF5 / 255f),
            _                                       => new Color(0x28 / 255f, 0x28 / 255f, 0x28 / 255f),
        };
    }

    private void SetSprites()
    {
        var img = GetComponent<UnityEngine.UI.Image>();
        if (img == null) return;

        switch (objective.type)
        {
            case GameManager.CorruptionType.Speech:
                if (speechFrame != null) img.sprite = speechFrame;
                else Debug.LogWarning("CorruptionCardController: speechFrame sprite not assigned in prefab!");
                SetToggleColors(speechBorderColor, speechBackgroundColor);
                break;
            case GameManager.CorruptionType.Interruption:
                if (interruptionFrame != null) img.sprite = interruptionFrame;
                else Debug.LogWarning("CorruptionCardController: interruptionFrame sprite not assigned in prefab!");
                SetToggleColors(interruptionBorderColor, interruptionBackgroundColor);
                break;
            case GameManager.CorruptionType.Betrayal:
                if (betrayalFrame != null) img.sprite = betrayalFrame;
                else Debug.LogWarning("CorruptionCardController: betrayalFrame sprite not assigned in prefab!");
                break;
        }
    }

    private void SetToggleColors(Color borderColor, Color backgroundColor)
    {
        if (toggleBorder != null) toggleBorder.color = borderColor;
        if (toggleBackground != null) toggleBackground.color = backgroundColor;
        if (checkmark != null) checkmark.color = borderColor;
    }

    public void ToggleComplete()
    {
        if (objective == null || player == null) return;
        if (player.isAccused) return;

        // Sync with the toggle's actual state rather than blindly flipping
        bool shouldBeCompleted = completedToggle != null ? completedToggle.isOn : !objective.completeted;

        if (shouldBeCompleted && !objective.completeted)
        {
            objective.completeted = true;
            // Updates BOTH score and roundCorruptionScore so the Scoreboard's gross "Corruption" bar
            // can be displayed independently of stolen/penalty manipulations on `score`.
            PlayerManager.AddRoundCorruptionScore(player.id, objective.points);
        }
        else if (!shouldBeCompleted && objective.completeted)
        {
            objective.completeted = false;
            PlayerManager.SubtractRoundCorruptionScore(player.id, objective.points);
        }
    }
}
