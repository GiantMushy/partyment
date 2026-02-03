using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Singleton pattern
    public static GameManager Instance { get; private set; }

    [Header("Dev Values")]
    public bool developmentMode = true;
    [SerializeField] private GameState startingState = GameState.LoadingScreen;
    private enum GameState
    {
        // Global States
        LoadingScreen,
        // Local Game States
        LocalVsOnline, StartLocalGame, AssignGroups,
        // Online Game States
        HostVsJoin
    }
    private Dictionary<GameState, GameObject> stateDictionary;

    public enum PlayerGroup { Unassigned, DM, Group_1, Group_2, Group_3, Group_4, Group_5}
    public Dictionary<int, PlayerModel> players = new Dictionary<int, PlayerModel>();

    [Header("State References")]
    public GameObject loadingScreen;
    public GameObject localVsOnline;

    // Local Game States
    public GameObject startLocalGame;
    public GameObject assignGroups;

    // Online Game States
    public GameObject hostVsJoin;

    void Start()
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

        stateDictionary = new Dictionary<GameState, GameObject>
        {
            { GameState.LoadingScreen, loadingScreen },
            { GameState.LocalVsOnline, localVsOnline },
            { GameState.AssignGroups, assignGroups },

            // Local Game States
            { GameState.StartLocalGame, startLocalGame },

            // Online Game States
            { GameState.HostVsJoin, hostVsJoin }
        };
        
        if (developmentMode)
        {
            Debug.Log("Development Mode: ON");
            SetState(startingState);
        }
        else
        {
            Debug.Log("Development Mode: OFF");
            StartCoroutine(LoadingSequence());
        }
    } 

    // ------------------------------ Button Functions ------------------------------
    public void LocalButton()
    {
        Debug.Log("Local Button Pressed");
        SetState(GameState.StartLocalGame);
    }

    public void OnlineButton()
    {
        Debug.Log("Online Button Pressed");
        SetState(GameState.HostVsJoin);
    }

    public void MenuButton()
    {
        Debug.Log("Open Menu Button Pressed");
        SetState(GameState.LocalVsOnline);
    }

    public void SaveLocalNames()
    {
        Debug.Log("Save Local Names Button Pressed");
        
        // Validate minimum 3 players
        if (players == null || players.Count < 3)
        {
            Debug.LogWarning($"Need at least 3 players to continue. Current count: {(players?.Count ?? 0)}");
            
            // ADD ERROR MESSAGE LOGIC HERE:
            // You can add UI error message display logic here, such as:
            // - Show a popup/modal with error message
            // - Display error text in the UI
            // - Play error sound/animation
            // - Flash the button red
            // Example: errorMessageText.text = "Need at least 3 players!";
            // Example: errorMessagePanel.SetActive(true);
            
            return; // Don't proceed if not enough players
        }
        
        Debug.Log($"Proceeding with {players.Count} players");
        SetState(GameState.AssignGroups);
    }

    public void ConfirmAssignGroups ()
    {
        Debug.Log("Assign Groups Next Button Pressed");
    }

    public void HostButton()
    {
        Debug.Log("Host Button Pressed");
    }

    public void JoinButton()
    {
        Debug.Log("Join Button Pressed");
    }

    // ------------------------------ Helper Functions ------------------------------
    private void SetState(GameState newState)
    {
        // Disable all states
        foreach (var state in stateDictionary.Values)
        {
            state.SetActive(false);
        }

        // Enable the desired state
        if (stateDictionary.ContainsKey(newState))
        {
            stateDictionary[newState].SetActive(true);
            Debug.Log($"Switched to state: {newState}");
        }
        else
        {
            Debug.LogError($"State {newState} not found in the dictionary!");
        }
    }
    
    private IEnumerator LoadingSequence()
    {
        // Start in the LoadingScreen state
        SetState(GameState.LoadingScreen);

        // Wait for 3 seconds
        yield return new WaitForSeconds(3f);

        // Switch to the LocalVsOnline state
        SetState(GameState.LocalVsOnline);
    }
}
