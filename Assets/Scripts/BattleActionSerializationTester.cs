using System.Collections.Generic;
using UnityEngine;

public static class BattleActionSerializationTester
{
    public static bool RunAll(BattleManager battleManager = null)
    {
        List<BattleAction> samples = CreateSamples();
        bool allPassed = true;
        BattleAction restoredEndTurnAction = null;

        Debug.Log($"[BattleActionSerializationTester] Start serialization tests. count={samples.Count}");

        foreach (BattleAction original in samples)
        {
            string json = BattleActionSerializer.ToJson(original);
            Debug.Log($"[BattleActionSerializationTester] {original.actionType} JSON: {json}");

            BattleAction restored = BattleActionSerializer.FromJson(json);
            bool passed = AreEqual(original, restored, original.actionType.ToString());

            if (!passed)
                allPassed = false;

            Debug.Log($"[BattleActionSerializationTester] {original.actionType} field preservation result={passed}");

            if (original.actionType == BattleActionType.EndTurn)
                restoredEndTurnAction = restored;
        }

        Debug.Log($"[BattleActionSerializationTester] Serialization tests completed. allPassed={allPassed}");

        if (allPassed)
            TryExecuteRestoredEndTurn(battleManager, restoredEndTurnAction);
        else
            Debug.LogWarning("[BattleActionSerializationTester] Skip executor test because field preservation failed.");

        return allPassed;
    }

    private static List<BattleAction> CreateSamples()
    {
        return new List<BattleAction>
        {
            CreateAction(1, BattleSlotOwner.My, BattleActionType.EndTurn),
            CreateAction(2, BattleSlotOwner.My, BattleActionType.SummonFaceDown, handIndex: 0, cardInstanceId: "my-hand-card-001", targetSlotId: "My_1_1"),
            CreateAction(3, BattleSlotOwner.My, BattleActionType.SummonFaceUp, handIndex: 1, cardInstanceId: "my-hand-card-002", targetSlotId: "My_2_1"),
            CreateAction(4, BattleSlotOwner.My, BattleActionType.FlipSummon, sourceSlotId: "My_1_1"),
            CreateAction(5, BattleSlotOwner.My, BattleActionType.MoveCharacter, sourceSlotId: "My_1_1", targetSlotId: "My_1_2"),
            CreateAction(6, BattleSlotOwner.My, BattleActionType.StartCollab, sourceSlotId: "My_1_1", targetSlotId: "My_2_1"),
            CreateAction(7, BattleSlotOwner.My, BattleActionType.UseContent, handIndex: 2, cardInstanceId: "my-content-001", effectRef: "CNTS-TEST-001.content.0", effectTiming: EffectTiming.Content),
            CreateAction(8, BattleSlotOwner.My, BattleActionType.UseCharacterActive, sourceSlotId: "My_3_1", effectRef: "CHAR-TEST-001.active.0", effectTiming: EffectTiming.CharacterActive),
            CreateAction(9, BattleSlotOwner.My, BattleActionType.UseIdolActive, effectRef: "IDOL-TEST-001.active.0", effectTiming: EffectTiming.IdolActive),
            CreateAction(10, BattleSlotOwner.My, BattleActionType.SelectEffectTarget, sourceSlotId: "My_3_1", targetSlotId: "Enemy_1_1", effectRef: "CHAR-TEST-001.active.0", effectTiming: EffectTiming.CharacterActive, selectedTargetIds: new List<string> { "Enemy_1_1", "Enemy_2_1" }),
            CreateAction(11, BattleSlotOwner.My, BattleActionType.SelectCardOption, effectRef: "CNTS-TEST-001.content.1", effectTiming: EffectTiming.Content, selectedCardIds: new List<string> { "choice-card-001" }, selectedIndexes: new List<int> { 0 }),
            CreateAction(12, BattleSlotOwner.My, BattleActionType.SelectMultipleCardOptions, effectRef: "CNTS-TEST-001.content.2", effectTiming: EffectTiming.Content, selectedCardIds: new List<string> { "choice-card-001", "choice-card-002" }, selectedIndexes: new List<int> { 2, 0 }),
            CreateAction(13, BattleSlotOwner.My, BattleActionType.SelectEffectChoice, effectRef: "CNTS-TEST-001.content.3", effectTiming: EffectTiming.Content, choiceId: "draw-or-viewer", choiceValue: "draw"),
            CreateAction(14, BattleSlotOwner.My, BattleActionType.Surrender, choiceId: "surrender", choiceValue: "confirm")
        };
    }

    private static BattleAction CreateAction(
        int actionSequence,
        BattleSlotOwner actor,
        BattleActionType actionType,
        int handIndex = -1,
        string cardInstanceId = "",
        string sourceSlotId = "",
        string targetSlotId = "",
        string effectRef = "",
        EffectTiming effectTiming = EffectTiming.None,
        List<string> selectedTargetIds = null,
        List<string> selectedCardIds = null,
        List<int> selectedIndexes = null,
        string choiceId = "",
        string choiceValue = "")
    {
        return new BattleAction
        {
            actionSequence = actionSequence,
            actor = actor,
            actionType = actionType,
            handIndex = handIndex,
            cardInstanceId = cardInstanceId,
            sourceSlotId = sourceSlotId,
            targetSlotId = targetSlotId,
            effectRef = effectRef,
            effectTiming = effectTiming,
            selectedTargetIds = selectedTargetIds ?? new List<string>(),
            selectedCardIds = selectedCardIds ?? new List<string>(),
            selectedIndexes = selectedIndexes ?? new List<int>(),
            choiceId = choiceId,
            choiceValue = choiceValue
        };
    }

