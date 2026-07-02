using System;
using System.Collections.Generic;

[Serializable]
public class BattleActionResult
{
    public int actionSequence;
    public BattleSlotOwner actor;
    public BattleActionType requestActionType;
    public bool isAccepted;
    public string rejectReason = "";
    public string message = "";

    public List<string> resolvedRandomCardIds = new List<string>();
    public List<string> resolvedTargetSlotIds = new List<string>();
    public List<string> resolvedChoiceIds = new List<string>();
    public List<string> playerMainDeckOrderIds = new List<string>();
    public List<string> enemyMainDeckOrderIds = new List<string>();
    public List<string> hostInitialHandCardInstanceIds = new List<string>();
    public List<string> clientInitialHandCardInstanceIds = new List<string>();
    public List<string> hostRemainingMainDeckOrderIds = new List<string>();
    public List<string> clientRemainingMainDeckOrderIds = new List<string>();
    public string firstActor = "";
    public string broadcastSetupFirstActor = "";
    public BattleSlotOwner currentTurnPlayer;
    public int turnCount;
    public string nextPhase = "";
    public bool didAdvanceTurn;
    public bool hostPassedThisTurn;
    public bool clientPassedThisTurn;
    public bool hostNoActionPassed;
    public bool clientNoActionPassed;
    public bool hostActedInCurrentPassCycle;
    public bool clientActedInCurrentPassCycle;
    public int consecutiveNoActionPassCount;
    public int hostViewerCount;
    public int clientViewerCount;
    public int hostViewerGain;
    public int clientViewerGain;
    public int hostHandCount;
    public int clientHandCount;
    public int hostDeckCount;
    public int clientDeckCount;
    public bool faceDown;
    public BattleSlotOwner characterOwner;
    public int characterCurrentHp;
    public int characterCurrentMaxHp;
    public int characterCurrentTension;
    public bool characterMovedThisTurn;
    public bool characterActiveUsedThisTurn;
    public BattleSlotOwner attackerOwner;
    public BattleSlotOwner defenderOwner;
    public int attackerHpBefore;
    public int defenderHpBefore;
    public int attackerHpAfter;
    public int defenderHpAfter;
    public int attackerMaxHp;
    public int defenderMaxHp;
    public int attackerTensionUsed;
    public int defenderTensionUsed;
    public int attackerTensionAfter;
    public int defenderTensionAfter;
    public bool defenderCounterattacked;
    public bool attackerDefeated;
    public bool defenderDefeated;
    public bool attackerMovedToDefenderSlot;
    public bool attackerSentToRest;
    public bool defenderSentToRest;
    public string resolvedEffectRef = "";
    public bool effectApplied;
    public string unsupportedReason = "";
    public int paidViewerCost;
    public BattleSlotOwner drawnPlayer;
    public List<string> drawnCardInstanceIds = new List<string>();
    public List<string> hostDrawnCardInstanceIds = new List<string>();
    public List<string> clientDrawnCardInstanceIds = new List<string>();

    public List<string> movedCardIds = new List<string>();
    public List<string> affectedCardIds = new List<string>();
    public List<string> affectedSlotIds = new List<string>();
    public List<string> effectMessages = new List<string>();
    public List<ViewerDelta> viewerDeltas = new List<ViewerDelta>();
    public List<FieldStatDelta> fieldStatDeltas = new List<FieldStatDelta>();
    public List<CardZoneMoveDelta> cardZoneMoveDeltas = new List<CardZoneMoveDelta>();
    public List<FieldContentDelta> fieldContentDeltas = new List<FieldContentDelta>();
    public List<CardRevealDelta> cardRevealDeltas = new List<CardRevealDelta>();
    public List<CardDrawDelta> cardDrawDeltas = new List<CardDrawDelta>();
    public List<DeckOrderDelta> deckOrderDeltas = new List<DeckOrderDelta>();
    public List<StatusDelta> statusDeltas = new List<StatusDelta>();
    public List<ActionStateDelta> actionStateDeltas = new List<ActionStateDelta>();
    public List<SelectionRequestDelta> selectionRequests = new List<SelectionRequestDelta>();
    public List<MessageDelta> messageDeltas = new List<MessageDelta>();
    public string effectClassification = "";
    public bool requiresFollowUpAction;
}

