using UnityEngine;
using System.Collections;

public class MenuController : MonoBehaviour
{
    private GameManager gameManager;

    private RectTransform parentTransform;
    private Vector3 closedPosition;
    private Vector3 openPosition;
    [SerializeField] private float transitionSpeed = 5f;

    private bool isMenuOpen = false;

    // Awake is called when the script instance is being loaded
    void Start()
    {
        gameManager = GameManager.Instance;
        parentTransform = GetComponent<RectTransform>();

        // Set initial positions for open and closed states
        closedPosition = parentTransform.localPosition;
        openPosition = new Vector3(500, parentTransform.localPosition.y, 0);
    }

    public void ToggleMenu()
    {
        Debug.Log("Menu Toggle Button Pressed");

        // Toggle the menu state
        isMenuOpen = !isMenuOpen;
        gameManager.menuOpen = isMenuOpen;

        // Start the smooth transition
        StopAllCoroutines();
        StartCoroutine(SmoothMove(isMenuOpen ? openPosition : closedPosition));
    }

    public void NewGame()
    {
        ToggleMenu();
        gameManager.BackToPackSelect();
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