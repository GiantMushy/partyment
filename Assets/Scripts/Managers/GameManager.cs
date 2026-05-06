using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework.Constraints;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent singleton (<c>DontDestroyOnLoad</c>) that owns the game's high-level state.
/// Every screen transition flows through <see cref="SetState"/>, which uses
/// <see cref="stateDictionary"/> to map <see cref="GameState"/> values onto the GameObject
/// panels assigned in the Inspector.
///
/// Responsibilities:
///   • <b>State machine</b> — activate/deactivate panels; reset Selectable animators on
///     the outgoing panel so buttons don't re-enter in a stale Pressed/Selected state.
///   • <b>Round / sequence orchestration</b> —
///     <see cref="StartCorruptionSequence"/> + <see cref="AdvanceCorruptionSequence"/>
///     walk every non-DM player through PlayerMutex → CorruptionDisplay before handing to
///     the DM; <see cref="StartVotingSequence"/> + <see cref="AdvanceVotingSequence"/> do
///     the same for group voting → DM metric voting; <see cref="StartNextRound"/> commits
///     scores via <see cref="PlayerManager.CommitRoundScores"/> and resets per-round state.
///   • <b>Manager refs</b> — <see cref="playerManager"/> / <see cref="topicManager"/> /
///     <see cref="corruptionManager"/> are MonoBehaviours on the same GameObject.
///   • <b>Helpers</b> — language switching, transitions
///     (<see cref="PlayTransition"/>), pack selection, online-game stubs.
///
/// Page controllers should never call <see cref="SetState"/> directly for game-flow
/// transitions; call the semantic methods (Start/Advance sequences) instead.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Game Rounds")]
    [SerializeField, Tooltip("Number of rounds per game")] public int totalRounds = 3;
    [HideInInspector] public int currentRound = 1;

    // Singleton pattern
    public static GameManager Instance { get; private set; }
    public PlayerManager playerManager;
    public TopicManager topicManager;
    public CorruptionManager corruptionManager;

    [Header("Dev Values")]
    public bool developmentMode = true;
    [SerializeField, Tooltip("Dictates the starting state of the game when development mode is ON")] private GameState startingState = GameState.LoadingScreen;
    public enum Language { English, Icelandic }
    public Language selectedLanguage = Language.English;
    public enum GameState
    {
        // Global States
        None, LoadingScreen, PackSelection, Settings, Rulebook,
        // Local Game States
        LocalVsOnline, StartLocalGame, AssignGroups, TopicSelection, MetricSelection, AssignPositions, PlayerMutex, CorruptionDisplay, DMDisplay, Voting, Scoreboard,
        // Online Game States
        HostVsJoin, HostOnlineGame, JoinOnlineGame
    }

    private GameState saveStateForMenu;

    // Game Settings
    public enum Pack { Default, Icelandic, EighteenPlus, Political, PopCulture }
    public List<Pack> OwnedPacks = new List<Pack>() { Pack.Default }; // This should be set based on actual owned packs in a real implementation
    public enum Position { For, Against }
    public enum CorruptionType { Civilian, Speech, Interruption, Betrayal }
    public static Pack selectedPack = Pack.Default;
    public int selectedSeriousnessLevel = 2; // 0 = Silly, 2 = Balanced, 4 = Serious

    // DM selected metric for voting
    public enum Metric { Comedy, Creativity, OnTopic, Factual, Enthusiasm }
    [HideInInspector] public List<Metric> selectedMetrics = new List<Metric>();

    // State Management
    private Dictionary<GameState, GameObject> stateDictionary;
    [HideInInspector] public GameState currentState = GameState.None;
    [HideInInspector] public bool menuOpen;

    // Networking Variables
    private string currentRoomCode;

    // Corruption Sequence — controls the order players view their corruptions
    // Edit this ordering logic in StartCorruptionSequence() to change player order
    private List<Player> corruptionOrder = new List<Player>();
    private int corruptionIndex = 0;

    // Voting Sequence — controls the order groups vote, then the DM votes metrics
    private List<Group> votingGroupOrder = new List<Group>();
    private int votingGroupIndex = 0;
    [HideInInspector] public bool isDMMetricVoting = false;
    [SerializeField] private float fakeLoadingTime = 3f;

    [Header("State References")]
    public GameObject loadingScreen;
    public GameObject menuPopup;
    public GameObject settings;
    public GameObject rulebook;
    public GameObject packSelection;
    public GameObject localVsOnline;

    // Local Game States
    public GameObject startLocalGame;
    public GameObject assignGroups;
    public GameObject topicSelection;
    public GameObject metricSelection;
    public GameObject assignPositions;
    public GameObject playerMutex;
    public GameObject corruptionDisplay;
    public GameObject dmDisplay;
    public GameObject voting;
    public GameObject scoreboard;

    [Header("Transition")]
    public GameObject transitionScreen;

    // Online Game States
    public GameObject hostVsJoin;
    public GameObject hostOnlineGame;
    public GameObject joinOnlineGame;

    void Awake()
    {
        // Singleton initialization
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        stateDictionary = new Dictionary<GameState, GameObject>
        {
            { GameState.LoadingScreen, loadingScreen },
            { GameState.Settings, settings },
            { GameState.Rulebook, rulebook },
            { GameState.PackSelection, packSelection },
            { GameState.LocalVsOnline, localVsOnline },

            // Local Game States
            { GameState.StartLocalGame, startLocalGame },
            { GameState.AssignGroups, assignGroups },
            { GameState.TopicSelection, topicSelection },
            { GameState.MetricSelection, metricSelection },
            { GameState.AssignPositions, assignPositions },
            { GameState.PlayerMutex, playerMutex },
            { GameState.CorruptionDisplay, corruptionDisplay },
            { GameState.DMDisplay, dmDisplay },
            { GameState.Voting, voting },
            { GameState.Scoreboard, scoreboard },

            // Online Game States
            { GameState.HostVsJoin, hostVsJoin },
            { GameState.HostOnlineGame, hostOnlineGame },
            { GameState.JoinOnlineGame, joinOnlineGame }
        };
        
        DisableAllStates();
        if (developmentMode)
        {
            Debug.Log("Development Mode: ON");
            playerManager.InitializeDevModePlayers();
            SetState(startingState);
        }
        else
        {
            Debug.Log("Development Mode: OFF");
            StartCoroutine(LoadingSequence());
        }
    }

    // ------------------------------ Helper Functions ------------------------------
    private static readonly string[] ButtonTriggers = { "Normal", "Highlighted", "Pressed", "Selected", "Disabled" };

    public void SetState(GameState newState)
    {
        if (menuOpen) return;

        // Disable the current state (skip on first call when currentState is None)
        if (currentState != GameState.None && stateDictionary.ContainsKey(currentState))
            DisableState(stateDictionary[currentState]);

        // Enable the desired state
        if (stateDictionary.ContainsKey(newState))
        {
            stateDictionary[newState].SetActive(true);
            currentState = newState;
            Debug.Log($"Switched to state: {newState}");
        }
        else
        {
            Debug.LogError($"State {newState} not found in the dictionary!");
        }
    }

    /// <summary>
    /// Resets only button/Selectable animators to "Normal", then deactivates the panel.
    /// Scoped to Selectables so we don't hit StateAnimator or other custom animators
    /// that don't have a "Normal" state.
    /// </summary>
    private void DisableState(GameObject state)
    {
        foreach (var selectable in state.GetComponentsInChildren<Selectable>(true))
        {
            var animator = selectable.GetComponent<Animator>();
            if (animator == null || !animator.isActiveAndEnabled) continue;

            foreach (var trigger in ButtonTriggers)
                animator.ResetTrigger(trigger);

            animator.Play("Normal", 0, 0f);
            animator.Update(0f);
        }
        state.SetActive(false);
    }

    private void DisableAllStates()
    {
        foreach (var state in stateDictionary.Values)
            state.SetActive(false);
    }

    public void SetPack(Pack pack)
    {
        selectedPack = pack;
        Debug.Log($"Selected Pack: {pack}");
    }

    public void NewGame()
    {
        Debug.Log("Starting a New Game");
        // Reset Metrics, Topic Selection, Corruptions, Player Groups, and ALL scores
        selectedMetrics.Clear();
        selectedPack = Pack.Default;
        topicManager.ResetTopicSelection();
        corruptionManager.ResetCorruptions();
        playerManager.ResetAllScores();
        playerManager.ResetPlayerGroups();
        currentRound = 1;
        assignGroups.GetComponent<AssignGroupsController>().ResetInitialization();

        // Go back to Pack Selection
        SetState(GameState.PackSelection);
    }

    public void OpenSettings()
    {
        if (saveStateForMenu == GameState.None) saveStateForMenu = currentState;
        SetState(GameState.Settings);
    }

    public void OpenRulebook()
    {
        if (saveStateForMenu == GameState.None) saveStateForMenu = currentState;
        SetState(GameState.Rulebook);
    }

    public void BackToSavedState()
    {
        SetState(saveStateForMenu);
        saveStateForMenu = GameState.None;
    }

    public void OpenFeedbackForm()
    {
        Debug.Log("Opening Feedback Form");
        //Application.OpenURL("https://forms.gle/partyment-feedback");
    }

    public void OpenDataPrivacyPage()
    {
        Debug.Log("Opening Data Privacy Page");
        //Application.OpenURL("https://partyment.com/privacy");
    }

    public void ButtonNotImplemented()
    {
        Debug.LogError("This Button Has not been programmed yet");
    }

    public void QuitGame()
    {
        Debug.Log("Quitting Game");
        Application.Quit();
    }

    /// <summary>
    /// Plays a full-screen transition overlay: fades in over the current screen,
    /// invokes <paramref name="midTransitionAction"/> while fully opaque (so the
    /// underlying state change is hidden), then fades out to reveal the new screen.
    /// </summary>
    public void PlayTransition(string text, System.Action midTransitionAction)
    {
        var ctrl = transitionScreen.GetComponent<TransitionController>();
        ctrl.Setup(text, midTransitionAction);
        transitionScreen.SetActive(true);
    }

    public void AddPlayer(int id, string name)
    {
        playerManager.AddPlayer(id, name);
    }

    public void RemovePlayer(int id)
    {
        playerManager.RemovePlayer(id);
    }

    public void UpdatePlayerGroup(int id, int newGroupId)
    {
        playerManager.UpdatePlayerGroup(id, newGroupId);
    }
    
    public IEnumerator LoadingSequence(GameState nextState = GameState.PackSelection)
    {
        // Start in the LoadingScreen state
        SetState(GameState.LoadingScreen);
        yield return new WaitForSeconds(fakeLoadingTime);

        SetState(nextState);
    }

    public void StartMutex(Player player, GameState nextState)
    {
        playerMutex.GetComponent<PlayerMutexController>().SetNameAndNextState(player.name, nextState);
        SetState(GameState.PlayerMutex);
    }

    public void StartMutex(string displayName, string buttonPrefix, GameState nextState)
    {
        playerMutex.GetComponent<PlayerMutexController>().SetNameAndNextState(displayName, buttonPrefix, nextState);
        SetState(GameState.PlayerMutex);
    }

    public void ExitMutex(GameState nextState)
    {
        // Set up the target state before transitioning
        switch (nextState)
        {
            case GameState.CorruptionDisplay:
                var currentPlayer = corruptionOrder[corruptionIndex];
                corruptionDisplay.GetComponent<CorruptionDisplayController>().SetPlayer(currentPlayer);
                break;
            case GameState.DMDisplay:
                break;
            case GameState.Voting:
                var votingCtrl = voting.GetComponent<VotingController>();
                if (isDMMetricVoting)
                    votingCtrl.PrepareForDMMetricVoting();
                else
                    votingCtrl.PrepareForGroupVoting();
                break;
            case GameState.TopicSelection:
                break;
        }
        SetState(nextState);
    }

    // -------------------- Corruption Sequence --------------------

    /// <summary>
    /// Starts a new round: advances round counter, resets round-specific state, and moves to topic selection.
    /// </summary>
    public void StartNextRound()
    {
        if (currentRound < totalRounds)
        {
            // Fold this round's earnings into each player's oldScore BEFORE incrementing the
            // round counter. The Scoreboard has already finished animating at this point.
            playerManager.CommitRoundScores();

            currentRound++;
            selectedMetrics.Clear();
            playerManager.ResetAccusations();
            corruptionManager.AssignCorruptionsToPlayers(playerManager.players, playerManager.dmId);
            SetState(GameState.TopicSelection);
        }
        else if (currentRound == totalRounds)
        {
            // Already at last round, go to scoreboard or finish
            SetState(GameState.Scoreboard);
        }
    }
    /// Players are shown in ascending player ID order (excluding the DM).
    /// Edit the OrderBy below to change the player ordering.
    /// </summary>
    public void StartCorruptionSequence()
    {
        int dmId = playerManager.dmId;

        // ---- Player ordering logic (edit here to change order) ----
        corruptionOrder = playerManager.players.Values
            .Where(p => p.id != dmId)
            .OrderBy(p => p.id)
            .ToList();
        // -----------------------------------------------------------

        corruptionIndex = 0;

        if (corruptionOrder.Count > 0)
        {
            StartMutex(corruptionOrder[0], GameState.CorruptionDisplay);
        }
        else
        {
            // No non-DM players, go straight to DM
            StartMutex(playerManager.players[dmId], GameState.DMDisplay);
        }
    }

    /// <summary>
    /// Advances to the next player in the corruption sequence,
    /// or hands the phone to the DM if all players have seen theirs.
    /// </summary>
    public void AdvanceCorruptionSequence()
    {
        corruptionIndex++;
        int dmId = playerManager.dmId;

        if (corruptionIndex < corruptionOrder.Count)
        {
            // Next player's mutex
            StartMutex(corruptionOrder[corruptionIndex], GameState.CorruptionDisplay);
        }
        else
        {
            // All players have seen their corruptions — hand to DM
            StartMutex(playerManager.players[dmId], GameState.DMDisplay);
        }
    }

    // -------------------- Voting Sequence --------------------

    /// <summary>
    /// Starts the voting sequence: each group votes for top groups,
    /// then the DM assigns metrics. Uses PlayerMutex between each voter.
    /// </summary>
    public void StartVotingSequence()
    {
        votingGroupOrder = playerManager.groups.Values
            .OrderBy(g => g.id)
            .ToList();
        votingGroupIndex = 0;
        isDMMetricVoting = false;

        if (votingGroupOrder.Count > 0)
        {
            StartMutex(votingGroupOrder[0].name, "We are ", GameState.Voting);
        }
    }

    /// <summary>
    /// Advances to the next group's vote, or to the DM's metric vote
    /// if all groups have voted.
    /// </summary>
    public void AdvanceVotingSequence()
    {
        votingGroupIndex++;

        if (votingGroupIndex < votingGroupOrder.Count)
        {
            // More groups to vote
            StartMutex(votingGroupOrder[votingGroupIndex].name, "We are ", GameState.Voting);
        }
        else
        {
            // All groups have voted — finalize group voting scores
            voting.GetComponent<VotingController>().FinalizeGroupVoting();

            // Hand to DM for metric voting
            isDMMetricVoting = true;
            int dmId = playerManager.dmId;
            Player dm = playerManager.players[dmId];
            StartMutex(dm.name, "I am ", GameState.Voting);
        }
    }

    public void SetLanguage(string langCode)
    {
        switch (langCode)
        {
            case "en":
                selectedLanguage = Language.English;
                break;
            case "is":
                selectedLanguage = Language.Icelandic;
                break;
            default:
                Debug.LogError($"Unsupported language code: {langCode}");
                break;
        }
        Debug.Log($"Selected Language: {selectedLanguage}");
    }

    // ------------------------ Networking ------------------------

    public string GetRoomCode() => currentRoomCode;

    public void JoinOnlineGame(int port, string playerName)
    {
        Debug.Log($"Attempting to join online game on port {port} as {playerName}");
        // TODO: Implement actual networking logic to connect to the host and join the game
    }

    public void HostOnlineGame()
    {
        GenerateRoomCode();
        Debug.Log($"Hosting online game with room code {currentRoomCode}");
    }

    public void StopHostingOnlineGame()
    {
        Debug.Log("Stopped hosting online game");
        currentRoomCode = null;
    }

    public void AddPlayersToOnlineGame(List<string> playerNames)
    {
        int id = 1;
        foreach (string playerName in playerNames)
        {
            playerManager.AddPlayer(id, playerName);
            id++;
        }
    }

    public void GenerateRoomCode()
    {
        // PLACEHOLDER FOR FUTURE CODE GENERATION LOGIC

        System.Random rand = new System.Random();
        int roomCode = rand.Next(100000, 999999); // Generate a random 6-digit code
        Debug.Log($"Generated Room Code: {roomCode}");
        currentRoomCode = roomCode.ToString();
    }

}
