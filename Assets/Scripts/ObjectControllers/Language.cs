using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.Collections;

public class LanguageButtons : MonoBehaviour
{
    public void SetIcelandic() => StartCoroutine(SetLocale("is"));
    public void SetEnglish()   => StartCoroutine(SetLocale("en"));

    private IEnumerator SetLocale(string code)
    {
        yield return LocalizationSettings.InitializationOperation;

        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);
        if (locale != null)
            LocalizationSettings.SelectedLocale = locale;
        else
            Debug.LogWarning("Locale not found: " + code);
    }
}