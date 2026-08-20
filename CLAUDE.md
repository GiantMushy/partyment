# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Partyment** is a Unity 6 (6000.3.1f1) party debate game. Players split into groups, debate topics chosen by a Discussion Moderator (DM), and vote on group performance. Each round may include Corruptions that players secretly complete for bonus points.

The project uses **Universal Render Pipeline (URP)**, **TextMeshPro**, and **Unity Localization** (English + Icelandic).

## Development

This is a Unity project — all building, running, and testing is done through the **Unity Editor** (version 6000.3.1f1). There are no CLI build or test commands.

- Open the project in Unity Hub and load `MainScene.unity` (or `BjarkiMainScene.unity` for the alternate dev scene).
- Enable **Development Mode** by setting `GameManager.developmentMode = true` in the Inspector. This seeds dev-mode players and starts in `startingState` instead of Pack Selection.
- Populate `PlayerManager.devModePlayerNames` with **more than 3 names** — `InitializeDevModePlayers()` silently does nothing with 3 or fewer.
- The starting state can be set via `GameManager.startingState` in the Inspector to jump directly to any screen for testing.

## Architecture

### State Machine (GameManager)

`GameManager` is a **persistent singleton** (`DontDestroyOnLoad`) that owns the entire game state. All screen transitions go through `GameManager.SetState(GameState)`.

`GameState` enum maps directly to GameObject panels (e.g. `GameState.Voting` → the `voting` GameObject). `SetState` deactivates the current panel and activates the new one, resetting button animator states on the outgoing panel to avoid visual glitches.

**Never navigate between screens without going through `GameManager.SetState()`.**

### Core Managers

All three managers are MonoBehaviours held as fields on the `GameManager` GameObject:

| Manager | Responsibility |
|---|---|
| `PlayerManager` | Players (`Dictionary<int, Player>`) and Groups (`Dictionary<int, Group>`). The DM is always the player with the lowest ID unless explicitly set. |
| `TopicManager` | Loads topics from `Topics.csv` via `TopicDatabase`, filters by selected `Pack` and `seriousness` level (0–5 scale). Tracks seen topics to avoid repeats. |
| `CorruptionManager` | Assigns weighted-random objectives each round (40% Speech, 15% Interruption, 5% Betrayal, ~40% Civilian/none). Loads from `Corruptions.csv`. |

### Game Flow (Local)

```
PackSelection → StartLocalGame → AssignGroups → TopicSelection → MetricSelection
→ AssignPositions → [PlayerMutex → CorruptionDisplay] × N → [PlayerMutex → DMDisplay]
→ [PlayerMutex → Voting] × groups + DM → Scoreboard → (repeat or new game)
```

**PlayerMutex** is a privacy handoff screen — used whenever the device passes between players so they can't see each other's information.

**Voting has two phases**: `GroupVoting` (each group ranks other groups) then `DMMetricVoting` (DM assigns two metric awards). `GameManager` orchestrates the sequence; `VotingController` handles the UI and scoring.

### PageControllers Pattern

Each `GameState` has a corresponding controller in `Assets/Scripts/PageControllers/`. Controllers:
- Reference `GameManager.Instance` (never cached across scenes).
- Re-initialize in `OnEnable()`, not `Start()`, because panels are toggled active/inactive repeatedly.
- Never call `SetState()` directly — they call semantic methods on `GameManager` (e.g. `gameManager.AdvanceVotingSequence()`).

#### StartLocalGameController

Player name registration screen (`Assets/Scripts/PageControllers/StartLocalGameController.cs`):
- Supports 3–16 players (configurable in `PlayerManager`). Next button disabled until ≥ 3 players are entered.
- Players added via an input field (max 12 characters; field auto-hides at max capacity).
- Each player entry supports inline name editing and deletion.
- `OnDefaultInputEndEdit()` adds the player on input submit; `UpdateNextButtonState()` gates the Next button.

#### AssignGroupsController

