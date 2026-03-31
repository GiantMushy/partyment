using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
    
public class SettingsController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;

    void Start()
    {
        gameManager = GameManager.Instance;
    }
    public void Back()
    {
        gameManager.BackToSavedState();
    }
    private IEnumerator SetLocale(string code)
    {
        yield return LocalizationSettings.InitializationOperation;

        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);
        if (locale != null)
            LocalizationSettings.SelectedLocale = locale;
        else
            Debug.LogWarning("Locale not found: " + code);
    }

    public void Icelandic()
    {
        gameManager.SetLanguage("is");
        StartCoroutine(SetLocale("is"));
    }

    public void English()
    {
        gameManager.SetLanguage("en");
        StartCoroutine(SetLocale("en"));
    }
}