[Serializable]
public class ViewerDelta
{
    public BattleSlotOwner owner;
    public int before;
    public int after;
    public int amount;
}

[Serializable]
public class FieldStatDelta
{
    public string slotId = "";
    public string cardInstanceId = "";
    public int hpBefore;
    public int hpAfter;
    public int tensionBefore;
    public int tensionAfter;
    public int maxHpBefore;
    public int maxHpAfter;
}

[Serializable]
public class CardZoneMoveDelta
{
    public string cardInstanceId = "";
    public string cardId = "";
    public BattleSlotOwner owner;
    public string fromZone = "";
    public string toZone = "";
    public string fromSlotId = "";
    public string toSlotId = "";
    public bool isPublic;
    public bool faceDown;
}

[Serializable]
public class FieldContentDelta
{
    public string slotId = "";
    public string contentCardInstanceId = "";
    public string contentCardId = "";
    public BattleSlotOwner contentOwner;
    public bool removed;
    public bool movedToRest;
}

[Serializable]
public class CardRevealDelta
{
    public BattleSlotOwner owner;
    public string cardInstanceId = "";
    public string cardId = "";
    public string fromZone = "";
    public string slotId = "";
    public string revealTo = "";
    public bool isPublicReveal;
}

[Serializable]
public class CardDrawDelta
{
    public BattleSlotOwner owner;
    public string cardInstanceId = "";
    public string cardId = "";
    public int fromDeckIndex = -1;
    public bool toHand = true;
    public bool visibleToOwnerOnly = true;
    public string publicCardIdForOpponent = "";
}

[Serializable]
public class DeckOrderDelta
{
    public BattleSlotOwner owner;
    public List<string> deckOrderIds = new List<string>();
    public string reason = "";
    public bool visibleToOpponent;
}

[Serializable]
public class StatusDelta
{
    public BattleSlotOwner owner;
    public string sourceCardInstanceId = "";
    public string sourceCardId = "";
    public string targetSlotId = "";
    public string targetCardInstanceId = "";
    public string statusType = "";
    public string value = "";
    public string durationType = "";
    public int expireTurn;
    public string expirePhase = "";
    public string stackPolicy = "";
    public bool addOrRemove;
}

[Serializable]
public class ActionStateDelta
{
    public BattleSlotOwner owner;
    public string cardInstanceId = "";
    public string slotId = "";
    public string actionStateType = "";
    public bool before;
    public bool after;
    public int turn;
}

[Serializable]
public class SelectionRequestDelta
{
    public BattleSlotOwner requestingPlayer;
    public string choiceType = "";
    public string sourceActionId = "";
    public string sourceEffectRef = "";
    public List<string> candidatePublicIds = new List<string>();
    public List<string> candidatePrivateIdsForOwnerOnly = new List<string>();
    public int minSelect;
    public int maxSelect;
    public string cancelPolicy = "";
    public string nextActionType = "";
}

[Serializable]
public class MessageDelta
{
    public string audience = "";
    public string messageKey = "";
    public string messageText = "";
    public string relatedCardId = "";
    public string relatedInstanceId = "";
}

public enum OnlineEffectSupportCategory
{
    SupportedImmediate,
    SupportedImmediateWithDelta,
    CurrentDeltaSupported,
    RequiresNewDelta,
    RequiresSelectionFlow,
    RequiresRandomResolution,
    RequiresPrivateInfoProtocol,
    RequiresPersistentStateDelta,
    RequiresTimingTriggerResult,
    RequiresKOOrRestResolution,
    IncludedInHostCalculation,
    UnsupportedForNow,
    NeedsUserDecision
}

