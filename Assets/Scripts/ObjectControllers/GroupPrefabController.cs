using UnityEngine;

/// <summary>
/// ⚠️ <b>Empty stub — currently unused.</b> Was intended as a per-group container prefab
/// controller for the AssignGroups screen, but that flow is now driven directly by
/// <see cref="AssignGroupsController"/>. Safe to delete unless you plan to migrate the
/// group-card composition logic out of <c>AssignGroupsController</c> into this prefab.
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
