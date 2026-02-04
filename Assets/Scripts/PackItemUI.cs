using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PackItemUI : MonoBehaviour
{
    public Image packImage;
    public TMP_Text titleText;

    [HideInInspector] public PackData data;

    public void Setup(PackData pack)
    {
        data = pack;
        titleText.text = pack.packName;
        packImage.sprite = pack.packSprite;
    }

    public void ClickPack()
    {
        Debug.Log("Chosen pack: " + data.packName);
        // Later: load your next screen here
    }
}
