using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Slide-out side menu. Owns all in-menu interactables: three animated dropdowns
/// (New Game confirmation, Language selection, Rulebook), a sound icon toggle,
/// and the Feedback / Data Privacy links. No longer navigates to separate screens.
/// </summary>
public class MenuController : MonoBehaviour
{
    private GameManager gameManager;

    private RectTransform parentTransform;
    private Vector3 menuClosedPosition;
    private Vector3 menuOpenPosition;
    [SerializeField] private float transitionSpeed = 5f;
    [SerializeField] private GameObject backdrop;

    [Header("Toggle Button Slide")]
    [SerializeField] private RectTransform menuToggleButton;
    [SerializeField] private float buttonSlideDistance = 30f;

    [Header("Dropdown Content Areas")]
    [SerializeField] private RectTransform languageContent;
    [SerializeField] private RectTransform languageContentInner;
    [SerializeField] private RectTransform rulebookContent;
    [SerializeField] private RectTransform rulebookContentInner;
    [SerializeField] private ScrollRect rulebookScrollRect;
    [SerializeField] private RectTransform newGameContent;
    [SerializeField] private RectTransform newGameContentInner;
    [SerializeField] private float dropdownOpenDuration = 0.2f;
    [SerializeField] private float dropdownCloseDuration = 0.15f;

    [Header("Sound Toggle")]
    [SerializeField] private Image soundToggleImage;
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;

    [Header("Language Selector")]
    [SerializeField] private RectTransform languageSelector;
    [SerializeField] private RectTransform englishButtonRect;
    [SerializeField] private RectTransform icelandicButtonRect;
    [SerializeField] private float selectorLerpSpeed = 5f;

    private Vector3 buttonClosedLocalPos;
    private bool isMenuOpen = false;

    private float langH, rulebookH, newGameH;
    private bool langOpen, rulebookOpen, newGameOpen;
    private Coroutine langAnim, rulebookAnim, newGameAnim;
    private bool soundEnabled = true;
    private Coroutine selectorAnim;

    void Start()
    {
        gameManager = GameManager.Instance;
        parentTransform = GetComponent<RectTransform>();

        menuClosedPosition = parentTransform.localPosition;
        menuOpenPosition = new Vector3(500, parentTransform.localPosition.y, 0);

        if (menuToggleButton != null)
            buttonClosedLocalPos = menuToggleButton.localPosition;

        if (backdrop != null) backdrop.SetActive(false);

        langH     = languageContent != null ? languageContent.sizeDelta.y  : 200f;
        rulebookH = rulebookContent != null ? rulebookContent.sizeDelta.y  : 200f;
        newGameH  = newGameContent  != null ? newGameContent.sizeDelta.y   : 200f;
        CloseAllInstant();
    }

    public void ToggleMenu()
    {
        Debug.Log("Menu Toggle Button Pressed");

        isMenuOpen = !isMenuOpen;
        gameManager.menuOpen = isMenuOpen;

        if (backdrop != null) backdrop.SetActive(isMenuOpen);

        StopAllCoroutines();
        selectorAnim = null;
        System.Action onSlideComplete = isMenuOpen ? null : (System.Action)CloseAllInstant;
        StartCoroutine(SmoothMove(isMenuOpen ? menuOpenPosition : menuClosedPosition, onSlideComplete));

        if (menuToggleButton != null)
        {
            Vector3 buttonTarget = isMenuOpen
                ? buttonClosedLocalPos + new Vector3(0, -buttonSlideDistance, 0)
                : buttonClosedLocalPos;
            StartCoroutine(SmoothMoveButton(buttonTarget));
        }
    }

    public void CloseFromBackdrop()
    {
        if (isMenuOpen) ToggleMenu();
    }

    // --- Dropdowns ---

    public void ToggleLanguageDropdown()
    {
        if (langOpen)
        {
            if (langAnim != null) StopCoroutine(langAnim);
            langAnim = StartCoroutine(CloseDropdown(languageContent, languageContentInner, langH, () => langAnim = null));
            langOpen = false;
        }
        else
        {
            CloseOtherDropdowns(closeLanguage: false);
            // Expand to full height so the VerticalLayoutGroup computes correct button
            // world positions, snap the selector, then reset to 0 for the open animation.
            // Everything runs in one frame before rendering — no visual flash.
            languageContent.gameObject.SetActive(true);
            languageContent.sizeDelta = new Vector2(languageContent.sizeDelta.x, langH);
            Canvas.ForceUpdateCanvases();
            SnapSelectorInstant();
            languageContent.sizeDelta = new Vector2(languageContent.sizeDelta.x, 0f);
            langAnim = StartCoroutine(OpenDropdown(languageContent, languageContentInner, langH, () => langAnim = null));
            langOpen = true;
        }
    }

