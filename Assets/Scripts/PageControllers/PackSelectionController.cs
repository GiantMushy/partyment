using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// First real screen of the game. Presents the five Pack options (Default, Icelandic,
/// 18+, Political, PopCulture); buttons for packs not in <see cref="GameManager.OwnedPacks"/>
/// are disabled. Selecting a pack records it on <see cref="GameManager"/>, asks
/// <see cref="TopicManager.LoadTopicsFromPack"/> to filter the topic list, and advances
/// to <see cref="GameManager.GameState.LocalVsOnline"/>.
/// </summary>
public class PackSelectionController : MonoBehaviour
{
    private GameManager gameManager;
    private TopicManager TopicManager => gameManager.topicManager;
    [SerializeField] private Button defaultPackButton;
    [SerializeField] private Button icelandicPackButton;
    [SerializeField] private Button eighteenPlusPackButton;
    [SerializeField] private Button politicalPackButton;
    [SerializeField] private Button popCulturePackButton;

    void Start()
    {
        gameManager = GameManager.Instance;

        // Disable all packs buttons that aren't owned (they use Transition > Animation)
        defaultPackButton.interactable = gameManager.OwnedPacks.Contains(GameManager.Pack.Default);
        icelandicPackButton.interactable = gameManager.OwnedPacks.Contains(GameManager.Pack.Icelandic);
        eighteenPlusPackButton.interactable = gameManager.OwnedPacks.Contains(GameManager.Pack.EighteenPlus);
        politicalPackButton.interactable = gameManager.OwnedPacks.Contains(GameManager.Pack.Political);
        popCulturePackButton.interactable = gameManager.OwnedPacks.Contains(GameManager.Pack.PopCulture);
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
