using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EffectTiming
{
    None,
    Content,
    PreCollab,
    PostCollab,
    OnAppear,
    OnRest,
    Passive,
    CharacterActive,
    IdolActive,
    Broadcast,
    TurnStart,
    TurnEnd
}

public enum EffectSourceZone
{
    Unknown,
    Hand,
    Field,
    RestZone
}

public class EffectContext
{
    public BattleManager battleManager;
    public CollaborationManager collaborationManager;
    public BattleSlotOwner actingOwner = BattleSlotOwner.My;
    public EffectTiming timing = EffectTiming.None;
    public BattleFieldSlot sourceSlot;
    public BattleFieldSlot targetSlot;
    public BattleFieldSlot attackerOriginalSlot;
    public BattleFieldSlot attackerSlot;
    public BattleFieldSlot defenderSlot;
    public BattleFieldSlot battleLocationSlot;
    public BattleFieldSlot defeatedSlot;
    public BattleFieldSlot restedSlot;
    public BaseCardData sourceCard;
    public BaseCardData targetCard;
    public BaseCardData defeatedCard;
    public BaseCardData restedCard;
    public EffectData sourceEffect;
    public bool consumeAction = true;
}

public class EffectCandidate
{
    public BaseCardData card;
    public BattleSlotOwner owner = BattleSlotOwner.My;
    public EffectSourceZone sourceZone = EffectSourceZone.Unknown;
    public BattleFieldSlot sourceSlot;
    public BattleFieldSlot targetSlot;
    public int handIndex = -1;
    public string refId;
    public EffectData sourceEffect;
    public EffectTiming timing = EffectTiming.None;
    public bool consumeAction = true;
}

public class EffectActivationRequest
{
    public BaseCardData sourceCard;
    public BattleSlotOwner owner;
    public EffectTiming timing;
    public BattleFieldSlot sourceSlot;
    public BattleFieldSlot targetSlot;
    public int handIndex = -1;
    public bool consumeAction;
    public Action<bool> onComplete;
}

public class RestReturnedCharacterEntry
{
    public BattleFieldSlot slot;
    public BaseCardData card;
}

public class RestReturnOnAppearChoice
{
    public EffectCandidate candidate;
    public EffectContext context;
}

public class EffectManager : MonoBehaviour
{
    private const int MaxRestReturnOnAppearDepth = 8;

    [SerializeField] private BattleManager battleManager;
    private int restReturnOnAppearDepth;
    private PendingOurTalesState pendingOurTales;
    private PendingPostCollabRebattleState pendingPostCollabRebattle;
    private readonly List<NegativeAmountInvertState> negativeAmountInvertStates =
        new List<NegativeAmountInvertState>();

    private class PendingOurTalesState
    {
        public BattleSlotOwner owner;
        public BaseCardData sourceCard;
        public BattleFieldSlot attackerSlot;
        public BattleFieldSlot defenderSlot;
        public BattleFieldSlot participantSlot;
        public BaseCardData participantCard;
        public bool processed;
    }

    private class PendingPostCollabRebattleState
    {
        public BattleSlotOwner owner;
        public BaseCardData sourceCard;
        public BattleFieldSlot attackerSlot;
        public BattleFieldSlot defenderSlot;
        public bool processed;
    }

    private class NegativeAmountInvertState
    {
        public BattleSlotOwner owner;
        public string sourceTag;
        public int turn;
        public BaseCardData sourceContentCard;
    }

    public void Init(BattleManager manager)
    {
        battleManager = manager;
    }

    private void Awake()
    {
        if (battleManager == null)
            battleManager = GetComponentInParent<BattleManager>();
    }

    public void ClearPendingEffectActivationFromExternal()
    {
        CardQuestionPanel panel = battleManager != null
            ? battleManager.BattleCardQuestionPanel
            : null;

        if (panel != null && panel.IsOpen())
            panel.Hide();
    }

    public bool IsEffectRefImplementedFromExternal(string effectRef)
    {
        return IsEffectRefImplemented(effectRef);
    }

    private bool IsEffectRefImplemented(string effectRef)
    {
        if (string.IsNullOrWhiteSpace(effectRef))
            return true;

        switch (effectRef.Trim())
        {
            case "idol.passive.collabNoKOByTag":
            case "idol.passive.collabTensionByCurrentHpForTag":
            case "idol.passive.allowActionOnAppearByTag":
            case "idol.active.fullHealOneControlled":
            case "idol.active.callFromRestByTagThenDonateViewers":
            case "idol.active.fetchTabiOrRestBoongAndFetchBoth":
            case "character.rest.gainViewers":
            case "character.rest.loseViewers":
            case "character.rest.reduceOpponentCollabTensionOnCollab":
            case "character.passive.viewersBonusIfAdjacentToTag":
            case "character.passive.reduceOwnerPrepViewers":
            case "character.passive.doubleStepMoveNoJump":
            case "character.passive.adjacentCollabTensionDeltaForTag":
            case "broadcast.always.prepViewersAndOccupantHpDelta":
            case "broadcast.always.taggedOccupantPrepViewersBonus":
            case "broadcast.always.prepViewersAndHealBonus":
            case "broadcast.always.noFaceDownSummonAndDisablePreCollabEffects":
            case "broadcast.always.disableIdolActiveAndLockMoveOnEnter":
            case "broadcast.always.gainViewersWhenOccupantLeaves":
            case "character.fetchCardsToHandByTags":
            case "character.active.peekTopAndTakeTaggedContents":
            case "character.active.discardOneThenFetchContentByTagFromDeck":
            case "character.active.forceBattleTargetAnywhere":
            case "character.active.modifyTaggedOnBoard":
            case "character.active.adjacentHpDownAndTensionUpForTag":
            case "character.onAppear.callFromRestByTagToEmptyPlatforms":
            case "character.onAppear.adjacentOppCollabTensionDeltaThisTurn":
            case "character.active.adjacentOppCollabTensionDeltaThisTurn":
            case "content.drawThenDiscard":
            case "content.postCollabHealOwnParticipant":
            case "content.silenceCharacterCollabThisTurn":
            case "content.lockBroadcastIdNoMoveNoKOUntilNextEnd":
            case "content.forbidOpponentAttackUntilNextTurn":
            case "content.removeAllLastingContentsOnBoard":
            case "content.returnUpToNFromRestToDeck":
            case "content.lasting.buffTagTensionAndHp":
            case "content.peekTopAndTakeTaggedCharacterOrBottom":
            case "content.moveOwnCharToEmptyOrBattleIfTagged":
            case "content.redrawIfBehindAndUniverseOnly":
            case "content.collabClicheSpendBuffRefund":
            case "content.forceOpponentFlipOrSack":
            case "content.forceOpponentSummonOrSackFromHand":
            case "content.postCollabTabiBoostAndRebattle":
            case "content.invertNegativeAmountForTagThisTurn":
                return true;

            default:
                return false;
        }
    }

    public bool ShouldDeferZeroHpDuringCollab(BattleFieldSlot slot)
    {
        return HasIdolPassiveForSlot(
            slot,
            "idol.passive.collabNoKOByTag",
            out _
        );
    }

    public int GetIdolPassiveCollabTensionModifier(BattleFieldSlot slot)
    {
        EffectContext context = new EffectContext
        {
            battleManager = battleManager,
            collaborationManager = battleManager != null ? battleManager.collaborationManager : null,
            timing = EffectTiming.Passive,
            sourceSlot = slot,
            sourceCard = slot != null ? slot.characterCard : null,
            attackerOriginalSlot = slot,
            attackerSlot = slot,
            defenderSlot = slot,
            battleLocationSlot = slot,
            actingOwner = slot != null ? slot.characterOwner : BattleSlotOwner.My,
            consumeAction = false
        };

        return GetIdolPassiveCollabTensionModifier(slot, context);
    }

    public int GetIdolPassiveCollabTensionModifier(
        BattleFieldSlot slot,
        EffectContext context)
    {
        if (slot == null ||
            !slot.HasCharacter ||
            slot.characterCard == null ||
            slot.currentCharacterHp <= 0)
        {
            return 0;
        }

        EffectContext safeContext = NormalizeContext(context, EffectTiming.Passive);
        bool onOwnBroadcastSlot = IsCharacterCollabingOnOwnBroadcastSlot(slot.characterOwner, safeContext);
        if (!HasIdolPassiveForSlot(
            slot,
            "idol.passive.collabTensionByCurrentHpForTag",
            out _))
        {
            LogRinPassiveCheck(slot, safeContext, false);
            return 0;
        }

        if (!onOwnBroadcastSlot)
        {
            LogRinPassiveCheck(slot, safeContext, false);
            return 0;
        }

        LogRinPassiveCheck(slot, safeContext, true);
        return Mathf.Max(0, slot.currentCharacterHp);
    }

    public BattleFieldSlot GetCollaborationBattleLocationSlot(EffectContext context)
    {
        if (context != null && context.battleLocationSlot != null)
            return context.battleLocationSlot;

        if (context != null && context.defenderSlot != null)
            return context.defenderSlot;

        return null;
    }

    public bool IsCharacterCollabingOnOwnBroadcastSlot(
        BattleSlotOwner characterOwner,
        EffectContext context)
    {
        BattleFieldSlot battleSlot = GetCollaborationBattleLocationSlot(context);

        if (battleSlot == null)
            return false;

        return battleSlot.owner == characterOwner;
    }

    public List<EffectTargetCandidate> BuildTargetCandidates(
        TargetSelector selector,
        EffectContext context)
    {
        EffectContext safeContext = context ?? new EffectContext();

        if (safeContext.battleManager == null)
            safeContext.battleManager = battleManager;

        if (safeContext.collaborationManager == null && battleManager != null)
            safeContext.collaborationManager = battleManager.collaborationManager;

        return EffectTargetingService.BuildTargetCandidates(selector, safeContext);
    }

    public ZoneMoveResult MoveCardBetweenZones(
        ZoneMoveRequest request,
        EffectContext context)
    {
        EffectContext safeContext = context ?? new EffectContext();

        if (safeContext.battleManager == null)
            safeContext.battleManager = battleManager;

        if (safeContext.collaborationManager == null && battleManager != null)
            safeContext.collaborationManager = battleManager.collaborationManager;

        return EffectZoneMoveService.MoveCardBetweenZones(request, safeContext);
    }

    public List<ZoneMoveResult> MoveCardsBetweenZones(
        List<ZoneMoveRequest> requests,
        EffectContext context)
    {
        EffectContext safeContext = context ?? new EffectContext();

        if (safeContext.battleManager == null)
            safeContext.battleManager = battleManager;

        if (safeContext.collaborationManager == null && battleManager != null)
            safeContext.collaborationManager = battleManager.collaborationManager;

        return EffectZoneMoveService.MoveCardsBetweenZones(requests, safeContext);
    }

    public void PeekTopSelectToHand(
        PeekTopSelectRequest request,
        EffectContext context,
        Action<PeekTopSelectResult> onComplete)
    {
        EffectContext safeContext = context ?? new EffectContext();

        if (safeContext.battleManager == null)
            safeContext.battleManager = battleManager;

        if (safeContext.collaborationManager == null && battleManager != null)
            safeContext.collaborationManager = battleManager.collaborationManager;

        EffectDeckPeekService.PeekTopSelectToHand(request, safeContext, onComplete);
    }

    public void SearchDeckSelectToHand(
        SearchDeckSelectRequest request,
        EffectContext context,
        Action<SearchDeckSelectResult> onComplete)
    {
        EffectContext safeContext = context ?? new EffectContext();

        if (safeContext.battleManager == null)
            safeContext.battleManager = battleManager;

        if (safeContext.collaborationManager == null && battleManager != null)
            safeContext.collaborationManager = battleManager.collaborationManager;

        EffectDeckPeekService.SearchDeckSelectToHand(request, safeContext, onComplete);
    }

    public void ModifyCharacterStats(
        ModifyCharacterStatsRequest request,
        EffectContext context,
        Action<ModifyCharacterStatsResult> onComplete)
    {
        EffectContext safeContext = context ?? new EffectContext();

        if (safeContext.battleManager == null)
            safeContext.battleManager = battleManager;

        if (safeContext.collaborationManager == null && battleManager != null)
            safeContext.collaborationManager = battleManager.collaborationManager;

        EffectStatService.ModifyCharacterStats(request, safeContext, onComplete);
    }

    public bool CanIgnoreAppearTurnActionLimit(BattleFieldSlot slot)
    {
        return HasIdolPassiveForSlot(
            slot,
            "idol.passive.allowActionOnAppearByTag",
            out _
        );
    }

    public List<EffectCandidate> GetPlayableEffects(
        EffectTiming timing,
        EffectContext context)
    {
        List<EffectCandidate> candidates = new List<EffectCandidate>();

        if (battleManager == null)
            return candidates;

        EffectContext safeContext = NormalizeContext(context, timing);

        switch (timing)
        {
            case EffectTiming.Content:
                CollectContentTimingHandCandidates(safeContext, candidates);
                break;

            case EffectTiming.PreCollab:
                CollectPreCollabHandCandidates(safeContext, candidates);
                break;

            case EffectTiming.PostCollab:
                CollectPostCollabCandidates(safeContext, candidates);
                break;

            case EffectTiming.OnAppear:
                CollectCharacterTimingCandidate(safeContext, EffectTiming.OnAppear, candidates);
                break;

            case EffectTiming.OnRest:
                CollectCharacterTimingCandidate(safeContext, EffectTiming.OnRest, candidates);
                break;

            case EffectTiming.Passive:
            case EffectTiming.CharacterActive:
            case EffectTiming.IdolActive:
            case EffectTiming.Broadcast:
            case EffectTiming.TurnStart:
            case EffectTiming.TurnEnd:
                // TODO: 각 타이밍별 필드/손패/방송 카드 후보 수집 규칙을 추가한다.
                break;
        }

        return candidates;
    }