[Serializable]
public class OnlineEffectRefMetadata
{
    public string effectRef = "";
    public OnlineEffectSupportCategory category;
    public string stateChanges = "";
    public string deltaTypes = "";
    public string onlineStatus = "";
    public bool requiresSelectionFlow;
    public bool requiresRandomResolution;
    public bool requiresPrivateInfoProtocol;
    public bool requiresPersistentStateDelta;
    public bool requiresTimingTriggerResult;
}

[Serializable]
public class OnlineEffectResolveResult
{
    public bool success;
    public string rejectReason = "";
    public string unsupportedReason = "";
    public bool requiresTargetSelection;
    public bool requiresCardSelection;
    public bool requiresMultipleSelection;
    public bool requiresRandomResolution;
    public bool requiresPrivateInfoHandling;
    public bool requiresPrePostCollabTiming;
    public bool requiresPersistentContentInstall;
    public bool requiresPersistentStateDelta;
    public bool requiresTimingTriggerResult;
    public bool requiresFollowUpAction;
    public string classification = "";
    public string usedEffectRef = "";
    public List<ViewerDelta> viewerDeltas = new List<ViewerDelta>();
    public List<FieldStatDelta> fieldStatDeltas = new List<FieldStatDelta>();
    public List<CardZoneMoveDelta> zoneMoveDeltas = new List<CardZoneMoveDelta>();
    public List<FieldContentDelta> fieldContentDeltas = new List<FieldContentDelta>();
    public List<CardRevealDelta> cardRevealDeltas = new List<CardRevealDelta>();
    public List<CardDrawDelta> cardDrawDeltas = new List<CardDrawDelta>();
    public List<DeckOrderDelta> deckOrderDeltas = new List<DeckOrderDelta>();
    public List<StatusDelta> statusDeltas = new List<StatusDelta>();
    public List<ActionStateDelta> actionStateDeltas = new List<ActionStateDelta>();
    public List<SelectionRequestDelta> selectionRequests = new List<SelectionRequestDelta>();
    public List<MessageDelta> messageDeltas = new List<MessageDelta>();
    public List<string> messages = new List<string>();
}

public class OnlineEffectResolver
{
    private const string RemoveAllLastingContentsOnBoard = "content.removeAllLastingContentsOnBoard";

    private readonly BattleManager battleManager;

    public OnlineEffectResolver(BattleManager battleManager)
    {
        this.battleManager = battleManager;
    }

    public bool CanResolveForHost(
        BattleAction action,
        BaseCardData sourceCard,
        out string failReason)
    {
        OnlineEffectResolveResult result = Classify(action, sourceCard);
        failReason = result.rejectReason;
        return result.success;
    }

    public static OnlineEffectSupportCategory GetSupportCategory(string effectRef)
    {
        return GetMetadata(effectRef).category;
    }

