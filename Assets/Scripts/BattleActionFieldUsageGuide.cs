using UnityEngine;

public static class BattleActionFieldUsageGuide
{
    public static void LogSummary()
    {
        Debug.Log(
            "[BattleActionFieldUsageGuide]\n" +
            "EndTurn: uses actionSequence, actor, actionType. Online-safe: actor/actionSequence. Index risk: none.\n" +
            "SummonFaceUp: uses cardInstanceId preferred, handIndex fallback, targetSlotId. Online-safe: cardInstanceId,targetSlotId. Risk: handIndex fallback.\n" +
            "SummonFaceDown: uses cardInstanceId preferred, handIndex fallback, targetSlotId. Online-safe: cardInstanceId,targetSlotId. Risk: handIndex fallback.\n" +
            "FlipSummon: uses sourceSlotId; cardInstanceId is logged for source validation. Online-safe: sourceSlotId/cardInstanceId. Risk: none currently.\n" +
            "MoveCharacter: uses sourceSlotId,targetSlotId; cardInstanceId is logged for source validation. Online-safe: slot IDs. Risk: none currently.\n" +
            "StartCollab: uses sourceSlotId,targetSlotId; cardInstanceId is logged for source validation. Online-safe: slot IDs. Risk: none currently.\n" +
            "UseContent: uses cardInstanceId preferred, handIndex fallback, effectRef,effectTiming. Online-safe: cardInstanceId,effectRef,effectTiming. Risk: handIndex fallback.\n" +
            "UseCharacterActive: uses sourceSlotId,effectRef; cardInstanceId is logged for source validation. Online-safe: sourceSlotId/cardInstanceId,effectRef. Risk: none currently.\n" +
            "UseIdolActive: uses actor,effectRef; cardInstanceId is logged for source validation. Online-safe: actor/cardInstanceId,effectRef. Risk: none currently.\n" +
            "SelectEffectTarget: uses selectedTargetIds/targetSlotId,effectRef. Online-safe: selectedTargetIds as slot IDs. Risk: targetSlotId duplication only.\n" +
            "SelectCardOption: uses selectedCardIds preferred, selectedIndexes fallback,effectRef. Online-safe: selectedCardIds when populated with cardInstanceId. Risk: selectedIndexes fallback and card id fallback for old actions.\n" +
            "SelectMultipleCardOptions: selectedCardIds should be preferred when pending implementation is added; selectedIndexes should remain fallback/order verification. Current executor path is not implemented.\n" +
            "SelectEffectChoice: uses effectRef,choiceId,choiceValue. Online-safe: choiceId/choiceValue. Risk: none.\n" +
            "Surrender: action type exists; executor handling is not implemented yet. Online-safe fields should be actionSequence,actor,actionType."
        );
    }
}
