using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent toggle-state visual for a <see cref="Button"/>.
///
/// Unity's built-in "Selected" EventSystem state drops the moment the user clicks anything
/// else on screen, making it unsuitable for latching toggle UI. This component instead
/// overrides the Button's <c>normalColor</c> in its <see cref="ColorBlock"/>: because the
/// Button's own transition logic always returns to <c>normalColor</c> after Highlighted /
/// Pressed interactions, the tint persists until the code explicitly clears it.
///
/// Usage:
///   1. Add this component to any Button GameObject (requires Button).
///   2. Set <see cref="toggledColor"/> in the Inspector (or leave the default dark tint).
///   3. Call <see cref="SetToggled"/> from your controller. Only one button in a group
///      should be toggled on at a time — the controller is responsible for mutual exclusion.
///
/// Attach to: Play / Pause / Stop buttons in DMDisplay; Versus / Scenarios in TopicSelection.
/// </summary>
[RequireComponent(typeof(Button))]
public class ToggleButton : MonoBehaviour
{
    [Tooltip("The color applied to the button's normal state when toggled on.")]
    [SerializeField] private Color toggledColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    /// <summary>The underlying Button — use for onClick wiring in code.</summary>
    public Button Button { get; private set; }

    public bool IsToggled { get; private set; }

    private Color _defaultNormalColor;

    void Awake()
    {
        Button = GetComponent<Button>();
        _defaultNormalColor = Button.colors.normalColor;
    }

    /// <summary>
    /// Latches or releases the toggled-on visual. Changing the Button's normalColor means
    /// the tint survives all interaction states (Hover, Press) without any per-frame upkeep.
    /// </summary>
    public void SetToggled(bool on)
    {
        IsToggled = on;
        var colors = Button.colors;
        colors.normalColor = on ? toggledColor : _defaultNormalColor;
        Button.colors = colors;
    }
}