    public static OnlineEffectRefMetadata GetMetadata(string effectRef)
    {
        string normalized = NormalizeEffectRef(effectRef);

        switch (normalized)
        {
            case "character.rest.gainViewers":
                return Meta(normalized, OnlineEffectSupportCategory.SupportedImmediateWithDelta, "viewer gain for effect owner; trigger wiring still required by caller", "ViewerDelta, MessageDelta", "delta supported, pending trigger wiring");
            case "character.rest.loseViewers":
                return Meta(normalized, OnlineEffectSupportCategory.SupportedImmediateWithDelta, "viewer loss for effect owner; trigger wiring still required by caller", "ViewerDelta, MessageDelta", "delta supported, pending trigger wiring");
            case RemoveAllLastingContentsOnBoard:
                return Meta(normalized, OnlineEffectSupportCategory.SupportedImmediateWithDelta, "field content removal and field content to rest zone move", "FieldContentDelta, CardZoneMoveDelta, MessageDelta", "supported");
            case "content.postCollabHealOwnParticipant":
                return Meta(normalized, OnlineEffectSupportCategory.RequiresTimingTriggerResult, "collab participant HP heal after collab", "FieldStatDelta, MessageDelta", "pending trigger result");
            case "character.active.adjacentHpDownAndTensionUpForTag":
            case "character.active.modifyTaggedOnBoard":
                return Meta(normalized, OnlineEffectSupportCategory.RequiresSelectionFlow, "selected or filtered field character HP/tension changes", "SelectionRequestDelta, FieldStatDelta, ActionStateDelta, MessageDelta", "pending selection flow", true, false, false, false, false);
            case "character.onAppear.adjacentOppCollabTensionDeltaThisTurn":
            case "character.active.adjacentOppCollabTensionDeltaThisTurn":
                return Meta(normalized, OnlineEffectSupportCategory.RequiresPersistentStateDelta, "temporary collab tension modifier for adjacent opponent", "StatusDelta, FieldStatDelta, MessageDelta", "pending persistent state", false, false, false, true, true);
            case "character.onAppear.callFromRestByTagToEmptyPlatforms":
                return Meta(normalized, OnlineEffectSupportCategory.NeedsUserDecision, "on-appear rest zone candidate selection and empty platform selection, plus possible OnAppear chain", "SelectionRequestDelta, CardZoneMoveDelta, MessageDelta", "pending user decision", true, false, false, false, true);
            case "content.silenceCharacterCollabThisTurn":
            case "content.moveOwnCharToEmptyOrBattleIfTagged":
            case "content.forceOpponentFlipOrSack":
            case "idol.active.fullHealOneControlled":
            case "idol.active.callFromRestByTagThenDonateViewers":
                return Meta(normalized, OnlineEffectSupportCategory.RequiresSelectionFlow, "field target selection and zone/stat changes", "SelectionRequestDelta, CardZoneMoveDelta, FieldStatDelta, ViewerDelta, ActionStateDelta, MessageDelta", "pending selection flow", true, false, false, false, false);
            case "character.fetchCardsToHandByTags":
            case "character.active.peekTopAndTakeTaggedContents":
            case "character.active.discardOneThenFetchContentByTagFromDeck":
            case "content.peekTopAndTakeTaggedCharacterOrBottom":
            case "content.drawThenDiscard":
            case "content.redrawIfBehindAndUniverseOnly":
            case "idol.active.fetchTabiOrRestBoongAndFetchBoth":
                return Meta(normalized, OnlineEffectSupportCategory.RequiresPrivateInfoProtocol, "deck/hand/rest search, draw, discard, reveal, deck order changes", "CardRevealDelta, CardDrawDelta, CardZoneMoveDelta, DeckOrderDelta, SelectionRequestDelta, MessageDelta", "pending private protocol", true, true, true, false, false);
            case "content.forceOpponentSummonOrSackFromHand":
                return Meta(normalized, OnlineEffectSupportCategory.RequiresPrivateInfoProtocol, "opponent private hand choice then summon or discard", "SelectionRequestDelta, CardZoneMoveDelta, CardRevealDelta, MessageDelta", "pending private protocol", true, false, true, false, false);
            case "content.returnUpToNFromRestToDeck":
                return Meta(normalized, OnlineEffectSupportCategory.RequiresSelectionFlow, "rest zone card selection and rest to deck move", "SelectionRequestDelta, CardZoneMoveDelta, DeckOrderDelta, MessageDelta", "pending selection flow", true, false, false, false, false);
            case "content.lasting.buffTagTensionAndHp":
            case "content.lockBroadcastIdNoMoveNoKOUntilNextEnd":
            case "content.invertNegativeAmountForTagThisTurn":
            case "content.forbidOpponentAttackUntilNextTurn":
            case "broadcast.always.noFaceDownSummonAndDisablePreCollabEffects":
            case "broadcast.always.disableIdolActiveAndLockMoveOnEnter":
            case "character.passive.doubleStepMoveNoJump":
            case "character.passive.adjacentCollabTensionDeltaForTag":
            case "character.rest.reduceOpponentCollabTensionOnCollab":
            case "idol.passive.collabNoKOByTag":
            case "idol.passive.collabTensionByCurrentHpForTag":
            case "idol.passive.allowActionOnAppearByTag":
                return Meta(normalized, OnlineEffectSupportCategory.RequiresPersistentStateDelta, "continuous or turn-duration rules, locks, silence, collab modifiers, KO prevention", "StatusDelta, ActionStateDelta, MessageDelta", "pending persistent state", false, false, false, true, true);
            case "broadcast.always.prepViewersAndOccupantHpDelta":
            case "broadcast.always.taggedOccupantPrepViewersBonus":
            case "broadcast.always.prepViewersAndHealBonus":
                return Meta(normalized, OnlineEffectSupportCategory.IncludedInHostCalculation, "prep viewer modifier and/or broadcast occupant HP max modifier are read by host calculation and slot state refresh", "BattleCountSnapshot, slot battle stats", "included in host calculation", false, false, false, false, true);
            case "broadcast.always.gainViewersWhenOccupantLeaves":
                return Meta(normalized, OnlineEffectSupportCategory.RequiresTimingTriggerResult, "viewer gain when public field occupant leaves broadcast slot", "ViewerDelta, MessageDelta", "pending leave trigger result", false, false, false, false, true);
            case "character.passive.viewersBonusIfAdjacentToTag":
            case "character.passive.reduceOwnerPrepViewers":
                return Meta(normalized, OnlineEffectSupportCategory.RequiresTimingTriggerResult, "broadcast/passive trigger modifies viewers or occupant HP", "ViewerDelta, FieldStatDelta, MessageDelta", "pending trigger result", false, false, false, false, true);
            case "character.active.forceBattleTargetAnywhere":
                return Meta(normalized, OnlineEffectSupportCategory.RequiresSelectionFlow, "battle target selection override", "SelectionRequestDelta, StatusDelta, ActionStateDelta, MessageDelta", "pending selection flow", true, false, false, true, false);
            case "content.postCollabTabiBoostAndRebattle":
            case "content.collabClicheSpendBuffRefund":
                return Meta(normalized, OnlineEffectSupportCategory.RequiresTimingTriggerResult, "post-collab battle/tension/viewer follow-up", "StatusDelta, FieldStatDelta, ViewerDelta, ActionStateDelta, MessageDelta", "pending trigger result", false, false, false, true, true);
            default:
                return Meta(normalized, OnlineEffectSupportCategory.UnsupportedForNow, "unknown or data/code mismatch", "MessageDelta", "unsupported");
        }
    }

