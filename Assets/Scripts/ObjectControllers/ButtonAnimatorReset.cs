using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Lightweight safety net: clears EventSystem selection when a panel is re-enabled
/// so <c>Button.OnEnable</c> doesn't re-evaluate to a stale Pressed/Selected state.
/// The primary fix lives in <see cref="GameManager.SetState"/>, which calls
/// <c>DisableState</c> to reset Selectable animators on the outgoing panel.
/// Place this on panels that contain buttons.
/// </summary>
public class ButtonAnimatorReset : MonoBehaviour
{
    private void OnEnable()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }
}