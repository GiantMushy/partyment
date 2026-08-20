using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Renders the per-player score breakdown at the end of a round. Each player has three
/// stacked bars (bottom to top): old score, group score, and corruption stacked with
/// stolen points. A top-of-screen phase label and per-player score-incrementer text
/// flash the points being added or removed in each animation phase. The bar container
/// has a fixed pixel height mapped to a fixed <see cref="maxScore"/> (the game's victory
/// threshold): bars never scale past it, so a player who reaches or exceeds
/// <see cref="maxScore"/> has their stack pinned at the top mark while the total counter
/// keeps climbing to break ties. The animation runs through six phases: init, group
/// votes, metric 1, metric 2, corruptions, stolen points, and penalties.
/// </summary>
public class ScoreboardController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;

    [Header("Per-Player Score Bars (index 0..6 = player slot 1..7)")]
    [SerializeField] private List<GameObject>      groupScoreDisplays      = new List<GameObject>(7);
    [Tooltip("Combined Corruption + Stolen bar. Stolen points stack on top of corruption in the same box.")]
    [SerializeField] private List<GameObject>      corruptionScoreDisplays = new List<GameObject>(7);
    [Tooltip("Deprecated. Stolen points are folded into the corruption bar; kept disabled at runtime for legacy prefabs.")]
    [SerializeField] private List<GameObject>      stolenScoreDisplays     = new List<GameObject>(7);
    [SerializeField] private List<GameObject>      oldScoreDisplays        = new List<GameObject>(7);
    [SerializeField] private List<TextMeshProUGUI> totalScoreDisplays      = new List<TextMeshProUGUI>(7);
    [SerializeField] private List<GameObject>      nameDisplays            = new List<GameObject>(7);

    [Header("Score Incrementer (one per player)")]
    [Tooltip("Per-player TMP_Text that flashes the points being added or removed in the current animation phase.")]
    [FormerlySerializedAs("penaltyFloatTexts")]
    [SerializeField] private List<TextMeshProUGUI> scoreIncrementerDisplays = new List<TextMeshProUGUI>(7);

    [Header("Phase Label")]
    [Tooltip("Top-of-screen TMP_Text labelling the current animation phase.")]
    [SerializeField] private TextMeshProUGUI pointTypeDisplay;

    [Header("Scoreboard Background")]
    [Tooltip("VerticalLayoutGroup holding the marker lines. Spacing is rescaled each frame to match the dynamic pixels-per-point.")]
    [SerializeField] private VerticalLayoutGroup scoreboardBackground;
    [Tooltip("Score interval between adjacent marker lines.")]
    [SerializeField] private int markerStepValue = 50;
    [Tooltip("Pixel height of one marker line image. Subtracted from the per-step pixel distance to derive VerticalLayoutGroup spacing.")]
    [SerializeField] private float markerLineHeight = 8f;

    [Header("Round UI")]
    [SerializeField] private TMP_Text roundButtonText;

    [Header("Scale")]
    [Tooltip("Score represented by the full bar height, and the game's victory threshold. Bars never scale past this; a player who exceeds it stays pinned at the top mark while their total counter keeps climbing.")]
    [SerializeField] private int   maxScore = 500;
    [Tooltip("Container height in pixels. If 0, auto-detected from the first assigned group bar's parent.")]
    [SerializeField] private float barContainerHeight = 0f;

    [Header("Animation")]
    [Tooltip("Seconds for ONE earn phase (group-votes, metric1, metric2, corruption, or stolen) to lerp its bar.")]
    [SerializeField] private float perPhaseDuration = 1.4f;
    [Tooltip("Seconds for the 'penalty' phase to deduct points from the group bar.")]
    [SerializeField] private float deductDuration = 1.6f;
    [Tooltip("Pause between phases (during which the Score Incrementer is reset to '+0').")]
    [SerializeField] private float interPhaseDelay = 0.6f;
    [Tooltip("Brief pause after the old-score init before phase ① starts.")]
    [SerializeField] private float initialHoldDelay = 0.4f;
    [Tooltip("Animation speed multiplier applied while the player holds a finger (or mouse button) anywhere on the screen.")]
    [SerializeField] private float holdFastForwardMultiplier = 4f;

    [Header("Phase Labels (override per-language strings here if desired)")]
    [SerializeField] private string groupVotesLabel    = "Group Votes";
    [SerializeField] private string metricLabelFormat  = "Points for {0}";
    [SerializeField] private string corruptionsLabel   = "Corruptions";
    [SerializeField] private string stolenPointsLabel  = "Stolen Points";
    [SerializeField] private string penaltiesLabel     = "Penalties";

    [Header("Victory (final scoreboard only)")]
    [Tooltip("Crown/trophy icon per player slot (index 0..6). Shown only on the winner's column when the game is over.")]
    [SerializeField] private List<GameObject> winnerCrownDisplays = new List<GameObject>(7);
    [Tooltip("CanvasGroup wrapping each player column (index 0..6). Losing columns are faded to Loser Dim Alpha when the game is over.")]
    [SerializeField] private List<CanvasGroup> playerColumnGroups = new List<CanvasGroup>(7);
    [Tooltip("Alpha applied to non-winner columns on the final scoreboard.")]
    [Range(0f, 1f)]
    [SerializeField] private float loserDimAlpha = 0.45f;
    [Tooltip("Victory header shown in the Point Type label once the game is over. {0} = winner's name.")]
    [SerializeField] private string winnerLabelFormat = "{0} Wins!";
    [Tooltip("Vertical bob distance of the leader's crown, in pixels. Kept small for a faint float.")]
    [SerializeField] private float crownBobAmplitude = 6f;
    [Tooltip("Bob speed of the leader's crown, in radians per second.")]
    [SerializeField] private float crownBobSpeed = 2.5f;

    /// <summary>One row of resolved score data per visible player slot.</summary>
    private class PlayerRow
    {
        public Player player;
        public int    groupScore;
        public int    voteScore;
        public int    metric1Score;
        public int    metric2Score;
        public int    oldScore;
        public int    grossTotal;
        public int    actualTotal;
        public int    penalty;
        public int    accusedLoss;
    }

    private readonly List<PlayerRow> rows = new List<PlayerRow>();
    private Coroutine animationRoutine;

    // Crown bob: the leader's crown floats around its authored position. Base positions are
    // captured once so the bob always oscillates around the designer-set offset, not a
    // mid-bob value left over from a previous scoreboard.
    private Coroutine crownBobRoutine;
    private Vector2[] crownBasePos;
    private bool      crownBasesCaptured;

    private float[] animOldVals;
    private float[] animGroupVals;
    private float[] animCorrVals;
    private float[] animStolenVals;
    private int[]   animDisplayedTotals;

    /// <summary>
    /// Per-row negative overflow: amount the deduction phase asked to remove that no
    /// bar could absorb. Subtracted from <see cref="animDisplayedTotals"/> so the
    /// total counter ticks below zero while bars stay flat at 0.
    /// </summary>
    private float[] animOverflowDeduct;

    void Start()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
    }

    private void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        EnsureContainerHeight();
        BuildRows();
        UpdateRoundButton();
        StartScoreboardAnimation();
    }

    private void OnDisable()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }
        StopCrownBob();
        for (int i = 0; i < scoreIncrementerDisplays.Count; i++)
            if (scoreIncrementerDisplays[i] != null)
                scoreIncrementerDisplays[i].gameObject.SetActive(false);
        if (pointTypeDisplay != null) pointTypeDisplay.text = string.Empty;
    }

    /// <summary>
    /// Auto-detects the bar-container height from the first assigned group bar's
    /// parent. Falls back to a default if no bars are wired.
    /// </summary>
    private void EnsureContainerHeight()
    {
        if (barContainerHeight > 0f) return;

        for (int i = 0; i < groupScoreDisplays.Count; i++)
        {
            var bar = groupScoreDisplays[i];
            if (bar == null) continue;
            var parent = bar.transform.parent as RectTransform;
            if (parent != null)
            {
                // Reserves a margin so the total-score TMP_Text at the top of the stack
                // keeps its space.
                barContainerHeight = parent.rect.height * 0.9f;
                if (barContainerHeight > 0f) return;
            }
        }
        barContainerHeight = 1000f;
    }

    /// <summary>
    /// Resolves the score components per non-DM player and writes them into
    /// <see cref="rows"/> in ascending player-ID order.
    /// </summary>
    private void BuildRows()
    {
        rows.Clear();

        int dmId = PlayerManager.dmId;
        var rankedPlayers = PlayerManager.players.Values
            .Where(p => p.id != dmId)
            .OrderBy(p => p.id)
            .ToList();

        foreach (var p in rankedPlayers)
        {
            Group g = (p.group_id >= 0 && PlayerManager.groups.ContainsKey(p.group_id))
                ? PlayerManager.groups[p.group_id]
                : null;

            int groupScore   = g != null ? g.score        : 0;
            int voteScore    = g != null ? g.voteScore    : 0;
            int metric1Score = g != null ? g.metric1Score : 0;
            int metric2Score = g != null ? g.metric2Score : 0;

            // Gross is the sum of all positive bars. Actual is what the player has;
            // p.score already nets penalty and accusedLoss.
            int gross  = p.oldScore + groupScore + p.roundCorruptionScore + p.stolenScore;
            int actual = p.oldScore + groupScore + p.score;
            // penalty: the explicit penaltyScore from an incorrect accusation.
            // accusedLoss: points lost from being correctly accused; not flashed in the
            // penalty phase because the corruption bar keeps showing the gross value.
            // The leftover is parked on the total counter via overflow.
            int penalty     = p.penaltyScore;
            int accusedLoss = Mathf.Max(0, gross - actual - penalty);

            rows.Add(new PlayerRow
            {
                player       = p,
                groupScore   = groupScore,
                voteScore    = voteScore,
                metric1Score = metric1Score,
                metric2Score = metric2Score,
                oldScore     = p.oldScore,
                grossTotal   = gross,
                actualTotal  = actual,
                penalty      = penalty,
                accusedLoss  = accusedLoss,
            });
        }

        // Stop any bob from the previous scoreboard and restore crowns to their base
        // position before this build re-hides them.
        StopCrownBob();

        int n = rows.Count;
        animOldVals          = new float[n];
        animGroupVals        = new float[n];
        animCorrVals         = new float[n];
        animStolenVals       = new float[n];
        animDisplayedTotals  = new int[n];
        animOverflowDeduct   = new float[n];

        // The standalone stolen bar stays hidden; its points are stacked into the
        // corruption bar during the stolen-points phase.
        for (int i = 0; i < 7; i++)
        {
            bool active = i < rows.Count;
            SetActiveSafe(groupScoreDisplays,      i, active);
            SetActiveSafe(corruptionScoreDisplays, i, active);
            SetActiveSafe(stolenScoreDisplays,     i, false);
            SetActiveSafe(nameDisplays,            i, active);
            if (i < totalScoreDisplays.Count && totalScoreDisplays[i] != null)
                totalScoreDisplays[i].gameObject.SetActive(active);

            if (i < scoreIncrementerDisplays.Count && scoreIncrementerDisplays[i] != null)
                scoreIncrementerDisplays[i].gameObject.SetActive(false);

            // Old-score bar is visible from Round 2 onward.
            bool oldActive = active && (gameManager != null && gameManager.currentRound > 1);
            SetActiveSafe(oldScoreDisplays, i, oldActive);

            // Clear any victory treatment from a previous scoreboard; it is re-applied at
            // the end of the animation only when the game is actually over.
            SetActiveSafe(winnerCrownDisplays, i, false);
            if (i < playerColumnGroups.Count && playerColumnGroups[i] != null)
                playerColumnGroups[i].alpha = 1f;
        }

        if (pointTypeDisplay != null) pointTypeDisplay.text = string.Empty;

        for (int i = 0; i < rows.Count; i++)
        {
            if (i < nameDisplays.Count && nameDisplays[i] != null)
            {
                var tmp = nameDisplays[i].GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = rows[i].player.name;
            }
        }
    }

    private void StartScoreboardAnimation()
    {
        if (animationRoutine != null) StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(RunScoreboardAnimation());
    }

    /// <summary>
    /// Drives the multi-phase animation across all visible player rows.
    /// </summary>
    private IEnumerator RunScoreboardAnimation()
    {
        // Init phase: pre-fill old score, all other bars at 0, total = oldScore.
        for (int i = 0; i < rows.Count; i++)
        {
            animOldVals[i]         = rows[i].oldScore;
            animGroupVals[i]       = 0f;
            animCorrVals[i]        = 0f;
            animStolenVals[i]      = 0f;
            animOverflowDeduct[i]  = 0f;
            animDisplayedTotals[i] = rows[i].oldScore;
        }
        SetPointType(string.Empty);
        HideAllIncrementers();
        UpdateBackgroundSpacing();
        ApplyAllBarsAndCounters();

        if (initialHoldDelay > 0f)
            yield return WaitAnimTime(initialHoldDelay);

        int n = rows.Count;
        int[] voteDelta    = new int[n];
        int[] metric1Delta = new int[n];
        int[] metric2Delta = new int[n];
        int[] corrDelta    = new int[n];
        int[] stolenDelta  = new int[n];
        for (int i = 0; i < n; i++)
        {
            voteDelta[i]    = rows[i].voteScore;
            metric1Delta[i] = rows[i].metric1Score;
            metric2Delta[i] = rows[i].metric2Score;
            corrDelta[i]    = rows[i].player.roundCorruptionScore;
            stolenDelta[i]  = rows[i].player.stolenScore;
        }

        // Phase 1: Group Votes (group bar).
        if (HasAny(voteDelta))
        {
            SetPointType(groupVotesLabel);
            SetIncrementersForPhase(voteDelta, isDeduction: false);
            yield return AnimateAddToBar(BarTarget.Group, voteDelta, perPhaseDuration);
            yield return WaitAnimTime(interPhaseDelay);
            HideAllIncrementers();
        }

        // Phase 2: Metric 1 (group bar).
        if (HasAny(metric1Delta))
        {
            SetPointType(string.Format(metricLabelFormat, GetSelectedMetricName(0)));
            SetIncrementersForPhase(metric1Delta, isDeduction: false);
            yield return AnimateAddToBar(BarTarget.Group, metric1Delta, perPhaseDuration);
            yield return WaitAnimTime(interPhaseDelay);
            HideAllIncrementers();
        }

        // Phase 3: Metric 2 (group bar).
        if (HasAny(metric2Delta))
        {
            SetPointType(string.Format(metricLabelFormat, GetSelectedMetricName(1)));
            SetIncrementersForPhase(metric2Delta, isDeduction: false);
            yield return AnimateAddToBar(BarTarget.Group, metric2Delta, perPhaseDuration);
            yield return WaitAnimTime(interPhaseDelay);
            HideAllIncrementers();
        }

        // Phase 4: Corruptions (corruption bar).
        if (HasAny(corrDelta))
        {
            SetPointType(corruptionsLabel);
            SetIncrementersForPhase(corrDelta, isDeduction: false);
            yield return AnimateAddToBar(BarTarget.Corruption, corrDelta, perPhaseDuration);
            yield return WaitAnimTime(interPhaseDelay);
            HideAllIncrementers();
        }

        // Phase 5: Stolen Points (stacked into the corruption bar).
        if (HasAny(stolenDelta))
        {
            SetPointType(stolenPointsLabel);
            SetIncrementersForPhase(stolenDelta, isDeduction: false);
            yield return AnimateAddToBar(BarTarget.Corruption, stolenDelta, perPhaseDuration);
            yield return WaitAnimTime(interPhaseDelay);
            HideAllIncrementers();
        }

        // Phase 6: Penalties. Drains group, corruption, and old in that order; any
        // remainder overflows into the negative counter. Only the incorrect-accusation
        // penalty is animated here; accusedLoss is applied silently below so the final
        // total matches actualTotal without flashing as a deduction.
        int[] penalties = rows.Select(r => r.penalty).ToArray();
        if (HasAny(penalties))
        {
            SetPointType(penaltiesLabel);
            SetIncrementersForPhase(penalties, isDeduction: true);
            yield return AnimateDeductFromBars(penalties, deductDuration);
            HideAllIncrementers();
        }

        // Parks each row's accusedLoss on the overflow counter so the displayed total
        // catches up to actualTotal without flashing a deduction. The corruption bar
        // continues to show the gross corruption value.
        for (int i = 0; i < n; i++)
        {
            if (rows[i].accusedLoss > 0)
            {
                animOverflowDeduct[i] += rows[i].accusedLoss;
                RecomputeRowDisplayedTotal(i);
            }
        }
        ApplyAllBarsAndCounters();

        // Crown the current leader on every scoreboard so players can see who is ahead,
        // not only who has won.
        ShowLeaderCrown();

        // The rest of the victory treatment (winner header + dimmed losers) only lands
        // once the game is actually over.
        if (gameManager != null && gameManager.IsGameOver())
            ShowVictory();

        animationRoutine = null;
    }

    /// <summary>Row index of the player with the highest final total, or -1 if none.</summary>
    private int GetLeadingRowIndex()
    {
        int best = -1;
        int bestScore = int.MinValue;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].actualTotal > bestScore)
            {
                bestScore = rows[i].actualTotal;
                best = i;
            }
        }
        return best;
    }

    /// <summary>
    /// Shows the crown on the current leader (and only the leader) and starts the bob, so
    /// the scoreboard indicates who is ahead every round, not just who has won.
    /// </summary>
    private void ShowLeaderCrown()
    {
        CaptureCrownBasesOnce();

        int leader = GetLeadingRowIndex();
        for (int i = 0; i < winnerCrownDisplays.Count; i++)
            SetActiveSafe(winnerCrownDisplays, i, i == leader);

        if (leader >= 0) StartCrownBob();
    }

    /// <summary>
    /// End-of-game treatment applied on top of the leader crown: rewrites the phase label
    /// to "{winner} Wins!" and dims the losing columns.
    /// </summary>
    private void ShowVictory()
    {
        int winner = GetLeadingRowIndex();
        if (winner < 0) return;

        if (pointTypeDisplay != null && rows[winner].player != null)
            pointTypeDisplay.text = string.Format(winnerLabelFormat, rows[winner].player.name);

        for (int i = 0; i < rows.Count; i++)
        {
            if (i < playerColumnGroups.Count && playerColumnGroups[i] != null)
                playerColumnGroups[i].alpha = (i == winner) ? 1f : loserDimAlpha;
        }
    }

    /// <summary>
    /// Records each crown's authored anchored position once, so the bob always oscillates
    /// around the designer-set offset rather than drifting off a previous bob frame.
    /// </summary>
    private void CaptureCrownBasesOnce()
    {
        if (crownBasesCaptured) return;
        crownBasePos = new Vector2[winnerCrownDisplays.Count];
        for (int i = 0; i < winnerCrownDisplays.Count; i++)
        {
            var rt = winnerCrownDisplays[i] != null
                ? winnerCrownDisplays[i].GetComponent<RectTransform>() : null;
            crownBasePos[i] = rt != null ? rt.anchoredPosition : Vector2.zero;
        }
        crownBasesCaptured = true;
    }

    private void StartCrownBob()
    {
        if (crownBobRoutine != null) StopCoroutine(crownBobRoutine);
        crownBobRoutine = StartCoroutine(CrownBobRoutine());
    }

    /// <summary>Stops the bob and returns every crown to its captured base position.</summary>
    private void StopCrownBob()
    {
        if (crownBobRoutine != null)
        {
            StopCoroutine(crownBobRoutine);
            crownBobRoutine = null;
        }
        if (crownBasePos == null) return;
        for (int i = 0; i < winnerCrownDisplays.Count && i < crownBasePos.Length; i++)
        {
            var rt = winnerCrownDisplays[i] != null
                ? winnerCrownDisplays[i].GetComponent<RectTransform>() : null;
            if (rt != null) rt.anchoredPosition = crownBasePos[i];
        }
    }

    /// <summary>Floats every active crown up and down around its captured base position.</summary>
    private IEnumerator CrownBobRoutine()
    {
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            float offset = Mathf.Sin(t * crownBobSpeed) * crownBobAmplitude;
            for (int i = 0; i < winnerCrownDisplays.Count; i++)
            {
                var go = winnerCrownDisplays[i];
                if (go == null || !go.activeSelf) continue;
                var rt = go.GetComponent<RectTransform>();
                if (rt == null) continue;
                Vector2 basePos = (crownBasePos != null && i < crownBasePos.Length)
                    ? crownBasePos[i] : rt.anchoredPosition;
                rt.anchoredPosition = new Vector2(basePos.x, basePos.y + offset);
            }
            yield return null;
        }
    }

    /// <summary>True while a finger or the left mouse button is held anywhere on screen.</summary>
    private static bool IsPointerHeld
    {
        get
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed) return true;
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }
    }

    /// <summary>Frame delta for the score animation; runs faster while the player holds the screen.</summary>
    private float AnimDeltaTime =>
        Time.unscaledDeltaTime * (IsPointerHeld ? holdFastForwardMultiplier : 1f);

    /// <summary>Waits for <paramref name="seconds"/> of animation time, honouring the hold-to-fast-forward multiplier.</summary>
    private IEnumerator WaitAnimTime(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += AnimDeltaTime;
            yield return null;
        }
    }

    private enum BarTarget { Group, Corruption }

    /// <summary>
    /// Lerps each row's selected bar from its current value up by <paramref name="delta"/>[i]
    /// over <paramref name="duration"/> seconds, ticking the total counter in step.
    /// </summary>
    private IEnumerator AnimateAddToBar(BarTarget target, int[] delta, float duration)
    {
        int n = rows.Count;
        float[] startVals = new float[n];
        for (int i = 0; i < n; i++)
            startVals[i] = (target == BarTarget.Group) ? animGroupVals[i] : animCorrVals[i];

        float t = 0f;
        while (t < duration)
        {
            t += AnimDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = EaseOutCubic(u);
            for (int i = 0; i < n; i++)
            {
                float v = startVals[i] + delta[i] * eased;
                if (target == BarTarget.Group) animGroupVals[i] = v;
                else                           animCorrVals[i]  = v;
                RecomputeRowDisplayedTotal(i);
            }
            ApplyAllBarsAndCounters();
            yield return null;
        }

        for (int i = 0; i < n; i++)
        {
            float v = startVals[i] + delta[i];
            if (target == BarTarget.Group) animGroupVals[i] = v;
            else                           animCorrVals[i]  = v;
            RecomputeRowDisplayedTotal(i);
        }
        ApplyAllBarsAndCounters();
    }

    /// <summary>
    /// Carves <paramref name="deductions"/>[i] off each row over the given duration,
    /// draining bars in priority order: Group, Corruption, then Old. Any excess beyond
    /// the available bar values is recorded in <see cref="animOverflowDeduct"/> so the
    /// displayed total can tick below zero.
    /// </summary>
    private IEnumerator AnimateDeductFromBars(int[] deductions, float duration)
    {
        int n = rows.Count;
        float[] startGroup = new float[n];
        float[] startCorr  = new float[n];
        float[] startOld   = new float[n];
        for (int i = 0; i < n; i++)
        {
            startGroup[i] = animGroupVals[i];
            startCorr[i]  = animCorrVals[i];
            startOld[i]   = animOldVals[i];
        }

        float t = 0f;
        while (t < duration)
        {
            t += AnimDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = EaseInOutCubic(u);
            for (int i = 0; i < n; i++)
            {
                if (deductions[i] <= 0) continue;
                ApplyDeductionCascade(i, deductions[i] * eased, startGroup[i], startCorr[i], startOld[i]);
                RecomputeRowDisplayedTotal(i);
            }
            ApplyAllBarsAndCounters();
            yield return null;
        }

        for (int i = 0; i < n; i++)
        {
            if (deductions[i] <= 0) continue;
            ApplyDeductionCascade(i, deductions[i], startGroup[i], startCorr[i], startOld[i]);
            RecomputeRowDisplayedTotal(i);
        }
        ApplyAllBarsAndCounters();
    }

    /// <summary>
    /// Removes <paramref name="amount"/> points from row <paramref name="i"/> by draining
    /// Group, Corruption, then Old. Any leftover is stored in
    /// <see cref="animOverflowDeduct"/> so the total counter can display a negative value.
    /// </summary>
    private void ApplyDeductionCascade(int i, float amount,
                                       float startGroup, float startCorr, float startOld)
    {
        float remaining = amount;

        float groupAfter = Mathf.Max(0f, startGroup - remaining);
        remaining -= (startGroup - groupAfter);
        animGroupVals[i] = groupAfter;

        float corrAfter = Mathf.Max(0f, startCorr - remaining);
        remaining -= (startCorr - corrAfter);
        animCorrVals[i] = corrAfter;

        float oldAfter = Mathf.Max(0f, startOld - remaining);
        remaining -= (startOld - oldAfter);
        animOldVals[i] = oldAfter;

        animOverflowDeduct[i] = remaining;
    }

    /// <summary>Recomputes <see cref="animDisplayedTotals"/>[i] from the current bar values minus overflow.</summary>
    private void RecomputeRowDisplayedTotal(int i)
    {
        animDisplayedTotals[i] = Mathf.RoundToInt(
            animOldVals[i] + animGroupVals[i] + animCorrVals[i] + animStolenVals[i] - animOverflowDeduct[i]);
    }

    private static bool HasAny(int[] arr)
    {
        for (int i = 0; i < arr.Length; i++) if (arr[i] != 0) return true;
        return false;
    }

    private void SetPointType(string text)
    {
        if (pointTypeDisplay != null) pointTypeDisplay.text = text;
    }

    /// <summary>Hides every player's Score Incrementer (used between phases).</summary>
    private void HideAllIncrementers()
    {
        for (int i = 0; i < scoreIncrementerDisplays.Count; i++)
            if (scoreIncrementerDisplays[i] != null)
                scoreIncrementerDisplays[i].gameObject.SetActive(false);
    }

    /// <summary>Shows row <paramref name="idx"/>'s Score Incrementer with the given text.</summary>
    private void ShowIncrementer(int idx, string text)
    {
        if (idx < 0 || idx >= scoreIncrementerDisplays.Count) return;
        if (scoreIncrementerDisplays[idx] == null) return;
        scoreIncrementerDisplays[idx].text = text;
        scoreIncrementerDisplays[idx].gameObject.SetActive(true);
    }

    /// <summary>
    /// Configures the per-player Score Incrementers for one phase. Only rows with a
    /// non-zero amount are shown. Renders as -N when <paramref name="isDeduction"/>
    /// is true, otherwise as +N.
    /// </summary>
    private void SetIncrementersForPhase(int[] perRowAmount, bool isDeduction)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            int amt = perRowAmount[i];
            if (amt > 0)
                ShowIncrementer(i, isDeduction ? $"-{amt}" : $"+{amt}");
            else if (i < scoreIncrementerDisplays.Count && scoreIncrementerDisplays[i] != null)
                scoreIncrementerDisplays[i].gameObject.SetActive(false);
        }
    }

    private string GetSelectedMetricName(int index)
    {
        if (gameManager == null) return string.Empty;
        var metrics = gameManager.selectedMetrics;
        if (metrics == null || index < 0 || index >= metrics.Count) return string.Empty;
        return metrics[index].ToString();
    }

    /// <summary>Pixels per score-point at the fixed <see cref="maxScore"/> scale.</summary>
    private float PixelsPerPoint =>
        maxScore <= 0 ? 0f : barContainerHeight / maxScore;

    /// <summary>
    /// Sets the scoreboard background's VerticalLayoutGroup spacing so each marker line
    /// sits at its labelled point value. The scale is fixed, so this is computed once.
    /// </summary>
    private void UpdateBackgroundSpacing()
    {
        if (scoreboardBackground == null) return;
        if (maxScore <= 0) return;
        float pxPerStep = markerStepValue * PixelsPerPoint;
        scoreboardBackground.spacing = Mathf.Max(0f, pxPerStep - markerLineHeight);
    }

    /// <summary>
    /// Pushes every row's animated values to the bars and total counters. The stacked
    /// bars (old → group → corruption) are clamped cumulatively to
    /// <see cref="barContainerHeight"/> — the pixel height of <see cref="maxScore"/>
    /// points — so a player past the threshold pins at the top mark. The numeric total
    /// counter is unclamped and keeps climbing, which is what breaks ties above the cap.
    /// </summary>
    private void ApplyAllBarsAndCounters()
    {
        float ppp = PixelsPerPoint;
        for (int i = 0; i < rows.Count; i++)
        {
            // Fill bottom-to-top out of a shared pixel budget so the visible stack never
            // exceeds the container (the maxScore mark), even when the real total does.
            float budget = barContainerHeight;
            float oldH    = ClampBar(animOldVals[i]    * ppp, ref budget);
            float groupH  = ClampBar(animGroupVals[i]  * ppp, ref budget);
            float corrH   = ClampBar(animCorrVals[i]   * ppp, ref budget);
            float stolenH = ClampBar(animStolenVals[i] * ppp, ref budget);

            ApplyBarHeight(oldScoreDisplays,        i, oldH);
            ApplyBarHeight(groupScoreDisplays,      i, groupH);
            ApplyBarHeight(corruptionScoreDisplays, i, corrH);
            ApplyBarHeight(stolenScoreDisplays,     i, stolenH);

            if (i < totalScoreDisplays.Count && totalScoreDisplays[i] != null)
                totalScoreDisplays[i].text = animDisplayedTotals[i].ToString();
        }
    }

    /// <summary>
    /// Returns the drawable height for one stacked segment, capped at the remaining
    /// <paramref name="budget"/> pixels, and subtracts what it took from the budget.
    /// </summary>
    private static float ClampBar(float desired, ref float budget)
    {
        float h = Mathf.Clamp(desired, 0f, Mathf.Max(0f, budget));
        budget -= h;
        return h;
    }

    private static void ApplyBarHeight(List<GameObject> list, int idx, float height)
    {
        if (list == null || idx >= list.Count) return;
        var go = list[idx];
        if (go == null || !go.activeInHierarchy) return;
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(0f, height));
    }

    private static void SetActiveSafe(List<GameObject> list, int idx, bool active)
    {
        if (list == null || idx >= list.Count) return;
        if (list[idx] != null) list[idx].SetActive(active);
    }

    private static float EaseOutCubic(float u)  { float v = 1f - u; return 1f - v * v * v; }
    private static float EaseInOutCubic(float u) =>
        u < 0.5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) * 0.5f;

    private void UpdateRoundButton()
    {
        if (gameManager == null) return;
        if (roundButtonText == null) return;
        // Game over (final round reached OR a player hit the win threshold) turns the
        // advance button into a restart; otherwise it moves to the next round.
        roundButtonText.text = gameManager.IsGameOver() ? "New Game" : "Next Round";
    }

    public void OnRoundButtonClicked()
    {
        if (!gameManager.IsGameOver())
        {
            gameManager.PlayTransition($"Round {gameManager.currentRound + 1}", () =>
            {
                gameManager.StartNextRound();
            });
        }
        else
        {
            gameManager.PlayTransition("Starting New Game!", () =>
            {
                gameManager.NewGame();
            });
        }
    }
}
