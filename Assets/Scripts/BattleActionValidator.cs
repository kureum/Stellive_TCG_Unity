using System;
using System.Collections.Generic;

public class BattleActionValidator
{
    private readonly BattleManager battleManager;

    public BattleActionValidator(BattleManager battleManager)
    {
        this.battleManager = battleManager;
    }

    public BattleActionValidationResult Validate(BattleAction action)
    {
        BattleActionValidationResult preflightResult = ValidatePreflight(action);
        if (!preflightResult.isValid)
            return preflightResult;

        switch (action.actionType)
        {
            case BattleActionType.EndTurn:
                return ValidateEndTurn(action);

            case BattleActionType.PlaceBroadcast:
                return ValidatePlaceBroadcast(action);

            case BattleActionType.SummonFaceDown:
                return ValidateSummonFaceDown(action);

            case BattleActionType.SummonFaceUp:
                return ValidateSummonFaceUp(action);

            case BattleActionType.MoveCharacter:
                return ValidateMoveCharacter(action);

            case BattleActionType.UseContent:
                return ValidateUseContent(action);

            case BattleActionType.Surrender:
                return ValidateSurrender(action);

            default:
                return BattleActionValidationResult.Valid();
        }
    }

    private BattleActionValidationResult ValidatePreflight(BattleAction action)
    {
        if (action == null)
            return BattleActionValidationResult.Invalid("BattleAction is null.");

        if (battleManager == null)
            return BattleActionValidationResult.Invalid("BattleManager is null.");

        if (!Enum.IsDefined(typeof(BattleSlotOwner), action.actor))
            return BattleActionValidationResult.Invalid($"Invalid actor: {action.actor}");

        if (!Enum.IsDefined(typeof(BattleActionType), action.actionType))
            return BattleActionValidationResult.Invalid($"Invalid actionType: {action.actionType}");

        if (battleManager.IsGameOverFromExternal() &&
            action.actionType != BattleActionType.Surrender)
        {
            return BattleActionValidationResult.Invalid("Battle is already over.");
        }

        if (action.actionType == BattleActionType.Surrender)
            return BattleActionValidationResult.Valid();

        return BattleActionValidationResult.Valid();
    }

    private BattleActionValidationResult ValidateActionPriority(BattleAction action)
    {
        if (battleManager.CurrentPhaseFromExternal != BattlePhase.MainGame)
            return BattleActionValidationResult.Invalid("Battle is not in MainGame phase.");

        BattlePlayerSide expectedSide = ToPlayerSide(action.actor);
        if (battleManager.CurrentActionSideFromExternal != expectedSide)
        {
            return BattleActionValidationResult.Invalid(
                $"Current action side mismatch. current={battleManager.CurrentActionSideFromExternal}, actor={action.actor}");
        }

        if (battleManager.IsBattleBusyFromExternal())
            return BattleActionValidationResult.Invalid(battleManager.GetBattleBusyReasonFromExternal());

        return BattleActionValidationResult.Valid();
    }

    private BattleActionValidationResult ValidateEndTurn(BattleAction action)
    {
        BattleActionValidationResult priorityResult = ValidateActionPriority(action);
        if (!priorityResult.isValid)
            return priorityResult;

        BattlePlayerSide expectedSide = ToPlayerSide(action.actor);
        if (battleManager.CurrentActionSideFromExternal != expectedSide)
            return BattleActionValidationResult.Invalid("Cannot end turn for a player that does not have action priority.");

        return BattleActionValidationResult.Valid();
    }

    private BattleActionValidationResult ValidatePlaceBroadcast(BattleAction action)
    {
        if (battleManager.CanPlaceBroadcastFromExternal(
                action.actor,
                action.cardInstanceId,
                action.targetSlotId,
                out string failReason))
        {
            return BattleActionValidationResult.Valid();
        }

        return BattleActionValidationResult.Invalid(failReason);
    }

    private BattleActionValidationResult ValidateSummonFaceDown(BattleAction action)
    {
        BattleActionValidationResult priorityResult = ValidateActionPriority(action);
        if (!priorityResult.isValid)
            return priorityResult;

        if (!TryResolveHandCard(action, out BaseCardData card, out int _, out string failReason))
            return BattleActionValidationResult.Invalid(failReason);

        if (!IsCharacterCard(card))
            return BattleActionValidationResult.Invalid("SummonFaceDown requires a Character card.");

        if (!TryResolveTargetSlot(action.targetSlotId, out BattleFieldSlot targetSlot, out failReason))
            return BattleActionValidationResult.Invalid(failReason);

        BattleActionValidationResult slotResult = ValidateSummonTargetSlot(action.actor, targetSlot);
        if (!slotResult.isValid)
            return slotResult;

        if (battleManager.summonManager == null)
            return BattleActionValidationResult.Invalid("SummonManager is null.");

        if (!battleManager.summonManager.CanSummonBacksideFromExternal(card, out failReason))
            return BattleActionValidationResult.Invalid(failReason);

        if (battleManager.IsFaceDownSummonForbiddenByBroadcastFromExternal(targetSlot, out failReason))
            return BattleActionValidationResult.Invalid(failReason);

        return BattleActionValidationResult.Valid();
    }

