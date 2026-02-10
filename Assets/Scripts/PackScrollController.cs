using UnityEngine;
using UnityEngine.UI;

public class PackScrollController : MonoBehaviour
{
    [Header("Scene refs")]
    public RectTransform content;
    public Image localBackground; // THIS screen's background only

    [Header("Packs")]
    public PackItemUI[] packItems;

    [Header("Tuning")]
    public float colorLerpSpeed = 6f;
    public float scaleLerpSpeed = 10f;
    public float selectedScale = 1.1f;
    public float unselectedScale = 0.9f;

    int selectedIndex = 0;

    void Update()
    {
        if (!gameObject.activeInHierarchy) return;

        // Find which pack is closest to center of the viewport (x=0 in content local space)
        float closest = float.MaxValue;

        for (int i = 0; i < packItems.Length; i++)
        {
            float x = content.InverseTransformPoint(packItems[i].transform.position).x;
            float dist = Mathf.Abs(x);

            if (dist < closest)
            {
                closest = dist;
                selectedIndex = i;
            }
        }

        // Background changes ONLY on this screen
        var targetColor = packItems[selectedIndex].data.backgroundColor;
        localBackground.color = Color.Lerp(localBackground.color, targetColor, Time.deltaTime * colorLerpSpeed);

        // Optional: scale selected pack bigger (Pokémon-like feel)
        for (int i = 0; i < packItems.Length; i++)
        {
            float targetScale = (i == selectedIndex) ? selectedScale : unselectedScale;
            Vector3 s = Vector3.one * targetScale;
            packItems[i].transform.localScale = Vector3.Lerp(packItems[i].transform.localScale, s, Time.deltaTime * scaleLerpSpeed);
        }
    }
}
