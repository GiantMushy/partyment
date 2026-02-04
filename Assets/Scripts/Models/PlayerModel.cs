using UnityEngine;

[System.Serializable]
public class PlayerModel
{
    public int id;
    public string name;
    public Color favouredColor;
    public GameManager.PlayerGroup group;

    public PlayerModel(int id, string name)
    {
        this.id = id;
        this.name = name;
        this.favouredColor = Color.white; // Default color
        this.group = GameManager.PlayerGroup.Unassigned;
    }
}