    private BattleActionValidationResult ValidateSummonFaceUp(BattleAction action)
    {
        BattleActionValidationResult priorityResult = ValidateActionPriority(action);
        if (!priorityResult.isValid)
            return priorityResult;

        if (!TryResolveHandCard(action, out BaseCardData card, out int _, out string failReason))
            return BattleActionValidationResult.Invalid(failReason);

        if (!IsCharacterCard(card))
            return BattleActionValidationResult.Invalid("SummonFaceUp requires a Character card.");

        if (!TryResolveTargetSlot(action.targetSlotId, out BattleFieldSlot targetSlot, out failReason))
            return BattleActionValidationResult.Invalid(failReason);

        BattleActionValidationResult slotResult = ValidateSummonTargetSlot(action.actor, targetSlot);
        if (!slotResult.isValid)
            return slotResult;

        if (battleManager.summonManager == null)
            return BattleActionValidationResult.Invalid("SummonManager is null.");

        int cost = battleManager.summonManager.GetCharacterAppearCostFromExternal(card);
        if (!battleManager.CanPayViewerCostFromExternal(action.actor, cost))
            return BattleActionValidationResult.Invalid($"Not enough viewers for appearCost={cost}.");

        return BattleActionValidationResult.Valid();
    }

    private BattleActionValidationResult ValidateMoveCharacter(BattleAction action)
    {
        BattleActionValidationResult priorityResult = ValidateActionPriority(action);
        if (!priorityResult.isValid)
            return priorityResult;

        if (!TryResolveTargetSlot(action.sourceSlotId, out BattleFieldSlot sourceSlot, out string failReason))
            return BattleActionValidationResult.Invalid($"Invalid sourceSlotId. {failReason}");

        if (!TryResolveTargetSlot(action.targetSlotId, out BattleFieldSlot targetSlot, out failReason))
            return BattleActionValidationResult.Invalid($"Invalid targetSlotId. {failReason}");

        if (!sourceSlot.HasCharacter || sourceSlot.characterCard == null)
            return BattleActionValidationResult.Invalid("Source slot has no character.");

        if (sourceSlot.characterOwner != action.actor)
            return BattleActionValidationResult.Invalid("Source character owner does not match actor.");

        if (sourceSlot.isCharacterFaceDown)
            return BattleActionValidationResult.Invalid("Face-down characters cannot move.");

        if (battleManager.movementManager == null)
            return BattleActionValidationResult.Invalid("MovementManager is null.");

        if (!battleManager.movementManager.CanMoveCharacterFromExternal(sourceSlot, targetSlot, out failReason))
            return BattleActionValidationResult.Invalid(failReason);

        return BattleActionValidationResult.Valid();
    }

    private BattleActionValidationResult ValidateUseContent(BattleAction action)
    {
        BattleActionValidationResult timingResult = ValidateUseContentTiming(action);
        if (!timingResult.isValid)
            return timingResult;

        if (!TryResolveHandCard(action, out BaseCardData card, out int _, out string failReason))
            return BattleActionValidationResult.Invalid(failReason);

        if (!IsContentCard(card))
            return BattleActionValidationResult.Invalid("UseContent requires a Content card.");

        if (action.effectTiming != EffectTiming.Content &&
            action.effectTiming != EffectTiming.PreCollab &&
            action.effectTiming != EffectTiming.PostCollab)
        {
            return BattleActionValidationResult.Invalid($"Unsupported content timing: {action.effectTiming}");
        }

        if (action.effectTiming == EffectTiming.Content &&
            battleManager.CurrentActionSideFromExternal != ToPlayerSide(action.actor))
        {
            return BattleActionValidationResult.Invalid("Content action timing does not match current action side.");
        }

        int cost = GetContentCost(card);
        if (!battleManager.CanPayViewerCostFromExternal(action.actor, cost))
            return BattleActionValidationResult.Invalid($"Not enough viewers for content cost={cost}.");

        if (action.selectedTargetIds != null)
        {
            foreach (string targetId in action.selectedTargetIds)
            {
                if (string.IsNullOrWhiteSpace(targetId))
                    return BattleActionValidationResult.Invalid("selectedTargetIds contains an empty target id.");

                if (battleManager.FindFieldSlotBySlotId(targetId) == null)
                    return BattleActionValidationResult.Invalid($"selectedTargetIds contains an unknown slot id: {targetId}");
            }
        }

        if (action.selectedCardIds != null)
        {
            foreach (string cardId in action.selectedCardIds)
            {
                if (string.IsNullOrWhiteSpace(cardId))
                    return BattleActionValidationResult.Invalid("selectedCardIds contains an empty card id.");
            }
        }

        return BattleActionValidationResult.Valid();
    }

