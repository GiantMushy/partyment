using UnityEngine.UI;

/// <summary>
/// A Button subclass that adds a persistent latching toggle state driven by the
/// button's own existing <b>Selected</b> animator / transition state.
///
/// The problem with <c>button.Select()</c>: Unity's EventSystem calls
/// <c>DoStateTransition(Normal)</c> the moment anything else is clicked, which
/// immediately exits the Selected animation. This subclass intercepts that call
/// and redirects Normal → Selected whenever <see cref="IsToggled"/> is true, so
/// the Selected state (and all its sprite / position / colour changes) persists
/// until the code explicitly releases it. Hover and Press animations are unaffected.
///
/// Setup:
///   • Remove the existing <c>Button</c> component from each toggle button and replace
///     it with this component. All Inspector onClick wiring must be re-connected after
///     swapping the component.
///   • No new Animator states or parameters are required — the existing "Selected"
///     state is reused as-is.
/// </summary>
public class ToggleButton : Button
{
    public bool IsToggled { get; private set; }

    /// <summary>
    /// Latches or releases the toggle visual immediately, using the button's existing
    /// Selected state. The visual persists through any subsequent user interaction.
    /// </summary>
    public void SetToggled(bool on)
    {
        IsToggled = on;
        DoStateTransition(on ? SelectionState.Selected : SelectionState.Normal, false);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        // While toggled on, swallow any attempt to return to Normal (e.g. EventSystem
        // deselect after clicking elsewhere) and stay in Selected instead.
        if (IsToggled && state == SelectionState.Normal)
            state = SelectionState.Selected;

        base.DoStateTransition(state, instant);
    }
}
