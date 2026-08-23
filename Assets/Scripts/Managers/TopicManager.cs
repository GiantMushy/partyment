using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// One debate-topic row loaded from <c>Topics.csv</c>. Carries English and Icelandic
/// text; Versus topics also include the option labels.
/// </summary>
[System.Serializable]
public class Topic
{
    public int id;
    public string title;
    public string description;
    public string descriptionIs;
    public string optionA;
    public string optionB;
    public string optionAIs;
    public string optionBIs;
    public GameManager.Pack pack;
    public TopicManager.TopicType type;
    public int seriousness;
}

/// <summary>
/// Owns the master list of <see cref="Topic"/> rows loaded from CSV via
/// <see cref="TopicDatabase"/>, the active pack subset, and the seen-topic history.
/// Topic selection filters by pack, type, and seriousness within ±1 of
/// <see cref="GameManager.selectedSeriousnessLevel"/>. When a type runs out of unseen
/// topics, only that type's history is reset.
/// </summary>
public class TopicManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TopicDatabase topicDatabase;

    public enum TopicType { Versus, Scenarios }

    public List<Topic> allTopics = new List<Topic>();
    private List<Topic> currentPackTopics = new List<Topic>();

    public Topic currentTopic;
    public List<Topic> seenTopics = new List<Topic>();

    // Set once the CSV has been parsed, so a caller that runs before Awake (or before this
    // component's Start) can force the load instead of filtering an empty list.
    private bool topicsLoaded;

    void Awake()
    {
        LoadTopics();
    }

    public void LoadTopics()
    {
        allTopics = topicDatabase.LoadTopics();
        topicsLoaded = true;
        Debug.Log($"TopicManager: Loaded {allTopics.Count} topics.");
    }

    public void LoadTopicsFromPack()
    {
        if (!topicsLoaded) LoadTopics();

        var pack = GameManager.selectedPack;
        currentPackTopics = allTopics.Where(b => b.pack == pack).ToList();
        Debug.Log($"Loaded {currentPackTopics.Count} topics for pack {pack}");
    }

    public void ResetTopicSelection()
    {
        seenTopics.Clear();
        currentTopic = null;
    }

    public Topic GetRandomVersusTopic(int seriousnessLevel)
    {
        return GetRandomTopic(TopicType.Versus, seriousnessLevel);
    }

    public Topic GetRandomScenarioTopic(int seriousnessLevel)
    {
        return GetRandomTopic(TopicType.Scenarios, seriousnessLevel);
    }

    private Topic GetRandomTopic(TopicType type, int seriousnessLevel)
    {
        var candidates = GetUnseenTopics(type, seriousnessLevel);

        if (candidates.Count == 0)
        {
            Debug.Log($"All {type} topics seen — resetting seen list for this type.");
            seenTopics.RemoveAll(t => t.type == type);
            candidates = GetUnseenTopics(type, seriousnessLevel);
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"No {type} topics available for seriousness {seriousnessLevel} even after reset.");
            return null;
        }

        var topic = candidates[Random.Range(0, candidates.Count)];
        seenTopics.Add(topic);
        return topic;
    }

    private List<Topic> GetUnseenTopics(TopicType type, int seriousnessLevel)
    {
        return currentPackTopics
            .Where(b => b.type == type)
            .Where(b => Mathf.Abs(b.seriousness - seriousnessLevel) <= 1)
            .Where(b => !seenTopics.Contains(b))
            .ToList();
    }
}
