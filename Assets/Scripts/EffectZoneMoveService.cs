using System;
using System.Collections.Generic;

public enum EffectZone
{
    None,
    Deck,
    Hand,
    Rest,
    FieldCharacter,
    FieldContent,
    BroadcastDeck,
    BroadcastSlot
}

public enum ZoneMoveReason
{
    Effect,
    Cost,
    Summon,
    Rest,
    BattleKO,
    ContentUsed,
    ReturnToDeck,
    Debug
}

public enum DeckInsertPosition
{
    Top,
    Bottom,
    Shuffle
}

[Serializable]
public class ZoneMoveRequest
{
    public BattleSlotOwner owner;
    public EffectZone fromZone = EffectZone.None;
    public EffectZone toZone = EffectZone.None;
    public BaseCardData card;
    public BattleFieldSlot fromSlot;
    public BattleFieldSlot toSlot;
    public int handIndex = -1;
    public bool faceDown;
    public ZoneMoveReason reason = ZoneMoveReason.Effect;
    public DeckInsertPosition deckInsertPosition = DeckInsertPosition.Bottom;
    public bool shuffleDeckAfterMove;
}

public class ZoneMoveResult
{
    public bool success;
    public string message;
    public BattleSlotOwner owner;
    public EffectZone fromZone;
    public EffectZone toZone;
    public BaseCardData movedCard;
    public string cardId;
    public BattleFieldSlot fromSlot;
    public BattleFieldSlot toSlot;
    public int fromX = -1;
    public int fromY = -1;
    public int toX = -1;
    public int toY = -1;
    public ZoneMoveReason reason;

    public static ZoneMoveResult Fail(ZoneMoveRequest request, string message)
    {
        return Create(request, false, message, request != null ? request.card : null);
    }

    public static ZoneMoveResult Success(ZoneMoveRequest request, string message, BaseCardData movedCard)
    {
        return Create(request, true, message, movedCard);
    }

    private static ZoneMoveResult Create(
        ZoneMoveRequest request,
        bool success,
        string message,
        BaseCardData movedCard)
    {
        BattleFieldSlot fromSlot = request != null ? request.fromSlot : null;
        BattleFieldSlot toSlot = request != null ? request.toSlot : null;

        return new ZoneMoveResult
        {
            success = success,
            message = message,
            owner = request != null ? request.owner : BattleSlotOwner.My,
            fromZone = request != null ? request.fromZone : EffectZone.None,
            toZone = request != null ? request.toZone : EffectZone.None,
            movedCard = movedCard,
            cardId = movedCard != null ? movedCard.id : "",
            fromSlot = fromSlot,
            toSlot = toSlot,
            fromX = fromSlot != null ? fromSlot.x : -1,
            fromY = fromSlot != null ? fromSlot.y : -1,
            toX = toSlot != null ? toSlot.x : -1,
            toY = toSlot != null ? toSlot.y : -1,
            reason = request != null ? request.reason : ZoneMoveReason.Effect
        };
    }
}

public static class EffectZoneMoveService
{
    public static ZoneMoveResult MoveCardBetweenZones(
        ZoneMoveRequest request,
        EffectContext context)
    {
        if (request == null)
            return ZoneMoveResult.Fail(null, "이동 요청 정보가 없습니다.");

        BattleManager battleManager = context != null ? context.battleManager : null;

        if (battleManager == null)
            return ZoneMoveResult.Fail(request, "BattleManager가 연결되어 있지 않습니다.");

        switch (request.fromZone)
        {
            case EffectZone.Deck:
                if (request.toZone == EffectZone.Hand)
                    return MoveDeckToHand(request, battleManager);
                break;

            case EffectZone.Hand:
                if (request.toZone == EffectZone.Rest)
                    return MoveHandToRest(request, battleManager);
                if (request.toZone == EffectZone.FieldCharacter)
                    return MoveHandOrRestToFieldCharacter(request, battleManager);
                break;

            case EffectZone.Rest:
                if (request.toZone == EffectZone.Deck)
                    return MoveRestToDeck(request, battleManager);
                if (request.toZone == EffectZone.FieldCharacter)
                    return MoveHandOrRestToFieldCharacter(request, battleManager);
                break;

            case EffectZone.FieldCharacter:
                if (request.toZone == EffectZone.Rest)
                    return MoveFieldCharacterToRest(request, battleManager);
                break;

            case EffectZone.FieldContent:
                if (request.toZone == EffectZone.Rest)
                    return MoveFieldContentToRest(request, battleManager);
                break;
        }

        return ZoneMoveResult.Fail(
            request,
            $"지원하지 않는 이동입니다: {request.fromZone} -> {request.toZone}");
    }

