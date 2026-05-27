using UnityEngine;

public class SummonManager : MonoBehaviour
{
    private const int FaceDownSummonCostLimit = 10000;

    [SerializeField] private BattleManager battleManager;

    private BattleFieldSlot pendingSummonSlot;
    private BaseCardData pendingSummonCard;
    private BattleFieldSlot pendingFlipSlot;
    private BaseCardData pendingFlipCard;

    private bool myHasSummonedFaceDownThisTurn = false;

    public bool HasPendingSummonChoice =>
        pendingSummonSlot != null ||
        pendingSummonCard != null;

    public bool HasPendingFlipChoice =>
        pendingFlipSlot != null ||
        pendingFlipCard != null;

    public void Init(BattleManager manager)
    {
        battleManager = manager;
        ClearPending();
    }

    private void Awake()
    {
        if (battleManager == null)
            battleManager = GetComponentInParent<BattleManager>();
    }

    public void ResetTurnLimitedFlagsForNewTurn()
    {
        myHasSummonedFaceDownThisTurn = false;
    }

    public int GetCharacterAppearCostFromExternal(BaseCardData card)
    {
        return GetCharacterAppearCost(card);
    }

    public bool CanFlipSummonByTurnFromExternal(BattleFieldSlot slot, out string failReason)
    {
        return CanFlipSummonByTurn(slot, out failReason);
    }

    public bool CanSummonBacksideByCostFromExternal(BaseCardData card)
    {
        return CanSummonBacksideByCost(card);
    }

    public void ClearPending()
    {
        ClearPendingSummonChoice();
        ClearPendingFlipChoice();
    }

