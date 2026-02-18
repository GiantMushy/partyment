using System;
using System.Collections;
using System.Collections.Generic;
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
        LoadingScreen, PackSelection, Scalelevel,
        // Local Game States
        LocalVsOnline, StartLocalGame, AssignGroups, BillSelection, MetricSelection, AssignPositions, PlayerMutex, SecretObjectiveMutexDisplay, DMDisplay, Voting, Scoreboard,
        // Online Game States
        HostVsJoin, HostOnlineGame, JoinOnlineGame
    }

    // Game Settings
    public enum Pack { Default, Icelandic, EighteenPlus, Political, PopCulture }
    public static Pack selectedPack;
    public int selectedSeriousnessLevel = 2; // 0 = Silly, 2 = Balanced, 4 = Serious

    // DM selected metric for voting
    public enum Metric { Comedy, Creativity, OnTopic, Factual, Enthusiasm }
    [HideInInspector] public List<Metric> selectedMetrics = new List<Metric>();

    // State Management
    private Dictionary<GameState, GameObject> stateDictionary;
    [HideInInspector] public GameState currentState;
    [HideInInspector] public bool menuOpen;

    // Secret Objective Management

    [Header("State References")]
    public GameObject loadingScreen;
    public GameObject menuPopup;
    public GameObject packSelection;
    public GameObject localVsOnline;

    public GameObject scalelevel;

    // Local Game States
    public GameObject startLocalGame;
    public GameObject assignGroups;
    public GameObject billSelection;
    public GameObject metricSelection;
    public GameObject assignPositions;
    public GameObject playerMutex;
    public GameObject secretObjectiveMutexDisplay;
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
            { GameState.Scalelevel, scalelevel},

            // Local Game States
            { GameState.StartLocalGame, startLocalGame },
            { GameState.AssignGroups, assignGroups },
            { GameState.BillSelection, billSelection },
            { GameState.MetricSelection, metricSelection },
            { GameState.AssignPositions, assignPositions },
            { GameState.PlayerMutex, playerMutex },
            { GameState.SecretObjectiveMutexDisplay, secretObjectiveMutexDisplay },
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

    public void BackToPackSelect()
    {
        Debug.Log("Going back to Pack Selection");
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

    public void AddPlayer(int id, string name, Color favouredColor = default, PlayerManager.PlayerGroup group = PlayerManager.PlayerGroup.Unassigned)
    {
        playerManager.AddPlayer(id, name, favouredColor, group);
    }

    public void RemovePlayer(int id)
    {
        playerManager.RemovePlayer(id);
    }

    public void UpdatePlayerGroup(int id, PlayerManager.PlayerGroup newGroup)
    {
        playerManager.UpdatePlayerGroup(id, newGroup);
    }

    public void UpdatePlayerColor(int id, Color newColor)
    {
        playerManager.UpdatePlayerColor(id, newColor);
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

    public void StartMutex(PlayerManager.PlayerModel player, GameState nextState)
    {
        playerMutex.GetComponent<PlayerMutexController>().SetNameAndNextState(player.name, nextState);
        SetState(GameState.PlayerMutex);
    }

    public void ExitMutex(GameState nextState)
    {
        switch (currentState)
        {
            case GameState.SecretObjectiveMutexDisplay:
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

    public void SetSecretObjectiveMutexDisplay(PlayerManager.PlayerModel player)
    {
        // TODO: 
    }

    public void SetDMDisplay()
    {
        // TODO:
    }

    public void SetVotingDisplay(PlayerManager.PlayerModel player)
    {
        
    }
}
