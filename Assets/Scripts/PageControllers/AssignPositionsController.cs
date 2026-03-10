using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AssignPositionsController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;
    [SerializeField] private GameObject groupDisplayParent;

    [Header("Prefabs")]
    [SerializeField] private GameObject groupContainerPrefab;
    [SerializeField] private GameObject nameInGroupPrefab;
    [SerializeField] private GameObject switchForAgainstPrefab;

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        RandomlyAssignPositions();
        DisplayGroups();
    }

    // -------------------- Button Logic --------------------

    public void Next()
    {
        gameManager.StartSecretObjectiveSequence();
    }

    public void Back()
    {
        gameManager.SetState(GameManager.GameState.MetricSelection);
    }

    // -------------------- Position Assignment --------------------

    private void RandomlyAssignPositions()
    {
        foreach (var group in PlayerManager.groups.Values)
        {
            GameManager.Position randomPosition = Random.value > 0.5f
                ? GameManager.Position.For
                : GameManager.Position.Against;
            PlayerManager.SwapPosition(group.id, randomPosition);
        }
    }

    // -------------------- Display Logic --------------------

    private void DisplayGroups()
    {
        ClearGroupDisplays();
        InstantiateGroupDisplays();
    }

    private void ClearGroupDisplays()
    {
        foreach (Transform child in groupDisplayParent.transform)
        {
            Destroy(child.gameObject);
        }
    }

    private void InstantiateGroupDisplays()
    {
        foreach (var group in PlayerManager.groups.Values)
        {
            var groupPlayers = PlayerManager.GetPlayersWithGroupId(group.id);

            // Instantiate the For/Against switch above the group display
            GameObject switchObj = Instantiate(switchForAgainstPrefab, groupDisplayParent.transform);
            SetupSwitch(switchObj, group);

            // Instantiate the single resizable group container and populate it
            GameObject container = Instantiate(groupContainerPrefab, groupDisplayParent.transform);
            SetGroupLabel(container, group.name);
            foreach (var player in groupPlayers)
                CreateNameCard(player, container.transform);
        }
    }

    private void SetGroupLabel(GameObject container, string label)
    {
        Transform titleTransform = container.transform.Find("Title");
        if (titleTransform == null) return;

        var tmp = titleTransform.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = label;
    }

    private void CreateNameCard(Player player, Transform parent)
    {
        GameObject card = Instantiate(nameInGroupPrefab, parent);

        Transform nameTransform = card.transform.Find("Name");
        if (nameTransform == null) return;

        var tmp = nameTransform.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = player.name;
    }

    // -------------------- Switch Logic --------------------

    private void SetupSwitch(GameObject switchObj, Group group)
    {
        TextMeshProUGUI forText = FindChildTMP(switchObj, "For");
        TextMeshProUGUI againstText = FindChildTMP(switchObj, "Against");
        Button swapButton = FindChildButton(switchObj, "Swap");

        UpdateSwitchVisuals(forText, againstText, group.position);

        if (swapButton != null)
        {
            swapButton.onClick.RemoveAllListeners();
            swapButton.onClick.AddListener(() =>
            {
                GameManager.Position newPosition = group.position == GameManager.Position.For
                    ? GameManager.Position.Against
                    : GameManager.Position.For;
                PlayerManager.SwapPosition(group.id, newPosition);
                UpdateSwitchVisuals(forText, againstText, newPosition);
            });
        }
    }

    private void UpdateSwitchVisuals(TextMeshProUGUI forText, TextMeshProUGUI againstText, GameManager.Position position)
    {
        if (forText != null)
        {
            Color forColor = forText.color;
            forColor.a = position == GameManager.Position.For ? 1f : 0.5f;
            forText.color = forColor;
        }

        if (againstText != null)
        {
            Color againstColor = againstText.color;
            againstColor.a = position == GameManager.Position.Against ? 1f : 0.5f;
            againstText.color = againstColor;
        }
    }

    private TextMeshProUGUI FindChildTMP(GameObject parent, string childName)
    {
        Transform child = parent.transform.Find(childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private Button FindChildButton(GameObject parent, string childName)
    {
        Transform child = parent.transform.Find(childName);
        return child != null ? child.GetComponent<Button>() : null;
    }
}
