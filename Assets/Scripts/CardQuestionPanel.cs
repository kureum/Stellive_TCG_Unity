using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardQuestionPanel : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Text")]
    [SerializeField] private TMP_Text requestText;

    [Header("Card List")]
    [SerializeField] private Transform cardContent;
    [SerializeField] private GameObject cardItemPrefab;

    [Header("Buttons")]
    [SerializeField] private Button selectionButton;
    [SerializeField] private Button cancelButton;

    [Header("Selection Visual")]
    [SerializeField] private Color selectedOutlineColor = new Color(1f, 0.86f, 0.2f, 1f);
    [SerializeField] private Vector2 selectedOutlineDistance = new Vector2(4f, 4f);

    private readonly List<GameObject> spawnedCardItems = new List<GameObject>();
    private readonly List<SelectionOutlineEntry> selectionOutlines = new List<SelectionOutlineEntry>();

    private BaseCardData selectedQuestionCard;
    private CardQuestionOption selectedQuestionOption;
    private Outline selectedQuestionOutline;
    private Action<BaseCardData> onSelectedAction;
    private Action<CardQuestionOption> onOptionSelectedAction;
    private Action onCancelAction;
    private Action<string> systemMessageAction;
    private bool isOpen;
    private bool canCancel;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        Hide();
    }

    public bool IsOpen()
    {
        return isOpen;
    }

    public BaseCardData SelectedQuestionCard
    {
        get { return selectedQuestionCard; }
    }

    public void Configure(GameObject defaultCardItemPrefab, Action<string> messageAction)
    {
        if (cardItemPrefab == null)
            cardItemPrefab = defaultCardItemPrefab;

        systemMessageAction = messageAction;
    }

    public void Show(
        string message,
        List<BaseCardData> cards,
        bool canCancel,
        Action<BaseCardData> onSelected,
        Action onCancel)
    {
        TryShow(message, cards, canCancel, onSelected, onCancel);
    }

    public bool TryShow(
        string message,
        List<BaseCardData> cards,
        bool canCancel,
        Action<BaseCardData> onSelected,
        Action onCancel)
    {
        if (isOpen)
            return false;

        if (cards == null || cards.Count == 0)
        {
            SendSystemMessage("선택 가능한 카드가 없습니다.");
            Hide();
            return false;
        }

        isOpen = true;
        this.canCancel = canCancel;
        selectedQuestionCard = null;
        selectedQuestionOption = null;
        selectedQuestionOutline = null;
        onSelectedAction = onSelected;
        onOptionSelectedAction = null;
        onCancelAction = onCancel;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (requestText != null)
            requestText.text = message;

        ClearCardItems();
        SetupButtons();

        foreach (BaseCardData card in cards)
        {
            if (card == null)
                continue;

            CreateCardItem(card);
        }

        return true;
    }

    public bool TryShowOptions(
        string message,
        List<CardQuestionOption> options,
        bool canCancel,
        Action<CardQuestionOption> onSelected,
        Action onCancel)
    {
        if (isOpen)
            return false;

        if (options == null || options.Count == 0)
        {
            SendSystemMessage("선택 가능한 카드가 없습니다.");
            Hide();
            return false;
        }

        isOpen = true;
        this.canCancel = canCancel;
        selectedQuestionCard = null;
        selectedQuestionOption = null;
        selectedQuestionOutline = null;
        onSelectedAction = null;
        onOptionSelectedAction = onSelected;
        onCancelAction = onCancel;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (requestText != null)
            requestText.text = message;

        ClearCardItems();
        SetupButtons();

        foreach (CardQuestionOption option in options)
        {
            if (option == null || option.card == null)
                continue;

            CreateOptionItem(option);
        }

        return true;
    }

    public void Hide()
    {
        isOpen = false;
        canCancel = false;
        selectedQuestionCard = null;
        selectedQuestionOption = null;
        selectedQuestionOutline = null;
        onSelectedAction = null;
        onOptionSelectedAction = null;
        onCancelAction = null;

        ClearLinkedSlotHighlights();
        ClearCardItems();
        ClearButtons();

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void SetupButtons()
    {
        if (selectionButton != null)
        {
            selectionButton.onClick.RemoveAllListeners();
            selectionButton.interactable = true;
            selectionButton.onClick.AddListener(ConfirmCurrentSelection);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.interactable = canCancel;

            if (canCancel)
                cancelButton.onClick.AddListener(Cancel);
        }
    }

    private void ClearButtons()
    {
        if (selectionButton != null)
        {
            selectionButton.onClick.RemoveAllListeners();
            selectionButton.interactable = true;
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.interactable = true;
        }
    }

    private void CreateCardItem(BaseCardData card)
    {
        if (cardContent == null)
        {
            Debug.LogWarning("CardQuestionPanel: CardQuestionContent가 연결되어 있지 않습니다.");
            return;
        }

        GameObject itemObject = cardItemPrefab != null
            ? Instantiate(cardItemPrefab, cardContent)
            : CreateFallbackCardItem(cardContent);

        spawnedCardItems.Add(itemObject);

        Outline outline = GetOrCreateSelectionOutline(itemObject);
        outline.enabled = false;
        selectionOutlines.Add(new SelectionOutlineEntry(card, outline));

        DeckCardItemUI cardItemUI = itemObject.GetComponent<DeckCardItemUI>();

        if (cardItemUI == null)
            cardItemUI = itemObject.AddComponent<DeckCardItemUI>();

        cardItemUI.SetCard(
            card,
            leftClickAction: selectedCard => SelectQuestionCard(selectedCard, outline),
            rightClickAction: null,
            doubleClickAction: selectedCard => ConfirmCardByDoubleClick(selectedCard, outline)
        );

        cardItemUI.SetDragActions(false);
    }

    private void CreateOptionItem(CardQuestionOption option)
    {
        if (cardContent == null)
        {
            Debug.LogWarning("CardQuestionPanel: CardQuestionContent가 연결되어 있지 않습니다.");
            return;
        }

        GameObject itemObject = cardItemPrefab != null
            ? Instantiate(cardItemPrefab, cardContent)
            : CreateFallbackCardItem(cardContent);

        spawnedCardItems.Add(itemObject);

        Outline outline = GetOrCreateSelectionOutline(itemObject);
        outline.enabled = false;
        selectionOutlines.Add(new SelectionOutlineEntry(option.card, outline, option));

        DeckCardItemUI cardItemUI = itemObject.GetComponent<DeckCardItemUI>();

        if (cardItemUI == null)
            cardItemUI = itemObject.AddComponent<DeckCardItemUI>();

        cardItemUI.SetCard(
            option.card,
            leftClickAction: selectedCard => SelectQuestionOption(option, outline),
            rightClickAction: null,
            doubleClickAction: selectedCard => ConfirmOptionByDoubleClick(option, outline)
        );

        cardItemUI.SetDragActions(false);
    }

    private Outline GetOrCreateSelectionOutline(GameObject itemObject)
    {
        Outline outline = itemObject.GetComponent<Outline>();

        if (outline == null)
            outline = itemObject.AddComponent<Outline>();

        outline.effectColor = selectedOutlineColor;
        outline.effectDistance = selectedOutlineDistance;
        outline.useGraphicAlpha = false;

        return outline;
    }

    private GameObject CreateFallbackCardItem(Transform parent)
    {
        GameObject itemObject = new GameObject("CardQuestionItem", typeof(RectTransform), typeof(Image), typeof(Button));
        itemObject.transform.SetParent(parent, false);

        RectTransform rect = itemObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(90f, 122f);

        Image image = itemObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.25f);
        image.raycastTarget = true;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(itemObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(6f, 6f);
        textRect.offsetMax = new Vector2(-6f, -6f);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 14f;
        text.textWrappingMode = TextWrappingModes.Normal;

        return itemObject;
    }

    private void SelectQuestionCard(BaseCardData card, Outline outline)
    {
        if (card == null)
            return;

        selectedQuestionCard = card;
        selectedQuestionOption = null;
        selectedQuestionOutline = outline;
        ClearLinkedSlotHighlights();
        RefreshSelectionVisual();
        SendSystemMessage($"선택 카드: {card.name}");
    }

    private void ConfirmCardByDoubleClick(BaseCardData card, Outline outline)
    {
        if (card == null)
            return;

        selectedQuestionCard = card;
        selectedQuestionOption = null;
        selectedQuestionOutline = outline;
        ClearLinkedSlotHighlights();
        RefreshSelectionVisual();
        ConfirmCurrentSelection();
    }

    private void SelectQuestionOption(CardQuestionOption option, Outline outline)
    {
        if (option == null || option.card == null)
            return;

        selectedQuestionCard = option.card;
        selectedQuestionOption = option;
        selectedQuestionOutline = outline;
        RefreshLinkedSlotHighlight(option);
        RefreshSelectionVisual();
        SendSystemMessage($"선택 카드: {option.card.name}");
    }

    private void ConfirmOptionByDoubleClick(CardQuestionOption option, Outline outline)
    {
        if (option == null || option.card == null)
            return;

        selectedQuestionCard = option.card;
        selectedQuestionOption = option;
        selectedQuestionOutline = outline;
        RefreshLinkedSlotHighlight(option);
        RefreshSelectionVisual();
        ConfirmCurrentSelection();
    }

    private void ConfirmCurrentSelection()
    {
        if (selectedQuestionCard == null)
        {
            SendSystemMessage("카드를 선택하세요.");
            return;
        }

        BaseCardData selectedCard = selectedQuestionCard;
        CardQuestionOption selectedOption = selectedQuestionOption;
        Action<BaseCardData> selectedAction = onSelectedAction;
        Action<CardQuestionOption> optionSelectedAction = onOptionSelectedAction;

        Hide();

        if (optionSelectedAction != null)
            optionSelectedAction.Invoke(selectedOption);
        else
            selectedAction?.Invoke(selectedCard);
    }

    private void Cancel()
    {
        if (!canCancel)
            return;

        Action cancelAction = onCancelAction;

        Hide();
        cancelAction?.Invoke();
    }

    private void RefreshSelectionVisual()
    {
        foreach (SelectionOutlineEntry entry in selectionOutlines)
        {
            if (entry.outline == null)
                continue;

            entry.outline.enabled = entry.outline == selectedQuestionOutline;
        }
    }

    private void ClearCardItems()
    {
        for (int i = 0; i < spawnedCardItems.Count; i++)
        {
            if (spawnedCardItems[i] != null)
                Destroy(spawnedCardItems[i]);
        }

        spawnedCardItems.Clear();
        selectionOutlines.Clear();
        selectedQuestionOutline = null;
    }

    private void RefreshLinkedSlotHighlight(CardQuestionOption selectedOption)
    {
        foreach (SelectionOutlineEntry entry in selectionOutlines)
        {
            if (entry.option == null || entry.option.linkedSlot == null)
                continue;

            entry.option.linkedSlot.SetQuestionTargetHighlight(entry.option == selectedOption);
        }
    }

    private void ClearLinkedSlotHighlights()
    {
        foreach (SelectionOutlineEntry entry in selectionOutlines)
        {
            if (entry.option == null || entry.option.linkedSlot == null)
                continue;

            entry.option.linkedSlot.SetQuestionTargetHighlight(false);
        }
    }

    private void SendSystemMessage(string message)
    {
        if (systemMessageAction != null)
            systemMessageAction.Invoke(message);
        else
            Debug.Log(message);
    }

    private class SelectionOutlineEntry
    {
        public readonly BaseCardData card;
        public readonly Outline outline;
        public readonly CardQuestionOption option;

        public SelectionOutlineEntry(BaseCardData card, Outline outline, CardQuestionOption option = null)
        {
            this.card = card;
            this.outline = outline;
            this.option = option;
        }
    }
}
