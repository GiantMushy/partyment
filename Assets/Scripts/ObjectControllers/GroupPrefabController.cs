using UnityEngine;

/// <summary>
/// Unused stub for a per-group container prefab controller. The AssignGroups screen
/// is driven directly by <see cref="AssignGroupsController"/>.
/// </summary>
public class GroupPrefabController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;

    [Header("Prefabs")]
    [SerializeField] private GameObject NameFieldPrefab;
    [SerializeField] private GameObject EmptyFieldPrefab;

    private void Start()
    {
        gameManager = GameManager.Instance;
    }
}
