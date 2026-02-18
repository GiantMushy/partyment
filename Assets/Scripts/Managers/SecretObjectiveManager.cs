using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class SecretObjective
{
    public int id;
    public int assignedPlayerId = -1; // -1 means unassigned
    public string title;
    public string description;
    public string shortDescription;
    public int points;
    public int? neededCount; // How many times the player needs to achieve this objective, if applicable (null if not count-based)
    public int? achievedCount; // How many times the player has achieved this objective so far (null if not count-based)
    public bool completeted;
    public SecretObjectiveManager.SecretObjectiveType type;
}

public class SecretObjectiveManager : MonoBehaviour
{
    public enum SecretObjectiveType { Speech, Interruption, Betrayal }
    public List<SecretObjective> allSecretObjectives = new List<SecretObjective>();
    public List<int> usedSecretObjectives = new List<int>(); // Track used objectives by their IDs

    void Start()
    {
        LoadSecretObjectives();
    }

    public void LoadSecretObjectives()
    {
        // Placeholder: In a real implementation, this would load from a database or file
        Debug.Log($"Loading secret objectives");
    }

    public SecretObjective GetRandomUnusedSecretObjective(SecretObjectiveType type)
    {
        // Placeholder: In a real implementation, this would filter secret objectives by type and return a random, unused one
        Debug.Log($"Getting random secret objective of type {type}");
        return null;
    }
}