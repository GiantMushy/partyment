using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Full-screen fade-in / hold / fade-out overlay used between major state changes
/// (start of a new round, new game, etc). Triggered via <see cref="GameManager.PlayTransition"/>:
/// <see cref="Setup"/> stores the action to invoke while the overlay is fully opaque,
/// then enabling the GameObject runs the animation.
/// </summary>
public class TransitionController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private CanvasGroup transitionCanvasGroup;
    [SerializeField] private TextMeshProUGUI transitionToText;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float holdDuration = 1f;

    private Action midTransitionAction;

    /// <summary>
    /// Configures the transition text and the action to run mid-transition
    /// (while the overlay is fully opaque). Call this before enabling the GameObject.
    /// </summary>
    public void Setup(string text, Action onMidTransition)
    {
        transitionToText.text = text;
        midTransitionAction = onMidTransition;
    }

    void OnEnable()
    {
        // Ensure the overlay starts invisible
        if (transitionCanvasGroup != null)
            transitionCanvasGroup.alpha = 0f;

        StartCoroutine(TransitionEffect());
    }

    private IEnumerator TransitionEffect()
    {
        // ---- Fade In ----
        yield return Fade(0f, 1f);

        // ---- Mid-transition: invoke the state change while fully opaque ----
        midTransitionAction?.Invoke();
        midTransitionAction = null;

        // ---- Hold ----
        yield return new WaitForSeconds(holdDuration);

        // ---- Fade Out ----
        yield return Fade(1f, 0f);

        // Deactivate ourselves when done
        gameObject.SetActive(false);
    }

    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        transitionCanvasGroup.alpha = from;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            transitionCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        transitionCanvasGroup.alpha = to;
    }
}