    public void ToggleRulebookDropdown()
    {
        if (rulebookOpen)
        {
            if (rulebookAnim != null) StopCoroutine(rulebookAnim);
            rulebookAnim = StartCoroutine(CloseDropdown(rulebookContent, rulebookContentInner, rulebookH, () => rulebookAnim = null));
            rulebookOpen = false;
        }
        else
        {
            CloseOtherDropdowns(closeRulebook: false);
            if (rulebookScrollRect != null)
                rulebookScrollRect.verticalNormalizedPosition = 1f;
            rulebookAnim = StartCoroutine(OpenDropdown(rulebookContent, rulebookContentInner, rulebookH, () => rulebookAnim = null));
            rulebookOpen = true;
        }
    }

    public void ToggleNewGameDropdown()
    {
        if (newGameOpen)
        {
            if (newGameAnim != null) StopCoroutine(newGameAnim);
            newGameAnim = StartCoroutine(CloseDropdown(newGameContent, newGameContentInner, newGameH, () => newGameAnim = null));
            newGameOpen = false;
        }
        else
        {
            CloseOtherDropdowns(closeNewGame: false);
            newGameAnim = StartCoroutine(OpenDropdown(newGameContent, newGameContentInner, newGameH, () => newGameAnim = null));
            newGameOpen = true;
        }
    }

    public void ConfirmNewGame()
    {
        CloseAllInstant();
        ToggleMenu();
        gameManager.PlayTransition("Lets try this again...", () => gameManager.NewGame());
    }

    // --- Sound ---

    public void ToggleSound()
    {
        soundEnabled = !soundEnabled;
        if (soundToggleImage != null)
            soundToggleImage.sprite = soundEnabled ? soundOnSprite : soundOffSprite;
    }

    // --- Language ---

    public void SetEnglish()
    {
        gameManager.SetLanguage("en");
        StartCoroutine(SetLocale("en"));
        MoveSelectorTo(englishButtonRect);
    }

