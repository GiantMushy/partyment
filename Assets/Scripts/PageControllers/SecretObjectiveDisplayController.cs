using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class SecretObjectiveDisplayController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("References")]
    private GameManager gameManager;
    private SecretObjectiveManager SecretObjectiveManager => gameManager.secretObjectiveManager;
    private Player player;
    [SerializeField] private GameObject cardHidden;
    [SerializeField] private GameObject cardRevealed;
    [SerializeField] private Sprite speechDisplaySprite;
    [SerializeField] private Sprite civilianDisplaySprite;
    [SerializeField] private Sprite interruptionDisplaySprite;
    [SerializeField] private Sprite betrayalDisplaySprite;
    [SerializeField] private Sprite civilianIconSprite;
    [SerializeField] private Sprite spyIconSprite;
    [SerializeField] private GameObject nextButton;

    [Header("Card Children")]
    [SerializeField] private Image cardImage;       // Background image on the revealed side
    [SerializeField] private Image spyIconImage;            // cardRevealed > Spy Icon
    [SerializeField] private Image civilianIconImage;       // cardRevealed > Civilian Icon
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI pointsText;
    [SerializeField] private TextMeshProUGUI typeText;

    [Header("Spy Icon Colors")]
    [SerializeField] private Color spySpeechColor = Color.white;
    [SerializeField] private Color spyInterruptionColor = Color.white;
    [SerializeField] private Color spyBetrayalColor = Color.white;

    [Header("Flip Settings")]
    [SerializeField] private float flipDuration = 0.2f;

    [Header("Variables")]
    private GameManager.SecretObjectiveType objectiveType;
    private string secretObjectiveText;
    private int score;

    private bool isRevealed = false;
    private bool hasBeenRevealed = false;
    private bool scalesCaptured = false;
    private Coroutine flipCoroutine;

    private Vector3 cardHiddenOriginalScale;
    private Vector3 cardRevealedOriginalScale;

    void Awake()
    {
        CaptureOriginalScales();
    }

    /// <summary>
    /// Lazily captures the original card scales exactly once, before any code can modify them.
    /// Safe to call whether or not Awake has run.
    /// </summary>
    private void CaptureOriginalScales()
    {
        if (scalesCaptured) return;

        if (cardHidden != null)
            cardHiddenOriginalScale = cardHidden.GetComponent<RectTransform>().localScale;
        if (cardRevealed != null)
            cardRevealedOriginalScale = cardRevealed.GetComponent<RectTransform>().localScale;

        scalesCaptured = true;
    }

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        ShowHiddenSide();
        hasBeenRevealed = false;
        if (nextButton != null) nextButton.SetActive(false);
    }

    void OnDisable()
    {
        // Stop any in-progress flip so scales aren't left at zero
        if (flipCoroutine != null)
        {
            StopCoroutine(flipCoroutine);
            flipCoroutine = null;
        }
        ResetCardScale(cardHidden);
        ResetCardScale(cardRevealed);
        isRevealed = false;
    }

    // -------------------- Pointer Events (Hold to Reveal) --------------------

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isRevealed)
        {
            FlipTo(revealed: true);

            if (!hasBeenRevealed)
            {
                hasBeenRevealed = true;
                if (nextButton != null) nextButton.SetActive(true);
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isRevealed)
            FlipTo(revealed: false);
    }

    // -------------------- Flip Animation --------------------

    private void FlipTo(bool revealed)
    {
        if (flipCoroutine != null)
            StopCoroutine(flipCoroutine);
        flipCoroutine = StartCoroutine(FlipCoroutine(revealed));
    }

    private IEnumerator FlipCoroutine(bool toRevealed)
    {
        GameObject activeCard = toRevealed ? cardHidden : cardRevealed;
        GameObject incomingCard = toRevealed ? cardRevealed : cardHidden;

        Vector3 activeOrigScale = toRevealed ? cardHiddenOriginalScale : cardRevealedOriginalScale;
        Vector3 incomingOrigScale = toRevealed ? cardRevealedOriginalScale : cardHiddenOriginalScale;

        RectTransform activeRect = activeCard.GetComponent<RectTransform>();
        RectTransform incomingRect = incomingCard.GetComponent<RectTransform>();

        Vector3 scale = activeRect.localScale;
        float halfDuration = flipDuration * 0.5f;

        // Phase 1: Scale active card X from current to 0 (card turns sideways)
        float startX = scale.x;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            scale.x = Mathf.Lerp(startX, 0f, t);
            activeRect.localScale = scale;
            yield return null;
        }
        scale.x = 0f;
        activeRect.localScale = scale;

        // Swap cards at the midpoint
        activeCard.SetActive(false);
        incomingCard.SetActive(true);
        isRevealed = toRevealed;

        // Reset active card scale for next time
        activeRect.localScale = activeOrigScale;

        // Phase 2: Scale incoming card X from 0 to original (new face appears)
        scale = incomingOrigScale;
        scale.x = 0f;
        incomingRect.localScale = scale;
        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            scale.x = Mathf.Lerp(0f, incomingOrigScale.x, t);
            incomingRect.localScale = scale;
            yield return null;
        }
        incomingRect.localScale = incomingOrigScale;

        flipCoroutine = null;
    }

    // -------------------- Card Visibility --------------------

    private void ShowHiddenSide()
    {
        if (cardHidden != null) cardHidden.SetActive(true);
        if (cardRevealed != null) cardRevealed.SetActive(false);
    }

    private void ShowRevealedSide()
    {
        if (cardHidden != null) cardHidden.SetActive(false);
        if (cardRevealed != null) cardRevealed.SetActive(true);
    }

    // -------------------- Player & Card Setup --------------------

    /// <summary>
    /// Sets the player and populates the card with their assigned secret objective.
    /// Call this before showing the display.
    /// </summary>
    public void SetPlayer(Player player)
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        CaptureOriginalScales(); // Ensure scales are captured before Awake may have run

        this.player = player;
        isRevealed = false;

        // Reset card scales in case a flip was interrupted
        ResetCardScale(cardHidden);
        ResetCardScale(cardRevealed);

        ShowHiddenSide();

        SecretObjective objective = SecretObjectiveManager.GetSecretObjectiveByPlayerId(player.id);

        if (objective != null)
            PopulateCard(objective);
        else
            PopulateCivilianCard();
    }

    private void ResetCardScale(GameObject card)
    {
        if (card == null) return;
        RectTransform rect = card.GetComponent<RectTransform>();
        if (card == cardHidden)
            rect.localScale = cardHiddenOriginalScale;
        else if (card == cardRevealed)
            rect.localScale = cardRevealedOriginalScale;
    }

    private void PopulateCard(SecretObjective objective)
    {
        objectiveType = objective.type;
        secretObjectiveText = objective.description;
        score = objective.points;

        if (cardImage != null)
            cardImage.sprite = GetSpriteForType(objective.type);

        // Icon logic: enable/disable and set sprite/color
        if (spyIconImage != null && civilianIconImage != null)
        {
            switch (objective.type)
            {
                case GameManager.SecretObjectiveType.Interruption:
                    spyIconImage.gameObject.SetActive(true);
                    civilianIconImage.gameObject.SetActive(false);
                    spyIconImage.sprite = spyIconSprite;
                    spyIconImage.color = GetSpyIconColorForType(objective.type);
                    break;
                case GameManager.SecretObjectiveType.Betrayal:
                    spyIconImage.gameObject.SetActive(true);
                    civilianIconImage.gameObject.SetActive(false);
                    spyIconImage.sprite = spyIconSprite;
                    spyIconImage.color = GetSpyIconColorForType(objective.type);
                    break;
                case GameManager.SecretObjectiveType.Speech:
                    spyIconImage.gameObject.SetActive(true);
                    civilianIconImage.gameObject.SetActive(false);
                    spyIconImage.sprite = spyIconSprite;
                    spyIconImage.color = GetSpyIconColorForType(objective.type);
                    break;
                case GameManager.SecretObjectiveType.Civilian:
                default:
                    spyIconImage.gameObject.SetActive(false);
                    civilianIconImage.gameObject.SetActive(true);
                    civilianIconImage.sprite = civilianIconSprite;
                    break;
            }
        }

        if (descriptionText != null)
            descriptionText.text = objective.description;

        if (pointsText != null)
            pointsText.text = $"{objective.points} pts";

        if (typeText != null)
            typeText.text = objective.type.ToString();

        Color textColor = GetTextColorForType(objective.type);
        if (descriptionText != null) descriptionText.color = textColor;
        if (pointsText != null)      pointsText.color      = textColor;
        if (typeText != null)        typeText.color        = textColor;
    }

    private void PopulateCivilianCard()
    {
        objectiveType = GameManager.SecretObjectiveType.Civilian;
        secretObjectiveText = "You have no secret objective. Debate honestly!";
        score = 0;

        if (cardImage != null)
            cardImage.sprite = civilianDisplaySprite;

        if (spyIconImage != null) spyIconImage.gameObject.SetActive(false);
        if (civilianIconImage != null)
        {
            civilianIconImage.gameObject.SetActive(true);
            civilianIconImage.sprite = civilianIconSprite;
        }

        if (descriptionText != null)
            descriptionText.text = secretObjectiveText;

        if (pointsText != null)
            pointsText.text = "";

        if (typeText != null)
            typeText.text = "Civilian";

        Color textColor = GetTextColorForType(GameManager.SecretObjectiveType.Civilian);
        if (descriptionText != null) descriptionText.color = textColor;
        if (pointsText != null)      pointsText.color      = textColor;
        if (typeText != null)        typeText.color        = textColor;
    }
    /// <summary>
    /// Returns the color for the spy icon based on the secret objective type.
    /// </summary>
    private Color GetSpyIconColorForType(GameManager.SecretObjectiveType type)
    {
        return type switch
        {
            GameManager.SecretObjectiveType.Speech => spySpeechColor,
            GameManager.SecretObjectiveType.Interruption => spyInterruptionColor,
            GameManager.SecretObjectiveType.Betrayal => spyBetrayalColor,
            _ => Color.white,
        };
    }

    private Sprite GetSpriteForType(GameManager.SecretObjectiveType type)
    {
        return type switch
        {
            GameManager.SecretObjectiveType.Speech => speechDisplaySprite,
            GameManager.SecretObjectiveType.Interruption => interruptionDisplaySprite,
            GameManager.SecretObjectiveType.Betrayal => betrayalDisplaySprite,
            GameManager.SecretObjectiveType.Civilian => civilianDisplaySprite,
            _ => civilianDisplaySprite
        };
    }

    /// <summary>
    /// Interruption and Betrayal use the spy icon.
    /// Speech uses the secObj icon. Civilian uses the civilian icon.
    /// </summary>
    private Sprite GetIconForType(GameManager.SecretObjectiveType type)
    {
        return type switch
        {
            GameManager.SecretObjectiveType.Interruption => spyIconSprite,
            GameManager.SecretObjectiveType.Betrayal     => spyIconSprite,
            GameManager.SecretObjectiveType.Speech       => spyIconSprite,
            GameManager.SecretObjectiveType.Civilian     => civilianIconSprite,
        };
    }

    /// <summary>
    /// Betrayal and Interruption use #DDF4E7 (light green).
    /// Speech and Civilian use #282828 (near-black).
    /// </summary>
    private Color GetTextColorForType(GameManager.SecretObjectiveType type)
    {
        return type switch
        {
            GameManager.SecretObjectiveType.Betrayal      => new Color(0xDD / 255f, 0xF4 / 255f, 0xE7 / 255f),
            GameManager.SecretObjectiveType.Interruption  => new Color(0xDD / 255f, 0xF4 / 255f, 0xE7 / 255f),
            _                                              => new Color(0x28 / 255f, 0x28 / 255f, 0x28 / 255f),
        };
    }

    // -------------------- Navigation --------------------

    public void Next()
    {
        gameManager.AdvanceSecretObjectiveSequence();
    }
}
