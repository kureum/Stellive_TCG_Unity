using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum BattlePhase
{
    None,
    BroadcastSetup,
    MainGame
}

public enum BattlePlayerSide
{
    My,
    Enemy
}

public class BattleManager : MonoBehaviour
{
    [Header("Preset")]
    [Tooltip("내가 사용할 프리셋 번호입니다. Preset 1이면 0입니다.")]
    public int presetIndex = 0;

    [Tooltip("상대가 사용할 프리셋 번호입니다. Preset 3이면 2입니다.")]
    public int enemyPresetIndex = 2;

    [Header("Panels / Zones")]
    public Transform myHandPanel;
    public Transform enemyHandCardArea;

    public Transform myIdolSlot;
    public Transform myDeckSlot;
    public Transform myBroadcastDeckSlot;
    public Transform myRestSlot;

    [Header("Enemy Zones")]
    public Transform enemyIdolSlot;
    public Transform enemyDeckSlot;
    public Transform enemyBroadcastDeckSlot;
    public Transform enemyRestSlot;

    [Header("Field Slots")]
    public Transform[] myFieldSlots;
    public Transform[] enemyFieldSlots;

    [Header("Broadcast Setup UI")]
    public GameObject broadcastSelectPanel;
    public Transform broadcastSelectContent;
    public Button broadcastSelectCancelButton;

    [Tooltip("비워두면 handCardItemPrefab을 사용합니다.")]
    public GameObject broadcastSelectCardItemPrefab;

    [Header("Question Panel")]
    public QuestionPanel questionPanel;

    [Header("Optional Prefab")]
    [Tooltip("없어도 됩니다. 없으면 BattleManager가 임시 텍스트 카드 버튼을 생성합니다.")]
    public GameObject handCardItemPrefab;

    [Header("Texts")]
    public TMP_Text systemMessageText;
    public TMP_Text myStatusText;
    public TMP_Text enemyStatusText;
    public TMP_Text enemyHandCountText;
    public TMP_Text myViewerText;
    public TMP_Text enemyViewerText;

    [Header("System Message Fade")]
    [SerializeField] private CanvasGroup systemMessageCanvasGroup;
    [SerializeField] private float systemMessageVisibleTime = 3f;
    [SerializeField] private float systemMessageFadeTime = 0.5f;

    private Coroutine systemMessageFadeCoroutine;

    [Header("Fonts")]
    [SerializeField] private TMP_FontAsset runtimeLabelFont;

    [Header("Images")]
    [Tooltip("메인 덱/방송 덱/상대 패처럼 뒷면으로 보여줄 때 사용하는 카드 뒷면 이미지입니다.")]
    [SerializeField] private Sprite cardBackSprite;

    [Header("Drag Preview")]
    [Tooltip("드래그 중인 카드 이미지를 띄울 Canvas입니다. 비워두면 자동으로 부모 Canvas를 찾습니다.")]
    public Canvas dragPreviewCanvas;

    [Tooltip("드래그 중 마우스를 따라다니는 카드 이미지 크기입니다.")]
    public Vector2 dragPreviewSize = new Vector2(108f, 154f);

    [Header("Detail Panel")]
    public CardDetailPanel cardDetailPanel;

    [Header("Buttons")]
    public Button turnEndButton;

    [Header("Sub Managers")]
    public MovementManager movementManager;
    public CollaborationManager collaborationManager;

    private readonly List<BaseCardData> allCards = new List<BaseCardData>();
    private readonly List<BattleFieldSlot> myBattleSlots = new List<BattleFieldSlot>();
    private readonly List<BattleFieldSlot> enemyBattleSlots = new List<BattleFieldSlot>();

    private BattlePlayerRuntime myPlayer;
    private BattlePlayerRuntime enemyPlayer;

    private BaseCardData selectedCard;
    private BattleFieldSlot selectedBroadcastTargetSlot;

    private BattleFieldSlot pendingSummonSlot;
    private BaseCardData pendingSummonCard;
    private BattleFieldSlot pendingFlipSlot;
    private BaseCardData pendingFlipCard;
    private BattlePhase currentPhase = BattlePhase.None;
    private BattlePlayerSide firstPlayerSide;
    private BattlePlayerSide currentSetupSide;

    private int myBroadcastPlacedCount = 0;
    private int enemyBroadcastPlacedCount = 0;

    private int myRequiredBroadcastCount = 0;
    private int enemyRequiredBroadcastCount = 0;

    private int turnCount = 1;

    private BattlePlayerSide currentActionSide;
    private int consecutivePassCount = 0;

    // 뒷면 출연은 행동권과 별개의 1턴 1회 제한입니다.
    private bool myHasSummonedFaceDownThisTurn = false;
    private bool enemyHasSummonedFaceDownThisTurn = false;

    private DeckCardItemUI draggingHandCardItem;
    private BaseCardData draggingHandCardData;
    private bool isDraggingHandCard = false;

    private GameObject dragPreviewObject;
    private RectTransform dragPreviewRect;
    private Image dragPreviewImage;

    private string SaveFilePath
    {
        get { return Path.Combine(Application.persistentDataPath, "deck_presets.json"); }
    }

    private void Start()
    {
        if (systemMessageCanvasGroup == null && systemMessageText != null)
            systemMessageCanvasGroup = systemMessageText.GetComponentInParent<CanvasGroup>();

        if (movementManager == null)
            movementManager = GetComponentInChildren<MovementManager>();

        if (movementManager != null)
            movementManager.Init(this);

        if (collaborationManager == null)
            collaborationManager = GetComponentInChildren<CollaborationManager>();

        if (collaborationManager != null)
            collaborationManager.Init(this);

        if (turnEndButton != null)
            turnEndButton.onClick.AddListener(OnClickTurnEndButton);

        if (broadcastSelectCancelButton != null)
            broadcastSelectCancelButton.onClick.AddListener(CancelBroadcastSelection);

        if (questionPanel != null)
            questionPanel.Hide();

        CloseBroadcastSelectPanel();

        StartBattleSetup();
    }

    private void StartBattleSetup()
    {
        SetSystemMessage("배틀 준비를 시작합니다.");

        if (!LoadCardDatabase())
            return;

        DeckPresetSaveData myPreset = LoadPreset(presetIndex);
        if (myPreset == null)
            return;

        DeckPresetSaveData enemyPreset = LoadPreset(enemyPresetIndex);
        if (enemyPreset == null)
            return;

        if (!myPreset.isValidForPlay)
        {
            SetSystemMessage($"내 덱은 아직 미완성 덱입니다.\n{myPreset.validationMessage}");
            Debug.LogWarning($"미완성 덱으로 배틀을 시작할 수 없습니다: {myPreset.validationMessage}");
            return;
        }

        if (!enemyPreset.isValidForPlay)
        {
            SetSystemMessage($"상대 덱은 아직 미완성 덱입니다.\n{enemyPreset.validationMessage}");
            Debug.LogWarning($"상대 미완성 덱으로 배틀을 시작할 수 없습니다: {enemyPreset.validationMessage}");
            return;
        }

        myPlayer = CreatePlayerRuntime("나", myPreset);
        enemyPlayer = CreatePlayerRuntime("상대", enemyPreset);

        if (myPlayer == null || enemyPlayer == null)
            return;

        ResolveFieldSlots();
        InitializeFieldSlots();

        Shuffle(myPlayer.mainDeck);
        Shuffle(enemyPlayer.mainDeck);

        DrawCards(myPlayer, 5);
        DrawCards(enemyPlayer, 5);

        RefreshAllUI();

        StartBroadcastSetupPhase(myPreset.deckName, enemyPreset.deckName);
    }

    private bool LoadCardDatabase()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("cards");

        if (jsonFile == null)
        {
            SetSystemMessage("cards.json을 찾을 수 없습니다. Assets/Resources/cards.json 위치를 확인하세요.");
            Debug.LogError("cards.json을 찾을 수 없습니다. Assets/Resources/cards.json 위치를 확인하세요.");
            return false;
        }

        CardDatabase database = JsonUtility.FromJson<CardDatabase>(jsonFile.text);

        if (database == null)
        {
            SetSystemMessage("cards.json 파싱에 실패했습니다.");
            Debug.LogError("cards.json 파싱에 실패했습니다.");
            return false;
        }

        allCards.Clear();

        if (database.idols != null)
            allCards.AddRange(database.idols);

        if (database.broadcasts != null)
            allCards.AddRange(database.broadcasts);

        if (database.characters != null)
            allCards.AddRange(database.characters);

        if (database.contents != null)
            allCards.AddRange(database.contents);

