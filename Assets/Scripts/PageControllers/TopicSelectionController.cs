using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Topic Selection screen. Versus topics are selected by default.
/// Button callbacks are registered in <see cref="Start"/> and must not also be wired
/// in the Inspector.
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

    private Topic versusTopic;
    private Topic scenarioTopic;
    private bool versusSelected = true;
    private int shufflesRemaining;

    // The round number topics were last loaded for. Guards against re-shuffling when the
    // screen is re-entered via the MetricSelection "Back" button. -1 forces a fresh load.
    private int loadedForRound = -1;

    void Start()
    {
        gameManager = GameManager.Instance;

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

        // Only shuffle a fresh topic on a genuinely new round (or new game). Re-entering
        // this screen via MetricSelection's "Back" button must preserve the chosen topic.
        if (loadedForRound != gameManager.currentRound)
        {
            shufflesRemaining = startingNumOfShuffles;
            versusSelected    = true;

            LoadRandomTopics();
            loadedForRound = gameManager.currentRound;
        }

        RefreshUI();
    }

    /// <summary>
    /// Forces the next entry to this screen to load a fresh topic. Called by
    /// <see cref="GameManager.NewGame"/> so a new game always re-shuffles, even when the
    /// previous game ended on the same round number.
    /// </summary>
    public void ResetForNewGame()
    {
        loadedForRound = -1;
    }

    void OnDisable()
    {
        GameManager.OnLanguageChanged -= RefreshUI;
    }

    private void LoadRandomTopics()
    {
        var tm = gameManager.topicManager;
        tm.LoadTopicsFromPack();

        int seriousness = gameManager.selectedSeriousnessLevel;
        versusTopic   = tm.GetRandomVersusTopic(seriousness);
        scenarioTopic = tm.GetRandomScenarioTopic(seriousness);

        Debug.Log($"TopicSelection — Versus: {versusTopic?.description}, Scenario: {scenarioTopic?.description}");
    }

    private void RefreshUI()
    {
        versusToggle?.SetToggled(versusSelected);
        scenariosToggle?.SetToggled(!versusSelected);

        Topic displayed = versusSelected ? versusTopic : scenarioTopic;
        if (bodyText != null)
            bodyText.text = displayed != null ? GetLocalizedDescription(displayed) : "No topic available";

        if (topicTypeVersusObject   != null) topicTypeVersusObject.SetActive(versusSelected);
        if (topicTypeScenariosObject != null) topicTypeScenariosObject.SetActive(!versusSelected);

        if (topicIcon != null)
            topicIcon.sprite = versusSelected ? versusIcon : scenariosIcon;

        if (shuffleButton != null)
            shuffleButton.interactable = shufflesRemaining > 0;
        if (shuffleCountText != null)
            shuffleCountText.text = $"x{shufflesRemaining}";

        if (selectButton != null)
            selectButton.interactable = displayed != null;
    }

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