    public OnlineEffectResolveResult ResolveForHost(
        BattleAction action,
        BaseCardData sourceCard,
        BattleSlotOwner owner)
    {
        OnlineEffectResolveResult result = Classify(action, sourceCard);
        if (!result.success)
            return result;

        TryResolveSimpleImmediateEffect(result, sourceCard, FindSourceEffect(sourceCard, result.usedEffectRef), owner);

        return result;
    }

    public OnlineEffectResolveResult ResolveSimpleEffectForHost(
        string effectRef,
        BaseCardData sourceCard,
        EffectData sourceEffect,
        BattleSlotOwner owner)
    {
        OnlineEffectResolveResult result = new OnlineEffectResolveResult
        {
            usedEffectRef = NormalizeEffectRef(effectRef)
        };

        OnlineEffectRefMetadata metadata = GetMetadata(result.usedEffectRef);
        ApplyMetadata(result, metadata);
        DebugLogClassification(result, metadata);

        if (battleManager == null)
            return Reject(result, "BattleManager is missing.");

        if (metadata.category != OnlineEffectSupportCategory.SupportedImmediateWithDelta &&
            metadata.category != OnlineEffectSupportCategory.SupportedImmediate &&
            metadata.category != OnlineEffectSupportCategory.CurrentDeltaSupported)
        {
            return RejectUnsupported(
                result,
                $"단순 Delta로 즉시 처리할 수 없는 effectRef입니다: {result.usedEffectRef} ({metadata.category})");
        }

        EffectData resolvedSourceEffect = sourceEffect ?? FindSourceEffect(sourceCard, result.usedEffectRef);

        if (!TryResolveSimpleImmediateEffect(result, sourceCard, resolvedSourceEffect, owner))
        {
            return RejectUnsupported(
                result,
                $"지원 카테고리이나 Delta 생성기가 아직 없습니다: {result.usedEffectRef}");
        }

        result.success = true;
        return result;
    }

