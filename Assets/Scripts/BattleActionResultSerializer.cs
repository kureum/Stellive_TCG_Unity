using UnityEngine;

public static class BattleActionResultSerializer
{
    public static string ToJson(BattleActionResult result)
    {
        if (result == null)
        {
            Debug.LogWarning("[BattleActionResultSerializer] ToJson failed: result is null");
            return "";
        }

        try
        {
            return JsonUtility.ToJson(result);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[BattleActionResultSerializer] ToJson failed: {ex.Message}");
            return "";
        }
    }

    public static BattleActionResult FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            BattleActionResult result = JsonUtility.FromJson<BattleActionResult>(json);
            Normalize(result);
            return result;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[BattleActionResultSerializer] FromJson failed: {ex.Message}");
            return null;
        }
    }

    private static void Normalize(BattleActionResult result)
    {
        if (result == null)
            return;

        if (result.resolvedRandomCardIds == null) result.resolvedRandomCardIds = new System.Collections.Generic.List<string>();
        if (result.resolvedTargetSlotIds == null) result.resolvedTargetSlotIds = new System.Collections.Generic.List<string>();
        if (result.resolvedChoiceIds == null) result.resolvedChoiceIds = new System.Collections.Generic.List<string>();
        if (result.playerMainDeckOrderIds == null) result.playerMainDeckOrderIds = new System.Collections.Generic.List<string>();
        if (result.enemyMainDeckOrderIds == null) result.enemyMainDeckOrderIds = new System.Collections.Generic.List<string>();
        if (result.hostInitialHandCardInstanceIds == null) result.hostInitialHandCardInstanceIds = new System.Collections.Generic.List<string>();
        if (result.clientInitialHandCardInstanceIds == null) result.clientInitialHandCardInstanceIds = new System.Collections.Generic.List<string>();
        if (result.hostRemainingMainDeckOrderIds == null) result.hostRemainingMainDeckOrderIds = new System.Collections.Generic.List<string>();
        if (result.clientRemainingMainDeckOrderIds == null) result.clientRemainingMainDeckOrderIds = new System.Collections.Generic.List<string>();
        if (result.drawnCardInstanceIds == null) result.drawnCardInstanceIds = new System.Collections.Generic.List<string>();
        if (result.hostDrawnCardInstanceIds == null) result.hostDrawnCardInstanceIds = new System.Collections.Generic.List<string>();
        if (result.clientDrawnCardInstanceIds == null) result.clientDrawnCardInstanceIds = new System.Collections.Generic.List<string>();
        if (result.movedCardIds == null) result.movedCardIds = new System.Collections.Generic.List<string>();
        if (result.affectedCardIds == null) result.affectedCardIds = new System.Collections.Generic.List<string>();
        if (result.affectedSlotIds == null) result.affectedSlotIds = new System.Collections.Generic.List<string>();
        if (result.effectMessages == null) result.effectMessages = new System.Collections.Generic.List<string>();
        if (result.viewerDeltas == null) result.viewerDeltas = new System.Collections.Generic.List<ViewerDelta>();
        if (result.fieldStatDeltas == null) result.fieldStatDeltas = new System.Collections.Generic.List<FieldStatDelta>();
        if (result.cardZoneMoveDeltas == null) result.cardZoneMoveDeltas = new System.Collections.Generic.List<CardZoneMoveDelta>();
        if (result.fieldContentDeltas == null) result.fieldContentDeltas = new System.Collections.Generic.List<FieldContentDelta>();
        if (result.cardRevealDeltas == null) result.cardRevealDeltas = new System.Collections.Generic.List<CardRevealDelta>();
        if (result.cardDrawDeltas == null) result.cardDrawDeltas = new System.Collections.Generic.List<CardDrawDelta>();
        if (result.deckOrderDeltas == null) result.deckOrderDeltas = new System.Collections.Generic.List<DeckOrderDelta>();
        if (result.statusDeltas == null) result.statusDeltas = new System.Collections.Generic.List<StatusDelta>();
        if (result.actionStateDeltas == null) result.actionStateDeltas = new System.Collections.Generic.List<ActionStateDelta>();
        if (result.selectionRequests == null) result.selectionRequests = new System.Collections.Generic.List<SelectionRequestDelta>();
        if (result.messageDeltas == null) result.messageDeltas = new System.Collections.Generic.List<MessageDelta>();
    }
}
