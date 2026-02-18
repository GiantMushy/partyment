using UnityEngine;

[CreateAssetMenu(menuName = "State your Case/Secret Objective")]
public class SecretObjectives : ScriptableObject
{
    public ObjectivesRoles role;
    public enum Type { Speech, Interruption, Betrayal }
    public string Title;
    
    [TextArea(2,5)]
    public string description;

    public int points;

    // kind of optional, per objective icon override
    public Sprite overrideIcon;
    
}
