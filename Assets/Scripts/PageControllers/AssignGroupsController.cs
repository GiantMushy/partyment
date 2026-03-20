using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Assign Groups screen.
/// Players are distributed into draggable groups. The DM (lowest player ID)
/// is always placed in a fixed "Discussion Moderator" container.
/// Players can be dragged between groups or into an unassigned holding area.
/// Groups can be added (+) or removed (–) with the constraint of a 2-group minimum.
/// </summary>
public class AssignGroupsController : MonoBehaviour
{
    // -------------------- Inspector References --------------------

    [Header("Managers")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;

    [Header("Prefabs")]
    [Tooltip("Container with VerticalLayoutGroup + ContentSizeFitter. Has Label/Title child and a content area.")]
    [SerializeField] private GameObject groupContainerPrefab;

    [Tooltip("Name card with Name TMP, Field image, and Drag Icon (with DragHandle).")]
    [SerializeField] private GameObject nameInGroupPrefab;

    [Tooltip("Transparent dotted-outline placeholder, same size as a name card.")]
    [SerializeField] private GameObject emptyFieldInGroupPrefab;

    [Header("Layout References")]
    [Tooltip("Parent transform that holds all GroupContainerPrefab instances.")]
    public Transform groupsParent;

    [Tooltip("VerticalLayoutGroup panel at the bottom for unassigned / ejected players.")]
    public Transform unassignedArea;

    [Tooltip("Top-level RectTransform used as parent for the drag ghost (renders above everything).")]
    public RectTransform dragLayer;

    [Tooltip("The ScrollRect containing the groups. Used to reset scroll position after layout rebuilds.")]
    [SerializeField] private ScrollRect scrollRect;

    [Header("Buttons")]
    [SerializeField] private Button plusButton;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button nextButton;

    // -------------------- Runtime State --------------------

    private int dmId;

    /// <summary>Number of debate groups (excludes the DM container).</summary>
    public int numberOfGroups = 2;

    /// <summary>Live list of group container GameObjects (index 0 = DM container).</summary>
    private List<GameObject> groupContainers = new List<GameObject>();

    /// <summary>Maps each NameInGroupPrefab instance to its player ID for bookkeeping.</summary>
    private Dictionary<RectTransform, int> cardToPlayerId = new Dictionary<RectTransform, int>();

    /// <summary>The group container currently highlighted during a drag.</summary>
    private Image currentHighlightedGroup;

    /// <summary>Cached original colour of the highlighted group, restored on unhighlight.</summary>
    private Color highlightOriginalColor;

    private static readonly Color HighlightTint = new Color(0.8f, 0.95f, 1f, 1f);

    /// <summary>Tracks whether the screen has been built at least once this game session.</summary>
    private bool hasBeenInitialized = false;

    // ================================================================
    //  Unity Lifecycle
    // ================================================================

    void Start()
    {
        gameManager = GameManager.Instance;
    }

    void OnEnable()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (gameManager == null || gameManager.playerManager == null) return;

        if (!hasBeenInitialized)
        {
            BuildScreen();
            hasBeenInitialized = true;
        }
        else
        {
            RebuildScreenPreservingLayout();
        }
    }

    // ================================================================
    //  Button Callbacks (wire these in the Inspector)
    // ================================================================

    /// <summary>Proceed to the next game state. Disabled while layout is invalid.</summary>
    public void Next()
    {
        // Commit the visual layout back to PlayerManager data
        CommitGroupAssignments();

        // Assign a random secret objective to each non-DM player
        gameManager.secretObjectiveManager.AssignSecretObjectivesToPlayers(PlayerManager.players, dmId);

        gameManager.PlayTransition("Game Is Starting!", () =>
        {
            gameManager.StartMutex(PlayerManager.players[dmId], GameManager.GameState.TopicSelection);
        });
    }

    public void Back()
    {
        gameManager.SetState(GameManager.GameState.StartLocalGame);
    }

    /// <summary>Add a new empty group.</summary>
    public void AddGroup()
    {
        numberOfGroups++;
        CreateGroupContainer(numberOfGroups); // label = "Group N"
        RefreshButtons();
    }

