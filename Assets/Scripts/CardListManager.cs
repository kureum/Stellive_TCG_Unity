using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardListManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform contentParent;      // CardListScrollView > Viewport > Content
    public GameObject cardItemPrefab;    // CardItem_Prefab

    [Header("Detail Panel")]
    public CardDetailPanel cardDetailPanel;

    [Header("Deck Builder")]
    public DeckBuilderManager deckBuilderManager;
    
    [Header("Search")]
    public TMP_InputField searchInput;   // SearchInputField
    public Button searchButton;          // SearchButton

    [Header("Filter Popup")]
    public GameObject filterPopupPanel;  // FilterPopupPanel
    public Button filterOpenButton;      // FilterSearchButton
    public Button filterCloseButton;     // FilterCloseButton
    public Button filterApplyButton;     // FilterApplyButton

    [Header("Charm Filter Buttons")]
    public Button allCharmButton;
    public Button lovelyButton;
    public Button trickyButton;
    public Button pureButton;
    public Button coolButton;
    public Button freeButton;

    [Header("Kind Filter Buttons")]
    public Button allKindButton;
    public Button idolButton;
    public Button broadcastButton;
    public Button characterButton;
    public Button contentButton;

    private List<BaseCardData> allCards = new List<BaseCardData>();

    private string selectedCharm = "All";
    private string selectedKind = "All";

    private Color normalFilterButtonColor = Color.white;
    private Color selectedFilterButtonColor = new Color(0.45f, 0.75f, 1f);

    private void Start()
    {
        LoadCardDatabase();

        SetupSearchEvents();
        SetupFilterPopupEvents();
        SetupFilterButtons();

        UpdateFilterButtonColors();

        RefreshCardList();
    }

    private void LoadCardDatabase()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("cards");

        if (jsonFile == null)
        {
            Debug.LogError("cards.json을 찾을 수 없습니다. Assets/Resources/cards.json 위치를 확인하세요.");
            return;
        }

        CardDatabase database = JsonUtility.FromJson<CardDatabase>(jsonFile.text);

        if (database == null)
        {
            Debug.LogError("cards.json 파싱에 실패했습니다.");
            return;
        }

        allCards.Clear();

        if (database.idols != null)
        {
            allCards.AddRange(database.idols);
        }

        if (database.broadcasts != null)
        {
            allCards.AddRange(database.broadcasts);
        }

        if (database.characters != null)
        {
            allCards.AddRange(database.characters);
        }

        if (database.contents != null)
        {
            allCards.AddRange(database.contents);
        }

        Debug.Log($"카드 데이터 로드 완료: {allCards.Count}장");
    }

    private void SetupSearchEvents()
    {
        if (searchButton != null)
        {
            searchButton.onClick.AddListener(RefreshCardList);
        }
        else
        {
            Debug.LogWarning("SearchButton이 연결되지 않았습니다.");
        }

        if (searchInput != null)
        {
            searchInput.onSubmit.AddListener(_ => RefreshCardList());
        }
        else
        {
            Debug.LogWarning("SearchInput이 연결되지 않았습니다.");
        }
    }

    private void SetupFilterPopupEvents()
    {
        if (filterPopupPanel != null)
        {
            filterPopupPanel.SetActive(false);
        }
        else
        {
            Debug.LogWarning("FilterPopupPanel이 연결되지 않았습니다.");
        }

        if (filterOpenButton != null)
        {
            filterOpenButton.onClick.AddListener(OpenFilterPopup);
        }
        else
        {
            Debug.LogWarning("FilterOpenButton이 연결되지 않았습니다.");
        }

        if (filterCloseButton != null)
        {
            filterCloseButton.onClick.AddListener(CloseFilterPopup);
        }
        else
        {
            Debug.LogWarning("FilterCloseButton이 연결되지 않았습니다.");
        }

        if (filterApplyButton != null)
        {
            filterApplyButton.onClick.AddListener(ApplyFilter);
        }
        else
        {
            Debug.LogWarning("FilterApplyButton이 연결되지 않았습니다.");
        }
    }

    private void SetupFilterButtons()
    {
        if (allCharmButton != null)
            allCharmButton.onClick.AddListener(() => SelectCharm("All"));

        if (lovelyButton != null)
            lovelyButton.onClick.AddListener(() => SelectCharm("Lovely"));

        if (trickyButton != null)
            trickyButton.onClick.AddListener(() => SelectCharm("Tricky"));

        if (pureButton != null)
            pureButton.onClick.AddListener(() => SelectCharm("Pure"));

        if (coolButton != null)
            coolButton.onClick.AddListener(() => SelectCharm("Cool"));

        if (freeButton != null)
            freeButton.onClick.AddListener(() => SelectCharm("Free"));


        if (allKindButton != null)
            allKindButton.onClick.AddListener(() => SelectKind("All"));

        if (idolButton != null)
            idolButton.onClick.AddListener(() => SelectKind("Idol"));

        if (broadcastButton != null)
            broadcastButton.onClick.AddListener(() => SelectKind("Broadcast"));

        if (characterButton != null)
            characterButton.onClick.AddListener(() => SelectKind("Character"));

        if (contentButton != null)
            contentButton.onClick.AddListener(() => SelectKind("Content"));
    }

    private void OpenFilterPopup()
    {
        if (filterPopupPanel != null)
        {
            filterPopupPanel.SetActive(true);
        }
    }

    private void CloseFilterPopup()
    {
        if (filterPopupPanel != null)
        {
            filterPopupPanel.SetActive(false);
        }
    }

    private void SelectCharm(string charm)
    {
        selectedCharm = charm;
        UpdateFilterButtonColors();

        Debug.Log($"선택된 속성: {selectedCharm}");
    }

    private void SelectKind(string kind)
    {
        selectedKind = kind;
        UpdateFilterButtonColors();

        Debug.Log($"선택된 유형: {selectedKind}");
    }

    private void ApplyFilter()
    {
        RefreshCardList();
        CloseFilterPopup();

        Debug.Log($"필터 적용: Charm={selectedCharm}, Kind={selectedKind}");
    }

    private void RefreshCardList()
    {
        if (contentParent == null)
        {
            Debug.LogError("contentParent가 연결되지 않았습니다.");
            return;
        }

        if (cardItemPrefab == null)
        {
            Debug.LogError("cardItemPrefab이 연결되지 않았습니다.");
            return;
        }

        ClearCardList();

        string keyword = "";

        if (searchInput != null)
        {
            keyword = Normalize(searchInput.text);
        }

        List<BaseCardData> filteredCards = allCards
            .Where(card => MatchesSearch(card, keyword))
            .Where(card => MatchesCharm(card, selectedCharm))
            .Where(card => MatchesKind(card, selectedKind))
            .ToList();

        foreach (BaseCardData card in filteredCards)
        {
            GameObject cardObject = Instantiate(cardItemPrefab, contentParent);

            CardItemUI cardItemUI = cardObject.GetComponent<CardItemUI>();

            if (cardItemUI == null)
            {
                Debug.LogError("CardItem_Prefab에 CardItemUI 컴포넌트가 없습니다.");
                continue;
            }

            cardItemUI.SetCard(card, HandleCardClick);
        }

        Debug.Log($"카드 리스트 갱신 완료: {filteredCards.Count}장");
    }

    private bool MatchesSearch(BaseCardData card, string keyword)
    {
        if (string.IsNullOrEmpty(keyword)) return true;
        if (card == null) return false;

        string normalizedKeyword = Normalize(keyword);

        // #으로 시작하면 해시태그만 검색
        if (normalizedKeyword.StartsWith("#"))
        {
            if (card.hashtags == null) return false;

            return card.hashtags.Any(tag =>
                Normalize(tag).Contains(normalizedKeyword)
            );
        }

        // #이 없으면 카드 이름만 검색
        if (string.IsNullOrEmpty(card.name)) return false;

        return Normalize(card.name).Contains(normalizedKeyword);
    }

    private bool MatchesCharm(BaseCardData card, string charm)
    {
        if (charm == "All") return true;
        if (card == null || card.charm == null) return false;

        return card.charm.Any(cardCharm => cardCharm == charm);
    }

    private bool MatchesKind(BaseCardData card, string kind)
    {
        if (kind == "All") return true;
        if (card == null || string.IsNullOrEmpty(card.kind)) return false;

        return card.kind == kind;
    }

    private void ClearCardList()
    {
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Destroy(contentParent.GetChild(i).gameObject);
        }
    }

    private void UpdateFilterButtonColors()
    {
        SetButtonColor(allCharmButton, selectedCharm == "All");
        SetButtonColor(lovelyButton, selectedCharm == "Lovely");
        SetButtonColor(trickyButton, selectedCharm == "Tricky");
        SetButtonColor(pureButton, selectedCharm == "Pure");
        SetButtonColor(coolButton, selectedCharm == "Cool");
        SetButtonColor(freeButton, selectedCharm == "Free");

        SetButtonColor(allKindButton, selectedKind == "All");
        SetButtonColor(idolButton, selectedKind == "Idol");
        SetButtonColor(broadcastButton, selectedKind == "Broadcast");
        SetButtonColor(characterButton, selectedKind == "Character");
        SetButtonColor(contentButton, selectedKind == "Content");
    }

    private void SetButtonColor(Button button, bool isSelected)
    {
        if (button == null) return;

        Image image = button.GetComponent<Image>();

        if (image == null) return;

        image.color = isSelected ? selectedFilterButtonColor : normalFilterButtonColor;
    }

    private string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Trim().ToLower();
    }

    private void ShowCardDetail(BaseCardData card)
    {
        if (cardDetailPanel == null)
        {
            Debug.LogWarning("CardDetailPanel이 연결되지 않았습니다.");
            return;
        }

        cardDetailPanel.ShowCard(card);

        Debug.Log($"선택된 카드: {card.name}");
    }

    private void HandleCardClick(BaseCardData card, UnityEngine.EventSystems.PointerEventData.InputButton button)
{
    if (card == null) return;

    bool isAlreadySelected = false;

    if (deckBuilderManager != null)
    {
        isAlreadySelected = deckBuilderManager.IsSelectedCard(card);
    }

    if (button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
    {
        if (deckBuilderManager != null)
        {
            deckBuilderManager.SetSelectedCard(card);
            ShowCardDetail(card);
            deckBuilderManager.RemoveSelectedCardFromDeck();
        }

        return;
    }

    if (button == UnityEngine.EventSystems.PointerEventData.InputButton.Left)
    {
        if (isAlreadySelected)
        {
            if (deckBuilderManager != null)
            {
                deckBuilderManager.AddSelectedCardToDeck();
            }
        }
        else
        {
            if (deckBuilderManager != null)
            {
                deckBuilderManager.SetSelectedCard(card);
            }

            ShowCardDetail(card);
        }
    }
}

public BaseCardData FindCardById(string cardId)
{
    if (string.IsNullOrEmpty(cardId))
        return null;

    return allCards.FirstOrDefault(card => card.id == cardId);
}

}