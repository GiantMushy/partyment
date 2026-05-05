using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ScoreboardController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;
    
    [Header("UI Elements")]
    
    [SerializeField] private List<GameObject> groupScoreDisplays = new List<GameObject>(7);
    [SerializeField] private List<GameObject> corruptionScoreDisplays = new List<GameObject>(7);
    [SerializeField] private List<TextMeshProUGUI> totalScoreDisplays = new List<TextMeshProUGUI>(7);
    [SerializeField] private List<GameObject> nameDisplays = new List<GameObject>(7);

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    // Remove duplicate OnEnable above

    private void RefreshScoreboard()
    {
        int dmId = PlayerManager.dmId;

        var rankedPlayers = PlayerManager.players.Values
            .Where(p => p.id != dmId)
            .OrderBy(p => p.id)
            .ToList();

        for (int i = 0; i < 7; i++)
        {
            if (i < rankedPlayers.Count)
            {
                var p = rankedPlayers[i];

                int groupScore = 0;
                if (p.group_id >= 0 && PlayerManager.groups.ContainsKey(p.group_id))
                    groupScore = PlayerManager.groups[p.group_id].score;
                int corruptionScore = p.score;

                // Resize group score bar (height = groupScore * 2)
                if (i < groupScoreDisplays.Count && groupScoreDisplays[i] != null)
                {
                    var rt = groupScoreDisplays[i].GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(rt.sizeDelta.x, groupScore * 2);
                    groupScoreDisplays[i].SetActive(true);
                }

                // Resize corruption score bar (height = corruptionScore * 2)
                if (i < corruptionScoreDisplays.Count && corruptionScoreDisplays[i] != null)
                {
                    var rt = corruptionScoreDisplays[i].GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(rt.sizeDelta.x, corruptionScore * 2);
                    corruptionScoreDisplays[i].SetActive(true);
                }

                // Set total score text
                if (i < totalScoreDisplays.Count && totalScoreDisplays[i] != null)                {
                    totalScoreDisplays[i].text = (groupScore + corruptionScore).ToString();
                    totalScoreDisplays[i].gameObject.SetActive(true);
                }

                // Set player name
                if (i < nameDisplays.Count && nameDisplays[i] != null)
                {
                    var tmp = nameDisplays[i].GetComponent<TMP_Text>();
                    if (tmp != null) tmp.text = p.name;
                    nameDisplays[i].SetActive(true);
                }
            }
            else
            {
                // Hide unused slots
                if (i < groupScoreDisplays.Count && groupScoreDisplays[i] != null)
                    groupScoreDisplays[i].SetActive(false);
                if (i < corruptionScoreDisplays.Count && corruptionScoreDisplays[i] != null)
                    corruptionScoreDisplays[i].SetActive(false);
                if (i < totalScoreDisplays.Count && totalScoreDisplays[i] != null)
                    totalScoreDisplays[i].gameObject.SetActive(false);
                if (i < nameDisplays.Count && nameDisplays[i] != null)
                    nameDisplays[i].SetActive(false);
            }
        }
    }


    [Header("Round UI")]
    [SerializeField] private TMP_Text roundButtonText;

    private void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        RefreshScoreboard();
        UpdateRoundButton();
    }

    private void UpdateRoundButton()
    {
        if (gameManager == null) return;
        if (roundButtonText == null) return;
        if (gameManager.currentRound < gameManager.totalRounds)
            roundButtonText.text = "Next Round";
        else if (gameManager.currentRound == gameManager.totalRounds)
            roundButtonText.text = "Finish Game";
        else
            roundButtonText.text = "New Game";
    }

    public void OnRoundButtonClicked()
    {
        if (gameManager.currentRound < gameManager.totalRounds)
        {
            gameManager.PlayTransition($"Round {gameManager.currentRound + 1}", () =>
            {
                gameManager.StartNextRound();
            });
        }
        else if (gameManager.currentRound == gameManager.totalRounds)
        {
            gameManager.PlayTransition("Starting New Game!", () =>
            {
                gameManager.NewGame();
            });
        }
    }
}