    private OnlineEffectResolveResult Classify(BattleAction action, BaseCardData sourceCard)
    {
        OnlineEffectResolveResult result = new OnlineEffectResolveResult
        {
            usedEffectRef = ResolveEffectRef(action, sourceCard)
        };

        if (battleManager == null)
            return Reject(result, "BattleManager is missing.");

        if (action == null)
            return Reject(result, "UseContent action is null.");

        if (sourceCard == null)
            return Reject(result, "UseContent source card is missing.");

        if (action.effectTiming != EffectTiming.Content)
        {
            result.requiresPrePostCollabTiming = true;
            return Reject(result, "온라인 1차 UseContent는 Content 타이밍만 지원합니다.");
        }

        if (action.selectedTargetIds != null && action.selectedTargetIds.Count > 0)
        {
            result.requiresTargetSelection = true;
            return Reject(result, "온라인 1차 UseContent는 대상 선택형 효과를 지원하지 않습니다.");
        }

        if (action.selectedCardIds != null && action.selectedCardIds.Count > 0)
        {
            result.requiresCardSelection = true;
            return Reject(result, "온라인 1차 UseContent는 카드 선택형 효과를 지원하지 않습니다.");
        }

        if (string.IsNullOrWhiteSpace(result.usedEffectRef))
            return Reject(result, "온라인 UseContent effectRef를 확인할 수 없습니다.");

        OnlineEffectRefMetadata metadata = GetMetadata(result.usedEffectRef);
        ApplyMetadata(result, metadata);

        DebugLogClassification(result, metadata);

        if (metadata.category == OnlineEffectSupportCategory.CurrentDeltaSupported ||
            metadata.category == OnlineEffectSupportCategory.SupportedImmediate ||
            metadata.category == OnlineEffectSupportCategory.SupportedImmediateWithDelta)
        {
            result.success = true;
            return result;
        }

        return RejectUnsupported(
            result,
            $"온라인 1차 UseContent는 아직 {metadata.category} effectRef를 처리하지 않습니다: {result.usedEffectRef}");
    }

    private void ResolveRemoveAllLastingContents(OnlineEffectResolveResult result)
    {
        IReadOnlyList<BattleFieldSlot> slots = battleManager.GetOnlineEffectFieldSlotsFromExternal();
        if (slots == null)
            return;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot == null ||
                slot.contentCard == null ||
                !battleManager.IsLastingContentCardFromExternal(slot.contentCard))
            {
                continue;
            }

            string slotId = slot.GetSlotId();
            BaseCardData contentCard = slot.contentCard;

            result.fieldContentDeltas.Add(new FieldContentDelta
            {
                slotId = slotId,
                contentCardInstanceId = contentCard.cardInstanceId,
                contentCardId = contentCard.id,
                contentOwner = slot.contentOwner,
                removed = true,
                movedToRest = true
            });

