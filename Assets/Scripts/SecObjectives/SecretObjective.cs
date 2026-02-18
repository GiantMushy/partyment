using UnityEngine;

[CreateAssetMenu(menuName = "State Your Case/Secret Objective")]
public class SecretObjectives : ScriptableObject
{
    public ObjectiveRole role;

    [TextArea(2, 5)]
    public string description;

    public int points;
}
