using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class Topic
{
    public int id;
    public string title;
    public string description;
    public string descriptionIs;       // Icelandic description
    public string optionA;             // "This" label (English)
    public string optionB;             // "That" label (English)
    public string optionAIs;           // "Hitt" label (Icelandic)
    public string optionBIs;           // "Þetta" label (Icelandic)
    public GameManager.Pack pack;
    public TopicManager.TopicType type;
    public int seriousness;            // 0-5 scale
}

public class TopicManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TopicDatabase topicDatabase;

    public enum TopicType { Versus, Scenarios }

    public List<Topic> allTopics = new List<Topic>();
    private List<Topic> currentPackTopics = new List<Topic>();

    public Topic currentTopic;
    public List<Topic> seenTopics = new List<Topic>();

    void Start()
    {
        LoadTopics();
    }

    public void LoadTopics()
    {
        allTopics = topicDatabase.LoadTopics();
        Debug.Log($"TopicManager: Loaded {allTopics.Count} topics.");
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

    public Topic GetRandomVersusTopic(int seriousnessLevel)
    {
        return GetRandomTopic(TopicType.Versus, seriousnessLevel);
    }

    public Topic GetRandomScenarioTopic(int seriousnessLevel)
    {
        return GetRandomTopic(TopicType.Scenarios, seriousnessLevel);
    }

    // -------------------- Internal Logic --------------------

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