    private static bool AreEqual(BattleAction expected, BattleAction actual, string label)
    {
        bool result = true;

        if (expected == null || actual == null)
        {
            Debug.LogWarning($"[BattleActionSerializationTester] {label}: null mismatch. expectedNull={expected == null}, actualNull={actual == null}");
            return false;
        }

        CompareField(label, "actionSequence", expected.actionSequence, actual.actionSequence, ref result);
        CompareField(label, "actor", expected.actor, actual.actor, ref result);
        CompareField(label, "actionType", expected.actionType, actual.actionType, ref result);
        CompareField(label, "handIndex", expected.handIndex, actual.handIndex, ref result);
        CompareField(label, "cardInstanceId", expected.cardInstanceId, actual.cardInstanceId, ref result);
        CompareField(label, "sourceSlotId", expected.sourceSlotId, actual.sourceSlotId, ref result);
        CompareField(label, "targetSlotId", expected.targetSlotId, actual.targetSlotId, ref result);
        CompareField(label, "effectRef", expected.effectRef, actual.effectRef, ref result);
        CompareField(label, "effectTiming", expected.effectTiming, actual.effectTiming, ref result);
        CompareStringList(label, "selectedTargetIds", expected.selectedTargetIds, actual.selectedTargetIds, ref result);
        CompareStringList(label, "selectedCardIds", expected.selectedCardIds, actual.selectedCardIds, ref result);
        CompareIntList(label, "selectedIndexes", expected.selectedIndexes, actual.selectedIndexes, ref result);
        CompareField(label, "choiceId", expected.choiceId, actual.choiceId, ref result);
        CompareField(label, "choiceValue", expected.choiceValue, actual.choiceValue, ref result);

        return result;
    }

    private static void CompareField<T>(string label, string fieldName, T expected, T actual, ref bool result)
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual))
            return;

        Debug.LogWarning($"[BattleActionSerializationTester] {label}: {fieldName} mismatch. expected={expected}, actual={actual}");
        result = false;
    }

    private static void CompareStringList(string label, string fieldName, List<string> expected, List<string> actual, ref bool result)
    {
        int expectedCount = expected != null ? expected.Count : -1;
        int actualCount = actual != null ? actual.Count : -1;

        if (expectedCount != actualCount)
        {
            Debug.LogWarning($"[BattleActionSerializationTester] {label}: {fieldName}.Count mismatch. expected={expectedCount}, actual={actualCount}");
            result = false;
            return;
        }

        if (expected == null)
            return;

        for (int i = 0; i < expected.Count; i++)
        {
            if (expected[i] == actual[i])
                continue;

            Debug.LogWarning($"[BattleActionSerializationTester] {label}: {fieldName}[{i}] mismatch. expected={expected[i]}, actual={actual[i]}");
            result = false;
        }
    }

    private static void CompareIntList(string label, string fieldName, List<int> expected, List<int> actual, ref bool result)
    {
        int expectedCount = expected != null ? expected.Count : -1;
        int actualCount = actual != null ? actual.Count : -1;

        if (expectedCount != actualCount)
        {
            Debug.LogWarning($"[BattleActionSerializationTester] {label}: {fieldName}.Count mismatch. expected={expectedCount}, actual={actualCount}");
            result = false;
            return;
        }

        if (expected == null)
            return;

        for (int i = 0; i < expected.Count; i++)
        {
            if (expected[i] == actual[i])
                continue;

            Debug.LogWarning($"[BattleActionSerializationTester] {label}: {fieldName}[{i}] mismatch. expected={expected[i]}, actual={actual[i]}");
            result = false;
        }
    }

    private static void TryExecuteRestoredEndTurn(BattleManager battleManager, BattleAction restoredEndTurnAction)
    {
        if (battleManager == null)
        {
            Debug.LogWarning("[BattleActionSerializationTester] EndTurn executor test skipped: BattleManager is null.");
            return;
        }

        if (restoredEndTurnAction == null)
        {
            Debug.LogWarning("[BattleActionSerializationTester] EndTurn executor test skipped: restored action is null.");
            return;
        }

        try
        {
            BattleActionExecutor executor = new BattleActionExecutor(battleManager);
            bool executed = executor.ExecuteAction(restoredEndTurnAction);
            Debug.Log($"[BattleActionSerializationTester] Restored EndTurn executor test attempted. result={executed}");

            if (!executed)
                Debug.LogWarning("[BattleActionSerializationTester] Restored EndTurn executor test returned false. This can be caused by current battle state and is separate from serialization preservation.");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[BattleActionSerializationTester] Restored EndTurn executor test failed with exception. This is separate from serialization preservation. reason={ex.Message}");
        }
    }
}
