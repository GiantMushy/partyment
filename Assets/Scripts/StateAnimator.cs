using UnityEngine;

public class StateAnimator : MonoBehaviour
{
    private Animator animator;

    void Awake()
    {
        // Get the Animator component
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError($"Animator not found on {gameObject.name}");
        }
    }

    void OnEnable()
    {
        if (animator != null)
        {
            animator.Play("Enter");
        }
    }

    void OnDisable()
    {
        if (animator != null)
        {
            animator.Play("Exit");
        }
    }
}