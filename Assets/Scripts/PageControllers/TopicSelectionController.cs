using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class TopicSelectionController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    [SerializeField] private Button shortTopicButton;
    [SerializeField] private Button mediumTopicButton;
    [SerializeField] private Button longTopicButton;
    [SerializeField] private Button selectButton;

    [Header("Variables")]
    public Topic shortTopic;
    public Topic mediumTopic;
    public Topic longTopic;
    private Topic selectedTopic;
    private Button selectedButton;

    private static readonly int SelectedParam = Animator.StringToHash("Selected");

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;

        // Guard: skip if GameManager isn't ready yet (initial scene load)
        if (gameManager == null || gameManager.topicManager == null)
        {
            Debug.LogWarning("TopicSelectionController.OnEnable: GameManager not ready, skipping.");
            return;
        }

        LoadRandomTopics();
        PopulateButtonText();
        ClearSelection();

        Debug.Log($"TopicSelection OnEnable — short: {shortTopic?.title}, medium: {mediumTopic?.title}, long: {longTopic?.title}");
    }

    // -------------------- Topic Loading --------------------

    private void LoadRandomTopics()
    {
        var tm = gameManager.topicManager;
        tm.LoadTopicsFromPack();

        int seriousness = gameManager.selectedSeriousnessLevel;
        shortTopic = tm.GetRandomShortTopic(seriousness);
        mediumTopic = tm.GetRandomMediumTopic(seriousness);
        longTopic = tm.GetRandomLongTopic(seriousness);
    }

    private void PopulateButtonText()
    {
        SetButtonText(shortTopicButton, shortTopic);
        SetButtonText(mediumTopicButton, mediumTopic);
        SetButtonText(longTopicButton, longTopic);
    }

    private void SetButtonText(Button button, Topic topic)
    {
        if (button == null || topic == null) return;

        var title = button.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
        var description = button.transform.Find("Description")?.GetComponent<TextMeshProUGUI>();

        if (title != null) title.text = topic.title;
        if (description != null) description.text = topic.description;
    }

    // -------------------- Button Callbacks --------------------

    public void TopicShort()  { ToggleTopic(shortTopic, shortTopicButton); }
    public void TopicMedium() { ToggleTopic(mediumTopic, mediumTopicButton); }
    public void TopicLong()   { ToggleTopic(longTopic, longTopicButton); }

    public void Select()
    {
        if (selectedTopic == null) return;

        gameManager.topicManager.currentTopic = selectedTopic;
        gameManager.SetState(GameManager.GameState.MetricSelection);
    }

    /// <summary>
    /// Loads 3 new random topics and resets the selection. Hook this up to the Refresh button.
    /// </summary>
    public void Refresh()
    {
        LoadRandomTopics();
        PopulateButtonText();
        ClearSelection();
    }

    // -------------------- Selection Logic --------------------

    private void ToggleTopic(Topic topic, Button button)
    {
        if (topic == null) return;

        // If the same topic is already selected, deselect it
        if (selectedTopic == topic)
        {
            ClearSelection();
            return;
        }

        selectedTopic = topic;
        SetSelectedVisual(button);
    }

    private void ClearSelection()
    {
        selectedTopic = null;
        SetSelectedVisual(null);
    }

    // LateUpdate runs after all input events and Button DoStateTransition calls,
    // so our bool assignments always win over the Button's own state machine.
    void LateUpdate()
    {
        SetButtonAnimatorSelected(shortTopicButton, selectedButton == shortTopicButton);
        SetButtonAnimatorSelected(mediumTopicButton, selectedButton == mediumTopicButton);
        SetButtonAnimatorSelected(longTopicButton, selectedButton == longTopicButton);
    }

    private void SetSelectedVisual(Button selected)
    {
        selectedButton = selected;

        // Clear EventSystem so keyboard-nav "Selected" trigger doesn't interfere.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (selectButton != null)
            selectButton.interactable = selected != null;
    }

    private void SetButtonAnimatorSelected(Button button, bool isSelected)
    {
        if (button == null) return;

        var animator = button.GetComponent<Animator>();
        if (animator != null)
            animator.SetBool(SelectedParam, isSelected);
    }
}
