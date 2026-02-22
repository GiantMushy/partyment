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
    [SerializeField] private GameObject onePlayerGroupPrefab;
    [SerializeField] private GameObject twoPlayerGroupPrefab;
    [SerializeField] private GameObject threePlayerGroupPrefab;
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
            GameObject prefab = GetPrefabForGroupSize(groupPlayers.Count);
            if (prefab == null) continue;

            // Instantiate the For/Against switch above the group display
            GameObject switchObj = Instantiate(switchForAgainstPrefab, groupDisplayParent.transform);
            SetupSwitch(switchObj, group);

            // Instantiate the group display below the switch
            GameObject display = Instantiate(prefab, groupDisplayParent.transform);
            SetGroupLabel(display, group.name);
            SetGroupPlayerNames(display, groupPlayers);
        }
    }

    private GameObject GetPrefabForGroupSize(int size)
    {
        return size switch
        {
            1 => onePlayerGroupPrefab,
            2 => twoPlayerGroupPrefab,
            3 => threePlayerGroupPrefab,
            _ => null
        };
    }

    private void SetGroupLabel(GameObject display, string label)
    {
        Transform labelTransform = display.transform.Find("Label");
        if (labelTransform == null) return;

        Transform titleTransform = labelTransform.Find("Title");
        if (titleTransform == null) return;

        var tmp = titleTransform.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = label;
    }

    private void SetGroupPlayerNames(GameObject display, List<Player> players)
    {
        string[] nameFieldNames = { "First Name Field", "Second Name Field", "Third Name Field" };

        for (int i = 0; i < players.Count && i < nameFieldNames.Length; i++)
        {
            SetPlayerNameField(display, nameFieldNames[i], players[i].name);
        }
    }

    private void SetPlayerNameField(GameObject display, string fieldName, string playerName)
    {
        Transform field = display.transform.Find(fieldName);
        if (field == null) return;

        Transform nameTransform = field.Find("Name");
        if (nameTransform == null) return;

        var tmp = nameTransform.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = playerName;
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
