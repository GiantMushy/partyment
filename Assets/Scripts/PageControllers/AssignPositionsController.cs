using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// For/Against assignment screen, shown after Metric Selection. On every entry
/// positions are re-randomised, alternating from a coin-flipped starting side so
/// adjacent groups always have opposing stances. Each group card shows the name,
/// current position, its player roster, and a Swap button that flips that group's
/// stance. Pressing Next invokes <see cref="GameManager.StartCorruptionSequence"/>.
/// </summary>
public class AssignPositionsController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;
    [SerializeField] private GameObject groupDisplayParent;

    [Header("Prefabs")]
    [SerializeField] private GameObject groupPositionPrefab;
    [SerializeField] private GameObject nameInGroupPrefab;

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

    public void Next()
    {
        gameManager.StartCorruptionSequence();
    }

    public void Back()
    {
        gameManager.SetState(GameManager.GameState.MetricSelection);
    }

    private void RandomlyAssignPositions()
    {
        var groups = PlayerManager.groups.Values.OrderBy(g => g.id).ToList();
        if (groups.Count == 0) return;

        GameManager.Position first = Random.value > 0.5f ? GameManager.Position.For : GameManager.Position.Against;
        GameManager.Position current = first;
        for (int i = 0; i < groups.Count; i++)
        {
            PlayerManager.SwapPosition(groups[i].id, current);
            current = current == GameManager.Position.For ? GameManager.Position.Against : GameManager.Position.For;
        }
    }

    private void DisplayGroups()
    {
        foreach (Transform child in groupDisplayParent.transform)
            Destroy(child.gameObject);

        foreach (var group in PlayerManager.groups.Values)
        {
            GameObject container = Instantiate(groupPositionPrefab, groupDisplayParent.transform);
            SetupGroupContainer(container, group);

            foreach (var player in PlayerManager.GetPlayersWithGroupId(group.id))
                CreateNameCard(player, container.transform);
        }
    }

    private void SetupGroupContainer(GameObject container, Group group)
    {
        var nameTMP = FindTMP(container, "Group Name Container/Name");
        if (nameTMP != null) nameTMP.text = group.name;

        var positionTMP = FindTMP(container, "Group Name Container/Position");
        if (positionTMP != null) positionTMP.text = PositionLabel(group.position);

        var swapButton = FindButton(container, "Group Name Container/Swap Button");
        if (swapButton != null)
        {
            swapButton.onClick.RemoveAllListeners();
            swapButton.onClick.AddListener(() =>
            {
                GameManager.Position newPosition = group.position == GameManager.Position.For
                    ? GameManager.Position.Against
                    : GameManager.Position.For;
                PlayerManager.SwapPosition(group.id, newPosition);
                if (positionTMP != null) positionTMP.text = PositionLabel(newPosition);
            });
        }
    }

    private void CreateNameCard(Player player, Transform parent)
    {
        GameObject card = Instantiate(nameInGroupPrefab, parent);
        Transform nameTransform = card.transform.Find("Name");
        if (nameTransform == null) return;
        var tmp = nameTransform.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = player.name;
    }

    private static string PositionLabel(GameManager.Position position)
        => position == GameManager.Position.For ? "For" : "Against";

    private TextMeshProUGUI FindTMP(GameObject root, string path)
    {
        Transform t = root.transform.Find(path);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    private Button FindButton(GameObject root, string path)
    {
        Transform t = root.transform.Find(path);
        return t != null ? t.GetComponent<Button>() : null;
    }
}
