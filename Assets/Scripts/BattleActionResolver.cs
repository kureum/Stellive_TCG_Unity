using UnityEngine;

public class BattleActionResolver
{
    private readonly BattleManager battleManager;
    private readonly BattleActionValidator validator;

    public BattleActionResolver(BattleManager battleManager)
    {
        this.battleManager = battleManager;
        validator = battleManager != null ? new BattleActionValidator(battleManager) : null;
    }

    public BattleActionResult ResolveActionAsHost(BattleAction action)
    {
        BattleActionResult result = CreateBaseResult(action);

        if (action == null)
        {
            result.isAccepted = false;
            result.rejectReason = "BattleAction is null.";
            return result;
        }

        if (battleManager == null || validator == null)
        {
            result.isAccepted = false;
            result.rejectReason = "BattleManager or validator is null.";
            return result;
        }

        BattleActionValidationResult validationResult = validator.Validate(action);
        if (validationResult == null || !validationResult.isValid)
        {
            result.isAccepted = false;
            result.rejectReason = validationResult != null
                ? validationResult.reason
                : "Validation result is null.";
            Debug.LogWarning($"[BattleActionResolver] Host rejected action. actionType={action.actionType}, reason={result.rejectReason}");
            return result;
        }

        result.isAccepted = true;
        result.message = $"Host accepted {action.actionType}";

        if (action.actionType == BattleActionType.EndTurn)
            return battleManager.CreateEndTurnResultFromExternal(action);

        if (action.actionType == BattleActionType.SummonFaceDown)
        {
            battleManager.ClearOnlineTurnPassStateForAcceptedActionFromExternal(action);
            return battleManager.CreateSummonFaceDownResultFromExternal(action);
        }

        if (action.actionType == BattleActionType.SummonFaceUp)
        {
            battleManager.ClearOnlineTurnPassStateForAcceptedActionFromExternal(action);
            return battleManager.CreateSummonFaceUpResultFromExternal(action);
        }

        battleManager.ClearOnlineTurnPassStateForAcceptedActionFromExternal(action);

        if (action.actionType == BattleActionType.PlaceBroadcast)
        {
            result.affectedCardIds.Add(action.cardInstanceId);
            result.affectedSlotIds.Add(action.targetSlotId);
        }

        if (action.actionType != BattleActionType.PlaceBroadcast)
        {
            result.playerMainDeckOrderIds = battleManager.GetMainDeckOrderIds(BattleSlotOwner.My);
            result.enemyMainDeckOrderIds = battleManager.GetMainDeckOrderIds(BattleSlotOwner.Enemy);
        }

        if (action.selectedTargetIds != null)
            result.resolvedTargetSlotIds.AddRange(action.selectedTargetIds);

        if (action.selectedCardIds != null)
            result.resolvedRandomCardIds.AddRange(action.selectedCardIds);

        if (!string.IsNullOrWhiteSpace(action.choiceId))
            result.resolvedChoiceIds.Add(action.choiceId);

        // TODO: For random effects, host should roll once here or in effect resolution,
        // then write cardInstanceId/slotId results into this BattleActionResult.
        // Non-host clients should apply these resolved IDs without rerolling.
        return result;
    }

    private BattleActionResult CreateBaseResult(BattleAction action)
    {
        if (action == null)
        {
            return new BattleActionResult
            {
                actionSequence = -1,
                actor = BattleSlotOwner.My,
                requestActionType = BattleActionType.EndTurn,
                message = "Null action result"
            };
        }

        return new BattleActionResult
        {
            actionSequence = action.actionSequence,
            actor = action.actor,
            requestActionType = action.actionType
        };
    }
}
