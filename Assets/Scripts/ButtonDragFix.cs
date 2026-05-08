using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Add to any Button to fix the "Selected" visual glitch that occurs when:
///   1. Pointer is pressed down on the button.
///   2. Pointer is dragged off the button while still held.
///   3. Pointer is released outside — the button shows the "Selected" state
///      forever and never fires its onClick.
///
/// With this component the button snaps back to Normal the instant the pointer
/// leaves its bounds while held, and no stale "Selected" highlight remains.
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonDragFix : MonoBehaviour, IPointerDownHandler, IPointerExitHandler
{
    private bool isDown;

    public void OnPointerDown(PointerEventData eventData)
    {
        isDown = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isDown) return;
        isDown = false;

        // Simulate a pointer-up on this object so Unity's Selectable clears its
        // internal isPointerDown flag — this suppresses the "Pressed" → "Selected"
        // transition without firing onClick (which only fires on OnPointerClick).
        ExecuteEvents.Execute<IPointerUpHandler>(
            gameObject, eventData, ExecuteEvents.pointerUpHandler);

        // Clear the EventSystem selection so hasSelection is also false,
        // preventing the "Selected" colour block from showing.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    // Reset internal state on pointer-up in case the pointer was released
    // while still inside the button (normal click path).
    void OnDisable()
    {
        isDown = false;
    }
}
