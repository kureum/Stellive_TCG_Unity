using System.Collections.Generic;
using UnityEngine;

public static class BattleActionResultTester
{
    public static bool RunAll()
    {
        List<BattleActionResult> samples = new List<BattleActionResult>
        {
            CreateAcceptedSample(),
            CreateRejectedSample(),
            CreateRandomResultSample(),
            CreateDeckOrderSample()
        };

        bool allPassed = true;

        foreach (BattleActionResult original in samples)
        {
            string json = BattleActionResultSerializer.ToJson(original);
            Debug.Log($"[BattleActionResultTester] {original.message} JSON: {json}");

            BattleActionResult restored = BattleActionResultSerializer.FromJson(json);
            bool passed = AreEqual(original, restored, original.message);
            Debug.Log($"[BattleActionResultTester] {original.message} result={passed}");

            if (!passed)
                allPassed = false;
        }

        Debug.Log($"[BattleActionResultTester] Completed. allPassed={allPassed}");
        return allPassed;
    }

    public static bool RunApplyTests(BattleManager battleManager)
    {
        if (battleManager == null)
        {
            Debug.LogWarning("[BattleActionResultTester] Apply tests failed: BattleManager is null.");
            return false;
        }

        BattleActionResultApplier applier = new BattleActionResultApplier(battleManager);
        bool allPassed = true;

        applier.Apply(new BattleActionResult
        {
            actionSequence = 101,
            actor = BattleSlotOwner.My,
            requestActionType = BattleActionType.SummonFaceUp,
            isAccepted = false,
            rejectReason = "Test reject",
            message = "Test rejected result"
        });
        Debug.Log("[BattleActionResultTester] Rejected Result Apply completed.");

        applier.Apply(new BattleActionResult
        {
            actionSequence = 102,
            actor = BattleSlotOwner.My,
            requestActionType = BattleActionType.EndTurn,
            isAccepted = true,
            message = "Test accepted"
        });
        Debug.Log("[BattleActionResultTester] Accepted Message Result Apply completed.");

        allPassed &= RunDeckOrderApplyTest(battleManager, applier);
        RunResolvedRandomCardIdsApplyTest(battleManager, applier);

        Debug.Log($"[BattleActionResultTester] Apply tests completed. allPassed={allPassed}");
        return allPassed;
    }

    private static bool RunDeckOrderApplyTest(
        BattleManager battleManager,
        BattleActionResultApplier applier)
    {
        List<string> originalOrder = battleManager.GetMainDeckOrderIds(BattleSlotOwner.My);
        if (originalOrder == null || originalOrder.Count <= 1)
        {
            Debug.Log("[BattleActionResultTester] DeckOrder Apply skipped: player main deck has 0 or 1 card.");
            return true;
        }

        List<string> reversedOrder = new List<string>(originalOrder);
        reversedOrder.Reverse();

        applier.Apply(new BattleActionResult
        {
            actionSequence = 103,
            actor = BattleSlotOwner.My,
            requestActionType = BattleActionType.EndTurn,
            isAccepted = true,
            message = "Test deck order apply",
            playerMainDeckOrderIds = reversedOrder
        });

        List<string> appliedOrder = battleManager.GetMainDeckOrderIds(BattleSlotOwner.My);
        bool reversedApplied = ListsEqual(reversedOrder, appliedOrder);

        bool restored = battleManager.ApplyMainDeckOrderFromExternal(
            BattleSlotOwner.My,
            originalOrder);

        if (!reversedApplied)
            Debug.LogWarning("[BattleActionResultTester] DeckOrder Apply failed: reversed order was not applied.");

        if (!restored)
            Debug.LogWarning("[BattleActionResultTester] DeckOrder Apply cleanup failed: original order was not restored.");

        Debug.Log($"[BattleActionResultTester] DeckOrder Apply result reversedApplied={reversedApplied}, restored={restored}");
        return reversedApplied && restored;
    }

    private static void RunResolvedRandomCardIdsApplyTest(
        BattleManager battleManager,
        BattleActionResultApplier applier)
    {
        string existingId = FindFirstKnownCardInstanceId(battleManager);
        BattleActionResult result = new BattleActionResult
        {
            actionSequence = 104,
            actor = BattleSlotOwner.My,
            requestActionType = BattleActionType.UseContent,
            isAccepted = true,
            message = "Test resolved random ids",
            resolvedRandomCardIds = new List<string>()
        };

        if (!string.IsNullOrWhiteSpace(existingId))
            result.resolvedRandomCardIds.Add(existingId);

        result.resolvedRandomCardIds.Add("missing-card-instance-for-apply-test");
        applier.Apply(result);
        Debug.Log("[BattleActionResultTester] ResolvedRandomCardIds Apply completed.");
    }

    private static string FindFirstKnownCardInstanceId(BattleManager battleManager)
    {
        List<string> deckOrder = battleManager.GetMainDeckOrderIds(BattleSlotOwner.My);
        if (deckOrder != null && deckOrder.Count > 0)
            return deckOrder[0];

        IReadOnlyList<BaseCardData> hand = battleManager.GetHandCardsFromExternal(BattleSlotOwner.My);
        if (hand != null)
        {
            foreach (BaseCardData card in hand)
            {
                if (card != null && !string.IsNullOrWhiteSpace(card.cardInstanceId))
                    return card.cardInstanceId;
            }
        }

        IReadOnlyList<BaseCardData> restZone = battleManager.GetRestZoneCardsFromExternal(BattleSlotOwner.My);
        if (restZone != null)
        {
            foreach (BaseCardData card in restZone)
            {
                if (card != null && !string.IsNullOrWhiteSpace(card.cardInstanceId))
                    return card.cardInstanceId;
            }
        }

        return "";
    }

