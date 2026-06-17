using System.Collections.Generic;
using UnityEngine;

public static class BattleActionValidatorTester
{
    public static bool Run(BattleManager battleManager)
    {
        if (battleManager == null)
        {
            Debug.LogWarning("[BattleActionValidatorTester] BattleManager is null.");
            return false;
        }

        BattleActionValidator validator = new BattleActionValidator(battleManager);
        BattleSlotOwner actor = battleManager.CurrentActionSideFromExternal == BattlePlayerSide.Enemy
            ? BattleSlotOwner.Enemy
            : BattleSlotOwner.My;

        bool allExpectedResultsMatched = true;

        allExpectedResultsMatched &= LogValidation(
            validator,
            "null action",
            null,
            false);

        allExpectedResultsMatched &= LogValidation(
            validator,
            "valid-ish EndTurn",
            new BattleAction
            {
                actionSequence = 9001,
                actor = actor,
                actionType = BattleActionType.EndTurn
            },
            true,
            allowStateDependentFailure: true);

        allExpectedResultsMatched &= LogValidation(
            validator,
            "missing cardInstanceId SummonFaceDown",
            new BattleAction
            {
                actionSequence = 9002,
                actor = actor,
                actionType = BattleActionType.SummonFaceDown,
                cardInstanceId = "missing-card-instance",
                targetSlotId = "missing-slot"
            },
            false);

        allExpectedResultsMatched &= LogValidation(
            validator,
            "missing targetSlotId SummonFaceUp",
            CreateActionWithFirstHandCard(
                battleManager,
                actor,
                BattleActionType.SummonFaceUp,
                "missing-slot"),
            false);

        BattleAction wrongTypeSummon = CreateActionWithFirstHandCardKind(
            battleManager,
            actor,
            "Content",
            BattleActionType.SummonFaceUp,
            FindFirstEmptyOwnedBroadcastSlotId(battleManager, actor));

        if (wrongTypeSummon != null)
        {
            allExpectedResultsMatched &= LogValidation(
                validator,
                "wrong card type SummonFaceUp",
                wrongTypeSummon,
                false);
        }
        else
        {
            Debug.Log("[BattleActionValidatorTester] Skip wrong card type sample: no Content card in current actor hand.");
        }

        BattleAction insufficientCostSummon = CreateInsufficientCostSummonAction(battleManager, actor);
        if (insufficientCostSummon != null)
        {
            allExpectedResultsMatched &= LogValidation(
                validator,
                "insufficient cost SummonFaceUp",
                insufficientCostSummon,
                false);
        }
        else
        {
            Debug.Log("[BattleActionValidatorTester] Skip insufficient cost sample: no hand character with appearCost above current viewers.");
        }

        BattleAction validSummon = CreateActionWithFirstHandCardKind(
            battleManager,
            actor,
            "Character",
            BattleActionType.SummonFaceDown,
            FindFirstEmptyOwnedBroadcastSlotId(battleManager, actor));

        if (validSummon != null)
        {
            allExpectedResultsMatched &= LogValidation(
                validator,
                "valid-ish SummonFaceDown",
                validSummon,
                true,
                allowStateDependentFailure: true);
        }
        else
        {
            Debug.Log("[BattleActionValidatorTester] Skip valid SummonFaceDown sample: no hand character or empty owned broadcast slot.");
        }

        Debug.Log($"[BattleActionValidatorTester] Completed. allExpectedResultsMatched={allExpectedResultsMatched}");
        return allExpectedResultsMatched;
    }

    private static bool LogValidation(
        BattleActionValidator validator,
        string label,
        BattleAction action,
        bool expectedValid,
        bool allowStateDependentFailure = false)
    {
        BattleActionValidationResult result = validator.Validate(action);
        bool actualValid = result != null && result.isValid;
        string reason = result != null ? result.reason : "result is null";
        bool matched = actualValid == expectedValid || (allowStateDependentFailure && !actualValid);

        Debug.Log(
            $"[BattleActionValidatorTester] {label}: " +
            $"expectedValid={expectedValid}, actualValid={actualValid}, matched={matched}, reason={reason}");

        if (!matched)
            Debug.LogWarning($"[BattleActionValidatorTester] Unexpected validation result for {label}: {reason}");

        return matched;
    }

    private static BattleAction CreateActionWithFirstHandCard(
        BattleManager battleManager,
        BattleSlotOwner actor,
        BattleActionType actionType,
        string targetSlotId)
    {
        IReadOnlyList<BaseCardData> hand = battleManager.GetHandCardsFromExternal(actor);
        BaseCardData card = hand != null && hand.Count > 0 ? hand[0] : null;

        return new BattleAction
        {
            actionSequence = 9100,
            actor = actor,
            actionType = actionType,
            handIndex = 0,
            cardInstanceId = card != null ? card.cardInstanceId : "",
            targetSlotId = targetSlotId ?? ""
        };
    }

    private static BattleAction CreateActionWithFirstHandCardKind(
        BattleManager battleManager,
        BattleSlotOwner actor,
        string kind,
        BattleActionType actionType,
        string targetSlotId)
    {
        if (string.IsNullOrWhiteSpace(targetSlotId))
            return null;

        IReadOnlyList<BaseCardData> hand = battleManager.GetHandCardsFromExternal(actor);
        if (hand == null)
            return null;

        for (int i = 0; i < hand.Count; i++)
        {
            BaseCardData card = hand[i];
            if (card == null || !string.Equals(card.kind, kind, System.StringComparison.OrdinalIgnoreCase))
                continue;

            return new BattleAction
            {
                actionSequence = 9101 + i,
                actor = actor,
                actionType = actionType,
                handIndex = i,
                cardInstanceId = card.cardInstanceId,
                targetSlotId = targetSlotId
            };
        }

        return null;
    }

    private static BattleAction CreateInsufficientCostSummonAction(BattleManager battleManager, BattleSlotOwner actor)
    {
        string targetSlotId = FindFirstEmptyOwnedBroadcastSlotId(battleManager, actor);
        if (string.IsNullOrWhiteSpace(targetSlotId))
            return null;

        IReadOnlyList<BaseCardData> hand = battleManager.GetHandCardsFromExternal(actor);
        if (hand == null)
            return null;

        int viewers = battleManager.GetViewersFromExternal(actor);

        for (int i = 0; i < hand.Count; i++)
        {
            CharacterCardData character = hand[i] as CharacterCardData;
            if (character == null || character.appearCost <= viewers)
                continue;

            return new BattleAction
            {
                actionSequence = 9200 + i,
                actor = actor,
                actionType = BattleActionType.SummonFaceUp,
                handIndex = i,
                cardInstanceId = character.cardInstanceId,
                targetSlotId = targetSlotId
            };
        }

        return null;
    }

    private static string FindFirstEmptyOwnedBroadcastSlotId(BattleManager battleManager, BattleSlotOwner actor)
    {
        IReadOnlyList<BattleFieldSlot> slots = battleManager.GetEmptyOwnedBroadcastSlotsFromExternal(actor);
        if (slots == null || slots.Count <= 0 || slots[0] == null)
            return "";

        return slots[0].GetSlotId();
    }
}
