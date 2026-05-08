using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ButtonDragFixInstaller
{
    [MenuItem("Tools/Partyment/Add ButtonDragFix to All Buttons in Scene")]
    private static void AddToAllButtons()
    {
        var buttons = Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int added   = 0;
        int skipped = 0;

        foreach (var btn in buttons)
        {
            if (btn.GetComponent<ButtonDragFix>() != null) { skipped++; continue; }
            Undo.AddComponent<ButtonDragFix>(btn.gameObject);
            added++;
        }

        Debug.Log($"[ButtonDragFix] Added to {added} button(s). {skipped} already had it.");
    }
}