Drag-and-drop group assignment screen (`Assets/Scripts/PageControllers/AssignGroupsController.cs`):
- The player with the lowest ID is placed in a fixed **Discussion Moderator** container (non-draggable label).
- Remaining players are distributed into 2+ debate groups, initially named "Group 1", "Group 2", etc. Group names are editable via `InputField`.
- **Drag & Drop:** Players drag cards between group containers. A "ghost" group at the bottom auto-creates a new group when a card is dropped on it; groups auto-delete when emptied.
- **Randomize button:** Reshuffles non-DM players across the current number of groups.
- Next button enabled only when: ≥ 2 real debate groups exist, no unassigned players remain, and the DM container has exactly 1 player.
- `BuildScreen()` sets the initial layout; `RebuildScreenPreservingLayout()` re-renders while preserving custom names and positions; `CommitGroupAssignments()` writes the final layout to `PlayerManager`.

#### TopicSelectionController

DM topic selection screen (`Assets/Scripts/PageControllers/TopicSelectionController.cs`):
- Displays two topic types the DM can toggle between: **Versus** ("This or That" style) and **Scenarios** (longer debate prompts).
- Topics are filtered by `gameManager.selectedSeriousnessLevel` (0 = Silly, 2 = Balanced, 4 = Serious) within ±1 tolerance.
- DM can shuffle the displayed topic up to `startingNumOfShuffles` times per round (default 1).
- `LoadRandomTopics()` fetches one random Versus and one random Scenario from `TopicManager`; `OnSelectClicked()` commits `topicManager.currentTopic` and advances to MetricSelection.

#### AssignPositionsController

Position assignment screen (`Assets/Scripts/PageControllers/AssignPositionsController.cs`):
- Randomly assigns **For** or **Against** positions to each debate group (alternating, starting from a random side).
- Each group card shows the group name, position label, and a list of its players.
- **Swap Button** on each group card lets the DM manually toggle that group's position.
- `Next()` calls `gameManager.StartCorruptionSequence()` to begin the secret objective reveal sequence.

### Drag & Drop System

Metric selection, group assignment, and voting use custom drag-and-drop systems:
- `MetricDragHandler` / `VotingDragHandler` — attached to draggable cards; handle both click-to-toggle and drag gestures.
- Drop targets implement `IMetricDropTarget` / `IVotingDropTarget`.
- The drag ghost is parented to `dragLayer` (a top-level Canvas `RectTransform`) so it renders above all UI.
- In DM metric voting, `VotingController` spawns clones of group cards so the same group can fill both metric slots.

### Data / Content

Topics and Corruptions are loaded from **CSV files** in `Assets/Database/`. The loader classes live in `Assets/Scripts/DevModeDatabase/` but read from CSV rather than hardcoded data.

#### Topics.csv (`Assets/Database/Topics.csv`)

| Column | Description |
|---|---|
| `id` | Unique integer |
| `Pack` | Classic, Icelandic, EighteenPlus, Political, PopCulture |
| `Length` | `"This or That"` (Versus) or `"Scenarios"` |
| `Description Enska` | English topic text |
| `Description Íslenska` | Icelandic topic text |
| `This` / `That` | English option labels (Versus only) |
| `Hitt` / `Þetta` | Icelandic option labels (Versus only) |

Loaded by `TopicDatabase.LoadTopics()` which parses RFC 4180 CSV, maps `Length` to `TopicType` (Versus/Scenarios), and skips rows with missing English descriptions. `seriousness` defaults to 2 (Balanced) since it is not yet a CSV column.

#### Corruptions.csv (`Assets/Database/Corruptions.csv`)

| Column | Description |
|---|---|
| `id` | Unique integer |
| `points` | Point value (defaults to 60 if missing) |
| `Pack` | Pack grouping |
| `Type` | Speech, Interruption, Betrayal, Civilian |
| `Title` / `Description` / `Short Description` | English strings |
| `Title(IS)` / `Description(IS)` / `Short Description(IS)` | Icelandic strings |

Loaded by `CorruptionDatabase.LoadCorruptions()`. Civilian type = no active objective. Current data: ~48 objectives spanning all types.

### Scoring

Scoring is **per-round**: every score field on `Player` and `Group` represents earnings for the *current* round only. At the end of each round, `PlayerManager.CommitRoundScores()` rolls each round's net into the player's `oldScore` and zeroes the per-round counters. `oldScore` is the only field that accumulates across rounds.

**Player score fields** (all on `Player` in `PlayerManager.cs`):

