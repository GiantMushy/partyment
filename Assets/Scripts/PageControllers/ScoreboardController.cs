using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the per-player score breakdown at the end of a round.
///
/// Each player has up to FOUR stacked bars (bottom → top, in display order):
///   1. Old Score        — committed totals from previous rounds (hidden in Round 1)
///   2. Group Score      — current round's group performance
///   3. Corruption Score — current round's corruption completion (gross — never decreased)
///   4. Stolen Score     — points stolen via correct accusations
///
/// Plus a total-score TMP_Text and a player-name display.
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
/// ──────────────────────────────────────────────────────────────────
///  Animation
/// ──────────────────────────────────────────────────────────────────
/// On enable, every player's bars animate from a starting state up to their
/// final values:
///   • Round 1 → all bars start at 0.
///   • Round 2+ → Old-Score bar pre-fills to oldScore; the other three start at 0.
///
/// The animation has two phases:
///   ① "Earn" phase  — bars grow up to the GROSS earned values
///                     (oldScore + group + roundCorruptionScore + stolenScore).
///                     The total-score counter ticks up at the same rate.
///   ② "Deduct" phase — for any player whose actual <c>score</c> is lower than the
///                     gross (because they were correctly accused or paid a penalty),
///                     the deficit is removed from the top of the stack and a
///                     floating "-N" tag flashes briefly.
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
    [SerializeField] private List<GameObject>      corruptionScoreDisplays = new List<GameObject>(7);
    [Tooltip("Optional — assign in Inspector. Leave empty until the bar GameObjects are added to the prefab/scene.")]
    [SerializeField] private List<GameObject>      stolenScoreDisplays     = new List<GameObject>(7);
    [Tooltip("Optional — assign in Inspector. Leave empty until the bar GameObjects are added to the prefab/scene.")]
    [SerializeField] private List<GameObject>      oldScoreDisplays        = new List<GameObject>(7);
    [SerializeField] private List<TextMeshProUGUI> totalScoreDisplays      = new List<TextMeshProUGUI>(7);
    [SerializeField] private List<GameObject>      nameDisplays            = new List<GameObject>(7);

    [Header("Floating Penalty/Loss Text (one per player; optional)")]
    [Tooltip("Shown briefly while a player's deduction is animating. Format: '-12'.")]
    [SerializeField] private List<TextMeshProUGUI> penaltyFloatTexts       = new List<TextMeshProUGUI>(7);

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
    [Tooltip("Seconds for the 'earn' phase to fully fill from start values to gross totals.")]
    [SerializeField] private float earnDuration   = 1.6f;
    [Tooltip("Seconds for the 'deduct' phase to remove penalties from the top of the stack.")]
    [SerializeField] private float deductDuration = 0.8f;
    [Tooltip("Pause between the earn phase and the deduct phase.")]
    [SerializeField] private float interPhaseDelay = 0.35f;
    [Tooltip("How long the '-N' floating text remains visible while the deduction animates.")]
    [SerializeField] private float floatTextDuration = 1.1f;

    // ─────────────────────────────────────────────────────────────────
    //  Runtime State
    // ─────────────────────────────────────────────────────────────────

    /// <summary>One row of resolved score data per visible player slot.</summary>
    private class PlayerRow
    {
        public Player player;
        public int    groupScore;
        public int    oldScore;
        public int    grossTotal;   // oldScore + group + roundCorruption + stolen   (post-animation phase ①)
        public int    actualTotal;  // gross − penalty − accusedLoss                  (post-animation phase ②)
        public int    deduction;    // grossTotal − actualTotal (≥ 0)
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
        // Hide all floating texts so they don't flash on next entry.
        for (int i = 0; i < penaltyFloatTexts.Count; i++)
            if (penaltyFloatTexts[i] != null) penaltyFloatTexts[i].gameObject.SetActive(false);
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
            int groupScore = (p.group_id >= 0 && PlayerManager.groups.ContainsKey(p.group_id))
                ? PlayerManager.groups[p.group_id].score
                : 0;

            // Gross = sum of the four positive bars. Actual = what the player really has.
            int gross  = p.oldScore + groupScore + p.roundCorruptionScore + p.stolenScore;
            int actual = p.oldScore + groupScore + p.score;       // p.score already nets penalty + accusedLoss
            int deduction = Mathf.Max(0, gross - actual);

            rows.Add(new PlayerRow
            {
                player      = p,
                groupScore  = groupScore,
                oldScore    = p.oldScore,
                grossTotal  = gross,
                actualTotal = actual,
                deduction   = deduction,
            });
        }

        // Allocate animation-tracking arrays sized for the current row count.
        int n = rows.Count;
        animOldVals          = new float[n];
        animGroupVals        = new float[n];
        animCorrVals         = new float[n];
        animStolenVals       = new float[n];
        animDisplayedTotals  = new int[n];

        // Hide unused slots up front.
        for (int i = 0; i < 7; i++)
        {
            bool active = i < rows.Count;
            SetActiveSafe(groupScoreDisplays,      i, active);
            SetActiveSafe(corruptionScoreDisplays, i, active);
            SetActiveSafe(stolenScoreDisplays,     i, active);
            SetActiveSafe(nameDisplays,            i, active);
            if (i < totalScoreDisplays.Count && totalScoreDisplays[i] != null)
                totalScoreDisplays[i].gameObject.SetActive(active);
            if (i < penaltyFloatTexts.Count && penaltyFloatTexts[i] != null)
                penaltyFloatTexts[i].gameObject.SetActive(false);

            // Old-score bar: only visible from Round 2 onward, AND only for assigned slots.
            bool oldActive = active && (gameManager != null && gameManager.currentRound > 1);
            SetActiveSafe(oldScoreDisplays, i, oldActive);
        }

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
    /// Drives the two-phase animation across all visible player rows.
    /// Phase 1 ramps every bar up to its gross value; phase 2 removes any deduction
    /// from the top of the stack and shows a floating "-N" tag.
    /// </summary>
    private IEnumerator RunScoreboardAnimation()
    {
        // ── Initial state: bars at start values ───────────────────────
        currentMaxScore = initialMaxScore;
        for (int i = 0; i < rows.Count; i++)
        {
            // Round 1 → all bars start at 0. Round 2+ → old-score bar pre-fills.
            animOldVals[i]    = rows[i].oldScore;
            animGroupVals[i]  = 0f;
            animCorrVals[i]   = 0f;
            animStolenVals[i] = 0f;
            animDisplayedTotals[i] = rows[i].oldScore;
        }
        RecomputeMaxScore();
        ApplyAllBarsAndCounters();

        // ── Phase ①: earn ─────────────────────────────────────────────
        float t = 0f;
        while (t < earnDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / earnDuration);
            float eased = EaseOutCubic(u);

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                animOldVals[i]    = r.oldScore; // already at full from frame 0
                animGroupVals[i]  = Mathf.Lerp(0f, r.groupScore,                eased);
                animCorrVals[i]   = Mathf.Lerp(0f, r.player.roundCorruptionScore, eased);
                animStolenVals[i] = Mathf.Lerp(0f, r.player.stolenScore,        eased);
                animDisplayedTotals[i] = Mathf.RoundToInt(
                    animOldVals[i] + animGroupVals[i] + animCorrVals[i] + animStolenVals[i]);
            }

            RecomputeMaxScore();
            ApplyAllBarsAndCounters();
            yield return null;
        }

        // Snap to exact gross values to avoid sub-pixel drift.
        for (int i = 0; i < rows.Count; i++)
        {
            animGroupVals[i]  = rows[i].groupScore;
            animCorrVals[i]   = rows[i].player.roundCorruptionScore;
            animStolenVals[i] = rows[i].player.stolenScore;
            animDisplayedTotals[i] = rows[i].grossTotal;
        }
        RecomputeMaxScore();
        ApplyAllBarsAndCounters();

        // ── Pause if anyone has a deduction; otherwise we're done ─────
        bool anyDeduction = rows.Any(r => r.deduction > 0);
        if (!anyDeduction)
        {
            animationRoutine = null;
            yield break;
        }

        yield return new WaitForSecondsRealtime(interPhaseDelay);

        // Show the floating "-N" tags right as the deduct phase starts.
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].deduction <= 0) continue;
            ShowFloatingPenalty(i, rows[i].deduction);
        }

        // ── Phase ②: deduct ───────────────────────────────────────────
        // Each row's deduction is "carved off" the top of its stack. The carve order
        // is stolen → corruption → group → old (top to bottom) so the visual feels
        // like the topmost coloured bar drains away first.
        float[] startStolen = new float[rows.Count];
        float[] startCorr   = new float[rows.Count];
        float[] startGroup  = new float[rows.Count];
        float[] startOld    = new float[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            startStolen[i] = animStolenVals[i];
            startCorr[i]   = animCorrVals[i];
            startGroup[i]  = animGroupVals[i];
            startOld[i]    = animOldVals[i];
        }

        t = 0f;
        while (t < deductDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / deductDuration);
            float eased = EaseInOutCubic(u);

            for (int i = 0; i < rows.Count; i++)
            {
                int totalDeduct = rows[i].deduction;
                if (totalDeduct <= 0) continue;
                float deductSoFar = totalDeduct * eased;
                ApplyDeductionTopDown(i, deductSoFar, startStolen[i], startCorr[i], startGroup[i], startOld[i]);
                animDisplayedTotals[i] = Mathf.RoundToInt(
                    animOldVals[i] + animGroupVals[i] + animCorrVals[i] + animStolenVals[i]);
            }

            RecomputeMaxScore();
            ApplyAllBarsAndCounters();
            yield return null;
        }

        // Snap to actual final values.
        for (int i = 0; i < rows.Count; i++)
        {
            // Re-derive bars from final actual total: keep old/group at full, then drain corruption & stolen
            // proportionally to fill up to actualTotal.
            float remaining = Mathf.Max(0, rows[i].actualTotal - rows[i].oldScore - rows[i].groupScore);
            animOldVals[i]   = rows[i].oldScore;
            animGroupVals[i] = Mathf.Min(rows[i].groupScore, Mathf.Max(0, rows[i].actualTotal - rows[i].oldScore));
            // Distribute remaining across corruption + stolen, prioritising corruption first.
            float corrFinal   = Mathf.Min(rows[i].player.roundCorruptionScore, remaining);
            float stolenFinal = Mathf.Max(0, remaining - corrFinal);
            animCorrVals[i]   = corrFinal;
            animStolenVals[i] = stolenFinal;
            animDisplayedTotals[i] = rows[i].actualTotal;
        }
        RecomputeMaxScore();
        ApplyAllBarsAndCounters();

        animationRoutine = null;
    }

    /// <summary>
    /// Subtracts <paramref name="deductSoFar"/> from row <paramref name="i"/>'s bar stack,
    /// peeling from the top down (stolen → corruption → group → old).
    /// </summary>
    private void ApplyDeductionTopDown(
        int i, float deductSoFar,
        float startStolen, float startCorr, float startGroup, float startOld)
    {
        float remaining = deductSoFar;

        // Stolen first.
        float stolenAfter = Mathf.Max(0, startStolen - remaining);
        remaining -= (startStolen - stolenAfter);
        animStolenVals[i] = stolenAfter;

        // Then corruption.
        float corrAfter = Mathf.Max(0, startCorr - remaining);
        remaining -= (startCorr - corrAfter);
        animCorrVals[i] = corrAfter;

        // Then group.
        float groupAfter = Mathf.Max(0, startGroup - remaining);
        remaining -= (startGroup - groupAfter);
        animGroupVals[i] = groupAfter;

        // Finally old. (Should not normally happen — the round's total can't exceed the round's earnings.)
        float oldAfter = Mathf.Max(0, startOld - remaining);
        animOldVals[i] = oldAfter;
    }

    private void ShowFloatingPenalty(int slot, int amount)
    {
        if (slot >= penaltyFloatTexts.Count) return;
        var tmp = penaltyFloatTexts[slot];
        if (tmp == null) return;
        tmp.text = $"-{amount}";
        tmp.gameObject.SetActive(true);
        StartCoroutine(HideFloatAfterDelay(tmp, floatTextDuration));
    }

    private IEnumerator HideFloatAfterDelay(TextMeshProUGUI tmp, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (tmp != null) tmp.gameObject.SetActive(false);
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
