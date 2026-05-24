using System;
using System.Collections.Generic;
using UnityEngine;

public enum EffectTiming
{
    OnAppear,
    MainPhase,
    BeforeCollab,
    AfterCollab,
    OnRest,
    Passive,
    IdolActive,
    Broadcast
}

public class EffectActivationRequest
{
    public BaseCardData sourceCard;
    public BattleSlotOwner owner;
    public EffectTiming timing;
    public BattleFieldSlot sourceSlot;
    public BattleFieldSlot targetSlot;
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

    public bool TryActivateEffect(EffectActivationRequest request)
    {
        string failReason;
        if (!CanActivateEffect(request, out failReason))
        {
            battleManager?.SetSystemMessageFromExternal(failReason);
            return false;
        }

        int cost = GetActivationCost(request.sourceCard);

        if (cost > 0 &&
            !battleManager.TryPayViewerCostFromExternal(request.owner, cost))
        {
            battleManager.SetSystemMessageFromExternal("시청자가 부족하여 효과를 발동할 수 없습니다.");
            return false;
        }

        if (!battleManager.MoveCardFromHandToRestZoneFromExternal(request.owner, request.sourceCard))
        {
            battleManager.SetSystemMessageFromExternal("효과 발동 카드를 손패에서 휴식존으로 이동할 수 없습니다.");
            return false;
        }

        battleManager.RefreshAllUIFromExternal();

        string message =
            $"{request.sourceCard.name} 콘텐츠 카드 효과를 발동했습니다.\n" +
            "효과 발동 성공: 실제 효과는 아직 미구현입니다.";

        if (cost > 0)
            message += $"\n시청자 -{cost}";

        if (request.consumeAction)
            battleManager.ResolveMyActionUsedFromExternal(message);
        else
            battleManager.SetSystemMessageFromExternal(message);

        return true;
    }

    public bool CanUseContentCardNow(
        BaseCardData card,
        BattleSlotOwner owner,
        out string failReason)
    {
        return CanUseCardAtTiming(card, EffectTiming.MainPhase, owner, out failReason);
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

        if (!IsContentCard(card))
        {
            failReason = "콘텐츠 카드만 사용할 수 있습니다.";
            return false;
        }

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

        if (owner != BattleSlotOwner.My)
        {
            failReason = "현재는 내 손패의 콘텐츠 카드만 발동할 수 있습니다.";
            return false;
        }

        if (!battleManager.IsCardInHandFromExternal(owner, card))
        {
            failReason = "손패에 있는 콘텐츠 카드만 발동할 수 있습니다.";
            return false;
        }

        EffectTiming cardTiming = ResolveContentCardTiming(card);

        if (cardTiming != timing)
        {
            failReason = GetTimingMismatchMessage(cardTiming);
            return false;
        }

        int cost = GetActivationCost(card);
        if (cost > 0 && !battleManager.CanPayViewerCostFromExternal(owner, cost))
        {
            failReason = "시청자가 부족합니다.";
            return false;
        }

        return true;
    }

    private bool CanActivateEffect(
        EffectActivationRequest request,
        out string failReason)
    {
        failReason = "";

        if (battleManager == null)
        {
            failReason = "BattleManager가 연결되어 있지 않습니다.";
            return false;
        }

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

        if (!IsContentCard(request.sourceCard))
        {
            failReason = "현재는 콘텐츠 카드 효과만 발동할 수 있습니다.";
            return false;
        }

        if (!CanUseCardAtTiming(request.sourceCard, request.timing, request.owner, out failReason))
            return false;

        if (request.timing != EffectTiming.MainPhase)
        {
            failReason = "현재는 본방송 단계의 콘텐츠 카드만 발동할 수 있습니다.";
            return false;
        }

        return true;
    }

    private int GetActivationCost(BaseCardData card)
    {
        ContentCardData content = card as ContentCardData;

        if (content == null)
            return 0;

        return Mathf.Max(0, content.cost);
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
                    timing == EffectTiming.MainPhase)
                {
                    return EffectTiming.MainPhase;
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
        return EffectTiming.MainPhase;
    }

    private bool TryResolveContentTypeTiming(string contentType, out EffectTiming timing)
    {
        timing = EffectTiming.MainPhase;

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
                timing = EffectTiming.MainPhase;
                return true;

            case "collab":
            case "collaboration":
            case "beforecollab":
            case "precollab":
                timing = EffectTiming.BeforeCollab;
                return true;

            case "aftercollab":
            case "postcollab":
                timing = EffectTiming.AfterCollab;
                return true;

            default:
                return false;
        }
    }

    private bool TryParseEffectTiming(string rawTiming, out EffectTiming timing)
    {
        timing = EffectTiming.MainPhase;

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
                timing = EffectTiming.MainPhase;
                return true;

            case "beforecollab":
            case "precollab":
            case "collab":
            case "collaboration":
                timing = EffectTiming.BeforeCollab;
                return true;

            case "aftercollab":
            case "postcollab":
                timing = EffectTiming.AfterCollab;
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
            case EffectTiming.BeforeCollab:
            case EffectTiming.AfterCollab:
                return "이 카드는 합방 타이밍에만 사용할 수 있습니다.";

            case EffectTiming.OnAppear:
            case EffectTiming.OnRest:
            case EffectTiming.Passive:
            case EffectTiming.IdolActive:
            case EffectTiming.Broadcast:
                return "아직 구현되지 않은 발동 타이밍입니다.";

            default:
                return "지금은 사용할 수 없는 카드입니다.";
        }
    }
}
