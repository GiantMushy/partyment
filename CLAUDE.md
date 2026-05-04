# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Partyment** is a Unity 6 (6000.3.1f1) party debate game. Players split into groups, debate topics chosen by a Discussion Moderator (DM), and vote on group performance. Each round may include Secret Objectives that players secretly complete for bonus points.

The project uses **Universal Render Pipeline (URP)**, **TextMeshPro**, and **Unity Localization** (English + Icelandic).

## Development

This is a Unity project — all building, running, and testing is done through the **Unity Editor** (version 6000.3.1f1). There are no CLI build or test commands.

- Open the project in Unity Hub and load `MainScene.unity` (or `BjarkiMainScene.unity` for the alternate dev scene).
- Enable **Development Mode** by setting `GameManager.developmentMode = true` in the Inspector. This skips the loading screen and starts in `startingState`.
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
| `TopicManager` | Loads topics from `TopicDatabase`, filters by selected `Pack` and `seriousness` level (0–5 scale). |
| `SecretObjectiveManager` | Assigns weighted-random objectives each round (40% Speech, 15% Interruption, 5% Betrayal, ~40% Civilian/none). |

### Game Flow (Local)

```
PackSelection → StartLocalGame → AssignGroups → TopicSelection → MetricSelection
→ AssignPositions → [PlayerMutex → SecretObjectiveDisplay] × N → [PlayerMutex → DMDisplay]
→ [PlayerMutex → Voting] × groups + DM → Scoreboard → (repeat or new game)
```

**PlayerMutex** is a privacy handoff screen — used whenever the device passes between players so they can't see each other's information.

**Voting has two phases**: `GroupVoting` (each group ranks other groups) then `DMMetricVoting` (DM assigns two metric awards). `GameManager` orchestrates the sequence; `VotingController` handles the UI and scoring.

### PageControllers Pattern

Each `GameState` has a corresponding controller in `Assets/Scripts/PageControllers/`. Controllers:
- Reference `GameManager.Instance` (never cached across scenes).
- Re-initialize in `OnEnable()`, not `Start()`, because panels are toggled active/inactive repeatedly.
- Never call `SetState()` directly — they call semantic methods on `GameManager` (e.g. `gameManager.AdvanceVotingSequence()`).

### Drag & Drop System

Metric selection and voting use a custom drag-and-drop system:
- `MetricDragHandler` / `VotingDragHandler` — attached to draggable cards; handle both click-to-toggle and drag gestures.
- Drop targets implement `IMetricDropTarget` / `IVotingDropTarget`.
- The drag ghost is parented to `dragLayer` (a top-level Canvas `RectTransform`) so it renders above all UI.
- In DM metric voting, `VotingController` spawns clones of group cards so the same group can fill both metric slots.

### Data / Content

Topics and Secret Objectives are hardcoded in `Assets/Scripts/DevModeDatabase/`. These are the dev-mode data source. Production packs/content will eventually replace or supplement these.

- **Topics**: organized by `Pack` (Default, Icelandic, EighteenPlus, Political, PopCulture), `TopicType` (Short/Medium/Long), and `seriousness` (0–5). The random selector matches topics within ±1 seriousness of the player-chosen level.
- **Secret Objectives**: types are `Civilian` (no objective), `Speech`, `Interruption`, `Betrayal`.

### Scoring

- **Group score** (`Group.score`): awarded during voting phase finalization and DM metric awards.
- **Player score** (`Player.score`): net total — can go negative.

**Player score fields** (all on `Player` in `PlayerManager.cs`):

| Field | Type | Resets each round | Description |
|---|---|---|---|
| `score` | `int` | No | Net total score |
| `stolenScore` | `int` | No | Points earned by correctly accusing another player |
| `penaltyScore` | `int` | No | Points lost from incorrect accusations (use to display deductions separately) |
| `hasAccused` | `bool` | Yes | True once this player has made an accusation this round |
| `isAccused` | `bool` | Yes | True once this player has been successfully accused this round |

**Accusation mechanic** — managed by `AccusationController` (a sub-panel of `DMDisplay`):
- Any non-DM player may accuse once per round (`hasAccused` gates this; their button is disabled after use).
- **Correct accusation**: stolen points = accused player's `SecretObjective.points`. Points are deducted from the accused via `SubtractScore` and added to the accuser via `AddStolenScore`. `SetPlayerAccused(accusedId)` is then called, which sets `isAccused = true` and prevents their objective toggle from awarding points going forward.
- **Incorrect accusation**: `AddPenaltyScore(accusingId, incorrectPenalty)` records the deduction in `penaltyScore` and subtracts from `score` in one call. Default penalty is 20 points (Inspector-configurable).
- Players with no secret objective (`secretObjectiveId == -1`) have their accusation-target buttons disabled during the `PlayerSelected` state.

**Secret Objective toggle** (`SecObjCardController`): when a player's `isAccused` flag becomes true, `Update()` immediately disables their toggle. If the toggle was already checked, it is silently unchecked and `objective.completeted` is cleared — the score is already correct because the accusation's `SubtractScore` handled the transfer.

### Localization

Language is set via `GameManager.SetLanguage("en"/"is")` which records the enum, and `SettingsController` also calls `LocalizationSettings.SelectedLocale` via the Unity Localization package to switch UI strings.