    private static BattleActionResult CreateAcceptedSample()
    {
        return new BattleActionResult
        {
            actionSequence = 1,
            actor = BattleSlotOwner.My,
            requestActionType = BattleActionType.EndTurn,
            isAccepted = true,
            message = "Accepted EndTurn",
            firstActor = "My"
        };
    }

    private static BattleActionResult CreateRejectedSample()
    {
        return new BattleActionResult
        {
            actionSequence = 2,
            actor = BattleSlotOwner.Enemy,
            requestActionType = BattleActionType.SummonFaceUp,
            isAccepted = false,
            rejectReason = "Invalid target slot",
            message = "Rejected SummonFaceUp"
        };
    }

    private static BattleActionResult CreateRandomResultSample()
    {
        return new BattleActionResult
        {
            actionSequence = 3,
            actor = BattleSlotOwner.My,
            requestActionType = BattleActionType.UseContent,
            isAccepted = true,
            message = "Random hand result",
            resolvedRandomCardIds = new List<string> { "My-12-CHAR001" },
            resolvedTargetSlotIds = new List<string> { "Enemy_1_1" },
            resolvedChoiceIds = new List<string> { "randomHandReveal" },
            movedCardIds = new List<string> { "My-12-CHAR001" },
            affectedCardIds = new List<string> { "Enemy-9-CHAR002" },
            affectedSlotIds = new List<string> { "Enemy_1_1" }
        };
    }

    private static BattleActionResult CreateDeckOrderSample()
    {
        return new BattleActionResult
        {
            actionSequence = 4,
            actor = BattleSlotOwner.My,
            requestActionType = BattleActionType.EndTurn,
            isAccepted = true,
            message = "Deck order result",
            playerMainDeckOrderIds = new List<string> { "My-1-CNTS001", "My-2-CHAR001" },
            enemyMainDeckOrderIds = new List<string> { "Enemy-1-CNTS002", "Enemy-2-CHAR002" },
            broadcastSetupFirstActor = "Enemy"
        };
    }

    private static bool AreEqual(BattleActionResult expected, BattleActionResult actual, string label)
    {
        bool result = true;

        if (expected == null || actual == null)
        {
            Debug.LogWarning($"[BattleActionResultTester] {label}: null mismatch");
            return false;
        }

        CompareField(label, "actionSequence", expected.actionSequence, actual.actionSequence, ref result);
        CompareField(label, "actor", expected.actor, actual.actor, ref result);
        CompareField(label, "requestActionType", expected.requestActionType, actual.requestActionType, ref result);
        CompareField(label, "isAccepted", expected.isAccepted, actual.isAccepted, ref result);
        CompareField(label, "rejectReason", expected.rejectReason, actual.rejectReason, ref result);
        CompareField(label, "message", expected.message, actual.message, ref result);
        CompareList(label, "resolvedRandomCardIds", expected.resolvedRandomCardIds, actual.resolvedRandomCardIds, ref result);
        CompareList(label, "resolvedTargetSlotIds", expected.resolvedTargetSlotIds, actual.resolvedTargetSlotIds, ref result);
        CompareList(label, "resolvedChoiceIds", expected.resolvedChoiceIds, actual.resolvedChoiceIds, ref result);
        CompareList(label, "playerMainDeckOrderIds", expected.playerMainDeckOrderIds, actual.playerMainDeckOrderIds, ref result);
        CompareList(label, "enemyMainDeckOrderIds", expected.enemyMainDeckOrderIds, actual.enemyMainDeckOrderIds, ref result);
        CompareField(label, "firstActor", expected.firstActor, actual.firstActor, ref result);
        CompareField(label, "broadcastSetupFirstActor", expected.broadcastSetupFirstActor, actual.broadcastSetupFirstActor, ref result);
        CompareList(label, "movedCardIds", expected.movedCardIds, actual.movedCardIds, ref result);
        CompareList(label, "affectedCardIds", expected.affectedCardIds, actual.affectedCardIds, ref result);
        CompareList(label, "affectedSlotIds", expected.affectedSlotIds, actual.affectedSlotIds, ref result);

        return result;
    }

    private static void CompareField<T>(string label, string fieldName, T expected, T actual, ref bool result)
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual))
            return;

        Debug.LogWarning($"[BattleActionResultTester] {label}: {fieldName} mismatch. expected={expected}, actual={actual}");
        result = false;
    }

    private static void CompareList(string label, string fieldName, List<string> expected, List<string> actual, ref bool result)
    {
        int expectedCount = expected != null ? expected.Count : -1;
        int actualCount = actual != null ? actual.Count : -1;

        if (expectedCount != actualCount)
        {
            Debug.LogWarning($"[BattleActionResultTester] {label}: {fieldName}.Count mismatch. expected={expectedCount}, actual={actualCount}");
            result = false;
            return;
        }

        if (expected == null)
            return;

        for (int i = 0; i < expected.Count; i++)
        {
            if (expected[i] == actual[i])
                continue;

            Debug.LogWarning($"[BattleActionResultTester] {label}: {fieldName}[{i}] mismatch. expected={expected[i]}, actual={actual[i]}");
            result = false;
        }
    }

    private static bool ListsEqual(List<string> expected, List<string> actual)
    {
        if (expected == null || actual == null)
            return expected == actual;

        if (expected.Count != actual.Count)
            return false;

        for (int i = 0; i < expected.Count; i++)
        {
            if (expected[i] != actual[i])
                return false;
        }

        return true;
    }
}
