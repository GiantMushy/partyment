# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Partyment** is a Unity 6 (6000.3.1f1) party debate game. Players split into groups, debate topics chosen by a Discussion Moderator (DM), and vote on group performance. Each round may include Corruptions that players secretly complete for bonus points.

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
| `Pack` | General, Icelandic, EighteenPlus, Political, PopCulture |
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
- Players with no secret objective (`corruptionId == -1`) have their accusation-target buttons disabled during the `PlayerSelected` state.

**Secret Objective toggle** (`CorruptionCardController`): when a player's `isAccused` flag becomes true, `Update()` immediately disables their toggle. If the toggle was already checked, it is silently unchecked and `objective.completeted` is cleared — the score is already correct because the accusation's `SubtractScore` handled the transfer.

### Localization

Language is set via `GameManager.SetLanguage("en"/"is")` which records the enum, and `SettingsController` also calls `LocalizationSettings.SelectedLocale` via the Unity Localization package to switch UI strings. CSV content is bilingual — English and Icelandic columns are loaded together and the correct one is selected at display time.
