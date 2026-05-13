using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Per-player secret corruption reveal screen, shown one player at a time after each
/// <see cref="GameManager.GameState.PlayerMutex"/> hand-off. The card is hidden by default;
/// holding the pointer down flips it to the revealed face (X-axis scale flip via
/// <see cref="FlipCoroutine"/>), releasing flips it back. The Next button only appears
/// after the first reveal, ensuring the player actually saw their objective.
/// Civilian players (no objective) get a generic card. Card art and colors vary by
/// corruption type — see <see cref="GetSpriteForType"/> / <see cref="GetTextColorForType"/>.
/// </summary>
public class CorruptionDisplayController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("References")]
    private GameManager gameManager;
    private CorruptionManager CorruptionManager => gameManager.corruptionManager;
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
    [SerializeField] private GameObject pointsNumObject;
    [SerializeField] private GameObject pointsStringObject;

    [Header("Spy Icon Colors")]
    [SerializeField] private Color spySpeechColor = Color.white;
    [SerializeField] private Color spyInterruptionColor = Color.white;
    [SerializeField] private Color spyBetrayalColor = Color.white;

    [Header("Flip Settings")]
    [SerializeField] private float flipDuration = 0.2f;

    [Header("Variables")]
    private GameManager.CorruptionType objectiveType;
    private string corruptionText;
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
        GameManager.OnLanguageChanged += OnLanguageChanged;
        ShowHiddenSide();
        hasBeenRevealed = false;
        if (nextButton != null) nextButton.SetActive(false);
    }

    void OnDisable()
    {
        GameManager.OnLanguageChanged -= OnLanguageChanged;
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

    private void OnLanguageChanged()
    {
        if (player == null) return;
        Corruption obj = CorruptionManager.GetCorruptionByPlayerId(player.id);
        if (obj != null)
        {
            if (descriptionText != null)
                descriptionText.text = GetLocalizedDescription(obj);
        }
        else
        {
            // Civilian card — refresh the hardcoded text
            if (descriptionText != null)
                descriptionText.text = GetCivilianText();
        }
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
    /// Sets the player and populates the card with their assigned corruption.
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

        Corruption objective = CorruptionManager.GetCorruptionByPlayerId(player.id);

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

    private void PopulateCard(Corruption objective)
    {
        objectiveType = objective.type;
        corruptionText = objective.description;
        score = objective.points;

        if (cardImage != null)
            cardImage.sprite = GetSpriteForType(objective.type);

        // Icon logic: enable/disable and set sprite/color
        if (spyIconImage != null && civilianIconImage != null)
        {
            switch (objective.type)
            {
                case GameManager.CorruptionType.Interruption:
                    spyIconImage.gameObject.SetActive(true);
                    civilianIconImage.gameObject.SetActive(false);
                    spyIconImage.sprite = spyIconSprite;
                    spyIconImage.color = GetSpyIconColorForType(objective.type);
                    break;
                case GameManager.CorruptionType.Betrayal:
                    spyIconImage.gameObject.SetActive(true);
                    civilianIconImage.gameObject.SetActive(false);
                    spyIconImage.sprite = spyIconSprite;
                    spyIconImage.color = GetSpyIconColorForType(objective.type);
                    break;
                case GameManager.CorruptionType.Speech:
                    spyIconImage.gameObject.SetActive(true);
                    civilianIconImage.gameObject.SetActive(false);
                    spyIconImage.sprite = spyIconSprite;
                    spyIconImage.color = GetSpyIconColorForType(objective.type);
                    break;
                case GameManager.CorruptionType.Civilian:
                default:
                    spyIconImage.gameObject.SetActive(false);
                    civilianIconImage.gameObject.SetActive(true);
                    civilianIconImage.sprite = civilianIconSprite;
                    break;
            }
        }

        if (descriptionText != null)
            descriptionText.text = GetLocalizedDescription(objective);

        bool showPoints = objective.type != GameManager.CorruptionType.Civilian;
        if (pointsNumObject != null) pointsNumObject.SetActive(showPoints);
        if (pointsStringObject != null) pointsStringObject.SetActive(showPoints);

        if (pointsText != null)
            pointsText.text = $"{objective.points}";

        if (typeText != null)
            typeText.text = objective.type.ToString();

        Color textColor = GetTextColorForType(objective.type);
        if (descriptionText != null) descriptionText.color = textColor;
        if (pointsText != null)      pointsText.color      = textColor;
        if (typeText != null)        typeText.color        = textColor;
    }

    private string GetLocalizedDescription(Corruption obj)
    {
        if (gameManager != null && gameManager.selectedLanguage == GameManager.Language.Icelandic
            && !string.IsNullOrEmpty(obj.descriptionIs))
            return obj.descriptionIs;
        return obj.description;
    }

    private string GetCivilianText()
    {
        return gameManager != null && gameManager.selectedLanguage == GameManager.Language.Icelandic
            ? "Þú hefur enga spillingu. Rökræddu heiðarlega!"
            : "You have no corruption. Debate honestly!";
    }

    private void PopulateCivilianCard()
    {
        objectiveType = GameManager.CorruptionType.Civilian;
        corruptionText = GetCivilianText();
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
            descriptionText.text = GetCivilianText();

        if (pointsNumObject != null) pointsNumObject.SetActive(false);
        if (pointsStringObject != null) pointsStringObject.SetActive(false);

        if (typeText != null)
            typeText.text = "Civilian";

        Color textColor = GetTextColorForType(GameManager.CorruptionType.Civilian);
        if (descriptionText != null) descriptionText.color = textColor;
        if (pointsText != null)      pointsText.color      = textColor;
        if (typeText != null)        typeText.color        = textColor;
    }

    private Color GetSpyIconColorForType(GameManager.CorruptionType type)
    {
        return type switch
        {
            GameManager.CorruptionType.Speech => spySpeechColor,
            GameManager.CorruptionType.Interruption => spyInterruptionColor,
            GameManager.CorruptionType.Betrayal => spyBetrayalColor,
            _ => Color.white,
        };
    }

    private Sprite GetSpriteForType(GameManager.CorruptionType type)
    {
        return type switch
        {
            GameManager.CorruptionType.Speech => speechDisplaySprite,
            GameManager.CorruptionType.Interruption => interruptionDisplaySprite,
            GameManager.CorruptionType.Betrayal => betrayalDisplaySprite,
            GameManager.CorruptionType.Civilian => civilianDisplaySprite,
            _ => civilianDisplaySprite
        };
    }

    private Sprite GetIconForType(GameManager.CorruptionType type)
    {
        return type switch
        {
            GameManager.CorruptionType.Interruption => spyIconSprite,
            GameManager.CorruptionType.Betrayal     => spyIconSprite,
            GameManager.CorruptionType.Speech       => spyIconSprite,
            GameManager.CorruptionType.Civilian     => civilianIconSprite,
        };
    }

    /// <summary>
    /// Betrayal and Interruption use #DDF4E7 (light green).
    /// Speech and Civilian use #282828 (near-black).
    /// </summary>
    private Color GetTextColorForType(GameManager.CorruptionType type)
    {
        return type switch
        {
            GameManager.CorruptionType.Betrayal      => new Color(0xDD / 255f, 0xF4 / 255f, 0xE7 / 255f),
            GameManager.CorruptionType.Interruption  => new Color(0xDD / 255f, 0xF4 / 255f, 0xE7 / 255f),
            _                                        => new Color(0x28 / 255f, 0x28 / 255f, 0x28 / 255f),
        };
    }

    // -------------------- Navigation --------------------

    public void Next()
    {
        gameManager.AdvanceCorruptionSequence();
    }
}
