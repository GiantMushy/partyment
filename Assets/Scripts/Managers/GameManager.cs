using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework.Constraints;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    // Singleton pattern
    public static GameManager Instance { get; private set; }
    public PlayerManager playerManager;
    public TopicManager topicManager;
    public SecretObjectiveManager secretObjectiveManager;

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
        LocalVsOnline, StartLocalGame, AssignGroups, TopicSelection, MetricSelection, AssignPositions, PlayerMutex, SecretObjectiveDisplay, DMDisplay, Voting, Scoreboard,
        // Online Game States
        HostVsJoin, HostOnlineGame, JoinOnlineGame
    }

    private GameState saveStateForMenu;

    // Game Settings
    public enum Pack { Default, Icelandic, EighteenPlus, Political, PopCulture }
    public enum Position { For, Against }
    public enum SecretObjectiveType { Civilian, Speech, Interruption, Betrayal }
    public static Pack selectedPack = Pack.Default;
    public int selectedSeriousnessLevel = 2; // 0 = Silly, 2 = Balanced, 4 = Serious

    // DM selected metric for voting
    public enum Metric { Comedy, Creativity, OnTopic, Factual, Enthusiasm }
    [HideInInspector] public List<Metric> selectedMetrics = new List<Metric>();

    // State Management
    private Dictionary<GameState, GameObject> stateDictionary;
    [HideInInspector] public GameState currentState = GameState.None;
    [HideInInspector] public bool menuOpen;

    // Secret Objective Sequence — controls the order players view their objectives
    // Edit this ordering logic in StartSecretObjectiveSequence() to change player order
    private List<Player> secretObjectiveOrder = new List<Player>();
    private int secretObjectiveIndex = 0;

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
    public GameObject secretObjectiveDisplay;
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
            { GameState.SecretObjectiveDisplay, secretObjectiveDisplay },
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
        // Reset Metrics, Topic Selection, Secret Objectives, and Player Groups
        selectedMetrics.Clear();
        selectedPack = Pack.Default;
        topicManager.ResetTopicSelection();
        secretObjectiveManager.ResetSecretObjectives();
        playerManager.ResetPlayerGroups();
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
            case GameState.SecretObjectiveDisplay:
                var currentPlayer = secretObjectiveOrder[secretObjectiveIndex];
                secretObjectiveDisplay.GetComponent<SecretObjectiveDisplayController>().SetPlayer(currentPlayer);
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

    // -------------------- Secret Objective Sequence --------------------

    /// <summary>
    /// Starts the sequence of showing each non-DM player their secret objective.
    /// Players are shown in ascending player ID order (excluding the DM).
    /// Edit the OrderBy below to change the player ordering.
    /// </summary>
    public void StartSecretObjectiveSequence()
    {
        int dmId = playerManager.dmId;

        // ---- Player ordering logic (edit here to change order) ----
        secretObjectiveOrder = playerManager.players.Values
            .Where(p => p.id != dmId)
            .OrderBy(p => p.id)
            .ToList();
        // -----------------------------------------------------------

        secretObjectiveIndex = 0;

        if (secretObjectiveOrder.Count > 0)
        {
            StartMutex(secretObjectiveOrder[0], GameState.SecretObjectiveDisplay);
        }
        else
        {
            // No non-DM players, go straight to DM
            StartMutex(playerManager.players[dmId], GameState.DMDisplay);
        }
    }

    /// <summary>
    /// Advances to the next player in the secret objective sequence,
    /// or hands the phone to the DM if all players have seen theirs.
    /// </summary>
    public void AdvanceSecretObjectiveSequence()
    {
        secretObjectiveIndex++;
        int dmId = playerManager.dmId;

        if (secretObjectiveIndex < secretObjectiveOrder.Count)
        {
            // Next player's mutex
            StartMutex(secretObjectiveOrder[secretObjectiveIndex], GameState.SecretObjectiveDisplay);
        }
        else
        {
            // All players have seen their objectives — hand to DM
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
}
