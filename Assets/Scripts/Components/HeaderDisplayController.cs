using UnityEngine;

/// <summary>
/// Shows the child header image that matches the current language, hides all others.
/// Expected child names: "Header EN", "Header IS" (case-insensitive suffix match).
/// Falls back to the first child containing "EN" when no match is found.
/// </summary>
public class HeaderDisplayController : MonoBehaviour
{
    void OnEnable()
    {
        GameManager.OnLanguageChanged += ApplyLanguage;
        ApplyLanguage();
    }

    void OnDisable()
    {
        GameManager.OnLanguageChanged -= ApplyLanguage;
    }

    void ApplyLanguage()
    {
        string code = GetLanguageCode();
        Transform fallback = null;
        Transform match = null;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            string upper = child.name.ToUpper();

            if (upper.Contains(code))
                match = child;

            if (upper.Contains("EN") && fallback == null)
                fallback = child;

            child.gameObject.SetActive(false);
        }

        Transform toShow = match != null ? match : fallback;
        if (toShow != null)
            toShow.gameObject.SetActive(true);
        else
            Debug.LogWarning($"HeaderDisplayController on '{gameObject.name}': no header child found for language '{code}' and no EN fallback.");
    }

    static string GetLanguageCode()
    {
        if (GameManager.Instance == null) return "EN";
        return GameManager.Instance.selectedLanguage == GameManager.Language.Icelandic ? "IS" : "EN";
    }
}
