using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Topic Selection screen.
///
/// Versus is selected by default. Callbacks are registered in Start() — do NOT
/// also wire them in the Inspector or they will fire twice.
///
/// Inspector wiring required:
///   Toggle Buttons  → versusToggle / scenariosToggle (ToggleButton components)
///   Topic Display   → bodyText (Body/Text TMP), topicTypeVersusObject / topicTypeScenariosObject
///                     (GameObjects for the two localized topic-type labels),
///                     topicIcon (Header/Icon Image), versusIcon + scenariosIcon (Sprites)
///   Shuffle         → shuffleButton, shuffleCountText (its TMP child)
///   Confirm         → selectButton
/// </summary>
public class TopicSelectionController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;

    [Header("Toggle Buttons")]
    [SerializeField] private ToggleButton versusToggle;
    [SerializeField] private ToggleButton scenariosToggle;

    [Header("Topic Description — Body")]
    [SerializeField] private TextMeshProUGUI bodyText;

    [Header("Topic Description — Header")]
    [SerializeField] private GameObject topicTypeVersusObject;
    [SerializeField] private GameObject topicTypeScenariosObject;
    [SerializeField] private Image topicIcon;
    [SerializeField] private Sprite versusIcon;
    [SerializeField] private Sprite scenariosIcon;

    [Header("Shuffle")]
    [SerializeField] private Button shuffleButton;
    [SerializeField] private TextMeshProUGUI shuffleCountText;
    [Tooltip("How many times the player may shuffle topics per round.")]
    public int startingNumOfShuffles = 1;

    [Header("Navigation")]
    [SerializeField] private Button selectButton;

    // -------------------- State --------------------

    private Topic versusTopic;
    private Topic scenarioTopic;
    private bool versusSelected = true;
    private int shufflesRemaining;

    // ================================================================
    //  Unity Lifecycle
    // ================================================================

    void Start()
    {
        gameManager = GameManager.Instance;

        // Register callbacks in code — do NOT also wire these in the Inspector.
        versusToggle?.onClick.AddListener(OnVersusClicked);
        scenariosToggle?.onClick.AddListener(OnScenariosClicked);
        if (shuffleButton  != null) shuffleButton.onClick.AddListener(OnShuffleClicked);
        if (selectButton   != null) selectButton.onClick.AddListener(OnSelectClicked);
    }

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (gameManager == null || gameManager.topicManager == null)
        {
            Debug.LogWarning("TopicSelectionController.OnEnable: GameManager not ready, skipping.");
            return;
        }

        GameManager.OnLanguageChanged += RefreshUI;

        shufflesRemaining = startingNumOfShuffles;
        versusSelected    = true;

        LoadRandomTopics();
        RefreshUI();
    }

    void OnDisable()
    {
        GameManager.OnLanguageChanged -= RefreshUI;
    }

    // ================================================================
    //  Topic Loading
    // ================================================================

    private void LoadRandomTopics()
    {
        var tm = gameManager.topicManager;
        tm.LoadTopicsFromPack();

        int seriousness = gameManager.selectedSeriousnessLevel;
        versusTopic   = tm.GetRandomVersusTopic(seriousness);
        scenarioTopic = tm.GetRandomScenarioTopic(seriousness);

        Debug.Log($"TopicSelection — Versus: {versusTopic?.description}, Scenario: {scenarioTopic?.description}");
    }

    // ================================================================
    //  UI Refresh
    // ================================================================

    private void RefreshUI()
    {
        versusToggle?.SetToggled(versusSelected);
        scenariosToggle?.SetToggled(!versusSelected);

        // Topic description — use Icelandic text when the game language is Icelandic
        Topic displayed = versusSelected ? versusTopic : scenarioTopic;
        if (bodyText != null)
            bodyText.text = displayed != null ? GetLocalizedDescription(displayed) : "No topic available";

        if (topicTypeVersusObject   != null) topicTypeVersusObject.SetActive(versusSelected);
        if (topicTypeScenariosObject != null) topicTypeScenariosObject.SetActive(!versusSelected);

        if (topicIcon != null)
            topicIcon.sprite = versusSelected ? versusIcon : scenariosIcon;

        // Shuffle button
        if (shuffleButton != null)
            shuffleButton.interactable = shufflesRemaining > 0;
        if (shuffleCountText != null)
            shuffleCountText.text = $"x{shufflesRemaining}";

        // Select button disabled when there's no topic to pick
        if (selectButton != null)
            selectButton.interactable = displayed != null;
    }

    // ================================================================
    //  Button Callbacks  (registered in Start — do NOT wire in Inspector)
    // ================================================================

    public void OnVersusClicked()
    {
        if (versusSelected) return;
        versusSelected = true;
        RefreshUI();
    }

    public void OnScenariosClicked()
    {
        if (!versusSelected) return;
        versusSelected = false;
        RefreshUI();
    }

    public void OnShuffleClicked()
    {
        if (shufflesRemaining <= 0) return;
        shufflesRemaining--;
        LoadRandomTopics();
        RefreshUI();
    }

    private string GetLocalizedDescription(Topic topic)
    {
        if (gameManager != null && gameManager.selectedLanguage == GameManager.Language.Icelandic
            && !string.IsNullOrEmpty(topic.descriptionIs))
            return topic.descriptionIs;
        return topic.description;
    }

    public void OnSelectClicked()
    {
        Topic selected = versusSelected ? versusTopic : scenarioTopic;
        if (selected == null) return;

        EventSystem.current.SetSelectedGameObject(null);
        gameManager.topicManager.currentTopic = selected;
        gameManager.SetState(GameManager.GameState.MetricSelection);
    }
}