        Debug.Log($"BattleManager 카드 데이터 로드 완료: {allCards.Count}장");
        return true;
    }

    private DeckPresetSaveData LoadPreset(int index)
    {
        if (!File.Exists(SaveFilePath))
        {
            SetSystemMessage($"덱 프리셋 파일이 없습니다.\n먼저 DeckBuilderScene에서 덱을 저장하세요.\n{SaveFilePath}");
            Debug.LogWarning($"덱 프리셋 파일 없음: {SaveFilePath}");
            return null;
        }

        string json = File.ReadAllText(SaveFilePath);

        if (string.IsNullOrEmpty(json))
        {
            SetSystemMessage("덱 프리셋 파일이 비어 있습니다.");
            return null;
        }

        DeckPresetSaveFile saveFile = JsonUtility.FromJson<DeckPresetSaveFile>(json);

        if (saveFile == null || saveFile.presets == null)
        {
            SetSystemMessage("덱 프리셋 파일을 읽을 수 없습니다.");
            return null;
        }

        DeckPresetSaveData preset = saveFile.presets.FirstOrDefault(item => item.presetIndex == index);

        if (preset == null)
        {
            SetSystemMessage($"프리셋 {index + 1}을 찾을 수 없습니다.");
            return null;
        }

        return preset;
    }

    private BattlePlayerRuntime CreatePlayerRuntime(string playerName, DeckPresetSaveData preset)
    {
        BattlePlayerRuntime player = new BattlePlayerRuntime();
        player.playerName = playerName;
        player.viewers = 0;

        foreach (string cardId in preset.cardIds)
        {
            BaseCardData card = FindCardById(cardId);

            if (card == null)
            {
                Debug.LogWarning($"프리셋에 저장된 카드 ID를 찾을 수 없습니다: {cardId}");
                continue;
            }

            switch (card.kind)
            {
                case "Idol":
                    player.idolCard = card;
                    break;

                case "Broadcast":
                    player.broadcastDeck.Add(card);
                    break;

                case "Character":
                case "Content":
                    player.mainDeck.Add(card);
                    break;

                default:
                    Debug.LogWarning($"알 수 없는 카드 유형입니다: {card.kind} / {card.name}");
                    break;
            }
        }

        if (player.idolCard == null)
        {
            SetSystemMessage($"{playerName}의 아이돌 카드가 없어 배틀을 시작할 수 없습니다.");
            return null;
        }

        return player;
    }

    private BaseCardData FindCardById(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
            return null;

        return allCards.FirstOrDefault(card => card.id == cardId);
    }

    private void ResolveFieldSlots()
    {
        myBattleSlots.Clear();
        enemyBattleSlots.Clear();

        if (myFieldSlots != null)
        {
            foreach (Transform slotTransform in myFieldSlots)
            {
                if (slotTransform == null)
                    continue;

                BattleFieldSlot slot = slotTransform.GetComponent<BattleFieldSlot>();

                if (slot == null)
                {
                    Debug.LogWarning($"{slotTransform.name}에 BattleFieldSlot이 없습니다.");
                    continue;
                }

                if (!myBattleSlots.Contains(slot))
                    myBattleSlots.Add(slot);
            }
        }

        if (enemyFieldSlots != null)
        {
            foreach (Transform slotTransform in enemyFieldSlots)
            {
                if (slotTransform == null)
                    continue;

                BattleFieldSlot slot = slotTransform.GetComponent<BattleFieldSlot>();

                if (slot == null)
                {
                    Debug.LogWarning($"{slotTransform.name}에 BattleFieldSlot이 없습니다.");
                    continue;
                }

                if (!enemyBattleSlots.Contains(slot))
                    enemyBattleSlots.Add(slot);
            }
        }

        Debug.Log($"필드 슬롯 연결 완료: 내 슬롯 {myBattleSlots.Count}개 / 상대 슬롯 {enemyBattleSlots.Count}개");
    }

    private void InitializeFieldSlots()
    {
        foreach (BattleFieldSlot slot in myBattleSlots)
        {
            if (slot == null)
                continue;

            slot.Init(
                OnClickFieldSlot,
                OnClickBroadcastCardOnField,
                OnClickCharacterCardOnField,
                OnClickContentCardOnField,
                OnDropHandCardOnFieldSlot,
                OnBeginDragFieldCharacter,
                OnDragFieldCharacter,
                OnEndDragFieldCharacter
            );

            slot.ClearAllCards();
            slot.SetSetupButtonVisible(false);
        }

        foreach (BattleFieldSlot slot in enemyBattleSlots)
        {
            if (slot == null)
                continue;

            slot.Init(
                OnClickFieldSlot,
                OnClickBroadcastCardOnField,
                OnClickCharacterCardOnField,
                OnClickContentCardOnField,
                OnDropHandCardOnFieldSlot,
                OnBeginDragFieldCharacter,
                OnDragFieldCharacter,
                OnEndDragFieldCharacter
            );

            slot.ClearAllCards();
            slot.SetSetupButtonVisible(false);
        }
    }

    private void StartBroadcastSetupPhase(string myDeckName, string enemyDeckName)
    {
        currentPhase = BattlePhase.BroadcastSetup;

        myBroadcastPlacedCount = 0;
        enemyBroadcastPlacedCount = 0;

        myRequiredBroadcastCount = GetRequiredBroadcastCount(myPlayer);
        enemyRequiredBroadcastCount = GetRequiredBroadcastCount(enemyPlayer);

        firstPlayerSide = UnityEngine.Random.Range(0, 2) == 0
            ? BattlePlayerSide.My
            : BattlePlayerSide.Enemy;

        currentSetupSide = firstPlayerSide;

        if (turnEndButton != null)
            turnEndButton.interactable = false;

        RefreshAllUI();
        RefreshBroadcastSetupButtons();

        SetSystemMessage(
            $"내 덱 불러오기 완료: {myDeckName}\n" +
            $"상대 덱 불러오기 완료: {enemyDeckName}\n" +
            $"서로 시작 패 5장을 드로우했습니다.\n\n" +
            $"방송 카드 설치 단계입니다.\n" +
            $"선공권: {GetSideName(firstPlayerSide)}\n" +
            $"현재 설치권: {GetSideName(currentSetupSide)}"
        );
    }

    private int GetRequiredBroadcastCount(BattlePlayerRuntime player)
    {
        if (player == null || player.idolCard == null)
            return 0;

        IdolCardData idol = player.idolCard as IdolCardData;

        if (idol == null)
        {
            Debug.LogWarning($"{player.playerName}의 아이돌 카드 데이터를 IdolCardData로 읽지 못했습니다. 방송 슬롯 수를 1로 처리합니다.");
            return 1;
        }

        return Mathf.Clamp(idol.maxBroadcastSlots, 0, 6);
    }

    private void RefreshBroadcastSetupButtons()
    {
        foreach (BattleFieldSlot slot in myBattleSlots)
        {
            if (slot == null)
                continue;

            bool canPlace =
                currentPhase == BattlePhase.BroadcastSetup &&
                currentSetupSide == BattlePlayerSide.My &&
                CanPlaceBroadcast(BattlePlayerSide.My, slot);

            slot.SetSetupButtonVisible(canPlace);
        }

        foreach (BattleFieldSlot slot in enemyBattleSlots)
        {
            if (slot == null)
                continue;

            bool canPlace =
                currentPhase == BattlePhase.BroadcastSetup &&
                currentSetupSide == BattlePlayerSide.Enemy &&
                CanPlaceBroadcast(BattlePlayerSide.Enemy, slot);

            slot.SetSetupButtonVisible(canPlace);
        }
    }

    public void Debug_AutoPlaceAllBroadcastsRandomly()
    {
        if (currentPhase != BattlePhase.BroadcastSetup)
        {
            SetSystemMessage("디버그 치트: 현재는 방송 카드 설치 단계가 아닙니다.");
            return;
        }

        selectedBroadcastTargetSlot = null;
        CloseBroadcastSelectPanel();

        bool myResult = Debug_AutoPlaceBroadcastsForSide(BattlePlayerSide.My);
        bool enemyResult = Debug_AutoPlaceBroadcastsForSide(BattlePlayerSide.Enemy);

        RefreshAllUI();

        if (IsBroadcastSetupComplete())
        {
            StartMainGame(
                "디버그 치트 발동: 양쪽 방송 카드가 랜덤으로 자동 배치되었습니다."
            );
            return;
        }

        RefreshBroadcastSetupButtons();

        string message =
            "디버그 치트 발동: 방송 카드 자동 배치를 시도했습니다.\n" +
            $"내 배치 결과: {(myResult ? "성공" : "일부 실패")}\n" +
            $"상대 배치 결과: {(enemyResult ? "성공" : "일부 실패")}\n" +
            $"내 방송 배치: {myBroadcastPlacedCount}/{myRequiredBroadcastCount}\n" +
            $"상대 방송 배치: {enemyBroadcastPlacedCount}/{enemyRequiredBroadcastCount}";

        SetSystemMessage(message);
    }

    private bool Debug_AutoPlaceBroadcastsForSide(BattlePlayerSide side)
    {
        BattlePlayerRuntime player = GetPlayer(side);

        if (player == null)
            return false;

        if (player.broadcastDeck == null)
            return false;

        bool allPlaced = true;

        while (GetBroadcastPlacedCount(side) < GetRequiredBroadcastCount(side))
        {
            if (player.broadcastDeck.Count <= 0)
            {
                allPlaced = false;
                break;
            }

            List<BattleFieldSlot> placeableSlots = GetSlots(side)
                .Where(slot => CanPlaceBroadcast(side, slot))
                .ToList();

            if (placeableSlots.Count <= 0)
            {
                allPlaced = false;
                break;
            }

            int slotIndex = UnityEngine.Random.Range(0, placeableSlots.Count);
            int cardIndex = UnityEngine.Random.Range(0, player.broadcastDeck.Count);

            BattleFieldSlot targetSlot = placeableSlots[slotIndex];
            BaseCardData card = player.broadcastDeck[cardIndex];

            Sprite sprite = LoadCardSprite(card);

            targetSlot.SetBroadcastCard(card, sprite);
            player.broadcastDeck.RemoveAt(cardIndex);

            AddBroadcastPlacedCount(side, 1);
        }

        return allPlaced &&
            GetBroadcastPlacedCount(side) >= GetRequiredBroadcastCount(side);
    }

    private bool CanPlaceBroadcast(BattlePlayerSide side, BattleFieldSlot slot)
    {
        if (slot == null)
            return false;

        if (slot.HasBroadcast)
            return false;

        if (!IsSlotOwnedBySide(side, slot))
            return false;

        int placedCount = GetBroadcastPlacedCount(side);

        if (placedCount >= GetRequiredBroadcastCount(side))
            return false;

        if (placedCount == 0)
            return slot.x == 2 && slot.y == 2;

        List<BattleFieldSlot> slots = GetSlots(side);

        foreach (BattleFieldSlot otherSlot in slots)
        {
            if (otherSlot == null || !otherSlot.HasBroadcast)
                continue;

            int distance = Mathf.Abs(otherSlot.x - slot.x) + Mathf.Abs(otherSlot.y - slot.y);

            if (distance == 1)
                return true;
        }

        return false;
    }

    private bool IsSlotOwnedBySide(BattlePlayerSide side, BattleFieldSlot slot)
    {
        if (slot == null)
            return false;

        if (side == BattlePlayerSide.My)
            return slot.owner == BattleSlotOwner.My;

        return slot.owner == BattleSlotOwner.Enemy;
    }

    private void OnClickFieldSlot(BattleFieldSlot slot)
    {
        if (slot == null)
            return;

        if (currentPhase != BattlePhase.BroadcastSetup)
        {
            SetSystemMessage("현재는 방송 카드 설치 단계가 아닙니다.");
            return;
        }

        if (!CanPlaceBroadcast(currentSetupSide, slot))
        {
            SetSystemMessage("이 슬롯에는 방송 카드를 설치할 수 없습니다.");
            return;
        }

        selectedBroadcastTargetSlot = slot;

        OpenBroadcastSelectPanel(currentSetupSide, slot);
    }

    private void OpenBroadcastSelectPanel(BattlePlayerSide side, BattleFieldSlot targetSlot)
    {
        BattlePlayerRuntime player = GetPlayer(side);

        if (player == null)
            return;

        if (broadcastSelectPanel == null)
        {
            SetSystemMessage("BroadcastSelectPanel이 연결되어 있지 않습니다.");
            Debug.LogWarning("BroadcastSelectPanel이 연결되어 있지 않습니다.");
            return;
        }

        if (broadcastSelectContent == null)
        {
            SetSystemMessage("BroadcastSelectContent가 연결되어 있지 않습니다.");
            Debug.LogWarning("BroadcastSelectContent가 연결되어 있지 않습니다.");
            return;
        }

        ClearChildren(broadcastSelectContent);

        foreach (BaseCardData card in player.broadcastDeck)
        {
            CreateBroadcastSelectCardItem(card, broadcastSelectContent);
        }

        broadcastSelectPanel.SetActive(true);

        SetSystemMessage(
            $"{GetSideName(side)}의 방송 카드 설치 위치 선택됨: ({targetSlot.x}, {targetSlot.y})\n" +
            "설치할 방송 카드를 선택하세요.\n" +
            "클릭: 상세 보기 / 더블클릭: 설치 / 취소: 위치 선택 취소"
        );
    }

    private void CreateBroadcastSelectCardItem(BaseCardData card, Transform parent)
    {
        GameObject itemObject;

        GameObject prefab = broadcastSelectCardItemPrefab != null
            ? broadcastSelectCardItemPrefab
            : handCardItemPrefab;

        if (prefab != null)
        {
            itemObject = Instantiate(prefab, parent);
        }
        else
        {
            itemObject = CreateFallbackHandCardItem(parent);
        }

        DeckCardItemUI cardItemUI = itemObject.GetComponent<DeckCardItemUI>();

        if (cardItemUI != null)
        {
            cardItemUI.SetCard(
                card,
                leftClickAction: SelectCard,
                rightClickAction: null,
                doubleClickAction: ConfirmPlaceBroadcastCard
            );

            cardItemUI.SetDragActions(false);
        }
        else
        {
            TMP_Text text = itemObject.GetComponentInChildren<TMP_Text>();

            if (text != null)
                text.text = $"{GetKoreanKind(card.kind)}\n{card.name}";

            Image image = null;

            Transform cardImageTransform = itemObject.transform.Find("CardImage");
            if (cardImageTransform != null)
                image = cardImageTransform.GetComponent<Image>();

            if (image == null)
                image = itemObject.GetComponent<Image>();

            if (image != null)
            {
                Sprite cardSprite = LoadCardSprite(card);

                if (cardSprite != null)
                {
                    image.sprite = cardSprite;
                    image.color = Color.white;
                }

                image.preserveAspect = true;
            }

            Button button = itemObject.GetComponent<Button>();
            if (button == null)
                button = itemObject.AddComponent<Button>();

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectCard(card));
        }
    }

    private void ConfirmPlaceBroadcastCard(BaseCardData card)
    {
        if (currentPhase != BattlePhase.BroadcastSetup)
        {
            SetSystemMessage("현재는 방송 카드 설치 단계가 아닙니다.");
            return;
        }

        if (selectedBroadcastTargetSlot == null)
        {
            SetSystemMessage("방송 카드를 설치할 슬롯이 선택되어 있지 않습니다.");
            return;
        }

        if (card == null)
        {
            SetSystemMessage("설치할 방송 카드가 없습니다.");
            return;
        }

        if (card.kind != "Broadcast")
        {
            SetSystemMessage("방송 카드만 설치할 수 있습니다.");
            return;
        }

        if (!CanPlaceBroadcast(currentSetupSide, selectedBroadcastTargetSlot))
        {
            SetSystemMessage("선택한 슬롯에는 더 이상 방송 카드를 설치할 수 없습니다.");
            return;
        }

        BattlePlayerRuntime player = GetPlayer(currentSetupSide);

        if (player == null)
            return;

        if (!player.broadcastDeck.Contains(card))
        {
            SetSystemMessage("현재 플레이어의 방송 덱에 없는 카드입니다.");
            return;
        }

        Sprite sprite = LoadCardSprite(card);

        selectedBroadcastTargetSlot.SetBroadcastCard(card, sprite);
        player.broadcastDeck.Remove(card);

        AddBroadcastPlacedCount(currentSetupSide, 1);

        string placedMessage =
            $"{GetSideName(currentSetupSide)}가 ({selectedBroadcastTargetSlot.x}, {selectedBroadcastTargetSlot.y}) 슬롯에\n" +
            $"{card.name} 방송 카드를 설치했습니다.";

        selectedBroadcastTargetSlot = null;

        CloseBroadcastSelectPanel();

        RefreshAllUI();

        AdvanceBroadcastSetupTurn(placedMessage);
    }

    private void AdvanceBroadcastSetupTurn(string previousActionMessage)
    {
        if (IsBroadcastSetupComplete())
        {
            StartMainGame(previousActionMessage);
            return;
        }

        BattlePlayerSide previousSide = currentSetupSide;
        BattlePlayerSide nextSide = GetOppositeSide(previousSide);

        if (!IsBroadcastSetupPlayerComplete(nextSide))
        {
            currentSetupSide = nextSide;
        }
        else if (!IsBroadcastSetupPlayerComplete(previousSide))
        {
            currentSetupSide = previousSide;
        }

        RefreshBroadcastSetupButtons();

        SetSystemMessage(
            $"{previousActionMessage}\n\n" +
            $"현재 설치권: {GetSideName(currentSetupSide)}\n" +
            $"설치 가능 슬롯을 선택하세요."
        );
    }

    private void StartMainGame(string previousActionMessage)
    {
        currentPhase = BattlePhase.MainGame;
        selectedBroadcastTargetSlot = null;
        ClearDraggingHandCard();
        ClearPendingSummonChoice();
        ClearPendingFlipChoice();

        currentActionSide = firstPlayerSide;
        consecutivePassCount = 0;
        myHasSummonedFaceDownThisTurn = false;
        enemyHasSummonedFaceDownThisTurn = false;

        CloseBroadcastSelectPanel();

        foreach (BattleFieldSlot slot in myBattleSlots)
        {
            if (slot != null)
                slot.SetSetupButtonVisible(false);
        }

        foreach (BattleFieldSlot slot in enemyBattleSlots)
        {
            if (slot != null)
                slot.SetSetupButtonVisible(false);
        }

        if (turnEndButton != null)
            turnEndButton.interactable = true;

        RefreshAllUI();

        SetSystemMessage(
            $"{previousActionMessage}\n\n" +
            "양쪽 플레이어의 방송 카드 설치가 완료되었습니다.\n" +
            "본게임을 시작합니다.\n" +
            $"현재 행동권: {GetSideName(currentActionSide)}\n" +
            "손패의 캐릭터 카드를 드래그하거나, 행동 종료 버튼으로 패스할 수 있습니다."
        );
    }

    private void CancelBroadcastSelection()
    {
        if (currentPhase != BattlePhase.BroadcastSetup)
        {
            CloseBroadcastSelectPanel();
            return;
        }

        selectedBroadcastTargetSlot = null;
        CloseBroadcastSelectPanel();

        RefreshBroadcastSetupButtons();

        SetSystemMessage(
            "방송 카드 설치 위치 선택을 취소했습니다.\n" +
            $"{GetSideName(currentSetupSide)}의 설치 가능 슬롯을 다시 선택하세요."
        );
    }

    private void CloseBroadcastSelectPanel()
    {
        if (broadcastSelectPanel != null)
            broadcastSelectPanel.SetActive(false);

        if (broadcastSelectContent != null)
            ClearChildren(broadcastSelectContent);
    }

    private bool IsBroadcastSetupComplete()
    {
        return IsBroadcastSetupPlayerComplete(BattlePlayerSide.My) &&
               IsBroadcastSetupPlayerComplete(BattlePlayerSide.Enemy);
    }

    private bool IsBroadcastSetupPlayerComplete(BattlePlayerSide side)
    {
        return GetBroadcastPlacedCount(side) >= GetRequiredBroadcastCount(side);
    }

    private int GetBroadcastPlacedCount(BattlePlayerSide side)
    {
        return side == BattlePlayerSide.My
            ? myBroadcastPlacedCount
            : enemyBroadcastPlacedCount;
    }

    private void AddBroadcastPlacedCount(BattlePlayerSide side, int amount)
    {
        if (side == BattlePlayerSide.My)
            myBroadcastPlacedCount += amount;
        else
            enemyBroadcastPlacedCount += amount;
    }

    private int GetRequiredBroadcastCount(BattlePlayerSide side)
    {
        return side == BattlePlayerSide.My
            ? myRequiredBroadcastCount
            : enemyRequiredBroadcastCount;
    }

    private BattlePlayerRuntime GetPlayer(BattlePlayerSide side)
    {
        return side == BattlePlayerSide.My
            ? myPlayer
            : enemyPlayer;
    }

    private List<BattleFieldSlot> GetSlots(BattlePlayerSide side)
    {
        return side == BattlePlayerSide.My
            ? myBattleSlots
            : enemyBattleSlots;
    }

    private BattlePlayerSide GetOppositeSide(BattlePlayerSide side)
    {
        return side == BattlePlayerSide.My
            ? BattlePlayerSide.Enemy
            : BattlePlayerSide.My;
    }

    private string GetSideName(BattlePlayerSide side)
    {
        return side == BattlePlayerSide.My
            ? "나"
            : "상대";
    }

    public QuestionPanel BattleQuestionPanel
    {
        get { return questionPanel; }
    }

    public BattlePhase CurrentPhaseFromExternal
    {
        get { return currentPhase; }
    }

    public BattlePlayerSide CurrentActionSideFromExternal
    {
        get { return currentActionSide; }
    }

    public IReadOnlyList<BattleFieldSlot> GetSlotsForMovement(BattlePlayerSide side)
    {
        return GetSlots(side);
    }

    public bool CanUseMyActionFromExternal(out string failReason)
    {
        return CanUseMyAction(out failReason);
    }

    public Sprite LoadCardSpriteFromExternal(BaseCardData card)
    {
        return LoadCardSprite(card);
    }

    public void SelectCardFromExternal(BaseCardData card)
    {
        SelectCard(card);
    }

    public void SetSystemMessageFromExternal(string message)
    {
        SetSystemMessage(message);
    }

    public void RefreshAllUIFromExternal()
    {
        RefreshAllUI();
    }

    public void ResolveMyActionUsedFromExternal(string actionMessage)
    {
        ResolveMyActionUsed(actionMessage);
    }

    private bool CanUseMyAction(out string failReason)
    {
        failReason = "";

        if (currentPhase != BattlePhase.MainGame)
        {
            failReason = "아직 본게임 단계가 아닙니다.";
            return false;
        }

        if (currentActionSide != BattlePlayerSide.My)
        {
            failReason = "현재는 내 행동권이 아닙니다.";
            return false;
        }

        return true;
    }

    private void ClearAllPendingBattleInteractions()
    {
        ClearDraggingHandCard();
        ClearPendingSummonChoice();
        ClearPendingFlipChoice();

        if (movementManager != null)
            movementManager.CancelMoveStateFromExternal();

        if (questionPanel != null && questionPanel.IsOpen())
            questionPanel.Hide();
    }

    private void ResetTurnLimitedFlags()
    {
        myHasSummonedFaceDownThisTurn = false;
        enemyHasSummonedFaceDownThisTurn = false;

        if (movementManager != null)
            movementManager.ResetAllCharacterMoveFlagsForNewTurn();
    }

    private void ResolveMyActionUsed(string actionMessage)
    {
        consecutivePassCount = 0;
        currentActionSide = BattlePlayerSide.Enemy;

        RefreshAllUI();

        SetSystemMessage(
            $"{actionMessage}\n" +
            "상대 행동권입니다."
        );
    }

    private void ResolveMyActionPass()
    {
        ClearAllPendingBattleInteractions();

        consecutivePassCount++;

        if (consecutivePassCount >= 2)
        {
            EndCurrentTurnAndStartNextTurn("양쪽 플레이어가 연속으로 행동하지 않았습니다.");
            return;
        }

        currentActionSide = BattlePlayerSide.Enemy;

        RefreshAllUI();

        SetSystemMessage(
            "나는 행동하지 않았습니다.\n" +
            "상대 행동권입니다."
        );
    }

    private void ResolveEnemyActionUsed(string actionMessage)
    {
        consecutivePassCount = 0;
        currentActionSide = BattlePlayerSide.My;

        RefreshAllUI();

        SetSystemMessage(
            $"{actionMessage}\n" +
            "내 행동권입니다."
        );
    }

    private void ResolveEnemyActionPass(string actionMessage)
    {
        consecutivePassCount++;

        if (consecutivePassCount >= 2)
        {
            EndCurrentTurnAndStartNextTurn(
                $"{actionMessage}\n" +
                "양쪽 플레이어가 연속으로 행동하지 않았습니다."
            );
            return;
        }

        currentActionSide = BattlePlayerSide.My;

        RefreshAllUI();

        SetSystemMessage(
            $"{actionMessage}\n" +
            "내 행동권입니다."
        );
    }

    private void EndCurrentTurnAndStartNextTurn(string reasonMessage)
    {
        ClearAllPendingBattleInteractions();

        turnCount++;

        consecutivePassCount = 0;
        currentActionSide = firstPlayerSide;

        ResetTurnLimitedFlags();

        DrawCards(myPlayer, 1);
        DrawCards(enemyPlayer, 1);

        int myGainedViewers = GainPrepViewers(BattleSlotOwner.My);
        int enemyGainedViewers = GainPrepViewers(BattleSlotOwner.Enemy);

        RefreshAllUI();

        SetSystemMessage(
            $"{reasonMessage}\n\n" +
            $"{turnCount}턴 시작.\n" +
            $"현재 행동권: {GetSideName(currentActionSide)}\n" +
            "서로 카드 1장을 드로우했습니다.\n" +
            $"내 시청자 +{myGainedViewers}\n" +
            $"상대 시청자 +{enemyGainedViewers}"
        );
    }

    public bool IsBroadcastSetupPhase()
    {
        return currentPhase == BattlePhase.BroadcastSetup;
    }

    public bool IsEnemySetupTurn()
    {
        return currentPhase == BattlePhase.BroadcastSetup &&
            currentSetupSide == BattlePlayerSide.Enemy;
    }

    public bool IsEnemyActionTurn()
    {
        return currentPhase == BattlePhase.MainGame &&
            currentActionSide == BattlePlayerSide.Enemy;
    }

    public void TestEnemyPassAction()
    {
        if (currentPhase != BattlePhase.MainGame)
            return;

        if (currentActionSide != BattlePlayerSide.Enemy)
            return;

        ResolveEnemyActionPass("상대는 행동하지 않았습니다.");
    }

    public void TestEnemyUseAction(string actionMessage)
    {
        if (currentPhase != BattlePhase.MainGame)
            return;

        if (currentActionSide != BattlePlayerSide.Enemy)
            return;

        ResolveEnemyActionUsed(actionMessage);
    }

    public bool TestEnemyTrySummonBacksideCharacter()
    {
        if (currentPhase != BattlePhase.MainGame)
            return false;

        if (currentActionSide != BattlePlayerSide.Enemy)
            return false;

        if (enemyPlayer == null || enemyPlayer.hand == null)
            return false;

        if (enemyHasSummonedFaceDownThisTurn)
            return false;

        BaseCardData characterCard = enemyPlayer.hand.FirstOrDefault(card =>
            card != null &&
            card.kind == "Character"
        );

        if (characterCard == null)
            return false;

        BattleFieldSlot targetSlot = enemyBattleSlots.FirstOrDefault(slot =>
            slot != null &&
            slot.owner == BattleSlotOwner.Enemy &&
            slot.HasBroadcast &&
            !slot.HasCharacter
        );

        if (targetSlot == null)
            return false;

        targetSlot.SetCharacterCard(characterCard, cardBackSprite, true, BattleSlotOwner.Enemy);
        targetSlot.faceDownSummonedTurn = turnCount;
        enemyPlayer.hand.Remove(characterCard);

        enemyHasSummonedFaceDownThisTurn = true;

        RefreshAllUI();

        ResolveEnemyActionUsed(
            $"상대가 ({targetSlot.x}, {targetSlot.y}) 슬롯에\n" +
            $"{characterCard.name} 카드를 뒷면으로 출연시켰습니다."
        );

        return true;
    }

    public void TestEnemyPlaceBroadcastCard()
    {
        if (currentPhase != BattlePhase.BroadcastSetup)
            return;

        if (currentSetupSide != BattlePlayerSide.Enemy)
            return;

        if (enemyPlayer == null)
            return;

        if (enemyPlayer.broadcastDeck == null || enemyPlayer.broadcastDeck.Count == 0)
        {
            SetSystemMessage("상대 방송 덱에 설치할 방송 카드가 없습니다.");
            return;
        }

        BattleFieldSlot targetSlot = FindFirstPlaceableBroadcastSlot(BattlePlayerSide.Enemy);

        if (targetSlot == null)
        {
            SetSystemMessage("상대가 설치 가능한 방송 슬롯을 찾지 못했습니다.");
            return;
        }

        BaseCardData card = enemyPlayer.broadcastDeck[0];

        if (card == null)
        {
            SetSystemMessage("상대가 설치할 방송 카드 데이터가 없습니다.");
            return;
        }

        Sprite sprite = LoadCardSprite(card);

        targetSlot.SetBroadcastCard(card, sprite);
        enemyPlayer.broadcastDeck.Remove(card);

        AddBroadcastPlacedCount(BattlePlayerSide.Enemy, 1);

        string placedMessage =
            $"상대가 ({targetSlot.x}, {targetSlot.y}) 슬롯에\n" +
            $"{card.name} 방송 카드를 설치했습니다.";

        selectedBroadcastTargetSlot = null;

        CloseBroadcastSelectPanel();

        RefreshAllUI();

        AdvanceBroadcastSetupTurn(placedMessage);
    }

    private BattleFieldSlot FindFirstPlaceableBroadcastSlot(BattlePlayerSide side)
    {
        List<BattleFieldSlot> slots = GetSlots(side);

        foreach (BattleFieldSlot slot in slots)
        {
            if (CanPlaceBroadcast(side, slot))
                return slot;
        }

        return null;
    }

    private void OnClickBroadcastCardOnField(BattleFieldSlot slot, BaseCardData card)
    {
        SelectCard(card);
        SetSystemMessage($"방송 카드 확인: {card.name}");
    }

    private void OnClickCharacterCardOnField(BattleFieldSlot slot, BaseCardData card) 
    {
        if (slot == null || card == null)
            return;

        if (slot.characterOwner == BattleSlotOwner.My)
        {
            SelectCard(card);

            if (slot.isCharacterFaceDown)
            {
                OpenFlipSummonQuestion(slot, card);
                return;
            }

            SetSystemMessage($"캐릭터 카드 확인: {card.name}");
            return;
        }

        if (slot.characterOwner == BattleSlotOwner.Enemy)
        {
            if (slot.isCharacterFaceDown)
            {
                SetSystemMessage("상대의 뒷면 캐릭터입니다.");
                return;
            }

            SelectCard(card);
            SetSystemMessage($"상대 캐릭터 카드 확인: {card.name}");
        }
    }

    private void OpenFlipSummonQuestion(BattleFieldSlot slot, BaseCardData card)
    {
        if (slot == null || card == null)
            return;

        if (!slot.isCharacterFaceDown)
            return;

        if (slot.characterOwner != BattleSlotOwner.My)
        {
            SetSystemMessage("내 캐릭터만 뒤집기 출연할 수 있습니다.");
            return;
        }

        string turnFailReason;
        if (!CanFlipSummonByTurn(slot, out turnFailReason))
        {
            SetSystemMessage(turnFailReason);
            return;
        }

        string failReason;
        if (!CanUseMyAction(out failReason))
        {
            SetSystemMessage(failReason);
            return;
        }

        if (questionPanel != null && questionPanel.IsOpen())
        {
            SetSystemMessage("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        ClearPendingSummonChoice();
        ClearDraggingHandCard();

        int cost = GetCharacterAppearCost(card);

        if (!CanPayViewerCost(myPlayer, cost))
        {
            SetSystemMessage("시청자가 부족하여 플립 출연할 수 없습니다.");
            return;
        }

        pendingFlipSlot = slot;
        pendingFlipCard = card;

        if (questionPanel == null)
        {
            SetSystemMessage("QuestionPanel이 BattleManager에 연결되어 있지 않습니다.");
            return;
        }

        questionPanel.ShowYesNoQuestion(
            "플립 출연을 하시겠습니까?",
            OnConfirmFlipSummon,
            CancelFlipSummonChoice,
            CancelFlipSummonChoice
        );

        SetSystemMessage($"{card.name} 카드를 플립 출연할 수 있습니다.");
    }

    private void OnConfirmFlipSummon()
    {
        if (pendingFlipSlot == null || pendingFlipCard == null)
        {
            ClearPendingFlipChoice();
            SetSystemMessage("플립 출연할 카드 또는 슬롯 정보가 없습니다.");
            return;
        }

        string failReason;
        if (!CanUseMyAction(out failReason))
        {
            ClearPendingFlipChoice();
            SetSystemMessage(failReason);
            return;
        }

        FlipSummonCharacter(pendingFlipSlot, pendingFlipCard);
    }

    private void CancelFlipSummonChoice()
    {
        string cardName = pendingFlipCard != null
            ? pendingFlipCard.name
            : "선택 카드";

        ClearPendingFlipChoice();

        SetSystemMessage($"{cardName}의 플립 출연을 취소했습니다.");
    }

    private void FlipSummonCharacter(BattleFieldSlot targetSlot, BaseCardData characterCard)
    {
        if (targetSlot == null || characterCard == null)
        {
            ClearPendingFlipChoice();
            SetSystemMessage("플립 출연 처리에 필요한 정보가 없습니다.");
            return;
        }

        if (targetSlot.characterOwner != BattleSlotOwner.My)
        {
            ClearPendingFlipChoice();
            SetSystemMessage("내 캐릭터만 플립 출연할 수 있습니다.");
            return;
        }

        if (!targetSlot.HasCharacter)
        {
            ClearPendingFlipChoice();
            SetSystemMessage("플립 출연할 캐릭터가 없습니다.");
            return;
        }

        if (!targetSlot.isCharacterFaceDown)
        {
            ClearPendingFlipChoice();
            SetSystemMessage("이미 앞면 상태인 캐릭터입니다.");
            return;
        }

        string turnFailReason;
        if (!CanFlipSummonByTurn(targetSlot, out turnFailReason))
        {
            ClearPendingFlipChoice();
            SetSystemMessage(turnFailReason);
            return;
        }

        int cost = GetCharacterAppearCost(characterCard);

        if (!CanPayViewerCost(myPlayer, cost))
        {
            ClearPendingFlipChoice();
            SetSystemMessage("시청자가 부족하여 플립 출연할 수 없습니다.");
            return;
        }

        Sprite sprite = LoadCardSprite(characterCard);

        if (sprite == null)
        {
            ClearPendingFlipChoice();
            SetSystemMessage($"{characterCard.name} 카드 이미지를 찾을 수 없습니다.");
            return;
        }

        myPlayer.viewers -= cost;

        targetSlot.SetCharacterCard(characterCard, sprite, false, targetSlot.characterOwner);

        ClearPendingFlipChoice();

        RefreshAllUI();

        ResolveMyActionUsed(
            $"{characterCard.name} 카드를 플립 출연했습니다.\n" +
            $"시청자 -{cost}"
        );
    }  

    private void OnClickContentCardOnField(BattleFieldSlot slot, BaseCardData card)
    {
        SelectCard(card);
        SetSystemMessage($"콘텐츠 카드 확인: {card.name}");
    }

    private void OnBeginDragFieldCharacter(
        BattleFieldSlot slot,
        BaseCardData card,
        PointerEventData eventData)
    {
        if (movementManager == null)
        {
            SetSystemMessage("MovementManager가 연결되어 있지 않습니다.");
            return;
        }

        movementManager.OnBeginDragFieldCharacter(slot, card, eventData);
    }

    private void OnDragFieldCharacter(
        BattleFieldSlot slot,
        BaseCardData card,
        PointerEventData eventData)
    {
        if (movementManager == null)
            return;

        movementManager.OnDragFieldCharacter(slot, card, eventData);
    }

    private void OnEndDragFieldCharacter(
        BattleFieldSlot slot,
        BaseCardData card,
        PointerEventData eventData)
    {
        if (movementManager == null)
            return;

        movementManager.OnEndDragFieldCharacter(slot, card, eventData);
    }

    private void OnDropHandCardOnFieldSlot(BattleFieldSlot slot, PointerEventData eventData)
    {
        if (movementManager != null && movementManager.IsDraggingMoveCard)
        {
            movementManager.OnDropMoveTargetSlot(slot, eventData);
            return;
        }
        
        if (slot == null)
        {
            ClearDraggingHandCard();
            ClearPendingSummonChoice();
            SetSystemMessage("대상 슬롯이 없습니다.");
            return;
        }

        if (questionPanel != null && questionPanel.IsOpen())
        {
            ClearDraggingHandCard();
            SetSystemMessage("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        if (!isDraggingHandCard || draggingHandCardData == null)
        {
            ClearDraggingHandCard();
            SetSystemMessage("드래그 중인 손패 카드가 없습니다.");
            return;
        }

        BaseCardData card = draggingHandCardData;

        string failReason;
        if (!CanOpenSummonQuestion(slot, card, out failReason))
        {
            ClearPendingSummonChoice();
            ClearDraggingHandCard();
            SetSystemMessage(failReason);
            return;
        }

        OpenSummonQuestion(slot, card);
    }

    private bool CanOpenSummonQuestion(BattleFieldSlot slot, BaseCardData card, out string failReason)
    {
        failReason = "";

        if (!CanUseMyAction(out failReason))
            return false;

        if (myPlayer == null)
        {
            failReason = "내 플레이어 데이터가 없습니다.";
            return false;
        }

        if (card == null)
        {
            failReason = "드롭한 카드 데이터가 없습니다.";
            return false;
        }

        if (card.kind != "Character")
        {
            failReason = "캐릭터 카드만 출연할 수 있습니다.";
            return false;
        }

        if (!myPlayer.hand.Contains(card))
        {
            failReason = "내 손패에 있는 카드만 출연할 수 있습니다.";
            return false;
        }

        if (slot == null)
        {
            failReason = "대상 슬롯이 없습니다.";
            return false;
        }

        if (slot.owner != BattleSlotOwner.My)
        {
            failReason = "내 방송 슬롯에만 캐릭터를 출연시킬 수 있습니다.";
            return false;
        }

        if (!slot.HasBroadcast)
        {
            failReason = "방송 카드가 설치된 슬롯에만 캐릭터를 출연시킬 수 있습니다.";
            return false;
        }

        if (slot.HasCharacter)
        {
            failReason = "이미 캐릭터가 있는 슬롯입니다.";
            return false;
        }

        return true;
    }

    private void OpenSummonQuestion(BattleFieldSlot slot, BaseCardData card)
    {
        string failReason;
        if (!CanUseMyAction(out failReason))
        {
            ClearPendingSummonChoice();
            ClearDraggingHandCard();
            SetSystemMessage(failReason);
            return;
        }

        if (questionPanel != null && questionPanel.IsOpen())
        {
            ClearPendingSummonChoice();
            ClearDraggingHandCard();
            SetSystemMessage("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        ClearPendingFlipChoice();

        pendingSummonSlot = slot;
        pendingSummonCard = card;

        int appearCost = GetCharacterAppearCost(card);
        bool canSummonFront = CanPayViewerCost(myPlayer, appearCost);
        bool canSummonBackside = !myHasSummonedFaceDownThisTurn;

        if (!canSummonFront && !canSummonBackside)
        {
            ClearPendingSummonChoice();
            ClearDraggingHandCard();

            SetSystemMessage(
                "불가능한 행동입니다.\n" +
                "시청자가 부족하여 앞면 출연할 수 없고,\n" +
                "이번 턴에는 이미 뒷면 출연을 했습니다."
            );

            return;
        }

        if (questionPanel == null)
        {
            SetSystemMessage(
                "QuestionPanel이 BattleManager에 연결되어 있지 않습니다.\n" +
                "BattleManager 인스펙터의 Question Panel 필드에 QuestionPanel 오브젝트를 연결해주세요."
            );
            return;
        }

        questionPanel.ShowSummonQuestion(
            "출연 방법을 선택해 주세요.",
            canSummonFront,
            canSummonBackside,
            OnSelectFrontSummonChoice,
            OnSelectBacksideSummonChoice,
            CancelSummonChoice
        );

        string frontState = canSummonFront
            ? "앞면 출연 가능"
            : "시청자가 부족하여 앞면 출연 불가";

        string backsideState = canSummonBackside
            ? "뒷면 출연 가능"
            : "이번 턴에는 이미 뒷면 출연을 했습니다.";

        SetSystemMessage(
            $"{card.name} 카드를 ({slot.x}, {slot.y}) 슬롯에 출연하려 합니다.\n" +
            $"{frontState}\n" +
            $"{backsideState}"
        );
    }

    private int GetCharacterAppearCost(BaseCardData card)
    {
        CharacterCardData character = card as CharacterCardData;

        if (character == null)
            return 0;

        return Mathf.Max(0, character.appearCost);
    }

    private bool CanPayViewerCost(BattlePlayerRuntime player, int cost)
    {
        if (player == null)
            return false;

        return player.viewers >= cost;
    }

    private bool CanFlipSummonByTurn(BattleFieldSlot slot, out string failReason)
    {
        failReason = "";

        if (slot == null)
        {
            failReason = "플립 출연할 슬롯 정보가 없습니다.";
            return false;
        }

        if (!slot.HasCharacter)
        {
            failReason = "플립 출연할 캐릭터가 없습니다.";
            return false;
        }

        if (!slot.isCharacterFaceDown)
        {
            failReason = "이미 앞면 상태인 캐릭터입니다.";
            return false;
        }

        if (slot.faceDownSummonedTurn >= 0 && turnCount <= slot.faceDownSummonedTurn)
        {
            failReason = "뒷면 출연한 턴에는 플립 출연할 수 없습니다.";
            return false;
        }

        return true;
    }

    private void OnSelectFrontSummonChoice()
    {
        if (pendingSummonSlot == null || pendingSummonCard == null)
        {
            ClearPendingSummonChoice();
            SetSystemMessage("앞면 출연할 카드 또는 슬롯 정보가 없습니다.");
            return;
        }

        BattleFieldSlot targetSlot = pendingSummonSlot;
        BaseCardData targetCard = pendingSummonCard;

        string failReason;
        if (!CanOpenSummonQuestion(targetSlot, targetCard, out failReason))
        {
            ClearPendingSummonChoice();
            ClearDraggingHandCard();
            SetSystemMessage(failReason);
            return;
        }

        SummonCharacterFront(targetSlot, targetCard);
    }

    private void OnSelectBacksideSummonChoice()
    {
        if (pendingSummonSlot == null || pendingSummonCard == null)
        {
            ClearPendingSummonChoice();
            ClearDraggingHandCard();
            SetSystemMessage("뒷면 출연할 카드 또는 슬롯 정보가 없습니다.");
            return;
        }

        string actionFailReason;
        if (!CanUseMyAction(out actionFailReason))
        {
            ClearPendingSummonChoice();
            ClearDraggingHandCard();
            SetSystemMessage(actionFailReason);
            return;
        }

        if (myHasSummonedFaceDownThisTurn)
        {
            ClearPendingSummonChoice();
            ClearDraggingHandCard();
            SetSystemMessage("뒷면 출연은 1턴에 1회만 가능합니다.");
            return;
        }

        BattleFieldSlot targetSlot = pendingSummonSlot;
        BaseCardData targetCard = pendingSummonCard;

        string failReason;
        if (!CanOpenSummonQuestion(targetSlot, targetCard, out failReason))
        {
            ClearPendingSummonChoice();
            ClearDraggingHandCard();
            SetSystemMessage(failReason);
            return;
        }

        SummonCharacterBackside(targetSlot, targetCard);
    }

    private void SummonCharacterBackside(BattleFieldSlot targetSlot, BaseCardData characterCard)
    {
        if (targetSlot == null || characterCard == null)
        {
            ClearPendingSummonChoice();
            SetSystemMessage("뒷면 출연 처리에 필요한 정보가 없습니다.");
            return;
        }

        if (myPlayer == null || myPlayer.hand == null)
        {
            ClearPendingSummonChoice();
            SetSystemMessage("내 손패 정보를 찾을 수 없습니다.");
            return;
        }

        if (!myPlayer.hand.Contains(characterCard))
        {
            ClearPendingSummonChoice();
            SetSystemMessage("내 손패에 없는 카드는 출연시킬 수 없습니다.");
            return;
        }

        if (targetSlot.owner != BattleSlotOwner.My)
        {
            ClearPendingSummonChoice();
            SetSystemMessage("내 슬롯에만 캐릭터를 출연시킬 수 있습니다.");
            return;
        }

        if (!targetSlot.HasBroadcast)
        {
            ClearPendingSummonChoice();
            SetSystemMessage("방송 카드가 설치된 슬롯에만 캐릭터를 출연시킬 수 있습니다.");
            return;
        }

        if (targetSlot.HasCharacter)
        {
            ClearPendingSummonChoice();
            SetSystemMessage("이미 캐릭터가 있는 슬롯입니다.");
            return;
        }

        targetSlot.SetCharacterCard(characterCard, cardBackSprite, true, BattleSlotOwner.My);
        targetSlot.faceDownSummonedTurn = turnCount;
        myPlayer.hand.Remove(characterCard);

        myHasSummonedFaceDownThisTurn = true;

        ClearPendingSummonChoice();
        ClearDraggingHandCard();

        RefreshAllUI();

        ResolveMyActionUsed(
            $"{characterCard.name} 카드를 뒷면으로 출연시켰습니다.\n" +
            $"위치: ({targetSlot.x}, {targetSlot.y})"
        );
    }

    private void SummonCharacterFront(BattleFieldSlot targetSlot, BaseCardData characterCard)
    {
        if (targetSlot == null || characterCard == null)
        {
            ClearPendingSummonChoice();
            SetSystemMessage("앞면 출연 처리에 필요한 정보가 없습니다.");
            return;
        }

        if (myPlayer == null || myPlayer.hand == null)
        {
            ClearPendingSummonChoice();
            SetSystemMessage("내 손패 정보를 찾을 수 없습니다.");
            return;
        }

        if (!myPlayer.hand.Contains(characterCard))
        {
            ClearPendingSummonChoice();
            SetSystemMessage("내 손패에 없는 카드는 출연시킬 수 없습니다.");
            return;
        }

        if (targetSlot.owner != BattleSlotOwner.My)
        {
            ClearPendingSummonChoice();
            SetSystemMessage("내 슬롯에만 캐릭터를 출연시킬 수 있습니다.");
            return;
        }

        if (!targetSlot.HasBroadcast)
        {
            ClearPendingSummonChoice();
            SetSystemMessage("방송 카드가 설치된 슬롯에만 캐릭터를 출연시킬 수 있습니다.");
            return;
        }

        if (targetSlot.HasCharacter)
        {
            ClearPendingSummonChoice();
            SetSystemMessage("이미 캐릭터가 있는 슬롯입니다.");
            return;
        }

        int cost = GetCharacterAppearCost(characterCard);

        if (!CanPayViewerCost(myPlayer, cost))
        {
            ClearPendingSummonChoice();
            SetSystemMessage("시청자가 부족하여 앞면 출연할 수 없습니다.");
            return;
        }

        Sprite sprite = LoadCardSprite(characterCard);

        if (sprite == null)
        {
            ClearPendingSummonChoice();
            SetSystemMessage($"{characterCard.name} 카드 이미지를 찾을 수 없습니다.");
            return;
        }

        myPlayer.viewers -= cost;

        targetSlot.SetCharacterCard(characterCard, sprite, false, BattleSlotOwner.My);
        myPlayer.hand.Remove(characterCard);

        ClearPendingSummonChoice();
        ClearDraggingHandCard();

        RefreshAllUI();

        ResolveMyActionUsed(
            $"{characterCard.name} 카드를 앞면으로 출연시켰습니다.\n" +
            $"시청자 -{cost}"
        );
    }

    private void CancelSummonChoice()
    {
        string cardName = pendingSummonCard != null
            ? pendingSummonCard.name
            : "선택 카드";

        ClearPendingSummonChoice();
        ClearDraggingHandCard();

        SetSystemMessage($"{cardName}의 출연 선택을 취소했습니다.");
    }

    private void ClearPendingSummonChoice()
    {
        pendingSummonSlot = null;
        pendingSummonCard = null;

        if (questionPanel != null && questionPanel.IsOpen())
            questionPanel.Hide();
    }

    private void ClearPendingFlipChoice()
    {
        pendingFlipSlot = null;
        pendingFlipCard = null;

        if (questionPanel != null && questionPanel.IsOpen())
            questionPanel.Hide();
    }

    private void Shuffle(List<BaseCardData> deck)
    {
        if (deck == null) return;

        for (int i = 0; i < deck.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, deck.Count);
            BaseCardData temp = deck[i];
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
    }

    private void DrawCards(BattlePlayerRuntime player, int count)
    {
        if (player == null) return;

        for (int i = 0; i < count; i++)
        {
            if (player.mainDeck.Count == 0)
            {
                Debug.LogWarning($"{player.playerName}의 메인 덱이 비어 있어 더 이상 드로우할 수 없습니다.");
                return;
            }

            BaseCardData drawnCard = player.mainDeck[0];
            player.mainDeck.RemoveAt(0);
            player.hand.Add(drawnCard);
        }
    }

    private void RefreshAllUI()
    {
        RefreshStatusUI();
        RefreshZoneTexts();
        RefreshHandUI();
        RefreshEnemyHandUI();

        if (currentPhase == BattlePhase.BroadcastSetup)
            RefreshBroadcastSetupButtons();
    }

    private void RefreshStatusUI()
    {
        if (myStatusText != null && myPlayer != null)
        {
            myStatusText.text =
                $"{myPlayer.playerName}\n" +
                $"시청자: {myPlayer.viewers}\n" +
                $"메인 덱: {myPlayer.mainDeck.Count}\n" +
                $"방송 덱: {myPlayer.broadcastDeck.Count}\n" +
                $"손패: {myPlayer.hand.Count}\n" +
                $"방송 설치: {myBroadcastPlacedCount}/{myRequiredBroadcastCount}";
        }

        if (enemyStatusText != null && enemyPlayer != null)
        {
            enemyStatusText.text =
                $"{enemyPlayer.playerName}\n" +
                $"시청자: {enemyPlayer.viewers}\n" +
                $"메인 덱: {enemyPlayer.mainDeck.Count}\n" +
                $"방송 덱: {enemyPlayer.broadcastDeck.Count}\n" +
                $"손패: {enemyPlayer.hand.Count}\n" +
                $"방송 설치: {enemyBroadcastPlacedCount}/{enemyRequiredBroadcastCount}";
        }

        RefreshViewerTextUI();
    }

    private void RefreshViewerTextUI()
    {
        if (myViewerText != null && myPlayer != null)
            myViewerText.text = $"{myPlayer.viewers}";

        if (enemyViewerText != null && enemyPlayer != null)
            enemyViewerText.text = $"{enemyPlayer.viewers}";
    }

    private void RefreshZoneTexts()
    {
        if (myPlayer == null || enemyPlayer == null) return;

        SetZoneCardVisual(myIdolSlot, myPlayer.idolCard, false, SelectCard, false);
        SetZoneCardVisual(enemyIdolSlot, enemyPlayer.idolCard, false, SelectCard, true);

        SetZoneCardVisual(myDeckSlot, null, true);
        SetZoneCardVisual(myBroadcastDeckSlot, null, true);
        SetZoneCardVisual(myRestSlot, null, true);
        SetZoneCardVisual(enemyDeckSlot, null, true, null, true);
        SetZoneCardVisual(enemyBroadcastDeckSlot, null, true, null, true);
        SetZoneCardVisual(enemyRestSlot, null, true, null, true);

        SetZoneLabel(myIdolSlot, myPlayer.idolCard != null ? myPlayer.idolCard.name : "아이돌 없음");
        SetZoneLabel(enemyIdolSlot, enemyPlayer.idolCard != null ? enemyPlayer.idolCard.name : "상대 아이돌 없음");

        SetZoneLabel(myDeckSlot, $"메인 덱\n{myPlayer.mainDeck.Count}장");
        SetZoneLabel(myBroadcastDeckSlot, $"방송 덱\n{myPlayer.broadcastDeck.Count}장");
        SetZoneLabel(myRestSlot, $"휴식존\n{myPlayer.restZone.Count}장");
        SetZoneLabel(enemyDeckSlot, $"상대 메인 덱\n{enemyPlayer.mainDeck.Count}장");
        SetZoneLabel(enemyBroadcastDeckSlot, $"상대 방송 덱\n{enemyPlayer.broadcastDeck.Count}장");
        SetZoneLabel(enemyRestSlot, $"상대 휴식존\n{enemyPlayer.restZone.Count}장");
    }

    private void RefreshHandUI()
    {
        if (myHandPanel == null || myPlayer == null) return;

        ClearChildren(myHandPanel);

        foreach (BaseCardData card in myPlayer.hand)
        {
            CreateHandCardItem(card, myHandPanel);
        }
    }

    private void RefreshEnemyHandUI()
    {
        if (enemyPlayer == null) return;

        int handCount = enemyPlayer.hand.Count;

        if (enemyHandCountText != null)
        {
            enemyHandCountText.text = $"{handCount}장";
        }

        if (enemyHandCardArea == null) return;

        ClearChildren(enemyHandCardArea);

        for (int i = 0; i < handCount; i++)
        {
            CreateEnemyHandBackCard(enemyHandCardArea);
        }
    }

    private void CreateEnemyHandBackCard(Transform parent)
    {
        GameObject cardObject = new GameObject(
            "EnemyHandBackCard",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement)
        );

        cardObject.transform.SetParent(parent, false);

        RectTransform rect = cardObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(108f, 154f);

        LayoutElement layoutElement = cardObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 108f;
        layoutElement.preferredHeight = 154f;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;

        Image image = cardObject.GetComponent<Image>();

        if (cardBackSprite != null)
        {
            image.sprite = cardBackSprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.enabled = true;
        }
        else
        {
            image.enabled = false;
        }

        image.raycastTarget = false;
    }

    private void CreateHandCardItem(BaseCardData card, Transform parent)
    {
        GameObject itemObject;

        if (handCardItemPrefab != null)
        {
            itemObject = Instantiate(handCardItemPrefab, parent);
        }
        else
        {
            itemObject = CreateFallbackHandCardItem(parent);
        }

        DeckCardItemUI cardItemUI = itemObject.GetComponent<DeckCardItemUI>();

        if (cardItemUI != null)
        {
            cardItemUI.SetCard(
                card,
                leftClickAction: SelectCard,
                rightClickAction: null,
                doubleClickAction: null
            );

            bool canDrag = CanStartHandCardDrag(card);

            cardItemUI.SetDragActions(
                canDrag,
                OnBeginDragHandCard,
                OnDragHandCard,
                OnEndDragHandCard
            );
        }
        else
        {
            TMP_Text text = itemObject.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = $"{GetKoreanKind(card.kind)}\n{card.name}";

            Image image = null;

            Transform cardImageTransform = itemObject.transform.Find("CardImage");
            if (cardImageTransform != null)
                image = cardImageTransform.GetComponent<Image>();

            if (image == null)
                image = itemObject.GetComponent<Image>();

            if (image != null)
            {
                Sprite cardSprite = LoadCardSprite(card);

                if (cardSprite != null)
                {
                    image.sprite = cardSprite;
                    image.color = Color.white;
                }

                image.preserveAspect = true;
            }

            Button button = itemObject.GetComponent<Button>();
            if (button == null)
                button = itemObject.AddComponent<Button>();

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectCard(card));
        }
    }

    private bool CanStartHandCardDrag(BaseCardData card)
    {
        if (card == null)
            return false;

        string failReason;
        if (!CanUseMyAction(out failReason))
            return false;

        if (myPlayer == null || myPlayer.hand == null)
            return false;

        if (!myPlayer.hand.Contains(card))
            return false;

        if (card.kind != "Character")
            return false;

        if (questionPanel != null && questionPanel.IsOpen())
            return false;

        if (pendingSummonCard != null || pendingSummonSlot != null)
            return false;

        if (pendingFlipCard != null || pendingFlipSlot != null)
            return false;

        return true;
    }

    private void OnBeginDragHandCard(
        DeckCardItemUI cardItemUI,
        BaseCardData card,
        PointerEventData eventData)
    {
        if (!CanStartHandCardDrag(card))
        {
            ClearDraggingHandCard();
            DestroyDragPreview();
            return;
        }

        draggingHandCardItem = cardItemUI;
        draggingHandCardData = card;
        isDraggingHandCard = true;

        SelectCard(card);
        CreateDragPreview(card, cardItemUI, eventData);
        UpdateDragPreviewPosition(eventData);

        SetSystemMessage(
            $"캐릭터 카드 드래그 시작: {card.name}\n" +
            "카드를 배치할 방송 슬롯 위로 이동하세요."
        );
    }

    private void OnDragHandCard(
        DeckCardItemUI cardItemUI,
        BaseCardData card,
        PointerEventData eventData)
    {
        if (!isDraggingHandCard)
            return;

        if (draggingHandCardData == null)
            return;

        UpdateDragPreviewPosition(eventData);
    }

    private void OnEndDragHandCard(
        DeckCardItemUI cardItemUI,
        BaseCardData card,
        PointerEventData eventData)
    {
        if (!isDraggingHandCard)
            return;

        string cardName = draggingHandCardData != null
            ? draggingHandCardData.name
            : "알 수 없는 카드";

        bool hasPendingSummonChoice = pendingSummonCard != null && pendingSummonSlot != null;

        ClearDraggingHandCard();
        DestroyDragPreview();

        if (hasPendingSummonChoice)
            return;

        SetSystemMessage(
            $"캐릭터 카드 드래그 종료: {cardName}\n" +
            "출연할 슬롯 위에 카드를 내려놓지 않았습니다."
        );
    }

    private void ClearDraggingHandCard()
    {
        draggingHandCardItem = null;
        draggingHandCardData = null;
        isDraggingHandCard = false;

        DestroyDragPreview();
    }

    private void CreateDragPreview(
        BaseCardData card,
        DeckCardItemUI sourceItem,
        PointerEventData eventData)
    {
        DestroyDragPreview();

        Canvas canvas = ResolveDragPreviewCanvas();

        if (canvas == null)
        {
            Debug.LogWarning("드래그 프리뷰를 표시할 Canvas를 찾지 못했습니다.");
            return;
        }

        Sprite sprite = LoadCardSprite(card);

        if (sprite == null)
        {
            Debug.LogWarning($"드래그 프리뷰용 카드 이미지를 찾을 수 없습니다: {card?.name}");
            return;
        }

        dragPreviewObject = new GameObject(
            "RuntimeDragPreviewCard",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image)
        );

        dragPreviewObject.transform.SetParent(canvas.transform, false);
        dragPreviewObject.transform.SetAsLastSibling();

        dragPreviewRect = dragPreviewObject.GetComponent<RectTransform>();
        dragPreviewRect.anchorMin = new Vector2(0.5f, 0.5f);
        dragPreviewRect.anchorMax = new Vector2(0.5f, 0.5f);
        dragPreviewRect.pivot = new Vector2(0.5f, 0.5f);
        dragPreviewRect.sizeDelta = GetDragPreviewSize(sourceItem);

        dragPreviewImage = dragPreviewObject.GetComponent<Image>();
        dragPreviewImage.sprite = sprite;
        dragPreviewImage.color = Color.white;
        dragPreviewImage.preserveAspect = true;
        dragPreviewImage.raycastTarget = false;

        CanvasGroup canvasGroup = dragPreviewObject.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasGroup.alpha = 0.85f;

        UpdateDragPreviewPosition(eventData);
    }

    private Canvas ResolveDragPreviewCanvas()
    {
        if (dragPreviewCanvas != null)
            return dragPreviewCanvas;

        Canvas parentCanvas = GetComponentInParent<Canvas>();

        if (parentCanvas != null)
        {
            dragPreviewCanvas = parentCanvas;
            return dragPreviewCanvas;
        }

        dragPreviewCanvas = FindAnyObjectByType<Canvas>();

        return dragPreviewCanvas;
    }

    private Vector2 GetDragPreviewSize(DeckCardItemUI sourceItem)
    {
        if (sourceItem != null)
        {
            RectTransform sourceRect = sourceItem.GetComponent<RectTransform>();

            if (sourceRect != null)
            {
                Vector2 sourceSize = sourceRect.rect.size;

                if (sourceSize.x > 1f && sourceSize.y > 1f)
                    return sourceSize;
            }
        }

        return dragPreviewSize;
    }

    private void UpdateDragPreviewPosition(PointerEventData eventData)
    {
        if (dragPreviewRect == null)
            return;

        Canvas canvas = ResolveDragPreviewCanvas();

        if (canvas == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;

        if (canvasRect == null)
            return;

        Camera targetCamera = null;

        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            targetCamera = canvas.worldCamera;

        Vector2 localPoint;

        bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            targetCamera,
            out localPoint
        );

        if (!success)
            return;

        dragPreviewRect.anchoredPosition = localPoint;
    }

    private void DestroyDragPreview()
    {
        if (dragPreviewObject != null)
        {
            Destroy(dragPreviewObject);
        }

        dragPreviewObject = null;
        dragPreviewRect = null;
        dragPreviewImage = null;
    }

    private GameObject CreateFallbackHandCardItem(Transform parent)
    {
        GameObject itemObject = new GameObject("HandCardItem", typeof(RectTransform), typeof(Image), typeof(Button));
        itemObject.transform.SetParent(parent, false);

        RectTransform rect = itemObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(90f, 122f);

        Image image = itemObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.25f);

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

        if (runtimeLabelFont != null)
            text.font = runtimeLabelFont;

        return itemObject;
    }

    private void SelectCard(BaseCardData card)
    {
        if (card == null)
            return;

        selectedCard = card;

        if (cardDetailPanel != null)
            cardDetailPanel.ShowCard(card);

        SetSystemMessage($"선택 카드: {card.name}");
    }

    private Sprite LoadCardSprite(BaseCardData card)
    {
        if (card == null || string.IsNullOrEmpty(card.image))
            return null;

        return Resources.Load<Sprite>(card.image);
    }

    private void SetZoneCardVisual(
        Transform zone,
        BaseCardData card,
        bool faceDown,
        Action<BaseCardData> clickAction = null,
        bool rotate180 = false)
    {
        if (zone == null) return;

        Image cardImage = GetOrCreateRuntimeCardImage(zone);
        if (cardImage == null) return;

        Sprite sprite = faceDown ? cardBackSprite : LoadCardSprite(card);

        if (sprite == null)
        {
            cardImage.enabled = false;
            SetRuntimeImageButton(cardImage, null, null);
            return;
        }

        cardImage.enabled = true;
        cardImage.sprite = sprite;
        cardImage.color = Color.white;
        cardImage.preserveAspect = true;

        RectTransform imageRect = cardImage.GetComponent<RectTransform>();
        if (imageRect != null)
            imageRect.localEulerAngles = rotate180 ? new Vector3(0f, 0f, 180f) : Vector3.zero;

        if (!faceDown && card != null && clickAction != null)
        {
            SetRuntimeImageButton(cardImage, card, clickAction);
        }
        else
        {
            SetRuntimeImageButton(cardImage, null, null);
        }
    }

    private Image GetOrCreateRuntimeCardImage(Transform zone)
    {
        Transform existing = zone.Find("RuntimeCardImage");

        if (existing != null)
        {
            Image existingImage = existing.GetComponent<Image>();

            if (existingImage != null)
                return existingImage;
        }

        GameObject imageObject = new GameObject("RuntimeCardImage", typeof(RectTransform), typeof(Image), typeof(Button));
        imageObject.transform.SetParent(zone, false);
        imageObject.transform.SetAsFirstSibling();

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6f, 6f);
        rect.offsetMax = new Vector2(-6f, -6f);

        Image image = imageObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;

        Button button = imageObject.GetComponent<Button>();
        button.enabled = false;
        button.interactable = false;

        return image;
    }

    private void SetRuntimeImageButton(
        Image image,
        BaseCardData card,
        Action<BaseCardData> clickAction)
    {
        if (image == null)
            return;

        Button button = image.GetComponent<Button>();

        if (button == null)
            button = image.gameObject.AddComponent<Button>();

        button.onClick.RemoveAllListeners();

        if (card != null && clickAction != null)
        {
            BaseCardData capturedCard = card;

            image.raycastTarget = true;
            button.enabled = true;
            button.interactable = true;
            button.onClick.AddListener(() => clickAction(capturedCard));
        }
        else
        {
            image.raycastTarget = false;
            button.interactable = false;
            button.enabled = false;
        }
    }

    public void PlaceCardOnSlot(BaseCardData card, Transform slot)
    {
        if (card == null || slot == null) return;

        SetZoneCardVisual(slot, card, false, SelectCard);
        SetZoneLabel(slot, card.name);
        SetSystemMessage($"{card.name} 카드를 슬롯에 배치했습니다.");
    }

    private void OnClickTurnEndButton()
    {
        if (myPlayer == null || enemyPlayer == null)
        {
            SetSystemMessage("아직 배틀 준비가 완료되지 않았습니다.");
            return;
        }

        if (currentPhase != BattlePhase.MainGame)
        {
            SetSystemMessage("아직 본게임 단계가 아닙니다.");
            return;
        }

        if (currentActionSide != BattlePlayerSide.My)
        {
            SetSystemMessage("현재는 내 행동권이 아닙니다.");
            return;
        }

        ResolveMyActionPass();
    }

    private int GainPrepViewers(BattleSlotOwner characterOwner)
    {
        BattlePlayerRuntime player = GetPlayerRuntime(characterOwner);

        if (player == null)
            return 0;

        int gainedViewers = CalculatePrepViewerGain(characterOwner);

        player.viewers += gainedViewers;

        return gainedViewers;
    }

    private int CalculatePrepViewerGain(BattleSlotOwner characterOwner)
    {
        int totalGain = 0;

        AddPrepViewerGainFromSlots(myBattleSlots, characterOwner, ref totalGain);
        AddPrepViewerGainFromSlots(enemyBattleSlots, characterOwner, ref totalGain);

        return Mathf.Max(0, totalGain);
    }

    private void AddPrepViewerGainFromSlots(
        List<BattleFieldSlot> slots,
        BattleSlotOwner characterOwner,
        ref int totalGain)
    {
        if (slots == null)
            return;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot == null)
                continue;

            if (!slot.HasBroadcast)
                continue;

            if (!slot.HasCharacter)
                continue;

            if (slot.characterOwner != characterOwner)
                continue;

            BattlePlayerRuntime fieldOwner = GetPlayerRuntime(slot.owner);
            int baseGain = GetIdolBaseViewersPerPrep(fieldOwner);
            int slotGain = baseGain;
            slotGain += GetBroadcastViewersModifier(slot.broadcastCard);

            totalGain += slotGain;
        }
    }

    private BattlePlayerRuntime GetPlayerRuntime(BattleSlotOwner owner)
    {
        return owner == BattleSlotOwner.My ? myPlayer : enemyPlayer;
    }

    private int GetIdolBaseViewersPerPrep(BattlePlayerRuntime player)
    {
        if (player == null || player.idolCard == null)
            return 0;

        IdolCardData idol = player.idolCard as IdolCardData;

        if (idol == null)
            return 0;

        return Mathf.Max(0, idol.baseViewersPerPrep);
    }

    private int GetBroadcastViewersModifier(BaseCardData card)
    {
        if (card == null)
            return 0;

        BroadcastCardData broadcast = card as BroadcastCardData;

        if (broadcast == null)
            return 0;

        return broadcast.viewersModifier;
    }

    private void SetZoneLabel(Transform zone, string text)
    {
        if (zone == null) return;

        TMP_Text label = zone.GetComponentInChildren<TMP_Text>();

        if (label == null)
        {
            GameObject labelObject = new GameObject("RuntimeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(zone, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            label = labelObject.GetComponent<TMP_Text>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 18f;
            label.textWrappingMode = TextWrappingModes.Normal;

            if (runtimeLabelFont != null)
                label.font = runtimeLabelFont;
        }

        label.raycastTarget = false;
        label.text = text;
    }

    private void ClearChildren(Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            if (parent.GetChild(i) != null)
                Destroy(parent.GetChild(i).gameObject);
        }
    }

    private void SetSystemMessage(string message)
    {
        if (systemMessageText != null)
            systemMessageText.text = message;

        Debug.Log(message);

        ShowSystemMessageWithFade();
    }

    private void ShowSystemMessageWithFade()
    {
        if (systemMessageCanvasGroup == null)
            return;

        if (systemMessageFadeCoroutine != null)
            StopCoroutine(systemMessageFadeCoroutine);

        systemMessageFadeCoroutine = StartCoroutine(SystemMessageFadeRoutine());
    }

    private IEnumerator SystemMessageFadeRoutine()
    {
        systemMessageCanvasGroup.alpha = 1f;

        if (systemMessageVisibleTime > 0f)
            yield return new WaitForSeconds(systemMessageVisibleTime);

        float elapsed = 0f;
        float fadeDuration = Mathf.Max(0.01f, systemMessageFadeTime);

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            systemMessageCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        systemMessageCanvasGroup.alpha = 0f;
        systemMessageFadeCoroutine = null;
    }

    private string GetKoreanKind(string kind)
    {
        switch (kind)
        {
            case "Idol":
                return "아이돌";
            case "Broadcast":
                return "방송";
            case "Character":
                return "캐릭터";
            case "Content":
                return "콘텐츠";
            default:
                return kind;
        }
    }
}

[Serializable]
public class BattlePlayerRuntime
{
    public string playerName;
    public int viewers;

    public BaseCardData idolCard;
    public List<BaseCardData> broadcastDeck = new List<BaseCardData>();
    public List<BaseCardData> mainDeck = new List<BaseCardData>();
    public List<BaseCardData> hand = new List<BaseCardData>();
    public List<BaseCardData> restZone = new List<BaseCardData>();
}