    /// <summary>Remove the last group, ejecting its players to the unassigned area.</summary>
    public void RemoveGroup()
    {
        if (numberOfGroups <= 2) return;

        // The last debate-group container (index 0 is DM, so last = end of list)
        GameObject lastContainer = groupContainers[groupContainers.Count - 1];
        EjectCardsToUnassigned(lastContainer.transform);
        groupContainers.Remove(lastContainer);
        Destroy(lastContainer);
        numberOfGroups--;

        RefreshButtons();
    }

    /// <summary>
    /// Randomizes all non-DM players into the current number of groups.
    /// Hook this up to a Randomize button in the Inspector.
    /// </summary>
    public void Randomize()
    {
        ClearScreen();

        dmId = PlayerManager.players.Keys.Min();
        var nonDMPlayers = GetShuffledNonDMPlayers(dmId);

        if (nonDMPlayers.Count < 2)
            numberOfGroups = Mathf.Max(1, nonDMPlayers.Count);
        else
            numberOfGroups = Mathf.Clamp(numberOfGroups, 2, nonDMPlayers.Count);

        // DM container
        GameObject dmContainer = CreateGroupContainer(0, "Discussion Moderator");
        CreateNameCard(PlayerManager.players[dmId], dmContainer.transform, draggable: true);

        // Distribute shuffled players into groups
        var distributed = DistributePlayersIntoGroups(nonDMPlayers, numberOfGroups);
        for (int g = 0; g < distributed.Count; g++)
        {
            GameObject container = CreateGroupContainer(g + 1);
            foreach (var player in distributed[g])
                CreateNameCard(player, container.transform, draggable: true);
        }

        RefreshButtons();
        ForceLayoutRebuild();
    }

    /// <summary>
    /// Resets the initialization flag so the next OnEnable does a fresh build.
    /// Called by GameManager.NewGame().
    /// </summary>
    public void ResetInitialization()
    {
        hasBeenInitialized = false;
        numberOfGroups = 2;
    }

    // ================================================================
    //  Screen Construction
    // ================================================================

    /// <summary>
    /// Tears down any existing UI and rebuilds the entire screen from
    /// the current PlayerManager data.
    /// </summary>
    private void BuildScreen()
    {
        ClearScreen();

        dmId = PlayerManager.players.Keys.Min();
        var nonDMPlayers = GetOrderedNonDMPlayers(dmId);

        // Always start with 2 groups on first build
        numberOfGroups = 2;
        if (nonDMPlayers.Count < 2)
            numberOfGroups = Mathf.Max(1, nonDMPlayers.Count);

        // --- DM container (always index 0) ---
        GameObject dmContainer = CreateGroupContainer(0, "Discussion Moderator");
        Player dmPlayer = PlayerManager.players[dmId];
        RectTransform dmCard = CreateNameCard(dmPlayer, dmContainer.transform, draggable: true);

        // --- Debate group containers ---
        var distributed = DistributePlayersIntoGroups(nonDMPlayers, numberOfGroups);
        for (int g = 0; g < distributed.Count; g++)
        {
            GameObject container = CreateGroupContainer(g + 1);
            foreach (var player in distributed[g])
            {
                CreateNameCard(player, container.transform, draggable: true);
            }
        }

        RefreshButtons();
        ForceLayoutRebuild();
    }

    private void ClearScreen()
    {
        // Destroy all group containers
        foreach (Transform child in groupsParent)
            Destroy(child.gameObject);

        // Destroy anything in the unassigned area
        foreach (Transform child in unassignedArea)
            Destroy(child.gameObject);

        groupContainers.Clear();
        cardToPlayerId.Clear();
    }

