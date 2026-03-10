using UnityEngine;

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
