using UnityEngine;

// This class applies host-resolved BattleActionResult.
// Non-host clients should not re-roll random effects.
// Non-host clients should apply resolved card/slot/deck order IDs from result.
// This is not a network transport layer.
public class BattleActionResultApplier
{
    private readonly BattleManager battleManager;

    public BattleActionResultApplier(BattleManager battleManager)
    {
        this.battleManager = battleManager;
    }

    public void Apply(BattleActionResult result)
    {
        if (result == null)
        {
            Debug.LogWarning("[BattleActionResultApplier] result is null");
            return;
        }

        if (!result.isAccepted)
        {
            ApplyRejected(result);
            return;
        }

        ApplyAccepted(result);
    }

    private void ApplyRejected(BattleActionResult result)
    {
        string reason = !string.IsNullOrWhiteSpace(result.rejectReason)
            ? result.rejectReason
            : "Action rejected.";

        Debug.LogWarning($"[BattleActionResultApplier] Rejected actionSequence={result.actionSequence}, actionType={result.requestActionType}, reason={reason}");

        if (battleManager != null)
        {
            battleManager.HandleRejectedActionResultFromExternal(result);
            battleManager.SetSystemMessageFromExternal(
                !string.IsNullOrWhiteSpace(result.message) ? result.message : reason);
        }
    }

    private void ApplyAccepted(BattleActionResult result)
    {
        Debug.Log($"[BattleActionResultApplier] Accepted actionSequence={result.actionSequence}, actionType={result.requestActionType}");

        if (battleManager != null &&
            result.requestActionType != BattleActionType.EndTurn &&
            result.requestActionType != BattleActionType.SummonFaceUp &&
            !string.IsNullOrWhiteSpace(result.message))
        {
            battleManager.SetSystemMessageFromExternal(result.message);
        }

        LogResolvedRandoms(result);
        ApplyDeckOrders(result);

        switch (result.requestActionType)
        {
            case BattleActionType.EndTurn:
                ApplyEndTurnResult(result);
                break;

            case BattleActionType.PlaceBroadcast:
                ApplyPlaceBroadcastResult(result);
                break;

            case BattleActionType.SummonFaceDown:
                ApplySummonFaceDownResult(result);
                break;

            case BattleActionType.SummonFaceUp:
                ApplySummonFaceUpResult(result);
                break;

            case BattleActionType.StartMainGame:
                ApplyStartMainGameResult(result);
                break;

            default:
                // TODO: Add ActionType-specific result application as host results become richer.
                break;
        }
    }

    private void ApplyEndTurnResult(BattleActionResult result)
    {
        if (battleManager == null)
            return;

        battleManager.ApplyEndTurnFromResult(result);
    }

    private void ApplyPlaceBroadcastResult(BattleActionResult result)
    {
        if (battleManager == null)
            return;

        string cardInstanceId = result.affectedCardIds != null && result.affectedCardIds.Count > 0
            ? result.affectedCardIds[0]
            : "";
        string targetSlotId = result.affectedSlotIds != null && result.affectedSlotIds.Count > 0
            ? result.affectedSlotIds[0]
            : "";

        battleManager.ApplyPlaceBroadcastFromResult(
            result.actor,
            cardInstanceId,
            targetSlotId);
    }

    private void ApplySummonFaceDownResult(BattleActionResult result)
    {
        if (battleManager == null)
            return;

        battleManager.ApplySummonFaceDownFromResult(result);
    }

    private void ApplySummonFaceUpResult(BattleActionResult result)
    {
        if (battleManager == null)
            return;

        battleManager.ApplySummonFaceUpFromResult(result);
    }

    private void ApplyStartMainGameResult(BattleActionResult result)
    {
        if (battleManager == null)
            return;

        battleManager.ApplyStartMainGameFromResult(result);
    }

    private void ApplyDeckOrders(BattleActionResult result)
    {
        if (battleManager == null)
        {
            Debug.LogWarning("[BattleActionResultApplier] Cannot apply deck order: BattleManager is null");
            return;
        }

        if (result.requestActionType == BattleActionType.StartMainGame)
            return;

        if (result.playerMainDeckOrderIds != null && result.playerMainDeckOrderIds.Count > 0)
        {
            bool applied = battleManager.ApplyMainDeckOrderFromExternal(
                BattleSlotOwner.My,
                result.playerMainDeckOrderIds);
            Debug.Log($"[BattleActionResultApplier] Apply player deck order result={applied}");
        }

        if (result.enemyMainDeckOrderIds != null && result.enemyMainDeckOrderIds.Count > 0)
        {
            bool applied = battleManager.ApplyMainDeckOrderFromExternal(
                BattleSlotOwner.Enemy,
                result.enemyMainDeckOrderIds);
            Debug.Log($"[BattleActionResultApplier] Apply enemy deck order result={applied}");
        }
    }

    private void LogResolvedRandoms(BattleActionResult result)
    {
        if (result.resolvedRandomCardIds != null)
        {
            foreach (string cardInstanceId in result.resolvedRandomCardIds)
            {
                if (battleManager == null)
                {
                    Debug.Log($"[BattleActionResultApplier] resolvedRandomCardId={cardInstanceId}");
                    continue;
                }

                BaseCardData card = battleManager.FindCardByInstanceIdFromAnyKnownZone(
                    result.actor,
                    cardInstanceId);
                if (card != null)
                    Debug.Log($"[BattleActionResultApplier] resolvedRandomCardId found: {cardInstanceId} ({card.id}/{card.name})");
                else
                    Debug.LogWarning($"[BattleActionResultApplier] resolvedRandomCardId not found: {cardInstanceId}");
            }
        }

        if (result.resolvedTargetSlotIds != null)
        {
            foreach (string slotId in result.resolvedTargetSlotIds)
            {
                BattleFieldSlot slot = battleManager != null
                    ? battleManager.FindFieldSlotBySlotId(slotId)
                    : null;
                if (slot != null)
                    Debug.Log($"[BattleActionResultApplier] resolvedTargetSlotId found: {slotId}");
                else
                    Debug.LogWarning($"[BattleActionResultApplier] resolvedTargetSlotId not found: {slotId}");
            }
        }

        LogIds("resolvedChoiceIds", result.resolvedChoiceIds);
        LogIds("movedCardIds", result.movedCardIds);
        LogIds("affectedCardIds", result.affectedCardIds);
        LogIds("affectedSlotIds", result.affectedSlotIds);
    }

    private void LogIds(string label, System.Collections.Generic.List<string> ids)
    {
        if (ids == null)
            return;

        foreach (string id in ids)
            Debug.Log($"[BattleActionResultApplier] {label}: {id}");
    }
}
