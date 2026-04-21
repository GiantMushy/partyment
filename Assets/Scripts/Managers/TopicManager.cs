using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class Topic
{
    public int id;
    public string title;
    public string description;
    public GameManager.Pack pack;
    public TopicManager.TopicType type;
    public int seriousness; // Scale of 0-5 for how serious the topic is
    public string leadingQuestionFor;
    public string leadingQuestionAgainst;
}

public class TopicManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TopicDatabase topicDatabase;
    public enum TopicType { Short, Medium, Long }

    // Master list — populated by TopicDatabase or a real data source
    public List<Topic> allTopics = new List<Topic>();

    // Working list — filtered to the currently selected pack
    private List<Topic> currentPackTopics = new List<Topic>();

    public Topic currentTopic;
    public List<Topic> seenTopics = new List<Topic>();

    void Start()
    {
        LoadDevTopics();
    }

    public void LoadDevTopics()
    {
        allTopics = topicDatabase.LoadDevTopics();
        Debug.Log($"TopicManager: Loaded {allTopics.Count} dev topics.");
    }

    public void LoadTopicsFromPack()
    {
        var pack = GameManager.selectedPack;
        currentPackTopics = allTopics.Where(b => b.pack == pack).ToList();
        Debug.Log($"Loaded {currentPackTopics.Count} topics for pack {pack}");
    }

    public void ResetTopicSelection()
    {
        seenTopics.Clear();
        currentTopic = null;
    }

    // -------------------- Public Getters --------------------

    public Topic GetRandomShortTopic(int seriousnessLevel)
    {
        return GetRandomTopic(TopicType.Short, seriousnessLevel);
    }

    public Topic GetRandomMediumTopic(int seriousnessLevel)
    {
        return GetRandomTopic(TopicType.Medium, seriousnessLevel);
    }

    public Topic GetRandomLongTopic(int seriousnessLevel)
    {
        return GetRandomTopic(TopicType.Long, seriousnessLevel);
    }

    // -------------------- Internal Logic --------------------

    private Topic GetRandomTopic(TopicType type, int seriousnessLevel)
    {
        var candidates = GetUnseenTopics(type, seriousnessLevel);

        if (candidates.Count == 0)
        {
            // All topics of this type have been seen — reset and try again
            Debug.Log($"All {type} topics seen, resetting seen list for this type.");
            seenTopics.RemoveAll(t => t.type == type);
            candidates = GetUnseenTopics(type, seriousnessLevel);
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"No {type} topics available for seriousness level {seriousnessLevel} even after reset.");
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