            result.zoneMoveDeltas.Add(new CardZoneMoveDelta
            {
                cardInstanceId = contentCard.cardInstanceId,
                cardId = contentCard.id,
                owner = slot.contentOwner,
                fromZone = "FieldContent",
                toZone = "RestZone",
                fromSlotId = slotId,
                isPublic = true,
                faceDown = false
            });
        }

        string message = $"장기 콘텐츠 {result.fieldContentDeltas.Count}장을 휴식존으로 보냅니다.";
        result.messages.Add(message);
        result.messageDeltas.Add(new MessageDelta
        {
            audience = "Public",
            messageKey = "content.removeAllLastingContentsOnBoard",
            messageText = message
        });
    }

    private bool TryResolveSimpleImmediateEffect(
        OnlineEffectResolveResult result,
        BaseCardData sourceCard,
        EffectData sourceEffect,
        BattleSlotOwner owner)
    {
        if (result == null)
            return false;

        switch (NormalizeEffectRef(result.usedEffectRef))
        {
            case RemoveAllLastingContentsOnBoard:
                ResolveRemoveAllLastingContents(result);
                return true;
            case "character.rest.gainViewers":
                ResolveViewerDelta(result, sourceCard, sourceEffect, owner, GetIntParam(sourceEffect, "amount", 0));
                return true;
            case "character.rest.loseViewers":
                ResolveViewerDelta(result, sourceCard, sourceEffect, owner, -GetIntParam(sourceEffect, "amount", 0));
                return true;
            default:
                return false;
        }
    }

    private void ResolveViewerDelta(
        OnlineEffectResolveResult result,
        BaseCardData sourceCard,
        EffectData sourceEffect,
        BattleSlotOwner owner,
        int amount)
    {
        int before = battleManager.GetViewersFromExternal(owner);
        int after = Math.Max(0, before + amount);
        int actualAmount = after - before;

        result.viewerDeltas.Add(new ViewerDelta
        {
            owner = owner,
            before = before,
            after = after,
            amount = amount
        });

        string sign = actualAmount >= 0 ? "+" : "";
        string message = $"{(sourceCard != null ? sourceCard.name : "카드")} 효과: 시청자 {sign}{actualAmount}";
        result.messages.Add(message);
        result.messageDeltas.Add(new MessageDelta
        {
            audience = "Public",
            messageKey = NormalizeEffectRef(result.usedEffectRef),
            messageText = message,
            relatedCardId = sourceCard != null ? sourceCard.id : "",
            relatedInstanceId = sourceCard != null ? sourceCard.cardInstanceId : ""
        });

        UnityEngine.Debug.Log(
            $"[OnlineEffectResolver] ViewerDelta generated. effectRef={result.usedEffectRef}, " +
            $"owner={owner}, before={before}, after={after}, amount={actualAmount}");
    }

    private string ResolveEffectRef(BattleAction action, BaseCardData sourceCard)
    {
        if (action != null && !string.IsNullOrWhiteSpace(action.effectRef))
            return action.effectRef;

        return battleManager != null
            ? battleManager.GetPrimaryContentEffectRefFromExternal(sourceCard)
            : "";
    }

    private static EffectData FindSourceEffect(BaseCardData card, string effectRef)
    {
        if (card == null)
            return null;

        CharacterCardData character = card as CharacterCardData;
        if (character != null)
            return FindEffect(character.effects, effectRef);

        ContentCardData content = card as ContentCardData;
        if (content != null)
            return FindEffect(content.effects, effectRef);

        BroadcastCardData broadcast = card as BroadcastCardData;
        if (broadcast != null)
            return FindEffect(broadcast.effects, effectRef);

        IdolCardData idol = card as IdolCardData;
        if (idol != null)
        {
            EffectData active = FindEffect(idol.active, effectRef);
            if (active != null)
                return active;

            return FindEffect(idol.passive, effectRef);
        }

        return null;
    }

    private static EffectData FindEffect(EffectData[] effects, string effectRef)
    {
        if (effects == null || string.IsNullOrWhiteSpace(effectRef))
            return null;

        foreach (EffectData effect in effects)
        {
            if (effect == null)
                continue;

            string candidate = !string.IsNullOrWhiteSpace(effect.@ref)
                ? effect.@ref
                : effect.refName;

            if (string.Equals(candidate, effectRef, StringComparison.OrdinalIgnoreCase))
                return effect;
        }

        return null;
    }

    private static int GetIntParam(EffectData effect, string key, int defaultValue)
    {
        EffectParams effectParams = effect != null ? effect.@params : null;
        if (effectParams == null || string.IsNullOrWhiteSpace(key))
            return defaultValue;

        switch (key)
        {
            case "amount":
                return effectParams.amount;
            case "hp":
                return effectParams.hp;
            case "tension":
                return effectParams.tension;
            case "tensionDelta":
                return effectParams.tensionDelta;
            case "hpMaxDelta":
                return effectParams.hpMaxDelta;
            default:
                return defaultValue;
        }
    }

    private static string NormalizeEffectRef(string effectRef)
    {
        return string.IsNullOrWhiteSpace(effectRef)
            ? ""
            : effectRef.Trim();
    }

    private static OnlineEffectRefMetadata Meta(
        string effectRef,
        OnlineEffectSupportCategory category,
        string stateChanges,
        string deltaTypes,
        string onlineStatus,
        bool requiresSelectionFlow = false,
        bool requiresRandomResolution = false,
        bool requiresPrivateInfoProtocol = false,
        bool requiresPersistentStateDelta = false,
        bool requiresTimingTriggerResult = false)
    {
        return new OnlineEffectRefMetadata
        {
            effectRef = effectRef,
            category = category,
            stateChanges = stateChanges,
            deltaTypes = deltaTypes,
            onlineStatus = onlineStatus,
            requiresSelectionFlow = requiresSelectionFlow,
            requiresRandomResolution = requiresRandomResolution,
            requiresPrivateInfoProtocol = requiresPrivateInfoProtocol,
            requiresPersistentStateDelta = requiresPersistentStateDelta,
            requiresTimingTriggerResult = requiresTimingTriggerResult
        };
    }

    private static void ApplyMetadata(
        OnlineEffectResolveResult result,
        OnlineEffectRefMetadata metadata)
    {
        if (result == null || metadata == null)
            return;

        result.classification = metadata.category.ToString();
        result.requiresFollowUpAction =
            metadata.category != OnlineEffectSupportCategory.CurrentDeltaSupported;
        result.requiresTargetSelection = metadata.requiresSelectionFlow;
        result.requiresCardSelection = metadata.requiresSelectionFlow ||
            metadata.requiresPrivateInfoProtocol;
        result.requiresMultipleSelection = metadata.requiresSelectionFlow &&
            metadata.deltaTypes.Contains("SelectionRequestDelta");
        result.requiresRandomResolution = metadata.requiresRandomResolution;
        result.requiresPrivateInfoHandling = metadata.requiresPrivateInfoProtocol;
        result.requiresPersistentStateDelta = metadata.requiresPersistentStateDelta;
        result.requiresTimingTriggerResult = metadata.requiresTimingTriggerResult;
        result.requiresPrePostCollabTiming = metadata.requiresTimingTriggerResult;
        result.requiresPersistentContentInstall =
            metadata.effectRef.StartsWith("content.lasting.", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(metadata.effectRef, "content.lockBroadcastIdNoMoveNoKOUntilNextEnd", StringComparison.OrdinalIgnoreCase);
    }

    private static void DebugLogClassification(
        OnlineEffectResolveResult result,
        OnlineEffectRefMetadata metadata)
    {
        UnityEngine.Debug.Log(
            $"[OnlineEffectResolver] effectRef={result.usedEffectRef}, " +
            $"category={metadata.category}, deltas={metadata.deltaTypes}, status={metadata.onlineStatus}");
    }

    private static OnlineEffectResolveResult Reject(
        OnlineEffectResolveResult result,
        string reason)
    {
        result.success = false;
        result.rejectReason = reason;
        return result;
    }

    private static OnlineEffectResolveResult RejectUnsupported(
        OnlineEffectResolveResult result,
        string reason)
    {
        result.success = false;
        result.rejectReason = reason;
        result.unsupportedReason = reason;
        return result;
    }
}
