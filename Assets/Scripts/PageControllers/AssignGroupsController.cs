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
/// A persistent "ghost" group container at the bottom accepts card drops to
/// create a new group; real groups auto-delete when emptied.
/// </summary>
public class AssignGroupsController : MonoBehaviour
{
    // -------------------- Inspector References --------------------

    [Header("Managers")]
    private GameManager gameManager;
    private PlayerManager PlayerManager => gameManager.playerManager;

    [Header("Prefabs")]
    [Tooltip("Container with VerticalLayoutGroup + ContentSizeFitter. Has Group Name Container child with InputField and Edit Button.")]
    [SerializeField] private GameObject groupContainerPrefab;

    [Tooltip("Display-only container. Group name is shown as static text (no input field). Used for the Discussion Moderator slot.")]
    [SerializeField] private GameObject groupDisplayPrefab;

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
    [SerializeField] private Button nextButton;

    // -------------------- Runtime State --------------------

    private int dmId;

    /// <summary>Number of real debate groups (excludes DM container and ghost).</summary>
    public int numberOfGroups = 2;

    /// <summary>Live list of real group container GameObjects (index 0 = DM container).</summary>
    private List<GameObject> groupContainers = new List<GameObject>();

    /// <summary>The always-present empty group at the bottom — not in groupContainers. Dropping a card here creates a new real group.</summary>
    private GameObject ghostGroupContainer;

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
        CommitGroupAssignments();
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

    /// <summary>
    /// Randomizes all non-DM players into the current number of groups.
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

        GameObject dmContainer = SetupDMContainer();
        CreateNameCard(PlayerManager.players[dmId], dmContainer.transform, draggable: true);

        var distributed = DistributePlayersIntoGroups(nonDMPlayers, numberOfGroups);
        for (int g = 0; g < distributed.Count; g++)
        {
            GameObject container = CreateGroupContainer(g + 1);
            foreach (var player in distributed[g])
                CreateNameCard(player, container.transform, draggable: true);
        }

        CreateGhostGroupContainer();
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

    private void BuildScreen()
    {
        ClearScreen();

        dmId = PlayerManager.players.Keys.Min();
        var nonDMPlayers = GetOrderedNonDMPlayers(dmId);

        numberOfGroups = 2;
        if (nonDMPlayers.Count < 2)
            numberOfGroups = Mathf.Max(1, nonDMPlayers.Count);

        GameObject dmContainer = SetupDMContainer();
        CreateNameCard(PlayerManager.players[dmId], dmContainer.transform, draggable: true);

        var distributed = DistributePlayersIntoGroups(nonDMPlayers, numberOfGroups);
        for (int g = 0; g < distributed.Count; g++)
        {
            GameObject container = CreateGroupContainer(g + 1);
            foreach (var player in distributed[g])
                CreateNameCard(player, container.transform, draggable: true);
        }

        CreateGhostGroupContainer();
        RefreshButtons();
        ForceLayoutRebuild();
    }

    private void ClearScreen()
    {
        foreach (Transform child in groupsParent)
            Destroy(child.gameObject);

        foreach (Transform child in unassignedArea)
            Destroy(child.gameObject);

        groupContainers.Clear();
        cardToPlayerId.Clear();
        ghostGroupContainer = null;
    }

    /// <summary>Removes all player name-cards and empty placeholders from a container without destroying the container itself.</summary>
    private void ClearPlayerCardsFrom(Transform container)
    {
        var toDestroy = new List<GameObject>();
        foreach (Transform child in container)
        {
            var rt = child.GetComponent<RectTransform>();
            if ((rt != null && cardToPlayerId.ContainsKey(rt)) || IsEmptyPlaceholder(child.gameObject))
                toDestroy.Add(child.gameObject);
        }
        foreach (var go in toDestroy)
            DestroyImmediate(go);
    }

    /// <summary>
    /// Instantiates the Group Display Prefab as the Discussion Moderator container (groupContainers[0]).
    /// The group name is shown as static text — no input field or edit button.
    /// </summary>
    private GameObject SetupDMContainer()
    {
        GameObject container = Instantiate(groupDisplayPrefab, groupsParent);
        SetDisplayLabel(container, "Discussion Moderator");
        Instantiate(emptyFieldInGroupPrefab, container.transform);
        groupContainers.Add(container);
        return container;
    }

