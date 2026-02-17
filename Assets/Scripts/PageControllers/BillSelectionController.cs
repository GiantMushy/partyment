using UnityEngine;

public class BillSelectionController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void BillShort()
    {
        
    }

    public void BillMedium()
    {
        
    }

    public void BillLong()
    {
        
    }

    public void Select()
    {
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }
}
