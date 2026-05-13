using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class PackCardController : MonoBehaviour
{
    [Header("Pack Data")]
    public GameManager.Pack packType;
    public Color backgroundColor = Color.white;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button selectButton;
    [SerializeField] private Image body;
    [SerializeField] private Image packIcon;
    [SerializeField] private GameObject lockedOverlay;

    public RectTransform Rect { get; private set; }
    public CanvasGroup CanvasGroup { get; private set; }

    void Awake()
    {
        Rect = (RectTransform)transform;
        CanvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// Wires the runtime Select callback without touching any scene-authored text or visuals.
    /// Call this instead of <see cref="Bind"/> when cards are pre-placed in the scene.
    /// </summary>
    public void BindSelectListener(UnityAction onSelect)
    {
        if (selectButton == null) return;
        selectButton.onClick.RemoveAllListeners();
        if (onSelect != null) selectButton.onClick.AddListener(onSelect);
    }

    public void SetSelectInteractable(bool interactable)
    {
        if (selectButton != null) selectButton.interactable = interactable;
    }

    public void SetLocked(bool locked)
    {
        if (lockedOverlay != null) lockedOverlay.SetActive(locked);
    }

    public void SetBodyColor(Color color)
    {
        if (body != null) body.color = color;
    }

    public void SetIcon(Sprite sprite)
    {
        if (packIcon == null || sprite == null) return;
        packIcon.sprite = sprite;
    }
}
