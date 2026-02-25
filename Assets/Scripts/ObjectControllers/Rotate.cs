using UnityEngine;

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
