using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Metric = GameManager.Metric;

public class MetricSelectionController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    [SerializeField] private Button comedyMetricButton;
    [SerializeField] private Button creativityMetricButton;
    [SerializeField] private Button onTopicMetricButton;
    [SerializeField] private Button factualMetricButton;
    [SerializeField] private Button enthusiasmMetricButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Transform firstVotePosition;
    [SerializeField] private Transform secondVotePosition;

    // Selection state: ordered list of selected metrics (max 2)
    private List<Metric> selectedMetrics = new List<Metric>();

    // Original positions for each button so they can return when deselected
    private Dictionary<Metric, Vector3> originalPositions = new Dictionary<Metric, Vector3>();

    void Start()
    {
        gameManager = GameManager.Instance;
        CacheOriginalPositions();
    }

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;

        ClearSelection();
    }

    // -------------------- Button Callbacks --------------------

    public void ToggleComedy()      { ToggleMetric(Metric.Comedy); }
    public void ToggleCreativity()  { ToggleMetric(Metric.Creativity); }
    public void ToggleOnTopic()     { ToggleMetric(Metric.OnTopic); }
    public void ToggleFactual()     { ToggleMetric(Metric.Factual); }
    public void ToggleEnthusiasm()  { ToggleMetric(Metric.Enthusiasm); }

    public void Next()
    {
        gameManager.selectedMetrics = new List<Metric>(selectedMetrics);
        gameManager.SetState(GameManager.GameState.AssignPositions);
    }

    public void Back()
    {
        gameManager.SetState(GameManager.GameState.TopicSelection);
    }

    // -------------------- Selection Logic --------------------

    private void ToggleMetric(Metric metric)
    {
        if (IsSelected(metric))
            DeselectMetric(metric);
        else
            SelectMetric(metric);

        RefreshButtonPositions();
        UpdateNextButton();
    }

    private bool IsSelected(Metric metric)
    {
        return selectedMetrics.Contains(metric);
    }

    private void SelectMetric(Metric metric)
    {
        if (selectedMetrics.Count >= 2) return;
        selectedMetrics.Add(metric);
    }

    private void DeselectMetric(Metric metric)
    {
        selectedMetrics.Remove(metric);
        MoveButtonToOriginal(metric);
    }

    private void ClearSelection()
    {
        // Move all currently selected buttons back before clearing
        foreach (var metric in selectedMetrics)
        {
            MoveButtonToOriginal(metric);
        }
        selectedMetrics.Clear();
        UpdateNextButton();
    }

    // -------------------- Position Logic --------------------

    private void CacheOriginalPositions()
    {
        CachePosition(Metric.Comedy, comedyMetricButton);
        CachePosition(Metric.Creativity, creativityMetricButton);
        CachePosition(Metric.OnTopic, onTopicMetricButton);
        CachePosition(Metric.Factual, factualMetricButton);
        CachePosition(Metric.Enthusiasm, enthusiasmMetricButton);
    }

    private void CachePosition(Metric metric, Button button)
    {
        if (button != null)
            originalPositions[metric] = button.transform.position;
    }

    private void RefreshButtonPositions()
    {
        Transform[] voteSlots = { firstVotePosition, secondVotePosition };

        for (int i = 0; i < selectedMetrics.Count; i++)
        {
            MoveButtonToTarget(selectedMetrics[i], voteSlots[i]);
        }
    }

    private void MoveButtonToTarget(Metric metric, Transform target)
    {
        var button = GetButton(metric);
        if (button != null && target != null)
            button.transform.position = target.position;
    }

    private void MoveButtonToOriginal(Metric metric)
    {
        var button = GetButton(metric);
        if (button != null && originalPositions.ContainsKey(metric))
            button.transform.position = originalPositions[metric];
    }

    // -------------------- UI State --------------------

    private void UpdateNextButton()
    {
        if (nextButton != null)
            nextButton.interactable = selectedMetrics.Count == 2;
    }

    // -------------------- Helpers --------------------

    private Button GetButton(Metric metric)
    {
        return metric switch
        {
            Metric.Comedy     => comedyMetricButton,
            Metric.Creativity => creativityMetricButton,
            Metric.OnTopic    => onTopicMetricButton,
            Metric.Factual    => factualMetricButton,
            Metric.Enthusiasm => enthusiasmMetricButton,
            _ => null
        };
    }
}
