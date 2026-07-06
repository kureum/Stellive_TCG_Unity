using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MovementManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;

    private readonly List<BattleFieldSlot> highlightedSlots = new List<BattleFieldSlot>();

    private BattleFieldSlot draggingFromSlot;
    private BaseCardData draggingCard;
    private bool isDraggingMoveCard;

    private BattleFieldSlot pendingMoveFromSlot;
    private BattleFieldSlot pendingMoveToSlot;
    private BaseCardData pendingMoveCard;

    private BattleFieldSlot pendingDoubleStepMoveFromSlot;
    private BaseCardData pendingDoubleStepMoveCard;
    private string pendingDoubleStepFirstMoveMessage;

    public bool IsDraggingMoveCard => isDraggingMoveCard;
    public bool HasPendingMoveChoice =>
        pendingMoveFromSlot != null ||
        pendingMoveToSlot != null ||
        pendingMoveCard != null ||
        pendingDoubleStepMoveFromSlot != null ||
        pendingDoubleStepMoveCard != null;

    public bool IsMoveInteractionActive => isDraggingMoveCard || HasPendingMoveChoice;

    public void Init(BattleManager manager)
    {
        battleManager = manager;
        ClearAllMoveState();
    }

    public bool CanMoveCharacterFromExternal(
        BattleFieldSlot sourceSlot,
        BattleFieldSlot targetSlot,
        out string failReason)
    {
        BaseCardData card = sourceSlot != null ? sourceSlot.characterCard : null;

        if (!CanStartMoveFromSlot(sourceSlot, card, out failReason))
            return false;

        return CanMoveToSlot(sourceSlot, targetSlot, out failReason);
    }

    public bool CanMoveCharacterForOwnerFromExternal(
        BattleSlotOwner actingOwner,
        BattleFieldSlot sourceSlot,
        BattleFieldSlot targetSlot,
        out string failReason)
    {
        BaseCardData card = sourceSlot != null ? sourceSlot.characterCard : null;

        if (!CanStartMoveFromSlotForOwner(actingOwner, sourceSlot, card, out failReason))
            return false;

        return CanMoveToSlotForOwner(actingOwner, sourceSlot, targetSlot, out failReason);
    }

    public bool ExecuteMoveCharacterFromAction(BattleFieldSlot sourceSlot, BattleFieldSlot targetSlot)
    {
        string failReason;
        if (!CanMoveCharacterFromExternal(sourceSlot, targetSlot, out failReason))
        {
            Debug.LogWarning($"[BattleAction] MoveCharacter failed: {failReason}");
            battleManager.SetSystemMessageFromExternal($"이동할 수 없습니다.\n{failReason}");
            ClearAllMoveState();
            return false;
        }

        BaseCardData card = sourceSlot.characterCard;
        bool isDoubleStepMove = IsDoubleStepMoveCharacter(card);
        string moveMessage = ExecuteMoveStep(sourceSlot, targetSlot, card, !isDoubleStepMove);

        ClearPendingMoveChoiceState();
        battleManager.RefreshAllUIFromExternal();

        if (TryStartDoubleStepFollowUp(targetSlot, card, moveMessage))
            return true;

        ClearAllMoveState();
        battleManager.ResolveMyActionUsedFromExternal(moveMessage);
        return true;
    }

    private void Awake()
    {
        if (battleManager == null)
            battleManager = GetComponentInParent<BattleManager>();
    }

    public void OnBeginDragFieldCharacter(
        BattleFieldSlot fromSlot,
        BaseCardData card,
        PointerEventData eventData)
    {
        string failReason;
        if (!CanStartMoveFromSlot(fromSlot, card, out failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            ClearAllMoveState();
            return;
        }

        draggingFromSlot = fromSlot;
        draggingCard = card;
        isDraggingMoveCard = true;

        battleManager.SelectCardFromExternal(card);

        HighlightMovableSlots(fromSlot);

        battleManager.SetSystemMessageFromExternal(
            $"{card.name} 이동을 시작했습니다.\n" +
            "하이라이트된 인접 방송 슬롯으로 드래그하세요."
        );
    }

    public void OnDragFieldCharacter(
        BattleFieldSlot fromSlot,
        BaseCardData card,
        PointerEventData eventData)
    {
        // 현재 1차 구현에서는 별도 드래그 프리뷰를 만들지 않습니다.
        // 필요하면 나중에 BattleManager의 드래그 프리뷰 기능을 공용화해서 붙이면 됩니다.
    }

    public void OnEndDragFieldCharacter(
        BattleFieldSlot fromSlot,
        BaseCardData card,
        PointerEventData eventData)
    {
        if (!isDraggingMoveCard)
            return;

        // 유효한 슬롯에 드롭해서 QuestionPanel이 열린 상태라면 여기서 취소하지 않습니다.
        if (pendingMoveFromSlot != null && pendingMoveToSlot != null)
            return;

        string cardName = draggingCard != null ? draggingCard.name : "캐릭터";

        ClearAllMoveState();

        battleManager.SetSystemMessageFromExternal(
            $"{cardName} 이동을 취소했습니다.\n" +
            "이동 가능한 슬롯 위에 내려놓지 않았습니다."
        );
    }

    public bool OnDropMoveTargetSlot(BattleFieldSlot toSlot, PointerEventData eventData)
    {
        if (!isDraggingMoveCard)
            return false;

        if (draggingFromSlot == null || draggingCard == null)
        {
            ClearAllMoveState();
            battleManager.SetSystemMessageFromExternal("이동 중인 캐릭터 정보가 없습니다.");
            return true;
        }

        string failReason;
        string collaborationFailReason;
        if (CanStartCollaborationAtSlot(draggingFromSlot, toSlot, out collaborationFailReason))
        {
            OpenCollaborationQuestion(draggingFromSlot, toSlot, draggingCard);
            return true;
        }

        if (toSlot != null &&
            toSlot.HasCharacter &&
            draggingFromSlot != null &&
            toSlot.characterOwner != draggingFromSlot.characterOwner)
        {
            ClearAllMoveState();
            battleManager.SetSystemMessageFromExternal(
                $"합방할 수 없습니다.\n{collaborationFailReason}"
            );
            return true;
        }

        if (!CanMoveToSlot(draggingFromSlot, toSlot, out failReason))
        {
            string cardName = draggingCard != null ? draggingCard.name : "캐릭터";
            ClearAllMoveState();

            battleManager.SetSystemMessageFromExternal(
                $"{cardName} 이동 불가\n{failReason}"
            );

            return true;
        }

        OpenMoveQuestion(draggingFromSlot, toSlot, draggingCard);
        return true;
    }

    private void OpenMoveQuestion(
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        BaseCardData card)
    {
        if (battleManager == null)
            return;

        QuestionPanel questionPanel = battleManager.BattleQuestionPanel;

        if (questionPanel == null)
        {
            ClearAllMoveState();
            battleManager.SetSystemMessageFromExternal("QuestionPanel이 연결되어 있지 않습니다.");
            return;
        }

        if (questionPanel.IsOpen())
        {
            ClearAllMoveState();
            battleManager.SetSystemMessageFromExternal("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        pendingMoveFromSlot = fromSlot;
        pendingMoveToSlot = toSlot;
        pendingMoveCard = card;

        // 드롭은 성공했으므로 드래그 상태는 끄되, pending 이동 정보는 유지합니다.
        isDraggingMoveCard = false;
        draggingFromSlot = null;
        draggingCard = null;

        ClearMoveHighlights();

        if (!questionPanel.TryShowYesNoQuestion(
            "이동하시겠습니까?",
            ConfirmPendingMove,
            CancelPendingMove,
            CancelPendingMove
        ))
        {
            ClearAllMoveState();
            battleManager.SetSystemMessageFromExternal("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        battleManager.SetSystemMessageFromExternal(
            $"{card.name} 카드를 ({fromSlot.x}, {fromSlot.y})에서 " +
            $"({toSlot.x}, {toSlot.y})로 이동할 수 있습니다."
        );
    }

    private void OpenCollaborationQuestion(
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        BaseCardData card)
    {
        if (battleManager == null)
            return;

        QuestionPanel questionPanel = battleManager.BattleQuestionPanel;

        if (questionPanel == null)
        {
            ClearAllMoveState();
            battleManager.SetSystemMessageFromExternal("QuestionPanel이 연결되어 있지 않습니다.");
            return;
        }

        if (questionPanel.IsOpen())
        {
            ClearAllMoveState();
            battleManager.SetSystemMessageFromExternal("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        pendingMoveFromSlot = fromSlot;
        pendingMoveToSlot = toSlot;
        pendingMoveCard = card;

        isDraggingMoveCard = false;
        draggingFromSlot = null;
        draggingCard = null;

        ClearMoveHighlights();

        if (!questionPanel.TryShowYesNoQuestion(
            "합방을 하시겠습니까?",
            ConfirmPendingCollaboration,
            CancelPendingCollaboration,
            CancelPendingCollaboration
        ))
        {
            ClearAllMoveState();
            battleManager.SetSystemMessageFromExternal("이미 다른 선택창이 열려 있습니다.");
            return;
        }

        battleManager.SetSystemMessageFromExternal(
            $"{card.name} 카드가 상대 캐릭터에게 합방을 시도합니다."
        );
    }

    private void ConfirmPendingCollaboration()
    {
        if (pendingMoveFromSlot == null ||
            pendingMoveToSlot == null ||
            pendingMoveCard == null)
        {
            ClearAllMoveState();
            battleManager.SetSystemMessageFromExternal("합방할 캐릭터 정보가 없습니다.");
            return;
        }

        string failReason;
        if (!CanStartCollaborationAtSlot(pendingMoveFromSlot, pendingMoveToSlot, out failReason))
        {
            ClearAllMoveState();
            battleManager.SetSystemMessageFromExternal($"합방할 수 없습니다.\n{failReason}");
            return;
        }

        if (battleManager.collaborationManager == null)
        {
            ClearAllMoveState();
            battleManager.SetSystemMessageFromExternal("CollaborationManager가 연결되어 있지 않습니다.");
            return;
        }

        BattleFieldSlot fromSlot = pendingMoveFromSlot;
        BattleFieldSlot toSlot = pendingMoveToSlot;

        ClearPendingMoveChoiceState();

        if (!battleManager.RequestStartCollabActionFromExternal(fromSlot, toSlot))
            ClearAllMoveState();
    }

    private void CancelPendingCollaboration()
    {
        string cardName = pendingMoveCard != null ? pendingMoveCard.name : "캐릭터";

        ClearAllMoveState();

        battleManager.SetSystemMessageFromExternal(
            $"{cardName} 합방을 취소했습니다."
        );
    }

    private void ConfirmPendingMove()
    {
        if (pendingMoveFromSlot == null ||
            pendingMoveToSlot == null ||
            pendingMoveCard == null)
        {
            ClearAllMoveState();
            battleManager.SetSystemMessageFromExternal("이동할 캐릭터 정보가 없습니다.");
            return;
        }

        string failReason;
        if (!CanMoveToSlot(pendingMoveFromSlot, pendingMoveToSlot, out failReason))
        {
            ClearAllMoveState();
            battleManager.SetSystemMessageFromExternal($"이동할 수 없습니다.\n{failReason}");
            return;
        }

        BattleFieldSlot fromSlot = pendingMoveFromSlot;
        BattleFieldSlot toSlot = pendingMoveToSlot;

        ClearPendingMoveChoiceState();

        if (!battleManager.RequestMoveCharacterActionFromExternal(fromSlot, toSlot))
            ClearAllMoveState();
    }

    private void CancelPendingMove()
    {
        string cardName = pendingMoveCard != null ? pendingMoveCard.name : "캐릭터";

        ClearAllMoveState();

        battleManager.SetSystemMessageFromExternal(
            $"{cardName} 이동을 취소했습니다."
        );
    }

    private string ExecuteMoveStep(
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        BaseCardData card,
        bool includeMoveExhaustedMessage = true,
        bool markMovedThisTurn = true)
    {
        Sprite currentSprite = fromSlot.GetCurrentCharacterSprite();
        bool wasFaceDown = fromSlot.isCharacterFaceDown;

        BattleSlotOwner movingCardOwner = fromSlot.characterOwner;
        int currentHp = fromSlot.currentCharacterHp;
        int currentMaxHp = fromSlot.currentCharacterMaxHp;
        int currentTension = fromSlot.currentCharacterTension;
        bool activeUsedThisTurn = fromSlot.characterActiveUsedThisTurn;
        int movementLockedUntilTurn = fromSlot.movementLockedByBroadcastUntilTurn;
        int collabEffectsSilencedUntilTurn = fromSlot.collabEffectsSilencedUntilTurn;
        int collabAttackForbiddenUntilTurn = fromSlot.collabAttackForbiddenUntilTurn;
        int broadcastHpMaxDelta = fromSlot.broadcastHpMaxDelta;

        toSlot.SetCharacterCard(card, currentSprite, wasFaceDown, movingCardOwner);
        toSlot.SetCharacterBattleStats(currentHp, currentMaxHp, currentTension);
        toSlot.SetCharacterMovedThisTurn(markMovedThisTurn ? true : fromSlot.characterMovedThisTurn);
        toSlot.SetCharacterActiveUsedThisTurn(activeUsedThisTurn);
        toSlot.SetMovementLockedByBroadcastUntilTurn(movementLockedUntilTurn);
        toSlot.SetCollabEffectsSilencedUntilTurn(collabEffectsSilencedUntilTurn);
        toSlot.SetCollabAttackForbiddenUntilTurn(collabAttackForbiddenUntilTurn);
        toSlot.SetBroadcastHpMaxDelta(broadcastHpMaxDelta);
        battleManager.ApplyBroadcastEnterEffectsFromExternal(toSlot, true);

        battleManager.ApplyBroadcastLeaveEffectsFromExternal(fromSlot);
        fromSlot.ClearCharacterCard();

        string fromOwnerName = fromSlot.owner == BattleSlotOwner.My ? "내 필드" : "상대 필드";
        string toOwnerName = toSlot.owner == BattleSlotOwner.My ? "내 필드" : "상대 필드";

        string message =
            $"{card.name} 카드를 이동했습니다.\n" +
            $"이동 전: {fromOwnerName} ({fromSlot.x}, {fromSlot.y})\n" +
            $"이동 후: {toOwnerName} ({toSlot.x}, {toSlot.y})";

        if (includeMoveExhaustedMessage)
            message += "\n이 캐릭터는 이번 턴에 더 이상 이동할 수 없습니다.";

        if (toSlot.owner == BattleSlotOwner.Enemy)
        {
            message += "\n상대의 빈 방송 플랫폼에 진입했습니다.";
        }

        return message;
    }

    public List<BattleFieldSlot> BuildMoveCandidatesForEffect(BattleFieldSlot fromSlot)
    {
        return BuildTeleportMoveCandidatesForEffect(fromSlot);
    }

    public List<BattleFieldSlot> BuildMoveCandidatesForEffectFromExternal(
        BattleSlotOwner actingOwner,
        BattleFieldSlot fromSlot)
    {
        return BuildTeleportMoveCandidatesForEffect(actingOwner, fromSlot);
    }

    public bool CanMoveCharacterByEffectForOwnerFromExternal(
        BattleSlotOwner actingOwner,
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        out string failReason)
    {
        return CanTeleportMoveToEmptySlotForEffect(actingOwner, fromSlot, toSlot, out failReason);
    }

    public bool CanStartCollaborationByEffectForOwnerFromExternal(
        BattleSlotOwner actingOwner,
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        out string failReason)
    {
        return CanStartTeleportCollaborationForEffect(actingOwner, fromSlot, toSlot, out failReason);
    }

    public bool TryMoveCharacterByEffect(
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        out string message)
    {
        message = "";

        if (fromSlot == null || toSlot == null)
        {
            message = "효과 이동 출발 슬롯 또는 대상 슬롯이 없습니다.";
            return false;
        }

        BaseCardData card = fromSlot.characterCard;
        if (card == null)
        {
            message = "효과로 이동할 캐릭터 카드가 없습니다.";
            return false;
        }

        if (!CanTeleportMoveToEmptySlotForEffect(fromSlot, toSlot, out string failReason))
        {
            message = failReason;
            return false;
        }

        message = ExecuteMoveStep(
            fromSlot,
            toSlot,
            card,
            false,
            false);

        battleManager.RefreshAllUIFromExternal();
        return true;
    }

    public bool TryStartCollaborationByEffect(
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        out string message)
    {
        message = "";

        if (!CanStartTeleportCollaborationForEffect(fromSlot, toSlot, out string failReason))
        {
            message = failReason;
            return false;
        }

        if (battleManager == null || battleManager.collaborationManager == null)
        {
            message = "CollaborationManager가 연결되어 있지 않습니다.";
            return false;
        }

        QuestionPanel questionPanel = battleManager.BattleQuestionPanel;
        if (questionPanel == null)
        {
            message = "QuestionPanel이 연결되어 있지 않습니다.";
            return false;
        }

        if (questionPanel.IsOpen())
        {
            message = "이미 다른 선택창이 열려 있습니다.";
            return false;
        }

        if (battleManager.collaborationManager.IsCollaborationInteractionActive)
        {
            message = "이미 합방 처리를 진행 중입니다.";
            return false;
        }

        if (!questionPanel.TryShowYesNoQuestion(
                "합방을 하시겠습니까?",
                () =>
                {
                    if (CanStartTeleportCollaborationForEffect(fromSlot, toSlot, out string yesFailReason))
                    {
                        battleManager.collaborationManager.StartEffectMoveCollaboration(fromSlot, toSlot);
                        return;
                    }

                    battleManager.SetSystemMessageFromExternal($"합방할 수 없습니다.\n{yesFailReason}");
                },
                () => battleManager.SetSystemMessageFromExternal("합방을 취소했습니다."),
                () => battleManager.SetSystemMessageFromExternal("합방을 취소했습니다.")))
        {
            message = "합방 질문창을 열 수 없습니다.";
            return false;
        }

        message = $"{fromSlot.characterCard.name} 카드가 상대 캐릭터에게 합방을 시도합니다.";
        return true;
    }

    public bool CanStartCollaborationForOwnerFromExternal(
        BattleSlotOwner actingOwner,
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        out string failReason)
    {
        return CanStartCollaborationAtSlot(actingOwner, fromSlot, toSlot, true, out failReason);
    }

    private List<BattleFieldSlot> BuildTeleportMoveCandidatesForEffect(BattleFieldSlot fromSlot)
    {
        return BuildTeleportMoveCandidatesForEffect(BattleSlotOwner.My, fromSlot);
    }

    private List<BattleFieldSlot> BuildTeleportMoveCandidatesForEffect(
        BattleSlotOwner actingOwner,
        BattleFieldSlot fromSlot)
    {
        List<BattleFieldSlot> candidates = new List<BattleFieldSlot>();

        AddTeleportMoveCandidatesForEffect(actingOwner, fromSlot, BattlePlayerSide.My, candidates);
        AddTeleportMoveCandidatesForEffect(actingOwner, fromSlot, BattlePlayerSide.Enemy, candidates);

        return candidates;
    }

    private void AddTeleportMoveCandidatesForEffect(
        BattleSlotOwner actingOwner,
        BattleFieldSlot fromSlot,
        BattlePlayerSide side,
        List<BattleFieldSlot> candidates)
    {
        if (battleManager == null || fromSlot == null || candidates == null)
            return;

        IReadOnlyList<BattleFieldSlot> slots = battleManager.GetSlotsForMovement(side);
        foreach (BattleFieldSlot slot in slots)
        {
            if (slot == null || candidates.Contains(slot))
                continue;

            string failReason;
            if (CanTeleportMoveToEmptySlotForEffect(actingOwner, fromSlot, slot, out failReason) ||
                CanStartTeleportCollaborationForEffect(actingOwner, fromSlot, slot, out failReason))
            {
                candidates.Add(slot);
            }
        }
    }

    private bool CanTeleportMoveToEmptySlotForEffect(
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        out string failReason)
    {
        return CanTeleportMoveToEmptySlotForEffect(BattleSlotOwner.My, fromSlot, toSlot, out failReason);
    }

    private bool CanTeleportMoveToEmptySlotForEffect(
        BattleSlotOwner actingOwner,
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        out string failReason)
    {
        if (!CanUseTeleportMoveSourceAndTargetForEffect(actingOwner, fromSlot, toSlot, out failReason))
            return false;

        if (toSlot.HasCharacter)
        {
            failReason = toSlot.characterOwner == fromSlot.characterOwner
                ? "이미 아군 캐릭터가 있는 슬롯으로는 이동할 수 없습니다."
                : "상대 캐릭터가 있는 슬롯은 합방 대상으로만 선택할 수 있습니다.";
            return false;
        }

        return true;
    }

    private bool CanStartTeleportCollaborationForEffect(
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        out string failReason)
    {
        return CanStartTeleportCollaborationForEffect(BattleSlotOwner.My, fromSlot, toSlot, out failReason);
    }

    private bool CanStartTeleportCollaborationForEffect(
        BattleSlotOwner actingOwner,
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        out string failReason)
    {
        if (!CanUseTeleportMoveSourceAndTargetForEffect(actingOwner, fromSlot, toSlot, out failReason))
            return false;

        if (battleManager.IsCollabAttackForbiddenFromExternal(fromSlot))
        {
            failReason = "효과로 인해 이번 턴 합방할 수 없습니다.";
            return false;
        }

        if (!toSlot.HasCharacter ||
            toSlot.characterOwner == fromSlot.characterOwner)
        {
            failReason = "상대 캐릭터가 있는 슬롯에서만 합방할 수 있습니다.";
            return false;
        }

        return true;
    }

    private bool CanUseTeleportMoveSourceAndTargetForEffect(
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        out string failReason)
    {
        return CanUseTeleportMoveSourceAndTargetForEffect(BattleSlotOwner.My, fromSlot, toSlot, out failReason);
    }

    private bool CanUseTeleportMoveSourceAndTargetForEffect(
        BattleSlotOwner actingOwner,
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        out string failReason)
    {
        failReason = "";

        if (fromSlot == null || toSlot == null)
        {
            failReason = "효과 이동 출발 슬롯 또는 대상 슬롯이 없습니다.";
            return false;
        }

        if (fromSlot == toSlot)
        {
            failReason = "같은 슬롯으로는 이동할 수 없습니다.";
            return false;
        }

        if (!fromSlot.HasCharacter || fromSlot.characterCard == null)
        {
            failReason = "효과로 이동할 캐릭터 카드가 없습니다.";
            return false;
        }

        if (fromSlot.characterOwner != actingOwner)
        {
            failReason = actingOwner == BattleSlotOwner.My
                ? "현재는 내 캐릭터만 효과로 이동할 수 있습니다."
                : "현재는 상대 캐릭터만 효과로 이동할 수 있습니다.";
            return false;
        }

        if (battleManager.IsMoveForbiddenByBroadcastMoveAndKoLockFromExternal(fromSlot, out failReason))
            return false;

        if (!toSlot.HasBroadcast)
        {
            failReason = "방송 카드가 설치된 슬롯으로만 이동할 수 있습니다.";
            return false;
        }

        return true;
    }

    private bool TryStartDoubleStepFollowUp(
        BattleFieldSlot currentSlot,
        BaseCardData card,
        string firstMoveMessage)
    {
        if (!IsDoubleStepMoveCharacter(card))
            return false;

        if (currentSlot == null ||
            !currentSlot.HasCharacter ||
            currentSlot.characterCard != card ||
            currentSlot.isCharacterFaceDown)
        {
            return false;
        }

        string lockFailReason;
        if (battleManager.IsCharacterMoveLockedByBroadcastFromExternal(currentSlot, out lockFailReason))
        {
            string message =
                $"{firstMoveMessage}\n" +
                "공포게임 효과로 추가 이동을 할 수 없습니다.\n" +
                "이 캐릭터는 이번 턴에 더 이상 이동할 수 없습니다.";

            ClearAllMoveState();
            battleManager.ResolveMyActionUsedFromExternal(message);
            return true;
        }

        List<BattleFieldSlot> candidates = BuildDoubleStepNextSlotCandidates(currentSlot);

        if (candidates.Count == 0)
        {
            string message =
                $"{firstMoveMessage}\n" +
                "추가로 이동할 수 있는 인접 방송 슬롯이 없습니다.\n" +
                "이 캐릭터는 이번 턴에 더 이상 이동할 수 없습니다.";

            ClearAllMoveState();
            battleManager.ResolveMyActionUsedFromExternal(message);
            return true;
        }

        pendingDoubleStepMoveFromSlot = currentSlot;
        pendingDoubleStepMoveCard = card;
        pendingDoubleStepFirstMoveMessage = firstMoveMessage;

        QuestionPanel questionPanel = battleManager.BattleQuestionPanel;
        if (questionPanel == null)
        {
            string message =
                $"{firstMoveMessage}\n" +
                "QuestionPanel이 연결되어 있지 않아 추가 이동을 종료합니다.\n" +
                "이 캐릭터는 이번 턴에 더 이상 이동할 수 없습니다.";

            ClearPendingDoubleStepMove();
            battleManager.ResolveMyActionUsedFromExternal(message);
            return true;
        }

        if (questionPanel.IsOpen())
        {
            string message =
                $"{firstMoveMessage}\n" +
                "이미 다른 선택창이 열려 있어 추가 이동을 종료합니다.\n" +
                "이 캐릭터는 이번 턴에 더 이상 이동할 수 없습니다.";

            ClearPendingDoubleStepMove();
            battleManager.ResolveMyActionUsedFromExternal(message);
            return true;
        }

        if (!questionPanel.TryShowYesNoQuestion(
                "추가 이동을 하시겠습니까?",
                RequestDoubleStepNextSlotSelection,
                CancelPendingDoubleStepMove,
                CancelPendingDoubleStepMove))
        {
            string message =
                $"{firstMoveMessage}\n" +
                "추가 이동 질문창을 열 수 없어 이동을 종료합니다.\n" +
                "이 캐릭터는 이번 턴에 더 이상 이동할 수 없습니다.";

            ClearPendingDoubleStepMove();
            battleManager.ResolveMyActionUsedFromExternal(message);
            return true;
        }

        return true;
    }

    private void RequestDoubleStepNextSlotSelection()
    {
        BattleFieldSlot fromSlot = pendingDoubleStepMoveFromSlot;
        BaseCardData card = pendingDoubleStepMoveCard;
        string firstMoveMessage = pendingDoubleStepFirstMoveMessage;

        if (fromSlot == null ||
            card == null ||
            !fromSlot.HasCharacter ||
            fromSlot.characterCard != card)
        {
            ClearPendingDoubleStepMove();
            battleManager.SetSystemMessageFromExternal("추가 이동할 캐릭터 정보가 없습니다.");
            battleManager.ResolveMyActionUsedFromExternal(firstMoveMessage);
            return;
        }

        string lockFailReason;
        if (battleManager.IsCharacterMoveLockedByBroadcastFromExternal(fromSlot, out lockFailReason))
        {
            string message =
                $"{firstMoveMessage}\n" +
                "공포게임 효과로 추가 이동을 할 수 없습니다.\n" +
                "이 캐릭터는 이번 턴에 더 이상 이동할 수 없습니다.";

            ClearPendingDoubleStepMove();
            battleManager.ResolveMyActionUsedFromExternal(message);
            return;
        }

        List<BattleFieldSlot> candidates = BuildDoubleStepNextSlotCandidates(fromSlot);
        if (candidates.Count == 0)
        {
            string message =
                $"{firstMoveMessage}\n" +
                "추가로 이동할 수 있는 위치가 없습니다.\n" +
                "이 캐릭터는 이번 턴에 더 이상 이동할 수 없습니다.";

            ClearPendingDoubleStepMove();
            battleManager.ResolveMyActionUsedFromExternal(message);
            return;
        }

        bool opened = battleManager.RequestFieldSlotSelection(
            "한 번 더 이동할 위치를 골라주세요.",
            candidates,
            selectedSlot => ExecuteDoubleStepMove(selectedSlot),
            CancelPendingDoubleStepMove
        );

        if (!opened)
        {
            string message =
                $"{firstMoveMessage}\n" +
                "추가 이동 위치 선택을 시작할 수 없어 이동을 종료합니다.\n" +
                "이 캐릭터는 이번 턴에 더 이상 이동할 수 없습니다.";

            ClearPendingDoubleStepMove();
            battleManager.ResolveMyActionUsedFromExternal(message);
        }
    }

    private List<BattleFieldSlot> BuildDoubleStepNextSlotCandidates(BattleFieldSlot fromSlot)
    {
        List<BattleFieldSlot> candidates = new List<BattleFieldSlot>();

        AddDoubleStepNextSlotCandidates(fromSlot, BattlePlayerSide.My, candidates);
        AddDoubleStepNextSlotCandidates(fromSlot, BattlePlayerSide.Enemy, candidates);

        return candidates;
    }

    private void AddDoubleStepNextSlotCandidates(
        BattleFieldSlot fromSlot,
        BattlePlayerSide side,
        List<BattleFieldSlot> candidates)
    {
        if (battleManager == null || fromSlot == null || candidates == null)
            return;

        IReadOnlyList<BattleFieldSlot> slots =
            battleManager.GetSlotsForMovement(side);

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot == null || candidates.Contains(slot))
                continue;

            string failReason;
            if (CanMoveToSlot(fromSlot, slot, out failReason) ||
                CanStartCollaborationAtSlot(fromSlot, slot, out failReason))
            {
                candidates.Add(slot);
            }
        }
    }

    private void ExecuteDoubleStepMove(BattleFieldSlot selectedSlot)
    {
        BattleFieldSlot fromSlot = pendingDoubleStepMoveFromSlot;
        BaseCardData card = pendingDoubleStepMoveCard;
        string firstMoveMessage = pendingDoubleStepFirstMoveMessage;

        if (fromSlot == null ||
            selectedSlot == null ||
            card == null ||
            !fromSlot.HasCharacter ||
            fromSlot.characterCard != card)
        {
            ClearPendingDoubleStepMove();
            battleManager.SetSystemMessageFromExternal("추가 이동할 캐릭터 정보가 없습니다.");
            battleManager.ResolveMyActionUsedFromExternal(firstMoveMessage);
            return;
        }

        string failReason;
        if (CanStartCollaborationAtSlot(fromSlot, selectedSlot, out failReason))
        {
            ClearPendingDoubleStepMove();
            battleManager.ResolveMyActionUsedFromExternal(
                $"{firstMoveMessage}\n추가 이동으로 합방을 시도합니다.");
            OpenCollaborationQuestion(fromSlot, selectedSlot, card);
            return;
        }

        if (!CanMoveToSlot(fromSlot, selectedSlot, out failReason))
        {
            battleManager.SetSystemMessageFromExternal($"추가 이동할 수 없습니다.\n{failReason}");
            RequestDoubleStepNextSlotSelectionAgain(fromSlot, card, firstMoveMessage);
            return;
        }

        string secondMoveMessage = ExecuteMoveStep(fromSlot, selectedSlot, card);
        string message =
            $"{firstMoveMessage}\n" +
            "추가 이동을 완료했습니다.\n" +
            secondMoveMessage;

        ClearAllMoveState();
        battleManager.RefreshAllUIFromExternal();
        battleManager.ResolveMyActionUsedFromExternal(message);
    }

    private void RequestDoubleStepNextSlotSelectionAgain(
        BattleFieldSlot fromSlot,
        BaseCardData card,
        string firstMoveMessage)
    {
        pendingDoubleStepMoveFromSlot = fromSlot;
        pendingDoubleStepMoveCard = card;
        pendingDoubleStepFirstMoveMessage = firstMoveMessage;
        RequestDoubleStepNextSlotSelection();
    }

    private void CancelPendingDoubleStepMove()
    {
        string firstMoveMessage = pendingDoubleStepFirstMoveMessage;
        ClearPendingDoubleStepMove();

        string message =
            $"{firstMoveMessage}\n" +
            "추가 이동을 종료했습니다.\n" +
            "이 캐릭터는 이번 턴에 더 이상 이동할 수 없습니다.";

        battleManager.ResolveMyActionUsedFromExternal(message);
    }

    private void ClearPendingDoubleStepMove()
    {
        pendingDoubleStepMoveFromSlot = null;
        pendingDoubleStepMoveCard = null;
        pendingDoubleStepFirstMoveMessage = "";
    }

    private bool IsDoubleStepMoveCharacter(BaseCardData card)
    {
        CharacterCardData character = card as CharacterCardData;

        if (character == null || character.effects == null)
            return false;

        foreach (EffectData effect in character.effects)
        {
            if (string.Equals(
                    GetEffectRef(effect),
                    "character.passive.doubleStepMoveNoJump",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string GetEffectRef(EffectData effect)
    {
        if (effect == null)
            return "";

        if (!string.IsNullOrWhiteSpace(effect.refName))
            return effect.refName;

        return effect.@ref;
    }

    private bool CanStartMoveFromSlot(
        BattleFieldSlot fromSlot,
        BaseCardData card,
        out string failReason)
    {
        failReason = "";

        if (battleManager == null)
        {
            failReason = "BattleManager가 연결되어 있지 않습니다.";
            return false;
        }

        if (!battleManager.CanUseMyActionFromExternal(out failReason))
            return false;

        if (battleManager.BattleQuestionPanel != null &&
            battleManager.BattleQuestionPanel.IsOpen())
        {
            failReason = "이미 다른 선택창이 열려 있습니다.";
            return false;
        }

        if (fromSlot == null)
        {
            failReason = "이동할 슬롯이 없습니다.";
            return false;
        }

        if (card == null)
        {
            failReason = "이동할 캐릭터 카드가 없습니다.";
            return false;
        }

        if (fromSlot.characterOwner != BattleSlotOwner.My)
        {
            failReason = "현재는 내 캐릭터만 이동할 수 있습니다.";
            return false;
        }

        if (!fromSlot.HasCharacter)
        {
            failReason = "선택한 슬롯에 캐릭터가 없습니다.";
            return false;
        }

        if (fromSlot.characterCard != card)
        {
            failReason = "슬롯의 캐릭터 정보가 일치하지 않습니다.";
            return false;
        }

        if (fromSlot.isCharacterFaceDown)
        {
            failReason = "뒷면 캐릭터는 이동할 수 없습니다.";
            return false;
        }

        int currentTurn = battleManager.GetCurrentTurnCountFromExternal();
        if (fromSlot.faceUpSummonedTurn >= 0 &&
            currentTurn <= fromSlot.faceUpSummonedTurn &&
            !battleManager.CanIgnoreAppearTurnActionLimitFromExternal(fromSlot))
        {
            failReason = "앞면으로 출연한 턴에는 이동할 수 없습니다.";
            return false;
        }

        if (fromSlot.characterMovedThisTurn)
        {
            failReason = "이 캐릭터는 이번 턴 이동할 수 없습니다.";
            return false;
        }

        if (battleManager.IsCharacterMoveLockedByBroadcastFromExternal(fromSlot, out failReason))
            return false;

        if (battleManager.IsMoveForbiddenByBroadcastMoveAndKoLockFromExternal(fromSlot, out failReason))
            return false;

        if (card.kind != "Character")
        {
            failReason = "캐릭터 카드만 이동할 수 있습니다.";
            return false;
        }

        return true;
    }

    private bool CanMoveToSlot(
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        out string failReason)
    {
        failReason = "";

        if (fromSlot == null || toSlot == null)
        {
            failReason = "이동 출발 슬롯 또는 대상 슬롯이 없습니다.";
            return false;
        }

        if (fromSlot == toSlot)
        {
            failReason = "같은 슬롯으로는 이동할 수 없습니다.";
            return false;
        }

        if (fromSlot.characterOwner != BattleSlotOwner.My)
        {
            failReason = "현재는 내 캐릭터만 이동할 수 있습니다.";
            return false;
        }

        if (battleManager.IsMoveForbiddenByBroadcastMoveAndKoLockFromExternal(fromSlot, out failReason))
            return false;

        if (!toSlot.HasBroadcast)
        {
            failReason = "방송 카드가 설치된 슬롯으로만 이동할 수 있습니다.";
            return false;
        }

        if (toSlot.HasCharacter)
        {
            if (toSlot.characterOwner == BattleSlotOwner.Enemy)
                failReason = "상대 캐릭터가 있는 슬롯 진입은 다음 합방 단계에서 구현합니다.";
            else
                failReason = "이미 아군 캐릭터가 있는 슬롯으로는 이동할 수 없습니다.";

            return false;
        }

        return CanReachMoveTarget(fromSlot, toSlot, out failReason);
    }

    private bool CanStartMoveFromSlotForOwner(
        BattleSlotOwner actingOwner,
        BattleFieldSlot fromSlot,
        BaseCardData card,
        out string failReason)
    {
        failReason = "";

        if (battleManager == null)
        {
            failReason = "BattleManager가 연결되어 있지 않습니다.";
            return false;
        }

        if (fromSlot == null)
        {
            failReason = "이동할 슬롯이 없습니다.";
            return false;
        }

        if (card == null)
        {
            failReason = "이동할 캐릭터 카드가 없습니다.";
            return false;
        }

        if (fromSlot.characterOwner != actingOwner)
        {
            failReason = "이동할 캐릭터의 소유자가 행동 주체와 일치하지 않습니다.";
            return false;
        }

        if (!fromSlot.HasCharacter)
        {
            failReason = "선택한 슬롯에 캐릭터가 없습니다.";
            return false;
        }

        if (fromSlot.characterCard != card)
        {
            failReason = "슬롯의 캐릭터 정보가 일치하지 않습니다.";
            return false;
        }

        if (fromSlot.isCharacterFaceDown)
        {
            failReason = "뒷면 캐릭터는 이동할 수 없습니다.";
            return false;
        }

        int currentTurn = battleManager.GetCurrentTurnCountFromExternal();
        if (fromSlot.faceUpSummonedTurn >= 0 &&
            currentTurn <= fromSlot.faceUpSummonedTurn &&
            !battleManager.CanIgnoreAppearTurnActionLimitFromExternal(fromSlot))
        {
            failReason = "앞면으로 출연한 턴에는 이동할 수 없습니다.";
            return false;
        }

        if (fromSlot.characterMovedThisTurn)
        {
            failReason = "이 캐릭터는 이번 턴 이동할 수 없습니다.";
            return false;
        }

        if (battleManager.IsCharacterMoveLockedByBroadcastFromExternal(fromSlot, out failReason))
            return false;

        if (battleManager.IsMoveForbiddenByBroadcastMoveAndKoLockFromExternal(fromSlot, out failReason))
            return false;

        if (card.kind != "Character")
        {
            failReason = "캐릭터 카드만 이동할 수 있습니다.";
            return false;
        }

        return true;
    }

    private bool CanMoveToSlotForOwner(
        BattleSlotOwner actingOwner,
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        out string failReason)
    {
        failReason = "";

        if (fromSlot == null || toSlot == null)
        {
            failReason = "이동 출발 슬롯 또는 대상 슬롯이 없습니다.";
            return false;
        }

        if (fromSlot == toSlot)
        {
            failReason = "같은 슬롯으로는 이동할 수 없습니다.";
            return false;
        }

        if (fromSlot.characterOwner != actingOwner)
        {
            failReason = "이동할 캐릭터의 소유자가 행동 주체와 일치하지 않습니다.";
            return false;
        }

        if (battleManager.IsMoveForbiddenByBroadcastMoveAndKoLockFromExternal(fromSlot, out failReason))
            return false;

        if (!toSlot.HasBroadcast)
        {
            failReason = "방송 카드가 설치된 슬롯으로만 이동할 수 있습니다.";
            return false;
        }

        if (toSlot.HasCharacter)
        {
            if (toSlot.characterOwner != actingOwner)
                failReason = "상대 캐릭터가 있는 슬롯 진입은 StartCollabAction 단계에서 처리해야 합니다.";
            else
                failReason = "이미 아군 캐릭터가 있는 슬롯으로는 이동할 수 없습니다.";

            return false;
        }

        return CanReachMoveTarget(fromSlot, toSlot, out failReason);
    }

    private bool CanStartCollaborationAtSlot(
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        out string failReason)
    {
        return CanStartCollaborationAtSlot(BattleSlotOwner.My, fromSlot, toSlot, false, out failReason);
    }

    private bool CanStartCollaborationAtSlot(
        BattleSlotOwner actingOwner,
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        bool validateSourceActionState,
        out string failReason)
    {
        failReason = "";

        if (fromSlot == null || toSlot == null)
        {
            failReason = "합방 출발 슬롯 또는 대상 슬롯이 없습니다.";
            return false;
        }

        if (fromSlot == toSlot)
        {
            failReason = "같은 슬롯에서는 합방할 수 없습니다.";
            return false;
        }

        if (!fromSlot.HasCharacter)
        {
            failReason = "합방을 시도할 캐릭터가 없습니다.";
            return false;
        }

        if (fromSlot.characterOwner != actingOwner)
        {
            failReason = actingOwner == BattleSlotOwner.My
                ? "현재는 내 캐릭터만 합방을 시도할 수 있습니다."
                : "현재는 상대 캐릭터만 합방을 시도할 수 있습니다.";
            return false;
        }

        if (fromSlot.isCharacterFaceDown)
        {
            failReason = "이 캐릭터는 합방을 시도할 수 없습니다.";
            return false;
        }

        BaseCardData fromCard = fromSlot.characterCard;
        if (fromCard == null || fromCard.kind != "Character")
        {
            failReason = "캐릭터 카드만 합방을 시도할 수 있습니다.";
            return false;
        }

        if (validateSourceActionState)
        {
            int currentTurn = battleManager.GetCurrentTurnCountFromExternal();
            if (fromSlot.faceUpSummonedTurn >= 0 &&
                currentTurn <= fromSlot.faceUpSummonedTurn &&
                !battleManager.CanIgnoreAppearTurnActionLimitFromExternal(fromSlot))
            {
                failReason = "앞면으로 출연한 턴에는 합방할 수 없습니다.";
                return false;
            }

            if (fromSlot.characterMovedThisTurn)
            {
                failReason = "이 캐릭터는 이번 턴 이동할 수 없습니다.";
                return false;
            }

            if (battleManager.IsCharacterMoveLockedByBroadcastFromExternal(fromSlot, out failReason))
                return false;
        }

        if (battleManager.IsMoveForbiddenByBroadcastMoveAndKoLockFromExternal(fromSlot, out failReason))
            return false;

        if (battleManager.IsCollabAttackForbiddenFromExternal(fromSlot))
        {
            failReason = "효과로 인해 이번 턴 합방할 수 없습니다.";
            return false;
        }

        if (!toSlot.HasBroadcast)
        {
            failReason = "방송 카드가 설치된 슬롯에서만 합방할 수 있습니다.";
            return false;
        }

        if (!toSlot.HasCharacter ||
            toSlot.characterOwner == actingOwner)
        {
            failReason = "상대 캐릭터가 있는 슬롯에서만 합방할 수 있습니다.";
            return false;
        }

        BaseCardData toCard = toSlot.characterCard;
        if (toCard == null || toCard.kind != "Character")
        {
            failReason = "합방 대상은 캐릭터 카드여야 합니다.";
            return false;
        }

        return CanReachMoveTarget(fromSlot, toSlot, out failReason);
    }

    private bool CanReachMoveTarget(
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        out string failReason)
    {
        failReason = "";

        int distance =
            Mathf.Abs(fromSlot.x - toSlot.x) +
            Mathf.Abs(fromSlot.y - toSlot.y);

        // 같은 방송 플랫폼 안에서의 이동
        // 예: 내 필드 안 이동, 상대 필드 안 이동
        if (fromSlot.owner == toSlot.owner)
        {
            if (distance != 1)
            {
                failReason = "같은 방송 플랫폼 안에서는 상하좌우로 인접한 슬롯으로만 이동할 수 있습니다.";
                return false;
            }

            return true;
        }

        // 내 방송 플랫폼 ↔ 상대 방송 플랫폼 사이 이동
        if (IsAdjacentAcrossFields(fromSlot, toSlot))
            return true;

        failReason = "상대 방송 플랫폼으로는 맞닿은 전방 슬롯으로만 이동할 수 있습니다.";
        return false;
    }

    private bool IsAdjacentAcrossFields(BattleFieldSlot fromSlot, BattleFieldSlot toSlot)
    {
        if (fromSlot == null || toSlot == null)
            return false;

        if (fromSlot.owner == toSlot.owner)
            return false;

        int mirroredX = 4 - fromSlot.x;

        // 2x3 필드 기준:
        // 각자 자기 관점의 전방 줄이 y=2라고 가정
        bool isFrontRowConnected =
            fromSlot.y == 2 &&
            toSlot.y == 2;

        return toSlot.x == mirroredX && isFrontRowConnected;
    }

    private void HighlightMovableSlots(BattleFieldSlot fromSlot)
    {
        ClearMoveHighlights();

        if (battleManager == null || fromSlot == null)
            return;

        HighlightMovableSlotsForSide(fromSlot, BattlePlayerSide.My);
        HighlightMovableSlotsForSide(fromSlot, BattlePlayerSide.Enemy);
    }

    private void HighlightMovableSlotsForSide(
        BattleFieldSlot fromSlot,
        BattlePlayerSide side)
    {
        IReadOnlyList<BattleFieldSlot> slots =
            battleManager.GetSlotsForMovement(side);

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot == null)
                continue;

            string failReason;
            if (CanMoveToSlot(fromSlot, slot, out failReason))
            {
                slot.SetMoveHighlightVisible(true);
                highlightedSlots.Add(slot);
                continue;
            }

            if (CanStartCollaborationAtSlot(fromSlot, slot, out failReason))
            {
                slot.SetMoveHighlightVisible(true, true);
                highlightedSlots.Add(slot);
            }
        }
    }

        private void ClearMoveHighlights()
        {
            foreach (BattleFieldSlot slot in highlightedSlots)
            {
                if (slot != null)
                    slot.SetMoveHighlightVisible(false);
            }

            highlightedSlots.Clear();
        }

        private void ClearAllMoveState()
        {
            ClearMoveHighlights();

            draggingFromSlot = null;
            draggingCard = null;
            isDraggingMoveCard = false;

            ClearPendingMoveChoiceState();
            ClearPendingDoubleStepMove();
        }

        private void ClearPendingMoveChoiceState()
        {
            pendingMoveFromSlot = null;
            pendingMoveToSlot = null;
            pendingMoveCard = null;
        }

        public void CancelMoveStateFromExternal()
        {
            ClearAllMoveState();
        }
        public void ResetAllCharacterMoveFlagsForNewTurn()
    {
        ResetCharacterMoveFlags(BattlePlayerSide.My);
        ResetCharacterMoveFlags(BattlePlayerSide.Enemy);
    }

    private void ResetCharacterMoveFlags(BattlePlayerSide side)
    {
        if (battleManager == null)
            return;

        IReadOnlyList<BattleFieldSlot> slots =
            battleManager.GetSlotsForMovement(side);

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot == null)
                continue;

            if (!slot.HasCharacter)
                continue;

            slot.SetCharacterMovedThisTurn(false);
        }
    }

}
