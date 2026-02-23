using UnityEngine;
using UnityEngine.UI;

public class PackCarouselForceVisible : MonoBehaviour
{
    public ScrollRect scrollRect;
    public RectTransform content;
    public PackItemView packPrefab;

    void Start()
    {
        if (!scrollRect || !content || !packPrefab)
        {
            Debug.LogError("Missing references on PackCarouselForceVisible.");
            return;
        }

        // Clear old
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        // Spawn 5 visible packs
        for (int i = 1; i <= 5; i++)
        {
            var item = Instantiate(packPrefab, content);
            item.Set("Pack " + i);

            // Click proof
            if (item.button != null)
            {
                int copy = i;
                item.button.onClick.RemoveAllListeners();
                item.button.onClick.AddListener(() => Debug.Log("CLICKED Pack " + copy));
            }
        }

        // Force layout rebuild so ContentSizeFitter updates width
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        // Start at left
        scrollRect.horizontalNormalizedPosition = 0f;
    }
}
