using UnityEngine;
using UnityEngine.EventSystems;

public class HoldFlipReveal : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, ICancelHandler
{
    public GameObject holdToReveal;
    public GameObject letGoToUnreveal;

    public float flipDuration = 0.18f;
    public float revealedYRotation = -180f;

    RectTransform rt;
    Coroutine flipRoutine;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
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

    // Called if the touch is cancelled (UI steals focus, etc.)
    public void OnCancel(BaseEventData eventData)
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

            // Swap sides after halfway
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
        var e = rt.localEulerAngles;
        e.y = y;
        rt.localEulerAngles = e;
    }

    void ShowFront()
    {
        holdToReveal.SetActive(true);
        letGoToUnreveal.SetActive(false);
    }

    void ShowBack()
    {
        holdToReveal.SetActive(false);
        letGoToUnreveal.SetActive(true);
    }

    float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    
}
