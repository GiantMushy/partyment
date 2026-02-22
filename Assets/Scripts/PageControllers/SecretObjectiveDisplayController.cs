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
    [SerializeField] private GameObject nextButton;

    [Header("Card Children")]
    [SerializeField] private Image cardImage;       // Background image on the revealed side
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI pointsText;
    [SerializeField] private TextMeshProUGUI typeText;

    [Header("Flip Settings")]
    [SerializeField] private float flipDuration = 0.2f;

    [Header("Variables")]
    private GameManager.SecretObjectiveType objectiveType;
    private string secretObjectiveText;
    private int score;

    private bool isRevealed = false;
    private bool hasBeenRevealed = false;
    private Coroutine flipCoroutine;

    private Vector3 cardHiddenOriginalScale;
    private Vector3 cardRevealedOriginalScale;

    void Awake()
    {
        if (cardHidden != null)
            cardHiddenOriginalScale = cardHidden.GetComponent<RectTransform>().localScale;
        if (cardRevealed != null)
            cardRevealedOriginalScale = cardRevealed.GetComponent<RectTransform>().localScale;
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
        this.player = player;
        isRevealed = false;

        // Reset card scales in case a flip was interrupted
        ResetCardScale(cardHidden);
        ResetCardScale(cardRevealed);

        ShowHiddenSide();

        SecretObjective objective = SecretObjectiveManager.GetSecretObjectiveForPlayer(player.id);

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

        if (descriptionText != null)
            descriptionText.text = objective.description;

        if (pointsText != null)
            pointsText.text = $"{objective.points} pts";

        if (typeText != null)
            typeText.text = objective.type.ToString();
    }

    private void PopulateCivilianCard()
    {
        objectiveType = GameManager.SecretObjectiveType.Civilian;
        secretObjectiveText = "You have no secret objective. Debate honestly!";
        score = 0;

        if (cardImage != null)
            cardImage.sprite = civilianDisplaySprite;

        if (descriptionText != null)
            descriptionText.text = secretObjectiveText;

        if (pointsText != null)
            pointsText.text = "";

        if (typeText != null)
            typeText.text = "Civilian";
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

    // -------------------- Navigation --------------------

    public void Next()
    {
        gameManager.AdvanceSecretObjectiveSequence();
    }
}