    public void OpenSummonQuestion(BattleFieldSlot slot, BaseCardData card)
    {
        string failReason;
        if (!battleManager.CanUseMyActionFromExternal(out failReason))
        {
            ClearPendingSummonChoice();
            battleManager.ClearDraggingHandCardFromExternal();
            battleManager.SetSystemMessageFromExternal(failReason);
            return;
        }

        QuestionPanel questionPanel = battleManager.BattleQuestionPanel;
        if (questionPanel != null && questionPanel.IsOpen())
        {
            ClearPendingSummonChoice();
            battleManager.ClearDraggingHandCardFromExternal();
            battleManager.SetSystemMessageFromExternal("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        if (!CanOpenSummonQuestion(slot, card, out failReason))
        {
            ClearPendingSummonChoice();
            battleManager.ClearDraggingHandCardFromExternal();
            battleManager.SetSystemMessageFromExternal(failReason);
            return;
        }

        ClearPendingFlipChoice();

        pendingSummonSlot = slot;
        pendingSummonCard = card;

        int appearCost = GetCharacterAppearCost(card);
        bool canSummonFront = battleManager.CanPayViewerCostFromExternal(BattleSlotOwner.My, appearCost);
        string backsideFailReason;
        bool canSummonBackside = CanSummonBackside(card, out backsideFailReason);

        if (!canSummonFront && !canSummonBackside)
        {
            ClearPendingSummonChoice();
            battleManager.ClearDraggingHandCardFromExternal();

            battleManager.SetSystemMessageFromExternal(
                "불가능한 행동입니다.\n" +
                "시청자가 부족하여 앞면 출연할 수 없습니다.\n" +
                backsideFailReason
            );

            return;
        }

        if (questionPanel == null)
        {
            battleManager.SetSystemMessageFromExternal(
                "QuestionPanel이 BattleManager에 연결되어 있지 않습니다.\n" +
                "BattleManager 인스펙터의 Question Panel 필드에 QuestionPanel 오브젝트를 연결해주세요."
            );
            return;
        }

        if (!questionPanel.TryShowSummonQuestion(
            "출연 방법을 선택해 주세요.",
            canSummonFront,
            canSummonBackside,
            OnSelectFrontSummonChoice,
            OnSelectBacksideSummonChoice,
            CancelSummonChoice
        ))
        {
            ClearPendingSummonChoice();
            battleManager.ClearDraggingHandCardFromExternal();
            battleManager.SetSystemMessageFromExternal("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        string frontState = canSummonFront
            ? "앞면 출연 가능"
            : "시청자가 부족하여 앞면 출연 불가";

        string backsideState = canSummonBackside
            ? "뒷면 출연 가능"
            : backsideFailReason;

        battleManager.SetSystemMessageFromExternal(
            $"{card.name} 카드를 ({slot.x}, {slot.y}) 슬롯에 출연하려 합니다.\n" +
            $"{frontState}\n" +
            $"{backsideState}"
        );
    }

    public void OpenFlipSummonQuestion(BattleFieldSlot slot, BaseCardData card)
    {
        if (slot == null || card == null)
            return;

        if (!slot.isCharacterFaceDown)
            return;

        if (slot.characterOwner != BattleSlotOwner.My)
        {
            battleManager.SetSystemMessageFromExternal("내 캐릭터만 뒤집기 출연할 수 있습니다.");
            return;
        }

        string turnFailReason;
        if (!CanFlipSummonByTurn(slot, out turnFailReason))
        {
            battleManager.SetSystemMessageFromExternal(turnFailReason);
            return;
        }

        string failReason;
        if (!battleManager.CanUseMyActionFromExternal(out failReason))
        {
            battleManager.SetSystemMessageFromExternal(failReason);
            return;
        }

        QuestionPanel questionPanel = battleManager.BattleQuestionPanel;
        if (questionPanel != null && questionPanel.IsOpen())
        {
            battleManager.SetSystemMessageFromExternal("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        ClearPendingSummonChoice();
        battleManager.ClearDraggingHandCardFromExternal();

        int cost = GetCharacterAppearCost(card);

        if (!battleManager.CanPayViewerCostFromExternal(BattleSlotOwner.My, cost))
        {
            battleManager.SetSystemMessageFromExternal("시청자가 부족하여 플립 출연할 수 없습니다.");
            return;
        }

        pendingFlipSlot = slot;
        pendingFlipCard = card;

        if (questionPanel == null)
        {
            battleManager.SetSystemMessageFromExternal("QuestionPanel이 BattleManager에 연결되어 있지 않습니다.");
            return;
        }

        if (!questionPanel.TryShowYesNoQuestion(
            "플립 출연을 하시겠습니까?",
            OnConfirmFlipSummon,
            CancelFlipSummonChoice,
            CancelFlipSummonChoice
        ))
        {
            ClearPendingFlipChoice();
            battleManager.SetSystemMessageFromExternal("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        battleManager.SetSystemMessageFromExternal($"{card.name} 카드를 플립 출연할 수 있습니다.");
    }

    private bool CanOpenSummonQuestion(BattleFieldSlot slot, BaseCardData card, out string failReason)
    {
        failReason = "";

        if (!battleManager.CanUseMyActionFromExternal(out failReason))
            return false;

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

        if (!battleManager.IsCardInHandFromExternal(BattleSlotOwner.My, card))
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

    private void OnSelectFrontSummonChoice()
    {
        if (pendingSummonSlot == null || pendingSummonCard == null)
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal("앞면 출연할 카드 또는 슬롯 정보가 없습니다.");
            return;
        }

        BattleFieldSlot targetSlot = pendingSummonSlot;
        BaseCardData targetCard = pendingSummonCard;

        string failReason;
        if (!CanOpenSummonQuestion(targetSlot, targetCard, out failReason))
        {
            ClearPendingSummonChoice();
            battleManager.ClearDraggingHandCardFromExternal();
            battleManager.SetSystemMessageFromExternal(failReason);
            return;
        }

        SummonCharacterFront(targetSlot, targetCard);
    }

    private void OnSelectBacksideSummonChoice()
    {
        if (pendingSummonSlot == null || pendingSummonCard == null)
        {
            ClearPendingSummonChoice();
            battleManager.ClearDraggingHandCardFromExternal();
            battleManager.SetSystemMessageFromExternal("뒷면 출연할 카드 또는 슬롯 정보가 없습니다.");
            return;
        }

        string actionFailReason;
        if (!battleManager.CanUseMyActionFromExternal(out actionFailReason))
        {
            ClearPendingSummonChoice();
            battleManager.ClearDraggingHandCardFromExternal();
            battleManager.SetSystemMessageFromExternal(actionFailReason);
            return;
        }

        BattleFieldSlot targetSlot = pendingSummonSlot;
        BaseCardData targetCard = pendingSummonCard;

        string backsideFailReason;
        if (!CanSummonBackside(targetCard, out backsideFailReason))
        {
            ClearPendingSummonChoice();
            battleManager.ClearDraggingHandCardFromExternal();
            battleManager.SetSystemMessageFromExternal(backsideFailReason);
            return;
        }

        string failReason;
        if (!CanOpenSummonQuestion(targetSlot, targetCard, out failReason))
        {
            ClearPendingSummonChoice();
            battleManager.ClearDraggingHandCardFromExternal();
            battleManager.SetSystemMessageFromExternal(failReason);
            return;
        }

        SummonCharacterBackside(targetSlot, targetCard);
    }

    private void OnConfirmFlipSummon()
    {
        if (pendingFlipSlot == null || pendingFlipCard == null)
        {
            ClearPendingFlipChoice();
            battleManager.SetSystemMessageFromExternal("플립 출연할 카드 또는 슬롯 정보가 없습니다.");
            return;
        }

        string failReason;
        if (!battleManager.CanUseMyActionFromExternal(out failReason))
        {
            ClearPendingFlipChoice();
            battleManager.SetSystemMessageFromExternal(failReason);
            return;
        }

        FlipSummonCharacter(pendingFlipSlot, pendingFlipCard);
    }

    private void SummonCharacterBackside(BattleFieldSlot targetSlot, BaseCardData characterCard)
    {
        if (targetSlot == null || characterCard == null)
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal("뒷면 출연 처리에 필요한 정보가 없습니다.");
            return;
        }

        if (!battleManager.IsCardInHandFromExternal(BattleSlotOwner.My, characterCard))
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal("내 손패에 없는 카드는 출연시킬 수 없습니다.");
            return;
        }

        if (targetSlot.owner != BattleSlotOwner.My)
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal("내 슬롯에만 캐릭터를 출연시킬 수 있습니다.");
            return;
        }

        if (!targetSlot.HasBroadcast)
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal("방송 카드가 설치된 슬롯에만 캐릭터를 출연시킬 수 있습니다.");
            return;
        }

        if (targetSlot.HasCharacter)
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal("이미 캐릭터가 있는 슬롯입니다.");
            return;
        }

        string backsideFailReason;
        if (!CanSummonBackside(characterCard, out backsideFailReason))
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal(backsideFailReason);
            return;
        }

        targetSlot.SetCharacterCard(
            characterCard,
            battleManager.GetCardBackSpriteFromExternal(),
            true,
            BattleSlotOwner.My
        );
        targetSlot.faceDownSummonedTurn = battleManager.GetCurrentTurnCountFromExternal();

        if (!battleManager.RemoveCardFromHandFromExternal(BattleSlotOwner.My, characterCard))
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal("내 손패에서 출연 카드를 제거할 수 없습니다.");
            return;
        }

        myHasSummonedFaceDownThisTurn = true;

        ClearPendingSummonChoice();
        battleManager.ClearDraggingHandCardFromExternal();

        battleManager.RefreshAllUIFromExternal();

        battleManager.ResolveMyActionUsedFromExternal(
            $"{characterCard.name} 카드를 뒷면으로 출연시켰습니다.\n" +
            $"위치: ({targetSlot.x}, {targetSlot.y})"
        );
    }

    private void SummonCharacterFront(BattleFieldSlot targetSlot, BaseCardData characterCard)
    {
        if (targetSlot == null || characterCard == null)
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal("앞면 출연 처리에 필요한 정보가 없습니다.");
            return;
        }

        if (!battleManager.IsCardInHandFromExternal(BattleSlotOwner.My, characterCard))
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal("내 손패에 없는 카드는 출연시킬 수 없습니다.");
            return;
        }

