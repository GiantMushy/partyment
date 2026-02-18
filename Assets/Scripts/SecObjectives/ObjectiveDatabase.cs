using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "State Your Case/Objective Database")]
public class ObjectiveDatabase : ScriptableObject
{
    public List<SecretObjectives> allObjectives = new List<SecretObjectives>();

    [Header("Role Sprites")]
    public Sprite speechSprite;
    public Sprite betrayalSprite;
    public Sprite interruptionSprite;
    public Sprite nanSprite;

    public Sprite GetSprite(ObjectiveRole role)
    {
        switch (role)
        {
            case ObjectiveRole.Speech: return speechSprite;
            case ObjectiveRole.Betrayal: return betrayalSprite;
            case ObjectiveRole.Interruption: return interruptionSprite;
            default: return nanSprite;
        }
    }
}
