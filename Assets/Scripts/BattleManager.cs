using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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

    [Header("Test Settings")]
    [Tooltip("테스트 중 승리 판정을 끌 수 있습니다.")]
    public bool victoryCheckEnabled = true;

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

    [Header("Rest Zone UI")]
    [Tooltip("비워두면 BroadcastSelectPanel을 임시로 재사용합니다.")]
    public GameObject restZonePanel;
    [Tooltip("비워두면 BroadcastSelectContent를 임시로 재사용합니다.")]
    public Transform restZoneContent;
    public Button restZoneCloseButton;
    [Tooltip("비워두면 broadcastSelectCardItemPrefab 또는 handCardItemPrefab을 사용합니다.")]
    public GameObject restZoneCardItemPrefab;

    [Header("Question Panel")]
    public QuestionPanel questionPanel;
    public CardQuestionPanel cardQuestionPanel;

    [Header("Battle Result UI")]
    public GameObject battleResultPanel;
    public TMP_Text battleResultText;

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
    [SerializeField] private SimpleMessagePanelController simpleMessagePanel;
    [SerializeField] private CanvasGroup systemMessageCanvasGroup;
    [SerializeField] private float systemMessageVisibleTime = 2f;
    [SerializeField] private float actionTransferMessageVisibleTime = 1.5f;
    [SerializeField] private float systemMessageFadeTime = 0.5f;

    [Header("Fonts")]
    [SerializeField] private TMP_FontAsset runtimeLabelFont;

    [Header("Images")]
    [Tooltip("메인 덱/방송 덱/상대 패처럼 뒷면으로 보여줄 때 사용하는 카드 뒷면 이미지입니다.")]
    [SerializeField] private Sprite cardBackSprite;

    [Header("Hand Selection Highlight")]
    [SerializeField] private Color selectedHandCardOutlineColor = new Color(1f, 0.86f, 0.2f, 1f);
    [SerializeField] private Vector2 selectedHandCardOutlineDistance = new Vector2(4f, 4f);

    [Header("Drag Preview")]
    [Tooltip("드래그 중인 카드 이미지를 띄울 Canvas입니다. 비워두면 자동으로 부모 Canvas를 찾습니다.")]
    public Canvas dragPreviewCanvas;

    [Tooltip("드래그 중 마우스를 따라다니는 카드 이미지 크기입니다.")]
    public Vector2 dragPreviewSize = new Vector2(108f, 154f);

    [Header("Draw Animation")]
    [SerializeField] private float drawAnimationDuration = 0.35f;
    [SerializeField] private Vector2 drawAnimationCardSize = new Vector2(90f, 122f);

    [Header("Detail Panel")]
    public CardDetailPanel cardDetailPanel;

    [Header("Buttons")]
    public Button turnEndButton;
    public Image turnEndButtonPanelImage;

    [Header("Turn End Button Panel Colors")]
    [SerializeField] private Color myTurnEndPanelColor = new Color(0.72f, 0.86f, 1f, 1f);
    [SerializeField] private Color enemyTurnEndPanelColor = new Color(1f, 0.74f, 0.74f, 1f);
    [SerializeField] private Color inactiveTurnEndPanelColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("Sub Managers")]
    public SummonManager summonManager;
    public MovementManager movementManager;
    public CollaborationManager collaborationManager;
    public EffectManager effectManager;

    private readonly List<BaseCardData> allCards = new List<BaseCardData>();
    private readonly List<BattleFieldSlot> myBattleSlots = new List<BattleFieldSlot>();
    private readonly List<BattleFieldSlot> enemyBattleSlots = new List<BattleFieldSlot>();

    private BattlePlayerRuntime myPlayer;
    private BattlePlayerRuntime enemyPlayer;

    private BaseCardData selectedCard;
    private int selectedHandCardIndex = -1;
    private BattleFieldSlot selectedBroadcastTargetSlot;

    private BaseCardData pendingContentCard;
    private int pendingContentHandIndex = -1;
    private BattleFieldSlot pendingContentInstallSlot;
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
    private bool isRestZonePanelOpen = false;
    private BattlePlayerSide openRestZoneSide = BattlePlayerSide.My;
    private const int VictoryViewerThreshold = 100000;
    private bool isGameOver = false;
    private bool isBusy = false;
    private string battleBusyReason = "";
    private float battleBusyStartedRealtime = -1f;
    private bool isBattleEnded = false;
    private bool isVictoryTiebreakerActive = false;
    private bool myActionUsedThisActionTurn = false;
    private bool isEndActionButtonFlow = false;
    private bool hasUsedMyIdolActiveThisTurn = false;
    private bool hasUsedEnemyIdolActiveThisTurn = false;
    private float lastIdolClickTime = -10f;
    private readonly HashSet<BattleFieldSlot> resolvingRestSlots = new HashSet<BattleFieldSlot>();
    private readonly HashSet<BattleFieldSlot> pendingFieldSlotSelectionValidSlots = new HashSet<BattleFieldSlot>();
    private Action<BattleFieldSlot> pendingFieldSlotSelectionSelectedAction;
    private Action pendingFieldSlotSelectionCancelAction;
    private bool isFieldSlotSelectionModeActive;

    private bool enemyHasSummonedFaceDownThisTurn = false;
    private TestEnemy testEnemyController;

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
        ResolveSimpleMessagePanel();

        if (summonManager == null)
            summonManager = GetComponentInChildren<SummonManager>();

        if (summonManager == null)
            summonManager = gameObject.AddComponent<SummonManager>();

        if (summonManager != null)
            summonManager.Init(this);

        if (movementManager == null)
            movementManager = GetComponentInChildren<MovementManager>();

        if (movementManager != null)
            movementManager.Init(this);

        if (collaborationManager == null)
            collaborationManager = GetComponentInChildren<CollaborationManager>();

        if (collaborationManager != null)
            collaborationManager.Init(this);

        if (effectManager == null)
            effectManager = GetComponentInChildren<EffectManager>();

        if (effectManager != null)
            effectManager.Init(this);

        if (turnEndButton != null)
            turnEndButton.onClick.AddListener(OnClickTurnEndButton);

        if (cardDetailPanel != null)
            cardDetailPanel.Init(this);

        ResolveTurnEndButtonPanelImage();
        ResolvePanelCloseButtons();
        ResolveBattleResultPanel();

        if (broadcastSelectCancelButton != null)
            broadcastSelectCancelButton.onClick.AddListener(OnClickSelectPanelCancelButton);

        if (restZoneCloseButton != null)
            restZoneCloseButton.onClick.AddListener(CloseRestZonePanel);

        if (questionPanel != null)
            questionPanel.Hide();

        if (cardQuestionPanel != null)
        {
            cardQuestionPanel.Configure(
                handCardItemPrefab,
                SetSystemMessage,
                ShowCardQuestionDetailPreview
            );
            cardQuestionPanel.Hide();
        }

        CloseBroadcastSelectPanel();
        CloseRestZonePanel();

        if (battleResultPanel != null)
            battleResultPanel.SetActive(false);

        StartBattleSetup();
    }

    private void Update()
    {
        if (!isFieldSlotSelectionModeActive)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            CancelPendingFieldSlotSelection();
    }

    private void ResolveBattleResultPanel()
    {
        if (battleResultPanel == null)
        {
            GameObject foundPanel = FindSceneGameObjectByName("BattleResultPanel");
            if (foundPanel != null)
                battleResultPanel = foundPanel;
        }

        if (battleResultPanel == null)
            return;

        if (battleResultText == null)
            battleResultText = battleResultPanel.GetComponentInChildren<TMP_Text>(true);

        Button panelButton = battleResultPanel.GetComponent<Button>();
        if (panelButton == null)
            panelButton = battleResultPanel.AddComponent<Button>();

        Graphic panelGraphic = battleResultPanel.GetComponent<Graphic>();
        if (panelGraphic == null)
        {
            Image image = battleResultPanel.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.01f);
            panelGraphic = image;
        }

        panelGraphic.raycastTarget = true;

        panelButton.onClick.RemoveAllListeners();
        panelButton.onClick.AddListener(() =>
        {
            if (!isBattleEnded)
                return;

            SceneManager.LoadScene("BattleLobbyScene");
        });
    }

    private GameObject FindSceneGameObjectByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        GameObject activeObject = GameObject.Find(objectName);
        if (activeObject != null)
            return activeObject;

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj == null || obj.name != objectName)
                continue;

            if (!obj.scene.IsValid())
                continue;

            return obj;
        }

        return null;
    }

    private void ResolveTurnEndButtonPanelImage()
    {
        if (turnEndButtonPanelImage != null)
            return;

        GameObject panelObject = FindSceneGameObjectByName("TurnEndButtonPanel");
        if (panelObject != null)
            turnEndButtonPanelImage = panelObject.GetComponent<Image>();

        if (turnEndButtonPanelImage == null && turnEndButton != null)
            turnEndButtonPanelImage = turnEndButton.GetComponentInParent<Image>();
    }

    private void ResolvePanelCloseButtons()
    {
        if (broadcastSelectCancelButton == null && broadcastSelectPanel != null)
            broadcastSelectCancelButton = FindCloseButtonInPanel(broadcastSelectPanel);

        if (restZoneCloseButton == null)
        {
            if (restZonePanel != null)
            {
                restZoneCloseButton = FindCloseButtonInPanel(restZonePanel);
            }
            else if (broadcastSelectPanel != null)
            {
                restZoneCloseButton = broadcastSelectCancelButton;
            }
        }
    }

    private Button FindCloseButtonInPanel(GameObject panel)
    {
        if (panel == null)
            return null;

        Button[] buttons = panel.GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            if (IsCloseButtonName(button.gameObject.name))
                return button;
        }

        return null;
    }

    private bool IsCloseButtonName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        return objectName.IndexOf("Cancel", StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.Contains("취소") ||
            objectName.Contains("닫");
    }

    private void StartBattleSetup()
    {
        SetSystemMessage("배틀 준비를 시작합니다.");

        if (BattleStartSettings.HasSelectedMyPreset)
        {
            presetIndex = BattleStartSettings.SelectedMyPresetIndex;
            Debug.Log($"BattleLobbyScene 선택 프리셋 적용: Preset {presetIndex + 1}");
        }

        isGameOver = false;
        SetBattleBusy(false, "StartBattleSetup");
        isBattleEnded = false;
        isVictoryTiebreakerActive = false;
        turnCount = 1;

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
                OnPointerClickFieldSlot,
                OnBeginDragFieldCharacter,
                OnDragFieldCharacter,
                OnEndDragFieldCharacter
            );
            slot.SetCharacterDoubleClickAction(OnDoubleClickCharacterCardOnField);

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
                OnPointerClickFieldSlot,
                OnBeginDragFieldCharacter,
                OnDragFieldCharacter,
                OnEndDragFieldCharacter
            );
            slot.SetCharacterDoubleClickAction(OnDoubleClickCharacterCardOnField);

            slot.ClearAllCards();
            slot.SetSetupButtonVisible(false);
        }

        SetupRestZoneButtons();
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

        StartCoroutine(PlaySimplePanelMessageRoutine(
            "방송 배치 시간",
            SimpleMessageExitDirection.LeftToRight
        ));

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
        string inputFailReason;
        if (IsInputBlocked(out inputFailReason))
        {
            SetSystemMessage(inputFailReason);
            return;
        }

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

    public BaseCardData DebugFindCardById(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return null;

        return allCards.FirstOrDefault(card =>
            card != null &&
            string.Equals(card.id, cardId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public bool DebugGiveCardToHand(
        BattleSlotOwner owner,
        string cardId,
        out string message)
    {
        message = "";

        BattlePlayerRuntime targetPlayer = GetPlayerRuntime(owner);

        if (targetPlayer == null || targetPlayer.hand == null)
        {
            message = "Cheat failed: player hand not ready";
            return false;
        }

        BaseCardData card = DebugFindCardById(cardId);

        if (card == null)
        {
            message = "Cheat failed: card id not found";
            return false;
        }

        targetPlayer.hand.Add(card);
        RefreshAllUI();

        message = $"Cheat give: {card.id} -> {FormatCheatOwner(owner)} hand";
        return true;
    }

    public void DebugClearPendingAndBusyState()
    {
        ClearAllPendingInteractionStates("DebugClearPendingAndBusyState");
        SetSystemMessage("처리 상태를 초기화했습니다.");
    }

    public void DebugPrintActionState(string reason)
    {
        Debug.Log(
            $"[ActionState] {reason}\n" +
            $"turn={turnCount}\n" +
            $"currentActionSide={currentActionSide}\n" +
            $"currentPhase={currentPhase}\n" +
            $"battleBusy={IsBattleBusy()}\n" +
            $"battleBusyReason={GetBattleBusyReason()}\n" +
            $"isEndActionButtonFlow={isEndActionButtonFlow}\n" +
            $"myActionUsedThisActionTurn={myActionUsedThisActionTurn}\n" +
            $"pending={BuildPendingStateSummary()}"
        );
    }

    public bool DebugSummonCharacterToSlot(
        BattleSlotOwner owner,
        string coord,
        string cardId,
        out string message)
    {
        message = "";

        if (!TryParseDebugCoord(coord, out int x, out int y))
        {
            message = "Cheat failed: invalid coord";
            return false;
        }

        BattleFieldSlot targetSlot = FindBattleSlot(owner, x, y);

        if (targetSlot == null)
        {
            message = "Cheat failed: target slot not found";
            return false;
        }

        if (!targetSlot.HasBroadcast)
        {
            message = "Cheat failed: target slot has no broadcast card";
            return false;
        }

        if (targetSlot.HasCharacter)
        {
            message = "Cheat failed: target slot already has a character";
            return false;
        }

        BaseCardData card = DebugFindCardById(cardId);

        if (card == null)
        {
            message = "Cheat failed: card id not found";
            return false;
        }

        if (!IsCharacterCardKind(card))
        {
            message = "Cheat failed: summon only supports Character cards";
            return false;
        }

        Sprite sprite = LoadCardSprite(card);

        if (sprite == null)
        {
            message = "Cheat failed: card image not found";
            return false;
        }

        targetSlot.SetCharacterCard(card, sprite, false, owner);
        ApplyBroadcastEnterEffectsFromExternal(targetSlot, false);
        RefreshAllUI();

        message = $"Cheat summon: {card.id} -> {FormatCheatOwner(owner)} {coord.Trim()}";
        return true;
    }

    private BattleFieldSlot FindBattleSlot(BattleSlotOwner owner, int x, int y)
    {
        List<BattleFieldSlot> slots = GetBattleSlots(owner);

        if (slots == null)
            return null;

        return slots.FirstOrDefault(slot =>
            slot != null &&
            slot.owner == owner &&
            slot.x == x &&
            slot.y == y);
    }

    private bool TryParseDebugCoord(string coord, out int x, out int y)
    {
        x = 0;
        y = 0;

        if (string.IsNullOrWhiteSpace(coord))
            return false;

        string trimmed = coord.Trim();

        if (trimmed.Length != 2)
            return false;

        if (!char.IsDigit(trimmed[0]) || !char.IsDigit(trimmed[1]))
            return false;

        y = trimmed[0] - '0';
        x = trimmed[1] - '0';

        return x > 0 && y > 0;
    }

    private string FormatCheatOwner(BattleSlotOwner owner)
    {
        return owner == BattleSlotOwner.My ? "me" : "enemy";
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

        if (HandlePendingFieldSlotSelectionClick(slot))
            return;

        string inputFailReason;
        if (IsInputBlocked(out inputFailReason))
        {
            SetSystemMessage(inputFailReason);
            return;
        }

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

    private void SetupRestZoneButtons()
    {
        SetupRestZoneButton(myRestSlot, BattlePlayerSide.My);
        SetupRestZoneButton(enemyRestSlot, BattlePlayerSide.Enemy);
    }

    private void SetupRestZoneButton(Transform restSlot, BattlePlayerSide side)
    {
        if (restSlot == null)
            return;

        Image image = restSlot.GetComponent<Image>();
        if (image == null)
        {
            image = restSlot.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.01f);
        }

        image.raycastTarget = true;

        Button button = restSlot.GetComponent<Button>();
        if (button == null)
            button = restSlot.gameObject.AddComponent<Button>();

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OpenRestZonePanel(side));
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
                leftClickAction: cardToSelect => SelectCard(cardToSelect),
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
        string inputFailReason;
        if (IsInputBlocked(out inputFailReason))
        {
            SetSystemMessage(inputFailReason);
            return;
        }

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
        ClearAllPendingActions();

        currentActionSide = firstPlayerSide;
        consecutivePassCount = 0;
        myActionUsedThisActionTurn = false;
        isEndActionButtonFlow = false;
        enemyHasSummonedFaceDownThisTurn = false;

        if (summonManager != null)
            summonManager.ResetTurnLimitedFlagsForNewTurn();

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

        StartCoroutine(PlayTurnIntroRoutine(turnCount));

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

    private void OnClickSelectPanelCancelButton()
    {
        if (isRestZonePanelOpen)
        {
            CloseRestZonePanel();
            return;
        }

        CancelBroadcastSelection();
    }

    private void CloseBroadcastSelectPanel()
    {
        if (broadcastSelectPanel != null)
            broadcastSelectPanel.SetActive(false);

        if (broadcastSelectContent != null)
            ClearChildren(broadcastSelectContent);
    }

    private void OpenRestZonePanel(BattlePlayerSide side)
    {
        string inputFailReason;
        if (IsInputBlocked(out inputFailReason))
        {
            SetSystemMessage(inputFailReason);
            return;
        }

        BattlePlayerRuntime player = GetPlayer(side);

        if (player == null)
            return;

        GameObject targetPanel = GetRestZonePanel();
        Transform targetContent = GetRestZoneContent();

        if (targetPanel == null || targetContent == null)
        {
            SetSystemMessage("Rest Zone 패널 또는 Content가 연결되어 있지 않습니다.");
            return;
        }

        if (targetPanel == broadcastSelectPanel)
            CloseBroadcastSelectPanel();

        RefreshRestZonePanelContent(side);
        isRestZonePanelOpen = true;
        openRestZoneSide = side;
        targetPanel.SetActive(true);

        SetSystemMessage(
            $"{GetSideName(side)}의 휴식존을 확인합니다.\n" +
            $"{player.restZone.Count}장"
        );
    }

    private void CloseRestZonePanel()
    {
        GameObject targetPanel = GetRestZonePanel();
        Transform targetContent = GetRestZoneContent();

        if (targetPanel != null)
            targetPanel.SetActive(false);

        if (targetContent != null)
            ClearChildren(targetContent);

        isRestZonePanelOpen = false;
    }

    private void RefreshOpenRestZonePanelIfNeeded()
    {
        if (!isRestZonePanelOpen)
            return;

        RefreshRestZonePanelContent(openRestZoneSide);
    }

    private void RefreshRestZonePanelContent(BattlePlayerSide side)
    {
        BattlePlayerRuntime player = GetPlayer(side);
        Transform targetContent = GetRestZoneContent();

        if (player == null || targetContent == null)
            return;

        ClearChildren(targetContent);

        foreach (BaseCardData card in player.restZone)
            CreateRestZoneCardItem(card, targetContent);
    }

    private GameObject GetRestZonePanel()
    {
        return restZonePanel != null ? restZonePanel : broadcastSelectPanel;
    }

    private Transform GetRestZoneContent()
    {
        return restZoneContent != null ? restZoneContent : broadcastSelectContent;
    }

    private void CreateRestZoneCardItem(BaseCardData card, Transform parent)
    {
        GameObject itemObject;

        GameObject prefab = restZoneCardItemPrefab != null
            ? restZoneCardItemPrefab
            : broadcastSelectCardItemPrefab != null
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
                leftClickAction: cardToSelect => SelectCard(cardToSelect),
                rightClickAction: null,
                doubleClickAction: null
            );

            cardItemUI.SetDragActions(false);
            return;
        }

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

    public CardQuestionPanel BattleCardQuestionPanel
    {
        get { return cardQuestionPanel; }
    }

    public bool IsFieldSlotSelectionModeActiveFromExternal
    {
        get { return isFieldSlotSelectionModeActive; }
    }

    public bool RequestFieldSlotSelection(
        string message,
        List<BattleFieldSlot> validSlots,
        Action<BattleFieldSlot> onSelected,
        Action onCancel = null)
    {
        if (onSelected == null)
        {
            SetSystemMessage("슬롯 선택 콜백이 없습니다.");
            return false;
        }

        if (validSlots == null || validSlots.Count == 0)
        {
            SetSystemMessage("선택할 수 있는 위치가 없습니다.");
            return false;
        }

        if (questionPanel != null && questionPanel.IsOpen())
        {
            SetSystemMessage("이미 선택창이 열려 있습니다.");
            return false;
        }

        if (cardQuestionPanel != null && cardQuestionPanel.IsOpen())
        {
            SetSystemMessage("이미 카드 선택창이 열려 있습니다.");
            return false;
        }

        ClearPendingFieldSlotSelection(false);

        foreach (BattleFieldSlot slot in validSlots)
        {
            if (slot == null)
                continue;

            pendingFieldSlotSelectionValidSlots.Add(slot);
            slot.SetQuestionTargetHighlight(true);
        }

        if (pendingFieldSlotSelectionValidSlots.Count == 0)
        {
            SetSystemMessage("선택할 수 있는 위치가 없습니다.");
            return false;
        }

        pendingFieldSlotSelectionSelectedAction = onSelected;
        pendingFieldSlotSelectionCancelAction = onCancel;
        isFieldSlotSelectionModeActive = true;
        SetBattleBusy(true, "FieldSlotSelection");
        SetSystemMessage(string.IsNullOrWhiteSpace(message) ? "출연시킬 위치를 골라주세요." : message);
        return true;
    }

    public void ClearPendingFieldSlotSelectionFromExternal()
    {
        ClearPendingFieldSlotSelection(false);
    }

    public void CancelPendingFieldSlotSelectionFromExternal()
    {
        CancelPendingFieldSlotSelection();
    }

    private void OnPointerClickFieldSlot(BattleFieldSlot slot, PointerEventData eventData)
    {
        if (!isFieldSlotSelectionModeActive)
            return;

        if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
        {
            CancelPendingFieldSlotSelection();
            return;
        }

        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        HandlePendingFieldSlotSelectionClick(slot);
    }

    private bool HandlePendingFieldSlotSelectionClick(BattleFieldSlot slot)
    {
        if (!isFieldSlotSelectionModeActive)
            return false;

        if (slot == null || !pendingFieldSlotSelectionValidSlots.Contains(slot))
        {
            SetSystemMessage("선택할 수 없는 위치입니다.");
            return true;
        }

        Action<BattleFieldSlot> selectedAction = pendingFieldSlotSelectionSelectedAction;
        ClearPendingFieldSlotSelection(false);
        selectedAction?.Invoke(slot);
        return true;
    }

    private void CancelPendingFieldSlotSelection()
    {
        if (!isFieldSlotSelectionModeActive)
            return;

        Action cancelAction = pendingFieldSlotSelectionCancelAction;
        ClearPendingFieldSlotSelection(false);
        SetSystemMessage("위치 선택을 취소했습니다.");
        cancelAction?.Invoke();
    }

    private void ClearPendingFieldSlotSelection(bool invokeCancel)
    {
        bool wasActive = isFieldSlotSelectionModeActive;
        Action cancelAction = pendingFieldSlotSelectionCancelAction;

        foreach (BattleFieldSlot slot in pendingFieldSlotSelectionValidSlots)
        {
            if (slot != null)
                slot.SetQuestionTargetHighlight(false);
        }

        pendingFieldSlotSelectionValidSlots.Clear();
        pendingFieldSlotSelectionSelectedAction = null;
        pendingFieldSlotSelectionCancelAction = null;
        isFieldSlotSelectionModeActive = false;

        if (wasActive)
            SetBattleBusy(false, "ClearPendingFieldSlotSelection");

        if (invokeCancel && wasActive)
            cancelAction?.Invoke();
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

    public IReadOnlyList<BattleFieldSlot> GetEmptyOwnedBroadcastSlotsFromExternal(BattleSlotOwner owner)
    {
        List<BattleFieldSlot> result = new List<BattleFieldSlot>();
        List<BattleFieldSlot> slots = GetBattleSlots(owner);

        if (slots == null)
            return result;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot == null)
                continue;

            if (slot.owner != owner)
                continue;

            if (!slot.HasBroadcast || slot.HasCharacter)
                continue;

            result.Add(slot);
        }

        return result;
    }

    public int GetCurrentTurnCountFromExternal()
    {
        return turnCount;
    }

    public bool IsCharacterCollabEffectSilencedFromExternal(BattleFieldSlot slot)
    {
        return slot != null && slot.IsCollabEffectsSilenced(turnCount);
    }

    public void ApplyCollabEffectSilenceThisTurnFromExternal(BattleFieldSlot slot)
    {
        if (slot == null || !slot.HasCharacter)
            return;

        slot.SetCollabEffectsSilencedUntilTurn(turnCount);
    }

    public void ApplyCollabAttackForbiddenUntilNextTurnFromExternal(BattleFieldSlot targetSlot)
    {
        if (targetSlot == null || !targetSlot.HasCharacter)
            return;

        int untilTurn = turnCount + 1;
        targetSlot.SetCollabAttackForbiddenUntilTurn(untilTurn);

        string cardName = targetSlot.characterCard != null
            ? targetSlot.characterCard.name
            : "대상 캐릭터";
        SetSystemMessageFromExternal($"{cardName}은(는) 다음 턴까지 합방을 시작할 수 없습니다.");
    }

    public bool IsCollabAttackForbiddenFromExternal(BattleFieldSlot slot)
    {
        return slot != null && slot.IsCollabAttackForbidden(turnCount);
    }

    public int CalculateNextOpponentTurnEndLockUntilTurnFromExternal()
    {
        return turnCount + 1;
    }

    public void ApplyBroadcastMoveAndKoLockFromExternal(BattleFieldSlot slot, int untilTurn)
    {
        if (slot == null || !slot.HasBroadcast)
            return;

        slot.SetBroadcastMoveAndKoLockedUntilTurn(untilTurn);
    }

    public bool IsMoveForbiddenByBroadcastMoveAndKoLockFromExternal(
        BattleFieldSlot slot,
        out string failReason)
    {
        failReason = "";

        if (slot == null || !slot.IsBroadcastMoveAndKoLocked(turnCount))
            return false;

        failReason = "효과로 인해 이동할 수 없습니다.";
        return true;
    }

    public bool ShouldPreventCollabKOByBroadcastMoveAndKoLockFromExternal(BattleFieldSlot slot)
    {
        return slot != null && slot.IsBroadcastMoveAndKoLocked(turnCount);
    }

    public bool CanUseMyActionFromExternal(out string failReason)
    {
        return CanUseMyAction(out failReason);
    }

    public bool IsBattleInputLockedFromExternal()
    {
        return IsBattleBusy();
    }

    public bool IsBattleBusyFromExternal()
    {
        return IsBattleBusy();
    }

    public string GetBattleBusyReasonFromExternal()
    {
        return GetBattleBusyReason();
    }

    public Sprite LoadCardSpriteFromExternal(BaseCardData card)
    {
        return LoadCardSprite(card);
    }

    public Sprite GetCardBackSpriteFromExternal()
    {
        return cardBackSprite;
    }

    public void SelectCardFromExternal(BaseCardData card)
    {
        SelectCard(card);
    }

    public void SelectFieldCharacterFromExternal(BattleFieldSlot slot)
    {
        SelectFieldCharacter(slot);
    }

    public void SetSystemMessageFromExternal(string message)
    {
        SetSystemMessage(message);
    }

    public void RefreshAllUIFromExternal()
    {
        RefreshAllUI();
    }

    public void RefreshFieldCharacterDetailFromExternal(BattleFieldSlot slot)
    {
        if (slot == null ||
            !slot.HasCharacter ||
            slot.characterCard == null ||
            cardDetailPanel == null)
        {
            return;
        }

        cardDetailPanel.ShowFieldCharacter(slot);
    }

    public void ResolveMyActionUsedFromExternal(string actionMessage)
    {
        ResolveMyActionUsed(actionMessage);
    }

    public void ResolveCollaborationActionUsedFromExternal(
        BattleSlotOwner actionOwner,
        string actionMessage)
    {
        if (actionOwner == BattleSlotOwner.Enemy)
        {
            ResolveEnemyActionUsed(actionMessage);
            return;
        }

        ResolveMyActionUsed(actionMessage);
    }

    public void AddCardToRestZoneFromExternal(BattleSlotOwner owner, BaseCardData card)
    {
        AddToRestZoneFromExternal(card, owner);
    }

    public void AddToRestZoneFromExternal(BaseCardData card, BattleSlotOwner cardOwner)
    {
        if (card == null)
            return;

        BattlePlayerRuntime targetPlayer =
            cardOwner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.restZone == null)
            return;

        targetPlayer.restZone.Add(card);
    }

    public void AddCharacterToRestZoneFromExternal(BattleSlotOwner owner, BaseCardData card)
    {
        AddToRestZoneFromExternal(card, owner);
    }

    public void AddFieldCharacterToRestZoneFromExternal(BattleFieldSlot slot)
    {
        if (slot == null || !slot.HasCharacter || slot.characterCard == null)
            return;

        AddToRestZoneFromExternal(slot.characterCard, slot.characterOwner);
    }

    public BaseCardData GetIdolCardFromExternal(BattleSlotOwner owner)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        return targetPlayer != null ? targetPlayer.idolCard : null;
    }

    public bool IsCollaborationResolvingFromExternal()
    {
        return collaborationManager != null &&
            collaborationManager.IsResolvingCollaboration;
    }

    public bool ShouldDeferZeroHpDuringCollabFromExternal(BattleFieldSlot slot)
    {
        if (slot == null ||
            effectManager == null ||
            !IsCollaborationResolvingFromExternal())
        {
            return false;
        }

        return effectManager.ShouldDeferZeroHpDuringCollab(slot);
    }

    public bool CanIgnoreAppearTurnActionLimitFromExternal(BattleFieldSlot slot)
    {
        return effectManager != null &&
            effectManager.CanIgnoreAppearTurnActionLimit(slot);
    }

    public bool IsFaceDownSummonForbiddenByBroadcastFromExternal(
        BattleFieldSlot slot,
        out string failReason)
    {
        failReason = "";

        if (HasBroadcastEffectBoolParam(
            slot,
            "broadcast.always.noFaceDownSummonAndDisablePreCollabEffects",
            "forbidFaceDownSummon"))
        {
            string broadcastName = slot != null && slot.broadcastCard != null
                ? slot.broadcastCard.name
                : "이 방송 카드";

            failReason = $"{broadcastName} 위에는 뒷면 표시로 출연할 수 없습니다.";
            return true;
        }

        return false;
    }

    public bool IsPreCollabEffectDisabledByBroadcastFromExternal(
        BattleFieldSlot attackerSlot,
        BattleFieldSlot defenderSlot,
        out string message)
    {
        message = "";

        if (HasBroadcastEffectBoolParam(
                attackerSlot,
                "broadcast.always.noFaceDownSummonAndDisablePreCollabEffects",
                "disablePreCollabEffects") ||
            HasBroadcastEffectBoolParam(
                defenderSlot,
                "broadcast.always.noFaceDownSummonAndDisablePreCollabEffects",
                "disablePreCollabEffects"))
        {
            message = "스텔섭 효과로 합방 전 효과를 사용할 수 없습니다.";
            return true;
        }

        return false;
    }

    public bool IsIdolActiveDisabledByBroadcastFromExternal(BattleSlotOwner owner)
    {
        return HasCharacterOwnedByOnBroadcastEffectBoolParam(
            owner,
            "broadcast.always.disableIdolActiveAndLockMoveOnEnter",
            "disableIdolActiveForOccupantOwner");
    }

    public bool IsCharacterMoveLockedByBroadcastFromExternal(
        BattleFieldSlot slot,
        out string failReason)
    {
        failReason = "";

        if (slot == null ||
            !slot.HasCharacter ||
            slot.movementLockedByBroadcastUntilTurn < 0 ||
            turnCount > slot.movementLockedByBroadcastUntilTurn)
        {
            return false;
        }

        failReason = "공포게임 효과로 이 캐릭터는 이번 턴에 이동할 수 없습니다.";
        return true;
    }

    public void ApplyBroadcastEnterEffectsFromExternal(
        BattleFieldSlot destinationSlot,
        bool enteredByMovement)
    {
        RefreshSlotCharacterBroadcastHpMaxModifierFromExternal(destinationSlot);

        if (!enteredByMovement ||
            destinationSlot == null ||
            !destinationSlot.HasCharacter)
        {
            return;
        }

        if (!HasBroadcastEffectBoolParam(
            destinationSlot,
            "broadcast.always.disableIdolActiveAndLockMoveOnEnter",
            "lockMoveOnEnterUntilNextTurn"))
        {
            return;
        }

        int lockUntilTurn = turnCount + 1;
        destinationSlot.SetMovementLockedByBroadcastUntilTurn(lockUntilTurn);
        Debug.Log(
            $"공포게임 이동 제한 적용: {destinationSlot.characterCard?.name}, " +
            $"owner={destinationSlot.characterOwner}, untilTurn={lockUntilTurn}");
    }

    public void RefreshSlotCharacterBroadcastHpMaxModifierFromExternal(BattleFieldSlot slot)
    {
        if (slot == null || !slot.HasCharacter)
            return;

        int hpMaxDelta = CalculateBroadcastHpMaxDelta(slot);
        slot.ApplyBroadcastHpMaxDelta(hpMaxDelta);
    }

    public int ApplyBroadcastLeaveEffectsFromExternal(BattleFieldSlot fromSlot)
    {
        if (fromSlot == null ||
            !fromSlot.HasCharacter ||
            fromSlot.characterCard == null)
        {
            return 0;
        }

        return ApplyBroadcastLeaveEffects(
            fromSlot,
            fromSlot.characterCard,
            fromSlot.characterOwner);
    }

    private int ApplyBroadcastLeaveEffects(
        BattleFieldSlot fromSlot,
        BaseCardData leavingCharacter,
        BattleSlotOwner characterOwner)
    {
        if (fromSlot == null || leavingCharacter == null)
            return 0;

        return TryApplyGainViewersWhenOccupantLeaves(
            fromSlot,
            leavingCharacter,
            characterOwner);
    }

    private int TryApplyGainViewersWhenOccupantLeaves(
        BattleFieldSlot fromSlot,
        BaseCardData leavingCharacter,
        BattleSlotOwner characterOwner)
    {
        EffectData effect = FindBroadcastEffect(
            fromSlot,
            "broadcast.always.gainViewersWhenOccupantLeaves");

        if (effect == null)
            return 0;

        int amount = GetEffectIntParamForBattleManager(effect, "amount", 0);

        if (amount == 0)
            return 0;

        int actualDelta = ModifyViewersFromExternal(characterOwner, amount);

        if (actualDelta == 0)
            return 0;

        string ownerName = characterOwner == BattleSlotOwner.My ? "내" : "상대";
        string message =
            $"{fromSlot.broadcastCard.name} 효과: {leavingCharacter.name}이 슬롯을 벗어나 {ownerName} 시청자 +{actualDelta}";

        Debug.Log(message);
        RefreshAllUI();
        TryResolveVictory(message);

        return actualDelta;
    }

    public int ApplyCharacterDamageFromExternal(
        BattleFieldSlot slot,
        int damage,
        bool resolveZeroHp = true)
    {
        if (slot == null || !slot.HasCharacter)
            return 0;

        int safeDamage = Mathf.Max(0, damage);

        if (safeDamage <= 0)
            return 0;

        int beforeHp = slot.currentCharacterHp;
        slot.ApplyCharacterDamage(safeDamage);

        if (resolveZeroHp && GetEffectiveCharacterHpFromExternal(slot, slot) <= 0)
            StartCoroutine(ResolveZeroHpCharacterRoutineFromExternal(slot));

        return beforeHp - slot.currentCharacterHp;
    }

    public void RequestResolveZeroHpCharacterFromExternal(
        BattleFieldSlot slot,
        BattleFieldSlot effectLocationSlot = null)
    {
        StartCoroutine(ResolveZeroHpCharacterRoutineFromExternal(slot, effectLocationSlot));
    }

    public IEnumerator ResolveZeroHpCharacterRoutineFromExternal(BattleFieldSlot slot)
    {
        yield return ResolveZeroHpCharacterRoutineFromExternal(slot, slot);
    }

    public IEnumerator ResolveZeroHpCharacterRoutineFromExternal(
        BattleFieldSlot slot,
        BattleFieldSlot effectLocationSlot)
    {
        if (slot == null ||
            !slot.HasCharacter ||
            slot.characterCard == null)
        {
            yield break;
        }

        if (GetEffectiveCharacterHpFromExternal(slot, effectLocationSlot) > 0)
            yield break;

        if (ShouldDeferZeroHpDuringCollabFromExternal(slot))
            yield break;

        if (slot.currentCharacterHp > 0)
            slot.SetCharacterBattleStats(0, slot.currentCharacterTension);

        yield return SendFieldCharacterToRestZoneRoutine(slot);
    }

    public IEnumerator SendFieldCharacterToRestZoneRoutine(BattleFieldSlot slot)
    {
        if (slot == null ||
            !slot.HasCharacter ||
            slot.characterCard == null ||
            resolvingRestSlots.Contains(slot))
        {
            yield break;
        }

        resolvingRestSlots.Add(slot);
        bool wasBusy = isBusy;
        string previousBusyReason = battleBusyReason;
        SetBattleBusy(true, $"SendFieldCharacterToRestZoneRoutine:{slot.characterCard.id}");

        BaseCardData restedCard = slot.characterCard;
        BattleSlotOwner owner = slot.characterOwner;

        yield return AnimateCharacterExitToRestZoneRoutine(slot);

        ApplyBroadcastLeaveEffectsFromExternal(slot);
        AddFieldCharacterToRestZoneFromExternal(slot);
        slot.ClearCharacterCard();
        RefreshAllUI();

        bool effectComplete = false;
        RequestOnRestEffectsFromExternal(
            slot,
            restedCard,
            owner,
            () => effectComplete = true
        );

        float waitStartedAt = Time.realtimeSinceStartup;
        const float effectWaitTimeout = 30f;

        while (!effectComplete)
        {
            if (Time.realtimeSinceStartup - waitStartedAt > effectWaitTimeout)
            {
                Debug.LogWarning($"OnRest 효과 처리 대기 시간이 초과되었습니다: {restedCard.id} / {restedCard.name}");
                break;
            }

            yield return null;
        }

        resolvingRestSlots.Remove(slot);
        if (wasBusy)
            SetBattleBusy(true, previousBusyReason);
        else
            SetBattleBusy(false, "SendFieldCharacterToRestZoneRoutine finished");
    }

    private IEnumerator AnimateCharacterExitToRestZoneRoutine(BattleFieldSlot slot)
    {
        if (slot == null || slot.characterCardImage == null)
            yield break;

        Image image = slot.characterCardImage;
        RectTransform rect = image.rectTransform;
        CanvasGroup canvasGroup = rect.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = rect.gameObject.AddComponent<CanvasGroup>();

        Color originalColor = image.color;
        Vector3 originalScale = rect.localScale;
        float originalAlpha = canvasGroup.alpha;
        float duration = 0.35f;
        float elapsed = 0f;

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            canvasGroup.alpha = Mathf.Lerp(originalAlpha, 0f, easedT);
            rect.localScale = Vector3.Lerp(originalScale, originalScale * 0.92f, easedT);
            yield return null;
        }

        canvasGroup.alpha = originalAlpha;
        rect.localScale = originalScale;
        image.color = originalColor;
    }

    public bool IsCardInHandFromExternal(BattleSlotOwner owner, BaseCardData card)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.hand == null || card == null)
            return false;

        return targetPlayer.hand.Contains(card);
    }

    public IReadOnlyList<BaseCardData> GetHandCardsFromExternal(BattleSlotOwner owner)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        return targetPlayer != null
            ? targetPlayer.hand
            : null;
    }

    public int FindHandCardIndexFromExternal(BattleSlotOwner owner, BaseCardData card)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.hand == null || card == null)
            return -1;

        return targetPlayer.hand.IndexOf(card);
    }

    public bool IsCardInHandAtIndexFromExternal(
        BattleSlotOwner owner,
        int handIndex,
        BaseCardData card)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.hand == null || card == null)
            return false;

        if (handIndex < 0 || handIndex >= targetPlayer.hand.Count)
            return false;

        return targetPlayer.hand[handIndex] == card;
    }

    public bool RemoveCardFromHandFromExternal(BattleSlotOwner owner, BaseCardData card)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.hand == null || card == null)
            return false;

        int removedIndex = targetPlayer.hand.IndexOf(card);
        bool removed = targetPlayer.hand.Remove(card);

        if (removed && owner == BattleSlotOwner.My)
        {
            if (removedIndex == selectedHandCardIndex)
                ClearSelectedHandCard();
            else if (removedIndex >= 0 && removedIndex < selectedHandCardIndex)
                selectedHandCardIndex--;
        }

        return removed;
    }

    public bool RemoveHandCardAtIndexFromExternal(
        BattleSlotOwner owner,
        int handIndex,
        BaseCardData card)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.hand == null || card == null)
            return false;

        if (handIndex < 0 || handIndex >= targetPlayer.hand.Count)
            return false;

        if (targetPlayer.hand[handIndex] != card)
            return false;

        targetPlayer.hand.RemoveAt(handIndex);

        if (owner == BattleSlotOwner.My)
        {
            if (handIndex == selectedHandCardIndex)
                ClearSelectedHandCard();
            else if (handIndex < selectedHandCardIndex)
                selectedHandCardIndex--;
        }

        return true;
    }

    public bool IsCardInRestZoneFromExternal(BattleSlotOwner owner, BaseCardData card)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.restZone == null || card == null)
            return false;

        return targetPlayer.restZone.Contains(card);
    }

    public IReadOnlyList<BaseCardData> GetRestZoneCardsFromExternal(BattleSlotOwner owner)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.restZone == null)
            return Array.Empty<BaseCardData>();

        return new List<BaseCardData>(targetPlayer.restZone);
    }

    public bool RemoveCardFromRestZoneFromExternal(BattleSlotOwner owner, BaseCardData card)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.restZone == null || card == null)
            return false;

        return targetPlayer.restZone.Remove(card);
    }

    public void AddCardToMainDeckFromExternal(
        BattleSlotOwner owner,
        BaseCardData card,
        DeckInsertPosition insertPosition,
        bool shuffleAfterMove)
    {
        if (card == null)
            return;

        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.mainDeck == null)
            return;

        switch (insertPosition)
        {
            case DeckInsertPosition.Top:
                targetPlayer.mainDeck.Insert(0, card);
                break;
            case DeckInsertPosition.Shuffle:
            case DeckInsertPosition.Bottom:
            default:
                targetPlayer.mainDeck.Add(card);
                break;
        }

        if (shuffleAfterMove || insertPosition == DeckInsertPosition.Shuffle)
            Shuffle(targetPlayer.mainDeck);
    }

    public IReadOnlyList<BaseCardData> PeekTopMainDeckCardsFromExternal(
        BattleSlotOwner owner,
        int count)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.mainDeck == null)
            return Array.Empty<BaseCardData>();

        int safeCount = Mathf.Clamp(count, 0, targetPlayer.mainDeck.Count);
        List<BaseCardData> cards = new List<BaseCardData>();

        for (int i = 0; i < safeCount; i++)
            cards.Add(targetPlayer.mainDeck[i]);

        return cards;
    }

    public IReadOnlyList<BaseCardData> GetMainDeckCardsFromExternal(BattleSlotOwner owner)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.mainDeck == null)
            return Array.Empty<BaseCardData>();

        return new List<BaseCardData>(targetPlayer.mainDeck);
    }

    public bool RemoveCardFromMainDeckFromExternal(BattleSlotOwner owner, BaseCardData card)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.mainDeck == null || card == null)
            return false;

        return targetPlayer.mainDeck.Remove(card);
    }

    public void AddCardToHandFromExternal(BattleSlotOwner owner, BaseCardData card)
    {
        if (card == null)
            return;

        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.hand == null)
            return;

        targetPlayer.hand.Add(card);
    }

    public void MoveMainDeckCardsToBottomFromExternal(
        BattleSlotOwner owner,
        IReadOnlyList<BaseCardData> cards,
        bool reverseOrder)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.mainDeck == null || cards == null)
            return;

        List<BaseCardData> orderedCards = new List<BaseCardData>(cards);

        if (reverseOrder)
            orderedCards.Reverse();

        foreach (BaseCardData card in orderedCards)
        {
            if (card == null)
                continue;

            if (targetPlayer.mainDeck.Remove(card))
                targetPlayer.mainDeck.Add(card);
        }
    }

    public void ShuffleMainDeckFromExternal(BattleSlotOwner owner)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.mainDeck == null)
            return;

        Shuffle(targetPlayer.mainDeck);
    }

    public bool CanPayViewerCostFromExternal(BattleSlotOwner owner, int cost)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        return CanPayViewerCost(targetPlayer, Mathf.Max(0, cost));
    }

    public bool TryPayViewerCostFromExternal(BattleSlotOwner owner, int cost)
    {
        int safeCost = Mathf.Max(0, cost);

        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (!CanPayViewerCost(targetPlayer, safeCost))
            return false;

        targetPlayer.viewers -= safeCost;
        return true;
    }

    public int ModifyViewersFromExternal(BattleSlotOwner owner, int delta)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null)
            return 0;

        int before = targetPlayer.viewers;
        targetPlayer.viewers = Mathf.Max(0, targetPlayer.viewers + delta);

        return targetPlayer.viewers - before;
    }

    public int GetViewersFromExternal(BattleSlotOwner owner)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        return targetPlayer != null ? targetPlayer.viewers : 0;
    }

    public int DrawCardsFromExternal(BattleSlotOwner owner, int count)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null)
            return 0;

        int before = targetPlayer.hand != null ? targetPlayer.hand.Count : 0;
        DrawCards(targetPlayer, Mathf.Max(0, count));
        int after = targetPlayer.hand != null ? targetPlayer.hand.Count : before;

        return Mathf.Max(0, after - before);
    }

    public void DrawCardsWithAnimationFromExternal(
        BattleSlotOwner owner,
        int count,
        Action<int> onComplete)
    {
        StartCoroutine(DrawCardsWithAnimationRoutine(owner, Mathf.Max(0, count), onComplete));
    }

    public int HealCharacterFromExternal(BattleFieldSlot slot, int amount)
    {
        if (slot == null || !slot.HasCharacter || slot.characterCard == null)
            return 0;

        int maxHp = Mathf.Max(1, slot.currentCharacterMaxHp);
        int beforeHp = slot.currentCharacterHp;
        int healAmount = Mathf.Max(0, amount);

        if (healAmount > 0)
            healAmount += CalculateBroadcastHealBonus(slot);

        int afterHp = Mathf.Min(maxHp, beforeHp + healAmount);

        slot.SetCharacterBattleStats(afterHp, slot.currentCharacterTension);

        return afterHp - beforeHp;
    }

    public int FullHealCharacterFromExternal(BattleFieldSlot slot)
    {
        if (slot == null || !slot.HasCharacter || slot.characterCard == null)
            return 0;

        return HealCharacterFromExternal(slot, Mathf.Max(1, slot.currentCharacterMaxHp));
    }

    public bool MoveCardFromHandToRestZoneFromExternal(BattleSlotOwner owner, BaseCardData card)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.hand == null || targetPlayer.restZone == null)
            return false;

        int removedIndex = targetPlayer.hand.IndexOf(card);

        if (card == null || removedIndex < 0)
            return false;

        targetPlayer.hand.RemoveAt(removedIndex);
        targetPlayer.restZone.Add(card);

        if (owner == BattleSlotOwner.My)
        {
            if (removedIndex == selectedHandCardIndex)
                ClearSelectedHandCard();
            else if (removedIndex < selectedHandCardIndex)
                selectedHandCardIndex--;
        }

        return true;
    }

    public bool MoveHandCardAtIndexToRestZoneFromExternal(
        BattleSlotOwner owner,
        int handIndex,
        BaseCardData card)
    {
        BattlePlayerRuntime targetPlayer =
            owner == BattleSlotOwner.My
                ? myPlayer
                : enemyPlayer;

        if (targetPlayer == null || targetPlayer.hand == null || targetPlayer.restZone == null)
            return false;

        if (card == null || handIndex < 0 || handIndex >= targetPlayer.hand.Count)
            return false;

        if (targetPlayer.hand[handIndex] != card)
            return false;

        targetPlayer.hand.RemoveAt(handIndex);
        targetPlayer.restZone.Add(card);

        if (owner == BattleSlotOwner.My)
        {
            if (handIndex == selectedHandCardIndex)
                ClearSelectedHandCard();
            else if (handIndex < selectedHandCardIndex)
                selectedHandCardIndex--;
        }

        return true;
    }

    public void RemoveAllLastingContentsOnBoardFromExternal(
        BattleSlotOwner effectOwner,
        out int removedCount)
    {
        removedCount = 0;

        removedCount += RemoveLastingContentsFromSlots(myBattleSlots);
        removedCount += RemoveLastingContentsFromSlots(enemyBattleSlots);

        Debug.Log($"빙하기 테스트 효과: 장기 콘텐츠 {removedCount}장을 제거했습니다.");
    }

    public int GetSlotCharacterTensionModifierFromExternal(BattleFieldSlot slot)
    {
        return GetSlotCharacterTensionModifierFromExternal(slot, slot);
    }

    public int GetSlotCharacterTensionModifierFromExternal(
        BattleFieldSlot slot,
        BattleFieldSlot effectLocationSlot)
    {
        if (slot == null ||
            !slot.HasCharacter ||
            slot.characterCard == null)
        {
            return 0;
        }

        int modifier = 0;
        BattleFieldSlot locationSlot = effectLocationSlot != null ? effectLocationSlot : slot;
        modifier += GetBroadcastCharacterTensionModifier(locationSlot, slot.characterCard);
        modifier += GetInstalledContentCharacterTensionModifier(slot, locationSlot);

        return modifier;
    }

    public int GetEffectiveCollabTensionFromExternal(
        BattleFieldSlot participantSlot,
        BattleFieldSlot battleLocationSlot)
    {
        if (participantSlot == null ||
            !participantSlot.HasCharacter ||
            participantSlot.characterCard == null)
        {
            return 0;
        }

        BattleFieldSlot locationSlot = battleLocationSlot != null ? battleLocationSlot : participantSlot;
        int tension = participantSlot.currentCharacterTension;
        tension += GetSlotCharacterTensionModifierFromExternal(participantSlot, locationSlot);
        tension += CalculateAdjacentCollabTensionDeltaForTag(participantSlot);

        if (effectManager != null)
        {
            EffectContext context = new EffectContext
            {
                battleManager = this,
                collaborationManager = collaborationManager,
                timing = EffectTiming.Passive,
                attackerOriginalSlot = collaborationManager != null && collaborationManager.CurrentCollaborationContext != null
                    ? collaborationManager.CurrentCollaborationContext.attackerOriginalSlot
                    : participantSlot,
                attackerSlot = collaborationManager != null && collaborationManager.CurrentCollaborationContext != null
                    ? collaborationManager.CurrentCollaborationContext.attackerSlot
                    : participantSlot,
                defenderSlot = locationSlot,
                battleLocationSlot = locationSlot,
                sourceSlot = participantSlot,
                sourceCard = participantSlot.characterCard,
                actingOwner = participantSlot.characterOwner,
                consumeAction = false
            };

            tension += effectManager.GetIdolPassiveCollabTensionModifier(participantSlot, context);
        }

        return Mathf.Max(0, tension);
    }

    private int CalculateAdjacentCollabTensionDeltaForTag(BattleFieldSlot participantSlot)
    {
        if (participantSlot == null ||
            !participantSlot.HasCharacter ||
            participantSlot.characterCard == null ||
            participantSlot.isCharacterFaceDown)
        {
            return 0;
        }

        int totalDelta = 0;
        AddAdjacentCollabTensionDeltaForTagFromSlots(participantSlot, myBattleSlots, ref totalDelta);
        AddAdjacentCollabTensionDeltaForTagFromSlots(participantSlot, enemyBattleSlots, ref totalDelta);
        return totalDelta;
    }

    private void AddAdjacentCollabTensionDeltaForTagFromSlots(
        BattleFieldSlot participantSlot,
        IEnumerable<BattleFieldSlot> sourceSlots,
        ref int totalDelta)
    {
        if (participantSlot == null || sourceSlots == null)
            return;

        foreach (BattleFieldSlot sourceSlot in sourceSlots)
        {
            if (sourceSlot == null ||
                sourceSlot == participantSlot ||
                !sourceSlot.HasCharacter ||
                sourceSlot.characterCard == null ||
                sourceSlot.isCharacterFaceDown ||
                sourceSlot.currentCharacterHp <= 0)
            {
                continue;
            }

            if (!AreSlotsOrthogonallyAdjacentOnSameField(sourceSlot, participantSlot))
                continue;

            CharacterCardData sourceCharacter = sourceSlot.characterCard as CharacterCardData;
            if (sourceCharacter == null || sourceCharacter.effects == null)
                continue;

            foreach (EffectData effect in sourceCharacter.effects)
            {
                string effectRef = GetEffectRefForBattleManager(effect);
                if (!string.Equals(
                        effectRef,
                        "character.passive.adjacentCollabTensionDeltaForTag",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string tag = GetEffectStringParamForBattleManager(effect, "tag", "");
                if (string.IsNullOrWhiteSpace(tag) ||
                    !CardHasHashtag(participantSlot.characterCard, tag))
                {
                    continue;
                }

                totalDelta += GetEffectIntParamForBattleManager(effect, "amount", 0);
            }
        }
    }

    public int GetSlotCharacterHpModifierFromExternal(BattleFieldSlot slot)
    {
        return GetSlotCharacterHpModifierFromExternal(slot, slot);
    }

    public int GetSlotCharacterHpModifierFromExternal(
        BattleFieldSlot slot,
        BattleFieldSlot effectLocationSlot)
    {
        if (slot == null ||
            !slot.HasCharacter ||
            slot.characterCard == null)
        {
            return 0;
        }

        int modifier = 0;
        BattleFieldSlot locationSlot = effectLocationSlot != null ? effectLocationSlot : slot;
        modifier += GetBroadcastCharacterHpModifier(locationSlot, slot.characterCard);
        modifier += GetInstalledContentCharacterHpModifier(slot, locationSlot);

        return modifier;
    }

    public int GetEffectiveCharacterHpFromExternal(
        BattleFieldSlot slot,
        BattleFieldSlot effectLocationSlot = null)
    {
        if (slot == null ||
            !slot.HasCharacter ||
            slot.characterCard == null ||
            slot.currentCharacterHp <= 0)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            slot.currentCharacterHp + GetSlotCharacterHpModifierFromExternal(slot, effectLocationSlot)
        );
    }

    private int GetBroadcastCharacterTensionModifier(
        BattleFieldSlot slot,
        BaseCardData participantCard)
    {
        return 0;
    }

    private int GetBroadcastCharacterHpModifier(
        BattleFieldSlot slot,
        BaseCardData participantCard)
    {
        return 0;
    }

    private int CalculateBroadcastHpMaxDelta(BattleFieldSlot slot)
    {
        EffectData effect = FindBroadcastEffect(
            slot,
            "broadcast.always.prepViewersAndOccupantHpDelta");

        return GetEffectIntParamForBattleManager(effect, "hpMaxDelta", 0);
    }

    private int CalculateBroadcastHealBonus(BattleFieldSlot slot)
    {
        EffectData effect = FindBroadcastEffect(
            slot,
            "broadcast.always.prepViewersAndHealBonus");

        return Mathf.Max(0, GetEffectIntParamForBattleManager(effect, "healBonus", 0));
    }

    private int GetInstalledContentCharacterTensionModifier(BattleFieldSlot slot)
    {
        return GetInstalledContentCharacterStatModifier(slot, slot, "tension");
    }

    private int GetInstalledContentCharacterHpModifier(BattleFieldSlot slot)
    {
        return GetInstalledContentCharacterStatModifier(slot, slot, "hp");
    }

    private int GetInstalledContentCharacterTensionModifier(
        BattleFieldSlot participantSlot,
        BattleFieldSlot effectLocationSlot)
    {
        return GetInstalledContentCharacterStatModifier(participantSlot, effectLocationSlot, "tension");
    }

    private int GetInstalledContentCharacterHpModifier(
        BattleFieldSlot participantSlot,
        BattleFieldSlot effectLocationSlot)
    {
        return GetInstalledContentCharacterStatModifier(participantSlot, effectLocationSlot, "hp");
    }

    private int GetInstalledContentCharacterStatModifier(
        BattleFieldSlot participantSlot,
        BattleFieldSlot effectLocationSlot,
        string statKey)
    {
        if (participantSlot == null ||
            participantSlot.characterCard == null ||
            effectLocationSlot == null ||
            effectLocationSlot.contentCard == null)
        {
            return 0;
        }

        ContentCardData content = effectLocationSlot.contentCard as ContentCardData;

        if (content == null || content.effects == null)
            return 0;

        int modifier = 0;

        foreach (EffectData effect in content.effects)
        {
            if (effect == null ||
                !string.Equals(GetEffectRefForBattleManager(effect), "content.lasting.buffTagTensionAndHp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string tag = GetEffectStringParamForBattleManager(effect, "tag", "");

            if (!MatchesEffectTagOrSharedHashtag(effectLocationSlot.contentCard, participantSlot.characterCard, tag))
                continue;

            modifier += GetEffectIntParamForBattleManager(effect, statKey, 0);
        }

        return modifier;
    }

    [Obsolete("Use GetSlotCharacterTensionModifierFromExternal instead.")]
    public int GetLastingContentTensionBonusFromExternal(BattleFieldSlot slot)
    {
        return GetInstalledContentCharacterTensionModifier(slot);
    }

    [Obsolete("Use GetSlotCharacterHpModifierFromExternal instead.")]
    public int GetLastingContentHpBonusFromExternal(BattleFieldSlot slot)
    {
        return GetInstalledContentCharacterHpModifier(slot);
    }

    private bool MatchesEffectTagOrSharedHashtag(
        BaseCardData sourceCard,
        BaseCardData targetCard,
        string tag)
    {
        if (!string.IsNullOrEmpty(tag))
            return CardHasHashtag(targetCard, tag);

        if (sourceCard == null ||
            sourceCard.hashtags == null ||
            targetCard == null ||
            targetCard.hashtags == null)
        {
            return false;
        }

        foreach (string sourceTag in sourceCard.hashtags)
        {
            if (CardHasHashtag(targetCard, sourceTag))
                return true;
        }

        return false;
    }

    private string GetEffectRefForBattleManager(EffectData effect)
    {
        if (effect == null)
            return "";

        if (!string.IsNullOrWhiteSpace(effect.@ref))
            return effect.@ref;

        return !string.IsNullOrWhiteSpace(effect.refName)
            ? effect.refName
            : "";
    }

    private bool HasBroadcastEffectBoolParam(
        BattleFieldSlot slot,
        string effectRef,
        string paramKey)
    {
        EffectData effect = FindBroadcastEffect(slot, effectRef);
        return GetEffectBoolParamForBattleManager(effect, paramKey, false);
    }

    private bool HasCharacterOwnedByOnBroadcastEffectBoolParam(
        BattleSlotOwner characterOwner,
        string effectRef,
        string paramKey)
    {
        return HasCharacterOwnedByOnBroadcastEffectBoolParam(myBattleSlots, characterOwner, effectRef, paramKey) ||
            HasCharacterOwnedByOnBroadcastEffectBoolParam(enemyBattleSlots, characterOwner, effectRef, paramKey);
    }

    private bool HasCharacterOwnedByOnBroadcastEffectBoolParam(
        List<BattleFieldSlot> slots,
        BattleSlotOwner characterOwner,
        string effectRef,
        string paramKey)
    {
        if (slots == null)
            return false;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot == null ||
                !slot.HasCharacter ||
                slot.characterOwner != characterOwner)
            {
                continue;
            }

            if (HasBroadcastEffectBoolParam(slot, effectRef, paramKey))
                return true;
        }

        return false;
    }

    private EffectData FindBroadcastEffect(BattleFieldSlot slot, string effectRef)
    {
        if (slot == null ||
            slot.broadcastCard == null ||
            string.IsNullOrWhiteSpace(effectRef))
        {
            return null;
        }

        BroadcastCardData broadcast = slot.broadcastCard as BroadcastCardData;

        if (broadcast == null || broadcast.effects == null)
            return null;

        foreach (EffectData effect in broadcast.effects)
        {
            if (string.Equals(
                GetEffectRefForBattleManager(effect),
                effectRef,
                StringComparison.OrdinalIgnoreCase))
            {
                return effect;
            }
        }

        return null;
    }

    private bool CardHasHashtag(BaseCardData card, string tag)
    {
        if (card == null || card.hashtags == null || string.IsNullOrEmpty(tag))
            return false;

        string normalizedTarget = NormalizeTagForComparison(tag);

        foreach (string hashtag in card.hashtags)
        {
            if (string.Equals(NormalizeTagForComparison(hashtag), normalizedTarget, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool GetEffectBoolParamForBattleManager(
        EffectData effect,
        string key,
        bool defaultValue)
    {
        EffectParams effectParams = effect != null ? effect.@params : null;

        if (effectParams == null || string.IsNullOrEmpty(key))
            return defaultValue;

        switch (key)
        {
            case "forbidFaceDownSummon":
                return effectParams.forbidFaceDownSummon;
            case "disablePreCollabEffects":
                return effectParams.disablePreCollabEffects;
            case "disableIdolActiveForOccupantOwner":
                return effectParams.disableIdolActiveForOccupantOwner;
            case "lockMoveOnEnterUntilNextTurn":
                return effectParams.lockMoveOnEnterUntilNextTurn;
            default:
                return defaultValue;
        }
    }

    private string NormalizeTagForComparison(string tag)
    {
        return string.IsNullOrEmpty(tag)
            ? ""
            : tag.Trim();
    }

    private int GetEffectIntParamForBattleManager(
        EffectData effect,
        string key,
        int defaultValue)
    {
        EffectParams effectParams = effect != null ? effect.@params : null;

        if (effectParams == null || string.IsNullOrEmpty(key))
            return defaultValue;

        switch (key)
        {
            case "amount":
                return effectParams.amount;
            case "viewersModifier":
                return effectParams.viewersModifier;
            case "hpMaxDelta":
                return effectParams.hpMaxDelta;
            case "healBonus":
                return effectParams.healBonus;
            case "tension":
                return effectParams.tension;
            case "hp":
                return effectParams.hp;
            default:
                return defaultValue;
        }
    }

    private string GetEffectStringParamForBattleManager(
        EffectData effect,
        string key,
        string defaultValue)
    {
        EffectParams effectParams = effect != null ? effect.@params : null;

        if (effectParams == null || string.IsNullOrEmpty(key))
            return defaultValue;

        switch (key)
        {
            case "tag":
                return !string.IsNullOrEmpty(effectParams.tag)
                    ? effectParams.tag
                    : defaultValue;
            default:
                return defaultValue;
        }
    }

    public bool TryStartSilenceCharacterCollabThisTurnFromExternal(
        BaseCardData sourceCard,
        BattleSlotOwner owner,
        int cost,
        bool consumeAction,
        int sourceHandIndex = -1)
    {
        if (sourceCard == null)
        {
            SetSystemMessage("발동할 콘텐츠 카드 정보가 없습니다.");
            return false;
        }

        if (owner != BattleSlotOwner.My)
        {
            SetSystemMessage("현재는 내 콘텐츠 카드만 테스트 발동할 수 있습니다.");
            return false;
        }

        if (!IsCardInHandFromExternal(owner, sourceCard))
        {
            SetSystemMessage("손패에 있는 콘텐츠 카드만 발동할 수 있습니다.");
            return false;
        }

        if (!CanPayViewerCostFromExternal(owner, cost))
        {
            SetSystemMessage("시청자가 부족하여 효과를 발동할 수 없습니다.");
            return false;
        }

        List<CardQuestionOption> candidates = GetFaceUpCharacterOptionsOnBoard();

        if (candidates.Count == 0)
        {
            SetSystemMessage("채팅 밴을 적용할 앞면 캐릭터가 없습니다.");
            return false;
        }

        if (cardQuestionPanel == null)
        {
            SetSystemMessage("CardQuestionPanel이 BattleManager에 연결되어 있지 않습니다.");
            return false;
        }

        if (!cardQuestionPanel.TryShowOptions(
            "채팅 밴 대상을 선택하세요.",
            candidates,
            false,
            selectedOption => ConfirmSilenceCharacterCollabThisTurn(sourceCard, owner, cost, consumeAction, sourceHandIndex, selectedOption),
            null
        ))
        {
            SetSystemMessage("이미 카드 선택창이 열려 있습니다.");
            return false;
        }

        SetSystemMessage("채팅 밴 대상을 선택하세요.");
        return true;
    }

    public bool TryOpenPreCollabContentQuestionFromExternal(Action onContinueCollaboration)
    {
        List<BaseCardData> candidates = GetPreCollabContentCardsInMyHand();

        if (candidates.Count == 0)
        {
            Debug.Log("PreCollab 테스트: 발동 가능한 합방 전 콘텐츠 카드가 없습니다.");
            return false;
        }

        Debug.Log($"PreCollab 테스트: 합방 전 콘텐츠 후보 {candidates.Count}장 감지");

        if (cardQuestionPanel == null)
        {
            Debug.LogWarning("PreCollab 테스트: CardQuestionPanel이 연결되어 있지 않아 후보 로그만 출력합니다.");
            SetSystemMessage("합방 전 콘텐츠 후보를 감지했지만 CardQuestionPanel이 연결되어 있지 않습니다.");
            return false;
        }

        if (cardQuestionPanel.IsOpen())
        {
            SetSystemMessage("이미 카드 선택창이 열려 있습니다.");
            return false;
        }

        bool opened = cardQuestionPanel.TryShow(
            "합방 전에 발동할 카드를 선택하세요.",
            candidates,
            true,
            selectedCard => ConfirmPreCollabContentTest(selectedCard, onContinueCollaboration),
            () => CancelPreCollabContentTest(onContinueCollaboration)
        );

        if (opened)
            SetSystemMessage("합방 전에 발동할 콘텐츠 카드를 선택하세요.");

        return opened;
    }

    private bool CanUseMyAction(out string failReason)
    {
        failReason = "";

        if (IsGameOver())
        {
            failReason = "이미 배틀이 종료되었습니다.";
            LogActionBlocked("CanUseMyAction", failReason);
            return false;
        }

        if (IsBattleBusy())
        {
            failReason = GetBattleBusyReason();
            LogActionBlocked("CanUseMyAction", failReason);
            return false;
        }

        if (questionPanel != null && questionPanel.IsOpen())
        {
            failReason = "이미 다른 선택창이 열려 있습니다.";
            LogActionBlocked("CanUseMyAction", failReason);
            return false;
        }

        if (cardQuestionPanel != null && cardQuestionPanel.IsOpen())
        {
            failReason = "이미 카드 선택창이 열려 있습니다.";
            LogActionBlocked("CanUseMyAction", failReason);
            return false;
        }

        if (movementManager != null && movementManager.HasPendingMoveChoice)
        {
            failReason = "이동 선택을 처리 중입니다.";
            LogActionBlocked("CanUseMyAction", failReason);
            return false;
        }

        if (collaborationManager != null && collaborationManager.HasPendingCollaborationChoice)
        {
            failReason = "합방 선택을 처리 중입니다.";
            LogActionBlocked("CanUseMyAction", failReason);
            return false;
        }

        if (currentPhase != BattlePhase.MainGame)
        {
            failReason = "아직 본게임 단계가 아닙니다.";
            LogActionBlocked("CanUseMyAction", failReason);
            return false;
        }

        if (currentActionSide != BattlePlayerSide.My)
        {
            failReason = "현재는 내 행동권이 아닙니다.";
            LogActionBlocked("CanUseMyAction", failReason);
            return false;
        }

        if (myActionUsedThisActionTurn)
        {
            failReason = "이미 행동권을 사용했습니다.";
            LogActionBlocked("CanUseMyAction", failReason);
            return false;
        }

        return true;
    }

    private bool IsGameOver()
    {
        return isGameOver || isBattleEnded;
    }

    private bool IsBattleBusy()
    {
        RecoverExpiredBattleBusyIfNeeded();

        if (isBusy)
            return true;

        return collaborationManager != null &&
            collaborationManager.IsCollaborationSequenceRunning;
    }

    private void RecoverExpiredBattleBusyIfNeeded()
    {
        if (!isBusy || string.IsNullOrEmpty(battleBusyReason))
            return;

        float elapsed = battleBusyStartedRealtime >= 0f
            ? Time.realtimeSinceStartup - battleBusyStartedRealtime
            : 0f;

        if (battleBusyReason.StartsWith("TransferActionSideRoutine", StringComparison.Ordinal) &&
            elapsed > 5f)
        {
            Debug.LogWarning($"[BattleBusy Watchdog] action transfer busy expired. reason={battleBusyReason} elapsed={elapsed:F2}");
            SetBattleBusy(false, "TransferActionSideRoutine watchdog");
            return;
        }

        if (battleBusyReason.StartsWith("EndCurrentTurnAndStartNextTurnRoutine", StringComparison.Ordinal) &&
            elapsed > 20f)
        {
            Debug.LogWarning($"[BattleBusy Watchdog] turn start busy expired. reason={battleBusyReason} elapsed={elapsed:F2}");
            SetBattleBusy(false, "TurnStart watchdog");
        }
    }

    private bool IsInputBlocked(out string failReason)
    {
        failReason = "";

        if (IsGameOver())
        {
            failReason = "이미 배틀이 종료되었습니다.";
            LogActionBlocked("IsInputBlocked", failReason);
            return true;
        }

        if (IsBattleBusy())
        {
            failReason = GetBattleBusyReason();
            LogActionBlocked("IsInputBlocked", failReason);
            return true;
        }

        if (questionPanel != null && questionPanel.IsOpen())
        {
            failReason = "이미 다른 선택창이 열려 있습니다.";
            LogActionBlocked("IsInputBlocked", failReason);
            return true;
        }

        if (cardQuestionPanel != null && cardQuestionPanel.IsOpen())
        {
            failReason = "이미 카드 선택창이 열려 있습니다.";
            LogActionBlocked("IsInputBlocked", failReason);
            return true;
        }

        return false;
    }

    private string GetBattleBusyReason()
    {
        if (collaborationManager != null &&
            collaborationManager.IsCollaborationSequenceRunning)
        {
            return "합방 처리를 진행 중입니다.";
        }

        if (resolvingRestSlots.Count > 0)
            return "퇴장 효과를 처리 중입니다.";

        if (isBusy)
            return !string.IsNullOrEmpty(battleBusyReason)
                ? $"현재 다른 처리를 진행 중입니다. ({battleBusyReason})"
                : "현재 다른 처리를 진행 중입니다.";

        return "";
    }

    private void LogActionBlocked(string attempted, string reason)
    {
        Debug.Log($"[ActionBlocked] attempted={attempted} reason={reason} busy={GetBattleBusyReason()} turn={turnCount} owner={currentActionSide} pending={BuildPendingStateSummary()}");
    }

    public void SetBattleBusyFromExternal(bool value, string reason = "")
    {
        if (IsGameOver() && value)
            return;

        SetBattleBusy(value, reason);
    }

    private void SetBattleBusy(bool value, string reason = "")
    {
        isBusy = value;
        battleBusyReason = value ? (string.IsNullOrWhiteSpace(reason) ? "Unspecified" : reason) : "";
        battleBusyStartedRealtime = value ? Time.realtimeSinceStartup : -1f;

        string owner = currentActionSide == BattlePlayerSide.My ? "My" : "Enemy";
        string pendingSummary = BuildPendingStateSummary();
        string stateText = value ? "ON" : "OFF";
        Debug.Log($"[BattleBusy {stateText}] reason={battleBusyReason} turn={turnCount} owner={owner} pending={pendingSummary}");

        RefreshTurnEndButtonState();
    }

    private string BuildPendingStateSummary()
    {
        return
            $"question={(questionPanel != null && questionPanel.IsOpen())}, " +
            $"cardQuestion={(cardQuestionPanel != null && cardQuestionPanel.IsOpen())}, " +
            $"fieldSlotSelection={isFieldSlotSelectionModeActive}, " +
            $"summon={(summonManager != null && (summonManager.HasPendingSummonChoice || summonManager.HasPendingFlipChoice))}, " +
            $"move={(movementManager != null && movementManager.HasPendingMoveChoice)}, " +
            $"collab={(collaborationManager != null && collaborationManager.HasPendingCollaborationChoice)}, " +
            $"resting={resolvingRestSlots.Count}, " +
            $"drag={isDraggingHandCard}";
    }

    private void ClearAllPendingActions()
    {
        ClearPendingHandDragState();
        ClearPendingSummonChoice();
        ClearPendingContentChoice();
        ClearPendingMoveChoice();
        ClearPendingCollaborationChoice();
        ClearPendingFieldSlotSelection(false);

        if (questionPanel != null && questionPanel.IsOpen())
            questionPanel.Hide();

        if (cardQuestionPanel != null && cardQuestionPanel.IsOpen())
            cardQuestionPanel.Hide();

        SetBattleBusy(false, "ClearAllPendingActions");

        CloseRestZonePanel();
    }

    private void ClearAllPendingBattleInteractions()
    {
        ClearAllPendingActions();
    }

    public void ClearAllPendingInteractionStates(string reason)
    {
        Debug.Log($"[ClearAllPendingInteractionStates] reason={reason} before={BuildPendingStateSummary()} busy={GetBattleBusyReason()}");

        ClearPendingHandDragState();
        ClearPendingSummonChoice();
        ClearPendingContentChoice();
        ClearPendingMoveChoice();
        ClearPendingCollaborationChoice();
        ClearPendingFieldSlotSelection(false);

        if (effectManager != null)
            effectManager.ClearPendingEffectActivationFromExternal();

        if (questionPanel != null && questionPanel.IsOpen())
            questionPanel.Hide();

        if (cardQuestionPanel != null && cardQuestionPanel.IsOpen())
            cardQuestionPanel.Hide();

        resolvingRestSlots.Clear();
        SetBattleBusy(false, reason);
        CloseRestZonePanel();
        RefreshAllUI();
    }

    private void ClearPendingHandDragState()
    {
        ClearDraggingHandCard();
    }

    public void ClearDraggingHandCardFromExternal()
    {
        ClearDraggingHandCard();
    }

    private void ClearPendingSummonChoice()
    {
        if (summonManager != null)
            summonManager.ClearPending();
    }

    private void ClearPendingMoveChoice()
    {
        if (movementManager != null)
            movementManager.CancelMoveStateFromExternal();
    }

    private void ClearPendingCollaborationChoice()
    {
        if (collaborationManager != null)
            collaborationManager.CancelCollaborationStateFromExternal();
    }

    private void ClearPendingContentChoice()
    {
        pendingContentCard = null;
        pendingContentHandIndex = -1;
        pendingContentInstallSlot = null;

        if (questionPanel != null && questionPanel.IsOpen())
            questionPanel.Hide();

        if (cardQuestionPanel != null && cardQuestionPanel.IsOpen())
            cardQuestionPanel.Hide();
    }

    private void ResetTurnLimitedFlags()
    {
        enemyHasSummonedFaceDownThisTurn = false;
        hasUsedMyIdolActiveThisTurn = false;
        hasUsedEnemyIdolActiveThisTurn = false;

        if (summonManager != null)
            summonManager.ResetTurnLimitedFlagsForNewTurn();

        if (movementManager != null)
            movementManager.ResetAllCharacterMoveFlagsForNewTurn();

        ClearExpiredBroadcastMoveLocks();
        ClearExpiredCollabEffectSilences();
        ClearExpiredCollabAttackForbiddenLocks();
        ClearExpiredBroadcastMoveAndKoLocks();
        ResetAllCharacterActiveFlagsForNewTurn();
    }

    private void ClearExpiredBroadcastMoveLocks()
    {
        ClearExpiredBroadcastMoveLocks(myBattleSlots);
        ClearExpiredBroadcastMoveLocks(enemyBattleSlots);
    }

    private void ClearExpiredBroadcastMoveLocks(List<BattleFieldSlot> slots)
    {
        if (slots == null)
            return;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot == null ||
                !slot.HasCharacter ||
                slot.movementLockedByBroadcastUntilTurn < 0)
            {
                continue;
            }

            if (turnCount > slot.movementLockedByBroadcastUntilTurn)
                slot.SetMovementLockedByBroadcastUntilTurn(-1);
        }
    }

    private void ClearExpiredCollabEffectSilences()
    {
        ClearExpiredCollabEffectSilences(myBattleSlots);
        ClearExpiredCollabEffectSilences(enemyBattleSlots);
    }

    private void ClearExpiredCollabEffectSilences(List<BattleFieldSlot> slots)
    {
        if (slots == null)
            return;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot == null ||
                !slot.HasCharacter ||
                slot.collabEffectsSilencedUntilTurn < 0)
            {
                continue;
            }

            if (turnCount > slot.collabEffectsSilencedUntilTurn)
                slot.ClearCollabEffectsSilence();
        }
    }

    private void ClearExpiredCollabAttackForbiddenLocks()
    {
        ClearExpiredCollabAttackForbiddenLocks(myBattleSlots);
        ClearExpiredCollabAttackForbiddenLocks(enemyBattleSlots);
    }

    private void ClearExpiredCollabAttackForbiddenLocks(List<BattleFieldSlot> slots)
    {
        if (slots == null)
            return;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot == null ||
                !slot.HasCharacter ||
                slot.collabAttackForbiddenUntilTurn < 0)
            {
                continue;
            }

            if (turnCount > slot.collabAttackForbiddenUntilTurn)
                slot.ClearCollabAttackForbidden();
        }
    }

    private void ClearExpiredBroadcastMoveAndKoLocks()
    {
        ClearExpiredBroadcastMoveAndKoLocks(myBattleSlots);
        ClearExpiredBroadcastMoveAndKoLocks(enemyBattleSlots);
    }

    private void ClearExpiredBroadcastMoveAndKoLocks(List<BattleFieldSlot> slots)
    {
        if (slots == null)
            return;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot == null ||
                slot.broadcastMoveAndKoLockedUntilTurn < 0)
            {
                continue;
            }

            if (turnCount > slot.broadcastMoveAndKoLockedUntilTurn)
            {
                slot.ClearBroadcastMoveAndKoLock();
                Debug.Log($"모라하지마 잠금 만료: slot=({slot.owner}, {slot.x}, {slot.y}), turn={turnCount}");
            }
        }
    }

    private void ResetAllCharacterActiveFlagsForNewTurn()
    {
        ResetCharacterActiveFlags(myBattleSlots);
        ResetCharacterActiveFlags(enemyBattleSlots);
    }

    private void ResetCharacterActiveFlags(IEnumerable<BattleFieldSlot> slots)
    {
        if (slots == null)
            return;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot != null)
                slot.SetCharacterActiveUsedThisTurn(false);
        }
    }

    private int RemoveLastingContentsFromSlots(IEnumerable<BattleFieldSlot> slots)
    {
        int removedCount = 0;

        if (slots == null)
            return removedCount;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot == null || slot.contentCard == null)
                continue;

            if (!IsLastingContentCard(slot.contentCard))
                continue;

            BaseCardData removedCard = slot.contentCard;
            BattleSlotOwner owner = slot.contentOwner;

            AddCardToRestZoneFromExternal(owner, removedCard);
            slot.ClearContentCardWithFade();
            removedCount++;

            Debug.Log($"빙하기 테스트 효과: {removedCard.name} 제거 -> {owner} 휴식존");
        }

        return removedCount;
    }

    private List<CardQuestionOption> GetFaceUpCharacterOptionsOnBoard()
    {
        List<CardQuestionOption> candidates = new List<CardQuestionOption>();
        AddFaceUpCharacterOptionsFromSlots(myBattleSlots, candidates);
        AddFaceUpCharacterOptionsFromSlots(enemyBattleSlots, candidates);
        return candidates;
    }

    private void AddFaceUpCharacterOptionsFromSlots(
        IEnumerable<BattleFieldSlot> slots,
        List<CardQuestionOption> candidates)
    {
        if (slots == null || candidates == null)
            return;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot == null || !slot.HasCharacter)
                continue;

            if (slot.isCharacterFaceDown)
                continue;

            if (slot.characterCard != null)
                candidates.Add(new CardQuestionOption(slot.characterCard, slot));
        }
    }

    private void ConfirmSilenceCharacterCollabThisTurn(
        BaseCardData sourceCard,
        BattleSlotOwner owner,
        int cost,
        bool consumeAction,
        int sourceHandIndex,
        CardQuestionOption selectedOption)
    {
        BaseCardData selectedCharacter = selectedOption != null
            ? selectedOption.card
            : null;
        BattleFieldSlot selectedSlot = selectedOption != null
            ? selectedOption.linkedSlot
            : null;

        if (selectedCharacter == null ||
            selectedSlot == null ||
            !selectedSlot.HasCharacter ||
            selectedSlot.characterCard != selectedCharacter ||
            selectedSlot.isCharacterFaceDown)
        {
            SetSystemMessage("채팅 밴 대상으로 선택할 수 없는 캐릭터입니다.");
            return;
        }

        if (!TryPayViewerCostFromExternal(owner, cost))
        {
            SetSystemMessage("시청자가 부족하여 효과를 발동할 수 없습니다.");
            return;
        }

        bool movedToRestZone = sourceHandIndex >= 0
            ? MoveHandCardAtIndexToRestZoneFromExternal(owner, sourceHandIndex, sourceCard)
            : MoveCardFromHandToRestZoneFromExternal(owner, sourceCard);

        if (!movedToRestZone)
        {
            SetSystemMessage("효과 발동 카드를 손패에서 휴식존으로 이동할 수 없습니다.");
            return;
        }

        ApplyCollabEffectSilenceThisTurnFromExternal(selectedSlot);

        string slotText = selectedSlot != null
            ? $" ({selectedSlot.owner} 슬롯 {selectedSlot.x}, {selectedSlot.y})"
            : "";
        string message = $"{selectedCharacter.name}은 이번 턴 합방 효과를 사용할 수 없습니다.{slotText}";
        Debug.Log($"채팅 밴 적용: {selectedCharacter.name}{slotText}, untilTurn={turnCount}");

        RefreshAllUI();

        if (consumeAction)
            ResolveMyActionUsed(message);
        else
            SetSystemMessage(message);
    }

    private List<BaseCardData> GetPreCollabContentCardsInMyHand()
    {
        List<BaseCardData> candidates = new List<BaseCardData>();

        if (myPlayer == null || myPlayer.hand == null)
            return candidates;

        foreach (BaseCardData card in myPlayer.hand)
        {
            if (IsPreCollabContentCard(card) &&
                CanPayViewerCostFromExternal(BattleSlotOwner.My, GetContentCardCost(card)))
            {
                candidates.Add(card);
                Debug.Log($"PreCollab 테스트 후보 감지: {card.id} / {card.name}");
            }
        }

        return candidates;
    }

    private void ConfirmPreCollabContentTest(
        BaseCardData selectedCard,
        Action onContinueCollaboration)
    {
        if (selectedCard == null)
        {
            onContinueCollaboration?.Invoke();
            return;
        }

        int cost = GetContentCardCost(selectedCard);

        if (!TryPayViewerCostFromExternal(BattleSlotOwner.My, cost))
        {
            SetSystemMessage("시청자가 부족하여 합방 전 콘텐츠 카드를 발동할 수 없습니다.");
            onContinueCollaboration?.Invoke();
            return;
        }

        if (!MoveCardFromHandToRestZoneFromExternal(BattleSlotOwner.My, selectedCard))
        {
            SetSystemMessage("합방 전 콘텐츠 카드를 손패에서 휴식존으로 이동할 수 없습니다.");
            onContinueCollaboration?.Invoke();
            return;
        }

        // TODO: Our Tales 실제 조건과 합방 버프/환급 효과는 이후 구현한다.
        string message = $"{selectedCard.name} 발동 테스트: 실제 효과는 아직 미구현";
        Debug.Log($"PreCollab 테스트 발동: {selectedCard.id} / {selectedCard.name}");

        RefreshAllUI();
        SetSystemMessage(message);
        onContinueCollaboration?.Invoke();
    }

    private void CancelPreCollabContentTest(Action onContinueCollaboration)
    {
        SetSystemMessage("합방 전 콘텐츠 카드 발동을 하지 않습니다.");
        onContinueCollaboration?.Invoke();
    }

    public void RequestPreCollabEffectsFromExternal(
        BattleFieldSlot attackerSlot,
        BattleFieldSlot defenderSlot,
        Action onComplete)
    {
        if (IsPreCollabEffectDisabledByBroadcastFromExternal(attackerSlot, defenderSlot, out string disabledMessage))
        {
            SetSystemMessage(disabledMessage);
            onComplete?.Invoke();
            return;
        }

        if (effectManager == null)
        {
            onComplete?.Invoke();
            return;
        }

        EffectContext context = new EffectContext
        {
            battleManager = this,
            collaborationManager = collaborationManager,
            actingOwner = attackerSlot != null ? attackerSlot.characterOwner : BattleSlotOwner.My,
            timing = EffectTiming.PreCollab,
            attackerOriginalSlot = attackerSlot,
            attackerSlot = attackerSlot,
            defenderSlot = defenderSlot,
            battleLocationSlot = defenderSlot,
            sourceSlot = attackerSlot,
            targetSlot = defenderSlot,
            sourceCard = attackerSlot != null ? attackerSlot.characterCard : null,
            targetCard = defenderSlot != null ? defenderSlot.characterCard : null,
            consumeAction = false
        };

        if (context.actingOwner == BattleSlotOwner.Enemy)
        {
            if (TryRequestTestEnemyEffectActivation(EffectTiming.PreCollab, context, onComplete))
                return;

            onComplete?.Invoke();
            return;
        }

        effectManager.RequestOptionalEffectActivation(
            EffectTiming.PreCollab,
            context,
            onComplete
        );
    }

    public void RequestPostCollabEffectsFromExternal(
        BattleFieldSlot attackerSlot,
        BattleFieldSlot defenderSlot,
        Action onComplete)
    {
        if (effectManager == null)
        {
            onComplete?.Invoke();
            return;
        }

        BattleSlotOwner postCollabOwner = ResolvePostCollabActingOwner(attackerSlot, defenderSlot);

        EffectContext context = new EffectContext
        {
            battleManager = this,
            collaborationManager = collaborationManager,
            actingOwner = postCollabOwner,
            timing = EffectTiming.PostCollab,
            attackerOriginalSlot = attackerSlot,
            attackerSlot = attackerSlot,
            defenderSlot = defenderSlot,
            battleLocationSlot = defenderSlot,
            sourceSlot = attackerSlot,
            targetSlot = defenderSlot,
            sourceCard = attackerSlot != null ? attackerSlot.characterCard : null,
            targetCard = defenderSlot != null ? defenderSlot.characterCard : null,
            consumeAction = false
        };

        if (context.actingOwner == BattleSlotOwner.Enemy)
        {
            if (TryRequestTestEnemyEffectActivation(EffectTiming.PostCollab, context, onComplete))
                return;

            onComplete?.Invoke();
            return;
        }

        effectManager.RequestOptionalEffectActivation(
            EffectTiming.PostCollab,
            context,
            onComplete
        );
    }

    private BattleSlotOwner ResolvePostCollabActingOwner(
        BattleFieldSlot attackerSlot,
        BattleFieldSlot defenderSlot)
    {
        if (IsSurvivingPostCollabParticipant(attackerSlot, BattleSlotOwner.My) ||
            IsSurvivingPostCollabParticipant(defenderSlot, BattleSlotOwner.My))
        {
            return BattleSlotOwner.My;
        }

        if (IsSurvivingPostCollabParticipant(attackerSlot, BattleSlotOwner.Enemy) ||
            IsSurvivingPostCollabParticipant(defenderSlot, BattleSlotOwner.Enemy))
        {
            return BattleSlotOwner.Enemy;
        }

        return attackerSlot != null
            ? attackerSlot.characterOwner
            : BattleSlotOwner.My;
    }

    private bool IsSurvivingPostCollabParticipant(
        BattleFieldSlot slot,
        BattleSlotOwner owner)
    {
        return slot != null &&
            slot.HasCharacter &&
            slot.characterCard != null &&
            slot.characterOwner == owner &&
            slot.currentCharacterHp > 0;
    }

    public void RequestOnAppearEffectsFromExternal(
        BattleFieldSlot sourceSlot,
        BaseCardData appearedCard,
        Action onComplete)
    {
        if (effectManager == null)
        {
            onComplete?.Invoke();
            return;
        }

        EffectContext context = new EffectContext
        {
            battleManager = this,
            actingOwner = sourceSlot != null ? sourceSlot.characterOwner : BattleSlotOwner.My,
            timing = EffectTiming.OnAppear,
            sourceSlot = sourceSlot,
            sourceCard = appearedCard,
            consumeAction = false
        };

        effectManager.RequestOptionalEffectActivation(
            EffectTiming.OnAppear,
            context,
            onComplete
        );
    }

    public void RequestOnRestEffectsFromExternal(
        BattleFieldSlot restedSlot,
        BaseCardData restedCard,
        BattleSlotOwner owner,
        Action onComplete)
    {
        if (effectManager == null)
        {
            onComplete?.Invoke();
            return;
        }

        EffectContext context = new EffectContext
        {
            battleManager = this,
            collaborationManager = collaborationManager,
            actingOwner = owner,
            timing = EffectTiming.OnRest,
            sourceSlot = restedSlot,
            defeatedSlot = restedSlot,
            restedSlot = restedSlot,
            sourceCard = restedCard,
            defeatedCard = restedCard,
            restedCard = restedCard,
            consumeAction = false
        };

        if (owner == BattleSlotOwner.Enemy &&
            TryRequestTestEnemyEffectActivation(EffectTiming.OnRest, context, onComplete))
        {
            return;
        }

        effectManager.RequestOptionalEffectActivation(
            EffectTiming.OnRest,
            context,
            onComplete
        );
    }

    private bool TryRequestTestEnemyEffectActivation(
        EffectTiming timing,
        EffectContext context,
        Action onComplete)
    {
        TestEnemy controller = GetTestEnemyController();

        if (controller == null)
            return false;

        return controller.TryResolveEffectActivation(timing, context, onComplete);
    }

    private TestEnemy GetTestEnemyController()
    {
        if (testEnemyController != null)
            return testEnemyController;

        testEnemyController = FindAnyObjectByType<TestEnemy>();

        return testEnemyController;
    }

    private void ResolveMyActionUsed(string actionMessage)
    {
        consecutivePassCount = 0;

        if (TryResolveVictory(actionMessage))
            return;

        myActionUsedThisActionTurn = true;
        Debug.Log($"[ActionResolved] MyActionComplete / action owner remains My / message={actionMessage}");
        RefreshAllUI();
    }

    private IEnumerator EndPlayerActionRoutine()
    {
        if (isBattleEnded)
            yield break;

        ClearAllPendingBattleInteractions();

        bool passedWithoutAction = !myActionUsedThisActionTurn;
        if (passedWithoutAction)
        {
            consecutivePassCount++;

            if (consecutivePassCount >= 2)
            {
                EndCurrentTurnAndStartNextTurn("양쪽 플레이어가 연속으로 행동하지 않았습니다.");
                yield break;
            }
        }
        else
        {
            consecutivePassCount = 0;
        }

        isEndActionButtonFlow = true;

        try
        {
            yield return TransferActionSideRoutine(
                BattlePlayerSide.Enemy,
                "상대의 행동 차례입니다.",
                SimpleMessageExitDirection.LeftToRight,
                "EndActionButton"
            );
        }
        finally
        {
            isEndActionButtonFlow = false;
        }
    }

    private void ResolveEnemyActionUsed(string actionMessage)
    {
        consecutivePassCount = 0;

        if (TryResolveVictory(actionMessage))
            return;

        StartCoroutine(TransferActionSideRoutine(
            BattlePlayerSide.My,
            "당신의 행동 차례입니다.",
            SimpleMessageExitDirection.RightToLeft,
            "EnemyActionUsed"
        ));
    }

    private void ResolveEnemyActionPass(string actionMessage)
    {
        if (isBattleEnded)
            return;

        consecutivePassCount++;

        if (consecutivePassCount >= 2)
        {
            EndCurrentTurnAndStartNextTurn(
                $"{actionMessage}\n" +
                "양쪽 플레이어가 연속으로 행동하지 않았습니다."
            );
            return;
        }

        StartCoroutine(TransferActionSideRoutine(
            BattlePlayerSide.My,
            "당신의 행동 차례입니다.",
            SimpleMessageExitDirection.RightToLeft,
            "EnemyActionPass"
        ));
    }

    private IEnumerator TransferActionSideRoutine(
        BattlePlayerSide nextSide,
        string message,
        SimpleMessageExitDirection exitDirection,
        string reason)
    {
        if (IsGameOver())
            yield break;

        if (nextSide == BattlePlayerSide.Enemy && !isEndActionButtonFlow)
        {
            Debug.LogError($"[ActionTransfer BLOCKED] reason={reason} / currentOwner={currentActionSide}");
            yield break;
        }

        BattlePlayerSide previousSide = currentActionSide;
        SetBattleBusy(true, $"TransferActionSideRoutine:{nextSide}:{reason}");
        currentActionSide = nextSide;

        if (nextSide == BattlePlayerSide.My)
            myActionUsedThisActionTurn = false;
        else if (nextSide == BattlePlayerSide.Enemy)
            myActionUsedThisActionTurn = false;

        Debug.Log($"[ActionTransfer] {previousSide} -> {nextSide} / reason={reason}");

        RefreshAllUI();

        bool shouldClearBusy = true;

        try
        {
            yield return PlaySystemMessageRoutine(message, exitDirection);
        }
        finally
        {
            if (shouldClearBusy)
                SetBattleBusy(false, "TransferActionSideRoutine finished");
        }
    }

    private bool TryResolveVictory(string previousMessage)
    {
        if (!victoryCheckEnabled)
            return false;

        if (isBattleEnded || myPlayer == null || enemyPlayer == null)
            return isBattleEnded;

        bool myReached = myPlayer.viewers >= VictoryViewerThreshold;
        bool enemyReached = enemyPlayer.viewers >= VictoryViewerThreshold;

        if (isVictoryTiebreakerActive)
        {
            if (myPlayer.viewers == enemyPlayer.viewers)
                return false;

            BattlePlayerSide winner =
                myPlayer.viewers > enemyPlayer.viewers
                    ? BattlePlayerSide.My
                    : BattlePlayerSide.Enemy;

            ResolveVictory(winner, previousMessage, "동점 승부가 깨졌습니다.");
            return true;
        }

        if (!myReached && !enemyReached)
            return false;

        if (myReached && enemyReached && myPlayer.viewers == enemyPlayer.viewers)
        {
            isVictoryTiebreakerActive = true;

            return false;
        }

        BattlePlayerSide resolvedWinner =
            myReached && (!enemyReached || myPlayer.viewers > enemyPlayer.viewers)
                ? BattlePlayerSide.My
                : BattlePlayerSide.Enemy;

        ResolveVictory(resolvedWinner, previousMessage, $"{VictoryViewerThreshold} 시청자를 달성했습니다.");
        return true;
    }

    private void ResolveVictory(
        BattlePlayerSide winner,
        string previousMessage,
        string reason)
    {
        isGameOver = true;
        isBattleEnded = true;
        SetBattleBusy(false, "ResolveVictory");
        currentPhase = BattlePhase.None;
        consecutivePassCount = 0;

        ClearAllPendingBattleInteractions();

        if (turnEndButton != null)
            turnEndButton.interactable = false;

        RefreshAllUI();

        string winnerName = GetSideName(winner);

        string resultMessage =
            $"{previousMessage}\n\n" +
            $"{reason}\n" +
            $"{winnerName} 승리!\n" +
            $"내 시청자: {myPlayer.viewers}\n" +
            $"상대 시청자: {enemyPlayer.viewers}";

        SetSystemMessage(resultMessage);
        ShowBattleResultPanel(winner == BattlePlayerSide.My ? "승리하였습니다" : "패배하였습니다");
    }

    private void ShowBattleResultPanel(string resultMessage)
    {
        ResolveBattleResultPanel();

        if (battleResultPanel == null)
            return;

        if (battleResultText != null)
            battleResultText.text = resultMessage;

        battleResultPanel.SetActive(true);
        battleResultPanel.transform.SetAsLastSibling();
    }

    private void EndCurrentTurnAndStartNextTurn(string reasonMessage)
    {
        if (IsGameOver())
            return;

        StartCoroutine(EndCurrentTurnAndStartNextTurnRoutine(reasonMessage));
    }

    private IEnumerator EndCurrentTurnAndStartNextTurnRoutine(string reasonMessage)
    {
        if (IsGameOver())
            yield break;

        ClearAllPendingBattleInteractions();
        SetBattleBusy(true, "EndCurrentTurnAndStartNextTurnRoutine");

        turnCount++;
        EffectStatService.ExpireTurnEndModifiers(this, turnCount);
        if (effectManager != null)
            effectManager.ClearExpiredNegativeAmountInvertStatesFromExternal();

        consecutivePassCount = 0;
        currentActionSide = firstPlayerSide;
        myActionUsedThisActionTurn = false;
        isEndActionButtonFlow = false;

        ResetTurnLimitedFlags();

        yield return PlayTurnIntroRoutine(turnCount);

        int myDrawnCount = 0;
        int enemyDrawnCount = 0;

        yield return DrawCardsWithAnimationRoutine(
            BattleSlotOwner.My,
            1,
            drawnCount => myDrawnCount = drawnCount
        );

        yield return DrawCardsWithAnimationRoutine(
            BattleSlotOwner.Enemy,
            1,
            drawnCount => enemyDrawnCount = drawnCount
        );

        int myGainedViewers = GainPrepViewers(BattleSlotOwner.My);
        int enemyGainedViewers = GainPrepViewers(BattleSlotOwner.Enemy);

        SetBattleBusy(false, "EndCurrentTurnAndStartNextTurnRoutine finished");
        RefreshAllUI();

        string turnStartMessage =
            $"{reasonMessage}\n\n" +
            $"{turnCount}턴 시작.\n" +
            $"현재 행동권: {GetSideName(currentActionSide)}\n" +
            $"내 드로우 {myDrawnCount}장 / 상대 드로우 {enemyDrawnCount}장\n" +
            $"내 시청자 +{myGainedViewers}\n" +
            $"상대 시청자 +{enemyGainedViewers}";

        if (TryResolveVictory(turnStartMessage))
            yield break;

        SetSystemMessage(turnStartMessage);
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
        return !IsGameOver() &&
            !IsBattleBusy() &&
            currentPhase == BattlePhase.MainGame &&
            currentActionSide == BattlePlayerSide.Enemy;
    }

    public void TestEnemyPassAction()
    {
        if (IsGameOver() || IsBattleBusy())
            return;

        if (currentPhase != BattlePhase.MainGame)
            return;

        if (currentActionSide != BattlePlayerSide.Enemy)
            return;

        ResolveEnemyActionPass("상대는 행동하지 않았습니다.");
    }

    public void TestEnemyUseAction(string actionMessage)
    {
        if (IsGameOver() || IsBattleBusy())
            return;

        if (currentPhase != BattlePhase.MainGame)
            return;

        if (currentActionSide != BattlePlayerSide.Enemy)
            return;

        ResolveEnemyActionUsed(actionMessage);
    }

    public bool TryExecuteTestEnemyAttack()
    {
        if (IsGameOver() || IsBattleBusy())
            return false;

        if (currentPhase != BattlePhase.MainGame)
            return false;

        if (currentActionSide != BattlePlayerSide.Enemy)
            return false;

        if (movementManager == null || collaborationManager == null)
            return false;

        BattleFieldSlot attackerSlot;
        BattleFieldSlot defenderSlot;
        if (!FindTestEnemyAttackCandidate(out attackerSlot, out defenderSlot))
            return false;

        return ExecuteEnemyCollaborationAttack(attackerSlot, defenderSlot);
    }

    private bool FindTestEnemyAttackCandidate(
        out BattleFieldSlot attackerSlot,
        out BattleFieldSlot defenderSlot)
    {
        attackerSlot = null;
        defenderSlot = null;

        foreach (BattleFieldSlot enemySlot in enemyBattleSlots)
        {
            if (!IsTestEnemyAttackSourceSlot(enemySlot))
                continue;

            List<BattleFieldSlot> attackableSlots =
                GetAttackablePlayerCharacterSlots(enemySlot);

            if (attackableSlots.Count == 0)
                continue;

            attackerSlot = enemySlot;
            defenderSlot = attackableSlots[0];
            return true;
        }

        return false;
    }

    private List<BattleFieldSlot> GetAttackablePlayerCharacterSlots(BattleFieldSlot attackerSlot)
    {
        List<BattleFieldSlot> result = new List<BattleFieldSlot>();

        if (movementManager == null || attackerSlot == null)
            return result;

        foreach (BattleFieldSlot playerSlot in myBattleSlots)
        {
            if (!IsTestEnemyAttackTargetSlot(playerSlot))
                continue;

            string failReason;
            if (movementManager.CanStartCollaborationForOwnerFromExternal(
                    BattleSlotOwner.Enemy,
                    attackerSlot,
                    playerSlot,
                    out failReason))
            {
                result.Add(playerSlot);
            }
        }

        return result;
    }

    private bool ExecuteEnemyCollaborationAttack(
        BattleFieldSlot attackerSlot,
        BattleFieldSlot defenderSlot)
    {
        if (collaborationManager == null)
            return false;

        string failReason;
        if (movementManager == null ||
            !movementManager.CanStartCollaborationForOwnerFromExternal(
                BattleSlotOwner.Enemy,
                attackerSlot,
                defenderSlot,
                out failReason))
        {
            return false;
        }

        return collaborationManager.StartCollaboration(attackerSlot, defenderSlot);
    }

    private bool IsTestEnemyAttackSourceSlot(BattleFieldSlot slot)
    {
        if (slot == null ||
            !slot.HasCharacter ||
            slot.characterOwner != BattleSlotOwner.Enemy ||
            slot.isCharacterFaceDown)
        {
            return false;
        }

        CharacterCardData character = slot.characterCard as CharacterCardData;
        return character != null;
    }

    private bool IsTestEnemyAttackTargetSlot(BattleFieldSlot slot)
    {
        if (slot == null ||
            !slot.HasCharacter ||
            slot.characterOwner != BattleSlotOwner.My)
        {
            return false;
        }

        CharacterCardData character = slot.characterCard as CharacterCardData;
        return character != null;
    }

    public bool TestEnemyTrySummonBacksideCharacter()
    {
        if (IsGameOver() || IsBattleBusy())
            return false;

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
            card.kind == "Character" &&
            (summonManager == null || summonManager.CanSummonBacksideByCostFromExternal(card))
        );

        if (characterCard == null)
            return false;

        BattleFieldSlot targetSlot = enemyBattleSlots.FirstOrDefault(slot =>
            slot != null &&
            slot.owner == BattleSlotOwner.Enemy &&
            slot.HasBroadcast &&
            !slot.HasCharacter &&
            !IsFaceDownSummonForbiddenByBroadcastFromExternal(slot, out _)
        );

        if (targetSlot == null)
            return false;

        targetSlot.SetCharacterCard(characterCard, cardBackSprite, true, BattleSlotOwner.Enemy);
        ApplyBroadcastEnterEffectsFromExternal(targetSlot, false);
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

    public bool TestEnemyTryFlipSummonCharacter()
    {
        if (IsGameOver() || IsBattleBusy())
            return false;

        if (currentPhase != BattlePhase.MainGame)
            return false;

        if (currentActionSide != BattlePlayerSide.Enemy)
            return false;

        if (enemyPlayer == null)
            return false;

        if (summonManager == null)
            return false;

        BattleFieldSlot targetSlot = enemyBattleSlots.FirstOrDefault(slot =>
        {
            if (slot == null ||
                slot.owner != BattleSlotOwner.Enemy ||
                slot.characterOwner != BattleSlotOwner.Enemy ||
                !slot.HasCharacter ||
                !slot.isCharacterFaceDown ||
                !summonManager.CanFlipSummonByTurnFromExternal(slot, out _))
            {
                return false;
            }

            BaseCardData card = slot.characterCard;
            int cost = summonManager.GetCharacterAppearCostFromExternal(card);

            return card != null &&
                CanPayViewerCost(enemyPlayer, cost) &&
                LoadCardSprite(card) != null;
        });

        if (targetSlot == null)
            return false;

        BaseCardData characterCard = targetSlot.characterCard;

        if (characterCard == null)
            return false;

        int cost = summonManager.GetCharacterAppearCostFromExternal(characterCard);

        if (!CanPayViewerCost(enemyPlayer, cost))
            return false;

        Sprite sprite = LoadCardSprite(characterCard);

        if (sprite == null)
            return false;

        enemyPlayer.viewers -= cost;

        targetSlot.SetCharacterCard(characterCard, sprite, false, BattleSlotOwner.Enemy);
        ApplyBroadcastEnterEffectsFromExternal(targetSlot, false);
        targetSlot.faceUpSummonedTurn = turnCount;

        RefreshAllUI();

        ResolveEnemyActionUsed(
            $"Enemy flip summoned {characterCard.name} at ({targetSlot.x}, {targetSlot.y}).\n" +
            $"Viewers -{cost}"
        );

        return true;
    }

    public bool TestEnemyTrySummonFrontCharacter()
    {
        if (IsGameOver() || IsBattleBusy())
            return false;

        if (currentPhase != BattlePhase.MainGame)
            return false;

        if (currentActionSide != BattlePlayerSide.Enemy)
            return false;

        if (enemyPlayer == null || enemyPlayer.hand == null)
            return false;

        if (summonManager == null)
            return false;

        BattleFieldSlot targetSlot = enemyBattleSlots.FirstOrDefault(slot =>
            slot != null &&
            slot.owner == BattleSlotOwner.Enemy &&
            slot.HasBroadcast &&
            !slot.HasCharacter
        );

        if (targetSlot == null)
            return false;

        BaseCardData characterCard = enemyPlayer.hand.FirstOrDefault(card =>
            card != null &&
            card.kind == "Character" &&
            CanPayViewerCost(enemyPlayer, summonManager.GetCharacterAppearCostFromExternal(card)) &&
            LoadCardSprite(card) != null
        );

        if (characterCard == null)
            return false;

        int cost = summonManager.GetCharacterAppearCostFromExternal(characterCard);
        Sprite sprite = LoadCardSprite(characterCard);

        if (sprite == null)
            return false;

        enemyPlayer.viewers -= cost;

        targetSlot.SetCharacterCard(characterCard, sprite, false, BattleSlotOwner.Enemy);
        ApplyBroadcastEnterEffectsFromExternal(targetSlot, false);
        targetSlot.faceUpSummonedTurn = turnCount;
        enemyPlayer.hand.Remove(characterCard);

        RefreshAllUI();

        ResolveEnemyActionUsed(
            $"Enemy front summoned {characterCard.name} at ({targetSlot.x}, {targetSlot.y}).\n" +
            $"Viewers -{cost}"
        );

        return true;
    }

    public void TestEnemyPlaceBroadcastCard()
    {
        if (IsGameOver() || IsBattleBusy())
            return;

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
        if (HandlePendingFieldSlotSelectionClick(slot))
            return;

        string inputFailReason;
        if (IsInputBlocked(out inputFailReason))
        {
            SetSystemMessage(inputFailReason);
            return;
        }

        SelectCard(card);
        SetSystemMessage($"방송 카드 확인: {card.name}");
    }

    private void OnClickCharacterCardOnField(BattleFieldSlot slot, BaseCardData card) 
    {
        if (slot == null || card == null)
            return;

        if (HandlePendingFieldSlotSelectionClick(slot))
            return;

        string inputFailReason;
        if (IsInputBlocked(out inputFailReason))
        {
            SetSystemMessage(inputFailReason);
            return;
        }

        if (slot.characterOwner == BattleSlotOwner.My)
        {
            SelectFieldCharacter(slot);

            if (slot.isCharacterFaceDown)
            {
                if (summonManager != null)
                    summonManager.OpenFlipSummonQuestion(slot, card);
                else
                    SetSystemMessage("SummonManager가 연결되어 있지 않습니다.");

                return;
            }

            SetSystemMessage($"캐릭터 카드 확인: {card.name}");
            return;
        }

        if (slot.characterOwner == BattleSlotOwner.Enemy)
        {
            if (slot.isCharacterFaceDown)
            {
                if (cardDetailPanel != null)
                    cardDetailPanel.Clear();

                SetSystemMessage("상대의 뒷면 캐릭터입니다.");
                return;
            }

            SelectFieldCharacter(slot);
            SetSystemMessage($"상대 캐릭터 카드 확인: {card.name}");
        }
    }

    private void OnDoubleClickCharacterCardOnField(BattleFieldSlot slot, BaseCardData card)
    {
        RequestCharacterActiveFromSlot(slot);
    }

    public void RequestCharacterActiveFromSlot(BattleFieldSlot slot)
    {
        if (!CanUseCharacterActive(slot, out string failReason))
        {
            SetSystemMessage(failReason);
            return;
        }

        BaseCardData card = slot.characterCard;
        string message = $"{card.name}의 액티브 효과를 발동하시겠습니까?";

        if (questionPanel == null ||
            !questionPanel.TryShowYesNoQuestion(
                message,
                () => ConfirmCharacterActive(slot),
                () => SetSystemMessage($"{card.name} 액티브 효과 발동을 취소했습니다."),
                () => SetSystemMessage($"{card.name} 액티브 효과 발동을 취소했습니다.")
            ))
        {
            SetSystemMessage("액티브 효과 질문창을 열 수 없습니다.");
        }
    }

    private bool CanUseCharacterActive(BattleFieldSlot slot, out string failReason)
    {
        failReason = "";

        if (slot == null || slot.characterCard == null)
        {
            failReason = "효과를 발동할 캐릭터가 없습니다.";
            return false;
        }

        if (slot.characterOwner != BattleSlotOwner.My)
        {
            failReason = "상대 캐릭터의 효과는 발동할 수 없습니다.";
            return false;
        }

        if (slot.isCharacterFaceDown)
        {
            failReason = "뒷면 캐릭터는 효과를 발동할 수 없습니다.";
            return false;
        }

        if (!HasActiveEffect(slot.characterCard))
        {
            failReason = "이 캐릭터는 효과를 발동할 수 없습니다.";
            return false;
        }

        if (slot.characterActiveUsedThisTurn)
        {
            failReason = "이 캐릭터는 효과를 발동할 수 없습니다.";
            return false;
        }

        if (!CanUseMyAction(out failReason))
            return false;

        int cost = GetActiveCost(slot.characterCard);
        if (!CanPayViewerCostFromExternal(BattleSlotOwner.My, cost))
        {
            failReason = "시청자가 부족합니다.";
            return false;
        }

        return true;
    }

    private void ConfirmCharacterActive(BattleFieldSlot slot)
    {
        if (!CanUseCharacterActive(slot, out string failReason))
        {
            SetSystemMessage(failReason);
            return;
        }

        bool waitForEffectCompletion = IsCharacterActiveEffectRef(
            slot.characterCard,
            "character.active.discardOneThenFetchContentByTagFromDeck") ||
            IsCharacterActiveEffectRef(
                slot.characterCard,
                "character.active.forceBattleTargetAnywhere") ||
            IsCharacterActiveEffectRef(
                slot.characterCard,
                "character.active.modifyTaggedOnBoard");

        EffectActivationRequest request = new EffectActivationRequest
        {
            sourceCard = slot.characterCard,
            owner = BattleSlotOwner.My,
            timing = EffectTiming.CharacterActive,
            sourceSlot = slot,
            targetSlot = null,
            handIndex = -1,
            consumeAction = true,
            onComplete = waitForEffectCompletion
                ? (Action<bool>)(success =>
                {
                    if (!success)
                    {
                        RefreshAllUI();
                        return;
                    }

                    slot.SetCharacterActiveUsedThisTurn(true);
                    RefreshAllUI();
                })
                : null
        };

        if (effectManager != null && effectManager.TryActivateEffect(request))
        {
            if (!waitForEffectCompletion)
            {
                slot.SetCharacterActiveUsedThisTurn(true);
                RefreshAllUI();
            }

            return;
        }

        SetSystemMessage("효과를 발동할 수 없습니다.");
    }

    public void RequestIdolActive(BattleSlotOwner owner)
    {
        if (!CanUseIdolActive(owner, out string failReason))
        {
            SetSystemMessage(failReason);
            return;
        }

        BaseCardData idolCard = GetIdolCardFromExternal(owner);
        string message = $"{idolCard.name}의 액티브 효과를 발동하시겠습니까?";

        if (questionPanel == null ||
            !questionPanel.TryShowYesNoQuestion(
                message,
                () => ConfirmIdolActive(owner),
                () => SetSystemMessage($"{idolCard.name} 액티브 효과 발동을 취소했습니다."),
                () => SetSystemMessage($"{idolCard.name} 액티브 효과 발동을 취소했습니다.")
            ))
        {
            SetSystemMessage("아이돌 액티브 효과 질문창을 열 수 없습니다.");
        }
    }

    private bool CanUseIdolActive(BattleSlotOwner owner, out string failReason)
    {
        failReason = "";

        if (owner != BattleSlotOwner.My)
        {
            failReason = "상대 아이돌의 효과는 발동할 수 없습니다.";
            return false;
        }

        BaseCardData idolCard = GetIdolCardFromExternal(owner);

        if (idolCard == null)
        {
            failReason = "아이돌 카드가 없습니다.";
            return false;
        }

        if (!HasActiveEffect(idolCard))
        {
            failReason = "발동 가능한 아이돌 액티브 효과가 없습니다.";
            return false;
        }

        if (IsIdolActiveDisabledByBroadcastFromExternal(owner))
        {
            failReason = "공포게임 위에 있는 캐릭터 때문에 아이돌 액티브를 사용할 수 없습니다.";
            return false;
        }

        if (HasUsedIdolActiveThisTurn(owner))
        {
            failReason = "이미 이번 턴에 아이돌 액티브 효과를 사용했습니다.";
            return false;
        }

        if (!CanUseMyAction(out failReason))
            return false;

        int cost = GetActiveCost(idolCard);
        if (!CanPayViewerCostFromExternal(owner, cost))
        {
            failReason = "시청자가 부족합니다.";
            return false;
        }

        return true;
    }

    private void ConfirmIdolActive(BattleSlotOwner owner)
    {
        if (!CanUseIdolActive(owner, out string failReason))
        {
            SetSystemMessage(failReason);
            return;
        }

        BaseCardData idolCard = GetIdolCardFromExternal(owner);
        bool waitForEffectCompletion = IsIdolActiveEffectRef(
            idolCard,
            "idol.active.fullHealOneControlled") ||
            IsIdolActiveEffectRef(
                idolCard,
                "idol.active.callFromRestByTagThenDonateViewers") ||
            IsIdolActiveEffectRef(
                idolCard,
                "idol.active.fetchTabiOrRestBoongAndFetchBoth");

        EffectActivationRequest request = new EffectActivationRequest
        {
            sourceCard = idolCard,
            owner = owner,
            timing = EffectTiming.IdolActive,
            sourceSlot = null,
            targetSlot = null,
            handIndex = -1,
            consumeAction = true,
            onComplete = waitForEffectCompletion
                ? (Action<bool>)(success =>
                {
                    if (!success)
                    {
                        RefreshAllUI();
                        return;
                    }

                    SetIdolActiveUsedThisTurn(owner, true);
                    RefreshAllUI();
                })
                : null
        };

        if (effectManager != null && effectManager.TryActivateEffect(request))
        {
            if (!waitForEffectCompletion)
            {
                SetIdolActiveUsedThisTurn(owner, true);
                RefreshAllUI();
            }

            return;
        }

        SetSystemMessage("효과를 발동할 수 없습니다.");
    }

    private bool HasUsedIdolActiveThisTurn(BattleSlotOwner owner)
    {
        return owner == BattleSlotOwner.My
            ? hasUsedMyIdolActiveThisTurn
            : hasUsedEnemyIdolActiveThisTurn;
    }

    private void SetIdolActiveUsedThisTurn(BattleSlotOwner owner, bool value)
    {
        if (owner == BattleSlotOwner.My)
            hasUsedMyIdolActiveThisTurn = value;
        else
            hasUsedEnemyIdolActiveThisTurn = value;
    }

    private bool HasActiveEffect(BaseCardData card)
    {
        if (card is CharacterCardData character)
            return HasEffectAtTiming(character.effects, EffectTiming.CharacterActive);

        if (card is IdolCardData idol)
            return idol.active != null && idol.active.Length > 0 && HasAnyEffectRef(idol.active);

        return false;
    }

    private bool HasEffectAtTiming(EffectData[] effects, EffectTiming timing)
    {
        if (effects == null)
            return false;

        foreach (EffectData effect in effects)
        {
            if (effect == null || string.IsNullOrEmpty(effect.timing))
                continue;

            if (TryParseEffectTimingForBattleManager(effect.timing, out EffectTiming effectTiming) &&
                effectTiming == timing &&
                !string.IsNullOrWhiteSpace(GetEffectRefForBattleManager(effect)))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsCharacterActiveEffectRef(BaseCardData card, string effectRef)
    {
        if (!(card is CharacterCardData character) ||
            character.effects == null ||
            string.IsNullOrWhiteSpace(effectRef))
        {
            return false;
        }

        foreach (EffectData effect in character.effects)
        {
            if (effect == null ||
                !TryParseEffectTimingForBattleManager(effect.timing, out EffectTiming timing) ||
                timing != EffectTiming.CharacterActive)
            {
                continue;
            }

            string refId = !string.IsNullOrWhiteSpace(effect.@ref)
                ? effect.@ref
                : effect.refName;

            if (string.Equals(refId, effectRef, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool IsIdolActiveEffectRef(BaseCardData card, string effectRef)
    {
        if (!(card is IdolCardData idol) ||
            idol.active == null ||
            string.IsNullOrWhiteSpace(effectRef))
        {
            return false;
        }

        foreach (EffectData effect in idol.active)
        {
            if (effect == null ||
                !TryParseEffectTimingForBattleManager(effect.timing, out EffectTiming timing) ||
                timing != EffectTiming.IdolActive)
            {
                continue;
            }

            string refId = !string.IsNullOrWhiteSpace(effect.@ref)
                ? effect.@ref
                : effect.refName;

            if (string.Equals(refId, effectRef, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool HasAnyEffectRef(EffectData[] effects)
    {
        if (effects == null)
            return false;

        foreach (EffectData effect in effects)
        {
            if (!string.IsNullOrWhiteSpace(GetEffectRefForBattleManager(effect)))
                return true;
        }

        return false;
    }

    private int GetActiveCost(BaseCardData card)
    {
        if (card is CharacterCardData character)
            return Mathf.Max(0, character.activeCost);

        if (card is IdolCardData idol)
            return Mathf.Max(0, idol.activeCost);

        return 0;
    }

    private bool TryParseEffectTimingForBattleManager(string value, out EffectTiming timing)
    {
        timing = EffectTiming.None;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value
            .Trim()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "")
            .ToLowerInvariant();

        switch (normalized)
        {
            case "idolactive":
                timing = EffectTiming.IdolActive;
                return true;

            case "characteractive":
            case "characteract":
            case "active":
                timing = EffectTiming.CharacterActive;
                return true;

            default:
                return Enum.TryParse(value, true, out timing);
        }
    }

    private void OnClickContentCardOnField(BattleFieldSlot slot, BaseCardData card)
    {
        if (HandlePendingFieldSlotSelectionClick(slot))
            return;

        string inputFailReason;
        if (IsInputBlocked(out inputFailReason))
        {
            SetSystemMessage(inputFailReason);
            return;
        }

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

        if (cardQuestionPanel != null && cardQuestionPanel.IsOpen())
        {
            ClearDraggingHandCard();
            SetSystemMessage("이미 카드 선택창이 열려 있습니다.");
            return;
        }

        if (!isDraggingHandCard || draggingHandCardData == null)
        {
            ClearDraggingHandCard();
            SetSystemMessage("드래그 중인 손패 카드가 없습니다.");
            return;
        }

        BaseCardData card = draggingHandCardData;

        if (IsCharacterCardKind(card))
        {
            if (summonManager == null)
            {
                ClearDraggingHandCard();
                SetSystemMessage("SummonManager가 연결되어 있지 않습니다.");
                return;
            }

            summonManager.OpenSummonQuestion(slot, card);
            return;
        }

        if (CanInstallAsFieldContentCard(card))
        {
            OpenLastingContentInstallQuestion(slot, card);
            return;
        }

        ClearDraggingHandCard();
        ClearPendingSummonChoice();
        SetSystemMessage("캐릭터 카드 또는 지속형 콘텐츠 카드만 슬롯에 배치할 수 있습니다.");
    }

    private bool CanPayViewerCost(BattlePlayerRuntime player, int cost)
    {
        if (player == null)
            return false;

        return player.viewers >= cost;
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

    private IEnumerator DrawCardsWithAnimationRoutine(
        BattleSlotOwner owner,
        int count,
        Action<int> onComplete)
    {
        BattlePlayerRuntime player = GetPlayerRuntime(owner);
        int drawnCount = 0;

        if (player == null || count <= 0)
        {
            onComplete?.Invoke(0);
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            if (player.mainDeck == null || player.mainDeck.Count == 0)
            {
                Debug.LogWarning($"{player.playerName}의 메인 덱이 비어 있어 더 이상 드로우할 수 없습니다.");
                break;
            }

            yield return PlayDrawCardAnimation(owner);

            BaseCardData drawnCard = player.mainDeck[0];
            player.mainDeck.RemoveAt(0);
            player.hand.Add(drawnCard);
            drawnCount++;

            RefreshAllUI();
        }

        onComplete?.Invoke(drawnCount);
    }

    private IEnumerator PlayDrawCardAnimation(BattleSlotOwner owner)
    {
        Transform deckTransform = owner == BattleSlotOwner.My
            ? myDeckSlot
            : enemyDeckSlot;
        Transform handTransform = owner == BattleSlotOwner.My
            ? myHandPanel
            : enemyHandCardArea;

        if (deckTransform == null || handTransform == null || cardBackSprite == null)
            yield break;

        Canvas canvas = ResolveAnimationCanvas(deckTransform);

        if (canvas == null)
            yield break;

        GameObject drawObject = new GameObject(
            "RuntimeDrawCardAnimation",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image)
        );

        drawObject.transform.SetParent(canvas.transform, false);
        drawObject.transform.SetAsLastSibling();

        RectTransform rect = drawObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = ResolveDrawAnimationSize(deckTransform);
        rect.position = GetTransformCenterPosition(deckTransform);

        Image image = drawObject.GetComponent<Image>();
        image.sprite = cardBackSprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;

        CanvasGroup canvasGroup = drawObject.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        Vector3 startPosition = GetTransformCenterPosition(deckTransform);
        Vector3 targetPosition = GetTransformCenterPosition(handTransform);
        float duration = Mathf.Max(0.01f, drawAnimationDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            rect.position = Vector3.Lerp(startPosition, targetPosition, easedT);
            yield return null;
        }

        rect.position = targetPosition;
        Destroy(drawObject);
    }

    private Canvas ResolveAnimationCanvas(Transform referenceTransform)
    {
        Canvas canvas = null;

        if (referenceTransform != null)
            canvas = referenceTransform.GetComponentInParent<Canvas>();

        if (canvas != null)
            return canvas;

        canvas = GetComponentInParent<Canvas>();

        if (canvas != null)
            return canvas;

        return FindAnyObjectByType<Canvas>();
    }

    private Vector2 ResolveDrawAnimationSize(Transform deckTransform)
    {
        RectTransform deckRect = deckTransform as RectTransform;

        if (deckRect != null && deckRect.rect.width > 1f && deckRect.rect.height > 1f)
            return deckRect.rect.size;

        return drawAnimationCardSize;
    }

    private Vector3 GetTransformCenterPosition(Transform targetTransform)
    {
        RectTransform rect = targetTransform as RectTransform;

        if (rect == null)
            return targetTransform.position;

        return rect.TransformPoint(rect.rect.center);
    }

    private void RefreshAllUI()
    {
        RefreshStatusUI();
        RefreshZoneTexts();
        RefreshHandUI();
        RefreshEnemyHandUI();
        RefreshOpenRestZonePanelIfNeeded();

        if (currentPhase == BattlePhase.BroadcastSetup)
            RefreshBroadcastSetupButtons();

        RefreshTurnEndButtonState();
    }

    private void RefreshTurnEndButtonState()
    {
        RefreshTurnEndButtonPanelColor();

        if (turnEndButton != null)
        {
            turnEndButton.interactable =
                !IsGameOver() &&
                !IsBattleBusy() &&
                currentPhase == BattlePhase.MainGame &&
                currentActionSide == BattlePlayerSide.My &&
                (questionPanel == null || !questionPanel.IsOpen());
        }
    }

    private void RefreshTurnEndButtonPanelColor()
    {
        if (turnEndButtonPanelImage == null)
            ResolveTurnEndButtonPanelImage();

        if (turnEndButtonPanelImage == null)
            return;

        if (currentPhase != BattlePhase.MainGame || IsGameOver())
        {
            turnEndButtonPanelImage.color = inactiveTurnEndPanelColor;
            return;
        }

        turnEndButtonPanelImage.color =
            currentActionSide == BattlePlayerSide.My
                ? myTurnEndPanelColor
                : enemyTurnEndPanelColor;
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
                $"방송 설치: {myBroadcastPlacedCount}/{myRequiredBroadcastCount}" +
                GetVictoryStatusText();
        }

        if (enemyStatusText != null && enemyPlayer != null)
        {
            enemyStatusText.text =
                $"{enemyPlayer.playerName}\n" +
                $"시청자: {enemyPlayer.viewers}\n" +
                $"메인 덱: {enemyPlayer.mainDeck.Count}\n" +
                $"방송 덱: {enemyPlayer.broadcastDeck.Count}\n" +
                $"손패: {enemyPlayer.hand.Count}\n" +
                $"방송 설치: {enemyBroadcastPlacedCount}/{enemyRequiredBroadcastCount}" +
                GetVictoryStatusText();
        }

        RefreshViewerTextUI();
    }

    private string GetVictoryStatusText()
    {
        if (isBattleEnded)
            return "\n배틀 종료";

        if (isVictoryTiebreakerActive)
            return "\n동점 승부 중";

        return "";
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

        SetZoneCardVisual(
            myIdolSlot,
            myPlayer.idolCard,
            false,
            cardToSelect => SelectCard(cardToSelect),
            false,
            cardToActivate => RequestIdolActive(BattleSlotOwner.My)
        );
        SetZoneCardVisual(enemyIdolSlot, enemyPlayer.idolCard, false, cardToSelect => SelectCard(cardToSelect), true);

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

        for (int i = 0; i < myPlayer.hand.Count; i++)
        {
            CreateHandCardItem(myPlayer.hand[i], myHandPanel, i);
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

    private void CreateHandCardItem(BaseCardData card, Transform parent, int handIndex)
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
                leftClickAction: selected => SelectHandCard(selected, handIndex),
                rightClickAction: null,
                doubleClickAction: selected => OnDoubleClickHandCard(selected, handIndex)
            );

            bool canDrag = CanPotentiallyDragHandCard(card);

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
            button.onClick.AddListener(() => SelectHandCard(card, handIndex));
        }

        ApplyHandCardSelectionHighlight(itemObject, handIndex);
    }

    private void OnDoubleClickHandCard(BaseCardData card, int handIndex)
    {
        if (card == null)
            return;

        if (!IsContentCardKind(card))
        {
            SelectHandCard(card, handIndex);
            return;
        }

        if (CanInstallAsFieldContentCard(card))
        {
            SelectHandCard(card, handIndex);
            SetSystemMessage(
                $"{card.name} 지속형 콘텐츠 카드입니다.\n" +
                "설치할 방송 슬롯으로 드래그하세요."
            );
            return;
        }

        if (IsCollabContentCard(card))
        {
            SelectHandCard(card, handIndex);
            SetSystemMessage(
                $"{card.name} 합방 타이밍 콘텐츠 카드입니다.\n" +
                "일반 행동권이 아니라 합방 전후 타이밍에서 사용할 수 있습니다."
            );
            return;
        }

        if (selectedHandCardIndex == handIndex && selectedCard == card)
        {
            OpenSelectedContentUseConfirmQuestion(card);
            return;
        }

        OpenContentUseQuestion(card);
    }

    private void OpenSelectedContentUseConfirmQuestion(BaseCardData card)
    {
        if (card == null)
            return;

        if (effectManager == null)
        {
            SetSystemMessage("EffectManager가 연결되어 있지 않습니다.");
            return;
        }

        string failReason;
        if (!effectManager.CanUseContentCardNow(card, BattleSlotOwner.My, out failReason))
        {
            SetSystemMessage(failReason);
            return;
        }

        if (!IsCardInHandFromExternal(BattleSlotOwner.My, card))
        {
            SetSystemMessage("손패에 있는 콘텐츠 카드만 사용할 수 있습니다.");
            return;
        }

        if (questionPanel == null)
        {
            SetSystemMessage("QuestionPanel이 BattleManager에 연결되어 있지 않습니다.");
            return;
        }

        ClearDraggingHandCard();
        ClearPendingSummonChoice();

        pendingContentCard = card;
        pendingContentHandIndex = selectedHandCardIndex;

        if (!questionPanel.TryShowYesNoQuestion(
            "콘텐츠 카드를 사용하시겠습니까?",
            ConfirmPendingContentUse,
            CancelPendingContentUse,
            CancelPendingContentUse
        ))
        {
            ClearPendingContentChoice();
            SetSystemMessage("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        SetSystemMessage($"{card.name} 콘텐츠 카드 사용을 확인해 주세요.");
    }

    private void OpenContentUseQuestion(BaseCardData card)
    {
        if (card == null)
            return;

        if (!IsContentCardKind(card))
        {
            SelectCard(card);
            return;
        }

        if (effectManager == null)
        {
            SetSystemMessage("EffectManager가 연결되어 있지 않습니다.");
            return;
        }

        string failReason;
        if (!effectManager.CanUseContentCardNow(card, BattleSlotOwner.My, out failReason))
        {
            SetSystemMessage(failReason);
            return;
        }

        if (!IsCardInHandFromExternal(BattleSlotOwner.My, card))
        {
            SetSystemMessage("손패에 있는 콘텐츠 카드만 사용할 수 있습니다.");
            return;
        }

        if (cardQuestionPanel == null)
        {
            SetSystemMessage("CardQuestionPanel이 BattleManager에 연결되어 있지 않습니다.");
            return;
        }

        ClearDraggingHandCard();
        ClearPendingSummonChoice();

        EffectContext context = new EffectContext
        {
            battleManager = this,
            actingOwner = BattleSlotOwner.My,
            timing = EffectTiming.Content,
            sourceCard = card,
            consumeAction = true
        };

        List<EffectCandidate> usableContentCandidates =
            effectManager.GetPlayableEffects(EffectTiming.Content, context);
        usableContentCandidates.RemoveAll(candidate =>
            candidate == null ||
            CanInstallAsFieldContentCard(candidate.card) ||
            IsCollabContentCard(candidate.card));

        if (usableContentCandidates.Count == 0)
        {
            ClearPendingContentChoice();
            SetSystemMessage("발동 가능한 콘텐츠 카드가 없습니다.");
            return;
        }

        List<CardQuestionOption> options = new List<CardQuestionOption>();

        foreach (EffectCandidate candidate in usableContentCandidates)
        {
            if (candidate == null || candidate.card == null)
                continue;

            options.Add(new CardQuestionOption(candidate.card, null, candidate));
        }

        if (!cardQuestionPanel.TryShowOptions(
            "발동할 카드를 선택하세요.",
            options,
            true,
            ConfirmSelectedContentUse,
            CancelPendingContentUse
        ))
        {
            ClearPendingContentChoice();
            SetSystemMessage("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        SelectCard(card);
        SetSystemMessage("발동할 콘텐츠 카드를 선택하세요.");
    }

    private void ConfirmSelectedContentUse(CardQuestionOption option)
    {
        BaseCardData card = option != null
            ? option.card
            : null;

        if (card == null)
        {
            ClearPendingContentChoice();
            SetSystemMessage("사용할 콘텐츠 카드 정보가 없습니다.");
            return;
        }

        if (effectManager == null)
        {
            ClearPendingContentChoice();
            SetSystemMessage("EffectManager가 연결되어 있지 않습니다.");
            return;
        }

        string failReason;
        if (!effectManager.CanUseContentCardNow(card, BattleSlotOwner.My, out failReason))
        {
            ClearPendingContentChoice();
            SetSystemMessage(failReason);
            return;
        }

        pendingContentCard = card;
        pendingContentHandIndex = option != null && option.linkedCandidate != null
            ? option.linkedCandidate.handIndex
            : FindHandCardIndexFromExternal(BattleSlotOwner.My, card);
        ConfirmPendingContentUse();
    }

    private void ConfirmPendingContentUse()
    {
        if (pendingContentCard == null)
        {
            ClearPendingContentChoice();
            SetSystemMessage("사용할 콘텐츠 카드 정보가 없습니다.");
            return;
        }

        BaseCardData card = pendingContentCard;
        int handIndex = pendingContentHandIndex;
        pendingContentCard = null;
        pendingContentHandIndex = -1;

        if (effectManager == null)
        {
            SetSystemMessage("EffectManager가 연결되어 있지 않습니다.");
            return;
        }

        EffectActivationRequest request = new EffectActivationRequest
        {
            sourceCard = card,
            owner = BattleSlotOwner.My,
            timing = EffectTiming.Content,
            sourceSlot = null,
            targetSlot = null,
            handIndex = handIndex,
            consumeAction = true
        };

        effectManager.TryActivateEffect(request);
    }

    private void CancelPendingContentUse()
    {
        string cardName = pendingContentCard != null
            ? pendingContentCard.name
            : "선택 카드";

        ClearPendingContentChoice();
        SetSystemMessage($"{cardName} 콘텐츠 카드 사용을 취소했습니다.");
    }

    private int GetContentCardCost(BaseCardData card)
    {
        ContentCardData content = card as ContentCardData;

        if (content == null)
            return 0;

        return Mathf.Max(0, content.cost);
    }

    private void OpenLastingContentInstallQuestion(BattleFieldSlot targetSlot, BaseCardData contentCard)
    {
        string failReason;
        if (!CanInstallLastingContentCard(targetSlot, contentCard, out failReason))
        {
            ClearDraggingHandCard();
            ClearPendingContentChoice();
            SetSystemMessage(failReason);
            return;
        }

        if (questionPanel == null)
        {
            ClearDraggingHandCard();
            ClearPendingContentChoice();
            SetSystemMessage("QuestionPanel이 BattleManager에 연결되어 있지 않습니다.");
            return;
        }

        pendingContentCard = contentCard;
        pendingContentInstallSlot = targetSlot;

        int cost = GetContentCardCost(contentCard);

        if (!questionPanel.TryShowYesNoQuestion(
            "지속형 콘텐츠 카드를 설치하시겠습니까?",
            ConfirmPendingContentInstall,
            CancelPendingContentInstall,
            CancelPendingContentInstall
        ))
        {
            ClearDraggingHandCard();
            ClearPendingContentChoice();
            SetSystemMessage("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        SetSystemMessage(
            $"{contentCard.name} 지속형 콘텐츠 카드를 설치하려 합니다.\n" +
            $"위치: ({targetSlot.x}, {targetSlot.y})\n" +
            $"시청자 -{cost}"
        );
    }

    private void ConfirmPendingContentInstall()
    {
        if (pendingContentInstallSlot == null || pendingContentCard == null)
        {
            ClearPendingContentChoice();
            SetSystemMessage("설치할 콘텐츠 카드 또는 슬롯 정보가 없습니다.");
            return;
        }

        BattleFieldSlot targetSlot = pendingContentInstallSlot;
        BaseCardData contentCard = pendingContentCard;

        pendingContentInstallSlot = null;
        pendingContentCard = null;

        InstallLastingContentCard(targetSlot, contentCard);
    }

    private void CancelPendingContentInstall()
    {
        string cardName = pendingContentCard != null
            ? pendingContentCard.name
            : "선택 카드";

        ClearPendingContentChoice();
        ClearDraggingHandCard();
        SetSystemMessage($"{cardName} 지속형 콘텐츠 카드 설치를 취소했습니다.");
    }

    private void InstallLastingContentCard(BattleFieldSlot targetSlot, BaseCardData contentCard)
    {
        string failReason;
        if (!CanInstallLastingContentCard(targetSlot, contentCard, out failReason))
        {
            ClearDraggingHandCard();
            ClearPendingSummonChoice();
            SetSystemMessage(failReason);
            return;
        }

        int cost = GetContentCardCost(contentCard);
        Sprite sprite = LoadCardSprite(contentCard);

        if (sprite == null)
        {
            ClearDraggingHandCard();
            ClearPendingSummonChoice();
            SetSystemMessage($"{contentCard.name} 카드 이미지를 찾을 수 없습니다.");
            return;
        }

        if (!TryPayViewerCostFromExternal(BattleSlotOwner.My, cost))
        {
            ClearDraggingHandCard();
            ClearPendingSummonChoice();
            SetSystemMessage("시청자가 부족하여 콘텐츠 카드를 설치할 수 없습니다.");
            return;
        }

        targetSlot.SetContentCard(contentCard, sprite, BattleSlotOwner.My);

        if (!RemoveCardFromHandFromExternal(BattleSlotOwner.My, contentCard))
        {
            ClearDraggingHandCard();
            ClearPendingSummonChoice();
            SetSystemMessage("내 손패에서 콘텐츠 카드를 제거할 수 없습니다.");
            return;
        }

        // TODO: Lasting 콘텐츠의 실제 효과, 버프/디버프, 지속 효과 계산은 별도 EffectManager 흐름으로 구현한다.
        ClearDraggingHandCard();
        ClearPendingSummonChoice();

        RefreshAllUI();

        ResolveMyActionUsed(
            $"{contentCard.name} 지속형 콘텐츠 카드를 설치했습니다.\n" +
            $"위치: ({targetSlot.x}, {targetSlot.y})\n" +
            $"시청자 -{cost}"
        );
    }

    private bool CanInstallLastingContentCard(
        BattleFieldSlot targetSlot,
        BaseCardData contentCard,
        out string failReason)
    {
        failReason = "";

        if (!CanUseMyAction(out failReason))
            return false;

        if (contentCard == null)
        {
            failReason = "설치할 콘텐츠 카드 정보가 없습니다.";
            return false;
        }

        if (!CanInstallAsFieldContentCard(contentCard))
        {
            failReason = "지속형 콘텐츠 카드만 필드에 설치할 수 있습니다.";
            return false;
        }

        if (!IsCardInHandFromExternal(BattleSlotOwner.My, contentCard))
        {
            failReason = "내 손패에 있는 콘텐츠 카드만 설치할 수 있습니다.";
            return false;
        }

        if (targetSlot == null)
        {
            failReason = "대상 슬롯이 없습니다.";
            return false;
        }

        if (targetSlot.owner != BattleSlotOwner.My)
        {
            failReason = "내 방송 슬롯에만 콘텐츠 카드를 설치할 수 있습니다.";
            return false;
        }

        if (!targetSlot.HasBroadcast)
        {
            failReason = "방송 카드가 설치된 슬롯에만 콘텐츠 카드를 설치할 수 있습니다.";
            return false;
        }

        if (targetSlot.HasContent)
        {
            failReason = "이미 콘텐츠 카드가 있는 슬롯입니다.";
            return false;
        }

        int cost = GetContentCardCost(contentCard);

        if (!CanPayViewerCostFromExternal(BattleSlotOwner.My, cost))
        {
            failReason = "시청자가 부족하여 콘텐츠 카드를 설치할 수 없습니다.";
            return false;
        }

        return true;
    }

    private bool IsContentCardKind(BaseCardData card)
    {
        if (card == null || string.IsNullOrEmpty(card.kind))
            return false;

        return string.Equals(card.kind, "Content", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(card.kind, "Contents", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsCharacterCardKind(BaseCardData card)
    {
        if (card == null || string.IsNullOrEmpty(card.kind))
            return false;

        return string.Equals(card.kind, "Character", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsLastingContentCard(BaseCardData card)
    {
        ContentCardData content = card as ContentCardData;

        if (content == null || !IsContentCardKind(card))
            return false;

        return string.Equals(content.contentType, "Lasting", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanInstallAsFieldContentCard(BaseCardData card)
    {
        return IsLastingContentCard(card) &&
            !HasPrimaryContentEffectRef(card, "content.lockBroadcastIdNoMoveNoKOUntilNextEnd");
    }

    private bool HasPrimaryContentEffectRef(BaseCardData card, string effectRef)
    {
        ContentCardData content = card as ContentCardData;

        if (content == null ||
            content.effects == null ||
            string.IsNullOrWhiteSpace(effectRef))
        {
            return false;
        }

        foreach (EffectData effect in content.effects)
        {
            if (effect == null)
                continue;

            string candidateRef = GetEffectRefForBattleManager(effect);
            if (string.Equals(candidateRef, effectRef, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool IsCollabContentCard(BaseCardData card)
    {
        ContentCardData content = card as ContentCardData;

        if (content == null || !IsContentCardKind(card))
            return false;

        return string.Equals(content.contentType, "Collab", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsPreCollabContentCard(BaseCardData card)
    {
        ContentCardData content = card as ContentCardData;

        if (content == null || !IsContentCardKind(card))
            return false;

        if (string.Equals(content.contentType, "Collab", StringComparison.OrdinalIgnoreCase))
            return HasContentEffectTiming(content, "PreCollab");

        return HasContentEffectTiming(content, "PreCollab");
    }

    private bool HasContentEffectTiming(ContentCardData content, string timing)
    {
        if (content == null || content.effects == null || string.IsNullOrEmpty(timing))
            return false;

        foreach (EffectData effect in content.effects)
        {
            if (effect == null || string.IsNullOrEmpty(effect.timing))
                continue;

            if (string.Equals(effect.timing, timing, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
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

        bool isCharacterCard = IsCharacterCardKind(card);
        bool isInstallableContentCard = CanInstallAsFieldContentCard(card);

        if (!isCharacterCard && !isInstallableContentCard)
            return false;

        if (isCharacterCard && summonManager == null)
            return false;

        if (questionPanel != null && questionPanel.IsOpen())
            return false;

        if (cardQuestionPanel != null && cardQuestionPanel.IsOpen())
            return false;

        if (summonManager != null && summonManager.HasPendingSummonChoice)
            return false;

        if (summonManager != null && summonManager.HasPendingFlipChoice)
            return false;

        return true;
    }

    private bool CanPotentiallyDragHandCard(BaseCardData card)
    {
        if (card == null)
            return false;

        return IsCharacterCardKind(card) || CanInstallAsFieldContentCard(card);
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

        if (IsCharacterCardKind(card))
        {
            SetSystemMessage(
                $"캐릭터 카드 드래그 시작: {card.name}\n" +
                "카드를 출연할 방송 슬롯 위로 이동하세요."
            );
        }
        else if (CanInstallAsFieldContentCard(card))
        {
            SetSystemMessage(
                $"지속형 콘텐츠 카드 드래그 시작: {card.name}\n" +
                "카드를 설치할 방송 슬롯 위로 이동하세요."
            );
        }
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

        bool hasPendingSummonChoice =
            summonManager != null && summonManager.HasPendingSummonChoice;
        bool hasPendingContentInstallChoice =
            pendingContentInstallSlot != null && pendingContentCard != null;

        ClearDraggingHandCard();
        DestroyDragPreview();

        if (hasPendingSummonChoice || hasPendingContentInstallChoice)
            return;

        if (CanInstallAsFieldContentCard(card))
        {
            SetSystemMessage(
                $"지속형 콘텐츠 카드 드래그 종료: {cardName}\n" +
                "설치할 슬롯 위에 카드를 내려놓지 않았습니다."
            );
            return;
        }

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

    private void ApplyHandCardSelectionHighlight(GameObject itemObject, int handIndex)
    {
        if (itemObject == null)
            return;

        Outline outline = itemObject.GetComponent<Outline>();

        if (outline == null)
            outline = itemObject.AddComponent<Outline>();

        outline.effectColor = selectedHandCardOutlineColor;
        outline.effectDistance = selectedHandCardOutlineDistance;
        outline.useGraphicAlpha = false;
        outline.enabled = selectedHandCardIndex >= 0 && handIndex == selectedHandCardIndex;
    }

    private void RefreshHandSelectionHighlights()
    {
        if (myHandPanel == null || myPlayer == null || myPlayer.hand == null)
            return;

        int index = 0;

        foreach (Transform child in myHandPanel)
        {
            if (child == null)
                continue;

            ApplyHandCardSelectionHighlight(child.gameObject, index);
            index++;
        }
    }

    private void SelectHandCard(BaseCardData card, int handIndex)
    {
        selectedHandCardIndex = handIndex;
        SelectCard(card, true);
    }

    private void SelectCard(BaseCardData card, bool keepHandSelection = false)
    {
        if (card == null)
            return;

        if (IsGameOver() || IsBattleBusy())
            return;

        selectedCard = card;

        if (!keepHandSelection)
            selectedHandCardIndex = -1;

        if (cardDetailPanel != null)
            cardDetailPanel.ShowCard(card);

        RefreshHandSelectionHighlights();
        Debug.Log($"선택 카드: {card.name}");
    }

    private void ClearSelectedHandCard()
    {
        selectedHandCardIndex = -1;
        RefreshHandSelectionHighlights();
    }

    private void SelectFieldCharacter(BattleFieldSlot slot)
    {
        if (slot == null || slot.characterCard == null)
            return;

        if (IsGameOver() || IsBattleBusy())
            return;

        selectedCard = slot.characterCard;
        selectedHandCardIndex = -1;

        if (cardDetailPanel != null)
            cardDetailPanel.ShowFieldCharacter(slot);

        RefreshHandSelectionHighlights();
        Debug.Log($"선택 카드: {slot.characterCard.name}");
    }

    private void ShowCardQuestionDetailPreview(BaseCardData card, BattleFieldSlot linkedSlot)
    {
        if (cardDetailPanel == null || card == null)
            return;

        if (linkedSlot != null &&
            linkedSlot.HasCharacter &&
            linkedSlot.characterCard == card)
        {
            cardDetailPanel.ShowFieldCharacter(linkedSlot);
            return;
        }

        cardDetailPanel.ShowCard(card);
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
        bool rotate180 = false,
        Action<BaseCardData> doubleClickAction = null)
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
            SetRuntimeImageButton(cardImage, card, clickAction, doubleClickAction);
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
        Action<BaseCardData> clickAction,
        Action<BaseCardData> doubleClickAction = null)
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
            button.onClick.AddListener(() =>
            {
                if (doubleClickAction == null)
                {
                    clickAction(capturedCard);
                    return;
                }

                if (Time.unscaledTime - lastIdolClickTime <= 0.32f)
                {
                    lastIdolClickTime = -10f;
                    doubleClickAction(capturedCard);
                    return;
                }

                lastIdolClickTime = Time.unscaledTime;
                clickAction(capturedCard);
            });
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

        SetZoneCardVisual(slot, card, false, cardToSelect => SelectCard(cardToSelect));
        SetZoneLabel(slot, card.name);
        SetSystemMessage($"{card.name} 카드를 슬롯에 배치했습니다.");
    }

    private void OnClickTurnEndButton()
    {
        string inputFailReason;
        if (IsInputBlocked(out inputFailReason))
        {
            SetSystemMessage(inputFailReason);
            return;
        }

        if (questionPanel != null && questionPanel.IsOpen())
        {
            SetSystemMessage("이미 다른 선택창이 열려 있습니다.");
            return;
        }

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

        if (questionPanel == null)
        {
            SetSystemMessage("QuestionPanel이 BattleManager에 연결되어 있지 않습니다.");
            return;
        }

        if (!questionPanel.TryShowYesNoQuestion(
            "행동을 종료하시겠습니까?",
            ConfirmTurnEnd,
            CancelTurnEnd,
            CancelTurnEnd
        ))
        {
            SetSystemMessage("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        RefreshTurnEndButtonState();
        SetSystemMessage("행동 종료를 확인해 주세요.");
    }

    private void ConfirmTurnEnd()
    {
        StartCoroutine(EndPlayerActionRoutine());
    }

    private void CancelTurnEnd()
    {
        RefreshTurnEndButtonState();
        SetSystemMessage("행동 종료를 취소했습니다.");
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
        BattlePlayerRuntime player = GetPlayerRuntime(characterOwner);
        int baseGain = GetIdolBaseViewersPerPrep(player);
        int totalGain = baseGain;

        AddPrepViewerGainFromSlots(myBattleSlots, characterOwner, baseGain, ref totalGain);
        AddPrepViewerGainFromSlots(enemyBattleSlots, characterOwner, baseGain, ref totalGain);
        totalGain += CalculatePrepViewerPassiveBonus(characterOwner);

        return Mathf.Max(0, totalGain);
    }

    private void AddPrepViewerGainFromSlots(
        List<BattleFieldSlot> slots,
        BattleSlotOwner characterOwner,
        int baseGain,
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

            int slotGain = baseGain;
            slotGain += CalculateBroadcastPrepViewerModifier(slot);

            totalGain += slotGain;
        }
    }

    private int CalculateBroadcastPrepViewerModifier(BattleFieldSlot slot)
    {
        if (slot == null || slot.broadcastCard == null)
            return 0;

        BroadcastCardData broadcast = slot.broadcastCard as BroadcastCardData;

        if (broadcast == null)
            return 0;

        bool handledByEffectRef = false;
        int modifier = 0;

        if (broadcast.effects != null)
        {
            foreach (EffectData effect in broadcast.effects)
            {
                string effectRef = GetEffectRefForBattleManager(effect);

                if (string.Equals(effectRef, "broadcast.always.prepViewersAndOccupantHpDelta", StringComparison.OrdinalIgnoreCase))
                {
                    handledByEffectRef = true;
                    modifier += ApplyNegativeAmountInvertForEffectSource(
                        slot.owner,
                        broadcast,
                        GetEffectIntParamForBattleManager(effect, "viewersModifier", 0));
                }
                else if (string.Equals(effectRef, "broadcast.always.taggedOccupantPrepViewersBonus", StringComparison.OrdinalIgnoreCase))
                {
                    handledByEffectRef = true;
                    modifier += ApplyNegativeAmountInvertForEffectSource(
                        slot.owner,
                        broadcast,
                        CalculateTaggedOccupantPrepViewerBonus(slot, effect));
                }
                else if (string.Equals(effectRef, "broadcast.always.prepViewersAndHealBonus", StringComparison.OrdinalIgnoreCase))
                {
                    handledByEffectRef = true;
                    modifier += ApplyNegativeAmountInvertForEffectSource(
                        slot.owner,
                        broadcast,
                        GetEffectIntParamForBattleManager(effect, "viewersModifier", 0));
                }
            }
        }

        if (!handledByEffectRef)
        {
            modifier += ApplyNegativeAmountInvertForEffectSource(
                slot.owner,
                broadcast,
                GetBroadcastViewersModifier(broadcast));
        }

        return modifier;
    }

    private int CalculateTaggedOccupantPrepViewerBonus(BattleFieldSlot slot, EffectData effect)
    {
        if (slot == null ||
            !slot.HasCharacter ||
            slot.isCharacterFaceDown ||
            slot.characterCard == null)
        {
            return 0;
        }

        string tag = GetEffectStringParamForBattleManager(effect, "tag", "");

        if (string.IsNullOrEmpty(tag) || !CardHasHashtag(slot.characterCard, tag))
            return 0;

        return GetEffectIntParamForBattleManager(effect, "amount", 0);
    }

    private int CalculatePrepViewerPassiveBonus(BattleSlotOwner characterOwner)
    {
        int bonus = 0;

        AddPrepViewerPassiveBonusFromSlots(myBattleSlots, characterOwner, ref bonus);
        AddPrepViewerPassiveBonusFromSlots(enemyBattleSlots, characterOwner, ref bonus);

        return bonus;
    }

    private void AddPrepViewerPassiveBonusFromSlots(
        List<BattleFieldSlot> slots,
        BattleSlotOwner characterOwner,
        ref int bonus)
    {
        if (slots == null)
            return;

        foreach (BattleFieldSlot slot in slots)
        {
            if (!IsFaceUpCharacterOwnedBy(slot, characterOwner))
                continue;

            CharacterCardData character = slot.characterCard as CharacterCardData;

            if (character == null || character.effects == null)
                continue;

            foreach (EffectData effect in character.effects)
            {
                string effectRef = GetEffectRefForBattleManager(effect);

                if (string.Equals(effectRef, "character.passive.viewersBonusIfAdjacentToTag", StringComparison.OrdinalIgnoreCase))
                {
                    string tag = GetEffectStringParamForBattleManager(effect, "tag", "");

                    if (!HasAdjacentFaceUpOwnedCharacterWithTag(slot, characterOwner, tag))
                        continue;

                    int amount = GetEffectIntParamForBattleManager(effect, "amount", 0);
                    amount = ApplyNegativeAmountInvertForEffectSource(characterOwner, character, amount);
                    bonus += amount;
                    Debug.Log($"{character.name} 패시브: {FormatSignedAmount(amount)}");
                }
                else if (string.Equals(effectRef, "character.passive.reduceOwnerPrepViewers", StringComparison.OrdinalIgnoreCase))
                {
                    int amount = GetEffectIntParamForBattleManager(effect, "amount", 0);
                    amount = ApplyNegativeAmountInvertForEffectSource(characterOwner, character, amount);
                    bonus += amount;
                    Debug.Log($"{character.name} 패시브: {FormatSignedAmount(amount)}");
                }
            }
        }
    }

    private int ApplyNegativeAmountInvertForEffectSource(
        BattleSlotOwner owner,
        BaseCardData sourceCard,
        int amount)
    {
        if (effectManager == null)
            return amount;

        return effectManager.ApplyNegativeAmountInvertIfNeeded(owner, sourceCard, amount);
    }

    private bool HasAdjacentFaceUpOwnedCharacterWithTag(
        BattleFieldSlot sourceSlot,
        BattleSlotOwner characterOwner,
        string tag)
    {
        if (sourceSlot == null || string.IsNullOrEmpty(tag))
            return false;

        foreach (BattleFieldSlot slot in GetBattleSlots(sourceSlot.owner))
        {
            if (!IsFaceUpCharacterOwnedBy(slot, characterOwner))
                continue;

            if (!AreSlotsOrthogonallyAdjacentOnSameField(sourceSlot, slot))
                continue;

            if (CardHasHashtag(slot.characterCard, tag))
                return true;
        }

        return false;
    }

    private bool IsFaceUpCharacterOwnedBy(BattleFieldSlot slot, BattleSlotOwner characterOwner)
    {
        return slot != null &&
            slot.HasCharacter &&
            !slot.isCharacterFaceDown &&
            slot.characterOwner == characterOwner;
    }

    private bool AreSlotsOrthogonallyAdjacentOnSameField(BattleFieldSlot sourceSlot, BattleFieldSlot targetSlot)
    {
        if (sourceSlot == null || targetSlot == null || sourceSlot == targetSlot)
            return false;

        if (sourceSlot.owner != targetSlot.owner)
            return false;

        int distance =
            Mathf.Abs(sourceSlot.x - targetSlot.x) +
            Mathf.Abs(sourceSlot.y - targetSlot.y);

        return distance == 1;
    }

    private string FormatSignedAmount(int amount)
    {
        return amount >= 0 ? $"+{amount}" : amount.ToString();
    }

    private List<BattleFieldSlot> GetBattleSlots(BattleSlotOwner owner)
    {
        return owner == BattleSlotOwner.My ? myBattleSlots : enemyBattleSlots;
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
        Debug.Log(message);

        string displayMessage = BuildShortSystemMessage(message);

        if (string.IsNullOrEmpty(displayMessage))
            return;

        if (simpleMessagePanel != null)
        {
            simpleMessagePanel.Show(displayMessage);
            return;
        }
    }

    private IEnumerator PlaySystemMessageRoutine(
        string message,
        SimpleMessageExitDirection exitDirection)
    {
        Debug.Log(message);

        string displayMessage = BuildShortSystemMessage(message);

        if (string.IsNullOrEmpty(displayMessage))
            yield break;

        if (simpleMessagePanel != null)
        {
            float visibleTime = exitDirection == SimpleMessageExitDirection.None
                ? systemMessageVisibleTime
                : actionTransferMessageVisibleTime;

            bool nonInterruptible = exitDirection != SimpleMessageExitDirection.None;
            simpleMessagePanel.Play(displayMessage, exitDirection, visibleTime, nonInterruptible);
            yield return WaitForSimpleMessageDuration(visibleTime);
            yield break;
        }
    }

    private IEnumerator PlayTurnIntroRoutine(int turnNumber)
    {
        yield return PlaySimplePanelMessageRoutine(
            $"Turn {Mathf.Max(1, turnNumber)}",
            SimpleMessageExitDirection.LeftToRight
        );
    }

    private IEnumerator PlaySimplePanelMessageRoutine(
        string message,
        SimpleMessageExitDirection exitDirection)
    {
        Debug.Log(message);

        if (string.IsNullOrWhiteSpace(message))
            yield break;

        if (simpleMessagePanel == null)
            yield break;

        simpleMessagePanel.Play(
            message,
            exitDirection,
            actionTransferMessageVisibleTime,
            true
        );

        yield return WaitForSimpleMessageDuration(actionTransferMessageVisibleTime);
    }

    private IEnumerator WaitForSimpleMessageDuration(float visibleTime)
    {
        float totalTime =
            Mathf.Max(0f, systemMessageFadeTime) +
            Mathf.Max(0f, visibleTime) +
            Mathf.Max(0f, systemMessageFadeTime) +
            0.1f;

        if (totalTime > 0f)
            yield return new WaitForSecondsRealtime(totalTime);
    }

    private void ResolveSimpleMessagePanel()
    {
        GameObject simplePanelObject = null;

        if (simpleMessagePanel != null)
            simplePanelObject = simpleMessagePanel.gameObject;

        if (simplePanelObject == null)
            simplePanelObject = FindSceneGameObjectByName("SimpleMessagePanel");

        if (simplePanelObject == null)
        {
            Debug.LogWarning("SimpleMessagePanel을 찾지 못했습니다. 화면 메시지는 Debug.Log에만 출력됩니다.");
            return;
        }

        CanvasGroup simpleCanvasGroup = simplePanelObject.GetComponent<CanvasGroup>();

        if (simpleCanvasGroup == null)
            simpleCanvasGroup = simplePanelObject.AddComponent<CanvasGroup>();

        if (simpleMessagePanel == null)
            simpleMessagePanel = simplePanelObject.GetComponent<SimpleMessagePanelController>();

        if (simpleMessagePanel == null)
            simpleMessagePanel = simplePanelObject.AddComponent<SimpleMessagePanelController>();

        if (simpleMessagePanel != null)
        {
            simpleMessagePanel.Configure(null, simpleCanvasGroup);
            simpleMessagePanel.SetTimings(
                systemMessageFadeTime,
                systemMessageVisibleTime,
                systemMessageFadeTime
            );
        }
    }

    private string BuildShortSystemMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "";

        string[] messageLines = message
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        string prioritizedFailureMessage = TryBuildActionFailureSimpleMessage(messageLines);
        if (!string.IsNullOrEmpty(prioritizedFailureMessage))
            return prioritizedFailureMessage;

        string firstLine = messageLines.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine))
            return "";

        firstLine = firstLine.Trim();

        if (firstLine == "상대의 행동 차례입니다." ||
            firstLine == "당신의 행동 차례입니다.")
        {
            return firstLine;
        }

        if (ContainsAny(
            firstLine,
            "선택 카드:",
            "카드 확인:",
            "행동 종료를 확인",
            "행동 종료를 취소",
            "행동을 취소",
            "카드를 드래그",
            "드래그 중",
            "연결되어 있지",
            "미구현",
            "TODO",
            "테스트",
            "Debug",
            "Cheat"))
        {
            return "";
        }

        if (firstLine.Contains("시청자가 부족"))
            return "시청자가 부족합니다.";

        if (ContainsAny(
            firstLine,
            "대상을 찾지",
            "대상이 없습니다",
            "대상이 없",
            "대상 카드 정보가 없습니다",
            "대상 슬롯이 없습니다",
            "슬롯 정보가 없습니다",
            "카드 정보가 없습니다",
            "캐릭터 정보가 없습니다",
            "캐릭터 카드가 없습니다",
            "발동 가능한 콘텐츠 카드가 없습니다"))
        {
            return "대상이 없습니다.";
        }

        if (firstLine.Contains("이번 턴에는 이미 뒷면 출연"))
            return "이번 턴에는 이미 뒷면 출연했습니다.";

        if (ContainsAny(firstLine, "이미 앞면 상태", "이미 캐릭터가", "이미 아군 캐릭터", "이미 방송 카드"))
            return "이미 사용 중인 자리입니다.";

        if (firstLine.Contains("이미 다른 선택창"))
            return "선택창이 열려 있습니다.";

        if (firstLine.Contains("이미 카드 선택창"))
            return "카드 선택창이 열려 있습니다.";

        if (firstLine.Contains("이미 배틀이 종료"))
            return "이미 배틀이 종료되었습니다.";

        if (firstLine.Contains("현재 다른 처리를"))
            return "처리 중입니다.";

        if (firstLine.Contains("처리 상태를 초기화"))
            return "처리 상태를 초기화했습니다.";

        if (firstLine.Contains("퇴장 효과를 처리"))
            return "퇴장 효과 처리 중입니다.";

        if (firstLine.Contains("합방 처리를 진행"))
            return "합방 처리 중입니다.";

        if (firstLine.Contains("현재는 내 행동권"))
            return "내 행동권이 아닙니다.";

        if (firstLine.Contains("아직 본게임"))
            return "아직 본게임이 아닙니다.";

        if (firstLine.Contains("아직 배틀 준비"))
            return "아직 배틀 준비 중입니다.";

        if (firstLine.Contains("뒷면 출연한 턴"))
            return "출연한 턴에는 할 수 없습니다.";

        if (ContainsAny(firstLine, "출연한 턴에는", "이번 턴에 더 이상"))
            return "이번 턴에는 할 수 없습니다.";

        if (firstLine.Contains("상대의 뒷면 캐릭터"))
            return "상대의 뒷면 카드입니다.";

        if (firstLine.Contains("카드 이미지를 찾"))
            return "카드 이미지를 찾지 못했습니다.";

        if (ContainsAny(firstLine, "할 수 없습니다", "할 수 없", "불가능", "불가"))
            return firstLine;

        if (ContainsAny(firstLine, "없습니다", "아닙니다"))
            return "";

        return "";
    }

    private string TryBuildActionFailureSimpleMessage(string[] messageLines)
    {
        if (messageLines == null || messageLines.Length == 0)
            return "";

        foreach (string line in messageLines)
        {
            string normalized = NormalizeActionFailureMessage(line);
            if (!string.IsNullOrEmpty(normalized))
                return normalized;
        }

        return "";
    }

    private string NormalizeActionFailureMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "";

        string value = message.Trim();

        if (value.Contains("뒷면 캐릭터") && value.Contains("이동"))
            return "뒷면 캐릭터는 이동할 수 없습니다.";

        if (value.Contains("이번 턴") && value.Contains("이동"))
            return "이 캐릭터는 이번 턴 이동할 수 없습니다.";

        if (value.Contains("효과") && value.Contains("이동") && ContainsAny(value, "할 수 없습니다", "할 수 없", "불가"))
            return "효과로 인해 이동할 수 없습니다.";

        if (value.Contains("뒷면 캐릭터") && value.Contains("효과"))
            return "뒷면 캐릭터는 효과를 발동할 수 없습니다.";

        if (value.Contains("효과는 무효화"))
            return "이 캐릭터의 효과는 무효화되어 있습니다.";

        if (value.Contains("채팅 밴") && value.Contains("합방 효과"))
            return "이 캐릭터의 효과는 무효화되어 있습니다.";

        if (value.Contains("효과를 발동할 수 없습니다") ||
            value.Contains("액티브 효과를 사용"))
        {
            return "이 캐릭터는 효과를 발동할 수 없습니다.";
        }

        if (value.Contains("뒷면 캐릭터") && value.Contains("합방"))
            return "이 캐릭터는 합방을 시도할 수 없습니다.";

        if (value.Contains("합방을 시작할 수 없습니다") ||
            value.Contains("합방을 시도할 수 없습니다"))
        {
            if (value.Contains("효과") || value.Contains("다음 턴까지"))
                return "효과로 인해 이번 턴 합방할 수 없습니다.";

            return "이 캐릭터는 합방을 시도할 수 없습니다.";
        }

        if (value.Contains("현재 타이밍에 발동할 수 없는 카드입니다"))
            return "현재 타이밍에 발동할 수 없는 카드입니다.";

        return "";
    }

    private bool ContainsAny(string value, params string[] patterns)
    {
        if (string.IsNullOrEmpty(value) || patterns == null)
            return false;

        foreach (string pattern in patterns)
        {
            if (!string.IsNullOrEmpty(pattern) && value.Contains(pattern))
                return true;
        }

        return false;
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
