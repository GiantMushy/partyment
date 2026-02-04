using UnityEngine;

[System.Serializable]
public class SecretObjective
{
    public int id;
    public string title;
    public string description;
    public string shortDescription;
    public int points;
    public int assignedPlayerId;
    public int? neededCount;
    public int? achievedCount;
    public bool completeted;
    public GameManager.SecretObjectiveTypes type;
}