using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VotingController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;

    [Header("Scoring")]
    public int firstPlacePoints = 3;
    public int secondPlacePoints = 2;
    public int thirdPlacePoints = 1;
    [Header("Local Vote Points (per individual group vote)")]
    public int localFirstPlacePoints = 3;
    public int localSecondPlacePoints = 2;
    public int localThirdPlacePoints = 1;
    public int metricPoints = 3;

    [Header("UI Elements")]
    [SerializeField] private GameObject nextButton;
    [SerializeField] private Image number1;
    [SerializeField] private Image number2;
    [SerializeField] private Image number3;
    [SerializeField] private Image metric1Image;
    [SerializeField] private Image metric2Image;
    [SerializeField] private Sprite comedySprite;
    [SerializeField] private Sprite creativitySprite;
    [SerializeField] private Sprite onTopicSprite;
    [SerializeField] private Sprite factualSprite;
    [SerializeField] private Sprite enthusiasmSprite;
    [SerializeField] private GameObject firstVoteContainer;
    [SerializeField] private GameObject secondVoteContainer;
    [SerializeField] private GameObject thirdVoteContainer;

    [Header("Group Buttons")]
    [SerializeField] private GameObject group1Button;
    [SerializeField] private GameObject group2Button;
    [SerializeField] private GameObject group3Button;
    [SerializeField] private GameObject group4Button;
    [SerializeField] private GameObject group5Button;
    [SerializeField] private GameObject group6Button;

    // Voting phase
    public enum VotingPhase { GroupVoting, DMMetricVoting }
    private VotingPhase currentPhase;

    // Selection tracking — ordered list of selected button slot indices
    private List<int> selectedSlots = new List<int>();
    private int maxSelections;

    // Button management
    private GameObject[] allGroupButtons;
    private Vector2[] cachedButtonPositions;
    private List<Group> activeGroups = new List<Group>();

    void Start()
    {
        gameManager = GameManager.Instance;
        InitButtonArrays();
    }

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (allGroupButtons == null) InitButtonArrays();
        SetupUI();
    }

    private void InitButtonArrays()
    {
        allGroupButtons = new GameObject[]
        {
            group1Button, group2Button, group3Button,
            group4Button, group5Button, group6Button
        };

        cachedButtonPositions = new Vector2[allGroupButtons.Length];
        for (int i = 0; i < allGroupButtons.Length; i++)
        {
            if (allGroupButtons[i] != null)
                cachedButtonPositions[i] = allGroupButtons[i].GetComponent<RectTransform>().anchoredPosition;
        }
    }

    // ===================================================================
    //  SETUP — Called by GameManager before enabling this GameObject
    // ===================================================================

    /// <summary>Prepares the controller for a group's vote.</summary>
    public void PrepareForGroupVoting()
    {
        currentPhase = VotingPhase.GroupVoting;
    }

    /// <summary>Prepares the controller for the DM's metric vote.</summary>
    public void PrepareForDMMetricVoting()
    {
        currentPhase = VotingPhase.DMMetricVoting;
    }

    // ===================================================================
    //  UI SETUP
    // ===================================================================

    private void SetupUI()
    {
        selectedSlots.Clear();
        ResetButtonPositions();

        activeGroups = PlayerManager.groups.Values
            .OrderBy(g => g.id)
            .ToList();
        int groupCount = activeGroups.Count;

        // Configure group buttons — show only the ones that map to a real group
        for (int i = 0; i < allGroupButtons.Length; i++)
        {
            if (i < groupCount)
            {
                allGroupButtons[i].SetActive(true);
                var label = allGroupButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = activeGroups[i].name;
            }
            else
            {
                allGroupButtons[i].SetActive(false);
            }
        }

        if (currentPhase == VotingPhase.GroupVoting)
            SetupGroupVotingUI(groupCount);
        else
            SetupDMMetricVotingUI();

        UpdateNextButton();
    }

    private void SetupGroupVotingUI(int groupCount)
    {
        // Top-2 when only 2 groups; top-3 otherwise
        maxSelections = groupCount <= 2 ? 2 : 3;

        firstVoteContainer.SetActive(true);
        secondVoteContainer.SetActive(true);
        thirdVoteContainer.SetActive(groupCount > 2);

        // Show rank-number images
        if (number1 != null) number1.gameObject.SetActive(true);
        if (number2 != null) number2.gameObject.SetActive(true);
        if (number3 != null) number3.gameObject.SetActive(groupCount > 2);

        // Hide metric images
        if (metric1Image != null) metric1Image.gameObject.SetActive(false);
        if (metric2Image != null) metric2Image.gameObject.SetActive(false);
    }

    private void SetupDMMetricVotingUI()
    {
        maxSelections = 2;

        firstVoteContainer.SetActive(true);
        secondVoteContainer.SetActive(true);
        thirdVoteContainer.SetActive(false);

        // Hide rank-number images
        if (number1 != null) number1.gameObject.SetActive(false);
        if (number2 != null) number2.gameObject.SetActive(false);
        if (number3 != null) number3.gameObject.SetActive(false);

        // Show metric images with the DM's selected metric sprites
        var metrics = gameManager.selectedMetrics;
        if (metric1Image != null && metrics.Count > 0)
        {
            metric1Image.sprite = GetMetricSprite(metrics[0]);
            metric1Image.gameObject.SetActive(true);
        }
        if (metric2Image != null && metrics.Count > 1)
        {
            metric2Image.sprite = GetMetricSprite(metrics[1]);
            metric2Image.gameObject.SetActive(true);
        }
    }

    // ===================================================================
    //  BUTTON POSITION MANAGEMENT
    // ===================================================================

    private void ResetButtonPositions()
    {
        if (cachedButtonPositions == null) return;
        for (int i = 0; i < allGroupButtons.Length; i++)
        {
            if (allGroupButtons[i] != null && i < cachedButtonPositions.Length)
                allGroupButtons[i].GetComponent<RectTransform>().anchoredPosition = cachedButtonPositions[i];
        }
    }

    private void RefreshButtonPositions()
    {
        GameObject[] containers = { firstVoteContainer, secondVoteContainer, thirdVoteContainer };
        for (int i = 0; i < selectedSlots.Count; i++)
        {
            if (i < containers.Length && containers[i] != null && containers[i].activeSelf)
                allGroupButtons[selectedSlots[i]].GetComponent<RectTransform>().anchoredPosition =
                    containers[i].GetComponent<RectTransform>().anchoredPosition;
        }
    }

    // ===================================================================
    //  GROUP BUTTON CALLBACKS — Wire each to its Button.onClick
    // ===================================================================

    public void ToggleGroup1() { ToggleSlot(0); }
    public void ToggleGroup2() { ToggleSlot(1); }
    public void ToggleGroup3() { ToggleSlot(2); }
    public void ToggleGroup4() { ToggleSlot(3); }
    public void ToggleGroup5() { ToggleSlot(4); }
    public void ToggleGroup6() { ToggleSlot(5); }

    private void ToggleSlot(int slot)
    {
        if (slot >= activeGroups.Count) return;

        if (selectedSlots.Contains(slot))
        {
            // Deselect — remove from list and return button to original position
            selectedSlots.Remove(slot);
            if (slot < cachedButtonPositions.Length)
                allGroupButtons[slot].GetComponent<RectTransform>().anchoredPosition = cachedButtonPositions[slot];
        }
        else
        {
            if (selectedSlots.Count >= maxSelections) return;
            selectedSlots.Add(slot);
        }

        RefreshButtonPositions();
        UpdateNextButton();
    }

    // ===================================================================
    //  NEXT BUTTON
    // ===================================================================

    private void UpdateNextButton()
    {
        if (nextButton != null)
        {
            var btn = nextButton.GetComponent<Button>();
            if (btn != null)
                btn.interactable = selectedSlots.Count >= maxSelections;
        }
    }

    public void Next()
    {
        ApplyVotes();

        if (currentPhase == VotingPhase.GroupVoting)
            gameManager.AdvanceVotingSequence();
        else
            gameManager.SetState(GameManager.GameState.Scoreboard);
    }

    // ===================================================================
    //  SCORING
    // ===================================================================

    private void ApplyVotes()
    {
        if (currentPhase == VotingPhase.GroupVoting)
        {
            // Accumulate local vote points — final ranking is resolved in FinalizeGroupVoting()
            int[] points = { localFirstPlacePoints, localSecondPlacePoints, localThirdPlacePoints };
            for (int i = 0; i < selectedSlots.Count && i < points.Length; i++)
            {
                int groupId = activeGroups[selectedSlots[i]].id;
                if (PlayerManager.groups.ContainsKey(groupId))
                    PlayerManager.groups[groupId].votingPhasePoints += points[i];
            }
        }
        else // DM metric voting
        {
            for (int i = 0; i < selectedSlots.Count; i++)
            {
                int groupId = activeGroups[selectedSlots[i]].id;
                if (PlayerManager.groups.ContainsKey(groupId))
                    PlayerManager.groups[groupId].score += metricPoints;
            }
        }
    }

    // ===================================================================
    //  FINALIZE GROUP VOTING
    // ===================================================================

    /// <summary>
    /// Ranks groups by their accumulated votingPhasePoints and awards
    /// the real firstPlacePoints / secondPlacePoints / thirdPlacePoints
    /// to the top-ranked groups. Resets votingPhasePoints afterwards.
    /// Called by GameManager after all groups have voted.
    /// </summary>
    public void FinalizeGroupVoting()
    {
        var rankedGroups = PlayerManager.groups.Values
            .OrderByDescending(g => g.votingPhasePoints)
            .ToList();

        int[] finalPoints = { firstPlacePoints, secondPlacePoints, thirdPlacePoints };
        for (int i = 0; i < rankedGroups.Count && i < finalPoints.Length; i++)
        {
            rankedGroups[i].score += finalPoints[i];
            Debug.Log($"Group '{rankedGroups[i].name}' finished #{i + 1} with {rankedGroups[i].votingPhasePoints} local votes — awarded {finalPoints[i]} points");
        }

        // Reset voting phase points for all groups
        foreach (var group in PlayerManager.groups.Values)
            group.votingPhasePoints = 0;
    }

    // ===================================================================
    //  HELPERS
    // ===================================================================

    private Sprite GetMetricSprite(GameManager.Metric metric)
    {
        return metric switch
        {
            GameManager.Metric.Comedy     => comedySprite,
            GameManager.Metric.Creativity => creativitySprite,
            GameManager.Metric.OnTopic    => onTopicSprite,
            GameManager.Metric.Factual    => factualSprite,
            GameManager.Metric.Enthusiasm => enthusiasmSprite,
            _ => null
        };
    }
}
