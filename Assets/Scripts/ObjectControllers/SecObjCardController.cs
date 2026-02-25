using GLTFast.Schema;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SecObjCardController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;
    private SecretObjectiveManager SecretObjectiveManager => gameManager.secretObjectiveManager;
    private SecretObjective objective;
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
    [SerializeField] private Sprite speechCheckbox;
    [SerializeField] private Sprite interruptionCheckbox;
    [SerializeField] private Sprite betrayalCheckbox;
    

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void Initialize(int playerId)
    {
        if (gameManager == null) gameManager = GameManager.Instance;

        player = PlayerManager.players[playerId];
        objective = SecretObjectiveManager.GetSecretObjectiveByPlayerId(player.id);
        if (objective == null)
        {
            Debug.LogError($"No secret objective found for player ID {playerId}");
            return;
        }

        SetValues();
        SetSprites();
    }

    private void SetValues()
    {
        typeText.text = objective.type.ToString();
        descriptionText.text = objective.description;
        pointsText.text = $"{objective.points} Points";
        nameText.text = player.name;
    }

    private void SetSprites()
    {
        switch (objective.type)
        {
            case GameManager.SecretObjectiveType.Speech:
                GetComponent<UnityEngine.UI.Image>().sprite = speechFrame;
                completedToggle.image.sprite = speechCheckbox;
                break;
            case GameManager.SecretObjectiveType.Interruption:
                GetComponent<UnityEngine.UI.Image>().sprite = interruptionFrame;
                completedToggle.image.sprite = interruptionCheckbox;
                break;
            case GameManager.SecretObjectiveType.Betrayal:
                GetComponent<UnityEngine.UI.Image>().sprite = betrayalFrame;
                completedToggle.image.sprite = betrayalCheckbox;
                break;
        }
    }

    public void ToggleComplete()
    {
        objective.completeted = !objective.completeted;
    }
}
