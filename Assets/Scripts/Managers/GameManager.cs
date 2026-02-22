using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Singleton pattern
    public static GameManager Instance { get; private set; }
    public PlayerManager playerManager;
    public BillManager billManager;
    public SecretObjectiveManager secretObjectiveManager;

    [Header("Dev Values")]
    public bool developmentMode = true;
    [SerializeField, Tooltip("Dictates the starting state of the game when development mode is ON")] private GameState startingState = GameState.LoadingScreen;
    public enum GameState
    {
        // Global States
        LoadingScreen, PackSelection,
        // Local Game States
        LocalVsOnline, StartLocalGame, AssignGroups, BillSelection, MetricSelection, AssignPositions, PlayerMutex, SecretObjectiveDisplay, DMDisplay, Voting, Scoreboard,
        // Online Game States
        HostVsJoin, HostOnlineGame, JoinOnlineGame
    }

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
    [HideInInspector] public GameState currentState;
    [HideInInspector] public bool menuOpen;

    // Secret Objective Sequence — controls the order players view their objectives
    // Edit this ordering logic in StartSecretObjectiveSequence() to change player order
    private List<Player> secretObjectiveOrder = new List<Player>();
    private int secretObjectiveIndex = 0;

    [Header("State References")]
    public GameObject loadingScreen;
    public GameObject menuPopup;
    public GameObject packSelection;
    public GameObject localVsOnline;

    // Local Game States
    public GameObject startLocalGame;
    public GameObject assignGroups;
    public GameObject billSelection;
    public GameObject metricSelection;
    public GameObject assignPositions;
    public GameObject playerMutex;
    public GameObject secretObjectiveDisplay;
    public GameObject dmDisplay;
    public GameObject voting;
    public GameObject scoreboard;

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
            { GameState.PackSelection, packSelection },
            { GameState.LocalVsOnline, localVsOnline },

            // Local Game States
            { GameState.StartLocalGame, startLocalGame },
            { GameState.AssignGroups, assignGroups },
            { GameState.BillSelection, billSelection },
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
    public void SetState(GameState newState)
    {
        if (menuOpen) return;

        // Disable all states
        foreach (var state in stateDictionary.Values)
        {
            state.SetActive(false);
        }

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

    public void SetPack(Pack pack)
    {
        selectedPack = pack;
        Debug.Log($"Selected Pack: {pack}");
    }

    public void NewGame()
    {
        Debug.Log("Starting a New Game");
        // Reset Metrics, Bill Selection, Secret Objectives, and Player Groups
        selectedMetrics.Clear();
        selectedPack = Pack.Default;
        billManager.ResetBillSelection();
        secretObjectiveManager.ResetSecretObjectives();
        playerManager.ResetPlayerGroups();

        // Go back to Pack Selection
        SetState(GameState.PackSelection);
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

        // Wait for 3 seconds
        yield return new WaitForSeconds(3f);

        // Switch to the next state
        SetState(nextState);
    }

    public void StartMutex(Player player, GameState nextState)
    {
        playerMutex.GetComponent<PlayerMutexController>().SetNameAndNextState(player.name, nextState);
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
                break;
            case GameState.BillSelection:
                break;
        }
        SetState(nextState);
    }

    // -------------------- Secret Objective Sequence --------------------

    /// <summary>
    /// Starts the sequence of showing each non-DM player their secret objective.
    /// Players are shown in ascending player ID order (excluding the DM, who has the lowest ID).
    /// Edit the OrderBy below to change the player ordering.
    /// </summary>
    public void StartSecretObjectiveSequence()
    {
        int dmId = playerManager.players.Keys.Min();

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
        int dmId = playerManager.players.Keys.Min();

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

    public void SetDMDisplay()
    {
        // TODO:
    }

    public void SetVotingDisplay(Player player)
    {
        
    }
}
