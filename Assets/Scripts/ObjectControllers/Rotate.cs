using UnityEngine;

/// <summary>
/// Spins the GameObject around its Z-axis at a constant rate. Drop on any UI element
/// or sprite that needs a continuous spinning effect (e.g. loading indicators).
/// </summary>
public class Rotate : MonoBehaviour
{
    [SerializeField] private bool clockwise = true;
    [SerializeField] private float speed = 10f;

    void Update()
    {
        float rotationDirection = clockwise ? -1f : 1f;
        transform.Rotate(0f, 0f, rotationDirection * speed * Time.deltaTime);
    }
}
