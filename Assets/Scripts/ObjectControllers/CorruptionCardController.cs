using GLTFast.Schema;
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

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    void Update()
    {
        if (player == null || completedToggle == null || !player.isAccused) return;
        if (!completedToggle.interactable) return;

        completedToggle.interactable = false;

        // If the toggle was already checked when the accusation landed, undo the completion
        // so the data model matches — the accusation's SubtractScore already handled the points.
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

        if (descriptionText != null) descriptionText.text = objective.description;
        else Debug.LogError("CorruptionCardController: descriptionText is not assigned in prefab!");

        if (pointsText != null) pointsText.text = $"{objective.points} Points";
        else Debug.LogError("CorruptionCardController: pointsText is not assigned in prefab!");

        if (nameText != null) nameText.text = player.name;
        else Debug.LogError("CorruptionCardController: nameText is not assigned in prefab!");
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
                break;
            case GameManager.CorruptionType.Interruption:
                if (interruptionFrame != null) img.sprite = interruptionFrame;
                else Debug.LogWarning("CorruptionCardController: interruptionFrame sprite not assigned in prefab!");
                break;
            case GameManager.CorruptionType.Betrayal:
                if (betrayalFrame != null) img.sprite = betrayalFrame;
                else Debug.LogWarning("CorruptionCardController: betrayalFrame sprite not assigned in prefab!");
                break;
        }
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
            player.score += objective.points;
        }
        else if (!shouldBeCompleted && objective.completeted)
        {
            objective.completeted = false;
            player.score -= objective.points;
        }
    }
}