    private BattleActionValidationResult ValidateUseContentTiming(BattleAction action)
    {
        if (battleManager.CurrentPhaseFromExternal != BattlePhase.MainGame)
            return BattleActionValidationResult.Invalid("Battle is not in MainGame phase.");

        bool isCollabContentTiming =
            action.effectTiming == EffectTiming.PreCollab ||
            action.effectTiming == EffectTiming.PostCollab;

        if (!isCollabContentTiming)
        {
            BattlePlayerSide expectedSide = ToPlayerSide(action.actor);
            if (battleManager.CurrentActionSideFromExternal != expectedSide)
            {
                return BattleActionValidationResult.Invalid(
                    $"Current action side mismatch. current={battleManager.CurrentActionSideFromExternal}, actor={action.actor}");
            }

            if (battleManager.IsBattleBusyFromExternal())
                return BattleActionValidationResult.Invalid(battleManager.GetBattleBusyReasonFromExternal());
        }

        return BattleActionValidationResult.Valid();
    }

    private BattleActionValidationResult ValidateSurrender(BattleAction action)
    {
        if (!Enum.IsDefined(typeof(BattleSlotOwner), action.actor))
            return BattleActionValidationResult.Invalid($"Invalid surrender actor: {action.actor}");

        return BattleActionValidationResult.Valid();
    }

    private BattleActionValidationResult ValidateSummonTargetSlot(BattleSlotOwner actor, BattleFieldSlot targetSlot)
    {
        if (targetSlot.owner != actor)
            return BattleActionValidationResult.Invalid("Target slot is not owned by actor.");

        if (!targetSlot.HasBroadcast)
            return BattleActionValidationResult.Invalid("Target slot has no broadcast card.");

        if (targetSlot.HasCharacter)
            return BattleActionValidationResult.Invalid("Target slot already has a character.");

        return BattleActionValidationResult.Valid();
    }

    private bool TryResolveHandCard(
        BattleAction action,
        out BaseCardData card,
        out int handIndex,
        out string failReason)
    {
        card = null;
        handIndex = -1;
        failReason = "";

        IReadOnlyList<BaseCardData> hand = battleManager.GetHandCardsFromExternal(action.actor);
        if (hand == null)
        {
            failReason = "Hand is not available.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(action.cardInstanceId))
        {
            handIndex = battleManager.FindHandIndexByInstanceId(action.actor, action.cardInstanceId);
            if (handIndex < 0)
            {
                failReason = $"Card instance is not in hand: {action.cardInstanceId}";
                return false;
            }

            card = hand[handIndex];
            if (card == null)
            {
                failReason = "Hand card is null.";
                return false;
            }

            return true;
        }

        if (action.handIndex < 0 || action.handIndex >= hand.Count)
        {
            failReason = $"Invalid handIndex: {action.handIndex}";
            return false;
        }

        handIndex = action.handIndex;
        card = hand[handIndex];

        if (card == null)
        {
            failReason = "Hand card is null.";
            return false;
        }

        return true;
    }

    private bool TryResolveTargetSlot(string slotId, out BattleFieldSlot slot, out string failReason)
    {
        slot = null;
        failReason = "";

        if (string.IsNullOrWhiteSpace(slotId))
        {
            failReason = "Slot id is empty.";
            return false;
        }

        slot = battleManager.FindFieldSlotBySlotId(slotId);
        if (slot == null)
        {
            failReason = $"Slot not found: {slotId}";
            return false;
        }

        return true;
    }

    private BattlePlayerSide ToPlayerSide(BattleSlotOwner owner)
    {
        return owner == BattleSlotOwner.My
            ? BattlePlayerSide.My
            : BattlePlayerSide.Enemy;
    }

    private bool IsCharacterCard(BaseCardData card)
    {
        return card != null &&
            string.Equals(card.kind, "Character", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsContentCard(BaseCardData card)
    {
        return card != null &&
            string.Equals(card.kind, "Content", StringComparison.OrdinalIgnoreCase);
    }

    private int GetContentCost(BaseCardData card)
    {
        ContentCardData content = card as ContentCardData;
        return content != null ? Math.Max(0, content.cost) : 0;
    }
}
