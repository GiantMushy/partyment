using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Lightweight safety net: clears EventSystem selection when a panel is re-enabled
/// so that Button.OnEnable doesn't re-evaluate to a stale Pressed/Selected state.
/// The primary fix lives in GameManager.ResetButtonAnimatorsBeforeDisable().
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