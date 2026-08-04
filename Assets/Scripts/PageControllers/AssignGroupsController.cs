using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Assign Groups screen. Players are distributed into draggable groups
/// and the DM is placed in a fixed Discussion Moderator container. A persistent
/// ghost container at the bottom accepts drops to create a new group; real groups
/// auto-delete when emptied.
/// </summary>
public class AssignGroupsController : MonoBehaviour
{
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
    [SerializeField] private GameObject errorDisplay;

    private int dmId;

    /// <summary>Number of real debate groups, excluding the DM and ghost containers.</summary>
    public int numberOfGroups = 2;

    /// <summary>Live list of group container GameObjects; index 0 is the DM container.</summary>
    private List<GameObject> groupContainers = new List<GameObject>();

    /// <summary>The empty group container at the bottom. Dropping a card here promotes it into a real group.</summary>
    private GameObject ghostGroupContainer;

    /// <summary>Maps each NameInGroupPrefab instance to its player ID.</summary>
    private Dictionary<RectTransform, int> cardToPlayerId = new Dictionary<RectTransform, int>();

    private Image currentHighlightedGroup;
    private Color highlightOriginalColor;

    private static readonly Color HighlightTint = new Color(0.8f, 0.95f, 1f, 1f);

    private bool hasBeenInitialized = false;
    private bool hasDuplicateGroupName = false;

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

    /// <summary>Proceeds to the next game state. Disabled while the layout is invalid.</summary>
    public void Next()
    {
        CommitGroupAssignments();
        gameManager.corruptionManager.AssignCorruptionsToPlayers(PlayerManager.players, dmId);
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
    /// Reshuffles non-DM players across the current number of groups.
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
    /// Clears the initialization flag so the next OnEnable performs a fresh build.
    /// Invoked by <see cref="GameManager.NewGame"/>.
    /// </summary>
    public void ResetInitialization()
    {
        hasBeenInitialized = false;
        numberOfGroups = 2;
    }

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
        hasDuplicateGroupName = false;
        if (errorDisplay != null) errorDisplay.SetActive(false);
    }

    /// <summary>Removes all name cards and empty placeholders from a container.</summary>
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
    /// Instantiates the Discussion Moderator container as <c>groupContainers[0]</c>.
    /// The group name is displayed as static text with no input field.
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
    /// Rebuilds the screen while preserving the player-to-group layout and custom
    /// group names. New players land in the unassigned area; removed players are
    /// silently skipped.
    /// </summary>
    private void RebuildScreenPreservingLayout()
    {
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

            // Restores the custom name only when it differs from both default labels.
            if (groupCustomNames.TryGetValue(g, out string savedName)
                && savedName != $"Group {g}" && savedName != $"Hópur {g}")
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

        CleanupEmptyGroups();

        CreateGhostGroupContainer();
        RefreshButtons();
        ForceLayoutRebuild();
    }

