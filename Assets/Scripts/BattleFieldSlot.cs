using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum BattleSlotOwner
{
    My,
    Enemy
}

public class BattleFieldSlot : MonoBehaviour,
    IDropHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("Slot Info")]
    public BattleSlotOwner owner;
    public int x;
    public int y;

    [Header("Setup Button")]
    public Button setupButton;
    public TMP_Text setupButtonText;

    [Header("Card Buttons")]
    public Button broadcastCardButton;
    public Button characterCardButton;
    public Button contentCardButton;

    [Header("Card Images")]
    public Image broadcastCardImage;
    public Image characterCardImage;
    public Image contentCardImage;

    [Header("Runtime Cards")]
    public BaseCardData broadcastCard;
    public BaseCardData characterCard;
    public BaseCardData contentCard;

    [Header("Runtime States")]
    public bool isCharacterFaceDown = false;
    public bool characterMovedThisTurn { get; private set; }
    public int faceDownSummonedTurn = -1;

    private Action<BattleFieldSlot> onSetupButtonClick;
    private Action<BattleFieldSlot, BaseCardData> onBroadcastCardClick;
    private Action<BattleFieldSlot, BaseCardData> onCharacterCardClick;
    private Action<BattleFieldSlot, BaseCardData> onContentCardClick;

    private Action<BattleFieldSlot, PointerEventData> onCardDropped;

    private Action<BattleFieldSlot, BaseCardData, PointerEventData> onBeginDragCharacter;
    private Action<BattleFieldSlot, BaseCardData, PointerEventData> onDragCharacter;
    private Action<BattleFieldSlot, BaseCardData, PointerEventData> onEndDragCharacter;

    private Image rootRaycastImage;
    private Color normalRootColor = new Color(1f, 1f, 1f, 0.01f);

    public bool HasBroadcast => broadcastCard != null;
    public bool HasCharacter => characterCard != null;
    public bool HasContent => contentCard != null;

    public void Init(
        Action<BattleFieldSlot> setupClickAction,
        Action<BattleFieldSlot, BaseCardData> broadcastClickAction,
        Action<BattleFieldSlot, BaseCardData> characterClickAction,
        Action<BattleFieldSlot, BaseCardData> contentClickAction,
        Action<BattleFieldSlot, PointerEventData> dropAction = null,
        Action<BattleFieldSlot, BaseCardData, PointerEventData> beginDragCharacterAction = null,
        Action<BattleFieldSlot, BaseCardData, PointerEventData> dragCharacterAction = null,
        Action<BattleFieldSlot, BaseCardData, PointerEventData> endDragCharacterAction = null)
    {
        onSetupButtonClick = setupClickAction;
        onBroadcastCardClick = broadcastClickAction;
        onCharacterCardClick = characterClickAction;
        onContentCardClick = contentClickAction;
        onCardDropped = dropAction;

        onBeginDragCharacter = beginDragCharacterAction;
        onDragCharacter = dragCharacterAction;
        onEndDragCharacter = endDragCharacterAction;

        EnsureDropRaycastTarget();

        SetupInstallButton();
        SetupCardButtons();

        SetSetupButtonVisible(false);
        SetMoveHighlightVisible(false);
        RefreshVisualState();
    }

    private void EnsureDropRaycastTarget()
    {
        Image rootImage = GetComponent<Image>();

        if (rootImage == null)
        {
            rootImage = gameObject.AddComponent<Image>();
            rootImage.color = normalRootColor;
        }

        rootRaycastImage = rootImage;
        rootRaycastImage.raycastTarget = true;

        normalRootColor = rootRaycastImage.color;

        if (normalRootColor.a <= 0f)
            normalRootColor = new Color(1f, 1f, 1f, 0.01f);
    }

    private void SetupInstallButton()
    {
        if (setupButton == null)
        {
            Transform found = transform.Find("SetupButton");

            if (found != null)
                setupButton = found.GetComponent<Button>();
        }

        if (setupButton == null)
        {
            GameObject setupObject = new GameObject(
                "SetupButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );

            setupObject.transform.SetParent(transform, false);

            RectTransform rect = setupObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(110f, 36f);

            Image image = setupObject.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.85f);
            image.raycastTarget = true;

            setupButton = setupObject.GetComponent<Button>();

            GameObject textObject = new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );

            textObject.transform.SetParent(setupObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            setupButtonText = textObject.GetComponent<TMP_Text>();
            setupButtonText.text = "방송 배치";
            setupButtonText.alignment = TextAlignmentOptions.Center;
            setupButtonText.fontSize = 18f;
            setupButtonText.color = Color.black;
        }

        if (setupButtonText == null && setupButton != null)
            setupButtonText = setupButton.GetComponentInChildren<TMP_Text>();

        if (setupButtonText != null)
            setupButtonText.text = "방송 배치";

        setupButton.onClick.RemoveAllListeners();
        setupButton.onClick.AddListener(OnClickSetupButton);
    }

    private void SetupCardButtons()
    {
        if (broadcastCardButton != null)
        {
            broadcastCardButton.onClick.RemoveAllListeners();
            broadcastCardButton.onClick.AddListener(OnClickBroadcastCard);
        }

        if (characterCardButton != null)
        {
            characterCardButton.onClick.RemoveAllListeners();
            characterCardButton.onClick.AddListener(OnClickCharacterCard);
        }

        if (contentCardButton != null)
        {
            contentCardButton.onClick.RemoveAllListeners();
            contentCardButton.onClick.AddListener(OnClickContentCard);
        }
    }

    public void SetSetupButtonVisible(bool value)
    {
        if (setupButton == null)
            return;

        setupButton.gameObject.SetActive(value);
        setupButton.interactable = value;
    }

    public void SetMoveHighlightVisible(bool value)
    {
        if (rootRaycastImage == null)
            EnsureDropRaycastTarget();

        if (rootRaycastImage == null)
            return;

        if (value)
        {
            rootRaycastImage.color = new Color(0.2f, 1f, 0.35f, 0.35f);
        }
        else
        {
            rootRaycastImage.color = normalRootColor;
        }
    }
    public void SetBroadcastCard(BaseCardData card, Sprite sprite)
    {
        broadcastCard = card;

        if (broadcastCardImage != null)
        {
            broadcastCardImage.sprite = sprite;
            broadcastCardImage.preserveAspect = true;
        }

        RefreshVisualState();
    }

    public void SetCharacterCard(BaseCardData card, Sprite sprite, bool faceDown = false)
    {
        characterCard = card;
        isCharacterFaceDown = faceDown;
        characterMovedThisTurn = false;

        if (!faceDown)
        faceDownSummonedTurn = -1;

        if (characterCardImage != null)
        {
            characterCardImage.sprite = sprite;
            characterCardImage.preserveAspect = true;
        }

        RefreshVisualState();
    }

    public void SetContentCard(BaseCardData card, Sprite sprite)
    {
        contentCard = card;

        if (contentCardImage != null)
        {
            contentCardImage.sprite = sprite;
            contentCardImage.preserveAspect = true;
        }

        RefreshVisualState();
    }

    public void ClearBroadcastCard()
    {
        broadcastCard = null;

        if (broadcastCardImage != null)
            broadcastCardImage.sprite = null;

        RefreshVisualState();
    }

    public void ClearCharacterCard()
    {
        characterCard = null;
        isCharacterFaceDown = false;
        faceDownSummonedTurn = -1;
        characterMovedThisTurn = false;

        if (characterCardImage != null)
        {
            characterCardImage.sprite = null;
            characterCardImage.enabled = false;
        }

        if (characterCardButton != null)
            characterCardButton.gameObject.SetActive(false);
    }

    public void ClearContentCard()
    {
        contentCard = null;

        if (contentCardImage != null)
            contentCardImage.sprite = null;

        RefreshVisualState();
    }

    public void ClearAllCards()
    {
        broadcastCard = null;
        characterCard = null;
        contentCard = null;
        faceDownSummonedTurn = -1;

        isCharacterFaceDown = false;

        if (broadcastCardImage != null)
            broadcastCardImage.sprite = null;

        if (characterCardImage != null)
            characterCardImage.sprite = null;

        if (contentCardImage != null)
            contentCardImage.sprite = null;

        SetMoveHighlightVisible(false);
        RefreshVisualState();
    }

    public void RefreshVisualState()
    {
        if (broadcastCardButton != null)
            broadcastCardButton.gameObject.SetActive(broadcastCard != null);

        if (characterCardButton != null)
            characterCardButton.gameObject.SetActive(characterCard != null);

        if (contentCardButton != null)
            contentCardButton.gameObject.SetActive(contentCard != null);

        RefreshCardRotation();
    }

    private void RefreshCardRotation()
    {
        bool isEnemySlot = owner == BattleSlotOwner.Enemy;

        if (characterCardButton != null)
        {
            RectTransform rect = characterCardButton.GetComponent<RectTransform>();
            if (rect != null)
                rect.localEulerAngles = isEnemySlot ? new Vector3(0f, 0f, 180f) : Vector3.zero;
        }

        if (contentCardButton != null)
        {
            RectTransform rect = contentCardButton.GetComponent<RectTransform>();
            if (rect != null)
                rect.localEulerAngles = isEnemySlot ? new Vector3(0f, 0f, 180f) : Vector3.zero;
        }

        // 방송 카드는 플랫폼 역할이므로 회전하지 않습니다.
        if (broadcastCardButton != null)
        {
            RectTransform rect = broadcastCardButton.GetComponent<RectTransform>();
            if (rect != null)
                rect.localEulerAngles = Vector3.zero;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        onCardDropped?.Invoke(this, eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (characterCard == null)
            return;

        onBeginDragCharacter?.Invoke(this, characterCard, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (characterCard == null)
            return;

        onDragCharacter?.Invoke(this, characterCard, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (characterCard == null)
            return;

        onEndDragCharacter?.Invoke(this, characterCard, eventData);
    }
    private void OnClickSetupButton()
    {
        onSetupButtonClick?.Invoke(this);
    }

    private void OnClickBroadcastCard()
    {
        if (broadcastCard == null)
            return;

        onBroadcastCardClick?.Invoke(this, broadcastCard);
    }

    private void OnClickCharacterCard()
    {
        if (characterCard == null)
            return;

        onCharacterCardClick?.Invoke(this, characterCard);
    }

    private void OnClickContentCard()
    {
        if (contentCard == null)
            return;

        onContentCardClick?.Invoke(this, contentCard);
    }

    public Sprite GetCurrentCharacterSprite()
    {
        if (characterCardImage == null)
            return null;

        return characterCardImage.sprite;
    }

    public void SetCharacterMovedThisTurn(bool value)
    {
        characterMovedThisTurn = value;
    }
}