| Field | Type | Resets each round | Description |
|---|---|---|---|
| `score` | `int` | Yes (commit) | Personal running net for the **current round**: +corruption +stolen −penalty −points lost when correctly accused. Group score is *not* included here. |
| `roundCorruptionScore` | `int` | Yes (commit) | Gross corruption earned this round via the toggle. **Never** decreased by accusations — used by the Scoreboard's "Corruption" bar so the player still sees what they completed before getting caught. |
| `stolenScore` | `int` | Yes (commit) | Points earned this round by correctly accusing another player. |
| `penaltyScore` | `int` | Yes (commit) | Points lost this round from incorrect accusations (use to display deductions separately). |
| `oldScore` | `int` | **No** — committed at end of round | Sum of all *previous* rounds' net earnings (group + corruption + stolen − penalty − accusedLoss). The Scoreboard's animation start point in rounds 2+. |
| `hasAccused` | `bool` | Yes | True once this player has made an accusation this round. |
| `isAccused` | `bool` | Yes | True once this player has been successfully accused this round. |

**Group score fields**:
- `Group.score` (`int`) — total round score; always equal to `voteScore + metric1Score + metric2Score`. Reset by `CommitRoundScores()` along with the breakdown components and `votingPhasePoints`.
- `Group.voteScore` (`int`) — points earned from the group-voting-phase ranking (1st/2nd/3rd place). Written by `VotingController.FinalizeGroupVoting` via the `AwardVotePoints` helper.
- `Group.metric1Score` / `Group.metric2Score` (`int`) — DM metric awards. Slot 0 maps to `gameManager.selectedMetrics[0]`, slot 1 to `selectedMetrics[1]`. Written by `VotingController.ApplyVotes` in DM-metric mode. The Scoreboard animates these as separate "Points for {metric}" phases.

**Round commit lifecycle** (`PlayerManager.CommitRoundScores()`):
- For each player: `oldScore += groupScore + score`, then zero `score`, `roundCorruptionScore`, `stolenScore`, `penaltyScore`.
- For each group: zero `score`, `voteScore`, `metric1Score`, `metric2Score`, and `votingPhasePoints`.
- **Called by `GameManager.StartNextRound()`** as the first step (after the Scoreboard is dismissed via the Next-Round button, before `currentRound++`).

**New-game reset** (`PlayerManager.ResetAllScores()`):
- Wipes ALL score state — both per-round counters and `oldScore` — on every player and group. Called by `GameManager.NewGame()` along with `currentRound = 1`.

**Score manipulation methods** (`PlayerManager`):
- `AddScore` / `SubtractScore` — direct mutations of `score` (used internally by the methods below; rarely called externally).
- `AddRoundCorruptionScore` / `SubtractRoundCorruptionScore` — used by `CorruptionCardController` toggle. Updates **both** `score` and `roundCorruptionScore` so the gross corruption bar stays in sync. **Never call `score += points` directly for corruption** — bypassing this means `roundCorruptionScore` won't track and the bar will be wrong.
- `AddStolenScore(accuserId, amount)` — increments `stolenScore` AND adds to `score`.
- `AddPenaltyScore(playerId, amount)` — increments `penaltyScore` AND subtracts from `score`.

**Accusation mechanic** — managed by `AccusationController` (a sub-panel of `DMDisplay`):
- Any non-DM player may accuse once per round (`hasAccused` gates this; their button is disabled after use).
- **Three-step DM flow**: select the accusing player's button → select the accused player's button (crossfire tint narrows to just them) → resolve with the green **Correct** or red **Incorrect** button (both in the `AcceptanceContainter` GameObject, hidden via CanvasGroup until a target is chosen). Clicking the accuser again cancels; clicking the chosen target again deselects the target. The Correct button is disabled when the target has no active corruption, since that accusation can only be incorrect. The player-selected/target-selected instruction text embeds player names and is written from code (bilingual EN/IS in `UpdateStateDescription`), not via LocalizeStringEvent.
- **Correct accusation**: stolen points = accused player's `SecretObjective.points`. Points are deducted from the accused via `SubtractScore` (`score` only — `roundCorruptionScore` is intentionally left intact so the gross corruption bar still shows what they completed) and added to the accuser via `AddStolenScore`. `SetPlayerAccused(accusedId)` then sets `isAccused = true` to prevent further objective toggles from awarding points.
- **Incorrect accusation**: `AddPenaltyScore(accusingId, incorrectPenalty)` records the deduction in `penaltyScore` and subtracts from `score` in one call. Default penalty is 20 points (Inspector-configurable).
- Players with no secret objective (`corruptionId == -1`) can still be selected as accusation targets (the accusation is simply incorrect), but the Correct button is disabled for them.