    public static List<ZoneMoveResult> MoveCardsBetweenZones(
        List<ZoneMoveRequest> requests,
        EffectContext context)
    {
        List<ZoneMoveResult> results = new List<ZoneMoveResult>();

        if (requests == null)
            return results;

        foreach (ZoneMoveRequest request in requests)
            results.Add(MoveCardBetweenZones(request, context));

        return results;
    }

    private static ZoneMoveResult MoveHandToRest(
        ZoneMoveRequest request,
        BattleManager battleManager)
    {
        BaseCardData card = request.card;

        bool moved = request.handIndex >= 0
            ? battleManager.MoveHandCardAtIndexToRestZoneFromExternal(request.owner, request.handIndex, card)
            : battleManager.MoveCardFromHandToRestZoneFromExternal(request.owner, card);

        if (!moved)
            return ZoneMoveResult.Fail(request, "손패에서 휴식존으로 이동할 수 없습니다.");

        battleManager.RefreshAllUIFromExternal();
        return ZoneMoveResult.Success(request, "손패에서 휴식존으로 이동했습니다.", card);
    }

    private static ZoneMoveResult MoveDeckToHand(
        ZoneMoveRequest request,
        BattleManager battleManager)
    {
        BaseCardData card = request.card;

        if (card == null)
            return ZoneMoveResult.Fail(request, "덱에서 패로 이동할 카드 정보가 없습니다.");

        if (!battleManager.RemoveCardFromMainDeckFromExternal(request.owner, card))
            return ZoneMoveResult.Fail(request, "덱에서 카드를 찾을 수 없습니다.");

        battleManager.AddCardToHandFromExternal(request.owner, card);
        battleManager.RefreshAllUIFromExternal();

        return ZoneMoveResult.Success(request, "덱에서 패로 이동했습니다.", card);
    }

    private static ZoneMoveResult MoveRestToDeck(
        ZoneMoveRequest request,
        BattleManager battleManager)
    {
        BaseCardData card = request.card;

        if (!battleManager.RemoveCardFromRestZoneFromExternal(request.owner, card))
            return ZoneMoveResult.Fail(request, "휴식존에서 카드를 찾을 수 없습니다.");

        battleManager.AddCardToMainDeckFromExternal(
            request.owner,
            card,
            request.deckInsertPosition,
            request.shuffleDeckAfterMove || request.deckInsertPosition == DeckInsertPosition.Shuffle);
        battleManager.RefreshAllUIFromExternal();

        return ZoneMoveResult.Success(request, "휴식존에서 덱으로 이동했습니다.", card);
    }

    private static ZoneMoveResult MoveFieldCharacterToRest(
        ZoneMoveRequest request,
        BattleManager battleManager)
    {
        BattleFieldSlot fromSlot = request.fromSlot;

        if (fromSlot == null || !fromSlot.HasCharacter || fromSlot.characterCard == null)
            return ZoneMoveResult.Fail(request, "필드 캐릭터 슬롯 정보가 없습니다.");

        BaseCardData card = fromSlot.characterCard;
        request.card = card;
        request.owner = fromSlot.characterOwner;

        battleManager.AddFieldCharacterToRestZoneFromExternal(fromSlot);
        fromSlot.ClearCharacterCard();
        battleManager.RefreshAllUIFromExternal();

        return ZoneMoveResult.Success(request, "필드 캐릭터를 휴식존으로 이동했습니다.", card);
    }