    /// <summary>
    /// Rebuilds the screen while preserving the player-to-group layout and custom group names.
    /// New players are placed in the unassigned area; removed players are silently skipped.
    /// </summary>
    private void RebuildScreenPreservingLayout()
    {
        // Snapshot current layout
        var playerToGroup = new Dictionary<int, int>();
        var unassignedPlayerIds = new HashSet<int>();
        var groupCustomNames = new Dictionary<int, string>();

        for (int i = 0; i < groupContainers.Count; i++)
        {
            foreach (Transform child in groupContainers[i].transform)
            {
                var rt = child.GetComponent<RectTransform>();
                if (rt != null && cardToPlayerId.TryGetValue(rt, out int pid))
                    playerToGroup[pid] = i;
            }
            if (i > 0)
                groupCustomNames[i] = GetContainerEffectiveName(groupContainers[i]);
        }

        foreach (Transform child in unassignedArea)
        {
            var rt = child.GetComponent<RectTransform>();
            if (rt != null && cardToPlayerId.TryGetValue(rt, out int pid))
                unassignedPlayerIds.Add(pid);
        }

        ClearScreen();

        var allPlayerIds = new HashSet<int>(PlayerManager.players.Keys);
        var placedIds = new HashSet<int>();

        var previousDM = playerToGroup.Where(kvp => kvp.Value == 0 && allPlayerIds.Contains(kvp.Key));
        dmId = previousDM.Any() ? previousDM.First().Key : allPlayerIds.Min();

        int nonDMCount = allPlayerIds.Count - 1;
        if (nonDMCount < 2)
            numberOfGroups = Mathf.Max(1, nonDMCount);
        else
            numberOfGroups = Mathf.Clamp(numberOfGroups, 2, nonDMCount);

        var dmContainer = SetupDMContainer();
        CreateNameCard(PlayerManager.players[dmId], dmContainer.transform, draggable: true);
        placedIds.Add(dmId);

        for (int g = 1; g <= numberOfGroups; g++)
        {
            var container = CreateGroupContainer(g);

            // Restore custom name only if it differs from the default placeholder
            if (groupCustomNames.TryGetValue(g, out string savedName) && savedName != $"Group {g}")
                SetContainerCustomName(container, savedName);

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

        foreach (int pid in unassignedPlayerIds)
        {
            if (allPlayerIds.Contains(pid) && !placedIds.Contains(pid))
            {
                CreateNameCard(PlayerManager.players[pid], unassignedArea, draggable: true);
                placedIds.Add(pid);
            }
        }

        foreach (int pid in allPlayerIds)
        {
            if (!placedIds.Contains(pid))
                CreateNameCard(PlayerManager.players[pid], unassignedArea, draggable: true);
        }

        // Remove groups that ended up empty (e.g. players were removed from the game)
        CleanupEmptyGroups();

        CreateGhostGroupContainer();
        RefreshButtons();
        ForceLayoutRebuild();
    }

    // ================================================================
    //  Layout Rebuild
    // ================================================================

    private void ForceLayoutRebuild()
    {
        if (groupsParent is RectTransform groupsRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(groupsRect);
        if (groupsParent.parent is RectTransform contentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    // ================================================================
    //  Group Container Helpers
    // ================================================================

    /// <summary>
    /// Instantiates a real GroupContainerPrefab, sets its placeholder label, wires the
    /// Edit button, and tracks it in groupContainers.
    /// <paramref name="groupNumber"/> 0 = DM, 1+ = "Group N".
    /// </summary>
    private GameObject CreateGroupContainer(int groupNumber, string overrideLabel = null)
    {
        GameObject container = Instantiate(groupContainerPrefab, groupsParent);
        string placeholderLabel = overrideLabel ?? $"Group {groupNumber}";
        SetContainerPlaceholder(container, placeholderLabel);
        SetupEditButton(container);
        Instantiate(emptyFieldInGroupPrefab, container.transform);
        groupContainers.Add(container);
        return container;
    }

    /// <summary>
    /// Creates (or recreates) the ghost group container — the always-present empty drop
    /// target at the bottom. It is NOT added to groupContainers.
    /// </summary>
    private void CreateGhostGroupContainer()
    {
        // ghostNumber = how many real groups exist (excluding DM), +1
        int ghostNumber = groupContainers.Count; // [DM, G1, G2] → count=3 → ghost = Group 3
        ghostGroupContainer = Instantiate(groupContainerPrefab, groupsParent);
        SetContainerPlaceholder(ghostGroupContainer, $"Group {ghostNumber}");
        SetupEditButton(ghostGroupContainer);
        Instantiate(emptyFieldInGroupPrefab, ghostGroupContainer.transform);
    }

    /// <summary>Sets the "Title" TextMeshProUGUI on a Group Display Prefab container.</summary>
    private void SetDisplayLabel(GameObject container, string label)
    {
        Transform titleTransform = container.transform.Find("Title");
        if (titleTransform == null) return;
        var tmp = titleTransform.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = label;
    }

    /// <summary>Sets the placeholder text of the TMP_InputField inside the Group Name Container.</summary>
    private void SetContainerPlaceholder(GameObject container, string label)
    {
        var inputField = container.transform
            .Find("Group Name Container/InputField (TMP)")
            ?.GetComponent<TMP_InputField>();
        if (inputField == null) return;
        var placeholder = inputField.placeholder as TextMeshProUGUI;
        if (placeholder != null) placeholder.text = label;
    }

    /// <summary>Sets the typed text of the TMP_InputField (used when restoring a custom name).</summary>
    private void SetContainerCustomName(GameObject container, string name)
    {
        var inputField = container.transform
            .Find("Group Name Container/InputField (TMP)")
            ?.GetComponent<TMP_InputField>();
        if (inputField != null) inputField.text = name;
    }

    /// <summary>
    /// Returns the typed text if non-empty, otherwise returns the placeholder text.
    /// This is what gets committed to PlayerManager as the group name.
    /// </summary>
    private string GetContainerEffectiveName(GameObject container)
    {
        var inputField = container.transform
            .Find("Group Name Container/InputField (TMP)")
            ?.GetComponent<TMP_InputField>();
        if (inputField != null && !string.IsNullOrWhiteSpace(inputField.text))
            return inputField.text.Trim();
        var placeholder = inputField?.placeholder as TextMeshProUGUI;
        return placeholder?.text ?? "Group";
    }

    /// <summary>
    /// Wires the Edit Button to focus the name input field and fixes the typed-text
    /// font size to match the placeholder (auto-size, min 72).
    /// </summary>
    private void SetupEditButton(GameObject container)
    {
        var editButton = container.transform
            .Find("Group Name Container/Edit Button")
            ?.GetComponent<Button>();
        var inputField = container.transform
            .Find("Group Name Container/InputField (TMP)")
            ?.GetComponent<TMP_InputField>();
        if (editButton == null || inputField == null) return;

        // The prefab's typed-text TMP component defaults to 14pt — fix it to match the placeholder.
        var textTMP = inputField.textComponent;
        if (textTMP != null)
        {
            textTMP.enableAutoSizing = true;
            textTMP.fontSizeMin = 72;
            textTMP.fontSizeMax = 72;
        }

        editButton.onClick.AddListener(() =>
        {
            inputField.ActivateInputField();
            inputField.Select();
        });
    }

    /// <summary>
    /// Updates placeholder labels on all real debate groups (index 1+) and the ghost
    /// so they reflect the current group order after additions or deletions.
    /// Custom names typed by the user are unaffected (only placeholder text changes).
    /// </summary>
    private void RenumberGroups()
    {
        for (int i = 1; i < groupContainers.Count; i++)
            SetContainerPlaceholder(groupContainers[i], $"Group {i}");
        if (ghostGroupContainer != null)
            SetContainerPlaceholder(ghostGroupContainer, $"Group {groupContainers.Count}");
    }

    /// <summary>
    /// After a rebuild, destroys any real group containers (index 1+) that ended up
    /// empty because players were removed from the game.
    /// </summary>
    private void CleanupEmptyGroups()
    {
        var toDelete = new List<GameObject>();
        for (int i = 1; i < groupContainers.Count; i++)
        {
            bool hasCards = false;
            foreach (Transform child in groupContainers[i].transform)
            {
                var rt = child.GetComponent<RectTransform>();
                if (rt != null && cardToPlayerId.ContainsKey(rt)) { hasCards = true; break; }
            }
            if (!hasCards) toDelete.Add(groupContainers[i]);
        }

        foreach (var container in toDelete)
        {
            groupContainers.Remove(container);
            numberOfGroups--;
            Destroy(container);
        }

        if (toDelete.Count > 0) RenumberGroups();
    }

    // ================================================================
    //  Name Card Helpers
    // ================================================================

    private RectTransform CreateNameCard(Player player, Transform parent, bool draggable)
    {
        RemoveEmptyPlaceholder(parent);
        GameObject card = Instantiate(nameInGroupPrefab, parent);
        SetCardName(card, player.name);
        RectTransform rt = card.GetComponent<RectTransform>();
        cardToPlayerId[rt] = player.id;

        foreach (var childHandle in card.GetComponentsInChildren<DragHandle>(true))
            DestroyImmediate(childHandle);

        DragHandle handle = card.AddComponent<DragHandle>();
        handle.nameCard = rt;
        handle.Initialize(this);
        handle.enabled = draggable;

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

    /// <summary>Adds an empty placeholder to a container if it has no name-card children. Used for DM container only.</summary>
    private void EnsurePlaceholderIfEmpty(Transform container)
    {
        if (container == unassignedArea) return;

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
            Instantiate(emptyFieldInGroupPrefab, container);
    }

    /// <summary>
    /// If a real debate group container (not DM, not ghost) is now empty, deletes it
    /// and renumbers the remaining groups. No-op for unassigned area, DM container,
    /// ghost container, and anything not in groupContainers.
    /// </summary>
    private void TryAutoDeleteIfEmpty(Transform container)
    {
        if (container == null) return;
        if (container == unassignedArea) return;
        if (ghostGroupContainer != null && container.gameObject == ghostGroupContainer) return;
        if (groupContainers.Count > 0 && container.gameObject == groupContainers[0]) return; // DM
        if (!groupContainers.Contains(container.gameObject)) return;

        bool hasCards = false;
        foreach (Transform child in container)
        {
            var rt = child.GetComponent<RectTransform>();
            if (rt != null && cardToPlayerId.ContainsKey(rt)) { hasCards = true; break; }
        }

        if (!hasCards)
        {
            groupContainers.Remove(container.gameObject);
            numberOfGroups--;
            Destroy(container.gameObject);
            RenumberGroups();
        }
    }

    private bool ContainsEmptyPlaceholder(Transform container)
    {
        foreach (Transform child in container)
        {
            if (IsEmptyPlaceholder(child.gameObject)) return true;
        }
        return false;
    }

    private bool IsEmptyPlaceholder(GameObject obj)
    {
        return obj.name.StartsWith(emptyFieldInGroupPrefab.name);
    }

    // ================================================================
    //  Drag & Drop API  (called by DragHandle)
    // ================================================================

    /// <summary>Called when a drag begins on a name card.</summary>
    public void OnCardDragBegin(RectTransform card, Transform originalParent) { }

    /// <summary>Called every frame during a drag to highlight the hovered group.</summary>
    public void OnCardDragUpdate(PointerEventData eventData)
    {
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        Transform hoveredGroup = null;
        bool hoveringUnassigned = false;

        foreach (var hit in results)
        {
            if (hit.gameObject.transform == unassignedArea ||
                hit.gameObject.transform.IsChildOf(unassignedArea))
            {
                hoveringUnassigned = true;
                break;
            }

            Transform candidate = hit.gameObject.transform;
            while (candidate != null)
            {
                if (groupContainers.Contains(candidate.gameObject) ||
                    (ghostGroupContainer != null && candidate.gameObject == ghostGroupContainer))
                {
                    hoveredGroup = candidate;
                    break;
                }
                candidate = candidate.parent;
            }
            if (hoveredGroup != null) break;
        }

        ClearHighlight();
        if (hoveredGroup != null)
            ApplyHighlight(hoveredGroup.GetComponent<Image>());
        else if (hoveringUnassigned)
            ApplyHighlight(unassignedArea.GetComponent<Image>());
    }

    /// <summary>Called when the drag ends. Decides where the card should land.</summary>
    public void OnCardDrop(RectTransform card, Transform originalParent, int originalSiblingIndex, PointerEventData eventData)
    {
        ClearHighlight();

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        Transform dropTarget = null;
        bool droppedOnUnassigned = false;
        bool droppedOnGhost = false;

        foreach (var hit in results)
        {
            if (hit.gameObject.transform == unassignedArea ||
                hit.gameObject.transform.IsChildOf(unassignedArea))
            {
                droppedOnUnassigned = true;
                break;
            }

            Transform candidate = hit.gameObject.transform;
            while (candidate != null)
            {
                if (groupContainers.Contains(candidate.gameObject))
                {
                    dropTarget = candidate;
                    break;
                }
                if (ghostGroupContainer != null && candidate.gameObject == ghostGroupContainer)
                {
                    dropTarget = candidate;
                    droppedOnGhost = true;
                    break;
                }
                candidate = candidate.parent;
            }
            if (dropTarget != null) break;
        }

        if (droppedOnUnassigned)
        {
            card.SetParent(unassignedArea, false);
            TryAutoDeleteIfEmpty(originalParent);
        }
        else if (droppedOnGhost && dropTarget != null)
        {
            // Promote ghost to a real group
            numberOfGroups++;
            groupContainers.Add(ghostGroupContainer);
            ghostGroupContainer = null;

            RemoveEmptyPlaceholder(dropTarget);
            card.SetParent(dropTarget, false);
            TryAutoDeleteIfEmpty(originalParent);

            CreateGhostGroupContainer();
            RenumberGroups();
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
                    // Send the current DM back to where the dragged card came from
                    RemoveEmptyPlaceholder(originalParent);
                    existingCard.SetParent(originalParent, false);
                    existingCard.SetSiblingIndex(originalSiblingIndex);
                    // originalParent now has existingCard, so it won't be empty
                }
                else
                {
                    // DM slot was vacant; original container may now be empty
                    TryAutoDeleteIfEmpty(originalParent);
                }

                RemoveEmptyPlaceholder(dropTarget);
                card.SetParent(dropTarget, false);
            }
            else
            {
                // Normal debate group drop
                RemoveEmptyPlaceholder(dropTarget);
                card.SetParent(dropTarget, false);
                TryAutoDeleteIfEmpty(originalParent);
            }
        }
        else
        {
            // Invalid drop — return card to its original position
            card.SetParent(originalParent, false);
            card.SetSiblingIndex(originalSiblingIndex);
        }

        // Keep a placeholder in the DM container when it's empty so it has visual drop-target feedback
        if (groupContainers.Count > 0)
            EnsurePlaceholderIfEmpty(groupContainers[0].transform);

        RefreshButtons();
        ForceLayoutRebuild();
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
    //  Helper: Find Player Card in Container
    // ================================================================

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

    private void RefreshButtons()
    {
        if (nextButton != null)
            nextButton.interactable = IsLayoutValid();
    }

    /// <summary>
    /// Layout is valid when:
    ///   1. At least 2 real debate groups exist.
    ///   2. No name cards remain in the unassigned area.
    ///   3. DM container has exactly one player.
    ///   4. No debate group contains an empty placeholder.
    /// </summary>
    private bool IsLayoutValid()
    {
        // Require at least 2 debate groups (index 0 = DM, so count must be ≥ 3)
        if (groupContainers.Count < 3)
            return false;

        // No cards in unassigned area
        foreach (Transform child in unassignedArea)
        {
            var rt = child.GetComponent<RectTransform>();
            if (rt != null && cardToPlayerId.ContainsKey(rt))
                return false;
        }

        // DM container must have exactly one player
        if (GetPlayerCardInContainer(groupContainers[0].transform) == null)
            return false;

        // No debate group may be empty
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
    /// Group names use the typed text if provided, otherwise the placeholder default.
    /// </summary>
    private void CommitGroupAssignments()
    {
        PlayerManager.ClearAllGroups();

        if (groupContainers.Count > 0)
        {
            RectTransform dmCard = GetPlayerCardInContainer(groupContainers[0].transform);
            if (dmCard != null && cardToPlayerId.TryGetValue(dmCard, out int dmPlayerId))
            {
                dmId = dmPlayerId;
                PlayerManager.dmId = dmPlayerId;
            }
        }

        for (int i = 1; i < groupContainers.Count; i++)
        {
            string groupName = GetContainerEffectiveName(groupContainers[i]);
            var group = PlayerManager.CreateGroup(groupName);

            foreach (Transform child in groupContainers[i].transform)
            {
                var rt = child.GetComponent<RectTransform>();
                if (rt != null && cardToPlayerId.TryGetValue(rt, out int playerId))
                    PlayerManager.UpdatePlayerGroup(playerId, group.id);
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