**Secret Objective toggle** (`CorruptionCardController`): when a player's `isAccused` flag becomes true, `Update()` immediately disables their toggle. If the toggle was already checked, it is silently unchecked and `objective.completeted` is cleared — the score is already correct because the accusation's `SubtractScore` handled the transfer. Note: `roundCorruptionScore` is *not* decremented here either; it remains as the gross corruption display value.

#### ScoreboardController

End-of-round score reveal screen (`Assets/Scripts/PageControllers/ScoreboardController.cs`):
- **Three stacked bars per player slot** (bottom → top in the `VerticalLayoutGroup` with `ReverseArrangement = 1`):
  1. **Old Score** (hidden in Round 1) — `Player.oldScore`
  2. **Group Score** — `Group.score` for the player's group (animated in three sub-passes: voteScore → metric1Score → metric2Score)
  3. **Corruption + Stolen** — `Player.roundCorruptionScore` first, then `Player.stolenScore` stacked into the same bar
- **Top-of-screen phase label** — `pointTypeDisplay` (TMP_Text) shows the current phase ("Group Votes", "Points for Comedy", "Corruptions", "Stolen Points", "Penalties").
- **Per-player Score Incrementer** — `scoreIncrementerDisplays` (one TMP_Text per player slot, formerly named `penaltyFloatTexts` / "Penalty Score N"). Visible **only while a row is actively being incremented** in the current phase (rows with a zero amount stay hidden); flashes "+N" during earn phases, "-N" during the penalty phase, hidden between phases. Inspector references survive the rename via `[FormerlySerializedAs("penaltyFloatTexts")]`.
- **Inspector lists** — one entry per player slot (7 max), parallel-indexed: `groupScoreDisplays`, `corruptionScoreDisplays`, `oldScoreDisplays`, `totalScoreDisplays`, `nameDisplays`, `scoreIncrementerDisplays`. The legacy `stolenScoreDisplays` list is kept for back-compat but always disabled at runtime — stolen points stack into the corruption bar visually.
- **Dynamic scaling**: `pixelsPerPoint = barContainerHeight / currentMaxScore`. Starts at `initialMaxScore = 300`, grows in `maxScoreStep = 200` increments (300 → 500 → 700 …) when any player's animated total exceeds the ceiling. Scale never shrinks — bars don't rebound. The **Scoarboard Background's** `VerticalLayoutGroup.spacing` is rescaled in lock step (`UpdateBackgroundSpacing`) so the marker lines (0, 50, 100, …) stay aligned with the values they advertise.
- **Multi-phase animation** on `OnEnable` — each phase is skipped if no row has a non-zero amount in it:
  - **⓪ Init** (instant): old-score bar pre-fills to `oldScore` in R2+; total counter starts at `oldScore`.
  - **① Group Votes** (`perPhaseDuration`, ease-out cubic): group bar grows by `Group.voteScore`. Score Incrementer = `+voteScore`.
  - **② Metric 1**: group bar grows by `Group.metric1Score`. Phase label = `"Points for {selectedMetrics[0]}"`.
  - **③ Metric 2**: group bar grows by `Group.metric2Score`. Phase label = `"Points for {selectedMetrics[1]}"`.
  - **④ Corruptions**: corruption bar grows by `roundCorruptionScore`.
  - **⑤ Stolen Points**: corruption bar continues growing by `stolenScore` (stacked above the corruption portion).
  - **⑥ Penalties** (`deductDuration`, ease-in-out): for any player whose actual total is below gross, the deficit is drained in priority order **GROUP → CORRUPTION → OLD**. Score Incrementer shows `-N`. If the deduction exceeds everything the player has (gross is already 0, or penalty > gross), the leftover lives in `animOverflowDeduct[i]` and is subtracted from the displayed total counter — bars stay flat at 0 and the score text just goes negative. The deduction itself is computed implicitly as `gross − actual`, so it covers both incorrect-accusation penalties and points lost when correctly accused.

  Between phases the Score Incrementer is hidden (Inspector-tunable `interPhaseDelay`).

### Localization

Language is set via `GameManager.SetLanguage("en"/"is")` which records the enum, and `SettingsController` also calls `LocalizationSettings.SelectedLocale` via the Unity Localization package to switch UI strings. CSV content is bilingual — English and Icelandic columns are loaded together and the correct one is selected at display time.