    public void SetIcelandic()
    {
        gameManager.SetLanguage("is");
        StartCoroutine(SetLocale("is"));
        MoveSelectorTo(icelandicButtonRect);
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

    // --- Language Selector ---

    private void SnapSelectorInstant()
    {
        if (languageSelector == null) return;
        RectTransform target = gameManager.selectedLanguage == GameManager.Language.Icelandic
            ? icelandicButtonRect : englishButtonRect;
        if (target != null)
            languageSelector.anchoredPosition = SelectorTargetPos(target);
    }

    private void MoveSelectorTo(RectTransform target)
    {
        if (languageSelector == null || target == null) return;
        if (selectorAnim != null) StopCoroutine(selectorAnim);
        selectorAnim = StartCoroutine(LerpSelector(target));
    }

    private IEnumerator LerpSelector(RectTransform target)
    {
        while (true)
        {
            Vector2 goal = SelectorTargetPos(target);
            if (Vector2.Distance(languageSelector.anchoredPosition, goal) < 0.5f)
            {
                languageSelector.anchoredPosition = goal;
                break;
            }
            languageSelector.anchoredPosition = Vector2.Lerp(
                languageSelector.anchoredPosition,
                goal,
                Time.deltaTime * selectorLerpSpeed
            );
            yield return null;
        }
        selectorAnim = null;
    }

    private Vector2 SelectorTargetPos(RectTransform buttonRect)
    {
        Vector3 local = languageContentInner.InverseTransformPoint(buttonRect.position);
        // languageContentInner's pivot is at (0.5, 0) — bottom-centre, not middle-centre.
        // anchoredPosition on a child anchored at (0.5, 0.5) is measured from the visual
        // centre, so subtract the gap between the pivot row and the anchor row.
        float anchorOffsetY = (0.5f - languageContentInner.pivot.y) * languageContentInner.rect.height;
        return new Vector2(languageSelector.anchoredPosition.x, local.y - anchorOffsetY);
    }

    // --- External links (kept for existing scene wiring) ---

    public void Feedback()
    {
        ToggleMenu();
        gameManager.OpenFeedbackForm();
    }

    public void OpenDataPrivacyPage()
    {
        ToggleMenu();
        gameManager.OpenDataPrivacyPage();
    }

    // --- Private helpers ---

    private void CloseAllInstant()
    {
        SetContentInstant(languageContent, languageContentInner);
        langOpen = false; langAnim = null;
        SetContentInstant(rulebookContent, rulebookContentInner);
        rulebookOpen = false; rulebookAnim = null;
        SetContentInstant(newGameContent, newGameContentInner);
        newGameOpen = false; newGameAnim = null;
    }

    private void SetContentInstant(RectTransform outer, RectTransform inner)
    {
        if (outer == null) return;
        outer.sizeDelta = new Vector2(outer.sizeDelta.x, 0f);
        outer.gameObject.SetActive(false);
        if (inner != null)
            inner.anchoredPosition = new Vector2(inner.anchoredPosition.x, 0f);
    }

    private void CloseOtherDropdowns(bool closeLanguage = true, bool closeRulebook = true, bool closeNewGame = true)
    {
        if (closeLanguage && langOpen)
        {
            if (langAnim != null) StopCoroutine(langAnim);
            langAnim = StartCoroutine(CloseDropdown(languageContent, languageContentInner, langH, () => langAnim = null));
            langOpen = false;
        }
        if (closeRulebook && rulebookOpen)
        {
            if (rulebookAnim != null) StopCoroutine(rulebookAnim);
            rulebookAnim = StartCoroutine(CloseDropdown(rulebookContent, rulebookContentInner, rulebookH, () => rulebookAnim = null));
            rulebookOpen = false;
        }
        if (closeNewGame && newGameOpen)
        {
            if (newGameAnim != null) StopCoroutine(newGameAnim);
            newGameAnim = StartCoroutine(CloseDropdown(newGameContent, newGameContentInner, newGameH, () => newGameAnim = null));
            newGameOpen = false;
        }
    }

    private IEnumerator OpenDropdown(RectTransform outer, RectTransform inner, float targetH, System.Action onDone)
    {
        outer.gameObject.SetActive(true);
        outer.sizeDelta = new Vector2(outer.sizeDelta.x, 0f);
        if (inner != null) inner.anchoredPosition = new Vector2(inner.anchoredPosition.x, 0f);

        float elapsed = 0f;
        while (elapsed < dropdownOpenDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dropdownOpenDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
            outer.sizeDelta = new Vector2(outer.sizeDelta.x, Mathf.Lerp(0f, targetH, eased));
            yield return null;
        }
        outer.sizeDelta = new Vector2(outer.sizeDelta.x, targetH);
        onDone?.Invoke();
    }

    private IEnumerator CloseDropdown(RectTransform outer, RectTransform inner, float fromH, System.Action onDone)
    {
        float elapsed = 0f;

        while (elapsed < dropdownCloseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dropdownCloseDuration);
            float eased = t * t * t; // ease-in cubic (accelerates into close)
            float currentH = Mathf.Lerp(fromH, 0f, eased);
            outer.sizeDelta = new Vector2(outer.sizeDelta.x, currentH);
            // Scroll content upward so raise button rides to the top and disappears behind the button above
            if (inner != null)
                inner.anchoredPosition = new Vector2(inner.anchoredPosition.x, Mathf.Lerp(0f, fromH, eased));
            yield return null;
        }

        outer.sizeDelta = new Vector2(outer.sizeDelta.x, 0f);
        outer.gameObject.SetActive(false);
        if (inner != null) inner.anchoredPosition = new Vector2(inner.anchoredPosition.x, 0f);
        onDone?.Invoke();
    }

    private IEnumerator SmoothMove(Vector3 targetPosition, System.Action onComplete = null)
    {
        while (Vector3.Distance(parentTransform.localPosition, targetPosition) > 0.01f)
        {
            parentTransform.localPosition = Vector3.Lerp(parentTransform.localPosition, targetPosition, Time.deltaTime * transitionSpeed);
            yield return null;
        }
        parentTransform.localPosition = targetPosition;
        onComplete?.Invoke();
    }

    private IEnumerator SmoothMoveButton(Vector3 targetPosition)
    {
        while (Vector3.Distance(menuToggleButton.localPosition, targetPosition) > 0.01f)
        {
            menuToggleButton.localPosition = Vector3.Lerp(menuToggleButton.localPosition, targetPosition, Time.deltaTime * transitionSpeed);
            yield return null;
        }
        menuToggleButton.localPosition = targetPosition;
    }
}
