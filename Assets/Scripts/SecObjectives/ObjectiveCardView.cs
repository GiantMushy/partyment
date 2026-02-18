using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObjectiveCardView : MonoBehaviour
{
    public ObjectiveDatabase database;

    [Header("Revealed UI")]
    public TMP_Text descriptionText;
    public TMP_Text pointsText;
    public Image roleImage;   //colored icon/image

    public void SetObjective(SecretObjectives obj)
    {
        if (obj == null) return;

        descriptionText.text = obj.description;
        pointsText.text = obj.points.ToString();
        roleImage.sprite = database.GetSprite(obj.role);
    }
}
