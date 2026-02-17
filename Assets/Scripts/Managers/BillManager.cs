using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class Bill
{
    public int id;
    public string title;
    public string description;
    public GameManager.Pack pack;
    public BillManager.BillType type;
    public int seriousness; // Scale of 0-5 for how serious the bill is
    public string leadingQuestionFor;
    public string leadingQuestionAgainst;
}

public class BillManager : MonoBehaviour
{
    public enum BillType { Short, Medium, Long }
    public List<Bill> allBills = new List<Bill>();
    public Bill currentBill;
    public List<Bill> seenBills = new List<Bill>();

    public void LoadBillsFromPack()
    {
        // Placeholder: In a real implementation, this would load from a database or file
        Debug.Log($"Loading bills for pack");
    }

    public void GetThreeRandomBills(GameManager.Pack pack, int seriousnessLevel)
    {
        // Placeholder: In a real implementation, this would filter bills by pack and seriousness, then return 3 random ones
        Debug.Log($"Getting 3 random bills from pack {pack} with seriousness level {seriousnessLevel}");
    }
}