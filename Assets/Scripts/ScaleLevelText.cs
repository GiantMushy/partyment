using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScaleLevelText : MonoBehaviour
{
    public Slider slider;
    public TMP_Text levelText;

    [Header("Text for each scale value")]
    public string minusTwoText = "Most of the questions are Political and is for people who don't want as many Fun and Stupid questions.";
    public string minusOneText = "Many of the questions are Political but there are some Fun and Stupid questions.";
    public string zeroText = "A mix of Fun and Political questions for people who want to discuss anything and everything.";
    public string plusOneText = "Many of the Questions are Fun and Stupid but there are some Political questions.";
    public string plusTwoText = "Most of the Questions are Fun and Stupid and is for people who don't want as many Political questions.";

    void Reset()
    {
        slider = GetComponent<Slider>();
    }

    void OnEnable()
    {
        slider.onValueChanged.AddListener(UpdateText);
        UpdateText(slider.value);
    }

    void OnDisable()
    {
        slider.onValueChanged.RemoveListener(UpdateText);
    }

    void UpdateText(float value)
    {
        int v = Mathf.RoundToInt(value);

        switch (v)
        {
            case -2:
                levelText.text = minusTwoText;
                break;
            case -1:
                levelText.text = minusOneText;
                break;
            case 0:
                levelText.text = zeroText;
                break;
            case 1:
                levelText.text = plusOneText;
                break;
            case 2:
                levelText.text = plusTwoText;
                break;
        }
    }
}
