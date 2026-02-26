using UnityEngine;

public class PackSelectionController : MonoBehaviour
{
    private GameManager gameManager;
    private TopicManager TopicManager => gameManager.topicManager;

    // Awake is called when the script instance is being loaded
    void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void Default()
    {
        gameManager.SetPack(GameManager.Pack.Default);
        TopicManager.LoadTopicsFromPack();
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }

    public void EighteenPlus()
    {
        gameManager.SetPack(GameManager.Pack.EighteenPlus);
        TopicManager.LoadTopicsFromPack();
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }

    public void Icelandic()
    {
        gameManager.SetPack(GameManager.Pack.Icelandic);
        TopicManager.LoadTopicsFromPack();
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }

    public void Political()
    {
        gameManager.SetPack(GameManager.Pack.Political);
        TopicManager.LoadTopicsFromPack();
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }

    public void PopCulture()
    {
        gameManager.SetPack(GameManager.Pack.PopCulture);
        TopicManager.LoadTopicsFromPack();
        gameManager.SetState(GameManager.GameState.LocalVsOnline);
    }

}
