using UnityEngine;

public class PackSelectionController : MonoBehaviour
{
    private GameManager gameManager;
    private BillManager BillManager => gameManager.billManager;

    // Awake is called when the script instance is being loaded
    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void Default()
    {
        gameManager.SetPack(GameManager.Pack.Default);
        BillManager.LoadBillsFromPack();
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }

    public void EighteenPlus()
    {
        gameManager.SetPack(GameManager.Pack.EighteenPlus);
        BillManager.LoadBillsFromPack();
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }

    public void Icelandic()
    {
        gameManager.SetPack(GameManager.Pack.Icelandic);
        BillManager.LoadBillsFromPack();
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }

    public void Political()
    {
        gameManager.SetPack(GameManager.Pack.Political);
        BillManager.LoadBillsFromPack();
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }

    public void PopCulture()
    {
        gameManager.SetPack(GameManager.Pack.PopCulture);
        BillManager.LoadBillsFromPack();
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }

}