    public bool CanActivateEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        string failReason;
        return CanActivateEffect(candidate, context, out failReason);
    }

    public void RequestOptionalEffectActivation(
        EffectTiming timing,
        EffectContext context,
        Action onComplete)
    {
        bool completed = false;
        Action completeOnce = () =>
        {
            if (completed)
                return;

            completed = true;
            onComplete?.Invoke();
        };

        try
        {
            EffectContext safeContext = NormalizeContext(context, timing);
            List<EffectCandidate> candidates = GetPlayableEffects(timing, safeContext);

            if (candidates.Count == 0)
            {
                completeOnce();
                return;
            }

            if (AreAllMandatoryEffectActivations(timing, candidates))
            {
                ResolveCandidatesSequentially(candidates, safeContext, 0, completeOnce);
                return;
            }

            CardQuestionPanel panel = battleManager != null
                ? battleManager.BattleCardQuestionPanel
                : null;

            if (panel == null)
            {
                battleManager?.SetSystemMessageFromExternal("발동 가능한 카드가 있지만 CardQuestionPanel이 연결되어 있지 않습니다.");
                Debug.LogWarning($"EffectManager: {timing} 후보 {candidates.Count}장을 감지했지만 CardQuestionPanel이 없습니다.");
                completeOnce();
                return;
            }

            if (panel.IsOpen())
            {
                battleManager?.SetSystemMessageFromExternal("이미 카드 선택창이 열려 있습니다.");
                completeOnce();
                return;
            }

            List<CardQuestionOption> options = BuildOptionsFromCandidates(candidates);
            bool opened = panel.TryShowOptions(
                GetOptionalEffectQuestionMessage(timing),
                options,
                CanCancelEffectActivation(timing, candidates),
                selectedOption =>
                {
                    EffectCandidate selectedCandidate = selectedOption != null
                        ? selectedOption.linkedCandidate
                        : null;
                    ResolveEffect(selectedCandidate, safeContext, completeOnce);
                },
                () =>
                {
                    battleManager?.SetSystemMessageFromExternal(GetOptionalEffectCancelMessage(timing));
                    completeOnce();
                }
            );

            if (!opened)
            {
                battleManager?.SetSystemMessageFromExternal("카드 선택창을 열 수 없습니다.");
                completeOnce();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            completeOnce();
        }
    }

    private bool CanCancelEffectActivation(
        EffectTiming timing,
        List<EffectCandidate> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return true;

        foreach (EffectCandidate candidate in candidates)
        {
            if (IsMandatoryEffectActivation(timing, candidate))
                return false;
        }

        return true;
    }

    private bool AreAllMandatoryEffectActivations(
        EffectTiming timing,
        List<EffectCandidate> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return false;

        foreach (EffectCandidate candidate in candidates)
        {
            if (!IsMandatoryEffectActivation(timing, candidate))
                return false;
        }

        return true;
    }

    private bool IsMandatoryEffectActivation(
        EffectTiming timing,
        EffectCandidate candidate)
    {
        if (candidate == null || candidate.card == null)
            return false;

        return timing == EffectTiming.OnRest &&
            candidate.card is CharacterCardData;
    }

    private void ResolveCandidatesSequentially(
        List<EffectCandidate> candidates,
        EffectContext context,
        int index,
        Action onComplete)
    {
        if (candidates == null || index >= candidates.Count)
        {
            onComplete?.Invoke();
            return;
        }

        ResolveEffect(
            candidates[index],
            context,
            () => ResolveCandidatesSequentially(candidates, context, index + 1, onComplete)
        );
    }

    public void ResolveEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action onComplete)
    {
        if (candidate != null && IsSearchDeckSelectToHandEffect(candidate, context))
        {
            ResolveSearchDeckSelectToHandEffect(candidate, context, onComplete);
            return;
        }

        if (candidate != null && IsPeekTopSelectToHandEffect(candidate, context))
        {
            ResolvePeekTopSelectToHandEffect(candidate, context, onComplete);
            return;
        }

        if (candidate != null && IsModifyCharacterStatsEffect(candidate, context))
        {
            ResolveModifyCharacterStatsEffect(candidate, context, onComplete);
            return;
        }

        if (candidate != null && IsDrawThenDiscardEffect(candidate, context))
        {
            ResolveDrawThenDiscardEffect(candidate, context, onComplete);
            return;
        }

        if (candidate != null && IsMoveOwnCharToEmptyOrBattleIfTaggedEffect(candidate, context))
        {
            ResolveMoveOwnCharToEmptyOrBattleIfTaggedEffect(candidate, context, _ => onComplete?.Invoke());
            return;
        }

        if (candidate != null && IsLockBroadcastIdNoMoveNoKOUntilNextEndEffect(candidate, context))
        {
            ResolveLockBroadcastIdNoMoveNoKOUntilNextEndEffect(candidate, context, _ => onComplete?.Invoke());
            return;
        }

        if (candidate != null && IsForbidOpponentAttackUntilNextTurnEffect(candidate, context))
        {
            ResolveForbidOpponentAttackUntilNextTurnEffect(candidate, context, _ => onComplete?.Invoke());
            return;
        }

        if (candidate != null && IsCallFromRestByTagToEmptyPlatformsEffect(candidate, context))
        {
            ResolveCallFromRestByTagToEmptyPlatformsEffect(
                candidate,
                context,
                _ => onComplete?.Invoke());
            return;
        }

        if (candidate != null && IsCollabClicheSpendBuffRefundEffect(candidate, context))
        {
            ResolveCollabClicheSpendBuffRefundEffect(candidate, context, _ => onComplete?.Invoke());
            return;
        }

        if (candidate != null && IsForceOpponentFlipOrSackEffect(candidate, context))
        {
            ResolveForceOpponentFlipOrSackEffect(candidate, context, _ => onComplete?.Invoke());
            return;
        }

        if (candidate != null && IsForceOpponentSummonOrSackFromHandEffect(candidate, context))
        {
            ResolveForceOpponentSummonOrSackFromHandEffect(candidate, context, _ => onComplete?.Invoke());
            return;
        }

        if (candidate != null && IsPostCollabTabiBoostAndRebattleEffect(candidate, context))
        {
            ResolvePostCollabTabiBoostAndRebattleEffect(candidate, context, _ => onComplete?.Invoke());
            return;
        }

        if (!TryResolveEffect(candidate, context, out string failReason))
        {
            if (!string.IsNullOrEmpty(failReason))
                battleManager?.SetSystemMessageFromExternal(failReason);
        }

        onComplete?.Invoke();
    }

    public void ExecuteEffectByRef(
        EffectCandidate candidate,
        EffectContext context,
        Action onComplete)
    {
        if (candidate == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (IsForbidOpponentAttackUntilNextTurnEffect(candidate, context))
        {
            ResolveForbidOpponentAttackUntilNextTurnEffect(candidate, context, _ => onComplete?.Invoke());
            return;
        }

        ExecuteEffectByRefInternal(
            candidate,
            context,
            ShouldConsumeAction(candidate, context)
        );

        onComplete?.Invoke();
    }

    public bool TryActivateEffect(EffectActivationRequest request)
    {
        string failReason;
        if (!TryBuildCandidateFromRequest(request, out EffectCandidate candidate, out EffectContext context, out failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            return false;
        }

        if (IsDrawThenDiscardEffect(candidate, context))
        {
            ResolveDrawThenDiscardEffect(candidate, context, null);
            return true;
        }

        if (IsMoveOwnCharToEmptyOrBattleIfTaggedEffect(candidate, context))
        {
            ResolveMoveOwnCharToEmptyOrBattleIfTaggedEffect(candidate, context, request.onComplete);
            return true;
        }

        if (IsLockBroadcastIdNoMoveNoKOUntilNextEndEffect(candidate, context))
        {
            ResolveLockBroadcastIdNoMoveNoKOUntilNextEndEffect(candidate, context, request.onComplete);
            return true;
        }

        if (IsForbidOpponentAttackUntilNextTurnEffect(candidate, context))
        {
            ResolveForbidOpponentAttackUntilNextTurnEffect(candidate, context, request.onComplete);
            return true;
        }

        if (IsSearchDeckSelectToHandEffect(candidate, context))
        {
            ResolveSearchDeckSelectToHandEffect(candidate, context, null);
            return true;
        }

        if (IsDiscardOneThenFetchContentByTagFromDeckEffect(candidate, context))
        {
            ResolveDiscardOneThenFetchContentByTagFromDeckEffect(candidate, context, request.onComplete);
            return true;
        }

        if (IsForceBattleTargetAnywhereEffect(candidate, context))
        {
            ResolveForceBattleTargetAnywhereEffect(candidate, context, request.onComplete);
            return true;
        }

        if (IsIdolFullHealOneControlledEffect(candidate, context))
        {
            ResolveIdolFullHealOneControlledEffect(candidate, context, request.onComplete);
            return true;
        }

        if (IsCallFromRestByTagThenDonateViewersEffect(candidate, context))
        {
            ResolveCallFromRestByTagThenDonateViewersEffect(candidate, context, request.onComplete);
            return true;
        }

        if (IsFetchTabiOrRestBoongAndFetchBothEffect(candidate, context))
        {
            ResolveFetchTabiOrRestBoongAndFetchBothEffect(candidate, context, request.onComplete);
            return true;
        }

        if (IsCallFromRestByTagToEmptyPlatformsEffect(candidate, context))
        {
            ResolveCallFromRestByTagToEmptyPlatformsEffect(candidate, context, request.onComplete);
            return true;
        }

        if (IsCollabClicheSpendBuffRefundEffect(candidate, context))
        {
            ResolveCollabClicheSpendBuffRefundEffect(candidate, context, request.onComplete);
            return true;
        }

        if (IsForceOpponentFlipOrSackEffect(candidate, context))
        {
            ResolveForceOpponentFlipOrSackEffect(candidate, context, request.onComplete);
            return true;
        }

        if (IsForceOpponentSummonOrSackFromHandEffect(candidate, context))
        {
            ResolveForceOpponentSummonOrSackFromHandEffect(candidate, context, request.onComplete);
            return true;
        }

        if (IsPostCollabTabiBoostAndRebattleEffect(candidate, context))
        {
            ResolvePostCollabTabiBoostAndRebattleEffect(candidate, context, request.onComplete);
            return true;
        }

        if (IsReturnUpToNFromRestToDeckEffect(candidate, context))
        {
            ResolveReturnUpToNFromRestToDeckEffect(candidate, context, null);
            return true;
        }

        if (IsPeekTopSelectToHandEffect(candidate, context))
        {
            ResolvePeekTopSelectToHandEffect(candidate, context, null);
            return true;
        }

        if (IsModifyCharacterStatsEffect(candidate, context))
        {
            ResolveModifyCharacterStatsEffectWithResult(candidate, context, request.onComplete);
            return true;
        }

        if (!TryResolveEffect(candidate, context, out failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            return false;
        }

        return true;
    }

    public bool CanUseContentCardNow(
        BaseCardData card,
        BattleSlotOwner owner,
        out string failReason)
    {
        return CanUseCardAtTiming(card, EffectTiming.Content, owner, out failReason);
    }

    public List<BaseCardData> GetUsableContentCardsForTiming(
        IEnumerable<BaseCardData> cards,
        BattleSlotOwner owner,
        EffectTiming timing)
    {
        List<BaseCardData> usableCards = new List<BaseCardData>();

        if (cards == null)
            return usableCards;

        foreach (BaseCardData card in cards)
        {
            string failReason;
            if (CanUseCardAtTiming(card, timing, owner, out failReason))
                usableCards.Add(card);
        }

        return usableCards;
    }

    public bool CanUseCardAtTiming(
        BaseCardData card,
        EffectTiming timing,
        BattleSlotOwner owner,
        out string failReason)
    {
        failReason = "";

        if (battleManager == null)
        {
            failReason = "BattleManager가 연결되어 있지 않습니다.";
            return false;
        }

        if (card == null)
        {
            failReason = "사용할 카드 정보가 없습니다.";
            return false;
        }

        EffectContext context = new EffectContext
        {
            battleManager = battleManager,
            actingOwner = owner,
            timing = timing,
            sourceCard = card,
            consumeAction = timing == EffectTiming.Content
        };

        EffectCandidate candidate = new EffectCandidate
        {
            card = card,
            owner = owner,
            sourceZone = EffectSourceZone.Hand,
            handIndex = battleManager.FindHandCardIndexFromExternal(owner, card),
            refId = GetPrimaryEffectRef(card, timing),
            sourceEffect = GetPrimaryEffectData(card, timing),
            timing = ResolveCardEffectTiming(card, timing),
            consumeAction = timing == EffectTiming.Content
        };

        return CanActivateEffect(candidate, context, out failReason);
    }

    public EffectTiming ResolveCardTimingFromExternal(BaseCardData card)
    {
        return ResolveContentCardTiming(card);
    }

    public string GetPrimaryEffectRefFromExternal(BaseCardData card)
    {
        return GetPrimaryEffectRef(card);
    }

    private void CollectContentTimingHandCandidates(
        EffectContext context,
        List<EffectCandidate> candidates)
    {
        string actionFailReason;
        if (context.actingOwner != BattleSlotOwner.My ||
            !battleManager.CanUseMyActionFromExternal(out actionFailReason))
        {
            return;
        }

        CollectHandCandidates(context.actingOwner, EffectTiming.Content, context, candidates);
    }

    private void CollectPreCollabHandCandidates(
        EffectContext context,
        List<EffectCandidate> candidates)
    {
        BattleSlotOwner owner = context.actingOwner;

        // TODO: 방어자 우선권, 양 플레이어 순차 발동, 체인 처리 규칙을 정교화한다.
        CollectHandCandidates(owner, EffectTiming.PreCollab, context, candidates);
    }

    private void CollectPostCollabCandidates(
        EffectContext context,
        List<EffectCandidate> candidates)
    {
        BattleSlotOwner owner = context.actingOwner;

        CollectHandCandidates(owner, EffectTiming.PostCollab, context, candidates);
        CollectCharacterTimingCandidate(context, EffectTiming.PostCollab, candidates);
    }

    private void CollectCharacterTimingCandidate(
        EffectContext context,
        EffectTiming timing,
        List<EffectCandidate> candidates)
    {
        if (context == null || candidates == null)
            return;

        BaseCardData card = ResolveContextCharacterCard(context, timing);

        if (card == null)
            return;

        BattleFieldSlot slot = ResolveContextCharacterSlot(context, timing);
        BattleSlotOwner owner = timing == EffectTiming.OnRest
            ? context.actingOwner
            : slot != null
            ? slot.characterOwner
            : context.actingOwner;

        EffectCandidate candidate = new EffectCandidate
        {
            card = card,
            owner = owner,
            sourceZone = timing == EffectTiming.OnRest
                ? EffectSourceZone.RestZone
                : EffectSourceZone.Field,
            sourceSlot = slot,
            targetSlot = context.targetSlot,
            handIndex = -1,
            refId = GetPrimaryEffectRef(card, timing),
            sourceEffect = GetPrimaryEffectData(card, timing),
            timing = ResolveCardEffectTiming(card, timing),
            consumeAction = false
        };

        string failReason;
        if (CanActivateEffect(candidate, context, out failReason))
            candidates.Add(candidate);
    }

    private void CollectHandCandidates(
        BattleSlotOwner owner,
        EffectTiming timing,
        EffectContext context,
        List<EffectCandidate> candidates)
    {
        IReadOnlyList<BaseCardData> hand = battleManager.GetHandCardsFromExternal(owner);

        if (hand == null)
            return;

        for (int i = 0; i < hand.Count; i++)
        {
            BaseCardData card = hand[i];
            EffectCandidate candidate = new EffectCandidate
            {
                card = card,
                owner = owner,
                sourceZone = EffectSourceZone.Hand,
                handIndex = i,
                refId = GetPrimaryEffectRef(card, timing),
                sourceEffect = GetPrimaryEffectData(card, timing),
                timing = ResolveCardEffectTiming(card, timing),
                consumeAction = timing == EffectTiming.Content
            };

            string failReason;
            if (CanActivateEffect(candidate, context, out failReason))
                candidates.Add(candidate);
        }
    }

    private bool CanActivateEffect(
        EffectCandidate candidate,
        EffectContext context,
        out string failReason)
    {
        failReason = "";

        if (battleManager == null)
        {
            failReason = "BattleManager가 연결되어 있지 않습니다.";
            return false;
        }

        if (candidate == null)
        {
            failReason = "효과 후보 정보가 없습니다.";
            return false;
        }

        if (candidate.card == null)
        {
            failReason = "효과를 발동할 카드 정보가 없습니다.";
            return false;
        }

        EffectContext safeContext = NormalizeContext(context, candidate.timing);
        EffectTiming requestedTiming = safeContext.timing;

        if (!IsEffectCardKindSupportedAtTiming(candidate.card, requestedTiming))
        {
            failReason = "현재 타이밍에 발동할 수 없는 카드입니다.";
            return false;
        }

        if (candidate.sourceZone == EffectSourceZone.Hand &&
            !battleManager.IsCardInHandAtIndexFromExternal(candidate.owner, candidate.handIndex, candidate.card))
        {
            failReason = "손패에 있는 콘텐츠 카드만 발동할 수 있습니다.";
            return false;
        }

        if (candidate.timing != requestedTiming)
        {
            failReason = GetTimingMismatchMessage(candidate.timing);
            return false;
        }

        if (requestedTiming == EffectTiming.OnRest &&
            candidate.owner != BattleSlotOwner.My &&
            !IsMandatoryEffectActivation(requestedTiming, candidate))
        {
            failReason = "상대 카드의 휴식 시 효과는 내가 발동할 수 없습니다.";
            return false;
        }

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, requestedTiming);

        if (requestedTiming == EffectTiming.OnRest &&
            string.Equals(
                effectRef,
                "character.rest.reduceOpponentCollabTensionOnCollab",
                StringComparison.OrdinalIgnoreCase))
        {
            failReason = "이 휴식 효과는 합방으로 퇴장했을 때만 처리됩니다.";
            return false;
        }

        if (string.Equals(
                effectRef,
                "content.forbidOpponentAttackUntilNextTurn",
                StringComparison.OrdinalIgnoreCase) &&
            FindSurvivingOpponentCollabParticipantSlot(candidate.owner, safeContext) == null)
        {
            failReason = "합방 후 생존한 상대 캐릭터가 없어 발동할 수 없습니다.";
            return false;
        }

        if (string.Equals(
                effectRef,
                "content.postCollabHealOwnParticipant",
                StringComparison.OrdinalIgnoreCase) &&
            FindSurvivingOwnCollabParticipantSlot(candidate.owner, safeContext) == null)
        {
            failReason = "합방에 참여한 내 캐릭터가 생존해 있지 않아 누룽지를 사용할 수 없습니다.";
            return false;
        }

        if (string.Equals(
                effectRef,
                "content.redrawIfBehindAndUniverseOnly",
                StringComparison.OrdinalIgnoreCase) &&
            !CanActivateRedrawIfBehindAndUniverseOnly(candidate, out failReason))
        {
            return false;
        }

        if (string.Equals(
                effectRef,
                "content.collabClicheSpendBuffRefund",
                StringComparison.OrdinalIgnoreCase) &&
            !CanActivateCollabClicheSpendBuffRefund(candidate, safeContext, out failReason))
        {
            return false;
        }

        if (string.Equals(
                effectRef,
                "content.forceOpponentFlipOrSack",
                StringComparison.OrdinalIgnoreCase) &&
            BuildOpponentFaceDownCharacterSlotCandidates(candidate.owner).Count == 0)
        {
            failReason = "상대 필드에 대상 가능한 뒷면 캐릭터가 없습니다.";
            return false;
        }

        if (string.Equals(
                effectRef,
                "content.forceOpponentSummonOrSackFromHand",
                StringComparison.OrdinalIgnoreCase) &&
            GetHandCardCount(GetOpponentOwner(candidate.owner)) <= 0)
        {
            failReason = "상대 손패에 선택할 카드가 없습니다.";
            return false;
        }

        if (string.Equals(
                effectRef,
                "content.postCollabTabiBoostAndRebattle",
                StringComparison.OrdinalIgnoreCase) &&
            !CanActivatePostCollabTabiBoostAndRebattle(candidate, safeContext, out failReason))
        {
            return false;
        }

        if (requestedTiming == EffectTiming.Content)
        {
            if (battleManager.IsBattleInputLockedFromExternal())
            {
                failReason = "합방 중에는 이 카드를 사용할 수 없습니다.";
                return false;
            }

            string actionFailReason;
            if (!battleManager.CanUseMyActionFromExternal(out actionFailReason))
            {
                failReason = actionFailReason;
                return false;
            }

            if (candidate.owner != BattleSlotOwner.My)
            {
                failReason = "현재는 내 손패의 콘텐츠 카드만 발동할 수 있습니다.";
                return false;
            }
        }

        int cost = GetActivationCost(candidate.card);
        if (cost > 0 && !battleManager.CanPayViewerCostFromExternal(candidate.owner, cost))
        {
            failReason = "시청자가 부족합니다.";
            return false;
        }

        if (ShouldSkipEffectCandidateDueToCollabSilence(candidate, safeContext, requestedTiming, out failReason))
            return false;

        return true;
    }

    private bool ShouldSkipEffectCandidateDueToCollabSilence(
        EffectCandidate candidate,
        EffectContext context,
        EffectTiming requestedTiming,
        out string failReason)
    {
        failReason = "";

        if (battleManager == null ||
            candidate == null ||
            candidate.sourceZone != EffectSourceZone.Field ||
            !IsCollabRelatedTiming(requestedTiming))
        {
            return false;
        }

        BattleFieldSlot sourceSlot = candidate.sourceSlot != null
            ? candidate.sourceSlot
            : context != null
            ? context.sourceSlot
            : null;

        if (!battleManager.IsCharacterCollabEffectSilencedFromExternal(sourceSlot))
            return false;

        failReason = "이 캐릭터의 효과는 무효화되어 있습니다.";
        Debug.Log(failReason);
        return true;
    }

    private bool IsCollabRelatedTiming(EffectTiming timing)
    {
        return timing == EffectTiming.PreCollab ||
            timing == EffectTiming.PostCollab;
    }

    private bool TryResolveEffect(
        EffectCandidate candidate,
        EffectContext context,
        out string failReason)
    {
        failReason = "";

        if (!CanActivateEffect(candidate, context, out failReason))
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);
        int cost = GetActivationCost(candidate.card);

        if (effectRef == "content.silenceCharacterCollabThisTurn")
        {
            return battleManager.TryStartSilenceCharacterCollabThisTurnFromExternal(
                candidate.card,
                candidate.owner,
                cost,
                ShouldConsumeAction(candidate, context),
                candidate.handIndex
            );
        }

        if (IsLockBroadcastIdNoMoveNoKOUntilNextEndEffect(candidate, context))
        {
            ResolveLockBroadcastIdNoMoveNoKOUntilNextEndEffect(candidate, context, null);
            return true;
        }

        if (cost > 0 &&
            !battleManager.TryPayViewerCostFromExternal(candidate.owner, cost))
        {
            failReason = "시청자가 부족하여 효과를 발동할 수 없습니다.";
            return false;
        }

        if (candidate.sourceZone == EffectSourceZone.Hand &&
            !MoveSourceHandCardToRestZone(candidate, context).success)
        {
            failReason = "효과 발동 카드를 손패에서 휴식존으로 이동할 수 없습니다.";
            return false;
        }

        battleManager.RefreshAllUIFromExternal();

        ExecuteEffectByRefInternal(
            candidate,
            context,
            ShouldConsumeAction(candidate, context)
        );

        return true;
    }

    private ZoneMoveResult MoveSourceHandCardToRestZone(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return ZoneMoveResult.Fail(null, "효과 후보 정보가 없습니다.");

        return MoveCardBetweenZones(
            new ZoneMoveRequest
            {
                owner = candidate.owner,
                fromZone = EffectZone.Hand,
                toZone = EffectZone.Rest,
                card = candidate.card,
                handIndex = candidate.handIndex,
                reason = ZoneMoveReason.ContentUsed
            },
            context
        );
    }

    private bool IsDrawThenDiscardEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        return effectRef == "content.drawThenDiscard";
    }

    private bool IsMoveOwnCharToEmptyOrBattleIfTaggedEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        return effectRef == "content.moveOwnCharToEmptyOrBattleIfTagged";
    }

    private bool IsLockBroadcastIdNoMoveNoKOUntilNextEndEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        return effectRef == "content.lockBroadcastIdNoMoveNoKOUntilNextEnd";
    }

    private bool IsForbidOpponentAttackUntilNextTurnEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        return effectRef == "content.forbidOpponentAttackUntilNextTurn";
    }

    private bool IsPeekTopSelectToHandEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        return effectRef == "content.peekTopAndTakeTaggedCharacterOrBottom" ||
            effectRef == "character.active.peekTopAndTakeTaggedContents";
    }

    private bool IsSearchDeckSelectToHandEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        return effectRef == "character.fetchCardsToHandByTags";
    }

    private bool IsDiscardOneThenFetchContentByTagFromDeckEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        return effectRef == "character.active.discardOneThenFetchContentByTagFromDeck";
    }

    private bool IsModifyTaggedOnBoardEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        return effectRef == "character.active.modifyTaggedOnBoard";
    }

    private bool IsForceBattleTargetAnywhereEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        return effectRef == "character.active.forceBattleTargetAnywhere";
    }

    private bool IsIdolFullHealOneControlledEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        return effectRef == "idol.active.fullHealOneControlled";
    }

    private bool IsCallFromRestByTagThenDonateViewersEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        return effectRef == "idol.active.callFromRestByTagThenDonateViewers";
    }

    private bool IsFetchTabiOrRestBoongAndFetchBothEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        return effectRef == "idol.active.fetchTabiOrRestBoongAndFetchBoth";
    }

    private bool IsCallFromRestByTagToEmptyPlatformsEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        return effectRef == "character.onAppear.callFromRestByTagToEmptyPlatforms";
    }

    private bool IsReturnUpToNFromRestToDeckEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        return effectRef == "content.returnUpToNFromRestToDeck";
    }

    private bool IsCollabClicheSpendBuffRefundEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        return IsEffectRef(candidate, context, "content.collabClicheSpendBuffRefund");
    }

    private bool IsForceOpponentFlipOrSackEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        return IsEffectRef(candidate, context, "content.forceOpponentFlipOrSack");
    }

    private bool IsForceOpponentSummonOrSackFromHandEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        return IsEffectRef(candidate, context, "content.forceOpponentSummonOrSackFromHand");
    }

    private bool IsPostCollabTabiBoostAndRebattleEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        return IsEffectRef(candidate, context, "content.postCollabTabiBoostAndRebattle");
    }

    private bool IsEffectRef(
        EffectCandidate candidate,
        EffectContext context,
        string expectedRef)
    {
        if (candidate == null || string.IsNullOrEmpty(expectedRef))
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        return string.Equals(effectRef, expectedRef, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsModifyCharacterStatsEffect(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null)
            return false;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, context != null ? context.timing : candidate.timing);

        switch (effectRef)
        {
            case "content.postCollabHealOwnParticipant":
            case "character.active.modifyTaggedOnBoard":
            case "character.active.adjacentHpDownAndTensionUpForTag":
            case "character.onAppear.adjacentOppCollabTensionDeltaThisTurn":
            case "character.active.adjacentOppCollabTensionDeltaThisTurn":
                return true;

            default:
                return false;
        }
    }

    private BattleFieldSlot FindSurvivingOpponentCollabParticipantSlot(
        BattleSlotOwner effectOwner,
        EffectContext context)
    {
        if (context == null)
            return null;

        if (context.attackerSlot != null &&
            context.attackerSlot.HasCharacter &&
            context.attackerSlot.characterCard != null &&
            context.attackerSlot.characterOwner != effectOwner)
        {
            return context.attackerSlot;
        }

        if (context.defenderSlot != null &&
            context.defenderSlot.HasCharacter &&
            context.defenderSlot.characterCard != null &&
            context.defenderSlot.characterOwner != effectOwner)
        {
            return context.defenderSlot;
        }

        return null;
    }

    private void ResolveForbidOpponentAttackUntilNextTurnEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action<bool> onComplete)
    {
        if (!CanActivateEffect(candidate, context, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke(false);
            return;
        }

        EffectContext safeContext = NormalizeContext(context, EffectTiming.PostCollab);
        BattleFieldSlot targetSlot = FindSurvivingOpponentCollabParticipantSlot(candidate.owner, safeContext);
        if (targetSlot == null)
        {
            battleManager?.SetSystemMessageFromExternal("합방 후 생존한 상대 캐릭터가 없어 발동할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (!ConsumeSourceContentForEffect(candidate, safeContext, out string consumeMessage))
        {
            battleManager?.SetSystemMessageFromExternal(consumeMessage);
            onComplete?.Invoke(false);
            return;
        }

        battleManager.ApplyCollabAttackForbiddenUntilNextTurnFromExternal(targetSlot);

        string targetName = targetSlot.characterCard != null
            ? targetSlot.characterCard.name
            : "대상 캐릭터";
        string message = $"{candidate.card.name} 발동: {targetName}은(는) 다음 턴까지 합방을 시작할 수 없습니다.";
        int cost = GetActivationCost(candidate.card);
        if (cost > 0)
            message += $"\n시청자 -{cost}";

        CompleteEffectResolution(message, ShouldConsumeAction(candidate, safeContext), () => onComplete?.Invoke(true));
    }

    private void ResolveMoveOwnCharToEmptyOrBattleIfTaggedEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action<bool> onComplete)
    {
        if (!CanActivateEffect(candidate, context, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke(false);
            return;
        }

        EffectContext safeContext = NormalizeContext(context, candidate.timing);
        if (safeContext.sourceEffect == null)
            safeContext.sourceEffect = candidate.sourceEffect;

        EffectData effect = safeContext.sourceEffect ?? candidate.sourceEffect;
        string tag = GetStringParam(effect, "tag", "");
        List<BattleFieldSlot> characterSlots = BuildOwnFaceUpTaggedFieldCharacterSlotCandidates(candidate.owner, tag);

        if (characterSlots.Count == 0)
        {
            battleManager?.SetSystemMessageFromExternal($"이동시킬 {tag} 캐릭터가 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        CardQuestionPanel panel = battleManager != null
            ? battleManager.BattleCardQuestionPanel
            : null;

        if (panel == null)
        {
            battleManager?.SetSystemMessageFromExternal("CardQuestionPanel이 없어 이동할 캐릭터를 선택할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (panel.IsOpen())
        {
            battleManager?.SetSystemMessageFromExternal("이미 카드 선택창이 열려 있어 이동할 캐릭터를 선택할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        bool opened = panel.TryShowOptions(
            $"이동시킬 {tag} 캐릭터를 선택하세요.",
            BuildSlotOptionsFromSlots(characterSlots),
            true,
            selectedOption =>
            {
                BattleFieldSlot selectedSlot = selectedOption != null
                    ? selectedOption.linkedSlot
                    : null;

                RequestMoveTaggedCharacterTargetSlot(candidate, safeContext, selectedSlot, onComplete);
            },
            () =>
            {
                battleManager?.SetSystemMessageFromExternal($"{candidate.card.name} 효과를 취소했습니다.");
                onComplete?.Invoke(false);
            }
        );

        if (!opened)
        {
            battleManager?.SetSystemMessageFromExternal("카드 선택창을 열 수 없어 이동할 캐릭터를 선택할 수 없습니다.");
            onComplete?.Invoke(false);
        }
    }

    private void RequestMoveTaggedCharacterTargetSlot(
        EffectCandidate candidate,
        EffectContext context,
        BattleFieldSlot characterSlot,
        Action<bool> onComplete)
    {
        if (!IsValidEffectMoveSourceSlot(candidate.owner, characterSlot))
        {
            battleManager?.SetSystemMessageFromExternal("선택한 캐릭터를 이동시킬 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        MovementManager movementManager = battleManager != null ? battleManager.movementManager : null;
        if (movementManager == null)
        {
            battleManager?.SetSystemMessageFromExternal("MovementManager가 연결되어 있지 않습니다.");
            onComplete?.Invoke(false);
            return;
        }

        List<BattleFieldSlot> targetSlots = movementManager.BuildMoveCandidatesForEffect(characterSlot);
        if (targetSlots.Count == 0)
        {
            battleManager?.SetSystemMessageFromExternal("이동 가능한 위치가 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        bool opened = battleManager.RequestFieldSlotSelection(
            "이동할 위치를 골라주세요.",
            targetSlots,
            selectedSlot => ResolveMoveTaggedCharacterToSelectedSlot(
                candidate,
                context,
                characterSlot,
                selectedSlot,
                onComplete),
            () =>
            {
                battleManager?.SetSystemMessageFromExternal($"{candidate.card.name} 효과를 취소했습니다.");
                onComplete?.Invoke(false);
            }
        );

        if (!opened)
        {
            battleManager?.SetSystemMessageFromExternal("슬롯 선택을 시작할 수 없어 이동할 위치를 선택할 수 없습니다.");
            onComplete?.Invoke(false);
        }
    }

    private void ResolveMoveTaggedCharacterToSelectedSlot(
        EffectCandidate candidate,
        EffectContext context,
        BattleFieldSlot characterSlot,
        BattleFieldSlot selectedSlot,
        Action<bool> onComplete)
    {
        if (!IsValidEffectMoveSourceSlot(candidate.owner, characterSlot))
        {
            battleManager?.SetSystemMessageFromExternal("선택한 캐릭터를 이동시킬 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        MovementManager movementManager = battleManager != null ? battleManager.movementManager : null;
        if (movementManager == null)
        {
            battleManager?.SetSystemMessageFromExternal("MovementManager가 연결되어 있지 않습니다.");
            onComplete?.Invoke(false);
            return;
        }

        bool isBattleTarget =
            selectedSlot != null &&
            selectedSlot.HasCharacter &&
            selectedSlot.characterOwner != candidate.owner;

        if (!ConsumeSourceContentForEffect(candidate, context, out string consumeMessage))
        {
            battleManager?.SetSystemMessageFromExternal(consumeMessage);
            onComplete?.Invoke(false);
            return;
        }

        string resultMessage;
        bool success = isBattleTarget
            ? movementManager.TryStartCollaborationByEffect(characterSlot, selectedSlot, out resultMessage)
            : movementManager.TryMoveCharacterByEffect(characterSlot, selectedSlot, out resultMessage);

        if (!success)
        {
            battleManager?.SetSystemMessageFromExternal(resultMessage);
            onComplete?.Invoke(false);
            return;
        }

        string message = $"{candidate.card.name} 발동: {resultMessage}";
        int cost = GetActivationCost(candidate.card);
        if (cost > 0)
            message += $"\n시청자 -{cost}";

        CompleteEffectResolution(message, ShouldConsumeAction(candidate, context), () => onComplete?.Invoke(true));
    }

    private void ResolveCollabClicheSpendBuffRefundEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action<bool> onComplete)
    {
        EffectContext safeContext = NormalizeContext(context, EffectTiming.PreCollab);

        if (!CanActivateEffect(candidate, safeContext, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke(false);
            return;
        }

        BattleFieldSlot participantSlot = FindOwnedCollabParticipantSlot(candidate.owner, safeContext);
        BaseCardData participantCard = participantSlot != null ? participantSlot.characterCard : null;

        if (!ConsumeSourceContentForEffect(candidate, safeContext, out string consumeMessage))
        {
            battleManager?.SetSystemMessageFromExternal(consumeMessage);
            onComplete?.Invoke(false);
            return;
        }

        int currentViewers = battleManager.GetViewersFromExternal(candidate.owner);
        if (currentViewers > 0)
            battleManager.ModifyViewersFromExternal(candidate.owner, -currentViewers);

        pendingOurTales = new PendingOurTalesState
        {
            owner = candidate.owner,
            sourceCard = candidate.card,
            attackerSlot = safeContext.attackerSlot,
            defenderSlot = safeContext.defenderSlot,
            participantSlot = participantSlot,
            participantCard = participantCard,
            processed = false
        };

        battleManager.RefreshAllUIFromExternal();
        CompleteEffectResolution(
            $"{candidate.card.name} 발동: 모든 시청자를 소모하고 합방을 계속합니다.",
            false,
            () => onComplete?.Invoke(true));
    }

    public IEnumerator ResolvePendingOurTalesAfterCollabRoutineFromExternal(Action<string> onComplete)
    {
        if (pendingOurTales == null || pendingOurTales.processed)
        {
            onComplete?.Invoke("");
            yield break;
        }

        PendingOurTalesState state = pendingOurTales;
        state.processed = true;
        pendingOurTales = null;

        BattleFieldSlot targetSlot = FindMatchingSurvivingOurTalesParticipantSlot(state);
        string sourceName = state.sourceCard != null ? state.sourceCard.name : "Our Tales";

        if (targetSlot == null)
        {
            onComplete?.Invoke($"{sourceName}: 합방을 시행한 캐릭터가 생존하지 않아 보상을 얻지 못했습니다.");
            yield break;
        }

        int hp = Mathf.Max(0, targetSlot.currentCharacterHp);
        int reward = hp * 1000;
        battleManager.ModifyViewersFromExternal(state.owner, reward);

        string message = $"{sourceName}: 생존한 캐릭터의 남은 체력 {hp}만큼 {reward} 시청자를 획득합니다.";
        yield return battleManager.SendFieldCharacterToRestZoneRoutine(targetSlot);

        battleManager.RefreshAllUIFromExternal();
        onComplete?.Invoke(message);
    }

    public bool TryConsumePendingPostCollabRebattleFromExternal(
        out BattleFieldSlot attackerSlot,
        out BattleFieldSlot defenderSlot,
        out string message)
    {
        attackerSlot = null;
        defenderSlot = null;
        message = "";

        if (pendingPostCollabRebattle == null || pendingPostCollabRebattle.processed)
            return false;

        PendingPostCollabRebattleState state = pendingPostCollabRebattle;
        state.processed = true;
        pendingPostCollabRebattle = null;

        if (!IsSurvivingOwnedCollabParticipantSlot(state.attackerSlot, state.owner) ||
            state.defenderSlot == null ||
            !state.defenderSlot.HasCharacter ||
            state.defenderSlot.characterCard == null ||
            state.defenderSlot.currentCharacterHp <= 0)
        {
            message = "여로: 추가 합방 대상이 더 이상 유효하지 않습니다.";
            return false;
        }

        attackerSlot = state.attackerSlot;
        defenderSlot = state.defenderSlot;
        string sourceName = state.sourceCard != null ? state.sourceCard.name : "여로";
        message = $"{sourceName}: 강화된 #타비 캐릭터가 한 번 더 합방합니다.";
        return true;
    }

    private void ResolveForceOpponentFlipOrSackEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action<bool> onComplete)
    {
        if (!CanActivateEffect(candidate, context, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke(false);
            return;
        }

        List<BattleFieldSlot> targetSlots = BuildOpponentFaceDownCharacterSlotCandidates(candidate.owner);
        if (targetSlots.Count == 0)
        {
            battleManager?.SetSystemMessageFromExternal("상대 필드에 대상 가능한 뒷면 캐릭터가 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (candidate.owner != BattleSlotOwner.My)
        {
            ResolveForceOpponentFlipOrSackToSelectedSlot(candidate, context, targetSlots[0], onComplete);
            return;
        }

        bool opened = battleManager.RequestFieldSlotSelection(
            "처리할 상대 뒷면 캐릭터를 선택하세요.",
            targetSlots,
            selectedSlot => ResolveForceOpponentFlipOrSackToSelectedSlot(candidate, context, selectedSlot, onComplete),
            () =>
            {
                battleManager?.SetSystemMessageFromExternal($"{candidate.card.name} 효과를 취소했습니다.");
                onComplete?.Invoke(false);
            });

        if (!opened)
        {
            battleManager?.SetSystemMessageFromExternal("슬롯 선택을 시작할 수 없어 상대 뒷면 캐릭터를 선택할 수 없습니다.");
            onComplete?.Invoke(false);
        }
    }

    private void ResolveForceOpponentFlipOrSackToSelectedSlot(
        EffectCandidate candidate,
        EffectContext context,
        BattleFieldSlot selectedSlot,
        Action<bool> onComplete)
    {
        if (!IsOpponentFaceDownCharacterSlot(candidate.owner, selectedSlot))
        {
            battleManager?.SetSystemMessageFromExternal("선택한 슬롯은 상대 필드의 뒷면 캐릭터가 아닙니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (!ConsumeSourceContentForEffect(candidate, context, out string consumeMessage))
        {
            battleManager?.SetSystemMessageFromExternal(consumeMessage);
            onComplete?.Invoke(false);
            return;
        }

        StartCoroutine(ResolveForceOpponentFlipOrSackRoutine(candidate, context, selectedSlot, onComplete));
    }

    private IEnumerator ResolveForceOpponentFlipOrSackRoutine(
        EffectCandidate candidate,
        EffectContext context,
        BattleFieldSlot selectedSlot,
        Action<bool> onComplete)
    {
        BaseCardData targetCard = selectedSlot != null ? selectedSlot.characterCard : null;
        BattleSlotOwner opponent = GetOpponentOwner(candidate.owner);
        string targetName = targetCard != null ? targetCard.name : "대상 캐릭터";
        int doubleCost = GetDoubleAppearCost(targetCard);

        if (targetCard == null ||
            !battleManager.CanPayViewerCostFromExternal(opponent, doubleCost))
        {
            yield return RestForceOpponentFlipOrSackTargetRoutine(selectedSlot);
            string unableMessage = $"{candidate.card.name} 발동: 상대가 2배 출연 코스트를 지불할 수 없어 해당 캐릭터가 휴식존으로 이동합니다.";
            battleManager.RefreshAllUIFromExternal();
            CompleteEffectResolution(unableMessage, ShouldConsumeAction(candidate, context), () => onComplete?.Invoke(true));
            yield break;
        }

        if (opponent == BattleSlotOwner.My)
        {
            bool choiceResolved = false;
            bool chooseFlip = false;
            QuestionPanel questionPanel = battleManager != null ? battleManager.BattleQuestionPanel : null;

            if (questionPanel != null &&
                questionPanel.TryShowYesNoQuestion(
                    $"{targetName}: 시청자 {doubleCost}을(를) 지불하고 플립하시겠습니까?",
                    () =>
                    {
                        chooseFlip = true;
                        choiceResolved = true;
                    },
                    () =>
                    {
                        chooseFlip = false;
                        choiceResolved = true;
                    },
                    () =>
                    {
                        chooseFlip = false;
                        choiceResolved = true;
                    }))
            {
                while (!choiceResolved)
                    yield return null;
            }
            else
            {
                battleManager?.SetSystemMessageFromExternal("QuestionPanel을 열 수 없어 지불하지 않는 선택으로 처리합니다.");
                chooseFlip = false;
            }

            if (chooseFlip)
            {
                if (!battleManager.CanPayViewerCostFromExternal(opponent, doubleCost))
                {
                    yield return RestForceOpponentFlipOrSackTargetRoutine(selectedSlot);
                    string failedAfterChoiceMessage = $"{candidate.card.name} 발동: 상대가 2배 출연 코스트를 지불할 수 없어 해당 캐릭터가 휴식존으로 이동합니다.";
                    battleManager.RefreshAllUIFromExternal();
                    CompleteEffectResolution(failedAfterChoiceMessage, ShouldConsumeAction(candidate, context), () => onComplete?.Invoke(true));
                    yield break;
                }

                string flipMessage = FlipForceOpponentFlipOrSackTarget(candidate, selectedSlot, opponent, doubleCost);
                battleManager.RefreshAllUIFromExternal();
                CompleteEffectResolution(flipMessage, ShouldConsumeAction(candidate, context), () => onComplete?.Invoke(true));
                yield break;
            }

            yield return RestForceOpponentFlipOrSackTargetRoutine(selectedSlot);
            string restMessage = $"{candidate.card.name} 발동: 상대가 지불하지 않아 캐릭터가 휴식존으로 이동합니다.";
            battleManager.RefreshAllUIFromExternal();
            CompleteEffectResolution(restMessage, ShouldConsumeAction(candidate, context), () => onComplete?.Invoke(true));
            yield break;
        }

        // TODO: TestEnemy/AI 선택 정책을 고도화한다. 1차 구현은 지불 가능하면 플립을 선택한다.
        if (!battleManager.CanPayViewerCostFromExternal(opponent, doubleCost))
        {
            yield return RestForceOpponentFlipOrSackTargetRoutine(selectedSlot);
            string failedBeforeAutoMessage = $"{candidate.card.name} 발동: 상대가 2배 출연 코스트를 지불할 수 없어 해당 캐릭터가 휴식존으로 이동합니다.";
            battleManager.RefreshAllUIFromExternal();
            CompleteEffectResolution(failedBeforeAutoMessage, ShouldConsumeAction(candidate, context), () => onComplete?.Invoke(true));
            yield break;
        }

        string autoFlipMessage = FlipForceOpponentFlipOrSackTarget(candidate, selectedSlot, opponent, doubleCost);
        battleManager.RefreshAllUIFromExternal();
        CompleteEffectResolution(autoFlipMessage, ShouldConsumeAction(candidate, context), () => onComplete?.Invoke(true));
    }

    private string FlipForceOpponentFlipOrSackTarget(
        EffectCandidate candidate,
        BattleFieldSlot selectedSlot,
        BattleSlotOwner opponent,
        int requiredCost)
    {
        BaseCardData targetCard = selectedSlot != null ? selectedSlot.characterCard : null;

        if (targetCard == null ||
            !battleManager.TryPayViewerCostFromExternal(opponent, requiredCost))
        {
            return $"{candidate.card.name} 발동: 상대가 2배 출연 코스트를 지불할 수 없어 해당 캐릭터가 휴식존으로 이동합니다.";
        }

        Sprite sprite = battleManager.LoadCardSpriteFromExternal(targetCard);
        selectedSlot.SetCharacterCard(targetCard, sprite, false, opponent);
        selectedSlot.faceUpSummonedTurn = battleManager.GetCurrentTurnCountFromExternal();
        selectedSlot.SetCharacterMovedThisTurn(false);
        return $"{candidate.card.name} 발동: 상대가 2배 출연 코스트를 지불하고 캐릭터를 플립했습니다.";
    }

    private IEnumerator RestForceOpponentFlipOrSackTargetRoutine(BattleFieldSlot selectedSlot)
    {
        if (selectedSlot == null ||
            !selectedSlot.HasCharacter ||
            selectedSlot.characterCard == null)
        {
            yield break;
        }

        yield return battleManager.SendFieldCharacterToRestZoneRoutine(selectedSlot);
    }

    private void ResolveForceOpponentSummonOrSackFromHandEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action<bool> onComplete)
    {
        if (!CanActivateEffect(candidate, context, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke(false);
            return;
        }

        BattleSlotOwner opponent = GetOpponentOwner(candidate.owner);
        IReadOnlyList<BaseCardData> opponentHand = battleManager.GetHandCardsFromExternal(opponent);
        if (opponentHand == null || opponentHand.Count == 0)
        {
            battleManager?.SetSystemMessageFromExternal("상대 손패에 선택할 카드가 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        int randomHandIndex = UnityEngine.Random.Range(0, opponentHand.Count);
        BaseCardData selectedCard = opponentHand[randomHandIndex];

        if (selectedCard == null ||
            !battleManager.IsCardInHandAtIndexFromExternal(opponent, randomHandIndex, selectedCard))
        {
            battleManager?.SetSystemMessageFromExternal("랜덤으로 선택한 상대 손패를 처리할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (!ConsumeSourceContentForEffect(candidate, context, out string consumeMessage))
        {
            battleManager?.SetSystemMessageFromExternal(consumeMessage);
            onComplete?.Invoke(false);
            return;
        }

        string revealMessage = $"{candidate.card.name}: 상대 손패에서 {selectedCard.name}이 공개되었습니다.";
        battleManager.SetSystemMessageFromExternal(revealMessage);

        if (!(selectedCard is CharacterCardData))
        {
            battleManager.RefreshAllUIFromExternal();
            CompleteEffectResolution(
                $"{revealMessage}\n공개된 카드는 캐릭터가 아니므로 아무 일도 일어나지 않습니다.",
                ShouldConsumeAction(candidate, context),
                () => onComplete?.Invoke(true));
            return;
        }

        StartCoroutine(ResolveForceOpponentSummonOrSackCharacterRoutine(
            candidate,
            context,
            opponent,
            selectedCard,
            randomHandIndex,
            revealMessage,
            onComplete));
    }

    private IEnumerator ResolveForceOpponentSummonOrSackCharacterRoutine(
        EffectCandidate candidate,
        EffectContext context,
        BattleSlotOwner opponent,
        BaseCardData selectedCard,
        int handIndex,
        string revealMessage,
        Action<bool> onComplete)
    {
        string selectedName = selectedCard != null ? selectedCard.name : "선택 카드";
        int requiredCost = GetDoubleAppearCost(selectedCard);
        List<BattleFieldSlot> emptySlots = BuildEmptyOwnedBroadcastSlotCandidates(opponent);

        if (!battleManager.CanPayViewerCostFromExternal(opponent, requiredCost))
        {
            MoveOpponentHandCardToRest(opponent, handIndex, selectedCard);
            battleManager.RefreshAllUIFromExternal();
            CompleteEffectResolution(
                $"{revealMessage}\n상대가 2배 출연 코스트를 지불할 수 없어 {selectedName}이 휴식존으로 이동합니다.",
                ShouldConsumeAction(candidate, context),
                () => onComplete?.Invoke(true));
            yield break;
        }

        if (emptySlots.Count == 0)
        {
            MoveOpponentHandCardToRest(opponent, handIndex, selectedCard);
            battleManager.RefreshAllUIFromExternal();
            CompleteEffectResolution(
                $"{revealMessage}\n상대 필드에 빈 방송 슬롯이 없어 {selectedName}이 휴식존으로 이동합니다.",
                ShouldConsumeAction(candidate, context),
                () => onComplete?.Invoke(true));
            yield break;
        }

        BattleFieldSlot selectedSlot = null;

        if (opponent == BattleSlotOwner.My)
        {
            bool slotSelected = false;
            bool opened = battleManager.RequestFieldSlotSelection(
                $"{selectedName}을(를) 출연시킬 내 빈 방송 슬롯을 선택하세요.",
                emptySlots,
                slot =>
                {
                    selectedSlot = slot;
                    slotSelected = true;
                },
                () =>
                {
                    selectedSlot = null;
                    slotSelected = true;
                });

            if (opened)
            {
                while (!slotSelected)
                    yield return null;
            }

            if (!opened || selectedSlot == null)
            {
                MoveOpponentHandCardToRest(opponent, handIndex, selectedCard);
                battleManager.RefreshAllUIFromExternal();
                CompleteEffectResolution(
                    $"{revealMessage}\n출연 위치를 선택하지 않아 {selectedName}이 휴식존으로 이동합니다.",
                    ShouldConsumeAction(candidate, context),
                    () => onComplete?.Invoke(true));
                yield break;
            }
        }
        else
        {
            selectedSlot = emptySlots[0];
        }

        string placeMessage = PlaceOpponentHandCharacterByKumorin(opponent, handIndex, selectedCard, selectedSlot, requiredCost);
        battleManager.RefreshAllUIFromExternal();
        CompleteEffectResolution(
            $"{revealMessage}\n{placeMessage}",
            ShouldConsumeAction(candidate, context),
            () => onComplete?.Invoke(true));
    }

    private void ResolvePostCollabTabiBoostAndRebattleEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action<bool> onComplete)
    {
        EffectContext safeContext = NormalizeContext(context, EffectTiming.PostCollab);

        if (!CanActivateEffect(candidate, safeContext, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke(false);
            return;
        }

        EffectData effect = safeContext.sourceEffect ?? candidate.sourceEffect;
        string tabiTag = GetStringParam(effect, "tabiTag", "#타비");
        string bunnyTag = GetStringParam(effect, "bunnyTag", "#뿡댕이");
        BattleFieldSlot tabiSlot = FindSurvivingOwnTaggedCollabParticipantSlot(candidate.owner, safeContext, tabiTag);
        BattleFieldSlot opponentSlot = FindSurvivingOpponentCollabParticipantSlot(candidate.owner, safeContext);

        if (tabiSlot == null || opponentSlot == null)
        {
            battleManager?.SetSystemMessageFromExternal("추가 합방을 진행할 생존 캐릭터가 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        int bonusTension;
        int bonusHp;
        int bunnyCount = SumOwnedFaceUpTaggedCharactersOnBoard(
            candidate.owner,
            bunnyTag,
            out bonusTension,
            out bonusHp);

        if (bunnyCount <= 0 || bonusTension + bonusHp <= 0)
        {
            battleManager?.SetSystemMessageFromExternal("여로를 사용할 수 없습니다: 내 필드 위 앞면 #뿡댕이가 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (!ConsumeSourceContentForEffect(candidate, safeContext, out string consumeMessage))
        {
            battleManager?.SetSystemMessageFromExternal(consumeMessage);
            onComplete?.Invoke(false);
            return;
        }

        tabiSlot.SetCharacterBattleStats(
            tabiSlot.currentCharacterHp + bonusHp,
            tabiSlot.currentCharacterMaxHp + bonusHp,
            tabiSlot.currentCharacterTension + bonusTension);

        pendingPostCollabRebattle = new PendingPostCollabRebattleState
        {
            owner = candidate.owner,
            sourceCard = candidate.card,
            attackerSlot = tabiSlot,
            defenderSlot = opponentSlot,
            processed = false
        };

        battleManager.RefreshAllUIFromExternal();
        battleManager.RefreshFieldCharacterDetailFromExternal(tabiSlot);

        string tabiName = tabiSlot.characterCard != null ? tabiSlot.characterCard.name : "#타비";
        string message =
            $"{candidate.card.name} 발동: {bunnyTag}들의 텐션 합계 {bonusTension}, 체력 합계 {bonusHp}을 {tabiTag}에게 더하고 다시 합방합니다.\n" +
            $"{candidate.card.name}: {tabiName}의 합방 텐션 +{bonusTension}, 체력 +{bonusHp}";
        CompleteEffectResolution(message, false, () => onComplete?.Invoke(true));
    }

    private bool ConsumeSourceContentForEffect(
        EffectCandidate candidate,
        EffectContext context,
        out string message)
    {
        message = "";

        if (candidate.sourceZone == EffectSourceZone.Hand &&
            !battleManager.IsCardInHandAtIndexFromExternal(candidate.owner, candidate.handIndex, candidate.card))
        {
            message = "손패에 있는 콘텐츠 카드만 발동할 수 있습니다.";
            return false;
        }

        int cost = GetActivationCost(candidate != null ? candidate.card : null);
        if (cost > 0 && !battleManager.TryPayViewerCostFromExternal(candidate.owner, cost))
        {
            message = "시청자가 부족하여 효과를 발동할 수 없습니다.";
            return false;
        }

        if (candidate.sourceZone == EffectSourceZone.Hand)
        {
            ZoneMoveResult moveResult = MoveSourceHandCardToRestZone(candidate, context);
            if (!moveResult.success)
            {
                message = "효과 발동 카드를 손패에서 휴식존으로 이동할 수 없습니다.";
                if (!string.IsNullOrWhiteSpace(moveResult.message))
                    message += $"\n{moveResult.message}";
                return false;
            }
        }

        battleManager.RefreshAllUIFromExternal();
        return true;
    }

    private List<BattleFieldSlot> BuildOwnFaceUpTaggedFieldCharacterSlotCandidates(
        BattleSlotOwner owner,
        string tag)
    {
        List<BattleFieldSlot> candidates = new List<BattleFieldSlot>();

        AddOwnFaceUpTaggedFieldCharacterSlotCandidates(owner, tag, BattlePlayerSide.My, candidates);
        AddOwnFaceUpTaggedFieldCharacterSlotCandidates(owner, tag, BattlePlayerSide.Enemy, candidates);

        return candidates;
    }

    private void AddOwnFaceUpTaggedFieldCharacterSlotCandidates(
        BattleSlotOwner owner,
        string tag,
        BattlePlayerSide side,
        List<BattleFieldSlot> candidates)
    {
        if (battleManager == null || candidates == null)
            return;

        IReadOnlyList<BattleFieldSlot> slots = battleManager.GetSlotsForMovement(side);
        if (slots == null)
            return;

        foreach (BattleFieldSlot slot in slots)
        {
            if (!IsValidEffectMoveSourceSlot(owner, slot))
                continue;

            if (!CardHasHashtag(slot.characterCard, tag))
                continue;

            candidates.Add(slot);
        }
    }

    private bool IsValidEffectMoveSourceSlot(BattleSlotOwner owner, BattleFieldSlot slot)
    {
        return slot != null &&
            slot.HasCharacter &&
            slot.characterCard != null &&
            !slot.isCharacterFaceDown &&
            slot.characterOwner == owner;
    }

    private void ResolveLockBroadcastIdNoMoveNoKOUntilNextEndEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action<bool> onComplete)
    {
        EffectContext safeContext = NormalizeContext(context, candidate != null ? candidate.timing : EffectTiming.Content);
        EffectData effect = candidate != null
            ? candidate.sourceEffect ?? GetPrimaryEffectData(candidate.card, safeContext.timing)
            : null;
        string requireTag = GetStringParam(effect, "requireTag", "#타비");

        if (!HasFaceUpOwnedCharacterWithTag(candidate != null ? candidate.owner : BattleSlotOwner.My, requireTag))
        {
            battleManager?.SetSystemMessageFromExternal($"발동 조건을 만족하는 {requireTag} 캐릭터가 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        List<BattleFieldSlot> broadcastSlots = BuildBroadcastSlotCandidates();
        if (broadcastSlots.Count == 0)
        {
            battleManager?.SetSystemMessageFromExternal("잠금 효과를 적용할 방송 슬롯이 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        bool opened = battleManager.RequestFieldSlotSelection(
            "잠금 효과를 적용할 방송 슬롯을 골라주세요.",
            broadcastSlots,
            selectedSlot => ResolveLockBroadcastIdNoMoveNoKOUntilNextEndTarget(
                candidate,
                safeContext,
                selectedSlot,
                onComplete),
            () =>
            {
                battleManager?.SetSystemMessageFromExternal($"{candidate.card.name} 효과를 취소했습니다.");
                onComplete?.Invoke(false);
            }
        );

        if (!opened)
        {
            battleManager?.SetSystemMessageFromExternal("슬롯 선택을 시작할 수 없어 방송 슬롯을 선택할 수 없습니다.");
            onComplete?.Invoke(false);
        }
    }

    private void ResolveLockBroadcastIdNoMoveNoKOUntilNextEndTarget(
        EffectCandidate candidate,
        EffectContext context,
        BattleFieldSlot selectedSlot,
        Action<bool> onComplete)
    {
        if (selectedSlot == null || !selectedSlot.HasBroadcast)
        {
            battleManager?.SetSystemMessageFromExternal("방송 카드가 설치된 슬롯만 선택할 수 있습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (!ConsumeSourceContentForEffect(candidate, context, out string consumeMessage))
        {
            battleManager?.SetSystemMessageFromExternal(consumeMessage);
            onComplete?.Invoke(false);
            return;
        }

        int untilTurn = battleManager.CalculateNextOpponentTurnEndLockUntilTurnFromExternal();
        battleManager.ApplyBroadcastMoveAndKoLockFromExternal(selectedSlot, untilTurn);

        string message =
            $"{candidate.card.name} 발동: 선택한 방송 슬롯은 다음 상대 턴 종료까지 이동 및 합방 KO가 제한됩니다.\n" +
            $"대상: {(selectedSlot.owner == BattleSlotOwner.My ? "내 필드" : "상대 필드")} ({selectedSlot.x}, {selectedSlot.y})";

        int cost = GetActivationCost(candidate.card);
        if (cost > 0)
            message += $"\n시청자 -{cost}";

        CompleteEffectResolution(message, ShouldConsumeAction(candidate, context), () => onComplete?.Invoke(true));
    }

    private bool HasFaceUpOwnedCharacterWithTag(BattleSlotOwner owner, string tag)
    {
        return BuildOwnFaceUpTaggedFieldCharacterSlotCandidates(owner, tag).Count > 0;
    }

    private List<BattleFieldSlot> BuildBroadcastSlotCandidates()
    {
        List<BattleFieldSlot> candidates = new List<BattleFieldSlot>();
        AddBroadcastSlotCandidates(BattlePlayerSide.My, candidates);
        AddBroadcastSlotCandidates(BattlePlayerSide.Enemy, candidates);
        return candidates;
    }

    private void AddBroadcastSlotCandidates(
        BattlePlayerSide side,
        List<BattleFieldSlot> candidates)
    {
        if (battleManager == null || candidates == null)
            return;

        IReadOnlyList<BattleFieldSlot> slots = battleManager.GetSlotsForMovement(side);
        if (slots == null)
            return;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot != null && slot.HasBroadcast && !candidates.Contains(slot))
                candidates.Add(slot);
        }
    }

    private void ResolvePeekTopSelectToHandEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action onComplete)
    {
        if (!CanActivateEffect(candidate, context, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke();
            return;
        }

        int cost = GetActivationCost(candidate.card);

        if (cost > 0 &&
            !battleManager.TryPayViewerCostFromExternal(candidate.owner, cost))
        {
            battleManager?.SetSystemMessageFromExternal("시청자가 부족하여 효과를 발동할 수 없습니다.");
            onComplete?.Invoke();
            return;
        }

        if (candidate.sourceZone == EffectSourceZone.Hand &&
            !MoveSourceHandCardToRestZone(candidate, context).success)
        {
            battleManager?.SetSystemMessageFromExternal("효과 발동 카드를 손패에서 휴식존으로 이동할 수 없습니다.");
            onComplete?.Invoke();
            return;
        }

        EffectContext safeContext = NormalizeContext(context, candidate.timing);
        if (safeContext.sourceEffect == null)
            safeContext.sourceEffect = candidate.sourceEffect;

        PeekTopSelectRequest request = BuildPeekTopSelectRequest(candidate, safeContext);
        bool consumeAction = ShouldConsumeAction(candidate, context);

        PeekTopSelectToHand(
            request,
            safeContext,
            result =>
            {
                string message = $"{candidate.card.name} 발동: {result?.message ?? "덱 공개 처리를 완료했습니다."}";

                if (cost > 0)
                    message += $"\n시청자 -{cost}";

                CompleteEffectResolution(message, consumeAction, onComplete);
            }
        );
    }

    private void ResolveSearchDeckSelectToHandEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action onComplete)
    {
        if (!CanActivateEffect(candidate, context, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke();
            return;
        }

        int cost = GetActivationCost(candidate.card);

        if (cost > 0 &&
            !battleManager.TryPayViewerCostFromExternal(candidate.owner, cost))
        {
            battleManager?.SetSystemMessageFromExternal("시청자가 부족하여 효과를 발동할 수 없습니다.");
            onComplete?.Invoke();
            return;
        }

        if (candidate.sourceZone == EffectSourceZone.Hand &&
            !MoveSourceHandCardToRestZone(candidate, context).success)
        {
            battleManager?.SetSystemMessageFromExternal("효과 발동 카드를 손패에서 휴식존으로 이동할 수 없습니다.");
            onComplete?.Invoke();
            return;
        }

        EffectContext safeContext = NormalizeContext(context, candidate.timing);
        if (safeContext.sourceEffect == null)
            safeContext.sourceEffect = candidate.sourceEffect;

        SearchDeckSelectRequest request = BuildSearchDeckSelectRequest(candidate, safeContext);
        bool consumeAction = ShouldConsumeAction(candidate, context);

        SearchDeckSelectToHand(
            request,
            safeContext,
            result =>
            {
                string message = $"{candidate.card.name} 발동: {result?.message ?? "덱 서치를 완료했습니다."}";

                if (cost > 0)
                    message += $"\n시청자 -{cost}";

                CompleteEffectResolution(message, consumeAction, onComplete);
            }
        );
    }

    private void ResolveDiscardOneThenFetchContentByTagFromDeckEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action<bool> onComplete)
    {
        if (!CanActivateEffect(candidate, context, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke(false);
            return;
        }

        EffectContext safeContext = NormalizeContext(context, candidate.timing);
        if (safeContext.sourceEffect == null)
            safeContext.sourceEffect = candidate.sourceEffect;

        int discardCount = Mathf.Max(1, GetIntParam(safeContext.sourceEffect, "discardCount", 1));
        int searchCount = Mathf.Max(1, GetIntParam(safeContext.sourceEffect, "searchCount", 1));
        int cost = GetActivationCost(candidate.card);
        bool consumeAction = ShouldConsumeAction(candidate, context);
        string startMessage = $"{candidate.card.name} 발동: 패 {discardCount}장을 버립니다.";

        Debug.Log(
            $"[DiscardThenFetchContentByTagFromDeck] ref={candidate.refId}, " +
            "discard is treated as a prior cost-like step; search may fail after discard.");

        RequestDiscardCardsForActiveFetch(
            candidate.owner,
            discardCount,
            safeContext,
            startMessage,
            (discardSuccess, discardMessage) =>
            {
                if (!discardSuccess)
                {
                    onComplete?.Invoke(false);
                    return;
                }

                if (cost > 0 &&
                    !battleManager.TryPayViewerCostFromExternal(candidate.owner, cost))
                {
                    battleManager?.SetSystemMessageFromExternal(
                        $"{discardMessage}\n시청자가 부족하여 효과를 발동할 수 없습니다.");
                    onComplete?.Invoke(false);
                    return;
                }

                string paidMessage = discardMessage;
                if (cost > 0)
                    paidMessage += $"\n시청자 -{cost}";

                SearchDeckSelectRequest request = BuildDiscardThenFetchSearchRequest(candidate, safeContext, searchCount);

                SearchDeckSelectToHand(
                    request,
                    safeContext,
                    result =>
                    {
                        bool searchSuccess = result != null && result.success;
                        string resultMessage = result != null ? result.message : "덱 서치를 완료하지 못했습니다.";
                        string message = $"{paidMessage}\n{resultMessage}";

                        if (!searchSuccess &&
                            result != null &&
                            result.selectableCards.Count == 0)
                        {
                            message = $"{paidMessage}\n대상 카드가 없습니다.";
                        }

                        foreach (ZoneMoveResult moveResult in result != null
                                     ? result.zoneMoveResults
                                     : new List<ZoneMoveResult>())
                        {
                            Debug.Log(
                                $"[DiscardThenFetchContentByTagFromDeck] ZoneMove " +
                                $"{moveResult.fromZone}->{moveResult.toZone}, success={moveResult.success}, " +
                                $"card={(moveResult.movedCard != null ? moveResult.movedCard.name : "null")}, " +
                                $"message={moveResult.message}");
                        }

                        CompleteEffectResolution(message, consumeAction, null);
                        onComplete?.Invoke(true);
                    }
                );
            }
        );
    }

    private void ResolveForceBattleTargetAnywhereEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action<bool> onComplete)
    {
        if (!CanActivateEffect(candidate, context, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke(false);
            return;
        }

        EffectContext safeContext = NormalizeContext(context, candidate.timing);
        if (safeContext.sourceEffect == null)
            safeContext.sourceEffect = candidate.sourceEffect;

        BattleFieldSlot sourceSlot = candidate.sourceSlot != null
            ? candidate.sourceSlot
            : safeContext.sourceSlot;

        if (sourceSlot == null || !sourceSlot.HasCharacter)
        {
            battleManager?.SetSystemMessageFromExternal("합방을 시작할 캐릭터가 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        TargetSelector selector = BuildForceBattleTargetSelector(candidate, safeContext);
        List<EffectTargetCandidate> candidates = BuildTargetCandidates(selector, safeContext);
        candidates.RemoveAll(target =>
            target == null ||
            target.slot == null ||
            target.slot == sourceSlot ||
            !target.slot.HasCharacter ||
            target.owner == candidate.owner);

        if (candidates.Count == 0)
        {
            battleManager?.SetSystemMessageFromExternal("대상 캐릭터가 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (candidates.Count == 1)
        {
            StartForceBattleTargetAnywhereCollab(candidate, safeContext, sourceSlot, candidates[0], onComplete);
            return;
        }

        CardQuestionPanel panel = battleManager != null
            ? battleManager.BattleCardQuestionPanel
            : null;

        if (panel == null)
        {
            battleManager?.SetSystemMessageFromExternal("CardQuestionPanel이 없어 합방 대상을 선택할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (panel.IsOpen())
        {
            battleManager?.SetSystemMessageFromExternal("이미 카드 선택창이 열려 있어 합방 대상을 선택할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        List<CardQuestionOption> options = BuildTargetOptions(candidates);

        bool opened = panel.TryShowOptions(
            "합방할 상대 캐릭터를 선택하세요.",
            options,
            true,
            selectedOption =>
            {
                EffectTargetCandidate target = FindTargetCandidate(candidates, selectedOption);
                StartForceBattleTargetAnywhereCollab(candidate, safeContext, sourceSlot, target, onComplete);
            },
            () =>
            {
                battleManager?.SetSystemMessageFromExternal("액티브 효과 발동을 취소했습니다.");
                onComplete?.Invoke(false);
            }
        );

        if (!opened)
        {
            battleManager?.SetSystemMessageFromExternal("카드 선택창을 열 수 없어 합방 대상을 선택할 수 없습니다.");
            onComplete?.Invoke(false);
        }
    }

    private TargetSelector BuildForceBattleTargetSelector(
        EffectCandidate candidate,
        EffectContext context)
    {
        EffectData effect = context != null && context.sourceEffect != null
            ? context.sourceEffect
            : candidate.sourceEffect;
        CardFilter filter = BuildCardFilterFromParams(effect, EffectCardKind.Character);

        if (filter.faceState == EffectFaceState.Any &&
            !GetBoolParam(effect, "faceDown", false))
        {
            filter.faceState = EffectFaceState.FaceUpOnly;
        }

        filter.owner = EffectTargetOwner.OpponentOfActingOwner;

        return new TargetSelector
        {
            scope = TargetSelectorScope.OpponentFieldCharacters,
            owner = EffectTargetOwner.ActingOwner,
            filter = filter
        };
    }

    private void StartForceBattleTargetAnywhereCollab(
        EffectCandidate candidate,
        EffectContext context,
        BattleFieldSlot sourceSlot,
        EffectTargetCandidate target,
        Action<bool> onComplete)
    {
        BattleFieldSlot targetSlot = target != null ? target.slot : null;

        if (!CanStartForceBattleTargetAnywhereCollab(sourceSlot, targetSlot, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke(false);
            return;
        }

        int cost = GetActivationCost(candidate.card);
        if (cost > 0 &&
            !battleManager.TryPayViewerCostFromExternal(candidate.owner, cost))
        {
            battleManager?.SetSystemMessageFromExternal("시청자가 부족하여 효과를 발동할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        string sourceName = sourceSlot.characterCard != null
            ? sourceSlot.characterCard.name
            : candidate.card.name;
        string targetName = targetSlot.characterCard != null
            ? targetSlot.characterCard.name
            : "대상 캐릭터";
        string message = $"{targetName}이 {sourceName}의 방송으로 이동해 합방을 시작합니다.";

        if (cost > 0)
            message += $"\n시청자 -{cost}";

        Debug.Log(
            $"[ForceBattleTargetAnywhere] reason={CollaborationStartReason.ForceBattleTargetAnywhere}, " +
            $"forcedAttacker={targetName}, attackerOwner={targetSlot.characterOwner}, " +
            $"attackerOriginalSlot=({targetSlot.owner}, x={targetSlot.x}, y={targetSlot.y}), " +
            $"defender={sourceName}, defenderOwner={sourceSlot.characterOwner}, " +
            $"defenderSlot=({sourceSlot.owner}, x={sourceSlot.x}, y={sourceSlot.y}), " +
            $"battleLocationSlot=({sourceSlot.owner}, x={sourceSlot.x}, y={sourceSlot.y})");

        battleManager.SetSystemMessageFromExternal(message);
        battleManager.collaborationManager.StartForcedIncomingCollaboration(
            targetSlot,
            sourceSlot,
            CollaborationStartReason.ForceBattleTargetAnywhere);
        onComplete?.Invoke(true);
    }

    private bool CanStartForceBattleTargetAnywhereCollab(
        BattleFieldSlot sourceSlot,
        BattleFieldSlot targetSlot,
        out string failReason)
    {
        failReason = "";

        if (battleManager == null)
        {
            failReason = "BattleManager가 연결되어 있지 않습니다.";
            return false;
        }

        if (battleManager.collaborationManager == null)
        {
            failReason = "CollaborationManager가 연결되어 있지 않습니다.";
            return false;
        }

        if (battleManager.collaborationManager.IsCollaborationInteractionActive)
        {
            failReason = "이미 합방 처리를 진행 중입니다.";
            return false;
        }

        if (sourceSlot == null || targetSlot == null)
        {
            failReason = "합방 슬롯 정보가 없습니다.";
            return false;
        }

        if (sourceSlot == targetSlot)
        {
            failReason = "같은 슬롯에서는 합방할 수 없습니다.";
            return false;
        }

        if (!sourceSlot.HasCharacter || !targetSlot.HasCharacter)
        {
            failReason = "합방할 캐릭터 정보가 없습니다.";
            return false;
        }

        if (sourceSlot.characterOwner != BattleSlotOwner.My)
        {
            failReason = "현재는 내 캐릭터만 합방을 시도할 수 있습니다.";
            return false;
        }

        if (sourceSlot.characterOwner == targetSlot.characterOwner)
        {
            failReason = "서로 다른 플레이어의 캐릭터끼리만 합방할 수 있습니다.";
            return false;
        }

        if (sourceSlot.isCharacterFaceDown)
        {
            failReason = "뒷면 캐릭터는 합방을 시도할 수 없습니다.";
            return false;
        }

        if (targetSlot.isCharacterFaceDown)
        {
            failReason = "뒷면 캐릭터는 대상으로 선택할 수 없습니다.";
            return false;
        }

        if (!(sourceSlot.characterCard is CharacterCardData) ||
            !(targetSlot.characterCard is CharacterCardData))
        {
            failReason = "합방은 캐릭터 카드끼리만 가능합니다.";
            return false;
        }

        return true;
    }

    private void ResolveModifyCharacterStatsEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action onComplete)
    {
        ResolveModifyCharacterStatsEffectWithResult(
            candidate,
            context,
            _ => onComplete?.Invoke());
    }

    private void ResolveModifyCharacterStatsEffectWithResult(
        EffectCandidate candidate,
        EffectContext context,
        Action<bool> onComplete)
    {
        if (!CanActivateEffect(candidate, context, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke(false);
            return;
        }

        EffectContext safeContext = NormalizeContext(context, candidate.timing);
        if (safeContext.sourceEffect == null)
            safeContext.sourceEffect = candidate.sourceEffect;

        ModifyCharacterStatsRequest request = BuildModifyCharacterStatsRequest(candidate, safeContext);
        bool consumeAction = ShouldConsumeAction(candidate, context);
        int cost = GetActivationCost(candidate.card);

        ModifyCharacterStats(
            request,
            safeContext,
            result =>
            {
                bool success = result != null && result.success;
                string resultMessage = result?.message ?? "스탯 변경을 완료했습니다.";

                if (!success)
                {
                    if (IsModifyTaggedOnBoardEffect(candidate, safeContext) &&
                        resultMessage.Contains("스탯을 변경할 대상이 없습니다."))
                    {
                        resultMessage = "대상 캐릭터가 없습니다.";
                    }

                    battleManager?.SetSystemMessageFromExternal(resultMessage);
                    onComplete?.Invoke(false);
                    return;
                }

                if (cost > 0 &&
                    !battleManager.TryPayViewerCostFromExternal(candidate.owner, cost))
                {
                    battleManager?.SetSystemMessageFromExternal("시청자가 부족하여 효과를 발동할 수 없습니다.");
                    onComplete?.Invoke(false);
                    return;
                }

                if (candidate.sourceZone == EffectSourceZone.Hand &&
                    !MoveSourceHandCardToRestZone(candidate, context).success)
                {
                    battleManager?.SetSystemMessageFromExternal("효과 발동 카드를 손패에서 휴식존으로 이동할 수 없습니다.");
                    onComplete?.Invoke(false);
                    return;
                }

                string message = $"{candidate.card.name} 발동: {resultMessage}";

                if (cost > 0)
                    message += $"\n시청자 -{cost}";

                CompleteEffectResolution(message, consumeAction, null);
                onComplete?.Invoke(true);
            }
        );
    }

    private void ResolveIdolFullHealOneControlledEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action<bool> onComplete)
    {
        if (!CanActivateEffect(candidate, context, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke(false);
            return;
        }

        EffectContext safeContext = NormalizeContext(context, candidate.timing);
        if (safeContext.sourceEffect == null)
            safeContext.sourceEffect = candidate.sourceEffect;

        TargetSelector selector = new TargetSelector
        {
            scope = TargetSelectorScope.FieldCharacters,
            owner = EffectTargetOwner.ActingOwner,
            filter = new CardFilter
            {
                kind = EffectCardKind.Character,
                owner = EffectTargetOwner.ActingOwner,
                faceState = EffectFaceState.FaceUpOnly
            }
        };

        List<EffectTargetCandidate> candidates = BuildTargetCandidates(selector, safeContext);

        if (candidates == null || candidates.Count == 0)
        {
            battleManager?.SetSystemMessageFromExternal("회복할 캐릭터가 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (candidates.Count == 1)
        {
            ApplyIdolFullHealToTarget(candidate, candidates[0], onComplete);
            return;
        }

        CardQuestionPanel panel = battleManager != null
            ? battleManager.BattleCardQuestionPanel
            : null;

        if (panel == null)
        {
            battleManager?.SetSystemMessageFromExternal("CardQuestionPanel이 없어 회복 대상을 선택할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (panel.IsOpen())
        {
            battleManager?.SetSystemMessageFromExternal("이미 카드 선택창이 열려 있어 회복 대상을 선택할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        bool opened = panel.TryShowOptions(
            "회복할 캐릭터를 선택하세요.",
            BuildTargetOptions(candidates),
            true,
            selectedOption =>
            {
                EffectTargetCandidate target = FindTargetCandidate(candidates, selectedOption);
                ApplyIdolFullHealToTarget(candidate, target, onComplete);
            },
            () =>
            {
                battleManager?.SetSystemMessageFromExternal("아이돌 액티브 효과 발동을 취소했습니다.");
                onComplete?.Invoke(false);
            }
        );

        if (!opened)
        {
            battleManager?.SetSystemMessageFromExternal("카드 선택창을 열 수 없어 회복 대상을 선택할 수 없습니다.");
            onComplete?.Invoke(false);
        }
    }

    private void ApplyIdolFullHealToTarget(
        EffectCandidate candidate,
        EffectTargetCandidate target,
        Action<bool> onComplete)
    {
        BattleFieldSlot targetSlot = target != null ? target.slot : null;

        if (targetSlot == null || !targetSlot.HasCharacter || targetSlot.characterCard == null)
        {
            battleManager?.SetSystemMessageFromExternal("회복할 캐릭터가 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        int cost = GetActivationCost(candidate.card);
        if (cost > 0 &&
            !battleManager.TryPayViewerCostFromExternal(candidate.owner, cost))
        {
            battleManager?.SetSystemMessageFromExternal("시청자가 부족하여 효과를 발동할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        int beforeHp = targetSlot.currentCharacterHp;
        int maxHp = Mathf.Max(1, targetSlot.currentCharacterMaxHp);
        battleManager.FullHealCharacterFromExternal(targetSlot);
        battleManager.RefreshAllUIFromExternal();

        int afterHp = targetSlot.currentCharacterHp;
        string sourceName = candidate.card != null ? candidate.card.name : "아이돌";
        string targetName = targetSlot.characterCard != null ? targetSlot.characterCard.name : "선택 캐릭터";
        string message = $"{sourceName}의 액티브 효과로 {targetName}의 체력을 모두 회복했습니다.";

        if (cost > 0)
            message += $"\n시청자 -{cost}";

        Debug.Log(
            $"[IdolActive.FullHeal] source={sourceName}, target={targetName}, " +
            $"hp before={beforeHp}, hp after={afterHp}, maxHp={maxHp}"
        );

        CompleteEffectResolution(message, ShouldConsumeAction(candidate, null), null);
        onComplete?.Invoke(true);
    }

    private void ResolveCallFromRestByTagThenDonateViewersEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action<bool> onComplete)
    {
        if (!CanActivateEffect(candidate, context, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke(false);
            return;
        }

        EffectContext safeContext = NormalizeContext(context, candidate.timing);
        if (safeContext.sourceEffect == null)
            safeContext.sourceEffect = candidate.sourceEffect;

        EffectData effect = safeContext.sourceEffect ?? candidate.sourceEffect;
        string tag = GetStringParam(effect, "tag", "");
        List<BaseCardData> restCandidates = BuildRestZoneTaggedCharacterCandidates(candidate.owner, tag);

        if (restCandidates.Count == 0)
        {
            battleManager?.SetSystemMessageFromExternal($"휴식존에 {tag} 캐릭터가 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        List<BattleFieldSlot> slotCandidates = BuildEmptyOwnedBroadcastSlotCandidates(candidate.owner);

        if (slotCandidates.Count == 0)
        {
            battleManager?.SetSystemMessageFromExternal("출연시킬 수 있는 내 빈 방송 슬롯이 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        CardQuestionPanel panel = battleManager != null
            ? battleManager.BattleCardQuestionPanel
            : null;

        if (panel == null)
        {
            battleManager?.SetSystemMessageFromExternal("CardQuestionPanel이 없어 휴식존 캐릭터를 선택할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (panel.IsOpen())
        {
            battleManager?.SetSystemMessageFromExternal("이미 카드 선택창이 열려 있어 휴식존 캐릭터를 선택할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        bool opened = panel.TryShowOptions(
            "휴식존에서 출연시킬 #리코 캐릭터를 선택하세요.",
            BuildCardOptions(restCandidates),
            true,
            selectedOption =>
            {
                BaseCardData selectedCard = selectedOption != null ? selectedOption.card : null;
                RequestCallFromRestDestinationSlot(
                    candidate,
                    safeContext,
                    selectedCard,
                    onComplete);
            },
            () =>
            {
                battleManager?.SetSystemMessageFromExternal("아이돌 액티브 효과 발동을 취소했습니다.");
                onComplete?.Invoke(false);
            }
        );

        if (!opened)
        {
            battleManager?.SetSystemMessageFromExternal("카드 선택창을 열 수 없어 휴식존 캐릭터를 선택할 수 없습니다.");
            onComplete?.Invoke(false);
        }
    }

    private void RequestCallFromRestDestinationSlot(
        EffectCandidate candidate,
        EffectContext context,
        BaseCardData selectedCard,
        Action<bool> onComplete)
    {
        if (selectedCard == null)
        {
            battleManager?.SetSystemMessageFromExternal("선택한 휴식존 캐릭터 정보가 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (!battleManager.IsCardInRestZoneFromExternal(candidate.owner, selectedCard))
        {
            battleManager?.SetSystemMessageFromExternal("선택한 캐릭터가 더 이상 휴식존에 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        List<BattleFieldSlot> slotCandidates = BuildEmptyOwnedBroadcastSlotCandidates(candidate.owner);

        if (slotCandidates.Count == 0)
        {
            battleManager?.SetSystemMessageFromExternal("출연시킬 수 있는 내 빈 방송 슬롯이 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        CardQuestionPanel panel = battleManager != null
            ? battleManager.BattleCardQuestionPanel
            : null;

        if (panel == null)
        {
            battleManager?.SetSystemMessageFromExternal("CardQuestionPanel이 없어 출연 위치를 선택할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (panel.IsOpen())
        {
            battleManager?.SetSystemMessageFromExternal("이미 카드 선택창이 열려 있어 출연 위치를 선택할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        bool opened = panel.TryShowOptions(
            "출연시킬 내 빈 방송 슬롯을 선택하세요.",
            BuildSlotOptions(selectedCard, slotCandidates),
            true,
            selectedOption =>
            {
                BattleFieldSlot selectedSlot = selectedOption != null
                    ? selectedOption.linkedSlot
                    : null;

                ApplyCallFromRestByTagThenDonateViewers(
                    candidate,
                    context,
                    selectedCard,
                    selectedSlot,
                    onComplete);
            },
            () =>
            {
                battleManager?.SetSystemMessageFromExternal("아이돌 액티브 효과 발동을 취소했습니다.");
                onComplete?.Invoke(false);
            }
        );

        if (!opened)
        {
            battleManager?.SetSystemMessageFromExternal("카드 선택창을 열 수 없어 출연 위치를 선택할 수 없습니다.");
            onComplete?.Invoke(false);
        }
    }

    private void ApplyCallFromRestByTagThenDonateViewers(
        EffectCandidate candidate,
        EffectContext context,
        BaseCardData selectedCard,
        BattleFieldSlot selectedSlot,
        Action<bool> onComplete)
    {
        if (selectedCard == null || !(selectedCard is CharacterCardData))
        {
            battleManager?.SetSystemMessageFromExternal("출연시킬 캐릭터 카드 정보가 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (selectedSlot == null ||
            selectedSlot.owner != candidate.owner ||
            !selectedSlot.HasBroadcast ||
            selectedSlot.HasCharacter)
        {
            battleManager?.SetSystemMessageFromExternal("선택한 슬롯에는 캐릭터를 출연시킬 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (!battleManager.IsCardInRestZoneFromExternal(candidate.owner, selectedCard))
        {
            battleManager?.SetSystemMessageFromExternal("선택한 캐릭터가 더 이상 휴식존에 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        EffectData effect = context != null ? context.sourceEffect : candidate.sourceEffect;
        int cost = GetActivationCost(candidate.card);
        int donateAmount = ResolveDonateViewersAmount(effect);
        int requiredViewers = Mathf.Max(0, cost) + Mathf.Max(0, donateAmount);

        if (requiredViewers > 0 &&
            !battleManager.CanPayViewerCostFromExternal(candidate.owner, requiredViewers))
        {
            battleManager?.SetSystemMessageFromExternal("시청자가 부족하여 효과를 발동할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (cost > 0 &&
            !battleManager.TryPayViewerCostFromExternal(candidate.owner, cost))
        {
            battleManager?.SetSystemMessageFromExternal("시청자가 부족하여 효과를 발동할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        bool placed = TryPlaceRestCharacterOnEmptyOwnedBroadcastSlot(
            candidate.owner,
            selectedCard,
            selectedSlot,
            out string placeMessage);

        if (!placed)
        {
            battleManager?.SetSystemMessageFromExternal(placeMessage);
            onComplete?.Invoke(false);
            return;
        }

        TriggerOnAppearForRestReturnedCharacter(
            selectedSlot,
            selectedCard,
            () =>
            {
                string message = placeMessage;

                if (donateAmount > 0)
                {
                    ApplyDonateViewers(candidate.owner, donateAmount);
                    message += $"\n상대에게 시청자 {donateAmount}명을 넘겼습니다.";
                }
                else
                {
                    Debug.LogWarning("idol.active.callFromRestByTagThenDonateViewers: donateViewers params가 없어 시청자 기부 처리는 보류합니다.");
                    message += "\n시청자 기부 수치가 없어 기부 처리는 보류했습니다.";
                }

                if (cost > 0)
                    message += $"\n시청자 -{cost}";

                CompleteEffectResolution(message, ShouldConsumeAction(candidate, context), null);
                onComplete?.Invoke(true);
            });
    }

    private void ResolveFetchTabiOrRestBoongAndFetchBothEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action<bool> onComplete)
    {
        if (!CanActivateEffect(candidate, context, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke(false);
            return;
        }

        EffectContext safeContext = NormalizeContext(context, candidate.timing);
        if (safeContext.sourceEffect == null)
            safeContext.sourceEffect = candidate.sourceEffect;

        EffectData effect = safeContext.sourceEffect ?? candidate.sourceEffect;
        string tabiTag = GetStringParam(effect, "tag", "#타비");
        string bunnyTag = GetStringParam(effect, "bunnyTag", "#뿡댕이");

        if (BuildDeckTaggedCardCandidates(candidate.owner, tabiTag).Count == 0)
        {
            battleManager?.SetSystemMessageFromExternal($"덱에 {tabiTag} 카드가 없어 아이돌 액티브를 발동할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        List<BattleFieldSlot> bunnySlots =
            BuildOwnBroadcastFaceUpTaggedCharacterSlotCandidates(candidate.owner, bunnyTag);

        if (bunnySlots.Count == 0 ||
            BuildDeckTaggedCardCandidates(candidate.owner, bunnyTag).Count == 0)
        {
            ResolveBasicFetchTabiCard(candidate, tabiTag, onComplete);
            return;
        }

        RequestBoongCharacterToRestOrUseBasic(
            candidate,
            tabiTag,
            bunnyTag,
            bunnySlots,
            onComplete);
    }

    private void RequestBoongCharacterToRestOrUseBasic(
        EffectCandidate candidate,
        string tabiTag,
        string bunnyTag,
        List<BattleFieldSlot> bunnySlots,
        Action<bool> onComplete)
    {
        CardQuestionPanel panel = battleManager != null
            ? battleManager.BattleCardQuestionPanel
            : null;

        if (panel == null)
        {
            battleManager?.SetSystemMessageFromExternal("CardQuestionPanel이 없어 기본 효과로 진행합니다.");
            ResolveBasicFetchTabiCard(candidate, tabiTag, onComplete);
            return;
        }

        if (panel.IsOpen())
        {
            battleManager?.SetSystemMessageFromExternal("이미 카드 선택창이 열려 있어 아이돌 액티브를 발동할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        bool opened = panel.TryShowOptions(
            $"강화하려면 자신 방송의 앞면 {bunnyTag} 캐릭터 1장을 선택하세요.\n기본 효과만 사용하려면 취소하세요.",
            BuildSlotOptionsFromSlots(bunnySlots),
            true,
            selectedOption =>
            {
                BattleFieldSlot selectedSlot = selectedOption != null
                    ? selectedOption.linkedSlot
                    : null;

                RequestEnhancedFetchAfterRestingBoong(
                    candidate,
                    tabiTag,
                    bunnyTag,
                    selectedSlot,
                    onComplete);
            },
            () =>
            {
                ResolveBasicFetchTabiCard(candidate, tabiTag, onComplete);
            }
        );

        if (!opened)
        {
            battleManager?.SetSystemMessageFromExternal("카드 선택창을 열 수 없어 아이돌 액티브를 발동할 수 없습니다.");
            onComplete?.Invoke(false);
        }
    }

    private void RequestEnhancedFetchAfterRestingBoong(
        EffectCandidate candidate,
        string tabiTag,
        string bunnyTag,
        BattleFieldSlot selectedSlot,
        Action<bool> onComplete)
    {
        if (!IsOwnBroadcastFaceUpTaggedCharacterSlot(candidate.owner, selectedSlot, bunnyTag))
        {
            battleManager?.SetSystemMessageFromExternal("선택한 슬롯은 강화 효과 비용으로 사용할 수 없습니다.");
            onComplete?.Invoke(false);
            return;
        }

        if (BuildDeckTaggedCardCandidates(candidate.owner, tabiTag).Count == 0 ||
            BuildDeckTaggedCardCandidates(candidate.owner, bunnyTag).Count == 0)
        {
            battleManager?.SetSystemMessageFromExternal("덱의 서치 후보가 부족하여 기본 효과로 진행합니다.");
            ResolveBasicFetchTabiCard(candidate, tabiTag, onComplete);
            return;
        }

        MoveSelectedFieldCharacterToRestForEffect(
            selectedSlot,
            restSuccess =>
            {
                if (!restSuccess)
                {
                    battleManager?.SetSystemMessageFromExternal("선택한 #뿡댕이 캐릭터를 퇴장시킬 수 없습니다.");
                    onComplete?.Invoke(false);
                    return;
                }

                ResolveEnhancedFetchTabiThenBoong(candidate, tabiTag, bunnyTag, onComplete);
            });
    }

    private void ResolveBasicFetchTabiCard(
        EffectCandidate candidate,
        string tabiTag,
        Action<bool> onComplete)
    {
        RequestDeckTaggedCardToHand(
            candidate.owner,
            tabiTag,
            $"{tabiTag} 카드 1장을 선택해 패에 더하세요.",
            CardQuestionCancelPolicy.DisallowCancel,
            (success, selectedCard, message) =>
            {
                if (!success)
                {
                    battleManager?.SetSystemMessageFromExternal(message);
                    onComplete?.Invoke(false);
                    return;
                }

                CompleteEffectResolution($"{candidate.card.name} 발동: {message}", false, null);
                onComplete?.Invoke(true);
            });
    }

    private void ResolveEnhancedFetchTabiThenBoong(
        EffectCandidate candidate,
        string tabiTag,
        string bunnyTag,
        Action<bool> onComplete)
    {
        RequestDeckTaggedCardToHand(
            candidate.owner,
            tabiTag,
            $"{tabiTag} 카드 1장을 선택해 패에 더하세요.",
            CardQuestionCancelPolicy.DisallowCancel,
            (tabiSuccess, tabiCard, tabiMessage) =>
            {
                if (!tabiSuccess)
                {
                    battleManager?.SetSystemMessageFromExternal($"#뿡댕이 퇴장은 완료했습니다.\n{tabiMessage}");
                    onComplete?.Invoke(true);
                    return;
                }

                RequestDeckTaggedCardToHand(
                    candidate.owner,
                    bunnyTag,
                    $"{bunnyTag} 카드 1장을 선택해 패에 더하세요.",
                    CardQuestionCancelPolicy.DisallowCancel,
                    (bunnySuccess, bunnyCard, bunnyMessage) =>
                    {
                        string message =
                            $"{candidate.card.name} 강화 효과: #뿡댕이 캐릭터를 퇴장시켰습니다.\n" +
                            tabiMessage;

                        message += $"\n{bunnyMessage}";

                        CompleteEffectResolution(message, false, null);
                        onComplete?.Invoke(true);
                    });
            });
    }

    private void RequestDeckTaggedCardToHand(
        BattleSlotOwner owner,
        string tag,
        string questionMessage,
        CardQuestionCancelPolicy cancelPolicy,
        Action<bool, BaseCardData, string> onComplete)
    {
        List<BaseCardData> candidates = BuildDeckTaggedCardCandidates(owner, tag);

        if (candidates.Count == 0)
        {
            onComplete?.Invoke(false, null, $"덱에 {tag} 카드가 없습니다.");
            return;
        }

        CardQuestionPanel panel = battleManager != null
            ? battleManager.BattleCardQuestionPanel
            : null;

        if (panel == null)
        {
            onComplete?.Invoke(false, null, "CardQuestionPanel이 없어 덱 카드를 선택할 수 없습니다.");
            return;
        }

        if (panel.IsOpen())
        {
            onComplete?.Invoke(false, null, "이미 카드 선택창이 열려 있어 덱 카드를 선택할 수 없습니다.");
            return;
        }

        bool opened = panel.TryShowOptions(
            questionMessage,
            BuildCardOptions(candidates),
            cancelPolicy,
            selectedOption =>
            {
                BaseCardData selectedCard = selectedOption != null ? selectedOption.card : null;

                if (!AddDeckCardToHand(owner, selectedCard, out string moveMessage))
                {
                    onComplete?.Invoke(false, selectedCard, moveMessage);
                    return;
                }

                onComplete?.Invoke(true, selectedCard, $"{selectedCard.name}을 덱에서 패에 더했습니다.");
            },
            () =>
            {
                onComplete?.Invoke(false, null, $"{tag} 카드 선택을 취소했습니다.");
            }
        );

        if (!opened)
            onComplete?.Invoke(false, null, "카드 선택창을 열 수 없어 덱 카드를 선택할 수 없습니다.");
    }

    private bool AddDeckCardToHand(
        BattleSlotOwner owner,
        BaseCardData selectedCard,
        out string message)
    {
        message = "";

        if (selectedCard == null)
        {
            message = "선택한 덱 카드 정보가 없습니다.";
            return false;
        }

        ZoneMoveResult result = MoveCardBetweenZones(
            new ZoneMoveRequest
            {
                owner = owner,
                fromZone = EffectZone.Deck,
                toZone = EffectZone.Hand,
                card = selectedCard,
                reason = ZoneMoveReason.Effect
            },
            null);

        message = result != null ? result.message : "덱에서 패로 카드를 이동할 수 없습니다.";
        return result != null && result.success;
    }

    private void MoveSelectedFieldCharacterToRestForEffect(
        BattleFieldSlot selectedSlot,
        Action<bool> onComplete)
    {
        if (selectedSlot == null || !selectedSlot.HasCharacter || selectedSlot.characterCard == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        StartCoroutine(MoveSelectedFieldCharacterToRestForEffectRoutine(selectedSlot, onComplete));
    }

    private System.Collections.IEnumerator MoveSelectedFieldCharacterToRestForEffectRoutine(
        BattleFieldSlot selectedSlot,
        Action<bool> onComplete)
    {
        BaseCardData card = selectedSlot != null ? selectedSlot.characterCard : null;

        if (battleManager == null || selectedSlot == null || card == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        yield return battleManager.SendFieldCharacterToRestZoneRoutine(selectedSlot);

        bool moved = selectedSlot == null || !selectedSlot.HasCharacter || selectedSlot.characterCard != card;
        onComplete?.Invoke(moved);
    }

    private void TriggerOnAppearForRestReturnedCharacter(
        BattleFieldSlot slot,
        BaseCardData returnedCard,
        Action onComplete)
    {
        bool completed = false;
        Action completeOnce = () =>
        {
            if (completed)
                return;

            completed = true;
            onComplete?.Invoke();
        };

        if (battleManager == null || slot == null || returnedCard == null)
        {
            completeOnce();
            return;
        }

        if (restReturnOnAppearDepth >= MaxRestReturnOnAppearDepth)
        {
            Debug.LogWarning(
                $"EffectManager: 휴식존 복귀 OnAppear 체인 깊이가 {MaxRestReturnOnAppearDepth}에 도달해 추가 OnAppear를 중단합니다.");
            completeOnce();
            return;
        }

        restReturnOnAppearDepth++;
        bool depthReleased = false;
        Action releaseDepthAndComplete = () =>
        {
            if (!depthReleased)
            {
                restReturnOnAppearDepth = Mathf.Max(0, restReturnOnAppearDepth - 1);
                depthReleased = true;
            }

            completeOnce();
        };

        try
        {
            battleManager.RequestOnAppearEffectsFromExternal(
                slot,
                returnedCard,
                releaseDepthAndComplete);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            releaseDepthAndComplete();
        }
    }

    private void TriggerOnAppearForRestReturnedCharacters(
        List<RestReturnedCharacterEntry> returnedCharacters,
        Action onComplete)
    {
        bool completed = false;
        Action completeOnce = () =>
        {
            if (completed)
                return;

            completed = true;
            onComplete?.Invoke();
        };

        if (battleManager == null || returnedCharacters == null || returnedCharacters.Count == 0)
        {
            completeOnce();
            return;
        }

        if (restReturnOnAppearDepth >= MaxRestReturnOnAppearDepth)
        {
            Debug.LogWarning(
                $"EffectManager: 휴식존 복귀 OnAppear 체인 깊이가 {MaxRestReturnOnAppearDepth}에 도달해 추가 OnAppear를 중단합니다.");
            completeOnce();
            return;
        }

        List<RestReturnOnAppearChoice> choices = BuildRestReturnedOnAppearChoices(returnedCharacters);
        if (choices.Count == 0)
        {
            completeOnce();
            return;
        }

        ResolveRestReturnedOnAppearChoices(choices, completeOnce);
    }

    private List<RestReturnOnAppearChoice> BuildRestReturnedOnAppearChoices(
        List<RestReturnedCharacterEntry> returnedCharacters)
    {
        List<RestReturnOnAppearChoice> choices = new List<RestReturnOnAppearChoice>();

        foreach (RestReturnedCharacterEntry entry in returnedCharacters)
        {
            if (entry == null ||
                entry.slot == null ||
                entry.card == null ||
                !entry.slot.HasCharacter ||
                entry.slot.characterCard != entry.card ||
                entry.slot.isCharacterFaceDown)
            {
                continue;
            }

            EffectContext context = new EffectContext
            {
                battleManager = battleManager,
                collaborationManager = battleManager != null ? battleManager.collaborationManager : null,
                timing = EffectTiming.OnAppear,
                actingOwner = entry.slot.characterOwner,
                sourceSlot = entry.slot,
                sourceCard = entry.card,
                consumeAction = false
            };

            List<EffectCandidate> candidates = GetPlayableEffects(EffectTiming.OnAppear, context);
            foreach (EffectCandidate candidate in candidates)
            {
                if (candidate == null)
                    continue;

                choices.Add(new RestReturnOnAppearChoice
                {
                    candidate = candidate,
                    context = context
                });
            }
        }

        return choices;
    }

    private void ResolveRestReturnedOnAppearChoices(
        List<RestReturnOnAppearChoice> choices,
        Action onComplete)
    {
        if (choices == null || choices.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        CardQuestionPanel panel = battleManager != null
            ? battleManager.BattleCardQuestionPanel
            : null;

        if (panel == null)
        {
            battleManager?.SetSystemMessageFromExternal("CardQuestionPanel이 없어 출연 시 효과를 선택할 수 없습니다.");
            onComplete?.Invoke();
            return;
        }

        if (panel.IsOpen())
        {
            battleManager?.SetSystemMessageFromExternal("이미 카드 선택창이 열려 있어 출연 시 효과를 선택할 수 없습니다.");
            onComplete?.Invoke();
            return;
        }

        List<EffectCandidate> candidates = new List<EffectCandidate>();
        foreach (RestReturnOnAppearChoice choice in choices)
        {
            if (choice != null && choice.candidate != null)
                candidates.Add(choice.candidate);
        }

        if (candidates.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        bool opened = panel.TryShowOptions(
            "출연 시 발동할 카드를 선택하세요.",
            BuildOptionsFromCandidates(candidates),
            true,
            selectedOption =>
            {
                EffectCandidate selectedCandidate = selectedOption != null
                    ? selectedOption.linkedCandidate
                    : null;
                RestReturnOnAppearChoice selectedChoice = choices.Find(choice =>
                    choice != null && choice.candidate == selectedCandidate);

                if (selectedChoice == null)
                {
                    ResolveRestReturnedOnAppearChoices(choices, onComplete);
                    return;
                }

                choices.Remove(selectedChoice);
                ResolveRestReturnedOnAppearChoice(
                    selectedChoice,
                    () => ResolveRestReturnedOnAppearChoices(choices, onComplete));
            },
            () =>
            {
                battleManager?.SetSystemMessageFromExternal("출연 시 카드 효과를 발동하지 않습니다.");
                onComplete?.Invoke();
            }
        );

        if (!opened)
        {
            battleManager?.SetSystemMessageFromExternal("카드 선택창을 열 수 없어 출연 시 효과를 선택할 수 없습니다.");
            onComplete?.Invoke();
        }
    }

    private void ResolveRestReturnedOnAppearChoice(
        RestReturnOnAppearChoice choice,
        Action onComplete)
    {
        if (choice == null || choice.candidate == null)
        {
            onComplete?.Invoke();
            return;
        }

        restReturnOnAppearDepth++;
        bool depthReleased = false;
        Action releaseDepthAndComplete = () =>
        {
            if (!depthReleased)
            {
                restReturnOnAppearDepth = Mathf.Max(0, restReturnOnAppearDepth - 1);
                depthReleased = true;
            }

            onComplete?.Invoke();
        };

        try
        {
            ResolveEffect(choice.candidate, choice.context, releaseDepthAndComplete);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            releaseDepthAndComplete();
        }
    }

    private void ResolveCallFromRestByTagToEmptyPlatformsEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action<bool> onComplete)
    {
        if (!CanActivateEffect(candidate, context, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke(false);
            return;
        }

        EffectContext safeContext = NormalizeContext(context, candidate.timing);
        if (safeContext.sourceEffect == null)
            safeContext.sourceEffect = candidate.sourceEffect;

        if (safeContext.sourceSlot == null ||
            !safeContext.sourceSlot.HasCharacter ||
            safeContext.sourceSlot.isCharacterFaceDown)
        {
            battleManager?.SetSystemMessageFromExternal("앞면으로 출연한 캐릭터가 없어 효과를 처리하지 않습니다.");
            onComplete?.Invoke(true);
            return;
        }

        EffectData effect = safeContext.sourceEffect ?? candidate.sourceEffect;
        string tag = GetStringParam(effect, "tag", "");
        int maxCount = Mathf.Max(0, GetIntParam(effect, "maxCount", 0));

        if (maxCount <= 0)
        {
            battleManager?.SetSystemMessageFromExternal($"{candidate.card.name} 효과로 출연시킬 수 있는 수량이 없습니다.");
            onComplete?.Invoke(true);
            return;
        }

        ResolveCallFromRestByTagToEmptyPlatformsSequentially(
            candidate,
            safeContext,
            tag,
            maxCount,
            0,
            new List<RestReturnedCharacterEntry>(),
            onComplete);
    }

    private void ResolveCallFromRestByTagToEmptyPlatformsSequentially(
        EffectCandidate candidate,
        EffectContext context,
        string tag,
        int remainingCount,
        int placedCount,
        List<RestReturnedCharacterEntry> returnedCharacters,
        Action<bool> onComplete)
    {
        if (remainingCount <= 0)
        {
            FinishCallFromRestByTagToEmptyPlatforms(candidate, tag, placedCount, returnedCharacters, onComplete);
            return;
        }

        List<BaseCardData> restCandidates = BuildRestZoneTaggedCharacterCandidates(candidate.owner, tag);
        List<BattleFieldSlot> slotCandidates = BuildEmptyOwnedBroadcastSlotCandidates(candidate.owner);

        if (restCandidates.Count == 0 || slotCandidates.Count == 0)
        {
            FinishCallFromRestByTagToEmptyPlatforms(candidate, tag, placedCount, returnedCharacters, onComplete);
            return;
        }

        CardQuestionPanel panel = battleManager != null
            ? battleManager.BattleCardQuestionPanel
            : null;

        if (panel == null)
        {
            battleManager?.SetSystemMessageFromExternal("CardQuestionPanel이 없어 휴식존 캐릭터를 선택할 수 없습니다.");
            onComplete?.Invoke(placedCount > 0);
            return;
        }

        if (panel.IsOpen())
        {
            battleManager?.SetSystemMessageFromExternal("이미 카드 선택창이 열려 있어 휴식존 캐릭터를 선택할 수 없습니다.");
            onComplete?.Invoke(placedCount > 0);
            return;
        }

        int selectableCount = Mathf.Min(remainingCount, restCandidates.Count, slotCandidates.Count);
        bool opened = panel.TryShowOptions(
            $"휴식존에서 출연시킬 {tag} 캐릭터를 선택하세요. (남은 수: {selectableCount})",
            BuildCardOptions(restCandidates),
            true,
            selectedOption =>
            {
                BaseCardData selectedCard = selectedOption != null ? selectedOption.card : null;
                RequestCallFromRestByTagToEmptyPlatformsSlot(
                    candidate,
                    context,
                    tag,
                    remainingCount,
                    placedCount,
                    returnedCharacters,
                    selectedCard,
                    onComplete);
            },
            () =>
            {
                FinishCallFromRestByTagToEmptyPlatforms(candidate, tag, placedCount, returnedCharacters, onComplete);
            }
        );

        if (!opened)
        {
            battleManager?.SetSystemMessageFromExternal("카드 선택창을 열 수 없어 휴식존 캐릭터를 선택할 수 없습니다.");
            onComplete?.Invoke(placedCount > 0);
        }
    }

    private void RequestCallFromRestByTagToEmptyPlatformsSlot(
        EffectCandidate candidate,
        EffectContext context,
        string tag,
        int remainingCount,
        int placedCount,
        List<RestReturnedCharacterEntry> returnedCharacters,
        BaseCardData selectedCard,
        Action<bool> onComplete)
    {
        if (selectedCard == null)
        {
            FinishCallFromRestByTagToEmptyPlatforms(candidate, tag, placedCount, returnedCharacters, onComplete);
            return;
        }

        if (!battleManager.IsCardInRestZoneFromExternal(candidate.owner, selectedCard))
        {
            battleManager?.SetSystemMessageFromExternal("선택한 캐릭터가 더 이상 휴식존에 없습니다.");
            onComplete?.Invoke(placedCount > 0);
            return;
        }

        List<BattleFieldSlot> slotCandidates = BuildEmptyOwnedBroadcastSlotCandidates(candidate.owner);

        if (slotCandidates.Count == 0)
        {
            FinishCallFromRestByTagToEmptyPlatforms(candidate, tag, placedCount, returnedCharacters, onComplete);
            return;
        }

        bool opened = battleManager != null &&
            battleManager.RequestFieldSlotSelection(
            "출연시킬 위치를 골라주세요.",
            slotCandidates,
            selectedSlot =>
            {
                bool placed = TryPlaceRestCharacterOnEmptyOwnedBroadcastSlot(
                    candidate.owner,
                    selectedCard,
                    selectedSlot,
                    out string message);

                if (!placed)
                {
                    battleManager?.SetSystemMessageFromExternal(message);
                    onComplete?.Invoke(placedCount > 0);
                    return;
                }

                int nextPlacedCount = placedCount + 1;
                Debug.Log($"바니걸 타비 OnAppear: {selectedCard.name} 휴식존 출연 ({nextPlacedCount}장)");

                if (returnedCharacters != null)
                {
                    returnedCharacters.Add(new RestReturnedCharacterEntry
                    {
                        slot = selectedSlot,
                        card = selectedCard
                    });
                }

                ResolveCallFromRestByTagToEmptyPlatformsSequentially(
                    candidate,
                    context,
                    tag,
                    remainingCount - 1,
                    nextPlacedCount,
                    returnedCharacters,
                    onComplete);
            },
            () =>
            {
                FinishCallFromRestByTagToEmptyPlatforms(candidate, tag, placedCount, returnedCharacters, onComplete);
            }
        );

        if (!opened)
        {
            battleManager?.SetSystemMessageFromExternal("슬롯 선택을 시작할 수 없어 출연 위치를 선택할 수 없습니다.");
            onComplete?.Invoke(placedCount > 0);
        }
    }

    private bool TryPlaceRestCharacterOnEmptyOwnedBroadcastSlot(
        BattleSlotOwner owner,
        BaseCardData selectedCard,
        BattleFieldSlot selectedSlot,
        out string message)
    {
        message = "";

        if (selectedCard == null || !(selectedCard is CharacterCardData))
        {
            message = "출연시킬 캐릭터 카드 정보가 없습니다.";
            return false;
        }

        if (selectedSlot == null ||
            selectedSlot.owner != owner ||
            !selectedSlot.HasBroadcast ||
            selectedSlot.HasCharacter)
        {
            message = "선택한 슬롯에는 캐릭터를 출연시킬 수 없습니다.";
            return false;
        }

        if (!battleManager.IsCardInRestZoneFromExternal(owner, selectedCard))
        {
            message = "선택한 캐릭터가 더 이상 휴식존에 없습니다.";
            return false;
        }

        Sprite sprite = battleManager.LoadCardSpriteFromExternal(selectedCard);

        if (sprite == null)
        {
            message = $"{selectedCard.name} 카드 이미지를 찾을 수 없습니다.";
            return false;
        }

        if (!battleManager.RemoveCardFromRestZoneFromExternal(owner, selectedCard))
        {
            message = "휴식존에서 선택한 캐릭터를 제거할 수 없습니다.";
            return false;
        }

        selectedSlot.SetCharacterCard(selectedCard, sprite, false, owner);
        selectedSlot.faceUpSummonedTurn = battleManager.GetCurrentTurnCountFromExternal();
        battleManager.ApplyBroadcastEnterEffectsFromExternal(selectedSlot, false);
        battleManager.RefreshAllUIFromExternal();

        message = $"{selectedCard.name}을 휴식존에서 앞면으로 출연시켰습니다.";
        return true;
    }

    private void FinishCallFromRestByTagToEmptyPlatforms(
        EffectCandidate candidate,
        string tag,
        int placedCount,
        List<RestReturnedCharacterEntry> returnedCharacters,
        Action<bool> onComplete)
    {
        string sourceName = candidate != null && candidate.card != null
            ? candidate.card.name
            : "캐릭터";

        string message = placedCount > 0
            ? $"{sourceName} 효과로 휴식존의 {tag} 캐릭터 {placedCount}장을 앞면으로 출연시켰습니다."
            : $"{sourceName} 효과로 출연시킬 {tag} 캐릭터 또는 내 빈 방송 슬롯이 없습니다.";

        CompleteEffectResolution(message, false, null);
        TriggerOnAppearForRestReturnedCharacters(
            returnedCharacters,
            () => onComplete?.Invoke(true));
    }

    private void ResolveReturnUpToNFromRestToDeckEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action onComplete)
    {
        if (!CanActivateEffect(candidate, context, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke();
            return;
        }

        EffectContext safeContext = NormalizeContext(context, candidate.timing);
        if (safeContext.sourceEffect == null)
            safeContext.sourceEffect = candidate.sourceEffect;

        int maxCount = ResolveReturnRestToDeckMaxCount(safeContext.sourceEffect);
        CardFilter filter = BuildCardFilterFromParams(safeContext.sourceEffect, EffectCardKind.Any);
        List<BaseCardData> candidates = BuildRestZoneReturnCandidates(candidate.owner, filter);

        if (candidates.Count == 0)
        {
            battleManager?.SetSystemMessageFromExternal("되돌릴 카드가 없습니다.");
            onComplete?.Invoke();
            return;
        }

        int cost = GetActivationCost(candidate.card);

        if (cost > 0 &&
            !battleManager.TryPayViewerCostFromExternal(candidate.owner, cost))
        {
            battleManager?.SetSystemMessageFromExternal("시청자가 부족하여 효과를 발동할 수 없습니다.");
            onComplete?.Invoke();
            return;
        }

        if (candidate.sourceZone == EffectSourceZone.Hand &&
            !MoveSourceHandCardToRestZone(candidate, context).success)
        {
            battleManager?.SetSystemMessageFromExternal("효과 발동 카드를 손패에서 휴식존으로 이동할 수 없습니다.");
            onComplete?.Invoke();
            return;
        }

        DeckInsertPosition insertPosition = ResolveDeckInsertPosition(safeContext.sourceEffect, DeckInsertPosition.Shuffle);
        bool shuffleDeckAfterMove = GetBoolParam(safeContext.sourceEffect, "shuffleDeckAfterMove", false);
        bool consumeAction = ShouldConsumeAction(candidate, context);
        List<ZoneMoveResult> moveResults = new List<ZoneMoveResult>();

        RequestReturnRestCardsToDeckSequentially(
            candidate,
            safeContext,
            candidates,
            maxCount,
            insertPosition,
            shuffleDeckAfterMove,
            moveResults,
            () =>
            {
                string message = BuildReturnRestToDeckMessage(candidate, moveResults);

                if (cost > 0)
                    message += $"\n시청자 -{cost}";

                CompleteEffectResolution(message, consumeAction, onComplete);
            }
        );
    }

    private List<BaseCardData> BuildRestZoneReturnCandidates(
        BattleSlotOwner owner,
        CardFilter filter)
    {
        List<BaseCardData> candidates = new List<BaseCardData>();
        IReadOnlyList<BaseCardData> restCards = battleManager.GetRestZoneCardsFromExternal(owner);

        if (restCards == null)
            return candidates;

        foreach (BaseCardData card in restCards)
        {
            if (card == null)
                continue;

            if (EffectTargetingService.CardMatchesFilter(card, filter))
                candidates.Add(card);
        }

        return candidates;
    }

    private void RequestReturnRestCardsToDeckSequentially(
        EffectCandidate candidate,
        EffectContext context,
        List<BaseCardData> candidates,
        int remainingCount,
        DeckInsertPosition insertPosition,
        bool shuffleDeckAfterMove,
        List<ZoneMoveResult> moveResults,
        Action onComplete)
    {
        if (remainingCount <= 0 || candidates == null || candidates.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        if (candidates.Count == 1)
        {
            MoveRestCardToDeck(
                candidate,
                context,
                candidates[0],
                insertPosition,
                shuffleDeckAfterMove,
                moveResults);
            onComplete?.Invoke();
            return;
        }

        CardQuestionPanel panel = battleManager != null
            ? battleManager.BattleCardQuestionPanel
            : null;

        if (panel == null)
        {
            battleManager?.SetSystemMessageFromExternal("CardQuestionPanel이 없어 휴식존 카드를 선택할 수 없습니다.");
            onComplete?.Invoke();
            return;
        }

        if (panel.IsOpen())
        {
            battleManager?.SetSystemMessageFromExternal("이미 카드 선택창이 열려 있어 휴식존 카드를 선택할 수 없습니다.");
            onComplete?.Invoke();
            return;
        }

        bool opened = panel.TryShowOptions(
            "덱으로 되돌릴 카드를 선택하세요.",
            BuildCardOptions(candidates),
            true,
            selectedOption =>
            {
                BaseCardData selectedCard = selectedOption != null ? selectedOption.card : null;

                if (selectedCard != null)
                {
                    MoveRestCardToDeck(
                        candidate,
                        context,
                        selectedCard,
                        insertPosition,
                        shuffleDeckAfterMove,
                        moveResults);
                    RemoveFirstMatchingCard(candidates, selectedCard);
                }

                RequestReturnRestCardsToDeckSequentially(
                    candidate,
                    context,
                    candidates,
                    remainingCount - 1,
                    insertPosition,
                    shuffleDeckAfterMove,
                    moveResults,
                    onComplete);
            },
            onComplete
        );

        if (!opened)
        {
            battleManager?.SetSystemMessageFromExternal("카드 선택창을 열 수 없어 휴식존 카드를 선택할 수 없습니다.");
            onComplete?.Invoke();
        }
    }

    private void MoveRestCardToDeck(
        EffectCandidate candidate,
        EffectContext context,
        BaseCardData card,
        DeckInsertPosition insertPosition,
        bool shuffleDeckAfterMove,
        List<ZoneMoveResult> moveResults)
    {
        ZoneMoveResult moveResult = MoveCardBetweenZones(
            new ZoneMoveRequest
            {
                owner = candidate.owner,
                fromZone = EffectZone.Rest,
                toZone = EffectZone.Deck,
                card = card,
                reason = ZoneMoveReason.ReturnToDeck,
                deckInsertPosition = insertPosition,
                shuffleDeckAfterMove = shuffleDeckAfterMove
            },
            context
        );

        moveResults?.Add(moveResult);

        Debug.Log(
            $"[ReturnRestToDeck] source={candidate.card?.name}, owner={candidate.owner}, " +
            $"selected={(card != null ? card.name : "null")}, insert={insertPosition}, " +
            $"shuffle={shuffleDeckAfterMove}, success={moveResult.success}, message={moveResult.message}"
        );

        if (!moveResult.success)
            battleManager?.SetSystemMessageFromExternal(moveResult.message);
    }

    private List<CardQuestionOption> BuildCardOptions(List<BaseCardData> cards)
    {
        List<CardQuestionOption> options = new List<CardQuestionOption>();

        if (cards == null)
            return options;

        foreach (BaseCardData card in cards)
        {
            if (card != null)
                options.Add(new CardQuestionOption(card));
        }

        return options;
    }

    private List<BaseCardData> BuildRestZoneTaggedCharacterCandidates(
        BattleSlotOwner owner,
        string tag)
    {
        List<BaseCardData> candidates = new List<BaseCardData>();

        if (battleManager == null || string.IsNullOrWhiteSpace(tag))
            return candidates;

        IReadOnlyList<BaseCardData> restCards = battleManager.GetRestZoneCardsFromExternal(owner);

        if (restCards == null)
            return candidates;

        foreach (BaseCardData card in restCards)
        {
            if (card == null || !(card is CharacterCardData))
                continue;

            if (CardHasHashtag(card, tag))
                candidates.Add(card);
        }

        return candidates;
    }

    private List<BaseCardData> BuildDeckTaggedCardCandidates(
        BattleSlotOwner owner,
        string tag)
    {
        List<BaseCardData> candidates = new List<BaseCardData>();

        if (battleManager == null || string.IsNullOrWhiteSpace(tag))
            return candidates;

        IReadOnlyList<BaseCardData> deckCards = battleManager.GetMainDeckCardsFromExternal(owner);

        if (deckCards == null)
            return candidates;

        foreach (BaseCardData card in deckCards)
        {
            if (card == null)
                continue;

            if (CardHasHashtag(card, tag))
                candidates.Add(card);
        }

        return candidates;
    }

    private List<BattleFieldSlot> BuildOwnBroadcastFaceUpTaggedCharacterSlotCandidates(
        BattleSlotOwner owner,
        string tag)
    {
        List<BattleFieldSlot> candidates = new List<BattleFieldSlot>();

        if (battleManager == null || string.IsNullOrWhiteSpace(tag))
            return candidates;

        BattlePlayerSide side = owner == BattleSlotOwner.My
            ? BattlePlayerSide.My
            : BattlePlayerSide.Enemy;
        IReadOnlyList<BattleFieldSlot> slots = battleManager.GetSlotsForMovement(side);

        if (slots == null)
            return candidates;

        foreach (BattleFieldSlot slot in slots)
        {
            if (IsOwnBroadcastFaceUpTaggedCharacterSlot(owner, slot, tag))
                candidates.Add(slot);
        }

        return candidates;
    }

    private bool IsOwnBroadcastFaceUpTaggedCharacterSlot(
        BattleSlotOwner owner,
        BattleFieldSlot slot,
        string tag)
    {
        if (slot == null ||
            slot.owner != owner ||
            slot.characterOwner != owner ||
            !slot.HasCharacter ||
            slot.characterCard == null ||
            slot.isCharacterFaceDown)
        {
            return false;
        }

        return CardHasHashtag(slot.characterCard, tag);
    }

    private List<BattleFieldSlot> BuildEmptyOwnedBroadcastSlotCandidates(BattleSlotOwner owner)
    {
        List<BattleFieldSlot> candidates = new List<BattleFieldSlot>();

        if (battleManager == null)
            return candidates;

        IReadOnlyList<BattleFieldSlot> slots =
            battleManager.GetEmptyOwnedBroadcastSlotsFromExternal(owner);

        if (slots == null)
            return candidates;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot != null)
                candidates.Add(slot);
        }

        return candidates;
    }

    private List<CardQuestionOption> BuildSlotOptions(
        BaseCardData displayCard,
        List<BattleFieldSlot> slots)
    {
        List<CardQuestionOption> options = new List<CardQuestionOption>();

        if (displayCard == null || slots == null)
            return options;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot != null)
                options.Add(new CardQuestionOption(displayCard, slot));
        }

        return options;
    }

    private List<CardQuestionOption> BuildSlotOptionsFromSlots(List<BattleFieldSlot> slots)
    {
        List<CardQuestionOption> options = new List<CardQuestionOption>();

        if (slots == null)
            return options;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot != null && slot.characterCard != null)
                options.Add(new CardQuestionOption(slot.characterCard, slot));
        }

        return options;
    }

    private int ResolveDonateViewersAmount(EffectData effect)
    {
        int donateViewers = GetIntParam(effect, "donateViewers", 0);

        if (donateViewers > 0)
            return donateViewers;

        int donateAmount = GetIntParam(effect, "donateAmount", 0);

        if (donateAmount > 0)
            return donateAmount;

        int amount = GetIntParam(effect, "amount", 0);

        if (amount > 0)
            return amount;

        int viewersCost = GetIntParam(effect, "viewersCost", 0);

        return Mathf.Max(0, viewersCost);
    }

    private void ApplyDonateViewers(BattleSlotOwner owner, int amount)
    {
        int safeAmount = Mathf.Max(0, amount);

        if (safeAmount <= 0 || battleManager == null)
            return;

        BattleSlotOwner opponent = owner == BattleSlotOwner.My
            ? BattleSlotOwner.Enemy
            : BattleSlotOwner.My;

        battleManager.TryPayViewerCostFromExternal(owner, safeAmount);
        battleManager.ModifyViewersFromExternal(opponent, safeAmount);
        battleManager.RefreshAllUIFromExternal();
    }

    private void RemoveFirstMatchingCard(List<BaseCardData> cards, BaseCardData selectedCard)
    {
        if (cards == null || selectedCard == null)
            return;

        for (int i = 0; i < cards.Count; i++)
        {
            if (ReferenceEquals(cards[i], selectedCard) || cards[i] == selectedCard)
            {
                cards.RemoveAt(i);
                return;
            }
        }
    }

    private string BuildReturnRestToDeckMessage(
        EffectCandidate candidate,
        List<ZoneMoveResult> moveResults)
    {
        if (moveResults == null || moveResults.Count == 0)
            return $"{candidate.card.name} 발동: 덱으로 되돌린 카드가 없습니다.";

        List<string> movedNames = new List<string>();

        foreach (ZoneMoveResult result in moveResults)
        {
            if (result == null || !result.success || result.movedCard == null)
                continue;

            movedNames.Add(result.movedCard.name);
        }

        if (movedNames.Count == 0)
            return $"{candidate.card.name} 발동: 덱으로 되돌린 카드가 없습니다.";

        if (movedNames.Count == 1)
            return $"{movedNames[0]}을 덱으로 되돌렸습니다.";

        return $"{string.Join(", ", movedNames)}을 덱으로 되돌렸습니다.";
    }

    private int ResolveReturnRestToDeckMaxCount(EffectData effect)
    {
        int max = GetIntParam(effect, "max", 0);

        if (max > 0)
            return max;

        int maxCount = GetIntParam(effect, "maxCount", 0);

        if (maxCount > 0)
            return maxCount;

        int count = GetIntParam(effect, "count", 0);
        return count > 0 ? count : 1;
    }

    private DeckInsertPosition ResolveDeckInsertPosition(
        EffectData effect,
        DeckInsertPosition defaultPosition)
    {
        string rawPosition = GetStringParam(effect, "deckInsertPosition", "");

        if (string.IsNullOrWhiteSpace(rawPosition))
            return defaultPosition;

        if (Enum.TryParse(rawPosition.Trim(), true, out DeckInsertPosition position))
            return position;

        Debug.LogWarning($"EffectManager: 알 수 없는 deckInsertPosition 값입니다: {rawPosition}");
        return defaultPosition;
    }

    private ModifyCharacterStatsRequest BuildModifyCharacterStatsRequest(
        EffectCandidate candidate,
        EffectContext context)
    {
        EffectData effect = context != null && context.sourceEffect != null
            ? context.sourceEffect
            : candidate.sourceEffect;
        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetEffectRef(effect);

        if (effectRef == "character.active.adjacentHpDownAndTensionUpForTag")
            return BuildAdjacentHpDownAndTensionUpRequest(candidate, context, effect, effectRef);

        if (effectRef == "character.active.modifyTaggedOnBoard")
            return BuildModifyTaggedOnBoardRequest(candidate, context, effect, effectRef);

        if (IsAdjacentOpponentThisTurnTensionEffect(effectRef))
            return BuildAdjacentOpponentThisTurnTensionRequest(candidate, context, effect, effectRef);

        TargetSelector selector = new TargetSelector
        {
            scope = TargetSelectorScope.CollabParticipants,
            owner = EffectTargetOwner.ActingOwner,
            filter = new CardFilter
            {
                kind = EffectCardKind.Character,
                owner = EffectTargetOwner.ActingOwner,
                faceState = EffectFaceState.FaceUpOnly
            }
        };

        return new ModifyCharacterStatsRequest
        {
            owner = candidate.owner,
            selector = selector,
            deltas = new List<StatDelta>
            {
                new StatDelta
                {
                    statType = EffectStatType.CurrentHp,
                    amount = Mathf.Max(0, GetIntParam(effect, "amount", 0)),
                    duration = EffectStatDuration.Instant,
                    clampHpToMax = true,
                    allowBelowZero = false
                }
            },
            sourceCard = candidate.card,
            sourceEffectRef = effectRef,
            requireTargetSelection = false,
            maxTargets = 1
        };
    }

    private ModifyCharacterStatsRequest BuildAdjacentHpDownAndTensionUpRequest(
        EffectCandidate candidate,
        EffectContext context,
        EffectData effect,
        string effectRef)
    {
        CardFilter filter = BuildCardFilterFromParams(effect, EffectCardKind.Character);
        filter.faceState = EffectFaceState.FaceUpOnly;

        TargetSelector selector = new TargetSelector
        {
            scope = TargetSelectorScope.AdjacentToSource,
            owner = EffectTargetOwner.Any,
            filter = filter
        };

        int hpDown = Mathf.Abs(GetIntParam(effect, "hp", 0));
        int tensionDelta = GetIntParam(effect, "tensionDelta", 0);

        List<StatDelta> deltas = new List<StatDelta>();

        if (hpDown > 0)
        {
            deltas.Add(new StatDelta
            {
                statType = EffectStatType.CurrentHp,
                amount = -hpDown,
                duration = EffectStatDuration.Instant,
                clampHpToMax = true,
                allowBelowZero = false
            });
        }

        if (tensionDelta != 0)
        {
            deltas.Add(new StatDelta
            {
                statType = EffectStatType.CurrentTension,
                amount = tensionDelta,
                duration = EffectStatDuration.Permanent,
                clampHpToMax = false,
                allowBelowZero = false
            });
        }

        return new ModifyCharacterStatsRequest
        {
            owner = candidate.owner,
            selector = selector,
            deltas = deltas,
            sourceCard = candidate.card,
            sourceEffectRef = effectRef,
            requireTargetSelection = false,
            maxTargets = 0
        };
    }

    private ModifyCharacterStatsRequest BuildModifyTaggedOnBoardRequest(
        EffectCandidate candidate,
        EffectContext context,
        EffectData effect,
        string effectRef)
    {
        CardFilter filter = BuildCardFilterFromParams(effect, EffectCardKind.Character);
        TargetSelectorScope scope = ResolveTargetSelectorScopeParam(effect, TargetSelectorScope.OwnFieldCharacters);
        EffectTargetOwner owner = ResolveTargetOwnerParam(effect, ResolveDefaultOwnerForScope(scope));
        filter.owner = owner;
        filter.faceState = EffectFaceState.FaceUpOnly;

        TargetSelector selector = new TargetSelector
        {
            scope = scope,
            owner = owner,
            filter = filter
        };

        int hpMaxDelta = GetIntParam(effect, "hpMaxDelta", 0);
        int hpDelta = GetIntParam(effect, "hp", 0);
        int tensionDelta = GetIntParam(effect, "tensionDelta", 0);

        if (tensionDelta == 0)
            tensionDelta = GetIntParam(effect, "tension", 0);

        List<StatDelta> deltas = new List<StatDelta>();

        if (hpMaxDelta != 0)
        {
            deltas.Add(new StatDelta
            {
                statType = EffectStatType.MaxHp,
                amount = hpMaxDelta,
                duration = EffectStatDuration.Permanent,
                clampHpToMax = true,
                allowBelowZero = false
            });
        }

        if (hpDelta != 0)
        {
            deltas.Add(new StatDelta
            {
                statType = EffectStatType.CurrentHp,
                amount = hpDelta,
                duration = EffectStatDuration.Permanent,
                clampHpToMax = true,
                allowBelowZero = false
            });
        }

        if (tensionDelta != 0)
        {
            deltas.Add(new StatDelta
            {
                statType = EffectStatType.CurrentTension,
                amount = tensionDelta,
                duration = EffectStatDuration.Permanent,
                clampHpToMax = false,
                allowBelowZero = false
            });
        }

        int maxTargets = ResolveMaxTake(effect);
        bool requireTargetSelection = maxTargets == 1;
        int candidateCount = BuildTargetCandidates(selector, context).Count;
        string tag = GetStringParam(effect, "tag", "");

        Debug.Log(
            $"[ModifyTaggedOnBoard] source={candidate.card?.name}, scope={scope}, owner={owner}, " +
            $"tag={tag}, candidates={candidateCount}"
        );

        Debug.Log(
            $"[BuildModifyTaggedOnBoardRequest] source={candidate.card?.name}, ref={effectRef}, " +
            $"tag={tag}, hpMaxDelta={hpMaxDelta}, hpDelta={hpDelta}, " +
            $"tensionDelta={tensionDelta}, maxTargets={maxTargets}, requireTargetSelection={requireTargetSelection}"
        );

        return new ModifyCharacterStatsRequest
        {
            owner = candidate.owner,
            selector = selector,
            deltas = deltas,
            sourceCard = candidate.card,
            sourceEffectRef = effectRef,
            requireTargetSelection = requireTargetSelection,
            maxTargets = maxTargets
        };
    }

    private bool IsAdjacentOpponentThisTurnTensionEffect(string effectRef)
    {
        return effectRef == "character.onAppear.adjacentOppCollabTensionDeltaThisTurn" ||
            effectRef == "character.active.adjacentOppCollabTensionDeltaThisTurn";
    }

    private ModifyCharacterStatsRequest BuildAdjacentOpponentThisTurnTensionRequest(
        EffectCandidate candidate,
        EffectContext context,
        EffectData effect,
        string effectRef)
    {
        CardFilter filter = BuildCardFilterFromParams(effect, EffectCardKind.Character);
        filter.owner = EffectTargetOwner.OpponentOfActingOwner;
        filter.faceState = EffectFaceState.FaceUpOnly;

        TargetSelector selector = new TargetSelector
        {
            scope = TargetSelectorScope.AdjacentToSource,
            owner = EffectTargetOwner.Any,
            filter = filter
        };

        int amount = GetIntParam(effect, "tensionDelta", 0);
        if (amount == 0)
            amount = GetIntParam(effect, "amount", 0);

        return new ModifyCharacterStatsRequest
        {
            owner = candidate.owner,
            selector = selector,
            deltas = new List<StatDelta>
            {
                new StatDelta
                {
                    statType = EffectStatType.CurrentTension,
                    amount = amount,
                    duration = EffectStatDuration.ThisTurn,
                    clampHpToMax = false,
                    allowBelowZero = false
                }
            },
            sourceCard = candidate.card,
            sourceEffectRef = effectRef,
            requireTargetSelection = false,
            maxTargets = 0
        };
    }

    private PeekTopSelectRequest BuildPeekTopSelectRequest(
        EffectCandidate candidate,
        EffectContext context)
    {
        EffectData effect = context != null && context.sourceEffect != null
            ? context.sourceEffect
            : candidate.sourceEffect;
        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetEffectRef(effect);
        EffectCardKind defaultKind = effectRef == "character.active.peekTopAndTakeTaggedContents"
            ? EffectCardKind.Content
            : EffectCardKind.Any;
        CardFilter filter = BuildCardFilterFromParams(effect, defaultKind);
        int maxTake = ResolveMaxTake(effect);

        return new PeekTopSelectRequest
        {
            owner = candidate.owner,
            revealCount = Mathf.Max(0, GetIntParam(effect, "reveal", 0)),
            maxTake = Mathf.Min(1, Mathf.Max(1, maxTake)),
            minTake = 1,
            filter = filter,
            restPolicy = PeekRestPolicy.KeepOrderToBottom,
            reason = ZoneMoveReason.Effect,
            sourceEffectRef = effectRef,
            sourceCard = candidate.card,
            requireSelection = true,
            selectionCostPerCard = Mathf.Max(0, GetIntParam(effect, "extraCostPer", 0))
        };
    }

    private SearchDeckSelectRequest BuildSearchDeckSelectRequest(
        EffectCandidate candidate,
        EffectContext context)
    {
        EffectData effect = context != null && context.sourceEffect != null
            ? context.sourceEffect
            : candidate.sourceEffect;
        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetEffectRef(effect);
        CardFilter filter = BuildCardFilterFromParams(effect, EffectCardKind.Any);
        int maxTake = ResolveMaxTake(effect);

        return new SearchDeckSelectRequest
        {
            owner = candidate.owner,
            maxTake = Mathf.Min(1, Mathf.Max(1, maxTake)),
            minTake = 1,
            filter = filter,
            reason = ZoneMoveReason.Effect,
            sourceEffectRef = effectRef,
            sourceCard = candidate.card,
            requireSelection = true,
            shuffleDeckAfterSearch = false,
            selectionCostPerCard = Mathf.Max(0, GetIntParam(effect, "extraCostPer", 0))
        };
    }

    private SearchDeckSelectRequest BuildDiscardThenFetchSearchRequest(
        EffectCandidate candidate,
        EffectContext context,
        int searchCount)
    {
        EffectData effect = context != null && context.sourceEffect != null
            ? context.sourceEffect
            : candidate.sourceEffect;
        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetEffectRef(effect);
        CardFilter filter = BuildCardFilterFromParams(effect, EffectCardKind.Content);
        int maxTake = ResolveMaxTake(effect);
        maxTake = Mathf.Max(1, Mathf.Min(maxTake, Mathf.Max(1, searchCount)));

        return new SearchDeckSelectRequest
        {
            owner = candidate.owner,
            maxTake = Mathf.Min(1, maxTake),
            minTake = 1,
            filter = filter,
            reason = ZoneMoveReason.Effect,
            sourceEffectRef = effectRef,
            sourceCard = candidate.card,
            requireSelection = true,
            shuffleDeckAfterSearch = false,
            selectionCostPerCard = Mathf.Max(0, GetIntParam(effect, "extraCostPer", 0)),
            questionMessage = "패에 추가할 콘텐츠 카드를 선택하세요."
        };
    }

    private CardFilter BuildCardFilterFromParams(
        EffectData effect,
        EffectCardKind defaultKind)
    {
        string tag = GetStringParam(effect, "tag", "");
        string kind = GetStringParam(effect, "kind", "");
        CardFilter filter = new CardFilter
        {
            kind = !string.IsNullOrWhiteSpace(kind)
                ? ParseEffectCardKind(kind)
                : defaultKind,
            owner = EffectTargetOwner.Any,
            faceState = EffectFaceState.Any
        };

        if (!string.IsNullOrWhiteSpace(tag))
            filter.anyTags.Add(tag);

        string[] allTags = GetStringListParam(effect, "allTags");
        if (allTags != null)
        {
            foreach (string requiredTag in allTags)
            {
                if (!string.IsNullOrWhiteSpace(requiredTag))
                    filter.allTags.Add(requiredTag);
            }
        }

        return filter;
    }

    private int ResolveMaxTake(EffectData effect)
    {
        int max = GetIntParam(effect, "max", 0);

        if (max > 0)
            return max;

        int maxCount = GetIntParam(effect, "maxCount", 0);
        return maxCount > 0 ? maxCount : 1;
    }

    private EffectCardKind ParseEffectCardKind(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
            return EffectCardKind.Any;

        if (Enum.TryParse(kind.Trim(), true, out EffectCardKind parsedKind))
            return parsedKind;

        return EffectCardKind.Any;
    }

    private TargetSelectorScope ResolveTargetSelectorScopeParam(
        EffectData effect,
        TargetSelectorScope defaultScope)
    {
        string scope = GetStringParam(effect, "targetScope", "");

        if (string.IsNullOrWhiteSpace(scope))
            scope = GetStringParam(effect, "scope", "");

        if (string.IsNullOrWhiteSpace(scope))
            return defaultScope;

        if (Enum.TryParse(scope.Trim(), true, out TargetSelectorScope parsedScope))
            return parsedScope;

        Debug.LogWarning($"EffectManager: 알 수 없는 targetScope/scope 값입니다: {scope}");
        return defaultScope;
    }

    private EffectTargetOwner ResolveTargetOwnerParam(
        EffectData effect,
        EffectTargetOwner defaultOwner)
    {
        string owner = GetStringParam(effect, "targetOwner", "");

        if (string.IsNullOrWhiteSpace(owner))
            owner = GetStringParam(effect, "ownerScope", "");

        if (string.IsNullOrWhiteSpace(owner))
            return defaultOwner;

        if (Enum.TryParse(owner.Trim(), true, out EffectTargetOwner parsedOwner))
            return parsedOwner;

        Debug.LogWarning($"EffectManager: 알 수 없는 targetOwner/ownerScope 값입니다: {owner}");
        return defaultOwner;
    }

    private EffectTargetOwner ResolveDefaultOwnerForScope(TargetSelectorScope scope)
    {
        switch (scope)
        {
            case TargetSelectorScope.FieldCharacters:
                return EffectTargetOwner.Any;
            case TargetSelectorScope.OpponentFieldCharacters:
                return EffectTargetOwner.OpponentOfActingOwner;
            case TargetSelectorScope.OwnFieldCharacters:
            default:
                return EffectTargetOwner.ActingOwner;
        }
    }

    private void ResolveDrawThenDiscardEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action onComplete)
    {
        if (!CanActivateEffect(candidate, context, out string failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            onComplete?.Invoke();
            return;
        }

        int cost = GetActivationCost(candidate.card);

        if (cost > 0 &&
            !battleManager.TryPayViewerCostFromExternal(candidate.owner, cost))
        {
            battleManager?.SetSystemMessageFromExternal("시청자가 부족하여 효과를 발동할 수 없습니다.");
            onComplete?.Invoke();
            return;
        }

        if (candidate.sourceZone == EffectSourceZone.Hand &&
            !MoveSourceHandCardToRestZone(candidate, context).success)
        {
            battleManager?.SetSystemMessageFromExternal("효과 발동 카드를 손패에서 휴식존으로 이동할 수 없습니다.");
            onComplete?.Invoke();
            return;
        }

        EffectContext safeContext = NormalizeContext(context, candidate.timing);
        if (safeContext.sourceEffect == null)
            safeContext.sourceEffect = candidate.sourceEffect;

        int drawCount = GetIntParam(safeContext.sourceEffect, "draw", 0);
        int discardCount = GetIntParam(safeContext.sourceEffect, "discard", 0);

        battleManager.DrawCardsWithAnimationFromExternal(
            candidate.owner,
            Mathf.Max(0, drawCount),
            drawnCount =>
            {
                battleManager.RefreshAllUIFromExternal();

                string startMessage = $"{candidate.card.name} 발동: {drawnCount}장을 드로우했습니다.";
                if (cost > 0)
                    startMessage += $"\n시청자 -{cost}";

                if (discardCount <= 0)
                {
                    CompleteEffectResolution(startMessage, ShouldConsumeAction(candidate, context), onComplete);
                    return;
                }

                RequestDiscardCards(
                    candidate.owner,
                    Mathf.Max(0, discardCount),
                    startMessage,
                    ShouldConsumeAction(candidate, context),
                    onComplete
                );
            }
        );
    }

    private void RequestDiscardCards(
        BattleSlotOwner owner,
        int remainingCount,
        string accumulatedMessage,
        bool consumeAction,
        Action onComplete)
    {
        if (remainingCount <= 0)
        {
            CompleteEffectResolution(accumulatedMessage, consumeAction, onComplete);
            return;
        }

        IReadOnlyList<BaseCardData> hand = battleManager.GetHandCardsFromExternal(owner);

        if (hand == null || hand.Count == 0)
        {
            CompleteEffectResolution(
                $"{accumulatedMessage}\n버릴 패가 없어 남은 버림을 처리하지 않았습니다.",
                consumeAction,
                onComplete
            );
            return;
        }

        CardQuestionPanel panel = battleManager.BattleCardQuestionPanel;

        if (panel == null)
        {
            CompleteEffectResolution(
                $"{accumulatedMessage}\nCardQuestionPanel이 없어 패 버림을 처리하지 않았습니다.",
                consumeAction,
                onComplete
            );
            return;
        }

        if (panel.IsOpen())
        {
            CompleteEffectResolution(
                $"{accumulatedMessage}\n이미 카드 선택창이 열려 있어 패 버림을 처리하지 않았습니다.",
                consumeAction,
                onComplete
            );
            return;
        }

        List<CardQuestionOption> options = BuildHandOptions(owner, hand);

        bool opened = panel.TryShowOptions(
            $"버릴 카드를 선택하세요. ({remainingCount}장 남음)",
            options,
            false,
            selectedOption =>
            {
                BaseCardData discardCard = selectedOption != null
                    ? selectedOption.card
                    : null;
                int handIndex = selectedOption != null && selectedOption.linkedCandidate != null
                    ? selectedOption.linkedCandidate.handIndex
                    : battleManager.FindHandCardIndexFromExternal(owner, discardCard);

                ZoneMoveResult discardMoveResult = MoveCardBetweenZones(
                    new ZoneMoveRequest
                    {
                        owner = owner,
                        fromZone = EffectZone.Hand,
                        toZone = EffectZone.Rest,
                        card = discardCard,
                        handIndex = handIndex,
                        reason = ZoneMoveReason.Cost
                    },
                    null
                );

                if (discardCard == null || !discardMoveResult.success)
                {
                    CompleteEffectResolution(
                        $"{accumulatedMessage}\n선택한 카드를 버릴 수 없습니다: {discardMoveResult.message}",
                        consumeAction,
                        onComplete
                    );
                    return;
                }

                battleManager.RefreshAllUIFromExternal();
                RequestDiscardCards(
                    owner,
                    remainingCount - 1,
                    $"{accumulatedMessage}\n{discardCard.name} 카드를 버렸습니다.",
                    consumeAction,
                    onComplete
                );
            },
            null
        );

        if (!opened)
        {
            CompleteEffectResolution(
                $"{accumulatedMessage}\n카드 선택창을 열 수 없어 패 버림을 처리하지 않았습니다.",
                consumeAction,
                onComplete
            );
        }
    }

    private void RequestDiscardCardsForActiveFetch(
        BattleSlotOwner owner,
        int remainingCount,
        EffectContext context,
        string accumulatedMessage,
        Action<bool, string> onComplete)
    {
        if (remainingCount <= 0)
        {
            onComplete?.Invoke(true, accumulatedMessage);
            return;
        }

        IReadOnlyList<BaseCardData> hand = battleManager.GetHandCardsFromExternal(owner);

        if (hand == null || hand.Count == 0)
        {
            battleManager.SetSystemMessageFromExternal("버릴 카드가 없습니다.");
            onComplete?.Invoke(false, accumulatedMessage);
            return;
        }

        CardQuestionPanel panel = battleManager.BattleCardQuestionPanel;

        if (panel == null)
        {
            battleManager.SetSystemMessageFromExternal("CardQuestionPanel이 없어 패 버림을 처리하지 않았습니다.");
            onComplete?.Invoke(false, accumulatedMessage);
            return;
        }

        if (panel.IsOpen())
        {
            battleManager.SetSystemMessageFromExternal("이미 카드 선택창이 열려 있어 패 버림을 처리하지 않았습니다.");
            onComplete?.Invoke(false, accumulatedMessage);
            return;
        }

        List<CardQuestionOption> options = BuildHandOptions(owner, hand);

        bool opened = panel.TryShowOptions(
            "버릴 카드를 선택하세요.",
            options,
            true,
            selectedOption =>
            {
                BaseCardData discardCard = selectedOption != null
                    ? selectedOption.card
                    : null;
                int handIndex = selectedOption != null && selectedOption.linkedCandidate != null
                    ? selectedOption.linkedCandidate.handIndex
                    : battleManager.FindHandCardIndexFromExternal(owner, discardCard);

                ZoneMoveResult discardMoveResult = MoveCardBetweenZones(
                    new ZoneMoveRequest
                    {
                        owner = owner,
                        fromZone = EffectZone.Hand,
                        toZone = EffectZone.Rest,
                        card = discardCard,
                        handIndex = handIndex,
                        reason = ZoneMoveReason.Cost
                    },
                    context
                );

                Debug.Log(
                    $"[DiscardThenFetchContentByTagFromDeck] DiscardMove " +
                    $"{discardMoveResult.fromZone}->{discardMoveResult.toZone}, " +
                    $"success={discardMoveResult.success}, " +
                    $"card={(discardMoveResult.movedCard != null ? discardMoveResult.movedCard.name : "null")}, " +
                    $"message={discardMoveResult.message}");

                if (discardCard == null || !discardMoveResult.success)
                {
                    battleManager.SetSystemMessageFromExternal($"선택한 카드를 버릴 수 없습니다: {discardMoveResult.message}");
                    onComplete?.Invoke(false, accumulatedMessage);
                    return;
                }

                battleManager.RefreshAllUIFromExternal();
                RequestDiscardCardsForActiveFetch(
                    owner,
                    remainingCount - 1,
                    context,
                    $"{accumulatedMessage}\n{discardCard.name} 카드를 버렸습니다.",
                    onComplete
                );
            },
            () =>
            {
                battleManager.SetSystemMessageFromExternal("액티브 효과 발동을 취소했습니다.");
                onComplete?.Invoke(false, accumulatedMessage);
            }
        );

        if (!opened)
        {
            battleManager.SetSystemMessageFromExternal("카드 선택창을 열 수 없어 패 버림을 처리하지 않았습니다.");
            onComplete?.Invoke(false, accumulatedMessage);
        }
    }

    private List<CardQuestionOption> BuildTargetOptions(
        List<EffectTargetCandidate> candidates)
    {
        List<CardQuestionOption> options = new List<CardQuestionOption>();

        if (candidates == null)
            return options;

        foreach (EffectTargetCandidate candidate in candidates)
        {
            if (candidate == null || candidate.card == null)
                continue;

            options.Add(new CardQuestionOption(candidate.card, candidate.slot));
        }

        return options;
    }

    private EffectTargetCandidate FindTargetCandidate(
        List<EffectTargetCandidate> candidates,
        CardQuestionOption selectedOption)
    {
        if (candidates == null || selectedOption == null)
            return null;

        foreach (EffectTargetCandidate candidate in candidates)
        {
            if (candidate == null)
                continue;

            if (candidate.slot == selectedOption.linkedSlot &&
                candidate.card == selectedOption.card)
            {
                return candidate;
            }
        }

        return null;
    }

    private List<CardQuestionOption> BuildHandOptions(
        BattleSlotOwner owner,
        IReadOnlyList<BaseCardData> hand)
    {
        List<CardQuestionOption> options = new List<CardQuestionOption>();

        if (hand == null)
            return options;

        for (int i = 0; i < hand.Count; i++)
        {
            BaseCardData card = hand[i];

            if (card == null)
                continue;

            EffectCandidate linkedCandidate = new EffectCandidate
            {
                card = card,
                owner = owner,
                sourceZone = EffectSourceZone.Hand,
                handIndex = i,
                timing = EffectTiming.Content,
                consumeAction = false
            };

            options.Add(new CardQuestionOption(card, null, linkedCandidate));
        }

        return options;
    }

    private void CompleteEffectResolution(
        string message,
        bool consumeAction,
        Action onComplete)
    {
        if (consumeAction)
            battleManager.ResolveMyActionUsedFromExternal(message);
        else
            battleManager.SetSystemMessageFromExternal(message);

        onComplete?.Invoke();
    }

    private void ExecuteEffectByRefInternal(
        EffectCandidate candidate,
        EffectContext context,
        bool consumeAction)
    {
        string message = ExecuteEffectByRefInternal(candidate, context);
        int cost = GetActivationCost(candidate != null ? candidate.card : null);

        if (cost > 0)
            message += $"\n시청자 -{cost}";

        if (consumeAction)
            battleManager.ResolveMyActionUsedFromExternal(message);
        else
            battleManager.SetSystemMessageFromExternal(message);
    }

    private string ExecuteEffectByRefInternal(
        EffectCandidate candidate,
        EffectContext context)
    {
        if (candidate == null || candidate.card == null)
            return "효과 발동 카드 정보가 없습니다.";

        EffectContext safeContext = NormalizeContext(context, candidate.timing);
        if (safeContext.sourceEffect == null)
            safeContext.sourceEffect = candidate.sourceEffect;

        string effectRef = !string.IsNullOrEmpty(candidate.refId)
            ? candidate.refId
            : GetPrimaryEffectRef(candidate.card, safeContext.timing);
        EffectData effect = safeContext.sourceEffect ?? candidate.sourceEffect;

        switch (effectRef)
        {
            case "character.rest.gainViewers":
                return ModifyViewers(candidate.owner, GetIntParam(effect, "amount", 0), candidate.card);

            case "character.rest.loseViewers":
                return ModifyViewers(candidate.owner, -GetIntParam(effect, "amount", 0), candidate.card);

            case "content.drawThenDiscard":
                return DrawThenDiscard(
                    candidate.owner,
                    GetIntParam(effect, "draw", 0),
                    GetIntParam(effect, "discard", 0)
                );

            case "content.redrawIfBehindAndUniverseOnly":
                return RedrawIfBehindAndUniverseOnly(candidate);

            case "content.postCollabHealOwnParticipant":
                return HealOwnCollabParticipant(
                    candidate.owner,
                    safeContext,
                    GetIntParam(effect, "amount", 0)
                );

            case "idol.active.fullHealOneControlled":
                return "아이돌 액티브 회복 효과는 대상 선택 UI 연결 후 구현 예정입니다.";

            case "content.removeAllLastingContentsOnBoard":
                return RemoveAllLastingContentsOnBoard(candidate.owner);

            case "content.lasting.buffTagTensionAndHp":
                return $"{candidate.card.name} 지속형 보정은 필드에 설치된 동안 합방 계산에 반영됩니다.";

            case "content.collabClicheSpendBuffRefund":
                return "Our Tales 발동 테스트: 실제 효과는 아직 미구현입니다.";

            case "content.invertNegativeAmountForTagThisTurn":
                return RegisterNegativeAmountInvertThisTurn(
                    candidate.owner,
                    GetStringParam(effect, "tag", "#뿡댕이"),
                    candidate.card);

            default:
                return GetUnimplementedEffectMessage(candidate.card, effect, effectRef);
        }
    }

    private bool TryBuildCandidateFromRequest(
        EffectActivationRequest request,
        out EffectCandidate candidate,
        out EffectContext context,
        out string failReason)
    {
        candidate = null;
        context = null;
        failReason = "";

        if (request == null)
        {
            failReason = "효과 발동 요청 정보가 없습니다.";
            return false;
        }

        if (request.sourceCard == null)
        {
            failReason = "효과를 발동할 카드 정보가 없습니다.";
            return false;
        }

        context = new EffectContext
        {
            battleManager = battleManager,
            actingOwner = request.owner,
            timing = request.timing,
            sourceSlot = request.sourceSlot,
            targetSlot = request.targetSlot,
            sourceCard = request.sourceCard,
            sourceEffect = GetPrimaryEffectData(request.sourceCard, request.timing),
            consumeAction = ShouldConsumeActionForTiming(request.timing, request.consumeAction)
        };

        candidate = new EffectCandidate
        {
            card = request.sourceCard,
            owner = request.owner,
            sourceZone = ResolveRequestSourceZone(request),
            sourceSlot = request.sourceSlot,
            targetSlot = request.targetSlot,
            handIndex = request.sourceSlot == null
                ? ResolveRequestHandIndex(request)
                : -1,
            refId = GetPrimaryEffectRef(request.sourceCard, request.timing),
            sourceEffect = GetPrimaryEffectData(request.sourceCard, request.timing),
            timing = request.timing,
            consumeAction = ShouldConsumeActionForTiming(request.timing, request.consumeAction)
        };

        return true;
    }

    private EffectSourceZone ResolveRequestSourceZone(EffectActivationRequest request)
    {
        if (request == null)
            return EffectSourceZone.Unknown;

        if (request.sourceSlot != null)
            return EffectSourceZone.Field;

        if (request.timing == EffectTiming.Content)
            return EffectSourceZone.Hand;

        return EffectSourceZone.Field;
    }

    private bool ShouldConsumeAction(EffectCandidate candidate, EffectContext context)
    {
        if (candidate == null)
            return false;

        EffectTiming timing = context != null && context.timing != EffectTiming.None
            ? context.timing
            : candidate.timing;

        return ShouldConsumeActionForTiming(timing, candidate.consumeAction);
    }

    private bool ShouldConsumeActionForTiming(EffectTiming timing, bool requestedConsumeAction)
    {
        return (timing == EffectTiming.Content ||
                timing == EffectTiming.CharacterActive ||
                timing == EffectTiming.IdolActive) &&
            requestedConsumeAction;
    }

    private int ResolveRequestHandIndex(EffectActivationRequest request)
    {
        if (request == null)
            return -1;

        if (request.handIndex >= 0 &&
            battleManager.IsCardInHandAtIndexFromExternal(request.owner, request.handIndex, request.sourceCard))
        {
            return request.handIndex;
        }

        return battleManager.FindHandCardIndexFromExternal(request.owner, request.sourceCard);
    }

    private EffectContext NormalizeContext(EffectContext context, EffectTiming timing)
    {
        EffectContext safeContext = context ?? new EffectContext();

        if (safeContext.battleManager == null)
            safeContext.battleManager = battleManager;

        if (safeContext.collaborationManager == null && battleManager != null)
            safeContext.collaborationManager = battleManager.collaborationManager;

        if (safeContext.timing == EffectTiming.None)
            safeContext.timing = timing;

        if (safeContext.attackerOriginalSlot == null)
            safeContext.attackerOriginalSlot = safeContext.attackerSlot;

        if (safeContext.battleLocationSlot == null)
            safeContext.battleLocationSlot = safeContext.defenderSlot;

        if (safeContext.sourceCard == null)
        {
            if (timing == EffectTiming.OnRest)
                safeContext.sourceCard = safeContext.restedCard ?? safeContext.defeatedCard;
            else if (safeContext.sourceSlot != null)
                safeContext.sourceCard = safeContext.sourceSlot.characterCard;
        }

        return safeContext;
    }

    private List<CardQuestionOption> BuildOptionsFromCandidates(List<EffectCandidate> candidates)
    {
        List<CardQuestionOption> options = new List<CardQuestionOption>();

        foreach (EffectCandidate candidate in candidates)
        {
            if (candidate == null || candidate.card == null)
                continue;

            options.Add(new CardQuestionOption(candidate.card, candidate.sourceSlot, candidate));
        }

        return options;
    }

    private int GetActivationCost(BaseCardData card)
    {
        ContentCardData content = card as ContentCardData;

        if (content != null)
            return Mathf.Max(0, content.cost);

        CharacterCardData character = card as CharacterCardData;

        if (character != null)
            return Mathf.Max(0, character.activeCost);

        IdolCardData idol = card as IdolCardData;

        if (idol != null)
            return Mathf.Max(0, idol.activeCost);

        return 0;
    }

    private string ModifyViewers(BattleSlotOwner owner, int delta, BaseCardData sourceCard)
    {
        int appliedDelta = ApplyNegativeAmountInvertIfNeeded(owner, sourceCard, delta);
        int actualDelta = battleManager.ModifyViewersFromExternal(owner, appliedDelta);
        string ownerName = owner == BattleSlotOwner.My ? "내" : "상대";
        string sign = actualDelta >= 0 ? "+" : "";

        battleManager.RefreshAllUIFromExternal();
        return $"{ownerName} 시청자 {sign}{actualDelta}";
    }

    private string DrawThenDiscard(
        BattleSlotOwner owner,
        int drawCount,
        int discardCount)
    {
        int drawnCount = battleManager.DrawCardsFromExternal(owner, Mathf.Max(0, drawCount));
        string ownerName = owner == BattleSlotOwner.My ? "내" : "상대";
        string message = $"{ownerName} 덱에서 {drawnCount}장을 드로우했습니다.";

        if (discardCount > 0)
        {
            message +=
                $"\n패 {discardCount}장 버림은 대상 선택 UI 연결 후 구현 예정입니다.";
            Debug.LogWarning($"EffectManager: drawThenDiscard discard 미구현. owner={owner}, discard={discardCount}");
        }

        battleManager.RefreshAllUIFromExternal();
        return message;
    }

    private bool CanActivateRedrawIfBehindAndUniverseOnly(
        EffectCandidate candidate,
        out string failReason)
    {
        failReason = "";

        if (candidate == null)
        {
            failReason = "효과 후보 정보가 없습니다.";
            return false;
        }

        BaseCardData idolCard = battleManager.GetIdolCardFromExternal(candidate.owner);
        if (!CardHasHashtag(idolCard, "#유니버스"))
        {
            failReason = "아이돌 카드에 #유니버스 태그가 없어 사용할 수 없습니다.";
            return false;
        }

        BattleSlotOwner opponent = GetOpponentOwner(candidate.owner);
        int ownerViewers = battleManager.GetViewersFromExternal(candidate.owner);
        int opponentViewers = battleManager.GetViewersFromExternal(opponent);

        if (ownerViewers >= opponentViewers)
        {
            failReason = "상대보다 시청자가 적을 때만 사용할 수 있습니다.";
            return false;
        }

        if (CountHandCardsExcludingSource(candidate) <= 0)
        {
            failReason = "되돌릴 손패가 없어 사용할 수 없습니다.";
            return false;
        }

        return true;
    }

    private string RedrawIfBehindAndUniverseOnly(EffectCandidate candidate)
    {
        if (candidate == null || candidate.card == null)
            return "효과 발동 카드 정보가 없습니다.";

        List<BaseCardData> returnCards = BuildCurrentHandCards(candidate.owner);
        int returnedCount = 0;

        for (int i = returnCards.Count - 1; i >= 0; i--)
        {
            BaseCardData card = returnCards[i];
            int handIndex = battleManager.FindHandCardIndexFromExternal(candidate.owner, card);

            bool removed = handIndex >= 0 &&
                battleManager.RemoveHandCardAtIndexFromExternal(candidate.owner, handIndex, card);

            if (!removed)
                removed = battleManager.RemoveCardFromHandFromExternal(candidate.owner, card);

            if (!removed)
            {
                Debug.LogWarning($"Stars Align: 손패에서 되돌릴 카드를 제거하지 못했습니다: {card?.id}");
                continue;
            }

            battleManager.AddCardToMainDeckFromExternal(
                candidate.owner,
                card,
                DeckInsertPosition.Bottom,
                false
            );
            returnedCount++;
        }

        battleManager.ShuffleMainDeckFromExternal(candidate.owner);
        int drawnCount = battleManager.DrawCardsFromExternal(candidate.owner, returnedCount);
        battleManager.RefreshAllUIFromExternal();

        return $"{candidate.card.name} 발동: 손패 {returnedCount}장을 덱으로 되돌리고 {drawnCount}장 드로우했습니다.";
    }

    private int CountHandCardsExcludingSource(EffectCandidate candidate)
    {
        if (candidate == null || battleManager == null)
            return 0;

        IReadOnlyList<BaseCardData> hand = battleManager.GetHandCardsFromExternal(candidate.owner);
        if (hand == null)
            return 0;

        int count = 0;

        for (int i = 0; i < hand.Count; i++)
        {
            if (i == candidate.handIndex)
                continue;

            if (hand[i] != null)
                count++;
        }

        return count;
    }

    private List<BaseCardData> BuildCurrentHandCards(BattleSlotOwner owner)
    {
        List<BaseCardData> cards = new List<BaseCardData>();

        if (battleManager == null)
            return cards;

        IReadOnlyList<BaseCardData> hand = battleManager.GetHandCardsFromExternal(owner);
        if (hand == null)
            return cards;

        for (int i = 0; i < hand.Count; i++)
        {
            BaseCardData card = hand[i];

            if (card == null)
                continue;

            cards.Add(card);
        }

        return cards;
    }

    private BattleSlotOwner GetOpponentOwner(BattleSlotOwner owner)
    {
        return owner == BattleSlotOwner.My
            ? BattleSlotOwner.Enemy
            : BattleSlotOwner.My;
    }

    private string RegisterNegativeAmountInvertThisTurn(
        BattleSlotOwner owner,
        string sourceTag,
        BaseCardData sourceContentCard)
    {
        string safeTag = string.IsNullOrWhiteSpace(sourceTag) ? "#뿡댕이" : sourceTag;
        int turn = battleManager != null ? battleManager.GetCurrentTurnCountFromExternal() : 0;

        NegativeAmountInvertState existing = negativeAmountInvertStates.Find(state =>
            state != null &&
            state.owner == owner &&
            state.turn == turn &&
            string.Equals(state.sourceTag, safeTag, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            negativeAmountInvertStates.Add(new NegativeAmountInvertState
            {
                owner = owner,
                sourceTag = safeTag,
                turn = turn,
                sourceContentCard = sourceContentCard
            });
        }
        else
        {
            existing.sourceContentCard = sourceContentCard;
        }

        string sourceName = sourceContentCard != null ? sourceContentCard.name : "왜 말을 그렇게 해 ;ㅁ;";
        return $"{sourceName} 발동: 이번 턴 {safeTag} 카드가 발생시키는 부정적 수치 효과가 반전됩니다.";
    }

    public void ClearExpiredNegativeAmountInvertStatesFromExternal()
    {
        ClearExpiredNegativeAmountInvertStates();
    }

    private void ClearExpiredNegativeAmountInvertStates()
    {
        if (battleManager == null || negativeAmountInvertStates.Count == 0)
            return;

        int currentTurn = battleManager.GetCurrentTurnCountFromExternal();
        negativeAmountInvertStates.RemoveAll(state => state == null || state.turn != currentTurn);
    }

    public bool HasNegativeAmountInvertThisTurn(BattleSlotOwner owner, string sourceTag)
    {
        ClearExpiredNegativeAmountInvertStates();

        if (battleManager == null || string.IsNullOrWhiteSpace(sourceTag))
            return false;

        int currentTurn = battleManager.GetCurrentTurnCountFromExternal();
        return negativeAmountInvertStates.Exists(state =>
            state != null &&
            state.owner == owner &&
            state.turn == currentTurn &&
            string.Equals(state.sourceTag, sourceTag, StringComparison.OrdinalIgnoreCase));
    }

    public int ApplyNegativeAmountInvertIfNeeded(
        BattleSlotOwner owner,
        BaseCardData sourceCard,
        int amount)
    {
        if (amount >= 0 || sourceCard == null)
            return amount;

        ClearExpiredNegativeAmountInvertStates();
        int currentTurn = battleManager != null ? battleManager.GetCurrentTurnCountFromExternal() : 0;

        foreach (NegativeAmountInvertState state in negativeAmountInvertStates)
        {
            if (state == null ||
                state.owner != owner ||
                state.turn != currentTurn)
            {
                continue;
            }

            if (!IsNegativeAmountInvertSource(sourceCard, state.sourceTag))
                continue;

            battleManager?.SetSystemMessageFromExternal(
                $"{state.sourceContentCard?.name ?? "왜 말을 그렇게 해 ;ㅁ;"}: {state.sourceTag} 카드의 부정적 효과가 긍정적 효과로 바뀌었습니다.");
            return Mathf.Abs(amount);
        }

        return amount;
    }

    public int ApplyNegativeAmountInvertIfNeeded(
        BattleSlotOwner owner,
        BattleFieldSlot sourceSlot,
        int amount)
    {
        BaseCardData sourceCard = sourceSlot != null ? sourceSlot.characterCard : null;
        return ApplyNegativeAmountInvertIfNeeded(owner, sourceCard, amount);
    }

    public bool IsNegativeAmountInvertSource(BaseCardData sourceCard, string sourceTag)
    {
        return sourceCard != null &&
            !string.IsNullOrWhiteSpace(sourceTag) &&
            CardHasHashtag(sourceCard, sourceTag);
    }

    private bool CanActivateCollabClicheSpendBuffRefund(
        EffectCandidate candidate,
        EffectContext context,
        out string failReason)
    {
        failReason = "";

        EffectData effect = candidate != null ? candidate.sourceEffect : null;
        string tag = GetStringParam(effect, "tag", "#클리셰");
        BaseCardData idolCard = battleManager.GetIdolCardFromExternal(candidate.owner);

        if (!CardHasHashtag(idolCard, tag))
        {
            failReason = $"아이돌 카드에 {tag} 태그가 없어 사용할 수 없습니다.";
            return false;
        }

        if (context == null ||
            context.attackerSlot == null ||
            context.defenderSlot == null ||
            FindOwnedCollabParticipantSlot(candidate.owner, context) == null)
        {
            failReason = "합방에 참여한 내 캐릭터가 없어 사용할 수 없습니다.";
            return false;
        }

        return true;
    }

    private bool CanActivatePostCollabTabiBoostAndRebattle(
        EffectCandidate candidate,
        EffectContext context,
        out string failReason)
    {
        failReason = "";

        if (pendingPostCollabRebattle != null && !pendingPostCollabRebattle.processed)
        {
            failReason = "이미 추가 합방이 예약되어 있습니다.";
            return false;
        }

        EffectData effect = candidate != null ? candidate.sourceEffect : null;
        string tabiTag = GetStringParam(effect, "tabiTag", "#타비");
        string bunnyTag = GetStringParam(effect, "bunnyTag", "#뿡댕이");

        if (FindSurvivingOwnTaggedCollabParticipantSlot(candidate.owner, context, tabiTag) == null)
        {
            failReason = "여로를 사용할 수 없습니다: 합방에 참여한 내 #타비 또는 상대 캐릭터가 생존해 있지 않습니다.";
            return false;
        }

        BattleFieldSlot opponentSlot = FindSurvivingOpponentCollabParticipantSlot(candidate.owner, context);
        if (opponentSlot == null || opponentSlot.currentCharacterHp <= 0)
        {
            failReason = "여로를 사용할 수 없습니다: 합방에 참여한 내 #타비 또는 상대 캐릭터가 생존해 있지 않습니다.";
            return false;
        }

        int tensionSum;
        int hpSum;
        int bunnyCount = SumOwnedFaceUpTaggedCharactersOnBoard(
            candidate.owner,
            bunnyTag,
            out tensionSum,
            out hpSum);

        if (bunnyCount <= 0 || tensionSum + hpSum <= 0)
        {
            failReason = "여로를 사용할 수 없습니다: 내 필드 위 앞면 #뿡댕이가 없습니다.";
            return false;
        }

        return true;
    }

    private BattleFieldSlot FindOwnedCollabParticipantSlot(
        BattleSlotOwner owner,
        EffectContext context)
    {
        if (context == null)
            return null;

        if (IsOwnedCollabParticipantSlot(context.attackerSlot, owner))
            return context.attackerSlot;

        if (IsOwnedCollabParticipantSlot(context.defenderSlot, owner))
            return context.defenderSlot;

        return null;
    }

    private bool IsOwnedCollabParticipantSlot(BattleFieldSlot slot, BattleSlotOwner owner)
    {
        return slot != null &&
            slot.HasCharacter &&
            slot.characterCard != null &&
            slot.characterOwner == owner;
    }

    private BattleFieldSlot FindSurvivingOwnTaggedCollabParticipantSlot(
        BattleSlotOwner owner,
        EffectContext context,
        string tag)
    {
        BattleFieldSlot ownSlot = FindSurvivingOwnCollabParticipantSlot(owner, context);

        if (ownSlot == null ||
            ownSlot.characterCard == null ||
            !CardHasHashtag(ownSlot.characterCard, tag))
        {
            return null;
        }

        return ownSlot;
    }

    private BattleFieldSlot FindMatchingSurvivingOurTalesParticipantSlot(PendingOurTalesState state)
    {
        if (state == null)
            return null;

        BattleFieldSlot slot = FindMatchingSurvivingOurTalesParticipantSlot(state.participantSlot, state);
        if (slot != null)
            return slot;

        slot = FindMatchingSurvivingOurTalesParticipantSlot(state.attackerSlot, state);
        if (slot != null)
            return slot;

        return FindMatchingSurvivingOurTalesParticipantSlot(state.defenderSlot, state);
    }

    private BattleFieldSlot FindMatchingSurvivingOurTalesParticipantSlot(
        BattleFieldSlot slot,
        PendingOurTalesState state)
    {
        if (slot == null ||
            !slot.HasCharacter ||
            slot.characterCard == null ||
            slot.characterOwner != state.owner ||
            slot.currentCharacterHp <= 0)
        {
            return null;
        }

        if (state.participantCard != null && slot.characterCard != state.participantCard)
            return null;

        return slot;
    }

    private List<BattleFieldSlot> BuildOpponentFaceDownCharacterSlotCandidates(BattleSlotOwner owner)
    {
        List<BattleFieldSlot> candidates = new List<BattleFieldSlot>();

        if (battleManager == null)
            return candidates;

        AddOpponentFaceDownCharacterSlotCandidates(candidates, owner, BattlePlayerSide.My);
        AddOpponentFaceDownCharacterSlotCandidates(candidates, owner, BattlePlayerSide.Enemy);
        return candidates;
    }

    private void AddOpponentFaceDownCharacterSlotCandidates(
        List<BattleFieldSlot> candidates,
        BattleSlotOwner owner,
        BattlePlayerSide side)
    {
        IReadOnlyList<BattleFieldSlot> slots = battleManager.GetSlotsForMovement(side);
        if (slots == null)
            return;

        foreach (BattleFieldSlot slot in slots)
        {
            if (IsOpponentFaceDownCharacterSlot(owner, slot))
                candidates.Add(slot);
        }
    }

    private bool IsOpponentFaceDownCharacterSlot(BattleSlotOwner owner, BattleFieldSlot slot)
    {
        BattleSlotOwner opponent = GetOpponentOwner(owner);

        return slot != null &&
            slot.owner == opponent &&
            slot.HasCharacter &&
            slot.characterCard != null &&
            slot.characterOwner == opponent &&
            slot.isCharacterFaceDown;
    }

    private int GetHandCardCount(BattleSlotOwner owner)
    {
        IReadOnlyList<BaseCardData> hand = battleManager != null
            ? battleManager.GetHandCardsFromExternal(owner)
            : null;

        return hand != null ? hand.Count : 0;
    }

    private List<CardQuestionOption> BuildOpponentHandOptions(BattleSlotOwner opponent)
    {
        List<CardQuestionOption> options = new List<CardQuestionOption>();
        IReadOnlyList<BaseCardData> hand = battleManager != null
            ? battleManager.GetHandCardsFromExternal(opponent)
            : null;

        if (hand == null)
            return options;

        for (int i = 0; i < hand.Count; i++)
        {
            BaseCardData card = hand[i];
            if (card == null)
                continue;

            EffectCandidate linkedCandidate = new EffectCandidate
            {
                card = card,
                owner = opponent,
                sourceZone = EffectSourceZone.Hand,
                handIndex = i,
                timing = EffectTiming.Content,
                consumeAction = false
            };

            options.Add(new CardQuestionOption(card, null, linkedCandidate));
        }

        return options;
    }

    private int GetDoubleAppearCost(BaseCardData card)
    {
        int cost = 0;

        if (battleManager != null && battleManager.summonManager != null && card is CharacterCardData)
            cost = battleManager.summonManager.GetCharacterAppearCostFromExternal(card);

        return Mathf.Max(0, cost * 2);
    }

    private bool MoveOpponentHandCardToRest(
        BattleSlotOwner opponent,
        int handIndex,
        BaseCardData selectedCard)
    {
        if (selectedCard == null)
            return false;

        bool removed = battleManager.RemoveHandCardAtIndexFromExternal(opponent, handIndex, selectedCard);
        if (!removed)
            removed = battleManager.RemoveCardFromHandFromExternal(opponent, selectedCard);

        if (!removed)
            return false;

        battleManager.AddCardToRestZoneFromExternal(opponent, selectedCard);
        return true;
    }

    private string PlaceOpponentHandCharacterByKumorin(
        BattleSlotOwner opponent,
        int handIndex,
        BaseCardData selectedCard,
        BattleFieldSlot selectedSlot,
        int requiredCost)
    {
        string selectedName = selectedCard != null ? selectedCard.name : "선택 카드";

        if (!(selectedCard is CharacterCardData))
            return "공개된 카드는 캐릭터가 아니므로 아무 일도 일어나지 않습니다.";

        if (selectedSlot == null ||
            selectedSlot.owner != opponent ||
            !selectedSlot.HasBroadcast ||
            selectedSlot.HasCharacter)
        {
            MoveOpponentHandCardToRest(opponent, handIndex, selectedCard);
            return $"선택한 슬롯에 출연할 수 없어 {selectedName}이 휴식존으로 이동합니다.";
        }

        if (!battleManager.CanPayViewerCostFromExternal(opponent, requiredCost))
        {
            MoveOpponentHandCardToRest(opponent, handIndex, selectedCard);
            return $"상대가 2배 출연 코스트를 지불할 수 없어 {selectedName}이 휴식존으로 이동합니다.";
        }

        bool removed = battleManager.RemoveHandCardAtIndexFromExternal(opponent, handIndex, selectedCard);
        if (!removed)
            removed = battleManager.RemoveCardFromHandFromExternal(opponent, selectedCard);

        if (!removed)
        {
            battleManager.AddCardToRestZoneFromExternal(opponent, selectedCard);
            return $"선택한 손패를 출연시킬 수 없어 {selectedName}이 휴식존으로 이동합니다.";
        }

        if (!battleManager.TryPayViewerCostFromExternal(opponent, requiredCost))
        {
            battleManager.AddCardToRestZoneFromExternal(opponent, selectedCard);
            return $"상대가 2배 출연 코스트를 지불할 수 없어 {selectedName}이 휴식존으로 이동합니다.";
        }

        Sprite sprite = battleManager.LoadCardSpriteFromExternal(selectedCard);
        selectedSlot.SetCharacterCard(selectedCard, sprite, false, opponent);
        selectedSlot.faceUpSummonedTurn = battleManager.GetCurrentTurnCountFromExternal();
        battleManager.ApplyBroadcastEnterEffectsFromExternal(selectedSlot, false);
        return $"상대가 {selectedName}을 앞면 출연시켰습니다.";
    }

    private string ResolveOpponentHandCardSummonOrSack(
        EffectCandidate candidate,
        BattleSlotOwner opponent,
        BaseCardData selectedCard)
    {
        string selectedName = selectedCard != null ? selectedCard.name : "선택 카드";

        if (!(selectedCard is CharacterCardData))
        {
            battleManager.AddCardToRestZoneFromExternal(opponent, selectedCard);
            return $"{candidate.card.name} 발동: {selectedName}은(는) 캐릭터 카드가 아니어서 휴식존으로 이동했습니다.";
        }

        int doubleCost = GetDoubleAppearCost(selectedCard);
        IReadOnlyList<BattleFieldSlot> emptySlots = battleManager.GetEmptyOwnedBroadcastSlotsFromExternal(opponent);

        if (emptySlots != null &&
            emptySlots.Count > 0 &&
            battleManager.CanPayViewerCostFromExternal(opponent, doubleCost) &&
            battleManager.TryPayViewerCostFromExternal(opponent, doubleCost))
        {
            BattleFieldSlot targetSlot = emptySlots[0];
            Sprite sprite = battleManager.LoadCardSpriteFromExternal(selectedCard);
            targetSlot.SetCharacterCard(selectedCard, sprite, false, opponent);
            targetSlot.faceUpSummonedTurn = battleManager.GetCurrentTurnCountFromExternal();
            battleManager.ApplyBroadcastEnterEffectsFromExternal(targetSlot, false);
            return $"{candidate.card.name} 발동: {selectedName}이(가) 시청자 {doubleCost}을(를) 지불하고 앞면으로 출연했습니다.";
        }

        battleManager.AddCardToRestZoneFromExternal(opponent, selectedCard);
        return $"{candidate.card.name} 발동: {selectedName}이(가) 출연 조건을 만족하지 못해 휴식존으로 이동했습니다.";
    }

    private int SumOwnedFaceUpTaggedCharactersOnBoard(
        BattleSlotOwner owner,
        string tag,
        out int totalTension,
        out int totalHp)
    {
        totalTension = 0;
        totalHp = 0;

        if (battleManager == null || string.IsNullOrWhiteSpace(tag))
            return 0;

        BattlePlayerSide side = owner == BattleSlotOwner.My
            ? BattlePlayerSide.My
            : BattlePlayerSide.Enemy;

        return SumOwnedFaceUpTaggedCharactersOnBoard(side, owner, tag, ref totalTension, ref totalHp);
    }

    private int SumOwnedFaceUpTaggedCharactersOnBoard(
        BattlePlayerSide side,
        BattleSlotOwner owner,
        string tag,
        ref int totalTension,
        ref int totalHp)
    {
        int count = 0;
        IReadOnlyList<BattleFieldSlot> slots = battleManager.GetSlotsForMovement(side);
        if (slots == null)
            return 0;

        foreach (BattleFieldSlot slot in slots)
        {
            if (slot == null ||
                slot.owner != owner ||
                !slot.HasCharacter ||
                slot.characterCard == null ||
                slot.characterOwner != owner ||
                slot.isCharacterFaceDown ||
                !CardHasHashtag(slot.characterCard, tag))
            {
                continue;
            }

            totalTension += Mathf.Max(0, slot.currentCharacterTension);
            totalHp += Mathf.Max(0, slot.currentCharacterHp);
            count++;
        }

        return count;
    }

    private string HealOwnCollabParticipant(
        BattleSlotOwner owner,
        EffectContext context,
        int amount)
    {
        BattleFieldSlot targetSlot = FindSurvivingOwnCollabParticipantSlot(owner, context);

        if (targetSlot == null)
            return "합방에 참여한 내 캐릭터가 생존해 있지 않아 누룽지를 사용할 수 없습니다.";

        int healedAmount = battleManager.HealCharacterFromExternal(targetSlot, Mathf.Max(0, amount));
        battleManager.RefreshAllUIFromExternal();
        battleManager.RefreshFieldCharacterDetailFromExternal(targetSlot);

        string cardName = targetSlot.characterCard != null
            ? targetSlot.characterCard.name
            : "선택 캐릭터";

        return $"{cardName}의 체력을 {healedAmount} 회복했습니다.";
    }

    private BattleFieldSlot FindSurvivingOwnCollabParticipantSlot(
        BattleSlotOwner owner,
        EffectContext context)
    {
        if (context == null)
            return null;

        if (IsSurvivingOwnedCollabParticipantSlot(context.attackerSlot, owner))
            return context.attackerSlot;

        if (IsSurvivingOwnedCollabParticipantSlot(context.defenderSlot, owner))
            return context.defenderSlot;

        return null;
    }

    private bool IsSurvivingOwnedCollabParticipantSlot(BattleFieldSlot slot, BattleSlotOwner owner)
    {
        return slot != null &&
            slot.HasCharacter &&
            slot.characterCard != null &&
            slot.characterOwner == owner &&
            slot.currentCharacterHp > 0;
    }

    private bool HasIdolPassiveForSlot(
        BattleFieldSlot slot,
        string requiredEffectRef,
        out EffectData matchedEffect)
    {
        matchedEffect = null;

        if (battleManager == null ||
            slot == null ||
            !slot.HasCharacter ||
            slot.characterCard == null ||
            string.IsNullOrEmpty(requiredEffectRef))
        {
            return false;
        }

        IdolCardData idol = battleManager.GetIdolCardFromExternal(slot.characterOwner) as IdolCardData;

        if (idol == null || idol.passive == null)
            return false;

        foreach (EffectData passive in idol.passive)
        {
            if (passive == null)
                continue;

            if (!string.Equals(GetEffectRef(passive), requiredEffectRef, StringComparison.OrdinalIgnoreCase))
                continue;

            string tag = GetStringParam(passive, "tag", "");

            if (!string.IsNullOrEmpty(tag) &&
                !CardHasHashtag(slot.characterCard, tag))
            {
                continue;
            }

            matchedEffect = passive;
            return true;
        }

        return false;
    }

    private void LogRinPassiveCheck(
        BattleFieldSlot participantSlot,
        EffectContext context,
        bool applied)
    {
        if (participantSlot == null ||
            participantSlot.characterCard == null)
        {
            return;
        }

        BattleFieldSlot battleLocationSlot = GetCollaborationBattleLocationSlot(context);
        BattleFieldSlot attackerOriginalSlot = context != null
            ? context.attackerOriginalSlot
            : null;

        Debug.Log(
            $"[RinPassiveCheck] participant={participantSlot.characterCard.name}, " +
            $"participantOwner={participantSlot.characterOwner}, " +
            $"battleLocationOwner={(battleLocationSlot != null ? battleLocationSlot.owner.ToString() : "None")}, " +
            $"attackerOriginalOwner={(attackerOriginalSlot != null ? attackerOriginalSlot.owner.ToString() : "None")}, " +
            $"applied={applied}"
        );
    }

    private bool CardHasHashtag(BaseCardData card, string tag)
    {
        if (card == null || card.hashtags == null || string.IsNullOrEmpty(tag))
            return false;

        string normalizedTag = tag.Trim();

        foreach (string hashtag in card.hashtags)
        {
            if (string.Equals(
                hashtag != null ? hashtag.Trim() : "",
                normalizedTag,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string RemoveAllLastingContentsOnBoard(BattleSlotOwner owner)
    {
        int removedCount;
        battleManager.RemoveAllLastingContentsOnBoardFromExternal(owner, out removedCount);
        battleManager.RefreshAllUIFromExternal();

        return $"장기 콘텐츠 {removedCount}장을 제거했습니다.";
    }

    private string GetUnimplementedEffectMessage(
        BaseCardData sourceCard,
        EffectData effect,
        string effectRef)
    {
        string safeRef = string.IsNullOrEmpty(effectRef)
            ? "(ref 없음)"
            : effectRef;
        string effectName = effect != null && !string.IsNullOrEmpty(effect.id)
            ? effect.id
            : "효과";

        Debug.LogWarning($"EffectManager: 미구현 effectRef={safeRef}, card={sourceCard?.id}/{sourceCard?.name}, effect={effectName}");

        return
            $"{sourceCard.name} 효과를 발동했습니다.\n" +
            $"아직 구현되지 않은 효과입니다: {safeRef}";
    }

    private int GetIntParam(EffectData effect, string key, int defaultValue)
    {
        EffectParams effectParams = effect != null ? effect.@params : null;

        if (effectParams == null || string.IsNullOrEmpty(key))
            return defaultValue;

        switch (key)
        {
            case "amount":
                return effectParams.amount;
            case "draw":
                return effectParams.draw;
            case "discard":
                return effectParams.discard;
            case "hp":
                return effectParams.hp;
            case "tension":
                return effectParams.tension;
            case "tensionDelta":
                return effectParams.tensionDelta;
            case "hpMaxDelta":
                return effectParams.hpMaxDelta;
            case "max":
                return effectParams.max;
            case "maxCount":
                return effectParams.maxCount;
            case "count":
                return effectParams.count;
            case "discardCount":
                return effectParams.discardCount;
            case "searchCount":
                return effectParams.searchCount;
            case "range":
                return effectParams.range;
            case "reveal":
                return effectParams.reveal;
            case "extraCostPer":
                return effectParams.extraCostPer;
            case "donateViewers":
                return effectParams.donateViewers;
            case "donateAmount":
                return effectParams.donateAmount;
            case "viewersCost":
                return effectParams.viewersCost;
            default:
                return defaultValue;
        }
    }

    private string GetStringParam(EffectData effect, string key, string defaultValue)
    {
        EffectParams effectParams = effect != null ? effect.@params : null;

        if (effectParams == null || string.IsNullOrEmpty(key))
            return defaultValue;

        switch (key)
        {
            case "tag":
                return !string.IsNullOrEmpty(effectParams.tag) ? effectParams.tag : defaultValue;
            case "requireTag":
                return !string.IsNullOrEmpty(effectParams.requireTag) ? effectParams.requireTag : defaultValue;
            case "tabiTag":
                return !string.IsNullOrEmpty(effectParams.tabiTag) ? effectParams.tabiTag : defaultValue;
            case "bunnyTag":
                return !string.IsNullOrEmpty(effectParams.bunnyTag) ? effectParams.bunnyTag : defaultValue;
            case "kind":
                return !string.IsNullOrEmpty(effectParams.kind) ? effectParams.kind : defaultValue;
            case "targetOwner":
                return !string.IsNullOrEmpty(effectParams.targetOwner) ? effectParams.targetOwner : defaultValue;
            case "ownerScope":
                return !string.IsNullOrEmpty(effectParams.ownerScope) ? effectParams.ownerScope : defaultValue;
            case "targetScope":
                return !string.IsNullOrEmpty(effectParams.targetScope) ? effectParams.targetScope : defaultValue;
            case "scope":
                return !string.IsNullOrEmpty(effectParams.scope) ? effectParams.scope : defaultValue;
            case "deckInsertPosition":
                return !string.IsNullOrEmpty(effectParams.deckInsertPosition) ? effectParams.deckInsertPosition : defaultValue;
            default:
                return defaultValue;
        }
    }

    private string[] GetStringListParam(EffectData effect, string key)
    {
        EffectParams effectParams = effect != null ? effect.@params : null;

        if (effectParams == null || string.IsNullOrEmpty(key))
            return Array.Empty<string>();

        if (key == "allTags" && effectParams.allTags != null)
            return effectParams.allTags;

        return Array.Empty<string>();
    }

    private bool GetBoolParam(EffectData effect, string key, bool defaultValue)
    {
        EffectParams effectParams = effect != null ? effect.@params : null;

        if (effectParams == null || string.IsNullOrEmpty(key))
            return defaultValue;

        switch (key)
        {
            case "oncePerTurn":
                return effectParams.oncePerTurn;
            case "faceUp":
                return effectParams.faceUp;
            case "shuffleDeckAfterMove":
                return effectParams.shuffleDeckAfterMove;
            default:
                return defaultValue;
        }
    }

    private string ResolveImmediateContentEffectMessage(
        BaseCardData sourceCard,
        BattleSlotOwner owner,
        string effectRef)
    {
        if (sourceCard is CharacterCardData)
            return ResolveCharacterTimingEffectMessage(sourceCard, effectRef);

        switch (effectRef)
        {
            case "content.removeAllLastingContentsOnBoard":
                int removedCount;
                battleManager.RemoveAllLastingContentsOnBoardFromExternal(owner, out removedCount);
                return $"{sourceCard.name} 발동: 장기 콘텐츠 {removedCount}장을 제거했습니다.";

            case "content.collabClicheSpendBuffRefund":
                // TODO: Our Tales 실제 조건, 시청자 소모 기반 버프, 합방 후 환급 처리를 구현한다.
                return "Our Tales 발동 테스트: 실제 효과는 아직 미구현입니다.";

            default:
                return
                    $"{sourceCard.name} 콘텐츠 카드 효과를 발동했습니다.\n" +
                    "효과 발동 성공: 실제 효과는 아직 미구현입니다.";
        }
    }

    private string ResolveCharacterTimingEffectMessage(BaseCardData sourceCard, string effectRef)
    {
        string timingText = "효과";

        if (!string.IsNullOrEmpty(effectRef))
        {
            if (effectRef.IndexOf("appear", StringComparison.OrdinalIgnoreCase) >= 0)
                timingText = "출연 시 효과";
            else if (effectRef.IndexOf("rest", StringComparison.OrdinalIgnoreCase) >= 0)
                timingText = "휴식 시 효과";
            else if (effectRef.IndexOf("collab", StringComparison.OrdinalIgnoreCase) >= 0)
                timingText = "합방 후 효과";
        }

        return $"{sourceCard.name}의 {timingText}가 발동되었습니다.\n실제 효과는 아직 미구현입니다.";
    }

    private string GetPrimaryEffectRef(BaseCardData card)
    {
        return GetPrimaryEffectRef(card, EffectTiming.None);
    }

    private string GetPrimaryEffectRef(BaseCardData card, EffectTiming preferredTiming)
    {
        EffectData effect = GetPrimaryEffectData(card, preferredTiming);
        return GetEffectRef(effect);
    }

    private EffectData GetPrimaryEffectData(BaseCardData card, EffectTiming preferredTiming)
    {
        EffectData[] effects = null;

        if (card is ContentCardData content)
            effects = content.effects;
        else if (card is CharacterCardData character)
            effects = character.effects;
        else if (card is BroadcastCardData broadcast)
            effects = broadcast.effects;
        else if (card is IdolCardData idol)
            effects = MergeIdolEffects(idol);

        if (effects == null)
            return null;

        if (preferredTiming != EffectTiming.None)
        {
            foreach (EffectData effect in effects)
            {
                if (effect == null || string.IsNullOrEmpty(effect.timing))
                    continue;

                if (!TryParseEffectTiming(effect.timing, out EffectTiming effectTiming) ||
                    effectTiming != preferredTiming)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(GetEffectRef(effect)))
                    return effect;
            }
        }

        foreach (EffectData effect in effects)
        {
            if (!string.IsNullOrEmpty(GetEffectRef(effect)))
                return effect;
        }

        if (preferredTiming != EffectTiming.None)
        {
            foreach (EffectData effect in effects)
            {
                if (effect == null || string.IsNullOrEmpty(effect.timing))
                    continue;

                if (TryParseEffectTiming(effect.timing, out EffectTiming effectTiming) &&
                    effectTiming == preferredTiming)
                {
                    return effect;
                }
            }
        }

        return null;
    }

    private EffectData[] MergeIdolEffects(IdolCardData idol)
    {
        if (idol == null)
            return null;

        List<EffectData> effects = new List<EffectData>();

        if (idol.active != null)
            effects.AddRange(idol.active);

        if (idol.passive != null)
            effects.AddRange(idol.passive);

        return effects.ToArray();
    }

    private string GetEffectRef(EffectData effect)
    {
        if (effect == null)
            return "";

        if (!string.IsNullOrEmpty(effect.refName))
            return effect.refName;

        return effect.@ref;
    }

    private bool IsContentCard(BaseCardData card)
    {
        if (card == null || string.IsNullOrEmpty(card.kind))
            return false;

        return string.Equals(card.kind, "Content", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(card.kind, "Contents", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsCharacterCard(BaseCardData card)
    {
        if (card == null || string.IsNullOrEmpty(card.kind))
            return false;

        return string.Equals(card.kind, "Character", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsEffectCardKindSupportedAtTiming(BaseCardData card, EffectTiming timing)
    {
        if (IsContentCard(card))
            return true;

        if (IsCharacterCard(card))
        {
            return timing == EffectTiming.OnAppear ||
                timing == EffectTiming.OnRest ||
                timing == EffectTiming.PostCollab ||
                timing == EffectTiming.CharacterActive ||
                timing == EffectTiming.IdolActive;
        }

        if (card is IdolCardData)
            return timing == EffectTiming.IdolActive;

        return false;
    }

    private BaseCardData ResolveContextCharacterCard(EffectContext context, EffectTiming timing)
    {
        if (context == null)
            return null;

        if (timing == EffectTiming.OnRest)
            return context.restedCard ?? context.defeatedCard ?? context.sourceCard;

        if (context.sourceCard != null)
            return context.sourceCard;

        if (context.sourceSlot != null)
            return context.sourceSlot.characterCard;

        return null;
    }

    private BattleFieldSlot ResolveContextCharacterSlot(EffectContext context, EffectTiming timing)
    {
        if (context == null)
            return null;

        if (timing == EffectTiming.OnRest)
            return context.restedSlot ?? context.defeatedSlot ?? context.sourceSlot;

        return context.sourceSlot;
    }

    private EffectTiming ResolveCardEffectTiming(BaseCardData card, EffectTiming requestedTiming)
    {
        if (IsContentCard(card))
            return ResolveContentCardTiming(card, requestedTiming);

        CharacterCardData character = card as CharacterCardData;

        if (character != null && character.effects != null)
            return ResolveTimingFromEffects(character.effects, requestedTiming, EffectTiming.None);

        BroadcastCardData broadcast = card as BroadcastCardData;

        if (broadcast != null && broadcast.effects != null)
            return ResolveTimingFromEffects(broadcast.effects, requestedTiming, EffectTiming.None);

        return requestedTiming;
    }

    private EffectTiming ResolveContentCardTiming(BaseCardData card)
    {
        return ResolveContentCardTiming(card, EffectTiming.None);
    }

    private EffectTiming ResolveContentCardTiming(BaseCardData card, EffectTiming requestedTiming)
    {
        ContentCardData content = card as ContentCardData;

        if (content != null && content.effects != null)
        {
            EffectTiming requested = ResolveTimingFromEffects(
                content.effects,
                requestedTiming,
                EffectTiming.None
            );

            if (requested != EffectTiming.None)
                return requested;

            foreach (EffectData effect in content.effects)
            {
                if (effect == null || string.IsNullOrEmpty(effect.timing))
                    continue;

                if (TryParseEffectTiming(effect.timing, out EffectTiming timing) &&
                    timing == EffectTiming.Content)
                {
                    return EffectTiming.Content;
                }
            }

            foreach (EffectData effect in content.effects)
            {
                if (effect == null || string.IsNullOrEmpty(effect.timing))
                    continue;

                if (TryParseEffectTiming(effect.timing, out EffectTiming timing))
                    return timing;
            }
        }

        if (content != null &&
            TryResolveContentTypeTiming(content.contentType, out EffectTiming contentTypeTiming))
        {
            return contentTypeTiming;
        }

        // TODO: JSON에 콘텐츠 카드 전용 timing 필드를 추가하면 이 기본값 대신 해당 값을 사용한다.
        return EffectTiming.Content;
    }

    private EffectTiming ResolveTimingFromEffects(
        EffectData[] effects,
        EffectTiming requestedTiming,
        EffectTiming fallbackTiming)
    {
        if (effects == null)
            return fallbackTiming;

        if (requestedTiming != EffectTiming.None)
        {
            foreach (EffectData effect in effects)
            {
                if (effect == null || string.IsNullOrEmpty(effect.timing))
                    continue;

                if (TryParseEffectTiming(effect.timing, out EffectTiming timing) &&
                    timing == requestedTiming)
                {
                    return timing;
                }
            }
        }

        return fallbackTiming;
    }

    private bool TryResolveContentTypeTiming(string contentType, out EffectTiming timing)
    {
        timing = EffectTiming.Content;

        if (string.IsNullOrEmpty(contentType))
            return false;

        string normalized = NormalizeTimingText(contentType);

        switch (normalized)
        {
            case "immediate":
            case "instant":
            case "main":
            case "mainphase":
            case "content":
            case "normal":
            case "continuous":
            case "longterm":
            case "lasting":
                timing = EffectTiming.Content;
                return true;

            case "collab":
            case "collaboration":
            case "beforecollab":
            case "precollab":
                timing = EffectTiming.PreCollab;
                return true;

            case "aftercollab":
            case "postcollab":
                timing = EffectTiming.PostCollab;
                return true;

            default:
                return false;
        }
    }

    private bool TryParseEffectTiming(string rawTiming, out EffectTiming timing)
    {
        timing = EffectTiming.Content;

        if (string.IsNullOrEmpty(rawTiming))
            return false;

        string normalized = NormalizeTimingText(rawTiming);

        switch (normalized)
        {
            case "main":
            case "mainphase":
            case "immediate":
            case "instant":
            case "content":
                timing = EffectTiming.Content;
                return true;

            case "beforecollab":
            case "precollab":
            case "collab":
            case "collaboration":
                timing = EffectTiming.PreCollab;
                return true;

            case "aftercollab":
            case "postcollab":
                timing = EffectTiming.PostCollab;
                return true;

            case "onappear":
                timing = EffectTiming.OnAppear;
                return true;

            case "rest":
            case "onrest":
                timing = EffectTiming.OnRest;
                return true;

            case "passive":
            case "always":
                timing = EffectTiming.Passive;
                return true;

            case "characteractive":
            case "characteract":
                timing = EffectTiming.CharacterActive;
                return true;

            case "idolactive":
                timing = EffectTiming.IdolActive;
                return true;

            case "active":
                timing = EffectTiming.CharacterActive;
                return true;

            case "broadcast":
                timing = EffectTiming.Broadcast;
                return true;

            case "turnstart":
                timing = EffectTiming.TurnStart;
                return true;

            case "turnend":
                timing = EffectTiming.TurnEnd;
                return true;

            default:
                return false;
        }
    }

    private string NormalizeTimingText(string value)
    {
        return value
            .Trim()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "")
            .ToLowerInvariant();
    }

    private string GetTimingMismatchMessage(EffectTiming timing)
    {
        switch (timing)
        {
            case EffectTiming.PreCollab:
            case EffectTiming.PostCollab:
                return "현재 타이밍에 발동할 수 없는 카드입니다.";

            case EffectTiming.OnAppear:
            case EffectTiming.OnRest:
            case EffectTiming.Passive:
            case EffectTiming.CharacterActive:
            case EffectTiming.IdolActive:
            case EffectTiming.Broadcast:
            case EffectTiming.TurnStart:
            case EffectTiming.TurnEnd:
                return "현재 타이밍에 발동할 수 없는 카드입니다.";

            default:
                return "현재 타이밍에 발동할 수 없는 카드입니다.";
        }
    }

    private string GetOptionalEffectQuestionMessage(EffectTiming timing)
    {
        switch (timing)
        {
            case EffectTiming.PreCollab:
                return "합방 전에 발동할 카드를 선택하세요.";

            case EffectTiming.PostCollab:
                return "합방 후에 발동할 카드를 선택하세요.";

            case EffectTiming.OnAppear:
                return "출연 시 발동할 카드를 선택하세요.";

            case EffectTiming.OnRest:
                return "휴식 시 발동할 카드를 선택하세요.";

            case EffectTiming.Content:
                return "발동할 카드를 선택하세요.";

            default:
                return "발동할 카드를 선택하세요.";
        }
    }

    private string GetOptionalEffectCancelMessage(EffectTiming timing)
    {
        switch (timing)
        {
            case EffectTiming.PreCollab:
                return "합방 전 콘텐츠 카드 발동을 하지 않습니다.";

            case EffectTiming.PostCollab:
                return "합방 후 카드 효과를 발동하지 않습니다.";

            case EffectTiming.OnAppear:
                return "출연 시 카드 효과를 발동하지 않습니다.";

            case EffectTiming.OnRest:
                return "휴식 시 카드 효과를 발동하지 않습니다.";

            default:
                return "카드 발동을 하지 않습니다.";
        }
    }
}
