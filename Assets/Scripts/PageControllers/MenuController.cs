using UnityEngine;
using System.Collections;

/// <summary>
/// Slide-out side menu available from anywhere in the game. Toggled by a corner button
/// that itself slides slightly while the menu is open. Sets <see cref="GameManager.menuOpen"/>
/// while open so <see cref="GameManager.SetState"/> blocks state changes underneath.
/// Menu items: New Game, Settings, Rulebook, Feedback, Data Privacy.
/// </summary>
public class MenuController : MonoBehaviour
{
    private GameManager gameManager;

    private RectTransform parentTransform;
    private Vector3 menuClosedPosition;
    private Vector3 menuOpenPosition;
    [SerializeField] private float transitionSpeed = 5f;
    [SerializeField] private GameObject backdrop; // Full-screen transparent panel behind the menu

    [Header("Toggle Button Slide")]
    [SerializeField] private RectTransform menuToggleButton; // Assign the MenuToggleButton child
    [SerializeField] private float buttonSlideDistance = 30f; // How far the button slides down (in pixels) — tweak this!

    private Vector3 buttonClosedLocalPos;

    private bool isMenuOpen = false;

    void Start()
    {
        gameManager = GameManager.Instance;
        parentTransform = GetComponent<RectTransform>();

        // Set initial positions for open and closed states
        menuClosedPosition = parentTransform.localPosition;

        menuOpenPosition = new Vector3(500, parentTransform.localPosition.y, 0);

        if (menuToggleButton != null)
            buttonClosedLocalPos = menuToggleButton.localPosition;

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
        StartCoroutine(SmoothMove(isMenuOpen ? menuOpenPosition : menuClosedPosition));

        // Slide the toggle button down/up
        if (menuToggleButton != null)
        {
            Vector3 buttonTarget = isMenuOpen
                ? buttonClosedLocalPos + new Vector3(0, -buttonSlideDistance, 0)
                : buttonClosedLocalPos;
            StartCoroutine(SmoothMoveButton(buttonTarget));
        }
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
        gameManager.PlayTransition("Lets try this again...", () =>
        {
            gameManager.NewGame();
        });
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

    private IEnumerator SmoothMoveButton(Vector3 targetPosition)
    {
        while (Vector3.Distance(menuToggleButton.localPosition, targetPosition) > 0.01f)
        {
            menuToggleButton.localPosition = Vector3.Lerp(menuToggleButton.localPosition, targetPosition, Time.deltaTime * transitionSpeed);
            yield return null;
        }

        menuToggleButton.localPosition = targetPosition;
    }
}