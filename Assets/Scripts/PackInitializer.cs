using UnityEngine;

public class PackInitializer : MonoBehaviour
{
    public PackData[] packs;
    public PackItemUI[] packItems;

    void Start()
    {
        for (int i = 0; i < packItems.Length && i < packs.Length; i++)
        {
            packItems[i].Setup(packs[i]);
        }
    }
}
