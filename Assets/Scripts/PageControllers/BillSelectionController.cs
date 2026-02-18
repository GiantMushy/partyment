using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BillSelectionController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    [SerializeField] private Button shortBillButton;
    [SerializeField] private Button mediumBillButton;
    [SerializeField] private Button longBillButton;
    [SerializeField] private Button selectButton;

    [Header("Sprites")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;

    [Header("Variables")]
    public Bill shortBill;
    public Bill mediumBill;
    public Bill longBill;
    private Bill selectedBill;
    private Button selectedButton;

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;

        // Guard: skip if GameManager isn't ready yet (initial scene load)
        if (gameManager == null || gameManager.billManager == null)
        {
            Debug.LogWarning("BillSelectionController.OnEnable: GameManager not ready, skipping.");
            return;
        }

        LoadRandomBills();
        PopulateButtonText();
        ClearSelection();

        Debug.Log($"BillSelection OnEnable — short: {shortBill?.title}, medium: {mediumBill?.title}, long: {longBill?.title}");
    }

    // -------------------- Bill Loading --------------------

    private void LoadRandomBills()
    {
        var bm = gameManager.billManager;
        bm.LoadBillsFromPack();

        int seriousness = gameManager.selectedSeriousnessLevel;
        shortBill = bm.GetRandomShortBill(seriousness);
        mediumBill = bm.GetRandomMediumBill(seriousness);
        longBill = bm.GetRandomLongBill(seriousness);
    }

    private void PopulateButtonText()
    {
        SetButtonText(shortBillButton, shortBill);
        SetButtonText(mediumBillButton, mediumBill);
        SetButtonText(longBillButton, longBill);
    }

    private void SetButtonText(Button button, Bill bill)
    {
        if (button == null || bill == null) return;

        var title = button.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
        var description = button.transform.Find("Description")?.GetComponent<TextMeshProUGUI>();

        if (title != null) title.text = bill.title;
        if (description != null) description.text = bill.description;
    }

    // -------------------- Button Callbacks --------------------

    public void BillShort()  { SelectBill(shortBill, shortBillButton); }
    public void BillMedium() { SelectBill(mediumBill, mediumBillButton); }
    public void BillLong()   { SelectBill(longBill, longBillButton); }

    public void Select()
    {
        if (selectedBill == null) return;

        gameManager.billManager.currentBill = selectedBill;
        gameManager.SetState(GameManager.GameState.MetricSelection);
    }

    // -------------------- Selection Logic --------------------

    private void SelectBill(Bill bill, Button button)
    {
        if (bill == null) return;

        selectedBill = bill;
        SetSelectedVisual(button);
    }

    private void ClearSelection()
    {
        selectedBill = null;
        SetSelectedVisual(null);
    }

    private void SetSelectedVisual(Button selected)
    {
        selectedButton = selected;
        ApplyButtonSprite(shortBillButton, selected == shortBillButton);
        ApplyButtonSprite(mediumBillButton, selected == mediumBillButton);
        ApplyButtonSprite(longBillButton, selected == longBillButton);

        if (selectButton != null)
            selectButton.interactable = selected != null;
    }

    private void ApplyButtonSprite(Button button, bool isSelected)
    {
        if (button == null) return;

        var image = button.GetComponent<Image>();
        if (image == null) return;

        Sprite target = isSelected ? selectedSprite : normalSprite;
        if (target != null) image.sprite = target;
    }
}
