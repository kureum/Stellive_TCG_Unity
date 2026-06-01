using System;
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
}

public class EffectManager : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;

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
            case "character.rest.gainViewers":
            case "character.rest.loseViewers":
            case "content.postCollabHealOwnParticipant":
            case "content.removeAllLastingContentsOnBoard":
            case "content.lasting.buffTagTensionAndHp":
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

    private bool IsMandatoryEffectActivation(
        EffectTiming timing,
        EffectCandidate candidate)
    {
        if (candidate == null || candidate.card == null)
            return false;

        return timing == EffectTiming.OnRest &&
            candidate.card is CharacterCardData;
    }

    public void ResolveEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action onComplete)
    {
        if (candidate != null && IsDrawThenDiscardEffect(candidate, context))
        {
            ResolveDrawThenDiscardEffect(candidate, context, onComplete);
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

        return true;
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

        if (cost > 0 &&
            !battleManager.TryPayViewerCostFromExternal(candidate.owner, cost))
        {
            failReason = "시청자가 부족하여 효과를 발동할 수 없습니다.";
            return false;
        }

        if (candidate.sourceZone == EffectSourceZone.Hand &&
            !battleManager.MoveHandCardAtIndexToRestZoneFromExternal(candidate.owner, candidate.handIndex, candidate.card))
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
            !battleManager.MoveHandCardAtIndexToRestZoneFromExternal(candidate.owner, candidate.handIndex, candidate.card))
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

                if (discardCard == null ||
                    !battleManager.MoveHandCardAtIndexToRestZoneFromExternal(owner, handIndex, discardCard))
                {
                    CompleteEffectResolution(
                        $"{accumulatedMessage}\n선택한 카드를 버릴 수 없습니다.",
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
                return ModifyViewers(candidate.owner, GetIntParam(effect, "amount", 0));

            case "character.rest.loseViewers":
                return ModifyViewers(candidate.owner, -GetIntParam(effect, "amount", 0));

            case "content.drawThenDiscard":
                return DrawThenDiscard(
                    candidate.owner,
                    GetIntParam(effect, "draw", 0),
                    GetIntParam(effect, "discard", 0)
                );

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
            sourceZone = request.sourceSlot != null ? EffectSourceZone.Field : EffectSourceZone.Hand,
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
        return timing == EffectTiming.Content && requestedConsumeAction;
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

        if (content == null)
            return 0;

        return Mathf.Max(0, content.cost);
    }

    private string ModifyViewers(BattleSlotOwner owner, int delta)
    {
        int actualDelta = battleManager.ModifyViewersFromExternal(owner, delta);
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

    private string HealOwnCollabParticipant(
        BattleSlotOwner owner,
        EffectContext context,
        int amount)
    {
        BattleFieldSlot targetSlot = GetOwnCollabParticipant(owner, context);

        if (targetSlot == null)
            return "회복할 합방 참가 캐릭터를 찾지 못했습니다.";

        int healedAmount = battleManager.HealCharacterFromExternal(targetSlot, Mathf.Max(0, amount));
        battleManager.RefreshAllUIFromExternal();

        string cardName = targetSlot.characterCard != null
            ? targetSlot.characterCard.name
            : "선택 캐릭터";

        return $"{cardName}의 체력을 {healedAmount} 회복했습니다.";
    }

    private BattleFieldSlot GetOwnCollabParticipant(
        BattleSlotOwner owner,
        EffectContext context)
    {
        if (context == null)
            return null;

        if (IsOwnedActiveCharacterSlot(context.attackerSlot, owner))
            return context.attackerSlot;

        if (IsOwnedActiveCharacterSlot(context.defenderSlot, owner))
            return context.defenderSlot;

        return null;
    }

    private bool IsOwnedActiveCharacterSlot(BattleFieldSlot slot, BattleSlotOwner owner)
    {
        return slot != null &&
            slot.HasCharacter &&
            !slot.isCharacterFaceDown &&
            slot.characterOwner == owner;
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
            case "range":
                return effectParams.range;
            case "reveal":
                return effectParams.reveal;
            case "extraCostPer":
                return effectParams.extraCostPer;
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
                timing == EffectTiming.PostCollab;
        }

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

            case "idolactive":
            case "active":
                timing = EffectTiming.IdolActive;
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
                return "이 카드는 합방 타이밍에만 사용할 수 있습니다.";

            case EffectTiming.OnAppear:
            case EffectTiming.OnRest:
            case EffectTiming.Passive:
            case EffectTiming.IdolActive:
            case EffectTiming.Broadcast:
            case EffectTiming.TurnStart:
            case EffectTiming.TurnEnd:
                return "아직 구현되지 않은 발동 타이밍입니다.";

            default:
                return "지금은 사용할 수 없는 카드입니다.";
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
