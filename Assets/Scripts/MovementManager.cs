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

    public bool IsDraggingMoveCard => isDraggingMoveCard;

    public void Init(BattleManager manager)
    {
        battleManager = manager;
        ClearAllMoveState();
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

        questionPanel.ShowYesNoQuestion(
            "이동하시겠습니까?",
            ConfirmPendingMove,
            CancelPendingMove,
            CancelPendingMove
        );

        battleManager.SetSystemMessageFromExternal(
            $"{card.name} 카드를 ({fromSlot.x}, {fromSlot.y})에서 " +
            $"({toSlot.x}, {toSlot.y})로 이동할 수 있습니다."
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

        ExecuteMove(pendingMoveFromSlot, pendingMoveToSlot, pendingMoveCard);
    }

    private void CancelPendingMove()
    {
        string cardName = pendingMoveCard != null ? pendingMoveCard.name : "캐릭터";

        ClearAllMoveState();

        battleManager.SetSystemMessageFromExternal(
            $"{cardName} 이동을 취소했습니다."
        );
    }

    private void ExecuteMove(
        BattleFieldSlot fromSlot,
        BattleFieldSlot toSlot,
        BaseCardData card)
    {
        Sprite currentSprite = fromSlot.GetCurrentCharacterSprite();
        bool wasFaceDown = fromSlot.isCharacterFaceDown;

        toSlot.SetCharacterCard(card, currentSprite, wasFaceDown);
        toSlot.SetCharacterMovedThisTurn(true);

        fromSlot.ClearCharacterCard();

        string message =
            $"{card.name} 카드를 이동했습니다.\n" +
            $"이동 전: ({fromSlot.x}, {fromSlot.y})\n" +
            $"이동 후: ({toSlot.x}, {toSlot.y})\n" +
            "이 캐릭터는 이번 턴에 더 이상 이동할 수 없습니다.";

        ClearAllMoveState();

        battleManager.RefreshAllUIFromExternal();
        battleManager.ResolveMyActionUsedFromExternal(message);
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

        if (fromSlot.owner != BattleSlotOwner.My)
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
            failReason = "뒷면 캐릭터는 아직 이동할 수 없습니다.";
            return false;
        }

        if (fromSlot.characterMovedThisTurn)
        {
            failReason = "이 캐릭터는 이번 턴에 이미 이동했습니다.";
            return false;
        }

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

        if (toSlot.owner != BattleSlotOwner.My)
        {
            failReason = "이번 단계에서는 내 필드 안에서만 이동할 수 있습니다.";
            return false;
        }

        if (!toSlot.HasBroadcast)
        {
            failReason = "방송 카드가 설치된 슬롯으로만 이동할 수 있습니다.";
            return false;
        }

        if (toSlot.HasCharacter)
        {
            failReason = "이미 캐릭터가 있는 슬롯으로는 이동할 수 없습니다.";
            return false;
        }

        int distance =
            Mathf.Abs(fromSlot.x - toSlot.x) +
            Mathf.Abs(fromSlot.y - toSlot.y);

        if (distance != 1)
        {
            failReason = "상하좌우로 인접한 슬롯으로만 이동할 수 있습니다.";
            return false;
        }

        return true;
    }

    private void HighlightMovableSlots(BattleFieldSlot fromSlot)
        {
            ClearMoveHighlights();

            if (battleManager == null || fromSlot == null)
                return;

            IReadOnlyList<BattleFieldSlot> slots =
                battleManager.GetSlotsForMovement(BattlePlayerSide.My);

            foreach (BattleFieldSlot slot in slots)
            {
                if (slot == null)
                    continue;

                string failReason;
                if (!CanMoveToSlot(fromSlot, slot, out failReason))
                    continue;

                slot.SetMoveHighlightVisible(true);
                highlightedSlots.Add(slot);
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

            pendingMoveFromSlot = null;
            pendingMoveToSlot = null;
            pendingMoveCard = null;
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