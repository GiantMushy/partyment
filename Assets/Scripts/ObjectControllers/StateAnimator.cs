using UnityEngine;

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