using UnityEngine;
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

    [Header("Sprites")]
    [SerializeField] private Sprite shortTopicSprite;
    [SerializeField] private Sprite shortTopicSelectedSprite;
    [SerializeField] private Sprite mediumTopicSprite;
    [SerializeField] private Sprite mediumTopicSelectedSprite;
    [SerializeField] private Sprite longTopicSprite;
    [SerializeField] private Sprite longTopicSelectedSprite;

    [Header("Variables")]
    public Topic shortTopic;
    public Topic mediumTopic;
    public Topic longTopic;
    private Topic selectedTopic;
    private Button selectedButton;

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

    public void TopicShort()  { SelectTopic(shortTopic, shortTopicButton); }
    public void TopicMedium() { SelectTopic(mediumTopic, mediumTopicButton); }
    public void TopicLong()   { SelectTopic(longTopic, longTopicButton); }

    public void Select()
    {
        if (selectedTopic == null) return;

        gameManager.topicManager.currentTopic = selectedTopic;
        gameManager.SetState(GameManager.GameState.MetricSelection);
    }

    // -------------------- Selection Logic --------------------

    private void SelectTopic(Topic topic, Button button)
    {
        if (topic == null) return;

        selectedTopic = topic;
        SetSelectedVisual(button);
    }

    private void ClearSelection()
    {
        selectedTopic = null;
        SetSelectedVisual(null);
    }

    private void SetSelectedVisual(Button selected)
    {
        selectedButton = selected;
        ApplyButtonSprite(shortTopicButton, selected == shortTopicButton, shortTopicSprite, shortTopicSelectedSprite);
        ApplyButtonSprite(mediumTopicButton, selected == mediumTopicButton, mediumTopicSprite, mediumTopicSelectedSprite);
        ApplyButtonSprite(longTopicButton, selected == longTopicButton, longTopicSprite, longTopicSelectedSprite);

        if (selectButton != null)
            selectButton.interactable = selected != null;
    }

    private void ApplyButtonSprite(Button button, bool isSelected, Sprite normalSprite, Sprite selectedSprite)
    {
        if (button == null) return;

        var image = button.GetComponent<Image>();
        if (image == null) return;

        Sprite target = isSelected ? selectedSprite : normalSprite;
        if (target != null) image.sprite = target;
    }
}
