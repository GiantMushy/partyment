using UnityEngine.UI;

/// <summary>
/// Button subclass that latches the existing Selected animator state until
/// <see cref="SetToggled"/> releases it. Any transition back to Normal while
/// toggled on is redirected to Selected. Hover and Press transitions are unaffected.
/// </summary>
public class ToggleButton : Button
{
    public bool IsToggled { get; private set; }

    /// <summary>Latches or releases the Selected visual state immediately.</summary>
    public void SetToggled(bool on)
    {
        IsToggled = on;
        DoStateTransition(on ? SelectionState.Selected : SelectionState.Normal, false);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        if (IsToggled && state == SelectionState.Normal)
            state = SelectionState.Selected;

        base.DoStateTransition(state, instant);
    }
}
