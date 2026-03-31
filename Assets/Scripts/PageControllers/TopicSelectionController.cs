using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class TopicSelectionController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    [SerializeField] private TextMeshProUGUI shortTopicText;
    [SerializeField] private TextMeshProUGUI mediumTopicText;
    [SerializeField] private TextMeshProUGUI longTopicText;

    [Header("Variables")]
    public Topic shortTopic;
    public Topic mediumTopic;
    public Topic longTopic;

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
        shortTopicText.text = shortTopic != null ? shortTopic.description : "No topic";
        mediumTopicText.text = mediumTopic != null ? mediumTopic.description : "No topic";
        longTopicText.text = longTopic != null ? longTopic.description : "No topic";
    }

    // -------------------- Button Callbacks --------------------

    public void TopicShort()  { SelectTopic(shortTopic); }
    public void TopicMedium() { SelectTopic(mediumTopic); }
    public void TopicLong()   { SelectTopic(longTopic); }

    /// <summary>
    /// Loads 3 new random topics and resets the selection. Hook this up to the Refresh button.
    /// </summary>
    public void Refresh()
    {
        LoadRandomTopics();
        PopulateButtonText();
    }

    // -------------------- Selection Logic --------------------
    private void SelectTopic(Topic topic)
    {
        if (topic == null) return;
        gameManager.topicManager.currentTopic = topic;
        gameManager.SetState(GameManager.GameState.MetricSelection);
    }
}
