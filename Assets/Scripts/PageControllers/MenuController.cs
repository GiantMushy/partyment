using UnityEngine;
using System.Collections;

public class MenuController : MonoBehaviour
{
    private GameManager gameManager;

    private RectTransform parentTransform;
    private Vector3 closedPosition;
    private Vector3 openPosition;
    [SerializeField] private float transitionSpeed = 5f;
    [SerializeField] private GameObject backdrop; // Full-screen transparent panel behind the menu

    private bool isMenuOpen = false;

    // Awake is called when the script instance is being loaded
    void Start()
    {
        gameManager = GameManager.Instance;
        parentTransform = GetComponent<RectTransform>();

        // Set initial positions for open and closed states
        closedPosition = parentTransform.localPosition;
        openPosition = new Vector3(500, parentTransform.localPosition.y, 0);

        if (backdrop != null) backdrop.SetActive(false);
    }

    public void ToggleMenu()
    {
        Debug.Log("Menu Toggle Button Pressed");

        // Toggle the menu state
        isMenuOpen = !isMenuOpen;
        gameManager.menuOpen = isMenuOpen;

        // Show/hide backdrop
        if (backdrop != null) backdrop.SetActive(isMenuOpen);

        // Start the smooth transition
        StopAllCoroutines();
        StartCoroutine(SmoothMove(isMenuOpen ? openPosition : closedPosition));
    }

    /// <summary>
    /// Called by the backdrop's Button OnClick — closes the menu if open.
    /// </summary>
    public void CloseFromBackdrop()
    {
        if (isMenuOpen) ToggleMenu();
    }

    public void NewGame()
    {
        ToggleMenu();
        gameManager.NewGame();
    }

    public void Settings()
    {
        ToggleMenu();
        gameManager.OpenSettings();
    }

    public void Rulebook()
    {
        ToggleMenu();
        gameManager.OpenRulebook();
    }

    public void Feedback()
    {
        ToggleMenu();
        gameManager.OpenFeedbackForm();
    }

    public void OpenDataPrivacyPage()
    {
        ToggleMenu();
        gameManager.OpenDataPrivacyPage();
    }

    private IEnumerator SmoothMove(Vector3 targetPosition)
    {
        while (Vector3.Distance(parentTransform.localPosition, targetPosition) > 0.01f)
        {
            parentTransform.localPosition = Vector3.Lerp(parentTransform.localPosition, targetPosition, Time.deltaTime * transitionSpeed);
            yield return null;
        }

        parentTransform.localPosition = targetPosition;
    }
}