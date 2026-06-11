using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardDetailPanel : MonoBehaviour
{
    private BattleManager battleManager;

    [Header("Image")]
    public Image cardImage;

    [Header("Basic Info Texts")]
    public TMP_Text cardNameText;
    public TMP_Text cardKindText;
    public TMP_Text cardCharmText;
    public TMP_Text cardHashtagText;

    [Header("Zoom Popup")]
    public GameObject cardZoomPopupPanel;
    public Image zoomCardImage;

    [Header("Effect Text")]
    public TMP_Text cardEffectText;

    [Header("Runtime Status")]
    public Image runtimeStatusPanelImage;
    public Button runtimeStatusButton;
    public TMP_Text runtimeStatusButtonText;
    public GameObject runtimeStatusTextRoot;
    public TMP_Text runtimeStatusText;

    [Header("Runtime Status Colors")]
    public Color runtimeStatusNormalColor = Color.white;
    public Color runtimeStatusChangedColor = new Color(0.75f, 0.87f, 1f, 1f);

    private BattleFieldSlot currentRuntimeSlot;
    private bool hasWarnedMissingRuntimeStatusImage;
    private bool hasWarnedMissingRuntimeStatusText;
    
    private void Awake()
    {
        if (battleManager == null)
            battleManager = GetComponentInParent<BattleManager>();

        if (cardZoomPopupPanel != null)
            cardZoomPopupPanel.SetActive(false);

        if (cardImage != null)
        {
            Button imageButton = cardImage.GetComponent<Button>();

            if (imageButton == null)
                imageButton = cardImage.gameObject.AddComponent<Button>();

            imageButton.onClick.RemoveAllListeners();
            imageButton.onClick.AddListener(OpenZoomPopup);
        }

        if (cardZoomPopupPanel != null)
        {
            Button popupButton = cardZoomPopupPanel.GetComponent<Button>();

            if (popupButton == null)
                popupButton = cardZoomPopupPanel.AddComponent<Button>();

            popupButton.onClick.RemoveAllListeners();
            popupButton.onClick.AddListener(CloseZoomPopup);
        }

        ResolveRuntimeStatusReferences();
        ConfigureRuntimeStatusButton();
        ResetRuntimeStatusPanel();
    }

    public void Init(BattleManager manager)
    {
        battleManager = manager;
    }

    public void ShowCard(BaseCardData card)
    {
        if (card == null)
        {
            Clear();
            return;
        }

        SetBasicInfo(card);
        SetCardImage(card);
        SetEffectText(card);
        ResetRuntimeStatusPanel();
    }

    public void ShowFieldCharacter(BattleFieldSlot slot)
    {
        if (slot == null || slot.characterCard == null)
        {
            Clear();
            return;
        }

        BaseCardData card = slot.characterCard;

        SetBasicInfo(card);
        SetCardImage(card);
        SetEffectText(card, slot);
        SetRuntimeStatusSlot(slot);
    }

    public void ShowFieldCharacter(BaseCardData card, BattleFieldSlot slot)
    {
        if (slot == null || card == null)
        {
            ShowCard(card);
            return;
        }

        ShowFieldCharacter(slot);
    }

    private void ResolveRuntimeStatusReferences()
    {
        Transform panel = transform.Find("RuntimeStatusPanel");

        if (runtimeStatusPanelImage == null)
        {
            if (panel != null)
                runtimeStatusPanelImage = panel.GetComponent<Image>();
        }

        bool hasRuntimeStatusPanel =
            panel != null ||
            runtimeStatusPanelImage != null ||
            runtimeStatusButton != null ||
            runtimeStatusTextRoot != null ||
            runtimeStatusText != null;

        if (!hasRuntimeStatusPanel)
            return;

        if (runtimeStatusPanelImage != null)
            runtimeStatusNormalColor = runtimeStatusPanelImage.color;
        else if (!hasWarnedMissingRuntimeStatusImage)
        {
            hasWarnedMissingRuntimeStatusImage = true;
            Debug.LogWarning("CardDetailPanel RuntimeStatusPanel Image 참조가 없습니다. Inspector에서 연결하거나 RuntimeStatusPanel에 Image를 추가하세요.");
        }

        if (runtimeStatusButton == null)
        {
            Transform buttonTransform = transform.Find("RuntimeStatusPanel/RuntimeStatusButton");
            if (buttonTransform != null)
                runtimeStatusButton = buttonTransform.GetComponent<Button>();
        }

        if (runtimeStatusButtonText == null && runtimeStatusButton != null)
            runtimeStatusButtonText = runtimeStatusButton.GetComponentInChildren<TMP_Text>(true);

        if (runtimeStatusTextRoot == null)
        {
            Transform textRootTransform = transform.Find("RuntimeStatusPanel/RuntimeStatusText");
            if (textRootTransform != null)
                runtimeStatusTextRoot = textRootTransform.gameObject;
        }

        if (runtimeStatusText == null)
        {
            if (runtimeStatusTextRoot != null)
                runtimeStatusText = runtimeStatusTextRoot.GetComponentInChildren<TMP_Text>(true);
            else
            {
                Transform textTransform = transform.Find("RuntimeStatusPanel/RuntimeStatusText/Text");
                if (textTransform != null)
                    runtimeStatusText = textTransform.GetComponent<TMP_Text>();
            }
        }

        if (runtimeStatusText == null && !hasWarnedMissingRuntimeStatusText)
        {
            hasWarnedMissingRuntimeStatusText = true;
            Debug.LogWarning("CardDetailPanel RuntimeStatusText TMP_Text 참조가 없습니다. Inspector에서 연결하세요.");
        }
    }

    private void ConfigureRuntimeStatusButton()
    {
        if (runtimeStatusButtonText != null)
            runtimeStatusButtonText.text = "상태 보기";

        if (runtimeStatusButton == null)
            return;

        runtimeStatusButton.onClick.RemoveAllListeners();
        runtimeStatusButton.interactable = true;
        ConfigureRuntimeStatusHoverEvents(runtimeStatusButton.gameObject);
    }

    private void ConfigureRuntimeStatusHoverEvents(GameObject targetObject)
    {
        if (targetObject == null)
            return;

        EventTrigger trigger = targetObject.GetComponent<EventTrigger>();

        if (trigger == null)
            trigger = targetObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();

        EventTrigger.Entry enterEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        enterEntry.callback.AddListener(_ => ShowRuntimeStatusTextOnHover());
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };
        exitEntry.callback.AddListener(_ => HideRuntimeStatusTextOnHoverExit());
        trigger.triggers.Add(exitEntry);
    }

    private void ResetRuntimeStatusPanel()
    {
        currentRuntimeSlot = null;

        if (runtimeStatusPanelImage != null)
            runtimeStatusPanelImage.color = runtimeStatusNormalColor;

        if (runtimeStatusButtonText != null)
            runtimeStatusButtonText.text = "상태 보기";

        SetRuntimeStatusButtonVisible(true);
        SetRuntimeStatusTextVisible(false);

        if (runtimeStatusText != null)
            runtimeStatusText.text = "";
    }

    private void SetRuntimeStatusSlot(BattleFieldSlot slot)
    {
        currentRuntimeSlot = slot;

        if (runtimeStatusButtonText != null)
            runtimeStatusButtonText.text = "상태 보기";

        SetRuntimeStatusButtonVisible(true);
        RefreshRuntimeStatusPanel();
        SetRuntimeStatusTextVisible(false);
    }

    private void ShowRuntimeStatusTextOnHover()
    {
        if (currentRuntimeSlot == null || !currentRuntimeSlot.HasCharacter)
            return;

        RefreshRuntimeStatusPanel();
        SetRuntimeStatusTextVisible(true);
    }

    private void HideRuntimeStatusTextOnHoverExit()
    {
        SetRuntimeStatusTextVisible(false);
    }

    private void RefreshRuntimeStatusPanel()
    {
        if (currentRuntimeSlot == null || !currentRuntimeSlot.HasCharacter)
        {
            ResetRuntimeStatusPanel();
            return;
        }

        if (runtimeStatusPanelImage != null)
        {
            runtimeStatusPanelImage.color = HasRuntimeStatusChange(currentRuntimeSlot)
                ? runtimeStatusChangedColor
                : runtimeStatusNormalColor;
        }

        if (runtimeStatusText != null)
            runtimeStatusText.text = BuildRuntimeStatusText(currentRuntimeSlot);
    }

    private void SetRuntimeStatusButtonVisible(bool visible)
    {
        if (runtimeStatusButton == null)
            return;

        runtimeStatusButton.gameObject.SetActive(visible);
        runtimeStatusButton.interactable = true;
    }

    private void SetRuntimeStatusTextVisible(bool visible)
    {
        if (runtimeStatusTextRoot != null)
        {
            runtimeStatusTextRoot.SetActive(visible);
            return;
        }

        if (runtimeStatusText != null)
            runtimeStatusText.gameObject.SetActive(visible);
    }

    private bool HasRuntimeStatusChange(BattleFieldSlot slot)
    {
        if (slot == null || !slot.HasCharacter)
            return false;

        CharacterCardData character = slot.characterCard as CharacterCardData;
        int currentTurn = GetCurrentTurnCount();

        if (character != null)
        {
            if (GetDisplayedCharacterHp(slot) != GetDisplayedCharacterMaxHp(slot))
                return true;

            if (GetDisplayedCharacterMaxHp(slot) != Mathf.Max(1, character.hpMax))
                return true;

            if (GetDisplayedCharacterTension(slot) != Mathf.Max(0, character.tension))
                return true;
        }

        if (slot.isCharacterFaceDown)
            return true;

        if (slot.characterMovedThisTurn)
            return true;

        if (slot.characterActiveUsedThisTurn)
            return true;

        if (IsMovementLocked(slot))
            return true;

        if (slot.IsCollabAttackForbidden(currentTurn))
            return true;

        if (slot.IsCollabEffectsSilenced(currentTurn))
            return true;

        // TODO: 일반 효과 무효화 상태가 별도 런타임 값으로 생기면 여기에서 판정한다.
        // TODO: 이동 완료와 합방/공격 완료가 분리되면 합방 완료 상태를 별도로 판정한다.

        return false;
    }

    private string BuildRuntimeStatusText(BattleFieldSlot slot)
    {
        if (slot == null || !slot.HasCharacter)
            return "";

        CharacterCardData character = slot.characterCard as CharacterCardData;
        int baseTension = character != null ? Mathf.Max(0, character.tension) : slot.currentCharacterTension;
        int currentTension = GetDisplayedCharacterTension(slot);
        int currentHp = GetDisplayedCharacterHp(slot);
        int maxHp = GetDisplayedCharacterMaxHp(slot);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("현재 상태");
        sb.AppendLine();
        sb.AppendLine($"체력: {currentHp} / {maxHp}");
        sb.AppendLine($"합방 텐션: {currentTension} / {baseTension}");
        sb.AppendLine();
        sb.AppendLine(BuildMoveStatusLine(slot));
        sb.AppendLine(BuildCollabStatusLine(slot));
        sb.AppendLine(BuildEffectStatusLine(slot));
        sb.AppendLine(BuildCollabEffectStatusLine(slot));

        return sb.ToString();
    }

    private string BuildMoveStatusLine(BattleFieldSlot slot)
    {
        if (slot == null || !slot.HasCharacter)
            return "이동: 불가 - 캐릭터가 없습니다.";

        if (slot.isCharacterFaceDown)
            return "이동: 불가 - 뒷면 캐릭터는 이동할 수 없습니다.";

        if (slot.characterMovedThisTurn)
            return "이동: 완료 - 이번 턴 이미 이동했습니다.";

        if (IsMovementLocked(slot))
            return "이동: 불가 - 효과로 인해 이동할 수 없습니다.";

        if (IsAppearTurnActionLimited(slot))
            return "이동: 불가 - 출연한 턴에는 이동할 수 없습니다.";

        return "이동: 가능";
    }

    private string BuildCollabStatusLine(BattleFieldSlot slot)
    {
        if (slot == null || !slot.HasCharacter)
            return "합방: 불가 - 캐릭터가 없습니다.";

        if (slot.isCharacterFaceDown)
            return "합방: 불가 - 이 캐릭터는 합방을 시도할 수 없습니다.";

        if (slot.characterMovedThisTurn)
            return "합방: 완료 - 이번 턴 이미 합방을 시도했거나 이동했습니다.";

        if (slot.IsCollabAttackForbidden(GetCurrentTurnCount()))
            return "합방: 불가 - 효과로 인해 이번 턴 합방을 시도할 수 없습니다.";

        if (IsMovementLocked(slot))
            return "합방: 불가 - 효과로 인해 합방을 시도할 수 없습니다.";

        if (IsAppearTurnActionLimited(slot))
            return "합방: 불가 - 출연한 턴에는 합방을 시도할 수 없습니다.";

        // TODO: 합방/공격 완료 전용 런타임 플래그가 생기면 characterMovedThisTurn과 분리해 표시한다.
        return "합방: 가능";
    }

    private string BuildEffectStatusLine(BattleFieldSlot slot)
    {
        if (slot == null || !slot.HasCharacter)
            return "효과: 불가 - 캐릭터가 없습니다.";

        if (slot.isCharacterFaceDown)
            return "효과: 불가 - 뒷면 캐릭터는 효과를 발동할 수 없습니다.";

        if (slot.characterActiveUsedThisTurn)
            return "효과: 완료 - 이번 턴 이미 효과를 발동했습니다.";

        if (!HasActiveEffect(slot.characterCard))
            return "효과: 불가 - 이 캐릭터는 효과를 발동할 수 없습니다.";

        // TODO: 일반 효과 무효화 런타임 플래그가 생기면 "효과: 무효"로 표시한다.
        return "효과: 가능";
    }

    private string BuildCollabEffectStatusLine(BattleFieldSlot slot)
    {
        if (slot != null && slot.IsCollabEffectsSilenced(GetCurrentTurnCount()))
            return "합방 효과: 무효 - 이번 턴 합방 효과가 무효화되어 있습니다.";

        return "합방 효과: 정상";
    }

    private bool IsMovementLocked(BattleFieldSlot slot)
    {
        if (slot == null)
            return false;

        int currentTurn = GetCurrentTurnCount();

        return (slot.movementLockedByBroadcastUntilTurn >= 0 &&
                currentTurn <= slot.movementLockedByBroadcastUntilTurn) ||
            slot.IsBroadcastMoveAndKoLocked(currentTurn);
    }

    private bool IsAppearTurnActionLimited(BattleFieldSlot slot)
    {
        if (slot == null)
            return false;

        if (slot.faceUpSummonedTurn < 0)
            return false;

        int currentTurn = GetCurrentTurnCount();
        if (currentTurn > slot.faceUpSummonedTurn)
            return false;

        return battleManager == null ||
            !battleManager.CanIgnoreAppearTurnActionLimitFromExternal(slot);
    }

    private bool HasActiveEffect(BaseCardData card)
    {
        CharacterCardData character = card as CharacterCardData;

        if (character == null || character.effects == null)
            return false;

        foreach (EffectData effect in character.effects)
        {
            if (effect == null)
                continue;

            if (TryParseEffectTiming(effect.timing, out EffectTiming timing) &&
                timing == EffectTiming.CharacterActive)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryParseEffectTiming(string rawTiming, out EffectTiming timing)
    {
        timing = EffectTiming.None;

        if (string.IsNullOrWhiteSpace(rawTiming))
            return false;

        string normalized = rawTiming
            .Trim()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "")
            .ToLowerInvariant();

        switch (normalized)
        {
            case "active":
            case "characteractive":
            case "characteract":
                timing = EffectTiming.CharacterActive;
                return true;

            default:
                return false;
        }
    }

    private int GetCurrentTurnCount()
    {
        return battleManager != null
            ? battleManager.GetCurrentTurnCountFromExternal()
            : 0;
    }

    private void SetBasicInfo(BaseCardData card)
    {
        if (cardNameText != null)
            cardNameText.text = card.name;

        if (cardKindText != null)
            cardKindText.text = $"유형: {GetKoreanKind(card.kind)}";

        if (cardCharmText != null)
            cardCharmText.text = $"속성: {ArrayToKoreanCharmText(card.charm)}";

        if (cardHashtagText != null)
            cardHashtagText.text = $"해시태그: {ArrayToText(card.hashtags)}";
    }

    private void SetCardImage(BaseCardData card)
    {
        if (cardImage == null) return;

        Sprite sprite = Resources.Load<Sprite>(card.image);

        if (sprite == null)
        {
            Debug.LogWarning($"상세 카드 이미지를 찾을 수 없습니다: {card.image}");
            cardImage.sprite = null;
            return;
        }

        cardImage.sprite = sprite;
    }

    private void SetEffectText(BaseCardData card)
    {
        SetEffectText(card, null);
    }

    private void SetEffectText(BaseCardData card, BattleFieldSlot fieldSlot)
    {
        if (cardEffectText == null) return;

        StringBuilder sb = new StringBuilder();

        if (card is IdolCardData idol)
        {
            sb.AppendLine("[아이돌 정보]");
            sb.AppendLine($"방송 슬롯: {idol.maxBroadcastSlots}");
            sb.AppendLine($"기본 시청자 획득량: {idol.baseViewersPerPrep}");
            sb.AppendLine($"액티브 코스트: {idol.activeCost}");
            sb.AppendLine();

            AppendEffects(sb, "[패시브 효과]", idol.passive);
            AppendEffects(sb, "[액티브 효과]", idol.active);
        }
        else if (card is BroadcastCardData broadcast)
        {
            sb.AppendLine("[방송 정보]");
            sb.AppendLine($"시청자 보정값: {broadcast.viewersModifier}");
            sb.AppendLine();

            AppendEffects(sb, "[방송 효과]", broadcast.effects);
        }
        else if (card is CharacterCardData character)
        {
            sb.AppendLine("[캐릭터 정보]");
            sb.AppendLine($"출연 코스트: {character.appearCost}");
            sb.AppendLine($"액티브 코스트: {character.activeCost}");
            int? currentTension = fieldSlot != null
                ? GetDisplayedCharacterTension(fieldSlot)
                : null;
            int? currentHp = fieldSlot != null
                ? GetDisplayedCharacterHp(fieldSlot)
                : null;
            int? currentMaxHp = fieldSlot != null
                ? GetDisplayedCharacterMaxHp(fieldSlot)
                : null;
            sb.AppendLine(FormatRuntimeStat("합방 텐션", character.tension, currentTension));
            sb.AppendLine(FormatHpRuntimeStat(character.hpMax, currentHp, currentMaxHp));
            sb.AppendLine();

            AppendEffects(sb, "[캐릭터 효과]", character.effects);
        }
        else if (card is ContentCardData content)
        {
            sb.AppendLine("[콘텐츠 정보]");
            sb.AppendLine($"콘텐츠 타입: {content.contentType}");
            sb.AppendLine($"사용 코스트: {content.cost}");
            sb.AppendLine();

            AppendEffects(sb, "[콘텐츠 효과]", content.effects);
        }

        cardEffectText.text = sb.ToString();
    }

    private string FormatRuntimeStat(string label, int baseValue, int? currentValue)
    {
        if (!currentValue.HasValue || currentValue.Value == baseValue)
            return $"{label}: {baseValue}";

        return $"{label}: {baseValue} (현재 {currentValue.Value})";
    }

    private string FormatHpRuntimeStat(int baseMaxHp, int? currentHp, int? currentMaxHp)
    {
        if (!currentHp.HasValue && !currentMaxHp.HasValue)
            return $"체력: {baseMaxHp}";

        int shownCurrentHp = currentHp ?? baseMaxHp;
        int shownMaxHp = currentMaxHp ?? baseMaxHp;

        if (shownCurrentHp == baseMaxHp && shownMaxHp == baseMaxHp)
            return $"체력: {baseMaxHp}";

        if (shownMaxHp == baseMaxHp)
            return $"체력: {baseMaxHp} (현재 {shownCurrentHp})";

        return $"체력: {baseMaxHp} (현재 {shownCurrentHp} / 최대 {shownMaxHp})";
    }

    private int GetDisplayedCharacterTension(BattleFieldSlot slot)
    {
        if (slot == null)
            return 0;

        int value = slot.currentCharacterTension;

        if (battleManager != null)
            value += battleManager.GetSlotCharacterTensionModifierFromExternal(slot);

        return Mathf.Max(0, value);
    }

    private int GetDisplayedCharacterHp(BattleFieldSlot slot)
    {
        if (slot == null)
            return 0;

        if (slot.currentCharacterHp <= 0)
            return 0;

        int value = slot.currentCharacterHp;

        if (battleManager != null)
            value += battleManager.GetSlotCharacterHpModifierFromExternal(slot);

        return Mathf.Max(0, value);
    }

    private int GetDisplayedCharacterMaxHp(BattleFieldSlot slot)
    {
        if (slot == null)
            return 0;

        return Mathf.Max(0, slot.currentCharacterMaxHp);
    }

    private void AppendEffects(StringBuilder sb, string title, EffectData[] effects)
    {
        sb.AppendLine(title);

        if (effects == null || effects.Length == 0)
        {
            sb.AppendLine("효과 없음");
            sb.AppendLine();
            return;
        }

        foreach (EffectData effect in effects)
        {
            if (effect == null) continue;

            if (!string.IsNullOrEmpty(effect.timing))
                sb.AppendLine($"[{GetKoreanTiming(effect.timing)}]");

            if (!string.IsNullOrEmpty(effect.description))
                sb.AppendLine(effect.description);

            sb.AppendLine();
        }
    }

    private string ArrayToText(string[] values)
    {
        if (values == null || values.Length == 0)
            return "-";

        return string.Join(", ", values);
    }

    public void OpenZoomPopup()
    {
        if (cardImage == null || cardImage.sprite == null)
            return;

        if (cardZoomPopupPanel == null || zoomCardImage == null)
            return;

        zoomCardImage.sprite = cardImage.sprite;
        zoomCardImage.preserveAspect = true;

        cardZoomPopupPanel.SetActive(true);
    }

    public void CloseZoomPopup()
    {
        if (cardZoomPopupPanel != null)
            cardZoomPopupPanel.SetActive(false);
    }
    
    public void Clear()
    {
        if (cardImage != null)
            cardImage.sprite = null;

        if (cardNameText != null)
            cardNameText.text = "";

        if (cardKindText != null)
            cardKindText.text = "";

        if (cardCharmText != null)
            cardCharmText.text = "";

        if (cardHashtagText != null)
            cardHashtagText.text = "";

        if (cardEffectText != null)
            cardEffectText.text = "";

        ResetRuntimeStatusPanel();
            
        CloseZoomPopup();
    }

    private string GetKoreanKind(string kind)
{
    switch (kind)
    {
        case "Idol":
            return "아이돌";
        case "Character":
            return "캐릭터";
        case "Content":
            return "컨텐츠";
        case "Broadcast":
            return "방송";
        default:
            return kind;
    }
}

private string GetKoreanCharm(string charm)
{
    switch (charm)
    {
        case "Lovely":
            return "러블리";
        case "Tricky":
            return "트리키";
        case "Pure":
            return "청초";
        case "Cool":
            return "쿨";
        case "Free":
            return "프리";
        default:
            return charm;
    }
}

    private string ArrayToKoreanCharmText(string[] values)
{
    if (values == null || values.Length == 0)
        return "-";

        string[] koreanValues = new string[values.Length];

        for (int i = 0; i < values.Length; i++)
        {
        koreanValues[i] = GetKoreanCharm(values[i]);
        }

    return string.Join(", ", koreanValues);
}

private string GetKoreanTiming(string timing)
{
    switch (timing)
    {
        case "Passive":
            return "상시";

        case "Active":
        case "IdolActive":
        case "CharacterActive":
            return "액티브";

        case "Content":
            return "콘텐츠";

        case "OnAppear":
            return "출연";

        case "Rest":
        case "OnRest":
            return "휴식";

        case "Always":
            return "상시";

        case "PreCollab":
            return "합방 전";

        case "PostCollab":
            return "합방 후";

        case "Collab":
            return "합방";

        default:
            return timing;
    }
}
}
