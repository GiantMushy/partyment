using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Clears the EventSystem selection when a panel is re-enabled so buttons do not
/// re-evaluate to a stale Pressed or Selected state.
/// </summary>
public class ButtonAnimatorReset : MonoBehaviour
{
    private void OnEnable()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }
}