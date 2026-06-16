using System;
using System.Collections;
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
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("Slot Info")]
    public BattleSlotOwner owner;
    public int x;
    public int y;

    public string GetSlotId()
    {
        return $"{owner}_{x}_{y}";
    }

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

    [Header("Question Target Highlight")]
    [Tooltip("카드 질문 패널에서 필드 대상 후보를 표시할 별도 오브젝트입니다. 비워두면 슬롯 배경 색상으로 표시합니다.")]
    public GameObject questionTargetHighlight;

    [Header("Runtime Cards")]
    public BaseCardData broadcastCard;
    public BaseCardData characterCard;
    public BaseCardData contentCard;

    [Header("Runtime States")]
    public bool isCharacterFaceDown = false;
    public bool characterMovedThisTurn { get; private set; }
    public bool characterActiveUsedThisTurn { get; private set; }
    public BattleSlotOwner characterOwner { get; private set; }
    public BattleSlotOwner contentOwner { get; private set; }
    public int faceDownSummonedTurn = -1;
    public int faceUpSummonedTurn = -1;
    public int movementLockedByBroadcastUntilTurn { get; private set; } = -1;
    public int collabEffectsSilencedUntilTurn { get; private set; } = -1;
    public int collabAttackForbiddenUntilTurn { get; private set; } = -1;
    public int broadcastMoveAndKoLockedUntilTurn { get; private set; } = -1;
    public int broadcastHpMaxDelta { get; private set; } = 0;
    public int currentCharacterTension { get; private set; }
    public int currentCharacterHp { get; private set; }
    public int currentCharacterMaxHp { get; private set; }
    private Action<BattleFieldSlot> onSetupButtonClick;
    private Action<BattleFieldSlot, BaseCardData> onBroadcastCardClick;
    private Action<BattleFieldSlot, BaseCardData> onCharacterCardClick;
    private Action<BattleFieldSlot, BaseCardData> onCharacterCardDoubleClick;
    private Action<BattleFieldSlot, BaseCardData> onContentCardClick;

    private Action<BattleFieldSlot, PointerEventData> onCardDropped;
    private Action<BattleFieldSlot, PointerEventData> onSlotPointerClick;

    private Action<BattleFieldSlot, BaseCardData, PointerEventData> onBeginDragCharacter;
    private Action<BattleFieldSlot, BaseCardData, PointerEventData> onDragCharacter;
    private Action<BattleFieldSlot, BaseCardData, PointerEventData> onEndDragCharacter;

    private Image rootRaycastImage;
    private Color normalRootColor = new Color(1f, 1f, 1f, 0.01f);
    private const float DoubleClickInterval = 0.32f;
    private float lastCharacterClickTime = -10f;
    private bool isMoveHighlightVisible;
    private bool isQuestionTargetHighlightVisible;
    private Coroutine contentFadeCoroutine;
    private CanvasGroup contentFadeCanvasGroup;

    public bool HasBroadcast => broadcastCard != null;
    public bool HasCharacter => characterCard != null;
    public bool HasContent => contentCard != null;

    public void Init(
        Action<BattleFieldSlot> setupClickAction,
        Action<BattleFieldSlot, BaseCardData> broadcastClickAction,
        Action<BattleFieldSlot, BaseCardData> characterClickAction,
        Action<BattleFieldSlot, BaseCardData> contentClickAction,
        Action<BattleFieldSlot, PointerEventData> dropAction = null,
        Action<BattleFieldSlot, PointerEventData> slotPointerClickAction = null,
        Action<BattleFieldSlot, BaseCardData, PointerEventData> beginDragCharacterAction = null,
        Action<BattleFieldSlot, BaseCardData, PointerEventData> dragCharacterAction = null,
        Action<BattleFieldSlot, BaseCardData, PointerEventData> endDragCharacterAction = null)
    {
        onSetupButtonClick = setupClickAction;
        onBroadcastCardClick = broadcastClickAction;
        onCharacterCardClick = characterClickAction;
        onCharacterCardDoubleClick = null;
        onContentCardClick = contentClickAction;
        onCardDropped = dropAction;
        onSlotPointerClick = slotPointerClickAction;

        onBeginDragCharacter = beginDragCharacterAction;
        onDragCharacter = dragCharacterAction;
        onEndDragCharacter = endDragCharacterAction;

        EnsureDropRaycastTarget();

        SetupInstallButton();
        SetupCardButtons();

        SetSetupButtonVisible(false);
        SetMoveHighlightVisible(false);
        SetQuestionTargetHighlight(false);
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

    public void SetMoveHighlightVisible(bool value, bool isCollaborationTarget = false)
    {
        if (rootRaycastImage == null)
            EnsureDropRaycastTarget();

        if (rootRaycastImage == null)
            return;

        isMoveHighlightVisible = value;

        if (isQuestionTargetHighlightVisible)
        {
            rootRaycastImage.color = new Color(1f, 0.86f, 0.2f, 0.45f);
            return;
        }

        if (value)
        {
            rootRaycastImage.color = isCollaborationTarget
                ? new Color(1f, 0.2f, 0.2f, 0.4f)
                : new Color(0.2f, 1f, 0.35f, 0.35f);
        }
        else
        {
            rootRaycastImage.color = normalRootColor;
        }
    }

    public void SetQuestionTargetHighlight(bool visible)
    {
        isQuestionTargetHighlightVisible = visible;

        if (questionTargetHighlight != null)
        {
            questionTargetHighlight.SetActive(visible);
            return;
        }

        if (rootRaycastImage == null)
            EnsureDropRaycastTarget();

        if (rootRaycastImage == null)
            return;

        if (visible)
        {
            rootRaycastImage.color = new Color(1f, 0.86f, 0.2f, 0.45f);
            return;
        }

        if (isMoveHighlightVisible)
            rootRaycastImage.color = new Color(0.2f, 1f, 0.35f, 0.35f);
        else
            rootRaycastImage.color = normalRootColor;
    }
    public void SetBroadcastCard(BaseCardData card, Sprite sprite)
    {
        broadcastCard = card;
        broadcastMoveAndKoLockedUntilTurn = -1;

        if (broadcastCardImage != null)
        {
            broadcastCardImage.sprite = sprite;
            broadcastCardImage.preserveAspect = true;
        }

        RefreshVisualState();
    }

    public void SetCharacterCard(
        BaseCardData card,
        Sprite sprite,
        bool faceDown = false,
        BattleSlotOwner cardOwner = BattleSlotOwner.My)
    {
        BaseCardData previousCard = characterCard;
        int previousHp = currentCharacterHp;
        int previousMaxHp = currentCharacterMaxHp;
        int previousTension = currentCharacterTension;
        int previousBroadcastHpMaxDelta = broadcastHpMaxDelta;

        bool isSameCharacter = previousCard != null && previousCard == card;

        characterCard = card;
        characterOwner = cardOwner;
        isCharacterFaceDown = faceDown;
        characterMovedThisTurn = false;
        characterActiveUsedThisTurn = false;
        movementLockedByBroadcastUntilTurn = -1;
        collabEffectsSilencedUntilTurn = -1;
        collabAttackForbiddenUntilTurn = -1;
        broadcastHpMaxDelta = isSameCharacter ? previousBroadcastHpMaxDelta : 0;

        if (!faceDown)
            faceDownSummonedTurn = -1;
        else
            faceUpSummonedTurn = -1;

        if (card == null)
        {
            currentCharacterHp = 0;
            currentCharacterMaxHp = 0;
            currentCharacterTension = 0;
        }
        else if (isSameCharacter && previousHp > 0)
        {
            currentCharacterHp = previousHp;
            currentCharacterMaxHp = previousMaxHp;
            currentCharacterTension = previousTension;
        }
        else
        {
            InitializeCharacterBattleStats(card);
        }

        if (characterCardImage != null)
        {
            characterCardImage.sprite = sprite;
            characterCardImage.enabled = sprite != null;
            characterCardImage.preserveAspect = true;

            // 카드 회전은 슬롯 owner가 아니라 cardOwner 기준입니다.
            characterCardImage.rectTransform.localRotation = Quaternion.identity;
        }

        if (characterCardButton != null)
        {
            characterCardButton.gameObject.SetActive(card != null);
            characterCardButton.interactable = card != null;
        }

        RefreshVisualState();
    }

    private void InitializeCharacterBattleStats(BaseCardData card)
    {
        CharacterCardData character = card as CharacterCardData;

        if (character == null)
        {
            currentCharacterHp = 0;
            currentCharacterMaxHp = 0;
            currentCharacterTension = 0;
            return;
        }

        currentCharacterMaxHp = Mathf.Max(1, character.hpMax);
        currentCharacterHp = currentCharacterMaxHp;
        currentCharacterTension = Mathf.Max(0, character.tension);
    }

    public void SetCharacterBattleStats(int hp, int tension)
    {
        SetCharacterBattleStats(hp, currentCharacterMaxHp, tension);
    }

    public void SetCharacterBattleStats(int hp, int maxHp, int tension)
    {
        currentCharacterMaxHp = Mathf.Max(1, maxHp);
        currentCharacterHp = Mathf.Min(hp, currentCharacterMaxHp);
        currentCharacterTension = Mathf.Max(0, tension);
    }

    public void ModifyCharacterMaxHp(int amount)
    {
        int nextMaxHp = Mathf.Max(1, currentCharacterMaxHp + amount);
        currentCharacterMaxHp = nextMaxHp;

        if (currentCharacterHp > currentCharacterMaxHp)
            currentCharacterHp = currentCharacterMaxHp;

        // TODO: 카드별 정책이 필요하면 max HP 증가 시 현재 HP도 함께 회복하는 옵션을 params로 분리한다.
    }

    public void ApplyCharacterDamage(int damage)
    {
        int safeDamage = Mathf.Max(0, damage);
        currentCharacterHp -= safeDamage;
    }

    public void SetContentCard(
        BaseCardData card,
        Sprite sprite,
        BattleSlotOwner cardOwner = BattleSlotOwner.My)
    {
        contentCard = card;
        contentOwner = cardOwner;

        StopContentFadeIfNeeded();

        if (contentCardImage != null)
        {
            contentCardImage.sprite = sprite;
            contentCardImage.enabled = sprite != null;
            contentCardImage.preserveAspect = true;
            contentCardImage.color = Color.white;
        }

        ResetContentFadeAlpha();

        if (contentCardButton != null)
        {
            contentCardButton.gameObject.SetActive(card != null);
            contentCardButton.interactable = card != null;
        }

        RefreshVisualState();
    }

    public void ClearBroadcastCard()
    {
        broadcastCard = null;
        broadcastMoveAndKoLockedUntilTurn = -1;

        if (broadcastCardImage != null)
            broadcastCardImage.sprite = null;

        RefreshVisualState();
    }

    public void ClearCharacterCard()
    {
        characterCard = null;
        characterOwner = BattleSlotOwner.My;
        isCharacterFaceDown = false;
        faceDownSummonedTurn = -1;
        faceUpSummonedTurn = -1;
        characterMovedThisTurn = false;
        characterActiveUsedThisTurn = false;
        movementLockedByBroadcastUntilTurn = -1;
        collabEffectsSilencedUntilTurn = -1;
        collabAttackForbiddenUntilTurn = -1;
        broadcastHpMaxDelta = 0;
        currentCharacterHp = 0;
        currentCharacterMaxHp = 0;
        currentCharacterTension = 0;

        if (characterCardImage != null)
        {
            characterCardImage.sprite = null;
            characterCardImage.enabled = false;
            characterCardImage.rectTransform.localRotation = Quaternion.identity;
        }

        if (characterCardButton != null)
        {
            RectTransform rect = characterCardButton.GetComponent<RectTransform>();

            if (rect != null)
                rect.localEulerAngles = Vector3.zero;
        }

        RefreshVisualState();
    }

    public void ClearContentCard()
    {
        StopContentFadeIfNeeded();
        contentCard = null;
        contentOwner = BattleSlotOwner.My;

        if (contentCardImage != null)
        {
            contentCardImage.sprite = null;
            contentCardImage.color = Color.white;
        }

        ResetContentFadeAlpha();
        RefreshVisualState();
    }

    public void ClearContentCardWithFade(float fadeDuration = 0.35f)
    {
        StopContentFadeIfNeeded();

        contentCard = null;
        contentOwner = BattleSlotOwner.My;

        if (contentCardButton != null)
            contentCardButton.interactable = false;

        if (contentCardImage == null || contentCardImage.sprite == null)
        {
            ClearContentCard();
            return;
        }

        contentFadeCoroutine = StartCoroutine(FadeOutContentCardRoutine(fadeDuration));
    }

    private IEnumerator FadeOutContentCardRoutine(float fadeDuration)
    {
        float safeDuration = Mathf.Max(0.01f, fadeDuration);
        float timer = 0f;
        CanvasGroup canvasGroup = ResolveContentFadeCanvasGroup();

        if (canvasGroup == null)
        {
            ClearContentCard();
            yield break;
        }

        float startAlpha = canvasGroup.alpha;

        while (timer < safeDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / safeDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        contentCardImage.sprite = null;
        canvasGroup.alpha = 1f;
        contentFadeCoroutine = null;

        RefreshVisualState();
    }

    private CanvasGroup ResolveContentFadeCanvasGroup()
    {
        if (contentFadeCanvasGroup != null)
            return contentFadeCanvasGroup;

        GameObject targetObject = null;

        if (contentCardButton != null)
            targetObject = contentCardButton.gameObject;
        else if (contentCardImage != null)
            targetObject = contentCardImage.gameObject;

        if (targetObject == null)
            return null;

        contentFadeCanvasGroup = targetObject.GetComponent<CanvasGroup>();

        if (contentFadeCanvasGroup == null)
            contentFadeCanvasGroup = targetObject.AddComponent<CanvasGroup>();

        return contentFadeCanvasGroup;
    }

    private void StopContentFadeIfNeeded()
    {
        if (contentFadeCoroutine == null)
            return;

        StopCoroutine(contentFadeCoroutine);
        contentFadeCoroutine = null;

        ResetContentFadeAlpha();
    }

    private void ResetContentFadeAlpha()
    {
        CanvasGroup canvasGroup = ResolveContentFadeCanvasGroup();

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    public void ClearAllCards()
    {
        broadcastCard = null;
        characterCard = null;
        contentCard = null;
        characterOwner = BattleSlotOwner.My;
        contentOwner = BattleSlotOwner.My;
        faceDownSummonedTurn = -1;
        faceUpSummonedTurn = -1;
        characterMovedThisTurn = false;
        characterActiveUsedThisTurn = false;
        movementLockedByBroadcastUntilTurn = -1;
        collabEffectsSilencedUntilTurn = -1;
        collabAttackForbiddenUntilTurn = -1;
        broadcastHpMaxDelta = 0;
        broadcastMoveAndKoLockedUntilTurn = -1;

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
        // 캐릭터 카드는 슬롯 소유자가 아니라 카드 소유자 기준으로 회전합니다.
        if (characterCardButton != null)
        {
            RectTransform rect = characterCardButton.GetComponent<RectTransform>();

            if (rect != null)
            {
                bool isEnemyCharacter =
                    characterCard != null &&
                    characterOwner == BattleSlotOwner.Enemy;

                rect.localEulerAngles =
                    isEnemyCharacter ? new Vector3(0f, 0f, 180f) : Vector3.zero;
            }
        }

        // 콘텐츠 카드도 나중에 상대 필드로 넘어갈 수 있으므로 contentOwner 기준으로 회전합니다.
        if (contentCardButton != null)
        {
            RectTransform rect = contentCardButton.GetComponent<RectTransform>();

            if (rect != null)
            {
                bool isEnemyContent =
                    contentCard != null &&
                    contentOwner == BattleSlotOwner.Enemy;

                rect.localEulerAngles =
                    isEnemyContent ? new Vector3(0f, 0f, 180f) : Vector3.zero;
            }
        }

        // 방송 카드는 플랫폼 자체이므로 슬롯 기준으로도 회전하지 않습니다.
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

    public void OnPointerClick(PointerEventData eventData)
    {
        onSlotPointerClick?.Invoke(this, eventData);
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

        float now = Time.unscaledTime;
        bool isDoubleClick = now - lastCharacterClickTime <= DoubleClickInterval;
        lastCharacterClickTime = now;

        if (isDoubleClick && onCharacterCardDoubleClick != null)
        {
            onCharacterCardDoubleClick.Invoke(this, characterCard);
            return;
        }

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

    public void SetCharacterActiveUsedThisTurn(bool value)
    {
        characterActiveUsedThisTurn = value;
    }

    public void SetMovementLockedByBroadcastUntilTurn(int turn)
    {
        movementLockedByBroadcastUntilTurn = turn;
    }

    public void SetCollabEffectsSilencedUntilTurn(int turn)
    {
        collabEffectsSilencedUntilTurn = turn;
    }

    public void ClearCollabEffectsSilence()
    {
        collabEffectsSilencedUntilTurn = -1;
    }

    public bool IsCollabEffectsSilenced(int currentTurn)
    {
        return HasCharacter &&
            collabEffectsSilencedUntilTurn >= 0 &&
            currentTurn <= collabEffectsSilencedUntilTurn;
    }

    public void SetCollabAttackForbiddenUntilTurn(int turn)
    {
        collabAttackForbiddenUntilTurn = Mathf.Max(collabAttackForbiddenUntilTurn, turn);
    }

    public void ClearCollabAttackForbidden()
    {
        collabAttackForbiddenUntilTurn = -1;
    }

    public bool IsCollabAttackForbidden(int currentTurn)
    {
        return HasCharacter &&
            collabAttackForbiddenUntilTurn >= 0 &&
            currentTurn <= collabAttackForbiddenUntilTurn;
    }

    public void SetBroadcastMoveAndKoLockedUntilTurn(int turn)
    {
        broadcastMoveAndKoLockedUntilTurn = turn;
    }

    public void ClearBroadcastMoveAndKoLock()
    {
        broadcastMoveAndKoLockedUntilTurn = -1;
    }

    public bool IsBroadcastMoveAndKoLocked(int currentTurn)
    {
        return HasBroadcast &&
            broadcastMoveAndKoLockedUntilTurn >= 0 &&
            currentTurn <= broadcastMoveAndKoLockedUntilTurn;
    }

    public void SetBroadcastHpMaxDelta(int delta)
    {
        broadcastHpMaxDelta = delta;
    }

    public void ApplyBroadcastHpMaxDelta(int delta)
    {
        if (!HasCharacter)
        {
            broadcastHpMaxDelta = 0;
            return;
        }

        int maxHpBeforeBroadcast = currentCharacterMaxHp - broadcastHpMaxDelta;
        int nextMaxHp = Mathf.Max(1, maxHpBeforeBroadcast + delta);

        broadcastHpMaxDelta = delta;
        currentCharacterMaxHp = nextMaxHp;

        if (currentCharacterHp > currentCharacterMaxHp)
            currentCharacterHp = currentCharacterMaxHp;
    }

    public void SetCharacterDoubleClickAction(Action<BattleFieldSlot, BaseCardData> doubleClickAction)
    {
        onCharacterCardDoubleClick = doubleClickAction;
    }
}
