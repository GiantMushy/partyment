using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Snaps a Button back to its Normal visual state when the pointer is dragged off
/// while held, preventing a stale Selected highlight from persisting after release
/// outside the button bounds.
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

        // Synthetic pointer-up clears Selectable.isPointerDown without firing onClick.
        ExecuteEvents.Execute<IPointerUpHandler>(
            gameObject, eventData, ExecuteEvents.pointerUpHandler);

        // Clear EventSystem selection so the Selected colour block does not display.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    void OnDisable()
    {
        isDown = false;
    }
}
