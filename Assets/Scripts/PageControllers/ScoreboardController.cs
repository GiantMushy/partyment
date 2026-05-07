using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Renders the per-player score breakdown at the end of a round.
///
/// Each player has THREE stacked bars (bottom → top, in display order):
///   1. Old Score              — committed totals from previous rounds (hidden in Round 1)
///   2. Group Score            — vote-rank + metric1 + metric2 awards earned this round
///   3. Corruption + Stolen    — gross corruption completed plus points stolen via accusation
///
/// Plus a total-score TMP_Text, a player-name display, and a per-player
/// "Score Incrementer" TMP_Text that flashes the +N (or −N) being added at each phase.
/// A single <see cref="pointTypeDisplay"/> at the top of the screen labels the current
/// phase ("Group Votes", "Points for Comedy", "Corruptions", "Penalties", …).
///
/// ──────────────────────────────────────────────────────────────────
///  Dynamic Scaling
/// ──────────────────────────────────────────────────────────────────
/// The bar container has a fixed pixel height (read from <see cref="barContainerHeight"/>,
/// or auto-detected). The pixels-per-point ratio is recomputed each frame based on
/// the current largest player total — starting at <see cref="initialMaxScore"/> (300)
/// and stepping up by <see cref="maxScoreStep"/> (200) when any player's animated
/// total exceeds the current ceiling.
///
/// The Scoarboard Background (vertical layout of "Marker N" lines) is rescaled in lock
/// step: its <see cref="VerticalLayoutGroup.spacing"/> is recomputed each frame from
/// <c>markerStepValue * pixelsPerPoint − markerLineHeight</c>, so the marker labels stay
/// aligned with the values they advertise no matter how high the ceiling has grown.
///
/// ──────────────────────────────────────────────────────────────────
///  Animation choreography
/// ──────────────────────────────────────────────────────────────────
///   ⓪ Init (instant)        — Old-Score bar pre-fills to oldScore in R2+, total counter
///                              starts at oldScore. All other bars at 0.
///   ① Group Votes           — group bar grows by Group.voteScore.        Point Type = "Group Votes"
///   ② Metric 1              — group bar grows by Group.metric1Score.     Point Type = "Points for {metric1}"
///   ③ Metric 2              — group bar grows by Group.metric2Score.     Point Type = "Points for {metric2}"
///   ④ Corruptions           — corruption bar grows by roundCorruptionScore. Point Type = "Corruptions"
///   ⑤ Stolen Points         — corruption bar continues growing by stolenScore. Point Type = "Stolen Points"
///   ⑥ Penalties (optional)  — for any player whose actual score is below gross, the
///                              deficit is carved off the GROUP bar (then corruption if needed).
///                              Score Incrementer shows "-N". Point Type = "Penalties"
///
/// Each phase shows its per-player +N (or −N) in the player's Score Incrementer slot;
/// inter-phase delays reset the slot back to "+0" so the next phase can flash again.
/// </summary>
public class ScoreboardController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    //  References
    // ─────────────────────────────────────────────────────────────────

    [Header("References")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;

    // ─────────────────────────────────────────────────────────────────
    //  UI Elements
    // ─────────────────────────────────────────────────────────────────

    [Header("Per-Player Score Bars (index 0..6 = player slot 1..7)")]
    [SerializeField] private List<GameObject>      groupScoreDisplays      = new List<GameObject>(7);
    [Tooltip("Combined Corruption + Stolen bar. Stolen points stack on top of corruption in the same box.")]
    [SerializeField] private List<GameObject>      corruptionScoreDisplays = new List<GameObject>(7);
    [Tooltip("DEPRECATED — stolen points are now folded into the corruption bar. Left in case the prefab still references it; always kept disabled at runtime.")]
    [SerializeField] private List<GameObject>      stolenScoreDisplays     = new List<GameObject>(7);
    [SerializeField] private List<GameObject>      oldScoreDisplays        = new List<GameObject>(7);
    [SerializeField] private List<TextMeshProUGUI> totalScoreDisplays      = new List<TextMeshProUGUI>(7);
    [SerializeField] private List<GameObject>      nameDisplays            = new List<GameObject>(7);

    [Header("Score Incrementer (one per player)")]
    [Tooltip("Per-player TMP_Text that flashes '+N' (or '-N' on penalties) for the points being added in the current animation phase. Renamed from the old 'Penalty Score #' boxes.")]
    [FormerlySerializedAs("penaltyFloatTexts")]
    [SerializeField] private List<TextMeshProUGUI> scoreIncrementerDisplays = new List<TextMeshProUGUI>(7);

    [Header("Phase Label")]
    [Tooltip("Top-of-screen TMP_Text labelling the current animation phase (e.g. 'Group Votes', 'Points for Comedy', 'Corruptions', 'Penalties').")]
    [SerializeField] private TextMeshProUGUI pointTypeDisplay;

    [Header("Scoreboard Background")]
    [Tooltip("VerticalLayoutGroup on the 'Scoarboard Background' object holding the marker lines. Its spacing is rescaled to match the dynamic pixels-per-point each frame.")]
    [SerializeField] private VerticalLayoutGroup scoreboardBackground;
    [Tooltip("Score interval between adjacent marker lines (e.g. 50 if markers are at 0, 50, 100, 150…).")]
    [SerializeField] private int markerStepValue = 50;
    [Tooltip("Pixel height of one marker line (the line image itself, not its label). Subtracted from the per-step pixel distance to derive VerticalLayoutGroup spacing.")]
    [SerializeField] private float markerLineHeight = 8f;

    [Header("Round UI")]
    [SerializeField] private TMP_Text roundButtonText;

    // ─────────────────────────────────────────────────────────────────
    //  Scaling
    // ─────────────────────────────────────────────────────────────────

    [Header("Dynamic Scaling")]
    [Tooltip("Initial max-score the bars represent in Round 1. Bar fully fills the container at this value.")]
    [SerializeField] private int   initialMaxScore   = 300;
    [Tooltip("How much the max-score grows when exceeded (300 → 500 → 700 → ...).")]
    [SerializeField] private int   maxScoreStep      = 200;
    [Tooltip("Container height in pixels. If 0, auto-detected from the first assigned group bar's parent.")]
    [SerializeField] private float barContainerHeight = 0f;

    // ─────────────────────────────────────────────────────────────────
    //  Animation
    // ─────────────────────────────────────────────────────────────────

    [Header("Animation")]
    [Tooltip("Seconds for ONE earn phase (group-votes, metric1, metric2, corruption, or stolen) to lerp its bar.")]
    [SerializeField] private float perPhaseDuration = 0.7f;
    [Tooltip("Seconds for the 'penalty' phase to deduct points from the group bar.")]
    [SerializeField] private float deductDuration = 0.8f;
    [Tooltip("Pause between phases (during which the Score Incrementer is reset to '+0').")]
    [SerializeField] private float interPhaseDelay = 0.35f;
    [Tooltip("Brief pause after the old-score init before phase ① starts.")]
    [SerializeField] private float initialHoldDelay = 0.25f;

    [Header("Phase Labels (override per-language strings here if desired)")]
    [SerializeField] private string groupVotesLabel    = "Group Votes";
    [SerializeField] private string metricLabelFormat  = "Points for {0}";
    [SerializeField] private string corruptionsLabel   = "Corruptions";
    [SerializeField] private string stolenPointsLabel  = "Stolen Points";
    [SerializeField] private string penaltiesLabel     = "Penalties";

    // ─────────────────────────────────────────────────────────────────
    //  Runtime State
    // ─────────────────────────────────────────────────────────────────

    /// <summary>One row of resolved score data per visible player slot.</summary>
    private class PlayerRow
    {
        public Player player;
        public int    groupScore;     // total group score (= voteScore + metric1Score + metric2Score)
        public int    voteScore;      // group component: voting-rank points
        public int    metric1Score;   // group component: DM's first metric award
        public int    metric2Score;   // group component: DM's second metric award
        public int    oldScore;
        public int    grossTotal;     // oldScore + group + roundCorruption + stolen
        public int    actualTotal;    // gross − penaltyScore − accusedLoss
        public int    penalty;        // ONLY the incorrect-accusation penalty (= Player.penaltyScore, fixed −20 by default)
        public int    accusedLoss;    // points lost because this player was correctly accused; NOT shown in penalty phase
    }

    private readonly List<PlayerRow> rows = new List<PlayerRow>();
    private Coroutine animationRoutine;
    private int currentMaxScore;

    // Per-row values currently being driven by the animation, used to compute the running max for scaling.
    private float[] animOldVals;
    private float[] animGroupVals;
    private float[] animCorrVals;
    private float[] animStolenVals;
    private int[]   animDisplayedTotals;

    /// <summary>
    /// Per-row "negative overflow": amount the deduction phase asked to remove that
    /// no bar could absorb (because all of old+group+corruption had already been
    /// drained to zero). Subtracted from <see cref="animDisplayedTotals"/> so the
    /// total counter ticks below zero — bars stay flat at 0 since there's nothing
    /// left to drain visually.
    /// </summary>
    private float[] animOverflowDeduct;

    // ─────────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────

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
        // Hide all incrementers so they don't flash a stale value on next entry.
        for (int i = 0; i < scoreIncrementerDisplays.Count; i++)
            if (scoreIncrementerDisplays[i] != null)
                scoreIncrementerDisplays[i].gameObject.SetActive(false);
        if (pointTypeDisplay != null) pointTypeDisplay.text = string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Setup
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Auto-detect the bar-container height from the first assigned group-bar's parent
    /// (so designers don't have to remember to set it). Falls back to a sane default if nothing is wired.
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
                // Auto-detected from the layout group rect. Subtract a margin so the
                // total-score TMP_Text (a sibling at the top of the stack) keeps its space.
                barContainerHeight = parent.rect.height * 0.9f;
                if (barContainerHeight > 0f) return;
            }
        }
        barContainerHeight = 1000f; // legacy default — close to (1100 - text margin)
    }

    /// <summary>
    /// Resolves the four score components per non-DM player and writes them into <see cref="rows"/>,
    /// ordered the same as the existing scoreboard (by player ID ascending).
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

            // Gross = sum of all positive bars. Actual = what the player really has.
            int gross  = p.oldScore + groupScore + p.roundCorruptionScore + p.stolenScore;
            int actual = p.oldScore + groupScore + p.score;       // p.score already nets penalty + accusedLoss
            // Split the loss into its two distinct sources:
            //   • penalty     = the explicit penaltyScore from a WRONG accusation (fixed −20 by default).
            //   • accusedLoss = points this player lost because someone correctly accused THEM. The corruption
            //                   bar deliberately keeps showing gross (CLAUDE.md → Scoring), so we never flash
            //                   accusedLoss in the penalty phase — it's parked on the total counter via overflow.
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

        // Allocate animation-tracking arrays sized for the current row count.
        int n = rows.Count;
        animOldVals          = new float[n];
        animGroupVals        = new float[n];
        animCorrVals         = new float[n];
        animStolenVals       = new float[n];
        animDisplayedTotals  = new int[n];
        animOverflowDeduct   = new float[n];

        // Hide unused slots up front. The standalone stolen bar is always hidden — its
        // points are visualised by extending the corruption bar in phase ⑤.
        for (int i = 0; i < 7; i++)
        {
            bool active = i < rows.Count;
            SetActiveSafe(groupScoreDisplays,      i, active);
            SetActiveSafe(corruptionScoreDisplays, i, active);
            SetActiveSafe(stolenScoreDisplays,     i, false);
            SetActiveSafe(nameDisplays,            i, active);
            if (i < totalScoreDisplays.Count && totalScoreDisplays[i] != null)
                totalScoreDisplays[i].gameObject.SetActive(active);

            // Score Incrementer: hidden by default — only shown for the rows actively
            // gaining/losing points in the current animation phase.
            if (i < scoreIncrementerDisplays.Count && scoreIncrementerDisplays[i] != null)
                scoreIncrementerDisplays[i].gameObject.SetActive(false);

            // Old-score bar: only visible from Round 2 onward, AND only for assigned slots.
            bool oldActive = active && (gameManager != null && gameManager.currentRound > 1);
            SetActiveSafe(oldScoreDisplays, i, oldActive);
        }

        if (pointTypeDisplay != null) pointTypeDisplay.text = string.Empty;

        // Set names once.
        for (int i = 0; i < rows.Count; i++)
        {
            if (i < nameDisplays.Count && nameDisplays[i] != null)
            {
                var tmp = nameDisplays[i].GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = rows[i].player.name;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Animation
    // ─────────────────────────────────────────────────────────────────

    private void StartScoreboardAnimation()
    {
        if (animationRoutine != null) StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(RunScoreboardAnimation());
    }

    /// <summary>
    /// Drives the multi-phase animation across all visible player rows.
    /// See the class summary for the phase order.
    /// </summary>
    private IEnumerator RunScoreboardAnimation()
    {
        // ── ⓪ Init: pre-fill old score, all other bars at 0, total = oldScore ──
        currentMaxScore = initialMaxScore;
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
        RecomputeMaxScore();
        UpdateBackgroundSpacing();
        ApplyAllBarsAndCounters();

        if (initialHoldDelay > 0f)
            yield return new WaitForSecondsRealtime(initialHoldDelay);

        // Build per-row delta arrays for each "earn" phase.
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

        // ── ① Group Votes → group bar ──
        if (HasAny(voteDelta))
        {
            SetPointType(groupVotesLabel);
            SetIncrementersForPhase(voteDelta, isDeduction: false);
            yield return AnimateAddToBar(BarTarget.Group, voteDelta, perPhaseDuration);
            yield return new WaitForSecondsRealtime(interPhaseDelay);
            HideAllIncrementers();
        }

        // ── ② Metric 1 → group bar ──
        if (HasAny(metric1Delta))
        {
            SetPointType(string.Format(metricLabelFormat, GetSelectedMetricName(0)));
            SetIncrementersForPhase(metric1Delta, isDeduction: false);
            yield return AnimateAddToBar(BarTarget.Group, metric1Delta, perPhaseDuration);
            yield return new WaitForSecondsRealtime(interPhaseDelay);
            HideAllIncrementers();
        }

        // ── ③ Metric 2 → group bar ──
        if (HasAny(metric2Delta))
        {
            SetPointType(string.Format(metricLabelFormat, GetSelectedMetricName(1)));
            SetIncrementersForPhase(metric2Delta, isDeduction: false);
            yield return AnimateAddToBar(BarTarget.Group, metric2Delta, perPhaseDuration);
            yield return new WaitForSecondsRealtime(interPhaseDelay);
            HideAllIncrementers();
        }

        // ── ④ Corruptions → corruption bar ──
        if (HasAny(corrDelta))
        {
            SetPointType(corruptionsLabel);
            SetIncrementersForPhase(corrDelta, isDeduction: false);
            yield return AnimateAddToBar(BarTarget.Corruption, corrDelta, perPhaseDuration);
            yield return new WaitForSecondsRealtime(interPhaseDelay);
            HideAllIncrementers();
        }

        // ── ⑤ Stolen Points → corruption bar (stacked on top of corruption) ──
        if (HasAny(stolenDelta))
        {
            SetPointType(stolenPointsLabel);
            SetIncrementersForPhase(stolenDelta, isDeduction: false);
            yield return AnimateAddToBar(BarTarget.Corruption, stolenDelta, perPhaseDuration);
            yield return new WaitForSecondsRealtime(interPhaseDelay);
            HideAllIncrementers();
        }

        // ── ⑥ Penalties → carve out of group → corruption → old, overflow goes negative ──
        // Only the WRONG-accusation penalty (Player.penaltyScore) is animated here — fixed
        // −20 by default. accusedLoss (when this player was caught) is intentionally NOT
        // shown as a deduction; it's applied silently to the overflow counter below so
        // the final total still matches actualTotal.
        int[] penalties = rows.Select(r => r.penalty).ToArray();
        if (HasAny(penalties))
        {
            SetPointType(penaltiesLabel);
            SetIncrementersForPhase(penalties, isDeduction: true);
            yield return AnimateDeductFromBars(penalties, deductDuration);
            HideAllIncrementers();
        }

        // Park each row's accusedLoss on the overflow counter so the displayed total
        // catches up to actualTotal without flashing a deduction. The corruption bar
        // keeps showing the gross corruption, by design.
        for (int i = 0; i < n; i++)
        {
            if (rows[i].accusedLoss > 0)
            {
                animOverflowDeduct[i] += rows[i].accusedLoss;
                RecomputeRowDisplayedTotal(i);
            }
        }
        ApplyAllBarsAndCounters();

        animationRoutine = null;
    }

    private enum BarTarget { Group, Corruption }

    /// <summary>
    /// Lerps each row's selected bar from its current value up by <paramref name="delta"/>[i]
    /// over <paramref name="duration"/> seconds, ticking the total counter and rescaling
    /// the background markers in step.
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
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = EaseOutCubic(u);
            for (int i = 0; i < n; i++)
            {
                float v = startVals[i] + delta[i] * eased;
                if (target == BarTarget.Group) animGroupVals[i] = v;
                else                           animCorrVals[i]  = v;
                RecomputeRowDisplayedTotal(i);
            }
            RecomputeMaxScore();
            UpdateBackgroundSpacing();
            ApplyAllBarsAndCounters();
            yield return null;
        }

        // Snap to exact final values for this phase.
        for (int i = 0; i < n; i++)
        {
            float v = startVals[i] + delta[i];
            if (target == BarTarget.Group) animGroupVals[i] = v;
            else                           animCorrVals[i]  = v;
            RecomputeRowDisplayedTotal(i);
        }
        RecomputeMaxScore();
        UpdateBackgroundSpacing();
        ApplyAllBarsAndCounters();
    }

    /// <summary>
    /// Carves <paramref name="deductions"/>[i] off each row over <paramref name="duration"/>
    /// seconds, draining bars in priority order GROUP → CORRUPTION → OLD. If the requested
    /// deduction exceeds everything the player has, the excess is recorded in
    /// <see cref="animOverflowDeduct"/> so the displayed total counter still ticks below zero
    /// even though no bar can shrink further.
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
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = EaseInOutCubic(u);
            for (int i = 0; i < n; i++)
            {
                if (deductions[i] <= 0) continue;
                ApplyDeductionCascade(i, deductions[i] * eased, startGroup[i], startCorr[i], startOld[i]);
                RecomputeRowDisplayedTotal(i);
            }
            RecomputeMaxScore();
            UpdateBackgroundSpacing();
            ApplyAllBarsAndCounters();
            yield return null;
        }

        // Snap to exact final per-row state.
        for (int i = 0; i < n; i++)
        {
            if (deductions[i] <= 0) continue;
            ApplyDeductionCascade(i, deductions[i], startGroup[i], startCorr[i], startOld[i]);
            RecomputeRowDisplayedTotal(i);
        }
        RecomputeMaxScore();
        UpdateBackgroundSpacing();
        ApplyAllBarsAndCounters();
    }

    /// <summary>
    /// Removes <paramref name="amount"/> points from row <paramref name="i"/> by draining
    /// GROUP → CORRUPTION → OLD in that order; any leftover after all three are exhausted
    /// is recorded as <see cref="animOverflowDeduct"/>[i] so the total counter can show
    /// it as a negative without leaving the bars in a weird state.
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

        animOverflowDeduct[i] = remaining; // ≥ 0 — leftover the bars couldn't absorb
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

    // ─────────────────────────────────────────────────────────────────
    //  Phase Label & Score Incrementer Helpers
    // ─────────────────────────────────────────────────────────────────

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
    /// Configures the per-player Score Incrementers for one phase: visible only on rows
    /// whose amount is non-zero. Pass <paramref name="isDeduction"/> true to render
    /// "-N" (penalty phase); otherwise renders "+N".
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

    // ─────────────────────────────────────────────────────────────────
    //  Bar Sizing & Scaling
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Recomputes <see cref="currentMaxScore"/> by stepping up from the previous value
    /// in <see cref="maxScoreStep"/> increments until the largest currently-displayed
    /// total fits beneath the ceiling. Never shrinks (so the bars don't visually rebound).
    /// </summary>
    private void RecomputeMaxScore()
    {
        int peak = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            int displayed = Mathf.RoundToInt(
                animOldVals[i] + animGroupVals[i] + animCorrVals[i] + animStolenVals[i]);
            if (displayed > peak) peak = displayed;
        }

        while (peak > currentMaxScore)
            currentMaxScore += maxScoreStep;
    }

    /// <summary>Pixels per score-point at the current scale.</summary>
    private float PixelsPerPoint =>
        currentMaxScore <= 0 ? 0f : barContainerHeight / currentMaxScore;

    /// <summary>
    /// Rescales the Scoarboard Background's VerticalLayoutGroup so each marker line
    /// sits at exactly its labelled point value (50, 100, 150, …) under the current
    /// pixels-per-point. Without this, the markers stay calibrated to the original
    /// 500-point ceiling and drift out of sync once the dynamic ceiling grows.
    /// </summary>
    private void UpdateBackgroundSpacing()
    {
        if (scoreboardBackground == null) return;
        if (currentMaxScore <= 0) return;
        float pxPerStep = markerStepValue * PixelsPerPoint;
        scoreboardBackground.spacing = Mathf.Max(0f, pxPerStep - markerLineHeight);
    }

    private void ApplyAllBarsAndCounters()
    {
        float ppp = PixelsPerPoint;
        for (int i = 0; i < rows.Count; i++)
        {
            ApplyBarHeight(oldScoreDisplays,        i, animOldVals[i]    * ppp);
            ApplyBarHeight(groupScoreDisplays,      i, animGroupVals[i]  * ppp);
            ApplyBarHeight(corruptionScoreDisplays, i, animCorrVals[i]   * ppp);
            ApplyBarHeight(stolenScoreDisplays,     i, animStolenVals[i] * ppp);

            if (i < totalScoreDisplays.Count && totalScoreDisplays[i] != null)
                totalScoreDisplays[i].text = animDisplayedTotals[i].ToString();
        }
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

    // ─────────────────────────────────────────────────────────────────
    //  Easing
    // ─────────────────────────────────────────────────────────────────

    private static float EaseOutCubic(float u)  { float v = 1f - u; return 1f - v * v * v; }
    private static float EaseInOutCubic(float u) =>
        u < 0.5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) * 0.5f;

    // ─────────────────────────────────────────────────────────────────
    //  Round Button
    // ─────────────────────────────────────────────────────────────────

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
