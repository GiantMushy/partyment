using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PackItemView : MonoBehaviour
{
    public TMP_Text packNameText;
    public Button button;

    public void Set(string packName)
    {
        if (packNameText != null) packNameText.text = packName;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Debug.Log("Clicked: " + packName));
        }
    }
}
