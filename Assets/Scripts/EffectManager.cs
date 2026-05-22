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

        if (request.sourceCard.kind != "Content")
        {
            failReason = "현재는 콘텐츠 카드 효과만 발동할 수 있습니다.";
            return false;
        }

        if (request.timing != EffectTiming.MainPhase)
        {
            failReason = "현재는 본방송 단계의 콘텐츠 카드만 발동할 수 있습니다.";
            return false;
        }

        if (request.owner != BattleSlotOwner.My)
        {
            failReason = "현재는 내 손패의 콘텐츠 카드만 발동할 수 있습니다.";
            return false;
        }

        if (request.consumeAction)
        {
            if (!battleManager.CanUseMyActionFromExternal(out failReason))
                return false;
        }

        if (!battleManager.IsCardInHandFromExternal(request.owner, request.sourceCard))
        {
            failReason = "손패에 있는 콘텐츠 카드만 발동할 수 있습니다.";
            return false;
        }

        int cost = GetActivationCost(request.sourceCard);
        if (cost > 0 && !battleManager.CanPayViewerCostFromExternal(request.owner, cost))
        {
            failReason = "시청자가 부족하여 효과를 발동할 수 없습니다.";
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
}
