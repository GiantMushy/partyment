using UnityEngine;

/// <summary>
/// Plays an Animator's "Enter" state on enable and "Exit" state on disable.
/// The Animator must have states named exactly <c>Enter</c> and <c>Exit</c>.
/// </summary>
public class StateAnimator : MonoBehaviour
{
    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null) Debug.LogError($"Animator not found on {gameObject.name}");
    }

    void OnEnable()  { animator.Play("Enter"); }
    void OnDisable() { animator.Play("Exit"); }
}