    private static ZoneMoveResult MoveHandOrRestToFieldCharacter(
        ZoneMoveRequest request,
        BattleManager battleManager)
    {
        ZoneMoveResult validation = ValidateFieldCharacterDestination(request);

        if (!validation.success)
            return validation;

        BaseCardData card = request.card;
        UnityEngine.Sprite sprite = request.faceDown
            ? battleManager.GetCardBackSpriteFromExternal()
            : battleManager.LoadCardSpriteFromExternal(card);

        if (sprite == null)
            return ZoneMoveResult.Fail(request, "출연시킬 카드 이미지를 찾을 수 없습니다.");

        bool removedFromSource;

        if (request.fromZone == EffectZone.Hand)
        {
            removedFromSource = request.handIndex >= 0
                ? battleManager.RemoveHandCardAtIndexFromExternal(request.owner, request.handIndex, card)
                : battleManager.RemoveCardFromHandFromExternal(request.owner, card);
        }
        else
        {
            removedFromSource = battleManager.RemoveCardFromRestZoneFromExternal(request.owner, card);
        }

        if (!removedFromSource)
            return ZoneMoveResult.Fail(request, "출연시킬 카드를 원래 존에서 제거할 수 없습니다.");

        request.toSlot.SetCharacterCard(
            card,
            sprite,
            request.faceDown,
            request.owner);

        int turn = battleManager.GetCurrentTurnCountFromExternal();
        if (request.faceDown)
            request.toSlot.faceDownSummonedTurn = turn;
        else
            request.toSlot.faceUpSummonedTurn = turn;

        battleManager.RefreshAllUIFromExternal();
        return ZoneMoveResult.Success(request, "캐릭터를 필드에 출연시켰습니다.", card);
    }

    private static ZoneMoveResult ValidateFieldCharacterDestination(ZoneMoveRequest request)
    {
        if (request.card == null)
            return ZoneMoveResult.Fail(request, "출연시킬 카드 정보가 없습니다.");

        if (!(request.card is CharacterCardData))
            return ZoneMoveResult.Fail(request, "캐릭터 카드만 필드 캐릭터 슬롯으로 이동할 수 있습니다.");

        if (request.toSlot == null)
            return ZoneMoveResult.Fail(request, "출연 위치가 없습니다.");

        if (!request.toSlot.HasBroadcast)
            return ZoneMoveResult.Fail(request, "방송 카드가 있는 슬롯에만 출연할 수 있습니다.");

        if (request.toSlot.HasCharacter)
            return ZoneMoveResult.Fail(request, "이미 캐릭터가 있는 슬롯에는 출연할 수 없습니다.");

        return ZoneMoveResult.Success(request, "", request.card);
    }

    private static ZoneMoveResult MoveFieldContentToRest(
        ZoneMoveRequest request,
        BattleManager battleManager)
    {
        BattleFieldSlot fromSlot = request.fromSlot;

        if (fromSlot == null || !fromSlot.HasContent || fromSlot.contentCard == null)
            return ZoneMoveResult.Fail(request, "필드 콘텐츠 슬롯 정보가 없습니다.");

        BaseCardData card = fromSlot.contentCard;
        request.card = card;
        request.owner = fromSlot.contentOwner;

        battleManager.AddCardToRestZoneFromExternal(request.owner, card);
        fromSlot.ClearContentCardWithFade();
        battleManager.RefreshAllUIFromExternal();

        return ZoneMoveResult.Success(request, "필드 콘텐츠를 휴식존으로 이동했습니다.", card);
    }
}
