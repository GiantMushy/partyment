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
    [SerializeField] private float holdDuration = 0.5f;

    private Action midTransitionAction;

    /// <summary>
    /// Configures the transition text and the action invoked while the overlay is
    /// fully opaque. Must be called before enabling the GameObject.
    /// </summary>
    public void Setup(string text, Action onMidTransition)
    {
        transitionToText.text = text;
        midTransitionAction = onMidTransition;
    }

    void OnEnable()
    {
        if (transitionCanvasGroup != null)
            transitionCanvasGroup.alpha = 0f;

        StartCoroutine(TransitionEffect());
    }

    private IEnumerator TransitionEffect()
    {
        yield return Fade(0f, 1f);

        // Invoke the state change while the overlay is fully opaque.
        midTransitionAction?.Invoke();
        midTransitionAction = null;

        yield return new WaitForSeconds(holdDuration);

        yield return Fade(1f, 0f);

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