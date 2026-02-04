using UnityEngine;

[System.Serializable]
public class CrisisModel
{
    public int id;
    public string title;
    public string description;
    public string leadingQuestionFor;
    public string leadingQuestionAgainst;
    public GameManager.CrisisPack pack;

    
}