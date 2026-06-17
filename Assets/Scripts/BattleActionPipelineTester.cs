using System.Collections.Generic;
using UnityEngine;

public class BattleActionPipelineTester
{
    private readonly BattleManager battleManager;
    private readonly BattleActionResolver resolver;
    private readonly BattleActionResultApplier applier;

    public BattleActionPipelineTester(BattleManager battleManager)
    {
        this.battleManager = battleManager;
        resolver = battleManager != null ? new BattleActionResolver(battleManager) : null;
        applier = battleManager != null ? new BattleActionResultApplier(battleManager) : null;
    }

    public void RunAllTests()
    {
        if (battleManager == null || resolver == null || applier == null)
        {
            Debug.LogWarning("[PipelineTest] Cannot run tests: BattleManager, resolver, or applier is null.");
            return;
        }

        Debug.Log("[PipelineTest] Start BattleAction JSON network simulation tests.");
        RunEndTurnPipelineTest();
        RunRejectedActionPipelineTest();
        RunDeckOrderResultPipelineTest();
        Debug.Log("[PipelineTest] Completed BattleAction JSON network simulation tests.");
    }

    private void RunEndTurnPipelineTest()
    {
        BattleSlotOwner actor = GetCurrentActor();
        BattleAction originalAction = new BattleAction
        {
            actionSequence = 10001,
            actor = actor,
            actionType = BattleActionType.EndTurn
        };

        Debug.Log("[PipelineTest] EndTurn - Original Action");
        RunActionToResultPipeline(originalAction, "EndTurn");
        Debug.Log("[PipelineTest] EndTurn test does not execute BattleActionExecutor, so turn state should not advance from this test path.");
    }

    private void RunRejectedActionPipelineTest()
    {
        BattleAction originalAction = new BattleAction
        {
            actionSequence = 10002,
            actor = GetCurrentActor(),
            actionType = BattleActionType.SummonFaceUp,
            cardInstanceId = "missing-card-instance-for-pipeline-test",
            handIndex = -1,
            targetSlotId = "missing-slot-for-pipeline-test"
        };

        Debug.Log("[PipelineTest] RejectedAction - Original Action");
        BattleActionResult result = RunActionToResultPipeline(originalAction, "RejectedAction");

        if (result == null)
        {
            Debug.LogWarning("[PipelineTest] RejectedAction expected rejected result but result is null.");
            return;
        }

        if (result.isAccepted)
            Debug.LogWarning("[PipelineTest] RejectedAction expected isAccepted=false but got true.");
        else
            Debug.Log($"[PipelineTest] RejectedAction rejectReason={result.rejectReason}");
    }

    private void RunDeckOrderResultPipelineTest()
    {
        List<string> originalOrder = battleManager.GetMainDeckOrderIds(BattleSlotOwner.My);
        if (originalOrder == null || originalOrder.Count <= 1)
        {
            Debug.Log("[PipelineTest] DeckOrder skipped: player main deck has 0 or 1 card.");
            return;
        }

        List<string> reversedOrder = new List<string>(originalOrder);
        reversedOrder.Reverse();

        BattleActionResult originalResult = new BattleActionResult
        {
            actionSequence = 10003,
            actor = BattleSlotOwner.My,
            requestActionType = BattleActionType.EndTurn,
            isAccepted = true,
            message = "Pipeline deck order result",
            playerMainDeckOrderIds = reversedOrder
        };

        Debug.Log("[PipelineTest] DeckOrder - Host Result");
        Debug.Log($"[PipelineTest] Host Result accepted={originalResult.isAccepted}, actionType={originalResult.requestActionType}, deckCount={originalResult.playerMainDeckOrderIds.Count}");

        string resultJson = BattleActionResultSerializer.ToJson(originalResult);
        Debug.Log($"[PipelineTest] Result JSON: {resultJson}");

        BattleActionResult receivedResult = BattleActionResultSerializer.FromJson(resultJson);
        Debug.Log($"[PipelineTest] Received Result accepted={receivedResult != null && receivedResult.isAccepted}, actionType={(receivedResult != null ? receivedResult.requestActionType.ToString() : "null")}");

        applier.Apply(receivedResult);
        Debug.Log("[PipelineTest] Apply Result: deck order result applied.");

        List<string> appliedOrder = battleManager.GetMainDeckOrderIds(BattleSlotOwner.My);
        bool reversedApplied = ListsEqual(reversedOrder, appliedOrder);
        Debug.Log($"[PipelineTest] DeckOrder reversedApplied={reversedApplied}");

        bool restored = battleManager.ApplyMainDeckOrderFromExternal(BattleSlotOwner.My, originalOrder);
        Debug.Log($"[PipelineTest] DeckOrder original order restored={restored}");

        if (!reversedApplied || !restored)
            Debug.LogWarning("[PipelineTest] DeckOrder pipeline test did not apply or restore as expected.");
    }

    private BattleActionResult RunActionToResultPipeline(BattleAction originalAction, string label)
    {
        Debug.Log($"[PipelineTest] {label} - Original Action: actionType={originalAction.actionType}, seq={originalAction.actionSequence}, actor={originalAction.actor}");

        string actionJson = BattleActionSerializer.ToJson(originalAction);
        Debug.Log($"[PipelineTest] {label} - Action JSON: {actionJson}");

        BattleAction receivedAction = BattleActionSerializer.FromJson(actionJson);
        if (receivedAction == null)
        {
            Debug.LogWarning($"[PipelineTest] {label} - Received Action is null.");
            return null;
        }

        Debug.Log($"[PipelineTest] {label} - Received Action: actionType={receivedAction.actionType}, seq={receivedAction.actionSequence}, actor={receivedAction.actor}");

        BattleActionResult hostResult = resolver.ResolveActionAsHost(receivedAction);
        if (hostResult == null)
        {
            Debug.LogWarning($"[PipelineTest] {label} - Host Result is null.");
            return null;
        }

        Debug.Log($"[PipelineTest] {label} - Host Result: accepted={hostResult.isAccepted}, rejectReason={hostResult.rejectReason}, message={hostResult.message}");

        string resultJson = BattleActionResultSerializer.ToJson(hostResult);
        Debug.Log($"[PipelineTest] {label} - Result JSON: {resultJson}");

        BattleActionResult receivedResult = BattleActionResultSerializer.FromJson(resultJson);
        if (receivedResult == null)
        {
            Debug.LogWarning($"[PipelineTest] {label} - Received Result is null.");
            return null;
        }

        Debug.Log($"[PipelineTest] {label} - Received Result: accepted={receivedResult.isAccepted}, rejectReason={receivedResult.rejectReason}, message={receivedResult.message}");

        applier.Apply(receivedResult);
        Debug.Log($"[PipelineTest] {label} - Apply Result completed.");

        return receivedResult;
    }

    private BattleSlotOwner GetCurrentActor()
    {
        return battleManager.CurrentActionSideFromExternal == BattlePlayerSide.Enemy
            ? BattleSlotOwner.Enemy
            : BattleSlotOwner.My;
    }

    private bool ListsEqual(List<string> expected, List<string> actual)
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
