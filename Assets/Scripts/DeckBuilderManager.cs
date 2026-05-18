using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeckBuilderManager : MonoBehaviour
{
    [Header("Buttons")]
    public Button addCardButton;
    public Button removeCardButton;

    [Header("Preset Buttons")]
    public Button[] presetButtons;
    public Button saveDeckButton;

    [Header("Card Database")]
    public CardListManager cardListManager;

    [Header("Detail Panel")]
    public CardDetailPanel cardDetailPanel;

    [Header("Deck Areas")]
    public Transform idolArea;
    public Transform broadcastArea;
    public Transform mainArea;

    [Header("Deck Card Prefab")]
    public GameObject deckCardItemPrefab;

    [Header("Status Text")]
    public TMP_Text deckValidationText;

    private BaseCardData selectedCard;
    private readonly List<BaseCardData> currentDeck = new List<BaseCardData>();

    private int currentPresetIndex = 0;
    private DeckPresetSaveFile saveFile = new DeckPresetSaveFile();

    private const int MaxIdolCount = 1;
    private const int MaxBroadcastCount = 9;
    private const int MaxMainDeckCount = 40;
    private const int MaxSameCardCount = 3;

    private string SaveFilePath
    {
        get
        {
            return Path.Combine(Application.persistentDataPath, "deck_presets.json");
        }
    }

    private void Start()
    {
        if (addCardButton != null)
            addCardButton.onClick.AddListener(AddSelectedCardToDeck);

        if (removeCardButton != null)
            removeCardButton.onClick.AddListener(RemoveSelectedCardFromDeck);

        if (saveDeckButton != null)
            saveDeckButton.onClick.AddListener(SaveCurrentPreset);

        SetupPresetButtons();
        LoadSaveFileFromDisk();

        RefreshDeckDisplay();
    }

    private void SetupPresetButtons()
    {
        if (presetButtons == null || presetButtons.Length == 0)
        {
            Debug.LogWarning("프리셋 버튼이 연결되지 않았습니다.");
            return;
        }

        for (int i = 0; i < presetButtons.Length; i++)
        {
            int presetIndex = i;

            if (presetButtons[i] != null)
            {
                presetButtons[i].onClick.AddListener(() =>
                {
                    SelectPreset(presetIndex);
                });
            }
        }
    }

    public void SetSelectedCard(BaseCardData card)
    {
        selectedCard = card;

        if (selectedCard != null)
            Debug.Log($"선택 카드 저장: {selectedCard.name}");
    }

    public bool IsSelectedCard(BaseCardData card)
    {
        if (selectedCard == null || card == null) return false;
        return selectedCard.id == card.id;
    }

    public void AddSelectedCardToDeck()
    {
        if (selectedCard == null)
        {
            SetValidationText("선택된 카드가 없습니다.");
            return;
        }

        TryAddCard(selectedCard);
    }

    public void RemoveSelectedCardFromDeck()
    {
        if (selectedCard == null)
        {
            SetValidationText("선택된 카드가 없습니다.");
            return;
        }

        RemoveCard(selectedCard);
    }

    public void TryAddCard(BaseCardData card)
    {
        if (card == null) return;

        string validationMessage;

        if (!CanAddCard(card, out validationMessage))
        {
            Debug.LogWarning(validationMessage);
            SetValidationText(validationMessage);
            return;
        }

        currentDeck.Add(card);

        Debug.Log($"덱에 추가: {card.name}");
        RefreshDeckDisplay();
    }

    public void RemoveCard(BaseCardData card)
    {
        if (card == null) return;

        for (int i = currentDeck.Count - 1; i >= 0; i--)
        {
            if (currentDeck[i].id == card.id)
            {
                Debug.Log($"덱에서 제거: {currentDeck[i].name}");
                currentDeck.RemoveAt(i);
                RefreshDeckDisplay();
                return;
            }
        }

        SetValidationText("덱에 해당 카드가 없습니다.");
    }

    private bool CanAddCard(BaseCardData card, out string message)
    {
        int sameCardCount = currentDeck.Count(deckCard => deckCard.id == card.id);

        if (card.kind == "Idol")
        {
            int idolCount = currentDeck.Count(deckCard => deckCard.kind == "Idol");

            if (idolCount >= MaxIdolCount)
            {
                message = "아이돌 카드는 1장만 넣을 수 있습니다.";
                return false;
            }

            message = "";
            return true;
        }

        if (!HasIdolCard())
        {
            message = "먼저 아이돌 카드를 추가해야 합니다.";
            return false;
        }

        if (!IsCharmCompatibleWithIdol(card))
        {
            BaseCardData idolCard = GetCurrentIdolCard();

            message =
                $"아이돌 속성과 맞지 않는 카드입니다.\n" +
                $"아이돌 속성: {GetCharmText(idolCard.charm)} / 카드 속성: {GetCharmText(card.charm)}";

            return false;
        }

        if (sameCardCount >= MaxSameCardCount)
        {
            message = "같은 이름의 카드는 최대 3장까지 넣을 수 있습니다.";
            return false;
        }

        if (card.kind == "Broadcast")
        {
            int broadcastCount = currentDeck.Count(deckCard => deckCard.kind == "Broadcast");

            if (broadcastCount >= MaxBroadcastCount)
            {
                message = "방송 카드는 최대 9장까지 넣을 수 있습니다.";
                return false;
            }

            message = "";
            return true;
        }

        if (card.kind == "Character" || card.kind == "Content")
        {
            int mainDeckCount = currentDeck.Count(deckCard =>
                deckCard.kind == "Character" || deckCard.kind == "Content"
            );

            if (mainDeckCount >= MaxMainDeckCount)
            {
                message = "메인 덱은 최대 40장까지 넣을 수 있습니다.";
                return false;
            }

            message = "";
            return true;
        }

        message = $"알 수 없는 카드 유형입니다: {card.kind}";
        return false;
    }

    private void RefreshDeckDisplay()
    {
        ClearArea(idolArea);
        ClearArea(broadcastArea);
        ClearArea(mainArea);

        List<BaseCardData> idolCards = GetSortedDisplayCards(
            currentDeck.Where(card => card.kind == "Idol").ToList()
        );

        List<BaseCardData> broadcastCards = GetSortedDisplayCards(
            currentDeck.Where(card => card.kind == "Broadcast").ToList()
        );

        List<BaseCardData> mainCards = GetSortedDisplayCards(
            currentDeck.Where(card => card.kind == "Character" || card.kind == "Content").ToList()
        );

        CreateCardItems(idolCards, idolArea, true);
        CreateCardItems(broadcastCards, broadcastArea, true);
        CreateCardItems(mainCards, mainArea, true);

        ValidateCurrentDeck();
    }

    private void CreateCardItems(List<BaseCardData> cards, Transform parent, bool canRemoveByRightClick)
{
    if (parent == null || deckCardItemPrefab == null) return;

    foreach (BaseCardData card in cards)
    {
        GameObject itemObject = Instantiate(deckCardItemPrefab, parent);

        DeckCardItemUI itemUI = itemObject.GetComponent<DeckCardItemUI>();

        if (itemUI == null)
            itemUI = itemObject.GetComponentInChildren<DeckCardItemUI>();

        if (itemUI != null)
        {
            itemUI.SetCard(
                card,
                SelectCardFromDeck,
                canRemoveByRightClick ? RemoveCard : null
            );
        }
        else
        {
            Debug.LogWarning("DeckCardItem_Prefab에 DeckCardItemUI가 없습니다.");
        }
    }
}

    private void ClearArea(Transform area)
    {
        if (area == null) return;

        for (int i = area.childCount - 1; i >= 0; i--)
        {
            Destroy(area.GetChild(i).gameObject);
        }
    }

    private void ValidateCurrentDeck()
    {
        string validationMessage;
        bool isValidForPlay = IsDeckValidForPlay(out validationMessage);

        if (isValidForPlay)
        {
            SetValidationText("덱 구성이 완료되었습니다. 게임 사용 가능 덱입니다.");
        }
        else
        {
            SetValidationText($"미완성 덱: {validationMessage}");
        }
    }

    private bool IsDeckValidForPlay(out string message)
    {
        int idolCount = currentDeck.Count(card => card.kind == "Idol");
        int broadcastCount = currentDeck.Count(card => card.kind == "Broadcast");
        int mainDeckCount = currentDeck.Count(card => card.kind == "Character" || card.kind == "Content");

        if (idolCount != 1)
        {
            message = $"아이돌 카드가 1장 필요합니다. 현재 {idolCount}/1";
            return false;
        }

        if (broadcastCount != MaxBroadcastCount)
        {
            message = $"방송 카드가 9장 필요합니다. 현재 {broadcastCount}/9";
            return false;
        }

        if (mainDeckCount != MaxMainDeckCount)
        {
            message = $"메인 덱이 40장 필요합니다. 현재 {mainDeckCount}/40";
            return false;
        }

        foreach (var group in currentDeck.GroupBy(card => card.id))
        {
            BaseCardData card = group.First();
            int count = group.Count();

            if (card.kind == "Idol" && count > MaxIdolCount)
            {
                message = "아이돌 카드는 1장만 넣을 수 있습니다.";
                return false;
            }

            if (card.kind != "Idol" && count > MaxSameCardCount)
            {
                message = $"{card.name} 카드는 최대 3장까지 넣을 수 있습니다.";
                return false;
            }
        }

        BaseCardData idolCard = GetCurrentIdolCard();

        foreach (BaseCardData card in currentDeck)
        {
            if (card.kind == "Idol")
                continue;

            if (!IsCharmCompatibleWithIdol(card))
            {
                message =
                    $"아이돌 속성과 맞지 않는 카드가 있습니다.\n" +
                    $"아이돌 속성: {GetCharmText(idolCard.charm)} / 카드: {card.name} / 카드 속성: {GetCharmText(card.charm)}";

                return false;
            }
        }

        message = "사용 가능 덱입니다.";
        return true;
    }

    private void SelectPreset(int presetIndex)
    {
        currentPresetIndex = presetIndex;

        LoadPreset(currentPresetIndex);

        Debug.Log($"프리셋 선택: {currentPresetIndex + 1}");
    }

    private void SaveCurrentPreset()
    {
        DeckPresetSaveData preset = GetOrCreatePreset(currentPresetIndex);

        preset.presetIndex = currentPresetIndex;
        preset.deckName = $"Preset {currentPresetIndex + 1}";
        preset.cardIds = currentDeck.Select(card => card.id).ToList();

        string validationMessage;
        bool isValidForPlay = IsDeckValidForPlay(out validationMessage);

        preset.isValidForPlay = isValidForPlay;
        preset.validationMessage = validationMessage;

        SaveFileToDisk();

        if (isValidForPlay)
        {
            SetValidationText($"프리셋 {currentPresetIndex + 1} 저장 완료 / 사용 가능 덱");
            Debug.Log($"프리셋 {currentPresetIndex + 1} 저장 완료 / 사용 가능 덱: {SaveFilePath}");
        }
        else
        {
            SetValidationText($"프리셋 {currentPresetIndex + 1} 저장 완료 / 미완성 덱\n{validationMessage}");
            Debug.Log($"프리셋 {currentPresetIndex + 1} 저장 완료 / 미완성 덱: {validationMessage}");
        }
    }

    private void LoadPreset(int presetIndex)
    {
        if (cardListManager == null)
        {
            SetValidationText("CardListManager가 연결되지 않았습니다.");
            Debug.LogWarning("CardListManager가 연결되지 않았습니다.");
            return;
        }

        DeckPresetSaveData preset = saveFile.presets
            .FirstOrDefault(item => item.presetIndex == presetIndex);

        currentDeck.Clear();

        if (preset == null || preset.cardIds == null || preset.cardIds.Count == 0)
        {
            RefreshDeckDisplay();
            SetValidationText($"프리셋 {presetIndex + 1}은 비어 있습니다.");
            return;
        }

        foreach (string cardId in preset.cardIds)
        {
            BaseCardData card = cardListManager.FindCardById(cardId);

            if (card == null)
            {
                Debug.LogWarning($"저장된 카드 ID를 찾을 수 없습니다: {cardId}");
                continue;
            }

            currentDeck.Add(card);
        }

        RefreshDeckDisplay();

        string validationMessage;
        bool isValidForPlay = IsDeckValidForPlay(out validationMessage);

        if (isValidForPlay)
        {
            SetValidationText($"프리셋 {presetIndex + 1} 불러오기 완료 / 사용 가능 덱");
        }
        else
        {
            SetValidationText($"프리셋 {presetIndex + 1} 불러오기 완료 / 미완성 덱\n{validationMessage}");
        }
    }

    private DeckPresetSaveData GetOrCreatePreset(int presetIndex)
    {
        DeckPresetSaveData preset = saveFile.presets
            .FirstOrDefault(item => item.presetIndex == presetIndex);

        if (preset == null)
        {
            preset = new DeckPresetSaveData
            {
                presetIndex = presetIndex,
                deckName = $"Preset {presetIndex + 1}",
                cardIds = new List<string>(),
                isValidForPlay = false,
                validationMessage = "아직 저장되지 않은 프리셋입니다."
            };

            saveFile.presets.Add(preset);
        }

        return preset;
    }

    private void LoadSaveFileFromDisk()
    {
        if (!File.Exists(SaveFilePath))
        {
            saveFile = new DeckPresetSaveFile();
            Debug.Log("저장된 덱 프리셋 파일이 없습니다. 새로 시작합니다.");
            return;
        }

        string json = File.ReadAllText(SaveFilePath);

        if (string.IsNullOrEmpty(json))
        {
            saveFile = new DeckPresetSaveFile();
            return;
        }

        saveFile = JsonUtility.FromJson<DeckPresetSaveFile>(json);

        if (saveFile == null)
        {
            saveFile = new DeckPresetSaveFile();
        }

        if (saveFile.presets == null)
        {
            saveFile.presets = new List<DeckPresetSaveData>();
        }

        Debug.Log($"덱 프리셋 파일 불러오기 완료: {SaveFilePath}");
    }

    private void SaveFileToDisk()
    {
        string json = JsonUtility.ToJson(saveFile, true);
        File.WriteAllText(SaveFilePath, json);
    }

    private BaseCardData GetCurrentIdolCard()
    {
        return currentDeck.FirstOrDefault(card => card.kind == "Idol");
    }

    private bool HasIdolCard()
    {
        return GetCurrentIdolCard() != null;
    }

    private bool IsCharmCompatibleWithIdol(BaseCardData card)
    {
        BaseCardData idolCard = GetCurrentIdolCard();

        if (idolCard == null)
            return false;

        if (card.charm == null || card.charm.Length == 0)
            return false;

        if (idolCard.charm == null || idolCard.charm.Length == 0)
            return false;

        if (card.charm.Any(charm => charm == "Free"))
            return true;

        return card.charm.Any(cardCharm =>
            idolCard.charm.Any(idolCharm => idolCharm == cardCharm)
        );
    }

    private string GetCharmText(string[] charms)
    {
        if (charms == null || charms.Length == 0)
            return "-";

        return string.Join(", ", charms);
    }

    private List<BaseCardData> GetSortedDisplayCards(List<BaseCardData> cards)
    {
        return cards
            .GroupBy(card => card.id)
            .OrderBy(group => GetKindOrder(group.First().kind))
            .ThenBy(group => group.First().name)
            .SelectMany(group => group)
            .ToList();
    }

    private int GetKindOrder(string kind)
    {
        switch (kind)
        {
            case "Idol":
                return 0;

            case "Broadcast":
                return 1;

            case "Character":
                return 2;

            case "Content":
                return 3;

            default:
                return 99;
        }
    }

    private void SetValidationText(string message)
    {
        if (deckValidationText != null)
            deckValidationText.text = message;
    }

    private void SelectCardFromDeck(BaseCardData card)
{
    if (card == null) return;

    SetSelectedCard(card);

    if (cardDetailPanel != null)
    {
        cardDetailPanel.ShowCard(card);
    }

    SetValidationText($"선택 카드: {card.name}");
}
}

[Serializable]
public class DeckPresetSaveFile
{
    public List<DeckPresetSaveData> presets = new List<DeckPresetSaveData>();
}

[Serializable]
public class DeckPresetSaveData
{
    public int presetIndex;
    public string deckName;
    public List<string> cardIds = new List<string>();

    public bool isValidForPlay;
    public string validationMessage;
}