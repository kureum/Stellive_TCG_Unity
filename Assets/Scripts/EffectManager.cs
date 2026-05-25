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
    public BattleFieldSlot attackerSlot;
    public BattleFieldSlot defenderSlot;
    public BaseCardData sourceCard;
    public BaseCardData targetCard;
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
            case EffectTiming.OnAppear:
            case EffectTiming.OnRest:
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
        EffectContext safeContext = NormalizeContext(context, timing);
        List<EffectCandidate> candidates = GetPlayableEffects(timing, safeContext);

        if (candidates.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        CardQuestionPanel panel = battleManager != null
            ? battleManager.BattleCardQuestionPanel
            : null;

        if (panel == null)
        {
            battleManager?.SetSystemMessageFromExternal("발동 가능한 카드가 있지만 CardQuestionPanel이 연결되어 있지 않습니다.");
            Debug.LogWarning($"EffectManager: {timing} 후보 {candidates.Count}장을 감지했지만 CardQuestionPanel이 없습니다.");
            onComplete?.Invoke();
            return;
        }

        if (panel.IsOpen())
        {
            battleManager?.SetSystemMessageFromExternal("이미 카드 선택창이 열려 있습니다.");
            onComplete?.Invoke();
            return;
        }

        List<CardQuestionOption> options = BuildOptionsFromCandidates(candidates);
        bool opened = panel.TryShowOptions(
            GetOptionalEffectQuestionMessage(timing),
            options,
            true,
            selectedOption =>
            {
                EffectCandidate selectedCandidate = selectedOption != null
                    ? selectedOption.linkedCandidate
                    : null;
                ResolveEffect(selectedCandidate, safeContext, onComplete);
            },
            () =>
            {
                battleManager?.SetSystemMessageFromExternal(GetOptionalEffectCancelMessage(timing));
                onComplete?.Invoke();
            }
        );

        if (!opened)
        {
            battleManager?.SetSystemMessageFromExternal("카드 선택창을 열 수 없습니다.");
            onComplete?.Invoke();
        }
    }

    public void ResolveEffect(
        EffectCandidate candidate,
        EffectContext context,
        Action onComplete)
    {
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
            candidate.card,
            candidate.owner,
            candidate.refId,
            candidate.consumeAction
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
            refId = GetPrimaryEffectRef(card),
            timing = ResolveContentCardTiming(card),
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
                refId = GetPrimaryEffectRef(card),
                timing = ResolveContentCardTiming(card),
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

        if (!IsContentCard(candidate.card))
        {
            failReason = "현재는 콘텐츠 카드 효과만 발동할 수 있습니다.";
            return false;
        }

        if (candidate.sourceZone == EffectSourceZone.Hand &&
            !battleManager.IsCardInHandAtIndexFromExternal(candidate.owner, candidate.handIndex, candidate.card))
        {
            failReason = "손패에 있는 콘텐츠 카드만 발동할 수 있습니다.";
            return false;
        }

        EffectContext safeContext = NormalizeContext(context, candidate.timing);
        EffectTiming requestedTiming = safeContext.timing;

        if (candidate.timing != requestedTiming)
        {
            failReason = GetTimingMismatchMessage(candidate.timing);
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
            : GetPrimaryEffectRef(candidate.card);
        int cost = GetActivationCost(candidate.card);

        if (effectRef == "content.silenceCharacterCollabThisTurn")
        {
            return battleManager.TryStartSilenceCharacterCollabThisTurnFromExternal(
                candidate.card,
                candidate.owner,
                cost,
                candidate.consumeAction,
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
            candidate.card,
            candidate.owner,
            effectRef,
            candidate.consumeAction
        );

        return true;
    }

    private void ExecuteEffectByRefInternal(
        BaseCardData sourceCard,
        BattleSlotOwner owner,
        string effectRef,
        bool consumeAction)
    {
        string message = ResolveImmediateContentEffectMessage(sourceCard, owner, effectRef);
        int cost = GetActivationCost(sourceCard);

        if (cost > 0)
            message += $"\n시청자 -{cost}";

        if (consumeAction)
            battleManager.ResolveMyActionUsedFromExternal(message);
        else
            battleManager.SetSystemMessageFromExternal(message);
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
            consumeAction = request.consumeAction
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
            refId = GetPrimaryEffectRef(request.sourceCard),
            timing = request.timing,
            consumeAction = request.consumeAction
        };

        return true;
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

        if (safeContext.timing == EffectTiming.None)
            safeContext.timing = timing;

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

    private string ResolveImmediateContentEffectMessage(
        BaseCardData sourceCard,
        BattleSlotOwner owner,
        string effectRef)
    {
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

    private string GetPrimaryEffectRef(BaseCardData card)
    {
        ContentCardData content = card as ContentCardData;

        if (content == null || content.effects == null)
            return "";

        foreach (EffectData effect in content.effects)
        {
            string effectRef = GetEffectRef(effect);

            if (!string.IsNullOrEmpty(effectRef))
                return effectRef;
        }

        return "";
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

    private EffectTiming ResolveContentCardTiming(BaseCardData card)
    {
        ContentCardData content = card as ContentCardData;

        if (content != null && content.effects != null)
        {
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

            default:
                return "카드 발동을 하지 않습니다.";
        }
    }
}
