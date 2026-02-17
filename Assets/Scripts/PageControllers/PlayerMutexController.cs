using UnityEngine;
using TMPro;

public class PlayerMutexController : MonoBehaviour
{
    [Header("References")]
    private GameManager gameManager;
    private PlayerManager playerManager => gameManager.playerManager;
    private TextMeshProUGUI nameDisplay;
    private TextMeshProUGUI buttonText;

    private GameManager.GameState nextState;
    private PlayerManager.PlayerModel currentPlayer;

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void SetNextStateAndName(GameManager.GameState nextState, PlayerManager.PlayerModel player)
    {
        this.nextState = nextState;
        this.currentPlayer = player;

        if (nameDisplay != null)
        {
            nameDisplay.text = player.name;
            buttonText.text = "I am " + player.name;
        }
    }

    public void IAmButton()
    {
        switch (nextState)
        {
            case GameManager.GameState.SecretObjectiveMutexDisplay:
                gameManager.SetSecretObjectiveMutexDisplay(currentPlayer);
                gameManager.SetState(nextState);
                break;
            
            case GameManager.GameState.DMDisplay:
                gameManager.SetDMDisplay();
                gameManager.SetState(nextState);
                break;

            case GameManager.GameState.Voting:
                gameManager.SetVotingDisplay(currentPlayer);
                gameManager.SetState(nextState);
                break;
        }
    }
}