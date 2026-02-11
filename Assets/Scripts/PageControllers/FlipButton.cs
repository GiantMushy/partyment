using UnityEngine;
using UnityEngine.EventSystems;

public class HoldFlipReveal : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Assign your sides")]
    public GameObject holdToReveal;       // front
    public GameObject letGoToUnreveal;    // back

    [Header("Flip settings")]
    public float flipDuration = 0.18f;    // sekúndur
    public float revealedYRotation = -180f; // flip "vinstri" (negative)

    RectTransform rt;
    Coroutine flipRoutine;

    void Awake()
    {
        rt = GetComponent<RectTransform>();

        // Start on "front"
        SetRotationY(0f);
        ShowFront();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        FlipTo(revealedYRotation);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        FlipTo(0f);
    }

    // If finger drags off the button, treat it like letting go
    public void OnPointerExit(PointerEventData eventData)
    {
        FlipTo(0f);
    }

    void FlipTo(float targetY)
    {
        if (flipRoutine != null) StopCoroutine(flipRoutine);
        flipRoutine = StartCoroutine(FlipCoroutine(targetY));
    }

    System.Collections.IEnumerator FlipCoroutine(float targetY)
    {
        float startY = NormalizeAngle(rt.localEulerAngles.y);
        float endY = targetY;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, flipDuration);
            float y = Mathf.LerpAngle(startY, endY, Mathf.SmoothStep(0f, 1f, t));
            SetRotationY(y);

            // Swap which face is visible when passing the halfway point (90 degrees)
            float absFromFront = Mathf.Abs(Mathf.DeltaAngle(0f, y));
            if (absFromFront < 90f) ShowFront();
            else ShowBack();

            yield return null;
        }

        SetRotationY(endY);

        float finalAbsFromFront = Mathf.Abs(Mathf.DeltaAngle(0f, endY));
        if (finalAbsFromFront < 90f) ShowFront();
        else ShowBack();

        flipRoutine = null;
    }

    void SetRotationY(float y)
    {
        Vector3 e = rt.localEulerAngles;
        e.y = y;
        rt.localEulerAngles = e;
    }

    void ShowFront()
    {
        if (holdToReveal) holdToReveal.SetActive(true);
        if (letGoToUnreveal) letGoToUnreveal.SetActive(false);
    }

    void ShowBack()
    {
        if (holdToReveal) holdToReveal.SetActive(false);
        if (letGoToUnreveal) letGoToUnreveal.SetActive(true);
    }

    float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
