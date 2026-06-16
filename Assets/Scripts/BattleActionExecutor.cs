using UnityEngine;

public class BattleActionExecutor
{
    private readonly BattleManager battleManager;

    public BattleActionExecutor(BattleManager battleManager)
    {
        this.battleManager = battleManager;
    }

    public bool ExecuteAction(BattleAction action)
    {
        if (action == null)
        {
            Debug.LogWarning("[BattleActionExecutor] action is null");
            return false;
        }

        Debug.Log($"[BattleActionExecutor] Execute: {action.actionType}, seq={action.actionSequence}, actor={action.actor}");

        if (battleManager == null)
        {
            Debug.LogWarning("[BattleActionExecutor] BattleManager is null");
            return false;
        }

        switch (action.actionType)
        {
            case BattleActionType.EndTurn:
                return battleManager.ExecuteEndTurnFromAction(action);

            case BattleActionType.SummonFaceDown:
                Debug.Log("[BattleActionExecutor] Execute SummonFaceDown");
                bool result = battleManager.ExecuteSummonFaceDownFromAction(action);
                if (result)
                    Debug.Log("[BattleActionExecutor] SummonFaceDown completed");
                return result;

            case BattleActionType.SummonFaceUp:
                Debug.Log("[BattleActionExecutor] Execute SummonFaceUp");
                bool faceUpResult = battleManager.ExecuteSummonFaceUpFromAction(action);
                Debug.Log($"[BattleActionExecutor] SummonFaceUp completed. result={faceUpResult}");
                return faceUpResult;

            case BattleActionType.FlipSummon:
                Debug.Log("[BattleActionExecutor] Execute FlipSummon");
                bool flipResult = battleManager.ExecuteFlipSummonFromAction(action);
                Debug.Log($"[BattleActionExecutor] FlipSummon completed. result={flipResult}");
                return flipResult;

            case BattleActionType.MoveCharacter:
                Debug.Log("[BattleActionExecutor] Execute MoveCharacter");
                bool moveResult = battleManager.ExecuteMoveCharacterFromAction(action);
                Debug.Log($"[BattleActionExecutor] MoveCharacter completed. result={moveResult}");
                return moveResult;

            case BattleActionType.StartCollab:
                Debug.Log("[BattleActionExecutor] Execute StartCollab");
                bool collabResult = battleManager.ExecuteStartCollabFromAction(action);
                Debug.Log($"[BattleActionExecutor] StartCollab completed. result={collabResult}");
                return collabResult;

            case BattleActionType.UseContent:
                Debug.Log("[BattleActionExecutor] Execute UseContent");
                bool contentResult = battleManager.ExecuteUseContentFromAction(action);
                Debug.Log($"[BattleActionExecutor] UseContent completed. result={contentResult}");
                return contentResult;

            case BattleActionType.UseCharacterActive:
                Debug.Log("[BattleActionExecutor] Execute UseCharacterActive");
                bool characterActiveResult = battleManager.ExecuteUseCharacterActiveFromAction(action);
                Debug.Log($"[BattleActionExecutor] UseCharacterActive completed. result={characterActiveResult}");
                return characterActiveResult;

            case BattleActionType.UseIdolActive:
                Debug.Log("[BattleActionExecutor] Execute UseIdolActive");
                bool idolActiveResult = battleManager.ExecuteUseIdolActiveFromAction(action);
                Debug.Log($"[BattleActionExecutor] UseIdolActive completed. result={idolActiveResult}");
                return idolActiveResult;

            case BattleActionType.SelectEffectTarget:
                Debug.Log("[BattleActionExecutor] Execute SelectEffectTarget");
                bool targetResult = battleManager.ExecuteSelectEffectTargetFromAction(action);
                Debug.Log($"[BattleActionExecutor] SelectEffectTarget completed. result={targetResult}");
                return targetResult;

            case BattleActionType.SelectCardOption:
                Debug.Log("[BattleActionExecutor] Execute SelectCardOption");
                bool cardOptionResult = battleManager.ExecuteSelectCardOptionFromAction(action);
                Debug.Log($"[BattleActionExecutor] SelectCardOption completed. result={cardOptionResult}");
                return cardOptionResult;

            case BattleActionType.SelectMultipleCardOptions:
                Debug.Log("[BattleActionExecutor] Execute SelectMultipleCardOptions");
                bool multipleCardResult = battleManager.ExecuteSelectMultipleCardOptionsFromAction(action);
                Debug.Log($"[BattleActionExecutor] SelectMultipleCardOptions completed. result={multipleCardResult}");
                return multipleCardResult;

            case BattleActionType.SelectEffectChoice:
                Debug.Log("[BattleActionExecutor] Execute SelectEffectChoice");
                bool effectChoiceResult = battleManager.ExecuteSelectEffectChoiceFromAction(action);
                Debug.Log($"[BattleActionExecutor] SelectEffectChoice completed. result={effectChoiceResult}");
                return effectChoiceResult;

            default:
                Debug.LogWarning($"[BattleActionExecutor] Unsupported action type: {action.actionType}");
                return false;
        }
    }
}