        if (targetSlot.owner != BattleSlotOwner.My)
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal("내 슬롯에만 캐릭터를 출연시킬 수 있습니다.");
            return;
        }

        if (!targetSlot.HasBroadcast)
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal("방송 카드가 설치된 슬롯에만 캐릭터를 출연시킬 수 있습니다.");
            return;
        }

        if (targetSlot.HasCharacter)
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal("이미 캐릭터가 있는 슬롯입니다.");
            return;
        }

        int cost = GetCharacterAppearCost(characterCard);

        if (!battleManager.CanPayViewerCostFromExternal(BattleSlotOwner.My, cost))
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal("시청자가 부족하여 앞면 출연할 수 없습니다.");
            return;
        }

        Sprite sprite = battleManager.LoadCardSpriteFromExternal(characterCard);

        if (sprite == null)
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal($"{characterCard.name} 카드 이미지를 찾을 수 없습니다.");
            return;
        }

        if (!battleManager.TryPayViewerCostFromExternal(BattleSlotOwner.My, cost))
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal("시청자가 부족하여 앞면 출연할 수 없습니다.");
            return;
        }

        targetSlot.SetCharacterCard(characterCard, sprite, false, BattleSlotOwner.My);
        targetSlot.faceUpSummonedTurn = battleManager.GetCurrentTurnCountFromExternal();

        if (!battleManager.RemoveCardFromHandFromExternal(BattleSlotOwner.My, characterCard))
        {
            ClearPendingSummonChoice();
            battleManager.SetSystemMessageFromExternal("내 손패에서 출연 카드를 제거할 수 없습니다.");
            return;
        }

        ClearPendingSummonChoice();
        battleManager.ClearDraggingHandCardFromExternal();

        battleManager.RefreshAllUIFromExternal();

        string actionMessage =
            $"{characterCard.name} 카드를 앞면으로 출연시켰습니다.\n" +
            $"시청자 -{cost}";

        RequestOnAppearThenResolveAction(targetSlot, characterCard, actionMessage);
    }

    private void FlipSummonCharacter(BattleFieldSlot targetSlot, BaseCardData characterCard)
    {
        if (targetSlot == null || characterCard == null)
        {
            ClearPendingFlipChoice();
            battleManager.SetSystemMessageFromExternal("플립 출연 처리에 필요한 정보가 없습니다.");
            return;
        }

        if (targetSlot.characterOwner != BattleSlotOwner.My)
        {
            ClearPendingFlipChoice();
            battleManager.SetSystemMessageFromExternal("내 캐릭터만 플립 출연할 수 있습니다.");
            return;
        }

        if (!targetSlot.HasCharacter)
        {
            ClearPendingFlipChoice();
            battleManager.SetSystemMessageFromExternal("플립 출연할 캐릭터가 없습니다.");
            return;
        }

        if (!targetSlot.isCharacterFaceDown)
        {
            ClearPendingFlipChoice();
            battleManager.SetSystemMessageFromExternal("이미 앞면 상태인 캐릭터입니다.");
            return;
        }

        string turnFailReason;
        if (!CanFlipSummonByTurn(targetSlot, out turnFailReason))
        {
            ClearPendingFlipChoice();
            battleManager.SetSystemMessageFromExternal(turnFailReason);
            return;
        }

        int cost = GetCharacterAppearCost(characterCard);

        if (!battleManager.CanPayViewerCostFromExternal(BattleSlotOwner.My, cost))
        {
            ClearPendingFlipChoice();
            battleManager.SetSystemMessageFromExternal("시청자가 부족하여 플립 출연할 수 없습니다.");
            return;
        }

        Sprite sprite = battleManager.LoadCardSpriteFromExternal(characterCard);

        if (sprite == null)
        {
            ClearPendingFlipChoice();
            battleManager.SetSystemMessageFromExternal($"{characterCard.name} 카드 이미지를 찾을 수 없습니다.");
            return;
        }

        if (!battleManager.TryPayViewerCostFromExternal(BattleSlotOwner.My, cost))
        {
            ClearPendingFlipChoice();
            battleManager.SetSystemMessageFromExternal("시청자가 부족하여 플립 출연할 수 없습니다.");
            return;
        }

        targetSlot.SetCharacterCard(characterCard, sprite, false, targetSlot.characterOwner);
        targetSlot.faceUpSummonedTurn = battleManager.GetCurrentTurnCountFromExternal();

        ClearPendingFlipChoice();

        battleManager.RefreshAllUIFromExternal();

        string actionMessage =
            $"{characterCard.name} 카드를 플립 출연했습니다.\n" +
            $"시청자 -{cost}";

        RequestOnAppearThenResolveAction(targetSlot, characterCard, actionMessage);
    }

    private void RequestOnAppearThenResolveAction(
        BattleFieldSlot targetSlot,
        BaseCardData characterCard,
        string actionMessage)
    {
        battleManager.RequestOnAppearEffectsFromExternal(
            targetSlot,
            characterCard,
            () => battleManager.ResolveMyActionUsedFromExternal(actionMessage)
        );
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

        int currentTurn = battleManager.GetCurrentTurnCountFromExternal();
        if (slot.faceDownSummonedTurn >= 0 && currentTurn <= slot.faceDownSummonedTurn)
        {
            failReason = "뒷면 출연한 턴에는 플립 출연할 수 없습니다.";
            return false;
        }

        return true;
    }

    private void CancelSummonChoice()
    {
        string cardName = pendingSummonCard != null
            ? pendingSummonCard.name
            : "선택 카드";

        ClearPendingSummonChoice();
        battleManager.ClearDraggingHandCardFromExternal();

        battleManager.SetSystemMessageFromExternal($"{cardName}의 출연 선택을 취소했습니다.");
    }

    private void CancelFlipSummonChoice()
    {
        string cardName = pendingFlipCard != null
            ? pendingFlipCard.name
            : "선택 카드";

        ClearPendingFlipChoice();

        battleManager.SetSystemMessageFromExternal($"{cardName}의 플립 출연을 취소했습니다.");
    }

    private void ClearPendingSummonChoice()
    {
        pendingSummonSlot = null;
        pendingSummonCard = null;

        QuestionPanel questionPanel = battleManager != null
            ? battleManager.BattleQuestionPanel
            : null;

        if (questionPanel != null && questionPanel.IsOpen())
            questionPanel.Hide();
    }

    private void ClearPendingFlipChoice()
    {
        pendingFlipSlot = null;
        pendingFlipCard = null;

        QuestionPanel questionPanel = battleManager != null
            ? battleManager.BattleQuestionPanel
            : null;

        if (questionPanel != null && questionPanel.IsOpen())
            questionPanel.Hide();
    }

    private int GetCharacterAppearCost(BaseCardData card)
    {
        CharacterCardData character = card as CharacterCardData;

        if (character == null)
            return 0;

        return Mathf.Max(0, character.appearCost);
    }

    private bool CanSummonBackside(BaseCardData card, out string failReason)
    {
        failReason = "";

        if (myHasSummonedFaceDownThisTurn)
        {
            failReason = "이번 턴에는 이미 뒷면 출연을 했습니다.";
            return false;
        }

        if (!CanSummonBacksideByCost(card))
        {
            failReason = $"출연 코스트가 {FaceDownSummonCostLimit} 이상인 캐릭터는 뒷면 출연할 수 없습니다.";
            return false;
        }

        return true;
    }

    private bool CanSummonBacksideByCost(BaseCardData card)
    {
        CharacterCardData character = card as CharacterCardData;

        if (character == null)
            return false;

        return character.appearCost < FaceDownSummonCostLimit;
    }
}