    /// <summary>
    /// Rebuilds the screen while preserving the player-to-group layout from before.
    /// New players (added since last visit) are placed in the unassigned area.
    /// Removed players are silently skipped.
    /// </summary>
    private void RebuildScreenPreservingLayout()
    {
        // Snapshot current layout: which player was in which group container index
        var playerToGroup = new Dictionary<int, int>();
        var unassignedPlayerIds = new HashSet<int>();

        for (int i = 0; i < groupContainers.Count; i++)
        {
            foreach (Transform child in groupContainers[i].transform)
            {
                var rt = child.GetComponent<RectTransform>();
                if (rt != null && cardToPlayerId.TryGetValue(rt, out int pid))
                    playerToGroup[pid] = i;
            }
        }

        foreach (Transform child in unassignedArea)
        {
            var rt = child.GetComponent<RectTransform>();
            if (rt != null && cardToPlayerId.TryGetValue(rt, out int pid))
                unassignedPlayerIds.Add(pid);
        }

        // Clear everything and rebuild
        ClearScreen();

        var allPlayerIds = new HashSet<int>(PlayerManager.players.Keys);
        var placedIds = new HashSet<int>();

        // Determine DM: whoever was in container index 0, or fallback to min ID
        var previousDM = playerToGroup.Where(kvp => kvp.Value == 0 && allPlayerIds.Contains(kvp.Key));
        dmId = previousDM.Any() ? previousDM.First().Key : allPlayerIds.Min();

        // Clamp numberOfGroups in case players were removed
        int nonDMCount = allPlayerIds.Count - 1;
        if (nonDMCount < 2)
            numberOfGroups = Mathf.Max(1, nonDMCount);
        else
            numberOfGroups = Mathf.Clamp(numberOfGroups, 2, nonDMCount);

        // DM container (always index 0)
        var dmContainer = CreateGroupContainer(0, "Discussion Moderator");
        CreateNameCard(PlayerManager.players[dmId], dmContainer.transform, draggable: true);
        placedIds.Add(dmId);

        // Recreate debate group containers, placing players in their previous groups
        for (int g = 1; g <= numberOfGroups; g++)
        {
            var container = CreateGroupContainer(g);

            foreach (var kvp in playerToGroup)
            {
                if (kvp.Value == g && allPlayerIds.Contains(kvp.Key)
                    && kvp.Key != dmId && !placedIds.Contains(kvp.Key))
                {
                    CreateNameCard(PlayerManager.players[kvp.Key], container.transform, draggable: true);
                    placedIds.Add(kvp.Key);
                }
            }
        }

        // Restore previously unassigned players
        foreach (int pid in unassignedPlayerIds)
        {
            if (allPlayerIds.Contains(pid) && !placedIds.Contains(pid))
            {
                CreateNameCard(PlayerManager.players[pid], unassignedArea, draggable: true);
                placedIds.Add(pid);
            }
        }

        // Any new players not in the previous layout → unassigned area
        foreach (int pid in allPlayerIds)
        {
            if (!placedIds.Contains(pid))
            {
                CreateNameCard(PlayerManager.players[pid], unassignedArea, draggable: true);
            }
        }

        RefreshButtons();
        ForceLayoutRebuild();
    }

    // ================================================================
    //  Layout Rebuild
    // ================================================================

