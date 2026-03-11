using UnityEngine;
using UnityEngine.Events;

public class OptionToggleController : MonoBehaviour
{
    // Controller for the functionality of a toggle. There are two buttons, one Image that highlights the "selected" button.
    // The buttons are set up so that only one can be selected at a time, and the highlight image moves to the selected button.
    [Header("References")]
    [SerializeField] private GameObject highlightImage;
    [SerializeField] private GameObject option1Button;
    [SerializeField] private GameObject option2Button;

    [Header("Settings")]
    [SerializeField] private float highlightMoveSpeed = 10f;

    [Header("Callbacks")]
    [SerializeField] private UnityEvent onOption1Selected;
    [SerializeField] private UnityEvent onOption2Selected;

    private GameObject currentSelectedButton;
    private Vector3 highlightTargetPosition;

    void Start()
    {
        // Default to option 1 selected
        currentSelectedButton = option1Button;
        MoveHighlightInstantly(currentSelectedButton);
    }

    void Update()
    {
        if (highlightImage != null)
        {
            highlightImage.transform.position = Vector3.Lerp(
                highlightImage.transform.position,
                highlightTargetPosition,
                Time.deltaTime * highlightMoveSpeed
            );
        }
    }

    // -------------------- Button Logic --------------------

    public void Option1()
    {
        if (currentSelectedButton == option1Button) return;
        currentSelectedButton = option1Button;
        highlightTargetPosition = option1Button.transform.position;
        onOption1Selected?.Invoke();
    }

    public void Option2()
    {
        if (currentSelectedButton == option2Button) return;
        currentSelectedButton = option2Button;
        highlightTargetPosition = option2Button.transform.position;
        onOption2Selected?.Invoke();
    }

    // -------------------- Helpers --------------------

    private void MoveHighlightInstantly(GameObject target)
    {
        if (highlightImage == null || target == null) return;
        highlightTargetPosition = target.transform.position;
        highlightImage.transform.position = highlightTargetPosition;
    }

    /// <summary>Returns true if option 1 is currently selected.</summary>
    public bool IsOption1Selected() => currentSelectedButton == option1Button;
}
