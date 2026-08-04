---
description: "Provides full codebase context for the Partyament Unity mobile party debate game. Use when working on any feature, bug fix, or refactor in this project."
---

# Partyament — Codebase Context

You are working on **Partyament**, a Unity (C#) mobile party game where players split into groups and debate each other in a localized social setting using a single shared phone. The game is pass-and-play: players hand the phone around for secret objectives, voting, and debate moderation.

## Project Structure

```
Assets/Scripts/
├── Managers/                        (Core game logic — singletons & services)
│   ├── GameManager.cs               (Singleton state machine, game flow orchestration)
│   ├── PlayerManager.cs             (Player/Group CRUD, scoring, group assignment)
│   ├── TopicManager.cs              (Topic filtering by pack/type/seriousness, random selection)
│   └── SecretObjectiveManager.cs    (Weighted random objective assignment per player)
├── PageControllers/                 (One controller per UI screen/state)
│   ├── MenuController.cs
│   ├── PackSelectionController.cs
│   ├── LocalVsOnlineController.cs
│   ├── StartLocalGameController.cs
│   ├── AssignGroupsController.cs
│   ├── TopicSelectionController.cs
│   ├── MetricSelectionController.cs
│   ├── AssignPositionsController.cs
│   ├── PlayerMutexController.cs
│   ├── SecretObjectiveDisplayController.cs
│   ├── DMDisplayController.cs
│   ├── VotingController.cs
│   ├── ScoreboardController.cs
│   ├── SettingsController.cs
│   ├── RulebookController.cs
│   ├── HostVsJoinController.cs
│   ├── HostOnlineGameController.cs
│   └── JoinOnlineGameController.cs
├── ObjectControllers/               (Reusable UI component scripts)
│   ├── MetricDragHandler.cs         (Drag-and-drop for metric cards: click + drag)
│   ├── VoteSlotDropTarget.cs        (Drop target for metric vote slots)
│   ├── MetricGridDropTarget.cs      (Drop target returning metrics to grid)
│   ├── DragHandle.cs                (Drag icon for player name cards in group assignment)
│   ├── GroupPrefabController.cs     (Group container prefab logic)
│   ├── SecObjCardController.cs      (Secret objective card display + completion toggle)
│   ├── ButtonAnimatorReset.cs       (Clears stale EventSystem selection on enable)
│   ├── StateAnimator.cs             (Plays Enter/Exit animations on enable/disable)
│   ├── OptionToggleController.cs    (Two-option toggle with lerping highlight)
│   └── Rotate.cs                    (Continuous rotation utility)
├── DevModeDatabase/                 (Hardcoded content for development)
│   ├── TopicDatabase.cs             (~50 topics across 5 packs)
│   └── SecretObjectiveDatabase.cs   (~50 objectives across 4 types)
└── IMetricDropTarget.cs             (Interface for drag-and-drop metric system)
```

### Key Asset Folders

- **Assets/Prefabs/**: PlayerEntry, Name In Group Prefab (static + moveable), Group Prefab, SecObj Card, Position Switch, Option Toggle, Input Field, button variants
- **Assets/Scenes/**: MainScene.unity, BjarkiMainScene.unity
- **Assets/Database/**: Currently empty (content lives in DevModeDatabase scripts)

## Data Model

### Player (Serializable class)
- `int id` — Unique player identifier
- `string name` — Display name
- `int score` — Individual score (from secret objectives)
- `int group_id` — Group assignment (-1 = unassigned)
- `int secretObjectiveId` — Assigned objective (-1 = none)

### Group (Serializable class)
- `int id` — Unique group identifier
- `string name` — Display name (e.g., "Group 1")
- `int score` — Final accumulated group score
- `Position position` — `For` or `Against` (debate stance)
- `int secretObjectiveId` — Group objective (-1 = none)
- `int votingPhasePoints` — Temporary accumulator during voting round

### Topic (Serializable class)
- `int id`, `string title`, `string description`
- `Pack pack` — Default, Icelandic, EighteenPlus, Political, PopCulture
- `TopicType type` — Short, Medium, Long
- `int seriousness` — 0–5 scale
- `string leadingQuestionFor` / `string leadingQuestionAgainst` — Guiding questions per side

### SecretObjective (Serializable class)
- `int id`, `string title`, `string description`, `string shortDescription`
- `int points` — 20–100 (multiples of 10)
- `int? neededCount` / `int? achievedCount` — Repetition tracking
- `bool completeted` — Completion flag (note: typo in original code)
- `SecretObjectiveType type` — Speech (40%), Interruption (15%), Civilian (40%), Betrayal (5%)

## Core Enums

```csharp
// Game content packs
enum Pack { Default, Icelandic, EighteenPlus, Political, PopCulture }

// Debate position
enum Position { For, Against }

// Secret objective types (with assignment weights)
enum SecretObjectiveType { Civilian, Speech, Interruption, Betrayal }

// Voting metrics — DM selects exactly 2 before debate
enum Metric { Comedy, Creativity, OnTopic, Factual, Enthusiasm }

// Game states (see flow below)
enum GameState {
    None, LoadingScreen, PackSelection, Settings, Rulebook,
    LocalVsOnline, StartLocalGame, AssignGroups, TopicSelection,
    MetricSelection, AssignPositions, PlayerMutex, SecretObjectiveDisplay,
    DMDisplay, Voting, Scoreboard,
    HostVsJoin, HostOnlineGame, JoinOnlineGame
}
```

## Game State Machine & Flow

**GameManager** is a singleton (`DontDestroyOnLoad`) that drives the entire game through a `Dictionary<GameState, GameObject>` mapping states to UI panels. `SetState(GameState)` disables the current panel (resetting button animators) and enables the new one.

### Local Game Flow (primary path)

```
LoadingScreen → PackSelection → LocalVsOnline → StartLocalGame → AssignGroups
    → TopicSelection → MetricSelection → AssignPositions
    → [SecretObjectiveSequence: PlayerMutex ↔ SecretObjectiveDisplay per player]
    → DMDisplay (live debate with timer)
    → [VotingSequence: PlayerMutex ↔ Voting per group, then DM metric vote]
    → Scoreboard → NewGame() → PackSelection
```

- **Settings / Rulebook**: Overlay states that save and restore the previous state.
- **PlayerMutex**: Pass-the-phone screen. Shows "Hand the phone to [Player]" with a confirm button. Used between secret objective reveals and between voting turns.

### Secret Objective Sequence
1. `StartSecretObjectiveSequence()`: Orders non-DM players by ascending ID.
2. For each player: `StartMutex(player) → SecretObjectiveDisplay` (hold-to-reveal card).
3. After all players: `StartMutex(DM) → DMDisplay`.

### Voting Sequence
1. `StartVotingSequence()`: Orders groups by ascending ID.
2. Each group selects their top 2–3 groups (depending on total group count).
3. `FinalizeGroupVoting()`: Ranks groups by accumulated `votingPhasePoints`, awards 1st=3, 2nd=2, 3rd=1.
4. DM votes: Selects 2 groups per chosen metric, each gets `metricPoints` (default 3) added to `group.score`.
5. Advance to Scoreboard.

### Scoring
- **Player score**: from completed secret objectives (`player.score`)
- **Group score**: from voting rounds + DM metric awards (`group.score`)
- **Total display**: `player.score + group.score`

## Architectural Patterns

1. **Singleton**: GameManager instance accessible globally via `GameManager.Instance`.
2. **State Machine**: `GameState` enum + `SetState()` activates/deactivates UI panels.
3. **PageController Pattern**: Each screen has a controller that references `GameManager.Instance` in `Start()`, refreshes UI in `OnEnable()`, and exposes `Next()`/`Back()` methods for navigation.
4. **Manager Services**: PlayerManager, TopicManager, SecretObjectiveManager handle domain logic. Accessed through GameManager's public references.
5. **Drag-and-Drop via `IMetricDropTarget`**: Interface with `OnDragHoverEnter()`, `OnDragHoverExit()`, `OnMetricDropped()`. Implemented by `VoteSlotDropTarget` (slots) and `MetricGridDropTarget` (return-to-grid). `MetricDragHandler` handles the drag source with ghost creation and raycasting.
6. **Prefab Templating**: Groups, player cards, objective cards, and toggles are instantiated from prefabs at runtime.
7. **Animator Safety**: `DisableState()` resets all `Selectable` animators to "Normal" before deactivating panels, preventing stale animation states.

## DM (Discussion Moderator) Role

The **DM** is initially the player with the **lowest ID** (first player added), but can be changed. The DM:
- Always receives the `Civilian` secret objective type (no active objective).
- Selects 2 metrics before the debate.
- Sees all players' secret objectives during the debate (DMDisplay).
- Controls a debate timer (start/pause/stop, 60s default).
- Advances group turns during the debate.
- Votes on metrics after group voting concludes.

## Development Mode

When `GameManager.developmentMode = true`:
- Loads hardcoded player names from `PlayerManager.devModePlayerNames`.
- Skips the loading screen.
- Starts at a configurable `startingState` (inspector field).
- Content loaded from `TopicDatabase` and `SecretObjectiveDatabase`.

## Online Mode (Stub)

Online states (`HostVsJoin`, `HostOnlineGame`, `JoinOnlineGame`) exist as UI-only stubs. No networking code is implemented yet.

## Important Implementation Details

- **Player limits**: 3–16 players for a local game.
- **Group limits**: Minimum 2 groups required.
- **Topic filtering**: By pack, then by type (Short/Medium/Long), then by seriousness (±1 of selected level). Seen topics are excluded.
- **Secret objective weights**: Speech 40%, Civilian 40%, Interruption 15%, Betrayal 5%. Civilian means no objective is assigned.
- **MetricDragHandler click vs drag**: Movement < 10px = click (toggle slot), ≥ 10px = drag (ghost + drop).
- **`completeted` typo**: The `SecretObjective.completeted` field is intentionally kept as-is throughout the codebase for consistency.

---

If a prompt is ambiguous, ask for clarification on necessary information instead of guessing or assuming anything.