    /// <summary>
    /// Forces Unity's layout system to recalculate sizes bottom-up
    /// (groupsParent first, then its parent Content) and resets the
    /// scroll position to the top. Fixes intermittent positioning bugs
    /// caused by ContentSizeFitter calculating before children exist.
    /// </summary>
    private void ForceLayoutRebuild()
    {
        if (groupsParent is RectTransform groupsRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(groupsRect);

        // Rebuild the outer Content layout (parent of groupsParent)
        if (groupsParent.parent is RectTransform contentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        // Reset scroll to top
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    // ================================================================
    //  Group Container Helpers
    // ================================================================

    /// <summary>
    /// Instantiates a GroupContainerPrefab, sets its label, and tracks it.
    /// <paramref name="groupNumber"/> 0 = DM, 1+ = "Group N".
    /// </summary>
    private GameObject CreateGroupContainer(int groupNumber, string overrideLabel = null)
    {
        GameObject container = Instantiate(groupContainerPrefab, groupsParent);
        string label = overrideLabel ?? $"Group {groupNumber}";
        SetContainerLabel(container, label);

        // All groups start with an empty placeholder (removed when a card is added)
        Instantiate(emptyFieldInGroupPrefab, container.transform);

        groupContainers.Add(container);
        return container;
    }

    private void SetContainerLabel(GameObject container, string label)
    {
        Transform titleTransform = container.transform.Find("Title");
        if (titleTransform == null) return;

        var tmp = titleTransform.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = label;
    }

    // ================================================================
    //  Name Card Helpers
    // ================================================================

    /// <summary>
    /// Creates a NameInGroupPrefab for the given player inside <paramref name="parent"/>.
    /// If the parent had an EmptyFieldInGroupPrefab, it is removed.
    /// </summary>
    private RectTransform CreateNameCard(Player player, Transform parent, bool draggable)
    {
        // Remove empty placeholder if present
        RemoveEmptyPlaceholder(parent);

        GameObject card = Instantiate(nameInGroupPrefab, parent);
        SetCardName(card, player.name);

        RectTransform rt = card.GetComponent<RectTransform>();
        cardToPlayerId[rt] = player.id;

        // Wire up the drag handle
        DragHandle handle = card.GetComponentInChildren<DragHandle>(true);
        if (handle != null)
        {
            handle.nameCard = rt;
            handle.Initialize(this);
            // Disable dragging for the DM card
            handle.enabled = draggable;
        }

        // Ensure card has a CanvasGroup for drag fading
        if (card.GetComponent<CanvasGroup>() == null)
            card.AddComponent<CanvasGroup>();

        return rt;
    }

    private void SetCardName(GameObject card, string playerName)
    {
        Transform nameTransform = card.transform.Find("Name");
        if (nameTransform == null) return;

        var tmp = nameTransform.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = playerName;
    }

    // ================================================================
    //  Placeholder Management
    // ================================================================

    /// <summary>Removes the first EmptyFieldInGroupPrefab child from a container.</summary>
    private void RemoveEmptyPlaceholder(Transform container)
    {
        foreach (Transform child in container)
        {
            if (IsEmptyPlaceholder(child.gameObject))
            {
                DestroyImmediate(child.gameObject);
                return;
            }
        }
    }

    /// <summary>Adds an empty placeholder to a container if it has no name-card children.</summary>
    private void EnsurePlaceholderIfEmpty(Transform container)
    {
        // Skip the unassigned area — it should never have a placeholder
        if (container == unassignedArea)
            return;

        bool hasCards = false;
        foreach (Transform child in container)
        {
            if (cardToPlayerId.ContainsKey(child.GetComponent<RectTransform>()))
            {
                hasCards = true;
                break;
            }
        }

        if (!hasCards && !ContainsEmptyPlaceholder(container))
        {
            Instantiate(emptyFieldInGroupPrefab, container);
        }
    }

    private bool ContainsEmptyPlaceholder(Transform container)
    {
        foreach (Transform child in container)
        {
            if (IsEmptyPlaceholder(child.gameObject))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Heuristic: a placeholder is any child that is NOT tracked as a name card
    /// and NOT the Label. We check by name prefix for robustness.
    /// </summary>
    private bool IsEmptyPlaceholder(GameObject obj)
    {
        // Placeholder instances will be named after the prefab
        return obj.name.StartsWith(emptyFieldInGroupPrefab.name);
    }

    // ================================================================
    //  Drag & Drop API  (called by DragHandle)
    // ================================================================

    /// <summary>Called when a drag begins on a name card.</summary>
    public void OnCardDragBegin(RectTransform card, Transform originalParent)
    {
        // Nothing extra needed here; state is held by DragHandle.
    }

    /// <summary>Called every frame during a drag to highlight the hovered group.</summary>
    public void OnCardDragUpdate(PointerEventData eventData)
    {
        // Raycast to find what's under the pointer
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        Transform hoveredGroup = null;
        bool hoveringUnassigned = false;

        foreach (var hit in results)
        {
            // Check if hovering the unassigned area
            if (hit.gameObject.transform == unassignedArea ||
                hit.gameObject.transform.IsChildOf(unassignedArea))
            {
                hoveringUnassigned = true;
                break;
            }

            // Check if hovering a group container
            Transform candidate = hit.gameObject.transform;
            while (candidate != null)
            {
                if (groupContainers.Contains(candidate.gameObject))
                {
                    hoveredGroup = candidate;
                    break;
                }
                candidate = candidate.parent;
            }
            if (hoveredGroup != null) break;
        }

        // Update highlight
        ClearHighlight();

        if (hoveredGroup != null)
        {
            ApplyHighlight(hoveredGroup.GetComponent<Image>());
        }
        else if (hoveringUnassigned)
        {
            ApplyHighlight(unassignedArea.GetComponent<Image>());
        }
    }

    /// <summary>Called when the drag ends. Decides where the card should land.</summary>
    public void OnCardDrop(RectTransform card, Transform originalParent, int originalSiblingIndex, PointerEventData eventData)
    {
        ClearHighlight();

        // Raycast to find the drop target
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        Transform dropTarget = null;
        bool droppedOnUnassigned = false;

        foreach (var hit in results)
        {
            // Unassigned area?
            if (hit.gameObject.transform == unassignedArea ||
                hit.gameObject.transform.IsChildOf(unassignedArea))
            {
                droppedOnUnassigned = true;
                break;
            }

            // Group container?
            Transform candidate = hit.gameObject.transform;
            while (candidate != null)
            {
                if (groupContainers.Contains(candidate.gameObject))
                {
                    dropTarget = candidate;
                    break;
                }
                candidate = candidate.parent;
            }
            if (dropTarget != null) break;
        }

        if (droppedOnUnassigned)
        {
            // Move card to the unassigned area
            card.SetParent(unassignedArea, false);
            EnsurePlaceholderIfEmpty(originalParent);
        }
        else if (dropTarget != null)
        {
            bool isDMContainer = groupContainers.Count > 0
                && dropTarget == groupContainers[0].transform;

            if (isDMContainer)
            {
                // DM container holds exactly 1 player — swap if occupied
                RectTransform existingCard = GetPlayerCardInContainer(dropTarget, card);
                if (existingCard != null)
                {
                    RemoveEmptyPlaceholder(originalParent);
                    existingCard.SetParent(originalParent, false);
                    existingCard.SetSiblingIndex(originalSiblingIndex);
                }
                else
                {
                    EnsurePlaceholderIfEmpty(originalParent);
                }

                RemoveEmptyPlaceholder(dropTarget);
                card.SetParent(dropTarget, false);
            }
            else
            {
                // Normal group drop
                RemoveEmptyPlaceholder(dropTarget);
                card.SetParent(dropTarget, false);
                EnsurePlaceholderIfEmpty(originalParent);
            }
        }
        else
        {
            // Invalid drop — return card to its original position
            card.SetParent(originalParent, false);
            card.SetSiblingIndex(originalSiblingIndex);
        }

        RefreshButtons();
    }

    // ================================================================
    //  Highlight Helpers
    // ================================================================

    private void ApplyHighlight(Image img)
    {
        if (img == null) return;
        currentHighlightedGroup = img;
        highlightOriginalColor = img.color;
        img.color = HighlightTint;
    }

    private void ClearHighlight()
    {
        if (currentHighlightedGroup != null)
        {
            currentHighlightedGroup.color = highlightOriginalColor;
            currentHighlightedGroup = null;
        }
    }

    // ================================================================
    //  Ejecting Cards from a Removed Group
    // ================================================================

    /// <summary>
    /// Moves every name card out of <paramref name="container"/> into the unassigned area.
    /// </summary>
    private void EjectCardsToUnassigned(Transform container)
    {
        // Collect first to avoid mutating the child list while iterating
        var cards = new List<Transform>();
        foreach (Transform child in container)
        {
            RectTransform rt = child.GetComponent<RectTransform>();
            if (rt != null && cardToPlayerId.ContainsKey(rt))
                cards.Add(child);
        }

        foreach (var card in cards)
            card.SetParent(unassignedArea, false);
    }

    // ================================================================
    //  Helper: Find Player Card in Container
    // ================================================================

    /// <summary>
    /// Returns the first player-card RectTransform found in <paramref name="container"/>,
    /// optionally excluding <paramref name="exclude"/> (useful during drag when the
    /// dragged card is still a child of its original parent).
    /// </summary>
    private RectTransform GetPlayerCardInContainer(Transform container, RectTransform exclude = null)
    {
        foreach (Transform child in container)
        {
            RectTransform rt = child.GetComponent<RectTransform>();
            if (rt != null && rt != exclude && cardToPlayerId.ContainsKey(rt))
                return rt;
        }
        return null;
    }

    // ================================================================
    //  Button State Management
    // ================================================================

    /// <summary>
    /// Refreshes the interactable state of +, –, and Next buttons
    /// based on the current layout.
    /// </summary>
    private void RefreshButtons()
    {
        // – is disabled when only 2 groups remain
        if (minusButton != null)
            minusButton.interactable = numberOfGroups > 2;

        // + is disabled when group count equals or exceeds non-DM player count
        if (plusButton != null)
        {
            int nonDMCount = PlayerManager.players.Count - 1;
            plusButton.interactable = numberOfGroups < nonDMCount;
        }

        // Next is disabled if any card is in the unassigned area
        // OR any group container still has an empty placeholder
        if (nextButton != null)
            nextButton.interactable = IsLayoutValid();
    }

    /// <summary>
    /// The layout is valid when:
    ///   1. No name cards remain in the unassigned area.
    ///   2. No debate-group container (index 1+) contains an EmptyFieldInGroupPrefab.
    /// </summary>
    private bool IsLayoutValid()
    {
        // Check unassigned area for any name cards
        foreach (Transform child in unassignedArea)
        {
            RectTransform rt = child.GetComponent<RectTransform>();
            if (rt != null && cardToPlayerId.ContainsKey(rt))
                return false;
        }

        // DM container must have exactly one player
        if (groupContainers.Count > 0 && GetPlayerCardInContainer(groupContainers[0].transform) == null)
            return false;

        // Check debate groups for empty placeholders (skip DM container at index 0)
        for (int i = 1; i < groupContainers.Count; i++)
        {
            if (ContainsEmptyPlaceholder(groupContainers[i].transform))
                return false;
        }

        return true;
    }

    // ================================================================
    //  Committing Layout → PlayerManager Data
    // ================================================================

    /// <summary>
    /// Reads the visual hierarchy and writes group assignments back into
    /// PlayerManager so downstream systems see the player's choices.
    /// </summary>
    private void CommitGroupAssignments()
    {
        PlayerManager.ClearAllGroups();

        // Read the DM from container index 0
        if (groupContainers.Count > 0)
        {
            RectTransform dmCard = GetPlayerCardInContainer(groupContainers[0].transform);
            if (dmCard != null && cardToPlayerId.TryGetValue(dmCard, out int dmPlayerId))
            {
                dmId = dmPlayerId;
                PlayerManager.dmId = dmPlayerId;
            }
        }

        // Skip index 0 (DM container). DM has no group assignment.
        for (int i = 1; i < groupContainers.Count; i++)
        {
            var group = PlayerManager.CreateGroup($"Group {i}");

            foreach (Transform child in groupContainers[i].transform)
            {
                RectTransform rt = child.GetComponent<RectTransform>();
                if (rt != null && cardToPlayerId.TryGetValue(rt, out int playerId))
                {
                    PlayerManager.UpdatePlayerGroup(playerId, group.id);
                }
            }
        }
    }

    // ================================================================
    //  Player Distribution Helpers (used during initial build)
    // ================================================================

    private List<Player> GetOrderedNonDMPlayers(int excludeId)
    {
        return PlayerManager.players.Values
            .Where(p => p.id != excludeId)
            .OrderBy(p => p.id)
            .ToList();
    }

    private List<Player> GetShuffledNonDMPlayers(int excludeId)
    {
        return PlayerManager.players.Values
            .Where(p => p.id != excludeId)
            .OrderBy(_ => Random.Range(0f, 1f))
            .ToList();
    }

    private List<List<Player>> DistributePlayersIntoGroups(List<Player> players, int targetGroupCount)
    {
        targetGroupCount = Mathf.Clamp(targetGroupCount, 1, players.Count);

        var groups = new List<List<Player>>();
        for (int i = 0; i < targetGroupCount; i++)
            groups.Add(new List<Player>());

        for (int i = 0; i < players.Count; i++)
            groups[i % targetGroupCount].Add(players[i]);

        return groups;
    }
}
