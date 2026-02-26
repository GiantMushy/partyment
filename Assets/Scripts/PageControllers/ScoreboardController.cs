using System.Linq;
using UnityEngine;
using TMPro;

public class ScoreboardController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;
    [SerializeField] private TextMeshProUGUI dataDisplayText;

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;

        RefreshScoreboard();
    }

    private void RefreshScoreboard()
    {
        var lines = PlayerManager.players.Values
            .Select(p =>
            {
                int groupScore = 0;
                if (p.group_id >= 0 && PlayerManager.groups.ContainsKey(p.group_id))
                    groupScore = PlayerManager.groups[p.group_id].score;

                int totalScore = groupScore + p.score;
                return new { p.name, totalScore };
            })
            .OrderByDescending(x => x.totalScore)
            .Select(x => $"{x.name}: {x.totalScore} Points")
            .ToList();

        dataDisplayText.text = string.Join("\n", lines);
    }

    public void NewGame()
    {
        gameManager.NewGame();
    }
}