    private void ForceLayoutRebuild()
    {
        if (groupsParent is RectTransform groupsRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(groupsRect);
        if (groupsParent.parent is RectTransform contentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    /// <summary>
    /// Instantiates a debate group container, sets its placeholder label, wires the
    /// Edit button, and tracks it in <c>groupContainers</c>. A
    /// <paramref name="groupNumber"/> of 0 represents the DM container; 1+ represents
    /// the labelled debate groups.
    /// </summary>
    private GameObject CreateGroupContainer(int groupNumber, string overrideLabel = null)
    {
        GameObject container = Instantiate(groupContainerPrefab, groupsParent);
        string placeholderLabel = overrideLabel ?? GroupLabel(groupNumber);
        SetContainerPlaceholder(container, placeholderLabel);
        SetupEditButton(container);
        Instantiate(emptyFieldInGroupPrefab, container.transform);
        groupContainers.Add(container);
        return container;
    }

    /// <summary>
    /// Creates the ghost group container, the empty drop target at the bottom. It is
    /// not added to <c>groupContainers</c> until promoted.
    /// </summary>
    private void CreateGhostGroupContainer()
    {
        int ghostNumber = groupContainers.Count;
        ghostGroupContainer = Instantiate(groupContainerPrefab, groupsParent);
        SetContainerPlaceholder(ghostGroupContainer, GroupLabel(ghostNumber));
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
    /// Returns the typed text if non-empty, otherwise the placeholder text. This is
    /// the value committed to PlayerManager as the group name.
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
    /// Wires the Edit Button to focus the name input field and locks the typed-text
    /// font size to 72pt.
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

        // The prefab's typed-text TMP component defaults to 14pt; lock it to 72pt.
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

        inputField.onValueChanged.AddListener(_ => RefreshButtons());
    }

    /// <summary>
    /// Updates the placeholder labels on every debate group and the ghost so they
    /// match the current order. Custom names typed by the user are unaffected.
    /// </summary>
    private void RenumberGroups()
    {
        for (int i = 1; i < groupContainers.Count; i++)
            SetContainerPlaceholder(groupContainers[i], GroupLabel(i));
        if (ghostGroupContainer != null)
            SetContainerPlaceholder(ghostGroupContainer, GroupLabel(groupContainers.Count));
    }

    /// <summary>
    /// Destroys any debate group containers that ended up empty after a rebuild.
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

    /// <summary>Adds an empty placeholder to a container when it has no name-card children. Used for the DM container.</summary>
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
    /// Deletes a debate group container when it ends up empty, then renumbers the
    /// remaining groups. No-op for the unassigned area, DM container, and ghost
    /// container.
    /// </summary>
    private void TryAutoDeleteIfEmpty(Transform container)
    {
        if (container == null) return;
        if (container == unassignedArea) return;
        if (ghostGroupContainer != null && container.gameObject == ghostGroupContainer) return;
        if (groupContainers.Count > 0 && container.gameObject == groupContainers[0]) return;
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

    /// <summary>Invoked when a drag begins on a name card.</summary>
    public void OnCardDragBegin(RectTransform card, Transform originalParent) { }

    /// <summary>Invoked every frame during a drag to highlight the hovered group.</summary>
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

    /// <summary>Invoked when the drag ends; decides where the card lands.</summary>
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
            // Promotes the ghost to a real group.
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
                // DM container holds exactly one player; swap when occupied.
                RectTransform existingCard = GetPlayerCardInContainer(dropTarget, card);
                if (existingCard != null)
                {
                    RemoveEmptyPlaceholder(originalParent);
                    existingCard.SetParent(originalParent, false);
                    existingCard.SetSiblingIndex(originalSiblingIndex);
                }
                else
                {
                    TryAutoDeleteIfEmpty(originalParent);
                }

                RemoveEmptyPlaceholder(dropTarget);
                card.SetParent(dropTarget, false);
            }
            else
            {
                RemoveEmptyPlaceholder(dropTarget);
                card.SetParent(dropTarget, false);
                TryAutoDeleteIfEmpty(originalParent);
            }
        }
        else
        {
            card.SetParent(originalParent, false);
            card.SetSiblingIndex(originalSiblingIndex);
        }

        // Keeps a placeholder visible in the DM container when empty for drop-target feedback.
        if (groupContainers.Count > 0)
            EnsurePlaceholderIfEmpty(groupContainers[0].transform);

        RefreshButtons();
        ForceLayoutRebuild();
    }

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

    private void RefreshButtons()
    {
        CheckForDuplicateGroupNames();
        if (nextButton != null)
            nextButton.interactable = IsLayoutValid() && !hasDuplicateGroupName;
    }

    private void CheckForDuplicateGroupNames()
    {
        var names = new List<string>();
        for (int i = 1; i < groupContainers.Count; i++)
            names.Add(GetContainerEffectiveName(groupContainers[i]).ToLowerInvariant());

        bool hasDup = names.Count != names.Distinct().Count();
        hasDuplicateGroupName = hasDup;
        if (errorDisplay != null) errorDisplay.SetActive(hasDup);
    }

    /// <summary>
    /// Returns true when there are at least two debate groups, the unassigned area is
    /// empty, the DM container holds exactly one player, and no debate group still has
    /// an empty placeholder.
    /// </summary>
    private bool IsLayoutValid()
    {
        if (groupContainers.Count < 3)
            return false;

        foreach (Transform child in unassignedArea)
        {
            var rt = child.GetComponent<RectTransform>();
            if (rt != null && cardToPlayerId.ContainsKey(rt))
                return false;
        }

        if (GetPlayerCardInContainer(groupContainers[0].transform) == null)
            return false;

        for (int i = 1; i < groupContainers.Count; i++)
        {
            if (ContainsEmptyPlaceholder(groupContainers[i].transform))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Reads the visual hierarchy and writes the group assignments back to
    /// PlayerManager. Group names use the typed text when present, otherwise the
    /// placeholder label.
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

    private string GroupLabel(int number)
    {
        if (gameManager != null && gameManager.selectedLanguage == GameManager.Language.Icelandic)
            return $"Hópur {number}";
        return $"Group {number}";
    }

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
