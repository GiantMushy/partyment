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
    [Header("References")]
    [SerializeField] private BillDatabase billDatabase;
    public enum BillType { Short, Medium, Long }

    // Master list — populated by BillDatabase or a real data source
    public List<Bill> allBills = new List<Bill>();

    // Working list — filtered to the currently selected pack
    private List<Bill> currentPackBills = new List<Bill>();

    public Bill currentBill;
    public List<Bill> seenBills = new List<Bill>();

    void Start()
    {
        LoadDevBills();
    }

    public void LoadDevBills()
    {
        allBills = billDatabase.LoadDevBills();
        Debug.Log($"BillManager: Loaded {allBills.Count} dev bills.");
    }

    public void LoadBillsFromPack()
    {
        var pack = GameManager.selectedPack;
        currentPackBills = allBills.Where(b => b.pack == pack).ToList();
        Debug.Log($"Loaded {currentPackBills.Count} bills for pack {pack}");
    }

    public void ResetBillSelection()
    {
        seenBills.Clear();
        currentBill = null;
    }

    // -------------------- Public Getters --------------------

    public Bill GetRandomShortBill(int seriousnessLevel)
    {
        return GetRandomBill(BillType.Short, seriousnessLevel);
    }

    public Bill GetRandomMediumBill(int seriousnessLevel)
    {
        return GetRandomBill(BillType.Medium, seriousnessLevel);
    }

    public Bill GetRandomLongBill(int seriousnessLevel)
    {
        return GetRandomBill(BillType.Long, seriousnessLevel);
    }

    // -------------------- Internal Logic --------------------

    private Bill GetRandomBill(BillType type, int seriousnessLevel)
    {
        var candidates = GetUnseenBills(type, seriousnessLevel);

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"No {type} bills available for seriousness level {seriousnessLevel}.");
            return null;
        }

        var bill = candidates[Random.Range(0, candidates.Count)];
        seenBills.Add(bill);
        return bill;
    }

    private List<Bill> GetUnseenBills(BillType type, int seriousnessLevel)
    {
        return currentPackBills
            .Where(b => b.type == type)
            .Where(b => Mathf.Abs(b.seriousness - seriousnessLevel) <= 1)
            .Where(b => !seenBills.Contains(b))
            .ToList();
    }
}