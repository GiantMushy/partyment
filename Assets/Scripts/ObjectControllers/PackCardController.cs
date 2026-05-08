using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// One card in the Pack Selection carousel. Created from <c>Assets/Prefabs/PackCard.prefab</c>
/// by <see cref="PackSelectionController"/>; the controller positions, scales, and fades
/// instances of this directly via <see cref="Rect"/> and <see cref="CanvasGroup"/>.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class PackCardController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button selectButton;
    [SerializeField] private Image body;          // optional visual; safe to leave unassigned
    [SerializeField] private GameObject lockedOverlay; // optional; shown for packs not in OwnedPacks

    /// <summary>Cached RectTransform for the controller to drive position/scale.</summary>
    public RectTransform Rect { get; private set; }
    /// <summary>Cached CanvasGroup for the controller to fade and disable raycasts.</summary>
    public CanvasGroup CanvasGroup { get; private set; }
    /// <summary>The Pack this card represents. Set by <see cref="Bind"/>.</summary>
    public GameManager.Pack Pack { get; private set; }

    void Awake()
    {
        Rect = (RectTransform)transform;
        CanvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// Configure this card for one pack. Replaces any prior Select listener so calling
    /// <see cref="Bind"/> twice is safe. <paramref name="description"/> may be empty/null;
    /// it's silently skipped if no <c>descriptionText</c> reference is wired.
    /// </summary>
    public void Bind(GameManager.Pack pack, string title, string description, UnityAction onSelect)
    {
        Pack = pack;
        if (titleText != null) titleText.text = title;
        if (descriptionText != null) descriptionText.text = description ?? string.Empty;
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            if (onSelect != null) selectButton.onClick.AddListener(onSelect);
        }
    }

    /// <summary>
    /// Gates the Select button. Used by the controller to lock peek cards
    /// (only the centered card should be selectable) and to disable unowned packs.
    /// </summary>
    public void SetSelectInteractable(bool interactable)
    {
        if (selectButton != null) selectButton.interactable = interactable;
    }

    /// <summary>
    /// Toggles the "Locked" overlay GameObject for unowned packs. The overlay's own
    /// children (badge icon, dim panel, etc.) are designed in the prefab — this just
    /// shows or hides the whole subtree. Safe no-op if no overlay is wired.
    /// </summary>
    public void SetLocked(bool locked)
    {
        if (lockedOverlay != null) lockedOverlay.SetActive(locked);
    }

    /// <summary>Optional helper for future per-pack art or tinting.</summary>
    public void SetBodyColor(Color color)
    {
        if (body != null) body.color = color;
    }
}
