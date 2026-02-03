using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Dev Values")]
    public bool developmentMode = true;
    [SerializeField] private GameState startingState = GameState.LoadingScreen;
    private enum GameState { LoadingScreen, LocalVsOnline, StartLocalGame, HostVsJoin }
    private Dictionary<GameState, GameObject> stateDictionary;

    [Header("State References")]
    public GameObject loadingScreen;
    public GameObject localVsOnline;
    public GameObject startLocalGame;
    public GameObject hostVsJoin;

    void Start()
    {
        // Initialize the dictionary
        stateDictionary = new Dictionary<GameState, GameObject>
        {
            { GameState.LoadingScreen, loadingScreen },
            { GameState.LocalVsOnline, localVsOnline },
            { GameState.StartLocalGame, startLocalGame },
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

    public void HostButton()
    {
        Debug.Log("Host Button Pressed");
    }

    public void JoinButton()
    {
        Debug.Log("Join Button Pressed");
    }

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
